using Dino_Defenders.Client;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Engine.Core;
using Dino_Engine.GUI;
using Dino_Engine.GUI.Rendering;
using Dino_Engine.Util;
using Dino_Engine.World;
using OpenTK.Mathematics;
using System.Globalization;

namespace Dino_Defenders.UI
{
    /// <summary>
    /// The Archive's records, read off its displays: walk up to one and a card rises over it saying what it keeps, for all of this player's heroes together and for the one being
    /// played (HALLS.md). Only the nearest display within reach speaks, and it fades in and out rather than popping. The numbers are this player's own: the hero being played as the
    /// server last told it, and every other hero on this machine as their saves have them (<see cref="Others"/>).
    /// </summary>
    internal sealed class ArchivePlaques : GuiElement
    {
        private const float Reach = 7.5f;           // Metres from a display's middle that it speaks.
        private const float FadeSeconds = 0.22f;
        private const float CardWidth = 380f;
        private const float Above = 1.6f;           // Metres over the top of the display that the card's stem ends.
        private const float StemLength = 18f;

        /// <summary>Every other hero on this machine, as their saves have them, for the records of them all. Empty when it isn't this player's own heroes being played, as in a scripted run.</summary>
        public static IReadOnlyList<HeroState> Others = Array.Empty<HeroState>();

        private ExhibitKind? _showing;
        private float _shown;

        public ArchivePlaques()
        {
            Width = UIValue.Fill;
            Height = UIValue.Fill;
            HitTest = HitTestMode.None;
        }

        protected override void OnDraw(GuiDrawList draw)
        {
            GameSession session = GameSession.Current;
            if (session == null || Context == null || !session.IsHaven) return;
            WorldHall? archive = session.World.Halls.FirstOrDefault(hall => hall.Kind == HallKind.Archive);
            if (archive == null) return;

            // The nearest display in reach, if the hero's in the Archive at all.
            Vector2 hero = session.PlayerPosition;
            HallExhibit? nearest = null;
            float best = Reach;
            if (archive.Contains(hero, 0.5f))
            {
                foreach (HallExhibit exhibit in archive.Design.Exhibits)
                {
                    float distance = (archive.ToWorld(exhibit.At) - hero).Length;
                    if (distance >= best) continue;
                    best = distance;
                    nearest = exhibit;
                }
            }

            // One at a time: the one going fades out before the next comes in.
            float step = Engine.Delta / FadeSeconds;
            if (nearest is HallExhibit wanted && wanted.Kind == _showing) _shown = MathF.Min(_shown + step, 1f);
            else _shown = MathF.Max(_shown - step, 0f);
            if (_shown <= 0f) _showing = nearest?.Kind;
            if (_showing is not ExhibitKind showing || _shown <= 0f) return;

            HallExhibit shown = archive.Design.Exhibits.First(e => e.Kind == showing);
            float top = archive.Design.Pieces.Where(p => p.Kind == PieceKind.Plinth && p.Variant == (int)showing).Select(p => p.Height).DefaultIfEmpty(3f).First();
            Vector2 at2 = archive.ToWorld(shown.At);
            Vector3 anchor = new Vector3(at2.X, archive.Floor + top + Above, at2.Y);
            if (!Hud.Project(anchor, Bounds, out Vector2 point)) return;
            DrawCard(draw, point, Card(showing, session), Smooth(_shown));
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        private readonly record struct Plaque(string Title, string Value, string Mine, string Words);

        private void DrawCard(GuiDrawList draw, Vector2 point, Plaque plaque, float alpha)
        {
            GuiContext context = Context!;
            float rise = (1f - alpha) * 10f;
            float height = 132f;
            var rect = new GuiRect(point.X - CardWidth * 0.5f, point.Y - StemLength - height + rise, CardWidth, height);
            // Keep it on the screen, whatever the camera's doing.
            rect = new GuiRect(Math.Clamp(rect.X, 12f, MathF.Max(Bounds.Width - CardWidth - 12f, 12f)), MathF.Max(rect.Y, 12f), rect.Width, rect.Height);

            draw.AddRect(new GuiRect(point.X - 1f, rect.Y + rect.Height, 2f, MathF.Max(point.Y - rect.Y - rect.Height, 0f)), Ui.Gold.MultiplyAlpha(0.7f * alpha));
            draw.AddRect(rect, GuiColors.Rgba(14, 11, 8, (int)(222 * alpha)), 6f, 1.2f, Ui.Gold.MultiplyAlpha(0.65f * alpha));
            draw.AddRect(new GuiRect(rect.X + 14f, rect.Y + 40f, rect.Width - 28f, 1f), Ui.Gold.MultiplyAlpha(0.35f * alpha));

            Ui.Text(context, draw, plaque.Title, 19f, new GuiRect(rect.X, rect.Y + 8f, rect.Width, 26f), Ui.Gold.MultiplyAlpha(alpha), Alignment.Center);
            Ui.Text(context, draw, plaque.Value, 30f, new GuiRect(rect.X, rect.Y + 46f, rect.Width, 36f), Ui.Parchment.MultiplyAlpha(alpha), Alignment.Center);
            Ui.Text(context, draw, plaque.Mine, 14f, new GuiRect(rect.X, rect.Y + 84f, rect.Width, 18f), Ui.Parchment.MultiplyAlpha(0.85f * alpha), Alignment.Center);
            Ui.Text(context, draw, plaque.Words, 13f, new GuiRect(rect.X + 10f, rect.Y + 104f, rect.Width - 20f, 18f), Ui.Faded.MultiplyAlpha(alpha), Alignment.Center);
        }

        // What a display says: the record of all this player's heroes, this hero's own beneath it, and a line of what it means.
        private static Plaque Card(ExhibitKind kind, GameSession session)
        {
            HeroState me = session.Hero;
            var everyone = new List<HeroState>(Others.Count + 1) { me };
            everyone.AddRange(Others);
            switch (kind)
            {
                case ExhibitKind.FoesSlain:
                    return new Plaque("Foes Slain", Count(everyone.Sum(h => h.Records.Slain)), $"{Mine(session)}{Count(me.Records.Slain)}", "Every one of them the Reich's, or its doing.");
                case ExhibitKind.HardestBlow:
                {
                    HeroState strongest = everyone.OrderByDescending(h => h.Records.HardestBlow).First();
                    string by = strongest.Records.HardestBlow > 0f && strongest != me ? $"Struck by {NameOf(strongest)}." : "One blow, and the stone gave way.";
                    return new Plaque("Hardest Blow", Count(strongest.Records.HardestBlow), $"{Mine(session)}{Count(me.Records.HardestBlow)}", by);
                }
                case ExhibitKind.TyrantsFelled:
                    return new Plaque("Tyrants Felled", Count(everyone.Sum(h => h.Records.TyrantsSlain)), $"{Mine(session)}{Count(me.Records.TyrantsSlain)}", "Each one held a land.");
                case ExhibitKind.Falls:
                    return new Plaque("Fallen", Count(everyone.Sum(h => h.Records.Falls)), $"{Mine(session)}{Count(me.Records.Falls)}", "Every time, someone got up again.");
                case ExhibitKind.SwiftestLand:
                {
                    HeroState? swiftest = everyone.Where(h => h.Records.SwiftestLand > 0f).OrderBy(h => h.Records.SwiftestLand).FirstOrDefault();
                    string value = swiftest == null ? "None yet" : Time(swiftest.Records.SwiftestLand);
                    string words = swiftest == null ? "Free a land in one go, and it's written here." : $"Land level {swiftest.Records.SwiftestLevel}, freed by {NameOf(swiftest)}.";
                    return new Plaque("Swiftest Land", value, $"{Mine(session)}{(me.Records.SwiftestLand > 0f ? Time(me.Records.SwiftestLand) : "none yet")}", words);
                }
                case ExhibitKind.UniquesFound:
                {
                    int all = UniqueCatalog.All.Count;
                    int found = everyone.SelectMany(h => h.Records.UniquesFound).Distinct().Count();
                    return new Plaque("Uniques Found", $"{Count(found)} of {Count(all)}", $"{Mine(session)}{Count(me.Records.UniquesFound.Count)}", "No two alike, and every one a trade.");
                }
                case ExhibitKind.LandsFreed:
                    return new Plaque("Lands Freed", Count(everyone.Sum(h => h.WorldsCleared)), $"{Mine(session)}{Count(me.WorldsCleared)}", "The map remembers every one.");
                case ExhibitKind.DeepestLand:
                {
                    HeroState deepest = everyone.OrderByDescending(h => h.BestLand).First();
                    string words = deepest.BestLand > 0 && deepest != me ? $"Reached by {NameOf(deepest)}." : "Every tenth step, a tyrant waits.";
                    return new Plaque("Deepest Land", deepest.BestLand > 0 ? $"Level {Count(deepest.BestLand)}" : "None yet", $"{Mine(session)}{(me.BestLand > 0 ? $"level {Count(me.BestLand)}" : "none yet")}", words);
                }
                default:
                    return new Plaque("Gold Gathered", Count(everyone.Sum(h => h.Records.GoldGathered)), $"{Mine(session)}{Count(me.Records.GoldGathered)}", "Most of it spent, and well.");
            }
        }

        private static string Mine(GameSession session) => $"{(string.IsNullOrWhiteSpace(session.Hero.Name) ? "This hero" : session.Hero.Name)}: ";

        private static string NameOf(HeroState hero) => string.IsNullOrWhiteSpace(hero.Name) ? "another hero" : hero.Name;

        private static string Count(double value) => Math.Round(value).ToString("N0", CultureInfo.InvariantCulture);

        private static string Time(float seconds)
        {
            int whole = (int)MathF.Round(seconds);
            return whole >= 3600 ? $"{whole / 3600}:{whole / 60 % 60:00}:{whole % 60:00}" : $"{whole / 60}:{whole % 60:00}";
        }
    }
}

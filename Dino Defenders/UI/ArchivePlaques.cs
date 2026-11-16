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
    /// The Archive's records, read off its displays (HALLS.md). While the hero's in the Archive every display has its name and its number over it, small, so that the whole
    /// of what's kept there is read from the door; walk up to one and that opens into its card, for all of this player's heroes together and for the one being played.
    /// Only the nearest display within reach opens, and everything fades in and out rather than popping. A card stands over the far edge of its display, which from a
    /// camera that looks north is clear of it. The numbers are this player's own: the hero being played as the server last told it, and every other hero on this machine
    /// as their saves have them (<see cref="Others"/>).
    /// </summary>
    internal sealed class ArchivePlaques : GuiElement
    {
        private const float Reach = 7.5f;           // Metres from a display's middle that it speaks.
        private const float FadeSeconds = 0.22f;
        private const float CardWidth = 380f;
        private const float Above = 1.6f;           // Metres over the top of the display that the card's stem ends.
        private const float StemLength = 18f;
        private const float CaptionFromFoot = 176f; // Pixels from the foot of the screen to the foot of a card shown as a caption, which clears the action bar.
        private const float TagWidth = 152f;
        private const float TagHeight = 44f;
        private const float TagStem = 8f;
        private const float TagSeconds = 0.5f;      // Between readings of what the tags say, which changes with nothing the hero does in here.

        /// <summary>Every other hero on this machine, as their saves have them, for the records of them all. Empty when it isn't this player's own heroes being played, as in a scripted run.</summary>
        public static IReadOnlyList<HeroState> Others = Array.Empty<HeroState>();

        private ExhibitKind? _showing;
        private float _shown;
        private float _inside;
        private float _tagAge = float.MaxValue;
        private readonly Dictionary<ExhibitKind, Plaque> _tags = new();

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
            bool inside = archive.Contains(hero, 0.5f);
            HallExhibit? nearest = null;
            float best = Reach;
            if (inside)
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
            _inside = Math.Clamp(_inside + (inside ? step : -step) * 0.6f, 0f, 1f);
            if (_inside <= 0f) return;

            // What every display says in brief, read again now and then.
            _tagAge += Engine.Delta;
            if (_tagAge >= TagSeconds || _tags.Count == 0)
            {
                _tagAge = 0f;
                foreach (HallExhibit exhibit in archive.Design.Exhibits) _tags[exhibit.Kind] = Card(exhibit.Kind, session);
            }
            foreach (HallExhibit exhibit in archive.Design.Exhibits)
            {
                float opened = exhibit.Kind == _showing ? Smooth(_shown) : 0f;
                float alpha = Smooth(_inside) * (1f - opened);
                if (alpha <= 0.01f || !_tags.TryGetValue(exhibit.Kind, out Plaque tag)) continue;
                // Only over a display that's in the picture: one pinned to the edge of the screen would point at nothing.
                if (!Hud.Project(AnchorOf(archive, exhibit), Bounds, out Vector2 at)) continue;
                if (at.X < TagWidth * 0.5f || at.X > Bounds.Width - TagWidth * 0.5f || at.Y < -220f || at.Y > Bounds.Height - 150f) continue;
                DrawTag(draw, at, tag, alpha);
            }

            if (_showing is not ExhibitKind showing || _shown <= 0f) return;
            HallExhibit shown = archive.Design.Exhibits.First(e => e.Kind == showing);
            if (!Hud.Project(AnchorOf(archive, shown), Bounds, out Vector2 point)) return;
            DrawCard(draw, point, Card(showing, session), Smooth(_shown) * Smooth(_inside));
        }

        // Where a display's card hangs from: over its far edge, as the camera looks north, so that what's written is drawn past the display and never across it.
        private static Vector3 AnchorOf(WorldHall archive, HallExhibit exhibit)
        {
            float top = 3f;
            Vector2 half = Vector2.Zero;
            foreach (HallPiece piece in archive.Design.Pieces)
            {
                if (piece.Kind != PieceKind.Plinth || piece.Variant != (int)exhibit.Kind) continue;
                top = piece.Height;
                half = piece.Half;
                break;
            }
            float far = MathF.Abs(MathF.Cos(exhibit.Yaw)) * half.Y + MathF.Abs(MathF.Sin(exhibit.Yaw)) * half.X;
            Vector2 at = archive.ToWorld(exhibit.At - Vector2.UnitY * far);
            return new Vector3(at.X, archive.Floor + top + Above, at.Y);
        }

        // A display's name and number, small, over it.
        private void DrawTag(GuiDrawList draw, Vector2 point, Plaque plaque, float alpha)
        {
            GuiContext context = Context!;
            var rect = new GuiRect(point.X - TagWidth * 0.5f, point.Y - TagStem - TagHeight, TagWidth, TagHeight);
            rect = new GuiRect(Math.Clamp(rect.X, 8f, MathF.Max(Bounds.Width - TagWidth - 8f, 8f)), MathF.Max(rect.Y, 8f), rect.Width, rect.Height);
            draw.AddRect(new GuiRect(point.X - 1f, rect.Y + rect.Height, 2f, MathF.Max(point.Y - rect.Y - rect.Height, 0f)), Ui.Gold.MultiplyAlpha(0.55f * alpha));
            draw.AddRect(rect, GuiColors.Rgba(14, 11, 8, (int)(176 * alpha)), 5f, 1f, Ui.Gold.MultiplyAlpha(0.45f * alpha));
            Ui.Text(context, draw, plaque.Title, 12.5f, new GuiRect(rect.X, rect.Y + 4f, rect.Width, 16f), Ui.Gold.MultiplyAlpha(0.92f * alpha), Alignment.Center);
            Ui.Text(context, draw, plaque.Value, 16.5f, new GuiRect(rect.X, rect.Y + 19f, rect.Width, 22f), Ui.Parchment.MultiplyAlpha(alpha), Alignment.Center);
        }
        private static float Smooth(float t) => t * t * (3f - 2f * t);

        private readonly record struct Plaque(string Title, string Value, string Mine, string Words);

        private void DrawCard(GuiDrawList draw, Vector2 point, Plaque plaque, float alpha)
        {
            GuiContext context = Context!;
            float rise = (1f - alpha) * 10f;
            float height = 132f;
            var rect = new GuiRect(point.X - CardWidth * 0.5f, point.Y - StemLength - height + rise, CardWidth, height);
            // Keep it on the screen, whatever the camera's doing. With the camera drawn in close there may be no room over the display: then it's a caption under the
            // hero, over the action bar, as pushed down from the top of the screen it would lie across the very thing it's about.
            bool over = rect.Y >= 12f;
            rect = over
                ? new GuiRect(Math.Clamp(rect.X, 12f, MathF.Max(Bounds.Width - CardWidth - 12f, 12f)), rect.Y, rect.Width, rect.Height)
                : new GuiRect((Bounds.Width - CardWidth) * 0.5f, Bounds.Height - CaptionFromFoot - height + rise, rect.Width, rect.Height);

            if (over) draw.AddRect(new GuiRect(point.X - 1f, rect.Y + rect.Height, 2f, MathF.Max(point.Y - rect.Y - rect.Height, 0f)), Ui.Gold.MultiplyAlpha(0.7f * alpha));
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

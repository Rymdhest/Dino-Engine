using Dino_Defenders.Gameplay;
using Dino_Defenders.UI;
using Dino_Engine.GUI;
using Dino_Engine.GUI.Rendering;
using Dino_Engine.GUI.Widgets;
using Dino_Engine.Sound;
using Dino_Engine.Sound.Content;
using Dino_Engine.Util;
using Dino_Engine.World.Lexicon;
using OpenTK.Mathematics;

namespace Dino_Defenders.AssetLexicon
{
    /// <summary>
    /// What the asset lexicon (LEXICON.md) shows over the world: a plaque in front of every exhibit with its name and what it is, a sign at the west end of every family's row, a sign at the
    /// south edge of every section, and the room's own by the landing, all read off <see cref="LexiconLayout.Current"/> with nothing sent, as the server stood everything by the same plan. In the
    /// top left corner, where the hero is and the way to the index and home. Nothing here stops the mouse but the corner's buttons, as the world under it is still walked on.
    /// </summary>
    internal sealed class LexiconLayer : GuiElement
    {
        private const float PlaqueReach = 90f;      // Metres from the hero past which a plaque fades out, as it would be clutter on the far edge of a zoomed-out view.
        private const float SignReach = 170f;
        private const float NameSize = 15f;
        private const float DetailSize = 12f;
        private const float SmallestName = 11f;
        private const float Lift = 0.25f;           // Metres over the ground a plaque is read at.

        private readonly LexiconCorner _corner;

        public LexiconLayer(LexiconIndex index)
        {
            Width = UIValue.Fill;
            Height = UIValue.Fill;
            HitTest = HitTestMode.PassThrough;
            _corner = Add(new LexiconCorner(index));
        }

        private static bool InLexicon => GameSession.Current is { IsLexicon: true };

        protected override void OnUpdate(float deltaTime)
        {
            // Decided here, as a hidden element isn't updated and could never show itself again.
            _corner.Visible = InLexicon;
        }

        protected override void OnDraw(GuiDrawList draw)
        {
            GameSession? session = GameSession.Current;
            if (session == null || !session.IsLexicon || Context == null) return;
            LexiconPlan plan = LexiconLayout.Current;
            GuiRect screen = Bounds;
            Vector2 hero = session.PlayerPosition;

            DrawRoomSign(draw, session, plan, screen, hero);
            foreach (PlacedSection section in plan.Sections) DrawSection(draw, session, section, screen, hero);
            foreach (PlacedFamily family in plan.Families) DrawFamily(draw, session, family, screen, hero);
            foreach (PlacedExhibit exhibit in plan.Exhibits) DrawPlaque(draw, session, exhibit, screen, hero);
        }

        private static float Fade(Vector2 at, Vector2 hero, float reach)
        {
            float away = (at - hero).Length;
            return Math.Clamp((reach - away) / (reach * 0.3f), 0f, 1f);
        }

        private static bool Seen(GameSession session, Vector2 at, GuiRect screen, out Vector2 point)
        {
            var world = new Vector3(at.X, session.World.Height.Sample(at) + Lift, at.Y);
            if (!Hud.Project(world, screen, out point)) return false;
            return point.X > -400f && point.X < screen.Width + 400f && point.Y > -120f && point.Y < screen.Height + 160f;
        }

        // The room's own sign, by the landing.
        private void DrawRoomSign(GuiDrawList draw, GameSession session, LexiconPlan plan, GuiRect screen, Vector2 hero)
        {
            float alpha = Fade(plan.Sign, hero, SignReach);
            if (alpha <= 0f || !Seen(session, plan.Sign, screen, out Vector2 at)) return;
            draw.PushOpacity(alpha);
            const float Width = 560f;
            List<string> lines = Ui.Wrap(Context!, LexiconWorld.Prologue, 14f, Width);
            GuiRect patch = new GuiRect(at.X - Width * 0.5f - 18f, at.Y - 8f, Width + 36f, 92f + lines.Count * 19f);
            draw.AddRect(patch, GuiColors.Rgba(10, 8, 6, 170), 8f, 0f, default, 14f);
            Ui.Text(Context!, draw, "The Asset Lexicon", 34f, new GuiRect(at.X - Width * 0.5f, at.Y, Width, 40f), Ui.Gold, Alignment.Center);
            float y = at.Y + 44f;
            draw.AddLine(new Vector2(at.X - Width * 0.35f, y), new Vector2(at.X + Width * 0.35f, y), Ui.Gold.MultiplyAlpha(0.6f), 1f);
            y += 8f;
            foreach (string line in lines)
            {
                Ui.Text(Context!, draw, line, 14f, new GuiRect(at.X - Width * 0.5f, y, Width, 19f), Ui.Parchment, Alignment.Center);
                y += 19f;
            }
            Ui.Text(Context!, draw, $"{plan.Exhibits.Count} exhibits in {plan.Sections.Count} sections. The landing's gate goes home.", 12.5f, new GuiRect(at.X - Width * 0.5f, y + 4f, Width, 18f), Ui.Faded, Alignment.Center);
            draw.PopOpacity();
        }

        // A section's sign at its south-west corner: its name large, and how it's laid out under it.
        private void DrawSection(GuiDrawList draw, GameSession session, PlacedSection section, GuiRect screen, Vector2 hero)
        {
            float alpha = Fade(section.Sign, hero, SignReach);
            if (alpha <= 0f || !Seen(session, section.Sign, screen, out Vector2 at)) return;
            draw.PushOpacity(alpha);
            const float Width = 470f;
            List<string> lines = Ui.Wrap(Context!, section.Section.Blurb, 13.5f, Width);
            int count = section.Section.Exhibits.Count();
            draw.AddRect(new GuiRect(at.X - 12f, at.Y - 26f, Width + 24f, 62f + lines.Count * 18f), GuiColors.Rgba(10, 8, 6, 150), 6f, 0f, default, 12f);
            Ui.Text(Context!, draw, section.Section.Name.ToUpperInvariant(), 30f, new GuiRect(at.X, at.Y - 24f, Width, 36f), Ui.Gold);
            Ui.Text(Context!, draw, $"{count} {(count == 1 ? "exhibit" : "exhibits")}, north from here", 12.5f, new GuiRect(at.X, at.Y + 12f, Width, 16f), Ui.Faded);
            float y = at.Y + 30f;
            foreach (string line in lines)
            {
                Ui.Text(Context!, draw, line, 13.5f, new GuiRect(at.X, y, Width, 18f), Ui.Parchment);
                y += 18f;
            }
            draw.PopOpacity();
        }

        // A family's sign at the west end of its run, level with its plaques.
        private void DrawFamily(GuiDrawList draw, GameSession session, PlacedFamily family, GuiRect screen, Vector2 hero)
        {
            float alpha = Fade(family.Sign, hero, PlaqueReach * 1.3f);
            if (alpha <= 0f || !Seen(session, family.Sign, screen, out Vector2 at)) return;
            draw.PushOpacity(alpha);
            string name = family.Continued ? family.Family.Name + ", continued" : family.Family.Name;
            float width = MathF.Max(GuiText.MeasureWidth(Context!, name, Ui.Face, 18f), GuiText.MeasureWidth(Context!, family.Family.Detail, Ui.Face, 12f));
            draw.AddRect(new GuiRect(at.X - 8f, at.Y - 14f, width + 16f, 40f), GuiColors.Rgba(10, 8, 6, 175), 4f, 0f, default, 8f);
            draw.AddRect(new GuiRect(at.X - 8f, at.Y - 14f, 3f, 40f), Ui.Gold.MultiplyAlpha(0.85f), 1f);
            Ui.Text(Context!, draw, name, 18f, new GuiRect(at.X, at.Y - 14f, width + 4f, 22f), Ui.Gold);
            Ui.Text(Context!, draw, family.Family.Detail, 12f, new GuiRect(at.X, at.Y + 8f, width + 4f, 16f), Ui.Faded);
            draw.PopOpacity();
        }

        // An exhibit's plaque: its name, smaller to fit its plot if it has to, and what it is under that.
        private void DrawPlaque(GuiDrawList draw, GameSession session, PlacedExhibit exhibit, GuiRect screen, Vector2 hero)
        {
            float alpha = Fade(exhibit.Plaque, hero, PlaqueReach);
            if (alpha <= 0f || !Seen(session, exhibit.Plaque, screen, out Vector2 at)) return;

            // As wide as its plot looks from here, so that neighbours' plaques never run into each other.
            float room = 220f;
            if (Seen(session, new Vector2(exhibit.Plot.Max.X, exhibit.Plaque.Y), screen, out Vector2 west) && Seen(session, new Vector2(exhibit.Plot.Min.X, exhibit.Plaque.Y), screen, out Vector2 east))
                room = Math.Clamp(MathF.Abs(east.X - west.X) - 6f, 60f, 420f);

            bool missing = exhibit.Exhibit.Kind == LexiconKind.Missing;
            float size = NameSize;
            string name = exhibit.Exhibit.Name;
            while (size > SmallestName && GuiText.MeasureWidth(Context!, name, Ui.Face, size) > room) size -= 0.5f;
            name = Clip(name, size, room);
            string detail = Clip(exhibit.Exhibit.Detail, DetailSize, room);
            float width = MathF.Max(GuiText.MeasureWidth(Context!, name, Ui.Face, size), GuiText.MeasureWidth(Context!, detail, Ui.Face, DetailSize));

            draw.PushOpacity(alpha * (missing ? 0.75f : 1f));
            draw.AddRect(new GuiRect(at.X - width * 0.5f - 7f, at.Y - 2f, width + 14f, 33f), GuiColors.Rgba(10, 8, 6, missing ? 120 : 165), 4f, 0f, default, 7f);
            Ui.Text(Context!, draw, name, size, new GuiRect(at.X - room * 0.5f, at.Y - 1f, room, 18f), missing ? Ui.Faded : Ui.Parchment, Alignment.Center);
            Ui.Text(Context!, draw, detail, DetailSize, new GuiRect(at.X - room * 0.5f, at.Y + 15f, room, 15f), missing ? Ui.Faded.MultiplyAlpha(0.8f) : Ui.Faded, Alignment.Center);
            draw.PopOpacity();
        }

        // Cut short with an ellipsis to fit.
        private string Clip(string text, float size, float room)
        {
            if (GuiText.MeasureWidth(Context!, text, Ui.Face, size) <= room) return text;
            int keep = text.Length;
            while (keep > 1 && GuiText.MeasureWidth(Context!, text.Substring(0, keep) + "…", Ui.Face, size) > room) keep--;
            return text.Substring(0, keep).TrimEnd() + "…";
        }
    }

    /// <summary>The corner of the screen the lexicon keeps for itself: what it is, where in it the hero is, and the way to the index and home.</summary>
    internal sealed class LexiconCorner : StackPanel
    {
        private const float LookAhead = 7f;     // Metres north of the hero that what they're looking at is, about.

        private readonly Label _here;

        public LexiconCorner(LexiconIndex index) : base(Orientation.Vertical, 6f)
        {
            Anchor = Anchor.TopLeft;
            Margin = new Thickness(Ui.Gutter, Ui.Gutter, 0f, 0f);
            Width = 360f;
            Height = UIValue.Auto;
            Padding = new Thickness(14f, 12f);
            Ui.Dress(this);
            Visible = false;

            Add(Ui.Heading("The Asset Lexicon", 21f));
            _here = Add(Ui.Line("", 14f, Ui.Parchment).Bind(Where));
            Add(Ui.Line("", 12.5f, Ui.Faded).Bind(() => $"{LexiconLayout.Current.Exhibits.Count} exhibits in {LexiconLayout.Current.Sections.Count} sections, every one on the index."));
            var buttons = Add(new StackPanel(Orientation.Horizontal, 6f) { Width = UIValue.Fill, Height = 32f });
            Button open = buttons.Add(Ui.Action("Index", () => index.Open()));
            open.Width = UIValue.Relative(0.5f, -3f);
            Button home = buttons.Add(Ui.Action("Back to the Haven", LexiconIndex.GoHome));
            home.Width = UIValue.Relative(0.5f, -3f);
        }

        // The section and the row the hero is looking at, as the index lists them, and the place they're in, whose plaque is out of sight at the front of its plot.
        private static string Where()
        {
            GameSession? session = GameSession.Current;
            if (session == null) return "";
            LexiconPlan plan = LexiconLayout.Current;
            Vector2 at = session.PlayerPosition;
            if ((at - plan.Arrival).Length < plan.ArrivalRadius * 2f) return "At the landing";
            PlacedExhibit? place = plan.Exhibits.FirstOrDefault(exhibit => exhibit.Exhibit.Kind == LexiconKind.Place && exhibit.Plot.Contains(at));
            if (place != null) return $"{place.Section.Section.Name} › {place.Family.Family.Name} › {place.Exhibit.Name}";
            // What's being looked at is ahead of the hero, as the camera shows more of what's north of them than south: whoever's sent to an exhibit stands south of it.
            Vector2 looked = at + new Vector2(0f, LookAhead);
            PlacedFamily? family = plan.FamilyAt(looked, 6f);
            if (family != null) return $"{family.Section.Section.Name} › {family.Family.Name}";
            PlacedSection? section = plan.SectionAt(looked);
            return section != null ? section.Section.Name : "On the lawn";
        }
    }

    /// <summary>
    /// Everything in the asset lexicon in one list, section by section and family by family, as it stands from the gate north, searched by anything about each, its name, its ids, its look,
    /// its family. Choosing one takes the hero to stand in front of it.
    /// </summary>
    internal sealed class LexiconIndex : GuiElement
    {
        private readonly TextBox _search;
        private readonly ScrollPanel _list;
        private readonly List<(IndexRow Row, string Words, PlacedSection Section, ExhibitFamily? Family)> _rows = new();
        private string[] _words = Array.Empty<string>();
        private bool _sift = true;
        private int _shown;
        private bool _built;

        public LexiconIndex()
        {
            Ui.DockLeft(this, 600f);
            Padding = new Thickness(20f);
            Ui.Dress(this);
            Visible = false;

            var body = Add(new StackPanel(Orientation.Vertical, 8f) { Width = UIValue.Fill, Height = UIValue.Fill });
            body.Add(Ui.Heading("Index of the Asset Lexicon", 24f));
            body.Add(Ui.Paragraph("Everything in the room, as it stands from the gate north. Choose anything to be taken to stand in front of it.", 13.5f, Ui.Faded));
            _search = body.Add(new TextBox("", "Search names, ids, looks and families") { Width = UIValue.Fill, Height = 28f, MaxLength = 64 });
            _search.TextChanged += text =>
            {
                _words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(word => word.ToLowerInvariant()).ToArray();
                _sift = true;
            };
            _search.Submitted += _ => _search.Unfocus();
            body.Add(Ui.Line("", 13f, Ui.Faded).Bind(() => $"{_shown} of {LexiconLayout.Current.Exhibits.Count} shown"));
            _list = body.Add(Kit.Scroll());
            var buttons = body.Add(new StackPanel(Orientation.Horizontal, 6f) { Width = UIValue.Fill, Height = 32f });
            Button close = buttons.Add(Ui.Action("Close", () => GameUi.Current.CloseAll()));
            close.Width = UIValue.Relative(0.5f, -3f);
            Button home = buttons.Add(Ui.Action("Back to the Haven", GoHome));
            home.Width = UIValue.Relative(0.5f, -3f);
        }

        public void Open()
        {
            Audio.Play(Interface.Click);
            bool open = Visible;
            GameUi.Current.CloseAll();
            if (open) return;
            Build();
            Visible = true;
            _search.Focus();
            _search.SelectAll();
        }

        /// <summary>Everyone home to the Haven.</summary>
        public static void GoHome()
        {
            Audio.Play(Interface.Click);
            GameUi.Current.CloseAll();
            GameSession.Current?.Client.AskLexicon(LexiconAsk.Leave);
        }

        private static void GoTo(Vector2 point)
        {
            Audio.Play(Interface.Click);
            GameUi.Current.CloseAll();
            GameSession.Current?.Client.AskLexicon(LexiconAsk.GoTo, point);
        }

        // The rows, once: the plan never changes while the game runs.
        private void Build()
        {
            if (_built) return;
            _built = true;
            LexiconPlan plan = LexiconLayout.Current;
            foreach (PlacedSection section in plan.Sections)
            {
                PlacedExhibit[] exhibits = plan.Exhibits.Where(exhibit => exhibit.Section == section).ToArray();
                if (exhibits.Length == 0) continue;
                Add(new IndexRow(section.Section.Name.ToUpperInvariant(), $"{exhibits.Length} exhibits · {section.Section.Families.Count} families", IndexRow.Kind.Section, () => GoTo(section.Sign - new Vector2(0f, 6f))),
                    section.Section.Name + " " + section.Section.Blurb, section, null);

                // Families as they're laid out, which for the shore's is a family of its own.
                foreach (ExhibitFamily family in exhibits.Select(exhibit => exhibit.Family.Family).Distinct())
                {
                    PlacedFamily first = plan.Families.First(placed => placed.Family == family);
                    PlacedExhibit[] members = exhibits.Where(exhibit => exhibit.Family.Family == family).ToArray();
                    Add(new IndexRow(family.Name, family.Detail, IndexRow.Kind.Family, () => GoTo(first.Sign - new Vector2(0f, 7f))), $"{section.Section.Name} {family.Name} {family.Detail}", section, family);
                    foreach (PlacedExhibit exhibit in members)
                    {
                        Exhibit shown = exhibit.Exhibit;
                        string words = $"{shown.Name} {shown.Detail} {shown.Key} {shown.Look} {shown.Keywords} {family.Name} {section.Section.Name}";
                        Add(new IndexRow(shown.Name, shown.Detail, shown.Kind == LexiconKind.Missing ? IndexRow.Kind.Missing : IndexRow.Kind.Exhibit, () => GoTo(exhibit.Viewpoint)), words, section, family);
                    }
                }
            }
        }

        private void Add(IndexRow row, string words, PlacedSection section, ExhibitFamily? family)
        {
            row.View = () => _list.Bounds;
            _list.Content.Add(row);
            _rows.Add((row, words.ToLowerInvariant(), section, family));
        }

        protected override void OnUpdate(float deltaTime)
        {
            if (!_sift) return;
            _sift = false;

            // An exhibit shows if every word is somewhere about it, and its family's and section's rows show if anything under them does.
            var families = new HashSet<ExhibitFamily>();
            var sections = new HashSet<PlacedSection>();
            _shown = 0;
            foreach ((IndexRow row, string words, PlacedSection section, ExhibitFamily? family) in _rows)
            {
                if (row.Is is IndexRow.Kind.Section or IndexRow.Kind.Family) continue;
                bool passes = _words.All(words.Contains);
                row.Visible = passes;
                if (!passes) continue;
                _shown++;
                if (family != null) families.Add(family);
                sections.Add(section);
            }
            foreach ((IndexRow row, string _, PlacedSection section, ExhibitFamily? family) in _rows)
            {
                if (row.Is == IndexRow.Kind.Section) row.Visible = sections.Contains(section);
                else if (row.Is == IndexRow.Kind.Family) row.Visible = family != null && families.Contains(family);
            }
        }
    }

    /// <summary>One line of the index: a section, a family or an exhibit, which takes the hero to it when chosen.</summary>
    internal sealed class IndexRow : GuiElement
    {
        public enum Kind { Section, Family, Exhibit, Missing }

        public readonly Kind Is;
        private readonly string _name;
        private readonly string _detail;

        /// <summary>What can be seen of the list it's in, as nothing outside that needs drawing, and the list is long.</summary>
        public Func<GuiRect>? View;

        public IndexRow(string name, string detail, Kind kind, Action chosen)
        {
            Is = kind;
            _name = name;
            _detail = detail;
            Width = UIValue.Fill;
            Height = kind switch { Kind.Section => 44f, Kind.Family => 36f, _ => 36f };
            Clicked += _ => chosen();
        }

        protected override void OnDraw(GuiDrawList draw)
        {
            if (Context == null || View != null && !View().Intersects(Bounds)) return;
            GuiRect bounds = Bounds;
            if (IsHovered) draw.AddRect(bounds, GuiColors.Rgba(255, 240, 200, 22), 3f);
            switch (Is)
            {
                case Kind.Section:
                    Ui.Text(Context, draw, _name, 20f, new GuiRect(bounds.X + 4f, bounds.Y + 8f, bounds.Width - 8f, 24f), Ui.Gold);
                    Ui.Text(Context, draw, _detail, 12f, new GuiRect(bounds.X + 4f, bounds.Y + 8f, bounds.Width - 12f, 24f), Ui.Faded, Alignment.End);
                    draw.AddLine(new Vector2(bounds.X + 4f, bounds.Bottom - 4f), new Vector2(bounds.Right - 4f, bounds.Bottom - 4f), Ui.Gold.MultiplyAlpha(0.45f), 1f);
                    break;
                case Kind.Family:
                    draw.AddRect(new GuiRect(bounds.X + 10f, bounds.Y + 8f, 3f, bounds.Height - 14f), Ui.Gold.MultiplyAlpha(0.8f), 1f);
                    Ui.Text(Context, draw, _name, 16f, new GuiRect(bounds.X + 20f, bounds.Y + 2f, bounds.Width - 24f, 20f), Ui.Gold);
                    Ui.Text(Context, draw, _detail, 12f, new GuiRect(bounds.X + 20f, bounds.Y + 19f, bounds.Width - 24f, 15f), Ui.Faded);
                    break;
                default:
                    bool missing = Is == Kind.Missing;
                    Ui.Text(Context, draw, _name, 14.5f, new GuiRect(bounds.X + 34f, bounds.Y + 2f, bounds.Width - 40f, 18f), missing ? Ui.Faded : IsHovered ? Ui.Bright : Ui.Parchment);
                    Ui.Text(Context, draw, _detail, 11.5f, new GuiRect(bounds.X + 34f, bounds.Y + 19f, bounds.Width - 40f, 15f), Ui.Faded.MultiplyAlpha(missing ? 0.75f : 1f));
                    break;
            }
        }
    }
}

using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Stats;
using Dino_Defenders.Tooltips;
using Dino_Engine.Core;
using Dino_Engine.GUI;
using Dino_Engine.GUI.Rendering;
using Dino_Engine.GUI.Widgets;
using Dino_Engine.Sound;
using Dino_Engine.Sound.Content;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Dino_Defenders.UI
{
    /// <summary>
    /// How the hero fell, read off the account the server sent the moment it happened (DEATHS.md). At first it's only what ended it, in a sentence or two, and what to do next; Details opens the last
    /// moments, newest first, each a line whose card says everything about it. A person found the first version, with a graph, shares and sums, far too much to read: keep it to this. It stays until
    /// they choose to get up or go home. Nothing here works anything out: it's all <see cref="RecapText"/>, from what the server wrote down.
    /// </summary>
    internal sealed class DeathScreen : GuiElement
    {
        private const double Delay = 1.1;           // Seconds after the fall before it comes up, so the fall itself is seen.
        private const double FadeIn = 0.45;
        private const float Pad = 24f;
        private const float HeadWidth = 760f;
        private const float ListWidth = 900f;
        private const float MostListHeight = 720f;
        private const float RowHeight = 25f;
        private const float WheelStep = RowHeight * 3f;
        private const float ButtonWidth = 210f;
        private const float ButtonHeight = 38f;
        private const float ButtonGap = 16f;
        private const float StripHeight = 40f;

        private static readonly Colour Dim = GuiColors.Rgba(6, 3, 2, 150);
        private static readonly Colour Edge = GuiColors.Rgba(120, 96, 58);
        private static readonly Colour Rule = GuiColors.Rgba(120, 96, 58, 90);
        private static readonly Colour Well = GuiColors.Rgba(28, 22, 18, 235);
        private static readonly Colour Hover = GuiColors.Rgba(70, 54, 36, 150);
        private static readonly Colour FatalRow = GuiColors.Rgba(110, 22, 16, 110);
        private static readonly Colour Bone = GuiColors.Rgba(232, 222, 200);

        private readonly List<HotSpot> _spots = new();
        private DeathRecap? _recap;
        private double _since;
        private bool _collapsed;
        private bool _details;
        private bool _wasOver;
        private float _scroll;
        private GuiRect _panel;
        private GuiRect _list;
        private GuiRect _primary;
        private GuiRect _secondary;
        private GuiRect _strip;
        private float _listHeight;

        public DeathScreen()
        {
            Width = UIValue.Fill;
            Height = UIValue.Fill;
            HitTest = HitTestMode.PassThrough;
        }

        /// <summary>The account on show, if one is.</summary>
        private DeathRecap? Showing
        {
            get
            {
                GameSession? session = GameSession.Current;
                if (session?.Client.Recap is not DeathRecap recap) return null;
                if (!ReferenceEquals(recap, _recap))
                {
                    _recap = recap;
                    _since = Engine.Time;
                    _collapsed = false;
                    _details = false;
                    _scroll = 0f;
                }
                return Engine.Time - _since >= Delay ? recap : null;
            }
        }

        public override bool HitTestSelf(Vector2 point)
        {
            if (Showing == null) return false;
            return _collapsed ? _strip.Contains(point) : true;
        }

        protected override void OnScroll(GuiScrollEventArgs e)
        {
            if (Showing == null || _collapsed || !_details) return;
            if (_list.Contains(e.Position)) _scroll = Math.Clamp(_scroll - e.Delta.Y * WheelStep, 0f, MathF.Max(_listHeight - _list.Height, 0f));
            e.Handled = true;
        }

        // A click only comes to whatever took the press, so the press is taken: on the strip when it's folded, and anywhere while it's open, as nothing behind it is for clicking then.
        protected override void OnMouseDown(GuiMouseEventArgs e)
        {
            if (Showing == null || e.Button != MouseButton.Left) return;
            if (!_collapsed || _strip.Contains(e.Position)) e.Handled = true;
        }

        protected override void OnClick(GuiMouseEventArgs e)
        {
            if (Showing == null || e.Button != MouseButton.Left) return;
            GameSession session = GameSession.Current;
            if (_collapsed)
            {
                if (!_strip.Contains(e.Position)) return;
                _collapsed = false;
                Audio.Play(Interface.Open);
                return;
            }

            if (_primary.Contains(e.Position)) Act(session, Primary(session));
            else if (_secondary.Contains(e.Position))
            {
                _details = !_details;
                _scroll = 0f;
                Audio.Play(_details ? Interface.Open : Interface.Close);
            }
            e.Handled = true;
        }

        private enum Choice
        {
            Rise,
            Home,
            Watch,
        }

        // What the one thing to do next is: get up where nothing's lost, take everyone home from a land that's over, or watch friends who are still at it.
        private static Choice Primary(GameSession session) =>
            session.IsHaven || session.IsLexicon ? Choice.Rise : session.Client.Over ? Choice.Home : Choice.Watch;

        private static string Label(Choice choice) => choice switch
        {
            Choice.Rise => "Rise",
            Choice.Home => "Return to the Haven",
            _ => "Watch your friends",
        };

        private void Act(GameSession session, Choice choice)
        {
            Audio.Play(Interface.Open);
            switch (choice)
            {
                case Choice.Rise:
                    session.Client.Rise();
                    session.Client.Forget();
                    break;
                case Choice.Home:
                    session.Client.ReturnHome();
                    break;
                case Choice.Watch:
                    _collapsed = true;
                    break;
            }
        }

        protected override void OnDraw(GuiDrawList draw)
        {
            base.OnDraw(draw);
            GameSession? session = GameSession.Current;
            if (Context == null || session == null || GameUi.Current.OnPage || Showing is not DeathRecap recap) return;

            float fade = (float)Math.Clamp((Engine.Time - _since - Delay) / FadeIn, 0.0, 1.0);
            GuiRect screen = Bounds;

            // Folded away to watch friends, it opens again once the last of them falls, as there's something to do then.
            if (_collapsed && session.Client.Over && !_wasOver) _collapsed = false;
            _wasOver = session.Client.Over;
            if (_collapsed)
            {
                DrawStrip(draw, screen);
                return;
            }

            draw.AddRect(screen, Dim.MultiplyAlpha(fade));
            _spots.Clear();
            if (_details) DrawDetails(draw, recap, screen);
            else DrawHeadline(draw, recap, screen);
            DrawButtons(draw, session);
            GameUi.Current.Tips.Placed(new List<HotSpot>(_spots), _panel);
        }

        // ---- What ended it.

        private void DrawHeadline(GuiDrawList draw, DeathRecap recap, GuiRect screen)
        {
            float width = MathF.Min(screen.Width - Ui.Gutter * 2f, HeadWidth);
            float inner = width - Pad * 2f;
            string cause = RecapText.Cause(recap);
            string blow = RecapText.Blow(recap);
            float height = Pad + 50f + Lines(cause, 19f, inner) * 25f + (blow.Length > 0 ? Lines(blow, 15f, inner) * 21f + 2f : 0f) + 18f + ButtonHeight + Pad;
            _panel = new GuiRect(screen.X + (screen.Width - width) * 0.5f, screen.Y + screen.Height * 0.36f - height * 0.5f, width, height);
            draw.AddRect(_panel, Ui.Paper, 4f, 1f, Edge, 0f);

            float x = _panel.X + Pad;
            float y = _panel.Y + Pad - 4f;
            Ui.Text(Context!, draw, RecapText.Title(recap), 38f, new GuiRect(x, y, inner, 46f), GuiColors.Rgba(206, 70, 52), Alignment.Center);
            y += 50f;
            y = Centred(draw, cause, 19f, Ui.Bright, x, y, inner, recap.Killer is RecapEntry killer ? Links(recap, killer) : Array.Empty<Card>());
            if (blow.Length > 0) Centred(draw, blow, 15f, Ui.Parchment, x, y + 2f, inner, Array.Empty<Card>());
        }

        private int Lines(string text, float size, float width) => Math.Max(Ui.Wrap(Context!, text, size, width).Count, 1);

        // A Book's card can be opened from its name in a sentence.
        private static IReadOnlyList<Card> Links(DeathRecap recap, in RecapEntry entry) => recap.BookOf(entry)?.Card is Card card ? new[] { card } : Array.Empty<Card>();

        private float Centred(GuiDrawList draw, string text, float size, Colour colour, float x, float y, float inner, IReadOnlyList<Card> links)
        {
            foreach (string line in Ui.Wrap(Context!, text, size, inner))
            {
                float width = GuiText.MeasureWidth(Context!, line, Ui.Face, size);
                Ui.Rich(Context!, draw, line, size, x + (inner - width) * 0.5f, y, colour, links, _spots);
                y += size + 6f;
            }
            return y;
        }

        // ---- The last moments, newest first.

        private void DrawDetails(GuiDrawList draw, DeathRecap recap, GuiRect screen)
        {
            float width = MathF.Min(screen.Width - Ui.Gutter * 2f, ListWidth);
            float rows = recap.Entries.Count * RowHeight;
            float height = MathF.Min(Pad + 26f + MathF.Max(rows, RowHeight) + 8f + 18f + ButtonHeight + Pad, MathF.Min(screen.Height - Ui.Gutter * 2f, MostListHeight));
            _panel = new GuiRect(screen.X + (screen.Width - width) * 0.5f, screen.Y + (screen.Height - height) * 0.5f, width, height);
            draw.AddRect(_panel, Ui.Paper, 4f, 1f, Edge, 0f);

            float x = _panel.X + Pad;
            float y = _panel.Y + Pad - 4f;
            GuiText.Draw(Context!, draw, "The last moments, newest first", Ui.Face, 15f, new GuiRect(x, y, 600f, 20f), Ui.Gold, Alignment.Start, Alignment.Center);
            _list = new GuiRect(x, y + 28f, _panel.Width - Pad * 2f, _panel.Bottom - Pad - ButtonHeight - 18f - (y + 28f));
            DrawRows(draw, recap);
        }

        private void DrawRows(GuiDrawList draw, DeathRecap recap)
        {
            draw.AddRect(_list.Inflate(4f), Well, 3f, 1f, Rule);
            _listHeight = recap.Entries.Count * RowHeight;
            _scroll = Math.Clamp(_scroll, 0f, MathF.Max(_listHeight - _list.Height, 0f));
            draw.PushClip(_list);
            Vector2 mouse = Context!.MousePosition;
            float when = 58f;
            float health = 90f;
            float rest = _list.Width - when - health;
            float who = rest * 0.3f;
            float what = rest * 0.3f;
            float outcome = rest - who - what;

            for (int n = 0; n < recap.Entries.Count; n++)
            {
                int index = recap.Entries.Count - 1 - n;
                float y = _list.Y + n * RowHeight - _scroll;
                if (y + RowHeight < _list.Y || y > _list.Bottom) continue;
                RecapRow row = RecapText.Row(recap, index);
                GuiRect rect = new GuiRect(_list.X, y, _list.Width, RowHeight);
                if (row.Fatal) draw.AddRect(rect, FatalRow, 2f);
                if (rect.Contains(mouse) && _list.Contains(mouse))
                {
                    draw.AddRect(rect, Hover, 2f);
                    int at = index;
                    GameUi.Current.Tips.Show(() => RecapText.Entry(recap, at), rect);
                }

                float x = rect.X + 4f;
                Colour quiet = row.Missed ? Ui.Faded : Ui.Parchment;
                GuiText.Draw(Context!, draw, row.When, Ui.Face, 13f, new GuiRect(x, y, when - 8f, RowHeight), Ui.Faded, Alignment.End, Alignment.Center);
                x += when;
                Colour whoInk = row.WhoRarity is Rarity rarity ? Ui.RarityColour(rarity) : quiet;
                GuiText.Draw(Context!, draw, Fit(row.Who, 13.5f, who - 8f), Ui.Face, 13.5f, new GuiRect(x, y, who - 6f, RowHeight), whoInk, Alignment.Start, Alignment.Center);
                x += who;
                GuiText.Draw(Context!, draw, Fit(row.What, 13.5f, what - 8f), Ui.Face, 13.5f, new GuiRect(x, y, what - 6f, RowHeight), quiet, Alignment.Start, Alignment.Center);
                x += what;
                Colour outcomeInk = row.Good || row.Missed ? Ui.Good : Colour(row.Type);
                GuiText.Draw(Context!, draw, Fit(row.Outcome, 13.5f, outcome - 8f), Ui.Face, 13.5f, new GuiRect(x, y, outcome - 6f, RowHeight), outcomeInk, Alignment.Start, Alignment.Center);
                x += outcome;

                // Health after it, as a share of all of it.
                float most = MathF.Max(recap.MaxHealth, 1f);
                GuiRect bar = new GuiRect(x, y + 8f, health - 10f, RowHeight - 16f);
                draw.AddRect(bar, GuiColors.Rgba(40, 32, 26, 220), 2f);
                draw.AddRect(new GuiRect(bar.X, bar.Y, bar.Width * Math.Clamp(row.Health / most, 0f, 1f), bar.Height), Ui.Life, 2f);
                if (row.Barrier > 0f) draw.AddRect(new GuiRect(bar.X, bar.Y, bar.Width * Math.Clamp(row.Barrier / most, 0f, 1f), 2f), Ui.Barrier);
            }
            draw.PopClip();
            if (_listHeight > _list.Height) Scrollbar(draw, _list, _scroll, _listHeight);
        }

        private static void Scrollbar(GuiDrawList draw, GuiRect area, float offset, float content)
        {
            float thumb = MathF.Max(area.Height * area.Height / content, 18f);
            float at = area.Y + (area.Height - thumb) * (offset / MathF.Max(content - area.Height, 1f));
            draw.AddRect(new GuiRect(area.Right + 1f, at, 3f, thumb), Edge, 1.5f);
        }

        // ---- What to do: read more, or carry on.

        private void DrawButtons(GuiDrawList draw, GameSession session)
        {
            float y = _panel.Bottom - Pad - ButtonHeight;
            float left = _panel.X + (_panel.Width - ButtonWidth * 2f - ButtonGap) * 0.5f;
            _secondary = new GuiRect(left, y, ButtonWidth, ButtonHeight);
            _primary = new GuiRect(left + ButtonWidth + ButtonGap, y, ButtonWidth, ButtonHeight);
            Button(draw, _secondary, _details ? "Back" : "Details", false);
            Button(draw, _primary, Label(Primary(session)), true);
        }

        private void Button(GuiDrawList draw, GuiRect rect, string label, bool main)
        {
            bool over = rect.Contains(Context!.MousePosition);
            draw.AddRect(rect, over ? GuiColors.Rgba(84, 64, 40) : Ui.InkLight, 3f, 1f, main ? Ui.Gold : Edge);
            Ui.Text(Context!, draw, label, 16f, rect, main ? Ui.Bright : Ui.Parchment, Alignment.Center);
        }

        // Folded, for watching friends: a strip that says so and opens it again.
        private void DrawStrip(GuiDrawList draw, GuiRect screen)
        {
            const string Text = "You have fallen. Read how";
            float width = GuiText.MeasureWidth(Context!, Text, Ui.Face, 15f) + 40f;
            _strip = new GuiRect(screen.X + (screen.Width - width) * 0.5f, screen.Y + 70f, width, StripHeight);
            bool over = _strip.Contains(Context!.MousePosition);
            draw.AddRect(_strip, over ? GuiColors.Rgba(60, 22, 16, 235) : Ui.Paper, 3f, 1f, Edge);
            Ui.Text(Context!, draw, Text, 15f, _strip, Ui.Parchment, Alignment.Center);
        }

        // ---- Colours.

        /// <summary>What a type of damage is drawn in on this screen: its own colour, light enough to read on ink, and none of them the Reich's.</summary>
        public static Colour Colour(DamageType type) => type switch
        {
            DamageType.Fire => GuiColors.Rgba(240, 128, 52),
            DamageType.Cold => GuiColors.Rgba(110, 190, 240),
            DamageType.Lightning => GuiColors.Rgba(196, 200, 255),
            DamageType.Magic => GuiColors.Rgba(190, 110, 240),
            DamageType.Poison => GuiColors.Rgba(120, 214, 84),
            _ => Bone,
        };

        // Cut to fit, with a mark that it was.
        private string Fit(string text, float size, float width)
        {
            if (GuiText.MeasureWidth(Context!, text, Ui.Face, size) <= width) return text;
            for (int length = text.Length - 1; length > 1; length--)
            {
                string cut = text[..length].TrimEnd() + "…";
                if (GuiText.MeasureWidth(Context!, cut, Ui.Face, size) <= width) return cut;
            }
            return text;
        }
    }
}

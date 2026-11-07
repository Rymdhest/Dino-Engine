using Dino_Defenders.Client;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Stats;
using Dino_Defenders.Tooltips;
using Dino_Engine.Core;
using Dino_Engine.GUI;
using Dino_Engine.GUI.Rendering;
using Dino_Engine.GUI.Widgets;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Dino_Defenders.UI
{
    /// <summary>Something in a card that can be rested on, and what that opens: what a word means, or another card.</summary>
    internal readonly record struct HotSpot(GuiRect Rect, Term? Term, Card? Card)
    {
        public bool Opens(in HotSpot other) => ReferenceEquals(Term, other.Term) && Card?.Title == other.Card?.Title && Card?.Kind == other.Card?.Kind;
    }

    /// <summary>A card with its text broken into lines and put in place, ready to be drawn anywhere.</summary>
    internal sealed class LaidCard
    {
        public readonly record struct Piece(string Text, string Value, float X, float Y, float Width, float Size, Colour Colour, Colour ValueColour, Card? Opens = null);
        public readonly record struct Chip(string Text, float X, float Y, float Width);

        public const float Pad = 12f;
        public const float PictureSize = 84f;
        private const float TagSize = 11.5f;
        private const float ChipHeight = 17f;
        private const string DetailsLink = "Details";
        private static string HoldHint => $"Hold {Bindings.Label(GameAction.Details)} for keywords and details";
        private static string ItemHint => $"Hold {Bindings.Label(GameAction.Details)} for keywords, {Bindings.Label(GameAction.Compare)} to compare";

        public readonly List<Piece> Pieces = new();
        public readonly List<Chip> Chips = new();
        public IReadOnlyList<Card> Links = Array.Empty<Card>();
        public Item? Picture;
        public Colour Edge;
        public float Width;
        public float Height;

        public static Colour TitleColour(Card card) => card.Grade is LootGrade grade ? Ui.GradeColour(grade) : card.Gilded ? Ui.Gold : Ui.Parchment;

        /// <param name="held">Whether it's being held still to be read, which is when there's anything to rest on.</param>
        /// <param name="comparable">Whether there's something to set it against with Alt.</param>
        public static LaidCard Of(GuiContext context, Card card, float width, bool held, bool comparable = false)
        {
            var laid = new LaidCard { Width = width, Picture = card.Picture, Links = card.Attached, Edge = card.Grade is LootGrade grade ? Ui.GradeColour(grade).MultiplyAlpha(0.8f) : GuiColors.Rgba(120, 96, 58) };
            float inner = width - Pad * 2f;
            float head = card.Picture != null ? inner - PictureSize - 8f : inner;
            float y = Pad - 3f;

            void Wrapped(string text, float size, Colour colour, float within)
            {
                foreach (string line in Ui.Wrap(context, text, size, within))
                {
                    laid.Pieces.Add(new Piece(line, "", Pad, y, within, size, colour, default));
                    y += size + 5f;
                }
            }

            Colour title = TitleColour(card);
            if (card.Title.Length > 0) Wrapped(card.Title, 18f, title, head);
            if (card.Kind.Length > 0) Wrapped(card.Kind, 13f, Ui.Faded, head);
            y = laid.LayChips(context, card, y, card.Picture != null && y < PictureSize + Pad ? head : inner);
            if (card.Picture != null) y = MathF.Max(y, PictureSize + Pad + 4f);
            y += 4f;

            for (int i = 0; i < card.Lines.Count; i++)
            {
                CardLine line = card.Lines[i];
                switch (line.Look)
                {
                    case LineLook.Gap:
                        y += 7f;
                        break;
                    case LineLook.Row:
                        laid.Pieces.Add(new Piece(line.Text, line.Value, Pad, y, inner, 14.5f, Ui.Parchment, Ui.Bright));
                        y += 20f;
                        break;
                    case LineLook.Cell:
                        float cell = (inner - 14f) * 0.5f;
                        bool paired = i + 1 < card.Lines.Count && card.Lines[i + 1].Look == LineLook.Cell && Fits(context, line, cell) && Fits(context, card.Lines[i + 1], cell);
                        laid.Pieces.Add(new Piece(line.Text, line.Value, Pad, y, paired ? cell : inner, 13f, Ui.Faded, Ui.Parchment));
                        if (paired)
                        {
                            CardLine next = card.Lines[++i];
                            laid.Pieces.Add(new Piece(next.Text, next.Value, Pad + cell + 14f, y, cell, 13f, Ui.Faded, Ui.Parchment));
                        }
                        y += 18f;
                        break;
                    case LineLook.Heading:
                        Wrapped(line.Text, 12.5f, Ui.Gold, inner);
                        break;
                    default:
                        (float size, Colour colour) = Style(line.Look, title);
                        Wrapped(line.Text, size, colour, inner);
                        break;
                }
            }

            if (held && card.Details != null)
            {
                y += 4f;
                laid.Pieces.Add(new Piece(DetailsLink, "", Pad, y, inner, 13f, Ui.TermInk, default, card.Details));
                y += 18f;
            }
            else if (!held)
            {
                y += 4f;
                laid.Pieces.Add(new Piece(comparable ? ItemHint : HoldHint, "", Pad, y, inner, 11.5f, Ui.Faded.MultiplyAlpha(0.7f), default));
                y += 16f;
            }

            laid.Height = y + Pad - 6f;
            return laid;
        }

        private static bool Fits(GuiContext context, in CardLine line, float width) => GuiText.MeasureWidth(context, $"{line.Text}  {line.Value}", Ui.Face, 13f) <= width;

        private float LayChips(GuiContext context, Card card, float y, float within)
        {
            float x = Pad;
            bool any = false;
            foreach (string name in Cards.TagNames(card.Tags))
            {
                float width = GuiText.MeasureWidth(context, name, Ui.Face, TagSize) + 12f;
                if (x + width > Pad + within && any)
                {
                    x = Pad;
                    y += ChipHeight + 3f;
                }
                Chips.Add(new Chip(name, x, y + 1f, width));
                x += width + 4f;
                any = true;
            }
            return any ? y + ChipHeight + 6f : y;
        }

        private static (float size, Colour colour) Style(LineLook look, Colour title) => look switch
        {
            LineLook.Rolled => (14f, title),
            LineLook.Dim => (14f, Ui.Faded.MultiplyAlpha(0.72f)),
            LineLook.Detail => (13f, Ui.Faded),
            LineLook.Lore => (13f, Ui.Faded),
            LineLook.Price => (14f, Ui.Gold),
            LineLook.Good => (13f, Ui.Good),
            LineLook.Bad => (13f, Ui.Blood),
            _ => (14f, Ui.Parchment),
        };

        /// <param name="spots">Gets everything drawn that can be rested on.</param>
        public void Draw(GuiContext context, GuiDrawList draw, float x, float y, bool framed, List<HotSpot> spots)
        {
            if (framed) draw.AddRect(new GuiRect(x, y, Width, Height), Ui.Paper, 3f, 1f, Edge);
            if (Picture != null) Ui.DrawItem(draw, Picture, new GuiRect(x + Width - PictureSize - 10f, y + 8f, PictureSize, PictureSize), Edge, GuiColors.White);

            foreach (Chip chip in Chips)
            {
                GuiRect rect = new GuiRect(x + chip.X, y + chip.Y, chip.Width, ChipHeight);
                draw.AddRect(rect, GuiColors.Rgba(52, 42, 32, 235), 3f);
                GuiText.Draw(context, draw, chip.Text, Ui.Face, TagSize, rect, Ui.Parchment, Alignment.Center);
                if (Lexicon.Named(chip.Text) is Term term) spots.Add(new HotSpot(rect, term, null));
            }

            foreach (Piece piece in Pieces)
            {
                float top = y + piece.Y;
                if (piece.Opens != null)
                {
                    float width = GuiText.MeasureWidth(context, piece.Text, Ui.Face, piece.Size);
                    GuiText.Draw(context, draw, piece.Text, Ui.Face, piece.Size, new GuiRect(x + piece.X, top, width + 2f, piece.Size + 5f), piece.Colour, Alignment.Start, Alignment.Start);
                    spots.Add(new HotSpot(new GuiRect(x + piece.X, top, width, piece.Size + 5f), null, piece.Opens));
                    continue;
                }

                Ui.Rich(context, draw, piece.Text, piece.Size, x + piece.X, top, piece.Colour, Links, spots);
                if (piece.Value.Length > 0)
                {
                    GuiText.Draw(context, draw, piece.Value, Ui.Face, piece.Size, new GuiRect(x + piece.X, top, piece.Width, piece.Size + 5f), piece.ValueColour, Alignment.End, Alignment.Start);
                }
            }
        }
    }

    /// <summary>
    /// Where every tooltip is drawn, on top of everything. Whatever is being looked at hands over a way of making its card each frame, so it can't go stale, and only that card shows, for as long as the mouse is
    /// on the thing itself. Holding left Ctrl holds it still, and only then does it stop the mouse and can a marked word or a tag on it be rested on, which explains that one word in a small card of its own.
    /// Ctrl is also what a click on something in a pack is held down with, so a card that's loose only stays for a Ctrl pressed while it's showing, and lets go the moment that click comes: a hand going down a
    /// row of things with Ctrl held has to see each of them, and never find the last one's card in its way.
    /// </summary>
    internal sealed class TipLayer : GuiElement
    {
        private const float CardWidth = 350f;
        private const float WordWidth = 300f;
        private const float Gap = 8f;
        private const float Margin = 8f;
        private const double Linger = 0.3;      // Seconds a word's card waits for the mouse to reach it.
        private const double Settle = 0.12;     // Seconds on a word before it opens, so that crossing one on the way to another does nothing.

        private readonly List<GuiRect> _rects = new();
        private readonly List<HotSpot> _open = new();
        private readonly List<List<HotSpot>> _layers = new();

        private Func<Card?>? _source;
        private Func<Card?>? _asked;
        private GuiRect _beside;
        private GuiRect _askedBeside;
        private bool _above;
        private bool _askedAbove;
        private bool _loose;
        private bool _askedLoose;
        private Func<List<Card>>? _alongside;
        private Func<List<Card>>? _askedAlongside;
        private bool _ctrlDown;
        private bool _pinned;       // Ctrl went down on the card showing, and hasn't been used for anything else.
        private List<HotSpot>? _placedSpots;
        private GuiRect _placed;
        private string _looking = "";
        private double _keepOpen;
        private HotSpot? _settling;
        private double _settlingSince;

        public TipLayer()
        {
            Width = UIValue.Fill;
            Height = UIValue.Fill;
            HitTest = HitTestMode.PassThrough;
        }

        /// <summary>Whether the card on show is being held still to be read.</summary>
        public bool Held => Context != null && Bindings.Down(GameAction.Details) && (_pinned || !_loose);

        // Only a card being held stops the mouse, and it has to then, or whatever is underneath would take over as the mouse crossed it.
        public override bool HitTestSelf(Vector2 point)
        {
            if (!Held) return false;
            foreach (GuiRect rect in _rects)
            {
                if (rect.Contains(point)) return true;
            }
            return false;
        }

        /// <summary>Shows a card beside something, for as long as this keeps being called.</summary>
        /// <param name="above">Over it rather than to one side, for what stands in a row along the bottom of the screen.</param>
        /// <param name="loose">Whether it's something a click with Ctrl held does something to.</param>
        /// <param name="alongside">Other cards to set beside this one while Alt is held.</param>
        public void Show(Func<Card?> card, GuiRect beside, bool above = false, bool loose = false, Func<List<Card>>? alongside = null)
        {
            _asked = card;
            _askedBeside = beside;
            _askedAbove = above;
            _askedLoose = loose;
            _askedAlongside = alongside;
        }

        /// <summary>A click with Ctrl held has done what it was for, so nothing is being held to be read, and won't be until Ctrl comes up.</summary>
        public void LetGo()
        {
            _pinned = false;
            _source = null;
            _open.Clear();
        }

        /// <summary>For a card drawn where it is, in a panel: its words can be rested on all the same.</summary>
        public void Placed(List<HotSpot> spots, GuiRect rect)
        {
            _placedSpots = spots;
            _placed = rect;
        }

        public void Clear()
        {
            _source = null;
            _asked = null;
            _alongside = null;
            _askedAlongside = null;
            _open.Clear();
            _rects.Clear();
        }

        protected override void OnDraw(GuiDrawList draw)
        {
            base.OnDraw(draw);
            if (Context == null) return;

            double now = Engine.Time;
            Vector2 mouse = Context.MousePosition;
            GuiRect screen = Bounds;
            Func<Card?>? asked = _asked;
            List<HotSpot>? placedSpots = _placedSpots;
            _asked = null;
            _placedSpots = null;

            bool ctrl = Bindings.Down(GameAction.Details);
            if (ctrl && !_ctrlDown) _pinned = _source != null;
            if (!ctrl) _pinned = false;
            _ctrlDown = ctrl;

            if (!Held || _source == null)
            {
                _source = asked;
                _beside = _askedBeside;
                _above = _askedAbove;
                _loose = asked != null && _askedLoose;
                _alongside = _askedAlongside;
            }

            bool held = Held;
            if (!held) _open.Clear();

            _rects.Clear();
            _layers.Clear();

            Card? card = GameSession.Current != null ? _source?.Invoke() : null;
            if (card == null) _source = null;
            if (card != null && card.Title != _looking)
            {
                _looking = card.Title;
                _open.Clear();
            }

            GuiRect parent = _placed;
            if (card != null)
            {
                var spots = new List<HotSpot>();
                LaidCard laid = LaidCard.Of(Context, card, CardWidth, held, _alongside != null);
                float x = _above ? _beside.Center.X - CardWidth * 0.5f : _beside.Right + Gap;
                if (!_above && x + CardWidth > screen.Width - Margin) x = _beside.X - CardWidth - Gap;
                x = Math.Clamp(x, Margin, MathF.Max(screen.Width - CardWidth - Margin, Margin));
                float y = _above ? _beside.Y - laid.Height - Gap : _beside.Center.Y - laid.Height * 0.5f;
                y = Math.Clamp(y, Margin, MathF.Max(screen.Height - laid.Height - Margin, Margin));
                laid.Draw(Context, draw, x, y, framed: true, spots);
                parent = new GuiRect(x, y, CardWidth, laid.Height);
                _rects.Add(parent);
                _layers.Add(spots);
                if (_alongside != null && Bindings.Down(GameAction.Compare)) DrawAlongside(draw, _alongside(), parent, screen);
            }
            else if (placedSpots != null)
            {
                _layers.Add(placedSpots);
                _rects.Add(default);
            }
            else
            {
                _open.Clear();
                return;
            }

            if (!held) return;
            DrawOpen(draw, parent, screen);
            Follow(draw, mouse, now, card == null);
        }

        // Beside a card and level with it, for reading the two against each other. They go on whichever side has room, keeping clear of the thing itself and of each other, and are only there to be read.
        private void DrawAlongside(GuiDrawList draw, List<Card> cards, GuiRect card, GuiRect screen)
        {
            float right = MathF.Max(card.Right, _beside.Right);
            float left = MathF.Min(card.X, _beside.X);
            foreach (Card other in cards)
            {
                LaidCard laid = LaidCard.Of(Context!, other, CardWidth, held: true);
                float x;
                if (right + Gap + CardWidth <= screen.Width - Margin)
                {
                    x = right + Gap;
                    right = x + CardWidth;
                }
                else
                {
                    x = MathF.Max(left - Gap - CardWidth, Margin);
                    left = x;
                }
                float y = Math.Clamp(card.Y, Margin, MathF.Max(screen.Height - laid.Height - Margin, Margin));
                laid.Draw(Context!, draw, x, y, framed: true, new List<HotSpot>());
            }
        }

        // Each card opened from the one before it, level with what opened it.
        private void DrawOpen(GuiDrawList draw, GuiRect parent, GuiRect screen)
        {
            for (int i = 0; i < _open.Count; i++)
            {
                HotSpot from = _open[i];
                foreach (HotSpot spot in _layers[i])
                {
                    if (spot.Opens(from)) from = spot;
                }

                Card opened = from.Card ?? WordCard(from.Term!);
                float width = from.Card != null ? CardWidth : WordWidth;
                LaidCard laid = LaidCard.Of(Context!, opened, width, held: true);
                float x = parent.Right + Gap;
                if (x + width > screen.Width - Margin) x = parent.X - width - Gap;
                x = Math.Clamp(x, Margin, MathF.Max(screen.Width - width - Margin, Margin));
                float y = Math.Clamp(from.Rect.Y - Gap, Margin, MathF.Max(screen.Height - laid.Height - Margin, Margin));

                var spots = new List<HotSpot>();
                laid.Draw(Context!, draw, x, y, framed: true, spots);
                draw.AddLine(new Vector2(from.Rect.X, from.Rect.Bottom), new Vector2(from.Rect.Right, from.Rect.Bottom), Ui.TermInk, 1f);
                parent = new GuiRect(x, y, width, laid.Height);
                _rects.Add(parent);
                _layers.Add(spots);
            }
        }

        private static Card WordCard(Term term)
        {
            var card = new Card { Title = term.Name };
            card.Text(term.Says);
            return card;
        }

        // Opens what's being rested on, and closes what the mouse has left behind.
        private void Follow(GuiDrawList draw, Vector2 mouse, double now, bool placed)
        {
            int top = -1;
            for (int i = _rects.Count - 1; i >= 0; i--)
            {
                if (i == 0 && placed ? _placed.Contains(mouse) : _rects[i].Contains(mouse))
                {
                    top = i;
                    break;
                }
            }

            HotSpot? under = null;
            if (top >= 0)
            {
                foreach (HotSpot spot in _layers[top])
                {
                    if (spot.Rect.Contains(mouse)) under = spot;
                }
            }

            if (top >= 1 && top == _open.Count) _keepOpen = now + Linger;

            if (under is HotSpot wanted)
            {
                draw.AddLine(new Vector2(wanted.Rect.X, wanted.Rect.Bottom), new Vector2(wanted.Rect.Right, wanted.Rect.Bottom), Ui.TermInk, 1f);
                if (top < _open.Count && _open[top].Opens(wanted))
                {
                    _keepOpen = now + Linger;
                    _settling = null;
                }
                else if (_settling is HotSpot settling && settling.Opens(wanted))
                {
                    if (now - _settlingSince < Settle) return;
                    _open.RemoveRange(top, _open.Count - top);
                    _open.Add(wanted);
                    _keepOpen = now + Linger;
                    _settling = null;
                }
                else
                {
                    _settling = wanted;
                    _settlingSince = now;
                }
                return;
            }

            _settling = null;
            int keep = Math.Max(top, 0);
            if (now > _keepOpen && _open.Count > keep) _open.RemoveRange(keep, _open.Count - keep);
        }
    }

    /// <summary>A card drawn in place in a panel, and kept up to date for as long as it's there.</summary>
    internal sealed class CardView : GuiElement
    {
        private readonly Func<Card?> _source;

        public CardView(Func<Card?> source)
        {
            _source = source;
        }

        protected override void OnDraw(GuiDrawList draw)
        {
            base.OnDraw(draw);
            if (Context == null || _source() is not Card card) return;

            var spots = new List<HotSpot>();
            LaidCard laid = LaidCard.Of(Context, card, Bounds.Width, GameUi.Current.Tips.Held);
            laid.Draw(Context, draw, Bounds.X, Bounds.Y, framed: false, spots);
            GameUi.Current.Tips.Placed(spots, new GuiRect(Bounds.X, Bounds.Y, Bounds.Width, laid.Height));
        }
    }
}

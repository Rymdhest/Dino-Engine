using Dino_Defenders.Books;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Stats;
using Dino_Defenders.Tooltips;
using Dino_Engine.GUI;
using Dino_Engine.GUI.Rendering;
using Dino_Engine.GUI.Widgets;
using Dino_Engine.Sound;
using Dino_Engine.Sound.Content;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Dino_Defenders.UI
{
    /// <summary>How something is written on a page, which is dark on light, and the only place in the game that is.</summary>
    internal static class Vellum
    {
        public static readonly Colour Sheet = GuiColors.Rgba(226, 209, 172);
        public static readonly Colour Edge = GuiColors.Rgba(150, 122, 84);
        public static readonly Colour Ink = GuiColors.Rgba(46, 33, 22);
        public static readonly Colour Pale = GuiColors.Rgba(112, 92, 66);
        public static readonly Colour Rubric = GuiColors.Rgba(136, 34, 18);
        public static readonly Colour Lit = GuiColors.Rgba(120, 80, 30, 46);
        public static readonly Colour Slip = GuiColors.Rgba(240, 228, 198);

        public static void Write(GuiContext context, GuiDrawList draw, string text, float size, GuiRect rect, Colour colour, Alignment horizontal = Alignment.Start, Alignment vertical = Alignment.Center)
        {
            GuiText.Draw(context, draw, text, Ui.Face, size, rect, colour, horizontal, vertical);
        }
    }

    /// <summary>
    /// What's written on a page, for reading and for changing. Each part of it that was chosen sits in a box of its own, however many words and lines it runs to, and the whole of that box is one thing to click,
    /// which is how anything on a page is ever changed. What's still missing from it is an empty box that does the same. There's no form to fill in: the page is the form.
    /// </summary>
    internal sealed class PageView : GuiElement
    {
        private const float Size = 16f;
        private const float LineHeight = 27f;
        private const float Indent = 18f;
        private const float Pad = 5f;
        private const float Round = 4f;

        private readonly BookPanel _panel;
        private readonly List<(GuiRect rect, PagePlace place)> _spots = new();
        private readonly List<(string word, GuiRect rect)> _words = new();
        private readonly List<GuiRect> _boxes = new();
        private bool _over;

        public PageView(BookPanel panel)
        {
            _panel = panel;
            Width = UIValue.Fill;
            Height = UIValue.Fill;
            HitTest = HitTestMode.Block;
            Clicked += e =>
            {
                if (e.Button != MouseButton.Left) return;
                foreach ((GuiRect rect, PagePlace place) in _spots)
                {
                    if (!rect.Contains(e.Position)) continue;
                    _panel.Pick(place, _spots.FindAll(spot => spot.place == place).ConvertAll(spot => spot.rect));
                    return;
                }
                _panel.PutDown();
            };
        }

        // One part of a page, which is laid out first, as a part is lit or not as a whole and the mouse can be over any of its lines. Long parts break over the end of a line, as anything written does.
        private void Flow(GuiDrawList draw, string text, PagePlace? place, ref float x, ref float y, float left, float right, bool faint = false)
        {
            if (Context == null) return;
            float space = GuiText.MeasureWidth(Context, " ", Ui.Face, Size);
            float pad = place != null ? Pad : 0f;

            _words.Clear();
            _boxes.Clear();
            float from = x;
            float end = x;
            bool empty = true;
            x += pad;
            foreach (string word in text.Split(' '))
            {
                float width = GuiText.MeasureWidth(Context, word, Ui.Face, Size);
                if (x + width + pad > right && x > left + pad)
                {
                    if (!empty) _boxes.Add(new GuiRect(from, y + 2f, end + pad - from, LineHeight - 4f));
                    from = left;
                    x = left + pad;
                    y += LineHeight;
                    empty = true;
                }
                _words.Add((word, new GuiRect(x, y + 1f, width, LineHeight)));
                end = x + width;
                x = end + space;
                empty = false;
            }
            _boxes.Add(new GuiRect(from, y + 2f, end + pad - from, LineHeight - 4f));
            x = end + pad + space;

            bool open = place != null && place == _panel.Open;
            bool over = false;
            if (place is PagePlace at)
            {
                foreach (GuiRect box in _boxes)
                {
                    _spots.Add((box, at));
                    over |= IsHovered && box.Contains(Context.MousePosition);
                }
                _over |= over;

                foreach (GuiRect box in _boxes)
                {
                    if (open) draw.AddRect(box, Vellum.Rubric, Round);
                    else if (over) draw.AddRect(box, Vellum.Slip, Round, 1.5f, Vellum.Rubric);
                    else if (faint) draw.AddRect(box, GuiColors.Transparent, Round, 1f, Vellum.Edge);
                    else draw.AddRect(box, Vellum.Lit, Round, 1f, Vellum.Edge);
                }
            }

            Colour colour = open ? Vellum.Slip : faint && !over ? Vellum.Pale : place != null ? Vellum.Rubric : Vellum.Ink;
            foreach ((string word, GuiRect rect) in _words) Vellum.Write(Context, draw, word, Size, rect, colour);
        }

        protected override void OnDraw(GuiDrawList draw)
        {
            base.OnDraw(draw);
            _spots.Clear();
            Cursor = _over ? MouseCursor.Hand : null;
            _over = false;
            if (Context == null) return;
            if (_panel.Page is not Page page)
            {
                Vellum.Write(Context, draw, "Drag a Page into this Book.", Size, new GuiRect(Bounds.X, Bounds.Y, Bounds.Width, LineHeight), Vellum.Pale);
                return;
            }

            int index = _panel.PageIndex;
            float left = Bounds.X;
            float right = Bounds.Right;
            float y = Bounds.Y;
            List<List<PagePart>> lines = BookText.Parts(page, index);
            int outcomes = page.Outcomes.Count;
            int before = lines.Count - outcomes;

            for (int i = 0; i < lines.Count; i++)
            {
                if (i == before)
                {
                    y += 6f;
                    float heading = left;
                    Flow(draw, "On Impact,", null, ref heading, ref y, left, right);
                    y += LineHeight;
                }

                float start = i >= before ? left + Indent : left;
                float x = start;
                foreach (PagePart part in lines[i]) Flow(draw, part.Text, part.Place, ref x, ref y, start, right);

                // What could still be said goes at the end of what it would be said about.
                if (i == 0 && page.FromWithin == null) Flow(draw, "+ how far off", new PagePlace(Blank.Within), ref x, ref y, start, right, faint: true);
                if (i == 1 && page.Kind == PageKind.Projectile && page.TowardsWithin == null) Flow(draw, "+ how far off", new PagePlace(Blank.Within, Towards: true), ref x, ref y, start, right, faint: true);
                y += LineHeight;
            }

            if (outcomes < Rulebook.MostOutcomes)
            {
                float x = left + Indent;
                Flow(draw, "+ another Outcome", new PagePlace(Blank.Deed, outcomes), ref x, ref y, left + Indent, right, faint: true);
            }
        }
    }

    /// <summary>One thing that could go in the blank that's open, with what it does to the page, and a click writes it in.</summary>
    internal sealed class ChoiceRow : GuiElement
    {
        private const float WordSize = 15f;
        private const float TradeSize = 12.5f;

        private readonly string _words;
        private readonly string _trade;
        private readonly bool _chosen;

        public readonly float RowHeight;

        public ChoiceRow(string words, string trade, bool chosen, Action pressed)
        {
            _words = words;
            _trade = trade;
            _chosen = chosen;
            RowHeight = trade.Length > 0 ? 42f : 26f;
            Width = UIValue.Fill;
            Height = RowHeight;
            HitTest = HitTestMode.Block;
            Clicked += e =>
            {
                if (e.Button == MouseButton.Left) pressed();
            };
        }

        protected override void OnDraw(GuiDrawList draw)
        {
            base.OnDraw(draw);
            if (Context == null) return;
            if (IsHovered || _chosen) draw.AddRect(Bounds, Vellum.Lit.MultiplyAlpha(_chosen ? 1f : 0.6f), 2f);

            float x = Bounds.X + 8f;
            float width = Bounds.Width - 12f;
            Vellum.Write(Context, draw, _words, WordSize, new GuiRect(x, Bounds.Y + 3f, width, 20f), _chosen ? Vellum.Rubric : Vellum.Ink);
            if (_trade.Length > 0) Vellum.Write(Context, draw, _trade, TradeSize, new GuiRect(x, Bounds.Y + 23f, width, 16f), Vellum.Pale);
        }
    }

    /// <summary>Whatever could go in the part of a page that's been clicked, on a slip laid over that page and beside what it's about.</summary>
    internal sealed class ChoiceSlip : GuiElement
    {
        private const float Pad = 8f;
        private const float Gap = 3f;
        private const float RowGap = 2f;
        private const float Heading = 24f;

        private readonly Label _title;

        public readonly ScrollPanel Rows;

        public ChoiceSlip()
        {
            Padding = new Thickness(Pad);
            BackgroundColor = Vellum.Slip;
            BorderColor = Vellum.Rubric;
            BorderWidth = 1f;
            CornerRadius = 3f;
            HitTest = HitTestMode.Block;
            Visible = false;

            var body = Add(new StackPanel(Orientation.Vertical, 2f) { Width = UIValue.Fill, Height = UIValue.Fill });
            _title = body.Add(new Label("") { Font = Ui.Face, FontSize = 14f, Color = Vellum.Pale, Height = Heading - 2f });
            Rows = body.Add(new ScrollPanel { Width = UIValue.Fill, Height = UIValue.Fill });
            Rows.Content.Layout = new StackLayout(Orientation.Vertical, RowGap);
        }

        /// <summary>Lays it over a page, below what it's about if it fits there, and otherwise wherever there's more room.</summary>
        /// <param name="title">What's being chosen.</param>
        /// <param name="from">What it's about, a line at a time, none of which it should cover.</param>
        /// <param name="page">What it has to stay inside of.</param>
        /// <param name="within">What it's placed from.</param>
        public void Lay(string title, List<GuiRect> from, GuiRect page, GuiRect within)
        {
            _title.Text = title;
            float top = from[0].Y;
            float bottom = from[^1].Bottom;
            float wanted = Pad * 2f + Heading;
            foreach (GuiElement row in Rows.Content.Children) wanted += ((ChoiceRow)row).RowHeight + RowGap;

            float below = page.Bottom - bottom - Gap - Pad;
            float above = top - page.Y - Gap - Pad;
            bool under = below >= wanted || below >= above;
            float height = MathF.Min(wanted, under ? below : above);

            Width = page.Width - Pad * 2f;
            Height = height;
            X = page.X + Pad - within.X;
            Y = (under ? bottom + Gap : top - Gap - height) - within.Y;
        }
    }

    /// <summary>A place on a page for a sticker, with what's on it said out loud, as a page's stickers are half of what it is.</summary>
    internal sealed class StickerRow : GuiElement
    {
        private const float Cell = 38f;

        private readonly ItemPlace _place;

        public StickerRow(HeroPanel hands, ItemPlace place)
        {
            _place = place;
            Width = UIValue.Fill;
            Height = Cell;
            Add(new ItemCell(hands, new Spot(Shelf.Held, place.Number), Cell));
        }

        protected override void OnDraw(GuiDrawList draw)
        {
            base.OnDraw(draw);
            if (Context == null || GameUi.Current.Book.At(_place)?.Sticker is not Sticker sticker) return;

            float x = Bounds.X + Cell + 8f;
            float width = Bounds.Width - Cell - 8f;
            Vellum.Write(Context, draw, BookText.Good(sticker), 13f, new GuiRect(x, Bounds.Y + 1f, width, 18f), Vellum.Ink);
            Vellum.Write(Context, draw, BookText.Bad(sticker), 13f, new GuiRect(x, Bounds.Y + 19f, width, 18f), Vellum.Rubric);
        }
    }

    /// <summary>What a book comes to, read off its card, always, on the page for the whole of it. It's as tall as it's written, as a book of six pages has a lot to say.</summary>
    internal sealed class Summary : GuiElement
    {
        private const float Size = 14f;
        private const float RowHeight = 20f;

        private readonly BookPanel _panel;

        public Summary(BookPanel panel)
        {
            _panel = panel;
            Width = UIValue.Fill;
            Height = RowHeight;
        }

        protected override void OnDraw(GuiDrawList draw)
        {
            base.OnDraw(draw);
            if (Context == null || _panel.Reading is not BookProfile book) return;

            float y = Bounds.Y;
            foreach (CardLine line in Cards.Book(book).Lines)
            {
                switch (line.Look)
                {
                    case LineLook.Gap:
                        y += 8f;
                        break;
                    case LineLook.Row:
                    case LineLook.Cell:
                        var row = new GuiRect(Bounds.X, y, Bounds.Width, RowHeight);
                        Vellum.Write(Context, draw, line.Text, Size, row, Vellum.Pale);
                        Vellum.Write(Context, draw, line.Value, Size, row, Vellum.Ink, Alignment.End);
                        y += RowHeight;
                        break;
                    default:
                        foreach (string wrapped in Ui.Wrap(Context, line.Text, Size, Bounds.Width))
                        {
                            Vellum.Write(Context, draw, wrapped, Size, new GuiRect(Bounds.X, y, Bounds.Width, RowHeight), Vellum.Ink);
                            y += RowHeight;
                        }
                        break;
                }
            }
            if (MathF.Abs(Bounds.Height - (y - Bounds.Y)) > 0.5f) Height = y - Bounds.Y;
        }
    }

    /// <summary>
    /// An open book, or a loose page, and nothing else: to open another, right click it. The pack is beside it, as everything that goes in a book comes from there. The left is the page that's turned to and everything about
    /// it, and clicking a part of it lays what could go there over it. The right is the whole book: what it's called, the places it has for pages, and what it comes to.
    /// </summary>
    internal sealed class BookPanel : GuiElement
    {
        private const int PackColumns = 6;
        private const float PackCell = 48f;
        private const float PackGap = 4f;
        private const float PackWidth = PackCell * PackColumns + PackGap * (PackColumns - 1);
        private const float PageCell = 50f;

        private static readonly Colour Leather = GuiColors.Rgba(44, 26, 18, 248);

        private readonly HeroPanel _hands;
        private readonly TextBox _name;
        private readonly Label _kind;
        private readonly Label _numbers;
        private readonly Label _where;
        private readonly Label _whole;
        private readonly Label _turnHint;
        private readonly Button _next;
        private readonly Button _last;
        private readonly StackPanel _stickers;
        private readonly Label _stickerHeading;
        private readonly StackPanel _pages;
        private readonly GuiElement _sheet;
        private readonly ChoiceSlip _slip;

        private ItemPlace _place;
        private int _turnedTo;
        private (int pages, int stickers, int turnedTo, ItemPlace place) _built = (-1, -1, -1, default);
        private bool _naming;

        private static GameSession Session => GameSession.Current;
        private static HeroState Hero => Session.Hero;

        /// <summary>The part of the page whose choices are showing, if any.</summary>
        public PagePlace? Open { get; private set; }

        private Item? Opened => At(_place);

        // Where the page that's turned to is: a place in the book, or the page itself if it's loose.
        private ItemPlace PagePlace => Opened is { IsPage: true } ? _place : _place.In(_turnedTo);

        public Page? Page => At(PagePlace)?.Page;

        /// <summary>Which page of what's written it is, which a gap in a book doesn't count towards.</summary>
        public int PageIndex
        {
            get
            {
                if (Opened?.Held is not Item?[] held || !Opened.IsBook) return 0;
                int index = 0;
                for (int i = 0; i < _turnedTo && i < held.Length; i++)
                {
                    if (held[i] != null) index++;
                }
                return index;
            }
        }

        /// <summary>What it comes to, in this hero's hands, or nothing while there's nothing in it.</summary>
        public BookProfile? Reading => GameSession.Current != null && Opened is Item item && item.Written() is Book book ? Session.Sheet.Preview(Scribe.Read(book, item.Name)) : null;

        /// <summary>What's in a place, if there's a hero to ask.</summary>
        public Item? At(ItemPlace place) => GameSession.Current == null ? null : Hero.At(place);

        /// <summary>Whether this is what's lying open.</summary>
        public bool IsOpen(Item? item) => Visible && item is { IsFolio: true } && Opened == item;

        public BookPanel(HeroPanel hands)
        {
            _hands = hands;
            Anchor = Anchor.Center;
            Width = 1180f;
            Height = 680f;
            Padding = new Thickness(16f);
            Ui.Dress(this);
            BackgroundColor = Leather;
            Visible = false;

            var columns = Add(new StackPanel(Orientation.Horizontal, 14f) { Width = UIValue.Fill, Height = UIValue.Fill });

            var side = columns.Add(new StackPanel(Orientation.Vertical, 6f) { Width = PackWidth, Height = UIValue.Fill });
            side.Add(Ui.Line("Carried", 16f, Ui.Gold));
            var bag = side.Add(new GridPanel(PackColumns, PackGap, PackGap));
            for (int i = 0; i < HeroState.InventorySize; i++) bag.Add(new ItemCell(hands, new Spot(Shelf.Pack, i), PackCell));
            side.Add(new Spacer(height: 4f));
            side.Add(Ui.Paragraph("Drag Pages into the Book and Stickers onto the Page, or Ctrl-click them.", 12.5f, Ui.Faded));
            side.Add(Ui.Paragraph("Right click another Book or Page to open that instead.", 12.5f, Ui.Faded));
            side.Add(Ui.Paragraph("Drag a Book onto the bar to use it.", 12.5f, Ui.Faded));

            var left = columns.Add(new StackPanel(Orientation.Vertical, 8f) { Width = UIValue.Fill, Height = UIValue.Fill, Padding = new Thickness(20f, 14f) });
            left.BackgroundColor = Vellum.Sheet;
            left.BorderColor = Vellum.Edge;
            left.BorderWidth = 1f;
            _sheet = left;

            var turning = left.Add(new StackPanel(Orientation.Horizontal, 6f) { Width = UIValue.Fill, Height = 26f });
            turning.Stack!.CrossAlignment = Alignment.Center;
            _last = turning.Add(Ui.Action("<", () => Turn(-1)));
            _last.Width = 26f;
            _last.Height = 26f;
            _where = turning.Add(new Label("") { Font = Ui.Face, FontSize = 16f, Color = Vellum.Rubric });
            _next = turning.Add(Ui.Action(">", () => Turn(1)));
            _next.Width = 26f;
            _next.Height = 26f;
            turning.Add(new Spacer(width: UIValue.Fill));
            _kind = turning.Add(new Label("") { Font = Ui.Face, FontSize = 14f, Color = Vellum.Pale });

            left.Add(new Spacer(height: 2f));
            left.Add(new PageView(this));

            _stickerHeading = left.Add(new Label("") { Font = Ui.Face, FontSize = 14f, Color = Vellum.Rubric });
            _stickers = left.Add(new StackPanel(Orientation.Vertical, 3f) { Width = UIValue.Fill, Height = UIValue.Auto });
            _numbers = left.Add(new Label("") { Font = Ui.Face, FontSize = 14f, Color = Vellum.Ink });

            var right = columns.Add(new StackPanel(Orientation.Vertical, 8f) { Width = UIValue.Fill, Height = UIValue.Fill, Padding = new Thickness(20f, 14f) });
            right.BackgroundColor = Vellum.Sheet;
            right.BorderColor = Vellum.Edge;
            right.BorderWidth = 1f;
            right.HitTest = HitTestMode.Block;
            right.Clicked += _ => PutDown();

            _whole = right.Add(new Label("") { Font = Ui.Face, FontSize = 16f, Color = Vellum.Rubric, Height = 26f });
            _name = right.Add(new TextBox("", "Name this book") { Width = UIValue.Fill, Height = 30f, MaxLength = HeroState.LongestBookName });
            _name.BackgroundColor = GuiColors.Rgba(60, 44, 30, 235);
            _name.Submitted += _ => _name.Unfocus();

            _pages = right.Add(new StackPanel(Orientation.Horizontal, 4f) { Width = UIValue.Fill, Height = PageCell });
            _turnHint = right.Add(new Label("Right click a Page to turn to it") { Font = Ui.Face, FontSize = 12.5f, Color = Vellum.Pale });

            var reading = right.Add(new ScrollPanel { Width = UIValue.Fill, Height = UIValue.Fill });
            reading.Content.Add(new Summary(this));

            _slip = Add(new ChoiceSlip());
        }

        protected override void OnUpdate(float deltaTime)
        {
            if (GameSession.Current == null) return;
            _hands.Tend();

            // The server has the last word on what's where, and something can be gone from under an open book.
            if (Opened is not { IsFolio: true, IsSticker: false } item)
            {
                Close();
                return;
            }

            // Whatever was typed is what it's called, however the box was left.
            if (_naming && !_name.IsFocused && item.IsBook)
            {
                Session.Client.Title(_place, _name.Text);
                Rebuilt();
            }
            _naming = _name.IsFocused;

            if (item.IsBook && At(PagePlace) == null) TurnToFirst();
            Rebuilt();

            _slip.Visible = Open != null;
            bool book = item.IsBook;
            _whole.Text = book ? "The whole Book" : "Not in any Book";
            _name.Visible = book;
            _pages.Visible = book;
            _turnHint.Visible = book;

            int pages = book ? item.Held?.Count(held => held != null) ?? 0 : 1;
            _where.Text = pages == 0 ? "No Pages yet" : $"Page {PageIndex + 1} of {pages}";
            _last.Visible = book && Neighbour(-1) >= 0;
            _next.Visible = book && Neighbour(1) >= 0;

            Item? shown = At(PagePlace);
            _kind.Text = shown?.Page is Page page ? $"{page.Kind} Page" : "";
            _stickerHeading.Text = shown == null ? "" : shown.Places == 0 ? "No room for Stickers" : "Stickers";

            if (Reading is BookProfile reading && PageIndex < reading.Pages.Length)
            {
                PageProfile profile = reading.Pages[PageIndex];
                string size = profile.Page.Kind == PageKind.Projectile ? $"Projectile Speed {Say.Amount(profile.Speed)} m a second" : profile.Page.Shape == AreaShape.Circle ? $"Radius {Say.Metres(profile.Length)}" : $"Reach {Say.Metres(profile.Length)}";
                string carried = MathF.Abs(profile.Page.Carried - 1f) > 0.001f ? $" (from {Say.Share(profile.Page.Carried)})" : "";
                _numbers.Text = $"Cost {Say.Amount(profile.Cost)}   Potency {Say.Share(profile.Potency)}{carried}   {size}";
            }
            else _numbers.Text = "";
        }

        protected override void OnDrawOverlay(GuiDrawList draw)
        {
            base.OnDrawOverlay(draw);
            _hands.DrawOver(draw);
        }

        // The places change with what's open, and a place is only a number, so nothing is rebuilt but when there are more or fewer of them or they're somewhere else.
        private void Rebuilt()
        {
            if (Opened is not Item item) return;
            int stickers = At(PagePlace)?.Places ?? 0;
            var wanted = (item.IsBook ? item.Places : 0, stickers, _turnedTo, PagePlace);
            if (wanted == _built) return;
            _built = wanted;

            _pages.ClearChildren();
            for (int i = 0; i < wanted.Item1; i++)
            {
                _pages.Add(new ItemCell(_hands, new Spot(Shelf.Held, _place.In(i).Number), PageCell) { Marked = i == _turnedTo });
            }

            _stickers.ClearChildren();
            for (int i = 0; i < stickers; i++) _stickers.Add(new StickerRow(_hands, PagePlace.In(i)));
        }

        private void Close()
        {
            Visible = false;
            Open = null;
        }

        /// <summary>Opens at whatever comes first: what's on the bar, or else what's in the pack.</summary>
        public void OpenAny()
        {
            foreach (ItemPlace place in Hero.BookPlaces())
            {
                Show(place);
                return;
            }
            Close();
        }

        /// <summary>Opens a book at its first page, or a page on its own.</summary>
        public void Show(ItemPlace place)
        {
            if (Hero.At(place) is not { IsFolio: true, IsSticker: false } item) return;
            _place = place;
            _name.Text = item.Name;
            _naming = false;
            Open = null;
            _turnedTo = 0;
            TurnToFirst();
            Offer();
        }

        private void TurnToFirst()
        {
            if (Opened?.Held is Item?[] held && Opened.IsBook) _turnedTo = Math.Max(Array.FindIndex(held, page => page != null), 0);
        }

        // The next place that has a page in it, one way or the other, or -1.
        private int Neighbour(int way)
        {
            if (Opened?.Held is not Item?[] held) return -1;
            for (int i = _turnedTo + way; i >= 0 && i < held.Length; i += way)
            {
                if (held[i] != null) return i;
            }
            return -1;
        }

        /// <summary>Turns to whichever page is in a place in the book.</summary>
        public void TurnTo(ItemPlace place)
        {
            if (place.Holder != _place) return;
            _turnedTo = place.Index;
            Open = null;
            Audio.Play(Interface.Click);
            Offer();
        }

        /// <summary>Whatever's in a cell of the pack, into the first place in what's open that takes it. Says whether it found one.</summary>
        public bool Slot(ItemPlace from)
        {
            if (At(from) is not Item item) return false;
            ItemPlace holder = item.IsSticker ? PagePlace : _place;
            int places = At(holder)?.Places ?? 0;
            for (int i = 0; i < places; i++)
            {
                ItemPlace to = holder.In(i);
                if (At(to) != null || !Hero.CanMove(from, to)) continue;
                Audio.Play(Interface.Equip);
                Session.Client.Move(from, to);
                return true;
            }
            return false;
        }

        /// <summary>Something has gone from one place to another, and what's open goes with it.</summary>
        public void Moved(ItemPlace from, ItemPlace to)
        {
            if (_place == from) _place = to;
            else if (_place == to) _place = from;
        }

        /// <summary>Why something didn't find a place.</summary>
        public string WhyNot(Item item)
        {
            if (item.Sticker is Sticker sticker && Page is Page page && !Stickers.GoesOn(sticker, page.Kind)) return "That Sticker is for another kind of Page";
            return item.IsSticker ? "There's no room on this Page" : "There's no place in this Book for that";
        }

        // Everything that could go in what's open, as a row each.
        private void Offer()
        {
            _slip.Rows.Content.ClearChildren();
            _slip.Rows.ScrollToTop();
            if (Open is not PagePlace open || Page is not Page page) return;

            string? chosen = Current(page, open);
            if (open.Blank == Blank.Within && chosen != null || open.Blank == Blank.Deed && open.Outcome < page.Outcomes.Count && page.Outcomes.Count > 1)
            {
                _slip.Rows.Content.Add(new ChoiceRow("Strike this out", "", false, () => Strike(open)));
            }

            foreach (Sentence sentence in Sentences.For(open.Blank))
            {
                if (!Fits(sentence, page)) continue;
                Sentence offered = sentence;
                _slip.Rows.Content.Add(new ChoiceRow(sentence.Words, string.Join(", ", BookText.Trade(sentence.Weights)), sentence.Id == chosen, () => Choose(offered)));
            }
        }

        private bool Fits(Sentence sentence, Page page)
        {
            if (sentence.Fits is PageKind kind && kind != page.Kind) return false;
            return !sentence.FollowsOnly || PageIndex > 0;
        }

        private static string? Current(Page page, in PagePlace open)
        {
            Outcome? outcome = open.Outcome >= 0 && open.Outcome < page.Outcomes.Count ? page.Outcomes[open.Outcome] : null;
            return open.Blank switch
            {
                Blank.From => page.From,
                Blank.Towards => page.Towards,
                Blank.Shape => page.Shape,
                Blank.Timing => page.Timing,
                Blank.Within => open.Towards ? page.TowardsWithin : page.FromWithin,
                Blank.Deed => outcome?.Deed,
                Blank.Element => outcome?.Element,
                Blank.Measure => outcome?.Measure,
                Blank.Ailment => outcome?.Ailment,
                Blank.Gift => outcome?.Gift,
                Blank.Kin => outcome?.Kin,
                Blank.Pool => outcome?.Pool,
                _ => outcome?.Whom,
            };
        }

        /// <summary>Opens a part of the page, or closes it if it's open already.</summary>
        /// <param name="place">Which part.</param>
        /// <param name="from">Where it is, a line at a time, for what opens to be laid beside it.</param>
        public void Pick(PagePlace place, List<GuiRect> from)
        {
            Audio.Play(Interface.Click);
            Open = Open == place ? null : place;
            Offer();
            if (Open != null) _slip.Lay(BookText.NameOf(place.Blank), from, _sheet.Bounds, ContentBounds);
        }

        /// <summary>Closes whatever part of the page is open.</summary>
        public void PutDown()
        {
            Open = null;
            Offer();
        }

        private void Choose(Sentence sentence)
        {
            if (Open is not PagePlace open || Page is not Page page) return;

            string id = sentence.Id;
            Outcome? outcome = open.Outcome >= 0 && open.Outcome < page.Outcomes.Count ? page.Outcomes[open.Outcome] : null;
            switch (open.Blank)
            {
                case Blank.From: page.From = id; break;
                case Blank.Towards: page.Towards = id; break;
                case Blank.Shape: page.Shape = id; break;
                case Blank.Timing: page.Timing = id; break;
                case Blank.Within when open.Towards: page.TowardsWithin = id; break;
                case Blank.Within: page.FromWithin = id; break;
                case Blank.Deed when outcome == null: page.Outcomes.Add(new Outcome { Deed = id }); break;
                case Blank.Deed: outcome.Deed = id; break;
                case Blank.Element when outcome != null: outcome.Element = id; break;
                case Blank.Measure when outcome != null: outcome.Measure = id; break;
                case Blank.Ailment when outcome != null: outcome.Ailment = id; break;
                case Blank.Gift when outcome != null: outcome.Gift = id; break;
                case Blank.Kin when outcome != null: outcome.Kin = id; break;
                case Blank.Pool when outcome != null: outcome.Pool = id; break;
                case Blank.Whom when outcome != null: outcome.Whom = id; break;
            }
            Open = null;
            Audio.Play(Interface.Equip);
            Commit(page);
        }

        private void Strike(in PagePlace place)
        {
            if (Page is not Page page) return;
            switch (place.Blank)
            {
                case Blank.Within when place.Towards: page.TowardsWithin = null; break;
                case Blank.Within: page.FromWithin = null; break;
                case Blank.Deed when place.Outcome < page.Outcomes.Count: page.Outcomes.RemoveAt(place.Outcome); break;
            }
            Open = null;
            Audio.Play(Interface.Click);
            Commit(page);
        }

        private void Turn(int way)
        {
            int next = Neighbour(way);
            if (next < 0) return;
            _turnedTo = next;
            Open = null;
            Audio.Play(Interface.Click);
            Offer();
        }

        // Done at once and asked of the server, which has the last word: see GameClient.AskGear.
        private void Commit(Page page)
        {
            Session.Client.Write(PagePlace, page);
            Offer();
        }
    }

    /// <summary>
    /// A place on the bar. It's a place like any on the hero while there's a pack open to bring something from, which a book has beside it. Otherwise the mouse is what's aimed with, so it's there only for the moment a right button comes down on a
    /// book, which opens it: a button held since before it got here belongs to the fight, and the attack mustn't stop every time the mouse crosses the bar.
    /// </summary>
    internal sealed class BarCell : ItemCell
    {
        private bool _heldElsewhere;

        public BarCell(HeroPanel panel, int slot, float size)
            : base(panel, new Spot(Shelf.Bar, slot), size)
        {
            ShowsPlaces = false;
        }

        private bool OpensBook => !_heldElsewhere && Context != null && Context.IsMouseDown(MouseButton.Right) && GameSession.Current is { Health: > 0f } session && session.Hero.At(Spot.Place) != null;

        public override bool HitTestSelf(Vector2 point) => (GameUi.Current.Hero.Active || OpensBook) && base.HitTestSelf(point);

        protected override void OnUpdate(float deltaTime)
        {
            _heldElsewhere = Context != null && Context.IsMouseDown(MouseButton.Right);
        }

        // Nothing else that's always showing has a hero to read from before there is one.
        protected override void OnDraw(GuiDrawList draw)
        {
            if (GameSession.Current != null) base.OnDraw(draw);
        }
    }

    /// <summary>
    /// The bar, with dodging and the rest beside it. Six places take a book each, and those are real places: what's dragged here lands the same way it would anywhere on the hero. It stops the mouse only while the pack is
    /// open, as otherwise the mouse is what's aimed with, but for a right click on a book: see <see cref="BarCell"/>.
    /// </summary>
    internal sealed class ActionBar : GuiElement
    {
        private const float Size = 62f;
        private const float Gap = 8f;
        private const float Bottom = 26f;

        public static readonly string[] Keys = { "LMB", "RMB", "Q", "1", "2", "3" };

        private readonly BarCell[] _cells = new BarCell[HeroState.BarSlots];

        private readonly record struct Ability(string Key, string Name, float Share, bool Ready, Func<Card?> Card);

        public ActionBar(HeroPanel panel)
        {
            Anchor = Anchor.Bottom;
            Height = Size;
            Width = 800f;
            Margin = new Thickness(0f, 0f, 0f, Bottom);
            HitTest = HitTestMode.PassThrough;
            for (int slot = 0; slot < _cells.Length; slot++)
            {
                _cells[slot] = Add(new BarCell(panel, slot, Size) { X = slot * (Size + Gap) });
            }
        }

        private static bool Resting(GuiContext context, GuiRect rect)
        {
            if (GameUi.Current.AnyOpen || context.IsMouseDown(MouseButton.Left) || context.IsMouseDown(MouseButton.Right)) return false;
            return rect.Contains(context.MousePosition);
        }

        private List<Ability> Others(GameSession session)
        {
            var abilities = new List<Ability>(3);
            HeroSimState sim = session.Sim;
            abilities.Add(new("Space", "Dodge", sim.DodgeCooldown / MathF.Max(session.Sheet.DodgeCooldown, 0.1f), true, () => Cards.Dodge(session.Sheet)));
            abilities.Add(new("R", $"Flask {session.Hero.Flasks}", 0f, session.Hero.Flasks > 0, () => Cards.Flask(session.Hero, session.Sheet)));
            if (session.Hero.HasGuidingWind) abilities.Add(new("F", "Wind", session.GuideCooldown / Rulebook.GuideCooldown, true, Cards.Wind));
            return abilities;
        }

        protected override void OnDrawOverlay(GuiDrawList draw)
        {
            base.OnDrawOverlay(draw);
            if (Context == null || GameSession.Current is not GameSession session) return;

            HeroSimState sim = session.Sim;
            for (int slot = 0; slot < _cells.Length; slot++)
            {
                GuiRect cell = _cells[slot].Bounds;
                BookProfile? book = session.Client.Kit.Slots[slot];
                Item? item = session.Hero.Bar[slot];

                float wait = book != null && book.Cooldown > 0f ? Math.Clamp(sim.Cooldowns[slot] / book.Cooldown, 0f, 1f) : 0f;
                if (wait > 0f) draw.AddRect(new GuiRect(cell.X, cell.Y + cell.Height * (1f - wait), cell.Width, cell.Height * wait), GuiColors.Rgba(20, 16, 12, 170), 2f);

                bool on = HeroRules.IsOn(sim, slot);
                bool affordable = book == null || book.Mode switch
                {
                    BookMode.Toggle => on || HeroRules.CanTurnOn(sim, session.Client.Kit, book),
                    _ => book.CostsHealth ? session.Health > book.Cost : sim.Mana >= book.Cost,
                };
                if (!affordable) draw.AddRect(cell, GuiColors.Rgba(20, 24, 70, 150), 2f);

                // What goes by itself shows how far it's got, and what's on shows that it is.
                if (book is { Mode: BookMode.Trigger })
                {
                    float charged = Math.Clamp(sim.Charged[slot], 0f, 1f);
                    draw.AddRect(new GuiRect(cell.X, cell.Y + cell.Height * (1f - charged), cell.Width, cell.Height * charged), GuiColors.Rgba(214, 172, 92, charged >= 1f ? 90 : 46), 2f);
                }
                if (on) draw.AddRect(cell.Inflate(1f), GuiColors.Rgba(214, 172, 92, 40), 3f, 2f, Ui.Gold);

                if (item != null)
                {
                    float nameSize = item.Name.Length > 8 ? 10.5f : 12f;
                    draw.PushClip(cell);
                    Ui.Text(Context, draw, item.Name, nameSize, new GuiRect(cell.X + 2f, cell.Y + 2f, cell.Width - 4f, 16f), affordable ? Ui.Parchment : Ui.Faded, Alignment.Center);
                    draw.PopClip();
                }
                Ui.Text(Context, draw, Keys[slot], 11.5f, new GuiRect(cell.X, cell.Bottom - 20f, cell.Width, 18f), Ui.Gold, Alignment.Center);

                if (book != null && Resting(Context, cell))
                {
                    BookProfile shown = book;
                    GameUi.Current.Tips.Show(() => Cards.Book(shown), cell, above: true);
                }
            }

            float x = Bounds.X + _cells.Length * (Size + Gap);
            float y = Bounds.Y;
            foreach (Ability ability in Others(session))
            {
                GuiRect slot = new GuiRect(x, y, Size, Size);
                draw.AddRect(slot, Ui.Ink, 3f, 1f, GuiColors.Rgba(120, 96, 58));

                float share = Math.Clamp(ability.Share, 0f, 1f);
                if (share > 0f) draw.AddRect(new GuiRect(x, y + Size * (1f - share), Size, Size * share), GuiColors.Rgba(150, 120, 70, 90), 3f);

                Colour colour = !ability.Ready ? Ui.Faded.MultiplyAlpha(0.6f) : share > 0f ? Ui.Faded : Ui.Parchment;
                Ui.Text(Context, draw, ability.Name, 12.5f, new GuiRect(x, y + 8f, Size, 22f), colour, Alignment.Center);
                Ui.Text(Context, draw, ability.Key, 11.5f, new GuiRect(x, y + Size - 22f, Size, 18f), Ui.Gold, Alignment.Center);
                if (Resting(Context, slot)) GameUi.Current.Tips.Show(ability.Card, slot, above: true);
                x += Size + Gap;
            }
        }

        protected override void OnUpdate(float deltaTime)
        {
            if (GameSession.Current is not GameSession session) return;
            int others = session.Hero.HasGuidingWind ? 3 : 2;
            Width = (_cells.Length + others) * Size + (_cells.Length + others - 1) * Gap;
        }
    }
}

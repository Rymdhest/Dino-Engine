using Dino_Defenders.Books;
using Dino_Defenders.Client;
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
        public static readonly Colour Number = GuiColors.Rgba(20, 12, 6);
        public static readonly Colour Term = GuiColors.Rgba(26, 62, 128);

        /// <summary>A card on a page, and anything else written on one.</summary>
        public static readonly Inks Written = new(Ink, Number, Pale, Rubric, Term);

        /// <summary>What's written in a box that's been filled in.</summary>
        public static readonly Inks OnRubric = new(Slip, Slip, Slip, Slip, Slip);

        /// <summary>One line, with what means something marked and able to be rested on, as anywhere else: see <see cref="Ui.Rich"/>.</summary>
        /// <param name="spots">Gets where each marked word ended up, which has to be handed to <see cref="TipLayer.Placed"/>.</param>
        /// <param name="links">Cards whose names in this line open them.</param>
        /// <param name="inks">What it's written in, if it's not on the page itself.</param>
        public static void Mark(GuiContext context, GuiDrawList draw, string text, float size, GuiRect rect, Colour colour, List<HotSpot> spots, IReadOnlyList<Card>? links = null, Inks? inks = null)
        {
            float height = context.GetFont(Ui.Face, size).LineHeight / context.PixelScale;
            Ui.Rich(context, draw, text, size, rect.X, rect.Y + (rect.Height - height) * 0.5f, colour, links ?? Array.Empty<Card>(), spots, inks ?? Written);
        }
    }

    /// <summary>A line on a page that isn't what's written there, and is read the same way.</summary>
    internal sealed class PageLine : GuiElement
    {
        private readonly float _size;
        private readonly Colour _colour;
        private readonly List<HotSpot> _spots = new();
        private string _text;

        public PageLine(string text, float size, Colour colour)
        {
            _text = text;
            _size = size;
            _colour = colour;
            HitTest = HitTestMode.PassThrough;
        }

        public string Text
        {
            get => _text;
            set
            {
                if (value == _text) return;
                _text = value;
                InvalidateLayout();
            }
        }

        protected override Vector2 MeasureContent(Vector2 reference, Vector2 available)
        {
            Vector2 children = base.MeasureContent(reference, available);
            return Context == null ? children : Vector2.ComponentMax(children, Context.MeasureText(_text, Ui.Face, _size));
        }

        protected override void OnDraw(GuiDrawList draw)
        {
            base.OnDraw(draw);
            if (Context == null) return;

            _spots.Clear();
            Vellum.Mark(Context, draw, _text, _size, Bounds, _colour, _spots);
            GameUi.Current.Tips.Placed(_spots, Bounds);
        }
    }

    /// <summary>What a page says, which is what it was found as and never changes: where it starts, where it goes or what it covers, and what it does on Impact. It reads what the page comes to, stickers and all.</summary>
    internal sealed class PageView : GuiElement
    {
        private const float Size = 16f;
        private const float LineHeight = 27f;
        private const float Indent = 18f;

        private readonly BookPanel _panel;
        private readonly List<HotSpot> _marked = new();

        public PageView(BookPanel panel)
        {
            _panel = panel;
            Width = UIValue.Fill;
            Height = UIValue.Fill;
            HitTest = HitTestMode.PassThrough;
        }

        // One line of a page, broken over the end of a line as anything written is.
        private void Flow(GuiDrawList draw, string text, ref float y, float left, float right)
        {
            if (Context == null) return;
            float space = GuiText.MeasureWidth(Context, " ", Ui.Face, Size);
            float x = left;
            int runStart = 0;
            int reached = 0;
            foreach (string word in text.Split(' '))
            {
                float width = GuiText.MeasureWidth(Context, word, Ui.Face, Size);
                if (x + width > right && x > left)
                {
                    Vellum.Mark(Context, draw, text.Substring(runStart, reached - 1 - runStart), Size, new GuiRect(left, y + 1f, right - left, LineHeight), Vellum.Ink, _marked, _panel.Links);
                    x = left;
                    y += LineHeight;
                    runStart = reached;
                }
                x += width + space;
                reached += word.Length + 1;
            }
            Vellum.Mark(Context, draw, text.Substring(runStart), Size, new GuiRect(left, y + 1f, right - left, LineHeight), Vellum.Ink, _marked, _panel.Links);
            y += LineHeight;
        }

        protected override void OnDraw(GuiDrawList draw)
        {
            base.OnDraw(draw);
            if (Context == null) return;

            _marked.Clear();
            GameUi.Current.Tips.Placed(_marked, Bounds);
            if (_panel.Page is not Page page || _panel.Shown is not PageProfile shown)
            {
                Vellum.Mark(Context, draw, "Drag a Page into this Book.", Size, new GuiRect(Bounds.X, Bounds.Y, Bounds.Width, LineHeight), Vellum.Pale, _marked);
                return;
            }

            // Where it starts, then where it goes or what it covers, and set apart from those, what happens.
            List<string> lines = BookText.Lines(page, shown.Page, shown.Potency);
            float y = Bounds.Y;
            Flow(draw, lines[0], ref y, Bounds.X, Bounds.Right);
            Flow(draw, $"then {lines[1]}", ref y, Bounds.X + Indent, Bounds.Right);
            y += 6f;
            Flow(draw, lines[2], ref y, Bounds.X, Bounds.Right);
        }
    }

    /// <summary>A place on a page for a sticker, with what's on it said out loud, as a page's stickers are half of what it is.</summary>
    internal sealed class StickerRow : GuiElement
    {
        private const float Cell = 38f;

        private readonly ItemPlace _place;
        private readonly List<HotSpot> _spots = new();

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
            _spots.Clear();
            Vellum.Mark(Context, draw, BookText.Good(sticker), 13f, new GuiRect(x, Bounds.Y + 1f, width, 18f), Vellum.Ink, _spots);
            Vellum.Mark(Context, draw, BookText.Bad(sticker), 13f, new GuiRect(x, Bounds.Y + 19f, width, 18f), Vellum.Rubric, _spots);
            GameUi.Current.Tips.Placed(_spots, Bounds);
        }
    }

    /// <summary>
    /// An open book, or a loose page, and nothing else: to open another, right click it. The pack is beside it, as everything that goes in a book comes from there. The left is the page that's turned to and everything about
    /// it, which is only read, as a page is what it was found as. The right is the whole book: what it's called, the places it has for pages, and what it comes to.
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
        private readonly PageLine _kind;
        private readonly PageLine _numbers;
        private readonly PageLine _where;
        private readonly PageLine _whole;
        private readonly PageLine _turnHint;
        private readonly Button _next;
        private readonly Button _last;
        private readonly StackPanel _stickers;
        private readonly PageLine _stickerHeading;
        private readonly StackPanel _pages;

        private ItemPlace _place;
        private int _turnedTo;
        private (int pages, int stickers, int turnedTo, ItemPlace place) _built = (-1, -1, -1, default);
        private bool _naming;
        private Card? _summary;

        private static GameSession Session => GameSession.Current;
        private static HeroState Hero => Session.Hero;

        private Item? Opened => At(_place);

        // Where the page that's turned to is: a place in the book, or the page itself if it's loose.
        private ItemPlace PagePlace => Opened is { IsPage: true } ? _place : _place.In(_turnedTo);

        public Page? Page => At(PagePlace)?.Page;

        /// <summary>What the page that's turned to comes to in this hero's hands, if there's anything to read.</summary>
        public PageProfile? Shown { get; private set; }

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

        /// <summary>Whatever's named on this book's card opens from that name, on its pages as well.</summary>
        public IReadOnlyList<Card> Links => _summary?.Attached ?? (IReadOnlyList<Card>)Array.Empty<Card>();

        // What it comes to, in this hero's hands, or nothing while there's nothing in it.
        private BookProfile? Reading => GameSession.Current != null && Opened is Item item && item.Written() is Book book ? Session.Sheet.Preview(Scribe.Read(book, item.Name)) : null;

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
            side.Add(Ui.Paragraph("", 12.5f, Ui.Faded).Bind(() => $"Drag Pages into the Book and Stickers onto the Page, or {Bindings.Label(GameAction.Details)}-click them."));
            side.Add(Ui.Paragraph("Right click another Book or Page to open that instead.", 12.5f, Ui.Faded));
            side.Add(Ui.Paragraph("Drag a Book onto the bar to use it.", 12.5f, Ui.Faded));

            var left = columns.Add(new StackPanel(Orientation.Vertical, 8f) { Width = UIValue.Fill, Height = UIValue.Fill, Padding = new Thickness(20f, 14f) });
            left.BackgroundColor = Vellum.Sheet;
            left.BorderColor = Vellum.Edge;
            left.BorderWidth = 1f;

            var turning = left.Add(new StackPanel(Orientation.Horizontal, 6f) { Width = UIValue.Fill, Height = 26f });
            turning.Stack!.CrossAlignment = Alignment.Center;
            _last = turning.Add(Ui.Action("<", () => Turn(-1)));
            _last.Width = 26f;
            _last.Height = 26f;
            _where = turning.Add(new PageLine("", 16f, Vellum.Rubric));
            _next = turning.Add(Ui.Action(">", () => Turn(1)));
            _next.Width = 26f;
            _next.Height = 26f;
            turning.Add(new Spacer(width: UIValue.Fill));
            _kind = turning.Add(new PageLine("", 14f, Vellum.Pale));

            left.Add(new Spacer(height: 2f));
            left.Add(new PageView(this));

            _stickerHeading = left.Add(new PageLine("", 14f, Vellum.Rubric));
            _stickers = left.Add(new StackPanel(Orientation.Vertical, 3f) { Width = UIValue.Fill, Height = UIValue.Auto });
            _numbers = left.Add(new PageLine("", 14f, Vellum.Ink));

            var right = columns.Add(new StackPanel(Orientation.Vertical, 8f) { Width = UIValue.Fill, Height = UIValue.Fill, Padding = new Thickness(20f, 14f) });
            right.BackgroundColor = Vellum.Sheet;
            right.BorderColor = Vellum.Edge;
            right.BorderWidth = 1f;
            right.HitTest = HitTestMode.Block;

            _whole = right.Add(new PageLine("", 16f, Vellum.Rubric) { Height = 26f });
            _name = right.Add(new TextBox("", "Name this book") { Width = UIValue.Fill, Height = 30f, MaxLength = HeroState.LongestBookName });
            _name.BackgroundColor = GuiColors.Rgba(60, 44, 30, 235);
            _name.Submitted += _ => _name.Unfocus();

            _pages = right.Add(new StackPanel(Orientation.Horizontal, 4f) { Width = UIValue.Fill, Height = PageCell });
            _turnHint = right.Add(new PageLine("Right click a Page to turn to it", 12.5f, Vellum.Pale));

            var reading = right.Add(new ScrollPanel { Width = UIValue.Fill, Height = UIValue.Fill });
            reading.Content.Add(new CardView(() => _summary, Vellum.Written, reading));
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
            _kind.Text = shown?.Page is Page page ? $"{page.Hand} {page.Kind} Page" : "";
            _stickerHeading.Text = shown == null ? "" : shown.Places == 0 ? "No room for Stickers" : "Stickers";

            BookProfile? written = Reading;
            _summary = written != null ? Summary(written) : null;
            Shown = written is BookProfile read && PageIndex < read.Pages.Length ? read.Pages[PageIndex] : null;
            if (Shown is PageProfile profile)
            {
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

        // What a book comes to is its card, always, on the page for the whole of it. What it's called and what it is are already at the top of that page.
        private static Card Summary(BookProfile book)
        {
            Card card = Cards.Book(book);
            card.Title = "";
            card.Kind = "";
            return card;
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
            _turnedTo = 0;
            TurnToFirst();
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
            Audio.Play(Interface.Click);
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

        private void Turn(int way)
        {
            int next = Neighbour(way);
            if (next < 0) return;
            _turnedTo = next;
            Audio.Play(Interface.Click);
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
    /// The bar, with dodging and the rest beside it, and Health and Mana along the top of it, as what's left and what it can pay for are read together. Six places take a book each, and those are real places: what's dragged
    /// here lands the same way it would anywhere on the hero. It stops the mouse only while the pack is open, as otherwise the mouse is what's aimed with, but for a right click on a book: see <see cref="BarCell"/>.
    /// </summary>
    internal sealed class ActionBar : GuiElement
    {
        private const float Size = 62f;
        private const float Gap = 8f;
        private const float Bottom = 26f;
        private const float HealthHeight = 16f;
        private const float ManaHeight = 12f;
        private const float PoolGap = 3f;
        private const float PoolsAbove = 6f;

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
            abilities.Add(new(Bindings.Label(GameAction.Dodge), "Dodge", sim.DodgeCooldown / MathF.Max(session.Sheet.DodgeCooldown, 0.1f), true, () => Cards.Dodge(session.Sheet)));
            abilities.Add(new(Bindings.Label(GameAction.Flask), $"Flask {session.Hero.Flasks}", 0f, session.Hero.Flasks > 0, () => Cards.Flask(session.Hero, session.Sheet)));
            if (session.Hero.HasGuidingWind) abilities.Add(new(Bindings.Label(GameAction.Guide), "Wind", session.GuideCooldown / Rulebook.GuideCooldown, true, Cards.Wind));
            return abilities;
        }

        protected override void OnDrawOverlay(GuiDrawList draw)
        {
            base.OnDrawOverlay(draw);
            if (Context == null || GameSession.Current is not GameSession session) return;

            DrawPools(draw, session);

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
                Ui.Text(Context, draw, Bindings.Label(Bindings.Slot(slot)), 11.5f, new GuiRect(cell.X, cell.Bottom - 20f, cell.Width, 18f), Ui.Gold, Alignment.Center);

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

        // As wide as the bar, whatever's on it. They're only drawn, so the mouse goes through them to the world.
        private void DrawPools(GuiDrawList draw, GameSession session)
        {
            ActorSheet sheet = session.Sheet;
            GuiRect mana = new GuiRect(Bounds.X, Bounds.Y - PoolsAbove - ManaHeight, Bounds.Width, ManaHeight);
            GuiRect health = new GuiRect(Bounds.X, mana.Y - PoolGap - HealthHeight, Bounds.Width, HealthHeight);
            Hud.DrawPools(draw, session, health, mana);

            string reading = session.Barrier > 0f ? $"{MathF.Max(session.Health, 0f):0} / {sheet.MaxHealth:0}  +{session.Barrier:0}" : $"{MathF.Max(session.Health, 0f):0} / {sheet.MaxHealth:0}";
            float below = Hud.BarrierHeight + 1f;
            Ui.Text(Context!, draw, reading, 12.5f, new GuiRect(health.X, health.Y + below, health.Width, health.Height - below), Ui.Parchment, Alignment.Center);
            Ui.Text(Context!, draw, $"{session.Sim.Mana:0} / {sheet.MaxMana:0}", 11f, mana, Ui.Parchment, Alignment.Center);
        }

        protected override void OnUpdate(float deltaTime)
        {
            if (GameSession.Current is not GameSession session) return;
            int others = session.Hero.HasGuidingWind ? 3 : 2;
            Width = (_cells.Length + others) * Size + (_cells.Length + others - 1) * Gap;
        }
    }
}

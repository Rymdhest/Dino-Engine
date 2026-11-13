using Dino_Defenders.Client;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Books;
using Dino_Defenders.Stats;
using Dino_Defenders.Talents;
using Dino_Defenders.Tooltips;
using Dino_Engine.ECS.Components;
using Dino_Engine.Core;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.GUI;
using Dino_Engine.GUI.Rendering;
using Dino_Engine.GUI.Widgets;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Sound;
using Dino_Engine.Sound.Content;
using Dino_Engine.Util;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Dino_Defenders.UI
{
    /// <summary>Which row of places something is in.</summary>
    internal enum Shelf
    {
        Pack,
        Worn,
        Stock,
        Sold,
        Bar,
        Held,       // Inside something: its index is the place's own number.
        Stash,      // Put away: its index is the tab times the size of one, and the cell.
        Vendor,     // On the beta vendor's table: its index is the tab times BetaVendorPanel.Stride, and the place.
        Bench,      // On the Forge's crafting bench: the first or second place, or what they'd make (CraftingBench).
    }

    /// <summary>A place something can be in this panel: on the hero, on a merchant's table, or in the stash.</summary>
    internal readonly record struct Spot(Shelf Shelf, int Index)
    {
        public bool OnHero => Shelf is Shelf.Pack or Shelf.Worn or Shelf.Bar or Shelf.Held;
        public int Tab => Index / Stash.TabSize;
        public int Cell => Index % Stash.TabSize;
        public ItemPlace Place => Shelf switch
        {
            Shelf.Pack => ItemPlace.Cell(Index),
            Shelf.Bar => ItemPlace.OnBar(Index),
            Shelf.Held => new ItemPlace(Index),
            _ => ItemPlace.Worn((WornSlot)Index),
        };
    }

    /// <summary>How a place should look at the moment.</summary>
    internal enum CellMood
    {
        Plain,
        Lifted,     // What's in it is on the cursor.
        Wanted,     // It's where what's being looked at could go.
        Refused,    // What's on the cursor can't go here.
        Missed,     // It isn't what's being looked for.
    }

    /// <summary>One place for an item: a place on the hero, a cell in the pack, or something on a merchant's table. What's in it is a picture of the thing itself.</summary>
    internal class ItemCell : GuiElement
    {
        public const float Size = 56f;

        private readonly HeroPanel _panel;
        private readonly ItemSlot? _slot;
        private readonly Spot _spot;
        private readonly Func<Spot>? _spotNow;

        /// <summary>Where this cell is. A stash cell is a different place on each tab.</summary>
        public Spot Spot => _spotNow?.Invoke() ?? _spot;

        /// <summary>What's keeping this place from being used, and shows in it, faintly: something two-handed, in the off hand.</summary>
        public Func<Item?>? TakenBy;

        /// <summary>Whether it's the one of its row that's being looked at.</summary>
        public bool Marked;

        /// <summary>Whether what's in it shows how much room it has.</summary>
        protected bool ShowsPlaces = true;

        public ItemCell(HeroPanel panel, Spot spot, float size = Size, ItemSlot? slot = null, Func<Spot>? spotNow = null)
        {
            _panel = panel;
            _spot = spot;
            _spotNow = spotNow;
            _slot = slot;
            Width = size;
            Height = size;
            BackgroundColor = Ui.InkLight;
            BorderColor = GuiColors.Rgba(84, 68, 46);
            BorderWidth = 1f;
            CornerRadius = 2f;
            MouseDown += e => _panel.Press(this, e);
            Clicked += e => _panel.Click(this, e);
            DragStarted += e => _panel.DragBegun(this, e);
            DragEnded += e => _panel.DragEnded(this, e);
            MouseEnter += () => _panel.Look(Spot, Bounds);
            MouseLeave += () => _panel.Look(null, Bounds);
        }

        protected override void OnDraw(GuiDrawList draw)
        {
            base.OnDraw(draw);
            Item? item = _panel.ItemAt(Spot);
            CellMood mood = _panel.MoodOf(this, item);
            if (mood == CellMood.Wanted) draw.AddRect(Bounds, GuiColors.Rgba(214, 172, 92, 44), 2f, 1.5f, Ui.Gold);
            if (mood == CellMood.Refused) draw.AddRect(Bounds, GuiColors.Rgba(172, 40, 30, 60), 2f, 1.5f, Ui.Blood);

            if (item == null)
            {
                if (TakenBy?.Invoke() is Item taker)
                {
                    Ui.DrawItem(draw, taker, Bounds, Ui.Faded, GuiColors.Rgba(255, 255, 255, 60));
                    draw.AddLine(Bounds.Center + new Vector2(-14f, -14f), Bounds.Center + new Vector2(14f, 14f), Ui.Blood, 2f);
                    draw.AddLine(Bounds.Center + new Vector2(14f, -14f), Bounds.Center + new Vector2(-14f, 14f), Ui.Blood, 2f);
                }
                else if (_slot is ItemSlot empty) Ui.DrawSlotIcon(draw, empty, Bounds, Ui.Faded.MultiplyAlpha(0.4f));
                return;
            }

            Colour colour = Ui.RarityColour(item.Rarity);
            if (mood == CellMood.Lifted)
            {
                Ui.DrawItem(draw, item, Bounds, colour, GuiColors.Rgba(255, 255, 255, 46));
                return;
            }
            if (mood == CellMood.Missed)
            {
                Ui.DrawItem(draw, item, Bounds, colour, GuiColors.Rgba(255, 255, 255, 40));
                draw.AddRect(Bounds, GuiColors.Transparent, 2f, 1f, colour.MultiplyAlpha(0.18f));
                return;
            }

            draw.AddRect(Bounds, colour.MultiplyAlpha(0.1f), 2f);
            if (IsHovered) draw.AddRect(Bounds, GuiColors.Rgba(255, 240, 200, 26), 2f);
            Ui.DrawItem(draw, item, Bounds, colour, GuiColors.White);
            if (ShowsPlaces) Ui.DrawPlaces(draw, item, Bounds);
            draw.AddRect(Bounds, GuiColors.Transparent, 2f, 1.5f, Marked ? Ui.Gold : colour.MultiplyAlpha(0.8f));

            if (Context != null && _panel.PriceOn(Spot, item) is int price)
            {
                GuiRect tag = new GuiRect(Bounds.X, Bounds.Bottom - 18f, Bounds.Width - 4f, 16f);
                Ui.Text(Context, draw, price.ToString(), 12.5f, tag, price > GameSession.Current.Hero.Gold ? Ui.Blood : Ui.Gold, Alignment.End);
            }
        }
    }

    /// <summary>One line of the hero's sheet: a name and what it comes to, and everything about it on a card when the mouse rests on it.</summary>
    internal sealed class StatLine : GuiElement
    {
        private const float Size = 13.5f;

        private readonly Func<string> _name;
        private readonly Func<string> _value;

        public readonly Func<Card?> Card;

        public StatLine(Func<string> name, Func<string> value, Func<Card?> card, Action<StatLine?> hover)
        {
            _name = name;
            _value = value;
            Card = card;
            Width = UIValue.Fill;
            Height = 19f;
            HitTest = HitTestMode.Block;
            MouseEnter += () => hover(this);
            MouseLeave += () => hover(null);
        }

        protected override void OnDraw(GuiDrawList draw)
        {
            base.OnDraw(draw);
            if (Context == null || GameSession.Current == null) return;

            if (IsHovered) draw.AddRect(Bounds, GuiColors.Rgba(255, 240, 200, 22), 2f);
            GuiRect text = new GuiRect(Bounds.X + 4f, Bounds.Y, Bounds.Width - 8f, Bounds.Height);
            Ui.Text(Context, draw, _name(), Size, text, IsHovered ? Ui.Parchment : Ui.Faded);
            Ui.Text(Context, draw, _value(), Size, text, Ui.Parchment, Alignment.End);
        }
    }

    /// <summary>The window on the left of the screen beside the hero's own, for whatever's being used (<see cref="HeroPanel.Side"/>).</summary>
    internal sealed class SideWindow : GuiElement
    {
        private readonly Action<GuiDrawList> _over;

        /// <summary>What's in it, top to bottom.</summary>
        public readonly StackPanel Body;

        public SideWindow(Action<GuiDrawList> over)
        {
            _over = over;
            Padding = new Thickness(20f);
            Ui.Dress(this);
            Visible = false;
            Body = Add(new StackPanel(Orientation.Vertical, 6f) { Width = UIValue.Fill, Height = UIValue.Fill });
        }

        protected override void OnDrawOverlay(GuiDrawList draw)
        {
            base.OnDrawOverlay(draw);
            _over(draw);
        }
    }

    /// <summary>The hero as they stand, drawn by the studio every frame the panel is open, in their own pose, with what they wear around them. Drag to turn them.</summary>
    internal sealed class HeroStage : GuiElement
    {
        private const float TurnSpeed = 0.012f;

        private float _turn = 0.4f;

        public HeroStage()
        {
            HitTest = HitTestMode.Block;
            Dragged += e => _turn += e.Delta.X * TurnSpeed;
        }

        protected override void OnDraw(GuiDrawList draw)
        {
            base.OnDraw(draw);
            if (GameSession.Current?.Client is not { } client || client.OwnGhost == null) return;

            ECSWorld world = client.Ecs;
            if (!world.TryGetEntityLocation(client.OwnGhost.Entity, out _, out _)) return;
            EntityView view = world.GetEntityView(client.OwnGhost.Entity);
            if (!view.Has<CharacterModelComponent>() || !view.Has<SkeletonComponent>()) return;

            int texture = Engine.RenderEngine.Studio.Doll(view.Get<CharacterModelComponent>().model, view.Get<SkeletonComponent>().SkinningMatrices, _turn);
            float height = MathF.Min(Bounds.Height, (Bounds.Width - 130f) * 1.5f);
            float width = height / 1.5f;
            GuiRect rect = new GuiRect(Bounds.Center.X - width * 0.5f, Bounds.Y + (Bounds.Height - height) * 0.5f, width, height);
            draw.AddImage(texture, rect, new Vector2(0f, 1f), new Vector2(1f, 0f), GuiColors.White);
        }
    }

    /// <summary>
    /// The hero and what they carry. They stand in the middle in everything they have on, with a place for each thing around them, what they've got beside them, and what they come to down
    /// the side, which is where a merchant's table goes when there is one. Whatever the mouse is over is described beside it, set against what's worn in its place.
    ///
    /// Everything here stays where it was put. What's on the cursor has only been picked up as far as this panel knows: it's still where it was until it's put somewhere, so closing
    /// the panel, falling or losing the server can't lose it. All a click can do is in <see cref="PlanFor"/>, which is also what the cursor says it will do, so the two can't disagree.
    /// </summary>
    internal sealed class HeroPanel : GuiElement
    {
        private const int Columns = 6;
        private const int MarketColumns = 4;
        private const int StockSize = 12;
        private const int FlaskPrice = 12;  // Shown here; the server is what charges it.
        private const float Cell = 52f;
        private const float CellGap = 6f;
        private const float MarketCell = 68f;
        private const float MarketGap = 8f;
        private const float SheetWidth = 290f;
        private const float GearWidth = Columns * Cell + (Columns - 1) * CellGap;
        private const float ColumnGap = 18f;
        private const float Inset = 20f;

        // How wide the window beside this is, for each thing that opens there.
        private const float TableWidth = 340f;
        private const float VendorWidth = 720f;

        private readonly GuiElement _sheet;
        private readonly GuiElement _market;
        private readonly GuiElement _stash;
        private readonly GuiElement _stage;
        private readonly Label _sideTitle;

        /// <summary>
        /// The window that opens on the left beside this one, for whatever's being used: a merchant's table, the stash, the crafting bench or the beta vendor. It's apart from this panel so that the
        /// hero's own side never changes, but every place in it is still this panel's, as it's this panel that knows what a click does.
        /// </summary>
        public readonly SideWindow Side;
        private readonly BetaVendorPanel _vendor;
        private bool _vending;
        private readonly CraftingBench _bench;
        private bool _crafting;
        private readonly List<Button> _tabs = new();
        private bool _stashing;
        private int _tab;
        private readonly Label _title;
        private readonly TextBox _search;
        private readonly Button _clear;
        private readonly SlotPicker _picker;
        private SiteState? _merchant;
        private Spot? _looking;
        private GuiRect _lookingFrom;
        private StatLine? _lookingAt;
        private string[] _words = Array.Empty<string>();
        private Spot? _hand;            // Where what's on the cursor really is.
        private bool _dragging;
        private bool _pressUsed;        // This press has done all it's going to, so what comes of it when it's let go is nothing.
        private bool _mouseWasDown;
        private bool _wasTyping;
        private bool _leftSearch;       // The search box has only just been left, by the same press of Escape that's about to be seen here.
        private GuiRect _pickerFrom;

        private static GameSession Session => GameSession.Current;
        private static HeroState Hero => Session.Hero;

        public HeroPanel()
        {
            Ui.DockRight(this, SheetWidth + ColumnGap + GearWidth + Inset * 2f);
            Padding = new Thickness(Inset);
            Ui.Dress(this);
            Visible = false;

            var columns = Add(new StackPanel(Orientation.Horizontal, ColumnGap) { Width = UIValue.Fill, Height = UIValue.Fill });

            var left = columns.Add(new StackPanel(Orientation.Vertical, 3f) { Width = SheetWidth, Height = UIValue.Fill });
            _title = left.Add(Ui.Heading("").Bind(() => Hero.Name.Length > 0 ? Hero.Name : "The Hero"));
            left.Add(Ui.Line("", 15f, Ui.Parchment).Bind(() => TalentTree.TitleOf(Hero)));
            left.Add(Ui.Line().Bind(() => $"Level {Hero.Level}"));
            left.Add(new Spacer(height: 6f));
            var sheet = left.Add(new ScrollPanel { Width = UIValue.Fill, Height = UIValue.Fill });
            AddStats(sheet.Content.Add(new StackPanel(Orientation.Vertical, 1f) { Width = UIValue.Fill, Height = UIValue.Auto }));
            _sheet = sheet;

            // What's worn on top, and what's carried under it.
            var right = columns.Add(new StackPanel(Orientation.Vertical, 8f) { Width = GearWidth, Height = UIValue.Fill });
            _stage = right.Add(BuildStage());
            right.Add(new Spacer(height: 4f));
            // The search, and the one button that ever moves what's carried by itself, beside it.
            var tools = right.Add(new StackPanel(Orientation.Horizontal, 8f) { Width = UIValue.Fill, Height = 28f });
            var find = tools.Add(new GuiElement { Width = UIValue.Fill, Height = 28f });
            _search = find.Add(new TextBox("", "Search names and stats") { Width = UIValue.Fill, Height = 28f, MaxLength = 64 });
            _search.TextChanged += text => _words = ItemSearch.Words(text);
            _search.Submitted += _ => _search.Unfocus();
            _clear = find.Add(Ui.Action("x", () => _search.Text = ""));
            _clear.Anchor = Anchor.Right;
            _clear.X = 3f;
            _clear.Width = 22f;
            _clear.Height = 22f;
            _clear.Padding = new Thickness(0f);
            _clear.MinWidth = 0f;
            _clear.Visible = false;
            Button sort = tools.Add(Ui.Action("Sort", SortPack));
            sort.Width = 64f;
            sort.Height = 28f;

            var bag = right.Add(new GridPanel(Columns, CellGap, CellGap));
            for (int i = 0; i < HeroState.InventorySize; i++) bag.Add(new ItemCell(this, new Spot(Shelf.Pack, i), Cell));
            right.Add(Ui.Line("", 16f, Ui.Gold).Bind(() => $"{Hero.Gold:N0} gold"));

            _picker = Add(new SlotPicker(this));

            Side = new SideWindow(DrawSideOver);
            _sideTitle = Side.Body.Add(Ui.Heading(""));
            _market = Side.Body.Add(BuildMarket());
            _stash = Side.Body.Add(BuildStash());
            _bench = Side.Body.Add(new CraftingBench(this));
            _vendor = Side.Body.Add(new BetaVendorPanel(this, VendorWidth));
        }

        // Where what's on the table stays, and where anything from the hero is sold by being brought to it.
        private GuiElement BuildMarket()
        {
            var market = new StackPanel(Orientation.Vertical, 6f) { Width = UIValue.Fill, Height = UIValue.Fill, Visible = false };
            market.MouseDown += e => PressOn(market, e);

            market.Add(Ui.Line("For sale", 15f, Ui.Gold));
            var stock = market.Add(new GridPanel(MarketColumns, MarketGap, MarketGap));
            for (int i = 0; i < StockSize; i++) stock.Add(new ItemCell(this, new Spot(Shelf.Stock, i), MarketCell));

            market.Add(new Spacer(height: 2f));
            market.Add(Ui.Line("Sold here, to buy back", 15f, Ui.Gold));
            var sold = market.Add(new GridPanel(MarketColumns, MarketGap, MarketGap));
            for (int i = 0; i < MerchantTable.MostSold; i++) sold.Add(new ItemCell(this, new Spot(Shelf.Sold, i), MarketCell));

            market.Add(new Spacer(height: 2f));
            Button flasks = market.Add(Ui.Action($"Fill flasks ({FlaskPrice} gold each)", BuyFlasks));
            flasks.Height = 28f;
            return market;
        }

        private const float StashCell = 44f;
        private const float StashGap = 4f;

        // What's put away, shared by every hero on this machine, a tab at a time, with the same cells and the same ways of moving things as the pack.
        private GuiElement BuildStash()
        {
            var stash = new StackPanel(Orientation.Vertical, 6f) { Width = UIValue.Fill, Height = UIValue.Fill, Visible = false };
            stash.MouseDown += e => PressOn(stash, e);

            stash.Add(Ui.Line("Shared by all your heroes", 13.5f, Ui.Faded));
            var tabs = stash.Add(new StackPanel(Orientation.Horizontal, 4f) { Width = UIValue.Fill, Height = 28f });
            for (int t = 0; t < Stash.TabCount; t++)
            {
                int tab = t;
                Button button = tabs.Add(Ui.Action(Numeral(tab), () => ShowTab(tab)));
                button.Width = UIValue.Relative(1f / Stash.TabCount, -3f);
                button.Height = 28f;
                _tabs.Add(button);
            }

            var cells = stash.Add(new GridPanel(Stash.Columns, StashGap, StashGap));
            for (int i = 0; i < Stash.TabSize; i++)
            {
                int cell = i;
                cells.Add(new ItemCell(this, new Spot(Shelf.Stash, cell), StashCell, spotNow: () => new Spot(Shelf.Stash, _tab * Stash.TabSize + cell)));
            }
            stash.Add(Ui.Paragraph("", 13.5f, Ui.Blood).Bind(() => Session.Client.Stash == null ? "Your stash is open in another copy of the game." : ""));
            return stash;
        }

        private static string Numeral(int tab) => tab switch { 0 => "I", 1 => "II", 2 => "III", _ => "IV" };

        private void ShowTab(int tab)
        {
            Audio.Play(Interface.Click);
            _tab = tab;
        }

        // Head to toe down one side, what's on them down the other, each hand at the foot of its side, and the hero between.
        private GuiElement BuildStage()
        {
            const float Step = Cell + 10f;
            var stage = new HeroStage { Width = GearWidth, Height = Step * 5f - 10f };
            stage.MouseDown += e => PressOn(stage, e);
            float right = GearWidth - Cell;

            void Place(WornSlot slot, float x, float y)
            {
                var cell = new ItemCell(this, new Spot(Shelf.Worn, (int)slot), Cell, slot: WornSlots.Holds(slot)) { X = x, Y = y };
                if (slot == WornSlot.OffHand) cell.TakenBy = TwoHandedHeld;
                stage.Add(cell);
            }

            Place(WornSlot.Head, 0f, 0f);
            Place(WornSlot.Body, 0f, Step);
            Place(WornSlot.Legs, 0f, Step * 2f);
            Place(WornSlot.Feet, 0f, Step * 3f);
            Place(WornSlot.MainHand, 0f, Step * 4f);

            Place(WornSlot.Neck, right, 0f);
            Place(WornSlot.Hands, right, Step);
            Place(WornSlot.Finger1, right, Step * 2f);
            Place(WornSlot.Finger2, right, Step * 3f);
            Place(WornSlot.OffHand, right, Step * 4f);
            return stage;
        }

        private static Item? TwoHandedHeld() => Hero.Equipped.TryGetValue(WornSlot.MainHand, out Item? held) && held.IsTwoHanded ? held : null;

        // Every line can be looked at, and only what touches this hero shows, so that what's on the sheet is what's in the build.
        private void AddStats(StackPanel parent)
        {
            GuiElement Line(Func<string> name, Func<string> value, Func<Card?> card) => parent.Add(new StatLine(name, value, card, line => _lookingAt = line));

            var lines = new List<(GuiElement line, StatRow row)>();
            var groups = new List<(GuiElement heading, string name)>();
            foreach (string group in StatBook.Rows.Select(row => row.Group).Distinct())
            {
                groups.Add((parent.Add(Ui.Line(group, 14f, Ui.Gold)), group));
                foreach (StatRow row in StatBook.Rows.Where(row => row.Group == group))
                {
                    lines.Add((Line(() => row.Name, () => row.Reads(Session.Sheet), () => Cards.OfRow(row, Session.Sheet)), row));
                }
            }

            // Decided here rather than by each of them, as a hidden element isn't updated and could never show itself again.
            parent.Updated += _ =>
            {
                if (GameSession.Current == null) return;
                ActorSheet sheet = Session.Sheet;
                foreach ((GuiElement line, StatRow row) in lines) line.Visible = row.Shows(sheet);
                foreach ((GuiElement heading, string name) in groups) heading.Visible = StatBook.Rows.Any(row => row.Group == name && row.Shows(sheet));
            };
        }

        /// <summary>Shows the panel, with a merchant's table if it was opened at one.</summary>
        public void Open(SiteState? merchant)
        {
            _merchant = merchant;
            _stashing = false;
            _vending = false;
            _crafting = false;
            _bench.Visible = false;
            _bench.Clear();
            _vendor.Visible = false;
            _stash.Visible = false;
            _market.Visible = merchant != null;
            Beside(merchant?.Site.Name, TableWidth, merchant != null);
            _looking = null;
            _lookingAt = null;
            Forget();
        }

        /// <summary>Shows the panel with the stash beside the pack, as it is at a strongbox.</summary>
        public void OpenStash()
        {
            Open(null);
            _stashing = true;
            _stash.Visible = true;
            Beside("The Stash", TableWidth);
        }

        /// <summary>Shows the panel with the Forge's crafting bench beside the pack, as it is at the bench.</summary>
        public void OpenBench()
        {
            Open(null);
            _crafting = true;
            _bench.Visible = true;
            Beside("Crafting Bench", TableWidth);
        }

        /// <summary>Shows the panel with the beta vendor's table in place of the sheet and the hero, as it is at his stall.</summary>
        public void OpenVendor()
        {
            Open(null);
            _vending = true;
            _vendor.Visible = true;
            Beside(null, VendorWidth);
            _vendor.Opened();
        }

        // Shows the window beside this, as wide as what's in it needs, with its name at the top if it has one of its own to give.
        private void Beside(string? title, float width, bool showing = true)
        {
            Side.Visible = showing && Visible;
            Ui.DockLeft(Side, width + Inset * 2f);
            _sideTitle.Text = title ?? "";
            _sideTitle.Visible = title != null;
        }

        /// <summary>The crafting bench's side of this, for a scripted run to lay things on.</summary>
        public CraftingBench Bench => _bench;

        /// <summary>The beta vendor's side of this, for a scripted run to open at a tab.</summary>
        public BetaVendorPanel Vendor => _vendor;

        /// <summary>Puts back anything of the vendor's that's on the cursor, for when what he has out has changed under it.</summary>
        public void ForgetVendor()
        {
            if (_hand is Spot { Shelf: Shelf.Vendor }) _hand = null;
        }

        /// <summary>Puts down whatever's been picked up, which is only to say it was never anywhere else.</summary>
        public void Forget()
        {
            _hand = null;
            _dragging = false;
            _picker.Visible = false;
        }

        /// <summary>What Escape means here before it means closing the panel. Says whether it meant anything.</summary>
        public bool StepBack()
        {
            if (_leftSearch) return true;
            if (_hand == null && !_picker.Visible) return false;
            Forget();
            return true;
        }

        public void FocusSearch()
        {
            if (_vending && _vendor.LookingUp)
            {
                _vendor.FocusSearch();
                return;
            }
            _search.Focus();
            _search.SelectAll();
        }

        /// <summary>
        /// Whether what the hero has on is on the screen. Nothing is put on without it, as a pack shows beside an open book and the beta vendor too, and wearing something
        /// there changed what's worn with nothing to show what came off.
        /// </summary>
        private bool SeesWorn => Visible && _stage.Visible;

        /// <summary>Whether things can be carried about at the moment, which they can whenever there's a pack on the screen.</summary>
        public bool Active => Visible || GameUi.Current.Book.Visible;

        /// <summary>What has to go on while there's something being carried. It's here for an open book to ask, as this panel isn't updated while it's hidden.</summary>
        public void Tend()
        {
            // Nothing can be held that's no longer there, as when the server has had the last word.
            if (_hand is Spot held && ItemAt(held) == null) _hand = null;
        }

        protected override void OnUpdate(float deltaTime)
        {
            if (GameSession.Current == null || Context == null) return;

            Tend();
            _clear.Visible = _search.Text.Length > 0;

            bool typing = _search.IsFocused || (_vending && _vendor.Typing);
            _leftSearch = _wasTyping && !typing;
            _wasTyping = typing;

            bool down = Context.IsMouseDown(MouseButton.Left) || Context.IsMouseDown(MouseButton.Right);
            if (down && !_mouseWasDown && _picker.Visible && !_picker.Bounds.Contains(Context.MousePosition) && !_pickerFrom.Contains(Context.MousePosition)) _picker.Visible = false;
            _mouseWasDown = down;
        }

        /// <summary>What's in a place in this panel.</summary>
        public Item? ItemAt(Spot spot) => spot.Shelf switch
        {
            Shelf.Stock => OnShelf(_merchant?.Table.Stock, spot.Index),
            Shelf.Sold => OnShelf(_merchant?.Table.Sold, spot.Index),
            Shelf.Stash => Session.Client.Stash?.At(spot.Tab, spot.Cell),
            Shelf.Vendor => _vending ? _vendor.At(spot.Index) : null,
            Shelf.Bench => _crafting ? _bench.At(spot.Index) : null,
            _ => Hero.At(spot.Place),
        };

        private static Item? OnShelf(Item?[]? shelf, int index) => shelf != null && index < shelf.Length ? shelf[index] : null;

        /// <summary>What something on the table costs, to show on it.</summary>
        public int? PriceOn(Spot spot, Item item) => spot.OnHero || spot.Shelf is Shelf.Stash or Shelf.Vendor or Shelf.Bench ? null : MerchantTable.PriceOf(item, spot.Shelf == Shelf.Sold);

        public CellMood MoodOf(ItemCell cell, Item? item)
        {
            Spot spot = cell.Spot;
            if (_hand == spot) return CellMood.Lifted;

            Item? moving = _hand is Spot held ? ItemAt(held) : _looking is Spot looking && looking.Shelf != Shelf.Worn ? ItemAt(looking) : null;
            if (spot.Shelf == Shelf.Worn && moving != null && WornSlots.Fits(moving, (WornSlot)spot.Index) && (_hand == null || _hand.Value.OnHero)) return CellMood.Wanted;
            if (spot.Shelf == Shelf.Bar && moving is { IsBook: true } && (_hand == null || _hand.Value.OnHero)) return CellMood.Wanted;
            if (spot.Shelf == Shelf.Held && moving != null && _looking is not null && (_hand ?? _looking) is Spot source && source != spot && source.OnHero && Hero.CanMove(source.Place, spot.Place)) return CellMood.Wanted;
            if (_hand != null && cell.IsHovered && PlanFor(cell).Refused) return CellMood.Refused;

            if (item == null) return CellMood.Plain;

            // What's on the bench is still in the pack, and shows there faintly, and what couldn't go on it is dimmed.
            if (_crafting && spot.Shelf == Shelf.Pack && _bench.Holds(spot.Index)) return CellMood.Lifted;
            if (_crafting && spot.Shelf == Shelf.Pack && _hand == null && !_bench.Takes(item)) return CellMood.Missed;
            if (_picker.Visible && spot.Shelf == Shelf.Pack) return WornSlots.Fits(item, _picker.Slot) ? CellMood.Wanted : CellMood.Missed;
            return ItemSearch.Finds(item, _words) ? CellMood.Plain : CellMood.Missed;
        }

        /// <summary>The mouse has come to a place or left one.</summary>
        public void Look(Spot? spot, GuiRect from)
        {
            _looking = spot;
            _lookingFrom = from;
        }

        /// <summary>What a click would do with what's on the cursor, if it came down here.</summary>
        private readonly record struct Plan(string Says, Action? Does, bool Refused = false)
        {
            public static readonly Plan Nothing = new("", null);

            public static Plan Refuse(string why) => new(why, null, true);
        }

        private Plan PlanFor(GuiElement? under)
        {
            if (_hand is not Spot from || ItemAt(from) is not Item item) return Plan.Nothing;

            var cell = under as ItemCell;
            if (cell != null && cell.Spot == from) return new Plan("", () => Audio.Play(Interface.Click));

            // Out of the stash, into the pack, or to another of its cells.
            if (from.Shelf == Shelf.Stash)
            {
                if (cell?.Spot.Shelf == Shelf.Pack) return new Plan(Hero.Inventory[cell.Spot.Index] != null ? "Swap" : "Take out", () => Unstow(from, cell.Spot.Index));
                if (cell?.Spot.Shelf == Shelf.Stash) return new Plan(ItemAt(cell.Spot) != null ? "Swap" : "", () => Restack(from, cell.Spot));
                return Plan.Nothing;
            }

            // Into the stash, from anywhere on the hero. Only the pack can take back what's there.
            if (_stashing && cell?.Spot.Shelf == Shelf.Stash)
            {
                if (ItemAt(cell.Spot) != null && from.Shelf != Shelf.Pack) return Plan.Refuse("Put it in your pack first");
                if (from.Shelf == Shelf.Held) return Plan.Refuse("Take it out of the Book first");
                return new Plan(ItemAt(cell.Spot) != null ? "Swap" : "Put away", () => Stow(from, cell.Spot));
            }
            if (_stashing && _stash.Contains(under) && from.OnHero)
            {
                if (from.Shelf == Shelf.Held) return Plan.Refuse("Take it out of the Book first");
                return Session.Client.Stash?.FreeCell(_tab) is int free and >= 0 ? new Plan("Put away", () => Stow(from, new Spot(Shelf.Stash, _tab * Stash.TabSize + free))) : Plan.Refuse("This tab is full");
            }

            // Off the crafting bench, which only ever showed it, or to its other place.
            if (from.Shelf == Shelf.Bench)
            {
                if (from.Index == CraftingBench.Made) return Plan.Nothing;
                if (cell?.Spot.Shelf == Shelf.Bench && cell.Spot.Index != CraftingBench.Made)
                {
                    int moved = cell.Spot.Index;
                    return new Plan(_bench.At(moved) != null ? "Swap" : "Move", () => SwapOnBench(from.Index, moved));
                }
                return new Plan("Take off the bench", () => _bench.TakeOff(from.Index));
            }

            // Onto the crafting bench, from the pack. It stays where it is, and the bench only shows it.
            if (_crafting && from.Shelf == Shelf.Pack && (cell?.Spot.Shelf == Shelf.Bench || (cell == null && _bench.Contains(under))))
            {
                int? place = cell?.Spot.Shelf == Shelf.Bench ? cell.Spot.Index : _bench.FreePlace;
                if (place is not int into || into == CraftingBench.Made) return Plan.Refuse("Put it in the first or second place");
                if (_bench.WhyNotPut(from.Index, into) is string why) return Plan.Refuse(why);
                int benching = from.Index;
                return new Plan("Put on the bench", () => _bench.Put(benching, into));
            }

            // From the beta vendor, into the pack, for nothing, and it stays where it was.
            if (from.Shelf == Shelf.Vendor)
            {
                if (cell == null || cell.Spot.Shelf != Shelf.Pack) return Plan.Nothing;
                if (!Hero.HasRoom()) return Plan.Refuse("Your pack is full");
                int into = cell.Spot.Index;
                return new Plan("Take", () => TakeOffer(from, into));
            }

            // From the table, the only place it can go is the pack.
            if (!from.OnHero)
            {
                if (cell == null || cell.Spot.Shelf != Shelf.Pack) return Plan.Nothing;
                int price = MerchantTable.PriceOf(item, from.Shelf == Shelf.Sold);
                if (Hero.Gold < price) return Plan.Refuse("You can't afford that");
                if (!Hero.HasRoom()) return Plan.Refuse("Your pack is full");
                int into = cell.Spot.Index;
                return new Plan($"Buy for {price} gold", () => Buy(from, into));
            }

            if (_merchant != null && _market.Contains(under)) return new Plan($"Sell for {item.SellValue} gold", () => Sell(from));
            if (under is GroundCatcher) return new Plan("Drop on the ground", () => Drop(from));
            if (under is HeroStage && from.Shelf == Shelf.Pack && item.IsWorn) return new Plan("Wear", () => Wear(from.Index, null));
            if (cell == null) return Plan.Nothing;

            ItemPlace to = cell.Spot.Place;

            // Dropped on what holds it, it goes into the first place there that takes it: a Sentence on a Page, a Page in a Book, and a Weapon or a Book given to a Summon Page's Minions.
            if (Hero.At(to) is Item holder && Into(item, holder) is string putting)
            {
                return FirstPlaceIn(from.Place, to, item, holder) is ItemPlace free
                    ? new Plan(putting, () => Move(from.Place, free))
                    : Plan.Refuse(NoRoomIn(item, holder));
            }

            if (!Hero.CanMove(from.Place, to)) return Plan.Refuse(WhyNot(item, cell.Spot));

            string says = cell.Spot.Shelf == Shelf.Worn ? "Wear" : cell.Spot.Shelf == Shelf.Bar && Hero.At(to) == null ? "Put on the Bar" : Hero.At(to) != null ? "Swap" : cell.Spot.Shelf == Shelf.Held ? item.IsSentence ? "Put on the Page" : item.IsPage ? "Put in the Book" : "Give to the Minions" : "";
            return new Plan(says, () => Move(from.Place, to));
        }

        // What putting this into that is called, if it's something that goes in it.
        private static string? Into(Item item, Item holder)
        {
            if (holder.IsPage && item.IsSentence) return "Put on the Page";
            if (holder.IsBook && item.IsPage) return "Put in the Book";
            if (holder.Summons && (item.IsWeapon || item.IsBook || item.FitsOffHand)) return "Give to the Minions";
            return null;
        }

        // The first empty place in something that this could go in, in order, or nothing if there's none.
        private static ItemPlace? FirstPlaceIn(ItemPlace from, ItemPlace holderAt, Item item, Item holder)
        {
            if (holder.Summons && (item.IsWeapon || item.IsBook || item.FitsOffHand))
            {
                // The weapon's hand first, then the other, then the book's place: whichever of them is empty and takes it.
                foreach (int index in new[] { holder.MinionWeaponPlace, holder.MinionOffHandPlace, holder.MinionBookPlace })
                {
                    ItemPlace given = holderAt.In(index);
                    if (Hero.At(given) == null && Hero.CanMove(from, given)) return given;
                }
                return null;
            }

            int places = holder.IsPage ? holder.SentencePlaces : holder.Places;
            for (int i = 0; i < places; i++)
            {
                ItemPlace to = holderAt.In(i);
                if (Hero.At(to) == null && Hero.CanMove(from, to)) return to;
            }
            return null;
        }

        private static string NoRoomIn(Item item, Item holder)
        {
            if (item.Sentence is Sentence sentence && holder.Page != null && !Sentences.GoesOn(sentence, holder.Page.Kind)) return "That Sentence is for another kind of Page";
            if (item.IsPage && holder.Mode == BookMode.Toggle && item.Page?.Kind != PageKind.Area) return "An Aura Book holds only Area Pages";
            if (holder.Summons) return item.IsBook && !Item.FitsMinions(item) ? "Minions only use a pressed Book with no Summon Page in it" : "Its Minions already have one";
            return holder.IsPage ? "There's no room on this Page" : "There's no place in this Book for that";
        }

        private static string WhyNot(Item item, Spot to)
        {
            if (to.Shelf == Shelf.Worn && WornSlots.Fits(item, (WornSlot)to.Index)) return "Your pack is full";
            if (to.Shelf != Shelf.Held) return "That doesn't go there";
            if (item.IsPage && Hero.At(to.Place.Holder) is { Mode: BookMode.Toggle }) return "An Aura Book holds only Area Pages";
            if (item.IsPage && Hero.At(to.Place.Holder) is { IsBook: true }) return "Nothing can come after a Summon Page";
            return item.IsSentence && Hero.At(to.Place.Holder) is { IsPage: true } ? "That Sentence is for another kind of Page" : "That doesn't go there";
        }

        // A plan that can't be done leaves what's on the cursor there, to be put somewhere that it can go.
        private void Carry(Plan plan)
        {
            if (plan.Refused) Refuse(plan.Says);
            if (plan.Does == null) return;
            _hand = null;
            plan.Does();
        }

        private static void Refuse(string why)
        {
            Audio.Play(Interface.Refuse);
            Session.Note($"{why}.");
        }

        /// <summary>What's on the cursor, if anything.</summary>
        public Item? Holding => _hand is Spot held ? ItemAt(held) : null;

        /// <summary>What the cursor should say it's about to do.</summary>
        public (string says, bool refused) Intent()
        {
            Plan plan = PlanFor(Context?.HoveredElement);
            return (plan.Says, plan.Refused);
        }

        private static bool Control => Bindings.Down(GameAction.Details);
        private static bool Shift => Bindings.Down(GameAction.ForceAttack);

        /// <summary>The mouse has come down on a place. Picking up and all the quick ways of doing things happen on the press, as a run of them shouldn't have to wait for each to be let go.</summary>
        public void Press(ItemCell cell, GuiMouseEventArgs e)
        {
            Item? item = ItemAt(cell.Spot);
            _pressUsed = true;

            if (e.Button == MouseButton.Right)
            {
                if (_hand != null) _hand = null;
                else if (item != null) Use(cell.Spot, item, rightClick: true);
                return;
            }
            if (e.Button != MouseButton.Left) return;

            if (_hand != null) Carry(PlanFor(cell));
            else if (item != null && Control) Use(cell.Spot, item, rightClick: false);
            else
            {
                _pressUsed = false;

                // What's worn can be dragged off, but a click on it is for seeing what else could go there.
                if (item != null && cell.Spot.Shelf != Shelf.Worn) Lift(cell.Spot);
            }
        }

        /// <summary>The mouse has come down on something that isn't a place, but that something can be brought to.</summary>
        public void PressOn(GuiElement element, GuiMouseEventArgs e)
        {
            if (_hand == null) return;
            if (e.Button == MouseButton.Right) _hand = null;
            else if (e.Button == MouseButton.Left) Carry(PlanFor(element));
        }

        /// <summary>A press has come back up on the same place, without going anywhere.</summary>
        public void Click(ItemCell cell, GuiMouseEventArgs e)
        {
            if (_pressUsed || e.Button != MouseButton.Left || cell.Spot.Shelf != Shelf.Worn) return;

            var place = (WornSlot)cell.Spot.Index;
            if (_picker.Visible && _picker.Slot == place)
            {
                _picker.Visible = false;
                return;
            }
            _pickerFrom = cell.Bounds;
            _picker.Open(place, cell.Bounds, ContentBounds);
        }

        public void DragBegun(ItemCell cell, GuiDragEventArgs e)
        {
            if (_pressUsed || e.Button != MouseButton.Left) return;
            if (_hand == null && cell.Spot.Shelf == Shelf.Worn && ItemAt(cell.Spot) != null) Lift(cell.Spot);
            _dragging = _hand != null;
        }

        public void DragEnded(ItemCell cell, GuiDragEventArgs e)
        {
            if (!_dragging) return;
            _dragging = false;
            if (_hand == null || Context == null) return;

            // A click that wandered a little. What's worn has nowhere to wait, as a click on it means something else.
            GuiElement? under = Context.HitTestAll(e.Position);
            if (under == cell)
            {
                if (cell.Spot.Shelf == Shelf.Worn) _hand = null;
                return;
            }

            // Letting go is the end of it, unlike a click, which can be tried again somewhere else.
            Carry(PlanFor(under));
            _hand = null;
        }

        private void Lift(Spot spot)
        {
            _hand = spot;
            _picker.Visible = false;
            Audio.Play(Interface.Click);
        }

        // The quick ways of doing things: whatever's most likely wanted of something, without carrying it anywhere.
        private void Use(Spot spot, Item item, bool rightClick)
        {
            switch (spot.Shelf)
            {
                case Shelf.Pack or Shelf.Bar when (item.IsBook || item.IsPage) && rightClick:
                    GameUi.Current.Read(spot.Place);
                    break;
                case Shelf.Held when item.IsPage && rightClick:
                    GameUi.Current.Book.TurnTo(spot.Place);
                    break;
                case Shelf.Held:
                    Unslot(spot);
                    break;
                case Shelf.Stash:
                    TakeOut(spot);
                    break;
                case Shelf.Vendor:
                    TakeOffer(spot, null);
                    break;
                case Shelf.Bench:
                    _bench.TakeOff(spot.Index);
                    break;
                case Shelf.Pack when _crafting && !rightClick:
                    PutOnBench(spot);
                    break;
                case Shelf.Pack when _stashing && !rightClick:
                    PutAway(spot);
                    break;
                case Shelf.Pack when _merchant != null && !rightClick:
                    Sell(spot);
                    break;
                case Shelf.Pack when item.IsBook:
                    ToBar(spot);
                    break;
                case Shelf.Pack when item.IsFolio:
                    if (GameUi.Current.Book.Visible && !GameUi.Current.Book.Slot(spot.Place)) Refuse(GameUi.Current.Book.WhyNot(item));
                    break;
                case Shelf.Pack when item.IsOrders:
                    Refuse("Orders are given to the map table in the Haven");
                    break;
                case Shelf.Bar:
                    FromBar(spot);
                    break;
                case Shelf.Pack when item.IsWorn && !SeesWorn:
                    Refuse($"Open the hero panel ({Bindings.Label(GameAction.Hero)}) to wear that");
                    break;
                case Shelf.Pack:
                    Wear(spot.Index, item.Slot == ItemSlot.Finger && Shift ? WornSlot.Finger2 : null);
                    break;
                case Shelf.Worn:
                    TakeOff((WornSlot)spot.Index);
                    break;
                default:
                    Buy(spot, null);
                    break;
            }
            GameUi.Current.Tips.LetGo();
        }

        // Into the first free cell of the tab that's showing, or of any tab.
        private void PutAway(Spot from)
        {
            if (Session.Client.Stash is not Stash stash)
            {
                Refuse("Your stash is open in another copy of the game");
                return;
            }
            for (int look = 0; look < Stash.TabCount; look++)
            {
                int tab = (_tab + look) % Stash.TabCount;
                int free = stash.FreeCell(tab);
                if (free < 0) continue;
                Stow(from, new Spot(Shelf.Stash, tab * Stash.TabSize + free));
                return;
            }
            Refuse("Your stash is full");
        }

        private void TakeOut(Spot from)
        {
            if (!Hero.HasRoom())
            {
                Refuse("Your pack is full");
                return;
            }
            Audio.Play(Interface.Click);
            Session.Client.Unstow(from.Tab, from.Cell);
        }

        // All of these are done here at once and asked of the server, which has the last word, the same as anything else that moves what's carried.
        private static void Stow(Spot from, Spot to)
        {
            Audio.Play(Interface.Click);
            Session.Client.Stow(from.Place, to.Tab, to.Cell);
        }

        private static void Unstow(Spot from, int into)
        {
            Audio.Play(Interface.Click);
            Session.Client.Unstow(from.Tab, from.Cell, into);
        }

        private static void Restack(Spot from, Spot to)
        {
            Audio.Play(Interface.Click);
            Session.Client.Shift(from.Tab, from.Cell, to.Tab, to.Cell);
        }

        private static void ToBar(Spot from)
        {
            int slot = Hero.FreeOnBar();
            if (slot < 0)
            {
                Refuse("Your bar is full");
                return;
            }
            Move(from.Place, ItemPlace.OnBar(slot));
        }

        private static void FromBar(Spot from)
        {
            int cell = Hero.FreeCell();
            if (cell < 0)
            {
                Refuse("Your pack is full");
                return;
            }
            Move(from.Place, ItemPlace.Cell(cell));
        }

        private static void Unslot(Spot from)
        {
            int cell = Hero.FreeCell();
            if (cell < 0)
            {
                Refuse("Your pack is full");
                return;
            }
            Move(from.Place, ItemPlace.Cell(cell));
        }

        // All of these are done here at once and asked of the server, which has the last word: see GameClient.AskGear.
        private static void Move(ItemPlace from, ItemPlace to)
        {
            Audio.Play(from.IsCell && to.IsCell ? Interface.Click : Interface.Equip);
            Session.Client.Move(from, to);
            GameUi.Current.Book.Moved(from, to);
        }

        /// <summary>Puts on what's in a cell, and what comes off takes its place.</summary>
        public void Wear(int cell, WornSlot? place)
        {
            if (Hero.Inventory[cell] is not Item item || !item.IsWorn) return;
            if (!Hero.CanMove(ItemPlace.Cell(cell), ItemPlace.Worn(Hero.PlaceFor(item, place))))
            {
                Refuse("Your pack is full");
                return;
            }
            Audio.Play(Interface.Equip);
            Session.Client.Equip(cell, place);
        }

        /// <summary>Takes something off, into the first free cell.</summary>
        public void TakeOff(WornSlot place)
        {
            if (!Hero.HasRoom())
            {
                Refuse("Your pack is full");
                return;
            }
            Audio.Play(Interface.Equip);
            Session.Client.Unequip(place);
        }

        private void Sell(Spot from)
        {
            if (_merchant == null) return;
            Audio.Play(Interface.Trade);
            Session.Client.Sell(Session.Sites.IndexOf(_merchant), from.Place);
        }

        private void Buy(Spot from, int? cell)
        {
            if (_merchant == null || ItemAt(from) is not Item item) return;

            bool buyingBack = from.Shelf == Shelf.Sold;
            if (Hero.Gold < MerchantTable.PriceOf(item, buyingBack))
            {
                Refuse("You can't afford that");
                return;
            }
            if (!Hero.HasRoom())
            {
                Refuse("Your pack is full");
                return;
            }
            Audio.Play(Interface.Trade);
            Session.Client.Buy(Session.Sites.IndexOf(_merchant), from.Index, buyingBack, cell);
        }

        private void TakeOffer(Spot from, int? cell)
        {
            if (!_vending || ItemAt(from) == null) return;
            if (!Hero.HasRoom())
            {
                Refuse("Your pack is full");
                return;
            }
            Audio.Play(Interface.Trade);
            _vendor.Take(from.Index, cell);
        }

        private static void Drop(Spot from)
        {
            Audio.Play(Interface.Click);
            Session.Client.Drop(from.Place);
        }

        // Into the first free place on the bench, or the second if both are taken, which is the one most often changed. Something already there comes off.
        private void PutOnBench(Spot from)
        {
            if (_bench.Holds(from.Index))
            {
                _bench.TakeOff(ReferenceEquals(_bench.At(CraftingBench.First), ItemAt(from)) ? CraftingBench.First : CraftingBench.Second);
                return;
            }
            int into = _bench.FreePlace ?? CraftingBench.Second;
            if (_bench.WhyNotPut(from.Index, into) is string why)
            {
                Refuse(why);
                return;
            }
            _bench.Put(from.Index, into);
        }

        private void SwapOnBench(int from, int to)
        {
            int? fromCell = BenchCell(from);
            int? toCell = BenchCell(to);
            if (fromCell is int moving) _bench.Put(moving, to);
            if (toCell is int other) _bench.Put(other, from);
            else _bench.TakeOff(from);
        }

        private int? BenchCell(int index) => _bench.At(index) is Item item && Array.IndexOf(Hero.Inventory, item) is int cell and >= 0 ? cell : null;

        private void SortPack()
        {
            // What's on the cursor is known by where it is, and that's about to mean something else.
            Forget();
            _bench.Clear();
            Session.Client.SortPack();
        }

        private static void BuyFlasks()
        {
            Audio.Play(Interface.Trade);
            Session.Client.BuyFlasks();
        }

        protected override void OnDrawOverlay(GuiDrawList draw)
        {
            base.OnDrawOverlay(draw);
            DrawOver(draw);
        }

        // Over the window beside this: where what's on the cursor can be brought, which is all of the merchant's side, all of the stash or all of the bench, and which tab of the stash is showing.
        private void DrawSideOver(GuiDrawList draw)
        {
            if (_merchant != null && _hand is Spot held && held.OnHero) draw.AddRect(_market.Bounds.Inflate(5f), GuiColors.Rgba(214, 172, 92, 18), 3f, 1.5f, Ui.Gold.MultiplyAlpha(0.8f));
            if (_crafting && _hand is Spot benched && benched.Shelf == Shelf.Pack) draw.AddRect(_bench.Bounds.Inflate(5f), GuiColors.Rgba(250, 156, 92, 14), 3f, 1.5f, GuiColors.Rgba(250, 156, 92, 160));
            if (_stashing && _hand is Spot lifted && lifted.OnHero) draw.AddRect(_stash.Bounds.Inflate(5f), GuiColors.Rgba(214, 188, 146, 14), 3f, 1.5f, GuiColors.Rgba(214, 188, 146, 160));
            for (int t = 0; t < _tabs.Count && _stashing; t++)
            {
                if (t == _tab) draw.AddRect(_tabs[t].Bounds.Inflate(1f), GuiColors.Transparent, 2f, 1.5f, Ui.Gold);
            }
        }

        /// <summary>What goes over everything while this is what's holding things: it's here for an open book to ask too.</summary>
        public void DrawOver(GuiDrawList draw)
        {
            if (Context == null) return;


            if (_lookingAt is StatLine line) GameUi.Current.Tips.Show(line.Card, line.Bounds);
            if (_hand != null || _looking is not Spot spot) return;

            // The list opened from a place goes right where that place's card would.
            if (_picker.Visible && spot.Shelf == Shelf.Worn) return;

            // What the vendor has out changes under the mouse, as his tabs turn and his lookup is narrowed, so his card is of whatever's under it now, if anything is.
            GuiRect from = _lookingFrom;
            if (spot.Shelf == Shelf.Vendor)
            {
                if (Context.HoveredElement is not ItemCell over || over.Spot.Shelf != Shelf.Vendor) return;
                spot = over.Spot;
                from = over.Bounds;
            }

            Item? item = ItemAt(spot) ?? (spot == new Spot(Shelf.Worn, (int)WornSlot.OffHand) ? TwoHandedHeld() : null);
            if (item == null) return;

            string? price = spot.Shelf switch
            {
                Shelf.Stock => Cards.Costs(item),
                Shelf.Sold => Cards.BuyBack(item),
                Shelf.Vendor => "Free to take",
                Shelf.Bench when spot.Index == CraftingBench.Made => $"Fusing costs {Crafting.FuseCost(item)} gold",
                _ => null,
            };
            Ui.DrawTooltip(Hero, item, from, price, compare: spot.Shelf != Shelf.Worn, loose: true);
        }
    }
}

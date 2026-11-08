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
            float height = MathF.Min(Bounds.Height - 20f, (Bounds.Width - 130f) * 1.5f);
            float width = height / 1.5f;
            GuiRect rect = new GuiRect(Bounds.Center.X - width * 0.5f, Bounds.Y + (Bounds.Height - height) * 0.5f - 24f, width, height);
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
        private const float CellGap = 6f;
        private const float MarketCell = 68f;
        private const float MarketGap = 8f;
        private const float StageWidth = 450f;

        private readonly GuiElement _sheet;
        private readonly GuiElement _market;
        private readonly GuiElement _stash;
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
            Anchor = Anchor.Center;
            Width = 1200f;
            Height = 720f;
            Padding = new Thickness(20f);
            Ui.Dress(this);
            Visible = false;

            var columns = Add(new StackPanel(Orientation.Horizontal, 18f) { Width = UIValue.Fill, Height = UIValue.Fill });

            var left = columns.Add(new StackPanel(Orientation.Vertical, 3f) { Width = 300f, Height = UIValue.Fill });
            _title = left.Add(Ui.Heading("The Hero"));
            left.Add(Ui.Line("", 15f, Ui.Parchment).Bind(() => TalentTree.TitleOf(Hero)));
            left.Add(Ui.Line().Bind(() => $"Level {Hero.Level}, {Hero.Gold} gold"));
            left.Add(Ui.Line("", 14f, Ui.Gold).Bind(() => Hero.UnspentPoints > 0 ? $"{Hero.UnspentPoints} talents to spend  [{Bindings.Label(GameAction.Talents)}]" : " "));
            left.Add(new Spacer(height: 6f));
            var sheet = left.Add(new ScrollPanel { Width = UIValue.Fill, Height = UIValue.Fill });
            AddStats(sheet.Content.Add(new StackPanel(Orientation.Vertical, 1f) { Width = UIValue.Fill, Height = UIValue.Auto }));
            _sheet = sheet;
            _market = left.Add(BuildMarket());
            _stash = left.Add(BuildStash());

            columns.Add(BuildStage());

            var right = columns.Add(new StackPanel(Orientation.Vertical, 8f) { Width = UIValue.Fill, Height = UIValue.Fill });
            var heading = right.Add(new StackPanel(Orientation.Horizontal, 8f) { Width = UIValue.Fill, Height = 26f });
            heading.Stack!.CrossAlignment = Alignment.Center;
            heading.Add(Ui.Line("Carried", 15f, Ui.Gold));
            heading.Add(Ui.Line("", 14f, Ui.Faded).Bind(Tally));
            heading.Add(new Spacer(height: 1f));
            Button sort = heading.Add(Ui.Action("Sort", SortPack));
            sort.Width = 64f;
            sort.Height = 26f;

            var find = right.Add(new GuiElement { Width = UIValue.Fill, Height = 28f });
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

            var bag = right.Add(new GridPanel(Columns, CellGap, CellGap));
            for (int i = 0; i < HeroState.InventorySize; i++) bag.Add(new ItemCell(this, new Spot(Shelf.Pack, i)));

            var help = right.Add(new StackPanel(Orientation.Vertical, 2f) { Width = UIValue.Fill, Height = UIValue.Auto, Margin = new Thickness(0f, 6f, 0f, 0f) });
            help.Add(Ui.Line("Click to pick up and put down, or drag", 13f, Ui.Faded));
            help.Add(Ui.Line("", 13f, Ui.Faded).Bind(() => _stashing ? $"{Bindings.Label(GameAction.Details)}-click to put away or take out, right-click to wear" : _merchant != null ? $"{Bindings.Label(GameAction.Details)}-click to sell, right-click to wear" : $"{Bindings.Label(GameAction.Details)}-click or right-click to wear"));
            help.Add(Ui.Line("Right-click a Book or a Page to open it, and drag a Book to the bar to use it", 13f, Ui.Faded));
            help.Add(Ui.Line("Drag off the panel to drop on the ground", 13f, Ui.Faded));
            help.Add(Ui.Line("Click a place on the hero to see what fits it", 13f, Ui.Faded));
            help.Add(Ui.Line("", 13f, Ui.Faded).Bind(() => $"Hold {Bindings.Label(GameAction.Compare)} to compare with what's worn"));

            _picker = Add(new SlotPicker(this));
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

        // Head to toe down one side, what's on them down the other, and both hands below.
        private GuiElement BuildStage()
        {
            var stage = new HeroStage { Width = StageWidth, Height = UIValue.Fill };
            stage.MouseDown += e => PressOn(stage, e);
            float step = ItemCell.Size + 22f;
            float right = StageWidth - ItemCell.Size;

            void Place(WornSlot slot, float x, float y)
            {
                var cell = new ItemCell(this, new Spot(Shelf.Worn, (int)slot), slot: WornSlots.Holds(slot)) { X = x, Y = y };
                if (slot == WornSlot.OffHand) cell.TakenBy = TwoHandedHeld;
                stage.Add(cell);
            }

            Place(WornSlot.Head, 0f, 8f);
            Place(WornSlot.Body, 0f, 8f + step);
            Place(WornSlot.Legs, 0f, 8f + step * 2f);
            Place(WornSlot.Feet, 0f, 8f + step * 3f);

            Place(WornSlot.Neck, right, 8f);
            Place(WornSlot.Hands, right, 8f + step);
            Place(WornSlot.Finger1, right, 8f + step * 2f);
            Place(WornSlot.Finger2, right, 8f + step * 3f);

            float bottom = 600f;
            Place(WornSlot.MainHand, StageWidth * 0.5f - ItemCell.Size - 12f, bottom);
            Place(WornSlot.OffHand, StageWidth * 0.5f + 12f, bottom);
            return stage;
        }

        private static Item? TwoHandedHeld() => Hero.Equipped.TryGetValue(WornSlot.MainHand, out Item? held) && held.IsTwoHanded ? held : null;

        // Every line can be looked at, and only what touches this hero shows, so that what's on the sheet is what's in the build.
        private void AddStats(StackPanel parent)
        {
            GuiElement Line(Func<string> name, Func<string> value, Func<Card?> card) => parent.Add(new StatLine(name, value, card, line => _lookingAt = line));

            parent.Add(Ui.Line("Your main book", 14f, Ui.Gold));
            static BookProfile? Main() => Session.Client.Kit.Slots[0];
            Line(() => Main()?.Book.Name ?? "Nothing", () => Main() is BookProfile main && main.DamagePerSecond > 0f ? Say.N(main.DamagePerSecond) : "", () => Main() is BookProfile main ? Cards.Book(main) : null);

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
            _stash.Visible = false;
            _market.Visible = merchant != null;
            _sheet.Visible = merchant == null;
            _title.Text = merchant != null ? merchant.Site.Name : "The Hero";
            _looking = null;
            _lookingAt = null;
            Forget();
        }

        /// <summary>Shows the panel with the stash beside the pack, as it is at a strongbox.</summary>
        public void OpenStash()
        {
            Open(null);
            _stashing = true;
            _sheet.Visible = false;
            _stash.Visible = true;
            _title.Text = "The Stash";
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
            _search.Focus();
            _search.SelectAll();
        }

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

            _leftSearch = _wasTyping && !_search.IsFocused;
            _wasTyping = _search.IsFocused;

            bool down = Context.IsMouseDown(MouseButton.Left) || Context.IsMouseDown(MouseButton.Right);
            if (down && !_mouseWasDown && _picker.Visible && !_picker.Bounds.Contains(Context.MousePosition) && !_pickerFrom.Contains(Context.MousePosition)) _picker.Visible = false;
            _mouseWasDown = down;
        }

        private string Tally()
        {
            if (_words.Length == 0) return $"{Hero.Carried().Count()} of {HeroState.InventorySize}";

            IEnumerable<Item> looking = Hero.Carried();
            if (_merchant != null) looking = looking.Concat(_merchant.Table.Stock.OfType<Item>()).Concat(_merchant.Table.Sold.OfType<Item>());
            if (_stashing && Session.Client.Stash is Stash stash) looking = looking.Concat(stash.Kept());
            return $"{looking.Count(item => ItemSearch.Finds(item, _words))} found";
        }

        /// <summary>What's in a place in this panel.</summary>
        public Item? ItemAt(Spot spot) => spot.Shelf switch
        {
            Shelf.Stock => OnShelf(_merchant?.Table.Stock, spot.Index),
            Shelf.Sold => OnShelf(_merchant?.Table.Sold, spot.Index),
            Shelf.Stash => Session.Client.Stash?.At(spot.Tab, spot.Cell),
            _ => Hero.At(spot.Place),
        };

        private static Item? OnShelf(Item?[]? shelf, int index) => shelf != null && index < shelf.Length ? shelf[index] : null;

        /// <summary>What something on the table costs, to show on it.</summary>
        public int? PriceOn(Spot spot, Item item) => spot.OnHero || spot.Shelf == Shelf.Stash ? null : MerchantTable.PriceOf(item, spot.Shelf == Shelf.Sold);

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
            if (!Hero.CanMove(from.Place, to)) return Plan.Refuse(WhyNot(item, cell.Spot));

            string says = cell.Spot.Shelf == Shelf.Worn ? "Wear" : cell.Spot.Shelf == Shelf.Bar && Hero.At(to) == null ? "Put on the Bar" : Hero.At(to) != null ? "Swap" : cell.Spot.Shelf == Shelf.Held ? item.IsSticker ? "Stick on the Page" : "Put in the Book" : "";
            return new Plan(says, () => Move(from.Place, to));
        }

        private static string WhyNot(Item item, Spot to)
        {
            if (to.Shelf == Shelf.Worn && WornSlots.Fits(item, (WornSlot)to.Index)) return "Your pack is full";
            if (to.Shelf != Shelf.Held) return "That doesn't go there";
            if (item.IsPage && Hero.At(to.Place.Holder) is { Mode: BookMode.Toggle }) return "A Toggle Book holds only Area Pages";
            if (item.IsPage && Hero.At(to.Place.Holder) is { IsBook: true }) return "Nothing can come after an Area Page";
            return item.IsSticker && Hero.At(to.Place.Holder) is { IsPage: true } ? "That Sticker is for another kind of Page" : "That doesn't go there";
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

        private static void Drop(Spot from)
        {
            Audio.Play(Interface.Click);
            Session.Client.Drop(from.Place);
        }

        private void SortPack()
        {
            // What's on the cursor is known by where it is, and that's about to mean something else.
            Forget();
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

        /// <summary>What goes over everything while this is what's holding things: it's here for an open book to ask too.</summary>
        public void DrawOver(GuiDrawList draw)
        {
            if (Context == null) return;

            // Where it can be sold is all of the merchant's side, and where it can be put away all of the stash.
            if (_merchant != null && _hand is Spot held && held.OnHero) draw.AddRect(_market.Bounds.Inflate(5f), GuiColors.Rgba(214, 172, 92, 18), 3f, 1.5f, Ui.Gold.MultiplyAlpha(0.8f));
            if (_stashing && _hand is Spot lifted && lifted.OnHero) draw.AddRect(_stash.Bounds.Inflate(5f), GuiColors.Rgba(214, 188, 146, 14), 3f, 1.5f, GuiColors.Rgba(214, 188, 146, 160));
            for (int t = 0; t < _tabs.Count && _stashing; t++)
            {
                if (t == _tab) draw.AddRect(_tabs[t].Bounds.Inflate(1f), GuiColors.Transparent, 2f, 1.5f, Ui.Gold);
            }

            if (_lookingAt is StatLine line) GameUi.Current.Tips.Show(line.Card, line.Bounds);
            if (_hand != null || _looking is not Spot spot) return;

            // The list opened from a place goes right where that place's card would.
            if (_picker.Visible && spot.Shelf == Shelf.Worn) return;

            Item? item = ItemAt(spot) ?? (spot == new Spot(Shelf.Worn, (int)WornSlot.OffHand) ? TwoHandedHeld() : null);
            if (item == null) return;

            string? price = spot.Shelf switch
            {
                Shelf.Stock => Cards.Costs(item),
                Shelf.Sold => Cards.BuyBack(item),
                _ => null,
            };
            Ui.DrawTooltip(Hero, item, _lookingFrom, price, compare: spot.Shelf != Shelf.Worn, loose: true);
        }
    }
}

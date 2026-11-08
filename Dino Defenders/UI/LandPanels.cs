using Dino_Defenders.Client;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Tooltips;
using Dino_Engine.GUI;
using Dino_Engine.GUI.Rendering;
using Dino_Engine.GUI.Widgets;
using Dino_Engine.Sound;
using Dino_Engine.Sound.Content;
using Dino_Engine.Util;
using Dino_Engine.World;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Dino_Defenders.UI
{
    /// <summary>
    /// The map table in the Haven: down the side, every level this hero can open, which is any they've freed and the one after, deepest first; and for the level chosen, the three lands the waygate
    /// reaches, side by side, each a different kind of world with its own story, biomes, weather and tyrant, or at every tenth level three arenas, each with a different tyrant. Under them are the
    /// orders in the pack, as many of which can be given as the table has slots for. Gold shows three other lands for a level. The lands are worked out from the hero alone, the same as the server
    /// does, so they're the same three however often the table is looked at and however many tries they take, until one is freed or gold is paid. Read LANDS.md.
    /// </summary>
    internal sealed class MapTablePanel : GuiElement
    {
        private const float OrdersCellSize = 52f;
        private const int MostOrdersShown = 14;
        private const float LevelListWidth = 168f;
        private const float LevelRowHeight = 30f;
        private const float LevelListHeight = 520f;
        private const float PanelWidth = 1240f;
        private const float PanelPadding = 24f;
        private const float TableGap = 16f;

        private static readonly Colour Chosen = GuiColors.Rgba(112, 74, 28);
        private static readonly Colour ArenaInk = GuiColors.Rgba(196, 92, 70);

        private readonly Label _climb;
        private readonly StackPanel _openRow;
        private readonly Label _openText;
        private readonly ScrollPanel _levels;
        private readonly Label _levelTitle;
        private readonly Label _rerollText;
        private readonly Button _reroll;
        private readonly StackPanel _cards;
        private readonly Label _ordersHeading;
        private readonly StackPanel _orders;
        private readonly Label _ordersNote;
        private readonly List<int> _chosenCells = new();
        private int _level = 1;
        private string _shownFor = "";
        private int _levelsFor = -1;
        private (OmenDefinition omen, float value, GuiRect rect)? _lookingAtOmen;
        private (Item item, GuiRect rect)? _lookingAtOrders;

        private static GameSession Session => GameSession.Current;
        private static HeroState Hero => Session.Hero;

        public MapTablePanel()
        {
            Anchor = Anchor.Center;
            Width = PanelWidth;
            Height = UIValue.Auto;
            Padding = new Thickness(PanelPadding);
            Ui.Dress(this);
            Visible = false;

            var body = Add(new StackPanel(Orientation.Vertical, 10f) { Width = UIValue.Fill, Height = UIValue.Auto });
            body.Add(Ui.Heading("The Map Table", 26f));
            _climb = body.Add(Ui.Paragraph("", 15f, Ui.Faded));

            _openRow = body.Add(new StackPanel(Orientation.Horizontal, 12f) { Width = UIValue.Fill, Height = 34f, Visible = false });
            _openRow.Stack!.CrossAlignment = Alignment.Center;
            _openText = _openRow.Add(Ui.Line("", 15f, Ui.Good));
            _openText.Width = UIValue.Fill;
            Button close = _openRow.Add(Ui.Action("Close the way", CloseWay));
            close.Width = 160f;

            var table = body.Add(new StackPanel(Orientation.Horizontal, TableGap) { Width = UIValue.Fill, Height = UIValue.Auto });

            var side = table.Add(new StackPanel(Orientation.Vertical, 6f) { Width = LevelListWidth, Height = UIValue.Auto });
            side.Add(Ui.Line("Levels", 17f, Ui.Gold));
            var listFrame = side.Add(new GuiElement { Width = UIValue.Fill, Height = LevelListHeight, BackgroundColor = Ui.InkLight, BorderColor = GuiColors.Rgba(84, 68, 46), BorderWidth = 1f, CornerRadius = 2f });
            _levels = listFrame.Add(Kit.Scroll());

            // A width of its own, as text that wraps is measured before a share of a row is worked out.
            var right = table.Add(new StackPanel(Orientation.Vertical, 10f) { Width = PanelWidth - PanelPadding * 2f - LevelListWidth - TableGap, Height = UIValue.Auto });
            var top = right.Add(new StackPanel(Orientation.Horizontal, 12f) { Width = UIValue.Fill, Height = 34f });
            top.Stack!.CrossAlignment = Alignment.Center;
            _levelTitle = top.Add(Ui.Line("", 20f, Ui.Parchment));
            _levelTitle.Width = UIValue.Fill;
            _rerollText = top.Add(Ui.Line("", 14f, Ui.Faded));
            _rerollText.Width = 300f;
            _reroll = top.Add(Ui.Action("Other lands", Reroll));
            _reroll.Width = 150f;

            _cards = right.Add(new StackPanel(Orientation.Horizontal, 14f) { Width = UIValue.Fill, Height = UIValue.Auto });

            _ordersHeading = right.Add(Ui.Heading("Orders", 20f));
            _ordersNote = right.Add(Ui.Paragraph("", 14f, Ui.Faded));
            _orders = right.Add(new StackPanel(Orientation.Horizontal, 8f) { Width = UIValue.Fill, Height = OrdersCellSize });
            right.Add(Ui.Line("", 13f, Ui.Faded).Bind(() => "Rest your eyes on an omen to read it all, or on orders to read them."));
        }

        /// <summary>Shows the table as it stands for this hero, at the deepest level they can open, with no orders chosen yet.</summary>
        public void Open()
        {
            _chosenCells.Clear();
            _level = Hero.LandLevel;
            _shownFor = "";
            _levelsFor = -1;
            Rebuild();
            _levels.ScrollToTop();
        }

        protected override void OnUpdate(float deltaTime)
        {
            if (GameSession.Current == null || !Visible) return;

            // Orders that have gone from where they were aren't chosen any more, as when the server has had the last word, and nor are more than there's room for.
            _chosenCells.RemoveAll(cell => Hero.Inventory[cell] is not { IsOrders: true });
            while (_chosenCells.Count > Lands.OrderSlots(Hero)) _chosenCells.RemoveAt(_chosenCells.Count - 1);
            _level = Math.Clamp(_level, 1, Hero.LandLevel);
            if (Signature() != _shownFor) Rebuild();

            var portal = Session.Client.Portal;
            _openRow.Visible = portal != null;
            if (portal is var (name, request, _)) _openText.Text = $"The way is open to {name}, level {request.Level}. Step through the waygate behind the table.";
            int cost = Lands.RerollCost(_level);
            _reroll.Enabled = Hero.Gold >= cost;
        }

        // What the panel shows depends on these and nothing else: which level, which lands, which orders there are to give, which are chosen, and what the hero can afford.
        private string Signature()
        {
            var orders = string.Join(",", OrdersCells().Select(cell => $"{cell}:{Hero.Inventory[cell]!.LookSeed()}"));
            return $"{Hero.Offers}/{Hero.LandLevel}/{_level}/{Hero.RerollsAt(_level)}/{Hero.Gold >= Lands.RerollCost(_level)}/{string.Join(",", _chosenCells)}/{orders}";
        }

        private IEnumerable<int> OrdersCells()
        {
            for (int cell = 0; cell < HeroState.InventorySize; cell++)
            {
                if (Hero.Inventory[cell] is { IsOrders: true }) yield return cell;
            }
        }

        private void Rebuild()
        {
            _shownFor = Signature();
            int deepest = Hero.LandLevel;
            int slots = Lands.OrderSlots(Hero);
            int nextArena = (deepest + Lands.BossEvery - 1) / Lands.BossEvery * Lands.BossEvery;
            _climb.Text = Hero.BestLand > 0
                ? $"You've freed lands as deep as level {Hero.BestLand}. Open any of them again, or level {deepest}, the next. Falling in a land, or leaving it unfree, costs nothing but the try. Every tenth level is a tyrant's arena, and the next is level {nextArena}."
                : $"Your first land is level {deepest}. Falling in a land, or leaving it unfree, costs nothing but the try. Every tenth level is a tyrant's arena.";

            if (_levelsFor != deepest) FillLevels(deepest);

            bool arena = Lands.IsBossLevel(_level);
            _levelTitle.Text = arena ? $"Level {_level}, a tyrant's arena" : _level == deepest ? $"Level {_level}, the next" : $"Level {_level}, freed";
            _levelTitle.Color = arena ? ArenaInk : Ui.Parchment;
            _rerollText.Text = $"Three other lands, for {Lands.RerollCost(_level)} gold";

            IReadOnlyList<Item> given = _chosenCells.Select(cell => Hero.Inventory[cell]!).ToList();
            _cards.ClearChildren();
            IReadOnlyList<LandOffer> offers = Lands.Offer(Hero, _level);
            for (int i = 0; i < offers.Count; i++) _cards.Add(Card(offers[i], i, given));

            _ordersHeading.Text = slots == 1 ? "Orders, one at a time" : $"Orders, up to {slots} at a time";
            _orders.ClearChildren();
            foreach (int cell in OrdersCells().Take(MostOrdersShown)) _orders.Add(new OrdersChip(this, cell));
            _ordersNote.Text = OrdersCells().Any()
                ? $"Choose orders to give the land you open: {(slots == 1 ? "the table has room for one" : $"the table has room for {slots}")}, and one more for every tyrant's arena you free. They're used up, and the land holds what they say."
                : "You have no orders. They're taken from the Reich: every tyrant has some on it, and the merchant sells a few.";
        }

        // Every level that can be opened, deepest first, as that's where a climb goes on from: a long climb is a long list.
        private void FillLevels(int deepest)
        {
            _levelsFor = deepest;
            _levels.Content.ClearChildren();
            var list = _levels.Content.Add(new StackPanel(Orientation.Vertical, 0f) { Width = UIValue.Fill, Height = UIValue.Auto });
            for (int level = deepest; level >= 1; level--) list.Add(new LevelRow(this, level, level == deepest));
        }

        private GuiElement Card(LandOffer offer, int index, IReadOnlyList<Item> given)
        {
            LandTerms terms = offer.Terms.Copy();
            foreach (Item orders in given) terms.Give(orders);

            var card = new StackPanel(Orientation.Vertical, 5f)
            {
                Width = UIValue.Relative(1f / Lands.Choices, -10f),
                Height = UIValue.Auto,
                Padding = new Thickness(14f),
                BackgroundColor = Ui.InkLight,
                BorderColor = offer.Arena ? ArenaInk.MultiplyAlpha(0.7f) : GuiColors.Rgba(84, 68, 46),
                BorderWidth = 1f,
                CornerRadius = 2f,
            };

            WorldPlan plan = offer.Plan;
            Herald herald = offer.Herald;
            card.Add(Ui.Paragraph(plan.Name, 20f, Ui.Parchment));
            if (offer.Arena)
            {
                card.Add(Ui.Line($"{plan.Archetype.Name}, {plan.Mood.Name.ToLowerInvariant()}", 14f, ArenaInk));
                card.Add(Ui.Paragraph(Lands.WeatherOf(plan), 13.5f, Ui.Faded));
                card.Add(Ui.Paragraph($"{herald.Beast}, {herald.Commander}'s {Chronicle.KindName(herald.BossKind)}, fights here before the people of the land.", 14.5f));
                card.Add(Ui.Paragraph(terms.Has(OrdersCatalog.TwinPens) ? "Bring down both its tyrants. Its handlers open the gates as they weaken, and let in the brood." : "Bring down its tyrant. Its handlers open the gates as it weakens, and let in the brood.", 14f));
            }
            else
            {
                card.Add(Ui.Line($"{plan.Archetype.Name}, {plan.Mood.Name.ToLowerInvariant()}", 14f, Ui.Gold));
                card.Add(Ui.Paragraph($"{Lands.SizeOf(plan)}: {string.Join(", ", plan.Biomes.Select(biome => biome.Name))}", 13.5f, Ui.Faded));
                card.Add(Ui.Paragraph(Lands.WeatherOf(plan), 13.5f, Ui.Faded));
                card.Add(Ui.Paragraph($"{herald.Beast}, {herald.Commander}'s {Chronicle.KindName(herald.BossKind)}, holds it with {herald.Faction}.", 14.5f));
                card.Add(Ui.Paragraph(terms.Has(OrdersCatalog.TwinPens) ? $"Free {terms.Places} of its places, and bring down both its tyrants." : $"Free {terms.Places} of its places, and bring down its tyrant.", 14f));
            }

            if (terms.Count > 0)
            {
                card.Add(new Spacer(height: 4f));
                card.Add(Ui.Line(terms.Count == 1 ? "One omen, from the orders" : $"{terms.Count} omens, from the orders", 15f, Ui.Gold));
                foreach ((OmenDefinition omen, float value) in terms.Chosen()) card.Add(new OmenRow(this, omen, value));
            }
            foreach (UniqueOrders unique in terms.Uniques())
            {
                card.Add(Ui.Paragraph(unique.Gives, 13f, Ui.RarityColour(Rarity.Unique)));
                card.Add(Ui.Paragraph(unique.Costs, 13f, Ui.Blood));
            }

            card.Add(new Spacer(height: 4f));
            card.Add(Ui.Paragraph(terms.Pays() is { Length: > 0 } pays ? $"Pays {pays}." : "Pays nothing past what the land gives.", 14f, Ui.Good));
            card.Add(new Spacer(height: 4f));
            Button open = card.Add(Ui.Action("Open the way", () => OpenWay(index)));
            int level = _level;
            open.Label.Bind(() => Session?.Client.Portal is var (name, request, _) && name == plan.Name && request.Level == level ? "The way is open" : _chosenCells.Count > 0 ? "Open the way, giving the orders" : "Open the way");
            return card;
        }

        private void OpenWay(int index)
        {
            Audio.Play(Interface.Trade);
            Session.Client.OpenWay(_level, index, _chosenCells.ToList());
            _chosenCells.Clear();
            GameUi.Current.CloseAll();
        }

        private void Reroll()
        {
            if (Hero.Gold < Lands.RerollCost(_level)) return;
            Audio.Play(Interface.Trade);
            Session.Client.Reroll(_level);
        }

        private static void CloseWay()
        {
            Audio.Play(Interface.Close);
            Session.Client.CloseWay();
        }

        /// <summary>Shows the lands of another level.</summary>
        public void ChooseLevel(int level)
        {
            if (level == _level) return;
            Audio.Play(Interface.Click);
            _level = level;
        }

        public bool IsChosenLevel(int level) => _level == level;

        /// <summary>Chooses the orders in a cell of the pack to give, or not, as long as there's room for them.</summary>
        public void Choose(int cell)
        {
            Audio.Play(Interface.Click);
            if (_chosenCells.Remove(cell)) return;
            if (_chosenCells.Count >= Lands.OrderSlots(Hero))
            {
                if (Lands.OrderSlots(Hero) > 1) return;
                _chosenCells.Clear();
            }
            _chosenCells.Add(cell);
        }

        public bool IsChosen(int cell) => _chosenCells.Contains(cell);

        public void LookAt(OmenDefinition? omen, float value, GuiRect rect) => _lookingAtOmen = omen != null ? (omen, value, rect) : null;

        public void LookAt(Item? item, GuiRect rect) => _lookingAtOrders = item != null ? (item, rect) : null;

        protected override void OnDrawOverlay(GuiDrawList draw)
        {
            base.OnDrawOverlay(draw);
            if (_lookingAtOmen is var (omen, value, rect)) GameUi.Current.Tips.Show(() => Cards.Omen(omen, value, GameSession.Current?.Sheet), rect);
            if (_lookingAtOrders is var (item, from)) Ui.DrawTooltip(Hero, item, from, null, compare: false);
        }

        /// <summary>One level in the list down the side: its number, and whether it's the next or an arena.</summary>
        private sealed class LevelRow : GuiElement
        {
            private readonly MapTablePanel _panel;
            private readonly int _level;
            private readonly bool _next;

            public LevelRow(MapTablePanel panel, int level, bool next)
            {
                _panel = panel;
                _level = level;
                _next = next;
                Width = UIValue.Fill;
                Height = LevelRowHeight;
                HitTest = HitTestMode.Block;
                MouseDown += e =>
                {
                    if (e.Button != MouseButton.Left) return;
                    e.Handled = true;
                    _panel.ChooseLevel(_level);
                };
            }

            protected override void OnDraw(GuiDrawList draw)
            {
                base.OnDraw(draw);
                if (Context == null) return;
                bool chosen = _panel.IsChosenLevel(_level);
                if (chosen) draw.AddRect(Bounds, Chosen.MultiplyAlpha(0.6f), 0f);
                else if (IsHovered) draw.AddRect(Bounds, GuiColors.Rgba(255, 240, 200, 18), 0f);

                bool arena = Lands.IsBossLevel(_level);
                var text = new GuiRect(Bounds.X + 12f, Bounds.Y, Bounds.Width - 24f, Bounds.Height);
                Ui.Text(Context, draw, $"Level {_level}", 15f, text, chosen ? Ui.Bright : arena ? ArenaInk : Ui.Parchment);
                string? note = _next ? "next" : arena ? "arena" : null;
                if (note != null) Ui.Text(Context, draw, note, 12.5f, text, _next ? Ui.Good : ArenaInk, Alignment.End);
            }
        }

        /// <summary>One omen on a land's card: its name, and what it does in a line or two, in the colour of a reward or a danger.</summary>
        private sealed class OmenRow : StackPanel
        {
            public OmenRow(MapTablePanel panel, OmenDefinition omen, float value) : base(Orientation.Vertical, 1f)
            {
                Width = UIValue.Fill;
                Height = UIValue.Auto;
                HitTest = HitTestMode.Block;
                Add(Ui.Line(omen.Name, 14f, omen.IsPrefix ? Ui.Good : Ui.Parchment));
                Add(Ui.Paragraph(omen.Describe(value), 12.5f, Ui.Faded));
                MouseEnter += () => panel.LookAt(omen, value, Bounds);
                MouseLeave += () => panel.LookAt(null, 0f, Bounds);
            }

            protected override void OnDraw(GuiDrawList draw)
            {
                if (IsHovered) draw.AddRect(Bounds, GuiColors.Rgba(255, 240, 200, 18), 2f);
                base.OnDraw(draw);
            }
        }

        /// <summary>Orders in the pack that could be given: a picture of them, chosen or not.</summary>
        private sealed class OrdersChip : GuiElement
        {
            private readonly MapTablePanel _panel;
            private readonly int _cell;

            public OrdersChip(MapTablePanel panel, int cell)
            {
                _panel = panel;
                _cell = cell;
                Width = OrdersCellSize;
                Height = OrdersCellSize;
                BackgroundColor = Ui.InkLight;
                BorderColor = GuiColors.Rgba(84, 68, 46);
                BorderWidth = 1f;
                CornerRadius = 2f;
                HitTest = HitTestMode.Block;
                MouseDown += e =>
                {
                    if (e.Button != MouseButton.Left) return;
                    e.Handled = true;
                    _panel.Choose(_cell);
                };
                MouseEnter += () => _panel.LookAt(Item, Bounds);
                MouseLeave += () => _panel.LookAt(null, Bounds);
            }

            private Item? Item => GameSession.Current != null ? GameSession.Current.Hero.Inventory[_cell] : null;

            protected override void OnDraw(GuiDrawList draw)
            {
                base.OnDraw(draw);
                if (Context == null || Item is not Item item) return;
                bool chosen = _panel.IsChosen(_cell);
                if (chosen) draw.AddRect(Bounds, Chosen.MultiplyAlpha(0.6f), 2f);
                if (IsHovered) draw.AddRect(Bounds, GuiColors.Rgba(255, 240, 200, 26), 2f);
                Colour colour = Ui.RarityColour(item.Rarity);
                Ui.DrawItem(draw, item, Bounds, colour, GuiColors.White);
                draw.AddRect(Bounds, GuiColors.Transparent, 2f, chosen ? 2f : 1.5f, chosen ? Ui.Gold : colour.MultiplyAlpha(0.8f));
            }
        }
    }

    /// <summary>Asked before leaving a land that isn't free, as going home now loses it, though it costs nothing but the try.</summary>
    internal sealed class LeavePrompt : GuiElement
    {
        private readonly Label _says;

        public LeavePrompt(Action leave, Action stay)
        {
            Anchor = Anchor.Center;
            Width = 460f;
            Height = UIValue.Auto;
            Padding = new Thickness(22f);
            Ui.Dress(this);
            Visible = false;

            var body = Add(new StackPanel(Orientation.Vertical, 12f) { Width = UIValue.Fill, Height = UIValue.Auto });
            body.Add(Ui.Heading("Leave this land?", 22f));
            _says = body.Add(Ui.Paragraph("", 15f));
            var buttons = body.Add(new StackPanel(Orientation.Horizontal, 10f) { Width = UIValue.Fill, Height = 34f });
            Button go = buttons.Add(Ui.Action("Leave it", leave));
            go.Width = UIValue.Relative(0.5f, -5f);
            Button wait = buttons.Add(Ui.Action("Stay", stay));
            wait.Width = UIValue.Relative(0.5f, -5f);
        }

        public void Ask()
        {
            GameSession session = GameSession.Current;
            _says.Text = $"{session.World.Plan.Name} isn't free. Go home now and it's lost, along with the orders given for it, though the map table will show it again to try once more.";
            Visible = true;
        }
    }
}

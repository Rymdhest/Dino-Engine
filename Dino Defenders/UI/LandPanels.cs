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
    /// The map table in the Haven: the three lands the waygate reaches for this hero, side by side, each a different kind of world with its own story, biomes and omens, and the orders in their pack
    /// that could be given for one of them. Choosing one opens the way, and the waygate behind the table is what's stepped through. The three are worked out from the hero alone, the same as the server
    /// does, so they're the same three however often the table is looked at, until a land has been gone to. Read LANDS.md.
    /// </summary>
    internal sealed class MapTablePanel : GuiElement
    {
        private const float OrdersCellSize = 52f;
        private const int MostOrdersShown = 14;

        private static readonly Colour Chosen = GuiColors.Rgba(112, 74, 28);

        private readonly Label _climb;
        private readonly StackPanel _openRow;
        private readonly Label _openText;
        private readonly StackPanel _cards;
        private readonly StackPanel _orders;
        private readonly Label _ordersNote;
        private int _chosenCell = -1;
        private string _shownFor = "";
        private (OmenDefinition omen, float value, GuiRect rect)? _lookingAtOmen;
        private (Item item, GuiRect rect)? _lookingAtOrders;

        private static GameSession Session => GameSession.Current;
        private static HeroState Hero => Session.Hero;

        public MapTablePanel()
        {
            Anchor = Anchor.Center;
            Width = 1040f;
            Height = UIValue.Auto;
            Padding = new Thickness(24f);
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

            _cards = body.Add(new StackPanel(Orientation.Horizontal, 14f) { Width = UIValue.Fill, Height = UIValue.Auto });

            body.Add(Ui.Heading("Orders", 20f));
            _ordersNote = body.Add(Ui.Paragraph("", 14f, Ui.Faded));
            _orders = body.Add(new StackPanel(Orientation.Horizontal, 8f) { Width = UIValue.Fill, Height = OrdersCellSize });
            body.Add(Ui.Line("", 13f, Ui.Faded).Bind(() => "Rest your eyes on an omen to read it all, or on orders to read them."));
        }

        /// <summary>Shows the table as it stands for this hero, with no orders chosen yet.</summary>
        public void Open()
        {
            _chosenCell = -1;
            _shownFor = "";
            Rebuild();
        }

        protected override void OnUpdate(float deltaTime)
        {
            if (GameSession.Current == null || !Visible) return;

            // Orders that have gone from where they were aren't chosen any more, as when the server has had the last word.
            if (_chosenCell >= 0 && Hero.Inventory[_chosenCell] is not { IsOrders: true }) _chosenCell = -1;
            if (Signature() != _shownFor) Rebuild();

            var portal = Session.Client.Portal;
            _openRow.Visible = portal != null;
            if (portal is var (name, request, _)) _openText.Text = $"The way is open to {name}, level {request.Level}. Step through the waygate behind the table.";
        }

        // What the panel shows depends on these and nothing else: which lands, which orders there are to give, and which are chosen.
        private string Signature()
        {
            var orders = string.Join(",", OrdersCells().Select(cell => $"{cell}:{Hero.Inventory[cell]!.LookSeed()}"));
            return $"{Hero.Offers}/{Hero.LandLevel}/{_chosenCell}/{orders}";
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
            int level = Hero.LandLevel;
            _climb.Text = $"Your next land is level {level}. Free it and you climb to {level + 1}. Fall in it, or leave it unfree, and you drop to {Math.Max(level - 1, 1)}.";

            Item? orders = _chosenCell >= 0 ? Hero.Inventory[_chosenCell] : null;
            _cards.ClearChildren();
            IReadOnlyList<LandOffer> offers = Lands.Offer(Hero);
            for (int i = 0; i < offers.Count; i++) _cards.Add(Card(offers[i], i, orders));

            _orders.ClearChildren();
            _orders.Add(new OrdersChip(this, -1));
            foreach (int cell in OrdersCells().Take(MostOrdersShown)) _orders.Add(new OrdersChip(this, cell));
            _ordersNote.Text = OrdersCells().Any()
                ? "Choose orders to give the land you open. They're used up, and the land holds what they say, on top of its own omens."
                : "You have no orders. They're taken from the Reich: every tyrant has some on it, and the merchant sells a few.";
        }

        private GuiElement Card(LandOffer offer, int index, Item? orders)
        {
            LandTerms terms = offer.Terms.Copy();
            if (orders != null) terms.Give(orders);
            var fromOrders = new HashSet<string>(orders != null ? OrdersMaker.OmensOf(orders).Select(omen => omen.Omen.Id) : Enumerable.Empty<string>());

            var card = new StackPanel(Orientation.Vertical, 5f)
            {
                Width = UIValue.Relative(1f / Lands.Choices, -10f),
                Height = UIValue.Auto,
                Padding = new Thickness(14f),
                BackgroundColor = Ui.InkLight,
                BorderColor = GuiColors.Rgba(84, 68, 46),
                BorderWidth = 1f,
                CornerRadius = 2f,
            };

            WorldPlan plan = offer.Plan;
            card.Add(Ui.Paragraph(plan.Name, 20f, Ui.Parchment));
            card.Add(Ui.Line($"{plan.Archetype.Name}, {plan.Mood.Name.ToLowerInvariant()}", 14f, Ui.Gold));
            card.Add(Ui.Paragraph(string.Join(", ", plan.Biomes.Select(biome => biome.Name)), 13.5f, Ui.Faded));
            Herald herald = offer.Herald;
            card.Add(Ui.Paragraph($"{herald.Beast}, {herald.Commander}'s {Chronicle.KindName(herald.BossKind)}, holds it with {herald.Faction}.", 14.5f));
            card.Add(Ui.Paragraph(terms.Unique() == OrdersCatalog.TwinPens ? $"Free {terms.Places} of its places, and bring down both its tyrants." : $"Free {terms.Places} of its places, and bring down its tyrant.", 14f));

            card.Add(new Spacer(height: 4f));
            card.Add(Ui.Line(terms.Count == 1 ? "One omen" : $"{terms.Count} omens", 15f, Ui.Gold));
            foreach ((OmenDefinition omen, float value) in terms.Chosen()) card.Add(new OmenRow(this, omen, value, fromOrders.Contains(omen.Id)));
            if (terms.Unique() is UniqueOrders unique)
            {
                card.Add(Ui.Paragraph(unique.Gives, 13f, Ui.RarityColour(Rarity.Unique)));
                card.Add(Ui.Paragraph(unique.Costs, 13f, Ui.Blood));
            }

            card.Add(new Spacer(height: 4f));
            card.Add(Ui.Paragraph(terms.Pays() is { Length: > 0 } pays ? $"Pays {pays}." : "Pays nothing past what the land gives.", 14f, Ui.Good));
            card.Add(new Spacer(height: 4f));
            Button open = card.Add(Ui.Action("Open the way", () => OpenWay(index)));
            open.Label.Bind(() => Session?.Client.Portal is var (name, _, _) && name == plan.Name ? "The way is open" : orders != null ? "Open the way, giving the orders" : "Open the way");
            return card;
        }

        private void OpenWay(int index)
        {
            Audio.Play(Interface.Trade);
            Session.Client.OpenWay(index, _chosenCell);
            _chosenCell = -1;
            GameUi.Current.CloseAll();
        }

        private static void CloseWay()
        {
            Audio.Play(Interface.Close);
            Session.Client.CloseWay();
        }

        /// <summary>Chooses the orders in a cell of the pack to give, or none.</summary>
        public void Choose(int cell)
        {
            Audio.Play(Interface.Click);
            _chosenCell = _chosenCell == cell ? -1 : cell;
        }

        public bool IsChosen(int cell) => _chosenCell == cell;

        public void LookAt(OmenDefinition? omen, float value, GuiRect rect) => _lookingAtOmen = omen != null ? (omen, value, rect) : null;

        public void LookAt(Item? item, GuiRect rect) => _lookingAtOrders = item != null ? (item, rect) : null;

        protected override void OnDrawOverlay(GuiDrawList draw)
        {
            base.OnDrawOverlay(draw);
            if (_lookingAtOmen is var (omen, value, rect)) GameUi.Current.Tips.Show(() => Cards.Omen(omen, value, GameSession.Current?.Sheet), rect);
            if (_lookingAtOrders is var (item, from)) Ui.DrawTooltip(Hero, item, from, null, compare: false);
        }

        /// <summary>One omen on a land's card: its name, and what it does in a line or two. Those its orders brought are in their colour.</summary>
        private sealed class OmenRow : StackPanel
        {
            public OmenRow(MapTablePanel panel, OmenDefinition omen, float value, bool fromOrders) : base(Orientation.Vertical, 1f)
            {
                Width = UIValue.Fill;
                Height = UIValue.Auto;
                HitTest = HitTestMode.Block;
                Add(Ui.Line(fromOrders ? $"{omen.Name}, from the orders" : omen.Name, 14f, fromOrders ? Ui.Gold : Ui.Parchment));
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

        /// <summary>Orders in the pack that could be given, or none: a picture of them, chosen or not.</summary>
        private sealed class OrdersChip : GuiElement
        {
            private readonly MapTablePanel _panel;
            private readonly int _cell;

            public OrdersChip(MapTablePanel panel, int cell)
            {
                _panel = panel;
                _cell = cell;
                Width = cell < 0 ? 96f : OrdersCellSize;
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

            private Item? Item => _cell >= 0 && GameSession.Current != null ? GameSession.Current.Hero.Inventory[_cell] : null;

            protected override void OnDraw(GuiDrawList draw)
            {
                base.OnDraw(draw);
                if (Context == null) return;
                bool chosen = _panel.IsChosen(_cell);
                if (chosen) draw.AddRect(Bounds, Chosen.MultiplyAlpha(0.6f), 2f);
                if (IsHovered) draw.AddRect(Bounds, GuiColors.Rgba(255, 240, 200, 26), 2f);

                if (Item is not Item item)
                {
                    Ui.Text(Context, draw, "No orders", 13.5f, Bounds, chosen ? Ui.Gold : Ui.Faded, Alignment.Center);
                    if (chosen) draw.AddRect(Bounds, GuiColors.Transparent, 2f, 1.5f, Ui.Gold);
                    return;
                }
                Colour colour = Ui.RarityColour(item.Rarity);
                Ui.DrawItem(draw, item, Bounds, colour, GuiColors.White);
                draw.AddRect(Bounds, GuiColors.Transparent, 2f, chosen ? 2f : 1.5f, chosen ? Ui.Gold : colour.MultiplyAlpha(0.8f));
            }
        }
    }

    /// <summary>Asked before leaving a land that isn't free, as going home now loses it and drops the climb.</summary>
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
            int climb = session.Terms.Climb;
            int next = Math.Max(session.Hero.LandLevel - climb, 1);
            _says.Text = $"{session.World.Plan.Name} isn't free. Go home now and it's lost, and your next land will be level {next}.";
            Visible = true;
        }
    }
}

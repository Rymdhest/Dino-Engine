using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Skills;
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
    /// <summary>One place for an item: a place on the hero, a cell in the pack, or something on a merchant's table. What's in it is a picture of the thing itself.</summary>
    internal sealed class ItemCell : GuiElement
    {
        public const float Size = 56f;

        private readonly Func<Item?> _item;
        private readonly ItemSlot? _slot;
        private readonly Action<Item?, GuiRect> _hover;

        /// <summary>What's keeping this place from being used, and shows in it, faintly: something two-handed, in the off hand.</summary>
        public Func<Item?>? TakenBy;

        /// <summary>Whether what's being looked at could go here.</summary>
        public Func<bool>? Wanted;

        public ItemCell(Func<Item?> item, Action<Item, GuiMouseEventArgs> pressed, Action<Item?, GuiRect> hover, ItemSlot? slot = null)
        {
            _item = item;
            _hover = hover;
            _slot = slot;
            Width = Size;
            Height = Size;
            BackgroundColor = Ui.InkLight;
            BorderColor = GuiColors.Rgba(84, 68, 46);
            BorderWidth = 1f;
            CornerRadius = 2f;
            Clicked += e =>
            {
                Item? held = _item();
                if (held != null) pressed(held, e);
            };
            MouseEnter += () => _hover(_item() ?? TakenBy?.Invoke(), Bounds);
            MouseLeave += () => _hover(null, Bounds);
        }

        protected override void OnDraw(GuiDrawList draw)
        {
            base.OnDraw(draw);
            Item? item = _item();
            bool wanted = Wanted?.Invoke() == true;
            if (wanted) draw.AddRect(Bounds, GuiColors.Rgba(214, 172, 92, 44), 2f, 1.5f, Ui.Gold);

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

            Colour colour = Ui.GradeColour(Loot.GradeOf(item));
            draw.AddRect(Bounds, colour.MultiplyAlpha(0.1f), 2f);
            if (IsHovered) draw.AddRect(Bounds, GuiColors.Rgba(255, 240, 200, 26), 2f);
            Ui.DrawItem(draw, item, Bounds, colour, GuiColors.White);
            draw.AddRect(Bounds, GuiColors.Transparent, 2f, 1.5f, colour.MultiplyAlpha(0.8f));
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
    /// the side. At a merchant there's a table too. Whatever the mouse is over is described beside it, set against what's worn in its place.
    /// </summary>
    internal sealed class HeroPanel : GuiElement
    {
        private const int Columns = 6;
        private const int StockColumns = 6;
        private const int StockSize = 12;
        private const int FlaskPrice = 12;  // Shown here; the server is what charges it.
        private const float CellGap = 6f;
        private const float StageWidth = 450f;

        private readonly GuiElement _market;
        private readonly Label _title;
        private SiteState? _merchant;
        private Item? _looking;
        private GuiRect _lookingFrom;
        private StatLine? _lookingAt;

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
            left.Add(Ui.Line("", 14f, Ui.Gold).Bind(() => Hero.UnspentPoints > 0 ? $"{Hero.UnspentPoints} talents to spend  [T]" : " "));
            left.Add(new Spacer(height: 6f));
            var sheet = left.Add(new ScrollPanel { Width = UIValue.Fill, Height = UIValue.Fill });
            AddStats(sheet.Content.Add(new StackPanel(Orientation.Vertical, 1f) { Width = UIValue.Fill, Height = UIValue.Auto }));

            columns.Add(BuildStage());

            var right = columns.Add(new StackPanel(Orientation.Vertical, 8f) { Width = UIValue.Fill, Height = UIValue.Fill });
            right.Add(Ui.Line("Carried", 15f, Ui.Gold));
            var bag = right.Add(new GridPanel(Columns, CellGap, CellGap));
            for (int i = 0; i < HeroState.InventorySize; i++)
            {
                int index = i;
                bag.Add(new ItemCell(() => index < Hero.Inventory.Count ? Hero.Inventory[index] : null, UseCarried, Look));
            }
            right.Add(Ui.Paragraph("Click what you carry to wear it, and what you wear to take it off. Shift puts a ring on your other hand.", 13f, Ui.Faded));

            _market = right.Add(new StackPanel(Orientation.Vertical, 6f) { Width = UIValue.Fill, Height = UIValue.Auto });
            _market.Add(Ui.Line("For sale", 15f, Ui.Gold));
            var table = _market.Add(new GridPanel(StockColumns, CellGap, CellGap));
            for (int i = 0; i < StockSize; i++)
            {
                int index = i;
                table.Add(new ItemCell(() => StockAt(index), (item, _) => Buy(item), Look));
            }
            _market.Add(Ui.Action($"Fill flasks ({FlaskPrice} gold each)", BuyFlasks));
        }

        // Head to toe down one side, what's on them down the other, and both hands below.
        private GuiElement BuildStage()
        {
            var stage = new HeroStage { Width = StageWidth, Height = UIValue.Fill };
            float step = ItemCell.Size + 22f;
            float right = StageWidth - ItemCell.Size;

            void Place(WornSlot slot, float x, float y)
            {
                WornSlot worn = slot;
                var cell = new ItemCell(() => Hero.Equipped.GetValueOrDefault(worn), (_, _) => TakeOff(worn), Look, WornSlots.Holds(worn))
                {
                    X = x,
                    Y = y,
                    Wanted = () => _looking is Item item && Hero.Inventory.Contains(item) && WornSlots.For(item.Slot).Contains(worn),
                };
                if (worn == WornSlot.OffHand) cell.TakenBy = () => Hero.Equipped.TryGetValue(WornSlot.MainHand, out Item? held) && held.IsTwoHanded ? held : null;
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

        private Item? StockAt(int index)
        {
            return _merchant != null && index < _merchant.Stock.Count ? _merchant.Stock[index] : null;
        }

        // Every line can be looked at, and only what touches this hero shows, so that what's on the sheet is what's in the build.
        private void AddStats(StackPanel parent)
        {
            GuiElement Line(Func<string> name, Func<string> value, Func<Card?> card) => parent.Add(new StatLine(name, value, card, line => _lookingAt = line));

            parent.Add(Ui.Line("Your main skill", 14f, Ui.Gold));
            static SkillProfile? Main() => Session.Client.Kit.Slots[0];
            Line(() => Main()?.Skill.Name ?? "Nothing", () => Main() is SkillProfile main && main.DealsDamage ? Say.N(main.DamagePerSecond) : "", () => Main() is SkillProfile main ? Cards.Skill(main) : null);

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
            _market.Visible = merchant != null;
            _title.Text = merchant != null ? merchant.Site.Name : "The Hero";
            _looking = null;
            _lookingAt = null;
        }

        private void Look(Item? item, GuiRect from)
        {
            _looking = item;
            _lookingFrom = from;
        }

        private void TakeOff(WornSlot place)
        {
            Session.Client.Unequip(place);
            Audio.Play(Interface.Equip);
            _looking = null;
        }

        // All of these ask, and the server answers: what the hero has changes when it says so, a moment later.
        private void UseCarried(Item item, GuiMouseEventArgs e)
        {
            int index = Hero.Inventory.IndexOf(item);
            if (index < 0) return;
            Audio.Play(_merchant != null ? Interface.Trade : Interface.Equip);
            if (_merchant != null) Session.Client.Sell(index);
            else Session.Client.Equip(index, item.Slot == ItemSlot.Finger && (e.Modifiers & OpenTK.Windowing.GraphicsLibraryFramework.KeyModifiers.Shift) != 0 ? WornSlot.Finger2 : null);
            _looking = null;
        }

        private void Buy(Item item)
        {
            if (_merchant == null || Hero.Gold < item.BuyValue) return;
            Audio.Play(Interface.Trade);
            Session.Client.Buy(Session.Sites.IndexOf(_merchant), _merchant.Stock.IndexOf(item));
            _looking = null;
        }

        private static void BuyFlasks()
        {
            Audio.Play(Interface.Trade);
            Session.Client.BuyFlasks();
        }

        protected override void OnDrawOverlay(GuiDrawList draw)
        {
            base.OnDrawOverlay(draw);
            if (Context == null) return;

            if (_lookingAt is StatLine line) GameUi.Current.Tips.Show(line.Card, line.Bounds);
            if (_looking is not Item item) return;

            string? price = null;
            bool carried = Hero.Inventory.Contains(item);
            if (_merchant != null) price = carried ? $"Sells for {item.SellValue} gold" : $"Costs {item.BuyValue} gold";

            Ui.DrawTooltip(Hero, item, _lookingFrom, price, compare: carried || _merchant != null);
        }
    }
}

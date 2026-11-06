using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Tooltips;
using Dino_Engine.GUI;
using Dino_Engine.GUI.Rendering;
using Dino_Engine.GUI.Widgets;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Dino_Defenders.UI
{
    /// <summary>One thing that could go in a place, with what it is and what it would change at a glance, and a click puts it on.</summary>
    internal sealed class PickerRow : GuiElement
    {
        public const float RowHeight = 50f;
        private const float Picture = 44f;

        private readonly Item _item;
        private readonly string _kind;
        private readonly (string text, bool better)? _change;

        public PickerRow(HeroPanel panel, Item item, int cell, Item? worn, Action chosen)
        {
            _item = item;
            _kind = $"{Cards.KindOf(item)}, Level {item.Level}";
            if (worn != null && Ui.Differences(item, worn).FirstOrDefault() is { name: not null } change) _change = ($"{Say.Signed(change.difference)} {change.name}", change.difference > 0f);

            Width = UIValue.Fill;
            Height = RowHeight;
            HitTest = HitTestMode.Block;
            Clicked += e =>
            {
                if (e.Button == MouseButton.Left) chosen();
            };
            var spot = new Spot(Shelf.Pack, cell);
            MouseEnter += () => panel.Look(spot, Bounds);
            MouseLeave += () => panel.Look(null, Bounds);
        }

        protected override void OnDraw(GuiDrawList draw)
        {
            base.OnDraw(draw);
            if (Context == null) return;

            Colour colour = Ui.GradeColour(Loot.GradeOf(_item));
            if (IsHovered) draw.AddRect(Bounds, GuiColors.Rgba(255, 240, 200, 26), 2f);

            GuiRect picture = new GuiRect(Bounds.X + 3f, Bounds.Y + 3f, Picture, Picture);
            draw.AddRect(picture, Ui.InkLight, 2f, 1f, colour.MultiplyAlpha(0.8f));
            Ui.DrawItem(draw, _item, picture, colour, GuiColors.White);

            float x = picture.Right + 8f;
            GuiRect text = new GuiRect(x, Bounds.Y + 5f, Bounds.Right - x - 4f, 20f);
            draw.PushClip(text);
            Ui.Text(Context, draw, _item.Name, 14.5f, text, colour);
            draw.PopClip();

            GuiRect below = new GuiRect(x, Bounds.Y + 25f, Bounds.Right - x - 4f, 18f);
            Ui.Text(Context, draw, _change is { } change ? change.text : _kind, 12.5f, below, _change is { better: true } ? Ui.Good : _change != null ? Ui.Blood : Ui.Faded);
        }
    }

    /// <summary>What's in the pack that could go in a place on the hero, best first, opened by clicking that place. What it shows lights up in the pack as well, and the rest of the pack goes dark.</summary>
    internal sealed class SlotPicker : GuiElement
    {
        private const float PickerWidth = 320f;
        private const float MostHeight = 430f;
        private const float RowGap = 2f;
        private const float Gap = 8f;
        private const float Pad = 10f;

        private readonly HeroPanel _panel;
        private readonly Label _title;
        private readonly Button _takeOff;
        private readonly ScrollPanel _scroll;
        private readonly Label _empty;

        public WornSlot Slot { get; private set; }

        private static HeroState Hero => GameSession.Current.Hero;

        public SlotPicker(HeroPanel panel)
        {
            _panel = panel;
            Width = PickerWidth;
            Height = UIValue.Auto;
            Padding = new Thickness(Pad);
            Ui.Dress(this);
            Visible = false;

            var body = Add(new StackPanel(Orientation.Vertical, 6f) { Width = UIValue.Fill, Height = UIValue.Auto });
            _title = body.Add(Ui.Line("", 15f, Ui.Gold));
            _takeOff = body.Add(Ui.Action("Take it off", TakeOff));
            _takeOff.Height = 26f;
            _empty = body.Add(Ui.Paragraph("Nothing you carry goes here.", 14f, Ui.Faded));
            _scroll = body.Add(new ScrollPanel { Width = UIValue.Fill });
            _scroll.Content.Layout = new StackLayout(Orientation.Vertical, RowGap);
        }

        /// <param name="from">The place on the hero it's about.</param>
        /// <param name="within">What it has to stay inside of.</param>
        public void Open(WornSlot slot, GuiRect from, GuiRect within)
        {
            Slot = slot;
            bool worn = Hero.Equipped.TryGetValue(slot, out Item? wearing);

            var fits = new List<(Item item, int cell)>();
            for (int cell = 0; cell < HeroState.InventorySize; cell++)
            {
                if (Hero.Inventory[cell] is Item item && WornSlots.For(item.Slot).Contains(slot)) fits.Add((item, cell));
            }

            _scroll.Content.ClearChildren();
            foreach ((Item item, int cell) in fits.OrderByDescending(fit => Loot.GradeOf(fit.item)).ThenByDescending(fit => fit.item.Level))
            {
                _scroll.Content.Add(new PickerRow(_panel, item, cell, wearing, () => Choose(cell)));
            }
            _scroll.ScrollToTop();

            float rows = fits.Count * (PickerRow.RowHeight + RowGap);
            float top = worn ? 26f + 6f : 0f;
            float heading = 22f + 6f;
            float list = MathF.Min(rows, MostHeight - heading - top);
            _scroll.Height = list;
            _scroll.Visible = fits.Count > 0;
            _empty.Visible = fits.Count == 0;
            _takeOff.Visible = worn;
            _title.Text = fits.Count == 0 ? SlotName(slot) : $"{SlotName(slot)}: {fits.Count} to choose from";

            // Beside the place, towards the hero, and all of it inside the panel.
            float height = heading + top + (fits.Count > 0 ? list : 24f) + Pad * 2f;
            X = from.Center.X < within.Center.X ? from.Right + Gap - within.X : from.X - Gap - PickerWidth - within.X;
            Y = Math.Clamp(from.Y - within.Y, 0f, MathF.Max(within.Height - height, 0f));
            Visible = true;
        }

        private static string SlotName(WornSlot slot) => slot switch
        {
            WornSlot.MainHand => "Main hand",
            WornSlot.OffHand => "Off hand",
            WornSlot.Finger1 or WornSlot.Finger2 => "Ring",
            WornSlot.Neck => "Amulet",
            _ => slot.ToString(),
        };

        private void Choose(int cell)
        {
            Visible = false;
            _panel.Look(null, default);
            _panel.Wear(cell, Slot);
        }

        private void TakeOff()
        {
            Visible = false;
            _panel.TakeOff(Slot);
        }
    }
}

using Dino_Engine.GUI;
using Dino_Engine.GUI.Rendering;
using Dino_Engine.GUI.Widgets;
using Dino_Engine.Util;
using Dino_Defenders.Progression;
using OpenTK.Mathematics;

namespace Dino_Defenders.UI
{
    /// <summary>Everywhere that isn't the hero's panel, while they have something on the cursor: to bring it here is to put it on the ground. Only then does this stop the mouse.</summary>
    internal sealed class GroundCatcher : GuiElement
    {
        public GroundCatcher()
        {
            Width = UIValue.Fill;
            Height = UIValue.Fill;
            MouseDown += e => GameUi.Current.Hero.PressOn(this, e);
        }

        // It has to really be over the game: with the window gone the mouse is nowhere, and losing the focus halfway through a drag mustn't put anything on the ground.
        public override bool HitTestSelf(Vector2 point) => Bounds.Contains(point) && GameUi.Current.Hero.Active && GameUi.Current.Hero.Holding != null;
    }

    /// <summary>What's on the cursor, over everything else, and what bringing it down here would do.</summary>
    internal sealed class HandLayer : GuiElement
    {
        private const float CaptionSize = 14f;
        private const float CaptionHeight = 21f;

        public HandLayer()
        {
            Width = UIValue.Fill;
            Height = UIValue.Fill;
            HitTest = HitTestMode.None;
        }

        protected override void OnDraw(GuiDrawList draw)
        {
            HeroPanel panel = GameUi.Current.Hero;
            if (Context == null || !panel.Active || panel.Holding is not Item item) return;

            Vector2 mouse = Context.MousePosition;
            float size = ItemCell.Size;
            GuiRect rect = new GuiRect(mouse.X - size * 0.5f, mouse.Y - size * 0.5f, size, size);
            Colour colour = Ui.RarityColour(item.Rarity);
            draw.AddRect(rect, Ui.InkLight, 2f, 1.5f, colour.MultiplyAlpha(0.9f));
            Ui.DrawItem(draw, item, rect, colour, GuiColors.White);

            (string says, bool refused) = panel.Intent();
            if (says.Length == 0) return;

            float width = GuiText.MeasureWidth(Context, says, Ui.Face, CaptionSize) + 16f;
            GuiRect caption = new GuiRect(mouse.X - width * 0.5f, rect.Bottom + 4f, width, CaptionHeight);
            draw.AddRect(caption, Ui.Paper, 3f, 1f, (refused ? Ui.Blood : Ui.Gold).MultiplyAlpha(0.7f));
            Ui.Text(Context, draw, says, CaptionSize, caption, refused ? Ui.Parchment : Ui.Gold, Alignment.Center);
        }
    }
}

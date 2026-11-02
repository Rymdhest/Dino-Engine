using Dino_Engine.GUI.Rendering;
using Dino_Engine.GUI.Text;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.GUI.Widgets
{
    /// <summary>Single line text helpers for widgets that draw text directly instead of using a <see cref="Label"/>.</summary>
    public static class GuiText
    {
        public static float MeasureWidth(GuiContext context, string text, FontFace face, float size)
        {
            Font font = context.GetFont(face, size);
            return TextLayout.MeasureWidth(font, text) / context.PixelScale;
        }

        public static float LineHeight(GuiContext context, FontFace face, float size)
        {
            return context.GetFont(face, size).LineHeight / context.PixelScale;
        }

        /// <summary>Draws one line of text aligned inside a rect. Text is not clipped; push a clip first if needed.</summary>
        public static void Draw(GuiContext context, GuiDrawList drawList, string text, FontFace face, float size, GuiRect rect, Colour colour,
            Alignment horizontal = Alignment.Start, Alignment vertical = Alignment.Center)
        {
            if (string.IsNullOrEmpty(text)) return;
            Font font = context.GetFont(face, size);
            float scale = context.PixelScale;
            float width = TextLayout.MeasureWidth(font, text) / scale;
            float height = font.LineHeight / scale;
            float x = horizontal switch
            {
                Alignment.Center => rect.X + (rect.Width - width) * 0.5f,
                Alignment.End => rect.Right - width,
                _ => rect.X,
            };
            float y = vertical switch
            {
                Alignment.Center => rect.Y + (rect.Height - height) * 0.5f,
                Alignment.End => rect.Bottom - height,
                _ => rect.Y,
            };
            drawList.AddText(font, text, 0, text.Length, new Vector2(x, y), colour);
        }
    }
}

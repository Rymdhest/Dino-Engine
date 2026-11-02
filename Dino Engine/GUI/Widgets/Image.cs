using Dino_Engine.GUI.Rendering;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.GUI.Widgets
{
    /// <summary>
    /// Shows any OpenGL texture, for example procedural textures or engine render targets
    /// (use <see cref="FlipY"/> for framebuffer attachments, whose origin is bottom-left).
    /// </summary>
    public class Image : GuiElement
    {
        /// <summary>GL texture id. Ignored when <see cref="TextureSource"/> is set.</summary>
        public int Texture { get; set; }
        /// <summary>Queried every frame; use for textures that are recreated (e.g. render targets on resize).</summary>
        public Func<int>? TextureSource { get; set; }
        public Vector2 UV0 { get; set; } = Vector2.Zero;
        public Vector2 UV1 { get; set; } = Vector2.One;
        public bool FlipY { get; set; }
        public GuiTextureMode Mode { get; set; } = GuiTextureMode.Rgba;
        public Colour Tint { get; set; } = GuiColors.White;

        public Image(int texture = 0)
        {
            Texture = texture;
            Width = 128f;
            Height = 128f;
        }

        protected override void OnDraw(GuiDrawList drawList)
        {
            base.OnDraw(drawList);
            int texture = TextureSource?.Invoke() ?? Texture;
            if (texture <= 0) return;
            Vector2 uv0 = UV0;
            Vector2 uv1 = UV1;
            if (FlipY) (uv0.Y, uv1.Y) = (uv1.Y, uv0.Y);
            GuiRect rect = BorderWidth > 0f ? Bounds.Deflate(new Thickness(BorderWidth)) : Bounds;
            drawList.AddImage(texture, rect, uv0, uv1, Tint, Mode, MathF.Max(0f, CornerRadius - BorderWidth));
        }
    }
}

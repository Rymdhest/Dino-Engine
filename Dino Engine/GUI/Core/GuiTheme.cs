using Dino_Engine.GUI.Text;
using Dino_Engine.Util;

namespace Dino_Engine.GUI
{
    /// <summary>
    /// Default look of all widgets. Widgets copy these values when they are constructed, so changing the theme
    /// affects widgets created afterwards; each widget's properties can still be overridden individually.
    /// </summary>
    public class GuiTheme
    {
        public FontFace Font { get; set; }
        public FontFace MonoFont { get; set; }
        public float FontSize { get; set; } = 14f;
        public float SmallFontSize { get; set; } = 12f;
        public float TitleFontSize { get; set; } = 14f;

        public Colour Text { get; set; } = GuiColors.Rgba(226, 229, 235);
        public Colour TextMuted { get; set; } = GuiColors.Rgba(150, 156, 168);
        public Colour TextDisabled { get; set; } = GuiColors.Rgba(105, 110, 120);
        public Colour Accent { get; set; } = GuiColors.Rgba(92, 196, 124);
        public Colour AccentHover { get; set; } = GuiColors.Rgba(118, 214, 147);
        public Colour Warning { get; set; } = GuiColors.Rgba(240, 190, 80);
        public Colour Error { get; set; } = GuiColors.Rgba(235, 95, 90);

        public Colour WindowBackground { get; set; } = GuiColors.Rgba(22, 24, 29, 238);
        public Colour WindowBorder { get; set; } = GuiColors.Rgba(62, 67, 78);
        public Colour TitleBar { get; set; } = GuiColors.Rgba(33, 36, 43);
        public Colour TitleBarActive { get; set; } = GuiColors.Rgba(40, 46, 54);
        public Colour PanelBackground { get; set; } = GuiColors.Rgba(30, 33, 39, 230);
        public Colour Shadow { get; set; } = GuiColors.Rgba(0, 0, 0, 120);

        public Colour Control { get; set; } = GuiColors.Rgba(45, 49, 58);
        public Colour ControlHover { get; set; } = GuiColors.Rgba(57, 62, 73);
        public Colour ControlPressed { get; set; } = GuiColors.Rgba(36, 39, 46);
        public Colour ControlDisabled { get; set; } = GuiColors.Rgba(36, 38, 43);
        public Colour ControlBorder { get; set; } = GuiColors.Rgba(70, 76, 89);
        public Colour InputBackground { get; set; } = GuiColors.Rgba(17, 19, 23);
        public Colour Selection { get; set; } = GuiColors.Rgba(92, 196, 124, 90);
        public Colour Highlight { get; set; } = GuiColors.Rgba(255, 255, 255, 18);
        public Colour Separator { get; set; } = GuiColors.Rgba(60, 64, 74);
        public Colour ScrollbarThumb { get; set; } = GuiColors.Rgba(80, 86, 99);
        public Colour ScrollbarThumbHover { get; set; } = GuiColors.Rgba(105, 112, 128);
        public Colour TooltipBackground { get; set; } = GuiColors.Rgba(12, 13, 16, 245);

        public float CornerRadius { get; set; } = 4f;
        public float WindowCornerRadius { get; set; } = 6f;
        public float ControlHeight { get; set; } = 24f;
        public float Spacing { get; set; } = 4f;
        public float WindowPadding { get; set; } = 8f;
        public float TitleBarHeight { get; set; } = 28f;
        public float ScrollbarWidth { get; set; } = 8f;
        public float ShadowSize { get; set; } = 14f;
        public float BorderWidth { get; set; } = 1f;

        public GuiTheme(FontFace font, FontFace monoFont)
        {
            Font = font;
            MonoFont = monoFont;
        }
    }
}

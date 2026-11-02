using Dino_Engine.GUI.Rendering;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.GUI.Widgets
{
    public enum GuiIcon
    {
        None,
        Close,
        ChevronDown,
        ChevronRight,
        ChevronUp,
        ChevronLeft,
        Minus,
        Plus,
        ResizeGrip,
    }

    /// <summary>Simple vector icons built from anti-aliased lines, so no icon assets are needed.</summary>
    public static class GuiIcons
    {
        public static void Draw(GuiDrawList drawList, GuiIcon icon, GuiRect rect, Colour colour, float thickness = 1.5f)
        {
            Vector2 c = rect.Center;
            float s = MathF.Min(rect.Width, rect.Height) * 0.5f;
            switch (icon)
            {
                case GuiIcon.Close:
                    drawList.AddLine(c + new Vector2(-s, -s), c + new Vector2(s, s), colour, thickness, true);
                    drawList.AddLine(c + new Vector2(-s, s), c + new Vector2(s, -s), colour, thickness, true);
                    break;
                case GuiIcon.ChevronDown:
                    drawList.AddLine(c + new Vector2(-s, -s * 0.5f), c + new Vector2(0f, s * 0.5f), colour, thickness, true);
                    drawList.AddLine(c + new Vector2(0f, s * 0.5f), c + new Vector2(s, -s * 0.5f), colour, thickness, true);
                    break;
                case GuiIcon.ChevronUp:
                    drawList.AddLine(c + new Vector2(-s, s * 0.5f), c + new Vector2(0f, -s * 0.5f), colour, thickness, true);
                    drawList.AddLine(c + new Vector2(0f, -s * 0.5f), c + new Vector2(s, s * 0.5f), colour, thickness, true);
                    break;
                case GuiIcon.ChevronRight:
                    drawList.AddLine(c + new Vector2(-s * 0.5f, -s), c + new Vector2(s * 0.5f, 0f), colour, thickness, true);
                    drawList.AddLine(c + new Vector2(s * 0.5f, 0f), c + new Vector2(-s * 0.5f, s), colour, thickness, true);
                    break;
                case GuiIcon.ChevronLeft:
                    drawList.AddLine(c + new Vector2(s * 0.5f, -s), c + new Vector2(-s * 0.5f, 0f), colour, thickness, true);
                    drawList.AddLine(c + new Vector2(-s * 0.5f, 0f), c + new Vector2(s * 0.5f, s), colour, thickness, true);
                    break;
                case GuiIcon.Minus:
                    drawList.AddLine(c + new Vector2(-s, 0f), c + new Vector2(s, 0f), colour, thickness, true);
                    break;
                case GuiIcon.Plus:
                    drawList.AddLine(c + new Vector2(-s, 0f), c + new Vector2(s, 0f), colour, thickness, true);
                    drawList.AddLine(c + new Vector2(0f, -s), c + new Vector2(0f, s), colour, thickness, true);
                    break;
                case GuiIcon.ResizeGrip:
                    for (int i = 1; i <= 3; i++)
                    {
                        float o = s * 2f * i / 3f;
                        drawList.AddLine(new Vector2(rect.Right - o, rect.Bottom), new Vector2(rect.Right, rect.Bottom - o), colour, 1f);
                    }
                    break;
            }
        }
    }

    /// <summary>Small square button showing a vector icon (window close/collapse buttons, etc).</summary>
    public class IconButton : Button
    {
        public GuiIcon Icon { get; set; }
        public float IconSize { get; set; } = 8f;
        public Colour? IconColor { get; set; }

        public IconButton(GuiIcon icon, Action? onPressed = null) : base("", onPressed)
        {
            Icon = icon;
            float size = Theme.ControlHeight - 4f;
            Width = size;
            Height = size;
            MinWidth = 0f;
            Padding = 0f;
            BorderWidth = 0f;
            NormalColor = GuiColors.Transparent;
            Label.Visible = false;
        }

        protected override void OnDrawOverlay(GuiDrawList drawList)
        {
            GuiTheme theme = Theme;
            Colour colour = IconColor ?? (IsEnabledInHierarchy ? (IsHovered ? theme.Text : theme.TextMuted) : theme.TextDisabled);
            GuiRect iconRect = new GuiRect(Bounds.Center.X - IconSize * 0.5f, Bounds.Center.Y - IconSize * 0.5f, IconSize, IconSize);
            GuiIcons.Draw(drawList, Icon, iconRect, colour);
        }
    }
}

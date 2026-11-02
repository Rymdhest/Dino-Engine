using Dino_Engine.GUI.Rendering;
using Dino_Engine.Util;
using OpenTK.Windowing.Common.Input;

namespace Dino_Engine.GUI.Widgets
{
    /// <summary>Clickable rectangle with a centred <see cref="Label"/>. Colours animate between normal, hover and pressed.</summary>
    public class Button : GuiElement
    {
        private Colour _currentColor;
        private bool _colorInitialized;

        public Label Label { get; }
        public string Text { get => Label.Text; set => Label.Text = value; }

        public Colour NormalColor { get; set; }
        public Colour HoverColor { get; set; }
        public Colour PressedColor { get; set; }
        public Colour DisabledColor { get; set; }
        /// <summary>Speed of the colour transition between states, higher is faster (0 = instant).</summary>
        public float ColorTransitionSpeed { get; set; } = 18f;

        /// <summary>Raised when the button is clicked (pressed and released over it).</summary>
        public event Action? Pressed;

        public Button(string text = "", Action? onPressed = null)
        {
            GuiTheme theme = Theme;
            CapturesMouse = true;
            Cursor = MouseCursor.Hand;
            Height = theme.ControlHeight;
            MinWidth = theme.ControlHeight;
            Padding = new Thickness(10f, 0f);
            CornerRadius = theme.CornerRadius;
            BorderWidth = theme.BorderWidth;
            BorderColor = theme.ControlBorder;
            NormalColor = theme.Control;
            HoverColor = theme.ControlHover;
            PressedColor = theme.ControlPressed;
            DisabledColor = theme.ControlDisabled;
            Label = Add(new Label(text) { Anchor = Anchor.Center, HitTest = HitTestMode.None });
            if (onPressed != null) Pressed += onPressed;
        }

        protected Colour TargetColor
        {
            get
            {
                if (!IsEnabledInHierarchy) return DisabledColor;
                if (IsPressed && IsHovered) return PressedColor;
                if (IsHovered || IsPressed) return HoverColor;
                return NormalColor;
            }
        }

        protected override void OnClick(GuiMouseEventArgs e)
        {
            if (e.Button == OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Left) Pressed?.Invoke();
        }

        protected override void OnUpdate(float deltaTime)
        {
            Colour target = TargetColor;
            if (!_colorInitialized || ColorTransitionSpeed <= 0f)
            {
                _currentColor = target;
                _colorInitialized = true;
            }
            else
            {
                float t = 1f - MathF.Exp(-ColorTransitionSpeed * deltaTime);
                _currentColor = GuiColors.Lerp(_currentColor, target, t);
            }
        }

        protected override void OnDraw(GuiDrawList drawList)
        {
            if (!_colorInitialized) OnUpdate(0f);
            DrawBackground(drawList, _currentColor);
        }
    }
}

using Dino_Engine.GUI.Rendering;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Dino_Engine.GUI.Widgets
{
    /// <summary>
    /// Floating window: draggable title bar, optional collapse/close buttons and a resize grip.
    /// Add children to <see cref="Content"/> (a vertical stack). With Height Auto the window fits its content;
    /// give it a fixed Height (or resize it) to make Fill children such as a <see cref="ScrollPanel"/> take the rest.
    /// </summary>
    public class GuiWindow : GuiElement
    {
        private bool _collapsed;
        private UIValue _expandedHeight;
        private readonly IconButton _collapseButton;
        private readonly IconButton _closeButton;
        private readonly ResizeGrip _grip;

        public GuiElement TitleBar { get; }
        public Label TitleLabel { get; }
        public GuiElement Content { get; }

        public string Title { get => TitleLabel.Text; set => TitleLabel.Text = value; }
        public bool Draggable { get; set; } = true;
        /// <summary>Keep at least this much of the title bar inside the parent while dragging.</summary>
        public float KeepVisibleMargin { get; set; } = 48f;

        public bool Closable
        {
            get => _closeButton.Visible;
            set => _closeButton.Visible = value;
        }

        public bool Collapsible
        {
            get => _collapseButton.Visible;
            set => _collapseButton.Visible = value;
        }

        public bool Resizable
        {
            get => _grip.Enabled;
            set
            {
                _grip.Enabled = value;
                _grip.Visible = value && !_collapsed;
            }
        }

        public event Action? Closed;
        public event Action<bool>? CollapsedChanged;

        public GuiWindow(string title, float width = 320f, float height = float.NaN)
        {
            GuiTheme theme = Theme;
            Width = width;
            Height = float.IsNaN(height) ? UIValue.Auto : height;
            MinWidth = 140f;
            MinHeight = theme.TitleBarHeight;
            BackgroundColor = theme.WindowBackground;
            BorderColor = theme.WindowBorder;
            BorderWidth = theme.BorderWidth;
            CornerRadius = theme.WindowCornerRadius;
            ShadowSize = theme.ShadowSize;
            ShadowColor = theme.Shadow;
            ClipChildren = true;
            BringToFrontOnPress = true;
            HitTest = HitTestMode.Block;
            Layout = new StackLayout(Orientation.Vertical);

            TitleBar = Add(new TitleBarElement(this)
            {
                Width = UIValue.Fill,
                Height = theme.TitleBarHeight,
                Padding = new Thickness(10f, 0f, 3f, 0f),
                Layout = new StackLayout(Orientation.Horizontal, 2f) { CrossAlignment = Alignment.Center },
            });
            TitleLabel = TitleBar.Add(new Label(title) { Width = UIValue.Fill, FontSize = theme.TitleFontSize, HitTest = HitTestMode.None });
            _collapseButton = TitleBar.Add(new IconButton(GuiIcon.ChevronDown, () => Collapsed = !Collapsed) { Tooltip = "Collapse" });
            _closeButton = TitleBar.Add(new IconButton(GuiIcon.Close, Close) { Tooltip = "Close" });

            Content = Add(new GuiElement
            {
                Name = "WindowContent",
                Width = UIValue.Fill,
                Height = UIValue.Fill,
                Padding = theme.WindowPadding,
                Layout = new StackLayout(Orientation.Vertical, theme.Spacing),
            });

            _grip = Add(new ResizeGrip(this) { IgnoreLayout = true, Anchor = Anchor.BottomRight, Width = 14f, Height = 14f });
        }

        public bool Collapsed
        {
            get => _collapsed;
            set
            {
                if (_collapsed == value) return;
                _collapsed = value;
                if (value)
                {
                    _expandedHeight = Height;
                    Height = UIValue.Auto;
                }
                else
                {
                    Height = _expandedHeight;
                }
                Content.Visible = !value;
                _grip.Visible = !value && _grip.Enabled;
                _collapseButton.Icon = value ? GuiIcon.ChevronRight : GuiIcon.ChevronDown;
                _collapseButton.Tooltip = value ? "Expand" : "Collapse";
                CollapsedChanged?.Invoke(value);
            }
        }

        /// <summary>True if no visible sibling window is drawn above this one.</summary>
        public bool IsTopmostWindow
        {
            get
            {
                if (Parent == null) return false;
                var siblings = Parent.Children;
                for (int i = siblings.Count - 1; i >= 0; i--)
                {
                    if (siblings[i] is GuiWindow window && window.Visible) return window == this;
                }
                return false;
            }
        }

        public void Show()
        {
            Visible = true;
            BringToFront();
        }

        public void Close()
        {
            if (!Visible) return;
            Visible = false;
            Closed?.Invoke();
        }

        public void Toggle()
        {
            if (Visible) Close();
            else Show();
        }

        /// <summary>Converts the current placement to TopLeft anchoring with pixel offsets, so it can be moved and resized freely.</summary>
        private void PinToTopLeft()
        {
            if (Parent == null) return;
            GuiRect area = Parent.ContentBounds;
            Anchor = Anchor.TopLeft;
            X = Bounds.X - area.X - Margin.Left;
            Y = Bounds.Y - area.Y - Margin.Top;
        }

        internal void MoveBy(Vector2 delta)
        {
            if (Parent == null) return;
            PinToTopLeft();
            GuiRect area = Parent.ContentBounds;
            float x = Bounds.X + delta.X;
            float y = Bounds.Y + delta.Y;
            x = Math.Clamp(x, area.X - Bounds.Width + KeepVisibleMargin, MathF.Max(area.X, area.Right - KeepVisibleMargin));
            y = Math.Clamp(y, area.Y, MathF.Max(area.Y, area.Bottom - TitleBar.Bounds.Height));
            X = x - area.X - Margin.Left;
            Y = y - area.Y - Margin.Top;
        }

        internal void BeginResize() => PinToTopLeft();

        internal void ResizeTo(Vector2 size)
        {
            Width = MathF.Max(MinWidth, size.X);
            Height = MathF.Max(MinHeight, size.Y);
        }

        private sealed class TitleBarElement : GuiElement
        {
            private readonly GuiWindow _window;

            public TitleBarElement(GuiWindow window)
            {
                _window = window;
                CapturesMouse = true;
            }

            protected override bool WantsDrag => _window.Draggable;

            protected override void OnDrag(GuiDragEventArgs e)
            {
                if (e.Button == MouseButton.Left) _window.MoveBy(e.Delta);
            }

            protected override void OnClick(GuiMouseEventArgs e)
            {
                if (e.ClickCount == 2 && _window.Collapsible) _window.Collapsed = !_window.Collapsed;
            }

            protected override void OnDraw(GuiDrawList drawList)
            {
                GuiTheme theme = Theme;
                bool active = _window.IsTopmostWindow;
                Colour colour = active ? theme.TitleBarActive : theme.TitleBar;
                // Rounded top corners: draw a rounded rect and square off its lower half.
                float radius = _window.CornerRadius;
                drawList.AddRect(Bounds, colour, radius);
                if (!_window.Collapsed && Bounds.Height > radius)
                {
                    drawList.AddRect(GuiRect.FromEdges(Bounds.X, Bounds.Y + radius, Bounds.Right, Bounds.Bottom), colour);
                    drawList.AddRect(new GuiRect(Bounds.X, Bounds.Bottom - 1f, Bounds.Width, 1f), theme.Separator);
                }
                if (active) drawList.AddRect(new GuiRect(Bounds.X + radius, Bounds.Y, Bounds.Width - radius * 2f, 2f), theme.Accent.WithAlpha(0.8f));
            }
        }

        private sealed class ResizeGrip : GuiElement
        {
            private readonly GuiWindow _window;
            private Vector2 _startSize;

            public ResizeGrip(GuiWindow window)
            {
                _window = window;
                CapturesMouse = true;
                Cursor = MouseCursor.HResize;
                Tooltip = null;
            }

            protected override bool WantsDrag => true;

            protected override void OnDragStart(GuiDragEventArgs e)
            {
                _window.BeginResize();
                _startSize = _window.Bounds.Size;
            }

            protected override void OnDrag(GuiDragEventArgs e) => _window.ResizeTo(_startSize + e.TotalDelta);

            protected override void OnDraw(GuiDrawList drawList)
            {
                GuiTheme theme = Theme;
                Colour colour = IsHovered || IsPressed ? theme.Text : theme.TextDisabled;
                GuiIcons.Draw(drawList, GuiIcon.ResizeGrip, Bounds.Deflate(new Thickness(0f, 0f, 3f, 3f)), colour);
            }
        }
    }
}

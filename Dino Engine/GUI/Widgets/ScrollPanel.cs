using Dino_Engine.GUI.Rendering;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Dino_Engine.GUI.Widgets
{
    /// <summary>
    /// Vertically scrolling viewport. Add children to <see cref="Content"/> (a vertical stack by default).
    /// Scrolls with the mouse wheel or by dragging the scrollbar thumb. With Height Auto and a MaxHeight it grows
    /// with its content until MaxHeight, then starts scrolling.
    /// </summary>
    public class ScrollPanel : GuiElement
    {
        private float _scrollOffset;
        private float _contentHeight;
        private float _viewportHeight;
        private bool _scrollbarVisible;
        private float _measuredWidth = float.NaN;
        private bool _draggingThumb;
        private float _dragStartOffset;
        private bool _stickToBottom;
        private bool _autoScrollToBottom;

        /// <summary>Container for the scrolled children.</summary>
        public GuiElement Content { get; }
        public float ScrollSpeed { get; set; } = 48f;
        public float ScrollbarWidth { get; set; }

        /// <summary>The thumb's colours, for anything that isn't dressed in the theme.</summary>
        public Colour? ThumbColor { get; set; }
        public Colour? ThumbHoverColor { get; set; }
        /// <summary>Keeps the view pinned to the bottom while the user is at the bottom (logs, consoles).</summary>
        public bool AutoScrollToBottom
        {
            get => _autoScrollToBottom;
            set
            {
                _autoScrollToBottom = value;
                _stickToBottom = value;
                InvalidateLayout();
            }
        }

        public ScrollPanel()
        {
            GuiTheme theme = Theme;
            ClipChildren = true;
            ScrollbarWidth = theme.ScrollbarWidth;
            Content = base.Add(new GuiElement { Width = UIValue.Fill, Layout = new StackLayout(Orientation.Vertical, theme.Spacing), Name = "ScrollContent" });
        }

        public float ScrollOffset
        {
            get => _scrollOffset;
            set
            {
                float clamped = Math.Clamp(value, 0f, MaxScroll);
                if (clamped == _scrollOffset) return;
                _scrollOffset = clamped;
                _stickToBottom = AutoScrollToBottom && _scrollOffset >= MaxScroll - 1f;
                InvalidateLayout();
            }
        }

        public float MaxScroll => MathF.Max(0f, _contentHeight - _viewportHeight);
        public bool CanScroll => MaxScroll > 0.5f;

        public void ScrollToTop() => ScrollOffset = 0f;

        public void ScrollToBottom()
        {
            _stickToBottom = AutoScrollToBottom;
            _scrollOffset = float.MaxValue; // clamped during arrange, content height may not be known yet
            InvalidateLayout();
        }

        /// <summary>Scrolls the minimum amount needed to show a descendant.</summary>
        public void ScrollIntoView(GuiElement element)
        {
            if (!Content.IsAncestorOf(element)) return;
            GuiRect viewport = ContentBounds;
            if (element.Bounds.Top < viewport.Top) ScrollOffset -= viewport.Top - element.Bounds.Top;
            else if (element.Bounds.Bottom > viewport.Bottom) ScrollOffset += element.Bounds.Bottom - viewport.Bottom;
        }

        private float ContentWidth(float viewportWidth, bool withScrollbar) => MathF.Max(0f, viewportWidth - (withScrollbar ? ScrollbarWidth + 4f : 0f));

        protected override Vector2 MeasureContent(Vector2 reference, Vector2 available)
        {
            float width = available.X;
            bool withScrollbar = _scrollbarVisible && float.IsFinite(width);
            float contentWidth = float.IsFinite(width) ? ContentWidth(width, withScrollbar) : float.PositiveInfinity;
            Content.Measure(new Vector2(contentWidth, float.PositiveInfinity), new Vector2(contentWidth, float.PositiveInfinity));
            if (float.IsFinite(width) && float.IsFinite(available.Y) && !withScrollbar && Content.DesiredSize.Y > available.Y)
            {
                contentWidth = ContentWidth(width, true);
                Content.Measure(new Vector2(contentWidth, float.PositiveInfinity), new Vector2(contentWidth, float.PositiveInfinity));
            }
            _measuredWidth = contentWidth;
            Vector2 extra = Vector2.Zero;
            foreach (var child in Children)
            {
                if (child != Content && child.Visible) extra = Vector2.ComponentMax(extra, MeasureAnchored(child, reference, available));
            }
            float desiredWidth = Content.DesiredSize.X + (float.IsFinite(width) && contentWidth < width ? ScrollbarWidth + 4f : 0f);
            return Vector2.ComponentMax(extra, new Vector2(desiredWidth, MathF.Min(Content.DesiredSize.Y, available.Y)));
        }

        protected override void ArrangeContent(GuiRect viewport)
        {
            _viewportHeight = viewport.Height;
            bool needScrollbar = Content.DesiredSize.Y > viewport.Height + 0.5f;
            float width = ContentWidth(viewport.Width, needScrollbar);
            if (MathF.Abs(width - _measuredWidth) > 0.01f)
            {
                Content.Measure(new Vector2(width, float.PositiveInfinity), new Vector2(width, float.PositiveInfinity));
                needScrollbar = Content.DesiredSize.Y > viewport.Height + 0.5f;
                float corrected = ContentWidth(viewport.Width, needScrollbar);
                if (MathF.Abs(corrected - width) > 0.01f)
                {
                    width = corrected;
                    Content.Measure(new Vector2(width, float.PositiveInfinity), new Vector2(width, float.PositiveInfinity));
                }
                _measuredWidth = width;
            }
            _scrollbarVisible = needScrollbar;
            _contentHeight = Content.DesiredSize.Y;

            if (_stickToBottom) _scrollOffset = MaxScroll;
            _scrollOffset = Math.Clamp(_scrollOffset, 0f, MaxScroll);
            Content.Arrange(new GuiRect(viewport.X, viewport.Y - _scrollOffset, width, _contentHeight));

            foreach (var child in Children)
            {
                if (child != Content && child.Visible) ArrangeAnchored(child, viewport);
            }
        }

        private GuiRect TrackRect
        {
            get
            {
                GuiRect viewport = ContentBounds;
                return new GuiRect(viewport.Right - ScrollbarWidth, viewport.Y + 2f, ScrollbarWidth, MathF.Max(0f, viewport.Height - 4f));
            }
        }

        private GuiRect ThumbRect
        {
            get
            {
                GuiRect track = TrackRect;
                float visible = _contentHeight > 0f ? Math.Clamp(_viewportHeight / _contentHeight, 0f, 1f) : 1f;
                float thumbHeight = MathF.Max(20f, track.Height * visible);
                float t = MaxScroll > 0f ? _scrollOffset / MaxScroll : 0f;
                return new GuiRect(track.X, track.Y + (track.Height - thumbHeight) * t, track.Width, thumbHeight);
            }
        }

        /// <summary>The whole viewport receives the mouse so the wheel works anywhere over it, including over pass-through text.</summary>
        public override bool HitTestSelf(Vector2 point)
        {
            if (HitTest == HitTestMode.None || HitTest == HitTestMode.PassThrough) return false;
            return Bounds.Contains(point);
        }

        protected override bool WantsDrag => _draggingThumb;

        protected override void OnMouseDown(GuiMouseEventArgs e)
        {
            if (!_scrollbarVisible || e.Button != MouseButton.Left || !TrackRect.Inflate(2f).Contains(e.Position)) return;
            GuiRect thumb = ThumbRect;
            if (!thumb.Contains(e.Position))
            {
                // Jump so the thumb centres on the click.
                GuiRect track = TrackRect;
                float travel = MathF.Max(1f, track.Height - thumb.Height);
                ScrollOffset = (e.Position.Y - track.Y - thumb.Height * 0.5f) / travel * MaxScroll;
            }
            _draggingThumb = true;
            _dragStartOffset = _scrollOffset;
            e.Handled = true;
        }

        protected override void OnDrag(GuiDragEventArgs e)
        {
            if (!_draggingThumb) return;
            GuiRect track = TrackRect;
            float travel = MathF.Max(1f, track.Height - ThumbRect.Height);
            ScrollOffset = _dragStartOffset + e.TotalDelta.Y / travel * MaxScroll;
        }

        protected override void OnMouseUp(GuiMouseEventArgs e) => _draggingThumb = false;
        protected override void OnDragEnd(GuiDragEventArgs e) => _draggingThumb = false;

        protected override void OnScroll(GuiScrollEventArgs e)
        {
            if (!CanScroll || e.Delta.Y == 0f) return;
            float before = _scrollOffset;
            ScrollOffset -= e.Delta.Y * ScrollSpeed;
            if (_scrollOffset != before) e.Handled = true;
        }

        protected override void OnDrawOverlay(GuiDrawList drawList)
        {
            if (!_scrollbarVisible) return;
            GuiTheme theme = Theme;
            GuiRect thumb = ThumbRect;
            bool hot = _draggingThumb || (IsHovered && TrackRect.Inflate(2f).Contains(Context?.MousePosition ?? Vector2.Zero));
            drawList.AddRect(TrackRect, GuiColors.Rgba(0, 0, 0, 40), ScrollbarWidth * 0.5f);
            drawList.AddRect(thumb, hot ? ThumbHoverColor ?? theme.ScrollbarThumbHover : ThumbColor ?? theme.ScrollbarThumb, ScrollbarWidth * 0.5f);
        }
    }
}

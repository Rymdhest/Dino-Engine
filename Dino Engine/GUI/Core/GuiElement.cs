using Dino_Engine.GUI.Rendering;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common.Input;

namespace Dino_Engine.GUI
{
    /// <summary>
    /// The single building block of the GUI: a rectangle that can be coloured, bordered, rounded, anchored,
    /// constrained and nested. Every widget is a GuiElement (or a composition of them).
    ///
    /// Layout is two-pass. <see cref="Measure"/> computes <see cref="DesiredSize"/> bottom-up, then
    /// <see cref="Arrange"/> assigns final absolute <see cref="Bounds"/> top-down. Children are placed by their
    /// <see cref="Anchor"/> and X/Y offsets, unless the parent has a <see cref="Layout"/> (stack, grid, ...).
    /// </summary>
    public class GuiElement
    {
        private readonly List<GuiElement> _children = new();
        private GuiContext? _context;

        private Anchor _anchor = Anchor.TopLeft;
        private UIValue _x = 0f;
        private UIValue _y = 0f;
        private UIValue _width = UIValue.Auto;
        private UIValue _height = UIValue.Auto;
        private float _minWidth;
        private float _minHeight;
        private float _maxWidth = float.PositiveInfinity;
        private float _maxHeight = float.PositiveInfinity;
        private Thickness _margin;
        private Thickness _padding;
        private float _aspectRatio;
        private GuiLayout? _layout;
        private bool _ignoreLayout;
        private bool _visible = true;

        public GuiElement() { }

        // =============================================================== hierarchy

        public GuiElement? Parent { get; private set; }
        public IReadOnlyList<GuiElement> Children => _children;
        /// <summary>The context this element is attached to, or null while it is not part of a GUI.</summary>
        public GuiContext? Context => _context;
        public bool IsAttached => _context != null;
        public string? Name { get; set; }
        /// <summary>Free slot for user data.</summary>
        public object? Tag { get; set; }

        /// <summary>Adds a child and returns it, so construction can be chained: <c>var b = panel.Add(new Button("Ok"));</c></summary>
        public T Add<T>(T child) where T : GuiElement
        {
            Insert(_children.Count, child);
            return child;
        }

        public void AddRange(params GuiElement[] children)
        {
            foreach (var child in children) Add(child);
        }

        public void Insert(int index, GuiElement child)
        {
            if (child == null) throw new ArgumentNullException(nameof(child));
            if (child == this || child.IsAncestorOf(this)) throw new InvalidOperationException("Cannot add an element to itself or to one of its descendants.");
            if (child.Parent == this && _children.IndexOf(child) < index) index--;
            child.RemoveFromParent();
            index = Math.Clamp(index, 0, _children.Count);
            _children.Insert(index, child);
            child.Parent = this;
            child.SetContextRecursive(_context);
            InvalidateLayout();
        }

        public bool Remove(GuiElement child)
        {
            if (!_children.Remove(child)) return false;
            child.Parent = null;
            child.SetContextRecursive(null);
            InvalidateLayout();
            return true;
        }

        public void ClearChildren()
        {
            while (_children.Count > 0) Remove(_children[^1]);
        }

        public void RemoveFromParent() => Parent?.Remove(this);

        /// <summary>Moves this element to the end of its parent's children, so it is drawn on top and hit first.</summary>
        public void BringToFront()
        {
            if (Parent == null) return;
            var siblings = Parent._children;
            if (siblings.Count > 0 && siblings[^1] == this) return;
            siblings.Remove(this);
            siblings.Add(this);
            InvalidateLayout();
        }

        public void SendToBack()
        {
            if (Parent == null) return;
            var siblings = Parent._children;
            if (siblings.Count > 0 && siblings[0] == this) return;
            siblings.Remove(this);
            siblings.Insert(0, this);
            InvalidateLayout();
        }

        public bool IsAncestorOf(GuiElement? element)
        {
            for (var current = element?.Parent; current != null; current = current.Parent)
            {
                if (current == this) return true;
            }
            return false;
        }

        /// <summary>True if the element is this element or one of its descendants.</summary>
        public bool Contains(GuiElement? element) => element == this || IsAncestorOf(element);

        public IEnumerable<GuiElement> Descendants()
        {
            foreach (var child in _children)
            {
                yield return child;
                foreach (var descendant in child.Descendants()) yield return descendant;
            }
        }

        public T? FindAncestor<T>() where T : GuiElement
        {
            for (var current = Parent; current != null; current = current.Parent)
            {
                if (current is T match) return match;
            }
            return null;
        }

        public T? Find<T>(string name) where T : GuiElement
        {
            foreach (var element in Descendants())
            {
                if (element is T match && element.Name == name) return match;
            }
            return null;
        }

        internal void SetContextRecursive(GuiContext? context)
        {
            if (_context == context) return;
            GuiContext? previous = _context;
            _context = context;
            if (previous != null)
            {
                previous.OnElementDetached(this);
                OnDetached();
            }
            if (context != null) OnAttached();
            foreach (var child in _children) child.SetContextRecursive(context);
            context?.InvalidateLayout();
        }

        /// <summary>Called when the element becomes part of a GUI context.</summary>
        protected virtual void OnAttached() { }
        /// <summary>Called when the element is removed from its GUI context.</summary>
        protected virtual void OnDetached() { }

        // =============================================================== layout properties

        /// <summary>Point of the parent this element is attached to. Ignored when the parent has a layout (unless <see cref="IgnoreLayout"/>).</summary>
        public Anchor Anchor { get => _anchor; set => SetLayoutField(ref _anchor, value); }
        /// <summary>Horizontal offset inward from the anchored edge (pixels or relative to the parent width).</summary>
        public UIValue X { get => _x; set => SetLayoutField(ref _x, value); }
        /// <summary>Vertical offset inward from the anchored edge (pixels or relative to the parent height).</summary>
        public UIValue Y { get => _y; set => SetLayoutField(ref _y, value); }
        public UIValue Width { get => _width; set => SetLayoutField(ref _width, value); }
        public UIValue Height { get => _height; set => SetLayoutField(ref _height, value); }
        public float MinWidth { get => _minWidth; set => SetLayoutField(ref _minWidth, value); }
        public float MinHeight { get => _minHeight; set => SetLayoutField(ref _minHeight, value); }
        public float MaxWidth { get => _maxWidth; set => SetLayoutField(ref _maxWidth, value); }
        public float MaxHeight { get => _maxHeight; set => SetLayoutField(ref _maxHeight, value); }
        /// <summary>Space kept free around the element.</summary>
        public Thickness Margin { get => _margin; set => SetLayoutField(ref _margin, value); }
        /// <summary>Space between the element's edge and its content/children.</summary>
        public Thickness Padding { get => _padding; set => SetLayoutField(ref _padding, value); }
        /// <summary>Width / height. When &gt; 0 an Auto dimension is derived from the other one.</summary>
        public float AspectRatio { get => _aspectRatio; set => SetLayoutField(ref _aspectRatio, value); }
        /// <summary>Arranges the children (stack, grid...). Null means children are placed by their own anchors.</summary>
        public GuiLayout? Layout
        {
            get => _layout;
            set
            {
                if (_layout == value) return;
                _layout = value;
                InvalidateLayout();
            }
        }
        /// <summary>When true this element is placed by its anchor even if the parent has a layout (overlays, corner grips...).</summary>
        public bool IgnoreLayout { get => _ignoreLayout; set => SetLayoutField(ref _ignoreLayout, value); }
        /// <summary>Invisible elements take no space, are not drawn and cannot be interacted with.</summary>
        public bool Visible { get => _visible; set => SetLayoutField(ref _visible, value); }

        /// <summary>Size computed by the last measure pass (border box, margins excluded).</summary>
        public Vector2 DesiredSize { get; private set; }
        /// <summary>Final absolute rectangle in GUI units, valid after layout.</summary>
        public GuiRect Bounds { get; private set; }
        /// <summary>Bounds minus padding: the area children are laid out in.</summary>
        public GuiRect ContentBounds => Bounds.Deflate(Padding);

        public void SetPosition(UIValue x, UIValue y)
        {
            X = x;
            Y = y;
        }

        public void SetSize(UIValue width, UIValue height)
        {
            Width = width;
            Height = height;
        }

        /// <summary>Requests a new layout pass for the whole context (cheap, coalesced into one pass per frame).</summary>
        public void InvalidateLayout() => _context?.InvalidateLayout();

        protected void SetLayoutField<T>(ref T field, T value)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            InvalidateLayout();
        }

        // =============================================================== visual properties

        public Colour BackgroundColor { get; set; } = GuiColors.Transparent;
        public Colour BorderColor { get; set; } = GuiColors.Transparent;
        public float BorderWidth { get; set; }
        public float CornerRadius { get; set; }
        /// <summary>Opacity multiplied into this element and all its children.</summary>
        public float Opacity { get; set; } = 1f;
        /// <summary>Clip children to this element's bounds.</summary>
        public bool ClipChildren { get; set; }
        public float ShadowSize { get; set; }
        public Colour ShadowColor { get; set; } = GuiColors.Rgba(0, 0, 0, 110);
        public Vector2 ShadowOffset { get; set; } = new Vector2(0f, 3f);

        // =============================================================== interaction properties

        public bool Enabled { get; set; } = true;
        public bool IsEnabledInHierarchy
        {
            get
            {
                for (var current = this; current != null; current = current.Parent)
                {
                    if (!current.Enabled) return false;
                }
                return true;
            }
        }

        public HitTestMode HitTest { get; set; } = HitTestMode.Auto;
        /// <summary>Can receive keyboard focus by clicking it.</summary>
        public bool Focusable { get; set; }
        /// <summary>Captures the mouse on press so it receives clicks and drags even if it has no handlers subscribed.</summary>
        public bool CapturesMouse { get; set; }
        /// <summary>When pressed (or a descendant is pressed) the element is moved to the front of its siblings. Used by windows.</summary>
        public bool BringToFrontOnPress { get; set; }
        /// <summary>Text shown in a tooltip after hovering for a moment.</summary>
        public string? Tooltip { get; set; }
        /// <summary>Mouse cursor shown while hovering. Null inherits from the parent.</summary>
        public MouseCursor? Cursor { get; set; }

        public bool IsHovered { get; internal set; }
        /// <summary>True while this element holds the mouse capture (pressed and not yet released).</summary>
        public bool IsPressed => _context != null && _context.CapturedElement == this;
        public bool IsFocused => _context != null && _context.FocusedElement == this;
        /// <summary>True if this element or any descendant has keyboard focus.</summary>
        public bool IsFocusWithin => _context != null && Contains(_context.FocusedElement);
        /// <summary>When focused, keyboard input is reserved for the GUI (the game should ignore it).</summary>
        public virtual bool WantsKeyboardInput => false;

        // =============================================================== events

        public event Action<GuiMouseEventArgs>? MouseDown;
        public event Action<GuiMouseEventArgs>? MouseUp;
        public event Action<GuiMouseEventArgs>? Clicked;
        public event Action<GuiScrollEventArgs>? Scrolled;
        public event Action<GuiDragEventArgs>? DragStarted;
        public event Action<GuiDragEventArgs>? Dragged;
        public event Action<GuiDragEventArgs>? DragEnded;
        public event Action<GuiKeyEventArgs>? KeyDown;
        public event Action<GuiTextEventArgs>? TextInput;
        public event Action? MouseEnter;
        public event Action? MouseLeave;
        public event Action? FocusGained;
        public event Action? FocusLost;
        /// <summary>Raised once per frame (before layout and drawing) while the element is visible. Ideal for binding live values.</summary>
        public event Action<float>? Updated;

        protected virtual void OnMouseDown(GuiMouseEventArgs e) { }
        protected virtual void OnMouseUp(GuiMouseEventArgs e) { }
        protected virtual void OnClick(GuiMouseEventArgs e) { }
        protected virtual void OnScroll(GuiScrollEventArgs e) { }
        protected virtual void OnDragStart(GuiDragEventArgs e) { }
        protected virtual void OnDrag(GuiDragEventArgs e) { }
        protected virtual void OnDragEnd(GuiDragEventArgs e) { }
        protected virtual void OnKeyDown(GuiKeyEventArgs e) { }
        protected virtual void OnTextInput(GuiTextEventArgs e) { }
        protected virtual void OnMouseEnter() { }
        protected virtual void OnMouseLeave() { }
        protected virtual void OnFocusGained() { }
        protected virtual void OnFocusLost() { }
        protected virtual void OnUpdate(float deltaTime) { }

        /// <summary>Whether the element reacts to the mouse (used by <see cref="HitTestMode.Auto"/> and mouse capture).</summary>
        protected virtual bool IsInteractive => CapturesMouse || Focusable || Clicked != null || MouseDown != null || Dragged != null || DragStarted != null;
        /// <summary>Whether pressing and moving the mouse should start a drag (which suppresses the click).</summary>
        protected virtual bool WantsDrag => Dragged != null || DragStarted != null;

        internal bool InternalIsInteractive => IsInteractive;
        internal bool InternalWantsDrag => WantsDrag;

        internal void RaiseMouseDown(GuiMouseEventArgs e) { OnMouseDown(e); MouseDown?.Invoke(e); }
        internal void RaiseMouseUp(GuiMouseEventArgs e) { OnMouseUp(e); MouseUp?.Invoke(e); }
        internal void RaiseClick(GuiMouseEventArgs e) { OnClick(e); Clicked?.Invoke(e); }
        internal void RaiseScroll(GuiScrollEventArgs e) { OnScroll(e); Scrolled?.Invoke(e); }
        internal void RaiseDragStart(GuiDragEventArgs e) { OnDragStart(e); DragStarted?.Invoke(e); }
        internal void RaiseDrag(GuiDragEventArgs e) { OnDrag(e); Dragged?.Invoke(e); }
        internal void RaiseDragEnd(GuiDragEventArgs e) { OnDragEnd(e); DragEnded?.Invoke(e); }
        internal void RaiseKeyDown(GuiKeyEventArgs e) { OnKeyDown(e); KeyDown?.Invoke(e); }
        internal void RaiseTextInput(GuiTextEventArgs e) { OnTextInput(e); TextInput?.Invoke(e); }
        internal void RaiseMouseEnter() { OnMouseEnter(); MouseEnter?.Invoke(); }
        internal void RaiseMouseLeave() { OnMouseLeave(); MouseLeave?.Invoke(); }
        internal void RaiseFocusGained() { OnFocusGained(); FocusGained?.Invoke(); }
        internal void RaiseFocusLost() { OnFocusLost(); FocusLost?.Invoke(); }

        /// <summary>Gives this element keyboard focus.</summary>
        public void Focus() => _context?.SetFocus(this);

        /// <summary>Removes keyboard focus if this element has it.</summary>
        public void Unfocus()
        {
            if (IsFocused) _context?.SetFocus(null);
        }

        // =============================================================== update

        /// <summary>Updates this visible subtree and returns how many elements were visited.</summary>
        internal int UpdateTree(float deltaTime)
        {
            if (!_visible) return 0;
            OnUpdate(deltaTime);
            Updated?.Invoke(deltaTime);
            int count = 1;
            // Index based so handlers may add/remove elements without invalidating an enumerator.
            for (int i = 0; i < _children.Count; i++) count += _children[i].UpdateTree(deltaTime);
            return count;
        }

        // =============================================================== measure

        public void Measure(Vector2 reference, Vector2 available)
        {
            float width = MeasureLength(_width, reference.X, available.X);
            float height = MeasureLength(_height, reference.Y, available.Y);
            if (_aspectRatio > 0f)
            {
                if (float.IsNaN(height) && !float.IsNaN(width)) height = width / _aspectRatio;
                else if (float.IsNaN(width) && !float.IsNaN(height)) width = height * _aspectRatio;
            }
            if (!float.IsNaN(width)) width = ClampWidth(width);
            if (!float.IsNaN(height)) height = ClampHeight(height);

            Vector2 contentReference = new Vector2(
                float.IsNaN(width) ? float.PositiveInfinity : MathF.Max(0f, width - _padding.Horizontal),
                float.IsNaN(height) ? float.PositiveInfinity : MathF.Max(0f, height - _padding.Vertical));
            float availableWidth = float.IsNaN(width) ? MathF.Min(available.X, _maxWidth) : width;
            float availableHeight = float.IsNaN(height) ? MathF.Min(available.Y, _maxHeight) : height;
            Vector2 contentAvailable = new Vector2(
                MathF.Max(0f, availableWidth - _padding.Horizontal),
                MathF.Max(0f, availableHeight - _padding.Vertical));

            Vector2 content = MeasureContent(contentReference, contentAvailable);
            if (float.IsNaN(width)) width = content.X + _padding.Horizontal;
            if (float.IsNaN(height)) height = content.Y + _padding.Vertical;
            if (_aspectRatio > 0f && _width.Mode == SizeMode.Auto && _height.Mode == SizeMode.Auto) height = width / _aspectRatio;

            DesiredSize = new Vector2(ClampWidth(width), ClampHeight(height));
        }

        /// <summary>
        /// Measures the content (children, text...) and returns its size, excluding padding.
        /// <paramref name="reference"/> is this element's content size if known (for relative child sizes, may be infinite),
        /// <paramref name="available"/> the space the content may use (may be infinite).
        /// </summary>
        protected virtual Vector2 MeasureContent(Vector2 reference, Vector2 available)
        {
            Vector2 size = Vector2.Zero;
            if (_layout != null) size = _layout.Measure(this, reference, available);
            foreach (var child in _children)
            {
                if (!child._visible) continue;
                if (_layout != null && !child._ignoreLayout) continue;
                Vector2 extent = MeasureAnchored(child, reference, available);
                size = Vector2.ComponentMax(size, extent);
            }
            return size;
        }

        /// <summary>Measures a child placed by its anchor, returning the space it needs including margins and offsets.</summary>
        public static Vector2 MeasureAnchored(GuiElement child, Vector2 reference, Vector2 available)
        {
            Vector2 childAvailable = new Vector2(
                MathF.Max(0f, available.X - child._margin.Horizontal),
                MathF.Max(0f, available.Y - child._margin.Vertical));
            child.Measure(reference, childAvailable);
            float offsetX = MathF.Abs(child._x.ResolveOffset(reference.X));
            float offsetY = MathF.Abs(child._y.ResolveOffset(reference.Y));
            return new Vector2(
                child.DesiredSize.X + child._margin.Horizontal + offsetX,
                child.DesiredSize.Y + child._margin.Vertical + offsetY);
        }

        private static float MeasureLength(UIValue value, float reference, float available)
        {
            switch (value.Mode)
            {
                case SizeMode.Pixels: return value.Value;
                case SizeMode.Relative: return float.IsFinite(reference) ? reference * value.Value + value.Offset : float.NaN;
                case SizeMode.Fill: return float.IsFinite(available) ? available : float.NaN;
                default: return float.NaN;
            }
        }

        public float ClampWidth(float width) => MathF.Max(0f, MathF.Max(_minWidth, MathF.Min(width, _maxWidth)));
        public float ClampHeight(float height) => MathF.Max(0f, MathF.Max(_minHeight, MathF.Min(height, _maxHeight)));

        // =============================================================== arrange

        /// <summary>Assigns the final rectangle (absolute GUI units) and arranges the children inside it.</summary>
        public void Arrange(GuiRect rect)
        {
            Bounds = Snap(rect);
            ArrangeContent(Bounds.Deflate(_padding));
        }

        /// <summary>Places the children inside the content rectangle.</summary>
        protected virtual void ArrangeContent(GuiRect content)
        {
            _layout?.Arrange(this, content);
            foreach (var child in _children)
            {
                if (!child._visible) continue;
                if (_layout != null && !child._ignoreLayout) continue;
                ArrangeAnchored(child, content);
            }
        }

        /// <summary>
        /// Final size of this element given its parent's content size (reference) and the space available to it
        /// (margins already removed). Fill takes the available space, Auto uses the measured size.
        /// </summary>
        public Vector2 ComputeArrangeSize(Vector2 reference, Vector2 available)
        {
            float width = ArrangeLength(_width, reference.X, available.X, DesiredSize.X);
            float height = ArrangeLength(_height, reference.Y, available.Y, DesiredSize.Y);
            if (_aspectRatio > 0f)
            {
                if (_height.Mode == SizeMode.Auto && _width.Mode != SizeMode.Auto) height = width / _aspectRatio;
                else if (_width.Mode == SizeMode.Auto && _height.Mode != SizeMode.Auto) width = height * _aspectRatio;
            }
            return new Vector2(ClampWidth(width), ClampHeight(height));
        }

        private static float ArrangeLength(UIValue value, float reference, float available, float desired)
        {
            switch (value.Mode)
            {
                case SizeMode.Pixels: return value.Value;
                case SizeMode.Relative: return float.IsFinite(reference) ? reference * value.Value + value.Offset : desired;
                case SizeMode.Fill: return float.IsFinite(available) ? available : desired;
                default: return desired;
            }
        }

        /// <summary>Places a child inside an area according to its anchor, offsets, size and margins.</summary>
        public static void ArrangeAnchored(GuiElement child, GuiRect area)
        {
            Vector2 available = new Vector2(
                MathF.Max(0f, area.Width - child._margin.Horizontal),
                MathF.Max(0f, area.Height - child._margin.Vertical));
            Vector2 size = child.ComputeArrangeSize(area.Size, available);
            float offsetX = child._x.ResolveOffset(area.Width);
            float offsetY = child._y.ResolveOffset(area.Height);

            float x = child._anchor.Horizontal() switch
            {
                Alignment.Start => area.X + child._margin.Left + offsetX,
                Alignment.Center => area.X + (area.Width - size.X) * 0.5f + (child._margin.Left - child._margin.Right) * 0.5f + offsetX,
                _ => area.Right - child._margin.Right - size.X - offsetX,
            };
            float y = child._anchor.Vertical() switch
            {
                Alignment.Start => area.Y + child._margin.Top + offsetY,
                Alignment.Center => area.Y + (area.Height - size.Y) * 0.5f + (child._margin.Top - child._margin.Bottom) * 0.5f + offsetY,
                _ => area.Bottom - child._margin.Bottom - size.Y - offsetY,
            };
            child.Arrange(new GuiRect(x, y, size.X, size.Y));
        }

        /// <summary>Snaps a rect's edges to the physical pixel grid so borders and text stay crisp.</summary>
        protected GuiRect Snap(GuiRect rect)
        {
            float scale = _context?.PixelScale ?? 1f;
            if (scale <= 0f) return rect;
            float left = MathF.Round(rect.Left * scale) / scale;
            float top = MathF.Round(rect.Top * scale) / scale;
            float right = MathF.Round(rect.Right * scale) / scale;
            float bottom = MathF.Round(rect.Bottom * scale) / scale;
            return GuiRect.FromEdges(left, top, MathF.Max(left, right), MathF.Max(top, bottom));
        }

        // =============================================================== hit testing

        /// <summary>Whether this element itself (not its children) blocks the mouse at the given point.</summary>
        public virtual bool HitTestSelf(Vector2 point)
        {
            switch (HitTest)
            {
                case HitTestMode.None:
                case HitTestMode.PassThrough:
                    return false;
                case HitTestMode.Block:
                    return Bounds.Contains(point);
                default:
                    if (!Bounds.Contains(point)) return false;
                    return BackgroundColor.IsVisible() || (BorderWidth > 0f && BorderColor.IsVisible()) || IsInteractive || Tooltip != null;
            }
        }

        /// <summary>The rect children are clipped to when <see cref="ClipChildren"/> is set.</summary>
        public virtual GuiRect ClipRect => BorderWidth > 0f ? Bounds.Deflate(new Thickness(BorderWidth)) : Bounds;

        // =============================================================== drawing

        internal void DrawTree(GuiDrawList drawList)
        {
            if (!_visible) return;
            bool pushedOpacity = Opacity < 1f;
            if (pushedOpacity)
            {
                drawList.PushOpacity(Opacity);
                if (drawList.CurrentOpacity <= 0.001f)
                {
                    drawList.PopOpacity();
                    return;
                }
            }

            GuiRect drawExtent = ShadowSize > 0f ? Bounds.Inflate(ShadowSize + MathF.Max(MathF.Abs(ShadowOffset.X), MathF.Abs(ShadowOffset.Y))) : Bounds;
            bool selfVisible = drawList.IsVisible(drawExtent);
            if (selfVisible) OnDraw(drawList);

            bool clip = ClipChildren;
            if (clip && !drawList.IsVisible(Bounds))
            {
                if (pushedOpacity) drawList.PopOpacity();
                return;
            }
            if (clip) drawList.PushClip(ClipRect);
            for (int i = 0; i < _children.Count; i++) _children[i].DrawTree(drawList);
            if (clip) drawList.PopClip();

            if (selfVisible) OnDrawOverlay(drawList);
            if (pushedOpacity) drawList.PopOpacity();
        }

        /// <summary>Draws the element itself, before its children. The default draws shadow, background and border.</summary>
        protected virtual void OnDraw(GuiDrawList drawList)
        {
            DrawBackground(drawList, BackgroundColor);
        }

        /// <summary>Draws on top of the children (scrollbars, focus rings, overlays).</summary>
        protected virtual void OnDrawOverlay(GuiDrawList drawList) { }

        protected void DrawBackground(GuiDrawList drawList, Colour background)
        {
            if (ShadowSize > 0f && ShadowColor.IsVisible())
            {
                GuiRect shadow = Bounds.Offset(ShadowOffset).Inflate(ShadowSize * 0.25f);
                drawList.AddRect(shadow, ShadowColor, CornerRadius + ShadowSize * 0.5f, 0f, default, ShadowSize);
            }
            drawList.AddRect(Bounds, background, CornerRadius, BorderWidth, BorderColor);
        }

        /// <summary>The theme of the attached context, or the global default theme.</summary>
        protected GuiTheme Theme => _context?.Theme ?? GuiContext.DefaultTheme;

        public override string ToString() => Name != null ? $"{GetType().Name} '{Name}'" : GetType().Name;
    }
}

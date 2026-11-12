using Dino_Engine.GUI.Rendering;
using Dino_Engine.GUI.Text;
using Dino_Engine.GUI.Widgets;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Diagnostics;

namespace Dino_Engine.GUI
{
    /// <summary>
    /// Owns a GUI: the element tree (in layers), input routing, focus, popups, tooltips, fonts and rendering.
    ///
    /// Per frame call <see cref="Update"/> before game logic (so <see cref="WantsMouse"/>/<see cref="WantsKeyboard"/> are
    /// current when gameplay reads input) and <see cref="Render"/> after the scene has been drawn to the back buffer.
    /// Units: everything is in GUI units; one GUI unit is <see cref="Scale"/> window pixels.
    /// </summary>
    public class GuiContext : IDisposable
    {
        private static GuiTheme? _defaultTheme;

        /// <summary>Theme used by widgets created before they are attached to a context. Loaded lazily from system fonts.</summary>
        public static GuiTheme DefaultTheme
        {
            get => _defaultTheme ??= new GuiTheme(SystemFonts.LoadDefaultSans(), SystemFonts.LoadDefaultMono());
            set => _defaultTheme = value;
        }

        private const float DragThreshold = 3f;
        private const float DoubleClickTime = 0.35f;

        private readonly NativeWindow _window;
        private readonly GuiInput _input;
        private readonly GuiRenderer _renderer;
        private readonly GuiDrawList _drawList = new();
        private readonly GlyphAtlas _atlas = new(1024);
        private readonly Dictionary<(FontFace face, int pixels), Font> _fonts = new();
        private readonly GuiElement[] _layers;
        private readonly List<PopupEntry> _popups = new();
        private readonly List<GuiElement> _hoverChain = new();
        private readonly List<GuiElement> _newHoverChain = new();
        private readonly HashSet<MouseButton> _buttonsPressedOnGui = new();
        private readonly Stopwatch _layoutStopwatch = new();
        private readonly Stopwatch _renderStopwatch = new();

        private GuiTheme _theme;
        private float _scale = 1f;
        private bool _layoutDirty = true;
        private Vector2 _screenSize;
        private Vector2i _framebufferSize;
        private Vector2 _mousePosition = new Vector2(-1e6f);
        private bool _mouseAvailable;

        private GuiElement? _hovered;
        private GuiElement? _captured;
        private MouseButton _captureButton;
        private Vector2 _pressPosition;
        private Vector2 _lastDragPosition;
        private bool _dragging;
        private GuiElement? _focused;
        private GuiElement? _lastClickElement;
        private float _lastClickTime = -10f;
        private int _pendingClickCount = 1;

        private GuiElement? _tooltipSource;
        private float _tooltipTimer;
        private readonly GuiElement _tooltipPanel;
        private readonly Label _tooltipLabel;
        private MouseCursor? _currentCursor;

        /// <summary>What a game draws in place of the system's cursors: given the cursor asked for (MouseCursor.Default, Hand and so on) and the interface's scale, it returns the one to show. It's asked every frame, so it should keep what it makes.</summary>
        public Func<MouseCursor, float, MouseCursor>? CursorStyle { get; set; }

        private record PopupEntry(GuiElement Popup, GuiElement? Owner, Action? OnClosed);

        public GuiContext(NativeWindow window, GuiTheme? theme = null)
        {
            _window = window;
            _input = new GuiInput(window);
            _renderer = new GuiRenderer();
            _theme = theme ?? DefaultTheme;

            if (window.TryGetCurrentMonitorScale(out float monitorScaleX, out _) && monitorScaleX > 0f) _scale = Math.Clamp(monitorScaleX, 0.5f, 4f);

            _layers = new GuiElement[3];
            for (int i = 0; i < _layers.Length; i++)
            {
                _layers[i] = new GuiElement { HitTest = HitTestMode.PassThrough, Name = i switch { 0 => "Root", 1 => "Popups", _ => "Tooltips" } };
                _layers[i].SetContextRecursive(this);
            }
            _layers[2].HitTest = HitTestMode.None;

            _tooltipPanel = new GuiElement
            {
                Name = "Tooltip",
                Padding = new Thickness(8, 5),
                BackgroundColor = _theme.TooltipBackground,
                BorderColor = _theme.WindowBorder,
                BorderWidth = 1f,
                CornerRadius = _theme.CornerRadius,
                ShadowSize = 8f,
                MaxWidth = 360f,
                Visible = false,
            };
            _tooltipLabel = _tooltipPanel.Add(new Label("") { Wrap = true, FontSize = _theme.SmallFontSize });
            _layers[2].Add(_tooltipPanel);

            UpdateScreen();
        }

        // =============================================================== public state

        public GuiTheme Theme { get => _theme; set => _theme = value ?? throw new ArgumentNullException(nameof(value)); }

        /// <summary>Main layer for windows, HUDs and panels. Add your elements here.</summary>
        public GuiElement Root => _layers[0];
        /// <summary>Layer above the root for dropdown lists, context menus and other popups.</summary>
        public GuiElement PopupLayer => _layers[1];

        /// <summary>GUI units to window pixels. Defaults to the monitor's content scale (e.g. 1.5 at 150% Windows scaling).</summary>
        public float Scale
        {
            get => _scale;
            set
            {
                float clamped = Math.Clamp(value, 0.5f, 4f);
                if (clamped == _scale) return;
                _scale = clamped;
                UpdateScreen();
                InvalidateLayout();
            }
        }

        /// <summary>Physical framebuffer pixels per GUI unit.</summary>
        public float PixelScale { get; private set; } = 1f;
        public Vector2 ScreenSize => _screenSize;
        public Vector2 MousePosition => _mousePosition;
        public float Time { get; private set; }
        public float TooltipDelay { get; set; } = 0.5f;

        /// <summary>Deepest element under the mouse that blocks input, or null when the mouse is over the game.</summary>
        public GuiElement? HoveredElement => _hovered;
        /// <summary>Element that received the mouse press and holds capture until release.</summary>
        public GuiElement? CapturedElement => _captured;
        public GuiElement? FocusedElement => _focused;
        public bool IsDragging => _dragging;

        /// <summary>True when the mouse is over (or pressed on) the GUI. Gameplay should ignore mouse input then.</summary>
        public bool WantsMouse => _mouseAvailable && (_hovered != null || _captured != null || _buttonsPressedOnGui.Count > 0);
        /// <summary>True when a text field (or other keyboard consumer) is focused. Gameplay should ignore keyboard input then.</summary>
        public bool WantsKeyboard => _focused != null && _focused.WantsKeyboardInput && _focused.IsEnabledInHierarchy;

        public string Clipboard { get => _input.Clipboard; set => _input.Clipboard = value; }
        public KeyModifiers Modifiers => _input.Modifiers;
        public bool IsMouseDown(MouseButton button) => _input.IsMouseButtonDown(button);
        public bool IsKeyDown(Keys key) => _input.IsKeyDown(key);
        public GlyphAtlas Atlas => _atlas;

        // Statistics for debugging the GUI itself.
        public int LastDrawCalls => _renderer.LastDrawCalls;
        public int LastQuadCount => _renderer.LastQuadCount;
        public int LayoutPassCount { get; private set; }
        public double LastLayoutMilliseconds { get; private set; }
        /// <summary>CPU time spent building the draw list last frame.</summary>
        public double LastBuildMilliseconds { get; private set; }
        /// <summary>CPU time spent uploading and issuing GL draw calls last frame.</summary>
        public double LastSubmitMilliseconds { get; private set; }
        public int ElementCount { get; private set; }

        /// <summary>Raised after the GUI has been drawn, before the frame is presented.</summary>
        public event Action<GuiContext>? AfterRender;

        // =============================================================== fonts

        /// <summary>Returns the font for a face at a size in GUI units, rasterized at the physical pixel size.</summary>
        public Font GetFont(FontFace face, float size)
        {
            int pixels = Math.Max(1, (int)MathF.Round(size * PixelScale));
            if (!_fonts.TryGetValue((face, pixels), out Font? font))
            {
                font = new Font(face, pixels, _atlas);
                _fonts[(face, pixels)] = font;
            }
            return font;
        }

        /// <summary>Measures single or multi-line text in GUI units (optionally wrapped).</summary>
        public Vector2 MeasureText(string text, FontFace? face = null, float size = 0f, float maxWidth = float.PositiveInfinity)
        {
            Font font = GetFont(face ?? _theme.Font, size > 0f ? size : _theme.FontSize);
            float wrap = float.IsFinite(maxWidth) ? maxWidth * PixelScale : float.PositiveInfinity;
            var lines = TextLayout.BreakLines(font, text, wrap);
            float width = 0f;
            foreach (var line in lines) width = MathF.Max(width, line.Width);
            return new Vector2(width, lines.Count * font.LineHeight) / PixelScale;
        }

        // =============================================================== layout

        public void InvalidateLayout() => _layoutDirty = true;

        private void UpdateScreen()
        {
            Vector2i client = _window.ClientSize;
            Vector2i framebuffer = _window.FramebufferSize;
            if (client.X <= 0 || client.Y <= 0 || framebuffer.X <= 0 || framebuffer.Y <= 0) return;
            Vector2 screen = new Vector2(client.X, client.Y) / _scale;
            float pixelScale = framebuffer.X / screen.X;
            if (screen != _screenSize || framebuffer != _framebufferSize || pixelScale != PixelScale)
            {
                _screenSize = screen;
                _framebufferSize = framebuffer;
                PixelScale = pixelScale;
                foreach (var layer in _layers)
                {
                    layer.Width = screen.X;
                    layer.Height = screen.Y;
                }
                InvalidateLayout();
            }
        }

        private bool IsMinimized => _window.ClientSize.X <= 0 || _window.ClientSize.Y <= 0 || _window.FramebufferSize.X <= 0;

        /// <summary>Runs the layout pass now if anything changed.</summary>
        public void EnsureLayout()
        {
            if (!_layoutDirty || _screenSize.X <= 0f) return;
            _layoutDirty = false;
            _layoutStopwatch.Restart();
            GuiRect screen = new GuiRect(0f, 0f, _screenSize.X, _screenSize.Y);
            foreach (var layer in _layers)
            {
                layer.Measure(_screenSize, _screenSize);
                layer.Arrange(screen);
            }
            _layoutStopwatch.Stop();
            LastLayoutMilliseconds = _layoutStopwatch.Elapsed.TotalMilliseconds;
            LayoutPassCount++;
        }

        // =============================================================== update

        public void Update(float deltaTime)
        {
            if (IsMinimized)
            {
                _input.ClearEvents();
                return;
            }
            Time += deltaTime;
            UpdateScreen();
            EnsureLayout();
            ValidateReferences();

            _mouseAvailable = _input.IsMouseAvailable;
            _mousePosition = _mouseAvailable ? _input.MousePosition / _scale : new Vector2(-1e6f);
            UpdateHover();

            ProcessEvents();
            ReleaseLostCapture();
            UpdateDrag();

            int elementCount = 0;
            foreach (var layer in _layers) elementCount += layer.UpdateTree(deltaTime);
            ElementCount = elementCount;

            EnsureLayout();
            UpdateHover();
            UpdateTooltip(deltaTime);
            UpdateCursor();
        }

        /// <summary>Drops references to elements that were removed from the tree and releases a capture whose button is no longer down.</summary>
        private void ValidateReferences()
        {
            // Only drop a capture whose element left the tree here. A released button is handled after the
            // queued events (see ReleaseLostCapture): GLFW already reports the button as up while its
            // MouseUp event is still waiting in the queue, and releasing early would swallow the click.
            if (_captured != null && _captured.Context != this)
            {
                ReleaseCapture();
            }
            if (_focused != null && (_focused.Context != this || !IsVisibleInHierarchy(_focused)))
            {
                SetFocus(null);
            }
            for (int i = _popups.Count - 1; i >= 0; i--)
            {
                var entry = _popups[i];
                if (entry.Popup.Context != this || (entry.Owner != null && (entry.Owner.Context != this || !IsVisibleInHierarchy(entry.Owner))))
                {
                    ClosePopupAt(i);
                }
            }
        }

        /// <summary>
        /// Runs after the event queue: if the capture button is no longer down but no MouseUp arrived
        /// (e.g. the window lost focus mid-press), release the capture so the GUI cannot stay stuck.
        /// </summary>
        private void ReleaseLostCapture()
        {
            if (_captured != null && !_input.IsMouseButtonDown(_captureButton)) ReleaseCapture();
            _buttonsPressedOnGui.RemoveWhere(button => !_input.IsMouseButtonDown(button));
        }

        private static bool IsVisibleInHierarchy(GuiElement element)
        {
            for (var current = element; current != null; current = current.Parent)
            {
                if (!current.Visible) return false;
            }
            return true;
        }

        private void UpdateHover()
        {
            _hovered = _mouseAvailable ? HitTestAll(_mousePosition) : null;

            _newHoverChain.Clear();
            for (var current = _hovered; current != null; current = current.Parent) _newHoverChain.Add(current);

            // Leave events deepest first, then enter events outermost first.
            for (int i = 0; i < _hoverChain.Count; i++)
            {
                var element = _hoverChain[i];
                if (_newHoverChain.Contains(element)) continue;
                element.IsHovered = false;
                if (element.Context == this) element.RaiseMouseLeave();
            }
            for (int i = _newHoverChain.Count - 1; i >= 0; i--)
            {
                var element = _newHoverChain[i];
                if (_hoverChain.Contains(element)) continue;
                element.IsHovered = true;
                element.RaiseMouseEnter();
            }
            _hoverChain.Clear();
            _hoverChain.AddRange(_newHoverChain);
        }

        /// <summary>Topmost element under a point (GUI units) that blocks the mouse.</summary>
        public GuiElement? HitTestAll(Vector2 point)
        {
            for (int i = _layers.Length - 1; i >= 0; i--)
            {
                var hit = HitTestTree(_layers[i], point);
                if (hit != null) return hit;
            }
            return null;
        }

        private static GuiElement? HitTestTree(GuiElement element, Vector2 point)
        {
            if (!element.Visible || element.HitTest == HitTestMode.None) return null;
            if (element.ClipChildren && !element.ClipRect.Contains(point))
            {
                return element.HitTestSelf(point) ? element : null;
            }
            var children = element.Children;
            for (int i = children.Count - 1; i >= 0; i--)
            {
                var hit = HitTestTree(children[i], point);
                if (hit != null) return hit;
            }
            return element.HitTestSelf(point) ? element : null;
        }

        // =============================================================== input events

        private void ProcessEvents()
        {
            while (_input.TryDequeue(out GuiInputEvent inputEvent))
            {
                switch (inputEvent.Type)
                {
                    case GuiInputEventType.MouseDown:
                        if (_mouseAvailable) HandleMouseDown(inputEvent);
                        break;
                    case GuiInputEventType.MouseUp:
                        HandleMouseUp(inputEvent);
                        break;
                    case GuiInputEventType.Scroll:
                        if (_mouseAvailable) HandleScroll(inputEvent);
                        break;
                    case GuiInputEventType.KeyDown:
                        HandleKeyDown(inputEvent);
                        break;
                    case GuiInputEventType.TextInput:
                        HandleTextInput(inputEvent);
                        break;
                }
            }
        }

        private void HandleMouseDown(GuiInputEvent inputEvent)
        {
            GuiElement? target = _hovered;
            ClosePopupsOutside(target);

            if (target == null)
            {
                // Clicked into the game world.
                SetFocus(null);
                return;
            }
            _buttonsPressedOnGui.Add(inputEvent.Button);

            for (var current = target; current != null; current = current.Parent)
            {
                if (current.BringToFrontOnPress) current.BringToFront();
            }

            GuiElement? focusTarget = null;
            for (var current = target; current != null; current = current.Parent)
            {
                if (current.Focusable && current.IsEnabledInHierarchy)
                {
                    focusTarget = current;
                    break;
                }
            }
            SetFocus(focusTarget);

            var args = new GuiMouseEventArgs { Target = target, Position = _mousePosition, Button = inputEvent.Button, Modifiers = inputEvent.Modifiers };
            for (var current = target; current != null; current = current.Parent)
            {
                if (!current.IsEnabledInHierarchy) continue;
                current.RaiseMouseDown(args);
                if (args.Handled || current.InternalIsInteractive)
                {
                    if (_captured == null)
                    {
                        _captured = current;
                        _captureButton = inputEvent.Button;
                        _pressPosition = _mousePosition;
                        _lastDragPosition = _mousePosition;
                        _dragging = false;

                        bool isDoubleClick = _lastClickElement == current && Time - _lastClickTime < DoubleClickTime;
                        _pendingClickCount = isDoubleClick ? 2 : 1;
                        _lastClickElement = isDoubleClick ? null : current;
                        _lastClickTime = Time;
                    }
                    break;
                }
            }
        }

        private void HandleMouseUp(GuiInputEvent inputEvent)
        {
            _buttonsPressedOnGui.Remove(inputEvent.Button);
            if (_captured == null || inputEvent.Button != _captureButton) return;

            GuiElement captured = _captured;
            var args = new GuiMouseEventArgs { Target = captured, Position = _mousePosition, Button = inputEvent.Button, Modifiers = inputEvent.Modifiers, ClickCount = _pendingClickCount };
            bool wasDragging = _dragging;
            ReleaseCapture();
            captured.RaiseMouseUp(args);
            if (!wasDragging && _mouseAvailable && captured.Context == this && captured.IsEnabledInHierarchy)
            {
                GuiElement? under = HitTestAll(_mousePosition);
                if (captured.Contains(under)) captured.RaiseClick(args);
            }
        }

        private void ReleaseCapture()
        {
            GuiElement? captured = _captured;
            bool wasDragging = _dragging;
            _captured = null;
            _dragging = false;
            if (wasDragging && captured != null && captured.Context == this)
            {
                captured.RaiseDragEnd(new GuiDragEventArgs { Target = captured, Button = _captureButton, StartPosition = _pressPosition, Position = _mousePosition, Delta = Vector2.Zero });
            }
        }

        private void UpdateDrag()
        {
            if (_captured == null || !_mouseAvailable) return;
            if (!_dragging)
            {
                if (!_captured.InternalWantsDrag || (_mousePosition - _pressPosition).Length < DragThreshold) return;
                _dragging = true;
                _captured.RaiseDragStart(new GuiDragEventArgs { Target = _captured, Button = _captureButton, StartPosition = _pressPosition, Position = _pressPosition, Delta = Vector2.Zero });
                if (_captured == null) return; // a handler may have released capture
            }
            if (_mousePosition != _lastDragPosition)
            {
                var args = new GuiDragEventArgs { Target = _captured, Button = _captureButton, StartPosition = _pressPosition, Position = _mousePosition, Delta = _mousePosition - _lastDragPosition };
                _lastDragPosition = _mousePosition;
                _captured.RaiseDrag(args);
            }
        }

        private void HandleScroll(GuiInputEvent inputEvent)
        {
            if (_hovered == null) return;
            var args = new GuiScrollEventArgs { Target = _hovered, Position = _mousePosition, Delta = inputEvent.Scroll, Modifiers = _input.Modifiers };
            for (var current = _hovered; current != null; current = current.Parent)
            {
                if (!current.IsEnabledInHierarchy) continue;
                current.RaiseScroll(args);
                if (args.Handled) break;
            }
        }

        private void HandleKeyDown(GuiInputEvent inputEvent)
        {
            var args = new GuiKeyEventArgs { Key = inputEvent.Key, Modifiers = inputEvent.Modifiers, IsRepeat = inputEvent.IsRepeat };
            if (_focused != null)
            {
                args.Target = _focused;
                for (var current = _focused; current != null; current = current.Parent)
                {
                    if (!current.IsEnabledInHierarchy) continue;
                    current.RaiseKeyDown(args);
                    if (args.Handled) return;
                }
            }

            if (inputEvent.Key == Keys.Escape)
            {
                if (_popups.Count > 0) ClosePopupAt(_popups.Count - 1);
                else SetFocus(null);
            }
            else if (inputEvent.Key == Keys.Tab && _focused != null)
            {
                MoveFocus((inputEvent.Modifiers & KeyModifiers.Shift) != 0 ? -1 : 1);
            }
        }

        private void HandleTextInput(GuiInputEvent inputEvent)
        {
            if (_focused == null || !_focused.IsEnabledInHierarchy) return;
            _focused.RaiseTextInput(new GuiTextEventArgs { Target = _focused, Codepoint = inputEvent.Codepoint });
        }

        // =============================================================== focus

        public void SetFocus(GuiElement? element)
        {
            if (element != null && element.Context != this) element = null;
            if (_focused == element) return;
            GuiElement? previous = _focused;
            _focused = element;
            if (previous != null && previous.Context == this) previous.RaiseFocusLost();
            if (_focused == element) element?.RaiseFocusGained();
        }

        /// <summary>Moves focus to the next (or previous) focusable, visible, enabled element in tree order.</summary>
        public void MoveFocus(int direction)
        {
            var focusables = new List<GuiElement>();
            foreach (var layer in _layers) CollectFocusables(layer, focusables);
            if (focusables.Count == 0) return;
            int index = _focused != null ? focusables.IndexOf(_focused) : -1;
            int next = index < 0 ? 0 : ((index + direction) % focusables.Count + focusables.Count) % focusables.Count;
            SetFocus(focusables[next]);
        }

        private static void CollectFocusables(GuiElement element, List<GuiElement> result)
        {
            if (!element.Visible || !element.Enabled) return;
            if (element.Focusable) result.Add(element);
            foreach (var child in element.Children) CollectFocusables(child, result);
        }

        internal void OnElementDetached(GuiElement element)
        {
            if (_focused == element)
            {
                _focused = null;
                element.RaiseFocusLost();
            }
            if (_captured == element)
            {
                _captured = null;
                _dragging = false;
            }
            if (_hovered == element) _hovered = null;
            if (_hoverChain.Remove(element)) element.IsHovered = false;
            if (_tooltipSource == element) _tooltipSource = null;
            if (_lastClickElement == element) _lastClickElement = null;
        }

        // =============================================================== popups

        /// <summary>
        /// Shows an element in the popup layer (positioned by its own anchor/X/Y in screen space).
        /// It closes when the user presses outside it and outside its owner, presses Escape, or the owner disappears.
        /// </summary>
        public void OpenPopup(GuiElement popup, GuiElement? owner = null, Action? onClosed = null)
        {
            if (IsPopupOpen(popup)) return;
            PopupLayer.Add(popup);
            _popups.Add(new PopupEntry(popup, owner, onClosed));
        }

        public bool IsPopupOpen(GuiElement popup) => _popups.Any(p => p.Popup == popup);

        public void ClosePopup(GuiElement popup)
        {
            int index = _popups.FindIndex(p => p.Popup == popup);
            if (index >= 0) ClosePopupAt(index);
        }

        public void CloseAllPopups()
        {
            for (int i = _popups.Count - 1; i >= 0; i--) ClosePopupAt(i);
        }

        private void ClosePopupAt(int index)
        {
            if (index < 0 || index >= _popups.Count) return;
            var entry = _popups[index];
            _popups.RemoveAt(index);
            entry.Popup.RemoveFromParent();
            entry.OnClosed?.Invoke();
        }

        private void ClosePopupsOutside(GuiElement? target)
        {
            for (int i = _popups.Count - 1; i >= 0; i--)
            {
                var entry = _popups[i];
                bool inside = target != null && (entry.Popup.Contains(target) || (entry.Owner != null && entry.Owner.Contains(target)));
                if (!inside) ClosePopupAt(i);
            }
        }

        // =============================================================== tooltip & cursor

        private void UpdateTooltip(float deltaTime)
        {
            GuiElement? source = null;
            if (_captured == null)
            {
                for (var current = _hovered; current != null; current = current.Parent)
                {
                    if (!string.IsNullOrEmpty(current.Tooltip))
                    {
                        source = current;
                        break;
                    }
                }
            }

            if (source != _tooltipSource)
            {
                _tooltipSource = source;
                _tooltipTimer = 0f;
                _tooltipPanel.Visible = false;
                return;
            }
            if (source == null) return;

            _tooltipTimer += deltaTime;
            if (_tooltipTimer < TooltipDelay) return;

            _tooltipLabel.Text = source.Tooltip!;
            _tooltipLabel.Color = _theme.Text;
            if (!_tooltipPanel.Visible)
            {
                _tooltipPanel.Visible = true;
                EnsureLayout();
            }
            Vector2 size = _tooltipPanel.DesiredSize;
            float x = _mousePosition.X + 14f;
            float y = _mousePosition.Y + 20f;
            if (x + size.X > _screenSize.X - 4f) x = MathF.Max(4f, _screenSize.X - size.X - 4f);
            if (y + size.Y > _screenSize.Y - 4f) y = MathF.Max(4f, _mousePosition.Y - size.Y - 6f);
            _tooltipPanel.X = x;
            _tooltipPanel.Y = y;
            EnsureLayout();
        }

        private void UpdateCursor()
        {
            if (!_mouseAvailable) return;
            MouseCursor desired = MouseCursor.Default;
            for (var current = _captured ?? _hovered; current != null; current = current.Parent)
            {
                if (current.Cursor != null)
                {
                    desired = current.Cursor;
                    break;
                }
            }
            if (CursorStyle != null) desired = CursorStyle(desired, _scale);
            if (!ReferenceEquals(desired, _currentCursor))
            {
                _window.Cursor = desired;
                _currentCursor = desired;
            }
        }

        // =============================================================== render

        /// <summary>Draws the GUI to the default framebuffer (the back buffer).</summary>
        public void Render()
        {
            if (IsMinimized) return;
            UpdateScreen();
            EnsureLayout();

            _renderStopwatch.Restart();
            _drawList.Begin(new GuiRect(0f, 0f, _screenSize.X, _screenSize.Y), PixelScale);
            foreach (var layer in _layers) layer.DrawTree(_drawList);
            LastBuildMilliseconds = _renderStopwatch.Elapsed.TotalMilliseconds;

            _renderStopwatch.Restart();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            _renderer.Render(_drawList, _screenSize, _framebufferSize);
            LastSubmitMilliseconds = _renderStopwatch.Elapsed.TotalMilliseconds;
            AfterRender?.Invoke(this);
        }

        public void Dispose()
        {
            foreach (var layer in _layers) layer.ClearChildren();
            _input.Dispose();
            _renderer.Dispose();
            _atlas.Dispose();
            _fonts.Clear();
        }
    }
}

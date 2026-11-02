using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Dino_Engine.GUI
{
    public enum GuiInputEventType
    {
        MouseDown,
        MouseUp,
        Scroll,
        KeyDown,
        TextInput,
    }

    public struct GuiInputEvent
    {
        public GuiInputEventType Type;
        public MouseButton Button;
        public Vector2 Scroll;
        public Keys Key;
        public KeyModifiers Modifiers;
        public bool IsRepeat;
        public int Codepoint;
    }

    /// <summary>
    /// Records window input events in arrival order so the GUI never misses a click or keystroke that
    /// starts and ends between two frames, and so typed text keeps its order.
    /// </summary>
    public class GuiInput : IDisposable
    {
        private readonly NativeWindow _window;
        private readonly Queue<GuiInputEvent> _events = new();

        public GuiInput(NativeWindow window)
        {
            _window = window;
            _window.MouseDown += OnMouseDown;
            _window.MouseUp += OnMouseUp;
            _window.MouseWheel += OnMouseWheel;
            _window.KeyDown += OnKeyDown;
            _window.TextInput += OnTextInput;
        }

        public NativeWindow Window => _window;

        /// <summary>Mouse position in window pixels (client area).</summary>
        public Vector2 MousePosition => _window.MousePosition;

        /// <summary>False while the cursor is grabbed by the game (e.g. mouse look) or the window is unfocused.</summary>
        public bool IsMouseAvailable => _window.CursorState != CursorState.Grabbed && _window.IsFocused;

        public bool IsMouseButtonDown(MouseButton button) => _window.IsMouseButtonDown(button);
        public bool IsKeyDown(Keys key) => _window.IsKeyDown(key);

        public KeyModifiers Modifiers
        {
            get
            {
                KeyModifiers modifiers = 0;
                if (_window.IsKeyDown(Keys.LeftShift) || _window.IsKeyDown(Keys.RightShift)) modifiers |= KeyModifiers.Shift;
                if (_window.IsKeyDown(Keys.LeftControl) || _window.IsKeyDown(Keys.RightControl)) modifiers |= KeyModifiers.Control;
                if (_window.IsKeyDown(Keys.LeftAlt) || _window.IsKeyDown(Keys.RightAlt)) modifiers |= KeyModifiers.Alt;
                return modifiers;
            }
        }

        public string Clipboard
        {
            get
            {
                try { return _window.ClipboardString ?? ""; }
                catch (Exception) { return ""; } // GLFW throws if the clipboard holds non-text data
            }
            set
            {
                try { _window.ClipboardString = value; }
                catch (Exception) { /* clipboard unavailable */ }
            }
        }

        public bool TryDequeue(out GuiInputEvent inputEvent) => _events.TryDequeue(out inputEvent);

        public void ClearEvents() => _events.Clear();

        private void OnMouseDown(MouseButtonEventArgs e) => _events.Enqueue(new GuiInputEvent { Type = GuiInputEventType.MouseDown, Button = e.Button, Modifiers = e.Modifiers });
        private void OnMouseUp(MouseButtonEventArgs e) => _events.Enqueue(new GuiInputEvent { Type = GuiInputEventType.MouseUp, Button = e.Button, Modifiers = e.Modifiers });
        private void OnMouseWheel(MouseWheelEventArgs e) => _events.Enqueue(new GuiInputEvent { Type = GuiInputEventType.Scroll, Scroll = e.Offset });
        private void OnKeyDown(KeyboardKeyEventArgs e) => _events.Enqueue(new GuiInputEvent { Type = GuiInputEventType.KeyDown, Key = e.Key, Modifiers = e.Modifiers, IsRepeat = e.IsRepeat });
        private void OnTextInput(TextInputEventArgs e) => _events.Enqueue(new GuiInputEvent { Type = GuiInputEventType.TextInput, Codepoint = e.Unicode });

        public void Dispose()
        {
            _window.MouseDown -= OnMouseDown;
            _window.MouseUp -= OnMouseUp;
            _window.MouseWheel -= OnMouseWheel;
            _window.KeyDown -= OnKeyDown;
            _window.TextInput -= OnTextInput;
        }
    }
}

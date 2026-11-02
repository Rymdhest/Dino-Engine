using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Dino_Engine.GUI
{
    /// <summary>Base for routed GUI events. Mouse, scroll and key events bubble from the target up through its parents until Handled.</summary>
    public class GuiEventArgs
    {
        /// <summary>The element the event was originally dispatched to (deepest hit element, or the focused element).</summary>
        public GuiElement Target { get; internal set; } = null!;
        /// <summary>Set to true to stop the event from bubbling further up.</summary>
        public bool Handled { get; set; }
    }

    public class GuiMouseEventArgs : GuiEventArgs
    {
        /// <summary>Mouse position in GUI units.</summary>
        public Vector2 Position { get; internal set; }
        public MouseButton Button { get; internal set; }
        public KeyModifiers Modifiers { get; internal set; }
        /// <summary>1 for a single click, 2 for a double click.</summary>
        public int ClickCount { get; internal set; } = 1;
    }

    public class GuiScrollEventArgs : GuiEventArgs
    {
        public Vector2 Position { get; internal set; }
        /// <summary>Wheel offset; positive Y means scrolled up / away from the user.</summary>
        public Vector2 Delta { get; internal set; }
        public KeyModifiers Modifiers { get; internal set; }
    }

    public class GuiDragEventArgs : GuiEventArgs
    {
        public MouseButton Button { get; internal set; }
        /// <summary>Mouse position where the drag started (GUI units).</summary>
        public Vector2 StartPosition { get; internal set; }
        /// <summary>Current mouse position (GUI units).</summary>
        public Vector2 Position { get; internal set; }
        /// <summary>Movement since the previous drag event (GUI units).</summary>
        public Vector2 Delta { get; internal set; }
        public Vector2 TotalDelta => Position - StartPosition;
    }

    public class GuiKeyEventArgs : GuiEventArgs
    {
        public Keys Key { get; internal set; }
        public KeyModifiers Modifiers { get; internal set; }
        public bool IsRepeat { get; internal set; }
        public bool Control => (Modifiers & KeyModifiers.Control) != 0;
        public bool Shift => (Modifiers & KeyModifiers.Shift) != 0;
        public bool Alt => (Modifiers & KeyModifiers.Alt) != 0;
    }

    public class GuiTextEventArgs : GuiEventArgs
    {
        public int Codepoint { get; internal set; }
        public string Text => char.ConvertFromUtf32(Codepoint);
    }
}

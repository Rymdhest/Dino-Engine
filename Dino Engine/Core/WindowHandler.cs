
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Dino_Engine.Core
{
    /// <summary>How the game sits on the screen.</summary>
    public enum DisplayMode
    {
        Windowed,
        Borderless,     // A window the size of the monitor, with nothing round it.
        Fullscreen,     // The monitor itself, at a size of its own.
    }

    public class WindowHandler : GameWindow
    {

        public WindowHandler(GameWindowSettings gws, NativeWindowSettings nws) : base(gws, nws)
        {
            refreshViewport();
        }
        public void refreshViewport()
        {
            GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
        }

        public void onResize(ResizeEventArgs eventArgs)
        {
            refreshViewport();
        }

        /// <summary>The sizes the monitor it's on can show, smallest first, and none so small that the game can't be used.</summary>
        public List<Vector2i> Resolutions()
        {
            var sizes = new List<Vector2i>();
            foreach (VideoMode mode in Monitors.GetMonitorFromWindow(this).SupportedVideoModes)
            {
                var size = new Vector2i(mode.Width, mode.Height);
                if (size.X >= 1024 && size.Y >= 720 && !sizes.Contains(size)) sizes.Add(size);
            }
            sizes.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
            return sizes;
        }

        /// <summary>Sits the game on the screen. Everything that depends on the size follows from the resize this makes.</summary>
        /// <param name="size">What a window or a screen of its own should be. Borderless is always the monitor's.</param>
        public unsafe void ApplyDisplay(DisplayMode mode, Vector2i size)
        {
            MonitorInfo monitor = Monitors.GetMonitorFromWindow(this);
            Vector2i native = new Vector2i(monitor.HorizontalResolution, monitor.VerticalResolution);
            Vector2i corner = monitor.ClientArea.Min;

            // Out of a screen of its own first, whatever comes next. It's left by hand, as it's entered by hand, and OpenTK only remembers where a window was for what it did itself.
            if (GLFW.GetWindowMonitor(WindowPtr) != null) GLFW.SetWindowMonitor(WindowPtr, null, corner.X + 40, corner.Y + 40, native.X - 80, native.Y - 80, 0);

            switch (mode)
            {
                case DisplayMode.Fullscreen:
                    // A window can be any size, and a screen only the sizes it has.
                    if (!Resolutions().Contains(size)) size = native;
                    GLFW.SetWindowMonitor(WindowPtr, monitor.Handle.ToUnsafePtr<OpenTK.Windowing.GraphicsLibraryFramework.Monitor>(), 0, 0, size.X, size.Y, GLFW.DontCare);
                    break;
                case DisplayMode.Borderless:
                    WindowState = WindowState.Normal;
                    WindowBorder = WindowBorder.Hidden;
                    Location = corner;
                    ClientSize = native;
                    break;
                default:
                    WindowState = WindowState.Normal;
                    WindowBorder = WindowBorder.Resizable;
                    ClientSize = Vector2i.ComponentMin(size, native);
                    Location = corner + (native - ClientSize) / 2;
                    break;
            }
        }

        public void setMouseGrabbed(bool setTo)
        {
            if (setTo)
            {
                base.CursorState = CursorState.Grabbed;
            }
            else
            {
                base.CursorState = CursorState.Normal;
            }
        }
    }

}

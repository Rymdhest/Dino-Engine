﻿
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace Dino_Engine.Core
{
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

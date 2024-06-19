using Dino_Engine.Core;
using OpenTK.Windowing.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.Rendering.Renderers
{
    public abstract class Renderer
    {
        public Renderer()
        {
            Engine.RenderEngine.Renderers.Add(this);
        }
        public abstract void OnResize(ResizeEventArgs eventArgs);
        public abstract void Update();
    }
}

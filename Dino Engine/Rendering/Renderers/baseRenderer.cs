using Dino_Engine.Core;
using Dino_Engine.ECS;
using Dino_Engine.ECS.ECS_Architecture;
using OpenTK.Windowing.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.Rendering.Renderers
{
    public abstract class baseRenderer
    {
        public baseRenderer()
        {
            Engine.RenderEngine.Renderers.Add(this);
        }
        public virtual void OnResize(ResizeEventArgs eventArgs) { }
        public virtual void Update() { }
        public abstract void CleanUp();
    }
}

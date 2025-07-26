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
    public abstract class BaseRenderer
    {
        public string Name { get; }
        public bool trackPerformance;

        public BaseRenderer(string name, bool trackPerformance = true)
        {
            Name = name;
            Engine.RenderEngine.Renderers.Add(this);
            this.trackPerformance = trackPerformance;
        }
        public virtual void OnResize(ResizeEventArgs eventArgs) { }
        public virtual void Update() { }
        public abstract void CleanUp();
    }
}

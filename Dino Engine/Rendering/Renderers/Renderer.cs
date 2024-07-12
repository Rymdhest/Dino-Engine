using Dino_Engine.Core;
using Dino_Engine.ECS;
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
        public void RenderPass(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
            Engine.PerformanceMonitor.startTask(this.GetType().Name);
            Prepare(eCSEngine, renderEngine);
            Render(eCSEngine, renderEngine);
            Finish(eCSEngine, renderEngine);
            Engine.PerformanceMonitor.finishTask(this.GetType().Name);
        }
        internal abstract void Prepare(ECSEngine eCSEngine, RenderEngine renderEngine);
        internal abstract void Finish(ECSEngine eCSEngine, RenderEngine renderEngine);
        internal abstract void Render(ECSEngine eCSEngine, RenderEngine renderEngine);
        public abstract void OnResize(ResizeEventArgs eventArgs);
        public abstract void Update();
        public abstract void CleanUp();
    }
}

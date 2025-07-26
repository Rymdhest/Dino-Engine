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
    public abstract class RenderPassRenderer : BaseRenderer
    {
        public RenderPassRenderer(string name) : base(name)
        {
        }
        public void RenderPass(RenderEngine renderEngine)
        {
            if (trackPerformance) Engine.PerformanceMonitor.startGPUTask(Name);
            if (trackPerformance) Engine.PerformanceMonitor.startCPUTask(Name+" RENDER");
            Prepare(renderEngine);
            Render(renderEngine);
            Finish(renderEngine);
            if (trackPerformance) Engine.PerformanceMonitor.finishGPUTask(Name);
            if (trackPerformance) Engine.PerformanceMonitor.finishCPUTask(Name+" RENDER");
        }
        internal abstract void Prepare(RenderEngine renderEngine);
        internal abstract void Finish(RenderEngine renderEngine);
        internal abstract void Render(RenderEngine renderEngine);
    }
}

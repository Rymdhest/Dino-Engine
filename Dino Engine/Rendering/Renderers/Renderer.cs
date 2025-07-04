﻿using Dino_Engine.Core;
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
    public abstract class Renderer
    {
        public Renderer()
        {
            Engine.RenderEngine.Renderers.Add(this);
        }
        public void RenderPass(RenderEngine renderEngine)
        {
            Engine.PerformanceMonitor.startTask(this.GetType().Name);
            Prepare(renderEngine);
            Render(renderEngine);
            Finish(renderEngine);
            Engine.PerformanceMonitor.finishTask(this.GetType().Name);
        }
        internal abstract void Prepare(RenderEngine renderEngine);
        internal abstract void Finish(RenderEngine renderEngine);
        internal abstract void Render(RenderEngine renderEngine);
        public virtual void OnResize(ResizeEventArgs eventArgs) { }
        public virtual void Update() { }
        public abstract void CleanUp();
    }
}

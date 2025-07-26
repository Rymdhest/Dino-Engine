using Dino_Engine.Core;
using Dino_Engine.ECS;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Rendering.Renderers.Lighting;
using OpenTK.Windowing.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.Rendering.Renderers
{
    public abstract class GeometryCommandDrivenRenderer<T> : BaseRenderer
    {

        protected List<T> geometryCommands = new List<T>();
        protected List<(T, Shadow)> shadowCommands = new List<(T, Shadow)>();
        public GeometryCommandDrivenRenderer(string name) : base(name)
        {
           
        }

        public void GeometryRenderPass(RenderEngine renderEngine)
        {
            string taskName = Name + " Geometry";
            if (trackPerformance) Engine.PerformanceMonitor.startGPUTask(taskName);
            if (trackPerformance) Engine.PerformanceMonitor.startCPUTask(taskName+" RENDER");
            PrepareGeometry(renderEngine);

            foreach (var command in geometryCommands)
            {
                PerformGeometryCommand(command, renderEngine);
            }
            geometryCommands.Clear();

            FinishGeometry(renderEngine);
            if (trackPerformance) Engine.PerformanceMonitor.finishGPUTask(taskName);
            if (trackPerformance) Engine.PerformanceMonitor.finishCPUTask(taskName+" RENDER");
        }
        public void ShadowRenderPass(RenderEngine renderEngine)
        {
            string taskName = Name + " Shadow";
            if (trackPerformance) Engine.PerformanceMonitor.startGPUTask(taskName);
            if (trackPerformance) Engine.PerformanceMonitor.startCPUTask(taskName+" RENDER");
            PrepareShadow(renderEngine);

            foreach (var command in shadowCommands)
            {
                PerformShadowCommand(command.Item1, command.Item2, renderEngine);
            }
            shadowCommands.Clear();

            FinishShadow(renderEngine);
            if (trackPerformance) Engine.PerformanceMonitor.finishGPUTask(taskName);
            if (trackPerformance) Engine.PerformanceMonitor.finishCPUTask(taskName+" RENDER");
        }

        internal abstract void PrepareGeometry(RenderEngine renderEngine);
        internal abstract void FinishGeometry(RenderEngine renderEngine);
        internal abstract void PrepareShadow(RenderEngine renderEngine);
        internal abstract void FinishShadow(RenderEngine renderEngine);

        internal abstract void PerformGeometryCommand(T command, RenderEngine renderEngine);
        internal abstract void PerformShadowCommand(T command, Shadow shadow, RenderEngine renderEngine);

        public void SubmitGeometryCommand(T Command)
        {
            geometryCommands.Add(Command);
        }
        public void SubmitShadowCommand(T Command, Shadow shadow)
        {
            shadowCommands.Add((Command, shadow));
        }
    }
}

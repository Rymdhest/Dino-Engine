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
    public abstract class GeometryCommandDrivenRenderer<T> : baseRenderer
    {

        protected List<T> geometryCommands = new List<T>();
        protected List<(T, Shadow)> shadowCommands = new List<(T, Shadow)>();
        public GeometryCommandDrivenRenderer() : base()
        {
           
        }

        public void GeometryRenderPass(RenderEngine renderEngine)
        {
            Engine.PerformanceMonitor.startTask(this.GetType().Name);
            PrepareGeometry(renderEngine);

            foreach (var command in geometryCommands)
            {
                PerformGeometryCommand(command, renderEngine);
            }
            geometryCommands.Clear();

            FinishGeometry(renderEngine);
            Engine.PerformanceMonitor.finishTask(this.GetType().Name);
        }
        public void ShadowRenderPass(RenderEngine renderEngine)
        {
            Engine.PerformanceMonitor.startTask(this.GetType().Name);
            PrepareShadow(renderEngine);

            foreach (var command in shadowCommands)
            {
                PerformShadowCommand(command.Item1, command.Item2, renderEngine);
            }
            shadowCommands.Clear();

            FinishShadow(renderEngine);
            Engine.PerformanceMonitor.finishTask(this.GetType().Name);
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

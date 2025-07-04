using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Rendering.Renderers.Lighting;
using Dino_Engine.Rendering.Renderers.PosGeometry;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;

namespace Dino_Engine.ECS.Systems
{
    public class CameraSystem : SystemBase
    {
        public CameraSystem()
            : base(new BitMask(
                typeof(PositionComponent),
                typeof(RotationComponent),
                typeof(LocalToWorldMatrixComponent),
                typeof(PerspectiveProjectionComponent),
                typeof(ViewMatrixComponent)
            ))
        {
        }
        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            Engine.RenderEngine.context.viewPos = entity.Get<PositionComponent>().value;
            Engine.RenderEngine.context.viewMatrix = entity.Get<ViewMatrixComponent>().value;
            Engine.RenderEngine.context.invViewMatrix = Matrix4.Invert(Engine.RenderEngine.context.viewMatrix);
            Engine.RenderEngine.context.projectionMatrix = entity.Get<PerspectiveProjectionComponent>().ProjectionMatrix;
            Engine.RenderEngine.context.invProjectionMatrix = Matrix4.Invert(Engine.RenderEngine.context.projectionMatrix);
        }

        protected override void ResizeEntity(EntityView entity, ECSWorld world, ResizeEventArgs args)
        {
            var projectionComponent = entity.Get<PerspectiveProjectionComponent>();

            projectionComponent.CalcAspect(args.Size);
            projectionComponent.RebuildMatrix();

            entity.Set(projectionComponent);
        }
    }
}

using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Rendering.Renderers.Geometry;
using Dino_Engine.Rendering.Renderers.Lighting;

namespace Dino_Engine.ECS.Systems
{
    /// <summary>
    /// Draws the trees of a <see cref="FarForestComponent"/>, all in one call, and gives them to the widest of the sun's shadow maps, which is the one that reaches them: a wood with no
    /// shadows in it is a row of cut-outs. Nothing's culled, as a few thousand quads cost less than looking through them. Runs after the sun's maps have been aimed for the frame
    /// (<see cref="DirectionalCascadeShadowSystem"/>), as what it submits carries where the map looks from.
    /// </summary>
    public class FarForestSystem : SystemBase
    {
        public FarForestSystem()
            : base(new BitMask(typeof(FarForestComponent)))
        {
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            ImposterInstanceData[] trees = entity.Get<FarForestComponent>().Trees;
            if (trees == null || trees.Length == 0) return;

            var command = new ImposterRenderCommand { instances = trees };
            Engine.RenderEngine._imposterRenderer.SubmitGeometryCommand(command);

            if (!Engine.RenderEngine.Lighting.Sun || !world.sun.IsValid() || !world.HasComponent<DirectionalCascadingShadowComponent>(world.sun)) return;
            Shadow[] cascades = world.GetComponent<DirectionalCascadingShadowComponent>(world.sun).cascades;
            // With only the game's own map there's nothing that far off to shade. A wide map isn't drawn every frame.
            if (cascades.Length > 1 && DirectionalCascadeShadowSystem.Redrawn(cascades[^1])) Engine.RenderEngine._imposterRenderer.SubmitShadowCommand(command, cascades[^1]);
        }
    }
}

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
    public class SkySYstem : SystemBase
    {
        public SkySYstem()
            : base(new BitMask(
                typeof(SkyTag),
                typeof(ColorComponent)
            ))
        {
        }
        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            Engine.RenderEngine.context.skyColour = entity.Get<ColorComponent>().value.ToVector3();
        }
    }
}

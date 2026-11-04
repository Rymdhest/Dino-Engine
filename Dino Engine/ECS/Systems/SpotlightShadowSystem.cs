using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Rendering.Renderers.Lighting;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    public class SpotlightShadowSystem : SystemBase
    {
        private readonly LocalShadowCasters _casters = new();

        public SpotlightShadowSystem()
            : base(new BitMask(
                typeof(SpotLightComponent),
                typeof(LocalToWorldMatrixComponent),
                typeof(LightRangeComponent),
                typeof(DirectionNormalizedComponent),
                typeof(SpotlightShadowComponent)))
        {
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            var direction = entity.Get<DirectionNormalizedComponent>().value;
            var shadowComp = entity.Get<SpotlightShadowComponent>();
            var lightPos = entity.Get<LocalToWorldMatrixComponent>().value.ExtractTranslation();

            shadowComp.shadow.lightViewMatrix = CreateLightViewMatrix(direction, lightPos);
            shadowComp.shadow.shadowFrameBuffer.ClearDepth();
            entity.Set(shadowComp);

            Shadow shadow = shadowComp.shadow;
            _casters.Gather(world, lightPos, entity.Get<LightRangeComponent>().Radius);
            _casters.Submit(new Frustum(shadow.lightViewMatrix * shadow.shadowProjectionMatrix), shadow);
        }

        private static Matrix4 CreateLightViewMatrix(Vector3 direction, Vector3 lightPos)
        {
            Vector3 up = MathF.Abs(Vector3.Dot(direction, Vector3.UnitY)) > 0.99f
                ? Vector3.UnitZ
                : Vector3.UnitY;

            return Matrix4.LookAt(lightPos, lightPos + direction, up);
        }
    }
}

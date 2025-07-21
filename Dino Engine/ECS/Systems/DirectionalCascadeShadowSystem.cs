using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Rendering.Renderers.Geometry;
using Dino_Engine.Rendering.Renderers.Lighting;
using Dino_Engine.Rendering.Renderers.PosGeometry;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    public class DirectionalCascadeShadowSystem : SystemBase
    {
        public DirectionalCascadeShadowSystem()
            : base(new BitMask(
                typeof(DirectionalLightTag),
                typeof(DirectionNormalizedComponent),
                typeof(DirectionalCascadingShadowComponent)))
        {
        }
        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            var direction = entity.Get<DirectionNormalizedComponent>().value;
            var shadow = entity.Get<DirectionalCascadingShadowComponent>();
            var cameraPos = world.GetComponent<LocalToWorldMatrixComponent>(world.Camera).value.ExtractTranslation();
            for (int i = 0; i<shadow.cascades.Length; i++)
            {
                shadow.cascades[i].lightViewMatrix = CreateLightViewMatrix(direction, cameraPos, shadow.cascades[i].projectionSize);
            }
            DirectionalShadowRenderCommand command = new DirectionalShadowRenderCommand();
            command.Cascades = new ShadowCascadeCommand[shadow.cascades.Length];

            var shadowCasters = world.QueryEntities(new BitMask(
                typeof(ModelComponent),
                typeof(ModelRenderTag),
                typeof(LocalToWorldMatrixComponent)), BitMask.Empty);
            ModelRenderCommand[] modelRenderCommands = new ModelRenderCommand[shadowCasters.Count];
            for (int i = 0; i <shadowCasters.Count; i++)
            {
                var LocalToWorldMatrix = world.GetComponent<LocalToWorldMatrixComponent>(shadowCasters[i]).value;
                var glModel = world.GetComponent<ModelComponent>(shadowCasters[i]).model;

                var ModelCommand = new ModelRenderCommand();
                ModelCommand.model = glModel;
                ModelCommand.localToWorldMatrices = [LocalToWorldMatrix];
                modelRenderCommands[i] = ModelCommand;
            }

            for (int i = 0; i < shadow.cascades.Length; i++)
            {
                command.Cascades[i].cascade = shadow.cascades[i];
                command.Cascades[i].modelCommands = modelRenderCommands;
            }

            Engine.RenderEngine._shadowCascadeMapRenderer.SubmitCommand(command);

            entity.Set(shadow);
        }

        private static Matrix4 CreateLightViewMatrix(Vector3 direction, Vector3 center, float size)
        {
            direction = Vector3.Normalize(direction);

            // Pick an "up" vector that's safe; usually Y up, but if light direction is vertical, use another up.
            Vector3 up = MathF.Abs(Vector3.Dot(direction, Vector3.UnitY)) > 0.99f
                ? Vector3.UnitZ  // fallback up if direction is nearly vertical
                : Vector3.UnitY;

            // The eye position: move "back" along the light direction from the center of the cascade
            Vector3 lightPos = center - direction * size/2f; // 100 units back; adjust based on cascade size

            return Matrix4.LookAt(lightPos, center, up);
        }

    }
}

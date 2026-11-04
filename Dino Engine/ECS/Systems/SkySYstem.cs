using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Rendering.Renderers.Lighting;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    /// <summary>
    /// The light that isn't any one light. The sky shines down from the colour of its entity, the ground throws back what falls
    /// on it, and the air scatters a little of the sun everywhere. Together they are all there is in the shade.
    /// </summary>
    public class SkySYstem : SystemBase
    {
        // Daylight, as what a white surface facing up shines at. Particles that are lit, not glowing, are this bright in it.
        private const float FullDaylight = 4f;

        public SkySYstem()
            : base(new BitMask(
                typeof(SkyTag),
                typeof(ColorComponent)
            ))
        {
        }
        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            LightingSettings lighting = Engine.RenderEngine.Lighting;
            Vector3 sky = lighting.Sky ? entity.Get<ColorComponent>().value.ToVector3() : Vector3.Zero;
            Vector3 sun = Sun(world);
            float sunHeight = sun == Vector3.Zero ? 0f : MathF.Max(-world.GetComponent<DirectionNormalizedComponent>(world.sun).value.Y, 0f);
            Vector3 sunOnGround = sun * (sunHeight / MathF.PI);
            Vector3 scattered = sun * lighting.SunFill;

            Engine.RenderEngine.context.skyColour = sky;
            Engine.RenderEngine.context.daylight = Vector3.Dot(sky + sunOnGround, new Vector3(0.2126f, 0.7152f, 0.0722f)) / FullDaylight;
            Engine.RenderEngine._ambientLightRenderer.SubmitCommand(new AmbientLightRenderCommand
            {
                sky = sky * lighting.SkyFill + scattered,
                ground = (sky + sunOnGround) * lighting.GroundAlbedo + scattered,
            });
        }

        private static Vector3 Sun(ECSWorld world)
        {
            if (!Engine.RenderEngine.Lighting.Sun || !world.HasComponent<ColorComponent>(world.sun)) return Vector3.Zero;
            return world.GetComponent<ColorComponent>(world.sun).value.ToVector3();
        }
    }
}

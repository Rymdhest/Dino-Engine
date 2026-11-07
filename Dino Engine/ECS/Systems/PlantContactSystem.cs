using Dino_Engine.Animation;
using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Rendering.Renderers.Simulation;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    /// <summary>
    /// Tells the plants what's going through them. A body parts them, and takes more of them along the faster it goes. Its feet are what pressed the ground, so they're
    /// what leaves a mark in it, where they really landed: a body alone is a round hole moving through a field.
    /// </summary>
    public class PlantContactSystem : SystemBase
    {
        private const float DragSpeed = 5f;     // Metres a second at which a body takes plants along as much as it parts them.
        private const float FootShare = 0.34f;  // How wide a foot's mark is, of the body's.
        private const float FootLean = 0.3f;    // Radians a foot pushes what's beside it.
        private const float FootPress = 1.15f;  // Radians a foot presses what's under it.

        public PlantContactSystem()
            : base(new BitMask(typeof(PositionComponent), typeof(PlantContactComponent)))
        {
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            Vector2 position = entity.Get<PositionComponent>().value.Xz;
            var contact = entity.Get<PlantContactComponent>();

            Vector2 velocity = entity.Has<VelocityComponent>() ? entity.Get<VelocityComponent>().value.Xz : Vector2.Zero;
            float speed = velocity.Length;
            Engine.RenderEngine._windRenderer.AddVegetationContact(new VegetationContact
            {
                center = position,
                previous = position - velocity * deltaTime,
                radius = contact.radius,
                lean = contact.lean,
                press = contact.press,
                drag = speed / (speed + DragSpeed),
            });

            if (!entity.Has<AnimatorComponent>()) return;
            foreach (Foot foot in entity.Get<AnimatorComponent>().Animator.Gait.Feet)
            {
                if (foot.Swinging) continue;
                Engine.RenderEngine._windRenderer.AddVegetationContact(new VegetationContact
                {
                    center = foot.At.Xz,
                    previous = foot.At.Xz,
                    radius = contact.radius * FootShare,
                    lean = FootLean,
                    press = FootPress,
                });
            }
        }
    }
}

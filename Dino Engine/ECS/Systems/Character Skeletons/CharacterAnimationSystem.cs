using Dino_Engine.Animation;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.World;

namespace Dino_Engine.ECS.Systems
{
    /// <summary>Gives every body what it needs to know about the world, and lets it get on with it. A game only says what's going on, through <see cref="CharacterAnimator.Orders"/>.</summary>
    public class CharacterAnimationSystem : SystemBase
    {
        private readonly FlatGround _flat = new FlatGround();
        private WorldGround? _ground;

        public CharacterAnimationSystem()
            : base(new BitMask(typeof(AnimatorComponent), typeof(PositionComponent), typeof(RotationComponent), typeof(ScaleComponent)))
        {
        }

        internal override void UpdateInternal(ECSWorld world, float deltaTime)
        {
            IGround ground = _flat;
            if (world.TryGetSingleton<WorldDataSingleton>(out Entity data))
            {
                WorldData current = world.GetComponent<WorldDataSingleton>(data).World;
                if (_ground?.World != current) _ground = new WorldGround(current);
                ground = _ground;
            }

            foreach (var archetype in world.QueryArchetypes(WithMask, WithoutMask))
            {
                var animators = archetype.GetComponentArray<AnimatorComponent>();
                var positions = archetype.GetComponentArray<PositionComponent>();
                var rotations = archetype.GetComponentArray<RotationComponent>();
                var scales = archetype.GetComponentArray<ScaleComponent>();

                int count = archetype.EntityCount;
                for (int i = 0; i < count; i++)
                {
                    CharacterAnimator animator = animators[i].Animator;
                    animator.Orders.Position = positions[i].value;
                    animator.Orders.Yaw = Frames.YawOf(rotations[i].quaternion);
                    animator.Orders.Scale = scales[i].value.Y;
                    animator.Step(animator.Orders, deltaTime, ground);
                }
            }
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime) { }
    }
}

using Dino_Engine.Animation;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;

namespace Dino_Engine.ECS.Systems
{
    public class CharacterAnimationControlSystem : SystemBase
    {
        public CharacterAnimationControlSystem()
            : base(new BitMask(typeof(AnimationComponent), typeof(VelocityComponent)))
        {
            Priority = -3; // Run before the skeletal animation evaluation
        }

        internal override void UpdateInternal(ECSWorld world, float deltaTime)
        {
            foreach (var archetype in world.QueryArchetypes(WithMask, WithoutMask))
            {
                var animArray = archetype.GetComponentArray<AnimationComponent>();
                var velArray = archetype.GetComponentArray<VelocityComponent>();

                int count = archetype.EntityCount;
                for (int i = 0; i < count; i++)
                {
                    // 1. Get a copy of the struct from the component array
                    var animComp = animArray[i];
                    var velocity = velArray[i];

                    float speed = velocity.value.Length;
                    bool isMoving = speed > 0.0001f;

                    // 2. Check if the Run layer is active
                    var runLayer = animComp.Layers.Find(l => l.ClipId == AnimationId.Run);

                    if (isMoving && runLayer == null)
                    {
                        // Add Run layer on top of Idle
                        animComp.PlayLayer(AnimationId.Run, loop:true, speed: speed*0.1f);
                    }
                    else if (!isMoving && runLayer != null)
                    {
                        // Remove Run layer so Idle takes over again
                        animComp.Layers.Remove(runLayer);
                    }

                    // 3. Write the modified struct back to the component array
                    animArray[i] = animComp;
                }
            }
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime) { }
    }
}
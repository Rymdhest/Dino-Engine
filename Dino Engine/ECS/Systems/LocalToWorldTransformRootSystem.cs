using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    public class LocalToWorldTransformRootSystem : SystemBase
    {
        public LocalToWorldTransformRootSystem()
            : base(new BitMask(typeof(LocalToWorldMatrixComponent), typeof(PositionComponent)),
                  new BitMask(typeof(ParentComponent)))
        { }

        // We override UpdateInternal to avoid the per-entity virtual call overhead.
        // This allows us to access the Archetype's raw component arrays.
        internal override void UpdateInternal(ECSWorld world, float deltaTime)
        {
            foreach (var archetype in world.QueryArchetypes(WithMask, WithoutMask))
            {
                // Retrieve raw component arrays directly from the archetype
                var posArray = archetype.GetComponentArray<PositionComponent>();
                var rotArray = archetype.GetComponentArray<RotationComponent>();
                var scaleArray = archetype.GetComponentArray<ScaleComponent>();
                var matrixArray = archetype.GetComponentArray<LocalToWorldMatrixComponent>();

                bool hasRotation = archetype.Has<RotationComponent>();
                bool hasScale = archetype.Has<ScaleComponent>();

                int count = archetype.EntityCount;

                for (int i = 0; i < count; i++)
                {
                    // Access arrays directly by index - no lookups, no branching per entity
                    var pos = posArray[i].value;

                    Quaternion rot = hasRotation ? rotArray[i].quaternion : Quaternion.Identity;
                    Vector3 scale = hasScale ? scaleArray[i].value : Vector3.One;

                    matrixArray[i] = new LocalToWorldMatrixComponent
                    {
                        value = MyMath.createTransformationMatrix(pos, rot, scale)
                    };
                }
            }
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            // Keeping this empty or throwing an exception as we now use UpdateInternal
        }
    }
}
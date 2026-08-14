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
            var archetypes = world.QueryArchetypes(WithMask, WithoutMask);

            // Run archetype chunks in parallel across CPU cores
            Parallel.ForEach(archetypes, archetype =>
            {
                var posArray = archetype.GetComponentArray<PositionComponent>();
                var rotArray = archetype.GetComponentArray<RotationComponent>();
                var scaleArray = archetype.GetComponentArray<ScaleComponent>();
                var matrixArray = archetype.GetComponentArray<LocalToWorldMatrixComponent>();

                bool hasRotation = archetype.Has<RotationComponent>();
                bool hasScale = archetype.Has<ScaleComponent>();

                int count = archetype.EntityCount;

                for (int i = 0; i < count; i++)
                {
                    var pos = posArray[i].value;

                    // FAST PATHS: Avoid heavy matrix math if rotation/scale are default
                    if (!hasRotation && !hasScale)
                    {
                        matrixArray[i] = new LocalToWorldMatrixComponent
                        {
                            value = Matrix4.CreateTranslation(pos)
                        };
                    }
                    else
                    {
                        // Fallback to full calculation
                        Quaternion rot = hasRotation ? rotArray[i].quaternion : Quaternion.Identity;
                        Vector3 scale = hasScale ? scaleArray[i].value : Vector3.One;

                        matrixArray[i] = new LocalToWorldMatrixComponent
                        {
                            // Note: If MyMath.createTransformationMatrix is slow, replace it with:
                            // Matrix4.CreateScale(scale) * Matrix4.CreateFromQuaternion(rot) * Matrix4.CreateTranslation(pos)
                            value = MyMath.createTransformationMatrix(pos, rot, scale)
                        };
                    }
                }
            });
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            // Keeping this empty or throwing an exception as we now use UpdateInternal
        }
    }
}
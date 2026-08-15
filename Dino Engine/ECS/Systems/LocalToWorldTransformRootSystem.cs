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


            var dirtyBuffer = world.DirtyEntities;
            if (dirtyBuffer.Count == 0) return;

            foreach (Entity entity in dirtyBuffer)
            {
                // Verify entity is still alive before fetching its view
                //if (!world.IsEntityValid(entity)) continue;

                // Dynamically fetch the UP-TO-DATE EntityView from world
                EntityView view = world.GetEntityView(entity);

                if (!view.Has<LocalToWorldMatrixComponent>()) continue;

                Vector3 pos = view.Has<PositionComponent>()
                    ? view.Get<PositionComponent>().value
                    : Vector3.Zero;

                Quaternion rot = view.Has<RotationComponent>()
                    ? view.Get<RotationComponent>().quaternion
                    : Quaternion.Identity;

                Vector3 scale = view.Has<ScaleComponent>()
                    ? view.Get<ScaleComponent>().value
                    : Vector3.One;

                view.Set(new LocalToWorldMatrixComponent
                {
                    value = MyMath.createTransformationMatrix(pos, rot, scale)
                });
            }

            dirtyBuffer.Clear();
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            // Keeping this empty or throwing an exception as we now use UpdateInternal
        }
    }
}
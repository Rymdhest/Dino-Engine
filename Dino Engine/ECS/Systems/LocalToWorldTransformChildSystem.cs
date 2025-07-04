using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    public class LocalToWorldTransformChildSystem : SystemBase
    {
        public LocalToWorldTransformChildSystem()
            : base(new BitMask(typeof(LocalToWorldMatrixComponent), typeof(PositionComponent), typeof(ParentComponent)))
        { 

        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            var localPos = entity.Get<PositionComponent>().value;

            Quaternion rotation = Quaternion.Identity;
            if (entity.Has<RotationComponent>()) rotation = entity.Get<RotationComponent>().quaternion;

            Vector3 scale = Vector3.One;
            if (entity.Has<ScaleComponent>()) scale = entity.Get<ScaleComponent>().value;

            var entityLocalToWorld = MyMath.createTransformationMatrix(localPos, rotation, scale);

            var parent = entity.Get<ParentComponent>().parent;
            var parentLocalToWorld = world.GetComponent<LocalToWorldMatrixComponent>(parent).value;

            entity.Set(new LocalToWorldMatrixComponent { value = (entityLocalToWorld* parentLocalToWorld) });
        }
    }
}

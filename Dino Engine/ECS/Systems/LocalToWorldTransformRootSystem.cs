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
                  new BitMask(typeof(ParentComponent))) { }


        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            var localPos = entity.Get<PositionComponent>().value;

            Quaternion rotation = Quaternion.Identity;
            if (entity.Has<RotationComponent>()) rotation = entity.Get<RotationComponent>().quaternion;

            Vector3 scale = Vector3.One;
            if (entity.Has<ScaleComponent>()) scale = entity.Get<ScaleComponent>().value;


            entity.Set(new LocalToWorldMatrixComponent{value = MyMath.createTransformationMatrix(localPos, rotation, scale)});
        }
    }
}

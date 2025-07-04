using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    public class ViewMatrixSystem : SystemBase
    {
        public ViewMatrixSystem()
            : base(new BitMask(
                typeof(LocalToWorldMatrixComponent),
                typeof(RotationComponent),
                typeof(ViewMatrixComponent)
            )) { }


        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            Vector3 worldPos = entity.Get<LocalToWorldMatrixComponent>().value.ExtractTranslation();

            Quaternion rotation = entity.Get<RotationComponent>().quaternion;



            entity.Set(new ViewMatrixComponent { value = MyMath.createViewMatrix(worldPos, rotation)});
        }
    }
}

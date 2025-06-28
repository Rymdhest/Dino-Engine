using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.ECS.Systems
{
    public class LocalToWorldTransformRootSystem : SystemBase
    {
        public LocalToWorldTransformRootSystem()
            : base(new BitMask(typeof(LocalToWorldMatrixComponent), typeof(PositionComponent)),
                  new BitMask(typeof(ParentComponent))) { }
        public override void Update(ECSWorld world, float deltaTime)
        {

        }
    }
}

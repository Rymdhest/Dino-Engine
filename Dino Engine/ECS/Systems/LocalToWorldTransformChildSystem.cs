using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.ECS.Systems
{
    public class LocalToWorldTransformChildSystem : SystemBase
    {
        public LocalToWorldTransformChildSystem()
            : base(new BitMask(typeof(LocalToWorldMatrixComponent), typeof(PositionComponent)))
        { 

        }
        public override void Update(ECSWorld world, float deltaTime)
        {

        }
    }
}

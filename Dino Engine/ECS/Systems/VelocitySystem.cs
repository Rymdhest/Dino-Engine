using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace Dino_Engine.ECS.Systems
{
    internal class VelocitySystem : ComponentSystem
    {

        public VelocitySystem()
        {
            addRequiredComponent<TransformationComponent>();
            addRequiredComponent<VelocityComponent>();
        }

        internal override void UpdateEntity(Entity entity)
        {
            Vector3 velocity = entity.getComponent<VelocityComponent>().velocity;
            Transformation transf = entity.getComponent<TransformationComponent>().Transformation;

            transf.translate(velocity*Engine.Delta);

            entity.getComponent<TransformationComponent>().Transformation = transf;
        }
    }
}

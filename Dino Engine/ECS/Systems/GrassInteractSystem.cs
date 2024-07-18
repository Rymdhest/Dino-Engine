﻿using Dino_Engine.ECS.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.ECS.Systems
{
    public class GrassInteractSystem : ComponentSystem
    {

        public GrassInteractSystem()
        {
            addRequiredComponent<TransformationComponent>();
            addRequiredComponent<CollisionComponent>();
        }
        internal override void UpdateEntity(Entity entity)
        {

        }
    }
}

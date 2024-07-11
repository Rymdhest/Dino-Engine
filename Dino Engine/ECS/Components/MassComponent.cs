﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.ECS.Components
{
    public class MassComponent : Component
    {
        public float mass;

        public MassComponent(float mass)
        {
            this.mass = mass;
        }
    }
}

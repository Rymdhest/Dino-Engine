﻿using Dino_Engine.Util.Data_Structures.Grids;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.Physics
{
    public class AABB : HitBox
    {
        public Vector3 min;
        public Vector3 max;
        public AABB (Vector3 min, Vector3 max)
        {
            this.min = min;
            this.max = max;
        }
    }
}

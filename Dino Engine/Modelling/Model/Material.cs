﻿using Dino_Engine.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.Modelling.Model
{

    public struct Material
    {
        public static Material ROCK = new Material(new Colour(55, 55, 55), 1);

        public Colour Colour;
        public int materialIndex;

        public Material(Colour colour, int materialIndex)
        {
            this.Colour = colour;
            this.materialIndex = materialIndex;
        }
    }
}

using Dino_Engine.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.Modelling.Model
{

    public struct Material
    {
        public static readonly Material WOOD = new Material(new Colour(107, 84, 61), 0f, 0.9f, 0f);
        public static readonly Material LEAF = new Material(new Colour(155, 161, 93), 0f, 0.5f, 0f);
        public static readonly Material SAND = new Material(new Colour(208, 177, 154), 0f, 0.9f, 0f);
        public static readonly Material ROCK = new Material(new Colour(124, 116, 126), 0f, 0.1f, 0f);
        public static readonly Material METAL = new Material(new Colour(198, 186, 179), 0.9f, 0.1f, 0f);

        public Colour Colour;
        public float metalicness;
        public float roughness;
        public float emission;

        public Material(Colour colour, float metalicness, float roughness, float emission)
        {
            this.Colour = colour;
            this.metalicness = metalicness;
            this.roughness = roughness;
            this.emission = emission;
        }
    }
}

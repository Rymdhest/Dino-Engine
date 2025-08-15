using Dino_Engine.Core;
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
        public static Material ROCK = new Material(new Colour(255, 255, 255), rougness:0.9f);
        public static Material GLOW = new Material(new Colour(255, 255, 255), emission:1.0f);
        public static Material BARK = new Material(new Colour(190, 120, 90), rougness: 0.98f, subSurfaceTransparancy:0.5f);

        public Colour Colour;
        public float roughness;
        public float emission;
        public float metalic;
        public float subSurfaceTransparancy;

        public Material(Colour colour, float rougness = 0.5f, float emission = 0.0f, float metalic = 0.0f, float subSurfaceTransparancy = 0.0f)
        {
            this.Colour = colour;
            this.roughness = rougness;
            this.emission = emission;
            this.metalic = metalic;
            this.subSurfaceTransparancy = subSurfaceTransparancy;
        }
        public Material(float rougness = 0.5f, float emission = 0.0f, float metalic = 0.0f, float subSurfaceTransparancy = 0.0f)
        {
            this.Colour = new Colour(255, 255, 255);
            this.roughness = rougness;
            this.emission = emission;
            this.metalic = metalic;
            this.subSurfaceTransparancy = subSurfaceTransparancy;
        }
    }
}

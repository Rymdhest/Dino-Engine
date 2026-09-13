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
        public static Material GROUND_ROCK = new Material(new Colour(100, 91, 75), rougness:0.9f);
        public static Material GROUND_SOIL   = new Material(new Colour(94, 58, 35), rougness: 0.9f);

        public static Material SAND = new Material(new Colour(150, 125, 85), rougness: 0.95f, subSurfaceTransparancy: 0.2f);

        public static Material Charcoal = new Material(new Colour(3, 3, 3), rougness: 0.99f, subSurfaceTransparancy: 0.0f);

        public static Material BARK_PINE = new Material(new Colour(105, 48, 25), rougness: 0.98f, subSurfaceTransparancy:0.0f);
        public static Material BARK_OAK = new Material(new Colour(190, 120, 90), rougness: 0.98f, subSurfaceTransparancy: 0.0f);
        public static Material BARK_BIRCH = new Material(new Colour(190, 120, 90), rougness: 0.98f, subSurfaceTransparancy: 0.0f);

        public static Material FOLIAGE_GREEN = new Material(new Colour(50, 53, 23), rougness: 0.98f, subSurfaceTransparancy: 0.8f);
        public static Material FOLIAGE_PINE = new Material(new Colour(45, 50, 20), rougness: 0.45f, subSurfaceTransparancy: 0.9f);
        public static Material FOLIAGE_OLIVE = new Material(new Colour(86, 89, 28), rougness: 0.6f, subSurfaceTransparancy: 0.6f);
        public static Material FOLIAGE_AUTUMN = new Material(new Colour(190, 120, 90), rougness: 0.98f, subSurfaceTransparancy: 0.5f);
        public static Material FOLIAGE_WHITE = new Material(new Colour(255, 255, 255), rougness: 0.98f, subSurfaceTransparancy: 0.5f);

        public static Material GLOW_WHITE = new Material(new Colour(255, 255, 255), emission: 1.0f);

        public static Material BONE = new Material(new Colour(125, 125, 125), rougness: 0.95f, subSurfaceTransparancy: 0.15f);
        public static Material IRON = new Material(new Colour(165, 165, 165), rougness: 0.4f, subSurfaceTransparancy: 0.0f, metalic:0.4f);

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

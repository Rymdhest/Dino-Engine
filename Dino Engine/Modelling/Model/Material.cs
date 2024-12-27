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
        public static Material ROCK = new Material(new Colour(255, 255, 255), Engine.RenderEngine.textureGenerator.test);
        public static Material GLOW = new Material(new Colour(255, 255, 255), Engine.RenderEngine.textureGenerator.flatGlow);
        public static Material BARK = new Material(new Colour(255, 255, 255), Engine.RenderEngine.textureGenerator.bark);

        public Colour Colour;
        public int materialIndex;

        public Material(Colour colour, int materialIndex)
        {
            this.Colour = colour;
            this.materialIndex = materialIndex;
        }
    }
}

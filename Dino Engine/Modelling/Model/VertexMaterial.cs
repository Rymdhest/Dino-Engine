using Dino_Engine.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.Modelling.Model
{
    public struct VertexMaterial
    {
        public int materialTextureIndex;
        public Colour colour;

        public VertexMaterial(int materialTextureIndex, Colour colour)
        {
            this.materialTextureIndex = materialTextureIndex;
            this.colour = colour;
        }
        public VertexMaterial(int materialTextureIndex)
        {
            this.materialTextureIndex = materialTextureIndex;
            this.colour = new Colour(255, 255, 255);
        }
    }
}

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

        /// <summary>How much of its model's growth stands on what's made of this, 0 to 1 (<see cref="Vertex.Shell"/>).</summary>
        public float Shell;

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

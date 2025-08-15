using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Model
{
    public class Vertex
    {
        public Vector3 position;
        public Vector2[] UVs;
        public int materialTextureIndex;
        public Colour colour;

        public Vertex(Vector3 position, VertexMaterial vertexMaterial, params Vector2[] UVs)        {
            this.position = position;
            this.UVs = UVs;
            this.materialTextureIndex = vertexMaterial.materialTextureIndex;
            this.colour = vertexMaterial.colour;
        }

        public Vertex(Vector3 position, int materialTextureIndex, Colour colour, params Vector2[] UVs)        {
            this.position = position;
            this.UVs = UVs;
            this.materialTextureIndex = materialTextureIndex;
            this.colour = colour;
        }

        public Vertex(Vertex v)        {
            this.position = v.position;
            this.UVs = v.UVs;
            this.materialTextureIndex = v.materialTextureIndex;
            this.colour = v.colour;
        }
    }
}

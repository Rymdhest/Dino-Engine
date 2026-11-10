using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Model
{
    public class Vertex
    {
        public Vector3 position;
        public Vector2[] UVs;
        public Vector2 boneIndices;
        public Vector2 BoneWeights;
        public int materialTextureIndex;
        public Colour colour;
        public int joint = -1;  // The PlantJoint of its mesh that holds it, or -1 if nothing does.

        /// <summary>
        /// Metres from here to each edge of the flat face it's a corner of, which the relief comes up to nothing at (Surface/Relief.glsl): a distance to a line is a
        /// straight function across a flat face, so a pixel's are these interpolated. <see cref="NoEdges"/> for whatever has no corners to keep clear of.
        /// </summary>
        public Vector4 Edges = NoEdges;

        public static readonly Vector4 NoEdges = new Vector4(1e6f);

        public Vertex(Vector3 position, VertexMaterial vertexMaterial, params Vector2[] UVs)
        {
            this.position = position;
            this.UVs = UVs;
            this.materialTextureIndex = vertexMaterial.materialTextureIndex;
            this.colour = vertexMaterial.colour;

            boneIndices = new Vector2(0.0f, 0.0f);
            BoneWeights = new Vector2(1.0f, 0.0f);
        }

        public Vertex(Vector3 position, int materialTextureIndex, Colour colour, Vector2 boneIndices, Vector2 boneWeights, params Vector2[] UVs)
        {
            this.position = position;
            this.UVs = UVs;
            this.materialTextureIndex = materialTextureIndex;
            this.colour = colour;
            this.boneIndices = boneIndices;
            this.BoneWeights = boneWeights;
        }

        public Vertex(Vertex v)
        {
            this.position = v.position;
            this.UVs = v.UVs;
            this.materialTextureIndex = v.materialTextureIndex;
            this.colour = v.colour;

            this.boneIndices = v.boneIndices;
            this.BoneWeights = v.BoneWeights;
            this.joint = v.joint;
            this.Edges = v.Edges;
        }
    }
}

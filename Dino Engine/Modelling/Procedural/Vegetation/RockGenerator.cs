using Dino_Engine.Core;
using Dino_Engine.ECS;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Rendering;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using OpenTK.Compute.OpenCL;
using OpenTK.Mathematics;
using Util.Noise;
using static OpenTK.Graphics.OpenGL.GL;
namespace Dino_Engine.Modelling.Procedural.Nature
{
    public class RockGenerator
    {
        public static Mesh GenerateRockBrown( )
        {
            Mesh RockMesh = IcoSphereGenerator.CreateIcosphere(4, new VertexMaterial(TextureGenerator.soil, new Colour(255, 255, 255)), 8);

            OpenSimplexNoise noise = new OpenSimplexNoise();
            for (int i = 0; i < RockMesh.meshVertices.Count; i++)
            {
                Vector3 oldPos = RockMesh.meshVertices[i].position;
                float noiseValue = noise.FBM(oldPos.X, oldPos.Y, oldPos.Z, 0.86f, 5);
                Vector3 newPos = oldPos + oldPos * noiseValue * 0.66f;

                float floorScale = 5f;
                float floorWeight = 0.7f;
                //newPos.X = MathF.Floor(newPos.X * floorScale) / floorScale;
                newPos.Y = (MathF.Floor(newPos.Y * floorScale) / floorScale) * floorWeight + newPos.Y * (1f - floorWeight);


                RockMesh.meshVertices[i].position = newPos;
            }
            RockMesh.FlatRandomness(0.01f);

            RockMesh.calculateAllNormals();
            for (int i = 0; i < RockMesh.meshVertices.Count; i++)
            {
                Vector3 normal = RockMesh.meshVertices[i].normal;
                float dotProduct = MyMath.clamp01(Vector3.Dot(normal, new Vector3(0f, 1f, 0f)));
                dotProduct = MathF.Pow(dotProduct, 3.0f);
                RockMesh.meshVertices[i].colour = new Colour(MyMath.lerp(RockMesh.meshVertices[i].colour.ToVector3(), Dino_Engine.Modelling.Model.Material.FOLIAGE_OLIVE.Colour.ToVector3(), dotProduct));

            }

            return RockMesh;
        }
        public static Mesh GenerateRock(float floorWeight)
        {
            Mesh RockMesh = IcoSphereGenerator.CreateIcosphere(4, new VertexMaterial(TextureGenerator.rock), 16);

            OpenSimplexNoise noise = new OpenSimplexNoise();
            for (int i = 0; i < RockMesh.meshVertices.Count; i++)
            {
                Vector3 oldPos = RockMesh.meshVertices[i].position;
                float noiseValue = noise.FBM(oldPos.X, oldPos.Y, oldPos.Z, 0.86f, 5);
                Vector3 newPos = oldPos + oldPos * noiseValue * 0.66f;

                float floorScale = 4f;
                //newPos.X = MathF.Floor(newPos.X * floorScale) / floorScale;
                newPos.Y = (MathF.Floor(newPos.Y * floorScale) / floorScale) * floorWeight + newPos.Y * (1f - floorWeight);


                RockMesh.meshVertices[i].position = newPos;
            }
            RockMesh.FlatRandomness(0.01f);

            RockMesh.calculateAllNormals();
            for (int i = 0; i < RockMesh.meshVertices.Count; i++)
            {
                Vector3 normal = RockMesh.meshVertices[i].normal;
                float dotProduct = MyMath.clamp01(Vector3.Dot(normal, new Vector3(0f, 1f, 0f)));
                dotProduct = MathF.Pow(dotProduct, 3.0f);
                RockMesh.meshVertices[i].colour = new Colour(MyMath.lerp(RockMesh.meshVertices[i].colour.ToVector3(), Dino_Engine.Modelling.Model.Material.FOLIAGE_OLIVE.Colour.ToVector3(), dotProduct*0.7f));

            }

            return RockMesh;
        }
    }
}

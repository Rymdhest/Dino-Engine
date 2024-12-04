

using Dino_Engine.Core;
using Dino_Engine.ECS;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using OpenTK.Mathematics;
namespace Dino_Engine.Modelling.Procedural.Nature
{
    public class TreeGenerator
    {

        public Material trunkMaterial = new Material(new Colour(107, 84, 61), Engine.RenderEngine.textureGenerator.grain);
        public Material leafMaterial = new Material(new Colour(195, 231, 73), Engine.RenderEngine.textureGenerator.flat);

        public Mesh GenerateTree()
        {
            Mesh tree = new Mesh();

            float r = 1f/15;
            float h = 1f;
            float topRadius = 1f/4f;

            List<Vector2> layers = new List<Vector2>() {
                new Vector2(r, 0),
                new Vector2(r*0.9f, h*0.33f),
                new Vector2(r*0.8f, h*0.66f),
                new Vector2(r*0.7f, h) };
            Mesh stem = MeshGenerator.generateCylinder(layers, 10, trunkMaterial);
            stem.FlatRandomness(new Vector3(0.01f, 0.05f, 0.01f));

            tree += stem;

            Mesh top = IcoSphereGenerator.CreateIcosphere(3, leafMaterial);
            top.FlatRandomness(0.0f);
            top.scale(new Vector3(topRadius, topRadius*1.5f, topRadius));
            top.translate(new Vector3(0f, h+ topRadius * 0.75f, 0f));


            tree += top;

            top.FlatRandomness(0.0f);

            return tree;
        }

        public Mesh GenerateFractalTree(int depth)
        {
            List<Vector2> layers = new List<Vector2>() {
                new Vector2(1.0f, 0),
                new Vector2(1.0f, 10.0f),
                new Vector2(1.0f, 40.0f),
                new Vector2(1.0f, 80.0f)};
            Mesh poleMesh = MeshGenerator.generateCylinder(layers, 50, new Material(new Colour(255, 255, 255), Engine.RenderEngine.textureGenerator.bark), sealTop: 0.1f);

            foreach (MeshVertex meshVertex in poleMesh.meshVertices)
            {
                float angle = MathF.Atan2(meshVertex.position.X, meshVertex.position.Z) * 12.0f;
                meshVertex.position += new Vector3(MathF.Sin(angle), 0f, MathF.Cos(angle)) * 0.01f;

                if (meshVertex.position.Y < 1f)
                {
                    meshVertex.material.Colour = new Colour(125, 165, 85);
                    float valueX = MathF.Pow((MathF.Sin(angle)), 1.0f);
                    float valueZ = MathF.Pow((MathF.Cos(angle)), 1.0f);
                    meshVertex.position += (new Vector3(valueX, 0f, valueZ) * .05f);
                }
            }

            poleMesh.FlatRandomness(new Vector3(.05f, 0f, .05f));
            return poleMesh;
        }
    }
}

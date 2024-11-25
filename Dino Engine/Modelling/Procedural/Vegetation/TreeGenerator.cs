

using Dino_Engine.Core;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using OpenTK.Mathematics;
namespace Dino_Engine.Modelling.Procedural.Nature
{
    public class TreeGenerator
    {

        public Material trunkMaterial = new Material(new Colour(107, 84, 61), Engine.RenderEngine.textureGenerator.grainIndex);
        public Material leafMaterial = new Material(new Colour(195, 231, 73), Engine.RenderEngine.textureGenerator.flatIndex);

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
            Mesh tree = GenerateTree();

            if (depth > 0)
            {
                int branches = 2+MyMath.rand.Next(4);

                for (int i = 0; i < branches; i++)
                {
                    float angle = MathF.PI / (6.5f + MyMath.rngMinusPlus());

                    Mesh branch = GenerateFractalTree(--depth);
                    branch.scale(new Vector3(0.3f, 0.45f, 0.3f));
                    branch.rotate(new Vector3(angle, 0, 0f));
                    branch.rotate(new Vector3(0f, ((MathF.PI * 2f) / branches) * i + MyMath.rngMinusPlus(0.5f), 0f));
                    branch.translate(new Vector3(0f, 0.4f + (MyMath.rngMinusPlus(0.1f) + i * (1f / branches) * 0.4f), 0f));

                    tree += branch;
                }
            }
            tree.rotate(new Vector3(0f, MathF.PI * 2f* MyMath.rng(), 0f));

            return tree;
        }
    }
}

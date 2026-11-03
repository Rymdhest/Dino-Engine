using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture;
using Dino_Engine.Modelling.Procedural.Nature;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Vegetation
{
    /// <summary>The smaller plants of the world's biomes, and its crops.</summary>
    public static class PlantGenerator
    {
        private static WorldRandom Random(string name) => new WorldRandom(0x9147).Fork(name);

        // A leaf-shaped strip: wide at the base, coming to a point, arching over towards +x as it rises.
        private static void Blade(MeshBuilder b, Vector3 root, float height, float width, float arch, float yaw, VertexMaterial material)
        {
            float cos = MathF.Cos(yaw);
            float sin = MathF.Sin(yaw);
            b.Surface(1, 5, (u, v) =>
            {
                float taper = width * (1f - v * v) + 0.002f;
                float across = (u * 2f - 1f) * taper;
                float reach = arch * v * v;
                float rise = height * (v - 0.35f * arch / MathF.Max(height, 0.01f) * v * v);
                return root + new Vector3(reach * cos - across * sin, rise, reach * sin + across * cos);
            }, material);
        }

        public static Mesh GenerateBamboo()
        {
            WorldRandom random = Random("bamboo");
            var b = new MeshBuilder();
            VertexMaterial stem = new VertexMaterial(TextureGenerator.wood, new Colour(120, 150, 74));
            VertexMaterial node = new VertexMaterial(TextureGenerator.wood, new Colour(74, 96, 52));
            Mesh leaf = TreeGenerator.GenerateLeafMesh(new Vector2(0.2f, -0.3f), new Vector2i(1, 3), new VertexMaterial(TextureGenerator.leaf_olive));

            int stalks = random.Int(9, 13);
            for (int s = 0; s < stalks; s++)
            {
                float angle = random.Float() * MathF.Tau;
                float distance = random.Range(0.2f, 1.7f);
                Vector3 foot = new Vector3(MathF.Cos(angle) * distance, -0.3f, MathF.Sin(angle) * distance);
                float height = random.Range(13f, 22f);
                Vector3 lean = new Vector3(random.Range(-0.09f, 0.09f), 1f, random.Range(-0.09f, 0.09f));
                float radius = random.Range(0.12f, 0.2f);

                int sections = (int)(height / 2.2f);
                Vector3 at = foot;
                for (int i = 0; i < sections; i++)
                {
                    Vector3 next = at + lean * (height / sections);
                    float t = (float)i / sections;
                    b.Tube(at, next, radius * (1f - t * 0.55f), radius * (1f - (t + 1f / sections) * 0.55f), 5, stem, capped: false);
                    b.Tube(next - Vector3.UnitY * 0.05f, next + Vector3.UnitY * 0.05f, radius * 1.14f * (1f - t * 0.55f), radius * 1.14f * (1f - t * 0.55f), 5, node, capped: false);

                    // Sprays of narrow leaves from the upper joints.
                    if (t > 0.42f)
                    {
                        int leaves = random.Int(5, 8);
                        for (int l = 0; l < leaves; l++)
                        {
                            float yaw = random.Float() * MathF.Tau;
                            b.Add(leaf, next + new Vector3(MathF.Cos(yaw), random.Range(-0.1f, 0.1f), MathF.Sin(yaw)) * random.Range(0.2f, 0.6f),
                                new Vector3(random.Range(1.1f, 1.9f), 1f, random.Range(0.32f, 0.5f)), yaw);
                        }
                    }
                    at = next;
                }
            }
            return b.Build();
        }

        public static Mesh GenerateReeds()
        {
            WorldRandom random = Random("reeds");
            var b = new MeshBuilder();
            VertexMaterial stem = new VertexMaterial(TextureGenerator.foliage_white, new Colour(126, 140, 74));
            VertexMaterial blade = new VertexMaterial(TextureGenerator.foliage_white, new Colour(102, 124, 60));
            VertexMaterial head = new VertexMaterial(TextureGenerator.foliage_white, new Colour(92, 62, 40));

            for (int i = 0; i < 16; i++)
            {
                Vector3 root = new Vector3(random.Range(-0.9f, 0.9f), -0.1f, random.Range(-0.9f, 0.9f));
                float height = random.Range(2.6f, 4.6f);
                Vector3 tip = root + new Vector3(random.Range(-0.3f, 0.3f), height, random.Range(-0.3f, 0.3f));
                b.Tube(root, tip, 0.05f, 0.02f, 3, stem, capped: false);
                if (i % 3 == 0)
                {
                    Vector3 from = Vector3.Lerp(root, tip, 0.74f);
                    b.Tube(from, Vector3.Lerp(root, tip, 0.94f), 0.11f, 0.1f, 5, head);
                }
                Blade(b, root, height * 0.74f, 0.09f, random.Range(0.4f, 1.1f), random.Float() * MathF.Tau, blade);
            }
            return b.Build();
        }

        public static Mesh GeneratePampas()
        {
            WorldRandom random = Random("pampas");
            var b = new MeshBuilder();
            VertexMaterial blade = new VertexMaterial(TextureGenerator.foliage_white, new Colour(132, 108, 58));
            VertexMaterial plume = new VertexMaterial(TextureGenerator.foliage_white, new Colour(190, 164, 118));
            VertexMaterial stem = new VertexMaterial(TextureGenerator.foliage_white, new Colour(140, 116, 72));

            for (int i = 0; i < 24; i++)
            {
                Vector3 root = new Vector3(random.Range(-0.5f, 0.5f), -0.1f, random.Range(-0.5f, 0.5f));
                Blade(b, root, random.Range(2.2f, 3.4f), 0.12f, random.Range(1f, 2.1f), random.Float() * MathF.Tau, blade);
            }
            for (int i = 0; i < 6; i++)
            {
                Vector3 root = new Vector3(random.Range(-0.3f, 0.3f), 0f, random.Range(-0.3f, 0.3f));
                float height = random.Range(4.4f, 6.2f);
                Vector3 top = root + new Vector3(random.Range(-0.5f, 0.5f), height, random.Range(-0.5f, 0.5f));
                b.Tube(root, top, 0.05f, 0.02f, 3, stem, capped: false);

                // The plume is a spray of fine strands falling away from the stem's tip, not one solid shape.
                Vector3 lean = (top - root).Normalized();
                for (int strand = 0; strand < 16; strand++)
                {
                    float along = random.Range(0f, 1.1f);
                    Vector3 from = top - lean * along;
                    Vector3 spread = new Vector3(random.Range(-1f, 1f), random.Range(-0.2f, 0.4f), random.Range(-1f, 1f));
                    Vector3 to = from + (lean * 0.5f + spread * 0.22f) * random.Range(0.6f, 1.2f);
                    b.Tube(from, to, 0.022f, 0.004f, 3, plume, capped: false);
                }
            }
            return b.Build();
        }

        public static Mesh GenerateHeather()
        {
            WorldRandom random = Random("heather");
            var b = new MeshBuilder();
            VertexMaterial leaves = new VertexMaterial(TextureGenerator.foliage_white, new Colour(66, 80, 44));
            VertexMaterial blossom = new VertexMaterial(TextureGenerator.foliage_white, new Colour(146, 78, 142));

            for (int i = 0; i < 26; i++)
            {
                float angle = random.Float() * MathF.Tau;
                float distance = MathF.Sqrt(random.Float()) * 1f;
                Vector3 at = new Vector3(MathF.Cos(angle) * distance, 0f, MathF.Sin(angle) * distance);
                float height = random.Range(0.5f, 1.05f) * (1.2f - distance * 0.5f);
                Vector3 tip = at + new Vector3(random.Range(-0.12f, 0.12f), height, random.Range(-0.12f, 0.12f));
                b.Tube(at - Vector3.UnitY * 0.1f, tip, 0.1f, 0.03f, 3, leaves, capped: false);
                b.Tube(Vector3.Lerp(at, tip, 0.45f), tip + Vector3.UnitY * 0.1f, 0.12f, 0.02f, 4, blossom);
            }
            return b.Build();
        }

        public static Mesh GenerateScrub()
        {
            WorldRandom random = Random("scrub");
            var b = new MeshBuilder();
            VertexMaterial twig = new VertexMaterial(TextureGenerator.pineBark, new Colour(164, 142, 118));
            VertexMaterial leaf = new VertexMaterial(TextureGenerator.leaf_white, new Colour(112, 110, 58));
            Mesh sprig = TreeGenerator.GenerateLeafMesh(new Vector2(0.15f, -0.2f), new Vector2i(1, 2), leaf);

            for (int i = 0; i < 12; i++)
            {
                float yaw = random.Float() * MathF.Tau;
                Vector3 direction = new Vector3(MathF.Cos(yaw) * random.Range(0.4f, 0.9f), random.Range(0.5f, 1f), MathF.Sin(yaw) * random.Range(0.4f, 0.9f)).Normalized();
                float length = random.Range(1.2f, 2.3f);
                Vector3 tip = direction * length;
                b.Tube(Vector3.Zero - Vector3.UnitY * 0.1f, tip, 0.07f, 0.02f, 3, twig, capped: false);
                for (int l = 0; l < 4; l++)
                {
                    Vector3 at = direction * length * random.Range(0.4f, 1f);
                    b.Add(sprig, at, new Vector3(random.Range(0.32f, 0.5f)), random.Float() * MathF.Tau);
                }
            }
            return b.Build();
        }

        public static Mesh GenerateMushrooms()
        {
            WorldRandom random = Random("mushrooms");
            var b = new MeshBuilder();
            VertexMaterial stem = new VertexMaterial(TextureGenerator.flat, new Colour(226, 214, 196));
            VertexMaterial cap = new VertexMaterial(TextureGenerator.leather, new Colour(random.Int(150, 210), 72, 44));

            for (int i = 0; i < 6; i++)
            {
                Vector3 at = new Vector3(random.Range(-0.42f, 0.42f), 0f, random.Range(-0.42f, 0.42f));
                float height = random.Range(0.26f, 0.56f);
                float radius = random.Range(0.13f, 0.27f);
                b.Tube(at - Vector3.UnitY * 0.06f, at + Vector3.UnitY * height, 0.05f, 0.04f, 5, stem, capped: false);
                b.Surface(8, 3, (u, v) =>
                {
                    float angle = -u * MathF.Tau;
                    float ring = radius * MathF.Cos(v * MathF.PI * 0.5f);
                    float rise = radius * 0.72f * MathF.Sin(v * MathF.PI * 0.5f);
                    return at + new Vector3(MathF.Cos(angle) * ring, height + rise, MathF.Sin(angle) * ring);
                }, cap);
            }
            return b.Build();
        }

        public static Mesh GenerateGrainCrop()
        {
            WorldRandom random = Random("grain");
            var b = new MeshBuilder();
            VertexMaterial stem = new VertexMaterial(TextureGenerator.foliage_white, new Colour(198, 166, 88));
            VertexMaterial head = new VertexMaterial(TextureGenerator.foliage_white, new Colour(214, 176, 92));

            for (int i = 0; i < 18; i++)
            {
                Vector3 root = new Vector3(random.Range(-1.1f, 1.1f), -0.05f, random.Range(-1.1f, 1.1f));
                float height = random.Range(2f, 2.9f);
                Vector3 tip = root + new Vector3(random.Range(-0.14f, 0.14f), height, random.Range(-0.14f, 0.14f));
                b.Tube(root, tip, 0.035f, 0.02f, 3, stem, capped: false);
                b.Tube(Vector3.Lerp(root, tip, 0.8f), tip + Vector3.UnitY * 0.12f, 0.07f, 0.02f, 4, head);
            }
            return b.Build();
        }

        public static Mesh GenerateVegetableCrop()
        {
            WorldRandom random = Random("vegetable");
            var b = new MeshBuilder();
            VertexMaterial heart = new VertexMaterial(TextureGenerator.foliage_white, new Colour(112, 150, 82));
            VertexMaterial leaf = new VertexMaterial(TextureGenerator.leaf_white, new Colour(82, 124, 62));
            Mesh broad = TreeGenerator.GenerateLeafMesh(new Vector2(0.3f, -0.5f), new Vector2i(2, 3), leaf);

            b.Add(IcoSphereGenerator.CreateIcosphere(1, heart), new Vector3(0f, 0.32f, 0f), new Vector3(0.42f, 0.36f, 0.42f));
            for (int i = 0; i < 9; i++)
            {
                float yaw = i * (MathF.Tau / 9f) + random.Range(-0.2f, 0.2f);
                b.Add(broad, new Vector3(0f, 0.1f, 0f), new Vector3(random.Range(0.75f, 1.05f)), yaw);
            }
            return b.Build();
        }
    }
}

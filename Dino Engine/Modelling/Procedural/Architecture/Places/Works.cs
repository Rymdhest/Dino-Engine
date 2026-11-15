using Dino_Engine.Modelling.Model;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.Modelling.Procedural.Architecture.Places.PlaceMaterials;

namespace Dino_Engine.Modelling.Procedural.Architecture.Places
{
    /// <summary>
    /// What the Reich's works and the land's farms run on (PLACES.md): the Amber Institute's boiler that keeps its broth warm, its firebox glowing through the grate and its chimney
    /// smoking; and hay, stacked round a pole under a cap of thatch, as the land has always stacked it, which the Reich's grazers eat now. Model +z is a piece's front, y = 0 where
    /// people stand. Headless.
    /// </summary>
    internal static class Works
    {
        private const float ChimneyHigh = 7.2f;

        /// <summary>Where a boiler's smoke leaves its chimney, in its own frame.</summary>
        public static IReadOnlyList<Vector3> BoilerSmoke() => new[] { new Vector3(-1.2f, ChimneyHigh + 0.4f, -0.55f) };

        /// <summary>
        /// The Institute's boiler: a drum of riveted plate on a firebox of brick, the coals glowing through its grate, brass bands round the drum and its gauges and whistle on top, a
        /// brick chimney behind with an iron cap, pipes off it, and the coal heaped by it with the shovel left in. Numbers: none.
        /// </summary>
        public static void Boiler(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            VertexMaterial brick = new VertexMaterial(MaterialCatalog.Brick.Index, new Colour(new Vector3(0.48f, 0.26f, 0.2f) * random.Range(0.9f, 1.05f)));
            VertexMaterial plate = Plating(random, 1.1f), iron = Iron(random), brass = Brass(random), soot = Flat(new Vector3(0.05f, 0.045f, 0.04f));
            // The firebox, and its door with the grate glowing behind it.
            Kit.Span(b, new Vector3(-1.7f, -0.3f, -0.8f), new Vector3(1.7f, 1.1f, 0.8f), 0.03f, brick);
            Kit.Span(b, new Vector3(-1.8f, 1.05f, -0.9f), new Vector3(1.8f, 1.2f, 0.9f), 0.02f, Dressed(new Vector3(0.4f, 0.38f, 0.35f), random));
            Kit.Span(b, new Vector3(-0.45f, 0.15f, 0.8f), new Vector3(0.45f, 0.85f, 0.84f), 0.01f, iron);
            for (int k = 0; k < 5; k++) Kit.Plank(b, new Vector3(-0.35f + k * 0.175f, 0.22f, 0.85f), new Vector3(-0.35f + k * 0.175f, 0.62f, 0.85f), Vector3.UnitZ, 0.04f, 0.02f, iron, 0f);
            Kit.Face(b, new Vector3(-0.38f, 0.2f, 0.845f), new Vector3(0.38f, 0.2f, 0.845f), new Vector3(0.38f, 0.64f, 0.845f), new Vector3(-0.38f, 0.64f, 0.845f), Vector3.UnitZ, Glow(1f, 0.4f, 0.1f, 1.6f));
            lights.Add(AssetLight.Burning(new Vector3(0f, 0.3f, 0.6f), 0.4f));
            lights.Add(AssetLight.Campfire(new Vector3(0f, 0.6f, 1.4f)) with { Intensity = 45f, Radius = 8f });
            // The drum, banded, its dome, gauges and whistle.
            Kit.Lathe(b, new Vector3(-1.5f, 1.95f, 0f), Vector3.UnitX, 3f, Kit.Profile((0f, 0.62f), (0.04f, 0.76f), (0.96f, 0.76f), (1f, 0.62f)), 14, 3, plate);
            foreach (float x in new[] { -1.1f, -0.3f, 0.5f, 1.2f }) b.Tube(new Vector3(x - 0.05f, 1.95f, 0f), new Vector3(x + 0.05f, 1.95f, 0f), 0.79f, 0.79f, 14, brass, capped: false);
            Kit.Lathe(b, new Vector3(0.6f, 2.65f, 0f), Vector3.UnitY, 0.45f, Kit.Profile((0f, 0.3f), (0.7f, 0.28f), (1f, 0.12f)), 10, 2, plate);
            Kit.Lathe(b, new Vector3(0.6f, 3.1f, 0f), Vector3.UnitY, 0.3f, Kit.Profile((0f, 0.06f), (0.5f, 0.07f), (1f, 0.03f)), 8, 2, brass);
            for (int k = 0; k < 2; k++)
            {
                Vector3 at = new Vector3(-0.6f + k * 0.5f, 2.2f, 0.78f);
                Kit.Lathe(b, at, Vector3.UnitZ, 0.06f, _ => 0.13f, 10, 1, brass);
                Kit.Lathe(b, at + Vector3.UnitZ * 0.06f, Vector3.UnitZ, 0.005f, _ => 0.1f, 10, 1, Plaster(1.1f));
                Kit.Plank(b, at + new Vector3(0f, 0f, 0.07f), at + new Vector3(0.06f, 0.06f, 0.07f), Vector3.UnitZ, 0.012f, 0.004f, Dark, 0f);
            }
            // Its legs on the firebox, and the chimney behind it.
            foreach (float x in new[] { -1.2f, 1.2f }) Kit.Span(b, new Vector3(x - 0.12f, 1.2f, -0.5f), new Vector3(x + 0.12f, 1.4f, 0.5f), 0.01f, iron);
            Kit.Span(b, new Vector3(-1.62f, -0.3f, -0.98f), new Vector3(-0.78f, ChimneyHigh, -0.12f), 0.03f, brick);
            Kit.Span(b, new Vector3(-1.72f, ChimneyHigh, -1.08f), new Vector3(-0.68f, ChimneyHigh + 0.22f, -0.02f), 0.02f, Dressed(new Vector3(0.4f, 0.38f, 0.35f), random));
            Kit.Span(b, new Vector3(-1.5f, ChimneyHigh + 0.22f, -0.86f), new Vector3(-0.9f, ChimneyHigh + 0.32f, -0.24f), 0.01f, soot);
            for (float y = 2f; y < ChimneyHigh - 0.5f; y += 1.6f) Kit.Span(b, new Vector3(-1.66f, y, -1.02f), new Vector3(-0.74f, y + 0.08f, -0.08f), 0.005f, iron);
            // Pipes off the drum, brass, along the ground and away towards whatever it warms.
            Kit.Pole(b, new Vector3(1.4f, 2.2f, 0f), new Vector3(2.2f, 2.2f, 0f), 0.09f, brass);
            Kit.Pole(b, new Vector3(2.2f, 2.25f, 0f), new Vector3(2.2f, 0.3f, 0f), 0.09f, brass);
            Kit.Pole(b, new Vector3(2.2f, 0.3f, 0f), new Vector3(2.2f, 0.3f, 2.6f), 0.09f, brass);
            Kit.Lathe(b, new Vector3(2.2f, 1.2f, 0.1f), Vector3.UnitZ, 0.08f, _ => 0.16f, 8, 1, iron);
            // The coal, heaped on its own side, and the shovel in it.
            Kit.Heap(b, new Vector3(-0.4f, Ground - 0.05f, 2.1f), new Vector2(1.3f, 0.9f), 0.75f, 0.3f, Flat(new Vector3(0.06f, 0.055f, 0.05f)), random, 4, 12);
            Vector3 dir = Vector3.Normalize(new Vector3(0.4f, 1f, 0.3f));
            Kit.Pole(b, new Vector3(-0.1f, 0.4f, 2.1f), new Vector3(-0.1f, 0.4f, 2.1f) + dir * 1.2f, 0.022f, Timber(random));
            Kit.Box(b, new Vector3(-0.15f, 0.35f, 2.05f), Vector3.UnitX, dir, Vector3.Cross(Vector3.UnitX, dir), new Vector3(0.12f, 0.16f, 0.01f), 0f, iron);
        }

        /// <summary>
        /// A net spread out on the shingle to be mended, its edges pegged down, its cork floats along one side, lumpy where it lies over stones, with the mender's stool, the needle and
        /// the basket of twine left by the hole they were at. Numbers: length, width.
        /// </summary>
        public static void MendingNet(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float length = Math.Clamp(spec.Metres(0), 2f, 8f), width = Math.Clamp(spec.Metres(1), 1.4f, 4f);
            float hx = length * 0.5f, hz = width * 0.5f;
            VertexMaterial net = Net(random, 1.1f), net2 = Net(random, 0.8f), wood = Weathered(random, 0.85f);
            float[,] lump = new float[7, 5];
            for (int i = 0; i < 7; i++) for (int j = 0; j < 5; j++) lump[i, j] = (i == 0 || i == 6 || j == 0 || j == 4) ? 0f : random.Range(0f, 0.06f);
            Vector3 P(int i, int j) => new Vector3(-hx + length * i / 6f + random.Range(-0.02f, 0.02f), Ground + 0.015f + lump[i, j], -hz + width * j / 4f);
            var grid = new Vector3[7, 5];
            for (int i = 0; i < 7; i++) for (int j = 0; j < 5; j++) grid[i, j] = P(i, j);
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < 4; j++) Kit.Sheet(b, grid[i, j], grid[i + 1, j], grid[i + 1, j + 1], grid[i, j + 1], net, net2);
            }
            // Pegs at its corners and along its sides, and its floats along the far edge.
            for (int i = 0; i <= 6; i += 2)
            {
                foreach (int j in new[] { 0, 4 }) Kit.Pole(b, grid[i, j] - Vector3.UnitY * 0.1f, grid[i, j] + Vector3.UnitY * 0.12f, 0.025f, wood);
            }
            for (int i = 0; i < 12; i++)
            {
                Vector3 at = new Vector3(-hx + length * (i + 0.5f) / 12f, Ground + 0.05f, -hz + 0.04f);
                Kit.Lathe(b, at - Vector3.UnitX * 0.06f, Vector3.UnitX, 0.12f, _ => 0.05f, 6, 1, Flat(new Vector3(0.5f, 0.34f, 0.18f)));
            }
            // The mender's stool, the needle and the basket of twine by the hole they were at.
            Vector3 stool = new Vector3(random.Range(-hx * 0.4f, hx * 0.4f), 0f, hz + 0.6f);
            Kit.Lathe(b, stool, Vector3.UnitY, 0.42f, Kit.Profile((0f, 0.14f), (0.9f, 0.14f), (1f, 0.17f)), 7, 2, wood);
            Kit.Lathe(b, stool + new Vector3(0.6f, 0f, 0.1f), Vector3.UnitY, 0.28f, Kit.Profile((0f, 0.16f), (1f, 0.22f)), 9, 1, Straw(random, 0.8f), openEnd: true);
            Kit.Ball(b, stool + new Vector3(0.6f, 0.22f, 0.1f), new Vector3(0.14f, 0.08f, 0.14f), 6, 3, Rope(random, 1.1f));
            Kit.Plank(b, stool + new Vector3(-0.2f, Ground + 0.02f, -0.7f), stool + new Vector3(0.05f, Ground + 0.02f, -0.62f), Vector3.UnitY, 0.03f, 0.01f, Bleached(random), 0f);
        }

        /// <summary>A table where the catch is gutted, fish laid out on it silver and the knife by them, a bucket of guts and a basket of what's done under it. Numbers: length.</summary>
        public static void GuttingTable(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float length = Math.Clamp(spec.Metres(0), 1.6f, 3.4f);
            float hx = length * 0.5f;
            VertexMaterial wood = Weathered(random, 0.9f), stained = new VertexMaterial(MaterialCatalog.Wood.Index, new Colour(0.3f, 0.24f, 0.2f));
            Kit.Span(b, new Vector3(-hx, 0.84f, -0.4f), new Vector3(hx, 0.9f, 0.4f), 0.01f, stained);
            foreach (float x in new[] { -hx + 0.25f, hx - 0.25f }) foreach (float z in new[] { -0.32f, 0.32f }) Kit.Span(b, new Vector3(x - 0.05f, 0f, z - 0.05f), new Vector3(x + 0.05f, 0.84f, z + 0.05f), 0.005f, wood);
            VertexMaterial fish = new VertexMaterial(MaterialCatalog.Skin.Index, new Colour(0.56f, 0.6f, 0.62f));
            int count = (int)(length / 0.24f);
            for (int k = 0; k < count; k++)
            {
                if (random.Chance(0.3f)) continue;
                float x = -hx + 0.2f + (length - 0.4f) * k / Math.Max(1, count - 1);
                float turn = random.Range(-0.3f, 0.3f);
                Vector3 axis = new Vector3(MathF.Sin(turn), 0f, MathF.Cos(turn));
                Kit.Lathe(b, new Vector3(x, 0.94f, 0f) - axis * 0.25f, axis, 0.5f, Kit.Profile((0f, 0.02f), (0.15f, 0.055f), (0.6f, 0.05f), (0.9f, 0.015f), (1f, 0.04f)), 6, 4, fish);
            }
            Kit.Plank(b, new Vector3(hx - 0.3f, 0.92f, 0.28f), new Vector3(hx - 0.05f, 0.92f, 0.3f), Vector3.UnitY, 0.035f, 0.01f, LandIron(random, 1.3f), 0f);
            Kit.Lathe(b, new Vector3(-hx + 0.4f, 0f, 0.7f), Vector3.UnitY, 0.38f, Kit.Profile((0f, 0.17f), (1f, 0.21f)), 8, 1, Timber(random, 0.7f), openEnd: true);
            Kit.Lathe(b, new Vector3(-hx + 0.4f, 0.34f, 0.7f), Vector3.UnitY, 0.01f, _ => 0.2f, 8, 1, Flat(new Vector3(0.3f, 0.1f, 0.08f)));
            Kit.Lathe(b, new Vector3(0.3f, 0f, 0.75f), Vector3.UnitY, 0.32f, Kit.Profile((0f, 0.24f), (1f, 0.3f)), 9, 1, Straw(random, 0.75f), openEnd: true);
            for (int k = 0; k < 4; k++) Kit.Lathe(b, new Vector3(0.3f + random.Range(-0.12f, 0.12f), 0.3f, 0.75f + random.Range(-0.12f, 0.12f)) - Vector3.UnitX * 0.2f, Vector3.UnitX, 0.4f, Kit.Profile((0f, 0.02f), (0.2f, 0.05f), (0.8f, 0.04f), (1f, 0.03f)), 6, 2, fish);
        }

        /// <summary>
        /// The slash a felling leaves: the boughs lopped off the trunks, heaped where they fell in a tangle, their twigs gone brown and their leaves or needles dropping, in the bark of the
        /// wood round the camp. Numbers: radius, bark (kind, then its colour, three per mille, as a stump's).
        /// </summary>
        public static void Slash(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float radius = Math.Clamp(spec.Metres(0), 1.2f, 4f);
            var kind = (Modelling.Procedural.Flora.BarkKind)Math.Clamp(spec.Int(1), 0, 9);
            Vector3 barkColour = spec.Count >= 5 ? new Vector3(spec.Int(2), spec.Int(3), spec.Int(4)) / 1000f : new Vector3(0.8f, 0.72f, 0.62f);
            VertexMaterial bark = Bark(kind, barkColour * 0.5f), dead = Flat(new Vector3(0.36f, 0.27f, 0.17f)), deader = Flat(new Vector3(0.28f, 0.22f, 0.15f));
            int boughs = (int)(radius * 7f);
            for (int k = 0; k < boughs; k++)
            {
                float a = random.Float() * MathF.Tau, r = radius * MathF.Sqrt(random.Float()) * 0.7f;
                Vector3 foot = new Vector3(MathF.Cos(a) * r, Ground + random.Range(0.05f, 0.5f), MathF.Sin(a) * r);
                float yaw = random.Float() * MathF.Tau;
                Vector3 dir = Vector3.Normalize(new Vector3(MathF.Cos(yaw), random.Range(-0.15f, 0.35f), MathF.Sin(yaw)));
                float length = random.Range(1.2f, 2.6f);
                var bough = new List<Vector3> { foot, foot + dir * (length * 0.5f) + new Vector3(0f, random.Range(-0.1f, 0.2f), 0f), foot + dir * length + new Vector3(0f, random.Range(-0.3f, 0.1f), 0f) };
                Kit.Bend(b, bough, random.Range(0.05f, 0.09f), 0.015f, bark);
                // Its twigs off it, and the dead leaves still on them.
                for (int t = 0; t < 3; t++)
                {
                    Vector3 from = Vector3.Lerp(bough[0], bough[2], 0.3f + t * 0.25f);
                    Vector3 twig = Vector3.Normalize(dir + new Vector3(random.Range(-0.8f, 0.8f), random.Range(0f, 0.6f), random.Range(-0.8f, 0.8f))) * random.Range(0.4f, 0.8f);
                    Kit.Pole(b, from, from + twig, 0.018f, bark, 0.006f);
                    Kit.Ball(b, from + twig * 0.85f, new Vector3(random.Range(0.18f, 0.3f), random.Range(0.1f, 0.16f), random.Range(0.18f, 0.3f)), 5, 3, random.Chance(0.5f) ? dead : deader);
                }
            }
        }

        /// <summary>
        /// A stack of hay round a pole, as the land stacks it, bellied and drawn in to a cap of thatch with the pole's end standing out of it, a fork leaning on it and the loose hay
        /// round its foot. Numbers: radius.
        /// </summary>
        public static void Haystack(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float radius = Math.Clamp(spec.Metres(0), 0.9f, 2.4f);
            float high = radius * random.Range(1.5f, 1.9f);
            VertexMaterial hay = Straw(random, random.Range(0.92f, 1.08f)), cap = new VertexMaterial(MaterialCatalog.Thatch.Index, new Colour(new Vector3(0.5f, 0.44f, 0.3f) * random.Range(0.9f, 1.05f)));
            Kit.Lathe(b, new Vector3(0f, -0.1f, 0f), Vector3.UnitY, high * 0.72f, Kit.Profile((0f, radius * 0.9f), (0.45f, radius * 1.06f), (1f, radius * 0.86f)), 12, 3, hay);
            Kit.Lathe(b, new Vector3(0f, high * 0.72f - 0.1f, 0f), Vector3.UnitY, high * 0.36f, Kit.Profile((0f, radius * 0.92f), (0.55f, radius * 0.45f), (1f, 0.05f)), 12, 2, cap);
            Kit.Pole(b, new Vector3(0f, high * 0.8f, 0f), new Vector3(0.05f, high + 0.7f, 0.03f), 0.05f, Weathered(random));
            // A rope round its cap, weighted with stones against the wind.
            b.Tube(new Vector3(0f, high * 0.82f, 0f), new Vector3(0f, high * 0.85f, 0f), radius * 0.72f, radius * 0.7f, 12, Rope(random), capped: false);
            for (int k = 0; k < 4; k++)
            {
                float a = k * MathF.Tau / 4f + 0.4f;
                Vector3 hang = new Vector3(MathF.Cos(a) * radius * 0.95f, high * 0.45f, MathF.Sin(a) * radius * 0.95f);
                Kit.Pole(b, new Vector3(MathF.Cos(a) * radius * 0.7f, high * 0.83f, MathF.Sin(a) * radius * 0.7f), hang, 0.015f, Rope(random));
                Kit.Block(b, hang - Vector3.UnitY * 0.1f, new Vector3(0.12f, 0.1f, 0.1f), a, 0.03f, Dressed(new Vector3(0.45f, 0.44f, 0.4f), random, 1f));
            }
            Vector3 lean = Vector3.Normalize(new Vector3(-0.35f, 1f, 0.1f));
            Vector3 foot = new Vector3(radius * 1.2f, Ground, radius * 0.4f);
            Kit.Pole(b, foot, foot + lean * 2.1f, 0.025f, Weathered(random));
            for (int k = -1; k <= 1; k++) Kit.Pole(b, foot + lean * 2.1f + new Vector3(0f, 0f, k * 0.07f), foot + lean * 2.45f + new Vector3(-0.05f, 0f, k * 0.08f), 0.012f, LandIron(random), 0.004f);
            Kit.Heap(b, new Vector3(0f, Ground - 0.08f, 0f), new Vector2(radius * 1.5f, radius * 1.4f), 0.18f, random.Float(), Straw(random, 0.8f), random, 3, 14);
        }
    }
}

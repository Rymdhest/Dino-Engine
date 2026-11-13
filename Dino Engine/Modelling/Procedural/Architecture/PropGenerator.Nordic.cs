using Dino_Engine.Modelling.Model;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture
{
    // What stands about a northern village (BuildingStyle.Nordic) that isn't a house: the pole its people dance round at midsummer, dressed in birch leaves and flowers,
    // with its two rings hanging from its arms (a midsommarstång); a well drawn with a long pole on a forked post, a stone at its butt to lift the bucket (a brunnsvipp);
    // a rack of poles with the hay hung over it to dry (a hässja); and firewood stacked round under a cap of slabs, as the north stacks it.
    public static partial class PropGenerator
    {
        private static bool BuildNordic(MeshBuilder b, string id, WorldRandom random)
        {
            switch (id)
            {
                case "maypole": BuildMaypole(b, random); return true;
                case "well.sweep": BuildWellSweep(b, random); return true;
                case "hayrack": BuildHayRack(b, random); return true;
                case "woodstack": BuildWoodstack(b, random); return true;
                default: return false;
            }
        }

        private static VertexMaterial Leaves(float shade = 1f) => new VertexMaterial(TextureGenerator.grass, new Colour(new Vector3(0.74f, 1f, 0.6f) * shade));
        private static VertexMaterial Peeled(float shade = 1f) => new VertexMaterial(TextureGenerator.barkBare, new Colour(new Vector3(0.86f, 0.8f, 0.7f) * shade));
        private static VertexMaterial Grey(float shade = 1f) => new VertexMaterial(TextureGenerator.barkBare, new Colour(new Vector3(0.62f, 0.6f, 0.56f) * shade));

        // A round length through the given points, closed on itself, as a wreath.
        private static void Ring(MeshBuilder b, Vector3 centre, Vector3 a, Vector3 c, float radius, float thick, int pieces, VertexMaterial material)
        {
            var points = new List<Vector3>(pieces + 1);
            for (int i = 0; i <= pieces; i++)
            {
                float angle = MathF.Tau * i / pieces;
                points.Add(centre + (a * MathF.Cos(angle) + c * MathF.Sin(angle)) * radius);
            }
            Bend(b, points, thick, thick, 6, material);
        }

        // Flowers tucked into the green: white, yellow, blue and red, as the meadows give them at midsummer.
        private static void Flowers(MeshBuilder b, Vector3 at, float size, WorldRandom random)
        {
            Vector3[] colours = { new Vector3(0.66f, 0.66f, 0.62f), new Vector3(0.56f, 0.42f, 0.04f), new Vector3(0.12f, 0.2f, 0.56f), new Vector3(0.56f, 0.07f, 0.1f) };
            var flower = new VertexMaterial(TextureGenerator.flat, new Colour(colours[random.Int(colours.Length)] * random.Range(0.9f, 1.1f)));
            b.Box(at, new Vector3(size), flower);
        }

        // A midsummer pole: a tall pole wound with two garlands of birch leaves crossing as they climb, an arm across it, a ring of leaves hanging from each end of the
        // arm, flowers all over the green, and a tuft of leaves on top. It stands on a village's green, and it's the one thing a village has that says it's the north's.
        private static void BuildMaypole(MeshBuilder b, WorldRandom random)
        {
            const float Top = 13.2f, ArmY = 9.4f, Arm = 3.1f, Wreath = 1.2f;
            b.Tube(new Vector3(0f, -1.2f, 0f), new Vector3(0f, Top, 0f), 0.22f, 0.15f, 8, Peeled());

            // The garlands, wound both ways round it.
            foreach (float turn in new[] { 1f, -1f })
            {
                var points = new List<Vector3>();
                const int Steps = 44;
                for (int i = 0; i <= Steps; i++)
                {
                    float t = i / (float)Steps;
                    float angle = turn * t * MathF.Tau * 4.5f + (turn > 0f ? 0f : MathF.PI);
                    float y = 0.6f + t * (Top - 1.2f);
                    float r = MathHelper.Lerp(0.26f, 0.19f, t);
                    points.Add(new Vector3(MathF.Cos(angle) * r, y, MathF.Sin(angle) * r));
                }
                Bend(b, points, 0.15f, 0.12f, 5, Leaves(turn > 0f ? 1f : 0.88f));
            }

            // The arm, dressed in green, and a ring hanging from each end of it.
            b.Tube(new Vector3(-Arm - 0.1f, ArmY, 0f), new Vector3(Arm + 0.1f, ArmY, 0f), 0.13f, 0.13f, 7, Peeled());
            b.Tube(new Vector3(-Arm, ArmY, 0f), new Vector3(Arm, ArmY, 0f), 0.24f, 0.24f, 7, Leaves(0.95f));
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 centre = new Vector3(s * (Arm - 0.25f), ArmY - Wreath - 0.12f, 0f);
                Ring(b, centre, Vector3.UnitX, Vector3.UnitY, Wreath, 0.22f, 16, Leaves(0.92f));
                for (int i = 0; i < 9; i++)
                {
                    float angle = MathF.Tau * (i + random.Range(-0.3f, 0.3f)) / 9f;
                    Flowers(b, centre + new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0f) * Wreath + new Vector3(0f, 0f, 0.2f * (i % 2 == 0 ? 1f : -1f)), 0.11f, random);
                }
                // What it hangs by.
                b.Tube(new Vector3(s * (Arm - 0.25f), ArmY, 0f), centre + new Vector3(0f, Wreath, 0f), 0.05f, 0.05f, 5, Peeled(0.8f));
            }
            for (int i = 0; i < 26; i++)
            {
                float y = random.Range(1.2f, Top - 1f);
                float angle = random.Range(0f, MathF.Tau);
                Flowers(b, new Vector3(MathF.Cos(angle) * 0.36f, y, MathF.Sin(angle) * 0.36f), 0.1f, random);
            }
            for (int i = 0; i < 8; i++) Flowers(b, new Vector3(random.Range(-Arm, Arm), ArmY + random.Range(-0.1f, 0.25f), random.Range(-0.28f, 0.28f)), 0.1f, random);

            // A tuft of leaves at the top.
            Lathe(b, new Vector3(0f, Top - 0.6f, 0f), Vector3.UnitY, 1.8f, t => 0.55f * MathF.Pow(MathF.Max(1f - t, 0f), 0.7f) * (0.75f + 0.25f * MathF.Sin(t * 9f)), 8, 5, Leaves(1.05f));
        }

        // A well drawn with a sweep: a curb of boards round the shaft, a forked post behind it, and a long pole pivoting in the fork, its tip over the well with the rod and
        // the bucket hanging from it, and a stone lashed to its butt that does the lifting. Long along z, the well towards +z.
        private static void BuildWellSweep(MeshBuilder b, WorldRandom random)
        {
            VertexMaterial boards = Wood(118, 98, 76);
            VertexMaterial dark = DarkWood;
            const float WellZ = 2.45f, Curb = 1.25f, CurbTop = 1.35f;

            // The curb, square, of thick boards, with the water dark inside it.
            b.Box(new Vector3(0f, CurbTop * 0.5f - 0.3f, WellZ + Curb - 0.12f), new Vector3(Curb, CurbTop * 0.5f + 0.3f, 0.12f), boards);
            b.Box(new Vector3(0f, CurbTop * 0.5f - 0.3f, WellZ - Curb + 0.12f), new Vector3(Curb, CurbTop * 0.5f + 0.3f, 0.12f), boards);
            b.Box(new Vector3(Curb - 0.12f, CurbTop * 0.5f - 0.3f, WellZ), new Vector3(0.12f, CurbTop * 0.5f + 0.3f, Curb - 0.24f), boards);
            b.Box(new Vector3(-Curb + 0.12f, CurbTop * 0.5f - 0.3f, WellZ), new Vector3(0.12f, CurbTop * 0.5f + 0.3f, Curb - 0.24f), boards);
            VertexMaterial water = new VertexMaterial(TextureGenerator.mirror, new Colour(30, 38, 42));
            b.Quad(new Vector3(-Curb + 0.24f, 0.55f, WellZ + Curb - 0.24f), new Vector3(Curb - 0.24f, 0.55f, WellZ + Curb - 0.24f), new Vector3(Curb - 0.24f, 0.55f, WellZ - Curb + 0.24f), new Vector3(-Curb + 0.24f, 0.55f, WellZ - Curb + 0.24f), water);

            // The forked post, and the pin through its fork.
            Vector3 pivot = new Vector3(0f, 4.7f, -1f);
            b.Tube(new Vector3(0f, -1f, pivot.Z), new Vector3(0f, pivot.Y - 0.5f, pivot.Z), 0.24f, 0.2f, 7, Grey());
            foreach (float s in new[] { -1f, 1f }) b.Tube(new Vector3(0f, pivot.Y - 0.55f, pivot.Z), new Vector3(s * 0.28f, pivot.Y + 0.35f, pivot.Z), 0.1f, 0.08f, 6, Grey());
            b.Tube(pivot - Vector3.UnitX * 0.42f, pivot + Vector3.UnitX * 0.42f, 0.045f, 0.045f, 5, dark);

            // The sweep, from its stone near the ground behind the post up over the well.
            Vector3 butt = new Vector3(0f, 1.45f, -3.5f);
            Vector3 along = (pivot - butt).Normalized();
            float reach = (WellZ - pivot.Z) / along.Z;
            Vector3 tip = pivot + along * reach;
            b.Tube(butt - along * 0.25f, tip, 0.19f, 0.09f, 7, Grey(0.95f));
            Vector3 stone = butt + Vector3.UnitY * 0.05f;
            b.Box(stone, new Vector3(0.42f, 0.36f, 0.48f), Rough(150));
            b.Box(stone + new Vector3(0f, 0.36f, 0f), new Vector3(0.44f, 0.05f, 0.08f), new VertexMaterial(TextureGenerator.thatch, new Colour(150, 128, 92)));

            // The rod down from the tip, and the bucket on it, set on the curb's edge.
            Vector3 hook = tip - Vector3.UnitY * 0.1f;
            float bucketTop = CurbTop + 0.85f;
            b.Tube(hook, new Vector3(hook.X, bucketTop, hook.Z), 0.05f, 0.05f, 5, Grey(0.9f));
            b.Tube(new Vector3(hook.X, bucketTop - 0.7f, hook.Z), new Vector3(hook.X, bucketTop, hook.Z), 0.3f, 0.36f, 9, boards);
            foreach (float y in new[] { bucketTop - 0.55f, bucketTop - 0.12f }) b.Tube(new Vector3(hook.X, y - 0.04f, hook.Z), new Vector3(hook.X, y + 0.04f, hook.Z), 0.37f, 0.37f, 9, Iron, capped: false);
        }

        // Hay hung to dry on a rack of poles: posts down a line, rails between them, and the hay draped over the rails in a long wall, golden-green, thickest at its foot,
        // the posts' tops standing out of it, as the north's meadows are full of in the late summer.
        private static void BuildHayRack(MeshBuilder b, WorldRandom random)
        {
            const float Half = 7.2f, HayTop = 3.25f, HayFoot = 0.55f;
            int posts = 7;
            for (int i = 0; i < posts; i++)
            {
                float x = -Half + 2f * Half * i / (posts - 1);
                b.Tube(new Vector3(x, -0.8f, 0f), new Vector3(x + random.Range(-0.06f, 0.06f), HayTop + random.Range(0.55f, 0.85f), random.Range(-0.05f, 0.05f)), 0.11f, 0.08f, 6, Grey(random.Range(0.85f, 1.05f)));
            }
            // The poles laid at each end, propping it against the wind.
            foreach (float s in new[] { -1f, 1f })
            {
                b.Tube(new Vector3(s * Half, HayTop - 0.3f, 0f), new Vector3(s * (Half + 0.55f), -0.3f, 0.35f), 0.08f, 0.07f, 5, Grey(0.9f));
                b.Tube(new Vector3(s * Half, HayTop - 0.3f, 0f), new Vector3(s * (Half + 0.55f), -0.3f, -0.35f), 0.08f, 0.07f, 5, Grey(0.9f));
            }

            // The hay: hung over each of five rails in a course of its own, splaying as it hangs and lapping the course under it as a roof's courses do, each a little
            // fresher or more bleached than the next, lumpy along its length and sagging between the posts, where nothing holds the rail up.
            Vector3 hayColour = new Vector3(0.82f, 0.9f, 0.6f) * random.Range(0.92f, 1.06f);
            VertexMaterial hay = new VertexMaterial(TextureGenerator.thatch, new Colour(hayColour));
            float seed = random.Float() * 30f;
            const int Courses = 5;
            for (int c = 0; c < Courses; c++)
            {
                float rail = HayFoot + (HayTop - HayFoot) * (c + 1) / Courses;
                float bottom = c == 0 ? HayFoot - 0.2f : rail - (HayTop - HayFoot) / Courses * 1.4f;
                var course = new VertexMaterial(TextureGenerator.thatch, new Colour(hayColour * random.Range(0.86f, 1.1f)));
                int k = c;
                Vector3 Course(float u, float v, float side)
                {
                    float x = (u * 2f - 1f) * (Half + 0.15f);
                    float y = bottom + (rail - bottom) * v;
                    float edge = MathF.Min(MathF.Min(u, 1f - u) * 10f, 1f);
                    float splay = (0.1f + 0.34f * (1f - v) * (1f - k * 0.08f)) * (0.6f + 0.4f * edge);
                    splay += 0.05f * MathF.Sin(x * 2.1f + seed + k * 1.7f) + 0.03f * MathF.Sin(x * 5.3f + k);
                    float sag = 0.1f * MathF.Abs(MathF.Sin((x + Half) / (2f * Half) * MathF.PI * (posts - 1)));
                    return new Vector3(x, y - sag * v, side * splay);
                }
                b.Surface(16, 3, (u, v) => Course(u, v, 1f), course);
                b.Surface(16, 3, (u, v) => Course(1f - u, v, -1f), Shaded(course, 0.94f));
                // Its ends closed, so that nothing shows through the wall of hay from the side.
                b.Quad(Course(1f, 0f, 1f), Course(1f, 0f, -1f), Course(1f, 1f, -1f), Course(1f, 1f, 1f), course);
                b.Quad(Course(0f, 0f, -1f), Course(0f, 0f, 1f), Course(0f, 1f, 1f), Course(0f, 1f, -1f), course);
            }
            // The crest over the top rail.
            b.Tube(new Vector3(-Half - 0.15f, HayTop - 0.02f, 0f), new Vector3(Half + 0.15f, HayTop - 0.02f, 0f), 0.17f, 0.17f, 6, hay);
        }

        // Firewood stacked round, the split ends out in rings, under a cap of slabs that sheds the rain: the north's way of drying a winter's wood.
        private static void BuildWoodstack(MeshBuilder b, WorldRandom random)
        {
            const float Radius = 1.5f, Tall = 2.3f, Log = 0.17f;
            // The heart of it, dark between the ends.
            b.Tube(new Vector3(0f, -0.2f, 0f), new Vector3(0f, Tall, 0f), Radius - 0.08f, Radius - 0.14f, 14, new VertexMaterial(TextureGenerator.wood, new Colour(70, 56, 42)));
            int rows = (int)(Tall / (Log * 2f));
            for (int row = 0; row < rows; row++)
            {
                float y = Log + row * Log * 2f;
                float r = MathHelper.Lerp(Radius, Radius - 0.08f, y / Tall);
                int count = (int)(MathF.Tau * r / (Log * 2.1f));
                float shift = row % 2 == 0 ? 0f : 0.5f;
                for (int i = 0; i < count; i++)
                {
                    float angle = MathF.Tau * (i + shift + random.Range(-0.15f, 0.15f)) / count;
                    Vector3 out_ = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                    float stick = random.Range(-0.05f, 0.08f);
                    Vector3 end = out_ * (r + stick) + Vector3.UnitY * (y + random.Range(-0.03f, 0.03f));
                    var grain = new VertexMaterial(TextureGenerator.barkBare, new Colour(new Vector3(0.84f, 0.74f, 0.56f) * random.Range(0.82f, 1.08f)));
                    b.Tube(end - out_ * 0.3f, end, Log * random.Range(0.88f, 1.05f), Log * random.Range(0.88f, 1.05f), 5, grain);
                }
            }
            // The cap: slabs laid round in a low cone, greyed by the weather, reaching only a little past the wood.
            VertexMaterial slabs = new VertexMaterial(TextureGenerator.shingle, new Colour(new Vector3(0.5f, 0.47f, 0.42f) * random.Range(0.9f, 1.05f)));
            Lathe(b, new Vector3(0f, Tall - 0.05f, 0f), Vector3.UnitY, 1.25f, t => (Radius + 0.16f) * (1f - t) + 0.05f, 16, 2, slabs);
            Lathe(b, new Vector3(0f, Tall - 0.18f, 0f), Vector3.UnitY, 0.13f, t => Radius + 0.16f, 16, 1, Shaded(slabs, 0.8f));
        }

        private static VertexMaterial Shaded(VertexMaterial material, float shade) => new VertexMaterial(material.materialTextureIndex, new Colour(material.colour.ToVector3() * shade));
    }
}

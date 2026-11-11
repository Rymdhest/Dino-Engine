using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture.Arena;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture
{
    // The countryside's own clutter: what stands about a farm and a yard, what a road passes, the landmarks of a land, the Ossuary's digs and the Reich's pens (LORE.md: a pen
    // should have cages, and a keep its mark). Each stands on y = 0 with its front towards +z, sized to its line in StructureCatalog. The ground is drawn 0.15 m above where
    // things stand (ArenaStone.Ground), so whatever lies flat on it starts above that.
    public static partial class PropGenerator
    {
        private const float GroundTop = 0.18f;

        private static bool BuildCountryside(MeshBuilder b, string id, WorldRandom random, List<AssetLight> lights)
        {
            switch (id)
            {
                case "hives": BuildHives(b, random); return true;
                case "trough": BuildTrough(b); return true;
                case "barrow": BuildBarrow(b, random); return true;
                case "sacks": BuildSacks(b, random); return true;
                case "oven": BuildOven(b); return true;
                case "jars": BuildJars(b, random); return true;
                case "laundry": BuildLaundry(b, random); return true;
                case "coop": BuildCoop(b); return true;
                case "lamp.pillar": BuildPillarLamp(b, lights); return true;
                case "lantern.paper": BuildPaperLantern(b, lights); return true;
                case "shrine.wayside": BuildWaysideShrine(b); return true;
                case "milestone": BuildMilestone(b); return true;
                case "stand.hunting": BuildHuntingStand(b); return true;
                case "windmill": BuildWindmill(b); return true;
                case "fossil": BuildFossil(b, random); return true;
                case "mound.termite": BuildTermiteMound(b, random); return true;
                case "dig": BuildDig(b, random, lights); return true;
                case "dig.bones": BuildBoneRows(b, random); return true;
                case "pen.cage": BuildCage(b, random); return true;
                case "pen.trough": BuildFeedTrough(b, random); return true;
                case "pen.rack": BuildMeatRack(b, random); return true;
                case "pen.banner": BuildBanner(b, random); return true;
                default: return false;
            }
        }

        // ---------------------------------------------------------------- Shared

        private static VertexMaterial Clay(int r, int g, int b) => new VertexMaterial(TextureGenerator.plaster, new Colour(r, g, b));
        private static VertexMaterial BoneOf(int r, int g, int b) => new VertexMaterial(TextureGenerator.bone, new Colour(r, g, b));
        private static VertexMaterial Earth(int r, int g, int b) => new VertexMaterial(TextureGenerator.soil, new Colour(r, g, b));
        private static VertexMaterial Shadowed => new VertexMaterial(TextureGenerator.flat, new Colour(16, 13, 11));
        private static VertexMaterial Meat(float shade = 1f) => new VertexMaterial(TextureGenerator.leather, new Colour(new Vector3(0.44f, 0.12f, 0.095f) * shade));

        /// <summary>
        /// A surface of revolution about <paramref name="axis"/> from <paramref name="foot"/>, its radius a function of how far along it is (0 to 1), facing out. A radius that
        /// goes to nothing at the end closes it there.
        /// </summary>
        private static void Lathe(MeshBuilder b, Vector3 foot, Vector3 axis, float length, Func<float, float> radius, int around, int along, VertexMaterial material)
        {
            axis = axis.Normalized();
            Vector3 e1 = MathF.Abs(axis.Y) > 0.9f ? Vector3.UnitX : Vector3.Cross(Vector3.UnitY, axis).Normalized();
            Vector3 e2 = Vector3.Cross(e1, axis);
            b.Surface(around, along, (u, v) =>
            {
                float angle = -u * MathF.Tau;
                return foot + axis * (v * length) + (e1 * MathF.Cos(angle) + e2 * MathF.Sin(angle)) * radius(v);
            }, material);
        }

        /// <summary>A radius along a lathe, as straight runs between the given points (how far along, radius).</summary>
        private static Func<float, float> Profile(params (float T, float R)[] points) => t =>
        {
            if (t <= points[0].T) return points[0].R;
            for (int i = 1; i < points.Length; i++)
            {
                if (t > points[i].T) continue;
                float s = (t - points[i - 1].T) / (points[i].T - points[i - 1].T);
                return points[i - 1].R + (points[i].R - points[i - 1].R) * s;
            }
            return points[^1].R;
        };

        /// <summary>A round length bent through the given points, thinning from one end to the other.</summary>
        private static void Bend(MeshBuilder b, IReadOnlyList<Vector3> points, float from, float to, int sides, VertexMaterial material)
        {
            for (int i = 1; i < points.Count; i++)
            {
                float r0 = from + (to - from) * (i - 1) / (points.Count - 1);
                float r1 = from + (to - from) * i / (points.Count - 1);
                b.Tube(points[i - 1], points[i], r0, r1, sides, material);
            }
        }

        /// <summary>A long bone: a shaft with a knuckle at each end.</summary>
        private static void LongBone(MeshBuilder b, Vector3 from, Vector3 to, float thick, VertexMaterial bone)
        {
            Vector3 along = (to - from).Normalized();
            b.Tube(from + along * thick, to - along * thick, thick * 0.6f, thick * 0.55f, 7, bone);
            foreach (Vector3 end in new[] { from, to })
            {
                Vector3 side = Vector3.Cross(along, Vector3.UnitY);
                side = side.LengthSquared > 1e-4f ? side.Normalized() : Vector3.UnitX;
                b.Add(IcoSphereGenerator.CreateIcosphere(1, bone), end - side * thick * 0.35f, new Vector3(thick * 0.95f));
                b.Add(IcoSphereGenerator.CreateIcosphere(1, bone), end + side * thick * 0.35f, new Vector3(thick * 0.85f));
            }
        }

        /// <summary>A rib arching from the top of a spine down one side and in under the ground, in the plane x = <paramref name="x"/>.</summary>
        private static void Rib(MeshBuilder b, float x, float height, float width, float side, float rake, float end, float thick, VertexMaterial bone)
        {
            var points = new List<Vector3>();
            const int Segments = 6;
            for (int i = 0; i <= Segments; i++)
            {
                float angle = end * i / Segments;
                points.Add(new Vector3(x - rake * angle, height * MathF.Cos(angle), side * width * MathF.Sin(angle)));
            }
            Bend(b, points, thick, thick * 0.6f, 6, bone);
        }

        /// <summary>A ladder from its foot to its top, its rails <paramref name="halfWidth"/> either side.</summary>
        private static void PropLadder(MeshBuilder b, Vector3 foot, Vector3 top, float halfWidth, VertexMaterial wood)
        {
            Vector3 up = top - foot;
            Vector3 across = Vector3.Cross(up, Vector3.UnitY);
            across = (across.LengthSquared > 1e-6f ? across.Normalized() : Vector3.UnitX) * halfWidth;
            b.Beam(foot - across, top - across, 0.07f, wood);
            b.Beam(foot + across, top + across, 0.07f, wood);
            int rungs = Math.Max((int)(up.Length / 0.55f), 2);
            for (int i = 1; i < rungs; i++)
            {
                Vector3 at = foot + up * (i / (float)rungs);
                b.Beam(at - across, at + across, 0.045f, wood);
            }
        }

        // ---------------------------------------------------------------- Farm and yard

        // Straw skeps on a bench, their doors to the morning.
        private static void BuildHives(MeshBuilder b, WorldRandom random)
        {
            VertexMaterial straw = new VertexMaterial(TextureGenerator.thatch, new Colour(214, 184, 124));
            b.Box(new Vector3(0f, 0.86f, 0f), new Vector3(2.15f, 0.08f, 0.72f), Wood(128, 100, 74));
            foreach (float x in new[] { -1.8f, 1.8f })
            {
                foreach (float z in new[] { -0.52f, 0.52f }) b.Beam(new Vector3(x, -0.3f, z), new Vector3(x, 0.8f, z), 0.07f, DarkWood);
            }
            b.Beam(new Vector3(-1.8f, 0.4f, 0.52f), new Vector3(1.8f, 0.4f, 0.52f), 0.05f, DarkWood);
            foreach (float x in new[] { -1.38f, 0f, 1.38f })
            {
                float size = random.Range(0.9f, 1.08f);
                float radius = 0.52f * size, height = 1.08f * size;
                Lathe(b, new Vector3(x, 0.94f, 0f), Vector3.UnitY, height, t => radius * MathF.Pow(MathF.Max(1f - t * t, 0f), 0.42f) + 0.02f * (1f - t), 12, 8, straw);
                b.Quad(new Vector3(x - 0.09f, 0.95f, radius + 0.025f), new Vector3(x + 0.09f, 0.95f, radius + 0.025f), new Vector3(x + 0.09f, 1.07f, radius + 0.015f), new Vector3(x - 0.09f, 1.07f, radius + 0.015f), Shadowed);
            }
        }

        // A trough of thick boards on two stones, full to the brim, the water dark under the sky it gives back.
        private static void BuildTrough(MeshBuilder b)
        {
            VertexMaterial boards = Wood(118, 92, 66);
            foreach (float x in new[] { -1.4f, 1.4f }) b.Box(new Vector3(x, 0.1f, 0f), new Vector3(0.32f, 0.26f, 0.52f), Rough(165));
            b.Box(new Vector3(0f, 0.42f, 0f), new Vector3(2.05f, 0.1f, 0.66f), boards);
            b.Box(new Vector3(0f, 0.8f, 0.58f), new Vector3(2.05f, 0.3f, 0.08f), boards);
            b.Box(new Vector3(0f, 0.8f, -0.58f), new Vector3(2.05f, 0.3f, 0.08f), boards);
            b.Box(new Vector3(1.97f, 0.8f, 0f), new Vector3(0.08f, 0.3f, 0.5f), boards);
            b.Box(new Vector3(-1.97f, 0.8f, 0f), new Vector3(0.08f, 0.3f, 0.5f), boards);
            VertexMaterial water = new VertexMaterial(TextureGenerator.mirror, new Colour(46, 56, 60));
            b.Quad(new Vector3(-1.89f, 0.98f, 0.5f), new Vector3(1.89f, 0.98f, 0.5f), new Vector3(1.89f, 0.98f, -0.5f), new Vector3(-1.89f, 0.98f, -0.5f), water);
        }

        // A wheelbarrow of boards on one solid wheel, its handles running under the tray to the axle, left with a load of firewood.
        private static void BuildBarrow(MeshBuilder b, WorldRandom random)
        {
            VertexMaterial boards = Wood(140, 112, 82);
            Vector3 hub = new Vector3(0f, 0.44f, 1.2f);
            b.Tube(hub, hub + Vector3.UnitX * 0.07f, 0.44f, 0.44f, 12, DarkWood);
            b.Tube(hub, hub - Vector3.UnitX * 0.07f, 0.44f, 0.44f, 12, DarkWood);
            b.Beam(hub - Vector3.UnitX * 0.18f, hub + Vector3.UnitX * 0.18f, 0.04f, Iron);

            b.Box(new Vector3(0f, 0.8f, 0.15f), new Vector3(0.55f, 0.05f, 0.62f), boards);
            b.Box(new Vector3(0.6f, 1.04f, 0.15f), new Vector3(0.05f, 0.24f, 0.72f), boards);
            b.Box(new Vector3(-0.6f, 1.04f, 0.15f), new Vector3(0.05f, 0.24f, 0.72f), boards);
            b.Box(new Vector3(0f, 1.04f, 0.82f), new Vector3(0.55f, 0.24f, 0.05f), boards);
            b.Box(new Vector3(0f, 1.04f, -0.52f), new Vector3(0.55f, 0.24f, 0.05f), boards);
            foreach (float s in new[] { -1f, 1f })
            {
                b.Beam(new Vector3(s * 0.14f, 0.44f, 1.2f), new Vector3(s * 0.45f, 0.6f, 0.7f), 0.05f, DarkWood);
                b.Beam(new Vector3(s * 0.45f, 0.6f, 0.7f), new Vector3(s * 0.52f, 0.72f, -1.62f), 0.05f, DarkWood);
                b.Beam(new Vector3(s * 0.46f, 0.64f, -0.4f), new Vector3(s * 0.47f, 0f, -0.48f), 0.05f, DarkWood);
            }
            VertexMaterial logs = new VertexMaterial(TextureGenerator.barkFurrowed, new Colour(170, 150, 128));
            for (int i = 0; i < 5; i++)
            {
                float x = -0.36f + i * 0.18f;
                float y = 0.95f + (i % 2) * 0.07f;
                b.Tube(new Vector3(x, y, -0.45f), new Vector3(x + random.Range(-0.05f, 0.05f), y, 0.75f), 0.09f, 0.08f, 6, logs);
            }
        }

        // Closed at both ends, as one lies on its side with its bottom in view.
        private static Func<float, float> SackProfile(float belly) => Profile(
            (0f, 0f), (0.02f, belly * 0.7f), (0.07f, belly * 0.97f), (0.4f, belly), (0.7f, belly * 0.74f), (0.83f, belly * 0.36f), (0.87f, belly * 0.3f), (0.94f, belly * 0.5f), (1f, 0f));

        private static VertexMaterial Sackcloth(WorldRandom random)
        {
            float shade = random.Range(0.88f, 1.06f);
            return Cloth((int)(164 * shade), (int)(140 * shade), (int)(104 * shade));
        }

        // Grain in sacks tied at the neck, three standing and one fallen over against them.
        private static void BuildSacks(MeshBuilder b, WorldRandom random)
        {
            (Vector3 At, float Height, float Belly)[] standing =
            {
                (new Vector3(-0.27f, 0f, -0.65f), 1.15f, 0.44f),
                (new Vector3(0.73f, 0f, -0.77f), 1.05f, 0.42f),
                (new Vector3(0.33f, 0f, 0f), 0.95f, 0.4f),
            };
            foreach (var sack in standing) Lathe(b, sack.At, Vector3.UnitY, sack.Height * random.Range(0.95f, 1.05f), SackProfile(sack.Belly), 10, 10, Sackcloth(random));
            Vector3 lying = new Vector3(MathF.Cos(0.4f), 0f, MathF.Sin(0.4f));
            Lathe(b, new Vector3(-0.77f, 0.36f, 0.4f), lying, 1.05f, SackProfile(0.38f), 10, 10, Sackcloth(random));
        }

        // A bread oven of clay over a stone hearth: a dome with its mouth built out in front, the opening dark and arched, and a slab under it to rest the loaves on.
        private static void BuildOven(MeshBuilder b)
        {
            VertexMaterial clay = Clay(200, 140, 98);
            b.Tube(new Vector3(0f, -0.5f, 0f), new Vector3(0f, 0.72f, 0f), 1.62f, 1.56f, 12, Rough(176));
            Lathe(b, new Vector3(0f, 0.7f, 0f), Vector3.UnitY, 1.55f, t => 1.36f * MathF.Pow(MathF.Max(1f - t * t, 0f), 0.48f) + 0.04f * (1f - t), 14, 8, clay);
            b.Box(new Vector3(0f, 1.12f, 1.3f), new Vector3(0.6f, 0.42f, 0.36f), clay);
            const float Face = 1.665f, Half = 0.34f, Spring = 1.18f;
            b.Quad(new Vector3(-Half, 0.74f, Face), new Vector3(Half, 0.74f, Face), new Vector3(Half, Spring, Face), new Vector3(-Half, Spring, Face), Shadowed);
            const int Pieces = 6;
            for (int i = 0; i < Pieces; i++)
            {
                float a0 = MathF.PI * i / Pieces, a1 = MathF.PI * (i + 1) / Pieces;
                b.Triangle(new Vector3(0f, Spring, Face), new Vector3(MathF.Cos(a0) * Half, Spring + MathF.Sin(a0) * 0.3f, Face), new Vector3(MathF.Cos(a1) * Half, Spring + MathF.Sin(a1) * 0.3f, Face), Shadowed);
            }
            b.Box(new Vector3(0f, 0.74f, 1.82f), new Vector3(0.5f, 0.05f, 0.22f), Stone(180));
        }

        // Big jars of fired clay for oil, grain and water, of a few sizes, one with its lid on.
        private static void BuildJars(MeshBuilder b, WorldRandom random)
        {
            (float X, float Z, float Height)[] jars = { (-0.7f, -0.35f, 1.85f), (0.45f, -0.5f, 1.55f), (0.85f, 0.45f, 1.2f), (-0.25f, 0.55f, 1.35f) };
            Vector3[] clays = { new Vector3(0.75f, 0.44f, 0.3f), new Vector3(0.7f, 0.4f, 0.27f), new Vector3(0.8f, 0.52f, 0.35f), new Vector3(0.73f, 0.47f, 0.33f) };
            for (int j = 0; j < jars.Length; j++)
            {
                float h = jars[j].Height * random.Range(0.94f, 1.04f);
                float belly = h * 0.3f;
                VertexMaterial clay = new VertexMaterial(TextureGenerator.plaster, new Colour(clays[j]));
                Vector3 foot = new Vector3(jars[j].X, 0f, jars[j].Z);
                Lathe(b, foot, Vector3.UnitY, h, Profile((0f, belly * 0.42f), (0.12f, belly * 0.8f), (0.45f, belly), (0.75f, belly * 0.7f), (0.86f, belly * 0.42f), (0.93f, belly * 0.4f), (0.97f, belly * 0.5f), (1f, belly * 0.5f)), 12, 12, clay);
                float rim = belly * 0.5f, inside = belly * 0.36f;
                if (j == 2)
                {
                    // A lid of the same clay, with a knob to lift it by.
                    b.Tube(foot + Vector3.UnitY * (h - 0.02f), foot + Vector3.UnitY * (h + 0.05f), rim + 0.04f, rim + 0.02f, 12, clay);
                    b.Tube(foot + Vector3.UnitY * (h + 0.05f), foot + Vector3.UnitY * (h + 0.14f), 0.07f, 0.05f, 8, clay);
                    continue;
                }
                // The rim turns in over the dark inside, which sits just under it, so that nothing behind the jar shows through its mouth.
                b.Surface(12, 1, (u, v) =>
                {
                    float angle = -u * MathF.Tau;
                    float r = rim + (inside - rim) * v;
                    return foot + new Vector3(MathF.Cos(angle) * r, h, MathF.Sin(angle) * r);
                }, clay);
                b.Tube(foot + Vector3.UnitY * (h - 0.06f), foot + Vector3.UnitY * (h - 0.005f), inside + 0.01f, inside + 0.01f, 12, Shadowed);
            }
        }

        // A line of washing between two posts, sagging, each piece hung by its top edge and stirred a little.
        private static void BuildLaundry(MeshBuilder b, WorldRandom random)
        {
            const float Span = 3.4f, High = 3.3f, Sag = 0.28f;
            foreach (float x in new[] { -Span, Span }) b.Beam(new Vector3(x, -0.4f, 0f), new Vector3(x, High + 0.25f, 0f), 0.07f, DarkWood);
            float LineY(float x) => High - Sag * (1f - (x / Span) * (x / Span));
            VertexMaterial cord = new VertexMaterial(TextureGenerator.flat, new Colour(150, 136, 110));
            const int Lengths = 8;
            for (int i = 0; i < Lengths; i++)
            {
                float x0 = -Span + 2f * Span * i / Lengths, x1 = -Span + 2f * Span * (i + 1) / Lengths;
                b.Beam(new Vector3(x0, LineY(x0), 0f), new Vector3(x1, LineY(x1), 0f), 0.025f, cord);
            }
            // Linen, a faded blue, ochre, a green and a brown: what a village dyes with.
            Vector3[] colours =
            {
                new Vector3(0.88f, 0.86f, 0.8f), new Vector3(0.38f, 0.48f, 0.6f), new Vector3(0.74f, 0.58f, 0.32f),
                new Vector3(0.44f, 0.52f, 0.38f), new Vector3(0.8f, 0.76f, 0.68f), new Vector3(0.58f, 0.44f, 0.34f),
            };
            float at = -Span + 0.35f;
            while (true)
            {
                float width = random.Range(0.6f, 1.15f);
                if (at + width > Span - 0.35f) break;
                float height = random.Range(0.8f, 1.5f);
                float left = at, right = at + width, phase = random.Float() * MathF.Tau;
                VertexMaterial cloth = new VertexMaterial(TextureGenerator.cloth, new Colour(colours[random.Int(colours.Length)]));
                Vector3 Point(float u, float v)
                {
                    float x = left + (right - left) * u;
                    float billow = (0.08f * MathF.Sin(u * MathF.PI) + 0.03f * MathF.Sin(u * 7f + phase)) * v;
                    return new Vector3(x, LineY(x) - 0.03f - v * height, billow);
                }
                b.Surface(3, 3, (u, v) => Point(1f - u, v), cloth);
                b.Surface(3, 3, (u, v) => Point(u, v) - new Vector3(0f, 0f, 0.012f), cloth);
                at = right + random.Range(0.12f, 0.4f);
            }
        }

        // A hen house of boards up on legs, out of the foxes' way, under a little roof, with a ramp up to its door.
        private static void BuildCoop(MeshBuilder b)
        {
            VertexMaterial boards = Wood(146, 118, 88);
            VertexMaterial roof = new VertexMaterial(TextureGenerator.shingle, new Colour(156, 136, 114));
            foreach (float x in new[] { -1.25f, 1.25f })
            {
                foreach (float z in new[] { -0.85f, 0.85f }) b.Beam(new Vector3(x, -0.3f, z), new Vector3(x, 0.72f, z), 0.07f, DarkWood);
            }
            b.Box(new Vector3(0f, 1.36f, 0f), new Vector3(1.4f, 0.64f, 1f), boards);
            b.Quad(new Vector3(-1.62f, 1.88f, 1.26f), new Vector3(1.62f, 1.88f, 1.26f), new Vector3(1.62f, 2.6f, 0f), new Vector3(-1.62f, 2.6f, 0f), roof);
            b.Quad(new Vector3(1.62f, 1.88f, -1.26f), new Vector3(-1.62f, 1.88f, -1.26f), new Vector3(-1.62f, 2.6f, 0f), new Vector3(1.62f, 2.6f, 0f), roof);
            b.Triangle(new Vector3(1.4f, 2f, 1f), new Vector3(1.4f, 2f, -1f), new Vector3(1.4f, 2.57f, 0f), boards);
            b.Triangle(new Vector3(-1.4f, 2f, -1f), new Vector3(-1.4f, 2f, 1f), new Vector3(-1.4f, 2.57f, 0f), boards);
            b.Beam(new Vector3(-1.66f, 2.62f, 0f), new Vector3(1.66f, 2.62f, 0f), 0.06f, DarkWood);
            b.Quad(new Vector3(0.25f, 0.86f, 1.01f), new Vector3(0.65f, 0.86f, 1.01f), new Vector3(0.65f, 1.26f, 1.01f), new Vector3(0.25f, 1.26f, 1.01f), Shadowed);
            b.Quad(new Vector3(0.2f, 0.02f, 1.95f), new Vector3(0.7f, 0.02f, 1.95f), new Vector3(0.7f, 0.8f, 1.02f), new Vector3(0.2f, 0.8f, 1.02f), boards);
            for (int i = 1; i <= 3; i++)
            {
                float t = i / 4f;
                Vector3 at = new Vector3(0.45f, 0.02f + 0.78f * t + 0.03f, 1.95f - 0.93f * t);
                b.Beam(at - Vector3.UnitX * 0.25f, at + Vector3.UnitX * 0.25f, 0.025f, DarkWood);
            }
        }

        // A street light where a village limewashes: a white pillar on a stone foot, an iron arm off its cap, and a lantern hung from it.
        private static void BuildPillarLamp(MeshBuilder b, List<AssetLight> lights)
        {
            lights.Add(AssetLight.Lantern(new Vector3(0f, 3.75f, 0.95f)));
            VertexMaterial lime = Clay(230, 222, 204);
            b.Box(new Vector3(0f, 0.1f, 0f), new Vector3(0.34f, 0.3f, 0.34f), Stone(176));
            b.Box(new Vector3(0f, 2.3f, 0f), new Vector3(0.21f, 1.9f, 0.21f), lime);
            b.Box(new Vector3(0f, 4.27f, 0f), new Vector3(0.3f, 0.08f, 0.3f), Stone(196));
            b.Beam(new Vector3(0f, 4.4f, 0.1f), new Vector3(0f, 4.4f, 1.02f), 0.04f, Iron);
            b.Beam(new Vector3(0f, 4.4f, 0.25f), new Vector3(0f, 3.9f, 0.04f), 0.03f, Iron);
            b.Tube(new Vector3(0f, 4.4f, 0.95f), new Vector3(0f, 4.15f, 0.95f), 0.02f, 0.02f, 3, Iron);
            b.Box(new Vector3(0f, 3.85f, 0.95f), new Vector3(0.2f, 0.28f, 0.2f), Glow(1f, 0.5f, 0.16f, 3f));
            b.Box(new Vector3(0f, 4.17f, 0.95f), new Vector3(0.26f, 0.05f, 0.26f), Iron);
            b.Box(new Vector3(0f, 3.54f, 0.95f), new Vector3(0.26f, 0.04f, 0.26f), Iron);
        }

        // Where people hang paper lanterns: a cane pole bent over at the top, and the lantern glowing at the end of it.
        private static void BuildPaperLantern(MeshBuilder b, List<AssetLight> lights)
        {
            Vector3 lantern = new Vector3(0f, 3.85f, 0.95f);
            lights.Add(new AssetLight
            {
                Position = lantern, Colour = new Vector3(1f, 0.6f, 0.3f), Intensity = 120f, Radius = 21f, Fill = 0.25f,
                Flicker = 0.08f, FlickerSpeed = 4f, Shadow = true, LightsFog = true,
            });
            VertexMaterial cane = new VertexMaterial(TextureGenerator.barkRinged, new Colour(196, 184, 122));
            b.Tube(new Vector3(0f, -0.3f, 0f), new Vector3(0f, 4.3f, 0.05f), 0.08f, 0.065f, 6, cane);
            b.Tube(new Vector3(0f, 4.3f, 0.05f), new Vector3(0f, 4.55f, 0.55f), 0.065f, 0.06f, 6, cane);
            b.Tube(new Vector3(0f, 4.55f, 0.55f), new Vector3(0f, 4.5f, 1.05f), 0.06f, 0.05f, 6, cane);
            b.Tube(new Vector3(0f, 4.5f, 0.95f), new Vector3(0f, 4.3f, 0.95f), 0.015f, 0.015f, 3, DarkWood);
            b.Add(IcoSphereGenerator.CreateIcosphere(1, Glow(1f, 0.55f, 0.22f, 2.2f)), lantern, new Vector3(0.34f, 0.42f, 0.34f));
            b.Tube(lantern + Vector3.UnitY * 0.38f, lantern + Vector3.UnitY * 0.46f, 0.16f, 0.14f, 8, DarkWood);
            b.Tube(lantern - Vector3.UnitY * 0.46f, lantern - Vector3.UnitY * 0.38f, 0.14f, 0.16f, 8, DarkWood);
        }

        // ---------------------------------------------------------------- The wayside

        // A little shrine of the land's own on a stone post by the road: a niche under a roof, with a figure carved in pale stone standing in it.
        private static void BuildWaysideShrine(MeshBuilder b)
        {
            VertexMaterial stone = Stone(196);
            VertexMaterial slate = new VertexMaterial(TextureGenerator.slate, new Colour(170, 174, 186));
            b.Box(new Vector3(0f, -0.2f, 0f), new Vector3(0.42f, 0.3f, 0.42f), Rough(160));
            b.Box(new Vector3(0f, 1.2f, 0f), new Vector3(0.22f, 1.15f, 0.22f), stone);
            b.Box(new Vector3(0f, 2.785f, 0f), new Vector3(0.46f, 0.455f, 0.36f), stone);
            b.Quad(new Vector3(-0.3f, 2.45f, 0.365f), new Vector3(0.3f, 2.45f, 0.365f), new Vector3(0.3f, 3f, 0.365f), new Vector3(-0.3f, 3f, 0.365f), Shadowed);
            b.Tube(new Vector3(0f, 2.46f, 0.42f), new Vector3(0f, 2.78f, 0.42f), 0.1f, 0.07f, 8, Stone(226));
            b.Tube(new Vector3(0f, 2.78f, 0.42f), new Vector3(0f, 2.92f, 0.42f), 0.07f, 0.02f, 8, Stone(226));
            b.Quad(new Vector3(-0.62f, 3.12f, 0.55f), new Vector3(0.62f, 3.12f, 0.55f), new Vector3(0.62f, 3.5f, 0f), new Vector3(-0.62f, 3.5f, 0f), slate);
            b.Quad(new Vector3(0.62f, 3.12f, -0.55f), new Vector3(-0.62f, 3.12f, -0.55f), new Vector3(-0.62f, 3.5f, 0f), new Vector3(0.62f, 3.5f, 0f), slate);
            b.Triangle(new Vector3(0.46f, 3.24f, 0.36f), new Vector3(0.46f, 3.24f, -0.36f), new Vector3(0.46f, 3.48f, 0f), stone);
            b.Triangle(new Vector3(-0.46f, 3.24f, -0.36f), new Vector3(-0.46f, 3.24f, 0.36f), new Vector3(-0.46f, 3.48f, 0f), stone);
            b.Beam(new Vector3(-0.66f, 3.52f, 0f), new Vector3(0.66f, 3.52f, 0f), 0.05f, Stone(170));
        }

        // A milestone: a block of the country's stone with a rounded head, a band cut round it near the top.
        private static void BuildMilestone(MeshBuilder b)
        {
            VertexMaterial stone = Rough(178);
            b.Box(new Vector3(0f, 0.4f, 0f), new Vector3(0.34f, 0.8f, 0.22f), stone);
            // Its head a little shorter than the block is deep, so that its round ends never lie in the block's faces.
            b.Tube(new Vector3(0f, 1.2f, 0f), new Vector3(0f, 1.2f, 0.215f), 0.34f, 0.34f, 12, stone);
            b.Tube(new Vector3(0f, 1.2f, 0f), new Vector3(0f, 1.2f, -0.215f), 0.34f, 0.34f, 12, stone);
            b.Box(new Vector3(0f, 0.95f, 0f), new Vector3(0.355f, 0.04f, 0.235f), Rough(120));
        }

        // A hunter's stand: a platform up on four poles leaning in, boarded at the back and sides, a roof of boards over it and a ladder up to it.
        private static void BuildHuntingStand(MeshBuilder b)
        {
            VertexMaterial pole = new VertexMaterial(TextureGenerator.barkFurrowed, new Colour(176, 156, 132));
            VertexMaterial boards = Wood(132, 106, 80);
            const float Floor = 4.6f, Roof = 7.2f;
            float At(float y) => 1.35f - 0.4f * (y + 0.5f) / (Roof - 0.1f + 0.5f);
            // The roof falls to the front, so each pole runs up to meet it wherever it is over that pole.
            float RoofOver(float z) => Roof - 0.35f + 0.45f * (1.3f - z) / 2.6f;
            foreach (float x in new[] { -1f, 1f })
            {
                foreach (float z in new[] { -1f, 1f }) b.Tube(new Vector3(x * 1.35f, -0.5f, z * 1.35f), new Vector3(x * 0.95f, RoofOver(z * 0.95f) - 0.02f, z * 0.95f), 0.13f, 0.1f, 6, pole);
            }
            float floorAt = At(Floor);
            b.Box(new Vector3(0f, Floor, 0f), new Vector3(floorAt + 0.1f, 0.08f, floorAt + 0.1f), boards);
            float wall = At(Floor + 0.6f) - 0.02f;
            b.Box(new Vector3(0f, Floor + 0.6f, -wall), new Vector3(wall, 0.52f, 0.04f), boards);
            b.Box(new Vector3(wall, Floor + 0.6f, 0f), new Vector3(0.04f, 0.52f, wall - 0.04f), boards);
            b.Box(new Vector3(-wall, Floor + 0.6f, 0f), new Vector3(0.04f, 0.52f, wall - 0.04f), boards);
            float rail = At(Floor + 1.2f);
            b.Beam(new Vector3(-rail, Floor + 1.2f, rail), new Vector3(rail, Floor + 1.2f, rail), 0.05f, DarkWood);
            b.Quad(new Vector3(-1.3f, Roof - 0.35f, 1.3f), new Vector3(1.3f, Roof - 0.35f, 1.3f), new Vector3(1.3f, Roof + 0.1f, -1.3f), new Vector3(-1.3f, Roof + 0.1f, -1.3f), boards);
            PropLadder(b, new Vector3(0f, 0f, 2.6f), new Vector3(0f, Floor + 0.1f, floorAt + 0.08f), 0.4f, DarkWood);
        }

        // ---------------------------------------------------------------- Landmarks

        // A post mill. Its body of boards turns on one great post, set in a cross of beams on stones, to bring its four sails round into the wind, and the ladder at its back
        // is what it's turned by. From up here it's the sails that say what it is, so they're big, and spread as an X, well clear of anyone's head.
        private static void BuildWindmill(MeshBuilder b)
        {
            VertexMaterial boards = Wood(156, 128, 98);
            VertexMaterial frame = DarkWood;
            VertexMaterial roof = new VertexMaterial(TextureGenerator.shingle, new Colour(168, 148, 126));
            VertexMaterial canvas = Cloth(204, 194, 172);

            // The cross-trees lie one on the other, so their stones are as high as each needs.
            foreach (float x in new[] { -3f, 3f }) b.Box(new Vector3(x, 0.1f, 0f), new Vector3(0.5f, 0.45f, 0.5f), Rough(170));
            foreach (float z in new[] { -3f, 3f }) b.Box(new Vector3(0f, 0.34f, z), new Vector3(0.5f, 0.69f, 0.5f), Rough(170));
            b.Beam(new Vector3(-3.3f, 0.79f, 0f), new Vector3(3.3f, 0.79f, 0f), 0.24f, frame);
            b.Beam(new Vector3(0f, 1.27f, -3.3f), new Vector3(0f, 1.27f, 3.3f), 0.24f, frame);
            b.Tube(new Vector3(0f, 0.6f, 0f), new Vector3(0f, 5.2f, 0f), 0.46f, 0.4f, 10, frame);
            foreach ((float x, float z, float y) in new[] { (1f, 0f, 0.79f), (-1f, 0f, 0.79f), (0f, 1f, 1.27f), (0f, -1f, 1.27f) })
            {
                b.Beam(new Vector3(x * 2.7f, y + 0.2f, z * 2.7f), new Vector3(x * 0.35f, 3.6f, z * 0.35f), 0.14f, frame);
            }

            // The body, with posts up its corners.
            const float Bottom = 5f, Top = 11.8f, HalfX = 2.5f, HalfZ = 2.9f;
            b.Box(new Vector3(0f, (Bottom + Top) * 0.5f, 0f), new Vector3(HalfX, (Top - Bottom) * 0.5f, HalfZ), boards);
            foreach (float x in new[] { -1f, 1f })
            {
                foreach (float z in new[] { -1f, 1f }) b.Beam(new Vector3(x * (HalfX + 0.03f), Bottom, z * (HalfZ + 0.03f)), new Vector3(x * (HalfX + 0.03f), Top, z * (HalfZ + 0.03f)), 0.12f, frame);
            }

            // The roof, its ridge running front to back, over gables of boards.
            float eave = Top - 0.15f, ridge = Top + 2f;
            b.Quad(new Vector3(-HalfX - 0.35f, eave, -HalfZ - 0.3f), new Vector3(-HalfX - 0.35f, eave, HalfZ + 0.3f), new Vector3(0f, ridge, HalfZ + 0.3f), new Vector3(0f, ridge, -HalfZ - 0.3f), roof);
            b.Quad(new Vector3(HalfX + 0.35f, eave, HalfZ + 0.3f), new Vector3(HalfX + 0.35f, eave, -HalfZ - 0.3f), new Vector3(0f, ridge, -HalfZ - 0.3f), new Vector3(0f, ridge, HalfZ + 0.3f), roof);
            b.Triangle(new Vector3(-HalfX, Top, HalfZ), new Vector3(HalfX, Top, HalfZ), new Vector3(0f, ridge - 0.05f, HalfZ), boards);
            b.Triangle(new Vector3(HalfX, Top, -HalfZ), new Vector3(-HalfX, Top, -HalfZ), new Vector3(0f, ridge - 0.05f, -HalfZ), boards);
            b.Beam(new Vector3(0f, ridge + 0.05f, -HalfZ - 0.35f), new Vector3(0f, ridge + 0.05f, HalfZ + 0.35f), 0.1f, frame);

            // The windshaft out of the front, tilted up as a mill's is, and the sails on it: a stock each, with a frame of bars on its trailing side and the canvas spread
            // on that. The tilt leans the sails back, so that from up here their faces show and not only their edges.
            const float Tilt = 0.3f;
            Vector3 normal = new Vector3(0f, MathF.Sin(Tilt), MathF.Cos(Tilt));
            Vector3 up = new Vector3(0f, MathF.Cos(Tilt), -MathF.Sin(Tilt));
            Vector3 hub = new Vector3(0f, 11.4f, HalfZ + 1.3f);
            b.Tube(hub - normal * 2.3f, hub, 0.32f, 0.28f, 10, frame);
            b.Box(hub, new Vector3(0.36f, 0.36f, 0.3f), frame);
            const float Inner = 2.2f, Outer = 9f, Wide = 1.5f;
            Vector3 centre = hub + normal * 0.1f;
            for (int k = 0; k < 4; k++)
            {
                float angle = MathF.PI * 0.25f + k * MathF.PI * 0.5f;
                Vector3 along = Vector3.UnitX * MathF.Cos(angle) + up * MathF.Sin(angle);
                Vector3 across = -Vector3.UnitX * MathF.Sin(angle) + up * MathF.Cos(angle);
                b.Beam(centre - along * 0.2f, centre + along * (Outer + 0.2f), 0.12f, frame);
                b.Beam(centre + along * Inner + across * Wide + normal * 0.04f, centre + along * Outer + across * Wide + normal * 0.04f, 0.05f, frame);
                for (float r = Inner; r <= Outer + 0.01f; r += (Outer - Inner) / 8f)
                {
                    b.Beam(centre + along * r + normal * 0.04f, centre + along * r + across * Wide + normal * 0.04f, 0.04f, frame);
                }
                Vector3 a = centre + along * Inner - normal * 0.06f, c = centre + along * Outer + across * Wide - normal * 0.06f;
                Vector3 bb = centre + along * Outer - normal * 0.06f, d = centre + along * Inner + across * Wide - normal * 0.06f;
                b.Quad(a, bb, c, d, canvas);
                Vector3 back = -normal * 0.012f;
                b.Quad(a + back, d + back, c + back, bb + back, canvas);
            }

            // The ladder up to the door at the back, and the tail pole it's turned by under it.
            b.Beam(new Vector3(0f, Bottom + 0.3f, -HalfZ), new Vector3(0f, 0.8f, -8f), 0.14f, frame);
            PropLadder(b, new Vector3(0f, 0f, -7.2f), new Vector3(0f, Bottom + 0.1f, -HalfZ - 0.06f), 0.55f, frame);
            b.Quad(new Vector3(0.6f, Bottom + 0.15f, -HalfZ - 0.01f), new Vector3(-0.6f, Bottom + 0.15f, -HalfZ - 0.01f), new Vector3(-0.6f, Bottom + 2.3f, -HalfZ - 0.01f), new Vector3(0.6f, Bottom + 2.3f, -HalfZ - 0.01f), Shadowed);
        }

        // The bones of one of the great beasts the land keeps, come up out of the rock where the rain wore it away: a ribcage arching out of a worn swell of ground, the spine
        // along its top, the tail running down into the earth, and the skull at the far end, half buried. Some ribs are gone and some broken short, as the ground gives back.
        private static void BuildFossil(MeshBuilder b, WorldRandom random)
        {
            VertexMaterial bone = BoneOf(182, 160, 124);
            VertexMaterial rock = new VertexMaterial(TextureGenerator.rock, new Colour(176, 152, 122));
            b.Surface(16, 6, (u, v) =>
            {
                float angle = -u * MathF.Tau;
                float ring = 1f - v;
                return new Vector3(MathF.Cos(angle) * 5.5f * ring, -0.4f + 0.95f * MathF.Sin(v * MathF.PI * 0.5f), MathF.Sin(angle) * 2.95f * ring);
            }, rock);

            const int Ribs = 10;
            const float First = -2.7f, Spacing = 0.6f, Last = First + (Ribs - 1) * Spacing;
            // The back rises over the ribcage, runs down from its last rib to the skull, and from its first into the ground along the tail, all one line.
            float SpineAt(float x)
            {
                if (x < First) return 1.6f - (First - x) * (1.9f / 2.7f);
                if (x > Last) return 1.6f - (x - Last) * 0.8f;
                return 1.6f + 0.7f * MathF.Sin((x - First) / (Last - First) * MathF.PI);
            }
            for (int i = 0; i < Ribs; i++)
            {
                float x = First + i * Spacing;
                float envelope = MathF.Sqrt(MathF.Sin(MathF.PI * (i + 1) / (Ribs + 1)));
                foreach (float side in new[] { -1f, 1f })
                {
                    if (random.Chance(0.12f)) continue;
                    float end = random.Chance(0.2f) ? random.Range(0.8f, 1.3f) : 1.9f;
                    Rib(b, x, SpineAt(x), 1.9f * envelope, side, 0.12f, end, 0.13f, bone);
                }
            }
            const float Skull = 3.9f, SkullLength = 1.9f;
            for (float x = -5.2f; x < Skull - 0.1f; x += 0.42f)
            {
                float y = SpineAt(x);
                if (y < 0f) continue;
                float size = x < First ? MathF.Max(0.1f, 0.2f * (1f - (First - x) / 3.2f)) : 0.2f;
                b.Box(new Vector3(x, y + 0.05f, 0f), new Vector3(size * 1.1f, size, size), bone);
                b.Beam(new Vector3(x, y + 0.05f, 0f), new Vector3(x - 0.08f, y + 0.05f + size * 2.6f, 0f), size * 0.3f, bone);
            }

            // The skull, lying on its side: a cranium closed at the back and narrowing to the snout, a jaw under it, dark eyes and a row of teeth along the snout.
            Func<float, float> head = Profile((0f, 0f), (0.06f, 0.5f), (0.3f, 0.62f), (0.6f, 0.46f), (0.85f, 0.32f), (0.97f, 0.24f), (1f, 0f));
            Lathe(b, new Vector3(Skull, 0.62f, 0f), Vector3.UnitX, SkullLength, head, 8, 10, bone);
            Bend(b, new[] { new Vector3(Skull + 0.15f, 0.18f, 0.22f), new Vector3(Skull + 0.95f, 0.2f, 0.26f), new Vector3(Skull + 1.65f, 0.3f, 0.2f) }, 0.13f, 0.08f, 6, bone);
            foreach (float side in new[] { -1f, 1f }) b.Add(IcoSphereGenerator.CreateIcosphere(1, Shadowed), new Vector3(Skull + 0.35f, 0.9f, side * 0.48f), new Vector3(0.17f, 0.15f, 0.12f));
            for (int i = 0; i < 7; i++)
            {
                float x = Skull + 0.45f + i * 0.18f;
                float r = head((x - Skull) / SkullLength);
                Vector3 root = new Vector3(x, 0.62f - r * 0.75f, r * 0.6f);
                b.Tube(root, root + new Vector3(0.02f, -0.18f, 0f), 0.05f, 0.005f, 4, bone);
            }

            // And one of its legs, washed out of the slope and lying across it, half sunk.
            LongBone(b, new Vector3(-1.6f, 0.24f, 2.15f), new Vector3(0.8f, 0.2f, 2.45f), 0.2f, bone);
        }

        // A termite mound: spires of the red earth it's built of, fluted where the rain runs down them and swelling where each season's building was laid, with lesser
        // spires grown against the great one. It stands like an old stone.
        private static void BuildTermiteMound(MeshBuilder b, WorldRandom random)
        {
            VertexMaterial earth = new VertexMaterial(TextureGenerator.redEarth, new Colour(232, 222, 210));
            Spire(b, Vector3.Zero, 1.4f, 5.6f, random, earth);
            int lesser = random.Int(2, 3);
            float start = random.Float() * MathF.Tau;
            for (int i = 0; i < lesser; i++)
            {
                float angle = start + i * MathF.Tau / lesser + random.Range(-0.4f, 0.4f);
                Vector3 at = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle)) * random.Range(0.75f, 1f);
                Spire(b, at, random.Range(0.6f, 0.85f), random.Range(2.2f, 3.6f), random, earth);
            }
        }

        private static void Spire(MeshBuilder b, Vector3 foot, float radius, float height, WorldRandom random, VertexMaterial earth)
        {
            float phase = random.Float() * MathF.Tau, lean = random.Range(-0.15f, 0.15f);
            b.Surface(12, 10, (u, v) =>
            {
                float angle = -u * MathF.Tau;
                float flutes = 1f + 0.13f * MathF.Sin(angle * 5f + phase + v * 2.5f) + 0.07f * MathF.Sin(angle * 9f - phase * 1.7f);
                float swell = 1f + 0.08f * MathF.Sin(v * 11f + phase);
                float r = radius * MathF.Pow(1f - v, 0.75f) * flutes * swell + 0.05f * (1f - v);
                return foot + new Vector3(MathF.Cos(angle) * r + lean * v * height * 0.2f, -0.4f + v * (height + 0.4f), MathF.Sin(angle) * r);
            }, earth);
        }

        // ---------------------------------------------------------------- The Ossuary

        // Where the Ossuary digs: a pit cut square into the ground with boards round its edge and the spoil heaped at its ends, the bones it's after coming up out of its
        // floor, and a tripod over it with a hoist on black iron chain and a lantern hung from one leg, as it digs by night.
        private static void BuildDig(MeshBuilder b, WorldRandom random, List<AssetLight> lights)
        {
            const float HalfX = 2.3f, HalfZ = 1.6f;
            VertexMaterial bone = BoneOf(204, 190, 160);
            VertexMaterial iron = ArenaStone.Iron(random);
            b.Quad(new Vector3(-HalfX, GroundTop, HalfZ), new Vector3(HalfX, GroundTop, HalfZ), new Vector3(HalfX, GroundTop, -HalfZ), new Vector3(-HalfX, GroundTop, -HalfZ), Earth(70, 54, 40));
            b.Beam(new Vector3(-HalfX - 0.1f, 0.26f, HalfZ), new Vector3(HalfX + 0.1f, 0.26f, HalfZ), 0.09f, DarkWood);
            b.Beam(new Vector3(-HalfX - 0.1f, 0.26f, -HalfZ), new Vector3(HalfX + 0.1f, 0.26f, -HalfZ), 0.09f, DarkWood);
            b.Beam(new Vector3(HalfX, 0.27f, -HalfZ + 0.1f), new Vector3(HalfX, 0.27f, HalfZ - 0.1f), 0.08f, DarkWood);
            b.Beam(new Vector3(-HalfX, 0.27f, -HalfZ + 0.1f), new Vector3(-HalfX, 0.27f, HalfZ - 0.1f), 0.08f, DarkWood);
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 heap = new Vector3(side * (HalfX + 0.65f), 0f, random.Range(-0.4f, 0.4f));
                b.Surface(10, 4, (u, v) =>
                {
                    float angle = -u * MathF.Tau;
                    float ring = 1f - v;
                    return heap + new Vector3(MathF.Cos(angle) * 0.72f * ring, -0.2f + 1.05f * MathF.Sin(v * MathF.PI * 0.5f), MathF.Sin(angle) * 1.25f * ring);
                }, Earth(118, 96, 72));
            }

            // The beast coming up out of the pit: its ribs and backbone, and one great leg bone already free.
            for (int i = 0; i < 5; i++)
            {
                float x = -1.3f + i * 0.55f;
                float scale = MathF.Sqrt(MathF.Sin(MathF.PI * (i + 1) / 6f));
                foreach (float side in new[] { -1f, 1f }) Rib(b, x, 0.2f + 1.05f * scale, 0.85f * scale, side, 0.08f, 1.75f, 0.09f, bone);
                b.Box(new Vector3(x, 0.25f + 1.05f * scale, 0f), new Vector3(0.14f, 0.12f, 0.12f), bone);
            }
            LongBone(b, new Vector3(0.9f, 0.32f, -1.05f), new Vector3(2f, 0.36f, 0.5f), 0.15f, bone);

            // The tripod and its hoist.
            Vector3 apex = new Vector3(-0.2f, 5f, 0f);
            for (int leg = 0; leg < 3; leg++)
            {
                float angle = MathF.PI * 0.5f + leg * MathF.Tau / 3f;
                b.Tube(new Vector3(MathF.Cos(angle) * 1.95f - 0.2f, -0.3f, MathF.Sin(angle) * 1.95f), apex + Vector3.UnitY * 0.25f, 0.1f, 0.08f, 6, DarkWood);
            }
            b.Box(apex, new Vector3(0.16f, 0.2f, 0.16f), iron);
            b.Tube(apex, apex - Vector3.UnitY * 3.1f, 0.035f, 0.035f, 4, iron);
            b.Tube(apex - Vector3.UnitY * 3.6f, apex - Vector3.UnitY * 3.1f, 0.3f, 0.34f, 8, Wood(120, 96, 70));

            // A lantern on a hook off the front leg, lit, as the Ossuary works in the dark. The leg runs from its foot to the apex, so the hook starts where the leg is at its height.
            const float HookY = 3.2f;
            float legZ = 1.95f * (1f - (HookY + 0.3f) / (apex.Y + 0.25f + 0.3f));
            Vector3 lamp = new Vector3(-0.2f, HookY - 0.45f, legZ + 0.46f);
            lights.Add(AssetLight.StoneLantern(lamp));
            b.Beam(new Vector3(-0.2f, HookY, legZ), new Vector3(-0.2f, HookY, lamp.Z + 0.02f), 0.03f, iron);
            b.Tube(new Vector3(-0.2f, HookY, lamp.Z), lamp + Vector3.UnitY * 0.26f, 0.015f, 0.015f, 3, iron);
            b.Box(lamp, new Vector3(0.14f, 0.2f, 0.14f), Glow(1f, 0.5f, 0.16f, 2.6f));
            b.Box(lamp + Vector3.UnitY * 0.24f, new Vector3(0.18f, 0.04f, 0.18f), iron);
            b.Box(lamp - Vector3.UnitY * 0.22f, new Vector3(0.18f, 0.03f, 0.18f), iron);
        }

        // What the Ossuary has taken out of the ground so far, laid out in rows on an oxblood cloth to be counted, as everything the Reich owns is.
        private static void BuildBoneRows(MeshBuilder b, WorldRandom random)
        {
            VertexMaterial bone = BoneOf(208, 194, 164);
            b.Quad(new Vector3(-2.2f, GroundTop, 1.3f), new Vector3(2.2f, GroundTop, 1.3f), new Vector3(2.2f, GroundTop, -1.3f), new Vector3(-2.2f, GroundTop, -1.3f), ArenaStone.Oxblood(0.9f));
            float y = GroundTop + 0.08f;
            for (int i = 0; i < 7; i++)
            {
                float size = random.Range(0.11f, 0.16f);
                b.Box(new Vector3(-1.8f + i * 0.55f, y + size * 0.5f, 0.85f), new Vector3(size * 1.2f, size * 0.8f, size), bone);
            }
            for (int i = 0; i < 4; i++)
            {
                float x = -1.7f + i * 0.95f;
                var points = new List<Vector3>();
                for (int k = 0; k <= 4; k++)
                {
                    float t = k / 4f;
                    points.Add(new Vector3(x + MathF.Sin(t * 1.6f) * 0.55f, y + 0.05f, 0.25f - t * 0.75f));
                }
                Bend(b, points, 0.07f, 0.045f, 5, bone);
            }
            LongBone(b, new Vector3(-1.5f, y + 0.12f, -0.95f), new Vector3(0.6f, y + 0.12f, -0.9f), 0.14f, bone);
            Lathe(b, new Vector3(1.1f, GroundTop + 0.005f, -0.85f), Vector3.UnitY, 0.38f, t => 0.42f * MathF.Pow(MathF.Max(1f - t * t, 0f), 0.5f), 8, 4, bone);
        }

        // ---------------------------------------------------------------- The Reich's pens

        // The Reich's cage for a beast between hunts: a floor of planks with straw on it, black iron bars all round and across the top in a heavy frame, the door hanging
        // open, chains down inside, a gnawed bone, and the Reich's mark in brass on oxblood over the door.
        private static void BuildCage(MeshBuilder b, WorldRandom random)
        {
            const float HalfX = 3.2f, HalfZ = 2.4f, Floor = 0.4f, Top = 4.4f, Bar = 0.42f;
            VertexMaterial iron = ArenaStone.Iron(random);
            b.Box(new Vector3(0f, Floor * 0.5f - 0.2f, 0f), new Vector3(HalfX + 0.1f, Floor * 0.5f + 0.2f, HalfZ + 0.1f), ArenaStone.Timber(random, 0.85f));

            // The posts stand higher than the head beams, so that where two beams cross at a corner their tops are inside a post.
            foreach (float x in new[] { -HalfX, HalfX })
            {
                foreach (float z in new[] { -HalfZ, HalfZ }) b.Beam(new Vector3(x, Floor, z), new Vector3(x, Top + 0.2f, z), 0.14f, iron);
            }
            const float DoorFrom = 0.3f, DoorTo = 1.9f;
            foreach (float y in new[] { Floor + 0.1f, Top, (Floor + Top) * 0.5f })
            {
                float half = y == Top ? 0.12f : 0.06f;
                // The front's middle rail stops either side of the doorway, which it would otherwise run across.
                if (y == Top || y == Floor + 0.1f) b.Beam(new Vector3(-HalfX, y, HalfZ), new Vector3(HalfX, y, HalfZ), half, iron);
                else
                {
                    b.Beam(new Vector3(-HalfX, y, HalfZ), new Vector3(DoorFrom, y, HalfZ), half, iron);
                    b.Beam(new Vector3(DoorTo, y, HalfZ), new Vector3(HalfX, y, HalfZ), half, iron);
                }
                b.Beam(new Vector3(HalfX, y, -HalfZ), new Vector3(-HalfX, y, -HalfZ), half, iron);
                b.Beam(new Vector3(HalfX, y, HalfZ), new Vector3(HalfX, y, -HalfZ), half, iron);
                b.Beam(new Vector3(-HalfX, y, -HalfZ), new Vector3(-HalfX, y, HalfZ), half, iron);
            }
            b.Beam(new Vector3(DoorFrom, Floor, HalfZ), new Vector3(DoorFrom, Top, HalfZ), 0.07f, iron);
            b.Beam(new Vector3(DoorTo, Floor, HalfZ), new Vector3(DoorTo, Top, HalfZ), 0.07f, iron);
            for (float x = -HalfX + Bar; x < HalfX - 0.1f; x += Bar)
            {
                if (x > DoorTo + 0.05f || x < DoorFrom - 0.05f) b.Beam(new Vector3(x, Floor, HalfZ), new Vector3(x, Top, HalfZ), 0.045f, iron);
                b.Beam(new Vector3(x, Floor, -HalfZ), new Vector3(x, Top, -HalfZ), 0.045f, iron);
            }
            for (float z = -HalfZ + Bar; z < HalfZ - 0.1f; z += Bar)
            {
                b.Beam(new Vector3(HalfX, Floor, z), new Vector3(HalfX, Top, z), 0.045f, iron);
                b.Beam(new Vector3(-HalfX, Floor, z), new Vector3(-HalfX, Top, z), 0.045f, iron);
            }
            for (float x = -HalfX + 0.6f; x < HalfX - 0.1f; x += 0.6f) b.Beam(new Vector3(x, Top, -HalfZ), new Vector3(x, Top, HalfZ), 0.045f, iron);

            // The door, a frame of bars swung out about its hinge.
            float swing = MathHelper.DegreesToRadians(68f);
            Vector3 hinge = new Vector3(DoorFrom, 0f, HalfZ);
            Vector3 open = new Vector3(MathF.Cos(swing), 0f, MathF.Sin(swing));
            float width = DoorTo - DoorFrom;
            // It hangs on the doorway's post, so it has a stile only at its free edge.
            b.Beam(hinge + open * width + Vector3.UnitY * Floor, hinge + open * width + Vector3.UnitY * (Top - 0.15f), 0.07f, iron);
            foreach (float y in new[] { Floor + 0.12f, Top - 0.25f, (Floor + Top) * 0.5f }) b.Beam(hinge + Vector3.UnitY * y, hinge + open * width + Vector3.UnitY * y, 0.05f, iron);
            for (float s = Bar; s < width - 0.1f; s += Bar) b.Beam(hinge + open * s + Vector3.UnitY * Floor, hinge + open * s + Vector3.UnitY * (Top - 0.15f), 0.04f, iron);

            // Straw trodden flat, a bone, and chains hanging from the top.
            VertexMaterial straw = new VertexMaterial(TextureGenerator.thatch, new Colour(196, 170, 118));
            foreach ((float x, float z, float r) in new[] { (-1.6f, -0.9f, 1.1f), (0.9f, -0.5f, 1.3f), (-0.4f, 1f, 0.9f) })
            {
                Lathe(b, new Vector3(x, Floor - 0.02f, z), Vector3.UnitY, 0.14f, t => r * MathF.Sqrt(MathF.Max(1f - t * t, 0f)), 9, 2, straw);
            }
            LongBone(b, new Vector3(-0.9f, Floor + 0.14f, 0.2f), new Vector3(0.3f, Floor + 0.12f, -0.4f), 0.13f, BoneOf(214, 200, 172));
            foreach (float x in new[] { -2f, 1.4f })
            {
                b.Tube(new Vector3(x, Top, -1f), new Vector3(x, Top - 1.6f, -1f), 0.04f, 0.04f, 4, iron);
                b.Box(new Vector3(x, Top - 1.68f, -1f), new Vector3(0.12f, 0.08f, 0.05f), iron);
            }

            float markX = (DoorFrom + DoorTo) * 0.5f - 1.6f;
            b.Box(new Vector3(markX, Top - 0.55f, HalfZ + 0.12f), new Vector3(0.55f, 0.36f, 0.03f), ArenaStone.Oxblood());
            ArenaStone.Mark(b, new Vector3(markX, Top - 0.55f, HalfZ + 0.15f), Vector3.UnitX, Vector3.UnitY, 0.62f, 0.025f, ArenaStone.Brass(random), ArenaStone.Oxblood());
        }

        // A feeding trough of heavy timber bound with black iron, with raw meat and bones in it.
        private static void BuildFeedTrough(MeshBuilder b, WorldRandom random)
        {
            VertexMaterial timber = ArenaStone.Timber(random, 0.8f);
            VertexMaterial iron = ArenaStone.Iron(random);
            b.Box(new Vector3(0f, 0.3f, 0f), new Vector3(2.45f, 0.14f, 0.7f), timber);
            b.Box(new Vector3(0f, 0.72f, 0.62f), new Vector3(2.45f, 0.34f, 0.08f), timber);
            b.Box(new Vector3(0f, 0.72f, -0.62f), new Vector3(2.45f, 0.34f, 0.08f), timber);
            b.Box(new Vector3(2.37f, 0.72f, 0f), new Vector3(0.08f, 0.34f, 0.54f), timber);
            b.Box(new Vector3(-2.37f, 0.72f, 0f), new Vector3(0.08f, 0.34f, 0.54f), timber);
            foreach (float x in new[] { -1.5f, 1.5f })
            {
                b.Box(new Vector3(x, 0.72f, 0.715f), new Vector3(0.07f, 0.36f, 0.015f), iron);
                b.Box(new Vector3(x, 0.72f, -0.715f), new Vector3(0.07f, 0.36f, 0.015f), iron);
            }
            b.Quad(new Vector3(-2.29f, 0.62f, 0.54f), new Vector3(2.29f, 0.62f, 0.54f), new Vector3(2.29f, 0.62f, -0.54f), new Vector3(-2.29f, 0.62f, -0.54f), Meat(0.45f));
            for (int i = 0; i < 7; i++)
            {
                float x = -1.9f + i * 0.62f + random.Range(-0.12f, 0.12f);
                float z = random.Range(-0.25f, 0.25f);
                float r = random.Range(0.18f, 0.27f);
                b.Add(IcoSphereGenerator.CreateIcosphere(1, Meat(random.Range(0.85f, 1.1f))), new Vector3(x, 0.66f + r * 0.4f, z), new Vector3(r * 1.3f, r * 0.8f, r));
            }
            VertexMaterial bone = BoneOf(214, 200, 172);
            LongBone(b, new Vector3(-1.2f, 0.95f, -0.3f), new Vector3(-0.2f, 1.02f, 0.25f), 0.1f, bone);
            LongBone(b, new Vector3(0.8f, 0.92f, 0.3f), new Vector3(1.8f, 1.05f, -0.2f), 0.09f, bone);
        }

        // A rack where a pen's meat is hung: two posts and a beam, with quarters of carcass on black iron hooks.
        private static void BuildMeatRack(MeshBuilder b, WorldRandom random)
        {
            VertexMaterial timber = ArenaStone.Timber(random, 0.85f);
            VertexMaterial iron = ArenaStone.Iron(random);
            foreach (float x in new[] { -2.3f, 2.3f })
            {
                b.Beam(new Vector3(x, -0.4f, 0f), new Vector3(x, 4.25f, 0f), 0.13f, timber);
                b.Beam(new Vector3(x, 1.2f, -0.6f), new Vector3(x, 0f, -1f), 0.07f, timber);
            }
            b.Beam(new Vector3(-2.55f, 4.05f, 0f), new Vector3(2.55f, 4.05f, 0f), 0.11f, timber);
            VertexMaterial bone = BoneOf(214, 200, 172);
            foreach (float x in new[] { -1.4f, 0.05f, 1.35f })
            {
                float length = random.Range(1.3f, 1.75f), fat = random.Range(0.36f, 0.5f);
                Vector3 hook = new Vector3(x, 3.92f, 0f);
                b.Tube(hook, hook - Vector3.UnitY * 0.3f, 0.025f, 0.025f, 4, iron);
                Vector3 top = hook - Vector3.UnitY * 0.28f;
                b.Tube(top, top - Vector3.UnitY * 0.32f, 0.07f, 0.08f, 6, bone);
                Lathe(b, top - Vector3.UnitY * 0.22f, -Vector3.UnitY, length, Profile((0f, 0f), (0.06f, fat * 0.45f), (0.25f, fat * 0.85f), (0.6f, fat), (0.88f, fat * 0.62f), (1f, 0f)), 9, 8, Meat(random.Range(0.9f, 1.08f)));
            }
        }

        // The Reich's banner: a black iron pole with a brass finial and a crossbar, and from it an oxblood pennant, swallow-tailed, with the mark on both faces.
        private static void BuildBanner(MeshBuilder b, WorldRandom random)
        {
            VertexMaterial iron = ArenaStone.Iron(random), brass = ArenaStone.Brass(random), cloth = ArenaStone.Oxblood();
            const float Bar = 7.4f, Width = 1.0f, Body = 2.6f, Tails = 1.0f;
            b.Tube(new Vector3(0f, -0.6f, 0f), new Vector3(0f, Bar + 0.5f, 0f), 0.09f, 0.075f, 8, iron);
            b.Tube(new Vector3(0f, Bar + 0.5f, 0f), new Vector3(0f, Bar + 1.05f, 0f), 0.13f, 0.02f, 8, brass);
            b.Beam(new Vector3(-Width - 0.15f, Bar, 0.12f), new Vector3(Width + 0.15f, Bar, 0.12f), 0.05f, iron);

            // The cloth hangs flat a hand in front of the pole, one face each way, and its tails part at the bottom.
            const float Z = 0.12f, Apart = 0.006f;
            float top = Bar - 0.06f, bottom = top - Body;
            b.Quad(new Vector3(-Width, bottom, Z + Apart), new Vector3(Width, bottom, Z + Apart), new Vector3(Width, top, Z + Apart), new Vector3(-Width, top, Z + Apart), cloth);
            b.Quad(new Vector3(Width, bottom, Z - Apart), new Vector3(-Width, bottom, Z - Apart), new Vector3(-Width, top, Z - Apart), new Vector3(Width, top, Z - Apart), cloth);
            foreach (float side in new[] { -1f, 1f })
            {
                // Each tail hangs from a stretch of the bottom edge to a point below it: seen from +z its corners go right, left, tip to face out, and the other way round behind.
                float left = MathF.Min(side * Width, side * 0.12f), right = MathF.Max(side * Width, side * 0.12f);
                Vector3 tip = new Vector3(side * (Width * 0.55f), bottom - Tails, Z);
                b.Triangle(new Vector3(right, bottom, Z + Apart), new Vector3(left, bottom, Z + Apart), tip + Vector3.UnitZ * Apart, cloth);
                b.Triangle(new Vector3(left, bottom, Z - Apart), new Vector3(right, bottom, Z - Apart), tip - Vector3.UnitZ * Apart, cloth);
            }
            float markY = top - Body * 0.45f;
            ArenaStone.Mark(b, new Vector3(0f, markY, Z + Apart), Vector3.UnitX, Vector3.UnitY, 1.3f, 0.02f, brass, cloth);
            ArenaStone.Mark(b, new Vector3(0f, markY, Z - Apart), -Vector3.UnitX, Vector3.UnitY, 1.3f, 0.02f, brass, cloth);
        }
    }
}

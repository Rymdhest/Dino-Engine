using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture.Places;
using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.Modelling.Procedural.Architecture.Places.PlaceMaterials;

namespace Dino_Engine.Modelling.Procedural.Architecture.Seats
{
    /// <summary>
    /// What the Reich's seats in the open are built round (SEATS.md). A triumph's colossus: the land's tyrant cast in black iron, roaring, its regalia in brass, on a stepped plinth
    /// of the Reich's dark granite; the pylons its way is lined with, hung with the shields taken off the land's dead; and the stand its commander watches from, the chair on it
    /// empty, as nobody has met one. A war camp's pavilion, the wagons chained end to end that are its wall, and the cage on wheels its tyrant was brought in. Model +z is a
    /// piece's front, towards the camera. Headless.
    /// </summary>
    internal static class ReichField
    {
        private static readonly Vector3 X = Vector3.UnitX, Y = Vector3.UnitY, Z = Vector3.UnitZ;
        private static readonly float[] Sides = { -1f, 1f };
        private static Vector3 Granite => ReichWorks.Granite;

        // The Reich's granite as walling takes it.
        private static Vector3 Stone => StoneColour(SeatKit.ReichStone.R, SeatKit.ReichStone.G, SeatKit.ReichStone.B);

        // What the land dyes, for what's been taken off it: never the Reich's oxblood.
        private static readonly Vector3[] LandDyes = { new Vector3(0.14f, 0.2f, 0.34f), new Vector3(0.18f, 0.28f, 0.16f), new Vector3(0.5f, 0.34f, 0.1f), new Vector3(0.42f, 0.4f, 0.34f) };

        // A bowl of fire in black iron, its foot at a point: burning, and lighting what's round it if asked.
        private static void FireBowl(MeshBuilder b, Vector3 at, float size, WorldRandom random, List<AssetLight> lights, bool shines)
        {
            Kit.Lathe(b, at, Y, 0.8f * size, Kit.Profile((0f, 0.3f * size), (0.3f, 0.2f * size), (0.6f, 0.55f * size), (1f, 0.8f * size)), 10, 4, Iron(random), openEnd: true);
            Kit.Heap(b, at + Y * (0.6f * size), new Vector2(0.68f * size), 0.16f * size, 0f, Coals, random, 2, 8);
            lights.Add(AssetLight.Burning(at + Y * (0.75f * size), size));
            if (shines) lights.Add(AssetLight.Campfire(at + Y * (1.6f * size)) with { Intensity = 110f, Radius = 21f });
        }

        // ---------------------------------------------------------------- The triumph

        /// <summary>Half of how long the plinth under a colossus of a length is, along x as the animal stands, and half of how deep.</summary>
        public static Vector2 PlinthOf(float length) => new Vector2(length * 0.3f + 1.4f, length * 0.1f + 1.8f);

        /// <summary>
        /// A triumph's colossus: the land's tyrant in black iron, far bigger than it lives, roaring, side on to whoever comes in, as it's the outline of it that says what it is; its
        /// regalia in brass and its cloth enamelled oxblood, on a plinth of the Reich's granite stepping in three times, the mark on its face and a bowl of fire at each corner.
        /// Numbers: which tyrant, how long it stands from snout to tail, how high its plinth, which way it faces (0 towards +x, 1 towards -x).
        /// </summary>
        public static void Colossus(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            float length = Math.Clamp(spec.Metres(1), 14f, 40f), high = Math.Clamp(spec.Metres(2), 2.4f, 8f);
            float turn = spec.Int(3) == 1 ? -MathF.PI * 0.5f : MathF.PI * 0.5f;
            Vector2 half = PlinthOf(length);
            Vector3 stone = Stone;
            for (int i = 0; i < 3; i++)
            {
                float hx = half.X - i * 0.8f, hz = half.Y - i * 0.8f, y0 = i == 0 ? -0.6f : i * high / 3f, y1 = (i + 1) * high / 3f;
                SeatKit.WallBox(b, new Vector3(-hx, y0, -hz), new Vector3(hx, y1, hz), SeatKit.Walling(stone, random), SeatKit.Paving(stone, 0.9f));
                Vector3[] c = { new Vector3(-hx - 0.05f, y1 - 0.13f, hz + 0.05f), new Vector3(hx + 0.05f, y1 - 0.13f, hz + 0.05f), new Vector3(hx + 0.05f, y1 - 0.13f, -hz - 0.05f), new Vector3(-hx - 0.05f, y1 - 0.13f, -hz - 0.05f) };
                for (int k = 0; k < 4; k++) SeatKit.Course(b, c[k], c[(k + 1) % 4], new Vector2(0.15f, 0.24f), stone, random);
            }
            Kit.Mark(b, new Vector3(0f, high * 0.5f, half.Y - 0.8f + 0.04f), X, Y, MathF.Min(high * 0.3f, 2.2f), 0.05f, random);
            foreach (float sx in Sides)
            {
                foreach (float sz in Sides) FireBowl(b, new Vector3(sx * (half.X - 0.4f), high / 3f, sz * (half.Y - 0.4f)), 1.15f, random, lights, sz > 0f);
            }
            // The animal: black iron, a little paler than the Reich's bars are, as from above something that dark is a hole; what it wears in brass.
            VertexMaterial iron = Iron(random, 1.45f), belly = Iron(random, 1.2f), brass = Brass(random), enamel = Oxblood(0.74f), strap = Iron(random, 0.95f);
            List<SeatBeasts.Tri> tris = SeatBeasts.Tyrant(spec.Int(0), spec.Seed, roar: true);
            SeatBeasts.Stand(b, tris, length, new Vector3(0f, high, 0f), tri =>
            {
                if (GearMaterials.IsMetal(tri.Material)) return SeatBeasts.Brassy(tri) ? brass : strap;
                if (tri.Material == GearMaterials.Cloth) return enamel;
                if (tri.Material == GearMaterials.Leather) return strap;
                Vector3 n = Vector3.Cross(tri.B - tri.A, tri.C - tri.A);
                return n.Y < 0f ? belly : iron;
            }, turn);
        }

        /// <summary>
        /// One of the pylons a triumph's way is lined with: a pier of the Reich's granite stepping in as it rises, the mark on its face, and nailed up it the shields of the land's
        /// dead over their crossed spears. On its head a bowl of fire, or a point of brass. Numbers: height, form (0 burning and lighting what's round it, 1 burning, 2 a point).
        /// </summary>
        public static void TrophyPylon(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            float height = Math.Clamp(spec.Metres(0), 4f, 12f);
            int form = spec.Int(1);
            int courses = Math.Max(4, (int)(height / 0.85f));
            float course = height / courses;
            float HalfAt(float y) => 1.05f - 0.35f * Math.Clamp(y / height, 0f, 1f);
            for (int i = 0; i < courses; i++)
            {
                float half = HalfAt(i * course) + (i == 0 ? 0.25f : 0f), y0 = i * course - (i == 0 ? 0.6f : 0f);
                Kit.Span(b, new Vector3(-half, y0, -half), new Vector3(half, (i + 1) * course - 0.012f, half), 0.05f, Dressed(Granite * random.Range(0.94f, 1.06f), random, y0));
            }
            VertexMaterial iron = Iron(random), brass = Brass(random), shaft = Weathered(random), tip = LandIron(random);
            Kit.Span(b, new Vector3(-0.9f, height, -0.9f), new Vector3(0.9f, height + 0.22f, 0.9f), 0.03f, iron);
            Vector3 top = new Vector3(0f, height + 0.22f, 0f);
            if (form == 2) Kit.Lathe(b, top, Y, 1.5f, Kit.Profile((0f, 0.4f), (0.2f, 0.5f), (0.4f, 0.2f), (1f, 0f)), 4, 4, brass);
            else FireBowl(b, top, 1f, random, lights, form == 0);
            Kit.Mark(b, new Vector3(0f, height * 0.76f, HalfAt(height * 0.76f) + 0.04f), X, Y, 0.95f, 0.04f, random);
            // The land's spears crossed on its face, and its shields over them: three up the front and one on each side.
            float reach = height * 0.6f;
            foreach (float s in Sides)
            {
                Vector3 foot = new Vector3(s * 0.75f, 0.5f, HalfAt(0.5f) + 0.07f), head = new Vector3(-s * 0.55f, reach, HalfAt(reach) + 0.07f);
                Kit.Pole(b, foot, head, 0.028f, shaft);
                Kit.Lathe(b, head, (head - foot).Normalized(), 0.34f, Kit.Profile((0f, 0.04f), (0.3f, 0.06f), (1f, 0f)), 4, 2, tip);
            }
            void Shield(Vector3 centre, Vector3 outward, float size)
            {
                Vector3 axis = (outward + new Vector3(random.Range(-0.12f, 0.12f), random.Range(-0.05f, 0.18f), random.Range(-0.12f, 0.12f))).Normalized();
                VertexMaterial paint = Painted(random.Pick(LandDyes) * random.Range(0.85f, 1.1f));
                Kit.Lathe(b, centre, axis, 0.09f, Kit.Profile((0f, size), (1f, size * 0.9f)), 10, 1, paint);
                b.Tube(centre + axis * 0.03f, centre + axis * 0.1f, size + 0.02f, size + 0.02f, 10, tip, capped: false);
                Kit.Ball(b, centre + axis * 0.11f, new Vector3(size * 0.26f), 6, 4, tip);
            }
            for (int k = 0; k < 3; k++)
            {
                float y = height * (0.2f + 0.17f * k);
                Shield(new Vector3((k - 1) * 0.3f * (k % 2 == 0 ? 1f : -1f), y, HalfAt(y) + 0.1f), Z, random.Range(0.44f, 0.54f));
            }
            foreach (float s in Sides)
            {
                float y = height * random.Range(0.26f, 0.4f);
                Shield(new Vector3(s * (HalfAt(y) + 0.1f), y, random.Range(-0.2f, 0.2f)), X * s, random.Range(0.44f, 0.52f));
            }
        }

        /// <summary>How deep a commander's stand is, front to back, and how high its floor.</summary>
        public const float DaisDepth = 6.4f, DaisHigh = 1.5f;

        /// <summary>
        /// The stand a commander watches from: three steps of granite up to a floor, a canopy of oxblood over it on posts of iron, the Reich's banners at its back, a bowl of fire
        /// either side, and under the canopy the chair, tall-backed, in black iron with the mark on it. Nobody's in it. Numbers: width.
        /// </summary>
        public static void Dais(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            float w = Math.Clamp(spec.Metres(0), 7f, 16f), hw = w * 0.5f, hd = DaisDepth * 0.5f;
            Vector3 stone = Stone;
            for (int i = 0; i < 3; i++)
            {
                float y1 = (i + 1) * DaisHigh / 3f, z1 = hd - i * 0.7f;
                SeatKit.WallBox(b, new Vector3(-hw, i == 0 ? -0.6f : i * DaisHigh / 3f, -hd), new Vector3(hw, y1, z1), SeatKit.Walling(stone, random), SeatKit.Paving(stone, 0.95f));
                SeatKit.Course(b, new Vector3(-hw, y1 - 0.1f, z1 + 0.04f), new Vector3(hw, y1 - 0.1f, z1 + 0.04f), new Vector2(0.11f, 0.2f), stone, random);
            }
            VertexMaterial iron = Iron(random), brass = Brass(random);
            float front = hd - 1.4f;
            // The canopy.
            const float Posts = 5.2f;
            float cx = hw - 0.8f, cz0 = -hd + 0.5f, cz1 = front - 0.5f;
            foreach (float sx in Sides)
            {
                foreach (float z in new[] { cz0, cz1 })
                {
                    Kit.Pole(b, new Vector3(sx * cx, DaisHigh, z), new Vector3(sx * cx, Posts, z), 0.07f, iron);
                    Kit.Lathe(b, new Vector3(sx * cx, Posts, z), Y, 0.5f, Kit.Profile((0f, 0.09f), (0.4f, 0.14f), (1f, 0f)), 6, 3, brass);
                }
            }
            VertexMaterial cloth = Oxblood(0.72f), under = Oxblood(0.48f);
            Kit.Cloth(b, new Vector3(-cx, Posts - 0.1f, cz0), new Vector3(cx, Posts - 0.1f, cz0), new Vector3(cx, Posts - 0.1f, cz1), new Vector3(-cx, Posts - 0.1f, cz1), 0.45f, 6, cloth, under);
            // Its valance along the front, and a line of brass along that.
            Kit.Sheet(b, new Vector3(-cx, Posts - 0.75f, cz1), new Vector3(cx, Posts - 0.75f, cz1), new Vector3(cx, Posts - 0.1f, cz1), new Vector3(-cx, Posts - 0.1f, cz1), Oxblood(0.64f), under);
            Kit.Sheet(b, new Vector3(-cx, Posts - 0.75f, cz1 + 0.01f), new Vector3(cx, Posts - 0.75f, cz1 + 0.01f), new Vector3(cx, Posts - 0.64f, cz1 + 0.01f), new Vector3(-cx, Posts - 0.64f, cz1 + 0.01f), brass, brass);
            // The chair.
            float seat = DaisHigh + 0.62f, chair = -hd + 1.5f;
            Kit.Span(b, new Vector3(-0.72f, DaisHigh, chair - 0.5f), new Vector3(0.72f, seat, chair + 0.62f), 0.03f, iron);
            Kit.Span(b, new Vector3(-0.8f, DaisHigh, chair - 0.72f), new Vector3(0.8f, DaisHigh + 3.3f, chair - 0.5f), 0.04f, iron);
            foreach (float s in Sides)
            {
                Kit.Span(b, new Vector3(s * 0.8f - 0.1f, seat, chair - 0.5f), new Vector3(s * 0.8f + 0.1f, seat + 0.42f, chair + 0.6f), 0.03f, iron);
                Kit.Lathe(b, new Vector3(s * 0.72f, DaisHigh + 3.3f, chair - 0.61f), Y, 0.6f, Kit.Profile((0f, 0.1f), (0.4f, 0.14f), (1f, 0f)), 4, 3, brass);
            }
            Kit.Mark(b, new Vector3(0f, DaisHigh + 2.45f, chair - 0.48f), X, Y, 0.95f, 0.03f, random);
            Kit.Span(b, new Vector3(-0.6f, seat, chair - 0.4f), new Vector3(0.6f, seat + 0.07f, chair + 0.55f), 0.02f, Oxblood(0.6f));
            // A runner from its feet down the steps.
            VertexMaterial runner = Oxblood(0.6f);
            Kit.Face(b, new Vector3(-0.9f, DaisHigh + 0.02f, chair + 0.7f), new Vector3(0.9f, DaisHigh + 0.02f, chair + 0.7f), new Vector3(0.9f, DaisHigh + 0.02f, front + 0.02f), new Vector3(-0.9f, DaisHigh + 0.02f, front + 0.02f), Y, runner);
            for (int i = 2; i >= 0; i--)
            {
                float y1 = (i + 1) * DaisHigh / 3f, y0 = i * DaisHigh / 3f, z1 = hd - i * 0.7f;
                Kit.Face(b, new Vector3(-0.9f, y0 + 0.02f, z1 + 0.02f), new Vector3(0.9f, y0 + 0.02f, z1 + 0.02f), new Vector3(0.9f, y1 + 0.02f, z1 + 0.02f), new Vector3(-0.9f, y1 + 0.02f, z1 + 0.02f), Z, runner);
                if (i > 0) Kit.Face(b, new Vector3(-0.9f, y0 + 0.02f, z1 + 0.02f), new Vector3(0.9f, y0 + 0.02f, z1 + 0.02f), new Vector3(0.9f, y0 + 0.02f, z1 + 0.72f), new Vector3(-0.9f, y0 + 0.02f, z1 + 0.72f), Y, runner);
            }
            // Fire either side of it, and the banners at its back corners.
            foreach (float s in Sides)
            {
                FireBowl(b, new Vector3(s * (hw - 1.9f), DaisHigh + 0.7f, front - 0.9f), 0.8f, random, lights, true);
                Kit.Pole(b, new Vector3(s * (hw - 1.9f), DaisHigh, front - 0.9f), new Vector3(s * (hw - 1.9f), DaisHigh + 0.72f, front - 0.9f), 0.09f, iron);
                Vector3 mast = new Vector3(s * (hw - 0.45f), DaisHigh, -hd + 0.4f);
                Kit.Pole(b, mast, mast + Y * 8f, 0.1f, iron, 0.07f);
                Kit.Lathe(b, mast + Y * 8f, Y, 0.7f, Kit.Profile((0f, 0.1f), (0.3f, 0.18f), (0.5f, 0.09f), (1f, 0f)), 6, 3, brass);
                Kit.Pole(b, mast + new Vector3(-1.1f, 7.5f, 0.14f), mast + new Vector3(1.1f, 7.5f, 0.14f), 0.045f, iron);
                SeatKit.Banner(b, mast + new Vector3(0f, 7.45f, 0.2f), Z, 1.8f, 4.6f, random);
            }
        }

        // ---------------------------------------------------------------- The war camp

        /// <summary>Half the width of a pavilion's middle tent and of each wing, and how far off its middle a wing stands, for a pavilion of a width.</summary>
        public static (float Middle, float Wing, float WingAt) PavilionBays(float width) => (width * 0.21f, width * 0.14f, width * 0.35f + 0.7f);

        /// <summary>How far before a pavilion's front its fly reaches.</summary>
        public const float PavilionFly = 3.6f;

        /// <summary>
        /// A brood's pavilion: a great wall tent of the Reich's charcoal canvas between two lesser ones, oxblood along their eaves and the mark on the gable, a mast through its ridge
        /// with the brood's pennant, a fly out over the table the land's maps are pinned to, the commander's chair at its head with nobody in it, a runner out from the door between
        /// two bowls of fire, and the Reich's banners on masts either side. Numbers: width, depth.
        /// </summary>
        public static void Pavilion(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            float w = Math.Clamp(spec.Metres(0), 14f, 26f), d = Math.Clamp(spec.Metres(1), 8f, 14f);
            (float hx, float wing, float at) = PavilionBays(w);
            float hz = d * 0.5f;
            VertexMaterial wood = Timber(random, 0.6f), iron = Iron(random), brass = Brass(random);
            // The commander's own is oxblood from ridge to hem, sewn and edged in brass thread, which nothing else in the camp is: stripes of it read as a fairground's. The
            // tents either side of it are the brood's charcoal.
            Vector3 thread = new Vector3(0.42f, 0.3f, 0.1f);
            Tents.Look look = Tents.Look.Reich(random, random.Range(0.05f, 0.15f));
            look.Canvas = Arena.ArenaStone.OxbloodColour * 0.6f;
            look.RidgeBand = thread;
            look.HemBand = Arena.ArenaStone.OxbloodColour * 0.36f;
            look.Eave = thread;
            look.Dark = false;
            Tents.Look plain = Tents.Look.Reich(random, random.Range(0.2f, 0.4f));
            plain.Eave = Arena.ArenaStone.OxbloodColour;
            float wall = 3f, ridge = wall + hx * 0.95f;
            Tents.Wall(b, random, look, hx, hz, wall, ridge, 1.8f, scalloped: true, valance: true, frontGuy: false);
            Kit.Mark(b, new Vector3(0f, wall + (ridge - wall) * 0.4f, hz + 0.04f), X, Y, 1.5f, 0.02f, random);
            float wingWall = 2.3f, wingRidge = wingWall + wing * 0.95f, wingHz = hz * 0.8f, wingBack = -hz * 0.15f;
            foreach (float s in Sides)
            {
                b.Place(new Vector3(s * at, 0f, wingBack));
                Tents.Wall(b, random, plain, wing, wingHz, wingWall, wingRidge, 1.4f, scalloped: true, valance: true);
                b.ResetPlacement();
                foreach (float z in new[] { wingBack - wingHz, wingBack + wingHz }) Kit.Lathe(b, new Vector3(s * at, wingRidge, z), Y, 0.6f, Kit.Profile((0f, 0.07f), (0.4f, 0.12f), (1f, 0f)), 6, 3, brass);
            }
            // Its mast, up through the ridge, and the brood's pennant.
            Kit.Pole(b, new Vector3(0f, ridge - 0.3f, 0f), new Vector3(0f, ridge + 3.4f, 0f), 0.08f, iron, 0.05f);
            Kit.Ball(b, new Vector3(0f, ridge + 3.5f, 0f), new Vector3(0.14f), 6, 4, brass);
            int first = b.VertexCount;
            Vector3 hoist = new Vector3(0f, ridge + 3.2f, 0f);
            VertexMaterial pennant = Oxblood(0.74f);
            Kit.Sheet(b, hoist - Y * 0.9f, hoist - Y * 0.56f + X * 3.8f, hoist - Y * 0.38f + X * 3.8f, hoist, pennant, pennant);
            b.Hang(first, hoist - Y * 0.45f, hoist - Y * 0.45f + X * 3.8f, 0.5f, 1.4f, 0.5f);
            foreach (float z in new[] { -hz, hz }) Kit.Lathe(b, new Vector3(0f, ridge, z), Y, 0.8f, Kit.Profile((0f, 0.09f), (0.4f, 0.15f), (1f, 0f)), 6, 3, brass);

            // The fly out in front on two poles, sagging between them, oxblood along its edge.
            float fly = PavilionFly, pole = wall + 0.5f;
            foreach (float s in Sides) Kit.Pole(b, new Vector3(s * hx * 0.9f, -0.3f, hz + fly), new Vector3(s * hx * 0.9f, pole, hz + fly), 0.07f, wood);
            float edge = 1f - 0.3f / fly;
            Vector3 FlyAt(float u, float v)
            {
                float across = (u - 0.5f) * 2f;
                Vector3 p = new Vector3(across * hx * 0.95f, MathHelper.Lerp(wall + 0.7f, pole - 0.06f, v), hz + 0.02f + v * fly);
                return p - Y * (0.2f * (1f - across * across) * MathF.Sin(MathF.PI * Math.Clamp(v * 1.15f, 0f, 1f)) + 0.04f * MathF.Sin(across * 9f) * v * (1f - across * across));
            }
            Tents.Sheet(b, new[] { 0f, 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f, 0.9f, 1f }, new[] { 0f, 0.15f, 0.3f, 0.45f, 0.6f, 0.75f, edge - 0.02f, edge + 0.02f, 1f }, FlyAt,
                (u, v) => v > edge ? thread : look.Canvas * (0.95f + 0.1f * MathF.Sin(u * 23f)), Y, MaterialCatalog.Cloth.Index);
            // The table under it, its maps pinned out, a lantern over it, and the chair at its head.
            Vector3 table = new Vector3(0f, 0f, hz + fly * 0.55f);
            Kit.Span(b, table + new Vector3(-1.5f, 0.86f, -0.6f), table + new Vector3(1.5f, 0.93f, 0.6f), 0.01f, wood);
            foreach (float x in new[] { -1.38f, 1.38f }) foreach (float z in new[] { -0.5f, 0.5f }) Kit.Span(b, table + new Vector3(x - 0.05f, 0f, z - 0.05f), table + new Vector3(x + 0.05f, 0.86f, z + 0.05f), 0.004f, wood);
            for (int k = 0; k < 4; k++)
            {
                Vector3 map = table + new Vector3(-0.95f + k * 0.62f, 0.935f + k * 0.002f, random.Range(-0.15f, 0.15f));
                float turn = random.Range(-0.25f, 0.25f);
                Vector3 ax = new Vector3(MathF.Cos(turn), 0f, -MathF.Sin(turn)), az = new Vector3(MathF.Sin(turn), 0f, MathF.Cos(turn));
                Kit.Face(b, map - ax * 0.36f - az * 0.26f, map + ax * 0.36f - az * 0.26f, map + ax * 0.36f + az * 0.26f, map - ax * 0.36f + az * 0.26f, Y, Plaster(0.8f + k * 0.03f));
                Kit.Plank(b, map - ax * 0.24f + az * 0.06f + Y * 0.002f, map + ax * 0.26f - az * 0.1f + Y * 0.002f, Y, 0.014f, 0.001f, Dark, 0f);
                if (k % 2 == 1) Kit.Plank(b, map - ax * 0.12f - az * 0.14f + Y * 0.003f, map + ax * 0.14f + az * 0.12f + Y * 0.003f, Y, 0.02f, 0.001f, OxbloodPaint(), 0f);
            }
            Kit.Lantern(b, table + new Vector3(0.9f, 1.42f, 0.25f), brass, lights, 0.9f);
            Vector3 chair = table + new Vector3(0f, 0f, -1.25f);
            Kit.Span(b, chair + new Vector3(-0.4f, 0f, -0.36f), chair + new Vector3(0.4f, 0.55f, 0.36f), 0.02f, iron);
            Kit.Span(b, chair + new Vector3(-0.44f, 0f, -0.46f), chair + new Vector3(0.44f, 1.9f, -0.36f), 0.03f, iron);
            Kit.Mark(b, chair + new Vector3(0f, 1.45f, -0.34f), X, Y, 0.5f, 0.02f, random);
            foreach (float x in new[] { -1.1f, 1.1f }) Kit.Lathe(b, table + new Vector3(x, 0f, 1f), Y, 0.48f, Kit.Profile((0f, 0.17f), (0.9f, 0.17f), (1f, 0.2f)), 7, 2, wood);
            // A runner out from the door, fire either side of its end, and the Reich's banners on their masts.
            float end = hz + fly + 4.2f;
            Kit.Face(b, new Vector3(-1.1f, Ground + 0.05f, hz + fly + 0.4f), new Vector3(1.1f, Ground + 0.05f, hz + fly + 0.4f), new Vector3(1.1f, Ground + 0.05f, end), new Vector3(-1.1f, Ground + 0.05f, end), Y, Oxblood(0.6f));
            foreach (float z in new[] { hz + fly + 0.5f, end - 0.35f }) Kit.Face(b, new Vector3(-1.1f, Ground + 0.06f, z), new Vector3(1.1f, Ground + 0.06f, z), new Vector3(1.1f, Ground + 0.06f, z + 0.14f), new Vector3(-1.1f, Ground + 0.06f, z + 0.14f), Y, brass);
            foreach (float s in Sides)
            {
                Vector3 bowl = new Vector3(s * 2.4f, 0f, end - 0.6f);
                Kit.Lathe(b, bowl - Y * 0.3f, Y, 1.2f, Kit.Profile((0f, 0.5f), (0.3f, 0.2f), (1f, 0.16f)), 8, 3, iron);
                FireBowl(b, bowl + Y * 0.85f, 0.85f, random, lights, true);
                Vector3 mast = new Vector3(s * (hx + 1.4f), 0f, hz + fly + 0.3f);
                Kit.Span(b, mast + new Vector3(-0.4f, -0.4f, -0.4f), mast + new Vector3(0.4f, 0.5f, 0.4f), 0.05f, Dressed(Granite, random, 0.4f));
                Kit.Pole(b, mast + Y * 0.45f, mast + Y * 10.5f, 0.12f, iron, 0.08f);
                Kit.Lathe(b, mast + Y * 10.5f, Y, 0.8f, Kit.Profile((0f, 0.11f), (0.3f, 0.2f), (0.5f, 0.1f), (1f, 0f)), 6, 3, brass);
                Kit.Pole(b, mast + new Vector3(-1.2f, 10f, 0.16f), mast + new Vector3(1.2f, 10f, 0.16f), 0.05f, iron);
                SeatKit.Banner(b, mast + new Vector3(0f, 9.95f, 0.24f), Z, 1.9f, 5.8f, random);
            }
            // What was the land's lord's, hung by the door the wrong way up.
            Vector3 rack = new Vector3(-hx - 0.2f, 0f, hz + 1.3f);
            foreach (float s in Sides) Kit.Pole(b, rack + new Vector3(s * 0.9f, -0.3f, 0f), rack + new Vector3(s * 0.9f, 2.9f, 0f), 0.045f, wood);
            Kit.Pole(b, rack + new Vector3(-1.05f, 2.8f, 0f), rack + new Vector3(1.05f, 2.8f, 0f), 0.04f, wood);
            VertexMaterial taken = new VertexMaterial(MaterialCatalog.Cloth.Index, new Colour(LandDyes[0] * 1.1f)), gilt = new VertexMaterial(MaterialCatalog.Cloth.Index, new Colour(LandDyes[2]));
            Kit.Sheet(b, rack + new Vector3(-0.8f, 0.7f, 0.03f), rack + new Vector3(0.8f, 0.7f, 0.03f), rack + new Vector3(0.8f, 2.76f, 0.03f), rack + new Vector3(-0.8f, 2.76f, 0.03f), taken, taken);
            Kit.Sheet(b, rack + new Vector3(-0.8f, 0.7f, 0.04f), rack + new Vector3(0.8f, 0.7f, 0.04f), rack + new Vector3(0.8f, 0.95f, 0.04f), rack + new Vector3(-0.8f, 0.95f, 0.04f), gilt, gilt);
            Kit.Sheet(b, rack + new Vector3(0f, 1.25f, 0.04f), rack + new Vector3(0.38f, 1.75f, 0.04f), rack + new Vector3(0f, 2.25f, 0.04f), rack + new Vector3(-0.38f, 1.75f, 0.04f), gilt, gilt);
        }

        /// <summary>Half of how wide a war wagon is across its bed.</summary>
        public const float WagonHalf = 1.25f;

        /// <summary>
        /// One of the wagons a war camp is walled with, along x, its outside to -z: an iron-shod wagon as high as two men, the side it turns to the land boarded up, plated and
        /// spiked, with the mark on it, and boards hung below its bed between the wheels; chained at each end to the next. On its bed its crew's gear, or a bolt thrower raised to
        /// shoot over its wall. Numbers: length, form (0 a wagon, 1 with a bolt thrower).
        /// </summary>
        public static void WarWagon(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float length = Math.Clamp(spec.Metres(0), 5f, 9f), hl = length * 0.5f;
            bool armed = spec.Int(1) == 1;
            VertexMaterial wood = Timber(random, 0.72f), tarred = Tarred(random, 1.7f), iron = Iron(random), plate = Plating(random);
            const float Bed = 1.35f, WheelR = 0.95f, Hw = WagonHalf;
            float top = Bed + 2.3f;
            Kit.Span(b, new Vector3(-hl, Bed - 0.16f, -Hw), new Vector3(hl, Bed, Hw), 0.015f, wood);
            foreach (float x in new[] { -hl + 1.25f, hl - 1.25f })
            {
                Kit.Pole(b, new Vector3(x, WheelR, -Hw - 0.3f), new Vector3(x, WheelR, Hw + 0.3f), 0.08f, wood);
                foreach (float s in Sides) ReichWorks.Wheel(b, new Vector3(x, WheelR, s * (Hw + 0.3f)), Z, WheelR, wood, iron, 10, 0.14f);
            }
            // Its wall towards the land: boards, plates of iron over them with a slit in each, and spikes along the top.
            Kit.Span(b, new Vector3(-hl, Bed, -Hw), new Vector3(hl, top, -Hw + 0.14f), 0.01f, wood);
            int plates = Math.Max(3, (int)(length / 1.25f));
            for (int i = 0; i < plates; i++)
            {
                float x0 = -hl + i * length / plates + 0.05f, x1 = -hl + (i + 1) * length / plates - 0.05f, xm = (x0 + x1) * 0.5f;
                if (i == plates / 2) continue;
                Kit.Span(b, new Vector3(x0, Bed + 0.45f, -Hw - 0.045f), new Vector3(x1, top - 0.3f, -Hw), 0.01f, i % 3 == 1 ? RustedPlating(random) : plate);
                Kit.Face(b, new Vector3(xm + 0.07f, Bed + 1.25f, -Hw - 0.05f), new Vector3(xm - 0.07f, Bed + 1.25f, -Hw - 0.05f), new Vector3(xm - 0.07f, Bed + 1.85f, -Hw - 0.05f), new Vector3(xm + 0.07f, Bed + 1.85f, -Hw - 0.05f), -Z, Dark);
            }
            for (float x = -hl + 0.3f; x < hl; x += 0.6f) Kit.Lathe(b, new Vector3(x, top, -Hw + 0.07f), Y, 0.45f, Kit.Profile((0f, 0.09f), (1f, 0f)), 4, 1, iron);
            float mid = plates / 2, m0 = -hl + mid * length / plates + 0.05f, m1 = -hl + (mid + 1f) * length / plates - 0.05f;
            Kit.Span(b, new Vector3(m0, Bed + 0.45f, -Hw - 0.05f), new Vector3(m1, top - 0.3f, -Hw), 0f, OxbloodPaint(0.9f));
            Kit.Mark(b, new Vector3((m0 + m1) * 0.5f, (Bed + 0.45f + top - 0.3f) * 0.5f, -Hw - 0.06f), -X, Y, MathF.Min(m1 - m0 - 0.1f, 1.1f), 0.02f, random);
            Kit.Span(b, new Vector3(-hl + 2.35f, 0.25f, -Hw - 0.02f), new Vector3(hl - 2.35f, Bed - 0.16f, -Hw + 0.06f), 0.01f, tarred);
            // Its ends and its inner side, low, and steps up.
            foreach (float s in Sides) Kit.Span(b, new Vector3(s * hl - 0.05f, Bed, -Hw), new Vector3(s * hl + 0.05f, Bed + 0.9f, Hw), 0.01f, wood);
            Kit.Span(b, new Vector3(-hl, Bed, Hw - 0.08f), new Vector3(hl, Bed + 0.5f, Hw), 0.01f, wood);
            for (int k = 0; k < 3; k++) Kit.Span(b, new Vector3(hl - 2.6f, 0.36f + k * 0.36f, Hw + 0.1f + (2 - k) * 0.3f), new Vector3(hl - 1.9f, 0.42f + k * 0.36f, Hw + 0.4f + (2 - k) * 0.3f), 0.01f, wood);
            // The chains to the next, and its pole.
            foreach (float s in Sides) Kit.Chain(b, new Vector3(s * hl, Bed + 0.5f, -Hw + 0.3f), new Vector3(s * (hl + 0.55f), Bed + 0.2f, -Hw + 0.3f), 0.12f, 0.3f, iron);
            Kit.Pole(b, new Vector3(hl - 0.5f, WheelR, 0f), new Vector3(hl + 0.5f, 0.5f, 0.5f), 0.07f, wood);
            if (armed)
            {
                // A bolt thrower on a stage, to shoot over the wall.
                float stage = Bed + 1.55f;
                Kit.Span(b, new Vector3(-1.1f, Bed, -0.75f), new Vector3(1.1f, stage, 0.95f), 0.02f, wood);
                Kit.Pole(b, new Vector3(0f, stage, 0.1f), new Vector3(0f, stage + 0.8f, 0.1f), 0.09f, iron);
                Vector3 pivot = new Vector3(0f, stage + 0.9f, 0.1f);
                Kit.Box(b, pivot + new Vector3(0f, 0.08f, -0.2f), X, (Y + Z * 0.14f).Normalized(), (Z - Y * 0.14f).Normalized(), new Vector3(0.11f, 0.09f, 1.5f), 0.01f, wood);
                foreach (float s in Sides)
                {
                    Kit.Bend(b, new[] { pivot + new Vector3(0f, 0.2f, -1.5f), pivot + new Vector3(s * 0.8f, 0.2f, -1.4f), pivot + new Vector3(s * 1.5f, 0.2f, -1f) }, 0.07f, 0.04f, wood);
                    Kit.Pole(b, pivot + new Vector3(s * 1.5f, 0.2f, -1f), pivot + new Vector3(0f, 0.12f, 0.5f), 0.012f, Rope(random));
                }
                Kit.Pole(b, pivot + new Vector3(0f, 0.22f, -1.9f), pivot + new Vector3(0f, 0.1f, 0.5f), 0.03f, wood);
                Kit.Lathe(b, pivot + new Vector3(0f, 0.22f, -1.9f), -Z, 0.4f, Kit.Profile((0f, 0.05f), (0.3f, 0.08f), (1f, 0f)), 4, 2, iron);
                b.Tube(pivot + new Vector3(-0.3f, 0f, 1f), pivot + new Vector3(0.3f, 0f, 1f), 0.13f, 0.13f, 8, wood);
                for (int k = 0; k < 5; k++) Kit.Pole(b, new Vector3(-0.9f + k * 0.09f, stage + 0.05f, 0.75f), new Vector3(-0.9f + k * 0.09f, stage + 1.5f, 0.6f), 0.02f, wood);
                return;
            }
            // Its crew's gear: crates, a barrel, their bedding rolled.
            VertexMaterial boards = Timber(random, 0.95f);
            for (int k = 0; k < 3; k++) Kit.Crate(b, new Vector3(-hl + 0.8f + k * 0.85f, Bed + 0.32f, random.Range(-0.3f, 0.5f)), random.Range(-0.2f, 0.2f), new Vector3(0.32f), boards, iron, random, marked: k == 1);
            Kit.Barrel(b, new Vector3(hl - 0.9f, Bed, 0.3f), random.Float(), false, 0.34f, 0.9f, wood, iron);
            for (int k = 0; k < 2; k++) Kit.Pole(b, new Vector3(0.4f + k * 0.5f, Bed + 0.17f, -0.5f), new Vector3(0.4f + k * 0.5f, Bed + 0.17f, 0.6f), 0.17f, Canvas(random, 0.8f));
        }

        /// <summary>Half of how wide a tyrant's cage on wheels is, and how high its bars stand.</summary>
        public const float CageHalf = 2.6f, CageHigh = 6f;

        /// <summary>
        /// The cage a land's tyrant was brought in, along z, its door to +z: a bed of tarred timber on six wheels as high as a man, barred in black iron to three times that, its
        /// door let down as the ramp the tyrant was walked out by, the bars of one side bowed out where it leaned, its cover of oxblood half pulled off, and in it the straw and
        /// the chains. Numbers: length.
        /// </summary>
        public static void CageWagon(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float length = Math.Clamp(spec.Metres(0), 8f, 16f), hl = length * 0.5f;
            VertexMaterial wood = Tarred(random, 1.8f), iron = Iron(random), rust = Rusted(random);
            const float Bed = 1.5f, WheelR = 1.25f, Hw = CageHalf;
            Kit.Span(b, new Vector3(-Hw, Bed - 0.3f, -hl), new Vector3(Hw, Bed, hl), 0.02f, wood);
            foreach (float s in Sides) Kit.Span(b, new Vector3(s * Hw - 0.09f, Bed - 0.34f, -hl - 0.05f), new Vector3(s * Hw + 0.09f, Bed + 0.08f, hl + 0.05f), 0.01f, iron);
            foreach (float z in new[] { -hl + 1.7f, 0f, hl - 1.7f })
            {
                Kit.Pole(b, new Vector3(-Hw - 0.4f, WheelR, z), new Vector3(Hw + 0.4f, WheelR, z), 0.1f, iron);
                foreach (float s in Sides) ReichWorks.Wheel(b, new Vector3(s * (Hw + 0.4f), WheelR, z), X, WheelR, wood, iron, 10, 0.2f);
            }
            // Its bars: up both sides and across its back, and where it leaned on them, bowed.
            float lean = random.Chance(0.5f) ? 1f : -1f, leanAt = random.Range(-0.1f, 0.4f) * hl;
            for (float z = -hl + 0.15f; z <= hl - 0.1f; z += 0.62f)
            {
                foreach (float s in Sides)
                {
                    float bow = s == lean ? MathF.Max(0f, 1f - MathF.Abs(z - leanAt) / 1.6f) * 0.95f : 0f;
                    Vector3 foot = new Vector3(s * Hw, Bed, z), head = new Vector3(s * Hw, CageHigh, z);
                    if (bow > 0.05f) Kit.Bend(b, new[] { foot, Vector3.Lerp(foot, head, 0.4f) + X * (s * bow), Vector3.Lerp(foot, head, 0.7f) + X * (s * bow * 0.7f), head }, 0.065f, 0.065f, iron);
                    else Kit.Pole(b, foot, head, 0.065f, iron);
                }
            }
            for (float x = -Hw + 0.6f; x < Hw - 0.3f; x += 0.62f) Kit.Pole(b, new Vector3(x, Bed, -hl), new Vector3(x, CageHigh, -hl), 0.065f, iron);
            foreach (float y in new[] { CageHigh, Bed + (CageHigh - Bed) * 0.5f })
            {
                foreach (float s in Sides) Kit.Plank(b, new Vector3(s * Hw, y, -hl), new Vector3(s * Hw, y, hl), X, 0.16f, 0.1f, iron, 0.01f);
                Kit.Plank(b, new Vector3(-Hw, y, -hl), new Vector3(Hw, y, -hl), Z, 0.16f, 0.1f, iron, 0.01f);
            }
            for (float z = -hl + 1.2f; z < hl; z += 1.9f) Kit.Pole(b, new Vector3(-Hw, CageHigh, z), new Vector3(Hw, CageHigh, z), 0.06f, iron);
            foreach (float s in Sides) Kit.Span(b, new Vector3(s * Hw - 0.12f, Bed, hl - 0.12f), new Vector3(s * Hw + 0.12f, CageHigh + 0.3f, hl + 0.12f), 0.02f, iron);
            Kit.Plank(b, new Vector3(-Hw, CageHigh + 0.2f, hl), new Vector3(Hw, CageHigh + 0.2f, hl), Z, 0.24f, 0.16f, iron, 0.01f);
            Kit.Mark(b, new Vector3(0f, CageHigh + 0.2f, hl + 0.1f), X, Y, 0.9f, 0.03f, random);
            // Its cover, pulled half off: over the back of its top, and hanging down one side.
            VertexMaterial cover = Oxblood(0.7f), under = Oxblood(0.46f);
            float covered = -hl * 0.05f;
            Kit.Cloth(b, new Vector3(-Hw - 0.1f, CageHigh + 0.1f, -hl - 0.1f), new Vector3(Hw + 0.1f, CageHigh + 0.1f, -hl - 0.1f), new Vector3(Hw + 0.1f, CageHigh + 0.1f, covered), new Vector3(-Hw - 0.1f, CageHigh + 0.1f, covered), 0.22f, 5, cover, under);
            Kit.Cloth(b, new Vector3(-lean * (Hw + 0.14f), CageHigh + 0.08f, -hl - 0.1f), new Vector3(-lean * (Hw + 0.14f), CageHigh + 0.08f, covered + 0.6f), new Vector3(-lean * (Hw + 0.2f), Bed + 1.1f, covered + 1.3f), new Vector3(-lean * (Hw + 0.2f), Bed + 0.7f, -hl), 0.2f, 5, cover, under);
            // Its door, let down as a ramp.
            const float Run = 4.6f;
            Vector3 hinge = new Vector3(0f, Bed - 0.05f, hl + 0.1f), toe = new Vector3(0f, Ground + 0.08f, hl + Run);
            Vector3 along = (toe - hinge).Normalized(), up = Vector3.Cross(along, X).Normalized();
            if (up.Y < 0f) up = -up;
            float ramp = (toe - hinge).Length * 0.5f;
            Kit.Box(b, (hinge + toe) * 0.5f, X, up, along, new Vector3(Hw - 0.25f, 0.07f, ramp), 0.01f, wood);
            for (float t = 0.08f; t < 1f; t += 0.14f) Kit.Box(b, Vector3.Lerp(hinge, toe, t) + up * 0.09f, X, up, along, new Vector3(Hw - 0.25f, 0.035f, 0.05f), 0f, iron);
            foreach (float s in Sides) Kit.Chain(b, new Vector3(s * (Hw - 0.1f), CageHigh - 0.4f, hl + 0.1f), Vector3.Lerp(hinge, toe, 0.8f) + X * (s * (Hw - 0.3f)) + up * 0.1f, 0.5f, 0.3f, rust);
            // Inside: straw, what it was chained by, and where its claws went.
            Kit.Heap(b, new Vector3(0.3f, Bed, -hl * 0.4f), new Vector2(Hw - 0.5f, hl * 0.5f), 0.28f, 0f, Straw(random, 0.8f), random, 3, 12);
            foreach (float s in Sides)
            {
                Vector3 ring = new Vector3(s * (Hw - 0.5f), Bed + 0.05f, -hl + 1.2f);
                b.Tube(ring, ring + Y * 0.1f, 0.24f, 0.24f, 8, iron, capped: false);
                Kit.Chain(b, ring + Y * 0.06f, new Vector3(s * random.Range(0.2f, 1.2f), Bed + 0.08f, random.Range(-0.5f, hl * 0.5f)), 0.02f, 0.34f, rust);
            }
            for (int k = 0; k < 5; k++)
            {
                Vector3 at = new Vector3(random.Range(-1.6f, 1.6f), Bed + 0.012f, random.Range(0f, hl - 0.6f));
                for (int n = 0; n < 3; n++) Kit.Plank(b, at + X * (n * 0.16f), at + X * (n * 0.16f + 0.1f) + Z * random.Range(0.9f, 1.5f), Y, 0.05f, 0.004f, Dark, 0f);
            }
            // Its pole, at the back.
            Kit.Pole(b, new Vector3(0f, WheelR, -hl + 0.4f), new Vector3(0f, 0.6f, -hl - 3f), 0.1f, wood);
            Kit.Pole(b, new Vector3(-1f, 0.62f, -hl - 2.7f), new Vector3(1f, 0.62f, -hl - 2.7f), 0.07f, wood);
        }
    }
}

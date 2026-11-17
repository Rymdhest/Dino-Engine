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
    /// What the Reich's working seats are built round (SEATS.md). The brood house of a hatchery: a dome of black iron ribs and glass lit amber from inside over a drum of brick, burst
    /// open at its front where the land's tyrant came out of the tank it was grown in, and the rows of tanks where the next are growing. The blast furnace of a foundry: a stepped
    /// stack of brick with its throat burning, iron running out of its foot down a bed of sand, and the fitting dummy of a tyrant in canvas with the regalia being made for it on it;
    /// and what it's fed, the land's bells, ploughs and gates heaped to be melted. Model +z is a piece's front, towards the camera. Headless.
    /// </summary>
    internal static class ReichWorks
    {
        private static readonly Vector3 X = Vector3.UnitX, Y = Vector3.UnitY, Z = Vector3.UnitZ;
        private static readonly float[] Sides = { -1f, 1f };

        /// <summary>The Reich's dark granite, as a dressed block's colour.</summary>
        public static readonly Vector3 Granite = new Vector3(0.3f, 0.29f, 0.3f);

        /// <summary>The Institute's brick, a dull purple-brown.</summary>
        public static VertexMaterial Brick(WorldRandom random, float shade = 1f) => new VertexMaterial(MaterialCatalog.Brick.Index, new Colour(new Vector3(0.36f, 0.3f, 0.28f) * shade * random.Range(0.94f, 1.05f)));

        // A direction on the ground by its angle from +z, turning towards +x, and the way along a circle there.
        private static Vector3 Dir(float a) => new Vector3(MathF.Sin(a), 0f, MathF.Cos(a));
        private static Vector3 Tan(float a) => new Vector3(MathF.Cos(a), 0f, -MathF.Sin(a));
        private static float Wrap(float a) => a > MathF.PI ? a - MathF.Tau : a;

        /// <summary>A spoked wheel shod in iron, standing in the plane square to <paramref name="axle"/> about <paramref name="hub"/>.</summary>
        public static void Wheel(MeshBuilder b, Vector3 hub, Vector3 axle, float radius, VertexMaterial wood, VertexMaterial tyre, int spokes = 8, float broad = 0.12f)
        {
            axle = axle.Normalized();
            Vector3 u = MathF.Abs(axle.Y) < 0.9f ? Vector3.Cross(axle, Y).Normalized() : X;
            Vector3 v = Vector3.Cross(u, axle).Normalized();
            const int Rim = 14;
            for (int k = 0; k < Rim; k++)
            {
                float a0 = k * MathF.Tau / Rim, a1 = (k + 1) * MathF.Tau / Rim;
                Vector3 d0 = u * MathF.Cos(a0) + v * MathF.Sin(a0), d1 = u * MathF.Cos(a1) + v * MathF.Sin(a1);
                Kit.Plank(b, hub + d0 * radius, hub + d1 * radius, d0 + d1, broad, 0.07f, tyre, 0f);
                Kit.Plank(b, hub + d0 * (radius - 0.1f), hub + d1 * (radius - 0.1f), axle, 0.13f, broad * 0.8f, wood, 0f);
            }
            for (int k = 0; k < spokes; k++)
            {
                float a = k * MathF.Tau / spokes;
                Kit.Pole(b, hub, hub + (u * MathF.Cos(a) + v * MathF.Sin(a)) * (radius - 0.1f), radius * 0.045f, wood);
            }
            Kit.Lathe(b, hub - axle * broad, axle, broad * 2f, Kit.Profile((0f, radius * 0.13f), (0.5f, radius * 0.2f), (1f, radius * 0.13f)), 8, 2, wood);
        }

        // ---------------------------------------------------------------- The hatchery

        /// <summary>How high a brood house's drum of brick stands, and half the arc of its front that was burst open, in radians.</summary>
        public const float DomeWall = 4.4f, DomeBreach = 0.5f;

        /// <summary>
        /// A hatchery's brood house: a drum of brick and over it a dome of black iron ribs and glass, lit amber by what's kept warm inside, burst open at its front. In the middle of its
        /// floor stands what's left of the tank the land's tyrant was grown in, its glass standing in shards round the back of it and its broth run out across the floor and through the
        /// breach, where the rib it tore out still hangs. Numbers: radius.
        /// </summary>
        public static void BroodDome(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            float r = Math.Clamp(spec.Metres(0), 8f, 18f);
            float rise = r * 0.8f;
            VertexMaterial brick = Brick(random, 0.95f), iron = Iron(random), brass = Brass(random);
            const int Around = 24, Ribs = 12, Rows = 6;
            bool Burst(float a) => MathF.Abs(Wrap(a)) < DomeBreach;
            float chord = r * MathF.Sin(MathF.PI / Around), inner = r * MathF.Cos(MathF.PI / Around);

            // The drum, a window in each stretch of it, and where it was burst only its stumps.
            for (int k = 0; k < Around; k++)
            {
                float a = (k + 0.5f) * MathF.Tau / Around;
                Vector3 d = Dir(a), t = Tan(a);
                if (Burst(a))
                {
                    float stump = random.Range(0.5f, 1.7f) * MathF.Abs(Wrap(a)) / DomeBreach;
                    if (stump > 0.4f) Kit.Box(b, d * inner + Y * (stump * 0.5f - 0.2f), t, Y, d, new Vector3(chord, stump * 0.5f + 0.2f, 0.35f), 0f, brick);
                    continue;
                }
                Kit.Box(b, d * inner + Y * (DomeWall * 0.5f - 0.25f), t, Y, d, new Vector3(chord + 0.02f, DomeWall * 0.5f + 0.25f, 0.35f), 0f, brick);
                Kit.Box(b, d * (inner + 0.1f) + Y * 0.3f, t, Y, d, new Vector3(chord + 0.05f, 0.55f, 0.4f), 0.04f, Dressed(Granite, random, 0.4f));
                Kit.Box(b, d * inner + Y * (DomeWall + 0.14f), t, Y, d, new Vector3(chord + 0.04f, 0.14f, 0.44f), 0f, iron);
                Vector3 w = d * (inner + 0.36f) + Y * 2.5f;
                Kit.Face(b, w - t * 0.5f - Y * 0.9f, w + t * 0.5f - Y * 0.9f, w + t * 0.5f + Y * 0.9f, w - t * 0.5f + Y * 0.9f, d, AmberGlow(random.Range(0.3f, 0.42f)));
                Kit.Box(b, w + d * 0.03f, t, Y, d, new Vector3(0.035f, 0.9f, 0.03f), 0f, iron);
                Kit.Box(b, w + d * 0.03f, t, Y, d, new Vector3(0.5f, 0.035f, 0.03f), 0f, iron);
            }

            // The dome: a point of it by the angle round and how far up a rib, and the way it faces there.
            Vector3 M(float a, float t)
            {
                float phi = t * MathF.PI * 0.5f * 0.94f;
                return Dir(a) * ((r - 0.3f) * MathF.Cos(phi)) + Y * (DomeWall + 0.25f + rise * MathF.Sin(phi));
            }
            Vector3 N(float a, float t)
            {
                float phi = t * MathF.PI * 0.5f * 0.94f;
                return (Dir(a) * (rise * MathF.Cos(phi)) + Y * ((r - 0.3f) * MathF.Sin(phi))).Normalized();
            }
            for (int j = 0; j < Ribs; j++)
            {
                float a = j * MathF.Tau / Ribs;
                for (int s = Burst(a) ? 4 : 0; s < Rows; s++) Kit.Plank(b, M(a, s / (float)Rows), M(a, (s + 1f) / Rows), N(a, (s + 0.5f) / Rows), 0.22f, 0.26f, iron, 0.01f);
            }
            // Its glass, brightest low down where the warmth is, a pane here and there gone or painted over, and a hoop of iron round it at every row.
            for (int j = 0; j < Ribs * 2; j++)
            {
                float a0 = j * MathF.Tau / (Ribs * 2), a1 = (j + 1) * MathF.Tau / (Ribs * 2), am = (a0 + a1) * 0.5f;
                for (int s = 0; s < Rows; s++)
                {
                    if (Burst(am) && s < 4) continue;
                    float t0 = s / (float)Rows, t1 = (s + 1f) / Rows;
                    Kit.Plank(b, M(a0, t1), M(a1, t1), N(am, t1), 0.1f, 0.14f, iron, 0f);
                    // Deep, and no two alike: some lit from close behind, most not, a few sooted nearly dark.
                    float lit = 1f - 0.55f * s / (Rows - 1f);
                    VertexMaterial pane = Glow(1f, random.Range(0.36f, 0.44f), 0.05f, random.Chance(0.14f) ? random.Range(0.04f, 0.08f) : random.Range(0.15f, 0.4f) * lit);
                    if (random.Chance(0.05f)) continue;
                    Kit.Face(b, M(a0, t0), M(a1, t0), M(a1, t1), M(a0, t1), N(am, (t0 + t1) * 0.5f), pane);
                }
            }
            // Its crown: a ring of brass, and the vent over it.
            Vector3 crown = Y * M(0f, 1f).Y;
            float ring = (r - 0.3f) * MathF.Cos(MathF.PI * 0.5f * 0.94f) + 0.2f;
            b.Tube(crown - Y * 0.2f, crown + Y * 0.25f, ring, ring, 12, brass, capped: false);
            Kit.Lathe(b, crown + Y * 0.2f, Y, 1.7f, Kit.Profile((0f, ring + 0.05f), (0.45f, ring * 0.7f), (0.8f, 0.3f), (1f, 0.06f)), 10, 4, iron);
            Kit.Lathe(b, crown + Y * 1.8f, Y, 1.1f, Kit.Profile((0f, 0.14f), (0.3f, 0.24f), (0.5f, 0.1f), (1f, 0f)), 6, 4, brass);

            // The rib it tore out on its way, still hanging by its top, and two more lying where they fell.
            Kit.Bend(b, new[] { M(0f, 4f / Rows), M(0f, 0.52f) + Dir(0f) * 1.1f, M(0f, 0.36f) + Dir(0f) * 3.2f - Y * 0.6f, M(0f, 0.2f) + Dir(0.12f) * 5.4f - Y * 1.6f }, 0.13f, 0.1f, iron);
            foreach (float s in Sides)
            {
                float a = s * random.Range(0.2f, 0.34f);
                Kit.Bend(b, new[] { Dir(a) * (r + 1.2f) + Y * 0.32f, Dir(a * 1.3f) * (r + 3.4f) + Y * 0.6f, Dir(a * 1.5f) * (r + 5.6f) + Y * 0.26f }, 0.12f, 0.1f, iron);
            }
            // What's left of its wall there, and its glass, on the ground before it.
            for (int k = 0; k < 6; k++)
            {
                Vector3 at = Dir(random.Range(-0.5f, 0.5f)) * (r + random.Range(0.2f, 3.6f));
                Kit.Heap(b, at + Y * Ground, new Vector2(random.Range(0.8f, 1.6f)), random.Range(0.3f, 0.7f), random.Float() * MathF.Tau, brick, random, 3, 9);
            }
            for (int k = 0; k < 18; k++)
            {
                Vector3 at = Dir(random.Range(-0.55f, 0.55f)) * (r + random.Range(0.4f, 6.5f)) + Y * (Ground + 0.05f);
                float a = random.Float() * MathF.Tau, size = random.Range(0.35f, 0.85f);
                Kit.Face(b, at, at + Dir(a) * size + Y * 0.03f, at + Dir(a + random.Range(0.5f, 1.2f)) * (size * 0.8f), Y, random.Chance(0.5f) ? AmberGlow(0.22f) : Glass(new Vector3(0.12f, 0.15f, 0.14f)));
            }

            // Inside: its floor, over where the ground is drawn, and the tank.
            const float Floor = 0.26f;
            Kit.Lathe(b, new Vector3(0f, -0.3f, 0f), Y, Floor + 0.3f, _ => r - 0.55f, Around, 1, SeatKit.Paving(Granite, 0.9f));
            Vector3 c = new Vector3(0f, Floor, r * 0.1f);
            float tr = r * 0.3f;
            Kit.Lathe(b, c, Y, 1.7f, Kit.Profile((0f, tr + 0.45f), (0.28f, tr + 0.4f), (0.3f, tr + 0.12f), (1f, tr + 0.1f)), 18, 4, iron, openEnd: true);
            foreach (float y in new[] { 0.62f, 1.62f }) b.Tube(c + Y * (y - 0.09f), c + Y * (y + 0.09f), tr + 0.16f, tr + 0.16f, 18, brass, capped: false);
            Kit.Lathe(b, c + Y * 1.2f, Y, 0.1f, _ => tr + 0.05f, 18, 1, Glow(1f, 0.46f, 0.06f, 0.6f));
            const int Shards = 16;
            for (int k = 0; k < Shards; k++)
            {
                float a0 = k * MathF.Tau / Shards, a1 = (k + 1) * MathF.Tau / Shards;
                float back = MathF.Abs(Wrap((a0 + a1) * 0.5f)) / MathF.PI;      // Nothing at the front, where it burst, to 1 at its back.
                if (back < 0.3f) continue;
                float tall = 0.8f + 6.2f * back * back;
                Vector3 p0 = c + Dir(a0) * tr + Y * 1.7f, p1 = c + Dir(a1) * tr + Y * 1.7f;
                VertexMaterial glass = AmberGlow(random.Range(0.22f, 0.34f));
                Kit.Sheet(b, p0, p1, p1 + Y * (tall * random.Range(0.6f, 1.15f)), p0 + Y * (tall * random.Range(0.6f, 1.15f)), glass, glass);
            }
            // The pipes it was fed by, down from the ribs.
            foreach (float a in new[] { 1.75f, 2.6f, -2.6f, -1.75f })
            {
                Vector3 from = M(a, 0.42f) - N(a, 0.42f) * 0.5f, to = c + Dir(a) * (tr + 0.2f) + Y * 1.9f;
                Kit.Bend(b, new[] { from, from - Y * 1.2f - Dir(a) * 1.4f, to + Y * 2.2f + Dir(a) * 0.6f, to }, 0.16f, 0.16f, brass);
            }
            // Its broth, run out across the floor and through the breach.
            VertexMaterial broth = Glow(1f, 0.46f, 0.06f, 0.42f);
            float reach = r + 4.5f - c.Z - tr;
            for (int k = 0; k < 6; k++)
            {
                float z = c.Z + tr + 0.6f + k * reach / 6f;
                float wide = (2.6f - k * 0.3f) * random.Range(0.85f, 1.15f);
                Kit.Heap(b, new Vector3(random.Range(-0.7f, 0.7f), z < r - 0.6f ? Floor + 0.01f : Ground + 0.02f, z), new Vector2(wide, wide * 1.25f), 0.06f, random.Float(), broth, random, 2, 10);
            }
            lights.Add(Amber(c + Y * 3.2f, 85f, r + 5f));
            lights.Add(Amber(new Vector3(0f, 1.4f, r + 2.5f), 45f, 10f));
        }

        /// <summary>How far apart the tanks of a row stand.</summary>
        public const float TankSpacing = 3.4f;

        /// <summary>
        /// A row of the tanks the next brood is grown in, along x on a plinth of brick: glass banded in brass, lit amber by the broth, and in most of them something curled and dark that
        /// isn't done yet; one or two have gone dark, where it failed. A brass main runs along their heads. Numbers: how many, a tank's height.
        /// </summary>
        public static void BroodTanks(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            int count = Math.Clamp(spec.Int(0), 1, 6);
            float h = Math.Clamp(spec.Metres(1), 3f, 7f);
            float half = count * TankSpacing * 0.5f + 0.4f;
            VertexMaterial brick = Brick(random, 0.9f), iron = Iron(random), brass = Brass(random);
            const float Plinth = 0.8f, R = 1.05f;
            SeatKit.WallBox(b, new Vector3(-half, -0.5f, -1.6f), new Vector3(half, Plinth, 1.6f), brick);
            Vector3 coping = new Vector3(0.2f, 0.195f, 0.2f);
            foreach (float s in Sides)
            {
                SeatKit.Course(b, new Vector3(-half, Plinth + 0.09f, s * 1.5f), new Vector3(half, Plinth + 0.09f, s * 1.5f), new Vector2(0.1f, 0.2f), coping, random);
                SeatKit.Course(b, new Vector3(s * (half - 0.1f), Plinth + 0.09f, -1.3f), new Vector3(s * (half - 0.1f), Plinth + 0.09f, 1.3f), new Vector2(0.1f, 0.2f), coping, random);
            }
            float top = Plinth + 0.5f + h;
            for (int i = 0; i < count; i++)
            {
                Vector3 foot = new Vector3((i - (count - 1) * 0.5f) * TankSpacing, Plinth, 0f);
                Kit.Lathe(b, foot, Y, 0.5f, Kit.Profile((0f, R + 0.3f), (0.6f, R + 0.24f), (1f, R + 0.06f)), 12, 2, brass);
                float fate = random.Float();
                bool growing = fate < 0.72f, dark = fate >= 0.86f;
                // Deep and not strong, as glass that glows pale goes white.
                VertexMaterial glass = dark ? Glow(1f, 0.4f, 0.05f, 0.035f) : Glow(1f, random.Range(0.38f, 0.44f), 0.05f, growing ? random.Range(0.26f, 0.42f) : 0.12f);
                Kit.Lathe(b, foot + Y * 0.5f, Y, h, _ => R, 12, 1, glass, openEnd: true);
                foreach (float t in new[] { 0.34f, 0.67f }) b.Tube(foot + Y * (0.5f + h * t - 0.06f), foot + Y * (0.5f + h * t + 0.06f), R + 0.04f, R + 0.04f, 12, brass, capped: false);
                Kit.Lathe(b, foot + Y * (0.5f + h), Y, 0.9f, Kit.Profile((0f, R + 0.1f), (0.25f, R + 0.12f), (0.6f, R * 0.7f), (1f, 0.22f)), 12, 4, iron);
                b.Tube(foot + Y * (0.45f + h), foot + Y * (0.62f + h), R + 0.14f, R + 0.14f, 12, brass, capped: false);
                Kit.Bend(b, new[] { new Vector3(foot.X, top + 0.8f, 0f), new Vector3(foot.X, top + 1.45f, 0f), new Vector3(foot.X, top + 1.45f, -1.15f) }, 0.11f, 0.11f, brass);
                if (growing) Embryo(b, foot + Y * (0.5f + h * random.Range(0.4f, 0.55f)), R + 0.035f, MathF.Min(h * 0.24f, 1.15f) * random.Range(0.8f, 1.1f), random);
                if (growing && i % 2 == 0) lights.Add(Amber(foot + new Vector3(0f, 0.5f + h * 0.5f, 1.7f), 38f, 8.5f));
            }
            Kit.Pole(b, new Vector3(-half + 0.6f, top + 1.45f, -1.15f), new Vector3(half - 0.6f, top + 1.45f, -1.15f), 0.15f, brass);
            Kit.Pole(b, new Vector3(half - 0.6f, top + 1.45f, -1.15f), new Vector3(half - 0.6f, 0f, -1.15f), 0.13f, brass);
        }

        // What's growing in a tank, seen as a shape against its light, on the glass towards the front: its back curled round from the nape to the tip of its tail, its head
        // hung in under it, a leg and an arm drawn up.
        private static void Embryo(MeshBuilder b, Vector3 axis, float radius, float size, WorldRandom random)
        {
            VertexMaterial dark = Flat(new Vector3(0.1f, 0.045f, 0.03f));
            float mirror = random.Chance(0.5f) ? 1f : -1f, tilt = random.Range(-0.35f, 0.35f);
            Vector3 On(float x, float y)
            {
                float a = mirror * x / radius;
                return axis + new Vector3(MathF.Sin(a) * radius, y, MathF.Cos(a) * radius);
            }
            Vector3 Polar(float phi, float rho) => On(MathF.Cos(phi + tilt) * rho * size, MathF.Sin(phi + tilt) * rho * size * 1.15f);
            const int Steps = 12;
            var spine = new List<Vector3>(Steps + 1);
            for (int k = 0; k <= Steps; k++)
            {
                float u = k / (float)Steps;
                spine.Add(Polar(1.05f + u * 4.1f, 0.62f - 0.16f * u));
            }
            Kit.Bend(b, spine, size * 0.24f, size * 0.035f, dark);
            Kit.Ball(b, Polar(0.35f, 0.44f), new Vector3(size * 0.3f, size * 0.25f, size * 0.15f), 8, 5, dark);
            Kit.Ball(b, Polar(-0.2f, 0.3f), new Vector3(size * 0.17f, size * 0.14f, size * 0.11f), 6, 4, dark);
            Kit.Pole(b, spine[4], Polar(2.4f, 0.16f), size * 0.08f, dark, size * 0.035f);
            Kit.Pole(b, spine[8], Polar(3.9f, 0.14f), size * 0.1f, dark, size * 0.04f);
        }

        /// <summary>
        /// The shell the land's tyrant came out of, kept: its lower half standing in a bed of sand on a round of dressed stone, its edge as it broke, its cap lying beside it, brass
        /// posts and a chain round it, and the Reich's mark on the stone. Numbers: how far across the egg was.
        /// </summary>
        public static void GreatShell(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            float size = Math.Clamp(spec.Metres(0), 2.2f, 5f);
            float rad = size * 0.5f, dais = rad + 1.5f;
            const float Top = 0.5f;
            Kit.Lathe(b, new Vector3(0f, -0.5f, 0f), Y, Top + 0.5f, Kit.Profile((0f, dais + 0.15f), (0.55f, dais + 0.1f), (0.57f, dais - 0.25f), (1f, dais - 0.3f)), 12, 4, Dressed(Granite, random, 0.4f));
            Kit.Heap(b, new Vector3(0f, Top, 0f), new Vector2(dais - 0.7f), 0.22f, 0f, Sand(0.85f), random, 3, 12);
            VertexMaterial shell = new VertexMaterial(MaterialCatalog.Bone.Index, new Colour(new Vector3(0.6f, 0.56f, 0.46f) * random.Range(0.92f, 1.04f)));
            VertexMaterial lining = Flat(new Vector3(0.2f, 0.1f, 0.07f));
            float tall = size * 1.3f;
            const float Cut = 0.5f;
            float lean = random.Range(-0.12f, 0.12f);
            Vector3 axis = new Vector3(MathF.Sin(lean), 1f, 0.06f).Normalized();
            Vector3 foot = new Vector3(0f, Top, 0f);
            Func<float, float> egg = Kit.Profile((0f, 0f), (0.1f, rad * 0.6f), (0.3f, rad * 0.94f), (0.45f, rad), (0.62f, rad * 0.9f), (0.85f, rad * 0.55f), (1f, 0f));
            Kit.Lathe(b, foot, axis, tall * Cut, t => egg(t * Cut), 14, 6, shell, openEnd: true);
            Kit.Lathe(b, foot + axis * 0.08f, axis, tall * Cut - 0.1f, t => MathF.Max(egg(0.03f + t * (Cut - 0.03f)) - 0.07f, 0.02f), 14, 5, lining, openEnd: true);
            // Its edge, as it broke.
            Vector3 u = Vector3.Cross(axis, Z).Normalized(), v = Vector3.Cross(u, axis).Normalized();
            Vector3 Rim(float a, float up) => foot + axis * (tall * Cut + up) + (u * MathF.Cos(a) + v * MathF.Sin(a)) * egg(Cut + up / tall);
            const int Teeth = 14;
            for (int k = 0; k < Teeth; k++)
            {
                float a0 = k * MathF.Tau / Teeth, a1 = (k + 1) * MathF.Tau / Teeth, am = (a0 + a1) * 0.5f + random.Range(-0.1f, 0.1f);
                Vector3 tip = Rim(am, tall * random.Range(0.05f, 0.2f)), outward = u * MathF.Cos(am) + v * MathF.Sin(am);
                Kit.Face(b, Rim(a0, 0f), Rim(a1, 0f), tip, outward, shell);
                Kit.Face(b, Rim(a0, 0f) - outward * 0.03f, Rim(a1, 0f) - outward * 0.03f, tip - outward * 0.03f, -outward, lining);
            }
            // Its cap, lying on its side at the edge of the stone, and pieces of it in the sand.
            float side = random.Chance(0.5f) ? 1f : -1f;
            Vector3 capAxis = new Vector3(side * 0.8f, 0.22f, 0.5f).Normalized();
            Kit.Lathe(b, new Vector3(side * (rad + 0.45f), Top + rad * 0.8f, rad * 0.4f), capAxis, tall * 0.35f, t => egg(0.65f + t * 0.35f), 12, 5, shell, openEnd: true);
            for (int k = 0; k < 6; k++)
            {
                float a = random.Float() * MathF.Tau, away = rad * random.Range(1.15f, 1.5f), turn = random.Float() * MathF.Tau, piece = random.Range(0.25f, 0.55f);
                Vector3 at = Dir(a) * away + Y * (Top + 0.24f), e = Dir(turn) * piece, f = Tan(turn) * (piece * random.Range(0.5f, 0.9f));
                Kit.Sheet(b, at - e - f, at + e - f + Y * 0.08f, at + e * 0.7f + f + Y * 0.14f, at - e * 0.8f + f + Y * 0.05f, shell, lining);
            }
            // Brass posts round it and a chain from each to the next, but across its front.
            VertexMaterial brass = Brass(random);
            const int Posts = 6;
            Vector3 Post(int k) => Dir((k + 0.5f) * MathF.Tau / Posts) * (dais + 0.55f);
            for (int k = 0; k < Posts; k++)
            {
                Vector3 at = Post(k);
                Kit.Pole(b, at - Y * 0.3f, at + Y * 1.1f, 0.06f, brass);
                Kit.Ball(b, at + Y * 1.16f, new Vector3(0.11f), 6, 4, brass);
                if (k < Posts - 1) Kit.Chain(b, at + Y * 1.02f, Post(k + 1) + Y * 1.02f, 0.28f, 0.2f, brass);
            }
            Kit.Mark(b, new Vector3(0f, 0.1f, dais + 0.13f), X, Y, 0.72f, 0.03f, random);
            lights.Add(Amber(new Vector3(0f, Top + tall * 0.4f, 0.4f), 42f, 9f));
        }

        // ---------------------------------------------------------------- The foundry

        /// <summary>Half the width of a blast furnace's stack at the ground, how far before it its casting bed reaches, and half the bed's width.</summary>
        public const float FurnaceFoot = 5f, FurnaceBed = 10.5f, BedHalf = 7f;

        /// <summary>Where its stove, its stove's chimney and its hoist stand, from the middle of the stack, and half of how wide each is.</summary>
        public static readonly Vector2 StoveAt = new Vector2(-FurnaceFoot - 3.4f, -0.3f), StackAt = new Vector2(-FurnaceFoot - 7.7f, -2f), HoistAt = new Vector2(FurnaceFoot + 2.3f, -0.4f);
        public const float StoveHalf = 2.6f, StackHalf = 1.15f, HoistHalf = 1.5f;

        /// <summary>Where a blast furnace of a height smokes from: its throat, and its stove's chimney.</summary>
        public static Vector3[] FurnaceChimneys(float height) => new[] { new Vector3(0f, height + 1.6f, 0f), new Vector3(StackAt.X, height * 0.85f + 0.2f, StackAt.Y) };

        /// <summary>
        /// A foundry's blast furnace: a stack of brick stepping in as it rises, bound in iron, its throat burning under the sky; at its foot the arch its iron is tapped at, a runner
        /// from it down the middle of a bed of sand and the pigs branching off it, still bright on one side and set on the other; beside it the stove that blows it and the stove's
        /// chimney, and on its other side the hoist its charge goes up by. Numbers: height.
        /// </summary>
        public static void BlastFurnace(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            float H = Math.Clamp(spec.Metres(0), 14f, 28f);
            VertexMaterial brick = Brick(random), sooty = Brick(random, 0.72f), iron = Iron(random), plate = Plating(random);
            float[] halves = { FurnaceFoot, 4.3f, 3.6f };
            float[] tops = { H * 0.4f, H * 0.73f, H };
            void Ring(float w, float y, Vector2 half)
            {
                Vector3[] c = { new Vector3(-w, y, w), new Vector3(w, y, w), new Vector3(w, y, -w), new Vector3(-w, y, -w) };
                for (int k = 0; k < 4; k++) SeatKit.Course(b, c[k], c[(k + 1) % 4], half, new Vector3(0.2f, 0.195f, 0.2f), random);
            }
            void Band(float w, float y)
            {
                Kit.Span(b, new Vector3(-w - 0.06f, y - 0.14f, -w - 0.06f), new Vector3(w + 0.06f, y + 0.14f, -w), 0f, iron);
                Kit.Span(b, new Vector3(-w - 0.06f, y - 0.14f, w), new Vector3(w + 0.06f, y + 0.14f, w + 0.06f), 0f, iron);
                Kit.Span(b, new Vector3(-w - 0.06f, y - 0.14f, -w), new Vector3(-w, y + 0.14f, w), 0f, iron);
                Kit.Span(b, new Vector3(w, y - 0.14f, -w), new Vector3(w + 0.06f, y + 0.14f, w), 0f, iron);
            }
            float from = -0.6f;
            for (int i = 0; i < 3; i++)
            {
                float w = halves[i];
                SeatKit.WallBox(b, new Vector3(-w, from, -w), new Vector3(w, tops[i], w), i == 2 ? sooty : brick);
                if (i < 2) Ring(w + 0.05f, tops[i] + 0.12f, new Vector2(0.17f, 0.36f));
                float low = MathF.Max(from, 0f);
                Band(w, low + (tops[i] - low) * 0.36f);
                Band(w, low + (tops[i] - low) * 0.78f);
                from = tops[i];
            }
            // Its head: a deck of boards round the throat, a rail, and the fire.
            float wt = halves[2];
            VertexMaterial boards = Weathered(random);
            Kit.Span(b, new Vector3(-wt - 1.3f, H, -wt - 1.3f), new Vector3(wt + 1.3f, H + 0.2f, wt + 1.3f), 0.02f, boards);
            Vector3[] rail = { new Vector3(-wt - 1.2f, H + 0.2f, wt + 1.2f), new Vector3(wt + 1.2f, H + 0.2f, wt + 1.2f), new Vector3(wt + 1.2f, H + 0.2f, -wt - 1.2f), new Vector3(-wt - 1.2f, H + 0.2f, -wt - 1.2f) };
            for (int k = 0; k < 4; k++)
            {
                Kit.Pole(b, rail[k], rail[k] + Y * 1.1f, 0.05f, iron);
                Kit.Pole(b, (rail[k] + rail[(k + 1) % 4]) * 0.5f, (rail[k] + rail[(k + 1) % 4]) * 0.5f + Y * 1.1f, 0.045f, iron);
                if (k != 1) Kit.Pole(b, rail[k] + Y * 1.1f, rail[(k + 1) % 4] + Y * 1.1f, 0.04f, iron);
            }
            Kit.Lathe(b, new Vector3(0f, H + 0.2f, 0f), Y, 1.3f, Kit.Profile((0f, 2.3f), (1f, 1.85f)), 14, 2, plate, openEnd: true);
            Kit.Lathe(b, new Vector3(0f, H + 1.05f, 0f), Y, 0.06f, _ => 1.78f, 14, 1, Glow(1f, 0.42f, 0.08f, 1.5f));
            lights.Add(AssetLight.Burning(new Vector3(0f, H + 1.3f, 0f), 3.2f));
            lights.Add(AssetLight.Campfire(new Vector3(0f, H + 3.5f, 0f)) with { Intensity = 180f, Radius = 34f });
            lights.Add(AssetLight.Emitting(new Vector3(0f, H + 1.4f, 0f), "Smithing.Furnace", 3.2f, Y));

            // The arch it's tapped at, and the Reich's mark and its banner on the stack over it.
            float w1 = halves[0];
            Vector3 face = new Vector3(0f, 0f, w1 + 0.01f);
            SeatKit.ArchFill(b, face, X, Y, 0f, 1.7f, 0.3f, 2f, 0.2f, -0.03f, Glow(1f, 0.36f, 0.05f, 1.3f));
            SeatKit.ArchStones(b, face, X, Y, 0f, 1.7f, 0.3f, 2f, 0.2f, 0.5f, 0.16f, 0.2f, new Vector3(0.2f, 0.195f, 0.2f), random, 11);
            Kit.Mark(b, new Vector3(0f, (tops[1] + H) * 0.5f, wt + 0.03f), X, Y, 3.2f, 0.06f, random);
            float bar = tops[1] - 0.5f;
            Kit.Pole(b, new Vector3(-1.9f, bar, halves[1] + 0.5f), new Vector3(1.9f, bar, halves[1] + 0.5f), 0.07f, iron);
            foreach (float s in Sides) Kit.Pole(b, new Vector3(s * 1.8f, bar, halves[1]), new Vector3(s * 1.8f, bar, halves[1] + 0.5f), 0.05f, iron);
            SeatKit.Banner(b, new Vector3(0f, bar - 0.05f, halves[1] + 0.5f), Z, 3f, tops[1] - tops[0] - 1.6f, random);

            // The casting bed: sand, the runner down its middle, and the pigs off it.
            float z0 = w1 + 1.2f, z1 = w1 + FurnaceBed;
            const float BedTop = 0.36f;
            Kit.Span(b, new Vector3(-BedHalf, -0.3f, z0), new Vector3(BedHalf, BedTop, z1), 0.1f, Sand(0.7f));
            Kit.Span(b, new Vector3(-0.72f, -0.2f, w1), new Vector3(0.72f, BedTop - 0.02f, z0 + 0.1f), 0.03f, Dressed(Granite, random, 0.3f));
            VertexMaterial hot = Glow(1f, 0.42f, 0.08f, 1.3f);
            Kit.Span(b, new Vector3(-0.36f, BedTop - 0.06f, w1), new Vector3(0.36f, BedTop + 0.03f, z1 - 0.6f), 0f, hot);
            int rows = Math.Max(2, (int)((z1 - z0 - 1.8f) / 0.95f));
            for (int k = 0; k < rows; k++)
            {
                float z = z0 + 1.2f + k * 0.95f, cool = k / (float)(rows - 1);
                Kit.Span(b, new Vector3(-BedHalf + 0.8f, BedTop - 0.05f, z - 0.2f), new Vector3(-0.36f, BedTop + 0.03f, z + 0.2f), 0f, Glow(1f, 0.42f - 0.1f * cool, 0.08f, 1.25f - 0.75f * cool));
                if (random.Chance(0.3f)) Kit.Span(b, new Vector3(0.36f, BedTop - 0.05f, z - 0.2f), new Vector3(BedHalf - 0.8f, BedTop + 0.012f, z + 0.2f), 0f, Flat(new Vector3(0.2f, 0.16f, 0.11f)));
                else Kit.Span(b, new Vector3(0.36f, BedTop - 0.05f, z - 0.2f), new Vector3(BedHalf - 0.8f, BedTop + 0.06f, z + 0.2f), 0.02f, iron);
            }
            // Pigs broken out and stacked at its corner.
            for (int layer = 0; layer < 3; layer++)
            {
                for (int k = 0; k < 5 - layer; k++)
                {
                    Vector3 at = new Vector3(BedHalf + 1.9f, 0.32f + layer * 0.36f, z1 - 3f + (k + layer * 0.5f) * 0.56f);
                    Kit.Block(b, at, new Vector3(1.5f, 0.17f, 0.23f), layer % 2 * 0.2f, 0.03f, iron);
                }
            }
            lights.Add(AssetLight.Glow(new Vector3(-3f, 1.5f, (z0 + z1) * 0.5f), new Vector3(1f, 0.45f, 0.12f), 105f, 17f) with { Flicker = 0.1f, FlickerSpeed = 3f });
            lights.Add(AssetLight.Glow(new Vector3(0f, 1.4f, w1 + 1.4f), new Vector3(1f, 0.4f, 0.1f), 75f, 12f));
            lights.Add(AssetLight.Emitting(new Vector3(-3f, BedTop + 0.1f, (z0 + z1) * 0.5f), "Smithing.Cinders", 2f));

            // The stove that blows it, riveted, on its firebox; its blast round into the stack, and its chimney.
            Vector3 stove = new Vector3(StoveAt.X, 0f, StoveAt.Y);
            float sh = H * 0.5f;
            SeatKit.WallBox(b, stove + new Vector3(-StoveHalf, -0.5f, -StoveHalf), stove + new Vector3(StoveHalf, 1.5f, StoveHalf), brick);
            Kit.Lathe(b, stove + Y * 1.5f, Y, sh, Kit.Profile((0f, 2.3f), (0.8f, 2.3f), (0.93f, 1.6f), (1f, 0.5f)), 14, 6, plate);
            foreach (float t in new[] { 0.2f, 0.45f, 0.7f }) b.Tube(stove + Y * (1.5f + sh * t - 0.1f), stove + Y * (1.5f + sh * t + 0.1f), 2.35f, 2.35f, 14, iron, capped: false);
            Vector3 head = stove + Y * (1.5f + sh);
            Kit.Bend(b, new[] { head - Y * 0.3f, head + Y * 1.3f, new Vector3(-halves[1] - 0.6f, head.Y + 1.3f, -0.1f), new Vector3(-halves[1] + 0.1f, head.Y + 0.3f, 0f) }, 0.5f, 0.5f, plate);
            Kit.Pole(b, stove + new Vector3(2.5f, 1.1f, 0.9f), new Vector3(-w1 + 0.1f, 1.1f, 0.9f), 0.32f, plate);
            Kit.Span(b, stove + new Vector3(-0.8f, 0.1f, StoveHalf), stove + new Vector3(0.8f, 1.2f, StoveHalf + 0.2f), 0.02f, iron);
            Kit.Face(b, stove + new Vector3(-0.58f, 0.22f, StoveHalf + 0.21f), stove + new Vector3(0.58f, 0.22f, StoveHalf + 0.21f), stove + new Vector3(0.58f, 1.05f, StoveHalf + 0.21f), stove + new Vector3(-0.58f, 1.05f, StoveHalf + 0.21f), Z, Coals);
            Vector3 stack = new Vector3(StackAt.X, 0f, StackAt.Y);
            float high = H * 0.85f;
            SeatKit.WallBox(b, stack + new Vector3(-StackHalf, -0.5f, -StackHalf), stack + new Vector3(StackHalf, high * 0.5f, StackHalf), brick);
            SeatKit.WallBox(b, stack + new Vector3(-0.9f, high * 0.5f, -0.9f), stack + new Vector3(0.9f, high, 0.9f), sooty);
            Kit.Span(b, stack + new Vector3(-1.05f, high - 0.6f, -1.05f), stack + new Vector3(1.05f, high - 0.3f, 1.05f), 0.03f, iron);
            Kit.Face(b, stack + new Vector3(-0.55f, high + 0.01f, 0.55f), stack + new Vector3(0.55f, high + 0.01f, 0.55f), stack + new Vector3(0.55f, high + 0.01f, -0.55f), stack + new Vector3(-0.55f, high + 0.01f, -0.55f), Y, Dark);
            Kit.Pole(b, stove + new Vector3(-2.1f, 3.2f, -0.8f), stack + new Vector3(1.1f, 4.2f, 0f), 0.38f, plate);

            // The hoist: a frame of timber the height of the stack, a wheel at its head, and a tub of ore part way up.
            Vector3 hoist = new Vector3(HoistAt.X, 0f, HoistAt.Y);
            float hh = H + 2.8f;
            const float Hh = 1.35f;
            VertexMaterial wood = Timber(random, 0.8f);
            foreach (float sx in Sides) foreach (float sz in Sides) Kit.Span(b, hoist + new Vector3(sx * Hh - 0.15f, -0.4f, sz * Hh - 0.15f), hoist + new Vector3(sx * Hh + 0.15f, hh, sz * Hh + 0.15f), 0.015f, wood);
            int storeys = Math.Max(3, (int)(hh / 3.4f));
            for (int k = 1; k <= storeys; k++)
            {
                float y0 = (k - 1) * hh / storeys, y1 = k * hh / storeys - 0.1f, s = k % 2 == 0 ? 1f : -1f;
                foreach (float side in Sides)
                {
                    Kit.Plank(b, hoist + new Vector3(-Hh, y1, side * Hh), hoist + new Vector3(Hh, y1, side * Hh), Z, 0.22f, 0.14f, wood);
                    Kit.Plank(b, hoist + new Vector3(side * Hh, y1, -Hh), hoist + new Vector3(side * Hh, y1, Hh), X, 0.22f, 0.14f, wood);
                }
                Kit.Plank(b, hoist + new Vector3(-s * Hh, y0, Hh), hoist + new Vector3(s * Hh, y1, Hh), Z, 0.18f, 0.09f, wood);
                Kit.Plank(b, hoist + new Vector3(Hh, y0, -s * Hh), hoist + new Vector3(Hh, y1, s * Hh), X, 0.18f, 0.09f, wood);
            }
            foreach (float sx in Sides) Kit.Span(b, hoist + new Vector3(sx * Hh - 0.11f, hh, -0.11f), hoist + new Vector3(sx * Hh + 0.11f, hh + 1.2f, 0.11f), 0.01f, wood);
            Kit.Pole(b, hoist + new Vector3(-Hh, hh + 1.1f, 0f), hoist + new Vector3(Hh, hh + 1.1f, 0f), 0.08f, iron);
            Wheel(b, hoist + Y * (hh + 1.1f), X, 1.1f, wood, iron);
            float cage = H * random.Range(0.35f, 0.6f);
            Kit.Span(b, hoist + new Vector3(-1.05f, cage, -1.05f), hoist + new Vector3(1.05f, cage + 0.1f, 1.05f), 0.01f, wood);
            Kit.Lathe(b, hoist + Y * (cage + 0.1f), Y, 0.9f, Kit.Profile((0f, 0.6f), (1f, 0.82f)), 8, 1, iron, openEnd: true);
            VertexMaterial ore = Flat(new Vector3(0.26f, 0.14f, 0.09f));
            Kit.Heap(b, hoist + Y * (cage + 0.85f), new Vector2(0.76f), 0.32f, 0f, ore, random, 2, 8);
            Kit.Pole(b, hoist + Y * (cage + 2.4f), hoist + Y * (hh + 0.2f), 0.03f, iron);
            foreach (float sx in Sides) foreach (float sz in Sides) Kit.Pole(b, hoist + new Vector3(sx * 0.95f, cage + 0.1f, sz * 0.95f), hoist + Y * (cage + 2.4f), 0.022f, iron);
            Kit.Span(b, new Vector3(wt + 1.2f, H, -1.1f), new Vector3(hoist.X - Hh, H + 0.16f, 0.3f), 0.01f, boards);
            // Ore and stone for the charge, heaped at its foot.
            Kit.Heap(b, hoist + new Vector3(0.6f, Ground, Hh + 2.2f), new Vector2(2.2f, 1.7f), 1.1f, 0.3f, ore, random, 3, 10);
            Kit.Heap(b, hoist + new Vector3(-1.9f, Ground, Hh + 3.8f), new Vector2(1.6f, 1.3f), 0.7f, 1.1f, Dressed(new Vector3(0.5f, 0.48f, 0.42f), random, 0.3f), random, 3, 9);
        }

        /// <summary>
        /// A tyrant's fitting dummy: the animal's own shape, as big as it lives, in canvas stuffed with straw, standing side on in a cradle of timber, and on it the regalia being
        /// made for the land's tyrant, as far as it's got. Numbers: which tyrant, how long it stands from snout to tail, which way it faces (0 towards +x, 1 towards -x).
        /// </summary>
        public static void Armature(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float length = Math.Clamp(spec.Metres(1), 8f, 28f);
            float turn = spec.Int(2) == 1 ? -MathF.PI * 0.5f : MathF.PI * 0.5f;
            List<SeatBeasts.Tri> tris = SeatBeasts.Tyrant(spec.Int(0), spec.Seed, roar: false);
            VertexMaterial canvas = Canvas(random, 0.92f), seam = Canvas(random, 0.7f), iron = Iron(random, 1.2f), brass = Brass(random), cloth = Oxblood(0.8f), leather = ReichLeather();
            (Vector3 min, Vector3 max) = SeatBeasts.Stand(b, tris, length, Vector3.Zero, tri =>
            {
                if (GearMaterials.IsMetal(tri.Material)) return SeatBeasts.Brassy(tri) ? brass : iron;
                if (tri.Material == GearMaterials.Cloth) return cloth;
                if (tri.Material == GearMaterials.Leather) return leather;
                // Sewn in panels, a darker one here and there where it's been patched.
                return ((int)MathF.Floor(tri.Middle.Z * 1.7f) + (int)MathF.Floor(tri.Middle.Y * 1.3f)) % 7 == 0 ? seam : canvas;
            }, turn);
            // The cradle: bents of timber across it, a beam along each side, and props.
            VertexMaterial wood = Fresh(random, 0.9f);
            float hz = (max.Z - min.Z) * 0.5f + 0.6f, tall = max.Y * 0.62f, reach = length * 0.2f;
            int bents = Math.Max(3, (int)(reach * 2f / 2.8f));
            for (int k = 0; k < bents; k++)
            {
                float x = -reach + 2f * reach * k / (bents - 1);
                foreach (float s in Sides)
                {
                    Kit.Span(b, new Vector3(x - 0.12f, -0.4f, s * hz - 0.12f), new Vector3(x + 0.12f, tall, s * hz + 0.12f), 0.015f, wood);
                    Kit.Plank(b, new Vector3(x, 0f, s * (hz + 1.7f)), new Vector3(x, tall * 0.7f, s * hz), X, 0.16f, 0.11f, wood);
                }
                Kit.Plank(b, new Vector3(x, tall * 0.3f, -hz), new Vector3(x, tall * 0.3f, hz), X, 0.22f, 0.15f, wood);
            }
            foreach (float s in Sides) Kit.Plank(b, new Vector3(-reach - 0.7f, tall - 0.1f, s * hz), new Vector3(reach + 0.7f, tall - 0.1f, s * hz), Z, 0.24f, 0.17f, wood);
            // A ladder up to its back, towards the camera.
            Vector3 foot = new Vector3(reach * 0.5f, 0f, hz + 1.5f), top = new Vector3(reach * 0.5f, tall + 1.6f, hz + 0.1f);
            foreach (float s in Sides) Kit.Pole(b, foot + X * (s * 0.3f), top + X * (s * 0.3f), 0.045f, wood);
            for (float t = 0.08f; t < 1f; t += 0.08f) Kit.Pole(b, Vector3.Lerp(foot, top, t) - X * 0.3f, Vector3.Lerp(foot, top, t) + X * 0.3f, 0.028f, wood);
        }

        /// <summary>
        /// What a foundry is fed and what it leaves. The land's metal, heaped to be melted: bells out of its towers, ploughshares, a gate's leaf, cauldrons, tyres off cart wheels and
        /// a sheaf of its people's spears; or coal; or slag, tipped while it was still bright, and glowing yet in its cracks. Numbers: form (0 the land's metal, 1 coal, 2 slag), how
        /// far across.
        /// </summary>
        public static void ScrapHeap(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            int form = spec.Int(0);
            float size = Math.Clamp(spec.Metres(1), 3f, 9f), rad = size * 0.5f;
            if (form == 1)
            {
                VertexMaterial coal = Flat(new Vector3(0.05f, 0.05f, 0.058f));
                Kit.Heap(b, new Vector3(0f, Ground - 0.05f, 0f), new Vector2(rad, rad * 0.85f), rad * 0.5f, random.Float(), coal, random, 5, 14);
                Kit.Heap(b, new Vector3(rad * 0.5f, Ground - 0.05f, rad * 0.45f), new Vector2(rad * 0.5f), rad * 0.2f, random.Float(), coal, random, 3, 10);
                // A shovel stuck in it and a barrow by it.
                VertexMaterial wood = Timber(random), iron = LandIron(random);
                Kit.Pole(b, new Vector3(-rad * 0.3f, rad * 0.3f, rad * 0.5f), new Vector3(-rad * 0.42f, rad * 0.3f + 1.3f, rad * 0.75f), 0.03f, wood);
                Kit.Span(b, new Vector3(rad + 0.3f, 0.35f, -0.4f), new Vector3(rad + 1.5f, 0.75f, 0.4f), 0.02f, wood);
                Wheel(b, new Vector3(rad + 0.2f, 0.32f, 0f), Z, 0.32f, wood, iron, 6, 0.06f);
                foreach (float s in Sides) Kit.Pole(b, new Vector3(rad + 1.4f, 0.6f, s * 0.36f), new Vector3(rad + 2.4f, 0.75f, s * 0.36f), 0.03f, wood);
                Kit.Heap(b, new Vector3(rad + 0.9f, 0.7f, 0f), new Vector2(0.5f, 0.34f), 0.2f, 0f, coal, random, 2, 8);
                return;
            }
            if (form == 2)
            {
                VertexMaterial slag = Flat(new Vector3(0.15f, 0.13f, 0.12f)), crust = Flat(new Vector3(0.22f, 0.2f, 0.19f));
                Kit.Heap(b, new Vector3(0f, Ground - 0.05f, 0f), new Vector2(rad, rad * 0.8f), rad * 0.5f, random.Float(), slag, random, 5, 14);
                for (int k = 0; k < 5; k++)
                {
                    float a = random.Float() * MathF.Tau, away = rad * random.Range(0.2f, 0.75f);
                    Kit.Heap(b, Dir(a) * away + Y * (Ground + rad * 0.42f * (1f - away / rad)), new Vector2(random.Range(0.5f, 1.1f)), random.Range(0.2f, 0.4f), random.Float(), crust, random, 2, 8);
                }
                // Where it's still bright under its crust.
                VertexMaterial hot = Glow(1f, 0.36f, 0.06f, 0.9f);
                for (int k = 0; k < 7; k++)
                {
                    float a = random.Float() * MathF.Tau, away = rad * random.Range(0.1f, 0.6f), turn = random.Float() * MathF.Tau, run = random.Range(0.5f, 1.3f);
                    Vector3 at = Dir(a) * away + Y * (Ground + rad * 0.5f * (1f - (away / rad) * (away / rad)) + 0.04f);
                    Kit.Plank(b, at - Dir(turn) * run, at + Dir(turn) * run - Y * (run * 0.25f), Y, 0.09f, 0.03f, hot, 0f);
                }
                if (size > 5f) lights.Add(AssetLight.Glow(new Vector3(0f, rad * 0.5f + 0.6f, 0f), new Vector3(1f, 0.4f, 0.1f), 34f, 8f));
                return;
            }
            VertexMaterial rust = LandIron(random), dull = LandIron(random, 0.8f), bronze = new VertexMaterial(MaterialCatalog.Iron.Index, new Colour(0.24f, 0.3f, 0.22f)), shaft = Weathered(random);
            Kit.Heap(b, new Vector3(0f, Ground - 0.05f, 0f), new Vector2(rad, rad * 0.85f), rad * 0.3f, random.Float(), dull, random, 4, 14);
            // Bells, the biggest on top with its headstock still on it.
            int bells = 2 + (size > 5.5f ? 1 : 0);
            for (int k = 0; k < bells; k++)
            {
                float a = k * MathF.Tau / bells + random.Range(-0.5f, 0.5f), away = k == 0 ? 0f : rad * random.Range(0.45f, 0.65f), big = k == 0 ? random.Range(1.5f, 1.9f) : random.Range(0.9f, 1.3f);
                Vector3 at = Dir(a) * away + Y * (Ground + rad * 0.3f * (1f - away / rad) + big * 0.3f);
                Vector3 axis = (Dir(a + random.Range(0.6f, 2f)) + Y * random.Range(0.1f, 0.5f)).Normalized();
                Kit.Lathe(b, at - axis * (big * 0.5f), axis, big, Kit.Profile((0f, big * 0.12f), (0.12f, big * 0.27f), (0.6f, big * 0.38f), (0.9f, big * 0.52f), (1f, big * 0.6f)), 10, 5, bronze, openEnd: true);
                if (k == 0) Kit.Box(b, at - axis * (big * 0.58f), Vector3.Cross(axis, Y).Normalized(), axis, Vector3.Cross(Vector3.Cross(axis, Y), axis).Normalized(), new Vector3(big * 0.75f, 0.12f, 0.14f), 0.02f, shaft);
            }
            // A gate's leaf, thrown on it.
            {
                float a = random.Float() * MathF.Tau;
                Vector3 at = Dir(a) * (rad * 0.55f) + Y * (Ground + rad * 0.2f), along = Dir(a + 1.3f), up = (Dir(a) * 0.8f + Y * 0.55f).Normalized();
                foreach (float s in Sides) Kit.Pole(b, at + along * (s * 1.1f) - up * 0.9f, at + along * (s * 1.1f) + up * 0.9f, 0.035f, rust);
                foreach (float s in Sides) Kit.Pole(b, at - along * 1.1f + up * (s * 0.85f), at + along * 1.1f + up * (s * 0.85f), 0.035f, rust);
                for (int k = -3; k <= 3; k++) Kit.Pole(b, at + along * (k * 0.3f) - up * 0.85f, at + along * (k * 0.3f) + up * (0.85f + (k % 2 == 0 ? 0.25f : 0f)), 0.02f, rust, 0.008f);
            }
            // Ploughshares, cauldrons, tyres, and a sheaf of spears.
            for (int k = 0; k < 4; k++)
            {
                float a = random.Float() * MathF.Tau, away = rad * random.Range(0.3f, 0.9f);
                Vector3 at = Dir(a) * away + Y * (Ground + rad * 0.3f * (1f - away / rad) + 0.2f);
                switch (k)
                {
                    case 0:
                        Kit.Lathe(b, at, (Y + Dir(a) * 0.4f).Normalized(), 0.7f, Kit.Profile((0f, 0.3f), (0.3f, 0.52f), (1f, 0.6f)), 9, 3, dull, openEnd: true);
                        break;
                    case 1:
                        b.Tube(at, at + (Y * 0.3f + Dir(a)).Normalized() * 0.08f, 0.7f, 0.7f, 12, rust, capped: false);
                        b.Tube(at + Dir(a + 1f) * 0.5f + Y * 0.1f, at + Dir(a + 1f) * 0.5f + Y * 0.18f, 0.6f, 0.6f, 12, rust, capped: false);
                        break;
                    case 2:
                    {
                        Vector3 along = Dir(a + 0.8f);
                        Kit.Pole(b, at, at + along * 1.9f + Y * 0.3f, 0.05f, shaft);
                        Kit.Sheet(b, at - along * 0.1f, at + along * 0.75f + Y * 0.05f, at + along * 0.5f - Y * 0.42f + Tan(a + 0.8f) * 0.2f, at - along * 0.05f - Y * 0.28f, rust, dull);
                        break;
                    }
                    default:
                    {
                        Vector3 along = (Dir(a + 1.2f) + Y * 0.25f).Normalized();
                        for (int n = 0; n < 6; n++)
                        {
                            Vector3 off = Tan(a + 1.2f) * ((n - 2.5f) * 0.07f) + Y * (n % 2 * 0.06f);
                            Kit.Pole(b, at + off, at + off + along * 2.3f, 0.022f, shaft);
                            Kit.Lathe(b, at + off + along * 2.3f, along, 0.3f, Kit.Profile((0f, 0.04f), (0.3f, 0.055f), (1f, 0f)), 4, 2, rust);
                        }
                        break;
                    }
                }
            }
        }
    }
}

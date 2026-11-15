using Dino_Engine.Modelling.Model;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using OpenTK.Mathematics;
using static Dino_Engine.Modelling.Procedural.Architecture.Places.PlaceMaterials;

namespace Dino_Engine.Modelling.Procedural.Architecture.Places
{
    /// <summary>
    /// Tents, of canvas that hangs as canvas does (PLACES.md). A tent was once flat quads in one colour, a perfect prism with a black triangle for a door, which a person
    /// said looked like something from RuneScape. Canvas here is one smooth sheet, seen from both sides: it bellies in between the poles, is drawn up into scallops between
    /// the pegs along its hem and creased in fans where the guys pull it, is sewn from cloths whose seams show, doubled along its ridge and hem, bleached on top, splashed
    /// with mud along the bottom, streaked where the rain runs off and patched where it's torn. Its door is two flaps gathered back and tied, and inside is its floor and
    /// what its people sleep on. Cloth lets light through as a sheet does (SUBSURFACE.md), so a tent's inside glows with the sun on its far side: it's never a black hole.
    /// Model +z is a tent's door, y = 0 where people stand. Headless.
    /// </summary>
    internal static class Tents
    {
        /// <summary>What a tent's canvas looks like.</summary>
        public sealed class Look
        {
            /// <summary>Its canvas, as it came off the bolt.</summary>
            public Vector3 Canvas;
            /// <summary>Dyed cloths sewn in alternately with the canvas, if any, a stripe each.</summary>
            public Vector3? Stripe;
            /// <summary>The doubled strip along its ridge.</summary>
            public Vector3 RidgeBand;
            /// <summary>The doubled strip along its hem.</summary>
            public Vector3 HemBand;
            /// <summary>The strip along a wall tent's eaves and its valance, if not its hem's.</summary>
            public Vector3? Eave;
            /// <summary>How old it is, 0 to 1: how faded, stained and patched.</summary>
            public float Wear;
            /// <summary>Weathers paler, as dark canvas does in the sun, rather than darker.</summary>
            public bool Dark;
            /// <summary>What its people sleep under.</summary>
            public Vector3 Blanket;
            public int Seed;

            /// <summary>Undyed canvas of the land's, its age as given.</summary>
            public static Look Land(WorldRandom random, float wear)
            {
                // Deep, as pale canvas in the sun goes white from up here.
                Vector3 canvas = Vector3.Lerp(new Vector3(0.41f, 0.37f, 0.28f), new Vector3(0.37f, 0.35f, 0.29f), random.Float());
                return new Look
                {
                    Canvas = canvas,
                    RidgeBand = canvas * 0.8f,
                    HemBand = Vector3.Lerp(canvas, new Vector3(0.3f, 0.25f, 0.18f), 0.5f),
                    Wear = wear,
                    Blanket = random.Pick(LandBlankets),
                    Seed = random.Int(int.MaxValue),
                };
            }

            /// <summary>The Reich's charcoal, an oxblood strip along its ridge.</summary>
            public static Look Reich(WorldRandom random, float wear)
            {
                Vector3 canvas = new Vector3(0.22f, 0.22f, 0.23f) * random.Range(0.95f, 1.05f);
                return new Look
                {
                    Canvas = canvas,
                    RidgeBand = Arena.ArenaStone.OxbloodColour,
                    HemBand = canvas * 0.82f,
                    Wear = wear,
                    Dark = true,
                    Blanket = new Vector3(0.3f, 0.3f, 0.31f),
                    Seed = random.Int(int.MaxValue),
                };
            }
        }

        // Wool the land dyes: rust, a blue, an olive, undyed. Never the Reich's oxblood.
        private static readonly Vector3[] LandBlankets =
        {
            new Vector3(0.44f, 0.21f, 0.1f), new Vector3(0.17f, 0.23f, 0.32f), new Vector3(0.3f, 0.31f, 0.2f), new Vector3(0.5f, 0.45f, 0.37f),
        };

        // ---------------------------------------------------------------- A ridge tent

        /// <summary>
        /// A ridge tent: canvas over a ridge pole on two uprights, pegged along its hem and guyed out at its sides and ends, its back closed and its door two flaps tied back.
        /// <paramref name="halfLength"/> along z, <paramref name="halfWidth"/> to its hem across x, its ridge <paramref name="high"/> up; guys reach <paramref name="guyReach"/> past the
        /// hem and the ridge's <paramref name="ridgeGuy"/> past each end.
        /// </summary>
        public static void Ridge(MeshBuilder b, WorldRandom random, Look look, float halfLength, float halfWidth, float high, float guyReach, float ridgeGuy, bool reich)
        {
            float L = halfLength, W = halfWidth;
            float H = high + 0.07f;                       // the canvas lies over the ridge pole
            float hem = Ground + 0.05f;
            float slope = new Vector2(W, H - hem).Length;
            Vector3 outRight = new Vector3(H - hem, W, 0f).Normalized();
            VertexMaterial wood = Timber(random, 0.85f), rope = Rope(random);
            int layer = MaterialCatalog.Cloth.Index;

            int pegCount = Math.Max(3, (int)MathF.Round(2f * L / 1.5f) + 1);
            var pegZ = new float[pegCount];
            for (int k = 0; k < pegCount; k++) pegZ[k] = -L + 2f * L * k / (pegCount - 1);
            int cloths = Math.Max(3, (int)MathF.Round(2f * L / 1.3f));
            var seams = new float[cloths - 1];
            for (int k = 1; k < cloths; k++) seams[k - 1] = -L + 2f * L * k / cloths + random.Range(-0.04f, 0.04f);
            float guyV = 0.7f;
            float sag = 0.17f + 0.12f * look.Wear, scallop = 0.2f + 0.12f * look.Wear;
            float ridgeBand = 0.2f / slope, hemBand = 1f - 0.3f / slope;
            var bays = new float[pegCount];
            for (int k = 0; k < pegCount; k++) bays[k] = random.Range(0f, MathF.Tau);

            // Where along the hem, between which pegs.
            (float t, int bay) Bay(float z)
            {
                for (int k = 0; k < pegCount - 1; k++)
                {
                    if (z <= pegZ[k + 1] + 1e-4f) return (Math.Clamp((z - pegZ[k]) / (pegZ[k + 1] - pegZ[k]), 0f, 1f), k);
                }
                return (1f, pegCount - 2);
            }

            Vector3 Slope(float side, float z, float v)
            {
                (float t, int bay) = Bay(z);
                float between = MathF.Pow(MathF.Sin(MathF.PI * t), 2f);
                float ends = Smooth(0f, 0.45f, z + L) * Smooth(0f, 0.45f, L - z);
                Vector3 ridge = new Vector3(0f, H, z);
                Vector3 eave = new Vector3(side * (W - 0.14f * between), hem + scallop * between, z);
                Vector3 p = Vector3.Lerp(ridge, eave, v);
                Vector3 normal = new Vector3(side * outRight.X, outRight.Y, 0f);
                // It bellies in between the ridge and the hem, most where the hem's drawn up between two pegs.
                float belly = sag * MathF.Pow(MathF.Max(MathF.Sin(MathF.PI * v), 0f), 0.8f) * (0.55f + 0.45f * between * v);
                // Creased in fans where each guy pulls, and in soft folds hanging between the pegs.
                float creases = 0f;
                for (int k = 0; k < pegCount; k++) creases += Fan(z - pegZ[k], (guyV - v) * slope, 0.065f, 1.2f, 5f);
                float drape = 0.055f * MathF.Pow(v, 1.5f) * MathF.Sin(MathF.PI * t) * MathF.Cos(MathF.Tau * 1.5f * t + bays[bay]);
                float rumple = 0.03f * (Noise(look.Seed + (side > 0f ? 11 : 23), z * 2.1f, v * slope * 2.1f) - 0.5f);
                float lift = IsSeam(seams, z) ? 0.01f : 0f;
                float shape = (-belly + (creases + drape + rumple) * Smooth(0f, 0.15f, v)) * ends + lift;
                return p + normal * shape;
            }

            var zs = Samples(-L, L, 0.3f, 0.012f, Extras(seams, 0f, 0.05f), pegZ);
            var vs = Samples(0f, 1f, 0.26f / slope, 0.012f / slope, new[] { ridgeBand - 0.025f / slope, ridgeBand + 0.025f / slope, hemBand - 0.025f / slope, hemBand + 0.025f / slope }, new[] { guyV });

            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 Outside(float z, float v)
                {
                    Vector3 c = Cloth(look, seams, z, look.Seed + (side > 0f ? 0 : 5));
                    if (v < ridgeBand) c = look.RidgeBand;
                    else if (v > hemBand) c = look.HemBand;
                    c = Weather(look, c, z * 1.3f + side * 7f, v * slope, v);
                    if (IsSeam(seams, z)) c *= 0.78f;
                    return c;
                }
                Sheet(b, zs, vs, (z, v) => Slope(side, z, v), Outside, new Vector3(side, 1f, 0f), layer);
                Patches(b, random, look, (z, v) => Slope(side, z, v) + new Vector3(side * outRight.X, outRight.Y, 0f) * 0.014f, -L + 0.4f, L - 0.4f, 0.25f, 0.85f, slope, new Vector3(side, 1f, 0f), layer);

                // Guys out to their pegs, and a peg through the hem's loop at each.
                for (int k = 0; k < pegCount; k++)
                {
                    float z = pegZ[k];
                    float end = k == 0 ? -1f : k == pegCount - 1 ? 1f : 0f;
                    Vector3 at = Slope(side, z, guyV) + new Vector3(side * outRight.X, outRight.Y, 0f) * 0.02f;
                    Vector3 peg = new Vector3(side * (W + guyReach), Ground, z + end * guyReach * 0.45f);
                    Kit.Rope(b, at, peg + Vector3.UnitY * 0.26f, 0.025f, 0.022f, rope, 3);
                    Peg(b, peg, new Vector3(side, 0f, end * 0.45f), 0.045f, wood);
                    Peg(b, new Vector3(side * (W + 0.05f), Ground, z), new Vector3(side, 0f, 0f), 0.03f, wood, 0.55f);
                }
            }

            // The back closed, bellied in a little and drawn up along its hem.
            {
                Vector3 apex = new Vector3(0f, H, -L);
                Vector3 Point(float x, float s) => Gable(apex, W, hem, -L, x, s, -1f, scallop * 0.8f, look.Seed + 3);
                var xs = Samples(-1f, 1f, 0.28f / W, 0.01f / W, new[] { -0.04f / W, 0f, 0.04f / W }, Array.Empty<float>());
                var ss = Samples(0.03f, 1f, 0.28f / slope, 0.01f / slope, new[] { hemBand - 0.025f / slope, hemBand + 0.025f / slope }, Array.Empty<float>());
                Vector3 Out(float x, float s)
                {
                    Vector3 c = s > hemBand ? look.HemBand : Cloth(look, Array.Empty<float>(), x * 3f, look.Seed + 9);
                    c = Weather(look, c, x * W * 1.3f + 31f, s * slope, s);
                    return MathF.Abs(x) * W < 0.012f ? c * 0.78f : c;
                }
                Sheet(b, xs, ss, Point, Out, -Vector3.UnitZ, layer);
            }

            // The door: two flaps gathered back to its sides and tied, one further open than the other.
            float openRight = random.Range(0.75f, 0.95f), openLeft = random.Range(0.35f, 0.7f);
            if (random.Chance(0.5f)) (openRight, openLeft) = (openLeft, openRight);
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 apex = new Vector3(0f, H, L), foot = new Vector3(side * W, hem, L), middle = new Vector3(0f, hem, L);
                float open = side > 0f ? openRight : openLeft, tie = random.Range(0.5f, 0.62f);
                Vector3 Point(float t, float a) => Flap(apex, foot, apex, middle, t, a, open, tie, Vector3.UnitZ, look.Seed + (side > 0f ? 41 : 43));
                var ts = Samples(0f, 1f, 0.1f, 0.005f, Array.Empty<float>(), Array.Empty<float>());
                var aas = Samples(0.03f, 1f, 0.24f / slope, 0.01f / slope, new[] { hemBand - 0.02f / slope, hemBand + 0.02f / slope }, Array.Empty<float>());
                Vector3 Out(float t, float a) => Weather(look, a > hemBand ? look.HemBand : look.Canvas * 0.97f, t * 2f + side * 13f, a * slope, a);
                Sheet(b, ts, aas, Point, Out, new Vector3(0f, 0f, 1f), layer);
                // Its tie, round the gathered cloth.
                Vector3 hinge = Vector3.Lerp(apex, foot, tie), bunch = Point(1f, tie);
                Kit.Rope(b, hinge + new Vector3(0f, 0.02f, 0.03f), bunch + new Vector3(side * 0.04f, 0.02f, 0.08f), 0.03f, 0.028f, rope, 3);
            }

            // The poles: an upright at each end with a pin through the canvas, and the ridge between them.
            foreach (float z in new[] { -L, L })
            {
                float at = z - MathF.Sign(z) * 0.06f;
                Kit.Pole(b, new Vector3(0f, -0.3f, at), new Vector3(0f, high, at), 0.075f, wood);
                Kit.Pole(b, new Vector3(0f, high, z), new Vector3(0f, H + 0.3f, z), 0.028f, wood);
                // The ridge's guy, out from the pin to a peg past the end.
                Vector3 peg = new Vector3(0f, Ground, z + MathF.Sign(z) * ridgeGuy);
                Kit.Rope(b, new Vector3(0f, H + 0.24f, z), peg + Vector3.UnitY * 0.26f, 0.03f, 0.022f, rope, 4);
                Peg(b, peg, new Vector3(0f, 0f, MathF.Sign(z)), 0.045f, wood);
            }
            Kit.Pole(b, new Vector3(0f, high, -L - 0.1f), new Vector3(0f, high, L + 0.1f), 0.07f, wood);

            Furnish(b, random, look, W, L, reich);
        }

        // What's inside a ridge tent, seen through its door: a floor of oiled cloth, a bedroll and a blanket kicked back, a sack or a box.
        private static void Furnish(MeshBuilder b, WorldRandom random, Look look, float W, float L, bool reich)
        {
            int layer = MaterialCatalog.Cloth.Index;
            Vector3 floor = reich ? new Vector3(0.16f, 0.16f, 0.17f) : new Vector3(0.24f, 0.2f, 0.14f);
            float x0 = -W * 0.8f, x1 = W * 0.8f, z0 = -L + 0.2f, z1 = L - 0.25f;
            int seed = look.Seed + 77;
            Sheet(b, Samples(0f, 1f, 0.2f, 0.01f, Array.Empty<float>(), Array.Empty<float>()), Samples(0f, 1f, 0.2f, 0.01f, Array.Empty<float>(), Array.Empty<float>()),
                (u, v) => new Vector3(MathHelper.Lerp(x0, x1, u), Ground + 0.03f + 0.02f * Noise(seed, u * 4f, v * 4f), MathHelper.Lerp(z0, z1, v)),
                (u, v) => floor * (0.85f + 0.3f * Noise(seed + 1, u * 3f, v * 3f)), Vector3.UnitY, layer);

            // A bedroll along one side, a blanket thrown over bedding along the other.
            float side = random.Chance(0.5f) ? 1f : -1f;
            VertexMaterial wool = new VertexMaterial(layer, new Colour(look.Blanket));
            Kit.Lathe(b, new Vector3(side * W * 0.45f, Ground + 0.17f, z0 + 0.25f), Vector3.UnitZ, MathF.Min(1.8f, z1 - z0 - 0.6f),
                Kit.Profile((0f, 0.12f), (0.06f, 0.17f), (0.5f, 0.15f), (0.94f, 0.17f), (1f, 0.12f)), 8, 4, wool);
            Vector3 other = Vector3.Lerp(look.Blanket, new Vector3(0.5f, 0.45f, 0.37f), 0.5f);
            float bx = -side * W * 0.45f, bw = W * 0.28f, bz0 = z0 + 0.2f, bz1 = MathF.Min(z1 - 0.3f, bz0 + 2.1f);
            Sheet(b, Samples(0f, 1f, 0.15f, 0.01f, Array.Empty<float>(), Array.Empty<float>()), Samples(0f, 1f, 0.12f, 0.01f, Array.Empty<float>(), Array.Empty<float>()),
                (u, v) =>
                {
                    float across = (u - 0.5f) * 2f;
                    float lump = 0.2f * MathF.Max(0f, 1f - across * across) * Smooth(0f, 0.15f, v) * (1f - 0.65f * Smooth(0.6f, 0.85f, v));
                    float folds = 0.03f * MathF.Sin(v * 17f + u * 3f) * (1f - MathF.Abs(across));
                    return new Vector3(bx + across * bw * (1f + 0.15f * v), Ground + 0.06f + lump + folds, MathHelper.Lerp(bz0, bz1, v));
                },
                (u, v) => other * (0.9f + 0.2f * Noise(seed + 3, u * 5f, v * 6f)) * (MathF.Abs(MathF.Sin(v * 9f)) < 0.12f ? 0.8f : 1f), Vector3.UnitY, layer);

            // Something kept at the back.
            Vector3 back = new Vector3(random.Range(-0.4f, 0.4f) * W, Ground, -L + 0.75f);
            if (reich) Kit.Crate(b, back + Vector3.UnitY * 0.36f, random.Range(-0.3f, 0.3f), new Vector3(0.48f, 0.36f, 0.36f), Timber(random, 0.7f), Iron(random), random, random.Chance(0.6f));
            else Kit.Sack(b, back, random.Float() * MathF.Tau, random.Chance(0.3f), 0.48f, Canvas(random, 0.85f));
        }

        // ---------------------------------------------------------------- A wall tent

        /// <summary>
        /// A wall tent: upright walls under a pitched roof that overhangs them, its eaves guyed out to pegs, its gables closed and its front wall two flaps tied back to its corners.
        /// <paramref name="hx"/> and <paramref name="hz"/> are half its walls' width and length, <paramref name="wall"/> their height and <paramref name="ridge"/> the ridge's. The
        /// eaves can end in a valance, scalloped if <paramref name="scalloped"/>. Without <paramref name="frontGuy"/> nothing holds its ridge out at the front, as a fly there does.
        /// </summary>
        public static void Wall(MeshBuilder b, WorldRandom random, Look look, float hx, float hz, float wall, float ridge, float guyReach, bool scalloped, bool valance, bool frontGuy = true)
        {
            int layer = MaterialCatalog.Cloth.Index;
            VertexMaterial wood = Timber(random, 0.7f), rope = Rope(random);
            float over = 0.32f;
            float pitch = (ridge - wall) / hx;
            float eaveY = wall - pitch * over;
            float hem = Ground + 0.04f;
            float roofSlope = new Vector2(hx + over, ridge - eaveY).Length;
            float wallHigh = wall - hem;
            int guyCount = Math.Max(3, (int)MathF.Round(2f * hz / 1.6f) + 1);
            var guyZ = new float[guyCount];
            for (int k = 0; k < guyCount; k++) guyZ[k] = -hz + 2f * hz * k / (guyCount - 1);
            int cloths = Math.Max(3, (int)MathF.Round(2f * hz / 1.3f));
            var seams = new float[cloths - 1];
            for (int k = 1; k < cloths; k++) seams[k - 1] = -hz + 2f * hz * k / cloths + random.Range(-0.03f, 0.03f);
            float ridgeBand = 0.2f / roofSlope, eaveBand = 1f - 0.26f / roofSlope;
            float hemBand = 1f - 0.32f / wallHigh;

            (float t, int bay) Bay(float[] at, float z)
            {
                for (int k = 0; k < at.Length - 1; k++)
                {
                    if (z <= at[k + 1] + 1e-4f) return (Math.Clamp((z - at[k]) / (at[k + 1] - at[k]), 0f, 1f), k);
                }
                return (1f, at.Length - 2);
            }

            // The roof: from the ridge out over the walls to its eaves, which dip between the guys that hold them out.
            Vector3 roofOut = new Vector3(ridge - eaveY, hx + over, 0f).Normalized();
            Vector3 Roof(float side, float z, float v)
            {
                (float t, _) = Bay(guyZ, z);
                float between = MathF.Pow(MathF.Sin(MathF.PI * t), 2f);
                float ends = Smooth(0f, 0.4f, z + hz) * Smooth(0f, 0.4f, hz - z);
                Vector3 p = Vector3.Lerp(new Vector3(0f, ridge, z), new Vector3(side * (hx + over), eaveY - 0.1f * between, z), v);
                Vector3 normal = new Vector3(side * roofOut.X, roofOut.Y, 0f);
                // Under the roof its walls hold it up from x = hx in, so it bellies only between the ridge and there.
                float overWall = Math.Clamp(v * (hx + over) / hx, 0f, 1f);
                float belly = (0.13f + 0.07f * look.Wear) * MathF.Pow(MathF.Max(MathF.Sin(MathF.PI * overWall), 0f), 0.9f);
                float creases = 0f;
                for (int k = 0; k < guyCount; k++) creases += Fan(z - guyZ[k], (1f - v) * roofSlope, 0.055f, 1f, 5f);
                float rumple = 0.026f * (Noise(look.Seed + (side > 0f ? 51 : 53), z * 2f, v * roofSlope * 2f) - 0.5f);
                float lift = IsSeam(seams, z) ? 0.01f : 0f;
                return p + normal * ((-belly + (creases + rumple) * Smooth(0f, 0.15f, v)) * ends + lift);
            }
            var zs = Samples(-hz, hz, 0.3f, 0.012f, Extras(seams, 0f, 0.05f), guyZ);
            var vs = Samples(0f, 1f, 0.28f / roofSlope, 0.012f / roofSlope, new[] { ridgeBand - 0.025f / roofSlope, ridgeBand + 0.025f / roofSlope, eaveBand - 0.025f / roofSlope, eaveBand + 0.025f / roofSlope, hx / (hx + over) }, Array.Empty<float>());
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 Out(float z, float v)
                {
                    Vector3 c = v < ridgeBand ? look.RidgeBand : v > eaveBand ? look.Eave ?? look.HemBand : Cloth(look, seams, z, look.Seed + (side > 0f ? 60 : 61));
                    c = Weather(look, c, z * 1.3f + side * 9f, v * roofSlope, v * 0.6f);
                    return IsSeam(seams, z) ? c * 0.78f : c;
                }
                Sheet(b, zs, vs, (z, v) => Roof(side, z, v), Out, new Vector3(side, 1f, 0f), layer);
                Patches(b, random, look, (z, v) => Roof(side, z, v) + new Vector3(side * roofOut.X, roofOut.Y, 0f) * 0.014f, -hz + 0.4f, hz - 0.4f, 0.2f, 0.75f, roofSlope, new Vector3(side, 1f, 0f), layer);

                // The valance hanging from the eave, its lower edge scalloped or straight.
                if (valance)
                {
                    int lobes = Math.Max(4, (int)MathF.Round(2f * hz / 0.62f));
                    var vzs = Samples(-hz, hz, 2f * hz / (lobes * (scalloped ? 4f : 1f)), 0.01f, Array.Empty<float>(), Array.Empty<float>());
                    Vector3 Hang(float z, float v)
                    {
                        Vector3 top = Roof(side, z, 1f);
                        float drop = 0.2f + (scalloped ? 0.13f * MathF.Abs(MathF.Sin(MathF.PI * (z + hz) / (2f * hz) * lobes)) : 0.08f);
                        return top + new Vector3(side * 0.02f * v, -drop * v, 0f);
                    }
                    Vector3 Band(float z, float v) => Weather(look, look.Stripe.HasValue && scalloped ? look.Stripe.Value : look.Eave ?? look.HemBand, z * 1.3f, 0f, 0.5f);
                    Sheet(b, vzs, new[] { 0f, 0.5f, 1f }, Hang, Band, new Vector3(side, 0f, 0f), layer);
                }

                // The walls, bellied in a little between the corners and drawn up between the pegs along their hem.
                var wzs = Samples(-hz, hz, 0.34f, 0.012f, Extras(seams, 0f, 0.05f), guyZ);
                var wvs = Samples(0f, 1f, 0.32f / wallHigh, 0.012f / wallHigh, new[] { hemBand - 0.025f / wallHigh, hemBand + 0.025f / wallHigh }, Array.Empty<float>());
                Vector3 WallAt(float z, float v)
                {
                    (float t, _) = Bay(guyZ, z);
                    float ends = Smooth(0f, 0.4f, z + hz) * Smooth(0f, 0.4f, hz - z);
                    float between = MathF.Pow(MathF.Sin(MathF.PI * t), 2f);
                    float top = Roof(side, z, hx / (hx + over)).Y - 0.02f;
                    Vector3 p = new Vector3(side * hx, MathHelper.Lerp(top, hem + 0.1f * between * look.Wear, v), z);
                    float belly = 0.09f * MathF.Sin(MathF.PI * v) * ends + 0.04f * between * v * v;
                    float folds = 0.035f * MathF.Sin(MathF.Tau * 2f * t + side) * v * v * ends;
                    return p + new Vector3(side, 0f, 0f) * (-belly + folds + (IsSeam(seams, z) ? 0.008f : 0f));
                }
                Vector3 WallOut(float z, float v)
                {
                    Vector3 c = v > hemBand ? look.HemBand : Cloth(look, seams, z, look.Seed + (side > 0f ? 70 : 71));
                    c = Weather(look, c, z * 1.3f + side * 19f, v * wallHigh, v);
                    return IsSeam(seams, z) ? c * 0.78f : c;
                }
                Sheet(b, wzs, wvs, WallAt, WallOut, new Vector3(side, 0f, 0f), layer);

                // Guys out from the eaves, and pegs through the walls' hem.
                foreach (float z in guyZ)
                {
                    Vector3 eave = Roof(side, z, 1f);
                    Vector3 peg = new Vector3(side * (hx + over + guyReach), Ground, z + (MathF.Abs(z) > hz - 0.01f ? MathF.Sign(z) * guyReach * 0.4f : 0f));
                    Kit.Rope(b, eave, peg + Vector3.UnitY * 0.26f, 0.03f, 0.022f, rope, 3);
                    Peg(b, peg, new Vector3(side, 0f, 0f), 0.045f, wood);
                    Peg(b, new Vector3(side * (hx + 0.06f), Ground, z), new Vector3(side, 0f, 0f), 0.03f, wood, 0.55f);
                }
            }

            // The gables, and the back wall under its own.
            Vector3 GableOut(float x, float s, float z) => Weather(look, Cloth(look, Array.Empty<float>(), x * 3f, look.Seed + 80), x * hx * 1.3f + z * 5f, s, s * 0.5f);
            foreach (float z in new[] { -hz, hz })
            {
                float face = MathF.Sign(z);
                Vector3 apex = new Vector3(0f, ridge, z);
                var gxs = Samples(-1f, 1f, 0.3f / hx, 0.01f / hx, Array.Empty<float>(), Array.Empty<float>());
                var gss = Samples(0.03f, 1f, 0.3f / (ridge - wall), 0.01f, Array.Empty<float>(), Array.Empty<float>());
                Vector3 Point(float x, float s)
                {
                    Vector3 foot = new Vector3(x * hx, Roof(x < 0f ? -1f : 1f, z, hx / (hx + over)).Y - 0.02f, z);
                    Vector3 p = Vector3.Lerp(apex, foot, s);
                    return p - new Vector3(0f, 0f, face) * 0.04f * MathF.Sin(MathF.PI * (x * 0.5f + 0.5f)) * MathF.Sin(MathF.PI * s);
                }
                Sheet(b, gxs, gss, Point, (x, s) => GableOut(x, s, z), new Vector3(0f, 0f, face), layer);
            }
            {
                var bxs = Samples(-hx, hx, 0.34f, 0.01f, new[] { -0.04f, 0f, 0.04f }, Array.Empty<float>());
                var bvs = Samples(0f, 1f, 0.32f / wallHigh, 0.012f / wallHigh, new[] { hemBand - 0.025f / wallHigh, hemBand + 0.025f / wallHigh }, Array.Empty<float>());
                Vector3 Back(float x, float v)
                {
                    float top = Roof(x < 0f ? -1f : 1f, -hz, hx / (hx + over)).Y - 0.02f;
                    float across = Smooth(0f, 0.4f, x + hx) * Smooth(0f, 0.4f, hx - x);
                    return new Vector3(x, MathHelper.Lerp(top, hem, v), -hz) + new Vector3(0f, 0f, 1f) * 0.05f * MathF.Sin(MathF.PI * v) * across;
                }
                Vector3 BackOut(float x, float v) => Weather(look, v > hemBand ? look.HemBand : Cloth(look, Array.Empty<float>(), x * 2f, look.Seed + 82), x * 1.3f + 41f, v * wallHigh, v) * (MathF.Abs(x) < 0.012f ? 0.78f : 1f);
                Sheet(b, bxs, bvs, Back, BackOut, -Vector3.UnitZ, layer);
            }

            // The front wall: two flaps tied back to its corners.
            foreach (float side in new[] { -1f, 1f })
            {
                float top = Roof(side, hz, hx / (hx + over)).Y - 0.02f;
                // The flap's top runs along the gable's foot from the corner to the middle, at the wall's height there.
                Vector3 hingeTop = new Vector3(side * hx, top, hz), hingeFoot = new Vector3(side * hx, hem, hz);
                Vector3 freeTop = new Vector3(0f, top, hz), freeFoot = new Vector3(0f, hem, hz);
                float open = random.Range(0.7f, 0.95f), tie = random.Range(0.45f, 0.6f);
                var ts = Samples(0f, 1f, 0.1f, 0.005f, Array.Empty<float>(), Array.Empty<float>());
                var aas = Samples(0f, 1f, 0.24f / wallHigh, 0.01f / wallHigh, new[] { hemBand - 0.025f / wallHigh, hemBand + 0.025f / wallHigh }, Array.Empty<float>());
                Vector3 Point(float t, float a) => Flap(hingeTop, hingeFoot, freeTop, freeFoot, t, a, open, tie, Vector3.UnitZ, look.Seed + (side > 0f ? 90 : 91));
                Vector3 Out(float t, float a) => Weather(look, a > hemBand ? look.HemBand : look.Canvas * 0.97f, t * 2f + side * 23f, a * wallHigh, a);
                Sheet(b, ts, aas, Point, Out, Vector3.UnitZ, layer);
                Vector3 bunch = Point(1f, tie);
                Kit.Rope(b, Vector3.Lerp(hingeTop, hingeFoot, tie) + new Vector3(side * 0.03f, 0f, 0.03f), bunch + new Vector3(0f, 0.02f, 0.08f), 0.03f, 0.028f, rope, 3);
            }

            // The poles: an upright at each end under the ridge, the ridge between them, and a pin up through the canvas with the ends' guys.
            foreach (float z in new[] { -hz, hz })
            {
                float s = MathF.Sign(z), at = z - s * 0.08f;
                Kit.Pole(b, new Vector3(0f, -0.3f, at), new Vector3(0f, ridge - 0.07f, at), 0.08f, wood);
                Kit.Pole(b, new Vector3(0f, ridge - 0.07f, z), new Vector3(0f, ridge + 0.3f, z), 0.03f, wood);
                if (s > 0f && !frontGuy) continue;
                Vector3 peg = new Vector3(0f, Ground, z + s * (guyReach + 0.3f));
                Kit.Rope(b, new Vector3(0f, ridge + 0.24f, z), peg + Vector3.UnitY * 0.26f, 0.04f, 0.022f, rope, 4);
                Peg(b, peg, new Vector3(0f, 0f, s), 0.045f, wood);
            }
            Kit.Pole(b, new Vector3(0f, ridge - 0.07f, -hz - 0.1f), new Vector3(0f, ridge - 0.07f, hz + 0.1f), 0.075f, wood);

            // Its floor, seen through the door.
            int seed = look.Seed + 95;
            Vector3 floor = look.Dark ? new Vector3(0.16f, 0.16f, 0.17f) : new Vector3(0.24f, 0.2f, 0.14f);
            Sheet(b, Samples(0f, 1f, 0.2f, 0.01f, Array.Empty<float>(), Array.Empty<float>()), Samples(0f, 1f, 0.2f, 0.01f, Array.Empty<float>(), Array.Empty<float>()),
                (u, v) => new Vector3(MathHelper.Lerp(-hx + 0.15f, hx - 0.15f, u), Ground + 0.03f + 0.015f * Noise(seed, u * 4f, v * 4f), MathHelper.Lerp(-hz + 0.15f, hz - 0.15f, v)),
                (u, v) => floor * (0.85f + 0.3f * Noise(seed + 1, u * 3f, v * 3f)), Vector3.UnitY, layer);
        }

        // ---------------------------------------------------------------- Canvas

        /// <summary>
        /// One smooth sheet of canvas over a grid of samples: <paramref name="at"/> gives each point and <paramref name="paint"/> its colour there, its outside facing
        /// <paramref name="outward"/> at its middle. It's one face: what's built is drawn from both sides, its normal turned to whoever looks (Model.frag), and a second face lying on it
        /// is drawn at exactly the same depth, so whichever was drawn last shows from both sides and the other never does. Its texture runs along the cloth in metres, so the weave
        /// is the same size wherever the sheet is drawn out.
        /// </summary>
        public static void Sheet(MeshBuilder b, IReadOnlyList<float> us, IReadOnlyList<float> vs, Func<float, float, Vector3> at, Func<float, float, Vector3> paint, Vector3 outward, int layer)
        {
            int nu = us.Count, nv = vs.Count;
            if (nu < 2 || nv < 2) return;
            var points = new Vector3[nv, nu];
            for (int j = 0; j < nv; j++) for (int i = 0; i < nu; i++) points[j, i] = at(us[i], vs[j]);
            float tiles = MaterialCatalog.TilesPerMetre(layer);
            var uv = new Vector2[nv, nu];
            for (int j = 0; j < nv; j++) for (int i = 1; i < nu; i++) uv[j, i].X = uv[j, i - 1].X + (points[j, i] - points[j, i - 1]).Length;
            for (int i = 0; i < nu; i++) for (int j = 1; j < nv; j++) uv[j, i].Y = uv[j - 1, i].Y + (points[j, i] - points[j - 1, i]).Length;

            // Wound so the outside faces out: a triangle (p00, p10, p11) faces along du x dv.
            int mi = nu / 2, mj = nv / 2;
            Vector3 du = points[mj, Math.Min(mi + 1, nu - 1)] - points[mj, Math.Max(mi - 1, 0)];
            Vector3 dv = points[Math.Min(mj + 1, nv - 1), mi] - points[Math.Max(mj - 1, 0), mi];
            bool flip = Vector3.Dot(Vector3.Cross(du, dv), outward) < 0f;

            // How the cloth curves at each point, seen from outside: positive in a hollow, negative on a fold's crest. From 33 m up, under cloud, a fold's normals
            // hardly show, so a hollow is a little darker and a crest a little lighter in the colour itself, as the light that reaches into a crease is less.
            var bend = new float[nv, nu];
            for (int j = 0; j < nv; j++)
            {
                for (int i = 0; i < nu; i++)
                {
                    Vector3 a = points[j, Math.Max(i - 1, 0)], c = points[j, Math.Min(i + 1, nu - 1)];
                    Vector3 d = points[Math.Max(j - 1, 0), i], e = points[Math.Min(j + 1, nv - 1), i];
                    Vector3 normal = Vector3.Cross(c - a, e - d);
                    if (normal.LengthSquared < 1e-12f) continue;
                    normal = normal.Normalized() * (flip ? -1f : 1f);
                    float curve = 0f;
                    if (i > 0 && i < nu - 1) curve += Curvature(a, points[j, i], c, normal);
                    if (j > 0 && j < nv - 1) curve += Curvature(d, points[j, i], e, normal);
                    bend[j, i] = curve;
                }
            }

            {
                var vertices = new List<Vertex>(nu * nv);
                var indices = new List<int>((nu - 1) * (nv - 1) * 6);
                for (int j = 0; j < nv; j++)
                {
                    for (int i = 0; i < nu; i++)
                    {
                        float shade = Math.Clamp(1f - 0.035f * bend[j, i], 0.68f, 1.12f);
                        Vector3 c = Vector3.Clamp(paint(us[i], vs[j]) * shade, Vector3.Zero, Vector3.One);
                        vertices.Add(new Vertex(points[j, i], new VertexMaterial(layer, new Colour(c)), uv[j, i] * tiles));
                    }
                }
                bool reverse = flip;
                void Triangle(int a, int c, int d)
                {
                    Vector3 pa = vertices[a].position, pc = vertices[c].position, pd = vertices[d].position;
                    if (Vector3.Cross(pc - pa, pd - pa).LengthSquared < 1e-12f) return;
                    indices.Add(a);
                    indices.Add(reverse ? d : c);
                    indices.Add(reverse ? c : d);
                }
                for (int j = 0; j < nv - 1; j++)
                {
                    for (int i = 0; i < nu - 1; i++)
                    {
                        int a = j * nu + i;
                        Triangle(a, a + 1, a + nu + 1);
                        Triangle(a, a + nu + 1, a + nu);
                    }
                }
                b.Add(new Mesh(vertices, indices), Vector3.Zero, Vector3.One);
            }
        }

        /// <summary>
        /// A flap of canvas hung from <paramref name="hingeTop"/> to <paramref name="hingeFoot"/>, its free edge from <paramref name="freeTop"/> to <paramref name="freeFoot"/>, gathered back
        /// towards its hinge by <paramref name="open"/> and tied there <paramref name="tie"/> of the way down, bunched out towards <paramref name="outward"/> in pleats. t runs from hinge to free
        /// edge and a from top to foot.
        /// </summary>
        public static Vector3 Flap(Vector3 hingeTop, Vector3 hingeFoot, Vector3 freeTop, Vector3 freeFoot, float t, float a, float open, float tie, Vector3 outward, int seed)
        {
            Vector3 hinge = Vector3.Lerp(hingeTop, hingeFoot, a), free = Vector3.Lerp(freeTop, freeFoot, a);
            // How much of it is drawn in: rising to the tie, and spreading again a little below it as the bunch hangs.
            float pull = a <= tie ? Smooth(0f, tie, a) * (0.35f + 0.65f * Smooth(0f, tie, a)) : 1f - 0.4f * (a - tie) / MathF.Max(1f - tie, 1e-3f);
            pull = MathF.Max(pull, a <= tie ? 0.25f : 0f);
            float width = 1f - open * 0.88f * pull;
            Vector3 p = Vector3.Lerp(hinge, free, t * width);
            float gathered = open * pull * t;
            // Bunched out where it's gathered, in pleats, and hanging a little forward below its tie.
            float pleats = MathF.Sin(t * MathF.PI * 5.5f + a * 1.7f + (seed & 7)) * 0.07f * gathered;
            float bunch = 0.22f * gathered * (1f - 0.5f * t);
            float hang = a > tie ? 0.08f * open * (a - tie) * t : 0f;
            // Even shut, it hangs in soft folds.
            float soft = 0.025f * MathF.Sin(t * MathF.PI * 3f) * a;
            return p + outward * (pleats + bunch + hang + soft);
        }

        /// <summary>A peg driven in at <paramref name="at"/>, leaning back from the tent along <paramref name="away"/>, as a peg holding a line does.</summary>
        public static void Peg(MeshBuilder b, Vector3 at, Vector3 away, float radius, VertexMaterial wood, float size = 1f)
        {
            Vector3 lean = away.LengthSquared > 1e-6f ? new Vector3(away.X, 0f, away.Z).Normalized() : Vector3.Zero;
            Kit.Pole(b, at - Vector3.UnitY * 0.3f * size + lean * 0.05f * size, at + Vector3.UnitY * 0.34f * size - lean * 0.13f * size, radius, wood, radius * 0.7f);
        }

        // A few patches sewn over tears, of another cloth, on a sheet given by `at` (lifted off it already), between z0 and z1 and v0 and v1.
        private static void Patches(MeshBuilder b, WorldRandom random, Look look, Func<float, float, Vector3> at, float z0, float z1, float v0, float v1, float length, Vector3 outward, int layer)
        {
            int count = random.Chance(look.Wear * (look.Dark ? 0.5f : 1f)) ? random.Int(1, look.Wear > 0.6f && !look.Dark ? 4 : 2) : 0;
            for (int k = 0; k < count; k++)
            {
                float z = random.Range(z0, z1), v = random.Range(v0, v1);
                float hz = random.Range(0.22f, 0.45f), hv = random.Range(0.2f, 0.38f) / length;
                // Newer cloth over the tear: paler canvas, or a browner bolt; on dark canvas, darker cloth, as anything paler shows from up here as a white square.
                Vector3 cloth = look.Dark ? look.Canvas * random.Range(0.78f, 0.9f)
                    : random.Chance(0.5f) ? look.Canvas * random.Range(1.08f, 1.2f) : Vector3.Lerp(look.Canvas, new Vector3(0.36f, 0.3f, 0.22f), random.Range(0.3f, 0.6f));
                var zs = new[] { z - hz, z - hz * 0.33f, z + hz * 0.33f, z + hz };
                var vs = new[] { v - hv, v - hv * 0.33f, v + hv * 0.33f, v + hv };
                int seed = random.Int(1 << 20);
                Sheet(b, zs, vs, at, (u, w) => Weather(look, cloth, u * 1.3f + seed, w * length, w * 0.7f), outward, layer);
            }
        }

        // ---------------------------------------------------------------- Colour

        // The canvas of whichever cloth z is in: each bolt weathered a little differently, and every other one dyed if the tent is striped.
        private static Vector3 Cloth(Look look, float[] seams, float z, int seed)
        {
            int cloth = 0;
            foreach (float s in seams) if (z > s + 0.001f) cloth++;
            Vector3 c = look.Stripe.HasValue && cloth % 2 == 1 ? look.Stripe.Value : look.Canvas;
            return c * (0.9f + 0.2f * Hash(seed, cloth, 0));
        }

        // What weather does to canvas: faded towards the top, streaked where rain runs down, blotched, and splashed with mud along the bottom. x is metres
        // across the cloth, y metres down it and down how far down, 0 to 1.
        private static Vector3 Weather(Look look, Vector3 c, float x, float y, float down)
        {
            float wear = look.Wear;
            // The sun has had the top longest: undyed canvas bleaches, and dark canvas goes a dusty brown-grey.
            float faded = (1f - down) * (1f - down);
            c = look.Dark ? Toward(c, new Vector3(0.36f, 0.34f, 0.3f), faded * (0.35f + 0.35f * wear)) : Vector3.Lerp(c, c * 1.1f + new Vector3(0.02f), faded * (0.12f + 0.18f * wear));
            // Rain runs down it in streaks, darker on canvas and leaving a pale tide on dark cloth.
            float streak = Smooth(0.5f, 0.82f, Noise(look.Seed + 101, x * 2.6f, y * 0.25f)) * down * (0.5f + wear);
            c = look.Dark ? Toward(c, new Vector3(0.38f, 0.37f, 0.35f), streak * 0.35f) : c * (1f - 0.26f * streak);
            // Stains and mildew in patches.
            float blotch = Smooth(0.55f, 0.8f, Noise(look.Seed + 103, x * 0.7f, y * 0.9f)) * (0.3f + 0.7f * wear);
            c = look.Dark ? Toward(c, new Vector3(0.3f, 0.28f, 0.25f), blotch * 0.75f) : Vector3.Lerp(c, c * new Vector3(0.6f, 0.62f, 0.5f), blotch * 0.8f);
            // Fine mottling, so no stretch of it is one flat colour.
            c *= 0.88f + 0.24f * Noise(look.Seed + 109, x * 2.4f, y * 2.4f);
            // Mud thrown up along the bottom.
            float splash = MathF.Pow(Smooth(0.6f, 1f, down), 1.5f) * (0.5f + 0.5f * Noise(look.Seed + 107, x * 3f, y * 3f));
            c = Toward(c, look.Dark ? new Vector3(0.34f, 0.29f, 0.22f) : new Vector3(0.22f, 0.17f, 0.11f), splash * (0.45f + 0.4f * wear));
            return c;
        }

        // Towards another colour by t, but a dyed colour only towards its brightness, keeping its own hue: oxblood that fades goes a duller red, never pink.
        private static Vector3 Toward(Vector3 c, Vector3 target, float t)
        {
            float most = MathF.Max(c.X, MathF.Max(c.Y, c.Z)), least = MathF.Min(c.X, MathF.Min(c.Y, c.Z));
            float dyed = most > 1e-4f ? Math.Clamp((most - least) / most * 1.6f - 0.3f, 0f, 1f) : 0f;
            float luma(Vector3 v) => v.X * 0.3f + v.Y * 0.59f + v.Z * 0.11f;
            Vector3 keepingHue = c * Math.Clamp(luma(target) / MathF.Max(luma(c), 1e-4f), 0.6f, 1.25f);
            return Vector3.Lerp(c, Vector3.Lerp(target, keepingHue, dyed), t);
        }

        // ---------------------------------------------------------------- Shape

        // The second derivative of a line through three points along a normal, per metre per metre, for points unevenly spaced.
        private static float Curvature(Vector3 before, Vector3 at, Vector3 after, Vector3 normal)
        {
            float h1 = (at - before).Length, h2 = (after - at).Length;
            if (h1 < 1e-4f || h2 < 1e-4f) return 0f;
            Vector3 second = ((after - at) / h2 - (at - before) / h1) * (2f / (h1 + h2));
            return Vector3.Dot(second, normal);
        }

        // A fan of creases radiating from where a line pulls on the cloth, dx along it and dy up it in metres: strongest a little way out, gone by a couple of metres.
        private static float Fan(float dx, float dy, float amplitude, float reach, float creases)
        {
            float r = MathF.Sqrt(dx * dx + dy * dy);
            if (r > reach * 3.5f || r < 1e-4f) return 0f;
            float angle = MathF.Atan2(dx, dy);
            return amplitude * MathF.Cos(angle * creases) * Smooth(0.08f, 0.4f, r) * MathF.Exp(-r / reach);
        }

        // A gable's point: from its apex down to its foot across x from -1 to 1, bellied in and drawn up at its hem between its corners.
        private static Vector3 Gable(Vector3 apex, float halfWidth, float hem, float z, float x, float s, float face, float scallop, int seed)
        {
            Vector3 foot = new Vector3(x * halfWidth, hem + scallop * (1f - x * x) * 0.6f, z);
            Vector3 p = Vector3.Lerp(apex, foot, s);
            float across = MathF.Sin(MathF.PI * (x * 0.5f + 0.5f));
            float belly = 0.12f * across * MathF.Sin(MathF.PI * s) + 0.025f * (Noise(seed, x * 3f, s * 4f) - 0.5f) * across * s;
            return p - new Vector3(0f, 0f, face) * belly;
        }

        private static bool IsSeam(float[] seams, float z)
        {
            foreach (float s in seams) if (MathF.Abs(z - s) < 0.002f) return true;
            return false;
        }

        // Every point given, and a step either side of each if a step is given.
        private static float[] Extras(float[] at, float either, float centre = -1f)
        {
            var list = new List<float>();
            foreach (float a in at)
            {
                list.Add(a);
                if (either > 0f) { list.Add(a - either); list.Add(a + either); }
                if (centre > 0f) { list.Add(a - centre); list.Add(a + centre); }
            }
            return list.ToArray();
        }

        // Samples from `from` to `to` no further apart than `step`, with every wanted point in range among them, sorted, none closer than `gap`: where two crowd each other
        // the ends win, then what was wanted, then the regular steps.
        private static List<float> Samples(float from, float to, float step, float gap, IEnumerable<float> wanted, IEnumerable<float> more)
        {
            var all = new List<(float At, int Rank)>();
            int count = Math.Max(1, (int)MathF.Ceiling((to - from) / step));
            for (int i = 0; i <= count; i++) all.Add((from + (to - from) * i / count, i == 0 || i == count ? 2 : 0));
            foreach (float e in wanted.Concat(more)) if (e > from && e < to) all.Add((e, 1));
            all.Sort((x, y) => x.At.CompareTo(y.At));
            var kept = new List<(float At, int Rank)> { all[0] };
            for (int i = 1; i < all.Count; i++)
            {
                if (all[i].At - kept[^1].At >= gap) kept.Add(all[i]);
                else if (all[i].Rank > kept[^1].Rank) kept[^1] = all[i];
            }
            return kept.Select(k => k.At).ToList();
        }

        private static float Smooth(float edge0, float edge1, float x)
        {
            float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        private static float Hash(int seed, int x, int y)
        {
            uint h = (uint)seed * 0x9E3779B1u ^ (uint)x * 0x85EBCA77u ^ (uint)y * 0xC2B2AE3Du;
            h ^= h >> 15;
            h *= 0x2C1B3C6Du;
            h ^= h >> 12;
            h *= 0x297A2D39u;
            h ^= h >> 15;
            return (h & 0xFFFFFF) / 16777215f;
        }

        // Smooth value noise, 0 to 1.
        private static float Noise(int seed, float x, float y)
        {
            int ix = (int)MathF.Floor(x), iy = (int)MathF.Floor(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(seed, ix, iy), b = Hash(seed, ix + 1, iy), c = Hash(seed, ix, iy + 1), d = Hash(seed, ix + 1, iy + 1);
            return MathHelper.Lerp(MathHelper.Lerp(a, b, fx), MathHelper.Lerp(c, d, fx), fy);
        }
    }
}

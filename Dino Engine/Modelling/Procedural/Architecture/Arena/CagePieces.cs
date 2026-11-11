using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture.Plaza;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Arena
{
    /// <summary>
    /// The Jurassic Cage, the Amber Institute's breeding cage: black iron bars leaded into a stone footing and leaning in overhead so that nothing climbs out, riveted posts at even steps with brass caps,
    /// bands of iron round it; the gates of the brood's pens, squat blocks of stone with an iron door; the nests in the straw; the Institute's gallery outside, where the blood-doctors watch; troughs, and
    /// a gibbet with what's being fed hung from it. Model y = 0 is where people stand at a piece's foot, and +z its front. Headless.
    /// </summary>
    internal static class CagePieces
    {
        private const float Foot = -0.6f;
        private static readonly Vector3 Amber = new Vector3(1f, 0.55f, 0.12f);

        // ---------------------------------------------------------------- The bars

        /// <summary>
        /// Bars round an arc about the origin, standing on a footing whose top is at y = 0: each a round bar of black iron up to <paramref name="height"/>, then leaning in towards the middle by
        /// <paramref name="lean"/> as it rises another quarter of that; every sixth a square post, riveted, with a brass cap; and flat bands of iron tying them all, three up the uprights and one
        /// round the top. Its bars go round between bearings, clockwise from north.
        /// </summary>
        public static void Bars(MeshBuilder b, float radius, float from, float sweep, float height, float lean, float spacing, uint seed)
        {
            var random = new WorldRandom(seed).Fork("bars");
            VertexMaterial iron = ArenaStone.Iron(random), dark = ArenaStone.Iron(random, 0.8f), rust = ArenaStone.Rusted(random), brass = ArenaStone.Brass(random);
            float length = MathHelper.DegreesToRadians(MathF.Abs(sweep)) * radius;
            int count = Math.Max(2, (int)MathF.Round(length / spacing));
            float curve = height * 0.28f;
            Vector3 Point(float bearing, float r, float y) => new Vector3(MathF.Sin(MathHelper.DegreesToRadians(bearing)) * r, y, MathF.Cos(MathHelper.DegreesToRadians(bearing)) * r);
            // A bar's path: up, then over in a quarter circle towards the middle.
            Vector3 Path(float bearing, float t)
            {
                if (t <= 0.75f) return Point(bearing, radius, -0.1f + (height + 0.1f) * t / 0.75f);
                float a = (t - 0.75f) / 0.25f * MathF.PI * 0.5f;
                return Point(bearing, radius - lean * (1f - MathF.Cos(a)), height + curve * MathF.Sin(a));
            }
            for (int i = 0; i <= count; i++)
            {
                float bearing = from + sweep * i / count;
                bool post = i % 6 == 0;
                const int Steps = 8;
                for (int s = 0; s < Steps; s++)
                {
                    float t0 = s / (float)Steps, t1 = (s + 1) / (float)Steps;
                    Vector3 p0 = Path(bearing, t0), p1 = Path(bearing, t1);
                    if (post) b.Tube(p0, p1, 0.2f, 0.2f, 4, s < 2 && random.Chance(0.5f) ? rust : dark, capped: s == Steps - 1);
                    else b.Tube(p0, p1, 0.075f, 0.075f, 6, iron, capped: s == Steps - 1);
                }
                if (post)
                {
                    // Rivets down its face, and a brass cap at its head.
                    Vector3 inward = -Point(bearing, 1f, 0f);
                    for (float y = 0.6f; y < height - 0.3f; y += 0.9f) b.Box(Point(bearing, radius, y) + inward * 0.2f, new Vector3(0.05f), dark);
                    Vector3 head = Path(bearing, 1f);
                    b.Tube(head, head + Vector3.UnitY * 0.3f, 0.26f, 0.12f, 6, brass);
                }
                // Each bar is leaded into the footing: a dark collar where it goes in.
                b.Tube(Point(bearing, radius, -0.02f), Point(bearing, radius, 0.12f), post ? 0.3f : 0.13f, post ? 0.28f : 0.11f, 6, dark);
            }
            // Bands round it: three up the uprights, and one along the top of the curve.
            foreach (float t in new[] { 0.06f, 0.4f, 0.72f, 1f })
            {
                int segments = Math.Max(2, (int)MathF.Ceiling(length / 1.2f));
                for (int k = 0; k < segments; k++)
                {
                    float a = from + sweep * k / segments, c = from + sweep * (k + 1) / segments;
                    Vector3 p0 = Path(a, t), p1 = Path(c, t);
                    b.Tube(p0, p1, 0.06f, 0.06f, 4, dark, capped: false);
                }
            }
        }

        // ---------------------------------------------------------------- A pen's gate

        /// <summary>
        /// The gate of one of the brood's pens, set into the cage's footing, its face on z = 0 towards +z: a block of stone <paramref name="block"/> high with a square opening <paramref name="width"/>
        /// across and <paramref name="height"/> high under an iron lintel carrying the Institute's brass plate, an iron frame round it studded with rivets, a passage into the dark behind, and the door
        /// itself a model of its own, which rises into the block.
        /// </summary>
        public static void PenGate(MeshBuilder b, ArenaStoneKind stone, float width, float height, float depth, float block, uint seed)
        {
            var random = new WorldRandom(seed).Fork("pen gate");
            float half = width * 0.5f, outer = half + 1.3f, top = MathF.Max(block - 0.3f, height + 1.2f);
            VertexMaterial iron = ArenaStone.Iron(random), dark = ArenaStone.Iron(random, 0.75f);
            // The block: two jambs, a course over the lintel, and a cap, in long and short blocks.
            foreach (float side in new[] { -1f, 1f })
            {
                int courses = Math.Max(2, (int)MathF.Round(height / 0.6f));
                for (int c = 0; c < courses; c++)
                {
                    float y0 = Foot + (height - Foot) * c / courses, y1 = Foot + (height - Foot) * (c + 1) / courses;
                    float split = c % 2 == 0 ? 0.45f : 0.62f;
                    float inner = side * half, m = side * (half + (outer - half) * split), o = side * outer;
                    LayBox(b, inner, m, y0, y1, 0.2f, -depth, ArenaStone.DressedFacing(stone, random.Fork(c * 2 + (side > 0f ? 1 : 0)), y0));
                    LayBox(b, m, o, y0, y1, 0.2f, -depth, ArenaStone.DressedFacing(stone, random.Fork(c * 2 + 40 + (side > 0f ? 1 : 0)), y0));
                }
            }
            ArenaGates.Slabs(b, -outer, outer, -depth, 0.2f, height + 0.6f, top, 1.4f, stone, random.Fork("over"), 0.16f);
            ArenaGates.Slabs(b, -outer - 0.15f, outer + 0.15f, -depth - 0.15f, 0.35f, top, top + 0.3f, 1.5f, stone, random.Fork("cap"), 0.2f);
            // The lintel, an iron beam, and the frame round the opening with its rivets.
            b.Box(new Vector3(0f, height + 0.3f, 0.12f), new Vector3(half + 0.35f, 0.3f, 0.18f), dark);
            for (float x = -half - 0.2f; x <= half + 0.2f; x += 0.45f) b.Box(new Vector3(x, height + 0.3f, 0.31f), new Vector3(0.05f, 0.05f, 0.02f), iron);
            foreach (float side in new[] { -1f, 1f })
            {
                b.Box(new Vector3(side * (half + 0.12f), height * 0.5f, 0.12f), new Vector3(0.14f, height * 0.5f, 0.16f), dark);
                for (float y = 0.4f; y < height; y += 0.55f) b.Box(new Vector3(side * (half + 0.12f), y, 0.29f), new Vector3(0.045f, 0.045f, 0.02f), iron);
            }
            // The Institute's plate on the lintel: brass, with a hatch number struck into it.
            Vector3 plate = new Vector3(0f, height + 0.95f, 0.22f);
            b.Box(plate, new Vector3(0.9f, 0.28f, 0.03f), ArenaStone.Brass(random));
            for (int n = 0; n < 3; n++) b.Box(plate + new Vector3(-0.4f + n * 0.4f, 0f, 0.035f), new Vector3(0.11f, 0.17f, 0.01f), dark);
            // The passage into the dark, straw on its floor.
            b.Quad(new Vector3(-half, -0.2f, -depth + 0.3f), new Vector3(half, -0.2f, -depth + 0.3f), new Vector3(half, height, -depth + 0.3f), new Vector3(-half, height, -depth + 0.3f), ArenaStone.Dark);
            b.Quad(new Vector3(-half, height - 0.01f, 0.1f), new Vector3(-half, height - 0.01f, -depth + 0.3f), new Vector3(half, height - 0.01f, -depth + 0.3f), new Vector3(half, height - 0.01f, 0.1f), ArenaStone.Tint(ArenaStone.Hearting(stone), new Vector3(0.5f)));
            VertexMaterial straw = new VertexMaterial(Textures.MaterialCatalog.Thatch.Index, new Util.Colour(0.42f, 0.34f, 0.17f));
            b.Quad(new Vector3(-half, ArenaStone.Ground + 0.04f, 0.1f), new Vector3(half, ArenaStone.Ground + 0.04f, 0.1f), new Vector3(half, ArenaStone.Ground + 0.04f, -depth + 0.3f), new Vector3(-half, ArenaStone.Ground + 0.04f, -depth + 0.3f), straw);
        }

        private static void LayBox(MeshBuilder b, float x0, float x1, float y0, float y1, float zFront, float zBack, Func<Vector3, VertexMaterial> facing)
        {
            PlazaWalls.Lay(b, PlazaPieces.Corners(new Vector3(MathF.Min(x0, x1) + 0.004f, y0 + 0.003f, zFront), new Vector3(MathF.Max(x0, x1) - 0.004f, y1 - 0.003f, zBack)), ArenaStone.Chamfer, facing);
        }

        // ---------------------------------------------------------------- Nests

        /// <summary>A nest the Institute keeps its eggs in, <paramref name="size"/> across: a ring of straw heaped up round a hollow lined with it. The eggs are a body of their own, which can be broken.</summary>
        public static void Nest(MeshBuilder b, float size, uint seed)
        {
            var random = new WorldRandom(seed).Fork("nest");
            float r = size * 0.5f, ground = ArenaStone.Ground;
            const int Tufts = 26;
            for (int i = 0; i < Tufts; i++)
            {
                float a = i * MathF.Tau / Tufts + random.Range(-0.1f, 0.1f);
                float rr = r * random.Range(0.78f, 1.02f);
                Vector3 at = new Vector3(MathF.Cos(a) * rr, ground + 0.12f, MathF.Sin(a) * rr);
                Vector3 lean = new Vector3(MathF.Cos(a + 1.4f), random.Range(0.25f, 0.6f), MathF.Sin(a + 1.4f)) * random.Range(0.5f, 0.9f);
                VertexMaterial straw = new VertexMaterial(Textures.MaterialCatalog.Thatch.Index, new Util.Colour(new Vector3(0.64f, 0.52f, 0.27f) * random.Range(0.75f, 1.08f)));
                b.Tube(at - lean * 0.5f, at + lean * 0.5f, 0.2f * size / 3f, 0.08f * size / 3f, 5, straw);
            }
            // The hollow, lined, and the mud trodden round it.
            b.Tube(new Vector3(0f, ground - 0.05f, 0f), new Vector3(0f, ground + 0.06f, 0f), r * 0.9f, r * 0.82f, 16, new VertexMaterial(Textures.MaterialCatalog.Thatch.Index, new Util.Colour(0.52f, 0.41f, 0.21f)));
        }

        /// <summary>
        /// The Institute's eggs in a nest <paramref name="size"/> across (<see cref="Nest"/>), a body of their own that can be broken: seven to nine, long and leathery, stood on their blunt ends in a ring
        /// round the hollow and leaning out as a laying bird's are, one or two upright in the middle, and the Institute's brass tag on a stake among them, which numbers the brood. What's grown from blood in
        /// amber shows through the shell at its crown, which is what's seen of them from above, so that a clutch is something to go for at a glance. Once they've hatched or been smashed
        /// (<paramref name="broken"/>) the lower halves of their shells are left, with ragged rims, and pieces of them round about.
        /// </summary>
        public static void Clutch(MeshBuilder b, float size, uint seed, bool broken)
        {
            var random = new WorldRandom(seed).Fork("clutch");
            float ground = ArenaStone.Ground + 0.04f;
            float ring = size * 0.5f * 0.4f;
            int count = random.Int(7, 10);
            int middle = count >= 9 ? 2 : 1;
            for (int k = 0; k < count; k++)
            {
                WorldRandom one = random.Fork(k);
                float length = one.Range(0.62f, 0.78f), width = length * one.Range(0.56f, 0.64f);
                Vector3 foot, axis;
                if (k < count - middle)
                {
                    float a = k * MathF.Tau / (count - middle) + one.Range(-0.12f, 0.12f);
                    float lean = MathHelper.DegreesToRadians(one.Range(18f, 30f));
                    foot = new Vector3(MathF.Cos(a) * ring, ground, MathF.Sin(a) * ring);
                    axis = new Vector3(MathF.Cos(a) * MathF.Sin(lean), MathF.Cos(lean), MathF.Sin(a) * MathF.Sin(lean));
                }
                else
                {
                    float a = one.Float() * MathF.Tau;
                    foot = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a)) * (middle == 1 ? 0.05f : 0.2f * (k == count - 1 ? 1f : -1f)) + Vector3.UnitY * ground;
                    axis = Vector3.Normalize(new Vector3(one.Range(-0.12f, 0.12f), 1f, one.Range(-0.12f, 0.12f)));
                }
                // Pale and warm, a little different each: an albedo, so kept off white.
                var shell = new VertexMaterial(Textures.MaterialCatalog.Bone.Index, new Util.Colour(new Vector3(0.6f, 0.53f, 0.4f) * one.Range(0.84f, 1.02f) + new Vector3(0f, one.Range(-0.02f, 0.03f), one.Range(-0.03f, 0.02f))));
                // The amber at its crown, deep, as anything glowing that's pale goes white.
                VertexMaterial amber = ArenaStone.Glow(1f, 0.5f, 0.12f, one.Range(0.75f, 1.05f));
                Egg(b, foot, axis, length, width, shell, broken ? one.Range(0.46f, 0.6f) : 1f, one.Fork("rim"), broken ? null : amber);
            }
            if (broken)
            {
                // The pieces that came away, lying about the nest and both ways up.
                var piece = new VertexMaterial(Textures.MaterialCatalog.Bone.Index, new Util.Colour(0.56f, 0.5f, 0.38f));
                int pieces = random.Int(12, 18);
                for (int p = 0; p < pieces; p++)
                {
                    WorldRandom one = random.Fork(100 + p);
                    float a = one.Float() * MathF.Tau, d = one.Range(0.2f, size * 0.55f);
                    Vector3 at = new Vector3(MathF.Cos(a) * d, ground + 0.02f, MathF.Sin(a) * d);
                    float s = one.Range(0.06f, 0.12f), turn = one.Float() * MathF.Tau;
                    Vector3 u = new Vector3(MathF.Cos(turn), 0.08f, MathF.Sin(turn)) * s, v = new Vector3(-MathF.Sin(turn), 0.12f, MathF.Cos(turn)) * s * 0.8f;
                    b.Triangle(at - u, at + u, at + v, piece);
                    b.Triangle(at - u, at + v, at + u, piece);
                }
            }
            // The tag, the brood's number on brass, on an iron stake pushed in among them.
            VertexMaterial iron = ArenaStone.Iron(random), brass = ArenaStone.Brass(random);
            float tagAngle = random.Float() * MathF.Tau;
            Vector3 stake = new Vector3(MathF.Cos(tagAngle), 0f, MathF.Sin(tagAngle)) * (ring + 0.42f);
            b.Tube(stake + Vector3.UnitY * (ground - 0.1f), stake + Vector3.UnitY * 0.72f, 0.022f, 0.018f, 5, iron);
            Vector3 outward = new Vector3(MathF.Cos(tagAngle), 0f, MathF.Sin(tagAngle));
            b.Place(stake + Vector3.UnitY * 0.66f + outward * 0.03f, MathF.Atan2(outward.X, outward.Z));
            b.Box(Vector3.Zero, new Vector3(0.09f, 0.055f, 0.008f), brass);
            b.ResetPlacement();
        }

        // An egg from its foot along its axis, blunt end down, with what's in it showing through its crown if it's whole, or the lower part of its shell up to a share of its length with a ragged rim,
        // seen from inside as well as out.
        private static void Egg(MeshBuilder b, Vector3 foot, Vector3 axis, float length, float width, VertexMaterial shell, float upTo, WorldRandom random, VertexMaterial? crown = null)
        {
            const float CrownFrom = 0.66f;
            // Across it, so that e1 × e2 = axis, which with the angle going round forwards faces the surface out.
            Vector3 e1 = Vector3.Normalize(Vector3.Cross(axis, MathF.Abs(axis.Y) < 0.95f ? Vector3.UnitY : Vector3.UnitX));
            Vector3 e2 = Vector3.Cross(axis, e1);
            const int Around = 10, Along = 7;
            float[] rim = new float[Around + 1];
            for (int i = 0; i <= Around; i++) rim[i] = i == Around ? rim[0] : upTo * (i % 2 == 0 ? random.Range(1f, 1.12f) : random.Range(0.8f, 0.92f));
            Vector3 Point(float u, float v, float scale)
            {
                // Up to the rim where it's broken, which goes up and down round it.
                float top = upTo >= 1f ? 1f : rim[Math.Clamp((int)MathF.Round(u * Around), 0, Around)];
                float t = MathF.PI * v * MathF.Min(top, 1f);
                float along = length * (1f - MathF.Cos(t)) * 0.5f;
                float radius = width * 0.5f * MathF.Sin(t) * (1f + 0.12f * MathF.Cos(t)) * scale;
                float angle = MathF.Tau * u;
                return foot + axis * along + (e1 * MathF.Cos(angle) + e2 * MathF.Sin(angle)) * radius;
            }
            if (crown is VertexMaterial glow && upTo >= 1f)
            {
                b.Surface(Around, Along, (u, v) => Point(u, v * CrownFrom, 1f), shell);
                b.Surface(Around, 3, (u, v) => Point(u, CrownFrom + (1f - CrownFrom) * v, 1f), glow);
                return;
            }
            b.Surface(Around, Along, (u, v) => Point(u, v, 1f), shell);
            if (upTo < 1f)
            {
                // Its inside, the other way round, a little in from the outside.
                var inside = new VertexMaterial(shell.materialTextureIndex, new Util.Colour(0.42f, 0.36f, 0.26f));
                b.Surface(Around, Along, (u, v) => Point(1f - u, v, 0.9f), inside);
            }
        }

        // ---------------------------------------------------------------- The Institute's gallery

        /// <summary>
        /// The Institute's gallery outside a cage, round an arc about the origin at <paramref name="radius"/>: a deck of planks <paramref name="width"/> deep on iron posts at <paramref name="deck"/> up,
        /// a rail along its front with brass at its posts, amber lamps on poles, and its jars of amber on racks against the back rail, glowing.
        /// </summary>
        public static void Gallery(MeshBuilder b, float radius, float from, float sweep, float deck, float width, uint seed, List<AssetLight> lights)
        {
            var random = new WorldRandom(seed).Fork("gallery");
            VertexMaterial iron = ArenaStone.Iron(random), brass = ArenaStone.Brass(random);
            float length = MathHelper.DegreesToRadians(MathF.Abs(sweep)) * radius;
            Vector3 P(float along, float out_, float y)
            {
                float bearing = MathHelper.DegreesToRadians(from + sweep * along / length);
                float rr = radius + out_;
                return new Vector3(MathF.Sin(bearing) * rr, y, MathF.Cos(bearing) * rr);
            }
            int bays = Math.Max(1, (int)MathF.Round(length / 3.2f));
            for (int k = 0; k <= bays; k++)
            {
                float along = length * k / bays;
                foreach (float o in new[] { 0f, width })
                {
                    b.Tube(P(along, o, -0.4f), P(along, o, deck), 0.12f, 0.12f, 4, iron);
                    b.Tube(P(along, o, deck), P(along, o, deck + 1.5f), 0.06f, 0.06f, 4, iron);
                    b.Tube(P(along, o, deck + 1.5f), P(along, o, deck + 1.62f), 0.09f, 0.0f, 4, brass);
                }
                // Cross braces under the deck.
                if (k < bays)
                {
                    float next = length * (k + 1) / bays;
                    b.Tube(P(along, 0f, 0.5f), P(next, 0f, deck - 0.3f), 0.045f, 0.045f, 4, iron);
                    b.Tube(P(along, width, deck - 0.3f), P(next, width, 0.5f), 0.045f, 0.045f, 4, iron);
                }
            }
            // The deck, plank by plank across it, and the rails.
            int planks = Math.Max(2, (int)MathF.Round(length / 0.32f));
            for (int p = 0; p < planks; p++)
            {
                float a = length * p / planks + 0.012f, c = length * (p + 1) / planks - 0.012f;
                VertexMaterial plank = ArenaStone.Timber(random.Fork(p), random.Range(0.62f, 0.85f));
                PlazaWalls.Lay(b, new[]
                {
                    P(a, -0.1f, deck - 0.1f), P(c, -0.1f, deck - 0.1f), P(c, -0.1f, deck), P(a, -0.1f, deck),
                    P(a, width + 0.1f, deck - 0.1f), P(c, width + 0.1f, deck - 0.1f), P(c, width + 0.1f, deck), P(a, width + 0.1f, deck),
                }, 0.012f, _ => plank);
            }
            foreach (float o in new[] { 0f, width })
            {
                foreach (float y in new[] { deck + 0.7f, deck + 1.45f })
                {
                    for (int k = 0; k < bays * 2; k++) b.Tube(P(length * k / (bays * 2), o, y), P(length * (k + 1) / (bays * 2), o, y), 0.035f, 0.035f, 4, iron, capped: false);
                }
            }
            // Amber lamps on every other front post, and racks of jars of amber against the back rail.
            for (int k = 1; k < bays; k += 2)
            {
                Vector3 lamp = P(length * k / bays, -0.15f, deck + 2.3f);
                b.Tube(P(length * k / bays, 0f, deck + 1.5f), lamp + Vector3.UnitY * 0.3f, 0.04f, 0.04f, 4, iron);
                b.Box(lamp, new Vector3(0.16f, 0.24f, 0.16f), ArenaStone.Glow(Amber.X, Amber.Y, Amber.Z, 2f));
                b.Box(lamp + Vector3.UnitY * 0.28f, new Vector3(0.21f, 0.04f, 0.21f), brass);
                lights.Add(AssetLight.Glow(lamp, Amber, 60f, 16f) with { Shadow = true });
            }
            for (int k = 0; k < bays; k++)
            {
                if (random.Chance(0.35f)) continue;
                float along = length * (k + 0.5f) / bays;
                PlazaWalls.Lay(b, new[]
                {
                    P(along - 0.9f, width - 0.65f, deck), P(along + 0.9f, width - 0.65f, deck), P(along + 0.9f, width - 0.65f, deck + 0.9f), P(along - 0.9f, width - 0.65f, deck + 0.9f),
                    P(along - 0.9f, width - 0.2f, deck), P(along + 0.9f, width - 0.2f, deck), P(along + 0.9f, width - 0.2f, deck + 0.9f), P(along - 0.9f, width - 0.2f, deck + 0.9f),
                }, 0.02f, _ => ArenaStone.Timber(random.Fork(100 + k), 0.55f));
                int jars = random.Int(3, 6);
                for (int j = 0; j < jars; j++)
                {
                    Vector3 jar = P(along - 0.7f + 1.4f * (j + 0.5f) / jars, width - 0.42f, deck + 0.9f);
                    float glow = random.Range(0.7f, 1.4f);
                    b.Tube(jar, jar + Vector3.UnitY * 0.42f, 0.15f, 0.13f, 8, ArenaStone.Glow(Amber.X, Amber.Y * 0.9f, Amber.Z * 0.6f, glow));
                    b.Tube(jar + Vector3.UnitY * 0.42f, jar + Vector3.UnitY * 0.5f, 0.1f, 0.1f, 8, brass);
                }
            }
        }

        // ---------------------------------------------------------------- Troughs and the gibbet

        /// <summary>A feeding trough of stone, <paramref name="length"/> long, with what's left in it dark and wet.</summary>
        public static void Trough(MeshBuilder b, ArenaStoneKind stone, float length, uint seed)
        {
            var random = new WorldRandom(seed).Fork("trough");
            float half = length * 0.5f, ground = ArenaStone.Ground;
            const float Wide = 1.1f, High = 0.95f, Wall = 0.2f;
            ArenaStone.Block(b, new Vector3(0f, ground - 0.1f + 0.15f, 0f), new Vector3(half, 0.15f, Wide * 0.5f), 0f, ArenaStone.Chamfer, ArenaStone.DressedFacing(stone, random.Fork("bed"), 0f));
            foreach (float s in new[] { -1f, 1f })
            {
                ArenaStone.Block(b, new Vector3(0f, ground + High * 0.5f, s * (Wide * 0.5f - Wall * 0.5f)), new Vector3(half, High * 0.5f, Wall * 0.5f), 0f, ArenaStone.Chamfer, ArenaStone.DressedFacing(stone, random.Fork(s > 0f ? "near" : "far"), 0.4f));
                ArenaStone.Block(b, new Vector3(s * (half - Wall * 0.5f), ground + High * 0.5f, 0f), new Vector3(Wall * 0.5f, High * 0.5f, Wide * 0.5f - Wall), 0f, ArenaStone.Chamfer, ArenaStone.DressedFacing(stone, random.Fork(s > 0f ? "east" : "west"), 0.4f));
            }
            b.Quad(new Vector3(-half + Wall, ground + High * 0.62f, -Wide * 0.5f + Wall), new Vector3(-half + Wall, ground + High * 0.62f, Wide * 0.5f - Wall), new Vector3(half - Wall, ground + High * 0.62f, Wide * 0.5f - Wall), new Vector3(half - Wall, ground + High * 0.62f, -Wide * 0.5f + Wall),
                new VertexMaterial(Textures.MaterialCatalog.Mud.Index, new Util.Colour(0.12f, 0.035f, 0.025f)));
        }

        /// <summary>A gibbet of black iron, a post and an arm out towards +z with a chain hanging from its end, and on the chain a carcass hung for the tyrant: meat on the bone, darkened.</summary>
        public static void Gibbet(MeshBuilder b, float height, uint seed)
        {
            var random = new WorldRandom(seed).Fork("gibbet");
            VertexMaterial iron = ArenaStone.Iron(random), dark = ArenaStone.Iron(random, 0.7f);
            ArenaStone.Block(b, new Vector3(0f, -0.1f, 0f), new Vector3(0.6f, 0.35f, 0.6f), 0f, ArenaStone.BigChamfer, ArenaStone.Dressed(ArenaStoneKind.Granite, random, 0f));
            b.Tube(new Vector3(0f, 0.2f, 0f), new Vector3(0f, height, 0f), 0.2f, 0.16f, 6, iron);
            float reach = height * 0.42f;
            b.Tube(new Vector3(0f, height - 0.2f, 0f), new Vector3(0f, height - 0.2f, reach), 0.14f, 0.12f, 6, iron);
            b.Tube(new Vector3(0f, height - 1.6f, 0.1f), new Vector3(0f, height - 0.3f, reach * 0.6f), 0.08f, 0.08f, 5, iron);
            // The chain, link by link.
            float hang = height * 0.45f;
            Vector3 top = new Vector3(0f, height - 0.32f, reach - 0.05f);
            int links = Math.Max(4, (int)(hang / 0.32f));
            for (int l = 0; l < links; l++)
            {
                Vector3 a = top - Vector3.UnitY * (hang * l / links), c = top - Vector3.UnitY * (hang * (l + 1) / links);
                b.Tube(a, c, 0.05f, 0.05f, 4, l % 2 == 0 ? dark : iron);
            }
            // The carcass: a haunch of something big on a hook, raw where it was cut and dark where it's dried.
            Vector3 meat = top - Vector3.UnitY * (hang + 0.9f);
            VertexMaterial raw = new VertexMaterial(Textures.MaterialCatalog.Leather.Index, new Util.Colour(0.36f, 0.07f, 0.05f));
            VertexMaterial dried = new VertexMaterial(Textures.MaterialCatalog.Leather.Index, new Util.Colour(0.2f, 0.06f, 0.04f));
            b.Surface(8, 6, (u, v) =>
            {
                float theta = u * MathF.Tau, phi = v * MathF.PI;
                float swell = 1f + 0.25f * MathF.Sin(phi * 2f);
                return meat + new Vector3(MathF.Cos(theta) * MathF.Sin(phi) * 0.55f * swell, -MathF.Cos(phi) * 0.95f, -MathF.Sin(theta) * MathF.Sin(phi) * 0.45f * swell);
            }, dried);
            b.Tube(meat + Vector3.UnitY * 0.5f, meat + Vector3.UnitY * 0.95f, 0.22f, 0.08f, 6, raw);
            b.Tube(meat - Vector3.UnitY * 0.8f, meat - Vector3.UnitY * 1.35f, 0.12f, 0.1f, 6, new VertexMaterial(Textures.MaterialCatalog.Bone.Index, new Util.Colour(0.75f, 0.68f, 0.56f)));
        }
    }
}

using Dino_Engine.Modelling.Model;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.Modelling.Procedural.Architecture.Places.PlaceMaterials;

namespace Dino_Engine.Modelling.Procedural.Architecture.Places
{
    /// <summary>
    /// A snow castle (PLACES.md), as the north's children build one every winter, which the frost's cult keeps now: walls of packed snow cut into blocks with merlons along their tops, towers
    /// pressed out of buckets in tiers with a cone on top, an arch over the way in, snowmen with coal eyes, lanterns of snowballs piled round a candle as the north builds them, the sledges and
    /// spades and buckets left lying, pennants in the land's colours stuck in the snow, and at its heart the cult's altar of ice with the cold burning on it. Headless.
    /// </summary>
    internal static class SnowWorks
    {
        // Packed snow, a little bluer in its shadows' places and whiter where it's fresh.
        private static VertexMaterial Packed(WorldRandom random, float shade = 1f) => PackedSnow(shade * random.Range(0.94f, 1.03f));

        /// <summary>The frost cult's cold flame: pale and violet, nobody else's, as the Ossuary's light is green and a ghost's blue.</summary>
        private static readonly Vector3 Frost = new Vector3(0.74f, 0.7f, 1f);

        /// <summary>A wall of packed snow along x, cut into blocks in courses, battered a little, its top cut into merlons with gaps between, a few broken down. Numbers: length, height.</summary>
        public static void Wall(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float length = Math.Clamp(spec.Metres(0), 2f, 14f), height = Math.Clamp(spec.Metres(1), 0.8f, 3f);
            float half = length * 0.5f;
            const float Thick = 0.9f, Course = 0.42f;
            int courses = Math.Max(2, (int)MathF.Round(height / Course));
            float course = height / courses;
            for (int c = 0; c < courses; c++)
            {
                float y0 = -0.25f + c * course, y1 = (c + 1) * course;
                float inset = c * 0.04f;
                float x = -half + (c % 2 == 1 ? -0.3f : 0f);
                while (x < half - 0.05f)
                {
                    float w = random.Range(0.55f, 0.85f);
                    float x0 = MathF.Max(x, -half), x1 = MathF.Min(x + w, half);
                    x += w;
                    if (x1 - x0 < 0.2f) continue;
                    Kit.Block(b, new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, 0f), new Vector3((x1 - x0) * 0.5f - 0.01f, (y1 - y0) * 0.5f - 0.01f, Thick * 0.5f - inset), random.Range(-0.02f, 0.02f), 0.06f, Packed(random));
                }
            }
            // Merlons along its top, every other block, some knocked off.
            for (float x = -half + 0.35f; x < half - 0.3f; x += 0.9f)
            {
                if (random.Chance(0.15f)) continue;
                Kit.Block(b, new Vector3(x, height + 0.22f, 0f), new Vector3(0.28f, 0.22f, Thick * 0.42f), random.Range(-0.05f, 0.05f), 0.07f, Packed(random, 1.03f));
            }
            // Snow banked up against its foot.
            foreach (float side in new[] { -1f, 1f }) Kit.Heap(b, new Vector3(0f, Ground - 0.15f, side * (Thick * 0.5f + 0.2f)), new Vector2(half, 0.6f), 0.35f, 0f, PlaceMaterials.Snow(0.98f), random, 2, 12);
        }

        /// <summary>A tower pressed out of buckets in tiers, each a little narrower, merlons round the top tier and a cone over it, its sides marked with the rings of the buckets. Numbers: radius, height, tiers.</summary>
        public static void Tower(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float radius = Math.Clamp(spec.Metres(0), 0.8f, 3f), height = Math.Clamp(spec.Metres(1), 1.5f, 7f);
            int tiers = Math.Clamp(spec.Int(2), 1, 4);
            float tier = height / tiers, y = -0.2f, r = radius;
            for (int t = 0; t < tiers; t++)
            {
                float top = y + tier + (t == 0 ? 0.2f : 0f);
                // Each tier is closed on top, where the next stands and round it, as the bucket's bottom pressed it flat.
                Kit.Lathe(b, new Vector3(0f, y, 0f), Vector3.UnitY, top - y, Kit.Profile((0f, r), (0.15f, r * 0.98f), (0.85f, r * 0.94f), (1f, r * 0.9f)), 28, 3, Packed(random));
                // The bucket's rings round it.
                b.Tube(new Vector3(0f, top - 0.18f, 0f), new Vector3(0f, top - 0.1f, 0f), r * 0.93f + 0.02f, r * 0.92f + 0.02f, 28, Packed(random, 0.92f), capped: false);
                y = top;
                r *= 0.78f;
            }
            float last = r / 0.78f;
            int merlons = Math.Max(5, (int)(last * 4f));
            for (int k = 0; k < merlons; k++)
            {
                if (random.Chance(0.12f)) continue;
                float a = k * MathF.Tau / merlons;
                Kit.Block(b, new Vector3(MathF.Cos(a) * last * 0.82f, y + 0.18f, MathF.Sin(a) * last * 0.82f), new Vector3(0.18f, 0.18f, last * 0.14f + 0.08f), -a, 0.06f, Packed(random, 1.03f));
            }
            Kit.Lathe(b, new Vector3(0f, y, 0f), Vector3.UnitY, last * 1.4f, Kit.Profile((0f, last * 0.62f), (1f, 0f)), 20, 3, Packed(random, 1.04f));
            Kit.Heap(b, new Vector3(0f, Ground - 0.15f, 0f), new Vector2(radius + 0.7f, radius + 0.7f), 0.35f, 0f, PlaceMaterials.Snow(0.98f), random, 2, 14);
        }

        /// <summary>How far the gatehouse reaches out either side of its opening, to where the front wall's stretches begin, and how deep its arch's stones are.</summary>
        public const float GateSide = 1.75f, GateRing = 0.8f;

        /// <summary>How high a gatehouse of this width and height stands, merlons apart: a course of blocks over its arch's crown.</summary>
        public static float GateTop(float width, float height)
        {
            float r = Math.Clamp(width, 1.6f, 5f) * 0.5f;
            return MathF.Max(Math.Clamp(height, 2f, 5f) - r, 0.8f) + r + GateRing + 0.3f;
        }

        /// <summary>
        /// The gatehouse over the way in, across x, the opening along z: a block of snow as wide as the gap the front wall leaves it, an arch of nine stones through it,
        /// laid in courses that run out from the opening to the walls on either side and above the springing follow the arch's back to meet over its crown, and
        /// merlons along its top, as a wall's are. Numbers: width and height of the opening.
        /// </summary>
        public static void Gate(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float width = Math.Clamp(spec.Metres(0), 1.6f, 5f), height = Math.Clamp(spec.Metres(1), 2f, 5f);
            float r = width * 0.5f, spring = MathF.Max(height - r, 0.8f);
            float outer = r + GateSide, back = r + GateRing, top = GateTop(width, height);
            const float Depth = 1.1f, Course = 0.45f, Foot = -0.25f;
            // The jambs, from the ground to the springing.
            int low = Math.Max(2, (int)MathF.Round((spring - Foot) / Course));
            for (int c = 0; c < low; c++) GateCourse(b, random, Foot + (spring - Foot) * c / low, Foot + (spring - Foot) * (c + 1) / low, r, r, outer, c % 2 == 1, Depth);
            // Over them, each course's inner end on the arch's back, until they meet over the crown.
            float BackAt(float y) => y >= spring + back ? 0f : MathF.Sqrt(MathF.Max(back * back - (y - spring) * (y - spring), 0f));
            int high = Math.Max(2, (int)MathF.Round((top - spring) / Course));
            for (int c = 0; c < high; c++)
            {
                float y0 = spring + (top - spring) * c / high, y1 = spring + (top - spring) * (c + 1) / high;
                GateCourse(b, random, y0, y1, BackAt(y0), BackAt(y1), outer, (low + c) % 2 == 1, Depth);
            }
            // The arch itself.
            const int Stones = 9;
            Vector2 In(float a) => new Vector2(MathF.Cos(a) * r, spring + MathF.Sin(a) * r);
            Vector2 Out(float a) => new Vector2(MathF.Cos(a) * back, spring + MathF.Sin(a) * back);
            for (int k = 0; k < Stones; k++)
            {
                float a0 = MathF.PI * k / Stones, a1 = MathF.PI * (k + 1) / Stones;
                Waygates.Masonry.Prism(b, In(a1), In(a0), Out(a0), Out(a1), -Depth * 0.5f - 0.03f, Depth * 0.5f + 0.03f, 0.06f, Packed(random, 1.02f));
            }
            // Merlons along its top, some knocked off.
            for (float x = -outer + 0.35f; x < outer - 0.3f; x += 0.9f)
            {
                if (random.Chance(0.12f)) continue;
                Kit.Block(b, new Vector3(x, top + 0.22f, 0f), new Vector3(0.28f, 0.22f, Depth * 0.42f), random.Range(-0.05f, 0.05f), 0.07f, Packed(random, 1.03f));
            }
            // Snow banked against its feet, both sides of both jambs.
            foreach (float s in new[] { -1f, 1f })
            {
                foreach (float z in new[] { -1f, 1f }) Kit.Heap(b, new Vector3(s * (r + GateSide * 0.5f), Ground - 0.15f, z * (Depth * 0.5f + 0.2f)), new Vector2(GateSide * 0.5f, 0.5f), 0.3f, 0f, PlaceMaterials.Snow(0.98f), random, 2, 10);
            }
        }

        // One course of the gatehouse on both sides of the opening, from its inner end (in0 at its foot, in1 at its head, which differ where it follows the arch's back) out to
        // the side, in blocks of a hand's choosing, the first of them cut to the opening or the arch; every other course starts with a shorter block, so the joints break.
        private static void GateCourse(MeshBuilder b, WorldRandom random, float y0, float y1, float in0, float in1, float outer, bool shifted, float depth)
        {
            const float Joint = 0.012f;
            float back = -depth * 0.5f, front = depth * 0.5f;
            foreach (float s in new[] { -1f, 1f })
            {
                Vector2 P(float x, float y) => new Vector2(s * x, y);
                float inner = MathF.Max(in0, in1);
                float cut = MathF.Min(inner + (shifted ? random.Range(0.3f, 0.5f) : random.Range(0.6f, 0.85f)), outer);
                if (outer - cut < 0.25f) cut = outer;
                Waygates.Masonry.Prism(b, P(in0 + Joint, y0 + Joint), P(cut - Joint, y0 + Joint), P(cut - Joint, y1 - Joint), P(in1 + Joint, y1 - Joint), back, front, 0.05f, Packed(random));
                while (cut < outer - 0.01f)
                {
                    float next = MathF.Min(cut + random.Range(0.55f, 0.85f), outer);
                    if (outer - next < 0.25f) next = outer;
                    Waygates.Masonry.Prism(b, P(cut + Joint, y0 + Joint), P(next - Joint, y0 + Joint), P(next - Joint, y1 - Joint), P(cut + Joint, y1 - Joint), back, front, 0.05f, Packed(random));
                    cut = next;
                }
            }
        }

        /// <summary>A snowman of two balls or three, coal eyes and buttons, a carrot nose, stick arms, and a scarf in the land's colours or a bucket on its head. Numbers: height, form.</summary>
        public static void Snowman(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float height = Math.Clamp(spec.Metres(0), 0.8f, 3f);
            bool three = spec.Int(1) == 1;
            float[] parts = three ? new[] { 0.42f, 0.33f, 0.25f } : new[] { 0.58f, 0.42f };
            float y = -0.05f;
            float headR = 0f, headY = 0f;
            foreach (float share in parts)
            {
                float rr = height * share * 0.5f;
                Kit.Ball(b, new Vector3(0f, y + rr * 0.95f, 0f), new Vector3(rr, rr * 0.95f, rr), 16, 10, PlaceMaterials.Snow(random.Range(0.95f, 1.02f)));
                headR = rr;
                headY = y + rr * 0.95f;
                y += rr * 1.75f;
            }
            VertexMaterial coal = Flat(new Vector3(0.03f, 0.03f, 0.03f));
            foreach (float s in new[] { -0.35f, 0.35f }) Kit.Block(b, new Vector3(s * headR, headY + headR * 0.25f, headR * 0.9f), new Vector3(0.035f), 0f, 0.01f, coal);
            Kit.Lathe(b, new Vector3(0f, headY, headR * 0.92f), Vector3.UnitZ, headR * 0.9f, Kit.Profile((0f, 0.05f), (1f, 0f)), 6, 1, Painted(new Vector3(0.62f, 0.25f, 0.05f)));
            // Stick arms.
            float bodyY = three ? height * 0.52f : height * 0.32f;
            foreach (float s in new[] { -1f, 1f }) Kit.Pole(b, new Vector3(s * height * 0.18f, bodyY, 0f), new Vector3(s * height * 0.48f, bodyY + height * random.Range(0.08f, 0.22f), random.Range(-0.1f, 0.1f)), 0.025f, Timber(random, 0.7f));
            if (random.Chance(0.5f))
            {
                // A scarf in the land's colours, never the Reich's.
                Vector3 colour = BoatPaints[random.Int(BoatPaints.Length)];
                b.Tube(new Vector3(0f, headY - headR * 0.95f, 0f), new Vector3(0f, headY - headR * 0.7f, 0f), headR * 0.95f, headR * 0.9f, 12, new VertexMaterial(Textures.MaterialCatalog.Cloth.Index, new Colour(colour)), capped: false);
                Kit.Plank(b, new Vector3(headR * 0.5f, headY - headR * 0.85f, headR * 0.7f), new Vector3(headR * 0.65f, headY - headR * 2.2f, headR * 0.85f), Vector3.UnitZ, 0.14f, 0.03f, new VertexMaterial(Textures.MaterialCatalog.Cloth.Index, new Colour(colour)));
            }
            else Kit.Lathe(b, new Vector3(0f, headY + headR * 0.75f, 0f), Vector3.UnitY, headR * 0.8f, Kit.Profile((0f, headR * 0.6f), (1f, headR * 0.5f)), 10, 1, Timber(random, 0.8f));
        }

        /// <summary>A lantern of snowballs piled in a ring that narrows to its top round a candle, the light coming through the gaps, as the north builds them on a winter's night. Numbers: size.</summary>
        public static void SnowLantern(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            float size = Math.Clamp(spec.Metres(0), 0.5f, 2f);
            int rings = 4;
            float ball = size * 0.14f;
            for (int ring = 0; ring < rings; ring++)
            {
                float rr = size * 0.45f * (1f - ring / (float)(rings + 1));
                int count = Math.Max(3, (int)(MathF.Tau * rr / (ball * 2.1f)));
                for (int k = 0; k < count; k++)
                {
                    float a = (k + ring * 0.5f) * MathF.Tau / count;
                    Vector3 at = new Vector3(MathF.Cos(a) * rr, ball * 0.8f + ring * ball * 1.7f, MathF.Sin(a) * rr);
                    // Pressed a little flat by what's on them.
                    Kit.Ball(b, at, new Vector3(1f, 0.92f, 1f) * (ball * random.Range(0.92f, 1.06f)), 12, 7, PlaceMaterials.Snow(random.Range(0.96f, 1.03f)));
                }
            }
            // The last ball sits down into the top ring, resting on its balls: two radii from each of their middles, less a little for the snow giving.
            float topRing = size * 0.45f * (1f - (rings - 1) / (float)(rings + 1));
            float rest = MathF.Sqrt(MathF.Max(4f * ball * ball - topRing * topRing, ball * ball)) * 0.94f;
            Kit.Ball(b, new Vector3(0f, ball * 0.8f + (rings - 1) * ball * 1.7f + rest, 0f), new Vector3(ball, ball * 0.94f, ball), 12, 7, PlaceMaterials.Snow());
            // The candle inside, and its light.
            Kit.Lathe(b, new Vector3(0f, 0.05f, 0f), Vector3.UnitY, size * 0.25f, Kit.Profile((0f, 0.05f), (1f, 0.05f)), 6, 1, new VertexMaterial(Textures.MaterialCatalog.Wax.Index, new Colour(0.75f, 0.7f, 0.58f)));
            Kit.Lathe(b, new Vector3(0f, 0.05f + size * 0.25f, 0f), Vector3.UnitY, 0.06f, Kit.Profile((0f, 0.022f), (1f, 0f)), 5, 1, LampGlow(3f));
            lights.Add(SnowCandle(new Vector3(0f, size * 0.4f, 0f)));
        }

        /// <summary>What the children left lying: sledges, a wooden spade stuck in the snow, buckets turned over. Numbers: count.</summary>
        public static void Sledges(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            int count = Math.Clamp(spec.Int(0), 1, 4);
            VertexMaterial wood = Timber(random, 1.05f), rope = Rope(random);
            for (int k = 0; k < count; k++)
            {
                Vector3 at = new Vector3(random.Range(-1.5f, 1.5f), Ground, random.Range(-1.2f, 1.2f));
                float yaw = random.Float() * MathF.Tau;
                float c = MathF.Cos(yaw), s = MathF.Sin(yaw);
                Vector3 X(float x, float y, float z) => at + new Vector3(x * c + z * s, y, -x * s + z * c);
                foreach (float x in new[] { -0.22f, 0.22f })
                {
                    var runner = new List<Vector3>();
                    for (int j = 0; j <= 4; j++)
                    {
                        float t = j / 4f;
                        runner.Add(X(x, 0.05f + (t > 0.75f ? MathF.Pow((t - 0.75f) / 0.25f, 2f) * 0.22f : 0f), -0.55f + t * 1.15f));
                    }
                    Kit.Bend(b, runner, 0.025f, 0.025f, wood);
                }
                for (float z = -0.45f; z < 0.5f; z += 0.18f) Kit.Plank(b, X(-0.28f, 0.17f, z), X(0.28f, 0.17f, z), Vector3.UnitY, 0.12f, 0.025f, wood);
                Kit.Rope(b, X(0f, 0.25f, 0.62f), X(random.Range(-0.4f, 0.4f), 0.02f, 1.3f), 0.05f, 0.015f, rope, 4);
            }
            // A spade stuck in the snow and a bucket turned over by it.
            Vector3 spade = new Vector3(random.Range(-1.2f, 1.2f), 0f, random.Range(-1f, 1f));
            Kit.Pole(b, spade - Vector3.UnitY * 0.1f, spade + new Vector3(0.1f, 1.1f, 0.05f), 0.025f, wood);
            Kit.Span(b, spade + new Vector3(-0.12f, -0.2f, -0.02f), spade + new Vector3(0.12f, 0.15f, 0.02f), 0.01f, wood);
            Kit.Lathe(b, new Vector3(random.Range(-1.4f, 1.4f), Ground - 0.02f, random.Range(-1.2f, 1.2f)), Vector3.UnitY, 0.3f, Kit.Profile((0f, 0.18f), (1f, 0.13f)), 10, 1, Painted(BoatPaints[random.Int(BoatPaints.Length)]));
        }

        /// <summary>The colour a pennant's id gives for the frost cult's violet rather than one of the land's.</summary>
        public const int CultPennant = 9;

        /// <summary>
        /// A pennant on a stick stuck in the snow, a long triangle in one of the land's own colours, never the Reich's, or, longer, the frost cult's violet where it's taken a castle
        /// (<see cref="CultPennant"/>). Numbers: height, colour.
        /// </summary>
        public static void Pennant(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float height = Math.Clamp(spec.Metres(0), 1f, 4f);
            // The frost cult's (colour 9) is its violet, longer, hung over the children's colours where it's taken their castle.
            bool cult = spec.Int(1) == CultPennant;
            Vector3 colour = cult ? new Vector3(0.74f, 0.7f, 1f) * new Vector3(0.6f, 0.5f, 0.82f) : BoatPaints[Math.Clamp(spec.Int(1), 0, BoatPaints.Length - 1)] * 1.15f;
            Kit.Pole(b, new Vector3(0f, -0.3f, 0f), new Vector3(0f, height, 0f), cult ? 0.04f : 0.03f, Timber(random));
            VertexMaterial cloth = new VertexMaterial(Textures.MaterialCatalog.Cloth.Index, new Colour(colour));
            float droop = random.Range(0.05f, 0.2f), fly = cult ? 1.5f : 0.9f, drop = cult ? 0.9f : 0.5f;
            Vector3 top = new Vector3(0f, height - 0.05f, 0f), foot = new Vector3(0f, height - drop, 0f), tip = new Vector3(fly, height - drop * 0.7f - droop, random.Range(-0.15f, 0.15f));
            Kit.Face(b, top, foot, tip, Vector3.UnitZ, cloth);
            Kit.Face(b, top + Vector3.UnitZ * -0.005f, tip + Vector3.UnitZ * -0.005f, foot + Vector3.UnitZ * -0.005f, -Vector3.UnitZ, cloth);
        }

        /// <summary>The frost cult's altar: a block of ice on a step of packed snow, shards of ice standing round it, and on it the cold burning, pale violet, giving off its own light.</summary>
        public static void IceAltar(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            Kit.Block(b, new Vector3(0f, 0.12f, 0f), new Vector3(1.5f, 0.3f, 1.1f), 0f, 0.08f, Packed(random, 1.02f));
            Kit.Block(b, new Vector3(0f, 0.85f, 0f), new Vector3(0.75f, 0.45f, 0.5f), random.Range(-0.05f, 0.05f), 0.04f, Ice(1.05f));
            int shards = random.Int(6, 9);
            for (int k = 0; k < shards; k++)
            {
                float a = k * MathF.Tau / shards + random.Range(-0.2f, 0.2f), d = random.Range(1.6f, 2.2f);
                Vector3 foot = new Vector3(MathF.Cos(a) * d, -0.1f, MathF.Sin(a) * d);
                Vector3 tip = foot + new Vector3(random.Range(-0.2f, 0.2f), random.Range(0.8f, 1.8f), random.Range(-0.2f, 0.2f));
                Kit.Pole(b, foot, tip, random.Range(0.1f, 0.18f), Ice(random.Range(0.9f, 1.1f)), 0.01f);
            }
            // The cold flame on it: tongues of light, and the light itself.
            for (int k = 0; k < 3; k++)
            {
                Vector3 at = new Vector3(random.Range(-0.3f, 0.3f), 1.3f, random.Range(-0.2f, 0.2f));
                Kit.Lathe(b, at, Vector3.UnitY, random.Range(0.4f, 0.7f), Kit.Profile((0f, 0.12f), (0.4f, 0.14f), (1f, 0f)), 6, 2, Glow(Frost.X, Frost.Y, Frost.Z, 2.4f));
            }
            lights.Add(AssetLight.Glow(new Vector3(0f, 1.6f, 0f), Frost, 40f, 10f));
        }
    }
}

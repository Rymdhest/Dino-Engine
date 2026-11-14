using Dino_Engine.Modelling.Model;
using Dino_Engine.Util;
using Dino_Engine.World;
using OpenTK.Mathematics;
using static Dino_Engine.Modelling.Procedural.Architecture.Places.PlaceMaterials;

namespace Dino_Engine.Modelling.Procedural.Architecture.Places
{
    /// <summary>
    /// A quarry (PLACES.md), where the Reich cuts the stone for its forts and pens with the hands of the land it took: the face stepping back into the hill in benches, its risers jogging where
    /// the blocks came out, the topsoil stripped back from its lip and the rubble tipped down its ends; the blocks cut and lying where they were levered down, each daubed in oxblood for whose
    /// it is, and stacked on bearers to be carted; the treadwheel crane that lowers them; a block on its sledge on rollers; the masons' bankers with their chips round them; and a block on the
    /// floor with the wedges in a row along the line it'll split on. Every stone is the land's own, its colour in each id (<see cref="PlaceMaterials.StoneOf"/>), so a quarry's blocks are the
    /// cliffs they came out of. Headless.
    /// </summary>
    internal static class Quarry
    {
        private static Vector3 StoneOfId(in PlaceSpec spec, int first) => StoneColour(spec.Int(first), spec.Int(first + 1), spec.Int(first + 2));

        // ---------------------------------------------------------------- The face

        /// <summary>
        /// A quarry's face: benches of cut stone stepping back and up into the hill from its foot along x, each a man's height or so, its risers jogging in and out where blocks have come
        /// out of them and blocks standing proud of them half cut free; the topsoil stripped back from its lip and lying dark over the top of the stone; loose blocks left on its treads and a
        /// ladder up them; and the rubble tipped down its ends, where it dies into the hillside. Its foot is at z = 0 and its lip at z = -benches × ledge, at the height the id says: the
        /// ground behind it is laid to that height, the ground in front to its foot, and the ground under it to a ramp half a metre under the foot of every riser (QuarryBuilder), so that it
        /// never comes up through a tread. Numbers: width, height, benches, ledge, how far its top runs back past its lip, stone.
        /// </summary>
        public static void Face(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float width = Math.Clamp(spec.Metres(0), 10f, 44f), height = Math.Clamp(spec.Metres(1), 2f, 10f);
            int benches = Math.Clamp(spec.Int(2), 1, 5);
            float ledge = Math.Clamp(spec.Metres(3), 1f, 3f), over = Math.Clamp(spec.Metres(4), 1f, 8f);
            Vector3 stone = StoneOfId(spec, 5);
            float rise = height / benches, half = width * 0.5f, lip = -benches * ledge, back = lip - over;
            VertexMaterial soil = Soil(0.8f), turf = Turf(0.9f);
            float topsoil = random.Range(0.4f, 0.75f);
            for (int k = 0; k < benches; k++)
            {
                float y0 = k * rise, y1 = (k + 1) * rise, zb = -k * ledge;
                bool top = k == benches - 1;
                // Each bench is cells along x, a block's face each, jogging in and out where blocks came away; the lowest stands a little into the ground, and the top's tread runs back over
                // the dip behind the lip.
                var cells = new List<(float X0, float X1, float Z)>();
                for (float x = -half; x < half - 0.01f;)
                {
                    float next = MathF.Min(x + random.Range(1.8f, 4.6f), half);
                    if (half - next < 1.2f) next = half;
                    cells.Add((x, next, zb + random.Range(-1f, 0.35f)));
                    x = next;
                }
                float bottom = k == 0 ? -0.45f : y0 - 0.02f;
                float treadBack = top ? back : -(k + 1) * ledge - 1.2f;
                float tread = top ? height + 0.05f : y1;
                for (int i = 0; i < cells.Count; i++)
                {
                    (float x0, float x1, float z) = cells[i];
                    float y = k * rise + 1f;
                    // The riser: stone, and on the top bench the topsoil over it, dark.
                    float stoneTop = top ? y1 - topsoil : y1;
                    Kit.Face(b, new Vector3(x0, bottom, z), new Vector3(x1, bottom, z), new Vector3(x1, stoneTop, z), new Vector3(x0, stoneTop, z), Vector3.UnitZ, CutStone(stone * random.Range(0.93f, 1.05f), y));
                    if (top) Kit.Face(b, new Vector3(x0, stoneTop, z), new Vector3(x1, stoneTop, z), new Vector3(x1, tread, z), new Vector3(x0, tread, z), Vector3.UnitZ, soil);
                    // Its tread, back under the next bench's riser, strewn with chips, or the turf over the top.
                    Kit.Face(b, new Vector3(x0, tread, z), new Vector3(x1, tread, z), new Vector3(x1, tread, treadBack), new Vector3(x0, tread, treadBack), Vector3.UnitY, top ? turf : CutStone(stone * 0.86f, y1));
                    // A block standing proud of it, half cut free, on the tread below or the floor.
                    if (!top && x1 - x0 > 1.6f && random.Chance(0.35f))
                    {
                        float w = MathF.Min((x1 - x0) * random.Range(0.3f, 0.6f), 2.2f) * 0.5f;
                        float d = random.Range(0.35f, MathF.Min(0.85f, ledge - 0.5f)) * 0.5f;
                        float h = (y1 - y0) * random.Range(0.55f, 1f) * 0.5f;
                        float cx = random.Range(x0 + w + 0.1f, x1 - w - 0.1f);
                        Kit.Block(b, new Vector3(cx, y0 + h - 0.02f, z + d - 0.05f), new Vector3(w, h, d), 0f, 0.05f, CutStone(stone * random.Range(0.96f, 1.08f), y));
                    }
                    // Where it jogs from the cell before, the side of the step between them, facing whichever way is open.
                    if (i > 0)
                    {
                        float before = cells[i - 1].Z;
                        if (MathF.Abs(before - z) > 0.01f)
                        {
                            Vector3 open = before > z ? Vector3.UnitX : -Vector3.UnitX;
                            float zFront = MathF.Max(before, z), zBack = MathF.Min(before, z);
                            Kit.Face(b, new Vector3(x0, bottom, zFront), new Vector3(x0, bottom, zBack), new Vector3(x0, tread, zBack), new Vector3(x0, tread, zFront), open, CutStone(stone * 0.9f, y));
                        }
                    }
                }
                // The bench's ends, closed from its riser back into the hill.
                foreach ((float x, float zEnd, Vector3 out_) in new[] { (-half, cells[0].Z, -Vector3.UnitX), (half, cells[^1].Z, Vector3.UnitX) })
                {
                    Kit.Face(b, new Vector3(x, bottom, zEnd), new Vector3(x, bottom, treadBack), new Vector3(x, tread, treadBack), new Vector3(x, tread, zEnd), out_, CutStone(stone * 0.88f, k * rise + 1f));
                }
            }
            // Blocks left on the treads where they were levered out, waiting to be lowered, a few split along their drill lines.
            for (int k = 0; k < benches - 1; k++)
            {
                int left = random.Int(1, 3);
                for (int n = 0; n < left; n++)
                {
                    Vector3 size = new Vector3(random.Range(0.7f, 1.5f), random.Range(0.5f, 0.9f), random.Range(0.5f, MathF.Min(0.9f, ledge * 0.45f)));
                    float x = random.Range(-half + 2f, half - 2f);
                    float z = -k * ledge - ledge * 0.5f - 0.1f;
                    Kit.Block(b, new Vector3(x, (k + 1) * rise + size.Y * 0.5f, z), size * 0.5f, random.Range(-0.3f, 0.3f), 0.05f, CutStone(stone * random.Range(0.95f, 1.08f), (k + 1) * rise + 1f));
                }
            }
            // A ladder up from the floor to the first tread, and on up the next.
            float lx = random.Range(-half * 0.6f, half * 0.6f);
            for (int k = 0; k < Math.Min(benches - 1, 2); k++)
            {
                Vector3 foot = new Vector3(lx + k * random.Range(1f, 3f), k * rise, -k * ledge + 0.9f), head = new Vector3(foot.X, (k + 1) * rise + 0.8f, -k * ledge + 0.05f);
                Ladder(b, foot, head, Weathered(random), random);
            }
            // The rubble tipped down its ends, from near its top down onto the floor and the hillside, where the face dies out: spalls and broken blocks over a dark core of what's
            // between them, more and bigger towards its foot, where the big pieces roll to.
            foreach (float s in new[] { -1f, 1f })
            {
                Vector2 radii = new Vector2(4.8f, MathF.Abs(lip) * 0.5f + 4f);
                Vector3 centre = new Vector3(s * (half + 0.4f), -0.35f, lip * 0.5f + 0.6f);
                float peak = height * 0.82f;
                Kit.Heap(b, centre, radii, peak, 0f, CutStone(stone * 0.5f, 0.3f), random, 5, 16);
                for (int n = 0; n < 34; n++)
                {
                    float a = random.Float() * MathF.Tau, t = MathF.Sqrt(random.Float()) * 0.92f;
                    float x = MathF.Cos(a) * radii.X * t, z = MathF.Sin(a) * radii.Y * t;
                    float y = centre.Y + peak * (1f - t * t) * 0.92f;
                    float r = random.Range(0.18f, 0.32f) + t * random.Range(0.1f, 0.45f);
                    Kit.Block(b, new Vector3(centre.X + x, y + r * 0.15f, centre.Z + z), new Vector3(r, r * 0.6f, r * 0.8f), random.Float() * MathF.Tau, r * 0.12f, CutStone(stone * random.Range(0.72f, 1f), MathF.Max(y, 0.3f)));
                }
            }
        }

        private static void Ladder(MeshBuilder b, Vector3 foot, Vector3 head, VertexMaterial wood, WorldRandom random)
        {
            Vector3 side = new Vector3(0.28f, 0f, 0f);
            Kit.Pole(b, foot - side, head - side, 0.045f, wood);
            Kit.Pole(b, foot + side, head + side, 0.045f, wood);
            float length = (head - foot).Length;
            for (float t = 0.3f / length; t < 1f; t += 0.32f / length) Kit.Pole(b, Vector3.Lerp(foot, head, t) - side, Vector3.Lerp(foot, head, t) + side, 0.03f, wood);
        }

        // ---------------------------------------------------------------- What's cut

        // The Reich's daub on a block that's to go to it: a stroke of oxblood across its top, and down its face, as every block it takes is marked for whose it is.
        private static void Daub(MeshBuilder b, Vector3 centre, Vector3 half, float yaw, WorldRandom random)
        {
            float c = MathF.Cos(yaw), s = MathF.Sin(yaw);
            Vector3 X(float x, float y, float z) => centre + new Vector3(x * c + z * s, y, -x * s + z * c);
            float w = MathF.Min(half.X, half.Z) * random.Range(0.25f, 0.4f);
            float along = random.Range(-0.3f, 0.3f) * half.X;
            VertexMaterial paint = OxbloodPaint(random.Range(0.85f, 1.1f));
            float top = half.Y + 0.006f;
            Kit.Face(b, X(along - w, top, -half.Z * 0.92f), X(along + w, top, -half.Z * 0.92f), X(along + w, top, half.Z * 0.92f), X(along - w, top, half.Z * 0.92f), Vector3.UnitY, paint);
            Vector3 front = new Vector3(s, 0f, c);
            Kit.Face(b, X(along - w, top, half.Z + 0.006f), X(along + w, top, half.Z + 0.006f), X(along + w, -half.Y * 0.3f, half.Z + 0.006f), X(along - w, -half.Y * 0.3f, half.Z + 0.006f), front, paint);
        }

        /// <summary>A block of the land's stone, split rough from the face or dressed square by a mason, and if it's the Reich's, daubed for it. Numbers: width, height, depth, stone, finish, daubed.</summary>
        public static void Block(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            Vector3 half = new Vector3(Math.Clamp(spec.Metres(0), 0.3f, 3.5f), Math.Clamp(spec.Metres(1), 0.3f, 2.5f), Math.Clamp(spec.Metres(2), 0.3f, 3f)) * 0.5f;
            Vector3 stone = StoneOfId(spec, 3);
            bool dressed = spec.Int(6) == 1;
            Vector3 centre = new Vector3(0f, half.Y - 0.04f, 0f);
            Kit.Block(b, centre, half, 0f, dressed ? 0.025f : 0.07f, dressed ? Dressed(stone, random, 1f) : CutStone(stone * random.Range(0.95f, 1.05f), 1f));
            if (spec.Int(7) == 1) Daub(b, centre, half, 0f, random);
            // Chips knocked off it round its foot.
            for (int k = 0; k < 3; k++)
            {
                float a = random.Float() * MathF.Tau, r = random.Range(0.08f, 0.18f);
                Vector3 at = new Vector3(MathF.Cos(a) * (half.X + 0.3f), r * 0.4f, MathF.Sin(a) * (half.Z + 0.3f));
                Kit.Block(b, at, new Vector3(r, r * 0.5f, r * 0.8f), random.Float() * MathF.Tau, 0.02f, CutStone(stone * 0.9f, 0.3f));
            }
        }

        /// <summary>Blocks dressed and stacked to be carted, on timber bearers, in a row and a row on it, each daubed for the Reich. Numbers: count, stone.</summary>
        public static void BlockStack(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            int count = Math.Clamp(spec.Int(0), 2, 9);
            Vector3 stone = StoneOfId(spec, 1);
            Vector3 half = new Vector3(random.Range(0.55f, 0.75f), random.Range(0.32f, 0.42f), random.Range(0.42f, 0.55f));
            int bottom = (count + 1) / 2;
            float length = bottom * (half.X * 2f + 0.12f);
            VertexMaterial wood = Timber(random, 0.9f);
            foreach (float z in new[] { -half.Z * 0.6f, half.Z * 0.6f }) Kit.Plank(b, new Vector3(-length * 0.5f - 0.2f, Ground + 0.06f, z), new Vector3(length * 0.5f + 0.2f, Ground + 0.06f, z), Vector3.UnitY, 0.22f, 0.18f, wood);
            int placed = 0;
            for (int row = 0; placed < count; row++)
            {
                int inRow = Math.Min(bottom - row, count - placed);
                if (inRow <= 0) break;
                float y = Ground + 0.15f + half.Y + row * (half.Y * 2f + 0.01f);
                for (int k = 0; k < inRow; k++, placed++)
                {
                    float x = (k - (inRow - 1) * 0.5f) * (half.X * 2f + 0.12f);
                    Vector3 h = half * random.Range(0.94f, 1.04f);
                    Vector3 centre = new Vector3(x, y, random.Range(-0.05f, 0.05f));
                    float yaw = random.Range(-0.04f, 0.04f);
                    Kit.Block(b, centre, h, yaw, 0.025f, Dressed(stone, random, y));
                    if (random.Chance(0.7f)) Daub(b, centre, h, yaw, random);
                }
            }
        }

        /// <summary>
        /// A treadwheel crane: a mast braced on a cross of beams, its jib reaching out along +z with a pulley at its end, the great wheel at its foot that men walk round inside to wind the rope,
        /// and a block slung from the jib by chains. Numbers: height, stone.
        /// </summary>
        public static void Crane(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float height = Math.Clamp(spec.Metres(0), 5f, 11f);
            Vector3 stone = StoneOfId(spec, 1);
            VertexMaterial wood = Timber(random), dark = Timber(random, 0.75f), iron = Iron(random), rope = Rope(random);
            // The cross of beams it stands on, and the mast braced from each arm.
            const float Arm = 2.6f;
            Kit.Plank(b, new Vector3(-Arm, Ground + 0.15f, 0f), new Vector3(Arm, Ground + 0.15f, 0f), Vector3.UnitY, 0.35f, 0.3f, dark);
            Kit.Plank(b, new Vector3(0f, Ground + 0.15f, -Arm), new Vector3(0f, Ground + 0.15f, Arm), Vector3.UnitY, 0.35f, 0.3f, dark);
            Vector3 top = new Vector3(0f, height, 0f);
            Kit.Plank(b, new Vector3(0f, Ground, 0f), top, Vector3.UnitX, 0.38f, 0.38f, wood);
            foreach (Vector3 arm in new[] { Vector3.UnitX, -Vector3.UnitX, Vector3.UnitZ, -Vector3.UnitZ })
            {
                Kit.Plank(b, arm * (Arm - 0.2f) + Vector3.UnitY * 0.3f, new Vector3(0f, height * 0.4f, 0f), Vector3.Cross(arm, Vector3.UnitY), 0.18f, 0.18f, wood);
            }
            // The jib, out along +z and up a little, with its stay back to the mast's head.
            Vector3 heel = new Vector3(0f, height * 0.55f, 0.2f), end = new Vector3(0f, height * 0.92f, height * 0.62f);
            Kit.Plank(b, heel, end, Vector3.UnitX, 0.24f, 0.24f, wood);
            Kit.Pole(b, top, end, 0.05f, rope);
            Kit.Lathe(b, end + new Vector3(-0.12f, 0f, 0f), Vector3.UnitX, 0.24f, Kit.Profile((0f, 0.3f), (1f, 0.3f)), 10, 1, iron);
            Kit.Lathe(b, top + new Vector3(-0.12f, 0.1f, 0f), Vector3.UnitX, 0.24f, Kit.Profile((0f, 0.25f), (1f, 0.25f)), 10, 1, iron);
            // The wheel beside the mast, men's height and more across, its two rims, its spokes and the slats they walk on.
            const float Wheel = 1.9f, Wide = 1.3f;
            Vector3 hub = new Vector3(2.1f, Wheel + 0.2f, 0f);
            const int Rim = 18;
            foreach (float x in new[] { hub.X - Wide * 0.5f, hub.X + Wide * 0.5f })
            {
                for (int k = 0; k < Rim; k++)
                {
                    float a0 = k * MathF.Tau / Rim, a1 = (k + 1) * MathF.Tau / Rim;
                    Kit.Pole(b, new Vector3(x, hub.Y + MathF.Cos(a0) * Wheel, MathF.Sin(a0) * Wheel), new Vector3(x, hub.Y + MathF.Cos(a1) * Wheel, MathF.Sin(a1) * Wheel), 0.08f, wood, capped: false);
                    if (k % 3 == 0) Kit.Pole(b, new Vector3(x, hub.Y, 0f), new Vector3(x, hub.Y + MathF.Cos(a0) * Wheel, MathF.Sin(a0) * Wheel), 0.05f, wood);
                }
            }
            for (int k = 0; k < Rim; k += 2)
            {
                float a = k * MathF.Tau / Rim;
                Vector3 p = new Vector3(0f, hub.Y + MathF.Cos(a) * (Wheel - 0.05f), MathF.Sin(a) * (Wheel - 0.05f));
                Kit.Plank(b, p + Vector3.UnitX * (hub.X - Wide * 0.5f), p + Vector3.UnitX * (hub.X + Wide * 0.5f), new Vector3(0f, MathF.Cos(a), MathF.Sin(a)), 0.16f, 0.05f, wood);
            }
            Kit.Pole(b, new Vector3(0.2f, hub.Y, 0f), hub + Vector3.UnitX * (Wide * 0.5f + 0.25f), 0.09f, iron);
            // Its frame to the ground, either side.
            foreach (float x in new[] { hub.X - Wide * 0.5f - 0.2f, hub.X + Wide * 0.5f + 0.2f })
            {
                Kit.Plank(b, new Vector3(x, Ground, -1.1f), new Vector3(x, hub.Y, 0f), Vector3.UnitX, 0.16f, 0.16f, dark);
                Kit.Plank(b, new Vector3(x, Ground, 1.1f), new Vector3(x, hub.Y, 0f), Vector3.UnitX, 0.16f, 0.16f, dark);
            }
            // The rope from the wheel's axle up the mast, over its head, out along the jib and down to the block.
            Kit.Pole(b, new Vector3(0.3f, hub.Y, 0f), top + new Vector3(0f, 0.1f, -0.25f), 0.035f, rope);
            Kit.Pole(b, top + new Vector3(0f, 0.1f, 0.25f), end + new Vector3(0f, 0.3f, 0f), 0.035f, rope);
            float hang = random.Range(1.6f, height * 0.45f);
            Vector3 hook = new Vector3(0f, hang + 1.2f, end.Z);
            Kit.Pole(b, end - new Vector3(0f, 0.3f, 0f), hook, 0.035f, rope);
            Vector3 blockHalf = new Vector3(random.Range(0.55f, 0.75f), random.Range(0.35f, 0.45f), random.Range(0.45f, 0.6f));
            Vector3 block = new Vector3(0f, hang, end.Z);
            Kit.Block(b, block, blockHalf, random.Range(-0.4f, 0.4f), 0.03f, Dressed(stone, random, hang));
            Kit.Chain(b, hook, block + new Vector3(-blockHalf.X * 0.8f, blockHalf.Y, 0f), 0.05f, 0.12f, iron);
            Kit.Chain(b, hook, block + new Vector3(blockHalf.X * 0.8f, blockHalf.Y, 0f), 0.05f, 0.12f, iron);
        }

        /// <summary>A block roped on a sledge, the sledge on rollers, the ropes it's hauled by run out along +z to where the hands were. Numbers: stone.</summary>
        public static void Sledge(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            Vector3 stone = StoneOfId(spec, 0);
            VertexMaterial wood = Timber(random), worn = Weathered(random), rope = Rope(random);
            // The rollers under it, logs crossways.
            foreach (float z in new[] { -1.3f, 0f, 1.3f })
            {
                float r = random.Range(0.13f, 0.17f);
                Kit.Pole(b, new Vector3(-1f, Ground + r - 0.04f, z + random.Range(-0.15f, 0.15f)), new Vector3(1f, Ground + r - 0.04f, z + random.Range(-0.15f, 0.15f)), r, worn);
            }
            // Its runners, curling up at the front, and the crosspieces over them.
            foreach (float x in new[] { -0.65f, 0.65f })
            {
                var runner = new List<Vector3>();
                for (int k = 0; k <= 6; k++)
                {
                    float t = k / 6f;
                    float z = -1.9f + t * 3.9f;
                    float y = Ground + 0.42f + (t > 0.8f ? MathF.Pow((t - 0.8f) / 0.2f, 2f) * 0.35f : 0f);
                    runner.Add(new Vector3(x, y, z));
                }
                Kit.Bend(b, runner, 0.1f, 0.08f, wood);
            }
            for (float z = -1.6f; z <= 1.6f; z += 0.53f) Kit.Plank(b, new Vector3(-0.85f, Ground + 0.55f, z), new Vector3(0.85f, Ground + 0.55f, z), Vector3.UnitY, 0.18f, 0.08f, wood);
            // The block, and the ropes round it and out to haul by.
            Vector3 half = new Vector3(random.Range(0.6f, 0.8f), random.Range(0.45f, 0.6f), random.Range(0.9f, 1.3f));
            Vector3 centre = new Vector3(0f, Ground + 0.6f + half.Y, 0f);
            Kit.Block(b, centre, half, random.Range(-0.05f, 0.05f), 0.04f, CutStone(stone, 1f));
            Daub(b, centre, half, 0f, random);
            foreach (float z in new[] { -half.Z * 0.5f, half.Z * 0.5f })
            {
                Kit.Pole(b, new Vector3(-half.X - 0.02f, centre.Y + half.Y + 0.02f, z), new Vector3(half.X + 0.02f, centre.Y + half.Y + 0.02f, z), 0.03f, rope);
                Kit.Pole(b, new Vector3(-half.X - 0.02f, centre.Y + half.Y, z), new Vector3(-half.X - 0.02f, Ground + 0.6f, z), 0.03f, rope);
                Kit.Pole(b, new Vector3(half.X + 0.02f, centre.Y + half.Y, z), new Vector3(half.X + 0.02f, Ground + 0.6f, z), 0.03f, rope);
            }
            foreach (float x in new[] { -0.55f, 0.55f })
            {
                Vector3 from = new Vector3(x, Ground + 0.75f, 2.1f);
                Kit.Rope(b, from, new Vector3(x * 1.8f + random.Range(-0.3f, 0.3f), Ground + 0.05f, 3.8f), 0.1f, 0.035f, rope, 5);
            }
        }

        /// <summary>A mason's banker, a heavy bench at a man's waist with a block half dressed on it, the mallet and chisels by it, and the chips heaped round its foot. Numbers: stone.</summary>
        public static void Banker(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            Vector3 stone = StoneOfId(spec, 0);
            VertexMaterial wood = Timber(random, 0.85f), iron = LandIron(random), handle = Timber(random, 1.15f);
            const float Top = 0.8f;
            Kit.Span(b, new Vector3(-1f, Top - 0.18f, -0.5f), new Vector3(1f, Top, 0.5f), 0.02f, wood);
            foreach (float x in new[] { -0.8f, 0.8f })
            {
                foreach (float z in new[] { -0.38f, 0.38f }) Kit.Span(b, new Vector3(x - 0.09f, -0.05f, z - 0.09f), new Vector3(x + 0.09f, Top - 0.18f, z + 0.09f), 0.01f, wood);
            }
            // The block: one face dressed, the rest still rough, which is two blocks of the same stone a hair apart in finish.
            Vector3 half = new Vector3(random.Range(0.35f, 0.5f), random.Range(0.25f, 0.33f), random.Range(0.28f, 0.36f));
            Vector3 centre = new Vector3(random.Range(-0.3f, 0.3f), Top + half.Y, 0f);
            Kit.Block(b, centre, half, random.Range(-0.2f, 0.2f), 0.02f, f => f.Y > 0.5f || f.Z > 0.5f ? Dressed(stone, random, 1f) : CutStone(stone, 1f));
            // The mallet and two chisels on the bench.
            Vector3 mallet = new Vector3(0.7f, Top + 0.08f, 0.2f);
            Kit.Lathe(b, mallet, Vector3.UnitZ, 0.18f, Kit.Profile((0f, 0.08f), (1f, 0.08f)), 8, 1, Timber(random, 0.7f));
            Kit.Pole(b, mallet + new Vector3(0f, 0f, 0.09f), mallet + new Vector3(-0.3f, 0f, 0.09f), 0.02f, handle);
            for (int k = 0; k < 2; k++) Kit.Pole(b, new Vector3(-0.75f + k * 0.12f, Top + 0.02f, -0.3f), new Vector3(-0.75f + k * 0.12f, Top + 0.02f, 0.02f), 0.012f, iron);
            // Chips heaped round its foot, pale and fresh.
            Kit.Heap(b, new Vector3(0f, Ground - 0.12f, 0f), new Vector2(1.8f, 1.3f), 0.22f, random.Float(), CutStone(stone * 1.05f, 0.2f), random, 3, 12);
        }

        /// <summary>A heap of spalls and chips tipped from the dressing, chunks of the stone standing out of it. Numbers: radius, stone.</summary>
        public static void Rubble(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float radius = Math.Clamp(spec.Metres(0), 1f, 6f);
            Vector3 stone = StoneOfId(spec, 1);
            Kit.Heap(b, new Vector3(0f, Ground - 0.2f, 0f), new Vector2(radius, radius * random.Range(0.75f, 1f)), radius * 0.42f, random.Float() * MathF.Tau, CutStone(stone * 0.85f, 0.4f), random, 4, 14);
            int chunks = (int)(radius * 3f);
            for (int k = 0; k < chunks; k++)
            {
                float a = random.Float() * MathF.Tau, d = MathF.Sqrt(random.Float()) * radius * 0.8f;
                float h = radius * 0.42f * (1f - (d / radius) * (d / radius)) - 0.1f;
                float r = random.Range(0.15f, 0.4f);
                Kit.Block(b, new Vector3(MathF.Cos(a) * d, Ground + h, MathF.Sin(a) * d), new Vector3(r, r * 0.55f, r * 0.75f), random.Float() * MathF.Tau, 0.04f, CutStone(stone * random.Range(0.88f, 1.02f), h));
            }
        }

        /// <summary>A block on the floor with the iron wedges driven in a row along the line it'll split on, the line drilled down its face and a sledgehammer leaning on it. Numbers: length, stone.</summary>
        public static void SplitLine(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float length = Math.Clamp(spec.Metres(0), 1.5f, 5f);
            Vector3 stone = StoneOfId(spec, 1);
            VertexMaterial iron = Iron(random), handle = Timber(random, 1.1f);
            Vector3 half = new Vector3(length * 0.5f, random.Range(0.55f, 0.8f), random.Range(0.6f, 0.85f));
            Vector3 centre = new Vector3(0f, half.Y - 0.05f, 0f);
            Kit.Block(b, centre, half, 0f, 0.06f, CutStone(stone, 1f));
            float split = random.Range(-0.15f, 0.15f) * half.Z;
            // The line, a dark seam across its top and down its face, where the drill went.
            Kit.Face(b, new Vector3(-half.X * 0.97f, half.Y * 2f - 0.045f, split - 0.02f), new Vector3(half.X * 0.97f, half.Y * 2f - 0.045f, split - 0.02f), new Vector3(half.X * 0.97f, half.Y * 2f - 0.045f, split + 0.02f), new Vector3(-half.X * 0.97f, half.Y * 2f - 0.045f, split + 0.02f), Vector3.UnitY, Dark);
            int wedges = (int)(length / 0.32f);
            for (int k = 0; k < wedges; k++)
            {
                float x = -half.X + (k + 0.5f) * (length / wedges);
                Kit.Block(b, new Vector3(x, half.Y * 2f - 0.05f + 0.07f, split), new Vector3(0.025f, 0.08f, 0.05f), 0f, 0.005f, iron);
            }
            // The hammer, leaning against it.
            Vector3 foot = new Vector3(half.X * 0.4f, Ground, half.Z + 0.7f);
            Vector3 head = new Vector3(half.X * 0.4f, half.Y * 1.5f, half.Z + 0.05f);
            Kit.Pole(b, foot, head, 0.025f, handle);
            Kit.Block(b, foot + new Vector3(0f, 0.06f, 0.02f), new Vector3(0.12f, 0.06f, 0.06f), 0f, 0.01f, iron);
        }
    }
}

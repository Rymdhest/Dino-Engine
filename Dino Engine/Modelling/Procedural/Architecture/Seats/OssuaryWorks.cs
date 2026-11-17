using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture.Places;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.Modelling.Procedural.Architecture.Places.PlaceMaterials;

namespace Dino_Engine.Modelling.Procedural.Architecture.Seats
{
    /// <summary>
    /// What the Ossuary's three seats are built of (SEATS.md), each the place its kind of tyrant was made. The boneyard: the slab of the land's rock a bone tyrant was raised out of, the
    /// hollow it left still in it and the tackle it came up on still over it; a fence of great bones, and arches of ribs at its ways in. The barrow: one of the land's great barrows cut
    /// open, its portal of standing stones laid bare. The bile works: the vats a rotting tyrant is kept going from, and the frame it's hooked into. The Ossuary's lights are its own: the
    /// cold green it raises bones with, the blue a ghost is called by, and the sick yellow-green of its bile. Model +z is a piece's front. Headless.
    /// </summary>
    internal static class OssuaryWorks
    {
        // ---------------------------------------------------------------- The boneyard

        /// <summary>How high the front edge of the slab stands, which is what makes the whole of it something walked round.</summary>
        public const float SlabLip = 1.5f;

        /// <summary>
        /// The slab a bone tyrant was raised out of, its face tilted up to the north and so to the camera: a bed of the land's rock cut clean, and in it the hollow the beast left, spine,
        /// ribs, hips, legs and skull, worked round with chisels and still faintly lit where its eyes and its heart were; over it the sheerlegs and the beam it came up on, their chains
        /// hanging slack and a shackle open; ladders up its side, and its edges broken as the rock breaks.
        /// </summary>
        public static void Matrix(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            float w = Math.Clamp(spec.Metres(0), 14f, 40f), d = Math.Clamp(spec.Metres(1), 8f, 22f), rise = Math.Clamp(spec.Metres(2), 4f, 12f);
            Vector3 stone = SeatKit.StoneOf(spec, 3);
            float hw = w * 0.5f, hd = d * 0.5f;
            VertexMaterial face = CutStone(stone * 0.92f, 3f), rough = SeatKit.Ashlar(stone, random, 2f, 0.8f), dark = Flat(stone * 0.2f), worked = CutStone(stone * 1.12f, 4f);
            Vector3 origin = new Vector3(0f, SlabLip, hd);
            Vector3 up = new Vector3(0f, rise - SlabLip, -d);
            float slope = up.Length;
            up /= slope;
            Vector3 normal = Vector3.Cross(Vector3.UnitX, up).Normalized();
            if (normal.Y < 0f) normal = -normal;
            Vector3 At(float x, float s, float lift = 0f) => origin + Vector3.UnitX * x + up * s + normal * lift;

            // The bed itself, its front lip, its sides and its back, down into the ground.
            Kit.Face(b, At(-hw, 0f), At(hw, 0f), At(hw, slope), At(-hw, slope), normal, face);
            Kit.Face(b, new Vector3(-hw, -0.6f, hd), new Vector3(hw, -0.6f, hd), At(hw, 0f), At(-hw, 0f), Vector3.UnitZ, rough);
            Kit.Face(b, new Vector3(-hw, -0.6f, -hd), new Vector3(hw, -0.6f, -hd), At(hw, slope), At(-hw, slope), -Vector3.UnitZ, rough);
            foreach (float s in new[] { -1f, 1f }) Kit.Face(b, new Vector3(s * hw, -0.6f, hd), At(s * hw, 0f), At(s * hw, slope), new Vector3(s * hw, -0.6f, -hd), new Vector3(s, 0f, 0f), rough);
            // Its edges, broken off in blocks, so that it isn't a ramp.
            for (int i = 0; i < 14; i++)
            {
                bool top = i < 6;
                float x = top ? -hw + w * (i + random.Range(0.2f, 0.8f)) / 6f : (i % 2 == 0 ? -1f : 1f) * (hw + random.Range(-0.3f, 0.5f));
                float s = top ? slope + random.Range(-0.4f, 0.3f) : slope * random.Range(0.1f, 0.95f);
                Vector3 half = new Vector3(random.Range(0.7f, 1.5f), random.Range(0.4f, 0.9f), random.Range(0.6f, 1.2f));
                Kit.Block(b, At(x, s, half.Y * 0.3f), half, random.Float() * MathF.Tau, 0.08f, SeatKit.AshlarFacing(stone, random, 3f, 0.86f));
            }

            // The hollow: a tyrant on its side as the rock kept it, drawn in the dark of what was cut out, each part with the paler margin the chisels worked round it.
            float length = w * 0.84f;
            void Strip(Vector2 a, Vector2 c, float width, bool margin = true)
            {
                Vector2 along = c - a;
                float l = along.Length;
                if (l < 0.02f) return;
                along /= l;
                Vector2 across = new Vector2(-along.Y, along.X) * (width * 0.5f);
                if (margin)
                {
                    Vector2 wide = across * ((width + 0.5f) / width), reach = along * 0.25f;
                    Kit.Face(b, At(a.X - reach.X - wide.X, a.Y - reach.Y - wide.Y, 0.025f), At(c.X + reach.X - wide.X, c.Y + reach.Y - wide.Y, 0.025f), At(c.X + reach.X + wide.X, c.Y + reach.Y + wide.Y, 0.025f), At(a.X - reach.X + wide.X, a.Y - reach.Y + wide.Y, 0.025f), normal, worked);
                }
                Kit.Face(b, At(a.X - across.X, a.Y - across.Y, 0.05f), At(c.X - across.X, c.Y - across.Y, 0.05f), At(c.X + across.X, c.Y + across.Y, 0.05f), At(a.X + across.X, a.Y + across.Y, 0.05f), normal, dark);
            }
            // The spine, from the tip of the tail to the back of the skull: the tail arched away, the neck thrown back over the body, as a beast's is when it dies and dries.
            Vector2[] spine =
            {
                new Vector2(-0.48f * length, 0.72f * slope), new Vector2(-0.38f * length, 0.56f * slope), new Vector2(-0.26f * length, 0.45f * slope), new Vector2(-0.12f * length, 0.4f * slope),
                new Vector2(0.03f * length, 0.42f * slope), new Vector2(0.16f * length, 0.48f * slope), new Vector2(0.26f * length, 0.6f * slope), new Vector2(0.3f * length, 0.74f * slope),
                new Vector2(0.25f * length, 0.86f * slope),
            };
            for (int i = 1; i < spine.Length; i++)
            {
                float thick = i <= 2 ? 0.3f + 0.1f * i : i >= 6 ? 0.42f : 0.6f;
                Strip(spine[i - 1], spine[i], thick);
            }
            // The ribs, off the body's length of it, curling in under where the belly was.
            for (int k = 0; k < 9; k++)
            {
                float t = (k + 0.5f) / 9f;
                Vector2 root = Vector2.Lerp(spine[3], spine[5], t) + (t > 0.5f ? (spine[5] - spine[4]) * 0f : Vector2.Zero);
                float reach = (1.5f + 1.3f * MathF.Sin(MathF.PI * (0.15f + 0.8f * t))) * length / 20f;
                Vector2 mid = root + new Vector2(0.12f * reach, -0.62f * reach), tip = root + new Vector2(0.5f * reach, -reach);
                Strip(root, mid, 0.2f, false);
                Strip(mid, tip, 0.16f, false);
            }
            // The hips and the two legs folded under them, and the little arms.
            Vector2 hips = spine[3];
            Strip(hips + new Vector2(-0.5f, -0.2f), hips + new Vector2(0.9f, -0.5f), 0.9f);
            foreach (float off in new[] { 0f, 0.7f })
            {
                float scale = length / 20f;
                Vector2 knee = hips + new Vector2(1.8f + off, -2.2f) * scale, ankle = knee + new Vector2(-2f, -1.4f) * scale, toe = ankle + new Vector2(1.5f, -0.5f) * scale;
                Strip(hips + new Vector2(0.3f, -0.4f), knee, 0.42f);
                Strip(knee, ankle, 0.32f);
                Strip(ankle, toe, 0.24f);
            }
            Vector2 shoulder = spine[5];
            Strip(shoulder + new Vector2(0.1f, -0.3f), shoulder + new Vector2(0.9f, -1.1f) * (length / 20f), 0.2f, false);
            // The skull, looking back the way the neck threw it, its jaw open.
            Vector2 skull = spine[^1];
            float sl = length * 0.16f;
            Vector2 snout = skull + new Vector2(-sl, 0.06f * sl);
            Strip(skull, snout, sl * 0.42f);
            Strip(skull + new Vector2(-0.1f * sl, -0.3f * sl), snout + new Vector2(0.1f * sl, -0.5f * sl), sl * 0.16f);
            // Where its eye and its heart were, the light it was raised by is still in the rock.
            VertexMaterial glow = OssuaryGlow(0.9f);
            Vector2 eye = skull + new Vector2(-0.3f * sl, 0.05f * sl), heart = Vector2.Lerp(spine[4], spine[5], 0.5f) + new Vector2(0.2f, -0.9f);
            foreach ((Vector2 at, float size) in new[] { (eye, 0.2f), (heart, 0.34f) })
            {
                // A stain of it, round, and veins of it running out along the cracks.
                const int Sides = 9;
                for (int k = 0; k < Sides; k++)
                {
                    float a0 = k * MathF.Tau / Sides, a1 = (k + 1) * MathF.Tau / Sides;
                    float r0 = size * (0.8f + 0.4f * ((k * 7) % 3) / 2f), r1 = size * (0.8f + 0.4f * (((k + 1) % Sides * 7) % 3) / 2f);
                    Kit.Face(b, At(at.X, at.Y, 0.08f), At(at.X + MathF.Cos(a0) * r0, at.Y + MathF.Sin(a0) * r0, 0.08f), At(at.X + MathF.Cos(a1) * r1, at.Y + MathF.Sin(a1) * r1, 0.08f), normal, glow);
                }
                for (int k = 0; k < 3; k++)
                {
                    float a = random.Float() * MathF.Tau, l = size * random.Range(2.5f, 4.5f);
                    Vector2 d0 = new Vector2(MathF.Cos(a), MathF.Sin(a)), side0 = new Vector2(-d0.Y, d0.X) * 0.035f;
                    Kit.Face(b, At(at.X - side0.X, at.Y - side0.Y, 0.08f), At(at.X + d0.X * l, at.Y + d0.Y * l, 0.08f), At(at.X + side0.X, at.Y + side0.Y, 0.08f), normal, glow);
                }
                lights.Add(Cold(At(at.X, at.Y, 1f), 30f, 9f));
            }

            // The tackle it came up on: sheerlegs either side of it, a beam between their heads, and the chains hanging slack over the hollow, one with its shackle open.
            VertexMaterial timber = Timber(random, 0.78f), iron = Iron(random), rust = Rusted(random);
            float span = w * 0.34f, apex = rise + 6.5f;
            Vector3 beamAt = new Vector3(0f, apex, -hd * 0.15f);
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 head = beamAt + new Vector3(s * span, 0f, 0f);
                Kit.Pole(b, new Vector3(s * (span + 1.6f), 0f, hd + 2.2f), head, 0.2f, timber, 0.15f);
                Kit.Pole(b, new Vector3(s * (span + 1.2f), rise - 0.5f, -hd - 0.4f), head, 0.2f, timber, 0.15f);
                Kit.Pole(b, new Vector3(s * (span + 4.5f), 0f, 0f), head, 0.17f, timber, 0.13f);
                b.Tube(head - Vector3.UnitY * 0.5f, head + Vector3.UnitY * 0.1f, 0.3f, 0.3f, 8, Rope(random), capped: false);
            }
            Kit.Plank(b, beamAt - Vector3.UnitX * (span + 0.6f), beamAt + Vector3.UnitX * (span + 0.6f), Vector3.UnitY, 0.4f, 0.4f, timber, 0.03f);
            for (int k = 0; k < 4; k++)
            {
                float x = -span * 0.75f + span * 1.5f * k / 3f;
                Vector3 top = beamAt + new Vector3(x, -0.2f, 0f);
                float s = slope * (0.42f + 0.1f * (k % 2));
                Vector3 end = At(x * 0.9f, s, 1.2f + random.Range(0f, 1.6f));
                Kit.Chain(b, top, end, 0.5f, 0.42f, k == 1 ? rust : iron);
                Kit.Lathe(b, end - Vector3.UnitY * 0.5f, Vector3.UnitY, 0.5f, Kit.Profile((0f, 0.05f), (0.5f, 0.34f), (1f, 0.2f)), 6, 2, iron, openEnd: true);
            }
            // Ladders up its side, and the Ossuary's cold lamps at its foot.
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 foot = new Vector3(s * (hw * 0.55f), 0f, hd + 1.6f), head = At(s * hw * 0.55f, 1.2f, 0.3f);
                Vector3 side = new Vector3(0.38f, 0f, 0f);
                Kit.Pole(b, foot + side, head + side, 0.05f, timber);
                Kit.Pole(b, foot - side, head - side, 0.05f, timber);
                for (float t = 0.12f; t < 1f; t += 0.14f) Kit.Pole(b, Vector3.Lerp(foot, head, t) + side, Vector3.Lerp(foot, head, t) - side, 0.03f, timber);
            }
        }

        /// <summary>
        /// A stretch of the Ossuary's fence along x: great bones stood in the ground side by side and lashed, long ones and short, a rib here and there curving out over the top towards
        /// -z, and a beast's skull hung on it.
        /// </summary>
        public static void BonePale(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float length = Math.Clamp(spec.Metres(0), 3f, 16f), height = Math.Clamp(spec.Metres(1), 2f, 6f);
            float hl = length * 0.5f;
            VertexMaterial rope = Rope(random, 0.8f);
            int count = Math.Max(3, (int)(length / 0.85f));
            for (int i = 0; i <= count; i++)
            {
                float x = -hl + length * i / count + random.Range(-0.08f, 0.08f);
                float tall = height * random.Range(0.72f, 1.05f) * (i % 3 == 0 ? 1.12f : 1f);
                VertexMaterial bone = Bone(random, random.Range(0.86f, 1.02f));
                Vector3 lean = new Vector3(random.Range(-0.12f, 0.12f), 1f, random.Range(-0.1f, 0.1f)).Normalized();
                Vector3 foot = new Vector3(x, -0.4f, random.Range(-0.08f, 0.08f)), head = foot + lean * (tall + 0.4f);
                if (i % 4 == 2)
                {
                    // A rib, curving out over the top.
                    var rib = new List<Vector3> { foot, foot + lean * (tall * 0.55f), foot + lean * (tall * 0.95f) - Vector3.UnitZ * 0.35f, foot + lean * (tall * 1.12f) - Vector3.UnitZ * 1.1f, foot + lean * (tall * 1.05f) - Vector3.UnitZ * 1.7f };
                    Kit.Bend(b, rib, 0.13f, 0.04f, bone);
                    continue;
                }
                Kit.Pole(b, foot, head, 0.12f, bone, 0.1f);
                Kit.Lathe(b, head - lean * 0.1f, lean, 0.34f, Kit.Profile((0f, 0.1f), (0.4f, 0.2f), (1f, 0.13f)), 6, 2, bone);
            }
            foreach (float y in new[] { height * 0.3f, height * 0.62f })
            {
                Kit.Rope(b, new Vector3(-hl, y, 0.14f), new Vector3(hl, y + random.Range(-0.15f, 0.15f), 0.14f), 0.12f, 0.05f, rope, 5);
            }
            // A skull hung on it, looking in.
            VertexMaterial skull = Bone(random, 1.02f);
            Vector3 at = new Vector3(random.Range(-hl * 0.5f, hl * 0.5f), height * 0.72f, 0.3f);
            Kit.Lathe(b, at, new Vector3(0f, -0.25f, 1f), 1.1f, Kit.Profile((0f, 0.2f), (0.3f, 0.36f), (0.7f, 0.26f), (1f, 0.1f)), 7, 4, skull);
            Kit.Face(b, at + new Vector3(-0.28f, 0.14f, 0.3f), at + new Vector3(-0.1f, 0.14f, 0.34f), at + new Vector3(-0.1f, 0.3f, 0.3f), at + new Vector3(-0.28f, 0.3f, 0.26f), new Vector3(-0.3f, 1f, 0.3f), Dark);
        }

        /// <summary>
        /// The way into the Ossuary's ground, across x: two of a great beast's ribs stood either side of it and crossed over it, bound where they cross, one of its cold lamps hung there,
        /// and skulls heaped at their feet.
        /// </summary>
        public static void RibArch(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            float span = Math.Clamp(spec.Metres(0), 5f, 14f), height = Math.Clamp(spec.Metres(1), 5f, 14f);
            float hs = span * 0.5f;
            VertexMaterial bone = Bone(random, 0.98f), iron = Iron(random);
            foreach (float s in new[] { -1f, 1f })
            {
                foreach (float z in new[] { -0.45f, 0.45f })
                {
                    var rib = new List<Vector3>();
                    const int Steps = 8;
                    for (int i = 0; i <= Steps; i++)
                    {
                        float t = i / (float)Steps;
                        float x = s * hs * (1f - 1.12f * MathF.Pow(t, 1.7f));
                        rib.Add(new Vector3(x, -0.5f + (height + 0.5f) * MathF.Sin(t * MathF.PI * 0.5f), z * (1f - 0.6f * t)));
                    }
                    Kit.Bend(b, rib, 0.42f, 0.12f, bone);
                }
                // Skulls at its foot.
                for (int k = 0; k < 4; k++)
                {
                    Vector3 at = new Vector3(s * (hs + random.Range(0.4f, 1.3f)), Ground + 0.2f + (k == 3 ? 0.4f : 0f), random.Range(-1f, 1f));
                    float a = random.Float() * MathF.Tau;
                    Kit.Lathe(b, at, new Vector3(MathF.Cos(a), 0.1f, MathF.Sin(a)), 0.9f, Kit.Profile((0f, 0.16f), (0.3f, 0.3f), (0.7f, 0.22f), (1f, 0.08f)), 6, 4, Bone(random, 0.94f));
                }
            }
            b.Tube(new Vector3(0f, height - 0.9f, 0f), new Vector3(0f, height - 0.3f, 0f), 0.5f, 0.42f, 8, iron, capped: false);
            Kit.Chain(b, new Vector3(0f, height - 0.9f, 0f), new Vector3(0f, height - 2.3f, 0f), 0f, 0.2f, iron);
            Vector3 lamp = new Vector3(0f, height - 2.6f, 0f);
            Kit.Lathe(b, lamp + Vector3.UnitY * 0.28f, Vector3.UnitY, 0.2f, Kit.Profile((0f, 0.3f), (1f, 0.03f)), 4, 1, iron);
            Kit.Ball(b, lamp, new Vector3(0.18f, 0.28f, 0.18f), 6, 4, OssuaryGlow(2f));
            lights.Add(Cold(lamp, 46f, 13f) with { Flicker = 0.08f, FlickerSpeed = 1.1f });
        }

        // ---------------------------------------------------------------- The barrow

        /// <summary>Half of how wide the cutting into a great barrow of a length is.</summary>
        public static float CuttingOf(float length) => MathF.Min(length * 0.1f, 4.6f);

        /// <summary>
        /// One of the land's great barrows, along x, cut open in the side towards +z: a long mound under turf with a kerb of the land's stones round its foot, and in the cutting the
        /// portal it was closed with, two stones and the one across them, the boards the Ossuary shored the cutting with and the earth it threw out either side.
        /// </summary>
        public static void GreatBarrow(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            float w = Math.Clamp(spec.Metres(0), 16f, 52f), d = Math.Clamp(spec.Metres(1), 10f, 26f), high = Math.Clamp(spec.Metres(2), 3f, 10f);
            Vector3 stone = SeatKit.StoneOf(spec, 3);
            float hw = w * 0.5f, hd = d * 0.5f, cut = CuttingOf(w);
            VertexMaterial turf = Turf(0.95f), soil = Soil(0.9f);
            // The mound, as two shoulders either side of the cutting and the crown behind it.
            foreach (float s in new[] { -1f, 1f }) Kit.Heap(b, new Vector3(s * (cut + (hw - cut) * 0.52f), -0.2f, 0f), new Vector2((hw - cut) * 0.6f + 1.2f, hd), high, 0f, turf, random, 6, 18);
            Kit.Heap(b, new Vector3(0f, -0.2f, -hd * 0.3f), new Vector2(cut + 4f, hd * 0.72f), high * 1.04f, 0f, turf, random, 6, 16);
            // Its kerb.
            int stones = (int)(MathF.PI * (hw + hd) / 2.1f);
            for (int i = 0; i < stones; i++)
            {
                float a = i * MathF.Tau / stones + random.Range(-0.03f, 0.03f);
                Vector3 at = new Vector3(MathF.Cos(a) * (hw + 0.4f), 0f, MathF.Sin(a) * (hd + 0.4f));
                if (MathF.Abs(at.X) < cut + 1.5f && at.Z > 0f) continue;
                Vector3 half = new Vector3(random.Range(0.5f, 0.95f), random.Range(0.5f, 1.05f), random.Range(0.4f, 0.7f));
                Kit.Block(b, at + Vector3.UnitY * (half.Y * 0.7f), half, -a + MathF.PI * 0.5f + random.Range(-0.2f, 0.2f), 0.1f, SeatKit.AshlarFacing(stone, random, 0.6f, 0.82f));
            }
            // The cutting: its floor, the boards holding its sides, and at its head the portal.
            float head = hd * 0.2f;
            Kit.Face(b, new Vector3(-cut, Ground + 0.03f, head), new Vector3(cut, Ground + 0.03f, head), new Vector3(cut + 1f, Ground + 0.03f, hd + 3f), new Vector3(-cut - 1f, Ground + 0.03f, hd + 3f), Vector3.UnitY, soil);
            VertexMaterial boards = Weathered(random, 0.95f), post = Timber(random, 0.7f);
            foreach (float s in new[] { -1f, 1f })
            {
                float top = high * 0.62f;
                Kit.Face(b, new Vector3(s * cut, 0f, head), new Vector3(s * (cut + 0.5f), 0f, hd), new Vector3(s * (cut + 0.5f), top * 0.35f, hd), new Vector3(s * cut, top, head), new Vector3(-s, 0f, 0f), boards);
                for (int k = 0; k <= 3; k++)
                {
                    float t = k / 3f;
                    Vector3 foot = new Vector3(s * (cut - 0.12f + 0.5f * t), 0f, MathHelper.Lerp(head, hd, t));
                    Kit.Pole(b, foot, foot + Vector3.UnitY * (MathHelper.Lerp(top, top * 0.35f, t) + 0.5f), 0.12f, post);
                }
                // The earth thrown out.
                Kit.Heap(b, new Vector3(s * (cut + 4.5f), Ground, hd + 2.5f), new Vector2(3.4f, 2.2f), 1.1f, s * 0.4f, soil, random, 3, 12);
            }
            float ph = high * 0.78f, pw = cut * 0.72f;
            foreach (float s in new[] { -1f, 1f }) Kit.Block(b, new Vector3(s * pw, ph * 0.5f - 0.3f, head - 0.6f), new Vector3(0.75f, ph * 0.5f + 0.3f, 0.6f), random.Range(-0.08f, 0.08f), 0.12f, SeatKit.AshlarFacing(stone, random, 2f, 0.85f));
            Kit.Block(b, new Vector3(0f, ph + 0.5f, head - 0.6f), new Vector3(pw + 1.3f, 0.55f, 0.85f), random.Range(-0.05f, 0.05f), 0.14f, SeatKit.AshlarFacing(stone, random, 4f, 0.88f));
            Kit.Face(b, new Vector3(-pw + 0.7f, 0f, head - 0.9f), new Vector3(pw - 0.7f, 0f, head - 0.9f), new Vector3(pw - 0.7f, ph, head - 0.9f), new Vector3(-pw + 0.7f, ph, head - 0.9f), Vector3.UnitZ, Dark);
            // The light the Ossuary called by, hung in the portal.
            Vector3 lamp = new Vector3(0f, ph - 0.9f, head - 0.2f);
            Kit.Chain(b, new Vector3(0f, ph, head - 0.3f), lamp + Vector3.UnitY * 0.3f, 0f, 0.16f, Iron(random));
            Kit.Ball(b, lamp, new Vector3(0.2f, 0.3f, 0.2f), 6, 4, Glow(SeatDressing.GhostLight.X, SeatDressing.GhostLight.Y, SeatDressing.GhostLight.Z, 2.2f));
            lights.Add(AssetLight.Glow(lamp + Vector3.UnitZ * 0.6f, SeatDressing.GhostLight, 70f, 16f) with { Flicker = 0.1f, FlickerSpeed = 1.4f });
        }

        // ---------------------------------------------------------------- The bile works

        /// <summary>How far apart a bile works' vats stand, and how far across each is of its height.</summary>
        public const float VatSpacing = 8.2f, VatGirth = 0.36f;

        /// <summary>Where a bile works' chimneys end, which is where its smoke comes from.</summary>
        public static Vector3[] VatChimneys(int count, float height)
        {
            float hx = count * VatSpacing * 0.5f;
            return new[] { new Vector3(-hx + 1.2f, height + 6.5f, -3.4f), new Vector3(hx - 1.2f, height + 7.4f, -3.4f) };
        }

        /// <summary>
        /// The vats a rotting tyrant is kept going from, in a row along x, their front to +z: a firebox of sooty brick with its stoke holes glowing, and on it great jars of glass in
        /// bands of iron, the bile in them lit its sick yellow-green and thick with what's steeping, brass pipes off their heads and out over the yard, and a chimney at each end.
        /// </summary>
        public static void BileVats(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            int count = Math.Clamp(spec.Int(0), 1, 5);
            float height = Math.Clamp(spec.Metres(1), 5f, 13f);
            float hx = count * VatSpacing * 0.5f, radius = height * VatGirth;
            const float Box = 3.2f, Deep = 4.6f;
            VertexMaterial brick = new VertexMaterial(MaterialCatalog.Brick.Index, new Colour(new Vector3(0.3f, 0.26f, 0.24f) * random.Range(0.94f, 1.05f)));
            VertexMaterial iron = Iron(random), brass = Brass(random), plinth = Dressed(new Vector3(0.24f, 0.24f, 0.25f), random, 0.5f);
            Vector3 bile = SeatDressing.BileLight;
            SeatKit.WallBox(b, new Vector3(-hx, -0.6f, -Deep), new Vector3(hx, Box, Deep), brick, plinth);
            Kit.Span(b, new Vector3(-hx - 0.25f, -0.6f, -Deep - 0.25f), new Vector3(hx + 0.25f, 0.7f, Deep + 0.25f), 0.05f, plinth);
            for (int i = 0; i < count; i++)
            {
                float x = -hx + (i + 0.5f) * VatSpacing;
                // Its stoke hole, and the fire in it.
                Kit.Span(b, new Vector3(x - 1.1f, 0.7f, Deep - 0.02f), new Vector3(x + 1.1f, 2.5f, Deep + 0.16f), 0.03f, iron);
                Kit.Face(b, new Vector3(x - 0.8f, 0.9f, Deep + 0.17f), new Vector3(x + 0.8f, 0.9f, Deep + 0.17f), new Vector3(x + 0.8f, 2.2f, Deep + 0.17f), new Vector3(x - 0.8f, 2.2f, Deep + 0.17f), Vector3.UnitZ, Coals);
                // The jar: glass, lit from in it, banded, capped, and what's steeping hanging in it.
                Vector3 foot = new Vector3(x, Box, 0f);
                // Deep and not strong, as glass that glows pale goes white.
                Kit.Lathe(b, foot, Vector3.UnitY, height, Kit.Profile((0f, radius * 0.9f), (0.06f, radius), (0.86f, radius), (1f, radius * 0.72f)), 14, 5, Glow(0.5f, 0.84f, 0.08f, random.Range(0.2f, 0.28f)), openEnd: true);
                foreach (float t in new[] { 0.03f, 0.3f, 0.58f, 0.86f }) b.Tube(foot + Vector3.UnitY * (height * t - 0.14f), foot + Vector3.UnitY * (height * t + 0.14f), radius + 0.05f, radius + 0.05f, 14, iron, capped: false);
                for (int k = 0; k < 6; k++)
                {
                    float a = k * MathF.Tau / 6f;
                    Vector3 r = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a)) * (radius + 0.06f);
                    Kit.Pole(b, foot + r, foot + r + Vector3.UnitY * (height * 0.88f), 0.07f, iron, capped: false);
                }
                Kit.Lathe(b, foot + Vector3.UnitY * height, Vector3.UnitY, radius * 0.6f, Kit.Profile((0f, radius * 0.76f), (0.5f, radius * 0.55f), (1f, 0.3f)), 12, 3, iron);
                for (int k = 0; k < 3; k++)
                {
                    float a = random.Float() * MathF.Tau, r = radius * random.Range(0.2f, 0.6f);
                    Vector3 lump = foot + new Vector3(MathF.Cos(a) * r, height * random.Range(0.2f, 0.7f), radius * 0.86f * MathF.Abs(MathF.Sin(a)) + 0.3f);
                    Kit.Ball(b, new Vector3(lump.X, lump.Y, MathF.Min(lump.Z, radius * 0.99f) + 0.03f), new Vector3(random.Range(0.4f, 0.8f), random.Range(0.6f, 1.2f), 0.1f), 6, 4, Flat(new Vector3(0.1f, 0.12f, 0.03f)));
                }
                // Its pipe, off its head and out over the yard, and the steam off it.
                Vector3 cap = foot + Vector3.UnitY * (height + radius * 0.6f);
                Kit.Bend(b, new[] { cap, cap + Vector3.UnitY * 1.3f, cap + new Vector3(0f, 1.8f, 1.2f), cap + new Vector3(0f, 1.6f, Deep + 2.5f), cap + new Vector3(0f, -1.2f, Deep + 4.4f) }, 0.24f, 0.2f, brass);
                lights.Add(AssetLight.Glow(foot + new Vector3(0f, height * 0.5f, radius + 1.2f), bile, 62f, 17f) with { Flicker = 0.1f, FlickerSpeed = 0.8f });
                lights.Add(AssetLight.Emitting(cap + Vector3.UnitY * 1.4f, "Smithing.Vat", 1.6f));
            }
            // A walk of iron along the front of the jars, and its ladder.
            Kit.Span(b, new Vector3(-hx, Box + 0.02f, radius + 0.5f), new Vector3(hx, Box + 0.14f, Deep + 0.4f), 0f, iron);
            for (float x = -hx; x <= hx + 0.01f; x += count * VatSpacing / (count * 3f)) Kit.Pole(b, new Vector3(x, Box, Deep + 0.34f), new Vector3(x, Box + 1.5f, Deep + 0.34f), 0.04f, iron);
            Kit.Pole(b, new Vector3(-hx, Box + 1.5f, Deep + 0.34f), new Vector3(hx, Box + 1.5f, Deep + 0.34f), 0.045f, iron);
            foreach (Vector3 top in VatChimneys(count, height))
            {
                SeatKit.WallBox(b, new Vector3(top.X - 1f, -0.6f, top.Z - 1f), new Vector3(top.X + 1f, top.Y - 0.5f, top.Z + 1f), brick);
                Kit.Span(b, new Vector3(top.X - 1.15f, top.Y - 0.5f, top.Z - 1.15f), new Vector3(top.X + 1.15f, top.Y - 0.2f, top.Z + 1.15f), 0.03f, iron);
                Kit.Lathe(b, new Vector3(top.X, top.Y - 0.2f, top.Z), Vector3.UnitY, 0.2f, Kit.Profile((0f, 0.7f), (1f, 0.6f)), 8, 1, Flat(new Vector3(0.08f, 0.07f, 0.06f)));
            }
        }

        /// <summary>How far either side of its middle a cradle's legs stand, of its width, and how far apart front to back.</summary>
        public const float CradleSpread = 2.2f;

        /// <summary>
        /// The frame a rotting tyrant is hooked into, across x: two trestles of riveted iron and a girder across them, and hanging from it the brass tubes it's fed by, the collar it's
        /// held in, hooks and chains; a winch on one leg, a grating under it all, and on the grating the bile that's dripped.
        /// </summary>
        public static void Cradle(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            float w = Math.Clamp(spec.Metres(0), 8f, 18f), high = Math.Clamp(spec.Metres(1), 6f, 14f);
            float hw = w * 0.5f;
            VertexMaterial plate = Plating(random), iron = Iron(random), brass = Brass(random), rust = Rusted(random);
            Vector3 bile = SeatDressing.BileLight;
            foreach (float s in new[] { -1f, 1f })
            {
                foreach (float z in new[] { -CradleSpread, CradleSpread })
                {
                    Kit.Plank(b, new Vector3(s * hw, -0.4f, z), new Vector3(s * (hw - 0.6f), high, z * 0.25f), new Vector3(s, 0f, 0f), 0.55f, 0.45f, plate, 0.02f);
                    Kit.Span(b, new Vector3(s * hw - 0.6f, -0.3f, z - 0.6f), new Vector3(s * hw + 0.6f, 0.3f, z + 0.6f), 0.04f, iron);
                }
                Kit.Plank(b, new Vector3(s * (hw - 0.2f), high * 0.45f, -CradleSpread * 0.66f), new Vector3(s * (hw - 0.2f), high * 0.45f, CradleSpread * 0.66f), Vector3.UnitY, 0.3f, 0.25f, iron, 0.02f);
            }
            Kit.Span(b, new Vector3(-hw - 0.4f, high - 0.1f, -0.55f), new Vector3(hw + 0.4f, high + 0.9f, 0.55f), 0.03f, plate);
            foreach (float y in new[] { high - 0.1f, high + 0.9f }) Kit.Span(b, new Vector3(-hw - 0.5f, y - 0.08f, -0.7f), new Vector3(hw + 0.5f, y + 0.08f, 0.7f), 0.01f, iron);
            // What hangs from it.
            for (int k = 0; k < 5; k++)
            {
                float x = -hw * 0.6f + hw * 1.2f * k / 4f;
                Vector3 top = new Vector3(x, high - 0.1f, random.Range(-0.3f, 0.3f));
                float drop = high * random.Range(0.3f, 0.52f);
                var tube = new List<Vector3> { top, top - Vector3.UnitY * (drop * 0.6f), top + new Vector3(random.Range(-0.5f, 0.5f), -drop * 0.92f, random.Range(-0.4f, 0.4f)), top + new Vector3(random.Range(-0.8f, 0.8f), -drop, random.Range(-0.6f, 0.6f)) };
                Kit.Bend(b, tube, 0.13f, 0.1f, brass);
                Vector3 end = tube[^1];
                Kit.Lathe(b, end - Vector3.UnitY * 0.5f, Vector3.UnitY, 0.5f, Kit.Profile((0f, 0.02f), (0.6f, 0.08f), (1f, 0.14f)), 5, 2, iron);
                if (k % 2 == 0) lights.Add(AssetLight.Emitting(end - Vector3.UnitY * 0.5f, "Smithing.Drip", 0.6f, tint: bile));
            }
            foreach (float s in new[] { -0.3f, 0.3f }) Kit.Chain(b, new Vector3(s * hw, high - 0.1f, 0f), new Vector3(s * 2.2f, high * 0.52f, 0f), 0.3f, 0.36f, rust);
            // The collar, a ring of iron as wide as a tyrant's neck, hung open.
            Vector3 collar = new Vector3(0f, high * 0.48f, 0f);
            const int Sides = 14;
            for (int k = 1; k < Sides - 1; k++)
            {
                float a0 = k * MathF.Tau / Sides + MathF.PI * 0.5f, a1 = (k + 1) * MathF.Tau / Sides + MathF.PI * 0.5f;
                b.Tube(collar + new Vector3(MathF.Cos(a0) * 1.7f, MathF.Sin(a0) * 0.5f, MathF.Sin(a0) * 1.3f), collar + new Vector3(MathF.Cos(a1) * 1.7f, MathF.Sin(a1) * 0.5f, MathF.Sin(a1) * 1.3f), 0.2f, 0.2f, 6, iron, capped: k == 1 || k == Sides - 2);
            }
            // The winch, the grating, and what's dripped on it.
            Kit.Lathe(b, new Vector3(hw + 0.3f, 1.4f, -0.8f), Vector3.UnitZ, 1.6f, _ => 0.5f, 10, 1, Timber(random, 0.7f));
            Kit.Pole(b, new Vector3(hw + 0.3f, 1.4f, 0.8f), new Vector3(hw + 0.3f, 1.4f, 1.5f), 0.05f, iron);
            Kit.Pole(b, new Vector3(hw + 0.3f, 1.4f, 1.5f), new Vector3(hw + 0.3f, 2.2f, 1.5f), 0.05f, iron);
            for (float x = -hw * 0.7f; x <= hw * 0.7f; x += 0.7f) Kit.Span(b, new Vector3(x - 0.06f, Ground + 0.01f, -2.2f), new Vector3(x + 0.06f, Ground + 0.1f, 2.2f), 0f, iron);
            foreach (float z in new[] { -2.2f, 0f, 2.2f }) Kit.Span(b, new Vector3(-hw * 0.72f, Ground + 0.02f, z - 0.08f), new Vector3(hw * 0.72f, Ground + 0.12f, z + 0.08f), 0f, iron);
            Kit.Heap(b, new Vector3(random.Range(-1f, 1f), Ground - 0.02f, random.Range(-0.6f, 0.6f)), new Vector2(hw * 0.5f, 1.7f), 0.05f, random.Float(), Glow(bile.X, bile.Y, bile.Z, 0.8f), random, 2, 12);
            lights.Add(AssetLight.Glow(new Vector3(0f, 1.2f, 0f), bile, 50f, 13f) with { Flicker = 0.12f, FlickerSpeed = 0.9f });
        }

        /// <summary>
        /// Bile standing on the ground where it's leaked: a pool of it, lit, in a rim of the black mud it makes of whatever it lies on. Nothing of it stands as high as an ankle.
        /// </summary>
        public static void BilePool(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            float radius = Math.Clamp(spec.Metres(0), 0.8f, 5f);
            Vector3 bile = SeatDressing.BileLight;
            float yaw = random.Float() * MathF.Tau;
            Kit.Heap(b, new Vector3(0f, Ground - 0.03f, 0f), new Vector2(radius * 1.2f, radius * 0.95f), 0.05f, yaw, Flat(new Vector3(0.07f, 0.075f, 0.04f)), random, 2, 14);
            Kit.Heap(b, new Vector3(0f, Ground, 0f), new Vector2(radius, radius * 0.74f), 0.04f, yaw, Glow(bile.X, bile.Y, bile.Z, 0.75f), random, 2, 14);
            if (spec.Int(1) == 1) lights.Add(AssetLight.Glow(new Vector3(0f, 0.9f, 0f), bile, 30f, 9f) with { Flicker = 0.1f, FlickerSpeed = 0.7f });
        }
    }
}

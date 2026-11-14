using Dino_Engine.Modelling.Model;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.Modelling.Procedural.Architecture.Places.PlaceMaterials;

namespace Dino_Engine.Modelling.Procedural.Architecture.Places
{
    /// <summary>
    /// The Reich's prison for beasts (PLACES.md): cages of black iron round a breaking yard, where a beast born free is chained to the post and goaded until it takes the collar; the racks of
    /// the harness it's put in once it has, and the forge its collars are made at. Everything the Reich owns is black iron, oxblood and brass, and nothing else is. A cage's door is a piece of its
    /// own, as it swings open when the place is freed. Headless.
    /// </summary>
    internal static class Prison
    {
        /// <summary>How wide a cage's door is, for a cage of a width: room for what's in it to walk out.</summary>
        public static float DoorWidth(float cageWidth) => MathF.Min(cageWidth * 0.55f, 3.8f);

        private const float Curb = 0.5f;        // How wide the stone curb its bars are leaded into is.

        /// <summary>A beast's cage, its front (where its door is) towards +z: bars leaded into a stone curb, heavy posts with brass caps, bars over the top, straw on the ground, a trough and chains inside, and the mark over the door.</summary>
        public static void Cage(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float w = Math.Clamp(spec.Metres(0), 3f, 10f), d = Math.Clamp(spec.Metres(1), 3f, 10f), h = Math.Clamp(spec.Metres(2), 3f, 7f);
            float hx = w * 0.5f, hz = d * 0.5f;
            float door = DoorWidth(w);
            VertexMaterial iron = Iron(random), dark = Iron(random, 0.75f), rust = Rusted(random), brass = Brass(random);
            Func<Vector3, VertexMaterial> stone = n => Dressed(new Vector3(0.3f, 0.3f, 0.31f), random, n.Y > 0.5f ? 1.2f : 0.4f);

            // The curb: four lengths of dark stone round the floor, the door's threshold a slab of its own.
            Kit.Span(b, new Vector3(-hx, -0.3f, -hz), new Vector3(hx, 0.42f, -hz + Curb), 0.05f, stone);
            Kit.Span(b, new Vector3(-hx, -0.3f, -hz + Curb), new Vector3(-hx + Curb, 0.42f, hz), 0.05f, stone);
            Kit.Span(b, new Vector3(hx - Curb, -0.3f, -hz + Curb), new Vector3(hx, 0.42f, hz), 0.05f, stone);
            Kit.Span(b, new Vector3(-hx + Curb, -0.3f, hz - Curb), new Vector3(-door * 0.5f, 0.42f, hz), 0.05f, stone);
            Kit.Span(b, new Vector3(door * 0.5f, -0.3f, hz - Curb), new Vector3(hx - Curb, 0.42f, hz), 0.05f, stone);
            Kit.Span(b, new Vector3(-door * 0.5f, -0.3f, hz - Curb), new Vector3(door * 0.5f, 0.2f, hz), 0.04f, stone);

            // Posts at its corners and every two metres or so, square, riveted, with a brass cap; flat bands tying them at the foot, the middle and the head.
            float inset = Curb * 0.5f;
            var xs = Stations(-hx + inset, hx - inset, 2.2f);
            var zs = Stations(-hz + inset, hz - inset, 2.2f);
            void Post(float x, float z)
            {
                Kit.Span(b, new Vector3(x - 0.13f, 0.3f, z - 0.13f), new Vector3(x + 0.13f, h, z + 0.13f), 0.02f, dark);
                Kit.Lathe(b, new Vector3(x, h, z), Vector3.UnitY, 0.32f, Kit.Profile((0f, 0.2f), (0.5f, 0.15f), (1f, 0.04f)), 6, 2, brass);
            }
            foreach (float x in xs) { Post(x, -hz + inset); Post(x, hz - inset); }
            foreach (float z in zs.Skip(1).SkipLast(1)) { Post(-hx + inset, z); Post(hx - inset, z); }
            // The door's own posts, heavier.
            foreach (float s in new[] { -1f, 1f }) Kit.Span(b, new Vector3(s * door * 0.5f - 0.17f, 0.2f, hz - inset - 0.17f), new Vector3(s * door * 0.5f + 0.17f, h + 0.1f, hz - inset + 0.17f), 0.02f, dark);
            foreach (float y in new[] { 0.55f, h * 0.5f, h - 0.12f })
            {
                Band(b, new Vector3(-hx + inset, y, -hz + inset), new Vector3(hx - inset, y, -hz + inset), dark);
                Band(b, new Vector3(-hx + inset, y, -hz + inset), new Vector3(-hx + inset, y, hz - inset), dark);
                Band(b, new Vector3(hx - inset, y, -hz + inset), new Vector3(hx - inset, y, hz - inset), dark);
                Band(b, new Vector3(-hx + inset, y, hz - inset), new Vector3(-door * 0.5f - 0.17f, y, hz - inset), dark);
                Band(b, new Vector3(door * 0.5f + 0.17f, y, hz - inset), new Vector3(hx - inset, y, hz - inset), dark);
            }
            Band(b, new Vector3(-door * 0.5f, h - 0.12f, hz - inset), new Vector3(door * 0.5f, h - 0.12f, hz - inset), dark);

            // The bars: round, close enough that nothing gets a head through, a few gone to rust at the foot.
            const float Spacing = 0.34f;
            void Bars(Vector3 from, Vector3 to, bool skipDoor)
            {
                float length = (to - from).Length;
                int n = Math.Max(1, (int)(length / Spacing));
                for (int i = 1; i < n; i++)
                {
                    Vector3 p = Vector3.Lerp(from, to, i / (float)n);
                    if (skipDoor && MathF.Abs(p.X) < door * 0.5f + 0.2f) continue;
                    Kit.Pole(b, new Vector3(p.X, 0.35f, p.Z), new Vector3(p.X, h - 0.05f, p.Z), 0.04f, random.Chance(0.12f) ? rust : iron);
                }
            }
            Bars(new Vector3(-hx + inset, 0f, -hz + inset), new Vector3(hx - inset, 0f, -hz + inset), false);
            Bars(new Vector3(-hx + inset, 0f, -hz + inset), new Vector3(-hx + inset, 0f, hz - inset), false);
            Bars(new Vector3(hx - inset, 0f, -hz + inset), new Vector3(hx - inset, 0f, hz - inset), false);
            Bars(new Vector3(-hx + inset, 0f, hz - inset), new Vector3(hx - inset, 0f, hz - inset), true);
            // Over the top, across its width, so the camera looks down through them at what's inside.
            int over = Math.Max(2, (int)(d / 0.55f));
            for (int i = 0; i <= over; i++)
            {
                float z = -hz + inset + (d - 2f * inset) * i / over;
                Kit.Pole(b, new Vector3(-hx + inset, h - 0.04f, z), new Vector3(hx - inset, h - 0.04f, z), 0.04f, iron);
            }
            foreach (float x in xs) Band(b, new Vector3(x, h, -hz + inset), new Vector3(x, h, hz - inset), dark);

            // Its mark on a plate over the door, facing out.
            Kit.Mark(b, new Vector3(0f, h - 0.55f, hz - inset + 0.2f), Vector3.UnitX, Vector3.UnitY, MathF.Min(door * 0.4f, 1.1f), 0.03f, random);

            // Inside: straw trodden into the dirt, a trough, chains from the back bars, a gnawed bone.
            Kit.Heap(b, new Vector3(random.Range(-0.5f, 0.5f), Ground - 0.05f, random.Range(-hz * 0.4f, 0f)), new Vector2(hx * 0.62f, hz * 0.55f), 0.18f, random.Range(0f, 1f), Straw(random, 0.72f), random, 3, 12);
            Vector3 trough = new Vector3(-hx + Curb + 0.7f, Ground, -hz + Curb + 1.4f);
            Kit.Box(b, trough + Vector3.UnitY * 0.35f, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(0.45f, 0.35f, 1.1f), 0.03f, Timber(random, 0.6f));
            Kit.Box(b, trough + Vector3.UnitY * 0.64f, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(0.32f, 0.04f, 0.95f), 0f, Flat(new Vector3(0.05f, 0.06f, 0.06f)));
            for (int k = 0; k < 2; k++)
            {
                Vector3 hook = new Vector3(random.Range(-hx * 0.5f, hx * 0.5f), random.Range(1.8f, 2.6f), -hz + inset + 0.05f);
                Kit.Chain(b, hook, new Vector3(hook.X + random.Range(-1.2f, 1.2f), Ground + 0.06f, hook.Z + random.Range(1.2f, 2.4f)), 0.15f, 0.26f, dark);
            }
            Vector3 bone = new Vector3(random.Range(-hx * 0.4f, hx * 0.4f), Ground + 0.12f, random.Range(0f, hz * 0.5f));
            float turn = random.Float() * MathF.PI;
            Vector3 dir = new Vector3(MathF.Cos(turn), 0f, MathF.Sin(turn));
            Kit.Pole(b, bone - dir * 0.7f, bone + dir * 0.7f, 0.09f, Bone(random));
        }

        // Where the posts along a side stand: its ends and evenly between, no more than `step` apart.
        private static List<float> Stations(float from, float to, float step)
        {
            int n = Math.Max(1, (int)MathF.Ceiling((to - from) / step));
            var list = new List<float>();
            for (int i = 0; i <= n; i++) list.Add(MathHelper.Lerp(from, to, i / (float)n));
            return list;
        }

        // A flat band of iron between two points, on edge.
        private static void Band(MeshBuilder b, Vector3 from, Vector3 to, VertexMaterial m) => Kit.Plank(b, from, to, Vector3.UnitY, 0.06f, 0.18f, m, 0.01f);

        /// <summary>A cage's door: a frame of flat iron filled with bars, hinged at x = 0 and closing along +x, a lock plate and its brass hasp at its free edge.</summary>
        public static void CageDoor(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float w = Math.Clamp(spec.Metres(0), 1.5f, 5f), h = Math.Clamp(spec.Metres(1), 2.5f, 7f);
            VertexMaterial iron = Iron(random), dark = Iron(random, 0.75f), brass = Brass(random);
            float top = h - 0.25f, foot = 0.28f;
            // The frame.
            Band(b, new Vector3(0.05f, foot, 0f), new Vector3(w - 0.05f, foot, 0f), dark);
            Band(b, new Vector3(0.05f, top, 0f), new Vector3(w - 0.05f, top, 0f), dark);
            Band(b, new Vector3(0.05f, (foot + top) * 0.5f, 0f), new Vector3(w - 0.05f, (foot + top) * 0.5f, 0f), dark);
            Kit.Span(b, new Vector3(0f, foot - 0.05f, -0.07f), new Vector3(0.12f, top + 0.05f, 0.07f), 0.01f, dark);
            Kit.Span(b, new Vector3(w - 0.12f, foot - 0.05f, -0.07f), new Vector3(w, top + 0.05f, 0.07f), 0.01f, dark);
            // A brace across it from the foot of the hinge to the head of the free edge.
            Kit.Plank(b, new Vector3(0.1f, foot + 0.05f, 0f), new Vector3(w - 0.1f, top - 0.05f, 0f), Vector3.UnitZ, 0.08f, 0.05f, dark);
            int bars = Math.Max(2, (int)(w / 0.34f));
            for (int i = 1; i < bars; i++)
            {
                float x = w * i / bars;
                Kit.Pole(b, new Vector3(x, foot, 0f), new Vector3(x, top, 0f), 0.04f, iron);
            }
            // Its hinges' knuckles, and the lock at its free edge with its hasp in brass.
            foreach (float y in new[] { foot + 0.4f, top - 0.4f }) b.Tube(new Vector3(0f, y - 0.14f, 0f), new Vector3(0f, y + 0.14f, 0f), 0.09f, 0.09f, 6, dark);
            Kit.Box(b, new Vector3(w - 0.32f, h * 0.48f, 0.05f), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(0.22f, 0.3f, 0.05f), 0.01f, dark);
            Kit.Box(b, new Vector3(w - 0.32f, h * 0.48f, 0.11f), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(0.1f, 0.14f, 0.025f), 0.005f, brass);
        }

        /// <summary>The post in the middle of a breaking yard: a great timber with an iron collar and rings, its chains run out across the trodden mud to stakes, and the goads leaning on a trestle.</summary>
        public static void BreakingPost(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float radius = Math.Clamp(spec.Metres(0), 3f, 10f);
            VertexMaterial wood = Timber(random, 0.7f), iron = Iron(random), dark = Iron(random, 0.75f), brass = Brass(random);
            float high = random.Range(3.8f, 4.6f);
            Kit.Pole(b, new Vector3(0f, -0.6f, 0f), new Vector3(0f, high, 0f), 0.42f, wood, 0.38f);
            foreach (float y in new[] { 0.9f, 2.3f, high - 0.3f }) b.Tube(new Vector3(0f, y - 0.12f, 0f), new Vector3(0f, y + 0.12f, 0f), 0.47f, 0.47f, 10, dark, capped: false);
            Kit.Lathe(b, new Vector3(0f, high, 0f), Vector3.UnitY, 0.4f, Kit.Profile((0f, 0.4f), (0.6f, 0.3f), (1f, 0.06f)), 8, 2, brass);
            // Rings round it, and from three of them chains out across the mud to their stakes.
            int chains = random.Int(3, 4);
            for (int k = 0; k < chains; k++)
            {
                float a = k * MathF.Tau / chains + random.Range(-0.3f, 0.3f);
                Vector3 ring = new Vector3(MathF.Cos(a) * 0.5f, 1.05f, MathF.Sin(a) * 0.5f);
                b.Tube(ring - new Vector3(0f, 0.12f, 0f), ring + new Vector3(0f, 0.12f, 0f), 0.16f, 0.16f, 8, iron, capped: false);
                Vector3 stake = new Vector3(MathF.Cos(a) * radius * random.Range(0.65f, 0.85f), Ground, MathF.Sin(a) * radius * random.Range(0.65f, 0.85f));
                Kit.Chain(b, ring, stake + Vector3.UnitY * 0.25f, 0.5f, 0.32f, dark);
                Kit.Pole(b, stake - Vector3.UnitY * 0.4f, stake + Vector3.UnitY * 0.35f, 0.09f, iron, 0.07f);
                b.Tube(stake + Vector3.UnitY * 0.25f, stake + Vector3.UnitY * 0.4f, 0.14f, 0.14f, 6, iron, capped: false);
            }
            // The goads, long poles with iron hooks, leaning on a trestle at the yard's edge.
            Vector3 trestle = new Vector3(radius * 0.9f, 0f, -radius * 0.2f);
            foreach (float s in new[] { -0.8f, 0.8f })
            {
                Kit.Pole(b, trestle + new Vector3(-0.3f, 0f, s), trestle + new Vector3(0f, 1.3f, s), 0.06f, wood);
                Kit.Pole(b, trestle + new Vector3(0.3f, 0f, s), trestle + new Vector3(0f, 1.3f, s), 0.06f, wood);
            }
            Kit.Pole(b, trestle + new Vector3(0f, 1.3f, -0.95f), trestle + new Vector3(0f, 1.3f, 0.95f), 0.07f, wood);
            for (int k = 0; k < 4; k++)
            {
                float z = -0.6f + k * 0.4f;
                Vector3 foot = trestle + new Vector3(-0.9f, Ground, z);
                Vector3 tip = trestle + new Vector3(0.5f, 2.7f, z + random.Range(-0.1f, 0.1f));
                Kit.Pole(b, foot, tip, 0.045f, ReichLeather(1.6f));
                Vector3 along = (tip - foot).Normalized();
                Kit.Pole(b, tip, tip + along * 0.25f + new Vector3(0.12f, 0f, 0f), 0.03f, iron, 0.01f);
            }
        }

        /// <summary>A rack of the harness a broken beast goes into: collars of black iron with their spikes, oxblood straps hanging in loops, a snout-mask on a peg, brass at every buckle.</summary>
        public static void HarnessRack(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            VertexMaterial wood = Timber(random, 0.65f), iron = Iron(random), brass = Brass(random), strap = ReichLeather(1.4f);
            const float Long = 3.2f, High = 2.2f;
            foreach (float x in new[] { -Long * 0.5f, Long * 0.5f })
            {
                Kit.Plank(b, new Vector3(x, 0f, -0.5f), new Vector3(x, High, 0f), Vector3.UnitX, 0.14f, 0.14f, wood);
                Kit.Plank(b, new Vector3(x, 0f, 0.5f), new Vector3(x, High, 0f), Vector3.UnitX, 0.14f, 0.14f, wood);
            }
            Kit.Plank(b, new Vector3(-Long * 0.5f - 0.2f, High, 0f), new Vector3(Long * 0.5f + 0.2f, High, 0f), Vector3.UnitY, 0.16f, 0.16f, wood);
            // Collars over the bar: rings of iron with spikes round them.
            int collars = random.Int(2, 3);
            for (int k = 0; k < collars; k++)
            {
                float x = -Long * 0.35f + k * (Long * 0.7f / Math.Max(1, collars - 1));
                float r = random.Range(0.42f, 0.56f);
                const int Segments = 10;
                for (int s = 0; s < Segments; s++)
                {
                    float a0 = s * MathF.Tau / Segments, a1 = (s + 1) * MathF.Tau / Segments;
                    Vector3 p0 = new Vector3(x, High - r + MathF.Cos(a0) * r, MathF.Sin(a0) * r), p1 = new Vector3(x, High - r + MathF.Cos(a1) * r, MathF.Sin(a1) * r);
                    b.Tube(p0, p1, 0.07f, 0.07f, 5, iron, capped: false);
                    if (s % 2 == 0)
                    {
                        Vector3 outward = (p0 - new Vector3(x, High - r, 0f)).Normalized();
                        Kit.Pole(b, p0, p0 + outward * 0.22f, 0.04f, iron, 0.005f);
                    }
                }
                Kit.Box(b, new Vector3(x, High - r * 2f, 0f), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(0.1f, 0.08f, 0.12f), 0.01f, brass);
            }
            // Straps hanging in loops between the uprights, a buckle on each.
            for (int k = 0; k < 3; k++)
            {
                float z = -0.18f + k * 0.18f;
                Kit.Rope(b, new Vector3(-Long * 0.5f, High - 0.1f, z), new Vector3(Long * 0.5f, High - 0.1f, z), random.Range(0.7f, 1.2f), 0.05f, strap, 8);
            }
            // A snout-mask on a peg at one end.
            Vector3 peg = new Vector3(Long * 0.5f + 0.1f, High * 0.62f, 0.35f);
            Kit.Pole(b, peg - new Vector3(0f, 0f, 0.3f), peg, 0.04f, wood);
            Kit.Lathe(b, peg + new Vector3(0f, -0.15f, 0.1f), new Vector3(0f, -0.2f, 1f), 0.75f, Kit.Profile((0f, 0.32f), (0.6f, 0.26f), (1f, 0.16f)), 8, 3, iron);
            Kit.Plank(b, peg + new Vector3(-0.3f, -0.15f, 0.4f), peg + new Vector3(0.3f, -0.15f, 0.4f), Vector3.UnitZ, 0.08f, 0.04f, brass);
        }

        /// <summary>Where a prison's collars are made: an anvil on its block, a brazier of coals, a quench tub, tongs and hammers on a rack, and collars cooling on the ground.</summary>
        public static void CollarForge(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            VertexMaterial iron = Iron(random), dark = Iron(random, 0.7f), wood = Timber(random, 0.6f);
            // The anvil on a block of oak.
            Kit.Pole(b, new Vector3(0f, -0.2f, 0f), new Vector3(0f, 0.75f, 0f), 0.42f, wood, 0.4f);
            Kit.Span(b, new Vector3(-0.45f, 0.75f, -0.2f), new Vector3(0.45f, 1.05f, 0.2f), 0.02f, dark);
            Kit.Span(b, new Vector3(-0.3f, 1.05f, -0.16f), new Vector3(0.3f, 1.12f, 0.16f), 0.01f, iron);
            Kit.Lathe(b, new Vector3(0.45f, 1.0f, 0f), Vector3.UnitX, 0.45f, Kit.Profile((0f, 0.12f), (1f, 0.01f)), 6, 1, dark);
            Kit.Brazier(b, new Vector3(-1.9f, 0f, -0.6f), 1.05f, random, lights);
            // The quench tub: half a barrel of black water.
            Vector3 tub = new Vector3(1.7f, 0f, -0.7f);
            Kit.Lathe(b, tub, Vector3.UnitY, 0.75f, Kit.Profile((0f, 0.5f), (1f, 0.58f)), 10, 2, Timber(random, 0.55f));
            Kit.Lathe(b, tub + Vector3.UnitY * 0.7f, Vector3.UnitY, 0.01f, Kit.Profile((0f, 0.55f), (1f, 0f)), 10, 1, Flat(new Vector3(0.03f, 0.035f, 0.04f)));
            // Tongs and hammers on a rack behind.
            Vector3 rack = new Vector3(0f, 0f, -1.6f);
            Kit.Plank(b, rack + new Vector3(-1.2f, 1.6f, 0f), rack + new Vector3(1.2f, 1.6f, 0f), Vector3.UnitY, 0.12f, 0.1f, wood);
            foreach (float x in new[] { -1.1f, 1.1f }) Kit.Plank(b, rack + new Vector3(x, 0f, 0f), rack + new Vector3(x, 1.7f, 0f), Vector3.UnitZ, 0.1f, 0.1f, wood);
            for (int k = 0; k < 5; k++)
            {
                float x = -0.8f + k * 0.4f;
                Kit.Pole(b, rack + new Vector3(x, 1.6f, 0.08f), rack + new Vector3(x + random.Range(-0.05f, 0.05f), 0.7f, 0.12f), 0.025f, dark);
            }
            // Collars cooling on the ground.
            for (int k = 0; k < 2; k++)
            {
                Vector3 at = new Vector3(random.Range(-0.6f, 0.9f), Ground + 0.06f, random.Range(0.8f, 1.4f));
                float r = random.Range(0.38f, 0.5f);
                for (int s = 0; s < 10; s++)
                {
                    float a0 = s * MathF.Tau / 10f, a1 = (s + 1) * MathF.Tau / 10f;
                    b.Tube(at + new Vector3(MathF.Cos(a0) * r, 0f, MathF.Sin(a0) * r), at + new Vector3(MathF.Cos(a1) * r, 0f, MathF.Sin(a1) * r), 0.06f, 0.06f, 5, iron, capped: false);
                }
            }
        }

        /// <summary>A stake of black iron driven into the ground, chains run out from it across the mud.</summary>
        public static void ChainStake(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float length = Math.Clamp(spec.Metres(0), 1f, 8f);
            VertexMaterial iron = Iron(random), dark = Iron(random, 0.75f);
            Kit.Pole(b, new Vector3(0f, -0.5f, 0f), new Vector3(0f, 0.6f, 0f), 0.1f, iron, 0.08f);
            b.Tube(new Vector3(0f, 0.45f, 0f), new Vector3(0f, 0.62f, 0f), 0.17f, 0.17f, 6, dark, capped: false);
            int chains = random.Int(1, 3);
            for (int k = 0; k < chains; k++)
            {
                float a = random.Float() * MathF.Tau;
                Vector3 end = new Vector3(MathF.Cos(a) * length * random.Range(0.6f, 1f), Ground + 0.05f, MathF.Sin(a) * length * random.Range(0.6f, 1f));
                Kit.Chain(b, new Vector3(0f, 0.5f, 0f), end, 0.02f, 0.3f, dark);
                Kit.Box(b, end, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(0.18f, 0.08f, 0.12f), 0.01f, iron);
            }
        }
    }
}

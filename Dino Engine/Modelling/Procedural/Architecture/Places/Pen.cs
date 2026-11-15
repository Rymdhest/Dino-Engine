using Dino_Engine.Modelling.Model;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using OpenTK.Mathematics;
using static Dino_Engine.Modelling.Procedural.Architecture.Places.PlaceMaterials;

namespace Dino_Engine.Modelling.Procedural.Architecture.Places
{
    /// <summary>
    /// A brood's pen (LORE.md: cages, chains and the smell of blood, where the Reich keeps its beasts between hunts): the kennels its war-raptors are shut in, a row of stalls with barred
    /// doors, their boards scored by claws and each stall's number on a brass plate; the butcher's block and the rack its meat hangs from; the dummies of straw dressed in the land's
    /// clothes that it teaches its beasts on; and the midden all of it ends up on. Black iron, oxblood and brass are the Reich's and nothing else is. Model +z is a piece's front, y = 0
    /// where people stand. Headless.
    /// </summary>
    internal static class Pen
    {
        /// <summary>
        /// A row of kennels under one roof: dark boards in black iron frames, each stall barred across its front with a door that some have left standing open, its back half roofed and
        /// its front half barred over so the camera sees into it, straw, a chain, a trough and gnawed bones in each, claws' scores pale in the boards and a brass plate with its number
        /// over each door, and the mark on its end. Numbers: stalls, a stall's width, depth.
        /// </summary>
        public static void Kennel(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            int stalls = Math.Clamp(spec.Int(0), 1, 7);
            float w = Math.Clamp(spec.Metres(1), 2f, 3.4f), d = Math.Clamp(spec.Metres(2), 2.6f, 4.4f);
            float length = stalls * w, hx = length * 0.5f, hz = d * 0.5f;
            const float Back = 2.7f, Front = 2.5f;
            // The land's own boards, gone grey, framed and barred in the Reich's black iron: pale, as from 33 m up a pen of dark boards is a hole.
            VertexMaterial boards = Weathered(random, 0.92f), iron = Iron(random), dark = Iron(random, 0.75f), brass = Brass(random), scored = Fresh(random, 1.05f);
            // The back wall, boarded in iron frames, and the ends.
            Kit.Span(b, new Vector3(-hx, -0.2f, -hz), new Vector3(hx, Back, -hz + 0.16f), 0.01f, boards);
            foreach (float s in new[] { -1f, 1f })
            {
                Kit.Span(b, new Vector3(s * hx - 0.08f, -0.2f, -hz), new Vector3(s * hx + 0.08f, Back, hz), 0.01f, boards);
                // The mark on the end the camera sees most of.
                Kit.Mark(b, new Vector3(s * (hx + 0.09f), Back * 0.6f, 0f), new Vector3(0f, 0f, -s), Vector3.UnitY, 0.9f, 0.01f, random);
            }
            for (float x = -hx; x <= hx + 0.01f; x += w * 0.5f) Kit.Span(b, new Vector3(x - 0.07f, -0.2f, -hz - 0.04f), new Vector3(x + 0.07f, Back + 0.05f, -hz + 0.2f), 0.01f, dark);
            Kit.Span(b, new Vector3(-hx - 0.1f, Back - 0.12f, -hz - 0.06f), new Vector3(hx + 0.1f, Back + 0.02f, -hz + 0.22f), 0.01f, dark);
            // The roof over the back half, iron sheets falling to the back, the front half barred over.
            float roofTo = -hz + d * 0.55f;
            Kit.Solid(b, new[]
            {
                new Vector3(-hx - 0.25f, Back + 0.4f, roofTo), new Vector3(hx + 0.25f, Back + 0.4f, roofTo), new Vector3(hx + 0.25f, Back + 0.45f, roofTo), new Vector3(-hx - 0.25f, Back + 0.45f, roofTo),
                new Vector3(-hx - 0.25f, Back - 0.05f, -hz - 0.35f), new Vector3(hx + 0.25f, Back - 0.05f, -hz - 0.35f), new Vector3(hx + 0.25f, Back, -hz - 0.35f), new Vector3(-hx - 0.25f, Back, -hz - 0.35f),
            }, 0f, Plating(random, 0.85f));
            for (float x = -hx; x <= hx + 0.01f; x += w) Kit.Pole(b, new Vector3(x, Front, roofTo), new Vector3(x, Back + 0.4f, roofTo), 0.06f, dark);
            Kit.Plank(b, new Vector3(-hx, Front + 0.05f, hz), new Vector3(hx, Front + 0.05f, hz), Vector3.UnitY, 0.12f, 0.16f, dark);
            Kit.Plank(b, new Vector3(-hx, Front + 0.05f, roofTo), new Vector3(hx, Front + 0.05f, roofTo), Vector3.UnitY, 0.12f, 0.16f, dark);
            for (float x = -hx + 0.3f; x < hx; x += 0.42f) Kit.Pole(b, new Vector3(x, Front + 0.08f, roofTo), new Vector3(x, Front + 0.08f, hz), 0.03f, iron);
            for (int s = 0; s < stalls; s++)
            {
                float x0 = -hx + s * w, x1 = x0 + w, mid = (x0 + x1) * 0.5f;
                // The partition on its left, boards to most of its height, scored low down by claws.
                if (s > 0)
                {
                    Kit.Span(b, new Vector3(x0 - 0.05f, -0.2f, -hz + 0.1f), new Vector3(x0 + 0.05f, 2.1f, hz - 0.1f), 0.008f, boards);
                    Kit.Pole(b, new Vector3(x0, -0.2f, hz), new Vector3(x0, Front, hz), 0.08f, dark);
                }
                for (int k = 0; k < random.Int(2, 5); k++)
                {
                    float z = random.Range(-hz + 0.5f, hz - 0.6f), y = random.Range(0.4f, 1.3f);
                    foreach (float side in new[] { -0.06f, 0.06f })
                    {
                        if (s == 0 && side < 0f) continue;
                        if (s == stalls - 1 && side > 0f) continue;
                        float px = (side < 0f ? x0 : x1) + side;
                        for (int c = 0; c < 3; c++) Kit.Plank(b, new Vector3(px, y + c * 0.07f, z), new Vector3(px, y + c * 0.07f + 0.32f, z + 0.28f), new Vector3(MathF.Sign(-side), 0f, 0f), 0.02f, 0.006f, scored, 0f);
                    }
                }
                // Its front: bars either side of the door, the door shut or standing open, and its number on a brass plate over it.
                float door = MathF.Min(1.5f, w - 0.7f);
                for (float x = x0 + 0.2f; x < x1 - 0.1f; x += 0.3f)
                {
                    if (MathF.Abs(x - mid) < door * 0.5f) continue;
                    Kit.Pole(b, new Vector3(x, 0.1f, hz), new Vector3(x, Front, hz), 0.03f, iron);
                }
                foreach (float y in new[] { 0.3f, Front * 0.55f }) Kit.Plank(b, new Vector3(x0, y, hz), new Vector3(x1, y, hz), Vector3.UnitY, 0.05f, 0.12f, dark, 0.005f);
                bool open = random.Chance(0.4f);
                float swing = open ? random.Range(1.1f, 1.9f) : 0f;
                Vector3 hinge = new Vector3(mid - door * 0.5f, 0f, hz + 0.05f);
                Vector3 along = new Vector3(MathF.Cos(swing), 0f, MathF.Sin(swing));
                foreach (float y in new[] { 0.25f, 1.15f, Front - 0.2f }) Kit.Plank(b, hinge + Vector3.UnitY * y, hinge + along * door + Vector3.UnitY * y, Vector3.UnitY, 0.05f, 0.1f, dark, 0.005f);
                for (int k = 0; k <= 4; k++) Kit.Pole(b, hinge + along * (door * k / 4f) + Vector3.UnitY * 0.15f, hinge + along * (door * k / 4f) + Vector3.UnitY * (Front - 0.1f), 0.028f, iron);
                Kit.Box(b, new Vector3(mid, Front - 0.35f, hz + 0.1f), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(0.22f, 0.12f, 0.015f), 0.004f, brass);
                Kit.Plank(b, new Vector3(mid - 0.12f, Front - 0.35f, hz + 0.118f), new Vector3(mid + 0.12f, Front - 0.35f, hz + 0.118f), Vector3.UnitZ, 0.05f, 0.004f, OxbloodPaint(), 0f);
                // What's in it: straw, the chain off the back wall, its trough and what it's gnawed.
                Kit.Heap(b, new Vector3(mid + random.Range(-0.3f, 0.3f), Ground - 0.06f, random.Range(-hz * 0.4f, 0f)), new Vector2(w * 0.38f, hz * 0.6f), 0.16f, random.Float(), Straw(random, 0.7f), random, 3, 10);
                Vector3 ring = new Vector3(mid + random.Range(-0.4f, 0.4f), 1.1f, -hz + 0.2f);
                b.Tube(ring - new Vector3(0f, 0.08f, 0f), ring + new Vector3(0f, 0.08f, 0f), 0.09f, 0.09f, 6, dark, capped: false);
                Kit.Chain(b, ring, new Vector3(ring.X + random.Range(-0.6f, 0.6f), Ground + 0.05f, ring.Z + random.Range(0.8f, 1.6f)), 0.12f, 0.22f, dark);
                Vector3 trough = new Vector3(x1 - 0.45f, 0f, -hz + 0.9f);
                Kit.Span(b, trough + new Vector3(-0.22f, 0f, -0.5f), trough + new Vector3(0.22f, 0.4f, 0.5f), 0.02f, Timber(random, 0.55f));
                Kit.Face(b, trough + new Vector3(-0.15f, 0.36f, -0.42f), trough + new Vector3(0.15f, 0.36f, -0.42f), trough + new Vector3(0.15f, 0.36f, 0.42f), trough + new Vector3(-0.15f, 0.36f, 0.42f), Vector3.UnitY, Flat(new Vector3(0.05f, 0.04f, 0.035f)));
                if (random.Chance(0.7f))
                {
                    float a = random.Float() * MathF.PI;
                    Vector3 bone = new Vector3(mid + random.Range(-0.5f, 0.5f), Ground + 0.08f, random.Range(0f, hz * 0.6f));
                    Vector3 dir = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a)) * 0.45f;
                    Kit.Pole(b, bone - dir, bone + dir, 0.06f, Bone(random));
                }
            }
        }

        /// <summary>
        /// Where the pen's meat is cut: a block of a whole round of trunk with two cleavers driven into it, a tub of offal and a bucket by it, the ground dark round it, and a frame over
        /// it with hooks off its beam and sides of meat hung from them. Numbers: none.
        /// </summary>
        public static void ButcherBlock(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            VertexMaterial wood = Timber(random, 0.6f), iron = Iron(random), stained = new VertexMaterial(MaterialCatalog.Wood.Index, new Colour(0.24f, 0.11f, 0.08f));
            // The ground soaked dark round it.
            const int Around = 12;
            var edge = new float[Around];
            for (int k = 0; k < Around; k++) edge[k] = random.Range(0.7f, 1.2f);
            for (int k = 0; k < Around; k++)
            {
                float a0 = k * MathF.Tau / Around, a1 = (k + 1) * MathF.Tau / Around;
                Kit.Face(b, new Vector3(0.3f, Ground + 0.008f, 0.5f), new Vector3(0.3f + MathF.Cos(a0) * 1.9f * edge[k], Ground + 0.008f, 0.5f + MathF.Sin(a0) * 1.4f * edge[k]), new Vector3(0.3f + MathF.Cos(a1) * 1.9f * edge[(k + 1) % Around], Ground + 0.008f, 0.5f + MathF.Sin(a1) * 1.4f * edge[(k + 1) % Around]), Vector3.UnitY, Flat(new Vector3(0.12f, 0.05f, 0.04f)));
            }
            // The block and the cleavers in it.
            Kit.Pole(b, new Vector3(0f, -0.2f, 0.6f), new Vector3(0f, 0.85f, 0.6f), 0.42f, stained);
            Kit.Lathe(b, new Vector3(0f, 0.85f, 0.6f), Vector3.UnitY, 0.01f, _ => 0.41f, 10, 1, Flat(new Vector3(0.3f, 0.14f, 0.1f)));
            for (int k = 0; k < 2; k++)
            {
                Vector3 bite = new Vector3(-0.12f + k * 0.26f, 0.9f, 0.55f + k * 0.12f);
                float turn = random.Range(-0.6f, 0.6f);
                Kit.Block(b, bite + Vector3.UnitY * 0.1f, new Vector3(0.16f, 0.11f, 0.012f), turn, 0.003f, iron);
                Vector3 handle = new Vector3(MathF.Cos(turn), 0f, -MathF.Sin(turn));
                Kit.Pole(b, bite + Vector3.UnitY * 0.16f + handle * 0.14f, bite + Vector3.UnitY * 0.2f + handle * 0.42f, 0.02f, wood);
            }
            // The tub of offal and the bucket.
            Kit.Lathe(b, new Vector3(1.1f, 0f, 0.9f), Vector3.UnitY, 0.55f, Kit.Profile((0f, 0.36f), (1f, 0.42f)), 10, 1, Timber(random, 0.5f), openEnd: true);
            Kit.Heap(b, new Vector3(1.1f, 0.45f, 0.9f), new Vector2(0.38f), 0.12f, 0f, Flesh(0.8f), random, 2, 9);
            Kit.Lathe(b, new Vector3(-0.85f, 0f, 1.2f), Vector3.UnitY, 0.38f, Kit.Profile((0f, 0.17f), (1f, 0.21f)), 8, 1, Timber(random, 0.7f), openEnd: true);
            Kit.Lathe(b, new Vector3(-0.85f, 0.34f, 1.2f), Vector3.UnitY, 0.01f, _ => 0.2f, 8, 1, Flat(new Vector3(0.14f, 0.03f, 0.03f)));
            // The frame over it, its hooks and what hangs from them.
            const float High = 2.9f, Hx = 1.7f;
            foreach (float s in new[] { -1f, 1f })
            {
                Kit.Span(b, new Vector3(s * Hx - 0.1f, -0.3f, -0.9f), new Vector3(s * Hx + 0.1f, High, -0.7f), 0.01f, wood);
                Kit.Plank(b, new Vector3(s * Hx, 0f, -0.2f), new Vector3(s * Hx, 1.3f, -0.8f), Vector3.UnitX, 0.08f, 0.08f, wood);
            }
            Kit.Plank(b, new Vector3(-Hx - 0.2f, High - 0.1f, -0.8f), new Vector3(Hx + 0.2f, High - 0.1f, -0.8f), Vector3.UnitY, 0.16f, 0.18f, wood);
            int hooks = random.Int(3, 5);
            for (int k = 0; k < hooks; k++)
            {
                float x = -Hx + 0.5f + (2f * Hx - 1f) * k / Math.Max(1, hooks - 1);
                Vector3 hook = new Vector3(x, High - 0.22f, -0.8f);
                Kit.Pole(b, hook, hook - Vector3.UnitY * 0.3f, 0.02f, iron);
                if (random.Chance(0.2f)) continue;
                // A side of meat: red, fat pale along it, ribs showing in the cut.
                float len = random.Range(0.9f, 1.5f);
                Vector3 top = hook - Vector3.UnitY * 0.32f;
                Kit.Lathe(b, top - Vector3.UnitY * len, Vector3.UnitY, len, Kit.Profile((0f, 0.12f), (0.35f, 0.24f), (0.8f, 0.2f), (1f, 0.06f)), 8, 4, Flesh(random.Range(0.85f, 1.1f)));
                Kit.Lathe(b, top - Vector3.UnitY * (len * 0.7f) + new Vector3(0f, 0f, 0.08f), Vector3.UnitY, len * 0.5f, Kit.Profile((0f, 0.12f), (1f, 0.1f)), 6, 1, Flat(new Vector3(0.66f, 0.6f, 0.5f)));
                for (int r = 0; r < 3; r++) Kit.Pole(b, top - Vector3.UnitY * (len * (0.3f + r * 0.15f)) + new Vector3(-0.18f, 0f, 0.18f), top - Vector3.UnitY * (len * (0.32f + r * 0.15f)) + new Vector3(0.18f, 0f, 0.18f), 0.025f, Bone(random));
            }
            // A saw and knives on a board by the frame.
            Kit.Span(b, new Vector3(Hx + 0.3f, 0.9f, -0.85f), new Vector3(Hx + 1.3f, 0.95f, -0.45f), 0.005f, wood);
            for (int k = 0; k < 3; k++) Kit.Plank(b, new Vector3(Hx + 0.45f + k * 0.28f, 0.96f, -0.75f), new Vector3(Hx + 0.5f + k * 0.28f, 0.96f, -0.5f), Vector3.UnitY, 0.035f, 0.01f, iron, 0f);
        }

        /// <summary>
        /// The dummies the pen's beasts are taught on: posts with a cross-bar for arms, a body and a head of sacking stuffed with straw, dressed in a cloak of the land's colours, gashed
        /// open by claws with the straw coming out; one knocked down. Numbers: how many.
        /// </summary>
        public static void BiteDummy(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            int count = Math.Clamp(spec.Int(0), 1, 4);
            VertexMaterial wood = Weathered(random, 0.8f), sack = Canvas(random, 0.85f), straw = Straw(random, 1.05f);
            Vector3[] cloaks = { new(0.1f, 0.18f, 0.32f), new(0.12f, 0.26f, 0.16f), new(0.46f, 0.32f, 0.1f), new(0.32f, 0.3f, 0.26f) };
            for (int k = 0; k < count; k++)
            {
                Vector3 foot = new Vector3((k - (count - 1) * 0.5f) * 2.4f + random.Range(-0.3f, 0.3f), 0f, random.Range(-0.6f, 0.6f));
                bool down = k == count - 1 && count > 1 && random.Chance(0.6f);
                float lean = down ? 1.35f : random.Range(-0.12f, 0.12f);
                float turn = random.Float() * MathF.Tau;
                Quaternion q = Quaternion.FromAxisAngle(Vector3.UnitY, turn) * Quaternion.FromAxisAngle(Vector3.UnitX, lean);
                Vector3 P(Vector3 p) => foot + Vector3.Transform(p, q) + (down ? Vector3.UnitY * (Ground + 0.25f) : Vector3.Zero);
                Vector3 up = Vector3.Transform(Vector3.UnitY, q), across = Vector3.Transform(Vector3.UnitX, q), front = Vector3.Transform(Vector3.UnitZ, q);
                Kit.Pole(b, P(new Vector3(0f, down ? 0f : -0.5f, 0f)), P(new Vector3(0f, 2.1f, 0f)), 0.07f, wood);
                Kit.Pole(b, P(new Vector3(-0.65f, 1.55f, 0f)), P(new Vector3(0.65f, 1.55f, 0f)), 0.05f, wood);
                // Its body and head, stuffed, and its cloak.
                Kit.Lathe(b, P(new Vector3(0f, 0.85f, 0f)), up, 0.85f, Kit.Profile((0f, 0.22f), (0.5f, 0.3f), (1f, 0.24f)), 8, 3, sack);
                Kit.Ball(b, P(new Vector3(0f, 1.92f, 0f)), new Vector3(0.18f, 0.2f, 0.18f), 7, 4, sack);
                Vector3 cloak = cloaks[random.Int(cloaks.Length)];
                Kit.Cloth(b, P(new Vector3(-0.6f, 1.6f, 0.08f)), P(new Vector3(0.6f, 1.6f, 0.08f)), P(new Vector3(0.45f, 0.8f, 0.3f)), P(new Vector3(-0.45f, 0.8f, 0.3f)), 0.08f, 3, new VertexMaterial(MaterialCatalog.Cloth.Index, new Colour(cloak)), new VertexMaterial(MaterialCatalog.Cloth.Index, new Colour(cloak * 0.6f)), (u, v) => MathF.Sin(u * 11f) * 0.04f);
                // Gashed open by claws, three long rents dark down its front and the straw coming out of them.
                for (int c = 0; c < 3; c++)
                {
                    Vector3 from = P(new Vector3(-0.15f + c * 0.12f, 1.45f, 0.31f)), to = P(new Vector3(-0.05f + c * 0.12f, 1.0f, 0.33f));
                    Kit.Plank(b, from, to, front, 0.04f, 0.01f, Dark, 0f);
                    Kit.Plank(b, to, to + front * 0.25f - up * 0.3f + across * random.Range(-0.1f, 0.1f), up, 0.06f, 0.01f, straw, 0f);
                }
            }
        }

        /// <summary>The midden the pen's dung and straw go on, steaming, with a fork left stuck in it. Numbers: radius.</summary>
        public static void Midden(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float radius = Math.Clamp(spec.Metres(0), 1f, 3f);
            Kit.Heap(b, new Vector3(0f, Ground - 0.1f, 0f), new Vector2(radius, radius * 0.8f), radius * 0.45f, random.Float(), new VertexMaterial(MaterialCatalog.Mud.Index, new Colour(0.18f, 0.13f, 0.08f)), random, 5, 14);
            Kit.Heap(b, new Vector3(radius * 0.2f, Ground + radius * 0.25f, 0f), new Vector2(radius * 0.6f, radius * 0.5f), radius * 0.2f, random.Float(), Straw(random, 0.55f), random, 3, 10);
            Vector3 fork = new Vector3(-radius * 0.3f, radius * 0.35f, radius * 0.1f);
            Vector3 dir = Vector3.Normalize(new Vector3(0.3f, 1f, 0.2f));
            Kit.Pole(b, fork - dir * 0.3f, fork + dir * 1.5f, 0.025f, Weathered(random));
            for (int k = -1; k <= 1; k++) Kit.Pole(b, fork - dir * 0.3f + new Vector3(k * 0.07f, 0f, 0f), fork - dir * 0.7f + new Vector3(k * 0.08f, 0f, 0f), 0.012f, LandIron(random), 0.004f);
        }
    }
}

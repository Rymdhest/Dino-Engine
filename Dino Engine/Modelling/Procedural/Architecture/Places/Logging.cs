using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.Modelling.Procedural.Architecture.Places.PlaceMaterials;

namespace Dino_Engine.Modelling.Procedural.Architecture.Places
{
    /// <summary>
    /// A logging camp (PLACES.md), felling timber for the Reich's pens with the hands it took: stumps where the wood was, their faces pale and their bark the wood's own; the trunks felled and
    /// limbed; the logs decked by the road with their cut ends out; the sawpit with a trunk over it and the long saw in it; boards stacked to season; the tools and the grindstone; a charcoal clamp
    /// smouldering under its turf; and the high wheels a log is slung under to be dragged out. A camp's wood is the wood round it, so its bark and colour come in each id. Headless.
    /// </summary>
    internal static class Logging
    {
        /// <summary>The bark an id carries: its kind and its colour, per mille, at <paramref name="first"/> and the three after it.</summary>
        private static VertexMaterial BarkOf(in PlaceSpec spec, int first) => Bark((BarkKind)Math.Clamp(spec.Int(first), 0, (int)BarkKind.Stem), new Vector3(spec.Int(first + 1), spec.Int(first + 2), spec.Int(first + 3)) / 1000f);

        // The wood inside, pale where it's freshly cut, darker at the heart.
        private static VertexMaterial Sap(WorldRandom random, float shade = 1f) => new VertexMaterial(MaterialCatalog.Wood.Index, new Colour(new Vector3(0.7f, 0.56f, 0.38f) * shade * random.Range(0.92f, 1.05f)));
        private static VertexMaterial Heart(WorldRandom random) => new VertexMaterial(MaterialCatalog.Wood.Index, new Colour(new Vector3(0.5f, 0.34f, 0.2f) * random.Range(0.9f, 1.05f)));

        // A cut face: the pale disc of it, the heart darker, a ring between, facing `normal`.
        private static void CutFace(MeshBuilder b, Vector3 centre, Vector3 normal, float radius, WorldRandom random)
        {
            Kit.Lathe(b, centre - normal * 0.01f, normal, 0.02f, Kit.Profile((0f, radius * 0.98f), (1f, 0f)), 12, 1, Sap(random));
            Kit.Lathe(b, centre + normal * 0.004f, normal, 0.02f, Kit.Profile((0f, radius * 0.4f), (1f, 0f)), 10, 1, Heart(random));
            b.Tube(centre + normal * 0.004f, centre + normal * 0.012f, radius * 0.7f, radius * 0.7f, 12, Sap(random, 0.82f), capped: false);
        }

        /// <summary>A stump: its roots flaring into the ground, its bark the wood's, and its top cut level but for the hinge where it broke as it fell.</summary>
        public static void Stump(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float r = Math.Clamp(spec.Metres(0), 0.2f, 1.6f), h = Math.Clamp(spec.Metres(1), 0.3f, 2f);
            VertexMaterial bark = BarkOf(spec, 2);
            Kit.Lathe(b, new Vector3(0f, -0.4f, 0f), Vector3.UnitY, h + 0.4f, Kit.Profile((0f, r * 1.45f), (0.25f, r * 1.12f), (0.6f, r), (1f, r * 0.98f)), 12, 4, bark);
            // Roots running out and down into the ground.
            int roots = random.Int(4, 6);
            for (int k = 0; k < roots; k++)
            {
                float a = k * MathF.Tau / roots + random.Range(-0.3f, 0.3f);
                Vector3 dir = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a));
                Kit.Pole(b, dir * r * 0.8f + Vector3.UnitY * 0.25f, dir * r * random.Range(2f, 2.8f) - Vector3.UnitY * 0.2f, r * 0.3f, bark, r * 0.08f);
            }
            CutFace(b, new Vector3(0f, h, 0f), Vector3.UnitY, r * 0.97f, random);
            // The hinge: a lip of splinters standing along one side, where it tore.
            float side = random.Float() * MathF.Tau;
            Vector3 across = new Vector3(MathF.Cos(side), 0f, MathF.Sin(side)), along = new Vector3(-across.Z, 0f, across.X);
            for (int k = -2; k <= 2; k++) Kit.Pole(b, new Vector3(0f, h, 0f) + across * (r * 0.15f) + along * (k * r * 0.18f), new Vector3(0f, h + random.Range(0.1f, 0.35f), 0f) + across * (r * 0.15f) + along * (k * r * 0.18f), r * 0.07f, Sap(random, 0.9f), 0.01f);
        }

        /// <summary>A felled trunk along x, limbed, its butt cut, its top end where it was taken off, the stubs of its branches left: its bark the wood's.</summary>
        public static void Log(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float length = Math.Clamp(spec.Metres(0), 2f, 24f), r = Math.Clamp(spec.Metres(1), 0.15f, 1.4f);
            VertexMaterial bark = BarkOf(spec, 2);
            float top = r * random.Range(0.55f, 0.75f);
            Vector3 butt = new Vector3(-length * 0.5f, Ground + r - 0.12f, 0f), tip = new Vector3(length * 0.5f, Ground + top - 0.08f, 0f);
            Kit.Pole(b, butt, tip, r, bark, top, capped: false);
            CutFace(b, butt, -Vector3.UnitX, r, random);
            CutFace(b, tip, Vector3.UnitX, top, random);
            // The stubs of its lopped branches, along its top half, whorled on a conifer.
            int stubs = (int)(length / 1.6f);
            for (int k = 1; k < stubs; k++)
            {
                float t = k / (float)stubs;
                if (t < 0.3f || random.Chance(0.35f)) continue;
                float rr = MathHelper.Lerp(r, top, t);
                float a = random.Range(0.4f, 2.7f);
                Vector3 at = Vector3.Lerp(butt, tip, t);
                Vector3 dir = new Vector3(0.3f, MathF.Sin(a), MathF.Cos(a)).Normalized();
                Kit.Pole(b, at + dir * rr * 0.8f, at + dir * (rr + random.Range(0.15f, 0.35f)), rr * 0.18f, bark, rr * 0.14f);
                CutFace(b, at + dir * (rr + 0.3f), dir, rr * 0.14f, random);
            }
            // Now and then the axe left in it.
            if (!random.Chance(0.35f)) return;
            Vector3 axe = Vector3.Lerp(butt, tip, random.Range(0.3f, 0.6f)) + Vector3.UnitY * r * 0.9f;
            Kit.Box(b, axe, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(0.05f, 0.2f, 0.16f), 0.01f, LandIron(random));
            Kit.Pole(b, axe + Vector3.UnitY * 0.1f, axe + new Vector3(0.1f, 0.4f, 1.1f), 0.05f, Timber(random, 1.1f));
        }

        /// <summary>Logs decked by the road to be carted, butts out along +z, in rows each a log fewer than the one under it, on skids.</summary>
        public static void LogDeck(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float length = Math.Clamp(spec.Metres(0), 3f, 16f);
            int rows = Math.Clamp(spec.Int(1), 1, 5);
            VertexMaterial bark = BarkOf(spec, 2);
            float r = random.Range(0.4f, 0.55f);
            foreach (float z in new[] { -length * 0.3f, length * 0.3f }) Kit.Plank(b, new Vector3(-rows * r * 1.2f - 0.6f, Ground + 0.1f, z), new Vector3(rows * r * 1.2f + 0.6f, Ground + 0.1f, z), Vector3.UnitY, 0.3f, 0.25f, Weathered(random));
            for (int row = 0; row < rows; row++)
            {
                int logs = rows - row + 1;
                for (int k = 0; k < logs; k++)
                {
                    float x = (k - (logs - 1) * 0.5f) * r * 2.02f;
                    float y = Ground + 0.25f + r + row * r * 1.72f;
                    float rr = r * random.Range(0.85f, 1.1f);
                    float jut = random.Range(-0.4f, 0.4f);
                    Vector3 front = new Vector3(x, y, length * 0.5f + jut), back = new Vector3(x, y, -length * 0.5f + jut);
                    Kit.Pole(b, back, front, rr, bark, rr * 0.92f, capped: false);
                    CutFace(b, front, Vector3.UnitZ, rr * 0.92f, random);
                    CutFace(b, back, -Vector3.UnitZ, rr, random);
                }
            }
            // Wedges holding the bottom row.
            foreach (float s in new[] { -1f, 1f }) Kit.Block(b, new Vector3(s * (rows + 1) * r * 1.05f, Ground + 0.4f, length * 0.3f), new Vector3(0.15f, 0.18f, 0.25f), 0f, 0.02f, Fresh(random));
        }

        /// <summary>A sawpit: the pit dug square and dark, boarded at its rim, a trunk on two trestles over it, the long saw through it and the sawdust heaped in the pit and round it.</summary>
        public static void Sawpit(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            VertexMaterial wood = Timber(random), fresh = Fresh(random), iron = LandIron(random);
            const float Long = 6.5f, Wide = 1.8f;
            // The pit: dark, boarded round.
            Kit.Face(b, new Vector3(-Long * 0.5f, Ground + 0.02f, Wide * 0.5f), new Vector3(Long * 0.5f, Ground + 0.02f, Wide * 0.5f), new Vector3(Long * 0.5f, Ground + 0.02f, -Wide * 0.5f), new Vector3(-Long * 0.5f, Ground + 0.02f, -Wide * 0.5f), Vector3.UnitY, Dark);
            foreach (float s in new[] { -1f, 1f })
            {
                Kit.Plank(b, new Vector3(-Long * 0.5f - 0.3f, Ground + 0.1f, s * (Wide * 0.5f + 0.15f)), new Vector3(Long * 0.5f + 0.3f, Ground + 0.1f, s * (Wide * 0.5f + 0.15f)), Vector3.UnitY, 0.3f, 0.2f, wood);
                Kit.Plank(b, new Vector3(s * (Long * 0.5f + 0.15f), Ground + 0.1f, -Wide * 0.5f), new Vector3(s * (Long * 0.5f + 0.15f), Ground + 0.1f, Wide * 0.5f), Vector3.UnitY, 0.3f, 0.2f, wood);
            }
            // The trestles and the trunk on them, half sawn into boards.
            foreach (float x in new[] { -Long * 0.32f, Long * 0.32f })
            {
                Kit.Plank(b, new Vector3(x, Ground + 0.9f, -Wide * 0.6f), new Vector3(x, Ground + 0.9f, Wide * 0.6f), Vector3.UnitY, 0.3f, 0.3f, wood);
            }
            float r = random.Range(0.42f, 0.55f);
            Vector3 a = new Vector3(-Long * 0.55f, Ground + 1.05f + r, 0f), c = new Vector3(Long * 0.55f, Ground + 1.05f + r, 0f);
            Kit.Pole(b, a, c, r, Timber(random, 0.7f), r * 0.95f, capped: false);
            CutFace(b, a, -Vector3.UnitX, r, random);
            CutFace(b, c, Vector3.UnitX, r * 0.95f, random);
            // The saw, standing through the log, its handle over it and its other handle down in the pit.
            float sx = random.Range(-1.2f, 1.2f);
            Kit.Box(b, new Vector3(sx, Ground + 1.05f + r, 0f), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(0.18f, 1.5f, 0.01f), 0f, iron);
            Kit.Plank(b, new Vector3(sx, Ground + 2.6f + r, -0.4f), new Vector3(sx, Ground + 2.6f + r, 0.4f), Vector3.UnitY, 0.08f, 0.08f, wood);
            Kit.Heap(b, new Vector3(sx, Ground - 0.05f, 0f), new Vector2(1f, 0.7f), 0.25f, 0f, Fresh(random, 1.1f), random, 2, 10);
            Kit.Heap(b, new Vector3(Long * 0.5f + 1.2f, Ground - 0.05f, 0.6f), new Vector2(1.1f, 0.9f), 0.3f, 0f, Fresh(random, 1.1f), random, 2, 10);
        }

        /// <summary>Sawn boards stacked to season, layer on layer with sticks between, a few boards on top against the rain.</summary>
        public static void PlankStack(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float length = Math.Clamp(spec.Metres(0), 2f, 10f);
            VertexMaterial board = Fresh(random), stick = Timber(random, 0.85f);
            int layers = random.Int(5, 8);
            const float Wide = 2.2f, Board = 0.42f;
            float y = Ground + 0.35f;
            foreach (float x in new[] { -length * 0.4f, 0f, length * 0.4f }) Kit.Span(b, new Vector3(x - 0.15f, Ground - 0.1f, -Wide * 0.55f), new Vector3(x + 0.15f, y, Wide * 0.55f), 0.02f, stick);
            for (int layer = 0; layer < layers; layer++)
            {
                int boards = (int)(Wide / Board);
                for (int k = 0; k < boards; k++)
                {
                    float z = -Wide * 0.5f + (k + 0.5f) * Board;
                    float jut = random.Range(-0.15f, 0.15f);
                    Kit.Span(b, new Vector3(-length * 0.5f + jut, y, z - Board * 0.46f), new Vector3(length * 0.5f + jut, y + 0.1f, z + Board * 0.46f), 0.01f, layer == layers - 1 ? Weathered(random) : board);
                }
                y += 0.1f;
                if (layer < layers - 1)
                {
                    foreach (float x in new[] { -length * 0.4f, 0f, length * 0.4f }) Kit.Span(b, new Vector3(x - 0.05f, y, -Wide * 0.52f), new Vector3(x + 0.05f, y + 0.06f, Wide * 0.52f), 0f, stick);
                    y += 0.06f;
                }
            }
        }

        /// <summary>A rack of the camp's tools: axes, the crosscut saws hung by their handles, wedges and mauls; and the grindstone beside it on its frame, with its trough.</summary>
        public static void ToolRack(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            VertexMaterial wood = Timber(random), iron = LandIron(random), handle = Timber(random, 1.15f);
            const float Long = 3f, High = 2.1f;
            foreach (float x in new[] { -Long * 0.5f, Long * 0.5f }) Kit.Plank(b, new Vector3(x, 0f, 0f), new Vector3(x, High, 0f), Vector3.UnitX, 0.14f, 0.14f, wood);
            Kit.Plank(b, new Vector3(-Long * 0.5f - 0.1f, High, 0f), new Vector3(Long * 0.5f + 0.1f, High, 0f), Vector3.UnitY, 0.12f, 0.12f, wood);
            Kit.Plank(b, new Vector3(-Long * 0.5f, 0.8f, 0.05f), new Vector3(Long * 0.5f, 0.8f, 0.05f), Vector3.UnitY, 0.1f, 0.08f, wood);
            // Axes leaning on the low rail, their heads up.
            for (int k = 0; k < 3; k++)
            {
                float x = -Long * 0.35f + k * 0.45f;
                Vector3 foot = new Vector3(x, Ground, 0.5f), head = new Vector3(x + 0.05f, 1.4f, 0.12f);
                Kit.Pole(b, foot, head, 0.035f, handle);
                Kit.Box(b, head + new Vector3(0.12f, 0.05f, 0f), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(0.18f, 0.08f, 0.03f), 0.01f, iron);
            }
            // Two saws hung from the top rail.
            for (int k = 0; k < 2; k++)
            {
                float x = Long * 0.12f + k * 0.6f;
                Kit.Box(b, new Vector3(x, High - 0.9f, 0.08f), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(0.08f, 0.85f, 0.008f), 0f, iron);
                Kit.Plank(b, new Vector3(x - 0.15f, High - 0.05f, 0.08f), new Vector3(x + 0.15f, High - 0.05f, 0.08f), Vector3.UnitY, 0.05f, 0.05f, handle);
            }
            // The grindstone on its frame, a trough under it.
            Vector3 g = new Vector3(Long * 0.5f + 1.5f, 0f, 0.3f);
            foreach (float s in new[] { -0.35f, 0.35f }) Kit.Plank(b, g + new Vector3(s, 0f, -0.3f), g + new Vector3(s, 1.1f, 0f), Vector3.UnitX, 0.1f, 0.1f, wood);
            Kit.Lathe(b, g + new Vector3(-0.15f, 1.15f, 0f), Vector3.UnitX, 0.3f, Kit.Profile((0f, 0.55f), (1f, 0.55f)), 14, 1, Dressed(new Vector3(0.5f, 0.48f, 0.44f), random));
            Kit.Pole(b, g + new Vector3(-0.5f, 1.15f, 0f), g + new Vector3(0.5f, 1.15f, 0f), 0.04f, iron);
            Kit.Span(b, g + new Vector3(-0.3f, 0.2f, -0.35f), g + new Vector3(0.3f, 0.55f, 0.35f), 0.02f, wood);
        }

        /// <summary>A charcoal clamp: wood stacked in a dome under turf and earth, smouldering, smoke from its vents (its chimneys), a ladder against it, and charcoal raked out at its foot.</summary>
        public static void CharcoalClamp(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float radius = ClampRadius(spec);
            Kit.Heap(b, new Vector3(0f, Ground - 0.2f, 0f), new Vector2(radius), radius * 0.7f, random.Float(), Turf(0.75f), random, 5, 16);
            foreach (Vector3 vent in VentsOf(radius)) Kit.Lathe(b, vent - Vector3.UnitY * 0.2f, Vector3.UnitY, 0.25f, Kit.Profile((0f, 0.26f), (1f, 0.2f)), 6, 1, Flat(new Vector3(0.05f, 0.04f, 0.04f)));
            // A ladder against it, and the charcoal raked out at its foot, black.
            Vector3 foot = new Vector3(radius * 0.95f, 0f, -radius * 0.2f), top = new Vector3(radius * 0.35f, radius * 0.65f, -radius * 0.1f);
            Vector3 side = new Vector3(0f, 0f, 0.3f);
            Kit.Pole(b, foot + side, top + side, 0.05f, Weathered(random));
            Kit.Pole(b, foot - side, top - side, 0.05f, Weathered(random));
            for (float t = 0.15f; t < 1f; t += 0.18f) Kit.Pole(b, Vector3.Lerp(foot, top, t) + side, Vector3.Lerp(foot, top, t) - side, 0.035f, Weathered(random));
            Kit.Heap(b, new Vector3(-radius * 1.05f, Ground - 0.05f, radius * 0.3f), new Vector2(1f, 0.7f), 0.35f, random.Float(), Flat(new Vector3(0.06f, 0.055f, 0.05f)), random, 3, 10);
        }

        /// <summary>How wide a clamp's id says it is.</summary>
        public static float ClampRadius(in PlaceSpec spec) => Math.Clamp(spec.Metres(0), 1.5f, 5f);

        /// <summary>How far a clamp of a size reaches from its middle, the charcoal raked out at its foot and all.</summary>
        public static float ClampReach(float radius) => radius * 1.1f + 1f;

        /// <summary>Where a clamp of a size smokes from, which is where its chimneys are.</summary>
        public static Vector3[] VentsOf(float radius) => new[] { new Vector3(0f, Ground + radius * 0.68f, 0f), new Vector3(radius * 0.4f, Ground + radius * 0.5f, radius * 0.3f) };

        /// <summary>A pair of high wheels on an axle, a log slung under it by chains, the tongue out along +z to whatever drags it.</summary>
        public static void Skidder(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            VertexMaterial wood = Timber(random), iron = spec.Int(0) == 1 ? Iron(random) : LandIron(random);
            const float Radius = 1.5f, Track = 2.6f;
            Vector3 axle = new Vector3(0f, Radius, 0f);
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 hub = axle + Vector3.UnitX * (s * Track * 0.5f);
                const int Rim = 14;
                for (int k = 0; k < Rim; k++)
                {
                    float a0 = k * MathF.Tau / Rim, a1 = (k + 1) * MathF.Tau / Rim;
                    Kit.Pole(b, hub + new Vector3(0f, MathF.Cos(a0) * Radius, MathF.Sin(a0) * Radius), hub + new Vector3(0f, MathF.Cos(a1) * Radius, MathF.Sin(a1) * Radius), 0.09f, wood, capped: false);
                    if (k % 2 == 0) Kit.Pole(b, hub, hub + new Vector3(0f, MathF.Cos(a0) * Radius, MathF.Sin(a0) * Radius), 0.05f, wood);
                }
                b.Tube(hub - Vector3.UnitX * 0.2f, hub + Vector3.UnitX * 0.2f, 0.2f, 0.2f, 8, iron);
            }
            Kit.Pole(b, axle - Vector3.UnitX * (Track * 0.5f), axle + Vector3.UnitX * (Track * 0.5f), 0.14f, iron);
            Kit.Plank(b, axle, new Vector3(0f, 0.7f, 5.5f), Vector3.UnitY, 0.2f, 0.18f, wood);
            // The log under it, its front end lifted by the chains.
            float r = random.Range(0.4f, 0.55f);
            Vector3 front = new Vector3(0f, Radius - 0.6f, 0.4f), back = new Vector3(0f, Ground + r, -random.Range(5f, 7f));
            Kit.Pole(b, back, front, r, Timber(random, 0.6f), r * 0.9f);
            Kit.Chain(b, axle - Vector3.UnitX * 0.4f, front + new Vector3(-0.3f, r * 0.5f, 0f), 0.1f, 0.24f, iron);
            Kit.Chain(b, axle + Vector3.UnitX * 0.4f, front + new Vector3(0.3f, r * 0.5f, 0f), 0.1f, 0.24f, iron);
        }
    }
}

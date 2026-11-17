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
    /// A castle of the land's, as the citadel is (SEATS.md): curtain walls with a walk and battlements, round towers with a beacon, a timber hoarding or the Reich's pennant on them,
    /// gatehouses with the portcullis up, flights of steps, the wall a terrace stands on, the hall and keep, and the stall the Reich built against it for its tyrant. Its stone is the
    /// land's own, as an id carries it: rubble walling with its trim dressed, slate on its roofs, and nothing of the Reich's but what the Reich added, in black iron, oxblood and brass.
    /// Model +z is a piece's front and y = 0 where people stand at its foot. Headless.
    /// </summary>
    internal static class Fortress
    {
        public const int WallWalk = 0, WallLow = 1, WallBreached = 2;
        public const int TowerBeacon = 0, TowerHoarding = 1, TowerPennant = 2;
        public const int GateGreat = 0, GateLesser = 1, GateOpen = 2;

        /// <summary>Half of how deep a gatehouse's way through is.</summary>
        public const float GateHalfDepth = 2.4f;

        // ---------------------------------------------------------------- The curtain

        /// <summary>
        /// A stretch of curtain wall along x, its outside to -z: battered at its foot, a buttress at its -x end (so one stands at every joint of a run), and along its top a walk behind a
        /// parapet on corbels with merlons; or low, with a coping and a pier; or breached and closed with the Reich's riveted plate on timber, its stones still lying where they fell.
        /// </summary>
        public static void Wall(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float length = Math.Clamp(spec.Metres(0), 3f, 30f), height = Math.Clamp(spec.Metres(1), 2f, 16f), thick = Math.Clamp(spec.Metres(2), 1f, 4f);
            int form = spec.Int(3);
            Vector3 stone = SeatKit.StoneOf(spec, 4);
            float hl = length * 0.5f, ht = thick * 0.5f;
            VertexMaterial walling = SeatKit.Walling(stone, random), paving = SeatKit.Paving(stone);
            bool low = form == WallLow, breached = form == WallBreached;
            float gap = MathF.Min(2.7f, hl * 0.45f);

            if (!breached) SeatKit.WallBox(b, new Vector3(-hl, -0.8f, -ht), new Vector3(hl, height, ht), walling, low ? walling : paving);
            else
            {
                SeatKit.WallBox(b, new Vector3(-hl, -0.8f, -ht), new Vector3(-gap, height, ht), walling, paving);
                SeatKit.WallBox(b, new Vector3(gap, -0.8f, -ht), new Vector3(hl, height, ht), walling, paving);
                SeatKit.WallBox(b, new Vector3(-gap, -0.8f, -ht), new Vector3(gap, height * 0.28f, ht), walling);
                Breach(b, gap, height, ht, stone, random);
            }

            // Its batter, on the outside, and the ends of it.
            float skirt = low ? 1.3f : 2.4f, splay = low ? 0.5f : 0.9f;
            SeatKit.Courses(b, new Vector3(hl, -0.8f, -ht - splay), new Vector3(-hl, -0.8f, -ht - splay), new Vector3(-hl, skirt, -ht - 0.02f), new Vector3(hl, skirt, -ht - 0.02f), walling);
            foreach (float s in new[] { -1f, 1f })
            {
                Kit.Face(b, new Vector3(s * hl, -0.8f, -ht - splay), new Vector3(s * hl, -0.8f, -ht), new Vector3(s * hl, skirt, -ht), new Vector3(s, 0f, 0f), walling);
            }

            if (low)
            {
                SeatKit.Course(b, new Vector3(-hl, height + 0.14f, 0f), new Vector3(hl, height + 0.14f, 0f), new Vector2(0.14f, ht + 0.12f), stone, random, 1.05f);
                // A pier at its end, a little over the wall, with a cap.
                int courses = Math.Max(2, (int)((height + 0.6f) / 0.8f));
                float course = (height + 0.6f) / courses;
                for (int i = 0; i < courses; i++)
                {
                    float y0 = i * course - (i == 0 ? 0.8f : 0f);
                    Kit.Span(b, new Vector3(-hl - 0.62f, y0, -ht - 0.28f), new Vector3(-hl + 0.62f, (i + 1) * course - 0.012f, ht + 0.28f), 0.045f, SeatKit.AshlarFacing(stone, random, y0));
                }
                Kit.Span(b, new Vector3(-hl - 0.75f, height + 0.6f, -ht - 0.4f), new Vector3(-hl + 0.75f, height + 0.86f, ht + 0.4f), 0.05f, SeatKit.AshlarFacing(stone, random, 6f, 1.06f));
                return;
            }

            // The buttress at its end, in three stages.
            {
                float top = height * 0.8f;
                int courses = Math.Max(3, (int)(top / 0.85f));
                float course = top / courses;
                for (int i = 0; i < courses; i++)
                {
                    float t = (i + 0.5f) / courses;
                    float reach = t < 0.4f ? 1.3f : t < 0.75f ? 0.95f : 0.6f;
                    float half = 0.74f + (i % 2 == 0 ? 0.07f : 0f);
                    float y0 = i * course - (i == 0 ? 0.8f : 0f);
                    Kit.Span(b, new Vector3(-hl - half, y0, -ht - reach), new Vector3(-hl + half, (i + 1) * course - 0.012f, -ht + 0.05f), 0.045f, SeatKit.AshlarFacing(stone, random, y0));
                }
            }

            // The parapet out on its corbels, the merlons, and the kerb along the walk's inner edge.
            void Crown(float x0, float x1)
            {
                SeatKit.Corbels(b, new Vector3(x0, height - 0.2f, -ht), new Vector3(x1, height - 0.2f, -ht), -Vector3.UnitZ, 0.45f, stone, random);
                SeatKit.Battlements(b, new Vector3(x0, height - 0.2f, -ht - 0.12f), new Vector3(x1, height - 0.2f, -ht - 0.12f), 0.66f, 1.35f, 1.6f, stone, random);
                SeatKit.WallBox(b, new Vector3(x0, height, ht - 0.32f), new Vector3(x1, height + 0.5f, ht), SeatKit.Walling(stone, random, 0.97f));
            }
            if (breached)
            {
                Crown(-hl, -gap - 0.4f);
                Crown(gap + 0.4f, hl);
            }
            else Crown(-hl, hl);
        }

        // The Reich's way of mending what its beasts broke: plate riveted over the gap on a frame of timber, shored from inside, and the fallen stone left lying.
        private static void Breach(MeshBuilder b, float gap, float height, float ht, Vector3 stone, WorldRandom random)
        {
            // The ragged ends of the wall either side of it.
            for (int k = 0; k < 4; k++)
            {
                float y0 = height * (0.28f + 0.17f * k), y1 = y0 + height * 0.17f;
                foreach (float s in new[] { -1f, 1f })
                {
                    float reach = (3 - k) * 0.4f + random.Range(0.05f, 0.3f);
                    float xa = s * gap, xb = s * (gap - reach);
                    Kit.Span(b, new Vector3(MathF.Min(xa, xb), y0, -ht + 0.05f), new Vector3(MathF.Max(xa, xb), y1 - 0.02f, ht - 0.05f), 0.05f, SeatKit.AshlarFacing(stone, random, y0, 0.86f));
                }
            }
            VertexMaterial plate = Plating(random), iron = Iron(random), timber = Timber(random, 0.8f);
            float foot = height * 0.2f, top = height * 0.95f;
            Kit.Span(b, new Vector3(-gap - 0.7f, foot, -ht - 0.16f), new Vector3(gap + 0.7f, top, -ht + 0.02f), 0.02f, plate);
            for (int i = 0; i < 4; i++)
            {
                float x = -gap - 0.4f + (gap * 2f + 0.8f) * i / 3f;
                Kit.Span(b, new Vector3(x - 0.13f, foot - 0.3f, -ht - 0.3f), new Vector3(x + 0.13f, top + 0.5f, -ht - 0.14f), 0.02f, iron);
                // The spike each rib ends in.
                Kit.Pole(b, new Vector3(x, top + 0.5f, -ht - 0.22f), new Vector3(x, top + 1.3f, -ht - 0.22f), 0.09f, iron, 0.01f);
            }
            foreach (float y in new[] { foot + 0.5f, (foot + top) * 0.5f, top - 0.4f }) Kit.Span(b, new Vector3(-gap - 0.75f, y - 0.14f, -ht - 0.26f), new Vector3(gap + 0.75f, y + 0.14f, -ht - 0.15f), 0.02f, iron);
            // The frame behind it, and the shores down to the yard.
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * gap * 0.72f;
                Kit.Span(b, new Vector3(x - 0.17f, height * 0.28f, -ht + 0.04f), new Vector3(x + 0.17f, top, -ht + 0.38f), 0.02f, timber);
                Kit.Plank(b, new Vector3(x, top * 0.82f, -ht + 0.4f), new Vector3(x, -0.2f, ht + 3f), Vector3.UnitZ, 0.3f, 0.3f, timber);
            }
            Kit.Plank(b, new Vector3(-gap, top * 0.6f, -ht + 0.42f), new Vector3(gap, top * 0.6f, -ht + 0.42f), Vector3.UnitZ, 0.26f, 0.22f, timber);
            // What came down, outside and in.
            VertexMaterial rubble = SeatKit.Ashlar(stone, random, 0.5f, 0.82f);
            Kit.Heap(b, new Vector3(random.Range(-0.6f, 0.6f), Ground, -ht - 2.3f), new Vector2(gap + 0.9f, 1.9f), 1.15f, 0f, rubble, random, 4, 12);
            for (int i = 0; i < 7; i++)
            {
                bool inside = i >= 5;
                Vector3 at = new Vector3(random.Range(-gap - 1.5f, gap + 1.5f), Ground + random.Range(0.25f, 0.5f), inside ? ht + random.Range(0.8f, 2.4f) : -ht - random.Range(3.2f, 5.6f));
                Kit.Block(b, at, new Vector3(random.Range(0.35f, 0.7f), random.Range(0.25f, 0.42f), random.Range(0.3f, 0.5f)), random.Float() * MathF.Tau, 0.05f, SeatKit.AshlarFacing(stone, random, 0.4f, 0.9f));
            }
        }

        /// <summary>
        /// A round tower of a castle: its drum, and on it battlements round a beacon burning, a timber hoarding out over its head under a cone of shingles, or battlements and the Reich's
        /// pennant on a mast.
        /// </summary>
        public static void Tower(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            float radius = Math.Clamp(spec.Metres(0), 2.2f, 8f), height = Math.Clamp(spec.Metres(1), 5f, 28f);
            int form = spec.Int(2);
            Vector3 stone = SeatKit.StoneOf(spec, 3);
            int around = radius > 4.6f ? 20 : 16;
            float head = SeatKit.Drum(b, Vector3.Zero, radius, height, stone, random, around);
            Vector3 top = Vector3.UnitY * height;
            if (form == TowerHoarding)
            {
                Hoarding(b, top, head, random);
                return;
            }
            SeatKit.DrumHead(b, top, head, stone, random, around);
            if (form == TowerBeacon) SeatKit.Beacon(b, top + Vector3.UnitY * 0.1f, 1.5f, random, lights, 150f, 28f);
            else
            {
                VertexMaterial iron = Iron(random);
                Kit.Pole(b, top, top + Vector3.UnitY * 8.6f, 0.13f, iron, 0.08f);
                Kit.Pole(b, top + new Vector3(-1.25f, 7.9f, 0.12f), top + new Vector3(1.25f, 7.9f, 0.12f), 0.05f, iron);
                Kit.Lathe(b, top + Vector3.UnitY * 8.6f, Vector3.UnitY, 0.6f, Kit.Profile((0f, 0.1f), (0.4f, 0.17f), (1f, 0f)), 6, 2, Brass(random));
                SeatKit.Banner(b, top + new Vector3(0f, 7.85f, 0.2f), Vector3.UnitZ, 2.2f, 5.6f, random);
            }
        }

        // A hoarding: a gallery of boards out over the head of a tower on braces, shuttered, under a cone of shingles gone silver.
        private static void Hoarding(MeshBuilder b, Vector3 top, float head, WorldRandom random)
        {
            const int Around = 12;
            VertexMaterial boards = Weathered(random, 1.02f), dark = Timber(random, 0.62f);
            float reach = head + 0.95f;
            for (int k = 0; k < Around; k++)
            {
                float a = k * MathF.Tau / Around;
                Vector3 d = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a));
                Kit.Plank(b, top + d * (head - 0.35f) - Vector3.UnitY * 2f, top + d * (reach - 0.1f) - Vector3.UnitY * 0.15f, d, 0.22f, 0.22f, dark);
            }
            Kit.Lathe(b, top - Vector3.UnitY * 0.22f, Vector3.UnitY, 0.24f, _ => reach, Around, 1, dark);
            Kit.Lathe(b, top + Vector3.UnitY * 0.03f, Vector3.UnitY, 2.3f, _ => reach - 0.06f, Around, 1, boards, openEnd: true);
            // Its shutters, propped open: the dark behind them.
            for (int k = 0; k < Around; k += 2)
            {
                float a = (k + 0.5f) * MathF.Tau / Around;
                Vector3 d = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a)), t = new Vector3(-MathF.Sin(a), 0f, MathF.Cos(a));
                Vector3 c = top + d * ((reach - 0.06f) * MathF.Cos(MathF.PI / Around) + 0.03f) + Vector3.UnitY * 1.3f;
                Kit.Face(b, c - t * 0.62f - Vector3.UnitY * 0.42f, c + t * 0.62f - Vector3.UnitY * 0.42f, c + t * 0.62f + Vector3.UnitY * 0.42f, c - t * 0.62f + Vector3.UnitY * 0.42f, d, Dark);
                Kit.Sheet(b, c - t * 0.66f + Vector3.UnitY * 0.44f, c + t * 0.66f + Vector3.UnitY * 0.44f, c + t * 0.66f + d * 0.6f + Vector3.UnitY * 0.1f, c - t * 0.66f + d * 0.6f + Vector3.UnitY * 0.1f, boards, dark);
            }
            SeatKit.Cone(b, top + Vector3.UnitY * 2.3f, reach + 0.75f, (reach + 0.75f) * 0.92f, SeatKit.Shingles(random), random, Around);
        }

        // ---------------------------------------------------------------- The gatehouse

        /// <summary>How far either side of its middle a gatehouse's towers stand, which what's solid about it needs (Hitboxes).</summary>
        public static float GateTowerOffset(float way, float radius) => way * 0.5f + radius + 0.15f;

        /// <summary>
        /// A gatehouse, its way through along z and its outside to -z: a pointed arch through a block of walling between two round towers, the portcullis drawn up into it with its
        /// spikes showing, a parapet out on corbels over the way in, the Reich's mark in brass on oxblood over the arch on both faces, and torches either side of it.
        /// </summary>
        public static void Gate(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            float way = Math.Clamp(spec.Metres(0), 4f, 10f), radius = Math.Clamp(spec.Metres(1), 2.4f, 6f);
            bool great = spec.Int(3) == GateGreat;
            Vector3 stone = SeatKit.StoneOf(spec, 4);
            if (spec.Int(3) == GateOpen)
            {
                OpenGate(b, way, radius, Math.Clamp(spec.Metres(2), 5f, 16f), stone, random, lights);
                return;
            }
            float hw = way * 0.5f, dz = GateHalfDepth, w2 = hw + 0.9f;
            const float Sharp = 0.25f;
            float spring = great ? 4.3f : 3.6f, rise = SeatKit.ArchRise(hw, Sharp);
            float crown = spring + rise * (hw + 0.6f) / hw;
            float deck = crown + 2.3f;
            float towers = MathF.Max(Math.Clamp(spec.Metres(2), 7f, 24f), deck + 2.4f);
            VertexMaterial walling = SeatKit.Walling(stone, random), shade = SeatKit.Walling(stone, random, 0.6f), iron = Iron(random);

            // Its two faces, each with the arch through it and the stones round it.
            (Vector3 Origin, Vector3 U)[] faces = { (new Vector3(w2, 0f, -dz), -Vector3.UnitX), (new Vector3(-w2, 0f, dz), Vector3.UnitX) };
            foreach ((Vector3 origin, Vector3 u) in faces)
            {
                SeatKit.ArchedPanel(b, origin, u, Vector3.UnitY, 0f, w2 * 2f, deck, w2, hw, -0.8f, spring, Sharp, walling, 12, -0.8f);
                SeatKit.ArchStones(b, origin, u, Vector3.UnitY, w2, hw, 0f, spring, Sharp, 0.6f, 0.14f, 0.4f, stone, random, 13);
            }
            // The way through: its sides and the underside of its arch, in shadow.
            b.Panel(new Vector3(-hw, 0f, dz), -Vector3.UnitZ, Vector3.UnitY, 0f, dz * 2f, -0.8f, spring, shade, new Vector4(0f, dz * 2f, -0.8f, spring));
            b.Panel(new Vector3(hw, 0f, -dz), Vector3.UnitZ, Vector3.UnitY, 0f, dz * 2f, -0.8f, spring, shade, new Vector4(0f, dz * 2f, -0.8f, spring));
            const int Steps = 12;
            for (int k = 0; k < Steps; k++)
            {
                Vector2 p0 = SeatKit.ArchPoint(hw, Sharp, k / (float)Steps), p1 = SeatKit.ArchPoint(hw, Sharp, (k + 1f) / Steps);
                Vector3 want = new Vector3(-(p0.X + p1.X), -(p0.Y + p1.Y) - 0.2f, 0f);
                Kit.Face(b, new Vector3(p0.X, spring + p0.Y, -dz), new Vector3(p1.X, spring + p1.Y, -dz), new Vector3(p1.X, spring + p1.Y, dz), new Vector3(p0.X, spring + p0.Y, dz), want, shade);
            }

            // The portcullis, drawn up: its bars down to a little below the springing, each ending in a spike.
            float grate = -dz + 1.1f, foot = spring - 0.9f;
            for (float x = -hw + 0.45f; x < hw - 0.2f; x += 0.75f)
            {
                float top = spring + SeatKit.ArchHeightAt(hw, Sharp, x) + 0.2f;
                Kit.Span(b, new Vector3(x - 0.07f, foot, grate - 0.07f), new Vector3(x + 0.07f, top, grate + 0.07f), 0f, iron);
                Kit.Pole(b, new Vector3(x, foot, grate), new Vector3(x, foot - 0.45f, grate), 0.08f, iron, 0.01f);
            }
            foreach (float y in new[] { foot + 0.12f, foot + 0.9f, foot + 1.7f, foot + 2.5f })
            {
                float dy = MathF.Max(y - spring, 0f), rho = hw * (1f + Sharp);
                float reach = dy <= 0f ? hw : MathF.Sqrt(MathF.Max(rho * rho - dy * dy, 0f)) - Sharp * hw;
                if (reach > 0.5f) Kit.Span(b, new Vector3(-reach, y - 0.06f, grate - 0.09f), new Vector3(reach, y + 0.06f, grate + 0.09f), 0f, iron);
            }

            // The deck over the way, its parapet out over the way in on corbels, and the one towards the yard.
            Kit.Face(b, new Vector3(-w2, deck, -dz), new Vector3(w2, deck, -dz), new Vector3(w2, deck, dz), new Vector3(-w2, deck, dz), Vector3.UnitY, SeatKit.Paving(stone));
            SeatKit.Corbels(b, new Vector3(-hw - 0.5f, deck - 0.2f, -dz), new Vector3(hw + 0.5f, deck - 0.2f, -dz), -Vector3.UnitZ, 0.5f, stone, random, 0.9f);
            SeatKit.Battlements(b, new Vector3(-w2, deck - 0.2f, -dz - 0.14f), new Vector3(w2, deck - 0.2f, -dz - 0.14f), 0.7f, 1.35f, 1.5f, stone, random, 2.2f);
            SeatKit.Battlements(b, new Vector3(-w2, deck, dz - 0.3f), new Vector3(w2, deck, dz - 0.3f), 0.6f, 1.15f, 1.5f, stone, random, 2.2f);

            // The Reich's mark over the arch, on both faces, and its torches.
            float markAt = crown + 1.05f;
            Kit.Mark(b, new Vector3(0f, markAt, -dz - 0.17f), -Vector3.UnitX, Vector3.UnitY, 1.6f, 0.05f, random);
            Kit.Mark(b, new Vector3(0f, markAt, dz + 0.17f), Vector3.UnitX, Vector3.UnitY, 1.6f, 0.05f, random);
            foreach (float s in new[] { -1f, 1f })
            {
                SeatKit.WallTorch(b, new Vector3(s * (hw + 0.78f), 3.5f, -dz - 0.14f), -Vector3.UnitZ, random, lights);
                if (great) SeatKit.WallTorch(b, new Vector3(s * (hw + 0.78f), 3.5f, dz + 0.14f), Vector3.UnitZ, random, lights);
            }

            // Its towers.
            float offset = GateTowerOffset(way, radius);
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 at = new Vector3(s * offset, 0f, -0.6f);
                float head = SeatKit.Drum(b, at, radius, towers, stone, random, 16);
                SeatKit.DrumHead(b, at + Vector3.UnitY * towers, head, stone, random, 16);
            }
        }

        /// <summary>How far each leaf of an open gate stands open, radians from shut, and how far in from the wall's line its hinges are.</summary>
        public static readonly float[] GateLeaves = { 1.3f, 1.05f };
        public const float GateHinge = 0.4f;

        // An open gate: two towers and nothing over the way between them but an arch of iron with the Reich's mark hung in it, so that nothing stands between the camera and
        // whoever comes in; its two leaves of iron bars standing open into the yard, and a torch on each tower.
        private static void OpenGate(MeshBuilder b, float way, float radius, float height, Vector3 stone, WorldRandom random, List<AssetLight> lights)
        {
            float hw = way * 0.5f, offset = GateTowerOffset(way, radius);
            VertexMaterial iron = Iron(random), brass = Brass(random);
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 at = new Vector3(s * offset, 0f, 0f);
                float head = SeatKit.Drum(b, at, radius, height, stone, random, 16);
                SeatKit.DrumHead(b, at + Vector3.UnitY * height, head, stone, random, 16);
                SeatKit.WallTorch(b, at + new Vector3(0f, 3.6f, -radius - 0.05f), -Vector3.UnitZ, random, lights);
            }
            // The arch: two bars of iron from tower to tower, pickets between them, and the mark hung from its middle on both faces.
            float foot = height - 1.9f, crown = height + 0.5f;
            var upper = new List<Vector3>();
            var lower = new List<Vector3>();
            const int Steps = 10;
            for (int i = 0; i <= Steps; i++)
            {
                float t = i / (float)Steps;
                float x = MathHelper.Lerp(-hw - 0.4f, hw + 0.4f, t), y = MathHelper.Lerp(foot, crown, MathF.Sin(t * MathF.PI));
                upper.Add(new Vector3(x, y, 0f));
                lower.Add(new Vector3(x, y - 0.75f, 0f));
            }
            Kit.Bend(b, upper, 0.09f, 0.09f, iron);
            Kit.Bend(b, lower, 0.075f, 0.075f, iron);
            for (int i = 1; i < Steps; i++) Kit.Pole(b, lower[i], upper[i] + Vector3.UnitY * 0.4f, 0.04f, iron, 0.006f);
            Vector3 mark = new Vector3(0f, crown - 1.7f, 0f);
            Kit.Mark(b, mark - Vector3.UnitZ * 0.04f, -Vector3.UnitX, Vector3.UnitY, 1.45f, 0.04f, random);
            Kit.Mark(b, mark + Vector3.UnitZ * 0.04f, Vector3.UnitX, Vector3.UnitY, 1.45f, 0.04f, random);
            foreach (float s in new[] { -1f, 1f }) Kit.Chain(b, lower[Steps / 2 + (int)s] , mark + new Vector3(s * 0.75f, 0.7f, 0f), 0.02f, 0.2f, iron);

            // Its leaves, of bars with a point on each, open into the yard.
            const float Tall = 4.4f;
            for (int k = 0; k < 2; k++)
            {
                float s = k == 0 ? -1f : 1f, open = GateLeaves[k];
                Vector3 hinge = new Vector3(s * hw, 0f, GateHinge);
                Vector3 swing = new Vector3(-s * MathF.Cos(open), 0f, MathF.Sin(open));
                float length = hw - 0.2f;
                int count = Math.Max(2, (int)MathF.Round(length / 0.55f));
                for (int i = 0; i <= count; i++)
                {
                    Vector3 at = hinge + swing * (length * i / count);
                    float top = Tall + 0.5f * MathF.Sin(MathF.PI * 0.5f * i / count);
                    Kit.Pole(b, at + Vector3.UnitY * 0.12f, at + Vector3.UnitY * top, 0.05f, iron, capped: false);
                    Kit.Pole(b, at + Vector3.UnitY * top, at + Vector3.UnitY * (top + 0.45f), 0.07f, iron, 0.005f);
                }
                foreach (float y in new[] { 0.5f, 2.3f, 4f }) Kit.Plank(b, hinge + Vector3.UnitY * y, hinge + swing * length + Vector3.UnitY * y, Vector3.UnitY, 0.16f, 0.1f, iron, 0f);
                Kit.Box(b, hinge + swing * (length * 0.5f) + Vector3.UnitY * 2.3f, swing, Vector3.UnitY, Vector3.Cross(swing, Vector3.UnitY), new Vector3(0.5f, 0.5f, 0.07f), 0.02f, brass);
                Kit.Pole(b, hinge - Vector3.UnitY * 0.3f, hinge + Vector3.UnitY * (Tall + 0.3f), 0.11f, iron);
            }
        }

        // ---------------------------------------------------------------- Steps and terraces

        /// <summary>How far a flight runs back from its bottom riser to the end of its cheeks.</summary>
        public static float StepsRun(int count, float tread) => count * tread + 0.7f;

        /// <summary>
        /// A flight of steps from the middle of its bottom riser up towards -z, each step in stones of its own, between two cheek walls as high as what it climbs to and a parapet, each
        /// ending in a pier at the flight's foot. The ground under it is a ramp along the inner corners of its steps (SQUARE.md), and its treads are the height the ground is drawn at above that.
        /// </summary>
        public static void Steps(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float width = Math.Clamp(spec.Metres(0), 3f, 26f);
            int count = Math.Clamp(spec.Int(1), 1, 14);
            float rise = Math.Clamp(spec.Metres(2), 0.15f, 0.6f), tread = Math.Clamp(spec.Metres(3), 0.5f, 2f), cheek = Math.Clamp(spec.Metres(4), 0.6f, 3f);
            Vector3 stone = SeatKit.StoneOf(spec, 5);
            float half = width * 0.5f;
            for (int i = 0; i < count; i++)
            {
                WorldRandom step = random.Fork(i);
                float front = -i * tread + 0.035f, back = -(i + 1) * tread - (i == count - 1 ? 0.7f : 0.32f);
                float top = Ground + (i + 1) * rise, bottom = Ground + i * rise - 0.45f;
                float x = -half - 0.2f - step.Range(0f, 1.2f);
                while (x < half + 0.2f)
                {
                    float piece = step.Range(1.5f, 2.4f);
                    float x0 = MathF.Max(x, -half - 0.2f), x1 = MathF.Min(x + piece, half + 0.2f);
                    if (x1 - x0 > 0.3f) Kit.Span(b, new Vector3(x0 + 0.006f, bottom, back), new Vector3(x1 - 0.006f, top, front), 0.05f, SeatKit.AshlarFacing(stone, step, top, 1.04f));
                    x += piece;
                }
            }
            // The cheeks, their copings, and the piers they end in.
            float run = StepsRun(count, tread), high = Ground + count * rise + 0.95f;
            VertexMaterial walling = SeatKit.Walling(stone, random);
            foreach (float s in new[] { -1f, 1f })
            {
                float x0 = s > 0f ? half : -half - cheek, x1 = x0 + cheek;
                SeatKit.WallBox(b, new Vector3(x0, -0.8f, -run), new Vector3(x1, high, 0.5f), walling);
                SeatKit.Course(b, new Vector3((x0 + x1) * 0.5f, high + 0.13f, -run), new Vector3((x0 + x1) * 0.5f, high + 0.13f, 0.5f), new Vector2(0.13f, cheek * 0.5f + 0.1f), stone, random, 1.05f);
                int courses = Math.Max(3, (int)((high + 0.5f) / 0.8f));
                float course = (high + 0.5f) / courses;
                for (int i = 0; i < courses; i++)
                {
                    float y0 = i * course - (i == 0 ? 0.8f : 0f);
                    Kit.Span(b, new Vector3(x0 - 0.22f, y0, 0.5f), new Vector3(x1 + 0.22f, (i + 1) * course - 0.012f, 2.2f), 0.05f, SeatKit.AshlarFacing(stone, random, y0));
                }
                Kit.Span(b, new Vector3(x0 - 0.36f, high + 0.5f, 0.36f), new Vector3(x1 + 0.36f, high + 0.78f, 2.34f), 0.05f, SeatKit.AshlarFacing(stone, random, 6f, 1.06f));
            }
        }

        /// <summary>How far back from its face a terrace's wall is paved, which covers where the ground it holds up comes down to the ground below (SQUARE.md).</summary>
        public const float RevetDepth = 2f;

        /// <summary>
        /// A wall along x that holds a terrace up behind it (-z), its face to +z: battered walling between piers, paved along its top as far back as the ground it holds takes to come
        /// up, and a parapet along its edge.
        /// </summary>
        public static void Revet(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float length = Math.Clamp(spec.Metres(0), 2f, 30f), rise = Math.Clamp(spec.Metres(1), 0.6f, 6f);
            Vector3 stone = SeatKit.StoneOf(spec, 2);
            float hl = length * 0.5f, top = rise + Ground;
            VertexMaterial walling = SeatKit.Walling(stone, random), paving = SeatKit.Paving(stone);
            SeatKit.Courses(b, new Vector3(-hl, -0.8f, 0.4f), new Vector3(hl, -0.8f, 0.4f), new Vector3(hl, top, 0f), new Vector3(-hl, top, 0f), walling);
            foreach (float s in new[] { -1f, 1f }) Kit.Face(b, new Vector3(s * hl, -0.8f, 0.4f), new Vector3(s * hl, top, 0f), new Vector3(s * hl, top, -RevetDepth), new Vector3(s * hl, -0.8f, -RevetDepth), new Vector3(s, 0f, 0f), walling);
            Kit.Face(b, new Vector3(-hl, top, 0f), new Vector3(hl, top, 0f), new Vector3(hl, top, -RevetDepth), new Vector3(-hl, top, -RevetDepth), Vector3.UnitY, paving);
            Kit.Face(b, new Vector3(-hl, top, -RevetDepth), new Vector3(hl, top, -RevetDepth), new Vector3(hl, top - 0.7f, -RevetDepth), new Vector3(-hl, top - 0.7f, -RevetDepth), -Vector3.UnitZ, paving);
            // The parapet along its edge, and its coping.
            SeatKit.WallBox(b, new Vector3(-hl, top, -0.62f), new Vector3(hl, top + 0.95f, -0.08f), SeatKit.Walling(stone, random, 0.98f));
            SeatKit.Course(b, new Vector3(-hl, top + 1.08f, -0.35f), new Vector3(hl, top + 1.08f, -0.35f), new Vector2(0.13f, 0.4f), stone, random, 1.05f);
            // Piers up its face.
            int piers = Math.Max(1, (int)MathF.Round(length / 6.5f));
            for (int i = 0; i < piers; i++)
            {
                float x = -hl + (i + 0.5f) * length / piers;
                int courses = Math.Max(2, (int)((top + 1.3f) / 0.8f));
                float course = (top + 1.3f) / courses;
                for (int k = 0; k < courses; k++)
                {
                    float y0 = k * course - (k == 0 ? 0.8f : 0f);
                    Kit.Span(b, new Vector3(x - 0.6f, y0, -0.7f), new Vector3(x + 0.6f, (k + 1) * course - 0.012f, 0.72f), 0.05f, SeatKit.AshlarFacing(stone, random, y0));
                }
                Kit.Span(b, new Vector3(x - 0.74f, top + 1.3f, -0.84f), new Vector3(x + 0.74f, top + 1.56f, 0.86f), 0.05f, SeatKit.AshlarFacing(stone, random, 6f, 1.06f));
            }
        }

        // ---------------------------------------------------------------- The hall and keep

        public const float HallFloor = 1f;          // Metres its floor stands over the ground, on its plinth.
        public const float PorchHalf = 4.6f, PorchDepth = 3.4f, WingWidth = 5.5f, TurretRadius = 2.3f;

        private static float RoofRise(float depth) => (depth * 0.5f + 0.7f) * 1.05f;

        /// <summary>Where a hall's chimneys end, which is where its smoke comes from.</summary>
        public static Vector3[] HallChimneys(float width, float depth, float eaves)
        {
            float ridge = eaves + RoofRise(depth);
            return new[] { new Vector3(-width * 0.3f, ridge + 2.6f, -depth * 0.24f), new Vector3(width * 0.31f, ridge + 3.2f, -depth * 0.27f) };
        }

        /// <summary>
        /// The citadel's hall and its keep, their front towards +z: a long hall of the land's stone under a steep roof of pale slate between crow-stepped gables, tall windows lit from inside
        /// between its buttresses, a gabled porch over its great door, the Reich's plate on what's left of the leaves and its mark over them, a turret at each front corner, a lean-to at
        /// each end; and out of its middle the keep, square, battlemented, a turret at each corner, the land's arms gone from its face and the Reich's banner down it, another on its mast.
        /// </summary>
        public static void Hall(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            float w = Math.Clamp(spec.Metres(0), 18f, 34f), d = Math.Clamp(spec.Metres(1), 10f, 18f), eaves = Math.Clamp(spec.Metres(2), 8f, 16f);
            float keep = Math.Clamp(spec.Metres(3), eaves + 9f, 38f);
            Vector3 stone = SeatKit.StoneOf(spec, 4);
            float hx = w * 0.5f, hz = d * 0.5f;
            float rise = RoofRise(d), ridge = eaves + rise, ez = hz + 0.7f;
            VertexMaterial walling = SeatKit.Walling(stone, random), dim = SeatKit.Walling(stone, random, 0.9f), iron = Iron(random);
            VertexMaterial slate = SeatKit.Slate();
            Func<Vector3, Colour> weather = SeatKit.Weathering(SeatKit.SlateColour, random, eaves, ridge);
            Vector3 X = Vector3.UnitX, Y = Vector3.UnitY, Z = Vector3.UnitZ;

            // The plinth, and the three walls nobody looks at twice.
            SeatKit.WallBox(b, new Vector3(-hx - 0.45f, -0.8f, -hz - 0.45f), new Vector3(hx + 0.45f, HallFloor, hz + 0.45f), SeatKit.Walling(stone, random, 0.8f));
            b.Panel(new Vector3(hx, 0f, -hz), -X, Y, 0f, w, HallFloor, eaves, dim, new Vector4(0f, w, HallFloor, eaves));
            b.Panel(new Vector3(-hx, 0f, -hz), Z, Y, 0f, d, HallFloor, eaves, dim, new Vector4(0f, d, HallFloor, eaves));
            b.Panel(new Vector3(hx, 0f, hz), -Z, Y, 0f, d, HallFloor, eaves, dim, new Vector4(0f, d, HallFloor, eaves));

            // The front: three bays either side of the porch, a tall window in each, a buttress between each and the next.
            Vector3 front = new Vector3(-hx, 0f, hz);
            const int Bays = 3;
            float bay = (hx - PorchHalf) / Bays;
            float sill = HallFloor + 2.3f, spring = eaves - 4.3f;
            int lit = 0;
            for (int side = 0; side < 2; side++)
            {
                for (int i = 0; i < Bays; i++)
                {
                    float u0 = side == 0 ? i * bay : hx + PorchHalf + i * bay, centre = u0 + bay * 0.5f;
                    SeatKit.ArchedPanel(b, front, X, Y, u0, u0 + bay, eaves, centre, 0.8f, sill, spring, 0.6f, walling, 8, HallFloor);
                    bool glows = random.Chance(0.72f);
                    SeatKit.Window(b, front, X, Y, centre, 0.8f, sill, spring, 0.6f, glows, stone, random);
                    if (glows && lit < 3 && i != 1)
                    {
                        lights.Add(AssetLight.Window(new Vector3(-hx + centre, (sill + spring) * 0.5f, hz + 0.7f), Z) with { Intensity = 46f, Radius = 12f });
                        lit++;
                    }
                }
            }
            b.Panel(front, X, Y, hx - PorchHalf, hx + PorchHalf, HallFloor, eaves, dim, new Vector4(hx - PorchHalf, hx + PorchHalf, HallFloor, eaves));
            // The corners have their turrets and the porch its own walls, so only the joints between bays have one.
            for (int i = 1; i < Bays; i++)
            {
                Buttress(b, new Vector3(-hx + i * bay, 0f, hz), Z, eaves - 1.3f, stone, random);
                Buttress(b, new Vector3(PorchHalf + i * bay, 0f, hz), Z, eaves - 1.3f, stone, random);
            }
            SeatKit.Course(b, new Vector3(-hx, eaves - 0.22f, hz + 0.24f), new Vector3(hx, eaves - 0.22f, hz + 0.24f), new Vector2(0.2f, 0.3f), stone, random, 1.03f);

            // The roof, either side of the keep, and in front of it up to its face.
            float kx = MathF.Min(5.6f, hx * 0.45f), kz1 = hz - 2.6f, kz0 = -hz - 0.8f;
            void Slopes(float x0, float x1)
            {
                int n = Math.Max(2, (int)((x1 - x0) / 2.5f));
                b.Surface(n, 4, (u, v) => new Vector3(MathHelper.Lerp(x0, x1, u), MathHelper.Lerp(eaves, ridge, v), MathHelper.Lerp(ez, 0f, v)), slate, weather);
                b.Surface(n, 4, (u, v) => new Vector3(MathHelper.Lerp(x1, x0, u), MathHelper.Lerp(eaves, ridge, v), MathHelper.Lerp(-ez, 0f, v)), slate, weather);
                // Its ridge in lead, and the Reich's iron along it.
                Kit.Plank(b, new Vector3(x0, ridge + 0.06f, 0f), new Vector3(x1, ridge + 0.06f, 0f), Y, 0.5f, 0.2f, SeatKit.Lead(random), 0.03f);
                for (float x = x0 + 0.8f; x < x1 - 0.4f; x += 1.3f) Kit.Pole(b, new Vector3(x, ridge + 0.1f, 0f), new Vector3(x, ridge + 0.95f, 0f), 0.07f, iron, 0.01f);
            }
            Slopes(-hx + 0.4f, -kx);
            Slopes(kx, hx - 0.4f);
            float meets = eaves + rise * (1f - kz1 / ez);
            b.Surface(3, 2, (u, v) => new Vector3(MathHelper.Lerp(-kx, kx, u), MathHelper.Lerp(eaves, meets, v), MathHelper.Lerp(ez, kz1, v)), slate, weather);

            // The gables, crow-stepped, each step a stone.
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 a = new Vector3(s * hx, eaves, s * hz), c = new Vector3(s * hx, eaves, -s * hz), apex = new Vector3(s * hx, ridge + 0.2f, 0f);
                b.Triangle(a, c, apex, walling);
                const int Crow = 6;
                for (int i = 0; i < Crow; i++)
                {
                    float z0 = ez * (1f - i / (float)Crow), z1 = ez * (1f - (i + 1f) / Crow);
                    float y0 = eaves + rise * i / Crow - 0.7f, y1 = eaves + rise * (i + 1f) / Crow + 0.55f;
                    foreach (float zs in new[] { -1f, 1f })
                    {
                        Kit.Span(b, new Vector3(s * hx - 0.42f, y0, MathF.Min(zs * z0, zs * z1)), new Vector3(s * hx + 0.42f, y1, MathF.Max(zs * z0, zs * z1)), 0.05f, SeatKit.AshlarFacing(stone, random, y0));
                    }
                }
                Kit.Span(b, new Vector3(s * hx - 0.46f, ridge - 0.3f, -0.5f), new Vector3(s * hx + 0.46f, ridge + 1.3f, 0.5f), 0.06f, SeatKit.AshlarFacing(stone, random, 9f, 1.05f));
            }

            Keep(b, kx, kz0, kz1, eaves, keep, stone, random);
            Porch(b, hz, stone, random, lights);

            // A turret at each front corner, under a cone of slate.
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 at = new Vector3(s * hx, 0f, hz);
                float high = eaves + 4.6f;
                SeatKit.Drum(b, at, TurretRadius, high, stone, random, 12, 0.4f);
                SeatKit.Cone(b, at + Y * (high - 0.15f), TurretRadius + 1.1f, 5.4f, new VertexMaterial(MaterialCatalog.Slate.Index, new Colour(SeatKit.SlateColour * random.Range(0.94f, 1.04f))), random, 12);
                // A slit lit near its head, towards the yard.
                Vector3 slit = at + new Vector3(0f, high - 2.6f, (TurretRadius + 0.42f) * MathF.Cos(MathF.PI / 12f) + 0.04f);
                Kit.Face(b, slit + new Vector3(-0.16f, -0.7f, 0f), slit + new Vector3(0.16f, -0.7f, 0f), slit + new Vector3(0.16f, 0.7f, 0f), slit + new Vector3(-0.16f, 0.7f, 0f), Z, LampGlow(0.7f));
            }

            // A lean-to at each end, under slate.
            float wz0 = -hz + 1f, wz1 = hz - 2.6f;
            foreach (float s in new[] { -1f, 1f })
            {
                float x0 = s > 0f ? hx - 0.1f : -hx - WingWidth, x1 = x0 + WingWidth + 0.1f;
                SeatKit.WallBox(b, new Vector3(x0, -0.8f, wz0), new Vector3(x1, 6.2f, wz1), SeatKit.Walling(stone, random, 0.95f));
                float outer = s * (hx + WingWidth + 0.45f), inner = s * hx;
                if (s > 0f) b.Surface(3, 2, (u, v) => new Vector3(MathHelper.Lerp(outer, inner, v), MathHelper.Lerp(6.1f, 8.3f, v), MathHelper.Lerp(wz1 + 0.45f, wz0 - 0.45f, u)), slate, weather);
                else b.Surface(3, 2, (u, v) => new Vector3(MathHelper.Lerp(outer, inner, v), MathHelper.Lerp(6.1f, 8.3f, v), MathHelper.Lerp(wz0 - 0.45f, wz1 + 0.45f, u)), slate, weather);
                // Its door, shut, and a lit slit beside it.
                float mid = s * (hx + WingWidth * 0.5f);
                Kit.Span(b, new Vector3(mid - 0.9f, 0f, wz1 - 0.05f), new Vector3(mid + 0.9f, 3.3f, wz1 + 0.07f), 0.02f, Timber(random, 0.7f));
                Kit.Span(b, new Vector3(mid - 1.05f, 3.3f, wz1 - 0.05f), new Vector3(mid + 1.05f, 3.65f, wz1 + 0.16f), 0.04f, SeatKit.AshlarFacing(stone, random, 3.3f));
                foreach (float y in new[] { 0.8f, 2.4f }) Kit.Span(b, new Vector3(mid - 0.9f, y - 0.1f, wz1 + 0.07f), new Vector3(mid + 0.9f, y + 0.1f, wz1 + 0.1f), 0f, iron);
            }

            // Its chimneys, out of the back slope.
            foreach (Vector3 top in HallChimneys(w, d, eaves))
            {
                float from = eaves + rise * (1f - MathF.Abs(top.Z) / ez) - 0.6f;
                SeatKit.WallBox(b, new Vector3(top.X - 0.85f, from, top.Z - 0.85f), new Vector3(top.X + 0.85f, top.Y - 0.7f, top.Z + 0.85f), dim);
                Kit.Span(b, new Vector3(top.X - 1f, top.Y - 0.7f, top.Z - 1f), new Vector3(top.X + 1f, top.Y - 0.42f, top.Z + 1f), 0.05f, SeatKit.AshlarFacing(stone, random, 9f, 1.04f));
                Kit.Lathe(b, new Vector3(top.X, top.Y - 0.42f, top.Z), Y, 0.42f, Kit.Profile((0f, 0.5f), (1f, 0.4f)), 8, 1, Flat(new Vector3(0.1f, 0.08f, 0.07f)));
            }
        }

        // A buttress up a wall's face: dressed courses, stepping back twice as it climbs.
        private static void Buttress(MeshBuilder b, Vector3 foot, Vector3 outward, float top, Vector3 stone, WorldRandom random)
        {
            Vector3 along = Vector3.Cross(Vector3.UnitY, outward).Normalized();
            int courses = Math.Max(3, (int)(top / 0.85f));
            float course = top / courses;
            for (int i = 0; i < courses; i++)
            {
                float t = (i + 0.5f) / courses;
                float reach = t < 0.38f ? 1.05f : t < 0.72f ? 0.75f : 0.45f;
                float half = 0.5f + (i % 2 == 0 ? 0.05f : 0f);
                float y0 = i * course - (i == 0 ? 0.8f : 0f), y1 = (i + 1) * course - 0.012f;
                Kit.Box(b, foot + outward * (reach * 0.5f - 0.03f) + Vector3.UnitY * ((y0 + y1) * 0.5f), along, Vector3.UnitY, outward, new Vector3(half, (y1 - y0) * 0.5f, reach * 0.5f + 0.03f), 0.045f, SeatKit.AshlarFacing(stone, random, y0));
            }
        }

        // The keep: a square tower out of the hall's middle, its head corbelled out under battlements with a turret at each corner, two windows lit high in its face, the Reich's
        // great banner down between them where the land's arms were, and its mast.
        private static void Keep(MeshBuilder b, float kx, float z0, float z1, float from, float top, Vector3 stone, WorldRandom random)
        {
            Vector3 X = Vector3.UnitX, Y = Vector3.UnitY, Z = Vector3.UnitZ;
            VertexMaterial walling = SeatKit.Walling(stone, random, 1.02f), iron = Iron(random);
            float low = from - 1f, depth = z1 - z0;
            // Its face in two halves, a window in each.
            float sill = top - 9.4f, spring = top - 6.4f;
            Vector3 face = new Vector3(-kx, 0f, z1);
            for (int i = 0; i < 2; i++)
            {
                float u0 = i * kx, centre = u0 + kx * 0.5f + (i == 0 ? -0.5f : 0.5f);
                SeatKit.ArchedPanel(b, face, X, Y, u0, u0 + kx, top, centre, 0.7f, sill, spring, 0.6f, walling, 8, low);
                SeatKit.Window(b, face, X, Y, centre, 0.7f, sill, spring, 0.6f, true, stone, random);
            }
            b.Panel(new Vector3(kx, 0f, z0), -X, Y, 0f, kx * 2f, low, top, walling, new Vector4(0f, kx * 2f, low, top));
            b.Panel(new Vector3(-kx, 0f, z0), Z, Y, 0f, depth, low, top, walling, new Vector4(0f, depth, low, top));
            b.Panel(new Vector3(kx, 0f, z1), -Z, Y, 0f, depth, low, top, walling, new Vector4(0f, depth, low, top));
            // Long and short stones up its two front corners.
            int quoins = (int)((top - from) / 0.8f);
            for (int i = 0; i < quoins; i++)
            {
                float y0 = from + i * 0.8f + 0.3f;
                float longer = i % 2 == 0 ? 0.75f : 0.45f;
                foreach (float s in new[] { -1f, 1f })
                {
                    float xa = s * (kx + 0.06f), xb = s * (kx - longer);
                    Kit.Span(b, new Vector3(MathF.Min(xa, xb), y0, z1 - (i % 2 == 0 ? 0.45f : 0.75f)), new Vector3(MathF.Max(xa, xb), y0 + 0.78f, z1 + 0.06f), 0.04f, SeatKit.AshlarFacing(stone, random, y0));
                }
            }

            // Its head: corbels, the parapet out on them, merlons all round, and the roof inside them.
            Vector3[] corners = { new Vector3(-kx, top, z1), new Vector3(kx, top, z1), new Vector3(kx, top, z0), new Vector3(-kx, top, z0) };
            Vector3[] outs = { Z, X, -Z, -X };
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = corners[i], c = corners[(i + 1) % 4], o = outs[i];
                SeatKit.Corbels(b, a - Y * 0.2f, c - Y * 0.2f, o, 0.45f, stone, random, 1f);
                Vector3 along = (c - a).Normalized();
                SeatKit.Battlements(b, a - along * 0.45f + o * 0.12f - Y * 0.2f, c + along * 0.45f + o * 0.12f - Y * 0.2f, 0.66f, 1.35f, 1.6f, stone, random, 2.3f);
            }
            Kit.Face(b, corners[0], corners[1], corners[2], corners[3], Y, SeatKit.Paving(stone, 0.95f));
            foreach (Vector3 corner in corners)
            {
                Vector3 at = corner + new Vector3(MathF.Sign(corner.X) * 0.25f, -3.2f, corner.Z > (z0 + z1) * 0.5f ? 0.25f : -0.25f);
                Kit.Lathe(b, at, Y, 5.6f, Kit.Profile((0f, 0.12f), (0.24f, 1.25f), (1f, 1.25f)), 10, 4, SeatKit.Walling(stone, random), openEnd: true);
                SeatKit.Cone(b, at + Y * 5.5f, 1.65f, 2.9f, SeatKit.Lead(random), random, 10);
            }

            // The Reich's banner down its face, between the windows, on a bar of iron, and another on the mast.
            float bar = top - 1.5f;
            Kit.Pole(b, new Vector3(-1.9f, bar, z1 + 0.5f), new Vector3(1.9f, bar, z1 + 0.5f), 0.07f, iron);
            foreach (float s in new[] { -1f, 1f }) Kit.Pole(b, new Vector3(s * 1.8f, bar, z1), new Vector3(s * 1.8f, bar, z1 + 0.5f), 0.05f, iron);
            SeatKit.Banner(b, new Vector3(0f, bar - 0.05f, z1 + 0.5f), Z, 3.2f, MathF.Max(top - from - 6.5f, 4f), random, 2.3f);
            Vector3 mast = new Vector3(0f, top, (z0 + z1) * 0.5f);
            Kit.Pole(b, mast, mast + Y * 10.5f, 0.16f, iron, 0.09f);
            Kit.Pole(b, mast + new Vector3(-1.5f, 9.7f, 0.14f), mast + new Vector3(1.5f, 9.7f, 0.14f), 0.06f, iron);
            Kit.Lathe(b, mast + Y * 10.5f, Y, 0.7f, Kit.Profile((0f, 0.12f), (0.4f, 0.2f), (1f, 0f)), 6, 2, Brass(random));
            SeatKit.Banner(b, mast + new Vector3(0f, 9.65f, 0.22f), Z, 2.7f, 6.8f, random);
        }

        // The porch over the great door: a gabled forebuilding, the arch of the door through its front, what's left of the door, steps up to it, and the Reich's mark and banners on it.
        private static void Porch(MeshBuilder b, float hz, Vector3 stone, WorldRandom random, List<AssetLight> lights)
        {
            Vector3 X = Vector3.UnitX, Y = Vector3.UnitY, Z = Vector3.UnitZ;
            VertexMaterial walling = SeatKit.Walling(stone, random, 1.03f), iron = Iron(random), plate = Plating(random), brass = Brass(random);
            float p = PorchHalf, z1 = hz + PorchDepth;
            const float High = 9.2f, Gable = 3.5f, DoorHalf = 2.5f, Spring = 4.4f, Sharp = 0.3f;
            Vector3 face = new Vector3(-p, 0f, z1);
            SeatKit.ArchedPanel(b, face, X, Y, 0f, p * 2f, High, p, DoorHalf, -0.8f, Spring, Sharp, walling, 12, -0.8f);
            SeatKit.ArchStones(b, face, X, Y, p, DoorHalf, HallFloor, Spring, Sharp, 0.55f, 0.14f, 0.5f, stone, random, 13);
            b.Triangle(new Vector3(-p, High, z1), new Vector3(p, High, z1), new Vector3(0f, High + Gable, z1), walling);
            b.Panel(new Vector3(-p, 0f, hz), Z, Y, 0f, PorchDepth, -0.8f, High, walling, new Vector4(0f, PorchDepth, -0.8f, High));
            b.Panel(new Vector3(p, 0f, z1), -Z, Y, 0f, PorchDepth, -0.8f, High, walling, new Vector4(0f, PorchDepth, -0.8f, High));
            // Its roof, running back into the hall's.
            VertexMaterial slate = SeatKit.Slate(0.97f);
            Func<Vector3, Colour> weather = SeatKit.Weathering(SeatKit.SlateColour, random, High, High + Gable);
            float back = hz - 2.4f, lip = z1 + 0.5f;
            b.Surface(2, 3, (u, v) => new Vector3(MathHelper.Lerp(-p - 0.45f, 0f, v), MathHelper.Lerp(High - 0.3f, High + Gable + 0.12f, v), MathHelper.Lerp(back, lip, u)), slate, weather);
            b.Surface(2, 3, (u, v) => new Vector3(MathHelper.Lerp(p + 0.45f, 0f, v), MathHelper.Lerp(High - 0.3f, High + Gable + 0.12f, v), MathHelper.Lerp(lip, back, u)), slate, weather);
            Kit.Plank(b, new Vector3(0f, High + Gable + 0.16f, back), new Vector3(0f, High + Gable + 0.16f, lip), Y, 0.4f, 0.18f, SeatKit.Lead(random), 0.03f);
            Kit.Mark(b, new Vector3(0f, High + Gable * 0.34f, z1 + 0.1f), X, Y, 1.7f, 0.05f, random);

            // The floor inside it, the dark of the hall beyond, and the door: one leaf shut, the other hanging open off its lower hinge, both plated by the Reich.
            Kit.Face(b, new Vector3(-p, HallFloor, hz - 0.6f), new Vector3(p, HallFloor, hz - 0.6f), new Vector3(p, HallFloor, z1 + 0.3f), new Vector3(-p, HallFloor, z1 + 0.3f), Y, SeatKit.Paving(stone, 0.8f));
            SeatKit.ArchFill(b, face, X, Y, p, DoorHalf, HallFloor, Spring, Sharp, 1.2f, Dark);
            float leaf = DoorHalf - 0.06f, tall = Spring + 0.9f - HallFloor;
            void Leaf(float side, float swing, float lean)
            {
                Vector3 hinge = new Vector3(side * DoorHalf, HallFloor, z1 - 0.35f);
                float a = swing * side;
                Vector3 ax = new Vector3(-side * MathF.Cos(a), 0f, -MathF.Sin(MathF.Abs(a))), up = new Vector3(ax.X * lean, 1f, ax.Z * lean).Normalized();
                Vector3 az = Vector3.Cross(ax, up).Normalized();
                Vector3 centre = hinge + ax * (leaf * 0.5f) + up * (tall * 0.5f);
                Kit.Box(b, centre, ax, up, az, new Vector3(leaf * 0.5f, tall * 0.5f, 0.09f), 0.02f, plate);
                foreach (float y in new[] { 0.16f, 0.5f, 0.84f })
                {
                    Kit.Box(b, hinge + ax * (leaf * 0.5f) + up * (tall * y), ax, up, az, new Vector3(leaf * 0.5f + 0.02f, 0.12f, 0.13f), 0f, iron);
                }
                Kit.Box(b, hinge + ax * (leaf * 0.86f) + up * (tall * 0.42f), ax, up, az, new Vector3(0.07f, 0.3f, 0.16f), 0.01f, brass);
            }
            Leaf(-1f, 0.12f, 0f);
            Leaf(1f, 1.05f, 0.12f);
            // The bars over the leaves, in the head of the arch.
            float head = Spring + 0.9f;
            Kit.Span(b, new Vector3(-DoorHalf, head - 0.1f, z1 - 0.42f), new Vector3(DoorHalf, head + 0.1f, z1 - 0.26f), 0f, iron);
            for (float x = -DoorHalf + 0.5f; x < DoorHalf - 0.2f; x += 0.62f)
            {
                float top = Spring + SeatKit.ArchHeightAt(DoorHalf, Sharp, x);
                if (top > head + 0.3f) Kit.Pole(b, new Vector3(x, head, z1 - 0.34f), new Vector3(x, top, z1 - 0.34f), 0.05f, iron);
            }

            // Three steps up to it.
            for (int k = 0; k < 3; k++)
            {
                float top = HallFloor * (k + 1f) / 3f + (k == 2 ? 0f : Ground * (2 - k) / 2f);
                float z0 = z1 + 0.3f + (2 - k) * 0.9f;
                WorldRandom step = random.Fork(k);
                float x = -p + 0.2f;
                while (x < p - 0.2f)
                {
                    float piece = MathF.Min(step.Range(1.6f, 2.5f), p - 0.2f - x);
                    if (piece > 0.3f) Kit.Span(b, new Vector3(x + 0.006f, -0.5f, z0 - 0.4f), new Vector3(x + piece - 0.006f, top, z0 + 0.9f), 0.05f, SeatKit.AshlarFacing(stone, step, top, 1.03f));
                    x += MathF.Max(piece, 0.3f);
                }
            }

            // The Reich's banners either side of the door, each on its bar, and a lantern over it.
            foreach (float s in new[] { -1f, 1f })
            {
                float x = s * (DoorHalf + 1.05f + 0.25f);
                Kit.Pole(b, new Vector3(x - 0.8f, High - 0.55f, z1 + 0.38f), new Vector3(x + 0.8f, High - 0.55f, z1 + 0.38f), 0.05f, iron);
                foreach (float e in new[] { -0.7f, 0.7f }) Kit.Pole(b, new Vector3(x + e, High - 0.55f, z1), new Vector3(x + e, High - 0.55f, z1 + 0.38f), 0.04f, iron);
                SeatKit.Banner(b, new Vector3(x, High - 0.6f, z1 + 0.38f), Z, 1.35f, 5.4f, random);
            }
            Kit.Pole(b, new Vector3(0f, High - 0.2f, z1), new Vector3(0f, High - 0.2f, z1 + 1.2f), 0.05f, iron);
            Kit.Lantern(b, new Vector3(0f, High - 0.22f, z1 + 1.15f), brass, lights, 1.5f);
        }

        // ---------------------------------------------------------------- The tyrant's stall

        /// <summary>Half of how wide a stall's gate is, of its own half width.</summary>
        public const float StallGateShare = 0.62f;

        /// <summary>How far each leaf of a stall's gate stands open, radians from shut.</summary>
        public static readonly float[] StallLeaves = { 1.35f, 0.95f };

        /// <summary>
        /// The stall the Reich built for its tyrant, its front towards +z: posts of whole trunks banded in iron, its back half boarded and strapped under a roof that falls to the front,
        /// its front half a cage of iron bars open to the sky, the two leaves of its gate standing open; and in it the straw it lies on, its trough, the chain it was kept on with the
        /// collar it slipped, and what's left of what it's been fed.
        /// </summary>
        public static void Stall(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            float w = Math.Clamp(spec.Metres(0), 10f, 26f), d = Math.Clamp(spec.Metres(1), 10f, 24f), h = Math.Clamp(spec.Metres(2), 5f, 12f);
            float hx = w * 0.5f, hz = d * 0.5f, gate = hx * StallGateShare;
            VertexMaterial post = Timber(random, 0.78f), boards = Weathered(random, 1.04f), iron = Iron(random), brass = Brass(random);
            Vector3 Y = Vector3.UnitY;

            // Posts, banded, and the beams they carry.
            var posts = new List<Vector2>();
            foreach (float x in new[] { -hx, hx }) foreach (float z in new[] { -hz, 0f, hz }) posts.Add(new Vector2(x, z));
            posts.Add(new Vector2(-gate, hz));
            posts.Add(new Vector2(gate, hz));
            posts.Add(new Vector2(0f, -hz));
            foreach (Vector2 at in posts)
            {
                Kit.Span(b, new Vector3(at.X - 0.3f, -0.6f, at.Y - 0.3f), new Vector3(at.X + 0.3f, h, at.Y + 0.3f), 0.05f, post);
                foreach (float y in new[] { 1.1f, h - 1.1f }) Kit.Span(b, new Vector3(at.X - 0.34f, y - 0.14f, at.Y - 0.34f), new Vector3(at.X + 0.34f, y + 0.14f, at.Y + 0.34f), 0.01f, iron);
            }
            foreach (float z in new[] { -hz, 0f, hz }) Kit.Plank(b, new Vector3(-hx - 0.55f, h + 0.22f, z), new Vector3(hx + 0.55f, h + 0.22f, z), Y, 0.5f, 0.45f, post, 0.03f);
            foreach (float x in new[] { -hx, hx }) Kit.Plank(b, new Vector3(x, h + 0.2f, -hz - 0.5f), new Vector3(x, h + 0.2f, hz + 0.5f), Y, 0.46f, 0.42f, post, 0.03f);

            // The back half: boards, strapped, and the roof.
            Kit.Span(b, new Vector3(-hx, 0f, -hz - 0.12f), new Vector3(hx, h - 0.2f, -hz + 0.12f), 0.01f, boards);
            foreach (float s in new[] { -1f, 1f }) Kit.Span(b, new Vector3(s * hx - 0.12f, 0f, -hz), new Vector3(s * hx + 0.12f, h - 0.2f, 0f), 0.01f, boards);
            foreach (float y in new[] { 1.6f, h * 0.5f, h - 1.7f })
            {
                Kit.Span(b, new Vector3(-hx, y - 0.16f, -hz + 0.12f), new Vector3(hx, y + 0.16f, -hz + 0.16f), 0f, iron);
                foreach (float s in new[] { -1f, 1f }) Kit.Span(b, new Vector3(s * hx - s * 0.16f - 0.02f, y - 0.16f, -hz), new Vector3(s * hx - s * 0.16f + 0.02f, y + 0.16f, 0f), 0f, iron);
            }
            float low = h + 0.55f, high = h + 2.5f;
            Func<Vector3, Colour> weather = SeatKit.Weathering(new Vector3(0.52f, 0.47f, 0.4f), random, low, high, 0.3f);
            b.Surface(6, 3, (u, v) => new Vector3(MathHelper.Lerp(-hx - 0.8f, hx + 0.8f, u), MathHelper.Lerp(low, high, v), MathHelper.Lerp(0.7f, -hz - 0.8f, v)), new VertexMaterial(MaterialCatalog.Shingle.Index, new Colour(1f, 1f, 1f)), weather);
            for (int i = 0; i <= 4; i++)
            {
                float x = -hx + w * i / 4f;
                Kit.Plank(b, new Vector3(x, low - 0.22f, 0.6f), new Vector3(x, high - 0.22f, -hz - 0.6f), Y, 0.24f, 0.3f, post);
            }
            Kit.Face(b, new Vector3(-hx, h - 0.2f, -hz), new Vector3(hx, h - 0.2f, -hz), new Vector3(hx, high - 0.3f, -hz), new Vector3(-hx, high - 0.3f, -hz), Vector3.UnitZ, boards);

            // The front half: bars.
            void Bars(Vector3 from, Vector3 to, float sag = 0f)
            {
                Vector3 along = to - from;
                float length = along.Length;
                if (length < 0.5f) return;
                along /= length;
                int count = Math.Max(1, (int)MathF.Round(length / 0.85f));
                for (int i = 0; i <= count; i++)
                {
                    Vector3 foot = from + along * (length * i / count) - Y * (sag * i / count);
                    Kit.Pole(b, foot + Y * 0.1f, foot + Y * (h - 0.2f), 0.085f, iron, capped: false);
                }
                foreach (float y in new[] { 1.5f, h * 0.55f, h - 0.5f }) Kit.Plank(b, from + Y * y, to + Y * (y - sag), Y, 0.2f, 0.12f, iron, 0f);
            }
            foreach (float s in new[] { -1f, 1f })
            {
                Bars(new Vector3(s * hx, 0f, 0.4f), new Vector3(s * hx, 0f, hz - 0.4f));
                Bars(new Vector3(s * (hx - 0.4f), 0f, hz), new Vector3(s * (gate + 0.4f), 0f, hz));
                // Its leaf of the gate, standing open, the further one sagging on its hinge.
                float open = StallLeaves[s < 0f ? 0 : 1];
                Vector3 hinge = new Vector3(s * gate, 0f, hz + 0.35f);
                Vector3 swing = new Vector3(-s * MathF.Cos(open), 0f, MathF.Sin(open));
                Bars(hinge + swing * 0.2f, hinge + swing * (gate - 0.2f), s > 0f ? 0.5f : 0f);
            }
            // The Reich's mark over the gate, on its board.
            Kit.Span(b, new Vector3(-1.2f, h + 0.5f, hz + 0.2f), new Vector3(1.2f, h + 2.3f, hz + 0.3f), 0.02f, Timber(random, 0.5f));
            Kit.Mark(b, new Vector3(0f, h + 1.4f, hz + 0.32f), Vector3.UnitX, Y, 1.5f, 0.04f, random);

            // Inside: straw, the trough, the chain and the collar, and bones.
            Kit.Heap(b, new Vector3(hx * 0.1f, Ground, -hz * 0.52f), new Vector2(hx * 0.78f, hz * 0.4f), 0.6f, 0f, Straw(random, 0.92f), random, 4, 14);
            Trough(b, new Vector3(-hx + 1.9f, 0f, -hz * 0.35f), 0f, MathF.Min(hz * 0.9f, 6f), random);
            Vector3 ring = new Vector3(0.34f, 2.4f, -hz + 0.2f);
            Kit.Lathe(b, ring + new Vector3(0f, 0f, 0.02f), Vector3.UnitZ, 0.14f, _ => 0.36f, 8, 1, iron);
            Vector3 collar = new Vector3(hx * 0.2f, Ground + 0.16f, hz * 0.45f);
            Kit.Chain(b, ring + new Vector3(0f, -0.3f, 0.2f), collar + new Vector3(-1.2f, 0.05f, 0f), 1.2f, 0.55f, iron);
            const int Links = 12;
            for (int k = 0; k < Links - 2; k++)
            {
                float a0 = k * MathF.Tau / Links, a1 = (k + 1) * MathF.Tau / Links;
                b.Tube(collar + new Vector3(MathF.Cos(a0) * 1.2f, 0f, MathF.Sin(a0) * 1.2f), collar + new Vector3(MathF.Cos(a1) * 1.2f, 0f, MathF.Sin(a1) * 1.2f), 0.16f, 0.16f, 6, iron, capped: k == 0 || k == Links - 3);
            }
            VertexMaterial bone = Bone(random, 0.95f);
            for (int k = 0; k < 6; k++)
            {
                Vector3 at = new Vector3(random.Range(-hx * 0.7f, hx * 0.7f), Ground + 0.1f, random.Range(0.5f, hz * 0.85f));
                float a = random.Float() * MathF.Tau, l = random.Range(0.6f, 1.4f);
                Kit.Pole(b, at, at + new Vector3(MathF.Cos(a) * l, 0f, MathF.Sin(a) * l), 0.09f, bone, 0.06f);
            }
            // Gouges down the boards of the back, where it's been at them.
            VertexMaterial raw = Fresh(random, 1.05f);
            for (int k = 0; k < 4; k++)
            {
                float x = random.Range(-hx * 0.7f, hx * 0.7f), y = random.Range(2.2f, h * 0.6f);
                for (int j = 0; j < 3; j++)
                {
                    Vector3 from = new Vector3(x + j * 0.22f, y + 1.3f, -hz + 0.135f), to = new Vector3(x + j * 0.22f + 0.5f, y, -hz + 0.135f);
                    Kit.Plank(b, from, to, Vector3.UnitZ, 0.07f, 0.02f, raw, 0f);
                }
            }
            Kit.Pole(b, new Vector3(-gate, h - 1.4f, hz + 0.3f), new Vector3(-gate, h - 1.4f, hz + 1.1f), 0.04f, iron);
            Kit.Lantern(b, new Vector3(-gate, h - 1.42f, hz + 1.05f), brass, lights, 1.3f);
        }

        /// <summary>
        /// A tyrant's trough of riveted iron on stone bearers, along z from its middle turned by <paramref name="yaw"/>, what's left of a carcass in it and the flies' share round it.
        /// </summary>
        public static void Trough(MeshBuilder b, Vector3 at, float yaw, float length, WorldRandom random)
        {
            float c = MathF.Cos(yaw), s = MathF.Sin(yaw);
            Vector3 ax = new Vector3(c, 0f, -s), az = new Vector3(s, 0f, c);
            VertexMaterial plate = Plating(random, 0.9f), iron = Iron(random);
            float hl = length * 0.5f;
            const float Wide = 0.85f, High = 1.15f;
            Vector3 P(float x, float y, float z) => at + ax * x + Vector3.UnitY * y + az * z;
            // Its two long sides leaning out, its ends, its floor.
            foreach (float side in new[] { -1f, 1f })
            {
                Kit.Solid(b, new[]
                {
                    P(side * 0.5f, 0.25f, hl), P(side * 0.58f, 0.25f, hl), P(side * (Wide + 0.08f), High, hl), P(side * Wide, High, hl),
                    P(side * 0.5f, 0.25f, -hl), P(side * 0.58f, 0.25f, -hl), P(side * (Wide + 0.08f), High, -hl), P(side * Wide, High, -hl),
                }, 0f, plate);
                Kit.Plank(b, P(side * (Wide + 0.04f), High + 0.04f, -hl - 0.06f), P(side * (Wide + 0.04f), High + 0.04f, hl + 0.06f), Vector3.UnitY, 0.2f, 0.1f, iron, 0f);
            }
            foreach (float end in new[] { -1f, 1f })
            {
                Kit.Solid(b, new[]
                {
                    P(-0.58f, 0.25f, end * hl), P(0.58f, 0.25f, end * hl), P(Wide + 0.08f, High, end * hl), P(-Wide - 0.08f, High, end * hl),
                    P(-0.58f, 0.25f, end * (hl - 0.08f)), P(0.58f, 0.25f, end * (hl - 0.08f)), P(Wide + 0.08f, High, end * (hl - 0.08f)), P(-Wide - 0.08f, High, end * (hl - 0.08f)),
                }, 0f, plate);
            }
            Kit.Box(b, P(0f, 0.3f, 0f), ax, Vector3.UnitY, az, new Vector3(0.58f, 0.06f, hl), 0f, iron);
            for (float z = -hl + 0.5f; z <= hl - 0.4f; z += MathF.Max((length - 0.9f) / 2f, 0.6f)) Kit.Box(b, P(0f, 0.05f, z), ax, Vector3.UnitY, az, new Vector3(0.75f, 0.24f, 0.28f), 0.04f, Dressed(new Vector3(0.3f, 0.3f, 0.31f), random, 0.3f));
            // What's in it.
            Kit.Heap(b, P(0f, 0.38f, random.Range(-hl * 0.4f, hl * 0.4f)), new Vector2(0.5f, hl * 0.6f), 0.5f, yaw, Flesh(0.85f), random, 3, 8);
            VertexMaterial bone = Bone(random);
            for (int k = 0; k < 4; k++)
            {
                float z = random.Range(-hl * 0.6f, hl * 0.6f);
                var rib = new List<Vector3> { P(-0.3f, 0.6f, z), P(-0.1f, 1.25f, z + 0.1f), P(0.35f, 1.5f, z + 0.15f) };
                Kit.Bend(b, rib, 0.06f, 0.03f, bone);
            }
        }
    }
}

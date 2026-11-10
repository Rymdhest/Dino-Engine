using Dino_Engine.Modelling.Model;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Halls
{
    /// <summary>
    /// What's inside a hall: everything its design says stands in it, each where it says, and what dresses its walls and fills it with life, panelling, candles in sconces, pictures,
    /// banners, a chandelier, and the small things that say someone works here. This is only drawn when someone's inside or near, as its roof hides it otherwise; its lights always burn.
    /// Nothing tall stands against the wall facing the camera, which comes down when someone's inside.
    /// </summary>
    internal static class HallInteriors
    {
        private const float FloorTop = Hall.FloorTop;
        private const float Wainscot = 2.2f;        // Below every window's sill.

        public static void Build(MeshBuilder b, HallDesign d, List<AssetLight> lights)
        {
            HallKit kit = HallKit.For(d.Kind);
            var random = new WorldRandom(0x1A51).Fork(d.Name);
            foreach (HallPiece piece in d.Pieces)
            {
                // The forge's hearth is part of the building, with its hood and chimney.
                if (piece.Kind == PieceKind.Hearth) continue;
                HallPiece p = piece;
                WorldRandom pieceRandom = random.Fork((int)(p.At.X * 31f + p.At.Y * 7f) ^ (int)p.Kind);
                Hall.At(b, new Vector3(p.At.X, FloorTop, p.At.Y), p.Yaw, () => HallFurniture.Build(b, p, kit, lights, pieceRandom));
            }

            switch (d.Kind)
            {
                case HallKind.Forge:
                    WallPosts(b, d, kit, 4f);
                    Sconces(b, d, lights, new[] { (HallSide.PlusX, 0f), (HallSide.Back, -4.4f), (HallSide.MinusX, -3.6f), (HallSide.MinusX, 4.6f) });
                    ForgeClutter(b, d, random);
                    break;
                case HallKind.GamingHouse:
                    Panelling(b, d, kit);
                    WallPosts(b, d, kit, Wainscot + 0.15f);
                    Sconces(b, d, lights, new[] { (HallSide.PlusX, -1.5f), (HallSide.PlusX, 6.4f), (HallSide.Back, -7.6f), (HallSide.Back, 7.6f), (HallSide.MinusX, -4.5f), (HallSide.MinusX, 2.6f) });
                    Chandelier(b, new Vector3(0f, d.WallTop - 0.1f, 1.6f), 2.6f, 8, lights);
                    Paintings(b, d, random);
                    TavernClutter(b, d, random);
                    break;
                default:
                    Panelling(b, d, kit);
                    WallPosts(b, d, kit, Wainscot + 0.15f);
                    Sconces(b, d, lights, new[] { (HallSide.PlusX, -3.75f), (HallSide.PlusX, 3.75f), (HallSide.Back, -11f), (HallSide.Back, 11f), (HallSide.Back, -3.2f), (HallSide.Back, 3.2f) });
                    Banners(b, d);
                    ArchiveClutter(b, d, random);
                    break;
            }
        }

        // A point on the inside of a wall, `out_` metres into the room from it.
        private static Vector3 Inside(HallDesign d, HallSide side, float along, float y, float out_ = 0f)
        {
            var s = new HallWalls.Side(d, side);
            return s.Outer(along, y, d.Wall + out_);
        }

        // Which way is into the room from a wall.
        private static Vector3 Into(HallSide side) => side switch
        {
            HallSide.Front => -Vector3.UnitZ,
            HallSide.Back => Vector3.UnitZ,
            HallSide.PlusX => -Vector3.UnitX,
            _ => Vector3.UnitX,
        };

        // The stretches of a wall's inside free of its doors, and of windows coming down below `below`.
        private static List<(float From, float To)> Clear(HallDesign d, HallSide side, float below, float margin)
        {
            var s = new HallWalls.Side(d, side);
            float from = -s.InnerHalf, to = s.InnerHalf;
            var runs = new List<(float, float)>();
            float at = from;
            foreach (HallOpening o in d.Openings.Where(o => o.Side == side && o.Sill < below).OrderBy(o => o.Along))
            {
                if (o.Along - o.HalfWidth - margin > at) runs.Add((at, o.Along - o.HalfWidth - margin));
                at = o.Along + o.HalfWidth + margin;
            }
            if (to > at) runs.Add((at, to));
            return runs;
        }

        // Oak panelling round the walls below the plaster: a skirting, stiles with fielded panels between, and a moulded rail along the top. The wall facing the camera keeps it low.
        private static void Panelling(MeshBuilder b, HallDesign d, HallKit kit)
        {
            VertexMaterial frame = kit.Panelling;
            VertexMaterial field = Hall.Wood(118, 82, 54);
            foreach (HallSide side in Enum.GetValues<HallSide>())
            {
                float top = side == HallSide.Front ? d.FrontCut - 0.1f : Wainscot;
                Vector3 into = Into(side);
                var s = new HallWalls.Side(d, side);
                foreach ((float from, float to) in Clear(d, side, top + 0.1f, 0.25f))
                {
                    if (to - from < 0.5f) continue;
                    Vector3 Mid(float along, float y, float out_) => Inside(d, side, along, y, out_);
                    float middle = (from + to) * 0.5f, half = (to - from) * 0.5f;
                    HallDressing.Box(b, Mid(middle, FloorTop + top * 0.5f, 0.035f), s.AlongDir, into, half, top * 0.5f, 0.035f, frame);
                    HallDressing.Box(b, Mid(middle, FloorTop + 0.16f, 0.09f), s.AlongDir, into, half, 0.16f, 0.06f, frame);
                    HallDressing.Box(b, Mid(middle, FloorTop + top - 0.06f, 0.11f), s.AlongDir, into, half + 0.04f, 0.08f, 0.08f, frame);
                    int panels = Math.Max(1, (int)MathF.Round((to - from) / 1.5f));
                    float width = (to - from) / panels;
                    for (int i = 0; i < panels; i++)
                    {
                        float a = from + width * (i + 0.5f);
                        HallDressing.Box(b, Mid(a, FloorTop + (0.35f + top - 0.16f) * 0.5f, 0.075f), s.AlongDir, into, width * 0.5f - 0.16f, (top - 0.51f) * 0.5f, 0.04f, field);
                        HallDressing.Box(b, Mid(from + width * i, FloorTop + top * 0.5f, 0.08f), s.AlongDir, into, 0.07f, top * 0.5f - 0.1f, 0.05f, frame);
                    }
                }
            }
        }

        // Timber posts up the inside of the walls between the windows, and a plate along their tops: the frame the walls hang on.
        private static void WallPosts(MeshBuilder b, HallDesign d, HallKit kit, float from)
        {
            VertexMaterial timber = kit.Timber;
            foreach (HallSide side in new[] { HallSide.Back, HallSide.PlusX, HallSide.MinusX })
            {
                var s = new HallWalls.Side(d, side);
                Vector3 into = Into(side);
                float plate = d.WallTop - 0.45f;
                HallDressing.Box(b, Inside(d, side, 0f, plate + 0.22f, 0.12f), s.AlongDir, into, s.InnerHalf, 0.22f, 0.12f, timber);
                var posts = new List<float> { -s.InnerHalf + 0.2f, s.InnerHalf - 0.2f };
                foreach (HallOpening o in d.Openings.Where(o => o.Side == side)) { posts.Add(o.Along - o.HalfWidth - 0.45f); posts.Add(o.Along + o.HalfWidth + 0.45f); }
                foreach (float a in posts.Distinct())
                {
                    if (MathF.Abs(a) > s.InnerHalf - 0.15f) continue;
                    if (d.Openings.Any(o => o.Side == side && MathF.Abs(o.Along - a) < o.HalfWidth + 0.25f)) continue;
                    HallDressing.Box(b, Inside(d, side, a, (from + plate) * 0.5f, 0.1f), s.AlongDir, into, 0.17f, (plate - from) * 0.5f, 0.1f, timber);
                    // A curved brace up to the plate.
                    b.Beam(Inside(d, side, a, plate - 1.4f, 0.12f), Inside(d, side, a + 1.1f, plate - 0.05f, 0.12f), 0.08f, timber);
                }
            }
        }

        // Candles in iron sconces on the walls, a back plate, an arm and a drip pan, and the light they give.
        private static void Sconces(MeshBuilder b, HallDesign d, List<AssetLight> lights, IEnumerable<(HallSide side, float along)> where)
        {
            VertexMaterial iron = Hall.Iron(74);
            foreach ((HallSide side, float along) in where)
            {
                var s = new HallWalls.Side(d, side);
                Vector3 into = Into(side);
                Vector3 wall = Inside(d, side, along, 4.6f, 0f);
                HallDressing.Box(b, wall + into * 0.03f, s.AlongDir, into, 0.16f, 0.32f, 0.03f, iron);
                Vector3 pan = wall + into * 0.55f + Vector3.UnitY * 0.15f;
                b.Beam(wall + into * 0.05f - Vector3.UnitY * 0.15f, pan - Vector3.UnitY * 0.03f, 0.03f, iron);
                b.Tube(pan - Vector3.UnitY * 0.03f, pan, 0.14f, 0.16f, 8, iron);
                Hall.Candle(b, pan, 0.45f, 0.065f, lights);
                lights.Add(AssetLight.Candles(pan + Vector3.UnitY * 0.8f + into * 0.4f) with { Intensity = 24f, Radius = 13f });
            }
        }

        // A wheel of wood hung on chains, candles all round its rim.
        private static void Chandelier(MeshBuilder b, Vector3 top, float radius, int candles, List<AssetLight> lights)
        {
            VertexMaterial wood = Hall.Wood(86, 58, 38);
            VertexMaterial iron = Hall.Iron(70);
            float y = top.Y - 2.6f;
            Vector3 hub = new Vector3(top.X, y, top.Z);
            for (int i = 0; i < 4; i++)
            {
                float a = MathF.Tau * i / 4f;
                b.Beam(hub + new Vector3(MathF.Cos(a) * radius * 0.9f, 0.05f, MathF.Sin(a) * radius * 0.9f), hub + Vector3.UnitY * 1.1f, 0.012f, iron);
            }
            b.Beam(hub + Vector3.UnitY * 1.1f, top, 0.02f, iron);
            for (int i = 0; i < 16; i++)
            {
                float a0 = MathF.Tau * i / 16f, a1 = MathF.Tau * (i + 1) / 16f;
                b.Beam(hub + new Vector3(MathF.Cos(a0) * radius, 0f, MathF.Sin(a0) * radius), hub + new Vector3(MathF.Cos(a1) * radius, 0f, MathF.Sin(a1) * radius), 0.1f, wood);
            }
            b.Beam(hub - Vector3.UnitX * radius, hub + Vector3.UnitX * radius, 0.07f, wood);
            b.Beam(hub - Vector3.UnitZ * radius, hub + Vector3.UnitZ * radius, 0.07f, wood);
            b.Tube(hub - Vector3.UnitY * 0.2f, hub + Vector3.UnitY * 0.15f, 0.18f, 0.18f, 8, wood);
            for (int i = 0; i < candles; i++)
            {
                float a = MathF.Tau * (i + 0.5f) / candles;
                Vector3 at = hub + new Vector3(MathF.Cos(a) * radius, 0.1f, MathF.Sin(a) * radius);
                b.Tube(at - Vector3.UnitY * 0.02f, at + Vector3.UnitY * 0.02f, 0.11f, 0.12f, 8, iron);
                Hall.Candle(b, at + Vector3.UnitY * 0.02f, 0.4f, 0.06f, lights);
            }
            lights.Add(new AssetLight
            {
                Position = hub + Vector3.UnitY * 0.6f, Colour = new Vector3(1f, 0.62f, 0.28f), Intensity = 90f, Radius = 20f, Fill = 0.25f,
                Flicker = 0.12f, FlickerSpeed = 6f, Shadow = true, LightsFog = true,
            });
        }

        // Pictures of the land in plain frames, on the walls of the gaming house: a coast, a vale, the Haven's own blossom.
        private static void Paintings(MeshBuilder b, HallDesign d, WorldRandom random)
        {
            (HallSide side, float along, float y, float w, float h)[] hung =
            {
                (HallSide.Back, -7.6f, 6.4f, 2.2f, 1.5f), (HallSide.Back, 7.6f, 6.4f, 2.2f, 1.5f),
                (HallSide.MinusX, -1f, 6.8f, 2.4f, 1.6f), (HallSide.PlusX, -1.6f, 6.6f, 1.6f, 1.2f),
            };
            Vector3[][] scenes =
            {
                new[] { new Vector3(150, 176, 196), new Vector3(70, 110, 140), new Vector3(196, 182, 140) },     // a coast
                new[] { new Vector3(190, 168, 140), new Vector3(86, 120, 70), new Vector3(60, 86, 52) },         // a vale
                new[] { new Vector3(214, 176, 150), new Vector3(200, 130, 150), new Vector3(90, 110, 70) },      // blossom
            };
            int scene = 0;
            foreach ((HallSide side, float along, float y, float w, float h) in hung)
            {
                var s = new HallWalls.Side(d, side);
                Vector3 into = Into(side);
                Vector3[] colours = scenes[scene++ % scenes.Length];
                HallDressing.Box(b, Inside(d, side, along, y, 0.06f), s.AlongDir, into, w * 0.5f + 0.15f, h * 0.5f + 0.15f, 0.06f, Hall.Wood(80, 52, 34));
                // Sky, middle distance, foreground, in bands.
                for (int band = 0; band < 3; band++)
                {
                    float y0 = y + h * 0.5f - h * (band + 1) / 3f, y1 = y + h * 0.5f - h * band / 3f;
                    Vector3 c = colours[band];
                    HallDressing.Box(b, Inside(d, side, along, (y0 + y1) * 0.5f, 0.13f), s.AlongDir, into, w * 0.5f, (y1 - y0) * 0.5f, 0.01f, Hall.Cloth((int)c.X, (int)c.Y, (int)c.Z));
                }
                // A hill or two across the bands.
                HallDressing.Box(b, Inside(d, side, along - w * 0.18f, y - h * 0.02f, 0.145f), s.AlongDir, into, w * 0.2f, h * 0.12f, 0.005f, Hall.Cloth((int)(colours[1].X * 0.8f), (int)(colours[1].Y * 0.8f), (int)(colours[1].Z * 0.8f)));
            }
        }

        // Long banners down the back wall between the windows, in the Haven's colours, each with a blossom on it.
        private static void Banners(MeshBuilder b, HallDesign d)
        {
            Vector3 into = Into(HallSide.Back);
            var s = new HallWalls.Side(d, HallSide.Back);
            foreach (float along in new[] { -11f, 11f })
            {
                float top = d.WallTop - 0.9f;
                float length = 3.6f;
                b.Beam(Inside(d, HallSide.Back, along - 1.3f, top, 0.2f), Inside(d, HallSide.Back, along + 1.3f, top, 0.2f), 0.06f, Hall.Wood(80, 54, 36));
                HallDressing.Box(b, Inside(d, HallSide.Back, along, top - length * 0.5f, 0.15f), s.AlongDir, into, 1.1f, length * 0.5f, 0.02f, Hall.Cloth(28, 92, 96));
                HallDressing.Box(b, Inside(d, HallSide.Back, along, top - length * 0.5f, 0.17f), s.AlongDir, into, 0.95f, length * 0.5f - 0.15f, 0.01f, Hall.Cloth(24, 78, 82));
                // The point at its foot.
                Vector3 l = Inside(d, HallSide.Back, along - 1.1f, top - length, 0.16f), r = Inside(d, HallSide.Back, along + 1.1f, top - length, 0.16f), p = Inside(d, HallSide.Back, along, top - length - 0.7f, 0.16f);
                if (Vector3.Dot(Vector3.Cross(r - l, p - l), into) > 0f) b.Triangle(l, r, p, Hall.Cloth(28, 92, 96));
                else b.Triangle(r, l, p, Hall.Cloth(28, 92, 96));
                // A blossom: five petals round a heart.
                Vector3 heart = Inside(d, HallSide.Back, along, top - length * 0.45f, 0.19f);
                for (int i = 0; i < 5; i++)
                {
                    float a = MathF.Tau * i / 5f + MathF.PI * 0.5f;
                    Vector3 petal = heart + s.AlongDir * (MathF.Cos(a) * 0.36f) + Vector3.UnitY * (MathF.Sin(a) * 0.36f);
                    HallDressing.Box(b, petal, s.AlongDir, into, 0.2f, 0.2f, 0.01f, Hall.Cloth(214, 160, 172));
                }
                HallDressing.Box(b, heart + into * 0.01f, s.AlongDir, into, 0.12f, 0.12f, 0.012f, Hall.Cloth(214, 176, 80));
            }
            // A great map of the lands framed on the west wall between the windows, high up.
            var west = new HallWalls.Side(d, HallSide.PlusX);
            Vector3 westInto = Into(HallSide.PlusX);
            foreach (float along in new[] { -3.75f, 3.75f })
            {
                HallDressing.Box(b, Inside(d, HallSide.PlusX, along, 6.8f, 0.06f), west.AlongDir, westInto, 1.6f, 1.15f, 0.06f, Hall.Wood(76, 50, 34));
                HallDressing.Box(b, Inside(d, HallSide.PlusX, along, 6.8f, 0.13f), west.AlongDir, westInto, 1.42f, 0.98f, 0.01f, Hall.Flat(204, 188, 148));
                var rng = new WorldRandom(0xA9).Fork((int)(along * 10f));
                for (int i = 0; i < 5; i++)
                {
                    HallDressing.Box(b, Inside(d, HallSide.PlusX, along + rng.Range(-1f, 1f), 6.8f + rng.Range(-0.6f, 0.6f), 0.145f), west.AlongDir, westInto, rng.Range(0.15f, 0.35f), rng.Range(0.1f, 0.25f), 0.005f, Hall.Flat(120, 140, 96));
                }
            }
        }

        // ---- What fills each hall out.

        private static void ForgeClutter(MeshBuilder b, HallDesign d, WorldRandom random)
        {
            // Horseshoes hung in rows on the east wall above the bench, a wheel waiting for its rim, sacks of charcoal, iron rods leaning, scale on the floor by the anvil.
            var east = new HallWalls.Side(d, HallSide.MinusX);
            Vector3 into = Into(HallSide.MinusX);
            for (int row = 0; row < 2; row++)
            {
                for (int i = 0; i < 6; i++)
                {
                    Vector3 at = Inside(d, HallSide.MinusX, -3.2f + i * 0.5f, 7.4f - row * 0.6f, 0.08f);
                    for (int k = 0; k < 5; k++)
                    {
                        float a0 = MathF.PI * (0.15f + k * 0.17f), a1 = MathF.PI * (0.15f + (k + 1) * 0.17f);
                        b.Beam(at + east.AlongDir * (MathF.Cos(a0) * 0.16f) - Vector3.UnitY * (MathF.Sin(a0) * 0.16f), at + east.AlongDir * (MathF.Cos(a1) * 0.16f) - Vector3.UnitY * (MathF.Sin(a1) * 0.16f), 0.025f, Hall.Iron(78));
                    }
                }
            }
            VertexMaterial sack = Hall.Cloth(150, 128, 92);
            foreach (Vector3 at in new[] { new Vector3(7.6f, FloorTop, -9.2f), new Vector3(8.6f, FloorTop, -9.5f), new Vector3(-2.8f, FloorTop, -9.4f) })
            {
                b.Box(at + Vector3.UnitY * 0.45f, new Vector3(0.42f, 0.45f, 0.32f), sack);
                b.Box(at + Vector3.UnitY * 0.95f, new Vector3(0.22f, 0.08f, 0.18f), sack);
            }
            for (int i = 0; i < 6; i++)
            {
                Vector3 foot = new Vector3(13.6f, FloorTop, 6.2f + i * 0.12f);
                b.Beam(foot, foot + new Vector3(-0.35f, 3.4f, 0.1f), 0.03f, Hall.Iron(80));
            }
            for (int i = 0; i < 20; i++)
            {
                Vector3 at = new Vector3(random.Range(-1.6f, 2.4f), FloorTop + 0.01f, random.Range(-5f, -1.8f));
                b.Box(at, new Vector3(random.Range(0.03f, 0.07f), 0.01f, random.Range(0.03f, 0.07f)), Hall.Flat(40, 34, 30));
            }
            // A half-made cart wheel on the floor near the door, and a bucket of water.
            Vector3 hub = new Vector3(6.2f, FloorTop + 0.15f, 5.4f);
            for (int i = 0; i < 12; i++)
            {
                float a0 = MathF.Tau * i / 12f, a1 = MathF.Tau * (i + 1) / 12f;
                if (i < 9) b.Beam(hub + new Vector3(MathF.Cos(a0), 0f, MathF.Sin(a0)) * 1.3f, hub + new Vector3(MathF.Cos(a1), 0f, MathF.Sin(a1)) * 1.3f, 0.1f, Hall.Wood(130, 96, 62));
                b.Beam(hub, hub + new Vector3(MathF.Cos(a0), 0f, MathF.Sin(a0)) * 1.25f, 0.05f, Hall.Wood(130, 96, 62));
            }
            b.Tube(hub - Vector3.UnitY * 0.12f, hub + Vector3.UnitY * 0.12f, 0.24f, 0.24f, 8, Hall.Wood(110, 80, 52));
        }

        private static void TavernClutter(MeshBuilder b, HallDesign d, WorldRandom random)
        {
            // Pegs by the door with cloaks and a hat on them, and a slate behind the bar with what's poured chalked on it.
            var west = new HallWalls.Side(d, HallSide.PlusX);
            Vector3 westInto = Into(HallSide.PlusX);
            HallDressing.Box(b, Inside(d, HallSide.PlusX, 8.6f, 4.4f, 0.05f), west.AlongDir, westInto, 1.4f, 0.1f, 0.05f, Hall.Wood(96, 64, 42));
            Vector3[] cloaks = { Hall.Cloths[1], Hall.Cloths[3], Hall.Cloths[2] };
            for (int i = 0; i < 3; i++)
            {
                Vector3 peg = Inside(d, HallSide.PlusX, 7.6f + i * 0.9f, 4.4f, 0.2f);
                b.Beam(peg - westInto * 0.15f, peg + westInto * 0.1f, 0.03f, Hall.Wood(96, 64, 42));
                Vector3 c = cloaks[i];
                b.Box(peg + westInto * 0.08f - Vector3.UnitY * 1.1f, new Vector3(0.25f, 1.05f, 0.25f) * 1f, Hall.ClothOf(c));
            }
            var back = new HallWalls.Side(d, HallSide.Back);
            Vector3 backInto = Into(HallSide.Back);
            HallDressing.Box(b, Inside(d, HallSide.Back, 0f, 6.9f, 0.05f), back.AlongDir, backInto, 1.6f, 0.75f, 0.04f, Hall.Wood(80, 52, 34));
            HallDressing.Box(b, Inside(d, HallSide.Back, 0f, 6.9f, 0.1f), back.AlongDir, backInto, 1.45f, 0.62f, 0.01f, Hall.Flat(40, 44, 42));
            for (int line = 0; line < 4; line++)
            {
                HallDressing.Box(b, Inside(d, HallSide.Back, -0.2f + (line % 2) * 0.3f, 7.3f - line * 0.27f, 0.115f), back.AlongDir, backInto, 0.9f - (line % 2) * 0.3f, 0.025f, 0.004f, Hall.Flat(200, 196, 184));
            }
            // Barrels and a tun by the stage, a fallen stool, coins dropped on the boards.
            HallFurniture.Barrel(b, new Vector3(-8.2f, FloorTop, 9.2f), 1.9f, 0.62f);
            for (int i = 0; i < 6; i++)
            {
                Vector3 at = new Vector3(random.Range(-6f, 6f), FloorTop + 0.01f, random.Range(-3f, 8f));
                if (MathF.Abs(at.X) < 4.5f && at.Z > -1.2f && at.Z < 4.4f) continue;
                b.Tube(at, at + Vector3.UnitY * 0.025f, 0.1f, 0.1f, 7, Hall.Metal(214, 168, 70));
            }
        }

        private static void ArchiveClutter(MeshBuilder b, HallDesign d, WorldRandom random)
        {
            // A ladder against the shelves, books stacked on the floor, a basket of scrolls by the desk.
            Vector3 foot = new Vector3(-18.9f, FloorTop, 2.2f);
            Vector3 top = new Vector3(-19.7f, 5.7f, 2.2f);
            foreach (float s in new[] { -0.5f, 0.5f }) b.Beam(foot + Vector3.UnitZ * s, top + Vector3.UnitZ * s, 0.06f, Hall.Wood(140, 104, 68));
            for (int i = 1; i < 10; i++)
            {
                Vector3 rung = Vector3.Lerp(foot, top, i / 10f);
                b.Beam(rung - Vector3.UnitZ * 0.5f, rung + Vector3.UnitZ * 0.5f, 0.035f, Hall.Wood(140, 104, 68));
            }
            foreach (Vector3 at in new[] { new Vector3(-17.8f, FloorTop, -2.2f), new Vector3(-18.2f, FloorTop, 9.4f), new Vector3(-12.6f, FloorTop, -11.6f) })
            {
                float y = 0f;
                for (int i = 0; i < random.Int(3, 7); i++)
                {
                    float thick = random.Range(0.08f, 0.14f);
                    Hall.At(b, at + Vector3.UnitY * (y + thick * 0.5f), random.Range(-0.4f, 0.4f), () => b.Box(Vector3.Zero, new Vector3(0.3f, thick * 0.5f, 0.22f), random.Pick(Hall.BookCovers)));
                    y += thick;
                }
            }
            Vector3 basket = new Vector3(-13.4f, FloorTop, -10.9f);
            b.Tube(basket, basket + Vector3.UnitY * 0.7f, 0.42f, 0.48f, 10, Hall.Wood(156, 120, 72), capped: false);
            for (int i = 0; i < 7; i++)
            {
                float a = MathF.Tau * i / 7f;
                Vector3 s = basket + new Vector3(MathF.Cos(a) * 0.22f, 0.15f, MathF.Sin(a) * 0.22f);
                b.Tube(s, s + new Vector3(MathF.Cos(a) * 0.12f, 1.3f, MathF.Sin(a) * 0.12f), 0.08f, 0.08f, 6, i % 2 == 0 ? Hall.Paper : Hall.Flat(196, 180, 146));
            }
            // A chair at the reading desk.
            Hall.At(b, new Vector3(-15.6f, FloorTop, -10.9f), 0f, () =>
            {
                VertexMaterial wood = Hall.Wood(90, 60, 40);
                foreach (float x in new[] { -0.45f, 0.45f })
                {
                    foreach (float z in new[] { -0.4f, 0.4f }) Hall.Leg(b, new Vector3(x, 0f, z), 1.04f, 0.07f, wood);
                    b.Box(new Vector3(x, 1.9f, -0.42f), new Vector3(0.07f, 0.85f, 0.07f), wood);
                }
                b.Box(new Vector3(0f, 1.1f, 0f), new Vector3(0.55f, 0.06f, 0.5f), wood);
                b.Box(new Vector3(0f, 2.55f, -0.42f), new Vector3(0.58f, 0.14f, 0.06f), wood);
            });
        }
    }
}

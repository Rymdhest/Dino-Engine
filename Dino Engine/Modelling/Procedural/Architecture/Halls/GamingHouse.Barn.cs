using Dino_Engine.Modelling.Model;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.Modelling.Procedural.Architecture.Halls.HallProps;

namespace Dino_Engine.Modelling.Procedural.Architecture.Halls
{
    /// <summary>
    /// The Gaming House is an old barn, and this is the barn: walls of rubble at the foot, weathered boards to the height of a raised hand and lime between dark timbers over them;
    /// two tie beams across it on four posts; a hayloft in its far corner over the tuns, with a ladder up; a cart wheel of candles over the great table; and what a gaming house
    /// hangs in a barn, lanterns of coloured paper over the bar, pennants along its beams, the wheel of fortune and the board the odds are chalked on. Everything stands under
    /// <see cref="HallDesign.WallTop"/>, as what's over it goes with the roof when someone's inside, and nothing tall stands by the wall facing the camera. Where things stand is the
    /// design's (<see cref="HallDesigns"/>): the bays are where its posts are, the loft is over its store, and the hearth's floor is before its fireplace.
    /// </summary>
    internal static partial class GamingHouse
    {
        private const float Bay = HallDesigns.GamingBay;
        private const float PostDepth = HallDesigns.GamingPostDepth;
        private const float TieUnder = 8.2f;        // The underside of the tie beams, and of the plate along the walls.
        private const float TieTop = 8.72f;
        private const float LoftTop = 5f;           // The top of the loft's boards.
        private const float Footing = 1.05f;        // The top of the rubble at the foot of the walls.
        private const float BoardTop = 4.7f;        // And of the boards over it.

        public static void Dress(MeshBuilder b, HallDesign d, List<AssetLight> lights, WorldRandom random)
        {
            Walls(b, d, random.Fork("walls"));
            TieBeams(b, d);
            Loft(b, d, lights, random.Fork("loft"));
            HallPiece table = d.Pieces.First(p => p.Kind == PieceKind.GamingTable);
            WheelOfCandles(b, new Vector3(table.At.X, 5.9f, table.At.Y + 0.9f), 1.7f, lights);
            PaperLanterns(b, lights);
            WheelOfFortune(b, d, 8f, lights, random.Fork("wheel"));
            OddsBoard(b, d, random.Fork("odds"));
            OnTheWalls(b, d);
            Underfoot(b, d, random.Fork("straw"));
        }

        // ---- The walls.

        private static void Walls(MeshBuilder b, HallDesign d, WorldRandom random)
        {
            foreach (HallSide side in Enum.GetValues<HallSide>())
            {
                var s = new HallWalls.Side(d, side);
                Vector3 into = HallInteriors.Into(side);
                Vector3 along = s.AlongDir;
                bool front = side == HallSide.Front;
                Vector3 On(float a, float y, float out_) => HallInteriors.Inside(d, side, a, y, out_);
                List<HallOpening> openings = d.Openings.Where(o => o.Side == side).ToList();

                // Rubble at the foot, under a course of dressed stone.
                foreach ((float from, float to) in HallInteriors.Clear(d, side, Footing, 0.04f))
                {
                    if (to - from < 0.3f) continue;
                    float middle = (from + to) * 0.5f, half = (to - from) * 0.5f;
                    HallDressing.Box(b, On(middle, FloorTop + Footing * 0.5f, 0.07f), along, into, half, Footing * 0.5f, 0.07f, Rubble);
                    HallDressing.Box(b, On(middle, FloorTop + Footing + 0.04f, 0.085f), along, into, half, 0.04f, 0.085f, Dressed);
                }

                // Boards over it, every one its own grey, cut short under a window. The wall facing the camera keeps them below where it's cut.
                float top = front ? d.FrontCut - 0.16f : BoardTop;
                int count = (int)MathF.Round(s.InnerHalf * 2f / 0.42f);
                float width = s.InnerHalf * 2f / count;
                for (int i = 0; i < count; i++)
                {
                    float a = -s.InnerHalf + width * (i + 0.5f);
                    float y0 = FloorTop + Footing + 0.08f, y1 = top;
                    bool door = false;
                    foreach (HallOpening o in openings)
                    {
                        if (MathF.Abs(a - o.Along) > o.HalfWidth + width * 0.5f + 0.02f) continue;
                        door |= o.Door;
                        if (o.Sill < y1) y1 = o.Sill - 0.14f;
                    }
                    if (door || y1 - y0 < 0.25f) continue;
                    float proud = 0.03f + random.Range(0f, 0.012f);
                    HallDressing.Box(b, On(a, (y0 + y1) * 0.5f, proud), along, into, width * 0.5f - 0.008f, (y1 - y0) * 0.5f, proud, Weathered(random));
                }
                foreach ((float from, float to) in HallInteriors.Clear(d, side, top + 0.2f, 0.12f))
                {
                    if (to - from < 0.3f) continue;
                    HallDressing.Box(b, On((from + to) * 0.5f, top + 0.09f, 0.075f), along, into, (to - from) * 0.5f, 0.09f, 0.075f, FrameDark);
                }
                if (front) continue;

                // Over the boards the frame shows, dark against the lime: a plate along the top, studs between, a stud either side of every window, and a brace into each corner.
                HallDressing.Box(b, On(0f, (TieUnder + TieTop) * 0.5f, 0.13f), along, into, s.InnerHalf, (TieTop - TieUnder) * 0.5f, 0.13f, Frame);
                float low = top + 0.18f;
                bool Crossed(float a, float reach) => openings.Any(o => MathF.Abs(a - o.Along) < o.HalfWidth + reach && o.Head > low && o.Sill < TieUnder);
                int studs = (int)MathF.Round(s.InnerHalf * 2f / 1.7f);
                for (int i = 1; i < studs; i++)
                {
                    float a = -s.InnerHalf + s.InnerHalf * 2f * i / studs;
                    if (Crossed(a, 0.45f) || MathF.Abs(MathF.Abs(a) - Bay) < 0.5f && side == HallSide.Back) continue;
                    HallDressing.Box(b, On(a, (low + TieUnder) * 0.5f, 0.05f), along, into, 0.09f, (TieUnder - low) * 0.5f, 0.05f, Frame);
                }
                foreach (float end in new[] { -1f, 1f })
                {
                    float corner = end * (s.InnerHalf - 0.14f);
                    HallDressing.Box(b, On(corner, (low + TieUnder) * 0.5f, 0.06f), along, into, 0.14f, (TieUnder - low) * 0.5f, 0.06f, Frame);
                    if (Crossed(corner - end * 1.1f, 1.1f)) continue;
                    Hall.Board(b, On(corner - end * 0.14f, low + 0.5f, 0.05f), On(corner - end * 2f, TieUnder - 0.02f, 0.05f), Vector3.UnitY, 0.1f, 0.05f, Frame);
                }

                // Every window cased in: a stool under it, a board up either side and a head over it.
                foreach (HallOpening o in openings.Where(o => !o.Door))
                {
                    HallDressing.Box(b, On(o.Along, o.Sill - 0.06f, 0.11f), along, into, o.HalfWidth + 0.3f, 0.06f, 0.11f, Frame);
                    HallDressing.Box(b, On(o.Along, o.Head + 0.11f, 0.06f), along, into, o.HalfWidth + 0.3f, 0.11f, 0.06f, Frame);
                    foreach (float edge in new[] { -1f, 1f })
                    {
                        HallDressing.Box(b, On(o.Along + edge * (o.HalfWidth + 0.12f), (o.Sill + o.Head) * 0.5f, 0.05f), along, into, 0.12f, (o.Head - o.Sill) * 0.5f, 0.05f, Frame);
                    }
                }
            }
        }

        // The two tie beams, front to back over the posts, with a post under each of their ends against the walls and a brace up from it.
        private static void TieBeams(MeshBuilder b, HallDesign d)
        {
            Vector2 inner = d.Inner;
            foreach (float x in new[] { -Bay, Bay })
            {
                Hall.Board(b, new Vector3(x, (TieUnder + TieTop) * 0.5f, -inner.Y), new Vector3(x, (TieUnder + TieTop) * 0.5f, inner.Y), Vector3.UnitX, 0.2f, (TieTop - TieUnder) * 0.5f, Frame);
                foreach (float end in new[] { -1f, 1f })
                {
                    float z = end * (inner.Y - 0.2f);
                    b.Box(new Vector3(x, 0.3f, z), new Vector3(0.32f, 0.3f, 0.24f), Dressed);
                    b.Box(new Vector3(x, TieUnder * 0.5f + 0.3f, z), new Vector3(0.22f, TieUnder * 0.5f - 0.3f, 0.2f), Frame);
                    Hall.Bent(b, Frame, Vector3.UnitX, 0.12f, 0.1f, new Vector3(x, 5.7f, z - end * 0.2f), new Vector3(x, 6.7f, z - end * 0.62f), new Vector3(x, 7.5f, z - end * 1.3f), new Vector3(x, TieUnder + 0.02f, z - end * 2.3f));
                }
            }
        }

        // ---- The hayloft.

        /// <summary>
        /// The loft in the far corner of the left bay: boards on joists on a beam from the wall to the post, railed with poles, with hay still on it, sacks, a cart wheel, and
        /// the bed of someone who sleeps up there. Under it the house's tuns are laid up on their cradles, kegs on their shoulders, each tapped, with a lantern hung to draw by.
        /// </summary>
        private static void Loft(MeshBuilder b, HallDesign d, List<AssetLight> lights, WorldRandom random)
        {
            HallPiece store = d.Pieces.First(p => p.Kind == PieceKind.LoftStore);
            float x0 = store.At.X - store.Half.X, x1 = -Bay + 0.27f;
            float z0 = -d.Inner.Y, z1 = -PostDepth + 0.27f;
            const float Boards = 0.07f, Joist = 0.2f, Beam = 0.44f;
            float joistTop = LoftTop - Boards, beamTop = joistTop - Joist, beamY = beamTop - Beam * 0.5f;

            // The beams it rests on, along its two open sides, and what braces them.
            Hall.Board(b, new Vector3(x0, beamY, -PostDepth), new Vector3(-Bay, beamY, -PostDepth), Vector3.UnitZ, 0.17f, Beam * 0.5f, Frame);
            Hall.Board(b, new Vector3(-Bay, beamY, z0), new Vector3(-Bay, beamY, -PostDepth), Vector3.UnitX, 0.17f, Beam * 0.5f, Frame);
            b.Box(new Vector3(x0 + 0.17f, beamTop * 0.5f, -PostDepth), new Vector3(0.17f, beamTop * 0.5f, 0.2f), Frame);
            Hall.Bent(b, Frame, Vector3.UnitZ, 0.11f, 0.09f, new Vector3(-Bay - 0.26f, 3.1f, -PostDepth), new Vector3(-Bay - 0.8f, 3.8f, -PostDepth), new Vector3(-Bay - 1.7f, beamTop - Beam + 0.04f, -PostDepth));
            Hall.Bent(b, Frame, Vector3.UnitZ, 0.11f, 0.09f, new Vector3(x0 + 0.32f, 3.1f, -PostDepth), new Vector3(x0 + 0.86f, 3.8f, -PostDepth), new Vector3(x0 + 1.76f, beamTop - Beam + 0.04f, -PostDepth));
            Hall.Bent(b, Frame, Vector3.UnitX, 0.11f, 0.09f, new Vector3(-Bay, 3.1f, -PostDepth - 0.26f), new Vector3(-Bay, 3.8f, -PostDepth - 0.8f), new Vector3(-Bay, beamTop - Beam + 0.04f, -PostDepth - 1.7f));

            // Joists front to back, their ends showing over the beam, and the boards across them.
            for (float x = x0 + 0.45f; x < -Bay - 0.3f; x += 0.92f)
            {
                Hall.Board(b, new Vector3(x, joistTop - Joist * 0.5f, z0), new Vector3(x, joistTop - Joist * 0.5f, z1 + 0.14f), Vector3.UnitX, 0.08f, Joist * 0.5f, FrameDark);
            }
            int count = (int)MathF.Round((z1 - z0) / 0.44f);
            float width = (z1 - z0) / count;
            for (int i = 0; i < count; i++)
            {
                float z = z0 + width * (i + 0.5f);
                Hall.Board(b, new Vector3(x0, LoftTop - Boards * 0.5f, z), new Vector3(x1 + random.Range(-0.03f, 0.12f), LoftTop - Boards * 0.5f, z), Vector3.UnitZ, width * 0.5f - 0.007f, Boards * 0.5f, Weathered(random));
            }

            // A rail of poles along both open sides, broken where the ladder comes up.
            HallPiece ladder = d.Pieces.First(p => p.Kind == PieceKind.Ladder);
            float gap = ladder.At.X - ladder.Half.X - 0.3f;
            VertexMaterial pole = Hall.Timber(160, 126, 88);
            float railZ = z1 - 0.12f, railX = x1 - 0.12f;
            var railPosts = new List<float>();
            for (float x = x0 + 0.2f; x < gap - 0.6f; x += 1.95f) railPosts.Add(x);
            railPosts.Add(gap);
            foreach (float x in railPosts) b.Tube(new Vector3(x, LoftTop, railZ), new Vector3(x, LoftTop + 1.2f, railZ), 0.06f, 0.05f, 6, pole);
            foreach (float y in new[] { 0.55f, 1.1f }) b.Tube(new Vector3(x0, LoftTop + y, railZ), new Vector3(gap + 0.12f, LoftTop + y, railZ), 0.045f, 0.045f, 6, pole);
            foreach (float z in new[] { z1 - 1.9f, z1 - 3.6f }) b.Tube(new Vector3(railX, LoftTop, z), new Vector3(railX, LoftTop + 1.2f, z), 0.06f, 0.05f, 6, pole);
            foreach (float y in new[] { 0.55f, 1.1f }) b.Tube(new Vector3(railX, LoftTop + y, z0), new Vector3(railX, LoftTop + y, z1 - 0.3f), 0.045f, 0.045f, 6, pole);
            Bunting(b, new Vector3(x0 + 0.2f, LoftTop + 1.04f, railZ + 0.06f), new Vector3(gap, LoftTop + 1.04f, railZ + 0.06f), 0.16f, 12, 1, 0.5f);

            // Hay: bales stacked in the far corner, a heap of it loose by the rail with wisps hanging over the edge, and a fork stood in it.
            Vector3 deck = Vector3.UnitY * LoftTop;
            Bale(b, deck + new Vector3(x0 + 1.5f, 0f, z0 + 0.75f), 0.02f, random);
            Bale(b, deck + new Vector3(x0 + 1.55f, 0f, z0 + 1.95f), -0.05f, random);
            Bale(b, deck + new Vector3(x0 + 3.75f, 0f, z0 + 0.72f), 0.04f, random);
            Bale(b, deck + new Vector3(x0 + 1.6f, 0.96f, z0 + 1.3f), 0.3f, random);
            Bale(b, deck + new Vector3(x0 + 3.4f, 0.96f, z0 + 0.8f), -0.12f, random);
            Vector3 heap = deck + new Vector3(x0 + 2.6f, 0f, z1 - 1.1f);
            HayHeap(b, heap, 1.05f, 0.95f, random);
            HayHeap(b, heap + new Vector3(1.3f, 0f, 0.3f), 0.6f, 0.5f, random);
            for (int i = 0; i < 16; i++)
            {
                float x = heap.X + random.Range(-1.3f, 1.9f);
                Vector3 from = new Vector3(x, LoftTop + 0.02f, z1 - 0.05f);
                b.Beam(from, from + new Vector3(random.Range(-0.12f, 0.12f), -random.Range(0.25f, 0.7f), random.Range(0.05f, 0.14f)), 0.014f, random.Chance(0.5f) ? Hall.Flat(210, 184, 112) : Hall.Flat(184, 158, 92));
            }
            Pitchfork(b, deck + new Vector3(x0 + 3.9f, 0f, z0 + 3f), deck + new Vector3(x0 + 3.2f, 2.4f, z0 + 1.6f));

            // Whoever sleeps up here: a bed roll under the little window, a pillow of sacking, a candle and a book.
            Vector3 bed = deck + new Vector3(x0 + 0.85f, 0f, z0 + 3.9f);
            b.Box(bed + Vector3.UnitY * 0.07f, new Vector3(0.6f, 0.07f, 1.25f), Hall.Quilted(60, 76, 120));
            b.Box(bed + new Vector3(0f, 0.17f, 0.4f), new Vector3(0.62f, 0.05f, 0.8f), Hall.Quilted(206, 196, 168));
            b.Box(bed + new Vector3(0f, 0.2f, -0.95f), new Vector3(0.44f, 0.1f, 0.26f), Sacking);
            DishCandle(b, bed + new Vector3(0.95f, 0f, -0.8f), lights, 0.2f);
            b.Box(bed + new Vector3(0.95f, 0.05f, -0.3f), new Vector3(0.16f, 0.05f, 0.22f), Hall.Leather(64, 36, 24));

            // By the rail over the bar: sacks, crates with a lantern on them, and a cart wheel against the wall.
            Sack(b, deck + new Vector3(x1 - 1.3f, 0f, z0 + 2.7f), 0.42f, 1.1f, random);
            Sack(b, deck + new Vector3(x1 - 0.75f, 0f, z0 + 3.3f), 0.4f, 1f, random);
            Sack(b, deck + new Vector3(x1 - 1.5f, 0f, z0 + 3.6f), 0.38f, 0.95f, random);
            Crate(b, deck + new Vector3(x1 - 2.7f, 0f, z0 + 0.7f), new Vector3(0.56f, 0.38f, 0.44f), 0.1f, random);
            Crate(b, deck + new Vector3(x1 - 2.65f, 0.76f, z0 + 0.72f), new Vector3(0.4f, 0.26f, 0.34f), -0.3f, random);
            Lantern(b, deck + new Vector3(x1 - 2.65f, 0.76f + 0.52f + 0.36f, z0 + 0.72f), lights, 20f, 10f, shadow: false);
            CartWheel(b, deck + new Vector3(x1 - 1.05f, 1.02f, z0 + 0.42f), new Vector3(0f, 0.3f, 1f), 1.02f, Hall.Timber(138, 108, 80));

            // Under it: three tuns on their cradles, heads out, each tapped over a pail, and two kegs laid on their shoulders.
            float headZ = store.At.Y + store.Half.Y - 0.45f;
            float span = (store.Half.X * 2f - 0.5f) / 3f;
            var tuns = new float[3];
            for (int i = 0; i < 3; i++)
            {
                float x = x0 + 0.42f + span * (i + 0.5f);
                tuns[i] = x;
                foreach (float z in new[] { headZ - 0.5f, headZ - 1.9f })
                {
                    b.Box(new Vector3(x, 0.2f, z), new Vector3(0.86f, 0.2f, 0.14f), FrameDark);
                    foreach (float s in new[] { -1f, 1f }) b.Box(new Vector3(x + s * 0.72f, 0.5f, z), new Vector3(0.14f, 0.12f, 0.14f), FrameDark);
                }
                Cask(b, new Vector3(x, 1.52f, headZ - 2.6f), new Vector3(x, 1.52f, headZ), 1.14f);
                Spigot(b, new Vector3(x, 0.9f, headZ + 0.012f), Vector3.UnitZ);
                for (int stroke = 0; stroke <= i; stroke++) b.Box(new Vector3(x - 0.1f * i + stroke * 0.2f, 1.9f, headZ + 0.02f), new Vector3(0.022f, 0.2f, 0.004f), Chalk);
                Vector3 pail = new Vector3(x + 0.05f, 0f, headZ + 0.2f);
                b.Tube(pail, pail + Vector3.UnitY * 0.5f, 0.22f, 0.27f, 9, Hall.Wood(130, 96, 62), capped: false);
                b.Tube(pail + Vector3.UnitY * 0.4f, pail + Vector3.UnitY * 0.42f, 0.25f, 0.25f, 9, Hall.Mirror(70, 50, 34));
                b.Tube(pail + Vector3.UnitY * 0.14f, pail + Vector3.UnitY * 0.2f, 0.245f, 0.25f, 9, Hall.Iron(84), capped: false);
            }
            for (int i = 0; i < 2; i++)
            {
                float x = (tuns[i] + tuns[i + 1]) * 0.5f;
                Cask(b, new Vector3(x, 3.02f, headZ - 2.3f), new Vector3(x, 3.02f, headZ - 0.25f), 0.64f);
            }
            // A lantern under the beam, between the settle's end and the ladder.
            HallPiece settle = d.Pieces.First(p => p.Kind == PieceKind.Settle);
            Vector3 hook = new Vector3((settle.At.X + settle.Half.X + gap) * 0.5f, beamTop - Beam, -PostDepth);
            b.Tube(hook, hook - Vector3.UnitY * 0.3f, 0.018f, 0.018f, 4, Iron);
            Lantern(b, hook - Vector3.UnitY * 0.84f, lights, 34f, 12f);
        }

        // ---- What hangs.

        // A cart wheel hung flat on chains over the great table, a candle on an iron pan at the end of every spoke.
        private static void WheelOfCandles(MeshBuilder b, Vector3 hub, float radius, List<AssetLight> lights)
        {
            VertexMaterial wood = FrameDark;
            VertexMaterial iron = Hall.Iron(70);
            const int Spokes = 10;
            CartWheel(b, hub, Vector3.UnitY, radius, wood, Spokes);
            Vector3 ring = hub + Vector3.UnitY * 1.75f;
            for (int i = 0; i < 4; i++)
            {
                float a = MathF.Tau * (i + 0.5f) / 4f;
                b.Beam(hub + new Vector3(MathF.Cos(a), 0.06f, MathF.Sin(a)) * (radius - 0.1f) + Vector3.UnitY * 0.05f, ring, 0.016f, iron);
            }
            b.Tube(ring - Vector3.UnitY * 0.06f, ring + Vector3.UnitY * 0.06f, 0.09f, 0.09f, 6, iron);
            b.Beam(ring, new Vector3(hub.X, 9.05f, hub.Z), 0.022f, iron);
            for (int i = 0; i < Spokes; i++)
            {
                float a = MathF.Tau * (i + 0.5f) / Spokes;
                Vector3 at = hub + new Vector3(MathF.Cos(a), 0f, MathF.Sin(a)) * (radius - 0.1f) + Vector3.UnitY * 0.1f;
                b.Tube(at, at + Vector3.UnitY * 0.035f, 0.13f, 0.15f, 8, iron);
                Hall.Candle(b, at + Vector3.UnitY * 0.035f, 0.34f + 0.1f * ((i * 7) % 3), 0.06f, lights);
                // Wax run down off the pan, as it does from candles nobody trims.
                b.Tube(at + new Vector3(0.1f, 0.02f, 0.05f), at + new Vector3(0.11f, -0.2f - 0.08f * (i % 3), 0.06f), 0.03f, 0.012f, 5, Hall.Wax);
            }
            lights.Add(new AssetLight
            {
                Position = hub + Vector3.UnitY * 0.7f, Colour = new Vector3(1f, 0.62f, 0.28f), Intensity = 96f, Radius = 22f, Fill = 0.25f,
                Flicker = 0.12f, FlickerSpeed = 6f, Shadow = true, LightsFog = true,
            });
        }

        // A string of paper lanterns over the bar, from one far post to the other, in the Haven's saffron, blossom and teal.
        private static void PaperLanterns(MeshBuilder b, List<AssetLight> lights)
        {
            Vector3 from = new Vector3(-Bay + 0.3f, TieUnder - 0.3f, -PostDepth), to = new Vector3(Bay - 0.3f, TieUnder - 0.3f, -PostDepth);
            Vector3 On(float t) => Vector3.Lerp(from, to, t) - Vector3.UnitY * 1.1f * 4f * t * (1f - t);
            VertexMaterial cord = Hall.Flat(58, 46, 34);
            (Vector3 glow, float strength)[] shades = { (new Vector3(1f, 0.5f, 0.08f), 0.5f), (new Vector3(1f, 0.26f, 0.36f), 0.46f), (new Vector3(0.08f, 0.78f, 0.66f), 0.46f) };
            const int Count = 9;
            Vector3 previous = from;
            for (int i = 0; i < Count * 2; i++)
            {
                Vector3 next = On((i + 1) / (Count * 2f));
                b.Beam(previous, next, 0.012f, cord);
                previous = next;
            }
            for (int i = 0; i < Count; i++)
            {
                Vector3 hang = On((i + 0.5f) / Count);
                (Vector3 glow, float strength) = shades[i % shades.Length];
                Vector3 middle = hang - Vector3.UnitY * 0.5f;
                b.Tube(hang, middle + Vector3.UnitY * 0.26f, 0.008f, 0.008f, 3, cord);
                b.Tube(middle - Vector3.UnitY * 0.24f, middle, 0.15f, 0.22f, 9, Hall.Glow(glow.X, glow.Y, glow.Z, strength), capped: false);
                b.Tube(middle, middle + Vector3.UnitY * 0.24f, 0.22f, 0.15f, 9, Hall.Glow(glow.X, glow.Y, glow.Z, strength), capped: false);
                b.Tube(middle + Vector3.UnitY * 0.24f, middle + Vector3.UnitY * 0.29f, 0.15f, 0.1f, 9, Hall.Flat(44, 34, 28));
                b.Tube(middle - Vector3.UnitY * 0.24f, middle - Vector3.UnitY * 0.29f, 0.15f, 0.1f, 9, Hall.Flat(44, 34, 28));
                b.Beam(middle - Vector3.UnitY * 0.29f, middle - Vector3.UnitY * 0.52f, 0.012f, Dyed(Saffron));
            }
            lights.Add(AssetLight.Glow(On(0.5f) - Vector3.UnitY * 0.5f, new Vector3(1f, 0.6f, 0.34f), 20f, 11f));
        }

        /// <summary>
        /// The wheel of fortune, on the back wall of the right bay: a great painted wheel on a board, pegs round its rim and a pointer over it for them to click past, and a shelf
        /// under it for what's staked.
        /// </summary>
        private static void WheelOfFortune(MeshBuilder b, HallDesign d, float along, List<AssetLight> lights, WorldRandom random)
        {
            var s = new HallWalls.Side(d, HallSide.Back);
            Vector3 into = HallInteriors.Into(HallSide.Back);
            Vector3 across = s.AlongDir;
            const float Radius = 1.62f, Middle = 4.5f;
            Vector3 On(float a, float y, float out_) => HallInteriors.Inside(d, HallSide.Back, a, y, out_);

            // The board it's hung on, framed.
            HallDressing.Box(b, On(along, Middle, 0.1f), across, into, 1.98f, 2.06f, 0.03f, Walnut);
            foreach (float e in new[] { -1f, 1f })
            {
                HallDressing.Box(b, On(along + e * 1.98f, Middle, 0.11f), across, into, 0.09f, 2.15f, 0.05f, FrameDark);
                HallDressing.Box(b, On(along, Middle + e * 2.06f, 0.11f), across, into, 2.07f, 0.09f, 0.05f, FrameDark);
            }

            // The wheel: sixteen wedges in the Haven's colours, one of them the house's own.
            Vector3 centre = On(along, Middle, 0.2f);
            Vector3 P(float t, float radius) => centre + (across * MathF.Cos(t) + Vector3.UnitY * MathF.Sin(t)) * radius;
            Vector3[] colours = { Teal, Cream, Saffron, Blue };
            const int Wedges = 16;
            for (int i = 0; i < Wedges; i++)
            {
                float t0 = MathF.Tau * i / Wedges, t1 = MathF.Tau * (i + 1) / Wedges;
                Vector3 colour = i == 5 ? Blossom : colours[i % colours.Length];
                Hall.Tri(b, centre, P(t0, Radius), P(t1, Radius), into, PaintedOf(colour));
                Hall.Board(b, P(t0, Radius + 0.05f) + into * 0.02f, P(t1, Radius + 0.05f) + into * 0.02f, into, 0.09f, 0.06f, FrameDark);
                b.Beam(P(t0, Radius - 0.1f), P(t0, Radius - 0.1f) + into * 0.16f, 0.022f, Pale);
                Hall.Board(b, P(t0, 0.3f) + into * 0.012f, P(t0, Radius - 0.02f) + into * 0.012f, into, 0.012f, 0.012f, FrameDark);
                if (i % 2 == 1)
                {
                    Vector3 coin = P((t0 + t1) * 0.5f, Radius * 0.68f);
                    b.Tube(coin, coin + into * 0.022f, 0.1f, 0.1f, 8, i == 5 ? SilverCoin : Gold);
                }
            }
            b.Tube(centre, centre + into * 0.2f, 0.3f, 0.22f, 12, FrameDark);
            b.Tube(centre + into * 0.2f, centre + into * 0.26f, 0.12f, 0.08f, 8, Hall.Pewter);

            // The pointer, a tongue of pale wood on an iron arm from the top of the board.
            Vector3 tip = P(MathF.PI * 0.5f, Radius - 0.16f) + into * 0.14f, root = P(MathF.PI * 0.5f, Radius + 0.36f) + into * 0.14f;
            Hall.Tri(b, root - across * 0.14f, root + across * 0.14f, tip, into, Pale);
            Hall.Tri(b, root - across * 0.14f - into * 0.012f, root + across * 0.14f - into * 0.012f, tip - into * 0.012f, -into, Pale);
            b.Beam(root - into * 0.14f, root + into * 0.02f, 0.03f, Iron);

            // The shelf: candles at its ends, what's been staked, and the bell that's rung when it stops.
            float shelf = Middle - 2.06f - 0.25f;
            Hall.Board(b, On(along - 1.7f, shelf, 0.34f), On(along + 1.7f, shelf, 0.34f), into, 0.28f, 0.05f, Oak);
            foreach (float e in new[] { -1.2f, 1.2f }) Hall.Board(b, On(along + e, shelf - 0.05f, 0.5f), On(along + e, shelf - 0.5f, 0.1f), across, 0.05f, 0.04f, Walnut);
            float top = shelf + 0.05f;
            Hall.Candle(b, On(along - 1.45f, top, 0.34f), 0.4f, 0.06f, lights);
            Hall.Candle(b, On(along + 1.45f, top, 0.34f), 0.32f, 0.06f, lights);
            lights.Add(AssetLight.Candles(On(along, top + 0.8f, 0.9f)) with { Intensity = 22f, Radius = 10f, Shadow = false });
            Coins(b, On(along - 0.8f, top, 0.36f), 7, random);
            Coins(b, On(along - 0.5f, top, 0.3f), 4, random);
            Coins(b, On(along + 0.2f, top, 0.4f), 9, random);
            Vector3 bell = On(along + 0.85f, top, 0.34f);
            b.Tube(bell, bell + Vector3.UnitY * 0.26f, 0.17f, 0.07f, 9, Hall.Pewter);
            b.Tube(bell + Vector3.UnitY * 0.26f, bell + Vector3.UnitY * 0.44f, 0.03f, 0.04f, 6, Walnut);
        }

        // The board the odds are chalked on, over the bar: a slate in a frame, a heading, and a name and its tally to a line.
        private static void OddsBoard(MeshBuilder b, HallDesign d, WorldRandom random)
        {
            var s = new HallWalls.Side(d, HallSide.Back);
            Vector3 into = HallInteriors.Into(HallSide.Back);
            Vector3 across = s.AlongDir;
            const float Middle = 7.1f, HalfWide = 2.5f, HalfHigh = 0.78f;
            Vector3 On(float a, float y, float out_) => HallInteriors.Inside(d, HallSide.Back, a, y, out_);
            HallDressing.Box(b, On(0f, Middle, 0.06f), across, into, HalfWide + 0.14f, HalfHigh + 0.14f, 0.06f, Walnut);
            HallDressing.Box(b, On(0f, Middle, 0.125f), across, into, HalfWide, HalfHigh, 0.008f, Slate);
            HallDressing.Box(b, On(0f, Middle + HalfHigh - 0.2f, 0.136f), across, into, 0.9f, 0.07f, 0.004f, Chalk);
            HallDressing.Box(b, On(0f, Middle + HalfHigh - 0.36f, 0.136f), across, into, HalfWide - 0.2f, 0.012f, 0.004f, Chalk);
            for (int column = 0; column < 3; column++)
            {
                float left = -HalfWide + 0.25f + column * (HalfWide * 2f - 0.3f) / 3f;
                for (int row = 0; row < 4; row++)
                {
                    float y = Middle + HalfHigh - 0.56f - row * 0.24f;
                    float name = random.Range(0.3f, 0.62f);
                    HallDressing.Box(b, On(left + name * 0.5f, y, 0.136f), across, into, name * 0.5f, 0.03f, 0.004f, Chalk);
                    int tally = random.Int(1, 6);
                    for (int k = 0; k < tally; k++) HallDressing.Box(b, On(left + 0.86f + k * 0.09f, y, 0.136f), across, into, 0.014f, 0.07f, 0.004f, Chalk);
                    if (tally == 5) Hall.Board(b, On(left + 0.82f, y - 0.06f, 0.14f), On(left + 1.26f, y + 0.06f, 0.14f), Vector3.UnitY, 0.012f, 0.004f, Chalk);
                }
            }
            // A ledge with the chalk and the rag.
            HallDressing.Box(b, On(0f, Middle - HalfHigh - 0.16f, 0.14f), across, into, HalfWide * 0.6f, 0.03f, 0.1f, Walnut);
            HallDressing.Box(b, On(0.6f, Middle - HalfHigh - 0.11f, 0.16f), across, into, 0.08f, 0.02f, 0.02f, Chalk);
            HallDressing.Box(b, On(-0.5f, Middle - HalfHigh - 0.1f, 0.16f), across, into, 0.16f, 0.03f, 0.06f, Dyed(Cream));
        }

        // What a barn keeps on its walls, up on the lime where it's seen: a scythe and a hay rake crossed, a cart wheel, flails, and a cloth hung behind the stage.
        private static void OnTheWalls(MeshBuilder b, HallDesign d)
        {
            VertexMaterial shaft = Hall.Timber(164, 126, 84);
            VertexMaterial steel = Hall.Metal(170, 172, 178);

            // Between the hearth and the stage's window.
            Vector3 east = HallInteriors.Into(HallSide.MinusX);
            Vector3 eastAlong = new HallWalls.Side(d, HallSide.MinusX).AlongDir;
            Vector3 OnEast(float a, float y, float out_ = 0.1f) => HallInteriors.Inside(d, HallSide.MinusX, a, y, out_);
            Vector3 heel = OnEast(3f, 5.3f), head = OnEast(4.55f, 7.9f);
            b.Tube(heel, head, 0.04f, 0.035f, 6, shaft);
            Vector3 blade = head;
            for (int i = 1; i <= 5; i++)
            {
                float t = i / 5f;
                Vector3 next = head + new Vector3(0f, 0.2f * MathF.Sin(t * 2.2f) - 0.5f * t * t, -1.5f * t);
                Hall.Board(b, blade + east * 0.02f, next + east * 0.02f, Vector3.UnitY, 0.07f * (1.1f - t), 0.008f, steel);
                blade = next;
            }
            foreach (float t in new[] { 0.35f, 0.6f }) b.Beam(Vector3.Lerp(heel, head, t), Vector3.Lerp(heel, head, t) + east * 0.26f, 0.025f, shaft);
            Vector3 rakeFoot = OnEast(4.5f, 5.3f, 0.2f), rakeHead = OnEast(2.9f, 7.9f, 0.2f);
            b.Tube(rakeFoot, rakeHead, 0.035f, 0.03f, 6, shaft);
            Vector3 bar = (rakeHead - rakeFoot).Normalized();
            Vector3 cross = Vector3.Cross(bar, east).Normalized();
            Hall.Board(b, rakeHead - cross * 0.75f, rakeHead + cross * 0.75f, east, 0.04f, 0.04f, shaft);
            for (int i = 0; i < 9; i++) b.Beam(rakeHead + cross * (-0.7f + i * 0.175f), rakeHead + cross * (-0.7f + i * 0.175f) + bar * 0.22f, 0.014f, shaft);

            // A target of plaited straw for throwing knives at, painted in rings, with three throws still in it: one good.
            Vector3 boss = OnEast(3.9f, 3.15f, 0.1f);
            (float radius, VertexMaterial paint)[] rings = { (0.82f, Hay), (0.62f, PaintedOf(Teal)), (0.42f, PaintedOf(Cream)), (0.2f, PaintedOf(Saffron)) };
            for (int i = 0; i < rings.Length; i++) b.Tube(boss, boss + east * (0.12f + i * 0.012f), rings[i].radius, rings[i].radius, 16, rings[i].paint);
            foreach ((float along, float up, float tilt) in new[] { (0.05f, 0.04f, 0.1f), (-0.36f, 0.3f, -0.2f), (0.3f, -0.5f, 0.3f) })
            {
                Vector3 point = boss + east * 0.14f + eastAlong * along + Vector3.UnitY * up;
                Vector3 out_ = (east + Vector3.UnitY * (0.25f + tilt * 0.5f) + eastAlong * tilt).Normalized();
                Hall.Board(b, point - out_ * 0.05f, point + out_ * 0.26f, Vector3.UnitY, 0.03f, 0.007f, steel);
                b.Tube(point + out_ * 0.26f, point + out_ * 0.5f, 0.03f, 0.034f, 6, Hall.Leather(72, 44, 28));
            }

            // The cloth behind the stage, by the door: the Haven's teal with a blossom on it.
            var eastSide = new HallWalls.Side(d, HallSide.MinusX);
            HallOpening stageWindow = d.Openings.Where(o => o.Side == HallSide.MinusX && !o.Door).OrderByDescending(o => o.Along).First();
            float clothFrom = stageWindow.Along + stageWindow.HalfWidth + 0.5f, clothTo = d.Inner.Y - 0.7f;
            if (clothTo - clothFrom > 0.6f)
            {
                float middle = (clothFrom + clothTo) * 0.5f, halfWide = (clothTo - clothFrom) * 0.5f;
                b.Tube(OnEast(clothFrom - 0.12f, 5.3f, 0.14f), OnEast(clothTo + 0.12f, 5.3f, 0.14f), 0.04f, 0.04f, 6, shaft);
                HallDressing.Box(b, OnEast(middle, 3.45f, 0.1f), eastSide.AlongDir, east, halfWide, 1.85f, 0.012f, Dyed(Teal));
                HallDressing.Box(b, OnEast(middle, 1.9f, 0.118f), eastSide.AlongDir, east, halfWide, 0.12f, 0.006f, Dyed(Saffron));
                HallDressing.Box(b, OnEast(middle, 5f, 0.118f), eastSide.AlongDir, east, halfWide, 0.1f, 0.006f, Dyed(Saffron));
                Vector3 heart = OnEast(middle, 3.5f, 0.12f);
                for (int i = 0; i < 5; i++)
                {
                    float a = MathF.Tau * i / 5f + MathF.PI * 0.5f;
                    HallDressing.Box(b, heart + eastSide.AlongDir * (MathF.Cos(a) * 0.3f) + Vector3.UnitY * (MathF.Sin(a) * 0.3f), eastSide.AlongDir, east, 0.17f, 0.17f, 0.006f, Dyed(Blossom));
                }
                HallDressing.Box(b, heart + east * 0.008f, eastSide.AlongDir, east, 0.1f, 0.1f, 0.008f, Dyed(Saffron));
            }

            // Over the stalls, between their windows: a cart wheel, and a pair of flails crossed.
            Vector3 west = HallInteriors.Into(HallSide.PlusX);
            Vector3 OnWest(float a, float y, float out_ = 0.1f) => HallInteriors.Inside(d, HallSide.PlusX, a, y, out_);
            float[] between = d.Pieces.Where(p => p.Kind == PieceKind.StallWall).Select(p => p.At.Y).OrderBy(z => z).ToArray();
            if (between.Length > 0) CartWheel(b, OnWest(between[0], 6.5f, 0.2f), west, 1.05f, Hall.Timber(140, 110, 82), 10);
            if (between.Length > 1)
            {
                foreach (float lean in new[] { -1f, 1f })
                {
                    Vector3 foot = OnWest(between[1] + lean * 0.8f, 5.3f, 0.12f + (lean > 0f ? 0.07f : 0f)), top = OnWest(between[1] - lean * 0.6f, 7.5f, 0.12f + (lean > 0f ? 0.07f : 0f));
                    b.Tube(foot, top, 0.035f, 0.035f, 6, shaft);
                    Vector3 swingle = top + new Vector3(0f, -0.9f, -lean * 0.5f);
                    b.Tube(top, swingle, 0.045f, 0.045f, 6, Hall.Timber(130, 96, 62));
                    b.Beam(top, top + (swingle - top) * 0.08f, 0.02f, Hall.Leather(70, 42, 26));
                }
            }
        }

        // ---- The floor.

        /// <summary>The hearth's floor of stone before the fireplace, in the hall's frame: least x, most x, least z, most z. The shell lays it and the inside kerbs it.</summary>
        public static Vector4 HearthFloor(HallDesign d)
        {
            HallPiece fire = d.Pieces.First(p => p.Kind == PieceKind.Fireplace);
            return new Vector4(-d.Inner.X, fire.At.X + fire.Half.Y + 1.7f, fire.At.Y - fire.Half.X - 0.45f, fire.At.Y + fire.Half.X + 0.45f);
        }

        // A sill of oak where the flags of the threshing floor meet the boards of each bay, a kerb round the hearth's stone, and straw trodden about wherever hay is carried.
        private static void Underfoot(MeshBuilder b, HallDesign d, WorldRandom random)
        {
            Vector2 inner = d.Inner;
            foreach (float x in new[] { -Bay, Bay })
            {
                Hall.Board(b, new Vector3(x, FloorTop + 0.008f, -inner.Y), new Vector3(x, FloorTop + 0.008f, inner.Y), Vector3.UnitX, 0.15f, 0.016f, FrameDark);
            }
            Vector4 hearth = HearthFloor(d);
            Hall.Board(b, new Vector3(hearth.Y, FloorTop + 0.02f, hearth.Z), new Vector3(hearth.Y, FloorTop + 0.02f, hearth.W), Vector3.UnitX, 0.1f, 0.03f, Dressed);
            foreach (float z in new[] { hearth.Z, hearth.W }) Hall.Board(b, new Vector3(hearth.X, FloorTop + 0.02f, z), new Vector3(hearth.Y + 0.1f, FloorTop + 0.02f, z), Vector3.UnitZ, 0.1f, 0.03f, Dressed);

            // Straw: thick at the foot of the ladder and under the loft's edge, by the bales at the door, thin along the right aisle and across the flags.
            HallPiece ladder = d.Pieces.First(p => p.Kind == PieceKind.Ladder);
            bool Open(Vector2 at)
            {
                foreach (HallPiece piece in d.Pieces)
                {
                    Vector2 local = Hall.RotateY(at - piece.At, -piece.Yaw);
                    if (piece.IsRound ? local.Length < piece.Half.X + 0.1f : MathF.Abs(local.X) < piece.Half.X + 0.1f && MathF.Abs(local.Y) < piece.Half.Y + 0.1f) return false;
                }
                return true;
            }
            Strewn(b, new Vector2(ladder.At.X - 2.2f, -PostDepth + 0.4f), new Vector2(-Bay + 1.2f, ladder.At.Y + 3.4f), 40, random, Open);
            Strewn(b, new Vector2(Bay - 1f, 5.4f), new Vector2(inner.X - 4.4f, inner.Y - 0.3f), 34, random, Open);
            Strewn(b, new Vector2(Bay + 0.3f, -inner.Y + 0.4f), new Vector2(inner.X - 4.4f, 5.4f), 24, random, Open);
            Strewn(b, new Vector2(-inner.X + 0.4f, -PostDepth), new Vector2(-Bay, inner.Y - 0.4f), 22, random, Open);
            Strewn(b, new Vector2(-Bay + 0.4f, -PostDepth), new Vector2(Bay - 0.4f, inner.Y - 0.6f), 16, random, Open);
        }
    }
}

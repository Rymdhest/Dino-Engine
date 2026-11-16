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
    /// The Forge's room, round what stands in it: the wall of tools over the bellows, the cloth hung to dry over the vats it was dyed in, soot up the wall either side of the
    /// hood, what hangs on the other walls (ladles by the furnace, the patterns things are cast from, the board of what's been ordered, belts and a quiver, a shelf of lasts),
    /// and the floor, which says where everything is carried: a kerb where the flags of the hot side meet the cobbles and a sill where the boards of the tailor's corner do, the
    /// drain the vats are emptied into, scorch before the hearth, and the cinders that drift off it. Where the floors change is worked out from what stands on them
    /// (<see cref="HotFloor"/>, <see cref="BoardFloor"/>), and the shell lays them to the same lines.
    /// </summary>
    internal static partial class Smithy
    {
        public static void Dress(MeshBuilder b, HallDesign d, List<AssetLight> lights, WorldRandom random)
        {
            ToolWall(b, d, random.Fork("tools"));
            DryingCloth(b, d, lights, random.Fork("cloth"));
            SootStains(b, d);
            OnTheWalls(b, d, lights, random.Fork("walls"));
            Underfoot(b, d, lights, random.Fork("floor"));
        }

        /// <summary>The flagged floor of the hot side, in the hall's frame: least x, most x, least z, most z. From the walls to a little past the hearth, and out as far as the anvil.</summary>
        public static Vector4 HotFloor(HallDesign d)
        {
            HallPiece hearth = d.Pieces.First(p => p.Kind == PieceKind.Hearth);
            HallPiece anvil = d.Pieces.First(p => p.Kind == PieceKind.Anvil);
            return new Vector4(-d.Inner.X, hearth.At.X + hearth.Half.X + 0.4f, -d.Inner.Y, anvil.At.Y + anvil.Half.X + 1.15f);
        }

        /// <summary>The boarded floor of the tailor's corner, laid the same way: from the table and the wheel to the walls.</summary>
        public static Vector4 BoardFloor(HallDesign d)
        {
            HallPiece table = d.Pieces.First(p => p.Kind == PieceKind.TailorTable);
            HallPiece wheel = d.Pieces.First(p => p.Kind == PieceKind.SpinningWheel);
            return new Vector4(table.At.X - table.Half.X - 0.5f, d.Inner.X, wheel.At.Y - wheel.Half.X - 1.1f, d.Inner.Y);
        }

        // ---- On the walls.

        // The wall of tools over the bellows, where the fire's light is: a board with two rails, and on them every pair of tongs, hammer, punch and file the Forge has.
        private static void ToolWall(MeshBuilder b, HallDesign d, WorldRandom random)
        {
            HallPiece bellows = d.Pieces.First(p => p.Kind == PieceKind.Bellows);
            var s = new HallWalls.Side(d, HallSide.Back);
            Vector3 into = HallInteriors.Into(HallSide.Back);
            Vector3 along = s.AlongDir;
            Vector3 On(float a, float y, float out_) => HallInteriors.Inside(d, HallSide.Back, a, y, out_);
            float from = bellows.At.X - bellows.Half.X + 0.1f, to = bellows.At.X + bellows.Half.X - 0.1f;
            const float Low = 3.3f, High = 6.5f;
            int boards = (int)MathF.Round((to - from) / 0.42f);
            for (int i = 0; i < boards; i++)
            {
                float a = from + (to - from) * (i + 0.5f) / boards;
                HallDressing.Box(b, On(a, (Low + High) * 0.5f, 0.035f), along, into, (to - from) / boards * 0.5f - 0.008f, (High - Low) * 0.5f, 0.035f, Weathered(random));
            }
            foreach (float y in new[] { High - 0.35f, Low + 1.45f })
            {
                HallDressing.Box(b, On((from + to) * 0.5f, y, 0.11f), along, into, (to - from) * 0.5f, 0.06f, 0.05f, FrameDark);
                for (float a = from + 0.22f; a < to - 0.15f; a += random.Range(0.3f, 0.44f))
                {
                    Vector3 hook = On(a, y - 0.02f, 0.2f);
                    float length = random.Range(0.85f, 1.3f);
                    switch (random.Int(4))
                    {
                        case 0:
                        case 1:
                            Tongs(b, hook - Vector3.UnitY * length, hook);
                            break;
                        case 2:
                            Hammer(b, hook - Vector3.UnitY * 0.06f, hook - Vector3.UnitY * length * 0.8f, 0.85f);
                            break;
                        default:
                            b.Beam(hook, hook - Vector3.UnitY * length * 0.7f, 0.022f, random.Chance(0.5f) ? DarkSteel : Hall.Iron(72));
                            b.Tube(hook, hook + Vector3.UnitY * 0.16f, 0.03f, 0.04f, 6, Handle);
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// The cloth hung to dry on a pole along the back wall, over the vats it came out of, so that what runs off it runs back in: lengths of every colour the Haven dyes,
        /// each darker at its hem where it's wettest, with the window behind them.
        /// </summary>
        private static void DryingCloth(MeshBuilder b, HallDesign d, List<AssetLight> lights, WorldRandom random)
        {
            HallPiece[] vats = d.Pieces.Where(p => p.Kind == PieceKind.Vat).OrderBy(p => p.At.X).ToArray();
            if (vats.Length == 0) return;
            float from = vats[0].At.X - vats[0].Half.X - 0.3f, to = d.Inner.X - 0.25f;
            float z = -d.Inner.Y + 0.62f;
            const float Pole = 6.9f;
            b.Tube(new Vector3(from - 0.2f, Pole, z), new Vector3(to, Pole, z), 0.045f, 0.045f, 6, Handle);
            foreach (float x in new[] { from, (from + to) * 0.5f, to - 0.15f })
            {
                b.Beam(new Vector3(x, Pole + 0.5f, -d.Inner.Y + 0.02f), new Vector3(x, Pole, z), 0.028f, Hall.Iron(70));
                b.Beam(new Vector3(x, Pole - 0.02f, -d.Inner.Y + 0.02f), new Vector3(x, Pole - 0.02f, z), 0.028f, Hall.Iron(70));
            }
            Vector3[] colours = { Teal, Saffron, Blossom, new Vector3(206, 198, 176), Blue, Teal };
            float at = from + 0.2f;
            for (int i = 0; at < to - 1f; i++)
            {
                float wide = random.Range(0.8f, 1.1f);
                float length = random.Range(2.3f, 3f);
                Vector3 colour = colours[i % colours.Length];
                float sway = random.Range(-0.08f, 0.08f);
                foreach (float side in new[] { -1f, 1f })
                {
                    // Over the pole, and down either side of it: the longer side to the room.
                    float drop = side > 0f ? length : length * 0.42f;
                    Vector3 t0 = new Vector3(at, Pole + 0.05f, z + side * 0.05f), t1 = new Vector3(at + wide, Pole + 0.05f, z + side * 0.05f);
                    Vector3 wet0 = new Vector3(at + 0.02f, Pole - drop + 0.5f, z + side * 0.09f + sway * 0.6f), wet1 = new Vector3(at + wide - 0.02f, Pole - drop + 0.5f, z + side * 0.09f + sway * 0.6f);
                    Vector3 b0 = new Vector3(at + 0.03f, Pole - drop, z + side * 0.1f + sway), b1 = new Vector3(at + wide - 0.03f, Pole - drop, z + side * 0.1f + sway);
                    Hall.Face(b, t0, t1, wet1, wet0, Vector3.UnitZ * side, Dyed(colour));
                    Hall.Face(b, wet0, wet1, b1, b0, Vector3.UnitZ * side, Dyed(colour * 0.72f));
                    if (side > 0f)
                    {
                        foreach (float t in new[] { 0.22f, 0.7f }) b.Beam(Vector3.Lerp(b0, b1, t), Vector3.Lerp(b0, b1, t) - Vector3.UnitY * random.Range(0.12f, 0.3f), 0.007f, Dyed(colour * 0.72f));
                    }
                }
                b.Box(new Vector3(at + wide * 0.5f, Pole + 0.06f, z), new Vector3(wide * 0.5f, 0.02f, 0.06f), Dyed(colour));
                // What runs off its hem, in its own colour, back into the vat under it.
                lights.Add(AssetLight.Emitting(new Vector3(at + wide * 0.5f, Pole - length, z + 0.1f + sway), "Smithing.Drip", wide, tint: colour / 255f));
                at += wide + random.Range(0.14f, 0.3f);
            }
        }

        // Soot up the wall either side of the hood and over the furnace, where the smoke that misses the flue goes.
        private static void SootStains(MeshBuilder b, HallDesign d)
        {
            HallPiece hearth = d.Pieces.First(p => p.Kind == PieceKind.Hearth);
            HallPiece smelter = d.Pieces.First(p => p.Kind == PieceKind.Smelter);
            Vector3 into = HallInteriors.Into(HallSide.Back);
            VertexMaterial soot = Hall.Flat(48, 44, 42);
            void Smear(float x, float y, float wide, float tall, int seed)
            {
                var random = new WorldRandom(0x5007).Fork(seed);
                Vector3 centre = HallInteriors.Inside(d, HallSide.Back, x, y, 0.014f);
                const int Points = 12;
                var rim = new Vector3[Points];
                for (int i = 0; i < Points; i++)
                {
                    float a = MathF.Tau * i / Points;
                    float reach = random.Range(0.6f, 1.05f);
                    rim[i] = centre + new Vector3(MathF.Cos(a) * wide * reach, MathF.Sin(a) * tall * reach * (MathF.Sin(a) > 0f ? 1.3f : 0.7f), 0f);
                }
                for (int i = 0; i < Points; i++) Hall.Tri(b, centre, rim[i], rim[(i + 1) % Points], into, soot);
            }
            float edge = hearth.Half.X + 0.5f;
            Smear(hearth.At.X - edge, 6.9f, 0.9f, 1.3f, 1);
            Smear(hearth.At.X + edge, 6.9f, 0.9f, 1.3f, 2);
            Smear(hearth.At.X - edge - 0.3f, 4.6f, 0.5f, 0.8f, 3);
            Smear(hearth.At.X + edge + 0.2f, 4.9f, 0.45f, 0.7f, 4);
            Smear(smelter.At.X + 0.4f, 7.2f, 1.5f, 1.2f, 5);
        }

        private static void OnTheWalls(MeshBuilder b, HallDesign d, List<AssetLight> lights, WorldRandom random)
        {
            // The hot side's wall (-x). By the furnace: ladles and a rake, long in the handle, on a rail.
            var hot = new HallWalls.Side(d, HallSide.MinusX);
            Vector3 east = HallInteriors.Into(HallSide.MinusX);
            Vector3 OnHot(float a, float y, float out_ = 0.1f) => HallInteriors.Inside(d, HallSide.MinusX, a, y, out_);
            HallPiece smelter = d.Pieces.First(p => p.Kind == PieceKind.Smelter);
            HallPiece bed = d.Pieces.First(p => p.Kind == PieceKind.CastingBed);
            float railFrom = smelter.At.Y + smelter.Half.X + 0.2f, railTo = bed.At.Y - bed.Half.Y + 0.4f;
            if (railTo - railFrom > 0.8f)
            {
                HallDressing.Box(b, OnHot((railFrom + railTo) * 0.5f, 5.4f, 0.05f), hot.AlongDir, east, (railTo - railFrom) * 0.5f, 0.07f, 0.05f, FrameDark);
                int hung = 0;
                for (float a = railFrom + 0.25f; a < railTo - 0.1f; a += 0.42f, hung++)
                {
                    Vector3 hook = OnHot(a, 5.36f, 0.16f);
                    float length = 2.1f + (hung % 3) * 0.3f;
                    b.Beam(hook, hook - Vector3.UnitY * length, 0.024f, Hall.Iron(70));
                    if (hung % 2 == 0)
                    {
                        // A ladle: a cup of iron, black inside.
                        b.Tube(hook - Vector3.UnitY * (length + 0.2f), hook - Vector3.UnitY * length, 0.1f, 0.17f, 8, Hall.Iron(70), capped: false);
                        b.Tube(hook - Vector3.UnitY * (length + 0.2f), hook - Vector3.UnitY * (length + 0.22f), 0.1f, 0.06f, 8, Hall.Iron(60));
                    }
                    else b.Box(hook - Vector3.UnitY * length, new Vector3(0.03f, 0.05f, 0.16f), Hall.Iron(70));
                }
            }

            // Between its windows: the patterns things are cast from, in pale wood, on pegs. An axe's head, a blade, a ring, a ploughshare.
            HallOpening[] hotWindows = d.Openings.Where(o => o.Side == HallSide.MinusX && !o.Door).OrderBy(o => o.Along).ToArray();
            if (hotWindows.Length >= 2)
            {
                float pier = (hotWindows[0].Along + hotWindows[0].HalfWidth + hotWindows[1].Along - hotWindows[1].HalfWidth) * 0.5f;
                VertexMaterial pattern = Hall.Timber(196, 164, 118);
                Vector3 P(float a, float y) => OnHot(pier + a, y, 0.13f);
                // The axe: a wedge with its eye.
                Hall.Tri(b, P(-0.5f, 5.9f), P(-0.5f, 5.3f), P(0.1f, 5.6f), east, pattern);
                Hall.Face(b, P(0.1f, 5.72f), P(0.1f, 5.48f), P(0.36f, 5.5f), P(0.36f, 5.7f), east, pattern);
                b.Tube(P(0.24f, 5.6f), P(0.24f, 5.6f) + east * 0.03f, 0.06f, 0.06f, 7, Hall.Flat(40, 34, 30));
                // The blade, point down.
                Hall.Board(b, P(0.62f, 6.2f), P(0.62f, 4.3f), hot.AlongDir, 0.08f, 0.02f, pattern);
                Hall.Tri(b, P(0.54f, 4.3f), P(0.7f, 4.3f), P(0.62f, 4f), east, pattern);
                // The ring, in two halves pegged together.
                Vector3 ring = P(-0.2f, 4.6f);
                for (int i = 0; i < 12; i++)
                {
                    float a0 = MathF.Tau * i / 12f, a1 = MathF.Tau * (i + 1) / 12f;
                    Hall.Board(b, ring + (hot.AlongDir * MathF.Cos(a0) + Vector3.UnitY * MathF.Sin(a0)) * 0.34f, ring + (hot.AlongDir * MathF.Cos(a1) + Vector3.UnitY * MathF.Sin(a1)) * 0.34f, east, 0.03f, 0.05f, pattern);
                }
                // The share.
                Hall.Tri(b, P(-0.66f, 4.1f), P(-0.1f, 3.6f), P(-0.62f, 3.5f), east, pattern);
                foreach ((float a, float y) in new[] { (-0.3f, 6f), (0.62f, 6.3f), (-0.2f, 5f), (-0.5f, 4.15f) }) b.Beam(OnHot(pier + a, y, 0f), OnHot(pier + a, y, 0.2f), 0.02f, Handle);
            }

            // By the door, between the grindstone's window and the shelf: the board of what's been ordered, every slip pinned, some struck through.
            HallPiece shelf = d.Pieces.First(p => p.Kind == PieceKind.Uncollected);
            float boardTo = shelf.At.Y - shelf.Half.X - 0.15f, boardFrom = hotWindows.Length > 0 ? hotWindows[^1].Along + hotWindows[^1].HalfWidth + 0.25f : boardTo - 1.2f;
            if (boardTo - boardFrom > 0.7f)
            {
                float middle = (boardFrom + boardTo) * 0.5f, half = (boardTo - boardFrom) * 0.5f;
                HallDressing.Box(b, OnHot(middle, 4.7f, 0.04f), hot.AlongDir, east, half, 1.05f, 0.04f, Walnut);
                for (int row = 0; row < 3; row++)
                {
                    for (int column = 0; column < 2; column++)
                    {
                        if (row == 2 && column == 1) continue;
                        Vector3 slip = OnHot(middle + (column - 0.5f) * half * 0.9f + random.Range(-0.05f, 0.05f), 5.35f - row * 0.64f + random.Range(-0.04f, 0.04f), 0.09f);
                        HallDressing.Box(b, slip, hot.AlongDir, east, 0.2f, 0.25f, 0.006f, Hall.Paper);
                        for (int line = 0; line < 3; line++) HallDressing.Box(b, slip + Vector3.UnitY * (0.12f - line * 0.1f) + east * 0.008f, hot.AlongDir, east, 0.13f, 0.012f, 0.003f, Hall.Ink);
                        if ((row + column) % 2 == 0) Hall.Board(b, slip + hot.AlongDir * -0.17f + Vector3.UnitY * 0.2f + east * 0.012f, slip + hot.AlongDir * 0.17f - Vector3.UnitY * 0.2f + east * 0.012f, Vector3.UnitY, 0.012f, 0.003f, Hall.Flat(150, 60, 30));
                        b.Box(slip + Vector3.UnitY * 0.21f + east * 0.012f, new Vector3(0.02f), Bright);
                    }
                }
                // A lantern on an arm over it, to read them by.
                LanternArm(b, OnHot(middle, 7f, 0f), east, 1f, lights, 40f, 14f);
            }

            // The soft side's wall (+x). Between the leather's window and the loom's: what's finished, on a rail of pegs. Belts by their buckles, a quiver with its arrows, a satchel.
            var soft = new HallWalls.Side(d, HallSide.PlusX);
            Vector3 west = HallInteriors.Into(HallSide.PlusX);
            Vector3 OnSoft(float a, float y, float out_ = 0.1f) => HallInteriors.Inside(d, HallSide.PlusX, a, y, out_);
            HallOpening[] softWindows = d.Openings.Where(o => o.Side == HallSide.PlusX && !o.Door).OrderBy(o => o.Along).ToArray();
            if (softWindows.Length >= 3)
            {
                float pegFrom = softWindows[1].Along + softWindows[1].HalfWidth + 0.35f, pegTo = softWindows[2].Along - softWindows[2].HalfWidth - 0.35f;
                HallDressing.Box(b, OnSoft((pegFrom + pegTo) * 0.5f, 5.6f, 0.05f), soft.AlongDir, west, (pegTo - pegFrom) * 0.5f, 0.08f, 0.05f, FrameDark);
                LanternArm(b, OnSoft((pegFrom + pegTo) * 0.5f, 7.1f, 0f), west, 1f, lights, 40f, 14f);
                int hung = 0;
                for (float a = pegFrom + 0.3f; a < pegTo - 0.2f; a += 0.56f, hung++)
                {
                    Vector3 peg = OnSoft(a, 5.56f, 0.2f);
                    b.Beam(OnSoft(a, 5.6f, 0.05f), peg, 0.025f, Handle);
                    switch (hung % 4)
                    {
                        case 0:
                        case 2:
                        {
                            float length = 1.7f + (hung % 3) * 0.25f;
                            b.Box(peg - Vector3.UnitY * 0.08f, new Vector3(0.02f, 0.06f, 0.06f), Steel);
                            Hall.Board(b, peg - Vector3.UnitY * 0.1f, peg - Vector3.UnitY * length, soft.AlongDir, 0.05f, 0.012f, hung % 4 == 0 ? Tan : DarkHide);
                            break;
                        }
                        case 1:
                        {
                            // The quiver, and what's in it.
                            Vector3 mouth = peg - Vector3.UnitY * 0.3f + west * 0.06f;
                            b.Tube(mouth - Vector3.UnitY * 1.5f, mouth, 0.13f, 0.17f, 8, Tan);
                            for (int arrow = 0; arrow < 5; arrow++)
                            {
                                Vector3 nock = mouth + new Vector3(0f, 0.5f + arrow * 0.03f, (arrow - 2) * 0.05f) + west * ((arrow % 2) * 0.05f);
                                b.Beam(mouth, nock, 0.01f, Pale);
                                Hall.Board(b, nock - Vector3.UnitY * 0.26f, nock, soft.AlongDir, 0.035f, 0.004f, Dyed(arrow % 2 == 0 ? Teal : Cream));
                            }
                            b.Beam(peg, mouth - Vector3.UnitY * 0.2f, 0.014f, DarkHide);
                            break;
                        }
                        default:
                        {
                            // The satchel, by its strap.
                            Vector3 bag = peg - Vector3.UnitY * 1.25f + west * 0.08f;
                            b.Beam(peg, bag + Vector3.UnitY * 0.3f, 0.016f, DarkHide);
                            HallDressing.Box(b, bag, soft.AlongDir, west, 0.3f, 0.3f, 0.1f, Tan);
                            HallDressing.Box(b, bag + Vector3.UnitY * 0.12f + west * 0.1f, soft.AlongDir, west, 0.31f, 0.2f, 0.012f, DarkHide);
                            b.Box(bag - Vector3.UnitY * 0.06f + west * 0.12f, new Vector3(0.012f, 0.04f, 0.04f), Steel);
                            break;
                        }
                    }
                }

                // Between the hide's window and the leather's: a shelf of lasts, a foot for everyone the Forge has shod.
                float lasts = (softWindows[0].Along + softWindows[0].HalfWidth + softWindows[1].Along - softWindows[1].HalfWidth) * 0.5f;
                Hall.Board(b, OnSoft(lasts - 0.85f, 4.9f, 0.24f), OnSoft(lasts + 0.85f, 4.9f, 0.24f), west, 0.22f, 0.04f, Walnut);
                foreach (float e in new[] { -0.7f, 0.7f }) Hall.Board(b, OnSoft(lasts + e, 4.86f, 0.4f), OnSoft(lasts + e, 4.4f, 0.06f), soft.AlongDir, 0.04f, 0.035f, Walnut);
                for (int i = 0; i < 5; i++)
                {
                    Vector3 last = OnSoft(lasts - 0.68f + i * 0.34f, 4.94f, 0.22f);
                    b.Box(last + Vector3.UnitY * 0.08f + west * 0.05f, new Vector3(0.14f, 0.08f, 0.08f), Pale);
                    b.Tube(last + Vector3.UnitY * 0.1f - west * 0.06f, last + Vector3.UnitY * 0.42f - west * 0.07f, 0.08f, 0.07f, 7, Pale);
                }
            }
        }

        // ---- The floor.

        private static void Underfoot(MeshBuilder b, HallDesign d, List<AssetLight> lights, WorldRandom random)
        {
            // A kerb of dressed stone where the flags stop, and a sill of oak where the boards do.
            Vector4 hot = HotFloor(d), boards = BoardFloor(d);
            float y = FloorTop + 0.012f;
            Hall.Board(b, new Vector3(hot.X, y, hot.W), new Vector3(hot.Y + 0.1f, y, hot.W), Vector3.UnitZ, 0.1f, 0.02f, Dressed);
            Hall.Board(b, new Vector3(hot.Y, y, hot.Z), new Vector3(hot.Y, y, hot.W), Vector3.UnitX, 0.1f, 0.02f, Dressed);
            Hall.Board(b, new Vector3(boards.X, y, boards.Z), new Vector3(boards.X, y, boards.W), Vector3.UnitX, 0.13f, 0.02f, FrameDark);
            Hall.Board(b, new Vector3(boards.X - 0.13f, y, boards.Z), new Vector3(boards.Y, y, boards.Z), Vector3.UnitZ, 0.13f, 0.02f, FrameDark);

            // Before the hearth: scorch where what's dropped has lain, and the cinders that never stop drifting off it.
            HallPiece hearth = d.Pieces.First(p => p.Kind == PieceKind.Hearth);
            float front = hearth.At.Y + hearth.Half.Y;
            float floor = FloorTop + 0.011f;
            Blot(b, new Vector3(hearth.At.X + 1.3f, floor, front + 0.7f), 0.5f, Hall.Flat(40, 36, 34), 201, 1.6f, 0.4f);
            Blot(b, new Vector3(hearth.At.X - 0.9f, floor, front + 0.5f), 0.36f, Hall.Flat(40, 36, 34), 202, 1.2f);
            lights.Add(AssetLight.Emitting(new Vector3(hearth.At.X, 1.2f, front + 0.6f), "Smithing.Cinders"));

            // The drain the vats are emptied into: a grate in the cobbles, and the wet that runs to it from each, in its own colour.
            HallPiece[] vats = d.Pieces.Where(p => p.Kind == PieceKind.Vat).OrderBy(p => p.At.X).ToArray();
            if (vats.Length > 0)
            {
                Vector3 drain = new Vector3(vats[0].At.X - 0.6f, FloorTop, vats[0].At.Y + vats[0].Half.X + 1.5f);
                b.Box(drain + Vector3.UnitY * 0.006f, new Vector3(0.5f, 0.006f, 0.5f), Hall.Flat(20, 20, 22));
                for (int bar = 0; bar < 5; bar++) Hall.Board(b, drain + new Vector3(-0.4f + bar * 0.2f, 0.02f, -0.5f), drain + new Vector3(-0.4f + bar * 0.2f, 0.02f, 0.5f), Vector3.UnitX, 0.035f, 0.012f, Hall.Iron(72));
                foreach (float s in new[] { -1f, 1f }) Hall.Board(b, drain + new Vector3(-0.52f, 0.02f, s * 0.5f), drain + new Vector3(0.52f, 0.02f, s * 0.5f), Vector3.UnitZ, 0.04f, 0.014f, Hall.Iron(72));
                for (int i = 0; i < vats.Length; i++)
                {
                    Vector3 lip = new Vector3(vats[i].At.X, FloorTop + 0.012f, vats[i].At.Y + vats[i].Half.X + 0.1f);
                    Vector3 run = drain + new Vector3(0.1f * i, 0.012f, -0.3f) - lip;
                    Hall.Board(b, lip, lip + run, Vector3.Cross(run, Vector3.UnitY), 0.07f, 0.004f, vats[i].Variant == 1 ? Hall.Flat(34, 92, 92) : Hall.Flat(74, 56, 40));
                }
            }

            // Shavings and offcuts trodden about the leather table, and wool by the wheel.
            HallPiece leather = d.Pieces.First(p => p.Kind == PieceKind.LeatherTable);
            for (int i = 0; i < 14; i++)
            {
                Vector3 at = new Vector3(leather.At.X - leather.Half.Y - random.Range(0.2f, 1.3f), floor, leather.At.Y + random.Range(-leather.Half.X, leather.Half.X));
                Blot(b, at, random.Range(0.05f, 0.11f), i % 3 == 0 ? Rawhide : i % 3 == 1 ? Tan : DarkHide, 220 + i, random.Range(1f, 3f), random.Range(0f, MathF.PI));
            }
            HallPiece wheel = d.Pieces.First(p => p.Kind == PieceKind.SpinningWheel);
            for (int i = 0; i < 6; i++)
            {
                Vector3 at = new Vector3(wheel.At.X + random.Range(-1.3f, 0.6f), floor + 0.03f, wheel.At.Y + random.Range(0.75f, 1.3f) * (i % 2 == 0 ? 1f : -1f));
                Blob(b, at, new Vector3(random.Range(0.07f, 0.12f), 0.04f, random.Range(0.07f, 0.12f)), Fleece, 6, 3);
            }
        }
    }
}

using Dino_Engine.Modelling.Model;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Halls
{
    /// <summary>
    /// What stands in the Archive besides its records (Exhibits.cs), each built in its own frame to the numbers of its <see cref="HallPiece"/>, its middle on the floor at the
    /// origin and its front towards +z: the columns of the nave, the cases its books are kept in, the keeper's desk, the globe, the benches and the tall stands of candles.
    /// Everything is marble, walnut, gold and the Haven's teal, and nothing is out of its place: a hall that's kept. A light is set well clear of anything pale, which
    /// it would wash white.
    /// </summary>
    internal static partial class Basilica
    {
        public static void Piece(MeshBuilder b, in HallPiece piece, HallKit kit, List<AssetLight> lights, WorldRandom random)
        {
            Vector2 half = piece.Half;
            float h = piece.Height;
            switch (piece.Kind)
            {
                case PieceKind.Column: Column(b, half.X, h, lights); break;
                case PieceKind.Plinth: Exhibits.Build(b, (ExhibitKind)piece.Variant, half, h, kit, lights, random); break;
                case PieceKind.Bookcase: Bookcase(b, half, h, piece.Variant, random); break;
                case PieceKind.ReadingDesk: Desk(b, half, h, lights); break;
                case PieceKind.CandleStand: Torchere(b, half.X, h, lights); break;
                case PieceKind.Bench: Bench(b, half, h); break;
                case PieceKind.Globe: Globe(b, half.X, h); break;
                default: HallFurniture.Build(b, piece, kit, lights, random); break;
            }
        }

        /// <summary>
        /// A column of the nave: an eight-sided foot of teal, a moulded base, a shaft of white marble a little fuller below its middle than above, a necking of gold, a
        /// capital spreading to a square slab, and the block the vault sprang from, which is where the roof is taken off. It carries a candle on an arm of gold towards
        /// the nave and another towards the aisle.
        /// </summary>
        private static void Column(MeshBuilder b, float r, float h, List<AssetLight> lights)
        {
            b.Tube(Vector3.Zero, Vector3.UnitY * 0.42f, r, r, 8, Teal);
            b.Tube(Vector3.UnitY * 0.42f, Vector3.UnitY * 0.5f, r * 0.95f, r * 0.95f, 8, Gilt);
            Hall.Turned(b, Vector3.UnitY * 0.5f, White, 18, (0f, 0.88f), (0.1f, 0.92f), (0.2f, 0.84f), (0.3f, 0.72f), (0.42f, 0.78f), (0.5f, 0.69f), (0.56f, 0.65f));
            float shaftFrom = 1.06f, shaftTo = h - 1.7f;
            float swell = shaftFrom + (shaftTo - shaftFrom) * 0.36f;
            b.Tube(Vector3.UnitY * shaftFrom, Vector3.UnitY * swell, 0.64f, 0.63f, 20, White, capped: false);
            b.Tube(Vector3.UnitY * swell, Vector3.UnitY * shaftTo, 0.63f, 0.55f, 20, White, capped: false);
            b.Tube(Vector3.UnitY * (shaftTo - 0.02f), Vector3.UnitY * (shaftTo + 0.1f), 0.6f, 0.6f, 20, Gilt, capped: false);

            // The capital: a bell with eight leaves of gold standing against it, under a square slab.
            float bell = shaftTo + 0.1f;
            Hall.Turned(b, Vector3.UnitY * bell, White, 16, (0f, 0.56f), (0.22f, 0.6f), (0.58f, 0.76f), (0.76f, 0.86f));
            for (int i = 0; i < 8; i++)
            {
                float a = MathF.Tau * i / 8f;
                Vector3 out_ = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a));
                Hall.Board(b, out_ * 0.6f + Vector3.UnitY * (bell + 0.05f), out_ * 0.82f + Vector3.UnitY * (bell + 0.62f), new Vector3(-out_.Z, 0f, out_.X), 0.13f, 0.03f, Gilt);
            }
            // A round slab over it, and the foot of what the vault sprang from, which is where the roof is taken off. It has a little light of its own over it, as
            // nothing in the room is high enough to light it: without it every column ends in a black round after dark.
            float slab = bell + 0.76f;
            b.Tube(Vector3.UnitY * slab, Vector3.UnitY * (slab + 0.07f), 0.93f, 0.93f, 20, Gilt, capped: false);
            b.Tube(Vector3.UnitY * (slab + 0.07f), Vector3.UnitY * (slab + 0.2f), 0.9f, 0.9f, 20, White);
            b.Tube(Vector3.UnitY * (slab + 0.2f), Vector3.UnitY * (slab + 0.23f), 0.66f, 0.66f, 20, Gilt);
            b.Tube(Vector3.UnitY * (slab + 0.23f), Vector3.UnitY * (slab + 0.38f), 0.6f, 0.56f, 20, Ivory);
            // Under where the walls end, or it's taken to be outside the room and lights nothing in it (Halls.glsl, insideHall).
            lights.Add(new AssetLight { Position = new Vector3(0f, slab + 1.2f, 0f), Colour = new Vector3(1f, 0.88f, 0.72f), Intensity = 1.5f, Radius = 3.6f });

            // Its candles.
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 root = new Vector3(s * 0.58f, 5.1f, 0f), cup = new Vector3(s * 1.2f, 5.35f, 0f);
                b.Tube(new Vector3(s * 0.6f, 5.1f, 0f), new Vector3(s * 0.68f, 5.1f, 0f), 0.14f, 0.1f, 8, Gilt);
                Hall.Bent(b, Gilt, Vector3.UnitZ, 0.035f, 0.03f, root, new Vector3(s * 0.95f, 4.92f, 0f), new Vector3(s * 1.2f, 5.08f, 0f), cup);
                b.Tube(cup, cup + Vector3.UnitY * 0.05f, 0.13f, 0.16f, 8, Gilt);
                Hall.Candle(b, cup + Vector3.UnitY * 0.05f, 0.5f, 0.06f, lights);
            }
        }

        // Volumes of one set along a shelf, spines out towards +z: all of a height and a binding, with two bands of gold, a few of them leaning where one's been taken out.
        private static void Volumes(MeshBuilder b, Vector3 start, float length, float depth, float tall, VertexMaterial cover, WorldRandom random)
        {
            float at = 0.03f;
            int taken = random.Chance(0.3f) ? random.Int(3, 9) : -1;
            for (int i = 0; at < length - 0.15f; i++)
            {
                float thick = random.Range(0.12f, 0.15f);
                if (i == taken)
                {
                    at += thick;
                    continue;
                }
                float height = tall * random.Range(0.93f, 1f);
                Vector3 middle = start + new Vector3(at + thick * 0.5f, height * 0.5f, 0f);
                b.Box(middle, new Vector3(thick * 0.5f - 0.006f, height * 0.5f, depth * 0.5f), cover);
                foreach (float band in new[] { 0.22f, 0.8f }) b.Box(start + new Vector3(at + thick * 0.5f, height * band, depth * 0.5f + 0.004f), new Vector3(thick * 0.5f - 0.014f, 0.022f, 0.004f), PaleGilt);
                at += thick;
            }
        }

        /// <summary>
        /// A case of the archive: walnut on a foot of marble, shelves of bound volumes, each shelf one set in one binding, a shelf of rolled charts among them, and a
        /// cornice of gold. The two in the end wall are the tallest and carry a pediment; one of them has the ladder against it.
        /// </summary>
        private static void Bookcase(MeshBuilder b, Vector2 half, float h, int variant, WorldRandom random)
        {
            bool great = variant is 1 or 2;
            float foot = 0.34f, crown = great ? 0.9f : 0.34f;
            b.Box(new Vector3(0f, foot * 0.5f, 0.03f), new Vector3(half.X + 0.05f, foot * 0.5f, half.Y + 0.03f), Teal);
            float top = h - crown;
            b.Box(new Vector3(0f, (foot + top) * 0.5f, -half.Y + 0.05f), new Vector3(half.X, (top - foot) * 0.5f, 0.05f), DarkWalnut);
            foreach (float x in new[] { -half.X + 0.08f, half.X - 0.08f }) b.Box(new Vector3(x, (foot + top) * 0.5f, 0f), new Vector3(0.08f, (top - foot) * 0.5f, half.Y), Walnut);
            int shelves = (int)MathF.Round((top - foot) / 0.98f);
            float room = (top - foot) / shelves;
            Vector3[] bindings = { new(28, 76, 80), new(38, 50, 92), new(184, 168, 132), new(96, 64, 40), new(42, 72, 50), new(72, 76, 88) };
            for (int shelf = 0; shelf <= shelves; shelf++)
            {
                float y = foot + shelf * room;
                b.Box(new Vector3(0f, y + 0.035f, 0f), new Vector3(half.X - 0.14f, 0.035f, half.Y), Walnut);
                if (shelf == shelves) break;
                Vector3 start = new Vector3(-half.X + 0.18f, y + 0.07f, 0.02f);
                float length = half.X * 2f - 0.36f;
                if (shelf == (variant + 1) % shelves)
                {
                    // Charts, rolled, their ends out.
                    for (float x = 0.14f; x < length - 0.1f; x += 0.25f)
                    {
                        b.Tube(start + new Vector3(x, 0.12f, -half.Y * 0.6f), start + new Vector3(x, 0.12f, half.Y * 0.72f), 0.1f, 0.1f, 6, Hall.Flat(206, 192, 156));
                        if (x + 0.25f < length - 0.1f) b.Tube(start + new Vector3(x + 0.125f, 0.32f, -half.Y * 0.6f), start + new Vector3(x + 0.125f, 0.32f, half.Y * 0.66f), 0.1f, 0.1f, 6, Hall.Flat(190, 174, 140));
                    }
                    continue;
                }
                Vector3 binding = bindings[(shelf * 2 + variant) % bindings.Length];
                Volumes(b, start, length, half.Y * 1.5f, room - 0.24f, Hall.Leather((int)binding.X, (int)binding.Y, (int)binding.Z), random);
            }
            // Its cornice, and over the great ones a pediment with the blossom in it.
            b.Box(new Vector3(0f, top + 0.1f, 0.05f), new Vector3(half.X + 0.1f, 0.1f, half.Y + 0.07f), Walnut);
            b.Box(new Vector3(0f, top + 0.24f, 0.07f), new Vector3(half.X + 0.16f, 0.04f, half.Y + 0.11f), Gilt);
            if (!great) return;
            float baseY = top + 0.28f;
            Vector3 l = new Vector3(-half.X - 0.14f, baseY, half.Y + 0.12f), r = new Vector3(half.X + 0.14f, baseY, half.Y + 0.12f), p = new Vector3(0f, baseY + 0.6f, half.Y + 0.12f);
            Vector3 back = new Vector3(0f, 0f, -half.Y * 2f - 0.1f);
            Hall.Tri(b, l, r, p, Vector3.UnitZ, Walnut);
            Hall.Face(b, l, p, p + back, l + back, new Vector3(-0.5f, 1f, 0f), Walnut);
            Hall.Face(b, p, r, r + back, p + back, new Vector3(0.5f, 1f, 0f), Walnut);
            Hall.Board(b, l + new Vector3(0f, 0.03f, 0.03f), p + new Vector3(0f, 0.05f, 0.03f), Vector3.UnitZ, 0.04f, 0.035f, Gilt);
            Hall.Board(b, p + new Vector3(0f, 0.05f, 0.03f), r + new Vector3(0f, 0.03f, 0.03f), Vector3.UnitZ, 0.04f, 0.035f, Gilt);
            b.Tube(new Vector3(0f, baseY + 0.23f, half.Y + 0.12f), new Vector3(0f, baseY + 0.23f, half.Y + 0.17f), 0.13f, 0.1f, 10, Gilt);
            if (variant != 1) return;
            // The ladder, hooked on its rail.
            b.Beam(new Vector3(-half.X + 0.1f, top - 0.5f, half.Y + 0.1f), new Vector3(half.X - 0.1f, top - 0.5f, half.Y + 0.1f), 0.03f, Gilt);
            Vector3 footAt = new Vector3(half.X * 0.35f, 0f, half.Y + 1.15f), headAt = new Vector3(half.X * 0.35f, top - 0.5f, half.Y + 0.14f);
            foreach (float s in new[] { -0.3f, 0.3f }) Hall.Board(b, footAt + Vector3.UnitX * s, headAt + Vector3.UnitX * s, Vector3.UnitZ, 0.05f, 0.03f, Walnut);
            for (int i = 1; i < 12; i++)
            {
                Vector3 rung = Vector3.Lerp(footAt, headAt, i / 12f);
                b.Beam(rung - Vector3.UnitX * 0.3f, rung + Vector3.UnitX * 0.3f, 0.028f, Walnut);
            }
        }

        /// <summary>
        /// The keeper's desk: walnut, its top let in with teal leather inside a line of gold, the great book of the records open on a slope with its ribbon across the
        /// page, a pen in its ink, three ledgers squared in a pile, a chart rolled and tied, a candle in silver, and the keeper's chair pushed back from it.
        /// </summary>
        private static void Desk(MeshBuilder b, Vector2 half, float h, List<AssetLight> lights)
        {
            b.Box(new Vector3(0f, h - 0.08f, 0f), new Vector3(half.X, 0.08f, half.Y), Walnut);
            b.Box(new Vector3(0f, h + 0.004f, 0f), new Vector3(half.X - 0.14f, 0.004f, half.Y - 0.14f), Gilt);
            b.Box(new Vector3(0f, h + 0.008f, 0f), new Vector3(half.X - 0.18f, 0.004f, half.Y - 0.18f), Hall.Leather(26, 78, 82));
            // Two pedestals of drawers, and the gap for knees between them.
            foreach (float s in new[] { -1f, 1f })
            {
                float x = s * (half.X - 0.55f);
                b.Box(new Vector3(x, (h - 0.16f) * 0.5f, 0f), new Vector3(0.5f, (h - 0.16f) * 0.5f, half.Y - 0.08f), DarkWalnut);
                for (int drawer = 0; drawer < 3; drawer++)
                {
                    float y = 0.3f + drawer * ((h - 0.5f) / 3f) + 0.2f;
                    foreach (float face in new[] { -1f, 1f })
                    {
                        b.Box(new Vector3(x, y, face * (half.Y - 0.07f)), new Vector3(0.42f, 0.2f, 0.012f), Walnut);
                        b.Box(new Vector3(x, y, face * (half.Y - 0.05f)), new Vector3(0.06f, 0.025f, 0.02f), Gilt);
                    }
                }
            }
            // The book, open on its slope.
            Vector3 lo = new Vector3(0f, h + 0.06f, 0.42f), hi = new Vector3(0f, h + 0.42f, -0.32f);
            Vector3 v = (hi - lo).Normalized(), n = Vector3.Cross(Vector3.UnitX, v).Normalized();
            if (n.Y < 0f) n = -n;
            Hall.Board(b, lo - n * 0.03f, hi - n * 0.03f, Vector3.UnitX, 0.78f, 0.03f, Walnut);
            b.Box(new Vector3(0f, h + 0.2f, -0.3f), new Vector3(0.7f, 0.2f, 0.04f), Walnut);
            Hall.Board(b, lo + v * 0.06f + n * 0.02f, hi - v * 0.06f + n * 0.02f, Vector3.UnitX, 0.7f, 0.02f, Hall.Leather(34, 46, 86));
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 side = Vector3.UnitX * (s * 0.35f);
                Hall.Board(b, lo + v * 0.1f + n * 0.055f + side, hi - v * 0.1f + n * 0.055f + side, Vector3.UnitX, 0.31f, 0.016f, Hall.Flat(222, 212, 184));
                for (int line = 0; line < 6; line++)
                {
                    Vector3 at = lo + v * (0.2f + line * 0.1f) + n * 0.074f + side;
                    Hall.Board(b, at - Vector3.UnitX * 0.22f, at + Vector3.UnitX * (0.22f - (line % 3) * 0.05f), v, 0.012f, 0.002f, Hall.Flat(70, 60, 50));
                }
            }
            Hall.Board(b, lo + v * 0.02f + n * 0.076f + Vector3.UnitX * 0.2f, hi - v * 0.3f + n * 0.076f + Vector3.UnitX * 0.2f, Vector3.UnitX, 0.03f, 0.002f, Hall.Cloth(190, 132, 44));

            // What else is on it, each in its place.
            Vector3 ink = new Vector3(half.X - 0.42f, h + 0.012f, half.Y - 0.36f);
            b.Tube(ink, ink + Vector3.UnitY * 0.16f, 0.1f, 0.08f, 8, Silver);
            b.Beam(ink + Vector3.UnitY * 0.12f, ink + new Vector3(0.12f, 0.7f, -0.12f), 0.014f, Hall.Flat(224, 220, 208));
            for (int i = 0; i < 3; i++)
            {
                VertexMaterial cover = i == 1 ? Hall.Leather(184, 168, 132) : Hall.Leather(28, 76, 80);
                b.Box(new Vector3(-half.X + 0.5f, h + 0.07f + i * 0.13f, 0.28f), new Vector3(0.33f, 0.06f, 0.24f), cover);
            }
            b.Tube(new Vector3(-half.X + 0.2f, h + 0.09f, -0.45f), new Vector3(-half.X + 1.05f, h + 0.09f, -0.5f), 0.075f, 0.075f, 6, Hall.Flat(206, 192, 156));
            b.Tube(new Vector3(-half.X + 0.58f, h + 0.09f, -0.474f), new Vector3(-half.X + 0.66f, h + 0.09f, -0.478f), 0.08f, 0.08f, 6, Hall.Cloth(28, 96, 98), capped: false);
            Vector3 stick = new Vector3(half.X - 0.4f, h + 0.012f, -half.Y + 0.36f);
            Hall.Turned(b, stick, Silver, 8, (0f, 0.17f), (0.05f, 0.15f), (0.09f, 0.05f), (0.42f, 0.04f), (0.48f, 0.11f));
            Hall.Candle(b, stick + Vector3.UnitY * 0.48f, 0.4f, 0.06f, lights);
            lights.Add(AssetLight.Candles(new Vector3(0f, h + 2.3f, 0.4f)) with { Intensity = 22f, Radius = 11f, Shadow = false });

            // The chair, behind it.
            Hall.At(b, new Vector3(0.1f, 0f, half.Y + 0.75f), 0.2f, () =>
            {
                foreach (float x in new[] { -0.48f, 0.48f })
                {
                    foreach (float z in new[] { -0.42f, 0.42f }) Hall.Leg(b, new Vector3(x, 0f, z), 1.04f, 0.07f, Walnut);
                    b.Box(new Vector3(x, 1.95f, 0.44f), new Vector3(0.07f, 0.9f, 0.07f), Walnut);
                }
                b.Box(new Vector3(0f, 1.1f, 0f), new Vector3(0.58f, 0.06f, 0.52f), Walnut);
                b.Box(new Vector3(0f, 1.2f, 0f), new Vector3(0.5f, 0.05f, 0.44f), Hall.Cloth(28, 96, 98));
                b.Box(new Vector3(0f, 2.2f, 0.44f), new Vector3(0.5f, 0.5f, 0.045f), Hall.Cloth(28, 96, 98));
                b.Box(new Vector3(0f, 2.82f, 0.44f), new Vector3(0.6f, 0.1f, 0.07f), Walnut);
                b.Box(new Vector3(0f, 2.96f, 0.44f), new Vector3(0.52f, 0.03f, 0.075f), Gilt);
            });
        }

        /// <summary>
        /// A tall stand of candles: a round foot of marble, a stem of gold turned in beads, a wide dish, and seven candles on it, the middle one tallest. Its light is
        /// set over the flames, clear of the wax.
        /// </summary>
        private static void Torchere(MeshBuilder b, float r, float h, List<AssetLight> lights)
        {
            b.Tube(Vector3.Zero, Vector3.UnitY * 0.14f, r, r * 0.94f, 16, Teal);
            b.Tube(Vector3.UnitY * 0.14f, Vector3.UnitY * 0.2f, r * 0.8f, r * 0.74f, 16, White);
            float dish = h - 1.05f;
            Hall.Turned(b, Vector3.UnitY * 0.2f, Gilt, 10, (0f, 0.3f), (0.14f, 0.2f), (0.3f, 0.09f), (0.62f, 0.16f), (0.78f, 0.07f), (dish * 0.5f, 0.06f), (dish * 0.5f + 0.14f, 0.13f), (dish * 0.5f + 0.28f, 0.06f),
                (dish - 0.62f, 0.055f), (dish - 0.4f, 0.14f), (dish - 0.26f, 0.08f), (dish - 0.2f, 0.5f));
            b.Tube(Vector3.UnitY * dish, Vector3.UnitY * (dish + 0.07f), 0.5f, 0.56f, 16, Gilt);
            for (int i = 0; i < 7; i++)
            {
                float a = MathF.Tau * i / 6f;
                Vector3 at = i == 6 ? new Vector3(0f, dish + 0.07f, 0f) : new Vector3(MathF.Cos(a) * 0.38f, dish + 0.07f, MathF.Sin(a) * 0.38f);
                Hall.Candle(b, at, i == 6 ? 0.78f : 0.5f, 0.06f, lights);
            }
            lights.Add(AssetLight.CandleStand(new Vector3(0f, h + 1.5f, 0f)) with { Intensity = 60f, Radius = 17f, Shadow = false });
        }

        /// <summary>A bench of white marble on two scrolled feet, a cushion of teal along it.</summary>
        private static void Bench(MeshBuilder b, Vector2 half, float h)
        {
            foreach (float x in new[] { -half.X + 0.42f, half.X - 0.42f })
            {
                b.Box(new Vector3(x, 0.09f, 0f), new Vector3(0.26f, 0.09f, half.Y - 0.02f), Teal);
                b.Box(new Vector3(x, (0.18f + h - 0.3f) * 0.5f, 0f), new Vector3(0.18f, (h - 0.48f) * 0.5f, half.Y - 0.1f), White);
            }
            b.Box(new Vector3(0f, h - 0.21f, 0f), new Vector3(half.X, 0.09f, half.Y), White);
            b.Box(new Vector3(0f, h - 0.315f, 0f), new Vector3(half.X - 0.04f, 0.015f, half.Y - 0.04f), Gilt);
            b.Box(new Vector3(0f, h - 0.07f, 0f), new Vector3(half.X - 0.12f, 0.07f, half.Y - 0.1f), Hall.Cloth(28, 96, 98));
            foreach (float x in new[] { -half.X + 0.14f, half.X - 0.14f }) b.Box(new Vector3(x, h - 0.07f, 0f), new Vector3(0.02f, 0.075f, half.Y - 0.09f), Hall.Cloth(190, 132, 44));
        }

        /// <summary>
        /// The globe: the sea in the Haven's teal and the lands pale on it, turning in a ring of gold tilted as the world is, in a cradle of three legs on a round of marble.
        /// </summary>
        private static void Globe(MeshBuilder b, float r, float h)
        {
            b.Tube(Vector3.Zero, Vector3.UnitY * 0.2f, r, r * 0.96f, 20, Teal);
            b.Tube(Vector3.UnitY * 0.2f, Vector3.UnitY * 0.28f, r * 0.84f, r * 0.8f, 20, White);
            float radius = r * 0.86f;
            Vector3 c = new Vector3(0f, h - radius - 0.16f, 0f);
            for (int i = 0; i < 3; i++)
            {
                float a = MathF.Tau * i / 3f + 0.5f;
                Vector3 out_ = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a));
                Hall.Bent(b, Walnut, new Vector3(-out_.Z, 0f, out_.X), 0.06f, 0.05f, out_ * (r * 0.7f) + Vector3.UnitY * 0.28f, out_ * (r * 0.42f) + Vector3.UnitY * (c.Y - radius - 0.5f), out_ * (radius + 0.12f) + Vector3.UnitY * (c.Y - 0.02f));
            }
            // The ring it sits in, level, and the one it turns in, tilted.
            const int Segments = 24;
            for (int i = 0; i < Segments; i++)
            {
                float a0 = MathF.Tau * i / Segments, a1 = MathF.Tau * (i + 1) / Segments;
                Vector3 p0 = new Vector3(MathF.Cos(a0), 0f, MathF.Sin(a0)), p1 = new Vector3(MathF.Cos(a1), 0f, MathF.Sin(a1));
                Hall.Board(b, c + p0 * (radius + 0.13f), c + p1 * (radius + 0.13f), Vector3.UnitY, 0.035f, 0.07f, Gilt);
                Hall.Board(b, c + TiltedRing(a0) * (radius + 0.06f), c + TiltedRing(a1) * (radius + 0.06f), Vector3.UnitZ, 0.03f, 0.025f, Gilt);
            }
            HallFurniture.Sphere(b, c, radius, 18, 12, Hall.Cloth(30, 92, 100));
            var rng = new WorldRandom(0x610B);
            VertexMaterial land = Hall.Cloth(206, 192, 156);
            for (int i = 0; i < 16; i++)
            {
                Vector3 d = new Vector3(rng.Range(-1f, 1f), rng.Range(-0.75f, 0.9f), rng.Range(-1f, 1f));
                if (d.LengthSquared < 0.1f) continue;
                d = d.Normalized();
                b.Box(c + d * (radius - 0.03f), new Vector3(rng.Range(0.1f, 0.24f), rng.Range(0.07f, 0.16f), rng.Range(0.1f, 0.24f)), land);
            }
            Vector3 pole = TiltedRing(0f);
            b.Tube(c + pole * (radius + 0.02f), c + pole * (radius + 0.22f), 0.03f, 0.05f, 6, Gilt);
        }

        // A point of the ring a globe turns in: a circle through its poles, in the plane that holds its tilted axis.
        private static Vector3 TiltedRing(float a)
        {
            const float Tilt = 0.41f;
            Vector3 axis = new Vector3(MathF.Sin(Tilt), MathF.Cos(Tilt), 0f), across = new Vector3(MathF.Cos(Tilt), -MathF.Sin(Tilt), 0f);
            return axis * MathF.Cos(a) + across * MathF.Sin(a);
        }
    }
}

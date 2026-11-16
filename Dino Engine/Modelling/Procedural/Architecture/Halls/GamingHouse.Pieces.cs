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
    /// What stands in the Gaming House, each piece built in its own frame to the numbers of its <see cref="HallPiece"/>, as <see cref="HallFurniture"/>'s are: its middle on the floor at
    /// the origin, its front towards +z. The camera looks down from the south, so a piece is seen from its front and from above: what's on a table matters more than what's under it,
    /// and a tall back belongs on the far side of whatever it's the back of. A person is 4.7 m tall, a table 1.95 m and a seat 1.1 m.
    /// </summary>
    internal static partial class GamingHouse
    {
        private const float FloorTop = Hall.FloorTop;

        public static void Piece(MeshBuilder b, in HallPiece piece, HallKit kit, List<AssetLight> lights, WorldRandom random)
        {
            Vector2 half = piece.Half;
            float r = half.X;
            float h = piece.Height;
            switch (piece.Kind)
            {
                case PieceKind.GamingTable: GreatTable(b, half, h, lights, random); break;
                case PieceKind.Chair: Chair(b, r, h, piece.Variant); break;
                case PieceKind.Bar: Bar(b, half, h, lights, random); break;
                case PieceKind.BarShelves: BackBar(b, half, h, random); break;
                case PieceKind.Fireplace: Inglenook(b, half, h, lights, random); break;
                case PieceKind.Settle: Settle(b, half, h); break;
                case PieceKind.Armchair: Armchair(b, r, h); break;
                case PieceKind.RoundTable:
                    if (piece.Variant == 2) DraughtsBarrel(b, r, h, lights);
                    else BoothTable(b, r, h, piece.Variant, lights, random);
                    break;
                case PieceKind.Stage: Stage(b, half, h, lights, random); break;
                case PieceKind.Rug:
                    if (piece.Variant == 3) Runner(b, half);
                    else GreatRug(b, half);
                    break;
                case PieceKind.HideRug: HideRug(b, half); break;
                case PieceKind.BarnPost: Post(b, piece, lights); break;
                case PieceKind.StallWall: StallWall(b, half, piece.Variant, lights, random); break;
                case PieceKind.BoothBench: BoothBench(b, half, piece.Variant); break;
                case PieceKind.Ladder: Ladder(b, half, h); break;
                case PieceKind.BladeBarrel: BladeBarrel(b, r); break;
                case PieceKind.Easel: Easel(b, h); break;
                case PieceKind.HayBales: HayStack(b, half, random); break;
                case PieceKind.LoftStore: break;    // The tuns under the loft, which are built with the loft (Loft).
                default: HallFurniture.Build(b, piece, kit, lights, random); break;
            }
        }

        // ---- The great table.

        /// <summary>
        /// The great table, where gold is played for: an oval of green cloth sunk in a padded rail, on two trestles, laid for a game with one place left empty at the front, where a
        /// hero stands. Places for cards are painted on the cloth, the bank is in its tray at the dealer's hand, and candles burn at either end.
        /// </summary>
        private static void GreatTable(MeshBuilder b, Vector2 half, float h, List<AssetLight> lights, WorldRandom random)
        {
            VertexMaterial wood = Hall.Timber(120, 80, 50);
            VertexMaterial dark = Walnut;
            VertexMaterial felt = Hall.Cloth(30, 92, 56);
            VertexMaterial rail = Hall.Leather(106, 64, 38);
            VertexMaterial paint = Hall.Flat(206, 190, 138);
            float top = h;
            float r = half.Y;
            float straight = half.X - r;
            float bed = top - 0.07f;

            Stadium(b, straight, r - 0.04f, top - 0.3f, bed - 0.004f, wood, 20, top: false);
            Stadium(b, straight, r - 0.3f, bed - 0.01f, bed, felt, 20, sides: false);
            Ring(b, straight, r, r - 0.32f, top - 0.16f, top, rail, 20);
            Ring(b, straight, r + 0.05f, r - 0.04f, top - 0.36f, top - 0.15f, dark, 20, inner: false);
            Stadium(b, straight - 0.1f, r - 0.5f, top - 0.82f, top - 0.3f, dark, 16, top: false);

            // Two trestles, each a foot along the floor, a turned column and a bearer, with a stretcher wedged through them.
            foreach (float s in new[] { -1f, 1f })
            {
                float x = s * (straight + 0.5f);
                b.Box(new Vector3(x, 0.14f, 0f), new Vector3(0.26f, 0.14f, 1.25f), dark);
                foreach (float z in new[] { -1.1f, 1.1f }) b.Box(new Vector3(x, 0.05f, z), new Vector3(0.3f, 0.05f, 0.18f), dark);
                float column = top - 0.82f - 0.28f;
                Hall.Turned(b, new Vector3(x, 0.28f, 0f), dark, 8, (0f, 0.3f), (0.1f, 0.34f), (0.2f, 0.2f), (column * 0.5f, 0.27f), (column * 0.62f, 0.34f), (column * 0.74f, 0.2f), (column, 0.26f));
                b.Box(new Vector3(x, top - 0.74f, 0f), new Vector3(0.2f, 0.08f, 1.2f), dark);
                b.Box(new Vector3(x + s * 0.36f, 0.5f, 0f), new Vector3(0.035f, 0.14f, 0.05f), Pale);
            }
            Hall.Board(b, new Vector3(-straight - 0.9f, 0.5f, 0f), new Vector3(straight + 0.9f, 0.5f, 0f), Vector3.UnitY, 0.12f, 0.07f, dark);

            // What's painted on the cloth: a line round it, a place for each player's cards, and the house's mark in the middle.
            float y = bed + 0.006f;
            Ring(b, straight, r - 0.52f, r - 0.56f, y - 0.004f, y, paint, 20, inner: false, outer: false);
            Vector2[] places = { new(-2.15f, 0.62f), new(-1.1f, 1.08f), new(0f, 1.2f), new(1.1f, 1.08f), new(2.15f, 0.62f) };
            foreach (Vector2 place in places) Outline(b, new Vector3(place.X, y, place.Y), -place.X * 0.22f, 0.23f, 0.3f, 0.016f, paint);
            Vector3 mark = new Vector3(0f, y, -0.05f);
            Hall.Tri(b, mark - Vector3.UnitX * 0.62f, mark + Vector3.UnitZ * 0.3f, mark + Vector3.UnitX * 0.62f, Vector3.UnitY, Hall.Flat(190, 132, 44));
            Hall.Tri(b, mark - Vector3.UnitX * 0.62f, mark - Vector3.UnitZ * 0.3f, mark + Vector3.UnitX * 0.62f, Vector3.UnitY, Hall.Flat(190, 132, 44));
            Vector3 heart = mark + Vector3.UnitY * 0.005f;
            Hall.Tri(b, heart - Vector3.UnitX * 0.34f, heart + Vector3.UnitZ * 0.16f, heart + Vector3.UnitX * 0.34f, Vector3.UnitY, Hall.Flat(30, 60, 100));
            Hall.Tri(b, heart - Vector3.UnitX * 0.34f, heart - Vector3.UnitZ * 0.16f, heart + Vector3.UnitX * 0.34f, Vector3.UnitY, Hall.Flat(30, 60, 100));

            // A hand in every place but the one at the front, which is waiting, and what each has staked pushed out before it.
            for (int i = 0; i < places.Length; i++)
            {
                if (i == 2) continue;
                Vector2 place = places[i];
                float turn = -place.X * 0.22f;
                Card(b, new Vector3(place.X - 0.07f, y + 0.008f, place.Y), turn + random.Range(-0.15f, 0.15f), true, i);
                Card(b, new Vector3(place.X + 0.09f, y + 0.02f, place.Y + 0.03f), turn + random.Range(-0.2f, 0.2f), true, i + 1);
                Vector2 inward = -place.Normalized();
                Coins(b, new Vector3(place.X + inward.X * 0.6f, y, place.Y + inward.Y * 0.62f), random.Int(3, 9), random);
                if (random.Chance(0.6f)) Coins(b, new Vector3(place.X + inward.X * 0.6f + 0.26f, y, place.Y + inward.Y * 0.62f + 0.08f), random.Int(2, 6), random);
            }
            Card(b, new Vector3(-0.06f, y + 0.008f, 1.2f), 0.1f, false);

            // The dealer's side: their own hand, the deck, what's been played, the bank in its tray, the rake, and a glass that runs out.
            Card(b, new Vector3(-0.16f, y + 0.008f, -0.95f), 0.05f, true, 1);
            Card(b, new Vector3(0.16f, y + 0.02f, -0.93f), -0.08f, false);
            b.Box(new Vector3(-0.85f, y + 0.07f, -1.1f), new Vector3(0.15f, 0.07f, 0.21f), Hall.Flat(30, 84, 88));
            b.Box(new Vector3(-0.85f, y + 0.142f, -1.1f), new Vector3(0.11f, 0.003f, 0.17f), Hall.Flat(204, 184, 120));
            for (int i = 0; i < 5; i++) Card(b, new Vector3(0.78f + random.Range(-0.08f, 0.08f), y + 0.008f + i * 0.012f, -1.08f + random.Range(-0.06f, 0.06f)), random.Range(-0.6f, 0.6f), true, i);

            Vector3 tray = new Vector3(1.75f, y, -0.92f);
            Hall.At(b, tray, -0.22f, () =>
            {
                b.Box(new Vector3(0f, 0.03f, 0f), new Vector3(0.56f, 0.03f, 0.34f), dark);
                foreach (float z in new[] { -0.34f, 0.34f }) b.Box(new Vector3(0f, 0.08f, z), new Vector3(0.58f, 0.08f, 0.025f), dark);
                foreach (float x in new[] { -0.56f, 0.56f }) b.Box(new Vector3(x, 0.08f, 0f), new Vector3(0.025f, 0.08f, 0.34f), dark);
                for (int row = 0; row < 2; row++)
                {
                    for (int column = 0; column < 4; column++) Coins(b, new Vector3(-0.39f + column * 0.26f, 0.06f, -0.15f + row * 0.3f), random.Int(4, 12), random);
                }
            });

            Vector3 rakeFrom = new Vector3(0.55f, y + 0.03f, -0.5f), rakeTo = new Vector3(2.05f, y + 0.03f, 0.16f);
            b.Tube(rakeFrom, rakeTo, 0.022f, 0.022f, 5, Hall.Timber(160, 120, 78));
            Vector3 rakeAcross = Vector3.Cross(rakeTo - rakeFrom, Vector3.UnitY).Normalized();
            Hall.Board(b, rakeFrom - rakeAcross * 0.2f, rakeFrom + rakeAcross * 0.2f, Vector3.UnitY, 0.045f, 0.012f, Hall.Timber(160, 120, 78));

            Vector3 glass = new Vector3(-1.5f, y, -1.05f);
            foreach (float s in new[] { 0f, 0.3f }) b.Tube(glass + Vector3.UnitY * s, glass + Vector3.UnitY * (s + 0.025f), 0.1f, 0.1f, 8, dark);
            b.Tube(glass + Vector3.UnitY * 0.025f, glass + Vector3.UnitY * 0.16f, 0.075f, 0.015f, 7, Hall.Mirror(190, 176, 140), capped: false);
            b.Tube(glass + Vector3.UnitY * 0.16f, glass + Vector3.UnitY * 0.3f, 0.015f, 0.075f, 7, Hall.Mirror(190, 176, 140), capped: false);
            for (int i = 0; i < 3; i++)
            {
                float a = MathF.Tau * i / 3f;
                b.Beam(glass + new Vector3(MathF.Cos(a) * 0.085f, 0.02f, MathF.Sin(a) * 0.085f), glass + new Vector3(MathF.Cos(a) * 0.085f, 0.31f, MathF.Sin(a) * 0.085f), 0.01f, dark);
            }

            // Dice thrown from a cup that's still lying where it was put down.
            DiceCup(b, new Vector3(-1.95f, y, -0.38f), new Vector3(1f, 0f, 0.5f));
            foreach ((float x, float z) in new[] { (-1.25f, -0.12f), (-0.92f, -0.42f), (-0.7f, 0.02f) }) Die(b, new Vector3(x, y + 0.11f, z), 0.11f, random.Range(0f, MathF.Tau));

            // On the rail: a tankard someone's put down, and a knife stood in the leather to say a thing was meant.
            Vector3 OnRail(float end, float angle) => new Vector3(end * straight + MathF.Cos(angle) * (r - 0.16f), top, MathF.Sin(angle) * (r - 0.16f));
            Mug(b, OnRail(-1f, MathF.PI + 0.5f), Hall.Pewter, 2.2f, 1.15f);
            Mug(b, OnRail(1f, -0.9f), Hall.Pewter, -0.6f, 1.15f);
            Vector3 knife = OnRail(1f, 0.82f);
            Vector3 hilt = knife + new Vector3(0.1f, 0.34f, -0.06f);
            Hall.Board(b, knife - Vector3.UnitY * 0.04f, hilt, Vector3.UnitX, 0.035f, 0.008f, Hall.Silver);
            Hall.Board(b, hilt - Vector3.UnitX * 0.1f, hilt + Vector3.UnitX * 0.1f, Vector3.UnitY, 0.02f, 0.02f, Iron);
            b.Tube(hilt, hilt + (hilt - knife).Normalized() * 0.24f, 0.03f, 0.035f, 6, Hall.Leather(70, 42, 26));

            // A branch of three candles at either end.
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 foot = new Vector3(s * (straight + r * 0.5f), bed, 0f);
                Candelabrum(b, foot, lights);
            }
        }

        // Three candles on a pewter stick: one on its stem and one on an arm either side.
        private static void Candelabrum(MeshBuilder b, Vector3 foot, List<AssetLight> lights)
        {
            VertexMaterial metal = Hall.Pewter;
            Hall.Turned(b, foot, metal, 8, (0f, 0.2f), (0.05f, 0.18f), (0.09f, 0.06f), (0.3f, 0.045f), (0.36f, 0.09f), (0.42f, 0.045f), (0.6f, 0.04f), (0.64f, 0.1f), (0.67f, 0.09f));
            Hall.Candle(b, foot + Vector3.UnitY * 0.67f, 0.36f, 0.055f, lights);
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 cup = foot + new Vector3(0f, 0.56f, s * 0.3f);
                b.Beam(foot + Vector3.UnitY * 0.4f, foot + new Vector3(0f, 0.44f, s * 0.2f), 0.02f, metal);
                b.Beam(foot + new Vector3(0f, 0.44f, s * 0.2f), cup, 0.02f, metal);
                b.Tube(cup, cup + Vector3.UnitY * 0.04f, 0.07f, 0.09f, 8, metal);
                Hall.Candle(b, cup + Vector3.UnitY * 0.04f, 0.3f, 0.05f, lights);
            }
        }

        // The outline of a stadium, two half rounds joined by straights, going round.
        private static List<Vector3> StadiumOutline(float straight, float r, int segments)
        {
            var outline = new List<Vector3>();
            for (int i = 0; i <= segments; i++)
            {
                float a = -MathF.PI * 0.5f + MathF.PI * i / segments;
                outline.Add(new Vector3(straight + MathF.Cos(a) * r, 0f, MathF.Sin(a) * r));
            }
            for (int i = 0; i <= segments; i++)
            {
                float a = MathF.PI * 0.5f + MathF.PI * i / segments;
                outline.Add(new Vector3(-straight + MathF.Cos(a) * r, 0f, MathF.Sin(a) * r));
            }
            return outline;
        }

        // Which way is out of a stadium at a point of it.
        private static Vector3 StadiumOut(Vector3 p, float straight)
        {
            Vector3 from = new Vector3(Math.Clamp(p.X, -straight, straight), 0f, 0f);
            Vector3 way = new Vector3(p.X, 0f, p.Z) - from;
            return way.LengthSquared > 1e-8f ? way.Normalized() : Vector3.UnitZ;
        }

        // A flat oval from y0 to y1: its top, laid out as one face so its grain runs straight across it, and its side.
        private static void Stadium(MeshBuilder b, float straight, float r, float y0, float y1, VertexMaterial material, int segments, bool top = true, bool sides = true)
        {
            List<Vector3> outline = StadiumOutline(straight, r, segments);
            Vector3 up = Vector3.UnitY * y1;
            Vector3 origin = new Vector3(-straight - r, y1, -r);
            Vector4 bounds = new Vector4(0f, (straight + r) * 2f, -r * 2f, 0f);
            for (int i = 0; i < outline.Count; i++)
            {
                Vector3 a = outline[i], c = outline[(i + 1) % outline.Count];
                if (top) b.PlaneTriangle(up, c + up, a + up, material, origin, Vector3.UnitX, -Vector3.UnitZ, bounds);
                if (sides) Hall.Face(b, a + Vector3.UnitY * y0, c + Vector3.UnitY * y0, c + up, a + up, StadiumOut((a + c) * 0.5f, straight), material);
            }
        }

        // A band round a stadium, from its inner edge to its outer and from y0 to y1: its top, and its walls outside and in.
        private static void Ring(MeshBuilder b, float straight, float outerRadius, float innerRadius, float y0, float y1, VertexMaterial material, int segments, bool inner = true, bool outer = true)
        {
            List<Vector3> far = StadiumOutline(straight, outerRadius, segments);
            List<Vector3> near = StadiumOutline(straight, innerRadius, segments);
            Vector3 up = Vector3.UnitY * y1, down = Vector3.UnitY * y0;
            for (int i = 0; i < far.Count; i++)
            {
                int next = (i + 1) % far.Count;
                Hall.Face(b, near[i] + up, far[i] + up, far[next] + up, near[next] + up, Vector3.UnitY, material);
                Vector3 out_ = StadiumOut((far[i] + far[next]) * 0.5f, straight);
                if (outer) Hall.Face(b, far[i] + down, far[next] + down, far[next] + up, far[i] + up, out_, material);
                if (inner) Hall.Face(b, near[i] + down, near[next] + down, near[next] + up, near[i] + up, -out_, material);
            }
        }

        // A rectangle drawn in line on something flat: four strips.
        private static void Outline(MeshBuilder b, Vector3 at, float yaw, float halfX, float halfZ, float width, VertexMaterial material)
        {
            Hall.At(b, at, yaw, () =>
            {
                foreach (float s in new[] { -1f, 1f })
                {
                    b.Box(new Vector3(0f, 0f, s * halfZ), new Vector3(halfX + width, 0.003f, width), material);
                    b.Box(new Vector3(s * halfX, 0f, 0f), new Vector3(width, 0.003f, halfZ - width), material);
                }
            });
        }

        // ---- Seats.

        // A farmhouse chair: square legs a little splayed, a board seat with a cushion tied on, and a back of three slats.
        private static void Chair(MeshBuilder b, float r, float h, int variant)
        {
            VertexMaterial wood = variant % 2 == 0 ? Oak : Hall.Timber(124, 88, 58);
            float seat = 1.1f;
            float w = r * 0.72f;
            foreach (float x in new[] { -1f, 1f })
            {
                Hall.Board(b, new Vector3(x * (w + 0.02f), 0f, w + 0.04f), new Vector3(x * (w - 0.06f), seat, w - 0.06f), Vector3.UnitX, 0.055f, 0.055f, wood);
                Hall.Board(b, new Vector3(x * (w + 0.02f), 0f, -w - 0.06f), new Vector3(x * (w - 0.06f), seat, -w + 0.06f), Vector3.UnitX, 0.055f, 0.055f, wood);
                Hall.Board(b, new Vector3(x * (w - 0.06f), seat, -w + 0.06f), new Vector3(x * (w - 0.04f), h, -w - 0.08f), Vector3.UnitX, 0.055f, 0.05f, wood);
                Hall.Board(b, new Vector3(x * (w - 0.02f), 0.42f, w), new Vector3(x * (w - 0.02f), 0.42f, -w), Vector3.UnitY, 0.04f, 0.03f, wood);
            }
            Hall.Board(b, new Vector3(-w, 0.5f, w), new Vector3(w, 0.5f, w), Vector3.UnitY, 0.04f, 0.03f, wood);
            Hall.Board(b, new Vector3(-w - 0.03f, seat + 0.04f, 0f), new Vector3(w + 0.03f, seat + 0.04f, 0f), Vector3.UnitZ, w + 0.03f, 0.045f, wood);
            b.Box(new Vector3(0f, seat + 0.14f, 0.03f), new Vector3(w - 0.06f, 0.06f, w - 0.08f), Dyed(Festive[variant % 2 == 0 ? 0 : 1]));
            for (int i = 0; i < 3; i++)
            {
                float t = (i + 1) / 3.6f;
                float y = seat + (h - seat) * t + 0.1f;
                float z = -w + 0.06f - 0.14f * t;
                Hall.Board(b, new Vector3(-w + 0.04f, y, z), new Vector3(w - 0.04f, y, z), Vector3.UnitY, i == 2 ? 0.13f : 0.08f, 0.025f, wood);
            }
        }

        // A settle: a bench with a boarded back as tall as whoever sits on it and a wing at either end, to keep a draught off by a fire.
        private static void Settle(MeshBuilder b, Vector2 half, float h)
        {
            VertexMaterial wood = Hall.Timber(126, 88, 56);
            var random = new WorldRandom(0x5E77);
            float seat = 1.1f;
            b.Box(new Vector3(0f, 0.5f, 0.04f), new Vector3(half.X - 0.1f, 0.5f, half.Y - 0.12f), Walnut);
            for (int i = 0; i < 3; i++) b.Box(new Vector3((i - 1) * (half.X * 0.62f), 0.5f, half.Y - 0.07f), new Vector3(half.X * 0.26f, 0.32f, 0.02f), wood);
            Hall.Board(b, new Vector3(-half.X + 0.06f, seat - 0.04f, 0.08f), new Vector3(half.X - 0.06f, seat - 0.04f, 0.08f), Vector3.UnitZ, half.Y - 0.02f, 0.05f, wood);
            int boards = 9;
            float width = (half.X * 2f - 0.2f) / boards;
            for (int i = 0; i < boards; i++)
            {
                float x = -half.X + 0.1f + width * (i + 0.5f);
                Hall.Board(b, new Vector3(x, seat, -half.Y + 0.06f), new Vector3(x, h - 0.16f, -half.Y + 0.06f), Vector3.UnitX, width * 0.5f - 0.008f, 0.04f, Weathered(random));
            }
            Hall.Board(b, new Vector3(-half.X, h - 0.1f, -half.Y + 0.08f), new Vector3(half.X, h - 0.1f, -half.Y + 0.08f), Vector3.UnitY, 0.1f, 0.07f, Walnut);
            Hall.Board(b, new Vector3(-half.X, seat + 0.9f, -half.Y + 0.1f), new Vector3(half.X, seat + 0.9f, -half.Y + 0.1f), Vector3.UnitY, 0.06f, 0.02f, Walnut);
            foreach (float s in new[] { -1f, 1f })
            {
                // An end: full depth to the arm, then cut back in two steps up to the top.
                float x = s * (half.X - 0.05f);
                b.Box(new Vector3(x, 0.85f, 0.02f), new Vector3(0.05f, 0.85f, half.Y - 0.02f), wood);
                b.Box(new Vector3(x, 2.1f, -half.Y * 0.3f), new Vector3(0.05f, 0.4f, half.Y * 0.68f), wood);
                b.Box(new Vector3(x, 2.9f, -half.Y * 0.55f), new Vector3(0.05f, 0.4f, half.Y * 0.42f), wood);
                b.Tube(new Vector3(x, 1.72f, half.Y - 0.04f), new Vector3(x, 1.72f, -half.Y * 0.2f), 0.07f, 0.07f, 6, Walnut);
            }
            // Cushions, a blanket thrown over the back, and what someone was drinking.
            Vector3[] cushions = { Teal, Saffron, Green };
            for (int i = 0; i < 3; i++) b.Box(new Vector3((i - 1) * 1.12f + (i == 1 ? 0.06f : 0f), seat + 0.1f, 0.1f), new Vector3(0.5f, 0.09f, 0.34f), Dyed(cushions[i]));
            b.Box(new Vector3(-0.95f, seat + 0.36f, -half.Y + 0.2f), new Vector3(0.4f, 0.26f, 0.1f), Hall.Quilted(206, 196, 168));
            b.Box(new Vector3(1.05f, h - 0.02f, -half.Y + 0.08f), new Vector3(0.42f, 0.03f, 0.13f), Dyed(Blossom));
            b.Box(new Vector3(1.05f, h - 0.45f, -half.Y + 0.14f), new Vector3(0.42f, 0.42f, 0.02f), Dyed(Blossom));
            b.Box(new Vector3(1.05f, h - 0.45f, -half.Y + 0.165f), new Vector3(0.42f, 0.05f, 0.012f), Dyed(Cream));
            Mug(b, new Vector3(half.X - 0.4f, seat + 0.01f, 0.3f), Hall.Pewter, 0.8f);
        }

        // A chair by the fire, in leather gone soft, with a blanket over its arm.
        private static void Armchair(MeshBuilder b, float r, float h)
        {
            VertexMaterial leather = Hall.Leather(120, 76, 46);
            VertexMaterial worn = Hall.Leather(140, 94, 58);
            float w = r * 0.74f;
            foreach (float x in new[] { -w + 0.12f, w - 0.12f })
            {
                foreach (float z in new[] { -w + 0.12f, w - 0.12f }) Hall.Turned(b, new Vector3(x, 0f, z), Walnut, 7, (0f, 0.07f), (0.1f, 0.1f), (0.3f, 0.07f), (0.36f, 0.1f));
            }
            b.Box(new Vector3(0f, 0.64f, 0f), new Vector3(w, 0.3f, w), leather, closeBottom: true);
            b.Box(new Vector3(0f, 1.02f, 0.08f), new Vector3(w - 0.2f, 0.1f, w - 0.16f), Hall.Quilted(52, 96, 62));
            foreach (float s in new[] { -1f, 1f })
            {
                float x = s * (w - 0.1f);
                b.Box(new Vector3(x, 1.2f, 0.02f), new Vector3(0.12f, 0.28f, w - 0.04f), leather);
                b.Tube(new Vector3(x, 1.5f, -w + 0.1f), new Vector3(x, 1.5f, w), 0.16f, 0.16f, 8, worn);
                b.Box(new Vector3(x, (h + 1.5f) * 0.5f - 0.2f, -w + 0.34f), new Vector3(0.1f, (h - 1.5f) * 0.5f - 0.1f, 0.22f), leather);
            }
            b.Box(new Vector3(0f, (h + 0.9f) * 0.5f, -w + 0.14f), new Vector3(w - 0.04f, (h - 0.9f) * 0.5f, 0.14f), leather);
            b.Tube(new Vector3(-w + 0.04f, h - 0.02f, -w + 0.14f), new Vector3(w - 0.04f, h - 0.02f, -w + 0.14f), 0.17f, 0.17f, 8, worn);
            // The blanket, over one arm.
            b.Box(new Vector3(w - 0.1f, 1.68f, 0.1f), new Vector3(0.2f, 0.03f, 0.36f), Dyed(Saffron));
            b.Box(new Vector3(w + 0.07f, 1.3f, 0.1f), new Vector3(0.02f, 0.4f, 0.36f), Dyed(Saffron));
            b.Box(new Vector3(w + 0.085f, 1.05f, 0.1f), new Vector3(0.012f, 0.05f, 0.36f), Dyed(Blue));
        }

        // A bench built into a booth: a boarded box to sit on with cushions along it, and a low back against what it stands by.
        private static void BoothBench(MeshBuilder b, Vector2 half, int variant)
        {
            var random = new WorldRandom(0xB007).Fork(variant);
            bool underWindow = variant >= 3;
            float seat = 1.08f;
            b.Box(new Vector3(0f, 0.5f, 0.02f), new Vector3(half.X - 0.04f, 0.5f, half.Y - 0.08f), Walnut);
            int fronts = 4;
            for (int i = 0; i < fronts; i++)
            {
                float x = -half.X + (half.X * 2f) * (i + 0.5f) / fronts;
                Hall.Board(b, new Vector3(x, 0.06f, half.Y - 0.05f), new Vector3(x, seat - 0.1f, half.Y - 0.05f), Vector3.UnitX, half.X / fronts - 0.02f, 0.025f, Weathered(random));
            }
            Hall.Board(b, new Vector3(-half.X, seat - 0.04f, 0.04f), new Vector3(half.X, seat - 0.04f, 0.04f), Vector3.UnitZ, half.Y, 0.05f, Oak);
            float back = underWindow ? 1.46f : 2.05f;
            for (float y = seat + 0.2f; y < back; y += 0.34f)
            {
                Hall.Board(b, new Vector3(-half.X, y, -half.Y + 0.05f), new Vector3(half.X, y, -half.Y + 0.05f), Vector3.UnitY, 0.15f, 0.03f, Weathered(random));
            }
            Vector3[] colours = { Festive[variant % Festive.Length], Festive[(variant + 2) % Festive.Length], Festive[(variant + 3) % Festive.Length] };
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * (half.X * 0.62f) + random.Range(-0.06f, 0.06f);
                Hall.At(b, new Vector3(x, seat + 0.01f, 0.06f + random.Range(-0.04f, 0.04f)), random.Range(-0.12f, 0.12f), () => b.Box(new Vector3(0f, 0.08f, 0f), new Vector3(0.46f, 0.08f, 0.34f), Dyed(colours[i])));
            }
        }

        // ---- The bar.

        /// <summary>
        /// The bar: a counter fronted in the barn's old boards under a top as thick as a beam, strapped with iron, a rail for feet along its foot, and everything a house pours
        /// set out along it, a keg on its cradle at one end and the book where debts are kept at the other.
        /// </summary>
        private static void Bar(MeshBuilder b, Vector2 half, float h, List<AssetLight> lights, WorldRandom random)
        {
            float top = h;
            b.Box(new Vector3(0f, 0.11f, 0f), new Vector3(half.X - 0.04f, 0.11f, half.Y - 0.1f), FrameDark);
            b.Box(new Vector3(0f, (top - 0.14f + 0.22f) * 0.5f, -0.04f), new Vector3(half.X - 0.05f, (top - 0.14f - 0.22f) * 0.5f, half.Y - 0.14f), Walnut);
            const float Board = 0.4f;
            int count = (int)MathF.Round((half.X * 2f - 0.04f) / Board);
            float width = (half.X * 2f - 0.04f) / count;
            for (int i = 0; i < count; i++)
            {
                float x = -half.X + 0.02f + width * (i + 0.5f);
                Hall.Board(b, new Vector3(x, 0.22f, half.Y - 0.07f), new Vector3(x, top - 0.14f, half.Y - 0.07f), Vector3.UnitX, width * 0.5f - 0.008f, 0.035f, Weathered(random));
            }
            foreach (float y in new[] { 0.62f, top - 0.52f }) Hall.Board(b, new Vector3(-half.X, y, half.Y - 0.028f), new Vector3(half.X, y, half.Y - 0.028f), Vector3.UnitY, 0.055f, 0.008f, Iron);
            foreach (float s in new[] { -1f, 1f }) b.Box(new Vector3(s * (half.X - 0.06f), (top - 0.14f) * 0.5f, 0f), new Vector3(0.08f, (top - 0.14f) * 0.5f, half.Y - 0.02f), FrameDark);
            Hall.Board(b, new Vector3(-half.X - 0.16f, top - 0.07f, 0.05f), new Vector3(half.X + 0.16f, top - 0.07f, 0.05f), Vector3.UnitZ, half.Y + 0.12f, 0.07f, Hall.Timber(152, 108, 66));
            Hall.Board(b, new Vector3(-half.X - 0.16f, top - 0.07f, half.Y + 0.175f), new Vector3(half.X + 0.16f, top - 0.07f, half.Y + 0.175f), Vector3.UnitY, 0.072f, 0.012f, Hall.Timber(184, 142, 94));
            b.Tube(new Vector3(-half.X, 0.4f, half.Y + 0.2f), new Vector3(half.X, 0.4f, half.Y + 0.2f), 0.05f, 0.05f, 6, Hall.Copper);
            for (float x = -half.X + 0.3f; x < half.X; x += 1.5f) b.Beam(new Vector3(x, 0.4f, half.Y - 0.04f), new Vector3(x, 0.4f, half.Y + 0.2f), 0.025f, Hall.Copper);

            // A keg on its cradle, tapped, with a tankard under the tap.
            float keg = -half.X + 1.15f;
            foreach (float z in new[] { -0.26f, 0.28f }) b.Box(new Vector3(keg, top + 0.07f, z), new Vector3(0.36f, 0.07f, 0.05f), Walnut);
            Cask(b, new Vector3(keg, top + 0.5f, -0.4f), new Vector3(keg, top + 0.5f, 0.42f), 0.42f);
            Spigot(b, new Vector3(keg, top + 0.3f, 0.42f), Vector3.UnitZ);
            Mug(b, new Vector3(keg + 0.02f, top, 0.56f), Hall.Pewter, 0.2f);

            // Tankards drying mouth down on a cloth, a jug and what it's poured into.
            b.Box(new Vector3(-2.3f, top + 0.008f, 0.08f), new Vector3(0.62f, 0.008f, 0.3f), Dyed(Cream));
            for (int i = 0; i < 4; i++)
            {
                Vector3 at = new Vector3(-2.72f + i * 0.28f, top + 0.016f, 0.02f + (i % 2) * 0.16f);
                b.Tube(at + Vector3.UnitY * 0.3f, at, 0.115f, 0.13f, 8, i == 2 ? Stoneware : Hall.Pewter);
            }
            Jug(b, new Vector3(-1.25f, top, -0.05f), Stoneware, 1.15f);
            Mug(b, new Vector3(-0.85f, top, 0.25f), Hall.Pewter, 0.4f);
            Mug(b, new Vector3(-0.55f, top, -0.02f), Stoneware, 2.4f);
            b.Box(new Vector3(0.1f, top + 0.012f, 0.3f), new Vector3(0.3f, 0.012f, 0.18f), Dyed(Teal));

            // A board of bread and cheese, cut into.
            Vector3 board = new Vector3(1.2f, top, 0.05f);
            b.Tube(board, board + Vector3.UnitY * 0.04f, 0.4f, 0.4f, 12, Pale);
            b.Tube(board + new Vector3(-0.1f, 0.04f, -0.05f), board + new Vector3(-0.1f, 0.2f, -0.05f), 0.24f, 0.23f, 12, Hall.Flat(216, 176, 84));
            b.Box(board + new Vector3(0.2f, 0.11f, 0.14f), new Vector3(0.1f, 0.07f, 0.16f), Hall.Flat(150, 100, 56));
            Hall.Board(b, board + new Vector3(0.02f, 0.05f, 0.3f), board + new Vector3(0.34f, 0.05f, 0.24f), Vector3.UnitY, 0.025f, 0.006f, Hall.Silver);

            // The book where debts are kept, open, with its pen, and coin taken in beside it.
            Vector3 ledger = new Vector3(2.45f, top, 0.06f);
            Hall.At(b, ledger, 0.12f, () =>
            {
                b.Box(new Vector3(0f, 0.025f, 0f), new Vector3(0.44f, 0.025f, 0.3f), Hall.Leather(66, 38, 24));
                foreach (float s in new[] { -1f, 1f }) b.Box(new Vector3(s * 0.21f, 0.058f, 0f), new Vector3(0.2f, 0.008f, 0.27f), Hall.Paper);
                for (int line = 0; line < 4; line++) b.Box(new Vector3(-0.21f, 0.068f, -0.18f + line * 0.11f), new Vector3(0.14f, 0.002f, 0.012f), Hall.Ink);
                for (int line = 0; line < 2; line++) b.Box(new Vector3(0.21f, 0.068f, -0.18f + line * 0.11f), new Vector3(0.14f, 0.002f, 0.012f), Hall.Ink);
            });
            b.Tube(ledger + new Vector3(0.62f, 0f, -0.2f), ledger + new Vector3(0.62f, 0.14f, -0.2f), 0.08f, 0.06f, 8, Hall.Flat(28, 28, 36));
            b.Beam(ledger + new Vector3(0.62f, 0.1f, -0.2f), ledger + new Vector3(0.74f, 0.6f, -0.3f), 0.012f, Hall.Flat(222, 216, 202));
            Spill(b, ledger + new Vector3(0.1f, 0f, 0.44f), 0.24f, 7, random);

            // Bottles at the far end, one with a candle in its neck, and a lantern at either end to pour by.
            for (int i = 0; i < 3; i++) Bottle(b, new Vector3(3.3f + i * 0.24f, top, -0.12f + (i % 2) * 0.14f), Glass[(i + 1) % Glass.Length], random.Range(0.5f, 0.66f));
            BottleCandle(b, new Vector3(3.45f, top, 0.32f), lights);
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 foot = new Vector3(s * (half.X - 0.12f), top, -0.06f);
                Lantern(b, foot + Vector3.UnitY * 0.36f, lights, 26f, 11f, shadow: false);
            }
        }

        /// <summary>
        /// What stands behind the bar against the wall: three tuns laid in its foot with their heads out, chalked with what's in them, and shelves over them of bottles, jugs and
        /// tankards, with the house's strongbox among them and kegs along its top.
        /// </summary>
        private static void BackBar(MeshBuilder b, Vector2 half, float h, WorldRandom random)
        {
            float front = half.Y;
            float body = h - 0.6f;      // Where its cornice is; what stands on it goes up to h.
            int boards = (int)MathF.Round(half.X * 2f / 0.45f);
            for (int i = 0; i < boards; i++)
            {
                float x = -half.X + half.X * 2f * (i + 0.5f) / boards;
                Hall.Board(b, new Vector3(x, 0f, -front + 0.04f), new Vector3(x, body, -front + 0.04f), Vector3.UnitX, half.X / boards - 0.008f, 0.04f, Weathered(random));
            }
            float bay = (half.X * 2f - 0.16f) / 3f;
            for (int i = 0; i <= 3; i++)
            {
                float x = -half.X + 0.08f + bay * i;
                b.Box(new Vector3(x, body * 0.5f, 0.02f), new Vector3(0.08f, body * 0.5f, front - 0.02f), FrameDark);
            }
            float[] shelves = { 2.86f, 3.92f };
            foreach (float y in shelves) Hall.Board(b, new Vector3(-half.X, y, 0.03f), new Vector3(half.X, y, 0.03f), Vector3.UnitZ, front - 0.01f, 0.05f, Walnut);
            Hall.Board(b, new Vector3(-half.X - 0.12f, body - 0.1f, 0.06f), new Vector3(half.X + 0.12f, body - 0.1f, 0.06f), Vector3.UnitZ, front + 0.1f, 0.1f, FrameDark);
            Hall.Board(b, new Vector3(-half.X - 0.06f, body - 0.3f, front + 0.02f), new Vector3(half.X + 0.06f, body - 0.3f, front + 0.02f), Vector3.UnitY, 0.1f, 0.03f, Walnut);

            for (int i = 0; i < 3; i++)
            {
                float x = -half.X + 0.08f + bay * (i + 0.5f);
                // A tun, its head out, on a cradle, tapped low, with strokes of chalk on its head to say what it is.
                foreach (float s in new[] { -1f, 1f }) b.Box(new Vector3(x + s * 0.7f, 0.25f, 0.06f), new Vector3(0.2f, 0.25f, front - 0.06f), FrameDark);
                Cask(b, new Vector3(x, 1.55f, -front + 0.1f), new Vector3(x, 1.55f, front + 0.06f), 1.14f);
                Spigot(b, new Vector3(x, 0.92f, front + 0.07f), Vector3.UnitZ);
                for (int stroke = 0; stroke <= i; stroke++) b.Box(new Vector3(x - 0.1f * i + stroke * 0.2f, 1.86f, front + 0.078f), new Vector3(0.022f, 0.2f, 0.004f), Chalk);
                b.Box(new Vector3(x, 1.5f, front + 0.078f), new Vector3(0.3f, 0.02f, 0.004f), Chalk);

                // The shelves: whatever comes to hand, no two bays alike.
                for (int shelf = 0; shelf < shelves.Length; shelf++)
                {
                    float y = shelves[shelf] + 0.05f;
                    if (i == 1 && shelf == 0)
                    {
                        Strongbox(b, new Vector3(x - 0.5f, y, 0.02f));
                        Coins(b, new Vector3(x + 0.35f, y, 0.1f), 7, random);
                        Coins(b, new Vector3(x + 0.62f, y, 0.02f), 4, random);
                        Jug(b, new Vector3(x + 1.1f, y, 0f), Stoneware);
                        continue;
                    }
                    for (float at = x - bay * 0.5f + 0.24f; at < x + bay * 0.5f - 0.2f; at += random.Range(0.25f, 0.4f))
                    {
                        switch (random.Int(7))
                        {
                            case 0:
                            case 1:
                            case 2:
                                Bottle(b, new Vector3(at, y, random.Range(-0.08f, 0.1f)), random.Pick(Glass), random.Range(0.46f, 0.7f));
                                break;
                            case 3:
                                Mug(b, new Vector3(at, y, 0.06f), random.Chance(0.6f) ? Hall.Pewter : Stoneware, random.Range(0f, MathF.Tau));
                                if (random.Chance(0.4f)) b.Tube(new Vector3(at, y + 0.62f, 0.06f), new Vector3(at, y + 0.32f, 0.06f), 0.115f, 0.13f, 8, Hall.Pewter);
                                break;
                            case 4:
                                Jug(b, new Vector3(at + 0.08f, y, 0f), random.Chance(0.5f) ? Stoneware : Hall.Brick(150, 132, 120), random.Range(0.9f, 1.2f));
                                at += 0.22f;
                                break;
                            case 5:
                                Cask(b, new Vector3(at + 0.12f, y + 0.27f, -0.2f), new Vector3(at + 0.12f, y + 0.27f, 0.24f), 0.27f);
                                at += 0.4f;
                                break;
                            default:
                                b.Tube(new Vector3(at + 0.1f, y + 0.34f, -front + 0.12f), new Vector3(at + 0.1f, y + 0.34f, -front + 0.16f), 0.32f, 0.32f, 12, random.Chance(0.5f) ? Hall.Pewter : Stoneware);
                                at += 0.34f;
                                break;
                        }
                    }
                }
            }
            // Along its top, where the camera looks: kegs lying, jars, and a lost hat.
            float crown = body;
            for (float x = -half.X + 0.5f; x < half.X - 0.4f; x += random.Range(0.75f, 1.2f))
            {
                if (random.Chance(0.55f)) Cask(b, new Vector3(x - 0.3f, crown + 0.28f, 0f), new Vector3(x + 0.3f, crown + 0.28f, 0.04f), 0.27f);
                else if (random.Chance(0.6f)) b.Tube(new Vector3(x, crown, 0f), new Vector3(x, crown + 0.5f, 0f), 0.2f, 0.16f, 9, Stoneware);
                else Crate(b, new Vector3(x, crown, 0f), new Vector3(0.3f, 0.2f, 0.24f), random.Range(-0.2f, 0.2f), random);
            }
        }

        // An iron-bound chest with a lock on it.
        private static void Strongbox(MeshBuilder b, Vector3 foot)
        {
            Vector3 half = new Vector3(0.48f, 0.26f, 0.26f);
            b.Box(foot + Vector3.UnitY * half.Y, half, Walnut);
            b.Tube(foot + new Vector3(-half.X, half.Y * 2f - 0.06f, 0f), foot + new Vector3(half.X, half.Y * 2f - 0.06f, 0f), half.Z, half.Z, 10, Walnut);
            foreach (float x in new[] { -0.3f, 0f, 0.3f })
            {
                b.Box(foot + new Vector3(x, half.Y, 0f), new Vector3(0.04f, half.Y + 0.004f, half.Z + 0.008f), Iron);
                b.Tube(foot + new Vector3(x - 0.04f, half.Y * 2f - 0.06f, 0f), foot + new Vector3(x + 0.04f, half.Y * 2f - 0.06f, 0f), half.Z + 0.01f, half.Z + 0.01f, 10, Iron, capped: false);
            }
            b.Box(foot + new Vector3(0f, half.Y * 1.5f, half.Z + 0.03f), new Vector3(0.07f, 0.09f, 0.025f), Hall.Iron(60));
        }

        // ---- The hearth.

        /// <summary>
        /// The hearth: two piers of dressed stone, a beam of oak across them, and a hood of stone narrowing up the wall to where the roof was. A fire of logs on iron dogs, a pot
        /// on a crane over it, what's kept on a mantel, antlers on the hood, wood stacked dry beside it. Its fire burns where its logs are.
        /// </summary>
        private static void Inglenook(MeshBuilder b, Vector2 half, float h, List<AssetLight> lights, WorldRandom random)
        {
            const float Pier = 0.5f;
            float open = half.X - Pier * 2f;
            const float Lintel = 3f;
            foreach (float s in new[] { -1f, 1f })
            {
                b.Box(new Vector3(s * (half.X - Pier), Lintel * 0.5f, 0f), new Vector3(Pier, Lintel * 0.5f, half.Y), Dressed);
                b.Box(new Vector3(s * (half.X - Pier), 0.2f, 0.03f), new Vector3(Pier + 0.06f, 0.2f, half.Y + 0.04f), Dressed);
                b.Box(new Vector3(s * (half.X - Pier), Lintel - 0.12f, 0.03f), new Vector3(Pier + 0.05f, 0.12f, half.Y + 0.03f), Dressed);
            }
            // The firebox: brick gone black, a raised stone to lay the fire on, and an iron plate to throw its heat out.
            VertexMaterial sooty = Hall.Brick(74, 62, 56);
            b.Box(new Vector3(0f, Lintel * 0.5f, -half.Y + 0.08f), new Vector3(open, Lintel * 0.5f, 0.08f), sooty);
            foreach (float s in new[] { -1f, 1f }) b.Box(new Vector3(s * (open - 0.04f), Lintel * 0.5f, 0f), new Vector3(0.05f, Lintel * 0.5f, half.Y - 0.02f), sooty);
            b.Box(new Vector3(0f, 0.1f, 0.02f), new Vector3(open, 0.1f, half.Y - 0.04f), Hall.Stone(120, 112, 104));
            b.Box(new Vector3(0f, 1f, -half.Y + 0.19f), new Vector3(0.85f, 0.62f, 0.03f), Hall.Iron(52));
            b.Tube(new Vector3(0f, 1.6f, -half.Y + 0.16f), new Vector3(0f, 1.6f, -half.Y + 0.22f), 0.85f, 0.85f, 14, Hall.Iron(52));

            // The beam, and the shelf on it.
            Hall.Board(b, new Vector3(-half.X - 0.22f, Lintel + 0.32f, 0.38f), new Vector3(half.X + 0.22f, Lintel + 0.32f, 0.38f), Vector3.UnitY, 0.32f, 0.36f, FrameDark);
            float shelf = Lintel + 0.7f;
            Hall.Board(b, new Vector3(-half.X - 0.34f, shelf - 0.05f, 0.52f), new Vector3(half.X + 0.34f, shelf - 0.05f, 0.52f), Vector3.UnitZ, 0.5f, 0.05f, Oak);
            foreach (float x in new[] { -half.X + 0.3f, 0f, half.X - 0.3f }) b.Box(new Vector3(x, Lintel + 0.48f, 0.84f), new Vector3(0.08f, 0.16f, 0.1f), Walnut);

            // The hood: shoulders of brick drawn in over the beam to a breast that goes straight up the wall to where the roof was, with a course of stone where they meet.
            float y0 = Lintel + 0.64f, y1 = 5.3f, y2 = 9f - FloorTop;
            const float Flue = 1.55f;
            float breast = -half.Y + 0.6f;
            VertexMaterial brick = Hall.Brick(172, 150, 136);
            Vector3 l0 = new Vector3(-half.X, y0, half.Y - 0.02f), r0 = new Vector3(half.X, y0, half.Y - 0.02f);
            Vector3 l1 = new Vector3(-Flue, y1, breast), r1 = new Vector3(Flue, y1, breast);
            Vector3 lb0 = new Vector3(-half.X, y0, -half.Y), rb0 = new Vector3(half.X, y0, -half.Y);
            Vector3 lb1 = new Vector3(-Flue, y1, -half.Y), rb1 = new Vector3(Flue, y1, -half.Y);
            Hall.Face(b, l0, r0, r1, l1, Vector3.UnitZ, brick);
            Hall.Face(b, r0, rb0, rb1, r1, Vector3.UnitX, brick);
            Hall.Face(b, lb0, l0, l1, lb1, -Vector3.UnitX, brick);
            b.Box(new Vector3(0f, y1 + 0.1f, (breast - half.Y) * 0.5f + 0.03f), new Vector3(Flue + 0.07f, 0.1f, (breast + half.Y) * 0.5f + 0.04f), Dressed);
            b.Box(new Vector3(0f, (y1 + 0.2f + y2) * 0.5f, (breast - half.Y) * 0.5f), new Vector3(Flue, (y2 - y1 - 0.2f) * 0.5f, (breast + half.Y) * 0.5f), brick);

            // The fire: dogs, logs across them, the bed of embers they've made, and its flames.
            foreach (float s in new[] { -0.72f, 0.72f })
            {
                b.Beam(new Vector3(s, 0.2f, 0.42f), new Vector3(s, 1.1f, 0.42f), 0.035f, Iron);
                b.Tube(new Vector3(s, 1.1f, 0.42f), new Vector3(s, 1.26f, 0.42f), 0.08f, 0.05f, 7, Iron);
                b.Beam(new Vector3(s, 0.44f, 0.42f), new Vector3(s, 0.44f, -0.42f), 0.03f, Iron);
                b.Beam(new Vector3(s, 0.2f, -0.4f), new Vector3(s, 0.44f, -0.4f), 0.03f, Iron);
                b.Beam(new Vector3(s - 0.12f, 0.22f, 0.42f), new Vector3(s + 0.12f, 0.22f, 0.42f), 0.03f, Iron);
            }
            VertexMaterial log = Hall.Bark(104, 86, 70);
            VertexMaterial charred = new VertexMaterial(MaterialCatalog.BarkCharred.Index, new Colour(150, 140, 134));
            b.Tube(new Vector3(-1.1f, 0.62f, 0.14f), new Vector3(1.12f, 0.64f, 0.06f), 0.17f, 0.16f, 7, log);
            b.Tube(new Vector3(-1f, 0.62f, -0.26f), new Vector3(1.04f, 0.66f, -0.2f), 0.16f, 0.15f, 7, charred);
            b.Tube(new Vector3(-0.84f, 0.92f, -0.08f), new Vector3(0.9f, 0.9f, -0.02f), 0.15f, 0.14f, 7, charred);
            b.Box(new Vector3(0f, 0.27f, -0.06f), new Vector3(1.05f, 0.07f, 0.42f), Hall.Ember);
            var embers = new WorldRandom(0xE3BE);
            for (int i = 0; i < 16; i++)
            {
                float size = embers.Range(0.06f, 0.13f);
                Vector3 at = new Vector3(embers.Range(-1f, 1f), 0.34f + size * 0.5f, embers.Range(-0.4f, 0.3f));
                b.Box(at, new Vector3(size, size * 0.6f, size), embers.Chance(0.6f) ? Hall.Glow(1f, 0.44f, 0.1f, 3.2f) : Hall.Flat(34, 28, 26));
            }
            Hall.Flame(lights, new Vector3(-0.46f, 0.7f, -0.08f), 0.85f);
            Hall.Flame(lights, new Vector3(0.42f, 0.74f, -0.04f), 0.7f);
            Hall.Flame(lights, new Vector3(0f, 0.98f, -0.1f), 0.55f);
            lights.Add(AssetLight.Campfire(new Vector3(0f, 1.7f, half.Y + 0.9f)) with { Intensity = 82f, Radius = 22f });

            // A pot on a crane swung out over it.
            float hinge = -open + 0.1f;
            b.Beam(new Vector3(hinge, 0.3f, 0.2f), new Vector3(hinge, 2.6f, 0.2f), 0.035f, Iron);
            b.Beam(new Vector3(hinge, 2.5f, 0.2f), new Vector3(0.12f, 2.5f, 0.12f), 0.03f, Iron);
            b.Beam(new Vector3(hinge, 1.7f, 0.2f), new Vector3(-0.5f, 2.46f, 0.16f), 0.025f, Iron);
            Vector3 pot = new Vector3(0.12f, 1.36f, 0.12f);
            b.Tube(new Vector3(0.12f, 2.5f, 0.12f), pot + Vector3.UnitY * 0.56f, 0.016f, 0.016f, 4, Iron);
            b.Tube(pot + Vector3.UnitY * 0.12f, pot - Vector3.UnitY * 0.04f, 0.34f, 0.2f, 10, Hall.Iron(50));
            b.Tube(pot + Vector3.UnitY * 0.12f, pot + Vector3.UnitY * 0.5f, 0.34f, 0.38f, 10, Hall.Iron(50), capped: false);
            b.Tube(pot + Vector3.UnitY * 0.4f, pot + Vector3.UnitY * 0.44f, 0.35f, 0.35f, 10, Hall.Flat(92, 64, 34));
            foreach (float s in new[] { -1f, 1f }) b.Beam(pot + new Vector3(s * 0.36f, 0.5f, 0f), pot + Vector3.UnitY * 0.86f, 0.014f, Iron);

            // On the mantel: a candle at either end, tankards in a row, a little cask, a jar.
            Hall.Candle(b, new Vector3(-half.X + 0.1f, shelf, 0.6f), 0.46f, 0.06f, lights);
            Hall.Candle(b, new Vector3(half.X - 0.1f, shelf, 0.6f), 0.4f, 0.06f, lights);
            b.Tube(new Vector3(half.X - 0.1f, shelf, 0.6f), new Vector3(half.X - 0.1f, shelf + 0.03f, 0.6f), 0.14f, 0.1f, 7, Hall.Wax);
            for (int i = 0; i < 3; i++) Mug(b, new Vector3(-1.75f + i * 0.34f, shelf, 0.56f), Hall.Pewter, 1.2f + i * 0.4f);
            Cask(b, new Vector3(1.05f, shelf + 0.24f, 0.56f), new Vector3(1.6f, shelf + 0.24f, 0.6f), 0.24f);
            b.Tube(new Vector3(0.2f, shelf, 0.56f), new Vector3(0.2f, shelf + 0.44f, 0.56f), 0.17f, 0.13f, 9, Stoneware);
            b.Tube(new Vector3(-0.42f, shelf, 0.62f), new Vector3(-0.42f, shelf + 0.05f, 0.62f), 0.26f, 0.28f, 10, Hall.Pewter);

            // Antlers on the breast, over the beam.
            Antlers(b, new Vector3(0f, 6.6f, breast + 0.01f), Vector3.UnitZ, 1.2f);

            // Wood split and stacked on the far side, ends out, in an iron frame, and the irons leaning on the near pier.
            VertexMaterial ends = Hall.Timber(184, 146, 102);
            float stack = half.X + 0.3f;
            for (int row = 0; row < 5; row++)
            {
                for (int column = 0; column < 4 - (row == 4 ? 1 : 0); column++)
                {
                    float radius = random.Range(0.13f, 0.17f);
                    Vector3 back = new Vector3(stack + 0.17f + column * 0.33f + (row % 2) * 0.12f, 0.18f + row * 0.29f, -half.Y + 0.06f);
                    Vector3 front = back + Vector3.UnitZ * random.Range(0.95f, 1.15f);
                    b.Tube(back, front, radius, radius, 6, log, capped: false);
                    b.Tube(front - Vector3.UnitZ * 0.01f, front + Vector3.UnitZ * 0.015f, radius * 0.97f, radius * 0.97f, 6, ends);
                }
            }
            foreach (float x in new[] { stack - 0.02f, stack + 1.5f })
            {
                b.Beam(new Vector3(x, 0f, 0.2f), new Vector3(x, 1.7f, 0.2f), 0.025f, Iron);
            }
            b.Beam(new Vector3(stack - 0.02f, 0.03f, 0.2f), new Vector3(stack + 1.5f, 0.03f, 0.2f), 0.025f, Iron);
            float lean = -half.X - 0.22f;
            b.Beam(new Vector3(lean, 0f, 0.95f), new Vector3(lean + 0.1f, 2.3f, half.Y + 0.04f), 0.022f, Iron);
            b.Beam(new Vector3(lean - 0.2f, 0f, 0.9f), new Vector3(lean - 0.08f, 2.1f, half.Y + 0.04f), 0.022f, Iron);
            b.Box(new Vector3(lean - 0.2f, 0.12f, 0.9f), new Vector3(0.11f, 0.12f, 0.02f), Iron);
        }

        // ---- Tables for games.

        // A table in a booth: a round top bound in iron on a great cask, with a game on it that someone's in the middle of.
        private static void BoothTable(MeshBuilder b, float r, float h, int variant, List<AssetLight> lights, WorldRandom random)
        {
            HallFurniture.Barrel(b, Vector3.Zero, h - 0.12f, 0.66f);
            Hall.Board(b, new Vector3(-0.85f, 0.05f, 0f), new Vector3(0.85f, 0.05f, 0f), Vector3.UnitY, 0.05f, 0.1f, Walnut);
            Hall.Board(b, new Vector3(0f, 0.05f, -0.85f), new Vector3(0f, 0.05f, 0.85f), Vector3.UnitY, 0.05f, 0.1f, Walnut);
            b.Tube(new Vector3(0f, h - 0.12f, 0f), new Vector3(0f, h, 0f), r, r, 18, Oak);
            b.Tube(new Vector3(0f, h - 0.1f, 0f), new Vector3(0f, h - 0.02f, 0f), r + 0.012f, r + 0.012f, 18, Iron, capped: false);
            float top = h;
            switch (variant)
            {
                case 0:
                    // Cards: a hand to each of four, face down, one turned up to show; the deck; what's staked in the middle.
                    for (int seat = 0; seat < 4; seat++)
                    {
                        float a = MathF.PI * 0.5f * seat + 0.5f;
                        Vector3 at = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a)) * (r * 0.62f);
                        for (int i = 0; i < 3; i++) Card(b, at + new Vector3(0f, top + 0.008f + i * 0.012f, 0f) + new Vector3(-MathF.Sin(a), 0f, MathF.Cos(a)) * ((i - 1) * 0.16f), -a + MathF.PI * 0.5f + (i - 1) * 0.22f, seat == 1, seat + i);
                    }
                    b.Box(new Vector3(0.1f, top + 0.06f, -0.1f), new Vector3(0.14f, 0.06f, 0.2f), Hall.Flat(30, 84, 88));
                    Spill(b, new Vector3(-0.2f, top, 0.1f), 0.22f, 9, random);
                    Mug(b, new Vector3(r * 0.74f, top, -r * 0.3f), Hall.Pewter, 0.3f);
                    Mug(b, new Vector3(-r * 0.78f, top, -r * 0.1f), Stoneware, 2.6f);
                    BottleCandle(b, new Vector3(-0.1f, top, -r * 0.62f), lights, 11f);
                    break;
                case 1:
                    // Dice: a throw lying where it fell, the cup, coin won and coin lost, and a knife stood in the wood.
                    foreach ((float x, float z) in new[] { (-0.12f, 0.2f), (0.2f, 0.34f), (0.05f, -0.06f), (0.42f, 0.02f) }) Die(b, new Vector3(x, top + 0.1f, z), 0.1f, random.Range(0f, MathF.Tau));
                    DiceCup(b, new Vector3(-0.5f, top, -0.2f));
                    Coins(b, new Vector3(0.62f, top, -0.46f), 9, random);
                    Coins(b, new Vector3(0.84f, top, -0.2f), 5, random);
                    Coins(b, new Vector3(-0.6f, top, 0.5f), 3, random);
                    Mug(b, new Vector3(-r * 0.2f, top, -r * 0.78f), Hall.Pewter, 1.9f, 1.1f);
                    Mug(b, new Vector3(r * 0.7f, top, r * 0.4f), Hall.Pewter, 0.2f, 1.1f);
                    Vector3 knife = new Vector3(-0.72f, top, 0.06f);
                    Hall.Board(b, knife, knife + new Vector3(0.06f, 0.36f, 0.04f), Vector3.UnitX, 0.03f, 0.008f, Hall.Silver);
                    b.Tube(knife + new Vector3(0.06f, 0.36f, 0.04f), knife + new Vector3(0.1f, 0.6f, 0.066f), 0.03f, 0.034f, 6, Hall.Leather(70, 42, 26));
                    DishCandle(b, new Vector3(0.1f, top, -r * 0.58f), lights, 0.3f);
                    break;
                default:
                    // The shells: three cups in a row, the pea beside the one it isn't under, and what's been bet on it.
                    for (int i = 0; i < 3; i++)
                    {
                        Vector3 cup = new Vector3(-0.42f + i * 0.42f, top, 0.2f + (i == 1 ? 0.06f : 0f));
                        b.Tube(cup + Vector3.UnitY * 0.2f, cup, 0.09f, 0.15f, 9, Hall.Timber(120, 82, 50));
                        b.Tube(cup + Vector3.UnitY * 0.2f, cup + Vector3.UnitY * 0.24f, 0.09f, 0.05f, 9, Hall.Timber(120, 82, 50));
                    }
                    HallFurniture.Sphere(b, new Vector3(0.22f, top + 0.045f, 0.5f), 0.045f, 6, 4, Hall.Flat(70, 110, 52));
                    Coins(b, new Vector3(-0.5f, top, -0.3f), 6, random);
                    Spill(b, new Vector3(0.5f, top, -0.28f), 0.18f, 5, random);
                    Jug(b, new Vector3(-r * 0.3f, top, -r * 0.7f), Stoneware);
                    Mug(b, new Vector3(r * 0.74f, top, 0.1f), Hall.Pewter, 0.1f);
                    BottleCandle(b, new Vector3(r * 0.3f, top, -r * 0.66f), lights, 11f);
                    break;
            }
        }

        // A barrel stood on its end by the fire with a board laid on it, and a game of draughts left as it stood.
        private static void DraughtsBarrel(MeshBuilder b, float r, float h, List<AssetLight> lights)
        {
            HallFurniture.Barrel(b, Vector3.Zero, h - 0.07f, r * 0.84f);
            b.Tube(new Vector3(0f, h - 0.07f, 0f), new Vector3(0f, h, 0f), r + 0.1f, r + 0.1f, 14, Oak);
            const float Square = 0.1f;
            Hall.At(b, new Vector3(-0.06f, h, 0.04f), 0.3f, () =>
            {
                b.Box(new Vector3(0f, 0.012f, 0f), new Vector3(Square * 3f + 0.03f, 0.012f, Square * 3f + 0.03f), Pale);
                for (int i = 0; i < 6; i++)
                {
                    for (int j = 0; j < 6; j++)
                    {
                        Vector3 at = new Vector3((i - 2.5f) * Square, 0.026f, (j - 2.5f) * Square);
                        if ((i + j) % 2 == 0) b.Box(at, new Vector3(Square * 0.5f, 0.002f, Square * 0.5f), Walnut);
                        bool pale = j < 2 && (i + j) % 2 == 0, dark = j > 3 && (i + j) % 2 == 0 && i != 2;
                        if (pale || dark) b.Tube(at, at + Vector3.UnitY * 0.03f, 0.036f, 0.036f, 7, pale ? Hall.Bone(222, 210, 186) : Hall.Flat(40, 32, 28));
                    }
                }
            });
            Mug(b, new Vector3(r * 0.62f, h, -r * 0.5f), Hall.Pewter, 0.9f);
            DishCandle(b, new Vector3(-r * 0.6f, h, -r * 0.62f), lights, 0.26f);
        }

        // ---- The stage.

        // A low stage of boards for whoever plays: a lute on its stand, a drum, a fiddle left on a stool, the music, a hat for coin, and candles along its edge.
        private static void Stage(MeshBuilder b, Vector2 half, float h, List<AssetLight> lights, WorldRandom random)
        {
            b.Box(new Vector3(0f, (h - 0.07f) * 0.5f, 0f), new Vector3(half.X - 0.04f, (h - 0.07f) * 0.5f, half.Y - 0.04f), FrameDark);
            int boards = (int)MathF.Round(half.X * 2f / 0.42f);
            for (int i = 0; i < boards; i++)
            {
                float x = -half.X + half.X * 2f * (i + 0.5f) / boards;
                Hall.Board(b, new Vector3(x, h - 0.035f, -half.Y), new Vector3(x, h - 0.035f, half.Y + random.Range(-0.02f, 0.04f)), Vector3.UnitX, half.X / boards - 0.008f, 0.035f, Weathered(random));
            }
            // A worn square of carpet for the player's feet.
            b.Box(new Vector3(0.1f, h + 0.012f, -0.1f), new Vector3(1.5f, 0.012f, 1.2f), Dyed(Blue));
            b.Box(new Vector3(0.1f, h + 0.026f, -0.1f), new Vector3(1.3f, 0.003f, 1f), Dyed(Teal));
            b.Box(new Vector3(0.1f, h + 0.032f, -0.1f), new Vector3(0.5f, 0.003f, 0.4f), Dyed(Saffron));

            // The lute, leaning in its stand: a bowl, a flat face with a rose cut in it, a neck and a head bent back.
            Hall.At(b, new Vector3(-half.X * 0.5f, h, -0.5f), 0.5f, () =>
            {
                b.Beam(new Vector3(-0.3f, 0f, 0.2f), new Vector3(0f, 0.9f, -0.2f), 0.025f, Walnut);
                b.Beam(new Vector3(0.3f, 0f, 0.2f), new Vector3(0f, 0.9f, -0.2f), 0.025f, Walnut);
                b.Beam(new Vector3(0f, 0f, -0.4f), new Vector3(0f, 0.9f, -0.2f), 0.025f, Walnut);
                Vector3 body = new Vector3(0f, 0.62f, 0f);
                Vector3 face = new Vector3(0f, 0.3f, 0.95f).Normalized();
                b.Tube(body - face * 0.24f, body, 0.2f, 0.46f, 12, Hall.Timber(150, 96, 54), capped: false);
                b.Tube(body - face * 0.01f, body + face * 0.012f, 0.46f, 0.46f, 12, Hall.Timber(206, 170, 118));
                b.Tube(body + face * 0.012f, body + face * 0.018f, 0.12f, 0.12f, 8, Hall.Flat(32, 24, 18));
                Vector3 up = Vector3.Cross(Vector3.UnitX, face).Normalized();
                if (up.Y < 0f) up = -up;
                Hall.Board(b, body + up * 0.4f, body + up * 1.5f, Vector3.UnitX, 0.06f, 0.035f, Walnut);
                Hall.Board(b, body + up * 1.5f, body + up * 1.5f - face * 0.34f + up * 0.06f, Vector3.UnitX, 0.07f, 0.03f, Walnut);
            });

            // A drum with its sticks on it.
            Vector3 drum = new Vector3(half.X * 0.5f, h, -0.9f);
            b.Tube(drum, drum + Vector3.UnitY * 0.86f, 0.52f, 0.52f, 14, Hall.Timber(150, 92, 50), capped: false);
            b.Tube(drum + Vector3.UnitY * 0.84f, drum + Vector3.UnitY * 0.87f, 0.53f, 0.53f, 14, Hall.Leather(220, 204, 170));
            foreach (float y in new[] { 0.08f, 0.8f }) b.Tube(drum + Vector3.UnitY * (y - 0.03f), drum + Vector3.UnitY * (y + 0.03f), 0.54f, 0.54f, 14, Dyed(Saffron), capped: false);
            for (int i = 0; i < 7; i++)
            {
                float a0 = MathF.Tau * i / 7f, a1 = MathF.Tau * (i + 0.5f) / 7f;
                b.Beam(drum + new Vector3(MathF.Cos(a0) * 0.545f, 0.8f, MathF.Sin(a0) * 0.545f), drum + new Vector3(MathF.Cos(a1) * 0.545f, 0.08f, MathF.Sin(a1) * 0.545f), 0.012f, Twine);
                b.Beam(drum + new Vector3(MathF.Cos(a1) * 0.545f, 0.08f, MathF.Sin(a1) * 0.545f), drum + new Vector3(MathF.Cos(a0 + MathF.Tau / 7f) * 0.545f, 0.8f, MathF.Sin(a0 + MathF.Tau / 7f) * 0.545f), 0.012f, Twine);
            }
            b.Tube(drum + new Vector3(-0.3f, 0.9f, 0.1f), drum + new Vector3(0.3f, 0.9f, -0.12f), 0.022f, 0.022f, 5, Pale);
            b.Tube(drum + new Vector3(-0.2f, 0.9f, -0.2f), drum + new Vector3(0.26f, 0.9f, 0.2f), 0.022f, 0.022f, 5, Pale);

            // A stool with a fiddle and its bow laid across it.
            Vector3 stool = new Vector3(0.2f, h, 0.3f);
            for (int i = 0; i < 3; i++)
            {
                float a = MathF.Tau * i / 3f + 0.3f;
                b.Beam(stool + new Vector3(MathF.Cos(a) * 0.38f, 0f, MathF.Sin(a) * 0.38f), stool + new Vector3(MathF.Cos(a) * 0.18f, 1.1f, MathF.Sin(a) * 0.18f), 0.045f, Oak);
            }
            b.Tube(stool + Vector3.UnitY * 1.08f, stool + Vector3.UnitY * 1.18f, 0.36f, 0.36f, 12, Oak);
            Hall.At(b, stool + Vector3.UnitY * 1.18f, 0.7f, () =>
            {
                b.Tube(new Vector3(-0.12f, 0f, 0f), new Vector3(-0.12f, 0.07f, 0f), 0.17f, 0.17f, 10, Hall.Timber(160, 88, 44));
                b.Tube(new Vector3(0.1f, 0f, 0f), new Vector3(0.1f, 0.07f, 0f), 0.14f, 0.14f, 10, Hall.Timber(160, 88, 44));
                Hall.Board(b, new Vector3(0.2f, 0.06f, 0f), new Vector3(0.62f, 0.06f, 0f), Vector3.UnitZ, 0.03f, 0.025f, Walnut);
                b.Tube(new Vector3(-0.3f, 0.1f, 0.2f), new Vector3(0.5f, 0.1f, 0.14f), 0.012f, 0.012f, 4, Pale);
            });

            // The music on its stand, and a hat on the boards with what's been thrown in it.
            Vector3 stand = new Vector3(1.4f, h, 0.8f);
            for (int i = 0; i < 3; i++)
            {
                float a = MathF.Tau * i / 3f;
                b.Beam(stand + new Vector3(MathF.Cos(a) * 0.3f, 0f, MathF.Sin(a) * 0.3f), stand + Vector3.UnitY * 0.5f, 0.02f, Iron);
            }
            b.Beam(stand + Vector3.UnitY * 0.5f, stand + Vector3.UnitY * 2f, 0.022f, Iron);
            Hall.Board(b, stand + new Vector3(0f, 1.86f, 0.1f), stand + new Vector3(0f, 2.36f, -0.04f), Vector3.UnitX, 0.34f, 0.015f, Walnut);
            Hall.Board(b, stand + new Vector3(0f, 1.9f, 0.115f), stand + new Vector3(0f, 2.32f, -0.005f), Vector3.UnitX, 0.28f, 0.006f, Hall.Paper);
            Vector3 hat = new Vector3(-0.5f, h + 0.03f, half.Y - 0.5f);
            b.Tube(hat, hat + Vector3.UnitY * 0.03f, 0.42f, 0.44f, 12, Hall.Cloth(70, 60, 48));
            b.Tube(hat + Vector3.UnitY * 0.03f, hat + Vector3.UnitY * 0.22f, 0.24f, 0.28f, 10, Hall.Cloth(70, 60, 48), capped: false);
            b.Tube(hat + Vector3.UnitY * 0.03f, hat + Vector3.UnitY * 0.05f, 0.23f, 0.23f, 10, Hall.Cloth(48, 40, 32));
            Spill(b, hat + Vector3.UnitY * 0.05f, 0.12f, 5, random);
            b.Tube(hat + new Vector3(0.6f, -0.03f, 0.12f), hat + new Vector3(0.6f, 0f, 0.12f), 0.1f, 0.1f, 8, Gold);

            // Candles along the two edges an audience sits at.
            foreach (Vector3 at in new[] { new Vector3(half.X - 0.2f, h, -half.Y + 0.3f), new Vector3(half.X - 0.2f, h, 0.3f), new Vector3(0f, h, -half.Y + 0.2f), new Vector3(-half.X + 0.9f, h, -half.Y + 0.2f) })
            {
                DishCandle(b, at, lights, 0.26f);
            }
            lights.Add(AssetLight.Candles(new Vector3(half.X - 0.6f, h + 1.4f, -half.Y + 0.6f)) with { Intensity = 30f, Radius = 12f, Shadow = false });
        }

        // ---- What lies on the floor.

        // The carpet the great table stands on, woven in the Haven's colours: a saffron border, bands of cream and blue, a field of teal, and a lozenge at either end.
        private static void GreatRug(MeshBuilder b, Vector2 half)
        {
            float y = 0.014f;
            void Layer(float insetX, float insetZ, Vector3 colour, float scale = 1f)
            {
                b.Box(new Vector3(0f, y, 0f), new Vector3(half.X - insetX, 0.006f, half.Y - insetZ), Dyed(colour * scale));
                y += 0.007f;
            }
            Layer(0f, 0f, Saffron);
            Layer(0.24f, 0.24f, Cream);
            Layer(0.34f, 0.34f, Blue);
            Layer(0.74f, 0.74f, Cream);
            Layer(0.82f, 0.82f, Teal);
            Layer(1.3f, 1.3f, Teal, 0.82f);
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f }) b.Box(new Vector3(sx * (half.X - 0.54f), y - 0.02f, sz * (half.Y - 0.54f)), new Vector3(0.13f, 0.006f, 0.13f), Dyed(Saffron));
                Vector3 lozenge = new Vector3(sx * (half.X - 1.75f), y, 0f);
                Hall.Tri(b, lozenge - Vector3.UnitZ * 1.3f, lozenge + Vector3.UnitX * 0.5f, lozenge + Vector3.UnitZ * 1.3f, Vector3.UnitY, Dyed(Saffron));
                Hall.Tri(b, lozenge - Vector3.UnitZ * 1.3f, lozenge - Vector3.UnitX * 0.5f, lozenge + Vector3.UnitZ * 1.3f, Vector3.UnitY, Dyed(Saffron));
                Vector3 heart = lozenge + Vector3.UnitY * 0.006f;
                Hall.Tri(b, heart - Vector3.UnitZ * 0.7f, heart + Vector3.UnitX * 0.27f, heart + Vector3.UnitZ * 0.7f, Vector3.UnitY, Dyed(Cream));
                Hall.Tri(b, heart - Vector3.UnitZ * 0.7f, heart - Vector3.UnitX * 0.27f, heart + Vector3.UnitZ * 0.7f, Vector3.UnitY, Dyed(Cream));
                // Its fringe, at either end.
                for (float z = -half.Y + 0.08f; z < half.Y; z += 0.16f) b.Box(new Vector3(sx * (half.X + 0.11f), 0.012f, z), new Vector3(0.11f, 0.005f, 0.03f), Dyed(Cream));
            }
        }

        // A runner up from the door, striped across.
        private static void Runner(MeshBuilder b, Vector2 half)
        {
            b.Box(new Vector3(0f, 0.014f, 0f), new Vector3(half.X, 0.006f, half.Y), Dyed(Blue));
            b.Box(new Vector3(0f, 0.021f, 0f), new Vector3(half.X - 0.2f, 0.004f, half.Y - 0.12f), Dyed(Saffron));
            b.Box(new Vector3(0f, 0.027f, 0f), new Vector3(half.X - 0.3f, 0.004f, half.Y - 0.22f), Dyed(Teal));
            for (float z = -half.Y + 0.7f; z < half.Y - 0.5f; z += 0.9f)
            {
                b.Box(new Vector3(0f, 0.033f, z), new Vector3(half.X - 0.3f, 0.003f, 0.09f), Dyed(Cream));
                b.Box(new Vector3(0f, 0.037f, z), new Vector3(half.X - 0.3f, 0.003f, 0.03f), Dyed(Saffron));
            }
            foreach (float s in new[] { -1f, 1f })
            {
                for (float x = -half.X + 0.06f; x < half.X; x += 0.14f) b.Box(new Vector3(x, 0.012f, s * (half.Y + 0.1f)), new Vector3(0.028f, 0.005f, 0.1f), Dyed(Cream));
            }
        }

        // A cow's hide before the fire: its own outline, legs and all, pale with dark patches.
        private static void HideRug(MeshBuilder b, Vector2 half)
        {
            const int Points = 28;
            var rim = new Vector3[Points];
            for (int i = 0; i < Points; i++)
            {
                float a = MathF.Tau * i / Points;
                // Legs out at the four corners, a neck at one end, drawn in between them.
                float legs = MathF.Pow(MathF.Abs(MathF.Sin(a * 2f)), 5f) * 0.34f;
                float neck = MathF.Pow(MathF.Max(MathF.Cos(a), 0f), 8f) * 0.12f;
                float reach = 0.7f + legs + neck + 0.04f * MathF.Sin(a * 7f);
                rim[i] = new Vector3(MathF.Cos(a) * half.X * reach, 0.03f, MathF.Sin(a) * half.Y * reach);
            }
            VertexMaterial pale = Hall.Pelt(198, 184, 158);
            Vector3 middle = new Vector3(0f, 0.034f, 0f);
            for (int i = 0; i < Points; i++) Hall.Tri(b, middle, rim[i], rim[(i + 1) % Points], Vector3.UnitY, pale);
            var random = new WorldRandom(0xC0F);
            VertexMaterial dark = Hall.Pelt(104, 72, 48);
            foreach ((float x, float z, float size) in new[] { (-0.45f, 0.2f, 0.5f), (0.4f, -0.3f, 0.42f), (0.1f, 0.42f, 0.26f), (-0.2f, -0.5f, 0.22f), (0.72f, 0.3f, 0.2f) })
            {
                Vector3 centre = new Vector3(x * half.X, 0.04f, z * half.Y);
                var blot = new Vector3[9];
                for (int i = 0; i < 9; i++)
                {
                    float a = MathF.Tau * i / 9f;
                    float reach = size * random.Range(0.7f, 1.2f);
                    blot[i] = centre + new Vector3(MathF.Cos(a) * half.X * reach, 0f, MathF.Sin(a) * half.Y * reach * 0.9f);
                }
                for (int i = 0; i < 9; i++) Hall.Tri(b, centre, blot[i], blot[(i + 1) % 9], Vector3.UnitY, dark);
            }
        }

        // ---- The barn's own.

        // One of the four posts: a stone under it, braces up to the tie beam it carries, a lantern on an arm towards the table, and what was hung on a nail in it and left.
        private static void Post(MeshBuilder b, in HallPiece piece, List<AssetLight> lights)
        {
            float head = TieUnder - FloorTop;
            b.Box(new Vector3(0f, 0.2f, 0f), new Vector3(0.5f, 0.2f, 0.5f), Dressed);
            b.Box(new Vector3(0f, 0.45f, 0f), new Vector3(0.4f, 0.06f, 0.4f), Dressed);
            Hall.Board(b, new Vector3(0f, 0.5f, 0f), new Vector3(0f, head, 0f), Vector3.UnitX, 0.27f, 0.27f, Frame);
            b.Box(new Vector3(0f, head - 0.36f, 0f), new Vector3(0.31f, 0.36f, 0.36f), Frame);
            foreach (float s in new[] { -1f, 1f })
            {
                Hall.Bent(b, Frame, Vector3.UnitX, 0.13f, 0.1f, new Vector3(0f, 5.7f, s * 0.2f), new Vector3(0f, 6.7f, s * 0.62f), new Vector3(0f, 7.5f, s * 1.3f), new Vector3(0f, head + 0.02f, s * 2.3f));
            }
            Vector3 inward = new Vector3(-MathF.Sign(piece.At.X), 0f, 0f);
            LanternArm(b, inward * 0.27f + Vector3.UnitY * 5f, inward, 1.05f, lights, 36f, 13f);
            b.Box(new Vector3(0f, 1.9f, 0f), new Vector3(0.285f, 0.05f, 0.285f), Iron);

            // On its far side, on a peg.
            Vector3 outward = -inward;
            Vector3 peg = outward * 0.27f + Vector3.UnitY * 3.5f;
            b.Beam(peg, peg + outward * 0.22f + Vector3.UnitY * 0.05f, 0.03f, Pale);
            Vector3 hung = peg + outward * 0.16f;
            switch (piece.Variant % 4)
            {
                case 0:
                {
                    // A horse's collar: a ring of stuffed leather, wider at its foot, and its hames.
                    VertexMaterial hide = Hall.Leather(84, 52, 32);
                    const int Segments = 12;
                    Vector3 middle = hung - Vector3.UnitY * 0.62f;
                    for (int i = 0; i < Segments; i++)
                    {
                        float a0 = MathF.Tau * i / Segments, a1 = MathF.Tau * (i + 1) / Segments;
                        Vector3 p0 = middle + new Vector3(0f, MathF.Cos(a0) * 0.66f, MathF.Sin(a0) * 0.44f), p1 = middle + new Vector3(0f, MathF.Cos(a1) * 0.66f, MathF.Sin(a1) * 0.44f);
                        float thick = 0.1f + 0.05f * (1f - MathF.Cos(a0)) * 0.5f;
                        b.Tube(p0, p1, thick, thick, 6, hide, capped: false);
                    }
                    foreach (float s in new[] { -1f, 1f }) b.Beam(middle + new Vector3(outward.X * 0.1f, 0.6f, s * 0.2f), middle + new Vector3(outward.X * 0.1f, -0.5f, s * 0.42f), 0.02f, Iron);
                    break;
                }
                case 1:
                {
                    // Rope, coiled.
                    VertexMaterial rope = Hall.Cloth(158, 134, 92);
                    for (int turn = 0; turn < 3; turn++)
                    {
                        Vector3 middle = hung - Vector3.UnitY * (0.5f + turn * 0.03f) + outward * (turn * 0.05f);
                        const int Segments = 10;
                        for (int i = 0; i < Segments; i++)
                        {
                            float a0 = MathF.Tau * i / Segments, a1 = MathF.Tau * (i + 1) / Segments;
                            b.Tube(middle + new Vector3(0f, MathF.Cos(a0) * 0.5f, MathF.Sin(a0) * 0.34f), middle + new Vector3(0f, MathF.Cos(a1) * 0.5f, MathF.Sin(a1) * 0.34f), 0.035f, 0.035f, 5, rope, capped: false);
                        }
                    }
                    break;
                }
                case 2:
                {
                    // A bridle: its headstall, its bit and its reins hanging in a loop.
                    VertexMaterial strap = Hall.Leather(70, 42, 26);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        b.Beam(hung, hung + new Vector3(0f, -0.8f, s * 0.22f), 0.016f, strap);
                        b.Beam(hung + new Vector3(0f, -0.8f, s * 0.22f), hung + new Vector3(0f, -1.6f, s * 0.3f), 0.016f, strap);
                        b.Beam(hung + new Vector3(0f, -1.6f, s * 0.3f), hung + new Vector3(0f, -1.95f, 0f), 0.016f, strap);
                    }
                    b.Beam(hung + new Vector3(0f, -0.8f, -0.24f), hung + new Vector3(0f, -0.8f, 0.24f), 0.02f, Iron);
                    b.Beam(hung + new Vector3(0f, -0.45f, -0.14f), hung + new Vector3(0f, -0.45f, 0.14f), 0.016f, strap);
                    break;
                }
                default:
                {
                    // Horseshoes nailed up for luck, heels up to hold it in.
                    for (int shoe = 0; shoe < 3; shoe++)
                    {
                        Vector3 middle = outward * 0.3f + Vector3.UnitY * (3.3f - shoe * 0.5f);
                        for (int k = 0; k < 6; k++)
                        {
                            float a0 = MathF.PI * (1.12f + k * 0.126f), a1 = MathF.PI * (1.12f + (k + 1) * 0.126f);
                            b.Beam(middle + new Vector3(0f, MathF.Sin(a0) * 0.17f, MathF.Cos(a0) * 0.15f), middle + new Vector3(0f, MathF.Sin(a1) * 0.17f, MathF.Cos(a1) * 0.15f), 0.024f, Hall.Iron(96));
                        }
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// The boarded side of a stall, as high as a horse's back at its post and sweeping up to the wall, where a horse would reach over: now the side of a booth. Its heel post
        /// keeps the name of whoever was stabled here and carries a lantern over the aisle. It runs along x, the wall at its +x end.
        /// </summary>
        private static void StallWall(MeshBuilder b, Vector2 half, int variant, List<AssetLight> lights, WorldRandom random)
        {
            float heel = -half.X + 0.2f, wall = half.X;
            Hall.Board(b, new Vector3(heel, 0.12f, 0f), new Vector3(wall, 0.12f, 0f), Vector3.UnitY, 0.12f, 0.13f, FrameDark);
            for (int i = 0; i < 6; i++)
            {
                float y = 0.24f + 0.36f * (i + 0.5f);
                Hall.Board(b, new Vector3(heel + 0.1f, y, 0f), new Vector3(wall, y, 0f), Vector3.UnitY, 0.172f, 0.07f, Weathered(random));
            }
            Hall.Board(b, new Vector3(heel + 1.4f, 2.58f, 0f), new Vector3(wall, 2.58f, 0f), Vector3.UnitY, 0.172f, 0.07f, Weathered(random));
            Hall.Board(b, new Vector3(heel + 2.8f, 2.9f, 0f), new Vector3(wall, 2.9f, 0f), Vector3.UnitY, 0.14f, 0.07f, Weathered(random));
            Vector3 low = new Vector3(heel + 0.1f, 2.5f, 0f), high = new Vector3(wall, 3.3f, 0f);
            Hall.Board(b, low, high, Vector3.UnitZ, 0.11f, 0.07f, FrameDark);
            b.Box(new Vector3(0.2f, 1.3f, 0f), new Vector3(0.09f, 1.08f, 0.1f), FrameDark);
            b.Box(new Vector3(wall - 0.12f, 1.75f, 0f), new Vector3(0.12f, 1.75f, 0.13f), Frame);

            // The heel post, turned at its head, banded where the gate hung, with the name board and the lantern.
            Hall.Board(b, new Vector3(heel, 0f, 0f), new Vector3(heel, 3.7f, 0f), Vector3.UnitX, 0.2f, 0.2f, Frame);
            Hall.Turned(b, new Vector3(heel, 3.7f, 0f), Frame, 8, (0f, 0.2f), (0.08f, 0.25f), (0.14f, 0.12f), (0.3f, 0.21f), (0.42f, 0.17f), (0.5f, 0.04f));
            foreach (float y in new[] { 0.9f, 2.3f }) b.Box(new Vector3(heel, y, 0f), new Vector3(0.212f, 0.045f, 0.212f), Iron);
            b.Box(new Vector3(heel - 0.212f, 2.75f, 0f), new Vector3(0.012f, 0.15f, 0.17f), Pale);
            b.Box(new Vector3(heel - 0.226f, 2.78f, 0f), new Vector3(0.004f, 0.025f, 0.12f), Hall.Ink);
            b.Box(new Vector3(heel - 0.226f, 2.7f, -0.02f), new Vector3(0.004f, 0.02f, 0.08f), Hall.Ink);
            LanternArm(b, new Vector3(heel - 0.2f, 3.5f, 0f), -Vector3.UnitX, 0.85f, lights, 40f, 13f);

            // What a stall still has in it, on the side the camera sees.
            if (variant == 0)
            {
                VertexMaterial strap = Hall.Leather(72, 44, 28);
                Vector3 hook = new Vector3(0.9f, 2.25f, 0.1f);
                b.Beam(hook - Vector3.UnitZ * 0.04f, hook + new Vector3(0f, 0.04f, 0.14f), 0.02f, Iron);
                foreach (float s in new[] { -1f, 1f })
                {
                    b.Beam(hook + Vector3.UnitZ * 0.1f, hook + new Vector3(s * 0.26f, -0.7f, 0.12f), 0.018f, strap);
                    b.Beam(hook + new Vector3(s * 0.26f, -0.7f, 0.12f), hook + new Vector3(s * 0.1f, -1.3f, 0.12f), 0.018f, strap);
                }
                b.Beam(hook + new Vector3(-0.28f, -0.7f, 0.13f), hook + new Vector3(0.28f, -0.7f, 0.13f), 0.022f, Iron);
                for (int k = 0; k < 6; k++)
                {
                    float a0 = MathF.PI * (1.12f + k * 0.126f), a1 = MathF.PI * (1.12f + (k + 1) * 0.126f);
                    Vector3 shoe = new Vector3(-0.7f, 1.9f, 0.09f);
                    b.Beam(shoe + new Vector3(MathF.Cos(a0) * 0.16f, MathF.Sin(a0) * 0.18f, 0f), shoe + new Vector3(MathF.Cos(a1) * 0.16f, MathF.Sin(a1) * 0.18f, 0f), 0.024f, Hall.Iron(96));
                }
            }
            else
            {
                // A horse blanket folded over the top, where it was always thrown.
                Vector3 over = Vector3.Lerp(low, high, 0.3f);
                b.Box(over + Vector3.UnitY * 0.1f, new Vector3(0.5f, 0.035f, 0.17f), Dyed(Saffron));
                foreach (float s in new[] { -1f, 1f })
                {
                    b.Box(over + new Vector3(0f, -0.26f, s * 0.15f), new Vector3(0.5f, 0.4f, 0.02f), Dyed(Saffron));
                    b.Box(over + new Vector3(0f, -0.5f, s * 0.172f), new Vector3(0.5f, 0.05f, 0.006f), Dyed(Blue));
                    b.Box(over + new Vector3(0f, -0.36f, s * 0.172f), new Vector3(0.5f, 0.02f, 0.006f), Dyed(Blue));
                }
            }
        }

        // The ladder to the loft: two poles and their rungs, its foot out in the room (+z) and its head a little over the loft's edge.
        private static void Ladder(MeshBuilder b, Vector2 half, float h)
        {
            Vector3 foot = new Vector3(0f, 0f, half.Y - 0.05f), head = new Vector3(0f, h + 0.75f, -half.Y - 0.06f);
            VertexMaterial pole = Hall.Timber(168, 132, 90);
            foreach (float s in new[] { -0.45f, 0.45f }) b.Tube(foot + Vector3.UnitX * s, head + Vector3.UnitX * s, 0.06f, 0.05f, 6, pole);
            const int Rungs = 13;
            for (int i = 1; i <= Rungs; i++)
            {
                Vector3 at = Vector3.Lerp(foot, head, i / (Rungs + 1.5f));
                b.Tube(at - Vector3.UnitX * 0.5f, at + Vector3.UnitX * 0.5f, 0.036f, 0.036f, 5, pole);
            }
        }

        // Where blades are left at the door: a barrel with swords, an axe, a spear and a bow stood in it, and a tag hung on it to say so.
        private static void BladeBarrel(MeshBuilder b, float r)
        {
            VertexMaterial staves = Hall.Wood(138, 98, 62);
            float radius = r * 0.86f;
            b.Tube(Vector3.Zero, Vector3.UnitY * 0.65f, radius * 0.86f, radius, 12, staves, capped: false);
            b.Tube(Vector3.UnitY * 0.65f, Vector3.UnitY * 1.3f, radius, radius * 0.88f, 12, staves, capped: false);
            b.Tube(Vector3.UnitY * 0.98f, Vector3.UnitY * 1f, radius * 0.9f, radius * 0.9f, 12, Soot);
            foreach (float y in new[] { 0.16f, 0.62f, 1.16f }) b.Tube(Vector3.UnitY * (y - 0.04f), Vector3.UnitY * (y + 0.04f), radius + 0.012f, radius + 0.012f, 12, Hall.Iron(84), capped: false);
            VertexMaterial steel = Hall.Metal(180, 182, 188);
            VertexMaterial grip = Hall.Leather(72, 44, 28);
            void Sword(Vector3 point, Vector3 lean, float length)
            {
                Vector3 up = (Vector3.UnitY + lean).Normalized();
                Vector3 guard = point + up * length;
                Vector3 across = Vector3.Cross(up, Vector3.UnitZ).Normalized();
                Hall.Board(b, point, guard, across, 0.05f, 0.012f, steel);
                Hall.Board(b, guard - across * 0.22f, guard + across * 0.22f, up, 0.03f, 0.03f, Hall.Iron(90));
                b.Tube(guard, guard + up * 0.42f, 0.035f, 0.035f, 6, grip);
                b.Tube(guard + up * 0.42f, guard + up * 0.52f, 0.06f, 0.04f, 6, Hall.Iron(90));
            }
            Sword(new Vector3(-0.12f, 0.5f, 0.1f), new Vector3(-0.16f, 0f, 0.1f), 1.55f);
            Sword(new Vector3(0.14f, 0.5f, -0.08f), new Vector3(0.2f, 0f, -0.04f), 1.4f);
            // An axe, its head up, and a spear.
            Vector3 haft = new Vector3(0.1f, 0.4f, 0.16f), axe = haft + new Vector3(0.24f, 2.1f, 0.2f);
            b.Tube(haft, axe, 0.035f, 0.035f, 6, Hall.Timber(160, 120, 78));
            Hall.Board(b, axe - new Vector3(0f, 0.16f, 0f), axe + new Vector3(0.36f, -0.14f, 0f), Vector3.UnitY, 0.2f, 0.016f, steel);
            Vector3 butt = new Vector3(-0.1f, 0.4f, -0.14f), tip = butt + new Vector3(-0.3f, 3.1f, -0.2f);
            b.Tube(butt, tip, 0.032f, 0.028f, 6, Hall.Timber(160, 120, 78));
            b.Tube(tip, tip + (tip - butt).Normalized() * 0.42f, 0.07f, 0.004f, 4, steel);
            // A bow, unstrung at one end.
            Vector3 bow = new Vector3(0.02f, 0.5f, -0.02f);
            Vector3 previous = bow;
            for (int i = 1; i <= 6; i++)
            {
                float t = i / 6f;
                Vector3 next = bow + new Vector3(0.34f * MathF.Sin(t * MathF.PI) - 0.1f * t, 2.2f * t, -0.22f * t);
                b.Tube(previous, next, 0.028f, 0.026f, 5, Hall.Timber(132, 92, 56), capped: false);
                previous = next;
            }
            // The tag.
            b.Box(new Vector3(0f, 0.9f, radius + 0.03f), new Vector3(0.26f, 0.17f, 0.014f), Pale);
            b.Box(new Vector3(0f, 0.94f, radius + 0.046f), new Vector3(0.16f, 0.022f, 0.004f), Hall.Ink);
            b.Box(new Vector3(-0.03f, 0.86f, radius + 0.046f), new Vector3(0.11f, 0.018f, 0.004f), Hall.Ink);
        }

        // The house's rules chalked on a slate, on an easel that faces whoever comes in.
        private static void Easel(MeshBuilder b, float h)
        {
            VertexMaterial wood = Hall.Timber(136, 98, 62);
            foreach (float s in new[] { -1f, 1f }) Hall.Board(b, new Vector3(s * 0.5f, 0f, 0.34f), new Vector3(s * 0.14f, h - 0.1f, -0.04f), Vector3.UnitX, 0.05f, 0.03f, wood);
            Hall.Board(b, new Vector3(0f, 0f, -0.5f), new Vector3(0f, h - 0.2f, -0.06f), Vector3.UnitX, 0.05f, 0.03f, wood);
            Vector3 foot = new Vector3(0f, 0.95f, 0.26f), headOf = new Vector3(0f, 2.85f, 0.05f);
            Vector3 up = (headOf - foot).Normalized();
            Vector3 face = Vector3.Cross(Vector3.UnitX, up).Normalized();
            if (face.Z < 0f) face = -face;
            Hall.Board(b, foot, headOf, Vector3.UnitX, 0.64f, 0.03f, Walnut);
            Hall.Board(b, foot + up * 0.08f + face * 0.034f, headOf - up * 0.08f + face * 0.034f, Vector3.UnitX, 0.57f, 0.006f, Slate);
            Hall.Board(b, foot + face * 0.1f - Vector3.UnitX * 0.66f, foot + face * 0.1f + Vector3.UnitX * 0.66f, face, 0.09f, 0.025f, wood);
            b.Box(foot + face * 0.12f + new Vector3(0.3f, 0.04f, 0f), new Vector3(0.07f, 0.014f, 0.014f), Chalk);
            // A heading, and under it a number and a line for each rule.
            Vector3 On(float along, float across) => foot + up * along + Vector3.UnitX * across + face * 0.044f;
            Hall.Board(b, On(1.62f, -0.34f), On(1.62f, 0.34f), up, 0.05f, 0.004f, Chalk);
            Hall.Board(b, On(1.5f, -0.44f), On(1.5f, 0.44f), up, 0.008f, 0.004f, Chalk);
            for (int line = 0; line < 5; line++)
            {
                float along = 1.3f - line * 0.24f;
                Hall.Board(b, On(along - 0.05f, -0.44f), On(along + 0.05f, -0.44f), Vector3.UnitX, 0.012f, 0.004f, Chalk);
                Hall.Board(b, On(along, -0.32f), On(along, 0.1f + (line * 37 % 5) * 0.08f), up, 0.022f, 0.004f, Chalk);
            }
        }

        // Hay that never left: bales stacked by the door with a heap come loose beside them and a fork left in the top one.
        private static void HayStack(MeshBuilder b, Vector2 half, WorldRandom random)
        {
            Bale(b, new Vector3(-0.3f, 0f, -0.5f), 0.04f, random);
            Bale(b, new Vector3(-0.22f, 0f, 0.62f), -0.06f, random);
            Bale(b, new Vector3(-0.1f, 0.96f, 0.02f), 0.5f, random);
            HayHeap(b, new Vector3(half.X - 0.5f, 0f, half.Y - 0.42f), 0.55f, 0.66f, random);
            Pitchfork(b, new Vector3(-0.95f, 4f, -0.25f), new Vector3(-0.4f, 2.22f, 0.02f));
        }
    }
}

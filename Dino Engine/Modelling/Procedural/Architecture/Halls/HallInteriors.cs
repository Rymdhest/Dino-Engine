using Dino_Engine.Modelling.Model;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Halls
{
    /// <summary>
    /// What's inside a hall: everything its design says stands in it, each where it says, and what dresses its walls and fills it with life, which is each hall's own
    /// (GamingHouse.Barn.cs, Smithy.Works.cs, Basilica.Order.cs). This is only drawn when someone's inside or near, as its roof hides it otherwise; its lights always burn.
    /// Nothing tall stands against the wall facing the camera, which comes down when someone's inside.
    /// </summary>
    internal static class HallInteriors
    {
        private const float FloorTop = Hall.FloorTop;

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
                // A piece says where its lights are in its own frame, as it does its shapes.
                Hall.At(b, new Vector3(p.At.X, FloorTop, p.At.Y), p.Yaw, lights, () =>
                {
                    if (d.Kind == HallKind.GamingHouse) GamingHouse.Piece(b, p, kit, lights, pieceRandom);
                    else if (d.Kind == HallKind.Forge) Smithy.Piece(b, p, kit, lights, pieceRandom);
                    else Basilica.Piece(b, p, kit, lights, pieceRandom);
                });
            }

            switch (d.Kind)
            {
                case HallKind.Forge:
                    WallPosts(b, d, kit, 4f);
                    Smithy.Dress(b, d, lights, random);
                    break;
                case HallKind.GamingHouse:
                    GamingHouse.Dress(b, d, lights, random);
                    break;
                default:
                    Basilica.Dress(b, d, lights, random);
                    break;
            }
        }

        // A point on the inside of a wall, `out_` metres into the room from it.
        internal static Vector3 Inside(HallDesign d, HallSide side, float along, float y, float out_ = 0f)
        {
            var s = new HallWalls.Side(d, side);
            return s.Outer(along, y, d.Wall + out_);
        }

        // Which way is into the room from a wall.
        internal static Vector3 Into(HallSide side) => side switch
        {
            HallSide.Front => -Vector3.UnitZ,
            HallSide.Back => Vector3.UnitZ,
            HallSide.PlusX => -Vector3.UnitX,
            _ => Vector3.UnitX,
        };

        // The stretches of a wall's inside free of its doors, and of windows coming down below `below`.
        internal static List<(float From, float To)> Clear(HallDesign d, HallSide side, float below, float margin)
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
    }
}

using Dino_Engine.Modelling.Model;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Halls
{
    /// <summary>
    /// The Archive's walls, dressed, and what hangs in it. Round the room a dado of marble, and under the cut of the walls an architrave, a frieze of teal and a cornice,
    /// so that the room is framed at its foot and its head. On the end wall, which the camera sees whole, a pilaster where each row of columns meets it, an arch on the wall
    /// behind each record that stands against it, and in the middle, over the dais, a great arch with the Haven's blossom in gold on a field of teal: what an altar would
    /// have behind it. On the side walls a banner over each record. And three rings of candles hung on chains, one over the nave inside the door and one over each aisle,
    /// which are most of its light after dark; each is hung where it lies over open floor as the camera sees it, which is a little to the north of where it hangs.
    /// Everything on a wall above the reach of a hand is seen at a slant from above, so it's bold and plain: arches, bands, one emblem.
    /// </summary>
    internal static partial class Basilica
    {
        private const float Dado = 2.2f;
        private const float FloorTop = Hall.FloorTop;

        public static void Dress(MeshBuilder b, HallDesign d, List<AssetLight> lights, WorldRandom random)
        {
            Wainscot(b, d);
            Entablature(b, d);
            foreach (float x in new[] { -HallDesigns.ArchiveArcade, HallDesigns.ArchiveArcade }) Pilaster(b, d, HallSide.Back, x);
            foreach (HallSide side in new[] { HallSide.PlusX, HallSide.MinusX })
            {
                foreach (float z in new[] { -11.3f, 11.3f }) Pilaster(b, d, side, z);
            }

            foreach (HallPiece piece in d.Pieces.Where(p => p.Kind == PieceKind.Plinth))
            {
                var record = (ExhibitKind)piece.Variant;
                if (record == ExhibitKind.TyrantsFelled) continue;
                if (piece.Yaw == 0f) Arch(b, d, HallSide.Back, piece.At.X, 1.76f, 5.3f, 0.24f, 0.12f, White, Gilt);
                else Banner(b, d, piece.At.X > 0f ? HallSide.PlusX : HallSide.MinusX, piece.At.Y, d.WallTop - 1.35f, 3f);
            }
            Reredos(b, d);

            // The rings of candles.
            float hung = d.WallTop - 0.08f;
            Corona(b, lights, new Vector3(0f, 6.9f, 9.2f), 1.5f, 12, hung);
            foreach (float s in new[] { -1f, 1f }) Corona(b, lights, new Vector3(s * 13.6f, 6.6f, 2.8f), 1.7f, 14, hung);
            // The candles on the columns, as one light to each pair of them, in the middle of the nave and well clear of the marble.
            foreach (float z in HallDesigns.ArchiveBays.Skip(1))
            {
                lights.Add(AssetLight.Candles(new Vector3(0f, 6.4f, z)) with { Intensity = 60f, Radius = 15f, Shadow = false, Flicker = 0.12f });
            }
        }

        private static Vector3 OnWall(HallDesign d, HallSide side, float along, float y, float proud) => HallInteriors.Inside(d, side, along, FloorTop + y, proud);

        // A course along a wall: from one height to another, standing `proud` of it.
        private static void Course(MeshBuilder b, HallDesign d, HallSide side, float from, float to, float y0, float y1, float proud, VertexMaterial material)
        {
            var s = new HallWalls.Side(d, side);
            HallDressing.Box(b, OnWall(d, side, (from + to) * 0.5f, (y0 + y1) * 0.5f, proud * 0.5f), s.AlongDir, HallInteriors.Into(side), (to - from) * 0.5f, (y1 - y0) * 0.5f, proud * 0.5f, material);
        }

        // The dado: a foot of teal, a line of gold, white marble to the height of a hip, and a rail. The wall facing the camera keeps it under its cut.
        private static void Wainscot(MeshBuilder b, HallDesign d)
        {
            foreach (HallSide side in Enum.GetValues<HallSide>())
            {
                float top = side == HallSide.Front ? d.FrontCut - 0.12f : Dado;
                foreach ((float from, float to) in HallInteriors.Clear(d, side, top + 0.1f, 0.12f))
                {
                    if (to - from < 0.4f) continue;
                    Course(b, d, side, from, to, 0f, 0.36f, 0.1f, Teal);
                    Course(b, d, side, from, to, 0.36f, 0.42f, 0.08f, Gilt);
                    Course(b, d, side, from, to, 0.42f, top - 0.2f, 0.05f, White);
                    Course(b, d, side, from, to, top - 0.24f, top - 0.2f, 0.09f, Gilt);
                    Course(b, d, side, from, to, top - 0.2f, top, 0.12f, White);
                }
            }
        }

        // Under the cut of the walls: an architrave, a frieze of teal with a stud of gold every couple of metres, and a cornice.
        private static void Entablature(MeshBuilder b, HallDesign d)
        {
            float top = d.WallTop - 0.14f;
            foreach (HallSide side in new[] { HallSide.Back, HallSide.PlusX, HallSide.MinusX })
            {
                var s = new HallWalls.Side(d, side);
                float half = s.InnerHalf;
                Course(b, d, side, -half, half, top - 1.06f, top - 0.9f, 0.1f, White);
                Course(b, d, side, -half, half, top - 0.9f, top - 0.86f, 0.12f, Gilt);
                Course(b, d, side, -half, half, top - 0.86f, top - 0.34f, 0.06f, Teal);
                Course(b, d, side, -half, half, top - 0.34f, top - 0.06f, 0.2f, White);
                Course(b, d, side, -half, half, top - 0.06f, top, 0.24f, Gilt);
                int studs = (int)(half / 1f);
                for (int i = -studs; i <= studs; i++)
                {
                    float along = i * (half - 0.5f) / studs;
                    HallDressing.Box(b, OnWall(d, side, along, top - 0.6f, 0.075f), s.AlongDir, HallInteriors.Into(side), 0.09f, 0.09f, 0.02f, Gilt);
                }
            }
        }

        // Half a column against a wall, where a row of columns meets it or the wall wants a pier: a foot, a shaft, a necking and a capital, under the architrave.
        private static void Pilaster(MeshBuilder b, HallDesign d, HallSide side, float along)
        {
            float head = d.WallTop - 1.2f;
            Course(b, d, side, along - 0.6f, along + 0.6f, 0f, 0.5f, 0.32f, Teal);
            Course(b, d, side, along - 0.56f, along + 0.56f, 0.5f, 0.56f, 0.3f, Gilt);
            Course(b, d, side, along - 0.54f, along + 0.54f, 0.56f, 0.86f, 0.28f, White);
            Course(b, d, side, along - 0.45f, along + 0.45f, 0.86f, head - 0.74f, 0.2f, White);
            Course(b, d, side, along - 0.48f, along + 0.48f, head - 0.74f, head - 0.64f, 0.23f, Gilt);
            Course(b, d, side, along - 0.5f, along + 0.5f, head - 0.64f, head - 0.2f, 0.26f, White);
            Course(b, d, side, along - 0.62f, along + 0.62f, head - 0.2f, head, 0.32f, White);
            var s = new HallWalls.Side(d, side);
            foreach (float leaf in new[] { -0.3f, 0f, 0.3f })
            {
                HallDressing.Box(b, OnWall(d, side, along + leaf, head - 0.42f, 0.275f), s.AlongDir, HallInteriors.Into(side), 0.1f, 0.2f, 0.02f, Gilt);
            }
        }

        // An arch on the face of a wall: two jambs up from the dado and a half round over them, a line of a second marble inside it and a keystone at its crown.
        private static void Arch(MeshBuilder b, HallDesign d, HallSide side, float along, float halfWidth, float spring, float ring, float proud, VertexMaterial stone, VertexMaterial line)
        {
            var s = new HallWalls.Side(d, side);
            Vector3 into = HallInteriors.Into(side);
            foreach (float e in new[] { -1f, 1f })
            {
                float at = along + e * (halfWidth + ring * 0.5f);
                Course(b, d, side, at - ring * 0.5f, at + ring * 0.5f, Dado, spring, proud, stone);
                Course(b, d, side, at - ring * 0.5f - 0.08f, at + ring * 0.5f + 0.08f, spring - 0.14f, spring + 0.06f, proud + 0.05f, stone);
            }
            int segments = Math.Max(12, (int)(halfWidth * 7f));
            Vector3 On(float angle, float radius, float out_) => OnWall(d, side, along + MathF.Cos(angle) * radius, spring + 0.06f + MathF.Sin(angle) * radius, out_);
            for (int i = 0; i < segments; i++)
            {
                float a0 = MathF.PI * i / segments, a1 = MathF.PI * (i + 1) / segments;
                Vector3 radial = s.AlongDir * MathF.Cos((a0 + a1) * 0.5f) + Vector3.UnitY * MathF.Sin((a0 + a1) * 0.5f);
                Hall.Board(b, On(a0, halfWidth + ring * 0.5f, proud * 0.5f), On(a1, halfWidth + ring * 0.5f, proud * 0.5f), radial, ring * 0.5f, proud * 0.5f, stone);
                Hall.Board(b, On(a0, halfWidth - 0.1f, 0.02f), On(a1, halfWidth - 0.1f, 0.02f), radial, 0.03f, 0.02f, line);
            }
            Course(b, d, side, along - ring * 0.42f, along + ring * 0.42f, spring + 0.06f + halfWidth - 0.06f, spring + 0.06f + halfWidth + ring + 0.1f, proud + 0.07f, line);
        }

        /// <summary>
        /// What's on the end wall over the dais: a great arch of white marble, its field of teal, and on the field the Haven's blossom in gold, five petals round a heart
        /// with a ray between each, as wide as the skull under it is long.
        /// </summary>
        private static void Reredos(MeshBuilder b, HallDesign d)
        {
            const float Half = 3.3f, Spring = 4.7f, Ring = 0.42f;
            HallSide side = HallSide.Back;
            Arch(b, d, side, 0f, Half, Spring, Ring, 0.2f, White, Gilt);
            // Its field.
            float crown = Spring + 0.06f;
            Course(b, d, side, -Half, Half, Dado, crown, 0.03f, DeepTeal);
            const int Segments = 28;
            Vector3 centre = OnWall(d, side, 0f, crown, 0.03f);
            for (int i = 0; i < Segments; i++)
            {
                float a0 = MathF.PI * i / Segments, a1 = MathF.PI * (i + 1) / Segments;
                Hall.Tri(b, centre, OnWall(d, side, MathF.Cos(a0) * Half, crown + MathF.Sin(a0) * Half, 0.03f), OnWall(d, side, MathF.Cos(a1) * Half, crown + MathF.Sin(a1) * Half, 0.03f), Vector3.UnitZ, DeepTeal);
            }

            // The blossom.
            Vector3 heart = OnWall(d, side, 0f, 6.35f, 0.05f);
            Vector3 lift = Vector3.UnitZ * 0.03f;
            for (int i = 0; i < 5; i++)
            {
                float a = MathF.Tau * i / 5f + MathF.PI * 0.5f;
                Vector3 out_ = new Vector3(MathF.Cos(a), MathF.Sin(a), 0f), across = new Vector3(-out_.Y, out_.X, 0f);
                // A ray between this petal and the next.
                float between = a + MathF.Tau / 10f;
                Vector3 ray = new Vector3(MathF.Cos(between), MathF.Sin(between), 0f), rayAcross = new Vector3(-ray.Y, ray.X, 0f);
                Hall.Tri(b, heart + ray * 0.45f + rayAcross * 0.08f, heart + ray * 0.45f - rayAcross * 0.08f, heart + ray * 1.75f, Vector3.UnitZ, PaleGilt);
                // The petal: a leaf, notched at its tip.
                Vector3 foot = heart + lift + out_ * 0.3f;
                Vector3[] leaf =
                {
                    foot, foot + out_ * 0.34f - across * 0.38f, foot + out_ * 0.8f - across * 0.45f, foot + out_ * 1.16f - across * 0.25f, foot + out_ * 1.06f,
                    foot + out_ * 1.16f + across * 0.25f, foot + out_ * 0.8f + across * 0.45f, foot + out_ * 0.34f + across * 0.38f,
                };
                for (int k = 1; k + 1 < leaf.Length; k++) Hall.Tri(b, leaf[0], leaf[k], leaf[k + 1], Vector3.UnitZ, Gilt);
                Hall.Tri(b, foot + lift + out_ * 0.14f - across * 0.03f, foot + lift + out_ * 0.14f + across * 0.03f, foot + lift + out_ * 0.78f, Vector3.UnitZ, PaleGilt);
            }
            b.Tube(heart + lift, heart + lift * 3f, 0.36f, 0.3f, 16, White);
            b.Tube(heart + lift * 3f, heart + lift * 4f, 0.16f, 0.12f, 12, Gilt);
        }

        // A banner over a record on a side wall: the Haven's teal between borders of saffron, its blossom on it, cut to a point at its foot, on a rod of gold.
        private static void Banner(MeshBuilder b, HallDesign d, HallSide side, float along, float top, float length)
        {
            var s = new HallWalls.Side(d, side);
            Vector3 into = HallInteriors.Into(side);
            const float Half = 0.66f, Out = 0.34f;
            b.Beam(OnWall(d, side, along - Half - 0.2f, top, Out), OnWall(d, side, along + Half + 0.2f, top, Out), 0.045f, Gilt);
            foreach (float e in new[] { -1f, 1f })
            {
                b.Beam(OnWall(d, side, along + e * (Half + 0.08f), top, 0f), OnWall(d, side, along + e * (Half + 0.08f), top, Out + 0.04f), 0.03f, Gilt);
                b.Box(OnWall(d, side, along + e * (Half + 0.24f), top, Out), new Vector3(0.07f), Gilt);
            }
            Vector3 head = OnWall(d, side, along, top - 0.02f, Out), foot = OnWall(d, side, along, top - length, Out);
            VertexMaterial cloth = Hall.Cloth(26, 94, 98), border = Hall.Cloth(190, 132, 44), petal = Hall.Cloth(206, 150, 160);
            Hall.Board(b, head, foot, s.AlongDir, Half, 0.012f, cloth);
            foreach (float e in new[] { -1f, 1f })
            {
                Hall.Board(b, head + s.AlongDir * (e * (Half - 0.09f)) + into * 0.014f, foot + s.AlongDir * (e * (Half - 0.09f)) + into * 0.014f, s.AlongDir, 0.05f, 0.004f, border);
            }
            // Its point.
            Vector3 l = foot - s.AlongDir * Half, r = foot + s.AlongDir * Half, p = foot - Vector3.UnitY * 0.62f;
            Hall.Tri(b, l + into * 0.012f, r + into * 0.012f, p + into * 0.012f, into, cloth);
            Hall.Tri(b, l - into * 0.012f, r - into * 0.012f, p - into * 0.012f, -into, cloth);
            b.Box(p - Vector3.UnitY * 0.07f, new Vector3(0.05f, 0.09f, 0.05f), border);
            // The blossom.
            Vector3 heart = Vector3.Lerp(head, foot, 0.5f) + into * 0.02f;
            for (int i = 0; i < 5; i++)
            {
                float a = MathF.Tau * i / 5f + MathF.PI * 0.5f;
                Vector3 out_ = s.AlongDir * MathF.Cos(a) + Vector3.UnitY * MathF.Sin(a), across = s.AlongDir * -MathF.Sin(a) + Vector3.UnitY * MathF.Cos(a);
                Hall.Face(b, heart + out_ * 0.08f, heart + out_ * 0.25f + across * 0.14f, heart + out_ * 0.42f, heart + out_ * 0.25f - across * 0.14f, into, petal);
            }
            Hall.Face(b, heart + into * 0.004f - s.AlongDir * 0.07f - Vector3.UnitY * 0.07f, heart + into * 0.004f + s.AlongDir * 0.07f - Vector3.UnitY * 0.07f, heart + into * 0.004f + s.AlongDir * 0.07f + Vector3.UnitY * 0.07f,
                heart + into * 0.004f - s.AlongDir * 0.07f + Vector3.UnitY * 0.07f, into, border);
        }

        /// <summary>
        /// A ring of candles hung on chains: a hoop of gold as wide as a cart, a candle in a cup every hand's breadth round it, spokes to a boss in its middle, and four
        /// chains up to one. Its light is under its middle, clear of the hoop, so it shadows nothing it lights.
        /// </summary>
        private static void Corona(MeshBuilder b, List<AssetLight> lights, Vector3 centre, float radius, int candles, float hungFrom)
        {
            const int Segments = 28;
            Vector3 P(float a, float r) => centre + new Vector3(MathF.Cos(a) * r, 0f, MathF.Sin(a) * r);
            for (int i = 0; i < Segments; i++)
            {
                float a0 = MathF.Tau * i / Segments, a1 = MathF.Tau * (i + 1) / Segments;
                Hall.Board(b, P(a0, radius), P(a1, radius), Vector3.UnitY, 0.11f, 0.03f, Gilt);
                Hall.Board(b, P(a0, radius + 0.035f) - Vector3.UnitY * 0.12f, P(a1, radius + 0.035f) - Vector3.UnitY * 0.12f, Vector3.UnitY, 0.025f, 0.05f, PaleGilt);
                Hall.Board(b, P(a0, radius + 0.035f) + Vector3.UnitY * 0.12f, P(a1, radius + 0.035f) + Vector3.UnitY * 0.12f, Vector3.UnitY, 0.02f, 0.045f, PaleGilt);
            }
            b.Tube(centre - Vector3.UnitY * 0.14f, centre + Vector3.UnitY * 0.1f, 0.2f, 0.17f, 10, Gilt);
            b.Tube(centre - Vector3.UnitY * 0.5f, centre - Vector3.UnitY * 0.14f, 0.03f, 0.2f, 10, Gilt, capped: false);
            for (int k = 0; k < 4; k++)
            {
                float a = MathF.Tau * k / 4f + MathF.PI * 0.25f;
                b.Beam(P(a, 0.17f), P(a, radius - 0.02f), 0.024f, Gilt);
            }
            for (int i = 0; i < candles; i++)
            {
                Vector3 at = P(MathF.Tau * i / candles, radius) + Vector3.UnitY * 0.11f;
                b.Tube(at, at + Vector3.UnitY * 0.05f, 0.08f, 0.11f, 6, Gilt);
                Hall.Candle(b, at + Vector3.UnitY * 0.05f, 0.44f, 0.05f, lights);
            }
            Vector3 gather = centre + Vector3.UnitY * (radius * 1.35f);
            for (int k = 0; k < 4; k++) b.Beam(P(MathF.Tau * k / 4f, radius) + Vector3.UnitY * 0.1f, gather, 0.016f, Silver);
            b.Tube(gather - Vector3.UnitY * 0.07f, gather + Vector3.UnitY * 0.07f, 0.08f, 0.08f, 6, Gilt);
            b.Beam(gather, new Vector3(centre.X, hungFrom, centre.Z), 0.02f, Silver);
            lights.Add(AssetLight.CandleStand(centre - Vector3.UnitY * 1.15f) with { Intensity = 115f, Radius = 26f, Fill = 0.3f, Flicker = 0.1f });
        }
    }
}

using Dino_Engine.Modelling.Model;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Halls
{
    /// <summary>
    /// The walls of a hall, with holes right through them for its doors and windows, so that the sun and the sky come in by them as they would (the sun's shadows and global illumination
    /// both see the hall whole). Each wall is built in courses, split where it's cut when someone's inside (<see cref="HallDesign.FrontCut"/> and <see cref="HallDesign.WallTop"/>), so that a
    /// cut wall has a real top and not a hollow. Every piece of a face shares that face's texture and edges (<see cref="MeshBuilder.Panel"/>), so a wall broken by windows shows no seams.
    /// </summary>
    internal static class HallWalls
    {
        private const float Below = 2.5f;           // Metres below the floor the outside of a wall goes, so a slope never shows daylight under it.
        private const float PlinthProud = 0.14f;
        private const int ArchSegments = 12;

        /// <summary>One wall, laid out: its outer face, which way along it is, and how far it runs.</summary>
        internal readonly struct Side
        {
            public readonly HallSide Which;
            public readonly Vector3 Out;
            public readonly Vector3 U;              // Along the outer face, left to right seen from outside.
            public readonly bool AlongIsU;          // Whether the design's "along" grows the way U does.
            public readonly Vector3 OuterOrigin;    // On the outer face, at along 0 and the floor.
            public readonly Vector3 InnerOrigin;
            public readonly float RunFrom, RunTo;   // How far its own wall runs, along.
            public readonly float FaceHalf;         // Half the width of this whole side of the building, corners and all.
            public readonly float InnerHalf;        // Half the width of its inner face.

            public Side(HallDesign d, HallSide which)
            {
                Which = which;
                Vector2 h = d.Half;
                float t = d.Wall;
                switch (which)
                {
                    case HallSide.Front:
                        Out = Vector3.UnitZ; U = Vector3.UnitX; AlongIsU = true;
                        OuterOrigin = new Vector3(0f, 0f, h.Y); InnerOrigin = new Vector3(0f, 0f, h.Y - t);
                        RunFrom = -h.X; RunTo = h.X; FaceHalf = h.X; InnerHalf = h.X - t;
                        break;
                    case HallSide.Back:
                        Out = -Vector3.UnitZ; U = -Vector3.UnitX; AlongIsU = false;
                        OuterOrigin = new Vector3(0f, 0f, -h.Y); InnerOrigin = new Vector3(0f, 0f, -h.Y + t);
                        RunFrom = -h.X; RunTo = h.X; FaceHalf = h.X; InnerHalf = h.X - t;
                        break;
                    case HallSide.PlusX:
                        Out = Vector3.UnitX; U = -Vector3.UnitZ; AlongIsU = false;
                        OuterOrigin = new Vector3(h.X, 0f, 0f); InnerOrigin = new Vector3(h.X - t, 0f, 0f);
                        RunFrom = -h.Y + t; RunTo = h.Y - t; FaceHalf = h.Y; InnerHalf = h.Y - t;
                        break;
                    default:
                        Out = -Vector3.UnitX; U = Vector3.UnitZ; AlongIsU = true;
                        OuterOrigin = new Vector3(-h.X, 0f, 0f); InnerOrigin = new Vector3(-h.X + t, 0f, 0f);
                        RunFrom = -h.Y + t; RunTo = h.Y - t; FaceHalf = h.Y; InnerHalf = h.Y - t;
                        break;
                }
            }

            /// <summary>The design's "along" as a point on the line of the wall, at no depth.</summary>
            public Vector3 AlongDir => AlongIsU ? U : -U;

            /// <summary>A point on the outer face, <paramref name="depth"/> metres into the wall.</summary>
            public Vector3 Outer(float along, float y, float depth = 0f) => OuterOrigin + AlongDir * along + Vector3.UnitY * y - Out * depth;

            /// <summary>The u of a run along the outer face, least first.</summary>
            public (float u0, float u1) OuterU(float a0, float a1) => AlongIsU ? (a0, a1) : (-a1, -a0);

            /// <summary>The u of a run along the inner face, whose u goes the other way.</summary>
            public (float u0, float u1) InnerU(float a0, float a1) => AlongIsU ? (-a1, -a0) : (a0, a1);
        }

        public static void Build(MeshBuilder b, HallDesign d, HallKit kit)
        {
            foreach (HallSide which in Enum.GetValues<HallSide>())
            {
                var side = new Side(d, which);
                List<HallOpening> openings = d.Openings.Where(o => o.Side == which).ToList();
                BuildCourse(b, d, kit, side, openings, 0f, d.FrontCut, kit.Dressing);
                BuildCourse(b, d, kit, side, openings, d.FrontCut, d.WallTop, kit.Framed ? kit.Timber : kit.Dressing);
                BuildPlinth(b, d, kit, side, openings);
                foreach (HallOpening opening in openings) BuildReveals(b, d, kit, side, opening);
            }
            BuildEnds(b, d, kit);
        }

        /// <summary>The top of an opening's straight sides: its head, or where its arch springs from.</summary>
        public static float Springing(in HallOpening o) => o.Arched ? o.Head - o.HalfWidth : o.Head;

        // One course of one wall, from y0 to y1, as a grid round its openings: every cell that isn't open is a piece of the outer face and of the inner face, and the course has a top.
        private static void BuildCourse(MeshBuilder b, HallDesign d, HallKit kit, Side side, List<HallOpening> openings, float y0, float y1, VertexMaterial cap)
        {
            var columns = new List<float> { side.RunFrom, side.RunTo };
            var rows = new List<float> { y0, y1 };
            foreach (HallOpening o in openings)
            {
                columns.Add(Math.Clamp(o.Along - o.HalfWidth, side.RunFrom, side.RunTo));
                columns.Add(Math.Clamp(o.Along + o.HalfWidth, side.RunFrom, side.RunTo));
                foreach (float y in new[] { o.Sill, Springing(o), o.Head }) if (y > y0 && y < y1) rows.Add(y);
            }
            columns = columns.Distinct().OrderBy(x => x).ToList();
            rows = rows.Distinct().OrderBy(y => y).ToList();

            float outerFrom = MathF.Max(y0, kit.PlinthTop);
            Vector4 outerBounds = new Vector4(-side.FaceHalf, side.FaceHalf, -Below, d.WallTop);
            Vector4 innerBounds = new Vector4(-side.InnerHalf, side.InnerHalf, 0f, d.WallTop);

            for (int i = 0; i + 1 < columns.Count; i++)
            {
                float a0 = columns[i], a1 = columns[i + 1];
                if (a1 - a0 < 1e-3f) continue;
                float middle = (a0 + a1) * 0.5f;
                HallOpening? over = openings.FirstOrDefault(o => middle > o.Along - o.HalfWidth && middle < o.Along + o.HalfWidth);
                bool inOpening = openings.Any(o => middle > o.Along - o.HalfWidth && middle < o.Along + o.HalfWidth);

                for (int j = 0; j + 1 < rows.Count; j++)
                {
                    float ya = rows[j], yb = rows[j + 1];
                    float ymid = (ya + yb) * 0.5f;
                    if (inOpening && over is HallOpening o)
                    {
                        if (ymid > o.Sill && ymid < Springing(o)) continue;               // The hole itself.
                        if (o.Arched && ymid > Springing(o) && ymid < o.Head) continue;   // The arch, which is built round its curve below.
                    }
                    Face(b, side, kit.OuterWall, kit.InnerWall, a0, a1, ya, yb, outerFrom, outerBounds, innerBounds);
                }

                // The course's top, wherever no opening runs up through it.
                bool openAtTop = inOpening && over is HallOpening top && top.Sill < y1 && top.Head > y1;
                if (!openAtTop) Top(b, d, side, cap, a0, a1, y1);
            }

            foreach (HallOpening o in openings)
            {
                if (!o.Arched) continue;
                float spring = Springing(o);
                if (spring >= y1 || o.Head <= y0) continue;
                Arch(b, side, kit, o, MathF.Max(spring, y0), MathF.Min(o.Head, y1), outerFrom, outerBounds, innerBounds);
            }
        }

        // A cell of the wall, outside and in. The outside starts above the plinth, which stands proud in front of it.
        private static void Face(MeshBuilder b, Side side, VertexMaterial outer, VertexMaterial inner, float a0, float a1, float y0, float y1, float outerFrom, Vector4 outerBounds, Vector4 innerBounds)
        {
            float oy0 = MathF.Max(y0, outerFrom);
            if (y1 > oy0)
            {
                (float u0, float u1) = side.OuterU(a0, a1);
                b.Panel(side.OuterOrigin, side.U, Vector3.UnitY, u0, u1, oy0, y1, outer, outerBounds);
            }
            (float i0, float i1) = side.InnerU(a0, a1);
            b.Panel(side.InnerOrigin, -side.U, Vector3.UnitY, i0, i1, y0, y1, inner, innerBounds);
        }

        // The top of a course, across the thickness of the wall: what's left showing when what's above it is cut away.
        private static void Top(MeshBuilder b, HallDesign d, Side side, VertexMaterial material, float a0, float a1, float y)
        {
            Vector3 origin = side.Outer(0f, y);
            // Seen from above: u along the wall, v into it from the outside.
            Vector3 v = -side.Out;
            Vector3 u = Vector3.Cross(v, Vector3.UnitY);
            float s0 = Vector3.Dot(side.Outer(a0, y) - origin, u);
            float s1 = Vector3.Dot(side.Outer(a1, y) - origin, u);
            b.Panel(origin, u, v, MathF.Min(s0, s1), MathF.Max(s0, s1), 0f, d.Wall, material, new Vector4(MathF.Min(s0, s1), MathF.Max(s0, s1), 0f, d.Wall));
        }

        // Where an arch is cut through a wall: the wall round its curve, outside and in, and the underside of the arch, its intrados, through the wall's thickness.
        private static void Arch(MeshBuilder b, Side side, HallKit kit, HallOpening o, float from, float to, float outerFrom, Vector4 outerBounds, Vector4 innerBounds)
        {
            float spring = Springing(o);
            float r = o.HalfWidth;
            float depth = Vector3.Distance(side.OuterOrigin, side.InnerOrigin);
            for (int i = 0; i < ArchSegments; i++)
            {
                float t0 = MathF.PI * i / ArchSegments, t1 = MathF.PI * (i + 1) / ArchSegments;
                // From the right of the opening seen from outside, over the top, to its left.
                float a0 = o.Along + r * MathF.Cos(t0) * (side.AlongIsU ? 1f : -1f);
                float a1 = o.Along + r * MathF.Cos(t1) * (side.AlongIsU ? 1f : -1f);
                float y0 = spring + r * MathF.Sin(t0);
                float y1 = spring + r * MathF.Sin(t1);
                float lo0 = Math.Clamp(y0, from, to), lo1 = Math.Clamp(y1, from, to);

                // Outside: the wall between the curve and the head, as seen from outside, wound counter-clockwise.
                if (to > MathF.Min(lo0, lo1))
                {
                    Vector3 p0 = side.Outer(a0, lo0), p1 = side.Outer(a1, lo1), q1 = side.Outer(a1, to), q0 = side.Outer(a0, to);
                    if (side.AlongIsU ? a1 < a0 : a1 > a0) b.PlaneQuad(p1, p0, q0, q1, kit.OuterWall, side.OuterOrigin, side.U, Vector3.UnitY, outerBounds);
                    else b.PlaneQuad(p0, p1, q1, q0, kit.OuterWall, side.OuterOrigin, side.U, Vector3.UnitY, outerBounds);

                    Vector3 n0 = side.Outer(a0, lo0, depth), n1 = side.Outer(a1, lo1, depth), m1 = side.Outer(a1, to, depth), m0 = side.Outer(a0, to, depth);
                    // Inside, seen from the room, the same curve is wound the other way.
                    if (side.AlongIsU ? a1 < a0 : a1 > a0) b.PlaneQuad(n0, n1, m1, m0, kit.InnerWall, side.InnerOrigin, -side.U, Vector3.UnitY, innerBounds);
                    else b.PlaneQuad(n1, n0, m0, m1, kit.InnerWall, side.InnerOrigin, -side.U, Vector3.UnitY, innerBounds);
                }

                // The intrados, facing down into the opening.
                if (y0 >= from - 1e-3f && y1 >= from - 1e-3f && y0 <= to + 1e-3f && y1 <= to + 1e-3f)
                {
                    Vector3 o0 = side.Outer(a0, y0), o1 = side.Outer(a1, y1), i1 = side.Outer(a1, y1, depth), i0 = side.Outer(a0, y0, depth);
                    Vector3 normal = Vector3.Cross(o1 - o0, i0 - o0);
                    Vector3 centre = side.Outer(o.Along, spring, depth * 0.5f);
                    if (Vector3.Dot(normal, centre - o0) > 0f) b.Quad(o0, o1, i1, i0, kit.Dressing);
                    else b.Quad(o1, o0, i0, i1, kit.Dressing);
                }
            }
        }

        // The sides of an opening through the wall, its sill and its head.
        private static void BuildReveals(MeshBuilder b, HallDesign d, HallKit kit, Side side, HallOpening o)
        {
            float depth = d.Wall;
            float spring = Springing(o);
            float left = o.Along - o.HalfWidth, right = o.Along + o.HalfWidth;
            foreach ((float at, float inward) in new[] { (left, 1f), (right, -1f) })
            {
                Vector3 a = side.Outer(at, o.Sill), c = side.Outer(at, spring, depth);
                Vector3 p0 = side.Outer(at, o.Sill), p1 = side.Outer(at, o.Sill, depth), p2 = side.Outer(at, spring, depth), p3 = side.Outer(at, spring);
                // Faces into the opening.
                Vector3 normal = Vector3.Cross(p1 - p0, p3 - p0);
                Vector3 towards = side.AlongDir * inward;
                if (Vector3.Dot(normal, towards) > 0f) b.Quad(p0, p1, p2, p3, kit.Dressing);
                else b.Quad(p1, p0, p3, p2, kit.Dressing);
            }
            if (!o.Door)
            {
                // The sill, facing up.
                Vector3 s0 = side.Outer(left, o.Sill), s1 = side.Outer(right, o.Sill), s2 = side.Outer(right, o.Sill, depth), s3 = side.Outer(left, o.Sill, depth);
                if (Vector3.Dot(Vector3.Cross(s1 - s0, s3 - s0), Vector3.UnitY) > 0f) b.Quad(s0, s1, s2, s3, kit.Dressing);
                else b.Quad(s1, s0, s3, s2, kit.Dressing);
            }
            if (!o.Arched)
            {
                // The head, facing down.
                Vector3 h0 = side.Outer(left, o.Head), h1 = side.Outer(right, o.Head), h2 = side.Outer(right, o.Head, depth), h3 = side.Outer(left, o.Head, depth);
                if (Vector3.Dot(Vector3.Cross(h1 - h0, h3 - h0), -Vector3.UnitY) > 0f) b.Quad(h0, h1, h2, h3, kit.Dressing);
                else b.Quad(h1, h0, h3, h2, kit.Dressing);
            }
        }

        // The plinth, standing a little proud of the wall all round, with a sloped top to shed the rain, broken only by doors.
        private static void BuildPlinth(MeshBuilder b, HallDesign d, HallKit kit, Side side, List<HallOpening> openings)
        {
            float top = kit.PlinthTop;
            float from = -side.FaceHalf - PlinthProud, to = side.FaceHalf + PlinthProud;
            var runs = new List<(float, float)>();
            float at = from;
            foreach (HallOpening door in openings.Where(o => o.Door).OrderBy(o => o.Along))
            {
                runs.Add((at, door.Along - door.HalfWidth));
                at = door.Along + door.HalfWidth;
            }
            runs.Add((at, to));

            Vector3 origin = side.OuterOrigin + side.Out * PlinthProud;
            Vector4 bounds = new Vector4(from, to, -Below, top);
            foreach ((float a0, float a1) in runs)
            {
                if (a1 - a0 < 0.05f) continue;
                (float u0, float u1) = side.OuterU(a0, a1);
                b.Panel(origin, side.U, Vector3.UnitY, u0, u1, -Below, top, kit.Plinth, bounds);

                // The weathered top, running back up to the wall, mitred at the corners.
                float inset0 = a0 <= from + 1e-3f ? PlinthProud : 0f;
                float inset1 = a1 >= to - 1e-3f ? PlinthProud : 0f;
                Vector3 o0 = side.OuterOrigin + side.Out * PlinthProud + side.AlongDir * a0 + Vector3.UnitY * top;
                Vector3 o1 = side.OuterOrigin + side.Out * PlinthProud + side.AlongDir * a1 + Vector3.UnitY * top;
                Vector3 w1 = side.OuterOrigin + side.AlongDir * (a1 - inset1) + Vector3.UnitY * (top + PlinthProud * 0.9f);
                Vector3 w0 = side.OuterOrigin + side.AlongDir * (a0 + inset0) + Vector3.UnitY * (top + PlinthProud * 0.9f);
                if (Vector3.Dot(Vector3.Cross(o1 - o0, w0 - o0), side.Out + Vector3.UnitY) > 0f) b.Quad(o0, o1, w1, w0, kit.Plinth);
                else b.Quad(o1, o0, w0, w1, kit.Plinth);

                // Where a door breaks it, its end.
                foreach ((float end, bool isStart) in new[] { (a0, true), (a1, false) })
                {
                    if (end <= from + 1e-3f || end >= to - 1e-3f) continue;
                    Vector3 e0 = side.OuterOrigin + side.AlongDir * end + Vector3.UnitY * -Below;
                    Vector3 e1 = e0 + side.Out * PlinthProud;
                    Vector3 e2 = e1 + Vector3.UnitY * (top + Below);
                    Vector3 e3 = e0 + Vector3.UnitY * (top + Below + PlinthProud * 0.9f);
                    Vector3 facing = side.AlongDir * (isStart ? -1f : 1f);
                    if (Vector3.Dot(Vector3.Cross(e1 - e0, e3 - e0), facing) > 0f) b.Quad(e0, e1, e2, e3, kit.Plinth);
                    else b.Quad(e1, e0, e3, e2, kit.Plinth);
                }
            }
        }

        // The ends of the front and back walls, which are the corners of the building, and part of its sides as seen from outside.
        private static void BuildEnds(MeshBuilder b, HallDesign d, HallKit kit)
        {
            foreach (HallSide which in new[] { HallSide.PlusX, HallSide.MinusX })
            {
                var side = new Side(d, which);
                Vector4 bounds = new Vector4(-side.FaceHalf, side.FaceHalf, -Below, d.WallTop);
                foreach (float end in new[] { -1f, 1f })
                {
                    // Along this side, the front wall's end is from its inner face out to the corner.
                    float a0 = end > 0f ? d.Half.Y - d.Wall : -d.Half.Y;
                    float a1 = end > 0f ? d.Half.Y : -d.Half.Y + d.Wall;
                    (float u0, float u1) = side.OuterU(a0, a1);
                    b.Panel(side.OuterOrigin, side.U, Vector3.UnitY, u0, u1, kit.PlinthTop, d.FrontCut > kit.PlinthTop ? d.FrontCut : kit.PlinthTop, kit.OuterWall, bounds);
                    b.Panel(side.OuterOrigin, side.U, Vector3.UnitY, u0, u1, MathF.Max(d.FrontCut, kit.PlinthTop), d.WallTop, kit.OuterWall, bounds);
                }
            }
        }

        /// <summary>The point on a wall's outer face at <paramref name="along"/> and <paramref name="y"/>, pushed <paramref name="proud"/> metres out of it.</summary>
        public static Vector3 OnWall(HallDesign d, HallSide which, float along, float y, float proud = 0f)
        {
            var side = new Side(d, which);
            return side.Outer(along, y) + side.Out * proud;
        }
    }
}

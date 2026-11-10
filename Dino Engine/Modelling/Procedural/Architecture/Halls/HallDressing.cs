using Dino_Engine.Modelling.Model;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Halls
{
    /// <summary>How a hall dresses its doors and windows.</summary>
    internal sealed class Dress
    {
        public bool StoneSurrounds;     // Arches of voussoirs and a keystone, lintels over flat heads, sills of stone.
        public bool Shutters;
        public bool FlowerBoxes;        // Under the windows that face the camera.
        public bool Lattice = true;     // Leaded diamonds; otherwise small square panes.
        public float? LanternAlong;     // Metres either side of a door's middle that its lanterns hang, if not the usual.
        public float LanternProud;      // And how far out of the wall they're fixed, if there's something there to fix them to.
    }

    /// <summary>
    /// What a hall's doors and windows are dressed with, outside: frames and leaded cames inside the openings, stone round them, open shutters against the wall, flowers under them, doors
    /// standing open against the front with their hinges and a lantern either side. All of it stands in or against the wall it belongs to.
    /// </summary>
    internal static class HallDressing
    {
        private const float FrameDepth = 0.3f;      // How far into the wall from its outside the frame of a window stands.
        private const float Came = 0.03f;            // Half the width of a lead came.

        public static void Openings(MeshBuilder b, HallDesign d, HallKit kit, Dress dress, List<AssetLight> lights, WorldRandom random)
        {
            foreach (HallOpening o in d.Openings)
            {
                var side = new HallWalls.Side(d, o.Side);
                if (o.Door) Door(b, d, kit, dress, side, o, lights);
                else Window(b, d, kit, dress, side, o, random);
            }
        }

        // ---- Windows.

        private static void Window(MeshBuilder b, HallDesign d, HallKit kit, Dress dress, HallWalls.Side side, HallOpening o, WorldRandom random)
        {
            float spring = HallWalls.Springing(o);
            float left = o.Along - o.HalfWidth, right = o.Along + o.HalfWidth;
            Vector3 along = side.AlongDir;
            Vector3 Point(float a, float y, float depth) => side.Outer(a, y, depth);

            // The frame, set back in the opening, and its mullion and transom.
            float fw = 0.09f;
            b.Beam(Point(left + fw, o.Sill, FrameDepth), Point(left + fw, spring, FrameDepth), fw, kit.Joinery);
            b.Beam(Point(right - fw, o.Sill, FrameDepth), Point(right - fw, spring, FrameDepth), fw, kit.Joinery);
            b.Beam(Point(left, o.Sill + fw, FrameDepth), Point(right, o.Sill + fw, FrameDepth), fw, kit.Joinery);
            if (!o.Arched) b.Beam(Point(left, o.Head - fw, FrameDepth), Point(right, o.Head - fw, FrameDepth), fw, kit.Joinery);
            if (o.HalfWidth > 0.9f) b.Beam(Point(o.Along, o.Sill, FrameDepth), Point(o.Along, spring, FrameDepth), 0.07f, kit.Joinery);
            float transom = o.Sill + (spring - o.Sill) * 0.66f;
            b.Beam(Point(left, transom, FrameDepth), Point(right, transom, FrameDepth), 0.07f, kit.Joinery);

            // The glazing's lead, in diamonds or squares, inside the frame.
            if (dress.Lattice) Diamonds(b, side, left + fw * 2f, right - fw * 2f, o.Sill + fw * 2f, (o.Arched ? spring : o.Head) - fw * 2f, kit.Lead);
            else Squares(b, side, left + fw * 2f, right - fw * 2f, o.Sill + fw * 2f, (o.Arched ? spring : o.Head) - fw * 2f, kit.Joinery);
            if (o.Arched) ArchHead(b, side, o, kit);

            // The sill outside, standing proud to throw the rain off the wall.
            VertexMaterial sillStone = dress.StoneSurrounds ? kit.Dressing : kit.Joinery;
            Vector3 sillCentre = Point(o.Along, o.Sill - 0.12f, -0.12f);
            Box(b, sillCentre, along, side.Out, o.HalfWidth + 0.32f, 0.12f, 0.24f, sillStone);

            if (dress.StoneSurrounds)
            {
                if (o.Arched) Voussoirs(b, side, o, kit.Dressing, 0.62f, 0.07f);
                else
                {
                    // A lintel, and blocks up either side.
                    Box(b, Point(o.Along, o.Head + 0.32f, -0.06f), along, side.Out, o.HalfWidth + 0.45f, 0.32f, 0.08f, kit.Dressing);
                    for (float y = o.Sill + 0.1f; y < o.Head - 0.2f; y += 1.1f)
                    {
                        float h = MathF.Min(0.5f, (o.Head - y) * 0.5f);
                        bool wide = ((int)((y - o.Sill) / 1.1f) % 2) == 0;
                        foreach (float edge in new[] { left - 0.3f, right + 0.3f })
                        {
                            Box(b, Point(edge, y + h, -0.05f), along, side.Out, wide ? 0.32f : 0.22f, h - 0.02f, 0.06f, kit.Dressing);
                        }
                    }
                }
            }
            else
            {
                // A timber frame round it, on the face of the wall.
                Box(b, Point(o.Along, o.Head + 0.14f, -0.06f), along, side.Out, o.HalfWidth + 0.3f, 0.14f, 0.07f, kit.Timber);
                foreach (float edge in new[] { left - 0.13f, right + 0.13f })
                {
                    Box(b, Point(edge, (o.Sill + o.Head) * 0.5f, -0.06f), along, side.Out, 0.13f, (o.Head - o.Sill) * 0.5f + 0.1f, 0.07f, kit.Timber);
                }
            }

            if (dress.Shutters) Shutters(b, side, o, spring, kit);
            if (dress.FlowerBoxes && o.Side == HallSide.Front) FlowerBox(b, side, o, kit, random);
        }

        private static void Diamonds(MeshBuilder b, HallWalls.Side side, float a0, float a1, float y0, float y1, VertexMaterial lead)
        {
            const float Step = 0.62f;   // Between cames, along the diagonal.
            float width = a1 - a0, height = y1 - y0;
            if (width <= 0.1f || height <= 0.1f) return;
            for (int dir = -1; dir <= 1; dir += 2)
            {
                // Lines along (1, dir), each a constant of a*dir... the line through (a0 + c, y0) going up at 45 degrees.
                for (float c = -height; c < width + height; c += Step)
                {
                    float sa = dir > 0 ? a0 + c : a1 - c;
                    var points = new List<Vector2>();
                    // Where the line s = sa + dir*t, y = y0 + t meets the rectangle.
                    for (int edge = 0; edge < 4; edge++)
                    {
                        float t;
                        switch (edge)
                        {
                            case 0: t = 0f; break;                       // bottom
                            case 1: t = height; break;                   // top
                            case 2: t = (a0 - sa) / dir; break;          // left
                            default: t = (a1 - sa) / dir; break;         // right
                        }
                        if (t < -1e-4f || t > height + 1e-4f) continue;
                        float s = sa + dir * t;
                        if (s < a0 - 1e-4f || s > a1 + 1e-4f) continue;
                        points.Add(new Vector2(s, y0 + t));
                    }
                    if (points.Count < 2) continue;
                    points.Sort((p, q) => p.Y.CompareTo(q.Y));
                    Vector2 p0 = points[0], p1 = points[^1];
                    if ((p1 - p0).LengthSquared < 0.01f) continue;
                    b.Beam(side.Outer(p0.X, p0.Y, FrameDepth), side.Outer(p1.X, p1.Y, FrameDepth), Came, lead);
                }
            }
        }

        private static void Squares(MeshBuilder b, HallWalls.Side side, float a0, float a1, float y0, float y1, VertexMaterial bars)
        {
            int across = Math.Max(2, (int)MathF.Round((a1 - a0) / 0.7f));
            int up = Math.Max(2, (int)MathF.Round((y1 - y0) / 0.75f));
            for (int i = 1; i < across; i++)
            {
                float a = a0 + (a1 - a0) * i / across;
                b.Beam(side.Outer(a, y0, FrameDepth), side.Outer(a, y1, FrameDepth), Came * 1.4f, bars);
            }
            for (int j = 1; j < up; j++)
            {
                float y = y0 + (y1 - y0) * j / up;
                b.Beam(side.Outer(a0, y, FrameDepth), side.Outer(a1, y, FrameDepth), Came * 1.4f, bars);
            }
        }

        // The head of an arched window: its frame round the curve, and bars running out from the middle like the sun coming up.
        private static void ArchHead(MeshBuilder b, HallWalls.Side side, HallOpening o, HallKit kit)
        {
            float spring = HallWalls.Springing(o);
            float r = o.HalfWidth - 0.09f;
            const int Segments = 10;
            Vector3 Arc(float t, float radius) => side.Outer(o.Along + MathF.Cos(t) * radius, spring + MathF.Sin(t) * radius, FrameDepth);
            for (int i = 0; i < Segments; i++)
            {
                float t0 = MathF.PI * i / Segments, t1 = MathF.PI * (i + 1) / Segments;
                b.Beam(Arc(t0, r), Arc(t1, r), 0.09f, kit.Joinery);
                b.Beam(Arc(t0, r * 0.45f), Arc(t1, r * 0.45f), Came, kit.Lead);
            }
            for (int k = 1; k < 6; k++)
            {
                float t = MathF.PI * k / 6f;
                b.Beam(Arc(t, 0.05f), Arc(t, r), Came, kit.Lead);
            }
        }

        // A ring of wedge-shaped stones round an arch on the face of the wall, with a keystone at its crown standing a little prouder.
        private static void Voussoirs(MeshBuilder b, HallWalls.Side side, HallOpening o, VertexMaterial stone, float depth, float proud)
        {
            float spring = HallWalls.Springing(o);
            float inner = o.HalfWidth, outer = o.HalfWidth + depth;
            int stones = o.HalfWidth > 2f ? 11 : 9;
            for (int i = 0; i < stones; i++)
            {
                float gap = 0.012f;
                float t0 = MathF.PI * i / stones + gap, t1 = MathF.PI * (i + 1) / stones - gap;
                bool key = i == stones / 2;
                float extra = key ? 0.18f : 0f;
                float p = proud + (key ? 0.06f : 0f);
                Vector3 A(float t, float radius, float y = 0f) => side.Outer(o.Along + MathF.Cos(t) * radius * (side.AlongIsU ? 1f : -1f), spring + MathF.Sin(t) * radius + y, -p);
                Vector3 i0 = A(t0, inner), i1 = A(t1, inner), e1 = A(t1, outer + extra), e0 = A(t0, outer + extra);
                Wedge(b, i0, i1, e1, e0, side.Out * p, stone);
            }
            // A block at either springing, where the arch rests.
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 at = side.Outer(o.Along + s * (o.HalfWidth + depth * 0.5f), spring - 0.22f, -proud * 0.6f);
                Box(b, at, side.AlongDir, side.Out, depth * 0.55f, 0.22f, proud * 0.6f + 0.02f, stone);
            }
        }

        // A block whose face is four points on a wall, wound counter-clockwise seen from outside, standing out of the wall by `out_`.
        private static void Wedge(MeshBuilder b, Vector3 f0, Vector3 f1, Vector3 f2, Vector3 f3, Vector3 out_, VertexMaterial material)
        {
            Vector3 normal = Vector3.Cross(f1 - f0, f3 - f0);
            if (Vector3.Dot(normal, out_) < 0f) (f0, f1, f2, f3) = (f1, f0, f3, f2);
            b.Quad(f0, f1, f2, f3, material);
            Vector3 b0 = f0 - out_, b1 = f1 - out_, b2 = f2 - out_, b3 = f3 - out_;
            b.Quad(b0, b1, f1, f0, material);
            b.Quad(b1, b2, f2, f1, material);
            b.Quad(b2, b3, f3, f2, material);
            b.Quad(b3, b0, f0, f3, material);
        }

        // Two shutters, opened flat against the wall either side of the window, boarded with a Z of battens across them.
        private static void Shutters(MeshBuilder b, HallWalls.Side side, HallOpening o, float spring, HallKit kit)
        {
            float width = o.HalfWidth - 0.05f;
            float height = spring - o.Sill;
            foreach (float s in new[] { -1f, 1f })
            {
                float middle = o.Along + s * (o.HalfWidth + 0.2f + width * 0.5f);
                Vector3 centre = side.Outer(middle, o.Sill + height * 0.5f, -0.07f);
                Box(b, centre, side.AlongDir, side.Out, width * 0.5f, height * 0.5f, 0.05f, kit.Shutter);
                // Battens, and the Z between them.
                float y0 = o.Sill + height * 0.18f, y1 = o.Sill + height * 0.82f;
                foreach (float y in new[] { y0, y1 }) Box(b, side.Outer(middle, y, -0.13f), side.AlongDir, side.Out, width * 0.5f, 0.09f, 0.025f, kit.Joinery);
                b.Beam(side.Outer(middle - width * 0.42f * s, y0 + 0.1f, -0.13f), side.Outer(middle + width * 0.42f * s, y1 - 0.1f, -0.13f), 0.06f, kit.Joinery);
                // Its hinges, at the window's side.
                foreach (float y in new[] { y0, y1 })
                {
                    Box(b, side.Outer(o.Along + s * (o.HalfWidth + 0.35f), y, -0.155f), side.AlongDir, side.Out, 0.32f, 0.04f, 0.012f, Hall.Iron(82));
                }
            }
        }

        // A box of flowers under a window, on brackets.
        private static void FlowerBox(MeshBuilder b, HallWalls.Side side, HallOpening o, HallKit kit, WorldRandom random)
        {
            float half = o.HalfWidth + 0.15f;
            float y = o.Sill - 0.5f;
            Box(b, side.Outer(o.Along, y, -0.42f), side.AlongDir, side.Out, half, 0.26f, 0.3f, kit.Joinery);
            foreach (float s in new[] { -0.75f, 0.75f })
            {
                b.Beam(side.Outer(o.Along + s * half, y - 0.3f, -0.05f), side.Outer(o.Along + s * half, y - 0.3f, -0.62f), 0.05f, kit.Joinery);
            }
            VertexMaterial leaves = new VertexMaterial(Textures.MaterialCatalog.FoliageWhite.Index, new Util.Colour(44, 82, 36));
            Vector3[] blooms = { new(214, 96, 132), new(240, 226, 212), new(226, 164, 60), new(150, 96, 190), new(232, 120, 120) };
            Vector3 bloom = random.Pick(blooms);
            int count = (int)(half * 9f);
            for (int i = 0; i < count; i++)
            {
                float a = o.Along + random.Range(-half + 0.1f, half - 0.1f);
                float depth = -random.Range(0.25f, 0.6f);
                float height = random.Range(0.15f, 0.45f);
                Vector3 foot = side.Outer(a, y + 0.22f, depth);
                b.Box(foot + Vector3.UnitY * height * 0.4f, new Vector3(0.11f, height * 0.4f, 0.11f), leaves);
                if (random.Chance(0.75f))
                {
                    Vector3 colour = random.Chance(0.25f) ? random.Pick(blooms) : bloom;
                    b.Box(foot + Vector3.UnitY * (height * 0.85f), new Vector3(0.09f, 0.07f, 0.09f), Hall.Flat((int)colour.X, (int)colour.Y, (int)colour.Z));
                }
            }
        }

        // ---- Doors.

        private static void Door(MeshBuilder b, HallDesign d, HallKit kit, Dress dress, HallWalls.Side side, HallOpening o, List<AssetLight> lights)
        {
            float spring = HallWalls.Springing(o);
            float left = o.Along - o.HalfWidth, right = o.Along + o.HalfWidth;

            // A worn stone sill right through the wall, down to the floor inside, and a step outside it.
            Vector3 threshold = side.Outer(o.Along, Hall.SillTop - 0.13f, d.Wall * 0.5f);
            Box(b, threshold, side.AlongDir, side.Out, o.HalfWidth, 0.13f, d.Wall * 0.5f, kit.Dressing);
            Box(b, side.Outer(o.Along, 0.06f, -0.7f), side.AlongDir, side.Out, o.HalfWidth + 0.6f, 0.11f, 0.7f, kit.Plinth);

            if (dress.StoneSurrounds && o.Arched)
            {
                Voussoirs(b, side, o, kit.Dressing, 0.75f, 0.09f);
                // Jamb stones up either side, long and short.
                for (float y = 0.1f; y < spring - 0.3f; y += 1f)
                {
                    bool wide = ((int)(y / 1f) % 2) == 0;
                    float h = MathF.Min(0.48f, (spring - y) * 0.5f);
                    foreach (float edge in new[] { left - 0.38f, right + 0.38f })
                    {
                        Box(b, side.Outer(edge, y + h, -0.07f), side.AlongDir, side.Out, wide ? 0.4f : 0.28f, h - 0.02f, 0.08f, kit.Dressing);
                    }
                }
            }
            else
            {
                // Posts and a carved lintel.
                foreach (float edge in new[] { left - 0.16f, right + 0.16f })
                {
                    Box(b, side.Outer(edge, o.Head * 0.5f, -0.08f), side.AlongDir, side.Out, 0.18f, o.Head * 0.5f, 0.1f, kit.Timber);
                }
                Box(b, side.Outer(o.Along, o.Head + 0.26f, -0.1f), side.AlongDir, side.Out, o.HalfWidth + 0.6f, 0.26f, 0.12f, kit.Timber);
                Box(b, side.Outer(o.Along, o.Head + 0.58f, -0.16f), side.AlongDir, side.Out, o.HalfWidth + 0.75f, 0.07f, 0.18f, kit.Timber);
            }

            // The leaves, standing wide open against the wall outside.
            foreach (float s in new[] { -1f, 1f })
            {
                float hinge = o.Along + s * o.HalfWidth;
                Leaf(b, side, o, hinge, -s, kit);
            }

            // A lantern either side, at the height of a hand held up.
            foreach (float s in new[] { -1f, 1f })
            {
                float a = o.Along + s * (dress.LanternAlong ?? o.HalfWidth + (dress.StoneSurrounds ? 1.45f : 1.15f));
                WallLantern(b, side.Outer(a, 5.6f, -dress.LanternProud), side.Out, lights);
            }
        }

        // One leaf of a door, boarded, swung out from its hinge until it nearly lies against the wall beside the door. Its top follows the arch it closes, if it has one.
        private static void Leaf(MeshBuilder b, HallWalls.Side side, HallOpening o, float hingeAlong, float towardsMiddle, HallKit kit)
        {
            const float Thick = 0.14f;
            const float Swing = 2.85f;      // Radians it's turned out through, from shut.
            float spring = HallWalls.Springing(o);
            float width = o.HalfWidth - 0.04f;
            Vector3 hinge = side.Outer(hingeAlong, 0.06f, -0.02f);
            Vector3 shut = side.AlongDir * towardsMiddle;
            Vector3 e1 = shut * MathF.Cos(Swing) + side.Out * MathF.Sin(Swing);
            Vector3 n = Vector3.Cross(Vector3.UnitY, e1).Normalized();
            float Top(float u)
            {
                if (!o.Arched) return o.Head - 0.08f - 0.06f;
                float fromMiddle = o.HalfWidth - u;
                return spring + MathF.Sqrt(MathF.Max(o.HalfWidth * o.HalfWidth - fromMiddle * fromMiddle, 0f)) - 0.14f;
            }

            const int Strips = 8;
            Vector3 P(float u, float v, float w) => hinge + e1 * u + Vector3.UnitY * v + n * w;
            Vector4 bounds = new Vector4(0f, width, 0f, o.Head);
            for (int i = 0; i < Strips; i++)
            {
                float u0 = width * i / Strips, u1 = width * (i + 1) / Strips;
                float t0 = Top(u0), t1 = Top(u1);
                // Both faces, each wound to face out from the leaf: the one at Thick towards n, the other away from it.
                b.PlaneQuad(P(u0, 0f, Thick), P(u0, t0, Thick), P(u1, t1, Thick), P(u1, 0f, Thick), kit.DoorLeaf, P(0f, 0f, Thick), e1, Vector3.UnitY, bounds);
                b.PlaneQuad(P(u0, 0f, 0f), P(u1, 0f, 0f), P(u1, t1, 0f), P(u0, t0, 0f), kit.DoorLeaf, P(0f, 0f, 0f), e1, Vector3.UnitY, bounds);
                // Its top edge, facing up.
                b.Quad(P(u1, t1, Thick), P(u0, t0, Thick), P(u0, t0, 0f), P(u1, t1, 0f), kit.DoorLeaf);
            }
            // Its two upright edges, facing out of either end.
            b.Quad(P(0f, Top(0f), 0f), P(0f, Top(0f), Thick), P(0f, 0f, Thick), P(0f, 0f, 0f), kit.DoorLeaf);
            b.Quad(P(width, Top(width), Thick), P(width, Top(width), 0f), P(width, 0f, 0f), P(width, 0f, Thick), kit.DoorLeaf);

            // Strap hinges across it, and a ring to pull it by.
            VertexMaterial iron = Hall.Iron(86);
            foreach (float v in new[] { 1.1f, Top(width) - 1.4f, (Top(width) + 1.1f) * 0.5f })
            {
                b.Beam(P(0.05f, v, Thick + 0.02f), P(width * 0.82f, v, Thick + 0.02f), 0.075f, iron);
                b.Beam(P(0.05f, v, -0.02f), P(width * 0.82f, v, -0.02f), 0.075f, iron);
            }
            b.Tube(P(width - 0.4f, 3.1f, Thick + 0.02f), P(width - 0.4f, 3.1f, Thick + 0.09f), 0.12f, 0.12f, 6, iron);
        }

        /// <summary>A lantern on an arm out of a wall, glass all round its flame, and the light it gives, which may cast shadows when it's among the nearest.</summary>
        public static void WallLantern(MeshBuilder b, Vector3 onWall, Vector3 out_, List<AssetLight> lights)
        {
            VertexMaterial iron = Hall.Iron(70);
            Vector3 hang = onWall + out_ * 0.85f;
            b.Box(onWall + out_ * 0.03f, AxisHalf(out_, 0.18f, 0.28f, 0.03f), iron);
            b.Beam(onWall + out_ * 0.05f + Vector3.UnitY * 0.12f, hang + Vector3.UnitY * 0.62f, 0.035f, iron);
            b.Beam(onWall + out_ * 0.05f - Vector3.UnitY * 0.2f, hang + Vector3.UnitY * 0.62f - out_ * 0.3f, 0.03f, iron);
            b.Tube(hang + Vector3.UnitY * 0.62f, hang + Vector3.UnitY * 0.42f, 0.02f, 0.02f, 4, iron);
            b.Box(hang, new Vector3(0.2f, 0.32f, 0.2f), Hall.Glow(1f, 0.58f, 0.22f, 2.6f));
            b.Box(hang + Vector3.UnitY * 0.36f, new Vector3(0.27f, 0.05f, 0.27f), iron);
            b.Tube(hang + Vector3.UnitY * 0.41f, hang + Vector3.UnitY * 0.6f, 0.22f, 0.04f, 4, iron);
            b.Box(hang - Vector3.UnitY * 0.36f, new Vector3(0.25f, 0.04f, 0.25f), iron);
            foreach (float x in new[] { -0.2f, 0.2f })
            {
                foreach (float z in new[] { -0.2f, 0.2f }) b.Beam(hang + new Vector3(x, -0.34f, z), hang + new Vector3(x, 0.34f, z), 0.02f, iron);
            }
            lights.Add(AssetLight.Lantern(hang + out_ * 0.3f) with { Intensity = 110f, Radius = 18f });
        }

        /// <summary>A sign on a bracket out of a wall, swinging from two rings, painted with what's inside.</summary>
        public static void Sign(MeshBuilder b, Vector3 onWall, Vector3 out_, VertexMaterial board, VertexMaterial paint, Action<Vector3, Vector3, Vector3> emblem)
        {
            VertexMaterial iron = Hall.Iron(74);
            Vector3 across = Vector3.Cross(Vector3.UnitY, out_).Normalized();
            Vector3 end = onWall + out_ * 2.3f;
            b.Beam(onWall, end, 0.05f, iron);
            b.Beam(onWall - Vector3.UnitY * 0.9f, onWall + out_ * 1.2f - Vector3.UnitY * 0.04f, 0.04f, iron);
            for (int i = 0; i < 4; i++)
            {
                // A curl at the end of the bracket.
                float t0 = i * MathF.PI * 0.5f, t1 = (i + 1) * MathF.PI * 0.5f;
                Vector3 c = onWall + out_ * 0.5f - Vector3.UnitY * 0.45f;
                b.Beam(c + (out_ * MathF.Cos(t0) + Vector3.UnitY * MathF.Sin(t0)) * 0.22f, c + (out_ * MathF.Cos(t1) + Vector3.UnitY * MathF.Sin(t1)) * 0.22f, 0.025f, iron);
            }
            Vector3 middle = onWall + out_ * 1.35f - Vector3.UnitY * 1.25f;
            foreach (float s in new[] { -0.55f, 0.55f }) b.Beam(onWall + out_ * (1.35f + s), middle + out_ * s + Vector3.UnitY * 0.95f, 0.02f, iron);
            b.Box(middle, AxisHalf(out_, 0.95f, 0.82f, 0.06f, across: true), board);
            b.Box(middle, AxisHalf(out_, 1.02f, 0.89f, 0.04f, across: true), Hall.DarkOak);
            emblem(middle, out_, across);
        }

        // A box's half extents turned so that its thin side faces `out_`: with `across`, the box lies in the plane that out_ runs along.
        private static Vector3 AxisHalf(Vector3 out_, float along, float up, float thick, bool across = false)
        {
            // Out along x or z: thick that way, `along` the other.
            bool alongX = MathF.Abs(out_.X) > MathF.Abs(out_.Z);
            if (across) return alongX ? new Vector3(along, up, thick) : new Vector3(thick, up, along);
            return alongX ? new Vector3(thick, up, along) : new Vector3(along, up, thick);
        }

        /// <summary>A box lying along a wall: <paramref name="halfAlong"/> along it, <paramref name="halfUp"/> up, and <paramref name="halfOut"/> out from it, about <paramref name="centre"/>.</summary>
        public static void Box(MeshBuilder b, Vector3 centre, Vector3 along, Vector3 out_, float halfAlong, float halfUp, float halfOut, VertexMaterial material)
        {
            Vector3 half = new Vector3(MathF.Abs(along.X) * halfAlong + MathF.Abs(out_.X) * halfOut, halfUp, MathF.Abs(along.Z) * halfAlong + MathF.Abs(out_.Z) * halfOut);
            b.Box(centre, half, material);
        }
    }
}

using Dino_Engine.Modelling.Model;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>Which sides of a plate are turned over into a rolled edge, as armour's are, so that a blade glances off and the edge takes the knocks.</summary>
    [Flags]
    public enum PlateEdge
    {
        None = 0,
        Start = 1,      // Where u is 0.
        End = 2,        // Where u is 1.
        Bottom = 4,     // Where v is 0.
        Top = 8,        // Where v is 1.
        All = 15,
    }

    /// <summary>
    /// What armour and weapons are made of beyond lofts and sweeps: bands of colour with crisp edges and no seam in the shading, plates with a thickness laid over a surface, straps that follow one,
    /// helical wraps, rivets, buckles and blocks. Everything goes through <see cref="At"/> and is skinned to <see cref="Bone"/>, as everything else here is.
    /// </summary>
    public sealed partial class GearMesh
    {
        /// <summary>
        /// A skin through a run of rings, like <see cref="Loft"/>, but coloured by bands: <paramref name="band"/> is asked for the band that starts at each ring, and where the next one is different the
        /// change is crisp, at the ring, with no seam in the shading unless the ring is <see cref="Ring.Hard"/>. This is how a hem, a trim or a wrap stays a band and doesn't run into what's beside it.
        /// </summary>
        public void Bands(Section section, IReadOnlyList<Ring> rings, Func<int, int, VertexMaterial> band, bool capStart = true, bool capEnd = true)
        {
            int points = section.Count;
            var columns = new List<(int point, float around)>(points * 2);
            float distance = 0f;
            Vector2[] first = rings[0].Shape ?? section.Points;
            for (int i = 0; i <= points; i++)
            {
                int point = i % points;
                columns.Add((point, distance));
                if (i > 0 && i < points && section.Hard[point]) columns.Add((point, distance));
                if (i < points) distance += (first[(i + 1) % points] - first[point]).Length;
            }

            float widest = 0f;
            foreach (Ring ring in rings) widest = MathF.Max(widest, MathF.Max(ring.HalfWidth, ring.HalfDepth));
            float wraps = MathF.Max(1f, MathF.Round(distance * widest * TilesFor(band(0, 0))));
            for (int c = 0; c < columns.Count; c++) columns[c] = (columns[c].point, distance > 0f ? columns[c].around / distance * wraps : 0f);

            Vector3 axis = rings[1].Centre - rings[0].Centre;
            bool forwards = Vector3.Dot(Vector3.Cross(rings[0].Side, rings[0].Front), axis) < 0f;

            int last = rings.Count - 1;
            float along = 0f;
            int[]? previous = null;
            for (int r = 0; r <= last; r++)
            {
                if (r > 0) along += (rings[r].Centre - rings[r - 1].Centre).Length;
                int before = Math.Clamp(r - 1, 0, last - 1), after = Math.Clamp(r, 0, last - 1);
                int[] closing = AddRow(section, rings[r], columns, before, along, band);
                if (previous != null) Stitch(previous, closing, columns, forwards);
                Weld(closing, columns, section);

                int[] opening = closing;
                if (r > 0 && r < last && (rings[r].Hard || Differ(band, before, after, points)))
                {
                    opening = AddRow(section, rings[r], columns, after, along, band);
                    Weld(opening, columns, section);
                    if (!rings[r].Hard)
                    {
                        for (int c = 0; c < columns.Count; c++) _welds.Add((closing[c], opening[c]));
                    }
                }
                previous = opening;
            }

            if (capStart) Cap(section, rings[0], 0, band, !forwards);
            if (capEnd) Cap(section, rings[last], last - 1, band, forwards);
        }

        private static bool Differ(Func<int, int, VertexMaterial> band, int a, int b, int points)
        {
            if (a == b) return false;
            for (int p = 0; p < points; p++)
            {
                if (!Same(band(a, p), band(b, p))) return true;
            }
            return false;
        }

        /// <summary>Whether two are the same material in the same colour.</summary>
        public static bool Same(in VertexMaterial a, in VertexMaterial b) => a.materialTextureIndex == b.materialTextureIndex && a.colour.ToVector4() == b.colour.ToVector4();

        /// <summary>
        /// A plate with a thickness, laid over a surface: a grid across (u) and along (v), each from 0 to 1, where <paramref name="surface"/> says where the plate's inner face is and which way is out.
        /// Its outer face is <paramref name="thick"/> further out, its sides are closed with crisp edges, and the sides in <paramref name="rolled"/> are turned over into a rolled edge of radius
        /// <paramref name="roll"/>. A plate that goes all the way round (<paramref name="wrap"/>) has no sides at u's ends, and the surface has to come back to where it started at u = 1.
        /// </summary>
        public void Plate(Func<float, float, (Vector3 at, Vector3 outward)> surface, int across, int along, float thick, Func<float, float, VertexMaterial> material,
            PlateEdge rolled = PlateEdge.None, float roll = 0f, bool wrap = false, VertexMaterial? underside = null, VertexMaterial? rollMaterial = null)
        {
            across = Math.Max(across, 1);
            along = Math.Max(along, 1);
            int columns = across + 1, rows = along + 1;
            var inner = new Vector3[columns, rows];
            var outer = new Vector3[columns, rows];
            var normal = new Vector3[columns, rows];
            for (int i = 0; i < columns; i++)
            {
                for (int j = 0; j < rows; j++)
                {
                    (Vector3 at, Vector3 out_) = surface((float)i / across, (float)j / along);
                    out_ = out_.LengthSquared > 1e-12f ? out_.Normalized() : Vector3.UnitZ;
                    inner[i, j] = at;
                    outer[i, j] = at + out_ * MathF.Max(thick, Thinnest);
                    normal[i, j] = out_;
                }
            }

            // Which way round the grid's faces go, from the middle of it, as a surface may run either way.
            int mi = across / 2, mj = along / 2;
            Vector3 probe = Vector3.Cross(outer[Math.Min(mi + 1, across), mj] - outer[mi, mj], outer[mi, Math.Min(mj + 1, along)] - outer[mi, mj]);
            bool reversed = Vector3.Dot(probe, normal[mi, mj]) < 0f;

            // Metres across and along the outer face, each material at its own size, a whole number of tiles round if it wraps.
            var u = new float[columns, rows];
            var v = new float[columns, rows];
            for (int j = 0; j < rows; j++)
            {
                for (int i = 1; i < columns; i++) u[i, j] = u[i - 1, j] + (outer[i, j] - outer[i - 1, j]).Length;
            }
            for (int i = 0; i < columns; i++)
            {
                for (int j = 1; j < rows; j++) v[i, j] = v[i, j - 1] + (outer[i, j] - outer[i, j - 1]).Length;
            }

            VertexMaterial Material(int i, int j) => material((float)i / across, (float)j / along);
            float Tiles(in VertexMaterial m) => TilesFor(m);
            float RoundU(int i, int j, float tiles)
            {
                if (!wrap) return u[i, j] * tiles;
                float round = u[across, j];
                float whole = MathF.Max(1f, MathF.Round(round * tiles));
                return round > 0f ? u[i, j] / round * whole : 0f;
            }

            int[,] Face(Vector3[,] where, Func<int, int, VertexMaterial> paint)
            {
                var index = new int[columns, rows];
                for (int i = 0; i < columns; i++)
                {
                    for (int j = 0; j < rows; j++)
                    {
                        VertexMaterial m = paint(i, j);
                        float tiles = Tiles(m);
                        index[i, j] = AddVertex(where[i, j], m, new Vector2(RoundU(i, j, tiles), v[i, j] * tiles));
                    }
                }
                return index;
            }

            void Grid(int[,] index, bool flip)
            {
                for (int i = 0; i < across; i++)
                {
                    for (int j = 0; j < along; j++)
                    {
                        int a = index[i, j], b = index[i + 1, j], c = index[i, j + 1], d = index[i + 1, j + 1];
                        if (flip)
                        {
                            AddFace(a, c, b);
                            AddFace(b, c, d);
                        }
                        else
                        {
                            AddFace(a, b, c);
                            AddFace(b, d, c);
                        }
                    }
                }
            }

            int[,] top = Face(outer, Material);
            Grid(top, reversed);
            VertexMaterial under = underside ?? Material(mi, mj);
            int[,] bottom = Face(inner, (_, _) => under);
            Grid(bottom, !reversed);

            if (wrap)
            {
                for (int j = 0; j < rows; j++)
                {
                    _welds.Add((top[0, j], top[across, j]));
                    _welds.Add((bottom[0, j], bottom[across, j]));
                }
            }

            // The sides, each a strip of its own so that the edge between face and side stays crisp.
            void Side(IList<(int i, int j)> line, Vector3 away)
            {
                var outerRow = new int[line.Count];
                var innerRow = new int[line.Count];
                float run = 0f;
                for (int k = 0; k < line.Count; k++)
                {
                    (int i, int j) = line[k];
                    if (k > 0) run += (outer[i, j] - outer[line[k - 1].i, line[k - 1].j]).Length;
                    VertexMaterial m = Material(i, j);
                    float tiles = Tiles(m);
                    outerRow[k] = AddVertex(outer[i, j], m, new Vector2(run * tiles, 0f));
                    innerRow[k] = AddVertex(inner[i, j], m, new Vector2(run * tiles, MathF.Max(thick, Thinnest) * tiles));
                }
                (int i0, int j0) = line[0];
                (int i1, int j1) = line[Math.Min(1, line.Count - 1)];
                Vector3 test = Vector3.Cross(outer[i1, j1] - outer[i0, j0], inner[i0, j0] - outer[i0, j0]);
                bool flip = Vector3.Dot(test, away) < 0f;
                for (int k = 0; k + 1 < line.Count; k++)
                {
                    int a = outerRow[k], b = outerRow[k + 1], c = innerRow[k], d = innerRow[k + 1];
                    if (flip)
                    {
                        AddFace(a, c, b);
                        AddFace(b, c, d);
                    }
                    else
                    {
                        AddFace(a, b, c);
                        AddFace(b, d, c);
                    }
                }
            }

            List<(int, int)> Along(int i) => Enumerable.Range(0, rows).Select(j => (i, j)).ToList();
            List<(int, int)> Across(int j) => Enumerable.Range(0, columns).Select(i => (i, j)).ToList();
            Side(Across(0), outer[mi, 0] - outer[mi, Math.Min(1, along)]);
            Side(Across(along), outer[mi, along] - outer[mi, Math.Max(along - 1, 0)]);
            if (!wrap)
            {
                Side(Along(0), outer[0, mj] - outer[Math.Min(1, across), mj]);
                Side(Along(across), outer[across, mj] - outer[Math.Max(across - 1, 0), mj]);
            }

            if (rolled == PlateEdge.None || roll <= 0f) return;
            void Roll(List<(int i, int j)> line)
            {
                if (line.Count < 2) return;
                var spine = new List<Vector3>(line.Count);
                foreach ((int i, int j) in line) spine.Add(outer[i, j] - normal[i, j] * (roll * 0.55f));
                (int fi, int fj) = line[0];
                Sweep(Section.Round(6), spine, _ => new Vector2(roll), rollMaterial ?? Material(fi, fj), normal[fi, fj], capStart: !wrap, capEnd: !wrap);
            }
            if ((rolled & PlateEdge.Bottom) != 0) Roll(Across(0));
            if ((rolled & PlateEdge.Top) != 0) Roll(Across(along));
            if (!wrap && (rolled & PlateEdge.Start) != 0) Roll(Along(0));
            if (!wrap && (rolled & PlateEdge.End) != 0) Roll(Along(across));
        }

        /// <summary>
        /// A flat band laid along a path over a surface, for straps, belts, trims and edging: <paramref name="outward"/> is the surface's normal at each point, which is the way the band's face looks, and
        /// it sits on the surface rather than in it. <paramref name="width"/> scales its half width along it, from 0 to 1 of the way.
        /// </summary>
        public void Strip(IReadOnlyList<Vector3> path, IReadOnlyList<Vector3> outward, float halfWidth, float thick, VertexMaterial material, bool capStart = true, bool capEnd = true, float bevel = 0.35f, Func<float, float>? width = null)
        {
            int count = path.Count;
            if (count < 2) return;
            var rings = new Ring[count];
            Vector3 lastSide = Vector3.UnitX;
            for (int i = 0; i < count; i++)
            {
                Vector3 tangent = path[Math.Min(i + 1, count - 1)] - path[Math.Max(i - 1, 0)];
                Vector3 up = outward[i].LengthSquared > 1e-12f ? outward[i].Normalized() : Vector3.UnitZ;
                Vector3 side = Vector3.Cross(up, tangent);
                side = side.LengthSquared > 1e-12f ? side.Normalized() : lastSide;
                lastSide = side;
                float half = halfWidth * (width?.Invoke((float)i / (count - 1)) ?? 1f);
                rings[i] = new Ring { Centre = path[i] + up * (thick * 0.5f), Side = side, Front = up, HalfWidth = MathF.Max(half, Thinnest), HalfDepth = thick * 0.5f };
            }
            Loft(Section.Strap(bevel), rings, material, capStart, capEnd);
        }

        /// <summary>
        /// A grip wrapped round and round in a strap or a cord, up y from <paramref name="bottom"/> to <paramref name="top"/>: each turn stands up where it laps the one before, which is what reads as
        /// a wrap and not a stripe. <paramref name="pitch"/> is how far up one turn goes and <paramref name="ridge"/> how far a turn stands out, as a share of the radius.
        /// </summary>
        public void Wrap(float bottom, float top, Func<float, float> radius, float pitch, float ridge, VertexMaterial material, int sides = 14, float squash = 1f, bool capStart = false, bool capEnd = false)
        {
            float length = top - bottom;
            pitch = MathF.Max(pitch, 0.005f);
            // Four rings to a turn are enough to read as a helix, and a long grip wound fine is only so many turns before nobody could count them.
            int steps = Math.Clamp((int)MathF.Ceiling(length / pitch * 4f), 4, 80);
            var rings = new Ring[steps + 1];
            for (int s = 0; s <= steps; s++)
            {
                float t = (float)s / steps;
                float y = bottom + t * length;
                var shape = new Vector2[sides];
                for (int k = 0; k < sides; k++)
                {
                    float angle = k * (MathF.Tau / sides);
                    float turn = angle / MathF.Tau + y / pitch;
                    turn -= MathF.Floor(turn);
                    float lap = Smooth(0f, 0.14f, turn) * (1f - 0.45f * turn);
                    float r = 1f + ridge * lap;
                    shape[k] = new Vector2(MathF.Cos(angle) * r, MathF.Sin(angle) * r);
                }
                float around = radius(t);
                rings[s] = new Ring { Centre = new Vector3(0f, y, 0f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = around, HalfDepth = around * squash, Shape = shape };
            }
            Loft(Section.Round(sides), rings, material, capStart, capEnd);
        }

        /// <summary>
        /// A rivet's or a stud's dome, sitting on a surface. A small one is a dot even on the character screen and there are dozens of them to a coat, so under 3.5 cm it's one step of five
        /// sides, and under 2 cm of four, which shade as the same bump.
        /// </summary>
        public void Rivet(Vector3 at, Vector3 normal, float radius, VertexMaterial material, int sides = 6)
        {
            int steps = radius < 0.035f ? 1 : 2;
            if (radius < 0.035f) sides = Math.Min(sides, radius < 0.02f ? 4 : 5);
            At(FacingOut(at, normal), () => Lathe(Section.Round(sides), radius * 0.6f, steps, t => radius * MathF.Sqrt(MathF.Max(1f - t * t, 0.05f)), material, capStart: false));
        }

        /// <summary>Turns what's built standing up y so that it stands up out of a surface along <paramref name="normal"/>, at <paramref name="at"/>.</summary>
        public static Matrix4 FacingOut(Vector3 at, Vector3 normal)
        {
            normal = normal.LengthSquared > 1e-12f ? normal.Normalized() : Vector3.UnitY;
            Vector3 axis = Vector3.Cross(Vector3.UnitY, normal);
            if (axis.LengthSquared < 1e-8f) return normal.Y < 0f ? Matrix4.CreateRotationX(MathF.PI) * Matrix4.CreateTranslation(at) : Matrix4.CreateTranslation(at);
            return Matrix4.CreateFromAxisAngle(axis.Normalized(), MathF.Acos(Math.Clamp(normal.Y, -1f, 1f))) * Matrix4.CreateTranslation(at);
        }

        /// <summary>A frame from three directions, for building in a place's own terms: x across, y along and z out.</summary>
        public static Matrix4 Basis(Vector3 at, Vector3 across, Vector3 along, Vector3 out_)
        {
            return new Matrix4(new Vector4(across, 0f), new Vector4(along, 0f), new Vector4(out_, 0f), new Vector4(at, 1f));
        }

        /// <summary>A box with its edges taken off, standing up y from its foot: each edge chamfered by <paramref name="bevel"/> of the smaller of its sizes, top and bottom as well as up its sides.</summary>
        public void Block(float halfWidth, float height, float halfDepth, float bevel, VertexMaterial material)
        {
            float b = MathF.Min(bevel * MathF.Min(halfWidth, halfDepth), height * 0.45f);
            float inset = MathF.Max(1f - b / MathF.Max(MathF.Min(halfWidth, halfDepth), Thinnest), 0.3f);
            Section section = Section.Slab(Math.Clamp(bevel, 0.05f, 0.6f));
            Ring[] rings =
            {
                Ring.Flat(0f, halfWidth * inset, halfDepth * inset, true),
                Ring.Flat(b, halfWidth, halfDepth, true),
                Ring.Flat(height - b, halfWidth, halfDepth, true),
                Ring.Flat(height, halfWidth * inset, halfDepth * inset, true),
            };
            Loft(section, rings, material);
        }

        /// <summary>
        /// A buckle on a strap: a frame with its corners rounded and a tongue across it, lying in the strap's plane, built in the place's own terms (<see cref="Basis"/>), with the strap running
        /// along y and the frame <paramref name="width"/> across.
        /// </summary>
        public void Buckle(float width, float length, float wire, VertexMaterial material)
        {
            float hw = width * 0.5f, hl = length * 0.5f, corner = MathF.Min(hw, hl) * 0.45f;
            var loop = new List<Vector3>();
            void Arc(float cx, float cy, float from)
            {
                for (int k = 0; k <= 2; k++)
                {
                    float a = from + k * (MathF.PI / 4f);
                    loop.Add(new Vector3(cx + MathF.Cos(a) * corner, cy + MathF.Sin(a) * corner, wire));
                }
            }
            Arc(hw - corner, hl - corner, 0f);
            Arc(-hw + corner, hl - corner, MathF.PI / 2f);
            Arc(-hw + corner, -hl + corner, MathF.PI);
            Arc(hw - corner, -hl + corner, MathF.PI * 1.5f);
            loop.Add(loop[0]);
            Sweep(Section.Round(4), loop, _ => new Vector2(wire), material, Vector3.UnitZ, capStart: false, capEnd: false);
            Sweep(Section.Round(4), new[] { new Vector3(0f, -hl, wire * 1.4f), new Vector3(0f, hl * 0.3f, wire * 1.8f) }, t => new Vector2(wire * (1f - t * 0.4f)), material, Vector3.UnitZ);
        }

        private static float Smooth(float edge0, float edge1, float x)
        {
            float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
            return t * t * (3f - 2f * t);
        }
    }
}

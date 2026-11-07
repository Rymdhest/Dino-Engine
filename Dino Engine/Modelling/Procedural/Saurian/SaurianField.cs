using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Saurian
{
    /// <summary>Which piece of a body a bit of flesh belongs to, which is what says which bones its skin hangs from.</summary>
    public enum SaurianPart : byte
    {
        Body,       // Along the line of the body: tail, trunk and neck.
        Head,
        LegL, LegR, ForeL, ForeR,
        ArmL, ArmR,
    }

    /// <summary>
    /// A body as signed distances, negative inside, built from rounded cones and ellipsoids blended together, as Quilez builds creatures. Each piece of the body is blended within itself, and each limb into
    /// the body only near where it joins, so two legs side by side never grow together however close they stand. The head is one volume cut along the line of the mouth: what's above is the skull,
    /// what's below is the jaw, and the two fit exactly when it's shut.
    /// </summary>
    public sealed class SaurianField
    {
        public enum Shape : byte { Cone, Ellipsoid }

        public struct Piece
        {
            public Shape Shape;
            public SaurianPart Part;
            public bool InHead;         // In the head's own frame rather than the body's.
            public bool Carve;          // Takes away rather than adds.
            public Vector3 A, B;        // A cone's ends, or an ellipsoid's middle.
            public float RA, RB;        // A cone's radius at each end.
            public Vector3 Radii;       // An ellipsoid's.
            public Vector3 X, Y, Z;     // An ellipsoid's axes, or across and up for a cone, which is squashed across by Squash.
            public float Squash;
            public float Blend;         // How softly it joins what it's with.
            public Vector3 Centre;      // What it can reach, for leaving it out of places it can't.
            public float Bound;
        }

        /// <summary>Where a limb joins the body, and how softly: it's blended in only near there.</summary>
        public struct Root
        {
            public SaurianPart Part;
            public Vector3 At;
            public float Blend;
            public float Reach;
        }

        public readonly List<Piece> Pieces = new();
        public readonly List<Root> Roots = new();
        public SaurianHead? Head;
        public bool Jaw;                // Whether this is the jaw's field rather than the rest of the body's.
        public float Gap;               // Between skull and jaw, so that their surfaces never touch.

        private const int PartCount = 8;

        public void Cone(SaurianPart part, Vector3 a, Vector3 b, float ra, float rb, float blend, float squash = 1f, Vector3? up = null, bool inHead = false, bool carve = false)
        {
            Vector3 z = b - a;
            if (z.LengthSquared < 1e-10f) z = Vector3.UnitZ * 1e-4f;
            Vector3 way = z.Normalized();
            Vector3 upWay = up ?? Vector3.UnitY;
            Vector3 y = upWay - way * Vector3.Dot(upWay, way);
            if (y.LengthSquared < 1e-6f) y = MathF.Abs(way.Y) < 0.9f ? Vector3.UnitY - way * way.Y : Vector3.UnitX - way * way.X;
            y.Normalize();
            float reach = MathF.Max(ra, rb) * MathF.Max(1f, 1f / MathF.Max(squash, 0.1f));
            Pieces.Add(new Piece
            {
                Shape = Shape.Cone, Part = part, InHead = inHead, Carve = carve, A = a, B = b, RA = ra, RB = rb, X = Vector3.Cross(y, way), Y = y, Z = way, Squash = squash, Blend = blend,
                Centre = (a + b) * 0.5f, Bound = (b - a).Length * 0.5f + reach + blend,
            });
        }

        public void Ellipsoid(SaurianPart part, Vector3 centre, Vector3 radii, float blend, Vector3? forward = null, Vector3? up = null, bool inHead = false, bool carve = false)
        {
            Vector3 z = (forward ?? Vector3.UnitZ).Normalized();
            Vector3 upWay = up ?? Vector3.UnitY;
            Vector3 y = upWay - z * Vector3.Dot(upWay, z);
            if (y.LengthSquared < 1e-6f) y = MathF.Abs(z.Y) < 0.9f ? Vector3.UnitY - z * z.Y : Vector3.UnitX - z * z.X;
            y.Normalize();
            Pieces.Add(new Piece
            {
                Shape = Shape.Ellipsoid, Part = part, InHead = inHead, Carve = carve, A = centre, Radii = radii, X = Vector3.Cross(y, z), Y = y, Z = z, Blend = blend,
                Centre = centre, Bound = MathF.Max(radii.X, MathF.Max(radii.Y, radii.Z)) + blend,
            });
        }

        public void JoinAt(SaurianPart part, Vector3 at, float blend, float reach) => Roots.Add(new Root { Part = part, At = at, Blend = blend, Reach = reach });

        // ---- Distances.

        public static float SmoothMin(float a, float b, float k)
        {
            if (k <= 1e-6f) return MathF.Min(a, b);
            float h = MathF.Max(k - MathF.Abs(a - b), 0f) / k;
            return MathF.Min(a, b) - h * h * k * 0.25f;
        }

        public static float SmoothMax(float a, float b, float k) => -SmoothMin(-a, -b, k);

        // Quilez's rounded cone, which is exact.
        private static float RoundCone(Vector3 p, float r1, float r2, float h)
        {
            float b = (r1 - r2) / h;
            float a = MathF.Sqrt(MathF.Max(1f - b * b, 0f));
            float qx = MathF.Sqrt(p.X * p.X + p.Y * p.Y);
            float k = -qx * b + p.Z * a;
            if (k < 0f) return MathF.Sqrt(qx * qx + p.Z * p.Z) - r1;
            if (k > a * h) return MathF.Sqrt(qx * qx + (p.Z - h) * (p.Z - h)) - r2;
            return qx * a + p.Z * b - r1;
        }

        // Quilez's ellipsoid, which is close enough near its surface, which is all that matters here.
        private static float Ellipse(Vector3 p, Vector3 r)
        {
            float k0 = new Vector3(p.X / r.X, p.Y / r.Y, p.Z / r.Z).Length;
            float k1 = new Vector3(p.X / (r.X * r.X), p.Y / (r.Y * r.Y), p.Z / (r.Z * r.Z)).Length;
            return k1 > 1e-9f ? k0 * (k0 - 1f) / k1 : -MathF.Min(r.X, MathF.Min(r.Y, r.Z));
        }

        private static float Distance(in Piece piece, Vector3 p)
        {
            Vector3 d = p - piece.A;
            Vector3 local = new Vector3(Vector3.Dot(d, piece.X), Vector3.Dot(d, piece.Y), Vector3.Dot(d, piece.Z));
            if (piece.Shape == Shape.Ellipsoid) return Ellipse(local, piece.Radii);

            // Squashed across, so a cone can be deeper than it's wide, as a tail and a neck are.
            local.X *= piece.Squash;
            float d0 = RoundCone(local, piece.RA, piece.RB, MathF.Max((piece.B - piece.A).Length, 1e-4f));
            return piece.Squash > 1f ? d0 / piece.Squash : d0;
        }

        /// <summary>How far <paramref name="p"/> is from the surface, and which piece of the body is nearest.</summary>
        public float Distance(Vector3 p, out SaurianPart part) => Distance(p, Pieces, out part, null);

        public float Distance(Vector3 p) => Distance(p, Pieces, out _, null);

        /// <summary>How far each part is, for a skin to blend its bones by.</summary>
        public void Parts(Vector3 p, Span<float> distances) => Distance(p, Pieces, out _, distances);

        private float Distance(Vector3 p, List<Piece> pieces, out SaurianPart nearest, Span<float> perPart)
        {
            Span<float> part = stackalloc float[PartCount];
            part.Fill(float.MaxValue);
            Vector3 inHead = Head != null ? Head.ToHead(p) : Vector3.Zero;

            foreach (Piece piece in pieces)
            {
                if (piece.Carve) continue;
                float d = Distance(piece, piece.InHead ? inHead : p);
                int i = (int)piece.Part;
                part[i] = part[i] == float.MaxValue ? d : SmoothMin(part[i], d, piece.Blend);
            }
            foreach (Piece piece in pieces)
            {
                if (!piece.Carve) continue;
                int i = (int)piece.Part;
                if (part[i] == float.MaxValue) continue;
                part[i] = SmoothMax(part[i], -Distance(piece, piece.InHead ? inHead : p), piece.Blend);
            }

            // The head is cut along the line of the mouth: the skull keeps what's above it and the jaw what's below.
            if (Head != null && part[(int)SaurianPart.Head] != float.MaxValue)
            {
                float lip = Head.Lip(inHead.Z);
                part[(int)SaurianPart.Head] = Jaw ? MathF.Max(part[(int)SaurianPart.Head], inHead.Y - (lip - Gap)) : MathF.Max(part[(int)SaurianPart.Head], lip - inHead.Y);
            }

            if (!perPart.IsEmpty) part.CopyTo(perPart);

            float result = part[(int)SaurianPart.Body];
            nearest = SaurianPart.Body;
            float best = result;
            for (int i = 1; i < PartCount; i++)
            {
                float d = part[i];
                if (d == float.MaxValue) continue;
                if (d < best)
                {
                    best = d;
                    nearest = (SaurianPart)i;
                }

                // A limb joins the body softly only near its root, and anywhere else it's only there.
                float blend = 0f;
                foreach (Root root in Roots)
                {
                    if ((int)root.Part != i) continue;
                    float near = 1f - Math.Clamp(((p - root.At).Length - root.Reach * 0.5f) / (root.Reach * 0.5f), 0f, 1f);
                    blend = MathF.Max(blend, root.Blend * near * near * (3f - 2f * near));
                }
                result = result == float.MaxValue ? d : SmoothMin(result, d, blend);
            }
            return result;
        }

        // ---- Sampling it.

        /// <summary>Everything that could have a surface, with room around it.</summary>
        public void Bounds(out Vector3 min, out Vector3 max)
        {
            min = new Vector3(float.MaxValue);
            max = new Vector3(float.MinValue);
            foreach (Piece piece in Pieces)
            {
                if (piece.Carve) continue;
                Vector3 centre = piece.InHead && Head != null ? Head.ToBody(piece.Centre) : piece.Centre;
                min = Vector3.ComponentMin(min, centre - new Vector3(piece.Bound));
                max = Vector3.ComponentMax(max, centre + new Vector3(piece.Bound));
            }
        }

        /// <summary>
        /// The field on a grid of <paramref name="cell"/>-sized steps over its bounds. Most of a grid is nowhere near the surface, so it's looked at a block at a time: a block further from the surface than it is
        /// wide is filled with its middle's value, and only the rest is worked out point by point, from the pieces that can reach it.
        /// </summary>
        public float[] Sample(float cell, out Vector3 origin, out int nx, out int ny, out int nz)
        {
            Bounds(out Vector3 min, out Vector3 max);
            min -= new Vector3(cell * 2f);
            max += new Vector3(cell * 2f);
            origin = min;
            nx = (int)MathF.Ceiling((max.X - min.X) / cell) + 1;
            ny = (int)MathF.Ceiling((max.Y - min.Y) / cell) + 1;
            nz = (int)MathF.Ceiling((max.Z - min.Z) / cell) + 1;
            var grid = new float[nx * ny * nz];

            const int Block = 6;
            float blockRadius = Block * cell * 0.87f;
            var near = new List<Piece>(Pieces.Count);
            for (int bz = 0; bz < nz; bz += Block)
            {
                for (int by = 0; by < ny; by += Block)
                {
                    for (int bx = 0; bx < nx; bx += Block)
                    {
                        Vector3 middle = min + (new Vector3(bx, by, bz) + new Vector3(Block * 0.5f)) * cell;
                        float value = Distance(middle);
                        bool clear = MathF.Abs(value) > blockRadius * 1.6f;

                        // Only what can reach this block can matter in it.
                        if (!clear)
                        {
                            near.Clear();
                            Vector3 middleInHead = Head != null ? Head.ToHead(middle) : middle;
                            foreach (Piece piece in Pieces)
                            {
                                Vector3 from = piece.InHead ? middleInHead : middle;
                                if ((from - piece.Centre).Length - piece.Bound < blockRadius * 2.2f) near.Add(piece);
                            }
                        }

                        for (int z = bz; z < Math.Min(bz + Block, nz); z++)
                        {
                            for (int y = by; y < Math.Min(by + Block, ny); y++)
                            {
                                for (int x = bx; x < Math.Min(bx + Block, nx); x++)
                                {
                                    int index = x + nx * (y + ny * z);
                                    grid[index] = clear ? value : Distance(min + new Vector3(x, y, z) * cell, near, out _, null);
                                }
                            }
                        }
                    }
                }
            }
            return grid;
        }

        /// <summary>Which way is out, from how the field rises.</summary>
        public Vector3 Normal(Vector3 p, float step)
        {
            Vector3 n = new Vector3(
                Distance(p + new Vector3(step, 0f, 0f)) - Distance(p - new Vector3(step, 0f, 0f)),
                Distance(p + new Vector3(0f, step, 0f)) - Distance(p - new Vector3(0f, step, 0f)),
                Distance(p + new Vector3(0f, 0f, step)) - Distance(p - new Vector3(0f, 0f, step)));
            return n.LengthSquared > 1e-12f ? n.Normalized() : Vector3.UnitY;
        }
    }
}

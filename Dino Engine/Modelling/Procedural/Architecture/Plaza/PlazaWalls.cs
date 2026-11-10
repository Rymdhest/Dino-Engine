using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture.Waygates;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Plaza
{
    /// <summary>How a wall is built.</summary>
    public enum WallForm
    {
        Field,      // Field stone in rough courses, big at the foot and smaller up, under flat copes: the walls of raised beds.
        Dressed,    // Ashlar in even courses on a plinth, under a moulded cope: a terrace's walls and a flight's cheeks.
        Seat,       // Ashlar under one broad cope at the height of a seat, all round a bed with a tree in it.
    }

    /// <summary>
    /// The line a wall's face follows: straight along +x, or round an arc about the origin, by bearing (clockwise from +z, seen from above). A point on it is given by how far
    /// along it is, how high, and how far back from the face, so a stone laid on an arc is cut to the arc, its joints running to the middle as a mason's would.
    /// </summary>
    internal readonly struct WallLine
    {
        public readonly float Length;
        private readonly bool _arc;
        private readonly float _radius;
        private readonly float _from;
        private readonly float _facing;

        private WallLine(float length, bool arc, float radius, float from, float facing)
        {
            Length = length;
            _arc = arc;
            _radius = radius;
            _from = from;
            _facing = facing;
        }

        public static WallLine Straight(float length) => new WallLine(length, false, 0f, 0f, 1f);

        /// <summary>An arc of <paramref name="radius"/> from bearing <paramref name="fromDegrees"/> through <paramref name="sweepDegrees"/>, its face towards the outside if <paramref name="facing"/> is 1 and the middle if -1.</summary>
        public static WallLine Arc(float radius, float fromDegrees, float sweepDegrees, float facing)
        {
            float from = MathHelper.DegreesToRadians(fromDegrees);
            float sweep = MathHelper.DegreesToRadians(sweepDegrees);
            return new WallLine(radius * MathF.Abs(sweep), true, radius, sweep >= 0f ? from : from + sweep, MathF.Sign(facing) == 0 ? 1f : MathF.Sign(facing));
        }

        /// <summary>Longest a straight stone can be laid here before it stands off the curve.</summary>
        public float LongestStone => _arc ? MathF.Max(0.3f, _radius * 0.42f) : float.MaxValue;

        public Vector3 At(float along, float back, float y)
        {
            if (!_arc) return new Vector3(along, y, -back);
            float theta = _from + along / _radius;
            float r = MathF.Max(_radius - _facing * back, 0.05f);
            return new Vector3(MathF.Sin(theta) * r, y, MathF.Cos(theta) * r);
        }

        /// <summary>The greatest distance back from the face a point can be before it passes the arc's middle.</summary>
        public float DeepestBack => _arc && _facing > 0f ? _radius - 0.15f : float.MaxValue;
    }

    /// <summary>
    /// Walls of the square, stone by stone (<see cref="Masonry"/>), each touching its neighbours face to face with its arrises taken off, so the joints read as joints from the
    /// game's camera with nothing coplanar anywhere. A wall stands from below the ground to <c>top</c>; one that holds up a bed or a terrace has its soil at <c>soil</c> (where people
    /// would stand, so the ground's drawn a little above it), and a soil bank along its back that hides where the ground was raised (SQUARE.md). Headless.
    /// </summary>
    internal static class PlazaWalls
    {
        private const float Foot = -0.5f;           // Metres below where people stand that a wall's stones start.
        private const float BermReach = 1.85f;      // Metres back from a wall that its soil bank runs, over where the raised ground meets the wall.

        public static void Build(MeshBuilder b, WallLine line, WallForm form, float top, float soil, float thick, uint seed)
        {
            var random = new WorldRandom(seed).Fork("wall");
            switch (form)
            {
                case WallForm.Field: Field(b, line, top, soil, thick, random); break;
                case WallForm.Dressed: Dressed(b, line, top, soil, thick, random); break;
                case WallForm.Seat: Seat(b, line, top, soil, thick, random); break;
            }
            if (soil >= 0f) Berm(b, line, soil, thick);
        }

        // ---------------------------------------------------------------- Bands

        /// <summary>
        /// Stones laid in the paving along the line, <paramref name="width"/> across it and centred on it, standing a finger proud of the drawn ground so that they read as laid
        /// stone and not as a painted stripe: a band of long kerbs (<paramref name="gutter"/> false), or a gutter, a kerb each side of a run of dished stones that carries the rain off.
        /// </summary>
        public static void Band(MeshBuilder b, WallLine line, bool gutter, float width, uint seed)
        {
            var random = new WorldRandom(seed).Fork("band");
            float ground = PlazaStone.Ground;
            if (!gutter)
            {
                Flush(b, line, -width * 0.5f, width * 0.5f, ground + 0.03f, 1.05f, random.Fork("kerbs"), 0.045f, 0.82f);
                return;
            }
            const float Kerb = 0.24f;
            Flush(b, line, -width * 0.5f, -width * 0.5f + Kerb, ground + 0.045f, 0.95f, random.Fork("near"), 0.04f, 0.86f);
            Flush(b, line, width * 0.5f - Kerb, width * 0.5f, ground + 0.045f, 0.95f, random.Fork("far"), 0.04f, 0.86f);
            Flush(b, line, -width * 0.5f + Kerb + 0.01f, width * 0.5f - Kerb - 0.01f, ground + 0.008f, 0.62f, random.Fork("channel"), 0.05f, 0.66f);
        }

        // A run of flush stones between two offsets across the line, each about `length` long, their tops at `top`, darker than the paving by `shade`.
        private static void Flush(MeshBuilder b, WallLine line, float near, float far, float top, float length, WorldRandom random, float chamfer, float shade)
        {
            for (float along = -random.Range(0f, length); along < line.Length;)
            {
                float piece = MathF.Min(length * random.Range(0.7f, 1.3f), line.LongestStone);
                float from = MathF.Max(along, 0f), to = MathF.Min(along + piece, line.Length);
                if (to - from > 0.08f)
                {
                    VertexMaterial stone = PlazaStone.Tint(PlazaStone.Dressed(random, 0.4f, 0.06f), new Vector3(shade));
                    StoneBetween(b, line, from + 0.008f, to - 0.008f, top - 0.32f, top + random.Range(-0.006f, 0.006f), near, far, 0f, chamfer, random, _ => stone);
                }
                along += piece;
            }
        }

        // ---------------------------------------------------------------- Field stone

        private static void Field(MeshBuilder b, WallLine line, float top, float soil, float thick, WorldRandom random)
        {
            float copeHeight = random.Range(0.15f, 0.2f);
            float body = top - copeHeight;
            const float Batter = 0.05f;     // Metres the face leans back for every metre it rises.
            const float JointWide = 0.028f, JointHigh = 0.02f;
            float moss = random.Range(0.25f, 0.45f);

            // The hearting, which is all that's seen in the joints.
            Hearting(b, line, Foot, body, 0.1f, Batter);
            if (soil < 0f) Hearting(b, line, Foot, body, thick - 0.1f, -Batter);

            foreach (bool front in soil < 0f ? new[] { true, false } : new[] { true })
            {
                WorldRandom side = random.Fork(front ? "front" : "back");
                float y = Foot;
                while (y < body - 0.08f)
                {
                    float rise = MathHelper.Lerp(0.42f, 0.26f, Math.Clamp((y - Foot) / MathF.Max(body - Foot, 0.5f), 0f, 1f)) * side.Range(0.85f, 1.15f);
                    if (body - (y + rise) < 0.14f) rise = body - y;
                    float along = -side.Range(0f, 0.3f);
                    while (along < line.Length)
                    {
                        float length = MathF.Min(side.Range(0.42f, 1.02f) * (y < 0.2f ? 1.15f : 1f), line.LongestStone);
                        float from = MathF.Max(along, 0f), to = MathF.Min(along + length, line.Length);
                        if (to - from > 0.12f)
                        {
                            float depth = MathF.Min(side.Range(0.38f, 0.62f), thick * 0.6f);
                            float proud = side.Range(-0.02f, 0.035f);
                            FieldStone(b, line, front, thick, from + JointWide * 0.5f, to - JointWide * 0.5f, y + JointHigh * 0.5f, y + rise - JointHigh * 0.5f, -proud, depth, Batter, side,
                                PlazaStone.Rubble(side, y, moss * 0.4f));
                        }
                        along += length;
                    }
                    y += rise;
                }
            }

            // Copes: flat slabs across the whole wall and a little over each face.
            WorldRandom copes = random.Fork("copes");
            float slab = copes.Range(0.5f, 0.6f);
            for (float along = -copes.Range(0f, 0.2f); along < line.Length;)
            {
                float length = MathF.Min(slab * copes.Range(0.8f, 1.5f), line.LongestStone);
                float from = MathF.Max(along, 0f), to = MathF.Min(along + length, line.Length);
                if (to - from > 0.12f)
                {
                    float lift = copes.Range(-0.018f, 0.012f);
                    float back = soil < 0f ? thick + 0.06f : thick + 0.03f;
                    StoneBetween(b, line, from + 0.012f, to - 0.012f, body - 0.02f, top + lift, -0.065f + copes.Range(-0.015f, 0.015f), back, copes.Range(0.012f, 0.022f),
                        0.05f, copes, PlazaStone.Rubble(copes, top, moss));
                }
                along += length;
            }
        }

        // A field stone: a rough block whose corners are pushed about a little, so no two are alike and none is a box.
        private static void FieldStone(MeshBuilder b, WallLine line, bool front, float thick, float from, float to, float bottom, float top, float face, float depth, float batter,
            WorldRandom random, Func<Vector3, VertexMaterial> facing)
        {
            float length = to - from, rise = top - bottom;
            float Jitter(float of) => random.Range(-of, of);
            float[] s = { from + Jitter(length * 0.06f), to + Jitter(length * 0.06f), to + Jitter(length * 0.06f), from + Jitter(length * 0.06f) };
            float[] y = { bottom + Jitter(rise * 0.08f), bottom + Jitter(rise * 0.08f), top + Jitter(rise * 0.1f), top + Jitter(rise * 0.1f) };
            float Back(float atY, float w) => front ? w + batter * (atY - Foot) : thick - w - batter * (atY - Foot);
            var corners = new Vector3[8];
            for (int k = 0; k < 4; k++)
            {
                float w = face + Jitter(0.015f);
                corners[k] = line.At(s[k], Back(y[k], w), y[k]);
                corners[k + 4] = line.At(s[k] + Jitter(0.03f), Back(y[k], face + depth), y[k] + Jitter(0.02f));
            }
            Lay(b, corners, MathF.Min(0.075f, MathF.Min(length, rise) * 0.3f), facing);
        }

        // ---------------------------------------------------------------- Dressed stone

        private static void Dressed(MeshBuilder b, WallLine line, float top, float soil, float thick, WorldRandom random)
        {
            const float Plinth = 0.44f, PlinthProud = 0.07f;
            const float Mould = 0.09f, Cope = 0.19f;
            float body = top - Mould - Cope;
            bool parapet = soil < 0f || top - (soil + PlazaStone.Ground) > 0.12f;
            float backProud = parapet ? 0.1f : 0.02f;

            // The plinth, in long blocks.
            WorldRandom plinth = random.Fork("plinth");
            Course(b, line, Foot, Plinth, -PlinthProud, thick + (soil < 0f ? PlinthProud : 0f), 1.5f, plinth.Range(0f, 0.6f), plinth, PlazaStone.Chamfer, 0.05f);

            // Even courses above it, each set half a block along from the one below.
            int courses = Math.Max(1, (int)MathF.Round((body - Plinth) / 0.4f));
            float rise = (body - Plinth) / courses;
            float stagger = random.Range(0f, 1.2f);
            for (int c = 0; c < courses; c++)
            {
                WorldRandom course = random.Fork(c);
                float y0 = Plinth + c * rise;
                Course(b, line, y0, y0 + rise, 0f, thick, 1.25f, stagger + (c % 2) * 0.62f, course, PlazaStone.Chamfer, 0.06f);
            }

            // A bed mould, and the cope over it, proud of both faces where the wall stands above what it holds.
            Course(b, line, body, body + Mould, -0.045f, thick + (parapet ? 0.045f : 0f), 1.6f, random.Range(0f, 0.8f), random.Fork("mould"), 0.03f, 0.04f);
            Course(b, line, body + Mould, top, -0.12f, thick + backProud, 1.45f, random.Range(0f, 0.8f), random.Fork("cope"), PlazaStone.BigChamfer, 0.035f, moss: 0.4f);
        }

        private static void Seat(MeshBuilder b, WallLine line, float top, float soil, float thick, WorldRandom random)
        {
            const float Cope = 0.17f;
            float body = top - Cope;
            int courses = Math.Max(1, (int)MathF.Round((body - Foot) / 0.36f));
            float rise = (body - Foot) / courses;
            float stagger = random.Range(0f, 1f);
            for (int c = 0; c < courses; c++)
            {
                float y0 = Foot + c * rise;
                Course(b, line, y0, y0 + rise, 0f, thick, 1.05f, stagger + (c % 2) * 0.52f, random.Fork(c), PlazaStone.Chamfer, 0.055f);
            }
            // One broad cope to sit on, rounded at its edges.
            Course(b, line, body, top, -0.11f, thick + 0.09f, 1.2f, random.Range(0f, 0.6f), random.Fork("cope"), 0.07f, 0.03f, moss: 0.25f);
        }

        // A course of dressed blocks between two heights and two depths, each about `length` long, starting `offset` along.
        private static void Course(MeshBuilder b, WallLine line, float bottom, float top, float face, float back, float length, float offset, WorldRandom random, float chamfer,
            float spread, float moss = 0f)
        {
            float along = -(offset % length);
            while (along < line.Length)
            {
                float piece = MathF.Min(length * random.Range(0.75f, 1.25f), line.LongestStone);
                float from = MathF.Max(along, 0f), to = MathF.Min(along + piece, line.Length);
                if (to - from > 0.1f)
                {
                    VertexMaterial plain = PlazaStone.Dressed(random, bottom, spread);
                    Func<Vector3, VertexMaterial> facing = moss > 0f ? PlazaStone.DressedFacing(random, bottom, moss, spread) : _ => plain;
                    StoneBetween(b, line, from + 0.004f, to - 0.004f, bottom, top, face, back, 0f, chamfer, random, facing);
                }
                along += piece;
            }
        }

        // A stone filling a stretch of the line between two heights and two depths, its top tilted by up to `tilt`.
        internal static void StoneBetween(MeshBuilder b, WallLine line, float from, float to, float bottom, float top, float face, float back, float tilt, float chamfer, WorldRandom random,
            Func<Vector3, VertexMaterial> facing)
        {
            float t0 = tilt > 0f ? random.Range(-tilt, tilt) : 0f, t1 = tilt > 0f ? random.Range(-tilt, tilt) : 0f;
            Lay(b, new[]
            {
                line.At(from, face, bottom), line.At(to, face, bottom), line.At(to, face, top + t1), line.At(from, face, top + t0),
                line.At(from, back, bottom), line.At(to, back, bottom), line.At(to, back, top + t1), line.At(from, back, top + t0),
            }, chamfer, facing);
        }

        /// <summary>
        /// Lays a block from its corners: the four of its face, as seen from in front of it going along and then up, then the four behind them. A wall's face can look either
        /// way, so whichever way round they come is turned to the way <see cref="Masonry.Block(MeshBuilder, Vector3[], float, Func{Vector3, VertexMaterial})"/> wants them.
        /// </summary>
        public static void Lay(MeshBuilder b, Vector3[] c, float chamfer, Func<Vector3, VertexMaterial> facing)
        {
            Vector3 front = (c[0] + c[1] + c[2] + c[3]) * 0.25f, behind = (c[4] + c[5] + c[6] + c[7]) * 0.25f;
            Vector3 normal = Vector3.Cross(c[2] - c[0], c[3] - c[1]);
            if (Vector3.Dot(normal, front - behind) < 0f)
            {
                (c[0], c[1]) = (c[1], c[0]);
                (c[2], c[3]) = (c[3], c[2]);
                (c[4], c[5]) = (c[5], c[4]);
                (c[6], c[7]) = (c[7], c[6]);
            }
            Masonry.Block(b, c, chamfer, facing);
        }

        // The dark fill behind a wall's face stones, set back from the face so it only shows between them.
        private static void Hearting(MeshBuilder b, WallLine line, float bottom, float top, float back, float batter)
        {
            VertexMaterial dark = PlazaStone.Hearting;
            int segments = Math.Max(1, (int)MathF.Ceiling(line.Length / 0.5f));
            for (int i = 0; i < segments; i++)
            {
                float s0 = line.Length * i / segments, s1 = line.Length * (i + 1) / segments;
                Vector3 a = line.At(s0, back + batter * (bottom - Foot), bottom), c = line.At(s1, back + batter * (bottom - Foot), bottom);
                Vector3 d = line.At(s1, back + batter * (top - Foot), top), e = line.At(s0, back + batter * (top - Foot), top);
                // Wound to face away from the wall's middle, whichever side it's on.
                Vector3 inward = line.At((s0 + s1) * 0.5f, back + (batter >= 0f ? 0.3f : -0.3f), (bottom + top) * 0.5f) - (a + c + d + e) * 0.25f;
                Vector3 normal = Vector3.Cross(d - a, e - c);
                if (Vector3.Dot(normal, inward) > 0f) b.Quad(c, a, e, d, dark);
                else b.Quad(a, c, d, e, dark);
            }
        }

        // Ground laid against a wall's back: as high as the ground is drawn at the wall and going under it a couple of metres in, over where the raised ground meets the wall,
        // which a metre's grid of heights can only lay as a slope. It's mossy and green, as the edge of a bed is, and the bed's grass grows up through it.
        private static void Berm(MeshBuilder b, WallLine line, float soil, float thick)
        {
            float near = thick - 0.04f, far = MathF.Min(thick + BermReach, line.DeepestBack);
            float high = soil + PlazaStone.Ground + 0.03f, low = soil + PlazaStone.Ground - 0.24f;
            VertexMaterial earth = PlazaStone.BedEdge;
            int segments = Math.Max(1, (int)MathF.Ceiling(line.Length / 0.6f));
            for (int i = 0; i < segments; i++)
            {
                float s0 = line.Length * i / segments, s1 = line.Length * (i + 1) / segments;
                float mid = (near + far) * 0.5f;
                Vector3 a = line.At(s0, near, high), c = line.At(s1, near, high), d = line.At(s1, mid, high - 0.08f), e = line.At(s0, mid, high - 0.08f);
                Vector3 f = line.At(s1, far, low), g = line.At(s0, far, low);
                UpQuad(b, a, c, d, e, earth);
                UpQuad(b, e, d, f, g, earth);
            }
        }

        private static void UpQuad(MeshBuilder b, Vector3 a, Vector3 c, Vector3 d, Vector3 e, VertexMaterial material)
        {
            Vector3 normal = Vector3.Cross(d - a, e - c);
            if (normal.Y < 0f) b.Quad(c, a, e, d, material);
            else b.Quad(a, c, d, e, material);
        }
    }
}

using Dino_Engine.Modelling.Model;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Halls
{
    /// <summary>
    /// The roofs of the halls, which are most of what anyone sees of a building from the game's camera, so they get the care: courses of shingle or slate weathered in soft patches, a real
    /// thickness at the eaves, carved boards along the gables, dormers and chimneys. Everything here stands above <see cref="HallDesign.WallTop"/> and goes when someone's inside.
    /// </summary>
    internal static class HallRoofs
    {
        private const float Thickness = 0.42f;      // Of a roof, from its underside to its face.
        private const float WallPlate = 0.3f;       // Metres the underside of a roof clears the top of the walls.

        /// <summary>A gable roof's shape: its ridge along x, and where everything about it is.</summary>
        internal readonly struct Gable
        {
            public readonly float HalfX;    // Of the walls.
            public readonly float HalfZ;
            public readonly float Base;     // Height of the underside where it crosses the outside of the walls.
            public readonly float Pitch;
            public readonly float Eaves;    // How far it reaches past the front and back walls.
            public readonly float Verge;    // And past the ends.

            public Gable(float halfX, float halfZ, float wallTop, float pitch, float eaves, float verge)
            {
                HalfX = halfX;
                HalfZ = halfZ;
                Base = wallTop + WallPlate;
                Pitch = pitch;
                Eaves = eaves;
                Verge = verge;
            }

            public float Ridge => Base + HalfZ * Pitch;
            public float EaveY => Base - Eaves * Pitch;
            public float Lift => Thickness * MathF.Sqrt(1f + Pitch * Pitch);

            /// <summary>The height of the roof's face over a point at <paramref name="z"/>.</summary>
            public float FaceAt(float z) => Base + (HalfZ - MathF.Abs(z)) * Pitch + Lift;
        }

        /// <summary>How a roof weathers: lichen and moss in soft patches, a little darker towards the eaves where the rain runs off.</summary>
        public static Func<Vector3, Colour> Weathering(VertexMaterial roof, Vector3 moss, float eaveY, float ridgeY, int seed)
        {
            Vector3 colour = roof.colour.ToVector3();
            float s = seed * 0.137f;
            return p =>
            {
                float patch = 0.5f + 0.5f * MathF.Sin(p.X * 0.31f + s) * MathF.Cos(p.Z * 0.43f - s * 1.7f) + 0.25f * MathF.Sin((p.X + p.Z) * 0.9f + s * 3.1f);
                patch = Math.Clamp((patch - 0.55f) * 2.2f, 0f, 1f);
                float down = Math.Clamp((ridgeY - p.Y) / MathF.Max(ridgeY - eaveY, 0.1f), 0f, 1f);
                Vector3 c = colour * Vector3.Lerp(Vector3.One, moss, patch) * (1.04f - 0.1f * down * down);
                return new Colour(c.X, c.Y, c.Z);
            };
        }

        /// <summary>
        /// A gable roof over walls <paramref name="g"/> describes: two slopes of <paramref name="kit"/>'s roof in a grid so that they can weather, the cut ends of the roof at the eaves and up
        /// the verges, boards along the verges and the eaves, the gable walls up to the underside, and a ridge with finials. <paramref name="gableWall"/> is what the gables are faced with.
        /// </summary>
        public static void GableRoof(MeshBuilder b, in Gable g, HallKit kit, VertexMaterial gableWall, VertexMaterial gableInner, float wallThickness, int seed, bool finials = true)
        {
            float x0 = -g.HalfX - g.Verge, x1 = g.HalfX + g.Verge;
            float zEave = g.HalfZ + g.Eaves;
            float eave = g.EaveY + g.Lift;
            float ridge = g.Ridge + g.Lift;
            var tint = Weathering(kit.Roof, kit.Moss, eave, ridge, seed);

            // The two slopes, each in a grid so that the weathering can vary across it.
            float slope = MathF.Sqrt(zEave * zEave + (ridge - eave) * (ridge - eave));
            foreach (float side in new[] { 1f, -1f })
            {
                Vector3 v = new Vector3(0f, ridge - eave, -zEave * side) / slope;   // Up the slope.
                Vector3 u = side > 0f ? Vector3.UnitX : -Vector3.UnitX;
                Vector3 origin = new Vector3(0f, eave, zEave * side);
                Vector4 bounds = new Vector4(x0, x1, 0f, slope);
                const int Across = 10, Up = 5;
                for (int i = 0; i < Across; i++)
                {
                    for (int j = 0; j < Up; j++)
                    {
                        float ua = x0 + (x1 - x0) * i / Across, ub = x0 + (x1 - x0) * (i + 1) / Across;
                        float va = slope * j / Up, vb = slope * (j + 1) / Up;
                        // u runs left to right seen from outside, so the back slope's runs from +x.
                        float pa = side > 0f ? ua : -ub, pb = side > 0f ? ub : -ua;
                        Vector3 c00 = origin + u * pa + v * va, c10 = origin + u * pb + v * va, c11 = origin + u * pb + v * vb, c01 = origin + u * pa + v * vb;
                        b.PlaneQuad(c00, c10, c11, c01, kit.Roof, origin, u, v, new Vector4(side > 0f ? x0 : -x1, side > 0f ? x1 : -x0, 0f, slope), tint);
                    }
                }

                // The cut end of the roof along the eaves, with a fascia board under it.
                float z = zEave * side;
                Vector3 e0 = new Vector3(x0, eave - g.Lift, z), e1 = new Vector3(x1, eave - g.Lift, z), e2 = new Vector3(x1, eave, z), e3 = new Vector3(x0, eave, z);
                if (side > 0f) b.Quad(e0, e1, e2, e3, kit.Roof);
                else b.Quad(e1, e0, e3, e2, kit.Roof);
                b.Box(new Vector3(0f, eave - g.Lift - 0.12f, z - side * 0.06f), new Vector3((x1 - x0) * 0.5f, 0.2f, 0.07f), kit.Soffit);

                // The underside where it reaches past the walls, boarded.
                Vector3 s0 = new Vector3(x0, g.EaveY, z), s1 = new Vector3(x1, g.EaveY, z), s2 = new Vector3(x1, g.Base, g.HalfZ * side), s3 = new Vector3(x0, g.Base, g.HalfZ * side);
                if (side > 0f) b.Quad(s1, s0, s3, s2, kit.Soffit);
                else b.Quad(s0, s1, s2, s3, kit.Soffit);
            }

            // Up the verges at either end: the cut end of the roof, and a carved board over it.
            foreach (float end in new[] { -1f, 1f })
            {
                float x = end > 0f ? x1 : x0;
                foreach (float side in new[] { 1f, -1f })
                {
                    Vector3 lo = new Vector3(x, eave - g.Lift, zEave * side), hi = new Vector3(x, ridge - g.Lift, 0f);
                    Vector3 loTop = lo + Vector3.UnitY * g.Lift, hiTop = hi + Vector3.UnitY * g.Lift;
                    Vector3 normal = Vector3.Cross(hi - lo, loTop - lo);
                    if (Vector3.Dot(normal, Vector3.UnitX * end) > 0f) b.Quad(lo, hi, hiTop, loTop, kit.Roof);
                    else b.Quad(hi, lo, loTop, hiTop, kit.Roof);
                    Bargeboard(b, new Vector3(x + end * 0.08f, 0f, 0f), lo + Vector3.UnitY * 0.1f, hi + Vector3.UnitY * 0.1f, kit.Soffit);
                }
                // The underside under the verge, past the gable wall.
                Vector3 u0 = new Vector3(end * g.HalfX, g.EaveY, zEave), u1 = new Vector3(x, g.EaveY, zEave), u2 = new Vector3(x, g.Ridge, 0f), u3 = new Vector3(end * g.HalfX, g.Ridge, 0f);
                b.Quad(u0, u1, u2, u3, kit.Soffit);
                b.Quad(u1, u0, u3, u2, kit.Soffit);
                Vector3 w0 = new Vector3(end * g.HalfX, g.EaveY, -zEave), w1 = new Vector3(x, g.EaveY, -zEave);
                b.Quad(w1, w0, u3, u2, kit.Soffit);
                b.Quad(w0, w1, u2, u3, kit.Soffit);

                GableWall(b, g, end, gableWall, gableInner, wallThickness);
            }

            // The ridge: a rounded cap all along it, and a finial at either end.
            b.Tube(new Vector3(x0 - 0.15f, ridge + 0.02f, 0f), new Vector3(x1 + 0.15f, ridge + 0.02f, 0f), 0.34f, 0.34f, 8, kit.Ridge);
            if (!finials) return;
            foreach (float end in new[] { -1f, 1f })
            {
                Vector3 foot = new Vector3(end * (x1 + 0.1f), ridge - 0.3f, 0f);
                Hall.Turned(b, foot, kit.Soffit, 6, (0f, 0.16f), (0.5f, 0.14f), (0.62f, 0.24f), (0.9f, 0.1f), (1.4f, 0.17f), (1.75f, 0.02f));
            }
        }

        // A board along a verge with its lower edge cut into scallops, the way a carpenter finishes a gable.
        private static void Bargeboard(MeshBuilder b, Vector3 plane, Vector3 lo, Vector3 hi, VertexMaterial material)
        {
            Vector3 along = hi - lo;
            float length = along.Length;
            Vector3 dir = along / length;
            Vector3 down = Vector3.Cross(dir, Vector3.UnitX).Normalized();
            if (down.Y > 0f) down = -down;
            int scallops = Math.Max(2, (int)(length / 0.9f));
            for (int i = 0; i < scallops; i++)
            {
                Vector3 a = lo + dir * (length * i / scallops);
                Vector3 c = lo + dir * (length * (i + 1) / scallops);
                Vector3 mid = (a + c) * 0.5f;
                Vector3 depthA = down * 0.42f, depthMid = down * 0.6f;
                Vector3 a0 = new Vector3(plane.X, a.Y, a.Z), c0 = new Vector3(plane.X, c.Y, c.Z), m0 = new Vector3(plane.X, mid.Y, mid.Z);
                b.Quad(a0, c0, c0 + depthA, a0 + depthA, material);
                b.Triangle(a0 + depthA, c0 + depthA, m0 + depthMid, material);
            }
        }

        // The triangle of wall at one end of a gable roof, from the top of the walls up to the roof's underside, thick as the walls.
        private static void GableWall(MeshBuilder b, in Gable g, float end, VertexMaterial outer, VertexMaterial inner, float thickness)
        {
            float top = g.Base - WallPlate;
            float x = end * g.HalfX;
            float xi = end * (g.HalfX - thickness);
            Vector3 a = new Vector3(x, top, -g.HalfZ), c = new Vector3(x, top, g.HalfZ), peak = new Vector3(x, g.Ridge - 0.05f, 0f);
            Vector3 origin = new Vector3(x, 0f, 0f);
            Vector3 u = end > 0f ? -Vector3.UnitZ : Vector3.UnitZ;
            Vector4 bounds = new Vector4(-g.HalfZ, g.HalfZ, top, g.Ridge);
            // Seen from outside, counter-clockwise.
            if (end > 0f) b.PlaneTriangle(c, a, peak, outer, origin, u, Vector3.UnitY, bounds);
            else b.PlaneTriangle(a, c, peak, outer, origin, u, Vector3.UnitY, bounds);
            Vector3 ai = new Vector3(xi, top, -g.HalfZ + thickness), ci = new Vector3(xi, top, g.HalfZ - thickness), peakIn = new Vector3(xi, g.Ridge - 0.05f - thickness * g.Pitch, 0f);
            Vector3 originIn = new Vector3(xi, 0f, 0f);
            if (end > 0f) b.PlaneTriangle(ai, ci, peakIn, inner, originIn, -u, Vector3.UnitY, bounds);
            else b.PlaneTriangle(ci, ai, peakIn, inner, originIn, -u, Vector3.UnitY, bounds);
        }

        /// <summary>
        /// A hipped roof: four slopes up to a short ridge, with its hips capped. <paramref name="halfX"/> must be the longer side. Returns the ridge's height, for whatever stands on it.
        /// </summary>
        public static float HipRoof(MeshBuilder b, float halfX, float halfZ, float wallTop, float pitch, float eaves, HallKit kit, int seed)
        {
            float baseY = wallTop + WallPlate;
            float lift = Thickness * MathF.Sqrt(1f + pitch * pitch);
            float ex = halfX + eaves, ez = halfZ + eaves;
            float eave = baseY - eaves * pitch + lift;
            float ridge = baseY + halfZ * pitch + lift;
            float ridgeHalf = MathF.Max(halfX - halfZ, 0.5f);
            var tint = Weathering(kit.Roof, kit.Moss, eave, ridge, seed);

            Vector3 r0 = new Vector3(-ridgeHalf, ridge, 0f), r1 = new Vector3(ridgeHalf, ridge, 0f);
            Vector3 fl = new Vector3(-ex, eave, ez), fr = new Vector3(ex, eave, ez), br = new Vector3(ex, eave, -ez), bl = new Vector3(-ex, eave, -ez);

            // The long slopes, front and back, as grids so that they weather.
            Slope(b, fl, fr, r1, r0, kit.Roof, tint, 10, 6);
            Slope(b, br, bl, r0, r1, kit.Roof, tint, 10, 6);
            // The hipped ends.
            Slope(b, fr, br, r1, r1, kit.Roof, tint, 6, 6);
            Slope(b, bl, fl, r0, r0, kit.Roof, tint, 6, 6);

            // The cut edge of the roof all round the eaves, and a fascia.
            Vector3[] corners = { fl, fr, br, bl };
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = corners[i], c = corners[(i + 1) % 4];
                Vector3 a0 = a - Vector3.UnitY * lift, c0 = c - Vector3.UnitY * lift;
                b.Quad(a0, c0, c, a, kit.Roof);
                Vector3 outward = Vector3.Cross(c - a, Vector3.UnitY).Normalized();
                Vector3 mid = (a0 + c0) * 0.5f - Vector3.UnitY * 0.12f - outward * 0.06f;
                float length = (c - a).Length;
                Vector3 dir = (c - a) / length;
                b.Beam(mid - dir * length * 0.5f, mid + dir * length * 0.5f, 0.12f, kit.Soffit);

                // The underside past the walls.
                Vector3 wa = new Vector3(MathF.Sign(a.X) * halfX, baseY, MathF.Sign(a.Z) * halfZ), wc = new Vector3(MathF.Sign(c.X) * halfX, baseY, MathF.Sign(c.Z) * halfZ);
                b.Quad(c0, a0, wa, wc, kit.Soffit);
            }

            // Caps along the ridge and down the hips.
            b.Tube(r0 - Vector3.UnitX * 0.2f, r1 + Vector3.UnitX * 0.2f, 0.32f, 0.32f, 8, kit.Ridge);
            foreach ((Vector3 top, Vector3 corner) in new[] { (r1, fr), (r1, br), (r0, fl), (r0, bl) })
            {
                b.Tube(top + Vector3.UnitY * 0.02f, corner + Vector3.UnitY * 0.04f, 0.26f, 0.24f, 7, kit.Ridge);
            }
            return ridge;
        }

        // A flat slope from its eave (a, c) up to its ridge (d, e: e above c, d above a), cut into a grid of quads so that its colour can vary.
        private static void Slope(MeshBuilder b, Vector3 a, Vector3 c, Vector3 e, Vector3 d, VertexMaterial material, Func<Vector3, Colour> tint, int across, int up)
        {
            Vector3 u = (c - a).Normalized();
            Vector3 normal = Vector3.Cross(c - a, d - a).Normalized();
            Vector3 v = Vector3.Cross(normal, u).Normalized();
            Vector3 origin = a;
            float width = (c - a).Length;
            float height = Vector3.Dot(d - a, v);
            Vector4 bounds = new Vector4(0f, width, 0f, height);
            for (int j = 0; j < up; j++)
            {
                float t0 = (float)j / up, t1 = (float)(j + 1) / up;
                Vector3 l0 = Vector3.Lerp(a, d, t0), l1 = Vector3.Lerp(a, d, t1);
                Vector3 r0 = Vector3.Lerp(c, e, t0), r1 = Vector3.Lerp(c, e, t1);
                for (int i = 0; i < across; i++)
                {
                    float s0 = (float)i / across, s1 = (float)(i + 1) / across;
                    Vector3 p00 = Vector3.Lerp(l0, r0, s0), p10 = Vector3.Lerp(l0, r0, s1), p11 = Vector3.Lerp(l1, r1, s1), p01 = Vector3.Lerp(l1, r1, s0);
                    if ((p10 - p00).LengthSquared < 1e-6f && (p11 - p01).LengthSquared < 1e-6f) continue;
                    b.PlaneQuad(p00, p10, p11, p01, material, origin, u, v, bounds, tint);
                }
            }
        }

        /// <summary>
        /// A dormer on the front slope of a gable roof: a little gabled window standing out of the roof, its own roof running back into the main one. <paramref name="along"/> is where it is
        /// along the ridge, <paramref name="out_"/> how far forward of the ridge its face stands.
        /// </summary>
        public static void Dormer(MeshBuilder b, in Gable g, HallKit kit, VertexMaterial cheeks, float along, float out_, float halfWidth, float wallHeight, List<AssetLight>? lights, int seed)
        {
            float foot = g.FaceAt(out_) - 0.2f;
            float top = foot + wallHeight;
            float pitch = 0.95f;
            float rise = halfWidth * pitch;
            float overhang = 0.45f;
            float roofLift = 0.28f;

            // Its face, with a window, and the little gable over it.
            Vector3 origin = new Vector3(0f, 0f, out_);
            Vector4 bounds = new Vector4(along - halfWidth, along + halfWidth, foot, top + rise);
            b.Panel(origin, Vector3.UnitX, Vector3.UnitY, along - halfWidth, along + halfWidth, foot, top, cheeks, bounds);
            b.PlaneTriangle(new Vector3(along - halfWidth, top, out_), new Vector3(along + halfWidth, top, out_), new Vector3(along, top + rise, out_), cheeks, origin, Vector3.UnitX, Vector3.UnitY, bounds);
            float windowHalf = halfWidth * 0.55f;
            float sill = foot + wallHeight * 0.18f, head = top - wallHeight * 0.12f;
            // Lit inside, as attics with someone in them are of an evening.
            b.Box(new Vector3(along, (sill + head) * 0.5f, out_ + 0.03f), new Vector3(windowHalf, (head - sill) * 0.5f, 0.04f), Hall.Glow(1f, 0.6f, 0.28f, 1.5f));
            b.Box(new Vector3(along, (sill + head) * 0.5f, out_ + 0.1f), new Vector3(0.05f, (head - sill) * 0.5f, 0.05f), kit.Joinery);
            b.Box(new Vector3(along, (sill + head) * 0.5f, out_ + 0.1f), new Vector3(windowHalf, 0.05f, 0.05f), kit.Joinery);
            foreach (float y in new[] { sill, head }) b.Box(new Vector3(along, y, out_ + 0.1f), new Vector3(windowHalf + 0.1f, 0.07f, 0.08f), kit.Joinery);
            foreach (float x in new[] { -windowHalf, windowHalf }) b.Box(new Vector3(along + x, (sill + head) * 0.5f, out_ + 0.1f), new Vector3(0.07f, (head - sill) * 0.5f, 0.08f), kit.Joinery);

            // Its sides, running back to where they meet the roof.
            float back = g.HalfZ - (top - g.Base - g.Lift) / g.Pitch;
            foreach (float side in new[] { -1f, 1f })
            {
                float x = along + side * halfWidth;
                Vector3 f0 = new Vector3(x, foot, out_), f1 = new Vector3(x, top, out_), k = new Vector3(x, top, back);
                if (side > 0f) b.Triangle(f0, k, f1, cheeks);
                else b.Triangle(f0, f1, k, cheeks);
            }

            // Its roof, its two slopes run back into the main roof, which hides where they go under it.
            float ridgeBack = g.HalfZ - (top + rise - g.Base - g.Lift) / g.Pitch - 0.5f;
            var tint = Weathering(kit.Roof, kit.Moss, top, top + rise, seed);
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 eaveFront = new Vector3(along + side * (halfWidth + overhang), top - overhang * pitch + roofLift, out_ + overhang);
                Vector3 eaveBack = new Vector3(along + side * (halfWidth + overhang), top - overhang * pitch + roofLift, ridgeBack);
                Vector3 ridgeFront = new Vector3(along, top + rise + roofLift, out_ + overhang);
                Vector3 ridgeRear = new Vector3(along, top + rise + roofLift, ridgeBack);
                if (side > 0f) Slope(b, eaveFront, eaveBack, ridgeRear, ridgeFront, kit.Roof, tint, 2, 2);
                else Slope(b, eaveBack, eaveFront, ridgeFront, ridgeRear, kit.Roof, tint, 2, 2);
            }
            b.Tube(new Vector3(along, top + rise + roofLift + 0.02f, out_ + overhang + 0.1f), new Vector3(along, top + rise + roofLift + 0.02f, ridgeBack), 0.16f, 0.16f, 6, kit.Ridge);
            // A board under its gable.
            b.Beam(new Vector3(along - halfWidth - overhang, top - overhang * pitch + 0.1f, out_ + overhang), new Vector3(along, top + rise + 0.1f, out_ + overhang), 0.09f, kit.Soffit);
            b.Beam(new Vector3(along + halfWidth + overhang, top - overhang * pitch + 0.1f, out_ + overhang), new Vector3(along, top + rise + 0.1f, out_ + overhang), 0.09f, kit.Soffit);
        }

        /// <summary>A stone chimney stack from <paramref name="bottom"/> to <paramref name="top"/>, with a corbelled cap and pots. Where its smoke leaves is added to <paramref name="chimneys"/>.</summary>
        public static void Chimney(MeshBuilder b, Vector3 foot, Vector2 half, float top, VertexMaterial stone, VertexMaterial cap, List<Vector3>? chimneys, int pots = 2)
        {
            float bottom = foot.Y;
            b.Box(new Vector3(foot.X, (bottom + top) * 0.5f, foot.Z), new Vector3(half.X, (top - bottom) * 0.5f, half.Y), stone);
            b.Box(new Vector3(foot.X, top - 0.55f, foot.Z), new Vector3(half.X + 0.12f, 0.12f, half.Y + 0.12f), cap);
            b.Box(new Vector3(foot.X, top + 0.08f, foot.Z), new Vector3(half.X + 0.2f, 0.14f, half.Y + 0.2f), cap);
            for (int i = 0; i < pots; i++)
            {
                float x = pots == 1 ? 0f : (i / (pots - 1f) - 0.5f) * half.X * 1.1f;
                Vector3 potFoot = new Vector3(foot.X + x, top + 0.22f, foot.Z);
                b.Tube(potFoot, potFoot + Vector3.UnitY * 0.7f, 0.26f, 0.2f, 8, Hall.Brick(150, 96, 70));
                b.Tube(potFoot + Vector3.UnitY * 0.7f, potFoot + Vector3.UnitY * 0.82f, 0.26f, 0.26f, 8, Hall.Brick(150, 96, 70));
                chimneys?.Add(potFoot + Vector3.UnitY * 1.6f);
            }
        }
    }
}

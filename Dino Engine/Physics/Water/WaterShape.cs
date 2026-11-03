using Dino_Engine.ECS.Components;
using OpenTK.Mathematics;

namespace Dino_Engine.Physics
{
    public enum WaterShape
    {
        /// <summary>An ellipse with the body's half extents as its radii.</summary>
        Ellipse = 0,
        /// <summary>A rectangle with the body's half extents.</summary>
        Rectangle = 1,
    }

    /// <summary>
    /// The outline of a still water body (<see cref="WaterBodyComponent"/>). This is an exact mirror of
    /// Shaders/Water/WaterShape.glsl (same hash, noise and formulas), so gameplay and placement code see the same
    /// outline the GPU draws. Any change here must be made there too, and the other way round.
    /// </summary>
    public static class WaterShapes
    {
        // Keeps lattice cells positive so the integer hash never sees a negative number (WATER_NOISE_CELL_OFFSET).
        private const int NoiseCellOffset = 32768;

        /// <summary>The body's local x axis on the world xz plane, from its yaw.</summary>
        public static Vector2 AxisFromRotation(Quaternion rotation)
        {
            Vector3 x = Vector3.Transform(Vector3.UnitX, rotation);
            Vector2 axis = new Vector2(x.X, x.Z);
            return axis.LengthSquared > 1e-8f ? axis.Normalized() : Vector2.UnitX;
        }

        /// <summary>World xz offset from the body's centre expressed along its local x and z axes.</summary>
        public static Vector2 ToLocal(Vector2 offset, Vector2 axis)
        {
            return new Vector2(Vector2.Dot(offset, axis), Vector2.Dot(offset, new Vector2(-axis.Y, axis.X)));
        }

        /// <summary>Signed distance in metres to the outline before edge noise; negative inside.</summary>
        public static float SignedDistance(WaterShape shape, Vector2 local, Vector2 halfExtents)
        {
            Vector2 r = Vector2.ComponentMax(halfExtents, new Vector2(1e-3f));
            if (shape == WaterShape.Rectangle)
            {
                Vector2 d = new Vector2(MathF.Abs(local.X), MathF.Abs(local.Y)) - r;
                Vector2 outside = Vector2.ComponentMax(d, Vector2.Zero);
                return outside.Length + MathF.Min(MathF.Max(d.X, d.Y), 0f);
            }

            // Ellipse distance estimate (exact on the outline, close nearby).
            float k0 = (local / r).Length;
            float k1 = (local / (r * r)).Length;
            return k1 > 1e-6f ? k0 * (k0 - 1f) / k1 : -MathF.Min(r.X, r.Y);
        }

        /// <summary>
        /// How much of the point at <paramref name="worldXZ"/> the body covers, 0..1: 1 inside, fading to 0 over
        /// EdgeSoftness metres inside the (noise-displaced) outline.
        /// </summary>
        public static float Coverage(in WaterBodyComponent body, Vector2 center, Vector2 axis, Vector2 worldXZ)
        {
            Vector2 local = ToLocal(worldXZ - center, axis);
            float distance = SignedDistance(body.Shape, local, body.HalfExtents);
            if (body.EdgeNoise > 0f)
            {
                distance += body.EdgeNoise * (2f * EdgeNoise(worldXZ / MathF.Max(body.EdgeNoiseScale, 0.05f)) - 1f);
            }
            if (body.EdgeSoftness <= 1e-4f) return distance < 0f ? 1f : 0f;
            return SmoothStep(0f, body.EdgeSoftness, -distance);
        }

        /// <summary>Half extents of a quad that contains the whole outline, edge noise included.</summary>
        public static Vector2 QuadHalfExtents(in WaterBodyComponent body)
        {
            return body.HalfExtents + new Vector2(MathF.Max(body.EdgeNoise, 0f) + 0.05f);
        }

        /// <summary>Two octaves of value noise, 0..1 (waterEdgeNoise).</summary>
        public static float EdgeNoise(Vector2 p)
        {
            return 0.65f * ValueNoise(p) + 0.35f * ValueNoise(p * 2.03f + new Vector2(17.3f));
        }

        /// <summary>Smooth value noise, 0..1 (waterValueNoise).</summary>
        public static float ValueNoise(Vector2 p)
        {
            float fx = MathF.Floor(p.X);
            float fy = MathF.Floor(p.Y);
            int x = (int)fx + NoiseCellOffset;
            int y = (int)fy + NoiseCellOffset;
            float tx = p.X - fx;
            float ty = p.Y - fy;
            float ux = tx * tx * tx * (tx * (tx * 6f - 15f) + 10f);
            float uy = ty * ty * ty * (ty * (ty * 6f - 15f) + 10f);

            float a = Lattice(x, y);
            float b = Lattice(x + 1, y);
            float c = Lattice(x, y + 1);
            float d = Lattice(x + 1, y + 1);
            return a + (b - a) * ux + (c - a) * uy + (a - b - c + d) * ux * uy;
        }

        private static float Lattice(int x, int y)
        {
            uint h = Hash((uint)x + Hash((uint)y + 40503u));
            return (h & 0xffffffu) / 16777215f;
        }

        /// <summary>Integer hash (waterHash), the same mixer Wind.glsl uses.</summary>
        public static uint Hash(uint x)
        {
            x ^= x >> 16;
            x *= 0x7feb352du;
            x ^= x >> 15;
            x *= 0x846ca68bu;
            x ^= x >> 16;
            return x;
        }

        private static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
            return t * t * (3f - 2f * t);
        }
    }
}

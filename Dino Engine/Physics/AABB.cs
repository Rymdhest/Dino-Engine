using OpenTK.Mathematics;

namespace Dino_Engine.Physics
{
    public class AABB : HitBox
    {
        public Vector3 Min;
        public Vector3 Max;
        public AABB (Vector3 min, Vector3 max)
        {
            this.Min = min;
            this.Max = max;
        }

        /// <summary>
        /// Transforms a local AABB by a 4x4 matrix into a new world-space AABB.
        /// Encapsulates all 8 transformed corners without heap allocations.
        /// </summary>
        public static AABB Transform(AABB aabb, Matrix4 transform)
        {
            Vector3 min = new Vector3(float.MaxValue);
            Vector3 max = new Vector3(float.MinValue);

            // Iterate through all 8 corners of the local bounding box
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3(
                    (i & 1) == 0 ? aabb.Min.X : aabb.Max.X,
                    (i & 2) == 0 ? aabb.Min.Y : aabb.Max.Y,
                    (i & 4) == 0 ? aabb.Min.Z : aabb.Max.Z
                );

                // OpenTK TransformPosition handles scale, rotation, and translation
                Vector3 transformed = Vector3.TransformPosition(corner, transform);

                min.X = MathF.Min(min.X, transformed.X);
                min.Y = MathF.Min(min.Y, transformed.Y);
                min.Z = MathF.Min(min.Z, transformed.Z);

                max.X = MathF.Max(max.X, transformed.X);
                max.Y = MathF.Max(max.Y, transformed.Y);
                max.Z = MathF.Max(max.Z, transformed.Z);
            }

            return new AABB(min, max);
        }


    }
}

using Dino_Engine.Physics;
using OpenTK.Mathematics;
using Vector3 = OpenTK.Mathematics.Vector3;

namespace Dino_Engine.Util
{
    public enum IntersectionResult
    {
        Outside,
        Intersecting,
        Inside
    }

    public class Frustum
    {

        public Plane[] Planes = new Plane[6];

        public Frustum(Matrix4 projectionMatrix)
        {
            Planes[0] = new Plane(  // Left
                projectionMatrix.M14 + projectionMatrix.M11,
                projectionMatrix.M24 + projectionMatrix.M21,
                projectionMatrix.M34 + projectionMatrix.M31,
                projectionMatrix.M44 + projectionMatrix.M41);
            Planes[1] = new Plane(  // Right
                projectionMatrix.M14 - projectionMatrix.M11,
                projectionMatrix.M24 - projectionMatrix.M21,
                projectionMatrix.M34 - projectionMatrix.M31,
                projectionMatrix.M44 - projectionMatrix.M41);
            Planes[2] = new Plane(  // Bottom
                projectionMatrix.M14 + projectionMatrix.M12,
                projectionMatrix.M24 + projectionMatrix.M22,
                projectionMatrix.M34 + projectionMatrix.M32,
                projectionMatrix.M44 + projectionMatrix.M42);
            Planes[3] = new Plane(  // Top
                projectionMatrix.M14 - projectionMatrix.M12,
                projectionMatrix.M24 - projectionMatrix.M22,
                projectionMatrix.M34 - projectionMatrix.M32,
                projectionMatrix.M44 - projectionMatrix.M42);
            Planes[4] = new Plane(  // Near
                projectionMatrix.M14 + projectionMatrix.M13,
                projectionMatrix.M24 + projectionMatrix.M23,
                projectionMatrix.M34 + projectionMatrix.M33,
                projectionMatrix.M44 + projectionMatrix.M43);
            Planes[5] = new Plane(  // Far
                projectionMatrix.M14 - projectionMatrix.M13,
                projectionMatrix.M24 - projectionMatrix.M23,
                projectionMatrix.M34 - projectionMatrix.M33,
                projectionMatrix.M44 - projectionMatrix.M43);

            for (int i = 0; i < 6; i++)
            {
                Planes[i] = Plane.Normalize(Planes[i]);
            }
        }

        /// <summary>Whether any of a sphere is inside. An infinite radius always is.</summary>
        public bool IntersectsSphere(Vector3 centre, float radius)
        {
            foreach (Plane plane in Planes)
            {
                if (Vector3.Dot(plane.Normal, centre) + plane.D < -radius) return false;
            }
            return true;
        }

        /// <summary>Whether any of the path of a sphere moved along <paramref name="sweep"/> is inside: where something can throw a shadow, for one.</summary>
        public bool IntersectsSweptSphere(Vector3 centre, float radius, Vector3 sweep)
        {
            foreach (Plane plane in Planes)
            {
                float distance = Vector3.Dot(plane.Normal, centre) + plane.D;
                if (distance + MathF.Max(Vector3.Dot(plane.Normal, sweep), 0f) < -radius) return false;
            }
            return true;
        }

        public IntersectionResult IntersectsAABB(AABB box)
        {

            bool intersecting = false;
            foreach (var plane in Planes)
            {
                Vector3 positive = new Vector3(
                    plane.Normal.X >= 0 ? box.Max.X : box.Min.X,
                    plane.Normal.Y >= 0 ? box.Max.Y : box.Min.Y,
                    plane.Normal.Z >= 0 ? box.Max.Z : box.Min.Z);

                Vector3 negative = new Vector3(
                    plane.Normal.X >= 0 ? box.Min.X : box.Max.X,
                    plane.Normal.Y >= 0 ? box.Min.Y : box.Max.Y,
                    plane.Normal.Z >= 0 ? box.Min.Z : box.Max.Z);

                if (Vector3.Dot(plane.Normal, positive) + plane.D < 0)
                    return IntersectionResult.Outside;

                if (Vector3.Dot(plane.Normal, negative) + plane.D < 0)
                    intersecting = true;
            }
            return intersecting ? IntersectionResult.Intersecting : IntersectionResult.Inside;
        }
    }
}

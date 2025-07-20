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
                projectionMatrix.M13,
                projectionMatrix.M23,
                projectionMatrix.M33,
                projectionMatrix.M43);
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
        public IntersectionResult IntersectsAABB(AABB box)
        {

            bool intersecting = false;
            foreach (var plane in Planes)
            {
                Vector3 positive = new Vector3(
                    plane.Normal.X >= 0 ? box.max.X : box.min.X,
                    plane.Normal.Y >= 0 ? box.max.Y : box.min.Y,
                    plane.Normal.Z >= 0 ? box.max.Z : box.min.Z);

                Vector3 negative = new Vector3(
                    plane.Normal.X >= 0 ? box.min.X : box.max.X,
                    plane.Normal.Y >= 0 ? box.min.Y : box.max.Y,
                    plane.Normal.Z >= 0 ? box.min.Z : box.max.Z);

                if (Vector3.Dot(plane.Normal, positive) + plane.D < 0)
                    return IntersectionResult.Outside;

                if (Vector3.Dot(plane.Normal, negative) + plane.D < 0)
                    intersecting = true;
            }
            return intersecting ? IntersectionResult.Intersecting : IntersectionResult.Inside;
        }
    }
}

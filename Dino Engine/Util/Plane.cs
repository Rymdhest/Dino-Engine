using OpenTK.Mathematics;

namespace Dino_Engine.Util
{
    public struct Plane
    {
        public Vector3 Normal;
        public float D;

        public Plane(float a, float b, float c, float d)
        {
            Normal = new Vector3(a, b, c);
            D = d;
        }

        public Plane(Vector3 normal, float d)
        {
            Normal = normal;
            D = d;
        }

        public static Plane Normalize(Plane plane)
        {
            float length = plane.Normal.Length;
            return new Plane(plane.Normal / length, plane.D / length);
        }

        public float DotCoordinate(Vector3 point)
        {
            return Vector3.Dot(Normal, point) + D;
        }
    }
}

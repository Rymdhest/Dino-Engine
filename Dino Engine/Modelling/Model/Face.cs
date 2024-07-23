using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Model
{
    public class Face
    {
        public MeshVertex A;
        public MeshVertex B;
        public MeshVertex C;

        public Vector3 faceNormal;
        public Vector3 faceTangent;
        public Vector3 faceBitanget;

        public Face(MeshVertex A, MeshVertex B, MeshVertex C)
        {
            this.A = A ?? throw new ArgumentNullException(nameof(A));
            this.B = B ?? throw new ArgumentNullException(nameof(B));
            this.C = C ?? throw new ArgumentNullException(nameof(C));
            //calcFaceNormal();
        }
        public void calcFaceNormal()
        {
            faceNormal = Face.CalcFaceNormal(A.position, B.position, C.position);
            CalcFaceTangent(A, B, C);
            //faceBitanget = Vector3.Cross(faceNormal , faceTangent).Normalized();
        }

        public static Vector3 CalcFaceNormal(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 edge1 = b - a;
            Vector3 edge2 = c - a;
            Vector3 normal = Vector3.Cross(edge1, edge2);



            normal = normal.Normalized();

            return normal;
        }
        private void CalcFaceTangent(MeshVertex v0, MeshVertex v1, MeshVertex v2)
        {
            Vector3 dv1 = v1.position - v0.position;
            Vector3 dv2 = v2.position - v0.position;

            Vector2 duv1 = v1.UV - v0.UV;
            Vector2 duv2 = v2.UV - v0.UV;

            float f = 1.0f / (duv1.X * duv2.Y - duv1.Y * duv2.X);

            if (float.IsInfinity(f) || float.IsNaN(f))
            {
                // Handle degenerate UV case by setting a default tangent
                //return new Vector3(1, 0, 0); // Default tangent
            }

            faceTangent = (dv1 * duv2.Y - dv2 * duv1.Y) * f;
            faceBitanget = (-dv1 * duv2.X + dv2 * duv1.X) * f;

            // Orthogonalize the tangent with the normal
            //faceTangent = (faceTangent - v0.normal * Vector3.Dot(v0.normal, faceTangent)).Normalized();

        }
    }
}

using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Model
{
    public class Face
    {
        public MeshVertex A;
        public MeshVertex B;
        public MeshVertex C;

        public int uvIndexA;
        public int uvIndexB;
        public int uvIndexC;

        public Vector3 faceNormal;
        public Vector3 faceTangent;
        public Vector3 faceBitanget;

        public Face(MeshVertex A, MeshVertex B, MeshVertex C, int uvIndexA, int uvIndexB, int uvIndexC)
        {
            this.A = A ?? throw new ArgumentNullException(nameof(A));
            this.B = B ?? throw new ArgumentNullException(nameof(B));
            this.C = C ?? throw new ArgumentNullException(nameof(C));

            this.uvIndexA = uvIndexA;
            this.uvIndexB = uvIndexB;
            this.uvIndexC = uvIndexC;

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
            // Compute edges of the final noisy triangle
            Vector3 edge1 = v1.position - v0.position;
            Vector3 edge2 = v2.position - v0.position;

            // Use the face normal as the base normal direction
            Vector3 n = faceNormal;

            // Create a stable geometric tangent using the triangle's edges projected onto the normal plane
            Vector3 tangent = edge1;
            if (tangent.LengthSquared < 0.00001f)
            {
                tangent = edge2;
            }

            // Gram-Schmidt orthogonalize the tangent against the normal
            tangent = tangent - n * Vector3.Dot(n, tangent);
            if (tangent.LengthSquared > 0.00001f)
            {
                faceTangent = tangent.Normalized();
            }
            else
            {
                // Fallback axis if degenerate
                Vector3 fallback = MathF.Abs(n.Y) < 0.99f ? Vector3.UnitY : Vector3.UnitZ;
                faceTangent = Vector3.Cross(n, fallback).Normalized();
            }

            // Compute bitangent via cross product ensuring correct orientation
            faceBitanget = Vector3.Cross(n, faceTangent).Normalized();
        }
    }
}

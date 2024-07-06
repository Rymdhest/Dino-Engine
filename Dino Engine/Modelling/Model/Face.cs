using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Model
{
    public class Face
    {
        public Vertex A;
        public Vertex B;
        public Vertex C;
        public Vector3 faceNormal;

        public Face(Vertex A, Vertex B, Vertex C)
        {
            this.A = A ?? throw new ArgumentNullException(nameof(A));
            this.B = B ?? throw new ArgumentNullException(nameof(B));
            this.C = C ?? throw new ArgumentNullException(nameof(C));
            //calcFaceNormal();
        }
        public void calcFaceNormal()
        {
            faceNormal = Face.CalcFaceNormal(A.position, B.position, C.position);
        }

        public static Vector3 CalcFaceNormal(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 edge1 = b - a;
            Vector3 edge2 = c - a;
            Vector3 normal = Vector3.Cross(edge1, edge2);

            // Verbose logging


            normal = normal.Normalized();

            return normal;
        }
    }
}

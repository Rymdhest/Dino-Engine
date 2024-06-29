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
            this.A = A;
            this.B = B;
            this.C = C;
            calcFaceNormal();
        }
        public void calcFaceNormal()
        {
            faceNormal = Face.CalcFaceNormal(A.position, B.position, C.position);
        }

        public static Vector3 CalcFaceNormal(Vector3 a, Vector3 b, Vector3 c)
        {
            return Vector3.Cross((b - a), (c - a)).Normalized();
        }
    }
}

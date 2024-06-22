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
            faceNormal = Vector3.Cross((B.position - A.position), (C.position - A.position));
            faceNormal.Normalize();
        }
    }
}

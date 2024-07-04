using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Model
{
    public class Vertex
    {
        public Vector3 position;
        public Vector3 normal;
        public Vector3 tangent;
        public Material material;
        public Vector2 UV;
        public List<Face> faces;
        public int index;
        public Vertex(Vector3 position, int index)
        {
            faces = new List<Face>();
            this.position = position;
            normal = new Vector3(0.0f, 1.0f, 0.0f);
            tangent = new Vector3(1.0f, 0.0f, 0.0f);
            this.index = index;
        }

        public void calculateNormal()
        {
            normal.X = 0;
            normal.Y = 0;
            normal.Z = 0;
            foreach (Face face in faces)
            {
                normal += face.faceNormal;
            }
            normal /= faces.Count;
            normal.Normalize();

            float radius = 1f;
            float u = UV.X;
            float v = UV.Y;
            float x = radius * (float)Math.Sin(u) * (float)Math.Cos(v);
            float y = radius * (float)Math.Sin(u) * (float)Math.Sin(v);
            float z = radius * (float)Math.Cos(u);

            Vector3 result = new Vector3(
                (float)Math.Cos(u) * (float)Math.Cos(v),
                (float)Math.Cos(u) * (float)Math.Sin(v),
                -(float)Math.Sin(u)
            );
            tangent = result;
        }


    }
}

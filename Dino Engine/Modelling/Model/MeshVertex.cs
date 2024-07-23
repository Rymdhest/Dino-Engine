using Dino_Engine.Debug;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using System.Collections.Generic;

namespace Dino_Engine.Modelling.Model
{
    public class MeshVertex : Vertex
    {
        public Vector3 normal;
        public Vector3 tangent;
        public Vector3 bitanget;
        public List<Face> faces;
        public int index;
        public MeshVertex(Vertex vertex, int index) : base(vertex.position, vertex.UV, vertex.material)
        {
            faces = new List<Face>();
            this.index = index;
            normal = new Vector3(0.0f, 1.0f, 0.0f);
            tangent = new Vector3(1.0f, 0.0f, 0.0f);
            bitanget = new Vector3(0.0f, 0.0f, 1.0f);
        }

        public void calculateNormalAndTangent()
        {
            normal.X = 0;
            normal.Y = 0;
            normal.Z = 0;
            foreach (Face face in faces)
            {
                normal += face.faceNormal;
            }
            if (faces.Count == 0) Console.WriteLine("Warning 0 faces in a vertex");
            normal /= faces.Count;
            normal.Normalize();

            tangent.X = 0;
            tangent.Y = 0;
            tangent.Z = 0;
            foreach (Face face in faces)
            {
                tangent += face.faceTangent;
            }
            if (faces.Count == 0) Console.WriteLine("Warning 0 faces in a vertex");
            tangent /= faces.Count;
            tangent.Normalize();

            bitanget.X = 0;
            bitanget.Y = 0;
            bitanget.Z = 0;
            foreach (Face face in faces)
            {
                bitanget += face.faceBitanget;
            }
            if (faces.Count == 0) Console.WriteLine("Warning 0 faces in a vertex");
            bitanget /= faces.Count;
            bitanget.Normalize();


            //bitanget = Vector3.Cross(tangent, normal).Normalized();
            //bitanget = new Vector3(0.0f, 0.0f, 1.0f);
        }

        public Vector2 GetTagentSpaceScaledUV(Vector3 scale)
        {
            
            Matrix3 TBM = new Matrix3(
                tangent.X, bitanget.X, normal.X,
                tangent.Y, bitanget.Y, normal.Y,
                tangent.Z, bitanget.Z, normal.Z);
            
            Matrix3 TBM2 = new Matrix3(
                tangent.X, tangent.Y, tangent.Z,
                bitanget.X, bitanget.Y, bitanget.Z,
                normal.X, normal.Y, normal.Z);

            Vector3 scaleTangentSpace = TBM* scale;


            return UV*scaleTangentSpace.Xy;


        }

    }
}

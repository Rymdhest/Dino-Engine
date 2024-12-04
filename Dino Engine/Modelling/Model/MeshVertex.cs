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
        public Vector3 bitangent;
        public List<Face> faces;
        public vIndex index;
        public MeshVertex(Vertex vertex, vIndex index) : base(vertex.position, vertex.material, vertex.UVs)
        {
            faces = new List<Face>();
            this.index = index;
            normal = new Vector3(0.0f, 1.0f, 0.0f);
            tangent = new Vector3(1.0f, 0.0f, 0.0f);
            bitangent = new Vector3(0.0f, 0.0f, 1.0f);
        }

        public void calculateNormalAndTangent()
        {
            // Step 1: Initialize vectors
            normal = new Vector3(0, 0, 0);
            tangent = new Vector3(0, 0, 0);
            bitangent = new Vector3(0, 0, 0);

            // Step 2: Accumulate face normals, tangents, and bitangents
            foreach (Face face in faces)
            {
                normal += face.faceNormal;
                tangent += face.faceTangent;
                bitangent += face.faceBitanget;
            }

            // Step 3: Check if there are any faces
            if (faces.Count == 0)
            {
                Console.WriteLine("Warning: 0 faces in a vertex");
                return;
            }

            // Step 4: Calculate the average vectors
            normal /= faces.Count;
            tangent /= faces.Count;
            bitangent /= faces.Count;

            // Step 5: Normalize the normal vector
            normal.Normalize();

            // Step 6: Orthogonalize and normalize the tangent vector
            tangent = Vector3.Normalize(tangent - normal * Vector3.Dot(normal, tangent));

            // Step 7: Orthogonalize and normalize the bitangent vector
            bitangent = Vector3.Cross(tangent, normal);
            bitangent.Normalize();

            // Optional Step: Ensure handedness (for DirectX or OpenGL consistency)
            // If your coordinate system is right-handed, you can use the following:
            if (Vector3.Dot(Vector3.Cross(normal, tangent), bitangent) < 0.0f)
            {
                //bitangent = -bitangent;
            }
        }

        /*
        public Vector2 GetTagentSpaceScaledUV(Vector3 scale)
        {
            calculateNormalAndTangent();
            Matrix3 TBM = new Matrix3(
                tangent.X, bitangent.X, normal.X,
                tangent.Y, bitangent.Y, normal.Y,
                tangent.Z, bitangent.Z, normal.Z);

            //TBM = Matrix3.Transpose(TBM);
            //TBM = Matrix3.Invert(TBM);
            Vector3 scaleTangentSpace = TBM* scale;

            Vector2 scaledUVs = UV * scaleTangentSpace.Xy; ;

            if (scaledUVs.X < 0) scaledUVs.X *= -1f;
            if (scaledUVs.Y < 0) scaledUVs.Y *= -1f;

            return scaledUVs;
        }
        */
    }
}

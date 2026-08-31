using System;
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
        }

        public void calcFaceNormal()
        {
            faceNormal = Face.CalcFaceNormal(A.position, B.position, C.position);
            CalcFaceTangent(A, B, C);
        }

        public static Vector3 CalcFaceNormal(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 edge1 = b - a;
            Vector3 edge2 = c - a;
            Vector3 normal = Vector3.Cross(edge1, edge2);
            return normal.Normalized();
        }

        private void CalcFaceTangent(MeshVertex v0, MeshVertex v1, MeshVertex v2)
        {
            // 1. Compute position edges
            Vector3 edge1 = v1.position - v0.position;
            Vector3 edge2 = v2.position - v0.position;

            // 2. Retrieve UV coordinates safely using the face's UV indices
            Vector2 uv0 = (v0.UVs != null && v0.UVs.Length > uvIndexA) ? v0.UVs[uvIndexA] : Vector2.Zero;
            Vector2 uv1 = (v1.UVs != null && v1.UVs.Length > uvIndexB) ? v1.UVs[uvIndexB] : Vector2.Zero;
            Vector2 uv2 = (v2.UVs != null && v2.UVs.Length > uvIndexC) ? v2.UVs[uvIndexC] : Vector2.Zero;

            // 3. Compute UV deltas
            Vector2 deltaUV1 = uv1 - uv0;
            Vector2 deltaUV2 = uv2 - uv0;

            float det = deltaUV1.X * deltaUV2.Y - deltaUV2.X * deltaUV1.Y;

            Vector3 tangent;
            Vector3 bitangent;

            // 4. Solve via Lengyel's formula (preserving both tangent and bitangent)
            if (MathF.Abs(det) < 0.00001f)
            {
                tangent = edge1;
                bitangent = edge2;
            }
            else
            {
                float r = 1.0f / det;
                tangent = (edge1 * deltaUV2.Y - edge2 * deltaUV1.Y) * r;
                bitangent = (edge2 * deltaUV1.X - edge1 * deltaUV2.X) * r;
            }

            Vector3 n = faceNormal;
            if (n.LengthSquared < 0.00001f)
            {
                n = CalcFaceNormal(v0.position, v1.position, v2.position);
            }

            // 5. Gram-Schmidt orthogonalize tangent against the normal
            tangent = tangent - n * Vector3.Dot(n, tangent);
            if (tangent.LengthSquared > 0.00001f)
            {
                faceTangent = tangent.Normalized();
            }
            else
            {
                Vector3 fallback = MathF.Abs(n.Y) < 0.99f ? Vector3.UnitY : Vector3.UnitZ;
                faceTangent = Vector3.Cross(n, fallback).Normalized();
            }

            // 6. Gram-Schmidt orthogonalize bitangent against normal AND tangent
            bitangent = bitangent - n * Vector3.Dot(n, bitangent);
            bitangent = bitangent - faceTangent * Vector3.Dot(faceTangent, bitangent);
            if (bitangent.LengthSquared > 0.00001f)
            {
                faceBitanget = bitangent.Normalized();
            }
            else
            {
                faceBitanget = Vector3.Cross(faceTangent, n).Normalized();
            }

            // 7. Respect UV winding / mirroring handedness
            if (det < 0.0f)
            {
                faceBitanget = -faceBitanget;
            }
        }
    }
}
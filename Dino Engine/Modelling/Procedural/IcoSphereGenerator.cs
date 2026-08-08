using Dino_Engine.Modelling.Model;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural
{
    public class IcoSphereGenerator
    {
        public static Mesh CreateIcosphere(int order, VertexMaterial material)
        {
            // Set up a 20-triangle icosahedron
            float f = (1 + MathF.Sqrt(5)) / 2f;
            int T = (int)Math.Pow(4, order);

            float[] positions = new float[(10 * T + 2) * 3];

            Array.Copy(new float[]
            {
        -1, f, 0,  1, f, 0, -1, -f, 0,  1, -f, 0,
         0, -1, f, 0, 1, f,  0, -1, -f, 0, 1, -f,
         f, 0, -1, f, 0, 1, -f,  0, -1, -f, 0, 1
            }, positions, 36);

            int[] indices = new int[]
            {
        0, 11, 5,  0, 5, 1,  0, 1, 7,  0, 7, 10,  0, 10, 11,
        11, 10, 2, 5, 11, 4,  1, 5, 9,  7, 1,  8, 10,  7,  6,
         3,  9, 4, 3,  4, 2,  3, 2, 6,  3, 6,  8,  3,  8,  9,
         9,  8, 1, 4,  9, 5,  2, 4, 11, 6, 2, 10,  8,  6,  7
            };

            int vert = 12;
            Dictionary<int, int>? midCache = order > 0 ? new Dictionary<int, int>() : null;

            int addMidPoint(int a, int b)
            {
                int key = (int)((a + b) * (a + b + 1) / 2) + Math.Min(a, b);
                if (midCache != null && midCache.TryGetValue(key, out int i))
                {
                    midCache.Remove(key);
                    return i;
                }
                if (midCache != null)
                {
                    midCache[key] = vert;
                }
                for (int k = 0; k < 3; k++)
                {
                    positions[3 * vert + k] = (positions[3 * a + k] + positions[3 * b + k]) / 2f;
                }
                return vert++;
            }

            int[] indicesPrev = indices;
            for (int i = 0; i < order; i++)
            {
                indices = new int[indicesPrev.Length * 4];
                for (int k = 0; k < indicesPrev.Length; k += 3)
                {
                    int v1 = indicesPrev[k + 0];
                    int v2 = indicesPrev[k + 1];
                    int v3 = indicesPrev[k + 2];
                    int a = addMidPoint(v1, v2);
                    int b = addMidPoint(v2, v3);
                    int c = addMidPoint(v3, v1);
                    int t = k * 4;
                    indices[t++] = v1; indices[t++] = a; indices[t++] = c;
                    indices[t++] = v2; indices[t++] = b; indices[t++] = a;
                    indices[t++] = v3; indices[t++] = c; indices[t++] = b;
                    indices[t++] = a; indices[t++] = b; indices[t++] = c;
                }
                indicesPrev = indices;
            }

            // Create MeshVertex list and normalize positions onto a unit sphere
            List<MeshVertex> meshVertices = new List<MeshVertex>();
            for (int i = 0; i < positions.Length; i += 3)
            {
                float x = positions[i];
                float y = positions[i + 1];
                float z = positions[i + 2];

                float m = 1f / MathF.Sqrt(x * x + y * y + z * z);
                x *= m;
                y *= m;
                z *= m;

                Vector3 position = new Vector3(x, y, z);

                float u = (MathF.Atan2(x, z) / (2f * MathF.PI)) + 0.5f;
                float v = (MathF.Asin(y) / MathF.PI) + 0.5f;

                Vertex baseVertex = new Vertex(position, material, new Vector2(u, v));
                MeshVertex meshVertex = new MeshVertex(baseVertex, new vIndex(meshVertices.Count));

                // A sphere's normal at any point is its normalized position
                meshVertex.normal = position;

                meshVertices.Add(meshVertex);
            }

            // Build faces
            List<Face> faces = new List<Face>();
            for (int i = 0; i < indices.Length; i += 3)
            {
                MeshVertex vA = meshVertices[indices[i]];
                MeshVertex vB = meshVertices[indices[i + 1]];
                MeshVertex vC = meshVertices[indices[i + 2]];

                Face face = new Face(vA, vB, vC, 0, 0, 0);

                // CRITICAL: Link the face to the vertices so calculateAllNormals() can find them!
                vA.faces.Add(face);
                vB.faces.Add(face);
                vC.faces.Add(face);

                // Compute analytical normals and tangents locally for this sphere face
                Vector3 center = (face.A.position + face.B.position + face.C.position) / 3.0f;
                face.faceNormal = center.Normalized();

                // Tangent points along increasing U (east-west around Y axis)
                Vector3 tangent = Vector3.Cross(face.faceNormal, Vector3.UnitY);
                if (tangent.LengthSquared < 0.0001f)
                {
                    tangent = Vector3.Cross(face.faceNormal, Vector3.UnitZ);
                }
                face.faceTangent = tangent.Normalized();
                face.faceBitanget = Vector3.Cross(face.faceNormal, face.faceTangent).Normalized();

                faces.Add(face);
            }

            Mesh mesh = new Mesh
            {
                meshVertices = meshVertices,
                faces = faces
            };

            // Process faces to configure alternate UVs and flags for seams/poles
            foreach (Face face in mesh.faces)
            {
                Vector2 uv0 = face.A.UVs[0];
                Vector2 uv1 = face.B.UVs[0];
                Vector2 uv2 = face.C.UVs[0];

                // 1. Check Seam Wraparound (Must run BEFORE pole averaging)
                if (MathF.Abs(uv0.X - uv1.X) > 0.5f ||
                    MathF.Abs(uv0.X - uv2.X) > 0.5f ||
                    MathF.Abs(uv1.X - uv2.X) > 0.5f)
                {
                    if (uv0.X < 0.5f) uv0.X += 1f;
                    if (uv1.X < 0.5f) uv1.X += 1f;
                    if (uv2.X < 0.5f) uv2.X += 1f;
                }

                // 2. Check Poles and Align U Coordinates cleanly
                bool isPoleA = MathF.Abs(MathF.Abs(face.A.position.Y) - 1f) < 0.001f;
                bool isPoleB = MathF.Abs(MathF.Abs(face.B.position.Y) - 1f) < 0.001f;
                bool isPoleC = MathF.Abs(MathF.Abs(face.C.position.Y) - 1f) < 0.001f;

                if (isPoleA) uv0.X = (uv1.X + uv2.X) * 0.5f;
                if (isPoleB) uv1.X = (uv0.X + uv2.X) * 0.5f;
                if (isPoleC) uv2.X = (uv0.X + uv1.X) * 0.5f;

                // Helper to register alternate UVs safely
                void AssignUV(ref MeshVertex vertex, ref int uvIndexFlag, Vector2 targetUV)
                {
                    // Check if it matches base UV[0]
                    if (MathF.Abs(vertex.UVs[0].X - targetUV.X) < 0.001f &&
                        MathF.Abs(vertex.UVs[0].Y - targetUV.Y) < 0.001f)
                    {
                        uvIndexFlag = 0;
                        return;
                    }

                    // Check existing alternate UVs
                    for (int j = 1; j < vertex.UVs.Length; j++)
                    {
                        if (MathF.Abs(vertex.UVs[j].X - targetUV.X) < 0.001f &&
                            MathF.Abs(vertex.UVs[j].Y - targetUV.Y) < 0.001f)
                        {
                            uvIndexFlag = j;
                            return;
                        }
                    }

                    // Append new alternate UV
                    Vector2[] newArray = new Vector2[vertex.UVs.Length + 1];
                    Array.Copy(vertex.UVs, newArray, vertex.UVs.Length);
                    newArray[^1] = targetUV;
                    vertex.UVs = newArray;
                    uvIndexFlag = vertex.UVs.Length - 1;
                }

                MeshVertex tempA = face.A; int flagA = face.uvIndexA; AssignUV(ref tempA, ref flagA, uv0); face.A = tempA; face.uvIndexA = flagA;
                MeshVertex tempB = face.B; int flagB = face.uvIndexB; AssignUV(ref tempB, ref flagB, uv1); face.B = tempB; face.uvIndexB = flagB;
                MeshVertex tempC = face.C; int flagC = face.uvIndexC; AssignUV(ref tempC, ref flagC, uv2); face.C = tempC; face.uvIndexC = flagC;
            }

            return mesh;
        }
    }
}

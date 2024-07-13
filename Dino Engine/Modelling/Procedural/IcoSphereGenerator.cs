using Dino_Engine.Modelling.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.Modelling.Procedural
{
    public class IcoSphereGenerator
    {
        public static Mesh CreateIcosphere(int order, Material material)
        {
            // set up a 20-triangle icosahedron
            float f = (1 + (float)Math.Sqrt(5)) / 2;
            int T = (int)Math.Pow(4, order);

            float[] positions = new float[(10 * T + 2) * 3];
            float[] uvs = new float[(10 * T + 2) * 2];
            Array.Copy(new float[]
            {
            -1, f, 0, 1, f, 0, -1, -f, 0, 1, -f, 0,
            0, -1, f, 0, 1, f, 0, -1, -f, 0, 1, -f,
            f, 0, -1, f, 0, 1, -f, 0, -1, -f, 0, 1
            }, positions, 36);

            int[] indices = new int[]
            {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
            11, 10, 2, 5, 11, 4, 1, 5, 9, 7, 1, 8, 10, 7, 6,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
            9, 8, 1, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7
            };

            int vert = 12;
            Dictionary<int, int>? midCache = order > 0 ? new Dictionary<int, int>() : null; // midpoint vertices cache to avoid duplicating shared vertices

            int addMidPoint(int a, int b)
            {
                int key = (int)((a + b) * (a + b + 1) / 2) + Math.Min(a, b); // Cantor's pairing function
                if (midCache.TryGetValue(key, out int i))
                {
                    midCache.Remove(key);
                    return i;
                }
                midCache[key] = vert;
                for (int k = 0; k < 3; k++)
                {
                    positions[3 * vert + k] = (positions[3 * a + k] + positions[3 * b + k]) / 2;
                }
                i = vert++;
                return i;
            }

            int[] indicesPrev = indices;
            for (int i = 0; i < order; i++)
            {
                // subdivide each triangle into 4 triangles
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

            // normalize vertices
            for (int i = 0; i < positions.Length; i += 3)
            {
                float m = 1 / (float)Math.Sqrt(positions[i] * positions[i] + positions[i + 1] * positions[i + 1] + positions[i + 2] * positions[i + 2]);
                positions[i] *= m;
                positions[i + 1] *= m;
                positions[i + 2] *= m;
            }
            for (int i = 0; i < positions.Length; i += 3)
            {
                float x = positions[i];
                float y = positions[i + 1];
                float z = positions[i + 2];

                float u = ((MathF.Atan2(x, z) / (2 * MathF.PI)) + 0.5f);
                float v = (MathF.Asin(y) / MathF.PI) + 0.5f;

                uvs[i / 3 * 2] = u;
                uvs[i / 3 * 2 + 1] = v;
            }

            return new Mesh(positions, indices, material);
        }
    }
}

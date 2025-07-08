
using OpenTK.Mathematics;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Util;

namespace Dino_Engine.Modelling
{
    public class MeshGenerator
    {


        public static Mesh GenerateCone(Material material)
        {


            List<Vector3> trunkLayers = new List<Vector3>() {
                //new Vector3(outerRadius, 0f, outerRadius),
                new Vector3(0, 0, 0),
                new Vector3(0.5f, 1, 0.5f),
                new Vector3(0, 1, 0) };
            Mesh mesh = MeshGenerator.generateCylinder(trunkLayers, 7, material);
            mesh.rotate(new Vector3(MathF.PI, 0f, 0f));
            return mesh;
        }
        public static Mesh generateCylinder(List<Vector2> rings2, int polygonsPerRing, Material material, float sealTop = float.NaN)
        {
            List<Vector3> rings = new List<Vector3>();
            for (int i = 0; i < rings2.Count; i++)
            {
                rings.Add(new Vector3(rings2[i].X, rings2[i].Y, rings2[i].X));
            }
            return generateCylinder(rings, polygonsPerRing, material, sealTop);
        }

        public static Mesh generateTube(Curve3D curve, int polygonsPerRing, Material material, float sealTop = float.NaN, int textureRepeats = 1, bool flatStart = true)
        {
            List<Vertex> vertices = new List<Vertex>();
            List<vIndex> indices = new List<vIndex>();
            Vector3[] previousPoints = new Vector3[polygonsPerRing];
            List<CurvePoint> rings = curve.curvePoints;

            float[] uvYs = new float[polygonsPerRing];
            for (int i = 0; i< uvYs.Length; i++)
            {
                uvYs[i] = 0f;
            }
            for (int ring = 0; ring < curve.curvePoints.Count; ring++)
            {


                // Generate vertices for this ring
                for (int detail = 0; detail < polygonsPerRing; detail++)
                {


                    // Angle around the ring
                    float theta = MathF.Tau * (detail / (float)polygonsPerRing);

                    // Compute offset using normal and bitangent
                    Vector3 offset = rings[ring].normal * MathF.Cos(theta) * rings[ring].width + rings[ring].biTangent * MathF.Sin(theta) * rings[ring].width;
                    Vector3 p = rings[ring].pos + offset;

                    // UV Coordinates
                    float uvX = (detail / (float)polygonsPerRing) * textureRepeats;

                    if (ring > 0)
                    {
                        uvYs[detail] += textureRepeats * Vector3.Distance(previousPoints[detail], p) / (MathF.Tau * rings[ring].width);
                    }


                    if (detail == 0)
                    {
                        float uvX2 = ((float)(polygonsPerRing) / (polygonsPerRing)) * textureRepeats;
                        vertices.Add(new Vertex(p, material, new Vector2(uvX, uvYs[detail]), new Vector2(uvX2, uvYs[detail])));
                    }
                    else
                    {
                        vertices.Add(new Vertex(p, material, new Vector2(uvX, uvYs[detail])));
                    }

                    // Add indices to form quads
                    if (ring < rings.Count - 1)
                    {
                        int current = ring * polygonsPerRing + detail;
                        int next = ring * polygonsPerRing + (detail + 1) % polygonsPerRing;
                        int above = (ring + 1) * polygonsPerRing + detail;
                        int aboveNext = (ring + 1) * polygonsPerRing + (detail + 1) % polygonsPerRing;

                        if (detail == polygonsPerRing - 1)
                        {
                            // First triangle
                            indices.Add(new vIndex(next, 1));
                            indices.Add(new vIndex(above, 0));
                            indices.Add(new vIndex(current, 0));

                            // Second triangle
                            indices.Add(new vIndex(aboveNext, 1));
                            indices.Add(new vIndex(above, 0));
                            indices.Add(new vIndex(next, 1));
                        }
                        else
                        {
                            // First triangle
                            indices.Add(new vIndex(next, 0));
                            indices.Add(new vIndex(above, 0));
                            indices.Add(new vIndex(current, 0));

                            // Second triangle
                            indices.Add(new vIndex(aboveNext, 0));
                            indices.Add(new vIndex(above, 0));
                            indices.Add(new vIndex(next, 0));
                        }
                    }
                    previousPoints[detail] = p;
                }
            }

            // Optional: Seal the top of the cylinder
            if (!float.IsNaN(sealTop))
            {
                int ring = rings.Count - 1;
                float y = rings[ring].pos.Y + sealTop;
                Vector3 center = new Vector3(0, y, 0); // Center of the top cap

                vertices.Add(new Vertex(center, material, new Vector2(0.5f, 0.5f)));
                int centerIndex = vertices.Count - 1;

                for (int detail = 0; detail < polygonsPerRing; detail++)
                {
                    int current = ring * polygonsPerRing + detail;
                    int next = ring * polygonsPerRing + (detail + 1) % polygonsPerRing;

                    indices.Add(new vIndex(current));
                    indices.Add(new vIndex(next));
                    indices.Add(new vIndex(centerIndex));
                }
            }

            return new Mesh(vertices, indices);
        }

        public static Mesh generateCylinder(List<Vector3> rings, int polygonsPerRing, Material material, float sealTop = float.NaN)
        {
            int textureRepeats = 1;

            float PI = MathF.PI;
            List<Vertex> vertices = new List<Vertex>();
            List<vIndex> indices = new List<vIndex>();
            float odd = 1;
            if (polygonsPerRing % 2 == 0) odd = 0;
            float uvY = 0f;
            for (int ring = 0; ring < rings.Count; ring++)
            {
                float y = rings[ring].Y;
                uvY = (y / (MathF.PI * 2 * new Vector2(rings[ring].X, rings[ring].Z).Length) * textureRepeats);
                uvY = y * 0.01f;
                for (int detail = 0; detail < polygonsPerRing; detail++)
                {
                    // Calculate the angle for this vertex
                    float theta = 2f * PI * ((float)detail / polygonsPerRing);

                    float x = MathF.Cos(theta) * rings[ring].X;
                    float z = MathF.Sin(theta) * rings[ring].Z;
                    Vector3 p = new Vector3(x, y, z);

                    // Calculate UV coordinates
                    float uvX = ((float)(detail) / (polygonsPerRing)) * textureRepeats;
                    if (detail == 0)
                    {
                        float uvX2 = ((float)(polygonsPerRing) / (polygonsPerRing)) * textureRepeats;
                        vertices.Add(new Vertex(p, material, new Vector2(uvX, uvY), new Vector2(uvX2, uvY)));
                    }
                    else
                    {
                        vertices.Add(new Vertex(p, material, new Vector2(uvX, uvY)));
                    }

                    // Add indices to form the quads between rings

                    if (ring < rings.Count - 1)
                    {
                        int current = ring * polygonsPerRing + detail;
                        int next = ring * polygonsPerRing + (detail + 1) % polygonsPerRing;
                        int above = (ring + 1) * polygonsPerRing + detail;
                        int aboveNext = (ring + 1) * polygonsPerRing + (detail + 1) % polygonsPerRing;

                        if (detail == polygonsPerRing - 1)
                        {
                            // First triangle
                            indices.Add(new vIndex(current, 0));
                            indices.Add(new vIndex(above, 0));
                            indices.Add(new vIndex(next, 1));

                            // Second triangle
                            indices.Add(new vIndex(next, 1));
                            indices.Add(new vIndex(above, 0));
                            indices.Add(new vIndex(aboveNext, 1));
                        }
                        else
                        {
                            // First triangle
                            indices.Add(new vIndex(current, 0));
                            indices.Add(new vIndex(above, 0));
                            indices.Add(new vIndex(next, 0));

                            // Second triangle
                            indices.Add(new vIndex(next, 0));
                            indices.Add(new vIndex(above, 0));
                            indices.Add(new vIndex(aboveNext, 0));
                        }


                    }

                }
            }

            if (sealTop != float.NaN)
            {
                int ring = rings.Count - 1;
                float x = 0;
                float z = 0;
                float y = rings[ring].Y + sealTop;
                Vector3 center = new Vector3(x, y, z);

                vertices.Add(new Vertex(center, material, new Vector2(0.0f, 0.0f)));
                int centerIndex = vertices.Count - 1;
                int indexOffset = centerIndex;
                for (int detail = 0; detail < polygonsPerRing; detail++)
                {
                    int current = ring * polygonsPerRing + detail;

                    Vector3 p1 = vertices[current].position;
                    vertices.Add(new Vertex(p1, material, new Vector2(p1.X, p1.Z) * 0.5f));

                    indices.Add(new vIndex(++indexOffset));
                    indices.Add(new vIndex(centerIndex));


                    if (detail >= polygonsPerRing - 1) indexOffset = centerIndex;

                    indices.Add(new vIndex(indexOffset + 1));
                }
            }
            return new Mesh(vertices, indices);
        }
        public static Mesh generateBox(Material material,   float sizeX = 1.0f, float sizeY = 1.0f, float sizeZ = 1.0f)
        {
            return generateBox(new Vector3(-sizeX, -sizeY, -sizeZ), new Vector3(sizeX, sizeY, sizeZ), material);
        }
        public static Mesh generateShape(List<Vector3> shape, Material material, bool UVTop = false)
        {
            List<Vector3> side2 = new List<Vector3>();
            for (int i = 0; i < shape.Count; i++)
            {
                side2.Add(new Vector3(-1f * shape[i].X, shape[i].Y, shape[i].Z));
            }
            return generateShape(shape, side2, material, UVTop: UVTop);
        }

        public static Mesh generateExtrudedShape(List<Vector3> botShape, List<Vector3> topShape, Material material, Material innerMateria, float depth, float size, bool mirror = false)
        {
            List<Vector3> postions = new List<Vector3>();
            Mesh mesh = new Mesh();

            postions.AddRange(botShape);
            postions.AddRange(topShape);


            int topOffset = botShape.Count;
            for (int i = 0; i < botShape.Count - 1; i++)
            {
                Vector3 p1 = postions[i];
                Vector3 p2 = postions[i + 1];
                Vector3 p3 = postions[i + 1 + topOffset];
                Vector3 p4 = postions[i + topOffset];
                Quad quad = new Quad(p1, p2, p3, p4);
                mesh += ExtrudedPlane(quad, depth, size, material, innerMateria);
            }

            if (mirror)
            {
                List<Vector3> side2 = new List<Vector3>();
                for (int i = 0; i < postions.Count; i++)
                {
                    side2.Add(new Vector3(-1f * postions[i].X, postions[i].Y, postions[i].Z));
                }
                postions.AddRange(side2);
                int sideOffset = (botShape.Count - 1) * 2 + 2;
                for (int i = 0; i < botShape.Count - 1; i++)
                {
                    int j = i + sideOffset;

                    Vector3 p1 = postions[j];
                    Vector3 p2 = postions[j + topOffset];
                    Vector3 p3 = postions[j + 1 + topOffset];
                    Vector3 p4 = postions[j + 1];
                    Quad quad = new Quad(p1, p2, p3, p4);
                    mesh += ExtrudedPlane(quad, depth, size, material, innerMateria);
                }
            }

            return mesh;
        }

        public static Mesh generateShape(List<Vector3> botShape, List<Vector3> topShape, Material material, bool mirror = false, bool UVTop = false)
        {
            List<Vector3> postions = new List<Vector3>();

            List<int> indices = new List<int>();

            postions.AddRange(botShape);
            postions.AddRange(topShape);


            int topOffset = botShape.Count;
            for (int i = 0; i < botShape.Count - 1; i++)
            {
                indices.Add(i);
                indices.Add(i + topOffset);
                indices.Add(i + 1);

                indices.Add(i + topOffset);
                indices.Add(i + 1 + topOffset);
                indices.Add(i + 1);
            }

            if (mirror)
            {
                List<Vector3> side2 = new List<Vector3>();
                for (int i = 0; i < postions.Count; i++)
                {
                    side2.Add(new Vector3(-1f * postions[i].X, postions[i].Y, postions[i].Z));
                }
                postions.AddRange(side2);
                int sideOffset = (botShape.Count - 1) * 2 + 2;
                for (int i = 0; i < botShape.Count - 1; i++)
                {
                    int j = i + sideOffset;

                    indices.Add(j);
                    indices.Add(j + 1);
                    indices.Add(j + topOffset);

                    indices.Add(j + topOffset);
                    indices.Add(j + 1);
                    indices.Add(j + 1 + topOffset);
                }
            }
            Vertex[] vertices = new Vertex[postions.Count];
            for (int i = 0; i < postions.Count; i++)
            {
                if (UVTop) vertices[i] = new Vertex(postions[i], material, new Vector2(-postions[i].X, postions[i].Z));
                else vertices[i] = new Vertex(postions[i], material, new Vector2(-postions[i].Z, postions[i].Y));


            }

            return new Mesh(vertices.ToList<Vertex>(), indices);
        }

        public static Mesh generateBox(Vector3 min, Vector3 max, Material material)
        {
            Mesh plane = generatePlane(material);
            plane.translate(new Vector3(0f, 0.5f, 0.0f));
            Mesh box = new Mesh();

            box += plane.rotated(new Vector3(0f, 0f, 0f));
            box += plane.rotated(new Vector3(MathF.PI / 2f, 0f, 0f));
            box += plane.rotated(new Vector3(MathF.PI, 0f, 0f));
            box += plane.rotated(new Vector3(-MathF.PI / 2f, 0f, 0f));
            box += plane.rotated(new Vector3(0f, 0f, MathF.PI / 2f));
            box += plane.rotated(new Vector3(0f, 0f, -MathF.PI / 2f));
            box.scale(max);
            return box;
        }

        public struct Quad()
        {
            public Vector3 p1;
            public Vector3 p2;
            public Vector3 p3;
            public Vector3 p4;

            public Quad(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4) : this()
            {
                this.p1 = p1;
                this.p2 = p2;
                this.p3 = p3;
                this.p4 = p4;
            }

            public Vector3 GetNormal()
            {
                return (Face.CalcFaceNormal(p1, p4, p2) + Face.CalcFaceNormal(p4, p3, p2)).Normalized();
            }

            public Vector3 GetCenter()
            {
                return (p1 + p2 + p3 + p4) / 4f;
            }

            public void translate(Vector3 translation)
            {
                p1 += translation;
                p2 += translation;
                p3 += translation;
                p4 += translation;
            }
            public void scale(Vector3 scale)
            {
                p1 *= scale;
                p2 *= scale;
                p3 *= scale;
                p4 *= scale;
            }

            private void FlatScale(float shrinkAmount)
            {
                Vector3 normal = GetNormal();

                // Compute the rotation quaternion to align normal with -Z axis
                Vector3 axis = Vector3.Cross(normal, -Vector3.UnitZ);
                float angle = (float)Math.Acos(Vector3.Dot(normal, -Vector3.UnitZ));
                Quaternion rotationQuaternion = Quaternion.FromAxisAngle(axis, angle);

                // Apply rotation to each vertex
                p1 = Vector3.Transform(p1, rotationQuaternion);
                p2 = Vector3.Transform(p2, rotationQuaternion);
                p3 = Vector3.Transform(p3, rotationQuaternion);
                p4 = Vector3.Transform(p4, rotationQuaternion);

                // Apply scaling by a flat amount along X and Y axes
                p1 += new Vector3(shrinkAmount, shrinkAmount, 0f);
                p2 += new Vector3(-shrinkAmount, shrinkAmount, 0f);
                p3 += new Vector3(-shrinkAmount, -shrinkAmount, 0f);
                p4 += new Vector3(shrinkAmount, -shrinkAmount, 0f);

                // Optionally, you can invert the rotation to restore the original orientation
                Quaternion rotationInverse = Quaternion.Invert(rotationQuaternion);
                p1 = Vector3.Transform(p1, rotationInverse);
                p2 = Vector3.Transform(p2, rotationInverse);
                p3 = Vector3.Transform(p3, rotationInverse);
                p4 = Vector3.Transform(p4, rotationInverse);
            }

            public void ScaleFlat(float shrinkAmount)
            {
                FlatScale(shrinkAmount);
            }

            public List<Vector3> ToList()
            {
                var list = new List<Vector3>();
                list.Add(p1);
                list.Add(p2);
                list.Add(p3);
                list.Add(p4);
                return list;
            }
        }

        public static Mesh ExtrudedPlane(Quad startQuad, float extrudeDepth, float extrudeScale, Material material, Material innerMaterial)
        {
            Vector3 extrudeDirection = startQuad.GetNormal();

            Vector3 center = startQuad.GetCenter();

            Quad innerQuad = startQuad;
            innerQuad.translate(-center);
            innerQuad.scale((new Vector3(extrudeScale)));
            innerQuad.translate(center);

            Quad extrudedQuad = innerQuad;
            extrudedQuad.translate(extrudeDirection * extrudeDepth);

            List<int> indices = new List<int>();
            List<Vector3> positions = new List<Vector3>();
            positions.AddRange(startQuad.ToList());
            positions.AddRange(innerQuad.ToList());
            positions.AddRange(extrudedQuad.ToList());

            // outer
            indices.Add(0);
            indices.Add(4);
            indices.Add(1);

            indices.Add(1);
            indices.Add(4);
            indices.Add(5);


            indices.Add(1);
            indices.Add(5);
            indices.Add(2);

            indices.Add(5);
            indices.Add(6);
            indices.Add(2);


            indices.Add(2);
            indices.Add(6);
            indices.Add(3);

            indices.Add(6);
            indices.Add(7);
            indices.Add(3);


            indices.Add(7);
            indices.Add(0);
            indices.Add(3);

            indices.Add(7);
            indices.Add(4);
            indices.Add(0);

            // into
            int i = 4;
            indices.Add(0 + i);
            indices.Add(4 + i);
            indices.Add(1 + i);

            indices.Add(1 + i);
            indices.Add(4 + i);
            indices.Add(5 + i);


            indices.Add(1 + i);
            indices.Add(5 + i);
            indices.Add(2 + i);

            indices.Add(5 + i);
            indices.Add(6 + i);
            indices.Add(2 + i);


            indices.Add(2 + i);
            indices.Add(6 + i);
            indices.Add(3 + i);

            indices.Add(6 + i);
            indices.Add(7 + i);
            indices.Add(3 + i);


            indices.Add(7 + i);
            indices.Add(0 + i);
            indices.Add(3 + i);

            indices.Add(7 + i);
            indices.Add(4 + i);
            indices.Add(0 + i);

            List<Vertex> verticesInner = new List<Vertex>();
            foreach (Vector3 position in positions)
            {
                verticesInner.Add(new Vertex(position, material, new Vector2(position.X, position.Y)));
            }
            Mesh outer = new Mesh(verticesInner, indices);


            indices.Clear();
            positions.Clear();
            positions.AddRange(extrudedQuad.ToList());

            indices.Add(0);
            indices.Add(3);
            indices.Add(1);

            indices.Add(3);
            indices.Add(2);
            indices.Add(1);

            List<Vertex> verticesOuter = new List<Vertex>();
            foreach (Vector3 position in positions)
            {
                verticesOuter.Add(new Vertex(position, innerMaterial, new Vector2(position.X, position.Y)));
            }

            Mesh inner = new Mesh(verticesOuter, indices);
            outer += inner;
            outer.makeFlat(true, true);
            return outer;
        }
        public static Mesh generatePlane(Material material, Vector2 size)
        {
            return generatePlane(size, new Vector2i(1, 1), material);
        }
        public static Mesh generatePlane(Material material)
        {
            return generatePlane(new Vector2(1f), new Vector2i(1, 1), material);
        }
        public static Mesh generatePlane(Vector2 size, Vector2i resolution, Material material, bool centerX = true, bool centerY = true)
        {
            int numverticesX = resolution.X + 1;
            int numverticesY = resolution.Y + 1;

            List<Vertex> vertices = new List<Vertex>(numverticesX *numverticesY);
            for (int z = 0; z< numverticesY; z++)
            {
                for (int x = 0; x < numverticesX; x++)
                {
                    float xRatio = x / ((float)resolution.X);
                    float zRatio = z / ((float)resolution.Y);
                    vertices.Add( new Vertex(new Vector3(xRatio*size.X, 0,  zRatio * size.Y), material, new Vector2(xRatio, zRatio)));
                }
            }

            List<int> indices = new List<int>(6*resolution.X*resolution.Y);
            for (int y = 0; y < resolution.Y; y++)
            {
                for (int x = 0; x < resolution.X; x++)
                {
                    indices.Add(y * numverticesX + x);
                    indices.Add((y + 1) * numverticesX + x);
                    indices.Add(y * numverticesX + (x+1));

                    indices.Add(y * numverticesX + (x + 1));
                    indices.Add((y + 1) * numverticesX + x);
                    indices.Add((y+1) * numverticesX + (x + 1));
                }
            }
            Mesh rawModel = new Mesh(vertices, indices);
            if (centerX)
            {
                rawModel.translate(new Vector3(-size.X/2f, 0f, 0f));
            }
            if (centerY)
            {
                rawModel.translate(new Vector3(0f, 0, -size.Y / 2f));
            }

            return rawModel;
        }
    }
}

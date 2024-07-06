
using OpenTK.Mathematics;
using System.Reflection.Emit;
using System;
using System.Drawing;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Util;
using System.Net.NetworkInformation;

namespace Dino_Engine.Modelling
{
    internal class MeshGenerator
    {


        public static Mesh GenerateCone(Material material)
        {


            List<Vector3> trunkLayers = new List<Vector3>() {
                //new Vector3(outerRadius, 0f, outerRadius),
                new Vector3(0, 0, 0),
                new Vector3(1, 1, 1),
                new Vector3(0, 1, 0) };
            Mesh mesh = MeshGenerator.generateCylinder(trunkLayers, 12, material);
            mesh.rotate(new Vector3(MathF.PI, 0f, 0f));
            return mesh;
        }
        public static Mesh generateCylinder(List<Vector2> rings2, int polygonsPerRing, Material material, bool sealTop = false)
        {
            List<Vector3> rings = new List<Vector3> ();
            for (int i = 0; i<rings2.Count; i++)
            {
                rings.Add(new Vector3(rings2[i].X, rings2[i].Y, rings2[i].X));
            }
            return generateCylinder(rings, polygonsPerRing, material, sealTop);
        }
            public static Mesh generateCylinder(List<Vector3> rings, int polygonsPerRing, Material material, bool sealTop = false)
        {
            float PI = MathF.PI;
            List<Vector3> positions = new List<Vector3>();
            List<int> indices = new List<int>();

            for (int ring = 0; ring < rings.Count; ring++)
            {
                for (int detail = 0; detail < polygonsPerRing; detail++)
                {
                    float x1 = MathF.Sin(2f * PI * ((float)detail / polygonsPerRing)) * rings[ring].X;
                    float z1 = MathF.Cos( 2f * PI * ((float)detail / polygonsPerRing)) * rings[ring].Z;
                    float y1 = rings[ring].Y;
                    Vector3 p1 = new Vector3(x1, y1, z1);

                    positions.Add(p1);

                    if (ring < rings.Count - 1)
                    {
                        indices.Add((ring + 1) * polygonsPerRing + (detail + 0) % polygonsPerRing);
                        indices.Add((ring + 0) * polygonsPerRing + (detail + 0) % polygonsPerRing);
                        indices.Add((ring + 0) * polygonsPerRing + (detail + 1) % polygonsPerRing);

                        indices.Add((ring + 1) * polygonsPerRing + (detail + 0) % polygonsPerRing);
                        indices.Add((ring + 0) * polygonsPerRing + (detail + 1) % polygonsPerRing);
                        indices.Add((ring + 1) * polygonsPerRing + (detail + 1) % polygonsPerRing);
                    }
                }
            }
            if (sealTop)
            {
                int ring = rings.Count - 1;
                float x1 = 0;
                float z1 = 0;
                float y1 = rings[ring].Y;
                Vector3 p1 = new Vector3(x1, y1, z1);

                positions.Add(p1);
                int i = positions.Count - 1;
                for (int detail = 0; detail < polygonsPerRing; detail++)
                {


                    indices.Add(i);
                    indices.Add((ring) * polygonsPerRing + (detail + 0) % polygonsPerRing);
                    indices.Add((ring) * polygonsPerRing + (detail + 1) % polygonsPerRing);
                    
                }
            }
            return new Mesh(positions, indices, material);
        }
        public static Mesh generateBox(Material material)
        {
            return generateBox(new Vector3(-0.5f), new Vector3(0.5f), material);
        }
        public static Mesh generateShape(List<Vector3> shape, Material material)
        {
            List<Vector3> side2 = new List<Vector3>();
            for (int i = 0; i < shape.Count; i++)
            {
                side2.Add(new Vector3(-1f * shape[i].X, shape[i].Y, shape[i].Z));
            }
            return generateShape(shape, side2, material);
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
                mesh += ExtrudedPlane(quad, depth,size, material, innerMateria);
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

        public static Mesh generateShape(List<Vector3> botShape, List<Vector3> topShape, Material material, bool mirror = false)
        {
            List<Vector3> postions = new List<Vector3>();

            List<int> indices = new List<int>();

            postions.AddRange(botShape);
            postions.AddRange(topShape);


            int topOffset = botShape.Count;
            for (int i = 0; i <botShape.Count-1; i++)
            {
                indices.Add(i);
                indices.Add(i+topOffset);
                indices.Add(i + 1);

                indices.Add(i+topOffset);
                indices.Add(i+1+topOffset);
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

            return new Mesh(postions, indices, material);
        }

        public static Mesh generateBox(Vector3 min, Vector3 max, Material material)
        {
            float[] positions = {
                min.X, max.Y, max.Z,
                max.X, max.Y, max.Z,
                max.X, max.Y, min.Z,
                min.X, max.Y, min.Z,

                min.X, min.Y, max.Z,
                max.X, min.Y, max.Z,
                max.X, min.Y, min.Z,
                min.X, min.Y, min.Z};

            int[] indices = {0,1,3, 3,1,2, //top
                        0,4,1, 1,4,5,  //front
                        1,5,6, 2,1,6,  // right
                        6,7,2, 3,2,7,  //back
                        3,7,4, 0,3,4,  //left
                        6,5,7, 7,5,4};  //bot

            Mesh rawModel = new Mesh(positions, indices, material);

            rawModel.makeFlat(true, true);
            return rawModel;
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

        public static Mesh ExtrudedPlane(Quad startQuad, float extrudeDepth, float extrudeScale,Material material, Material innerMaterial)
        {
            Vector3 extrudeDirection = startQuad.GetNormal();

            Vector3 center = startQuad.GetCenter();

            Quad innerQuad = startQuad;
            innerQuad.translate(-center);
            innerQuad.scale((new Vector3(extrudeScale)));
            innerQuad.translate(center);

            Quad extrudedQuad = innerQuad;
            extrudedQuad.translate(extrudeDirection* extrudeDepth);

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
            indices.Add(0+i);
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


            Mesh outer = new Mesh(positions, indices, material);


            indices.Clear();
            positions.Clear();
            positions.AddRange(extrudedQuad.ToList());

            indices.Add(0);
            indices.Add(3);
            indices.Add(1);

            indices.Add(3);
            indices.Add(2);
            indices.Add(1);
            Mesh inner = new Mesh(positions, indices, innerMaterial);
            outer += inner;
            outer.makeFlat(true, true);
            return outer;
        }

        public static void ExtrudedPlane(Material material, Material innerMaterial)
        {

        }
            public static Mesh ExtrudedPlane(Vector3 extrusionSize, Material material, Material innerMaterial)
        {
            float scaleFactor = 1f / MathF.Sqrt(2f);

            float outerRadius = scaleFactor;
            Vector2 innerRadius = extrusionSize.Xy*scaleFactor;
            float innerDepth = extrusionSize.Z;
            List<Vector3> trunkLayers = new List<Vector3>() {
                //new Vector3(outerRadius, 0f, outerRadius),
                new Vector3(outerRadius, 0, outerRadius),
                new Vector3(innerRadius.X, 0, innerRadius.Y),
                new Vector3(innerRadius.X, innerDepth, innerRadius.Y) };
            Mesh mesh = MeshGenerator.generateCylinder(trunkLayers, 4, material);

            Mesh inner = generatePlane(innerMaterial);
            inner.scale(extrusionSize);
            inner.translate(new Vector3(0f, 0f, -extrusionSize.Z));
            inner.rotate(new Vector3(MathF.PI, 0f, 0f));


            mesh.rotate(new Vector3(MathF.PI / 2f, MathF.PI / 4f, 0f));

            mesh += inner;
            mesh.makeFlat(true, true);
            return mesh;
        }
        public static Mesh generatePlane(Material material)
        {
            return generatePlane(new Vector2(1f), material);
        }
            public static Mesh generatePlane(Vector2 size, Material material)
        {
            Vector2 r = size * 0.5f;
            float[] positions = {
                -r.X, -r.Y, 0,
                -r.X, r.Y, 0,
                r.X, r.Y, 0,
                r.X, -r.Y, 0};

            int[] indices = { 0, 1, 2, 3, 0, 2 };

            Mesh rawModel = new Mesh(positions, indices, material);
            return rawModel;
        }
    }
}

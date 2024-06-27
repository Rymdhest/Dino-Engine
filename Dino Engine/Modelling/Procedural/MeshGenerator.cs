
using OpenTK.Mathematics;
using System.Reflection.Emit;
using System;
using System.Drawing;
using Dino_Engine.Modelling.Model;

namespace Dino_Engine.Modelling
{
    internal class MeshGenerator
    {

        public static Mesh generateCylinder(List<Vector3> rings, int polygonsPerRing, Material material)
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
            return new Mesh(positions, indices, material);
        }
        public static Mesh generateBox(Material material)
        {
            return generateBox(new Vector3(-0.5f), new Vector3(0.5f), material);
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
            return rawModel;
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

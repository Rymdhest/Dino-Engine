

using Dino_Engine.Core;
using Dino_Engine.ECS;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Rendering;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using System.Reflection.Emit;
namespace Dino_Engine.Modelling.Procedural.Nature
{
    public class TreeGenerator
    {

        public VertexMaterial trunkMaterial = new VertexMaterial(TextureGenerator.bark, new Colour(107, 84, 61));
        public VertexMaterial leafMaterial = new VertexMaterial(TextureGenerator.flat, new Colour(195, 231, 73));


        public static Mesh GenerateLeaf()
        {
            VertexMaterial leafMaterial = new VertexMaterial(TextureGenerator.bark, new Colour(100, 170, 15));

            Mesh leafMesh = MeshGenerator.generatePlane(new Vector2(0.15f, 1f), new Vector2i(50, 50), leafMaterial);
            leafMesh.rotate(new Vector3(MathF.PI/2f, 0f, 0f));

            for (int i = 0; i < leafMesh.meshVertices.Count; i++)
            {
                leafMesh.meshVertices[i].position.X *= 2f + MathF.Sin((leafMesh.meshVertices[i].position.Y + 0.25f) * MathF.Tau * 1.0f) * 0.99f;
                //leafMesh.meshVertices[i].position.X += MathF.Sin(leafMesh.meshVertices[i].position.Y * MathF.Tau * 10f) * 0.005f;

                leafMesh.meshVertices[i].position.Z += MathF.Sin(leafMesh.meshVertices[i].position.X * MathF.Tau * 20f) * 0.001f;


                //leafMesh.meshVertices[i].position.Z = MathF.Sin(leafMesh.meshVertices[i].position.X * MathF.Tau * 15f) * 0.000005f;
            }



            leafMesh.rotate(new Vector3(0, 0f, 0f));

            return leafMesh;
        }

        public Mesh GenerateTree()
        {
            var controlPoints = new List<Vector3>
            {
                new Vector3(0, 0, 0),
                new Vector3(0, 2, 0),
                new Vector3(2, 4, 0),
                new Vector3(3, 6, 0),
                new Vector3(3, 8, 0)
            };
            controlPoints = new List<Vector3>
            {
                new Vector3(0, 0, 0),
                new Vector3(0, 5, 0),
                new Vector3(2.5f, 5, 0),
                new Vector3(5, 5, 0),
                new Vector3(5, 0, 10)
            };
            controlPoints.Clear();

            int n = 100;
            float[] sinFBM = FBMmisc.sinFBM(5, 0.23f, n);
            float[] sinFBM2 = FBMmisc.sinFBM(5, 0.15f, n);
            float r = 2.0f;
            float h = 50f;
            for (int i = 0; i < n; i++)
            {
                float traversedRatio = i / (float)(n - 1);
                float angle = MathF.PI * i * 0.2f;
                float x = sinFBM[i] * r * traversedRatio;
                float z = sinFBM2[i] * r * traversedRatio;
                float y = traversedRatio * h;
                controlPoints.Add(new Vector3(x, y, z));
            }

            CardinalSpline3D spline = new CardinalSpline3D(controlPoints, 0.0f);



            Curve3D curve = spline.GenerateCurve(1);
            curve.LERPWidth(1.3f, 0.1f);
            Mesh cylinderMesh = MeshGenerator.generateCurvedTube(curve, 11, trunkMaterial, textureRepeats: 1, flatStart: true);

            Mesh branch = MeshGenerator.generatePlane(new Vector2(40f, 40f), new Vector2i(2, 2), new VertexMaterial(TextureGenerator.treeBranch), centerY: false);
            for (int i = 0; i < branch.meshVertices.Count; i++)
            {
                branch.meshVertices[i].position.Z -= MathF.Abs(MathF.Pow(branch.meshVertices[i].position.X, 2.0f)) * 0.05f;
                branch.meshVertices[i].position.Z -= MathF.Abs(MathF.Pow(branch.meshVertices[i].position.Y, 2.0f)) * 0.015f;
            }
            branch.translate(new Vector3(0f, -2f, 0.0f));
            branch.rotate(new Vector3(-MathF.PI / 1.45f, 0f, 0f));


            Mesh branch2 = cylinderMesh.scaled(new Vector3(1.0f, 1f, 1.0f));
            int nTwigs = 40;
            for (int i = 0; i < nTwigs; i++)
            {
                float t = 0.5f + 0.5f * (float)i / (nTwigs - 1);
                CurvePoint curvePoint = curve.getPointAt(t);
                var newBranch = branch.scaled(new Vector3(0.6f - t * 0.4f));
                Vector3 col = MyMath.rng3D(0.3f);
                newBranch.setColour(new Colour(new Vector3(1f) - col));
                newBranch.rotate(new Vector3(0.9f - t * 0.5f, 0f, 0f));
                newBranch.translate(new Vector3(0f, -curvePoint.width / 2f, 0f));
                //newBranch.translate(new Vector3(0f, 0f, -curvePoint.width / 2f));
                //newBranch.rotate(new Vector3(0f, i * MathF.Tau / 3f, 0f));
                newBranch.rotate(new Vector3(0f, MyMath.rng() * MathF.Tau, 0f));
                newBranch.rotate(curvePoint.rotation);
                newBranch.translate(curvePoint.pos);
                branch2 += newBranch;
            }
            //branch = cylinderMesh;

            //branch = MeshGenerator.generateBox(Material.ROCK);
            //branch.scale(new Vector3(0.3f, 0.3f, 5f));
            //branch.translate(new Vector3(0f, 0f, -2.5f));
            int nBranches = 15;
            for (int i = 0; i < nBranches; i++)
            {
                float t = 0.3f + 0.7f * (float)i / (nBranches - 1);
                CurvePoint curvePoint = curve.getPointAt(t);
                var newBranch = branch2.scaled(new Vector3(0.5f - t * 0.4f));
                newBranch.rotate(new Vector3(.6f + t * 0.2f, 0f, 0f));
                newBranch.translate(new Vector3(0f, -curvePoint.width / 2f, 0f));
                //newBranch.translate(new Vector3(0f, 0f, -curvePoint.width / 2f));
                newBranch.rotate(new Vector3(0f, i * MathF.Tau / 3f + MyMath.rngMinusPlus(MathF.Tau / 6f), 0f));
                //newBranch.rotate(new Vector3(0f, MyMath.rng() * MathF.Tau, 0f));
                newBranch.rotate(curvePoint.rotation);
                newBranch.translate(curvePoint.pos);
                cylinderMesh += newBranch;
            }

            return cylinderMesh;
        }

        public Mesh GenerateFractalTree(int depth)
        {
            List<Vector2> layers = new List<Vector2>() {
                new Vector2(1.0f, 0),
                new Vector2(1.0f, 10.0f),
                new Vector2(1.0f, 40.0f),
                new Vector2(1.0f, 80.0f)};
            Mesh poleMesh = MeshGenerator.generateCylinder(layers, 50, trunkMaterial, sealTop: 0.1f);

            foreach (MeshVertex meshVertex in poleMesh.meshVertices)
            {
                float angle = MathF.Atan2(meshVertex.position.X, meshVertex.position.Z) * 12.0f;
                meshVertex.position += new Vector3(MathF.Sin(angle), 0f, MathF.Cos(angle)) * 0.01f;

                if (meshVertex.position.Y < 1f)
                {
                    meshVertex.colour = new Colour(125, 165, 85);
                    float valueX = MathF.Pow((MathF.Sin(angle)), 1.0f);
                    float valueZ = MathF.Pow((MathF.Cos(angle)), 1.0f);
                    meshVertex.position += (new Vector3(valueX, 0f, valueZ) * .05f);
                }
            }

            poleMesh.FlatRandomness(new Vector3(.05f, 0f, .05f));
            return poleMesh;
        }
    }
}

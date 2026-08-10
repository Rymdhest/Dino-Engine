

using Dino_Engine.Core;
using Dino_Engine.ECS;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Rendering;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using System.Reflection.Emit;
using static OpenTK.Graphics.OpenGL.GL;
namespace Dino_Engine.Modelling.Procedural.Nature
{
    public class TreeGenerator
    {

        public VertexMaterial trunkMaterial = new VertexMaterial(TextureGenerator.bark, new Colour(107, 84, 61));
        public VertexMaterial leafMaterial = new VertexMaterial(TextureGenerator.flat, new Colour(195, 231, 73));

        public static Mesh GenerateFlowerBush()
        {
            VertexMaterial leafMaterial = new VertexMaterial(TextureGenerator.fernBranch, new Colour(255, 255, 255));



            float leafSize = 0.3f;
            Mesh leafMesh = MeshGenerator.generatePlane(new Vector2(leafSize), new Vector2i(1, 1), new VertexMaterial(TextureGenerator.leaf));
            //leafMesh.rotate(new Vector3(MathF.PI / 2f, 0, 0f));
            leafMesh.rotate(new Vector3(0, MathF.PI / 2f, 0));
            //leafMesh.rotate(new Vector3(0, 0f, MathF.PI / 4f));
            leafMesh.translate(new Vector3(-leafSize / 2f, 0f, 0f));


            Mesh flowerPlant = new Mesh();

            var controlPoints = new List<Vector3>();
            int n = 10;
            float[] sinFBM = FBMmisc.sinFBM(4, 0.6f, n);
            float[] sinFBM2 = FBMmisc.sinFBM(4, 0.9f, n);
            float r = 0.03f;
            float h = 1f;
            for (int i = 0; i < n; i++)
            {
                float traversedRatio = i / (float)(n - 1);
                float angle = MathF.PI * i * 0.6f;
                float x = sinFBM[i] * r * traversedRatio;
                float z = sinFBM2[i] * r * traversedRatio * 0.05f;
                float y = traversedRatio * h;
                controlPoints.Add(new Vector3(x, y, z));
            }
            CardinalSpline3D spline = new CardinalSpline3D(controlPoints, 0.0f);

            Curve3D curve = spline.GenerateCurve(3);
            curve.LERPWidth(0.03f, 0.02f);
            Mesh stem = MeshGenerator.generateCurvedTube(curve, 3, new VertexMaterial(TextureGenerator.grass), textureRepeats: 1, flatStart: true);
            flowerPlant += stem;


            int leavesAlongStem = 7;
            for (int i = 0; i < leavesAlongStem; i++)
            {
                float t = 0.25f + 0.75f * MathF.Pow((float)i / (leavesAlongStem - 1), 0.8f);
                CurvePoint curvePoint = curve.getPointAt(t);
                var newBranch = leafMesh.scaled(new Vector3(1.0f - t * 0.7f));
                Vector3 col = MyMath.rng3D(0.15f);
                newBranch.setColour(new Colour(new Vector3(1f) - col));
                newBranch.rotate(new Vector3(0f, 0f, 0.9f - t * 0.5f));
                newBranch.rotate(new Vector3(0f, MyMath.rng()*MathF.Tau, 0f));
                newBranch.rotate(curvePoint.rotation);
                newBranch.translate(curvePoint.pos);
                flowerPlant += newBranch;
            }
            Mesh flower = new Mesh();

            int leavesAroundFlower = 7;
            CurvePoint curvePointTop = curve.getPointAt(1.0f);
            for (int i = 0; i < leavesAroundFlower; i++)
            {
                float t = (float)i / (leavesAroundFlower);
                var newBranch = leafMesh.scaled(new Vector3(0.35f)).rotated(new Vector3(0f, 0f, -0.4f));
                Vector3 col = new Vector3(10.9f, 0.2f, 0.8f); ;
                newBranch.setColour(new Colour(col));
                newBranch.rotate(new Vector3(0f, t * MathF.Tau, 0f));
                flower += newBranch;
            }

            flowerPlant += flower.rotated(curvePointTop.rotation).translated(curvePointTop.pos);

            Mesh flowerBush = new Mesh();

            int flowerPlantsInBush = 17;
            for (int i = 0; i < flowerPlantsInBush; i++)
            {
                float spread = 0.3f;
                Mesh tempFlower = flowerPlant.rotated(new Vector3(MyMath.rngMinusPlus(0.5f), MathF.Tau * MyMath.rng(), MyMath.rngMinusPlus(0.5f)));
                tempFlower.scale(new Vector3(1f, 1f+MyMath.rngMinusPlus(0.35f), 1f));
                tempFlower.translate(new Vector3(MyMath.rngMinusPlus(spread), 0f, MyMath.rngMinusPlus(spread)));
                flowerBush += tempFlower;
            }
            return flowerBush;
        }

        public static Mesh GenerateFern()
        {
            VertexMaterial leafMaterial = new VertexMaterial(TextureGenerator.fernBranch, new Colour(255, 255, 255));

            Vector2 leafSize = new Vector2(0.7f, 1f) * 1f;





            Mesh fern = new Mesh();

            int numberFerns = 15;
            for (int j = 0;j<numberFerns; j++)
            {
                Mesh leafMesh = MeshGenerator.generatePlane(leafSize, new Vector2i(2, 4), leafMaterial, centerY: false);
                leafMesh.rotate(new Vector3(-MathF.PI / 2f, 0f, 0f));
                float ratio = (float)j / (numberFerns);
                float scale = 1f + MyMath.rng(2f);
                for (int i = 0; i < leafMesh.meshVertices.Count; i++)
                {
                    Vector2 bendyness = new Vector2(0.75f, 0.3f+(1f-scale/3f)*2f);
                    float ratioY = leafMesh.meshVertices[i].position.Y / leafSize.Y;
                    float ratioX = MathF.Abs(leafMesh.meshVertices[i].position.X / leafSize.X);
                    leafMesh.meshVertices[i].position.Z += bendyness.Y - MathF.Pow(1f - ratioY, 0.5f) * bendyness.Y;
                    leafMesh.meshVertices[i].position.Z += bendyness.X - MathF.Pow(1f - ratioX, 0.5f) * bendyness.X;

                    //leafMesh.meshVertices[i].position.Y = MathF.Pow(ratioY, 0.5f) * bendyness.Y;
                }

                fern += leafMesh.rotated(new Vector3(0f, ratio*MathF.Tau, 0f)).scaled(new Vector3(scale));
            }

            return fern;
        }

        public static Mesh GenerateFernLeaf()
        {
            VertexMaterial leafMaterial = new VertexMaterial(TextureGenerator.grass, new Colour(135, 155, 145));

            Mesh leafMesh = MeshGenerator.generatePlane(new Vector2(1f, 1f), new Vector2i(50, 50), leafMaterial);
            leafMesh.rotate(new Vector3(MathF.PI / 2f, 0f, 0f));

            float wholeLeafBend = 1.0f;
            float stemBend = 0.5f;

            for (int i = 0; i < leafMesh.meshVertices.Count; i++)
            {
                float ratioY = leafMesh.meshVertices[i].position.Y + 0.5f;
                float width = leafMesh.meshVertices[i].position.X;
                width *= MathF.Sin((MathF.Pow(ratioY, 0.6f)) * MathF.PI/2f) + 0.001f + MathF.Sin(ratioY * 6 * MathF.Tau) * 0.1f;
                //width *= MathF.Pow(2, 0.9f+ratioY*0.1f);
                leafMesh.meshVertices[i].position.X = width * 0.5f;

                //leafMesh.meshVertices[i].position.X += MathF.Sin(leafMesh.meshVertices[i].position.Y * MathF.Tau * 10f) * 0.005f;

                //leafMesh.meshVertices[i].position.Z += MathF.Sin(leafMesh.meshVertices[i].position.X * MathF.Tau * 20f) * 0.001f;


                //leafMesh.meshVertices[i].position.Z = MathF.Sin(leafMesh.meshVertices[i].position.X * MathF.Tau * 15f) * 0.000005f;
            }
            leafMesh.ProjectUVsWorldSpaceCube(1.0f);


            leafMesh.rotate(new Vector3(0, 0f, 0f));

            return leafMesh;
        }

        public static Mesh GenerateLeaf()
        {
            VertexMaterial leafMaterial = new VertexMaterial(TextureGenerator.grass, new Colour(135, 155, 145));

            Mesh leafMesh = MeshGenerator.generatePlane(new Vector2(1f, 1f), new Vector2i(50, 50), leafMaterial);
            leafMesh.rotate(new Vector3(MathF.PI/2f, 0f, 0f));

            float wholeLeafBend = 1.0f;
            float stemBend = 0.5f;

            for (int i = 0; i < leafMesh.meshVertices.Count; i++)
            {
                float ratioY = leafMesh.meshVertices[i].position.Y+0.5f;
                float width = leafMesh.meshVertices[i].position.X;
                width *= MathF.Sin((MathF.Pow(ratioY, 0.7f)) * MathF.PI) + 0.001f+MathF.Sin(ratioY*16* MathF.Tau)*0.035f;
                //width *= MathF.Pow(2, 0.9f+ratioY*0.1f);
                leafMesh.meshVertices[i].position.X = width*0.5f;

                //leafMesh.meshVertices[i].position.X += MathF.Sin(leafMesh.meshVertices[i].position.Y * MathF.Tau * 10f) * 0.005f;

                //leafMesh.meshVertices[i].position.Z += MathF.Sin(leafMesh.meshVertices[i].position.X * MathF.Tau * 20f) * 0.001f;


                //leafMesh.meshVertices[i].position.Z = MathF.Sin(leafMesh.meshVertices[i].position.X * MathF.Tau * 15f) * 0.000005f;
            }
            leafMesh.ProjectUVsWorldSpaceCube(1.0f);


            leafMesh.rotate(new Vector3(0, 0f, 0f));

            return leafMesh;
        }

        public static Mesh GenerateDeadTree()
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

            int n = 20;
            float[] sinFBM = FBMmisc.sinFBM(5, 0.23f, n);
            float[] sinFBM2 = FBMmisc.sinFBM(5, 0.15f, n);
            float r = 0.2f;
            float h = 15f;
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
            curve.LERPWidth(1.3f, 1.1f);
            Mesh cylinderMesh = MeshGenerator.generateCurvedTube(curve, 11, new VertexMaterial(TextureGenerator.bark, new Colour(215, 255, 135)), textureRepeats: 1, flatStart: true, sealTop:-0.2f);
            Mesh deadTree = new Mesh();
            deadTree += cylinderMesh;
            Mesh branch2 = cylinderMesh.scaled(new Vector3(1.0f, 1f, 1.0f));
            int nTwigs =5;
            for (int i = 0; i < nTwigs; i++)
            {
                float t = 0.25f + 0.55f * (float)i / (nTwigs - 1);
                CurvePoint curvePoint = curve.getPointAt(t);
                var newBranch = cylinderMesh.scaled(new Vector3(0.4f - t * 0.2f)*0.6f);
                newBranch.rotate(new Vector3(1.2f - t * 0.5f, 0f, 0f));
                newBranch.translate(new Vector3(0f, -curvePoint.width / 2f, 0f));
                //newBranch.translate(new Vector3(0f, 0f, -curvePoint.width / 2f));
                //newBranch.rotate(new Vector3(0f, i * MathF.Tau / 3f, 0f));
                newBranch.rotate(new Vector3(0f, MyMath.rng() * MathF.Tau, 0f));
                newBranch.rotate(curvePoint.rotation);
                newBranch.translate(curvePoint.pos);
                deadTree += newBranch;
            }

            for (int i = 0; i < nTwigs; i++)
            {
                float t = 0.25f + 0.55f * (float)i / (nTwigs - 1);
                CurvePoint curvePoint = curve.getPointAt(t);
                var newBranch = cylinderMesh.scaled(new Vector3(0.4f - t * 0.2f) * 0.6f*new Vector3(1f, 2f, 1f));
                newBranch.rotate(new Vector3(1.2f - t * 0.5f, 0f, 0f));
                newBranch.translate(new Vector3(0f, -curvePoint.width / 2f, 0f));
                //newBranch.translate(new Vector3(0f, 0f, -curvePoint.width / 2f));
                //newBranch.rotate(new Vector3(0f, i * MathF.Tau / 3f, 0f));
                newBranch.rotate(new Vector3(0f, MyMath.rng() * MathF.Tau, 0f));
                newBranch.rotate(curvePoint.rotation);
                newBranch.translate(curvePoint.pos);
                deadTree += newBranch;
            }


            return deadTree;
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
            int nBranches = 10;
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

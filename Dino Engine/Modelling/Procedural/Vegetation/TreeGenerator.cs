using Dino_Engine.Core;
using Dino_Engine.ECS;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Vegetation;
using Dino_Engine.Modelling.Procedural.Vegetation;
using Dino_Engine.Rendering;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using OpenTK.Compute.OpenCL;
using OpenTK.Mathematics;
using System.Reflection.Emit;
using System.Reflection.PortableExecutable;
using System.Xml.Serialization;
using static OpenTK.Graphics.OpenGL.GL;
namespace Dino_Engine.Modelling.Procedural.Nature
{
    public class TreeGenerator
    {

        public static Mesh GenerateFlowerBush(Vector3 flowerColour)
        {


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
            float h = 1.5f;
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
            Mesh stem = MeshGenerator.generateCurvedTube(curve, 3, new VertexMaterial(TextureGenerator.grass, new Colour(200, 150, 200)), textureRepeats: 1, flatStart: true);
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
                newBranch.setColour(new Colour(flowerColour));
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

            Vector2 leafSize = new Vector2(0.5f,1f) * 1f;





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

        public static Mesh generatePineTree(int numberBranches, float branchStartRatio, bool alive = true, bool fallen = false)
        {
            float radius = 0.7f;

            TreeBuilder builder = new TreeBuilder(new VertexMaterial(TextureGenerator.pineBark, new Colour(255, 255, 255)));
            TreeBuilder.StemBuildSettings stemSettings = new TreeBuilder.StemBuildSettings();
            stemSettings.radiusBase = radius;
            stemSettings.detailsHeight = 3;
            stemSettings.detailPerRing = 32;
            stemSettings.radiusTop = 0.03f;
            stemSettings.stemBendRadius = 0.7f;
            stemSettings.height = 30.0f;
            stemSettings.sinkAmount = 3f;
            stemSettings.stemBaseHeight = 3f;
            stemSettings.stemWavePatternAmount = 0.1f;
            stemSettings.baseRadiusFactor = 0.6f;
            stemSettings.textureRepeats = 2;
            stemSettings.baseColor = new Colour(115, 215, 115);
            builder.BuildStem(stemSettings);
            for (int i = 0; i < builder.mesh.meshVertices.Count; i++)
            {
                float t =MathF.Pow( MyMath.rng(), 3.0f);
                builder.mesh.meshVertices[i].colour = new Colour(MyMath.lerp(builder.mesh.meshVertices[i].colour.ToVector3(), new Vector3(0.5f, 1f, 0.5f), t));
            }
            TreeBuilder BranchBuilder = new TreeBuilder(new VertexMaterial(TextureGenerator.pineBark, new Colour(255, 255, 255)));


            TreeBuilder.StemBuildSettings branchSettings = new TreeBuilder.StemBuildSettings();
            branchSettings.detailsHeight = 1;
            branchSettings.detailPerRing = 3;
            branchSettings.height = 7f;
            branchSettings.radiusBase = 0.45f;
            branchSettings.radiusTop = 0.05f;
            branchSettings.baseRadiusFactor = 0.2f;
            branchSettings.sinkAmount = 0.5f;
            branchSettings.stemBendRadius = 0.4f;
            branchSettings.textureRepeats = 1;
            BranchBuilder.BuildStem(branchSettings);

            float branchSize = alive ? 10f : 8f;
            int numberTwigs = alive ? 10 : 5;
            VertexMaterial branchTexture = alive ? new VertexMaterial(TextureGenerator.pineBranch) : new VertexMaterial(TextureGenerator.deadTwig);
            Mesh leafMesh = MeshGenerator.generatePlane(new Vector2(branchSize), new Vector2i(2, 4), branchTexture, centerY: false);
            for (int i = 0; i < leafMesh.meshVertices.Count; i++)
            {
                leafMesh.meshVertices[i].position.Y += MathF.Abs(MathF.Pow(leafMesh.meshVertices[i].position.X, 2.0f)) * 0.1f;
                leafMesh.meshVertices[i].position.Y += MathF.Abs(MathF.Pow(leafMesh.meshVertices[i].position.Z, 2.0f)) * 0.05f;
            }
            //leafMesh += leafMesh.rotated(new Vector3(0f, 0f, MathF.PI / 2f));
            TreeBuilder.SpreadAroundStemSettings twigSpawnSettings = new TreeBuilder.SpreadAroundStemSettings();
            twigSpawnSettings.BranchRandomRotation.Z = .0f;
            twigSpawnSettings.BranchStartRotation.X = 0;
            twigSpawnSettings.BranchEndRotation.X = 0;
            twigSpawnSettings.BranchStartScale = new Vector3(1f);
            twigSpawnSettings.BranchEndScale = new Vector3(0.6f);
            twigSpawnSettings.numberBranches = numberTwigs;
            twigSpawnSettings.startStemRatio = 0.05f;
            BranchBuilder.SpreadMeshAroundStem(leafMesh.rotated(new Vector3(-MathF.PI * 0.25f, 0f, 0f)), twigSpawnSettings);


            TreeBuilder.SpreadAroundStemSettings branchSpawnSettings = new TreeBuilder.SpreadAroundStemSettings();
            branchSpawnSettings.BranchRandomRotation.Z = 1.0f;
            branchSpawnSettings.BranchStartRotation.X = 0.95f;
            branchSpawnSettings.BranchEndRotation.X = 0.45f;
            branchSpawnSettings.numberBranches = numberBranches;
            branchSpawnSettings.startStemRatio = branchStartRatio;
            branchSpawnSettings.BranchEndScale = new Vector3(0.13f);

            builder.SpreadMeshAroundStem(BranchBuilder.mesh.rotated(new Vector3(MathF.PI / 2f, 0f, 0f)), branchSpawnSettings);


            Mesh deadTwigMesh = MeshGenerator.generatePlane(new Vector2(2f, 2f), new Vector2i(2, 4), new VertexMaterial(TextureGenerator.deadTwig, new Colour(255, 255, 255)), centerY: false);
            for (int i = 0; i < deadTwigMesh.meshVertices.Count; i++)
            {
                deadTwigMesh.meshVertices[i].position.Y -= MathF.Abs(MathF.Pow(deadTwigMesh.meshVertices[i].position.X, 2.0f)) * 0.8f;
                deadTwigMesh.meshVertices[i].position.Y += MathF.Abs(MathF.Pow(deadTwigMesh.meshVertices[i].position.Z, 2.0f)) * 0.4f;
            }
            deadTwigMesh += deadTwigMesh.rotated(new Vector3(0f, 0f, MathF.PI / 2f));

            TreeBuilder.SpreadAroundStemSettings deadTwigSpawnSettings = new TreeBuilder.SpreadAroundStemSettings();
            deadTwigSpawnSettings.BranchRandomRotation.Z = 0.0f;
            deadTwigSpawnSettings.BranchStartRotation.X = 0.0f;
            deadTwigSpawnSettings.BranchEndRotation.X = 0.0f;
            deadTwigSpawnSettings.numberBranches = 5;
            deadTwigSpawnSettings.randomSpin = true;
            deadTwigSpawnSettings.startStemRatio = 0.2f;
            deadTwigSpawnSettings.endStemRatio = branchStartRatio;
            deadTwigSpawnSettings.branchRandomScale = 0.5f;
            deadTwigSpawnSettings.BranchEndScale = new Vector3(0.2f);

            builder.SpreadMeshAroundStem(deadTwigMesh.rotated(new Vector3(MathF.PI * 0.25f, 0f, 0f)), deadTwigSpawnSettings);

            return builder.mesh;
        }

        public static Mesh GenerateFallenPineTree()
        {
            Colour mossColour = new Colour(115, 215, 115);
            float radiusBase = 0.8f;
            TreeBuilder builder = new TreeBuilder(new VertexMaterial(TextureGenerator.pineBark, new Colour(255, 255, 255)));
            TreeBuilder.StemBuildSettings stemSettings = new TreeBuilder.StemBuildSettings();
            stemSettings.radiusBase = radiusBase;
            stemSettings.radiusTop = 0.8f;
            stemSettings.stemBendRadius = 0.1f;
            stemSettings.height = 10.0f;
            stemSettings.sinkAmount = 0f;
            stemSettings.stemBaseHeight = 3f;
            stemSettings.stemWavePatternAmount = 0.1f;
            stemSettings.baseRadiusFactor = 1.0f;
            stemSettings.textureRepeats = 3;
            stemSettings.baseColor = mossColour;
            builder.BuildStem(stemSettings);

            for (int i = 0; i < builder.mesh.meshVertices.Count; i++)
            {
                float t = -builder.mesh.meshVertices[i].position.Z/ radiusBase;
                t = t + MyMath.rngMinusPlus(0.3f);
                t = MyMath.clamp01(t);
                t = MathF.Max(0f, t);
                t = MathF.Pow(t, 2.0f);
                Colour col = new Colour(MyMath.lerp(new Vector3(1f), mossColour.ToVector3(), t));
                builder.mesh.meshVertices[i].colour = col;
            }

            TreeBuilder BranchBuilder = new TreeBuilder(new VertexMaterial(TextureGenerator.pineBark, new Colour(255, 255, 255)));


            TreeBuilder.StemBuildSettings branchSettings = new TreeBuilder.StemBuildSettings();
            branchSettings.detailsHeight = 4;
            branchSettings.detailPerRing = 6;
            branchSettings.height = 7f;
            branchSettings.radiusBase = 0.45f;
            branchSettings.radiusTop = 0.05f;
            branchSettings.baseRadiusFactor = 0.2f;
            branchSettings.sinkAmount = 0.5f;
            branchSettings.stemBendRadius = 0.4f;
            branchSettings.textureRepeats = 1;
            branchSettings.baseColor = mossColour;
            BranchBuilder.BuildStem(branchSettings);

            Mesh leafMesh = MeshGenerator.generatePlane(new Vector2(3f, 3f), new Vector2i(2, 4), new VertexMaterial(TextureGenerator.deadTwig, new Colour(255, 255, 255)), centerY: false);
            for (int i = 0; i < leafMesh.meshVertices.Count; i++)
            {
                leafMesh.meshVertices[i].position.Y -= MathF.Abs(MathF.Pow(leafMesh.meshVertices[i].position.X, 2.0f)) * 0.1f;
                leafMesh.meshVertices[i].position.Y += MathF.Abs(MathF.Pow(leafMesh.meshVertices[i].position.Z, 2.0f)) * 0.05f;
            }
            //leafMesh += leafMesh.rotated(new Vector3(0f, 0f, MathF.PI / 2f));
            TreeBuilder.SpreadAroundStemSettings twigSpawnSettings = new TreeBuilder.SpreadAroundStemSettings();
            twigSpawnSettings.BranchRandomRotation.Z = .0f;
            twigSpawnSettings.BranchStartRotation.X = 0;
            twigSpawnSettings.BranchEndRotation.X = 0;
            twigSpawnSettings.BranchStartScale = new Vector3(1f);
            twigSpawnSettings.BranchEndScale = new Vector3(0.5f);
            twigSpawnSettings.numberBranches = 4;
            twigSpawnSettings.startStemRatio = 0.05f;
            BranchBuilder.SpreadMeshAroundStem(leafMesh.rotated(new Vector3(-MathF.PI * 0.25f, 0f, 0f)), twigSpawnSettings);


            TreeBuilder.SpreadAroundStemSettings branchSpawnSettings = new TreeBuilder.SpreadAroundStemSettings();
            branchSpawnSettings.BranchRandomRotation.Z = 1.0f;
            branchSpawnSettings.BranchStartRotation.X = 0.95f;
            branchSpawnSettings.BranchEndRotation.X = 0.45f;
            branchSpawnSettings.numberBranches = 3;
            branchSpawnSettings.startStemRatio = 0.45f;
            branchSpawnSettings.endStemRatio = 0.95f;
            branchSpawnSettings.BranchEndScale = new Vector3(0.13f);

            builder.SpreadMeshAroundStem(BranchBuilder.mesh.rotated(new Vector3(MathF.PI / 2f, 0f, 0f)), branchSpawnSettings);


            Mesh deadTwigMesh = MeshGenerator.generatePlane(new Vector2(2f, 2f), new Vector2i(2, 4), new VertexMaterial(TextureGenerator.deadTwig, new Colour(255, 255, 255)), centerY: false);
            for (int i = 0; i < deadTwigMesh.meshVertices.Count; i++)
            {
                deadTwigMesh.meshVertices[i].position.Y -= MathF.Abs(MathF.Pow(deadTwigMesh.meshVertices[i].position.X, 2.0f)) * 0.8f;
                deadTwigMesh.meshVertices[i].position.Y += MathF.Abs(MathF.Pow(deadTwigMesh.meshVertices[i].position.Z, 2.0f)) * 0.4f;
            }
            deadTwigMesh += deadTwigMesh.rotated(new Vector3(0f, 0f, MathF.PI / 2f));

            TreeBuilder.SpreadAroundStemSettings deadTwigSpawnSettings = new TreeBuilder.SpreadAroundStemSettings();
            deadTwigSpawnSettings.BranchRandomRotation.Z = 0.0f;
            deadTwigSpawnSettings.BranchStartRotation.X = 0.0f;
            deadTwigSpawnSettings.BranchEndRotation.X = 0.0f;
            deadTwigSpawnSettings.numberBranches = 5;
            deadTwigSpawnSettings.randomSpin = true;
            deadTwigSpawnSettings.startStemRatio = 0.2f;
            deadTwigSpawnSettings.endStemRatio = 0.95f;
            deadTwigSpawnSettings.branchRandomScale = 0.5f;
            deadTwigSpawnSettings.BranchEndScale = new Vector3(0.2f);

            builder.SpreadMeshAroundStem(deadTwigMesh.rotated(new Vector3(MathF.PI * 0.25f, 0f, 0f)), deadTwigSpawnSettings);

            builder.mesh.rotate(new Vector3(MathF.PI/2f, 0f, 0f));

            return builder.mesh;
        }


        public static Mesh GenerateBirchTree()
        {
            TreeBuilder builder = new TreeBuilder(new VertexMaterial(TextureGenerator.barkBirch, new Colour(255, 255, 255)));
            TreeBuilder.StemBuildSettings stemSettings = new TreeBuilder.StemBuildSettings();
            stemSettings.radiusBase = 0.5f;
            stemSettings.radiusTop = 0.03f;
            stemSettings.stemBendRadius = 0.9f;
            stemSettings.height = 30.0f;
            stemSettings.sinkAmount = 3f;
            stemSettings.stemBaseHeight = 2f;
            stemSettings.stemWavePatternAmount = 0.0f;
            stemSettings.baseRadiusFactor = 0.8f;
            stemSettings.baseColor = new Colour(115, 115,115);
            builder.BuildStem(stemSettings);

            TreeBuilder BranchBuilder = new TreeBuilder(new VertexMaterial(TextureGenerator.barkBirch, new Colour(255, 255, 255)));


            TreeBuilder.StemBuildSettings branchSettings = new TreeBuilder.StemBuildSettings();
            branchSettings.detailsHeight = 4;
            branchSettings.detailPerRing = 6;
            branchSettings.height = 7f;
            branchSettings.radiusBase = 0.25f;
            branchSettings.radiusTop = 0.05f;
            branchSettings.baseRadiusFactor = 0.2f;
            branchSettings.sinkAmount = 0.5f;
            branchSettings.stemBendRadius = 0.4f;
            branchSettings.textureRepeats = 1;
            BranchBuilder.BuildStem(branchSettings);

            Mesh leafMesh = MeshGenerator.generatePlane(new Vector2(6f, 6f), new Vector2i(2, 4), new VertexMaterial(TextureGenerator.oakBranch, new Colour(255, 255, 255)), centerY: false);
            //leafMesh += leafMesh.rotated(new Vector3(0f, 0f, MathF.PI / 2f));
            TreeBuilder.SpreadAroundStemSettings twigSpawnSettings = new TreeBuilder.SpreadAroundStemSettings();
            twigSpawnSettings.BranchRandomRotation.Z = .0f;
            twigSpawnSettings.BranchStartRotation.X = MathF.PI/1.2f;
            twigSpawnSettings.BranchEndRotation.X = MathF.PI / 1.2f;
            twigSpawnSettings.BranchStartScale = new Vector3(1f);
            twigSpawnSettings.BranchEndScale = new Vector3(0.8f);
            twigSpawnSettings.numberBranches = 10;
            twigSpawnSettings.startStemRatio = 0.05f;
            BranchBuilder.SpreadMeshAroundStem(leafMesh.rotated(new Vector3(MathF.PI / 2f, 0f, 0f)), twigSpawnSettings);


            TreeBuilder.SpreadAroundStemSettings branchSpawnSettings = new TreeBuilder.SpreadAroundStemSettings();
            branchSpawnSettings.BranchRandomRotation.Z = 1.0f;
            branchSpawnSettings.BranchStartRotation.X = -0.95f;
            branchSpawnSettings.BranchEndRotation.X = -0.45f;
            branchSpawnSettings.numberBranches = 40;
            branchSpawnSettings.startStemRatio = 0.25f;
            branchSpawnSettings.BranchEndScale = new Vector3(0.2f);

            builder.SpreadMeshAroundStem(BranchBuilder.mesh.rotated(new Vector3(MathF.PI/2f, 0f, 0f)), branchSpawnSettings);





            return builder.mesh;
        }

    }
}

using Dino_Engine.Modelling.Model;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.Modelling.Procedural.Vegetation
{
    internal class TreeBuilder
    {
        public Mesh mesh;
        public VertexMaterial trunkMaterial;
        public VertexMaterial branchMaterial;
        public Curve3D? curve3D = null;

        public TreeBuilder(VertexMaterial trunkMaterial, VertexMaterial branchMaterial)
        {
            this.trunkMaterial = trunkMaterial;
            this.branchMaterial = branchMaterial;
            mesh = new Mesh();
        }

        private CardinalSpline3D GenerateSpline(float bendRadius, float fromY, float toY)
        {
            var controlPoints = new List<Vector3>();
            float totalLength = toY - fromY;
            int n = 10;
            float[] sinFBM = FBMmisc.sinFBM(4, 0.6f, n);
            float[] sinFBM2 = FBMmisc.sinFBM(4, 0.9f, n);
            for (int i = 0; i < n; i++)
            {
                float traversedRatio = i / (float)(n - 1);
                float x = sinFBM[i] * bendRadius * traversedRatio;
                float z = sinFBM2[i] * bendRadius * traversedRatio * 0.05f;
                float y = fromY + traversedRatio * totalLength;
                controlPoints.Add(new Vector3(x, y, z));
            }
            return new CardinalSpline3D(controlPoints, 0.0f);
        }

        public Curve3D BuildCurve(float bendRadius, float length, float sink, int detail)
        {
            Curve3D curve = GenerateSpline(bendRadius, -sink, length).GenerateCurve(detail);

            return curve;
        }
        public class SpreadAroundStemSettings()
        {
            public int numberBranches = 40 ;
            public float startStemRatio = 0.2f;
            public float endStemRatio = 1.0f;
            public Vector3 BranchStartRotation = new Vector3(0f, 0f, 0f);
            public Vector3 BranchEndRotation = new Vector3(0f, 0f, 0f);
            public Vector3 BranchRandomRotation = new Vector3(0.0f, 0.0f, 0.0f);
            public float BranchStartSpin = 0f;
            public float BranchEndSpin = 0f;
            public float branchRandomScale = 0f;
            public Vector3 BranchStartScale = new Vector3(1f);
            public Vector3 BranchEndScale = new Vector3(0.1f);
            public bool randomSpin = true;
        }
        public Mesh SpreadMeshAroundStem(Mesh branch, SpreadAroundStemSettings settings)
        {
            for (int i = 0; i < settings.numberBranches; i++)
            {
                float t = (float)i / (settings.numberBranches - 1);
                t = settings.startStemRatio + (settings.endStemRatio - settings.startStemRatio) * t;
                CurvePoint curvePoint = curve3D.getPointAt(t);
                Vector3 scale = MyMath.lerp(settings.BranchStartScale, settings.BranchEndScale, t);
                scale = scale + scale * new Vector3(settings.branchRandomScale * MyMath.rng());
                Mesh newBranch = branch.scaled(scale);
                newBranch.rotate(MyMath.lerp(settings.BranchStartRotation, settings.BranchEndRotation, t)+ settings.BranchRandomRotation*MyMath.rng3DMinusPlus(MathF.PI));
                //newBranch.translate(new Vector3(0f, -curvePoint.width / 2f, 0f));
                newBranch.translate(new Vector3(0f, 0f, curvePoint.width));
                newBranch.rotate(new Vector3(0f, MyMath.lerp(settings.BranchStartSpin, settings.BranchEndSpin, t), 0f));
                if (settings.randomSpin) newBranch.rotate(new Vector3(0f, MyMath.rng() * MathF.Tau, 0f));
                newBranch.rotate(curvePoint.rotation);
                newBranch.translate(curvePoint.pos);

                mesh += newBranch;
            }

            return mesh;
        }

        public class StemBuildSettings()
        {
            public float radiusBase = 1f;
            public float radiusTop = 0.1f;
            public float height = 20f;
            public float stemBendRadius = 1f;
            public float stemWavePatternAmount = 0.1f;
            public float BaseWavePatternAmount = 0.4f;
            public float baseRadiusFactor = 1f;
            public int detailPerRing = 32;
            public int detailsHeight = 4;
            public float sinkAmount = 0f;
            public float stemBaseHeight = 3f;
            public int wavePatternFrequenzy = 8;
            public int textureRepeats = 2;
            public Colour? baseColor = null;
        }

        public Mesh BuildStem(StemBuildSettings settings)
        {
            if (curve3D == null) curve3D = BuildCurve(settings.stemBendRadius, settings.height, settings.sinkAmount, settings.detailsHeight);
            curve3D.LERPWidth(settings.radiusBase, settings.radiusTop);
            Mesh poleMesh = MeshGenerator.generateCurvedTube(curve3D, settings.detailPerRing, trunkMaterial, textureRepeats: settings.textureRepeats, flatStart: true, sealTop: settings.radiusBase*0.1f);
            foreach (MeshVertex meshVertex in poleMesh.meshVertices)
            {
                float angle = (MathF.Atan2(meshVertex.position.X, meshVertex.position.Z) + MathF.PI) * settings.wavePatternFrequenzy;
                //meshVertex.position += new Vector3(MathF.Sin(angle), 0f, MathF.Cos(angle)) * .5f;

                if (meshVertex.position.Y < settings.stemBaseHeight)
                {
                    float baseFactor = MathF.Max(meshVertex.position.Y, 0f) / settings.stemBaseHeight;
                    float baseAmount = 1f-MathF.Pow(baseFactor, 0.5f);
                    meshVertex.position.Xz *= 1f + MathF.Sin(angle) * MyMath.lerp(settings.stemWavePatternAmount, settings.BaseWavePatternAmount, baseAmount);
                    

                    if (settings.baseColor != null) meshVertex.colour = new Colour(MyMath.lerp(settings.baseColor.Value.ToVector3(), meshVertex.colour.ToVector3(), MathF.Pow(baseFactor, 3.0f)));


                    meshVertex.position.Xz *= 1f+settings.baseRadiusFactor * baseAmount;
                }
                else
                {
                    meshVertex.position.Xz *= 1f + MathF.Sin(angle) * settings.stemWavePatternAmount;
                }
            }
            //poleMesh.translate(new Vector3(0f, -settings.sinkAmount, 0f));
            //poleMesh.FlatRandomness(new Vector3(.05f, 0f, .05f));

            this.mesh = poleMesh;

            return poleMesh;
        }
    }
}

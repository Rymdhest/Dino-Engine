using Dino_Engine.Core;
using Dino_Engine.Debug;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.Modelling.Procedural.Vegetation;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using OpenTK.Compute.OpenCL;
using OpenTK.Mathematics;
using static OpenTK.Graphics.OpenGL.GL;

namespace Dino_Engine.Modelling.Procedural.Indoor
{
    public class SkeletonGenerator
    {

        public static Mesh GenerateSkeleton()
        {
            float armLength = 1.7f;
            float legLength = 2f;

            Mesh skeleton = GenerateUpperBody(out Vector3 leftArmAchnor, out Vector3 rightArmAchnor, out Vector3 leftLegAchnor, out Vector3 RightLegAchnor, out Vector3 headAnchor);

            Mesh leg = generateLeg(legLength);
            skeleton += leg.translated(RightLegAchnor);
            skeleton += leg.scaled(new Vector3(-1f, 1f, 1f)).translated(leftLegAchnor);

   
            Mesh arm = GenerateArm(armLength);
            Vector3 rightArmRotation = new Vector3(-1f, 1.65f, -0.5f);
            Vector3 leftArmRotation = new Vector3(-.2f, .3f, 0.5f);
            skeleton += arm.rotated(rightArmRotation).translated(rightArmAchnor);
            skeleton += arm.scaled(new Vector3(-1f, 1f, 1f)).rotated(leftArmRotation).translated(leftArmAchnor);


            Mesh head = GenerateHead();
            skeleton += head.translated(headAnchor);

            Mesh sword = WeaponGenerator.GenerateSword();
            sword.translate(new Vector3(0f, - 0.25f, 0f));
            sword.rotate(new Vector3(0f, 0f, MathF.PI/2f));
            sword.translate(new Vector3(0f, -armLength-0.15f, 0f));
            sword.rotate(rightArmRotation);
            sword.translate(rightArmAchnor);
            skeleton += sword;


            Mesh shield = ArmourGenerator.GenerateShield();
            shield.translate(new Vector3(0f, -0.25f, 0f));
            //shield.rotate(new Vector3(0f, 0f, MathF.PI / 2f));
            shield.translate(new Vector3(0f, -armLength - 0.15f, 0f));
            shield.rotate(leftArmRotation);
            shield.translate(leftArmAchnor);
            skeleton += shield;



            skeleton.translate(new Vector3(0f, legLength, 0f));




            return skeleton;
        }

        public static Mesh GenerateUpperBody(out Vector3 leftArmAchnor, out Vector3 rightArmAchnor, out Vector3 leftLegAchnor, out Vector3 RightLegAchnor, out Vector3 headAnchor)
        {
            VertexMaterial boneMaterial = new VertexMaterial(TextureGenerator.bone);
            Mesh body = new Mesh();
            Mesh hip = IcoSphereGenerator.CreateIcosphere(3, boneMaterial);
            for (int i = 0; i < hip.meshVertices.Count; i++)
            {
                hip.meshVertices[i].position.X -= MathF.Pow((MathF.Abs(hip.meshVertices[i].position.Y) + MathF.Abs(hip.meshVertices[i].position.Z)), 2.0f) * 1.3f;
                hip.meshVertices[i].position.X -= MathF.Pow(MyMath.clamp01(-hip.meshVertices[i].position.Z), 2.0f) * 1.0f;
            }
            hip.scale(new Vector3(0.02f, .19f, .40f));

            hip.translate(new Vector3(0f, 0f, -0.18f));
            hip.rotate(new Vector3(0.7f, 0.5f, 0.3f));

            body += hip.translated(new Vector3(0.35f, 0, 0f));
            body += hip.translated(new Vector3(0.35f, 0, 0f)).scaled(new Vector3(-1f, 1f, 1f));

            TreeBuilder spineBuilder = new TreeBuilder(boneMaterial);
            TreeBuilder.StemBuildSettings spineSettings = new TreeBuilder.StemBuildSettings();
            spineSettings.baseRadiusFactor = 0.0f;
            spineSettings.radiusBase = 0.065f;
            spineSettings.radiusTop = 0.065f;
            spineSettings.BaseWavePatternAmount = 0.0f;
            spineSettings.stemWavePatternAmount = 0.0f;
            spineSettings.sinkAmount = 0.0f;
            spineSettings.detailsHeight = 2;
            spineSettings.detailPerRing = 5;
            List<Vector3> curvePoints = new List<Vector3> {
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, .3f, 0.15f),
                new Vector3(0f, 0.35f, 0.16f),
                new Vector3(0f, 0.45f, 0.15f),
                new Vector3(0f, 0.7f, 0.03f),
                new Vector3(0f, 0.78f, 0.02f),
                new Vector3(0f, 0.85f, 0.01f),
                new Vector3(0f, 1.13f, 0.01f),
                new Vector3(0f, 1.55f, 0.1f),
                new Vector3(0f, 1.75f, .15f),
                new Vector3(0f, 1.85f, .2f)
            };
            Curve3D spineCurve = new Curve3D(curvePoints);
            spineBuilder.BuildStem(spineSettings, spineCurve);

            Mesh spinePart = IcoSphereGenerator.CreateIcosphere(1, boneMaterial);
            spinePart.translate(new Vector3(0f, 0f, -0.7f));
            spinePart.scale(new Vector3(0.03f, 0.043f, 0.162f));
            TreeBuilder.SpreadAroundStemSettings spinePartSettings = new TreeBuilder.SpreadAroundStemSettings();
            spinePartSettings.BranchStartScale = new Vector3(1f);
            spinePartSettings.BranchEndScale = new Vector3(1f);
            spinePartSettings.randomSpin = false;
            spinePartSettings.numberBranches = 10;
            spineBuilder.SpreadMeshAroundStem(spinePart, spinePartSettings);

            Mesh rib = GenerateRibPart();
            rib.translate(new Vector3(0f, 0f, -0.16f));

            spinePartSettings.numberBranches = 9;
            spinePartSettings.startStemRatio = 0.4f;
            spinePartSettings.endStemRatio = 0.9f;
            spinePartSettings.BranchEndScale = new Vector3(0.45f);
            spinePartSettings.BranchStartScale = new Vector3(1.3f);
            spinePartSettings.BranchStartRotation = new Vector3(-0.5f, 0f, 0f);

            spineBuilder.SpreadMeshAroundStem(rib, spinePartSettings);


            Mesh backSpine = spineBuilder.mesh;

            body += backSpine.translated(new Vector3(0f, 0f, -0.23f));

            leftLegAchnor = new Vector3(0.25f, 0f, 0f);
            RightLegAchnor = new Vector3(-0.25f, 0f, 0f);
            headAnchor = new Vector3(0f, 1.85f, .2f-0.23f);
            leftArmAchnor = new Vector3(0.45f, 1.85f, 0f);
            rightArmAchnor = new Vector3(-0.45f, 1.85f, 0f);

            Vector3 frontChestAnchor = new Vector3(0.0f, 1.75f, .20f);
            Mesh collarBone = GenerateBone();
            collarBone.scale(new Vector3((leftArmAchnor-frontChestAnchor).Length));
            //collarBone.rotate(new Vector3(0f, 0f, MathF.PI/2f));
            collarBone.rotate(MyMath.FromToRotation(Vector3.UnitY, frontChestAnchor-leftArmAchnor));
            body += collarBone.translated(leftArmAchnor);
            body += collarBone.scaled(new Vector3(-1f, 1f, 1f)).translated(rightArmAchnor);

            return body;
        }

        public static Mesh GenerateRibPart()
        {
            VertexMaterial boneMaterial = new VertexMaterial(TextureGenerator.bone);
            TreeBuilder spineBuilder = new TreeBuilder(boneMaterial);
            TreeBuilder.StemBuildSettings spineSettings = new TreeBuilder.StemBuildSettings();
            spineSettings.baseRadiusFactor = 0.0f;
            spineSettings.radiusBase = 0.025f;
            spineSettings.radiusTop = 0.025f;
            spineSettings.BaseWavePatternAmount = 0.0f;
            spineSettings.stemWavePatternAmount = 0.0f;
            spineSettings.sinkAmount = 0.0f;
            spineSettings.detailsHeight = 1;
            spineSettings.detailPerRing = 5;

            List<Vector3> curvePoints = new List<Vector3>();

            int detailsLength = 10;
            float r = 0.3f;
            for (int i = 0; i<detailsLength; i++)
            {
                float t = i / (detailsLength - 1f);

                float x = MathF.Sin(t*MathF.PI*1.3f-0.8f)*r;
                float z = MathF.Cos(t * MathF.PI*1.3f-0.8f) * r*0.9f;
                curvePoints.Add(new Vector3(x, 0f, z));
            }

            Curve3D spineCurve = new Curve3D(curvePoints);
            spineBuilder.BuildStem(spineSettings, spineCurve);

            Mesh spinePart = spineBuilder.mesh;
            spinePart.translate(new Vector3(0f, 0f, r));
            spinePart.rotate(new Vector3(0f, 0.5f, 0f));
            spinePart.scale(new Vector3(1f, 1.5f, 1f));
            spinePart += spinePart.scaled(new Vector3(-1f, 1f, 1f));
            return spinePart;
        }

        public static Mesh GenerateHead()
        {

            Vector3 scale = new Vector3(0.25f, 0.4f, 0.3f);

            VertexMaterial boneMaterial = new VertexMaterial(TextureGenerator.bone);
            Mesh head = IcoSphereGenerator.CreateIcosphere(4, boneMaterial);
            head.FlatRandomness(0.005f);

            //Eyes
            deformFeature(head, new Vector3(0.4f, 0.2f, 1f).Normalized(), new Vector2(0.3f), 0.5f, mirrorX:true);

            //Nose
            deformFeature(head, new Vector3(0.0f, 0.0f, 1f).Normalized(), new Vector2(0.2f, 0.3f), 0.1f, carveInward: false);

            //Mouth
            deformFeature(head, new Vector3(0.0f, -.35f, 1f).Normalized(), new Vector2(1.2f, 0.15f), 0.5f);
            Mesh tooth = MeshGenerator.generateBox(boneMaterial);
            Vector3 toothScale = new Vector3(0.095f, 0.12f, 0.04f);  
            tooth.scale(toothScale);
            int numTeeth = 16;
            for (int i = 0; i<numTeeth; i++)
            {
                float t = i / (numTeeth-1.0f)*2.0f-1.0f;
                float angle = t * MathF.Tau * 0.15f;
                float x = MathF.Sin(angle) *0.9f;
                float z = MathF.Cos(angle) *0.9f;
                head += tooth.rotated(new Vector3(0f, angle + MyMath.rngMinusPlus(MathF.Tau * 0.04f), 0f)).translated(new Vector3(x, -0.2f, z));
                head += tooth.rotated(new Vector3(0f, angle + MyMath.rngMinusPlus(MathF.Tau * 0.04f), 0f)).translated(new Vector3(x, -0.2f-toothScale.Y*1.03f, z));
            }

            //Ears
            deformFeature(head, new Vector3(1.0f, 0f, 0f).Normalized(), new Vector2(0.7f, 0.7f), 0.1f, mirrorX: true);
            deformFeature(head, new Vector3(1.0f, 0.15f, 0f).Normalized(), new Vector2(0.4f, 0.3f), 0.1f, mirrorX: true);

            //Forehead
            deformFeature(head, new Vector3(0f, 1f, 0f).Normalized(), new Vector2(0.7f, 0.7f), 0.1f);

            head.translate(new Vector3(0f, 1f, 0f));
            head.scale(scale);

            List<Vector2> rings = new List<Vector2> {
                new Vector2(1f, 0f),
                new Vector2(1f, 0.04f),
                new Vector2(0.5f, 0.08f),
                new Vector2(0.4f, 0.5f),
                new Vector2(0.2f, 0.54f)
            };

            Mesh hat = MeshGenerator.generateCylinder(rings, 8, new VertexMaterial(TextureGenerator.leather, new Colour(0.02f, 0.02f, 0.02f)), sealTop:0.1f);

            head += hat.scaled(new Vector3(0.5f)).translated(new Vector3(0f, scale.Y*2.0f-scale.Y*0.4f, 0f));

            return head;
        }

        private static Mesh deformFeature(
            Mesh mesh,
            Vector3 featureCenter,
            Vector2 featureRadius,
            float depth,
            bool carveInward = true,
            bool mirrorX = false)
        {
            // Direction from origin to feature center (assuming sphere is centered at origin)
            Vector3 featureNormal = featureCenter.Normalized();

            // Create a local coordinate system (Tangent/Bitangent) on the sphere surface
            Vector3 up = MathF.Abs(featureNormal.Y) > 0.99f ? Vector3.UnitX : Vector3.UnitY;
            Vector3 tangent = Vector3.Cross(up, featureNormal).Normalized();
            Vector3 bitangent = Vector3.Cross(featureNormal, tangent).Normalized();

            for (int i = 0; i < mesh.meshVertices.Count; i++)
            {
                Vector3 pos = mesh.meshVertices[i].position;
                Vector3 delta = pos - featureCenter;

                // 1. Only affect vertices facing the front of the feature
                float frontFacing = Vector3.Dot(delta, featureNormal);
                if (frontFacing < -featureRadius.X) continue; // Behind feature plane

                // 2. Project offset onto local surface 2D axes (Tangent & Bitangent)
                float xDist = Vector3.Dot(delta, tangent);
                float yDist = Vector3.Dot(delta, bitangent);

                // 3. Normalize distance relative to feature dimensions (elliptical bounds)
                Vector2 normalizedOffset = new Vector2(xDist / featureRadius.X, yDist / featureRadius.Y);
                float dist = normalizedOffset.Length;

                if (dist >= 1.0f) continue; // Outside brush area

                // 4. Smooth cosine/cosine-squared falloff for organic socket shape
                float falloff = MyMath.clamp01(1.0f - dist);
                // Smoothstep (3x^2 - 2x^3) makes the socket edges transition smoothly into the skull
                float smoothFalloff = falloff * falloff * (3.0f - 2.0f * falloff);

                // 5. Apply displacement along feature normal (inward for socket, outward for nose/chin)
                float displacement = smoothFalloff * depth;
                if (carveInward) displacement = -displacement;

                mesh.meshVertices[i].position += featureNormal * displacement;
            }
            if (mirrorX)
            {
                deformFeature(mesh, new Vector3(-1.0f*featureCenter.X, featureCenter.Y, featureCenter.Z), featureRadius, depth, carveInward, mirrorX:false);
            }
            return mesh;
        }

        public static Mesh generateLeg(float length)
        {
            Mesh mesh = new Mesh();

            Mesh leg = new Mesh();

            Mesh legLowerPart = GenerateBone(length: length/2f, radius: 0.05f);
            Mesh legLower = new Mesh();
            legLower += legLowerPart.translated(new Vector3(0.05f, 0f, 0f));
            legLower += legLowerPart.translated(new Vector3(-0.05f, 0f, 0f));
            leg += legLower;

            Mesh legUpper = GenerateBone(length: length/2f).translated(new Vector3(0f, length/2f, 0f));
            leg += legUpper;

            Mesh foot = new Mesh();
            Mesh toe = GenerateBone(length: 0.7f, radius: 0.03f).scaled(new Vector3(0.7f)).rotated(new Vector3(MathF.PI / 2f, 0f, 0f));
            for (int i = -2; i <= 2; i++)
            {
                foot += toe.translated(new Vector3(0.05f * i, 0f, 0f)).scaled(new Vector3(1f, 2.5f, 1f + (2 + i) * 0.05f));
            }
            leg += foot.translated(new Vector3(0f, 0.05f, -0.05f));

            leg.translate(new Vector3(0f, -length, 0f));

            return leg;
        }

        public static Mesh GenerateArm(float length)
        {
            Mesh arm = new Mesh();
            Mesh armLowerPart = GenerateBone(length: length/2f, radius: 0.035f);
            Mesh armLower = new Mesh();
            armLower += armLowerPart.translated(new Vector3(0.05f, 0f, 0f));
            armLower += armLowerPart.translated(new Vector3(-0.05f, 0f, 0f));
            arm += armLower;

            Mesh armUpper = GenerateBone(length: length/2f, radius:0.05f).translated(new Vector3(0f, length/2f, 0f));
            arm += armUpper;

            Mesh hand = new Mesh();
            Mesh finger = GenerateBone(length: 0.65f, radius: 0.023f).scaled(new Vector3(0.5f)).rotated(new Vector3(MathF.PI, 0f, 0f));
            for (int i = -2; i <= 2; i++)
            {
                hand += finger.translated(new Vector3(0.05f * i, 0f, 0f)).scaled(new Vector3(1f, 1f + (2 + i) * 0.05f, 2.5f));
            }
            arm += hand.translated(new Vector3(0f, 0.0f, 0.0f));

            arm.translate(new Vector3(0f, -length, 0f));

            return arm;
        }

        public static Mesh GenerateBone(float length = 1.0f, float radius = 0.1f)
        {
            float r = radius;
            float h = length;
            float knobHeight = 0.14f;
            List<Vector2> layers = new List<Vector2> {
                new Vector2(r*0.1f, knobHeight*0.0f),
                new Vector2(r, knobHeight*0.3f),
                new Vector2(r*1.20f, knobHeight*0.4f),
                new Vector2(r*1.25f, knobHeight*0.7f),
                new Vector2(r, knobHeight*1.0f),
                new Vector2(r*0.8f, h*0.3f),
                new Vector2(r*0.7f, h*0.5f),
                new Vector2(r*0.8f, h*0.7f),
                new Vector2(r, h-knobHeight),
                new Vector2(r*1.25f, h-knobHeight*0.7f),
                new Vector2(r*1.20f, h-knobHeight*0.4f),
                new Vector2(r, h-knobHeight*0.3f),
                new Vector2(r*0.1f, h-knobHeight*0.0f)
            };
            Mesh mesh = MeshGenerator.generateCylinder(layers, 8, new VertexMaterial(TextureGenerator.bone), sealTop:0.001f, sealBot:-0.001f);

            return mesh;
        }
    }
}

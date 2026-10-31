using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Procedural.Indoor;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    public enum BoneType
    {
        Chest = 0,
        Head,
        UpperArmL, LowerArmL, HandL,
        UpperArmR, LowerArmR, HandR,
        UpperLegL, LowerLegL, FootL,
        UpperLegR, LowerLegR, FootR,
        Count
    }

    public struct SkeletonComponent : IComponent
    {
        public int[] ParentIndices;

        public Vector3[] LocalRestPositions;
        public Quaternion[] LocalRestRotations;

        public Vector3[] LocalPositions;
        public Quaternion[] LocalRotations;

        public Matrix4[] ModelTransforms;
        public Matrix4[] BindPoseInverse;
        public Matrix4[] SkinningMatrices;

        public SkeletonComponent()
        {
            int boneCount = (int)BoneType.Count;

            ParentIndices = new int[boneCount];
            LocalRestPositions = new Vector3[boneCount];
            LocalRestRotations = new Quaternion[boneCount];

            LocalPositions = new Vector3[boneCount];
            LocalRotations = new Quaternion[boneCount];

            ModelTransforms = new Matrix4[boneCount];
            BindPoseInverse = new Matrix4[boneCount];
            SkinningMatrices = new Matrix4[boneCount];

            InitializeBindPose();
        }

        public void InitializeBindPose()
        {
            Matrix4[] invBind = SkeletonGenerator.GetInverseBindMatrices();

            ParentIndices[(int)BoneType.Chest] = -1;
            ParentIndices[(int)BoneType.Head] = (int)BoneType.Chest;

            ParentIndices[(int)BoneType.UpperArmR] = (int)BoneType.Chest;
            ParentIndices[(int)BoneType.LowerArmR] = (int)BoneType.UpperArmR;
            ParentIndices[(int)BoneType.HandR] = (int)BoneType.LowerArmR;

            ParentIndices[(int)BoneType.UpperArmL] = (int)BoneType.Chest;
            ParentIndices[(int)BoneType.LowerArmL] = (int)BoneType.UpperArmL;
            ParentIndices[(int)BoneType.HandL] = (int)BoneType.LowerArmL;

            ParentIndices[(int)BoneType.UpperLegR] = (int)BoneType.Chest;
            ParentIndices[(int)BoneType.LowerLegR] = (int)BoneType.UpperLegR;
            ParentIndices[(int)BoneType.FootR] = (int)BoneType.LowerLegR;

            ParentIndices[(int)BoneType.UpperLegL] = (int)BoneType.Chest;
            ParentIndices[(int)BoneType.LowerLegL] = (int)BoneType.UpperLegL;
            ParentIndices[(int)BoneType.FootL] = (int)BoneType.LowerLegL;

            for (int i = 0; i < (int)BoneType.Count; i++)
            {
                BindPoseInverse[i] = invBind[i];

                Matrix4 childGlobalRest = Matrix4.Invert(invBind[i]);
                int parentIdx = ParentIndices[i];

                Matrix4 childLocalRest;
                if (parentIdx == -1)
                {
                    childLocalRest = childGlobalRest;
                }
                else
                {
                    Matrix4 parentGlobalRest = Matrix4.Invert(invBind[parentIdx]);
                    childLocalRest = childGlobalRest * Matrix4.Invert(parentGlobalRest);
                }

                LocalRestPositions[i] = childLocalRest.ExtractTranslation();
                LocalRestRotations[i] = childLocalRest.ExtractRotation();

                LocalPositions[i] = LocalRestPositions[i];
                LocalRotations[i] = LocalRestRotations[i];
            }
        }
    }
}
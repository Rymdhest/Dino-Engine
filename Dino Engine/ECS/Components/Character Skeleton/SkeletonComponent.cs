using Dino_Engine.Animation;
using Dino_Engine.ECS.ECS_Architecture;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    /// <summary>
    /// Every bone there is, whoever it's on. Meshes hold these by number, and what isn't on anything in particular is on 0, so a new bone goes on the end and nothing has to move. An animal uses the
    /// same ones, with its arms as forelegs.
    /// </summary>
    public enum BoneType
    {
        Chest = 0,
        Head,
        UpperArmL, LowerArmL, HandL,
        UpperArmR, LowerArmR, HandR,
        UpperLegL, LowerLegL, FootL,
        UpperLegR, LowerLegR, FootR,
        Pelvis,
        Spine,
        Neck,
        ClavicleL, ClavicleR,
        TailBase, TailTip,
        Count
    }

    /// <summary>What's drawn: how a body is built, and where its bones are this frame, as <see cref="CharacterAnimator"/> left them.</summary>
    public struct SkeletonComponent : IComponent
    {
        public Rig Rig;
        public Matrix4[] SkinningMatrices;

        public SkeletonComponent(Rig rig, Matrix4[] skinningMatrices)
        {
            Rig = rig;
            SkinningMatrices = skinningMatrices;
        }
    }
}

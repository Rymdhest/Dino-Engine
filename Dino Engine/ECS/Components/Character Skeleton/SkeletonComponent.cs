using Dino_Engine.Animation;
using Dino_Engine.ECS.ECS_Architecture;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    /// <summary>
    /// Every bone there is, whoever it's on. Meshes hold these by number, and what isn't on anything in particular is on 0, so a new bone goes on the end and nothing has to move. An animal uses the
    /// same ones, with its arms as forelegs. A dinosaur uses them all: a jaw, a neck in two, a tail in five and toes of their own, and a long-necked giant a neck in five. On anyone who has none of
    /// those they sit where the bone they'd continue does, with nothing on them, so a person is posed exactly as before. The shaders take 32 (u_Bones[32]).
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
        Jaw,
        NeckTop,
        Tail1, Tail2, Tail3,
        ToeL, ToeR,
        Neck1, Neck2, Neck3,    // Between Neck and NeckTop, on a neck long enough to curve: a sauropod's.
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

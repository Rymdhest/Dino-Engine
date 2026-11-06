using Dino_Engine.ECS.Components;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>What of a body something covers, which is what's left out of the body under it, so that skin can't come through.</summary>
    [Flags]
    public enum BodyRegions
    {
        None = 0,
        Torso = 1 << 0,
        UpperArms = 1 << 1,
        LowerArms = 1 << 2,
        Hands = 1 << 3,
        UpperLegs = 1 << 4,
        LowerLegs = 1 << 5,
        Feet = 1 << 6,
        Hair = 1 << 7,
    }

    /// <summary>
    /// The surface of a body, as numbers. All armour starts from this and goes outwards, so that a piece fits without anything being placed by eye: everything is a height and a distance
    /// from a limb, and nothing else knows where a shoulder is. It's in the space of the rig, with the ground at y = 0 and a person facing +z, and has to agree with <see cref="Character.HumanoidGenerator"/>.
    /// </summary>
    public static class BodyForm
    {
        public const float Ground = 0f;
        public const float Hip = 2f;            // Where legs meet the body.
        public const float Knee = 1f;
        public const float Shoulder = 3.85f;
        public const float Neck = 4.06f;
        public const float HeadCentre = 4.46f;
        public const float HeadTop = 4.86f;

        public const float HipX = 0.25f;
        public const float ShoulderX = 0.45f;
        public const float ArmLength = 1.7f;
        public const float ElbowX = ShoulderX + ArmLength * 0.5f;
        public const float WristX = ShoulderX + ArmLength;

        /// <summary>A body is flatter front to back than across.</summary>
        public const float Depth = 0.68f;

        public const float HeadHalfWidth = 0.33f;
        public const float HeadHalfDepth = 0.36f;
        public const float HeadHalfHeight = 0.4f;

        private static readonly Profile Waist = new Profile((1.45f, 0.76f), (1.9f, 0.56f), (3.05f, 0.6f), (3.82f, 0.68f), (4.06f, 0.22f));

        /// <summary>How far the side of the body is from its middle at this height.</summary>
        public static float Torso(float y) => Waist.At(y);

        /// <summary>The radius of a leg, at a height.</summary>
        public static float Leg(float y) => y > Knee ? 0.2f + 0.06f * (y - Knee) / (Hip - Knee) : 0.14f + 0.06f * y / Knee;

        /// <summary>The radius of an arm, out along it from the shoulder at 0 to the wrist at 1.</summary>
        public static float Arm(float t) => t < 0.5f ? 0.21f - 0.04f * t * 2f : 0.17f - 0.05f * (t - 0.5f) * 2f;

        public static BoneType UpperArm(float side) => side > 0f ? BoneType.UpperArmL : BoneType.UpperArmR;
        public static BoneType LowerArm(float side) => side > 0f ? BoneType.LowerArmL : BoneType.LowerArmR;
        public static BoneType Hand(float side) => side > 0f ? BoneType.HandL : BoneType.HandR;
        public static BoneType UpperLeg(float side) => side > 0f ? BoneType.UpperLegL : BoneType.UpperLegR;
        public static BoneType LowerLeg(float side) => side > 0f ? BoneType.LowerLegL : BoneType.LowerLegR;
        public static BoneType Foot(float side) => side > 0f ? BoneType.FootL : BoneType.FootR;

        /// <summary>
        /// What anything hanging below the hips is skinned to. The run swings a leg nearly level, and a robe bound to the chest has knees coming through it, so the further down the more it
        /// belongs to the legs, each side to its own: a skirt then twists as cloth does. Only some of the way, or a hem flies up with the leg. There are two weights to a vertex, so it goes
        /// from the chest to the nearer leg, and then from the nearer leg to both.
        /// </summary>
        public static (Vector2 bones, Vector2 weights) Skirt(Vector3 at)
        {
            float below = Math.Clamp((Hip + 0.15f - at.Y) / 1.6f, 0f, 1f);
            float side = Math.Clamp(at.X / 0.7f * 0.5f + 0.5f, 0f, 1f);
            var left = (float)BoneType.UpperLegL;
            var right = (float)BoneType.UpperLegR;

            const float MostOnLegs = 0.62f;
            float onLegs = below * MostOnLegs;
            float nearer = side > 0.5f ? left : right;
            float share = MathF.Max(side, 1f - side);
            if (share > 0.82f || below < 0.5f) return (new Vector2((int)BoneType.Chest, nearer), new Vector2(1f - onLegs, onLegs));

            // Down the middle there's no nearer leg, and it stays between the two.
            return (new Vector2(left, right), new Vector2(side, 1f - side));
        }
    }
}

using Dino_Engine.Modelling.Procedural.Wildlife;
using OpenTK.Mathematics;

namespace Dino_Engine.Wildlife
{
    /// <summary>How a bird is holding itself this frame.</summary>
    public struct BirdPose
    {
        public float Stroke;    // Radians through a wingbeat: down through 0 and up through pi.
        public float Beat;      // How hard: 0 holding its wings still, 1 flat out.
        public float Fold;      // 0 wings out, 1 folded along its sides.
        public float Reach;     // Radians its wings are held forward of square, as they are when it brakes to land.
        public float Hold;      // Radians its wings are held above level between beats: how a gull's sit, or a vulture's.
        public float Pitch;     // Radians, nose down.
        public float Bank;      // Radians its left wing is up.
        public float Crouch;    // 0 standing, 1 down on its legs.
        public float Peck;      // 0 head up, 1 at the ground.
        public float HeadYaw;   // Radians towards its left.
        public float TailPitch; // Radians the tail is pressed down.
        public float Legs;      // 0 tucked away, 1 down.

        private const float FoldBack = 1.52f;       // Wings lie along the body.
        private const float FoldTwist = -1.32f;     // With their feathers hanging down its flank and not sticking into it.
        private const float FoldLift = 0.06f;
        private const float HandFold = 2.95f;       // And it's a Z: the hand comes back along the arm, or a wing's as long folded as it is open, and a finch has three tails.

        /// <summary>
        /// A wingbeat has a shape. The downstroke is the power: the wing comes down straight and spread. The upstroke is the recovery: the wrist bends and the hand trails, so the wing doesn't push back up what
        /// it's just pushed down, and the hand always follows the arm a little late. Without that it's a hinge opening and shutting, which is what a toy bird does.
        /// </summary>
        public readonly void Solve(Skeleton skeleton, in Matrix4 root, Span<Matrix4> skin)
        {
            Span<Quaternion> turns = stackalloc Quaternion[BirdBody.Count];
            float open = 1f - Fold;

            float arm = Hold * open + (0.1f + MathF.Cos(Stroke) * 0.82f) * Beat * open;
            float hand = (MathF.Cos(Stroke - 0.85f) * 0.62f) * Beat * open;
            float recovery = MathF.Max(MathF.Sin(Stroke), 0f);
            float flex = recovery * 0.75f * Beat * open;

            float sweep = Reach * open + (MathF.Sin(Stroke) * 0.2f) * Beat * open;

            for (int side = 0; side < 2; side++)
            {
                float mirror = side == 0 ? 1f : -1f;
                int armBone = side == 0 ? BirdBody.ArmL : BirdBody.ArmR;
                int handBone = side == 0 ? BirdBody.HandL : BirdBody.HandR;

                Quaternion spread = Turn.Yaw(-sweep * mirror) * Turn.Roll(arm * mirror);
                Quaternion folded = Turn.Yaw(FoldBack * mirror) * Turn.Roll(FoldLift * mirror) * Turn.Pitch(FoldTwist);
                turns[armBone] = Quaternion.Slerp(spread, folded, Fold);
                turns[handBone] = Quaternion.Slerp(Turn.Yaw(flex * mirror) * Turn.Roll(hand * mirror), Turn.Yaw(HandFold * mirror), Fold);
            }

            turns[BirdBody.Trunk] = Turn.Pitch(Pitch) * Turn.Roll(Bank);

            // A bird's head stays level whatever the body does, which is why it can see where it's going.
            float reach = Peck * 1.5f;
            turns[BirdBody.Neck] = Turn.Pitch(reach * 0.55f - Pitch * 0.55f);
            turns[BirdBody.Head] = Turn.Yaw(HeadYaw) * Turn.Pitch(reach * 0.65f - Pitch * 0.4f);
            turns[BirdBody.Tail] = Turn.Pitch(-TailPitch);

            Quaternion leg = Turn.Pitch(-Pitch * Legs + (1f - Legs) * 1.35f - Crouch * 0.35f);
            turns[BirdBody.LegL] = leg;
            turns[BirdBody.LegR] = leg;

            skeleton.Solve(turns, root, skin);
        }
    }
}

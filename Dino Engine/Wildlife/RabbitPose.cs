using Dino_Engine.Modelling.Procedural.Wildlife;
using OpenTK.Mathematics;

namespace Dino_Engine.Wildlife
{
    /// <summary>How a rabbit is holding itself this frame. Everything is a few numbers around 0 and 1, which is what lets one way of standing ease into another with nothing to get stuck in.</summary>
    public struct RabbitPose
    {
        public float Pitch;     // Radians, nose down, about the hips.
        public float Rear;      // 0 on all fours, 1 sat up looking about.
        public float Crouch;    // 0 at ease, 1 pressed flat to the ground.
        public float Arch;      // The back: below 0 gathered up under itself, above 0 stretched out.
        public float Hind;      // Radians the thighs are pushed back.
        public float Fore;      // Radians the forelegs reach forward.
        public float HeadPitch; // Radians, nose down, from level.
        public float HeadYaw;   // Radians towards its left.
        public float EarsBack;  // 0 pricked, 1 laid along the back.
        public float EarLeft;   // Radians each is flicked, on top of that.
        public float EarRight;
        public float Tail;      // 0 down, 1 flashing.

        private const float RearPitch = 1.2f;
        private const float EarLaid = 1.5f;
        private const float EarSplay = 0.2f;
        private const float TailRaise = 1.1f;

        public readonly void Solve(Skeleton skeleton, in Matrix4 root, Span<Matrix4> skin)
        {
            Span<Quaternion> turns = stackalloc Quaternion[RabbitBody.Count];
            float body = Pitch - Rear * RearPitch;
            float chest = -Arch * 0.32f + Rear * 0.28f + Crouch * 0.1f;
            float shoulders = body + chest;

            turns[RabbitBody.Pelvis] = Turn.Pitch(body);
            turns[RabbitBody.Chest] = Turn.Pitch(chest);
            turns[RabbitBody.Head] = Turn.Yaw(HeadYaw) * Turn.Pitch(HeadPitch - shoulders * 0.9f);
            turns[RabbitBody.EarL] = Turn.Pitch(-EarsBack * EarLaid + EarLeft) * Turn.Roll(-EarSplay * (1f - EarsBack));
            turns[RabbitBody.EarR] = Turn.Pitch(-EarsBack * EarLaid + EarRight) * Turn.Roll(EarSplay * (1f - EarsBack));
            turns[RabbitBody.Scut] = Turn.Pitch(-Tail * TailRaise);

            // A rabbit sat up lets its paws hang against its chest, which leaning back has already brought them forward of.
            float fore = Fore - Rear * 0.55f;
            turns[RabbitBody.ForeL] = Turn.Pitch(-fore - shoulders * (1f - Rear));
            turns[RabbitBody.ForeR] = Turn.Pitch(-fore - shoulders * (1f - Rear));

            // Thighs swing from the hips, and the long hind foot stays flat on the ground until it's pushed off from.
            float thigh = Hind - body * (1f - Rear) + Crouch * -0.28f;
            float foot = -(body + thigh) + MathF.Max(Hind, 0f) * 0.9f;
            turns[RabbitBody.ThighL] = Turn.Pitch(thigh);
            turns[RabbitBody.ThighR] = Turn.Pitch(thigh);
            turns[RabbitBody.FootL] = Turn.Pitch(foot);
            turns[RabbitBody.FootR] = Turn.Pitch(foot);

            skeleton.Solve(turns, root, skin);
        }
    }

    /// <summary>
    /// A rabbit's bound, as a function of how far through it is. It's the half-bound rabbits and hares really use, not a mirrored hop: the hind legs drive together, it flies stretched out, the forelegs land first and
    /// one after the other, and the hind legs swing through past them, which is what bunches the whole animal up between bounds. That's the shape a rabbit is known by even from far away.
    /// </summary>
    public static class RabbitGait
    {
        /// <summary>When it leaves the ground and comes back to it.</summary>
        public const float Leaves = 0.15f, Lands = 0.66f;

        private static readonly Keys PitchKeys = new Keys(0f, 0.08f, 0.15f, -0.4f, 0.42f, -0.04f, 0.62f, 0.34f, 0.8f, 0.2f, 1f, 0.08f);
        private static readonly Keys ArchKeys = new Keys(0f, -0.7f, 0.15f, 0.85f, 0.45f, 0.6f, 0.66f, 0.1f, 0.86f, -0.9f, 1f, -0.7f);
        private static readonly Keys HindKeys = new Keys(0f, -0.5f, 0.15f, 1.05f, 0.45f, 0.95f, 0.72f, 0.2f, 0.9f, -0.62f, 1f, -0.5f);
        private static readonly Keys ForeKeys = new Keys(0f, -0.25f, 0.2f, 0.1f, 0.45f, 1.05f, 0.62f, 0.5f, 0.8f, -0.35f, 1f, -0.25f);

        /// <summary>Shares of the same pose for a bound of this strength, 0 standing still and 1 flat out.</summary>
        public static void Apply(ref RabbitPose pose, float through, float strength)
        {
            pose.Pitch += PitchKeys.At(through) * strength;
            pose.Arch += ArchKeys.At(through) * strength;
            pose.Hind += HindKeys.At(through) * strength;
            pose.Fore += ForeKeys.At(through) * strength;
        }

        /// <summary>How high it is off the ground, as a share of the top of the bound: a true arc while it's in the air, with nothing else lifting it.</summary>
        public static float Height(float through)
        {
            if (through <= Leaves || through >= Lands) return 0f;
            float s = (through - Leaves) / (Lands - Leaves);
            return 4f * s * (1f - s);
        }

        /// <summary>How much of the way along it's got by now. It travels in the air, and hardly at all with its feet down.</summary>
        public static float Along(float through)
        {
            float t = Math.Clamp((through - Leaves * 0.6f) / (Lands + 0.12f - Leaves * 0.6f), 0f, 1f);
            return t * t * (3f - 2f * t) * 0.3f + t * 0.7f;
        }
    }
}

using Dino_Engine.Modelling.Procedural.Wildlife;
using OpenTK.Mathematics;

namespace Dino_Engine.Wildlife
{
    /// <summary>How a butterfly is holding itself: its wings' angle above flat, and how it's pitched.</summary>
    public struct ButterflyPose
    {
        public float Wings;     // Radians above flat: about 1.45 is shut over its back.
        public float Pitch;     // Radians, nose down.

        public readonly void Solve(Skeleton skeleton, in Matrix4 root, Span<Matrix4> skin)
        {
            Span<Quaternion> turns = stackalloc Quaternion[ButterflyBody.Count];
            turns[ButterflyBody.Trunk] = Turn.Pitch(Pitch);
            turns[ButterflyBody.WingL] = Turn.Roll(Wings);
            turns[ButterflyBody.WingR] = Turn.Roll(-Wings);
            skeleton.Solve(turns, root, skin);
        }
    }

    /// <summary>How a dragonfly is holding itself. Fore and hind wings beat out of step, which is why they can hover.</summary>
    public struct DragonflyPose
    {
        public float Stroke;    // Radians through a beat.
        public float Pitch;
        public float Bank;

        public readonly void Solve(Skeleton skeleton, in Matrix4 root, Span<Matrix4> skin)
        {
            Span<Quaternion> turns = stackalloc Quaternion[DragonflyBody.Count];
            float front = MathF.Sin(Stroke) * 0.34f;
            float back = MathF.Sin(Stroke + 2.2f) * 0.34f;
            turns[DragonflyBody.Trunk] = Turn.Pitch(Pitch) * Turn.Roll(Bank);
            turns[DragonflyBody.FrontL] = Turn.Roll(front);
            turns[DragonflyBody.FrontR] = Turn.Roll(-front);
            turns[DragonflyBody.BackL] = Turn.Roll(back);
            turns[DragonflyBody.BackR] = Turn.Roll(-back);
            skeleton.Solve(turns, root, skin);
        }
    }

    /// <summary>How a frog is holding itself.</summary>
    public struct FrogPose
    {
        public float Leap;      // 0 folded up, 1 stretched out behind it.
        public float Pitch;     // Radians, nose down.

        public readonly void Solve(Skeleton skeleton, in Matrix4 root, Span<Matrix4> skin)
        {
            Span<Quaternion> turns = stackalloc Quaternion[FrogBody.Count];
            turns[FrogBody.Trunk] = Turn.Pitch(Pitch - 0.22f * (1f - Leap));

            // The thigh swings back from along its side, and the shin opens out from under it.
            turns[FrogBody.ThighL] = Turn.Yaw(Leap * 2.2f);
            turns[FrogBody.ThighR] = Turn.Yaw(-Leap * 2.2f);
            turns[FrogBody.ShinL] = Turn.Yaw(-Leap * 2.5f);
            turns[FrogBody.ShinR] = Turn.Yaw(Leap * 2.5f);
            turns[FrogBody.ArmL] = Turn.Pitch(Leap * 1.3f);
            turns[FrogBody.ArmR] = Turn.Pitch(Leap * 1.3f);
            skeleton.Solve(turns, root, skin);
        }
    }

    /// <summary>How a fish is holding itself: a bend down its length, which runs from head to tail.</summary>
    public struct FishPose
    {
        public float Bend;      // Radians at the tail.
        public float Lag;       // And at the middle.
        public float Pitch;     // Radians, nose down.

        public readonly void Solve(Skeleton skeleton, in Matrix4 root, Span<Matrix4> skin)
        {
            Span<Quaternion> turns = stackalloc Quaternion[FishBody.Count];
            turns[FishBody.Front] = Turn.Pitch(Pitch);
            turns[FishBody.Middle] = Turn.Yaw(Lag);
            turns[FishBody.Tail] = Turn.Yaw(Bend);
            skeleton.Solve(turns, root, skin);
        }
    }
}

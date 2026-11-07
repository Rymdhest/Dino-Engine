using Dino_Engine.Modelling.Model;
using Dino_Engine.Wildlife;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Wildlife
{
    /// <summary>What a crab looks like.</summary>
    public sealed class CrabLook
    {
        public Vector3 Shell = new Vector3(0.36f, 0.075f, 0.03f);
        public Vector3 Under = new Vector3(0.55f, 0.42f, 0.28f);
        public Vector3 Leg = new Vector3(0.3f, 0.09f, 0.04f);
        public Vector3 Claw = new Vector3(0.42f, 0.1f, 0.04f);
        public Vector3 ClawTip = new Vector3(0.05f, 0.03f, 0.03f);
    }

    /// <summary>
    /// A crab: a shell on eight legs of two bones and two claws of two. It's +Z forward like everything else, which is the way it doesn't go. A crab is only worth having for how it moves, and how it moves is
    /// legs, so they're thick enough to see from up here and stand well out from the shell. Its left claw is the big one, as a male's is.
    /// </summary>
    public static class CrabBody
    {
        public const int Shell = 0, ArmL = 1, ClawL = 2, ArmR = 3, ClawR = 4, FirstLeg = 5, Legs = 8, Count = FirstLeg + Legs * 2;

        /// <summary>How far the shell's middle stands off the ground at rest.</summary>
        public const float Standing = 0.21f;

        private static readonly float[] LegZ = { 0.1f, 0.035f, -0.03f, -0.09f };
        private static readonly float[] Fan = { 0.34f, 0.1f, -0.1f, -0.36f };

        /// <summary>Bones of a leg, counted 0 to 3 down its left side and 4 to 7 down its right.</summary>
        public static int Upper(int leg) => FirstLeg + leg * 2;
        public static int Lower(int leg) => FirstLeg + leg * 2 + 1;

        private static float Side(int leg) => leg < Legs / 2 ? 1f : -1f;

        public static Vector3 Hip(int leg)
        {
            float side = Side(leg);
            return new Vector3(0.14f * side, Standing, LegZ[leg % 4]);
        }

        public static Vector3 RestKnee(int leg)
        {
            float side = Side(leg);
            float fan = Fan[leg % 4];
            Vector3 outward = new Vector3(MathF.Cos(fan) * side, 0f, MathF.Sin(fan));
            return Hip(leg) + outward * 0.13f + new Vector3(0f, 0.1f, 0f);
        }

        /// <summary>Where a leg's tip is when it's standing easy.</summary>
        public static Vector3 RestFoot(int leg)
        {
            float side = Side(leg);
            float fan = Fan[leg % 4];
            Vector3 outward = new Vector3(MathF.Cos(fan) * side, 0f, MathF.Sin(fan));
            return new Vector3(Hip(leg).X, 0f, Hip(leg).Z) + outward * 0.3f;
        }

        public static float UpperReach(int leg) => (RestKnee(leg) - Hip(leg)).Length;
        public static float LowerReach(int leg) => (RestFoot(leg) - RestKnee(leg)).Length;

        public static Skeleton NewSkeleton()
        {
            var pivots = new Vector3[Count];
            var parents = new int[Count];
            pivots[Shell] = new Vector3(0f, Standing, 0f);
            parents[Shell] = -1;
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? 1f : -1f;
                int arm = side == 0 ? ArmL : ArmR;
                int claw = side == 0 ? ClawL : ClawR;
                pivots[arm] = new Vector3(0.15f * x, Standing, 0.13f);
                parents[arm] = Shell;
                pivots[claw] = new Vector3(0.28f * x, Standing - 0.015f, 0.22f);
                parents[claw] = arm;
            }
            for (int leg = 0; leg < Legs; leg++)
            {
                pivots[Upper(leg)] = Hip(leg);
                parents[Upper(leg)] = Shell;
                pivots[Lower(leg)] = RestKnee(leg);
                parents[Lower(leg)] = Upper(leg);
            }
            return new Skeleton(pivots, parents);
        }

        public static Mesh Build(CrabLook look)
        {
            var body = new BodyMesh();

            // Wider than it's long, with its front edge the broader, and domed. Lofted from side to side, so the rings run across it.
            Span<Ring> shell = stackalloc Ring[]
            {
                new Ring(new Vector3(0f, Standing, -0.135f), 0.12f, 0.02f, Shell),
                new Ring(new Vector3(0f, Standing + 0.004f, -0.1f), 0.19f, 0.06f, Shell),
                new Ring(new Vector3(0f, Standing + 0.01f, -0.03f), 0.23f, 0.09f, Shell),
                new Ring(new Vector3(0f, Standing + 0.01f, 0.05f), 0.235f, 0.085f, Shell),
                new Ring(new Vector3(0f, Standing + 0.002f, 0.12f), 0.2f, 0.05f, Shell),
                new Ring(new Vector3(0f, Standing - 0.004f, 0.15f), 0.13f, 0.016f, Shell),
            };
            Vector3 Carapace(float along, float up) => Vector3.Lerp(look.Under, look.Shell, Smooth(-0.3f, 0.05f, up));
            body.Loft(shell, 9, Carapace);

            for (int side = -1; side <= 1; side += 2)
            {
                float x = side;
                bool big = side > 0;
                body.Limb(new Vector3(0.05f * x, Standing + 0.03f, 0.13f), new Vector3(0.062f * x, Standing + 0.09f, 0.155f), 0.012f, 0.01f, look.Leg, Shell, 4);
                body.Egg(new Vector3(0.062f * x, Standing + 0.1f, 0.158f), new Vector3(0.02f), new Vector3(0.015f, 0.012f, 0.012f), Shell, 4, 1);

                int arm = big ? ArmL : ArmR;
                int claw = big ? ClawL : ClawR;
                float size = big ? 1.35f : 0.9f;
                Vector3 elbow = new Vector3(0.28f * x, Standing - 0.015f, 0.22f);
                body.Limb(new Vector3(0.15f * x, Standing, 0.13f), elbow, 0.034f, 0.04f * size, look.Claw, arm, 5);

                // A palm and two fingers, closing towards the middle.
                Vector3 palm = elbow + new Vector3(-0.055f * x, 0.005f, 0.075f * size);
                body.Egg(palm, new Vector3(0.05f, 0.04f, 0.075f) * size, look.Claw, claw, 5, 2, -0.1f);
                Vector3 tip = palm + new Vector3(-0.08f * x, 0.015f, 0.1f) * size;
                body.Limb(palm + new Vector3(0.012f * x, 0.02f * size, 0.04f * size), tip, 0.03f * size, 0.008f, look.ClawTip, claw, 4);
                body.Limb(palm + new Vector3(0.03f * x, -0.012f * size, 0.03f * size), tip + new Vector3(0.05f * x, -0.024f, -0.024f) * size, 0.026f * size, 0.008f, look.ClawTip, claw, 4);
            }

            for (int leg = 0; leg < Legs; leg++)
            {
                Vector3 hip = Hip(leg);
                Vector3 knee = RestKnee(leg);
                Vector3 foot = RestFoot(leg);
                body.Limb(hip, knee, 0.042f, 0.034f, look.Leg, Upper(leg), 4);
                body.Limb(knee, foot, 0.034f, 0.009f, look.Leg * 0.86f, Lower(leg), 4);
            }

            return body.Build();
        }

        private static float Smooth(float from, float to, float value)
        {
            float t = Math.Clamp((value - from) / (to - from), 0f, 1f);
            return t * t * (3f - 2f * t);
        }
    }
}

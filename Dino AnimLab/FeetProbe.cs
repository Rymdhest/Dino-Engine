using Dino_Engine.Animation;
using Dino_Engine.ECS.Components;
using OpenTK.Mathematics;

namespace Dino_AnimLab
{
    /// <summary>
    /// Whether a leg really puts its foot where the gait planted it. A gait can't let a foot slide, but a leg that can't reach that far leaves the foot it draws short of it, hanging in the air and
    /// dragged along: that's a slide all the same, and the worst kind, as it comes and goes with every step.
    /// </summary>
    internal static class FeetProbe
    {
        private const float Step = 1f / 60f;
        public static bool Axes;
        public static int Landings;

        /// <summary>How far short of its planted foot a leg falls, worst of all, as a share of the leg, walking at a pace and turning as it goes.</summary>
        public static (float stance, float slide, float jerk) Measure(Body body, float pace, float turn, float seconds = 16f, float scale = 1f)
        {
            CharacterAnimator animator = body.NewAnimator(5);
            var ground = new FlatGround();
            float leg = body.Rig.LegLength * scale;
            float speed = pace * MathF.Sqrt(Rig.Gravity * leg);
            float worst = 0f, slide = 0f, jerk = 0f;
            var was = new Vector3[body.Rig.Legs.Length];
            var wasDown = new bool[body.Rig.Legs.Length];
            Vector3 position = Vector3.Zero;
            float yaw = 0f;
            Vector3 lastHips = Vector3.Zero, lastHipSpeed = Vector3.Zero;
            for (float time = 0f; time < seconds; time += Step)
            {
                yaw += turn * Step;
                position += Frames.Forward(yaw) * speed * Step;
                animator.Step(AnimationInput.At(position, yaw, scale), Step, ground);
                if (time < 3f) continue;

                Quaternion toWorld = Frames.Yaw(yaw);
                for (int i = 0; i < body.Rig.Legs.Length; i++)
                {
                    Limb limb = body.Rig.Legs[i];
                    bool down = !animator.Gait.Feet[i].Swinging;
                    Vector3 drawn = position + Vector3.Transform(animator.JointOf(limb.End), toWorld) * scale;
                    if (down && !wasDown[i]) Landings++;
                    if (down)
                    {
                        float miss = (animator.JointOf(limb.End) - animator.AnkleWanted(i)).Length / body.Rig.LegLength;
                        worst = MathF.Max(worst, miss);
                        if (wasDown[i]) slide = MathF.Max(slide, (drawn - was[i]).Xz.Length / Step);
                    }
                    was[i] = drawn;
                    wasDown[i] = down;
                }

                Vector3 hips = position + Vector3.Transform(animator.JointOf(BoneType.Pelvis), toWorld) * scale;
                Vector3 hipSpeed = (hips - lastHips) / Step;
                if (time > 3.1f)
                {
                    Vector3 change = Vector3.Transform((hipSpeed - lastHipSpeed) / Step / Rig.Gravity, Frames.Yaw(-yaw));
                    if (change.Length > jerk && Axes) Console.Write($"[{change.X:0.0} {change.Y:0.0} {change.Z:0.0} at {time:0.00}] ");
                    jerk = MathF.Max(jerk, change.Length);
                }
                lastHips = hips;
                lastHipSpeed = hipSpeed;
            }
            return (worst, slide, jerk);
        }

        public static int Run(Options options)
        {
            string[] names = options.Text("bodies", "giant,hornback,spikeback,tyrant,raptor,ravager,wolf,bear,bare").Split(',');
            float turn = options.Float("turn", 0f);
            Axes = options.Has("axes");
            foreach (string name in names)
            {
                Body body = Body.Named(name);
                Console.Write($"{name,-12}");
                foreach (float pace in new[] { 0.12f, 0.2f, 0.3f, 0.5f, 0.8f })
                {
                    Landings = 0;
                    (float stance, float slide, float jerk) = Measure(body, pace, turn, 16f, options.Float("scale", 1f));
                    float speed = pace * MathF.Sqrt(Rig.Gravity * body.Rig.LegLength * options.Float("scale", 1f));
                    Console.Write($"  {pace:0.00} ({speed:0.0} m/s): {stance * 100f,4:0.0}% {jerk,4:0.0} g {Landings / 13f / body.Rig.Legs.Length * 2f,4:0.00}/s");
                }
                Console.WriteLine();
            }
            return 0;
        }
    }
}

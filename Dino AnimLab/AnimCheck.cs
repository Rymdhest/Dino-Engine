using Dino_Engine.Animation;
using Dino_Engine.World;
using Dino_Engine.World.Generation;
using Dino_Engine.ECS.Components;
using OpenTK.Mathematics;
using System.Diagnostics;

namespace Dino_AnimLab
{
    /// <summary>
    /// Whether bodies hold up, whatever's thrown at them. A lab can't say a body moves well: a person has to look at it. It can say that nothing goes wrong, in every combination of body, speed, ground and
    /// frame rate, and with input as bad as a game can give it. Everything here is something that once went wrong, or that would be seen at once if it did.
    /// </summary>
    internal static class AnimCheck
    {
        private const float Step = 1f / 60f;

        private static int _passed;
        private static int _failed;

        private static void Check(string what, bool ok, string detail = "")
        {
            if (ok) _passed++;
            else _failed++;
            Console.WriteLine($"  {(ok ? "ok  " : "FAIL")}  {what}{(detail.Length > 0 ? $": {detail}" : "")}");
        }

        public static int Run(Options options)
        {
            Maths();
            Limbs();
            Feet();
            Whole();
            Hostile();
            Actions();
            Falling();
            FirstSeenFrozen();
            Reaching();
            Standing();

            Console.WriteLine();
            Console.WriteLine($"{_passed} passed, {_failed} failed");
            return _failed > 0 ? 1 : 0;
        }

        private static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);

        private static bool Sound(CharacterAnimator animator, out string problem)
        {
            for (int i = 0; i < animator.Skinning.Length; i++)
            {
                Matrix4 m = animator.Skinning[i];
                for (int r = 0; r < 4; r++)
                {
                    for (int c = 0; c < 4; c++)
                    {
                        if (float.IsFinite(m[r, c])) continue;
                        problem = $"{(BoneType)i} isn't a number";
                        return false;
                    }
                }

                // A bone's turn has to stay a turn, or a mesh shears.
                var row = new Vector3(m.Row0.X, m.Row0.Y, m.Row0.Z);
                if (MathF.Abs(row.Length - 1f) > 0.02f)
                {
                    problem = $"{(BoneType)i} is stretched, by {row.Length:0.000}";
                    return false;
                }
            }
            problem = "";
            return true;
        }

        // ---- The parts.

        private static void Maths()
        {
            Console.WriteLine("Springs");

            float value = 1f, velocity = 0f;
            for (int i = 0; i < 600; i++) Spring.Step(ref value, ref velocity, 0f, 0.1f, Step);
            Check("A spring gets there", MathF.Abs(value) < 1e-3f, $"{value:0.00000}");

            // The whole point of solving them exactly.
            float a = 1f, av = 0f, b = 1f, bv = 0f;
            for (int i = 0; i < 60; i++) Spring.Step(ref a, ref av, 0f, 0.1f, 1f / 60f);
            for (int i = 0; i < 6; i++) Spring.Step(ref b, ref bv, 0f, 0.1f, 1f / 6f);
            Check("A spring doesn't care about the frame rate", MathF.Abs(a - b) < 1e-3f, $"{a:0.0000} and {b:0.0000}");

            float huge = 1f, hv = 0f;
            Spring.Step(ref huge, ref hv, 0f, 0.001f, 10f);
            Check("A long frame can't blow one up", float.IsFinite(huge) && MathF.Abs(huge) <= 1f);

            Vector3 ring = Vector3.UnitX, rv = Vector3.Zero;
            float least = 1f;
            for (int i = 0; i < 300; i++)
            {
                Spring.Ring(ref ring, ref rv, 3f, 0.5f, Step);
                least = MathF.Min(least, ring.X);
            }
            Check("A ringing spring goes past and settles", least < 0f && ring.Length < 0.01f, $"least {least:0.00}, ending {ring.Length:0.0000}");

            var motion = new Continuity();
            motion.Follow(Vector3.Zero, 0.05f, 100f, Step);
            Vector3 after = motion.Follow(new Vector3(50f, 0f, 0f), 0.05f, 100f, Step);
            Check("A jump doesn't get through", after.X < 8f, $"{after.X:0.00} of 50");
            for (int i = 0; i < 120; i++) after = motion.Follow(new Vector3(50f, 0f, 0f), 0.05f, 100f, Step);
            Check("And it gets there all the same", MathF.Abs(after.X - 50f) < 0.01f, $"{after.X:0.000}");

            var told = new Continuity();
            told.Follow(Vector3.Zero, 0.05f, 100f, Step);
            Vector3 first = told.Follow(Vector3.UnitX, 0.05f, 100f, Step, transition: true);
            Check("A change of mind starts from where things were", first.X < 0.1f, $"{first.X:0.000}");

            for (int i = 0; i < 2000; i++)
            {
                var random = new Random(i);
                Vector3 x = new Vector3(random.NextSingle() - 0.5f, random.NextSingle() - 0.5f, random.NextSingle() - 0.5f).Normalized();
                Vector3 y = Frames.Across(new Vector3(random.NextSingle() - 0.5f, random.NextSingle() - 0.5f, random.NextSingle() - 0.5f), x, Vector3.UnitY);
                Quaternion q = Frames.Of(x, y);
                if ((Vector3.Transform(Vector3.UnitX, q) - x).Length > 1e-3f || (Vector3.Transform(Vector3.UnitY, q) - y).Length > 1e-3f)
                {
                    Check("A rotation made from two directions has those directions", false, $"failed on the {i}th");
                    return;
                }
            }
            Check("A rotation made from two directions has those directions", true, "2000 of them");
        }

        private static void Limbs()
        {
            Console.WriteLine("Limbs");

            Limb leg = Rig.Humanoid.Legs[0];
            Vector3 root = Rig.Humanoid.Pivots[(int)leg.Upper];
            var random = new Random(5);
            float worstUpper = 0f, worstLower = 0f;
            bool numbers = true;
            bool bent = true;
            for (int i = 0; i < 20000; i++)
            {
                float reach = random.NextSingle() * leg.Length * 3f;
                Vector3 target = root + new Vector3(random.NextSingle() - 0.5f, random.NextSingle() - 0.5f, random.NextSingle() - 0.5f).Normalized() * reach;
                Vector3 bend = new Vector3(random.NextSingle() - 0.5f, random.NextSingle() - 0.5f, random.NextSingle() - 0.5f);
                Reach.Solve(leg, root, target, bend, out Vector3 middle, out Vector3 end, out Quaternion upper, out Quaternion lower);
                numbers &= Finite(middle) && Finite(end) && float.IsFinite(upper.W) && float.IsFinite(lower.W);
                worstUpper = MathF.Max(worstUpper, MathF.Abs((middle - root).Length - leg.UpperLength));
                worstLower = MathF.Max(worstLower, MathF.Abs((end - middle).Length - leg.LowerLength));
                bent &= (end - root).Length <= leg.Length + 1e-3f;
            }
            Check("A limb is always a number, wherever it's sent", numbers);
            Check("A limb is always as long as it is", worstUpper < 1e-3f && worstLower < 1e-3f, $"{worstUpper:0.00000} and {worstLower:0.00000} out");
            Check("Nothing stretches to reach what's out of reach", bent);

            Reach.Solve(leg, root, root - Vector3.UnitY * 1.4f, Vector3.UnitZ, out Vector3 knee, out _, out _, out _);
            Check("A knee goes forward", knee.Z > 0.1f, $"{knee.Z:0.00}");

            Reach.Solve(leg, root, root - Vector3.UnitY * leg.Length * 0.998f, Vector3.UnitZ, out Vector3 nearly, out Vector3 reached, out _, out _);
            Reach.Solve(leg, root, root - Vector3.UnitY * leg.Length * 1.002f, Vector3.UnitZ, out Vector3 past, out _, out _, out _);
            Check("A knee doesn't snap at full stretch", (nearly - past).Length < 0.02f, $"{(nearly - past).Length:0.0000} m");
        }

        // ---- Feet, which can't slide.

        private static void Feet()
        {
            Console.WriteLine("Feet");

            foreach ((string name, float speed) in new[] { ("a walk", 3.6f), ("a jog", 6.8f), ("a run", 10f), ("a sprint", 16f) })
            {
                foreach (string bodyName in new[] { "bare", "brute", "wolf", "bear" })
                {
                    Body body = Body.Named(bodyName);
                    CharacterAnimator animator = body.NewAnimator();
                    var ground = new Slope();
                    var planted = new Vector3[animator.Gait.Feet.Length];
                    var wasDown = new bool[animator.Gait.Feet.Length];
                    float slid = 0f;
                    float sunk = 0f;
                    int steps = 0;
                    float time = 0f;
                    for (int i = 0; i < 600; i++)
                    {
                        time += Step;
                        float z = time * speed;
                        animator.Step(AnimationInput.At(new Vector3(0f, ground.HeightAt(0f, z), z), 0f), Step, ground);
                        for (int f = 0; f < planted.Length; f++)
                        {
                            Foot foot = animator.Gait.Feet[f];
                            bool down = !foot.Swinging;
                            if (down && wasDown[f]) slid = MathF.Max(slid, (foot.At - planted[f]).Length);
                            if (down && !wasDown[f]) steps++;
                            if (down)
                            {
                                planted[f] = foot.At;
                                sunk = MathF.Max(sunk, ground.HeightAt(foot.At.X, foot.At.Z) - foot.At.Y);
                            }
                            wasDown[f] = down;
                        }
                    }
                    Check($"{body.Name}, {name}: no foot on the ground moves", slid < 1e-5f && steps > 8, $"{steps} steps");
                    Check($"{body.Name}, {name}: no foot goes through the ground", sunk < 0.02f, $"{sunk:0.000} m");
                }
            }

            {
                CharacterAnimator animator = Body.Named("bare").NewAnimator();
                var flat = new FlatGround();
                float reach = 0f;
                for (int i = 0; i < 300; i++)
                {
                    animator.Step(AnimationInput.At(new Vector3(0f, 0f, MathF.Min(i, 60) * Step * 10f), 0f), Step, flat);
                    if (i < 200) continue;
                    foreach (Foot foot in animator.Gait.Feet) reach = MathF.Max(reach, foot.At.Xz.Length > 0f ? (foot.At.Xz - new Vector2(0f, 10f)).Length : 0f);
                }
                Check("Feet come back under someone who's stopped", reach < 0.7f, $"{reach:0.00} m from the middle");
            }

            {
                CharacterAnimator animator = Body.Named("bare").NewAnimator();
                var flat = new FlatGround();
                int steps = 0;
                float turnedBy = 0f;
                for (int i = 0; i < 300; i++)
                {
                    turnedBy = i < 60 ? 0f : MathF.PI * 0.75f;
                    animator.Step(AnimationInput.At(Vector3.Zero, turnedBy), Step, flat);
                    if (animator.Footfalls != 0) steps++;
                }
                float off = 0f;
                foreach (Foot foot in animator.Gait.Feet) off = MathF.Max(off, MathF.Abs(Spring.Shortest(foot.Yaw - turnedBy)));
                Check("Turning on the spot takes a step or two", steps >= 1 && off < 0.3f, $"{steps} steps, feet {off:0.00} rad from where they should be");
            }

            {
                CharacterAnimator animator = Body.Named("bare").NewAnimator();
                var flat = new FlatGround();
                for (int i = 0; i < 30; i++) animator.Step(AnimationInput.At(Vector3.Zero, 0f), Step, flat);
                animator.Step(AnimationInput.At(new Vector3(400f, 0f, 400f), 0f), Step, flat);
                float far = 0f;
                foreach (Foot foot in animator.Gait.Feet) far = MathF.Max(far, (foot.At.Xz - new Vector2(400f, 400f)).Length);
                Check("Being put somewhere else brings the feet too", far < 1f, $"{far:0.00} m");
            }
        }

        // ---- Everything together, on every body.

        private static void Whole()
        {
            Console.WriteLine("Bodies");

            foreach (Body body in Body.All)
            {
                var problems = new List<string>();
                foreach (string scriptName in new[] { "idle", "guard", "walk", "jog", "run", "sprint", "stopstart", "turn", "circle", "strafe", "back", "slope" })
                {
                    Script script = Script.Named(scriptName)!;
                    CharacterAnimator animator = body.NewAnimator();
                    float time = 0f;
                    for (int i = 0; i < 300; i++)
                    {
                        time += Step;
                        (Vector3 position, float yaw) = script.Path(time);
                        var input = AnimationInput.At(position, yaw);
                        input.Alert = script.Alert;
                        input.Dazed = script.Dazed;
                        animator.Step(input, Step, script.Ground);
                        if (Sound(animator, out string problem))
                        {
                            continue;
                        }
                        problems.Add($"{scriptName}: {problem}");
                        break;
                    }
                }
                Check($"{body.Name} is sound doing everything", problems.Count == 0, string.Join(", ", problems.Take(3)));
            }

            foreach (Body body in Body.All)
            {
                CharacterAnimator a = body.NewAnimator();
                CharacterAnimator b = body.NewAnimator();
                var ground = new Slope();
                for (int i = 0; i < 200; i++)
                {
                    var input = AnimationInput.At(new Vector3(0f, 0f, i * 0.1f), 0.01f * i);
                    a.Step(input, Step, ground);
                    b.Step(input, Step, ground);
                }
                bool same = true;
                for (int i = 0; i < a.Skinning.Length; i++) same &= a.Skinning[i] == b.Skinning[i];
                if (!same) Check($"{body.Name}: the same goes the same way", false);
            }
            Check("The same input gives the same body", true);
        }

        // ---- Whatever a game can do to one.

        private static void Hostile()
        {
            Console.WriteLine("Abuse");

            foreach (Body body in Body.All)
            {
                string problem = "";
                float worstBone = 0f;
                for (int seed = 0; seed < 12 && problem.Length == 0; seed++)
                {
                    var random = new Random(seed);
                    CharacterAnimator animator = body.NewAnimator(seed);
                    var ground = new Slope();
                    Vector3 position = Vector3.Zero;
                    float yaw = 0f;
                    Vector2 going = Vector2.Zero;
                    int serial = 0;
                    ActionInput action = default;
                    float actionFor = 0f;

                    for (int i = 0; i < 1500; i++)
                    {
                        // Frames from a blink to a stall, and every so often no time at all.
                        double roll = random.NextDouble();
                        float dt = roll < 0.03 ? 0f : roll < 0.06 ? 0.4f : roll < 0.1 ? 1f / 15f : roll < 0.2 ? 1f / 240f : 1f / 60f;

                        // A hero can go from a stand to anything in one tick, and be put anywhere at all.
                        if (random.NextDouble() < 0.05) going = new Vector2(random.NextSingle() - 0.5f, random.NextSingle() - 0.5f) * 2f * (random.NextDouble() < 0.2 ? 60f : 14f);
                        if (random.NextDouble() < 0.1) going = Vector2.Zero;
                        if (random.NextDouble() < 0.05) yaw += random.NextSingle() * 7f - 3.5f;
                        if (random.NextDouble() < 0.005) position += new Vector3(random.NextSingle() * 300f, 0f, random.NextSingle() * 300f);

                        position += new Vector3(going.X, 0f, going.Y) * dt;
                        position.Y = ground.HeightAt(position.X, position.Z);

                        var input = AnimationInput.At(position, yaw, 0.6f + (seed % 5) * 0.25f);
                        input.Alert = random.NextDouble() < 0.5 ? 1f : 0f;
                        input.Rate = random.NextDouble() < 0.02 ? 0f : random.NextDouble() < 0.1 ? 0.4f : 1f;
                        input.Dazed = random.NextDouble() < 0.05;
                        if (random.NextDouble() < 0.03) input.Dash = new Vector2(random.NextSingle() - 0.5f, random.NextSingle() - 0.5f).Normalized();

                        if (random.NextDouble() < 0.03)
                        {
                            Gesture[] all = { Gestures.Forehand, Gestures.Backhand, Gestures.Sweep, Gestures.Overhead, Gestures.Thrust, Gestures.Whirl, Gestures.Bash, Gestures.Shoot, Gestures.Throw, Gestures.Push, Gestures.Call, Gestures.Burst, Gestures.Gather, Gestures.Summon, Gestures.Lunge, Gestures.Drink, Gestures.Use, Gestures.Bite, Gestures.Maul };
                            float impact = 0.01f + random.NextSingle() * 1.4f;
                            action = new ActionInput { Gesture = all[random.Next(all.Length)], Elapsed = 0f, Impact = impact, Length = impact + random.NextSingle() * 1.2f, Aim = random.NextSingle() * 12f - 6f, Serial = ++serial };
                            actionFor = 0f;
                        }
                        actionFor += dt;
                        action.Elapsed = actionFor;
                        input.Action = action;

                        if (random.NextDouble() < 0.04)
                        {
                            Vector3 way = new Vector3(random.NextSingle() - 0.5f, 0f, random.NextSingle() - 0.5f);
                            animator.Struck(way, random.NextSingle() * 3f, random.NextSingle() * 0.1f, Vector3.One);
                        }
                        if (random.NextDouble() < 0.01) animator.Hold(body.Carry == Carry.Nothing ? Carry.Sword : Carry.Nothing, random.NextDouble() < 0.5, random.NextDouble() < 0.3 ? Carry.Dagger : null);

                        animator.Step(input, dt, ground);
                        if (!Sound(animator, out string found) || animator.Faults > 0)
                        {
                            problem = $"seed {seed}, frame {i}: {found}, {animator.Faults} faults";
                            break;
                        }

                        for (int bone = 0; bone < animator.Rig.Legs.Length; bone++)
                        {
                            Limb limb = animator.Rig.Legs[bone];
                            float length = (animator.JointOf(limb.Lower) - animator.JointOf(limb.Upper)).Length;
                            worstBone = MathF.Max(worstBone, MathF.Abs(length - limb.UpperLength));
                        }
                    }
                }
                Check($"{body.Name} survives everything at once", problem.Length == 0, problem.Length > 0 ? problem : "18000 frames of it");
                Check($"{body.Name} stays in one piece", worstBone < 0.01f, $"{worstBone:0.0000} m out");
            }
        }

        // ---- Actions.

        private static void Actions()
        {
            Console.WriteLine("Blows");

            // What's seen has to land when the rules say, however long it has to get there.
            foreach ((string name, Gesture gesture) in new[] { ("forehand", Gestures.Forehand), ("sweep", Gestures.Sweep), ("overhead", Gestures.Overhead) })
            {
                foreach (float impact in new[] { 0.06f, 0.15f, 0.45f, 1.1f })
                {
                    // An overhead blow is only ever a slow one.
                    if (gesture == Gestures.Overhead && impact < 0.4f) continue;
                    float length = impact + 0.45f;
                    CharacterAnimator animator = Body.Named("sword").NewAnimator();
                    var flat = new FlatGround();
                    for (int i = 0; i < 40; i++) animator.Step(AnimationInput.At(Vector3.Zero, 0f), Step, flat);

                    float time = 0f;
                    Vector3 last = animator.WeaponTip;
                    float fastest = 0f;
                    float fastestAt = 0f;
                    while (time < length)
                    {
                        time += Step;
                        var input = AnimationInput.At(Vector3.Zero, 0f);
                        input.Action = new ActionInput { Gesture = gesture, Elapsed = time, Impact = impact, Length = length, Serial = 1 };
                        animator.Step(input, Step, flat);
                        float speed = (animator.WeaponTip - last).Length / Step;
                        last = animator.WeaponTip;
                        if (speed <= fastest) continue;
                        fastest = speed;
                        fastestAt = time;
                    }

                    // The fastest moment is the blow, and it comes around contact: just before it, on anything but the very quickest, as the swing's already through by the moment it lands.
                    Check($"{name} in {impact:0.00} s is at its fastest on the blow", MathF.Abs(fastestAt - impact) < 0.075f, $"at {fastestAt:0.000} s, {fastest:0} m/s");
                }
            }

            // Held down, a blade goes to and fro: the second blow starts from where the first one left off.
            {
                CharacterAnimator animator = Body.Named("sword").NewAnimator();
                var flat = new FlatGround();
                for (int i = 0; i < 40; i++) animator.Step(AnimationInput.At(Vector3.Zero, 0f), Step, flat);

                float[] sides = new float[3];
                for (int blow = 0; blow < 3; blow++)
                {
                    float time = 0f;
                    while (time < 0.6f)
                    {
                        time += Step;
                        var input = AnimationInput.At(Vector3.Zero, 0f);
                        input.Action = new ActionInput { Gesture = Gestures.Forehand, Elapsed = time, Impact = 0.15f, Length = 0.6f, Serial = blow + 1 };
                        animator.Step(input, Step, flat);
                        if (time >= 0.26f && sides[blow] == 0f) sides[blow] = animator.WeaponTip.X;
                    }
                }
                Check("A run of blows swings to and fro", sides[0] > 0.5f && sides[1] < -0.5f && sides[2] > 0.5f, $"the blade ends up at {sides[0]:0.0}, {sides[1]:0.0} and {sides[2]:0.0}");

                for (int i = 0; i < 90; i++) animator.Step(AnimationInput.At(Vector3.Zero, 0f), Step, flat);
                var again = AnimationInput.At(Vector3.Zero, 0f);
                float side = 0f;
                float at = 0f;
                while (at < 0.26f)
                {
                    at += Step;
                    again.Action = new ActionInput { Gesture = Gestures.Forehand, Elapsed = at, Impact = 0.15f, Length = 0.6f, Serial = 9 };
                    animator.Step(again, Step, flat);
                    side = animator.WeaponTip.X;
                }
                Check("And a blow after a rest starts afresh", side > 0.5f, $"{side:0.0}");
            }

            {
                CharacterAnimator animator = Body.Named("sword").NewAnimator();
                var flat = new FlatGround();
                for (int i = 0; i < 40; i++) animator.Step(AnimationInput.At(Vector3.Zero, 0f), Step, flat);

                // Pressing at any moment has to show on that very frame, in the hand.
                Vector3 before = animator.WeaponTip;
                var input = AnimationInput.At(Vector3.Zero, 0f);
                float moved = 0f;
                for (int i = 1; i <= 3; i++)
                {
                    input.Action = new ActionInput { Gesture = Gestures.Forehand, Elapsed = i * Step, Impact = 0.15f, Length = 0.6f, Serial = 1 };
                    animator.Step(input, Step, flat);
                    moved = (animator.WeaponTip - before).Length;
                }
                Check("A blow is under way within three frames of the press", moved > 0.25f, $"{moved:0.00} m");
            }

            // A cancelled blow can leave no pop: the pose goes on from where it was.
            {
                CharacterAnimator animator = Body.Named("sword").NewAnimator();
                var flat = new FlatGround();
                for (int i = 0; i < 40; i++) animator.Step(AnimationInput.At(Vector3.Zero, 0f), Step, flat);
                var input = AnimationInput.At(Vector3.Zero, 0f);
                for (int i = 1; i <= 9; i++)
                {
                    input.Action = new ActionInput { Gesture = Gestures.Forehand, Elapsed = i * Step, Impact = 0.15f, Length = 0.6f, Serial = 1 };
                    animator.Step(input, Step, flat);
                }
                Vector3 last = animator.WeaponBase;
                input.Action = default;
                float jump = 0f;
                for (int i = 0; i < 10; i++)
                {
                    animator.Step(input, Step, flat);
                    jump = MathF.Max(jump, (animator.WeaponBase - last).Length);
                    last = animator.WeaponBase;
                }
                Check("Stopping in the middle of a blow doesn't jump", jump < 2f, $"at most {jump:0.00} m in a frame");
            }

            // Strike: a hit takes the shape of the blow.
            {
                CharacterAnimator animator = Body.Named("sword").NewAnimator();
                var flat = new FlatGround();
                for (int i = 0; i < 40; i++) animator.Step(AnimationInput.At(Vector3.Zero, 0f), Step, flat);
                Vector3 rest = animator.JointOf(BoneType.Pelvis);
                animator.Struck(Vector3.UnitZ, 1f, 0f, Vector3.One);
                float most = 0f;
                for (int i = 0; i < 60; i++)
                {
                    animator.Step(AnimationInput.At(Vector3.Zero, 0f), Step, flat);
                    most = MathF.Max(most, animator.JointOf(BoneType.Pelvis).Z - rest.Z);
                }
                float end = (animator.JointOf(BoneType.Pelvis) - rest).Length;
                Check("A blow moves a body the way it's going, and it comes back", most > 0.1f && end < 0.2f, $"{most:0.00} m and back to {end:0.00}");
            }
        }

        // ---- Falling.

        private static void Falling()
        {
            Console.WriteLine("Falling");

            foreach (Body body in Body.All)
            {
                foreach ((string name, Vector3 push) in new[] { ("dropped", Vector3.Zero), ("shoved", new Vector3(3f, 6f, 13f)), ("launched", new Vector3(0f, 17f, 30f)), ("thrown hard", new Vector3(-200f, 400f, 200f)) })
                {
                    CharacterAnimator animator = body.NewAnimator();
                    var ground = new Slope();
                    for (int i = 0; i < 60; i++) animator.Step(AnimationInput.At(new Vector3(0f, ground.HeightAt(0f, i * 0.1f), i * 0.1f), 0f), Step, ground);

                    var input = AnimationInput.At(new Vector3(0f, ground.HeightAt(0f, 6f), 6f), 0f);
                    animator.Die(push);
                    bool sound = true;
                    string problem = "";
                    float lowest = float.MaxValue;
                    int asleepBy = -1;
                    for (int i = 0; i < 1500; i++)
                    {
                        animator.Step(input, Step, ground);
                        if (sound && !Sound(animator, out problem)) sound = false;
                        if (asleepBy < 0 && animator.Resting) asleepBy = i;
                    }
                    Check($"{body.Name} {name}: comes through it and comes to rest", sound && asleepBy >= 0, sound ? (asleepBy >= 0 ? $"after {asleepBy * Step:0.0} s" : "still going after 25 s") : problem);
                }
            }

            // Long limbs of a fallen body stay what they are.
            {
                CharacterAnimator animator = Body.Named("bare").NewAnimator();
                var flat = new FlatGround();
                for (int i = 0; i < 30; i++) animator.Step(AnimationInput.At(Vector3.Zero, 0f), Step, flat);
                animator.Die(new Vector3(0f, 20f, 34f));
                float worst = 0f;
                float backwards = 0f;
                for (int i = 0; i < 400; i++)
                {
                    animator.Step(AnimationInput.At(Vector3.Zero, 0f), Step, flat);
                    foreach (Limb limb in animator.Rig.Legs)
                    {
                        float length = (animator.JointOf(limb.Lower) - animator.JointOf(limb.Upper)).Length;
                        worst = MathF.Max(worst, MathF.Abs(length - limb.UpperLength) / limb.UpperLength);
                    }
                }
                Check("A body holds together in the air", worst < 0.25f, $"{worst * 100f:0}% at worst");
            }

            {
                var flat = new FlatGround();
                float Distance(Vector3 push)
                {
                    CharacterAnimator animator = Body.Named("bare").NewAnimator();
                    for (int i = 0; i < 30; i++) animator.Step(AnimationInput.At(Vector3.Zero, 0f), Step, flat);
                    animator.Die(push);
                    for (int i = 0; i < 600; i++) animator.Step(AnimationInput.At(Vector3.Zero, 0f), Step, flat);
                    return animator.JointOf(BoneType.Pelvis).Xz.Length;
                }
                float dropped = Distance(new Vector3(0f, 1f, 2.5f));
                float shoved = Distance(new Vector3(0f, 7f, 13f));
                float launched = Distance(new Vector3(0f, 17f, 30f));
                Check("How far a body goes is how hard it was hit", dropped < shoved && shoved < launched && launched > 18f, $"{dropped:0} m, {shoved:0} m and {launched:0} m");
            }

            // What's standing is in the way. The hardest of these goes further in a step than the wall is thick. It stands a little in front of whatever the body reaches to, which for anything but a
            // long-necked giant is 7 m off: a giant's head is further than that in front of its hips, and would start the fall through the wall.
            foreach (Body body in Body.All)
            {
                float front = body.Rig.Frame?.HeadTip.Z ?? 0f;
                foreach (Vector3 pivot in body.Rig.Pivots) front = MathF.Max(front, pivot.Z);
                foreach ((string name, float speed) in new[] { ("thrown at a wall", 9f), ("thrown hard at a wall", 30f), ("hurled at a wall", 90f) })
                {
                    var wall = new Wall { Z = MathF.Max(7f, front + 1f), Thickness = 0.2f, Height = PlacementTall };
                    CharacterAnimator animator = Thrown(body, wall, new Vector3(0f, speed * 0.2f, speed));
                    bool sound = true;
                    string problem = "";
                    float furthest = float.MinValue;
                    for (int i = 0; i < 1500; i++)
                    {
                        animator.Step(AnimationInput.At(Vector3.Zero, 0f), Step, wall);
                        if (sound && !Sound(animator, out problem)) sound = false;
                        for (int bone = 0; bone < Rig.BoneCount; bone++)
                        {
                            if (animator.Rig.Parents[bone] >= 0 || bone == (int)BoneType.Pelvis) furthest = MathF.Max(furthest, animator.JointOf((BoneType)bone).Z);
                        }
                    }
                    Check($"{body.Name} {name}: stops at it", furthest <= wall.Z && sound && animator.Resting, sound ? (animator.Resting ? $"reached {furthest:0.00} m of {wall.Z:0.00} m" : "still going") : problem);
                }
            }

            {
                float Landed(Vector3 push, float height)
                {
                    var wall = new Wall { Z = 7f, Thickness = 0.2f, Height = height };
                    CharacterAnimator animator = Thrown(Body.Named("bare"), wall, push);
                    for (int i = 0; i < 900; i++) animator.Step(AnimationInput.At(Vector3.Zero, 0f), Step, wall);
                    return animator.JointOf(BoneType.Pelvis).Z;
                }

                Vector3 high = new Vector3(0f, 22f, 22f);
                Check("What goes over something isn't stopped by it", Landed(high, 1.2f) > 7f, $"came down at {Landed(high, 1.2f):0.0} m");
                Check("But the same throw is stopped by something tall", Landed(high, PlacementTall) < 7f, $"came down at {Landed(high, PlacementTall):0.0} m");

                Vector3 low = new Vector3(0f, 4f, 22f);
                Check("And something low stops what's thrown low", Landed(low, 2.4f) < 7f, $"came down at {Landed(low, 2.4f):0.0} m");

                // How fast it arrives against how fast it leaves.
                var solid = new Wall { Z = 7f, Thickness = 0.2f, Height = PlacementTall };
                CharacterAnimator bounced = Thrown(Body.Named("bare"), solid, new Vector3(0f, 4f, 30f));
                float arrived = 0f;
                float left = 0f;
                float before = bounced.JointOf(BoneType.Pelvis).Z;
                for (int i = 0; i < 240; i++)
                {
                    bounced.Step(AnimationInput.At(Vector3.Zero, 0f), Step, solid);
                    float now = bounced.JointOf(BoneType.Pelvis).Z;
                    float speed = (now - before) / Step;
                    before = now;
                    arrived = MathF.Max(arrived, speed);
                    left = MathF.Max(left, -speed);
                }
                Check("A wall takes the speed out of a body rather than sending it back", arrived > 15f && left < arrived * 0.35f, $"arrived at {arrived:0.0} m/s, left at {left:0.0} m/s");
            }

            // Something falling right up against a wall can start inside it.
            {
                var wall = new Wall { Z = 0.4f, Thickness = 2f, Height = PlacementTall };
                CharacterAnimator animator = Thrown(Body.Named("bare"), wall, new Vector3(0f, 2f, 1f));
                bool sound = true;
                string problem = "";
                for (int i = 0; i < 1500; i++)
                {
                    animator.Step(AnimationInput.At(Vector3.Zero, 0f), Step, wall);
                    if (sound && !Sound(animator, out problem)) sound = false;
                }
                Check("Dying inside a wall comes out of it without any fuss", sound && animator.Resting, problem);
            }
        }

        // Someone can come into view frozen solid, or be there already when a player arrives, and there's no earlier moment to have been standing in.
        private static void FirstSeenFrozen()
        {
            Console.WriteLine("First seen frozen");
            var flat = new FlatGround();
            foreach (Body body in Body.All)
            {
                float WorstAgainstAlive(float rate)
                {
                    CharacterAnimator frozen = body.NewAnimator();
                    CharacterAnimator alive = body.NewAnimator();
                    AnimationInput input = AnimationInput.At(Vector3.Zero, 0f);
                    AnimationInput still = input;
                    still.Rate = rate;
                    for (int i = 0; i < 3; i++)
                    {
                        frozen.Step(still, Step, flat);
                        if (i == 0) alive.Step(input, Step, flat);
                    }

                    float worst = 0f;
                    for (int bone = 0; bone < Rig.BoneCount; bone++) worst = MathF.Max(worst, (frozen.JointOf((BoneType)bone) - alive.JointOf((BoneType)bone)).Length);
                    return worst;
                }

                float off = WorstAgainstAlive(0f);
                Check($"{body.Name}: stands the way anyone stands", off < 0.05f, $"a joint is {off:0.00} m from where it should be");
            }
        }

        // A long neck goes where it's told: the mouth gets to a point anywhere a giant would eat or look from, high in a tree, low in front of it and off to either side, and up over the tallest
        // trees there are, and it gets there without a jump.
        private static void Reaching()
        {
            Console.WriteLine("Reaching");
            Body body = Body.Named("giant");
            SaurianFrame frame = body.Rig.Frame!;
            var flat = new FlatGround();
            Vector3 root = body.Rig.Pivots[(int)BoneType.Neck];
            Vector3 rest = body.Rig.Pivots[(int)BoneType.Head] + frame.Mouth;
            float reach = (rest - root).Length;

            Vector3 Mouth(CharacterAnimator animator) => animator.JointOf(BoneType.Head) + Vector3.Transform(frame.Mouth, animator.RotationOf(BoneType.Head));

            float worst = 0f;
            string worstAt = "";
            int tried = 0;
            foreach (float height in new[] { 0.42f, 0.6f, 0.85f, 1.05f, 1.2f })
            {
                foreach (float side in new[] { -0.55f, 0f, 0.55f })
                {
                    foreach (float out_ in new[] { 0.76f, 0.9f })
                    {
                        // As a herd asks: no further from the root of the neck than it reaches comfortably, and no nearer than a neck that long folds, which isn't far.
                        Vector3 target = new Vector3(MathF.Sin(side) * reach * out_, rest.Y * height, root.Z + MathF.Cos(side) * reach * out_);
                        Vector3 fromRoot = target - root;
                        if (fromRoot.Length > reach * 0.93f) target = root + fromRoot.Normalized() * reach * 0.93f;
                        CharacterAnimator animator = body.NewAnimator();
                        var input = AnimationInput.At(Vector3.Zero, 0f);
                        input.ReachFor = target;
                        input.Reaching = 1f;
                        for (int i = 0; i < 360; i++) animator.Step(input, Step, flat);
                        float off = (Mouth(animator) - target).Length;
                        tried++;
                        if (off <= worst) continue;
                        worst = off;
                        worstAt = $"{target.Y:0.0} m up, {(target - root).Length / reach:P0} of the neck's reach out, {side:0.00} rad round";
                    }
                }
            }
            Check("A long neck puts its mouth where it's told", worst < 1.2f, $"{tried} places, {worst:0.00} m off at worst, {worstAt}");

            {
                CharacterAnimator animator = body.NewAnimator();
                var input = AnimationInput.At(Vector3.Zero, 0f);
                input.ReachFor = root + new Vector3(0f, reach * 2f, reach * 0.3f);
                input.Reaching = 1f;
                for (int i = 0; i < 360; i++) animator.Step(input, Step, flat);
                float highest = Mouth(animator).Y;
                Check("And reaches over the tallest tree there is", highest > 24f, $"{highest:0.0} m up, as against 21.9 m for the tallest in the Haven");
            }

            {
                // Sent somewhere new each frame, from high to low and side to side, it follows without a joint ever jumping.
                CharacterAnimator animator = body.NewAnimator();
                var random = new Random(3);
                var input = AnimationInput.At(Vector3.Zero, 0f);
                float jump = 0f;
                Vector3 last = Vector3.Zero;
                for (int i = 0; i < 1200; i++)
                {
                    if (i % 40 == 0)
                    {
                        input.ReachFor = root + new Vector3(random.NextSingle() * 2f - 1f, random.NextSingle() * 1.6f - 0.6f, random.NextSingle()) * reach;
                        input.Reaching = random.NextDouble() < 0.8 ? 1f : 0f;
                        input.Looking = random.NextDouble() < 0.5;
                        input.LookAt = new Vector3(random.NextSingle() * 60f - 30f, 4f, random.NextSingle() * 60f - 10f);
                    }
                    animator.Step(input, Step, flat);
                    Vector3 head = animator.JointOf(BoneType.Head);
                    if (i > 0) jump = MathF.Max(jump, (head - last).Length);
                    last = head;
                }
                Check("A long neck never jumps, however it's told", jump < 0.6f && animator.Faults == 0, $"the head moved at most {jump:0.00} m in a frame");
            }
        }

        private const float PlacementTall = PlacementIndex.Tall;

        // ---- What a world really has in it, by the same road a game takes.

        private static void Standing()
        {
            Console.WriteLine("Against what a world has in it");

            WorldData world = new WorldGenerator().Generate(new WorldRequest(303, "isle"));
            var ground = new WorldGround(world);

            var near = new List<(Footprint Shape, float Height)>();
            var seen = new HashSet<Vector2>();
            var buildings = new List<Footprint>();
            var trunks = new List<Footprint>();
            var random = new Random(1);
            for (int look = 0; look < 400; look++)
            {
                near.Clear();
                world.Placement.CollectSolids(new Vector2(random.NextSingle(), random.NextSingle()) * world.Size, 40f, near);
                foreach ((Footprint shape, float height) in near)
                {
                    if (height < PlacementIndex.Tall || !seen.Add(shape.Centre)) continue;
                    (shape.IsCircle ? trunks : buildings).Add(shape);
                }
            }

            // A clear run at it across level ground, and how far past the middle of it a body ends up.
            bool RunUp(in Footprint shape, out Vector2 start, out Vector2 way)
            {
                for (int tries = 0; tries < 12; tries++)
                {
                    float angle = tries * MathF.Tau / 12f;
                    way = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    start = shape.Centre - way * (shape.BoundingRadius + 8f);
                    bool clear = world.InBounds(start);
                    for (float at = 0f; clear && at < 6f; at += 1f)
                    {
                        Vector2 point = start + way * at;
                        clear = !world.Placement.OverlapsSolid(Footprint.Circle(point, 1.6f)) && MathF.Abs(world.Height.Sample(point) - world.Height.Sample(start)) < 1f;
                    }
                    if (clear) return true;
                }
                start = default;
                way = default;
                return false;
            }

            float Thrown(Vector2 start, Vector2 way, Vector2 past, out bool sound, out string problem)
            {
                var input = AnimationInput.At(new Vector3(start.X, world.Height.Sample(start), start.Y), 0f);
                CharacterAnimator animator = Body.Named("bare").NewAnimator();
                for (int i = 0; i < 30; i++) animator.Step(input, Step, ground);
                animator.Die(new Vector3(way.X * 30f, 6f, way.Y * 30f));

                sound = true;
                problem = "";
                for (int i = 0; i < 360; i++)
                {
                    animator.Step(input, Step, ground);
                    if (sound && !Sound(animator, out problem)) sound = false;
                }
                Vector2 rests = input.Position.Xz + animator.JointOf(BoneType.Pelvis).Xz;
                return Vector2.Dot(rests - past, way);
            }

            int tried = 0;
            int through = 0;
            int unsound = 0;
            string first = "";
            foreach (Footprint shape in buildings.Take(60))
            {
                if (!RunUp(shape, out Vector2 start, out Vector2 way)) continue;
                float beyond = Thrown(start, way, shape.Centre, out bool sound, out string problem);
                tried++;
                if (!sound)
                {
                    unsound++;
                    first = problem;
                }
                if (beyond > 0f)
                {
                    through++;
                    first = $"{beyond:0.0} m past the middle of one {shape.BoundingRadius:0.0} m across";
                }
            }
            Check($"Thrown hard at a building, nobody goes through it", tried >= 20 && through == 0 && unsound == 0, $"{tried} thrown, {through} went through, {unsound} came apart {first}");

            tried = 0;
            through = 0;
            foreach (Footprint shape in trunks.Take(120))
            {
                if (!RunUp(shape, out Vector2 start, out Vector2 way)) continue;
                float beyond = Thrown(start, way, shape.Centre, out bool sound, out string problem);
                tried++;
                if (beyond > shape.BoundingRadius + 2f) through++;
                if (!sound) unsound++;
            }
            Check("Nor does a tree stop being there", tried >= 30 && through <= tried / 5 && unsound == 0, $"{tried} thrown, {through} carried on past, {unsound} came apart");
        }

        private static CharacterAnimator Thrown(Body body, IGround ground, Vector3 push)
        {
            CharacterAnimator animator = body.NewAnimator();
            for (int i = 0; i < 30; i++) animator.Step(AnimationInput.At(Vector3.Zero, 0f), Step, ground);
            animator.Die(push);
            return animator;
        }

        /// <summary>A floor with a wall across it.</summary>
        private sealed class Wall : IGround
        {
            public float Z;
            public float Thickness;
            public float Height;

            public float HeightAt(float x, float z) => 0f;

            public bool LookAround(Vector2 centre, float reach) => MathF.Abs(centre.Y - Z) < reach + Thickness;

            public bool TryPushOut(Vector3 at, float radius, out Vector2 push)
            {
                push = default;
                float half = Thickness * 0.5f + radius;
                float gap = at.Z - Z;
                if (at.Y > Height || MathF.Abs(gap) >= half) return false;
                push = new Vector2(0f, gap < 0f ? -half - gap : half - gap);
                return true;
            }
        }

        /// <summary>What a body costs, and that it costs no memory.</summary>
        public static int Bench()
        {
            var ground = new Slope();
            foreach (string name in new[] { "bare", "sword", "shield", "twin", "wolf", "raptor", "tyrant", "hornback", "giant" })
            {
                Body body = Body.Named(name);
                CharacterAnimator[] crowd = new CharacterAnimator[200];
                for (int i = 0; i < crowd.Length; i++) crowd[i] = body.NewAnimator(i);

                float time = 0f;
                void Frame()
                {
                    time += Step;
                    for (int i = 0; i < crowd.Length; i++)
                    {
                        float z = time * 8f + i * 4f;
                        crowd[i].Step(AnimationInput.At(new Vector3(i * 3f, ground.HeightAt(i * 3f, z), z), 0f), Step, ground);
                    }
                }

                for (int i = 0; i < 120; i++) Frame();
                long before = GC.GetAllocatedBytesForCurrentThread();
                var watch = Stopwatch.StartNew();
                const int frames = 300;
                for (int i = 0; i < frames; i++) Frame();
                watch.Stop();
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                double each = watch.Elapsed.TotalMilliseconds * 1000.0 / (frames * crowd.Length);
                Console.WriteLine($"{name,-8} {each,6:0.0} us a body a frame, {each * 200.0 / 1000.0:0.00} ms for two hundred, {allocated / (frames * crowd.Length)} bytes allocated each");
            }

            {
                // A herd of giants as the Haven has them, walking slowly with their necks in a tree, chewing, and keeping their feet off someone, which is every part of a giant's body at once.
                Body body = Body.Named("giant");
                CharacterAnimator[] herd = new CharacterAnimator[30];
                for (int i = 0; i < herd.Length; i++) herd[i] = body.NewAnimator(i);
                float time = 0f;
                void Frame()
                {
                    time += Step;
                    for (int i = 0; i < herd.Length; i++)
                    {
                        float z = time * 1.5f + i * 30f;
                        var position = new Vector3(i * 30f, ground.HeightAt(i * 30f, z), z);
                        var input = AnimationInput.At(position, 0f);
                        input.ReachFor = position + new Vector3(MathF.Sin(time + i) * 3f, 17f, 15f);
                        input.Reaching = 1f;
                        input.Chewing = 1f;
                        input.KeepOff = position.Xz + new Vector2(2.5f, 6f);
                        input.KeepOffRadius = 2.2f;
                        herd[i].Step(input, Step, ground);
                    }
                }
                for (int i = 0; i < 120; i++) Frame();
                long before = GC.GetAllocatedBytesForCurrentThread();
                var watch = Stopwatch.StartNew();
                const int frames = 300;
                for (int i = 0; i < frames; i++) Frame();
                watch.Stop();
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                double each = watch.Elapsed.TotalMilliseconds * 1000.0 / (frames * herd.Length);
                Console.WriteLine($"browsing {each,6:0.0} us a giant a frame, reaching into a tree as it walks, {allocated / (frames * herd.Length)} bytes allocated each");
            }

            {
                var flat = new FlatGround();
                CharacterAnimator[] crowd = new CharacterAnimator[60];
                for (int i = 0; i < crowd.Length; i++)
                {
                    crowd[i] = Body.Named("sword").NewAnimator(i);
                    for (int j = 0; j < 20; j++) crowd[i].Step(AnimationInput.At(new Vector3(i * 2f, 0f, 0f), 0f), Step, flat);
                    crowd[i].Die(new Vector3(0f, 14f, 20f));
                }
                var watch = Stopwatch.StartNew();
                const int frames = 90;
                for (int f = 0; f < frames; f++)
                {
                    for (int i = 0; i < crowd.Length; i++) crowd[i].Step(AnimationInput.At(new Vector3(i * 2f, 0f, 0f), 0f), Step, flat);
                }
                watch.Stop();
                double each = watch.Elapsed.TotalMilliseconds * 1000.0 / (frames * crowd.Length);
                Console.WriteLine($"falling  {each,6:0.0} us a body a frame, {each * 60.0 / 1000.0:0.00} ms for sixty in the air at once");
            }

            {
                var wall = new Wall { Z = 3f, Thickness = 1f, Height = PlacementTall };
                CharacterAnimator[] crowd = new CharacterAnimator[60];
                for (int i = 0; i < crowd.Length; i++)
                {
                    crowd[i] = Body.Named("sword").NewAnimator(i);
                    for (int j = 0; j < 20; j++) crowd[i].Step(AnimationInput.At(new Vector3(i * 2f, 0f, 0f), 0f), Step, wall);
                    crowd[i].Die(new Vector3(0f, 14f, 20f));
                }
                long before = GC.GetAllocatedBytesForCurrentThread();
                var watch = Stopwatch.StartNew();
                const int frames = 90;
                for (int f = 0; f < frames; f++)
                {
                    for (int i = 0; i < crowd.Length; i++) crowd[i].Step(AnimationInput.At(new Vector3(i * 2f, 0f, 0f), 0f), Step, wall);
                }
                watch.Stop();
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                double each = watch.Elapsed.TotalMilliseconds * 1000.0 / (frames * crowd.Length);
                Console.WriteLine($"by a wall{each,6:0.0} us a body a frame, {each * 60.0 / 1000.0:0.00} ms for sixty at once, {allocated / (frames * crowd.Length)} bytes allocated each");
            }
            return 0;
        }
    }
}

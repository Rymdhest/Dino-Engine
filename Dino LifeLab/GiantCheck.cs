using Dino_Engine.Animation;
using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Procedural.Saurian;
using Dino_Engine.Util;
using Dino_Engine.Wildlife;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_LifeLab
{
    /// <summary>
    /// The Haven's giants, for a while, in the real Haven, on the bodies the game gives them, with someone walking out from the village to see them, standing right under one, and going home. Fails on
    /// anything a giant mustn't do: put a foot in the water, in anything solid, in the village or on someone, come apart, stand idle for long, not reach the trees, or never eat. It says nothing about
    /// how they look. For that, look at them.
    /// </summary>
    internal static class GiantCheck
    {
        private const float Step = 1f / 30f;
        private const float HeroSpeed = 8f;
        private const float HeroWidth = 1.1f;       // A person's body, as wide as the game's characters are taken to be.
        private const float WetFoot = 0.35f;        // Metres of water a giant's foot can stand in: no more than the edge of a beach.
        private const float TailOverHead = 2.3f;    // Metres up that a tail goes over a person's head.

        public static int Run(Options options) => Run(options.Int("seed", 1), options.Float("minutes", 12f), options.Text("out", ""), true, options.Text("trace", ""));

        /// <param name="trace">"giant,from,to": what one giant is doing every half second between two times, in seconds.</param>
        /// <returns>How many things went wrong.</returns>
        public static int Run(int seed, float minutes, string map, bool talk, string trace = "")
        {
            int traced = -1;
            float traceFrom = 0f, traceTo = 0f;
            if (trace.Split(',') is { Length: 3 } parts)
            {
                traced = int.Parse(parts[0]);
                traceFrom = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                traceTo = float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
            }
            WorldData world = new Dino_Engine.World.Generation.WorldGenerator().Generate(new WorldRequest(1610, "haven"));
            WorldSite village = Herds.Village(world) ?? throw new InvalidOperationException("The Haven has no village.");
            var rigs = new List<Rig>();
            var shapes = new List<GiantShape>();
            foreach ((string model, float scale) in Herds.Haven)
            {
                Rig rig = SaurianForge.RigOf(SaurianSpec.Parse(model) ?? throw new InvalidOperationException($"No giant called {model}."));
                rigs.Add(rig);
                shapes.Add(GiantShape.Of(rig, scale));
            }

            int failures = 0;
            var reports = new List<string>();
            void Fail(string report)
            {
                if (failures++ < 20) reports.Add(report);
            }

            Herd herd = Herds.InHaven(world, shapes, (uint)seed) ?? throw new InvalidOperationException("No herd in the Haven.");
            float clock = 0f;
            if (traced >= 0) herd.Report = line => { if (clock >= traceFrom && clock <= traceTo) Console.WriteLine($"  {clock,6:0.0}s {line}"); };
            if (herd.Members.Count != shapes.Count) Fail($"only {herd.Members.Count} of {shapes.Count} giants found room in the Haven");

            // The tallest tree they could ever be asked to eat from, against the highest any of them reaches.
            float tallest = 0f;
            foreach (Browse tree in herd.Land.Trees) tallest = MathF.Max(tallest, tree.Height);
            float lowestReach = float.MaxValue;
            foreach (GiantShape shape in shapes) lowestReach = MathF.Min(lowestReach, shape.HighestMouth);
            if (lowestReach < tallest + 1f) Fail($"a giant reaches {lowestReach:0.0} m, and the tallest tree in the Haven is {tallest:0.0} m");

            var ground = new WorldGround(world);
            var animators = new CharacterAnimator[herd.Members.Count];
            for (int i = 0; i < animators.Length; i++)
            {
                Rig rig = rigs[shapes.IndexOf(herd.Members[i].Shape)];
                animators[i] = new CharacterAnimator(rig, MotionStyle.Giant, i * 7 + 3);
            }

            var doing = new float[herd.Members.Count, Enum.GetValues<GiantDoing>().Length];
            var walked = new float[herd.Members.Count];
            var idle = new float[herd.Members.Count];
            var idleFrom = new Vector2[herd.Members.Count];
            var idleTold = new bool[herd.Members.Count];
            var trees = new HashSet<Vector2>[herd.Members.Count];
            for (int i = 0; i < trees.Length; i++) trees[i] = new HashSet<Vector2>();
            var trails = new List<Vector2>[herd.Members.Count];
            for (int i = 0; i < trails.Length; i++) trails[i] = new List<Vector2>();
            var heroTrail = new List<Vector2>();
            int calls = 0, tugs = 0, regards = 0, bites = 0, reached = 0;
            float worstReach = 0f;
            string worstBite = "";
            var onSomeone = new float[herd.Members.Count];
            var tailTold = new bool[herd.Members.Count];
            Vector2 heroWas = Vector2.Zero;
            float heroStill = 0f;
            int brushed = 0;
            var lastDoing = new GiantDoing[herd.Members.Count];

            // Someone who walks out to one of the giants, comes right up under it and stands there, wanders round it, and goes back to the square, over and over.
            Vector2 hero = village.Position;
            Vector2 heroGoal = hero;
            float heroWait = 20f;
            int visit = 0;
            Span<Vector2> people = stackalloc Vector2[1];

            int frames = (int)(minutes * 60f / Step);
            for (int frame = 0; frame < frames; frame++)
            {
                float time = frame * Step;
                clock = time;
                heroWait -= Step;
                if (heroWait <= 0f && (heroGoal - hero).Length < 0.5f)
                {
                    visit++;
                    Giant visited = herd.Members[visit / 3 % herd.Members.Count];
                    heroGoal = (visit % 3) switch
                    {
                        0 => village.Position,
                        1 => visited.Middle,                                            // Right under it.
                        _ => visited.Middle + visited.Forward * (visited.Shape.Front + 6f),  // In front of it, where it would walk.
                    };
                    heroWait = visit % 3 == 0 ? 25f : 12f;
                }
                Vector2 toGoal = heroGoal - hero;
                if (toGoal.Length > 0.01f) hero += toGoal.Normalized() * MathF.Min(HeroSpeed * Step, toGoal.Length);
                people[0] = hero;

                herd.Step(Step, people);

                for (int i = 0; i < herd.Members.Count; i++)
                {
                    Giant giant = herd.Members[i];
                    CharacterAnimator animator = animators[i];
                    Vector2 root = giant.Root;
                    var input = AnimationInput.At(new Vector3(root.X, world.Height.Sample(root), root.Y), giant.Yaw, giant.Shape.Scale);
                    input.ReachFor = giant.ReachFor;
                    input.Reaching = giant.Reaching;
                    input.LookAt = giant.LookAt;
                    input.Looking = giant.Looking;
                    input.Chewing = giant.Chewing;
                    input.KeepOff = giant.KeepOff;
                    input.KeepOffRadius = giant.KeepOffRadius;
                    Vector2 was = animator.Position.Xz;
                    animator.Step(input, Step, ground);
                    if (frame > 0) walked[i] += (input.Position.Xz - was).Length;

                    doing[i, (int)giant.Doing] += Step;
                    // Not eating, looking at anyone or calling, and not getting anywhere either, whatever it means to be doing.
                    if (giant.Doing is GiantDoing.Browsing or GiantDoing.Regarding or GiantDoing.Calling || (giant.Middle - idleFrom[i]).Length > 3f)
                    {
                        idle[i] = 0f;
                        idleFrom[i] = giant.Middle;
                    }
                    else idle[i] += Step;
                    if (idle[i] > 100f && !idleTold[i])
                    {
                        Fail($"giant {i} stood about for over 100 s by {time:0} s, at {giant.Middle.X:0},{giant.Middle.Y:0}");
                        idleTold[i] = true;
                    }
                    if (giant.EatingFrom(out Vector2 trunk)) trees[i].Add(trunk);
                    if (giant.Called) calls++;
                    if (giant.Tugged) tugs++;
                    if (giant.Doing == GiantDoing.Regarding && lastDoing[i] != GiantDoing.Regarding) regards++;
                    lastDoing[i] = giant.Doing;
                    if (frame % 30 == 0) trails[i].Add(giant.Middle);

                    if (animator.Faults > 0)
                    {
                        Fail($"giant {i} came apart at {time:0} s");
                        return Finish(failures, reports, talk);
                    }

                    if (i == traced && time >= traceFrom && time <= traceTo && (frame % 15 == 0 || traceTo - traceFrom < 3f))
                    {
                        Vector3 mouthNow = input.Position + Vector3.Transform(animator.JointOf(BoneType.Head) + Vector3.Transform(animator.Rig.Frame!.Mouth, animator.RotationOf(BoneType.Head)), Frames.Yaw(giant.Yaw)) * giant.Shape.Scale;
                        string feet = string.Join(" ", animator.Gait.Feet.Select(foot => $"{(foot.Swinging ? "~" : "")}{foot.At.X:0.0},{foot.At.Z:0.0}"));
                        Console.WriteLine($"  {time,6:0.00}s hero {hero.X:0.0},{hero.Y:0.0} {giant.Doing,-9} middle {giant.Middle.X:0.0},{giant.Middle.Y:0.0} yaw {giant.Yaw:0.00} speed {giant.Speed:0.0} reaching {giant.Reaching:0.0} for {giant.ReachFor.X:0.0},{giant.ReachFor.Y:0.0},{giant.ReachFor.Z:0.0} mouth {mouthNow.X:0.0},{mouthNow.Y:0.0},{mouthNow.Z:0.0} chew {giant.Chewing:0} clear {herd.Land.Clear(giant.Middle):0.0} feet {feet}");
                    }

                    // Where its mouth really got to, against where it was sent, once it's had time to get there.
                    if (giant.Doing == GiantDoing.Browsing && giant.Chewing > 0.5f)
                    {
                        Vector3 mouth = input.Position + Vector3.Transform(animator.JointOf(BoneType.Head) + Vector3.Transform(animator.Rig.Frame!.Mouth, animator.RotationOf(BoneType.Head)), Frames.Yaw(giant.Yaw)) * giant.Shape.Scale;
                        float off = (mouth - giant.ReachFor).Length;
                        bites++;
                        if (off < 1.5f * giant.Shape.Scale) reached++;
                        if (off > worstReach)
                        {
                            worstReach = off;
                            Vector3 neckRoot = giant.NeckRootAt(input.Position.Y);
                            worstBite = $"giant {i} at {time:0} s, {(giant.ReachFor - neckRoot).Length / giant.Shape.Reach:P0} of its reach from the root of its neck, {giant.ReachFor.Y - input.Position.Y:0.0} m up";
                        }
                    }

                    // A foot never comes down on anyone, and isn't left on anyone who stands still for long: someone who walks into a leg has done that themselves.
                    bool touching = false;
                    for (int f = 0; f < animator.Gait.Feet.Length; f++)
                    {
                        Foot foot = animator.Gait.Feet[f];
                        if (foot.Swinging) continue;
                        Vector2 at = foot.At.Xz;
                        if (world.WaterDepthAt(at) > WetFoot) Fail($"giant {i} stood in the water at {at.X:0},{at.Y:0}, {world.WaterDepthAt(at):0.0} m deep, at {time:0} s");
                        if (world.Placement.OverlapsSolid(Footprint.Circle(at, 0.5f))) Fail($"giant {i} put a foot in something solid at {at.X:0},{at.Y:0} at {time:0} s");
                        if ((at - village.Position).Length < village.Radius - 2f) Fail($"giant {i} put a foot in the village at {at.X:0},{at.Y:0} at {time:0} s");
                        bool on = (at - hero).Length < HeroWidth + 0.6f;
                        if (on && (animator.Footfalls & (1 << f)) != 0)
                        {
                            // On someone who'd been standing there, it should have seen them. Someone running under a foot that was already coming down did that themselves.
                            if (heroStill > 1f) Fail($"giant {i} put a foot down on someone standing still at {at.X:0},{at.Y:0} at {time:0} s");
                            else brushed++;
                        }
                        touching |= on;
                    }
                    // Nor is a tail swept through anyone standing about, of whatever of it is low enough to hit them.
                    if (heroStill > 1f && !tailTold[i])
                    {
                        foreach (BoneType bone in animator.Rig.Frame!.Tail)
                        {
                            Vector3 joint = input.Position + Vector3.Transform(animator.JointOf(bone), Frames.Yaw(giant.Yaw)) * giant.Shape.Scale;
                            if (joint.Y - world.Height.Sample(joint.Xz) > TailOverHead || (joint.Xz - hero).Length > HeroWidth + 0.4f) continue;
                            Fail($"giant {i} swept its tail through someone standing still at {hero.X:0},{hero.Y:0} at {time:0} s");
                            tailTold[i] = true;
                            break;
                        }
                    }

                    bool still = (hero - heroWas).Length < 0.01f;
                    onSomeone[i] = touching && still ? onSomeone[i] + Step : 0f;
                    if (onSomeone[i] > 4f)
                    {
                        Fail($"giant {i} kept a foot on someone standing still for 4 s, at {time:0} s");
                        onSomeone[i] = float.MinValue;
                    }
                }
                if (frame % 30 == 0) heroTrail.Add(hero);
                heroStill = (hero - heroWas).Length < 0.01f ? heroStill + Step : 0f;
                heroWas = hero;
            }

            // What a day of theirs should have in it.
            float total = minutes * 60f;
            for (int i = 0; i < herd.Members.Count; i++)
            {
                float browsing = doing[i, (int)GiantDoing.Browsing] / total;
                float walking = doing[i, (int)GiantDoing.Walking] / total;
                if (browsing < 0.3f) Fail($"giant {i} spent only {browsing:P0} of its time eating");
                if (walking < 0.04f || walked[i] < 60f) Fail($"giant {i} hardly went anywhere: {walked[i]:0} m, {walking:P0} of its time walking");
                if (trees[i].Count < 2) Fail($"giant {i} ate from only {trees[i].Count} trees");
                if (talk) Console.WriteLine($"  giant {i}: {browsing:P0} eating, {walking:P0} walking, {doing[i, (int)GiantDoing.Standing] / total:P0} standing, {doing[i, (int)GiantDoing.Regarding] / total:P0} looking at someone, {doing[i, (int)GiantDoing.Shuffling] / total:P0} shuffling, {walked[i]:0} m walked, {trees[i].Count} trees");
            }
            if (calls < 3) Fail($"only {calls} calls in {minutes:0} minutes");
            if (regards < 2) Fail($"they looked at whoever came to see them only {regards} times");
            float reachShare = bites > 0 ? reached / (float)bites : 0f;
            if (reachShare < 0.85f) Fail($"a mouth got to what it was reaching for only {reachShare:P0} of the time, {worstReach:0.0} m off at worst: {worstBite}");
            if (talk)
            {
                Console.WriteLine($"  {calls} calls, {tugs} pulls at a crown, {regards} long looks at someone, mouths where they were sent {reachShare:P0} of the time ({worstReach:0.0} m off at worst: {worstBite})");
                Console.WriteLine($"  the tallest tree in the Haven is {tallest:0.0} m, and the shortest reach among them {lowestReach:0.0} m");
                Console.WriteLine($"  {brushed} times someone ran in under a foot that was already coming down");
            }

            if (map.Length > 0) Draw(world, herd, village, trails, heroTrail, map);
            return Finish(failures, reports, talk);
        }

        private static int Finish(int failures, List<string> reports, bool talk)
        {
            foreach (string report in reports) Console.WriteLine($"  FAIL  {report}");
            if (talk) Console.WriteLine(failures == 0 ? "Giants: ok" : $"Giants: {failures} faults");
            return failures;
        }

        // The isle from above, with its trees, the village, where each giant went and where the visitor walked.
        private static void Draw(WorldData world, Herd herd, WorldSite village, List<Vector2>[] trails, List<Vector2> hero, string path)
        {
            const int Size = 1024;
            float scale = Size / world.Size;
            var pixels = new byte[Size * Size * 4];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    var at = new Vector2((x + 0.5f) / scale, world.Size - (y + 0.5f) / scale);
                    bool water = world.WaterDepthAt(at) > 0.05f;
                    float clear = herd.Land.Clear(at);
                    Vector3 colour = water ? new Vector3(0.16f, 0.32f, 0.45f) : clear > 0f ? new Vector3(0.36f, 0.5f, 0.25f) : new Vector3(0.28f, 0.33f, 0.24f);
                    if ((at - village.Position).Length < village.Radius) colour = Vector3.Lerp(colour, new Vector3(0.6f, 0.55f, 0.45f), 0.5f);
                    Put(pixels, x, y, colour);
                }
            }
            foreach (Browse tree in herd.Land.Trees) Circle(pixels, tree.Trunk, tree.CrownRadius, new Vector3(0.12f, 0.28f, 0.1f), world, scale);
            Vector3[] colours = { new Vector3(0.95f, 0.55f, 0.2f), new Vector3(0.95f, 0.9f, 0.3f), new Vector3(0.9f, 0.4f, 0.8f), new Vector3(0.3f, 0.9f, 0.9f) };
            for (int i = 0; i < trails.Length; i++) Line(pixels, trails[i], colours[i % colours.Length], world, scale);
            Line(pixels, hero, new Vector3(1f, 1f, 1f), world, scale);
            PngWriter.Write(path, pixels, Size, Size);
            Console.WriteLine($"  wrote {path}");
        }

        private static void Put(byte[] pixels, int x, int y, Vector3 colour)
        {
            const int Size = 1024;
            if (x < 0 || y < 0 || x >= Size || y >= Size) return;
            int i = (y * Size + x) * 4;
            pixels[i] = (byte)(Math.Clamp(colour.X, 0f, 1f) * 255f);
            pixels[i + 1] = (byte)(Math.Clamp(colour.Y, 0f, 1f) * 255f);
            pixels[i + 2] = (byte)(Math.Clamp(colour.Z, 0f, 1f) * 255f);
            pixels[i + 3] = 255;
        }

        private static (int x, int y) Pixel(Vector2 at, WorldData world, float scale) => ((int)(at.X * scale), (int)((world.Size - at.Y) * scale));

        private static void Circle(byte[] pixels, Vector2 centre, float radius, Vector3 colour, WorldData world, float scale)
        {
            for (int k = 0; k < 64; k++)
            {
                float angle = k * MathF.Tau / 64f;
                (int x, int y) = Pixel(centre + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius, world, scale);
                Put(pixels, x, y, colour);
            }
        }

        private static void Line(byte[] pixels, List<Vector2> points, Vector3 colour, WorldData world, float scale)
        {
            for (int i = 1; i < points.Count; i++)
            {
                (int x0, int y0) = Pixel(points[i - 1], world, scale);
                (int x1, int y1) = Pixel(points[i], world, scale);
                int steps = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0)) + 1;
                for (int s = 0; s <= steps; s++) Put(pixels, x0 + (x1 - x0) * s / steps, y0 + (y1 - y0) * s / steps, colour);
            }
        }
    }
}

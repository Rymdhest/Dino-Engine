using Dino_Engine.ECS.Components;
using Dino_Engine.Physics;
using Dino_Engine.Wildlife;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using Dino_Engine.World.Generation;
using OpenTK.Mathematics;
using System.Diagnostics;

namespace Dino_LifeLab
{
    /// <summary>
    /// Walks someone through real worlds, with nothing else going on, and checks what's alive there holds up: nothing goes through the ground or the camera, or stands where it can't, and everything gets out of the way.
    /// It proves nothing about how any of it looks. For that, look at it.
    /// </summary>
    internal static class LifeCheck
    {
        private const float Step = 1f / 60f;
        private const float WalkSpeed = 8f;
        private const float ViewWide = 44f;     // Half of what can be seen across, at 16:9, in metres.
        private const float ViewDeep = 25f;
        private const float CameraHeight = 30f;

        private sealed class Tally
        {
            public int Failures;
            public readonly Dictionary<string, int> Seen = new();
            public readonly List<string> Reports = new();
            public double WorstStep, WorstDraw, TotalStep, TotalDraw;
            public long Frames, Allocated, CreatureFrames, LeafFrames, AirborneFrames, MeasuredFrames;
            public int MostCreatures, MostLeaves, WorldsWithBirds, WorldsWithRabbits, WorldsWithCrabs;

            public void Fail(string report)
            {
                if (Failures++ < 24) Reports.Add(report);
            }
        }

        public static int Run(Options options)
        {
            int first = options.Int("seed", 1);
            int count = options.Int("count", 12);
            float seconds = options.Float("seconds", 50f);
            string? archetype = options.Text("archetype", "") is { Length: > 0 } named ? named : null;
            var tally = new Tally();

            for (int seed = first; seed < first + count; seed++)
            {
                WorldData world = new WorldGenerator().Generate(new WorldRequest(seed, archetype));
                Walk(world, seed, seconds, tally);
                Console.WriteLine($"  seed {seed}: {world.Plan.Biomes.Count} biomes, {tally.Failures} faults so far");
            }

            Frightens(tally);
            Cards(tally);
            Report(tally, seconds);
            return tally.Failures == 0 ? 0 : 1;
        }

        // What the falling leaves stand on. These are the regimes of Andersen, Pesavento and Wang's figure 3, and getting one term of their equations wrong moves every one of them while still looking like a falling leaf.
        private static void Cards(Tally tally)
        {
            static (float turns, float swing) Measure(FallingCard card)
            {
                float least = float.PositiveInfinity, most = float.NegativeInfinity, left = float.PositiveInfinity, right = float.NegativeInfinity;
                for (float t = 100f; t < 220f; t += 0.05f)
                {
                    card.At(t, out float x, out _, out float angle);
                    least = MathF.Min(least, angle);
                    most = MathF.Max(most, angle);
                    float swing = x - card.MeanDrift * t;
                    left = MathF.Min(left, swing);
                    right = MathF.Max(right, swing);
                }
                return ((most - least) / MathF.Tau, right - left);
            }

            (float flutterTurns, float flutterSwing) = Measure(FallingCard.Flutter);
            (float tumbleTurns, _) = Measure(FallingCard.Tumble);
            (float sinkTurns, float sinkSwing) = Measure(FallingCard.Sink);

            if (flutterTurns >= 1f) tally.Fail($"a fluttering card went right over: {flutterTurns:0.00} turns");
            if (flutterSwing < 5f) tally.Fail($"a fluttering card hardly swung: {flutterSwing:0.0} half widths");
            if (MathF.Abs(FallingCard.Flutter.MeanDrift) > 0.02f) tally.Fail($"a fluttering card went off to one side at {FallingCard.Flutter.MeanDrift:0.000}");
            if (tumbleTurns < 4f) tally.Fail($"a tumbling card turned only {tumbleTurns:0.0} times");
            if (MathF.Abs(FallingCard.Tumble.MeanDrift) < 0.3f) tally.Fail($"a tumbling card didn't glide away: {FallingCard.Tumble.MeanDrift:0.000}");
            if (sinkTurns >= 1f || sinkSwing > 3f) tally.Fail($"a heavy card should come down broadside, and swung {sinkSwing:0.0} over {sinkTurns:0.00} turns");
            tally.Reports.Add($"cards: flutter swings {flutterSwing:0.0} half widths, tumble turns {tumbleTurns:0.0} times, a petal swings {sinkSwing:0.0}");
        }

        private static WildlifeView NewView(Vector2 at, float height, float moving)
        {
            var view = new WildlifeView
            {
                Camera = new Vector3(at.X, height + CameraHeight, at.Y + 14f),
                Focus = new Vector3(at.X, height, at.Y),
                Least = at - new Vector2(ViewWide, ViewDeep),
                Most = at + new Vector2(ViewWide, ViewDeep),
            };
            view.Scares.Add(new Scare { At = at, Moving = moving });
            return view;
        }

        // Someone going from place to place on foot, by whatever way the ground allows.
        private static void Walk(WorldData world, int seed, float seconds, Tally tally)
        {
            var random = new WorldRandom(seed).Fork("lifecheck");
            var sim = new WildlifeSim(world);
            var canvas = new CountingCanvas();
            Vector2 at = world.Sites.Count > 0 ? world.Sites[0].Position : new Vector2(world.Size * 0.5f);
            Vector2 goal = at;
            var wind = new WindSingleton { Speed = 4f, Direction = 60f };
            float total = 0f;
            bool birds = false, rabbits = false, crabs = false;

            for (int frame = 0; total < seconds; frame++, total += Step)
            {
                if ((goal - at).LengthSquared < 4f)
                {
                    for (int tries = 0; tries < 60; tries++)
                    {
                        Vector2 next = at + new Vector2(random.Range(-110f, 110f), random.Range(-110f, 110f));
                        if (world.InBounds(next) && world.Passable.IsSet(next))
                        {
                            goal = next;
                            break;
                        }
                    }
                }

                Vector2 way = goal - at;
                float length = way.Length;
                if (length > 0.01f) at += way / length * MathF.Min(WalkSpeed * Step, length);

                WildlifeView view = NewView(at, world.Height.Sample(at), 1f);
                wind.LiveSpeed = wind.Speed;
                wind.Velocity = new Vector2(2.4f, 3.2f);
                view.Wind = wind;

                long before = GC.GetAllocatedBytesForCurrentThread();
                long started = Stopwatch.GetTimestamp();
                sim.Step(view, Step);
                double stepped = Stopwatch.GetElapsedTime(started).TotalMilliseconds;

                started = Stopwatch.GetTimestamp();
                sim.Draw(canvas);
                double drawn = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

                sim.Noises.Clear();
                sim.Splashes.Clear();
                sim.Contacts.Clear();

                tally.Frames++;
                if (frame > 120)
                {
                    tally.MeasuredFrames++;
                    tally.Allocated += allocated;
                    tally.TotalStep += stepped;
                    tally.TotalDraw += drawn;
                    tally.WorstStep = Math.Max(tally.WorstStep, stepped);
                    tally.WorstDraw = Math.Max(tally.WorstDraw, drawn);
                    tally.CreatureFrames += sim.Creatures;
                    tally.LeafFrames += sim.Leaves.Count;
                    tally.AirborneFrames += sim.Leaves.Airborne;
                    tally.MostCreatures = Math.Max(tally.MostCreatures, sim.Creatures);
                    tally.MostLeaves = Math.Max(tally.MostLeaves, sim.Leaves.Count);
                    if (sim.Leaves.Count > view.MostLeaves) tally.Fail($"seed {seed}: {sim.Leaves.Count} leaves, over the {view.MostLeaves} allowed");
                    if (sim.Creatures > view.MostCreatures + 16) tally.Fail($"seed {seed}: {sim.Creatures} creatures, over the {view.MostCreatures} allowed");
                }

                if (frame % 6 != 0) continue;
                foreach (Critter critter in sim.Living)
                {
                    Inspect(critter, sim, view, seed, tally);
                    string name = critter.GetType().Name;
                    tally.Seen[name] = tally.Seen.GetValueOrDefault(name) + 1;
                    birds |= critter is Bird;
                    rabbits |= critter is Rabbit;
                    crabs |= critter is Crab;
                }
            }

            if (birds) tally.WorldsWithBirds++;
            if (rabbits) tally.WorldsWithRabbits++;
            if (crabs) tally.WorldsWithCrabs++;
            if (canvas.Bad) tally.Fail($"seed {seed}: a bone came out as something that isn't a number");
        }

        private static void Inspect(Critter critter, WildlifeSim sim, WildlifeView view, int seed, Tally tally)
        {
            Vector3 at = critter.Position;
            string who = $"seed {seed}: {critter.GetType().Name} ({critter.Doing}) at {at.X:0},{at.Y:0.0},{at.Z:0}";
            if (!float.IsFinite(at.X) || !float.IsFinite(at.Y) || !float.IsFinite(at.Z) || !float.IsFinite(critter.Yaw))
            {
                tally.Fail($"{who} is nowhere");
                return;
            }

            Vector2 xz = at.Xz;
            float ground = sim.Land.Ground(xz);
            float high = at.Y - ground;
            switch (critter)
            {
                case Rabbit:
                    if (high < -0.05f) tally.Fail($"{who} is {-high:0.00} m under the ground");
                    if (high > 4f) tally.Fail($"{who} is {high:0.0} m up");
                    if (!sim.Land.World.InBounds(xz) || sim.Land.World.WaterDepthAt(xz) > 0.15f) tally.Fail($"{who} is in the water");
                    if (!sim.Land.World.Passable.IsSet(xz) && !NearPassable(sim, xz)) tally.Fail($"{who} is standing in something solid");
                    break;
                case Crab:
                    if (high > 0.2f) tally.Fail($"{who} is {high:0.00} m off the ground");
                    if (sim.Land.World.WaterDepthAt(xz) > 1f) tally.Fail($"{who} is in deep water");
                    break;
                case Bird:
                    if (high < -0.1f) tally.Fail($"{who} is {-high:0.00} m under the ground");
                    if ((at - view.Camera).Length < 7f) tally.Fail($"{who} is up by the camera, {(at - view.Camera).Length:0.0} m from it");
                    break;
                case Frog:
                    if (high < -0.3f && sim.Land.World.WaterDepthAt(xz) <= 0.01f) tally.Fail($"{who} is under the ground");
                    break;
                case Butterfly:
                case Dragonfly:
                case Bat:
                    if (high < -0.05f && sim.Land.World.WaterDepthAt(xz) <= 0.01f) tally.Fail($"{who} is under the ground");
                    break;
            }
        }

        private static bool NearPassable(WildlifeSim sim, Vector2 at)
        {
            for (int i = 0; i < 8; i++)
            {
                if (sim.Land.World.Passable.IsSet(at + Turn.Heading(i * MathF.Tau / 8f) * 2f)) return true;
            }
            return false;
        }

        // Runs someone straight at each kind of thing that's on the ground: it has to have gone before they arrive, and it's the point of all of it that nobody ever treads on one.
        private static void Frightens(Tally tally)
        {
            WorldData? world = null;
            int rabbits = 0, birds = 0, crabs = 0;
            float closestRabbit = float.PositiveInfinity, closestBird = float.PositiveInfinity, closestCrab = float.PositiveInfinity;
            var canvas = new CountingCanvas();
            string caught = "", caughtCrab = "";

            for (int seed = 40; seed < 52 && (rabbits < 8 || birds < 8 || crabs < 6); seed++)
            {
                world = new WorldGenerator().Generate(new WorldRequest(seed, null));
                var sim = new WildlifeSim(world);
                Vector2 at = world.Sites.Count > 0 ? world.Sites[0].Position : new Vector2(world.Size * 0.5f);
                var random = new WorldRandom(seed).Fork("chase");

                for (int spot = 0; spot < 30; spot++)
                {
                    // Half of these are on beaches, as crabs live nowhere else and a world has few.
                    Vector2 start = new Vector2(random.Float(), random.Float()) * world.Size;
                    if (spot % 2 == 1)
                    {
                        for (int tries = 0; tries < 800 && sim.Land.HabitatAt(start) != Habitat.Beach; tries++) start = new Vector2(random.Float(), random.Float()) * world.Size;
                    }
                    if (!world.Passable.IsSet(start) && sim.Land.HabitatAt(start) != Habitat.Beach) continue;

                    sim.Forget();
                    at = start;
                    Critter? quarry = null;
                    for (int frame = 0; frame < 60 * 22; frame++)
                    {
                        WildlifeView view = NewView(at, world.Height.Sample(at), 1f);
                        view.Wind = new WindSingleton { Speed = 3f };

                        if (quarry == null || quarry.Gone)
                        {
                            quarry = Nearest(sim, at);
                            if (frame > 200 && quarry == null) break;
                        }
                        if (quarry != null)
                        {
                            Vector2 way = quarry.Position.Xz - at;
                            float length = way.Length;
                            if (length > 0.1f) at += way / length * WalkSpeed * Step;

                            // A moment for whatever settled underfoot to notice, as nothing can do better than that.
                            if (frame > 40)
                            {
                                if (quarry is Rabbit && quarry.Doing != "GoingToGround" && length < closestRabbit)
                                {
                                    closestRabbit = length;
                                    caught = $"rabbit {quarry.Doing} in seed {seed} frame {frame} at {quarry.Position.X:0},{quarry.Position.Z:0}, fright {sim.Fright(quarry.Position.Xz, 12f, out _):0.00}";
                                }
                                if (quarry is Bird bird && !bird.Aloft) closestBird = MathF.Min(closestBird, length);
                                if (quarry is Crab && quarry.Doing is not ("Digging" or "Wading") && length < closestCrab)
                                {
                                    closestCrab = length;
                                    caughtCrab = $"crab {quarry.Doing} in seed {seed} frame {frame} at {quarry.Position.X:0},{quarry.Position.Z:0}";
                                }
                            }
                        }

                        sim.Step(view, Step);
                        sim.Noises.Clear();
                        sim.Splashes.Clear();
                        sim.Contacts.Clear();
                    }

                    foreach (Critter critter in sim.Living)
                    {
                        if (critter is Rabbit) rabbits++;
                        if (critter is Bird) birds++;
                        if (critter is Crab) crabs++;
                    }
                }
            }

            tally.Reports.Add($"chased: got within {closestRabbit:0.0} m of a rabbit, {closestBird:0.0} m of a bird on the ground, {closestCrab:0.0} m of a crab");
            tally.Reports.Add($"closest: {caught}; {caughtCrab}");
            if (closestRabbit < 0.4f) tally.Fail($"something stood on a rabbit, at {closestRabbit:0.0} m");
            if (closestBird < 4f) tally.Fail($"something ran down a bird on the ground, to {closestBird:0.0} m");
            if (closestCrab < 1.2f) tally.Fail($"something trod on a crab, at {closestCrab:0.0} m");
        }

        private static Critter? Nearest(WildlifeSim sim, Vector2 at)
        {
            Critter? best = null;
            float nearest = float.PositiveInfinity;
            foreach (Critter critter in sim.Living)
            {
                if (critter is not (Rabbit or Crab or Bird)) continue;
                if (critter is Bird { Aloft: true }) continue;
                float distance = (critter.Position.Xz - at).LengthSquared;
                if (distance >= nearest) continue;
                nearest = distance;
                best = critter;
            }
            return best;
        }

        private static void Report(Tally tally, float seconds)
        {
            Console.WriteLine();
            foreach (string report in tally.Reports) Console.WriteLine("  " + report);
            Console.WriteLine();
            Console.WriteLine($"Worlds with rabbits {tally.WorldsWithRabbits}, birds {tally.WorldsWithBirds}, crabs {tally.WorldsWithCrabs}.");
            long frames = Math.Max(tally.MeasuredFrames, 1);
            Console.WriteLine($"In the view on average: {tally.CreatureFrames / (double)frames:0.0} creatures, {tally.AirborneFrames / (double)frames:0.0} leaves in the air, {tally.LeafFrames / (double)frames:0.0} in all. At most {tally.MostCreatures} and {tally.MostLeaves}.");
            Console.WriteLine($"A step takes {tally.TotalStep / frames:0.000} ms, at worst {tally.WorstStep:0.00}. Drawing {tally.TotalDraw / frames:0.000} ms, at worst {tally.WorstDraw:0.00}. It allocates {tally.Allocated / frames} bytes a frame.");
            if (tally.Allocated / frames > 1024) tally.Fail($"it allocates {tally.Allocated / frames} bytes a frame, and a collection is a dropped frame");
            if (tally.TotalStep / frames + tally.TotalDraw / frames > 0.6) tally.Fail("it costs too much for something nobody's meant to notice");
            foreach (var (name, seen) in tally.Seen.OrderByDescending(pair => pair.Value)) Console.WriteLine($"  {name,-12} {seen}");
            Console.WriteLine(tally.Failures == 0 ? "All good." : $"{tally.Failures} faults.");
        }
    }

    /// <summary>Catches what would be drawn, and makes sure it's all numbers.</summary>
    internal sealed class CountingCanvas : IWildlifeCanvas
    {
        public bool Bad;
        public long Instances;

        public void Draw(WildlifeShape shape, ReadOnlySpan<Matrix4> skin, ReadOnlySpan<Vector4> tints)
        {
            Instances += tints.Length;
            for (int i = 0; i < skin.Length; i += 7)
            {
                if (!float.IsFinite(skin[i].M41) || !float.IsFinite(skin[i].M11)) Bad = true;
            }
        }
    }
}

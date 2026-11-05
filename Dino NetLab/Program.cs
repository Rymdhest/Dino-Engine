using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Networking;

namespace Dino_NetLab
{
    /// <summary>
    /// Tests networking with no window and no real network: a server and clients in one process, joined by a simulated line, stepped
    /// by hand so that minutes pass in seconds and every run goes the same way.
    ///   core  [--seed N] [--fuzz N]                 The replication layer on its own.
    ///   game  [--seed N] [--seconds N] [--fuzz N]   The whole game: real server, real clients, every kind of line.
    ///   build [--seed N]                            Every skill, ailment, reaction and keystone, done for real on a running server.
    ///   pace  [--seed N] [--level N] [--hero N] [--minutes N] [--archetype id] [--peril N] [--teeming N] [--overrun N] [--omens a,b]   How playing goes: time in fights, kills, falls, and what's dropped.
    ///   loot  [--seed N]                            What's on the ground: who sees it, who gets it, and that nobody gets it without asking.
    ///   offering                                    What's paid at a waygate: who pays, what it does to the land, what falling costs, and what it pays out.
    /// Exit code 1 on any failure.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            // The game's assembly has to be loaded for its components to be found.
            _ = typeof(Dino_Defenders.Server.GameServer).Assembly;
            ComponentTypeRegistry.AutoRegisterAllComponents();
            NetSchema.Build();

            string command = args.Length > 0 ? args[0] : "core";
            var options = new Options(args);
            switch (command)
            {
                case "core":
                    return CoreTests.Run(options);
                case "game":
                    return GameTests.Run(options);
                case "build":
                    return BuildTests.Run(options);
                case "pace":
                    return PaceTests.Run(options);
                case "loot":
                    return LootTests.Run(options);
                case "offering":
                    return OfferingTests.Run(options);
                default:
                    Console.Error.WriteLine($"Unknown command '{command}'.");
                    return 2;
            }
        }
    }

    internal sealed class Options
    {
        private readonly Dictionary<string, string> _values = new();

        public Options(string[] args)
        {
            for (int i = 1; i + 1 < args.Length; i += 2) _values[args[i].TrimStart('-')] = args[i + 1];
        }

        public int Int(string name, int fallback) => _values.TryGetValue(name, out string? value) ? int.Parse(value) : fallback;
        public string? Text(string name) => _values.GetValueOrDefault(name);
    }

    /// <summary>The lines a game has to survive. Latencies are round trip, in milliseconds.</summary>
    internal static class Scenarios
    {
        public static readonly (string name, NetworkConditions conditions)[] All =
        {
            ("LAN", new NetworkConditions { Latency = 2, Jitter = 1 }),
            ("Good broadband", new NetworkConditions { Latency = 40, Jitter = 8, LossChance = 0.002 }),
            ("Across a continent", new NetworkConditions { Latency = 100, Jitter = 20, LossChance = 0.01 }),
            ("Across an ocean", new NetworkConditions { Latency = 180, Jitter = 30, LossChance = 0.02, DuplicateChance = 0.005 }),
            ("Bad wifi", new NetworkConditions { Latency = 60, Jitter = 90, LossChance = 0.05, DuplicateChance = 0.02, SpikeChance = 0.003, SpikeLength = 300 }),
            ("Awful", new NetworkConditions { Latency = 250, Jitter = 120, LossChance = 0.15, DuplicateChance = 0.05, SpikeChance = 0.005, SpikeLength = 500 }),
        };
    }

    /// <summary>What passed and what didn't.</summary>
    internal sealed class Report
    {
        private int _passed;
        private int _failed;

        public void Section(string name) => Console.WriteLine($"\n{name}");

        public void Check(string what, bool passed, string detail = "")
        {
            if (passed) _passed++;
            else _failed++;
            Console.WriteLine($"  {(passed ? "ok  " : "FAIL")}  {what}{(passed || detail.Length == 0 ? "" : $"  ({detail})")}");
        }

        public void Near(string what, float actual, float expected, float tolerance = 0.001f)
        {
            Check(what, MathF.Abs(actual - expected) <= tolerance * MathF.Max(1f, MathF.Abs(expected)), $"{actual} instead of {expected}");
        }

        public void Note(string text) => Console.WriteLine($"        {text}");

        public int Finish()
        {
            Console.WriteLine($"\n{_passed} passed, {_failed} failed");
            return _failed == 0 ? 0 : 1;
        }
    }
}

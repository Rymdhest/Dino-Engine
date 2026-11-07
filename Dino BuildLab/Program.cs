using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Networking;

namespace Dino_BuildLab
{
    /// <summary>
    /// Tests the build system with no window, no server and no world: the sums, the content, and how much they cost.
    ///   check [--rounds N]   Everything that can be right or wrong, then N random builds. Exit code 1 on any failure.
    ///   bench     What it costs, on this machine.
    ///   balance   How a fight goes at each level, for a few sample builds.
    ///   list [--what keystones|uniques|notables|affixes|slots|items|skills|cards|terms|sheet]   Everything a player can read.
    ///   density [--seed N] [--count N] [--level N] [--archetype id]   How much there is to fight in a world, and how long it is between fights.
    ///   loot [--level N]   What each source gives, what a whole world comes to, and the longest dry spells.
    ///   offering [--level N] [--worlds N]   What each step of peril costs and pays.
    ///   books [--level N] [--weapon sword|wand|bow]   What every sentence comes to on a plain page, and what's on the shelf comes to.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            _ = typeof(Dino_Defenders.Server.GameServer).Assembly;
            ComponentTypeRegistry.AutoRegisterAllComponents();
            NetSchema.Build();

            string command = args.Length > 0 ? args[0] : "check";
            var options = new Options(args);
            switch (command)
            {
                case "check":
                    return Checks.Run(options);
                case "bench":
                    return Bench.Run(options);
                case "balance":
                    return Balance.Run(options);
                case "list":
                    return Listing.Run(options);
                case "density":
                    return Density.Run(options);
                case "loot":
                    return LootLab.Run(options);
                case "offering":
                    return OfferingLab.Run(options);
                case "books":
                    return BookLab.Run(options);
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

    /// <summary>Every check there is, in the order the system is built up.</summary>
    internal static class Checks
    {
        public static int Run(Options options)
        {
            var report = new Report();
            StatChecks.Run(report);
            ContentChecks.Run(report);
            BookChecks.Run(report);
            TooltipChecks.Run(report);
            OfferingLab.Checks(report);
            LootLab.Check(report);
            FuzzChecks.Run(report, options.Int("rounds", 3000));
            return report.Finish();
        }
    }
}

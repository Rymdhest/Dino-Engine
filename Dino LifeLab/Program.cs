using Dino_Engine.Wildlife;

namespace Dino_LifeLab
{
    /// <summary>
    /// Looks at what's alive in a world, with no window and no GPU. See WILDLIFE.md.
    ///   check  [--seed N] [--count N] [--seconds S] [--archetype id]   Walks someone through real worlds and runs them at everything. Exit code 1 if anything goes wrong.
    ///   sheet  [--do scene] [--view side|front|top|three|game] [--frames N] [--columns N] [--tile N] [--metres M] [--out file.png]   Any body doing anything.
    ///   leaf   What each falling card does.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string command = args.Length > 0 ? args[0] : "check";
            switch (command)
            {
                case "leaf":
                    return Leaf();
                case "sheet":
                    return Sheet.Run(new Options(args));
                case "check":
                    return LifeCheck.Run(new Options(args));
                default:
                    Console.WriteLine($"No command called {command}.");
                    return 1;
            }
        }

        private static int Leaf()
        {
            foreach ((string name, FallingCard card) in new[] { ("flutter", FallingCard.Flutter), ("tumble", FallingCard.Tumble), ("mixed", FallingCard.Mixed), ("sink", FallingCard.Sink) })
            {
                float left = float.PositiveInfinity, right = float.NegativeInfinity;
                int rises = 0;
                float previousY = 0f;
                bool rising = false;
                float minAngle = float.PositiveInfinity, maxAngle = float.NegativeInfinity;
                for (float t = 80f; t < 200f; t += 0.05f)
                {
                    card.At(t, out float x, out float y, out float angle);
                    float swing = x - card.MeanDrift * t;
                    left = MathF.Min(left, swing);
                    right = MathF.Max(right, swing);
                    minAngle = MathF.Min(minAngle, angle);
                    maxAngle = MathF.Max(maxAngle, angle);
                    bool now = y > previousY;
                    if (now && !rising) rises++;
                    rising = now;
                    previousY = y;
                }
                Console.WriteLine($"{name,-8} I*={card.Inertia:0.00}  fall {card.MeanFall:0.000}  drift {card.MeanDrift:0.000}  swing {right - left:0.0}  turned {(maxAngle - minAngle) / MathF.Tau:0.00} turns  rises {rises}");
            }
            return 0;
        }
    }
}

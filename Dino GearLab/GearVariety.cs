using Dino_Engine.Modelling.Procedural.Gear;
using OpenTK.Mathematics;

namespace Dino_GearLab
{
    /// <summary>
    /// Whether things that are meant to differ really do. Two things are compared by their silhouettes, as that's what's read first and what a palette can't fake: how much of one isn't
    /// covered by the other, drawn the same way and the same size. Colour is compared apart from that. Exit code 1 if they're too much alike.
    /// </summary>
    internal static class GearVariety
    {
        private const int Size = 72;
        private const float SameShape = 0.04f;      // Below this share of their outline apart, two things look like one thing.
        private const float MostTwins = 0.002f;     // At most this share of things can have a twin.
        private const float LeastTypical = 0.1f;    // How far apart two things should be, in the middle of what's made.

        public static int Run(Options options)
        {
            int count = options.Int("count", 1500);
            int failures = 0;
            foreach (GearKind kind in Enum.GetValues<GearKind>())
            {
                if (options.Has("kind") && !string.Equals(options.Text("kind", ""), kind.ToString(), StringComparison.OrdinalIgnoreCase)) continue;
                failures += Measure(kind, count, options);
            }

            Console.WriteLine(failures == 0 ? "All passed" : $"{failures} failed");
            return failures == 0 ? 0 : 1;
        }

        private static int Measure(GearKind kind, int count, Options options)
        {
            // One base's worth of specs, as that's the hard case: a leather tunic against every other leather tunic. Differences in form only make things easier.
            var masks = new ulong[count][];
            var render = new SoftRender(Size, Size);
            Matrix3 view = Program.View(kind);
            for (int i = 0; i < count; i++)
            {
                GearSpec spec = Program.Spec(options, (ulong)i * 2862933555777941757UL + 3037000493UL, kind);
                render.Draw(GearForge.Make(spec).Build(false), view);
                masks[i] = Mask(render);
            }

            var nearest = new float[count];
            var distances = new List<float>();
            Array.Fill(nearest, 1f);
            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    float d = Distance(masks[i], masks[j]);
                    nearest[i] = MathF.Min(nearest[i], d);
                    nearest[j] = MathF.Min(nearest[j], d);
                    if ((i + j) % 7 == 0) distances.Add(d);
                }
            }

            distances.Sort();
            Array.Sort(nearest);
            float typical = distances[distances.Count / 2];
            float twins = nearest.Count(d => d < SameShape) / (float)count;
            bool fine = twins <= MostTwins && typical >= LeastTypical;
            Console.WriteLine($"  {(fine ? "ok  " : "FAIL")}  {kind,-10} of {count} with the same maker's mark: typically {typical:P0} apart, closest pair {nearest[0]:P1}, {twins:P2} have a twin");
            return fine ? 0 : 1;
        }

        private static ulong[] Mask(SoftRender render)
        {
            var mask = new ulong[(Size * Size + 63) / 64];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    if (!render.Covered(x, y)) continue;
                    int bit = y * Size + x;
                    mask[bit >> 6] |= 1ul << (bit & 63);
                }
            }
            return mask;
        }

        // One less the overlap of the two. Nothing shared is 1 and the same outline is 0.
        private static float Distance(ulong[] a, ulong[] b)
        {
            int both = 0, either = 0;
            for (int i = 0; i < a.Length; i++)
            {
                both += System.Numerics.BitOperations.PopCount(a[i] & b[i]);
                either += System.Numerics.BitOperations.PopCount(a[i] | b[i]);
            }
            return either == 0 ? 0f : 1f - (float)both / either;
        }
    }
}

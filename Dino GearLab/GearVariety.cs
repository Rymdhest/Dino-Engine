using Dino_Engine.Modelling.Procedural.Gear;
using OpenTK.Mathematics;

namespace Dino_GearLab
{
    /// <summary>
    /// Whether things that are meant to differ really do. Two things are compared by their silhouettes, as that's what's read first and what a palette can't fake, and by what's drawn inside
    /// that, as armour has to fit a body and only so much of it can show at the edge. A twin is what looks the same both ways. Exit code 1 if there are too many, or if things are too near each
    /// other in general. Shape alone is reported as well, as a kind whose variety is all colour is a kind that wants more parameters.
    /// </summary>
    internal static class GearVariety
    {
        private const int Size = 96;
        private const int Grid = 12;
        private const float SameShape = 0.04f;      // Below this share of their outline apart, two things are one shape.
        private const float SameLook = 0.05f;       // And below this much colour apart, they're the same thing.
        private const float MostTwins = 0.0005f;    // At most one pair in this many, picked at random, can look alike.
        private const float LeastTypical = 0.1f;    // How far apart two things should be, in the middle of what's made.

        public static int Run(Options options)
        {
            int count = options.Int("count", 1500);
            int failures = 0;
            foreach (GearKind kind in Enum.GetValues<GearKind>())
            {
                if (options.Has("kind") && !string.Equals(options.Text("kind", ""), kind.ToString(), StringComparison.OrdinalIgnoreCase)) continue;

                // There's only ever one of these on screen, beside the shut book it is, so what could make it a twin of another is what makes that book one: see CheckOpening.
                if (kind == GearKind.OpenBook) continue;

                failures += Measure(kind, count, options);
            }

            Console.WriteLine(failures == 0 ? "All passed" : $"{failures} failed");
            return failures == 0 ? 0 : 1;
        }

        private static int Measure(GearKind kind, int count, Options options)
        {
            // One base's worth of specs, as that's the hard case: a leather tunic against every other leather tunic. Differences in form only make things easier.
            var masks = new ulong[count][];
            var looks = new Vector3[count][];
            var render = new SoftRender(Size, Size);
            // What a player compares is the picture: laid out as an icon shows it. Gloves worn in a T are mostly the gap between them.
            GearDisplay.Angle angle = GearDisplay.PictureOf(kind);
            Matrix3 view = new Matrix3(Matrix4.CreateRotationX(angle.Rise) * Matrix4.CreateRotationY(angle.Yaw) * Matrix4.CreateRotationX(angle.Pitch) * Matrix4.CreateRotationZ(angle.Roll));
            view.Transpose();
            for (int i = 0; i < count; i++)
            {
                GearSpec spec = Program.Spec(options, (ulong)i * 2862933555777941757UL + 3037000493UL, kind);
                render.Draw(GearDisplay.Lying(GearForge.Make(spec), kind).Build(false), view);
                masks[i] = Mask(render);
                looks[i] = render.Grid(Grid);
            }

            var nearest = new float[count];
            var distances = new List<float>();
            long twinPairs = 0;
            long pairs = 0;
            Array.Fill(nearest, 1f);
            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    float d = Distance(masks[i], masks[j]);
                    nearest[i] = MathF.Min(nearest[i], d);
                    nearest[j] = MathF.Min(nearest[j], d);
                    if ((i + j) % 7 == 0) distances.Add(MathF.Max(d, Apart(looks[i], looks[j])));
                    pairs++;
                    if (d < SameShape && Apart(looks[i], looks[j]) < SameLook) twinPairs++;
                }
            }

            distances.Sort();
            Array.Sort(nearest);
            float typical = distances[distances.Count / 2];
            float twins = (float)twinPairs / pairs;
            float sameShape = nearest.Count(d => d < SameShape) / (float)count;
            bool fine = twins <= MostTwins && typical >= LeastTypical;
            string odds = twinPairs == 0 ? "no two alike" : $"one pair in {pairs / twinPairs} alike";
            Console.WriteLine($"  {(fine ? "ok  " : "FAIL")}  {kind,-10} of {count} from one base: typically {typical:P0} apart, {odds}, closest outline {nearest[0]:P1}");
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

        private static float Apart(Vector3[] a, Vector3[] b)
        {
            float sum = 0f;
            for (int i = 0; i < a.Length; i++) sum += (a[i] - b[i]).Length;
            return sum / a.Length;
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

using Dino_Engine.Rendering;
using OpenTK.Mathematics;

namespace Dino_Engine.Textures.Synthesis
{
    public enum NoiseKind
    {
        Perlin = 0,     // Soft hills and hollows.
        Value = 1,      // The same, blockier.
        Ridged = 2,     // Sharp crests where gradient noise crosses zero: veins, ridges, cracks.
        Billow = 3,     // Round lumps with creases between them: cushions, heaped earth, clouds.
    }

    /// <summary>
    /// Where a material's recipe draws (TEXTURES.md): one tile, wrapping, at the arrays' size, with the material's own <see cref="MaterialSpec"/> to make heights
    /// physical. Every generator here tiles, as every lattice in it is a whole number of cells across: a frequency is rounded to one. Every generator draws on a
    /// seed of its own, from the material's name and how many have been asked for before it, so a material comes out the same every time and on every machine.
    /// Nothing is drawn until the material is baked (<see cref="MaterialBaker"/>).
    /// </summary>
    public sealed class Canvas
    {
        public readonly MaterialSpec Spec;
        public readonly int Size;
        internal readonly Synthesizer Gpu;
        private readonly uint _seed;
        private uint _drawn;

        /// <summary>
        /// One octave of each kind of noise's mean and deviation, measured over a large tile (<see cref="MeasureNoise"/>): what standardises a sum of octaves to the
        /// same spread whatever its kind and length, so that a recipe can rely on it.
        /// </summary>
        internal static readonly (float Mean, float Deviation)[] OctaveStatistics =
        {
            (0f, 0.2154f),          // Perlin: from -0.69 to 0.70.
            (0f, 0.4503f),          // Value: from -1 to 1.
            (0.5558f, 0.2617f),     // Ridged: from 0 to 1.
            (0.1759f, 0.1243f),     // Billow: from 0 to 0.70.
        };

        internal Canvas(Synthesizer gpu, MaterialSpec spec, int size)
        {
            Gpu = gpu;
            Spec = spec;
            Size = size;
            _seed = Fnv(spec.Name);
        }

        private int NextSeed()
        {
            _drawn++;
            uint v = _seed ^ (_drawn * 0x9E3779B9u);
            v ^= v >> 16;
            v *= 0x7feb352du;
            v ^= v >> 15;
            v *= 0x846ca68bu;
            v ^= v >> 16;
            return (int)(v & 0x7fffffff);
        }

        private static uint Fnv(string text)
        {
            uint hash = 2166136261u;
            foreach (char c in text)
            {
                hash ^= c;
                hash *= 16777619u;
            }
            return hash;
        }

        private static Vector2i Whole(Vector2 frequency) => new Vector2i(Math.Max(1, (int)MathF.Round(frequency.X)), Math.Max(1, (int)MathF.Round(frequency.Y)));

        /// <summary>
        /// Fractal noise of <paramref name="octaves"/> octaves from <paramref name="frequency"/> cells across the tile, each <paramref name="lacunarity"/> times as fine
        /// and <paramref name="gain"/> times as strong as the last. It comes out with mean 0.5 and a deviation of 1/6 whatever its kind and length, nearly all within
        /// 0 to 1. <paramref name="warp"/> warps it by a slower noise first, by about that share of the tile, which is what makes it look grown.
        /// </summary>
        public Field Noise(float frequency, int octaves = 6, float gain = 0.5f, NoiseKind kind = NoiseKind.Perlin, float warp = 0f, int lacunarity = 2)
        {
            return Noise(new Vector2(frequency), octaves, gain, kind, warp, lacunarity);
        }

        public Field Noise(Vector2 frequency, int octaves = 6, float gain = 0.5f, NoiseKind kind = NoiseKind.Perlin, float warp = 0f, int lacunarity = 2)
        {
            return new Field(NoiseNode(frequency, octaves, gain, kind, warp, lacunarity, 1, ImageFormat.Scalar));
        }

        /// <summary>Two independent noises for a warp's two axes (<see cref="Field.Warp"/>).</summary>
        public Offsets Offsets(float frequency, int octaves = 4, float gain = 0.5f, float warp = 0f)
        {
            return new Offsets(NoiseNode(new Vector2(frequency), octaves, gain, NoiseKind.Perlin, warp, 2, 2, ImageFormat.Colour));
        }

        private Node NoiseNode(Vector2 frequency, int octaves, float gain, NoiseKind kind, float warp, int lacunarity, int channels, ImageFormat format)
        {
            Vector2i whole = Whole(frequency);
            Vector2i warpFrequency = new Vector2i(Math.Max(1, whole.X / 2), Math.Max(1, whole.Y / 2));
            int seed = NextSeed();
            (float mean, float deviation) = OctaveStatistics[(int)kind];
            float perlinDeviation = OctaveStatistics[(int)NoiseKind.Perlin].Deviation;
            return new PassNode(this, gpu => gpu.Noise, Array.Empty<Operand>(), shader =>
            {
                shader.loadUniformVector2i("frequency", whole);
                shader.loadUniformInt("octaves", Math.Max(octaves, 1));
                shader.loadUniformFloat("gain", gain);
                shader.loadUniformInt("lacunarity", Math.Max(lacunarity, 1));
                shader.loadUniformInt("kind", (int)kind);
                shader.loadUniformInt("seed", seed);
                shader.loadUniformFloat("mean", mean);
                shader.loadUniformFloat("deviation", deviation);
                shader.loadUniformFloat("perlinDeviation", perlinDeviation);
                shader.loadUniformFloat("warp", warp);
                shader.loadUniformVector2i("warpFrequency", warpFrequency);
                shader.loadUniformInt("channels", channels);
            }, format);
        }

        /// <summary>A Voronoi diagram of one site in each of <paramref name="frequency"/> cells, placed anywhere in their cells at a <paramref name="jitter"/> of 1.</summary>
        public Cells Cells(Vector2 frequency, float jitter = 1f) => Cells(frequency, new Vector2(jitter));

        /// <summary>
        /// A Voronoi diagram whose sites stray across and down by different shares of a cell: much across and little down keeps them in rows, which is how
        /// stones are laid in rough courses.
        /// </summary>
        public Cells Cells(Vector2 frequency, Vector2 jitter)
        {
            Vector2i whole = Whole(frequency);
            int seed = NextSeed();
            Vector2 stray = new Vector2(Math.Clamp(jitter.X, 0f, 1f), Math.Clamp(jitter.Y, 0f, 1f));
            return new Cells(new PassNode(this, gpu => gpu.Cells, Array.Empty<Operand>(), shader =>
            {
                shader.loadUniformVector2i("frequency", whole);
                shader.loadUniformVector2f("jitter", stray);
                shader.loadUniformInt("seed", seed);
            }, ImageFormat.Vector, ImageFormat.Vector));
        }

        public Cells Cells(float frequency, float jitter = 1f) => Cells(new Vector2(frequency), jitter);

        /// <summary>
        /// <paramref name="rows"/> courses of <paramref name="perRow"/> pieces each, with joints <paramref name="gap"/> of the tile wide. Each course is shifted by
        /// <paramref name="bond"/> of a piece from the last (a half for a running bond: then the courses must be even to tile) and by a random share of
        /// <paramref name="rowJitter"/> of one more. <paramref name="lengthJitter"/> makes the pieces of a course different lengths. <paramref name="upright"/> runs the
        /// courses up the tile instead of across it, as the boards of a floor or a plank wall.
        /// </summary>
        public Bricks Bricks(int rows, int perRow, float gap, float bond = 0.5f, float rowJitter = 0f, float lengthJitter = 0f, bool upright = false)
        {
            int seed = NextSeed();
            return new Bricks(new PassNode(this, gpu => gpu.Bricks, Array.Empty<Operand>(), shader =>
            {
                shader.loadUniformInt("rows", Math.Max(rows, 1));
                shader.loadUniformInt("perRow", Math.Max(perRow, 1));
                shader.loadUniformInt("axis", upright ? 1 : 0);
                shader.loadUniformFloat("bond", bond);
                shader.loadUniformFloat("rowJitter", rowJitter);
                shader.loadUniformFloat("lengthJitter", Math.Clamp(lengthJitter, 0f, 0.95f));
                shader.loadUniformFloat("gap", gap);
                shader.loadUniformInt("seed", seed);
            }, ImageFormat.Vector, ImageFormat.Vector));
        }

        /// <summary>
        /// Up to four stones in each of <paramref name="frequency"/> cells, each an ellipse of about <paramref name="radius"/> cells, standing as a dome. The higher of two
        /// overlapping shows. <paramref name="flatness"/> flattens their tops as wear does.
        /// </summary>
        public Stones Scatter(Vector2 frequency, int count = 1, float radius = 0.45f, float radiusJitter = 0.3f, float elongation = 0.5f, float heightJitter = 0.3f, float flatness = 0f, float turn = 0f, float turnJitter = 1f)
        {
            Vector2i whole = Whole(frequency);
            int seed = NextSeed();
            return new Stones(new PassNode(this, gpu => gpu.Scatter, Array.Empty<Operand>(), shader =>
            {
                shader.loadUniformVector2i("frequency", whole);
                shader.loadUniformInt("count", Math.Clamp(count, 1, 4));
                shader.loadUniformFloat("radius", radius);
                shader.loadUniformFloat("radiusJitter", radiusJitter);
                shader.loadUniformFloat("elongation", elongation);
                shader.loadUniformFloat("heightJitter", heightJitter);
                shader.loadUniformFloat("flatness", Math.Clamp(flatness, 0f, 0.95f));
                shader.loadUniformFloat("turn", turn);
                shader.loadUniformFloat("turnJitter", turnJitter);
                shader.loadUniformInt("seed", seed);
            }, ImageFormat.Vector, ImageFormat.Vector));
        }

        public Stones Scatter(float frequency, int count = 1, float radius = 0.45f, float radiusJitter = 0.3f, float elongation = 0.5f, float heightJitter = 0.3f, float flatness = 0f, float turn = 0f, float turnJitter = 1f)
        {
            return Scatter(new Vector2(frequency), count, radius, radiusJitter, elongation, heightJitter, flatness, turn, turnJitter);
        }

        private Node? _coordinates;

        /// <summary>How far across the tile each point is, 0 to 1. It jumps back to 0 at the edge, so it only tiles through something that repeats a whole number of times across, as <see cref="Field.Sine"/> with a whole frequency.</summary>
        public Field U => new Field(_coordinates ??= new PassNode(this, gpu => gpu.Coordinates, Array.Empty<Operand>(), null, ImageFormat.Vector), 0, 0);

        /// <summary>How far up the tile, as <see cref="U"/> is across.</summary>
        public Field V => new Field(_coordinates ??= new PassNode(this, gpu => gpu.Coordinates, Array.Empty<Operand>(), null, ImageFormat.Vector), 0, 1);

        /// <summary>A plain weave of <paramref name="threads"/> threads each way (even numbers, to tile), each filling <paramref name="thickness"/> of its space.</summary>
        public Weave Weave(Vector2i threads, float thickness = 0.8f, float dip = 0.4f)
        {
            int seed = NextSeed();
            Vector2i even = new Vector2i(Math.Max(2, threads.X + (threads.X & 1)), Math.Max(2, threads.Y + (threads.Y & 1)));
            return new Weave(new PassNode(this, gpu => gpu.Weave, Array.Empty<Operand>(), shader =>
            {
                shader.loadUniformVector2i("threads", even);
                shader.loadUniformFloat("thickness", thickness);
                shader.loadUniformFloat("dip", dip);
                shader.loadUniformInt("seed", seed);
            }, ImageFormat.Vector));
        }

        /// <summary>Mail of <paramref name="links"/> links across and rows down (rows made even, to tile).</summary>
        public Links Mail(Vector2i links, float ring = 0.62f, float wire = 0.16f, float lean = 0.6f)
        {
            int seed = NextSeed();
            Vector2i even = new Vector2i(Math.Max(1, links.X), Math.Max(2, links.Y + (links.Y & 1)));
            return new Links(new PassNode(this, gpu => gpu.Mail, Array.Empty<Operand>(), shader =>
            {
                shader.loadUniformVector2i("links", even);
                shader.loadUniformFloat("ring", ring);
                shader.loadUniformFloat("wire", wire);
                shader.loadUniformFloat("lean", lean);
                shader.loadUniformInt("seed", seed);
            }, ImageFormat.Vector));
        }

        /// <summary>Broken rock: a tilted flat face in each of <paramref name="frequency"/> cells and a crack between them.</summary>
        public Facets Facets(Vector2 frequency, float tilt = 0.5f, float relief = 0.6f, float crack = 0.3f, float width = 0.04f)
        {
            Vector2i whole = Whole(frequency);
            int seed = NextSeed();
            return new Facets(new PassNode(this, gpu => gpu.Facets, Array.Empty<Operand>(), shader =>
            {
                shader.loadUniformVector2i("frequency", whole);
                shader.loadUniformFloat("tilt", tilt);
                shader.loadUniformFloat("relief", relief);
                shader.loadUniformFloat("crack", crack);
                shader.loadUniformFloat("width", width);
                shader.loadUniformInt("seed", seed);
            }, ImageFormat.Vector));
        }

        /// <summary>The grain of each of a set of boards (made <paramref name="boards"/> with upright courses), with <paramref name="rings"/> growth rings across a board.</summary>
        public Grain Grain(Bricks boards, float rings = 7f, float waviness = 0.35f, float knots = 0.4f)
        {
            int seed = NextSeed();
            Operand[] operands = { new Operand(boards.Node, 0, 4), new Operand(boards.Node, 1, 4) };
            return new Grain(new PassNode(this, gpu => gpu.Grain, operands, shader =>
            {
                shader.loadUniformFloat("rings", rings);
                shader.loadUniformFloat("waviness", waviness);
                shader.loadUniformFloat("knots", knots);
                shader.loadUniformInt("seed", seed);
            }, ImageFormat.Vector));
        }

        /// <summary>A gradient map: <paramref name="t"/> read as a place along a row of colours, up to eight, each at its own place (in increasing order).</summary>
        public Paint Ramp(Field t, params (float At, Vector3 Colour)[] stops)
        {
            if (stops.Length == 0 || stops.Length > 8) throw new ArgumentException("A ramp has one to eight stops.", nameof(stops));
            if (t.IsConstant)
            {
                Vector3 colour = stops[0].Colour;
                for (int i = 1; i < stops.Length; i++)
                {
                    float share = Math.Clamp((t.Value - stops[i - 1].At) / MathF.Max(stops[i].At - stops[i - 1].At, 1e-6f), 0f, 1f);
                    colour = Vector3.Lerp(colour, stops[i].Colour, share);
                }
                return colour;
            }
            Vector4[] packed = new Vector4[8];
            for (int i = 0; i < 8; i++)
            {
                var stop = stops[Math.Min(i, stops.Length - 1)];
                packed[i] = new Vector4(stop.Colour, stop.At);
            }
            return new Paint(new PassNode(this, gpu => gpu.Ramp, new[] { t.Operand }, shader =>
            {
                shader.loadUniformInt("stopCount", stops.Length);
                shader.loadUniformVector4fArray("stops", packed);
            }, ImageFormat.Colour));
        }

        /// <summary>
        /// One octave of each kind of noise, over a tile of many cells, measured: what <see cref="OctaveStatistics"/> holds. Run once whenever the noise changes, as
        /// --texturecheck does, and copy what it prints.
        /// </summary>
        internal static string MeasureNoise(Synthesizer gpu, MaterialSpec spec, int size)
        {
            var lines = new System.Text.StringBuilder();
            foreach (NoiseKind kind in Enum.GetValues<NoiseKind>())
            {
                var canvas = new Canvas(gpu, spec, size);
                Vector2i frequency = new Vector2i(128);
                Node node = new PassNode(canvas, g => g.Noise, Array.Empty<Operand>(), shader =>
                {
                    shader.loadUniformVector2i("frequency", frequency);
                    shader.loadUniformInt("octaves", 1);
                    shader.loadUniformFloat("gain", 0.5f);
                    shader.loadUniformInt("lacunarity", 2);
                    shader.loadUniformInt("kind", (int)kind);
                    shader.loadUniformInt("seed", 12345);
                    shader.loadUniformFloat("mean", 0f);
                    shader.loadUniformFloat("deviation", 1f);
                    shader.loadUniformFloat("perlinDeviation", 1f);
                    shader.loadUniformFloat("warp", 0f);
                    shader.loadUniformVector2i("warpFrequency", new Vector2i(1));
                    shader.loadUniformInt("channels", 1);
                }, ImageFormat.Scalar);
                var evaluation = new Evaluation(gpu, new[] { node });
                Image image = evaluation.Get(node)[0];
                FieldStatistics s = gpu.Measure(image, 0);
                evaluation.Finish();
                // The shader wrote 0.5 + n / 6.
                lines.AppendLine($"{kind}: mean {(s.Mean - 0.5f) * 6f:0.0000}, deviation {s.Deviation * 6f:0.0000}, range {(s.Least - 0.5f) * 6f:0.000} to {(s.Most - 0.5f) * 6f:0.000}");
            }
            lines.Append(CheckFields(gpu, spec, size));
            return lines.ToString();
        }

        /// <summary>
        /// What a recipe relies on, measured: noise standardised whatever its kind, Equalize spreading a field evenly, a threshold after it covering just its share
        /// of the tile, Normalize spanning 0 to 1, and a blur and a warp keeping a field's mean. Each line says what it should be and what it is, and FAILS where
        /// they're apart. NaN is what isn't checked.
        /// </summary>
        internal static string CheckFields(Synthesizer gpu, MaterialSpec spec, int size)
        {
            var canvas = new Canvas(gpu, spec, size);
            // Fine enough that a tile holds hundreds of features, or a mean drifts by chance as far as the tolerance.
            Field noise = canvas.Noise(16f, octaves: 5);
            Field even = noise.Equalize();
            const float No = float.NaN;
            var checks = new (string Name, Field Field, float Mean, float Deviation, float Least, float Most)[]
            {
                ("Perlin noise", noise, 0.5f, 1f / 6f, No, No),
                ("ridged noise", canvas.Noise(16f, octaves: 5, kind: NoiseKind.Ridged), 0.5f, 1f / 6f, No, No),
                ("billow noise", canvas.Noise(16f, octaves: 5, kind: NoiseKind.Billow), 0.5f, 1f / 6f, No, No),
                ("equalized", even, 0.5f, 1f / MathF.Sqrt(12f), 0f, 1f),
                ("over 0.7 of it", even.Threshold(0.7f), 0.3f, MathF.Sqrt(0.21f), 0f, 1f),
                ("patches of 0.45", canvas.Noise(3f, octaves: 5, warp: 0.1f).Equalize().Threshold(0.55f, 0.15f), 0.45f, No, 0f, 1f),
                ("normalized", canvas.Noise(5f, octaves: 4).Normalize(), No, No, 0f, 1f),
                ("blurred", noise.Blur(0.03f), 0.5f, No, No, No),
                ("warped", noise.Warp(canvas.Offsets(4f), 0.05f), 0.5f, No, No, No),
            };
            var lines = new System.Text.StringBuilder();
            foreach ((string name, Field field, float mean, float deviation, float least, float most) in checks)
            {
                Operand operand = field.Operand;
                var evaluation = new Evaluation(gpu, new[] { operand.Node! });
                Image image = evaluation.Get(operand.Node!)[operand.Output];
                FieldStatistics s = gpu.Measure(image, operand.Channel >= 4 ? 0 : operand.Channel);
                evaluation.Finish();
                static bool Near(float measured, float wanted, float within) => float.IsNaN(wanted) || MathF.Abs(measured - wanted) < within;
                bool good = Near(s.Mean, mean, 0.02f) && Near(s.Deviation, deviation, 0.02f) && Near(s.Least, least, 0.01f) && Near(s.Most, most, 0.01f);
                lines.AppendLine($"{(good ? "ok" : "FAILS")}: {name}, mean {s.Mean:0.000}{Of(mean)}, deviation {s.Deviation:0.000}{Of(deviation)}, from {s.Least:0.000}{Of(least)} to {s.Most:0.000}{Of(most)}");
            }
            lines.Append(CheckSeams(gpu, spec, size));
            return lines.ToString();

            static string Of(float wanted) => float.IsNaN(wanted) ? "" : $" of {wanted:0.000}";
        }

        /// <summary>
        /// Every generator and neighbourhood operation read back and measured across the tile's edges: the step from the last column to the first, and from the last
        /// row to the first, against the average step between neighbours anywhere else. Seamless, they're the same; a seam is a step several times as big.
        /// </summary>
        internal static string CheckSeams(Synthesizer gpu, MaterialSpec spec, int size)
        {
            var canvas = new Canvas(gpu, spec, size);
            Field noise = canvas.Noise(new Vector2(5f, 3f), octaves: 6);
            Cells cells = canvas.Cells(new Vector2(7f, 5f), jitter: 0.9f);
            Stones stones = canvas.Scatter(new Vector2(6f, 9f), count: 2, radius: 0.4f, radiusJitter: 0.6f, elongation: 2f);
            Bricks bricks = canvas.Bricks(rows: 6, perRow: 4, gap: 0.01f, bond: 0.5f, rowJitter: 0.5f, lengthJitter: 0.4f);
            var checks = new (string Name, Field Field)[]
            {
                ("noise", noise),
                ("warped noise", canvas.Noise(new Vector2(3f, 2f), octaves: 5, warp: 0.1f)),
                ("ridged warped noise", canvas.Noise(new Vector2(3f, 12f), octaves: 4, kind: NoiseKind.Ridged, warp: 0.06f)),
                ("cell border", cells.Border),
                ("cell centre", cells.Centre),
                ("stones", stones.Height),
                ("long stones", canvas.Scatter(9f, count: 2, radius: 0.45f, radiusJitter: 0.4f, elongation: 12f).Height),
                ("bricks", bricks.Edge),
                ("facets", canvas.Facets(new Vector2(3f, 6f)).Height),
                ("weave", canvas.Weave(new Vector2i(12, 12)).Height),
                ("warp", noise.Warp(canvas.Offsets(6f), 0.05f)),
                ("blur", cells.Border.Blur(0.01f)),
                ("erode", cells.Border.Erode(0.004f)),
                ("occlusion", noise.Occlusion(0.05f)),
                ("u", (canvas.U * 3f).Sine()),
            };
            var lines = new System.Text.StringBuilder();
            foreach ((string name, Field field) in checks)
            {
                Operand operand = field.Operand;
                var evaluation = new Evaluation(gpu, new[] { operand.Node! });
                Image image = evaluation.Get(operand.Node!)[operand.Output];
                float[] texels = gpu.Read(image);
                evaluation.Finish();
                int channel = operand.Channel >= 4 ? 0 : operand.Channel;
                int stride = image.Format == ImageFormat.Scalar ? 1 : 4;
                int n = image.Size;
                float At(int x, int y) => texels[(y * n + x) * stride + channel];
                double insideU = 0, insideV = 0, edgeU = 0, edgeV = 0;
                for (int y = 0; y < n; y++)
                {
                    for (int x = 1; x < n; x++) insideU += MathF.Abs(At(x, y) - At(x - 1, y));
                    edgeU += MathF.Abs(At(0, y) - At(n - 1, y));
                }
                for (int x = 0; x < n; x++)
                {
                    for (int y = 1; y < n; y++) insideV += MathF.Abs(At(x, y) - At(x, y - 1));
                    edgeV += MathF.Abs(At(x, 0) - At(x, n - 1));
                }
                double ratioU = (edgeU / n) / Math.Max(insideU / (n * (n - 1.0)), 1e-12);
                double ratioV = (edgeV / n) / Math.Max(insideV / (n * (n - 1.0)), 1e-12);
                bool good = ratioU < 1.6 && ratioV < 1.6;
                lines.AppendLine($"{(good ? "ok" : "FAILS")}: seams of {name}, across u {ratioU:0.00} and v {ratioV:0.00} times an inside step");
            }
            return lines.ToString();
        }
    }

    /// <summary>Colours as a recipe gives them: the engine's albedo is linear, and these are its numbers out of 255.</summary>
    public static class Colours
    {
        public static Vector3 Rgb(int red, int green, int blue) => new Vector3(red, green, blue) / 255f;

        public static Vector3 Grey(int value) => new Vector3(value) / 255f;
    }
}

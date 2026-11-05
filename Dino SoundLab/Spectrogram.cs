using Dino_Engine.Sound.Content;
using Dino_Engine.Sound.Dsp;
using Dino_Engine.Sound.Synthesis;
using Dino_Engine.Util;

namespace Dino_SoundLab
{
    /// <summary>
    /// Sound as a picture, which is how a sound can be judged by something with no ears. A spectrogram shows at a glance what a meter can't:
    /// a synthetic impact is clean lines on black, a real one is a dense field that starts with a line straight up the whole height.
    /// </summary>
    internal static class Spectrogram
    {
        private const int Window = 1024;
        private const float Floor = -84f;
        private const int LowestHz = 40;

        /// <summary>Draws a tile for every sound into one big picture, each labelled.</summary>
        public static int Sheet(Options options)
        {
            string path = options.Text("out") ?? "sounds.png";
            string? only = options.Text("only");
            int columns = options.Int("columns", 10);
            const int Width = 180;
            const int Height = 110;
            const int Label = 12;

            List<SoundDefinition> sounds = SoundCatalog.All.Where(d => only == null || d.Id.Contains(only)).ToList();
            int rows = (sounds.Count + columns - 1) / columns;
            int sheetWidth = columns * Width;
            int sheetHeight = rows * (Height + Label);
            var pixels = new byte[sheetWidth * sheetHeight * 4];
            for (int i = 3; i < pixels.Length; i += 4) pixels[i] = 255;

            var fft = new Fft(Window);
            for (int s = 0; s < sounds.Count; s++)
            {
                SoundBuffer take = sounds[s].Render(77);
                int x = (s % columns) * Width;
                int y = (s / columns) * (Height + Label);
                Draw(take, fft, pixels, sheetWidth, x, y + Label, Width - 2, Height - 2);
                TinyFont.Write(pixels, sheetWidth, x + 2, y + 2, sounds[s].Id.ToUpperInvariant(), Width - 4);
            }

            PngWriter.Write(path, pixels, sheetWidth, sheetHeight);
            Console.WriteLine($"{sounds.Count} sounds to {path}");
            return 0;
        }

        // Time left to right over the length of the sound, pitch up the side on a log scale, as it's heard.
        private static void Draw(SoundBuffer take, Fft fft, byte[] pixels, int stride, int left, int top, int width, int height)
        {
            var real = new float[Window];
            var imaginary = new float[Window];
            var column = new float[height];
            float nyquistBin = Window / 2f;

            for (int x = 0; x < width; x++)
            {
                int centre = (int)((long)take.Length * x / width);
                for (int i = 0; i < Window; i++)
                {
                    int index = centre + i - Window / 2;
                    float value = (uint)index < (uint)take.Length ? take.Left[index] : 0f;
                    float hann = 0.5f - 0.5f * MathF.Cos(MathF.Tau * i / Window);
                    real[i] = value * hann;
                    imaginary[i] = 0f;
                }
                fft.Transform(real, imaginary);

                for (int y = 0; y < height; y++)
                {
                    float along = 1f - y / (float)height;
                    float hz = LowestHz * MathF.Pow(AudioFormat.Nyquist / LowestHz, along);
                    int bin = Math.Min((int)(hz / AudioFormat.Nyquist * nyquistBin), (int)nyquistBin - 1);
                    float power = real[bin] * real[bin] + imaginary[bin] * imaginary[bin];
                    column[y] = 10f * MathF.Log10(MathF.Max(power, 1e-14f)) - 24f;
                }

                for (int y = 0; y < height; y++)
                {
                    float level = Math.Clamp((column[y] - Floor) / -Floor, 0f, 1f);
                    int at = ((top + y) * stride + left + x) * 4;
                    (pixels[at], pixels[at + 1], pixels[at + 2]) = Heat(level);
                }
            }
        }

        private static (byte, byte, byte) Heat(float level)
        {
            float r = Math.Clamp(level * 2.2f - 0.25f, 0f, 1f);
            float g = Math.Clamp(level * 2.4f - 1.1f, 0f, 1f);
            float b = Math.Clamp(MathF.Sin(level * MathF.PI * 1.1f) * 0.55f + (level > 0.85f ? (level - 0.85f) * 4f : 0f), 0f, 1f);
            return ((byte)(r * 255f), (byte)(g * 255f), (byte)(b * 255f));
        }
    }

    /// <summary>Just enough of a font to label pictures with no graphics library to hand: 3 by 5 pixels, capitals and digits.</summary>
    internal static class TinyFont
    {
        private static readonly Dictionary<char, string> Glyphs = new()
        {
            ['A'] = "010101111101101", ['B'] = "110101110101110", ['C'] = "011100100100011", ['D'] = "110101101101110",
            ['E'] = "111100110100111", ['F'] = "111100110100100", ['G'] = "011100101101011", ['H'] = "101101111101101",
            ['I'] = "111010010010111", ['J'] = "001001001101010", ['K'] = "101101110101101", ['L'] = "100100100100111",
            ['M'] = "101111111101101", ['N'] = "110101101101101", ['O'] = "010101101101010", ['P'] = "110101110100100",
            ['Q'] = "010101101110011", ['R'] = "110101110101101", ['S'] = "011100010001110", ['T'] = "111010010010010",
            ['U'] = "101101101101111", ['V'] = "101101101101010", ['W'] = "101101111111101", ['X'] = "101101010101101",
            ['Y'] = "101101010010010", ['Z'] = "111001010100111", ['0'] = "111101101101111", ['1'] = "010110010010111",
            ['2'] = "110001010100111", ['3'] = "110001010001110", ['4'] = "101101111001001", ['5'] = "111100110001110",
            ['6'] = "011100111101111", ['7'] = "111001010010010", ['8'] = "111101111101111", ['9'] = "111101111001110",
            ['.'] = "000000000000010", ['_'] = "000000000000111",
        };

        public static void Write(byte[] pixels, int stride, int x, int y, string text, int width)
        {
            int cursor = x;
            foreach (char c in text)
            {
                if (cursor + 4 > x + width) break;
                if (Glyphs.TryGetValue(c, out string? glyph))
                {
                    for (int i = 0; i < 15; i++)
                    {
                        if (glyph[i] != '1') continue;
                        int at = ((y + i / 3) * stride + cursor + i % 3) * 4;
                        pixels[at] = pixels[at + 1] = pixels[at + 2] = 235;
                    }
                }
                cursor += 4;
            }
        }
    }
}

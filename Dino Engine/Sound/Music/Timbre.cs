using Dino_Engine.Sound.Dsp;

namespace Dino_Engine.Sound.Music
{
    /// <summary>
    /// The sound of a sustained instrument, as a set of single cycles to read from: one for each few semitones of its range, at each of several loudnesses.
    /// A real instrument isn't one wave turned up and down. Measured, a horn's eighth harmonic is forty decibels stronger at fortissimo than at pianissimo,
    /// and its fundamental hardly changes, so loudness here morphs between spectra, which is what makes a played note sound played.
    /// </summary>
    public sealed class Timbre
    {
        public const int Length = 2048;
        private const int Mask = Length - 1;
        private const int Layers = 3;                   // Pianissimo, mezzo forte, fortissimo.
        private const float Spacing = 5f;               // Semitones between tables.
        private const float MostFrequency = 19000f;

        private readonly float[][][] _tables;           // [region][layer][sample]
        private readonly float _lowestNote;

        private Timbre(float[][][] tables, float lowestNote)
        {
            _tables = tables;
            _lowestNote = lowestNote;
        }

        /// <summary>What a harmonic at this frequency gains over its mezzo forte level going to fortissimo, and loses the other way, by family.</summary>
        public delegate float Brightening(float frequency);

        /// <summary>
        /// Builds a timbre from measured spectra. Each table holds only harmonics the top of its pitch range can carry below the Nyquist limit, as anything
        /// above would alias, and is built by inverse FFT with fixed random phases, so the waves aren't all peaks at one point.
        /// </summary>
        public static Timbre Build(RegisterSpectrum[] registers, Brightening brightening, float lowestNote, float highestNote, ulong seed)
        {
            int regions = (int)((highestNote - lowestNote) / Spacing) + 2;
            var tables = new float[regions][][];
            var random = new SoundRandom(seed);
            var phases = new float[Length / 2];
            for (int i = 0; i < phases.Length; i++) phases[i] = random.Float() * MathF.Tau;

            var fft = new Fft(Length);
            var real = new float[Length];
            var imaginary = new float[Length];

            for (int region = 0; region < regions; region++)
            {
                float note = lowestNote + region * Spacing;
                float hertz = 440f * MathF.Pow(2f, (note - 69f) / 12f);
                float top = hertz * MathF.Pow(2f, (Spacing + 1f) / 12f);
                int harmonics = Math.Min((int)(MostFrequency / top), Length / 2 - 1);

                tables[region] = new float[Layers][];
                for (int layer = 0; layer < Layers; layer++)
                {
                    float loudness = layer - 1f;    // -1, 0, 1.
                    Array.Clear(real);
                    Array.Clear(imaginary);
                    for (int h = 1; h <= harmonics; h++)
                    {
                        float decibels = Level(registers, hertz, h) + brightening(hertz * h) * loudness;
                        float amplitude = AudioFormat.DecibelsToGain(decibels);
                        real[h] = amplitude * MathF.Cos(phases[h]);
                        imaginary[h] = amplitude * MathF.Sin(phases[h]);
                        real[Length - h] = real[h];
                        imaginary[Length - h] = -imaginary[h];
                    }
                    fft.Transform(real, imaginary, inverse: true);

                    // Every table of a timbre peaks at one, so that loudness comes from the player and not from the spectrum. The player knows how much a horn gains by being blown hard.
                    var table = new float[Length + 1];
                    float peak = 1e-9f;
                    for (int i = 0; i < Length; i++) peak = MathF.Max(peak, MathF.Abs(real[i]));
                    for (int i = 0; i < Length; i++) table[i] = real[i] / peak;
                    table[Length] = table[0];
                    tables[region][layer] = table;
                }
            }
            return new Timbre(tables, lowestNote);
        }

        // Interpolated in log frequency between the registers either side, as a spectrum changes smoothly up an instrument.
        private static float Level(RegisterSpectrum[] registers, float hertz, int harmonic)
        {
            int index = harmonic - 1;
            if (hertz <= registers[0].Hertz) return At(registers[0], index);
            for (int r = 1; r < registers.Length; r++)
            {
                if (hertz > registers[r].Hertz) continue;
                float along = MathF.Log(hertz / registers[r - 1].Hertz) / MathF.Log(registers[r].Hertz / registers[r - 1].Hertz);
                return At(registers[r - 1], index) * (1f - along) + At(registers[r], index) * along;
            }
            return At(registers[^1], index);
        }

        // Past the end of the table it goes on falling, by the octave as real spectra do. By the harmonic, a low note would run out of top long before a high one.
        private static float At(in RegisterSpectrum register, int index)
        {
            sbyte[] harmonics = register.Harmonics;
            if (index < harmonics.Length) return harmonics[index];
            return harmonics[^1] - 18f * MathF.Log2((index + 1f) / harmonics.Length);
        }

        /// <summary>Builds a timbre from a formula for each harmonic, for what's better described than measured: voices.</summary>
        public static Timbre Build(Func<float, int, float> decibels, float lowestNote, float highestNote, ulong seed)
        {
            int regions = (int)((highestNote - lowestNote) / Spacing) + 2;
            var registers = new RegisterSpectrum[regions];
            for (int region = 0; region < regions; region++)
            {
                float hertz = 440f * MathF.Pow(2f, (lowestNote + region * Spacing - 69f) / 12f);
                var harmonics = new sbyte[64];
                for (int h = 1; h <= harmonics.Length; h++) harmonics[h - 1] = (sbyte)Math.Clamp(MathF.Round(decibels(hertz, h)), -127f, 0f);
                registers[region] = new RegisterSpectrum(hertz, harmonics);
            }
            return Build(registers, f => Math.Clamp(5f * MathF.Log2(f / 500f), 0f, 9f), lowestNote, highestNote, seed);
        }

        /// <summary>The table for a note, and how far between the two loudness layers it's being played. <paramref name="loudness"/> is 0 at pianissimo and 1 at fortissimo.</summary>
        public void Select(float note, float loudness, out float[] quiet, out float[] loud, out float blend)
        {
            int region = Math.Clamp((int)((note - _lowestNote) / Spacing + 0.5f), 0, _tables.Length - 1);
            float position = Math.Clamp(loudness, 0f, 1f) * (Layers - 1);
            int layer = Math.Min((int)position, Layers - 2);
            quiet = _tables[region][layer];
            loud = _tables[region][layer + 1];
            blend = position - layer;
        }

        public static float Read(float[] table, float phase)
        {
            float position = phase * Length;
            int whole = (int)position;
            float fraction = position - whole;
            float a = table[whole & Mask];
            return a + (table[(whole & Mask) + 1] - a) * fraction;
        }

        /// <summary>
        /// How loudness brightens each family, from recordings at each dynamic. It goes by frequency and not by harmonic, as what brightens brass is the sound wave steepening on its
        /// way down the tube, and that acts on what's high. Measured from pp to ff at C4 to B4, a horn gained 7 dB at 520 Hz and 40 dB at 2 kHz: this is half of that, as it's applied
        /// either way from the middle, and fits it to within a decibel or two. A flute gains far less, and evenly.
        /// </summary>
        public static float BrassBrightening(float frequency) => Math.Clamp(3f + 7.5f * MathF.Log2(frequency / 450f), 0f, 21f);
        public static float WoodwindBrightening(float frequency) => Math.Clamp(5f + 2.2f * MathF.Log2(frequency / 500f), 0f, 11f);
        public static float StringBrightening(float frequency) => Math.Clamp(3f + 5f * MathF.Log2(frequency / 500f), 0f, 15f);
    }
}

using Dino_Engine.Sound.Content;
using Dino_Engine.Sound.Dsp;
using Dino_Engine.Sound.Mixing;
using Dino_Engine.Sound.Synthesis;
using OpenTK.Mathematics;
using System.Diagnostics;

namespace Dino_SoundLab
{
    /// <summary>
    /// Everything that can be known about a sound without ears. Nobody can tell from this whether a footstep sounds good, but a sound that fails here is
    /// wrong for certain: it clicks, clips, never ends, repeats itself or costs too much. Run it after any change to a sound or to the mixer.
    /// </summary>
    internal static class SoundCheck
    {
        private const int TakesChecked = 4;
        private const float CeilingAllowance = 0.15f;       // Decibels.
        private const float LoudnessAllowance = 2.5f;       // Decibels from the target.
        private const float EdgeLimit = 0.012f;
        private const float Longest = 30f;                  // Seconds.

        private static int _failures;
        private static int _checks;

        private static void Fail(string what)
        {
            _failures++;
            Console.WriteLine($"  FAIL {what}");
        }

        private static void Expect(bool condition, string what)
        {
            _checks++;
            if (!condition) Fail(what);
        }

        public static int Run(Options options)
        {
            var clock = Stopwatch.StartNew();
            string? only = options.Text("only");

            Console.WriteLine("Sounds");
            CheckSounds(only);
            Console.WriteLine("Live sounds");
            CheckLive(only);
            if (only == null)
            {
                Console.WriteLine("The land");
                CheckWind();
                CheckRain();
                CheckDrips();
                CheckGrounds();
                CheckWater();
                Console.WriteLine("Loot");
                CheckLoot();
                Console.WriteLine("Side by side");
                CheckSideBySide();
                Console.WriteLine("DSP");
                CheckDsp();
                Console.WriteLine("Mixer");
                MixerCheck.Run(Expect);
            }

            Console.WriteLine($"{_checks} checks, {_failures} failed, in {clock.Elapsed.TotalSeconds:0.0} s.");
            return _failures == 0 ? 0 : 1;
        }

        private static void CheckSounds(string? only)
        {
            var ids = new HashSet<string>();
            var slowest = (id: "", seconds: 0.0);
            foreach (SoundDefinition definition in SoundCatalog.All)
            {
                if (only != null && !definition.Id.Contains(only)) continue;
                Expect(ids.Add(definition.Id), $"{definition.Id}: the id is used twice");

                var takes = new SoundBuffer[TakesChecked];
                for (int i = 0; i < TakesChecked; i++)
                {
                    var clock = Stopwatch.StartNew();
                    takes[i] = definition.Render(1000UL + (ulong)i);
                    if (clock.Elapsed.TotalSeconds > slowest.seconds) slowest = (definition.Id, clock.Elapsed.TotalSeconds);
                    CheckTake(definition, takes[i], i);
                }

                Expect(Same(definition.Render(1000UL), takes[0]), $"{definition.Id}: the same seed gave a different take");
                CheckVariation(definition, takes);
            }
            Console.WriteLine($"  slowest take: {slowest.id}, {slowest.seconds * 1000.0:0} ms");
        }

        private static void CheckTake(SoundDefinition definition, SoundBuffer take, int index)
        {
            string name = $"{definition.Id}#{index}";
            ReadOnlySpan<float> left = take.Left.AsSpan(0, take.Length);
            ReadOnlySpan<float> right = take.Right != null ? take.Right.AsSpan(0, take.Length) : default;

            Expect(take.Seconds is > 0.008f and < Longest, $"{name}: is {take.Seconds:0.000} s long");
            Expect(definition.Stereo == take.IsStereo, $"{name}: stereo is not as defined");

            bool finite = true;
            float peak = 0f;
            double sum = 0.0;
            for (int i = 0; i < left.Length; i++)
            {
                float value = left[i];
                float other = right.IsEmpty ? value : right[i];
                finite &= float.IsFinite(value) && float.IsFinite(other);
                peak = MathF.Max(peak, MathF.Max(MathF.Abs(value), MathF.Abs(other)));
                sum += value + other;
            }
            Expect(finite, $"{name}: has NaN or infinity in it");
            if (!finite) return;

            float ceiling = definition.Mastering.Ceiling;
            float peakDecibels = AudioFormat.GainToDecibels(peak);
            Expect(peakDecibels <= ceiling + CeilingAllowance, $"{name}: peak {peakDecibels:0.00} dB is over the ceiling of {ceiling:0.0}");
            Expect(peak > 0.003f, $"{name}: is silent");

            float mean = (float)(sum / (left.Length * 2.0));
            Expect(MathF.Abs(mean) < 0.004f, $"{name}: has {mean:0.0000} of DC");

            float start = MathF.Max(MathF.Abs(left[0]), right.IsEmpty ? 0f : MathF.Abs(right[0]));
            float end = MathF.Max(MathF.Abs(left[^1]), right.IsEmpty ? 0f : MathF.Abs(right[^1]));
            Expect(start < EdgeLimit, $"{name}: starts with a click ({start:0.000})");
            Expect(end < EdgeLimit, $"{name}: ends with a click ({end:0.000})");

            if (definition.Mastering.Target is float target)
            {
                float loudness = definition.Mastering.Sustained ? Loudness.Integrated(left, right) : Loudness.Peak(left, right);

                // Being limited makes it quieter than asked, which is the limiter doing its job on a very peaky sound. Louder is never right.
                Expect(loudness <= target + LoudnessAllowance, $"{name}: {loudness:0.0} LUFS is over its target of {target:0.0}");
                Expect(loudness >= target - 12f, $"{name}: {loudness:0.0} LUFS is far under its target of {target:0.0}");
            }
        }

        private static bool Same(SoundBuffer a, SoundBuffer b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a.Left[i] != b.Left[i]) return false;
            }
            return true;
        }

        // Variation is the point of all this. Two takes as alike as the same sound played twice would be heard as a machine-gun effect.
        private static void CheckVariation(SoundDefinition definition, SoundBuffer[] takes)
        {
            float closest = 0f;
            for (int a = 0; a < takes.Length; a++)
            {
                for (int b = a + 1; b < takes.Length; b++) closest = MathF.Max(closest, Likeness(takes[a], takes[b]));
            }
            Expect(closest < 0.97f, $"{definition.Id}: takes are too alike ({closest:0.000})");
        }

        // The highest correlation between the two over small shifts.
        private static float Likeness(SoundBuffer a, SoundBuffer b)
        {
            int length = Math.Min(Math.Min(a.Length, b.Length), AudioFormat.SampleRate / 2);
            float best = 0f;
            for (int shift = -48; shift <= 48; shift += 4)
            {
                double dot = 0.0, energyA = 0.0, energyB = 0.0;
                for (int i = Math.Max(0, -shift); i < length - Math.Max(shift, 0); i++)
                {
                    float x = a.Left[i];
                    float y = b.Left[i + shift];
                    dot += x * y;
                    energyA += x * x;
                    energyB += y * y;
                }
                best = MathF.Max(best, MathF.Abs((float)(dot / Math.Sqrt(Math.Max(energyA * energyB, 1e-18)))));
            }
            return best;
        }

        private static void CheckLive(string? only)
        {
            foreach (LiveDefinition definition in SoundCatalog.AllLive)
            {
                if (only != null && !definition.Id.Contains(only)) continue;
                string name = definition.Id;

                LiveSound sound = definition.Create(11);
                for (int i = 0; i < definition.Defaults.Length; i++) sound.SetTarget(i, definition.Defaults[i]);
                (float[] left, float[] right, double cost) = Run(sound, 12f);

                bool finite = left.All(float.IsFinite) && right.All(float.IsFinite);
                Expect(finite, $"{name}: has NaN or infinity in it");
                if (!finite) continue;

                float peak = 0f;
                foreach (float value in left) peak = MathF.Max(peak, MathF.Abs(value));
                foreach (float value in right) peak = MathF.Max(peak, MathF.Abs(value));
                Expect(peak > 0.002f, $"{name}: is silent");
                Expect(cost < 0.04, $"{name}: takes {cost * 100.0:0.00}% of a core");
                Expect(AudioFormat.GainToDecibels(peak) < 6f, $"{name}: peaks at {AudioFormat.GainToDecibels(peak):0.0} dBFS");

                // A thing at a place is placed by the mixer. What's all around has to be wide of itself.
                double correlation = Correlation(left, right);
                if (!definition.Positional) Expect(MathF.Abs((float)correlation) < 0.97f, $"{name}: left and right are nearly the same ({correlation:0.00}), so it won't surround anyone");

                // Parameters at nothing and left a long while: where a tail decays into denormals, this gets very slow.
                for (int i = 0; i < definition.Defaults.Length; i++) sound.SetTarget(i, 0f);
                (float[] quietLeft, _, double quietCost) = Run(sound, 40f);
                Expect(quietCost < 0.05, $"{name}: takes {quietCost * 100.0:0.00}% of a core going quiet");
                Expect(quietLeft.All(float.IsFinite), $"{name}: goes bad when it's all turned down");

                // And swung about at random, as a game would, it must stay in one piece.
                var random = new SoundRandom(3);
                var block = new float[240];
                var blockRight = new float[240];
                bool stable = true;
                for (int step = 0; step < 3000; step++)
                {
                    if (step % 20 == 0)
                    {
                        for (int i = 0; i < definition.Defaults.Length; i++) sound.SetTarget(i, random.Range(0f, MathF.Max(definition.Defaults[i], 1f) * 2f));
                    }
                    sound.Render(block, blockRight);
                    foreach (float value in block) stable &= float.IsFinite(value) && MathF.Abs(value) < 64f;
                }
                Expect(stable, $"{name}: comes apart when its parameters are thrown about");
            }
        }

        private static (float[] left, float[] right) Heard(LiveDefinition definition, float seconds, params float[] parameters)
        {
            LiveSound sound = definition.Create(11);
            for (int i = 0; i < parameters.Length; i++) sound.SetTarget(i, parameters[i]);
            (float[] left, float[] right, _) = Run(sound, seconds);
            return (left, right);
        }

        // Decibels in the octave around a frequency.
        private static float Octave(ReadOnlySpan<float> samples, float hertz)
        {
            Svf filter = Svf.Create(hertz, 1.4f);
            double energy = 0.0;
            foreach (float sample in samples)
            {
                float band = filter.BandpassNormal(sample);
                energy += band * band;
            }
            return AudioFormat.GainToDecibels(MathF.Sqrt((float)(energy / Math.Max(samples.Length, 1))) + 1e-7f);
        }

        // What the player sees has to be what they hear: wind that's barely stirring the grass is barely there, it grows steadily with what's on screen, and it's the sound of plants and
        // moving air and not of a noise generator. Real wind in a wood falls away above 2 kHz, and left bright, a breeze hisses at any level.
        private static void CheckWind()
        {
            float[] speeds = { 1f, 3f, 5f, 8f, 12f, 16f };
            var levels = new float[speeds.Length];
            float gain = AudioFormat.GainToDecibels(Soundscape.Wind.Gain);
            for (int i = 0; i < speeds.Length; i++)
            {
                (float[] left, float[] right) = Heard(Soundscape.Wind, 8f, speeds[i], 0.5f, 0.2f, 0.6f, 0.5f, 0.4f);
                levels[i] = Loudness.Integrated(left, right) + gain;
                if (i > 0) Expect(levels[i] > levels[i - 1] + 2f, $"wind: no louder at {speeds[i]} m/s than at {speeds[i - 1]} ({levels[i]:0.0} and {levels[i - 1]:0.0} LUFS)");

                float air = Octave(left, 1000f);
                Expect(Octave(left, 8000f) < air - 10f, $"wind: hisses at {speeds[i]} m/s ({Octave(left, 8000f) - air:0.0} dB at 8 kHz against 1 kHz)");
            }
            Console.WriteLine($"  Wind at 3, 8 and 16 m/s: {levels[1]:0.0}, {levels[3]:0.0} and {levels[5]:0.0} LUFS.");
            Expect(levels[1] < -46f, $"wind: a light breeze is {levels[1]:0.0} LUFS, which is more than it should say");
            Expect(levels[5] - levels[1] > 14f, $"wind: only {levels[5] - levels[1]:0.0} dB between a breeze and a gale");

            // A wave going through the plants, as seen on screen, has to be heard going through them.
            (float[] calm, float[] calmRight) = Heard(Soundscape.Wind, 8f, 5f, 0.5f, 0.4f, 0.8f, 0.5f, 0f);
            (float[] crest, float[] crestRight) = Heard(Soundscape.Wind, 8f, 5f, 0.5f, 0.4f, 0.8f, 0.5f, 1f);
            float swell = Loudness.Integrated(crest, crestRight) - Loudness.Integrated(calm, calmRight);
            Expect(swell > 3f, $"wind: a crest going by adds only {swell:0.0} dB");
        }

        // Rain has to sit under everything that matters and still be plainly rain, and it has the same trap as wind: a noise with its top left in is a hiss at any level.
        private static void CheckRain()
        {
            float[] intensities = { 0.15f, 0.4f, 0.7f, 1f };
            var levels = new float[intensities.Length];
            for (int i = 0; i < intensities.Length; i++)
            {
                (float[] left, float[] right) = Heard(Soundscape.Rain, 8f, intensities[i], 0.15f, 0.05f);
                levels[i] = Loudness.Integrated(left, right);
                if (i > 0) Expect(levels[i] > levels[i - 1] + 2f, $"rain: no louder at {intensities[i]} than at {intensities[i - 1]} ({levels[i]:0.0} and {levels[i - 1]:0.0} LUFS)");

                float body = Octave(left, 1000f);
                Expect(Octave(left, 8000f) < body - 4f, $"rain: hisses at {intensities[i]} ({Octave(left, 8000f) - body:0.0} dB at 8 kHz against 1 kHz)");
            }
            Console.WriteLine($"  Rain at 0.15, 0.4, 0.7 and 1: {levels[0]:0.0}, {levels[1]:0.0}, {levels[2]:0.0} and {levels[3]:0.0} LUFS.");
            Expect(levels[0] < -44f, $"rain: a drizzle is {levels[0]:0.0} LUFS, which is more than it should say");
            Expect(levels[3] < -25f, $"rain: a downpour is {levels[3]:0.0} LUFS, and would bury the fight going on in it");
            Expect(levels[3] - levels[0] > 12f, $"rain: only {levels[3] - levels[0]:0.0} dB between a drizzle and a downpour");

            // On leaves and on water it's another sound, not the same one quieter.
            (float[] onGround, _) = Heard(Soundscape.Rain, 8f, 0.7f, 0f, 0f);
            (float[] onLeaves, _) = Heard(Soundscape.Rain, 8f, 0.7f, 0.9f, 0f);
            (float[] onWater, _) = Heard(Soundscape.Rain, 8f, 0.7f, 0f, 0.9f);
            float[] bands = { 500f, 1000f, 2000f, 4000f, 8000f };
            float leaves = 0f, water = 0f;
            foreach (float band in bands)
            {
                leaves += MathF.Abs(Octave(onLeaves, band) - Octave(onGround, band));
                water += MathF.Abs(Octave(onWater, band) - Octave(onGround, band));
            }
            Expect(leaves / bands.Length > 0.5f, $"rain: on leaves it's only {leaves / bands.Length:0.0} dB from rain on the ground");
            Expect(water / bands.Length > 0.5f, $"rain: on water it's only {water / bands.Length:0.0} dB from rain on the ground");
        }

        // What's left once the rain stops: small, nothing at all when nothing's wet, a few separate drops and never a wash, and in standing water something else again.
        private static void CheckDrips()
        {
            (float[] dry, _) = Heard(Soundscape.Drips, 8f, 0f, 0f);
            Expect(dry.All(sample => sample == 0f), "drips: heard with nothing wet");

            (float[] few, float[] fewRight) = Heard(Soundscape.Drips, 16f, 0.3f, 0.2f);
            (float[] many, float[] manyRight) = Heard(Soundscape.Drips, 16f, 1f, 0.2f);
            float quiet = Loudness.Integrated(few, fewRight);
            float busy = Loudness.Integrated(many, manyRight);
            Console.WriteLine($"  Drips at 0.3 and 1: {quiet:0.0} and {busy:0.0} LUFS.");
            Expect(busy > quiet + 1f, $"drips: no louder at full rate ({busy:0.0} and {quiet:0.0} LUFS)");
            Expect(busy < -30f, $"drips: {busy:0.0} LUFS, which is more than a few drops should say");
            Expect(Octave(many, 8000f) < Octave(many, 1000f) - 4f, "drips: they hiss");

            // Separate events with quiet between them: a wash would never fall back.
            int block = AudioFormat.SampleRate / 50;
            int silent = 0, blocks = 0;
            for (int at = 0; at + block <= many.Length; at += block, blocks++)
            {
                float peak = 0f;
                for (int i = at; i < at + block; i++) peak = MathF.Max(peak, MathF.Abs(many[i]));
                if (peak < 1e-4f) silent++;
            }
            Expect(silent > blocks / 3, $"drips: only {silent} of {blocks} moments are quiet, so they've run together");

            (float[] onGround, _) = Heard(Soundscape.Drips, 16f, 1f, 0f);
            (float[] inWater, _) = Heard(Soundscape.Drips, 16f, 1f, 1f);
            float[] bands = { 500f, 1000f, 2000f, 4000f };
            float apart = 0f;
            foreach (float band in bands) apart += MathF.Abs(Octave(inWater, band) - Octave(onGround, band));
            Expect(apart / bands.Length > 1f, $"drips: in water only {apart / bands.Length:0.0} dB from on the ground");
        }

        // A foot on one thing has to sound unlike a foot on another, and like a foot: most of what's heard of a step is its middle.
        private static void CheckGrounds()
        {
            float[] bands = { 125f, 250f, 500f, 1000f, 2000f, 4000f, 8000f };
            Ground[] grounds = Enum.GetValues<Ground>();
            var shapes = new float[grounds.Length][];
            for (int g = 0; g < grounds.Length; g++)
            {
                var shape = new float[bands.Length + 2];
                const int Takes = 6;
                float lasting = 0f, grit = 0f;
                for (int take = 0; take < Takes; take++)
                {
                    SoundBuffer step = Footsteps.On(grounds[g]).Render((ulong)(take + 1) * 977UL);
                    for (int b = 0; b < bands.Length; b++) shape[b] += Octave(step.Left.AsSpan(0, step.Length), bands[b]) / Takes;
                    lasting += Lasting(step) / Takes;
                    grit += Grit(step) / Takes;
                }
                float top = shape.Take(bands.Length).Max();
                for (int b = 0; b < bands.Length; b++) shape[b] -= top;

                // A click and a splash can share a spectrum, and nobody takes one for the other: how long it goes on is half of what a ground is, and whether it arrives as grit or as a
                // hush is most of the rest. Each doubling counts as much as 6 dB.
                shape[bands.Length] = 6f * MathF.Log2(lasting);
                shape[bands.Length + 1] = 6f * MathF.Log2(grit);
                shapes[g] = shape;

                // Weight below and hiss above with nothing between was what a footstep used to be.
                Expect(MathF.Max(shape[1], shape[2]) > -13f, $"{grounds[g]}: nothing in the middle of a step ({MathF.Max(shape[1], shape[2]):0.0} dB at 250 to 500 Hz)");
            }

            float closest = float.MaxValue;
            string pair = "";
            for (int a = 0; a < grounds.Length; a++)
            {
                for (int b = a + 1; b < grounds.Length; b++)
                {
                    float sum = 0f;
                    for (int n = 0; n < shapes[a].Length; n++) sum += (shapes[a][n] - shapes[b][n]) * (shapes[a][n] - shapes[b][n]);
                    float distance = MathF.Sqrt(sum / shapes[a].Length);
                    if (distance < closest)
                    {
                        closest = distance;
                        pair = $"{grounds[a]} and {grounds[b]}";
                    }
                }
            }
            Console.WriteLine($"  Closest grounds: {pair}, {closest:0.0} dB apart.");
            Expect(closest > 2.5f, $"{pair} sound alike, {closest:0.0} dB apart");
        }

        // Seconds in which the energy of a sound arrives, weighted by when it does.
        private static float Lasting(SoundBuffer sound)
        {
            double energy = 0.0, moment = 0.0;
            for (int i = 0; i < sound.Length; i++)
            {
                double power = sound.Left[i] * sound.Left[i];
                energy += power;
                moment += power * i;
            }
            return (float)(moment / Math.Max(energy, 1e-12) / AudioFormat.SampleRate) + 0.002f;
        }

        // How much the top of a sound comes in separate bits and not as a wash: the peak of its envelope over the average of it, above 1 kHz, in steps of a millisecond.
        private static float Grit(SoundBuffer sound)
        {
            const int Window = AudioFormat.SampleRate / 1000;
            Svf filter = Svf.Create(1000f, 0.7f);
            float peak = 0f;
            double sum = 0.0;
            int windows = 0;
            for (int start = 0; start + Window < sound.Length; start += Window)
            {
                float energy = 0f;
                for (int i = start; i < start + Window; i++)
                {
                    filter.Bandpass(sound.Left[i]);
                    energy += filter.High * filter.High;
                }
                peak = MathF.Max(peak, energy);
                sum += energy;
                windows++;
            }
            return MathF.Sqrt(peak / (float)Math.Max(sum / Math.Max(windows, 1), 1e-12)) + 0.01f;
        }

        // Takes are made side by side on worker threads, sharing a pool of memory, and a failure there is only ever heard as a silence where something should have been. Long stereo takes
        // are what the pool's odd sizes catch out, and one in a couple of hundred was enough for a thunderclap now and then to come to nothing.
        private static void CheckSideBySide()
        {
            SoundDefinition[] wide = SoundCatalog.All.Where(d => d.Stereo).ToArray();
            int failures = 0;
            string? failed = null;
            Parallel.For(0, 1200, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
            {
                SoundDefinition definition = wide[i % wide.Length];
                try
                {
                    definition.Render((ulong)(uint)i * 0x9E3779B97F4A7C15UL);
                }
                catch (Exception)
                {
                    Interlocked.Increment(ref failures);
                    failed = definition.Id;
                }
            });
            Expect(failures == 0, $"{failures} takes failed when made side by side, such as {failed}");
        }

        private static void CheckWater()
        {
            // The sea from a distance and at the edge.
            (float[] farLeft, float[] farRight) = Heard(Soundscape.Ocean, 24f, 0.2f, 0.5f, 0f, 0f);
            (float[] nearLeft, float[] nearRight) = Heard(Soundscape.Ocean, 24f, 0.95f, 0.5f, 0f, 0f);
            Expect(Octave(nearLeft, 4000f) > Octave(farLeft, 4000f) + 12f, "ocean: as bright from far off as at the edge");
            float surf = Loudness.Integrated(nearLeft, nearRight);
            Expect(surf > -32f, $"ocean: only {surf:0.0} LUFS at the water's edge");

            // Waves come when the game says they do.
            LiveSound told = Soundscape.Ocean.Create(11);
            told.SetTarget(OceanSound.Near, 0.95f);
            told.SetTarget(OceanSound.Swell, 0.6f);
            told.SetTarget(OceanSound.Arrivals, 1f);
            (float[] first, _, _) = Run(told, 6f);
            told.SetTarget(OceanSound.Arrivals, 2f);
            (float[] second, _, _) = Run(told, 6f);
            Expect(Octave(second, 1000f) > Octave(first, 1000f) - 8f, "ocean: a wave that was told to arrive didn't");

            (float[] lapLeft, float[] lapRight) = Heard(Soundscape.Lapping, 24f, 0.9f, 0.4f, 0f);
            (float[] outLeft, float[] outRight) = Heard(Soundscape.Lapping, 8f, 0f, 0.4f, 0f);
            float lapping = Loudness.Integrated(lapLeft, lapRight);
            Console.WriteLine($"  At the edge: surf {surf:0.0}, lapping {lapping:0.0} LUFS.");
            Expect(lapping > -38f, $"lapping: only {lapping:0.0} LUFS at the bank");
            Expect(Loudness.Integrated(outLeft, outRight) < -70f, "lapping: heard from out of earshot");

            // It comes and goes: wavelets, not a noise.
            float loudest = 0f, sum = 0f;
            int windows = 0;
            const int Window = AudioFormat.SampleRate / 10;
            for (int start = 0; start + Window < lapLeft.Length; start += Window)
            {
                double energy = 0.0;
                for (int i = start; i < start + Window; i++) energy += lapLeft[i] * lapLeft[i];
                float level = (float)(energy / Window);
                loudest = MathF.Max(loudest, level);
                sum += level;
                windows++;
            }
            Expect(loudest > sum / windows * 4f, "lapping: as steady as a noise");

            SoundBuffer body = Splashes.Body.Render(5);
            SoundBuffer wade = Splashes.Wade.Render(5);
            Expect(body.Length > wade.Length, "a body going in makes less of a splash than a foot");
        }

        // A good find is known by ear before it's seen, so each step up has to be more of an event than the last, and these are the one kind of sound that stays the same from take to take.
        private static void CheckLoot()
        {
            SoundDefinition[] drops = { Treasure.DropRare, Treasure.DropUnique };
            for (int i = 0; i < drops.Length; i++)
            {
                Expect(drops[i].PitchSpread == 0f, $"{drops[i].Id}: wanders in pitch, and has to be learned like a name");
                if (i == 0) continue;
                Expect(drops[i].Presence > drops[i - 1].Presence, $"{drops[i].Id}: stands out no more than {drops[i - 1].Id}");
                Expect(drops[i].Render(1).Seconds > drops[i - 1].Render(1).Seconds, $"{drops[i].Id}: is over sooner than {drops[i - 1].Id}");
            }

            // It lands on the very moment its sound breaks. Anything else is a thud and then, a little late, a chord.
            SoundBuffer unique = Treasure.DropUnique.Render(1);
            int strike = (int)(Treasure.UniqueStrike * AudioFormat.SampleRate);
            int window = AudioFormat.SampleRate / 5;
            float before = 0f, after = 0f;
            for (int i = strike - window; i < strike - AudioFormat.SampleRate / 50; i++) before = MathF.Max(before, MathF.Abs(unique.Left[i]));
            for (int i = strike; i < strike + window; i++) after = MathF.Max(after, MathF.Abs(unique.Left[i]));
            Console.WriteLine($"  A unique breaks {AudioFormat.GainToDecibels(after / MathF.Max(before, 1e-6f)):0.0} dB above what led up to it.");
            Expect(after > before * 2f, $"loot.drop.unique: doesn't break when it lands ({before:0.000} before, {after:0.000} after)");
        }

        private static (float[] left, float[] right, double cost) Run(LiveSound sound, float seconds)
        {
            int frames = (int)(seconds * AudioFormat.SampleRate);
            var left = new float[frames];
            var right = new float[frames];
            var clock = Stopwatch.StartNew();
            for (int done = 0; done < frames; done += 240)
            {
                int count = Math.Min(240, frames - done);
                sound.Render(left.AsSpan(done, count), right.AsSpan(done, count));
            }
            return (left, right, clock.Elapsed.TotalSeconds / seconds);
        }

        private static double Correlation(float[] a, float[] b)
        {
            double dot = 0.0, energyA = 0.0, energyB = 0.0;
            for (int i = 0; i < a.Length; i++)
            {
                dot += (double)a[i] * b[i];
                energyA += (double)a[i] * a[i];
                energyB += (double)b[i] * b[i];
            }
            return dot / Math.Sqrt(Math.Max(energyA * energyB, 1e-18));
        }

        private static void CheckDsp()
        {
            // The limiter's promise: nothing past the ceiling, ever, whatever goes in.
            var random = new SoundRandom(9);
            var limiter = new Limiter(-1f);
            var left = new float[48000];
            var right = new float[48000];
            for (int i = 0; i < left.Length; i++)
            {
                left[i] = random.Bipolar() * (i % 4000 < 200 ? 9f : 0.2f);
                right[i] = random.Bipolar() * 0.3f;
            }
            limiter.Process(left, right);
            float ceiling = AudioFormat.DecibelsToGain(-1f);
            float peak = 0f;
            foreach (float value in left) peak = MathF.Max(peak, MathF.Abs(value));
            Expect(peak <= ceiling + 1e-4f, $"limiter let through {peak:0.000} past a ceiling of {ceiling:0.000}");

            // A quiet signal must pass through it untouched, only late.
            var clean = new Limiter(-1f);
            var input = new float[4800];
            for (int i = 0; i < input.Length; i++) input[i] = 0.3f * MathF.Sin(i * 0.05f);
            var quietLeft = (float[])input.Clone();
            var quietRight = (float[])input.Clone();
            clean.Process(quietLeft, quietRight);
            float difference = 0f;
            for (int i = clean.Latency; i < input.Length; i++) difference = MathF.Max(difference, MathF.Abs(quietLeft[i] - input[i - clean.Latency]));
            Expect(difference < 1e-5f, $"limiter changed a quiet signal by {difference}");

            // 1 kHz sine at -3.01 dBFS peak in one channel of two reads -3.01 LUFS less 3 for the single channel, by the standard's definition.
            var tone = new float[AudioFormat.SampleRate * 3];
            for (int i = 0; i < tone.Length; i++) tone[i] = 0.7071f * MathF.Sin(MathF.Tau * 997f * i / AudioFormat.SampleRate);
            float lufs = Loudness.Integrated(tone, new float[tone.Length]);
            Expect(MathF.Abs(lufs - (-6.01f)) < 0.3f, $"loudness meter reads {lufs:0.00} LUFS for a tone that should read about -6.0");

            // The filter that everything leans on has to hold its shape at both ends of the range.
            Expect(FilterGainAt(1000f, 1000f, 0.7071f) is > -3.6f and < -2.4f, "a lowpass isn't 3 dB down at its cutoff");
            Expect(FilterGainAt(1000f, 8000f, 0.7071f) < -33f, "a lowpass isn't falling away");
            Expect(FilterGainAt(10000f, 1000f, 0.7071f) > -0.5f, "a lowpass takes level out of what it should pass");

            var modes = ModalObject.Draw(new SoundRandom(4), Material.Steel, ModalShape.Bar, 500f, 12);
            Expect(modes.ModeCount >= 12, "a bar came out with too few modes");

            // Reverb can't be allowed to grow, and must eventually stop.
            var reverb = new Reverb(1);
            reverb.Configure(new ReverbSettings { DecayTime = 2f, DecayHigh = 0.8f, PreDelay = 0.02f, Wet = 1f, EchoTime = 0.3f, EchoBrightness = 2000f });
            var inLeft = new float[256];
            var inRight = new float[256];
            inLeft[0] = 1f;
            inRight[0] = 1f;
            var outLeft = new float[256];
            var outRight = new float[256];
            float early = 0f;
            float late = 0f;
            for (int block = 0; block < 1400; block++)
            {
                Array.Clear(outLeft);
                Array.Clear(outRight);
                reverb.Process(inLeft, inRight, outLeft, outRight);
                inLeft[0] = 0f;
                inRight[0] = 0f;
                float level = 0f;
                foreach (float value in outLeft) level = MathF.Max(level, MathF.Abs(value));
                if (block < 200) early = MathF.Max(early, level);
                if (block > 1200) late = MathF.Max(late, level);
                Expect(float.IsFinite(level), "the reverb went bad");
            }
            Expect(early > 0.01f, "the reverb gave nothing back");
            Expect(late < early * 0.01f, $"the reverb hasn't died away after seven seconds ({late / MathF.Max(early, 1e-9f):0.000})");
        }

        private static float FilterGainAt(float cutoff, float frequency, float q)
        {
            Svf filter = Svf.Create(cutoff, q);
            float peak = 0f;
            for (int i = 0; i < 48000; i++)
            {
                float value = filter.Lowpass(MathF.Sin(MathF.Tau * frequency * i / AudioFormat.SampleRate));
                if (i > 24000) peak = MathF.Max(peak, MathF.Abs(value));
            }
            return AudioFormat.GainToDecibels(peak);
        }
    }

    /// <summary>The mixer's promises, run with no device and no clock, so a minute of it takes a moment.</summary>
    internal static class MixerCheck
    {
        private const float Silent = 1e-6f;     // Below this is only what keeps filters out of denormal numbers: -120 dB, far under anything audible.

        private static readonly SoundDefinition Test = new SoundDefinition("test.tone", take => take.Mix(take.Sine(0.5f, 700f).Fade(0.01f, 0.01f)))
        {
            Takes = 2, PitchSpread = 0f, GainSpread = 0f, Spacing = 0f, NearDistance = 5f, FarDistance = 50f, MostAtOnce = 4, ReverbSend = 0f,
            Mastering = Mastering.Default with { Stage = -100f },
        };

        private static readonly SoundDefinition Loud = new SoundDefinition("test.loud", take => take.Mix(take.White(1f).Fade(0.002f, 0.002f)))
        {
            Takes = 2, PitchSpread = 0f, GainSpread = 0f, Spacing = 0f, Presence = 12f, Priority = 90, MostAtOnce = 64, NearDistance = 50f, FarDistance = 200f,
            Mastering = Mastering.Default with { Target = -6f, Stage = -100f },
        };

        private sealed class Rig
        {
            public readonly Mixer Mixer = new();
            public readonly SoundPool TestPool = Ready(Test);
            public readonly SoundPool LoudPool = Ready(Loud);
            private readonly float[] _block = new float[4096 * 2];

            public Rig()
            {
                SetListener();
            }

            public void SetListener()
            {
                Mixer.Post(new AudioCommand { Kind = CommandKind.Listener, A = Vector3.Zero, B = Vector3.UnitX, C = -Vector3.UnitZ, D = Vector3.UnitY });
            }

            public void Play(SoundPool pool, Vector3? at = null, Vector3 velocity = default)
            {
                Mixer.Post(new AudioCommand { Kind = CommandKind.Play, Voice = Mixer.Reserve(), Payload = pool, A = at ?? default, B = velocity, X = 1f, Y = 1f, Flag = at != null });
            }

            /// <summary>Runs for a while, giving back the loudest moment of each ear and all that came out.</summary>
            public (float left, float right, float[] all) Render(float seconds)
            {
                int frames = (int)(seconds * AudioFormat.SampleRate);
                var all = new float[frames * 2];
                float left = 0f, right = 0f;
                for (int done = 0; done < frames; done += 2048)
                {
                    int count = Math.Min(2048, frames - done);
                    Mixer.Render(_block, count);
                    Array.Copy(_block, 0, all, done * 2, count * 2);
                    for (int i = 0; i < count; i++)
                    {
                        left = MathF.Max(left, MathF.Abs(_block[i * 2]));
                        right = MathF.Max(right, MathF.Abs(_block[i * 2 + 1]));
                    }
                }
                return (left, right, all);
            }
        }

        private static SoundPool Ready(SoundDefinition definition)
        {
            var pool = new SoundPool(definition);
            for (int i = 0; i < pool.Slots; i++) pool.Store(i, definition.Render((ulong)i + 1));
            return pool;
        }

        public static void Run(Action<bool, string> expect)
        {
            var rig = new Rig();
            (float quietLeft, float quietRight, _) = rig.Render(0.5f);
            expect(quietLeft < Silent && quietRight < Silent, "the mixer makes sound from nothing");

            rig.Play(rig.TestPool);
            (float left, float right, _) = rig.Render(0.3f);
            expect(left > 0.02f && MathF.Abs(left - right) < 1e-4f, $"a sound with no place isn't even in both ears ({left:0.000}, {right:0.000})");
            rig.Render(0.6f);
            expect(rig.Mixer.ActiveVoices == 0, "a sound didn't end");
            expect(rig.TestPool.Playing == 0, "a pool still counts a sound that has ended");

            rig = new Rig();
            rig.Play(rig.TestPool, new Vector3(20f, 0f, 0f));
            (left, right, _) = rig.Render(0.4f);
            expect(right > left * 1.3f, $"a sound on the right isn't louder on the right ({left:0.000}, {right:0.000})");

            rig = new Rig();
            rig.Play(rig.TestPool, new Vector3(-20f, 0f, 0f));
            (left, right, _) = rig.Render(0.4f);
            expect(left > right * 1.3f, $"a sound on the left isn't louder on the left ({left:0.000}, {right:0.000})");

            rig = new Rig();
            rig.Play(rig.TestPool, new Vector3(0f, 0f, -3f));
            (float near, _, _) = rig.Render(0.4f);
            rig = new Rig();
            rig.Play(rig.TestPool, new Vector3(0f, 0f, -35f));
            (float far, _, _) = rig.Render(0.4f);
            rig = new Rig();
            rig.Play(rig.TestPool, new Vector3(0f, 0f, -60f));
            (float beyond, _, _) = rig.Render(0.4f);
            expect(far < near * 0.5f && far > 0f, $"distance doesn't quieten a sound ({near:0.000} near, {far:0.000} far)");
            expect(beyond < Silent, $"a sound past its far limit was heard ({beyond:0.000000})");

            rig = new Rig();
            for (int i = 0; i < 12; i++) rig.Play(rig.TestPool);
            rig.Render(0.05f);
            expect(rig.TestPool.Playing <= Test.MostAtOnce, $"{rig.TestPool.Playing} playing of a sound allowed {Test.MostAtOnce}");

            // Far more than can be played at once, all of it loud: it must take this in its stride, and not go over.
            rig = new Rig();
            var random = new SoundRandom(2);
            for (int i = 0; i < 400; i++) rig.Play(rig.LoudPool, new Vector3(random.Range(-30f, 30f), 0f, random.Range(-30f, 30f)));
            (left, right, float[] all) = rig.Render(1.5f);
            float ceiling = AudioFormat.DecibelsToGain(-1f);
            expect(MathF.Max(left, right) <= ceiling + 1e-3f, $"the master went to {MathF.Max(left, right):0.000}, over its ceiling");
            expect(all.All(float.IsFinite), "the master went bad under load");
            expect(rig.Mixer.ActiveVoices <= Mixer.MostVoices, "more voices than there are");

            // The mix makes way for what's loud.
            rig = new Rig();
            rig.Play(rig.TestPool);
            (float alone, _, _) = rig.Render(0.2f);
            rig = new Rig();
            rig.Play(rig.TestPool);
            rig.Play(rig.LoudPool);
            rig.Render(0.05f);
            expect(rig.Mixer.WindowDecibels > 9f, $"the window didn't open for a loud sound ({rig.Mixer.WindowDecibels:0.0} dB)");

            // A queue flooded by a game gone wrong loses sounds and nothing else.
            rig = new Rig();
            for (int i = 0; i < 20000; i++) rig.Play(rig.TestPool);
            rig.Render(0.2f);
            expect(rig.TestPool.Playing <= Test.MostAtOnce, "a flooded queue broke the voice limit");

            // Stopped, a sound fades out and doesn't cut.
            var fire = new Rig();
            int id = fire.Mixer.Reserve();
            fire.Mixer.Post(new AudioCommand { Kind = CommandKind.Start, Voice = id, Payload = Soundscape.Rain.Create(5), Extra = Soundscape.Rain, X = 1f });
            fire.Mixer.Post(new AudioCommand { Kind = CommandKind.SetParameter, Voice = id, X = 0f, Y = 1f });
            fire.Render(0.3f);
            expect(fire.Mixer.ActiveVoices == 1, "a live sound didn't start");
            fire.Mixer.Post(new AudioCommand { Kind = CommandKind.Stop, Voice = id, X = 0.1f });
            (_, _, float[] tail) = fire.Render(0.3f);
            expect(fire.Mixer.ActiveVoices == 0, "a stopped live sound went on");
            float step = 0f;
            for (int i = 2; i < tail.Length; i += 2) step = MathF.Max(step, MathF.Abs(tail[i] - tail[i - 2]));
            expect(step < 0.5f, $"stopping a sound made a click ({step:0.00})");

            // A pitch a moving source raises must be real: count the zero crossings of something flying in and of something flying off.
            float coming = PitchOfFlyby(120f);
            float going = PitchOfFlyby(-120f);
            expect(coming > 1020f && coming < 1130f, $"something coming at 120 m/s should sound near 1077 Hz, not {coming:0.0}");
            expect(going > 480f && going < 560f, $"something going at 120 m/s should sound near 518 Hz, not {going:0.0}");

            CheckAllocation(expect);
            CheckTakes(expect);

            // Chaos for a long time: everything at once, moved and stopped, for what a real session could throw at it.
            rig = new Rig();
            var ids = new List<int>();
            bool sane = true;
            for (int step2 = 0; step2 < 900; step2++)
            {
                rig.Mixer.Post(new AudioCommand { Kind = CommandKind.Listener, A = new Vector3(random.Range(-5f, 5f), 0f, random.Range(-5f, 5f)), B = Vector3.UnitX, C = -Vector3.UnitZ, D = Vector3.UnitY });
                rig.Play(rig.TestPool, new Vector3(random.Range(-40f, 40f), 0f, random.Range(-40f, 40f)), new Vector3(random.Bipolar() * 300f, 0f, 0f));
                if (step2 % 40 == 0) rig.Play(rig.LoudPool, new Vector3(random.Range(-40f, 40f), 0f, 0f));
                if (step2 % 7 == 0)
                {
                    int live = rig.Mixer.Reserve();
                    ids.Add(live);
                    rig.Mixer.Post(new AudioCommand { Kind = CommandKind.Start, Voice = live, Payload = Soundscape.Stream.Create((ulong)step2 + 1), Extra = Soundscape.Stream, X = 1f, A = default, Flag = true });
                }
                if (ids.Count > 3)
                {
                    rig.Mixer.Post(new AudioCommand { Kind = CommandKind.Stop, Voice = ids[0], X = 0.02f });
                    ids.RemoveAt(0);
                }
                if (step2 % 50 == 0)
                {
                    rig.Mixer.Post(new AudioCommand { Kind = CommandKind.Environment, Reverb = new ReverbSettings { DecayTime = random.Range(0.3f, 5f), DecayHigh = 0.3f, PreDelay = random.Range(0.01f, 0.2f), Wet = random.Float(), Echo = random.Float(), EchoTime = random.Range(0.1f, 0.6f), EchoBrightness = 2000f } });
                }
                (_, _, float[] block) = rig.Render(0.05f);
                foreach (float value in block) sane &= float.IsFinite(value) && MathF.Abs(value) <= 1f;
            }
            expect(sane, "the mixer went bad under a long stretch of chaos");
        }

        // The audio thread must never allocate: that's what would one day stop it for a garbage collection, and be heard as a gap.
        private static void CheckAllocation(Action<bool, string> expect)
        {
            var rig = new Rig();
            var block = new float[512];
            foreach (LiveDefinition definition in SoundCatalog.AllLive)
            {
                int id = rig.Mixer.Reserve();
                rig.Mixer.Post(new AudioCommand { Kind = CommandKind.Start, Voice = id, Payload = definition.Create(9), Extra = definition, X = 1f, A = new Vector3(4f, 0f, 2f), Flag = true });
                for (int i = 0; i < definition.Defaults.Length; i++) rig.Mixer.Post(new AudioCommand { Kind = CommandKind.SetParameter, Voice = id, X = i, Y = definition.Defaults[i] });
            }

            // Warmed up first: the runtime compiles and resolves on first use, which isn't the mixer's doing.
            for (int i = 0; i < 400; i++)
            {
                if (i % 8 == 0) rig.Play(rig.TestPool, new Vector3(i % 30, 0f, 3f));
                rig.Mixer.Render(block, 256);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 4000; i++)
            {
                if (i % 8 == 0)
                {
                    rig.Mixer.Post(new AudioCommand { Kind = CommandKind.Play, Voice = 1, Payload = rig.TestPool, A = new Vector3(i % 30, 0f, 3f), X = 1f, Y = 1f, Flag = true });
                    rig.Mixer.Post(new AudioCommand { Kind = CommandKind.Listener, A = new Vector3(i % 3, 0f, 0f), B = Vector3.UnitX, C = -Vector3.UnitZ, D = Vector3.UnitY });
                }
                rig.Mixer.Render(block, 256);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            expect(allocated == 0, $"the mixer allocated {allocated} bytes while running, with every live sound going");
        }

        // That a sound never comes round the same: nothing played twice running, heard takes replaced by new ones, and what isn't played kept small.
        private static void CheckTakes(Action<bool, string> expect)
        {
            var definition = new SoundDefinition("test.takes", take => take.Mix(take.White(0.02f).Fade(0.002f, 0.002f))) { Takes = 4, PlaysPerTake = 2, Mastering = Mastering.Default with { Stage = -100f } };
            var pool = new SoundPool(definition);
            var random = new SoundRandom(1);
            using var factory = new SoundFactory(new[] { pool });
            for (int wait = 0; wait < 400 && !factory.Ready; wait++) Thread.Sleep(5);
            expect(factory.Ready, "the factory never made a first take");
            expect(factory.TakesMade == 1, $"a sound nobody has asked for has {factory.TakesMade} takes, not one");

            // Asked for once, it fills up. Until then its one take is all there is, and it's better heard twice than not at all.
            pool.Next(random);
            for (int wait = 0; wait < 400 && factory.TakesMade < definition.Takes; wait++) Thread.Sleep(5);
            expect(factory.TakesMade >= definition.Takes, "a sound that was asked for didn't fill up");

            var heard = new HashSet<SoundBuffer>();
            SoundBuffer? last = null;
            bool repeated = false;
            for (int i = 0; i < 400; i++)
            {
                SoundBuffer? take = pool.Next(random);
                if (take == null) continue;
                repeated |= take == last;
                last = take;
                heard.Add(take);
                Thread.Sleep(1);
            }
            expect(!repeated, "the same take played twice in a row");
            expect(heard.Count > definition.Takes * 3, $"only {heard.Count} takes were heard in 400 plays: they aren't being replaced");
        }

        private static float PitchOfFlyby(float speed)
        {
            var definition = new SoundDefinition("test.doppler", take => take.Mix(take.Sine(0.5f, 700f).Fade(0.01f, 0.01f)))
            {
                Takes = 1, PitchSpread = 0f, GainSpread = 0f, Doppler = true, FarDistance = 400f, NearDistance = 300f, ReverbSend = 0f,
                Mastering = Mastering.Default with { Stage = -100f },
            };
            var rig = new Rig();
            SoundPool pool = Ready(definition);
            rig.Play(pool, new Vector3(0f, 0f, -80f), new Vector3(0f, 0f, speed));
            (_, _, float[] all) = rig.Render(0.3f);

            int crossings = 0;
            int from = (int)(0.08f * AudioFormat.SampleRate) * 2;
            int to = (int)(0.25f * AudioFormat.SampleRate) * 2;
            for (int i = from + 2; i < to; i += 2)
            {
                if (all[i - 2] < 0f && all[i] >= 0f) crossings++;
            }
            return crossings / 0.17f;
        }
    }
}

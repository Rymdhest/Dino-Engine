using Dino_Engine.Sound.Dsp;

namespace Dino_Engine.Sound.Synthesis
{
    /// <summary>
    /// What a struck thing is made of. Hearing tells materials apart almost entirely by how fast they ring out and how much faster
    /// the top goes (Klatzky, Pai and Krotkov; Aramaki and Kronland-Martinet), and that is a measured property: a mode at f
    /// decays at about pi f tan(phi), tan(phi) being the material's loss factor. So each entry is that number, not a guess at a sound.
    /// </summary>
    public sealed class Material
    {
        public readonly string Name;
        public readonly float LossFactor;       // tan(phi): internal friction. Steel about 0.0004, wood about 0.015.
        public readonly float BaseDecay;        // Per second, at any pitch: what's lost to the air and to whatever holds it.
        public readonly float Contact;          // Seconds a hard strike on it lasts. A stiffer thing gives a shorter force and so more top.

        public Material(string name, float lossFactor, float baseDecay, float contact)
        {
            Name = name;
            LossFactor = lossFactor;
            BaseDecay = baseDecay;
            Contact = contact;
        }

        public float DecayAt(float frequency) => BaseDecay + MathF.PI * LossFactor * frequency;

        public static readonly Material Steel = new("Steel", 0.0005f, 2.4f, 0.00012f);
        public static readonly Material Iron = new("Iron", 0.0014f, 5f, 0.00018f);
        public static readonly Material Bronze = new("Bronze", 0.0004f, 1.2f, 0.00015f);
        public static readonly Material Gold = new("Gold", 0.0011f, 4f, 0.00012f);
        public static readonly Material Glass = new("Glass", 0.0022f, 3.5f, 0.00008f);
        public static readonly Material Ceramic = new("Ceramic", 0.0055f, 9f, 0.00009f);
        public static readonly Material Stone = new("Stone", 0.012f, 34f, 0.0001f);
        public static readonly Material Bone = new("Bone", 0.018f, 26f, 0.00018f);
        public static readonly Material Wood = new("Wood", 0.016f, 15f, 0.00035f);
        public static readonly Material Plank = new("Plank", 0.024f, 22f, 0.0005f);
        public static readonly Material Leather = new("Leather", 0.08f, 60f, 0.0016f);
        public static readonly Material Ice = new("Ice", 0.0065f, 16f, 0.00011f);
        public static readonly Material Flesh = new("Flesh", 0.075f, 15f, 0.0035f);
        public static readonly Material Cloth = new("Cloth", 0.2f, 90f, 0.002f);
        public static readonly Material Earth = new("Earth", 0.09f, 42f, 0.0022f);
    }

    /// <summary>
    /// How a kind of object's modes are spaced. This is geometry, and it is what makes a bar sound unlike a plate of the same
    /// metal. Each is the solution of that shape's wave equation, relative to its lowest mode.
    /// </summary>
    public enum ModalShape
    {
        Bar,        // Free at both ends: a blade seen end on, a beam, a bone.
        Plate,      // Panels, shields, a breastplate: many modes, closely packed, in no pattern the ear can follow.
        Shell,      // A bell or a pot: modes in near pairs, which beat against each other.
        Solid,      // A lump: few, wide, and high.
    }

    /// <summary>
    /// A vibrating object as modal synthesis sees it: a set of decaying sine waves, each with its frequency, decay and strength
    /// (Adrien; van den Doel and Pai). It's a linear system, so it can be built once and struck by any force at all, and the
    /// same strike point always excites it the same way. Where the object is struck decides which modes speak, and a
    /// new object is drawn for each take, so no two sound alike.
    /// </summary>
    public sealed class ModalObject
    {
        // Free-free bar: the roots of cos(x) cosh(x) = 1, squared and relative to the first.
        private static readonly float[] BarRatios = { 1f, 2.757f, 5.404f, 8.933f, 13.344f, 18.638f, 24.815f, 31.87f, 39.81f, 48.64f, 58.34f, 68.92f, 80.4f, 92.7f };

        private readonly float[] _frequency;
        private readonly float[] _decay;
        private readonly float[] _gain;
        private readonly int _count;

        public int ModeCount => _count;
        public ReadOnlySpan<float> Frequencies => _frequency.AsSpan(0, _count);

        private ModalObject(float[] frequency, float[] decay, float[] gain, int count)
        {
            _frequency = frequency;
            _decay = decay;
            _gain = gain;
            _count = count;
        }

        /// <summary>
        /// Draws one such object. <paramref name="fundamental"/> is its lowest mode; <paramref name="modes"/> how many to keep;
        /// <paramref name="tilt"/> is dB per octave lost going up. Top modes are what's cut first by anything soft that strikes it, which <see cref="Contact"/> does.
        /// </summary>
        public static ModalObject Draw(SoundRandom random, Material material, ModalShape shape, float fundamental, int modes = 32, float tilt = -3f, float ringing = 1f)
        {
            var frequency = new float[modes * 3];
            var decay = new float[modes * 3];
            var gain = new float[modes * 3];
            int count = 0;

            // Nothing real is perfectly even, and any flaw splits a mode into two a hair apart, which beat. In a thing that rings on,
            // this is the movement that keeps a partial from sounding like a test tone.
            float twinChance = material.LossFactor < 0.004f ? 0.7f : 0f;

            // Being struck somewhere other than the middle is what makes mode strength uneven: sin(n pi x) is the mode shape at x.
            float strike = random.Range(0.12f, 0.45f);
            float aspect = random.Range(1.1f, 2.4f);

            void AddMode(float ratio, int index)
            {
                float f = fundamental * ratio;
                if (f > 19000f || count >= frequency.Length) return;
                float shapeGain = MathF.Abs(MathF.Sin((index + 1) * MathF.PI * strike)) * 0.85f + 0.15f;
                float octaves = MathF.Log2(ratio);
                float level = AudioFormat.DecibelsToGain(tilt * octaves + random.Gaussian() * 3.5f) * shapeGain;
                frequency[count] = f;
                decay[count] = material.DecayAt(f) * random.Range(0.8f, 1.25f) / ringing;
                gain[count] = level;
                count++;

                if (random.Chance(twinChance) && count < frequency.Length)
                {
                    frequency[count] = f * (1f + random.Range(0.0004f, 0.0045f));
                    decay[count] = material.DecayAt(f) * random.Range(0.8f, 1.25f) / ringing;
                    gain[count] = level * random.Range(0.35f, 1f);
                    count++;
                }
            }

            switch (shape)
            {
                case ModalShape.Bar:
                    for (int i = 0; i < modes; i++)
                    {
                        // Past the table a thin bar's modes keep to the square law.
                        float ratio = i < BarRatios.Length ? BarRatios[i] : MathF.Pow(i + 0.5f, 2f) * 0.2235f * 1.0f;
                        AddMode(ratio * (1f + random.Gaussian() * 0.012f), i);
                    }
                    break;

                case ModalShape.Plate:
                    // A rectangle: f is proportional to m^2 + (n / aspect)^2, for every pair of whole numbers. Sorted, these are a thicket.
                    var ratios = new List<float>();
                    float baseline = 1f + 1f / (aspect * aspect);
                    for (int m = 1; m <= 12; m++)
                    {
                        for (int n = 1; n <= 12; n++) ratios.Add((m * m + n * n / (aspect * aspect)) / baseline);
                    }
                    ratios.Sort();
                    for (int i = 0; i < modes && i < ratios.Count; i++) AddMode(ratios[i] * (1f + random.Gaussian() * 0.01f), i);
                    break;

                case ModalShape.Shell:
                    // A ring's modes go as about n^2, and any flaw splits each into a pair a hair apart, which beat: the shimmer in a bell.
                    for (int i = 0; i < modes / 2; i++)
                    {
                        float n = i + 2f;
                        float ratio = n * n * (1f + 0.02f * i) / 4f * (1f + random.Gaussian() * 0.012f);
                        AddMode(ratio, i);
                        AddMode(ratio * (1f + random.Range(0.0006f, 0.004f)), i);
                    }
                    break;

                case ModalShape.Solid:
                    for (int i = 0; i < modes; i++) AddMode((1f + i * 0.7f + i * i * 0.11f) * (1f + random.Gaussian() * 0.04f), i);
                    break;
            }

            return new ModalObject(frequency, decay, gain, count);
        }

        /// <summary>Adds a little more of the object: a second, unrelated set of modes laid on the first. For things with parts, like a blade in its hilt.</summary>
        public ModalObject With(ModalObject other, float decibels = 0f)
        {
            float gain = AudioFormat.DecibelsToGain(decibels);
            int count = _count + other._count;
            var frequency = new float[count];
            var decay = new float[count];
            var gains = new float[count];
            Array.Copy(_frequency, frequency, _count);
            Array.Copy(_decay, decay, _count);
            Array.Copy(_gain, gains, _count);
            for (int i = 0; i < other._count; i++)
            {
                frequency[_count + i] = other._frequency[i];
                decay[_count + i] = other._decay[i];
                gains[_count + i] = other._gain[i] * gain;
            }
            return new ModalObject(frequency, decay, gains, count);
        }

        /// <summary>
        /// Drives every mode with a force. Each one is a two-pole resonator, run in double precision as the low ones sit very close to
        /// the unit circle, and stopped once it has fallen 80 dB, which is what makes a hundred modes affordable.
        /// </summary>
        public Signal Ring(Signal force, float seconds)
        {
            Signal output = force.Take.Track(seconds);
            float[] result = output.Samples;
            float[] drive = force.Samples;
            int driveLength = Math.Min(force.Length, output.Length);

            for (int m = 0; m < _count; m++)
            {
                double w = 2.0 * Math.PI * _frequency[m] / AudioFormat.SampleRate;
                double r = Math.Exp(-_decay[m] / AudioFormat.SampleRate);
                double a1 = 2.0 * r * Math.Cos(w);
                double a2 = -r * r;
                double scale = _gain[m] * Math.Sin(w);
                double y1 = 0.0, y2 = 0.0;

                int last = Math.Min(output.Length, driveLength + (int)(9.2 / _decay[m] * AudioFormat.SampleRate));
                for (int i = 0; i < last; i++)
                {
                    double x = i < driveLength ? drive[i] : 0.0;
                    double y = a1 * y1 + a2 * y2 + scale * x;
                    y2 = y1;
                    y1 = y;
                    result[i] += (float)y;
                }
            }
            return output;
        }
    }

    /// <summary>
    /// The rest of what a struck thing does. Modes alone are sterile: a spectrogram of them is clean lines on black, and a real
    /// impact has a dense floor between its partials, from all the modes too close together or too quick to tell apart. So,
    /// as in Aramaki and Kronland-Martinet's synthesizer, a noise is added with the same physics: split into bands, each dying at
    /// the rate the material gives that frequency. It is what makes a hit sound like an object and not an instrument.
    /// </summary>
    public static class Residual
    {
        private const int Bands = 20;

        /// <param name="tilt">Decibels per octave going up.</param>
        /// <param name="ringing">Above 1 it dies more slowly than the material's own rate.</param>
        public static Signal Of(Take take, Material material, float seconds, float lowest = 80f, float highest = 19000f, float tilt = -2f, float ringing = 1f)
        {
            Signal output = take.Track(seconds);
            Signal noise = take.White(seconds);
            float[] result = output.Samples;
            float[] white = noise.Samples;
            float ratio = highest / lowest;

            for (int b = 0; b < Bands; b++)
            {
                float frequency = lowest * MathF.Pow(ratio, (b + 0.5f) / Bands);
                float decay = material.DecayAt(frequency) / ringing;
                float fall = MathF.Exp(-decay / AudioFormat.SampleRate);
                float level = AudioFormat.DecibelsToGain(tilt * MathF.Log2(frequency / lowest));
                var band = Svf.Create(frequency, 2.6f);

                int length = Math.Min(output.Length, (int)(9.2f / decay * AudioFormat.SampleRate));
                for (int i = 0; i < length; i++)
                {
                    result[i] += band.BandpassNormal(white[i]) * level;
                    level *= fall;
                }
            }
            return output;
        }
    }

    /// <summary>A blow on something: what it is, what shape, and how it's struck.</summary>
    public struct Blow
    {
        public Material Material;
        public ModalShape Shape;
        public float Fundamental;       // Hz.
        public int Modes;
        public float Tilt;              // Decibels per octave up across the modes.
        public float Ringing;           // Above 1 it rings longer than the material's own rate.
        public float Hardness;          // Seconds of contact. Zero uses the material's own.
        public float Rattle;            // 0..1 the micro-collisions of a hard contact.
        public float Noise;             // Decibels of the stochastic part against the modes.
        public float NoiseLow;          // Hz, bottom of the stochastic part.
        public int Bounces;             // Lesser blows that follow, as a thing rebounds and lands again.
    }

    /// <summary>A complete impact, modes and residual together.</summary>
    public static class Impact
    {
        public static Signal Of(Take take, in Blow blow, float seconds, float strength = 1f)
        {
            SoundRandom random = take.Random;
            float ringing = blow.Ringing > 0f ? blow.Ringing : 1f;
            ModalObject body = ModalObject.Draw(random, blow.Material, blow.Shape, blow.Fundamental, Math.Max(blow.Modes, 8), blow.Tilt, ringing);
            float hardness = (blow.Hardness > 0f ? blow.Hardness : blow.Material.Contact) * (1.4f - 0.4f * strength);

            Signal ring = body.Ring(Contact.Strike(take, body, hardness, strength, blow.Rattle), seconds);
            float scale = 1f / MathF.Max(ring.Peak(), 1e-9f);
            ring.Scale(scale);
            Signal residual = Residual.Of(take, blow.Material, seconds, blow.NoiseLow > 0f ? blow.NoiseLow : 80f, 19000f, -1.5f, ringing).Normalize(blow.Noise);
            ring.Add(residual);

            float at = 0f;
            float level = 1f;
            for (int i = 0; i < blow.Bounces; i++)
            {
                at += random.Range(0.012f, 0.05f) * (1f + i * 0.5f);
                level *= random.Range(0.25f, 0.5f);
                if (at >= seconds) break;
                Signal again = body.Ring(Contact.Press(take, hardness * 1.3f, level), seconds - at);
                ring.Add(again, at, scale);
            }
            return ring;
        }
    }

    /// <summary>
    /// The force of a contact, which is what drives a <see cref="ModalObject"/>. How hard a thing is, is heard as how short this is:
    /// a 1 - cos pulse is enough (van den Doel, Kry and Pai). A short force is a wide one in frequency, which is why metal on
    /// metal rings to the top and a fist on a door doesn't.
    /// </summary>
    public static class Contact
    {
        /// <summary>One smooth press lasting <paramref name="seconds"/>.</summary>
        public static Signal Press(Take take, float seconds, float strength = 1f, float at = 0f)
        {
            Signal force = take.Track(at + seconds + 0.002f);
            AddPress(force, seconds, strength, at);
            return force;
        }

        private static void AddPress(Signal force, float seconds, float strength, float at)
        {
            int start = (int)(at * AudioFormat.SampleRate);
            int length = Math.Max((int)(seconds * AudioFormat.SampleRate), 1);

            // Over a single sample the shape is lost, and what's left has to carry the same momentum.
            float height = strength * 2f / length * 30f;
            for (int i = 0; i < length && start + i < force.Length; i++)
            {
                force.Samples[start + i] += height * 0.5f * (1f - MathF.Cos(MathF.Tau * (i + 0.5f) / length));
            }
        }

        /// <summary>
        /// A hard strike. A single clean pulse sounds too clean: real hard contacts are a rattle of separations and collisions within
        /// about 15 ms, driven by the modes themselves (van den Doel, Kry and Pai). So this is a main pulse and a burst of
        /// smaller ones, spaced at the periods of the object's first few modes.
        /// </summary>
        public static Signal Strike(Take take, ModalObject body, float seconds, float strength = 1f, float rattle = 0.5f)
        {
            SoundRandom random = take.Random;
            Signal force = take.Track(0.03f);
            AddPress(force, seconds, strength, 0f);

            ReadOnlySpan<float> modes = body.Frequencies;
            int bursts = Math.Min(modes.Length, 4);
            for (int m = 0; m < bursts; m++)
            {
                float period = 1f / MathF.Max(modes[m], 200f);
                float time = period * random.Range(0.8f, 1.2f);
                float level = strength * rattle * random.Range(0.2f, 0.6f);
                while (time < 0.015f && level > 0.01f)
                {
                    AddPress(force, seconds * random.Range(0.6f, 1.2f), level, time);
                    time += period * random.Range(0.7f, 1.9f);
                    level *= random.Range(0.45f, 0.8f);
                }
            }
            return force;
        }

        /// <summary>
        /// Something dragged along something: the phonograph needle model, with the roughness of the surface as fractal noise, played
        /// as fast as the contact moves and passed through a resonance that follows it. Level goes as the square root of speed
        /// times force, which is friction's power.
        /// </summary>
        public static Signal Scrape(Take take, float seconds, Curve speed, Curve pressure, float roughness = 0.5f, float pitch = 900f)
        {
            SoundRandom random = take.Random;
            Signal force = take.Track(seconds);
            var pink = new PinkNoise();
            var resonance = new Svf();
            var body = new Svf();
            body.Set(pitch * 3f, 0.7f);

            float[] samples = force.Samples;
            float held = 0f;
            float position = 0f;
            for (int i = 0; i < force.Length; i++)
            {
                float t = i * (1f / AudioFormat.SampleRate);
                float v = speed.At(t);

                // Slowly, the needle passes fewer bumps a second: sample-and-hold is a surface being read at that speed.
                position += MathF.Min(v * 2f, 1f);
                if (position >= 1f)
                {
                    position -= 1f;
                    held = pink.Next(random.Bipolar()) * (1f - roughness) + random.Bipolar() * roughness * 0.6f;
                }
                if (i % 16 == 0) resonance.Set(pitch * (0.4f + 2f * v), 2.2f);
                float tone = resonance.BandpassNormal(held);
                samples[i] = (tone * 0.7f + body.Highpass(held) * 0.5f) * MathF.Sqrt(MathF.Max(v * pressure.At(t), 0f));
            }
            return force;
        }
    }
}

namespace Dino_Engine.Sound.Dsp
{
    /// <summary>What everything is made and mixed at. Windows converts on the way out if the device runs at something else.</summary>
    public static class AudioFormat
    {
        public const int SampleRate = 48000;
        public const float Nyquist = SampleRate * 0.5f;

        /// <summary>Added to what feeds a recursive filter. Without it a tail decays into denormal numbers, which are very slow, and .NET can't turn them off.</summary>
        public const float Denormal = 1e-20f;

        public static float DecibelsToGain(float decibels) => MathF.Pow(10f, decibels * 0.05f);
        public static float GainToDecibels(float gain) => 20f * MathF.Log10(MathF.Max(gain, 1e-12f));

        /// <summary>The pitch ratio of a number of semitones.</summary>
        public static float Semitones(float semitones) => MathF.Pow(2f, semitones * (1f / 12f));
    }

    /// <summary>
    /// A first order filter. Cheap, can't ring, and right for smoothing control values, air absorption and the head's shadow.
    /// </summary>
    public struct OnePole
    {
        private float _state;
        private float _coefficient;

        public void SetCutoff(float frequency, float rate = AudioFormat.SampleRate)
        {
            _coefficient = 1f - MathF.Exp(-MathF.Tau * MathF.Min(frequency, rate * 0.49f) / rate);
        }

        /// <summary>Sets it to reach about 63% of the way to a new value in this time.</summary>
        public void SetTime(float seconds, float rate = AudioFormat.SampleRate)
        {
            _coefficient = 1f - MathF.Exp(-1f / MathF.Max(seconds * rate, 1e-3f));
        }

        public float State { readonly get => _state; set => _state = value; }

        public float Lowpass(float input)
        {
            _state += (input - _state) * _coefficient + AudioFormat.Denormal;
            return _state;
        }

        public float Highpass(float input) => input - Lowpass(input);
    }

    /// <summary>
    /// The workhorse: a state variable filter, solved by trapezoidal integration (Simper, after Zavalishin's topology preserving
    /// transform). Unlike a biquad or Chamberlin's filter it stays stable and quiet when its frequency is swept at audio rate,
    /// and nearly everything here sweeps. One call gives the lowpass, bandpass and highpass outputs together.
    /// </summary>
    public struct Svf
    {
        private float _ic1, _ic2;
        private float _a1, _a2, _a3, _k;

        public float Low, Band, High;

        /// <summary>A ready filter, as a struct's zero state would pass nothing.</summary>
        public static Svf Create(float frequency, float q = 0.7071f)
        {
            var filter = new Svf();
            filter.Set(frequency, q);
            return filter;
        }

        public void Set(float frequency, float q = 0.7071f)
        {
            float g = MathF.Tan(MathF.PI * Math.Clamp(frequency, 4f, AudioFormat.SampleRate * 0.49f) / AudioFormat.SampleRate);
            _k = 1f / MathF.Max(q, 0.02f);
            _a1 = 1f / (1f + g * (g + _k));
            _a2 = g * _a1;
            _a3 = g * _a2;
        }

        public void Process(float input)
        {
            float v3 = input - _ic2;
            float v1 = _a1 * _ic1 + _a2 * v3;
            float v2 = _ic2 + _a2 * _ic1 + _a3 * v3;
            _ic1 = 2f * v1 - _ic1 + AudioFormat.Denormal;
            _ic2 = 2f * v2 - _ic2 + AudioFormat.Denormal;
            Low = v2;
            Band = v1;
            High = input - _k * v1 - v2;
        }

        public float Lowpass(float input)
        {
            Process(input);
            return Low;
        }

        public float Highpass(float input)
        {
            Process(input);
            return High;
        }

        public float Bandpass(float input)
        {
            Process(input);
            return Band;
        }

        /// <summary>Bandpass with a peak of unity, whatever the Q: <see cref="Band"/> alone peaks at Q.</summary>
        public float BandpassNormal(float input)
        {
            Process(input);
            return Band * _k;
        }

        public float Notch(float input)
        {
            Process(input);
            return Low + High;
        }

        public void Reset()
        {
            _ic1 = _ic2 = 0f;
            Low = Band = High = 0f;
        }
    }

    /// <summary>
    /// For fixed equalisation, where a shelf or a bell is wanted (RBJ's cookbook, transposed direct form II). Its state is double, so
    /// it stays clean down at the bottom of the range. Sweep an <see cref="Svf"/> instead.
    /// </summary>
    public struct Biquad
    {
        private double _b0, _b1, _b2, _a1, _a2, _z1, _z2;

        public static Biquad Peak(float frequency, float q, float decibels)
        {
            double a = Math.Pow(10.0, decibels / 40.0);
            Angles(frequency, q, out double cos, out double alpha);
            return Normalized(1 + alpha * a, -2 * cos, 1 - alpha * a, 1 + alpha / a, -2 * cos, 1 - alpha / a);
        }

        public static Biquad LowShelf(float frequency, float decibels, float q = 0.7071f)
        {
            double a = Math.Pow(10.0, decibels / 40.0);
            Angles(frequency, q, out double cos, out double alpha);
            double root = 2 * Math.Sqrt(a) * alpha;
            return Normalized(
                a * ((a + 1) - (a - 1) * cos + root),
                2 * a * ((a - 1) - (a + 1) * cos),
                a * ((a + 1) - (a - 1) * cos - root),
                (a + 1) + (a - 1) * cos + root,
                -2 * ((a - 1) + (a + 1) * cos),
                (a + 1) + (a - 1) * cos - root);
        }

        public static Biquad HighShelf(float frequency, float decibels, float q = 0.7071f)
        {
            double a = Math.Pow(10.0, decibels / 40.0);
            Angles(frequency, q, out double cos, out double alpha);
            double root = 2 * Math.Sqrt(a) * alpha;
            return Normalized(
                a * ((a + 1) + (a - 1) * cos + root),
                -2 * a * ((a - 1) + (a + 1) * cos),
                a * ((a + 1) + (a - 1) * cos - root),
                (a + 1) - (a - 1) * cos + root,
                2 * ((a - 1) - (a + 1) * cos),
                (a + 1) - (a - 1) * cos - root);
        }

        public static Biquad Highpass(float frequency, float q = 0.7071f)
        {
            Angles(frequency, q, out double cos, out double alpha);
            return Normalized((1 + cos) / 2, -(1 + cos), (1 + cos) / 2, 1 + alpha, -2 * cos, 1 - alpha);
        }

        public static Biquad Lowpass(float frequency, float q = 0.7071f)
        {
            Angles(frequency, q, out double cos, out double alpha);
            return Normalized((1 - cos) / 2, 1 - cos, (1 - cos) / 2, 1 + alpha, -2 * cos, 1 - alpha);
        }

        /// <summary>Set by raw coefficients, for filters defined by a standard rather than a shape.</summary>
        public static Biquad FromCoefficients(double b0, double b1, double b2, double a1, double a2)
        {
            return new Biquad { _b0 = b0, _b1 = b1, _b2 = b2, _a1 = a1, _a2 = a2 };
        }

        private static void Angles(float frequency, float q, out double cos, out double alpha)
        {
            double w = 2.0 * Math.PI * Math.Clamp(frequency, 1f, AudioFormat.SampleRate * 0.49f) / AudioFormat.SampleRate;
            cos = Math.Cos(w);
            alpha = Math.Sin(w) / (2.0 * q);
        }

        private static Biquad Normalized(double b0, double b1, double b2, double a0, double a1, double a2)
        {
            return new Biquad { _b0 = b0 / a0, _b1 = b1 / a0, _b2 = b2 / a0, _a1 = a1 / a0, _a2 = a2 / a0 };
        }

        public float Process(float input)
        {
            double x = input;
            double y = _b0 * x + _z1;
            _z1 = _b1 * x - _a1 * y + _z2;
            _z2 = _b2 * x - _a2 * y;
            return (float)y;
        }

        public void Reset() => _z1 = _z2 = 0.0;
    }

    /// <summary>Takes out what doesn't move. Left in, it eats headroom and makes a click wherever a sound starts or stops.</summary>
    public struct DcBlocker
    {
        private float _x, _y;

        public float Process(float input)
        {
            float y = input - _x + 0.9985f * _y + AudioFormat.Denormal;
            _x = input;
            _y = y;
            return y;
        }
    }

    /// <summary>
    /// Pink noise: the same power in every octave, as most things in nature have, where white noise has far too much at the top
    /// (Paul Kellet's filter, good to about 0.05 dB over the audible range).
    /// </summary>
    public struct PinkNoise
    {
        private float _b0, _b1, _b2, _b3, _b4, _b5, _b6;

        public float Next(float white)
        {
            _b0 = 0.99886f * _b0 + white * 0.0555179f;
            _b1 = 0.99332f * _b1 + white * 0.0750759f;
            _b2 = 0.96900f * _b2 + white * 0.1538520f;
            _b3 = 0.86650f * _b3 + white * 0.3104856f;
            _b4 = 0.55000f * _b4 + white * 0.5329522f;
            _b5 = -0.7616f * _b5 - white * 0.0168980f;
            float pink = _b0 + _b1 + _b2 + _b3 + _b4 + _b5 + _b6 + white * 0.5362f;
            _b6 = white * 0.115926f;
            return pink * 0.11f;
        }
    }

    /// <summary>Brown noise: what's left when each step wanders from the last. Most of it is at the bottom, like distant surf or a rumble.</summary>
    public struct BrownNoise
    {
        private float _state;

        public float Next(float white)
        {
            _state = (_state + white * 0.02f) * 0.9985f;
            return _state * 3.5f;
        }
    }

    /// <summary>
    /// A control value wandering smoothly at random, with its energy spread like the fluctuations of nature (roughly 1/f), for whatever must not
    /// sit still: a gust, a flame, the depth of the rain. Three random walks a few octaves apart, advanced only once a block.
    /// </summary>
    public struct Wander
    {
        private OnePole _slow, _middle, _fast;
        private float _slowTarget, _middleTarget, _fastTarget;
        private float _untilSlow, _untilMiddle, _untilFast;
        private float _period;

        /// <summary>Set up to change about once in <paramref name="period"/> seconds, sampled at <paramref name="rate"/> calls to <see cref="Next"/> a second.</summary>
        public void Set(float period, float rate)
        {
            _period = period;
            _slow.SetTime(period, rate);
            _middle.SetTime(period * 0.25f, rate);
            _fast.SetTime(period * 0.0625f, rate);
        }

        /// <summary>Between about -1 and 1. <paramref name="step"/> is the seconds since the last call.</summary>
        public float Next(SoundRandom random, float step)
        {
            if ((_untilSlow -= step) <= 0f)
            {
                _untilSlow = random.Exponential(_period);
                _slowTarget = random.Bipolar();
            }
            if ((_untilMiddle -= step) <= 0f)
            {
                _untilMiddle = random.Exponential(_period * 0.25f);
                _middleTarget = random.Bipolar();
            }
            if ((_untilFast -= step) <= 0f)
            {
                _untilFast = random.Exponential(_period * 0.0625f);
                _fastTarget = random.Bipolar();
            }
            return _slow.Lowpass(_slowTarget) * 0.6f + _middle.Lowpass(_middleTarget) * 0.3f + _fast.Lowpass(_fastTarget) * 0.15f;
        }
    }
}

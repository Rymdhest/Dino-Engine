namespace Dino_Engine.Sound.Synthesis
{
    /// <summary>
    /// A value over the seconds of a sound: a filter's sweep, a pitch falling, a force rising. Anything taking one also takes a
    /// plain number, so a fixed cutoff and a moving one are written the same way.
    /// </summary>
    public readonly struct Curve
    {
        private readonly Func<float, float>? _function;
        private readonly float _value;

        public Curve(Func<float, float> function)
        {
            _function = function;
            _value = 0f;
        }

        private Curve(float value)
        {
            _function = null;
            _value = value;
        }

        public static implicit operator Curve(float value) => new Curve(value);

        public bool IsConstant => _function == null;

        public float At(float seconds) => _function != null ? _function(seconds) : _value;

        /// <summary>A straight line, then holding.</summary>
        public static Curve Line(float from, float to, float seconds)
        {
            return new Curve(t => t >= seconds ? to : from + (to - from) * (t / seconds));
        }

        /// <summary>From one value to another evenly in ratio, then holding: how a pitch or a cutoff should sweep, as they're heard in octaves.</summary>
        public static Curve Glide(float from, float to, float seconds)
        {
            float rate = MathF.Log(to / from) / seconds;
            return new Curve(t => t >= seconds ? to : from * MathF.Exp(rate * t));
        }

        /// <summary>Falling away from one value towards another, reaching about 63% of the way in <paramref name="timeConstant"/>. Never quite there, as things that lose energy are not.</summary>
        public static Curve Settle(float from, float to, float timeConstant)
        {
            return new Curve(t => to + (from - to) * MathF.Exp(-t / timeConstant));
        }

        /// <summary>Straight lines between points given as (seconds, value), holding the last.</summary>
        public static Curve Points(params (float time, float value)[] points)
        {
            return new Curve(t =>
            {
                if (t <= points[0].time) return points[0].value;
                for (int i = 1; i < points.Length; i++)
                {
                    if (t > points[i].time) continue;
                    float span = points[i].time - points[i - 1].time;
                    float along = span > 0f ? (t - points[i - 1].time) / span : 1f;
                    return points[i - 1].value + (points[i].value - points[i - 1].value) * along;
                }
                return points[^1].value;
            });
        }

        /// <summary>Rising to a peak at <paramref name="peak"/> seconds and falling back to nothing by <paramref name="end"/>, smooth at both ends: a swing, a gust, the wash of a wave.</summary>
        public static Curve Hump(float peak, float end, float height = 1f, float sharpness = 1f)
        {
            return new Curve(t =>
            {
                if (t <= 0f || t >= end) return 0f;
                float x = t < peak ? t / peak : (end - t) / (end - peak);
                float smooth = x * x * (3f - 2f * x);
                return height * (sharpness == 1f ? smooth : MathF.Pow(smooth, sharpness));
            });
        }

        public static Curve operator *(Curve curve, float scale)
        {
            if (curve.IsConstant) return curve._value * scale;
            Func<float, float> inner = curve._function!;
            return new Curve(t => inner(t) * scale);
        }

        public static Curve operator *(Curve a, Curve b)
        {
            if (a.IsConstant && b.IsConstant) return a._value * b._value;
            return new Curve(t => a.At(t) * b.At(t));
        }

        public static Curve operator +(Curve a, Curve b)
        {
            if (a.IsConstant && b.IsConstant) return a._value + b._value;
            return new Curve(t => a.At(t) + b.At(t));
        }
    }
}

using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>
    /// A number that changes along something, through points that needn't be evenly spaced: a blade's width along its length, a pommel's radius up its height. Between points it's a
    /// monotone cubic, which can't overshoot, so a width that's above nothing at every point is above nothing all the way along.
    /// </summary>
    public sealed class Profile
    {
        private readonly float[] _at;
        private readonly float[] _value;
        private readonly float[] _slope;

        public Profile(params (float at, float value)[] keys)
        {
            if (keys.Length < 2) throw new ArgumentException("A profile needs at least two points.");
            _at = keys.Select(k => k.at).ToArray();
            _value = keys.Select(k => k.value).ToArray();
            _slope = new float[keys.Length];

            int last = keys.Length - 1;
            var secant = new float[last];
            for (int i = 0; i < last; i++) secant[i] = (_value[i + 1] - _value[i]) / MathF.Max(_at[i + 1] - _at[i], 1e-5f);

            _slope[0] = secant[0];
            _slope[last] = secant[last - 1];
            for (int i = 1; i < last; i++)
            {
                // Flat where it turns, which is what keeps it from overshooting.
                _slope[i] = secant[i - 1] * secant[i] <= 0f ? 0f : 2f * secant[i - 1] * secant[i] / (secant[i - 1] + secant[i]);
            }
        }

        public float At(float t)
        {
            if (t <= _at[0]) return _value[0];
            int last = _at.Length - 1;
            if (t >= _at[last]) return _value[last];

            int i = 0;
            while (t > _at[i + 1]) i++;

            float span = _at[i + 1] - _at[i];
            float s = (t - _at[i]) / MathF.Max(span, 1e-5f);
            float s2 = s * s;
            float s3 = s2 * s;
            return (2f * s3 - 3f * s2 + 1f) * _value[i] + (s3 - 2f * s2 + s) * span * _slope[i] + (-2f * s3 + 3f * s2) * _value[i + 1] + (s3 - s2) * span * _slope[i + 1];
        }
    }

    /// <summary>A smooth line through points, for the spine of anything swept along one.</summary>
    public static class Spine
    {
        /// <summary><paramref name="steps"/> + 1 points along a Catmull-Rom curve through these, going through every one of them.</summary>
        public static List<Vector3> Through(IReadOnlyList<Vector3> points, int steps)
        {
            var result = new List<Vector3>(steps + 1);
            int spans = points.Count - 1;
            for (int i = 0; i <= steps; i++)
            {
                float along = (float)i / steps * spans;
                int span = Math.Min((int)along, spans - 1);
                float t = along - span;

                Vector3 p1 = points[span];
                Vector3 p2 = points[span + 1];
                Vector3 p0 = span > 0 ? points[span - 1] : p1 * 2f - p2;
                Vector3 p3 = span + 2 < points.Count ? points[span + 2] : p2 * 2f - p1;

                float t2 = t * t;
                float t3 = t2 * t;
                result.Add(0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3));
            }
            return result;
        }
    }
}

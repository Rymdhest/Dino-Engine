using OpenTK.Mathematics;

namespace Dino_Engine.Particles
{
    /// <summary>
    /// A number over a particle's life, from 0 at birth to 1 at death: straight lines between keys, held flat past the first and last. The renderer bakes it into
    /// <see cref="Samples"/> steps of a texture row (<see cref="ParticleStyle"/>), so it costs one lookup however many keys it has.
    /// </summary>
    public sealed class Curve
    {
        public const int Samples = 64;

        private readonly (float Time, float Value)[] _keys;

        public Curve(params (float Time, float Value)[] keys)
        {
            if (keys.Length == 0) throw new ArgumentException("A curve needs a key.");
            _keys = keys.OrderBy(k => k.Time).ToArray();
        }

        public static implicit operator Curve(float value) => new Curve((0f, value));

        /// <summary>From <paramref name="from"/> at birth to <paramref name="to"/> at death.</summary>
        public static Curve Line(float from, float to) => new Curve((0f, from), (1f, to));

        /// <summary>Up from nothing by <paramref name="rise"/>, held, and down to nothing from <paramref name="fall"/>: what most things that appear and go do.</summary>
        public static Curve Fade(float rise, float fall, float peak = 1f) => new Curve((0f, 0f), (rise, peak), (fall, peak), (1f, 0f));

        public float this[float t]
        {
            get
            {
                if (t <= _keys[0].Time) return _keys[0].Value;
                for (int i = 1; i < _keys.Length; i++)
                {
                    if (t > _keys[i].Time) continue;
                    var (t0, v0) = _keys[i - 1];
                    var (t1, v1) = _keys[i];
                    return t1 <= t0 ? v1 : v0 + (v1 - v0) * ((t - t0) / (t1 - t0));
                }
                return _keys[^1].Value;
            }
        }

        public float Largest => _keys.Max(k => k.Value);
    }

    /// <summary>A colour over a particle's life, as <see cref="Curve"/> is a number: linear colours, straight lines between keys.</summary>
    public sealed class Gradient
    {
        private readonly (float Time, Vector3 Colour)[] _keys;

        public Gradient(params (float Time, Vector3 Colour)[] keys)
        {
            if (keys.Length == 0) throw new ArgumentException("A gradient needs a key.");
            _keys = keys.OrderBy(k => k.Time).ToArray();
        }

        public static implicit operator Gradient(Vector3 colour) => new Gradient((0f, colour));

        public static Gradient Line(Vector3 from, Vector3 to) => new Gradient((0f, from), (1f, to));

        public Vector3 this[float t]
        {
            get
            {
                if (t <= _keys[0].Time) return _keys[0].Colour;
                for (int i = 1; i < _keys.Length; i++)
                {
                    if (t > _keys[i].Time) continue;
                    var (t0, c0) = _keys[i - 1];
                    var (t1, c1) = _keys[i];
                    return t1 <= t0 ? c1 : Vector3.Lerp(c0, c1, (t - t0) / (t1 - t0));
                }
                return _keys[^1].Colour;
            }
        }
    }
}

using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Flora
{
    /// <summary>The outlines a crown can take, as Weber and Penn's ShapeRatio table: how far out a crown reaches at each height.</summary>
    public enum CrownShape
    {
        Conical,        // Widest at the bottom, to a point.
        Spherical,
        Hemispherical,  // Domed, widest low down.
        Cylindrical,    // The same all the way up.
        TaperedColumn,
        Flame,          // Widest high up, to a point.
        InverseCone,    // A vase: narrow at the bottom, widest at the top.
        Umbrella,       // Flat on top and spreading, as an acacia or an old pine.
    }

    /// <summary>
    /// Where a crown is allowed to be. Growth keeps inside this, so a species' shape is held here and not in the luck of a seed, and it's also what says which way is "out" when
    /// foliage is shaded. A crown is never a clean solid: <see cref="Bumps"/> push its edge about, and a few big lobes cut it into the masses that read from above.
    /// </summary>
    public sealed class Envelope
    {
        public readonly CrownShape Shape;
        public readonly float Bottom;
        public readonly float Top;
        public readonly float Radius;
        public readonly Vector2 Offset;     // How far the crown has wandered from the foot of the tree.
        private readonly float _bumps;
        private readonly uint _seed;

        public Envelope(CrownShape shape, float bottom, float top, float radius, Vector2 offset, float bumps, uint seed)
        {
            Shape = shape;
            Bottom = bottom;
            Top = top;
            Radius = radius;
            Offset = offset;
            _bumps = bumps;
            _seed = seed;
        }

        public Vector3 Centre => new Vector3(Offset.X, (Bottom + Top) * 0.5f, Offset.Y);

        /// <summary>The share of the widest radius a shape has at a height, 0 at the crown's foot and 1 at its top. Weber and Penn 4.3, with the ends brought in so that a crown closes.</summary>
        public static float Ratio(CrownShape shape, float height)
        {
            float h = Math.Clamp(height, 0f, 1f);
            switch (shape)
            {
                case CrownShape.Conical: return 1f - 0.92f * h;
                case CrownShape.Spherical: return 0.15f + 0.85f * MathF.Sin(MathF.PI * h);
                case CrownShape.Hemispherical: return 0.15f + 0.85f * MathF.Sin(MathF.PI * (0.18f + 0.82f * MathF.Pow(h, 1.25f)));
                case CrownShape.Cylindrical: return 0.75f + 0.25f * MathF.Sin(MathF.PI * h);
                case CrownShape.TaperedColumn: return 0.5f + 0.5f * (1f - h);
                case CrownShape.Flame: return h <= 0.7f ? 0.1f + 0.9f * h / 0.7f : 0.1f + 0.9f * (1f - h) / 0.3f;
                case CrownShape.InverseCone: return 0.2f + 0.8f * MathF.Pow(h, 0.7f) * (h > 0.85f ? (1f - h) / 0.15f * 0.6f + 0.4f : 1f);
                case CrownShape.Umbrella: return 0.3f + 0.7f * MathF.Pow(h, 0.45f) * (h > 0.8f ? MathF.Sqrt(MathF.Max(1f - (h - 0.8f) / 0.2f, 0f)) * 0.75f + 0.25f : 1f);
                default: return 1f;
            }
        }

        /// <summary>How far inside a point is: 1 on the axis, 0 at the edge and below 0 outside.</summary>
        public float Inside(Vector3 at)
        {
            float height = (at.Y - Bottom) / MathF.Max(Top - Bottom, 1e-3f);
            if (height < -0.02f || height > 1.02f) return -1f;

            Vector2 off = at.Xz - Offset;
            float reach = Radius * Ratio(Shape, height);
            if (_bumps > 0f)
            {
                Vector3 sample = new Vector3(off.X, at.Y, off.Y) / MathF.Max(Radius * 0.45f, 0.5f);
                reach *= 1f + _bumps * (FloraNoise.Fbm(sample, _seed, 2) * 2f - 1f) * 1.6f;
            }
            return 1f - off.Length / MathF.Max(reach, 1e-3f);
        }

        /// <summary>Which way is out of the crown from here, for shading: mostly away from the middle and a little up, as light comes from above.</summary>
        public Vector3 Outward(Vector3 at)
        {
            Vector3 from = at - Centre;
            from.Y *= Radius / MathF.Max((Top - Bottom) * 0.5f, 1e-3f);
            float length = from.Length;
            return length > 1e-4f ? from / length : Vector3.UnitY;
        }
    }
}

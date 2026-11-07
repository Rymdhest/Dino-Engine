using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Flora
{
    /// <summary>
    /// How much light reaches anywhere in a crown, as a coarse grid: shadow propagation (Palubicki et al. 2009, section 4.1). Each thing in the crown casts a pyramid of shade on
    /// the cells below it, fading with depth by a * b^-q. It's what buds compete for while a tree grows, and afterwards it's what says which leaves are deep in the shade, which is
    /// most of what makes a crown read as a mass from above.
    /// </summary>
    public sealed class ShadowGrid
    {
        private readonly float[] _shade;
        private readonly int _width;
        private readonly int _height;
        private readonly Vector3 _min;
        private readonly float _cell;
        private readonly float _strength;   // a
        private readonly float _falloff;    // b
        private readonly int _depth;        // qmax
        private readonly float[] _layers;

        /// <param name="cell">Metres to a cell, about an internode (Palubicki section 6).</param>
        public ShadowGrid(Vector3 min, Vector3 max, float cell, float strength = 0.35f, float falloff = 1.6f, int depth = 6)
        {
            _min = min;
            _cell = cell;
            _strength = strength;
            _falloff = falloff;
            _depth = depth;
            Vector3 size = max - min;
            _width = Math.Clamp((int)MathF.Ceiling(MathF.Max(size.X, size.Z) / cell) + 1, 4, 96);
            _height = Math.Clamp((int)MathF.Ceiling(size.Y / cell) + 1, 4, 128);
            _shade = new float[_width * _width * _height];

            _layers = new float[depth + 1];
            for (int q = 0; q <= depth; q++) _layers[q] = strength * MathF.Pow(falloff, -q);
        }

        public void Clear() => Array.Clear(_shade);

        private int Cell(float value, float origin, int cells) => Math.Clamp((int)((value - origin) / _cell), 0, cells - 1);

        private int Index(int x, int y, int z) => (y * _width + z) * _width + x;

        public void Cast(Vector3 at)
        {
            int cx = Cell(at.X, _min.X, _width), cy = Cell(at.Y, _min.Y, _height), cz = Cell(at.Z, _min.Z, _width);
            for (int q = 0; q <= _depth; q++)
            {
                int y = cy - q;
                if (y < 0) break;
                float shade = _layers[q];
                int fromX = Math.Max(cx - q, 0), toX = Math.Min(cx + q, _width - 1);
                int fromZ = Math.Max(cz - q, 0), toZ = Math.Min(cz + q, _width - 1);
                for (int z = fromZ; z <= toZ; z++)
                {
                    int row = Index(0, y, z);
                    for (int x = fromX; x <= toX; x++) _shade[row + x] += shade;
                }
            }
        }

        /// <summary>The light at a point, 1 in the open: Q = max(C - s + a, 0), which takes away a thing's own shade from itself.</summary>
        public float Light(Vector3 at)
        {
            int x = Cell(at.X, _min.X, _width), y = Cell(at.Y, _min.Y, _height), z = Cell(at.Z, _min.Z, _width);
            return Math.Clamp(1f - _shade[Index(x, y, z)] + _strength, 0f, 1f);
        }

        /// <summary>Which way there's more light: the negative gradient of the shade.</summary>
        public Vector3 TowardsLight(Vector3 at)
        {
            int x = Cell(at.X, _min.X, _width), y = Cell(at.Y, _min.Y, _height), z = Cell(at.Z, _min.Z, _width);
            int left = Math.Max(x - 1, 0), right = Math.Min(x + 1, _width - 1);
            int down = Math.Max(y - 1, 0), up = Math.Min(y + 1, _height - 1);
            int back = Math.Max(z - 1, 0), front = Math.Min(z + 1, _width - 1);
            Vector3 gradient = new Vector3(
                _shade[Index(left, y, z)] - _shade[Index(right, y, z)],
                _shade[Index(x, down, z)] - _shade[Index(x, up, z)],
                _shade[Index(x, y, back)] - _shade[Index(x, y, front)]);
            float length = gradient.Length;
            return length > 1e-5f ? gradient / length : Vector3.UnitY;
        }
    }
}

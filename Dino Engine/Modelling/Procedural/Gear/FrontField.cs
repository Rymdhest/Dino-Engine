using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>
    /// How far forward something comes at each height and across, as seen from straight in front: its triangles drawn into a grid, the furthest forward kept, over a floor given for where
    /// nothing was drawn. It's for laying one thing over another, as a necklace lies on whatever's on the chest (<see cref="Character.Outfit.Necklace"/>). Headless, and made fresh where it's used.
    /// </summary>
    public sealed class FrontField
    {
        private readonly float _x0, _y0, _cell;
        private readonly int _columns, _rows;
        private readonly float[] _front;

        public FrontField(float x0, float x1, float y0, float y1, float cell, Func<float, float, float> floor)
        {
            _x0 = x0;
            _y0 = y0;
            _cell = cell;
            _columns = Math.Max((int)MathF.Ceiling((x1 - x0) / cell), 1) + 1;
            _rows = Math.Max((int)MathF.Ceiling((y1 - y0) / cell), 1) + 1;
            _front = new float[_columns * _rows];
            for (int j = 0; j < _rows; j++)
            {
                for (int i = 0; i < _columns; i++) _front[j * _columns + i] = floor(x0 + i * cell, y0 + j * cell);
            }
        }

        /// <summary>Draws every triangle of a mesh in, where it comes further forward than what's there.</summary>
        public void Add(GearMesh mesh)
        {
            IReadOnlyList<Model.Vertex> vertices = mesh.Vertices;
            IReadOnlyList<int> indices = mesh.Indices;
            for (int t = 0; t + 2 < indices.Count; t += 3)
            {
                Vector3 a = vertices[indices[t]].position, b = vertices[indices[t + 1]].position, c = vertices[indices[t + 2]].position;
                float area = (b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y);
                if (MathF.Abs(area) < 1e-9f) continue;
                int i0 = Math.Max((int)MathF.Ceiling((MathF.Min(a.X, MathF.Min(b.X, c.X)) - _x0) / _cell), 0);
                int i1 = Math.Min((int)MathF.Floor((MathF.Max(a.X, MathF.Max(b.X, c.X)) - _x0) / _cell), _columns - 1);
                int j0 = Math.Max((int)MathF.Ceiling((MathF.Min(a.Y, MathF.Min(b.Y, c.Y)) - _y0) / _cell), 0);
                int j1 = Math.Min((int)MathF.Floor((MathF.Max(a.Y, MathF.Max(b.Y, c.Y)) - _y0) / _cell), _rows - 1);
                for (int j = j0; j <= j1; j++)
                {
                    float y = _y0 + j * _cell;
                    for (int i = i0; i <= i1; i++)
                    {
                        float x = _x0 + i * _cell;
                        float wa = ((b.X - x) * (c.Y - y) - (c.X - x) * (b.Y - y)) / area;
                        float wb = ((c.X - x) * (a.Y - y) - (a.X - x) * (c.Y - y)) / area;
                        float wc = 1f - wa - wb;
                        if (wa < -1e-4f || wb < -1e-4f || wc < -1e-4f) continue;
                        float z = wa * a.Z + wb * b.Z + wc * c.Z;
                        ref float kept = ref _front[j * _columns + i];
                        if (z > kept) kept = z;
                    }
                }
            }
        }

        /// <summary>The furthest forward it comes anywhere within <paramref name="reach"/> of a point across and up, for laying something that wide on it, as a link of a chain is.</summary>
        public float Around(float x, float y, float reach)
        {
            int i0 = Math.Clamp((int)MathF.Floor((x - reach - _x0) / _cell), 0, _columns - 1), i1 = Math.Clamp((int)MathF.Ceiling((x + reach - _x0) / _cell), 0, _columns - 1);
            int j0 = Math.Clamp((int)MathF.Floor((y - reach - _y0) / _cell), 0, _rows - 1), j1 = Math.Clamp((int)MathF.Ceiling((y + reach - _y0) / _cell), 0, _rows - 1);
            float most = float.NegativeInfinity;
            for (int j = j0; j <= j1; j++)
            {
                for (int i = i0; i <= i1; i++) most = MathF.Max(most, _front[j * _columns + i]);
            }
            return most;
        }

        /// <summary>How far forward it comes here, the furthest of the grid's corners round the point, so that what's laid on it never dips between them.</summary>
        public float At(float x, float y)
        {
            float u = (x - _x0) / _cell, v = (y - _y0) / _cell;
            int i = Math.Clamp((int)MathF.Floor(u), 0, _columns - 2), j = Math.Clamp((int)MathF.Floor(v), 0, _rows - 2);
            float most = _front[j * _columns + i];
            most = MathF.Max(most, _front[j * _columns + i + 1]);
            most = MathF.Max(most, _front[(j + 1) * _columns + i]);
            return MathF.Max(most, _front[(j + 1) * _columns + i + 1]);
        }
    }
}

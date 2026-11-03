using OpenTK.Mathematics;

namespace Dino_Engine.World
{
    /// <summary>
    /// A grid of 0..1 values stored as bytes, for the masks that cover a whole world at fine resolution. Laid out like
    /// <see cref="ScalarField"/>: the value at node (x, z) belongs to world position (x, z) * <see cref="CellSize"/>.
    /// </summary>
    public sealed class ByteField
    {
        private const float Unit = 1f / 255f;

        public readonly int Width;
        public readonly int Depth;
        public readonly float CellSize;
        public readonly byte[] Data;

        private readonly float _inverseCell;

        public ByteField(int width, int depth, float cellSize)
        {
            Width = width;
            Depth = depth;
            CellSize = cellSize;
            Data = new byte[width * depth];
            _inverseCell = 1f / cellSize;
        }

        public static ByteField ForWorld(float worldSize, float cellSize)
        {
            int nodes = (int)MathF.Ceiling(worldSize / cellSize) + 1;
            return new ByteField(nodes, nodes, cellSize);
        }

        /// <summary>Raises the value at a node to <paramref name="value"/> if that is higher.</summary>
        public void Raise(int x, int z, float value)
        {
            int i = z * Width + x;
            byte encoded = (byte)Math.Clamp((int)(value * 255f + 0.5f), 0, 255);
            if (encoded > Data[i]) Data[i] = encoded;
        }

        public float Sample(Vector2 position) => Sample(position.X, position.Y);

        /// <summary>Bilinear, 0..1.</summary>
        public float Sample(float worldX, float worldZ)
        {
            float fx = worldX * _inverseCell;
            float fz = worldZ * _inverseCell;
            int x = (int)MathF.Floor(fx);
            int z = (int)MathF.Floor(fz);
            float tx = fx - x;
            float tz = fz - z;

            if (x < 0) { x = 0; tx = 0f; }
            else if (x >= Width - 1) { x = Width - 2; tx = 1f; }
            if (z < 0) { z = 0; tz = 0f; }
            else if (z >= Depth - 1) { z = Depth - 2; tz = 1f; }

            int i = z * Width + x;
            float near = Data[i] + (Data[i + 1] - Data[i]) * tx;
            float far = Data[i + Width] + (Data[i + Width + 1] - Data[i + Width]) * tx;
            return (near + (far - near) * tz) * Unit;
        }

        /// <summary>The nearest node's value is above one half.</summary>
        public bool IsSet(Vector2 position)
        {
            int x = Math.Clamp((int)MathF.Round(position.X * _inverseCell), 0, Width - 1);
            int z = Math.Clamp((int)MathF.Round(position.Y * _inverseCell), 0, Depth - 1);
            return Data[z * Width + x] > 127;
        }
    }
}

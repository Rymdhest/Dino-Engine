using Dino_Engine.Textures;
using OpenTK.Graphics.OpenGL;

namespace Dino_Engine.Rendering
{
    /// <summary>
    /// What every shader that reads a tiling material needs to know of it besides its textures (Shaders/Surface/MaterialTable.glsl): its relief over its tile's
    /// width, which its normal map was made with and its parallax marches to, and the metres a tile spans. Bound once, for good, as nothing else uses its binding.
    /// </summary>
    public static class MaterialTableBuffer
    {
        public const int Binding = 12;
        public const int MaxMaterials = 128;

        private static int _buffer;

        public static void Upload()
        {
            if (MaterialCatalog.Count > MaxMaterials) throw new InvalidOperationException($"There are {MaterialCatalog.Count} materials, and the table holds {MaxMaterials}.");
            float[] data = new float[MaxMaterials * 4];
            foreach (MaterialSpec spec in MaterialCatalog.All)
            {
                data[spec.Index * 4] = spec.ReliefRatio;
                data[spec.Index * 4 + 1] = spec.TileMetres;
            }
            if (_buffer == 0) _buffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.UniformBuffer, _buffer);
            GL.BufferData(BufferTarget.UniformBuffer, data.Length * sizeof(float), data, BufferUsageHint.StaticDraw);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, Binding, _buffer);
        }

        public static void CleanUp()
        {
            if (_buffer != 0) GL.DeleteBuffer(_buffer);
            _buffer = 0;
        }
    }
}

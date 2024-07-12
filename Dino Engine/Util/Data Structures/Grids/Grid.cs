using Dino_Engine.Modelling.Model;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;

namespace Dino_Engine.Util
{
    public abstract class Grid <T>
    {
        private T[,] _grid;
        private Vector2i _resolution;
        public T[,] Values { get => _grid; set => _grid = value; }
        public Vector2i Resolution { get => _resolution;}

        private int texture = -1;

        public Grid(Vector2i resolution)
        {
            _grid = new T[resolution.X, resolution.Y];
           _resolution = resolution;
        }
        public abstract T BilinearInterpolate(Vector2 p);

        protected abstract int GenerateTexture();
        public int GetTexture()
        {
            if (texture == -1)
            {
                texture = GenerateTexture();
            }
            return texture;
        }
        public void cleanUp()
        {
            GL.DeleteTexture(texture);
        }
    }
}

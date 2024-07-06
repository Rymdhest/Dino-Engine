using Dino_Engine.Modelling.Model;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.Util
{
    public class Grid
    {
        private float[,] _grid;
        private Vector2i _resolution;
        public float[,] Values { get => _grid; set => _grid = value; }
        public Vector2i Resolution { get => _resolution;}

        public Grid(Vector2i resolution)
        {
            _grid = new float[resolution.X, resolution.Y];
           _resolution = resolution;
        }
        public float BilinearInterpolate(Vector2 p)
        {
            int x1 = (int)Math.Floor(p.X);
            int x2 = x1 + 1;
            int y1 = (int)Math.Floor(p.Y);
            int y2 = y1 + 1;

            x1 = Math.Clamp(x1, 0, Resolution.X-1);
            x2 = Math.Clamp(x2, 0, Resolution.X - 1);
            y1 = Math.Clamp(y1, 0, Resolution.Y - 1);
            y2 = Math.Clamp(y2, 0, Resolution.Y - 1);

            float t_x = p.X - x1;
            float t_y = p.Y - y1;

            float heightQ11 = _grid[x1, y1];
            float heightQ12 = _grid[x2, y1];
            float heightQ21 = _grid[x1, y2];
            float heightQ22 = _grid[x2, y2];

            // Interpolate along the x-axis (bottom and top rows)
            float interpolatedHeight1 = heightQ11 * (1 - t_x) + heightQ12 * t_x;
            float interpolatedHeight2 = heightQ21 * (1 - t_x) + heightQ22 * t_x;

            // Interpolate along the y-axis (left and right columns)
            float interpolatedHeight = interpolatedHeight1 * (1 - t_y) + interpolatedHeight2 * t_y;

            return interpolatedHeight;
        }
    }
}

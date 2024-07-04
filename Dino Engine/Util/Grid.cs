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

    }
}

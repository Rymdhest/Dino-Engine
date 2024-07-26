using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.Debug
{
    public class Transformation2D
    {
        public Vector2 position;
        public float rotation;
        public Vector2 scale;

        public Transformation2D(Vector2 position, float rotation, Vector2 scale)
        {
            this.position = position;
            this.rotation = rotation;
            this.scale = scale;
        }

    }
}

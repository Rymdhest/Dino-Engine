using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.ECS.Components
{
    public class DirectionComponent : Component
    {
        private Vector3 _direction;
        public Vector3 Direction { get => _direction; set => _direction = value.Normalized(); }

        public DirectionComponent(Vector3 direction)
        {
            Direction = direction;
        }

    }
}

using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.ECS.Components
{
    internal class DirectionComponent : Component
    {
        private Vector3 _direction;
        public Vector3 Direction { get => _direction; set => _direction = value; }

        public DirectionComponent(Vector3 direction)
        {
            _direction = direction;
        }

    }
}

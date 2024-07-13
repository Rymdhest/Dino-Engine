using Dino_Engine.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.ECS.Components
{
    public class AmbientLightComponent : Component
    {
        float _ambientFactor;
        public const float DEFAULT = 0.0f;

        public float AmbientFactor { get => _ambientFactor; set => _ambientFactor = value; }

        public AmbientLightComponent(float ambientFactor)
        {
            _ambientFactor = ambientFactor;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.ECS.Components
{
    public class SelfDestroyComponent : Component
    {
        public float secondsRemaining;
        public float initialSeconds;
        public SelfDestroyComponent(float seconds)
        {
            initialSeconds = seconds;
            secondsRemaining = seconds;
        }
    }
}

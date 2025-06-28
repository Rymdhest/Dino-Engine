using Dino_Engine.Core;
using Dino_Engine.ECS.Systems;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.ECS.ComponentsOLD
{
    public class SelfDestroyComponent : ComponentOLD
    {
        public float secondsRemaining;
        public float initialSeconds;
        public SelfDestroyComponent(float seconds)
        {
            initialSeconds = seconds;
            secondsRemaining = seconds;
        }

        public float getRemainingRatio()
        {
            return secondsRemaining / initialSeconds;
        }
    }
}

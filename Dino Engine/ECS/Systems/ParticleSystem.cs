using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.ECS.Systems
{
    public class ParticleSystem : ComponentSystem
    {

        public ParticleSystem()
        {
            addRequiredComponent<TransformationComponent>();
            addRequiredComponent<SelfDestroyComponent>();
            addRequiredComponent<ColourComponent>();
            addRequiredComponent<VelocityComponent>();
            addRequiredComponent<MassComponent>();
        }

        internal override void UpdateEntity(Entity entity)
        {
            float mix = 1f- entity.getComponent<SelfDestroyComponent>().getRemainingRatio();

            Colour particleColourStart = new Colour(100, 40, 10, 0.3f, .03f);
            Colour particleColourEnd = new Colour(45, 45, 45, 0.01f, 0.0f);

            Colour color = Colour.mix(particleColourStart, particleColourEnd, mix);
            entity.getComponent<ColourComponent>().Colour = color;

        }
    }
}

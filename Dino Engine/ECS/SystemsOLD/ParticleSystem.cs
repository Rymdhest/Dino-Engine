using Dino_Engine.Core;
using Dino_Engine.ECS.ComponentsOLD;
using Dino_Engine.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.ECS.SystemsOLD
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

        internal override void UpdateEntity(EntityOLD entity)
        {
            float mix = 1f - entity.getComponent<SelfDestroyComponent>().getRemainingRatio();

            //Colour particleColourStart = new Colour(100, 40, 10, 0.3f, .03f);
            Colour particleColourStart = new Colour(45, 45, 45, 1.01f, 0.5f);
            Colour particleColourEnd = new Colour(255, 255, 255, 0.51f, 0.0f);

            Colour color = Colour.mix(particleColourStart, particleColourEnd, mix);
            entity.getComponent<ColourComponent>().Colour = color;

        }
    }
}

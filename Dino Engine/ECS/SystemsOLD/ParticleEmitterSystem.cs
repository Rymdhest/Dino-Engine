using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ComponentsOLD;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.SystemsOLD
{
    public class ParticleEmitterSystem : ComponentSystem
    {
        public ParticleEmitterSystem() : base()
        {
            addRequiredComponent<DirectionComponent>();
            addRequiredComponent<ParticleEmitterComponent>();
            addRequiredComponent<TransformationComponent>();
        }

        internal override void UpdateEntity(EntityOLD entity)
        {
            ParticleEmitterComponent emitter = entity.getComponent<ParticleEmitterComponent>();

            float particlesToCreate = emitter.particlesPerSecond * Engine.Delta;
            int count = (int)particlesToCreate;
            float partialParticle = particlesToCreate % 1;

            for (int i = 0; i < count; i++)
            {
                emitParticle(entity);
            }
            if (MyMath.rng() < partialParticle)
            {
                emitParticle(entity);
            }
        }

        private void emitParticle(EntityOLD entity)
        {
            ParticleEmitterComponent emitter = entity.getComponent<ParticleEmitterComponent>();
            Vector3 direction = entity.getComponent<DirectionComponent>().Direction;
            Transformation transformation = entity.getComponent<TransformationComponent>().Transformation;

            EntityOLD particle = new EntityOLD("particle");
            particle.addComponent(new TransformationComponent(new Transformation(transformation.position + MyMath.rng3DMinusPlus(emitter.particlePositionError), new Vector3(0), new Vector3(emitter.particleSizeStart * (1f + MyMath.rngMinusPlus(emitter.particleSizeError))))));
            direction = (direction + MyMath.rng3DMinusPlus(emitter.particleDirectionError)).Normalized();
            particle.addComponent(new VelocityComponent(direction * emitter.particleSpeed * (1f + MyMath.rngMinusPlus(emitter.particleSpeedError))));
            particle.addComponent(new MassComponent(emitter.particleWeight * (1f + MyMath.rngMinusPlus(emitter.particleWeightError))));
            particle.addComponent(new ColourComponent(emitter.particleColourStart));
            particle.addComponent(new SelfDestroyComponent(emitter.particleDuration * (1f + MyMath.rngMinusPlus(emitter.particleDurationError))));

            Engine.Instance.ECSEngine.AddEnityToSystem<ParticleSystem>(particle);
            Engine.Instance.ECSEngine.AddEnityToSystem<VelocitySystem>(particle);
            Engine.Instance.ECSEngine.AddEnityToSystem<GravitySystem>(particle);
            Engine.Instance.ECSEngine.AddEnityToSystem<SelfDestroySystem>(particle);
        }
    }
}

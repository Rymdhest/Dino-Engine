using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using System.Runtime.CompilerServices;

namespace Dino_Engine.ECS.Systems
{
    public class ParticleEmitterSystem : SystemBase
    {
        public ParticleEmitterSystem()
            : base(new BitMask(
                typeof(ParticleEmitterComponent),
                typeof(LocalToWorldMatrixComponent),
                typeof(PositionComponent),
                typeof(DirectionNormalizedComponent)))
        {
        }
        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            var emitter = entity.Get<ParticleEmitterComponent>();
            var localToWorldMatrix = entity.Get<LocalToWorldMatrixComponent>().value;
            var direction = entity.Get<DirectionNormalizedComponent>().value;
            Vector3 worldPos = localToWorldMatrix.ExtractTranslation();
            Vector3 velocity = entity.GetOptional(new VelocityComponent(Vector3.Zero)).value;

            float particlesToCreate = emitter.particlesPerSecond * deltaTime;
            int count = (int)particlesToCreate;
            float partialParticle = particlesToCreate % 1;

            for (int i = 0; i < count; i++)
            {
                world.CreateEntity((CreateParticleComponents(emitter, worldPos, direction, velocity)).ToArray<IComponent>());
            }
            if (MyMath.rng() < partialParticle)
            {
                world.CreateEntity((CreateParticleComponents(emitter, worldPos, direction, velocity)).ToArray<IComponent>());
            }
        }

        private IEnumerable<IComponent> CreateParticleComponents(ParticleEmitterComponent emitter, Vector3 position, Vector3 direction, Vector3 velocity)
        {
            direction = direction + MyMath.rng3DMinusPlus(emitter.particleDirectionError);
            direction.Normalize();
            yield return new ParticleRenderTag();
            yield return new LocalToWorldMatrixComponent();
            yield return new PositionComponent (position);
            yield return new ScaleComponent(new Vector3(emitter.particleSizeStart));
            yield return new ColorComponent(emitter.particleColourStart);
            yield return new VelocityComponent (velocity+direction * emitter.particleSpeed * (1f + MyMath.rngMinusPlus(emitter.particleSpeedError)));
            yield return new MassComponent(emitter.particleWeight * (1f + MyMath.rngMinusPlus(emitter.particleWeightError)));
            yield return new selfDestroyComponent(emitter.particleDuration * (1f + MyMath.rngMinusPlus(emitter.particleDurationError)));
        }
    }
}

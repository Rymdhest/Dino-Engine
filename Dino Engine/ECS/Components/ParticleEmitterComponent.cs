using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    public struct ParticleEmitterComponent : IComponent
    {
        public float particlesPerSecond = 10f;
        public float particleSpeed = 10f;
        public float particleDuration = 5f;
        public float particleSizeStart = 1f;
        public float particleSizeEnd = 1f;
        public float particleWeight = -0.2f;


        public float particleWeightError = 0.1f;
        public float particleSizeError = 0.25f;
        public float particleDurationError = 0.3f;
        public float particleSpeedError = 0.2f;
        public float particleColourError = 0.1f;
        public float particleDirectionError = 0.5f;
        public float particlePositionError = 2.5f;

        public Colour particleColourStart = new Colour(255, 0, 0, 1f, 1f);
        public Colour particleColourEnd = new Colour(255, 55, 55, 1f, 1f);

        public bool parentEmitter = false;

        public ParticleEmitterComponent()
        {

        }
    }
}

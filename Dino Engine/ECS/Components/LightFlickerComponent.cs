using Dino_Engine.ECS.ECS_Architecture;

namespace Dino_Engine.ECS.Components
{
    /// <summary>A light that won't hold still: its brightness wanders below full by up to <see cref="Amount"/>.</summary>
    public struct LightFlickerComponent : IComponent
    {
        public float Amount;    // 0 steady, 1 dipping to nothing.
        public float Speed;     // Radians per second of the slowest wave.
        public float Phase;     // Keeps neighbouring lights out of step.

        public LightFlickerComponent(float amount, float speed, float phase)
        {
            Amount = amount;
            Speed = speed;
            Phase = phase;
        }

        /// <summary>How bright the light is at this time, 0 to 1.</summary>
        public readonly float Brightness(float time)
        {
            float t = time * Speed + Phase;
            float wave = MathF.Sin(t) * 0.5f + MathF.Sin(t * 2.3f + Phase * 1.7f) * 0.3f + MathF.Sin(t * 5.1f + Phase * 3.1f) * 0.2f;
            return 1f - Amount * (wave * 0.5f + 0.5f);
        }
    }

    /// <summary>A light that dies away over the life of its <see cref="selfDestroyComponent"/>.</summary>
    public struct LightPulseTag : IComponent
    {
    }
}

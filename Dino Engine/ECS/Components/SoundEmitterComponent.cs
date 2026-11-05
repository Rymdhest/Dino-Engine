using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Sound.Content;

namespace Dino_Engine.ECS.Components
{
    /// <summary>
    /// Makes an entity sound for as long as it exists and wherever it goes: a fire, a waterfall, a thing that hums. <see cref="Systems.SoundEmitterSystem"/>
    /// starts it, moves it and stops it, so nothing else has to remember to.
    /// </summary>
    public struct SoundEmitterComponent : IComponent
    {
        public LiveDefinition Definition;
        public float Gain;
        public float Parameter;     // What the sound's first parameter is set to.

        public SoundEmitterComponent(LiveDefinition definition, float parameter = 1f, float gain = 1f)
        {
            Definition = definition;
            Parameter = parameter;
            Gain = gain;
        }
    }
}

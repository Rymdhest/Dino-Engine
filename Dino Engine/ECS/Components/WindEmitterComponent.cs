using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Physics;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    /// <summary>
    /// Makes an entity a continuous source of wind at its position (needs a LocalToWorldMatrixComponent), such as a
    /// rotor's downwash or a fan. For one-off blasts use <see cref="Wind.AddBlast"/> instead.
    /// </summary>
    public struct WindEmitterComponent : IComponent
    {
        public WindEmitterType Type;   // Radial or Directional.
        public float Radius;           // Metres.
        public float Strength;         // Peak wind speed in m/s.
        public Vector3 Direction;      // Directional only, in the entity's local space (rotates with it).

        public WindEmitterComponent(WindEmitterType type, float radius, float strength, Vector3 direction = default)
        {
            Type = type;
            Radius = radius;
            Strength = strength;
            Direction = direction;
        }
    }
}

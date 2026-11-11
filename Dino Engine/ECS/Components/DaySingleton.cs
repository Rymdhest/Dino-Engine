using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.World;

namespace Dino_Engine.ECS.Components
{
    /// <summary>
    /// The world's day, for one that has one (<see cref="World.Content.WorldArchetype.DayAndNight"/>): the cycle, its clock and the moment it's at, which
    /// <see cref="Systems.DaySystem"/> works out each frame. The sun's direction follows it; its colour and the sky's are applied as light is read
    /// (<see cref="Systems.SkySYstem"/>), as night's and cloud's are. With no cycle the sun stands where the world put it, as it always has.
    /// </summary>
    public struct DaySingleton : IComponent
    {
        public DayCycle? Cycle;     // None, and nothing here does anything.
        public double Clock;        // Seconds: the same as the weather's, so a misty dawn is a dawn.
        public bool ClockGiven;     // The game owns the clock; otherwise DaySystem runs it.
        public DaySample Now;       // This frame's light.

        public readonly bool Live => Cycle != null;
    }
}

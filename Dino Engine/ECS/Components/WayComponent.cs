using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Procedural.Architecture.Waygates;
using Dino_Engine.Particles;

namespace Dino_Engine.ECS.Components
{
    /// <summary>
    /// An open way standing in a waygate, where this entity is (its position, rotation and scale are the gate's): what kind of gate it fills and what's seen in it.
    /// <see cref="Systems.WaySystem"/> does everything else: it opens it, lights what's around it, breathes motes out of it, and has it drawn (WAYGATES.md).
    /// </summary>
    public struct WayComponent : IComponent
    {
        public WaygateDesign Design;
        public WayLook Look;

        /// <summary>The system's own, from the moment it first sees the way: shared by every copy of the component, as a struct is copied about.</summary>
        public WayState State;

        public WayComponent(WaygateDesign design, WayLook look)
        {
            Design = design;
            Look = look;
            State = new WayState();
        }
    }

    /// <summary>What <see cref="Systems.WaySystem"/> keeps on a way it's showing.</summary>
    public sealed class WayState
    {
        internal bool Begun;
        internal float Born;                // Engine time it was first seen.
        internal bool Kindled;              // Whether it's been seen to open, which happens once.
        internal Entity Front, Back;        // Its lights.
        internal EffectRun? Shimmer, Through;
        internal bool ThroughTinted;
    }

    /// <summary>A light a way owns, so that its system never takes one that isn't its own, as entity ids are given out again.</summary>
    public struct WayLightComponent : IComponent
    {
        public WayState Owner;
    }
}

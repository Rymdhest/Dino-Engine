using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.World;

namespace Dino_Engine.ECS.Components
{
    /// <summary>
    /// The generated world a simulation runs in: its maps and footprints, with nothing of the GPU about it. Unlike
    /// <see cref="WorldStreamSingleton"/> a server has this too.
    /// </summary>
    public struct WorldDataSingleton : IComponent
    {
        public WorldData World;

        public WorldDataSingleton(WorldData world)
        {
            World = world;
        }
    }
}

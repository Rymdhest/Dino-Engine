using Dino_Engine.Animation;

namespace Dino_Engine.World
{
    /// <summary>The ground of a world, as feet find it.</summary>
    public sealed class WorldGround : IGround
    {
        public readonly WorldData World;

        public WorldGround(WorldData world)
        {
            World = world;
        }

        public float HeightAt(float x, float z) => World.Height.SampleCubic(x, z);
    }
}

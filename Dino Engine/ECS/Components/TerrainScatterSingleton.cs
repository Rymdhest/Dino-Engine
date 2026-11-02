using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Procedural.Terrain;
using OpenTK.Mathematics;
using System.Collections.Generic;

namespace Dino_Engine.ECS.Components
{
    /// <summary>
    /// Objects (trees, rocks, flowers, ...) scattered over the terrain. They are streamed together with
    /// the terrain chunks by <see cref="Systems.TerrainChunkSystem"/>: a chunk's objects are generated
    /// when the chunk is created and destroyed with it, so only the ring around the camera exists.
    /// </summary>
    public struct TerrainScatterSingleton : IComponent
    {
        public int Seed;
        public Vector2 BoundsMin;   // Objects are only placed inside [BoundsMin, BoundsMax) on XZ.
        public Vector2 BoundsMax;

        public List<TerrainScatterLayer> Layers = new();

        /// <summary>
        /// The live object entities of each chunk, keyed like <see cref="TerrainQuadTreeSingleton.Chunks"/>.
        /// </summary>
        public Dictionary<Vector2i, List<Entity>> ChunkInstances = new();

        public TerrainScatterSingleton(int seed, Vector2 boundsMin, Vector2 boundsMax)
        {
            Seed = seed;
            BoundsMin = boundsMin;
            BoundsMax = boundsMax;
        }
    }
}

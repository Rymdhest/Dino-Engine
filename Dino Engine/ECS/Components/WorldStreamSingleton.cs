using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Model;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using System.Collections.Generic;

namespace Dino_Engine.ECS.Components
{
    /// <summary>
    /// The generated world being played, and which of its objects exist as entities right now. Only what is near the camera
    /// does: <see cref="Systems.TerrainChunkSystem"/> brings sectors of placed objects and the ground cover of terrain chunks
    /// in and out as the camera moves.
    /// </summary>
    public struct WorldStreamSingleton : IComponent
    {
        public WorldData World;
        public WorldAssetLibrary Assets;
        public WorldGroundCover GroundCover;
        public glModel[] ObjectModels;      // By index in World.Objects.AssetIds.
        public IReadOnlyList<AssetLight>[] ObjectLights;    // Likewise.

        /// <summary>The live entities of each sector of placed objects.</summary>
        public Dictionary<Vector2i, List<Entity>> Sectors = new();

        /// <summary>The live ground cover entities of each terrain chunk, keyed like <see cref="TerrainQuadTreeSingleton.Chunks"/>.</summary>
        public Dictionary<Vector2i, List<Entity>> ChunkPlants = new();

        public WorldStreamSingleton(WorldData world, WorldAssetLibrary assets)
        {
            World = world;
            Assets = assets;
            GroundCover = new WorldGroundCover(world);
            ObjectModels = new glModel[world.Objects.AssetIds.Count];
            ObjectLights = new IReadOnlyList<AssetLight>[ObjectModels.Length];
            for (int i = 0; i < ObjectModels.Length; i++)
            {
                ObjectModels[i] = assets.Get(world.Objects.AssetIds[i]);
                ObjectLights[i] = assets.LightsOf(world.Objects.AssetIds[i]);
            }
        }
    }
}

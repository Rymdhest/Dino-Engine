using Dino_Engine.Core;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Procedural.Terrain;
using Dino_Engine.Util;
using Dino_Engine.Util.Data_Structures.Grids;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    public struct TerrainChunkComponent : IComponent, ICleanupComponent
    {
        public int normalHeightTextureArrayID;
        public FloatGrid heightGrid;
        public Vector3Grid normalGrid;
        public TerrainChunkComponent(FloatGrid heightGrid, Vector3Grid normalGrid, TerrainChunkSurface surface, Vector2 chunkPos, Vector2 chunkSize)
        {
            this.heightGrid = heightGrid;
            this.normalGrid = normalGrid;

            ECSWorld world = Engine.Instance.world;
            Entity gridEntity = world.GetSingleton<RenderSpatialGridSingleton>();
            SpatialGrid spatialGrid = world.GetComponent<RenderSpatialGridSingleton>(gridEntity).Grid;

            normalHeightTextureArrayID = Engine.RenderEngine._terrainRenderer.insertDataAndCarveChunk(
                            heightGrid, normalGrid, surface, chunkPos, chunkSize, spatialGrid
                        );
        }

        public void Cleanup()
        {
            Engine.RenderEngine._terrainRenderer.freeChunk(normalHeightTextureArrayID);
        }
    }
}

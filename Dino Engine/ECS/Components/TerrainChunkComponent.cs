using Dino_Engine.Core;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Util.Data_Structures.Grids;

namespace Dino_Engine.ECS.Components
{
    public struct TerrainChunkComponent : IComponent, ICleanupComponent
    {
        public int normalHeightTextureArrayID;
        public FloatGrid heightGrid;
        public Vector3Grid normalGrid;
        public FloatGrid grassGrid;
        public TerrainChunkComponent(FloatGrid heightGrid, Vector3Grid normalGrid, FloatGrid grassGrid)
        {
            this.heightGrid = heightGrid;
            this.normalGrid = normalGrid;
            this.grassGrid = grassGrid;

            normalHeightTextureArrayID = Engine.RenderEngine._terrainRenderer.insertDataToTextureArray(heightGrid, normalGrid, grassGrid);
        }

        public void Cleanup()
        {
            Engine.RenderEngine._terrainRenderer.freeChunk(normalHeightTextureArrayID);
        }
    }
}

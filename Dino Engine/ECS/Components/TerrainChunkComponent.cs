using Dino_Engine.Core;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Util.Data_Structures.Grids;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    public struct TerrainChunkComponent : IComponent, ICleanupComponent
    {
        public int normalHeightTextureArrayID;
        public FloatGrid heightGrid;
        public Vector3Grid normalGrid;
        public TerrainChunkComponent(FloatGrid heightGrid, Vector3Grid normalGrid)
        {
            this.heightGrid = heightGrid;
            this.normalGrid = normalGrid;

            normalHeightTextureArrayID = Engine.RenderEngine._terrainRenderer.insertDataToTextureArray(heightGrid, normalGrid);
        }

        public void Cleanup()
        {
            Engine.RenderEngine._terrainRenderer.freeChunk(normalHeightTextureArrayID);
        }
    }
}

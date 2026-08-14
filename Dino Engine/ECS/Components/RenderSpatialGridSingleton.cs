using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    public struct RenderSpatialGridSingleton : IComponent
    {
        public SpatialGrid Grid;

        public RenderSpatialGridSingleton(float cellSize)
        {
            Grid = new SpatialGrid(cellSize);
        }
    }
}

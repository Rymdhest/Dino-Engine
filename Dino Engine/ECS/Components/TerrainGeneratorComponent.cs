using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Procedural.Terrain;
using Dino_Engine.Util.Data_Structures;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    public struct TerrainGeneratorComponent : IComponent
    {
        public TerrainGenerator Generator;

        public TerrainGeneratorComponent(TerrainGenerator generator)
        {
            this.Generator = generator;
        }
    }
}

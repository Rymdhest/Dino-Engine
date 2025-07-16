using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Util.Data_Structures;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    public struct TerrainQuadTreeComponent : IComponent
    {
        public QuadTreeNode QuadTree;
        public float rootSize;
        public TerrainQuadTreeComponent(QuadTreeNode quadTree)
        {
            this.QuadTree = quadTree;
            this.rootSize = QuadTree.Size;
        }
    }
}

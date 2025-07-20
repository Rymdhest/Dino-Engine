using Dino_Engine.ECS.ECS_Architecture;
using OpenTK.Mathematics;
namespace Dino_Engine.Util.Data_Structures
{
    public class QuadTreeNode
    {
        public Vector2 WorldPos;   // bottom-left of the node in world space
        public Entity ChunkEntity;   
        public float Size;         
        public int Depth;          
        public QuadTreeNode[]? Children; // null if leaf

        public QuadTreeNode(Vector2 worldPos, float size, int depth)
        {
            WorldPos = worldPos;
            Size = size;
            Depth = depth;
            Children = null;    
        }

        public void Subdivide()
        {
            float half = Size / 2f;
            Children = new QuadTreeNode[4];
            Children[0] = new QuadTreeNode(WorldPos, half, Depth + 1);                          // bottom-left
            Children[1] = new QuadTreeNode(WorldPos + new Vector2(half, 0), half, Depth + 1);   // bottom-right
            Children[2] = new QuadTreeNode(WorldPos + new Vector2(0, half), half, Depth + 1);   // top-left
            Children[3] = new QuadTreeNode(WorldPos + new Vector2(half, half), half, Depth + 1);// top-right
        }
        public Vector2 GetCenter()
        {
            return WorldPos+(new Vector2(Size)/2f);
        }
    }
}

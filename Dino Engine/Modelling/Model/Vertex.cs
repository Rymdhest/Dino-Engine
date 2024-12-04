using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Model
{
    public class Vertex
    {
        public Vector3 position;
        public Vector2[] UVs;
        public Material material;

        public Vertex(Vector3 position, Material material, params Vector2[] UVs)        {
            this.position = position;
            this.UVs = UVs;
            this.material = material;
        } 
    }
}

using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.Modelling.Model
{
    public class Vertex
    {
        public Vector3 position;
        public Vector2 UV;
        public Material material;

        public Vertex(Vector3 position, Vector2 uV, Material material)        {
            this.position = position;
            this.UV = uV;
            this.material = material;
        } 
    }
}

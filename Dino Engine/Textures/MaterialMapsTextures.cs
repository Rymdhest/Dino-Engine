using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.Textures
{
    public class MaterialMapsTextures
    {
        public int albedo;
        public int normal;
        public int properties;

        public MaterialMapsTextures(int albedo, int normal, int properties)
        {
            this.albedo = albedo;
            this.normal = normal;
            this.properties = properties;
        }
    }
}

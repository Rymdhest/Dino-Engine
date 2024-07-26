using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.Textures
{
    public class MaterialMapsTextures
    {

        public int[] textures = new int[3];

        public MaterialMapsTextures(int albedo, int normal, int materials)
        {
            textures[0] = albedo;
            textures[1] = normal;
            textures[2] = materials;
        }

        public void CleanUp()
        {
            GL.DeleteTexture(textures[0]);
            GL.DeleteTexture(textures[1]);
            GL.DeleteTexture(textures[2]);
        }
    }
}

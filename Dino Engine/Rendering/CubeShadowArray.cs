using OpenTK.Graphics.OpenGL;

namespace Dino_Engine.Rendering
{
    /// <summary>
    /// The point lights' cube shadow maps, all in one cube map array, so that one pass can read every light's map (PointLightRenderer, the volumetric fog). Each map
    /// is a framebuffer that draws into its own cube of it (<see cref="FrameBuffer.ForCube"/>), so whatever draws casters into a map still binds one face at a time.
    /// It grows when more maps are wanted than it holds, copying what's in it, so maps already drawn stay drawn.
    /// </summary>
    public sealed class CubeShadowArray
    {
        public readonly int Resolution;

        /// <summary>The cube map array, depth compared as a shadow sampler reads it.</summary>
        public int Texture { get; private set; }
        /// <summary>How many cubes it holds.</summary>
        public int Capacity { get; private set; }

        public CubeShadowArray(int resolution)
        {
            Resolution = resolution;
        }

        /// <summary>Makes room for at least <paramref name="cubes"/>, keeping what's drawn.</summary>
        public void EnsureCapacity(int cubes)
        {
            if (cubes <= Capacity) return;
            int texture = GL.GenTexture();
            GL.BindTexture(TextureTarget.TextureCubeMapArray, texture);
            GL.TexStorage3D(TextureTarget3d.TextureCubeMapArray, 1, (SizedInternalFormat)All.DepthComponent16, Resolution, Resolution, cubes * 6);
            GL.TexParameter(TextureTarget.TextureCubeMapArray, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.TextureCubeMapArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.TextureCubeMapArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMapArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMapArray, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMapArray, TextureParameterName.TextureCompareMode, (int)TextureCompareMode.CompareRefToTexture);
            GL.TexParameter(TextureTarget.TextureCubeMapArray, TextureParameterName.TextureCompareFunc, (int)DepthFunction.Lequal);
            GL.BindTexture(TextureTarget.TextureCubeMapArray, 0);

            if (Texture != 0)
            {
                GL.CopyImageSubData(Texture, ImageTarget.TextureCubeMapArray, 0, 0, 0, 0, texture, ImageTarget.TextureCubeMapArray, 0, 0, 0, 0, Resolution, Resolution, Capacity * 6);
                GL.DeleteTexture(Texture);
            }
            Texture = texture;
            Capacity = cubes;
        }
    }
}

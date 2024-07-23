using Dino_Engine.Core;
using Dino_Engine.Rendering;
using Dino_Engine.Rendering.Renderers;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.Textures
{
    public class TextureGenerator
    {
        private ShaderProgram _textureGeneratorShader = new ShaderProgram("Simple.vert", "Texture_Generate.frag");
        private ShaderProgram _textureNormalShader = new ShaderProgram("Simple.vert", "Texture_Normal_Generate.frag");
        private ShaderProgram _textureColorShader = new ShaderProgram("Simple.vert", "Texture_Color_Generate.frag");

        public FrameBuffer _albedoBuffer;
        public FrameBuffer _normalBuffer;
        public FrameBuffer _materialBuffer;
        private Vector2i textureResolution = new Vector2i(512, 512);
        public int megaAlbedoTextureArray;
        public int megaNormalTextureArray;
        public int megaMaterialTextureArray;

        public int grainIndex;
        public int sandIndex;



        public List<MaterialMapsTextures> preparedTextures = new List<MaterialMapsTextures>();

        public TextureGenerator()
        {
            FrameBufferSettings ablebdoBufferSettings = new FrameBufferSettings(textureResolution);
            DrawBufferSettings albedoAttachment = new DrawBufferSettings(FramebufferAttachment.ColorAttachment0);
            albedoAttachment.formatInternal = PixelInternalFormat.Rgba;
            albedoAttachment.pixelType = PixelType.UnsignedByte;
            albedoAttachment.formatExternal = PixelFormat.Rgba;
            //albedoAttachment.minFilterType = TextureMinFilter.LinearMipmapLinear;
            albedoAttachment.wrapMode = TextureWrapMode.Repeat;
            ablebdoBufferSettings.drawBuffers.Add(albedoAttachment);
            _albedoBuffer = new FrameBuffer(ablebdoBufferSettings);

            FrameBufferSettings normalBufferSettings = new FrameBufferSettings(textureResolution);
            DrawBufferSettings normalAttachment = new DrawBufferSettings(FramebufferAttachment.ColorAttachment0);
            normalAttachment.formatInternal = PixelInternalFormat.Rgba;
            normalAttachment.pixelType = PixelType.UnsignedByte;
            normalAttachment.wrapMode = TextureWrapMode.Repeat;
            normalAttachment.formatExternal = PixelFormat.Rgba;
            //normalAttachment.minFilterType = TextureMinFilter.LinearMipmapLinear;
            normalBufferSettings.drawBuffers.Add(normalAttachment);
            _normalBuffer = new FrameBuffer(normalBufferSettings);

            FrameBufferSettings propertiesBufferSettings = new FrameBufferSettings(textureResolution);
            DrawBufferSettings roughnessMetalicHeightAmbientAttachment = new DrawBufferSettings(FramebufferAttachment.ColorAttachment0);
            roughnessMetalicHeightAmbientAttachment.formatInternal = PixelInternalFormat.Rgba;
            roughnessMetalicHeightAmbientAttachment.pixelType = PixelType.UnsignedByte;
            roughnessMetalicHeightAmbientAttachment.formatExternal = PixelFormat.Rgba;
            roughnessMetalicHeightAmbientAttachment.wrapMode = TextureWrapMode.Repeat;
            roughnessMetalicHeightAmbientAttachment.minFilterType = TextureMinFilter.Linear;
            propertiesBufferSettings.drawBuffers.Add(roughnessMetalicHeightAmbientAttachment);
            _materialBuffer = new FrameBuffer(propertiesBufferSettings);


            _textureNormalShader.bind();
            _textureNormalShader.loadUniformInt("heightMap", 0);
            _textureNormalShader.loadUniformVector2f("texelSize",new Vector2(1f)/textureResolution);
            _textureNormalShader.unBind();

            _textureColorShader.bind();
            _textureColorShader.loadUniformInt("heightMap", 0);
            _textureColorShader.unBind();

        }

        public void GenerateAllTextures()
        {
            grainIndex = createGrainTexture();
            grainIndex = createGrainTexture();
            loadAllTexturesToArray();
        }

        private int DumpCurrentFramebuffersIntoTextures()
        {
            ScreenQuadRenderer renderer = Engine.RenderEngine.ScreenQuadRenderer;
            _normalBuffer.bind();
            _textureNormalShader.bind();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _materialBuffer.GetAttachment(0));
            renderer.Render();

            int albedoTexture = _albedoBuffer.exportAttachmentAsTexture(ReadBufferMode.ColorAttachment0);
            int normalTexture = _normalBuffer.exportAttachmentAsTexture(ReadBufferMode.ColorAttachment0);
            int materialTexture = _materialBuffer.exportAttachmentAsTexture(ReadBufferMode.ColorAttachment0);

            preparedTextures.Add(new MaterialMapsTextures(albedoTexture, normalTexture, materialTexture));

            return preparedTextures.Count-1;
        }

        private int loadTypeOfTextureToArray(int type)
        {
            int textureArray = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2DArray, textureArray);
            Engine.CheckGLError("After BindTexture");

            // Allocate storage for the texture array with the correct number of slices
            GL.TexStorage3D(TextureTarget3d.Texture2DArray, 8, SizedInternalFormat.Rgba8, textureResolution.X, textureResolution.Y, preparedTextures.Count);
            Engine.CheckGLError("After TexStorage3D");

            for (int i = 0; i < preparedTextures.Count; i++)
            {
                //GL.BindTexture(TextureTarget.Texture2D, preparedTextures[i].textures[type]);
                Engine.CheckGLError("After BindTexture2D");
                //GL.CopyTexSubImage3D(TextureTarget.Texture2DArray, 0, 0, 0, i, 0, 0, textureResolution.X, textureResolution.Y);
                GL.CopyImageSubData(preparedTextures[i].textures[type], ImageTarget.Texture2D, 0, 0, 0, 0, textureArray, ImageTarget.Texture2DArray, 0, 0, 0, i,textureResolution.X, textureResolution.Y, 1 );
                Engine.CheckGLError("After CopyTexSubImage3D");
            }
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

            Engine.CheckGLError("After TexParameter");
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2DArray);


            // Unbind the texture array
            GL.BindTexture(TextureTarget.Texture2DArray, 0);

            return textureArray;
        }


        private void loadAllTexturesToArray()
        {
            megaAlbedoTextureArray = loadTypeOfTextureToArray(0);
            megaNormalTextureArray = loadTypeOfTextureToArray(1);
            megaMaterialTextureArray = loadTypeOfTextureToArray(2);
        }


        private int createGrainTexture()
        {
            
            ScreenQuadRenderer renderer = Engine.RenderEngine.ScreenQuadRenderer;
            _materialBuffer.bind();
            _textureGeneratorShader.bind();


            _textureGeneratorShader.loadUniformInt("octaves",5);
            _textureGeneratorShader.loadUniformFloat("seed", 0.1f);
            _textureGeneratorShader.loadUniformBool("rigged", true);
            _textureGeneratorShader.loadUniformVector2f("startFrequenzy", new Vector2(20.0f, 20.0f));

            renderer.Render();


            _albedoBuffer.bind();
            _textureColorShader.bind();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _materialBuffer.GetAttachment(0));
            renderer.Render();

            return DumpCurrentFramebuffersIntoTextures();
        }
        public void CleanUp()
        {
            _textureGeneratorShader.cleanUp();
            _textureNormalShader.cleanUp();
            _albedoBuffer.cleanUp();
            _normalBuffer.cleanUp();
            _materialBuffer.cleanUp();
        }
    }
}

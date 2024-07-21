using Dino_Engine.Core;
using Dino_Engine.Rendering;
using Dino_Engine.Rendering.Renderers;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
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
        public FrameBuffer _poopertiesBuffer;
        private Vector2i textureResolution = new Vector2i(256, 256);

        public TextureGenerator()
        {
            FrameBufferSettings ablebdoBufferSettings = new FrameBufferSettings(textureResolution);
            DrawBufferSettings albedoAttachment = new DrawBufferSettings(FramebufferAttachment.ColorAttachment0);
            albedoAttachment.formatInternal = PixelInternalFormat.Rgba16f;
            albedoAttachment.pixelType = PixelType.Float;
            albedoAttachment.minFilterType = TextureMinFilter.LinearMipmapLinear;
            albedoAttachment.wrapMode = TextureWrapMode.Repeat;
            ablebdoBufferSettings.drawBuffers.Add(albedoAttachment);
            _albedoBuffer = new FrameBuffer(ablebdoBufferSettings);

            FrameBufferSettings normalBufferSettings = new FrameBufferSettings(textureResolution);
            DrawBufferSettings normalAttachment = new DrawBufferSettings(FramebufferAttachment.ColorAttachment0);
            normalAttachment.formatInternal = PixelInternalFormat.Rgba16f;
            normalAttachment.pixelType = PixelType.Float;
            normalAttachment.wrapMode = TextureWrapMode.Repeat;
            normalAttachment.minFilterType = TextureMinFilter.LinearMipmapLinear;
            normalBufferSettings.drawBuffers.Add(normalAttachment);
            _normalBuffer = new FrameBuffer(normalBufferSettings);

            FrameBufferSettings propertiesBufferSettings = new FrameBufferSettings(textureResolution);
            DrawBufferSettings roughnessMetalicHeightAmbientAttachment = new DrawBufferSettings(FramebufferAttachment.ColorAttachment0);
            roughnessMetalicHeightAmbientAttachment.formatInternal = PixelInternalFormat.Rgba16f;
            roughnessMetalicHeightAmbientAttachment.pixelType = PixelType.Float;
            roughnessMetalicHeightAmbientAttachment.formatExternal = PixelFormat.Rgba;
            roughnessMetalicHeightAmbientAttachment.wrapMode = TextureWrapMode.Repeat;
            roughnessMetalicHeightAmbientAttachment.minFilterType = TextureMinFilter.Linear;
            propertiesBufferSettings.drawBuffers.Add(roughnessMetalicHeightAmbientAttachment);
            _poopertiesBuffer = new FrameBuffer(propertiesBufferSettings);


            _textureNormalShader.bind();
            _textureNormalShader.loadUniformInt("heightMap", 0);
            _textureNormalShader.loadUniformVector2f("texelSize",new Vector2(1f)/textureResolution);
            _textureNormalShader.unBind();

            _textureColorShader.bind();
            _textureColorShader.loadUniformInt("heightMap", 0);
            _textureColorShader.unBind();
        }

        public void createTexture()
        {
            
            ScreenQuadRenderer renderer = Engine.RenderEngine.ScreenQuadRenderer;
            _poopertiesBuffer.bind();
            _textureGeneratorShader.bind();
            renderer.Render();

            _normalBuffer.bind();
            _textureNormalShader.bind();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _poopertiesBuffer.GetAttachment(0));
            renderer.Render();

            _albedoBuffer.bind();
            _textureColorShader.bind();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _poopertiesBuffer.GetAttachment(0));
            renderer.Render();









            GL.BindTexture(TextureTarget.Texture2D, _poopertiesBuffer.GetAttachment(0));
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

            GL.BindTexture(TextureTarget.Texture2D, _albedoBuffer.GetAttachment(0));
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

            GL.BindTexture(TextureTarget.Texture2D, _normalBuffer.GetAttachment(0));
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
        }

        public void makeIntoArray()
        {

        }

        public void CleanUp()
        {
            _textureGeneratorShader.cleanUp();
            _textureNormalShader.cleanUp();
            _albedoBuffer.cleanUp();
            _normalBuffer.cleanUp();
            _poopertiesBuffer.cleanUp();
        }
    }
}

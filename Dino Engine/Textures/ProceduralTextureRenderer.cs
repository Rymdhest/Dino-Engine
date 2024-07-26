using Dino_Engine.Core;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Rendering;
using Dino_Engine.Rendering.Renderers;
using Dino_Engine.Util;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System;

namespace Dino_Engine.Textures
{
    public class ProceduralTextureRenderer
    {
        public enum BlendMode
        {
            adding,
            multiplying,
            overriding,
        }

        private ShaderProgram _textureFBMShader = new ShaderProgram("Simple.vert", "Texture_FBM.frag");
        private ShaderProgram _textureNormalShader = new ShaderProgram("Simple.vert", "Texture_Normal_Generate.frag");
        private ShaderProgram _textureSeamlessShader = new ShaderProgram("Simple.vert", "Texture_Seamless_Generate.frag");

        public FrameBuffer _normalBuffer;
        public FrameBuffer _materialBuffer1;
        public FrameBuffer _materialBuffer2;

        private bool _toggle = true;


        public float heightFactor;
        public int octaves;
        public float seed;
        public float exponent;
        public float amplitudePerOctave;
        public float frequenzyPerOctave;
        public float normalFlatness;
        public float rougness;
        public float emission;
        public float metlaic;
        public Colour colour;
        public bool rigged;
        public bool invert;
        public bool depthCheck;
        public bool writeToHeight;
        public bool scaleOutputToHeight;
        public Vector2 startFrequenzy;
        public BlendMode blendMode;


        private Vector2i textureResolution;

        public ProceduralTextureRenderer(Vector2i textureResolution)
        {
            this.textureResolution = textureResolution;

            DrawBufferSettings normalAttachment = new DrawBufferSettings(FramebufferAttachment.ColorAttachment0);
            normalAttachment.formatInternal = PixelInternalFormat.Rgba;
            normalAttachment.pixelType = PixelType.UnsignedByte;
            normalAttachment.wrapMode = TextureWrapMode.Repeat;
            normalAttachment.formatExternal = PixelFormat.Rgba;
            FrameBufferSettings normalBufferSettings = new FrameBufferSettings(textureResolution);
            normalBufferSettings.drawBuffers.Add(normalAttachment);
            _normalBuffer = new FrameBuffer(normalBufferSettings);

            DrawBufferSettings albedoAttachment = new DrawBufferSettings(FramebufferAttachment.ColorAttachment0);
            albedoAttachment.formatInternal = PixelInternalFormat.Rgba;
            albedoAttachment.pixelType = PixelType.UnsignedByte;
            albedoAttachment.formatExternal = PixelFormat.Rgba;
            albedoAttachment.wrapMode = TextureWrapMode.Repeat;
            albedoAttachment.minFilterType = TextureMinFilter.Linear;

            DrawBufferSettings materialAttachment = new DrawBufferSettings(FramebufferAttachment.ColorAttachment1);
            materialAttachment.formatInternal = PixelInternalFormat.Rgba;
            materialAttachment.pixelType = PixelType.UnsignedByte;
            materialAttachment.formatExternal = PixelFormat.Rgba;
            materialAttachment.wrapMode = TextureWrapMode.Repeat;
            materialAttachment.minFilterType = TextureMinFilter.Linear;

            FrameBufferSettings materialSettings = new FrameBufferSettings(textureResolution);
            materialSettings.drawBuffers.Add(albedoAttachment);
            materialSettings.drawBuffers.Add(materialAttachment);


            _materialBuffer1 = new FrameBuffer(materialSettings);
            _materialBuffer2 = new FrameBuffer(materialSettings);


            _textureNormalShader.bind();
            _textureNormalShader.loadUniformInt("heightMap", 0);
            _textureNormalShader.loadUniformVector2f("texelSize", new Vector2(1f) / textureResolution);
            _textureNormalShader.unBind();


            _textureSeamlessShader.bind();
            _textureSeamlessShader.loadUniformInt("inputTexture1", 0);
            _textureSeamlessShader.loadUniformInt("inputTexture2", 1);
            _textureSeamlessShader.unBind();

            _textureFBMShader.bind();
            _textureFBMShader.loadUniformInt("previousAlbedoTexture", 0);
            _textureFBMShader.loadUniformInt("previousMaterialTexture", 1);
            _textureFBMShader.unBind();

            ResetUniforms();
        }


        public void Tap()
        {
            ScreenQuadRenderer renderer = Engine.RenderEngine.ScreenQuadRenderer;
            GetNextFrameBuffer().bind();
            _textureFBMShader.bind();

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, GetLastFrameBuffer().GetAttachment(0));
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, GetLastFrameBuffer().GetAttachment(1));

            _textureFBMShader.loadUniformInt("octaves", octaves);
            _textureFBMShader.loadUniformFloat("seed", seed);
            _textureFBMShader.loadUniformFloat("exponent", exponent);
            _textureFBMShader.loadUniformFloat("amplitudePerOctave", amplitudePerOctave);
            _textureFBMShader.loadUniformFloat("frequenzyPerOctave", frequenzyPerOctave);
            _textureFBMShader.loadUniformFloat("heightFactor", heightFactor);
            _textureFBMShader.loadUniformVector4f("albedo", colour.ToVector4());
            _textureFBMShader.loadUniformVector3f("materials", new Vector3(rougness, emission, metlaic));
            _textureFBMShader.loadUniformBool("rigged", rigged);
            _textureFBMShader.loadUniformBool("invert", invert);
            _textureFBMShader.loadUniformBool("depthCheck", depthCheck);
            _textureFBMShader.loadUniformBool("writeToHeight", writeToHeight);
            _textureFBMShader.loadUniformBool("scaleOutputToHeight", scaleOutputToHeight);
            _textureFBMShader.loadUniformInt("blendMode", ((int)blendMode));
            _textureFBMShader.loadUniformVector2f("startFrequenzy", startFrequenzy);

            renderer.Render();
            StepToggle();
        }

        public void makeSeamless()
        {
            ScreenQuadRenderer renderer = Engine.RenderEngine.ScreenQuadRenderer;

            GetNextFrameBuffer().bind();
            _textureSeamlessShader.bind();
            _textureSeamlessShader.loadUniformFloat("borderBlendRadius", 50.0f);
            _textureSeamlessShader.loadUniformVector2f("texSize",textureResolution);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, GetLastFrameBuffer().GetAttachment(0));

            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, GetLastFrameBuffer().GetAttachment(1));

            renderer.Render();
            StepToggle();
        }



        public MaterialMapsTextures Export()
        {
            //makeSeamless();
            ScreenQuadRenderer renderer = Engine.RenderEngine.ScreenQuadRenderer;
            _normalBuffer.bind();
            _textureNormalShader.bind();

            _textureNormalShader.loadUniformFloat("normalFlatness", normalFlatness);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, GetLastFrameBuffer().GetAttachment(1));
            renderer.Render();

            int albedoTexture = GetLastFrameBuffer().exportAttachmentAsTexture(ReadBufferMode.ColorAttachment0);
            int normalTexture = _normalBuffer.exportAttachmentAsTexture(ReadBufferMode.ColorAttachment0);
            int materialTexture = GetLastFrameBuffer().exportAttachmentAsTexture(ReadBufferMode.ColorAttachment1);

            ResetUniforms();
            _materialBuffer1.ClearColorDepth();
            _materialBuffer2.ClearColorDepth();

            return new MaterialMapsTextures(albedoTexture, normalTexture, materialTexture);
        }

        public void ResetUniforms()
        {
            heightFactor = 1f;
            octaves = 1;
            seed = 1f;
            exponent = 1f;
            amplitudePerOctave = 0.5f;
            frequenzyPerOctave = 2.0f;
            normalFlatness = 1.0f;
            rougness = 0.5f;
            emission = 0f;
            metlaic = 0f;
            colour = new Colour(255, 255, 255, 1f, 1f);
            rigged = false;
            invert = false;
            depthCheck = false;
            startFrequenzy = new Vector2(1f, 1f);
            blendMode = BlendMode.overriding;
            writeToHeight = true;
            scaleOutputToHeight = false;
        }

        public void CleanUp()
        {
            _textureFBMShader.cleanUp();
            _textureNormalShader.cleanUp();
            _textureSeamlessShader.cleanUp();

            _normalBuffer.cleanUp();
            _materialBuffer1.cleanUp();
            _materialBuffer2.cleanUp();
        }

        public FrameBuffer GetNextFrameBuffer()
        {
            if (_toggle) return _materialBuffer1;
            else return _materialBuffer2;
        }
        public FrameBuffer GetLastFrameBuffer()
        {
            if (_toggle) return _materialBuffer2;
            else return _materialBuffer1;
        }
        public void StepToggle()
        {
            if (_toggle == true) _toggle = false;
            else _toggle = true;
        }
    }
}

using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Graphics.OpenGL;
using System.Net.Mail;
using Dino_Engine.Core;
using Dino_Engine.ECS;

namespace Dino_Engine.Rendering.Renderers.Lighting
{
    internal class SSAORenderer : Renderer
    {
        private ShaderProgram ambientOcclusionShader = new ShaderProgram("Simple.vert", "AmbientOcclusion.frag");
        private ShaderProgram ambientOcclusionBlurShader = new ShaderProgram("Simple.vert", "AmbientOcclusion_Blur.frag");

        public int noiseScale = 4;
        private const int kernelSize = 16;
        private Vector3[] kernelSamples;
        private int noiseTexture;

        internal SSAORenderer()
        {
            ambientOcclusionBlurShader.bind();
            ambientOcclusionBlurShader.loadUniformInt("ssaoInput", 0);
            ambientOcclusionBlurShader.unBind();

            ambientOcclusionShader.bind();
            ambientOcclusionShader.loadUniformInt("texNoise", 0);
            ambientOcclusionShader.loadUniformInt("gNormal", 1);
            ambientOcclusionShader.loadUniformInt("gPosition", 2);
            ambientOcclusionShader.unBind();

            kernelSamples = new Vector3[kernelSize];
            for (int i = 0; i < kernelSize; i++)
            {
                Vector3 sample = new Vector3(MyMath.rngMinusPlus(), MyMath.rngMinusPlus(), MyMath.rand.NextSingle());
                sample.Normalize();
                sample *= MyMath.rand.NextSingle();
                float scale = (float)i / kernelSize;
                scale = 0.1f + scale * scale * (1f - 0.1f);
                sample *= scale;
                kernelSamples[i] = sample;
            }

            var noisePixels = new float[3 * noiseScale * noiseScale];
            for (int i = 0; i < noiseScale * noiseScale; i++)
            {
                noisePixels[i * 3] = MyMath.rngMinusPlus();
                noisePixels[i * 3 + 1] = MyMath.rngMinusPlus();
                noisePixels[i * 3 + 2] = 0;
            }
            noiseTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, noiseTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, noiseScale, noiseScale, 0, PixelFormat.Rgb, PixelType.Float, noisePixels);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (float)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (float)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (float)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (float)TextureWrapMode.Repeat);

        }
        internal override void Prepare(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
        }

        internal override void Finish(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
        }

        internal override void Render(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
            FrameBuffer gBuffer = renderEngine.GBuffer;
            Matrix4 projectionMatrix = eCSEngine.Camera.getComponent<ProjectionComponent>().ProjectionMatrix;
            Vector2i resolution = Engine.Resolution;

            ambientOcclusionShader.bind();
            ambientOcclusionShader.loadUniformVector2f("noiseScale", new Vector2(resolution.X / noiseScale, resolution.Y / noiseScale));
            ambientOcclusionShader.loadUniformMatrix4f("projectionMatrix", projectionMatrix);
            ambientOcclusionShader.loadUniformVector3fArray("samples", kernelSamples);

            ambientOcclusionShader.loadUniformFloat("radius", 1.2f);
            ambientOcclusionShader.loadUniformFloat("strength", 0.6f);
            ambientOcclusionShader.loadUniformFloat("bias", 0.002f);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, noiseTexture);
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, gBuffer.GetAttachment(1));
            GL.ActiveTexture(TextureUnit.Texture2);
            GL.BindTexture(TextureTarget.Texture2D, gBuffer.GetAttachment(2));

            renderEngine.ScreenQuadRenderer.RenderToNextFrameBuffer();

            ambientOcclusionShader.unBind();


            ambientOcclusionBlurShader.bind();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, renderEngine.ScreenQuadRenderer.GetLastOutputTexture());

            gBuffer.bind();
            GL.ColorMask(0, false, false, false, false);
            GL.ColorMask(1, false, false, false, true);
            GL.ColorMask(2, false, false, false, false);
            GL.ColorMask(3, false, false, false, false);
            renderEngine.ScreenQuadRenderer.Render();
            GL.ColorMask(0, true, true, true, true);
            GL.ColorMask(1, true, true, true, true);
            GL.ColorMask(2, true, true, true, true);
            GL.ColorMask(3, true, true, true, true);
            ambientOcclusionBlurShader.unBind();
        }

        public override void CleanUp()
        {
            ambientOcclusionBlurShader.cleanUp();
            ambientOcclusionShader.cleanUp();
            GL.DeleteTexture(noiseTexture);
        }

        public override void OnResize(ResizeEventArgs eventArgs)
        {
        }

        public override void Update()
        {
        }


    }
}

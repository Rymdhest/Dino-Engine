using OpenTK.Windowing.Common;
using OpenTK.Graphics.OpenGL;
using Dino_Engine.Core;
using Dino_Engine.Rendering.Renderers.PosGeometry;

namespace Dino_Engine.Rendering.Renderers.PostProcessing
{
    internal class FogRenderer : Renderer
    {
        private ShaderProgram fogShader = new ShaderProgram("Simple.vert", "Fog.frag");
        private float time = 0;
        public FogRenderer()
        {
            fogShader.bind();
            fogShader.loadUniformInt("shadedColourTexture", 0);
            fogShader.loadUniformInt("gDepth", 1);
            fogShader.unBind();
        }

        internal override void Prepare(RenderEngine renderEngine)
        {
            fogShader.bind();
        }

        internal override void Finish(RenderEngine renderEngine)
        {
        }

        internal override void Render(RenderEngine renderEngine)
        {
            DualBuffer buffer = renderEngine.lastUsedBuffer;
            FrameBuffer gBuffer = renderEngine.GBuffer;


            fogShader.loadUniformFloat("fogDensity", 0.03f);
            fogShader.loadUniformFloat("heightFallOff", 0.0002f);
            fogShader.loadUniformFloat("noiseFactor", 0.9f);
            fogShader.loadUniformVector3f("fogColor", SkyRenderer.SkyColour.ToVector3());

            

            fogShader.loadUniformVector3f("cameraPosWorldSpace", renderEngine.context.viewPos);
            fogShader.loadUniformMatrix4f("inverseViewMatrix", renderEngine.context.invViewMatrix);
            fogShader.loadUniformMatrix4f("inverseProjection", renderEngine.context.invProjectionMatrix);
            fogShader.loadUniformVector2f("resolution", Engine.Resolution);
            fogShader.loadUniformFloat("time", time);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, buffer.GetLastOutputTexture());
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, gBuffer.getDepthAttachment());
            buffer.RenderToNextFrameBuffer();

        }


        public override void CleanUp()
        {
            fogShader.cleanUp();
        }

        public override void Update()
        {
            time += Engine.Delta;
        }


    }
}

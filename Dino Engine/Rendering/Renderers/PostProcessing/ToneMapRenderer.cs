using Dino_Engine.Core;
using Dino_Engine.ECS;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;

namespace Dino_Engine.Rendering.Renderers.PostProcessing
{
    internal class ToneMapRenderer : RenderPassRenderer
    {
        private ShaderProgram HDRMapShader = new ShaderProgram("Simple.vert", "HDR_Mapper.frag");

        /// <summary>Draws onto the screen itself, for when this is the last thing that happens to the picture.</summary>
        public bool ToScreen { get; set; }

        public ToneMapRenderer() : base("Tonemapping")
        {
            HDRMapShader.bind();
            HDRMapShader.loadUniformInt("HDRcolorTexture", 0);
            HDRMapShader.unBind();
        }
        internal override void Prepare(RenderEngine renderEngine)
        {
            HDRMapShader.bind();
        }

        internal override void Finish(RenderEngine renderEngine)
        {
        }

        internal override void Render(RenderEngine renderEngine)
        {
            DualBuffer buffer = renderEngine.lastUsedBuffer;

            HDRMapShader.loadUniformFloat("exposure", renderEngine.Lighting.Exposure * renderEngine.Adaptation);
            HDRMapShader.loadUniformFloat("gamma", 2.2f);
            // A whole step of the 8 bits it ends up in: anything less leaves bands in the slow fades of a dark scene.
            HDRMapShader.loadUniformFloat("dithering", 1f);

            if (ToScreen)
            {
                Engine.WindowHandler.refreshViewport();
                buffer.RenderTextureToScreen(buffer.GetLastOutputTexture());
            }
            else
            {
                buffer.RenderTextureToNextFrameBuffer(buffer.GetLastOutputTexture());
            }

            HDRMapShader.unBind();
        }

        public override void CleanUp()
        {
            HDRMapShader.cleanUp();
        }


    }
}

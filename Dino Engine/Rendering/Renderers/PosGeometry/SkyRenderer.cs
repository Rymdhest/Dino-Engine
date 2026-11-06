using OpenTK.Windowing.Common;
using OpenTK.Graphics.OpenGL;
using Dino_Engine.Util;
using Dino_Engine.Core;
using OpenTK.Mathematics;

namespace Dino_Engine.Rendering.Renderers.PosGeometry
{

    public class SkyRenderer : RenderPassRenderer
    {
        /// <summary>Brightness of the drawn sky relative to skyColour. Water reflects the same sky (WaterCommon.glsl).</summary>
        public const float SkyStrength = 5.0f;

        private ShaderProgram skyShader = new ShaderProgram("Simple.vert", "sky.frag");

        public SkyRenderer() : base("sky")
        {
            skyShader.bind();
            skyShader.loadUniformInt("skyMap", 0);
            skyShader.unBind();
        }
        internal override void Prepare(RenderEngine renderEngine)
        {
            skyShader.bind();
            // Into the multisampled scene: the per-sample depth test fills exactly the samples nothing covers, so outlines
            // against the sky are anti-aliased.
            renderEngine.Msaa.SceneBuffer.bind();
            GL.DepthFunc(DepthFunction.Lequal);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, renderEngine._weatherRenderer.SkyMapTexture);


            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
            GL.BlendEquation(BlendEquationMode.FuncAdd);

        }

        internal override void Finish(RenderEngine renderEngine)
        {
            GL.DepthFunc(DepthFunction.Less);
        }

        public override void CleanUp()
        {
            skyShader.cleanUp();
        }

        internal override void Render(RenderEngine renderEngine)
        {
            renderEngine.ScreenQuadRenderer.Render(depthTest: true, blend: false, clearColor: false);
        }
    }
}

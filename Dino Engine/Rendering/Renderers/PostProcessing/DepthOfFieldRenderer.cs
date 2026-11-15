using Dino_Engine.Core;
using Dino_Engine.ECS;
using OpenTK.Windowing.Common;
using OpenTK.Graphics.OpenGL;

namespace Dino_Engine.Rendering.Renderers.PostProcessing
{
    public class DepthOfFieldRenderer : RenderPassRenderer
    {
        private ShaderProgram _depthOfFieldShader = new ShaderProgram("Simple.vert", "Depth_Of_Field.frag");

        // How out of focus each metre from the focus is.
        private const float Range = 0.001f;

        /// <summary>
        /// Whether this frame's mix of the sharp picture and the blurred one is left to the tone mapping (HDR_Mapper.frag), which reads every pixel anyway, rather than
        /// drawn as a pass of its own: in play, where the sun isn't drawn over the sky and the lens is the camera's own. The bloom then starts from the sharp picture,
        /// which differs from the mixed one by a few hundredths of a blur.
        /// </summary>
        public bool LeftToToneMap { get; private set; }
        private float _focus;
        private bool _inPlay;     // Left out altogether this frame (Render).

        public DepthOfFieldRenderer() : base("Depth of Field")
        {
            _depthOfFieldShader.bind();
            _depthOfFieldShader.loadUniformInt("colorTexture", 0);
            _depthOfFieldShader.loadUniformInt("blurTexture", 1);
            _depthOfFieldShader.loadUniformInt("gDepth", 2);
            _depthOfFieldShader.unBind();
        }

        internal override void Prepare(RenderEngine renderEngine)
        {
        }

        internal override void Finish(RenderEngine renderEngine)
        {
        }

        internal override void Render(RenderEngine renderEngine)
        {
            DualBuffer buffer = renderEngine.lastUsedBuffer;
            FrameBuffer gBuffer = renderEngine.GBuffer;
            GaussianBlurRenderer gaussianBlurRenderer = renderEngine.GaussianBlurRenderer;

            // In focus at the hero. Focused on the camera itself, the whole picture is a little out of focus.
            ref ShaderGlobals context = ref renderEngine.context;
            float focus = context.focusPoint.W > 0f ? (context.focusPoint.Xyz - context.viewPos).Length : 0f;
            // A landscape's lens is asked for a frame at a time.
            float landscape = MathF.Max(renderEngine.LandscapeLens, 0f);
            renderEngine.LandscapeLens = 0f;

            _focus = focus;
            LeftToToneMap = landscape <= 0f && !renderEngine._sunRenderer.Shows(renderEngine);
            // In play the game's camera has the hero's ground within a dozen metres or so of the focus, a hundredth or two of a blur at most (Range),
            // which isn't worth a blurred copy of the screen at any quality.
            _inPlay = LeftToToneMap;
            if (_inPlay) return;

            gaussianBlurRenderer.Render(buffer.GetLastFrameBuffer(), 5, renderEngine.ScreenQuadRenderer);
            if (LeftToToneMap) return;

            _depthOfFieldShader.bind();
            _depthOfFieldShader.loadUniformFloat("range", Range);
            _depthOfFieldShader.loadUniformFloat("focusDistance", focus);
            _depthOfFieldShader.loadUniformFloat("landscape", landscape);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, buffer.GetLastOutputTexture());

            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, gaussianBlurRenderer.GetLastResultTexture());

            GL.ActiveTexture(TextureUnit.Texture2);
            GL.BindTexture(TextureTarget.Texture2D, gBuffer.getDepthAttachment());

            buffer.RenderToNextFrameBuffer();

            _depthOfFieldShader.unBind();
        }

        /// <summary>
        /// Gives the tone mapping what it needs to mix in the blur itself when that's left to it (<see cref="LeftToToneMap"/>), on texture units 1 and 2, and says
        /// whether it is. Call it with the tone mapping's shader bound.
        /// </summary>
        public void LendToToneMap(ShaderProgram shader, RenderEngine renderEngine)
        {
            bool lent = LeftToToneMap && !_inPlay;
            shader.loadUniformInt("depthOfField", lent ? 1 : 0);
            if (!lent) return;
            shader.loadUniformFloat("depthOfFieldRange", Range);
            shader.loadUniformFloat("focusDistance", _focus);
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, renderEngine.GaussianBlurRenderer.GetLastResultTexture());
            GL.ActiveTexture(TextureUnit.Texture2);
            GL.BindTexture(TextureTarget.Texture2D, renderEngine.GBuffer.getDepthAttachment());
            GL.ActiveTexture(TextureUnit.Texture0);
        }

        public override void CleanUp()
        {
            _depthOfFieldShader.cleanUp();
        }
    }
}

using Dino_Engine.Core;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Rendering.Renderers.AntiAliasing;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;

namespace Dino_Engine.Rendering.Renderers.PosGeometry
{
    /// <summary>One body to draw a line round.</summary>
    public struct OutlineCommand
    {
        public glModel Model;
        public Matrix4 ModelMatrix;
        public Matrix4[] SkinningMatrices;

        /// <summary>What colour it is, from 0 to 1.</summary>
        public Vector4 Colour;
    }

    /// <summary>
    /// A line round a body, the same few pixels wide however far off it is, for picking one thing out of a crowd. Each body is drawn flat into a mask, with no depth test, so that it's known about behind a tree as well.
    /// One pass over the screen then colours whatever lies just outside that mask. The hull method isn't used, as bodies are separate rigid pieces, and hard edges come apart when they're pushed out.
    /// All of it is skipped when there's nobody to outline.
    /// </summary>
    public class OutlineRenderer : CommandDrivenRenderer<OutlineCommand>
    {
        private const float Thickness = 2.4f;       // Pixels, at a thousand lines up the screen.
        private const float Brightness = 2.6f;      // It goes into the scene and is tone mapped with it, and it has to stand out from a sunlit field.
        private const float LinesForThickness = 1000f;

        private readonly ShaderProgram _maskShader = new ShaderProgram("Model_Character_Shadow.vert", "Marks/Outline_Mask.frag");
        private readonly ShaderProgram _compositeShader = new ShaderProgram("Simple.vert", "Marks/Outline_Composite.frag");
        private readonly FrameBuffer _mask;
        private readonly float[] _clear = { 0f, 0f, 0f, 0f };
        private bool _any;

        public OutlineRenderer() : base("Outlines")
        {
            var settings = new FrameBufferSettings(Engine.Resolution);
            settings.drawBuffers.Add(new DrawBufferSettings(FramebufferAttachment.ColorAttachment0)
            {
                formatInternal = PixelInternalFormat.Rgba8,
                wrapMode = TextureWrapMode.ClampToEdge,
            });
            _mask = new FrameBuffer(settings);
            _mask.unbind();

            _compositeShader.bind();
            _compositeShader.loadUniformInt("mask", 0);
            _compositeShader.unBind();
        }

        internal override void Prepare(RenderEngine renderEngine)
        {
            _any = commands.Count > 0;
            if (!_any) return;

            _mask.bind();
            GL.ClearBuffer(ClearBuffer.Color, 0, _clear);

            GL.DepthMask(false);
            GL.Disable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);
            GL.Disable(EnableCap.StencilTest);
            GL.Disable(EnableCap.Blend);

            _maskShader.bind();
        }

        public override void PerformCommand(OutlineCommand command, RenderEngine renderEngine)
        {
            glModel model = command.Model;
            GL.BindVertexArray(model.getVAOID());
            GL.EnableVertexAttribArray(0); // Position
            GL.EnableVertexAttribArray(6); // Bone Indices
            GL.EnableVertexAttribArray(7); // Bone Weights

            _maskShader.loadUniformMatrix4f("modelViewProjectionMatrix", command.ModelMatrix * renderEngine.context.viewMatrix * renderEngine.context.projectionMatrix);
            _maskShader.loadUniformMatrix4fArray("u_Bones", command.SkinningMatrices);
            _maskShader.loadUniformVector4f("colour", command.Colour);
            GL.DrawElements(PrimitiveType.Triangles, model.getVertexCount(), DrawElementsType.UnsignedInt, 0);
        }

        internal override void Finish(RenderEngine renderEngine)
        {
            if (!_any) return;

            GL.BindVertexArray(0);
            renderEngine.Msaa.SceneBuffer.bind();
            GL.Enable(EnableCap.Blend);
            GL.BlendFuncSeparate(BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha, BlendingFactorSrc.Zero, BlendingFactorDest.One);

            _compositeShader.bind();
            _compositeShader.loadUniformFloat("thickness", Thickness * Engine.Resolution.Y / LinesForThickness);
            _compositeShader.loadUniformFloat("brightness", Brightness);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _mask.GetAttachment(0));
            renderEngine.ScreenQuadRenderer.DrawQuad();

            GL.Disable(EnableCap.Blend);
            GL.Enable(EnableCap.CullFace);
            GL.DepthMask(true);
        }

        public override void OnResize(ResizeEventArgs eventArgs)
        {
            _mask.resize(eventArgs.Size);
        }

        public override void CleanUp()
        {
            _maskShader.cleanUp();
            _compositeShader.cleanUp();
            _mask.cleanUp();
        }
    }
}

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
        // The screen rectangle the bodies drawn into the mask cover, in pixels, which is all the composite has to look at (Finish).
        private Vector2 _least, _most;
        private bool _wholeScreen;
        private const float PoseReach = 1.5f;      // How far past its bind pose's bounds a body's limbs and tail can swing, as a share of them.

        public OutlineRenderer() : base("Outlines")
        {
            var settings = new FrameBufferSettings(Engine.RenderResolution);
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
            _least = new Vector2(float.MaxValue);
            _most = new Vector2(float.MinValue);
            _wholeScreen = false;
        }

        // Grows the rectangle the composite covers by this body's bounds, projected.
        private void Cover(in OutlineCommand command, RenderEngine renderEngine)
        {
            if (_wholeScreen) return;
            command.Model.WorldBounds(command.ModelMatrix, out Vector3 centre, out float radius);
            radius *= PoseReach;
            if (!float.IsFinite(radius)) { _wholeScreen = true; return; }
            Matrix4 viewProjection = renderEngine.context.viewMatrix * renderEngine.context.projectionMatrix;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 offset = new Vector3((corner & 1) != 0 ? radius : -radius, (corner & 2) != 0 ? radius : -radius, (corner & 4) != 0 ? radius : -radius);
                Vector4 clip = new Vector4(centre + offset, 1f) * viewProjection;
                if (clip.W <= 1e-3f) { _wholeScreen = true; return; }
                Vector2 ndc = clip.Xy / clip.W;
                _least = Vector2.ComponentMin(_least, ndc);
                _most = Vector2.ComponentMax(_most, ndc);
            }
        }

        public override void PerformCommand(OutlineCommand command, RenderEngine renderEngine)
        {
            Cover(command, renderEngine);
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
            _compositeShader.loadUniformFloat("thickness", Thickness * Engine.RenderResolution.Y / LinesForThickness);
            _compositeShader.loadUniformFloat("brightness", Brightness);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _mask.GetAttachment(0));
            // Only round the bodies, and the line's width past them: the rest of the screen is outside every mask and nowhere near one.
            Vector2i size = Engine.RenderResolution;
            if (!_wholeScreen)
            {
                int margin = (int)MathF.Ceiling(Thickness * size.Y / LinesForThickness) + 2;
                Vector2i min = new Vector2i((int)MathF.Floor((_least.X * 0.5f + 0.5f) * size.X) - margin, (int)MathF.Floor((_least.Y * 0.5f + 0.5f) * size.Y) - margin);
                Vector2i max = new Vector2i((int)MathF.Ceiling((_most.X * 0.5f + 0.5f) * size.X) + margin, (int)MathF.Ceiling((_most.Y * 0.5f + 0.5f) * size.Y) + margin);
                min = Vector2i.Clamp(min, Vector2i.Zero, size);
                max = Vector2i.Clamp(max, Vector2i.Zero, size);
                GL.Enable(EnableCap.ScissorTest);
                GL.Scissor(min.X, min.Y, Math.Max(max.X - min.X, 0), Math.Max(max.Y - min.Y, 0));
            }
            renderEngine.ScreenQuadRenderer.DrawQuad();
            GL.Disable(EnableCap.ScissorTest);

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

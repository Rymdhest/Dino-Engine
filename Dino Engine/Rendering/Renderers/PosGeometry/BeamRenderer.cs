using Dino_Engine.Core;
using Dino_Engine.Util;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Dino_Engine.Rendering.Renderers.PosGeometry
{
    public struct BeamRenderCommand : IRenderCommand
    {
        public Vector3 Base;
        public float Height;
        public float Width;
        public Colour Colour;
        public float Seed;
    }

    /// <summary>Upright pillars of light, over the lit scene with the particles. They read the depth buffer and don't write to it, so what stands in front still does.</summary>
    public class BeamRenderer : CommandDrivenRenderer<BeamRenderCommand>
    {
        private readonly ShaderProgram _shader = new ShaderProgram("Beam.vert", "Beam.frag");
        private readonly int _corners = GL.GenVertexArray();    // The shader knows its own four corners, but drawing needs something bound.

        public BeamRenderer() : base("Beams")
        {
        }

        internal override void Prepare(RenderEngine renderEngine)
        {
            GL.DepthMask(false);
            GL.Enable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);
            GL.Enable(EnableCap.Blend);
            GL.BlendFuncSeparate(BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha, BlendingFactorSrc.Zero, BlendingFactorDest.One);
            GL.BlendEquation(BlendEquationMode.FuncAdd);

            renderEngine.Msaa.SceneBuffer.bind();
            _shader.bind();
            GL.BindVertexArray(_corners);

            _shader.loadUniformMatrix4f("viewProjection", renderEngine.context.viewMatrix * renderEngine.context.projectionMatrix);
            _shader.loadUniformVector3f("eye", renderEngine.context.viewPos);
            _shader.loadUniformFloat("time", Engine.Time % 3600f);
        }

        internal override void Finish(RenderEngine renderEngine)
        {
            GL.Enable(EnableCap.CullFace);
        }

        public override void CleanUp()
        {
            _shader.cleanUp();
            GL.DeleteVertexArray(_corners);
        }

        public override void PerformCommand(BeamRenderCommand command, RenderEngine renderEngine)
        {
            _shader.loadUniformVector3f("base", command.Base);
            _shader.loadUniformVector2f("size", new Vector2(command.Width, command.Height));
            _shader.loadUniformVector3f("color", command.Colour.ToVector4().Xyz);
            _shader.loadUniformFloat("seed", command.Seed);
            GL.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
        }
    }
}

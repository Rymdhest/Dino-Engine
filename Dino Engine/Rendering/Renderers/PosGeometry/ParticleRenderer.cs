using Dino_Engine.Modelling.Model;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Graphics.OpenGL;
using Dino_Engine.Modelling.Procedural;

namespace Dino_Engine.Rendering.Renderers.PosGeometry
{
    public struct ParticleRenderCommand : IRenderCommand
    {
        public Matrix4 localToWorldMatrix;
        public Colour color;
    }

    public class ParticleRenderer : CommandDrivenRenderer<ParticleRenderCommand>
    {

        private ShaderProgram flatShader = new ShaderProgram("Particle.vert", "Particle.frag");

        public ParticleRenderer() : base("Particles")
        {
        }

        internal override void Prepare(RenderEngine renderEngine)
        {
            GL.DepthMask(false);
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.Enable(EnableCap.Blend);
            //GL.Disable(EnableCap.Blend);

            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
            GL.BlendEquation(BlendEquationMode.FuncAdd);

            renderEngine.lastUsedBuffer.GetLastFrameBuffer().bind();
            flatShader.bind();
            GL.BindVertexArray(ModelGenerator.UNIT_SPHERE.getVAOID());
            GL.EnableVertexAttribArray(0);
        }
        internal override void Finish(RenderEngine renderEngine)
        {
        }

        public override void CleanUp()
        {
            flatShader.cleanUp();
        }


        public override void PerformCommand(ParticleRenderCommand command, RenderEngine renderEngine)
        {
            Matrix4 transformationMatrix = command.localToWorldMatrix;

            Matrix4 MVPMatrix = transformationMatrix * renderEngine.context.viewMatrix* renderEngine.context.projectionMatrix;

            flatShader.loadUniformMatrix4f("modelViewProjectionMatrix", MVPMatrix);

            flatShader.loadUniformVector4f("color", command.color.ToVector4());

            GL.DrawElements(PrimitiveType.Triangles, ModelGenerator.UNIT_SPHERE.getVertexCount(), DrawElementsType.UnsignedInt, 0);
        }
    }
}

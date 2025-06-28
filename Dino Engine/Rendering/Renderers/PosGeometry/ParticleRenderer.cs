using Dino_Engine.Core;
using Dino_Engine.ECS;
using Dino_Engine.ECS.Systems;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Graphics.OpenGL;
using Dino_Engine.Modelling.Procedural;
using Dino_Engine.Modelling;
using Dino_Engine.ECS.ComponentsOLD;

namespace Dino_Engine.Rendering.Renderers.PosGeometry
{
    public struct ParticleRenderCommand : IRenderCommand
    {
        public Matrix4 localToWorldMatrix;
    }

    public class ParticleRenderer : Renderer
    {

        private ShaderProgram flatShader = new ShaderProgram("Particle.vert", "Particle.frag");
        private List<ParticleRenderCommand> renderCommands = new List<ParticleRenderCommand>();


        public void SubmitCommand(ParticleRenderCommand command)
        {
            renderCommands.Add(command);
        }

        internal override void Render(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
            EntityOLD camera = eCSEngine.Camera;
            glModel glmodel = ModelGenerator.UNIT_SPHERE;
            Matrix4 projectionMatrix = camera.getComponent<ProjectionComponent>().ProjectionMatrix;
            Matrix4 viewMatrix = MyMath.createViewMatrix(camera.getComponent<TransformationComponent>().Transformation);
            renderEngine.lastUsedBuffer.GetLastFrameBuffer().bind();
            flatShader.bind();
            GL.BindVertexArray(glmodel.getVAOID());
            GL.EnableVertexAttribArray(0);

            foreach (ParticleRenderCommand command in renderCommands)
            {
                Matrix4 transformationMatrix = command.localToWorldMatrix;

                Matrix4 modelViewMatrix = transformationMatrix * viewMatrix;

                flatShader.loadUniformMatrix4f("modelViewProjectionMatrix", modelViewMatrix * projectionMatrix);

                flatShader.loadUniformVector4f("color", new Vector4(1f, 1f, 0.1f, 0.5f));

                GL.DrawElements(PrimitiveType.Triangles, glmodel.getVertexCount(), DrawElementsType.UnsignedInt, 0);
            }
        }

        internal override void Prepare(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
            GL.DepthMask(false);
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.Enable(EnableCap.Blend);
            //GL.Disable(EnableCap.Blend);

            //GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
            GL.BlendEquation(BlendEquationMode.FuncAdd);
        }
        internal override void Finish(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
            renderCommands.Clear();
        }

        public override void CleanUp()
        {
            flatShader.cleanUp();
        }

        public override void OnResize(ResizeEventArgs eventArgs)
        {
        }

        public override void Update()
        {
        }


    }
}

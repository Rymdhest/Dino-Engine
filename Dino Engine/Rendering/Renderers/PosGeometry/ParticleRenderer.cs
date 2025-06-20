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

namespace Dino_Engine.Rendering.Renderers.PosGeometry
{
    public class ParticleRenderer : Renderer
    {

        private ShaderProgram flatShader = new ShaderProgram("Particle.vert", "Particle.frag");


        internal override void Render(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
            Entity camera = eCSEngine.Camera;
            glModel glmodel = ModelGenerator.UNIT_SPHERE;
            Matrix4 projectionMatrix = camera.getComponent<ProjectionComponent>().ProjectionMatrix;
            Matrix4 viewMatrix = MyMath.createViewMatrix(camera.getComponent<TransformationComponent>().Transformation);
            renderEngine.ScreenQuadRenderer.GetLastFrameBuffer().bind();
            flatShader.bind();
            GL.BindVertexArray(glmodel.getVAOID());
            GL.EnableVertexAttribArray(0);

            foreach (Entity entity in eCSEngine.getSystem<ParticleSystem>().MemberEntities)
            {
                Matrix4 transformationMatrix = MyMath.createTransformationMatrix(entity.getComponent<TransformationComponent>().Transformation);

                Matrix4 modelViewMatrix = transformationMatrix * viewMatrix;

                flatShader.loadUniformMatrix4f("modelViewProjectionMatrix", modelViewMatrix * projectionMatrix);

                flatShader.loadUniformVector4f("color", entity.getComponent<ColourComponent>().Colour.ToVector4());

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
            GL.Disable(EnableCap.Blend);

            //GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
            GL.BlendEquation(BlendEquationMode.FuncAdd);
        }
        internal override void Finish(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
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

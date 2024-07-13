using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using Dino_Engine.Rendering.Renderers;
using Dino_Engine.ECS;
using Dino_Engine.Util;
using Dino_Engine.Modelling.Model;

namespace Dino_Engine.Rendering.Renderers.Geometry
{
    internal class ModelRenderer : Renderer
    {
        private ShaderProgram _modelShader = new ShaderProgram("Model.vert", "Model.frag");

        public ModelRenderer()
        {
        }
        internal override void Prepare(ECSEngine eCSEngine, RenderEngine renderEngine)
        {

            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.Disable(EnableCap.Blend);
            _modelShader.bind();

        }
        internal override void Render(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
            Entity camera = eCSEngine.Camera;
            Matrix4 viewMatrix = MyMath.createViewMatrix(camera.getComponent<TransformationComponent>().Transformation);
            Matrix4 projectionMatrix = camera.getComponent<ProjectionComponent>().ProjectionMatrix;

            foreach (KeyValuePair<glModel, List<Entity>> glmodels in eCSEngine.getSystem<ModelRenderSystem>().ModelsDictionary)
            {
                glModel glmodel = glmodels.Key;

                GL.BindVertexArray(glmodel.getVAOID());
                GL.EnableVertexAttribArray(0);
                GL.EnableVertexAttribArray(1);
                GL.EnableVertexAttribArray(2);
                GL.EnableVertexAttribArray(3);
                foreach (Entity entity in glmodels.Value)
                {
                    Matrix4 transformationMatrix = MyMath.createTransformationMatrix(entity.getComponent<TransformationComponent>().Transformation);
                    Matrix4 modelViewMatrix = transformationMatrix * viewMatrix;
                    _modelShader.loadUniformMatrix4f("modelViewMatrix", modelViewMatrix);
                    _modelShader.loadUniformMatrix4f("modelViewProjectionMatrix", modelViewMatrix * projectionMatrix);
                    _modelShader.loadUniformMatrix4f("normalModelViewMatrix", Matrix4.Transpose(Matrix4.Invert(modelViewMatrix)));

                    GL.DrawElements(PrimitiveType.Triangles, glmodel.getVertexCount(), DrawElementsType.UnsignedInt, 0);
                }
            }
        }
        public override void Update()
        {
        }
        public override void OnResize(ResizeEventArgs eventArgs)
        {
        }
        internal override void Finish(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
            GL.DisableVertexAttribArray(0);
            GL.DisableVertexAttribArray(1);
            GL.DisableVertexAttribArray(2);
            GL.DisableVertexAttribArray(3);
            GL.BindVertexArray(0);
        }
        public override void CleanUp()
        {
            _modelShader.cleanUp();
        }
    }
}

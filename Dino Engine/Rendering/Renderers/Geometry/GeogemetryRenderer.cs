using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using Dino_Engine.Rendering.Renderers;
using Dino_Engine.ECS;
using Dino_Engine.Util;
using Dino_Engine.Modelling.Model;

namespace Dino_Engine.Rendering.Renderers.Geometry
{
    internal class GeogemetryRenderer : Renderer
    {
        private ShaderProgram flatShader = new ShaderProgram("Geometry_Vertex", "Flat_Shade_Fragment");

        public GeogemetryRenderer()
        {
        }
        private void prepareFrame(Matrix4 viewMatrix, Matrix4 projectionMatrix)
        {
            GL.DepthMask(true);
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.Disable(EnableCap.Blend);
            GL.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        }
        public void render(ModelRenderSystem flatShadeEntities, Entity camera)
        {
            Matrix4 viewMatrix = MyMath.createViewMatrix(camera.getComponent<TransformationComponent>().Transformation);
            Matrix4 projectionMatrix = camera.getComponent<ProjectionComponent>().ProjectionMatrix;
            prepareFrame(viewMatrix, projectionMatrix);
            flatShader.bind();

            foreach (KeyValuePair<glModel, List<Entity>> glmodels in flatShadeEntities.ModelsDictionary)
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
                    flatShader.loadUniformMatrix4f("modelViewMatrix", modelViewMatrix);
                    flatShader.loadUniformMatrix4f("modelViewProjectionMatrix", modelViewMatrix * projectionMatrix);
                    flatShader.loadUniformMatrix4f("normalModelViewMatrix", Matrix4.Transpose(Matrix4.Invert(modelViewMatrix)));

                    GL.DrawElements(PrimitiveType.Triangles, glmodel.getVertexCount(), DrawElementsType.UnsignedInt, 0);
                }
            }
            flatShader.unBind();
            finishFrame();
        }
        public override void Update()
        {
        }
        public override void OnResize(ResizeEventArgs eventArgs)
        {
        }
        private void finishFrame()
        {
            GL.BindVertexArray(0);
            GL.DisableVertexAttribArray(0);
            GL.DisableVertexAttribArray(1);
            GL.DisableVertexAttribArray(2);
            GL.DisableVertexAttribArray(3);
        }
        public override void CleanUp()
        {
            flatShader.cleanUp();
        }
    }
}

using Dino_Engine.Core;
using Dino_Engine.Modelling;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Rendering;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;

namespace Dino_Engine.Debug
{
    public class DebugRenderer
    {
        public  ShaderProgram simpleShader = new ShaderProgram("Debug.vert", "Debug_Simple.frag");
        public  ShaderProgram circleShader = new ShaderProgram("Debug.vert", "Debug_Circle.frag");
        public  ShaderProgram RingShader = new ShaderProgram("Debug.vert", "Debug_Ring.frag");
        public ShaderProgram _simpleShader = new ShaderProgram("Simple.vert", "Simple.frag");

        public ShaderProgram _normalsShader = new ShaderProgram("Debug_Normals.vert", "Debug_Normals.frag", "Debug_Normals.geo");

        private glModel unitSquare;
        private glModel lineBase;
        private Matrix4 projection;

        public List<Circle> circles = new List<Circle>();
        public List<Line> lines = new List<Line>();
        public List<Ring> rings = new List<Ring>();

        public static int texture;

        public DebugRenderer()
        {
            float[] positions = { -1, 1, -1, -1, 1, -1, 1, 1 };
            int[] indices = { 0, 1, 2, 3, 0, 2 };
            unitSquare = glLoader.loadToVAO(positions, indices, 2);

            float[] positionsLine = { 0, 0.5f, 0, -0.5f, 1, -0.5f, 1, 0.5f };
            int[] indicesLine = { 0, 1, 2, 3, 0, 2 };
            lineBase = glLoader.loadToVAO(positionsLine, indicesLine, 2);

            _simpleShader.bind();
            _simpleShader.loadUniformInt("blitTexture", 0);
            _simpleShader.unBind();

        }
        public void prepareFrame()
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            projection = Matrix4.CreateOrthographicOffCenter(0.0f, Engine.Resolution.X, 0.0f, Engine.Resolution.Y, -1.0f, 1.0f);
            GL.DepthMask(true);
            GL.Disable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.ClearColor(1.0f, 1.0f, 1.0f, 0.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        }

        public void render(DualBuffer buffer)
        {

            prepareFrame();

            _simpleShader.bind();

            GL.Viewport(0, 0, 512, 512);
            buffer.RenderTextureToScreen(texture);
            Engine.WindowHandler.refreshViewport();

            renderLines();
            renderRings();
            GL.Enable(EnableCap.Blend);
            rendercircles();

            finishFrame();
        }

        public void RenderNormals(DualBuffer renderer)
        {
            /*
            _normalsShader.bind();
            renderer.GetLastFrameBuffer().bind();
            //renderer.GetLastFrameBuffer().ClearColorDepth();
            GL.Enable(EnableCap.DepthTest);
            GL.Disable(EnableCap.Blend);
            Matrix4 viewMatrix = MyMath.createViewMatrix(camera.getComponent<TransformationComponent>().Transformation);
            Matrix4 projectionMatrix = camera.getComponent<ProjectionComponent>().ProjectionMatrix;
            _normalsShader.loadUniformMatrix4f("viewMatrix", viewMatrix);
            _normalsShader.loadUniformMatrix4f("projectionMatrix", projectionMatrix);
            _normalsShader.loadUniformFloat("lineLength", 0.6f);

            foreach (KeyValuePair<glModel, List<EntityOLD>> glmodels in eCSEngine.getSystem<ModelRenderSystem>().ModelsDictionary)
            {
                glModel glmodel = glmodels.Key;

                GL.BindVertexArray(glmodel.getVAOID());
                GL.EnableVertexAttribArray(0);
                GL.EnableVertexAttribArray(2);
                GL.EnableVertexAttribArray(3);
                foreach (EntityOLD entity in glmodels.Value)
                {
                    Matrix4 transformationMatrix = MyMath.createTransformationMatrix(entity.getComponent<TransformationComponent>().Transformation);
                    _normalsShader.loadUniformMatrix4f("modelMatrix", transformationMatrix);

                    GL.DrawElements(PrimitiveType.Points, glmodel.getVertexCount(), DrawElementsType.UnsignedInt, 0);
                }
            }
            GL.DisableVertexAttribArray(0);
            GL.DisableVertexAttribArray(1);
            GL.DisableVertexAttribArray(2);
            GL.DisableVertexAttribArray(3);
            GL.DisableVertexAttribArray(4);
            GL.DisableVertexAttribArray(5);
            */
        }

        private void renderRings()
        {
            RingShader.bind();
            foreach (Ring ring in rings)
            {
                RingShader.loadUniformMatrix4f("uProjection", projection);
                RingShader.loadUniformVector2f("center", ring.Transformation.position);
                RingShader.loadUniformVector3f("color", ring.Color);
                RingShader.loadUniformFloat("radius", ring.Radius);
                RingShader.loadUniformFloat("width", ring.Width);
                RingShader.loadUniformMatrix4f("modelMatrix", createTransformationMatrix(ring.Transformation));

                glModel glmodel = unitSquare;
                GL.BindVertexArray(glmodel.getVAOID());
                GL.EnableVertexAttribArray(0);
                GL.DrawElements(PrimitiveType.Triangles, glmodel.getVertexCount(), DrawElementsType.UnsignedInt, 0);
            }
            RingShader.unBind();
        }

        private void rendercircles()
        {
            circleShader.bind();
            foreach (Circle circle in circles)
            {
                circleShader.loadUniformMatrix4f("uProjection", projection);
                circleShader.loadUniformVector2f("center", circle.Transformation.position);
                circleShader.loadUniformVector3f("color", circle.Color);
                circleShader.loadUniformFloat("radius", circle.Transformation.scale.X);
                circleShader.loadUniformMatrix4f("modelMatrix", createTransformationMatrix(circle.Transformation));

                glModel glmodel = unitSquare;
                GL.BindVertexArray(glmodel.getVAOID());
                GL.EnableVertexAttribArray(0);
                GL.DrawElements(PrimitiveType.Triangles, glmodel.getVertexCount(), DrawElementsType.UnsignedInt, 0);
            }
            circleShader.unBind();
        }

        private void renderLines()
        {
            simpleShader.bind();
            foreach (Line line in lines)
            {
                simpleShader.loadUniformMatrix4f("uProjection", projection);
                simpleShader.loadUniformVector3f("color", line.Color);
                simpleShader.loadUniformMatrix4f("modelMatrix", createTransformationMatrix(line.Transformation));

                glModel glmodel = lineBase;
                GL.BindVertexArray(glmodel.getVAOID());
                GL.EnableVertexAttribArray(0);
                GL.DrawElements(PrimitiveType.Triangles, glmodel.getVertexCount(), DrawElementsType.UnsignedInt, 0);
            }
            simpleShader.unBind();
        }
        public void finishFrame()
        {

        }

        private Matrix4 createTransformationMatrix(Transformation2D transformation)
        {
            Matrix4 matrix = Matrix4.Identity;
            matrix = matrix * Matrix4.CreateScale(transformation.scale.X,  transformation.scale.Y, 0);
            matrix = matrix * Matrix4.CreateRotationZ(transformation.rotation);
            matrix = matrix * Matrix4.CreateTranslation(transformation.position.X,transformation.position.Y, 0);
            return matrix;
        }
    }

}

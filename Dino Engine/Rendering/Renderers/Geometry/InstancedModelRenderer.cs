using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using Dino_Engine.Rendering.Renderers;
using Dino_Engine.ECS;
using Dino_Engine.Util;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Debug;
using System.Numerics;
using System.Reflection;
using Dino_Engine.Modelling;
using static Dino_Engine.Modelling.MeshGenerator;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Dino_Engine.Rendering.Renderers.Geometry
{
    internal class InstancedModelRenderer : Renderer
    {
        private ShaderProgram _instancedModelShader = new ShaderProgram("Instanced_Model.vert", "Model.frag");
        private static readonly int Matrix4SizeInBytes = 4 * 4 * sizeof(float);
        private static readonly int INSTANCE_DATA_LENGTH = 16;
        private int pointer = 0;
        private List<int> vbos = new List<int>();
        private bool reAllocate = true;

        public InstancedModelRenderer()
        {
        }
        internal override void Prepare(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
            GL.DepthMask(true);
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.Disable(EnableCap.Blend);
            _instancedModelShader.bind();

        }
        internal override void Render(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
            if (reAllocate)
            {
                foreach (int vbo in vbos)
                {
                    GL.DeleteBuffer(vbo);
                }
            }

            
            Entity camera = eCSEngine.Camera;
            Matrix4 viewMatrix = MyMath.createViewMatrix(camera.getComponent<TransformationComponent>().Transformation);
            Matrix4 projectionMatrix = camera.getComponent<ProjectionComponent>().ProjectionMatrix;

            _instancedModelShader.loadUniformMatrix4f("viewMatrix", viewMatrix);
            _instancedModelShader.loadUniformMatrix4f("projectionMatrix", projectionMatrix);

            foreach (KeyValuePair<glModel, List<Entity>> glmodels in eCSEngine.getSystem<InstancedModelSystem>().ModelsDictionary)
            {
                pointer = 0;
                glModel glmodel = glmodels.Key;
                if (reAllocate)
                {
                    //Console.WriteLine("allocating");
                    float[] vboData = new float[glmodels.Value.Count * INSTANCE_DATA_LENGTH];
                    foreach (Entity entity in glmodels.Value)
                    {
                        Matrix4 modelMatrix = MyMath.createTransformationMatrix(entity.getComponent<TransformationComponent>().Transformation);
                        storeMatrixData(modelMatrix, vboData);
                    }

                    GL.BindVertexArray(glmodel.getVAOID());
                    int vbo = createVbo(INSTANCE_DATA_LENGTH * glmodels.Value.Count, vboData);
                    vbos.Add(vbo);
                    addInstancedAttribute(glmodel.getVAOID(), vbo, 4, 4, INSTANCE_DATA_LENGTH, 0);
                    addInstancedAttribute(glmodel.getVAOID(), vbo, 5, 4, INSTANCE_DATA_LENGTH, 4);
                    addInstancedAttribute(glmodel.getVAOID(), vbo, 6, 4, INSTANCE_DATA_LENGTH, 8);
                    addInstancedAttribute(glmodel.getVAOID(), vbo, 7, 4, INSTANCE_DATA_LENGTH, 12);
                }


                GL.BindVertexArray(glmodel.getVAOID());
                GL.EnableVertexAttribArray(0);
                GL.EnableVertexAttribArray(1);
                GL.EnableVertexAttribArray(2);
                GL.EnableVertexAttribArray(3);
                GL.EnableVertexAttribArray(4);
                GL.EnableVertexAttribArray(5);
                GL.EnableVertexAttribArray(6);
                GL.EnableVertexAttribArray(7);




                GL.DrawElementsInstanced(PrimitiveType.Triangles, glmodel.getVertexCount(), DrawElementsType.UnsignedInt, 0, glmodels.Value.Count);
            }
            reAllocate = true;
        }

        private void storeMatrixData(Matrix4 matrix, float[] vboData)
        {
            vboData[pointer++] = matrix.M11;
            vboData[pointer++] = matrix.M12;
            vboData[pointer++] = matrix.M13;
            vboData[pointer++] = matrix.M14 ;
            vboData[pointer++] = matrix.M21;
            vboData[pointer++] = matrix.M22;
            vboData[pointer++] = matrix.M23;
            vboData[pointer++] = matrix.M24;
            vboData[pointer++] = matrix.M31;
            vboData[pointer++] = matrix.M32;
            vboData[pointer++] = matrix.M33;
            vboData[pointer++] = matrix.M34;
            vboData[pointer++] = matrix.M41;
            vboData[pointer++] = matrix.M42;
            vboData[pointer++] = matrix.M43;
            vboData[pointer++] = matrix.M44;
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
            GL.DisableVertexAttribArray(4);
            GL.DisableVertexAttribArray(5);
            GL.DisableVertexAttribArray(6);
            GL.DisableVertexAttribArray(7);
            GL.BindVertexArray(0);
        }
        public override void CleanUp()
        {
            _instancedModelShader.cleanUp();
        }

        public int createVbo(int floatCount, float[] data)
        {
            int vbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, floatCount * 4, data, BufferUsageHint.StaticDraw);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            return vbo;
        }

        public void addInstancedAttribute(int vao, int vbo, int attribute, int dataSize, int instancedDataLength, int offset)
        {
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            GL.BindVertexArray(vao);
            GL.VertexAttribPointer(attribute, dataSize, VertexAttribPointerType.Float, false, instancedDataLength * 4, offset * 4);
            GL.VertexAttribDivisor(attribute, 1);

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);
        }
    }
}

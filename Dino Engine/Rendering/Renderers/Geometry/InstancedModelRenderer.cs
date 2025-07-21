using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using Dino_Engine.Modelling.Model;
using System.Runtime.InteropServices;

namespace Dino_Engine.Rendering.Renderers.Geometry
{

    public class InstancedModelRenderer : CommandDrivenRenderer<ModelRenderCommand>
    {
        private ShaderProgram _modelShader = new ShaderProgram("ModelInstanced.vert", "Model.frag");
        private int _instanceVBO;

        public InstancedModelRenderer()
        {
            _modelShader.bind();
            _modelShader.loadUniformInt("albedoMapTextureArray", 0);
            _modelShader.loadUniformInt("normalMapTextureArray", 1);
            _modelShader.loadUniformInt("materialMapTextureArray", 2);

            _modelShader.loadUniformInt("albedoMapModelTextureArray", 3);
            _modelShader.loadUniformInt("normalMapModelTextureArray", 4);
            _modelShader.loadUniformInt("materialMapModelTextureArray", 5);
            _modelShader.unBind();

            _instanceVBO = GL.GenBuffer();
        }

        internal override void Prepare(RenderEngine renderEngine)
        {

            GL.Enable(EnableCap.DepthTest);
            //GL.Enable(EnableCap.CullFace);
            GL.Disable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.Disable(EnableCap.Blend);
            _modelShader.bind();

            _modelShader.loadUniformFloat("parallaxDepth", 0.05f);
            _modelShader.loadUniformFloat("parallaxLayers", 30f);

            _modelShader.loadUniformInt("numberOfMaterials", renderEngine.textureGenerator.loadedMaterialTextures);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaAlbedoTextureArray);
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaNormalTextureArray);
            GL.ActiveTexture(TextureUnit.Texture2);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaMaterialTextureArray);

            GL.ActiveTexture(TextureUnit.Texture3);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaAlbedoModelTextureArray);
            GL.ActiveTexture(TextureUnit.Texture4);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaNormalModelTextureArray);
            GL.ActiveTexture(TextureUnit.Texture5);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaMaterialModelTextureArray);


        }

        public override void PerformCommand(ModelRenderCommand command, RenderEngine renderEngine)
        {
            glModel glmodel = command.model;
            int instanceCount = command.localToWorldMatrices.Length;
            int sizeInBytes = instanceCount * Marshal.SizeOf<Matrix4>();

            // Upload per-instance matrices
            GL.BindBuffer(BufferTarget.ArrayBuffer, _instanceVBO);
            GL.BufferData(BufferTarget.ArrayBuffer, sizeInBytes, command.localToWorldMatrices, BufferUsageHint.DynamicDraw);


            GL.BindVertexArray(glmodel.getVAOID());
            GL.EnableVertexAttribArray(0);
            GL.EnableVertexAttribArray(1);
            GL.EnableVertexAttribArray(2);
            GL.EnableVertexAttribArray(3);
            GL.EnableVertexAttribArray(4);
            GL.EnableVertexAttribArray(5);

            GL.EnableVertexAttribArray(6);
            GL.EnableVertexAttribArray(7);
            GL.EnableVertexAttribArray(8);
            GL.EnableVertexAttribArray(9);

            // Set up instanced attributes 6-9 (one-time per VAO, could live in glModel)
            
            GL.BindBuffer(BufferTarget.ArrayBuffer, _instanceVBO);
            int vec4Size = Vector4.SizeInBytes;
            for (int i = 0; i < 4; i++)
            {
                int attribLocation = 6 + i;
                GL.EnableVertexAttribArray(attribLocation);
                GL.VertexAttribPointer(attribLocation, 4, VertexAttribPointerType.Float, false, sizeof(float) * 16, i * vec4Size);
                GL.VertexAttribDivisor(attribLocation, 1);
            }
            
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            
            // Draw all instances
            GL.DrawElementsInstanced(
                PrimitiveType.Triangles,
                glmodel.getVertexCount(),
                DrawElementsType.UnsignedInt,
                IntPtr.Zero,
                instanceCount);
        }

        internal override void Finish(RenderEngine renderEngine)
        {
            GL.DisableVertexAttribArray(0);
            GL.DisableVertexAttribArray(1);
            GL.DisableVertexAttribArray(2);
            GL.DisableVertexAttribArray(3);
            GL.DisableVertexAttribArray(4);
            GL.DisableVertexAttribArray(5);
            GL.DisableVertexAttribArray(6);
            GL.DisableVertexAttribArray(7);
            GL.DisableVertexAttribArray(8);
            GL.DisableVertexAttribArray(9);
            GL.BindVertexArray(0);
        }
        public override void CleanUp()
        {
            _modelShader.cleanUp();
        }


    }
}

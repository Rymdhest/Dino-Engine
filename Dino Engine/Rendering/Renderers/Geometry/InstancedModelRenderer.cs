using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using Dino_Engine.Modelling.Model;
using System.Runtime.InteropServices;
using Dino_Engine.Rendering.Renderers.Lighting;

namespace Dino_Engine.Rendering.Renderers.Geometry
{

    public class InstancedModelRenderer : GeometryCommandDrivenRenderer<ModelRenderCommand>
    {
        private ShaderProgram _instancedModelShader = new ShaderProgram("Model_Instanced.vert", "Model.frag");
        private ShaderProgram _instancedModelShadowShader = new ShaderProgram("Model_Instanced_shadow.vert", "Shadow.frag");
        private int _instanceVBO;

        public InstancedModelRenderer()
        {
            _instancedModelShader.bind();
            _instancedModelShader.loadUniformInt("albedoMapTextureArray", 0);
            _instancedModelShader.loadUniformInt("normalMapTextureArray", 1);
            _instancedModelShader.loadUniformInt("materialMapTextureArray", 2);

            _instancedModelShader.loadUniformInt("albedoMapModelTextureArray", 3);
            _instancedModelShader.loadUniformInt("normalMapModelTextureArray", 4);
            _instancedModelShader.loadUniformInt("materialMapModelTextureArray", 5);
            _instancedModelShader.unBind();

            _instancedModelShadowShader.bind();
            _instancedModelShadowShader.loadUniformInt("albedoMapTextureArray", 0);
            _instancedModelShadowShader.loadUniformInt("albedoMapModelTextureArray", 3);
            _instancedModelShadowShader.unBind();

            _instanceVBO = GL.GenBuffer();
        }


        public override void CleanUp()
        {
            _instancedModelShader.cleanUp();
            _instancedModelShadowShader.cleanUp();
        }

        internal override void PrepareGeometry(RenderEngine renderEngine)
        {
            GL.Enable(EnableCap.DepthTest);
            //GL.Enable(EnableCap.CullFace);
            GL.Disable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.Disable(EnableCap.Blend);
            _instancedModelShader.bind();

            _instancedModelShader.loadUniformFloat("parallaxDepth", 0.05f);
            _instancedModelShader.loadUniformFloat("parallaxLayers", 30f);

            _instancedModelShader.loadUniformInt("numberOfMaterials", renderEngine.textureGenerator.loadedMaterialTextures);

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

        internal override void FinishGeometry(RenderEngine renderEngine)
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

        internal override void PrepareShadow(RenderEngine renderEngine)
        {
            GL.DepthMask(true);
            GL.Enable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);
            GL.Disable(EnableCap.Blend);
            GL.Enable(EnableCap.PolygonOffsetFill);
            GL.CullFace(CullFaceMode.Front);

            _instancedModelShadowShader.bind();
            _instancedModelShadowShader.loadUniformInt("numberOfMaterials", renderEngine.textureGenerator.loadedMaterialTextures);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaAlbedoTextureArray);

            GL.ActiveTexture(TextureUnit.Texture3);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaAlbedoModelTextureArray);
        }

        internal override void FinishShadow(RenderEngine renderEngine)
        {
            GL.Disable(EnableCap.PolygonOffsetFill);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);

            GL.DisableVertexAttribArray(0);
            GL.DisableVertexAttribArray(4);
            GL.DisableVertexAttribArray(5);
            GL.DisableVertexAttribArray(6);
            GL.DisableVertexAttribArray(7);
            GL.DisableVertexAttribArray(8);
            GL.DisableVertexAttribArray(9);
            GL.BindVertexArray(0);
        }

        internal override void PerformGeometryCommand(ModelRenderCommand command, RenderEngine renderEngine)
        {
            glModel glmodel = command.model;
            int instanceCount = command.matrices.Length;
            int sizeInBytes = instanceCount * Marshal.SizeOf<Matrix4>();

            // Upload per-instance matrices
            GL.BindBuffer(BufferTarget.ArrayBuffer, _instanceVBO);
            GL.BufferData(BufferTarget.ArrayBuffer, sizeInBytes, command.matrices, BufferUsageHint.DynamicDraw);


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

        internal override void PerformShadowCommand(ModelRenderCommand command, Shadow shadow, RenderEngine renderEngine)
        {
            shadow.cascadeFrameBuffer.bind();
            //GL.Clear(ClearBufferMask.DepthBufferBit);
            GL.PolygonOffset(shadow.polygonOffset, shadow.polygonOffset * 10.1f);
            //GL.PolygonOffset(4f, 1f);

            glModel glmodel = command.model;

            int instanceCount = command.matrices.Length;
            int sizeInBytes = instanceCount * Marshal.SizeOf<Matrix4>();

            _instancedModelShadowShader.loadUniformMatrix4f("projectionMatrix", shadow.cascadeProjectionMatrix);
            _instancedModelShadowShader.loadUniformMatrix4f("viewMatrix", shadow.lightViewMatrix);

            // Upload per-instance matrices
            GL.BindBuffer(BufferTarget.ArrayBuffer, _instanceVBO);
            GL.BufferData(BufferTarget.ArrayBuffer, sizeInBytes, command.matrices, BufferUsageHint.DynamicDraw);

            GL.BindVertexArray(glmodel.getVAOID());
            GL.EnableVertexAttribArray(0);
            GL.EnableVertexAttribArray(4);
            GL.EnableVertexAttribArray(5);

            GL.EnableVertexAttribArray(6);
            GL.EnableVertexAttribArray(7);
            GL.EnableVertexAttribArray(8);
            GL.EnableVertexAttribArray(9);

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
    }
}

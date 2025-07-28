using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Rendering.Renderers.Lighting;

namespace Dino_Engine.Rendering.Renderers.Geometry
{
    public struct ModelRenderCommand : IRenderCommand
    {
        public Matrix4[] matrices;
        public glModel model;
    }
    public class ModelRenderer : GeometryCommandDrivenRenderer<ModelRenderCommand>
    {
        private ShaderProgram _modelShader = new ShaderProgram("Model.vert", "Model.frag");
        private ShaderProgram _modelShadowShader = new ShaderProgram("Model_Shadow.vert", "Shadow.frag");

        public ModelRenderer() : base("Model")
        {
            _modelShader.bind();
            _modelShader.loadUniformInt("albedoMapTextureArray", 0);
            _modelShader.loadUniformInt("normalMapTextureArray", 1);
            _modelShader.loadUniformInt("materialMapTextureArray", 2);

            _modelShader.loadUniformInt("albedoMapModelTextureArray", 3);
            _modelShader.loadUniformInt("normalMapModelTextureArray", 4);
            _modelShader.loadUniformInt("materialMapModelTextureArray", 5);
            _modelShader.unBind();

            _modelShadowShader.bind();
            _modelShadowShader.loadUniformInt("albedoMapTextureArray", 0);
            _modelShadowShader.loadUniformInt("albedoMapModelTextureArray", 3);
            _modelShadowShader.unBind();
        }

        public override void CleanUp()
        {
            _modelShadowShader.cleanUp();
            _modelShader.cleanUp();
        }

        internal override void PrepareGeometry(RenderEngine renderEngine)
        {
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
            //GL.Disable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.Disable(EnableCap.Blend);
            _modelShader.bind();

            _modelShader.loadUniformFloat("parallaxDepth", 0.08f);
            _modelShader.loadUniformFloat("parallaxLayers", 20f);

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

        internal override void FinishGeometry(RenderEngine renderEngine)
        {
            GL.DisableVertexAttribArray(0);
            GL.DisableVertexAttribArray(1);
            GL.DisableVertexAttribArray(2);
            GL.DisableVertexAttribArray(3);
            GL.DisableVertexAttribArray(4);
            GL.DisableVertexAttribArray(5);
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

            _modelShadowShader.bind();
            _modelShadowShader.loadUniformInt("numberOfMaterials", renderEngine.textureGenerator.loadedMaterialTextures);

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
        }

        internal override void PerformGeometryCommand(ModelRenderCommand command, RenderEngine renderEngine)
        {
            glModel glmodel = command.model;

            GL.BindVertexArray(glmodel.getVAOID());
            GL.EnableVertexAttribArray(0);
            GL.EnableVertexAttribArray(1);
            GL.EnableVertexAttribArray(2);
            GL.EnableVertexAttribArray(3);
            GL.EnableVertexAttribArray(4);
            GL.EnableVertexAttribArray(5);

            for (int i = 0; i < command.matrices.Length; i++)
            {
                Matrix4 transformationMatrix = command.matrices[i];
                Matrix4 modelViewMatrix = transformationMatrix * renderEngine.context.viewMatrix;
                _modelShader.loadUniformMatrix4f("modelMatrix", transformationMatrix);
                _modelShader.loadUniformMatrix4f("normalModelViewMatrix", Matrix4.Transpose(Matrix4.Invert(modelViewMatrix)));

                GL.DrawElements(PrimitiveType.Triangles, glmodel.getVertexCount(), DrawElementsType.UnsignedInt, 0);
            }
        }

        internal override void PerformShadowCommand(ModelRenderCommand command, Shadow shadow, RenderEngine renderEngine)
        {
            shadow.shadowFrameBuffer.bind();
            for (int i = 0; i < command.matrices.Length; i++)
            {
                //GL.Clear(ClearBufferMask.DepthBufferBit);
                GL.PolygonOffset(shadow.polygonOffset, shadow.polygonOffset * 10.1f);
                //GL.PolygonOffset(4f, 1f);

                glModel glmodel = command.model;
                GL.BindVertexArray(glmodel.getVAOID());
                GL.EnableVertexAttribArray(0);
                GL.EnableVertexAttribArray(4);
                GL.EnableVertexAttribArray(5);
                GL.EnableVertexAttribArray(6);

                Matrix4 transformationMatrix = command.matrices[i];
                _modelShadowShader.loadUniformMatrix4f("modelViewProjectionMatrix", transformationMatrix * shadow.lightViewMatrix * shadow.shadowProjectionMatrix);

                GL.DrawElements(PrimitiveType.Triangles, glmodel.getVertexCount(), DrawElementsType.UnsignedInt, 0);

            }
        }
    }
}

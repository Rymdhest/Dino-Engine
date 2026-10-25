using System.Collections.Generic;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Rendering.Renderers.Lighting;

namespace Dino_Engine.Rendering.Renderers.Geometry
{
    public struct CharacterInstanceData
    {
        public Matrix4 ModelMatrix;
        public Matrix4[] SkinningMatrices;
    }
    public struct CharacterRenderCommand : IRenderCommand
    {
        public glModel model;
        public List<CharacterInstanceData> instances;

        public CharacterRenderCommand(glModel model, List<CharacterInstanceData> instances)
        {
            this.model = model;
            this.instances = instances;
        }
    }
    public class CharacterRenderer : GeometryCommandDrivenRenderer<CharacterRenderCommand>
    {
        private ShaderProgram _characterShader = new ShaderProgram("Model_Character.vert", "Model.frag");
        private ShaderProgram _characterShadowShader = new ShaderProgram("Model_Character_Shadow.vert", "Shadow.frag");

        public CharacterRenderer() : base("Character")
        {
            _characterShader.bind();
            _characterShader.loadUniformInt("albedoMapTextureArray", 0);
            _characterShader.loadUniformInt("normalMapTextureArray", 1);
            _characterShader.loadUniformInt("materialMapTextureArray", 2);

            _characterShader.loadUniformInt("albedoMapModelTextureArray", 3);
            _characterShader.loadUniformInt("normalMapModelTextureArray", 4);
            _characterShader.loadUniformInt("materialMapModelTextureArray", 5);
            _characterShader.unBind();

            _characterShadowShader.bind();
            _characterShadowShader.loadUniformInt("albedoMapTextureArray", 0);
            _characterShadowShader.loadUniformInt("albedoMapModelTextureArray", 3);
            _characterShadowShader.unBind();
        }

        public override void CleanUp()
        {
            _characterShadowShader.cleanUp();
            _characterShader.cleanUp();
        }

        internal override void PrepareGeometry(RenderEngine renderEngine)
        {
            GL.Enable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.Disable(EnableCap.Blend);
            _characterShader.bind();

            _characterShader.loadUniformFloat("parallaxDepth", 0.0f);
            _characterShader.loadUniformFloat("parallaxLayers", 60);

            _characterShader.loadUniformInt("numberOfMaterials", renderEngine.textureGenerator.loadedMaterialTextures);

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
            /*
            GL.DisableVertexAttribArray(0);
            GL.DisableVertexAttribArray(1);
            GL.DisableVertexAttribArray(2);
            GL.DisableVertexAttribArray(3);
            GL.DisableVertexAttribArray(4);
            GL.DisableVertexAttribArray(5);
            GL.DisableVertexAttribArray(6); // Bone Indices
            GL.DisableVertexAttribArray(7); // Bone Weights
            */
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

            _characterShadowShader.bind();
            _characterShadowShader.loadUniformInt("numberOfMaterials", renderEngine.textureGenerator.loadedMaterialTextures);

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
            GL.DisableVertexAttribArray(6); // Bone Indices
            GL.DisableVertexAttribArray(7); // Bone Weights
        }

        internal override void PerformGeometryCommand(CharacterRenderCommand command, RenderEngine renderEngine)
        {
            glModel glmodel = command.model;

            GL.BindVertexArray(glmodel.getVAOID());
            GL.EnableVertexAttribArray(0); // Position
            GL.EnableVertexAttribArray(1); // Normal
            GL.EnableVertexAttribArray(2); // UV
            GL.EnableVertexAttribArray(3); // Tangent
            GL.EnableVertexAttribArray(4); // Material Index
            GL.EnableVertexAttribArray(5); // Model Texture Index
            GL.EnableVertexAttribArray(6); // Bone Indices (ivec4)
            GL.EnableVertexAttribArray(7); // Bone Weights (vec4)

            for (int i = 0; i < command.instances.Count; i++)
            {
                CharacterInstanceData instance = command.instances[i];
                Matrix4 transformationMatrix = instance.ModelMatrix;
                Matrix4 modelViewMatrix = transformationMatrix * renderEngine.context.viewMatrix;

                _characterShader.loadUniformMatrix4f("modelMatrix", transformationMatrix);
                _characterShader.loadUniformMatrix4f("normalModelViewMatrix", Matrix4.Transpose(Matrix4.Invert(modelViewMatrix)));

                for (int j = 0; j< instance.SkinningMatrices.Length; j++)
                {
                    //instance.SkinningMatrices[j] = instance.SkinningMatrices[0];
                }

                // Upload array of skinning matrices: uniform mat4 u_Bones[14];
                _characterShader.loadUniformMatrix4fArray("u_Bones", instance.SkinningMatrices);

                GL.DrawElements(PrimitiveType.Triangles, glmodel.getVertexCount(), DrawElementsType.UnsignedInt, 0);
            }
        }

        internal override void PerformShadowCommand(CharacterRenderCommand command, Shadow shadow, RenderEngine renderEngine)
        {
            Console.WriteLine("character shadow");
            if (shadow.isCubeMap && shadow.cubemapFaceIndex >= 0)
            {
                shadow.shadowFrameBuffer.bindFace(TextureTarget.TextureCubeMapPositiveX + shadow.cubemapFaceIndex);
            }
            else
            {
                shadow.shadowFrameBuffer.bind();
            }

            for (int i = 0; i < command.instances.Count; i++)
            {
                GL.PolygonOffset(shadow.polygonOffsetModel, shadow.polygonOffsetModel * 10.1f);

                glModel glmodel = command.model;
                GL.BindVertexArray(glmodel.getVAOID());
                GL.EnableVertexAttribArray(0); // Position
                GL.EnableVertexAttribArray(4); // Material Index
                GL.EnableVertexAttribArray(5); // Model Texture Index
                GL.EnableVertexAttribArray(6); // Bone Indices
                GL.EnableVertexAttribArray(7); // Bone Weights

                CharacterInstanceData instance = command.instances[i];
                Matrix4 transformationMatrix = instance.ModelMatrix;

                _characterShadowShader.loadUniformMatrix4f("modelViewProjectionMatrix", transformationMatrix * shadow.lightViewMatrix * shadow.shadowProjectionMatrix);
                _characterShadowShader.loadUniformMatrix4fArray("u_Bones", instance.SkinningMatrices);

                GL.DrawElements(PrimitiveType.Triangles, glmodel.getVertexCount(), DrawElementsType.UnsignedInt, 0);
            }
        }
    }
}
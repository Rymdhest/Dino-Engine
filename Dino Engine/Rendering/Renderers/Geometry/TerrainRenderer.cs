using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Core;
using Dino_Engine.Util;

namespace Dino_Engine.Rendering.Renderers.Geometry
{
    public struct TerrainChunkRenderCommand : IRenderCommand
    {
        public Vector3 chunkPos;
        public Vector3 size;
    }

    public class TerrainRenderer : CommandDrivenRenderer<TerrainChunkRenderCommand>
    {
        private ShaderProgram _terrainShader = new ShaderProgram("Terrain.vert", "Terrain.frag");

        public TerrainRenderer()
        {
            _terrainShader.bind();
            _terrainShader.loadUniformInt("albedoMapTextureArray", 0);
            _terrainShader.loadUniformInt("normalMapTextureArray", 1);
            _terrainShader.loadUniformInt("materialMapTextureArray", 2);

            _terrainShader.loadUniformInt("albedoMapModelTextureArray", 3);
            _terrainShader.loadUniformInt("normalMapModelTextureArray", 4);
            _terrainShader.loadUniformInt("materialMapModelTextureArray", 5);

            _terrainShader.loadUniformInt("heightMap", 6);

            _terrainShader.unBind();
        }
        internal override void Prepare(RenderEngine renderEngine)
        {

            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
            //GL.Disable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.Disable(EnableCap.Blend);
            _terrainShader.bind();

            _terrainShader.loadUniformFloat("parallaxDepth", 0.06f);
            _terrainShader.loadUniformFloat("parallaxLayers", 40f);

            _terrainShader.loadUniformInt("numberOfMaterials", renderEngine.textureGenerator.loadedMaterialTextures);

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

            _terrainShader.loadUniformVector3f("viewPos", renderEngine.context.viewPos);
        }
        internal override void Render(RenderEngine renderEngine)
        {
            Matrix4 viewMatrix = renderEngine.context.viewMatrix;
            Matrix4 projectionMatrix = renderEngine.context.projectionMatrix;
            /*
            foreach (KeyValuePair<glModel, List<EntityOLD>> glmodels in eCSEngine.getSystem<TerrainRenderSystem>().ModelsDictionary)
            {
                
            }
            */
        }
        public override void Update()
        {
        }
        public override void OnResize(ResizeEventArgs eventArgs)
        {
        }
        internal override void Finish(RenderEngine renderEngine)
        {
            GL.DisableVertexAttribArray(0);
            GL.DisableVertexAttribArray(1);
            GL.DisableVertexAttribArray(2);
            GL.DisableVertexAttribArray(3);
            GL.DisableVertexAttribArray(4);
            GL.DisableVertexAttribArray(5);
            GL.BindVertexArray(0);
        }
        public override void CleanUp()
        {
            _terrainShader.cleanUp();
        }

        public override void PerformCommand(TerrainChunkRenderCommand command, RenderEngine renderEngine)
        {
            /*
            glModel glmodel = glmodels.Key;


            _terrainShader.loadUniformFloat("groundID", Engine.RenderEngine.textureGenerator.sandDunes);
            _terrainShader.loadUniformFloat("rockID", Engine.RenderEngine.textureGenerator.rock);

            GL.BindVertexArray(glmodel.getVAOID());
            GL.EnableVertexAttribArray(0);
            GL.EnableVertexAttribArray(1);
            GL.EnableVertexAttribArray(2);
            GL.EnableVertexAttribArray(3);
            GL.EnableVertexAttribArray(4);
            GL.EnableVertexAttribArray(5);
            foreach (EntityOLD entity in glmodels.Value)
            {
                Matrix4 transformationMatrix = MyMath.createTransformationMatrix(entity.getComponent<TransformationComponent>().Transformation);
                Matrix4 modelViewMatrix = transformationMatrix * viewMatrix;
                _terrainShader.loadUniformMatrix4f("modelMatrix", transformationMatrix);
                _terrainShader.loadUniformMatrix4f("modelViewMatrix", modelViewMatrix);
                _terrainShader.loadUniformMatrix4f("modelViewProjectionMatrix", modelViewMatrix * projectionMatrix);
                _terrainShader.loadUniformMatrix4f("normalModelViewMatrix", Matrix4.Transpose(Matrix4.Invert(modelViewMatrix)));

                GL.DrawElements(PrimitiveType.Triangles, glmodel.getVertexCount(), DrawElementsType.UnsignedInt, 0);
            }
            */
        }
    }
}

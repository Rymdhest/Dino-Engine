
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Graphics.OpenGL;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Rendering.Renderers.Geometry;

namespace Dino_Engine.Rendering.Renderers.Lighting
{
    public struct ShadowCascade
    {
        public Matrix4 lightViewMatrix;
        public Matrix4 cascadeProjectionMatrix;
        public FrameBuffer cascadeFrameBuffer;
        public float projectionSize;
        public float polygonOffset;
        public ShadowCascade(Vector2i resolution, float projectionSize, float polygonOffset)
        {
            this.projectionSize = projectionSize;
            this.polygonOffset = polygonOffset;
            FrameBufferSettings settings = new FrameBufferSettings(resolution);
            DepthAttachmentSettings depthAttachmentSettings = new DepthAttachmentSettings();
            depthAttachmentSettings.isTexture = true;
            depthAttachmentSettings.isShadowDepthTexture = true;
            settings.depthAttachmentSettings = depthAttachmentSettings;
            cascadeFrameBuffer = new FrameBuffer(settings);
            lightViewMatrix = Matrix4.Identity;
            cascadeProjectionMatrix = Matrix4.CreateOrthographic(projectionSize, projectionSize, -projectionSize, projectionSize);
        }
    }

    public struct ShadowCascadeCommand : IRenderCommand
    {
        public ModelRenderCommand[] modelCommands;
        public ShadowCascade cascade;
    }

    public struct DirectionalShadowRenderCommand : IRenderCommand
    {
        public ShadowCascadeCommand[] Cascades;
    }

    public class ShadowCascadeMapRenderer : CommandDrivenRenderer<DirectionalShadowRenderCommand>
    {

        private ShaderProgram _shadowShader = new ShaderProgram("Shadow.vert", "Shadow.frag");
        private ShaderProgram _InstancedShadowShader = new ShaderProgram("Shadow_Instanced.vert", "Shadow.frag");

        public const int CASCADETEXTURESINDEXSTART = 4;

        public ShadowCascadeMapRenderer()
        {
            _shadowShader.bind();
            _shadowShader.loadUniformInt("albedoMapTextureArray", 0);

            _shadowShader.loadUniformInt("albedoMapModelTextureArray", 3);
            _shadowShader.unBind();

            _InstancedShadowShader.bind();
            _InstancedShadowShader.loadUniformInt("albedoMapTextureArray", 0);

            _InstancedShadowShader.loadUniformInt("albedoMapModelTextureArray", 3);
            _InstancedShadowShader.unBind();
        }

        internal override void Prepare(RenderEngine renderEngine)
        {
            GL.DepthMask(true);
            GL.Enable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);
            GL.Disable(EnableCap.Blend);
            GL.Enable(EnableCap.PolygonOffsetFill);
            GL.CullFace(CullFaceMode.Front);

            _shadowShader.bind();

            _shadowShader.loadUniformInt("numberOfMaterials", renderEngine.textureGenerator.loadedMaterialTextures);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaAlbedoTextureArray);

            GL.ActiveTexture(TextureUnit.Texture3);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaAlbedoModelTextureArray);
        }

        internal override void Finish(RenderEngine renderEngine)
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
        public override void CleanUp()
        {
            _shadowShader.cleanUp();
        }

        public override void PerformCommand(DirectionalShadowRenderCommand command, RenderEngine renderEngine)
        {

            foreach (ShadowCascadeCommand cascadeCommand in command.Cascades)
            {
                var cascade = cascadeCommand.cascade;
                cascade.cascadeFrameBuffer.bind();
                GL.Clear(ClearBufferMask.DepthBufferBit);
                GL.PolygonOffset(cascade.polygonOffset, cascade.polygonOffset * 10.1f);
                //GL.PolygonOffset(4f, 1f);

                foreach (ModelRenderCommand modelCommand in cascadeCommand.modelCommands)
                {
                    glModel glmodel = modelCommand.model;
                    GL.BindVertexArray(glmodel.getVAOID());
                    GL.EnableVertexAttribArray(0);
                    GL.EnableVertexAttribArray(4);
                    GL.EnableVertexAttribArray(5);

                    Matrix4 transformationMatrix = modelCommand.localToWorldMatrix;
                    _shadowShader.loadUniformMatrix4f("modelViewProjectionMatrix", transformationMatrix * cascade.lightViewMatrix * cascade.cascadeProjectionMatrix);

                    GL.DrawElements(PrimitiveType.Triangles, glmodel.getVertexCount(), DrawElementsType.UnsignedInt, 0);
                }
            }
        }
    }
}

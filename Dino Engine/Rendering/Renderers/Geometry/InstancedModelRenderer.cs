using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using Dino_Engine.Core;
using Dino_Engine.Modelling.Model;
using System.Runtime.InteropServices;
using Dino_Engine.Rendering.Renderers.Lighting;

namespace Dino_Engine.Rendering.Renderers.Geometry
{
    public class InstancedModelRenderer : GeometryCommandDrivenRenderer<ModelRenderCommand>
    {
        // Drawn twice: first for depth alone, with every cut Model.frag makes (Model_Depth.frag), then shaded with depth tested first and on equal, so that
        // only what's seen is shaded, once a sample. Model.frag cuts and writes the sample mask, which otherwise leaves depth to be tested after it runs,
        // and everything behind a wall or under a crown, relief and all, was shaded and thrown away.
        private ShaderProgram _instancedModelShader = new ShaderProgram("Model_Instanced.vert", "Model.frag", new[] { "DEPTH_SETTLED" });
        private ShaderProgram _instancedModelDepthShader = new ShaderProgram("Model_Instanced.vert", "Model_Depth.frag");
        private ShaderProgram _instancedModelShadowShader = new ShaderProgram("Model_Instanced_shadow.vert", "Shadow.frag");
        private int _instanceVBO;

        public InstancedModelRenderer() : base("Model Instanced")
        {
            _instancedModelShader.bind();
            _instancedModelShader.loadUniformInt("albedoMapTextureArray", 0);
            _instancedModelShader.loadUniformInt("normalMapTextureArray", 1);
            _instancedModelShader.loadUniformInt("materialMapTextureArray", 2);

            _instancedModelShader.loadUniformInt("albedoMapModelTextureArray", 3);
            _instancedModelShader.loadUniformInt("normalMapModelTextureArray", 4);
            _instancedModelShader.loadUniformInt("materialMapModelTextureArray", 5);

            LinkPlantSamplers(_instancedModelShader);
            Simulation.WeatherRenderer.LinkSurfaceSamplers(_instancedModelShader);
            _instancedModelShader.unBind();

            _instancedModelShadowShader.bind();
            _instancedModelShadowShader.loadUniformInt("albedoMapTextureArray", 0);
            _instancedModelShadowShader.loadUniformInt("albedoMapModelTextureArray", 3);

            LinkPlantSamplers(_instancedModelShadowShader);
            _instancedModelShadowShader.unBind();

            _instancedModelDepthShader.bind();
            _instancedModelDepthShader.loadUniformInt("albedoMapModelTextureArray", 3);
            LinkPlantSamplers(_instancedModelDepthShader);
            _instancedModelDepthShader.unBind();

            _instanceVBO = GL.GenBuffer();
        }

        public override void CleanUp()
        {
            _instancedModelShader.cleanUp();
            _instancedModelDepthShader.cleanUp();
            _instancedModelShadowShader.cleanUp();
            GL.DeleteBuffer(_instanceVBO);
        }

        internal override void PrepareGeometry(RenderEngine renderEngine)
        {
            GL.Enable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.Disable(EnableCap.Blend);

            GL.ActiveTexture(TextureUnit.Texture3);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaAlbedoModelTextureArray);
            BindWindTextures(renderEngine);

            // Depth alone, with the same cuts.
            GL.DepthFunc(DepthFunction.Less);
            GL.DepthMask(true);
            GL.ColorMask(false, false, false, false);
            _instancedModelDepthShader.bind();
            _instancedModelDepthShader.loadUniformInt("numberOfMaterials", renderEngine.textureGenerator.loadedMaterialTextures);
            foreach (ModelRenderCommand command in geometryCommands) Draw(_instancedModelDepthShader, command);
            GL.ColorMask(true, true, true, true);

            // Then shaded where it was laid down, and nowhere else.
            GL.DepthFunc(DepthFunction.Equal);
            GL.DepthMask(false);
            _instancedModelShader.bind();

            _instancedModelShader.loadUniformFloat("reliefScale", renderEngine.ReliefScale);

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


            BindWindTextures(renderEngine);
        }

        // What moves plants (WindRenderer, read through Plant.glsl): bendMap and canopyMap are the wind's springs, touchMap what's walking through, windMap the
        // wind field, and plantJoints the joints of the model being drawn.
        private const int BendMapUnit = 6;
        private const int WindMapUnit = 7;
        private const int CanopyMapUnit = 8;
        private const int TouchMapUnit = 9;
        private const int JointsUnit = 10;

        private static void LinkPlantSamplers(ShaderProgram shader)
        {
            shader.loadUniformInt("bendMap", BendMapUnit);
            shader.loadUniformInt("windMap", WindMapUnit);
            shader.loadUniformInt("canopyMap", CanopyMapUnit);
            shader.loadUniformInt("touchMap", TouchMapUnit);
            shader.loadUniformInt("plantJoints", JointsUnit);
        }

        private static void BindWindTextures(RenderEngine renderEngine)
        {
            GL.ActiveTexture(TextureUnit.Texture0 + BendMapUnit);
            GL.BindTexture(TextureTarget.Texture2D, renderEngine._windRenderer.VegetationBendTexture);
            GL.ActiveTexture(TextureUnit.Texture0 + WindMapUnit);
            GL.BindTexture(TextureTarget.Texture2D, renderEngine._windRenderer.WindMapTexture);
            GL.ActiveTexture(TextureUnit.Texture0 + CanopyMapUnit);
            GL.BindTexture(TextureTarget.Texture2D, renderEngine._windRenderer.CanopyTexture);
            GL.ActiveTexture(TextureUnit.Texture0 + TouchMapUnit);
            GL.BindTexture(TextureTarget.Texture2D, renderEngine._windRenderer.VegetationTouchTexture);
        }

        private static void BindPlant(ShaderProgram shader, glModel model)
        {
            bool plant = model.JointTexture != 0;
            shader.loadUniformInt("plant", plant ? 1 : 0);
            if (!plant) return;
            GL.ActiveTexture(TextureUnit.Texture0 + JointsUnit);
            GL.BindTexture(TextureTarget.TextureBuffer, model.JointTexture);
        }

        internal override void FinishGeometry(RenderEngine renderEngine)
        {
            GL.DepthFunc(DepthFunction.Less);
            GL.DepthMask(true);
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

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaAlbedoTextureArray);

            GL.ActiveTexture(TextureUnit.Texture3);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaAlbedoModelTextureArray);
        }

        internal override void PrepareShadow(RenderEngine renderEngine)
        {
            GL.DepthMask(true);
            GL.Enable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);
            GL.Disable(EnableCap.Blend);
            GL.Enable(EnableCap.PolygonOffsetFill);
            GL.CullFace(CullFaceMode.Back);

            _instancedModelShadowShader.bind();
            _instancedModelShadowShader.loadUniformInt("numberOfMaterials", renderEngine.textureGenerator.loadedMaterialTextures);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaAlbedoTextureArray);

            GL.ActiveTexture(TextureUnit.Texture3);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaAlbedoModelTextureArray);

            BindWindTextures(renderEngine);
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
            int instanceCount = command.matrices.Count;
            Engine.PerformanceMonitor.Count("Model draws", 1);
            Engine.PerformanceMonitor.Count("Model instances", instanceCount);
            Engine.PerformanceMonitor.Count("Model triangles", (long)instanceCount * glmodel.getVertexCount() / 3);
            Draw(_instancedModelShader, command);
        }

        private void Draw(ShaderProgram shader, ModelRenderCommand command)
        {
            glModel glmodel = command.model;
            int instanceCount = command.matrices.Count;
            int sizeInBytes = instanceCount * Marshal.SizeOf<Matrix4>();
            BindPlant(shader, glmodel);
            shader.loadUniformVector3f("modelSize", glmodel.Size);
            shader.loadUniformFloat("focusOpens", glmodel.OpensForFocus ? 1f : 0f);

            GL.BindBuffer(BufferTarget.ArrayBuffer, _instanceVBO);
            Span<Matrix4> matrixSpan = CollectionsMarshal.AsSpan(command.matrices);

            // Pass the first element by reference. OpenTK will read 'sizeInBytes' starting from that memory address.
            GL.BufferData(BufferTarget.ArrayBuffer, sizeInBytes, ref matrixSpan[0], BufferUsageHint.DynamicDraw);

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

            GL.DrawElementsInstanced(
                PrimitiveType.Triangles,
                glmodel.getVertexCount(),
                DrawElementsType.UnsignedInt,
                IntPtr.Zero,
                instanceCount);
        }

        internal override void PerformShadowCommand(ModelRenderCommand command, Shadow shadow, RenderEngine renderEngine)
        {
            if (shadow.isCubeMap && shadow.cubemapFaceIndex >= 0)
            {
                shadow.shadowFrameBuffer.bindFace(TextureTarget.TextureCubeMapPositiveX + shadow.cubemapFaceIndex);
            }
            else
            {
                shadow.shadowFrameBuffer.bind();
            }

            GL.PolygonOffset(shadow.polygonOffsetModel, shadow.polygonOffsetModel * 10.1f);

            glModel glmodel = command.model;
            int instanceCount = command.matrices.Count;
            int sizeInBytes = instanceCount * Marshal.SizeOf<Matrix4>();

            _instancedModelShadowShader.loadUniformMatrix4f("viewpPojectionMatrix", shadow.lightViewMatrix * shadow.shadowProjectionMatrix);
            BindPlant(_instancedModelShadowShader, glmodel);

            GL.BindBuffer(BufferTarget.ArrayBuffer, _instanceVBO);
            Span<Matrix4> matrixSpan = CollectionsMarshal.AsSpan(command.matrices);
            GL.BufferData(BufferTarget.ArrayBuffer, sizeInBytes, ref matrixSpan[0], BufferUsageHint.DynamicDraw);

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

            Engine.PerformanceMonitor.Count("Model shadow draws", 1);
            Engine.PerformanceMonitor.Count("Model shadow instances", instanceCount);
            Engine.PerformanceMonitor.Count("Model shadow triangles", (long)instanceCount * glmodel.getVertexCount() / 3);
            GL.DrawElementsInstanced(
                PrimitiveType.Triangles,
                glmodel.getVertexCount(),
                DrawElementsType.UnsignedInt,
                IntPtr.Zero,
                instanceCount);
        }
    }
}
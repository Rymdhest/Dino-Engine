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
        private readonly List<Matrix4> _shellInstances = new();

        // Every command's matrices, uploaded once a frame for all its draws, which each start at their own instance in them (DrawElementsInstancedBaseInstance): a command
        // used to be uploaded again for every draw of it, the depth pass, the shaded pass and every shadow, which was 2.8 ms of the Haven's CPU on a Ryzen 5 4600H.
        private int _geometryInstances, _shadowInstances;
        private readonly List<Matrix4> _gathered = new();
        private readonly List<int> _geometryBases = new(), _shadowBases = new();
        private int _nextGeometry, _nextShadow;
        private Shadow? _boundShadow;
        private readonly Func<int, List<Matrix4>> _geometryMatrices, _shadowMatrices;     // Made once, as a lambda made each frame is garbage.

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
            PosGeometry.WaterRenderer.LinkShoreSamplers(_instancedModelShader);
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
            _geometryMatrices = i => geometryCommands[i].matrices;
            _shadowMatrices = i => shadowCommands[i].Item1.matrices;
            _geometryInstances = GL.GenBuffer();
            _shadowInstances = GL.GenBuffer();
        }

        // Puts every command's matrices into one buffer, one after another, and says where each starts.
        private void Gather(int count, Func<int, List<Matrix4>> matricesOf, int buffer, List<int> bases)
        {
            _gathered.Clear();
            bases.Clear();
            for (int i = 0; i < count; i++)
            {
                bases.Add(_gathered.Count);
                _gathered.AddRange(matricesOf(i));
            }
            GL.BindBuffer(BufferTarget.ArrayBuffer, buffer);
            Span<Matrix4> all = CollectionsMarshal.AsSpan(_gathered);
            if (all.Length > 0) GL.BufferData(BufferTarget.ArrayBuffer, all.Length * Marshal.SizeOf<Matrix4>(), ref all[0], BufferUsageHint.StreamDraw);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        }

        // The matrices' attributes on a model's VAO, from the start of one of the gathered buffers: each draw picks its own run of them by its base instance.
        private static void PointInstances(int buffer)
        {
            GL.BindBuffer(BufferTarget.ArrayBuffer, buffer);
            for (int i = 0; i < 4; i++)
            {
                int attribLocation = 6 + i;
                GL.EnableVertexAttribArray(attribLocation);
                GL.VertexAttribPointer(attribLocation, 4, VertexAttribPointerType.Float, false, sizeof(float) * 16, i * Vector4.SizeInBytes);
                GL.VertexAttribDivisor(attribLocation, 1);
            }
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        }

        public override void CleanUp()
        {
            _instancedModelShader.cleanUp();
            _instancedModelDepthShader.cleanUp();
            _instancedModelShadowShader.cleanUp();
            GL.DeleteBuffer(_instanceVBO);
            GL.DeleteBuffer(_geometryInstances);
            GL.DeleteBuffer(_shadowInstances);
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
            Gather(geometryCommands.Count, _geometryMatrices, _geometryInstances, _geometryBases);

            // Depth alone, with the same cuts.
            GL.DepthFunc(DepthFunction.Less);
            GL.DepthMask(true);
            GL.ColorMask(false, false, false, false);
            _instancedModelDepthShader.bind();
            _instancedModelDepthShader.loadUniformInt("numberOfMaterials", renderEngine.textureGenerator.loadedMaterialTextures);
            for (int i = 0; i < geometryCommands.Count; i++) Draw(_instancedModelDepthShader, geometryCommands[i], _geometryBases[i]);
            GL.ColorMask(true, true, true, true);
            _nextGeometry = 0;

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
            Gather(shadowCommands.Count, _shadowMatrices, _shadowInstances, _shadowBases);
            _nextShadow = 0;
            _boundShadow = null;
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
            // The same commands in the same order as the depth pass, so the next base is this one's.
            Draw(_instancedModelShader, command, _geometryBases[_nextGeometry++]);
        }

        private void Draw(ShaderProgram shader, ModelRenderCommand command, int baseInstance)
        {
            glModel glmodel = command.model;
            int instanceCount = command.matrices.Count;
            BindPlant(shader, glmodel);
            shader.loadUniformVector3f("modelSize", glmodel.Size);
            shader.loadUniformFloat("focusOpens", glmodel.OpensForFocus ? 1f : 0f);

            GL.BindVertexArray(glmodel.getVAOID());
            GL.EnableVertexAttribArray(0);
            GL.EnableVertexAttribArray(1);
            GL.EnableVertexAttribArray(2);
            GL.EnableVertexAttribArray(3);
            GL.EnableVertexAttribArray(4);
            GL.EnableVertexAttribArray(5);

            PointInstances(_geometryInstances);

            GL.DrawElementsInstancedBaseInstance(
                PrimitiveType.Triangles,
                glmodel.getVertexCount(),
                DrawElementsType.UnsignedInt,
                IntPtr.Zero,
                instanceCount,
                baseInstance);
        }

        internal override bool WantsShells(RenderEngine renderEngine)
        {
            foreach (ModelRenderCommand command in geometryCommands)
            {
                if (renderEngine.Shells.Draws(command.model)) return true;
            }
            return false;
        }

        // Moss on stone, on the rock's skin and on bark (SHELLS.md): every layer of every instance near enough to show any, in one call a model, each instance's
        // matrix read once for all its layers. The plants bend as their own pass bent them, from the same textures.
        internal override void ShellPass(RenderEngine renderEngine)
        {
            ShellRenderer shells = renderEngine.Shells;
            ShaderProgram program = shells.Instanced;
            shells.Begin(program, renderEngine);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaAlbedoTextureArray);
            BindWindTextures(renderEngine);

            Vector3 camera = renderEngine.context.viewPos;
            float focusDistance = (renderEngine.context.focusPoint.Xyz - camera).Length;
            foreach (ModelRenderCommand command in geometryCommands)
            {
                glModel model = command.model;
                if (!shells.Draws(model)) continue;
                Rendering.Shells.ShellStyle style = Rendering.Shells.ShellStyles.Of(model.Shell)!;

                // Only what's near enough for what grows on it to stand up at all.
                _shellInstances.Clear();
                float nearest = float.MaxValue;
                foreach (Matrix4 matrix in command.matrices)
                {
                    model.WorldBounds(matrix, out Vector3 centre, out float radius);
                    float distance = (centre - camera).Length - radius;
                    if (distance >= style.FadeTo) continue;
                    _shellInstances.Add(matrix);
                    nearest = MathF.Min(nearest, distance);
                }
                if (_shellInstances.Count == 0) continue;

                // A big model's bounds reach nearly to the camera, though little of it is: nothing on screen is much nearer than half the way to the hero.
                int layers = shells.LayersAt(style, MathF.Max(nearest, focusDistance * 0.5f), renderEngine);
                shells.Use(program, model.Shell, layers);
                BindPlant(program, model);
                program.loadUniformVector3f("modelSize", model.Size);
                program.loadUniformFloat("focusOpens", model.OpensForFocus ? 1f : 0f);

                GL.BindBuffer(BufferTarget.ArrayBuffer, _instanceVBO);
                Span<Matrix4> matrices = CollectionsMarshal.AsSpan(_shellInstances);
                GL.BufferData(BufferTarget.ArrayBuffer, matrices.Length * Marshal.SizeOf<Matrix4>(), ref matrices[0], BufferUsageHint.StreamDraw);

                GL.BindVertexArray(model.getVAOID());
                GL.EnableVertexAttribArray(0);
                GL.EnableVertexAttribArray(1);
                GL.EnableVertexAttribArray(2);
                GL.EnableVertexAttribArray(5);
                for (int i = 0; i < 4; i++)
                {
                    int attribute = 6 + i;
                    GL.EnableVertexAttribArray(attribute);
                    GL.VertexAttribPointer(attribute, 4, VertexAttribPointerType.Float, false, sizeof(float) * 16, i * Vector4.SizeInBytes);
                    // Each instance's matrix holds for all of its layers.
                    GL.VertexAttribDivisor(attribute, layers);
                }
                GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

                GL.DrawElementsInstanced(PrimitiveType.Triangles, model.ShellIndexCount, DrawElementsType.UnsignedInt, IntPtr.Zero, _shellInstances.Count * layers);
                ShellRenderer.Count(model.Shell, 1, (long)_shellInstances.Count * layers, (long)_shellInstances.Count * layers * model.ShellIndexCount / 3);

                for (int i = 0; i < 4; i++) GL.VertexAttribDivisor(6 + i, 1);
            }

            for (int i = 0; i < 10; i++) GL.DisableVertexAttribArray(i);
            GL.BindVertexArray(0);
        }

        internal override void PerformShadowCommand(ModelRenderCommand command, Shadow shadow, RenderEngine renderEngine)
        {
            int baseInstance = _shadowBases[_nextShadow++];
            // The map the draw before drew into is still bound, with its offset and matrix: commands for one shadow come together.
            if (!ReferenceEquals(shadow, _boundShadow))
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
                _instancedModelShadowShader.loadUniformMatrix4f("viewpPojectionMatrix", shadow.lightViewMatrix * shadow.shadowProjectionMatrix);
                _boundShadow = shadow;
            }

            glModel glmodel = command.model;
            int instanceCount = command.matrices.Count;
            BindPlant(_instancedModelShadowShader, glmodel);

            GL.BindVertexArray(glmodel.getVAOID());
            GL.EnableVertexAttribArray(0);
            GL.EnableVertexAttribArray(4);
            GL.EnableVertexAttribArray(5);

            PointInstances(_shadowInstances);

            Engine.PerformanceMonitor.Count("Model shadow draws", 1);
            Engine.PerformanceMonitor.Count("Model shadow instances", instanceCount);
            Engine.PerformanceMonitor.Count("Model shadow triangles", (long)instanceCount * glmodel.getVertexCount() / 3);
            GL.DrawElementsInstancedBaseInstance(
                PrimitiveType.Triangles,
                glmodel.getVertexCount(),
                DrawElementsType.UnsignedInt,
                IntPtr.Zero,
                instanceCount,
                baseInstance);
        }
    }
}
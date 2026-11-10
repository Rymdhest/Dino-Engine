using Dino_Engine.Modelling;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Rendering.Renderers.Lighting;
using Dino_Engine.Wildlife;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Dino_Engine.Rendering.Renderers.Geometry
{
    /// <summary>Everything of one shape, as far as the GPU's concerned: where its bones start in this frame's buffer, and how many there are.</summary>
    public readonly record struct WildlifeDraw(glModel Model, int Bones, int FirstBone, int FirstTint, int Instances);

    /// <summary>
    /// Draws what's alive. Each shape takes one instanced call however many there are of it, reading its bones out of one buffer filled once a frame, so a flock costs what a bird does. It's the geometry pass like anything
    /// solid, so it's lit, shadowed, wet and reflected with no more said, and nothing's written to the stencil characters use: a mark on the ground is a promise, and it goes over whatever's hopping across it.
    /// </summary>
    public sealed class WildlifeRenderer : GeometryCommandDrivenRenderer<WildlifeDraw>, IWildlifeCanvas
    {
        private const int BoneBinding = 3;
        private const int TintBinding = 4;

        private readonly ShaderProgram _shader = new ShaderProgram("Wildlife.vert", "Model.frag", new[] { "WILDLIFE" });
        private readonly ShaderProgram _shadowShader = new ShaderProgram("Wildlife_Shadow.vert", "Shadow.frag");
        private readonly int _boneBuffer;
        private readonly int _tintBuffer;
        private readonly List<WildlifeDraw> _draws = new();

        private Matrix4[] _bones = new Matrix4[4096];
        private Vector4[] _tints = new Vector4[1024];
        private int _boneCount;
        private int _tintCount;
        private int _boneCapacity;
        private int _tintCapacity;

        public WildlifeRenderer() : base("Wildlife")
        {
            _shader.bind();
            _shader.loadUniformInt("albedoMapTextureArray", 0);
            _shader.loadUniformInt("normalMapTextureArray", 1);
            _shader.loadUniformInt("materialMapTextureArray", 2);
            _shader.loadUniformInt("albedoMapModelTextureArray", 3);
            _shader.loadUniformInt("normalMapModelTextureArray", 4);
            _shader.loadUniformInt("materialMapModelTextureArray", 5);
            Simulation.WeatherRenderer.LinkSurfaceSamplers(_shader);
            _shader.unBind();

            _shadowShader.bind();
            _shadowShader.loadUniformInt("albedoMapTextureArray", 0);
            _shadowShader.loadUniformInt("albedoMapModelTextureArray", 3);
            _shadowShader.unBind();

            _boneBuffer = GL.GenBuffer();
            _tintBuffer = GL.GenBuffer();
        }

        public override void CleanUp()
        {
            _shader.cleanUp();
            _shadowShader.cleanUp();
            GL.DeleteBuffer(_boneBuffer);
            GL.DeleteBuffer(_tintBuffer);
        }

        /// <summary>Starts a frame's worth of draws.</summary>
        public void Begin()
        {
            _boneCount = 0;
            _tintCount = 0;
            _draws.Clear();
        }

        public void Draw(WildlifeShape shape, ReadOnlySpan<Matrix4> skin, ReadOnlySpan<Vector4> tints)
        {
            if (tints.Length == 0) return;
            glModel model = shape.Drawn as glModel ?? Load(shape);

            if (_boneCount + skin.Length > _bones.Length) Array.Resize(ref _bones, Math.Max(_bones.Length * 2, _boneCount + skin.Length));
            if (_tintCount + tints.Length > _tints.Length) Array.Resize(ref _tints, Math.Max(_tints.Length * 2, _tintCount + tints.Length));
            skin.CopyTo(_bones.AsSpan(_boneCount));
            tints.CopyTo(_tints.AsSpan(_tintCount));

            var draw = new WildlifeDraw(model, shape.Bones, _boneCount, _tintCount, tints.Length);
            _boneCount += skin.Length;
            _tintCount += tints.Length;
            _draws.Add(draw);
            SubmitGeometryCommand(draw);
        }

        private static glModel Load(WildlifeShape shape)
        {
            glModel model = glLoader.loadToVAO(shape.Build());
            shape.Drawn = model;
            return model;
        }

        /// <summary>Everything drawn this frame goes up to the GPU in one go.</summary>
        public void Finish()
        {
            if (_tintCount == 0) return;
            Upload(_boneBuffer, _bones, _boneCount, ref _boneCapacity);
            Upload(_tintBuffer, _tints, _tintCount, ref _tintCapacity);
        }

        // Asked for afresh each frame, so the driver can hand over a new block and never wait on what's still being read.
        private static void Upload<T>(int buffer, T[] data, int count, ref int capacity) where T : struct
        {
            int size = System.Runtime.InteropServices.Marshal.SizeOf<T>();
            capacity = Math.Max(capacity, count);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, buffer);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, capacity * size, IntPtr.Zero, BufferUsageHint.StreamDraw);
            GL.BufferSubData(BufferTarget.ShaderStorageBuffer, IntPtr.Zero, count * size, data);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
        }

        /// <summary>Casts everything drawn this frame into a shadow.</summary>
        public void SubmitShadows(Shadow shadow)
        {
            for (int i = 0; i < _draws.Count; i++) SubmitShadowCommand(_draws[i], shadow);
        }

        private void BindBuffers()
        {
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, BoneBinding, _boneBuffer);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, TintBinding, _tintBuffer);
        }

        internal override void PrepareGeometry(RenderEngine renderEngine)
        {
            if (geometryCommands.Count == 0) return;

            GL.Enable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);
            GL.Disable(EnableCap.Blend);
            _shader.bind();
            _shader.loadUniformFloat("reliefScale", renderEngine.ReliefScale);
            _shader.loadUniformInt("numberOfMaterials", renderEngine.textureGenerator.loadedMaterialTextures);

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
            BindBuffers();
        }

        internal override void FinishGeometry(RenderEngine renderEngine) => Unbind();

        internal override void PrepareShadow(RenderEngine renderEngine)
        {
            if (shadowCommands.Count == 0) return;

            GL.DepthMask(true);
            GL.Enable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);
            GL.Disable(EnableCap.Blend);
            GL.Enable(EnableCap.PolygonOffsetFill);
            _shadowShader.bind();
            _shadowShader.loadUniformInt("numberOfMaterials", renderEngine.textureGenerator.loadedMaterialTextures);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaAlbedoTextureArray);
            GL.ActiveTexture(TextureUnit.Texture3);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaAlbedoModelTextureArray);
            BindBuffers();
        }

        internal override void FinishShadow(RenderEngine renderEngine)
        {
            GL.Disable(EnableCap.PolygonOffsetFill);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            Unbind();
        }

        private static void Unbind()
        {
            for (int i = 0; i < 8; i++) GL.DisableVertexAttribArray(i);
            GL.BindVertexArray(0);
        }

        private static void BindModel(glModel model, bool shading)
        {
            GL.BindVertexArray(model.getVAOID());
            GL.EnableVertexAttribArray(0);
            if (shading)
            {
                GL.EnableVertexAttribArray(1);
                GL.EnableVertexAttribArray(2);
                GL.EnableVertexAttribArray(3);
            }
            GL.EnableVertexAttribArray(4);
            GL.EnableVertexAttribArray(5);
            GL.EnableVertexAttribArray(6);
            GL.EnableVertexAttribArray(7);
        }

        internal override void PerformGeometryCommand(WildlifeDraw command, RenderEngine renderEngine)
        {
            BindModel(command.Model, true);
            _shader.loadUniformInt("firstBone", command.FirstBone);
            _shader.loadUniformInt("bonesEach", command.Bones);
            _shader.loadUniformInt("firstTint", command.FirstTint);

            Core.Engine.PerformanceMonitor.Count("Model draws", 1);
            Core.Engine.PerformanceMonitor.Count("Model instances", command.Instances);
            Core.Engine.PerformanceMonitor.Count("Model triangles", (long)command.Instances * command.Model.getVertexCount() / 3);
            GL.DrawElementsInstanced(PrimitiveType.Triangles, command.Model.getVertexCount(), DrawElementsType.UnsignedInt, IntPtr.Zero, command.Instances);
        }

        internal override void PerformShadowCommand(WildlifeDraw command, Shadow shadow, RenderEngine renderEngine)
        {
            if (shadow.isCubeMap && shadow.cubemapFaceIndex >= 0) shadow.shadowFrameBuffer.bindFace(TextureTarget.TextureCubeMapPositiveX + shadow.cubemapFaceIndex);
            else shadow.shadowFrameBuffer.bind();

            GL.PolygonOffset(shadow.polygonOffsetModel, shadow.polygonOffsetModel * 10.1f);
            BindModel(command.Model, false);
            _shadowShader.loadUniformMatrix4f("viewProjection", shadow.lightViewMatrix * shadow.shadowProjectionMatrix);
            _shadowShader.loadUniformInt("firstBone", command.FirstBone);
            _shadowShader.loadUniformInt("bonesEach", command.Bones);

            Core.Engine.PerformanceMonitor.Count("Model shadow draws", 1);
            Core.Engine.PerformanceMonitor.Count("Model shadow instances", command.Instances);
            Core.Engine.PerformanceMonitor.Count("Model shadow triangles", (long)command.Instances * command.Model.getVertexCount() / 3);
            GL.DrawElementsInstanced(PrimitiveType.Triangles, command.Model.getVertexCount(), DrawElementsType.UnsignedInt, IntPtr.Zero, command.Instances);
        }
    }
}

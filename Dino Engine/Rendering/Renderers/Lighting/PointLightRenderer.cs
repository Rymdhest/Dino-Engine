using Dino_Engine.Core;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;
using Dino_Engine.Physics;
using Dino_Engine.Rendering.Renderers.AntiAliasing;

namespace Dino_Engine.Rendering.Renderers.Lighting
{
    public struct PointlightRenderCommand : IRenderCommand
    {
        public Vector3 colour;
        public float radius;
        public float fill;
        public Vector3 positionWorld;
        public Shadow? shadow;
        public float shadowStrength;
    }

    /// <summary>
    /// Every point light in one pass over the screen, tiled: a compute pass finds which lights can reach anything in each 16 x 16 tile (Lighting/PointLight_Cull.comp),
    /// and each pixel then reads the G-buffer once and adds up its tile's lights (Point_Light.frag), per pixel and per sample on the edge pixels (MsaaRenderer.DrawSplit).
    /// Drawing a volume for each light read the G-buffer and blended into the scene once a light, and a square full of lanterns overlaps a dozen deep. Every light's
    /// shadow map is a cube of one array (CubeShadowArray), which is what lets one pass read them all. Lights go 256 at a time (POINT_BATCH).
    /// Spotlights with no shadow, a window's light or a stained glass's, are lit in the same pass as a point light with a cone (<see cref="SubmitSpot"/>); one with a
    /// shadow keeps its own volume (SpotLightRenderer).
    /// </summary>
    public class PointLightRenderer : CommandDrivenRenderer<PointlightRenderCommand>
    {
        // Clip planes of the cube shadow map projection (PointLightShadowSystem); shaders reconstruct depth with them. The near plane is past a light's own fitting, the glass,
        // cap and frame of a lantern (half a metre to a cap's corner), so a lantern that's part of something larger, a hall, an arch, a pier, doesn't shut its light in
        // and leave only a wedge lit past its cap. A light is never put closer than this to anything that should shadow it.
        public const float ShadowNearPlane = 0.65f;
        public const float ShadowFarPlane = 30f;
        // Width of the shadow filter per metre from the light.
        private const float ShadowSoftness = 0.012f;

        // Lighting/PointLightTiles.glsl's POINT_TILE, POINT_BATCH and its bindings, past everything global illumination binds.
        private const int Tile = 16;
        private const int Batch = 256;
        private const int Words = Batch / 32;
        private const int LightFloats = 20;
        private const int LightsBinding = 30;
        private const int TilesBinding = 31;
        private const int ShadowUnit = 4;

        // Per pixel, and per sample for the edge pixels (MsaaRenderer.DrawSplit).
        private readonly ShaderProgram _shader = new ShaderProgram("Simple.vert", "Point_Light.frag");
        private readonly ShaderProgram _sampleShader = new ShaderProgram("Simple.vert", "Point_Light.frag", new[] { "PER_SAMPLE" });
        private readonly ShaderProgram _cullShader = new ShaderProgram("Lighting/PointLight_Cull.comp");

        private readonly float[] _lightData = new float[Batch * LightFloats];
        private readonly List<PointlightRenderCommand> _visible = new();
        private readonly List<SpotlightRenderCommand> _spots = new();
        private readonly List<SpotlightRenderCommand> _visibleSpots = new();
        private readonly int _lightBuffer;
        private int _tileBuffer;
        private Vector2i _tiles;
        private Frustum _view;

        public PointLightRenderer() : base("Point Light")
        {
            foreach (ShaderProgram shader in new[] { _shader, _sampleShader })
            {
                shader.bind();
                shader.loadUniformInt("gAlbedo", 0);
                shader.loadUniformInt("gNormal", 1);
                shader.loadUniformInt("gMaterials", 2);
                shader.loadUniformInt("gDepth", 3);
                MsaaRenderer.LinkScreenTerms(shader);
                shader.loadUniformInt("shadowMaps", ShadowUnit);
                shader.loadUniformFloat("shadowSoftness", ShadowSoftness);
                shader.loadUniformFloat("shadowNearPlane", ShadowNearPlane);
                shader.loadUniformFloat("shadowFarPlane", ShadowFarPlane);
                shader.unBind();
            }
            _cullShader.bind();
            _cullShader.loadUniformInt("gDepth", 3);
            _cullShader.unBind();

            _lightBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _lightBuffer);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, _lightData.Length * sizeof(float), IntPtr.Zero, BufferUsageHint.StreamDraw);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
        }

        private void EnsureTiles(Vector2i size)
        {
            Vector2i tiles = new Vector2i((size.X + Tile - 1) / Tile, (size.Y + Tile - 1) / Tile);
            if (tiles == _tiles && _tileBuffer != 0) return;
            if (_tileBuffer != 0) GL.DeleteBuffer(_tileBuffer);
            _tiles = tiles;
            _tileBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _tileBuffer);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, Math.Max(1, tiles.X * tiles.Y) * Words * sizeof(uint), IntPtr.Zero, BufferUsageHint.DynamicCopy);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
        }

        internal override void Prepare(RenderEngine renderEngine)
        {
            _view = new Frustum(renderEngine.context.viewMatrix * renderEngine.context.projectionMatrix);
        }

        /// <summary>A spotlight with no shadow, to be lit with the point lights.</summary>
        public void SubmitSpot(SpotlightRenderCommand command) => _spots.Add(command);

        public override void PerformCommand(PointlightRenderCommand command, RenderEngine renderEngine)
        {
            Vector3 reach = new Vector3(command.radius * Engine.RenderEngine.Lighting.LightReach);
            if (_view.IntersectsAABB(new AABB(command.positionWorld - reach, command.positionWorld + reach)) == IntersectionResult.Outside) return;
            _visible.Add(command);
        }

        internal override void Render(RenderEngine renderEngine)
        {
            base.Render(renderEngine);
            foreach (SpotlightRenderCommand spot in _spots)
            {
                Vector3 reach = new Vector3(spot.radius * renderEngine.Lighting.LightReach);
                if (_view.IntersectsAABB(new AABB(spot.positionWorld - reach, spot.positionWorld + reach)) != IntersectionResult.Outside) _visibleSpots.Add(spot);
            }
            _spots.Clear();
            int total = _visible.Count + _visibleSpots.Count;
            if (total == 0) return;

            Vector2i size = renderEngine.Msaa.GeometryBuffer.getResolution();
            EnsureTiles(size);
            renderEngine.Msaa.BindGeometrySamples(renderEngine.AmbientOcclusionTexture);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, LightsBinding, _lightBuffer);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, TilesBinding, _tileBuffer);

            for (int start = 0; start < total; start += Batch)
            {
                int count = Math.Min(Batch, total - start);
                CubeShadowArray? shadowMaps = Upload(start, count, renderEngine);

                // Which lights each tile has. The last batch's draw has to have read the tiles before they're written again.
                GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);
                _cullShader.bind();
                _cullShader.loadUniformInt("lightCount", count);
                _cullShader.loadUniformInt("tilesAcross", _tiles.X);
                _cullShader.loadUniformInt("samples", Math.Max(1, renderEngine.Msaa.Samples));
                GL.DispatchCompute(_tiles.X, _tiles.Y, 1);
                GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);

                GL.ActiveTexture(TextureUnit.Texture0 + ShadowUnit);
                GL.BindTexture(TextureTarget.TextureCubeMapArray, shadowMaps?.Texture ?? 0);
                GL.ActiveTexture(TextureUnit.Texture0);

                GL.Disable(EnableCap.DepthTest);
                GL.DepthMask(false);
                GL.Disable(EnableCap.CullFace);
                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
                GL.BlendEquation(BlendEquationMode.FuncAdd);
                // Only where this batch's lights can reach: a pixel with none in its tile still reads the G-buffer, and a few lamps rarely cover a tenth of the screen.
                Box2i reach = ScreenReach(start, count, size, renderEngine);
                if (reach.Size.X <= 0 || reach.Size.Y <= 0) continue;
                GL.Enable(EnableCap.ScissorTest);
                GL.Scissor(reach.Min.X, reach.Min.Y, reach.Size.X, reach.Size.Y);
                renderEngine.Msaa.DrawSplit(_shader, _sampleShader, shader =>
                {
                    shader.loadUniformInt("tilesAcross", _tiles.X);
                    renderEngine.ScreenQuadRenderer.DrawQuad();
                });
                GL.Disable(EnableCap.ScissorTest);
            }
            _visible.Clear();
            _visibleSpots.Clear();
        }

        // The pixels a batch's lights can reach: the corners of each one's box round its radius, projected, and all of the screen if any of them is behind the eye.
        private Box2i ScreenReach(int start, int count, Vector2i size, RenderEngine renderEngine)
        {
            Matrix4 viewProjection = renderEngine.context.viewMatrix * renderEngine.context.projectionMatrix;
            Vector2 least = new Vector2(float.MaxValue), most = new Vector2(float.MinValue);
            for (int i = start; i < start + count; i++)
            {
                (Vector3 centre, float radius) = i < _visible.Count
                    ? (_visible[i].positionWorld, _visible[i].radius * renderEngine.Lighting.LightReach)
                    : (_visibleSpots[i - _visible.Count].positionWorld, _visibleSpots[i - _visible.Count].radius * renderEngine.Lighting.LightReach);
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 offset = new Vector3((corner & 1) != 0 ? radius : -radius, (corner & 2) != 0 ? radius : -radius, (corner & 4) != 0 ? radius : -radius);
                    Vector4 clip = new Vector4(centre + offset, 1f) * viewProjection;
                    if (clip.W <= 1e-3f) return new Box2i(Vector2i.Zero, size);
                    Vector2 ndc = clip.Xy / clip.W;
                    least = Vector2.ComponentMin(least, ndc);
                    most = Vector2.ComponentMax(most, ndc);
                }
            }
            Vector2i min = new Vector2i((int)MathF.Floor((least.X * 0.5f + 0.5f) * size.X) - 1, (int)MathF.Floor((least.Y * 0.5f + 0.5f) * size.Y) - 1);
            Vector2i max = new Vector2i((int)MathF.Ceiling((most.X * 0.5f + 0.5f) * size.X) + 1, (int)MathF.Ceiling((most.Y * 0.5f + 0.5f) * size.Y) + 1);
            return new Box2i(Vector2i.Clamp(min, Vector2i.Zero, size), Vector2i.Clamp(max, Vector2i.Zero, size));
        }

        // Lays out a batch of lights as Lighting/PointLightTiles.glsl's PointLight reads them. Returns the array their shadow maps are in, if any has one.
        private CubeShadowArray? Upload(int start, int count, RenderEngine renderEngine)
        {
            Matrix4 view = renderEngine.context.viewMatrix;
            HallShaderData halls = renderEngine.context.halls;
            CubeShadowArray? shadowMaps = null;
            for (int i = 0; i < count; i++)
            {
                if (start + i >= _visible.Count)
                {
                    UploadSpot(i, _visibleSpots[start + i - _visible.Count], renderEngine);
                    continue;
                }
                PointlightRenderCommand command = _visible[start + i];
                Vector4 positionView = new Vector4(command.positionWorld, 1f) * view;
                // The fill stands in for what a light bounces off everything around it, which global illumination does for real while it's in.
                float fill = command.fill * renderEngine._globalIllumination.FillKept(command.positionWorld, command.radius);

                float layer = -1f;
                if (command.shadow != null && command.shadowStrength > 0f)
                {
                    FrameBuffer map = command.shadow.Value.shadowFrameBuffer;
                    if (map.CubeArray != null && (shadowMaps == null || shadowMaps == map.CubeArray))
                    {
                        shadowMaps = map.CubeArray;
                        layer = map.CubeIndex;
                    }
                }

                Engine.PerformanceMonitor.Count("Point lights drawn", 1);
                if (command.shadow != null) Engine.PerformanceMonitor.Count("Shadowed point lights drawn", 1);

                int o = i * LightFloats;
                _lightData[o + 0] = positionView.X;
                _lightData[o + 1] = positionView.Y;
                _lightData[o + 2] = positionView.Z;
                _lightData[o + 3] = command.radius * renderEngine.Lighting.LightReach;
                _lightData[o + 4] = command.colour.X;
                _lightData[o + 5] = command.colour.Y;
                _lightData[o + 6] = command.colour.Z;
                _lightData[o + 7] = fill;
                _lightData[o + 8] = command.positionWorld.X;
                _lightData[o + 9] = command.positionWorld.Y;
                _lightData[o + 10] = command.positionWorld.Z;
                _lightData[o + 11] = layer;
                _lightData[o + 12] = Math.Clamp(command.shadowStrength, 0f, 1f);
                _lightData[o + 13] = halls.RoomsOf(command.positionWorld);
                _lightData[o + 14] = 0f;
                _lightData[o + 15] = 0f;
                _lightData[o + 16] = 0f;
                _lightData[o + 17] = 0f;
                _lightData[o + 18] = 0f;
                _lightData[o + 19] = NoCone;
            }

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _lightBuffer);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, _lightData.Length * sizeof(float), IntPtr.Zero, BufferUsageHint.StreamDraw);
            GL.BufferSubData(BufferTarget.ShaderStorageBuffer, IntPtr.Zero, count * LightFloats * sizeof(float), _lightData);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
            return shadowMaps;
        }

        // What a light that isn't a spotlight has for its cone (Lighting/PointLightTiles.glsl).
        private const float NoCone = -2f;

        // A spotlight as Lighting/PointLightTiles.glsl's PointLight reads it, as SpotLightRenderer would light it, but for a shadow, which it hasn't.
        private void UploadSpot(int i, SpotlightRenderCommand command, RenderEngine renderEngine)
        {
            Vector4 positionView = new Vector4(command.positionWorld, 1f) * renderEngine.context.viewMatrix;
            // Spot_Light.frag's normalize(-lightDirectionViewSpace), the way from what it lights back towards it.
            Vector3 directionView = (new Vector4(SpotLightRenderer.WorldDirection(command), 1f) * Matrix4.Transpose(renderEngine.context.invViewMatrix)).Xyz;
            Vector3 back = directionView.LengthSquared > 0f ? -directionView.Normalized() : Vector3.UnitY;
            float fill = command.fill * renderEngine._globalIllumination.FillKept(command.positionWorld, command.radius);

            int o = i * LightFloats;
            _lightData[o + 0] = positionView.X;
            _lightData[o + 1] = positionView.Y;
            _lightData[o + 2] = positionView.Z;
            _lightData[o + 3] = command.radius * renderEngine.Lighting.LightReach;
            _lightData[o + 4] = command.colour.X;
            _lightData[o + 5] = command.colour.Y;
            _lightData[o + 6] = command.colour.Z;
            _lightData[o + 7] = fill;
            _lightData[o + 8] = command.positionWorld.X;
            _lightData[o + 9] = command.positionWorld.Y;
            _lightData[o + 10] = command.positionWorld.Z;
            _lightData[o + 11] = -1f;
            _lightData[o + 12] = 0f;
            _lightData[o + 13] = renderEngine.context.halls.RoomsOf(command.positionWorld);
            _lightData[o + 14] = command.softness;
            _lightData[o + 15] = 0f;
            _lightData[o + 16] = back.X;
            _lightData[o + 17] = back.Y;
            _lightData[o + 18] = back.Z;
            _lightData[o + 19] = command.cutoffCosine;
        }

        // Leaves the state as the volumes it replaced did, adding with depth writes off, for whatever follows.
        internal override void Finish(RenderEngine renderEngine)
        {
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
            GL.BlendEquation(BlendEquationMode.FuncAdd);
            GL.DepthMask(false);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Less);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
        }

        public override void CleanUp()
        {
            _shader.cleanUp();
            _sampleShader.cleanUp();
            _cullShader.cleanUp();
            GL.DeleteBuffer(_lightBuffer);
            if (_tileBuffer != 0) GL.DeleteBuffer(_tileBuffer);
        }
    }
}

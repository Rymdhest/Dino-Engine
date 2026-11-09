using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System.Runtime.InteropServices;

namespace Dino_Engine.Rendering.Renderers.Lighting.GlobalIllumination
{
    // The voxels: four nested windows of the world around the camera, built from the world's data, never from what happens to be drawn,
    // so that what's behind the camera, under a roof or off the screen is as solid as what's in view.
    public partial class GlobalIlluminationRenderer
    {
        private const int ChunkGrid = 64;               // Chunks across the map of which chunk's textures are where: 512 m, past every window.
        private const int CacheBudget = 16384;           // Surface voxels a level works out a sun and sky for in a frame while GI isn't showing,
        private const int CacheWork = 2048;              // and once it is (GI_Voxel_Cache.comp).
        private const int CountPending = 120;            // Where in the counts buffer each level keeps how many voxels are listed for that.
        private static readonly Vector3 FallbackGround = new Vector3(0.16f, 0.18f, 0.11f);

        private int _voxColor;
        private int _voxInfo;
        private int _voxIndex;
        private int _voxBricks;
        private int _brickLight;
        private int _scratchColour;
        private int _scratchEmission;
        private int _pool;
        private int _brickScan;
        private int _brickSum;
        private int _pending;            // Each level's voxels still to have a sun and sky worked out, by their place in its pool.
        private int _heightMap;
        private Vector4 _heightMapping;
        private int _chunkLayers;
        private Vector2i _chunkOrigin;
        private readonly int[] _chunkLayerData = new int[ChunkGrid * ChunkGrid];
        private int _voxelizeFramebuffer;
        private int _instanceBuffer;

        private ShaderProgram _clearShader = null!;
        private ShaderProgram _terrainShader = null!;
        private ShaderProgram _groundShader = null!;
        private ShaderProgram _voxelizeShader = null!;
        private ShaderProgram _finalizeShader = null!;
        private ShaderProgram _bricksShader = null!;
        private ShaderProgram _surfaceShader = null!;
        private ShaderProgram _countShader = null!;
        private ShaderProgram _indexShader = null!;
        private ShaderProgram _cacheShader = null!;
        private ShaderProgram _lightShader = null!;
        private ShaderProgram _brickLightShader = null!;

        private readonly Vector3i[] _origin = new Vector3i[Levels];
        private readonly bool[] _built = new bool[Levels];
        private readonly int[] _cacheCursor = { PoolCapacity, PoolCapacity, PoolCapacity, PoolCapacity };   // Where each level's list has been gone through to.

        private WorldData? _world;
        private glModel[] _objectModels = Array.Empty<glModel>();
        private StructureDefinition?[] _objectLoose = Array.Empty<StructureDefinition?>();
        private float _chunkSize = WorldScene.ChunkSize;
        private readonly HashSet<Vector2i> _liveChunks = new();
        private readonly HashSet<Vector2i> _stillLive = new();
        private readonly Queue<Vector2i> _groundRefresh = new();
        private readonly Dictionary<glModel, List<Matrix4>> _batches = new();
        private readonly List<(Vector3i min, Vector3i size)> _regions = new();

        private static float VoxelSize(int level) => FinestVoxel * (1 << level);

        private bool AllLevelsBuilt()
        {
            for (int level = 0; level < Levels; level++) if (!_built[level]) return false;
            return true;
        }

        private void InvalidateLevels()
        {
            Array.Fill(_built, false);
            _liveChunks.Clear();
            _groundRefresh.Clear();
            ForgetProbes();
        }

        // ---- The world it's built from.

        /// <summary>The world being played, its objects' models and which of them are loose clutter, as WorldStreamSingleton has them.</summary>
        public void SetWorld(WorldData? world, glModel[]? objectModels, StructureDefinition?[]? objectLoose)
        {
            if (ReferenceEquals(world, _world)) return;
            _world = world;
            _objectModels = objectModels ?? Array.Empty<glModel>();
            _objectLoose = objectLoose ?? Array.Empty<StructureDefinition?>();
            InvalidateLevels();
            if (world != null && _resources) UploadHeightMap(world);
        }

        /// <summary>The live chunks of terrain, whose textures say what the ground is made of. A chunk that's just arrived has its ground coloured again.</summary>
        public void SetChunks(Dictionary<Vector2i, Entity> chunks, ECSWorld ecs, float chunkSize, Vector3 focus)
        {
            if (!_resources) return;
            _chunkSize = chunkSize;
            Vector2i centre = new Vector2i((int)MathF.Floor(focus.X / chunkSize), (int)MathF.Floor(focus.Z / chunkSize));
            Vector2i origin = centre - new Vector2i(ChunkGrid / 2);
            bool moved = origin != _chunkOrigin;
            _chunkOrigin = origin;
            Array.Fill(_chunkLayerData, -1);
            _stillLive.Clear();
            foreach ((Vector2i key, Entity entity) in chunks)
            {
                if (!ecs.HasComponent<TerrainChunkComponent>(entity)) continue;
                Vector2i at = key - origin;
                if (at.X < 0 || at.Y < 0 || at.X >= ChunkGrid || at.Y >= ChunkGrid) continue;
                _chunkLayerData[at.Y * ChunkGrid + at.X] = ecs.GetComponent<TerrainChunkComponent>(entity).normalHeightTextureArrayID;
                _stillLive.Add(key);
                if (_liveChunks.Add(key) && AllLevelsBuilt()) _groundRefresh.Enqueue(key);
            }
            _liveChunks.IntersectWith(_stillLive);

            GL.BindTexture(TextureTarget.Texture2D, _chunkLayers);
            GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, ChunkGrid, ChunkGrid, PixelFormat.RedInteger, PixelType.Int, _chunkLayerData);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            _ = moved;
        }

        private void UploadHeightMap(WorldData world)
        {
            ScalarField height = world.Height;
            if (_heightMap == 0) _heightMap = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _heightMap);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R32f, height.Width, height.Depth, 0, PixelFormat.Red, PixelType.Float, height.Data);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            // A node every CellSize from the world's corner, at the centre of its texel.
            _heightMapping = new Vector4(1f / (height.CellSize * height.Width), 1f / (height.CellSize * height.Depth), 0.5f / height.Width, 0.5f / height.Depth);
        }

        // ---- Resources.

        private void CreateVoxelResources()
        {
            Vector3i stacked = new Vector3i(Dim.X, Dim.Y * Levels, Dim.Z);
            _voxColor = CreateTexture3D(SizedInternalFormat.Rgba8, stacked, integer: false);
            _voxInfo = CreateTexture3D(SizedInternalFormat.R32ui, stacked, integer: true);
            _voxIndex = CreateTexture3D(SizedInternalFormat.R32ui, stacked, integer: true);
            _voxBricks = CreateTexture3D(SizedInternalFormat.Rg32ui, new Vector3i(Bricks.X, Bricks.Y * Levels, Bricks.Z), integer: true);
            _brickLight = CreateTexture3D(SizedInternalFormat.Rgba16f, new Vector3i(Bricks.X, Bricks.Y * Levels, Bricks.Z), integer: false);
            _scratchColour = CreateTexture3D(SizedInternalFormat.R32ui, Dim, integer: true);
            _scratchEmission = CreateTexture3D(SizedInternalFormat.R32ui, Dim, integer: true);

            _pool = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _pool);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, (IntPtr)((long)Levels * PoolCapacity * PoolStride * sizeof(uint)), IntPtr.Zero, BufferUsageHint.DynamicCopy);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
            _brickScan = CreateBuffer(Bricks.X * Bricks.Y * Bricks.Z * sizeof(uint));
            _brickSum = CreateBuffer(Bricks.X * Bricks.Y * Levels * Bricks.Z * 4 * sizeof(uint));
            _pending = CreateBuffer(Levels * PoolCapacity * sizeof(uint));

            _chunkLayers = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _chunkLayers);
            GL.TexStorage2D(TextureTarget2d.Texture2D, 1, SizedInternalFormat.R32i, ChunkGrid, ChunkGrid);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            Array.Fill(_chunkLayerData, -1);
            GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, ChunkGrid, ChunkGrid, PixelFormat.RedInteger, PixelType.Int, _chunkLayerData);
            GL.BindTexture(TextureTarget.Texture2D, 0);

            if (_world != null) UploadHeightMap(_world);
            else
            {
                _heightMap = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, _heightMap);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R32f, 1, 1, 0, PixelFormat.Red, PixelType.Float, new float[1]);
                GL.BindTexture(TextureTarget.Texture2D, 0);
            }

            // Objects are drawn into the voxels with no attachments, but samples: how many a triangle covers is how much of a voxel it fills.
            _voxelizeFramebuffer = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _voxelizeFramebuffer);
            GL.FramebufferParameter(FramebufferTarget.Framebuffer, FramebufferDefaultParameter.FramebufferDefaultWidth, VoxelizeViewport);
            GL.FramebufferParameter(FramebufferTarget.Framebuffer, FramebufferDefaultParameter.FramebufferDefaultHeight, VoxelizeViewport);
            GL.FramebufferParameter(FramebufferTarget.Framebuffer, FramebufferDefaultParameter.FramebufferDefaultSamples, VoxelizeSamples);
            // Drivers may rasterize a framebuffer with no attachments at one sample whatever it asks for, and GI_Voxelize.frag counts
            // what it's given (gl_NumSamples): with one, a triangle fills all of each voxel whose centre it covers.
            GL.GetInteger(GetPName.Samples, out int samples);
            Console.WriteLine($"Global illumination: objects are voxelized with {Math.Max(samples, 1)} sample{(samples > 1 ? "s" : "")} a voxel.");
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            _instanceBuffer = GL.GenBuffer();

            _clearShader = new ShaderProgram("GI/GI_Voxel_Clear.comp");
            _terrainShader = new ShaderProgram("GI/GI_Voxel_Terrain.comp");
            _groundShader = new ShaderProgram("GI/GI_Voxel_Ground.comp");
            _voxelizeShader = new ShaderProgram("GI/GI_Voxelize.vert", "GI/GI_Voxelize.frag", "GI/GI_Voxelize.geom");
            _finalizeShader = new ShaderProgram("GI/GI_Voxel_Finalize.comp");
            _bricksShader = new ShaderProgram("GI/GI_Voxel_Bricks.comp");
            _surfaceShader = new ShaderProgram("GI/GI_Voxel_Surface.comp");
            _countShader = new ShaderProgram("GI/GI_Voxel_Count.comp");
            _indexShader = new ShaderProgram("GI/GI_Voxel_Index.comp");
            _cacheShader = new ShaderProgram("GI/GI_Voxel_Cache.comp");
            _lightShader = new ShaderProgram("GI/GI_Voxel_Light.comp");
            _brickLightShader = new ShaderProgram("GI/GI_Voxel_BrickLight.comp");
            _scanShader = new ShaderProgram("GI/GI_Scan.comp");
            _scanSums = CreateBuffer((ScanGroup + 1) * sizeof(uint));

            _voxelizeShader.bind();
            // The normal maps are declared for the helpers it includes, and never read.
            _voxelizeShader.loadUniformInt("albedoMapTextureArray", 0);
            _voxelizeShader.loadUniformInt("materialMapTextureArray", 2);
            _voxelizeShader.loadUniformInt("albedoMapModelTextureArray", 3);
            _voxelizeShader.loadUniformInt("materialMapModelTextureArray", 5);
            _voxelizeShader.unBind();

            foreach (ShaderProgram shader in new[] { _terrainShader, _groundShader })
            {
                shader.bind();
                shader.loadUniformInt("giSurfaceWeightsA", 0);
                shader.loadUniformInt("giSurfaceWeightsB", 1);
                shader.loadUniformInt("giSurfaceWeightsC", 2);
                shader.loadUniformInt("giGrassMap", 3);
                shader.loadUniformInt("giGrassColourMap", 4);
                shader.loadUniformInt("giGroundMap", 5);
                shader.loadUniformInt("albedoMapTextureArray", 6);
                shader.loadUniformInt("giChunkLayers", UnitChunkLayers);
                LinkShared(shader, height: true);
                shader.unBind();
            }
        }

        private static int CreateTexture3D(SizedInternalFormat format, Vector3i size, bool integer)
        {
            int texture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture3D, texture);
            GL.TexStorage3D(TextureTarget3d.Texture3D, 1, format, size.X, size.Y, size.Z);
            GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureMinFilter, (int)(integer ? TextureMinFilter.Nearest : TextureMinFilter.Linear));
            GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureMagFilter, (int)(integer ? TextureMagFilter.Nearest : TextureMagFilter.Linear));
            GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureWrapR, (int)TextureWrapMode.Repeat);
            GL.BindTexture(TextureTarget.Texture3D, 0);
            return texture;
        }

        private void BindVoxelImages()
        {
            GL.BindImageTexture(0, _voxColor, 0, true, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rgba8);
            GL.BindImageTexture(1, _voxInfo, 0, true, 0, TextureAccess.ReadWrite, SizedInternalFormat.R32ui);
            GL.BindImageTexture(2, _voxIndex, 0, true, 0, TextureAccess.ReadWrite, SizedInternalFormat.R32ui);
            GL.BindImageTexture(3, _scratchColour, 0, true, 0, TextureAccess.ReadWrite, SizedInternalFormat.R32ui);
            GL.BindImageTexture(4, _scratchEmission, 0, true, 0, TextureAccess.ReadWrite, SizedInternalFormat.R32ui);
            GL.BindImageTexture(5, _voxBricks, 0, true, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rg32ui);
            GL.BindImageTexture(6, _brickLight, 0, true, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rgba16f);
        }

        // ---- Keeping the windows where the camera is.

        // Where a level's window should be, moved from where it is only once that's worth it: a hero standing on a step can't make it flip.
        private Vector3i TargetOrigin(int level, Vector3 focus, bool built)
        {
            float size = VoxelSize(level);
            Vector3 centre = focus + new Vector3(0f, 0f, LevelAhead);
            int x = (int)MathF.Floor(centre.X / size) - Dim.X / 2;
            int z = (int)MathF.Floor(centre.Z / size) - Dim.Z / 2;
            // Down, a window always reaches at least WindowBelow of its height under the hero, as the ground falls away from where
            // they stand and nothing at all can be left out of the finest voxels under their feet: the rest is above, where things stand.
            int bottom = (int)MathF.Floor((focus.Y - Dim.Y * size * WindowBelow) / size);
            Vector3i snapped = new Vector3i(Snap(x), (int)MathF.Floor(bottom / (float)ScrollStep) * ScrollStep, Snap(z));
            if (!built) return snapped;

            Vector3i current = _origin[level];
            const int Slack = ScrollStep * 3 / 4;
            if (Math.Abs(x - current.X) <= Slack) snapped.X = current.X;
            if (Math.Abs(z - current.Z) <= Slack) snapped.Z = current.Z;
            // Low enough to reach under the hero, and not so low that half the window is underground.
            if (current.Y <= bottom && current.Y >= bottom - ScrollStep - Slack) snapped.Y = current.Y;
            return snapped;
        }

        private static int Snap(int voxel) => (int)MathF.Floor(voxel / (float)ScrollStep + 0.5f) * ScrollStep;

        private void UpdateVoxels(RenderEngine engine)
        {
            Vector3 focus = Focus(engine);
            BindVoxelImages();

            // One level a frame, which has to be built in full or move, all in the frame its window moves: the coarsest unbuilt one
            // first, then whichever has strayed furthest from where it should be.
            int chosen = -1;
            bool full = false;
            Vector3i target = default;
            for (int level = Levels - 1; level >= 0 && chosen < 0; level--)
            {
                if (_built[level]) continue;
                chosen = level;
                full = true;
                target = TargetOrigin(level, focus, false);
            }
            if (chosen < 0)
            {
                int worst = 0;
                for (int level = 0; level < Levels; level++)
                {
                    Vector3i want = TargetOrigin(level, focus, true);
                    Vector3i d = want - _origin[level];
                    int stray = Math.Max(Math.Abs(d.X), Math.Max(Math.Abs(d.Y), Math.Abs(d.Z)));
                    if (stray > worst)
                    {
                        worst = stray;
                        chosen = level;
                        target = want;
                    }
                }
                if (chosen >= 0)
                {
                    Vector3i d = target - _origin[chosen];
                    full = Math.Abs(d.X) >= Dim.X || Math.Abs(d.Y) >= Dim.Y || Math.Abs(d.Z) >= Dim.Z;
                }
            }

            if (chosen >= 0)
            {
                _regions.Clear();
                Vector3i old = _origin[chosen];
                _origin[chosen] = target;
                if (full)
                {
                    _regions.Add((target, Dim));
                }
                else
                {
                    for (int axis = 0; axis < 3; axis++)
                    {
                        int d = target[axis] - old[axis];
                        if (d == 0) continue;
                        Vector3i min = target;
                        Vector3i size = Dim;
                        if (d > 0)
                        {
                            min[axis] = old[axis] + Dim[axis];
                            size[axis] = d;
                        }
                        else
                        {
                            min[axis] = target[axis];
                            size[axis] = -d;
                        }
                        _regions.Add((min, size));
                    }
                }
                UploadFrame(engine);
                if (_settings.Debug != 0) Console.WriteLine($"GI: level {chosen} {(full ? "built" : "moved")} to {target} ({(Vector3)target * VoxelSize(chosen)} m), focus {focus}, {_regions.Count} regions");
                foreach ((Vector3i min, Vector3i size) in _regions) BuildRegion(engine, chosen, min, size);
                foreach ((Vector3i min, Vector3i size) in _regions) SettleSurface(chosen, min, size);
                Compact(chosen);
                _built[chosen] = true;
            }

            RefreshGround(engine);
        }

        // ---- Building a region of a level.

        private void BuildRegion(RenderEngine engine, int level, Vector3i min, Vector3i size)
        {
            Engine.PerformanceMonitor.Count("GI regions built", 1);

            _clearShader.bind();
            SetRegion(_clearShader, level, min, size);
            Dispatch(Groups(size.X, 4), Groups(size.Y, 4), Groups(size.Z, 4));
            Barrier();

            _terrainShader.bind();
            SetRegion(_terrainShader, level, min, size);
            BindGroundTextures(engine, _terrainShader);
            Dispatch(Groups(size.X, 8), Groups(size.Z, 8));
            Barrier();

            VoxelizeObjects(engine, level, min, size);
            Barrier();

            _finalizeShader.bind();
            SetRegion(_finalizeShader, level, min, size);
            Dispatch(Groups(size.X, 4), Groups(size.Y, 4), Groups(size.Z, 4));
            Barrier();

            _bricksShader.bind();
            SetRegion(_bricksShader, level, min, size);
            Dispatch(Groups(size.X / 4, 4), Groups(size.Y / 4, 4), Groups(size.Z / 4, 4));
            Barrier();
        }

        private static void SetRegion(ShaderProgram shader, int level, Vector3i min, Vector3i size)
        {
            shader.loadUniformVector3i("regionMin", min);
            shader.loadUniformVector3i("regionSize", size);
            shader.loadUniformInt("level", level);
        }

        // Which faces are open, over the region and a voxel around it, whose neighbours have changed too.
        private void SettleSurface(int level, Vector3i min, Vector3i size)
        {
            Vector3i low = min - Vector3i.One;
            Vector3i span = size + Vector3i.One * 2;
            _surfaceShader.bind();
            LinkShared(_surfaceShader, bricks: true);
            SetRegion(_surfaceShader, level, low, span);
            _surfaceShader.loadUniformVector3i("rebuiltMin", min);
            _surfaceShader.loadUniformVector3i("rebuiltMax", min + size);
            Dispatch(Groups(span.X, 4), Groups(span.Y, 4), Groups(span.Z, 4));
            Barrier();
        }

        // Every surface voxel of the level a place in its pool.
        private void Compact(int level)
        {
            int bricks = Bricks.X * Bricks.Y * Bricks.Z;
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 15, _brickScan);
            _countShader.bind();
            _countShader.loadUniformInt("level", level);
            Dispatch(Groups(bricks, 64));
            Barrier();
            int groups = Scan(_brickScan, bricks);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 15, _brickScan);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 14, _scanSums);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 28, _pending);
            // The list of what's still to have its sun and sky worked out starts again, from every voxel still marked so.
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _counts);
            GL.ClearBufferSubData(BufferTarget.ShaderStorageBuffer, PixelInternalFormat.R32ui, (IntPtr)((CountPending + level) * sizeof(uint)),
                (IntPtr)sizeof(uint), PixelFormat.RedInteger, PixelType.UnsignedInt, IntPtr.Zero);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
            _indexShader.bind();
            _indexShader.loadUniformInt("level", level);
            _indexShader.loadUniformInt("groups", groups);
            Dispatch(Groups(bricks, 64));
            Barrier();
            _relight[level] = true;
            _cacheCursor[level] = 0;
        }

        private void BindGroundTextures(RenderEngine engine, ShaderProgram shader)
        {
            Geometry.TerrainRenderer terrain = engine._terrainRenderer;
            BindTexture(0, TextureTarget.Texture2DArray, terrain.SurfaceWeightArray(0));
            BindTexture(1, TextureTarget.Texture2DArray, terrain.SurfaceWeightArray(1));
            BindTexture(2, TextureTarget.Texture2DArray, terrain.SurfaceWeightArray(2));
            BindTexture(3, TextureTarget.Texture2DArray, terrain.GrassTextureArray);
            BindTexture(4, TextureTarget.Texture2DArray, terrain.GrassColourTextureArray);
            BindTexture(5, TextureTarget.Texture2DArray, terrain.NormalRoadHeightTextureArray);
            BindTexture(6, TextureTarget.Texture2DArray, engine.textureGenerator.megaAlbedoTextureArray);
            GL.ActiveTexture(TextureUnit.Texture0);
            shader.loadUniformVector4fArray("giSurfaceMaterials", terrain.SurfaceMaterials);
            shader.loadUniformVector2i("giChunkOrigin", _chunkOrigin);
            shader.loadUniformFloat("giChunkSize", _chunkSize);
            shader.loadUniformFloat("giMapOffset", 1f / Geometry.TerrainRenderer.CHUNK_RESOLUTION);
            shader.loadUniformVector3f("giFallbackGround", FallbackGround);
        }

        // The ground of chunks whose textures have just come, in every level, coloured again.
        private void RefreshGround(RenderEngine engine)
        {
            if (_groundRefresh.Count == 0) return;
            _groundShader.bind();
            BindGroundTextures(engine, _groundShader);
            int done = 0;
            while (_groundRefresh.Count > 0 && done < 24)
            {
                Vector2i chunk = _groundRefresh.Dequeue();
                done++;
                for (int level = 0; level < Levels; level++)
                {
                    float size = VoxelSize(level);
                    int x0 = (int)MathF.Floor(chunk.X * _chunkSize / size), z0 = (int)MathF.Floor(chunk.Y * _chunkSize / size);
                    int x1 = (int)MathF.Ceiling((chunk.X + 1) * _chunkSize / size), z1 = (int)MathF.Ceiling((chunk.Y + 1) * _chunkSize / size);
                    x0 = Math.Max(x0, _origin[level].X);
                    z0 = Math.Max(z0, _origin[level].Z);
                    x1 = Math.Min(x1, _origin[level].X + Dim.X);
                    z1 = Math.Min(z1, _origin[level].Z + Dim.Z);
                    if (x1 <= x0 || z1 <= z0) continue;
                    SetRegion(_groundShader, level, new Vector3i(x0, 0, z0), new Vector3i(x1 - x0, 1, z1 - z0));
                    Dispatch(Groups(x1 - x0, 8), Groups(z1 - z0, 8));
                }
            }
            Barrier();
        }

        // ---- Objects into voxels.

        private void VoxelizeObjects(RenderEngine engine, int level, Vector3i min, Vector3i size)
        {
            if (_world == null) return;
            float voxel = VoxelSize(level);
            Vector3 low = (Vector3)min * voxel;
            Vector3 high = (Vector3)(min + size) * voxel;
            // Too small to fill much of a voxel this big: a pebble is nothing to a two-metre voxel.
            float smallest = voxel * 0.4f;

            foreach ((glModel _, List<Matrix4> list) in _batches) list.Clear();
            WorldObjects objects = _world.Objects;
            const float Reach = 24f;    // Past its sector an object can stand out this far.
            Vector2i first = objects.SectorOf(new Vector2(low.X - Reach, low.Z - Reach));
            Vector2i last = objects.SectorOf(new Vector2(high.X + Reach, high.Z + Reach));
            int drawn = 0;
            for (int sz = first.Y; sz <= last.Y; sz++)
            {
                for (int sx = first.X; sx <= last.X; sx++)
                {
                    IReadOnlyList<WorldObject>? sector = objects.InSector(new Vector2i(sx, sz));
                    if (sector == null) continue;
                    for (int i = 0; i < sector.Count; i++)
                    {
                        WorldObject item = sector[i];
                        if (item.Asset >= _objectModels.Length) continue;
                        if (item.Asset < _objectLoose.Length && _objectLoose[item.Asset] != null) continue;
                        glModel model = _objectModels[item.Asset];
                        if (model == null) continue;
                        Matrix4 matrix = MyMath.createTransformationMatrix(item.Position, item.Rotation, item.Scale);
                        model.WorldBounds(matrix, out Vector3 centre, out float radius);
                        if (float.IsInfinity(radius) || radius < smallest) continue;
                        if (centre.X + radius < low.X || centre.X - radius > high.X) continue;
                        if (centre.Y + radius < low.Y || centre.Y - radius > high.Y) continue;
                        if (centre.Z + radius < low.Z || centre.Z - radius > high.Z) continue;
                        if (!_batches.TryGetValue(model, out List<Matrix4>? list)) _batches[model] = list = new List<Matrix4>();
                        list.Add(matrix);
                        drawn++;
                    }
                }
            }
            if (drawn == 0) return;
            Engine.PerformanceMonitor.Count("GI objects voxelized", drawn);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _voxelizeFramebuffer);
            GL.Viewport(0, 0, VoxelizeViewport, VoxelizeViewport);
            GL.Disable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);
            GL.Disable(EnableCap.Blend);
            GL.Disable(EnableCap.StencilTest);
            GL.Enable(EnableCap.Multisample);
            GL.DepthMask(false);
            GL.ColorMask(false, false, false, false);

            _voxelizeShader.bind();
            _voxelizeShader.loadUniformInt("level", level);
            _voxelizeShader.loadUniformVector3i("regionMin", min);
            _voxelizeShader.loadUniformVector3i("regionMax", min + size);
            _voxelizeShader.loadUniformInt("numberOfMaterials", engine.textureGenerator.loadedMaterialTextures);
            BindTexture(0, TextureTarget.Texture2DArray, engine.textureGenerator.megaAlbedoTextureArray);
            BindTexture(1, TextureTarget.Texture2DArray, engine.textureGenerator.megaNormalTextureArray);
            BindTexture(2, TextureTarget.Texture2DArray, engine.textureGenerator.megaMaterialTextureArray);
            BindTexture(3, TextureTarget.Texture2DArray, engine.textureGenerator.megaAlbedoModelTextureArray);
            BindTexture(4, TextureTarget.Texture2DArray, engine.textureGenerator.megaNormalModelTextureArray);
            BindTexture(5, TextureTarget.Texture2DArray, engine.textureGenerator.megaMaterialModelTextureArray);
            GL.ActiveTexture(TextureUnit.Texture0);

            foreach ((glModel model, List<Matrix4> matrices) in _batches)
            {
                if (matrices.Count == 0) continue;
                _voxelizeShader.loadUniformInt("plant", model.JointTexture != 0 ? 1 : 0);
                GL.BindBuffer(BufferTarget.ArrayBuffer, _instanceBuffer);
                Span<Matrix4> span = CollectionsMarshal.AsSpan(matrices);
                GL.BufferData(BufferTarget.ArrayBuffer, matrices.Count * Marshal.SizeOf<Matrix4>(), ref span[0], BufferUsageHint.StreamDraw);
                GL.BindVertexArray(model.getVAOID());
                GL.EnableVertexAttribArray(0);
                GL.EnableVertexAttribArray(1);
                GL.EnableVertexAttribArray(4);
                GL.EnableVertexAttribArray(5);
                for (int i = 0; i < 4; i++)
                {
                    GL.EnableVertexAttribArray(6 + i);
                    GL.VertexAttribPointer(6 + i, 4, VertexAttribPointerType.Float, false, sizeof(float) * 16, i * Vector4.SizeInBytes);
                    GL.VertexAttribDivisor(6 + i, 1);
                }
                GL.DrawElementsInstanced(PrimitiveType.Triangles, model.getVertexCount(), DrawElementsType.UnsignedInt, IntPtr.Zero, matrices.Count);
            }
            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.ColorMask(true, true, true, true);
            GL.DepthMask(true);
            GL.Enable(EnableCap.DepthTest);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            _voxelizeShader.unBind();
        }

        // ---- Lighting the voxels, every frame.

        private void LightVoxels(RenderEngine engine)
        {
            Engine.PerformanceMonitor.startGPUTask("GI Voxel Cache");
            BindVoxelImages();

            // The sky's light on a face of each direction.
            BindTexture(UnitSky, TextureTarget.Texture2D, engine._weatherRenderer.SkyMapTexture);
            GL.ActiveTexture(TextureUnit.Texture0);
            _skyShader.bind();
            _skyShader.loadUniformInt("skyMap", UnitSky);
            Dispatch(1);
            Barrier();

            // What each new surface voxel can see of the sun and sky: the next slice of what each level's last layout listed, a few
            // thousand a level once GI is showing, and before that as many as it can, as nothing is shown until it's done.
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 28, _pending);
            _cacheShader.bind();
            LinkShared(_cacheShader, bricks: true);
            int slice = _presence < 1f ? CacheBudget : CacheWork;
            for (int level = 0; level < Levels; level++)
            {
                if (_cacheCursor[level] >= PoolCapacity) continue;
                _cacheShader.loadUniformInt("level", level);
                _cacheShader.loadUniformInt("first", _cacheCursor[level]);
                _cacheShader.loadUniformInt("budget", slice);
                Dispatch(Groups(slice, 64));
                _cacheCursor[level] += slice;
            }
            Barrier();

            Engine.PerformanceMonitor.finishGPUTask("GI Voxel Cache");
            Engine.PerformanceMonitor.startGPUTask("GI Voxel Light");
            BindTexture(UnitCloud, TextureTarget.Texture2D, engine._weatherRenderer.CloudShadowTexture);
            GL.ActiveTexture(TextureUnit.Texture0);
            // The finest two levels every frame; the coarse ones, far off and lit by much the same light from one frame to the next, every
            // second and fourth. A level whose pool has just been laid out again has nothing right in it, and is lit at once.
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 24, _brickSum);
            _lightShader.bind();
            LinkShared(_lightShader, bricks: true, brickLight: true);
            _lightShader.loadUniformInt("cloudShadowMap", UnitCloud);
            for (int level = 0; level < Levels; level++)
            {
                _lit[level] = _relight[level] || (_frame & LightPeriodMask[level]) == (level & LightPeriodMask[level]);
                _relight[level] = false;
                if (!_lit[level]) continue;
                _lightShader.loadUniformInt("level", level);
                GL.DispatchComputeIndirect((IntPtr)((4 + level * 4) * sizeof(uint)));
            }
            Barrier();
            Engine.PerformanceMonitor.finishGPUTask("GI Voxel Light");

            Engine.PerformanceMonitor.startGPUTask("GI Brick Light");
            // From the sums the lighting left, for the levels it lit.
            _brickLightShader.bind();
            for (int level = 0; level < Levels; level++)
            {
                if (!_lit[level]) continue;
                _brickLightShader.loadUniformInt("level", level);
                Dispatch(Bricks.X / 4, Bricks.Y / 4, Bricks.Z / 4);
            }
            Barrier();
            Engine.PerformanceMonitor.finishGPUTask("GI Brick Light");
        }

        // Frames between the lightings of each level, less one, as a mask: 1, 1, 2 and 4.
        private static readonly int[] LightPeriodMask = { 0, 0, 1, 3 };
        private readonly bool[] _lit = new bool[Levels];
        private readonly bool[] _relight = new bool[Levels];

        private void CleanUpVoxels()
        {
            foreach (int texture in new[] { _voxColor, _voxInfo, _voxIndex, _voxBricks, _brickLight, _scratchColour, _scratchEmission, _heightMap, _chunkLayers })
            {
                if (texture != 0) GL.DeleteTexture(texture);
            }
            GL.DeleteBuffer(_pool);
            GL.DeleteBuffer(_brickScan);
            GL.DeleteBuffer(_brickSum);
            GL.DeleteBuffer(_pending);
            GL.DeleteBuffer(_scanSums);
            GL.DeleteBuffer(_instanceBuffer);
            GL.DeleteFramebuffer(_voxelizeFramebuffer);
            foreach (ShaderProgram shader in new[] { _clearShader, _terrainShader, _groundShader, _voxelizeShader, _finalizeShader, _bricksShader, _surfaceShader,
                _countShader, _indexShader, _cacheShader, _lightShader, _brickLightShader, _scanShader })
            {
                shader.cleanUp();
            }
        }
    }
}

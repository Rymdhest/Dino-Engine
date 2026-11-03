using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Procedural.Terrain;
using Dino_Engine.Physics;
using Dino_Engine.Rendering.Renderers.Geometry;
using Dino_Engine.Util;
using Dino_Engine.Util.Data_Structures.Grids;
using OpenTK.Mathematics;
using System.Collections.Generic;

namespace Dino_Engine.ECS.Systems
{
    /// <summary>
    /// Streams fixed-size terrain chunks around the camera. Unlike the old LOD quadtree, every
    /// chunk is exactly <see cref="TerrainQuadTreeSingleton.ChunkSize"/> in size, so quality and
    /// grass density are uniform everywhere (no popping). Only a square ring of
    /// <see cref="TerrainQuadTreeSingleton.Radius"/> chunks on each side of the camera exists at
    /// any time; everything else is destroyed.
    /// </summary>
    public class TerrainChunkSystem : SystemBase
    {
        // Reusable scratch buffers (avoid per-frame allocations)
        private readonly List<Vector2i> _toCreate = new();
        private readonly List<Vector2i> _toDestroy = new();
        private readonly List<TerrainScatterInstance> _scatterInstances = new();
        private readonly List<(Entity entity, AABB bounds)> _spawnedScatter = new();
        private readonly HashSet<Vector2i> _createdThisFrame = new();
        private readonly Dictionary<Vector2i, List<Entity>> _carveTargets = new();

        public TerrainChunkSystem()
            : base(new BitMask(typeof(TerrainChunkComponent), typeof(ScaleComponent), typeof(LocalToWorldMatrixComponent)))
        {
            Priority = -1;
        }

        internal override void UpdateInternal(ECSWorld world, float deltaTime)
        {
            var terrainComp = world.GetComponent<TerrainQuadTreeSingleton>(world.GetSingleton<TerrainQuadTreeSingleton>());
            TerrainGenerator generator = world.GetComponent<TerrainGeneratorSingleton>(world.GetSingleton<TerrainGeneratorSingleton>()).Generator;

            Vector3 cameraPos = world.GetComponent<LocalToWorldMatrixComponent>(world.Camera).value.ExtractTranslation();
            float chunkSize = terrainComp.ChunkSize;
            int radius = terrainComp.Radius;
            var chunks = terrainComp.Chunks;

            // The cell the camera is currently standing in. We keep a square ring of `radius`
            // chunks on each side of this cell alive, and destroy everything else.
            int camCx = (int)MathF.Floor(cameraPos.X / chunkSize);
            int camCz = (int)MathF.Floor(cameraPos.Z / chunkSize);

            // 1. Find chunks that have drifted outside the desired ring around the camera.
            _toDestroy.Clear();
            foreach (var kv in chunks)
                if (!IsInRadius(kv.Key, camCx, camCz, radius))
                    _toDestroy.Add(kv.Key);

            // 2. Find missing chunks inside the ring.
            _toCreate.Clear();
            for (int cx = camCx - radius; cx <= camCx + radius; cx++)
                for (int cz = camCz - radius; cz <= camCz + radius; cz++)
                    if (!chunks.ContainsKey(new Vector2i(cx, cz)))
                        _toCreate.Add(new Vector2i(cx, cz));

            // Where the water is: plants stay out of it and grass doesn't grow under it.
            WaterQuery? water = _toCreate.Count > 0 ? WaterQuery.Create(world) : null;

            // 3. Stream the scattered objects. They have to exist before their chunks are created, because a
            // chunk carves the grass under whatever is in the spatial grid at creation time.
            bool hasScatter = world.TryGetSingleton<TerrainScatterSingleton>(out Entity scatterEntity);
            SpatialGrid grid = world.GetComponent<RenderSpatialGridSingleton>(world.GetSingleton<RenderSpatialGridSingleton>()).Grid;
            _spawnedScatter.Clear();
            if (hasScatter)
            {
                var scatter = world.GetComponent<TerrainScatterSingleton>(scatterEntity);
                for (int i = 0; i < _toDestroy.Count; i++)
                    DespawnScatter(_toDestroy[i], scatter, world, grid);
                for (int i = 0; i < _toCreate.Count; i++)
                    SpawnScatter(_toCreate[i], chunkSize, scatter, world, generator, grid, water);

                // The grass carve reads components of the new objects, so they must be real entities now.
                world.ApplyDeferredCommands();
            }

            // 4. Destroy and create the chunks themselves.
            for (int i = 0; i < _toDestroy.Count; i++)
            {
                Vector2i cell = _toDestroy[i];
                Entity e = chunks[cell];
                world.DestroyEntity(e);
                chunks.Remove(cell);
            }

            for (int i = 0; i < _toCreate.Count; i++)
            {
                Vector2i key = _toCreate[i];
                Entity e = CreateChunkEntity(key, chunkSize, world, generator, water);
                chunks[key] = e;
            }

            // 5. New objects that reach into chunks which already existed must carve those chunks too.
            if (_spawnedScatter.Count > 0)
                CarveScatterIntoExistingChunks(chunkSize, chunks, world);

            Engine.Instance.world.ApplyDeferredCommands();
        }

        private void SpawnScatter(Vector2i cell, float chunkSize, TerrainScatterSingleton scatter, ECSWorld world, TerrainGenerator generator, SpatialGrid grid, WaterQuery? water)
        {
            TerrainScatter.GenerateChunk(generator, scatter.Layers, scatter.Seed, cell, chunkSize, scatter.BoundsMin, scatter.BoundsMax, _scatterInstances, water);
            if (_scatterInstances.Count == 0) return;

            var entities = new List<Entity>(_scatterInstances.Count);
            for (int i = 0; i < _scatterInstances.Count; i++)
            {
                TerrainScatterInstance instance = _scatterInstances[i];
                // Same matrix LocalToWorldTransformRootSystem computes, filled in now because that system has
                // already run this frame and the spatial grid needs the bounds immediately.
                Matrix4 localToWorld = MyMath.createTransformationMatrix(instance.Position, instance.Rotation, instance.Scale);

                Entity e = world.CreateEntity(
                    new PositionComponent(instance.Position),
                    new RotationComponent(instance.Rotation),
                    new ScaleComponent(instance.Scale),
                    new ModelComponent(instance.Model),
                    new ModelRenderTag(),
                    new LocalToWorldMatrixComponent { value = localToWorld }
                );

                // Inserted directly: entities spawned here are applied after RenderSpatialGridSystem has run,
                // and the spawn buffer it reads is cleared at the end of the frame.
                AABB bounds = RenderSpatialGridSystem.ComputeWorldBounds(instance.Model, localToWorld);
                grid.InsertOrUpdate(e, bounds);

                entities.Add(e);
                _spawnedScatter.Add((e, bounds));
            }
            scatter.ChunkInstances[cell] = entities;
        }

        private static void DespawnScatter(Vector2i cell, TerrainScatterSingleton scatter, ECSWorld world, SpatialGrid grid)
        {
            if (!scatter.ChunkInstances.Remove(cell, out var entities)) return;

            for (int i = 0; i < entities.Count; i++)
            {
                // Removed directly, for the same reason as the insert in SpawnScatter. Leaving a destroyed
                // entity in the grid would break the next grass carve that finds it.
                grid.Remove(entities[i]);
                world.DestroyEntity(entities[i]);
            }
        }

        private void CarveScatterIntoExistingChunks(float chunkSize, Dictionary<Vector2i, Entity> chunks, ECSWorld world)
        {
            _createdThisFrame.Clear();
            for (int i = 0; i < _toCreate.Count; i++)
                _createdThisFrame.Add(_toCreate[i]);

            _carveTargets.Clear();
            for (int i = 0; i < _spawnedScatter.Count; i++)
            {
                var (entity, bounds) = _spawnedScatter[i];
                int minCx = (int)MathF.Floor(bounds.Min.X / chunkSize);
                int maxCx = (int)MathF.Floor(bounds.Max.X / chunkSize);
                int minCz = (int)MathF.Floor(bounds.Min.Z / chunkSize);
                int maxCz = (int)MathF.Floor(bounds.Max.Z / chunkSize);

                for (int cx = minCx; cx <= maxCx; cx++)
                    for (int cz = minCz; cz <= maxCz; cz++)
                    {
                        // Chunks created this frame already carved everything in the grid.
                        Vector2i cell = new Vector2i(cx, cz);
                        if (_createdThisFrame.Contains(cell) || !chunks.ContainsKey(cell)) continue;

                        if (!_carveTargets.TryGetValue(cell, out var list))
                        {
                            list = new List<Entity>();
                            _carveTargets[cell] = list;
                        }
                        list.Add(entity);
                    }
            }

            foreach (var (cell, entities) in _carveTargets)
            {
                var chunk = world.GetComponent<TerrainChunkComponent>(chunks[cell]);
                Engine.RenderEngine._terrainRenderer.CarveGrass(
                    chunk.normalHeightTextureArrayID,
                    new Vector2(cell.X * chunkSize, cell.Y * chunkSize),
                    new Vector2(chunkSize),
                    chunk.heightGrid.Resolution,
                    entities);
            }
        }

        private static bool IsInRadius(Vector2i cell, int camCx, int camCz, int radius)
        {
            return Math.Abs(cell.X - camCx) <= radius && Math.Abs(cell.Y - camCz) <= radius;
        }

        private Entity CreateChunkEntity(Vector2i cell, float chunkSize, ECSWorld world, TerrainGenerator generator, WaterQuery? water)
        {
            Vector3 scale = new Vector3(chunkSize, 1.0f, chunkSize);
            Vector2 position = new Vector2(cell.X * chunkSize, cell.Y * chunkSize);

            var heightGrid = generator.generateChunk(position, scale.Xz, new Vector2i(TerrainRenderer.CHUNK_RESOLUTION));
            var normalgrid = generator.generateNormalGridFor(heightGrid, scale, position, out var grassGrid);
            if (water != null && !water.IsEmpty) ClearGrassUnderWater(grassGrid, heightGrid, position, chunkSize, water);

            return world.CreateEntity("Terrain Chunk",
                new TerrainChunkComponent(heightGrid, normalgrid, grassGrid, position, scale.Xz),
                new PositionComponent(new Vector3(position.X, 0.0f, position.Y)),
                new ScaleComponent(scale),
                new LocalToWorldMatrixComponent()
            );
        }

        /// <summary>
        /// No grass under water: it thins out over the first few centimetres of depth. Texels sit where generateChunk
        /// samples the heights.
        /// </summary>
        private static void ClearGrassUnderWater(FloatGrid grassGrid, FloatGrid heightGrid, Vector2 origin, float chunkSize, WaterQuery water)
        {
            Vector2i resolution = grassGrid.Resolution;
            Vector2 cell = new Vector2(chunkSize) / (resolution - new Vector2(1));
            for (int z = 0; z < resolution.Y; z++)
            {
                for (int x = 0; x < resolution.X; x++)
                {
                    if (grassGrid.Values[x, z] <= 0f) continue;
                    Vector2 xz = origin + new Vector2(x, z) * cell;
                    float depth = water.DepthAt(new Vector3(xz.X, heightGrid.Values[x, z] + TerrainRenderer.SurfaceOffset, xz.Y));
                    if (depth <= 0f) continue;
                    float t = Math.Clamp(depth / 0.05f, 0f, 1f);
                    grassGrid.Values[x, z] *= 1f - t * t * (3f - 2f * t);
                }
            }
        }

        /// <summary>
        /// Frustum-culls the currently-alive fixed-size chunks and appends their entities to
        /// <paramref name="visibleChunks"/>. All chunks are the same size, so this is a simple
        /// per-chunk AABB test (no LOD recursion). <paramref name="margin"/> widens each chunk's box
        /// on the xz plane, for things that reach out of their chunk (grass blades).
        /// </summary>
        public static void CollectVisibleChunks(TerrainQuadTreeSingleton terrainComp, Frustum frustum, List<Entity> visibleChunks, float margin = 0f)
        {
            float minHeight = -100;
            float maxHeight = 700;
            float chunkSize = terrainComp.ChunkSize;

            foreach (var kv in terrainComp.Chunks)
            {
                Vector2 origin = new Vector2(kv.Key.X * chunkSize, kv.Key.Y * chunkSize);
                Vector3 min = new Vector3(origin.X - margin, minHeight, origin.Y - margin);
                Vector3 max = new Vector3(origin.X + chunkSize + margin, maxHeight, origin.Y + chunkSize + margin);

                var result = frustum.IntersectsAABB(new Physics.AABB(min, max));
                if (result == IntersectionResult.Outside)
                    continue;

                visibleChunks.Add(kv.Value);
            }
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            throw new NotImplementedException();
        }
    }
}

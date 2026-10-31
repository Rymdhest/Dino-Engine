using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Procedural.Terrain;
using Dino_Engine.Rendering.Renderers.Geometry;
using Dino_Engine.Util;
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

            // 1. Destroy chunks that have drifted outside the desired ring around the camera.
            _toDestroy.Clear();
            foreach (var kv in chunks)
                if (!IsInRadius(kv.Key, camCx, camCz, radius))
                    _toDestroy.Add(kv.Key);

            for (int i = 0; i < _toDestroy.Count; i++)
            {
                Vector2i cell = _toDestroy[i];
                Entity e = chunks[cell];
                world.DestroyEntity(e);
                chunks.Remove(cell);
            }

            // 2. Create any missing chunks inside the ring.
            _toCreate.Clear();
            for (int cx = camCx - radius; cx <= camCx + radius; cx++)
                for (int cz = camCz - radius; cz <= camCz + radius; cz++)
                    if (!chunks.ContainsKey(new Vector2i(cx, cz)))
                        _toCreate.Add(new Vector2i(cx, cz));

            for (int i = 0; i < _toCreate.Count; i++)
            {
                Vector2i key = _toCreate[i];
                Entity e = CreateChunkEntity(key, chunkSize, world, generator);
                chunks[key] = e;
            }

            Engine.Instance.world.ApplyDeferredCommands();
        }

        private static bool IsInRadius(Vector2i cell, int camCx, int camCz, int radius)
        {
            return Math.Abs(cell.X - camCx) <= radius && Math.Abs(cell.Y - camCz) <= radius;
        }

        private Entity CreateChunkEntity(Vector2i cell, float chunkSize, ECSWorld world, TerrainGenerator generator)
        {
            Vector3 scale = new Vector3(chunkSize, 1.0f, chunkSize);
            Vector2 position = new Vector2(cell.X * chunkSize, cell.Y * chunkSize);

            var heightGrid = generator.generateChunk(position, scale.Xz, new Vector2i(TerrainRenderer.CHUNK_RESOLUTION));
            var normalgrid = generator.generateNormalGridFor(heightGrid, scale, position, out var grassGrid);

            return world.CreateEntity("Terrain Chunk",
                new TerrainChunkComponent(heightGrid, normalgrid, grassGrid, position, scale.Xz),
                new PositionComponent(new Vector3(position.X, 0.0f, position.Y)),
                new ScaleComponent(scale),
                new LocalToWorldMatrixComponent()
            );
        }

        /// <summary>
        /// Frustum-culls the currently-alive fixed-size chunks and appends their entities to
        /// <paramref name="visibleChunks"/>. All chunks are the same size, so this is a simple
        /// per-chunk AABB test (no LOD recursion).
        /// </summary>
        public static void CollectVisibleChunks(TerrainQuadTreeSingleton terrainComp, Frustum frustum, List<Entity> visibleChunks)
        {
            float minHeight = -100;
            float maxHeight = 700;
            float chunkSize = terrainComp.ChunkSize;

            foreach (var kv in terrainComp.Chunks)
            {
                Vector2 origin = new Vector2(kv.Key.X * chunkSize, kv.Key.Y * chunkSize);
                Vector3 min = new Vector3(origin.X, minHeight, origin.Y);
                Vector3 max = new Vector3(origin.X + chunkSize, maxHeight, origin.Y + chunkSize);

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

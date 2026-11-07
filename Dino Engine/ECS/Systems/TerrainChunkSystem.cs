using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Terrain;
using Dino_Engine.Physics;
using Dino_Engine.Rendering.Renderers.Geometry;
using Dino_Engine.Util;
using Dino_Engine.Util.Data_Structures.Grids;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using System.Collections.Generic;

namespace Dino_Engine.ECS.Systems
{
    /// <summary>
    /// Streams the world around the camera. Terrain comes in fixed-size chunks: every chunk is exactly
    /// <see cref="TerrainQuadTreeSingleton.ChunkSize"/> in size, so quality and grass density are uniform everywhere (no
    /// popping). Only a square ring of <see cref="TerrainQuadTreeSingleton.Radius"/> chunks on each side of the camera exists
    /// at any time; everything else is destroyed. The objects a world has placed come in larger sectors that reach further
    /// out than the chunks, and small plants are grown chunk by chunk (see <see cref="WorldStreamSingleton"/>).
    /// </summary>
    public class TerrainChunkSystem : SystemBase
    {
        // How far past the terrain ring sectors stay alive, in metres. Trees are tall: they show before the ground under them.
        private const float SectorMargin = 24f;
        private const int SectorsPerFrame = 3;
        private const int ChunksPerFrame = 3;
        private const int MostToSpreadOut = 80;

        // Reusable scratch buffers (avoid per-frame allocations)
        private readonly List<Vector2i> _toCreate = new();
        private readonly List<Vector2i> _toDestroy = new();
        private readonly List<Vector2i> _sectorsToLoad = new();
        private readonly List<Vector2i> _sectorsToDrop = new();
        private readonly List<GroundPlant> _plants = new();
        private readonly List<(Entity entity, AABB bounds)> _spawnedObjects = new();
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
            var stream = world.GetComponent<WorldStreamSingleton>(world.GetSingleton<WorldStreamSingleton>());

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

            // Crossing into a new chunk brings a whole row of them, and every one means a run of small uploads the driver has to fit in around what's being drawn.
            // Taken a few a frame, nearest first, they cost nothing anyone can feel. A jump has nothing to show yet, so it gets everything at once.
            if (_toCreate.Count > ChunksPerFrame && _toCreate.Count <= MostToSpreadOut)
            {
                _toCreate.Sort((a, b) => ((a - new Vector2i(camCx, camCz)).EuclideanLengthSquared).CompareTo((b - new Vector2i(camCx, camCz)).EuclideanLengthSquared));
                _toCreate.RemoveRange(ChunksPerFrame, _toCreate.Count - ChunksPerFrame);
            }

            // 3. Stream the objects. They have to exist before their chunks are created, because a chunk carves the
            // grass under whatever is in the spatial grid at creation time.
            SpatialGrid grid = world.GetComponent<RenderSpatialGridSingleton>(world.GetSingleton<RenderSpatialGridSingleton>()).Grid;
            _spawnedObjects.Clear();
            float sectorReach = (radius + 1) * chunkSize + SectorMargin;
            StreamSectors(cameraPos.Xz, sectorReach, stream, world, grid);

            for (int i = 0; i < _toDestroy.Count; i++)
                DespawnEntities(_toDestroy[i], stream.ChunkPlants, world, grid);
            for (int i = 0; i < _toCreate.Count; i++)
                SpawnPlants(_toCreate[i], chunkSize, stream, world, grid);

            // The grass carve reads components of the new objects, so they must be real entities now.
            if (_spawnedObjects.Count > 0) world.ApplyDeferredCommands();

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
                Entity e = CreateChunkEntity(key, chunkSize, world, generator);
                chunks[key] = e;
            }

            // 5. New objects that reach into chunks which already existed must carve those chunks too.
            if (_spawnedObjects.Count > 0)
                CarveObjectsIntoExistingChunks(chunkSize, chunks, world);

            Engine.Instance.world.ApplyDeferredCommands();
        }

        private void StreamSectors(Vector2 camera, float reach, WorldStreamSingleton stream, ECSWorld world, SpatialGrid grid)
        {
            WorldObjects objects = stream.World.Objects;
            float size = WorldObjects.SectorSize;

            _sectorsToDrop.Clear();
            float dropReach = reach + size * 1.5f;
            foreach (var kv in stream.Sectors)
                if (SectorDistance(kv.Key, camera) > dropReach)
                    _sectorsToDrop.Add(kv.Key);
            for (int i = 0; i < _sectorsToDrop.Count; i++)
                DespawnEntities(_sectorsToDrop[i], stream.Sectors, world, grid);

            _sectorsToLoad.Clear();
            int minX = (int)MathF.Floor((camera.X - reach) / size);
            int maxX = (int)MathF.Floor((camera.X + reach) / size);
            int minZ = (int)MathF.Floor((camera.Y - reach) / size);
            int maxZ = (int)MathF.Floor((camera.Y + reach) / size);
            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    var sector = new Vector2i(x, z);
                    if (!stream.Sectors.ContainsKey(sector) && SectorDistance(sector, camera) <= reach) _sectorsToLoad.Add(sector);
                }
            }

            // Nearest first, and a few per frame: walking over a sector boundary shouldn't hitch. A world just entered has
            // nothing to show yet, so it gets everything at once.
            _sectorsToLoad.Sort((a, b) => SectorDistance(a, camera).CompareTo(SectorDistance(b, camera)));
            int budget = stream.Sectors.Count == 0 ? int.MaxValue : SectorsPerFrame;
            for (int i = 0; i < _sectorsToLoad.Count && i < budget; i++)
                SpawnSector(_sectorsToLoad[i], stream, world, grid);
        }

        // Metres from a point to the nearest edge of a sector.
        private static float SectorDistance(Vector2i sector, Vector2 point)
        {
            float size = WorldObjects.SectorSize;
            Vector2 min = new Vector2(sector.X, sector.Y) * size;
            Vector2 nearest = Vector2.Clamp(point, min, min + new Vector2(size));
            return (nearest - point).Length;
        }

        private void SpawnSector(Vector2i sector, WorldStreamSingleton stream, ECSWorld world, SpatialGrid grid)
        {
            IReadOnlyList<WorldObject>? objects = stream.World.Objects.InSector(sector);
            var entities = new List<Entity>(objects?.Count ?? 0);
            if (objects != null)
            {
                for (int i = 0; i < objects.Count; i++)
                {
                    WorldObject item = objects[i];
                    entities.Add(SpawnModel(stream.ObjectModels[item.Asset], item.Position, item.Rotation, item.Scale, world, grid));
                    foreach (AssetLight light in stream.ObjectLights[item.Asset]) entities.Add(SpawnLight(light, item, world));
                }
            }
            // Empty sectors are remembered too, or they would be looked up again every frame.
            stream.Sectors[sector] = entities;
        }

        private void SpawnPlants(Vector2i cell, float chunkSize, WorldStreamSingleton stream, ECSWorld world, SpatialGrid grid)
        {
            stream.GroundCover.Generate(new Vector2(cell.X, cell.Y) * chunkSize, chunkSize, _plants);
            if (_plants.Count == 0) return;

            var entities = new List<Entity>(_plants.Count);
            for (int i = 0; i < _plants.Count; i++)
            {
                GroundPlant plant = _plants[i];
                Quaternion rotation = Quaternion.FromAxisAngle(Vector3.UnitY, plant.Yaw);
                entities.Add(SpawnModel(stream.Assets.Get(plant.Asset.Id), plant.Position, rotation, new Vector3(plant.Scale), world, grid));
            }
            stream.ChunkPlants[cell] = entities;
        }

        private Entity SpawnModel(glModel model, Vector3 position, Quaternion rotation, Vector3 scale, ECSWorld world, SpatialGrid grid)
        {
            // Same matrix LocalToWorldTransformRootSystem computes, filled in now because that system has
            // already run this frame and the spatial grid needs the bounds immediately.
            Matrix4 localToWorld = MyMath.createTransformationMatrix(position, rotation, scale);

            Entity e = world.CreateEntity(
                new PositionComponent(position),
                new RotationComponent(rotation),
                new ScaleComponent(scale),
                new ModelComponent(model),
                new ModelRenderTag(),
                new LocalToWorldMatrixComponent { value = localToWorld }
            );

            // Inserted directly: entities spawned here are applied after RenderSpatialGridSystem has run,
            // and the spawn buffer it reads is cleared at the end of the frame.
            AABB bounds = RenderSpatialGridSystem.ComputeWorldBounds(model, localToWorld);
            grid.InsertOrUpdate(e, bounds);
            _spawnedObjects.Add((e, bounds));
            return e;
        }

        private static Entity SpawnLight(AssetLight light, WorldObject owner, ECSWorld world)
        {
            Vector3 position = owner.Position + Vector3.Transform(light.Position * owner.Scale, owner.Rotation);
            var components = new List<IComponent>
            {
                new PositionComponent(position),
                new LocalToWorldMatrixComponent { value = Matrix4.CreateTranslation(position) },
                new LightRangeComponent(light.Radius, light.Fill, light.LightsFog),
                new ColorComponent(new Colour(light.Colour.X, light.Colour.Y, light.Colour.Z, light.Intensity)),
            };

            // Each light keeps its own time, so a village doesn't flicker as one.
            if (light.Flicker > 0f) components.Add(new LightFlickerComponent(light.Flicker, light.FlickerSpeed, position.X * 1.3f + position.Z * 0.7f));

            if (light.ConeAngle > 0f)
            {
                components.Add(new SpotLightComponent(1f, light.ConeAngle));
                components.Add(new DirectionNormalizedComponent(Vector3.Transform(light.Direction, owner.Rotation)));
            }
            else
            {
                components.Add(new PointLightTag());
                if (light.Shadow) components.Add(new PointLightShadowComponent());
            }
            return world.CreateEntity(components.ToArray());
        }

        private static void DespawnEntities(Vector2i key, Dictionary<Vector2i, List<Entity>> groups, ECSWorld world, SpatialGrid grid)
        {
            if (!groups.Remove(key, out var entities)) return;

            for (int i = 0; i < entities.Count; i++)
            {
                // Removed directly, for the same reason as the insert in SpawnModel. Leaving a destroyed
                // entity in the grid would break the next grass carve that finds it.
                grid.Remove(entities[i]);
                world.DestroyEntity(entities[i]);
            }
        }

        private void CarveObjectsIntoExistingChunks(float chunkSize, Dictionary<Vector2i, Entity> chunks, ECSWorld world)
        {
            _createdThisFrame.Clear();
            for (int i = 0; i < _toCreate.Count; i++)
                _createdThisFrame.Add(_toCreate[i]);

            _carveTargets.Clear();
            for (int i = 0; i < _spawnedObjects.Count; i++)
            {
                var (entity, bounds) = _spawnedObjects[i];
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

        private Entity CreateChunkEntity(Vector2i cell, float chunkSize, ECSWorld world, TerrainGenerator generator)
        {
            Vector3 scale = new Vector3(chunkSize, 1.0f, chunkSize);
            Vector2 position = new Vector2(cell.X * chunkSize, cell.Y * chunkSize);

            var heightGrid = generator.generateChunk(position, scale.Xz, new Vector2i(TerrainRenderer.CHUNK_RESOLUTION));
            var normalGrid = generator.generateNormalGridFor(position, scale.Xz, new Vector2i(TerrainRenderer.CHUNK_RESOLUTION));
            var surface = generator.generateSurface(position, scale.Xz, heightGrid, normalGrid);

            return world.CreateEntity("Terrain Chunk",
                new TerrainChunkComponent(heightGrid, normalGrid, surface, position, scale.Xz),
                new PositionComponent(new Vector3(position.X, 0.0f, position.Y)),
                new ScaleComponent(scale),
                new LocalToWorldMatrixComponent()
            );
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

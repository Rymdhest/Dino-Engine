using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Procedural.Terrain;
using Dino_Engine.Rendering.Renderers.Geometry;
using Dino_Engine.Util.Data_Structures;
using OpenTK.Mathematics;
using System.Xml.Linq;

namespace Dino_Engine.ECS.Systems
{
    public class TerrainChunkSystem : SystemBase
    {
        public TerrainChunkSystem()
            : base(new BitMask(typeof(TerrainChunkComponent), typeof(ScaleComponent), typeof(LocalToWorldMatrixComponent)))
        {
        }

        public override void Update(ECSWorld world, float deltaTime)
        {
            QuadTreeNode quadtree = world.GetComponent<TerrainQuadTreeComponent>( world.GetSingleton<TerrainQuadTreeComponent>()).QuadTree;
            TerrainGenerator generator = world.GetComponent<TerrainGeneratorComponent>(world.GetSingleton<TerrainGeneratorComponent>()).Generator;
            Vector3 cameraPos = world.GetComponent<LocalToWorldMatrixComponent>(world.Camera).value.ExtractTranslation();
            UpdateNodeLODRecursive(quadtree, cameraPos, world, generator);
        }

        private void UpdateNodeLODRecursive(QuadTreeNode node, Vector3 cameraPos, ECSWorld world, TerrainGenerator generator)
        {
            float distance = Vector2.Distance(cameraPos.Xz, node.GetCenter());
            int desiredLOD = ComputeDesiredLOD(distance, node, 3f, 7, 30f);

            if (desiredLOD > node.Depth)
            {
                if (node.Children == null)
                    node.Subdivide();

                foreach (var child in node.Children)
                    UpdateNodeLODRecursive(child, cameraPos, world, generator);

                // Since we split, this node is no longer a leaf → remove its chunk entity if valid
                if (node.ChunkEntity.IsValid())
                {
                    world.DestroyEntity(node.ChunkEntity);
                    node.ChunkEntity = Entity.Invalid;
                }
            }
            else if (desiredLOD < node.Depth)
            {
                // Needs merge: remove children recursively if they exist
                if (node.Children != null)
                {
                    RemoveChildEntities(node, world);
                    node.Children = null;
                }

                // Now node is a leaf: make sure it has a valid chunk entity
                if (!node.ChunkEntity.IsValid())
                    node.ChunkEntity = CreateChunkEntity(node, world, generator);
            }
            else
            {
                // Desired LOD matches current node’s LOD:
                if (node.Children != null)
                {
                    foreach (var child in node.Children)
                        UpdateNodeLODRecursive(child, cameraPos, world, generator);

                    // If still has chunk entity (since node has children, it shouldn’t), remove it
                    if (node.ChunkEntity.IsValid())
                    {
                        world.DestroyEntity(node.ChunkEntity);
                        node.ChunkEntity = Entity.Invalid;
                    }
                }
                else
                {
                    // Leaf: ensure chunk entity exists
                    if (!node.ChunkEntity.IsValid())
                        node.ChunkEntity = CreateChunkEntity(node, world, generator);
                }
            }
        }
        private int ComputeDesiredLOD(float distance, QuadTreeNode node, float lodFactor, int maxDepth, float minSize)
        {
            // Early out if node too small → deepest LOD reached
            if (node.Size <= minSize)
                return maxDepth;

            // Compute desired depth directly based on distance and quadtree parameters
            int desiredDepth = 0;

            // At each possible depth, estimate the size of a node at that depth, and determine
            // if the camera distance would require that level of detail.
            for (int d = 0; d <= maxDepth; d++)
            {
                // Approximate node size at this depth: root size / 2^depth
                float sizeAtDepth = 1000f / (1 << d);

                float threshold = sizeAtDepth * lodFactor;

                if (distance < threshold)
                    desiredDepth = d;
            }

            return desiredDepth;
        }

        private Entity CreateChunkEntity(QuadTreeNode node, ECSWorld world, TerrainGenerator generator)
        {
            Vector3 scale = new Vector3(node.Size, 1.0f, node.Size);
            Vector2 position = node.WorldPos;
            var heightGrid = generator.generateChunk(node.WorldPos, scale.Xz, new Vector2i(TerrainRenderer.CHUNK_RESOLUTION));
            var normalgrid = generator.generateNormalGridFor(heightGrid, scale, position);


            return world.CreateEntity("Terrain Chunk",
                new TerrainChunkComponent(heightGrid, normalgrid),
                new PositionComponent(new Vector3(position.X, 0.0f, position.Y)),
                new ScaleComponent(scale),
                new LocalToWorldMatrixComponent()
            );
        }
        private void RemoveChildEntities(QuadTreeNode node, ECSWorld world)
        {
            if (node.Children == null)
                return;

            foreach (var child in node.Children)
            {
                if (child.ChunkEntity.IsValid())
                {
                    world.DestroyEntity(child.ChunkEntity);
                    child.ChunkEntity = Entity.Invalid;
                }

                RemoveChildEntities(child, world);
            }
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            throw new NotImplementedException();
        }
    }
}

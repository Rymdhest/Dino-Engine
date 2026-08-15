using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Procedural.Terrain;
using Dino_Engine.Physics;
using Dino_Engine.Rendering.Renderers.Geometry;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;

namespace Dino_Engine.ECS.Systems
{
    public class RenderSpatialGridSystem : SystemBase
    {
        public RenderSpatialGridSystem()
            : base(new BitMask())
        {
            Priority = 1;
        }

        internal override void UpdateInternal(ECSWorld world, float deltaTime)  
        {
            Entity gridSingleton = world.GetSingleton<RenderSpatialGridSingleton>();

            SpatialGrid grid = world.GetComponent<RenderSpatialGridSingleton>(gridSingleton).Grid;

            DirtyEntitiesBuffers dirtyEntitiesBuffers = world.DirtyEntitiesBuffers;

            // 1. Process NEW entities (Run ONCE when spawned)
            var newEntities = dirtyEntitiesBuffers.SpawnedEntitiesBuffer;
            for (int i = 0; i < newEntities.Count; i++)
            {
                Entity entity = newEntities[i];
                if (world.HasComponent<ModelComponent>(entity))
                {
                    UpdateEntityInGrid(world, grid, entity);
                }
            }

            // 2. Process MOVED entities (O(M) iteration - only entities that actually moved!)
            var movedEntities = dirtyEntitiesBuffers.TransformChangesBuffer;
            for (int i = 0; i < movedEntities.Count; i++)
            {
                Entity entity = movedEntities[i];
                if (world.HasComponent<ModelComponent>(entity))
                {
                    UpdateEntityInGrid(world, grid, entity);
                }
            }

            // 3. Process DESTROYED entities
            var destroyedEntities = dirtyEntitiesBuffers.DestroyedEntitiesBuffer;
            for (int i = 0; i < destroyedEntities.Count; i++)
            {
                grid.Remove(destroyedEntities[i]);
            }

        }
        private void UpdateEntityInGrid(ECSWorld world, SpatialGrid grid, Entity entity)
        {
            var ltw = world.GetComponent<LocalToWorldMatrixComponent>(entity).value;
            var modelComp = world.GetComponent<ModelComponent>(entity);

            AABB localBounds = modelComp.model != null
                ? new AABB(modelComp.model.box.Min, modelComp.model.box.Max)
                : new AABB(new Vector3(-1f), new Vector3(1f));

            AABB worldBounds = AABB.Transform(localBounds, ltw);
            grid.InsertOrUpdate(entity, worldBounds);
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            throw new NotImplementedException();
        }
    }
}
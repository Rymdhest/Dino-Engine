using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Model;
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

            // 1. Process DESTROYED entities FIRST. A recycled id goes back in below, if there is something alive under it.
            var destroyedEntities = dirtyEntitiesBuffers.DestroyedEntitiesBuffer;
            for (int i = 0; i < destroyedEntities.Count; i++)
            {
                grid.Remove(destroyedEntities[i]);
            }

            // 2. Process NEW entities
            var newEntities = dirtyEntitiesBuffers.SpawnedEntitiesBuffer;
            for (int i = 0; i < newEntities.Count; i++)
            {
                Entity entity = newEntities[i];
                if (world.HasComponent<ModelComponent>(entity))
                {
                    UpdateEntityInGrid(world, grid, entity);
                }
            }

            // 3. Process MOVED entities
            var movedEntities = dirtyEntitiesBuffers.TransformChangesBuffer;
            for (int i = 0; i < movedEntities.Count; i++)
            {
                Entity entity = movedEntities[i];
                if (world.HasComponent<ModelComponent>(entity))
                {
                    UpdateEntityInGrid(world, grid, entity);
                }
            }

            // Cleared here, by what reads them, and nowhere else. Entities come and go whenever deferred commands are applied, which
            // isn't only between frames: one lost from these lists stays in the grid under an id that goes on to mean something else.
            dirtyEntitiesBuffers.ClearAll();
        }
        private void UpdateEntityInGrid(ECSWorld world, SpatialGrid grid, Entity entity)
        {
            var ltw = world.GetComponent<LocalToWorldMatrixComponent>(entity).value;
            var modelComp = world.GetComponent<ModelComponent>(entity);
            grid.InsertOrUpdate(entity, ComputeWorldBounds(modelComp.model, ltw));
        }

        public static AABB ComputeWorldBounds(glModel? model, Matrix4 localToWorld)
        {
            AABB localBounds = model != null
                ? new AABB(model.box.Min, model.box.Max)
                : new AABB(new Vector3(-1f), new Vector3(1f));

            return AABB.Transform(localBounds, localToWorld);
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            throw new NotImplementedException();
        }
    }
}
using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Procedural.Terrain;
using Dino_Engine.Rendering.Renderers.Geometry;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;

namespace Dino_Engine.ECS.Systems
{
    public class FixToTerrainHeightSystem : SystemBase
    {


        // The PhysicsSystem naturally iterates over Dynamic bodies using the base class mask
        public FixToTerrainHeightSystem()
            : base(new BitMask(typeof(PositionComponent), typeof(FixToTerrainHeightComponent)))
        {

        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            var pos = entity.Get<PositionComponent>();

            TerrainGenerator generator = world.GetComponent<TerrainGeneratorSingleton>(world.GetSingleton<TerrainGeneratorSingleton>()).Generator;
            pos.value.Y = generator.getHeightAt(pos.value.Xz);
            entity.Set(pos);
        }
    }
}
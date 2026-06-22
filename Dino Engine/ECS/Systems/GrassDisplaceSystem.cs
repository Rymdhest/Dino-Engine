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
    public class GrassDisplaceSystem : SystemBase
    {


        // The PhysicsSystem naturally iterates over Dynamic bodies using the base class mask
        public GrassDisplaceSystem()
            : base(new BitMask())
        {

        }

        internal override void UpdateInternal(ECSWorld world, float deltaTime)
        {
            var buffer = world.GetComponent<CollisionEventBufferComponent>(world.GetSingleton<CollisionEventBufferComponent>());

            foreach (var collisionEvent in buffer.Events)
            {
                Console.WriteLine(collisionEvent.ContactPoint);

                BlastData blast = new BlastData();
                blast.exponent = 0.6f;
                blast.radius = 1.33f;
                blast.power = 1.6f;
                blast.center = collisionEvent.ContactPoint.Xz;

                Engine.RenderEngine._grassRenderer.blasts.Add(blast);
            }

        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            throw new NotImplementedException();
        }
    }
}
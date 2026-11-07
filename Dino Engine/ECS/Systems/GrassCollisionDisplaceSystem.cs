using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Procedural.Terrain;
using Dino_Engine.Rendering.Renderers.Simulation;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;

namespace Dino_Engine.ECS.Systems
{
    public class GrassCollisionDisplaceSystem : SystemBase
    {


        // The PhysicsSystem naturally iterates over Dynamic bodies using the base class mask
        public GrassCollisionDisplaceSystem()
            : base(new BitMask())
        {

        }

        internal override void UpdateInternal(ECSWorld world, float deltaTime)
        {
            var buffer = world.GetComponent<CollisionEventBufferSingleton>(world.GetSingleton<CollisionEventBufferSingleton>());

            foreach (var collisionEvent in buffer.Events)
            {
                Engine.RenderEngine._windRenderer.AddVegetationContact(new VegetationContact
                {
                    center = collisionEvent.ContactPoint.Xz,
                    previous = collisionEvent.ContactPoint.Xz,
                    radius = 1.33f,
                    lean = 1.2f,
                    press = 1.1f,
                });
            }

        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            throw new NotImplementedException();
        }
    }
}
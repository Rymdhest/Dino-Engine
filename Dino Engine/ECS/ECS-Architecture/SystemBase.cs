using Dino_Engine.Core;
using Dino_Engine.Rendering.Renderers.PosGeometry;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.ECS.ECS_Architecture
{
    public abstract class SystemBase
    {
        protected readonly BitMask WithMask;
        protected readonly BitMask WithoutMask;


        protected SystemBase(BitMask withMask, BitMask withoutMask)
        {
            WithMask = withMask;
            WithoutMask = withoutMask;
        }
        protected SystemBase(BitMask withMask)
        {
            WithMask = withMask;
            WithoutMask = BitMask.Empty;
        }
        protected abstract void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime);
        protected virtual void ResizeEntity(EntityView entity, ECSWorld world, ResizeEventArgs args) { }

        public virtual void Update(ECSWorld world, float deltaTime)
        {
            foreach (var archetype in world.QueryArchetypes(WithMask, WithoutMask))
            {
                var accessor = new ComponentAccessor(archetype);

                foreach (var entity in accessor)
                {
                    UpdateEntity(entity, world, deltaTime);
                }
            }
        }

        public void OnResize(ECSWorld world, ResizeEventArgs args)
        {
            foreach (var archetype in world.QueryArchetypes(WithMask, WithoutMask))
            {
                var accessor = new ComponentAccessor(archetype);

                foreach (var entity in accessor)
                {
                    ResizeEntity(entity, world, args);
                }
            }
        }
    }
}

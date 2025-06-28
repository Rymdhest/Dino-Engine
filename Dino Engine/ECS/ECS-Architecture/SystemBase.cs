using Dino_Engine.Core;
using Dino_Engine.Rendering.Renderers.PosGeometry;
using Dino_Engine.Util;
using OpenTK.Mathematics;
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

        public abstract void Update(ECSWorld world, float deltaTime);
    }

    public class MovementSystem : SystemBase
    {

        public MovementSystem() : base(new BitMask(typeof(Position), typeof(Velocity))) { }

        public override void Update(ECSWorld world)
        {
            foreach (var arch in world.Query(WithMask, WithoutMask))
            {
                var posArray = (ComponentArray<Position>)arch.ComponentArrays[ComponentTypeRegistry.GetId<Position>()];
                var velArray = (ComponentArray<Velocity>)arch.ComponentArrays[ComponentTypeRegistry.GetId<Velocity>()];

                for (int i = 0; i < arch.Entities.Count ; i++)
                {
                    Position pos = posArray.Get(i);
                    Velocity vel = velArray.Get(i);

                    float delta = Engine.Delta;
                    pos.X += vel.X * delta;
                    pos.Y += vel.Y * delta;
                    pos.Z += vel.Z * delta;

                    posArray.Set(i, pos);
                }
            }
        }
    }

    public class ParticleSystem : SystemBase
    {

        public ParticleSystem() : base(new BitMask(typeof(Position), typeof(LocalToWorldMatrixComponent))) { }

        public override void Update(ECSWorld world)
        {
            foreach (var arch in world.Query(WithMask, WithoutMask))
            {
                var posArray = (ComponentArray<Position>)arch.ComponentArrays[ComponentTypeRegistry.GetId<Position>()];
                var matrixArray = (ComponentArray<LocalToWorldMatrixComponent>)arch.ComponentArrays[ComponentTypeRegistry.GetId<LocalToWorldMatrixComponent>()];

                for (int i = 0; i < arch.Entities.Count; i++)
                {
                    Position pos = posArray.Get(i);
                    LocalToWorldMatrixComponent mat = matrixArray.Get(i);
                    mat.LocalToWorldMatrix = MyMath.createTransformationMatrix(new Vector3(pos.X, pos.Y, pos.Z));
                    matrixArray.Set(i, mat);
                    ParticleRenderCommand command = new ParticleRenderCommand();
                    command.localToWorldMatrix = mat.LocalToWorldMatrix;
                    Engine.RenderEngine._particleRenderer.SubmitCommand(command);
                }
            }
        }
    }
}

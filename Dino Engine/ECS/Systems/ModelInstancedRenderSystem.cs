using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Rendering.Renderers.Geometry;
using Dino_Engine.Rendering.Renderers.Lighting;
using Dino_Engine.Rendering.Renderers.PosGeometry;
using OpenTK.Mathematics;
using System.Transactions;

namespace Dino_Engine.ECS.Systems
{
    public class ModelInstancedRenderSystem : SystemBase
    {
        private List<Matrix4> matrices = new List<Matrix4>();
        private glModel model;

        public ModelInstancedRenderSystem()
            : base(new BitMask(typeof(modelInstancedRenderTag), typeof(ModelComponent), typeof(LocalToWorldMatrixComponent)))
        {
        }



        public override void Update(ECSWorld world, float deltaTime)
        {
            base.Update(world, deltaTime);

            if (matrices.Count <= 0) return;

            InstancedModelRenderCommand command = new InstancedModelRenderCommand();
            command.model = model;
            command.localToWorldMatrix = matrices.ToArray();
            Engine.RenderEngine._instancedModelRenderer.SubmitCommand(command);
            matrices.Clear();
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            matrices.Add(entity.Get<LocalToWorldMatrixComponent>().value);
            model = entity.Get<ModelComponent>().model;
        }
    }
}

using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Rendering.Renderers.Geometry;
using Dino_Engine.Rendering.Renderers.Lighting;
using Dino_Engine.Rendering.Renderers.PosGeometry;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    public class ModelRenderSystem : SystemBase
    {
        private Dictionary<glModel, List<Matrix4>> commands = new();
        private int minCountForInstanced = 10;
        public ModelRenderSystem()
            : base(new BitMask(typeof(ModelRenderTag), typeof(ModelComponent), typeof(LocalToWorldMatrixComponent)))
        {
        }

        public override void Update(ECSWorld world, float deltaTime)
        {
            base.Update(world, deltaTime);

            foreach (var cmd in commands)
            {
                ModelRenderCommand command = new ModelRenderCommand();
                command.model = cmd.Key;
                command.matrices = cmd.Value.ToArray();

                if (command.matrices.Length > minCountForInstanced)
                {
                    Engine.RenderEngine._instancedModelRenderer.SubmitGeometryCommand(command);
                } else
                {
                    Engine.RenderEngine._modelRenderer.SubmitGeometryCommand(command);
                }

                cmd.Value.Clear();
            }

            commands.Clear();
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            glModel model = entity.Get<ModelComponent>().model;
            Matrix4 modelMatrix = entity.Get<LocalToWorldMatrixComponent>().value;

            if (!commands.ContainsKey(model)) commands[model] = new List<Matrix4>();

            commands[model].Add(modelMatrix);
        }
    }
}

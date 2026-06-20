using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Rendering.Renderers.Geometry;
using OpenTK.Mathematics;
using System.Collections.Generic;

namespace Dino_Engine.ECS.Systems
{
    public class ModelRenderSystem : SystemBase
    {
        private readonly Dictionary<glModel, List<Matrix4>> _commands = new();
        private readonly int _minCountForInstanced = 10;

        public ModelRenderSystem()
            : base(new BitMask(typeof(ModelRenderTag), typeof(ModelComponent), typeof(LocalToWorldMatrixComponent)))
        {
        }

        internal override void UpdateInternal(ECSWorld world, float deltaTime)
        {
            // Clear previous frame commands without reallocating the dictionary itself
            foreach (var list in _commands.Values)
            {
                list.Clear();
            }

            // Bulk process all archetypes that match our requirement
            foreach (var archetype in world.QueryArchetypes(WithMask, WithoutMask))
            {
                var modelArray = archetype.GetComponentArray<ModelComponent>();
                var matrixArray = archetype.GetComponentArray<LocalToWorldMatrixComponent>();
                int count = archetype.EntityCount;

                for (int i = 0; i < count; i++)
                {
                    glModel model = modelArray[i].model;
                    Matrix4 matrix = matrixArray[i].value;

                    if (!_commands.TryGetValue(model, out var list))
                    {
                        list = new List<Matrix4>();
                        _commands[model] = list;
                    }
                    list.Add(matrix);
                }
            }

            // Submit grouped commands to the renderers
            foreach (var kvp in _commands)
            {
                var model = kvp.Key;
                var matrices = kvp.Value;

                if (matrices.Count == 0) continue;

                var command = new ModelRenderCommand
                {
                    model = model,
                    matrices = matrices.ToArray()
                };

                if (command.matrices.Length > _minCountForInstanced)
                {
                    Engine.RenderEngine._instancedModelRenderer.SubmitGeometryCommand(command);
                }
                else
                {
                    Engine.RenderEngine._modelRenderer.SubmitGeometryCommand(command);
                }
            }
        }

        // Overridden to be unused since we use bulk UpdateInternal
        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime) { }
    }
}
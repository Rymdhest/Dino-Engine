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


        private readonly Dictionary<glModel, List<Matrix4>> _nearModels = new();

        // Far: One global list for all imposters (Single Draw Call)
        private readonly List<ImposterInstanceData> _farImposters = new();

        private readonly float _lodDistanceSquared = 20.0f * 20.0f; // Example: 100 units
        private readonly int _minCountForInstanced = 10;

        public ModelRenderSystem()
            : base(new BitMask(typeof(ModelRenderTag), typeof(ModelComponent), typeof(LocalToWorldMatrixComponent)))
        {
        }

        internal override void UpdateInternal(ECSWorld world, float deltaTime)
        {
            // 1. Clear state
            foreach (var list in _nearModels.Values) list.Clear();
            _farImposters.Clear();

            Vector3 camPos = world.GetComponent<LocalToWorldMatrixComponent>(world.Camera).value.ExtractTranslation();

            // 2. Classify Archetypes
            foreach (var archetype in world.QueryArchetypes(WithMask, WithoutMask))
            {
                var modelArray = archetype.GetComponentArray<ModelComponent>();
                var matrixArray = archetype.GetComponentArray<LocalToWorldMatrixComponent>();
                int count = archetype.EntityCount;

                for (int i = 0; i < count; i++)
                {
                    Matrix4 mat = matrixArray[i].value;
                    Vector3 pos = mat.ExtractTranslation();
                    float distSq = (pos - camPos).LengthSquared;

                    glModel model = modelArray[i].model;

                    if (distSq > _lodDistanceSquared)
                    {
                        // Add to global far bucket
                        _farImposters.Add(new ImposterInstanceData
                        {
                            Position = pos,
                            Scale = mat.ExtractScale().Y * 10, 
                            RotationY = MathF.Atan2(mat.M13, mat.M11)
                            
                        });
                    }
                    else
                    {
                        // Add to near bucket
                        if (!_nearModels.TryGetValue(model, out var list))
                        {
                            list = new List<Matrix4>();
                            _nearModels[model] = list;
                        }
                        list.Add(mat);
                    }
                }
            }

            // 3. Submit Near Commands (Instanced or Standard)
            foreach (var kvp in _nearModels)
            {
                if (kvp.Value.Count == 0) continue;

                if (kvp.Value.Count > _minCountForInstanced)
                {
                    Engine.RenderEngine._instancedModelRenderer.SubmitGeometryCommand(new ModelRenderCommand( kvp.Key, kvp.Value.ToArray()));
                }
                else
                {
                    Engine.RenderEngine._modelRenderer.SubmitGeometryCommand(new ModelRenderCommand(kvp.Key, kvp.Value.ToArray()));
                }
            }

            // 4. Submit Global Imposter Command (One draw call total!)
            if (_farImposters.Count > 0)
            {
                Engine.RenderEngine._imposterRenderer.SubmitGeometryCommand(new ImposterRenderCommand
                {
                    instances = _farImposters.ToArray()
                });
            }
        }

        // Overridden to be unused since we use bulk UpdateInternal
        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime) { }
    }
}
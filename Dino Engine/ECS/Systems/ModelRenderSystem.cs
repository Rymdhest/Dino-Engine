using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Rendering.Renderers.Geometry;
using OpenTK.Mathematics;
using System.Collections.Generic;
using static Dino_Engine.Modelling.Model.glModel;
using static OpenTK.Graphics.OpenGL.GL;

namespace Dino_Engine.ECS.Systems
{
    public class ModelRenderSystem : SystemBase
    {
        private readonly Dictionary<glModel, List<Matrix4>> _commands = new();


        private readonly Dictionary<glModel, List<Matrix4>> _models = new();

        // Far: One global list for all imposters (Single Draw Call)
        private readonly List<ImposterInstanceData> _imposters = new();

        private readonly int _minCountForInstanced = 1;

        public ModelRenderSystem()
            : base(new BitMask(typeof(ModelRenderTag), typeof(ModelComponent), typeof(LocalToWorldMatrixComponent), typeof(RotationComponent), typeof(PositionComponent), typeof(ScaleComponent)))
        {
        }

        internal override void UpdateInternal(ECSWorld world, float deltaTime)
        {
            // 1. Clear state
            foreach (var list in _models.Values) list.Clear();
            _imposters.Clear();

            Vector3 camPos = world.GetComponent<LocalToWorldMatrixComponent>(world.Camera).value.ExtractTranslation();

            // 2. Classify Archetypes
            foreach (var archetype in world.QueryArchetypes(WithMask, WithoutMask))
            {
                var modelArray = archetype.GetComponentArray<ModelComponent>();
                var matrixArray = archetype.GetComponentArray<LocalToWorldMatrixComponent>();
                var rotArray = archetype.GetComponentArray<RotationComponent>();

                var posArray = archetype.GetComponentArray<PositionComponent>();
                var scaleArray = archetype.GetComponentArray<ScaleComponent>();

                int count = archetype.EntityCount;

                for (int i = 0; i < count; i++)
                {
                    Matrix4 mat = matrixArray[i].value;
                    Vector3 pos = posArray[i].value;
                    float distSq = (pos - camPos).LengthSquared;

                    glModel model = modelArray[i].model;
                    bool useImposter = false;

                    if (model.Imposter != null)
                    {
                        if (distSq > model.Imposter.DistanceSquared) useImposter = true;
                    }

                    if (useImposter)
                    {
                        Vector3 entityScale = scaleArray[i].value;
                        ImposterData imposter = model.Imposter;

                        // 3. Grab the pure quaternion directly from ECS (assuming the field is called 'value' or 'rotation')
                        Quaternion rot = rotArray[i].quaternion;

                        // 4. Standard forward transformation. No inversion hacks!
                        Vector3 scaledCenter = imposter.LocalCenter * entityScale;
                        Vector3 rotatedOffset = Vector3.Transform(scaledCenter, rot);
                        Vector3 quadWorldCenter = pos + rotatedOffset;

                        _imposters.Add(new ImposterInstanceData
                        {
                            modelID = (float)model.Imposter.TextureIndex,
                            Position = quadWorldCenter,
                            Scale = entityScale,
                            Rotation = rot, // Pass pure rotation to shader
                            BaseLength = imposter.BaseLength
                        });
                    }
                    else
                    {
                        if (!_models.TryGetValue(model, out var list))
                        {
                            list = new List<Matrix4>();
                            _models[model] = list;
                        }
                        list.Add(mat);
                    }
                }
            }

            // 3. Submit Near Commands (Instanced or Standard)
            foreach (var kvp in _models)
            {
                if (kvp.Value.Count == 0) continue;

                if (kvp.Value.Count > _minCountForInstanced)
                {
                    Engine.RenderEngine._instancedModelRenderer.SubmitGeometryCommand(new ModelRenderCommand( kvp.Key, kvp.Value));
                }
                else
                {
                    Engine.RenderEngine._modelRenderer.SubmitGeometryCommand(new ModelRenderCommand(kvp.Key, kvp.Value));
                }
            }

            // 4. Submit Global Imposter Command (One draw call total!)
            if (_imposters.Count > 0)
            {
                Engine.RenderEngine._imposterRenderer.SubmitGeometryCommand(new ImposterRenderCommand
                {
                    instances = _imposters.ToArray()
                });
            }
        }

        // Overridden to be unused since we use bulk UpdateInternal
        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime) { }
    }
}
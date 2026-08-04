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

        private readonly int _minCountForInstanced = 10;

        public ModelRenderSystem()
            : base(new BitMask(typeof(ModelRenderTag), typeof(ModelComponent), typeof(LocalToWorldMatrixComponent)))
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
                int count = archetype.EntityCount;

                for (int i = 0; i < count; i++)
                {
                    Matrix4 mat = matrixArray[i].value;
                    Vector3 pos = mat.ExtractTranslation();
                    float distSq = (pos - camPos).LengthSquared;

                    glModel model = modelArray[i].model;
                    bool useImposter = false;

                    if (model.Imposter != null)
                    {
                        if (distSq > model.Imposter.DistanceSquared) useImposter = true;
                    }

                    if (useImposter)
                    {
                        Vector3 entityScale = mat.ExtractScale();
                        ImposterData imposter = model.Imposter;

                        // 1. Safe extraction of rotation (fixes spinning bugs caused by scale)
                        float rotY = MathF.Atan2(mat.M13, mat.M11);

                        // 2. Rotate pre-calculated local center by entity's Y-rotation
                        // Note: imposter.LocalCenter must be calculated in your constructor as: (box.max + box.min) / 2f
                        Vector3 scaledCenter = imposter.LocalCenter * entityScale;

                        // USE rotY HERE, NOT ExtractRotation()
                        float cos = MathF.Cos(rotY);
                        float sin = MathF.Sin(rotY);

                        Vector3 rotatedOffset = new Vector3(
                            scaledCenter.X * cos - scaledCenter.Z * sin,
                            scaledCenter.Y, // Y offset is unaffected by Y-rotation
                            scaledCenter.X * sin + scaledCenter.Z * cos
                        );

                        // 3. Final quad center in world space
                        Vector3 quadWorldCenter = pos + rotatedOffset;

                        // 4. Add to instance buffer
                        _imposters.Add(new ImposterInstanceData
                        {
                            modelID = (float)model.Imposter.TextureIndex,
                            Position = quadWorldCenter,

                            // imposter.Scale should be (maxXZ, length.Y) from your texture generation step
                            Scale = imposter.Scale * new Vector2(MathF.Max(entityScale.X, entityScale.Z), entityScale.Y),
                            RotationY = rotY
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
                    Engine.RenderEngine._instancedModelRenderer.SubmitGeometryCommand(new ModelRenderCommand( kvp.Key, kvp.Value.ToArray()));
                }
                else
                {
                    Engine.RenderEngine._modelRenderer.SubmitGeometryCommand(new ModelRenderCommand(kvp.Key, kvp.Value.ToArray()));
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
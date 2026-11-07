using System;
using System.Collections.Generic;
using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Rendering.Renderers.Geometry;
using Dino_Engine.Rendering.Renderers.PosGeometry;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    public class CharacterRenderSystem : SystemBase
    {
        private readonly Dictionary<glModel, List<CharacterInstanceData>> _characterCommands = new();

        public CharacterRenderSystem()
            : base(new BitMask(typeof(CharacterModelComponent), typeof(SkeletonComponent), typeof(LocalToWorldMatrixComponent)))
        {
            Priority = -1; // Runs AFTER SkeletalAnimationSystem (Priority 0)
        }

        internal override void UpdateInternal(ECSWorld world, float deltaTime)
        {
            // 1. Clear previous frame state
            // A hero's model changes with what they wear, and the old one is freed.
            foreach (var (model, list) in _characterCommands)
            {
                if (list.Count == 0) _characterCommands.Remove(model);
                else list.Clear();
            }

            // 2. Classify Archetypes with SkeletonComponent
            foreach (var archetype in world.QueryArchetypes(WithMask, WithoutMask))
            {
                var modelArray = archetype.GetComponentArray<CharacterModelComponent>();
                var matrixArray = archetype.GetComponentArray<LocalToWorldMatrixComponent>();
                var skeletonArray = archetype.GetComponentArray<SkeletonComponent>();
                var animatorArray = archetype.GetComponentArray<AnimatorComponent>();

                int count = archetype.EntityCount;

                for (int i = 0; i < count; i++)
                {
                    glModel model = modelArray[i].model;
                    Matrix4 modelMatrix = matrixArray[i].value;

                    if (!_characterCommands.TryGetValue(model, out var list))
                    {
                        list = new List<CharacterInstanceData>();
                        _characterCommands[model] = list;
                    }

                    if (animatorArray != null && animatorArray[i].Animator.Outline.W > 0f)
                    {
                        Engine.RenderEngine._outlineRenderer.SubmitCommand(new OutlineCommand { Model = model, ModelMatrix = modelMatrix, SkinningMatrices = skeletonArray[i].SkinningMatrices, Colour = animatorArray[i].Animator.Outline });
                    }

                    list.Add(new CharacterInstanceData
                    {
                        ModelMatrix = modelMatrix,
                        SkinningMatrices = skeletonArray[i].SkinningMatrices,
                        Flash = animatorArray != null ? new Vector4(animatorArray[i].Animator.FlashColour, animatorArray[i].Animator.Flash) : Vector4.Zero,
                    });
                }
            }

            // 3. Dispatch Render Commands to CharacterRenderer
            foreach (var kvp in _characterCommands)
            {
                if (kvp.Value.Count == 0) continue;

                Engine.RenderEngine._characterRenderer.SubmitGeometryCommand(
                    new CharacterRenderCommand(kvp.Key, kvp.Value)
                );
            }
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime) { }
    }
}
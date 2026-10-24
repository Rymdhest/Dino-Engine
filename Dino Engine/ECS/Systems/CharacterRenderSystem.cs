using System;
using System.Collections.Generic;
using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Rendering.Renderers.Geometry;
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
            foreach (var list in _characterCommands.Values)
            {
                list.Clear();
            }

            // 2. Classify Archetypes with SkeletonComponent
            foreach (var archetype in world.QueryArchetypes(WithMask, WithoutMask))
            {
                var modelArray = archetype.GetComponentArray<CharacterModelComponent>();
                var matrixArray = archetype.GetComponentArray<LocalToWorldMatrixComponent>();
                var skeletonArray = archetype.GetComponentArray<SkeletonComponent>();

                int count = archetype.EntityCount;

                for (int i = 0; i < count; i++)
                {
                    glModel model = modelArray[i].model;
                    Matrix4 modelMatrix = matrixArray[i].value;
                    var skeleton = skeletonArray[i];

                    var skinningMatrices = skeleton.SkinningMatrices;
                    int boneCount = skinningMatrices != null ? skinningMatrices.Length : (int)BoneType.Count;
                    Matrix4[] skinningMatricesCopy = new Matrix4[boneCount];

                    if (skinningMatrices != null && skinningMatrices.Length > 0)
                    {
                        Array.Copy(skinningMatrices, skinningMatricesCopy, boneCount);
                    }
                    else
                    {
                        for (int b = 0; b < boneCount; b++)
                        {
                            skinningMatricesCopy[b] = Matrix4.Identity;
                        }
                    }

                    if (!_characterCommands.TryGetValue(model, out var list))
                    {
                        list = new List<CharacterInstanceData>();
                        _characterCommands[model] = list;
                    }

                    list.Add(new CharacterInstanceData
                    {
                        ModelMatrix = modelMatrix,
                        SkinningMatrices = skinningMatricesCopy
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
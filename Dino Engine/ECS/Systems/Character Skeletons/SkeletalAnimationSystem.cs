using System;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    public class SkeletalAnimationSystem : SystemBase
    {
        private int _frameCounter = 0;

        public SkeletalAnimationSystem()
            : base(new BitMask(typeof(SkeletonComponent), typeof(AnimationComponent)))
        {
            Priority = -2; // Runs before CharacterRenderSystem (-1)
        }

        internal override void UpdateInternal(ECSWorld world, float deltaTime)
        {
            _frameCounter++;
            bool logThisFrame = (_frameCounter % 60 == 0);

            int totalEntities = 0;

            foreach (var archetype in world.QueryArchetypes(WithMask, WithoutMask))
            {
                var skeletonArray = archetype.GetComponentArray<SkeletonComponent>();
                var stackArray = archetype.GetComponentArray<AnimationComponent>();

                int count = archetype.EntityCount;
                totalEntities += count;

                for (int i = 0; i < count; i++)
                {
                    var skeleton = skeletonArray[i];
                    var stack = stackArray[i];

                    skeleton.EnsureAllocated();

                    if (stack.Layers == null || stack.Layers.Count == 0)
                    {
                        if (logThisFrame)
                            Console.WriteLine($"[AnimSystem] WARNING: Entity {i} has NO animation layers!");
                        continue;
                    }

                    // 1. Advance clip timers & cleanup finished non-looping layers
                    for (int j = stack.Layers.Count - 1; j >= 0; j--)
                    {
                        var layer = stack.Layers[j];
                        if (layer.Clip == null) continue;

                        layer.Time += deltaTime * layer.Speed;

                        if (layer.Time >= layer.Clip.Duration)
                        {
                            if (layer.Loop)
                            {
                                layer.Time %= layer.Clip.Duration;
                            }
                            else
                            {
                                stack.Layers.RemoveAt(j);
                                continue;
                            }
                        }
                    }

                    // 2. Bone-by-Bone Priority Resolution & Keyframe Evaluation
                    int boneCount = skeleton.ParentIndices.Length;
                    for (int b = 0; b < boneCount; b++)
                    {
                        BoneType bone = (BoneType)b;

                        AnimationLayer winningLayer = null;
                        int maxPriority = int.MinValue;

                        foreach (var layer in stack.Layers)
                        {
                            if (layer.Clip != null && layer.Clip.Tracks.ContainsKey(bone) && layer.Priority > maxPriority)
                            {
                                maxPriority = layer.Priority;
                                winningLayer = layer;
                            }
                        }
                        if (winningLayer != null)
                        {
                            var track = winningLayer.Clip.Tracks[bone];
                            Quaternion sampledRot = EvaluateTrack(track, winningLayer.Time);

                            // Combine with the rest pose orientation so it offsets from the T-pose correctly
                            Quaternion targetRot = (skeleton.LocalRestRotations[b] * sampledRot).Normalized();

                            skeleton.LocalRotations[b] = Quaternion.Slerp(
                                skeleton.LocalRotations[b],
                                targetRot,
                                deltaTime * 20f
                            );
                        }
                        else
                        {
                            skeleton.LocalRotations[b] = Quaternion.Slerp(
                                skeleton.LocalRotations[b],
                                skeleton.LocalRestRotations[b],
                                deltaTime * 20f
                            );
                        }

                        // Use rest offset/position
                        skeleton.LocalPositions[b] = skeleton.LocalRestPositions[b];
                    }

                    // 3. Update hierarchy matrices & GPU skinning (Using original working order)
                    UpdateModelTransforms(ref skeleton);
                    ComputeSkinningMatrices(ref skeleton);

                    skeletonArray[i] = skeleton;
                    stackArray[i] = stack;
                }
            }
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime) { }

        private Quaternion EvaluateTrack(AnimationTrack track, float time)
        {
            var keys = track.Keyframes;
            if (keys.Count == 0) return Quaternion.Identity;
            if (keys.Count == 1 || time <= keys[0].Time) return keys[0].Rotation;
            if (time >= keys[keys.Count - 1].Time) return keys[keys.Count - 1].Rotation;

            for (int i = 0; i < keys.Count - 1; i++)
            {
                if (time >= keys[i].Time && time <= keys[i + 1].Time)
                {
                    float t = (time - keys[i].Time) / (keys[i + 1].Time - keys[i].Time);
                    return Quaternion.Slerp(keys[i].Rotation, keys[i + 1].Rotation, t).Normalized();
                }
            }

            return keys[keys.Count - 1].Rotation;
        }

        private void UpdateModelTransforms(ref SkeletonComponent skeleton)
        {
            int count = skeleton.ParentIndices.Length;
            for (int i = 0; i < count; i++)
            {
                int parent = skeleton.ParentIndices[i];
                Matrix4 local = Matrix4.CreateFromQuaternion(skeleton.LocalRotations[i]) *
                                Matrix4.CreateTranslation(skeleton.LocalPositions[i]);

                // Original working matrix order
                skeleton.ModelTransforms[i] = parent == -1 ? local : local * skeleton.ModelTransforms[parent];
            }
        }

        private void ComputeSkinningMatrices(ref SkeletonComponent skeleton)
        {
            int count = skeleton.ParentIndices.Length;
            for (int i = 0; i < count; i++)
            {
                // Original working matrix order
                skeleton.SkinningMatrices[i] = skeleton.BindPoseInverse[i] * skeleton.ModelTransforms[i];
            }
        }
    }
}
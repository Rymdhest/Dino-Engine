using System.Collections.Generic;
using OpenTK.Mathematics;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Animation;

namespace Dino_Engine.ECS.Components
{
    public struct Keyframe
    {
        public float Time;
        public Quaternion Rotation;

        public Keyframe(float time, Quaternion rotation)
        {
            Time = time;
            Rotation = rotation;
        }
    }

    public class AnimationTrack
    {
        public List<Keyframe> Keyframes;

        public AnimationTrack(List<Keyframe> keyframes)
        {
            Keyframes = keyframes;
        }
    }

    public class AnimationClip
    {
        public AnimationId Id;
        public float Duration;
        public int Priority;
        public Dictionary<BoneType, AnimationTrack> Tracks = new();
    }

    public class AnimationLayer
    {
        public AnimationId ClipId;
        public AnimationClip Clip;
        public int Priority;
        public float Time;
        public float Speed;
        public bool Loop;

        public AnimationLayer(AnimationId clipId, AnimationClip clip, int priority, bool loop, float speed = 1.0f)
        {
            ClipId = clipId;
            Clip = clip;
            Priority = priority;
            Loop = loop;
            Speed = speed;
            Time = 0.0f;
        }
    }

    public struct AnimationComponent : IComponent
    {
        public List<AnimationLayer> Layers;

        // Parameterless constructor automatically provisions the permanent base Idle layer
        public AnimationComponent()
        {
            Layers = new List<AnimationLayer>
            {
                new AnimationLayer(AnimationId.Idle, AnimationRegistry.Get(AnimationId.Idle), priority: 0, loop: true)
            };
        }

        /// <summary>
        /// Universally enforces that an animation type can only exist once. 
        /// If already active, it resets its timer. If not, it safely adds it.
        /// </summary>
        public void PlayLayer(AnimationId clipId, bool loop = false, float speed = 1.0f)
        {
            var clip = AnimationRegistry.Get(clipId);

            StopLayer(clipId);

            Layers.Add(new AnimationLayer(clipId, clip, clip.Priority, loop, speed));
        }

        /// <summary>
        /// Universally stops and removes an animation layer by its ID.
        /// </summary>
        public void StopLayer(AnimationId clipId)
        {
            Layers.RemoveAll(l => l.ClipId == clipId);
        }
    }
}
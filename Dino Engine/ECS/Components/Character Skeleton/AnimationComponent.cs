using System.Collections.Generic;
using Dino_Engine.ECS.ECS_Architecture;
using OpenTK.Mathematics;

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

        public AnimationTrack(List<Keyframe> Keyframes)
        {
            this.Keyframes = Keyframes;
        }
    }

    public class AnimationClip
    {
        public string Name;
        public float Duration;
        public Dictionary<BoneType, AnimationTrack> Tracks = new Dictionary<BoneType, AnimationTrack>();
    }

    public class AnimationLayer
    {
        public string Name;
        public AnimationClip Clip;
        public int Priority;             // Higher number wins (e.g., Idle=0, Run=10, Attack=50)
        public float Time;               // Current playback time in the clip
        public float Speed = 1.0f;
        public bool Loop;

    }

    public struct AnimationComponent : IComponent
    {
        public List<AnimationLayer> Layers;

        public AnimationComponent(bool init)
        {
            Layers = new List<AnimationLayer>();
        }
    }
}
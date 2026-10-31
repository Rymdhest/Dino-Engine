using System;
using System.Collections.Generic;
using Dino_Engine.ECS.Components;

namespace Dino_Engine.Animation
{
    public static class AnimationRegistry
    {
        private static readonly Dictionary<AnimationId, AnimationClip> _clips = new();
        private static int _priorityCounter = 0;

        public static void Register(AnimationClip clip)
        {
            // Registration order defines the priority stack!
            clip.Priority = _priorityCounter;
            _priorityCounter += 1;

            _clips[clip.Id] = clip;
        }

        public static AnimationClip Get(AnimationId id)
        {
            if (_clips.TryGetValue(id, out var clip)) return clip;
            throw new Exception($"[AnimationRegistry] Clip '{id}' not found!");
        }

        public static void Reset()
        {
            _priorityCounter = 0;
            _clips.Clear();
        }
    }
}
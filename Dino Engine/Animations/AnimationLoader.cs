using System;
using System.Collections.Generic;
using OpenTK.Mathematics;
using Dino_Engine.ECS.Components;

namespace Dino_Engine.Animation
{
    public static class AnimationLoader
    {
        public static void Initialize()
        {
            AnimationRegistry.Reset();

            AnimationRegistry.Register(CreateIdleClip());
            AnimationRegistry.Register(CreateRunClip());
            AnimationRegistry.Register(CreateSwordSwingClip());
        }

        private static AnimationClip CreateIdleClip()
        {
            var clip = new AnimationClip
            {
                Id = AnimationId.Idle,
                Duration = 3.0f
            };

            // 1. Posture: Drop T-pose arms down along the body (-90° / +90° around Z)
            Quaternion armDownL = Quaternion.FromAxisAngle(Vector3.UnitZ, -MathF.PI / 2f);
            Quaternion armDownR = Quaternion.FromAxisAngle(Vector3.UnitZ, MathF.PI / 2f);

            // 2. Subtle breathing / swaying offsets for arms
            // (A gentle rotation around local Y to simulate breathing movement)
            var leftArmKeys = new List<Keyframe>
    {
        new Keyframe(0.0f, armDownL),
        new Keyframe(1.5f, armDownL * Quaternion.FromAxisAngle(Vector3.UnitY, 0.15f)), // slight breath forward
        new Keyframe(3.0f, armDownL)
    };

            var rightArmKeys = new List<Keyframe>
    {
        new Keyframe(0.0f, armDownR),
        new Keyframe(1.5f, armDownR * Quaternion.FromAxisAngle(Vector3.UnitY, -0.15f)),
        new Keyframe(3.0f, armDownR)
    };

            clip.Tracks[BoneType.UpperArmL] = new AnimationTrack(leftArmKeys);
            clip.Tracks[BoneType.UpperArmR] = new AnimationTrack(rightArmKeys);

            // 3. Subtle head idle nod/look around
            var headKeys = new List<Keyframe>
    {
        new Keyframe(0.0f, Quaternion.Identity),
        new Keyframe(1.0f, Quaternion.FromAxisAngle(new Vector3(0.2f, 1f, 0f), 0.35f)), // subtle head turn
        new Keyframe(2.0f, Quaternion.FromAxisAngle(new Vector3(0.2f, 1f, 0f), -0.35f)), // subtle head turn
        new Keyframe(3.0f, Quaternion.Identity)
    };
            clip.Tracks[BoneType.Head] = new AnimationTrack(headKeys);

            // NOTICE: We do NOT add tracks for the Chest or Legs in Idle! 
            // This ensures the legs stay 100% planted on the ground in their rest pose,
            // and the feet will never slide.

            return clip;
        }

        private static AnimationClip CreateSwordSwingClip()
        {
            var clip = new AnimationClip
            {
                Id = AnimationId.Swing,
                Duration = 1.0f // Fast, punchy attack duration
            };

            // Right arm starting resting position (hanging down)
            Quaternion armDownR = Quaternion.FromAxisAngle(Vector3.UnitZ, MathF.PI / 2f);

            // Upper Arm Swing Track (wind up, slash forward, follow through)
            var upperArmKeys = new List<Keyframe>
    {
        new Keyframe(0.0f, armDownR),
        new Keyframe(0.12f, armDownR * Quaternion.FromAxisAngle(Vector3.UnitX, -1.3f)), // Wind up / slash up
        new Keyframe(0.28f, armDownR * Quaternion.FromAxisAngle(Vector3.UnitX,  0.6f)), // Slash down / follow through
        new Keyframe(0.40f, armDownR)
    };
            clip.Tracks[BoneType.UpperArmR] = new AnimationTrack(upperArmKeys);

            // Lower Arm Elbow Snap
            var lowerArmKeys = new List<Keyframe>
    {
        new Keyframe(0.0f, Quaternion.Identity),
        new Keyframe(0.12f, Quaternion.FromAxisAngle(Vector3.UnitY, 0.5f)),
        new Keyframe(0.28f, Quaternion.FromAxisAngle(Vector3.UnitY, 1.0f)),
        new Keyframe(0.40f, Quaternion.Identity)
    };
            clip.Tracks[BoneType.LowerArmR] = new AnimationTrack(lowerArmKeys);

            return clip;
        }

        private static AnimationClip CreateRunClip()
        {
            var clip = new AnimationClip
            {
                Id = AnimationId.Run,
                Duration = 1.0f
            };

            float upperLegSize = 1.55f;
            float lowerLegSize = -0.4f;
            float swingAmplitude = 0.9f;
            float lowerArmBend = 0.9f;

            Quaternion armDownL = Quaternion.FromAxisAngle(Vector3.UnitZ, -MathF.PI / 2f);
            Quaternion armDownR = Quaternion.FromAxisAngle(Vector3.UnitZ, MathF.PI / 2f);
            Vector3 shoulderAxis = Vector3.UnitY;
            Vector3 elbowAxis = Vector3.UnitY;

            // --- Upper Arms ---
            clip.Tracks[BoneType.UpperArmL] = new AnimationTrack(new List<Keyframe>
            {
                new Keyframe(0.0f, armDownL * Quaternion.FromAxisAngle(shoulderAxis,  swingAmplitude)),
                new Keyframe(0.5f, armDownL * Quaternion.FromAxisAngle(shoulderAxis, -swingAmplitude)),
                new Keyframe(1.0f, armDownL * Quaternion.FromAxisAngle(shoulderAxis,  swingAmplitude))
            });

            clip.Tracks[BoneType.UpperArmR] = new AnimationTrack(new List<Keyframe>
            {
                new Keyframe(0.0f, armDownR * Quaternion.FromAxisAngle(shoulderAxis, swingAmplitude)),
                new Keyframe(0.5f, armDownR * Quaternion.FromAxisAngle(shoulderAxis, -swingAmplitude)),
                new Keyframe(1.0f, armDownR * Quaternion.FromAxisAngle(shoulderAxis, swingAmplitude))
            });

            // --- Lower Arms ---
            clip.Tracks[BoneType.LowerArmL] = new AnimationTrack(new List<Keyframe>
            {
                new Keyframe(0.0f, Quaternion.FromAxisAngle(elbowAxis, -0.4f)),
                new Keyframe(0.5f, Quaternion.FromAxisAngle(elbowAxis, -lowerArmBend)),
                new Keyframe(1.0f, Quaternion.FromAxisAngle(elbowAxis, -0.4f))
            });

            clip.Tracks[BoneType.LowerArmR] = new AnimationTrack(new List<Keyframe>
            {
                new Keyframe(0.0f, Quaternion.FromAxisAngle(elbowAxis,  lowerArmBend)),
                new Keyframe(0.5f, Quaternion.FromAxisAngle(elbowAxis,  0.4f)),
                new Keyframe(1.0f, Quaternion.FromAxisAngle(elbowAxis,  lowerArmBend))
            });

            // --- Legs ---
            clip.Tracks[BoneType.UpperLegL] = new AnimationTrack(new List<Keyframe>
            {
                new Keyframe(0.0f, Quaternion.FromAxisAngle(Vector3.UnitX, -upperLegSize)),
                new Keyframe(0.5f, Quaternion.FromAxisAngle(Vector3.UnitX,  upperLegSize)),
                new Keyframe(1.0f, Quaternion.FromAxisAngle(Vector3.UnitX, -upperLegSize))
            });

            clip.Tracks[BoneType.LowerLegL] = new AnimationTrack(new List<Keyframe>
            {
                new Keyframe(0.0f, Quaternion.FromAxisAngle(Vector3.UnitX, -lowerLegSize)),
                new Keyframe(0.5f, Quaternion.FromAxisAngle(Vector3.UnitX,  lowerLegSize)),
                new Keyframe(1.0f, Quaternion.FromAxisAngle(Vector3.UnitX, -lowerLegSize))
            });

            clip.Tracks[BoneType.UpperLegR] = new AnimationTrack(new List<Keyframe>
            {
                new Keyframe(0.0f, Quaternion.FromAxisAngle(Vector3.UnitX,  upperLegSize)),
                new Keyframe(0.5f, Quaternion.FromAxisAngle(Vector3.UnitX, -upperLegSize)),
                new Keyframe(1.0f, Quaternion.FromAxisAngle(Vector3.UnitX,  upperLegSize))
            });

            clip.Tracks[BoneType.LowerLegR] = new AnimationTrack(new List<Keyframe>
            {
                new Keyframe(0.0f, Quaternion.FromAxisAngle(Vector3.UnitX,  lowerLegSize)),
                new Keyframe(0.5f, Quaternion.FromAxisAngle(Vector3.UnitX, -lowerLegSize)),
                new Keyframe(1.0f, Quaternion.FromAxisAngle(Vector3.UnitX,  lowerLegSize))
            });

            return clip;
        }
    }
}
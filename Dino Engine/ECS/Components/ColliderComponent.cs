using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using System.Runtime.InteropServices;

namespace Dino_Engine.ECS.Components
{
    public enum ColliderType : byte
    {
        Sphere,
        Box,
        Cylinder
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct ColliderData
    {
        [FieldOffset(0)] public float Radius;               // For Sphere
        [FieldOffset(0)] public Vector3 HalfExtents;        // For Box
        [FieldOffset(0)] public Vector2 CylinderData;       // For Cylinder (X = Radius, Y = HalfHeight)
    }
    public struct ColliderComponent : IComponent
    {
        public ColliderType Type;
        public ColliderData Data;
        public Vector3 LocalOffset;
        public float Restitution; // Bounciness (0.0 to 1.0)
    }
}

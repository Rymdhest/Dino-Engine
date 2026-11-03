using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Physics;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    /// <summary>
    /// A body of still water: a puddle, a pond, a lake, a pool. It is a flat surface at the height of the entity's
    /// PositionComponent, bounded by <see cref="Shape"/> around that position (turned by the RotationComponent's yaw,
    /// if it has one). Water only shows where the ground is below the surface, so a body placed in a hollow takes the
    /// shape of the hollow by itself; the outline only has to enclose it (<see cref="WaterBasins"/> finds such hollows).
    /// For a puddle on flat ground, sit the surface a few centimetres above the ground and let the outline shape it.
    /// <para>Bodies are placed in world space; parenting is not supported. Grass and scattered objects avoid bodies
    /// that exist when their terrain chunk is generated.</para>
    /// </summary>
    public struct WaterBodyComponent : IComponent
    {
        public WaterShape Shape;
        public Vector2 HalfExtents;   // Metres along the body's local x and z.
        public float EdgeSoftness;    // Metres over which the water fades out inside the outline (0 = hard edge).
        public float EdgeNoise;       // Metres the outline is pushed in and out by noise, for organic puddle shapes.
        public float EdgeNoiseScale;  // Metres, size of those bumps.

        public WaterOptics Optics;
        public float WindExposure;    // 0..1, how much the wind ripples it: 1 = open water, small sheltered puddles less.
        public float EdgeFade;        // Metres of depth over which the water fades in at the shore.
        public float EdgeFoam;        // 0..1, a thin line of foam where the water meets the ground.

        public WaterBodyComponent(WaterShape shape, Vector2 halfExtents, WaterOptics optics)
        {
            Shape = shape;
            HalfExtents = halfExtents;
            EdgeSoftness = 0f;
            EdgeNoise = 0f;
            EdgeNoiseScale = 1f;
            Optics = optics;
            WindExposure = 1f;
            EdgeFade = 0.06f;
            EdgeFoam = 0f;
        }

        /// <summary>A pond or lake filling a hollow; the terrain around it draws the shoreline.</summary>
        public static WaterBodyComponent Pond(Vector2 halfExtents, WaterOptics optics)
        {
            return new WaterBodyComponent(WaterShape.Rectangle, halfExtents, optics)
            {
                WindExposure = 1f,
                EdgeFade = 0.08f,
                EdgeFoam = 0.15f,
            };
        }

        /// <summary>A shallow puddle with a soft, irregular outline.</summary>
        public static WaterBodyComponent Puddle(float radius)
        {
            return new WaterBodyComponent(WaterShape.Ellipse, new Vector2(radius), WaterOptics.Puddle)
            {
                EdgeSoftness = MathF.Min(0.25f, radius * 0.3f),
                EdgeNoise = radius * 0.3f,
                EdgeNoiseScale = MathF.Max(radius * 0.35f, 0.15f),
                WindExposure = 0.35f,
                EdgeFade = 0.015f,
                EdgeFoam = 0f,
            };
        }
    }
}

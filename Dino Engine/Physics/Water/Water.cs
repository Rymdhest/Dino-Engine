using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Rendering.Renderers.Geometry;
using OpenTK.Mathematics;

namespace Dino_Engine.Physics
{
    /// <summary>A splash waiting to be added to the ripple simulation (see <see cref="Water.AddSplash"/>).</summary>
    public struct WaterSplash
    {
        public Vector3 Position;
        public float Radius;    // Metres.
        public float Strength;  // Metres the surface is pushed down at the centre.
    }

    public struct WaterSample
    {
        /// <summary>The sampled point lies below a water surface.</summary>
        public bool InWater;
        /// <summary>There is water in this column at all (the ground is below some water surface).</summary>
        public bool HasWater;
        /// <summary>The water is the sea rather than a still water body.</summary>
        public bool IsOcean;
        /// <summary>World y of the calm surface: the body's level, or sea level. Waves move the visible surface around it.</summary>
        public float SurfaceHeight;
        /// <summary>World y of the visible ground under the water, or negative infinity without terrain.</summary>
        public float BedHeight;
        /// <summary>Metres of water above the ground.</summary>
        public float Depth => MathF.Max(SurfaceHeight - BedHeight, 0f);
    }

    /// <summary>
    /// Gameplay access to the engine's water (the world's <see cref="WaterSingleton"/> and its <see cref="WaterBodyComponent"/>s).
    /// Surfaces are the calm water levels; the moving waves on top are visual only (see WaterWaves.SwellElevation for the
    /// open-sea swell). Without a water singleton there is no water.
    /// </summary>
    public static class Water
    {
        public static bool TryGet(ECSWorld? world, out WaterSingleton water)
        {
            water = default;
            if (world == null || !world.TryGetSingleton<WaterSingleton>(out Entity entity)) return false;
            water = world.GetComponent<WaterSingleton>(entity);
            return true;
        }

        /// <summary>The water at <paramref name="position"/>'s column: the highest surface that has ground below it.</summary>
        public static WaterSample Sample(ECSWorld world, Vector3 position)
        {
            WaterQuery query = WaterQuery.Create(world);
            float bed = BedHeight(world, position.Xz);
            WaterSample sample = new WaterSample { SurfaceHeight = float.NegativeInfinity, BedHeight = bed };
            if (query.TryGetSurfaceAbove(new Vector3(position.X, bed, position.Z), 0f, out float surface, out bool ocean))
            {
                sample.HasWater = true;
                sample.IsOcean = ocean;
                sample.SurfaceHeight = surface;
                sample.InWater = surface > position.Y;
            }
            return sample;
        }

        /// <summary>Whether <paramref name="position"/> is under water by more than <paramref name="margin"/> metres.</summary>
        public static bool IsSubmerged(ECSWorld world, Vector3 position, float margin = 0f)
        {
            return WaterQuery.Create(world).TryGetSurfaceAbove(position, margin, out _, out _);
        }

        /// <summary>World y of the visible ground (the terrain surface the renderer draws), or negative infinity without terrain.</summary>
        public static float BedHeight(ECSWorld? world, Vector2 xz)
        {
            if (world == null || !world.TryGetSingleton<TerrainGeneratorSingleton>(out Entity entity)) return float.NegativeInfinity;
            var generator = world.GetComponent<TerrainGeneratorSingleton>(entity).Generator;
            return generator == null ? float.NegativeInfinity : generator.getHeightAt(xz) + TerrainRenderer.SurfaceOffset;
        }

        /// <summary>
        /// Sends a ring of ripples out from <paramref name="position"/> if it is on a water surface near the camera:
        /// <paramref name="strength"/> metres deep at the centre, <paramref name="radius"/> metres wide.
        /// </summary>
        public static void AddSplash(ECSWorld? world, Vector3 position, float radius, float strength)
        {
            if (radius <= 0f || strength == 0f || world == null || !world.TryGetSingleton<WaterSingleton>(out Entity entity)) return;
            var water = world.GetComponent<WaterSingleton>(entity);
            if (water.Splashes == null) return;
            water.Splashes.Add(new WaterSplash { Position = position, Radius = radius, Strength = strength });
        }
    }

    /// <summary>
    /// A snapshot of where the water is, for asking many questions at once (placing vegetation, masking grass).
    /// Take a new one when water bodies change.
    /// </summary>
    public sealed class WaterQuery
    {
        private struct Body
        {
            public WaterBodyComponent Component;
            public Vector3 Center;
            public Vector2 Axis;
            public float Reach;   // Radius around the centre outside which the body certainly covers nothing.
        }

        private static readonly BitMask BodyMask = new BitMask(typeof(WaterBodyComponent), typeof(PositionComponent));

        private readonly List<Body> _bodies = new();
        private bool _ocean;
        private float _seaLevel;

        public bool IsEmpty => !_ocean && _bodies.Count == 0;

        public static WaterQuery Create(ECSWorld? world)
        {
            var query = new WaterQuery();
            if (world == null || !Water.TryGet(world, out WaterSingleton water) || !water.Enabled) return query;

            query._ocean = water.OceanEnabled;
            query._seaLevel = water.SeaLevel;
            foreach (var archetype in world.QueryArchetypes(BodyMask, BitMask.Empty))
            {
                var bodies = archetype.GetComponentArray<WaterBodyComponent>();
                var positions = archetype.GetComponentArray<PositionComponent>();
                var rotations = archetype.GetComponentArray<RotationComponent>();
                for (int i = 0; i < archetype.EntityCount; i++)
                {
                    WaterBodyComponent body = bodies![i];
                    Vector2 quad = WaterShapes.QuadHalfExtents(body);
                    query._bodies.Add(new Body
                    {
                        Component = body,
                        Center = positions![i].value,
                        Axis = rotations != null ? WaterShapes.AxisFromRotation(rotations[i].quaternion) : Vector2.UnitX,
                        Reach = quad.Length,
                    });
                }
            }
            return query;
        }

        /// <summary>
        /// The highest calm water surface over <paramref name="point"/>'s column that lies more than
        /// <paramref name="margin"/> metres above the point. For a point on the ground this says whether it is under water.
        /// </summary>
        public bool TryGetSurfaceAbove(Vector3 point, float margin, out float surface, out bool ocean)
        {
            surface = float.NegativeInfinity;
            ocean = false;
            float threshold = point.Y + margin;
            if (_ocean && _seaLevel > threshold)
            {
                surface = _seaLevel;
                ocean = true;
            }

            Vector2 xz = point.Xz;
            for (int i = 0; i < _bodies.Count; i++)
            {
                Body body = _bodies[i];
                if (body.Center.Y <= threshold || body.Center.Y <= surface) continue;
                Vector2 offset = xz - body.Center.Xz;
                if (offset.LengthSquared > body.Reach * body.Reach) continue;
                if (WaterShapes.Coverage(body.Component, body.Center.Xz, body.Axis, xz) < 0.5f) continue;
                surface = body.Center.Y;
                ocean = false;
            }
            return surface > float.NegativeInfinity;
        }

        /// <summary>Water depth over a point on the ground, 0 where it is dry.</summary>
        public float DepthAt(Vector3 groundPoint)
        {
            return TryGetSurfaceAbove(groundPoint, 0f, out float surface, out _) ? surface - groundPoint.Y : 0f;
        }
    }
}

using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Physics;
using Dino_Engine.Rendering.Renderers.Geometry;
using Dino_Engine.Rendering.Renderers.PosGeometry;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    /// <summary>
    /// Advances the water (<see cref="WaterSingleton"/>): builds the sea up and down with the wind, moves the waves and
    /// the ripple patterns along, finds the visible still water and the terrain chunks that can hold sea, turns waders
    /// and splashes into ripples, and hands the frame's water to the <see cref="WaterRenderer"/>. Runs after WindSystem.
    /// </summary>
    public class WaterSystem : SystemBase
    {
        // Frame times above this are treated as hitches, so a stall doesn't fling the waves ahead.
        private const float MaxStep = 0.1f;
        // Metres parallax mapping can show the ground below its mesh; a chunk may hold sea down to this much lower.
        private const float ParallaxReliefMargin = 0.5f;
        // How far a splash may be from the surface it disturbs, metres.
        private const float SplashReach = 2f;

        private static readonly BitMask BodyMask = new BitMask(typeof(WaterBodyComponent), typeof(PositionComponent));
        private static readonly BitMask DisturberMask = new BitMask(typeof(WaterDisturberComponent), typeof(PositionComponent));

        private readonly List<WaterBodyRenderData> _bodies = new();
        private readonly List<Vector4> _oceanQuads = new();
        private readonly List<TerrainChunkRenderData> _shoreChunks = new();
        private readonly List<WaterContact> _rates = new();
        private readonly List<WaterContact> _impulses = new();
        private Dictionary<uint, (Vector3 position, bool inWater)> _waders = new();
        private Dictionary<uint, (Vector3 position, bool inWater)> _wadersNext = new();
        private readonly float[] _swellAmplitudes = new float[WaterWaves.SwellCount];
        // The shore field's square (ChooseShoreSquare).
        private Vector2 _shoreCorner;
        private float _shoreSize;
        private bool _shoreSquare;

        public WaterSystem()
            : base(new BitMask(typeof(WaterSingleton)))
        {
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            var water = entity.Get<WaterSingleton>();
            water.Splashes ??= new List<WaterSplash>();
            float dt = Math.Clamp(deltaTime, 0f, MaxStep);
            Advance(ref water, Wind.MeanVelocity(world), dt);

            if (!water.Enabled || !world.Camera.IsValid())
            {
                water.Splashes.Clear();
                _waders.Clear();
                entity.Set(water);
                return;
            }

            Vector3 cameraPosition = world.GetComponent<LocalToWorldMatrixComponent>(world.Camera).value.ExtractTranslation();
            Matrix4 view = world.GetComponent<ViewMatrixComponent>(world.Camera).value;
            var projection = world.GetComponent<PerspectiveProjectionComponent>(world.Camera);
            Frustum frustum = new Frustum(view * projection.ProjectionMatrix);

            WaterWaves.SwellAmplitudes(water, _swellAmplitudes);
            float swellSum = 0f;
            foreach (float a in _swellAmplitudes) swellSum += a;
            float breaker = WaterWaves.ShoreBreakerHeight(water);
            float crest = breaker * WaterWaves.AlongShoreMax;
            // The swash climbs at most MaxRunUpHeight wave heights, as a sheet at most 0.12 m thick (Water_Ocean.frag).
            float runUp = crest * WaterWaves.MaxRunUpHeight * MathF.Max(water.RunUp, 0f) + 0.12f;
            Vector2 slab = new Vector2(
                water.SeaLevel + MathF.Max(crest, runUp) + swellSum + 0.05f,
                water.SeaLevel - (crest * 0.45f + swellSum + 0.05f));

            CollectBodies(world, frustum);
            long shoreSignature = CollectOcean(world, water, frustum, cameraPosition, projection.Far, slab, out Vector2 shoreCorner, out float shoreSize);

            WaterQuery query = WaterQuery.Create(world);
            _rates.Clear();
            _impulses.Clear();
            CollectWaders(world, query, deltaTime);
            CollectSplashes(water, query, world, dt);
            water.Splashes.Clear();

            Engine.RenderEngine._waterRenderer.SubmitCommand(new WaterRenderCommand
            {
                settings = water,
                bodies = _bodies.ToArray(),
                oceanQuads = _oceanQuads.ToArray(),
                oceanSlab = slab,
                breakerHeight = breaker,
                swellAmplitudesA = new Vector4(_swellAmplitudes[0], _swellAmplitudes[1], _swellAmplitudes[2], _swellAmplitudes[3]),
                swellAmplitudesB = new Vector4(_swellAmplitudes[4], _swellAmplitudes[5], _swellAmplitudes[6], _swellAmplitudes[7]),
                whitecaps = Whitecaps(water, _swellAmplitudes),
                shoreChunks = _shoreChunks.ToArray(),
                shoreCorner = shoreCorner,
                shoreSize = shoreSize,
                shoreSignature = shoreSignature,
                rippleCenter = RegionCenter(cameraPosition, view),
                rippleRates = _rates.ToArray(),
                rippleImpulses = _impulses.ToArray(),
                deltaTime = deltaTime,
            });

            entity.Set(water);
        }

        // ---------------------------------------------------------------- live state

        private static void Advance(ref WaterSingleton water, Vector2 wind, float dt)
        {
            float speed = wind.Length;
            if (!water.Initialized)
            {
                water.SeaState = speed;
                water.SeaDirection = speed > 0.1f ? wind / speed : Vector2.UnitY;
                water.Initialized = true;
            }
            else
            {
                // The sea takes time to build up and to calm down; its direction swings round just as slowly.
                float blend = 1f - MathF.Exp(-dt / MathF.Max(water.SeaStateResponseTime, 1e-3f));
                water.SeaState += (speed - water.SeaState) * blend;
                if (speed > 0.1f)
                {
                    Vector2 direction = water.SeaDirection + (wind / speed - water.SeaDirection) * blend;
                    if (direction.LengthSquared > 1e-8f) water.SeaDirection = direction.Normalized();
                }
            }

            // The phase wraps at a set, and the sets are counted, so the shaders can tell one wave from another (Water/ShoreData.glsl).
            float shorePhase = water.ShorePhase + dt / MathF.Max(water.ShoreWavePeriod, 0.5f);
            int setsDone = (int)MathF.Floor(shorePhase / WaterWaves.SetLength);
            water.ShorePhase = Wrap(shorePhase, WaterWaves.SetLength);
            water.ShoreSets = ((water.ShoreSets + setsDone) % WaterWaves.SetsCounted + WaterWaves.SetsCounted) % WaterWaves.SetsCounted;
            for (int i = 0; i < WaterWaves.SwellCount; i++)
            {
                float phase = Wrap(WaterWaves.SwellPhase(water, i) + dt * WaterWaves.SwellAngularFrequency(i) / MathF.Tau, 1f);
                if (i < 4) water.SwellPhasesA[i] = phase;
                else water.SwellPhasesB[i - 4] = phase;
            }

            // Patterns are moved by subtracting from their offsets, which carries them along the velocity. Each layer drifts
            // with the wind in a slightly different direction and animates as fast as its longest waves would run.
            Vector2 drift = wind * MathF.Max(water.RippleDrift, 0f);
            water.RippleOffsetA = Wrap(water.RippleOffsetA - drift * (dt / WaterRenderer.RippleTileA));
            water.RippleOffsetB = Wrap(water.RippleOffsetB - Rotate(drift, 0.35f) * (1.25f * dt / WaterRenderer.RippleTileB));
            water.RippleTimeA = Wrap(water.RippleTimeA + dt / RippleLoopSeconds(WaterRenderer.RippleTileA), 1f);
            water.RippleTimeB = Wrap(water.RippleTimeB + dt / RippleLoopSeconds(WaterRenderer.RippleTileB), 1f);
            water.CausticOffsetA = Wrap(water.CausticOffsetA - Rotate(drift, 0.2f) * (dt / WaterRenderer.CausticTile));
            water.CausticOffsetB = Wrap(water.CausticOffsetB - Rotate(drift, -0.5f) * (1.3f * dt / WaterRenderer.CausticTile));
            // Foam rides the surface current, about 3% of the wind speed.
            water.FoamOffset = Wrap(water.FoamOffset - wind * (0.03f * dt / WaterRenderer.FoamTile));
            water.ShoreFoamOffset = Wrap(water.ShoreFoamOffset / WaterRenderer.ShoreFoamTile - wind * (0.03f * dt / WaterRenderer.ShoreFoamTile)) * WaterRenderer.ShoreFoamTile;
        }

        /// <summary>Seconds one loop of the ripple texture lasts at a tile size: one period of its longest waves.</summary>
        private static float RippleLoopSeconds(float tileSize)
        {
            float wavenumber = MathF.Tau * WaterTextureBaker.RippleMinCycles / tileSize;
            return MathF.Tau / WaterWaves.AngularFrequency(wavenumber);
        }

        private static Vector2 Whitecaps(in WaterSingleton water, float[] amplitudes)
        {
            float deviation = WaterWaves.StandardDeviation(amplitudes);
            float coverage = WaterWaves.WhitecapCoverage(water.SeaState) * MathF.Max(water.Whitecaps, 0f);
            if (deviation < 1e-4f || coverage < 1e-6f) return new Vector2(1e4f, 0f);
            return new Vector2(WaterWaves.GaussianExceedance(coverage), 1f / deviation);
        }

        // ---------------------------------------------------------------- what to draw

        private void CollectBodies(ECSWorld world, Frustum frustum)
        {
            _bodies.Clear();
            foreach (var archetype in world.QueryArchetypes(BodyMask, BitMask.Empty))
            {
                var bodies = archetype.GetComponentArray<WaterBodyComponent>()!;
                var positions = archetype.GetComponentArray<PositionComponent>()!;
                var rotations = archetype.GetComponentArray<RotationComponent>();
                for (int i = 0; i < archetype.EntityCount; i++)
                {
                    WaterBodyComponent body = bodies[i];
                    Vector3 centre = positions[i].value;
                    Vector2 axis = rotations != null ? WaterShapes.AxisFromRotation(rotations[i].quaternion) : Vector2.UnitX;
                    Vector2 quad = WaterShapes.QuadHalfExtents(body);
                    Vector2 extents = new Vector2(
                        MathF.Abs(axis.X) * quad.X + MathF.Abs(axis.Y) * quad.Y,
                        MathF.Abs(axis.Y) * quad.X + MathF.Abs(axis.X) * quad.Y);
                    Vector3 min = new Vector3(centre.X - extents.X, centre.Y - 0.5f, centre.Z - extents.Y);
                    Vector3 max = new Vector3(centre.X + extents.X, centre.Y + 0.05f, centre.Z + extents.Y);
                    if (frustum.IntersectsAABB(new AABB(min, max)) == IntersectionResult.Outside) continue;
                    _bodies.Add(new WaterBodyRenderData { body = body, centre = centre, axis = axis, boundsMin = min, boundsMax = max });
                }
            }
        }

        /// <summary>
        /// Finds the terrain the shore field captures (still water reads the ground's height from it too) and the visible
        /// chunks whose ground dips low enough for the sea. Returns a signature that changes whenever the shore field has
        /// to be rebuilt.
        /// </summary>
        private long CollectOcean(ECSWorld world, in WaterSingleton water, Frustum frustum, Vector3 cameraPosition, float farPlane, Vector2 slab,
            out Vector2 shoreCorner, out float shoreSize)
        {
            _oceanQuads.Clear();
            _shoreChunks.Clear();
            shoreCorner = Vector2.Zero;
            shoreSize = 0f;

            if (!world.TryGetSingleton<TerrainQuadTreeSingleton>(out Entity terrainEntity))
            {
                // No terrain: open sea all the way to the far plane.
                float reach = MathF.Max(farPlane, 1f);
                if (water.OceanEnabled) _oceanQuads.Add(new Vector4(cameraPosition.X - reach, cameraPosition.Z - reach, 2f * reach, 2f * reach));
                return 1;
            }

            var terrain = world.GetComponent<TerrainQuadTreeSingleton>(terrainEntity);
            float chunkSize = terrain.ChunkSize;
            if (terrain.Chunks.Count == 0 || !(chunkSize > 0f)) return 2;

            Vector2i minCell = new Vector2i(int.MaxValue), maxCell = new Vector2i(int.MinValue);
            foreach (Vector2i cell in terrain.Chunks.Keys)
            {
                minCell = Vector2i.ComponentMin(minCell, cell);
                maxCell = Vector2i.ComponentMax(maxCell, cell);
            }
            ChooseShoreSquare(water, terrain, minCell, maxCell);
            shoreCorner = _shoreCorner;
            shoreSize = _shoreSize;
            Vector2 shoreFar = shoreCorner + new Vector2(shoreSize);

            unchecked
            {
                long signature = 17;
                signature = signature * 31 + BitConverter.SingleToInt32Bits(shoreCorner.X);
                signature = signature * 31 + BitConverter.SingleToInt32Bits(shoreCorner.Y);
                signature = signature * 31 + BitConverter.SingleToInt32Bits(shoreSize);
                signature = signature * 31 + BitConverter.SingleToInt32Bits(water.SeaLevel);
                signature = signature * 31 + BitConverter.SingleToInt32Bits(chunkSize);
                long chunkHash = 0;

                foreach (var (cell, chunkEntity) in terrain.Chunks)
                {
                    if (!world.HasComponent<TerrainChunkComponent>(chunkEntity)) continue;
                    var chunk = world.GetComponent<TerrainChunkComponent>(chunkEntity);
                    // Same placement TerrainChunkSystem gives the chunk.
                    Vector2 origin = new Vector2(cell.X, cell.Y) * chunkSize;
                    var renderData = new TerrainChunkRenderData
                    {
                        chunkPos = new Vector3(origin.X, 0f, origin.Y),
                        size = new Vector3(chunkSize, 1f, chunkSize),
                        arrayID = chunk.normalHeightTextureArrayID,
                    };

                    bool inShoreField = origin.X < shoreFar.X && origin.Y < shoreFar.Y && origin.X + chunkSize > shoreCorner.X && origin.Y + chunkSize > shoreCorner.Y;
                    if (inShoreField)
                    {
                        _shoreChunks.Add(renderData);
                        chunkHash += (((long)cell.X * 73856093) ^ ((long)cell.Y * 19349663) ^ ((long)chunk.normalHeightTextureArrayID * 83492791)) | 1;
                    }

                    if (!water.OceanEnabled) continue;
                    float lowest = LowestHeight(chunk) + TerrainRenderer.SurfaceOffset - ParallaxReliefMargin;
                    if (lowest >= slab.X) continue;
                    var bounds = new AABB(new Vector3(origin.X, lowest, origin.Y), new Vector3(origin.X + chunkSize, slab.X, origin.Y + chunkSize));
                    if (frustum.IntersectsAABB(bounds) == IntersectionResult.Outside) continue;
                    _oceanQuads.Add(new Vector4(origin.X, origin.Y, chunkSize, chunkSize));
                }
                return signature * 31 + chunkHash;
            }
        }

        /// <summary>
        /// Picks the shore field's square: big enough for the whole square of chunks the terrain keeps and the slack it moves in, on a grid fixed to the world
        /// in steps of <see cref="WaterShoreField.MoveMetres"/>, and moved only when what's loaded no longer fits in it, so a new build finds every texel where
        /// the last had it (WaterShoreField). Laid over the loaded chunks as they happened to be, every crossing of a chunk moved it by a fraction of a texel.
        /// </summary>
        private void ChooseShoreSquare(in WaterSingleton water, in TerrainQuadTreeSingleton terrain, Vector2i minCell, Vector2i maxCell)
        {
            float chunkSize = terrain.ChunkSize;
            float ring = (2 * terrain.Radius + 1) * chunkSize;
            // In whole lots of 48 m, so that a texel is a whole sixteenth of a metre, exact in a float.
            float wanted = MathF.Ceiling(ring * WaterShoreField.Resolution / (WaterShoreField.Resolution - WaterShoreField.MoveTexels) / 48f) * 48f;
            float size = MathF.Max(MathF.Min(wanted, water.MaxShoreFieldSize), chunkSize);
            float step = WaterShoreField.MoveMetres(size);

            // What it has to hold: every loaded chunk, as far as a square that fits in it with room to move allows.
            Vector2 low = new Vector2(minCell.X, minCell.Y) * chunkSize;
            Vector2 high = new Vector2(maxCell.X + 1, maxCell.Y + 1) * chunkSize;
            Vector2 centre = (low + high) * 0.5f;
            Vector2 half = new Vector2((size - step) * 0.5f);
            low = Vector2.ComponentMax(low, centre - half);
            high = Vector2.ComponentMin(high, centre + half);

            bool fits = _shoreSquare && _shoreSize == size
                && low.X >= _shoreCorner.X && low.Y >= _shoreCorner.Y && high.X <= _shoreCorner.X + size && high.Y <= _shoreCorner.Y + size;
            if (fits) return;
            Vector2 middle = (low + high) * 0.5f;
            _shoreCorner = new Vector2(MathF.Round((middle.X - size * 0.5f) / step) * step, MathF.Round((middle.Y - size * 0.5f) / step) * step);
            _shoreSize = size;
            _shoreSquare = true;
        }

        private static float LowestHeight(in TerrainChunkComponent chunk)
        {
            if (chunk.heightGrid == null) return float.NegativeInfinity;
            float lowest = float.MaxValue;
            foreach (float height in chunk.heightGrid.Values) lowest = MathF.Min(lowest, height);
            return lowest;
        }

        // ---------------------------------------------------------------- ripples

        /// <summary>Waders push the water while they move through it, and send a ring out as they step in.</summary>
        private void CollectWaders(ECSWorld world, WaterQuery query, float deltaTime)
        {
            _wadersNext.Clear();
            if (!query.IsEmpty)
            {
                foreach (var archetype in world.QueryArchetypes(DisturberMask, BitMask.Empty))
                {
                    var disturbers = archetype.GetComponentArray<WaterDisturberComponent>()!;
                    var positions = archetype.GetComponentArray<PositionComponent>()!;
                    var matrices = archetype.GetComponentArray<LocalToWorldMatrixComponent>();
                    for (int i = 0; i < archetype.EntityCount; i++)
                    {
                        WaterDisturberComponent disturber = disturbers[i];
                        Vector3 position = matrices != null ? matrices[i].value.ExtractTranslation() : positions[i].value;
                        uint id = archetype.entities[i].Id;

                        // In water while the surface lies between its feet and the top of its body.
                        bool inWater = query.TryGetSurfaceAbove(position, -0.05f, out float surface, out _) && surface < position.Y + disturber.Height;
                        _wadersNext[id] = (position, inWater);
                        if (!inWater) continue;

                        bool known = _waders.TryGetValue(id, out var previous);
                        Vector2 center = position.Xz;
                        if (!known || !previous.inWater)
                        {
                            _impulses.Add(new WaterContact { Center = center, Radius = disturber.Radius * 1.5f, Amount = disturber.Strength * 3f });
                        }
                        if (known && deltaTime > 1e-5f)
                        {
                            float speed = (position.Xz - previous.position.Xz).Length / deltaTime;
                            if (speed > 0.05f)
                            {
                                _rates.Add(new WaterContact { Center = center, Radius = disturber.Radius, Amount = disturber.Strength * MathF.Min(speed, 20f) });
                            }
                        }
                    }
                }
            }
            (_waders, _wadersNext) = (_wadersNext, _waders);
        }

        /// <summary>Splashes from Water.AddSplash, and wind blasts going off over water.</summary>
        private void CollectSplashes(in WaterSingleton water, WaterQuery query, ECSWorld world, float dt)
        {
            if (query.IsEmpty) return;
            foreach (WaterSplash splash in water.Splashes)
            {
                if (!NearSurface(query, splash.Position)) continue;
                _impulses.Add(new WaterContact { Center = splash.Position.Xz, Radius = splash.Radius, Amount = splash.Strength });
            }

            // WindSystem ages each blast by the same clamped step before this runs, so a blast is new exactly once.
            if (!Wind.TryGet(world, out WindSingleton wind) || wind.Blasts == null || dt <= 0f) return;
            foreach (WindEmitter blast in wind.Blasts)
            {
                if (blast.Age <= 0f || blast.Age > dt * 1.0001f || !NearSurface(query, blast.Position)) continue;
                _impulses.Add(new WaterContact { Center = blast.Position.Xz, Radius = MathF.Max(blast.Radius * 0.2f, 0.5f), Amount = blast.Strength * 0.004f });
            }
        }

        private static bool NearSurface(WaterQuery query, Vector3 position)
        {
            return query.TryGetSurfaceAbove(position - new Vector3(0f, SplashReach, 0f), 0f, out float surface, out _)
                && surface < position.Y + SplashReach;
        }

        /// <summary>A little ahead of the camera, where most of a top-down view is (as WindSystem does).</summary>
        private static Vector2 RegionCenter(Vector3 cameraPosition, Matrix4 view)
        {
            Matrix4 invView = Matrix4.Invert(view);
            Vector3 forward = (new Vector4(0f, 0f, -1f, 0f) * invView).Xyz.Normalized();
            return cameraPosition.Xz + new Vector2(forward.X, forward.Z) * (WaterRippleSimulation.Size * 0.25f);
        }

        // ---------------------------------------------------------------- helpers

        private static float Wrap(float value, float period) => value - period * MathF.Floor(value / period);

        private static Vector2 Wrap(Vector2 value) => new Vector2(value.X - MathF.Floor(value.X), value.Y - MathF.Floor(value.Y));

        private static Vector2 Rotate(Vector2 v, float radians)
        {
            float c = MathF.Cos(radians), s = MathF.Sin(radians);
            return new Vector2(c * v.X - s * v.Y, s * v.X + c * v.Y);
        }
    }
}

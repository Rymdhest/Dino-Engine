using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Procedural.Terrain;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;

namespace Dino_Engine.ECS.Systems
{
    public class PhysicsSystem : SystemBase
    {
        // --- 1. DATA-ORIENTED COLLECTIONS ---
        // A flat, contiguous array of all moving bodies for this specific frame.
        private List<BodyState> _dynamicBodies = new();
        private List<ContactManifold> _manifolds = new();

        // Spatial partitioning for static geometry (Trees, Buildings)
        private SpatialHashGrid _staticGrid = new SpatialHashGrid(50f);
        private int _cachedStaticCount = -1;
        private readonly BitMask _staticMask;
        private readonly BitMask _staticExcludeMask;

        // The PhysicsSystem naturally iterates over Dynamic bodies using the base class mask
        public PhysicsSystem()
            : base(new BitMask(typeof(PositionComponent), typeof(ColliderComponent), typeof(VelocityComponent), typeof(MassComponent)))
        {
            // Static bodies are defined as having a Position and Collider, but explicitly NO Mass
            _staticMask = new BitMask(typeof(PositionComponent), typeof(ColliderComponent));
            _staticExcludeMask = new BitMask(typeof(MassComponent));
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            throw new NotImplementedException();
        }

        internal override void UpdateInternal(ECSWorld world, float deltaTime)
        {
            _dynamicBodies.Clear();
            _manifolds.Clear();

            // --- PHASE 1: EVENT BUFFER & TERRAIN PREP ---
            CollisionEventBufferComponent eventBuffer = default;
            bool hasEventBuffer = false;
            var eventSingleton = world.GetSingleton<CollisionEventBufferComponent>();
            if (eventSingleton.IsValid())
            {
                eventBuffer = world.GetComponent<CollisionEventBufferComponent>(eventSingleton);
                eventBuffer.Events.Clear();
                hasEventBuffer = true;
            }

            var genEntity = world.GetSingleton<TerrainGeneratorComponent>();
            TerrainGenerator generator = genEntity.IsValid() ? world.GetComponent<TerrainGeneratorComponent>(genEntity).Generator : null;


            // --- PHASE 2: STATIC GRID MAINTENANCE ---
            int currentStaticCount = 0;
            foreach (var archetype in world.QueryArchetypes(_staticMask, _staticExcludeMask))
                currentStaticCount += archetype.entities.Count;

            // Rebuild spatial partition only if the environment structurally changes
            if (currentStaticCount != _cachedStaticCount)
            {
                _staticGrid.Clear();
                foreach (var archetype in world.QueryArchetypes(_staticMask, _staticExcludeMask))
                {
                    foreach (var entity in new ComponentAccessor(archetype))
                    {
                        _staticGrid.Add(new StaticCollider
                        {
                            Entity = entity,
                            Position = entity.Get<PositionComponent>().value,
                            Collider = entity.Get<ColliderComponent>()
                        });
                    }
                }
                _cachedStaticCount = currentStaticCount;
            }


            // --- PHASE 3: DYNAMIC DATA EXTRACTION (Respecting SystemBase) ---
            // We use the system's inherent WithMask/WithoutMask to gather active physical bodies
            foreach (var archetype in world.QueryArchetypes(WithMask, WithoutMask))
            {
                foreach (var entity in new ComponentAccessor(archetype))
                {
                    _dynamicBodies.Add(new BodyState
                    {
                        Entity = entity,
                        Position = entity.Get<PositionComponent>().value,
                        Velocity = entity.Get<VelocityComponent>().value,
                        InverseMass = 1f / entity.Get<MassComponent>().value,
                        Collider = entity.Get<ColliderComponent>()
                    });
                }
            }


            // --- PHASE 4: INTEGRATION & BROADPHASE ---
            for (int i = 0; i < _dynamicBodies.Count; i++)
            {
                var bodyA = _dynamicBodies[i];

                // 1. Integration (Move the body)
                bodyA.Position += bodyA.Velocity * deltaTime;
                _dynamicBodies[i] = bodyA; // Save integrated position back for narrowphase

                // 2. Terrain Collision (Generates a manifold just like any other object)
                if (generator != null)
                {
                    float terrainHeight = generator.getHeightAt(new Vector2(bodyA.Position.X, bodyA.Position.Z));
                    float radius = bodyA.Collider.Type == ColliderType.Sphere ? bodyA.Collider.Data.Radius : 0.5f;

                    if (bodyA.Position.Y - radius < terrainHeight)
                    {
                        _manifolds.Add(new ContactManifold
                        {
                            BodyIndexA = i,
                            BodyIndexB = -1, // -1 signals the solver to use infinite mass
                            EntityA = bodyA.Entity,
                            EntityB = default, // Terrain has no entity view
                            // FIX: Normal must point from A (Body) to B (Terrain), so it must point DOWN
                            Normal = -generator.GetNormalAt(bodyA.Position.X, bodyA.Position.Z),
                            Penetration = (terrainHeight + radius) - bodyA.Position.Y,
                            Restitution = bodyA.Collider.Restitution,
                            ContactPoint = new Vector3(bodyA.Position.X, terrainHeight, bodyA.Position.Z)
                        });
                    }
                }

                // 3. Dynamic vs Dynamic
                for (int j = i + 1; j < _dynamicBodies.Count; j++)
                {
                    var bodyB = _dynamicBodies[j];
                    ContactManifold m = Dispatch(bodyA.Position, bodyA.Collider, bodyB.Position, bodyB.Collider);
                    if (m.IsColliding)
                    {
                        m.BodyIndexA = i;
                        m.BodyIndexB = j;
                        m.EntityA = bodyA.Entity;
                        m.EntityB = bodyB.Entity;
                        m.Restitution = MathF.Min(bodyA.Collider.Restitution, bodyB.Collider.Restitution);
                        _manifolds.Add(m);
                    }
                }

                // 4. Dynamic vs Static Trees
                foreach (var staticCol in _staticGrid.QueryNearby(bodyA.Position))
                {
                    ContactManifold m = Dispatch(bodyA.Position, bodyA.Collider, staticCol.Position, staticCol.Collider);
                    if (m.IsColliding)
                    {
                        m.BodyIndexA = i;
                        m.BodyIndexB = -1; // -1 signals the solver to use infinite mass
                        m.EntityA = bodyA.Entity;
                        m.EntityB = staticCol.Entity;
                        m.Restitution = MathF.Min(bodyA.Collider.Restitution, staticCol.Collider.Restitution);
                        _manifolds.Add(m);
                    }
                }
            }


            // --- PHASE 5: THE UNIFIED SOLVER ---
            ResolveCollisions();


            // --- PHASE 6: WRITE BACK TO ECS ---
            foreach (var body in _dynamicBodies)
            {
                var posComponent = body.Entity.Get<PositionComponent>();
                posComponent.value = body.Position;
                body.Entity.Set(posComponent);

                var velComponent = body.Entity.Get<VelocityComponent>();
                velComponent.value = body.Velocity;
                body.Entity.Set(velComponent);
            }


            // --- PHASE 7: EMIT COLLISION EVENTS ---
            if (hasEventBuffer)
            {
                foreach (var m in _manifolds)
                {
                    Vector3 velA = _dynamicBodies[m.BodyIndexA].Velocity;
                    Vector3 velB = m.BodyIndexB >= 0 ? _dynamicBodies[m.BodyIndexB].Velocity : Vector3.Zero;

                    eventBuffer.Events.Add(new CollisionEvent
                    {
                        EntityA = m.EntityA,
                        EntityB = m.EntityB,
                        ContactPoint = m.ContactPoint,
                        Normal = m.Normal,
                        ImpactForce = MathF.Abs(Vector3.Dot(velB - velA, m.Normal))
                    });
                }
            }
        }

        // --- THE UNIFIED SOLVER ---
        // Notice there are NO boolean flags here. 
        // A -1 index purely means "this object has 0 velocity and 0 inverse mass".
        private void ResolveCollisions()
        {
            const float PositionalCorrectionPercent = 0.2f;
            const float PositionalCorrectionSlop = 0.01f;

            foreach (var m in _manifolds)
            {
                var a = _dynamicBodies[m.BodyIndexA];

                // Fetch B properties, falling back to static/infinite mass if Index is -1
                Vector3 bVelocity = m.BodyIndexB >= 0 ? _dynamicBodies[m.BodyIndexB].Velocity : Vector3.Zero;
                float bInverseMass = m.BodyIndexB >= 0 ? _dynamicBodies[m.BodyIndexB].InverseMass : 0f;

                Vector3 relativeVelocity = bVelocity - a.Velocity;
                float velAlongNormal = Vector3.Dot(relativeVelocity, m.Normal);

                // Do not resolve if velocities are separating
                // This check dictates that m.Normal MUST point from A to B
                if (velAlongNormal > 0) continue;

                float totalInverseMass = a.InverseMass + bInverseMass;
                if (totalInverseMass == 0) continue;

                // 1. Calculate Impulse
                float j = -(1.0f + m.Restitution) * velAlongNormal;
                j /= totalInverseMass;
                Vector3 impulse = m.Normal * j;

                // 2. Apply Impulse
                a.Velocity -= impulse * a.InverseMass;

                // Optional: Simplified Friction for objects hitting the static world
                if (m.BodyIndexB == -1)
                {
                    Vector3 tangentVel = a.Velocity - Vector3.Dot(a.Velocity, m.Normal) * m.Normal;
                    a.Velocity -= tangentVel * 0.05f;
                }

                // 3. Positional Correction
                Vector3 correction = m.Normal * (MathF.Max(m.Penetration - PositionalCorrectionSlop, 0.0f) / totalInverseMass * PositionalCorrectionPercent);
                a.Position -= correction * a.InverseMass;

                // Write 'a' back to array
                _dynamicBodies[m.BodyIndexA] = a;

                // Write 'b' back to array (only if it actually exists in the dynamic array)
                if (m.BodyIndexB >= 0)
                {
                    var b = _dynamicBodies[m.BodyIndexB];
                    b.Velocity += impulse * bInverseMass;
                    b.Position += correction * bInverseMass;
                    _dynamicBodies[m.BodyIndexB] = b;
                }
            }
        }

        // --- DISPATCHER & NARROWPHASE MATH ---
        private ContactManifold Dispatch(Vector3 posA, ColliderComponent colA, Vector3 posB, ColliderComponent colB)
        {
            if (colA.Type == ColliderType.Sphere && colB.Type == ColliderType.Sphere)
                return CheckSphereVsSphere(posA, colA, posB, colB);

            if (colA.Type == ColliderType.Sphere && colB.Type == ColliderType.Cylinder)
                return CheckSphereVsCylinder(posA, colA, posB, colB);

            if (colA.Type == ColliderType.Cylinder && colB.Type == ColliderType.Sphere)
            {
                var m = CheckSphereVsCylinder(posB, colB, posA, colA);
                m.Normal = -m.Normal; // Flip normal since we swapped A and B
                return m;
            }

            return new ContactManifold { IsColliding = false };
        }

        private ContactManifold CheckSphereVsCylinder(Vector3 spherePos, ColliderComponent sphereCol, Vector3 cylPos, ColliderComponent cylCol)
        {
            // Apply LocalOffsets to ensure accurate world space positioning
            Vector3 worldSpherePos = spherePos + sphereCol.LocalOffset;
            Vector3 worldCylPos = cylPos + cylCol.LocalOffset;

            // relPos is the vector from Cylinder(B) to Sphere(A)
            Vector3 relPos = worldSpherePos - worldCylPos;

            float verticalDist = MathF.Abs(relPos.Y);
            float halfHeight = cylCol.Data.CylinderData.Y;
            float horizDistSq = relPos.X * relPos.X + relPos.Z * relPos.Z;
            float radius = cylCol.Data.CylinderData.X;

            float closestX = MyMath.clamp(relPos.X, -radius, radius);
            float closestZ = MyMath.clamp(relPos.Z, -radius, radius);
            float closestY = MyMath.clamp(relPos.Y, -halfHeight, halfHeight);

            // closestPoint is local relative to the Cylinder
            Vector3 closestPoint = new Vector3(closestX, closestY, closestZ);
            float distSq = (relPos - closestPoint).LengthSquared;
            float radiusSq = sphereCol.Data.Radius * sphereCol.Data.Radius;

            if (distSq > radiusSq) return new ContactManifold { IsColliding = false };

            float dist = MathF.Sqrt(distSq);

            // FIX: We need normal from A(Sphere) to B(Cylinder)
            // (closestPoint - relPos) calculates the vector pointing FROM the sphere TO the cylinder surface.
            Vector3 normal = dist == 0 ? -Vector3.UnitY : (closestPoint - relPos).Normalized();

            return new ContactManifold
            {
                IsColliding = true,
                Normal = normal,
                Penetration = sphereCol.Data.Radius - dist,
                ContactPoint = worldCylPos + closestPoint
            };
        }

        private ContactManifold CheckSphereVsSphere(Vector3 posA, ColliderComponent colA, Vector3 posB, ColliderComponent colB)
        {
            Vector3 worldPosA = posA + colA.LocalOffset;
            Vector3 worldPosB = posB + colB.LocalOffset;

            // Normal from A to B
            Vector3 normal = worldPosB - worldPosA;
            float distSq = normal.LengthSquared;
            float radiusSum = colA.Data.Radius + colB.Data.Radius;

            if (distSq >= radiusSum * radiusSum) return new ContactManifold { IsColliding = false };

            float dist = MathF.Sqrt(distSq);
            return new ContactManifold
            {
                IsColliding = true,
                Normal = dist == 0 ? -Vector3.UnitY : normal.Normalized(),
                Penetration = radiusSum - dist,
                ContactPoint = worldPosA + (normal.Normalized() * colA.Data.Radius)
            };
        }

        // --- INTERNAL DATA STRUCTURES ---
        private struct BodyState
        {
            public EntityView Entity;
            public Vector3 Position;
            public Vector3 Velocity;
            public float InverseMass;
            public ColliderComponent Collider;
        }

        private struct StaticCollider
        {
            public EntityView Entity;
            public Vector3 Position;
            public ColliderComponent Collider;
        }

        // PURE DATA STRUCT - No nested structs, no boolean flags.
        private struct ContactManifold
        {
            public bool IsColliding;

            // Unified Solver Data
            public int BodyIndexA;
            public int BodyIndexB; // -1 represents ANY infinite-mass static object
            public float Restitution;
            public Vector3 Normal;
            public float Penetration;
            public Vector3 ContactPoint;

            // Event Output Data
            public EntityView EntityA;
            public EntityView EntityB;
        }

        // --- SPATIAL HASH GRID ---
        private class SpatialHashGrid
        {
            private float _cellSize;
            private Dictionary<long, List<StaticCollider>> _grid = new();

            public SpatialHashGrid(float cellSize) => _cellSize = cellSize;

            public void Clear() => _grid.Clear();

            private long GetKey(long x, long z) => (x * 31337) ^ z;

            public void Add(StaticCollider node)
            {
                long x = (long)MathF.Floor(node.Position.X / _cellSize);
                long z = (long)MathF.Floor(node.Position.Z / _cellSize);
                long key = GetKey(x, z);

                if (!_grid.ContainsKey(key)) _grid[key] = new List<StaticCollider>();
                _grid[key].Add(node);
            }

            public IEnumerable<StaticCollider> QueryNearby(Vector3 pos)
            {
                long cx = (long)MathF.Floor(pos.X / _cellSize);
                long cz = (long)MathF.Floor(pos.Z / _cellSize);

                for (long x = cx - 1; x <= cx + 1; x++)
                {
                    for (long z = cz - 1; z <= cz + 1; z++)
                    {
                        if (_grid.TryGetValue(GetKey(x, z), out var list))
                        {
                            foreach (var node in list) yield return node;
                        }
                    }
                }
            }
        }
    }
}
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
        // Cache collections to avoid GC allocations every frame
        private List<PhysicsNode> _activeNodes = new();
        private List<CollisionPair> _broadphasePairs = new();
        private List<ContactManifold> _manifolds = new();

        // System requires at minimum a Position and a Collider.
        public PhysicsSystem()
            : base(new BitMask(typeof(PositionComponent), typeof(ColliderComponent)))
        {
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            // Not used by this system because physics runs globally in UpdateInternal
            throw new NotImplementedException();
        }

        internal override void UpdateInternal(ECSWorld world, float deltaTime)
        {
            _activeNodes.Clear();
            _broadphasePairs.Clear();
            _manifolds.Clear();

            // 1. GATHER EVENT BUFFER & GENERATOR
            CollisionEventBufferComponent eventBuffer = default;
            bool hasEventBuffer = false;
            var eventSingleton = world.GetSingleton<CollisionEventBufferComponent>();
            if (eventSingleton.IsValid())
            {
                eventBuffer = world.GetComponent<CollisionEventBufferComponent>(eventSingleton);
                eventBuffer.Events.Clear();
                hasEventBuffer = true;
            }

            // Get Generator for Terrain Collision
            var genEntity = world.GetSingleton<TerrainGeneratorComponent>();
            TerrainGenerator generator = genEntity.IsValid() ? world.GetComponent<TerrainGeneratorComponent>(genEntity).Generator : null;

            // 2. DATA EXTRACTION
            foreach (var archetype in world.QueryArchetypes(WithMask, WithoutMask))
            {
                foreach (var entity in new ComponentAccessor(archetype))
                {
                    _activeNodes.Add(new PhysicsNode
                    {
                        Entity = entity,
                        Position = entity.Get<PositionComponent>().value,
                        Velocity = entity.GetOptional(new VelocityComponent(Vector3.Zero)).value,
                        InverseMass = entity.GetOptional(new MassComponent(0f)).value > 0 ? 1f / entity.Get<MassComponent>().value : 0f,
                        Collider = entity.Get<ColliderComponent>()
                    });
                }
            }

            // 3. INTEGRATION & TERRAIN COLLISION
            for (int i = 0; i < _activeNodes.Count; i++)
            {
                var node = _activeNodes[i];
                if (node.InverseMass == 0) continue;

                node.Position += node.Velocity * deltaTime;

                if (generator != null)
                {
                    float terrainHeight = generator.getHeightAt(new Vector2(node.Position.X, node.Position.Z));
                    Vector3 terrainNormal = generator.GetNormalAt(node.Position.X, node.Position.Z); // NEW: Sample normal

                    float radius = node.Collider.Type == ColliderType.Sphere ? node.Collider.Data.Radius : 0.5f;

                    if (node.Position.Y - radius < terrainHeight)
                    {
                        // Resolve Position (Push out along the terrain normal)
                        float penetration = (terrainHeight + radius) - node.Position.Y;
                        node.Position += terrainNormal * penetration;

                        // Resolve Velocity (Bounce off the normal, not just Y-axis)
                        // Reflect velocity across the surface normal: v_new = v_old - (1 + restitution) * (v_old dot N) * N
                        float velDotNormal = Vector3.Dot(node.Velocity, terrainNormal);

                        if (velDotNormal < 0) // Only bounce if moving into the surface
                        {
                            node.Velocity -= (1.0f + node.Collider.Restitution) * velDotNormal * terrainNormal;
                        }

                        // APPLY FRICTION (Now that we have a normal, we can kill sliding on slopes)
                        // Projects velocity onto the tangent plane and scales it down
                        Vector3 tangentVel = node.Velocity - Vector3.Dot(node.Velocity, terrainNormal) * terrainNormal;
                        node.Velocity -= tangentVel * 0.05f; // Adjust 0.05f for "stickiness"
                    }
                }
                _activeNodes[i] = node;
            }

            // 4. BROADPHASE
            for (int i = 0; i < _activeNodes.Count; i++)
            {
                for (int j = i + 1; j < _activeNodes.Count; j++)
                {
                    _broadphasePairs.Add(new CollisionPair { IndexA = i, IndexB = j });
                }
            }

            // 5. NARROWPHASE (The Dispatcher)
            foreach (var pair in _broadphasePairs)
            {
                var nodeA = _activeNodes[pair.IndexA];
                var nodeB = _activeNodes[pair.IndexB];

                ContactManifold manifold = new ContactManifold { IsColliding = false };

                // Collision Matrix
                if (nodeA.Collider.Type == ColliderType.Sphere && nodeB.Collider.Type == ColliderType.Sphere)
                {
                    manifold = CheckSphereVsSphere(nodeA, nodeB);
                }
                else if (nodeA.Collider.Type == ColliderType.Sphere && nodeB.Collider.Type == ColliderType.Cylinder)
                {
                    manifold = CheckSphereVsCylinder(nodeA, nodeB);
                }
                else if (nodeA.Collider.Type == ColliderType.Cylinder && nodeB.Collider.Type == ColliderType.Sphere)
                {
                    manifold = CheckSphereVsCylinder(nodeB, nodeA);
                }

                if (manifold.IsColliding)
                {
                    manifold.IndexA = pair.IndexA;
                    manifold.IndexB = pair.IndexB;
                    _manifolds.Add(manifold);
                }
            }

            // 6. SOLVER (Apply impulses and positional correction)
            ResolveCollisions();

            // 7. WRITE BACK TO ECS (Using your entity.Set() pattern)
            foreach (var node in _activeNodes)
            {
                if (node.InverseMass == 0) continue; // Static objects haven't moved

                var posComponent = node.Entity.Get<PositionComponent>();
                posComponent.value = node.Position;
                node.Entity.Set(posComponent);

                var velComponent = node.Entity.GetOptional(new VelocityComponent(Vector3.Zero));
                velComponent.value = node.Velocity;
                node.Entity.Set(velComponent);
            }

            // 8. EMIT EVENTS
            if (hasEventBuffer)
            {
                foreach (var m in _manifolds)
                {
                    var a = _activeNodes[m.IndexA];
                    var b = _activeNodes[m.IndexB];
                    Vector3 relativeVelocity = b.Velocity - a.Velocity;

                    eventBuffer.Events.Add(new CollisionEvent
                    {
                        EntityA = a.Entity,
                        EntityB = b.Entity,
                        ContactPoint = m.ContactPoint,
                        Normal = m.Normal,
                        ImpactForce = MathF.Abs(Vector3.Dot(relativeVelocity, m.Normal))
                    });
                }
            }
        }

        private void ResolveCollisions()
        {
            const float PositionalCorrectionPercent = 0.2f;
            const float PositionalCorrectionSlop = 0.01f;

            foreach (var m in _manifolds)
            {
                var a = _activeNodes[m.IndexA];
                var b = _activeNodes[m.IndexB];

                Vector3 relativeVelocity = b.Velocity - a.Velocity;
                float velAlongNormal = Vector3.Dot(relativeVelocity, m.Normal);

                // Do not resolve if velocities are separating
                if (velAlongNormal > 0) continue;

                float restitution = MathF.Min(a.Collider.Restitution, b.Collider.Restitution);
                float totalInverseMass = a.InverseMass + b.InverseMass;

                if (totalInverseMass == 0) continue;

                // Impulse Scalar
                float j = -(1.0f + restitution) * velAlongNormal;
                j /= totalInverseMass;

                Vector3 impulse = m.Normal * j;

                a.Velocity -= impulse * a.InverseMass;
                b.Velocity += impulse * b.InverseMass;

                // Positional Correction (Sinking Prevention)
                Vector3 correction = m.Normal * (MathF.Max(m.Penetration - PositionalCorrectionSlop, 0.0f) / totalInverseMass * PositionalCorrectionPercent);
                a.Position -= correction * a.InverseMass;
                b.Position += correction * b.InverseMass;

                // Write modified structs back to the array
                _activeNodes[m.IndexA] = a;
                _activeNodes[m.IndexB] = b;
            }
        }
        private ContactManifold CheckSphereVsCylinder(PhysicsNode sphere, PhysicsNode cylinder)
        {

            Vector3 relPos = sphere.Position - cylinder.Position;

            // 1. Check vertical (Y) distance
            float verticalDist = MathF.Abs(relPos.Y);
            float halfHeight = cylinder.Collider.Data.CylinderData.Y;

            // 2. Check horizontal distance (XZ plane)
            float horizDistSq = relPos.X * relPos.X + relPos.Z * relPos.Z;
            float radius = cylinder.Collider.Data.CylinderData.X;

            // Determine the closest point on the cylinder to the sphere center
            float closestX = MyMath.clamp(relPos.X, -radius, radius);
            float closestZ = MyMath.clamp(relPos.Z, -radius, radius);
            float closestY = MyMath.clamp(relPos.Y, -halfHeight, halfHeight);

            Vector3 closestPoint = new Vector3(closestX, closestY, closestZ);
            float distSq = (relPos - closestPoint).LengthSquared;
            float radiusSq = sphere.Collider.Data.Radius * sphere.Collider.Data.Radius;

            if (distSq > radiusSq) return new ContactManifold { IsColliding = false };

            float dist = MathF.Sqrt(distSq);
            Vector3 normal = dist == 0 ? Vector3.UnitY : (relPos - closestPoint).Normalized();
            return new ContactManifold
            {
                IsColliding = true,
                Normal = normal,
                Penetration = sphere.Collider.Data.Radius - dist,
                ContactPoint = cylinder.Position + closestPoint
            };
        }
        private ContactManifold CheckSphereVsSphere(PhysicsNode a, PhysicsNode b)
        {
            ContactManifold m = new ContactManifold { IsColliding = false };

            Vector3 posA = a.Position + a.Collider.LocalOffset;
            Vector3 posB = b.Position + b.Collider.LocalOffset;

            Vector3 normal = posB - posA;
            float distSq = normal.LengthSquared;
            float radiusSum = a.Collider.Data.Radius + b.Collider.Data.Radius;

            if (distSq >= radiusSum * radiusSum) return m;

            float dist = MathF.Sqrt(distSq);
            m.IsColliding = true;
            m.Normal = dist == 0 ? Vector3.UnitY : normal.Normalized();
            m.Penetration = radiusSum - dist;
            m.ContactPoint = posA + (m.Normal * a.Collider.Data.Radius);

            return m;
        }

        // --- INTERNAL DATA STRUCTURES ---

        private struct PhysicsNode
        {
            public EntityView Entity;
            public Vector3 Position;
            public Vector3 Velocity;
            public float InverseMass;
            public ColliderComponent Collider;
        }

        private struct CollisionPair
        {
            public int IndexA;
            public int IndexB;
        }

        private struct ContactManifold
        {
            public bool IsColliding;
            public int IndexA;
            public int IndexB;
            public Vector3 Normal;
            public float Penetration;
            public Vector3 ContactPoint;
        }
    }
}
using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.Physics;
using Dino_Engine.Util;
using Dino_Engine.Util.Data_Structures.Grids;
using System;
using OpenTK.Mathematics;
using System.Text;

namespace Dino_Engine.ECS.Systems
{
    public class CollidingSystem : ComponentSystem
    {
        public CollidingSystem()
        {
            addRequiredComponent<TransformationComponent>();
            addRequiredComponent<CollisionComponent>();
            addRequiredComponent<CollisionEventComponent>();
        }
        internal override void UpdateEntity(Entity entity)
        {
            foreach(Entity other in Engine.Instance.ECSEngine.getSystem<CollidableSystem>().MemberEntities)
            {
                if (entity != other)
                {
                    if (TryCollide(entity, other, out Vector3 collisionPosition, out Vector3 collisionNormal))
                    {

                        entity.getComponent<CollisionEventComponent>().onCollision(other, collisionPosition, collisionNormal);
                        return;
                    }
                }
            }

        }

        private bool TryCollide(Entity entity, Entity other, out Vector3 collisionPosition, out Vector3 collisionNormal)
        {
            Type entityType = entity.getComponent<CollisionComponent>().HitBox.GetType();
            Type otherType = other.getComponent<CollisionComponent>().HitBox.GetType();

            if (entityType == typeof(SphereHitbox) && otherType == typeof(TerrainHitBox))
            {
                return TrySphereTerrainCollision(entity, other, out collisionPosition, out collisionNormal);
            }
            else if (entityType == typeof(SphereHitbox) && otherType == typeof(SphereHitbox))
            {
                return TrySphereSphereCollision(entity, other, out collisionPosition, out collisionNormal);
            }
            else
            {
                throw new Exception($"Tried perform collision case that is not implemented ({entityType.Name} + {otherType.Name})");
            }
        }
        private bool TrySphereSphereCollision(Entity sphereA, Entity sphereB, out Vector3 collisionPosition, out Vector3 collisionNormal)
        {
            collisionPosition = Vector3.Zero;
            collisionNormal = Vector3.Zero;

            float sphereARadius = ((SphereHitbox)sphereA.getComponent<CollisionComponent>().HitBox).Radius;
            float sphereBRadius = ((SphereHitbox)sphereB.getComponent<CollisionComponent>().HitBox).Radius;


            Vector3 sphereAPosition = sphereA.getComponent<TransformationComponent>().Transformation.position;
            Vector3 sphereBPosition = sphereB.getComponent<TransformationComponent>().Transformation.position;

            float dist = Vector3.Distance(sphereAPosition, sphereBPosition);
            float minDist = sphereARadius + sphereBRadius;
            if (dist <= minDist)
            {
                collisionNormal = Vector3.Normalize(sphereAPosition - sphereBPosition);
                collisionPosition = sphereAPosition + collisionNormal * (sphereARadius - (minDist - dist) / 2);

                return true;
            }
            return false;
        }
            private bool TrySphereTerrainCollision(Entity sphere, Entity terrain, out Vector3 collisionPosition, out Vector3 collisionNormal)
        {
            collisionPosition = Vector3.Zero;
            collisionNormal = Vector3.Zero;

            float sphereRadius = ((SphereHitbox)sphere.getComponent<CollisionComponent>().HitBox).Radius;

            // Transfer sphere to terrain space
            Transformation terrainTransform = terrain.getComponent<TransformationComponent>().Transformation;
            Transformation sphereTransform = sphere.getComponent<TransformationComponent>().Transformation;
            sphereTransform.translate(-terrainTransform.position);

            FloatGrid heightMap = terrain.getComponent<TerrainMapsComponent>().heightMap;
            Vector3Grid normalMap = terrain.getComponent<TerrainMapsComponent>().normalMap;

            // Sample points around the sphere's bottom hemisphere
            Vector3 sphereCenter = sphereTransform.position;
            Vector3[] samplePoints = {
                sphereCenter,
                sphereCenter + new Vector3(sphereRadius, 0, 0),
                sphereCenter + new Vector3(-sphereRadius, 0, 0),
                sphereCenter + new Vector3(0, 0, sphereRadius),
                sphereCenter + new Vector3(0, 0, -sphereRadius),
                sphereCenter + new Vector3(sphereRadius / MathF.Sqrt(2), 0, sphereRadius / MathF.Sqrt(2)),
                sphereCenter + new Vector3(sphereRadius / MathF.Sqrt(2), 0, -sphereRadius / MathF.Sqrt(2)),
                sphereCenter + new Vector3(-sphereRadius / MathF.Sqrt(2), 0, sphereRadius / MathF.Sqrt(2)),
                sphereCenter + new Vector3(-sphereRadius / MathF.Sqrt(2), 0, -sphereRadius / MathF.Sqrt(2))
            };

            foreach (Vector3 samplePoint in samplePoints)
            {
                if (!heightMap.Contains(samplePoint.Xz))
                {
                    continue;
                }
                float terrainHeight = heightMap.BilinearInterpolate(samplePoint.Xz);

                float distanceToTerrain = samplePoint.Y - terrainHeight;

                if (distanceToTerrain <= 0)
                {
                    collisionPosition = samplePoint+terrainTransform.position;
                    collisionNormal = normalMap.BilinearInterpolate(samplePoint.Xz);
                    collisionNormal.Normalize();
                    return true;
                }
            }
            return false;
        }

    }
}

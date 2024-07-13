using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.Physics;
using Dino_Engine.Util;
using Dino_Engine.Util.Data_Structures.Grids;
using System;
using OpenTK.Mathematics;

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
                if (TryCollide(entity, other, out Vector3 collisionPosition, out Vector3 collisionNormal))
                {
                    entity.getComponent<CollisionEventComponent>().onCollision(other, collisionPosition, collisionNormal);
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
            } else
            {
                throw new Exception($"Tried perform collision case that is not implemented ({entityType.Name} + {otherType.Name})");
            }
        }

        private bool TrySphereTerrainCollision(Entity sphere, Entity terrain, out Vector3 collisionPosition, out Vector3 collisionNormal)
        {
            collisionPosition = Vector3.Zero;
            collisionNormal = Vector3.Zero;

            // Get sphere radius
            float sphereRadius = ((SphereHitbox)sphere.getComponent<CollisionComponent>().HitBox).Radius;

            // Transfer sphere to terrain space
            Transformation terrainTransform = terrain.getComponent<TransformationComponent>().Transformation;
            Transformation sphereTransform = sphere.getComponent<TransformationComponent>().Transformation;

            // Get height map and normal map
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
                // Get the terrain height at the sample point's XZ plane
                float terrainHeight = heightMap.BilinearInterpolate(samplePoint.Xz);

                // Calculate the vertical distance from the sample point to the terrain height
                float distanceToTerrain = samplePoint.Y - terrainHeight;

                // Check if the sphere intersects with the terrain at this sample point
                if (distanceToTerrain <= 0)
                {
                    // Calculate the collision position and normal
                    collisionPosition = samplePoint;
                    collisionNormal = normalMap.BilinearInterpolate(samplePoint.Xz);

                    // Normalize the normal vector
                    collisionNormal.Normalize();

                    return true;
                }
            }

            return false;
        }

    }
}

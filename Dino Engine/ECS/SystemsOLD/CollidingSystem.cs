using Dino_Engine.Core;
using Dino_Engine.Physics;
using Dino_Engine.Util;
using Dino_Engine.Util.Data_Structures.Grids;
using System;
using OpenTK.Mathematics;
using System.Text;
using Dino_Engine.ECS.ComponentsOLD;

namespace Dino_Engine.ECS.SystemsOLD
{
    public class CollidingSystem : ComponentSystem
    {
        public CollidingSystem()
        {
            addRequiredComponent<TransformationComponent>();
            addRequiredComponent<CollisionComponent>();
            addRequiredComponent<CollisionEventComponent>();
        }
        internal override void UpdateEntity(EntityOLD entity)
        {
            foreach (EntityOLD other in Engine.Instance.ECSEngine.getSystem<CollidableSystem>().MemberEntities)
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

        private bool TryCollide(EntityOLD entity, EntityOLD other, out Vector3 collisionPosition, out Vector3 collisionNormal)
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
        private bool TrySphereSphereCollision(EntityOLD sphereA, EntityOLD sphereB, out Vector3 collisionPosition, out Vector3 collisionNormal)
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
        private bool TrySphereTerrainCollision(EntityOLD sphere, EntityOLD terrain, out Vector3 collisionPosition, out Vector3 collisionNormal)
        {
            collisionPosition = Vector3.Zero;
            collisionNormal = Vector3.Zero;

            FloatGrid heightMap = terrain.getComponent<TerrainMapsComponent>().heightMap;
            Vector3Grid normalMap = terrain.getComponent<TerrainMapsComponent>().normalMap;

            Vector3 scaleToGridSpace = ((TerrainHitBox)terrain.getComponent<CollisionComponent>().HitBox)._max;
            scaleToGridSpace.Y = 1f;
            scaleToGridSpace.Xz = heightMap.Resolution / scaleToGridSpace.Xz;
            Vector2 sphereRadiusGridSpace = ((SphereHitbox)sphere.getComponent<CollisionComponent>().HitBox).Radius * scaleToGridSpace.Xz;
            float sphereRadiusWorldSpace = ((SphereHitbox)sphere.getComponent<CollisionComponent>().HitBox).Radius;

            // Transfer sphere to terrain space
            Transformation terrainTransform = terrain.getComponent<TransformationComponent>().Transformation;
            Transformation sphereTransform = sphere.getComponent<TransformationComponent>().Transformation;
            sphereTransform.translate(-terrainTransform.position);


            // Sample points around the sphere's bottom hemisphere
            Vector3 sphereCenter = sphereTransform.position;
            sphereCenter.Xz = sphereCenter.Xz * scaleToGridSpace.Xz;
            Vector2 radiusOversqrt2 = sphereRadiusGridSpace / MathF.Sqrt(2);
            float radiusOver2 = sphereRadiusWorldSpace / MathF.Sqrt(2) - sphereRadiusWorldSpace / 2f;

            Vector3[] samplePoints = {
                sphereCenter + new Vector3(0, -sphereRadiusWorldSpace, 0),
                sphereCenter + new Vector3(sphereRadiusGridSpace.X, -radiusOver2, 0),
                sphereCenter + new Vector3(-sphereRadiusGridSpace.X, -radiusOver2, 0),
                sphereCenter + new Vector3(0, -radiusOver2, sphereRadiusGridSpace.Y),
                sphereCenter + new Vector3(0, -radiusOver2, -sphereRadiusGridSpace.Y),
                sphereCenter + new Vector3(radiusOversqrt2.X, -radiusOver2, radiusOversqrt2.Y),
                sphereCenter + new Vector3(radiusOversqrt2.X, -radiusOver2, -radiusOversqrt2.Y),
                sphereCenter + new Vector3(-radiusOversqrt2.X, -radiusOver2, radiusOversqrt2.Y),
                sphereCenter + new Vector3(-radiusOversqrt2.X, -radiusOver2, -radiusOversqrt2.Y)
            };

            float closestDistance = 999999f;
            Vector3 closestSample = new Vector3(0f);
            bool found = false;

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
                    if (distanceToTerrain < closestDistance)
                    {
                        found = true;
                        closestSample = samplePoint;
                        closestDistance = distanceToTerrain;
                        closestSample.Y = terrainHeight;
                    }

                }
            }
            if (found)
            {
                collisionPosition = closestSample / scaleToGridSpace + terrainTransform.position;
                collisionNormal = normalMap.BilinearInterpolate(closestSample.Xz);
                collisionNormal.Normalize();
                return true;
            }


            return false;
        }

    }
}

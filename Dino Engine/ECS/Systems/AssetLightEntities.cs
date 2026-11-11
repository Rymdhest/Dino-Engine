using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Particles;
using Dino_Engine.Particles.Content;
using Dino_Engine.Util;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    /// <summary>
    /// The lights and flames that are part of a model (<see cref="AssetLight"/>), as entities of their own where the model stands: what a world object streamed round the camera has
    /// (<see cref="TerrainChunkSystem"/>), and what anything that moves keeps beside it, moving them with it (<see cref="Move"/>).
    /// </summary>
    public static class AssetLightEntities
    {
        /// <summary>Where on a model placed so one of its lights is.</summary>
        public static Vector3 Where(in AssetLight light, Vector3 position, Quaternion rotation, Vector3 scale) => position + Vector3.Transform(light.Position * scale, rotation);

        /// <summary>The light a model gives off there, if it gives off any, with <paramref name="with"/> on it too if it's given, which is how whoever made it knows it again.</summary>
        public static Entity Light(ECSWorld world, in AssetLight light, Vector3 position, Quaternion rotation, Vector3 scale, IComponent? with = null)
        {
            Vector3 at = Where(light, position, rotation, scale);
            var components = new List<IComponent>
            {
                new PositionComponent(at),
                new LocalToWorldMatrixComponent { value = Matrix4.CreateTranslation(at) },
                new LightRangeComponent(light.Radius, light.Fill, light.LightsFog),
                new ColorComponent(new Colour(light.Colour.X, light.Colour.Y, light.Colour.Z, light.Intensity)),
            };

            // Each light keeps its own time, so a village doesn't flicker as one.
            if (light.Flicker > 0f) components.Add(new LightFlickerComponent(light.Flicker, light.FlickerSpeed, at.X * 1.3f + at.Z * 0.7f));

            if (light.ConeAngle > 0f)
            {
                components.Add(new SpotLightComponent(1f, light.ConeAngle));
                components.Add(new DirectionNormalizedComponent(Vector3.Transform(light.Direction, rotation)));
            }
            else
            {
                components.Add(new PointLightTag());
                if (light.Shadow) components.Add(new PointLightShadowComponent());
            }
            if (with != null) components.Add(with);
            return world.CreateEntity(components.ToArray());
        }

        /// <summary>An open flame that's part of a model: a candle's, a torch's or a brazier's, by how wide it is.</summary>
        public static Entity Flame(ECSWorld world, in AssetLight light, Vector3 position, Quaternion rotation, Vector3 scale, IComponent? with = null)
        {
            Vector3 at = Where(light, position, rotation, scale);
            float width = light.FlameWidth * scale.Y;
            (ParticleEffect effect, float size) = width < Fire.CandleWidth * 2f ? (Fire.Candle, width / Fire.CandleWidth)
                : width < Fire.TorchWidth * 2f ? (Fire.Torch, width / Fire.TorchWidth)
                : (Fire.Brazier, width / Fire.BrazierWidth);
            var components = new List<IComponent>
            {
                new PositionComponent(at),
                new LocalToWorldMatrixComponent { value = Matrix4.CreateTranslation(at) },
                new ParticleEffectComponent(effect, EffectShape.Point, size),
            };
            if (with != null) components.Add(with);
            return world.CreateEntity(components.ToArray());
        }

        /// <summary>Everything a model gives off, placed so, onto a list, each with <paramref name="with"/> on it if it's given.</summary>
        public static void Spawn(ECSWorld world, IReadOnlyList<AssetLight> lights, Vector3 position, Quaternion rotation, Vector3 scale, List<Entity> into, IComponent? with = null)
        {
            foreach (AssetLight light in lights)
            {
                if (light.Intensity > 0f) into.Add(Light(world, light, position, rotation, scale, with));
                if (light.FlameWidth > 0f) into.Add(Flame(world, light, position, rotation, scale, with));
            }
        }

        /// <summary>
        /// Brings what <see cref="Spawn"/> made for these lights along with a model that's moved, in the same order: each light and flame to where it now is on it, and a spotlight's beam round with it.
        /// </summary>
        public static void Move(ECSWorld world, IReadOnlyList<AssetLight> lights, Vector3 position, Quaternion rotation, Vector3 scale, List<Entity> made)
        {
            int next = 0;
            foreach (AssetLight light in lights)
            {
                Vector3 at = Where(light, position, rotation, scale);
                if (light.Intensity > 0f) Place(world, next < made.Count ? made[next++] : default, at, light.ConeAngle > 0f ? Vector3.Transform(light.Direction, rotation) : null);
                if (light.FlameWidth > 0f) Place(world, next < made.Count ? made[next++] : default, at, null);
            }
        }

        private static void Place(ECSWorld world, Entity entity, Vector3 at, Vector3? beam)
        {
            if (!entity.IsValid() || !world.TryGetEntityLocation(entity, out _, out _)) return;
            EntityView view = world.GetEntityView(entity);
            if (view.Get<PositionComponent>().value == at && beam == null) return;
            view.Set(new PositionComponent(at));
            view.Set(new LocalToWorldMatrixComponent { value = Matrix4.CreateTranslation(at) });
            if (beam is Vector3 way) view.Set(new DirectionNormalizedComponent(way));
        }
    }
}

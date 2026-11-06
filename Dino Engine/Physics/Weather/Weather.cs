using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.World;
using OpenTK.Mathematics;
using static Dino_Engine.World.WorldMath;

namespace Dino_Engine.Physics
{
    /// <summary>
    /// Access to the engine's weather (the world's <see cref="WeatherSingleton"/>), and what it does to light. Without a weather singleton it's a fair day and always will be.
    /// </summary>
    public static class Weather
    {
        private static readonly WeatherSample Fair = new WeatherSample { Gale = 1f };

        public static bool TryGet(ECSWorld world, out WeatherSingleton weather)
        {
            weather = default;
            if (world == null || !world.TryGetSingleton<WeatherSingleton>(out Entity entity)) return false;
            weather = world.GetComponent<WeatherSingleton>(entity);
            return true;
        }

        /// <summary>What's happening at the camera's focus, as it's seen and heard.</summary>
        public static WeatherSample Here(ECSWorld world)
        {
            return TryGet(world, out WeatherSingleton weather) && weather.Enabled ? weather.Here : Fair;
        }

        /// <summary>The weather anywhere at this moment, on a world's own terms. Forced weather is everywhere at once.</summary>
        public static WeatherSample At(ECSWorld world, Vector2 position)
        {
            if (!TryGet(world, out WeatherSingleton weather) || !weather.Enabled) return Fair;
            if (weather.Forced is WeatherSample forced) return forced;
            return weather.Field?.Sample(position, weather.Clock) ?? Fair;
        }

        /// <summary>How much of the sun gets past this much cloud, 1 under a clear sky. Clouds thin the light well before they close over.</summary>
        public static float SunThrough(in WeatherSingleton weather, float cloud)
        {
            float closed = Smoothstep(0.2f, 1f, cloud);
            return Lerp(1f, weather.CloudShadow, closed);
        }

        /// <summary>The sky over this weather, from what it would be on a clear day.</summary>
        public static Vector3 Sky(Vector3 clear, in WeatherSingleton weather)
        {
            WeatherSample here = weather.Here;
            float gloom = Lerp(1f, weather.RainGloom, here.Fall) * Lerp(1f, weather.StormGloom, here.Storm);
            Vector3 sky = Vector3.Lerp(clear, weather.OvercastSky * gloom, Smoothstep(0.1f, 0.95f, here.Cloud));
            return Vector3.Lerp(sky, weather.DustColour * 0.8f, here.Dust * 0.75f);
        }
    }
}

using OpenTK.Mathematics;

namespace Dino_Engine.World.Content
{
    /// <summary>
    /// A light that is part of a model, in model space: whatever glows also shines. The generators say where theirs are, as they
    /// do for chimneys, and <see cref="Dino_Engine.ECS.Systems.TerrainChunkSystem"/> brings them in and out with the objects
    /// they belong to, so anything placed anywhere lights its surroundings. A <see cref="ConeAngle"/> of 0 is a point light.
    /// </summary>
    public readonly record struct AssetLight
    {
        public Vector3 Position { get; init; }
        public Vector3 Colour { get; init; }
        public float Intensity { get; init; }       // What it gives at one metre.
        public float Radius { get; init; }          // Metres, to nothing.
        public float Fill { get; init; }            // See LightRangeComponent.
        public float Flicker { get; init; }         // See LightFlickerComponent.
        public float FlickerSpeed { get; init; }
        public bool Shadow { get; init; }           // May hold a shadow map, when it's among the nearest lights.
        public bool LightsFog { get; init; }
        public Vector3 Direction { get; init; }     // Spotlights: the way it shines.
        public float ConeAngle { get; init; }       // Spotlights: radians, edge to edge.

        private static readonly Vector3 Flame = new Vector3(1f, 0.52f, 0.2f);
        private static readonly Vector3 Oil = new Vector3(1f, 0.66f, 0.34f);

        public static AssetLight Campfire(Vector3 at) => new AssetLight
        {
            Position = at, Colour = Flame, Intensity = 130f, Radius = 25f, Fill = 0.25f,
            Flicker = 0.32f, FlickerSpeed = 9f, Shadow = true, LightsFog = true,
        };

        public static AssetLight Lantern(Vector3 at) => new AssetLight
        {
            Position = at, Colour = Oil, Intensity = 160f, Radius = 24f, Fill = 0.2f,
            Flicker = 0.1f, FlickerSpeed = 5f, Shadow = true, LightsFog = true,
        };

        public static AssetLight StoneLantern(Vector3 at) => new AssetLight
        {
            Position = at, Colour = Oil, Intensity = 75f, Radius = 17f, Fill = 0.2f,
            Flicker = 0.14f, FlickerSpeed = 6f, Shadow = true, LightsFog = true,
        };

        /// <summary>The cold light of old stones and holy places.</summary>
        public static AssetLight Glow(Vector3 at, Vector3 colour, float intensity, float radius) => new AssetLight
        {
            Position = at, Colour = colour, Intensity = intensity, Radius = radius, Fill = 0.3f,
            Flicker = 0.12f, FlickerSpeed = 1.3f, LightsFog = true,
        };

        /// <summary>A lit room seen from outside: a pool of light on the ground under a window.</summary>
        public static AssetLight Window(Vector3 at, Vector3 outward) => new AssetLight
        {
            Position = at, Colour = Oil, Intensity = 50f, Radius = 13f, Fill = 0.1f,
            Direction = (outward - Vector3.UnitY * 0.55f).Normalized(), ConeAngle = MathHelper.DegreesToRadians(115f),
        };
    }
}

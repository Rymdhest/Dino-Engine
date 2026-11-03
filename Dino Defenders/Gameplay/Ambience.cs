using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Physics;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Defenders.Gameplay
{
    /// <summary>
    /// Keeps a world breathing around the hero. The air takes on the place they are in: a marsh is thick with fog, a steppe is
    /// windy and dry, and what drifts past them is pollen, snow or embers according to the biome. The chimneys of a site
    /// smoke while it's near. The guiding wind turns the whole sky toward the next goal, and when the warlord falls the land clears.
    /// </summary>
    public class AmbienceSystem : SystemBase
    {
        private const float RegionInterval = 0.5f;
        private const float RegionResponse = 0.5f;      // Per second.
        private const float MoteRadius = 26f;
        private const float GuideTime = 9f;
        private const float GuideCooldown = 14f;
        private const float GuideSpeed = 9f;
        private const float LiberationTime = 7f;
        private const float LiberatedFog = 0.35f;
        private const float LiberatedSun = 1.18f;

        private readonly WorldRandom _random = new WorldRandom(Environment.TickCount);
        private GameSession? _session;
        private float _nextRegion;
        private float _fogTarget;
        private float _windTarget;
        private Vector3 _fogColourTarget;
        private AmbientMotes _motes;
        private float _moteDebt;
        private float _guideLeft;
        private float _guideDirection;
        private float _liberation;
        private Colour _sunColour;

        public AmbienceSystem()
            : base(new BitMask(typeof(PlayerTag)))
        {
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime) { }

        protected override void UpdateFrame(ECSWorld world, float deltaTime)
        {
            GameSession session = GameSession.Current;
            if (session == null) return;
            if (session != _session) Begin(world, session);

            _nextRegion -= deltaTime;
            if (_nextRegion <= 0f)
            {
                _nextRegion = RegionInterval;
                ReadRegion(session);
                session.UpdateGuidance();
            }

            if (session.Intent.Guide && session.GuideCooldown <= 0f && session.Guidance is Vector2 target)
            {
                Vector2 toward = target - session.PlayerPosition;
                _guideDirection = MathHelper.RadiansToDegrees(MathF.Atan2(toward.X, toward.Y));
                _guideLeft = GuideTime;
                session.GuideCooldown = GuideCooldown;
            }

            UpdateAir(world, session, deltaTime);
            UpdateLiberation(world, session, deltaTime);
            SpawnMotes(world, session, deltaTime);
        }

        private void Begin(ECSWorld world, GameSession session)
        {
            _session = session;
            _nextRegion = 0f;
            _guideLeft = 0f;
            _liberation = session.Liberated ? 1f : 0f;
            _sunColour = world.GetComponent<ColorComponent>(world.sun).value;
            ReadRegion(session);
        }

        // What the air should be like here: the biomes underfoot, weighted, in the proportions the world's weather was set from.
        private void ReadRegion(GameSession session)
        {
            WorldData world = session.World;
            WorldMood mood = world.Plan.Mood;

            float fog = 0f;
            float wind = 0f;
            float averageFog = 0f;
            float averageWind = 0f;
            float strongest = 0f;
            Vector3 colour = Vector3.Zero;

            for (int i = 0; i < world.Plan.Biomes.Count; i++)
            {
                BiomeDefinition biome = world.Plan.Biomes[i];
                float weight = world.BiomeWeights[i].Sample(session.PlayerPosition.X, session.PlayerPosition.Y);
                fog += biome.FogDensity * weight;
                wind += biome.WindStrength * weight;
                colour += biome.FogColour * weight;
                averageFog += biome.FogDensity;
                averageWind += biome.WindStrength;
                if (weight > strongest)
                {
                    strongest = weight;
                    _motes = biome.Motes;
                }
            }

            averageFog /= world.Plan.Biomes.Count;
            averageWind /= world.Plan.Biomes.Count;

            _fogTarget = fog / MathF.Max(averageFog, 1e-4f) * mood.FogDensity;
            _windTarget = wind / MathF.Max(averageWind, 1e-4f) * mood.WindSpeed;
            _fogColourTarget = colour.LengthSquared > 0.01f ? colour : mood.FogColour;
        }

        private void UpdateAir(ECSWorld world, GameSession session, float deltaTime)
        {
            float blend = 1f - MathF.Exp(-RegionResponse * deltaTime);

            EntityView fogView = world.GetEntityView(world.GetSingleton<VolumetricFogSingleton>());
            var fog = fogView.Get<VolumetricFogSingleton>();
            float clearing = MathHelper.Lerp(1f, LiberatedFog, _liberation);
            fog.Density += (_fogTarget * clearing - fog.Density) * blend;
            fog.Albedo += (_fogColourTarget - fog.Albedo) * blend;
            fogView.Set(fog);

            EntityView windView = world.GetEntityView(world.GetSingleton<WindSingleton>());
            var wind = windView.Get<WindSingleton>();
            if (_guideLeft > 0f)
            {
                _guideLeft -= deltaTime;
                wind.Direction = _guideDirection;
                wind.Speed = MathF.Max(_windTarget, GuideSpeed);
            }
            else
            {
                wind.Direction = session.World.Plan.Mood.WindDirection;
                wind.Speed += (_windTarget - wind.Speed) * blend;
            }
            windView.Set(wind);
        }

        private void UpdateLiberation(ECSWorld world, GameSession session, float deltaTime)
        {
            if (!session.Liberated || _liberation >= 1f) return;

            _liberation = MathF.Min(_liberation + deltaTime / LiberationTime, 1f);
            float gain = MathHelper.Lerp(1f, LiberatedSun, _liberation);
            Colour sun = _sunColour;
            sun.Intensity *= gain;
            world.GetEntityView(world.sun).Set(new ColorComponent(sun));
        }

        private void SpawnMotes(ECSWorld world, GameSession session, float deltaTime)
        {
            bool guiding = _guideLeft > 0f;
            AmbientMotes kind = guiding ? AmbientMotes.FallingLeaves : _motes;
            if (kind == AmbientMotes.None || session.Paused) return;

            MoteStyle style = StyleOf(kind);
            _moteDebt += (guiding ? 45f : style.Rate) * deltaTime;
            Vector3 centre = new Vector3(session.PlayerPosition.X, session.World.Height.Sample(session.PlayerPosition), session.PlayerPosition.Y);

            Vector2 drift = guiding
                ? new Vector2(MathF.Sin(MathHelper.DegreesToRadians(_guideDirection)), MathF.Cos(MathHelper.DegreesToRadians(_guideDirection))) * 12f
                : world.GetComponent<WindSingleton>(world.GetSingleton<WindSingleton>()).Velocity * style.Drift;

            while (_moteDebt >= 1f)
            {
                _moteDebt -= 1f;
                Vector3 at = centre + new Vector3(_random.Range(-MoteRadius, MoteRadius), _random.Range(style.LowestHeight, style.HighestHeight), _random.Range(-MoteRadius, MoteRadius));
                Vector3 velocity = new Vector3(drift.X + _random.Range(-0.5f, 0.5f), style.Rise + _random.Range(-0.2f, 0.2f), drift.Y + _random.Range(-0.5f, 0.5f));
                world.CreateEntity(
                    new ParticleRenderTag(),
                    new LocalToWorldMatrixComponent(),
                    new PositionComponent(at),
                    new ScaleComponent(new Vector3(style.Size * _random.Range(0.7f, 1.2f))),
                    new ColorComponent(style.Colour),
                    new VelocityComponent(velocity),
                    new selfDestroyComponent(style.Life * _random.Range(0.7f, 1.2f)));
            }
        }

        private readonly record struct MoteStyle(float Rate, float Size, float Life, float LowestHeight, float HighestHeight, float Rise, float Drift, Colour Colour);

        private static MoteStyle StyleOf(AmbientMotes kind) => kind switch
        {
            AmbientMotes.Pollen => new(14f, 0.05f, 5f, 0.6f, 5f, 0.1f, 0.25f, new Colour(1f, 0.92f, 0.55f, 1.3f)),
            AmbientMotes.Petals => new(16f, 0.11f, 6f, 2f, 11f, -0.7f, 0.4f, new Colour(1f, 0.62f, 0.68f, 1.1f)),
            AmbientMotes.FallingLeaves => new(12f, 0.13f, 6f, 3f, 12f, -1f, 0.45f, new Colour(0.85f, 0.42f, 0.14f, 1f)),
            AmbientMotes.Snow => new(34f, 0.07f, 6f, 4f, 15f, -1.5f, 0.5f, new Colour(0.95f, 0.97f, 1f, 1.2f)),
            AmbientMotes.Embers => new(16f, 0.06f, 3.2f, 0.3f, 4f, 1.4f, 0.4f, new Colour(1f, 0.4f, 0.1f, 4f)),
            AmbientMotes.Fireflies => new(9f, 0.06f, 3.5f, 0.6f, 3.5f, 0.1f, 0.05f, new Colour(0.75f, 1f, 0.35f, 4f)),
            AmbientMotes.Dust => new(12f, 0.1f, 3.4f, 0.2f, 3f, 0.05f, 0.8f, new Colour(0.8f, 0.68f, 0.48f, 0.7f)),
            _ => default,
        };
    }

    /// <summary>Lights the fires of a place, and its lanterns, while the hero is near enough to see them.</summary>
    public static class Ambience
    {
        private const int MostLightsPerSite = 6;

        public static void Light(ECSWorld world, GameSession session, SiteState state)
        {
            int lights = 0;
            foreach (SiteFeature feature in state.Features)
            {
                switch (feature.Kind)
                {
                    case SiteFeatureKind.Chimney:
                        state.Live.Add(Smoke(world, feature.Position));
                        break;

                    case SiteFeatureKind.Campfire:
                        state.Live.Add(Flames(world, feature.Position));
                        state.Live.Add(Smoke(world, feature.Position + Vector3.UnitY * 1.6f));
                        if (lights++ < MostLightsPerSite) state.Live.Add(Glow(world, feature.Position + Vector3.UnitY * 1.2f, new Colour(1f, 0.56f, 0.22f, 12f)));
                        break;

                    case SiteFeatureKind.Lantern:
                        if (lights++ < MostLightsPerSite) state.Live.Add(Glow(world, feature.Position, new Colour(1f, 0.72f, 0.4f, 6f)));
                        break;
                }
            }
        }

        private static Entity Glow(ECSWorld world, Vector3 position, Colour colour)
        {
            return world.CreateEntity("Site Light",
                new PointLightTag(),
                new PositionComponent(position),
                new LocalToWorldMatrixComponent(),
                new AttunuationComponent(1f, 0.25f, 0.12f),
                new ColorComponent(colour));
        }

        private static Entity Smoke(ECSWorld world, Vector3 position)
        {
            return world.CreateEntity("Smoke",
                new ParticleEmitterComponent
                {
                    particlesPerSecond = 5f,
                    particleSpeed = 1.5f,
                    particleDuration = 5.5f,
                    particleSizeStart = 1.1f,
                    particleWeight = -0.04f,
                    particleDirectionError = 0.28f,
                    particleColourStart = new Colour(0.56f, 0.55f, 0.54f, 0.6f, 0.55f),
                },
                new PositionComponent(position),
                new DirectionNormalizedComponent(Vector3.UnitY),
                new LocalToWorldMatrixComponent());
        }

        private static Entity Flames(ECSWorld world, Vector3 position)
        {
            return world.CreateEntity("Flames",
                new ParticleEmitterComponent
                {
                    particlesPerSecond = 18f,
                    particleSpeed = 2.4f,
                    particleDuration = 0.8f,
                    particleSizeStart = 0.34f,
                    particleWeight = -0.25f,
                    particleDirectionError = 0.3f,
                    particleColourStart = new Colour(1f, 0.5f, 0.12f, 4f),
                },
                new PositionComponent(position),
                new DirectionNormalizedComponent(Vector3.UnitY),
                new LocalToWorldMatrixComponent());
        }
    }
}

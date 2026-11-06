using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Networking;
using Dino_Engine.Physics;
using Dino_Engine.Sound;
using Dino_Engine.Sound.Content;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Defenders.Client
{
    /// <summary>
    /// Keeps a world breathing around the hero. The air takes on the place they are in: a marsh is thick with fog, a steppe is
    /// windy and dry, and what drifts past them is pollen, snow or embers according to the biome. The chimneys of a site
    /// smoke while it's near. The guiding wind turns the whole sky toward the next goal, and when the warlord falls the land clears.
    /// It is always night in a churchyard: the day goes out as the hero nears one, and fog rises off the graves.
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
        private const float LightsWake = 100f;
        private const float LightsSleep = 160f;
        private const float NightReach = 90f;           // Metres outside a churchyard's wall that its night begins.
        private const float NightResponse = 1.2f;       // Per second.
        private const float ChurchyardFog = 0.26f;
        private const float WispsFrom = 0.5f;           // How deep the night has to be for them to come out.
        private const float HeroLightHeight = 6f;
        private const float HeroLightRadius = 20f;
        private const float HeroLightIntensity = 36f;

        private static readonly Vector3 ChurchyardFogColour = new Vector3(0.82f, 0.88f, 1f);

        private readonly WorldRandom _random = new WorldRandom(Environment.TickCount);
        private GameSession? _session;
        private float _nextRegion;
        private float _fogTarget;
        private float _windTarget;
        private Vector3 _fogColourTarget;
        private AmbientMotes _motes;
        private float _nightTarget;
        private Entity _heroLight;
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

            session.GuideCooldown = MathF.Max(session.GuideCooldown - deltaTime, 0f);

            _nextRegion -= deltaTime;
            if (_nextRegion <= 0f)
            {
                _nextRegion = RegionInterval;
                ReadRegion(world, session);
                session.UpdateGuidance();
                UpdateSiteLights(world, session);
            }

            if (session.Intent.Guide && session.GuideCooldown <= 0f && session.Guidance is Vector2 target)
            {
                Audio.Play(Magic.GuidingWind);
                Vector2 toward = target - session.PlayerPosition;
                _guideDirection = MathHelper.RadiansToDegrees(MathF.Atan2(toward.X, toward.Y));
                _guideLeft = GuideTime;
                session.GuideCooldown = GuideCooldown;
            }

            UpdateWeather(world, session);
            UpdateAir(world, session, deltaTime);
            UpdateNight(world, session, deltaTime);
            UpdateLiberation(world, session, deltaTime);
            SpawnMotes(world, session, deltaTime);
        }

        // The fires of a place are for looking at, so each player lights the ones near them.
        private static void UpdateSiteLights(ECSWorld world, GameSession session)
        {
            foreach (SiteState state in session.Sites)
            {
                float distance = (state.Site.Position - session.PlayerPosition).Length - state.Site.Radius;
                if (!state.Active && distance < LightsWake)
                {
                    state.Active = true;
                    Ambience.Light(world, session, state);
                }
                else if (state.Active && distance > LightsSleep)
                {
                    foreach (Entity entity in state.Live) world.DestroyEntity(entity);
                    state.Live.Clear();
                    state.Active = false;
                }
            }
        }

        private void Begin(ECSWorld world, GameSession session)
        {
            _session = session;
            _nextRegion = 0f;
            _guideLeft = 0f;
            _liberation = session.Liberated ? 1f : 0f;
            _sunColour = world.GetComponent<ColorComponent>(world.sun).value;
            ReadRegion(world, session);

            // Whoever starts in a churchyard is already in its night.
            EntityView nightView = world.GetEntityView(world.GetSingleton<NightfallSingleton>());
            var night = nightView.Get<NightfallSingleton>();
            night.Amount = _nightTarget;
            nightView.Set(night);

            EntityView fogView = world.GetEntityView(world.GetSingleton<VolumetricFogSingleton>());
            var fog = fogView.Get<VolumetricFogSingleton>();
            fog.Density = MathHelper.Lerp(fog.Density, _fogTarget, _nightTarget);
            fog.Albedo = Vector3.Lerp(fog.Albedo, _fogColourTarget, _nightTarget);
            fogView.Set(fog);

            _heroLight = world.CreateEntity("Hero Light",
                new PointLightTag(),
                new PositionComponent(HeroLightPosition(session)),
                new LocalToWorldMatrixComponent(),
                new LightRangeComponent(HeroLightRadius, 0.35f, lightsFog: false),
                new ColorComponent(new Colour(1f, 0.86f, 0.68f, 0f)));
        }

        private static Vector3 HeroLightPosition(GameSession session)
        {
            Vector2 at = session.PlayerPosition;
            return new Vector3(at.X, session.World.Height.Sample(at) + HeroLightHeight, at.Y);
        }

        // Full night inside the walls, and none at all once they are far enough behind.
        private static float NightAt(GameSession session)
        {
            if (session.Offering.Has(Omens.Night)) return 1f;

            float night = 0f;
            foreach (SiteState state in session.Sites)
            {
                if (state.Wild || state.Site.Kind != SiteKind.Graveyard) continue;
                float outside = (state.Site.Position - session.PlayerPosition).Length - state.Site.Radius;
                float t = Math.Clamp(1f - outside / NightReach, 0f, 1f);
                night = MathF.Max(night, t * t * (3f - 2f * t));
            }
            return night;
        }

        private void UpdateNight(ECSWorld world, GameSession session, float deltaTime)
        {
            EntityView view = world.GetEntityView(world.GetSingleton<NightfallSingleton>());
            var night = view.Get<NightfallSingleton>();
            night.Amount += (_nightTarget - night.Amount) * (1f - MathF.Exp(-NightResponse * deltaTime));
            view.Set(night);

            // Enough to see what's coming by, and no more than a hero's own lamp would throw.
            if (!world.TryGetEntityLocation(_heroLight, out _, out _)) return;
            EntityView light = world.GetEntityView(_heroLight);
            light.Set(new PositionComponent(HeroLightPosition(session)));
            light.Set(new ColorComponent(new Colour(1f, 0.86f, 0.68f, HeroLightIntensity * night.Amount)));
        }

        // What the air should be like here: the biomes underfoot, weighted, in the proportions the world's weather was set from.
        private void ReadRegion(ECSWorld ecs, GameSession session)
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
            _windTarget = wind / MathF.Max(averageWind, 1e-4f) * mood.WindSpeed * Weather.Here(ecs).Gale;
            _fogColourTarget = colour.LengthSquared > 0.01f ? colour : mood.FogColour;

            _nightTarget = NightAt(session);
            _fogTarget = MathHelper.Lerp(_fogTarget, MathF.Max(_fogTarget, ChurchyardFog), _nightTarget);
            _fogColourTarget = Vector3.Lerp(_fogColourTarget, ChurchyardFogColour, _nightTarget);
            if (_nightTarget > WispsFrom) _motes = AmbientMotes.Wisps;
            if (session.ScriptedWind is (float speed, _)) _windTarget = speed;
        }

        // The weather runs on the server's clock, so that everyone in a world looks up at the same sky with nothing sent. Every machine works it out from the seed.
        private static void UpdateWeather(ECSWorld world, GameSession session)
        {
            EntityView view = world.GetEntityView(world.GetSingleton<WeatherSingleton>());
            var weather = view.Get<WeatherSingleton>();
            weather.Clock = session.Client.Render.RenderTick * NetTime.TickSeconds + session.WeatherClockOffset;
            weather.ClockGiven = true;
            weather.Forced = session.ScriptedWeather;
            view.Set(weather);
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
                wind.Direction = session.ScriptedWind?.direction ?? session.World.Plan.Mood.WindDirection;
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
            _moteDebt += (guiding ? 45f : style.Rate * LeftInTheAir(world, kind)) * deltaTime;
            Vector3 centre = new Vector3(session.PlayerPosition.X, session.World.Height.Sample(session.PlayerPosition), session.PlayerPosition.Y);

            Vector2 drift = guiding
                ? new Vector2(MathF.Sin(MathHelper.DegreesToRadians(_guideDirection)), MathF.Cos(MathHelper.DegreesToRadians(_guideDirection))) * 12f
                : Carried(world.GetComponent<WindSingleton>(world.GetSingleton<WindSingleton>()).Velocity) * style.Drift;

            while (_moteDebt >= 1f)
            {
                _moteDebt -= 1f;
                Vector3 at = centre + new Vector3(_random.Range(-MoteRadius, MoteRadius), _random.Range(style.LowestHeight, style.HighestHeight), _random.Range(-MoteRadius, MoteRadius));
                Vector3 velocity = new Vector3(drift.X + _random.Range(-0.5f, 0.5f), style.Rise + _random.Range(-0.2f, 0.2f), drift.Y + _random.Range(-0.5f, 0.5f));
                world.CreateEntity(
                    new ParticleRenderTag { Lit = !style.Glows },
                    new LocalToWorldMatrixComponent(),
                    new PositionComponent(at),
                    new ScaleComponent(new Vector3(style.Size * _random.Range(0.7f, 1.2f))),
                    new ColorComponent(style.Colour),
                    new VelocityComponent(velocity),
                    new selfDestroyComponent(style.Life * _random.Range(0.7f, 1.2f)));
            }
        }

        // Motes ride the wind's patterns, whose speed levels off, as a speck going by at the speed of a gale is a line across the screen.
        private static Vector2 Carried(Vector2 wind)
        {
            float speed = wind.Length;
            return speed < 1e-3f ? wind : wind * (WindField.PatternSpeed(speed) / speed);
        }

        // Rain washes dust and pollen out of the air, and what's wet doesn't lift again until it has dried.
        private static float LeftInTheAir(ECSWorld world, AmbientMotes kind)
        {
            if (kind is not (AmbientMotes.Dust or AmbientMotes.Pollen)) return 1f;
            WeatherSample here = Weather.Here(world);
            return 1f - MathF.Max(here.Wet, here.Rain);
        }

        private readonly record struct MoteStyle(float Rate, float Size, float Life, float LowestHeight, float HighestHeight, float Rise, float Drift, Colour Colour, bool Glows = false);

        private static MoteStyle StyleOf(AmbientMotes kind) => kind switch
        {
            AmbientMotes.Pollen => new(14f, 0.05f, 5f, 0.6f, 5f, 0.1f, 0.25f, new Colour(1f, 0.92f, 0.55f, 1.3f)),
            AmbientMotes.Petals => new(16f, 0.11f, 6f, 2f, 11f, -0.7f, 0.4f, new Colour(1f, 0.62f, 0.68f, 1.1f)),
            AmbientMotes.FallingLeaves => new(12f, 0.13f, 6f, 3f, 12f, -1f, 0.45f, new Colour(0.85f, 0.42f, 0.14f, 1f)),
            AmbientMotes.Snow => new(34f, 0.07f, 6f, 4f, 15f, -1.5f, 0.5f, new Colour(0.95f, 0.97f, 1f, 1.2f)),
            AmbientMotes.Embers => new(16f, 0.06f, 3.2f, 0.3f, 4f, 1.4f, 0.4f, new Colour(1f, 0.4f, 0.1f, 4f), Glows: true),
            AmbientMotes.Fireflies => new(9f, 0.06f, 3.5f, 0.6f, 3.5f, 0.1f, 0.05f, new Colour(0.75f, 1f, 0.35f, 4f), Glows: true),
            AmbientMotes.Dust => new(12f, 0.1f, 3.4f, 0.2f, 3f, 0.05f, 0.5f, new Colour(0.8f, 0.68f, 0.48f, 0.7f)),
            AmbientMotes.Wisps => new(2.5f, 0.13f, 7f, 0.6f, 3f, 0.1f, 0.05f, new Colour(0.3f, 1f, 0.6f, 4f), Glows: true),
            _ => default,
        };
    }

    /// <summary>The flames and smoke of a place, while the hero is near enough to see them. Their light comes with the models they rise from.</summary>
    public static class Ambience
    {
        /// <summary>Finds what comes alive at each site: the chimneys and fires the generator placed there.</summary>
        public static void FindFeatures(GameSession session, WorldAssetLibrary assets)
        {
            WorldObjects objects = session.World.Objects;
            foreach (WorldObject item in objects.All())
            {
                string id = objects.AssetIds[item.Asset];
                bool fire = id == StructureCatalog.Campfire.Id;
                IReadOnlyList<Vector3> chimneys = assets.ChimneysOf(id);
                if (!fire && chimneys.Count == 0) continue;

                SiteState? owner = NearestSite(session, item.Position.Xz);
                if (owner == null) continue;

                if (fire) owner.Features.Add(new SiteFeature(SiteFeatureKind.Campfire, item.Position + Vector3.UnitY * 0.7f));
                foreach (Vector3 top in chimneys)
                {
                    Vector3 scaled = top * item.Scale;
                    owner.Features.Add(new SiteFeature(SiteFeatureKind.Chimney, item.Position + Vector3.Transform(scaled, item.Rotation)));
                }
            }
        }

        private static SiteState? NearestSite(GameSession session, Vector2 position)
        {
            SiteState? nearest = null;
            float best = float.PositiveInfinity;
            foreach (SiteState state in session.Sites)
            {
                if (state.Wild && state.Site.Haunt != HauntKind.Camp) continue;
                float distance = (state.Site.Position - position).Length - state.Site.Radius;
                if (distance < best && distance < 20f)
                {
                    best = distance;
                    nearest = state;
                }
            }
            return nearest;
        }

        public static void Light(ECSWorld world, GameSession session, SiteState state)
        {
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
                        break;
                }
            }
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
                    particlesLit = true,
                },
                new PositionComponent(position),
                new DirectionNormalizedComponent(Vector3.UnitY),
                new LocalToWorldMatrixComponent());
        }

        private static Entity Flames(ECSWorld world, Vector3 position)
        {
            return world.CreateEntity("Flames",
                new SoundEmitterComponent(Soundscape.Fire, 0.55f),
                new ParticleEmitterComponent
                {
                    particlesPerSecond = 18f,
                    particleSpeed = 2.4f,
                    particleDuration = 0.8f,
                    particleSizeStart = 0.34f,
                    particleWeight = -0.25f,
                    particleDirectionError = 0.3f,
                    // Deep and dim for a flame: these are added together where they overlap, and many of them burn out to white.
                    particleColourStart = new Colour(1f, 0.34f, 0.06f, 1.3f),
                },
                new PositionComponent(position),
                new DirectionNormalizedComponent(Vector3.UnitY),
                new LocalToWorldMatrixComponent());
        }
    }
}

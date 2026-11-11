using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Stats;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.ECS.Systems;
using Dino_Engine.Networking;
using Dino_Engine.Particles;
using Dino_Engine.Particles.Content;
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
    /// It is always night in a churchyard: the day goes out as the hero nears one, and fog rises off the graves. A world with days (the Haven) turns through them on the
    /// same clock as its weather, so everyone in it shares the hour.
    /// </summary>
    public class AmbienceSystem : SystemBase
    {
        private const float RegionInterval = 0.5f;
        private const float RegionResponse = 0.5f;      // Per second.
        private const float MoteRadius = 26f;
        private const float GuideTime = 9f;
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
        private Vector3? _moteColour;
        private float _nightTarget;
        private Entity _heroLight;
        private float _moteDebt;
        private float _guideLeft;
        private float _guideDirection;
        private float _liberation;
        private Colour _sunColour;
        private double? _hourShift;

        public AmbienceSystem()
            : base(new BitMask(typeof(PlayerTag)))
        {
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime) { }

        protected override void UpdateFrame(ECSWorld world, float deltaTime)
        {
            GameSession session = GameSession.Current;
            if (session == null)
            {
                Nature.Paused = false;
                return;
            }
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
                session.GuideCooldown = Rulebook.GuideCooldown;
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
            _hourShift = null;
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

        // Full night inside the walls, and none at all once they are far enough behind: a churchyard's, and the Cathedral of the Iron Apex's, which is always seen by its candles (ARENAS.md).
        private static float NightAt(GameSession session)
        {
            if (session.Terms.Has(Omens.Night)) return 1f;

            float night = 0f;
            foreach (SiteState state in session.Sites)
            {
                if (state.Site.Arena is { Kind: ArenaKind.Cathedral } cathedral)
                {
                    float beyond = (cathedral.Centre - session.PlayerPosition).Length - cathedral.Reach;
                    float u = Math.Clamp(1f - beyond / NightReach, 0f, 1f);
                    night = MathF.Max(night, u * u * (3f - 2f * u));
                    continue;
                }
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
            light.Set(new ColorComponent(new Colour(1f, 0.86f, 0.68f, HeroLightIntensity * SkySYstem.NightOf(world).Amount)));
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
                    _moteColour = biome.MoteColour;
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

        // The weather and the day run on the server's clock, so that everyone in a world looks up at the same sky with nothing sent. Every machine works it out from the seed,
        // and from where in its sky the server began the world, which for the Haven is somewhere new every visit.
        private void UpdateWeather(ECSWorld world, GameSession session)
        {
            GameClient client = session.Client;
            double clock = (client.Render.RenderTick - client.SkyTick) * NetTime.TickSeconds + client.SkyStart + session.WeatherClockOffset;

            EntityView dayView = world.GetEntityView(world.GetSingleton<DaySingleton>());
            var day = dayView.Get<DaySingleton>();
            if (day.Cycle != null && session.ScriptedHour is float hour)
            {
                _hourShift ??= day.Cycle.ClockAt(hour) - clock % DayCycle.Length;
                clock += _hourShift.Value;
            }
            day.Clock = clock;
            day.ClockGiven = true;
            dayView.Set(day);

            EntityView view = world.GetEntityView(world.GetSingleton<WeatherSingleton>());
            var weather = view.Get<WeatherSingleton>();
            weather.Clock = clock;
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
            Nature.Paused = session.Paused;
            if (_guideLeft > 0f && !session.Paused)
            {
                float heading = MathHelper.DegreesToRadians(_guideDirection);
                Nature.Guiding = new Vector2(MathF.Sin(heading), MathF.Cos(heading));
            }

            // Leaves and petals aren't motes: they come from real trees, in WildlifeSystem.
            AmbientMotes kind = _motes;
            if (kind is AmbientMotes.None or AmbientMotes.FallingLeaves or AmbientMotes.Petals || session.Paused) return;

            MoteStyle style = StyleOf(kind);
            _moteDebt += style.Rate * LeftInTheAir(world, kind) * deltaTime;
            Vector3 centre = new Vector3(session.PlayerPosition.X, session.World.Height.Sample(session.PlayerPosition), session.PlayerPosition.Y);

            Vector2 drift = Carried(world.GetComponent<WindSingleton>(world.GetSingleton<WindSingleton>()).Velocity) * style.Drift;

            while (_moteDebt >= 1f)
            {
                _moteDebt -= 1f;
                Vector3 at = centre + new Vector3(_random.Range(-MoteRadius, MoteRadius), _random.Range(style.LowestHeight, style.HighestHeight), _random.Range(-MoteRadius, MoteRadius));
                var spawn = new ParticleSpawn(at, style.Size * _random.Range(0.7f, 1.2f), style.Life * _random.Range(0.7f, 1.2f))
                {
                    Velocity = new Vector3(drift.X + _random.Range(-0.5f, 0.5f), style.Rise + _random.Range(-0.2f, 0.2f), drift.Y + _random.Range(-0.5f, 0.5f)),
                    Tint = _moteColour is Vector3 tint ? tint / style.Look.Colour[0f] : Vector3.One,
                };
                Particles.Spawn(style.Look, spawn, ParticlePriority.Ambient);
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

        private readonly record struct MoteStyle(float Rate, float Size, float Life, float LowestHeight, float HighestHeight, float Rise, float Drift, ParticleStyle Look);

        private static MoteStyle StyleOf(AmbientMotes kind) => kind switch
        {
            AmbientMotes.Pollen => new(14f, 0.05f, 5f, 0.6f, 5f, 0.1f, 0.25f, Motes.Pollen),
            AmbientMotes.Snow => new(34f, 0.07f, 6f, 4f, 15f, -1.5f, 0.5f, Motes.Snow),
            AmbientMotes.Embers => new(16f, 0.06f, 3.2f, 0.3f, 4f, 1.4f, 0.4f, Motes.Ember),
            AmbientMotes.Fireflies => new(9f, 0.06f, 3.5f, 0.6f, 3.5f, 0.1f, 0.05f, Motes.Firefly),
            AmbientMotes.Dust => new(12f, 0.1f, 3.4f, 0.2f, 3f, 0.05f, 0.5f, Motes.Dust),
            AmbientMotes.Wisps => new(2.5f, 0.13f, 7f, 0.6f, 3f, 0.1f, 0.05f, Motes.Wisp),
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
                        break;
                }
            }
        }

        private static Entity Smoke(ECSWorld world, Vector3 position)
        {
            return world.CreateEntity("Smoke",
                new ParticleEffectComponent(Dino_Engine.Particles.Content.Smoke.Chimney, EffectShape.Point),
                new PositionComponent(position),
                new LocalToWorldMatrixComponent { value = Matrix4.CreateTranslation(position) });
        }

        // The fire burns on the bed of its logs, a little under where it's heard from.
        private static Entity Flames(ECSWorld world, Vector3 position)
        {
            Vector3 bed = position - Vector3.UnitY * 0.45f;
            return world.CreateEntity("Flames",
                new SoundEmitterComponent(Soundscape.Fire, 0.55f),
                new ParticleEffectComponent(Fire.Campfire, EffectShape.Point),
                new PositionComponent(bed),
                new LocalToWorldMatrixComponent { value = Matrix4.CreateTranslation(bed) });
        }
    }
}

using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Physics;
using Dino_Engine.Rendering.Renderers.Lighting;
using Dino_Engine.Rendering.Renderers.Simulation;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    /// <summary>
    /// Advances the weather (<see cref="WeatherSingleton"/>): reads the world's <see cref="WeatherField"/> around the camera, moves what the air carries, lets lightning fall, and
    /// hands the frame's weather to the <see cref="WeatherRenderer"/>. The map the GPU reads covers the whole world and is baked on a worker every so often, then eased towards, so
    /// a frame only ever costs a blend of a few thousand numbers. Runs before WindSystem and everything else that reads the weather.
    ///
    /// As with the wind, whatever the weather moves keeps a running total here and adds to it: nothing multiplies a clock by a live speed.
    /// </summary>
    public class WeatherSystem : SystemBase
    {
        private const float MaxStep = 0.1f;             // Frame times above this are hitches, and a stall mustn't fling the rain ahead.
        private const float BakeInterval = 0.75f;       // Seconds between looks at the whole world.
        private const float MapResponse = 1.2f;         // Seconds the map takes to settle on what the field says.
        private const float HereResponse = 1.5f;        // And what's seen and heard, at the focus.
        private const float HereInterval = 0.2f;

        // Every drop falls at one of these shares of the speed, in eighths, so wrapping the total at eight volumes moves no drop at all. Must match Precipitation.vert.
        private const float FallPeriodVolumes = 8f;
        private const float SplashPeriod = 64f;         // Seconds: a whole number of every splash's life.
        private const float RipplePeriod = 64f;
        private const float DripPeriod = 6930f;         // Seconds: a whole number of every period from 1 to 12 quarters, the longest Weather/Drip.glsl can draw.

        private const float BoltLife = 0.9f;            // Seconds a bolt is drawn, with its restrikes.
        private const float FlashDecay = 0.055f;        // Seconds each stroke's light takes to fall away.
        private const int MaxBolts = 6;
        private const float StrikeLight = 9000f;        // What a bolt gives the ground, at one metre.
        private const float StrikeReach = 110f;         // Metres.
        private const float StrikeHeight = 9f;          // Metres up its channel that the light seems to come from.

        private static readonly Vector3 FlashColour = new Vector3(0.72f, 0.8f, 1f);

        private WeatherField? _field;
        private readonly float[] _live = new float[WeatherField.MapResolution * WeatherField.MapResolution * WeatherField.MapChannels];
        private float[] _target = new float[WeatherField.MapResolution * WeatherField.MapResolution * WeatherField.MapChannels];
        private float[] _baking = new float[WeatherField.MapResolution * WeatherField.MapResolution * WeatherField.MapChannels];
        private Task? _bake;
        private float _nextBake;
        private float _nextHere;
        private WeatherSample _exact = new WeatherSample { Gale = 1f };
        private double _lastClock;
        private double _forcedStrike = 2.0;
        private readonly WorldRandom _forcedRandom = new WorldRandom(0x5702);
        private readonly List<LightningStrike> _strikes = new();

        public WeatherSystem()
            : base(new BitMask(typeof(WeatherSingleton)))
        {
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            var weather = entity.Get<WeatherSingleton>();
            weather.Bolts ??= new List<Bolt>();
            weather.Struck ??= new List<LightningStrike>();
            float dt = Math.Clamp(deltaTime, 0f, MaxStep);

            if (!weather.Enabled || (weather.Field == null && weather.Forced == null))
            {
                weather.Here = new WeatherSample { Gale = 1f };
                weather.Flash = Vector3.Zero;
                weather.Bolts.Clear();
                weather.Struck.Clear();
                entity.Set(weather);
                Engine.RenderEngine._weatherRenderer.SubmitCommand(new WeatherRenderCommand { weather = weather, map = null, deltaTime = dt });
                return;
            }

            if (!weather.ClockGiven) weather.Clock += dt;
            bool freshWorld = weather.Field != _field;
            if (freshWorld) Begin(weather);

            Vector3 focus = FocusOf(world);
            UpdateMap(weather, dt);
            UpdateHere(ref weather, focus.Xz, dt, freshWorld);
            Advance(ref weather, Wind.MeanVelocity(world), dt);
            UpdateLightning(ref weather, world, focus, dt);

            entity.Set(weather);

            Engine.RenderEngine._weatherRenderer.SubmitCommand(new WeatherRenderCommand
            {
                weather = weather,
                map = _live,
                mapSize = _field?.Size ?? 0f,
                focus = focus,
                wind = Wind.MeanVelocity(world),
                deltaTime = dt,
            });
        }

        // What's being looked at, down on the ground: all of this is about what can be seen, not about where the camera happens to hang.
        private static Vector3 FocusOf(ECSWorld world)
        {
            var context = Engine.RenderEngine.context;
            if (context.focusPoint.W > 0f) return context.focusPoint.Xyz;
            return world.Camera.IsValid() ? world.GetComponent<LocalToWorldMatrixComponent>(world.Camera).value.ExtractTranslation() : Vector3.Zero;
        }

        private void Begin(in WeatherSingleton weather)
        {
            _bake?.Wait();
            _bake = null;
            _field = weather.Field;
            _lastClock = weather.Clock;
            _nextBake = 0f;
            _nextHere = 0f;
            if (_field != null)
            {
                _field.Bake(weather.Clock, _target);
                Array.Copy(_target, _live, _live.Length);
            }
        }

        private void UpdateMap(in WeatherSingleton weather, float dt)
        {
            if (weather.Forced is WeatherSample forced)
            {
                for (int i = 0; i < _live.Length; i += WeatherField.MapChannels) WeatherField.Write(forced, _live, i);
                return;
            }

            if (_bake is { IsCompleted: true })
            {
                _bake = null;
                (_target, _baking) = (_baking, _target);
            }

            _nextBake -= dt;
            if (_bake == null && _nextBake <= 0f && _field != null)
            {
                _nextBake = BakeInterval;
                WeatherField field = _field;
                double clock = weather.Clock;
                float[] into = _baking;
                _bake = Task.Run(() => field.Bake(clock, into));
            }

            float blend = 1f - MathF.Exp(-dt / MapResponse);
            for (int i = 0; i < _live.Length; i++) _live[i] += (_target[i] - _live[i]) * blend;
        }

        private void UpdateHere(ref WeatherSingleton weather, Vector2 focus, float dt, bool freshWorld)
        {
            _nextHere -= dt;
            if (_nextHere <= 0f)
            {
                _nextHere = HereInterval;
                _exact = weather.Forced ?? _field?.Sample(focus, weather.Clock) ?? new WeatherSample { Gale = 1f };
            }

            if (freshWorld)
            {
                weather.Here = _exact;
                return;
            }

            float blend = 1f - MathF.Exp(-dt / HereResponse);
            ref WeatherSample here = ref weather.Here;
            here.Cloud += (_exact.Cloud - here.Cloud) * blend;
            here.Fall += (_exact.Fall - here.Fall) * blend;
            here.Storm += (_exact.Storm - here.Storm) * blend;
            here.Snow += (_exact.Snow - here.Snow) * blend;
            here.Ash += (_exact.Ash - here.Ash) * blend;
            here.Dust += (_exact.Dust - here.Dust) * blend;
            here.Mist += (_exact.Mist - here.Mist) * blend;
            here.Wet += (_exact.Wet - here.Wet) * blend;
            here.Flood += (_exact.Flood - here.Flood) * blend;
            here.SnowCover += (_exact.SnowCover - here.SnowCover) * blend;
            here.Gale += (_exact.Gale - here.Gale) * blend;
        }

        /// <summary>One step of what the air carries: the clouds overhead, and everything falling through it.</summary>
        public static void Advance(ref WeatherSingleton weather, Vector2 wind, float dt)
        {
            float cloudSize = MathF.Max(weather.CloudSize, 1f);
            Vector2 cloudVelocity = (weather.Field?.Drift ?? Vector2.UnitY * 3f) * weather.CloudSpeed;
            weather.CloudOffset = Wrap(weather.CloudOffset - cloudVelocity * (dt / cloudSize));
            weather.CloudPhase = WrapTo(weather.CloudPhase + dt * 0.0035f, 1f);

            float volume = MathF.Max(weather.VolumeSize, 1f);
            weather.RainFallen = WrapTo(weather.RainFallen + MathF.Max(weather.RainSpeed, 0f) * dt, volume * FallPeriodVolumes);
            weather.SnowFallen = WrapTo(weather.SnowFallen + MathF.Max(weather.SnowSpeed, 0f) * dt, volume * FallPeriodVolumes);
            weather.AirDrift = new Vector2(WrapTo(weather.AirDrift.X + wind.X * dt, volume), WrapTo(weather.AirDrift.Y + wind.Y * dt, volume));
            weather.SplashClock = WrapTo(weather.SplashClock + dt, SplashPeriod);
            weather.RippleClock = WrapTo(weather.RippleClock + dt, RipplePeriod);
            weather.DripClock = WrapTo(weather.DripClock + dt, DripPeriod);
        }

        private void UpdateLightning(ref WeatherSingleton weather, ECSWorld world, Vector3 focus, float dt)
        {
            weather.Struck.Clear();
            if (weather.Forced is WeatherSample forced) ForcedStrikes(ref weather, forced, focus.Xz, dt);
            else if (_field != null && weather.Clock > _lastClock)
            {
                _strikes.Clear();
                // Never further back than a hitch can excuse: a clock that jumps shouldn't bring on every bolt it passed.
                _field.Strikes(Math.Max(_lastClock, weather.Clock - 2.0), weather.Clock, _strikes);
                weather.Struck.AddRange(_strikes);
            }
            _lastClock = weather.Clock;

            foreach (LightningStrike strike in weather.Struck)
            {
                if ((strike.Position - focus.Xz).LengthSquared > weather.BoltReach * weather.BoltReach || weather.Bolts.Count >= MaxBolts) continue;
                float height = world.TryGetSingleton<WorldDataSingleton>(out Entity land)
                    ? world.GetComponent<WorldDataSingleton>(land).World.Height.Sample(strike.Position)
                    : focus.Y;
                weather.Bolts.Add(new Bolt { Strike = strike, Ground = new Vector3(strike.Position.X, height, strike.Position.Y) });
            }

            for (int i = weather.Bolts.Count - 1; i >= 0; i--)
            {
                Bolt bolt = weather.Bolts[i];
                bolt.Age += dt;
                if (bolt.Age >= BoltLife) weather.Bolts.RemoveAt(i);
                else weather.Bolts[i] = bolt;
            }

            // The sky lights up for bolts near and far, and the further away the less of it reaches here.
            Vector3 towards = Vector3.Zero;
            float flash = 0f;
            foreach (Bolt bolt in weather.Bolts)
            {
                float strength = BoltLight(bolt.Strike, bolt.Age);
                float distance = (bolt.Strike.Position - focus.Xz).Length;
                strength *= 1f / (1f + distance * distance / (160f * 160f));
                flash += strength;
                Vector3 to = bolt.Ground + Vector3.UnitY * weather.CloudAltitude * 0.5f - focus;
                towards += to.Normalized() * strength;
            }
            // Lightning is the sky's light, and goes out with it.
            bool lit = Engine.RenderEngine.Lighting.Sky;
            weather.Flash = lit ? FlashColour * (flash * weather.FlashPower) : Vector3.Zero;
            weather.FlashFrom = towards.LengthSquared > 1e-8f ? towards.Normalized() : Vector3.UnitY;
            if (weather.Flash == Vector3.Zero) return;

            var light = new DirectionallightRenderCommand { colour = weather.Flash * 0.6f, direction = -weather.FlashFrom, cascades = Array.Empty<Shadow>() };
            Engine.RenderEngine._directionalLightRenderer.SubmitCommand(light);
            Engine.RenderEngine._volumetricFogRenderer.SubmitDirectionalLight(light);

            // And where it comes down, the ground lights up around it.
            foreach (Bolt bolt in weather.Bolts)
            {
                float strength = BoltLight(bolt.Strike, bolt.Age);
                if (strength <= 0.01f) continue;
                var strike = new PointlightRenderCommand
                {
                    colour = FlashColour * (strength * StrikeLight),
                    radius = StrikeReach,
                    fill = 0.2f,
                    positionWorld = bolt.Ground + Vector3.UnitY * StrikeHeight,
                };
                Engine.RenderEngine._pointLightRenderer.SubmitCommand(strike);
                Engine.RenderEngine._waterRenderer.SubmitPointLight(strike);
                Engine.RenderEngine._volumetricFogRenderer.SubmitPointLight(strike);
                Engine.RenderEngine._globalIllumination.SubmitPointLight(strike);
            }
        }

        // A storm on order brings its bolts near enough to look at. Real ones are drawn from the field, the same for everybody.
        private void ForcedStrikes(ref WeatherSingleton weather, in WeatherSample forced, Vector2 focus, float dt)
        {
            if (forced.Storm <= 0f) return;
            _forcedStrike -= dt;
            if (_forcedStrike > 0.0) return;
            _forcedStrike = _forcedRandom.Range(5f, 14f) / forced.Storm;
            float angle = _forcedRandom.Float() * MathF.Tau;
            float reach = _forcedRandom.Range(6f, 70f);
            Vector2 at = focus + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * reach;
            weather.Struck.Add(new LightningStrike(weather.Clock, at, _forcedRandom.Range(0.5f, 1f), (uint)_forcedRandom.NextULong()));
        }

        /// <summary>
        /// A bolt's light over time, 0..1. A flash is several strokes down one channel, each a few tens of milliseconds behind the last, and that flicker is what's seen on the land.
        /// Drawn from the strike itself, so its shape in the sky and its light on the ground are the same event.
        /// </summary>
        public static float BoltLight(in LightningStrike strike, float age)
        {
            uint hash = strike.Seed;
            float at = 0f;
            float light = 0f;
            int strokes = 2 + (int)(hash % 3u);
            for (int i = 0; i < strokes; i++)
            {
                float since = age - at;
                float strength = strike.Power * (i == 0 ? 1f : 0.75f - 0.1f * i);
                if (since >= 0f) light = MathF.Max(light, strength * MathF.Exp(-since / FlashDecay));
                hash = hash * 1664525u + 1013904223u;
                at += 0.05f + (hash >> 8) * (1f / 16777216f) * 0.13f;
            }
            return light;
        }

        private static float WrapTo(float value, float period) => value - period * MathF.Floor(value / period);

        private static Vector2 Wrap(Vector2 value) => new Vector2(value.X - MathF.Floor(value.X), value.Y - MathF.Floor(value.Y));
    }
}

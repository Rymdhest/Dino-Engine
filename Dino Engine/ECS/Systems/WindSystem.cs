using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Physics;
using Dino_Engine.Rendering.Renderers.Simulation;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    /// <summary>
    /// Advances the wind (<see cref="WindSingleton"/>): eases the live wind towards its settings, carries the gust,
    /// turbulence and wave patterns along with it, ages blasts, gathers emitters, and hands the frame's wind to the
    /// <see cref="WindRenderer"/>. Runs before every system that reads the wind.
    ///
    /// Everything that moves with the wind keeps its own running total here and adds to it each step, at whatever rate the
    /// wind asks for now. Nothing may multiply a clock by a live speed: the phase would then jump by the whole of the time
    /// so far at every change, and the longer the game ran the harder the field would strobe whenever the wind changed.
    /// </summary>
    public class WindSystem : SystemBase
    {
        // Rates at which the patterns change shape, in noise cells per second: a base rate plus a part that grows
        // with how fast the wind sweeps cells past.
        private const float GustEvolutionBase = 0.04f;
        private const float GustEvolutionPerSweep = 0.1f;
        private const float TurbulenceEvolutionBase = 0.05f;
        private const float TurbulenceEvolutionPerSweep = 0.1f;

        private const float WaveWanderRate = 0.03f;     // Cycles per second that the fronts of the waves waver.
        private const float FlutterRate = 0.8f;         // Flutters per second in still air, for the slowest tufts. The quickest go 1.75 times as fast.
        private const float FlutterRatePerSpeed = 0.03f;
        private const float FlutterRateLimit = 1.1f;

        // Frame times above this are treated as hitches, so a stall doesn't fling the patterns ahead.
        private const float MaxStep = 0.1f;

        public WindSystem()
            : base(new BitMask(typeof(WindSingleton)))
        {
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            var wind = entity.Get<WindSingleton>();
            wind.Blasts ??= new List<WindEmitter>();
            wind.Emitters ??= new List<WindEmitter>();
            float dt = Math.Clamp(deltaTime, 0f, MaxStep);

            Advance(ref wind, dt);

            for (int i = wind.Blasts.Count - 1; i >= 0; i--)
            {
                WindEmitter blast = wind.Blasts[i];
                blast.Age += dt;
                if (blast.Age >= blast.Duration) wind.Blasts.RemoveAt(i);
                else wind.Blasts[i] = blast;
            }

            wind.Emitters.Clear();
            wind.Emitters.AddRange(wind.Blasts);
            CollectEmitterComponents(world, wind.Emitters);

            entity.Set(wind);

            if (!world.Camera.IsValid()) return;
            Engine.RenderEngine._windRenderer.SubmitCommand(new WindRenderCommand
            {
                wind = wind,
                emitters = wind.Emitters.ToArray(),
                center = RegionCenter(world),
                deltaTime = dt,
            });
        }

        /// <summary>One step of the ambient wind: the live speed and heading, and the patterns they carry along.</summary>
        public static void Advance(ref WindSingleton wind, float dt)
        {
            float heading = MathHelper.DegreesToRadians(wind.Direction);
            float targetSpeed = MathF.Max(wind.Speed, 0f);
            if (!wind.Initialized)
            {
                wind.Heading = heading;
                wind.LiveSpeed = targetSpeed;
                wind.Prevailing = WindSingleton.Towards(heading);
                wind.Initialized = true;
            }
            else
            {
                // Speed and heading ease apart, so that a wind turning right round swings through the quarters at full
                // strength rather than dying away and springing up again. They start and finish gently, without a kick.
                float shortWay = wind.Heading + WrapAngle(heading - wind.Heading);
                wind.Heading = WrapAngle(SmoothDamp(wind.Heading, shortWay, ref wind.HeadingRate, wind.ResponseTime, dt));
                wind.LiveSpeed = MathF.Max(SmoothDamp(wind.LiveSpeed, targetSpeed, ref wind.LiveSpeedRate, wind.ResponseTime, dt), 0f);
            }
            Vector2 along = WindSingleton.Towards(wind.Heading);
            wind.Velocity = along * wind.LiveSpeed;

            // The patterns are carried along by the live wind (frozen turbulence), so changing the wind never makes
            // them jump: only how fast they drift changes.
            float drift = WindField.PatternSpeed(wind.LiveSpeed);
            float inverseGustScale = WindField.InverseScale(wind.GustScale);
            float inverseTurbulenceScale = WindField.InverseScale(wind.TurbulenceScale);
            wind.GustOffset = Wrap(wind.GustOffset + along * (drift * dt * inverseGustScale));
            wind.GustPhase = WindField.WrapToPeriod(wind.GustPhase + dt * (GustEvolutionBase + GustEvolutionPerSweep * drift * inverseGustScale));
            wind.TurbulenceOffset = Wrap(wind.TurbulenceOffset + along * (drift * dt * inverseTurbulenceScale));
            wind.TurbulencePhase = WindField.WrapToPeriod(wind.TurbulencePhase + dt * (TurbulenceEvolutionBase + TurbulenceEvolutionPerSweep * drift * inverseTurbulenceScale));

            wind.WaveCycles = WrapTo(wind.WaveCycles + dt * drift * WindField.InverseScale(wind.WaveLength), WindField.WaveCyclesPeriod);
            wind.WaveWander = WrapTo(wind.WaveWander + dt * WaveWanderRate, 1f);
            float flutterRate = MathF.Min(FlutterRate + FlutterRatePerSpeed * wind.LiveSpeed, FlutterRateLimit);
            wind.FlutterCycles = WrapTo(wind.FlutterCycles + dt * flutterRate, WindField.FlutterCyclesPeriod);
        }

        /// <summary>Follows a moving target at a critically damped pace, without overshoot. After Lowe, Game Programming Gems 4.</summary>
        private static float SmoothDamp(float value, float target, ref float rate, float smoothTime, float dt)
        {
            float omega = 2f / MathF.Max(smoothTime, 1e-3f);
            float x = omega * dt;
            float decay = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
            float offset = value - target;
            float carried = (rate + omega * offset) * dt;
            rate = (rate - omega * carried) * decay;
            return target + (offset + carried) * decay;
        }

        private static float WrapAngle(float angle) => angle - MathF.Tau * MathF.Floor(angle / MathF.Tau + 0.5f);

        private static float WrapTo(float value, float period) => value - period * MathF.Floor(value / period);

        private static Vector2 Wrap(Vector2 offset)
        {
            return new Vector2(WindField.WrapToPeriod(offset.X), WindField.WrapToPeriod(offset.Y));
        }

        private static void CollectEmitterComponents(ECSWorld world, List<WindEmitter> emitters)
        {
            var query = world.QueryArchetypes(new BitMask(typeof(WindEmitterComponent), typeof(LocalToWorldMatrixComponent)), BitMask.Empty);
            foreach (var archetype in query)
            {
                var emitterArray = archetype.GetComponentArray<WindEmitterComponent>();
                var matrixArray = archetype.GetComponentArray<LocalToWorldMatrixComponent>();
                for (int i = 0; i < archetype.EntityCount; i++)
                {
                    WindEmitterComponent emitter = emitterArray[i];
                    Matrix4 localToWorld = matrixArray[i].value;
                    Vector3 direction = (new Vector4(emitter.Direction, 0f) * localToWorld).Xyz;
                    emitters.Add(new WindEmitter
                    {
                        Type = emitter.Type,
                        Position = localToWorld.ExtractTranslation(),
                        Direction = direction.LengthSquared > 1e-12f ? direction.Normalized() : Vector3.Zero,
                        Radius = emitter.Radius,
                        Strength = emitter.Strength,
                    });
                }
            }
        }

        /// <summary>A little ahead of the camera, where most of a top-down view is.</summary>
        private static Vector2 RegionCenter(ECSWorld world)
        {
            Vector3 cameraPosition = world.GetComponent<LocalToWorldMatrixComponent>(world.Camera).value.ExtractTranslation();
            Matrix4 invView = Matrix4.Invert(world.GetComponent<ViewMatrixComponent>(world.Camera).value);
            Vector3 forward = (new Vector4(0f, 0f, -1f, 0f) * invView).Xyz.Normalized();
            return cameraPosition.Xz + new Vector2(forward.X, forward.Z) * (WindRenderer.VegetationSize * 0.25f);
        }
    }
}

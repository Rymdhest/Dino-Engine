using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Physics;
using Dino_Engine.Rendering.Renderers.Simulation;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    /// <summary>
    /// Advances the wind (<see cref="WindSingleton"/>): eases the live wind towards its settings, carries the gust
    /// and turbulence patterns along with it, ages blasts, gathers emitters, and hands the frame's wind to the
    /// <see cref="WindRenderer"/>. Runs before every system that reads the wind.
    /// </summary>
    public class WindSystem : SystemBase
    {
        // Rates at which the patterns change shape, in noise cells per second: a base rate plus a part that grows
        // with how fast the wind sweeps cells past.
        private const float GustEvolutionBase = 0.05f;
        private const float GustEvolutionPerSweep = 0.15f;
        private const float TurbulenceEvolutionBase = 0.1f;
        private const float TurbulenceEvolutionPerSweep = 0.5f;

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

            Vector2 target = wind.TargetVelocity;
            if (!wind.Initialized)
            {
                wind.Velocity = target;
                wind.Initialized = true;
            }
            else
            {
                wind.Velocity += (target - wind.Velocity) * (1f - MathF.Exp(-dt / MathF.Max(wind.ResponseTime, 1e-3f)));
            }

            // The patterns are carried along by the live wind (frozen turbulence), so changing the wind never makes
            // them jump: only how fast they drift changes.
            float speed = wind.Velocity.Length;
            float inverseGustScale = WindField.InverseScale(wind.GustScale);
            float inverseTurbulenceScale = WindField.InverseScale(wind.TurbulenceScale);
            wind.GustOffset = Wrap(wind.GustOffset + wind.Velocity * (dt * inverseGustScale));
            wind.GustPhase = WindField.WrapToPeriod(wind.GustPhase + dt * (GustEvolutionBase + GustEvolutionPerSweep * speed * inverseGustScale));
            wind.TurbulenceOffset = Wrap(wind.TurbulenceOffset + wind.Velocity * (dt * inverseTurbulenceScale));
            wind.TurbulencePhase = WindField.WrapToPeriod(wind.TurbulencePhase + dt * (TurbulenceEvolutionBase + TurbulenceEvolutionPerSweep * speed * inverseTurbulenceScale));

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

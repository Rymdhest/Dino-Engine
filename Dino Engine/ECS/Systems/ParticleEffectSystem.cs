using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Particles;
using Dino_Engine.Physics;
using Dino_Engine.Rendering;
using Dino_Engine.Rendering.Renderers.Lighting;
using Dino_Engine.Util;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    /// <summary>
    /// Runs every effect: those on entities (<see cref="ParticleEffectComponent"/>), following them about, and those playing on their own (<see cref="Particles.Particles.Play"/>).
    /// An effect that goes on emits nothing while it can't be seen. Each gives its light to the same passes the light systems give theirs to, the
    /// <see cref="MostLights"/> nearest the hero at most: a light costs its circle on screen, and a fight full of burning things would otherwise pay for
    /// dozens. Those beyond fade out towards where the last one kept is, which moves smoothly however things move, so none of them blinks.
    /// </summary>
    public class ParticleEffectSystem : SystemBase
    {
        public const int MostLights = 12;
        // The share of the way out to the farthest light kept over which lights fade.
        private const float LightFade = 0.25f;

        private static readonly Comparison<Shining> Nearest = (a, b) => a.Distance.CompareTo(b.Distance);
        private readonly List<Shining> _lights = new List<Shining>();
        private readonly LandGround _land = new LandGround();
        private Frustum _view = new Frustum(Matrix4.Identity);
        private ECSWorld? _world;
        private PlacementIndex? _placement;
        private const float LightClearance = 0.7f;  // Metres an effect's light keeps off anything solid taller than it, so it falls on a face rather than grazing it.

        public ParticleEffectSystem()
            : base(new BitMask(typeof(ParticleEffectComponent), typeof(LocalToWorldMatrixComponent)))
        {
        }

        protected override void UpdateFrame(ECSWorld world, float deltaTime)
        {
            _world = world;
            if (world.TryGetSingleton<WorldDataSingleton>(out Entity data))
            {
                WorldData land = world.GetComponent<WorldDataSingleton>(data).World;
                _land.Height = land.Height;
                _placement = land.Placement;
                Particles.Particles.Ground = _land;
            }
            else
            {
                Particles.Particles.Ground = null;
                _placement = null;
            }

            _view = new Frustum(Engine.RenderEngine.context.viewMatrix * Engine.RenderEngine.context.projectionMatrix);
            float step = Particles.Particles.Paused ? 0f : deltaTime;
            _lights.Clear();

            base.UpdateFrame(world, step);

            List<EffectRun> playing = Particles.Particles.Playing;
            for (int i = playing.Count - 1; i >= 0; i--)
            {
                EffectRun run = playing[i];
                Run(run, step);
                if (run.Finished) playing.RemoveAt(i);
            }

            SubmitLights();
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            EffectRun run = entity.Get<ParticleEffectComponent>().Run;
            if (run == null) return;
            run.Position = entity.Get<LocalToWorldMatrixComponent>().value.ExtractTranslation();
            run.Velocity = entity.Has<VelocityComponent>() ? entity.Get<VelocityComponent>().value : Vector3.Zero;
            Run(run, deltaTime);
        }

        private void Run(EffectRun run, float step)
        {
            bool visible = _view.IntersectsSphere(run.Middle, run.Reach);
            Vector3 air = run.Effect.Lasts && visible && _world != null ? Wind.Sample(_world, run.Position).Velocity : Vector3.Zero;
            run.Step(step, visible, air);
            if (visible) Light(run);
        }

        private void Light(EffectRun run)
        {
            LightingSettings lighting = Engine.RenderEngine.Lighting;
            if (!lighting.LocalLights || !run.LightNow(Engine.Time, out Vector3 position, out Vector3 colour, out float radius)) return;

            // What goes off against a wall shines on its face: a light inside something solid lights it from within, through the stone, and nothing near it. Only for
            // what happens once and for a patch of ground, which goes wherever a blow lands: a lasting flame on a point was put where it is, as a lantern's is against its post.
            if (_placement != null && (!run.Effect.Lasts || run.Shape.Kind == EffectShapeKind.Area))
            {
                float above = position.Y - _land.HeightAt(position.X, position.Z);
                Vector2 clear = _placement.ClearOfSolids(position.Xz, LightClearance, above);
                position = new Vector3(clear.X, position.Y, clear.Y);
            }

            ref ShaderGlobals context = ref Engine.RenderEngine.context;
            Vector3 focus = context.focusPoint.W > 0f ? context.focusPoint.Xyz : context.viewPos;
            var command = new PointlightRenderCommand
            {
                colour = colour * lighting.LocalLightScale,
                radius = radius,
                fill = run.Effect.Light!.Fill,
                positionWorld = position,
            };
            _lights.Add(new Shining(command, run.Effect.Light.LightsFog, (position - focus).Length));
        }

        private void SubmitLights()
        {
            float cutoff = float.MaxValue;
            if (_lights.Count > MostLights)
            {
                _lights.Sort(Nearest);
                cutoff = _lights[MostLights].Distance;
            }

            foreach (Shining light in _lights)
            {
                float weight = cutoff == float.MaxValue ? 1f : Math.Clamp((cutoff - light.Distance) / (cutoff * LightFade), 0f, 1f);
                if (weight <= 0f) continue;
                PointlightRenderCommand command = light.Command;
                command.colour *= weight;
                Engine.RenderEngine._pointLightRenderer.SubmitCommand(command);
                if (light.Fog) Engine.RenderEngine._volumetricFogRenderer.SubmitPointLight(command);
                Engine.RenderEngine._waterRenderer.SubmitPointLight(command);
                Engine.RenderEngine._globalIllumination.SubmitPointLight(command);
                Engine.RenderEngine._particleRenderer.SubmitPointLight(command);
            }
        }

        private readonly record struct Shining(PointlightRenderCommand Command, bool Fog, float Distance);

        private sealed class LandGround : IParticleGround
        {
            public ScalarField? Height;

            public float HeightAt(float x, float z) => Height?.Sample(x, z) ?? 0f;
        }
    }
}

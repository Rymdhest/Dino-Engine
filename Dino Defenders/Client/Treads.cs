using Dino_Engine.Animation;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.ECS.Systems;
using Dino_Engine.Particles;
using Dino_Engine.Particles.Content;
using Dino_Engine.Physics;
using Dino_Engine.Sound.Content;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Defenders.Client
{
    /// <summary>
    /// Something heavy enough that each of its feet is felt where it lands, as a giant's are (GiantShow): a ring of wind out through the grass round the foot, and a little of the ground thrown up.
    /// How far the ring goes is <see cref="Reach"/>, in metres, and how much is thrown up <see cref="Puff"/>, as a scale for its particles.
    /// </summary>
    public struct TreadComponent : IComponent
    {
        public float Reach;
        public float Puff;

        public TreadComponent(float reach, float puff)
        {
            Reach = reach;
            Puff = puff;
        }
    }

    /// <summary>What a heavy foot does where it comes down, for the giants and for anything that big the server sends, read off the body that came down, so that it can't drift from what's seen.</summary>
    public static class Treads
    {
        /// <summary>How high its hips stand, at the size it's drawn, before a body's feet are felt as well as heard: a little under twice a person's.</summary>
        public const float FeltFrom = 3.6f;

        public const float FootShare = 0.34f;   // As PlantContactSystem has it: how wide a foot's mark is, of the body's.
        private const float Gust = 5.5f;        // The ring is a wind this many metres a second at its front,
        private const float For = 0.7f;         // for this long: under anything that breaks what it reaches (BreakageSystem.BreakingWind).

        /// <summary>How far round a foot it presses the grass, for a body whose hips stand this high: well past the foot itself, as a weight that size flattens what's round it.</summary>
        public static float FootMarkFor(float hips) => hips * 0.36f;

        /// <summary>How far the ring a foot sends through the grass goes, for a body whose hips stand this high.</summary>
        public static float ReachFor(float hips) => hips * 0.92f;

        /// <summary>A foot that came down: a ring of wind through the grass round it, and a little of the ground thrown up, more off bare ground than turf, or a splash if it was in water.</summary>
        public static void Felt(ECSWorld ecs, WorldData? world, Vector3 at, float reach, float puff)
        {
            Wind.AddBlast(ecs, at, reach, Gust, For);
            if (world == null) return;
            float depth = world.WaterDepthAt(at.Xz);
            if (depth > 0.02f)
            {
                Water.AddSplash(ecs, at, reach * 0.55f, MathF.Min(depth * 2f, 1f));
                return;
            }
            bool turf = Footsteps.GroundOf(world.SurfaceAt(at.Xz)) == Ground.Grass;
            Particles.Play(Smoke.Puff, at, (turf ? 0.45f : 0.65f) * puff, EffectShape.Ball(1.35f * puff), strength: turf ? 0.35f : 0.75f);
        }
    }

    /// <summary>Feels every heavy foot that came down this frame (TreadComponent). The dead have no feet to come down.</summary>
    public sealed class TreadSystem : SystemBase
    {
        private readonly BitMask _treads = new BitMask(typeof(TreadComponent), typeof(AnimatorComponent));

        public TreadSystem() : base(new BitMask(typeof(TreadComponent))) { }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime) { }

        protected override void UpdateFrame(ECSWorld world, float deltaTime)
        {
            if (deltaTime <= 0f) return;
            WorldData? land = Gameplay.GameSession.Current?.World;
            foreach (Archetype archetype in world.QueryArchetypes(_treads, BitMask.Empty))
            {
                var treads = archetype.GetComponentArray<TreadComponent>()!;
                var animators = archetype.GetComponentArray<AnimatorComponent>()!;
                for (int i = 0; i < archetype.EntityCount; i++)
                {
                    CharacterAnimator animator = animators[i].Animator;
                    int landed = animator.Footfalls;
                    if (landed == 0 || animator.Dead) continue;
                    Foot[] feet = animator.Gait.Feet;
                    for (int f = 0; f < feet.Length; f++)
                    {
                        if ((landed & (1 << f)) != 0) Treads.Felt(world, land, feet[f].At, treads[i].Reach, treads[i].Puff);
                    }
                }
            }
        }
    }
}

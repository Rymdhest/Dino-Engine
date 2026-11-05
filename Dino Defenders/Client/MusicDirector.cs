using Dino_Defenders.Gameplay;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Sound.Music;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Defenders.Client
{
    /// <summary>
    /// Decides what the music is about. It reads the game as a film composer reads a scene: who's nearby and whether they've noticed, where the hero is, and how badly it's going.
    /// The mood is what kind of music it is, and intensity how hard it's playing. Everything here is what this player can see, so friends in a world each hear their own score.
    /// </summary>
    public class MusicDirectorSystem : SystemBase
    {
        private const float Interval = 0.2f;
        private const float BattleHolds = 5f;           // Seconds a fight stays a fight once nobody's chasing: the moment between two enemies isn't peace.
        private const float NoticeReach = 34f;          // Metres within which an unaware enemy is felt.
        private const float BossReach = 60f;
        private const float RiseSpeed = 0.9f;           // Per second: a fight flares up at once.
        private const float FallSpeed = 0.12f;          // And it takes a while for the heart to stop racing.

        private readonly BitMask _enemies = new BitMask(typeof(EnemyComponent), typeof(PositionComponent));
        private GameSession? _session;
        private MoodHold _hold = new();
        private float _next;
        private float _intensity;
        private float _fightLeft;
        private bool _bossFought;
        private bool _wasDown;

        public MusicDirectorSystem()
            : base(new BitMask(typeof(PlayerTag)))
        {
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime) { }

        protected override void UpdateFrame(ECSWorld world, float deltaTime)
        {
            GameSession session = GameSession.Current;
            if (session == null) return;
            if (session != _session)
            {
                _session = session;
                _hold = new MoodHold();
                _intensity = 0f;
                _fightLeft = 0f;
                _bossFought = false;
                _wasDown = false;

                // A world's score comes from its seed, so a place keeps its music from one visit to the next.
                Music.Begin((ulong)(uint)session.World.Plan.Seed * 2654435761UL + 11UL);
            }

            _next -= deltaTime;
            if (_next > 0f) return;
            _next = Interval;
            if (!Music.Manual) Direct(world, session);
        }

        private void Direct(ECSWorld world, GameSession session)
        {
            Vector2 at = session.PlayerPosition;
            float closest = float.MaxValue;
            float pressure = 0f;
            bool boss = false;

            foreach (Archetype archetype in world.QueryArchetypes(_enemies, BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    var enemy = entity.Get<EnemyComponent>();
                    if (enemy.State == EnemyState.Dead) continue;

                    float distance = (entity.Get<PositionComponent>().value.Xz - at).Length;
                    bool engaged = enemy.State is EnemyState.Chasing or EnemyState.WindingUp or EnemyState.Recovering;
                    if (engaged) pressure += enemy.Rank switch { EnemyRank.Boss => 1f, EnemyRank.Elite => 0.5f, _ => 0.26f };
                    if (enemy.Rank == EnemyRank.Boss && distance < BossReach) boss = true;
                    closest = MathF.Min(closest, distance);
                }
            }

            bool down = session.Health <= 0f;
            if (down && !_wasDown) Music.Play(MusicCue.Defeat);
            _wasDown = down;

            // The boss has fallen when there was one and there isn't any more.
            if (_bossFought && !boss && !down)
            {
                Music.Play(MusicCue.Victory);
                _bossFought = false;
            }
            _bossFought |= boss && pressure > 0f;

            if (pressure > 0f) _fightLeft = BattleHolds;
            else _fightLeft = MathF.Max(_fightLeft - Interval, 0f);

            // Being hurt raises the stakes as much as more enemies do.
            float hurt = 1f - Math.Clamp(session.Health / MathF.Max(session.Sheet.MaxHealth, 1f), 0f, 1f);

            MusicMood mood;
            float wanted;
            if (down)
            {
                mood = MusicMood.Silence;
                wanted = 0f;
            }
            else if (boss && (pressure > 0f || _fightLeft > 0f))
            {
                mood = MusicMood.Boss;
                wanted = 0.65f + hurt * 0.35f;
            }
            else if (_fightLeft > 0f)
            {
                mood = MusicMood.Battle;
                wanted = Math.Clamp(0.25f + pressure * 0.55f + hurt * 0.4f, 0f, 1f);
            }
            else
            {
                mood = Place(world, session, out wanted);

                // Something unseen is near. Among the dead the place already says so, and better: put unease first and a churchyard, where there's always something near, would never sound like one.
                float unease = closest < NoticeReach ? Math.Clamp(1f - closest / NoticeReach, 0f, 1f) : 0f;
                if (mood == MusicMood.Haunted) wanted = MathF.Max(wanted, unease);
                else if (closest < NoticeReach)
                {
                    mood = MusicMood.Tension;
                    wanted = unease;
                }
            }

            // Up quickly, down slowly.
            float rate = wanted > _intensity ? RiseSpeed : FallSpeed;
            _intensity += Math.Clamp(wanted - _intensity, -rate * Interval, rate * Interval);

            Music.Mood = _hold.Update(mood, Interval);
            Music.Intensity = _intensity;
        }

        // With nothing around, what's playing is the place.
        private static MusicMood Place(ECSWorld world, GameSession session, out float intensity)
        {
            Vector2 at = session.PlayerPosition;
            float night = world.TryGetSingleton<NightfallSingleton>(out Entity entity) ? world.GetComponent<NightfallSingleton>(entity).Amount : 0f;
            if (night > 0.4f)
            {
                intensity = 0.2f + night * 0.5f;
                return MusicMood.Haunted;
            }

            foreach (SiteState state in session.Sites)
            {
                if (state.Wild) continue;
                float inside = (state.Site.Position - at).Length - state.Site.Radius;
                if (inside > 8f) continue;

                switch (state.Site.Kind)
                {
                    case SiteKind.Village or SiteKind.Hamlet or SiteKind.FishingHamlet or SiteKind.Farmstead or SiteKind.LumberCamp:
                        // A place set free is merrier for it.
                        intensity = state.Cleared ? 0.8f : 0.5f;
                        return MusicMood.Town;
                    case SiteKind.Shrine or SiteKind.StandingStones:
                        intensity = state.Kindled ? 0.55f : 0.25f;
                        return MusicMood.Sacred;
                }
            }

            intensity = 0.35f;
            return MusicMood.Wander;
        }
    }
}

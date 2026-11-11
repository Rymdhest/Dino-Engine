using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.ECS.Systems;
using Dino_Engine.Sound.Music;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Defenders.Client
{
    /// <summary>
    /// Decides what the music is about. A land has a piece of its own, drawn from its seed and its character, and it plays the whole time the hero is there, whatever they're doing:
    /// only the tyrant changes it, once the hero is inside the keep's walls, and it comes back when the tyrant falls. The Haven has a piece of its own, the same on every visit, and
    /// an arena, every tenth level, is one long fight. Everything here is what this player can see, so friends in a world each hear their own score.
    /// </summary>
    public class MusicDirectorSystem : SystemBase
    {
        private const float Interval = 0.2f;
        private const float KeepEnters = 6f;            // Metres past a keep's wall that count as inside it,
        private const float KeepLeaves = 22f;           // and how far out the hero has to go before it no longer does, so walking along the wall doesn't flick the music back and forth.
        private const float FloorReach = 1.5f;          // As the server counts a keep's arena's floor, a little past its bounds.
        private const float LandIntensity = 0.5f;
        private const float HavenDay = 0.62f;
        private const float HavenNight = 0.3f;
        private const float RiseSpeed = 0.9f;           // Per second: a fight flares up at once.
        private const float FallSpeed = 0.12f;          // And it takes a while for the heart to stop racing.

        private readonly BitMask _enemies = new BitMask(typeof(EnemyComponent), typeof(PositionComponent), typeof(HealthComponent));
        private GameSession? _session;
        private MoodHold _hold = new();
        private float _next;
        private float _intensity;
        private bool _wasDown;
        private bool _inKeep;
        private bool _tyrantSeen;       // A tyrant has been seen standing where it's being fought.
        private bool _won;              // And has fallen.

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
                _wasDown = false;
                _inKeep = false;
                _tyrantSeen = false;
                _won = false;
                Music.Begin(PlaceOf(session));
            }

            _next -= deltaTime;
            if (_next > 0f) return;
            _next = Interval;
            if (!Music.Manual) Direct(world, session);
        }

        /// <summary>What a land is like, as far as its music cares: from its biomes, its weather and how deep into the climb it is. Every land gets its own seed for the rest.</summary>
        public static MusicPlace PlaceOf(GameSession session) => session.IsHaven ? MusicPlace.Home : PlaceOf(session.World);

        public static MusicPlace PlaceOf(WorldData world)
        {
            WorldPlan plan = world.Plan;
            float dark = 0f, cold = 0f, open = 0f;
            int count = Math.Max(plan.Biomes.Count, 1);
            foreach (BiomeDefinition biome in plan.Biomes)
            {
                bool grim = biome.Id is "ash" or "badlands" or "darkwood" or "marsh" || biome.Ground is SurfaceMaterial.Ash or SurfaceMaterial.Mud;
                dark += (grim ? 0.8f : 0f) + Math.Clamp(biome.FogDensity * 10f, 0f, 0.3f);
                cold += biome.Ground is SurfaceMaterial.Snow or SurfaceMaterial.Ice ? 1f : Math.Clamp(0.4f - biome.Temperature, 0f, 0.4f);
                open += Math.Clamp(1f - biome.ForestCover * 1.4f, 0f, 1f) * 0.7f + Math.Clamp((biome.WindStrength - 0.8f) * 0.5f, 0f, 0.3f);
            }
            dark /= count;
            cold /= count;
            open /= count;

            // Grey and misty days are heavier; a golden evening is warmer. The deeper the climb, the darker the lands.
            string mood = plan.Mood.Name.ToLowerInvariant();
            if (mood.Contains("overcast") || mood.Contains("mist")) dark += 0.15f;
            if (mood.Contains("golden") || mood.Contains("summer")) dark -= 0.1f;
            if (mood.Contains("windswept")) open += 0.15f;
            dark += Math.Min(plan.Level, 60) / 300f;

            var place = new MusicPlace
            {
                Seed = (ulong)(uint)plan.Seed * 2654435761UL + 11UL,
                Arena = plan.Archetype.Arena,
                Dark = Math.Clamp(dark, 0f, 1f),
                Cold = Math.Clamp(cold, 0f, 1f),
                Open = Math.Clamp(open, 0f, 1f),
                Level = plan.Level,
            };
            foreach (WorldSite site in world.Sites)
            {
                if (site.Arena is { Keep: false } arena) place.ArenaKind = arena.Kind;
            }
            return place;
        }

        private void Direct(ECSWorld world, GameSession session)
        {
            bool down = session.Health <= 0f;
            if (down && !_wasDown) Music.Play(MusicCue.Defeat);
            _wasDown = down;

            MusicMood mood;
            float wanted;
            if (down)
            {
                mood = MusicMood.Silence;
                wanted = 0f;
            }
            else if (session.IsHaven)
            {
                // Home is quieter at night: the guitar and the flute, with the hymn under them only in the day.
                float night = SkySYstem.NightOf(world).Amount;
                mood = MusicMood.Haven;
                wanted = HavenDay + (HavenNight - HavenDay) * night;
            }
            else if (session.World.Plan.Archetype.Arena)
            {
                // An arena is one fight, from the gates closing until its tyrant is down, harder as it weakens and as the hero does.
                float tyrant = TyrantHealth(world, session, null);
                mood = _won ? MusicMood.Silence : MusicMood.Arena;
                wanted = 0.7f + 0.2f * (1f - tyrant) + 0.1f * Hurt(session);
            }
            else
            {
                SiteState? keep = Keep(session);
                bool cleared = keep == null || keep.Cleared || session.Liberated;
                if (keep != null && !_won)
                {
                    if (!cleared) TyrantHealth(world, session, keep);
                    else if (_tyrantSeen)
                    {
                        _won = true;
                        Music.Play(MusicCue.Victory);
                    }
                }
                bool fighting = keep != null && !cleared && !_won && _inKeep;
                mood = fighting ? MusicMood.Boss : MusicMood.Land;
                wanted = fighting ? 0.72f + 0.28f * Hurt(session) : LandIntensity;
            }

            // Up quickly, down slowly.
            float rate = wanted > _intensity ? RiseSpeed : FallSpeed;
            _intensity += Math.Clamp(wanted - _intensity, -rate * Interval, rate * Interval);

            Music.Mood = _hold.Update(mood, Interval);
            Music.Intensity = _intensity;
        }

        private static float Hurt(GameSession session) => 1f - Math.Clamp(session.Health / MathF.Max(session.Sheet.MaxHealth, 1f), 0f, 1f);

        // The keep the hero is in, if they're in one: inside its walls, or down on its arena's floor.
        private SiteState? Keep(GameSession session)
        {
            Vector2 at = session.PlayerPosition;
            SiteState? found = null;
            bool inside = false;
            foreach (SiteState state in session.Sites)
            {
                if (state.Wild || state.Site.Kind != SiteKind.Keep) continue;
                found ??= state;
                float distance = (state.Site.Position - at).Length - state.Site.Radius;
                bool floor = state.Site.Arena is { Keep: true } plan && plan.Contains(at, FloorReach);
                if (floor || distance < (_inKeep ? KeepLeaves : KeepEnters))
                {
                    found = state;
                    inside = true;
                    break;
                }
            }
            _inKeep = inside;
            return found;
        }

        /// <summary>
        /// How much of the tyrant is left, 0 to 1, as this player can see it: in an arena any tyrant at all, at a keep the one standing in it. Once one has been seen standing and then
        /// isn't, it has fallen, and that's what the victory is.
        /// </summary>
        private float TyrantHealth(ECSWorld world, GameSession session, SiteState? keep)
        {
            float left = 1f;
            bool standing = false, fallen = false;
            foreach (Archetype archetype in world.QueryArchetypes(_enemies, BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    var enemy = entity.Get<EnemyComponent>();
                    if (enemy.Rarity != Rarity.Unique || enemy.Friendly) continue;
                    Vector2 position = entity.Get<PositionComponent>().value.Xz;
                    if (keep != null && (keep.Site.Position - position).Length > keep.Site.Radius + KeepLeaves && !(keep.Site.Arena is { Keep: true } plan && plan.Contains(position, FloorReach))) continue;

                    if (enemy.State == EnemyState.Dead)
                    {
                        fallen = true;
                        continue;
                    }
                    var health = entity.Get<HealthComponent>();
                    standing = true;
                    left = MathF.Min(left, Math.Clamp(health.Current / MathF.Max(health.Max, 1f), 0f, 1f));
                }
            }

            if (standing) _tyrantSeen = true;
            else if (_tyrantSeen && fallen && !_won)
            {
                _won = true;
                Music.Play(MusicCue.Victory);
            }
            return left;
        }
    }
}

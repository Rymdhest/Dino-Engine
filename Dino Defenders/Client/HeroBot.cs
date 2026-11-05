using Dino_Defenders.Gameplay;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Defenders.Client
{
    /// <summary>
    /// Plays the hero with nobody at the keyboard, to watch the game run. It fights whatever is close and drinks when hurt. Sweeping,
    /// it also goes on to the next thing the world holds: every enemy, shrine and chest in turn, until none are left. It doesn't
    /// try to play well; it is here to touch everything a player would.
    /// </summary>
    internal sealed class HeroBot
    {
        private const int Epilogue = 540;           // Frames left running at the end, long enough for a freed land to clear.
        private const float EngageRange = 80f;
        private const float SiteReach = 30f;
        private const float UseReach = 3f;
        private const int MostAttempts = 4;
        private const int MostFramesHeld = 120;
        private const int FramesToArrive = 30;      // Being moved goes by way of the server, so it isn't done the moment it's asked.

        private static readonly BitMask EnemyMask = new BitMask(typeof(EnemyComponent), typeof(PositionComponent));

        private readonly ECSWorld _ecs;
        private readonly bool _sweep;
        private readonly Vector2? _walkTo;
        private readonly Action<HeroBot> _finished;
        private readonly Dictionary<Vector2, int> _attempts = new();
        private int _framesLeft = -1;
        private Vector2 _heldAt;
        private int _framesHeld;
        private int _arriving;

        public int Unreachable { get; private set; }

        public HeroBot(ECSWorld ecs, bool sweep, Vector2? walkTo, Action<HeroBot> finished)
        {
            _ecs = ecs;
            _sweep = sweep;
            _walkTo = walkTo;
            _finished = finished;
        }

        public HeroIntent Think(GameSession session)
        {
            var intent = new HeroIntent();
            if (_arriving > 0)
            {
                _arriving--;
                return intent;
            }

            // Straight there on foot and nothing else, for what only happens to someone actually running over the ground. Getting about otherwise is mostly done by being moved.
            if (_walkTo is Vector2 goal)
            {
                Vector2 way = goal - session.PlayerPosition;
                if (way.LengthSquared > 1f) intent.Move = way;
                return intent;
            }

            if (session.Health < session.Sheet.MaxHealth * 0.4f) intent.Flask = true;
            intent.Interact = session.Offered is InteractKind.Shrine or InteractKind.Chest;

            Vector2 here = session.PlayerPosition;
            Vector2 nearest = default;
            float best = float.PositiveInfinity;
            int close = 0;
            foreach (Entity entity in _ecs.QueryEntities(EnemyMask, BitMask.Empty))
            {
                EntityView view = _ecs.GetEntityView(entity);
                var enemy = view.Get<EnemyComponent>();
                if (enemy.State == EnemyState.Dead || enemy.Friendly) continue;

                Vector2 position = view.Get<PositionComponent>().value.Xz;
                float distance = (position - here).Length;
                if (distance < 8f) close++;
                if (distance >= best) continue;
                best = distance;
                nearest = position;
            }

            if (best > EngageRange)
            {
                if (_sweep) GoOn(session);
                return intent;
            }

            // It walks in straight lines, so a wall can hold it where a player would simply go around.
            if ((here - _heldAt).LengthSquared > 0.25f || best < 8f)
            {
                _heldAt = here;
                _framesHeld = 0;
            }
            else if (++_framesHeld > MostFramesHeld)
            {
                _framesHeld = 0;
                Travel(session, nearest);
            }

            Vector2 toward = nearest - here;
            intent.Aim = toward;
            if (best > 4.5f) intent.Move = toward;
            if (best < 6f) intent.Use(0);
            intent.Dash = best is > 10f and < 18f;

            // Whatever else is on the bar gets used whenever it could reach, so that a scripted run exercises a build and not only its first skill.
            for (int slot = 1; slot < Progression.HeroState.SkillSlots; slot++)
            {
                if (session.Client.Kit.Slots[slot] is not Stats.SkillProfile skill) continue;
                float reach = MathF.Max(skill.Range, skill.Reach);
                if (skill.Skill.Form == Skills.SkillForm.Strike && skill.Skill.Arc >= MathF.Tau ? close >= 2 : best < reach) intent.Use(slot);
            }
            return intent;
        }

        private void Travel(GameSession session, Vector2 to)
        {
            session.Client.TravelTo(to);
            _arriving = FramesToArrive;
        }

        private void GoOn(GameSession session)
        {
            if (TryFindErrand(session, out Vector2 errand, out float reach))
            {
                if ((errand - session.PlayerPosition).Length <= reach) return;
                _attempts[errand] = _attempts.GetValueOrDefault(errand) + 1;
                Travel(session, errand);
                return;
            }

            if (_framesLeft < 0) _framesLeft = Epilogue;
            if (_framesLeft-- == 0) _finished(this);
        }

        private bool TryFindErrand(GameSession session, out Vector2 errand, out float reach)
        {
            Vector2 found = default;
            float foundReach = 0f;
            float best = float.PositiveInfinity;

            void Consider(Vector2 position, float withinReach)
            {
                if (_attempts.GetValueOrDefault(position) >= MostAttempts)
                {
                    Unreachable++;
                    return;
                }
                float distance = (position - session.PlayerPosition).Length;
                if (distance >= best) return;
                best = distance;
                found = position;
                foundReach = withinReach;
            }

            Unreachable = 0;
            foreach (SiteState state in session.Sites)
            {
                // A pack is what it meets on the way. There are hundreds, and they aren't what freeing a world is about.
                if (!state.Cleared && state.HostilesTotal > 0 && !state.Wild) Consider(state.Site.Position, SiteReach);

                for (int i = 0; i < state.Site.Spawns.Count; i++)
                {
                    SiteSpawn spawn = state.Site.Spawns[i];
                    bool free = state.Cleared || state.HostilesTotal == 0;
                    if (spawn.Role == SpawnRole.Chest && free && !state.Opened.Contains(i)) Consider(spawn.Position, UseReach);
                    if (spawn.Role == SpawnRole.Altar && !state.Kindled) Consider(spawn.Position, UseReach);
                }
            }

            errand = found;
            reach = foundReach;
            return !float.IsInfinity(best);
        }
    }
}

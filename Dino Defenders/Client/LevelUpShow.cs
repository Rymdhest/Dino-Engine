using Dino_Defenders.Gameplay;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Particles;
using Dino_Engine.Particles.Content;
using Dino_Engine.Physics;
using OpenTK.Mathematics;

namespace Dino_Defenders.Client
{
    /// <summary>
    /// A hero going up a level, as it's seen: light going up from them (<see cref="Triumph.LevelUp"/>), following them as they run, a shockwave of wind going out through the grass that
    /// knocks over loose clutter near them (for show, as BreakageSystem's breaking always is), and for this player's own hero a kick of the camera and the moment across the screen (<see cref="GameSession.Rise"/>, drawn by the Hud) and on the level's seal. This player's own comes from their level going
    /// up in what the server last said of their hero, which is the truth and can't be missed; anyone else's from the fanfare the server sends with it, which can, as it's only for show.
    /// Nothing here is read by anything else, and the fanfare is <see cref="GameSounds"/>'.
    /// </summary>
    public sealed class LevelUpShow
    {
        // The shockwave's wind. At its front it's over BreakageSystem.BreakingWind for the first ten metres or so, as a slam's is, so barrels and crates near the hero go over; past
        // that it's only the grass bowing away in a ring, out to where the light's front stops.
        private const float Gust = 17f;
        private const float GustReach = 26f;
        private const float GustFor = 1.4f;
        private const float Kick = 5.5f;        // Metres a second the camera is knocked down, as it is by a heavy blow, and springs back.
        private const float Whose = 5f;         // Metres from where the fanfare was sent, inside which a hero is taken to be whose it was.

        private static readonly BitMask HeroMask = new BitMask(typeof(HeroComponent), typeof(PositionComponent));

        private readonly ECSWorld _world;
        private readonly GameClient _client;
        private readonly List<(EffectRun Run, Entity Who)> _following = new();
        private Progression.HeroState? _watched;
        private int _level;

        public LevelUpShow(ECSWorld world, GameClient client)
        {
            _world = world;
            _client = client;
            client.Events.On<SoundEvent>(e =>
            {
                if (e.Cue == SoundCue.LevelUp) Heard(e.Position);
            });
        }

        public void Update(GameSession session)
        {
            Progression.HeroState hero = _client.Hero;
            if (!ReferenceEquals(hero, _watched))
            {
                _watched = hero;
                _level = hero.Level;
            }
            else if (hero.Level != _level)
            {
                bool up = hero.Level > _level;
                _level = hero.Level;
                if (up) Rise(session, hero.Level, hero.UnspentPoints);
            }

            for (int i = _following.Count - 1; i >= 0; i--)
            {
                (EffectRun run, Entity who) = _following[i];
                if (run.Finished || !who.IsValid() || !_world.TryGetEntityLocation(who, out _, out _))
                {
                    _following.RemoveAt(i);
                    continue;
                }
                run.Position = _world.GetEntityView(who).Get<PositionComponent>().value;
            }
        }

        private void Rise(GameSession session, int level, int points)
        {
            session.Rise = new LevelRise { Level = level, Points = points };
            if (!session.Player.IsValid() || !_world.TryGetEntityLocation(session.Player, out _, out _)) return;
            Play(session.Player, _world.GetEntityView(session.Player).Get<PositionComponent>().value);

            if (!_world.GameCamera.IsValid() || !_world.TryGetEntityLocation(_world.GameCamera, out _, out _)) return;
            EntityView camera = _world.GetEntityView(_world.GameCamera);
            var follow = camera.Get<FollowCameraComponent>();
            follow.JoltSpeed += -Vector3.UnitY * Kick;
            camera.Set(follow);
        }

        // Someone else's: whoever's standing where it was sent from. This player's own is already shown, from their level.
        private void Heard(Vector3 position)
        {
            Entity nearest = default;
            float best = Whose * Whose;
            Vector3 at = position;
            foreach (Archetype archetype in _world.QueryArchetypes(HeroMask, BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    Vector3 where = entity.Get<PositionComponent>().value;
                    float away = (where.Xz - position.Xz).LengthSquared;
                    if (away >= best) continue;
                    best = away;
                    nearest = entity.Entity;
                    at = where;
                }
            }
            if (!nearest.IsValid() || _world.GetEntityView(nearest).Get<HeroComponent>().Player == _client.PlayerId) return;
            Play(nearest, at);
        }

        private void Play(Entity who, Vector3 feet)
        {
            _following.Add((Particles.Play(Triumph.LevelUp, feet, 1f, EffectShape.Point), who));
            Wind.AddBlast(_world, feet, GustReach, Gust, GustFor);
        }
    }
}

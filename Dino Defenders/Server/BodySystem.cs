using Dino_Defenders.Gameplay;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using OpenTK.Mathematics;

namespace Dino_Defenders.Server
{
    /// <summary>
    /// Bodies don't go through each other. A brain steers round whoever's in its way, which keeps a pack loose, but nothing made it so, and minions at heel stood inside one another. This runs after
    /// every brain has said where it's going and before the world has its say (<see cref="CharacterMotorSystem"/>), so what it does is only ever more walking: it can't put anyone in a wall. Whatever
    /// walks into someone is stopped at their edge, and two that overlap are eased apart, the lighter giving way, so a tyrant hardly notices the soldiers round its feet. A hero gives no way at all,
    /// as their step is predicted on their own machine from nothing but the world (see NETWORKING.md): whatever they walk into is moved out of their way.
    /// </summary>
    public sealed class BodySystem : SystemBase
    {
        private const float Settle = 0.6f;          // Of an overlap that's put right each tick, short of all of it, so a crowd eases apart rather than jumping.
        private const float SteppingAside = 1.5f;   // Metres a second, faster than which something with nothing to fight turns to the way it's really going.
        private const float MostShove = 5f;         // Metres a second, the fastest anyone is moved out of someone else: any faster and a body pressed on both sides shakes.
        private const float MostHeroShove = 14f;    // Out of a hero's way, which has to be faster than a hero runs, or they'd walk into it.
        private const float BiggestBody = 3.2f;     // Further than any body reaches, so a look round finds whoever it could be touching.

        private readonly GameServer _server;
        private readonly List<Actor> _near = new();

        public BodySystem(GameServer server)
            : base(new BitMask(typeof(ActorComponent), typeof(VelocityComponent), typeof(PositionComponent)))
        {
            _server = server;
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            ServerSession? session = _server.Session;
            if (session == null || session.Paused || deltaTime <= 0f) return;
            Actor actor = entity.Get<ActorComponent>().Actor;
            if (actor.Dead || actor.IsHero || actor.Still) return;

            // Everyone near, on either side.
            _near.Clear();
            float reach = actor.Radius + BiggestBody;
            session.Combat.Grid.Foes(Team.Heroes, actor.Position, reach, _near);
            session.Combat.Grid.Foes(Team.Enemies, actor.Position, reach, _near);

            var velocity = entity.Get<VelocityComponent>();
            Vector2 walking = velocity.value.Xz;
            Vector2 shove = Vector2.Zero, heroShove = Vector2.Zero;
            bool touching = false;
            foreach (Actor other in _near)
            {
                if (other == actor) continue;
                Vector2 apart = actor.Position - other.Position;
                float distance = apart.Length;
                float overlap = actor.Radius + other.Radius - distance;
                if (overlap <= 0f) continue;
                touching = true;

                // Two in exactly one place have nothing to tell them apart but who they are.
                Vector2 away = distance > 1e-3f ? apart / distance : new Vector2(MathF.Cos(actor.Entity.Id * 2.4f), MathF.Sin(actor.Entity.Id * 2.4f));
                float gives = other.IsHero || other.Still ? 1f : Weight(other) / (Weight(actor) + Weight(other));
                if (other.IsHero) heroShove += away * overlap;
                else shove += away * (overlap * gives);

                // What's left of the way it was going goes round them, not into them, as much as it's the one to give way: something heavy walks on through, and what's in front of it is shoved.
                float into = -Vector2.Dot(walking, away);
                if (into > 0f) walking += away * (into * gives);
            }
            if (!touching) return;

            Vector2 push = Capped(shove * (Settle / deltaTime), MostShove) + Capped(heroShove * (Settle / deltaTime), MostHeroShove);
            velocity.value = new Vector3(walking.X + push.X, velocity.value.Y, walking.Y + push.Y);
            entity.Set(velocity);

            // Shoved along with nothing to fight, it steps aside: it turns to the way it's going, or it would glide there sideways. In a fight it keeps its eyes on its foe.
            Vector2 going = velocity.value.Xz;
            if (entity.Has<EnemyComponent>() && entity.Get<EnemyComponent>().State is EnemyState.Idle or EnemyState.Returning && going.LengthSquared > SteppingAside * SteppingAside)
            {
                EnemyBrainSystem.Face(entity, going, deltaTime);
            }
        }

        private static Vector2 Capped(Vector2 push, float most) => push.LengthSquared > most * most ? push.Normalized() * most : push;

        // How hard a body is to move, which goes with how much of it there is.
        private static float Weight(Actor actor) => actor.Radius * actor.Radius;
    }
}

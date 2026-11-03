using Dino_Engine.Animation;
using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Physics;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Dino_Defenders.Gameplay
{
    /// <summary>
    /// The hero under the player's hands. WASD moves, the mouse aims. The left button swings at where the mouse points, space
    /// dodges, Q spins and R drinks. A swing's blow lands a moment after it starts, so that it reads as a blow.
    /// </summary>
    public class PlayerControlSystem : SystemBase
    {
        private const float SwingDuration = 0.6f;
        private const float SwingImpact = 0.15f;
        private const float SwingReach = 6.4f;
        private const float SwingArc = 2.4f;
        private const float DashDuration = 0.2f;
        private const float DashSpeed = 34f;
        private const float DashCooldown = 2.4f;
        private const float WhirlReach = 8.5f;
        private const float WhirlCooldown = 8f;
        private const float WhirlDamage = 1.9f;
        private const float FlaskHeal = 0.45f;
        private const float DeathDelay = 2.4f;
        private const float OpeningRadius = 9f;
        private const float CharacterHeight = 4.7f;

        private float _swingImpactIn = -1f;
        private float _dashTimeLeft;
        private Vector2 _dashDirection;
        private Vector2 _aim = Vector2.UnitY;
        private float _deadFor = -1f;

        public PlayerControlSystem()
            : base(new BitMask(typeof(PlayerTag), typeof(VelocityComponent)))
        {
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            GameSession session = GameSession.Current;
            Vector3 position = entity.Get<PositionComponent>().value;
            session.PlayerPosition = position.Xz;
            Engine.RenderEngine.context.focusPoint = new Vector4(position + Vector3.UnitY * CharacterHeight, OpeningRadius);

            // The free camera owns the keyboard and mouse; park the player meanwhile
            if (world.IsFreeCameraActive)
            {
                entity.Set(new VelocityComponent());
                return;
            }

            UpdateCooldowns(session, entity, deltaTime);
            if (session.Health <= 0f)
            {
                UpdateDeath(entity, session, deltaTime);
                return;
            }

            var window = Engine.WindowHandler;
            bool keyboardFree = !Engine.Gui.WantsKeyboard;
            bool mouseFree = !Engine.Gui.WantsMouse;

            UpdateAim(position);

            Vector2 move = Vector2.Zero;
            if (keyboardFree)
            {
                if (window.IsKeyDown(Keys.W)) move.Y += 1f;
                if (window.IsKeyDown(Keys.S)) move.Y -= 1f;
                if (window.IsKeyDown(Keys.A)) move.X += 1f;
                if (window.IsKeyDown(Keys.D)) move.X -= 1f;
            }
            if (move.LengthSquared > 0f) move.Normalize();

            float speed = session.Stats.MoveSpeed;
            if (_swingImpactIn >= 0f || session.SwingCooldown > 0f) speed *= 0.55f;

            if (keyboardFree && window.IsKeyPressed(Keys.Space) && session.DashCooldown <= 0f)
            {
                _dashDirection = move.LengthSquared > 0f ? move : _aim;
                _dashTimeLeft = DashDuration;
                session.DashCooldown = DashCooldown;
                entity.Set(new HeroInvulnerableComponent { Time = DashDuration + 0.1f });
                Wind.AddBlast(world, position, 6f, 8f, 0.4f);
            }

            Vector2 velocity = move * speed;
            if (_dashTimeLeft > 0f)
            {
                _dashTimeLeft -= deltaTime;
                velocity = _dashDirection * DashSpeed;
            }
            entity.Set(new VelocityComponent(new Vector3(velocity.X, 0f, velocity.Y)));

            bool attacking = _swingImpactIn >= 0f || session.SwingCooldown > 0f;
            CharacterFactory.Face(entity, attacking ? _aim : (move.LengthSquared > 0f ? move : Vector2.Zero), deltaTime);

            if (mouseFree && window.IsMouseButtonDown(MouseButton.Left) && session.SwingCooldown <= 0f) StartSwing(entity, session);
            if (keyboardFree && window.IsKeyPressed(Keys.Q) && session.WhirlCooldown <= 0f) Whirl(world, session, position);
            if (keyboardFree && window.IsKeyPressed(Keys.R)) DrinkFlask(session, position);

            if (_swingImpactIn >= 0f)
            {
                _swingImpactIn -= deltaTime;
                if (_swingImpactIn < 0f)
                {
                    Combat.StrikeEnemies(world, session, position.Xz, _aim, SwingReach, SwingArc, 1f);
                    Combat.Spark(world, position + new Vector3(_aim.X * 3f, 3f, _aim.Y * 3f), new Colour(1f, 0.95f, 0.8f, 1.2f), 3, 4f);
                }
            }

            session.Health = Math.Min(session.Health + session.Stats.Regeneration * deltaTime, session.Stats.MaxHealth);
        }

        private static void UpdateCooldowns(GameSession session, EntityView entity, float deltaTime)
        {
            session.SwingCooldown = MathF.Max(session.SwingCooldown - deltaTime, 0f);
            session.DashCooldown = MathF.Max(session.DashCooldown - deltaTime, 0f);
            session.WhirlCooldown = MathF.Max(session.WhirlCooldown - deltaTime, 0f);
            session.GuideCooldown = MathF.Max(session.GuideCooldown - deltaTime, 0f);

            var safe = entity.Get<HeroInvulnerableComponent>();
            safe.Time -= deltaTime;
            entity.Set(safe);
        }

        // The ground point under the mouse: the view ray met with a plane at the character's chest.
        private void UpdateAim(Vector3 position)
        {
            var context = Engine.RenderEngine.context;
            Vector2 mouse = Engine.WindowHandler.MousePosition;
            Vector2i size = Engine.Resolution;
            float x = mouse.X / size.X * 2f - 1f;
            float y = 1f - mouse.Y / size.Y * 2f;

            Matrix4 inverse = Matrix4.Invert(context.viewMatrix * context.projectionMatrix);
            Vector4 near = new Vector4(x, y, -1f, 1f) * inverse;
            Vector4 far = new Vector4(x, y, 1f, 1f) * inverse;
            Vector3 from = near.Xyz / near.W;
            Vector3 to = far.Xyz / far.W;

            float planeY = position.Y + 2.5f;
            float t = (planeY - from.Y) / (to.Y - from.Y);
            if (t <= 0f || float.IsNaN(t)) return;

            Vector3 hit = from + (to - from) * t;
            Vector2 offset = hit.Xz - position.Xz;
            if (offset.LengthSquared > 0.25f) _aim = offset.Normalized();
        }

        private void StartSwing(EntityView entity, GameSession session)
        {
            float rate = session.Stats.AttackSpeed;
            session.SwingCooldown = SwingDuration / rate;
            _swingImpactIn = SwingImpact / rate;

            var animation = entity.Get<AnimationComponent>();
            animation.PlayLayer(AnimationId.Swing, loop: false, speed: 2f * rate);
            entity.Set(animation);
        }

        private static void Whirl(ECSWorld world, GameSession session, Vector3 position)
        {
            session.WhirlCooldown = WhirlCooldown;
            Combat.StrikeEnemies(world, session, position.Xz, Vector2.UnitX, WhirlReach, MathF.Tau, WhirlDamage * session.Stats.SkillPower);
            Wind.AddBlast(world, position, 14f, 22f, 1f);
            for (int i = 0; i < 18; i++)
            {
                float angle = i * (MathF.Tau / 18f);
                Vector3 ring = position + new Vector3(MathF.Cos(angle) * 6f, 2.4f, MathF.Sin(angle) * 6f);
                Combat.Spark(world, ring, new Colour(0.85f, 0.95f, 1f, 1.6f), 1, 2f);
            }
        }

        private static void DrinkFlask(GameSession session, Vector3 position)
        {
            if (session.Hero.Flasks <= 0 || session.Health >= session.Stats.MaxHealth) return;
            session.Hero.Flasks--;
            float heal = session.Stats.MaxHealth * FlaskHeal;
            session.Health = Math.Min(session.Health + heal, session.Stats.MaxHealth);
            session.Say(position + Vector3.UnitY * 5.4f, $"+{heal:0}", new Vector3(0.4f, 1f, 0.5f), 18f, 1.2f);
        }

        // A fallen hero wakes at the last shrine they lit, a little poorer.
        private void UpdateDeath(EntityView entity, GameSession session, float deltaTime)
        {
            entity.Set(new VelocityComponent());
            if (_deadFor < 0f)
            {
                _deadFor = 0f;
                int lost = session.Hero.Gold / 10;
                session.Hero.Gold -= lost;
                session.Announce("You have fallen", lost > 0 ? $"{lost} gold lost on the road" : "The land goes on without you");
            }

            _deadFor += deltaTime;
            if (_deadFor < DeathDelay) return;

            _deadFor = -1f;
            var position = entity.Get<PositionComponent>();
            position.value = new Vector3(session.Respawn.X, position.value.Y, session.Respawn.Y);
            entity.Set(position);
            session.Health = session.Stats.MaxHealth;
        }
    }
}

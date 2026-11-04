using Dino_Defenders.Gameplay;
using Dino_Defenders.Server;
using Dino_Engine.Animation;
using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Networking;
using Dino_Engine.Physics;
using Dino_Engine.Util;
using Dino_Engine.World;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Dino_Defenders.Client
{
    /// <summary>How a ghost was last seen moving, to work out what it looks like it's doing.</summary>
    public struct GhostMotionComponent : IComponent
    {
        public Vector3 Last;
        public bool Seen;
    }

    /// <summary>
    /// Gives ghosts a body. A server only says what something is; this adds everything that takes to draw on this machine. New kinds of
    /// thing need nothing here: a <see cref="VisualComponent"/> is enough.
    /// </summary>
    public sealed class GhostPresenter : IGhostHost
    {
        private readonly CharacterFactory _characters;

        public GhostPresenter(CharacterFactory characters)
        {
            _characters = characters;
        }

        public void Decorate(Ghost ghost, List<IComponent> components)
        {
            Vector3 position = Vector3.Zero;
            float scale = 1f;
            bool placed = false;
            bool turned = false;
            bool sized = false;
            VisualComponent? visual = null;

            for (int i = 0; i < components.Count; i++)
            {
                switch (components[i])
                {
                    case PositionComponent found:
                        position = found.value;
                        placed = true;
                        break;
                    case RotationComponent:
                        turned = true;
                        break;
                    case ScaleComponent found:
                        scale = found.value.Y;
                        sized = true;
                        break;
                    case VisualComponent found:
                        visual = found;
                        break;
                    case BallisticComponent found:
                        position = found.Origin;
                        break;
                    case SwingComponent found:
                        // Whatever it was last doing happened before this client was there to see it.
                        found.Played = found.Serial;
                        components[i] = found;
                        break;
                }
            }

            for (int i = 0; i < components.Count; i++)
            {
                if (components[i] is PickupComponent pickup)
                {
                    pickup.Rest = position;
                    components[i] = pickup;
                }
            }

            if (!placed) components.Add(new PositionComponent(position));
            if (!turned) components.Add(new RotationComponent(new Vector3(0f)));
            if (!sized) components.Add(new ScaleComponent(new Vector3(1f)));
            if (visual == null) return;

            components.Add(new LocalToWorldMatrixComponent());
            if (visual.Value.Kind == VisualKind.Character)
            {
                components.Add(new CharacterModelComponent(_characters.Resolve(visual.Value.Model)));
                components.Add(new AnimationComponent());
                components.Add(new SkeletonComponent());
                components.Add(new VelocityComponent());
                components.Add(new FixToTerrainHeightComponent());
                components.Add(new GrassDisplaceComponent(1.4f * scale));
                components.Add(new WaterDisturberComponent(0.42f * scale));
                components.Add(new GhostMotionComponent());
                components.Add(new NetMovedTag());
            }
            else
            {
                components.Add(new ModelComponent(_characters.Resolve(visual.Value.Model)));
                components.Add(new ModelRenderTag());
            }
        }

        public void Created(Ghost ghost)
        {
        }

        public void Removing(Ghost ghost)
        {
        }
    }

    /// <summary>
    /// Sets everything on screen to how it should look this frame: ghosts from what the server said, a little in the past; this
    /// player's hero from what is predicted, now; and everything that can be worked out from those without being sent, such as
    /// which way the dead fall and where arrows are.
    /// </summary>
    public class GhostPresentationSystem : SystemBase
    {
        private const float OpeningRadius = 9f;
        private const float CharacterHeight = 4.7f;
        private const float FallingStart = 0.5f;
        private const double ArrowCatchUp = 15.0;   // Ticks over which an arrow moves up into the player's own moment.
        private const float ArrowHitReach = 2.4f;

        private readonly Func<GameClient?> _client;
        private readonly BitMask _motionMask = new BitMask(typeof(GhostMotionComponent), typeof(PositionComponent), typeof(VelocityComponent));
        private readonly BitMask _swingMask = new BitMask(typeof(SwingComponent), typeof(AnimationComponent));
        private readonly BitMask _enemyMask = new BitMask(typeof(EnemyComponent), typeof(RotationComponent), typeof(ScaleComponent));
        private readonly BitMask _heroMask = new BitMask(typeof(HeroComponent), typeof(HealthComponent), typeof(RotationComponent));
        private readonly BitMask _arrowMask = new BitMask(typeof(BallisticComponent), typeof(PositionComponent), typeof(ScaleComponent));
        private readonly BitMask _pickupMask = new BitMask(typeof(PickupComponent), typeof(PositionComponent));

        public GhostPresentationSystem(Func<GameClient?> client)
            : base(new BitMask(typeof(PositionComponent)))
        {
            _client = client;
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime) { }

        protected override void UpdateFrame(ECSWorld world, float deltaTime)
        {
            GameClient? client = _client();
            GameSession? session = client?.Session;
            if (client == null || session == null || client.State != ClientState.Playing || !client.Render.Started) return;

            client.Ghosts.Apply(client.Render.RenderTick);

            MoveOwnHero(world, client, session);
            if (deltaTime > 0f) ReadMotion(world, deltaTime);
            PlaySwings(world, client);
            ToppleFallen(world, deltaTime);
            ToppleHeroes(world, deltaTime);
            FlyArrows(world, client, session);
            BobPickups(world, deltaTime);
        }

        private static void MoveOwnHero(ECSWorld world, GameClient client, GameSession session)
        {
            if (client.OwnGhost == null || !world.TryGetEntityLocation(client.OwnGhost.Entity, out _, out _)) return;

            EntityView view = world.GetEntityView(client.OwnGhost.Entity);
            float alpha = client.Commands.Alpha;
            Vector2 at = client.Predictor.DrawnPosition(alpha);
            Vector3 position = new Vector3(at.X, view.Get<PositionComponent>().value.Y, at.Y);
            view.Set(new PositionComponent(position));
            view.Set(new RotationComponent(Quaternion.FromAxisAngle(Vector3.UnitY, client.Predictor.DrawnYaw(alpha))));
            Engine.RenderEngine.context.focusPoint = new Vector4(position + Vector3.UnitY * CharacterHeight, OpeningRadius);
        }

        // Nothing sends how fast a ghost is going: it can be seen, and what's seen is what the animation should match.
        private void ReadMotion(ECSWorld world, float deltaTime)
        {
            foreach (Archetype archetype in world.QueryArchetypes(_motionMask, BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    var motion = entity.Get<GhostMotionComponent>();
                    Vector3 position = entity.Get<PositionComponent>().value;
                    Vector3 velocity = entity.Get<VelocityComponent>().value;

                    Vector3 seen = motion.Seen ? (position - motion.Last) / deltaTime : Vector3.Zero;
                    seen.Y = 0f;
                    if (seen.LengthSquared > 60f * 60f) seen = Vector3.Zero;

                    // Starting is at once, as a run's speed is set by how it starts; stopping is eased, as a frame without news isn't a stop.
                    if (seen.LengthSquared >= velocity.LengthSquared * 0.25f) velocity = seen;
                    else velocity *= MathF.Exp(-12f * deltaTime);
                    if (velocity.LengthSquared < 0.02f) velocity = Vector3.Zero;

                    entity.Set(new VelocityComponent(velocity));
                    motion.Last = position;
                    motion.Seen = true;
                    entity.Set(motion);
                }
            }
        }

        private void PlaySwings(ECSWorld world, GameClient client)
        {
            uint own = client.OwnGhost?.Entity.Id ?? 0;
            foreach (Archetype archetype in world.QueryArchetypes(_swingMask, BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    var swing = entity.Get<SwingComponent>();
                    if (swing.Serial == swing.Played) continue;
                    swing.Played = swing.Serial;
                    entity.Set(swing);

                    // This player's own swing started when they pressed.
                    if (entity.Entity.Id == own) continue;
                    var animation = entity.Get<AnimationComponent>();
                    animation.PlayLayer(AnimationId.Swing, loop: false, speed: swing.Speed);
                    entity.Set(animation);
                }
            }
        }

        private void ToppleFallen(ECSWorld world, float deltaTime)
        {
            foreach (Archetype archetype in world.QueryArchetypes(_enemyMask, BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    var enemy = entity.Get<EnemyComponent>();
                    if (enemy.State != EnemyState.Dead) continue;

                    if (enemy.DeadFor == 0f)
                    {
                        enemy.DeathScale = entity.Get<ScaleComponent>().value;
                        enemy.DeathRotation = entity.Get<RotationComponent>().quaternion;
                    }
                    enemy.DeadFor += deltaTime;
                    entity.Set(enemy);

                    float fall = WorldMath.Smoothstep(0f, DyingSystem.FallTime, enemy.DeadFor);
                    var rotation = entity.Get<RotationComponent>();
                    rotation.quaternion = Quaternion.FromAxisAngle(enemy.FallAxis, fall * MathF.PI * 0.5f) * enemy.DeathRotation;
                    entity.Set(rotation);

                    float end = DyingSystem.FallTime + DyingSystem.LieTime;
                    float fade = 1f - WorldMath.Smoothstep(end, end + DyingSystem.FadeTime, enemy.DeadFor);
                    entity.Set(new ScaleComponent(enemy.DeathScale * MathF.Max(fade, 0.01f)));
                }
            }
        }

        private void ToppleHeroes(ECSWorld world, float deltaTime)
        {
            foreach (Archetype archetype in world.QueryArchetypes(_heroMask, BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    var hero = entity.Get<HeroComponent>();
                    if (entity.Get<HealthComponent>().Current > 0f)
                    {
                        if (hero.DownFor > 0f)
                        {
                            hero.DownFor = 0f;
                            entity.Set(hero);
                            entity.Set(new RotationComponent(hero.FellFrom));
                        }
                        continue;
                    }

                    if (hero.DownFor == 0f) hero.FellFrom = entity.Get<RotationComponent>().quaternion;
                    hero.DownFor += deltaTime;
                    entity.Set(hero);

                    float fall = MathF.Min(hero.DownFor / FallingStart, 1f);
                    entity.Set(new RotationComponent(hero.FellFrom * Quaternion.FromAxisAngle(Vector3.UnitX, -MathF.PI * 0.5f * fall * fall)));
                }
            }
        }

        // Something flying straight can be drawn at any moment, so an arrow needn't be stuck in the past with everything else. It starts
        // there, leaving the bow as the archer is seen to draw, and catches up with the player's own moment on the way. So when it
        // reaches them it really has reached them, and a dodge that looks like it worked is one that did.
        private void FlyArrows(ECSWorld world, GameClient client, GameSession session)
        {
            double past = client.Render.RenderTick;
            double present = client.Commands.Tick + client.Commands.Alpha;

            foreach (Archetype archetype in world.QueryArchetypes(_arrowMask, BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    var arrow = entity.Get<BallisticComponent>();
                    double age = past - arrow.StartTick;
                    double blend = Math.Clamp(age / ArrowCatchUp, 0.0, 1.0);
                    double tick = past + (present - past) * blend * blend * (3.0 - 2.0 * blend);
                    Vector3 position = arrow.PositionAt(Math.Max(tick, arrow.StartTick));

                    // It's the server that decides, and word of that is on the way. Meanwhile an arrow shouldn't be seen flying on through what it hit.
                    bool spent = (position.Xz - session.PlayerPosition).LengthSquared < ArrowHitReach * ArrowHitReach
                        || position.Y < session.World.Height.Sample(position.Xz)
                        || session.World.Placement.OverlapsSolid(Footprint.Circle(position.Xz, 0.3f));

                    entity.Set(new PositionComponent(position));
                    entity.Set(new ScaleComponent(new Vector3(spent ? 0.001f : 1f)));
                }
            }
        }

        private void BobPickups(ECSWorld world, float deltaTime)
        {
            foreach (Archetype archetype in world.QueryArchetypes(_pickupMask, BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    var pickup = entity.Get<PickupComponent>();
                    pickup.Age += deltaTime;
                    entity.Set(pickup);

                    Vector3 position = entity.Get<PositionComponent>().value;
                    position.Y = pickup.Rest.Y + 0.3f + 0.2f * MathF.Sin(pickup.Age * 3f);
                    entity.Set(new PositionComponent(position));
                }
            }
        }
    }

    /// <summary>What's on offer where the hero stands. Offering is for the player to see; using it goes to the server, which decides for itself.</summary>
    public class PromptSystem : SystemBase
    {
        public PromptSystem()
            : base(new BitMask(typeof(InteractableComponent), typeof(PositionComponent)))
        {
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime) { }

        protected override void UpdateFrame(ECSWorld world, float deltaTime)
        {
            GameSession session = GameSession.Current;
            if (session == null) return;

            string? prompt = null;
            InteractKind? kind = null;
            float best = float.PositiveInfinity;
            foreach (Archetype archetype in world.QueryArchetypes(WithMask, WithoutMask))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    var interactable = entity.Get<InteractableComponent>();
                    float distance = (entity.Get<PositionComponent>().value.Xz - session.PlayerPosition).Length;
                    if (distance > interactable.Radius || distance >= best) continue;
                    best = distance;
                    prompt = interactable.Prompt;
                    kind = interactable.Kind;
                }
            }

            bool up = session.Health > 0f;
            session.InteractPrompt = up ? prompt : null;
            session.Offered = up ? kind : null;
        }
    }

    /// <summary>Sparks, numbers and gusts: what things look like happening, whether told by the server or done by this player a moment ago.</summary>
    public sealed class ClientEffects
    {
        private static readonly Vector3 HitColour = new Vector3(1f, 0.92f, 0.7f);
        private static readonly Vector3 CritColour = new Vector3(1f, 0.55f, 0.15f);
        private static readonly Vector3 HurtColour = new Vector3(1f, 0.25f, 0.2f);

        private readonly ECSWorld _world;
        private readonly WorldRandom _random = new WorldRandom(Environment.TickCount);

        public ClientEffects(ECSWorld world, GameClient client)
        {
            _world = world;

            client.Events.On<DamageEvent>(e =>
            {
                Vector3 colour = e.ToHero ? HurtColour : e.Critical ? CritColour : HitColour;
                float size = e.ToHero ? 17f : e.Critical ? 22f : 16f;
                client.Session?.Say(e.Position, $"{e.Amount:0}", colour, size, 0.9f);
            });
            client.Events.On<TextEvent>(e => client.Session?.Say(e.Position, e.Text, e.Colour, e.Size, e.Life));
            client.Events.On<SparkEvent>(e => Spark(e.Position, ColourOf(e.Kind), e.Count, e.Speed));
            client.Events.On<HeroMoveEvent>(e => Show(e.Move, e.Position, e.Aim));
        }

        /// <summary>This player's hero, the moment they acted.</summary>
        public void HeroActed(GameClient client, HeroStepEvents events, in HeroSimState hero)
        {
            GameSession? session = client.Session;
            if (session == null) return;

            if ((events & HeroStepEvents.Swung) != 0 && client.OwnGhost != null && _world.TryGetEntityLocation(client.OwnGhost.Entity, out _, out _))
            {
                EntityView view = _world.GetEntityView(client.OwnGhost.Entity);
                var animation = view.Get<AnimationComponent>();
                animation.PlayLayer(AnimationId.Swing, loop: false, speed: 2f * client.Stats.AttackSpeed);
                view.Set(animation);
            }

            Vector3 position = new Vector3(hero.Position.X, session.World.Height.Sample(hero.Position), hero.Position.Y);
            Show(events, position, hero.Aim);
        }

        private void Show(HeroStepEvents events, Vector3 position, Vector2 aim)
        {
            if ((events & HeroStepEvents.Struck) != 0)
            {
                Spark(position + new Vector3(aim.X * 3f, 3f, aim.Y * 3f), ColourOf(SparkKind.Blade), 3, 4f);
            }

            if ((events & HeroStepEvents.Dashed) != 0) Wind.AddBlast(_world, position, 6f, 8f, 0.4f);

            if ((events & HeroStepEvents.Whirled) != 0)
            {
                Wind.AddBlast(_world, position, 14f, 22f, 1f);
                for (int i = 0; i < 18; i++)
                {
                    float angle = i * (MathF.Tau / 18f);
                    Spark(position + new Vector3(MathF.Cos(angle) * 6f, 2.4f, MathF.Sin(angle) * 6f), ColourOf(SparkKind.Whirl), 1, 2f);
                }
            }
        }

        private static Colour ColourOf(SparkKind kind) => kind switch
        {
            SparkKind.Blade => new Colour(1f, 0.95f, 0.8f, 1.2f),
            SparkKind.Whirl => new Colour(0.85f, 0.95f, 1f, 1.6f),
            SparkKind.Shrine => new Colour(0.5f, 1f, 0.75f, 2.5f),
            _ => new Colour(0.8f, 0.1f, 0.06f, 1.5f),
        };

        /// <summary>A short burst of particles.</summary>
        private void Spark(Vector3 position, Colour colour, int count, float speed)
        {
            for (int i = 0; i < Math.Min(count, 64); i++)
            {
                Vector3 direction = new Vector3(_random.Range(-1f, 1f), _random.Range(0.2f, 1.2f), _random.Range(-1f, 1f)).Normalized();
                _world.CreateEntity(
                    new ParticleRenderTag(),
                    new LocalToWorldMatrixComponent(),
                    new PositionComponent(position),
                    new ScaleComponent(new Vector3(_random.Range(0.1f, 0.22f))),
                    new ColorComponent(colour),
                    new VelocityComponent(direction * speed * _random.Range(0.5f, 1.2f)),
                    new MassComponent(1f),
                    new selfDestroyComponent(_random.Range(0.25f, 0.6f)));
            }
        }
    }

    /// <summary>
    /// The hero under the player's hands. WASD moves, the mouse aims. The left button swings at where the mouse points, space
    /// dodges, Q spins and R drinks.
    /// </summary>
    public static class LocalInput
    {
        public static HeroIntent Read(Vector3 position)
        {
            var window = Engine.WindowHandler;
            var intent = new HeroIntent { Aim = ReadAim(position) };

            if (!Engine.Gui.WantsMouse) intent.Attack = window.IsMouseButtonDown(MouseButton.Left);
            if (Engine.Gui.WantsKeyboard) return intent;

            if (window.IsKeyDown(Keys.W)) intent.Move.Y += 1f;
            if (window.IsKeyDown(Keys.S)) intent.Move.Y -= 1f;
            if (window.IsKeyDown(Keys.A)) intent.Move.X += 1f;
            if (window.IsKeyDown(Keys.D)) intent.Move.X -= 1f;
            intent.Dash = window.IsKeyPressed(Keys.Space);
            intent.Whirl = window.IsKeyPressed(Keys.Q);
            intent.Flask = window.IsKeyPressed(Keys.R);
            intent.Interact = window.IsKeyPressed(Keys.E);
            intent.Guide = window.IsKeyPressed(Keys.F);
            return intent;
        }

        // The ground point under the mouse: the view ray met with a plane at the character's chest.
        private static Vector2? ReadAim(Vector3 position)
        {
            var context = Engine.RenderEngine.context;
            Vector2 mouse = Engine.WindowHandler.MousePosition;
            Vector2i size = Engine.Resolution;
            float x = mouse.X / size.X * 2f - 1f;
            float y = 1f - mouse.Y / size.Y * 2f;

            // Before the first frame has been rendered there's no camera to look through yet.
            Matrix4 viewProjection = context.viewMatrix * context.projectionMatrix;
            if (viewProjection.Determinant == 0f) return null;

            Matrix4 inverse = Matrix4.Invert(viewProjection);
            Vector4 near = new Vector4(x, y, -1f, 1f) * inverse;
            Vector4 far = new Vector4(x, y, 1f, 1f) * inverse;
            Vector3 from = near.Xyz / near.W;
            Vector3 to = far.Xyz / far.W;

            float planeY = position.Y + 2.5f;
            float t = (planeY - from.Y) / (to.Y - from.Y);
            if (t <= 0f || float.IsNaN(t)) return null;

            Vector2 offset = (from + (to - from) * t).Xz - position.Xz;
            return offset.LengthSquared > 0.25f ? offset : null;
        }
    }
}

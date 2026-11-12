using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Server;
using Dino_Defenders.Books;
using Dino_Defenders.Stats;
using Dino_Engine.Animation;
using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Networking;
using Dino_Engine.Particles;
using Dino_Engine.Particles.Content;
using Dino_Engine.Physics;
using Dino_Engine.Sound;
using Dino_Engine.Sound.Content;
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

    /// <summary>A chest's lid, raised on the client alone, as only a yaw is sent: how long it's been coming up, and which way its chest faces.</summary>
    public struct ChestLidComponent : IComponent
    {
        public Quaternion Yaw;
        public float Age;
    }

    /// <summary>
    /// Gives ghosts a body. A server only says what something is; this adds everything that takes to draw on this machine. New kinds of
    /// thing need nothing here: a <see cref="VisualComponent"/> is enough.
    /// </summary>
    public sealed class GhostPresenter : IGhostHost
    {
        private readonly CharacterFactory _characters;
        private int _hostileBolts;

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
            bool hostileBolt = false;

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
                        hostileBolt = !found.FromHeroes;
                        break;
                    case ActionComponent found:
                        // Whatever it was last doing happened before this client was there to see it.
                        found.Heard = found.Serial;
                        components[i] = found;
                        break;
                }
            }

            PickupComponent? loot = null;
            for (int i = 0; i < components.Count; i++)
            {
                if (components[i] is PickupComponent pickup)
                {
                    pickup.Rest = position;
                    components[i] = pickup;
                    loot = pickup;
                }
            }

            if (visual?.Model is ChestLook.Opening or ChestLook.Lid)
            {
                for (int i = 0; i < components.Count; i++)
                {
                    if (components[i] is not RotationComponent shut) continue;
                    components.Add(new ChestLidComponent { Yaw = shut.quaternion, Age = visual.Value.Model == ChestLook.Lid ? ChestLook.Swing : 0f });
                    break;
                }
            }

            if (!placed) components.Add(new PositionComponent(position));
            if (!turned) components.Add(new RotationComponent(new Vector3(0f)));
            if (!sized) components.Add(new ScaleComponent(new Vector3(1f)));

            // A patch of ground doing something shows it all over, with the light of it.
            for (int i = 0; i < components.Count; i++)
            {
                if (components[i] is not ZoneComponent zone) continue;
                components.Add(new LocalToWorldMatrixComponent { value = Matrix4.CreateTranslation(position) });
                components.Add(new ParticleEffectComponent(ParticleShow.GroundOf(PageSign.Read(zone.Sign).Look), EffectShape.Over(new GroundArea(zone.ShapeAt(position.Xz), GameSession.Current?.World))));
                break;
            }

            // A gate between worlds and a place of power can be heard before they're seen. They're drawn by the world, so this is all of them there is.
            foreach (IComponent component in components)
            {
                if (component is not InteractableComponent place) continue;
                if (place.Kind == InteractKind.Portal) components.Add(new SoundEmitterComponent(Life.Gate, gain: PortalHum));
                else if (place.Kind == InteractKind.Shrine) components.Add(new SoundEmitterComponent(Life.ShrineGlow, gain: PlaceHum));
                break;
            }

            if (visual == null) return;

            components.Add(new LocalToWorldMatrixComponent());
            if (visual.Value.Kind == VisualKind.Character)
            {
                components.Add(new CharacterModelComponent(_characters.Resolve(visual.Value.Model)));

                // Who this is decides how they differ from everyone else of their kind, so that no two in a pack are in step.
                CharacterFactory.Bearing bearing = CharacterFactory.BearingOf(visual.Value.Model);
                var animator = new CharacterAnimator(bearing.Rig, bearing.Style, (int)ghost.Id);
                animator.Hold(bearing.Carry, bearing.Shield, bearing.Second);
                components.Add(new AnimatorComponent(animator));
                components.Add(new SkeletonComponent(bearing.Rig, animator.Skinning));
                components.Add(new VelocityComponent());
                components.Add(new FixToTerrainHeightComponent());

                // What a turret's weapon is held by has no feet, to part the grass or make a sound.
                if (!MinionLooks.Hovers(visual.Value.Model))
                {
                    components.Add(new PlantContactComponent(2.7f * scale));
                    components.Add(new WaterDisturberComponent(0.42f * scale));
                    components.Add(new FootfallComponent());
                }
                components.Add(new GhostMotionComponent());
                components.Add(new NetMovedTag());
            }
            else if (WayKeys.IsWay(visual.Value.Model))
            {
                // An open way, in the Haven's great gate or a land's lit one: drawn, lit and breathed out by WaySystem, from what the land it goes to looks like. A land's hums as it stands
                // there, as a dark one doesn't; the Haven's hums as anything that can be used does.
                if (_characters.WayOf(visual.Value.Model) is (Dino_Engine.Modelling.Procedural.Architecture.Waygates.WaygateDesign design, Dino_Engine.Modelling.Procedural.Architecture.Waygates.WayLook look))
                {
                    components.Add(new WayComponent(design, look));
                }
                if (visual.Value.Model.StartsWith(Roads.Lit, StringComparison.Ordinal)) components.Add(new SoundEmitterComponent(Life.Gate, gain: PlaceHum));
            }
            else
            {
                components.Add(new ModelComponent(_characters.Resolve(visual.Value.Model)));
                components.Add(new ModelRenderTag());
                IReadOnlyList<Dino_Engine.World.Content.AssetLight> glows = CharacterFactory.LightsOf(visual.Value.Model);
                if (glows.Count > 0) components.Add(new PropLightsComponent(glows));
                if (loot is PickupComponent find)
                {
                    // Gold catches the eye by shining on what's around it, and so does anything better than plain, which starts to when it lands.
                    if (find.Find == 0)
                    {
                        components.Add(new PointLightTag());
                        components.Add(new LightRangeComponent(GoldLightRadius, 0.2f, lightsFog: false));
                        components.Add(new ColorComponent(new Colour(1f, 0.74f, 0.3f, 0f)));
                    }
                    else if (LootShow.GlowStrength(find.Rarity) > 0f)
                    {
                        Colour light = LootShow.GlowOf(find.Rarity);
                        components.Add(new PointLightTag());
                        components.Add(new LightRangeComponent(LootShow.LightRadius, 0.2f, lightsFog: false));
                        components.Add(new ColorComponent(new Colour(light.Red, light.Green, light.Blue, 0f)));
                    }
                    if (LootShow.BeamOf(find.Rarity) != null) components.Add(new BeamComponent());
                }
                else if (visual.Value.Model.StartsWith(CharacterFactory.BoltPrefix) && Enum.TryParse(visual.Value.Model.AsSpan(CharacterFactory.BoltPrefix.Length), out DamageType look))
                {
                    // A pack of casters can have dozens in the air, and it's the hero's own that the eye is on: one in three of theirs still lights the ground.
                    if (!hostileBolt || ++_hostileBolts % HostileBoltsPerLight == 0)
                    {
                        Colour light = ClientEffects.LightOf(look);
                        components.Add(new PointLightTag());
                        components.Add(new LightRangeComponent(BoltLightRadius, 0.2f, lightsFog: false));
                        components.Add(new ColorComponent(new Colour(light.Red, light.Green, light.Blue, 40f)));
                    }

                    // What it leaves in the air behind it as it flies.
                    if (ParticleShow.BoltOf(look) is ParticleEffect trail) components.Add(new ParticleEffectComponent(trail, EffectShape.Point, prewarm: false));
                }

                // The only thing that can be used with no light of its own, and it's brown on brown ground.
                foreach (IComponent component in components)
                {
                    if (component is not InteractableComponent { Kind: InteractKind.Chest }) continue;
                    components.Add(new PointLightTag());
                    components.Add(new LightRangeComponent(ChestLightRadius, 0.2f, lightsFog: false));
                    components.Add(new ColorComponent(new Colour(1f, 0.62f, 0.16f, ChestLight)));
                    break;
                }
            }
        }

        private const float GoldLightRadius = 6f;
        private const float PortalHum = 0.8f;
        private const float PlaceHum = 0.55f;   // It never stops, so it sits under the land's own sound. At full level it's as loud as all the rest together.
        private const float ChestLightRadius = 9f;
        private const float ChestLight = 14f;
        private const float BoltLightRadius = 11f;
        private const int HostileBoltsPerLight = 3;

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
        private const float BuriedDepth = 2.6f;     // How far under the ground a body goes as it's cleared away.
        private const float AlertFor = 3.5f;        // Seconds someone stays ready after doing something.
        private const float DashSpeed = 18f;        // Faster than this, what's seen going by isn't running.
        private const float ChilledRate = 0.65f;
        private const float LookReach = 34f;
        private const double ArrowCatchUp = 15.0;   // Ticks over which an arrow moves up into the player's own moment.
        private const float ArrowHitReach = 2.4f;
        private const float BoltSize = 0.42f;
        private const float ThrownTurn = 0.3f;      // Radians a tick, for what's turning end over end.

        private readonly Func<GameClient?> _client;
        private readonly Func<ClientEffects?> _effects;
        private readonly Wardrobe _wardrobe;
        private readonly BitMask _zoneMask = new BitMask(typeof(ZoneComponent), typeof(PositionComponent));
        private readonly BitMask _afflictedMask = new BitMask(typeof(AfflictedComponent), typeof(PositionComponent));
        private readonly BitMask _motionMask = new BitMask(typeof(GhostMotionComponent), typeof(PositionComponent), typeof(VelocityComponent));
        private readonly BitMask _bodyMask = new BitMask(typeof(ActionComponent), typeof(AnimatorComponent));
        private readonly BitMask _enemyMask = new BitMask(typeof(EnemyComponent), typeof(AnimatorComponent));
        private readonly BitMask _heroMask = new BitMask(typeof(HeroComponent), typeof(HealthComponent), typeof(AnimatorComponent));
        private readonly BitMask _dressedMask = new BitMask(typeof(HeroComponent), typeof(CharacterModelComponent));
        private readonly BitMask _arrowMask = new BitMask(typeof(BallisticComponent), typeof(PositionComponent), typeof(ScaleComponent));
        private readonly BitMask _lidMask = new BitMask(typeof(ChestLidComponent), typeof(RotationComponent));

        public GhostPresentationSystem(Func<GameClient?> client, Func<ClientEffects?> effects, Wardrobe wardrobe)
            : base(new BitMask(typeof(PositionComponent)))
        {
            _client = client;
            _effects = effects;
            _wardrobe = wardrobe;
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime) { }

        protected override void UpdateFrame(ECSWorld world, float deltaTime)
        {
            _wardrobe.Update();

            GameClient? client = _client();
            GameSession? session = client?.Session;
            if (client == null || session == null || client.State != ClientState.Playing || !client.Render.Started) return;

            client.Ghosts.Apply(client.Render.RenderTick);

            MoveOwnHero(world, client, session);
            if (deltaTime > 0f) ReadMotion(world, deltaTime);
            DriveBodies(world, client, session);
            LayDownFallen(world, deltaTime);
            LayDownHeroes(world);
            if (_effects() is ClientEffects shown)
            {
                shown.Marks.Update(world, client, session, deltaTime);
                shown.Trails.Update(world, deltaTime);
            }
            FlyArrows(world, client, session);
            SwingLids(world, deltaTime);
            LootShow.Update(world, client, session, _effects(), _wardrobe, deltaTime);
            DressHeroes(world, client);
            ShowLasting(world, deltaTime);
        }

        // A chest's lid comes up on its hinge when it's opened, and stays up. It's set every frame, as what the server sends would turn it back to its yaw, and a few in a world cost nothing.
        private void SwingLids(ECSWorld world, float deltaTime)
        {
            foreach (Archetype archetype in world.QueryArchetypes(_lidMask, BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    var lid = entity.Get<ChestLidComponent>();
                    if (lid.Age < ChestLook.Swing)
                    {
                        lid.Age = MathF.Min(lid.Age + deltaTime, ChestLook.Swing);
                        entity.Set(lid);
                    }
                    entity.Set(new RotationComponent(ChestLook.Raised(lid.Yaw, ChestLook.AngleAt(lid.Age))));
                }
            }
        }

        // A hero is drawn in what they have on, and keeps being asked for by what's drawing them. Whoever hasn't been described yet just stands there as they are.
        private void DressHeroes(ECSWorld world, GameClient client)
        {
            foreach (Archetype archetype in world.QueryArchetypes(_dressedMask, BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    int player = entity.Get<HeroComponent>().Player;
                    if (!client.Worn.TryGetValue(player, out List<WornLook>? worn)) continue;
                    if (_wardrobe.Dressed(player - 1, worn) is not Dino_Engine.Modelling.Model.glModel model) continue;
                    if (entity.Get<CharacterModelComponent>().model == model) continue;

                    entity.Set(new CharacterModelComponent(model));
                    if (entity.Has<AnimatorComponent>()) SkillMotions.Hold(entity.Get<AnimatorComponent>().Animator, worn);
                }
            }
        }

        // What's going on on the ground and on people, which has no event of its own as it doesn't happen at any one moment.
        private void ShowLasting(ECSWorld world, float deltaTime)
        {
            if (_effects() is not ClientEffects effects || deltaTime <= 0f) return;

            foreach (Archetype archetype in world.QueryArchetypes(_zoneMask, BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    var zone = entity.Get<ZoneComponent>();
                    if (zone.Age == 0f) effects.Sounds.ZoneOpens(PageSign.Read(zone.Sign).Look, entity.Get<PositionComponent>().value);
                    zone.Age += deltaTime;
                    entity.Set(zone);

                    // The ground it covers goes wherever it does, and dies down as its time runs out.
                    if (!entity.Has<ParticleEffectComponent>() || entity.Get<ParticleEffectComponent>().Run is not EffectRun run || run.Shape.Area is not GroundArea area) continue;
                    area.Shape = zone.ShapeAt(entity.Get<PositionComponent>().value.Xz);
                    run.Strength = zone.Life > 0f ? Math.Clamp((zone.Life - zone.Age) / ZoneDyingTime, 0f, 1f) : 1f;
                }
            }

            foreach (Archetype archetype in world.QueryArchetypes(_afflictedMask, BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    effects.Ailments.Show(entity, entity.Entity.Id, entity.Get<AfflictedComponent>());
                }
            }
            effects.Ailments.EndFrame();
        }

        private const float ZoneDyingTime = 0.6f;

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

        // What each body has been told. A hero is doing what the player is doing right now. Everyone else is doing what the server said, and all of that's a start and an end and a clock to read them by: enemies by this
        // player's own clock, the one the hero moves by, so that their blows are seen to land on the tick they land on and a dodge that looked in time was.
        private void DriveBodies(ECSWorld world, GameClient client, GameSession session)
        {
            double past = client.Render.RenderTick;
            double present = client.Commands.Tick + client.Commands.Alpha;
            uint own = client.OwnGhost?.Entity.Id ?? 0;

            foreach (Archetype archetype in world.QueryArchetypes(_bodyMask, BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    CharacterAnimator animator = entity.Get<AnimatorComponent>().Animator;
                    if (entity.Entity.Id == own)
                    {
                        DriveOwnHero(animator, client, session, present);
                        continue;
                    }

                    animator.Outline = entity.Has<EnemyComponent>() ? OutlineOf(entity.Get<EnemyComponent>()) : Vector4.Zero;

                    var action = entity.Get<ActionComponent>();
                    bool hero = entity.Has<HeroComponent>();
                    double clock = hero ? past : present;
                    animator.Orders.Action = Reading(action, clock, animator);
                    if (animator.Orders.Action.Gesture == null && _effects() is ClientEffects doing) animator.Orders.Action = doing.FlourishOf(entity.Entity, animator.Orders.Yaw);

                    double since = (clock - action.Ends) * NetTime.TickSecondsPrecise;
                    float alert = action.Sign != PageSign.None && since < AlertFor ? 1f : 0f;
                    bool looking = false;
                    if (entity.Has<EnemyComponent>())
                    {
                        var enemy = entity.Get<EnemyComponent>();
                        if (enemy.State is EnemyState.Chasing or EnemyState.WindingUp or EnemyState.Recovering)
                        {
                            alert = 1f;
                            Vector2 at = session.PlayerPosition;
                            if (!enemy.Friendly && (entity.Get<PositionComponent>().value.Xz - at).LengthSquared < LookReach * LookReach)
                            {
                                animator.Orders.LookAt = new Vector3(at.X, entity.Get<PositionComponent>().value.Y + 3f, at.Y);
                                looking = true;
                            }
                        }
                    }
                    animator.Orders.Looking = looking;
                    animator.Orders.Alert = alert;
                    Afflict(animator, entity);

                    // Nothing's sent to say someone else is dodging: it can be seen.
                    Vector3 velocity = entity.Has<VelocityComponent>() ? entity.Get<VelocityComponent>().value : Vector3.Zero;
                    animator.Orders.Dash = velocity.LengthSquared > DashSpeed * DashSpeed ? velocity.Xz.Normalized() : Vector2.Zero;
                }
            }
        }

        // How rare something is has to be seen at a glance in a crowd, so it's drawn round it in the colour of what it might drop. Whatever's normal has nothing, or nothing would stand out.
        private static Vector4 OutlineOf(in EnemyComponent enemy)
        {
            if (enemy.Rarity == Rarity.Normal || enemy.Friendly || enemy.State == EnemyState.Dead) return Vector4.Zero;
            Colour colour = LootShow.GlowOf(enemy.Rarity);
            return new Vector4(colour.Red, colour.Green, colour.Blue, 1f);
        }

        private static ActionInput Reading(in ActionComponent action, double clock, CharacterAnimator animator)
        {
            if (action.Sign == PageSign.None || clock < action.Start || clock >= action.Ends) return default;

            return new ActionInput
            {
                Gesture = SkillMotions.Of(PageSign.Read(action.Sign), animator.Carrying, animator.Rig, action.Serial),
                Elapsed = (float)((clock - action.Start) * NetTime.TickSecondsPrecise),
                Impact = (action.Lands - action.Start) * NetTime.TickSeconds,
                Length = (action.Ends - action.Start) * NetTime.TickSeconds,
                Aim = action.Aim,
                Serial = action.Serial,
            };
        }

        private void DriveOwnHero(CharacterAnimator animator, GameClient client, GameSession session, double present)
        {
            HeroSimState sim = client.Predictor.Current;
            ClientEffects? effects = _effects();
            OwnAction? doing = effects?.Doing;

            animator.Orders.Action = default;
            if (doing is OwnAction action && sim.Using >= 0)
            {
                // Their own rules say how far through it they are, which stops with the world and slows with them. The clock only says how far on from the last tick this moment is.
                float betweenTicks = session.Paused && client.Alone ? 1f : (float)(present - client.Commands.Tick) + 1f;
                float elapsed = action.Length - sim.BusyFor + betweenTicks * NetTime.TickSeconds * sim.ActionRate;
                animator.Orders.Action = new ActionInput
                {
                    Gesture = SkillMotions.Of(action.Sign, animator.Carrying, false, action.Serial),
                    Elapsed = elapsed,
                    Impact = action.Impact,
                    Length = action.Length,
                    Aim = action.Aim,
                    Serial = action.Serial,
                };
            }

            if (animator.Orders.Action.Gesture == null && effects != null) animator.Orders.Action = effects.FlourishOf(client.OwnGhost!.Entity, animator.Orders.Yaw);

            double since = doing is OwnAction last ? (present - last.Start) * NetTime.TickSecondsPrecise - last.Length : double.MaxValue;
            animator.Orders.Alert = since < AlertFor ? 1f : 0f;

            float reach = MathF.Max(sim.AimReach, 4f);
            Vector2 at = session.PlayerPosition;
            animator.Orders.LookAt = new Vector3(at.X + sim.Aim.X * reach, session.World.Height.Sample(at) + 3f, at.Y + sim.Aim.Y * reach);
            animator.Orders.Looking = sim.Using >= 0;
            animator.Orders.Dash = sim.DashLeft > 0f ? sim.DashDirection : Vector2.Zero;
            animator.Orders.Rate = (sim.State & Condition.Frozen) != 0 ? 0f : (sim.State & Condition.Chilled) != 0 ? ChilledRate : 1f;
            animator.Orders.Dazed = (sim.State & Condition.Stunned) != 0;
        }

        private static void Afflict(CharacterAnimator animator, in EntityView entity)
        {
            if (!entity.Has<AfflictedComponent>()) return;
            var afflicted = entity.Get<AfflictedComponent>();
            animator.Orders.Rate = afflicted.Has(Condition.Frozen) ? 0f : afflicted.Has(Condition.Chilled) ? ChilledRate : 1f;
            animator.Orders.Dazed = afflicted.Has(Condition.Stunned);
        }

        // Whoever falls stays where they fall, and goes down into the ground when their time's up.
        private void LayDownFallen(ECSWorld world, float deltaTime)
        {
            foreach (Archetype archetype in world.QueryArchetypes(_enemyMask, BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    var enemy = entity.Get<EnemyComponent>();
                    if (enemy.State != EnemyState.Dead) continue;

                    CharacterAnimator animator = entity.Get<AnimatorComponent>().Animator;
                    if (!animator.Dead) animator.Die(enemy.Thrown);

                    enemy.DeadFor += deltaTime;
                    entity.Set(enemy);
                    float end = DyingSystem.FallTime + DyingSystem.LieTime;
                    animator.Sink = WorldMath.Smoothstep(end, end + DyingSystem.FadeTime, enemy.DeadFor) * BuriedDepth;
                }
            }
        }

        private void LayDownHeroes(ECSWorld world)
        {
            foreach (Archetype archetype in world.QueryArchetypes(_heroMask, BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    CharacterAnimator animator = entity.Get<AnimatorComponent>().Animator;
                    bool down = entity.Get<HealthComponent>().Current <= 0f;
                    if (down && !animator.Dead) animator.Die(entity.Get<HeroComponent>().Thrown);
                    else if (!down && animator.Dead) animator.Revive();
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
                    bool struck = arrow.Velocity.Xz == Vector2.Zero ? position.Y < session.World.Height.Sample(position.Xz) : BallisticComponent.Stopped(session.World, position);
                    bool spent = struck || (!arrow.FromHeroes && (position.Xz - session.PlayerPosition).LengthSquared < ArrowHitReach * ArrowHitReach);

                    // What the world stopped stays where it struck, a little back the way it came, or a bolt's light flies on into the wall and shines out of it.
                    if (struck && arrow.Velocity.Xz != Vector2.Zero) position = StruckAt(session.World, arrow, Math.Max(tick, arrow.StartTick));

                    entity.Set(new PositionComponent(position));
                    if (entity.Has<VisualComponent>() && entity.Get<VisualComponent>().Model.StartsWith(Thrown.Prefix)) Tumble(entity, arrow, tick);
                    bool bolt = entity.Has<VisualComponent>() && entity.Get<VisualComponent>().Model.StartsWith(CharacterFactory.BoltPrefix);
                    entity.Set(new ScaleComponent(new Vector3(spent ? 0.001f : bolt ? BoltSize : 1f)));
                    // Its trail ends where it does, though the server's word on it is still to come.
                    if (entity.Has<ParticleEffectComponent>() && entity.Get<ParticleEffectComponent>().Run is EffectRun trail) trail.Strength = spent ? 0f : 1f;
                }
            }
        }

        // The last place along its line the world didn't stop it, by halving, then StruckStandOff back towards where it came from, never past its start.
        private static Vector3 StruckAt(WorldData world, in BallisticComponent arrow, double tick)
        {
            double clear = arrow.StartTick;
            double inside = tick;
            if (BallisticComponent.Stopped(world, arrow.PositionAt(clear))) return arrow.Origin;
            for (int i = 0; i < 14; i++)
            {
                double middle = (clear + inside) * 0.5;
                if (BallisticComponent.Stopped(world, arrow.PositionAt(middle))) inside = middle;
                else clear = middle;
            }
            Vector3 at = arrow.PositionAt(clear);
            Vector3 back = arrow.Origin - at;
            float flown = back.Length;
            return flown > 1e-4f ? at + back * (MathF.Min(StruckStandOff, flown) / flown) : at;
        }

        private const float StruckStandOff = 0.5f;  // Metres, on top of the Girth the world already keeps it off what it hit.

        // End over end, along the way it's going. What a hero threw is the thing in their hands, which has to keep being asked for.
        private void Tumble(in EntityView entity, in BallisticComponent flight, double tick)
        {
            float yaw = MathF.Atan2(flight.Velocity.X, flight.Velocity.Z);
            float turned = (float)(tick - flight.StartTick) * ThrownTurn;
            entity.Set(new RotationComponent(Quaternion.FromAxisAngle(Vector3.UnitY, yaw) * Quaternion.FromAxisAngle(Vector3.UnitX, turned)));

            if (!entity.Has<ThrownComponent>()) return;
            var thrown = entity.Get<ThrownComponent>();
            if (_wardrobe.Lying(thrown.Seed, thrown.Traits) is Dino_Engine.Modelling.Model.glModel model) entity.Set(new ModelComponent(model));
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
            Entity thing = default;
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
                    thing = entity.Entity;
                }
            }

            bool up = session.Health > 0f;
            session.InteractPrompt = up ? prompt : null;
            session.Offered = up ? kind : null;
            session.OfferedThing = up ? thing : default;
        }
    }

    /// <summary>What this player's hero is doing, from the tick they pressed.</summary>
    public readonly record struct OwnAction(PageSign Sign, uint Start, float Impact, float Length, float Aim, int Serial);

    /// <summary>Sparks, numbers and gusts: what things look like happening, whether told by the server or done by this player a moment ago.</summary>
    public sealed class ClientEffects
    {
        private const int MostCallouts = 2;

        // How a blow lands. It's all in terms of how much of whoever it hit it took: a little is a flinch, and a lot stops everything for a moment. Sakurai's rule is that both sides stop for the same time, longer for a
        // bigger hit, and never long: these are the longest a fight can stand still and still be a fight. Time only stops for bodies here. The rules don't wait for this, or for anything.
        private const float LeastFlinch = 0.26f;
        private const float FlinchPerHealth = 2.2f;
        private const float CriticalFlinch = 0.35f;
        private const float LeastHeld = 0.03f;
        private const float HeldPerHealth = 0.14f;
        private const float MostHeld = 0.085f;
        private const float HeroHeld = 0.02f;
        private const float BigBlow = 0.4f;         // Of what they had. Past this the camera feels it.
        private const float JoltSpeed = 4.2f;

        public readonly GroundMarks Marks = new GroundMarks();
        public readonly WeaponTrails Trails = new WeaponTrails();

        /// <summary>The last thing this player's hero started.</summary>
        public OwnAction? Doing { get; private set; }

        private const float DrinkTime = 0.9f;
        private const float UseTime = 0.7f;
        private const float FlourishImpact = 0.4f;  // Of the way through, that it's at its height.

        private readonly Dictionary<uint, (Gesture Gesture, float Start, float Length, int Serial)> _flourishes = new();
        private int _flourished = 1 << 20;          // Clear of what a skill's serial can be.

        private void Flourished(GameClient client, in FlourishEvent e)
        {
            if (!client.Ghosts.Ghosts.TryGetValue(e.Who, out Ghost? ghost) || !ghost.Entity.IsValid()) return;
            bool drink = e.Kind == Flourish.Drink;
            _flourishes[ghost.Entity.Id] = (drink ? Gestures.Drink : Gestures.Use, Engine.Time, drink ? DrinkTime : UseTime, ++_flourished);
        }

        /// <summary>What someone with nothing else to do is doing for show, if anything. They go on facing as they are: it's only their hands.</summary>
        public ActionInput FlourishOf(Entity who, float facing)
        {
            if (_flourishes.Count == 0 || !_flourishes.TryGetValue(who.Id, out var flourish)) return default;

            float elapsed = Engine.Time - flourish.Start;
            if (elapsed >= flourish.Length)
            {
                _flourishes.Remove(who.Id);
                return default;
            }
            return new ActionInput { Gesture = flourish.Gesture, Elapsed = elapsed, Impact = flourish.Length * FlourishImpact, Length = flourish.Length, Aim = facing, Serial = flourish.Serial };
        }

        private int _serial;

        private static readonly Vector3 HitColour = new Vector3(1f, 0.92f, 0.7f);
        private static readonly Vector3 CritColour = new Vector3(1f, 0.55f, 0.15f);
        private static readonly Vector3 HurtColour = new Vector3(1f, 0.25f, 0.2f);
        private static readonly Vector3 HealColour = new Vector3(0.45f, 0.95f, 0.45f);
        private static readonly Vector3 ManaColour = new Vector3(0.4f, 0.6f, 1f);
        private static readonly Vector3 BarrierColour = new Vector3(0.7f, 0.85f, 1f);

        private readonly ECSWorld _world;
        private readonly GameClient _client;
        private readonly WorldRandom _random = new WorldRandom(Environment.TickCount);

        public readonly GameSounds Sounds;

        public ClientEffects(ECSWorld world, GameClient client)
        {
            _world = world;
            Sounds = new GameSounds(world, client, this);

            // What happens to this hero happens now, however far behind everything else is drawn.
            client.Events.Urgent<DamageEvent>(e => e.Target != 0 && e.Target == client.OwnGhost?.Id);
            client.Events.Urgent<FlourishEvent>(e => e.Who == client.OwnGhost?.Id);
            client.Events.On<FlourishEvent>(e => Flourished(client, e));

            client.Events.On<DamageEvent>(e =>
            {
                Landed(client, e);
                client.Session?.Meter.Landed(e, client, Engine.Time);
                if (e.Note != HitNote.None) return;   // Only damage puts anything over a head, and only as a number.

                Vector3 colour = e.ToHero ? HurtColour : e.Critical ? CritColour : e.Type == DamageType.Physical ? HitColour : TextColourOf(e.Type);
                float size = e.ToHero ? 17f : e.Critical ? 22f : 16f;
                client.Session?.Say(e.Position, $"{e.Amount:0}", colour, size, 0.9f);
            });
            client.Events.On<PageEvent>(ShowPage);
            client.Events.On<HealEvent>(e => client.Session?.Say(e.Position, $"+{e.Amount:0}", e.Pool switch { Pool.Mana => ManaColour, Pool.Barrier => BarrierColour, _ => HealColour }, 16f, 0.9f));
            client.Events.On<LinkEvent>(ShowLink);
            client.Events.On<ReactionEvent>(e =>
            {
                // A reaction shows by its burst alone: words over a fight are only ever numbers.
                Burst(ReactionOf(e.Kind), e.Position, e.Radius);
            });
            client.Events.On<TextEvent>(e => client.Session?.Say(e.Position, e.Text, e.Colour, e.Size, e.Life));
            client.Events.On<SparkEvent>(e =>
            {
                switch (e.Kind)
                {
                    case SparkKind.Shrine:
                        Particles.Play(Arcane.Shrine, e.Position);
                        break;
                    case SparkKind.Blood:
                        Particles.Play(Blood.Hit, e.Position);
                        break;
                    case SparkKind.Blade:
                        Particles.Play(Impact.Sparks, e.Position);
                        break;
                    default:
                        Burst(Impact.Shockwave, e.Position, 2f);
                        break;
                }
            });
            client.Events.On<HeroMoveEvent>(e => Show(e.Move, e.Position, e.Aim, e.Sign));
            client.Reached += (item, room) => Sounds.Reached(item, room);
            _client = client;
        }

        // A blow arriving on someone's body, as far as it's still news.
        private void Landed(GameClient client, in DamageEvent e)
        {
            if (e.Note is HitNote.Evaded or HitNote.Dodged || !client.Ghosts.Ghosts.TryGetValue(e.Target, out Ghost? ghost) || !ghost.Entity.IsValid()) return;
            if (!_world.TryGetEntityLocation(ghost.Entity, out _, out _)) return;

            EntityView view = _world.GetEntityView(ghost.Entity);
            if (!view.Has<AnimatorComponent>()) return;

            CharacterAnimator body = view.Get<AnimatorComponent>().Animator;
            bool own = ghost == client.OwnGhost;
            Vector3 way = new Vector3(e.Way.X, 0f, e.Way.Y);

            // This player's own blow was shown the moment it landed. All that's left to say is whether it turned out to be a good one, and what it was made of.
            if (body.JustStruck && !own)
            {
                if (e.Critical) Jolt(e.Way, JoltSpeed);
                if (ParticleShow.HitOf(e.Type) is Dino_Engine.Particles.ParticleEffect element) Particles.Play(element, e.Position, forward: way);
                return;
            }

            Vector3 colour = e.Note == HitNote.Blocked ? new Vector3(0.9f, 0.9f, 0.85f) : own ? new Vector3(1f, 0.16f, 0.1f) : FlashOf(e.Type);
            Strike(body, way, e.Weight, e.Critical, colour, own ? HeroHeld : HeldBy(e.Weight, e.Critical));
            if (own || e.Weight >= BigBlow) Jolt(e.Way, own ? JoltSpeed * 0.6f : JoltSpeed);

            // What comes off the blow: sparks off a shield, or what the body's made of, or the element it was dealt in.
            if (e.Note == HitNote.Blocked) Particles.Play(Impact.Sparks, e.Position, forward: way);
            else if (ParticleShow.HitOf(e.Type) is Dino_Engine.Particles.ParticleEffect hit) Particles.Play(hit, e.Position, forward: way);
            else ParticleShow.Wound(e.Position, e.Way, e.Body == HitBody.Bone ? ParticleShow.Flesh.Bone : FleshOf(view), e.Body == HitBody.Armour, e.Weight, e.Critical);
        }

        private static ParticleShow.Flesh FleshOf(in EntityView view) => view.Has<VisualComponent>() ? ParticleShow.FleshOf(view.Get<VisualComponent>().Model) : ParticleShow.Flesh.Blood;

        /// <summary>This player has just struck something, as far as can be seen from here.</summary>
        public void Connected(EntityView target, Vector2 way, float weight, bool heavy, Vector3 at)
        {
            CharacterAnimator body = target.Get<AnimatorComponent>().Animator;
            float held = HeldBy(weight, false);
            Strike(body, new Vector3(way.X, 0f, way.Y), weight, false, new Vector3(1f, 0.95f, 0.85f), held);
            ParticleShow.Wound(at, way, FleshOf(target), false, weight, false);

            if (_client.OwnGhost is not Ghost own || !_world.TryGetEntityLocation(own.Entity, out _, out _)) return;
            _world.GetEntityView(own.Entity).Get<AnimatorComponent>().Animator.Connected(held);
            if (heavy) Jolt(way, JoltSpeed);
        }

        private static void Strike(CharacterAnimator body, Vector3 way, float weight, bool critical, Vector3 colour, float held)
        {
            float strength = LeastFlinch + MathF.Min(weight * FlinchPerHealth, 1.1f) + (critical ? CriticalFlinch : 0f);
            body.Struck(way, strength, held, colour);
        }

        private static float HeldBy(float weight, bool critical) => MathF.Min(LeastHeld + weight * HeldPerHealth + (critical ? 0.02f : 0f), MostHeld);

        // What a hit looks like going through someone. It's the same colour as the number that comes up off it.
        private static Vector3 FlashOf(DamageType type)
        {
            if (type == DamageType.Physical) return new Vector3(1f, 0.95f, 0.85f);
            Colour light = LightOf(type);
            return new Vector3(light.Red, light.Green, light.Blue);
        }

        private void Jolt(Vector2 way, float speed)
        {
            if (!_world.GameCamera.IsValid() || !_world.TryGetEntityLocation(_world.GameCamera, out _, out _)) return;
            EntityView camera = _world.GetEntityView(_world.GameCamera);
            var follow = camera.Get<FollowCameraComponent>();
            follow.JoltSpeed += new Vector3(way.X, -0.3f, way.Y) * speed;
            camera.Set(follow);
        }

        /// <summary>Something has just left what it came out of. A unique's whole sound starts here, and breaks as it lands.</summary>
        public void LootLaunched(Rarity rarity, bool gold, Vector3 position)
        {
            if (!gold) Sounds.LootLaunched(rarity, position);
        }

        /// <summary>Something has come down, for all to see.</summary>
        public void LootLanded(int find, Rarity rarity, ItemSlot slot, bool gold, Vector3 position)
        {
            Sounds.LootLanded(rarity, slot, gold, position);
            if (gold || rarity < Rarity.Magic) return;

            Colour glow = LootShow.GlowOf(rarity);
            Vector3 tint = new Vector3(glow.Red, glow.Green, glow.Blue);
            if (rarity == Rarity.Magic)
            {
                Particles.Play(Impact.Landing, position, 0.45f, tint: tint, strength: 0.6f);
                return;
            }

            // A unique is bigger in every way, and moves the land itself.
            int step = rarity == Rarity.Unique ? UniqueStep : 0;
            Flash(position + Vector3.UnitY * 2.5f, new Colour(glow.Red, glow.Green, glow.Blue, 260f + 240f * step), 16f + 13f * step, 0.6f + 0.4f * step);
            Particles.Play(Impact.Landing, position, 1f + 0.6f * step, tint: tint, strength: 1f + 0.5f * step);
            if (step > 0) Wind.AddBlast(_world, position, 9f + 9f * step, 12f + 12f * step, 0.5f + 0.3f * step);
            if (step > 0) Announce(find, rarity);
        }

        private const int UniqueStep = 2;

        private void Announce(int find, Rarity rarity)
        {
            GameSession? session = _client.Session;
            if (session == null || !session.Ground.TryGetValue(find, out Item? item)) return;

            // A boss can pour out more of these than there's room for, and it gives its best last.
            if (session.Callouts.Count >= MostCallouts) session.Callouts.RemoveAt(0);
            session.Callouts.Add(new LootCallout { Text = item.Name, Rarity = rarity });
        }

        /// <summary>Something good, already lying there when it came into sight.</summary>
        public void LootDiscovered(Rarity rarity, Vector3 position)
        {
            Sounds.LootDiscovered(rarity, position);
        }

        /// <summary>A spark off something good, in the air or rising up its beam.</summary>
        public void LootMote(Rarity rarity, Vector3 position, float height)
        {
            Vector3 at = position + new Vector3(_random.Range(-0.5f, 0.5f), _random.Range(0f, height), _random.Range(-0.5f, 0.5f));
            Colour glow = LootShow.GlowOf(rarity);
            var spawn = new ParticleSpawn(at, _random.Range(0.1f, 0.16f), _random.Range(0.7f, 1.1f))
            {
                Velocity = new Vector3(_random.Range(-0.3f, 0.3f), _random.Range(0.8f, 1.6f), _random.Range(-0.3f, 0.3f)),
                Tint = new Vector3(glow.Red, glow.Green, glow.Blue),
            };
            Particles.Spawn(Glows.Glint(ParticlePalette.White), spawn, ParticlePriority.Ambient);
        }

        /// <summary>
        /// What a type of damage looks like as something glowing in the air. These are deep on purpose: particles go straight into a scene lit by a sun of
        /// about 13, and anything pale or bright comes out the same washed-out white, which would leave a player unable to tell fire from frost.
        /// </summary>
        public static Colour GlowOf(DamageType type) => type switch
        {
            DamageType.Fire => new Colour(1f, 0.24f, 0.02f, 1.5f),
            DamageType.Cold => new Colour(0.08f, 0.42f, 1f, 1.5f),
            DamageType.Lightning => new Colour(0.3f, 0.36f, 1f, 1.9f),
            DamageType.Magic => new Colour(0.52f, 0.06f, 1f, 1.5f),
            DamageType.Poison => new Colour(0.16f, 0.8f, 0.04f, 1.3f),
            _ => new Colour(0.8f, 0.66f, 0.46f, 1f),
        };

        /// <summary>What a type of damage looks like, as light.</summary>
        public static Colour LightOf(DamageType type) => type switch
        {
            DamageType.Fire => new Colour(1f, 0.46f, 0.12f, 1f),
            DamageType.Cold => new Colour(0.42f, 0.78f, 1f, 1f),
            DamageType.Lightning => new Colour(0.7f, 0.74f, 1f, 1f),
            DamageType.Magic => new Colour(0.72f, 0.3f, 1f, 1f),
            DamageType.Poison => new Colour(0.4f, 0.95f, 0.2f, 1f),
            _ => new Colour(0.95f, 0.88f, 0.74f, 1f),
        };

        public static Dino_Engine.Sound.Content.Element ElementOf(DamageType type) => type switch
        {
            DamageType.Fire => Element.Fire,
            DamageType.Cold => Element.Frost,
            DamageType.Lightning => Element.Lightning,
            DamageType.Poison => Element.Poison,
            DamageType.Magic => Element.Arcane,
            _ => Element.Shadow,
        };

        private static Vector3 TextColourOf(DamageType type)
        {
            Colour light = LightOf(type);
            return new Vector3(light.Red, light.Green, light.Blue);
        }

        private void ShowPage(PageEvent e)
        {
            PageSign sign = PageSign.Read(e.Sign);
            var shape = new GroundShape(e.Kind, e.Position.Xz, e.Facing, e.Length, e.Width);
            var area = new GroundArea(shape, GameSession.Current?.World);
            Vector3 centre = new Vector3(area.Middle.X, e.Position.Y, area.Middle.Y);

            if (!e.Quiet) Sounds.Burst(sign.Look, e.Position);

            // A round blast goes off all over its circle, with a ring out to its edge, and anything else all over its ground. What goes on doing this is seen more faintly each time.
            bool round = e.Kind == ShapeKind.Circle;
            float radius = round ? e.Length : MathF.Sqrt(area.SquareMetres / MathF.PI);
            Particles.Play(ParticleShow.BurstOf(sign.Look), round ? e.Position : centre, BurstScale(radius), round && !area.Cut ? EffectShape.Ball(MathF.Max(e.Length, 0.5f)) : EffectShape.Over(area),
                new Vector3(e.Facing.X, 0f, e.Facing.Y), strength: e.Quiet ? 0.5f : 1f);
            if (e.Length > 4f && !e.Quiet) Wind.AddBlast(_world, centre, e.Length * 1.6f, 14f, 0.6f);
            Marks.Struck(shape, _client.Commands.Tick);
        }

        // Bursts are made for a blast a few metres across and spread over anything wider, so what's in them grows only a little with it.
        private static float BurstScale(float radius) => Math.Clamp(MathF.Sqrt(radius / EffectRun.SpreadRadius), 0.7f, 1.6f);

        private static void Burst(ParticleEffect effect, Vector3 position, float radius)
        {
            Particles.Play(effect, position, BurstScale(radius), EffectShape.Ball(MathF.Max(radius, 0.5f)));
        }

        private static ParticleEffect ReactionOf(Reaction reaction) => reaction switch
        {
            Reaction.Scald => Smoke.Scald,
            Reaction.Crackle => Frost.Burst,
            Reaction.Flashfire => Fire.Burst,
            Reaction.Shatter => Frost.Burst,
            _ => Lightning.Burst,
        };

        // From one to the next, as a chain jumps: lightning arcs across, and anything else leaves a streak of itself along the way.
        private void ShowLink(LinkEvent e)
        {
            Vector3 way = e.To - e.From;
            if (e.Look == DamageType.Lightning)
            {
                float width = e.Wide ? 0.16f : 0.1f;
                Arcs.Strike(Lightning.Arc, e.From, e.To, width, 0.26f, Vector3.One, 1f, forks: 2, priority: ParticlePriority.Vital);
                Arcs.Strike(Lightning.Arc, e.From, e.To, width * 0.7f, 0.12f, Vector3.One, 0.8f, forks: 1, priority: ParticlePriority.Vital);
            }
            else
            {
                Trace(e.From, e.To, e.Look, e.Wide);
            }
            if (ParticleShow.HitOf(e.Look) is ParticleEffect hit) Particles.Play(hit, e.To, 0.8f, forward: way);

            Colour light = GlowOf(e.Look);
            Flash(e.To, new Colour(light.Red, light.Green, light.Blue, 90f), 9f, 0.2f);
        }

        private void Trace(Vector3 from, Vector3 to, DamageType look, bool wide)
        {
            ParticleStyle style = look == DamageType.Fire ? Fire.Gout : Glows.Core(PaletteOf(look));
            float size = (look == DamageType.Fire ? 0.55f : 0.45f) * (wide ? 1.5f : 1f);
            Vector3 way = to - from;
            int steps = Math.Clamp((int)(way.Length / (size * 0.7f)), 3, 48);
            for (int i = 0; i <= steps; i++)
            {
                float share = (float)i / steps;
                Vector3 jitter = new Vector3(_random.Range(-0.15f, 0.15f), _random.Range(-0.15f, 0.15f), _random.Range(-0.15f, 0.15f)) * size;
                var spawn = new ParticleSpawn(from + way * share + jitter, size * _random.Range(0.8f, 1.2f), _random.Range(0.2f, 0.32f) + share * 0.12f)
                {
                    Velocity = new Vector3(_random.Range(-0.4f, 0.4f), _random.Range(0f, 0.6f), _random.Range(-0.4f, 0.4f)),
                    Angle = _random.Range(0f, MathF.Tau),
                };
                Particles.Spawn(style, spawn, ParticlePriority.Vital);
            }
        }

        private static ParticlePalette PaletteOf(DamageType type) => type switch
        {
            DamageType.Fire => ParticlePalette.Fire,
            DamageType.Cold => ParticlePalette.Frost,
            DamageType.Lightning => ParticlePalette.Lightning,
            DamageType.Poison => ParticlePalette.Poison,
            DamageType.Magic => ParticlePalette.Arcane,
            _ => ParticlePalette.Fire,
        };

        /// <summary>What everyone suffering from something gives off, kept going for as long as it lasts.</summary>
        public readonly Ailments Ailments = new Ailments();

        /// <summary>This player's hero, the moment they acted.</summary>
        public void HeroActed(GameClient client, HeroStepEvents events, in HeroSimState hero)
        {
            GameSession? session = client.Session;
            if (session == null) return;

            BookProfile? book = hero.Using >= 0 ? client.Kit.Slots[hero.Using] : null;
            if ((events & HeroStepEvents.Used) != 0 && book != null)
            {
                Doing = new OwnAction(book.Sign, client.Commands.Tick, hero.ImpactIn, hero.BusyFor, MathF.Atan2(hero.Aim.X, hero.Aim.Y), ++_serial);

                // What it covers, for as long as it takes to land.
                if (book.ShowsArea)
                {
                    double leaves = client.Commands.Tick + hero.ImpactIn / MathF.Max(hero.ActionRate, 0.1f) * NetTime.TicksPerSecond;
                    Marks.Own(book, hero, client.Commands.Tick, leaves, leaves + book.Delay * NetTime.TicksPerSecond);
                }
            }

            // A blow nobody marked still shows where it went, for a moment.
            if ((events & HeroStepEvents.Landed) != 0 && book != null && book.Sign.Form == PageForm.Strike && !book.ShowsArea && book.First.DealsDamage)
            {
                Marks.Struck(GroundShape.Of(book, hero.Position, hero.Aim, hero.AimReach), client.Commands.Tick);
            }

            Vector3 position = new Vector3(hero.Position.X, session.World.Height.Sample(hero.Position), hero.Position.Y);
            Show(events, position, hero.Aim, book?.Sign.Number ?? PageSign.None);
            Sounds.HeroActed(events, position, hero.Aim, book);
        }

        private void Show(HeroStepEvents events, Vector3 position, Vector2 aim, int number)
        {
            if ((events & HeroStepEvents.Dashed) != 0) Wind.AddBlast(_world, position, 6f, 8f, 0.4f);
            if ((events & HeroStepEvents.Landed) == 0 || number == PageSign.None) return;
            PageSign sign = PageSign.Read(number);

            if (sign.Form == PageForm.Nova && !sign.Spell)
            {
                Wind.AddBlast(_world, position, 14f, 22f, 1f);
                Flash(position + Vector3.UnitY * 2.4f, new Colour(0.8f, 0.92f, 1f, 90f), 14f, 0.5f);
                Burst(Impact.Shockwave, position, 6f);
            }
            else if (sign.Form == PageForm.Leap)
            {
                // Coming down hard throws the dust up.
                Burst(Impact.Shockwave, position, 2.5f);
            }
            else if (sign.Spell)
            {
                Colour light = LightOf(sign.Look);
                Vector3 hand = position + new Vector3(aim.X * 1.5f, 3.2f, aim.Y * 1.5f);
                if (ParticleShow.HitOf(sign.Look) is ParticleEffect cast) Particles.Play(cast, hand, 0.6f, forward: new Vector3(aim.X, 0f, aim.Y));
                Flash(position + Vector3.UnitY * 3f, new Colour(light.Red, light.Green, light.Blue, 40f), 8f, 0.2f);
            }
        }

        /// <summary>A moment of light from what just happened here, dying away over <paramref name="life"/> seconds.</summary>
        private void Flash(Vector3 position, Colour colour, float radius, float life)
        {
            _world.CreateEntity("Flash",
                new PointLightTag(),
                new LightPulseTag(),
                new PositionComponent(position),
                new LocalToWorldMatrixComponent { value = Matrix4.CreateTranslation(position) },
                new LightRangeComponent(radius, 0.3f),
                new ColorComponent(colour),
                new selfDestroyComponent(life));
        }
    }

    /// <summary>
    /// The hero under the player's hands. WASD moves, the mouse aims. The mouse buttons, Q and 1 to 4 use what's in those slots, at
    /// where the mouse points, for as long as they're held. Space dodges and R drinks.
    /// </summary>
    public static class LocalInput
    {
        public static HeroIntent Read(Vector3 position, in HeroSimState hero)
        {
            var intent = new HeroIntent { Aim = ReadAim(position) };

            // Anything can be on anything: what's on the mouse asks the interface first, and what's on the keyboard whether someone's typing. The first two places are held, and the rest pressed.
            for (int slot = 0; slot < HeroState.BarSlots; slot++)
            {
                GameAction action = Bindings.Slot(slot);
                if (Bindings.OnMouse(action) ? Engine.Gui.WantsMouse : Engine.Gui.WantsKeyboard) continue;
                if (slot < HeroRules.HeldSlots ? Bindings.Down(action) : HeroRules.KeyPresses(hero, slot, Bindings.Down(action), Bindings.Pressed(action))) intent.Use(slot);
            }

            bool Usable(GameAction action) => Bindings.OnMouse(action) ? !Engine.Gui.WantsMouse : !Engine.Gui.WantsKeyboard;
            bool Held(GameAction action) => Usable(action) && Bindings.Down(action);
            bool Pressed(GameAction action) => Usable(action) && Bindings.Pressed(action);

            if (Held(GameAction.MoveUp)) intent.Move.Y += 1f;
            if (Held(GameAction.MoveDown)) intent.Move.Y -= 1f;
            if (Held(GameAction.MoveLeft)) intent.Move.X += 1f;
            if (Held(GameAction.MoveRight)) intent.Move.X -= 1f;
            intent.Dash = Pressed(GameAction.Dodge);
            intent.Flask = Pressed(GameAction.Flask);
            intent.Interact = Pressed(GameAction.Interact);
            intent.Guide = Pressed(GameAction.Guide) && GameSession.Current.Hero.HasGuidingWind;
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

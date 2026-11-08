using Dino_Defenders.Books;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Stats;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Defenders.Server
{
    /// <summary>One use of a book, from when it's let go until everything it set off is over. It's what its pages have in common, however many places they've got to.</summary>
    public sealed class Cast
    {
        public Actor Source = null!;
        public BookProfile Book = null!;
        public Vector2 Cursor;
        public float Strength = 1f;
        public int ViewLag;

        /// <summary>Whether it's a Toggle Book going off by itself, which nobody's hands show.</summary>
        public bool Pulsing;

        /// <summary>Whether it goes off over and over by itself, and so shows itself without the noise a spell makes.</summary>
        public bool Quiet;

        /// <summary>How much it's made so far, which has a limit.</summary>
        public int Made;

        /// <summary>Whether it's started over from its first page once already, for those whose books run on.</summary>
        public bool RanOn;

        /// <summary>Everyone it's reached so far, whom nothing goes looking for again.</summary>
        public readonly List<Actor> Struck = new();
    }

    /// <summary>Something in flight. Who threw it and with what numbers go with it, so it still counts if they've died or moved on.</summary>
    public sealed class Projectile
    {
        public Cast Cast = null!;
        public PageProfile Page = null!;
        public float Life;
        public int Pierce;
        public int Chains;
        public float Strength = 1f;
        public Vector2 Start;
        public Vector2 Way;
        public bool Returning;
        public bool Forked;         // It, or what it came from, has split already.
        public bool Falling;        // Only there to be seen: it does what it does when it said it would.
        public readonly List<Actor> Spent = new();
    }

    public struct ProjectileComponent : IComponent
    {
        public Projectile State;
    }

    /// <summary>A patch of ground that's doing something to whoever stands on it.</summary>
    public sealed class ZoneState
    {
        public Cast Cast = null!;
        public PageProfile Page = null!;
        public GroundShape Shape;
        public float Left;
        public float NextPulse;
        public float Strength = 1f;
    }

    public struct ZoneStateComponent : IComponent
    {
        public ZoneState State;
    }

    /// <summary>
    /// Keeps everyone in a fight up to date: where they are for the grid, what's true of them, what's wearing off, and what they've lost. It goes through
    /// component arrays directly, as it's the one thing that visits every actor every tick, and looks up nothing it doesn't need to.
    /// </summary>
    public sealed class ActorSystem : SystemBase
    {
        private const float LandInterval = 0.25f;

        private static readonly BitMask Mask = new BitMask(typeof(ActorComponent), typeof(PositionComponent), typeof(HealthComponent));

        private readonly GameServer _server;
        private readonly List<Actor> _near = new();
        private float _nextLand;

        public ActorSystem(GameServer server)
            : base(Mask)
        {
            _server = server;
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime) { }

        protected override void UpdateFrame(ECSWorld world, float deltaTime)
        {
            ServerSession? session = _server.Session;
            if (session == null) return;

            Combat combat = session.Combat;
            if (session.Paused) return;

            _nextLand -= deltaTime;
            bool readLand = _nextLand <= 0f;
            if (readLand)
            {
                _nextLand = LandInterval;
                combat.Land.Forget((float)session.Time);
            }

            combat.Grid.Begin();
            foreach (Archetype archetype in world.QueryArchetypes(Mask, BitMask.Empty))
            {
                List<ActorComponent> actors = archetype.GetComponentArray<ActorComponent>()!;
                List<PositionComponent> positions = archetype.GetComponentArray<PositionComponent>()!;
                List<HealthComponent> healths = archetype.GetComponentArray<HealthComponent>()!;
                List<AfflictedComponent>? afflicted = archetype.GetComponentArray<AfflictedComponent>();

                for (int i = 0; i < archetype.EntityCount; i++)
                {
                    Actor actor = actors[i].Actor;
                    Vector3 position = positions[i].value;
                    actor.Height = position.Y;
                    actor.Advance(deltaTime, _server.Tick, position.Xz);

                    if (!actor.Dead)
                    {
                        combat.Grid.Add(actor);
                        combat.Linger(actor, deltaTime);
                        combat.Presence(actor, deltaTime);

                        if (actor.LoadoutChanged) session.Rebuild(actor);
                        if (actor.IsHero) actor.Counters = Counted(session, actor);
                        else if (actor.Owner != null) actor.Counters = actor.Owner.Counters;

                        // Only those who care about the land are worth asking it for. They ask a few times a second: nobody's walking far in that.
                        if (readLand && (actor.Loadout.Relevant & (Condition.Land | Condition.Weather)) != 0)
                        {
                            actor.Ground = combat.Land.At(actor.Position);
                            if ((actor.Loadout.Relevant & Condition.Weather) != 0) actor.Ground |= combat.Land.Weather(actor.Position, (float)session.Time);
                        }
                        actor.ReadState(actor.Ground);
                    }

                    // This is how everyone else sees it, the dead included: it's what tells a friend's client that a hero has fallen.
                    HealthComponent health = healths[i];
                    if (health.Current != actor.Vitals.Health || health.Max != actor.Sheet.MaxHealth)
                    {
                        healths[i] = new HealthComponent { Current = actor.Vitals.Health, Max = actor.Sheet.MaxHealth };
                    }

                    if (afflicted != null)
                    {
                        uint shown = (uint)(actor.State & Condition.Afflicted);
                        if (afflicted[i].Flags != shown) afflicted[i] = new AfflictedComponent { Flags = shown };
                    }
                }
            }

            // Who's around a hero, now that everyone's where they are. It's read next tick, which nobody can tell.
            foreach (ServerPlayer hero in session.Heroes)
            {
                Actor actor = hero.Actor;
                if (actor.Dead) continue;
                _near.Clear();
                combat.Grid.Foes(actor.Team, actor.Position, Rulebook.CrowdReach, _near);
                actor.EnemiesNear = _near.Count;
                if (actor.Sheet.Has(Rule.Lodestone)) combat.Draw(actor, deltaTime);
            }

            combat.Step();
        }

        private static Counters Counted(ServerSession session, Actor actor)
        {
            int shrines = 0;
            foreach (SiteState state in session.Sites)
            {
                if (state.Kindled) shrines++;
            }

            int allies = 0;
            foreach (ServerPlayer hero in session.Heroes)
            {
                if (hero.Actor != actor && (hero.Actor.Position - actor.Position).LengthSquared < Rulebook.AlliesReach * Rulebook.AlliesReach) allies++;
            }

            int minions = 0;
            foreach (Actor minion in session.Minions)
            {
                if (minion.Owner == actor) minions++;
            }

            HeroState own = actor.Player!.Hero;
            int empty = 0, normal = 0, uniques = 0;
            foreach (WornSlot place in Enum.GetValues<WornSlot>())
            {
                if (!own.Equipped.TryGetValue(place, out Item? worn))
                {
                    // What's held in both hands fills the other one.
                    if (place != WornSlot.OffHand || !own.Equipped.TryGetValue(WornSlot.MainHand, out Item? held) || !held.IsTwoHanded) empty++;
                    continue;
                }
                if (worn.Rarity == Rarity.Normal) normal++;
                if (worn.Rarity == Rarity.Unique) uniques++;
            }

            int carried = 0;
            foreach (Item? item in own.Inventory)
            {
                if (item != null) carried++;
            }

            Counters counters = actor.Counters;
            counters.Gold = own.Gold;
            counters.ShrinesKindled = (byte)Math.Min(shrines, 255);
            counters.AlliesNear = (byte)allies;
            counters.Minions = (byte)Math.Min(minions, 255);
            counters.EnemiesNear = (byte)Math.Min(actor.EnemiesNear, 255);
            counters.Ailments = (byte)System.Numerics.BitOperations.PopCount((ulong)(actor.State & Condition.Afflicted));
            counters.EmptySlots = (byte)empty;
            counters.NormalWorn = (byte)normal;
            counters.UniquesWorn = (byte)uniques;
            counters.Carried = (byte)Math.Min(carried, 255);
            counters.LandLevel = (byte)(session.IsHaven ? 0 : Math.Min(session.World.Plan.Level, 255));
            counters.Omens = (byte)Math.Min(session.Terms.Count, 255);
            counters.RaresSlain = (byte)Math.Min(actor.RaresSlain, 255);
            counters.FightSeconds = (byte)Math.Min((int)actor.FightFor, 255);
            counters.Streak = (byte)Math.Min(actor.Streak, 255);
            int variety = 0;
            foreach (float since in actor.SinceSlot)
            {
                if (since < Rulebook.Recently) variety++;
            }
            counters.Variety = (byte)variety;
            return counters;
        }
    }

    /// <summary>Flies everything until it finds someone, something solid, or the end of its strength.</summary>
    public class ProjectileSystem : SystemBase
    {
        private readonly GameServer _server;

        public ProjectileSystem(GameServer server)
            : base(new BitMask(typeof(ProjectileComponent), typeof(BallisticComponent), typeof(PositionComponent)))
        {
            _server = server;
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            ServerSession session = _server.Session!;
            Projectile projectile = entity.Get<ProjectileComponent>().State;
            projectile.Life -= deltaTime;

            BallisticComponent flight = entity.Get<BallisticComponent>();
            Vector3 position = flight.PositionAt(_server.Tick);
            entity.Set(new PositionComponent(position));

            bool blocked = position.Y < session.World.Height.Sample(position.Xz) || session.World.Placement.OverlapsSolid(Footprint.Circle(position.Xz, 0.3f));
            bool flying = session.Combat.Touch(projectile, position);

            // What's falling isn't stopped by anything: it comes down where it said it would.
            if (projectile.Falling) blocked = false;
            if (flying && !blocked && projectile.Life > 0f) return;

            // It ended on something other than a body, which is no impact unless a sticker says it is.
            if (flying) session.Combat.Ended(projectile, position, blocked);
            session.Combat.Returns(projectile, position);
            world.DestroyEntity(entity.Entity);
        }
    }

    /// <summary>How long a mark on the ground has left.</summary>
    public struct TelegraphLifeComponent : IComponent
    {
        public float Left;
    }

    /// <summary>
    /// Clears away marks on the ground once what they were for has happened. Everything that's coming is a tick it comes on, and ticks go on while someone alone has stopped the world to look at a panel. So while it's
    /// stopped those moments move along with them, or everything would be over by the time they came back.
    /// </summary>
    public class TelegraphSystem : SystemBase
    {
        private static readonly BitMask Marks = new BitMask(typeof(TelegraphLifeComponent), typeof(TelegraphComponent));
        private static readonly BitMask Actions = new BitMask(typeof(ActionComponent));

        private readonly GameServer _server;

        public TelegraphSystem(GameServer server)
            : base(Marks)
        {
            _server = server;
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime) { }

        protected override void UpdateFrame(ECSWorld world, float deltaTime)
        {
            bool paused = _server.Session is not { Paused: false };
            if (paused) _server.Session?.Combat.Wait();
            foreach (Archetype archetype in world.QueryArchetypes(Marks, BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    if (paused)
                    {
                        var telegraph = entity.Get<TelegraphComponent>();
                        telegraph.Start++;
                        telegraph.Lands++;
                        entity.Set(telegraph);
                        continue;
                    }

                    var life = entity.Get<TelegraphLifeComponent>();
                    life.Left -= deltaTime;
                    if (life.Left <= 0f) world.DestroyEntity(entity.Entity);
                    else entity.Set(life);
                }
            }

            if (!paused) return;
            foreach (Archetype archetype in world.QueryArchetypes(Actions, BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    var action = entity.Get<ActionComponent>();
                    if (action.Ends <= _server.Tick) continue;
                    action.Start++;
                    action.Lands++;
                    action.Ends++;
                    entity.Set(action);
                }
            }
        }
    }

    /// <summary>Works out what lasting ground does, and clears it when it's done.</summary>
    public class ZoneSystem : SystemBase
    {
        private readonly GameServer _server;

        public ZoneSystem(GameServer server)
            : base(new BitMask(typeof(ZoneStateComponent)))
        {
            _server = server;
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            ServerSession session = _server.Session!;
            if (session.Paused) return;

            ZoneState zone = entity.Get<ZoneStateComponent>().State;
            zone.Left -= deltaTime;
            zone.NextPulse -= deltaTime;
            if (zone.NextPulse <= 0f)
            {
                zone.NextPulse += zone.Page.Interval;
                session.Combat.Pulse(zone);
            }
            if (zone.Left <= 0f) world.DestroyEntity(entity.Entity);
        }
    }
}

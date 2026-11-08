using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Stats;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Networking;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using Dino_Engine.World.Generation;
using OpenTK.Mathematics;

namespace Dino_Defenders.Server
{
    /// <summary>
    /// What happens in an arena besides the fight itself: each time its tyrant loses another share of its health, the brood's handlers throw up some of the gates at the foot of the stands and let
    /// more of the brood in, Magic and Rare, as many more as the land's orders put at its places, and the last time all of them. A gate's portcullis is something everyone sees go up and come down,
    /// and whoever comes through belongs to no place: nothing about them is kept, and they drop what anything else drops (<see cref="ServerSession.Arrivals"/>). Read LANDS.md.
    /// </summary>
    public sealed class ArenaSystem : SystemBase
    {
        public const string PortcullisModel = "arena.portcullis";

        private const float Rise = 3.6f;                // Metres a portcullis goes up into the block over its gate.
        private const float RiseTime = 1.4f;            // Seconds it takes going up, and coming down.
        private const float ArrivalAggro = 70f;         // Metres: whoever is let in sees the whole floor.
        private const float ArrivalSpread = 2.2f;       // Metres between those let through one gate, side by side.
        private const float LeaderShare = 0.3f;         // Of those let in, how many are of the kinds that lead a pack.

        private readonly GameServer _server;
        private readonly List<Gate> _gates = new();
        private ServerSession? _for;
        private Vector2 _middle;
        private int _waves;

        private sealed class Gate
        {
            public Entity Portcullis;
            public Vector3 Shut;
            public Vector2 Out;             // Where whoever comes through it is first seen.
            public Vector2 Outward;         // From the middle of the arena, through it.
            public double? OpenedAt;
            public int Letting;             // How many it lets through when it's up.
            public int Wave;
        }

        public ArenaSystem(GameServer server)
            : base(new BitMask(typeof(ActorComponent)))
        {
            _server = server;
        }

        /// <summary>How many times the gates have been opened in this arena so far.</summary>
        public int Waves => _waves;

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime) { }

        protected override void UpdateFrame(ECSWorld world, float deltaTime)
        {
            ServerSession? session = _server.Session;
            if (session != _for)
            {
                _for = session;
                _gates.Clear();
                _waves = 0;
            }
            if (session == null || !session.IsArena || session.Paused) return;
            if (_gates.Count == 0 && !PutUpGates(world, session)) return;

            if (!session.Liberated && Tyrants(session) is float lost)
            {
                int due = Math.Min((int)(lost / Rulebook.ArenaWaveEvery), MostWaves);
                if (due > _waves) Open(session, ++_waves);
            }
            foreach (Gate gate in _gates) Move(world, session, gate);
        }

        /// <summary>How many times the gates open in a fight, which is once for every share of the tyrant's health but the last, as by then it's down.</summary>
        public static int MostWaves => Math.Max((int)MathF.Round(1f / Rulebook.ArenaWaveEvery) - 1, 0);

        // A portcullis at the back of each gate, shut, as the world was built with them.
        private bool PutUpGates(ECSWorld world, ServerSession session)
        {
            WorldSite? arena = session.World.Sites.FirstOrDefault(site => site.Kind == SiteKind.Arena);
            if (arena == null) return false;
            _middle = arena.Position;
            foreach (SiteSpawn spawn in arena.Spawns)
            {
                if (spawn.Role != SpawnRole.ArenaGate) continue;
                Vector2 outward = spawn.Position - arena.Position;
                if (outward.LengthSquared < 1f) continue;
                outward.Normalize();
                Vector2 at = arena.Position + outward * (ArenaBuilder.GateFront + ArenaBuilder.PortcullisBack);
                var shut = new Vector3(at.X, session.World.Height.Sample(spawn.Position), at.Y);
                Entity portcullis = world.CreateEntity("Portcullis",
                    new NetIdentityComponent(_server.Ghosts.NextId(), importance: 1f),
                    new PositionComponent(shut),
                    new RotationComponent(Quaternion.FromAxisAngle(Vector3.UnitY, MathF.Atan2(outward.X, outward.Y))),
                    new ScaleComponent(new Vector3(1f)),
                    new VisualComponent(PortcullisModel, VisualKind.Prop));
                _gates.Add(new Gate { Portcullis = portcullis, Shut = shut, Out = spawn.Position, Outward = outward });
            }
            return _gates.Count > 0;
        }

        // How much of their health the tyrants here have lost between them, or nothing if none of them is here yet.
        private static float? Tyrants(ServerSession session)
        {
            float health = 0f;
            float most = 0f;
            foreach (SiteState state in session.Sites)
            {
                if (state.Site.Kind != SiteKind.Arena) continue;
                foreach (Entity entity in state.Live)
                {
                    if (!session.Server.Ecs.TryGetEntityLocation(entity, out _, out _)) continue;
                    EntityView view = session.Server.Ecs.GetEntityView(entity);
                    if (!view.Has<ActorComponent>()) continue;
                    Actor actor = view.Get<ActorComponent>().Actor;
                    if (!actor.Tyrant) continue;
                    most += actor.Sheet.MaxHealth;
                    health += actor.Dead ? 0f : MathF.Max(actor.Vitals.Health, 0f);
                }
            }
            return most > 0f ? 1f - health / most : null;
        }

        // Some of the gates go up, spread round the bowl and a different few each time, and the last time all of them.
        private void Open(ServerSession session, int wave)
        {
            int count = wave >= MostWaves ? _gates.Count : Math.Min(Rulebook.ArenaWaveGates, _gates.Count);
            int each = Math.Max((int)MathF.Round(Rulebook.ArenaWaveSize * (1f + session.Terms.MorePeople)), 1);
            for (int i = 0; i < count; i++)
            {
                Gate gate = _gates[(wave - 1 + i * _gates.Count / count) % _gates.Count];
                if (gate.OpenedAt != null) continue;
                gate.OpenedAt = session.Time;
                gate.Letting = each;
                gate.Wave = wave;
                _server.Broadcast(new SoundEvent { Position = gate.Shut, Cue = SoundCue.ArenaGate }, gate.Shut.Xz);
            }
            Console.WriteLine($"The arena's gates open, for the {wave} time of {MostWaves}.");
        }

        // A gate rises, lets through whoever it's letting through once it's high enough to walk under, stays up a while, and comes down.
        private void Move(ECSWorld world, ServerSession session, Gate gate)
        {
            float lift = 0f;
            if (gate.OpenedAt is double opened)
            {
                float since = (float)(session.Time - opened);
                float shutting = since - RiseTime - Rulebook.ArenaGateOpen;
                lift = since < RiseTime ? since / RiseTime : shutting < 0f ? 1f : 1f - shutting / RiseTime;
                if (gate.Letting > 0 && since >= RiseTime * 0.6f)
                {
                    LetThrough(world, session, gate);
                    gate.Letting = 0;
                }
                if (shutting >= RiseTime)
                {
                    gate.OpenedAt = null;
                    lift = 0f;
                }
            }

            if (!world.TryGetEntityLocation(gate.Portcullis, out _, out _)) return;
            EntityView view = world.GetEntityView(gate.Portcullis);
            Vector3 position = gate.Shut + Vector3.UnitY * Rise * SmoothStep(Math.Clamp(lift, 0f, 1f));
            if (view.Get<PositionComponent>().value != position) view.Set(new PositionComponent(position));
        }

        private static float SmoothStep(float t) => t * t * (3f - 2f * t);

        // The tyrant's own brood, side by side in the gate's mouth, every one of them Magic or Rare, and already looking for whoever's on the floor.
        private void LetThrough(ECSWorld world, ServerSession session, Gate gate)
        {
            var random = new WorldRandom(session.World.Plan.Seed).Fork("arena").Fork(gate.Wave).Fork(_gates.IndexOf(gate));
            Recipe recipe = Warbands.ArenaRecipe(session.Chronicle.BossKind);
            int level = session.World.Plan.Level;
            Vector2 across = new Vector2(gate.Outward.Y, -gate.Outward.X);
            for (int i = 0; i < gate.Letting; i++)
            {
                WorldRandom one = random.Fork(i);
                bool leads = recipe.Leaders.Length > 0 && one.Chance(LeaderShare);
                bool behind = !leads && recipe.Support.Length > 0 && one.Chance(recipe.SupportShare);
                EnemyKind kind = one.Pick(leads ? recipe.Leaders : behind ? recipe.Support : recipe.Core);
                Rarity rarity = one.Chance(Rulebook.ArenaWaveRareChance) ? Rarity.Rare : Rarity.Magic;
                int traits = Bestiary.Fill(Bestiary.NoTraits, rarity, one.Fork("traits"));
                if (rarity == Rarity.Rare && session.Terms.Has(OrdersCatalog.Ledger)) traits = Bestiary.OneMore(traits, one.Fork("ledger"));

                Vector2 spot = gate.Out + across * ((i - (gate.Letting - 1) * 0.5f) * ArrivalSpread);
                if (!session.World.Passable.IsSet(spot)) spot = gate.Out;
                Arrive(world, session, kind, rarity, traits, level, spot, gate.Outward, one);
            }
        }

        private void Arrive(ECSWorld world, ServerSession session, EnemyKind kind, Rarity rarity, int traits, int level, Vector2 at, Vector2 outward, WorldRandom random)
        {
            EnemyPower power = Bestiary.PowerOf(rarity);
            float scale = kind.Scale * power.Scale;
            string model = Bestiary.ModelOf(kind, random);
            Loadout loadout = session.Bestiary.Shared(kind, level, rarity, traits);
            string name = Bestiary.NameOf(kind, traits);

            var enemy = new EnemyComponent { Name = name, Rarity = rarity, Level = level };
            var brain = new EnemyBrainComponent
            {
                SiteIndex = -1,
                SpawnIndex = -1,
                Home = _middle,
                AggroRadius = ArrivalAggro,
                LeashRadius = MathF.Max(kind.LeashRadius, ArrivalAggro),
                KeepsAway = kind.KeepsAway,
                Experience = Bestiary.ExperienceAt(level, power.Health * kind.Health),
                Abilities = Bestiary.AbilitiesOf(kind, traits),
                Cooldown = random.Range(0f, 1f),
            };

            float height = session.World.Height.Sample(at);
            float radius = kind.RadiusAt(scale);
            var actor = new Actor(Team.Enemies, loadout, at, radius)
            {
                Body = model,
                Height = height,
                Nature = rarity == Rarity.Rare ? Condition.Rare : Condition.None,
            };
            var position = new Vector3(at.X, height, at.Y);
            actor.Entity = world.CreateEntity(name, Bodies.Walking(_server, model, position, scale, power.Importance, radius, enemy, brain, new HealthComponent(actor.Sheet.MaxHealth), new ActionComponent(), new AfflictedComponent(), new ActorComponent(actor)));
            if (world.TryGetEntityLocation(actor.Entity, out _, out _)) world.GetEntityView(actor.Entity).Set(new RotationComponent(Quaternion.FromAxisAngle(Vector3.UnitY, MathF.Atan2(-outward.X, -outward.Y))));
            session.Arrivals.Add(actor);
        }
    }
}

using Dino_Defenders.Books;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Stats;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Networking;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Defenders.Server
{
    /// <summary>
    /// What happens in an arena besides the fight itself: each time its tyrant loses another share of its health, the brood's handlers open some of its entries and let more of the brood in, Magic and
    /// Rare, as many more as the land's orders put at its places, and the last time all of them. How an entry opens is its kind's (<see cref="ArenaEntryKind"/>): a grille rises into the stone over a
    /// gate, two leaves drop open in the floor over a lift, bronze doors swing in, or the ash heaves and the Ossuary's dead claw up out of it. Whatever moves is something everyone sees move, and whoever
    /// comes through belongs to no place: nothing about them is kept, and they drop what anything else drops (<see cref="ServerSession.Arrivals"/>). What each arena does on its own to whoever's on its
    /// floor is in <c>ArenaSystem.Hazards.cs</c>. Everything about where comes from the arena's plan (<see cref="ArenaPlan"/>). Read ARENAS.md.
    /// </summary>
    public sealed partial class ArenaSystem : SystemBase
    {
        private const float RiseTime = 1.4f;            // Seconds a grille takes going up, and coming down.
        private const float SwingTime = 1.1f;           // And a leaf, opening or shutting.
        private const float ArrivalAggro = 70f;         // Metres: whoever is let in sees the whole floor.
        private const float ArrivalSpread = 2.2f;       // Metres between those let through one entry, side by side.
        private const float LeaderShare = 0.3f;         // Of those let in, how many are of the kinds that lead a pack.
        private const float DoorReach = 3.6f;           // Metres from where a keep's arena's way down is used to anyone who can use it, standing before its gate.
        private const float ExitReach = 3.2f;

        private readonly GameServer _server;
        private readonly List<Gate> _gates = new();
        private ServerSession? _for;
        private ArenaPlan? _plan;
        private WorldSite? _site;
        private SiteState? _state;
        private bool _searched;
        private int _waves;

        // A keep's arena's way up: the grille shut in its gate, which rises once the tyrant's down, and when it did.
        private Entity _way;
        private double? _wayOpened;

        // Seconds the arena has run, which stand still while the world does, as a mark's times are moved on while it's stopped and anything here has to keep in step with them.
        private double _clock;

        private sealed class Gate
        {
            public ArenaEntry Entry;
            public Entity First;            // What moves: the grille, or a hatch's or a pair of doors' first leaf.
            public Entity Second;           // A hatch's or a pair of doors' second leaf.
            public Vector2 Outward;         // From the middle of the arena, through it.
            public double? OpenedAt;        // On the arena's own clock.
            public int Letting;             // How many it lets through when it's open.
            public int Wave;
            public int HeaveTicks;          // Ticks until the ash bursts where the dead come up, once it's begun to heave.
        }

        public ArenaSystem(GameServer server)
            : base(new BitMask(typeof(ActorComponent)))
        {
            _server = server;
        }

        /// <summary>How many times the entries have been opened in this arena so far.</summary>
        public int Waves => _waves;

        /// <summary>The arena being run, once its entries are up.</summary>
        public ArenaPlan? Plan => _plan;

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime) { }

        protected override void UpdateFrame(ECSWorld world, float deltaTime)
        {
            ServerSession? session = _server.Session;
            if (session != _for)
            {
                _for = session;
                _gates.Clear();
                _plan = null;
                _site = null;
                _state = null;
                _searched = false;
                _waves = 0;
                _clock = 0;
                _way = default;
                _wayOpened = null;
                ForgetHazards();
            }
            if (session == null || session.Paused) return;
            if (_plan == null && (_searched || !PutUp(world, session))) return;
            _clock += deltaTime;
            if (_plan!.Keep) KeepWays(world, session);

            // What's begun goes on first, so that anything started this tick waits its whole time from the next, as its mark does.
            foreach (Gate gate in _gates) Move(world, session, gate);
            CarryPieces(world, session);

            if (!session.Liberated && Tyrants(session) is float lost)
            {
                int due = Math.Min((int)(lost / Rulebook.ArenaWaveEvery), MostWaves);
                if (due > _waves) Open(session, ++_waves);
            }
            Hazards(world, session, deltaTime);
        }

        /// <summary>How many times the entries open in a fight, which is once for every share of the tyrant's health but the last, as by then it's down.</summary>
        public static int MostWaves => Math.Max((int)MathF.Round(1f / Rulebook.ArenaWaveEvery) - 1, 0);

        /// <summary>How long one of an arena's entries takes to open.</summary>
        public static float OpeningTime(ArenaEntryKind kind) => kind switch
        {
            ArenaEntryKind.Portcullis or ArenaEntryKind.PenDoor or ArenaEntryKind.Screen => RiseTime,
            ArenaEntryKind.Rising => Rulebook.HeaveWindUp,
            _ => SwingTime,
        };

        // The arena's site, its entries as the world was built with them, shut, and its set pieces.
        private bool PutUp(ECSWorld world, ServerSession session)
        {
            _searched = true;
            WorldSite? site = session.World.Sites.FirstOrDefault(s => s.Arena != null);
            if (site?.Arena is not ArenaPlan plan) return false;
            _plan = plan;
            _site = site;
            _state = session.Sites.FirstOrDefault(s => s.Site == site);
            foreach (ArenaEntry entry in plan.Entries)
            {
                Vector2 outward = entry.Out - plan.Centre;
                outward = outward.LengthSquared > 1f ? outward.Normalized() : Vector2.UnitY;
                var gate = new Gate { Entry = entry, Outward = outward };
                switch (entry.Kind)
                {
                    case ArenaEntryKind.Rising:
                        break;
                    case ArenaEntryKind.Hatch:
                    case ArenaEntryKind.Doors:
                        gate.First = Leaf(world, entry, 0);
                        gate.Second = Leaf(world, entry, 1);
                        break;
                    default:
                        gate.First = Moving(world, "Grille", entry.Model, entry.Door, Quaternion.FromAxisAngle(Vector3.UnitY, entry.Yaw));
                        break;
                }
                _gates.Add(gate);
            }
            PutUpPieces(world, session, plan);
            if (plan.Keep)
            {
                // The way down, before its gate in the wall round the arena, and the grille shut in the gate of the way up, behind whoever comes down.
                Marker(world, session, plan.Door, InteractKind.ArenaDown, "Go down into the arena", DoorReach);
                if (plan.Way is ArenaEntry way) _way = Moving(world, "Way up", way.Model, way.Door, Quaternion.FromAxisAngle(Vector3.UnitY, way.Yaw));
            }
            return true;
        }

        private Entity Marker(ECSWorld world, ServerSession session, Vector2 at, InteractKind kind, string prompt, float radius)
        {
            int index = _state == null ? 0 : session.Sites.IndexOf(_state);
            return world.CreateEntity(prompt,
                new NetIdentityComponent(_server.Ghosts.NextId(), importance: 0.5f),
                new PositionComponent(new Vector3(at.X, session.World.Height.Sample(at), at.Y)),
                new InteractableComponent { Kind = kind, SiteIndex = index, SpawnIndex = -1, Radius = radius, Prompt = prompt });
        }

        // Once a keep's tyrant is down the grille in the gate of its arena's way up rises, and the way up can be used.
        private void KeepWays(ECSWorld world, ServerSession session)
        {
            if (_state == null || _plan?.Way is not ArenaEntry way) return;
            if (_wayOpened == null && session.TyrantDown(_state))
            {
                _wayOpened = _clock;
                Marker(world, session, _plan.Exit, InteractKind.ArenaUp, "Climb out of the arena", ExitReach);
                _server.Broadcast(new SoundEvent { Position = way.Door, Cue = SoundCue.ArenaGate }, way.Door.Xz);
            }
            if (_wayOpened is double opened) Shift(world, _way, way.Door + Vector3.UnitY * way.Lift * SmoothStep(Math.Clamp((float)(_clock - opened) / RiseTime, 0f, 1f)));
        }

        private Entity Moving(ECSWorld world, string name, string model, Vector3 at, Quaternion rotation)
        {
            return world.CreateEntity(name,
                new NetIdentityComponent(_server.Ghosts.NextId(), importance: 1f),
                new PositionComponent(at),
                new RotationComponent(rotation),
                new ScaleComponent(new Vector3(1f)),
                new VisualComponent(model, VisualKind.Prop));
        }

        // A leaf of a hatch or a pair of doors: hinged on its own side of the opening, the second the first turned round, so that each turns about its hinge.
        private Entity Leaf(ECSWorld world, in ArenaEntry entry, int which)
        {
            Vector3 hinge = HingeOf(entry, which);
            return Moving(world, which == 0 ? "Leaf" : "Second leaf", entry.Model, hinge, LeafTurn(entry, which, 0f));
        }

        private static Vector3 HingeOf(in ArenaEntry entry, int which)
        {
            // The opening's own x runs across it: (cos yaw, -sin yaw) on the ground.
            var across = new Vector3(MathF.Cos(entry.Yaw), 0f, -MathF.Sin(entry.Yaw));
            return entry.Door + across * (which == 0 ? -entry.Span * 0.5f : entry.Span * 0.5f);
        }

        // Which way a leaf faces, opened so far: a hatch's leaves drop into the shaft about their hinges, and a pair of doors swing in towards the floor.
        private static Quaternion LeafTurn(in ArenaEntry entry, int which, float open)
        {
            float yaw = entry.Yaw + (which == 1 ? MathF.PI : 0f);
            float angle = MathHelper.DegreesToRadians(entry.Lift) * open;
            Quaternion facing = Quaternion.FromAxisAngle(Vector3.UnitY, yaw);
            // The leaf reaches along its own +x from its hinge: a hatch's turns down about +z, and a door's about the upright, towards the floor.
            Quaternion turn = entry.Kind == ArenaEntryKind.Hatch ? Quaternion.FromAxisAngle(Vector3.UnitZ, -angle) : Quaternion.FromAxisAngle(Vector3.UnitY, which == 0 ? -angle : angle);
            return facing * turn;
        }

        // How much of their health the tyrants of the arena's site have lost between them, or nothing if none of them is here yet.
        private float? Tyrants(ServerSession session)
        {
            float health = 0f;
            float most = 0f;
            foreach (Actor actor in TyrantsHere(session))
            {
                most += actor.Sheet.MaxHealth;
                health += actor.Dead ? 0f : MathF.Max(actor.Vitals.Health, 0f);
            }
            return most > 0f ? 1f - health / most : null;
        }

        // The tyrants of the arena's site that are here, alive or not.
        private IEnumerable<Actor> TyrantsHere(ServerSession session)
        {
            foreach (SiteState state in session.Sites)
            {
                if (state.Site != _site) continue;
                foreach (Entity entity in state.Live)
                {
                    if (!session.Server.Ecs.TryGetEntityLocation(entity, out _, out _)) continue;
                    EntityView view = session.Server.Ecs.GetEntityView(entity);
                    if (!view.Has<ActorComponent>()) continue;
                    Actor actor = view.Get<ActorComponent>().Actor;
                    if (actor.Tyrant) yield return actor;
                }
            }
        }

        // Some of the entries open, spread round the floor and a different few each time, and the last time all of them. A chandelier gives too, where there are any.
        private void Open(ServerSession session, int wave)
        {
            int count = wave >= MostWaves ? _gates.Count : Math.Min(Rulebook.ArenaWaveGates, _gates.Count);
            int each = Math.Max((int)MathF.Round(Rulebook.ArenaWaveSize * (1f + session.Terms.MorePeople)), 1);
            for (int i = 0; i < count; i++)
            {
                Gate gate = _gates[(wave - 1 + i * _gates.Count / count) % _gates.Count];
                gate.Letting += each;
                gate.Wave = wave;
                if (gate.Entry.Kind == ArenaEntryKind.Rising)
                {
                    // The ash heaves where they'll come up, and whoever's standing on it is thrown when it bursts. One already heaving lets the next ones through too.
                    Vector3 ash = new Vector3(gate.Entry.Out.X, gate.Entry.Door.Y, gate.Entry.Out.Y);
                    if (gate.HeaveTicks <= 0)
                    {
                        gate.HeaveTicks = Ticks(Rulebook.HeaveWindUp);
                        Mark(GroundShape.Circle(gate.Entry.Out, Reach(session, Shelf.Heave)), session, gate.HeaveTicks);
                    }
                    _server.Broadcast(new SoundEvent { Position = ash, Cue = SoundCue.ArenaGate }, ash.Xz);
                    continue;
                }

                // One still open from the last time stays open longer and lets the next ones through too, and one shutting opens again from where it is.
                gate.OpenedAt = _clock - OpeningTime(gate.Entry.Kind) * OpennessOf(gate);
                _server.Broadcast(new SoundEvent { Position = gate.Entry.Door, Cue = SoundCue.ArenaGate }, gate.Entry.Door.Xz);
            }
            Console.WriteLine($"The arena's entries open, for the {wave} time of {MostWaves}.");
            ChandelierGives(session);
        }

        // How far open an entry is, from shut to open.
        private float OpennessOf(Gate gate)
        {
            if (gate.OpenedAt is not double opened) return 0f;
            float time = OpeningTime(gate.Entry.Kind);
            float since = (float)(_clock - opened);
            float shutting = since - time - Rulebook.ArenaGateOpen;
            return Math.Clamp(since < time ? since / time : shutting < 0f ? 1f : 1f - shutting / time, 0f, 1f);
        }

        // An entry opens, lets through whoever it's letting through once it's open enough to walk through, stays open a while, and shuts. The ash bursts where the dead come up, on the tick its mark said.
        private void Move(ECSWorld world, ServerSession session, Gate gate)
        {
            if (gate.Entry.Kind == ArenaEntryKind.Rising)
            {
                if (gate.HeaveTicks > 0 && --gate.HeaveTicks == 0)
                {
                    Heave(session, gate.Entry.Out);
                    LetThrough(world, session, gate);
                    gate.Letting = 0;
                }
                return;
            }

            float open = OpennessOf(gate);
            float time = OpeningTime(gate.Entry.Kind);
            if (gate.OpenedAt is double opened)
            {
                float since = (float)(_clock - opened);
                if (gate.Letting > 0 && since >= time * 0.6f)
                {
                    LetThrough(world, session, gate);
                    gate.Letting = 0;
                }
                if (since - time - Rulebook.ArenaGateOpen >= time)
                {
                    gate.OpenedAt = null;
                    open = 0f;
                }
            }

            float eased = SmoothStep(Math.Clamp(open, 0f, 1f));
            switch (gate.Entry.Kind)
            {
                case ArenaEntryKind.Hatch:
                case ArenaEntryKind.Doors:
                    Turn(world, gate.First, LeafTurn(gate.Entry, 0, eased));
                    Turn(world, gate.Second, LeafTurn(gate.Entry, 1, eased));
                    break;
                default:
                    Shift(world, gate.First, gate.Entry.Door + Vector3.UnitY * gate.Entry.Lift * eased);
                    break;
            }
        }

        private static void Shift(ECSWorld world, Entity entity, Vector3 position)
        {
            if (!entity.IsValid() || !world.TryGetEntityLocation(entity, out _, out _)) return;
            EntityView view = world.GetEntityView(entity);
            if (view.Get<PositionComponent>().value != position) view.Set(new PositionComponent(position));
        }

        private static void Turn(ECSWorld world, Entity entity, Quaternion rotation)
        {
            if (!entity.IsValid() || !world.TryGetEntityLocation(entity, out _, out _)) return;
            EntityView view = world.GetEntityView(entity);
            if (view.Get<RotationComponent>().quaternion != rotation) view.Set(new RotationComponent(rotation));
        }

        private static float SmoothStep(float t) => t * t * (3f - 2f * t);

        // The tyrant's own brood, side by side in an entry's mouth or round a lift, every one of them Magic or Rare, and already looking for whoever's on the floor.
        private void LetThrough(ECSWorld world, ServerSession session, Gate gate)
        {
            var random = new WorldRandom(session.World.Plan.Seed).Fork("arena").Fork(gate.Wave).Fork(_gates.IndexOf(gate));
            Recipe recipe = Warbands.ArenaRecipe(session.Chronicle.BossKind);
            int level = session.World.Plan.Level;
            Vector2 across = new Vector2(gate.Outward.Y, -gate.Outward.X);
            bool round = gate.Entry.Kind is ArenaEntryKind.Hatch or ArenaEntryKind.Rising;
            for (int i = 0; i < gate.Letting; i++)
            {
                WorldRandom one = random.Fork(i);
                bool leads = recipe.Leaders.Length > 0 && one.Chance(LeaderShare);
                bool behind = !leads && recipe.Support.Length > 0 && one.Chance(recipe.SupportShare);
                EnemyKind kind = one.Pick(leads ? recipe.Leaders : behind ? recipe.Support : recipe.Core);
                Rarity rarity = one.Chance(Rulebook.ArenaWaveRareChance) ? Rarity.Rare : Rarity.Magic;
                int traits = Bestiary.Fill(Bestiary.NoTraits, rarity, one.Fork("traits"));
                if (rarity == Rarity.Rare && session.Terms.Has(OrdersCatalog.Ledger)) traits = Bestiary.OneMore(traits, one.Fork("ledger"));

                // Out of a gate they come side by side across its mouth; up a lift or out of the ash, round where it opened.
                Vector2 spot = round
                    ? gate.Entry.Out + new Vector2(MathF.Cos(i * 2.4f), MathF.Sin(i * 2.4f)) * (i == 0 ? 0f : ArrivalSpread * (0.8f + 0.2f * i))
                    : gate.Entry.Out + across * ((i - (gate.Letting - 1) * 0.5f) * ArrivalSpread);
                if (!session.World.Passable.IsSet(spot)) spot = gate.Entry.Out;
                Vector2 facing = round ? (_plan!.Centre - spot).LengthSquared > 1f ? (_plan.Centre - spot).Normalized() : Vector2.UnitY : -gate.Outward;
                Arrive(world, session, kind, rarity, traits, level, spot, facing, one);
            }
        }

        private void Arrive(ECSWorld world, ServerSession session, EnemyKind kind, Rarity rarity, int traits, int level, Vector2 at, Vector2 facing, WorldRandom random)
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
                Home = _plan?.Centre ?? at,
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
            if (world.TryGetEntityLocation(actor.Entity, out _, out _)) world.GetEntityView(actor.Entity).Set(new RotationComponent(Quaternion.FromAxisAngle(Vector3.UnitY, MathF.Atan2(facing.X, facing.Y))));
            session.Arrivals.Add(actor);
        }
    }
}

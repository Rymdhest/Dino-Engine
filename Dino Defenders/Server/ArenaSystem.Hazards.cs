using Dino_Defenders.Books;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Stats;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Procedural.Architecture.Arena;
using Dino_Engine.Networking;
using Dino_Engine.World;
using OpenTK.Mathematics;
using static Dino_Defenders.Stats.M;

namespace Dino_Defenders.Server
{
    /// <summary>
    /// What each arena does on its own to whoever's on its floor, which is what makes one fight unlike another (ARENAS.md). The Colosseum's bolt-throwers loose down lines across it from the commander's
    /// box. The Cage's clutches hatch into more of the brood unless they're smashed first. The Sun-Temple's mirrors gather the sun onto someone and burn whatever's there, the tyrant too. The Onslaught's
    /// vents breathe fire along their cracks, and its ash heaves and throws whoever's on it where the dead come up. The Cathedral's chandeliers give as the tyrant weakens and come down burning on whatever's
    /// under them, the tyrant too. The Pit does nothing but let in the brood: it's the plain fight.
    /// Nothing here has a rule of its own. Whatever an arena does is a book on <see cref="Shelf"/> in the hands of one of two actors who stand nowhere and can't be struck, one on the Reich's side and one
    /// that falls on the tyrant and its brood, released from where the piece is and marked like any blow for as long as it takes, which is counted in ticks that pass only while the world does, as a
    /// mark's are (<see cref="Combat.Warn"/>). What falls on the tyrant takes a share of its health (<see cref="Rulebook.ChandelierTyrantShare"/>), as anything sized to a hero would be nothing to it.
    /// It begins <see cref="Rulebook.HazardGrace"/> after a hero first sets foot on the floor, or as soon as the tyrant's hurt, and stops when it falls.
    /// </summary>
    public sealed partial class ArenaSystem
    {
        private const float TurnShare = 0.4f;           // Of a wind-up, how long a thrower or a mirror takes to come round to where it's aimed.
        private const float Gravity = 9.81f;
        private const float DrawnGround = 0.15f;        // Metres the ground is drawn above where people stand, which is what a fallen thing lies on.
        private const float FallenLean = 0.32f;         // Radians a chandelier lies over on its side once it's down.
        private const float ShakeTurn = 0.05f;          // Radians a chandelier swings as its chain gives, and a stirring clutch rocks.
        private const float ClutchReach = 0.9f;         // Metres a clutch reaches, for what strikes it.
        private const float TimerStart = 1.5f;          // Seconds into the fight before the first thing on a timer.
        private const float NoneInReach = 1f;           // Seconds before trying again, when nothing's in reach of anyone.
        private const int EmbersEvery = 8;              // Ticks between embers from a vent that's waking.

        // Every book an arena can set off, which both of its actors carry.
        private static readonly BookPlan[] HazardBooks = { Shelf.BallistaBolt, Shelf.Sunfire, Shelf.CinderTrail, Shelf.Eruption, Shelf.Smoulder, Shelf.Heave, Shelf.Crash, Shelf.Wreck };

        private sealed class Piece
        {
            public ArenaPiece Of;
            public Entity Body;             // What's seen of it, if the arena puts it up: a thrower, a mirror, a chandelier or a clutch.
            public Actor? Clutch;           // A clutch's own body, which can be struck.
            public float Yaw;               // Which way it faces now.
            public float FromYaw;           // Which way it faced when it was last aimed.
            public float ToYaw;             // And where it's coming round to.
            public int Ticks;               // Until what it's started lands, or 0 if it's started nothing.
            public int Total;               // What that takes in all.
            public Entity Mark;
            public Vector2 From;            // Where what it's started begins.
            public Vector2 Way;             // Which way it goes.
            public bool Leads;              // Of the mirrors gathering the sun onto one place, the one it lands with.
            public bool Spent;              // A chandelier comes down once, and a clutch hatches or breaks once.
            public bool Stirring;
            public float HatchAt;           // Seconds into the fight a clutch hatches.
            public Vector3 Lean;            // The way a chandelier lies over once it's down.
        }

        private readonly List<Piece> _pieces = new();
        private readonly List<ServerPlayer> _floor = new();
        private readonly List<Actor> _under = new();
        private Actor? _reich;
        private Actor? _anyone;
        private float _anyoneFor;           // The tyrant's health the second actor was made for.
        private float _onFloor = -1f;       // Seconds since a hero first set foot on the floor, or less than nothing if none has.
        private float _fight;               // Seconds the arena's been doing anything of its own.
        private float _next;                // Until it next does what it does on a timer.
        private int _turn;
        private WorldRandom _luck = new WorldRandom(1);

        private void ForgetHazards()
        {
            _pieces.Clear();
            _reich = null;
            _anyone = null;
            _anyoneFor = 0f;
            _onFloor = -1f;
            _fight = 0f;
            _next = TimerStart;
            _turn = 0;
        }

        private static int Ticks(float seconds) => Math.Max(1, (int)MathF.Round(seconds * NetTime.TicksPerSecond));

        // ---------------------------------------------------------------- Putting them up

        // What the arena moves of its pieces, as the world was built with them: its throwers and mirrors where they stand, its chandeliers on their chains and its clutches in their nests.
        private void PutUpPieces(ECSWorld world, ServerSession session, ArenaPlan plan)
        {
            _luck = new WorldRandom(session.World.Plan.Seed).Fork("arena pieces");
            int clutches = 0;
            foreach (ArenaPiece of in plan.Pieces)
            {
                var piece = new Piece { Of = of, Yaw = of.Yaw };
                switch (of.Kind)
                {
                    case ArenaPieceKind.Ballista:
                    case ArenaPieceKind.Mirror:
                        piece.Body = Moving(world, of.Kind.ToString(), of.Model, of.At, Facing(of.Yaw));
                        break;
                    case ArenaPieceKind.Chandelier:
                        piece.Body = Moving(world, "Chandelier", of.Model, of.At, Quaternion.Identity);
                        break;
                    case ArenaPieceKind.Clutch:
                        PutUpClutch(world, session, piece);
                        piece.HatchAt = Rulebook.ClutchHatchFirst + clutches++ * Rulebook.ClutchHatchApart;
                        break;
                }
                _pieces.Add(piece);
            }
        }

        private static Quaternion Facing(float yaw) => Quaternion.FromAxisAngle(Vector3.UnitY, yaw);

        // A clutch is something of the Reich's that can be struck and broken, standing still in its nest, with a little more to it than one of the brood.
        private void PutUpClutch(ECSWorld world, ServerSession session, Piece piece)
        {
            int level = session.World.Plan.Level;
            Loadout core = session.Bestiary.Shared(CoreKind(session), level, Rarity.Normal, Bestiary.NoTraits);
            var loadout = new Loadout(core.Level, core.Weapon, core.Mods.Append(More(Stat.Health, Rulebook.ClutchHealth)), Array.Empty<BookPlan>());
            Vector2 at = piece.Of.At.Xz;
            var actor = new Actor(Team.Enemies, loadout, at, ClutchReach) { Still = true, Height = piece.Of.At.Y, Body = piece.Of.Model };
            actor.Entity = world.CreateEntity("Clutch",
                new NetIdentityComponent(_server.Ghosts.NextId(), importance: 1.5f),
                new PositionComponent(piece.Of.At),
                new RotationComponent(Facing(piece.Of.Yaw)),
                new ScaleComponent(new Vector3(1f)),
                new VisualComponent(piece.Of.Model, VisualKind.Prop),
                new VelocityComponent(),
                new EnemyComponent { Name = "Clutch", Rarity = Rarity.Normal, Level = level },
                new EnemyBrainComponent { SiteIndex = -1, SpawnIndex = -1, Home = at },
                new HealthComponent(actor.Sheet.MaxHealth),
                new AfflictedComponent(),
                new ActorComponent(actor));
            piece.Body = actor.Entity;
            piece.Clutch = actor;
        }

        // The kind most of the tyrant's brood is, which is what the arena's own hands are sized by.
        private static EnemyKind CoreKind(ServerSession session)
        {
            Recipe recipe = Warbands.ArenaRecipe(session.Chronicle.BossKind);
            return recipe.Core.Length > 0 ? recipe.Core[0] : Bestiary.Find(session.Chronicle.BossKind) ?? Bestiary.Everyone[0];
        }

        // The arena's hand on the Reich's side, against whoever's on its floor: as strong as one of the brood, with every book the arena can set off.
        private Actor Reich(ServerSession session)
        {
            if (_reich != null) return _reich;
            Loadout core = session.Bestiary.Shared(CoreKind(session), session.World.Plan.Level, Rarity.Normal, Bestiary.NoTraits);
            _reich = new Actor(Team.Enemies, new Loadout(core.Level, core.Weapon, core.Mods, HazardBooks), _plan?.Centre ?? Vector2.Zero, 0.5f) { Still = true };
            return _reich;
        }

        // What falls on the tyrant and its brood: Spell Damage of a hundredth of the tyrant's health and nothing else, so that what a Spell Page of the arena's takes off is a share of that before
        // resistance, whatever the tyrant's level and the land's orders. Only what burns falls on them, and a Spell can't be evaded. It's never a Critical Hit and leaves nothing on them, as a burn from a
        // blow that size would be worth several times the blow.
        private Actor? Anyone(ServerSession session)
        {
            Actor? tyrant = TyrantsHere(session).FirstOrDefault(actor => !actor.Dead);
            if (tyrant == null) return null;
            float most = tyrant.Sheet.MaxHealth;
            if (_anyone != null && _anyoneFor == most) return _anyone;
            var mods = new[] { Added(DamageType.Physical, most / 100f, Tags.Spell), More(Stat.AilmentChance, -100f), More(Stat.CritChance, -100f) };
            _anyone = new Actor(Team.Heroes, new Loadout(session.World.Plan.Level, WeaponProfile.Fists, mods, HazardBooks), _plan?.Centre ?? Vector2.Zero, 0.5f) { Still = true };
            _anyoneFor = most;
            return _anyone;
        }

        private static BookProfile Profile(Actor actor, BookPlan book) => actor.Sheet.Profile(actor.Loadout.IndexOf(book));

        /// <summary>How far one of the arena's books reaches, which is what its mark has to show.</summary>
        private float Reach(ServerSession session, BookPlan book) => Profile(Reich(session), book).First.Length;

        // The ground one of the arena's books will cover, from somewhere, going some way: exactly what it's released with, as that's what its mark has to be.
        private GroundShape ShapeOf(ServerSession session, BookPlan book, Vector2 from, Vector2 way) => GroundShape.Of(Profile(Reich(session), book).First, from, way);

        // Says to everyone that something's coming, for as long as there is until it lands. Everything an arena does is worth a hero's attention, whatever they've chosen to see of the least of enemies.
        private Entity Mark(in GroundShape shape, ServerSession session, int ticks) => session.Combat.Warn(shape, MarkTier.Grave, hostile: true, ticks * NetTime.TickSeconds, from: Rarity.Unique);

        // Lets one of the arena's books go, from the Reich's side, where its mark said.
        private void Release(ServerSession session, BookPlan book, Vector2 from, Vector2 way)
        {
            Actor reich = Reich(session);
            reich.Position = from;
            reich.Height = session.World.Height.Sample(from);
            BookProfile profile = Profile(reich, book);
            session.Combat.Release(reich, profile, from, way, profile.First.Length);
        }

        // What it falls on of the tyrant's side, the tyrant itself among them, takes a share of the tyrant's health, through the same shape the heroes were found in.
        private void Befall(ServerSession session, BookPlan book, Vector2 at, Vector2 way, float share)
        {
            Actor? anyone = Anyone(session);
            if (anyone == null) return;
            anyone.Position = at;
            anyone.Height = session.World.Height.Sample(at);
            PageProfile page = Profile(anyone, book).First;
            _under.Clear();
            session.Combat.Within(GroundShape.Of(page, at, way), Team.Heroes, _under);
            float strength = share / MathF.Max(page.Potency, 0.01f);
            foreach (Actor foe in _under) session.Combat.Hit(anyone, page, foe, strength, at, foe.Position - at);
        }

        // ---------------------------------------------------------------- Running them

        // Whatever's begun goes on to land, and clutches that are struck down break, whether the fight's begun or not, and once the tyrant's down only that.
        private void CarryPieces(ECSWorld world, ServerSession session)
        {
            if (session.Liberated) Settle(world, session);
            foreach (Piece piece in _pieces) Carry(world, session, piece);
        }

        // Who's on the floor, and once the fight's begun, the clutches' time and whatever the arena does on a timer.
        private void Hazards(ECSWorld world, ServerSession session, float deltaTime)
        {
            if (_plan == null || session.Liberated) return;

            _floor.Clear();
            foreach (ServerPlayer hero in _server.Heroes)
            {
                if (!hero.Actor.Dead && _plan.Contains(hero.Sim.Position)) _floor.Add(hero);
            }
            if (_onFloor < 0f && _floor.Count > 0) _onFloor = 0f;
            if (_onFloor >= 0f) _onFloor += deltaTime;

            bool begun = _onFloor >= Rulebook.HazardGrace || Tyrants(session) is > 0f;
            if (!begun) return;
            _fight += deltaTime;
            Clutches(world, session);

            if (_floor.Count == 0 || (_next -= deltaTime) > 0f) return;
            switch (_plan.Kind)
            {
                case ArenaKind.Colosseum:
                    Loose(session);
                    break;
                case ArenaKind.SunTemple:
                    Gather(session);
                    break;
                case ArenaKind.Onslaught:
                    Wake(session);
                    break;
                default:
                    _next = float.MaxValue;
                    break;
            }
        }

        // Once the tyrant's down nothing more comes: whatever was on its way is withdrawn, and what was falling lies where it would have.
        private void Settle(ECSWorld world, ServerSession session)
        {
            foreach (Piece piece in _pieces)
            {
                if (piece.Ticks <= 0) continue;
                session.Combat.Withdraw(piece.Mark);
                piece.Mark = default;
                piece.Ticks = 0;
                if (piece.Of.Kind == ArenaPieceKind.Chandelier) LayDown(world, session, piece);
            }
        }

        // One tick of something that's begun: coming round to its aim, shaking and falling, or waking, until the tick its mark lands on.
        private void Carry(ECSWorld world, ServerSession session, Piece piece)
        {
            if (piece.Of.Kind == ArenaPieceKind.Clutch)
            {
                if (!piece.Spent && piece.Clutch is { Dead: true }) Break(world, session, piece, hatched: false);
                return;
            }
            if (piece.Ticks <= 0) return;

            float done = 1f - (piece.Ticks - 1) / (float)piece.Total;
            switch (piece.Of.Kind)
            {
                case ArenaPieceKind.Ballista:
                case ArenaPieceKind.Mirror:
                    piece.Yaw = TurnTowards(piece.FromYaw, piece.ToYaw, SmoothStep(Math.Clamp(done / TurnShare, 0f, 1f)));
                    Turn(world, piece.Body, Facing(piece.Yaw));
                    break;
                case ArenaPieceKind.Chandelier:
                    Fall(world, session, piece);
                    break;
                case ArenaPieceKind.Vent:
                    if (piece.Ticks % EmbersEvery == 0)
                    {
                        Vector3 at = new Vector3(piece.Of.At.X, piece.Of.At.Y + 0.4f, piece.Of.At.Z);
                        _server.Broadcast(new SparkEvent { Position = at, Kind = SparkKind.Blade, Count = 10, Speed = 5f }, at.Xz);
                    }
                    break;
            }

            if (--piece.Ticks > 0) return;
            piece.Mark = default;
            switch (piece.Of.Kind)
            {
                case ArenaPieceKind.Ballista:
                    Loosed(session, piece);
                    break;
                case ArenaPieceKind.Mirror:
                    if (piece.Leads) Burned(session, piece);
                    break;
                case ArenaPieceKind.Vent:
                    Erupted(session, piece);
                    break;
                case ArenaPieceKind.Chandelier:
                    Crashed(world, session, piece);
                    break;
            }
        }

        // The way from one bearing to another the short way round, some of the way along.
        private static float TurnTowards(float from, float to, float share)
        {
            float turn = MathF.IEEERemainder(to - from, MathF.Tau);
            return from + turn * share;
        }

        // Someone on the floor, any of them.
        private ServerPlayer Someone() => _floor[_luck.Int(_floor.Count)];

        // ---------------------------------------------------------------- The Colosseum's bolt-throwers

        // The next thrower in turn is aimed at someone on the floor, and its line runs from where it meets the floor across it.
        private void Loose(ServerSession session)
        {
            List<Piece> throwers = _pieces.Where(p => p.Of.Kind == ArenaPieceKind.Ballista).ToList();
            Piece? thrower = throwers.Count == 0 ? null : throwers[_turn++ % throwers.Count];
            if (thrower == null || thrower.Ticks > 0)
            {
                _next = NoneInReach;
                return;
            }
            Vector2 at = thrower.Of.At.Xz;
            Vector2 toward = Someone().Sim.Position - at;
            Vector2 way = toward.LengthSquared > 0.01f ? toward.Normalized() : -Vector2.UnitY;
            Vector2 from = at;
            for (float d = 0f; d < toward.Length + 1f; d += 0.5f)
            {
                if (!_plan!.Contains(at + way * d)) continue;
                from = at + way * MathF.Max(d - 0.5f, 0f);
                break;
            }
            thrower.From = from;
            thrower.Way = way;
            thrower.FromYaw = thrower.Yaw;
            thrower.ToYaw = MathF.Atan2(way.X, way.Y);
            thrower.Ticks = thrower.Total = Ticks(Rulebook.BallistaWindUp);
            thrower.Mark = Mark(ShapeOf(session, Shelf.BallistaBolt, from, way), session, thrower.Ticks);
            _next = Rulebook.BallistaEvery;
        }

        // The bolt goes down its line, and a streak of it from the thrower's head to where it leaves the floor.
        private void Loosed(ServerSession session, Piece thrower)
        {
            Release(session, Shelf.BallistaBolt, thrower.From, thrower.Way);
            Vector3 muzzle = thrower.Of.At + Vector3.Transform(ArenaForge.BallistaMuzzle, Facing(thrower.Yaw));
            Vector2 end = thrower.From;
            float reach = Reach(session, Shelf.BallistaBolt);
            for (float d = 1f; d <= reach; d += 0.5f)
            {
                end = thrower.From + thrower.Way * d;
                if (d > 2f && !_plan!.Contains(end)) break;
            }
            var to = new Vector3(end.X, session.World.Height.Sample(end) + 1f, end.Y);
            _server.Broadcast(new LinkEvent { From = muzzle, To = to, Look = DamageType.Physical, Wide = true }, thrower.From);
        }

        // ---------------------------------------------------------------- The Sun-Temple's mirrors

        // The two mirrors nearest someone on the floor come round onto where they stand, and the sun they gather lands there.
        private void Gather(ServerSession session)
        {
            Vector2 aim = Someone().Sim.Position;
            List<Piece> mirrors = _pieces.Where(p => p.Of.Kind == ArenaPieceKind.Mirror && p.Ticks <= 0).OrderBy(p => (p.Of.At.Xz - aim).LengthSquared).Take(2).ToList();
            if (mirrors.Count == 0)
            {
                _next = NoneInReach;
                return;
            }
            int ticks = Ticks(Rulebook.SunfireWindUp);
            foreach (Piece mirror in mirrors)
            {
                Vector2 toward = aim - mirror.Of.At.Xz;
                mirror.From = aim;
                mirror.Leads = mirror == mirrors[0];
                mirror.FromYaw = mirror.Yaw;
                mirror.ToYaw = MathF.Atan2(toward.X, toward.Y);
                mirror.Ticks = mirror.Total = ticks;
            }
            mirrors[0].Mark = Mark(ShapeOf(session, Shelf.Sunfire, aim, Vector2.UnitY), session, ticks);
            _next = Rulebook.SunfireEvery;
        }

        // The light lands: it burns whoever's in it and the ground it falls on, and takes its share of anything of the tyrant's caught there, and is seen coming off each mirror that gathered it.
        private void Burned(ServerSession session, Piece leader)
        {
            Vector2 aim = leader.From;
            Release(session, Shelf.Sunfire, aim, Vector2.UnitY);
            Release(session, Shelf.CinderTrail, aim, Vector2.UnitY);
            Befall(session, Shelf.Sunfire, aim, Vector2.UnitY, Rulebook.SunfireTyrantShare);
            var to = new Vector3(aim.X, session.World.Height.Sample(aim) + 0.3f, aim.Y);
            foreach (Piece mirror in _pieces)
            {
                if (mirror.Of.Kind != ArenaPieceKind.Mirror || mirror.From != aim) continue;
                _server.Broadcast(new LinkEvent { From = mirror.Of.At, To = to, Look = DamageType.Fire, Wide = true }, aim);
            }
        }

        // ---------------------------------------------------------------- The Onslaught's vents

        // The vent nearest someone on the floor wakes, if it's near enough to them, and breathes fire along its crack.
        private void Wake(ServerSession session)
        {
            Vector2 near = Someone().Sim.Position;
            Piece? vent = _pieces.Where(p => p.Of.Kind == ArenaPieceKind.Vent && p.Ticks <= 0).OrderBy(p => (p.Of.At.Xz - near).LengthSquared).FirstOrDefault();
            if (vent == null || (vent.Of.At.Xz - near).Length > Rulebook.VentReach)
            {
                _next = NoneInReach;
                return;
            }
            // Its crack runs along its own x: (cos yaw, -sin yaw) on the ground. The fire runs the length of the book along it, centred on it.
            Vector2 way = new Vector2(MathF.Cos(vent.Of.Yaw), -MathF.Sin(vent.Of.Yaw));
            vent.Way = way;
            vent.From = vent.Of.At.Xz - way * (Reach(session, Shelf.Eruption) * 0.5f);
            vent.Ticks = vent.Total = Ticks(Rulebook.VentWindUp);
            vent.Mark = Mark(ShapeOf(session, Shelf.Eruption, vent.From, way), session, vent.Ticks);
            _next = Rulebook.VentEvery;
        }

        private void Erupted(ServerSession session, Piece vent)
        {
            Release(session, Shelf.Eruption, vent.From, vent.Way);
            Release(session, Shelf.Smoulder, vent.From, vent.Way);
        }

        // The ash bursts where the dead come up, and throws whoever's on it.
        private void Heave(ServerSession session, Vector2 at)
        {
            Release(session, Shelf.Heave, at, Vector2.UnitY);
            var ground = new Vector3(at.X, session.World.Height.Sample(at) + 0.5f, at.Y);
            _server.Broadcast(new SparkEvent { Position = ground, Kind = SparkKind.Blade, Count = 30, Speed = 9f }, at);
        }

        // ---------------------------------------------------------------- The Cathedral's chandeliers

        // A chandelier gives, the one nearest anyone on the floor of those still hanging, unless one's already coming down.
        private void ChandelierGives(ServerSession session)
        {
            if (session.Liberated || _pieces.Any(p => p.Of.Kind == ArenaPieceKind.Chandelier && p.Ticks > 0)) return;
            Vector2 near = _floor.Count > 0 ? Someone().Sim.Position : _plan!.Centre;
            Piece? chandelier = _pieces.Where(p => p.Of.Kind == ArenaPieceKind.Chandelier && !p.Spent).OrderBy(p => (p.Of.At.Xz - near).LengthSquared).FirstOrDefault();
            if (chandelier == null) return;
            chandelier.From = chandelier.Of.At.Xz;
            chandelier.Ticks = chandelier.Total = Ticks(Rulebook.ChandelierWindUp);
            float lean = _luck.Float() * MathF.Tau;
            chandelier.Lean = new Vector3(MathF.Cos(lean), 0f, MathF.Sin(lean));
            chandelier.Mark = Mark(ShapeOf(session, Shelf.Crash, chandelier.From, Vector2.UnitY), session, chandelier.Ticks);
            var at = chandelier.Of.At;
            _server.Broadcast(new SoundEvent { Position = at, Cue = SoundCue.ArenaGate }, at.Xz);
        }

        // Where a chandelier's hook is once it's down, its wheel on the floor.
        private float Down(ServerSession session, Piece chandelier) => session.World.Height.Sample(chandelier.Of.At.Xz) + DrawnGround + ArenaForge.ChandelierDrop * MathF.Cos(FallenLean);

        // It swings as its chain gives, then falls the last of its time, onto the floor on the tick its mark lands.
        private void Fall(ECSWorld world, ServerSession session, Piece chandelier)
        {
            float top = chandelier.Of.At.Y, bottom = Down(session, chandelier);
            float fallTime = MathF.Sqrt(2f * MathF.Max(top - bottom, 0f) / Gravity);
            float left = (chandelier.Ticks - 1) * NetTime.TickSeconds;
            if (left > fallTime)
            {
                float swing = MathF.Sin(left * 19f) * ShakeTurn;
                Turn(world, chandelier.Body, Quaternion.FromAxisAngle(Vector3.UnitX, swing) * Quaternion.FromAxisAngle(Vector3.UnitZ, swing * 0.6f));
                return;
            }
            float drop = 0.5f * Gravity * (fallTime - left) * (fallTime - left);
            Shift(world, chandelier.Body, new Vector3(chandelier.Of.At.X, MathF.Max(top - drop, bottom), chandelier.Of.At.Z));
        }

        private void Crashed(ECSWorld world, ServerSession session, Piece chandelier)
        {
            LayDown(world, session, chandelier);
            Vector2 at = chandelier.From;
            Release(session, Shelf.Crash, at, Vector2.UnitY);
            Release(session, Shelf.Wreck, at, Vector2.UnitY);
            Befall(session, Shelf.Crash, at, Vector2.UnitY, Rulebook.ChandelierTyrantShare);
            var ground = new Vector3(at.X, session.World.Height.Sample(at) + 0.6f, at.Y);
            _server.Broadcast(new SparkEvent { Position = ground, Kind = SparkKind.Blade, Count = 40, Speed = 11f }, at);
        }

        // What's left of it lies on the floor, over on its side, for the rest of the fight.
        private void LayDown(ECSWorld world, ServerSession session, Piece chandelier)
        {
            chandelier.Spent = true;
            Shift(world, chandelier.Body, new Vector3(chandelier.Of.At.X, Down(session, chandelier), chandelier.Of.At.Z));
            Vector3 axis = Vector3.Cross(Vector3.UnitY, chandelier.Lean);
            Turn(world, chandelier.Body, Quaternion.FromAxisAngle(axis.LengthSquared > 1e-4f ? axis.Normalized() : Vector3.UnitX, FallenLean));
        }

        // ---------------------------------------------------------------- The Cage's clutches

        // Each clutch stirs a while before its time, and hatches into the brood when it comes, unless it's been smashed first.
        private void Clutches(ECSWorld world, ServerSession session)
        {
            foreach (Piece piece in _pieces)
            {
                if (piece.Of.Kind != ArenaPieceKind.Clutch || piece.Spent || piece.Clutch is not Actor clutch || clutch.Dead) continue;
                if (_fight >= piece.HatchAt)
                {
                    Break(world, session, piece, hatched: true);
                    continue;
                }
                if (_fight < piece.HatchAt - Rulebook.ClutchStirs) continue;
                if (!piece.Stirring)
                {
                    piece.Stirring = true;
                    _server.Broadcast(TextEvent.Word(piece.Of.At + Vector3.UnitY * 2.4f, "Stirring"), clutch.Position);
                }
                float rock = MathF.Sin(_fight * 11f) * ShakeTurn * Math.Clamp(1f - (piece.HatchAt - _fight) / Rulebook.ClutchStirs, 0.3f, 1f);
                Turn(world, piece.Body, Facing(piece.Of.Yaw) * Quaternion.FromAxisAngle(Vector3.UnitX, rock));
            }
        }

        // The eggs go and their shells are left, and what was in them comes out if they hatched.
        private void Break(ECSWorld world, ServerSession session, Piece piece, bool hatched)
        {
            piece.Spent = true;
            if (piece.Clutch is Actor clutch)
            {
                clutch.Dead = true;
                if (clutch.Entity.IsValid() && world.TryGetEntityLocation(clutch.Entity, out _, out _)) world.DestroyEntity(clutch.Entity);
            }
            if (ArenaSpec.TryParse(piece.Of.Model, out ArenaSpec eggs))
            {
                Moving(world, "Shells", new ArenaSpec(ArenaShape.Shells, eggs.Seed, eggs.Int(0)).Id, piece.Of.At, Facing(piece.Of.Yaw));
            }
            var burst = piece.Of.At + Vector3.UnitY * 0.5f;
            _server.Broadcast(new SparkEvent { Position = burst, Kind = SparkKind.Blood, Count = hatched ? 24 : 16, Speed = hatched ? 4f : 6f }, burst.Xz);
            if (!hatched) return;

            var random = new WorldRandom(session.World.Plan.Seed).Fork("hatched").Fork(_pieces.IndexOf(piece));
            EnemyKind kind = CoreKind(session);
            Vector2 at = piece.Of.At.Xz;
            for (int i = 0; i < Rulebook.ClutchHatchlings; i++)
            {
                float angle = i * MathF.Tau / Rulebook.ClutchHatchlings + random.Range(-0.4f, 0.4f);
                Vector2 spot = at + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 2.2f;
                if (!session.World.Passable.IsSet(spot) && !GameServer.TryFindGround(session.World, spot, out spot)) spot = at;
                Vector2 facing = (spot - at).LengthSquared > 0.01f ? (spot - at).Normalized() : Vector2.UnitY;
                Arrive(world, session, kind, Rarity.Normal, Bestiary.NoTraits, session.World.Plan.Level, spot, facing, random.Fork(i));
            }
        }
    }
}

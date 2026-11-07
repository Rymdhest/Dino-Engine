using Dino_Defenders.Books;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Stats;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Networking;
using Dino_Engine.World;
using OpenTK.Mathematics;
using static Dino_Defenders.Stats.Rulebook;

namespace Dino_Defenders.Server
{
    /// <summary>
    /// The rules of a fight. Everything in here works on <see cref="Actor"/>s and has no idea whether one is a hero, an enemy or somebody's skeleton: a
    /// book is used the same way, a blow lands the same way, and anything that can happen to one can happen to any of them.
    ///
    /// What a blow sets off is never done inside the blow. It goes on a list and happens on the next pass. An explosion that kills something that explodes
    /// would otherwise go as deep as the crowd is large, and be working through a list that was being changed under it.
    /// </summary>
    public sealed class Combat
    {
        private const int MostRewind = 15;      // A quarter of a second: past this, what a player saw is too old to hold against anyone else.
        private const int MostPerStep = 256;    // Consequences dealt with in a tick. What's over waits for the next one.

        // What a blow does to a body. Everything's a share of how much health it took, as that's what a hit means to whoever took it.
        private const float PushPerHealth = 34f;    // Metres a second, for a blow that takes all there is.
        private const float MostPush = 11f;
        private const float BossGive = 0.12f;
        private const float EliteGive = 0.5f;
        private const float ReelFrom = 0.05f;       // Seconds a blow stops someone for, at least.
        private const float ReelPerHealth = 0.5f;
        private const float MostReel = 0.3f;
        private const float LeastThrow = 2.5f;      // Metres a second, for what only just died of it.
        private const float MostThrow = 30f;        // On top, for a blow worth many of them.
        private const float ThrowRate = 1.15f;      // How quickly that comes, with how far past dead it went.
        private const float ThrowLift = 0.56f;
        private const float CriticalForce = 1.25f;
        private const float BlastForce = 1.15f;

        private enum Later : byte
        {
            Release,
            Turn,
            Fall,
            Blast,
            Conduct,
            Raise,
            Spread,
            Burst,
            Rally,
        }

        private struct Consequence
        {
            public Later Kind;
            public double At;
            public Actor Source;
            public BookProfile? Book;
            public Cast? Cast;
            public int Page;
            public Vector2 Origin;
            public Vector2 Facing;
            public float Reach;
            public float Strength;
            public DamageType Type;
            public float Amount;
            public float Radius;
            public Tags Tags;
            public Actor? Victim;
        }

        private readonly ServerSession _session;
        private readonly WorldRandom _luck = new WorldRandom(Environment.TickCount);
        private readonly List<Actor> _found = new();
        private readonly List<Actor> _chained = new();
        private readonly List<Actor> _near = new();
        private readonly List<Actor> _friends = new();
        private readonly List<Consequence> _later = new();
        private readonly LootLuck _lootLuck = new();

        public readonly ActorGrid Grid;
        public readonly LandReader Land;

        public Combat(ServerSession session)
        {
            _session = session;
            Grid = new ActorGrid(session.World.Size);
            Land = new LandReader(session.World, session.Offering.Has(Omens.Night));
        }

        private GameServer Server => _session.Server;
        private ECSWorld World => Server.Ecs;
        private double Now => _session.Time;

        /// <summary>A book going off, from wherever and in whatever direction. <paramref name="reach"/> is how far off it was aimed.</summary>
        /// <summary>A Toggle Book's pulse: every page at once, for a share of what they'd do by hand.</summary>
        public void Pulse(Actor user, BookProfile book, Vector2 origin, Vector2 facing, float reach)
        {
            if (facing.LengthSquared < 1e-4f) facing = Vector2.UnitY;
            var cast = new Cast { Source = user, Book = book, Cursor = origin + facing * MathF.Min(reach, book.First.Range), Strength = Rulebook.AuraStrength, Pulsing = true };
            for (int page = 0; page < book.Pages.Length; page++) Open(cast, page, origin, facing);
        }

        /// <summary>Something has happened that a Trigger Book of someone's might be waiting for. It only counts here: what's ready goes off between blows, from <see cref="GameServer"/>.</summary>
        public void Watch(Actor actor, BookWhen when, float amount)
        {
            foreach (BookWatch watch in actor.Watches)
            {
                if (watch.When == when) watch.Progress = MathF.Min(watch.Progress + amount, watch.Amount);
            }
        }

        public void Release(Actor user, BookProfile book, Vector2 origin, Vector2 facing, float reach, int viewLag = 0, float strength = 1f)
        {
            user.SinceUse = 0f;
            if (facing.LengthSquared < 1e-4f) facing = Vector2.UnitY;

            var cast = new Cast { Source = user, Book = book, Cursor = origin + facing * MathF.Min(reach, book.First.Range), Strength = strength, ViewLag = viewLag };
            Open(cast, 0, origin, facing);

            if ((book.First.Tags & Tags.Spell) == 0 || (book.First.Tags & Tags.Triggered) != 0) return;

            if (user.Sheet.Has(Rule.SpellEcho) && strength >= 1f)
            {
                Defer(new Consequence { Kind = Later.Release, At = Now + EchoDelay, Source = user, Book = book, Origin = origin, Facing = facing, Reach = reach, Strength = EchoStrength });
            }

            if (user.Sheet.Has(Rule.WaterTrail) && user.Sheet.Profile(Shelf.Puddle) is BookProfile puddle) Release(user, puddle, cast.Cursor, facing, 0f);
        }

        private void Defer(in Consequence consequence)
        {
            _later.Add(consequence);
        }

        /// <summary>Everything that was waiting for now.</summary>
        public void Step()
        {
            int done = 0;
            for (int i = 0; i < _later.Count && done < MostPerStep; i++)
            {
                Consequence consequence = _later[i];
                if (consequence.At > Now + NetTime.TickSecondsPrecise * 0.5) continue;
                _later.RemoveAt(i--);
                done++;
                Carry(consequence);
            }
        }

        /// <summary>The world has stood still for a tick, though the clock hasn't. What's waiting goes on waiting for just as long.</summary>
        public void Wait()
        {
            for (int i = 0; i < _later.Count; i++)
            {
                Consequence consequence = _later[i];
                consequence.At += NetTime.TickSecondsPrecise;
                _later[i] = consequence;
            }
        }

        private void Carry(in Consequence c)
        {
            switch (c.Kind)
            {
                case Later.Release:
                    if (c.Book != null) Release(c.Source, c.Book, c.Origin, c.Facing, c.Reach, 0, c.Strength);
                    break;
                case Later.Turn:
                    if (c.Cast != null) Open(c.Cast, c.Page, c.Origin, c.Facing, c.Victim);
                    break;
                case Later.Fall:
                    if (c.Cast != null) Fell(c.Cast, c.Cast.Book.Pages[c.Page], c.Origin, c.Facing, c.Strength);
                    break;
                case Later.Blast:
                    Blast(c.Source, c.Origin, c.Radius, c.Type, c.Amount, c.Tags, c.Victim);
                    break;
                case Later.Conduct:
                    if (c.Victim != null) Conduct(c.Source, c.Victim, c.Amount);
                    break;
                case Later.Raise:
                    if (c.Victim != null) _session.RaiseFallen(c.Source, c.Victim);
                    break;
                case Later.Spread:
                    if (c.Victim != null) SpreadAilments(c.Source, c.Victim);
                    break;
                case Later.Burst:
                    Show(PageEvent.Round(Ground(c.Origin), PageSign.Blast(DamageType.Fire), c.Radius), c.Origin);
                    Blast(c.Source, c.Origin, c.Radius, c.Type, c.Amount, c.Tags, null);
                    break;
                case Later.Rally:
                    Rally(c.Source);
                    break;
            }
        }

        private Vector3 Ground(Vector2 at) => new Vector3(at.X, _session.World.Height.Sample(at), at.Y);

        private void Show<T>(in T value, Vector2 near) where T : struct => Server.Broadcast(value, near);

        // Heroes are in an area when they look it, so a foot on the edge of one doesn't count. See GroundShape.HeroBodyShare.
        private static float BodyOf(Actor target, bool hostile) => hostile && target.IsHero ? target.Radius * GroundShape.HeroBodyShare : target.Radius;

        /// <summary>Everyone on the other side from <paramref name="team"/> that a shape touches, which is the only way anyone is ever found to be in an area.</summary>
        private void Within(in GroundShape shape, Team team, List<Actor> found)
        {
            _near.Clear();
            Grid.Foes(team, shape.Origin, shape.Extent, _near);
            foreach (Actor foe in _near)
            {
                if (shape.Covers(foe.Position, BodyOf(foe, team == Team.Enemies))) found.Add(foe);
            }
        }

        // ---- What's coming.

        /// <summary>Marks a piece of ground for everyone, for as long as there is until it lands. Whoever's doing this can take it back with <see cref="Withdraw"/>.</summary>
        public Entity Warn(in GroundShape shape, MarkTier tier, bool hostile, float landsIn, int owner = 0)
        {
            uint ticks = Server.Tick + (uint)MathF.Round(landsIn * NetTime.TicksPerSecond);
            return World.CreateEntity("Warning",
                new NetIdentityComponent(Server.Ghosts.NextId(), importance: hostile ? 6f : 2f),
                new PositionComponent(Ground(shape.Origin)),
                new TelegraphComponent { Serial = ++_marksMade, Kind = shape.Kind, Tier = tier, Hostile = hostile, Owner = owner, Facing = shape.Facing, Length = shape.Length, Width = shape.Width, Start = Server.Tick, Lands = ticks },
                new TelegraphLifeComponent { Left = landsIn + MarkLingers });
        }

        /// <summary>It isn't happening after all.</summary>
        public void Withdraw(Entity mark)
        {
            if (!mark.IsValid() || !World.TryGetEntityLocation(mark, out _, out _)) return;

            // One that's outlived its time has left its id to whatever came next.
            if (World.GetEntityView(mark).Has<TelegraphComponent>()) World.DestroyEntity(mark);
        }

        private const float MarkLingers = 0.1f;     // Seconds a mark stays after it lands, as the word that it's gone shouldn't overtake it.

        private uint _marksMade;

        /// <summary>Says what someone is doing, for all who can see them.</summary>
        public void Perform(Entity who, PageSign sign, float landsIn, float lasts, Vector2 aim)
        {
            if (!who.IsValid() || !World.TryGetEntityLocation(who, out _, out _)) return;
            EntityView view = World.GetEntityView(who);
            if (!view.Has<ActionComponent>()) return;

            var action = view.Get<ActionComponent>();
            action.Serial++;
            action.Sign = sign.Number;
            action.Start = Server.Tick;
            action.Lands = Server.Tick + (uint)MathF.Round(landsIn * NetTime.TicksPerSecond);
            action.Ends = Server.Tick + (uint)MathF.Round(MathF.Max(lasts, landsIn) * NetTime.TicksPerSecond);
            action.Aim = MathF.Atan2(aim.X, aim.Y);
            view.Set(action);
        }

        /// <summary>Whatever they were doing, they aren't any more.</summary>
        public void Stop(Entity who)
        {
            if (!who.IsValid() || !World.TryGetEntityLocation(who, out _, out _)) return;
            EntityView view = World.GetEntityView(who);
            if (!view.Has<ActionComponent>()) return;

            var action = view.Get<ActionComponent>();
            if (action.Ends <= Server.Tick) return;
            action.Ends = Server.Tick;
            view.Set(action);
        }

        // ---- Getting there.

        // A page comes to something. Whatever's over the limit doesn't happen, which is what keeps a book that branches at every impact from going on for ever.
        private static bool Spend(Cast cast)
        {
            if (cast.Made >= MostMade) return false;
            cast.Made++;
            return true;
        }

        /// <summary>Goes to a page of a book: the first, or the next one from where the last struck. A book that's run out of pages is over.</summary>
        /// <param name="off">Who it's coming off, if it came off anyone.</param>
        private void Open(Cast cast, int index, Vector2 at, Vector2 way, Actor? off = null)
        {
            if (index >= cast.Book.Pages.Length) return;

            PageProfile page = cast.Book.Pages[index];
            if (page.Page.Kind == PageKind.Area) Spread(cast, page, at, way);
            else Fire(cast, page, at, way, off);
        }

        private void Turn(Cast cast, PageProfile page, Vector2 at, Vector2 way, Actor? off = null)
        {
            if (page.Page.Last) return;
            Defer(new Consequence { Kind = Later.Turn, At = Now, Source = cast.Source, Cast = cast, Page = page.Page.Index + 1, Origin = at, Facing = way, Victim = off });
        }

        // Somewhere within so far of a place, evenly over the circle.
        private Vector2 Scattered(Vector2 around, float radius)
        {
            if (radius <= 0f) return around;
            float angle = _luck.Float() * MathF.Tau;
            float reach = MathF.Sqrt(_luck.Float()) * radius;
            return around + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * reach;
        }

        // Who a page is after: never anyone this cast has already been to, as that's what makes a chain go on to someone new.
        private Actor? Seek(Cast cast, Vector2 around, bool nearest)
        {
            _near.Clear();
            Grid.Foes(cast.Source.Team, around, SeekReach, _near);
            for (int i = _near.Count - 1; i >= 0; i--)
            {
                if (cast.Struck.Contains(_near[i])) _near.RemoveAt(i);
            }
            if (_near.Count == 0) return null;
            if (!nearest) return _near[_luck.Int(_near.Count)];

            Actor? best = null;
            float closest = float.PositiveInfinity;
            foreach (Actor foe in _near)
            {
                float distance = (foe.Position - around).LengthSquared;
                if (distance >= closest) continue;
                closest = distance;
                best = foe;
            }
            return best;
        }

        // Where a page begins. With nobody to begin from, it's where they aimed.
        private Vector2 Where(Cast cast, PageProfile page, Vector2 at)
        {
            PagePlan plan = page.Page;
            bool steady = cast.Source.Sheet.Has(Rule.Steady);
            Vector2 from = plan.From switch
            {
                Start.Cursor => cast.Cursor,
                Start.Nearest => Seek(cast, cast.Source.Position, true)?.Position ?? cast.Cursor,
                Start.Random => Seek(cast, cast.Source.Position, steady)?.Position ?? cast.Cursor,
                _ => at,
            };
            return Scattered(from, steady ? 0f : plan.FromWithin);
        }

        // Where a projectile is going, from where it starts.
        private Vector2 Aimed(Cast cast, PageProfile page, Vector2 from, Vector2 way)
        {
            PagePlan plan = page.Page;
            bool steady = cast.Source.Sheet.Has(Rule.Steady);
            Vector2 target;
            switch (plan.To)
            {
                case Bearing.You:
                    target = cast.Source.Position;
                    break;
                case Bearing.Nearest:
                    target = Seek(cast, from, true)?.Position ?? cast.Cursor;
                    break;
                case Bearing.Random:
                    target = Seek(cast, from, steady)?.Position ?? cast.Cursor;
                    break;
                case Bearing.Anywhere when !steady:
                    float angle = _luck.Float() * MathF.Tau;
                    target = from + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * page.Range;
                    break;
                case Bearing.Onward:
                    target = from + way * page.Range;
                    break;
                default:
                    target = cast.Cursor;
                    break;
            }
            return Scattered(target, steady ? 0f : plan.ToWithin);
        }

        private void Spread(Cast cast, PageProfile page, Vector2 at, Vector2 way)
        {
            if (!Spend(cast)) return;

            GroundShape shape = GroundShape.Of(page, Where(cast, page, at), way);
            if (page.Page.Lingers)
            {
                Lay(cast, page, shape);
                return;
            }

            // A plain blow shows for itself, in the hands of whoever made it, which a pulse isn't.
            if (page.Sign.Form != PageForm.Strike || cast.Pulsing) Announce(page, shape);
            Cover(cast, page, shape, cast.Strength);
        }

        private void Announce(PageProfile page, in GroundShape shape)
        {
            Vector2 at = shape.Origin;
            Show(new PageEvent { Position = Ground(at), Sign = page.Sign.Number, Kind = shape.Kind, Facing = shape.Facing, Length = shape.Length, Width = shape.Width }, at);
            if (page.Sign.Form == PageForm.Beam) Show(new LinkEvent { From = Ground(at) + Vector3.UnitY * 3f, To = Ground(at + shape.Facing * shape.Length) + Vector3.UnitY * 3f, Look = page.Page.Look, Wide = true }, at);
        }

        // Everything a shape has in it. An enemy is where the attacker saw it, which is where it was a moment ago: a client draws others slightly in the past, and a blow that connects on
        // screen has to connect. This tick's positions aren't recorded until it ends, so with no rewind, where it is now is the answer.
        private void Cover(Cast cast, PageProfile page, in GroundShape shape, float strength, bool pulsing = false)
        {
            Actor user = cast.Source;
            if (page.Page.Hostile)
            {
                _found.Clear();
                int rewind = user.IsHero && page.Page.Index == 0 ? Math.Clamp(cast.ViewLag, 0, MostRewind) : 0;
                if (rewind == 0) Within(shape, user.Team, _found);
                else Grid.Foes(user.Team, shape.Origin, shape.Extent + 2f, _found);

                bool hostile = user.Team == Team.Enemies;
                bool line = shape.Kind == ShapeKind.Line;
                foreach (Actor foe in _found)
                {
                    Vector2 position = rewind > 0 ? foe.PositionAgo(rewind, Server.Tick) : foe.Position;
                    if (rewind > 0 && !shape.Covers(position, BodyOf(foe, hostile))) continue;
                    Strike(cast, page, foe, strength, shape.Origin, line ? shape.Facing : position - shape.Origin);
                }
            }
            Do(cast, page, shape, strength, pulsing);
        }

        // One page reaching one Enemy.
        private void Strike(Cast cast, PageProfile page, Actor foe, float strength, Vector2 from, Vector2 way)
        {
            if (!cast.Struck.Contains(foe)) cast.Struck.Add(foe);
            if (page.DealsDamage || page.Page.Ailments.Length > 0) Hit(cast.Source, page, foe, strength, from, way);
            if (page.Page.Push != 0f) Thrust(foe, page.Page.Push, from);
        }

        // A page that throws or drags, by how much it gives: the heaviest barely move, and a hero's feet are their own.
        private static void Thrust(Actor target, float speed, Vector2 from)
        {
            if (target.IsHero || target.Still || target.Dead) return;
            Vector2 away = target.Position - from;
            if (away.LengthSquared < 0.01f) return;
            float give = (target.Nature & Condition.Boss) != 0 ? BossGive : (target.Nature & Condition.Elite) != 0 ? EliteGive : 1f;
            target.Push += away.Normalized() * (speed * give);
        }

        // Everything a page does that isn't to an Enemy, at a place. What can only happen once doesn't happen again with each pulse.
        private void Do(Cast cast, PageProfile page, in GroundShape shape, float strength, bool pulsing)
        {
            PagePlan plan = page.Page;
            Actor user = cast.Source;
            ActorSheet sheet = user.Sheet;

            if (plan.Heals.Length > 0 || plan.Gifts.Length > 0)
            {
                _friends.Clear();
                Within(shape, user.Team == Team.Enemies ? Team.Heroes : Team.Enemies, _friends);

                for (int i = 0; i < plan.Heals.Length; i++)
                {
                    float amount = page.Heals[i] * strength * sheet.RecoveryScale;
                    if (plan.Heals[i].Whom == Whom.You) Restore(user, plan.Heals[i].Pool, amount);
                    else
                    {
                        foreach (Actor friend in _friends) Restore(friend, plan.Heals[i].Pool, amount);
                    }
                }

                for (int i = 0; i < plan.Gifts.Length; i++)
                {
                    GiftPart gift = plan.Gifts[i];
                    if (gift.Whom == Whom.You) AddBuff(user, gift.Buff, page.GiftSeconds[i]);
                    else
                    {
                        foreach (Actor friend in _friends) AddBuff(friend, gift.Buff, page.GiftSeconds[i]);
                    }
                }
            }

            if (pulsing) return;
            Vector2 at = shape.Origin;
            if (plan.Minion != null) _session.Summon(user, page, at);
            if (plan.MakesWater) Land.AddPool(at, shape.Kind == ShapeKind.Circle ? shape.Length : GroundShape.FlightWidth * 2f, (float)Now + MathF.Max(page.Duration, WaterStays));
            if (plan.Moves) _session.Carry(user, at);
        }

        // Back into a pool, for whoever. It's what's said on the page and a friend's own recovery doesn't come into it.
        private void Restore(Actor who, Pool pool, float amount)
        {
            if (who.Dead || amount <= 0f) return;
            ActorSheet sheet = who.Sheet;
            switch (pool)
            {
                case Pool.Mana:
                    who.Vitals.Mana = MathF.Min(who.Vitals.Mana + amount, sheet.MaxMana);
                    break;
                case Pool.Barrier:
                    who.Vitals.Barrier = MathF.Min(who.Vitals.Barrier + amount, sheet.MaxBarrier);
                    break;
                default:
                    Hits.Heal(sheet, ref who.Vitals, amount);
                    break;
            }
            Show(new HealEvent { Position = new Vector3(who.Position.X, who.Height + 3.4f, who.Position.Y), Amount = amount, Pool = pool }, who.Position);
        }

        private void Fire(Cast cast, PageProfile page, Vector2 at, Vector2 way, Actor? off)
        {
            PagePlan plan = page.Page;

            // Whoever's ridden it has arrived, which is the whole of its flight.
            if (plan.Rides)
            {
                Reached(cast, page, at, way, cast.Strength);
                return;
            }

            int count = page.Projectiles;
            float start = -(count - 1) * 0.5f * plan.Spread;
            for (int i = 0; i < count; i++)
            {
                if (!Spend(cast)) return;
                Vector2 from = Where(cast, page, at);

                if (plan.To == Bearing.Down)
                {
                    Drop(cast, page, count > 1 && plan.FromWithin <= 0f ? Scattered(from, RainScatter) : from, way);
                    continue;
                }

                Vector2 target = Aimed(cast, page, from, way);
                Vector2 toward = target - from;
                float distance = toward.Length;
                Vector2 aimed = distance > 0.01f ? toward / distance : way;

                float angle = start + i * plan.Spread;
                float cos = MathF.Cos(angle);
                float sin = MathF.Sin(angle);
                var direction = new Vector2(aimed.X * cos - aimed.Y * sin, aimed.X * sin + aimed.Y * cos);

                // What flies from someone's hands starts ahead of them, and at the height they let it go.
                bool hands = plan.From == Start.You && plan.Index == 0;
                float height = (hands ? cast.Source.Height : Ground(from).Y) + FlightHeight;
                float length = plan.Stops ? MathF.Min(distance, page.Range) : page.Range;
                Launch(cast, page, hands ? from + direction * FlightOffset : from, direction, height, cast.Strength, length, null, off);
            }
        }

        private static string ModelOf(Actor user, PageProfile page)
        {
            Tags weapon = user.Loadout.Weapon.Tags;
            if ((weapon & Tags.Bow) != 0) return "arrow";
            if ((weapon & (Tags.Wand | Tags.Staff)) != 0) return $"bolt:{page.Page.Look}";
            return Thrown.Prefix + Thrown.Of(weapon);
        }

        // What starts from an impact starts touching whoever took it, and stopping there is no way to get anywhere, so that one it goes straight through.
        private void Launch(Cast cast, PageProfile page, Vector2 from, Vector2 direction, float height, float strength, float length, Projectile? parent, Actor? off = null)
        {
            Actor user = cast.Source;
            float speed = MathF.Max(page.Speed, 1f);
            float yaw = MathF.Atan2(direction.X, direction.Y);
            var origin = new Vector3(from.X, height, from.Y);
            var projectile = new Projectile
            {
                Cast = cast,
                Page = page,
                Life = length / speed + (page.Page.Stops ? 0f : FlightGrace),
                Pierce = page.Pierce,
                Chains = parent?.Chains ?? page.Chains,
                Strength = strength,
                Start = from,
                Way = direction,
                Returning = parent?.Returning ?? false,
            };
            if (parent != null) projectile.Spent.AddRange(parent.Spent);
            if (off != null) projectile.Spent.Add(off);

            World.CreateEntity("Projectile", Parts(user,
                new NetIdentityComponent(Server.Ghosts.NextId(), importance: 2f),
                new PositionComponent(origin),
                new RotationComponent(Quaternion.FromAxisAngle(Vector3.UnitY, yaw)),
                new VisualComponent(ModelOf(user, page), VisualKind.Prop),
                new BallisticComponent { Origin = origin, Velocity = new Vector3(direction.X, 0f, direction.Y) * speed, StartTick = Server.Tick, FromHeroes = user.Team == Team.Heroes },
                new ProjectileComponent { State = projectile }));
        }

        // What leaves a hero's hands is what was in them, down to the look of it.
        private static IComponent[] Parts(Actor user, params IComponent[] parts)
        {
            if (user.Player is not ServerPlayer hero || !hero.Hero.Equipped.TryGetValue(WornSlot.MainHand, out Item? weapon)) return parts;
            if ((user.Loadout.Weapon.Tags & (Tags.Bow | Tags.Wand | Tags.Staff)) != 0) return parts;

            ulong seed = weapon.LookSeed();
            return parts.Append(new ThrownComponent { SeedLow = (uint)seed, SeedHigh = (uint)(seed >> 32), Traits = ItemLook.Traits(weapon) }).ToArray();
        }

        // Out of the sky. What it does happens when it says it will, for what's on the ground to be true, so what's in the air is only there to be seen.
        private void Drop(Cast cast, PageProfile page, Vector2 at, Vector2 way)
        {
            float speed = MathF.Max(page.Speed, 1f);
            float fall = SkyHeight / speed;
            Vector3 ground = Ground(at);
            var origin = new Vector3(ground.X, ground.Y + SkyHeight, ground.Z);
            Actor user = cast.Source;

            World.CreateEntity("Projectile", Parts(user,
                new NetIdentityComponent(Server.Ghosts.NextId(), importance: 2f),
                new PositionComponent(origin),
                new RotationComponent(Quaternion.FromAxisAngle(Vector3.UnitX, MathF.PI * 0.5f)),
                new VisualComponent(ModelOf(user, page), VisualKind.Prop),
                new BallisticComponent { Origin = origin, Velocity = -Vector3.UnitY * speed, StartTick = Server.Tick, FromHeroes = user.Team == Team.Heroes },
                new ProjectileComponent { State = new Projectile { Cast = cast, Page = page, Life = fall, Falling = true, Start = at, Way = way } }));

            Defer(new Consequence { Kind = Later.Fall, At = Now + fall, Source = user, Cast = cast, Page = page.Page.Index, Origin = at, Facing = way, Strength = cast.Strength });
        }

        private void Fell(Cast cast, PageProfile page, Vector2 at, Vector2 way, float strength) => Reached(cast, page, at, way, strength);

        // Whatever's where it came to an end, for what doesn't fly into anyone: what fell, and whoever's ridden there.
        private void Reached(Cast cast, PageProfile page, Vector2 at, Vector2 way, float strength)
        {
            GroundShape spot = GroundShape.Circle(at, GroundShape.FlightWidth);
            Cover(cast, page, spot, strength);
            Turn(cast, page, at, way);
        }

        /// <summary>A projectile has reached something, or the end of its strength. Returns whether it flies on.</summary>
        public bool Touch(Projectile projectile, Vector3 at)
        {
            if (projectile.Falling) return true;

            PageProfile page = projectile.Page;
            Cast cast = projectile.Cast;
            Actor source = cast.Source;
            bool friends = page.Page.SeeksFriends;

            _found.Clear();
            Within(GroundShape.Circle(at.Xz, GroundShape.FlightWidth), friends ? (source.Team == Team.Enemies ? Team.Heroes : Team.Enemies) : source.Team, _found);

            bool flying = true;
            foreach (Actor foe in _found)
            {
                if (projectile.Spent.Contains(foe)) continue;

                // Nothing stops at whoever it's only just left.
                if (foe == source && page.Page.Index == 0) continue;
                projectile.Spent.Add(foe);
                float strength = projectile.Strength * Flight(projectile, at.Xz);
                if (!friends) Strike(cast, page, foe, strength, at.Xz, projectile.Way);
                Do(cast, page, GroundShape.Circle(at.Xz, GroundShape.FlightWidth), strength, false);
                Turn(cast, page, at.Xz, projectile.Way, foe);

                if (projectile.Chains > 0 && Retarget(projectile, foe, at))
                {
                    flying = false;
                    break;
                }

                if (projectile.Pierce > 0)
                {
                    projectile.Pierce--;
                    continue;
                }
                flying = false;
                break;
            }
            return flying;
        }

        /// <summary>A flight has come to an end against something that isn't a body, or at the end of its range. That's an impact as well: only there's nobody to take it.</summary>
        public void Ended(Projectile projectile, Vector3 at)
        {
            if (projectile.Falling) return;
            Do(projectile.Cast, projectile.Page, GroundShape.Circle(at.Xz, GroundShape.FlightWidth), projectile.Strength, false);
            Turn(projectile.Cast, projectile.Page, at.Xz, projectile.Way);
        }

        // How a shot gets stronger as it goes, for those who've arranged that.
        private static float Flight(Projectile projectile, Vector2 at)
        {
            if (!projectile.Cast.Source.Sheet.Has(Rule.LongShot)) return 1f;
            float metres = (at - projectile.Start).Length;
            return metres < LongShotNear ? 1f - LongShotNearLess : 1f + MathF.Min(metres * LongShotPerMetre, LongShotMost);
        }

        private bool Retarget(Projectile projectile, Actor from, Vector3 at)
        {
            Cast cast = projectile.Cast;
            _chained.Clear();
            Grid.Foes(cast.Source.Team, at.Xz, ProjectileChainReach, _chained);

            Actor? next = null;
            float best = float.PositiveInfinity;
            foreach (Actor foe in _chained)
            {
                if (projectile.Spent.Contains(foe)) continue;
                float distance = (foe.Position - at.Xz).LengthSquared;
                if (distance >= best) continue;
                best = distance;
                next = foe;
            }
            if (next == null || !Spend(cast)) return false;

            projectile.Chains--;
            Vector2 direction = (next.Position - at.Xz).Normalized();
            float growth = cast.Source.Sheet.Has(Rule.ChainsGrow) ? ChainGrowth : ChainFade;
            Launch(cast, projectile.Page, at.Xz, direction, at.Y, projectile.Strength * growth, projectile.Page.Range, projectile);
            return true;
        }

        /// <summary>A projectile that's done coming back to whoever threw it, if they're someone who does that.</summary>
        public void Returns(Projectile projectile, Vector3 at)
        {
            Actor source = projectile.Cast.Source;
            if (!source.Sheet.Has(Rule.ProjectilesReturn) || projectile.Returning || projectile.Falling || source.Dead) return;

            Vector2 toward = source.Position - at.Xz;
            if (toward.LengthSquared < 4f || !Spend(projectile.Cast)) return;
            projectile.Returning = true;
            projectile.Spent.Clear();
            Launch(projectile.Cast, projectile.Page, at.Xz, toward.Normalized(), at.Y, projectile.Strength, toward.Length, projectile);
        }

        private void Lay(Cast cast, PageProfile page, in GroundShape shape)
        {
            Actor user = cast.Source;
            float life = MathF.Max(page.Duration, ShortestZone);
            var zone = new ZoneState { Cast = cast, Page = page, Left = life, NextPulse = FirstPulse, Shape = shape, Strength = cast.Strength };

            // Each new one past what anyone can keep up is paid for by the oldest.
            user.Zones.Add(zone);
            user.Zones.RemoveAll(held => held.Left <= 0f);
            if (user.Zones.Count > MostZones)
            {
                user.Zones[0].Left = 0f;
                user.Zones.RemoveAt(0);
            }

            World.CreateEntity("Zone",
                new NetIdentityComponent(Server.Ghosts.NextId(), importance: 1.5f),
                new PositionComponent(Ground(shape.Origin)),
                new ZoneComponent { Sign = page.Sign.Number, Hostile = user.Team == Team.Enemies, Kind = shape.Kind, Facing = shape.Facing, Length = shape.Length, Width = shape.Width, Life = life },
                new ZoneStateComponent { State = zone });

            Do(cast, page, shape, 0f, false);
        }

        /// <summary>A zone's moment comes round: everything standing in it gets its share.</summary>
        public void Pulse(ZoneState zone)
        {
            Cover(zone.Cast, zone.Page, zone.Shape, zone.Strength * zone.Page.PulseShare, pulsing: true);
        }

        // ---- Landing.

        /// <summary>One page reaching one target. This is the whole of what happens when anything hits anything.</summary>
        /// <param name="way">Which way the blow is going, however long, if it's not simply away from where it came from.</param>
        public void Hit(Actor source, PageProfile skill, Actor target, float strength, Vector2 from, Vector2 way = default)
        {
            if (target.Dead) return;
            if (way.LengthSquared <= 0.01f) way = target.Position - from;
            way = way.LengthSquared > 0.01f ? way.Normalized() : Vector2.UnitY;

            // Nothing stands idle while someone shoots at it, and it doesn't keep that to itself.
            target.Alarm = EnemyBrainSystem.StruckAlarm;
            Vector3 point = new Vector3(target.Position.X, target.Height + 3.4f, target.Position.Y);

            if (target.Player is ServerPlayer hero && hero.Sim.Safe > 0f)
            {
                Dodged(target, point);
                return;
            }

            Condition foe = target.State;
            if ((skill.FoeConditions & Condition.Land) != 0) foe |= Land.At(target.Position);

            bool attack = (skill.Tags & Tags.Attack) != 0;
            if (attack && _luck.Float() < Hits.EvadeChance(target.Sheet.Evasion, skill.Accuracy))
            {
                Show(new DamageEvent { Position = point, Note = HitNote.Evaded, ToHero = target.IsHero, Target = GhostOf(target) }, target.Position);
                return;
            }

            float block = attack ? target.Sheet.BlockAttacks : (skill.Tags & Tags.Spell) != 0 ? target.Sheet.BlockSpells : 0f;
            if (block > 0f && _luck.Float() * 100f < block)
            {
                Blocked(source, skill, target, foe, point, way);
                return;
            }

            bool sureCrit = source.Critical;
            source.Critical = false;

            HitPacket hit = default;
            float power = strength * Advantage(source, target);
            if (source.Static > 0f && source.Sheet.Has(Rule.StaticCharge))
            {
                power *= 1f + source.Static * StaticPerCharge;
                source.Static = 0f;
            }
            Hits.Roll(skill, foe, _luck, ref hit, power, sureCrit);

            // Fed into a burn, it doesn't arrive as a blow at all.
            float burnFuel = 0f;
            if (source.Sheet.Has(Rule.FireFeedsBurn))
            {
                burnFuel = hit.Damage[(int)DamageType.Fire];
                hit.Damage[(int)DamageType.Fire] = 0f;
            }

            float bonus = React(source, skill, target, ref hit);
            DamageTable raw = hit.Damage;
            if (bonus != 1f)
            {
                for (int i = 0; i < DamageKinds.Count; i++) hit.Damage[i] *= bonus;
            }

            float total = Hits.Mitigate(target.Sheet, ref hit, source.State, target.Afflictions.ResistanceLost, target.TakenIncrease);

            if (source.Sheet.Has(Rule.FreezeStores) && target.Afflictions.FreezeLeft > 0f)
            {
                target.Afflictions.Stored += total;
                target.Afflictions.StoredBy = source;
                total = 0f;
                hit.Total = 0f;
            }

            if (target.Sheet.Has(Rule.FireHeals))
            {
                float fire = hit.Dealt[(int)DamageType.Fire] * FireHealShare;
                hit.Total -= fire;
                hit.Dealt[(int)DamageType.Fire] -= fire;
                Hits.Heal(target.Sheet, ref target.Vitals, fire * target.Sheet.RecoveryScale);
            }

            if (Shoulder(target, ref hit)) total = hit.Total;
            float healthBefore = target.Vitals.Health;
            float lost = hit.Total > 0f ? Hits.Pay(target.Sheet, ref target.Vitals, hit) : 0f;
            target.SinceHit = 0f;

            float weight = total / MathF.Max(target.Sheet.MaxHealth, 1f);
            if (skill.DealsDamage) Show(new DamageEvent { Position = point, Amount = total, Critical = hit.Critical, ToHero = target.IsHero, Body = BodyOf(target), Type = StrongestOf(hit), Target = GhostOf(target), Way = way, Weight = weight }, target.Position);
            Shove(target, skill, way, weight, hit.Critical);

            if (hit.Critical) source.SinceCrit = 0f;
            Recover(source, skill, total);
            Afflict(source, skill, target, raw, burnFuel, hit.Total, strength);

            if (hit.Critical && skill.Execute > 0f && target.Vitals.Health < target.Sheet.MaxHealth * skill.Execute / 100f) target.Vitals.Health = 0f;

            if (target.Sheet.Reflect > 0f && (skill.Tags & Tags.Melee) != 0 && total > 0f) Harm(target, source, total * target.Sheet.Reflect / 100f, DamageType.Physical);

            Watch(target, BookWhen.Damaged, total / MathF.Max(target.Sheet.MaxHealth, 1f) * 100f);
            Wake(target);
            SetOff(source, TriggerEvent.Hit, source.Position);
            if (hit.Critical) SetOff(source, TriggerEvent.Critical, source.Position);
            SetOff(target, TriggerEvent.Struck, target.Position);


            if (target.Vitals.Health <= 0f) Die(target, source, skill, from, Throw(way, (total - healthBefore) / MathF.Max(target.Sheet.MaxHealth, 1f), ForceOf(skill, hit.Critical)));
        }

        // How much of a blow is behind it: slow ones have more, and whatever went round the whole body has most.
        private static float ForceOf(PageProfile? skill, bool critical)
        {
            float force = skill == null ? 1f : Math.Clamp(skill.Page.UseTime / PageUseTime, 0.8f, 1.6f);
            return critical ? force * CriticalForce : force;
        }

        // What a blow does to someone it doesn't kill. It only ever moves those the rules allow to be moved: a hero's feet are their own, or they couldn't predict them.
        private static void Shove(Actor target, PageProfile skill, Vector2 way, float weight, bool critical)
        {
            if (target.IsHero || target.Still) return;

            float give = (target.Nature & Condition.Boss) != 0 ? BossGive : (target.Nature & Condition.Elite) != 0 ? EliteGive : 1f;
            target.Push += way * (MathF.Min(weight * PushPerHealth * ForceOf(skill, critical), MostPush) * give);
            target.Reeling = MathF.Max(target.Reeling, MathF.Min(ReelFrom + weight * ReelPerHealth, MostReel) * give);
        }

        // What's left of someone is thrown by however much more than enough it was. A blow that only just did it lets them drop, and one worth many of them sends them across the screen.
        private static Vector3 Throw(Vector2 way, float pastDead, float force)
        {
            float past = MathF.Max(pastDead, 0f);
            float speed = LeastThrow + MostThrow * (1f - MathF.Exp(-past * ThrowRate)) * force;
            return new Vector3(way.X * speed, speed * ThrowLift, way.Y * speed);
        }

        private uint GhostOf(Actor actor)
        {
            if (!actor.Entity.IsValid() || !World.TryGetEntityLocation(actor.Entity, out _, out _)) return 0;
            EntityView view = World.GetEntityView(actor.Entity);
            return view.Has<NetIdentityComponent>() ? view.Get<NetIdentityComponent>().Id : 0;
        }

        // What standing where you are is worth, for those who've arranged that.
        private static float Advantage(Actor source, Actor target)
        {
            if (!source.Sheet.Has(Rule.HighGround)) return 1f;
            float above = source.Height - target.Height;
            return above >= 0f ? 1f + MathF.Min(above * HighGroundPerMetre, HighGroundMost) : 1f - MathF.Min(-above * LowGroundPerMetre, LowGroundMost);
        }

        // The rest of a party takes some of it, if one of them has sworn to.
        private bool Shoulder(Actor target, ref HitPacket hit)
        {
            if (target.Team != Team.Heroes || !target.IsHero || hit.Total <= 0f) return false;

            foreach (ServerPlayer hero in _session.Heroes)
            {
                Actor guardian = hero.Actor;
                if (guardian == target || guardian.Dead || !guardian.Sheet.Has(Rule.Guardian)) continue;
                if ((guardian.Position - target.Position).LengthSquared > GuardianReach * GuardianReach) continue;

                float taken = hit.Total * GuardianShare;
                for (int i = 0; i < DamageKinds.Count; i++) hit.Dealt[i] *= 1f - GuardianShare;
                hit.Total *= 1f - GuardianShare;
                Hits.Pay(guardian.Sheet, ref guardian.Vitals, taken, 0f);
                return true;
            }
            return false;
        }

        private void Dodged(Actor target, Vector3 point)
        {
            target.SinceDodge = 0f;
            if (target.Sheet.Has(Rule.DodgeCrit)) target.Critical = true;
            Show(new DamageEvent { Position = point, Note = HitNote.Dodged, ToHero = true, Target = GhostOf(target) }, target.Position);
            SetOff(target, TriggerEvent.Dodge, target.Position);
        }

        private void Blocked(Actor source, PageProfile skill, Actor target, Condition foe, Vector3 point, Vector2 way)
        {
            target.SinceBlock = 0f;
            Show(new DamageEvent { Position = point, Note = HitNote.Blocked, ToHero = target.IsHero, Target = GhostOf(target), Way = way, Weight = 0.1f }, target.Position);
            SetOff(target, TriggerEvent.Block, target.Position);

            if (!target.Sheet.Has(Rule.BlockReflects)) return;
            HitPacket turned = default;
            Hits.Roll(skill, foe, _luck, ref turned);
            Harm(target, source, turned.Damage.Total, DamageType.Physical);
        }

        /// <summary>Damage that isn't a blow: thorns, a blow turned back, a burst of something. It's resisted, but it can't be dodged, and it sets nothing off.</summary>
        /// <param name="from">Where it came from, if not from whoever did it.</param>
        public void Harm(Actor source, Actor target, float amount, DamageType type, Tags tags = Tags.None, Vector2? from = null)
        {
            if (target.Dead || amount <= 0f) return;

            HitPacket hit = default;
            hit.Damage[(int)type] = amount;
            hit.Tags = tags;
            float total = Hits.Mitigate(target.Sheet, ref hit, source.State, target.Afflictions.ResistanceLost, target.TakenIncrease);
            float healthBefore = target.Vitals.Health;
            Hits.Pay(target.Sheet, ref target.Vitals, hit);
            target.SinceHit = 0f;

            Vector2 origin = from ?? source.Position;
            Vector2 way = target.Position - origin;
            way = way.LengthSquared > 0.01f ? way.Normalized() : Vector2.UnitY;

            Vector3 point = new Vector3(target.Position.X, target.Height + 3.4f, target.Position.Y);
            Show(new DamageEvent { Position = point, Amount = total, ToHero = target.IsHero, Body = BodyOf(target), Type = type, Target = GhostOf(target), Way = way, Weight = total / MathF.Max(target.Sheet.MaxHealth, 1f) }, target.Position);

            Watch(target, BookWhen.Damaged, total / MathF.Max(target.Sheet.MaxHealth, 1f) * 100f);
            Wake(target);
            if (target.Vitals.Health <= 0f) Die(target, source, null, origin, Throw(way, (total - healthBefore) / MathF.Max(target.Sheet.MaxHealth, 1f), BlastForce));
        }

        private void Blast(Actor source, Vector2 at, float radius, DamageType type, float amount, Tags tags, Actor? spared)
        {
            _chained.Clear();
            Grid.Foes(source.Team, at, radius, _chained);
            foreach (Actor foe in _chained)
            {
                if (foe != spared) Harm(source, foe, amount, type, tags, at);
            }
        }

        private static DamageType StrongestOf(in HitPacket hit)
        {
            int best = 0;
            for (int i = 1; i < DamageKinds.Count; i++)
            {
                if (hit.Dealt[i] > hit.Dealt[best]) best = i;
            }
            return (DamageType)best;
        }

        private static HitBody BodyOf(Actor actor)
        {
            if (actor.Body.StartsWith("skeleton")) return HitBody.Bone;
            if (actor.Body is "wolf" or "direwolf" or "boar" or "bear") return HitBody.Beast;
            return actor.Body is "warlord" or "revenant" ? HitBody.Armour : HitBody.Flesh;
        }

        private void Recover(Actor source, PageProfile skill, float dealt)
        {
            if (dealt <= 0f) return;
            ActorSheet sheet = source.Sheet;

            float health = dealt * skill.HealthLeech + skill.HealthOnHit;
            if (health > 0f) Hits.Heal(sheet, ref source.Vitals, health * sheet.RecoveryScale);

            float mana = dealt * skill.ManaLeech + skill.ManaOnHit;
            if (mana > 0f) source.Vitals.Mana = MathF.Min(source.Vitals.Mana + mana * sheet.RecoveryScale, sheet.MaxMana);
        }

        // A hit that wakes someone, even from afar.
        private void Wake(Actor target)
        {
            if (target.IsHero || !target.Entity.IsValid()) return;
            if (!World.TryGetEntityLocation(target.Entity, out _, out _)) return;

            EntityView view = World.GetEntityView(target.Entity);
            var enemy = view.Get<EnemyComponent>();
            if (enemy.State is not (EnemyState.Idle or EnemyState.Returning)) return;
            enemy.State = EnemyState.Chasing;
            view.Set(enemy);
        }

        // ---- Reactions. Two things meeting on someone, which neither does alone.

        // What's already on someone meets what's just arrived. Returns what the blow itself is worth now.
        private float React(Actor source, PageProfile skill, Actor target, ref HitPacket hit)
        {
            if ((skill.Tags & Tags.Reaction) != 0) return 1f;

            Afflictions a = target.Afflictions;
            float fire = hit.Damage[(int)DamageType.Fire];
            float cold = hit.Damage[(int)DamageType.Cold];
            float lightning = hit.Damage[(int)DamageType.Lightning];
            float physical = hit.Damage[(int)DamageType.Physical];
            float scale = skill.ReactionScale;
            float radius = ReactionRadius * skill.ReactionArea;

            // Shatter: what's frozen is brittle, and a real blow breaks it.
            if (a.FreezeLeft > 0f && physical > 0f)
            {
                Thaw(target);
                Announce(target, Reaction.Shatter, radius);
                return 1f + scale;
            }

            // Scald: fire on the chilled turns all that cold to steam.
            if (fire > 0f && (a.ChillLeft > 0f || a.FreezeLeft > 0f))
            {
                a.ChillLeft = 0f;
                Thaw(target);
                Spread(source, target, radius, DamageType.Fire, fire * ReactionShare * scale);
                Announce(target, Reaction.Scald, radius);
            }

            // Crackle: cold on the burning cracks what's been heated.
            else if (cold > 0f && a.BurnLeft > 0f)
            {
                a.BurnLeft = 0f;
                Spread(source, target, radius, DamageType.Physical, cold * ReactionShare * scale);
                Announce(target, Reaction.Crackle, radius);
            }

            // Flashfire: fire finds venom and takes all of it at once.
            if (fire > 0f && a.Venom is { Count: > 0 })
            {
                float left = 0f;
                foreach (VenomStack stack in a.Venom) left += stack.PerSecond * stack.Left;
                a.Venom.Clear();
                Spread(source, target, radius, DamageType.Fire, left * scale);
                Announce(target, Reaction.Flashfire, radius);
            }

            // Conduction: lightning runs through whatever's standing in the same water.
            if (lightning > 0f && Land.InWater(target.Position))
            {
                Defer(new Consequence { Kind = Later.Conduct, At = Now, Source = source, Victim = target, Amount = lightning * ReactionShare * scale });
                Announce(target, Reaction.Conduction, ConductReach);
            }
            return 1f;
        }

        private void Spread(Actor source, Actor around, float radius, DamageType type, float amount)
        {
            Defer(new Consequence { Kind = Later.Blast, At = Now, Source = source, Origin = around.Position, Radius = radius, Type = type, Amount = amount, Tags = Tags.Reaction, Victim = null });
        }

        private void Conduct(Actor source, Actor target, float amount)
        {
            _chained.Clear();
            Grid.Foes(source.Team, target.Position, ConductReach, _chained);
            foreach (Actor foe in _chained)
            {
                if (foe == target || !Land.InWater(foe.Position)) continue;
                Show(new LinkEvent { From = Ground(target.Position) + Vector3.UnitY, To = Ground(foe.Position) + Vector3.UnitY, Look = DamageType.Lightning }, target.Position);
                Harm(source, foe, amount, DamageType.Lightning, Tags.Reaction);
            }
        }

        private void Announce(Actor target, Reaction reaction, float radius)
        {
            Show(new ReactionEvent { Position = Ground(target.Position) + Vector3.UnitY * 3f, Kind = reaction, Radius = radius }, target.Position);
        }

        private void Thaw(Actor target)
        {
            Afflictions a = target.Afflictions;
            a.FreezeLeft = 0f;
            if (a.Stored <= 0f || a.StoredBy == null) return;

            float stored = a.Stored * (1f + StoredBonus);
            Actor source = a.StoredBy;
            a.Stored = 0f;
            a.StoredBy = null;
            Defer(new Consequence { Kind = Later.Blast, At = Now, Source = source, Origin = target.Position, Radius = 0.5f, Type = DamageType.Cold, Amount = stored, Tags = Tags.Reaction });
        }

        // ---- What stays behind.

        private void Afflict(Actor source, PageProfile skill, Actor target, in DamageTable raw, float burnFuel, float dealt, float strength)
        {
            float physical = raw[(int)DamageType.Physical];
            float fire = raw[(int)DamageType.Fire];
            float cold = raw[(int)DamageType.Cold];
            float lightning = raw[(int)DamageType.Lightning];
            float magic = raw[(int)DamageType.Magic];
            float poison = raw[(int)DamageType.Poison];

            // What a page inflicts outright isn't down to what the hit had in it, or to any chance. It's as strong as a hit of that much would have left.
            float Inflicted(Ailment ailment) => skill.Inflicts[(int)ailment] * strength;
            float health = target.Sheet.MaxHealth;
            ActorSheet sheet = target.Sheet;
            Afflictions a = target.Afflictions;

            bool Lands(Ailment ailment, bool sure = false)
            {
                float chance = skill.Ailments[(int)ailment].Chance;
                if (!sure && (chance <= 0f || _luck.Float() * 100f >= chance)) return false;
                return _luck.Float() * 100f >= sheet.AilmentAvoid[(int)ailment];
            }

            float Length(Ailment ailment, float seconds) => seconds * skill.Ailments[(int)ailment].Duration * sheet.AilmentTaken[(int)ailment];
            float Strength(Ailment ailment) => skill.Ailments[(int)ailment].Effect * skill.LingerScale[(int)ailment];

            float flame = fire + burnFuel + Inflicted(Ailment.Burn);
            if (flame > 0f && !sheet.Has(Rule.ImmuneToBurn) && Lands(Ailment.Burn, burnFuel > 0f || Inflicted(Ailment.Burn) > 0f))
            {
                float seconds = Length(Ailment.Burn, AilmentRules.BurnSeconds);
                float perSecond = AilmentRules.PerSecond(Ailment.Burn, flame) * Strength(Ailment.Burn);
                if (seconds > 0f && (perSecond >= a.BurnPerSecond || a.BurnLeft <= 0f))
                {
                    a.BurnPerSecond = perSecond;
                    a.BurnLeft = seconds;
                    a.BurnSource = source;
                }
            }

            float wound = physical + Inflicted(Ailment.Bleed);
            if (wound > 0f && Lands(Ailment.Bleed, Inflicted(Ailment.Bleed) > 0f))
            {
                float seconds = Length(Ailment.Bleed, AilmentRules.BleedSeconds);
                float perSecond = AilmentRules.PerSecond(Ailment.Bleed, wound) * Strength(Ailment.Bleed);
                if (seconds > 0f && (perSecond >= a.BleedPerSecond || a.BleedLeft <= 0f))
                {
                    a.BleedPerSecond = perSecond;
                    a.BleedLeft = seconds;
                    a.BleedSource = source;
                }
            }

            float toxin = physical + poison + Inflicted(Ailment.Venom);
            if (toxin > 0f && !sheet.Has(Rule.Carrier) && Lands(Ailment.Venom, Inflicted(Ailment.Venom) > 0f))
            {
                float seconds = Length(Ailment.Venom, AilmentRules.VenomSeconds);
                float perSecond = AilmentRules.PerSecond(Ailment.Venom, toxin) * Strength(Ailment.Venom);
                int most = source.Sheet.Has(Rule.EndlessVenom) ? AilmentRules.MostEndlessVenom : AilmentRules.MostVenom;
                a.Venom ??= new List<VenomStack>();
                if (a.Venom.Count >= most) a.Venom.RemoveAt(0);
                if (seconds > 0f) a.Venom.Add(new VenomStack { PerSecond = perSecond, Left = seconds, Source = source });
            }

            float frost = cold + Inflicted(Ailment.Chill);
            if (frost > 0f && !sheet.Has(Rule.CannotBeChilled) && Lands(Ailment.Chill, sure: true))
            {
                float effect = AilmentRules.ChillEffect(frost / health, skill.Ailments[(int)Ailment.Chill].Effect);
                if (effect >= a.ChillEffect || a.ChillLeft <= 0f)
                {
                    a.ChillEffect = effect;
                    a.ChillLeft = Length(Ailment.Chill, AilmentRules.ChillSeconds);
                    a.ChillSource = source;
                }
            }

            float ice = cold + Inflicted(Ailment.Freeze);
            if (ice > 0f && !sheet.Has(Rule.CannotBeFrozen) && Lands(Ailment.Freeze, ice / health >= AilmentRules.FreezeThreshold || Inflicted(Ailment.Freeze) > 0f))
            {
                a.FreezeLeft = MathF.Max(a.FreezeLeft, Length(Ailment.Freeze, AilmentRules.FreezeSeconds));
            }

            float charge = lightning + Inflicted(Ailment.Shock);
            if (charge > 0f && Lands(Ailment.Shock, Inflicted(Ailment.Shock) > 0f))
            {
                float effect = AilmentRules.ShockEffect(charge / health, skill.Ailments[(int)Ailment.Shock].Effect);
                if (effect >= a.ShockEffect || a.ShockLeft <= 0f)
                {
                    a.ShockEffect = effect;
                    a.ShockLeft = Length(Ailment.Shock, AilmentRules.ShockSeconds);
                }
            }

            if (magic + Inflicted(Ailment.Hex) > 0f && Lands(Ailment.Hex, Inflicted(Ailment.Hex) > 0f))
            {
                a.HexStacks = Math.Min(a.HexStacks + 1, AilmentRules.MostHex);
                a.HexEffect = MathF.Max(a.HexEffect, skill.Ailments[(int)Ailment.Hex].Effect);
                a.HexLeft = Length(Ailment.Hex, AilmentRules.HexSeconds);
            }

            float bearable = health * sheet.StunThreshold / 100f;
            float blow = dealt + Inflicted(Ailment.Stun);
            if (!sheet.Has(Rule.CannotBeStunned) && physical + Inflicted(Ailment.Stun) > 0f && Lands(Ailment.Stun, blow / bearable >= AilmentRules.StunThreshold || Inflicted(Ailment.Stun) > 0f))
            {
                a.StunLeft = MathF.Max(a.StunLeft, Length(Ailment.Stun, AilmentRules.StunSeconds));
            }
        }

        /// <summary>A moment of everything that's working on someone.</summary>
        public void Linger(Actor actor, float seconds)
        {
            Afflictions a = actor.Afflictions;
            ActorSheet sheet = actor.Sheet;
            float resistanceLost = a.ResistanceLost;
            float taken = 1f + actor.TakenIncrease;
            Actor? killer = null;
            float dealt = 0f;

            float Through(DamageType type, float perSecond)
            {
                float resistance = sheet.Resistance[(int)type] - resistanceLost;
                return perSecond * seconds * (1f - MathF.Max(resistance, Hits.LeastResistance) / 100f) * sheet.Taken[(int)type] * taken;
            }

            if (a.BurnLeft > 0f)
            {
                a.BurnLeft -= seconds;
                dealt += Through(DamageType.Fire, a.BurnPerSecond);
                killer = a.BurnSource;
                if (a.BurnLeft <= 0f) a.BurnPerSecond = 0f;
            }

            if (a.BleedLeft > 0f)
            {
                a.BleedLeft -= seconds;
                dealt += Through(DamageType.Physical, a.BleedPerSecond * (actor.Moving ? 1f + AilmentRules.BleedMoving : 1f));
                killer ??= a.BleedSource;
                if (a.BleedLeft <= 0f) a.BleedPerSecond = 0f;
            }

            if (a.Venom is { Count: > 0 })
            {
                for (int i = a.Venom.Count - 1; i >= 0; i--)
                {
                    VenomStack stack = a.Venom[i];
                    dealt += Through(DamageType.Poison, stack.PerSecond);
                    killer ??= stack.Source;
                    stack.Left -= seconds;
                    if (stack.Left <= 0f) a.Venom.RemoveAt(i);
                    else a.Venom[i] = stack;
                }
            }

            if (a.ChillLeft > 0f)
            {
                bool held = a.ChillSource is { Dead: false } && a.ChillSource.Sheet.Has(Rule.ChillNeverFades)
                    && (a.ChillSource.Position - actor.Position).LengthSquared < ChillReach * ChillReach;
                if (!held) a.ChillLeft -= seconds;
            }

            if (a.FreezeLeft > 0f)
            {
                a.FreezeLeft -= seconds;
                if (a.FreezeLeft <= 0f) Thaw(actor);
            }
            if (a.ShockLeft > 0f) a.ShockLeft -= seconds;
            if (a.HexLeft > 0f)
            {
                a.HexLeft -= seconds;
                if (a.HexLeft <= 0f) a.HexStacks = 0;
            }
            if (a.StunLeft > 0f) a.StunLeft -= seconds;

            if (dealt > 0f)
            {
                Hits.Pay(sheet, ref actor.Vitals, dealt, 0f);
                if (actor.Vitals.Health <= 0f) Die(actor, killer ?? actor, null, actor.Position, Vector3.Zero);
            }

            Hits.Recover(sheet, ref actor.Vitals, seconds, actor.SinceHit);
            if (actor.Vitals.Health <= 0f && !actor.Dead) Die(actor, actor, null, actor.Position, Vector3.Zero);
        }

        /// <summary>Someone's time is up, through nobody's doing.</summary>
        public void Dismiss(Actor actor)
        {
            if (actor.Dead) return;
            actor.Dead = true;
            actor.Vitals.Health = 0f;
            _session.Fell(actor, Vector3.Zero);
        }

        // Whoever dies, this is what it does for them, and for whoever did it.
        private void Die(Actor actor, Actor killer, PageProfile? skill, Vector2 from, Vector3 thrown)
        {
            if (actor.Dead) return;

            if (actor.Sheet.Has(Rule.Rebirth) && actor.RebirthIn <= 0f)
            {
                actor.RebirthIn = RebirthCooldown;
                actor.Vitals.Health = actor.Sheet.MaxHealth * RebirthHealth;
                actor.Vitals.Debt = 0f;
                Show(PageEvent.Round(Ground(actor.Position), PageSign.Blast(DamageType.Fire), RebirthRadius), actor.Position);
                Defer(new Consequence { Kind = Later.Blast, At = Now, Source = actor, Origin = actor.Position, Radius = RebirthRadius, Type = DamageType.Fire, Amount = actor.Sheet.MaxHealth * RebirthBlast, Tags = Tags.Reaction });
                return;
            }

            actor.Dead = true;
            actor.Vitals.Health = 0f;

            Actor credit = killer.Owner ?? killer;
            if (credit != actor && credit.IsFoeOf(actor)) Credit(credit, actor, skill);
            if (actor.Sheet.Has(Rule.BurstsOnDeath)) BeginBurst(actor);
            if (actor.Sheet.Has(Rule.RalliesOnDeath)) Defer(new Consequence { Kind = Later.Rally, At = Now, Source = actor });

            if (actor.Player != null)
            {
                _session.HeroThrown(actor, thrown);
                return;
            }
            _session.Fell(actor, thrown);
        }

        // It's the ring that's the danger, so there's long enough to see it and get clear. A crowd going up at once can kill whoever stands in the middle of it, and that's the omen working.
        private void BeginBurst(Actor actor)
        {
            Warn(GroundShape.Circle(actor.Position, BurstRadius), MarkTier.Grave, actor.Team == Team.Enemies, BurstDelay);
            float amount = Bestiary.DamageAt(actor.Loadout.Level) * BurstShare * _session.Offering.EnemyDamage;
            Defer(new Consequence { Kind = Later.Burst, At = Now + BurstDelay, Source = actor, Origin = actor.Position, Radius = BurstRadius, Type = DamageType.Fire, Amount = amount, Tags = Tags.Reaction });
        }

        private void Rally(Actor fallen)
        {
            _chained.Clear();
            Grid.Foes(fallen.Team == Team.Enemies ? Team.Heroes : Team.Enemies, fallen.Position, RallyReach, _chained);
            bool anyone = false;
            foreach (Actor ally in _chained)
            {
                if (ally.Dead) continue;
                AddBuff(ally, BuffCatalog.Rallied, 0f);
                anyone = true;
            }
            if (anyone) Show(PageEvent.Round(Ground(fallen.Position), PageSign.Blast(DamageType.Physical), RallyReach), fallen.Position);
        }

        private void Credit(Actor killer, Actor victim, PageProfile? skill)
        {
            killer.SinceKill = 0f;
            killer.KillHeat += 1f;
            killer.Vitals.Debt *= 1f - DebtForgiven;

            ActorSheet sheet = killer.Sheet;
            if (skill != null)
            {
                if (skill.HealthOnKill > 0f) Hits.Heal(sheet, ref killer.Vitals, skill.HealthOnKill * sheet.RecoveryScale);
                if (skill.ManaOnKill > 0f) killer.Vitals.Mana = MathF.Min(killer.Vitals.Mana + skill.ManaOnKill * sheet.RecoveryScale, sheet.MaxMana);
                if (skill.BarrierOnKill > 0f) killer.Vitals.Barrier = MathF.Min(killer.Vitals.Barrier + skill.BarrierOnKill, sheet.MaxBarrier);
            }

            if (sheet.ExplodeOnKill > 0f)
            {
                float amount = victim.Sheet.MaxHealth * sheet.ExplodeOnKill / 100f;
                Show(PageEvent.Round(Ground(victim.Position), PageSign.Blast(DamageType.Fire), ExplosionRadius), victim.Position);
                Defer(new Consequence { Kind = Later.Blast, At = Now, Source = killer, Origin = victim.Position, Radius = ExplosionRadius, Type = DamageType.Fire, Amount = amount, Tags = Tags.Reaction, Victim = victim });
            }

            if (sheet.Has(Rule.AilmentsLeap) && (victim.Afflictions.Current & Condition.Afflicted) != 0)
            {
                Defer(new Consequence { Kind = Later.Spread, At = Now, Source = killer, Victim = victim });
            }

            if (sheet.RaiseFallen > 0f && victim.Team == Team.Enemies && (victim.Nature & Condition.Boss) == 0 && _luck.Float() * 100f < sheet.RaiseFallen)
            {
                Defer(new Consequence { Kind = Later.Raise, At = Now + RisenDelay, Source = killer, Victim = victim });
            }

            SetOff(killer, TriggerEvent.Kill, victim.Position);
        }

        private void SpreadAilments(Actor source, Actor victim)
        {
            Afflictions from = victim.Afflictions;
            _chained.Clear();
            Grid.Foes(source.Team, victim.Position, AilmentLeapReach, _chained);
            foreach (Actor foe in _chained)
            {
                Afflictions to = foe.Afflictions;
                if (from.BurnLeft > 0f && from.BurnPerSecond > to.BurnPerSecond)
                {
                    to.BurnPerSecond = from.BurnPerSecond;
                    to.BurnLeft = from.BurnLeft;
                    to.BurnSource = source;
                }
                if (from.BleedLeft > 0f && from.BleedPerSecond > to.BleedPerSecond)
                {
                    to.BleedPerSecond = from.BleedPerSecond;
                    to.BleedLeft = from.BleedLeft;
                    to.BleedSource = source;
                }
                if (from.Venom is { Count: > 0 })
                {
                    to.Venom ??= new List<VenomStack>();
                    foreach (VenomStack stack in from.Venom)
                    {
                        if (to.Venom.Count < AilmentRules.MostVenom) to.Venom.Add(stack);
                    }
                }
                if (from.ChillLeft > 0f && from.ChillEffect > to.ChillEffect)
                {
                    to.ChillEffect = from.ChillEffect;
                    to.ChillLeft = from.ChillLeft;
                }
                if (from.ShockLeft > 0f && from.ShockEffect > to.ShockEffect)
                {
                    to.ShockEffect = from.ShockEffect;
                    to.ShockLeft = from.ShockLeft;
                }
            }
        }

        // ---- What goes off by itself.

        /// <summary>Something has happened to someone; anything of theirs waiting for that goes off.</summary>
        public void SetOff(Actor actor, TriggerEvent on, Vector2 at)
        {
            Watch(actor, (BookWhen)on, 1f);
            Mod[] triggers = actor.Loadout.Triggers;
            for (int i = 0; i < triggers.Length; i++)
            {
                ref readonly Mod trigger = ref triggers[i];
                if (trigger.On != on || actor.TriggerReady[i] > 0f) continue;
                if (trigger.Value < 100f && _luck.Float() * 100f >= trigger.Value) continue;

                if (actor.Sheet.Profile(Shelf.Get(trigger.Book)) is not BookProfile book) continue;

                actor.TriggerReady[i] = MathF.Max(book.Cooldown, TriggerGap);
                Defer(new Consequence { Kind = Later.Release, At = Now, Source = actor, Book = book, Origin = at, Facing = Vector2.UnitY, Reach = 0f, Strength = 1f });
            }
        }

        /// <summary>What someone does just by being there, for those with a keystone that says so. Called every tick for those it could matter to.</summary>
        public void Presence(Actor actor, float seconds)
        {
            ActorSheet sheet = actor.Sheet;
            if (sheet.Has(Rule.BurningTrail) && actor.Moving)
            {
                actor.TrailStep += sheet.MoveSpeed * seconds;
                if (actor.TrailStep >= TrailSpacing && sheet.Profile(Shelf.CinderTrail) is BookProfile cinders)
                {
                    actor.TrailStep = 0f;
                    Release(actor, cinders, actor.Position, Vector2.UnitY, 0f);
                }
            }

            if (sheet.Has(Rule.Carrier))
            {
                actor.CarrierClock += seconds;
                if (actor.CarrierClock >= CarrierInterval && sheet.Profile(Shelf.VenomCloud) is BookProfile cloud)
                {
                    actor.CarrierClock = 0f;
                    Defer(new Consequence { Kind = Later.Release, At = Now, Source = actor, Book = cloud, Origin = actor.Position, Facing = Vector2.UnitY, Strength = 1f });
                }
            }
        }

        public void AddBuff(Actor actor, BuffDefinition buff, float duration)
        {
            float length = duration > 0f ? duration : buff.Duration;
            for (int i = 0; i < actor.Buffs.Count; i++)
            {
                ActiveBuff active = actor.Buffs[i];
                if (active.Buff != buff) continue;

                // Refreshing it is free. Only another stack changes what anything adds up to.
                bool grows = active.Stacks < buff.MostStacks;
                active.Left = MathF.Max(active.Left, length);
                if (grows) active.Stacks++;
                actor.Buffs[i] = active;
                actor.LoadoutChanged |= grows;
                return;
            }

            actor.Buffs.Add(new ActiveBuff { Buff = buff, Stacks = 1, Left = length });
            actor.LoadoutChanged = true;
        }

        // ---- What it leaves behind.

        public void DropLoot(in EnemyComponent enemy, Vector3 position, Actor actor)
        {
            // A world holds a thousand of these. What's worth having comes from those that took some beating, and how much beating is in the health it really had.
            // What's been paid for on this land comes out of that, or peril would turn its fodder into champions and pay for them twice.
            LootBoon boon = _session.Boon;
            LootTier tier = LootTables.TierOf(enemy.Rank, actor.Sheet.MaxHealth / (Bestiary.HealthAt(enemy.Level) * _session.Offering.EnemyHealth));
            List<Item> items = Loot.Roll(tier, enemy.Level, StrongestHero, BestOfParty(s => s.ItemRarity) / 100f, _lootLuck, _luck.Fork(_luck.Int(1 << 30)), boon);

            // What it was wearing is yours now. That's the whole point of having seen it used.
            if (actor.Gear != null) items.AddRange(actor.Gear);

            int gold = Loot.RollGold(tier, enemy.Level, BestOfParty(s => s.GoldFind) / 100f, _luck, boon.Gold);
            _session.Ground.Spill(position, items, gold, LootTables.Of(tier).GoldPiles);
        }

        private int StrongestHero
        {
            get
            {
                int best = 1;
                foreach (ServerPlayer hero in _session.Heroes) best = Math.Max(best, hero.Hero.Level);
                return best;
            }
        }

        private float BestOfParty(Func<ActorSheet, float> stat)
        {
            float best = 0f;
            foreach (ServerPlayer hero in _session.Heroes) best = MathF.Max(best, stat(hero.Actor.Sheet));
            return best;
        }

        public void OpenChest(ServerPlayer opener, Vector3 position, int level, LootTier tier)
        {
            Server.Broadcast(new SoundEvent { Position = position, Cue = SoundCue.Chest }, position.Xz);
            ActorSheet sheet = opener.Actor.Sheet;
            LootBoon boon = _session.Boon;
            List<Item> items = Loot.Roll(tier, level, StrongestHero, sheet.ItemRarity / 100f, _lootLuck, _luck.Fork(_luck.Int(1 << 30)), boon);
            int gold = Loot.RollGold(tier, level, sheet.GoldFind / 100f, _luck, boon.Gold);
            _session.Ground.Spill(position, items, gold, LootTables.Of(tier).GoldPiles, GroundLoot.ChestDelay);
        }

        /// <summary>What a land pays out on top of everything else, for an offering seen through to the end. It comes as hoards, on the spot.</summary>
        public void PayOut(Vector3 position, int level, int hoards)
        {
            LootBoon boon = _session.Boon;
            float find = BestOfParty(s => s.ItemRarity) / 100f;
            var items = new List<Item>();
            int gold = 0;
            for (int i = 0; i < hoards; i++)
            {
                items.AddRange(Loot.Roll(LootTier.Hoard, level, StrongestHero, find, _lootLuck, _luck.Fork(_luck.Int(1 << 30)), boon));
                gold += Loot.RollGold(LootTier.Hoard, level, BestOfParty(s => s.GoldFind) / 100f, _luck, boon.Gold);
            }
            Server.Broadcast(new SoundEvent { Position = position, Cue = SoundCue.Chest }, position.Xz);
            _session.Ground.Spill(position, items, gold, LootTables.Of(LootTier.Hoard).GoldPiles * hoards, GroundLoot.ChestDelay);
        }
    }
}

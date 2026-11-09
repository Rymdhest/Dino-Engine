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
        private const float RareGive = 0.5f;
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
            Overflow,
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
            public bool Again;      // A book going again, which doesn't go again itself.
            public bool Quiet;      // Something that goes off every so often, which would be noise.
        }

        private readonly ServerSession _session;
        private readonly WorldRandom _luck = new WorldRandom(Environment.TickCount);
        private readonly List<Actor> _found = new();
        private readonly List<Actor> _chained = new();
        private readonly List<Actor> _near = new();
        private readonly List<Actor> _friends = new();
        private readonly List<Actor> _spread = new();
        private readonly List<Actor> _choir = new();
        private readonly List<Consequence> _later = new();
        private readonly LootLuck _lootLuck = new();

        public readonly ActorGrid Grid;
        public readonly LandReader Land;

        public Combat(ServerSession session)
        {
            _session = session;
            Grid = new ActorGrid(session.World.Size);
            Land = new LandReader(session.World, session.Terms.Has(Omens.Night));
        }

        private GameServer Server => _session.Server;

        /// <summary>Whether something with this chance happens, for what the fight's own luck should decide.</summary>
        public bool Luck(float chance) => _luck.Chance(chance);
        private ECSWorld World => Server.Ecs;
        private double Now => _session.Time;

        /// <summary>A book going off, from wherever and in whatever direction. <paramref name="reach"/> is how far off it was aimed.</summary>
        /// <summary>A Toggle Book's pulse: every page at once, for a share of what they'd do by hand.</summary>
        public void Pulse(Actor user, BookProfile book, Vector2 origin, Vector2 facing, float reach)
        {
            if (facing.LengthSquared < 1e-4f) facing = Vector2.UnitY;
            var cast = new Cast { Source = user, Book = book, Cursor = origin + facing * MathF.Min(reach, book.First.Range), Strength = book.AuraStrength, Pulsing = true };
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

        /// <param name="again">Whether it's a book going again of itself, which nothing makes go again.</param>
        /// <param name="quiet">Whether it goes off every so often by itself, and so shows without a spell's noise.</param>
        public void Release(Actor user, BookProfile book, Vector2 origin, Vector2 facing, float reach, int viewLag = 0, float strength = 1f, bool again = false, bool quiet = false)
        {
            user.SinceUse = 0f;
            if (facing.LengthSquared < 1e-4f) facing = Vector2.UnitY;
            ActorSheet sheet = user.Sheet;
            bool own = !again && (book.First.Tags & Tags.Triggered) == 0;

            // Some hands can't be trusted, either way.
            if (own && sheet.Has(Rule.Fumble) && _luck.Float() < FumbleChance)
            {
                Show(TextEvent.Word(new Vector3(user.Position.X, user.Height + 5f, user.Position.Y), "Fumbled"), user.Position);
                return;
            }
            if (own && sheet.Has(Rule.Fluke) && _luck.Float() < FlukeChance)
            {
                Defer(new Consequence { Kind = Later.Release, At = Now + FlukeDelay, Source = user, Book = book, Origin = origin, Facing = facing, Reach = reach, Strength = strength, Again = true });
            }

            var cast = new Cast { Source = user, Book = book, Cursor = origin + facing * MathF.Min(reach, book.First.Range), Strength = strength, ViewLag = viewLag, Quiet = quiet };
            Open(cast, 0, origin, facing);

            if (own && sheet.Has(Rule.Mirror))
            {
                var back = new Cast { Source = user, Book = book, Cursor = origin * 2f - cast.Cursor, Strength = strength };
                Open(back, 0, origin, -facing);
            }
            if (own && sheet.Has(Rule.Choir)) Choir(user, book, cast.Cursor);

            if ((book.First.Tags & Tags.Spell) == 0 || (book.First.Tags & Tags.Triggered) != 0) return;

            if (sheet.Has(Rule.SpellEcho) && strength >= 1f && !again)
            {
                Defer(new Consequence { Kind = Later.Release, At = Now + EchoDelay, Source = user, Book = book, Origin = origin, Facing = facing, Reach = reach, Strength = EchoStrength, Again = true });
            }

            if (sheet.Has(Rule.WaterTrail) && sheet.Profile(Shelf.Puddle) is BookProfile puddle) Release(user, puddle, cast.Cursor, facing, 0f);
        }

        // Every minion of theirs does it too, from where it stands, at the same place, for a share of it.
        private void Choir(Actor user, BookProfile book, Vector2 cursor)
        {
            _choir.Clear();
            foreach (Actor minion in _session.Minions)
            {
                if (minion.Owner == user && !minion.Dead) _choir.Add(minion);
            }
            foreach (Actor minion in _choir)
            {
                Vector2 way = cursor - minion.Position;
                way = way.LengthSquared > 0.01f ? way.Normalized() : Vector2.UnitY;
                Open(new Cast { Source = minion, Book = book, Cursor = cursor, Strength = ChoirStrength }, 0, minion.Position, way);
            }
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
                    if (c.Book != null) Release(c.Source, c.Book, c.Origin, c.Facing, c.Reach, 0, c.Strength, c.Again, c.Quiet);
                    break;
                case Later.Overflow:
                    if (c.Victim != null) Overflow(c.Source, c.Victim, c.Amount);
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
        public Entity Warn(in GroundShape shape, MarkTier tier, bool hostile, float landsIn, int owner = 0, Rarity from = Rarity.Normal)
        {
            uint ticks = Server.Tick + (uint)MathF.Round(landsIn * NetTime.TicksPerSecond);
            return World.CreateEntity("Warning",
                new NetIdentityComponent(Server.Ghosts.NextId(), importance: hostile ? 6f : 2f),
                new PositionComponent(Ground(shape.Origin)),
                new TelegraphComponent { Serial = ++_marksMade, Kind = shape.Kind, Tier = tier, Hostile = hostile, Owner = owner, From = from, Facing = shape.Facing, Length = shape.Length, Width = shape.Width, Start = Server.Tick, Lands = ticks },
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
            if (page.Page.Last)
            {
                RunOn(cast, at, way);
                return;
            }
            Defer(new Consequence { Kind = Later.Turn, At = Now, Source = cast.Source, Cast = cast, Page = page.Page.Index + 1, Origin = at, Facing = way, Victim = off });
        }

        // The last page is done, so the first opens again from there, weaker, and only the once.
        private void RunOn(Cast cast, Vector2 at, Vector2 way)
        {
            if (cast.RanOn || cast.Pulsing || !cast.Source.Sheet.Has(Rule.RunOn)) return;
            cast.RanOn = true;
            var again = new Cast { Source = cast.Source, Book = cast.Book, Cursor = cast.Cursor, Strength = cast.Strength * RunOnStrength, RanOn = true };
            Defer(new Consequence { Kind = Later.Turn, At = Now, Source = cast.Source, Cast = again, Page = 0, Origin = at, Facing = way });
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
            return Remote(cast, plan) ? cast.Cursor : from;
        }

        // Whether what would happen around them happens where they're aiming instead.
        private static bool Remote(Cast cast, PagePlan plan) => plan.Kind == PageKind.Area && plan.From == Start.You && plan.Index == 0 && cast.Source.Sheet.Has(Rule.Remote);

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
            return target;
        }

        private void Spread(Cast cast, PageProfile page, Vector2 at, Vector2 way)
        {
            if (!Spend(cast)) return;

            GroundShape shape = GroundShape.Of(page, Where(cast, page, at), way);
            if (page.Page.Lingers) Lay(cast, page, shape);
            else
            {
                // A plain blow shows for itself, in the hands of whoever made it, which a pulse isn't, and one landing far from them isn't either.
                if (page.Sign.Form != PageForm.Strike || cast.Pulsing || Remote(cast, page.Page)) Announce(page, shape, cast.Pulsing || cast.Quiet);
                Cover(cast, page, shape, cast.Strength);
            }
            if (page.Page.Last) RunOn(cast, shape.Origin, way);
        }

        private void Announce(PageProfile page, in GroundShape shape, bool quiet = false)
        {
            Vector2 at = shape.Origin;
            Show(new PageEvent { Position = Ground(at), Sign = page.Sign.Number, Kind = shape.Kind, Facing = shape.Facing, Length = shape.Length, Width = shape.Width, Quiet = quiet }, at);
            if (page.Sign.Form == PageForm.Beam) Show(new LinkEvent { From = Ground(at) + Vector3.UnitY * 3f, To = Ground(at + shape.Facing * shape.Length) + Vector3.UnitY * 3f, Look = page.Look, Wide = true }, at);
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
            if (page.DealsDamage || page.Page.Ailments.Length > 0)
            {
                Hit(cast.Source, page, foe, strength, from, way);
                if ((page.Tags & Tags.Spell) != 0 && cast.Source.Sheet.Has(Rule.SpellsTwice)) Hit(cast.Source, page, foe, strength, from, way);
            }
            if (page.Page.Push != 0f) Thrust(foe, page.Page.Push, from);
            if ((page.Tags & Tags.Projectile) != 0 && cast.Source.Sheet.Has(Rule.Drags)) Thrust(foe, -DragSpeed, cast.Source.Position);
        }

        // A page that throws or drags, by how much it gives: the heaviest barely move, and a hero's feet are their own.
        private static void Thrust(Actor target, float speed, Vector2 from)
        {
            if (target.IsHero || target.Still || target.Dead) return;
            Vector2 away = target.Position - from;
            if (away.LengthSquared < 0.01f) return;
            float give = (target.Nature & Condition.Boss) != 0 ? BossGive : (target.Nature & Condition.Rare) != 0 ? RareGive : 1f;
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

                float restored = 0f;
                for (int i = 0; i < plan.Heals.Length; i++)
                {
                    float amount = page.Heals[i] * strength * sheet.RecoveryScale;
                    if (plan.Heals[i].Whom == Whom.You) restored += Restore(user, plan.Heals[i].Pool, amount);
                    else
                    {
                        foreach (Actor friend in _friends) restored += Restore(friend, plan.Heals[i].Pool, amount);
                    }
                }

                if (restored > 0f && sheet.Has(Rule.HealsHarm))
                {
                    Show(PageEvent.Round(Ground(shape.Origin), PageSign.Blast(DamageType.Magic), HealsHarmRadius), shape.Origin);
                    Defer(new Consequence { Kind = Later.Blast, At = Now, Source = user, Origin = shape.Origin, Radius = HealsHarmRadius, Type = DamageType.Magic, Amount = restored * HealsHarmShare, Tags = Tags.Reaction });
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

        // Back into a pool, for whoever. It's what's said on the page and a friend's own recovery doesn't come into it. Returns what was offered.
        private float Restore(Actor who, Pool pool, float amount)
        {
            if (who.Dead || amount <= 0f) return 0f;
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
            return amount;
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

            // For some, what flies goes out all around them, evenly, the first of it where they aimed.
            bool ring = count > 1 && cast.Source.Sheet.Has(Rule.Starburst);
            float spread = ring ? MathF.Tau / count : plan.Spread;
            float start = ring ? 0f : -(count - 1) * 0.5f * spread;
            for (int i = 0; i < count; i++)
            {
                if (!Spend(cast)) return;
                Vector2 from = Where(cast, page, at);

                if (plan.Falls || cast.Source.Sheet.Has(Rule.Lobbed))
                {
                    Vector2 lands = Aimed(cast, page, from, way);
                    Drop(cast, page, count > 1 ? Scattered(lands, RainScatter) : lands, way);
                    continue;
                }

                Vector2 target = Aimed(cast, page, from, way);
                Vector2 toward = target - from;
                float distance = toward.Length;
                Vector2 aimed = distance > 0.01f ? toward / distance : way;

                float angle = start + i * spread;
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

        // A Spell is a ball of what it deals, and so is anything a wand or a staff does. Any other Attack is whatever's in the hand: an arrow from a bow, and anything else thrown.
        private static string ModelOf(Actor user, PageProfile page)
        {
            Tags weapon = user.Loadout.Weapon.Tags;
            if (page.Page.Hand == Hand.Spell || (weapon & (Tags.Wand | Tags.Staff)) != 0) return $"bolt:{page.Look}";
            if ((weapon & Tags.Bow) != 0) return "arrow";
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
                Forked = parent?.Forked ?? false,
            };
            if (parent != null) projectile.Spent.AddRange(parent.Spent);
            if (off != null) projectile.Spent.Add(off);

            World.CreateEntity("Projectile", Parts(user, page,
                new NetIdentityComponent(Server.Ghosts.NextId(), importance: 2f),
                new PositionComponent(origin),
                new RotationComponent(Quaternion.FromAxisAngle(Vector3.UnitY, yaw)),
                new VisualComponent(ModelOf(user, page), VisualKind.Prop),
                new BallisticComponent { Origin = origin, Velocity = new Vector3(direction.X, 0f, direction.Y) * speed, StartTick = Server.Tick, FromHeroes = user.Team == Team.Heroes },
                new ProjectileComponent { State = projectile }));
        }

        // What leaves a hero's hands is what was in them, down to the look of it.
        private static IComponent[] Parts(Actor user, PageProfile page, params IComponent[] parts)
        {
            if (user.Player is not ServerPlayer hero || !hero.Hero.Equipped.TryGetValue(WornSlot.MainHand, out Item? weapon)) return parts;
            if (page.Page.Hand == Hand.Spell || (user.Loadout.Weapon.Tags & (Tags.Bow | Tags.Wand | Tags.Staff)) != 0) return parts;

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

            World.CreateEntity("Projectile", Parts(user, page,
                new NetIdentityComponent(Server.Ghosts.NextId(), importance: 2f),
                new PositionComponent(origin),
                new RotationComponent(Quaternion.FromAxisAngle(Vector3.UnitX, MathF.PI * 0.5f)),
                new VisualComponent(ModelOf(user, page), VisualKind.Prop),
                new BallisticComponent { Origin = origin, Velocity = -Vector3.UnitY * speed, StartTick = Server.Tick, FromHeroes = user.Team == Team.Heroes },
                new ProjectileComponent { State = new Projectile { Cast = cast, Page = page, Life = fall, Falling = true, Start = at, Way = way } }));

            Defer(new Consequence { Kind = Later.Fall, At = Now + fall, Source = user, Cast = cast, Page = page.Page.Index, Origin = at, Facing = way, Strength = cast.Strength });
        }

        private void Fell(Cast cast, PageProfile page, Vector2 at, Vector2 way, float strength) => Reached(cast, page, at, way, strength);

        // Whatever's where it came to an end, for what doesn't fly into anyone: what fell, and whoever's ridden there. It's only an impact if there's someone there to take it, or the ground counts.
        private void Reached(Cast cast, PageProfile page, Vector2 at, Vector2 way, float strength)
        {
            GroundShape spot = GroundShape.Circle(at, GroundShape.FlightWidth);
            if (!page.Page.Grounded && !Occupied(cast, page, spot)) return;
            Cover(cast, page, spot, strength);
            Turn(cast, page, at, way);
        }

        // Whether anyone a page could reach is in a place: an Enemy, or a friend for one that only helps.
        private bool Occupied(Cast cast, PageProfile page, in GroundShape spot)
        {
            Team source = cast.Source.Team;
            _found.Clear();
            Within(spot, page.Page.SeeksFriends ? (source == Team.Enemies ? Team.Heroes : Team.Enemies) : source, _found);
            return _found.Count > 0;
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

                if (!projectile.Forked && source.Sheet.Has(Rule.Forks)) Fork(projectile, at);

                if (projectile.Pierce > 0)
                {
                    projectile.Pierce--;
                    if (source.Sheet.Has(Rule.PierceGrows)) projectile.Strength *= 1f + PierceGrowth;
                    continue;
                }
                flying = false;
                break;
            }
            return flying;
        }

        // Two more go on from where it struck, either side of the way it was going, and neither forks again.
        private void Fork(Projectile projectile, Vector3 at)
        {
            projectile.Forked = true;
            foreach (float angle in new[] { -ForkAngle, ForkAngle })
            {
                if (!Spend(projectile.Cast)) return;
                float cos = MathF.Cos(angle);
                float sin = MathF.Sin(angle);
                Vector2 way = projectile.Way;
                var direction = new Vector2(way.X * cos - way.Y * sin, way.X * sin + way.Y * cos);
                Launch(projectile.Cast, projectile.Page, at.Xz, direction, at.Y, projectile.Strength, projectile.Page.Range, projectile);
            }
        }

        /// <summary>
        /// A flight has come to an end against something that isn't a body, or at the end of its range. Nothing's been reached, so that's no impact and the book stops there, unless what it hit was solid and a sticker
        /// says that counts.
        /// </summary>
        /// <param name="blocked">Whether it ended against something solid, rather than running out.</param>
        public void Ended(Projectile projectile, Vector3 at, bool blocked)
        {
            if (projectile.Falling || !blocked || !projectile.Page.Page.Grounded) return;
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
            bool back = cast.Source.Sheet.Has(Rule.ChainsBack);
            _chained.Clear();
            Grid.Foes(cast.Source.Team, at.Xz, ProjectileChainReach, _chained);

            Actor? next = null;
            float best = float.PositiveInfinity;
            foreach (Actor foe in _chained)
            {
                if (foe == from || !back && projectile.Spent.Contains(foe)) continue;
                float distance = (foe.Position - at.Xz).LengthSquared;
                if (distance >= best) continue;
                best = distance;
                next = foe;
            }
            if (next == null || !Spend(cast)) return false;

            projectile.Chains--;
            if (back)
            {
                // Only who it's leaving is out of reach: anyone it struck before can be struck again.
                projectile.Spent.Clear();
                projectile.Spent.Add(from);
            }
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

            // Whoever marks someone marks the first they reach, and keeps them until they're gone.
            if (source.Sheet.Has(Rule.Marks) && source.Focus is not { Dead: false })
            {
                source.Focus = target;
                Show(TextEvent.Word(point + Vector3.UnitY * 1.5f, "Marked"), target.Position);
            }

            Condition foe = target.State;
            if ((skill.FoeConditions & Condition.Land) != 0) foe |= Land.At(target.Position);
            if ((skill.FoeConditions & Condition.Relations) != 0) foe |= Relation(source, target, skill.FoeConditions);

            bool attack = (skill.Tags & Tags.Attack) != 0;
            float evasion = attack ? target.Sheet.Evasion : (skill.Tags & Tags.Spell) != 0 ? target.Sheet.Evasion * target.Sheet.SpellEvasion / 100f : 0f;
            if (evasion > 0f && _luck.Float() < Hits.EvadeChance(evasion, skill.Accuracy))
            {
                Show(new DamageEvent { Position = point, Note = HitNote.Evaded, ToHero = target.IsHero, Target = GhostOf(target) }, target.Position);
                return;
            }

            float block = attack ? target.Sheet.BlockAttacks : (skill.Tags & Tags.Spell) != 0 ? target.Sheet.BlockSpells : 0f;
            if (target.Sheet.Has(Rule.Wall) && (target.State & Condition.Stationary) != 0 && (skill.Tags & Tags.Projectile) != 0) block = 100f;
            if (target.Sheet.Has(Rule.ThirdBlock) && ++target.Struck % ThirdBlockEvery == 0) block = 100f;
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

            // A share of whatever they are, which nothing else of the hitter's makes any bigger.
            if (source.Sheet.Has(Rule.OnlyLevels)) hit.Damage = default;
            if (source.Sheet.Has(Rule.Leveller))
            {
                float share = (target.Nature & Condition.Boss) != 0 ? LevellerBossShare : LevellerShare;
                hit.Damage[(int)DamageType.Physical] += target.Sheet.MaxHealth * share * power * (hit.Critical ? skill.CritMultiplier / 100f : 1f);
            }

            // What was blocked and kept goes back with this.
            if (source.Kept > 0f && source.Sheet.Has(Rule.Retort))
            {
                hit.Damage[(int)DamageType.Physical] += source.Kept;
                source.Kept = 0f;
            }

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
            if (Screen(source, target, ref hit)) total = hit.Total;
            if (PayInGold(target, ref hit)) total = hit.Total;
            if (target.Sheet.Has(Rule.HitCap) && hit.Total > target.Sheet.MaxHealth * HitCapShare)
            {
                Scale(ref hit, target.Sheet.MaxHealth * HitCapShare / hit.Total);
                total = hit.Total;
            }
            float healthBefore = target.Vitals.Health;
            float barrierBefore = target.Vitals.Barrier;
            float lost = hit.Total > 0f ? Hits.Pay(target.Sheet, ref target.Vitals, hit) : 0f;
            target.SinceHit = 0f;
            source.SinceStrike = 0f;

            float weight = total / MathF.Max(target.Sheet.MaxHealth, 1f);
            if (skill.DealsDamage) Show(new DamageEvent { Position = point, Amount = total, Critical = hit.Critical, ToHero = target.IsHero, Body = BodyOf(target), Type = StrongestOf(hit), Target = GhostOf(target), Way = way, Weight = weight }, target.Position);
            Shove(target, skill, way, weight, hit.Critical);

            if (hit.Critical) source.SinceCrit = 0f;
            Recover(source, skill, total);
            Afflict(source, skill, target, raw, burnFuel, hit.Total, strength);

            bool anyHit = source.Sheet.Has(Rule.ExecuteAny);
            float execute = skill.Execute + (anyHit ? ExecuteAnyReach : 0f);
            if ((hit.Critical || anyHit) && execute > 0f && target.Vitals.Health < target.Sheet.MaxHealth * execute / 100f) target.Vitals.Health = 0f;

            if (target.Sheet.Reflect > 0f && ((skill.Tags & Tags.Melee) != 0 || target.Sheet.Has(Rule.ThornsAlways)) && total > 0f) Harm(target, source, total * target.Sheet.Reflect / 100f, DamageType.Physical);
            if ((skill.Tags & Tags.Melee) != 0 && total > 0f && source.Sheet.Has(Rule.MeleeSplash)) Splash(source, target, raw);
            if (barrierBefore > 0f && target.Vitals.Barrier <= 0f) SetOff(target, TriggerEvent.BarrierBroke, target.Position);
            if (total > 0f && target.Sheet.Has(Rule.Adapts)) Adapt(target, StrongestOf(hit));

            Watch(target, BookWhen.Damaged, total / MathF.Max(target.Sheet.MaxHealth, 1f) * 100f);
            Wake(target);
            SetOff(source, TriggerEvent.Hit, source.Position);
            if (hit.Critical) SetOff(source, TriggerEvent.Critical, source.Position);
            SetOff(target, TriggerEvent.Struck, target.Position);

            if (target.Vitals.Health > 0f) return;
            if (source.Sheet.Has(Rule.Overkill) && target.Vitals.Health < 0f) Defer(new Consequence { Kind = Later.Overflow, At = Now, Source = source, Victim = target, Amount = -target.Vitals.Health });
            Die(target, source, skill, from, Throw(way, (total - healthBefore) / MathF.Max(target.Sheet.MaxHealth, 1f), ForceOf(skill, hit.Critical)));
        }

        // What's true between the two of them, worked out only when something asks.
        private Condition Relation(Actor source, Actor target, Condition asked)
        {
            Condition relation = target == source.Focus ? Condition.Marked : Condition.Unmarked;
            relation |= Unaware(target) ? Condition.Unaware : Condition.Aware;
            float apart = (target.Position - source.Position).Length - target.Radius;
            relation |= apart <= CloseReach ? Condition.UpClose : Condition.AtRange;

            if ((asked & (Condition.Isolated | Condition.Crowded)) != 0)
            {
                _spread.Clear();
                Grid.Foes(source.Team, target.Position, IsolatedReach, _spread);
                relation |= _spread.Count <= 1 ? Condition.Isolated : Condition.Crowded;
            }
            return relation;
        }

        // Whether it's yet to notice anyone. A hero always has.
        private bool Unaware(Actor actor)
        {
            if (actor.IsHero || !actor.Entity.IsValid() || !World.TryGetEntityLocation(actor.Entity, out _, out _)) return false;
            EntityView view = World.GetEntityView(actor.Entity);
            return view.Has<EnemyComponent>() && view.Get<EnemyComponent>().State is EnemyState.Idle or EnemyState.Returning;
        }

        private static void Scale(ref HitPacket hit, float kept)
        {
            for (int i = 0; i < DamageKinds.Count; i++) hit.Dealt[i] *= kept;
            hit.Total *= kept;
        }

        // A minion of theirs steps in front of part of it, for those whose minions do.
        private bool Screen(Actor source, Actor target, ref HitPacket hit)
        {
            if (hit.Total <= 0f || !target.Sheet.Has(Rule.MinionsShoulder)) return false;
            foreach (Actor minion in _session.Minions)
            {
                if (minion.Owner != target || minion.Dead) continue;
                float taken = hit.Total * ShoulderShare;
                Scale(ref hit, 1f - ShoulderShare);
                Harm(source, minion, taken, StrongestOf(hit));
                return true;
            }
            return false;
        }

        // Gold pays for part of it, while there's any to pay with.
        private bool PayInGold(Actor target, ref HitPacket hit)
        {
            if (hit.Total <= 0f || !target.Sheet.Has(Rule.GoldAbsorbs) || target.Player is not ServerPlayer hero || hero.Hero.Gold <= 0) return false;
            float covered = MathF.Min(hit.Total * GoldAbsorbShare, hero.Hero.Gold / GoldPerHealth);
            hero.Hero.Gold = Math.Max(hero.Hero.Gold - (int)MathF.Ceiling(covered * GoldPerHealth), 0);
            hero.SummaryChanged = true;
            Scale(ref hit, 1f - covered / hit.Total);
            return true;
        }

        // Hardened against what last got through, and only that.
        private void Adapt(Actor actor, DamageType type)
        {
            BuffDefinition wanted = BuffCatalog.Inured[(int)type];
            for (int i = actor.Buffs.Count - 1; i >= 0; i--)
            {
                if (actor.Buffs[i].Buff == wanted || Array.IndexOf(BuffCatalog.Inured, actor.Buffs[i].Buff) < 0) continue;
                actor.Buffs.RemoveAt(i);
                actor.LoadoutChanged = true;
            }
            AddBuff(actor, wanted, 0f);
        }

        // What was left over when someone died goes on to whoever's nearest.
        private void Overflow(Actor source, Actor from, float amount)
        {
            _spread.Clear();
            Grid.Foes(source.Team, from.Position, OverkillReach, _spread);
            Actor? next = null;
            float best = float.PositiveInfinity;
            foreach (Actor foe in _spread)
            {
                float distance = (foe.Position - from.Position).LengthSquared;
                if (foe == from || foe.Dead || distance >= best) continue;
                best = distance;
                next = foe;
            }
            if (next == null) return;

            Show(new LinkEvent { From = Ground(from.Position) + Vector3.UnitY * 2f, To = Ground(next.Position) + Vector3.UnitY * 2f, Look = DamageType.Physical }, from.Position);
            Harm(source, next, amount, DamageType.Physical, Tags.Reaction, from.Position);
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

            float give = (target.Nature & Condition.Boss) != 0 ? BossGive : (target.Nature & Condition.Rare) != 0 ? RareGive : 1f;
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

            ActorSheet sheet = target.Sheet;
            bool keeps = sheet.Has(Rule.Retort);
            bool feeds = sheet.Has(Rule.BlockFeedsMana) && (skill.Tags & Tags.Spell) != 0;
            if (!sheet.Has(Rule.BlockReflects) && !keeps && !feeds) return;

            HitPacket turned = default;
            Hits.Roll(skill, foe, _luck, ref turned);
            float blocked = turned.Damage.Total;
            if (keeps) target.Kept = MathF.Min(target.Kept + blocked, sheet.MaxHealth * RetortMost);
            if (feeds) target.Vitals.Mana = MathF.Min(target.Vitals.Mana + blocked * BlockManaShare, sheet.MaxMana);
            if (sheet.Has(Rule.BlockReflects)) Harm(target, source, blocked, DamageType.Physical);
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
            if (target.Vitals.Health > 0f) return;
            if (source.Sheet.Has(Rule.Overkill) && target.Vitals.Health < 0f) Defer(new Consequence { Kind = Later.Overflow, At = Now, Source = source, Victim = target, Amount = -target.Vitals.Health });
            Die(target, source, null, origin, Throw(way, (total - healthBefore) / MathF.Max(target.Sheet.MaxHealth, 1f), BlastForce));
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
            if (actor.Body.StartsWith("skeleton") || actor.Body.StartsWith("dino:") && actor.Body.Contains(".bones")) return HitBody.Bone;
            if (actor.Body is "wolf" or "direwolf" or "boar" or "bear" || actor.Body.StartsWith("dino:")) return HitBody.Beast;
            return actor.Body is "warlord" or "revenant" or "reich:legionary" ? HitBody.Armour : HitBody.Flesh;
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
                Announce(source, target, Reaction.Shatter, radius);
                return 1f + scale;
            }

            // Scald: fire on the chilled turns all that cold to steam.
            if (fire > 0f && (a.ChillLeft > 0f || a.FreezeLeft > 0f))
            {
                a.ChillLeft = 0f;
                Thaw(target);
                Spread(source, target, radius, DamageType.Fire, fire * ReactionShare * scale);
                Announce(source, target, Reaction.Scald, radius);
            }

            // Crackle: cold on the burning cracks what's been heated.
            else if (cold > 0f && a.BurnLeft > 0f)
            {
                a.BurnLeft = 0f;
                Spread(source, target, radius, DamageType.Physical, cold * ReactionShare * scale);
                Announce(source, target, Reaction.Crackle, radius);
            }

            // Flashfire: fire finds venom and takes all of it at once.
            if (fire > 0f && a.Venom is { Count: > 0 })
            {
                float left = 0f;
                foreach (VenomStack stack in a.Venom) left += stack.PerSecond * stack.Left;

                // Wildfire leaves it where it is and carries it on to whoever's near.
                if (source.Sheet.Has(Rule.Wildfire)) Defer(new Consequence { Kind = Later.Spread, At = Now, Source = source, Victim = target });
                else a.Venom.Clear();
                Spread(source, target, radius, DamageType.Fire, left * scale);
                Announce(source, target, Reaction.Flashfire, radius);
            }

            // Conduction: lightning runs through whatever's standing in the same water.
            if (lightning > 0f && Conductive(source, target))
            {
                Defer(new Consequence { Kind = Later.Conduct, At = Now, Source = source, Victim = target, Amount = lightning * ReactionShare * scale });
                Announce(source, target, Reaction.Conduction, ConductReach);
            }
            return 1f;
        }

        // A Melee Hit spilling onto whoever stands around whoever took it, of every type it was, as it left the hand: each of them has their own defences.
        private void Splash(Actor source, Actor target, in DamageTable dealt)
        {
            Show(PageEvent.Round(Ground(target.Position), PageSign.Blast(DamageType.Physical), SplashRadius), target.Position);
            for (int i = 0; i < DamageKinds.Count; i++)
            {
                if (dealt[i] <= 0f) continue;
                Defer(new Consequence { Kind = Later.Blast, At = Now, Source = source, Origin = target.Position, Radius = SplashRadius, Type = (DamageType)i, Amount = dealt[i] * SplashShare, Tags = Tags.Melee, Victim = target });
            }
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
                if (foe == target || !Conductive(source, foe)) continue;
                Show(new LinkEvent { From = Ground(target.Position) + Vector3.UnitY, To = Ground(foe.Position) + Vector3.UnitY, Look = DamageType.Lightning }, target.Position);
                Harm(source, foe, amount, DamageType.Lightning, Tags.Reaction);
            }
        }

        // Lightning runs through water, and for some through whoever's shocked as well.
        private bool Conductive(Actor source, Actor foe) => Land.InWater(foe.Position) || foe.Afflictions.ShockLeft > 0f && source.Sheet.Has(Rule.Conducts);

        private void Announce(Actor source, Actor target, Reaction reaction, float radius)
        {
            Show(new ReactionEvent { Position = Ground(target.Position) + Vector3.UnitY * 3f, Kind = reaction, Radius = radius }, target.Position);
            SetOff(source, TriggerEvent.Reaction, target.Position);
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
            if (sheet.Has(Rule.Inoculated) || sheet.Has(Rule.Barricaded) && target.Vitals.Barrier > 0f) return;

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
                if (seconds > 0f && a.BurnLeft > 0f && a.BurnSource == source && source.Sheet.Has(Rule.BurnsStack))
                {
                    // Embers on embers: what's there grows by the new one, as far as so many of the new one, and never shrinks for a weaker one.
                    a.BurnPerSecond = MathF.Max(a.BurnPerSecond, MathF.Min(a.BurnPerSecond + perSecond, perSecond * MostEmbers));
                    a.BurnLeft = MathF.Max(a.BurnLeft, seconds);
                }
                else if (seconds > 0f && (perSecond >= a.BurnPerSecond || a.BurnLeft <= 0f))
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
                a.VenomDrags |= source.Sheet.Has(Rule.VenomSlows);

                // A full dose lands all at once, for those whose venom does.
                if (source.Sheet.Has(Rule.VenomBursts) && a.Venom.Count >= AilmentRules.MostVenom)
                {
                    float dose = 0f;
                    foreach (VenomStack stack in a.Venom) dose += stack.PerSecond * stack.Left;
                    a.Venom.Clear();
                    Show(PageEvent.Round(Ground(target.Position), PageSign.Blast(DamageType.Poison), target.Radius * 2f), target.Position);
                    Defer(new Consequence { Kind = Later.Blast, At = Now, Source = source, Origin = target.Position, Radius = 0.5f, Type = DamageType.Poison, Amount = dose, Tags = Tags.Reaction });
                }
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
                a.HexStacks = Math.Min(a.HexStacks + 1, source.Sheet.Has(Rule.DeepHex) ? MostDeepHex : AilmentRules.MostHex);
                a.HexEffect = MathF.Max(a.HexEffect, skill.Ailments[(int)Ailment.Hex].Effect);
                a.HexLeft = Length(Ailment.Hex, AilmentRules.HexSeconds);
            }

            float bearable = health * sheet.StunThreshold / 100f;
            float blow = dealt + Inflicted(Ailment.Stun);
            if (!sheet.Has(Rule.CannotBeStunned) && physical + Inflicted(Ailment.Stun) > 0f && Lands(Ailment.Stun, blow / bearable >= AilmentRules.StunThreshold || Inflicted(Ailment.Stun) > 0f))
            {
                float stun = Length(Ailment.Stun, AilmentRules.StunSeconds);
                a.StunLeft = MathF.Max(a.StunLeft, stun);
                if (source.Sheet.Has(Rule.StunSpreads)) SpreadStun(source, target, stun);
                SetOff(source, TriggerEvent.Stun, target.Position);
            }
        }

        // Ringing in everyone's ears around it.
        private void SpreadStun(Actor source, Actor target, float seconds)
        {
            _spread.Clear();
            Grid.Foes(source.Team, target.Position, StunSpreadReach, _spread);
            foreach (Actor other in _spread)
            {
                if (other == target || other.Dead || other.Sheet.Has(Rule.CannotBeStunned)) continue;
                other.Afflictions.StunLeft = MathF.Max(other.Afflictions.StunLeft, seconds * other.Sheet.AilmentTaken[(int)Ailment.Stun]);
            }
        }

        // Some get back part of what their lingering harm does.
        private static void LeechOverTime(Actor? source, float dealt)
        {
            if (source is not { Dead: false } || dealt <= 0f || !source.Sheet.Has(Rule.LeechesOverTime)) return;
            Hits.Heal(source.Sheet, ref source.Vitals, dealt * OverTimeLeech * source.Sheet.RecoveryScale);
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
                float burnt = Through(DamageType.Fire, a.BurnPerSecond);
                dealt += burnt;
                LeechOverTime(a.BurnSource, burnt);
                killer = a.BurnSource;
                if (a.BurnLeft <= 0f) a.BurnPerSecond = 0f;
            }

            if (a.BleedLeft > 0f)
            {
                a.BleedLeft -= seconds;
                float bled = Through(DamageType.Physical, a.BleedPerSecond * (actor.Moving ? 1f + AilmentRules.BleedMoving : 1f));
                dealt += bled;
                LeechOverTime(a.BleedSource, bled);
                killer ??= a.BleedSource;
                if (a.BleedLeft <= 0f) a.BleedPerSecond = 0f;
            }

            if (a.Venom is { Count: > 0 })
            {
                for (int i = a.Venom.Count - 1; i >= 0; i--)
                {
                    VenomStack stack = a.Venom[i];
                    float poisoned = Through(DamageType.Poison, stack.PerSecond);
                    dealt += poisoned;
                    LeechOverTime(stack.Source, poisoned);
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

            // A dummy left alone a while is as good as new, so that a second try starts where the first did.
            if (actor.Dummy && actor.SinceHit > DummyRest && actor.Vitals.Health < sheet.MaxHealth) actor.Vitals = Vitals.Full(sheet);
            if (actor.Vitals.Health <= 0f && !actor.Dead) Die(actor, actor, null, actor.Position, Vector3.Zero);

            // Death is waiting for a kill, and doesn't wait long.
            if (actor.GaspLeft <= 0f || actor.Dead) return;
            actor.GaspLeft -= seconds;
            actor.Vitals.Health = MathF.Max(actor.Vitals.Health, 1f);
            if (actor.GaspLeft > 0f) return;
            actor.Vitals.Health = 0f;
            Die(actor, actor, null, actor.Position, Vector3.Zero);
        }

        /// <summary>Someone's time is up, through nobody's doing.</summary>
        public void Dismiss(Actor actor)
        {
            if (actor.Dead) return;
            actor.Dead = true;
            actor.Vitals.Health = 0f;
            if (actor.Owner != null) Gone(actor);
            _session.Fell(actor, Vector3.Zero);
        }

        // A minion of someone's is gone, however it went.
        private void Gone(Actor minion)
        {
            Actor owner = minion.Owner!;
            if (owner.Sheet.Has(Rule.MinionsBurst))
            {
                Show(PageEvent.Round(Ground(minion.Position), PageSign.Blast(DamageType.Fire), ExplosionRadius), minion.Position);
                Defer(new Consequence { Kind = Later.Blast, At = Now, Source = owner, Origin = minion.Position, Radius = ExplosionRadius, Type = DamageType.Fire, Amount = minion.Sheet.MaxHealth * MinionBurstShare, Tags = Tags.Reaction });
            }
            SetOff(owner, TriggerEvent.MinionDied, minion.Position);
        }

        /// <summary>The oldest of someone's minions still standing, if they have any.</summary>
        public Actor? Oldest(Actor owner)
        {
            foreach (Actor minion in _session.Minions)
            {
                if (minion.Owner == owner && !minion.Dead) return minion;
            }
            return null;
        }

        // Whoever dies, this is what it does for them, and for whoever did it.
        private void Die(Actor actor, Actor killer, PageProfile? skill, Vector2 from, Vector3 thrown)
        {
            if (actor.Dead) return;

            // A dummy is there to be hit, and never goes down.
            if (actor.Dummy)
            {
                actor.Vitals = Vitals.Full(actor.Sheet);
                return;
            }

            // Gasping, they can't die until the time's up.
            if (actor.GaspLeft > 0f)
            {
                actor.Vitals.Health = 1f;
                return;
            }

            if (actor.Sheet.Has(Rule.MinionShield) && Oldest(actor) is Actor standIn)
            {
                Dismiss(standIn);
                actor.Vitals.Health = actor.Sheet.MaxHealth * MinionShieldHealth;
                actor.Vitals.Debt = 0f;
                return;
            }

            if (actor.Sheet.Has(Rule.Rebirth) && actor.RebirthIn <= 0f)
            {
                actor.RebirthIn = RebirthCooldown;
                actor.Vitals.Health = actor.Sheet.MaxHealth * RebirthHealth;
                actor.Vitals.Debt = 0f;
                Show(PageEvent.Round(Ground(actor.Position), PageSign.Blast(DamageType.Fire), RebirthRadius), actor.Position);
                Defer(new Consequence { Kind = Later.Blast, At = Now, Source = actor, Origin = actor.Position, Radius = RebirthRadius, Type = DamageType.Fire, Amount = actor.Sheet.MaxHealth * RebirthBlast, Tags = Tags.Reaction });
                return;
            }

            if (actor.Sheet.Has(Rule.LastGasp) && actor.GaspIn <= 0f)
            {
                actor.GaspIn = GaspCooldown;
                actor.GaspLeft = GaspSeconds;
                actor.Vitals.Health = 1f;
                actor.Vitals.Debt = 0f;
                AddBuff(actor, BuffCatalog.LastGasp, GaspSeconds);
                return;
            }

            actor.Dead = true;
            actor.Vitals.Health = 0f;
            if (actor.Owner != null) Gone(actor);

            Actor credit = killer.Owner ?? killer;
            if (credit != actor && credit.IsFoeOf(actor)) Credit(credit, actor, skill);
            if (actor.Sheet.Has(Rule.BurstsOnDeath) && Goes(actor, Omens.Volatile)) BeginBurst(actor);
            if (actor.Sheet.Has(Rule.RalliesOnDeath) && Goes(actor, Omens.Rallying)) Defer(new Consequence { Kind = Later.Rally, At = Now, Source = actor });

            if (actor.Player != null)
            {
                _session.HeroThrown(actor, thrown);
                return;
            }
            _session.Fell(actor, thrown);
        }

        // Whether something an omen gives every enemy happens for this one, as the omen says how many of them it's true of. Anyone who has it from anywhere else always does.
        private bool Goes(Actor actor, OmenDefinition omen)
        {
            if (actor.Team != Team.Enemies || actor.Owner != null || !_session.Terms.Has(omen)) return true;
            return _luck.Chance(_session.Terms.Chance(omen));
        }

        // It's the ring that's the danger, so there's long enough to see it and get clear. A crowd going up at once can kill whoever stands in the middle of it, and that's the omen working.
        private void BeginBurst(Actor actor)
        {
            Warn(GroundShape.Circle(actor.Position, BurstRadius), MarkTier.Grave, actor.Team == Team.Enemies, BurstDelay);
            float amount = Bestiary.DamageAt(actor.Loadout.Level) * BurstShare * _session.Terms.EnemyDamage;
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
            if ((victim.Nature & (Condition.Rare | Condition.Boss)) != 0) killer.RaresSlain++;

            ActorSheet sheet = killer.Sheet;
            if (sheet.Has(Rule.NoItems)) victim.Plundered = true;
            if (sheet.Has(Rule.BleedBurst) && victim.Afflictions.BleedLeft > 0f) Burst(killer, victim, DamageType.Physical, victim.Sheet.MaxHealth * BleedBurstShare);
            if (sheet.Has(Rule.IceBursts) && victim.Afflictions.FreezeLeft > 0f) Burst(killer, victim, DamageType.Cold, victim.Sheet.MaxHealth * IceBurstShare);
            if (sheet.Has(Rule.KillResets) && killer.Player is ServerPlayer hero && killer.LastSlot >= 0) hero.Sim.Cooldowns[killer.LastSlot] = 0f;

            // A kill while gasping is what death was waiting for.
            if (killer.GaspLeft > 0f)
            {
                killer.GaspLeft = 0f;
                Hits.Heal(sheet, ref killer.Vitals, sheet.MaxHealth * GaspHeal);
                RemoveBuff(killer, BuffCatalog.LastGasp);
            }
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

        // What was on someone goes up around them as they die.
        private void Burst(Actor killer, Actor victim, DamageType type, float amount)
        {
            Show(PageEvent.Round(Ground(victim.Position), PageSign.Blast(type), ExplosionRadius), victim.Position);
            Defer(new Consequence { Kind = Later.Blast, At = Now, Source = killer, Origin = victim.Position, Radius = ExplosionRadius, Type = type, Amount = amount, Tags = Tags.Reaction, Victim = victim });
        }

        private void SpreadAilments(Actor source, Actor victim)
        {
            Afflictions from = victim.Afflictions;
            _chained.Clear();
            Grid.Foes(source.Team, victim.Position, AilmentLeapReach, _chained);
            foreach (Actor foe in _chained)
            {
                // What it's spreading from can still be standing there, and has all of it already.
                if (foe == victim) continue;
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
            if (on <= TriggerEvent.Flask) Watch(actor, (BookWhen)on, 1f);
            Mod[] triggers = actor.Loadout.Triggers;
            for (int i = 0; i < triggers.Length; i++)
            {
                ref readonly Mod trigger = ref triggers[i];
                if (trigger.On != on || actor.TriggerReady[i] > 0f || !Holds(actor, trigger)) continue;
                if (trigger.Value < 100f && _luck.Float() * 100f >= trigger.Value) continue;
                GoOff(actor, i, trigger, at);
            }
        }

        private static bool Holds(Actor actor, in Mod mod) => (mod.Need & ~actor.State) == 0 && (mod.Without & actor.State) == 0;

        // One trigger going: a buff given or taken away, or a book let off where it happened.
        private void GoOff(Actor actor, int index, in Mod trigger, Vector2 at)
        {
            actor.TriggerReady[index] = TriggerGap;
            if (trigger.Gift != 0)
            {
                if (BuffCatalog.Get(trigger.Gift) is not BuffDefinition buff) return;
                if (trigger.Spends) RemoveBuff(actor, buff);
                else AddBuff(actor, buff, 0f);
                return;
            }

            if (actor.Sheet.Profile(Shelf.Get(trigger.Book)) is not BookProfile book) return;
            actor.TriggerReady[index] = MathF.Max(book.Cooldown, TriggerGap);
            Defer(new Consequence { Kind = Later.Release, At = Now, Source = actor, Book = book, Origin = at, Facing = Vector2.UnitY, Reach = 0f, Strength = 1f, Quiet = trigger.On == TriggerEvent.Time });
        }

        /// <summary>What someone does just by being there, for those with a keystone that says so. Called every tick for those it could matter to.</summary>
        public void Presence(Actor actor, float seconds)
        {
            ActorSheet sheet = actor.Sheet;

            // What goes by the clock waits again whenever what it asks for stops holding, and what goes by the step only counts steps.
            Mod[] triggers = actor.Loadout.Triggers;
            for (int i = 0; i < triggers.Length; i++)
            {
                ref readonly Mod trigger = ref triggers[i];
                if (trigger.On == TriggerEvent.Time)
                {
                    if (!Holds(actor, trigger)) actor.TriggerLeft[i] = trigger.PerStep;
                    else if ((actor.TriggerLeft[i] -= seconds) <= 0f)
                    {
                        actor.TriggerLeft[i] = trigger.PerStep;
                        GoOff(actor, i, trigger, actor.Position);
                    }
                }
                else if (trigger.On == TriggerEvent.Walked && (actor.TriggerLeft[i] -= actor.Stepped) <= 0f)
                {
                    actor.TriggerLeft[i] = trigger.PerStep;
                    GoOff(actor, i, trigger, actor.Position);
                }
            }

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

        /// <summary>All of a buff goes at once.</summary>
        public void RemoveBuff(Actor actor, BuffDefinition buff)
        {
            if (actor.Buffs.RemoveAll(active => active.Buff == buff) > 0) actor.LoadoutChanged = true;
        }

        /// <summary>Something comes towards whoever draws it, by as much as it gives.</summary>
        public void Draw(Actor actor, float seconds)
        {
            _spread.Clear();
            Grid.Foes(actor.Team, actor.Position, LodestoneReach, _spread);
            foreach (Actor foe in _spread)
            {
                Vector2 toward = actor.Position - foe.Position;
                if (toward.LengthSquared < CloseReach * CloseReach || foe.Still || foe.Dead || foe.IsHero) continue;
                float give = (foe.Nature & Condition.Boss) != 0 ? BossGive : (foe.Nature & Condition.Rare) != 0 ? RareGive : 1f;
                foe.Push += toward.Normalized() * (LodestonePull * give * seconds * EnemyBrainSystem.PushFade);
            }
        }

        // ---- What it leaves behind.

        public void DropLoot(in EnemyComponent enemy, Vector3 position, Actor actor)
        {
            // A world holds a thousand of these. What's worth having comes from those that took some beating, and how much beating is in the health it really had.
            // What a land's omens add comes out of that, or they'd turn its fodder into champions and pay for them twice.
            LootBoon boon = _session.Boon;
            float health = Bestiary.HealthAt(enemy.Level) * _session.Terms.EnemyHealth * (actor.Tyrant ? _session.TyrantHealth : 1f);
            LootTier tier = LootTables.TierOf(enemy.Rarity, actor.Sheet.MaxHealth / health);

            // A trophied land's tyrant drops more, and a ledger's rares are worth twice what they'd be.
            if (actor.Tyrant) boon = boon with { Quantity = boon.Quantity * (1f + _session.Terms.ValueOf(Omens.Trophies) / 100f) };
            if (enemy.Rarity == Rarity.Rare && _session.Terms.Has(OrdersCatalog.Ledger)) boon = boon with { Quantity = boon.Quantity * OrdersCatalog.LedgerLoot };
            List<Item> items = Loot.Roll(tier, enemy.Level, StrongestHero, BestOfParty(s => s.ItemRarity) / 100f, _lootLuck, _luck.Fork(_luck.Int(1 << 30)), boon);

            // A tyrant always has its brood's orders on it, which is where the next land's come from.
            if (actor.Tyrant) items.Add(Loot.OrdersFrom(enemy.Level, _luck.Fork(_luck.Int(1 << 30)), boon));

            // What it was wearing is yours now. That's the whole point of having seen it used.
            if (actor.Gear != null) items.AddRange(actor.Gear);

            // Unless whoever killed it turns everything to gold.
            if (actor.Plundered) items.Clear();

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

    }
}

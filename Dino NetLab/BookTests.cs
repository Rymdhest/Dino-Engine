using Dino_Defenders.Books;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Server;
using Dino_Defenders.Stats;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Networking;
using Dino_Engine.World;
using OpenTK.Mathematics;
using System.Text.Json;
using static Dino_Defenders.Stats.M;

namespace Dino_NetLab
{
    /// <summary>
    /// What a book says, against what a running server does with it. A sentence that reads well and does something else is the worst thing this system could have, so everything here is a page written the way a
    /// player would write it, let off for real, and looked for in the world.
    /// </summary>
    internal static class BookTests
    {
        private static readonly Vector2 East = new Vector2(1f, 0f);
        private static readonly Vector2 West = new Vector2(-1f, 0f);
        private static readonly Vector2 North = new Vector2(0f, 1f);

        private static Outcome Deal(string element = Sentences.PlainElement) => new Outcome { Element = element };

        private static Page Flight(params string[] stickers)
        {
            Page page = Scribe.NewPage(PageKind.Projectile);
            page.Stickers.AddRange(stickers.Select(name => Stickers.Written(name)!));
            return page;
        }

        private static Page Area(string shape = "circle", params string[] stickers)
        {
            Page page = Scribe.NewPage(PageKind.Area);
            page.Shape = shape;
            page.Stickers.AddRange(stickers.Select(name => Stickers.Written(name)!));
            return page;
        }

        private static Page With(this Page page, params Outcome[] outcomes)
        {
            page.Outcomes = outcomes.ToList();
            return page;
        }

        private static Page Going(this Page page, string towards, string? within = null)
        {
            page.Towards = towards;
            page.TowardsWithin = within;
            return page;
        }

        private static Page Starting(this Page page, string from, string? within = null)
        {
            page.From = from;
            page.FromWithin = within;
            return page;
        }

        private static Page Lasting(this Page page, string timing)
        {
            page.Timing = timing;
            return page;
        }

        private static BookProfile Read(Arena arena, params Page[] pages)
        {
            var book = new Book { Pages = pages.ToList() };
            Scribe.Fix(book);
            return arena.Hero.Sheet.Preview(Scribe.Read(book, "Probe"));
        }

        private static void Cast(Arena arena, BookProfile book, Vector2 facing, float reach) => arena.Combat.Release(arena.Hero, book, arena.At, facing, reach);

        private static HeroState Hero()
        {
            HeroState hero = HeroState.CreateNew();
            hero.Level = 30;
            hero.Equipped[WornSlot.Neck] = new Item { Name = "Lab Charm", Slot = ItemSlot.Neck, Level = 30, Lines = { new ItemLine("health", 1000f), new ItemLine("mana", 400f) } };
            return hero;
        }

        private static int Count<T>(Arena arena) where T : struct, IComponent
        {
            int count = 0;
            foreach (Archetype archetype in arena.Lab.Server.Ecs.QueryArchetypes(new BitMask(typeof(T)), BitMask.Empty)) count += archetype.EntityCount;
            return count;
        }

        public static int Run(Options options)
        {
            var report = new Report();
            var request = new WorldRequest(options.Int("seed", 303), "isle");
            var worlds = new WorldCache();
            Console.WriteLine("Growing a world...");
            worlds.Get(request).Wait();

            report.Section("Where a page begins");
            Starts(report, worlds, request);

            report.Section("Where a projectile goes");
            Flights(report, worlds, request);

            report.Section("Turning the page");
            Turning(report, worlds, request);

            report.Section("Areas");
            Areas(report, worlds, request);

            report.Section("Outcomes");
            Outcomes(report, worlds, request);

            report.Section("Limits");
            Limits(report, worlds, request);

            report.Section("Weapons");
            Weapons(report, worlds, request);

            report.Section("Everything on the shelf works");
            Everything(report, worlds, request);

            report.Section("Writing, on both ends");
            Writing(report, worlds, request);
            Modes(report, worlds, request);

            return report.Finish();
        }

        private static void Starts(Report report, WorldCache worlds, WorldRequest request)
        {
            using (var arena = new Arena(worlds, request, Hero()))
            {
                Actor near = arena.Dummy(East * 5f);
                Actor far = arena.Dummy(East * 14f);
                BookProfile cursor = Read(arena, Area("circle", "tight").Starting("from-cursor"));
                Cast(arena, cursor, East, 14f);
                report.Check("From the cursor: it happens where it was aimed", Arena.Lost(far) > 0f);
                report.Check("And not where they're standing", Arena.Lost(near) == 0f);
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                Actor near = arena.Dummy(East * 8f);
                Actor far = arena.Dummy(West * 20f);
                BookProfile nearest = Read(arena, Area("circle", "tight").Starting("from-nearest"));
                Cast(arena, nearest, West, 20f);
                report.Check("From the nearest Enemy: wherever they were aiming", Arena.Lost(near) > 0f && Arena.Lost(far) == 0f, $"{Arena.Lost(near)} near, {Arena.Lost(far)} far");
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                Actor one = arena.Dummy(East * 12f);
                Actor other = arena.Dummy(West * 12f);
                BookProfile random = Read(arena, Area("circle", "tight").Starting("from-random"));
                for (int i = 0; i < 40 && (Arena.Lost(one) == 0f || Arena.Lost(other) == 0f); i++) Cast(arena, random, North, 2f);
                report.Check("From a random Enemy: any of them, in time", Arena.Lost(one) > 0f && Arena.Lost(other) > 0f);
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                float beyond = Rulebook.SeekReach + 6f;
                Actor aimed = arena.Dummy(East * beyond);
                BookProfile nearest = Read(arena, Area("circle", "tight").Starting("from-nearest"));
                Cast(arena, nearest, East, beyond);
                report.Check("With nobody in reach it falls back on where they aimed, rather than doing nothing", Arena.Lost(aimed) > 0f);
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                float beyond = Rulebook.SeekReach + 6f;
                Actor aimed = arena.Dummy(East * beyond);
                BookProfile nearest = Read(arena, Flight("far").Going("towards-nearest").With(Deal()));
                report.Check("A flight long enough to tell", nearest.First.Range > beyond, $"{nearest.First.Range} m");
                Cast(arena, nearest, East, beyond);
                arena.Run(1.6);
                report.Check("And a flight towards nobody goes where they aimed", Arena.Lost(aimed) > 0f);
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                Actor aimed = arena.Dummy(East * 12f);
                var hits = 0;
                BookProfile scattered = Read(arena, Area("circle", "tight").Starting("from-cursor", "within-far"));
                for (int i = 0; i < 30; i++)
                {
                    float before = Arena.Lost(aimed);
                    Cast(arena, scattered, East, 12f);
                    if (Arena.Lost(aimed) > before) hits++;
                }
                report.Check("Somewhere within a radius: some land there and some don't", hits is > 0 and < 30, $"{hits} of 30");
            }
        }

        private static void Flights(Report report, WorldCache worlds, WorldRequest request)
        {
            using (var arena = new Arena(worlds, request, Hero()))
            {
                Actor ahead = arena.Dummy(East * 20f);
                BookProfile plain = Read(arena, Flight().With(Deal()));
                Cast(arena, plain, East, 5f);
                report.Check("A projectile takes time", Arena.Lost(ahead) == 0f);
                arena.Run(1f);
                report.Check("And flies on past what it was aimed at", Arena.Lost(ahead) > 0f, $"aimed at 5 m, struck at 20 m");
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                Actor beyond = arena.Dummy(East * 20f);
                Actor burst = arena.Dummy(East * 7f + North * 3f);
                BookProfile stopping = Read(arena, Flight("stops").With(new Outcome { Deed = "pass" }), Area("circle", "tight").With(Deal()));
                Cast(arena, stopping, East, 7f);
                arena.Run(1.2);
                report.Check("Stopping at its target: it goes no further", Arena.Lost(beyond) == 0f);
                report.Check("And has its impact there, with nobody to take it", Arena.Lost(burst) > 0f);
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                Actor target = arena.Dummy(West * 10f);
                Actor further = arena.Dummy(East * 16f);
                BookProfile seeking = Read(arena, Flight().Going("towards-nearest").With(Deal()));
                Cast(arena, seeking, North, 3f);
                arena.Run(1f);
                report.Check("Towards the nearest Enemy: whichever way they were aiming", Arena.Lost(target) > 0f && Arena.Lost(further) == 0f, $"{Arena.Lost(target)}, {Arena.Lost(further)}");
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                int flying = 0;
                Actor anyone = arena.Dummy(East * 15f);
                BookProfile wild = Read(arena, Flight("three-at-once").Going("anywhere").With(Deal()));
                var ways = new HashSet<int>();
                Cast(arena, wild, East, 10f);
                arena.Run(0.05);
                foreach (Archetype archetype in arena.Lab.Server.Ecs.QueryArchetypes(new BitMask(typeof(BallisticComponent)), BitMask.Empty))
                {
                    foreach (EntityView entity in new ComponentAccessor(archetype))
                    {
                        Vector3 velocity = entity.Get<BallisticComponent>().Velocity;
                        ways.Add((int)(MathF.Atan2(velocity.X, velocity.Z) * 8f));
                        flying++;
                    }
                }
                report.Check("Three at once are three", flying == 3, $"{flying}");
                report.Check("And in a random direction, each its own", ways.Count >= 2, $"{ways.Count}");
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                Actor under = arena.Dummy(East * 10f);
                BookProfile falling = Read(arena, Flight().Starting("from-cursor").Going("downwards").With(Deal()));
                Cast(arena, falling, East, 10f);
                report.Check("Straight down: nothing yet", Arena.Lost(under) == 0f);
                arena.Run(falling.Delay * 0.5);
                report.Check("Nor half way", Arena.Lost(under) == 0f);
                arena.Run(falling.Delay * 0.5 + 0.1);
                report.Check("And then it lands on whatever's under it", Arena.Lost(under) > 0f);
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                Actor wall = arena.Dummy(East * 8f);
                BookProfile falling = Read(arena, Flight().Starting("from-cursor").Going("downwards").With(Deal()));
                Cast(arena, falling, East, 20f);
                arena.Run(falling.Delay + 0.2);
                report.Check("And nothing it passes over on the way", Arena.Lost(wall) == 0f);
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                Actor first = arena.Dummy(East * 6f);
                Actor second = arena.Dummy(East * 10f);
                Actor third = arena.Dummy(East * 14f);
                BookProfile through = Read(arena, Flight("pierce-one").With(Deal()));
                Cast(arena, through, East, 20f);
                arena.Run(1f);
                report.Check("Passing through one Enemy: two struck", Arena.Lost(first) > 0f && Arena.Lost(second) > 0f && Arena.Lost(third) == 0f);
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                var line = new[] { arena.Dummy(East * 6f), arena.Dummy(East * 12f), arena.Dummy(East * 18f) };
                BookProfile leaping = Read(arena, Flight("chain-one").With(Deal()));
                Cast(arena, leaping, East, 20f);
                arena.Run(1.2);
                report.Check("Leaping to one more Enemy: two struck, and no more", line.Count(a => Arena.Lost(a) > 0f) == 2, $"{line.Count(a => Arena.Lost(a) > 0f)}");
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                BookProfile quick = Read(arena, Flight("swift").With(Deal()));
                BookProfile slow = Read(arena, Flight("slow").With(Deal()));
                report.Check("How fast it goes is what the sentence says", quick.First.Speed > Rulebook.ProjectileSpeed && slow.First.Speed < Rulebook.ProjectileSpeed);
                Actor target = arena.Dummy(East * 24f);
                Cast(arena, quick, East, 24f);
                arena.Run(24f / quick.First.Speed + 0.1);
                bool arrived = Arena.Lost(target) > 0f;
                target.Vitals.Health = target.Sheet.MaxHealth;
                Cast(arena, slow, East, 24f);
                arena.Run(24f / quick.First.Speed + 0.1);
                report.Check("And that's what it does", arrived && Arena.Lost(target) == 0f);
            }
        }

        private static void Turning(Report report, WorldCache worlds, WorldRequest request)
        {
            using (var arena = new Arena(worlds, request, Hero()))
            {
                Actor target = arena.Dummy(East * 12f);
                Actor beside = arena.Dummy(East * 12f + North * 4f);
                Actor away = arena.Dummy(West * 12f);
                BookProfile fireball = Read(arena, Flight().With(new Outcome { Deed = "pass" }), Area("circle").With(Deal("fire")));
                Cast(arena, fireball, East, 12f);
                arena.Run(0.7);
                report.Check("A page follows from where the last struck", Arena.Lost(target) > 0f && Arena.Lost(beside) > 0f);
                report.Check("And nowhere else", Arena.Lost(away) == 0f);
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                Actor behind = arena.Dummy(West * 6f);
                BookProfile returning = Read(arena, Flight("stops").With(Deal()), Flight().Going("towards-you").With(Deal()));
                Cast(arena, returning, East, 10f);
                arena.Run(0.2);
                report.Check("Going out: not on its way back yet", Arena.Lost(behind) == 0f);
                arena.Run(1f);
                report.Check("And then coming back, past them and on", Arena.Lost(behind) > 0f, $"{Arena.Lost(behind)}");
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                Actor out_ = arena.Dummy(East * 10f);
                Actor behind = arena.Dummy(West * 6f);
                BookProfile returning = Read(arena, Flight().Going("towards-nearest").With(Deal()), Flight().Going("towards-you").With(Deal()));
                Cast(arena, returning, East, 10f);
                arena.Run(1.2);
                float once = Arena.Lost(out_);
                report.Check("To an Enemy and back: what it came off isn't struck again by leaving it", once > 0f && Arena.Lost(behind) > 0f, $"{once}, {Arena.Lost(behind)}");
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                var line = new[] { arena.Dummy(East * 6f), arena.Dummy(East * 12f), arena.Dummy(East * 18f), arena.Dummy(West * 25f) };
                BookProfile chain = Read(arena, Flight().Going("towards-nearest").With(Deal("lightning")), Flight().Going("towards-nearest").With(Deal("lightning")), Flight().Going("towards-nearest").With(Deal("lightning")));
                Cast(arena, chain, East, 6f);
                arena.Run(2f);
                int struck = line.Take(3).Count(a => Arena.Lost(a) > 0f);
                report.Check("Going to the nearest each time: nobody twice, so it goes on to someone new", struck == 3, $"{struck} of 3");
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                Actor first = arena.Dummy(East * 6f);
                Actor corner = arena.Dummy(East * 6f - North * 8f);
                Actor last = arena.Dummy(West * 3f - North * 8f);
                Page Seeking() => Flight().Going("towards-nearest").With(Deal());
                Cast(arena, Read(arena, Seeking(), Seeking(), Seeking()), East, 6f);
                arena.Run(2f);
                bool open = Enumerable.Range(0, 10).All(metre => !arena.Session.World.Placement.OverlapsSolid(Footprint.Circle(arena.At + new Vector2(6f - metre, -8f), 0.3f)));
                report.Check("With nothing standing in the way of that", open);
                report.Check("Round a corner, past someone it's been to that's nearer than anyone it hasn't", Arena.Lost(corner) > 0f && Arena.Lost(last) > 0f, $"{Arena.Lost(first)}, {Arena.Lost(corner)}, {Arena.Lost(last)}");
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                BookProfile missing = Read(arena, Flight("short").With(Deal()), Area("circle", "tight").With(Deal()));
                Actor end = arena.Dummy(East * (missing.First.Range + 1f));
                Cast(arena, missing, East, 30f);
                arena.Run(1.2);
                report.Check("Reaching the end of its Range is an impact, so the page still turns", Arena.Lost(end) > 0f, $"{missing.First.Range} m");
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                Actor third = arena.Dummy(East * 20f);
                Actor target = arena.Dummy(East * 10f);
                BookProfile onward = Read(arena, Flight().With(new Outcome { Deed = "pass" }), Flight().Going("onward").With(Deal()));
                Cast(arena, onward, East, 10f);
                arena.Run(1.2);
                report.Check("The way it was going: on from where it struck", Arena.Lost(third) > 0f && Arena.Lost(target) == 0f, $"{Arena.Lost(third)}, {Arena.Lost(target)}");
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                Actor target = arena.Dummy(East * 10f);
                BookProfile passing = Read(arena, Flight().With(new Outcome { Deed = "pass" }));
                Cast(arena, passing, East, 10f);
                arena.Run(0.6);
                report.Check("Doing nothing but turning the page does nothing to who it reaches", Arena.Lost(target) == 0f);
                report.Check("And doesn't fly on through them either", Count<ProjectileComponent>(arena) == 0);
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                Actor target = arena.Dummy(East * 8f);
                BookProfile last = Read(arena, Flight().With(Deal()));
                Cast(arena, last, East, 8f);
                arena.Run(0.5);
                report.Check("A last page has nothing to turn to, and nothing comes of that", Arena.Lost(target) > 0f);
            }
        }

        private static void Areas(Report report, WorldCache worlds, WorldRequest request)
        {
            using (var arena = new Arena(worlds, request, Hero()))
            {
                Actor ahead = arena.Dummy(East * 4f);
                Actor behind = arena.Dummy(West * 4f);
                Cast(arena, Read(arena, Area("cone")), East, 4f);
                report.Check("A cone: what's in front", Arena.Lost(ahead) > 0f && Arena.Lost(behind) == 0f);
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                Actor ahead = arena.Dummy(East * 4f);
                Actor behind = arena.Dummy(West * 4f);
                Actor outside = arena.Dummy(West * 15f);
                Cast(arena, Read(arena, Area("circle")), East, 4f);
                report.Check("A circle: all around", Arena.Lost(ahead) > 0f && Arena.Lost(behind) > 0f && Arena.Lost(outside) == 0f);
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                Actor along = arena.Dummy(East * 18f);
                Actor aside = arena.Dummy(East * 10f + North * 6f);
                Cast(arena, Read(arena, Area("line")), East, 10f);
                report.Check("A line: a long way, and narrow", Arena.Lost(along) > 0f && Arena.Lost(aside) == 0f);
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                Actor aside = arena.Dummy(East * 8f + North * 3.5f);
                Actor beyond = arena.Dummy(East * 18f);
                Cast(arena, Read(arena, Area("rectangle")), East, 8f);
                report.Check("A rectangle: wider than a line and shorter", Arena.Lost(aside) > 0f && Arena.Lost(beyond) == 0f);
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                Actor before = arena.Dummy(East * 11f);
                Actor after = arena.Dummy(East * 19f);
                Cast(arena, Read(arena, Area("rectangle").Starting("from-cursor")), East, 15f);
                report.Check("Away from them, it's centred on where it starts", Arena.Lost(before) > 0f && Arena.Lost(after) > 0f);
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                Actor inside = arena.Dummy(East * 4f);
                BookProfile lingering = Read(arena, Area("circle").Lasting("lingering").With(Deal()));
                Cast(arena, lingering, East, 4f);
                report.Check("Lingering: nothing all at once", Arena.Lost(inside) == 0f);
                arena.Run(lingering.First.Duration + 0.6);
                float all = Arena.Lost(inside);

                Actor once = arena.Dummy(West * 4f);
                float plain = 0f;
                BookProfile direct = Read(arena, Area("circle").With(Deal()));
                for (int i = 0; i < 50; i++)
                {
                    float before = Arena.Lost(once);
                    Cast(arena, direct, West, 4f);
                    plain += Arena.Lost(once) - before;
                }
                plain /= 50f;
                report.Check("And over its Duration it comes to what it says, and its premium", all > plain * Rulebook.LingerPremium * 0.6f && all < plain * Rulebook.LingerPremium * 1.6f, $"{all} against {plain} at once");
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                Actor inside = arena.Dummy(East * 15f);
                Actor outside = arena.Dummy(East * 15f + North * 8f);
                BookProfile line = Read(arena, Area("line").Lasting("lingering").With(Deal()));
                Cast(arena, line, East, 15f);
                arena.Run(1f);
                report.Check("And in whatever shape it's in", Arena.Lost(inside) > 0f && Arena.Lost(outside) == 0f);
            }
        }

        private static void Outcomes(Report report, WorldCache worlds, WorldRequest request)
        {
            using (var arena = new Arena(worlds, request, Hero()))
            {
                Actor target = arena.Dummy(East * 4f);
                Cast(arena, Read(arena, Area().With(new Outcome { Element = "fire", Measure = "of-health" })), East, 4f);
                report.Check("Deal Damage from anything: a share of Maximum Health", Arena.Lost(target) > arena.Hero.Sheet.MaxHealth * 0.03f);
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                arena.Hero.Vitals.Health = 100f;
                float heal = arena.Hero.Sheet.MaxMana * 0.1f;
                Cast(arena, Read(arena, Area().With(new Outcome { Deed = "heal", Measure = "of-mana" })), East, 4f);
                report.Near("Heal yourself for a share of your Maximum Mana", arena.Hero.Vitals.Health - 100f, heal * arena.Hero.Sheet.RecoveryScale, 1f);
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                arena.Hero.Vitals.Mana = 10f;
                Cast(arena, Read(arena, Area().With(new Outcome { Deed = "heal", Pool = "mana", Measure = "of-health" })), East, 4f);
                report.Check("Mana can be what's restored", arena.Hero.Vitals.Mana > 10f);
            }

            using (var arena = new Arena(worlds, request, Hero(), heroes: 2))
            {
                Actor friend = arena.Lab.Server.Heroes[1].Actor;
                friend.Vitals.Health = 50f;
                arena.Hero.Vitals.Health = 50f;
                Vector2 toward = friend.Position - arena.At;
                BookProfile mending = Read(arena, Area("circle").Starting("from-cursor").With(new Outcome { Deed = "heal", Whom = "friends", Measure = "of-health" }));
                arena.Combat.Release(arena.Hero, mending, arena.At, toward.Normalized(), toward.Length);
                report.Check("Friends there: a friend in it is healed", friend.Vitals.Health > 50f, $"{friend.Vitals.Health}");
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                Actor foe = arena.Dummy(East * 8f);
                BookProfile friendly = Read(arena, Flight().With(new Outcome { Deed = "heal", Whom = "friends", Measure = "of-health" }));
                Actor beyond = arena.Dummy(East * 8.3f);
                report.Check("What only helps friends stops at friends", friendly.First.Page.SeeksFriends);
                Cast(arena, friendly, East, 8f);
                arena.Run(0.4);
                report.Check("And flies straight through an Enemy", Arena.Lost(foe) == 0f && Count<ProjectileComponent>(arena) > 0);
            }

            foreach ((string id, Func<Actor, bool> has) in new (string, Func<Actor, bool>)[]
            {
                ("burn", a => a.Afflictions.BurnLeft > 0f), ("bleed", a => a.Afflictions.BleedLeft > 0f), ("venom", a => a.Afflictions.Venom is { Count: > 0 }), ("chill", a => a.Afflictions.ChillLeft > 0f),
                ("freeze", a => a.Afflictions.FreezeLeft > 0f), ("shock", a => a.Afflictions.ShockLeft > 0f), ("hex", a => a.Afflictions.HexLeft > 0f), ("stun", a => a.Afflictions.StunLeft > 0f),
            })
            {
                using var arena = new Arena(worlds, request, Hero());
                Actor target = arena.Dummy(East * 4f);
                Cast(arena, Read(arena, Area().With(new Outcome { Deed = "afflict", Ailment = id })), East, 4f);
                report.Check($"Inflicting {id}: it's there, with no chance about it", has(target));
                report.Check("  And no Damage came with it", Arena.Lost(target) == 0f, $"{Arena.Lost(target)}");
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                Cast(arena, Read(arena, Area().With(new Outcome { Deed = "give", Gift = "quickening" })), East, 4f);
                arena.Run(0.1);
                report.Check("Give a Buff", arena.Hero.Buffs.Any(buff => buff.Buff == BuffCatalog.Quickening));
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                Cast(arena, Read(arena, Area().Starting("from-cursor").With(new Outcome { Deed = "raise", Kin = "bones" })), East, 10f);
                arena.Run(0.2);
                report.Check("Raise a Minion, where it happens", arena.Session.Minions.Count == 2 && (arena.Session.Minions[0].Position - (arena.At + East * 10f)).Length < 6f, $"{arena.Session.Minions.Count}");
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                Vector2 before = arena.At;
                Cast(arena, Read(arena, Area().Starting("from-cursor").With(new Outcome { Deed = "move" })), East, 15f);
                arena.Run(0.3);
                float moved = (arena.Truth.Sim.Position - before).Length;
                report.Check("Move you there", moved is > 10f and < 20f, $"{moved} m");
                report.Check("And the client believes it at once", (arena.Player.Client.Predictor.Current.Position - arena.Truth.Sim.Position).Length < 1f);
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                Vector2 at = arena.At + East * 8f;
                Cast(arena, Read(arena, Area().Starting("from-cursor").With(new Outcome { Deed = "water" })), East, 8f);
                report.Check("Make Water there", arena.Combat.Land.InWater(at));
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                Actor pushed = arena.Dummy(East * 4f);
                pushed.Still = false;
                Cast(arena, Read(arena, Area().With(new Outcome { Deed = "push" })), East, 4f);
                report.Check("Throw them back: away from what did it", Vector2.Dot(pushed.Push, East) > 0f, $"{pushed.Push}");

                pushed.Push = Vector2.Zero;
                Cast(arena, Read(arena, Area().With(new Outcome { Deed = "pull" })), East, 4f);
                report.Check("Drag them in: towards it", Vector2.Dot(pushed.Push, East) < 0f, $"{pushed.Push}");
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                Actor target = arena.Dummy(East * 4f);
                arena.Hero.Vitals.Health = 100f;
                Cast(arena, Read(arena, Area().With(Deal("cold"), new Outcome { Deed = "heal", Measure = "of-health" }, new Outcome { Deed = "afflict", Ailment = "burn" })), East, 4f);
                report.Check("Several Outcomes all happen", Arena.Lost(target) > 0f && arena.Hero.Vitals.Health > 100f && target.Afflictions.BurnLeft > 0f);
            }
        }

        private static void Limits(Report report, WorldCache worlds, WorldRequest request)
        {
            using (var arena = new Arena(worlds, request, Hero()))
            {
                for (int i = 0; i < 16; i++) arena.Dummy(East * 8f + North * (i - 8f) * 2f);

                // As many as can be fitted on, at every page of the longest book there is. It would be five to the sixth without a limit.
                var pages = new List<Page>();
                for (int i = 0; i < Rulebook.MostPages; i++) pages.Add(Flight("five-at-once", "pierce-three").Going("anywhere").With(Deal()));
                BookProfile branching = Read(arena, pages.ToArray());
                int most = 0;
                Cast(arena, branching, East, 8f);
                arena.Lab.Run(6.0, () => most = Math.Max(most, Count<ProjectileComponent>(arena)));
                report.Check($"However a book branches, one use comes to {Rulebook.MostMade} at most", most <= Rulebook.MostMade, $"{most} at once");
                report.Check("And it's over when it's over", Count<ProjectileComponent>(arena) == 0);
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                BookProfile zone = Read(arena, Area("circle").Lasting("lingering-long").With(Deal()));
                for (int i = 0; i < Rulebook.MostZones + 4; i++)
                {
                    Cast(arena, zone, East, 4f);
                    arena.Run(0.2);
                }
                report.Check($"Nobody keeps more than {Rulebook.MostZones} lingering at once, the oldest going first", Count<ZoneStateComponent>(arena) <= Rulebook.MostZones + 1, $"{Count<ZoneStateComponent>(arena)}");
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                BookProfile bones = Read(arena, Flight("five-at-once").With(new Outcome { Deed = "raise", Kin = "bones" }));
                for (int i = 0; i < 4; i++) Cast(arena, bones, East, 4f);
                arena.Run(1.5);
                report.Check($"Nor more than {Rulebook.MostMinions} minions of a kind", arena.Session.Minions.Count(m => !m.Dead) <= Rulebook.MostMinions, $"{arena.Session.Minions.Count(m => !m.Dead)}");
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                BookPlan bones = Scribe.Read(new Book { Pages = { Flight().With(new Outcome { Deed = "raise", Kin = "bones" }) } }, "Crowd");
                var crowded = new Loadout(30, arena.Hero.Loadout.Weapon, new[] { Flat(Stat.MinionCount, 40f) }, Array.Empty<BookPlan>()).SheetFor(Condition.None).Preview(bones);
                report.Check("A build can ask for more than that", crowded.First.MinionCount > Rulebook.MostMinions, $"{crowded.First.MinionCount}");
                arena.Session.Summon(arena.Hero, crowded.First, arena.At);
                report.Check("And doesn't get them", arena.Session.Minions.Count(m => !m.Dead) == Rulebook.MostMinions, $"{arena.Session.Minions.Count(m => !m.Dead)}");
            }
        }

        private static void Weapons(Report report, WorldCache worlds, WorldRequest request)
        {
            HeroState archer = Hero();
            archer.Equipped[WornSlot.MainHand] = new Item { Name = "Lab Bow", Slot = ItemSlot.MainHand, Level = 30, Kind = WeaponKind.Bow, WeaponDamage = 40f };
            HeroState mage = Hero();
            mage.Equipped[WornSlot.MainHand] = new Item { Name = "Lab Wand", Slot = ItemSlot.MainHand, Level = 30, Kind = WeaponKind.Wand, WeaponDamage = 40f };
            HeroState staff = Hero();
            staff.Equipped[WornSlot.MainHand] = new Item { Name = "Lab Staff", Slot = ItemSlot.MainHand, Level = 30, Kind = WeaponKind.Staff, WeaponDamage = 40f };

            string ModelOf(HeroState hero, out bool thrown)
            {
                using var arena = new Arena(worlds, request, hero);
                Cast(arena, Read(arena, Flight().With(Deal("fire"))), East, 10f);
                arena.Run(0.05);
                string model = "";
                bool real = false;
                foreach (Archetype archetype in arena.Lab.Server.Ecs.QueryArchetypes(new BitMask(typeof(ProjectileComponent), typeof(VisualComponent)), BitMask.Empty))
                {
                    foreach (EntityView entity in new ComponentAccessor(archetype))
                    {
                        model = entity.Get<VisualComponent>().Model;
                        real = entity.Has<ThrownComponent>();
                    }
                }
                thrown = real;
                return model;
            }

            report.Check("A bow makes it an arrow", ModelOf(archer, out _) == "arrow");
            report.Check("A wand makes it a ball of magic, of what it deals", ModelOf(mage, out _) == "bolt:Fire");
            report.Check("And so does a staff, for now", ModelOf(staff, out _) == "bolt:Fire");
            report.Check("Anything else is thrown", ModelOf(Hero(), out bool thrown).StartsWith(Thrown.Prefix), ModelOf(Hero(), out _));
            report.Check("And it's what they're really holding", thrown);

            using (var arena = new Arena(worlds, request, mage))
            {
                Actor target = arena.Dummy(East * 4f, 1000000f, Flat(Stat.Evasion, 100000f));
                float hits = 0f;
                for (int i = 0; i < 40; i++)
                {
                    float before = Arena.Lost(target);
                    Cast(arena, Read(arena, Area("cone")), East, 4f);
                    if (Arena.Lost(target) > before) hits++;
                }
                report.Check("A Spell can't be Evaded", hits == 40f, $"{hits} of 40");
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                Actor target = arena.Dummy(East * 4f, 1000000f, Flat(Stat.Evasion, 100000f));
                float hits = 0f;
                for (int i = 0; i < 40; i++)
                {
                    float before = Arena.Lost(target);
                    Cast(arena, Read(arena, Area("cone")), East, 4f);
                    if (Arena.Lost(target) > before) hits++;
                }
                report.Check("And the same page with a sword can", hits < 40f, $"{hits} of 40");
            }
        }

        // Everything written comes to something when it's let go, with nothing thrown and something to show for it.
        private static void Everything(Report report, WorldCache worlds, WorldRequest request)
        {
            using var arena = new Arena(worlds, request, Hero());
            var broken = new List<string>();
            foreach (BookPlan plan in Shelf.All)
            {
                Actor target = arena.Dummy(East * 6f);
                try
                {
                    Cast(arena, arena.Hero.Sheet.Preview(plan), East, 6f);
                    arena.Run(1.4);
                }
                catch (Exception e)
                {
                    broken.Add($"{plan.Id}: {e.GetType().Name}");
                }
                arena.Combat.Hit(arena.Hero, arena.Page(Shelf.Strike), target, 100000000f, arena.At);
            }
            report.Check($"All {Shelf.All.Count} written books can be used", broken.Count == 0, string.Join(", ", broken));
        }

        private static string Text(Item? item) => item == null ? "" : JsonSerializer.Serialize(item);

        private static void Press(Arena arena, int slot)
        {
            arena.Player.Script = () => { var press = new HeroIntent { Aim = East * 6f }; press.Use(slot); return press; };
            arena.Run(0.06);
            arena.Player.Script = () => new HeroIntent { Aim = East * 6f };
            arena.Run(0.3);
        }

        // Books that nobody presses, as what happens on a real server.
        private static void Modes(Report report, WorldCache worlds, WorldRequest request)
        {
            HeroState aura = Hero();
            aura.Bar[1] = ItemGenerator.Copied(Shelf.Inferno.Source!, "Inferno", 4);
            aura.Bar[2] = ItemGenerator.Copied(Shelf.Inferno.Source!, "Inferno Too", 5);
            aura.Equipped[WornSlot.Finger1] = new Item { Name = "Lab Ring", Slot = ItemSlot.Finger, Level = 30, Lines = { new ItemLine("mana-regen", 10f) } };
            using (var arena = new Arena(worlds, request, aura))
            {
                arena.Run(0.5);
                BookProfile book = arena.Truth.Kit.Slots[1]!;
                float regen = arena.Hero.Sheet.ManaRegen;
                Actor target = arena.Dummy(East * 3f);
                float before = target.Vitals.Health;
                arena.Run(1.5);
                report.Check("A Toggle Book does nothing while it's off", target.Vitals.Health == before);

                arena.Hero.Vitals.Mana = 50f;
                arena.Run(0.1);
                float mana = arena.Truth.Sim.Mana;
                arena.Run(2);
                report.Near("And costs nothing", (arena.Truth.Sim.Mana - mana) / 2f, regen, 0.3f);

                Press(arena, 1);
                report.Check("A press turns it on, on the server", HeroRules.IsOn(arena.Truth.Sim, 1));
                report.Check("And on the client", HeroRules.IsOn(arena.Player.Client.Predictor.Current, 1));
                report.Check("Using nothing, and taking no Mana by the press", arena.Truth.Sim.Using < 0);

                arena.Hero.Vitals.Mana = 50f;
                arena.Run(0.1);
                mana = arena.Truth.Sim.Mana;
                float health = target.Vitals.Health;
                arena.Run(3);
                report.Near("While it's on, its Cost comes off Mana Regeneration", (arena.Truth.Sim.Mana - mana) / 3f, regen - book.Cost, 0.3f);
                report.Check("And it goes on by itself, with nobody pressing anything", target.Vitals.Health < health);
                int corrected = arena.Player.Client.Predictor.Corrections;
                arena.Run(2);
                report.Check("Which the client predicts", arena.Player.Client.Predictor.Corrections == corrected, $"{arena.Player.Client.Predictor.Corrections - corrected}");

                float byHand = target.Vitals.Health;
                arena.Combat.Release(arena.Hero, arena.Skill(Shelf.Inferno), arena.At, East, 0f);
                arena.Run(0.05);
                float handHit = byHand - target.Vitals.Health;
                float beforePulse = target.Vitals.Health;
                arena.Combat.Pulse(arena.Hero, book, arena.At, East, 0f);
                arena.Run(0.05);
                float pulseHit = beforePulse - target.Vitals.Health;
                report.Check("A pulse does a share of what the same page does by hand", pulseHit < handHit * 0.8f && pulseHit > 0f, $"{pulseHit} of {handHit}");

                arena.Player.Script = () => { var hold = new HeroIntent { Aim = East * 6f }; hold.Use(1); return hold; };
                arena.Run(1);
                arena.Player.Script = () => new HeroIntent { Aim = East * 6f };
                arena.Run(0.2);
                report.Check("A press is a press: held down it doesn't flicker, and ends up off", !HeroRules.IsOn(arena.Truth.Sim, 1));

                health = target.Vitals.Health;
                arena.Run(1.5);
                report.Check("Off again, it stops", target.Vitals.Health == health);

                Press(arena, 1);
                Press(arena, 2);
                report.Check("Two can be on at once, if there's enough to give up", HeroRules.IsOn(arena.Truth.Sim, 1) && HeroRules.IsOn(arena.Truth.Sim, 2), $"{arena.Truth.Sim.Toggled}");
                arena.Player.Client.Unequip(WornSlot.Finger1);
                arena.Run(0.5);
                report.Check("Taking off what paid for them turns off the last, until what's left can be kept up", HeroRules.IsOn(arena.Truth.Sim, 1) && !HeroRules.IsOn(arena.Truth.Sim, 2), $"{arena.Truth.Sim.Toggled}");
                report.Check("And the client agrees", arena.Player.Client.Predictor.Current.Toggled == arena.Truth.Sim.Toggled);
                arena.Player.Client.Move(ItemPlace.OnBar(1), ItemPlace.Cell(7));
                arena.Run(0.5);
                report.Check("Taking one off the bar turns it off", !HeroRules.IsOn(arena.Truth.Sim, 1) && !HeroRules.IsOn(arena.Player.Client.Predictor.Current, 1));
            }

            HeroState poor = HeroState.CreateNew();
            poor.Level = 30;
            var dear = new Book { Mode = BookMode.Toggle, Pages = { Area("circle").With(Deal("fire"), new Outcome { Deed = "afflict", Ailment = "burn" }) } };
            poor.Bar[1] = ItemGenerator.Copied(dear, "Too Dear", 4);
            using (var arena = new Arena(worlds, request, poor))
            {
                arena.Run(0.5);
                report.Check("A book that costs what can be regenerated or more", arena.Truth.Kit.Slots[1]!.Cost >= arena.Hero.Sheet.ManaRegen, $"{arena.Truth.Kit.Slots[1]!.Cost} against {arena.Hero.Sheet.ManaRegen}");
                Actor target = arena.Dummy(East * 3f);
                float health = target.Vitals.Health;
                for (int i = 0; i < 3; i++) Press(arena, 1);
                report.Check("Can't be turned on", !HeroRules.IsOn(arena.Truth.Sim, 1) && !HeroRules.IsOn(arena.Player.Client.Predictor.Current, 1));
                report.Check("Not even for a moment, or pressing it over and over would be free", target.Vitals.Health == health, $"{health - target.Vitals.Health}");
            }

            HeroState moving = Hero();
            moving.Bar[1] = ItemGenerator.Copied(Shelf.Retort.Source!, "Retort", 4);
            using (var arena = new Arena(worlds, request, moving))
            {
                arena.Run(0.5);
                float mana = arena.Truth.Sim.Mana;
                Press(arena, 1);
                report.Check("A Trigger Book can't be pressed", arena.Truth.Sim.Using < 0 && arena.Truth.Sim.Mana >= mana);

                BookWatch watch = arena.Hero.Watches[0];
                report.Check("It knows what it's waiting for", watch.When == BookWhen.Moved && watch.Amount == 20f);
                arena.Player.Script = () => new HeroIntent { Move = East, Aim = East };
                arena.Run(0.8);
                arena.Player.Script = () => new HeroIntent { Aim = East };
                arena.Run(0.2);
                report.Check("Moving counts towards it", watch.Progress > 3f && watch.Progress < 20f, $"{watch.Progress}");
                report.Check("And the client sees how far it's got", arena.Player.Client.Predictor.Current.Charged[1] > 0.1f, $"{arena.Player.Client.Predictor.Current.Charged[1]}");

                arena.Hero.Vitals.Mana = 0f;
                watch.Progress = 20f;
                arena.Run(0.02);
                report.Check("With nothing to pay with, it waits", watch.Progress == 20f);

                Actor target = arena.Dummy(East * 3f);
                float health = target.Vitals.Health;
                arena.Run(0.1);
                arena.Hero.Vitals.Mana = 200f;
                arena.Run(0.3);
                report.Check("And goes the moment there is", watch.Progress < 20f && target.Vitals.Health < health, $"{watch.Progress}");
                report.Check("Paying its Cost", arena.Truth.Sim.Mana < 200f + arena.Hero.Sheet.ManaRegen * 0.3f - arena.Truth.Kit.Slots[1]!.Cost + 0.1f, $"{arena.Truth.Sim.Mana}, {arena.Hero.Sheet.ManaRegen}, {arena.Truth.Kit.Slots[1]!.Cost}");
            }

            foreach ((BookWhen when, Action<Arena> happen, string words) in new (BookWhen, Action<Arena>, string)[]
            {
                (BookWhen.Block, arena => arena.Combat.SetOff(arena.Hero, TriggerEvent.Block, arena.At), "a Block"),
                (BookWhen.Kill, arena => arena.Combat.SetOff(arena.Hero, TriggerEvent.Kill, arena.At), "a Kill"),
                (BookWhen.Struck, arena => arena.Combat.SetOff(arena.Hero, TriggerEvent.Struck, arena.At), "being Hit"),
                (BookWhen.Damaged, arena => arena.Combat.Harm(arena.Dummy(East * 30f), arena.Hero, arena.Hero.Sheet.MaxHealth * 0.1f, DamageType.Physical), "Damage"),
            })
            {
                HeroState hero = Hero();
                var trigger = new Book { Mode = BookMode.Trigger, When = new BookTrigger { When = when, Amount = when == BookWhen.Damaged ? 15f : 3f }, Pages = { Area("circle").With(Deal("cold")) } };
                hero.Bar[2] = ItemGenerator.Copied(trigger, "Waiter", 4);
                using var arena = new Arena(worlds, request, hero);
                arena.Run(0.3);
                happen(arena);
                report.Check($"{words} counts towards what's waiting for it", arena.Hero.Watches[0].Progress > 0f, $"{arena.Hero.Watches[0].Progress}");
            }

            HeroState spending = Hero();
            spending.Bar[1] = ItemGenerator.Copied(new Book { Mode = BookMode.Trigger, When = new BookTrigger { When = BookWhen.Used, Amount = 2f }, Pages = { Area("circle").With(Deal("cold")) } }, "Echo", 4);
            using (var arena = new Arena(worlds, request, spending))
            {
                arena.Run(0.3);
                arena.Player.Script = () => { var press = new HeroIntent { Aim = East * 6f }; press.Use(0); return press; };
                arena.Run(0.2);
                arena.Player.Script = () => new HeroIntent { Aim = East * 6f };
                arena.Run(0.2);
                report.Check("A Book used by hand counts towards one waiting for that", arena.Hero.Watches[0].Progress >= 1f, $"{arena.Hero.Watches[0].Progress}");
                report.Check("And what goes off by itself doesn't count, or it would never stop", arena.Hero.Watches[0].Progress <= 1f);
            }
        }

        // A hero with something to put in a book, as nobody can write one out of nothing now.
        private static HeroState Stocked()
        {
            HeroState hero = Hero();
            hero.Pickup(new Item { Name = "Leaf", Slot = ItemSlot.Page, Level = 1, Seed = 3, Page = Scribe.NewPage(PageKind.Projectile), Held = new Item?[2] }, 0);
            hero.Pickup(ItemGenerator.StickerItem(Stickers.Written("two-at-once")!, 5, 1, Rarity.Normal), 1);
            hero.Pickup(new Item { Name = "Empty Book", Slot = ItemSlot.Book, Level = 1, Seed = 6, Held = new Item?[1] }, 2);
            hero.Pickup(new Item { Name = "Spare Leaf", Slot = ItemSlot.Page, Level = 1, Seed = 8, Page = Scribe.NewPage(PageKind.Projectile), Held = new Item?[1] }, 3);
            return hero;
        }

        private static void Writing(Report report, WorldCache worlds, WorldRequest request)
        {
            foreach ((string name, NetworkConditions line) in Scenarios.All)
            {
                if (name is not ("Good broadband" or "Awful")) continue;
                using var arena = new Arena(worlds, request, Stocked(), line: line);
                var player = arena.Player.Client;
                HeroState client = player.Hero;
                HeroState truth = arena.Truth.Hero;
                ItemPlace book = ItemPlace.OnBar(0);

                player.Move(book.In(0), book.In(1));
                player.Move(ItemPlace.Cell(0), book.In(0));
                report.Check($"{name}: a Page slotted into a Book shows at once", client.Bar[0]!.Written() is { Pages.Count: 2 } && client.Inventory[0] == null);
                report.Check("  And changes what the Kit does at once, on this end", player.Kit.Slots[0]!.Pages.Length == 2);

                Page fire = client.At(book.In(1))!.Page!.Copy();
                fire.Shape = "circle";
                fire.Outcomes = new List<Outcome> { Deal("fire") };
                player.Write(book.In(1), fire);
                player.Title(book, "Fireball");
                report.Check("  What's written shows at once", client.Bar[0]!.Name == "Fireball" && player.Kit.Slots[0]!.Pages[1].Page.Shape == AreaShape.Circle);
                arena.Run(3);
                report.Check("  The server has it", truth.Bar[0]!.Name == "Fireball" && Text(truth.Bar[0]) == Text(client.Bar[0]));
                report.Check("  And what it comes to is the same on both ends", arena.Truth.Kit.Slots[0]!.Cost == player.Kit.Slots[0]!.Cost && arena.Truth.Kit.Slots[0]!.UseTime == player.Kit.Slots[0]!.UseTime);

                player.Move(ItemPlace.Cell(1), book.In(0).In(1));
                report.Check("  A Sticker stuck on a Page shows at once", player.Kit.Slots[0]!.First.Projectiles == 2 && client.Inventory[1] == null);
                arena.Run(3);
                report.Check("  And counts on the server", arena.Truth.Kit.Slots[0]!.First.Projectiles == 2 && Text(truth.Bar[0]) == Text(client.Bar[0]));

                player.Move(ItemPlace.Cell(0), book.In(0));
                player.Move(book.In(0).In(1), book.In(1).In(0));
                arena.Run(3);
                report.Check("  A move the rules refuse is refused on both ends", Text(truth.Bar[0]) == Text(client.Bar[0]));

                player.Move(ItemPlace.Cell(2), ItemPlace.OnBar(3));
                arena.Run(3);
                report.Check("  A Book with nothing in it goes on the bar, and can't be used", truth.Bar[3] != null && player.Kit.Slots[3] == null && arena.Truth.Kit.Slots[3] == null);
                player.Move(ItemPlace.Cell(3), ItemPlace.OnBar(3).In(0));
                report.Check("  Until it has a Page, at once", client.Bar[3] != null && client.Inventory[3] == null && player.Kit.Slots[3] != null);
                arena.Run(3);
                report.Check("  On both ends", truth.Bar[3] != null && truth.Inventory[3] == null && arena.Truth.Kit.Slots[3] != null);

                for (int i = 0; i < 20; i++)
                {
                    Page quick = Flight().With(Deal());
                    quick.Towards = i % 2 == 0 ? "towards-you" : Sentences.PlainTowards;
                    player.Write(ItemPlace.OnBar(3).In(0), quick);
                    player.Title(ItemPlace.OnBar(3), $"Draft {i}");
                }
                arena.Run(5);
                report.Check("  A run of changes ends up the last one, on both ends", truth.Bar[3]!.Name == "Draft 19" && client.Bar[3]!.Name == "Draft 19" && Text(truth.Bar[3]) == Text(client.Bar[3]));

                int corrected = arena.Player.Client.Predictor.Corrections;
                arena.Player.Script = () => { var press = new HeroIntent { Aim = East * 10f }; press.Use(3); return press; };
                arena.Run(4);
                arena.Player.Script = () => new HeroIntent();
                report.Check("  A written book is predicted like any other", arena.Player.Client.Predictor.Corrections - corrected < 25, $"{arena.Player.Client.Predictor.Corrections - corrected}");
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                GameLabPress(arena);
                report.Check("A plain hero attacks with the plain book", arena.Truth.Sim.Using == 0 || arena.Player.Uses[0] > 0);
            }

            HeroState rider = Hero();
            rider.Bar[0] = ItemGenerator.Copied(new Book { Pages = { Flight("rides").With(new Outcome { Deed = "pass" }), Area("cone").With(Deal()) } }, "Charge", 4);
            using (var arena = new Arena(worlds, request, rider))
            {
                arena.Run(0.5);
                Vector2 before = arena.Truth.Sim.Position;
                arena.Player.Script = () => { var press = new HeroIntent { Aim = East * 14f }; press.Use(0); return press; };
                arena.Run(0.1);
                arena.Player.Script = () => new HeroIntent();
                arena.Run(1);
                float moved = (arena.Truth.Sim.Position - before).Length;
                report.Check("Riding what's let go carries the hero, predicted like a dodge", moved > 6f, $"{moved} m");
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                HeroState client = arena.Player.Client.Hero;
                arena.Player.Client.Move(ItemPlace.OnBar(0), ItemPlace.Cell(5));
                arena.Run(0.5);
                report.Check("Taking the only book off the bar leaves nothing to use", arena.Truth.Kit.Slots[0] == null);
                arena.Player.Client.Drop(ItemPlace.Cell(5));
                arena.Run(0.5);
                report.Check("A book can be put on the ground", arena.Truth.Hero.Inventory[5] == null && arena.Session.Ground.Count > 0);
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                arena.Truth.Hero.Pickup(new Item { Name = "Sword", Slot = ItemSlot.MainHand, Level = 1, WeaponDamage = 5f }, 3);
                arena.Run(0.2);
                arena.Player.Client.Move(ItemPlace.Cell(3), ItemPlace.OnBar(2));
                arena.Run(1);
                report.Check("A sword can't be put on the bar, whatever a client asks", arena.Truth.Hero.Bar[2] == null && arena.Player.Client.Hero.Bar[2] == null);
            }
        }

        private static void GameLabPress(Arena arena)
        {
            arena.Player.Script = () => { var press = new HeroIntent { Aim = East * 5f }; press.Use(0); return press; };
            arena.Run(1);
            arena.Player.Script = () => new HeroIntent();
        }
    }
}

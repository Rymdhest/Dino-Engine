using Dino_Defenders.Client;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Server;
using Dino_Defenders.Books;
using Dino_Defenders.Stats;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Networking;
using Dino_Engine.World;
using OpenTK.Mathematics;
using static Dino_Defenders.Stats.M;

namespace Dino_NetLab
{
    /// <summary>
    /// What a fight promises a player, against a real server and real clients. Everything here is something said on screen: that what's marked is what gets hit, that it comes on the tick it says, that what's called off
    /// is seen to be, that a dodge is there when it's asked for, and that how hard a blow was goes into what it hit. None of this can say whether any of that feels good.
    /// </summary>
    internal static class CombatTests
    {
        private const float EdgeMargin = 0.03f;     // Metres either side of an edge that it's tried at. Well under what anyone could see.
        private const float Reaction = 0.2f;        // Seconds from seeing something to pressing a key, for someone who's watching for it.

        private const float SureAccuracy = 1e13f;   // There's no such thing as a blow that can't miss, only one so unlikely to that a shape can be tested with it.

        private static readonly Vector2 East = new Vector2(1f, 0f);
        private static readonly Vector2 West = new Vector2(-1f, 0f);

        private static HeroState Hero(params string[] talents)
        {
            HeroState hero = HeroState.CreateNew();
            hero.Level = 60;

            // Levels give no life, and these stand and take a great many blows in a row. One that dies part way through ignores all the rest.
            hero.Equipped[WornSlot.Neck] = new Item { Name = "Lab Charm", Slot = ItemSlot.Neck, Level = 60, Lines = { new ItemLine("health", 100000f) } };
            GameLab.Learn(hero, talents);
            hero.Bar[1] = GameLab.Written(Shelf.FrostNova);
            return hero;
        }

        // Someone with one thing to do, who doesn't miss. They stand still until told otherwise.
        private static Actor Fighter(Arena arena, Vector2 offset, BookPlan skill, float health = 1000000f, Team team = Team.Enemies)
        {
            var mods = new List<Mod> { Flat(Stat.Health, health), Flat(Stat.MoveSpeed, 1f), Flat(Stat.Mana, 100f), Flat(Stat.Accuracy, SureAccuracy) };
            return arena.Place(new Loadout(1, new WeaponProfile(10f, 10f, 0.6f, 0f, Tags.Sword), mods, new[] { skill }), offset, team);
        }

        private static bool TryMark(ECSWorld world, out TelegraphComponent mark, out Vector2 at)
        {
            foreach (Archetype archetype in world.QueryArchetypes(new BitMask(typeof(TelegraphComponent), typeof(PositionComponent)), BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    mark = entity.Get<TelegraphComponent>();
                    at = entity.Get<PositionComponent>().value.Xz;
                    return true;
                }
            }
            mark = default;
            at = default;
            return false;
        }

        private static bool Any<T>(ECSWorld world) where T : struct, IComponent
        {
            foreach (Archetype archetype in world.QueryArchetypes(new BitMask(typeof(T)), BitMask.Empty))
            {
                foreach (EntityView _ in new ComponentAccessor(archetype)) return true;
            }
            return false;
        }

        private static bool Until(Arena arena, Func<bool> done, double seconds = 5.0)
        {
            for (double waited = 0.0; waited < seconds; waited += NetTime.TickSeconds)
            {
                if (done()) return true;
                arena.Run(NetTime.TickSeconds);
            }
            return done();
        }

        // A player who does this on the next frame, and then nothing.
        private static void Once(LabPlayer player, HeroIntent intent)
        {
            bool done = false;
            player.Script = () =>
            {
                if (done) return new HeroIntent();
                done = true;
                return intent;
            };
        }

        private static int SlotOf(Arena arena, BookPlan skill) => Array.FindIndex(arena.Truth.Kit.Slots, s => s?.Book.Name == skill.Name);

        private static ActionComponent ActionOf(GameServer server, Entity who) => server.Ecs.GetEntityView(who).Get<ActionComponent>();

        private struct Blow
        {
            public uint Marked;
            public uint Promised;
            public uint Came;
        }

        // Lets someone go for the hero, and sees when the first blow was said to be coming and when it came. Something can be done to them a number of ticks after they've said so.
        private static Blow Watch(Arena arena, Actor enemy, Func<bool>? happened = null, int after = -1, Action? then = null, double seconds = 4.0)
        {
            var blow = new Blow();
            GameServer server = arena.Lab.Server;
            Actor hero = arena.Hero;
            hero.Vitals.Health = hero.Sheet.MaxHealth;
            happened ??= () => Arena.Lost(hero) > 0.001f;

            enemy.Still = false;
            arena.Lab.Run(seconds, () =>
            {
                uint tick = server.Tick - 1;
                if (blow.Promised == 0 && TryMark(server.Ecs, out TelegraphComponent mark, out _))
                {
                    blow.Marked = mark.Start;
                    blow.Promised = mark.Lands;
                }
                if (blow.Promised != 0 && after >= 0 && tick == blow.Marked + (uint)after) then?.Invoke();
                if (blow.Came == 0 && happened()) blow.Came = tick;
            });
            enemy.Still = true;
            return blow;
        }

        public static int Run(Options options)
        {
            var report = new Report();
            var request = new WorldRequest(options.Int("seed", 303), "isle");
            var worlds = new WorldCache();
            Console.WriteLine("Growing a world...");
            worlds.Get(request).Wait();

            report.Section("What a shape covers");
            Shapes(report);

            report.Section("What's marked is what hits");
            Marked(report, worlds, request);

            report.Section("It lands when it says");
            Landing(report, worlds, request);

            report.Section("What's called off");
            CalledOff(report, worlds, request);

            report.Section("A hero's own blows");
            OwnBlows(report, worlds, request);

            report.Section("Getting out of the way");
            Dodging(report, worlds, request);

            report.Section("Stopping the world");
            Pausing(report, worlds, request);

            report.Section("What a blow does to a body");
            Weight(report, worlds, request);

            report.Section("How far past dead");
            Overkill(report, worlds, request);

            report.Section("What the other end sees");
            Seen(report, worlds, request);

            report.Section("On every kind of line");
            Lines(report, worlds, request);

            return report.Finish();
        }

        private static void Shapes(Report report)
        {
            GroundShape circle = GroundShape.Circle(Vector2.Zero, 4f);
            report.Check("A circle goes as far as its edge and no further", circle.Covers(new Vector2(3.99f, 0f), 0f) && !circle.Covers(new Vector2(4.01f, 0f), 0f));
            report.Check("And a body can reach into it from outside", circle.Covers(new Vector2(4.4f, 0f), 0.5f) && !circle.Covers(new Vector2(4.6f, 0f), 0.5f));

            GroundShape sector = GroundShape.Sector(Vector2.Zero, Vector2.UnitY, 6f, MathF.PI / 2f);
            report.Check("A slice is what's in front", sector.Covers(new Vector2(0f, 5f), 0f) && !sector.Covers(new Vector2(0f, -5f), 0f));
            report.Check("Inside its angle", sector.Covers(new Vector2(3f, 3.2f), 0f) && !sector.Covers(new Vector2(3.2f, 3f), 0f));
            report.Check("One all the way round is a circle", GroundShape.Sector(Vector2.Zero, Vector2.UnitY, 6f, MathF.Tau).Kind == ShapeKind.Circle);

            GroundShape line = GroundShape.Line(Vector2.Zero, Vector2.UnitY, 10f, 1f);
            report.Check("A strip has sides", line.Covers(new Vector2(0.9f, 5f), 0f) && !line.Covers(new Vector2(1.1f, 5f), 0f));
            report.Check("And ends", !line.Covers(new Vector2(0f, -0.1f), 0f) && !line.Covers(new Vector2(0f, 10.1f), 0f));
            report.Check("A body counts at the sides of it", line.Covers(new Vector2(1.4f, 5f), 0.5f));

            report.Check("Nothing is further from the start of a shape than it says", circle.Extent >= 4f && sector.Extent >= 6f && line.Extent >= 10f);
        }

        // As they were, with nothing left over from last time that goes on hurting.
        private static void Fresh(Actor target)
        {
            target.Afflictions.BurnLeft = 0f;
            target.Afflictions.BleedLeft = 0f;
            target.Afflictions.Venom?.Clear();
            target.Vitals.Health = target.Sheet.MaxHealth;
        }

        // Puts what's aimed at either side of each part of a shape's edge, by moving where it comes from, and sees whether it's touched. Everything that can go wrong with a shape goes wrong at its edge.
        private static void Edges(Report report, Arena arena, string label, Actor user, BookProfile skill, Actor target, float body, int directions = 24, float settle = 0f)
        {
            Vector2 facing = new Vector2(0.6f, 0.8f);
            const float aimed = 11f;
            GroundShape shape = GroundShape.Of(skill, Vector2.Zero, facing, aimed);
            Vector2 middle = shape.Kind == ShapeKind.Circle ? shape.Origin : shape.Origin + shape.Facing * shape.Length * 0.5f;

            int tried = 0;
            int wrong = 0;
            string first = "";
            for (int i = 0; i < directions; i++)
            {
                float angle = (i + 0.37f) * MathF.Tau / directions;
                var way = new Vector2(MathF.Cos(angle), MathF.Sin(angle));

                float inside = 0f;
                float outside = shape.Extent * 2.5f + 4f;
                for (int step = 0; step < 26; step++)
                {
                    float at = (inside + outside) * 0.5f;
                    if (shape.Covers(middle + way * at, body)) inside = at;
                    else outside = at;
                }

                foreach (float offset in new[] { -EdgeMargin, EdgeMargin })
                {
                    Vector2 point = middle + way * (inside + offset);
                    bool marked = shape.Covers(point, body);

                    Fresh(target);
                    arena.Combat.Release(user, skill, target.Position - point, facing, aimed);
                    bool struck = Arena.Lost(target) > 0f;
                    if (settle > 0f) arena.Lab.Run(settle, () => struck |= Arena.Lost(target) > 0f);

                    tried++;
                    if (struck == marked) continue;
                    wrong++;
                    if (first.Length == 0) first = $"{(marked ? "marked but missed" : "hit but not marked")} at {point.X:0.00},{point.Y:0.00}";
                }
            }
            report.Check($"{label}: hits exactly what's marked, either side of the edge", wrong == 0, $"{wrong} of {tried} wrong, first {first}");
        }

        // What flies is the strip it's shown as, down both sides. Past the end it goes on, as an arrow does.
        private static void Sides(Report report, Arena arena, string label, Actor user, BookProfile skill, Actor target, float body)
        {
            const float aimed = 14f;
            const float margin = 0.25f;     // A flight goes a little way each tick.
            float width = GroundShape.Of(skill, Vector2.Zero, East, aimed).Width;
            WorldData world = arena.Session.World;

            float height = target.Height + 3.2f;

            int tried = 0;
            int wrong = 0;
            string first = "";
            for (int direction = 0; direction < 16; direction++)
            {
                float angle = direction * MathF.Tau / 16f;
                var way = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                var across = new Vector2(-way.Y, way.X);

                foreach (float along in new[] { 5f, 9f, 13f })
                {
                    foreach (float side in new[] { -1f, 1f })
                    {
                        foreach ((float offset, bool marked) in new[] { (width + body - margin, true), (width + body + margin, false) })
                        {
                            Vector2 from = target.Position - way * along - across * (side * offset);

                            // Something in the way stops an arrow whatever's marked, and it's right to.
                            bool clear = true;
                            for (float at = 1.5f; at < along + 2f; at += 0.25f)
                            {
                                Vector2 point = from + way * at;
                                if (height < world.Height.Sample(point) || world.Placement.OverlapsSolid(Footprint.Circle(point, 0.3f))) clear = false;
                            }
                            if (!clear) continue;

                            // What flies goes at the height of whoever let it go, who's been stood a long way off to be out of the way, and that's read afresh each tick.
                            Fresh(target);
                            user.Height = target.Height;
                            arena.Combat.Release(user, skill, from, way, aimed);
                            bool struck = false;
                            arena.Lab.Run(along / skill.First.Speed + 0.2, () => struck |= Arena.Lost(target) > 0f);
                            tried++;
                            if (struck == marked) continue;
                            wrong++;
                            if (first.Length == 0) first = $"{(marked ? "marked but missed" : "hit but not marked")}, {along} along, {side * offset} across";
                        }
                    }
                }
            }
            report.Check($"{label}: down both sides of it, whichever way it's going", wrong == 0 && tried >= 24, $"{wrong} of {tried} wrong, first {first}");
        }

        private static void Marked(Report report, WorldCache worlds, WorldRequest request)
        {
            using var arena = new Arena(worlds, request, Hero());
            Actor hero = arena.Hero;
            float heroBody = hero.Radius * GroundShape.HeroBodyShare;
            Actor dummy = arena.Dummy(new Vector2(30f, 0f));

            report.Note($"A hero's evasion is {hero.Sheet.Evasion:0.0}: a blow at accuracy 1000000 misses them one time in {1f / Hits.EvadeChance(hero.Sheet.Evasion, 1000000f):0}");
            float missed = Hits.EvadeChance(hero.Sheet.Evasion, SureAccuracy);
            report.Check("  (Blows tried here are as good as certain not to be evaded, so a miss means the shape)", missed < 1e-7f, $"{missed}");

            var instant = new[] { Shelf.Strike, Shelf.Cleave, Shelf.Sunder, Shelf.Whirl, Shelf.Maul, Shelf.Bite, Shelf.FrostNova, Shelf.Immolate, Shelf.Lance };
            foreach (BookPlan skill in instant)
            {
                Actor enemy = Fighter(arena, new Vector2(0f, 40f), skill);
                Edges(report, arena, $"{skill.Name} at a hero", enemy, enemy.Sheet.Profile(0), hero, heroBody);

                Actor friend = Fighter(arena, new Vector2(0f, -40f), skill, team: Team.Heroes);
                Edges(report, arena, $"{skill.Name} at an enemy", friend, friend.Sheet.Profile(0), dummy, dummy.Radius);
            }

            Actor mage = Fighter(arena, new Vector2(0f, 40f), Shelf.Meteor);
            Edges(report, arena, "A meteor, once it's come down", mage, mage.Sheet.Profile(0), hero, heroBody, directions: 8, settle: 1f);

            Actor undoer = Fighter(arena, new Vector2(0f, 42f), Shelf.Unmaking);
            Edges(report, arena, "Unmaking, once it's come down", undoer, undoer.Sheet.Profile(0), hero, heroBody, directions: 8, settle: 0.6f);

            Actor storm = Fighter(arena, new Vector2(0f, 44f), Shelf.Storm);
            Edges(report, arena, "Stormcall, while it lasts", storm, storm.Sheet.Profile(0), hero, heroBody, directions: 2, settle: storm.Sheet.Profile(0).First.Duration + 0.5f);

            Actor archer = Fighter(arena, new Vector2(0f, 46f), Shelf.Shot);
            Sides(report, arena, "An arrow", archer, archer.Sheet.Profile(0), hero, heroBody);

            Actor caster = Fighter(arena, new Vector2(0f, 48f), Shelf.Firebolt);
            Sides(report, arena, "A firebolt", caster, caster.Sheet.Profile(0), hero, heroBody);
        }

        private static void Landing(Report report, WorldCache worlds, WorldRequest request)
        {
            (string name, BookPlan skill, float stands)[] blows =
            {
                ("A cut", Shelf.Strike, 3.5f),
                ("A heavy blow", Shelf.Sunder, 4f),
                ("A bite", Shelf.Bite, 3.5f),
                ("A whirl", Shelf.Whirl, 4f),
                ("A nova", Shelf.FrostNova, 4f),
                                ("A beam", Shelf.Lance, 10f),
            };
            foreach ((string name, BookPlan skill, float stands) in blows)
            {
                using var arena = new Arena(worlds, request, Hero());
                Blow blow = Watch(arena, Fighter(arena, East * stands, skill));
                report.Check($"{name} lands on the tick it says", blow.Promised != 0 && blow.Came == blow.Promised, $"said {blow.Promised}, came {blow.Came}");
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                Blow blow = Watch(arena, Fighter(arena, East * 10f, Shelf.Meteor));
                report.Check("What comes down from above lands on the tick it says, however long it takes", blow.Promised != 0 && blow.Came == blow.Promised, $"said {blow.Promised}, came {blow.Came}");
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                Blow blow = Watch(arena, Fighter(arena, East * 12f, Shelf.Shot), () => Any<ProjectileComponent>(arena.Lab.Server.Ecs));
                report.Check("What flies leaves on the tick it says", blow.Promised != 0 && blow.Came == blow.Promised, $"said {blow.Promised}, came {blow.Came}");
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                Blow blow = Watch(arena, Fighter(arena, East * 12f, Shelf.Storm), () => Any<ZoneComponent>(arena.Lab.Server.Ecs));
                report.Check("Ground that's going to turn bad does so on the tick it says", blow.Promised != 0 && blow.Came == blow.Promised, $"said {blow.Promised}, came {blow.Came}");
            }

            float brisk;
            using (var arena = new Arena(worlds, request, Hero()))
            {
                Blow blow = Watch(arena, Fighter(arena, East * 4f, Shelf.Sunder));
                brisk = (blow.Promised - blow.Marked) * NetTime.TickSeconds;
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                Blow blow = Watch(arena, Fighter(arena, East * 3.5f, Shelf.Bite));
                float quickest = (blow.Promised - blow.Marked) * NetTime.TickSeconds;
                report.Check("Even the quickest blow leaves a moment to see it coming", quickest >= 0.6f - NetTime.TickSeconds, $"{quickest} s");
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                Actor enemy = Fighter(arena, East * 4f, Shelf.Sunder);
                enemy.Afflictions.ChillEffect = 0.5f;
                enemy.Afflictions.ChillLeft = 30f;
                Blow blow = Watch(arena, enemy, seconds: 6.0);
                float slow = (blow.Promised - blow.Marked) * NetTime.TickSeconds;
                report.Check("Someone slowed says it'll take them longer", slow > brisk * 1.8f && slow < brisk * 2.2f, $"{slow} s against {brisk} s");
                report.Check("And takes exactly that long", blow.Promised != 0 && blow.Came == blow.Promised, $"said {blow.Promised}, came {blow.Came}");
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                Actor enemy = Fighter(arena, East * 4f, Shelf.Sunder);
                Blow blow = Watch(arena, enemy, after: 6, then: () =>
                {
                    enemy.Afflictions.ChillEffect = 0.5f;
                    enemy.Afflictions.ChillLeft = 30f;
                });
                report.Check("A blow already promised comes when it was promised, whatever slows them after", blow.Promised != 0 && blow.Came == blow.Promised, $"said {blow.Promised}, came {blow.Came}");
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                Actor enemy = Fighter(arena, East * 4f, Shelf.Sunder);
                enemy.Afflictions.ChillEffect = 0.5f;
                enemy.Afflictions.ChillLeft = 30f;
                Blow blow = Watch(arena, enemy, after: 6, then: () => enemy.Afflictions.ChillLeft = 0f, seconds: 6.0);
                report.Check("Or whatever stops slowing them", blow.Promised != 0 && blow.Came == blow.Promised, $"said {blow.Promised}, came {blow.Came}");
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                Actor enemy = Fighter(arena, East * 4f, Shelf.Sunder);
                enemy.Still = false;
                bool same = Until(arena, () => TryMark(arena.Lab.Server.Ecs, out _, out _));
                ActionComponent action = ActionOf(arena.Lab.Server, enemy.Entity);
                TryMark(arena.Lab.Server.Ecs, out TelegraphComponent mark, out _);
                report.Check("How someone moves and what's on the ground are the same moment", same && action.Lands == mark.Lands && action.Start == mark.Start, $"{action.Lands} and {mark.Lands}");
                report.Check("And it's carried through for a while after", action.Ends > action.Lands);
            }
        }

        private static void CalledOff(Report report, WorldCache worlds, WorldRequest request)
        {
            const int seenFor = 16;     // Ticks after the mark that something's done about it, by when a client has it.

            (string name, Action<Arena, Actor> stop)[] ways =
            {
                ("Stunned", (_, enemy) => enemy.Afflictions.StunLeft = 1.5f),
                ("Frozen", (_, enemy) => enemy.Afflictions.FreezeLeft = 1.5f),
                ("Killed", (arena, enemy) => arena.Combat.Hit(arena.Hero, arena.Page(Shelf.Strike), enemy, 1000f, arena.At)),
            };

            foreach ((string name, Action<Arena, Actor> stop) in ways)
            {
                using var arena = new Arena(worlds, request, Hero());
                GameServer server = arena.Lab.Server;
                Actor enemy = Fighter(arena, East * 4f, Shelf.Sunder, health: 10f);
                bool seen = false;
                Blow blow = Watch(arena, enemy, after: seenFor, then: () =>
                {
                    seen = TryMark(arena.Player.Ecs, out _, out _);
                    stop(arena, enemy);
                }, seconds: 2.0);

                report.Check($"{name} in the wind-up: a client had been told", seen);
                report.Check("  The mark is gone", !TryMark(server.Ecs, out _, out _));
                report.Check("  For the client too", !TryMark(arena.Player.Ecs, out _, out _));
                report.Check("  And nothing lands", blow.Came == 0 && Arena.Lost(arena.Hero) == 0f, $"lost {Arena.Lost(arena.Hero)}");
                ActionComponent action = ActionOf(server, enemy.Entity);
                report.Check("  And they've stopped moving as if it were", action.Ends <= server.Tick);
            }
        }

        private static void OwnBlows(Report report, WorldCache worlds, WorldRequest request)
        {
            // A blow that's seen through, as the control for all that follows.
            using (var arena = new Arena(worlds, request, Hero("winter.white-ring"), heroes: 2))
            {
                GameServer server = arena.Lab.Server;
                LabPlayer friend = arena.Lab.Players[1];
                Actor target = arena.Dummy(East * 4f);
                int slot = SlotOf(arena, Shelf.FrostNova);
                report.Check("A hero can be given a skill to test with", slot >= 0, $"{slot}");

                var press = new HeroIntent { Aim = East * 4f };
                press.Use(slot);
                Once(arena.Player, press);
                bool shown = Until(arena, () => TryMark(friend.Ecs, out _, out _), 0.5);
                report.Check("A friend is shown what a hero's about to do", shown);
                TryMark(friend.Ecs, out TelegraphComponent mark, out _);
                report.Check("  As theirs, not as a danger", !mark.Hostile && mark.Owner == arena.Truth.Id);

                arena.Run(1.0);
                report.Check("  And it lands", Arena.Lost(target) > 0f);
            }

            using (var arena = new Arena(worlds, request, Hero("winter.white-ring"), heroes: 2))
            {
                GameServer server = arena.Lab.Server;
                LabPlayer friend = arena.Lab.Players[1];
                Actor target = arena.Dummy(East * 4f);
                int slot = SlotOf(arena, Shelf.FrostNova);

                var press = new HeroIntent { Aim = East * 4f };
                press.Use(slot);
                Once(arena.Player, press);
                Until(arena, () => arena.Truth.Sim.Using == slot, 0.5);
                report.Check("A swing can be seen starting", arena.Truth.Sim.ImpactIn > 0f);
                Once(arena.Player, new HeroIntent { Dash = true, Move = West });
                Until(arena, () => arena.Truth.Sim.DashLeft > 0f, 0.5);

                report.Check("Dodging out of it takes it back for the server", !TryMark(server.Ecs, out _, out _));
                ActionComponent action = ActionOf(server, arena.Truth.Entity);
                report.Check("And friends stop seeing it swung", action.Ends <= server.Tick);
                arena.Run(0.4);
                report.Check("And stop seeing it marked", !TryMark(friend.Ecs, out _, out _));
                arena.Run(0.6);
                report.Check("And it never lands", Arena.Lost(target) == 0f, $"{Arena.Lost(target)}");
            }

            using (var arena = new Arena(worlds, request, Hero("winter.white-ring"), heroes: 2))
            {
                GameServer server = arena.Lab.Server;
                int slot = SlotOf(arena, Shelf.FrostNova);
                Actor target = arena.Dummy(East * 4f);

                var press = new HeroIntent { Aim = East * 4f };
                press.Use(slot);
                Once(arena.Player, press);
                Until(arena, () => arena.Truth.Sim.Using == slot, 0.5);
                arena.Hero.Afflictions.StunLeft = 0.5f;
                arena.Run(0.1);
                report.Check("Stunned in a swing takes it back too", !TryMark(server.Ecs, out _, out _));
                report.Check("And friends stop seeing it swung", ActionOf(server, arena.Truth.Entity).Ends <= server.Tick);
                arena.Run(1.0);
                report.Check("And it never lands", Arena.Lost(target) == 0f, $"{Arena.Lost(target)}");
            }

            using (var arena = new Arena(worlds, request, Hero("winter.white-ring"), heroes: 2))
            {
                GameServer server = arena.Lab.Server;
                int slot = SlotOf(arena, Shelf.FrostNova);
                Actor enemy = Fighter(arena, East * 3f, Shelf.Strike);

                var press = new HeroIntent { Aim = East * 4f };
                press.Use(slot);
                Once(arena.Player, press);
                Until(arena, () => arena.Truth.Sim.Using == slot, 0.5);
                arena.Combat.Hit(enemy, enemy.Sheet.Profile(0).First, arena.Hero, 1000000f, enemy.Position);
                arena.Run(0.1);
                report.Check("Dying in a swing takes it back too", arena.Hero.Dead && !TryMark(server.Ecs, out _, out _));
                report.Check("And friends stop seeing it swung", ActionOf(server, arena.Truth.Entity).Ends <= server.Tick);
            }

            using (var arena = new Arena(worlds, request, Hero("winter.white-ring")))
            {
                GameServer server = arena.Lab.Server;
                int slot = SlotOf(arena, Shelf.FrostNova);
                byte before = ActionOf(server, arena.Truth.Entity).Serial;

                var both = new HeroIntent { Aim = East * 4f, Dash = true, Move = West };
                both.Use(slot);
                Once(arena.Player, both);
                Until(arena, () => arena.Truth.Sim.Using == slot, 0.5);
                arena.Run(0.05);
                ActionComponent action = ActionOf(server, arena.Truth.Entity);
                report.Check("Pressed in the same breath as a dodge, a skill is still seen", action.Serial != before && action.Ends > server.Tick, $"serial {before} to {action.Serial}, ends {action.Ends} at {server.Tick}");
            }

            // What's shown of a hero's own blow, which is done on their machine and goes nowhere near the server.
            using (var arena = new Arena(worlds, request, Hero("winter.white-ring")))
            {
                BookProfile nova = arena.Skill(Shelf.FrostNova);
                HeroSimState hero = HeroSimState.At(arena.At, 100f);
                hero.Using = 2;
                hero.Aim = East;
                hero.AimReach = 4f;

                var marks = new GroundMarks();
                marks.Own(nova, hero, start: 100.0, leaves: 118.0, lands: 118.0);
                report.Check("A hero's own area is shown", marks.Showing.Count() == 1);

                hero.Position += East * 2f;
                marks.Follow(hero, 110.0, 0.016f);
                report.Check("And goes with them until it leaves their hands", (marks.Showing.Single().Shape.Origin - hero.Position).Length < 0.01f);

                hero.Position += East * 2f;
                marks.Follow(hero, 119.0, 0.016f);
                report.Check("And stays where it was let go", (marks.Showing.Single().Shape.Origin - hero.Position).Length > 1.9f);

                hero.Using = HeroSimState.NoSlot;
                marks.Own(nova, hero, start: 120.0, leaves: 138.0, lands: 138.0);
                marks.Follow(hero, 125.0, 0.016f);
                report.Check("A blow they've let go of before it left their hands is taken back", marks.Withdrawing == 1, $"{marks.Withdrawing}");
            }

            // An entity's id goes to whatever's made next, so a client that goes by them loses marks in just the kind of fight that has most of them.
            {
                var world = new ECSWorld();
                var marks = new GroundMarks();
                Entity Mark(uint serial) => world.CreateEntity(new PositionComponent(new Vector3(serial, 0f, 0f)), new TelegraphComponent { Serial = serial, Kind = ShapeKind.Circle, Facing = Vector2.UnitY, Length = 3f, Hostile = true, Start = 10, Lands = 40 });

                Entity first = Mark(1);
                world.ApplyDeferredCommands();
                marks.Read(world, 0);
                world.DestroyEntity(first);
                world.ApplyDeferredCommands();
                Entity second = Mark(2);
                world.ApplyDeferredCommands();
                report.Check("  (The next thing made really is given the same id)", second.Id == first.Id, $"{first.Id}, {second.Id}");
                marks.Read(world, 0);
                report.Check("A mark that comes as another goes isn't taken for it", marks.Showing.Count() == 2, $"{marks.Showing.Count()}");

                EntityView view = world.GetEntityView(second);
                var moved = view.Get<TelegraphComponent>();
                moved.Lands = 90;
                view.Set(moved);
                marks.Read(world, 0);
                report.Check("When something's put off, what's shown is too", marks.Showing.Any(mark => mark.Lands == 90.0));

                Mark(3);
                world.ApplyDeferredCommands();
                var own = world.CreateEntity(new PositionComponent(Vector3.Zero), new TelegraphComponent { Serial = 4, Owner = 7, Kind = ShapeKind.Circle, Facing = Vector2.UnitY, Length = 3f, Start = 10, Lands = 40 });
                world.ApplyDeferredCommands();
                marks.Read(world, 7);
                report.Check("A hero's own blow isn't shown twice", marks.Showing.Count() == 3, $"{marks.Showing.Count()}");
            }
        }

        // The rules with nothing else in the way, tick by tick.
        private sealed class Rules
        {
            public HeroSimState Hero;
            public readonly HeroKit Kit;
            private readonly CharacterMover _mover;
            private uint _tick;

            public Rules(Arena arena)
            {
                Kit = arena.Truth.Kit;
                _mover = new CharacterMover(arena.Session.World);
                Hero = HeroSimState.At(arena.At, arena.Hero.Sheet.MaxMana);
                Hero.Health = arena.Hero.Sheet.MaxHealth;
            }

            public HeroStepEvents Step(int slots = 0, bool dash = false, Vector2 move = default)
            {
                var intent = new HeroIntent { Move = move, Aim = East * 6f, Slots = slots, Dash = dash };
                HeroCommand command = HeroCommand.From(intent, _tick++, 0);
                return HeroRules.Step(ref Hero, command, Kit, _mover);
            }

            public HeroStepEvents Run(double seconds, int slots = 0, Vector2 move = default)
            {
                var all = HeroStepEvents.None;
                for (int tick = (int)Math.Round(seconds * NetTime.TicksPerSecond); tick > 0; tick--) all |= Step(slots, false, move);
                return all;
            }

            public int Count(double seconds, HeroStepEvents what, int slots = 0)
            {
                int count = 0;
                for (int tick = (int)Math.Round(seconds * NetTime.TicksPerSecond); tick > 0; tick--)
                {
                    if ((Step(slots) & what) != 0) count++;
                }
                return count;
            }
        }

        private static void Dodging(Report report, WorldCache worlds, WorldRequest request)
        {
            using var arena = new Arena(worlds, request, Hero());
            BookProfile swing = arena.Truth.Kit.Slots[0]!;
            ActorSheet sheet = arena.Hero.Sheet;

            var rules = new Rules(arena);
            rules.Step(slots: 1);
            report.Check("A press starts a swing at once", rules.Hero.Using == 0 && rules.Hero.ImpactIn > 0f);
            rules.Run(swing.ImpactTime * 0.5);
            Vector2 from = rules.Hero.Position;
            HeroStepEvents dodge = rules.Step(dash: true, move: West);
            report.Check("A dodge goes the tick it's pressed, in the middle of a swing", (dodge & HeroStepEvents.Dashed) != 0);
            report.Check("And the swing's over", rules.Hero.Using == HeroSimState.NoSlot && rules.Hero.ImpactIn < 0f && rules.Hero.BusyFor == 0f);
            report.Check("And nothing can touch them", rules.Hero.Safe > 0f);
            HeroStepEvents after = rules.Run(1.0);
            report.Check("And its blow never lands", (after & HeroStepEvents.Landed) == 0);
            report.Check("And they're well away", (rules.Hero.Position - from).Length > sheet.DodgeSpeed * HeroRules.DashDuration * 0.7f, $"{(rules.Hero.Position - from).Length} m");
            report.Check("And safety doesn't outlast the dodge by much", rules.Hero.Safe == 0f);

            // Found by pressing real keys: with the button still down, the swing a dodge had just ended started again in the same tick.
            rules = new Rules(arena);
            rules.Step(slots: 1);
            rules.Run(0.05, slots: 1);
            rules.Step(slots: 1, dash: true, move: West);
            report.Check("A button still held doesn't swing through a dodge", rules.Hero.Using == HeroSimState.NoSlot && rules.Hero.DashLeft > 0f);
            bool swungThrough = false;
            while (rules.Hero.DashLeft > 0f)
            {
                rules.Step(slots: 1);
                if (rules.Hero.DashLeft > 0f && rules.Hero.Using >= 0) swungThrough = true;
            }
            report.Check("Not at any point in it", !swungThrough);
            report.Check("But it goes on the tick it's over", rules.Hero.Using == 0, $"{rules.Hero.Using}");

            rules = new Rules(arena);
            rules.Step(dash: true, move: West);
            rules.Run(0.05);
            rules.Step(slots: 1);
            report.Check("A click in the middle of a dodge doesn't start anything yet", rules.Hero.Using == HeroSimState.NoSlot);
            report.Check("But it isn't lost: it comes straight after", (rules.Run(0.25) & HeroStepEvents.Used) != 0);

            rules = new Rules(arena);
            HeroStepEvents landing = rules.Step(slots: 1);
            while ((landing & HeroStepEvents.Landed) == 0) landing = rules.Step();
            report.Check("Once a blow's landed, it's landed", rules.Hero.ImpactIn < 0f);
            report.Check("And a dodge still takes them out of the rest of the swing", (rules.Step(dash: true) & HeroStepEvents.Dashed) != 0 && rules.Hero.Using == HeroSimState.NoSlot);

            rules = new Rules(arena);
            rules.Step(dash: true);
            rules.Run(sheet.DodgeCooldown - 0.1);
            HeroStepEvents early = rules.Step(dash: true);
            report.Check("A dodge pressed a moment too soon doesn't go yet", (early & HeroStepEvents.Dashed) == 0);
            report.Check("But goes by itself when it can", (rules.Run(0.15) & HeroStepEvents.Dashed) != 0);

            rules = new Rules(arena);
            rules.Step(dash: true);
            rules.Run(sheet.DodgeCooldown - 0.4);
            rules.Step(dash: true);
            report.Check("One pressed long before doesn't, or a player's pressing a key for something that's over", (rules.Run(0.6) & HeroStepEvents.Dashed) == 0);

            rules = new Rules(arena);
            rules.Hero.Held = 0.1f;
            report.Check("Stunned, a dodge has to wait", (rules.Step(dash: true) & HeroStepEvents.Dashed) == 0);
            report.Check("And goes the moment it can", (rules.Run(0.12) & HeroStepEvents.Dashed) != 0);

            rules = new Rules(arena);
            rules.Step(slots: 1);
            rules.Run(swing.UseTime - 0.2);
            report.Check("A click made near the end of a swing doesn't start anything yet", (rules.Step(slots: 1) & HeroStepEvents.Used) == 0);
            report.Check("But comes straight after", rules.Count(0.3, HeroStepEvents.Used) == 1);

            rules = new Rules(arena);
            int swings = rules.Count(3.0, HeroStepEvents.Used, slots: 1);
            int expected = (int)MathF.Ceiling(3f / swing.UseTime);
            report.Check("A button held down goes on swinging", Math.Abs(swings - expected) <= 1, $"{swings} against {expected}");
            report.Check("And letting go of it stops it", rules.Count(swing.UseTime + 0.6, HeroStepEvents.Used) == 0);

            rules = new Rules(arena);
            float Speed()
            {
                Vector2 was = rules.Hero.Position;
                rules.Step(move: East);
                return (rules.Hero.Position - was).Length * NetTime.TicksPerSecond;
            }

            float free = Speed();
            rules.Step(slots: 1, move: East);
            float swinging = Speed();
            while (rules.Hero.ImpactIn >= 0f) rules.Step(move: East);
            rules.Run(swing.UseTime * 0.2, move: East);
            float eased = Speed();
            report.Check("A swing holds someone back until it's landed", swinging < free * 0.95f || swing.MoveFactor >= 1f, $"{swinging} against {free}");
            report.Check("And then lets go of them, so that a hero who's struck can get away", eased > swinging || swing.MoveFactor >= 1f, $"{eased} against {swinging}");

            using (var dodger = new Arena(worlds, request, Hero()))
            {
                Actor enemy = Fighter(dodger, East * 4f, Shelf.Sunder);
                dodger.Truth.Sim.Safe = 5f;
                Blow blow = Watch(dodger, enemy, seconds: 1.2);
                report.Check("A blow landing on someone in the middle of a dodge does nothing", blow.Promised != 0 && Arena.Lost(dodger.Hero) == 0f, $"lost {Arena.Lost(dodger.Hero)}");
            }
        }

        private static void Pausing(Report report, WorldCache worlds, WorldRequest request)
        {
            using var arena = new Arena(worlds, request, Hero());
            GameServer server = arena.Lab.Server;
            GameClient client = arena.Player.Client;
            Actor enemy = Fighter(arena, East * 4f, Shelf.Sunder);
            arena.Hero.Vitals.Health = arena.Hero.Sheet.MaxHealth;

            enemy.Still = false;
            Until(arena, () => TryMark(server.Ecs, out _, out _));
            arena.Run(0.1);
            TryMark(server.Ecs, out TelegraphComponent mark, out _);
            long left = (long)mark.Lands - server.Tick;

            client.Session!.Paused = true;
            bool stopped = Until(arena, () => server.Paused, 1.0);
            long leftWhenStopped = TryMark(server.Ecs, out mark, out _) ? (long)mark.Lands - server.Tick : -1;
            ActionComponent action = ActionOf(server, enemy.Entity);
            long actionLeft = (long)action.Lands - server.Tick;

            arena.Run(3.0);
            report.Check("Alone, a panel stops the world", stopped);
            report.Check("With whatever was coming still coming", TryMark(server.Ecs, out mark, out _));
            long stillLeft = (long)mark.Lands - server.Tick;
            report.Check("And no nearer", Math.Abs(stillLeft - leftWhenStopped) <= 1, $"{leftWhenStopped} ticks, then {stillLeft}");
            long stillActionLeft = (long)ActionOf(server, enemy.Entity).Lands - server.Tick;
            report.Check("What they're doing stands still with it", Math.Abs(stillActionLeft - actionLeft) <= 1, $"{actionLeft} ticks, then {stillActionLeft}");
            report.Check("And nothing's landed", Arena.Lost(arena.Hero) == 0f);

            client.Session.Paused = false;
            uint promised = mark.Lands;
            uint came = 0;
            arena.Lab.Run(1.5, () =>
            {
                if (TryMark(server.Ecs, out TelegraphComponent still, out _)) promised = still.Lands;
                if (came == 0 && Arena.Lost(arena.Hero) > 0f) came = server.Tick - 1;
            });
            enemy.Still = true;
            report.Check("And when it starts again it lands on the tick it says", came != 0 && came == promised, $"said {promised}, came {came}");

            // Something that's left their hands and is on its way down, which no mark or body is keeping time for.
            using var falling = new Arena(worlds, request, Hero());
            server = falling.Lab.Server;
            client = falling.Player.Client;
            Actor caster = Fighter(falling, East * 10f, Shelf.Meteor);
            falling.Hero.Vitals.Health = falling.Hero.Sheet.MaxHealth;

            caster.Still = false;
            Until(falling, () => TryMark(server.Ecs, out _, out _));
            TryMark(server.Ecs, out mark, out _);
            uint lands = mark.Lands;
            Until(falling, () => server.Tick >= lands - 20);
            caster.Still = true;

            client.Session!.Paused = true;
            Until(falling, () => server.Paused, 1.0);
            falling.Run(3.0);
            report.Check("What's already on its way waits in the air", Arena.Lost(falling.Hero) == 0f, $"lost {Arena.Lost(falling.Hero)}");

            client.Session.Paused = false;
            promised = 0;
            came = 0;
            falling.Lab.Run(1.5, () =>
            {
                if (TryMark(server.Ecs, out TelegraphComponent still, out _)) promised = still.Lands;
                if (came == 0 && Arena.Lost(falling.Hero) > 0f) came = server.Tick - 1;
            });
            report.Check("And comes down on the tick its mark says", came != 0 && came == promised, $"said {promised}, came {came}");
        }

        private static void Weight(Report report, WorldCache worlds, WorldRequest request)
        {
            using var arena = new Arena(worlds, request, Hero());
            PageProfile strike = arena.Page(Shelf.Strike);

            // What a blow does, per share of health it took, to someone of this nature.
            float PushOf(Condition nature, float strength, out Vector2 push, out float reeling)
            {
                Actor target = arena.Dummy(East * 3f, 20000f);
                target.Still = false;
                target.Nature |= nature;
                target.Push = Vector2.Zero;
                arena.Combat.Hit(arena.Hero, strike, target, strength, arena.At);
                push = target.Push;
                reeling = target.Reeling;
                float weight = Arena.Lost(target) / target.Sheet.MaxHealth;
                target.Still = true;
                arena.Combat.Hit(arena.Hero, strike, target, 100000f, arena.At);
                return weight;
            }

            float light = PushOf(Condition.None, 1f, out Vector2 lightPush, out float lightReel);
            float heavy = PushOf(Condition.None, 10f, out Vector2 heavyPush, out float heavyReel);
            report.Check("A blow moves what it hits", lightPush.LengthSquared > 0f);
            report.Check("Away from whoever struck it", Vector2.Dot(lightPush.Normalized(), East) > 0.99f, $"{lightPush}");
            report.Check("By how much of them it took", heavyPush.Length > lightPush.Length * 3f && heavy > light * 5f, $"{lightPush.Length} for {light}, {heavyPush.Length} for {heavy}");
            report.Check("And stops them for a moment, more for more", heavyReel >= lightReel && lightReel > 0f, $"{lightReel}, {heavyReel}");

            PushOf(Condition.None, 900f, out Vector2 hugePush, out float hugeReel);
            report.Check("But only so much, however hard", hugePush.Length <= 11.001f && hugeReel <= 0.3001f, $"{hugePush.Length}, {hugeReel}");

            float normalWeight = PushOf(Condition.None, 6f, out Vector2 normal, out _);
            float eliteWeight = PushOf(Condition.Elite, 6f, out Vector2 elite, out _);
            float bossWeight = PushOf(Condition.Boss, 6f, out Vector2 boss, out _);
            float normalGive = normal.Length / normalWeight;
            float eliteGive = elite.Length / eliteWeight;
            float bossGive = boss.Length / bossWeight;
            report.Check("Something great gives less", eliteGive < normalGive, $"{eliteGive} against {normalGive}");
            report.Check("And something greater gives hardly at all", bossGive < eliteGive && bossGive < normalGive * 0.25f, $"{bossGive} against {normalGive}");

            Actor dummy = arena.Dummy(East * 3f, 20000f);
            arena.Combat.Hit(arena.Hero, strike, dummy, 6f, arena.At);
            report.Check("What stands and takes it stands", dummy.Push == Vector2.Zero);

            Actor moved = arena.Dummy(East * 3f, 2000f);
            moved.Still = false;
            Vector2 was = moved.Position;
            arena.Combat.Hit(arena.Hero, strike, moved, 40f, arena.At);
            float carries = moved.Push.Length / 9f * (1f - MathF.Exp(-9f * 0.4f));
            arena.Run(0.4);
            moved.Still = true;
            float along = Vector2.Dot(moved.Position - was, East);
            report.Check("And it really goes that far", along > carries * 0.75f && along < carries * 1.25f, $"{along} m, for a shove worth {carries} m");

            foreach ((string name, Action<Actor> hold) in new (string, Action<Actor>)[] { ("stunned", a => a.Afflictions.StunLeft = 3f), ("frozen", a => a.Afflictions.FreezeLeft = 3f) })
            {
                Actor held = arena.Dummy(East * 3f, 2000f);
                held.Still = false;
                hold(held);
                arena.Run(0.05);
                Vector2 was2 = held.Position;
                arena.Combat.Hit(arena.Hero, strike, held, 40f, arena.At);
                float worth = held.Push.Length / 9f * (1f - MathF.Exp(-9f * 0.4f));
                arena.Run(0.4);
                float went = Vector2.Dot(held.Position - was2, East);
                report.Check($"Someone {name} goes with a blow just the same, and not when they wake", went > worth * 0.75f && held.Push.Length < 0.5f, $"{went} m for a shove worth {worth} m, {held.Push.Length} m/s left");
                held.Still = true;
            }

            Actor enemy = Fighter(arena, East * 3f, Shelf.Strike);
            Vector2 stood = arena.Truth.Sim.Position;
            arena.Combat.Release(enemy, enemy.Sheet.Profile(0), enemy.Position, West, 3f);
            arena.Run(0.2);
            report.Check("A hero's feet are their own: nothing shoves them", arena.Hero.Push == Vector2.Zero && (arena.Truth.Sim.Position - stood).Length < 0.001f, $"{arena.Hero.Push}");
            report.Check("Or their prediction would never agree with anything", arena.Player.Client.Predictor.Corrections == 0, $"{arena.Player.Client.Predictor.Corrections}");
        }

        private static void Overkill(Report report, WorldCache worlds, WorldRequest request)
        {
            using var arena = new Arena(worlds, request, Hero());
            GameServer server = arena.Lab.Server;
            PageProfile strike = arena.Page(Shelf.Strike);

            // A body that takes this much health to fill, with one point left in it, struck this hard.
            Vector3 Thrown(float health, float strength, out EntityView view)
            {
                Actor target = arena.Dummy(East * 3f, health);
                target.Vitals.Health = 1f;
                arena.Combat.Hit(arena.Hero, strike, target, strength, arena.At);
                view = server.Ecs.GetEntityView(target.Entity);
                return view.Get<EnemyComponent>().Thrown;
            }

            Vector3 barely = Thrown(1000000f, 1f, out _);
            Vector3 somewhat = Thrown(100f, 1f, out _);
            Vector3 hard = Thrown(100f, 10f, out _);
            Vector3 hugely = Thrown(100f, 10000f, out EntityView last);

            float Across(Vector3 thrown) => thrown.Xz.Length;
            report.Check("Only just dying lets a body drop", Across(barely) < 3f, $"{Across(barely)} m/s");
            report.Check("A lot more than enough throws it", Across(somewhat) > Across(barely), $"{Across(somewhat)} against {Across(barely)}");
            report.Check("And more, further", Across(hard) > Across(somewhat), $"{Across(hard)} against {Across(somewhat)}");
            report.Check("And there's an end to it", Across(hugely) >= Across(hard) && Across(hugely) < 65f, $"{Across(hugely)} m/s");
            report.Check("Away from whoever did it", Vector2.Dot(hugely.Xz.Normalized(), East) > 0.99f, $"{hugely}");
            report.Check("And up into the air", hugely.Y > 0f && hugely.Y < Across(hugely));
            report.Check("And that's the end of them", last.Get<EnemyComponent>().State == EnemyState.Dead);

            uint id = last.Get<NetIdentityComponent>().Id;
            arena.Run(0.5);
            bool told = arena.Player.Client.Ghosts.Ghosts.TryGetValue(id, out Ghost? ghost)
                && (arena.Player.Ecs.GetEntityView(ghost.Entity).Get<EnemyComponent>().Thrown - hugely).Length <= 0.25f;
            report.Check("How a body was thrown reaches a client", told);

            using var other = new Arena(worlds, request, Hero());
            Actor enemy = Fighter(other, East * 3f, Shelf.Strike);
            other.Hero.Vitals.Health = 1f;
            other.Truth.Sim.Health = 1f;
            other.Combat.Hit(enemy, enemy.Sheet.Profile(0).First, other.Hero, 100000f, enemy.Position);
            Vector3 hero = other.Lab.Server.Ecs.GetEntityView(other.Truth.Entity).Get<HeroComponent>().Thrown;
            report.Check("A hero goes the same way as anyone", hero.Xz.Length > 0f && Vector2.Dot(hero.Xz.Normalized(), West) > 0.99f, $"{hero}");
        }

        private static void Seen(Report report, WorldCache worlds, WorldRequest request)
        {
            using (var arena = new Arena(worlds, request, Hero()))
            {
                GameServer server = arena.Lab.Server;
                Actor target = arena.Dummy(East * 3f, 2000f);
                uint ghost = server.Ecs.GetEntityView(target.Entity).Get<NetIdentityComponent>().Id;

                var seen = new List<DamageEvent>();
                arena.Player.Client.Events.On<DamageEvent>(seen.Add);
                int counted = arena.Player.HitsSeen;
                arena.Combat.Hit(arena.Hero, arena.Page(Shelf.Strike), target, 3f, arena.At);
                arena.Run(0.5);

                report.Check("Everyone listening for a blow hears it, not just the last to ask", seen.Count > 0 && arena.Player.HitsSeen > counted, $"{seen.Count}, {arena.Player.HitsSeen - counted}");
                DamageEvent blow = seen.FirstOrDefault(e => e.Target == ghost);
                report.Check("A blow says who it landed on", blow.Target == ghost);
                report.Check("And which way it was going", Vector2.Dot(blow.Way, East) > 0.95f, $"{blow.Way}");
                report.Check("And how much of them it took", MathF.Abs(blow.Weight - Arena.Lost(target) / target.Sheet.MaxHealth) < 0.02f, $"{blow.Weight}");
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                var seen = new List<FlourishEvent>();
                arena.Player.Client.Events.On<FlourishEvent>(seen.Add);
                arena.Hero.Vitals.Health = arena.Hero.Sheet.MaxHealth * 0.5f;
                Once(arena.Player, new HeroIntent { Flask = true });
                arena.Run(0.5);
                report.Check("A drink from a flask is seen", seen.Count == 1 && seen[0].Kind == Flourish.Drink && seen[0].Who == arena.Truth.GhostId, $"{seen.Count}");

                seen.Clear();
                arena.Hero.Vitals.Health = arena.Hero.Sheet.MaxHealth;
                Once(arena.Player, new HeroIntent { Flask = true });
                arena.Run(0.5);
                report.Check("And one that wasn't drunk isn't", seen.Count == 0, $"{seen.Count}");
            }

            using (var arena = new Arena(worlds, request, Hero(), line: new NetworkConditions { Latency = 100 }))
            {
                GameServer server = arena.Lab.Server;
                GameClient client = arena.Player.Client;
                Actor first = arena.Dummy(East * 3f, 2000f);
                Actor second = arena.Dummy(East * 3f, 2000f);
                uint urgent = server.Ecs.GetEntityView(first.Entity).Get<NetIdentityComponent>().Id;

                uint urgentCame = 0;
                uint plainCame = 0;
                client.Events.Urgent<DamageEvent>(e => e.Target == urgent);
                client.Events.On<DamageEvent>(e =>
                {
                    if (e.Target == urgent) urgentCame = server.Tick;
                    else plainCame = server.Tick;
                });

                uint struck = server.Tick;
                arena.Combat.Hit(arena.Hero, arena.Page(Shelf.Strike), first, 1f, arena.At);
                arena.Combat.Hit(arena.Hero, arena.Page(Shelf.Strike), second, 1f, arena.At);
                arena.Run(0.6);
                report.Check("What's urgent isn't held back to be shown with everything else", urgentCame != 0 && plainCame != 0 && urgentCame < plainCame, $"{urgentCame - struck} ticks against {plainCame - struck}");
            }

            using (var arena = new Arena(worlds, request, Hero()))
            {
                GameServer server = arena.Lab.Server;
                Actor enemy = Fighter(arena, East * 4f, Shelf.Cleave);
                enemy.Still = false;
                Until(arena, () => TryMark(server.Ecs, out _, out _));
                TryMark(server.Ecs, out TelegraphComponent truth, out Vector2 at);
                Until(arena, () => TryMark(arena.Player.Ecs, out _, out _), 1.0);
                bool got = TryMark(arena.Player.Ecs, out TelegraphComponent mark, out Vector2 shownAt);
                enemy.Still = true;

                report.Check("A client gets a mark", got);
                report.Check("  With the moment it lands, to the tick", mark.Lands == truth.Lands);
                report.Check("  In the same place", (shownAt - at).Length < 0.1f, $"{(shownAt - at).Length} m off");
                report.Check("  The same size", MathF.Abs(mark.Length - truth.Length) <= 1f / 32f && MathF.Abs(mark.Width - truth.Width) <= 1f / 128f);
                report.Check("  Pointing the same way", (mark.Facing - truth.Facing).Length <= 1f / 64f);
                report.Check("  And as much of a danger", mark.Hostile && mark.Tier == truth.Tier);
            }
        }

        private static void Lines(Report report, WorldCache worlds, WorldRequest request)
        {
            foreach ((string name, NetworkConditions line) in Scenarios.All)
            {
                using var arena = new Arena(worlds, request, Hero(), line: line);
                GameServer server = arena.Lab.Server;
                GameClient client = arena.Player.Client;
                Actor enemy = Fighter(arena, East * 4.5f, Shelf.Strike);

                double warning = -1.0;
                double pressAt = -1.0;
                arena.Player.Script = () =>
                {
                    double now = client.Commands.Tick + client.Commands.Alpha;
                    var intent = new HeroIntent { Aim = East * 5f };
                    if (warning < 0.0 && TryMark(arena.Player.Ecs, out TelegraphComponent mark, out _))
                    {
                        warning = (mark.Lands - now) * NetTime.TickSeconds;
                        pressAt = now + Reaction * NetTime.TicksPerSecond;
                    }
                    if (pressAt >= 0.0 && now >= pressAt)
                    {
                        pressAt = -1.0;
                        intent.Dash = true;
                        intent.Move = West;
                    }
                    return intent;
                };

                arena.Hero.Vitals.Health = arena.Hero.Sheet.MaxHealth;
                enemy.Still = false;
                Until(arena, () => TryMark(server.Ecs, out _, out _));
                TryMark(server.Ecs, out TelegraphComponent truth, out _);
                float windUp = (truth.Lands - truth.Start) * NetTime.TickSeconds;
                Until(arena, () => server.Tick > truth.Lands + 4, 3.0);
                enemy.Still = true;
                float lost = Arena.Lost(arena.Hero);

                report.Note($"{name}: a {windUp:0.00} s blow is on screen {warning:0.00} s before it lands");
                if (name == "Awful") continue;
                report.Check($"{name}: it's seen in time to do something about it", warning > Reaction, $"{warning:0.00} s");
                report.Check($"{name}: and dodging when it's seen gets out of it", lost == 0f, $"lost {lost}");
            }

            // Each time a client thinks a blow has started it shows what it covers, so if a correction could start one again there'd be a pile of them on the ground.
            foreach ((string name, NetworkConditions line) in Scenarios.All)
            {
                using var arena = new Arena(worlds, request, Hero("winter.white-ring"), line: line);
                LabPlayer player = arena.Player;
                int slot = SlotOf(arena, Shelf.FrostNova);
                BookProfile nova = arena.Truth.Kit.Slots[slot]!;
                arena.Run(2.0);

                int before = player.Uses[slot];
                player.Script = () =>
                {
                    var intent = new HeroIntent { Aim = East * 4f };
                    intent.Use(slot);
                    return intent;
                };
                const float seconds = 12f;
                arena.Run(seconds);
                int uses = player.Uses[slot] - before;
                int most = (int)MathF.Ceiling(seconds / nova.UseTime) + 1;
                report.Check($"{name}: a key held down starts a blow no faster than it can go", uses >= 1 && uses <= most, $"{uses} in {seconds} s, at most {most}");
            }

            foreach ((string name, NetworkConditions line) in Scenarios.All.Take(4))
            {
                using var arena = new Arena(worlds, request, Hero(), line: line);
                LabPlayer player = arena.Player;
                arena.Run(2.0);
                int before = player.Client.Predictor.Corrections;

                int frame = 0;
                player.Script = () =>
                {
                    frame++;
                    var intent = new HeroIntent { Aim = East * 5f, Move = (frame / 40 % 2 == 0) ? East : West };
                    if (frame % 7 == 0) intent.Use(0);
                    if (frame % 150 == 0) intent.Dash = true;
                    return intent;
                };
                arena.Run(30.0);
                int corrections = player.Client.Predictor.Corrections - before;
                report.Check($"{name}: swinging and dodging for half a minute never needs correcting", corrections == 0, $"{corrections}");
            }
        }
    }
}

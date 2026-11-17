using Dino_Defenders.Client;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Server;
using Dino_Defenders.Stats;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Networking;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Defenders.Stats.M;

namespace Dino_NetLab
{
    /// <summary>
    /// What an enemy's rarity comes to, against a real server and real clients: who turns up as what, that what a trait gives it really gets used, and that a client is told enough to show it.
    /// How it looks, and whether a mark is drawn, can only be judged in the game.
    /// </summary>
    internal static class FoeTests
    {
        private static HeroState Tough()
        {
            HeroState hero = HeroState.CreateNew();
            hero.Level = 60;
            hero.Equipped[WornSlot.Neck] = new Item { Name = "Lab Charm", Slot = ItemSlot.Neck, Level = 60, Lines = { new ItemLine("health", 1000000f) } };
            return hero;
        }

        public static int Run(Options options)
        {
            var report = new Report();
            var request = new WorldRequest(options.Int("seed", 303), "isle");
            var worlds = new WorldCache();
            Console.WriteLine("Growing a world...");
            worlds.Get(request).Wait();

            report.Section("Who turns up as what");
            Musters(report, worlds.Get(request).Result);

            report.Section("What a trait gives it");
            Abilities(report, worlds, request);

            report.Section("What the world really puts there");
            Spawned(report, worlds, request);

            report.Section("What a client is told");
            Told(report, worlds, request);

            report.Section("Whoever chases gets there");
            ComesIn(report, worlds, request);
            Chases(report, worlds, new[] { request }.Concat(Enumerable.Range(1, options.Int("chaseworlds", 3)).Select(i => new WorldRequest(options.Int("seed", 303) + i * 101, options.Text("chasearchetype")))));

            report.Section("Bodies keep apart");
            PackKeepsApart(report, worlds, request);
            MinionsKeepApart(report, worlds, request);

            report.Section("What each beast does that's its own");
            Pounces(report, worlds, request);
            StrikesAndRuns(report, worlds, request);
            LeavesBile(report, worlds, request);
            Goads(report, worlds, request);
            PassesItsLightOn(report, worlds, request);
            BonesAndArrows(report, worlds, request);

            return report.Finish();
        }

        // What goes off by itself is no reason to hang back. A tyrant whose gear set off a zone 30 m out once stood that far off a hero for good, waiting to strike with it.
        private static void ComesIn(Report report, WorldCache worlds, WorldRequest request)
        {
            using var arena = new Arena(worlds, request, Tough());
            GameServer server = arena.Lab.Server;
            Loadout shared = arena.Session.Bestiary.Shared(Bestiary.Outlaw, 10, Rarity.Normal, Bestiary.NoTraits);
            var loadout = new Loadout(shared.Level, shared.Weapon, shared.Mods.Concat(shared.RuleMods).Concat(shared.Triggers), shared.Books.Append(Dino_Defenders.Books.Shelf.CinderTrail));

            Vector2 at = arena.At + new Vector2(18f, 0f);
            for (float turn = 0f; turn < MathF.Tau && !arena.Session.World.Passable.IsSet(at); turn += 0.4f) at = arena.At + new Vector2(MathF.Cos(turn), MathF.Sin(turn)) * 18f;
            float height = arena.Session.World.Height.Sample(at);
            var actor = new Actor(Team.Enemies, loadout, at, WorldData.CharacterRadius) { Body = "skeleton", Height = height };
            actor.Entity = server.Ecs.CreateEntity("Outlaw", Bodies.Walking(server.Main, "skeleton", new Vector3(at.X, height, at.Y), 1f, 2f,
                new EnemyComponent { Name = "Outlaw", Level = 10 },
                new EnemyBrainComponent { SiteIndex = 0, SpawnIndex = -1, Home = at, AggroRadius = 60f, LeashRadius = 200f },
                new HealthComponent(actor.Sheet.MaxHealth), new ActionComponent(), new AfflictedComponent(), new ActorComponent(actor)));

            bool struck = false;
            for (double waited = 0.0; waited < 8.0 && !struck; waited += NetTime.TickSeconds)
            {
                arena.Run(NetTime.TickSeconds);
                struck = server.Ecs.GetEntityView(actor.Entity).Get<EnemyComponent>().State == EnemyState.WindingUp;
            }
            report.Check("Something with a book that goes off by itself still comes in and fights", struck, $"got to {(actor.Position - arena.At).Length:0.0} m");
        }

        // A tyrant at home in the arena its keep holds (ARENAS.md), which is only there once a hero's come down to it, and a hero as far off as its floor goes in each of
        // eight directions, up to 28 m. It has to come to all of them that it can reach, and it can reach any it can find a path to: a body four metres across that stops at
        // the first statue, pew or nest is a boss that never fights. Then the rest of the keep, who have to come to a hero at the keep's gate.
        private static void Chases(Report report, WorldCache worlds, IEnumerable<WorldRequest> requests)
        {
            int tried = 0, reached = 0, followersTried = 0, followersReached = 0;
            float chasing = 0f, stalled = 0f, seenTravel = 0f, trueTravel = 0f;
            var stuck = new List<string>();
            var worldsSeen = new List<string>();
            foreach (WorldRequest request in requests)
            {
                worlds.Get(request).Wait();
                using var arena = new Arena(worlds, request, Tough());
                ECSWorld world = arena.Lab.Server.Ecs;
                WorldData land = arena.Session.World;
                SiteState keep = arena.Session.Sites.First(site => site.Site.Kind == SiteKind.Keep);
                arena.Player.Client.TravelTo(keep.Site.Position + keep.Site.RoadDirection * keep.Site.Radius * 1.3f);
                arena.Run(2.0);

                Entity boss = keep.Live.FirstOrDefault(entity => world.TryGetEntityLocation(entity, out _, out _) && world.GetEntityView(entity).Has<EnemyComponent>() && world.GetEntityView(entity).Get<EnemyComponent>().Rarity == Rarity.Unique);
                if (!boss.IsValid() || !world.TryGetEntityLocation(boss, out _, out _) || !world.GetEntityView(boss).Has<ActorComponent>())
                {
                    report.Note($"Seed {request.Seed}: no boss in the keep");
                    continue;
                }
                Actor tyrant = world.GetEntityView(boss).Get<ActorComponent>().Actor;
                Vector2 home = world.GetEntityView(boss).Get<EnemyBrainComponent>().Home;

                // From inside its notice, so that it has to come most of the way.
                for (int direction = 0; direction < 8; direction++)
                {
                    float angle = direction * MathF.Tau / 8f;
                    Vector2 way = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    float far = 28f;
                    while (far > 6f && !land.Passable.IsSet(home + way * far)) far -= 1f;
                    Vector2 spot = home + way * far;
                    if (!land.Passable.IsSet(spot) || new WorldPathfinder(exact: true).Find(land, home, spot) is not { Count: > 0 } path || (path[^1] - spot).Length > 2f) continue;

                    // Back home, with the hero already there.
                    Put(world, boss, tyrant, home);
                    arena.Player.Client.TravelTo(spot);
                    arena.Run(0.6);
                    Put(world, boss, tyrant, home);

                    bool struck = false;
                    for (double waited = 0.0; waited < 16.0 && !struck; waited += 0.25)
                    {
                        arena.Run(0.25);
                        struck = world.GetEntityView(boss).Get<EnemyComponent>().State == EnemyState.WindingUp;
                    }
                    tried++;
                    if (struck) reached++;
                    else stuck.Add($"seed {request.Seed} at {direction * 45}°: stopped {(tyrant.Position - arena.At).Length:0.0} m off");
                }

                // A hero who moves, as anyone fighting one does: somewhere else in the keep every few seconds. Whenever it's out of reach and after them, it has to be
                // getting somewhere.
                var hops = new WorldRandom((ulong)request.Seed).Fork("hops");
                float chasingHere = 0f, stalledHere = 0f;
                Put(world, boss, tyrant, home);
                Vector2 seenLast = default, trueLast = tyrant.Position;
                for (int hop = 0; hop < 16; hop++)
                {
                    float spread = 30f;
                    Vector2 spot = home + new Vector2(hops.Range(-spread, spread), hops.Range(-spread, spread));
                    if (!land.Passable.IsSet(spot)) continue;
                    // Somewhere it could get to, as wide as it is: an arena's floor has corners between a nest or a pew and its wall that a person fits and a tyrant doesn't, and
                    // a hero there isn't one it's being held up from reaching.
                    if (new WorldPathfinder(exact: true).Find(land, tyrant.Position, spot, WorldPathfinder.Wider(tyrant.Radius)) is not { Count: > 0 } reachable || (reachable[^1] - spot).Length > 2f) continue;
                    arena.Player.Client.TravelTo(spot);
                    Vector2 last = tyrant.Position;
                    float chasedFor = 0f;
                    for (double waited = 0.0; waited < 2.5; waited += 0.25)
                    {
                        arena.Run(0.25);
                        Vector2 seen = SeenBoss(arena);
                        if (seenLast != default && seen != default) seenTravel += (seen - seenLast).Length;
                        seenLast = seen;
                        trueTravel += (tyrant.Position - trueLast).Length;
                        trueLast = tyrant.Position;

                        bool after = world.GetEntityView(boss).Get<EnemyComponent>().State == EnemyState.Chasing && (tyrant.Position - arena.At).Length - tyrant.Radius >= 12f;
                        // Held up is going nowhere once it's had a moment to turn: on an arena's floor a chase is a second long, and the turn would be most of it.
                        chasedFor = after ? chasedFor + 0.25f : 0f;
                        if (after)
                        {
                            chasingHere += 0.25f;
                            if (chasedFor > TurnAround && (tyrant.Position - last).Length < tyrant.Sheet.MoveSpeed * 0.25f * 0.25f) stalledHere += 0.25f;
                        }
                        last = tyrant.Position;
                    }
                }
                chasing += chasingHere;
                stalled += stalledHere;
                worldsSeen.Add($"{request.Seed} {land.Plan.Archetype.Id} {stalledHere:0.0} of {chasingHere:0.0} s");

                // The gate, and everyone else in the keep. The tyrant goes far away, so as not to be what's in their way. Whoever went after the hero and gave up at
                // the end of their leash did what they should.
                Put(world, boss, tyrant, home + new Vector2(0f, 500f));
                Vector2 gate = keep.Site.Position + keep.Site.RoadDirection * keep.Site.Radius * 0.95f;
                arena.Player.Client.TravelTo(gate);
                var came = new HashSet<uint>();
                var noticed = new HashSet<uint>();
                var gaveUp = new HashSet<uint>();
                for (double waited = 0.0; waited < 16.0; waited += 0.25)
                {
                    arena.Run(0.25);
                    foreach (Entity entity in keep.Live)
                    {
                        EntityView view = world.GetEntityView(entity);
                        if (!view.Has<EnemyComponent>()) continue;
                        EnemyState state = view.Get<EnemyComponent>().State;
                        if (state == EnemyState.WindingUp) came.Add(entity.Id);
                        if (state == EnemyState.Chasing) noticed.Add(entity.Id);
                        if (state == EnemyState.Returning) gaveUp.Add(entity.Id);
                    }
                }
                foreach (Entity entity in keep.Live)
                {
                    if (entity.Id == boss.Id || !noticed.Contains(entity.Id) || (gaveUp.Contains(entity.Id) && !came.Contains(entity.Id))) continue;
                    followersTried++;
                    if (came.Contains(entity.Id)) followersReached++;
                }
            }

            report.Note($"Tyrants held up, by world: {string.Join("; ", worldsSeen)}");
            report.Check("A tyrant comes to a hero wherever there's a way on its arena's floor, and fights", tried > 0 && reached == tried, $"{reached} of {tried}" + (stuck.Count > 0 ? "; " + string.Join("; ", stuck) : ""));
            report.Check("A tyrant after a hero who keeps moving is hardly ever held up", chasing > 0f && stalled <= chasing * 0.1f, $"{stalled:0.0} s of {chasing:0.0} s chasing");
            report.Check("And a client sees it go as far as it went", trueTravel > 0f && MathF.Abs(seenTravel - trueTravel) < trueTravel * 0.05f, $"{seenTravel:0} m of {trueTravel:0} m");
            report.Check("And whoever in the keep comes after a hero at the gate gets to them", followersTried > 0 && followersReached >= followersTried * 0.9f, $"{followersReached} of {followersTried}");
        }

        private const float TurnAround = 0.5f;     // Seconds a tyrant that's set off after someone takes to come round, before going nowhere counts as being held up.

        private static Vector2 SeenBoss(Arena arena)
        {
            foreach (Archetype archetype in arena.Player.Ecs.QueryArchetypes(new BitMask(typeof(EnemyComponent), typeof(PositionComponent)), BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    if (entity.Get<EnemyComponent>().Rarity == Rarity.Unique) return entity.Get<PositionComponent>().value.Xz;
                }
            }
            return default;
        }

        private static void Put(ECSWorld world, Entity entity, Actor actor, Vector2 at)
        {
            float height = actor.Height;
            world.GetEntityView(entity).Set(new PositionComponent(new Vector3(at.X, height, at.Y)));
            world.GetEntityView(entity).Set(new VelocityComponent());
            actor.Advance(0f, 0, at);
        }

        private static void Musters(Report report, WorldData world)
        {
            var seen = new int[Enum.GetValues<Rarity>().Length];
            int wrongTraits = 0, unledMagic = 0;
            int Followers(int level)
            {
                int magic = 0;
                for (int i = 0; i < world.Haunts.Count; i++)
                {
                    var random = new WorldRandom((ulong)(i + level * 10000));
                    var orders = new PackOrders(world, world.Haunts[i], world.Haunts[i].Folk, level, true, random.Fork("pack"), new LandTerms());
                    for (int place = 0; place < 12; place++)
                    {
                        (_, Rarity rarity, int traits) = orders.Draw(place, random.Fork(place));
                        seen[(int)rarity]++;
                        if (place > 0 && rarity == Rarity.Magic) magic++;

                        // What's drawn here is as many as it shares. What's left to have comes as it spawns, and nothing normal gets any.
                        int have = System.Numerics.BitOperations.PopCount((uint)traits);
                        if (rarity == Rarity.Normal && have != 0) wrongTraits++;
                        if (have > Bestiary.PowerOf(rarity).Traits) wrongTraits++;
                        if (place > 0 && rarity == Rarity.Rare) unledMagic++;
                    }
                }
                return magic;
            }

            int early = Followers(2);
            int late = Followers(30);
            report.Note($"Normal {seen[0]}, magic {seen[1]}, rare {seen[2]}, unique {seen[3]}");
            report.Check("Nothing normal has a trait, and nothing has more than its rarity allows", wrongTraits == 0, $"{wrongTraits}");
            report.Check("Most of a pack is normal, some is magic, and a little is rare", seen[0] > seen[1] && seen[1] > seen[2] && seen[2] > 0, $"{seen[0]}, {seen[1]}, {seen[2]}");
            report.Check("Only a leader is rare", unledMagic == 0, $"{unledMagic}");
            report.Check("Nothing out in the wild is unique", seen[3] == 0);
            report.Check("There's more magic among followers further in", late > early * 1.5f, $"{early} to {late}");
        }

        // Like what's put in the world, and awake.
        // How far two bodies are inside each other, as a share of how far apart their middles should be.
        private static float Overlap(Actor a, Actor b) => MathF.Max(0f, 1f - (a.Position - b.Position).Length / (a.Radius + b.Radius));

        private static float WorstOverlap(IReadOnlyList<Actor> bodies)
        {
            float worst = 0f;
            for (int i = 0; i < bodies.Count; i++)
            {
                for (int j = i + 1; j < bodies.Count; j++)
                {
                    if (!bodies[i].Dead && !bodies[j].Dead) worst = MathF.Max(worst, Overlap(bodies[i], bodies[j]));
                }
            }
            return worst;
        }

        // Somewhere a body can stand, this far out from the hero, as near the way asked as there is.
        private static Vector2 Standing(Arena arena, float angle, float distance)
        {
            for (float turn = 0f; turn < MathF.Tau; turn += 0.2f)
            {
                Vector2 at = arena.At + new Vector2(MathF.Cos(angle + turn), MathF.Sin(angle + turn)) * distance;
                if (arena.Session.World.Passable.IsSet(at)) return at;
            }
            return arena.At;
        }

        // Eight close on a hero who stands there. However they jostle, none of them stands in another or in the hero.
        private static void PackKeepsApart(Report report, WorldCache worlds, WorldRequest request)
        {
            using var arena = new Arena(worlds, request, Tough());
            GameServer server = arena.Lab.Server;
            arena.Player.Fights = false;
            var pack = new List<Actor>();
            for (int i = 0; i < 8; i++) pack.Add(Spawn(arena, Bestiary.Outlaw, Rarity.Normal, Bestiary.NoTraits, Standing(arena, i * MathF.Tau / 8f, 12f) - arena.At).actor);

            float worstPack = 0f, worstHero = 0f;
            for (double waited = 0.0; waited < 7.0; waited += NetTime.TickSeconds)
            {
                arena.Run(NetTime.TickSeconds);
                if (waited < 3.0) continue;
                worstPack = MathF.Max(worstPack, WorstOverlap(pack));
                foreach (Actor foe in pack)
                {
                    if (!foe.Dead) worstHero = MathF.Max(worstHero, Overlap(foe, arena.Hero));
                }
            }
            report.Check("A pack closing on a hero doesn't stand in itself", worstPack < BodySlack, $"{worstPack:P0} at worst");
            report.Check("Or in the hero", worstHero < BodySlack, $"{worstHero:P0} at worst");

            // Idle, nobody steers round anybody, so four standing in one place only come apart by not being able to be there.
            var idle = new List<Actor>();
            Vector2 heap = Standing(arena, MathF.PI, 30f) - arena.At;
            for (int i = 0; i < 4; i++) idle.Add(Spawn(arena, Bestiary.Outlaw, Rarity.Normal, Bestiary.NoTraits, heap).actor);
            server.Ecs.ApplyDeferredCommands();
            foreach (Actor foe in idle)
            {
                EntityView view = server.Ecs.GetEntityView(foe.Entity);
                var brain = view.Get<EnemyBrainComponent>();
                brain.AggroRadius = 0f;
                view.Set(brain);
            }
            arena.Run(1.0);
            report.Check("Bodies standing in one place push each other out of it", WorstOverlap(idle) < BodySlack, $"{WorstOverlap(idle):P0} at worst");
        }

        private const float BodySlack = 0.15f;      // Of how far apart two should be, that they can be pressed into each other at the worst moment.

        // Minions raised all in one place step out of each other, then follow their owner on a walk, each in a place of its own, facing where it's going, and stand still once it's there.
        private static void MinionsKeepApart(Report report, WorldCache worlds, WorldRequest request)
        {
            using var arena = new Arena(worlds, request, Tough());
            arena.Player.Fights = false;
            GameServer server = arena.Lab.Server;
            Actor owner = arena.Hero;
            Loadout loadout = Bestiary.MinionOf(owner.Loadout, owner.Sheet, Bestiary.Hound);
            Vector2 start = Standing(arena, MathF.PI * 0.5f, 3f);
            var pets = new List<Actor>();
            for (int i = 0; i < 5; i++)
            {
                float height = arena.Session.World.Height.Sample(start);
                var pet = new Actor(Team.Heroes, loadout, start, WorldData.CharacterRadius * 0.85f) { Owner = owner, Body = Bestiary.Hound.Look, Height = height, MinionLeft = 1000f };
                pet.Entity = server.Ecs.CreateEntity("Hound", Bodies.Walking(server.Main, Bestiary.Hound.Look, new Vector3(start.X, height, start.Y), 0.85f, 1.5f,
                    new EnemyComponent { Name = "Hound", Level = owner.Loadout.Level, Friendly = true },
                    new EnemyBrainComponent { SiteIndex = -1, SpawnIndex = -1, Home = owner.Position, AggroRadius = 26f, LeashRadius = 45f },
                    new HealthComponent(pet.Sheet.MaxHealth), new ActionComponent(), new AfflictedComponent(), new ActorComponent(pet)));
                arena.Session.Minions.Add(pet);
                pets.Add(pet);
            }

            arena.Run(2.0);
            report.Check("Minions raised in one place step out of each other", WorstOverlap(pets) < BodySlack, $"{WorstOverlap(pets):P0} at worst");

            // Across the way everything starts out facing (+z), so that facing where they go is something they have to do.
            arena.Player.Heading = Standing(arena, 0f, 30f);
            float worst = 0f;
            int moving = 0, facing = 0;
            var last = pets.Select(p => p.Position).ToArray();
            for (double walked = 0.0; walked < 9.0; walked += NetTime.TickSeconds)
            {
                arena.Run(NetTime.TickSeconds);
                worst = MathF.Max(worst, WorstOverlap(pets));
                for (int i = 0; i < pets.Count; i++)
                {
                    Vector2 step = pets[i].Position - last[i];
                    last[i] = pets[i].Position;
                    if (step.Length < 2f * NetTime.TickSeconds) continue;
                    moving++;
                    Quaternion turned = server.Ecs.GetEntityView(pets[i].Entity).Get<RotationComponent>().quaternion;
                    Vector3 ahead = Vector3.Transform(Vector3.UnitZ, turned);
                    if (Vector2.Dot(new Vector2(ahead.X, ahead.Z).Normalized(), step.Normalized()) > MathF.Cos(MathF.PI / 4f)) facing++;
                }
            }
            report.Check("They follow without standing in each other", worst < BodySlack, $"{worst:P0} at worst");
            report.Check("Facing where they're going, not gliding sideways", moving > 0 && facing >= moving * 0.85f, $"{facing} of {moving} steps");

            arena.Player.Heading = null;
            arena.Player.Fights = false;
            arena.Run(3.0);
            float still = 0f;
            last = pets.Select(p => p.Position).ToArray();
            arena.Run(1.0);
            for (int i = 0; i < pets.Count; i++) still = MathF.Max(still, (pets[i].Position - last[i]).Length);
            float farthest = pets.Max(p => (p.Position - owner.Position).Length);
            report.Check("Once their owner stops, they stand still near them", still < 0.3f && farthest < 12f, $"moved {still:0.00} m in the last second, the farthest {farthest:0.0} m off");

            // Back the way they came, which is through them, and then they have to get round to behind again.
            arena.Player.Heading = Standing(arena, MathF.PI, 25f);
            float throughHero = 0f;
            int backMoving = 0, backFacing = 0;
            last = pets.Select(p => p.Position).ToArray();
            for (double walked = 0.0; walked < 6.0; walked += NetTime.TickSeconds)
            {
                arena.Run(NetTime.TickSeconds);
                for (int i = 0; i < pets.Count; i++)
                {
                    throughHero = MathF.Max(throughHero, Overlap(pets[i], owner));
                    Vector2 step = pets[i].Position - last[i];
                    last[i] = pets[i].Position;
                    if (step.Length < 2f * NetTime.TickSeconds) continue;
                    backMoving++;
                    Vector3 ahead = Vector3.Transform(Vector3.UnitZ, server.Ecs.GetEntityView(pets[i].Entity).Get<RotationComponent>().quaternion);
                    if (Vector2.Dot(new Vector2(ahead.X, ahead.Z).Normalized(), step.Normalized()) > MathF.Cos(MathF.PI / 4f)) backFacing++;
                }
            }
            report.Check("A hero walking back through their minions doesn't walk through them", throughHero < BodySlack, $"{throughHero:P0} at worst");
            report.Check("And they turn to follow, facing where they go", backMoving > 0 && backFacing >= backMoving * 0.75f, $"{backFacing} of {backMoving} steps");
        }

        // One of a kind as the world would stand it: its own body's width, and its kind's habits in its brain.
        private static (Actor actor, Entity entity) Beast(Arena arena, EnemyKind kind, Vector2 offset, float aggro = 60f)
        {
            GameServer server = arena.Lab.Server;
            Loadout loadout = arena.Session.Bestiary.Shared(kind, 10, Rarity.Normal, Bestiary.NoTraits);
            Vector2 at = arena.At + offset;
            float height = arena.Session.World.Height.Sample(at);
            float radius = kind.RadiusAt(kind.Scale);
            var actor = new Actor(Team.Enemies, loadout, at, radius) { Body = "skeleton", Height = height };
            actor.Entity = server.Ecs.CreateEntity(kind.Name, Bodies.Walking(server.Main, "skeleton", new Vector3(at.X, height, at.Y), 1f, 2f, radius,
                new EnemyComponent { Name = kind.Name, Rarity = Rarity.Normal, Level = 10 },
                new EnemyBrainComponent { SiteIndex = 0, SpawnIndex = -1, Home = at, AggroRadius = aggro, LeashRadius = 200f, KeepsAway = kind.KeepsAway, Skirmishes = kind.Skirmishes },
                new HealthComponent(actor.Sheet.MaxHealth), new ActionComponent(), new AfflictedComponent(), new ActorComponent(actor)));
            return (actor, actor.Entity);
        }

        private static (EnemyComponent enemy, EnemyBrainComponent brain) Mind(Arena arena, Entity entity)
        {
            EntityView view = arena.Lab.Server.Ecs.GetEntityView(entity);
            return (view.Get<EnemyComponent>(), view.Get<EnemyBrainComponent>());
        }

        // A ripper from far enough off comes the rest of the way in one go. Where it's going is marked before it leaves, it's there as the blow is, and the blow is where the mark was.
        private static void Pounces(Report report, WorldCache worlds, WorldRequest request)
        {
            using var arena = new Arena(worlds, request, Tough());
            GameServer server = arena.Lab.Server;
            (Actor ripper, Entity entity) = Beast(arena, Bestiary.Ripper, Standing(arena, 0f, 15f) - arena.At);

            bool leapt = false;
            EnemyBrainComponent brain = default;
            Vector2 from = default;
            for (double waited = 0.0; waited < 5.0 && !leapt; waited += NetTime.TickSeconds)
            {
                from = ripper.Position;
                arena.Run(NetTime.TickSeconds);
                (EnemyComponent enemy, brain) = Mind(arena, entity);
                leapt = enemy.State == EnemyState.WindingUp && brain.Leaping;
            }
            report.Check("A ripper far enough off pounces", leapt);
            if (!leapt) return;

            Vector2 to = brain.LeapTo;
            float gap = (arena.At - to).Length;
            report.Check("At whoever it's after, from where it stood", (to - from).Length > 6f && gap < ripper.Radius + arena.Hero.Radius + 1f, $"{(to - from).Length:0.0} m, to {gap:0.0} m off them");

            bool marked = brain.Mark.IsValid() && server.Ecs.TryGetEntityLocation(brain.Mark, out _, out _);
            Vector2 markAt = marked ? server.Ecs.GetEntityView(brain.Mark).Get<PositionComponent>().value.Xz : default;
            uint lands = marked ? server.Ecs.GetEntityView(brain.Mark).Get<TelegraphComponent>().Lands : 0u;
            report.Check("What's marked is where it comes down, and not where it's standing", marked && (markAt - to).Length < 0.2f, $"{(markAt - to).Length:0.00} m off");
            report.Check("With long enough to see it and step out", marked && (lands - arena.Session.Server.Tick) * NetTime.TickSeconds >= 0.6f, $"{(lands - arena.Session.Server.Tick) * NetTime.TickSeconds:0.00} s");

            // It stays where it is while it gathers itself, then goes.
            float health = arena.Hero.Vitals.Health;
            float still = 0f;
            bool gone = false;
            uint let = 0;
            for (int guard = 0; guard < 240; guard++)
            {
                Vector2 was = ripper.Position;
                arena.Run(NetTime.TickSeconds);
                (EnemyComponent enemy, brain) = Mind(arena, entity);
                if (!gone && (ripper.Position - was).Length < 0.02f) still += NetTime.TickSeconds;
                else gone = true;
                if (enemy.State == EnemyState.WindingUp) continue;
                let = arena.Session.Server.Tick;
                break;
            }
            report.Check("It gathers itself first, which is when it's seen coming", still >= 0.3f, $"{still:0.00} s");
            report.Check("And lets go on the tick its mark said", marked && Math.Abs((long)let - lands) <= 1, $"marked for {lands}, let go on {let}");
            arena.Run(NetTime.TickSeconds * 3);
            report.Check("It's there as the blow lands", (ripper.Position - to).Length < 1.5f, $"{(ripper.Position - to).Length:0.0} m off");
            report.Check("Which reaches whoever stood for it", arena.Hero.Vitals.Health < health);
            report.Check("And it's a while before it does that again", ripper.Cooldowns[0] > 4f, $"{ripper.Cooldowns[0]:0.0} s");

            // Too near to be worth it, it bites.
            using var close = new Arena(worlds, request, Tough());
            (Actor near, Entity nearEntity) = Beast(close, Bestiary.Ripper, Standing(close, 0f, 6f) - close.At);
            bool wound = false, leaping = false;
            for (double waited = 0.0; waited < 4.0 && !wound; waited += NetTime.TickSeconds)
            {
                close.Run(NetTime.TickSeconds);
                (EnemyComponent enemy, EnemyBrainComponent mind) = Mind(close, nearEntity);
                wound = enemy.State == EnemyState.WindingUp;
                leaping = mind.Leaping;
            }
            report.Check("Up close there's nothing to pounce over, and it does something else", wound && !leaping);
        }

        // A strider bites and is gone, and comes back for another.
        private static void StrikesAndRuns(Report report, WorldCache worlds, WorldRequest request)
        {
            using var arena = new Arena(worlds, request, Tough());
            (Actor strider, Entity entity) = Beast(arena, Bestiary.Strider, Standing(arena, 0f, 10f) - arena.At);

            int blows = 0;
            float atBlow = 0f, furthest = 0f;
            EnemyState last = EnemyState.Idle;
            for (double waited = 0.0; waited < 10.0 && blows < 2; waited += NetTime.TickSeconds)
            {
                arena.Run(NetTime.TickSeconds);
                (EnemyComponent enemy, _) = Mind(arena, entity);
                float off = (strider.Position - arena.At).Length;
                if (last == EnemyState.WindingUp && enemy.State != EnemyState.WindingUp)
                {
                    if (++blows == 1) atBlow = off;
                }
                else if (blows == 1) furthest = MathF.Max(furthest, off);
                last = enemy.State;
            }
            report.Check("A strider that's struck is gone again", blows >= 1 && furthest > atBlow + 5f, $"struck at {atBlow:0.0} m, got to {furthest:0.0} m");
            report.Check("And comes back for another", blows >= 2);

            (Actor raptor, Entity other) = Beast(arena, Bestiary.Raptor, Standing(arena, MathF.PI, 10f) - arena.At);
            float nearest = float.MaxValue, after = 0f;
            bool struck = false;
            last = EnemyState.Idle;
            for (double waited = 0.0; waited < 6.0; waited += NetTime.TickSeconds)
            {
                arena.Run(NetTime.TickSeconds);
                (EnemyComponent enemy, _) = Mind(arena, other);
                float off = (raptor.Position - arena.At).Length;
                if (last == EnemyState.WindingUp && enemy.State != EnemyState.WindingUp) struck = true;
                if (struck) after = MathF.Max(after, off);
                nearest = MathF.Min(nearest, off);
                last = enemy.State;
            }
            report.Check("What doesn't run stands its ground between blows", struck && after < nearest + 4f, $"within {nearest:0.0} m, and {after:0.0} m at its furthest after");
        }

        // What's rotting leaves its bile where it falls, which hurts whoever stays standing in it. Anything else leaves nothing.
        private static void LeavesBile(Report report, WorldCache worlds, WorldRequest request)
        {
            using var arena = new Arena(worlds, request, Tough());
            (Actor rotting, _) = Beast(arena, Bestiary.RottingRaptor, Standing(arena, 0f, 2.5f) - arena.At, aggro: 0f);
            (Actor living, _) = Beast(arena, Bestiary.Raptor, Standing(arena, MathF.PI, 30f) - arena.At, aggro: 0f);
            arena.Run(0.2);

            arena.Combat.Harm(arena.Hero, rotting, 1e9f, DamageType.Physical);
            arena.Combat.Harm(arena.Hero, living, 1e9f, DamageType.Physical);
            float health = arena.Hero.Vitals.Health;
            arena.Run(0.3);
            report.Check("Something rotting falls", rotting.Dead && living.Dead);
            report.Check("And leaves what kept it going where it lay", rotting.Zones.Count == 1 && rotting.Zones[0].Left > 2f, $"{rotting.Zones.Count}");
            report.Check("Which is ground that lingers, and can be walked out of", rotting.Zones.Count == 1 && rotting.Zones[0].Page.Page.Lingers);
            report.Check("Something living leaves nothing", living.Zones.Count == 0, $"{living.Zones.Count}");

            arena.Run(2.5);
            report.Check("Whoever stays standing in it is the worse for it", arena.Hero.Vitals.Health < health, $"{health - arena.Hero.Vitals.Health:0.0}");
            arena.Run(5.0);
            float left = arena.Hero.Vitals.Health;
            arena.Run(8.0);
            report.Check("And it's gone in the end", rotting.Zones.TrueForAll(zone => zone.Left <= 0f) && arena.Hero.Vitals.Health >= left - 0.01f);
        }

        // A wrangler goads what's round them before it fights, and nothing's marked for it, as it's nothing to get out of the way of.
        private static void Goads(Report report, WorldCache worlds, WorldRequest request)
        {
            using var arena = new Arena(worlds, request, Tough());
            (Actor wrangler, Entity entity) = Beast(arena, Bestiary.Wrangler, Standing(arena, 0f, 13f) - arena.At);
            (Actor raptor, _) = Beast(arena, Bestiary.WarRaptor, Standing(arena, 0.3f, 15f) - arena.At, aggro: 0f);

            bool goading = false, marked = false, goaded = false;
            for (double waited = 0.0; waited < 5.0 && !goaded; waited += NetTime.TickSeconds)
            {
                arena.Run(NetTime.TickSeconds);
                (EnemyComponent enemy, EnemyBrainComponent brain) = Mind(arena, entity);
                if (enemy.State == EnemyState.WindingUp && brain.Book == 0)
                {
                    goading = true;
                    marked |= brain.Mark.IsValid();
                }
                goaded = wrangler.Cooldowns[0] > 0f;
            }
            arena.Run(0.2);
            bool Quick(Actor actor) => actor.Buffs.Exists(buff => buff.Buff == Dino_Defenders.Books.BuffCatalog.Quickening);
            report.Check("A wrangler goads before it fights", goading && goaded);
            report.Check("Which marks no ground", !marked);
            report.Check("What's round them is the quicker for it", Quick(raptor) && raptor.Sheet.MoveSpeed > Bestiary.WarRaptor.MoveSpeed * 1.2f, $"{raptor.Sheet.MoveSpeed:0.0} m/s");
            report.Check("And no hero is", !Quick(arena.Hero));

            bool fights = false;
            for (double waited = 0.0; waited < 8.0 && !fights; waited += NetTime.TickSeconds)
            {
                arena.Run(NetTime.TickSeconds);
                (EnemyComponent enemy, EnemyBrainComponent brain) = Mind(arena, entity);
                fights = enemy.State == EnemyState.WindingUp && brain.Book != 0;
            }
            report.Check("Then it comes in and fights, rather than standing off to goad again", fights, $"{(wrangler.Position - arena.At).Length:0.0} m off");
        }

        // One light moves a pit of bone skitters: when one falls, the rest have what moved it.
        private static void PassesItsLightOn(Report report, WorldCache worlds, WorldRequest request)
        {
            using var arena = new Arena(worlds, request, Tough());
            Vector2 at = Standing(arena, 0f, 25f) - arena.At;
            (Actor first, _) = Beast(arena, Bestiary.BoneSkitter, at, aggro: 0f);
            (Actor second, _) = Beast(arena, Bestiary.BoneSkitter, at + new Vector2(3f, 0f), aggro: 0f);
            (Actor raptor, _) = Beast(arena, Bestiary.BoneRaptor, at + new Vector2(0f, 3f), aggro: 0f);
            arena.Run(0.2);
            float before = second.Sheet.Preview(Dino_Defenders.Books.Shelf.HungryBite).First.AverageHit;

            arena.Combat.Harm(arena.Hero, first, 1e9f, DamageType.Physical);
            arena.Run(0.3);
            bool Rallied(Actor actor) => actor.Buffs.Exists(buff => buff.Buff == Dino_Defenders.Books.BuffCatalog.Rallied);
            report.Check("A bone skitter falling drives on the ones beside it", first.Dead && Rallied(second) && Rallied(raptor));
            report.Check("Which bite the harder for it", second.Sheet.Preview(Dino_Defenders.Books.Shelf.HungryBite).First.AverageHit > before * 1.05f);

            arena.Combat.Harm(arena.Hero, raptor, 1e9f, DamageType.Physical);
            arena.Run(0.3);
            report.Check("A bone raptor falling drives nobody on", raptor.Dead && second.Buffs.Find(buff => buff.Buff == Dino_Defenders.Books.BuffCatalog.Rallied).Stacks == 1);

            (Actor shell, _) = Beast(arena, Bestiary.GhostClubtail, at + new Vector2(-8f, 0f), aggro: 0f);
            arena.Run(0.2);
            report.Check("A clubtail's ghost has a shell of light", shell.Sheet.MaxBarrier > shell.Sheet.MaxHealth * 0.2f && shell.Vitals.Barrier >= shell.Sheet.MaxBarrier * 0.99f, $"{shell.Vitals.Barrier:0} of {shell.Sheet.MaxBarrier:0}");
        }

        // What flies goes between a bone-beast's ribs: an arrow takes less of it than of the living animal, and a blade takes the same.
        private static void BonesAndArrows(Report report, WorldCache worlds, WorldRequest request)
        {
            using var arena = new Arena(worlds, request, Tough());

            // Each with far more Health than it has, so that it stands for all of it, and nothing else about it changed. Not so much more that a blow is lost in the rounding of it.
            Actor Standing(EnemyKind kind, Vector2 offset)
            {
                Loadout shared = arena.Session.Bestiary.Shared(kind, 10, Rarity.Normal, Bestiary.NoTraits);
                return arena.Place(new Loadout(shared.Level, shared.Weapon, shared.Mods.Concat(shared.RuleMods).Concat(shared.Triggers).Append(Flat(Stat.Health, 300000f)), shared.Books), offset);
            }
            Actor bones = Standing(Bestiary.BoneRaptor, new Vector2(30f, 0f)), living = Standing(Bestiary.Raptor, new Vector2(30f, 6f)), ghost = Standing(Bestiary.GhostRaptor, new Vector2(30f, 12f));

            float Taken(Actor target, Dino_Defenders.Books.BookPlan book)
            {
                float before = target.Vitals.Health;
                for (int i = 0; i < 600; i++) arena.Combat.Hit(arena.Hero, arena.Page(book), target, 1f, arena.At);
                return before - target.Vitals.Health;
            }
            float shotBones = Taken(bones, Dino_Defenders.Books.Shelf.Shot), struckBones = Taken(bones, Dino_Defenders.Books.Shelf.Strike);
            float shotLiving = Taken(living, Dino_Defenders.Books.Shelf.Shot), struckLiving = Taken(living, Dino_Defenders.Books.Shelf.Strike);
            float share = shotBones / MathF.Max(struckBones, 1f) / (shotLiving / MathF.Max(struckLiving, 1f));
            report.Check("An arrow takes about a third less of a bone-beast than of the living animal, for what a blade takes of each", share > 0.55f && share < 0.75f, $"{share:P0}");

            // A ghost is harder to lay a blade on than the animal it was.
            int Landed(Actor target)
            {
                int landed = 0;
                for (int i = 0; i < 1000; i++)
                {
                    float before = target.Vitals.Health;
                    arena.Combat.Hit(arena.Hero, arena.Page(Dino_Defenders.Books.Shelf.Strike), target, 1f, arena.At);
                    if (target.Vitals.Health < before) landed++;
                }
                return landed;
            }
            int onGhost = Landed(ghost), onLiving = Landed(living);
            report.Check("Blows go through a ghost more often than through the animal it was", onLiving > 100 && onGhost < onLiving * 0.9f, $"{onGhost} of 1000 landed on the ghost, {onLiving} on the animal");
            report.Check("A ghost's bite goes through what's worn against it", ghost.Sheet.Preview(Dino_Defenders.Books.Shelf.RakingBite).First.ArmourIgnore >= 40f && living.Sheet.Preview(Dino_Defenders.Books.Shelf.RakingBite).First.ArmourIgnore <= 0f);
        }

        private static (Actor actor, Entity entity) Spawn(Arena arena, EnemyKind kind, Rarity rarity, int traits, Vector2 offset)
        {
            GameServer server = arena.Lab.Server;
            Loadout loadout = arena.Session.Bestiary.Shared(kind, 10, rarity, traits);
            Vector2 at = arena.At + offset;
            float height = arena.Session.World.Height.Sample(at);
            var actor = new Actor(Team.Enemies, loadout, at, WorldData.CharacterRadius) { Body = "skeleton", Height = height };
            actor.Entity = server.Ecs.CreateEntity(kind.Name, Bodies.Walking(server.Main, "skeleton", new Vector3(at.X, height, at.Y), 1f, 2f,
                new EnemyComponent { Name = Bestiary.NameOf(kind, traits), Rarity = rarity, Level = 10 },
                new EnemyBrainComponent { SiteIndex = 0, SpawnIndex = -1, Home = at, AggroRadius = 60f, LeashRadius = 200f, Abilities = Bestiary.AbilitiesOf(kind, traits) },
                new HealthComponent(actor.Sheet.MaxHealth), new ActionComponent(), new AfflictedComponent(), new ActorComponent(actor)));
            return (actor, actor.Entity);
        }

        private static void Abilities(Report report, WorldCache worlds, WorldRequest request)
        {
            using var arena = new Arena(worlds, request, Tough());

            // Swift gives a melee fighter something thrown. It has to use it, and then not stand off waiting to use it again.
            int swift = Bestiary.Trait(Array.FindIndex(Bestiary.Traits, trait => trait.Name == "Swift"));
            (Actor foe, Entity entity) = Spawn(arena, Bestiary.Outlaw, Rarity.Magic, swift, new Vector2(11f, 0f));
            report.Check("It knows one more thing than a normal one", foe.Loadout.Books.Length == Bestiary.Outlaw.Books.Length + 1, $"{foe.Loadout.Books.Length}");

            bool thrown = false;
            for (double waited = 0.0; waited < 8.0 && !thrown; waited += NetTime.TickSeconds)
            {
                arena.Run(NetTime.TickSeconds);
                thrown = foe.Cooldowns[0] > 0f;
            }
            report.Check("And uses it", thrown);
            report.Check("Which then takes a while to come round", foe.Cooldowns[0] > Rulebook.AbilityCooldown * 0.5f, $"{foe.Cooldowns[0]}");

            bool struckClose = false;
            float nearest = float.MaxValue;
            for (double waited = 0.0; waited < 4.5; waited += NetTime.TickSeconds)
            {
                arena.Run(NetTime.TickSeconds);
                nearest = MathF.Min(nearest, (foe.Position - arena.At).Length);
                var enemy = arena.Lab.Server.Ecs.GetEntityView(entity).Get<EnemyComponent>();
                struckClose |= enemy.State == EnemyState.WindingUp && foe.Cooldowns[0] > 0f;
            }
            float reach = foe.Sheet.Profile(foe.Loadout.Books.Length - 1).First.Length;
            report.Check("Meanwhile it comes in and does what it always did, rather than standing off", struckClose && nearest <= reach, $"got to {nearest:0.0} m of a reach of {reach:0.0} m");
        }

        private static void Spawned(Report report, WorldCache worlds, WorldRequest request)
        {
            using var arena = new Arena(worlds, request, Tough());
            var seen = new int[Enum.GetValues<Rarity>().Length];
            int wrongNature = 0, wrongAbilities = 0, wrongTraits = 0, weak = 0, withAbilities = 0;
            // The share of health the weakest creature there is has, which was a wolf's 0.42 until skitters came, at 0.22: no rarer one may fall below it.
            float weakest = Bestiary.Everyone.Min(kind => kind.Health);
            ECSWorld world = arena.Lab.Server.Ecs;

            foreach (SiteState state in arena.Session.Sites.Where(site => site.Site.Kind == SiteKind.Keep).Concat(arena.Session.Sites.Where(site => site.Wild).Take(40)))
            {
                arena.Player.Client.TravelTo(state.Site.Position + new Vector2(0f, 5f));
                arena.Run(1.5);
                foreach (Entity entity in state.Live)
                {
                    EntityView view = world.GetEntityView(entity);
                    if (!view.Has<EnemyComponent>() || !view.Has<ActorComponent>()) continue;
                    var enemy = view.Get<EnemyComponent>();
                    var brain = view.Get<EnemyBrainComponent>();
                    Actor actor = view.Get<ActorComponent>().Actor;
                    seen[(int)enemy.Rarity]++;

                    Condition nature = enemy.Rarity switch { Rarity.Unique => Condition.Boss, Rarity.Rare => Condition.Rare, _ => Condition.None };
                    if (actor.Nature != nature) wrongNature++;
                    if (brain.Abilities > Bestiary.PowerOf(enemy.Rarity).Traits || brain.Abilities >= actor.Loadout.Books.Length) wrongAbilities++;
                    if (brain.Abilities > 0) withAbilities++;
                    if (enemy.Rarity != Rarity.Unique && Bestiary.Traits.Count(trait => enemy.Name.Contains(trait.Name)) != Bestiary.PowerOf(enemy.Rarity).Traits) wrongTraits++;
                    if (enemy.Rarity != Rarity.Normal && actor.Sheet.MaxHealth < Bestiary.HealthAt(enemy.Level) * weakest * Bestiary.PowerOf(enemy.Rarity).Health * 0.7f) weak++;
                }
            }

            report.Note($"Normal {seen[0]}, magic {seen[1]}, rare {seen[2]}, unique {seen[3]}");
            report.Check("Everything is there: a world has all four", seen.All(count => count > 0), string.Join(", ", seen));
            report.Check("What it is as far as the rules go matches", wrongNature == 0, $"{wrongNature}");
            report.Check("It knows how many of its books are abilities, and has no more than its traits give", wrongAbilities == 0, $"{wrongAbilities}");
            report.Check("Some of them really do have one", withAbilities > 0);
            report.Check("Everything has exactly the traits its rarity says, wherever it stands", wrongTraits == 0, $"{wrongTraits}");
            report.Check("And it's as strong as its rarity says", weak == 0, $"{weak}");
        }

        private static void Told(Report report, WorldCache worlds, WorldRequest request)
        {
            using var arena = new Arena(worlds, request, Tough());
            ECSWorld client = arena.Player.Ecs;

            (Actor rare, _) = Spawn(arena, Bestiary.Outlaw, Rarity.Rare, Bestiary.Trait(0) | Bestiary.Trait(1), new Vector2(3f, 0f));
            bool named = false, marked = false;
            Rarity told = Rarity.Normal, from = Rarity.Normal;
            for (double waited = 0.0; waited < 6.0 && !marked; waited += NetTime.TickSeconds)
            {
                arena.Run(NetTime.TickSeconds);
                foreach (Archetype archetype in client.QueryArchetypes(new BitMask(typeof(EnemyComponent)), BitMask.Empty))
                {
                    foreach (EntityView entity in new ComponentAccessor(archetype))
                    {
                        told = entity.Get<EnemyComponent>().Rarity;
                        named = entity.Get<EnemyComponent>().Name == "Burning Frostbound Outlaw";
                    }
                }
                foreach (Archetype archetype in client.QueryArchetypes(new BitMask(typeof(TelegraphComponent)), BitMask.Empty))
                {
                    foreach (EntityView entity in new ComponentAccessor(archetype))
                    {
                        from = entity.Get<TelegraphComponent>().From;
                        marked = true;
                    }
                }
            }
            report.Check("A client knows how rare it is", told == Rarity.Rare, $"{told}");
            report.Check("And what it's called, with all of what it is", named);
            report.Check("And how rare whatever's coming is from", marked && from == Rarity.Rare, $"{marked}, {from}");
        }
    }
}

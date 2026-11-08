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
            actor.Entity = server.Ecs.CreateEntity("Outlaw", Bodies.Walking(server, "skeleton", new Vector3(at.X, height, at.Y), 1f, 2f,
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

        // A hero 20 m off in each of eight directions, and a tyrant at home in its keep, which is a yard of crates and tents inside walls. It has to come out to all of
        // them that it can reach, and it can reach any it can find a path to: a body four metres across that stops at the first crate is a boss that never fights.
        // Then the rest of the keep, who have to come to a hero at the gate.
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

                Entity boss = keep.Live.FirstOrDefault(entity => world.GetEntityView(entity).Has<EnemyComponent>() && world.GetEntityView(entity).Get<EnemyComponent>().Rarity == Rarity.Unique);
                if (!world.GetEntityView(boss).Has<ActorComponent>())
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
                    Vector2 spot = home + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 28f;
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
                    Vector2 spot = home + new Vector2(hops.Range(-30f, 30f), hops.Range(-30f, 30f));
                    if (!land.Passable.IsSet(spot)) continue;
                    arena.Player.Client.TravelTo(spot);
                    Vector2 last = tyrant.Position;
                    for (double waited = 0.0; waited < 2.5; waited += 0.25)
                    {
                        arena.Run(0.25);
                        Vector2 seen = SeenBoss(arena);
                        if (seenLast != default && seen != default) seenTravel += (seen - seenLast).Length;
                        seenLast = seen;
                        trueTravel += (tyrant.Position - trueLast).Length;
                        trueLast = tyrant.Position;

                        bool after = world.GetEntityView(boss).Get<EnemyComponent>().State == EnemyState.Chasing && (tyrant.Position - arena.At).Length - tyrant.Radius >= 12f;
                        if (after)
                        {
                            chasingHere += 0.25f;
                            if ((tyrant.Position - last).Length < tyrant.Sheet.MoveSpeed * 0.25f * 0.25f) stalledHere += 0.25f;
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
            report.Check("A tyrant comes out of its keep to a hero wherever there's a way, and fights", tried > 0 && reached == tried, $"{reached} of {tried}" + (stuck.Count > 0 ? "; " + string.Join("; ", stuck) : ""));
            report.Check("A tyrant after a hero who keeps moving is hardly ever held up", chasing > 0f && stalled <= chasing * 0.1f, $"{stalled:0.0} s of {chasing:0.0} s chasing");
            report.Check("And a client sees it go as far as it went", trueTravel > 0f && MathF.Abs(seenTravel - trueTravel) < trueTravel * 0.05f, $"{seenTravel:0} m of {trueTravel:0} m");
            report.Check("And whoever in the keep comes after a hero at the gate gets to them", followersTried > 0 && followersReached >= followersTried * 0.9f, $"{followersReached} of {followersTried}");
        }

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
                pet.Entity = server.Ecs.CreateEntity("Hound", Bodies.Walking(server, Bestiary.Hound.Look, new Vector3(start.X, height, start.Y), 0.85f, 1.5f,
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

        private static (Actor actor, Entity entity) Spawn(Arena arena, EnemyKind kind, Rarity rarity, int traits, Vector2 offset)
        {
            GameServer server = arena.Lab.Server;
            Loadout loadout = arena.Session.Bestiary.Shared(kind, 10, rarity, traits);
            Vector2 at = arena.At + offset;
            float height = arena.Session.World.Height.Sample(at);
            var actor = new Actor(Team.Enemies, loadout, at, WorldData.CharacterRadius) { Body = "skeleton", Height = height };
            actor.Entity = server.Ecs.CreateEntity(kind.Name, Bodies.Walking(server, "skeleton", new Vector3(at.X, height, at.Y), 1f, 2f,
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
                    if (enemy.Rarity != Rarity.Normal && actor.Sheet.MaxHealth < Bestiary.HealthAt(enemy.Level) * 0.42f * Bestiary.PowerOf(enemy.Rarity).Health * 0.7f) weak++;
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

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

            return report.Finish();
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

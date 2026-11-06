using Dino_Defenders.Client;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Server;
using Dino_Defenders.UI;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.GUI;
using Dino_Engine.Networking;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_NetLab
{
    /// <summary>
    /// What's on the ground, for real, with clients on the other end of a line. The rule under test is that nothing ever reaches a pack unless a player asked for it and was
    /// there, and everything else is what has to stay true around that: who sees what, who gets what when two reach for one thing, and what becomes of it when things go wrong.
    /// </summary>
    internal static class LootTests
    {
        private const int Walls = 30;

        private static readonly NetworkConditions Line = new NetworkConditions { Latency = 40, Jitter = 8, LossChance = 0.01 };

        private sealed class Scene : IDisposable
        {
            public readonly GameLab Lab;
            public readonly LabPlayer Player;
            public readonly ServerPlayer Truth;

            public Scene(WorldCache worlds, WorldRequest request, NetworkConditions? line = null)
            {
                Lab = new GameLab(worlds, request, "Landing");
                Player = Lab.AddPlayer("Hero", line ?? Line, 1);
                Player.Fights = false;
                Lab.Run(line != null ? 8 : 4);
                Truth = Lab.Server.Heroes[0];
            }

            public ServerSession Session => Lab.Server.Session!;
            public GameSession Seen => Player.Client.Session!;

            /// <summary>Something lying near the hero, and the find it is.</summary>
            public (int find, Vector2 at) Lay(Item item, Vector2 offset = default)
            {
                Vector2 from = Truth.Sim.Position + offset;
                var ground = new Vector3(from.X, Session.World.Height.Sample(from), from.Y);
                var before = Session.Ground.All.Select(l => l.find).ToHashSet();
                Session.Ground.Spill(ground, new[] { item }, 0, 0);
                (int find, _, Vector2 at) = Session.Ground.All.First(l => !before.Contains(l.find));
                return (find, at);
            }

            public void Go(Vector2 to)
            {
                Player.Client.TravelTo(to);
                Lab.Run(0.6);
            }

            public void Dispose() => Lab.Dispose();
        }

        private static Item Blade(string name = "Test Blade") => new Item
        {
            Name = name, Slot = ItemSlot.Weapon, Rarity = Rarity.Rare, Level = 5, WeaponDamage = 20f, Lines = { new ItemLine("health", 40f) },
        };

        public static int Run(Options options)
        {
            var report = new Report();
            var worlds = new WorldCache();
            var request = new WorldRequest(options.Int("seed", 303), "isle", 1);
            worlds.Get(request).Wait();

            NothingIsPickedUpByWalking(report, worlds, request);
            ClientsAreToldWhatItIs(report, worlds, request);
            ReachMatters(report, worlds, request);
            ClickingWalksTheHeroThere(report, worlds, request, options);
            ChangingYourMindStopsIt(report, worlds, request);
            OneFindOneHero(report, worlds, request);
            LatecomersSeeWhatsLying(report, worlds, request);
            AFullPackLeavesItThere(report, worlds, request);
            GoldComesByItself(report, worlds, request);
            ChestsPourOut(report, worlds, request);
            FindsLandWhereTheyCanBeReached(report, worlds, request);
            NonsenseIsIgnored(report, worlds, request);
            TheFallenTakeNothing(report, worlds, request);
            ABadLineLosesNothing(report, worlds, request);
            AnotherWorldStartsClean(report, worlds, request);
            LabelsAlwaysSettle(report);
            return report.Finish();
        }

        // Labels go up past each other, and the numbers here are from a game that froze doing it: lifted clear of this one, a label still touched it by a rounding error,
        // and lifting it again left it where it was, for as long as anyone would wait. Only near the top of the screen, where a pile of finds ends up.
        private static void LabelsAlwaysSettle(Report report)
        {
            report.Section("Labels piled up settle");
            const float Gap = 2f;

            var placed = new GuiRect(100f, BitConverter.UInt32BitsToSingle(0xc0c8a6e4), 85f, 24f);
            var box = new GuiRect(100f, 10f, 85f, 24f);
            report.Check("One in the way goes up past it", LabelStack.Lift(ref box, placed, Gap) && box.Bottom < placed.Y);
            report.Check("And is then done with it", !LabelStack.Lift(ref box, placed, Gap), $"at {box.Y}");

            var random = new Random(1);
            int stuck = 0, onTop = 0;
            for (int pile = 0; pile < 20000; pile++)
            {
                var boxes = new GuiRect[random.Next(2, 25)];
                for (int i = 0; i < boxes.Length; i++)
                {
                    boxes[i] = new GuiRect(400f + random.NextSingle() * 60f, random.NextSingle() * 180f - 60f, 60f + random.NextSingle() * 120f, 24f + random.Next(0, 6) * 1.5f);
                }

                // As LootLabels and UseMarks go round them, with a limit of what going up can take: past each of the others once.
                for (int i = 1; i < boxes.Length; i++)
                {
                    int lifts = 0;
                    for (int other = 0; other < i && lifts <= i; other++)
                    {
                        if (!LabelStack.Lift(ref boxes[i], boxes[other], Gap)) continue;
                        lifts++;
                        other = -1;
                    }
                    if (lifts > i) stuck++;
                    for (int other = 0; other < i; other++)
                    {
                        if (boxes[i].Intersects(boxes[other])) onTop++;
                    }
                }
            }
            report.Check("Every pile comes to rest", stuck == 0, $"{stuck} never did");
            report.Check("With nothing left on top of anything", onTop == 0, $"{onTop} overlaps");
        }

        private static void NothingIsPickedUpByWalking(Report report, WorldCache worlds, WorldRequest request)
        {
            report.Section("Nothing comes by itself");
            using var scene = new Scene(worlds, request);
            int carried = scene.Truth.Hero.Inventory.Count;
            (_, Vector2 at) = scene.Lay(Blade());
            scene.Lab.Run(1.5);

            scene.Go(at);
            scene.Lab.Run(3);
            report.Check("A hero standing on a find doesn't get it", scene.Truth.Hero.Inventory.Count == carried, $"carrying {scene.Truth.Hero.Inventory.Count}");
            report.Check("And it's still there", scene.Session.Ground.Count == 1);
        }

        private static void ClientsAreToldWhatItIs(Report report, WorldCache worlds, WorldRequest request)
        {
            report.Section("What a client sees");
            using var scene = new Scene(worlds, request);
            Vector2 from = scene.Truth.Sim.Position + new Vector2(3f, 0f);
            (int find, Vector2 at) = scene.Lay(Blade("Cruel Blade of Haste"), new Vector2(3f, 0f));
            scene.Lab.Run(1);

            report.Check("It knows what the find is, name and all", scene.Seen.Ground.TryGetValue(find, out Item? item) && item.Name == "Cruel Blade of Haste" && item.Lines.Count == 1);

            bool seen = false;
            foreach (Entity entity in scene.Player.Ecs.QueryEntities(new BitMask(typeof(PickupComponent), typeof(PositionComponent)), BitMask.Empty))
            {
                EntityView view = scene.Player.Ecs.GetEntityView(entity);
                var pickup = view.Get<PickupComponent>();
                if (pickup.Find != find) continue;
                seen = pickup.Grade == LootGrade.Rare && pickup.Slot == ItemSlot.Weapon && (pickup.From.Xz - from).Length < 0.1f && pickup.Born > 0 && (view.Get<PositionComponent>().value.Xz - at).Length < 0.1f;
            }
            report.Check("And has all it needs to show it falling", seen);
        }

        private static void ReachMatters(Report report, WorldCache worlds, WorldRequest request)
        {
            report.Section("Reach");
            using var scene = new Scene(worlds, request);
            int carried = scene.Truth.Hero.Inventory.Count;
            (int find, Vector2 at) = scene.Lay(Blade());
            scene.Lab.Run(1);

            scene.Go(at + new Vector2(40f, 0f));
            scene.Player.Client.PickUp(find);
            scene.Lab.Run(2);
            report.Check("Asking from across a field gets nothing", scene.Truth.Hero.Inventory.Count == carried);
            report.Check("And it stays on the ground for whoever does go", scene.Session.Ground.Count == 1);
            report.Check("Asking is forgotten, and doesn't wait for them to wander past", scene.Truth.Asked.Count == 0);

            scene.Go(at + new Vector2(Loot.Reach - 0.5f, 0f));
            scene.Seen.Taken.Clear();
            scene.Player.Client.PickUp(find);
            scene.Lab.Run(1);
            report.Check("Asking from close by gets it", scene.Truth.Hero.Inventory.Count == carried + 1);
            report.Check("And the client's own record says so", scene.Player.Client.Hero.Inventory.Any(i => i.Name == "Test Blade"));
            report.Check("And it's gone from the ground, for the client too", scene.Session.Ground.Count == 0 && !scene.Seen.Ground.ContainsKey(find));
        }

        private static void ClickingWalksTheHeroThere(Report report, WorldCache worlds, WorldRequest request, Options options)
        {
            report.Section("Clicking");
            using var scene = new Scene(worlds, request);
            int carried = scene.Truth.Hero.Inventory.Count;
            (int find, Vector2 at) = scene.Lay(Blade());
            scene.Lab.Run(1);

            scene.Go(at + new Vector2(22f, 4f));
            Vector2 start = scene.Truth.Sim.Position;
            scene.Seen.Fetch.Begin(find, at);
            scene.Lab.Run(6);
            report.Check("Clicking a find walks the hero to it and takes it", scene.Truth.Hero.Inventory.Count == carried + 1, $"carrying {scene.Truth.Hero.Inventory.Count}, {(scene.Truth.Sim.Position - at).Length:0.0} m away");
            report.Check("Having really walked there", (scene.Truth.Sim.Position - start).Length > 15f);
            report.Check("And that's the end of it", !scene.Seen.Fetch.Active);

            // Each with something standing between the hero and the find. Where a find lands is random, so one of these proves little: the failures were one in a few.
            WorldData world = scene.Session.World;
            var paths = new WorldPathfinder(exact: true);
            Vector2 site = scene.Truth.Sim.Position;
            var random = new WorldRandom(options.Int("seed", 303));
            int tried = 0, reached = 0;
            for (int attempt = 0; attempt < 400000 && tried < Walls; attempt++)
            {
                Vector2 a = site + new Vector2(random.Range(-300f, 300f), random.Range(-300f, 300f));
                Vector2 b = a + new Vector2(random.Range(-24f, 24f), random.Range(-24f, 24f));
                if (!world.InBounds(a) || !world.InBounds(b) || !world.Passable.IsSet(a) || !world.Passable.IsSet(b) || WorldPathfinder.HasClearLine(world, a, b)) continue;
                if (paths.Find(world, a, b) is not { Count: > 3 and < 45 } way || (way[^1] - b).Length > 2f) continue;

                scene.Go(b);
                string name = $"Behind A Wall {tried}";
                (int behind, Vector2 lying) = scene.Lay(Blade(name));
                scene.Lab.Run(0.4);
                scene.Go(a);
                scene.Seen.Fetch.Begin(behind, lying);
                scene.Lab.Run(14);
                tried++;
                if (scene.Truth.Hero.Inventory.Any(i => i.Name == name)) reached++;
                else report.Note($"from {a} to {lying}: stopped {(scene.Truth.Sim.Position - lying).Length:0.0} m short at {scene.Truth.Sim.Position}, clear line {WorldPathfinder.HasClearLine(world, a, lying)}, path {paths.Find(world, a, lying)?.Count ?? -1}, still going {scene.Seen.Fetch.Active}");
            }
            report.Check("Heroes get around what's in the way", tried >= Walls && reached == tried, $"{reached} of {tried}");
        }

        private static void ChangingYourMindStopsIt(Report report, WorldCache worlds, WorldRequest request)
        {
            report.Section("Changing your mind");
            using var scene = new Scene(worlds, request);
            (int find, Vector2 at) = scene.Lay(Blade());
            scene.Lab.Run(1);

            foreach ((string what, HeroIntent intent) in new[] { ("moving", new HeroIntent { Move = Vector2.UnitX }), ("dodging", new HeroIntent { Dash = true }), ("a skill", Used()) })
            {
                scene.Seen.Fetch.Begin(find, at + new Vector2(50f, 0f));
                HeroIntent input = intent;
                scene.Seen.Fetch.Steer(scene.Seen, ref input);
                report.Check($"Anything else the player does calls it off: {what}", !scene.Seen.Fetch.Active);
            }

            static HeroIntent Used()
            {
                var used = new HeroIntent();
                used.Use(0);
                return used;
            }

            int carried = scene.Truth.Hero.Inventory.Count;
            scene.Go(at + new Vector2(25f, 0f));
            scene.Seen.Fetch.Begin(find, at);
            scene.Lab.Run(0.3);
            scene.Seen.Fetch.Cancel();
            scene.Lab.Run(3);
            report.Check("And a walk called off takes nothing", scene.Truth.Hero.Inventory.Count == carried);
            report.Check("And goes no further", (scene.Truth.Sim.Position - at).Length > Loot.ServerReach);
        }

        private static void OneFindOneHero(Report report, WorldCache worlds, WorldRequest request)
        {
            report.Section("Two heroes, one find");
            using var scene = new Scene(worlds, request);
            LabPlayer friend = scene.Lab.AddPlayer("Friend", Line, 2);
            friend.Fights = false;
            scene.Lab.Run(4);
            ServerPlayer other = scene.Lab.Server.Heroes[1];
            int mine = scene.Truth.Hero.Inventory.Count;
            int theirs = other.Hero.Inventory.Count;

            (int find, Vector2 at) = scene.Lay(Blade());
            scene.Lab.Run(1);
            report.Check("Both are told about it", friend.Client.Session!.Ground.ContainsKey(find) && scene.Seen.Ground.ContainsKey(find));

            scene.Go(at);
            friend.Client.TravelTo(at);
            scene.Lab.Run(0.6);
            scene.Player.Client.PickUp(find);
            friend.Client.PickUp(find);
            scene.Lab.Run(1);

            int gained = scene.Truth.Hero.Inventory.Count - mine + other.Hero.Inventory.Count - theirs;
            report.Check("Reaching for it together, exactly one gets it", gained == 1, $"{gained} did");
            report.Check("And neither client thinks it's still there", !scene.Seen.Ground.ContainsKey(find) && !friend.Client.Session!.Ground.ContainsKey(find));
            report.Check("Nobody's drawn it as taken for ever", scene.Seen.Taken.Count == 0 && friend.Client.Session!.Taken.Count == 0);

            (int second, Vector2 lying) = scene.Lay(Blade("Second"));
            scene.Lab.Run(1);
            scene.Go(lying + new Vector2(60f, 0f));
            scene.Seen.Fetch.Begin(second, lying);
            scene.Lab.Run(0.3);
            friend.Client.TravelTo(lying);
            scene.Lab.Run(0.6);
            friend.Client.PickUp(second);
            scene.Lab.Run(0.6);
            report.Check("Someone else taking what a hero is walking to calls the walk off", !scene.Seen.Fetch.Active);
        }

        private static void LatecomersSeeWhatsLying(Report report, WorldCache worlds, WorldRequest request)
        {
            report.Section("Arriving late");
            using var scene = new Scene(worlds, request);
            (int find, Vector2 at) = scene.Lay(Blade("Waiting For You"));
            scene.Lab.Run(2);

            LabPlayer late = scene.Lab.AddPlayer("Latecomer", Line, 3);
            late.Fights = false;
            scene.Lab.Run(5);
            report.Check("Someone arriving is told what's on the ground", late.Client.Session!.Ground.TryGetValue(find, out Item? item) && item.Name == "Waiting For You");

            late.Client.TravelTo(at);
            scene.Lab.Run(0.8);
            late.Client.PickUp(find);
            scene.Lab.Run(1);
            report.Check("And can take it", late.Client.Hero.Inventory.Any(i => i.Name == "Waiting For You"));
        }

        private static void AFullPackLeavesItThere(Report report, WorldCache worlds, WorldRequest request)
        {
            report.Section("A full pack");
            using var scene = new Scene(worlds, request);
            (int find, Vector2 at) = scene.Lay(Blade());
            scene.Lab.Run(1);
            scene.Go(at);

            // Filled on the server alone, as when two things are taken in a moment and the client has yet to hear of the first.
            while (scene.Truth.Hero.Inventory.Count < HeroState.InventorySize) scene.Truth.Hero.Inventory.Add(Blade("Filler"));
            scene.Player.Client.PickUp(find);
            scene.Lab.Run(1);
            report.Check("With no room, it stays on the ground", scene.Session.Ground.Count == 1 && scene.Truth.Hero.Inventory.Count == HeroState.InventorySize);
            report.Check("And the player is told why", scene.Seen.Log.Any(l => l.Text.Contains("full")));
            report.Check("And nothing's been lost", scene.Seen.Ground.ContainsKey(find));

            scene.Truth.GearChanged = true;
            scene.Lab.Run(1);
            scene.Seen.Taken.Clear();
            bool? room = null;
            scene.Player.Client.Reached += (_, answer) => room = answer;
            scene.Player.Client.PickUp(find);
            report.Check("A client that knows it's full says so at once", room == false);
            report.Check("And doesn't hide what it couldn't take", !scene.Seen.Taken.ContainsKey(find));

            scene.Truth.Hero.Inventory.RemoveAt(0);
            scene.Truth.GearChanged = true;
            scene.Lab.Run(1);
            scene.Player.Client.PickUp(find);
            scene.Lab.Run(1);
            report.Check("With room again, it can be taken", scene.Session.Ground.Count == 0);
        }

        private static void GoldComesByItself(Report report, WorldCache worlds, WorldRequest request)
        {
            report.Section("Gold");
            using var scene = new Scene(worlds, request);
            int before = scene.Truth.Hero.Gold;
            Vector2 at = scene.Truth.Sim.Position;
            var ground = new Vector3(at.X, scene.Session.World.Height.Sample(at), at.Y);
            scene.Session.Ground.Spill(ground, Array.Empty<Item>(), 300, 3);
            scene.Lab.Run(0.2);
            report.Check("Gold can't be had while it's in the air", scene.Truth.Hero.Gold == before);
            scene.Lab.Run(3);
            report.Check("It comes to whoever is standing there", scene.Truth.Hero.Gold == before + 300, $"{scene.Truth.Hero.Gold - before} of 300");
            report.Check("It's nothing to do with finds", scene.Session.Ground.Count == 0);
        }

        private static void ChestsPourOut(Report report, WorldCache worlds, WorldRequest request)
        {
            report.Section("Chests");
            using var scene = new Scene(worlds, request);
            Vector2 at = scene.Truth.Sim.Position;
            var ground = new Vector3(at.X, scene.Session.World.Height.Sample(at), at.Y);
            uint opened = scene.Lab.Server.Tick;
            scene.Session.Combat.OpenChest(scene.Truth, ground, 8, LootTier.Hoard);
            report.Check("A hoard gives what its table says", scene.Session.Ground.Count >= 5 && scene.Session.Ground.All.All(l => Loot.GradeOf(l.item) >= LootGrade.Rare) && scene.Session.Ground.All.Any(l => Loot.GradeOf(l.item) >= LootGrade.Masterwork));

            scene.Lab.Run(0.1);
            var bySoonest = new List<(uint born, LootGrade grade)>();
            foreach (Entity entity in scene.Lab.Server.Ecs.QueryEntities(new BitMask(typeof(PickupComponent)), BitMask.Empty))
            {
                var pickup = scene.Lab.Server.Ecs.GetEntityView(entity).Get<PickupComponent>();
                if (pickup.Find != 0) bySoonest.Add((pickup.Born, pickup.Grade));
            }
            bySoonest.Sort();

            bool bestLast = true;
            for (int i = 1; i < bySoonest.Count; i++) bestLast &= bySoonest[i].grade >= bySoonest[i - 1].grade && bySoonest[i].born > bySoonest[i - 1].born;
            report.Check("It all comes out one after another, with the best last", bestLast);
            report.Check("And not before the lid is up", bySoonest[0].born >= opened + (uint)(GroundLoot.ChestDelay * NetTime.TicksPerSecond));
        }

        private static void FindsLandWhereTheyCanBeReached(Report report, WorldCache worlds, WorldRequest request)
        {
            report.Section("Where things land");
            using var scene = new Scene(worlds, request);
            WorldData world = scene.Session.World;
            int bad = 0, total = 0;
            foreach (WorldSite site in world.Sites)
            {
                foreach (SiteSpawn spawn in site.Spawns.Take(6))
                {
                    var ground = new Vector3(spawn.Position.X, world.Height.Sample(spawn.Position), spawn.Position.Y);
                    var before = scene.Session.Ground.All.Select(l => l.find).ToHashSet();
                    scene.Session.Ground.Spill(ground, new[] { Blade() }, 0, 0);
                    (_, _, Vector2 at) = scene.Session.Ground.All.First(l => !before.Contains(l.find));
                    total++;

                    // With nowhere better, a find stays where its owner fell, which they could stand on.
                    bool stayed = (at - spawn.Position).LengthSquared < 0.01f;
                    if (!stayed && (!world.Passable.IsSet(at) || world.WaterDepthAt(at) > 0.3f)) bad++;
                }
            }
            report.Check($"All of {total} finds came down on ground a hero can stand on", bad == 0, $"{bad} didn't");

            // Against the worst of it: everything that stands in the land, with a find thrown from right beside it.
            var paths = new WorldPathfinder(exact: true);
            var random = new WorldRandom(8);
            int cut = 0, thrown = 0;
            for (int attempt = 0; attempt < 60000 && thrown < 300; attempt++)
            {
                var from = new Vector2(random.Range(0f, world.Size), random.Range(0f, world.Size));
                if (!world.Passable.IsSet(from) || world.Passable.IsSet(from + Vector2.UnitX * 2.5f)) continue;

                var ground = new Vector3(from.X, world.Height.Sample(from), from.Y);
                var before = scene.Session.Ground.All.Select(l => l.find).ToHashSet();
                scene.Session.Ground.Spill(ground, new[] { Blade() }, 0, 0);
                (_, _, Vector2 at) = scene.Session.Ground.All.First(l => !before.Contains(l.find));
                thrown++;
                if ((at - from).LengthSquared > 0.01f && paths.Find(world, from, at) is not { Count: > 0 } way) cut++;
                else if ((at - from).LengthSquared > 0.01f && (paths.Find(world, from, at)![^1] - at).Length > 2f) cut++;
            }
            report.Check($"And none of {thrown} thrown from beside a wall went over it", thrown >= 300 && cut == 0, $"{cut} cut off");
        }

        private static void NonsenseIsIgnored(Report report, WorldCache worlds, WorldRequest request)
        {
            report.Section("Nonsense");
            using var scene = new Scene(worlds, request);
            for (int i = 0; i < 200; i++) scene.Session.Ground.Ask(scene.Truth, 1000 + i);
            report.Check("Nobody can pile up requests", scene.Truth.Asked.Count <= 8, $"{scene.Truth.Asked.Count}");
            scene.Lab.Run(0.5);
            report.Check("Asking for what doesn't exist does nothing", scene.Truth.Asked.Count == 0 && scene.Player.DisconnectReason == null);
            scene.Player.Client.PickUp(987654);
            scene.Lab.Run(0.5);
            report.Check("A client asking for what it's never heard of does nothing", scene.Seen.Taken.Count == 0 && scene.Player.DisconnectReason == null);
        }

        private static void TheFallenTakeNothing(Report report, WorldCache worlds, WorldRequest request)
        {
            report.Section("The fallen");
            using var scene = new Scene(worlds, request);
            int carried = scene.Truth.Hero.Inventory.Count;
            (int find, Vector2 at) = scene.Lay(Blade());
            scene.Lab.Run(1);
            scene.Go(at);

            scene.Truth.Actor.Vitals.Health = 0f;
            scene.Lab.Run(0.2);
            scene.Session.Ground.Ask(scene.Truth, find);
            scene.Lab.Run(0.5);
            report.Check("Nobody picks anything up while they're down", scene.Truth.Hero.Inventory.Count == carried);
            scene.Lab.Run(8);
            report.Check("And what they fell beside waits for them", scene.Session.Ground.Count == 1);
        }

        private static void ABadLineLosesNothing(Report report, WorldCache worlds, WorldRequest request)
        {
            report.Section("On the worst line there is");
            NetworkConditions awful = Scenarios.All.First(s => s.name == "Awful").conditions;
            using var scene = new Scene(worlds, request, awful);
            int carried = scene.Truth.Hero.Inventory.Count;

            var finds = new List<(int find, Vector2 at)>();
            for (int i = 0; i < 6; i++) finds.Add(scene.Lay(Blade($"Blade {i}")));
            scene.Lab.Run(5);
            report.Check("Every find is heard of", finds.All(f => scene.Seen.Ground.ContainsKey(f.find)));

            foreach ((int find, Vector2 at) in finds)
            {
                scene.Seen.Fetch.Begin(find, at);
                scene.Lab.Run(6);
            }
            scene.Lab.Run(3);
            report.Check("And walked to and taken, all of them", scene.Truth.Hero.Inventory.Count == carried + finds.Count, $"{scene.Truth.Hero.Inventory.Count - carried} of {finds.Count}");
            report.Check("And the client ends up with what the server has", scene.Player.Client.Hero.Inventory.Count == scene.Truth.Hero.Inventory.Count);
        }

        private static void AnotherWorldStartsClean(Report report, WorldCache worlds, WorldRequest request)
        {
            report.Section("Moving on");
            using var scene = new Scene(worlds, request);
            (int find, _) = scene.Lay(Blade("Left Behind"));
            scene.Lab.Run(1);
            scene.Session.Ground.Ask(scene.Truth, find);

            var next = new WorldRequest(404, "isle", 2);
            worlds.Get(next).Wait();
            scene.Player.Client.StepThroughWaygate(next, new Offering());
            scene.Lab.Run(6);

            report.Check("What was left behind is gone", scene.Session.Ground.Count == 0 && scene.Seen.Ground.Count == 0);
            report.Check("And so is any request for it", scene.Truth.Asked.Count == 0);

            (int fresh, Vector2 at) = scene.Lay(Blade("New World"));
            scene.Lab.Run(1);
            report.Check("Finds in the new world are heard of", scene.Seen.Ground.TryGetValue(fresh, out Item? item) && item.Name == "New World");
            scene.Go(at);
            scene.Player.Client.PickUp(fresh);
            scene.Lab.Run(1);
            report.Check("And can be taken", scene.Truth.Hero.Inventory.Any(i => i.Name == "New World"));
        }
    }
}

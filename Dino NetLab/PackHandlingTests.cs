using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Server;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Networking;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_NetLab
{
    /// <summary>
    /// What a pack does with a server on the other end of a line. A client changes what's carried the moment it's asked to and tells the server, which has the last word, so the things to prove are that
    /// what's shown never jumps back while the server catches up, that the two always end up agreeing, and that nothing is lost or doubled however bad the line is.
    /// </summary>
    internal static class PackHandlingTests
    {
        private static readonly NetworkConditions Line = new NetworkConditions { Latency = 80, Jitter = 10, LossChance = 0.01 };
        private static readonly NetworkConditions Awful = new NetworkConditions { Latency = 300, Jitter = 150, LossChance = 0.15, DuplicateChance = 0.05, SpikeChance = 0.005, SpikeLength = 500 };

        private sealed class Scene : IDisposable
        {
            public readonly GameLab Lab;
            public readonly LabPlayer Player;
            public readonly ServerPlayer Truth;
            public readonly int Site;

            public Scene(WorldCache worlds, WorldRequest request, HeroState hero, NetworkConditions? line = null)
            {
                Lab = new GameLab(worlds, request, "Landing");
                Player = Lab.AddPlayer("Hero", line ?? Line, 1, hero: hero);
                Player.Fights = false;
                Lab.Run(line != null ? 8 : 4);
                Truth = Lab.Server.Heroes[0];
                Site = Lab.Server.Session!.Sites.FindIndex(s => s.Site.Kind == SiteKind.Village);
            }

            public HeroState Shown => Player.Client.Hero;
            public HeroState Real => Truth.Hero;
            public ServerSession Session => Lab.Server.Session!;

            /// <summary>The merchant's table, as a player walking up to them would be shown it.</summary>
            public MerchantTable OpenTable()
            {
                Lab.Server.OpenPanel(Truth, InteractKind.Merchant, Site);
                Lab.Run(1);
                return Player.Client.Session!.Sites[Site].Table;
            }

            public void Dispose() => Lab.Dispose();
        }

        private static int _named;

        private static Item Thing(ItemSlot slot, string name = "", WeaponKind kind = WeaponKind.Sword) => new Item
        {
            Name = name.Length > 0 ? name : $"Thing {++_named}", Slot = slot, Kind = kind, Rarity = Rarity.Magic, Level = 10, Seed = (ulong)(1000 + ++_named), Lines = { new ItemLine("gold-find", 5f) },
        };

        private static HeroState Hero(params (int cell, Item item)[] things)
        {
            HeroState hero = HeroState.CreateNew();
            hero.Gold = 100000;
            foreach ((int cell, Item item) in things) hero.Inventory[cell] = item;
            return hero;
        }

        private static string Names(HeroState hero) => string.Join(",", hero.Inventory.Select((item, cell) => item != null ? $"{cell}:{item.Name}" : "").Where(cell => cell.Length > 0))
            + "|" + string.Join(",", hero.Equipped.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value.Name}"));

        public static int Run(Options options)
        {
            var report = new Report();
            var worlds = new WorldCache();
            var request = new WorldRequest(options.Int("seed", 303), "isle", 1);
            worlds.Get(request).Wait();

            ShownAtOnce(report, worlds, request);
            AnEarlierAnswerUndoesNothing(report, worlds, request);
            WhatTheServerRefusesComesBack(report, worlds, request);
            Equipping(report, worlds, request);
            SellingAndBuyingBack(report, worlds, request);
            ATableIsForOneLand(report, worlds, request);
            Dropping(report, worlds, request);
            Sorting(report, worlds, request);
            ABadLine(report, worlds, request);
            return report.Finish();
        }

        private static void ShownAtOnce(Report report, WorldCache worlds, WorldRequest request)
        {
            report.Section("Moving something");
            Item ring = Thing(ItemSlot.Finger, "Ring"), other = Thing(ItemSlot.Finger, "Other");
            using var scene = new Scene(worlds, request, Hero((3, ring), (9, other)));

            scene.Player.Client.Move(ItemPlace.Cell(3), ItemPlace.Cell(20));
            report.Check("It shows at once, with the server yet to hear", scene.Shown.Inventory[20]?.Name == "Ring" && scene.Shown.Inventory[3] == null && scene.Real.Inventory[3]?.Name == "Ring");

            scene.Lab.Run(1);
            report.Check("And the server ends up the same", Names(scene.Real) == Names(scene.Shown) && scene.Real.Inventory[20]?.Name == "Ring");

            scene.Player.Client.Move(ItemPlace.Cell(9), ItemPlace.Cell(20));
            scene.Lab.Run(1);
            report.Check("Onto something, the two change places on both ends", scene.Real.Inventory[20]?.Name == "Other" && scene.Real.Inventory[9]?.Name == "Ring" && Names(scene.Real) == Names(scene.Shown));
        }

        // The whole point of the number: an answer that left before the server had heard the last thing asked says the pack is as it was, and showing that would make things jump back and forth.
        private static void AnEarlierAnswerUndoesNothing(Report report, WorldCache worlds, WorldRequest request)
        {
            report.Section("What's on its way");
            Item ring = Thing(ItemSlot.Finger, "Ring");
            using var scene = new Scene(worlds, request, Hero((3, ring)));

            // Something found, and the server telling them. It has to have left by the time the player does anything, or it's no answer from before the move: this is what's on its way when they do.
            scene.Real.Pickup(Thing(ItemSlot.Feet, "Found"));
            scene.Truth.GearChanged = true;
            while (scene.Truth.GearChanged) scene.Lab.Run(1.0 / 60.0);
            report.Check("The answer has left, and hasn't arrived", !scene.Shown.Carried().Any(i => i.Name == "Found"));
            scene.Player.Client.Move(ItemPlace.Cell(3), ItemPlace.Cell(30));

            bool jumpedBack = false;
            scene.Lab.Run(1.5, () => jumpedBack |= scene.Shown.Inventory[30]?.Name != "Ring");
            report.Check("An answer from before the move doesn't undo it, even for a moment", !jumpedBack);
            report.Check("And what it had to say isn't lost", Names(scene.Real) == Names(scene.Shown) && scene.Shown.Carried().Any(i => i.Name == "Found"));
        }

        private static void WhatTheServerRefusesComesBack(Report report, WorldCache worlds, WorldRequest request)
        {
            report.Section("When the server says no");
            Item ring = Thing(ItemSlot.Finger, "Ring");
            using var scene = new Scene(worlds, request, Hero((3, ring)));

            // As if the two had fallen out: the server's hero has something the client hasn't heard of yet, which makes the move mean something else.
            scene.Real.Inventory[3] = null;
            scene.Player.Client.Move(ItemPlace.Cell(3), ItemPlace.Cell(8));
            report.Check("A client can be wrong for a moment", scene.Shown.Inventory[8]?.Name == "Ring");
            scene.Lab.Run(1);
            report.Check("But it's told the truth, even though nothing changed", Names(scene.Shown) == Names(scene.Real) && scene.Shown.Inventory[8] == null);

            MerchantTable table = scene.OpenTable();
            Item wanted = table.Stock.First(item => item != null)!;
            int index = Array.IndexOf(table.Stock, wanted);
            scene.Shown.Gold = wanted.BuyValue;
            scene.Real.Gold = 0;
            scene.Player.Client.Buy(scene.Site, index);
            report.Check("A client that thinks it can pay shows it bought", table.Stock[index] == null);

            // With something else asked for right behind it, which is what the table's answer mustn't be held up by: nothing would ever put it right.
            scene.Player.Client.Equip(0);
            scene.Lab.Run(1);
            report.Check("And it goes back on the table when it couldn't", table.Stock[index] != null && !scene.Shown.Carried().Any() && scene.Shown.Gold == 0);
        }

        private static void Equipping(Report report, WorldCache worlds, WorldRequest request)
        {
            report.Section("What's worn");
            Item blade = Thing(ItemSlot.MainHand, "New Blade"), ring = Thing(ItemSlot.Finger, "Ring"), keeps = Thing(ItemSlot.Feet, "Stays Put");
            using var scene = new Scene(worlds, request, Hero((12, blade), (5, ring), (30, keeps)));
            string oldBlade = scene.Real.Equipped[WornSlot.MainHand].Name;

            float findBefore = scene.Player.Client.Sheet.GoldFind;
            scene.Player.Client.Equip(12);
            report.Check("Equipping shows at once, in the same cell", scene.Shown.Equipped[WornSlot.MainHand].Name == "New Blade" && scene.Shown.Inventory[12]?.Name == oldBlade);
            report.Check("And the sheet goes with it, with no waiting", scene.Player.Client.Sheet.GoldFind > findBefore, $"{findBefore} to {scene.Player.Client.Sheet.GoldFind}");
            scene.Lab.Run(1);
            report.Check("Both ends agree on what changed places", scene.Real.Equipped[WornSlot.MainHand].Name == "New Blade" && scene.Real.Inventory[12]?.Name == oldBlade && Names(scene.Real) == Names(scene.Shown));
            report.Check("And nothing else moved", scene.Real.Inventory[5]?.Name == "Ring" && scene.Real.Inventory[30]?.Name == "Stays Put");

            scene.Player.Client.Move(ItemPlace.Cell(5), ItemPlace.Worn(WornSlot.Finger2));
            scene.Lab.Run(1);
            report.Check("Moving onto a place puts it there", scene.Real.Equipped.ContainsKey(WornSlot.Finger2) && scene.Real.Inventory[5] == null && Names(scene.Real) == Names(scene.Shown));

            float before = scene.Truth.Actor.Sheet.GoldFind;
            scene.Player.Client.Move(ItemPlace.Worn(WornSlot.Finger2), ItemPlace.Cell(7));
            scene.Lab.Run(1);
            report.Check("Moving it off puts it where it's dropped", scene.Real.Inventory[7]?.Name == "Ring" && !scene.Real.Equipped.ContainsKey(WornSlot.Finger2));
            report.Check("And what it gave goes with it", scene.Truth.Actor.Sheet.GoldFind < before, $"{before} to {scene.Truth.Actor.Sheet.GoldFind}");
        }

        private static void SellingAndBuyingBack(Report report, WorldCache worlds, WorldRequest request)
        {
            report.Section("A merchant");
            Item ring = Thing(ItemSlot.Finger, "Ring"), keeps = Thing(ItemSlot.Feet, "Stays Put");
            using var scene = new Scene(worlds, request, Hero((4, ring), (5, keeps)));
            MerchantTable table = scene.OpenTable();
            report.Check("There's a table, with nothing of the hero's on it", table.Stock.Any(item => item != null) && table.Sold.All(item => item == null));

            int gold = scene.Real.Gold;
            scene.Player.Client.Sell(scene.Site, ItemPlace.Cell(4));
            report.Check("Selling shows at once", scene.Shown.Inventory[4] == null && table.Sold[0]?.Name == "Ring" && scene.Shown.Gold == gold + ring.SellValue);
            scene.Lab.Run(1);
            report.Check("Selling pays, on both ends", scene.Real.Gold == gold + ring.SellValue && scene.Shown.Gold == scene.Real.Gold);
            report.Check("And leaves the gap, on both ends", scene.Real.Inventory[4] == null && scene.Real.Inventory[5]?.Name == "Stays Put" && Names(scene.Real) == Names(scene.Shown));
            report.Check("And it's on the table", table.Sold[0]?.Name == "Ring");

            scene.Player.Client.Buy(scene.Site, 0, buyingBack: true, cell: 4);
            scene.Lab.Run(1);
            report.Check("Buying it back costs what it paid", scene.Real.Gold == gold && scene.Shown.Gold == gold);
            report.Check("And it's back where it was", scene.Real.Inventory[4]?.Name == "Ring" && Names(scene.Real) == Names(scene.Shown));
            report.Check("And gone from the table", table.Sold[0] == null);

            int first = Array.FindIndex(table.Stock, item => item != null);
            int beside = Array.FindIndex(table.Stock, first + 1, item => item != null);
            string nextName = table.Stock[beside]!.Name;
            scene.Player.Client.Buy(scene.Site, first, cell: 20);
            scene.Player.Client.Buy(scene.Site, first, cell: 21);
            scene.Lab.Run(1);
            report.Check("A double click on a table buys one thing", scene.Real.Inventory[20] != null && scene.Real.Inventory[21] == null && Names(scene.Real) == Names(scene.Shown));
            report.Check("And what's beside it stays where it is", table.Stock[first] == null && table.Stock[beside]?.Name == nextName);

            scene.Player.Client.Sell(scene.Site, ItemPlace.Worn(WornSlot.Body));
            scene.Lab.Run(1);
            report.Check("What's being worn can be sold from the hero", !scene.Real.Equipped.ContainsKey(WornSlot.Body) && Names(scene.Real) == Names(scene.Shown) && table.Sold.Any(item => item != null));

            for (int cell = 0; cell < 8; cell++) scene.Shown.Inventory[cell] = scene.Real.Inventory[cell];
            scene.Player.Client.Sell(scene.Site, ItemPlace.Cell(5));
            scene.Player.Client.Move(ItemPlace.Cell(4), ItemPlace.Cell(6));
            scene.Lab.Run(1);
            report.Check("A table's own answer counts, even with another move behind it", Names(scene.Real) == Names(scene.Shown) && table.Sold.Count(item => item != null) == 2);
        }

        private static void ATableIsForOneLand(Report report, WorldCache worlds, WorldRequest request)
        {
            report.Section("Leaving");
            Item ring = Thing(ItemSlot.Finger, "Ring");
            using var scene = new Scene(worlds, request, Hero((4, ring)));
            MerchantTable table = scene.OpenTable();
            scene.Player.Client.Sell(scene.Site, ItemPlace.Cell(4));
            scene.Lab.Run(1);
            report.Check("Something's on the table", table.Sold[0] != null);

            MerchantTable again = scene.OpenTable();
            report.Check("Coming back to it finds it still there", again.Sold[0]?.Name == "Ring");

            worlds.Get(Lands.Haven).Wait();
            scene.Lab.Server.SendHome();
            scene.Lab.Run(10);
            report.Check("Another world has nothing kept for the hero", scene.Truth.Tables.Count == 0);
        }

        private static void Dropping(Report report, WorldCache worlds, WorldRequest request)
        {
            report.Section("Putting something down");
            Item ring = Thing(ItemSlot.Finger, "Ring"), keeps = Thing(ItemSlot.Feet, "Stays Put");
            ring.Rarity = Rarity.Unique;
            using var scene = new Scene(worlds, request, Hero((4, ring), (5, keeps)));

            scene.Player.Client.Drop(ItemPlace.Cell(4));
            report.Check("It's gone from the pack at once", scene.Shown.Inventory[4] == null);
            scene.Lab.Run(1.5);
            report.Check("And from the server's", scene.Real.Inventory[4] == null && scene.Real.Inventory[5]?.Name == "Stays Put" && Names(scene.Real) == Names(scene.Shown));
            report.Check("It's lying there", scene.Session.Ground.Count == 1);

            (int find, Item lying, Vector2 at) = scene.Session.Ground.All.First();
            report.Check("Beside the hero", (at - scene.Truth.Sim.Position).Length < 8f, $"{(at - scene.Truth.Sim.Position).Length:0.0} m");
            report.Check("And the client knows what it is", scene.Player.Client.Session!.Ground.TryGetValue(find, out Item? seen) && seen.Name == "Ring");

            PickupComponent? pickup = null;
            foreach (Entity entity in scene.Player.Ecs.QueryEntities(new BitMask(typeof(PickupComponent)), BitMask.Empty)) pickup = scene.Player.Ecs.GetEntityView(entity).Get<PickupComponent>();
            report.Check("Marked as put down, so that nobody hears a unique land", pickup is { Laid: true });

            scene.Player.Client.TravelTo(at);
            scene.Lab.Run(0.6);
            scene.Player.Client.PickUp(find);
            scene.Lab.Run(1);
            report.Check("And it can be picked up again, as it was", scene.Session.Ground.Count == 0 && scene.Real.Carried().Any(i => i.Name == "Ring" && i.Rarity == Rarity.Unique) && scene.Shown.Carried().Any(i => i.Name == "Ring"));

            float armed = scene.Truth.Actor.Sheet.Profile(0).First.AverageHit;
            scene.Player.Client.Drop(ItemPlace.Worn(WornSlot.MainHand));
            scene.Lab.Run(1.5);
            report.Check("What's worn can be put down too, and the hero's bare-handed for it", !scene.Real.Equipped.ContainsKey(WornSlot.MainHand) && scene.Session.Ground.Count == 1 && scene.Truth.Actor.Sheet.Profile(0).First.AverageHit != armed);

            int lying2 = scene.Session.Ground.Count;
            scene.Player.Client.Drop(ItemPlace.Cell(20));
            scene.Player.Client.Drop(new ItemPlace(-1));
            scene.Lab.Run(1);
            report.Check("An empty cell puts nothing down", scene.Session.Ground.Count == lying2 && scene.Player.DisconnectReason == null);
        }

        private static void Sorting(Report report, WorldCache worlds, WorldRequest request)
        {
            report.Section("Sorting");
            using var scene = new Scene(worlds, request, Hero((30, Thing(ItemSlot.Finger)), (2, Thing(ItemSlot.Head)), (17, Thing(ItemSlot.MainHand))));
            string before = Names(scene.Real);
            scene.Lab.Run(2);
            report.Check("Nothing sorts itself", Names(scene.Real) == before && Names(scene.Shown) == before);

            scene.Player.Client.SortPack();
            report.Check("Asked to, it shows at once", scene.Shown.Inventory[0] != null && scene.Shown.Inventory[1] != null && scene.Shown.Inventory[2] != null && scene.Shown.Inventory[30] == null);
            scene.Lab.Run(1);
            report.Check("And the server comes to the same thing", Names(scene.Real) == Names(scene.Shown) && scene.Real.Inventory[30] == null);
        }

        private static void ABadLine(Report report, WorldCache worlds, WorldRequest request)
        {
            report.Section("A bad line");
            var things = new List<(int, Item)>();
            for (int i = 0; i < 24; i++) things.Add((i * 3 % HeroState.InventorySize, Thing((ItemSlot)(i % 9), kind: (WeaponKind)(i % 8))));
            using var scene = new Scene(worlds, request, Hero(things.ToArray()), Awful);
            MerchantTable table = scene.OpenTable();
            int total = scene.Real.Carried().Count() + scene.Real.Equipped.Count;

            var random = new Random(9);
            ItemPlace Anywhere() => new ItemPlace(random.Next(HeroState.InventorySize + 10));
            bool everHadTooMany = false;
            for (int round = 0; round < 400; round++)
            {
                switch (random.Next(5))
                {
                    case 0:
                    case 1:
                        scene.Player.Client.Move(Anywhere(), Anywhere());
                        break;
                    case 2:
                        scene.Player.Client.Equip(random.Next(HeroState.InventorySize));
                        break;
                    case 3:
                        scene.Player.Client.Unequip((WornSlot)random.Next(10));
                        break;
                    default:
                        scene.Player.Client.SortPack();
                        break;
                }
                scene.Lab.Run(0.03);
                if (scene.Real.Carried().Count() + scene.Real.Equipped.Count > total) everHadTooMany = true;
            }

            scene.Lab.Run(12);
            report.Check("Four hundred changes on an awful line leave both ends the same", Names(scene.Real) == Names(scene.Shown), $"{Names(scene.Real)} against {Names(scene.Shown)}");
            report.Check("Nothing was lost", scene.Real.Carried().Count() + scene.Real.Equipped.Count == total, $"{scene.Real.Carried().Count() + scene.Real.Equipped.Count} of {total}");
            report.Check("Nothing was doubled, at any point", !everHadTooMany);
            report.Check("The line held", scene.Player.DisconnectReason == null, scene.Player.DisconnectReason ?? "");

            int gold = scene.Real.Gold;
            int cell = Array.FindIndex(scene.Real.Inventory, item => item != null);
            Item sold = scene.Real.Inventory[cell]!;
            scene.Player.Client.Sell(scene.Site, ItemPlace.Cell(cell));
            scene.Player.Client.Buy(scene.Site, 0, buyingBack: true, cell: cell);
            scene.Player.Client.Sell(scene.Site, ItemPlace.Cell(cell));
            scene.Lab.Run(8);
            report.Check("A sale, a change of heart and a sale come out as one sale", scene.Real.Gold == gold + sold.SellValue && scene.Shown.Gold == scene.Real.Gold && table.Sold.Count(item => item != null) == 1, $"{gold} to {scene.Real.Gold}");
        }
    }
}

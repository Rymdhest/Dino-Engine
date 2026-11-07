using Dino_Defenders.Net;
using Dino_Defenders.Progression;
using Dino_Defenders.Tooltips;
using Dino_Engine.Networking;
using Dino_Engine.World;

namespace Dino_BuildLab
{
    /// <summary>
    /// What a pack promises: everything stays where it was put, and nothing that moves can get lost or doubled on the way. Selling and buying back are in here too, as they're the other way in and
    /// out, and so is finding things. How much of this reaches a client, and how soon, is <c>Dino NetLab pack</c>.
    /// </summary>
    internal static class PackChecks
    {
        private static int _named;

        public static void Run(Report report)
        {
            report.Section("A pack stays as it was left");
            Cells(report);
            SwappingWithWhatsWorn(report);
            BothHands(report);
            Moving(report);
            Sorting(report);
            NothingGetsLost(report);

            report.Section("A merchant's table");
            Selling(report);
            BuyingBack(report);
            AFullTable(report);

            report.Section("Finding things");
            Searching(report);

            report.Section("Packs that come from elsewhere");
            OldPacks(report);
            Wire(report);
        }

        private static Item Thing(ItemSlot slot, WeaponKind kind = WeaponKind.Sword) => new Item { Name = $"Thing {++_named}", Slot = slot, Kind = kind, Level = 5, Rarity = Rarity.Magic, Seed = (ulong)_named };

        private static Item Ring() => Thing(ItemSlot.Finger);

        // Laid out with gaps in it, as a real pack is. Each is where its number says.
        private static HeroState Laid(params (int cell, Item item)[] things)
        {
            var hero = new HeroState();
            foreach ((int cell, Item item) in things) hero.Inventory[cell] = item;
            return hero;
        }

        private static string Layout(HeroState hero) => string.Join(",", hero.Inventory.Select((item, cell) => item != null ? $"{cell}:{item.Name}" : "").Where(cell => cell.Length > 0));

        private static void Cells(Report report)
        {
            Item first = Ring(), second = Ring();
            var hero = new HeroState();
            report.Check("A pack has its size from the start, all of it empty", hero.Inventory.Length == HeroState.InventorySize && hero.Inventory.All(item => item == null) && hero.FreeCell() == 0);

            hero.Pickup(first);
            hero.Pickup(second);
            report.Check("What's picked up goes in the first free cell", hero.Inventory[0] == first && hero.Inventory[1] == second);

            hero.Inventory[0] = null;
            Item third = Ring();
            hero.Pickup(third);
            report.Check("And into a gap, with nothing else moving to fill it", hero.Inventory[0] == third && hero.Inventory[1] == second);

            Item fourth = Ring();
            hero.Pickup(fourth, 20);
            report.Check("It can be asked to go in a cell of its own", hero.Inventory[20] == fourth);
            Item fifth = Ring();
            hero.Pickup(fifth, 20);
            report.Check("But never on top of something", hero.Inventory[20] == fourth && hero.Inventory[2] == fifth);

            report.Check("A cell that isn't there is no different", hero.Pickup(Ring(), 999) && hero.Pickup(Ring(), -1) && hero.Carried().Count() == 6);

            for (int cell = 0; cell < HeroState.InventorySize; cell++) hero.Inventory[cell] ??= Ring();
            report.Check("A full pack takes nothing and says so", !hero.HasRoom() && hero.FreeCell() == -1 && !hero.Pickup(Ring()) && hero.Carried().Count() == HeroState.InventorySize);
        }

        private static void SwappingWithWhatsWorn(Report report)
        {
            Item helm = Thing(ItemSlot.Head), hat = Thing(ItemSlot.Head), before = Ring(), after = Ring();
            HeroState hero = Laid((3, before), (7, helm), (11, after));
            hero.Equipped[WornSlot.Head] = hat;

            hero.Equip(7);
            report.Check("What's put on and what comes off change places, in the same cell", hero.Equipped[WornSlot.Head] == helm && hero.Inventory[7] == hat);
            report.Check("And nothing else in the pack so much as stirs", hero.Inventory[3] == before && hero.Inventory[11] == after && hero.Carried().Count() == 3);

            hero.Equip(7);
            report.Check("Doing it again puts it all back", hero.Equipped[WornSlot.Head] == hat && hero.Inventory[7] == helm);

            Item boots = Thing(ItemSlot.Feet);
            hero.Inventory[20] = boots;
            hero.Equip(20);
            report.Check("With nothing to take off, the cell is left empty", hero.Equipped[WornSlot.Feet] == boots && hero.Inventory[20] == null && hero.Inventory[11] == after);

            hero.Unequip(WornSlot.Feet);
            report.Check("Taking something off puts it in the first free cell", hero.Inventory[0] == boots);

            hero.Equip(0);
            hero.Unequip(WornSlot.Feet, 30);
            report.Check("Or in the one asked for", hero.Inventory[30] == boots && hero.Inventory[0] == null);

            report.Check("An empty cell or a cell that isn't there can't be put on", !hero.Equip(0) && !hero.Equip(-1) && !hero.Equip(999));
            report.Check("Nor can something that isn't in the pack", !hero.Equip(Ring()));
            report.Check("Nothing worn comes off into a full pack", Full(hero) && !hero.Unequip(WornSlot.Head) && hero.Equipped[WornSlot.Head] == hat);
        }

        private static bool Full(HeroState hero)
        {
            for (int cell = 0; cell < HeroState.InventorySize; cell++) hero.Inventory[cell] ??= Ring();
            return !hero.HasRoom();
        }

        private static void BothHands(Report report)
        {
            Item sword = Thing(ItemSlot.MainHand), shield = Thing(ItemSlot.OffHand), staff = Thing(ItemSlot.MainHand, WeaponKind.Staff), ring = Ring();
            HeroState hero = Laid((0, ring), (9, staff));
            hero.Equipped[WornSlot.MainHand] = sword;
            hero.Equipped[WornSlot.OffHand] = shield;

            hero.Equip(9);
            report.Check("Taking up two hands, what was in the main hand goes where it came from", hero.Equipped[WornSlot.MainHand] == staff && hero.Inventory[9] == sword);
            report.Check("And what was in the other goes in the first free cell", hero.Inventory[1] == shield && !hero.Equipped.ContainsKey(WornSlot.OffHand) && hero.Inventory[0] == ring);

            hero.Equip(1);
            report.Check("Something in the off hand sends what needs two to its cell", hero.Equipped[WornSlot.OffHand] == shield && hero.Inventory[1] == staff && !hero.Equipped.ContainsKey(WornSlot.MainHand));
            report.Check("Which is all that happens to the pack", hero.Inventory[9] == sword && hero.Inventory[0] == ring);

            HeroState tight = Laid((0, staff));
            tight.Equipped[WornSlot.MainHand] = sword;
            tight.Equipped[WornSlot.OffHand] = shield;
            Full(tight);
            string before = Layout(tight);
            report.Check("Two things can't come off into room for one, and it all stays as it was", !tight.Equip(0) && Layout(tight) == before && tight.Equipped[WornSlot.MainHand] == sword && tight.Equipped[WornSlot.OffHand] == shield);

            HeroState handFree = Laid((0, staff));
            handFree.Equipped[WornSlot.OffHand] = shield;
            Full(handFree);
            report.Check("But one thing coming off fits where it came from, however full the pack", handFree.Equip(0) && handFree.Inventory[0] == shield && handFree.Equipped[WornSlot.MainHand] == staff);

            report.Check("What's held in both hands is what's compared against", staffAgainstBoth(sword, shield, staff));
            static bool staffAgainstBoth(Item sword, Item shield, Item staff)
            {
                var hero = new HeroState();
                hero.Equipped[WornSlot.MainHand] = sword;
                hero.Equipped[WornSlot.OffHand] = shield;
                return hero.WornInPlaceOf(staff).SequenceEqual(new[] { sword, shield });
            }

            var armed = new HeroState();
            armed.Equipped[WornSlot.MainHand] = staff;
            report.Check("And what needs two is what an off hand is compared against", armed.WornInPlaceOf(shield).SequenceEqual(new[] { staff }));

            var ringed = new HeroState();
            Item left = Ring(), right = Ring();
            ringed.Equipped[WornSlot.Finger1] = left;
            ringed.Equipped[WornSlot.Finger2] = right;
            report.Check("A ring is compared against both, and not against itself", ringed.WornInPlaceOf(Ring()).SequenceEqual(new[] { left, right }) && ringed.WornInPlaceOf(left).SequenceEqual(new[] { right }));
        }

        private static void Moving(Report report)
        {
            Item a = Ring(), b = Ring(), helm = Thing(ItemSlot.Head), ring = Ring(), other = Ring();
            HeroState hero = Laid((2, a), (5, b), (8, helm));
            ItemPlace Cell(int cell) => ItemPlace.Cell(cell);
            ItemPlace Worn(WornSlot place) => ItemPlace.Worn(place);

            report.Check("Into an empty cell, it just goes there", hero.Move(Cell(2), Cell(20)) && hero.Inventory[20] == a && hero.Inventory[2] == null);
            report.Check("Onto something, the two change places", hero.Move(Cell(5), Cell(20)) && hero.Inventory[20] == b && hero.Inventory[5] == a);
            report.Check("Nothing to move means nothing moves", !hero.Move(Cell(2), Cell(3)) && !hero.Move(Cell(5), Cell(5)) && hero.Inventory[5] == a);
            report.Check("Nor from or to somewhere that isn't there", !hero.Move(new ItemPlace(-1), Cell(0)) && !hero.Move(Cell(5), new ItemPlace(9999)) && !hero.Move(new ItemPlace(9999), Cell(0)) && hero.Inventory[5] == a);

            report.Check("Onto a place that fits, it's put on", hero.Move(Cell(8), Worn(WornSlot.Head)) && hero.Equipped[WornSlot.Head] == helm && hero.Inventory[8] == null);
            report.Check("Onto a place that doesn't, it stays put", !hero.Move(Cell(5), Worn(WornSlot.Head)) && hero.Inventory[5] == a && hero.Equipped[WornSlot.Head] == helm);

            report.Check("From a place into an empty cell, it comes off there", hero.Move(Worn(WornSlot.Head), Cell(30)) && hero.Inventory[30] == helm && !hero.Equipped.ContainsKey(WornSlot.Head));
            hero.Equip(30);

            Item hat = Thing(ItemSlot.Head);
            hero.Inventory[31] = hat;
            report.Check("From a place onto something that fits there, they change places", hero.Move(Worn(WornSlot.Head), Cell(31)) && hero.Equipped[WornSlot.Head] == hat && hero.Inventory[31] == helm);
            report.Check("Onto something that doesn't, neither moves", !hero.Move(Worn(WornSlot.Head), Cell(5)) && hero.Equipped[WornSlot.Head] == hat && hero.Inventory[5] == a);

            hero.Equipped[WornSlot.Finger1] = ring;
            report.Check("A ring goes to the other hand", hero.Move(Worn(WornSlot.Finger1), Worn(WornSlot.Finger2)) && hero.Equipped[WornSlot.Finger2] == ring && !hero.Equipped.ContainsKey(WornSlot.Finger1));
            hero.Equipped[WornSlot.Finger1] = other;
            report.Check("And with one on each they change hands", hero.Move(Worn(WornSlot.Finger1), Worn(WornSlot.Finger2)) && hero.Equipped[WornSlot.Finger2] == other && hero.Equipped[WornSlot.Finger1] == ring);
            report.Check("But nothing else goes from one place to another", !hero.Move(Worn(WornSlot.Head), Worn(WornSlot.Feet)) && hero.Equipped[WornSlot.Head] == hat);

            string before = Layout(hero);
            report.Check("Asking whether something would work doesn't do it", hero.CanMove(Cell(5), Cell(6)) && hero.CanMove(Worn(WornSlot.Finger1), Worn(WornSlot.Finger2)) && Layout(hero) == before && hero.Equipped[WornSlot.Finger1] == ring);
            report.Check("And says no to what wouldn't", !hero.CanMove(Cell(5), Worn(WornSlot.Head)) && !hero.CanMove(Cell(6), Cell(7)));
        }

        private static void Sorting(Report report)
        {
            Item Sword(Rarity rarity, int level) => new Item { Name = $"Sword {rarity} {level}", Slot = ItemSlot.MainHand, Rarity = rarity, Level = level };
            Item low = Sword(Rarity.Magic, 3), high = Sword(Rarity.Magic, 9), rare = Sword(Rarity.Rare, 1), helm = Thing(ItemSlot.Head), ring = Ring();
            HeroState hero = Laid((30, ring), (2, low), (17, helm), (20, high), (9, rare));

            report.Check("Nothing sorts itself: everything's still where it was laid", hero.Inventory[30] == ring && hero.Inventory[2] == low);

            hero.Sort();
            report.Check("Asked to, it closes up from the first cell", hero.Carried().Count() == 5 && hero.Inventory.Take(5).All(item => item != null) && hero.Inventory.Skip(5).All(item => item == null));
            report.Check("By where it's worn, then the best of it first", hero.Inventory[0] == rare && hero.Inventory[1] == high && hero.Inventory[2] == low && hero.Inventory[3] == helm && hero.Inventory[4] == ring);

            HeroState again = Laid((5, ring), (0, helm), (1, rare), (9, high), (3, low));
            again.Sort();
            report.Check("Two ends sorting what they both have get the same thing, however it started", Layout(again) == Layout(hero));
        }

        // Anything that goes wrong in here is an item that's gone, or one that's been doubled. So everything is tried, over and over, with everything counted.
        private static void NothingGetsLost(Report report)
        {
            var random = new Random(5);
            var hero = new HeroState { Gold = 1000000 };
            var everything = new List<Item>();
            for (int i = 0; i < 28; i++)
            {
                var slot = (ItemSlot)random.Next((int)ItemSlot.Book);
                Item item = Thing(slot, (WeaponKind)random.Next(Enum.GetValues<WeaponKind>().Length));
                everything.Add(item);
                hero.Pickup(item);
            }

            ItemPlace Anywhere() => new ItemPlace(random.Next(-1, HeroState.InventorySize + Enum.GetValues<WornSlot>().Length + 1));
            int lost = 0, doubled = 0, overlapping = 0;
            for (int step = 0; step < 200000; step++)
            {
                switch (random.Next(6))
                {
                    case 0:
                    case 1:
                        hero.Move(Anywhere(), Anywhere());
                        break;
                    case 2:
                        hero.Equip(random.Next(-1, HeroState.InventorySize + 1), random.Next(2) == 0 ? null : (WornSlot)random.Next(10));
                        break;
                    case 3:
                        hero.Unequip((WornSlot)random.Next(10), random.Next(2) == 0 ? null : random.Next(HeroState.InventorySize));
                        break;
                    case 4:
                        if (random.Next(50) == 0) hero.Sort();
                        break;
                    default:
                        hero.CanMove(Anywhere(), Anywhere());
                        break;
                }

                var now = hero.Carried().Concat(hero.Equipped.Values).ToList();
                if (now.Count < everything.Count) lost++;
                if (now.Distinct().Count() != now.Count) doubled++;
                if (hero.Equipped.TryGetValue(WornSlot.MainHand, out Item? held) && held.IsTwoHanded && hero.Equipped.ContainsKey(WornSlot.OffHand)) overlapping++;
            }
            report.Check("Two hundred thousand moves lose nothing", lost == 0, $"{lost} times");
            report.Check("And double nothing", doubled == 0, $"{doubled} times");
            report.Check("And never leave something in both hands beside something else", overlapping == 0, $"{overlapping} times");
            report.Check("Everything worn is where it fits", hero.Equipped.All(pair => WornSlots.Holds(pair.Key) == pair.Value.Slot));
            report.Check("And what's left is what there was", hero.Carried().Concat(hero.Equipped.Values).ToHashSet().SetEquals(everything));
        }

        private static void Selling(Report report)
        {
            var table = new MerchantTable();
            Item ring = Ring(), helm = Thing(ItemSlot.Head), stays = Ring();
            HeroState hero = Laid((4, ring), (9, stays), (12, helm));

            report.Check("Selling pays what it says", table.Sell(hero, ItemPlace.Cell(4)) && hero.Gold == ring.SellValue);
            report.Check("And leaves a gap, with nothing moving into it", hero.Inventory[4] == null && hero.Inventory[9] == stays && hero.Inventory[12] == helm);
            report.Check("And it's on the table to be bought back", table.Sold[0] == ring);

            hero.Equip(12);
            int gold = hero.Gold;
            report.Check("Something being worn can be sold as well", table.Sell(hero, ItemPlace.Worn(WornSlot.Head)) && !hero.Equipped.ContainsKey(WornSlot.Head) && table.Sold[1] == helm && hero.Gold == gold + helm.SellValue);

            gold = hero.Gold;
            report.Check("An empty cell can't be sold, nor a cell that isn't there", !table.Sell(hero, ItemPlace.Cell(4)) && !table.Sell(hero, new ItemPlace(9999)) && hero.Gold == gold);
        }

        private static void BuyingBack(Report report)
        {
            Item ring = Thing(ItemSlot.Finger);
            ring.Level = 30;
            ring.Lines.Add(new ItemLine("gold-find", 5f));
            Item wares = Thing(ItemSlot.Finger), more = Thing(ItemSlot.Finger);
            var table = new MerchantTable { Stock = new Item?[] { wares, more } };
            HeroState hero = Laid((6, ring));

            table.Sell(hero, ItemPlace.Cell(6));
            int paid = hero.Gold;
            report.Check("What's sold is worth something", paid > 0);
            report.Check("Buying it back costs what was paid, not what's asked for wares", MerchantTable.PriceOf(ring, buyingBack: true) == paid && MerchantTable.PriceOf(ring, buyingBack: false) > paid);

            report.Check("And it goes in the cell it's asked to", table.Buy(hero, buyingBack: true, 0, 6) && hero.Inventory[6] == ring);
            report.Check("So a slip of the hand costs nothing", hero.Gold == 0);
            report.Check("And it's gone from the table", table.Sold[0] == null);

            hero.Gold = wares.BuyValue;
            report.Check("Something for sale leaves a gap, with the next thing staying where it is", table.Buy(hero, buyingBack: false, 0) && table.Stock[0] == null && table.Stock[1] == more);
            report.Check("So a second click on the same place buys nothing", !table.Buy(hero, buyingBack: false, 0) && hero.Gold == 0);
            report.Check("Nothing is bought with gold that isn't there", !table.Buy(hero, buyingBack: false, 1) && table.Stock[1] == more);

            hero.Gold = 1000000;
            Full(hero);
            int gold = hero.Gold;
            report.Check("Nor into a full pack, and nothing's paid", !table.Buy(hero, buyingBack: false, 1) && table.Stock[1] == more && hero.Gold == gold);

            report.Check("A place on the table that isn't there is nothing", !table.Buy(hero, false, -1) && !table.Buy(hero, false, 99) && !table.Buy(hero, true, 99));
        }

        private static void AFullTable(Report report)
        {
            var table = new MerchantTable();
            var hero = new HeroState();
            var sold = new List<Item>();
            for (int i = 0; i < MerchantTable.MostSold + 3; i++)
            {
                Item item = Ring();
                sold.Add(item);
                hero.Inventory[0] = item;
                table.Sell(hero, ItemPlace.Cell(0));
            }
            report.Check("A merchant keeps what they've room for, and the oldest is what goes", table.Sold.SequenceEqual(sold.Skip(3)));

            hero.Gold = 1000000;
            table.Buy(hero, buyingBack: true, 5);
            Item next = Ring();
            hero.Inventory[1] = next;
            table.Sell(hero, ItemPlace.Cell(1));
            report.Check("A gap is filled before anything goes", table.Sold[5] == next && table.Sold[0] == sold[3]);
        }

        private static void Searching(Report report)
        {
            var random = new WorldRandom(12);
            Item Found(string baseName, Rarity rarity, params (string affix, float value)[] lines)
            {
                Item item = ItemGenerator.Plain(baseName, 20, random);
                item.Rarity = rarity;
                foreach ((string affix, float value) in lines) item.Lines.Add(new ItemLine(affix, value));
                return item;
            }
            bool Finds(Item item, string typed) => ItemSearch.Finds(item, ItemSearch.Words(typed));

            Item sword = Found("Longsword", Rarity.Rare, ("fire-resistance", 22f), ("added-cold", 7f));
            Item ring = Found("Ring", Rarity.Magic, ("health", 30f));
            Item robe = Found("Silk Vestments", Rarity.Common);
            Item unique = UniqueCatalog.Make(UniqueCatalog.All[0], 20, random);

            report.Check("Nothing typed finds everything", Finds(sword, "") && Finds(sword, "   "));
            report.Check("A name finds it", Finds(sword, "longsword") && Finds(sword, "LONG") && !Finds(ring, "longsword"));
            report.Check("So does what's rolled on it", Finds(sword, "fire res") && Finds(sword, "cold") && Finds(ring, "health") && !Finds(ring, "cold"));
            report.Check("Every word has to be there, in any order", Finds(sword, "resistance fire") && !Finds(sword, "resistance health"));
            report.Check("And a number on it", Finds(sword, "22") && !Finds(sword, "2200"));
            report.Check("What grade and what kind of thing it is", Finds(sword, "rare") && Finds(sword, "sword") && Finds(ring, "magic ring") && !Finds(ring, "rare"));
            report.Check("What it's made of as a family", Finds(robe, "cloth") && !Finds(robe, "heavy"));
            report.Check("What it is before anything's rolled", Finds(robe, "barrier"));
            report.Check("Its level", Finds(sword, "level 20"));
            Item spring = Found("Round Shield", Rarity.Magic);
            spring.Name = "Round Shield of the Spring";
            report.Check("Words are found from their start, so a ring isn't anything of the Spring", Finds(spring, "spr") && !Finds(spring, "ring") && Finds(Found("Ring", Rarity.Magic), "ring") && Finds(sword, "resist"));
            report.Check("A unique is both things it can be called", Finds(unique, "unique") && Finds(unique, "legendary"));
            report.Check("And anything a player can read on a card", Cards.Item(sword, null, Cards.KindOf(sword)).Words().Where(word => word.Length > 0).All(word => ItemSearch.Finds(sword, ItemSearch.Words(word))));
        }

        // A pack from before there were cells is only a list, and it comes in where it was.
        private const string VersionFour = """
            {
              "Version": 4,
              "Hero": {
                "Level": 12, "Gold": 40,
                "Inventory": [
                  { "Name": "First", "Slot": "Finger", "Rarity": "Magic", "Level": 2, "Lines": [ { "Affix": "gold-find", "Value": 5 } ] },
                  { "Name": "Second", "Slot": "Head", "Rarity": "Common", "Level": 3, "Armour": 7, "Lines": [] },
                  { "Name": "Third", "Slot": "Feet", "Rarity": "Common", "Level": 4, "Evasion": 2, "Lines": [] }
                ],
                "Equipped": {
                  "Finger2": { "Name": "On a finger", "Slot": "Finger", "Rarity": "Magic", "Level": 2, "Lines": [] }
                }
              },
              "WorldSeed": 5, "WorldLevel": 3
            }
            """;

        private static void OldPacks(Report report)
        {
            SaveGame? save = SaveGame.Read(VersionFour);
            report.Check("A save from before cells loads", save != null);
            if (save == null) return;

            HeroState hero = save.Hero;
            report.Check("Its pack is the full size", hero.Inventory.Length == HeroState.InventorySize);
            report.Check("With everything in the order it was in, from the first cell", hero.Inventory[0]?.Name == "First" && hero.Inventory[1]?.Name == "Second" && hero.Inventory[2]?.Name == "Third" && hero.Inventory[3] == null);
            report.Check("And what's worn where it was", hero.Equipped[WornSlot.Finger2].Name == "On a finger");
            report.Check("It knows it's up to date", save.Version == SaveGame.CurrentVersion);

            hero.Move(ItemPlace.Cell(0), ItemPlace.Cell(29));
            hero.Move(ItemPlace.Cell(2), ItemPlace.Cell(35));
            string path = Path.Combine(Path.GetTempPath(), $"buildlab-pack-{Environment.ProcessId}.json");
            SaveGame.PathOverride = path;
            save.Store();
            SaveGame? loaded = SaveGame.Load();
            File.Delete(path);
            SaveGame.PathOverride = null;
            report.Check("A pack with gaps in it comes back with the same gaps", loaded != null && Layout(loaded.Hero) == Layout(hero) && loaded.Hero.Inventory[29]?.Name == "First" && loaded.Hero.Inventory[35]?.Name == "Third" && loaded.Hero.Inventory[0] == null);

            SaveGame? noPack = SaveGame.Read("""{ "Version": 4, "Hero": { "Inventory": null } }""");
            report.Check("One with no pack at all has an empty one", noPack != null && noPack.Hero.Inventory.Length == HeroState.InventorySize);
        }

        private static void Wire(Report report)
        {
            Item ring = Ring(), helm = Thing(ItemSlot.Head);
            HeroState hero = Laid((3, ring), (35, helm));

            var writer = new BitWriter();
            Protocol.WriteGear(writer, hero);
            var received = new HeroState();
            Protocol.ReadGear(new BitReader(writer.ToArray()), received);
            report.Check("Gaps come through the wire as gaps", received.Inventory.Length == HeroState.InventorySize && Layout(received) == Layout(hero));

            var forged = new BitWriter();
            forged.WriteCount(HeroState.InventorySize + 1);
            for (int i = 0; i <= HeroState.InventorySize; i++) forged.WriteBool(false);
            forged.WriteCount(0);
            bool refused = false;
            try { Protocol.ReadGear(new BitReader(forged.ToArray()), new HeroState()); }
            catch (NetReadException) { refused = true; }
            report.Check("A pack bigger than a pack is refused at the door", refused);

            var shortPack = new BitWriter();
            shortPack.WriteCount(1);
            shortPack.WriteBool(false);
            shortPack.WriteCount(0);
            shortPack.WriteCount(0);
            var shorter = new HeroState();
            Protocol.ReadGear(new BitReader(shortPack.ToArray()), shorter);
            report.Check("And a smaller one still ends up the size a pack is", shorter.Inventory.Length == HeroState.InventorySize);
        }
    }
}

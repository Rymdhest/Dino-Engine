using Dino_Defenders.Books;
using Dino_Defenders.Net;
using Dino_Defenders.Progression;
using Dino_Defenders.Tooltips;
using Dino_Engine.Networking;
using Dino_Engine.World;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dino_BuildLab
{
    /// <summary>
    /// The beta vendor in the Haven, who gives anything away for testing. What he has out has to be exactly the same on both ends for the same tab, roll and level, or a client is shown one thing and
    /// handed another, and it has to be within the rules, or a save that holds it is changed when it's loaded. His uniques have to be every one there is, and the lookup has to find each of them by
    /// what it does. How it goes across a line is <c>Dino NetLab vendor</c>.
    /// </summary>
    internal static class VendorChecks
    {
        private static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

        private static string Written(Item item) => JsonSerializer.Serialize(item, Json);

        private static int Lookup => Array.FindIndex(BetaVendor.Tabs, tab => tab.Lookup);

        public static void Run(Report report)
        {
            report.Section("The beta vendor");
            Stock(report);
            WithinTheRules(report);
            Taking(report);
            NothingElseMoves(report);

            report.Section("Looking up uniques");
            Lookups(report);
        }

        /// <summary>The lookup with no game: every unique a search finds, with what it gives and what it costs, for going over them all at once.</summary>
        public static int List(Options options)
        {
            LookupType type = Enum.TryParse(options.Text("type") ?? "", true, out LookupType parsed) ? parsed : LookupType.Any;
            LookupOrder order = Enum.TryParse(options.Text("order") ?? "", true, out LookupOrder sorted) ? sorted : LookupOrder.Slot;
            string? kind = options.Text("kind");
            List<LookupEntry> entries = UniqueLookup.Entries(BetaVendor.Stock(Lookup, 1, options.Int("level", 1)));
            LookupWord[] words = UniqueLookup.Words(options.Text("find") ?? "");
            List<LookupEntry> found = UniqueLookup.Sorted(entries.Where(entry => UniqueLookup.Passes(entry, type, kind, words)), order).ToList();
            foreach (LookupEntry entry in found)
            {
                Console.WriteLine($"{entry.Name}  ({entry.Slot}, {entry.Kind}, level {entry.Level}{(entry.HeroOnly ? ", heroes only" : "")})");
                foreach (string line in entry.Gives) Console.WriteLine($"    + {line}");
                foreach (string line in entry.Costs) Console.WriteLine($"    - {line}");
            }
            Console.WriteLine($"\n{found.Count} of {entries.Count}");
            return 0;
        }

        private static void Stock(Report report)
        {
            var tabs = BetaVendor.Tabs;
            bool same = true, apart = true, fresh = true, filled = true, levelled = true;
            for (int t = 0; t < tabs.Length; t++)
            {
                List<Item> first = BetaVendor.Stock(t, 77, 12), again = BetaVendor.Stock(t, 77, 12);
                same &= first.Count == again.Count && first.Select(Written).SequenceEqual(again.Select(Written));
                fresh &= first.Zip(again).All(pair => !ReferenceEquals(pair.First, pair.Second));
                filled &= first.Count > 0;
                if (!tabs[t].Lookup) apart &= !first.Select(Written).SequenceEqual(BetaVendor.Stock(t, 78, 12).Select(Written));
                levelled &= BetaVendor.Stock(t, 77, 400).Select(Written).SequenceEqual(BetaVendor.Stock(t, 77, BetaVendor.MostLevel).Select(Written))
                    && BetaVendor.Stock(t, 77, -5).Select(Written).SequenceEqual(BetaVendor.Stock(t, 77, 1).Select(Written));
            }
            report.Check("Every tab has something on it", filled);
            report.Check("The same tab, roll and level are the same things, to the last number, as each end makes its own", same);
            report.Check("Each time they're made afresh, so nothing handed out is shared with anything", fresh);
            report.Check("Another roll is other things", apart);
            report.Check("He only stocks for a level there can be", levelled);
            report.Check("Nothing that isn't a tab is anything", BetaVendor.Stock(-1, 1, 1).Count == 0 && BetaVendor.Stock(tabs.Length, 1, 1).Count == 0 && BetaVendor.Offer(0, 1, 1, -1) == null && BetaVendor.Offer(0, 1, 1, 100000) == null);

            int Tab(string name) => Array.FindIndex(tabs, tab => tab.Name == name);
            var rolls = Enumerable.Range(0, 6).Select(roll => roll * 7919).ToArray();
            List<Item> All(string name, int level = 20) => rolls.SelectMany(roll => BetaVendor.Stock(Tab(name), roll, level)).ToList();
            List<Item> gear = All("Gear"), books = All("Books"), pages = All("Pages"), stickers = All("Stickers"), orders = All("Orders");
            report.Check("Gear is everything worn, and nothing unique", gear.All(item => item.IsWorn && item.Rarity != Rarity.Unique && item.Unique == null));
            report.Check("Of every rarity but that, and every place on a hero", Enum.GetValues<Rarity>().Where(r => r != Rarity.Unique).All(r => gear.Any(item => item.Rarity == r))
                && Enum.GetValues<ItemSlot>().Where(slot => slot < ItemSlot.Book).All(slot => gear.Any(item => item.Slot == slot)));
            report.Check("Books are books, of every way a book goes", books.All(item => item.IsBook && item.Places > 0) && Enum.GetValues<BookMode>().All(mode => books.Any(item => item.Mode == mode)));
            report.Check("Pages are pages, of every kind", pages.All(item => item.IsPage && item.Page != null) && Enum.GetValues<PageKind>().All(kind => pages.Any(item => item.Page!.Kind == kind)));
            report.Check("Stickers are stickers, and none of them Unique, as those are with the uniques", stickers.All(item => item.IsSticker && item.Sticker != null && item.Rarity != Rarity.Unique && !Stickers.IsUnique(item.Sticker.Good)));
            report.Check("Orders are orders, and none of them unique", orders.All(item => item.IsOrders && item.Rarity != Rarity.Unique && item.Unique == null));
            report.Check("Everything is of the level asked for", gear.Concat(books).Concat(pages).Concat(stickers).Concat(orders).All(item => item.Level == 20));

            List<Item> uniques = BetaVendor.Stock(Lookup, 5, 1);
            List<string> worn = uniques.Where(item => item.IsWorn).Select(item => item.Unique ?? "").ToList();
            report.Check("Every unique that's worn is there, once", worn.Count == UniqueCatalog.All.Count && worn.Distinct().Count() == worn.Count && UniqueCatalog.All.All(u => worn.Contains(u.Id)), $"{worn.Count} of {UniqueCatalog.All.Count}");
            List<PageEffect> stuck = uniques.Where(item => item.IsSticker).Select(item => item.Sticker!.Good).ToList();
            report.Check("And everything only a Unique sticker does, once each, on a Unique sticker", stuck.Count == Stickers.Uniques.Count() && Stickers.Uniques.All(stuck.Contains)
                && uniques.Where(item => item.IsSticker).All(item => item.Rarity == Rarity.Unique), $"{stuck.Count} of {Stickers.Uniques.Count()}");
            List<string> given = uniques.Where(item => item.IsOrders).Select(item => item.Unique ?? "").ToList();
            report.Check("And every unique orders, once each", given.Count == OrdersCatalog.All.Count && OrdersCatalog.All.All(o => given.Contains(o.Id)));
            report.Check("And nothing else", uniques.Count == worn.Count + stuck.Count + given.Count && uniques.All(item => item.Rarity == Rarity.Unique));
            report.Check("None of them below the level it's first found at", uniques.All(item => item.Level >= (UniqueCatalog.Find(item.Unique ?? "")?.MinLevel ?? OrdersCatalog.Find(item.Unique)?.MinLevel ?? 1)));
        }

        // Whatever he gives away comes back from a save or across a line as it was, as the rules for both have nothing to put right in it.
        private static void WithinTheRules(Report report)
        {
            var changed = new List<string>();
            var lost = new List<string>();
            for (int t = 0; t < BetaVendor.Tabs.Length; t++)
            {
                foreach (int roll in new[] { 3, 1009, 65537 })
                {
                    foreach (int level in new[] { 1, 14, 60 })
                    {
                        foreach (Item item in BetaVendor.Stock(t, roll, level))
                        {
                            string before = Written(item);
                            var writer = new BitWriter();
                            Protocol.WriteItem(writer, item);
                            Item sent = Protocol.ReadItem(new BitReader(writer.ToArray()));
                            if (Written(sent) != before) lost.Add(item.Name);

                            Item fixedUp = JsonSerializer.Deserialize<Item>(before, Json)!;
                            ItemGenerator.Fix(fixedUp);
                            if (Written(fixedUp) != before) changed.Add($"{BetaVendor.Tabs[t].Name}: {item.Name}");
                        }
                    }
                }
            }
            report.Check("Everything he has goes across a line as it is", lost.Count == 0, string.Join(", ", lost.Distinct().Take(6)));
            report.Check("And is already within the rules, so a save that holds it loads it as it was", changed.Count == 0, string.Join(", ", changed.Distinct().Take(6)));
        }

        private static void Taking(Report report)
        {
            HeroState hero = HeroState.CreateNew();
            Array.Clear(hero.Inventory);
            Item expected = BetaVendor.Offer(0, 9, 10, 3)!;
            report.Check("Something taken goes in the cell asked for", BetaVendor.Take(hero, 0, 9, 10, 3, 17) && hero.Inventory[17] != null && Written(hero.Inventory[17]!) == Written(expected));
            report.Check("And stays on his table, to be taken again as another of itself", BetaVendor.Take(hero, 0, 9, 10, 3, 17) && hero.Inventory[0] != null && Written(hero.Inventory[0]!) == Written(expected)
                && !ReferenceEquals(hero.Inventory[0], hero.Inventory[17]));
            report.Check("Nothing that isn't there can be taken", !BetaVendor.Take(hero, 0, 9, 10, 100000) && !BetaVendor.Take(hero, BetaVendor.Tabs.Length, 9, 10, 0) && hero.Carried().Count() == 2);

            for (int i = 0; i < HeroState.InventorySize; i++) hero.Inventory[i] ??= BetaVendor.Offer(0, 9, 10, 0);
            report.Check("Nor into a pack that's full, which is left as it was", !BetaVendor.Take(hero, Lookup, 9, 10, 0) && hero.Carried().Count() == HeroState.InventorySize);
            report.Check("He only gives in the Haven, where he stands", BetaVendor.GivesIn(true) == BetaVendor.Standing && !BetaVendor.GivesIn(false));
        }

        // What had to be asked for in particular is drawn just as it would have been anyway, so nothing else rolled from the same place changes.
        private static void NothingElseMoves(Report report)
        {
            bool folios = true, stickers = true;
            for (int i = 0; i < 400; i++)
            {
                Item found = ItemGenerator.Folio(12, new WorldRandom(i), (Rarity)(i % 3));
                Item asked = ItemGenerator.Folio(12, new WorldRandom(i), (Rarity)(i % 3), found.Slot);
                folios &= Written(found) == Written(asked);

                Sticker rolled = Stickers.Roll(Rarity.Unique, new WorldRandom(i));
                Sticker same = Stickers.Roll(Rarity.Unique, new WorldRandom(i), rolled.Good);
                stickers &= rolled.Good == same.Good && rolled.Bad == same.Bad && rolled.GoodBy == same.GoodBy && rolled.BadBy == same.BadBy;
            }
            report.Check("Asking for a book, page or sticker draws just as finding one did", folios);
            report.Check("Asking for what a Unique sticker does draws just as finding one did", stickers);
        }

        private static void Lookups(Report report)
        {
            List<LookupEntry> entries = UniqueLookup.Entries(BetaVendor.Stock(Lookup, 21, 30));
            List<string> Find(string typed, LookupType type = LookupType.Any, string? second = null) => entries.Where(entry => UniqueLookup.Passes(entry, type, second, UniqueLookup.Words(typed))).Select(entry => entry.Name).ToList();
            string Name(string id) => UniqueCatalog.Find(id)!.Name;

            report.Check("Nothing typed finds them all", Find("").Count == entries.Count && Find("   ").Count == entries.Count);
            List<string> missed = entries.Where(entry => !Find(entry.Name).Contains(entry.Name)).Select(entry => entry.Name).ToList();
            report.Check("Each is found by its name", missed.Count == 0, string.Join(", ", missed.Take(6)));
            missed = UniqueCatalog.All.Where(unique => !Find(unique.Id).Contains(unique.Name)).Select(unique => unique.Id).ToList();
            report.Check("And by the id it's saved as", missed.Count == 0, string.Join(", ", missed.Take(6)));
            missed = entries.Where(entry => entry.Gives.Count == 0).Select(entry => entry.Name).ToList();
            report.Check("Each says what it gives", missed.Count == 0, string.Join(", ", missed.Take(6)));
            missed = entries.Where(entry => entry.Costs.Count == 0).Select(entry => entry.Name).ToList();
            report.Check("And what it costs", missed.Count == 0, string.Join(", ", missed.Take(6)));

            List<string> gold = Find("gold");
            string[] golden = { "beggars-staff", "gilded-aegis", "midas-touch", "vow-of-poverty", "moneylenders-ring", "highwaymans-breeches", "cutpurses-fingers" };
            report.Check("\"gold\" finds everything that does something with gold", golden.All(id => gold.Contains(Name(id))), string.Join(", ", golden.Where(id => !gold.Contains(Name(id)))));
            report.Check("And not what has nothing to do with it", !gold.Contains(Name("ashen-promise")) && !gold.Contains(Name("first-cut")) && gold.Count < entries.Count / 4, $"{gold.Count}");
            report.Check("\"carry\" finds what cares what's carried", Find("carry").Contains(Name("hoarders-harness")) && Find("carried").Contains(Name("hoarders-harness")) && Find("inventory").Contains(Name("hoarders-harness")));
            report.Check("A word is found in its other forms", Find("stunned").Contains(Name("aftershock")) && Find("move").Contains(Name("forced-march")) && Find("kills").Contains(Name("mourning-edge")));
            report.Check("But a word isn't anything that merely starts the same", !Find("fire").Contains(Name("first-cut")) && Find("string").All(name => name == Name("string-of-teeth")), string.Join(", ", Find("string")));
            report.Check("Every word has to be there, in any order", Find("gold staff").SequenceEqual(new[] { Name("beggars-staff") }) && Find("staff gold").SequenceEqual(new[] { Name("beggars-staff") }));
            report.Check("Its lore is searched too", Find("burning house").Contains(Name("ashen-promise")));
            report.Check("Words a player is likely to type mean what the game calls them", Find("health").All(Find("life").Contains) && Find("health").All(Find("hp").Contains) && Find("health").Count > 0);

            List<string> costsFire = Find($"{UniqueLookup.CostsMark}fire"), givesFire = Find($"{UniqueLookup.GivesMark}fire");
            report.Check("What it costs can be searched alone", costsFire.Contains(Name("wolfpelt")) && !givesFire.Contains(Name("wolfpelt")) && costsFire.Contains(Name("ashen-promise")));
            report.Check("And what it gives", givesFire.Contains(Name("ashen-promise")) && Find($"{UniqueLookup.GivesMark}chance").Contains(Name("honest-blade")) && !Find($"{UniqueLookup.GivesMark}multiplier").Contains(Name("honest-blade"))
                && Find($"{UniqueLookup.CostsMark}multiplier").Contains(Name("honest-blade")));
            report.Check("A side on its own is for the word after it", Find($"{UniqueLookup.CostsMark} fire").SequenceEqual(costsFire));

            report.Check("Stickers are every Unique sticker and nothing else", Find("", LookupType.Sticker).Count == Stickers.Uniques.Count() && entries.Where(entry => entry.Type == LookupType.Sticker).All(entry => entry.Item.IsSticker));
            List<string> projectile = Find("projectile", LookupType.Sticker, "Projectile Page");
            report.Check("And narrowed and searched among", projectile.Count > 0 && projectile.Count < Stickers.Uniques.Count() && Find("cursor", LookupType.Sticker).Contains("Farcaster Sticker") && !Find("cursor", LookupType.Orders).Any(), string.Join(", ", projectile));
            report.Check("Orders are every unique orders", Find("", LookupType.Orders).Count == OrdersCatalog.All.Count);
            report.Check("Weapons are what's held in the main hand, and jewellery rings and amulets", entries.Where(e => e.Type == LookupType.Weapon).All(e => e.Item.Slot == ItemSlot.MainHand)
                && entries.Where(e => e.Type == LookupType.Jewellery).All(e => e.Item.Slot is ItemSlot.Finger or ItemSlot.Neck));

            bool partitioned = true;
            foreach (LookupType type in Enum.GetValues<LookupType>())
            {
                int all = Find("", type).Count;
                partitioned &= all == UniqueLookup.Seconds(entries, type).Sum(second => Find("", type, second).Count);
            }
            report.Check("What each type has in it is split between the choices after it, with nothing left over", partitioned);
            report.Check("Which go in the order a hero is dressed", UniqueLookup.Seconds(entries, LookupType.Any).First() == "Main Hand" && UniqueLookup.Seconds(entries, LookupType.Weapon).First() == "Sword");
            report.Check("A ring is a ring, and a sword a sword", Find("", LookupType.Jewellery, "Ring").Count == UniqueCatalog.All.Count(u => u.Base == "Ring") && Find("", LookupType.Weapon, "Sword").Count > 0);

            List<LookupEntry> byName = UniqueLookup.Sorted(entries, LookupOrder.Name).ToList(), byLevel = UniqueLookup.Sorted(entries, LookupOrder.Level).ToList();
            report.Check("They can be put in order by name and by level", byName.Zip(byName.Skip(1)).All(p => string.Compare(p.First.Name, p.Second.Name, StringComparison.OrdinalIgnoreCase) <= 0)
                && byLevel.Zip(byLevel.Skip(1)).All(p => p.First.Level <= p.Second.Level) && byName.Count == entries.Count);
        }
    }
}

using Dino_Defenders.Net;
using Dino_Defenders.Progression;
using Dino_Engine.Networking;
using Dino_Engine.World;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dino_BuildLab
{
    /// <summary>
    /// The Forge's crafting bench (<see cref="Crafting"/>). What a fuse makes has to be within the rules, or a save that holds it changes it on loading, and has to cross a line as it is, as both ends make
    /// it. Only Magic things of the same sort fuse, and the hero pays, the first cell holds what's made and the second is left empty, or nothing changes at all.
    /// </summary>
    internal static class CraftingChecks
    {
        private static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

        private static string Written(Item item) => JsonSerializer.Serialize(item, Json);

        private static readonly ItemSlot[] Sorts = Enum.GetValues<ItemSlot>().Where(slot => slot != ItemSlot.Orders).ToArray();

        public static void Run(Report report)
        {
            report.Section("The crafting bench");
            Fusing(report);
            Refusing(report);
            AtTheBench(report);
        }

        private static Item Magic(ItemSlot slot, int level, WorldRandom random) => slot >= ItemSlot.Book
            ? ItemGenerator.Folio(level, random, Rarity.Magic, slot)
            : ItemGenerator.Forge(level, random, Rarity.Magic, 0f, slot);

        // Every sort, at many levels: what's made is the first's base and look with all four lines, Rare, within the rules and the same across a line, and neither of what went in is changed.
        private static void Fusing(Report report)
        {
            var random = new WorldRandom(4242);
            var badShape = new List<string>();
            var changed = new List<string>();
            var lost = new List<string>();
            var touched = new List<string>();
            var fused = new Dictionary<ItemSlot, int>();
            int tried = 0;
            foreach (ItemSlot slot in Sorts)
            {
                fused[slot] = 0;
                for (int i = 0; i < 300; i++)
                {
                    int level = 1 + random.Int(ItemGenerator.MostItemLevel);
                    Item first = Magic(slot, level, random), second = Magic(slot, 1 + random.Int(ItemGenerator.MostItemLevel), random);
                    if (Crafting.WhyNot(first, second) != null) continue;
                    tried++;
                    fused[slot]++;
                    string firstBefore = Written(first), secondBefore = Written(second);
                    Item made = Crafting.Fused(first, second);
                    if (Written(first) != firstBefore || Written(second) != secondBefore) touched.Add(first.Name);

                    bool shaped = made.Rarity == Rarity.Rare && made.Lines.Count == 4 && made.Slot == first.Slot && made.Kind == first.Kind && made.Level == first.Level
                        && made.Base == first.Base && made.LookSeed() == first.LookSeed() && made.Armour == first.Armour && made.WeaponDamage == first.WeaponDamage
                        && first.Lines.Concat(second.Lines).All(line => made.Lines.Any(m => m.Affix == line.Affix && m.Value == line.Value));
                    if (!shaped) badShape.Add($"{first.Name} + {second.Name}");

                    string before = Written(made);
                    Item fixedUp = JsonSerializer.Deserialize<Item>(before, Json)!;
                    ItemGenerator.Fix(fixedUp);
                    if (Written(fixedUp) != before) changed.Add($"{first.Name} + {second.Name} = {made.Name}");

                    var writer = new BitWriter();
                    Protocol.WriteItem(writer, made);
                    if (Written(Protocol.ReadItem(new BitReader(writer.ToArray()))) != before) lost.Add(made.Name);
                }
            }
            report.Check("Every sort can be fused", fused.Values.All(count => count > 0), string.Join(", ", fused.Where(pair => pair.Value == 0).Select(pair => pair.Key)));
            report.Note($"{tried} pairs fused, of {Sorts.Length * 300} of the same sort drawn ({string.Join(", ", fused.Select(pair => $"{pair.Key} {pair.Value}"))})");
            report.Check("What's made is the first's base and look, Rare, with both one's lines", badShape.Count == 0, string.Join(", ", badShape.Take(4)));
            report.Check("And is within the rules, so a save loads it as it was", changed.Count == 0, string.Join(", ", changed.Take(4)));
            report.Check("And goes across a line as it is", lost.Count == 0, string.Join(", ", lost.Take(4)));
            report.Check("Working out what's made changes neither of what went in", touched.Count == 0, string.Join(", ", touched.Take(4)));
        }

        private static (Item first, Item second) Pair(ItemSlot slot, WorldRandom random)
        {
            for (int i = 0; ; i++)
            {
                Item first = Magic(slot, 30, random), second = Magic(slot, 30, random);
                if (Crafting.WhyNot(first, second) == null || i > 500) return (first, second);
            }
        }

        private static void Refusing(Report report)
        {
            var random = new WorldRandom(77);
            (Item helm, Item otherHelm) = Pair(ItemSlot.Head, random);
            Item gloves = Magic(ItemSlot.Hands, 30, random);
            report.Check("Two of the same sort fuse", Crafting.WhyNot(helm, otherHelm) == null);
            report.Check("Two of different sorts don't", Crafting.WhyNot(helm, gloves) != null && Crafting.WhyNot(gloves, helm) != null);
            report.Check("Nor does something with itself", Crafting.WhyNot(helm, helm) != null);

            Item sword = ItemGenerator.Forge(30, random, Rarity.Magic, 0f, ItemSlot.MainHand);
            Item other = sword;
            for (int i = 0; i < 200 && other.Kind == sword.Kind; i++) other = ItemGenerator.Forge(30, random, Rarity.Magic, 0f, ItemSlot.MainHand);
            report.Check("Nor weapons of different kinds", other.Kind != sword.Kind && Crafting.WhyNot(sword, other) != null);

            (Item book, Item otherBook) = Pair(ItemSlot.Book, random);
            (Item page, _) = Pair(ItemSlot.Page, random);
            report.Check("Two Books fuse, and a Book and a Page don't", Crafting.WhyNot(book, otherBook) == null && Crafting.WhyNot(book, page) != null);

            Item rare = ItemGenerator.Forge(30, random, Rarity.Rare, 0f, ItemSlot.Head);
            Item plain = ItemGenerator.Forge(30, random, Rarity.Normal, 0f, ItemSlot.Head);
            report.Check("Only Magic things fuse", Crafting.WhyNot(helm, rare) != null && Crafting.WhyNot(rare, helm) != null && Crafting.WhyNot(helm, plain) != null);

            var twin = JsonSerializer.Deserialize<Item>(Written(otherHelm), Json)!;
            twin.Lines[0] = new ItemLine(helm.Lines[0].Affix, helm.Lines[0].Value - 1f);
            report.Check("Two with the same line fuse", Crafting.WhyNot(helm, twin) == null);
            Item both = Crafting.Fused(helm, twin);
            string written = Written(both);
            ItemGenerator.Fix(both);
            report.Check("And what's made keeps both, as two lines, through a save", both.Lines.Count(line => line.Affix == helm.Lines[0].Affix) == 2 && Written(both) == written);

            (Item folio, Item otherFolio) = Pair(ItemSlot.Page, random);
            var folioTwin = JsonSerializer.Deserialize<Item>(Written(otherFolio), Json)!;
            folioTwin.Lines[0] = new ItemLine(folio.Lines[0].Affix, folio.Lines[0].Value);
            Item bothFolio = Crafting.Fused(folio, folioTwin);
            string folioWritten = Written(bothFolio);
            ItemGenerator.Fix(bothFolio);
            report.Check("And so does a Page", Crafting.WhyNot(folio, folioTwin) == null && bothFolio.Lines.Count(line => line.Affix == folio.Lines[0].Affix) == 2 && Written(bothFolio) == folioWritten);
        }

        private static void AtTheBench(Report report)
        {
            var random = new WorldRandom(9);
            (Item first, Item second) = Pair(ItemSlot.Body, random);
            HeroState hero = HeroState.CreateNew();
            Array.Clear(hero.Inventory);
            hero.Inventory[4] = first;
            hero.Inventory[11] = second;
            int cost = Crafting.FuseCost(first);
            string expected = Written(Crafting.Fused(first, second));

            hero.Gold = cost - 1;
            report.Check("A hero who can't afford it fuses nothing", !Crafting.Fuse(hero, 4, 11) && hero.Gold == cost - 1 && ReferenceEquals(hero.Inventory[4], first) && ReferenceEquals(hero.Inventory[11], second));
            hero.Gold = cost + 5;
            report.Check("Nor anything that isn't two cells of the pack", !Crafting.Fuse(hero, 4, 4) && !Crafting.Fuse(hero, 4, 12) && !Crafting.Fuse(hero, 4, HeroState.InventorySize) && !Crafting.Fuse(hero, -1, 11) && hero.Gold == cost + 5);
            report.Check("One who can pays, and what's made is in the first's cell and the second's is empty", Crafting.Fuse(hero, 4, 11) && hero.Gold == 5 && hero.Inventory[4] is Item made && Written(made) == expected && hero.Inventory[11] == null);
            report.Check("Nothing else in the pack moves", hero.Carried().Count() == 1);
            report.Check("And what's made can't be fused again", Crafting.WhyNotFusing(hero.Inventory[4]!) != null);
        }
    }
}

using Dino_Defenders.Books;
using Dino_Engine.World;

namespace Dino_Defenders.Progression
{
    /// <summary>One of the beta vendor's tabs: what it's called, and what's on it at a level for a roll. One that's a lookup is everything of a kind, to be searched, rather than a handful at random.</summary>
    public sealed record BetaTab(string Name, Func<int, WorldRandom, List<Item>> Stock, bool Lookup = false);

    /// <summary>
    /// The beta vendor in the Haven, who is there for testing the game and for nothing else, and gives away whatever he has: random gear, books, pages, stickers and orders, and every unique there is.
    /// What's out is a pure function of which tab, which roll and what level, so a client shows it with nothing sent, and the server makes the very same thing again when one is asked for. Both ends run
    /// these rules, as they do a merchant's, and the server has the last word. Nothing is ever taken off his table, so the same thing can be had twice, and a roll only changes when someone asks him for
    /// another. A new tab is a line in <see cref="Tabs"/>.
    /// </summary>
    public static class BetaVendor
    {
        /// <summary>Whether he stands in the Haven at all. When the game isn't being tested any more, this is what sends him away.</summary>
        public static readonly bool Standing = true;

        public const int MostLevel = 100;

        private const ulong Seed = 0xBE7A5EEDUL;
        private const int GearCount = 70;
        private const int BookCount = 30;
        private const int PageCount = 40;
        private const int StickerCount = 40;
        private const int OrdersCount = 20;

        // Of what's random: mostly what's worth looking at, with enough plain things among it to see what those are like.
        private const float NormalShare = 0.2f;
        private const float MagicShare = 0.35f;

        public static readonly BetaTab[] Tabs =
        {
            new("Gear", Gear),
            new("Books", (level, random) => Folios(level, random, BookCount, ItemSlot.Book)),
            new("Pages", (level, random) => Folios(level, random, PageCount, ItemSlot.Page)),
            new("Stickers", (level, random) => Folios(level, random, StickerCount, ItemSlot.Sticker)),
            new("Orders", Orders),
            new("Uniques", Uniques, Lookup: true),
        };

        /// <summary>Whether he'll give anything away in a world, which is only ever the Haven, where he stands.</summary>
        public static bool GivesIn(bool haven) => Standing && haven;

        /// <summary>A level he can stock for.</summary>
        public static int Clamp(int level) => Math.Clamp(level, 1, MostLevel);

        /// <summary>Everything on a tab, in the order it's shown. Each call makes it all afresh, so nothing handed out is shared with anything else.</summary>
        public static List<Item> Stock(int tab, int roll, int level)
        {
            if (tab < 0 || tab >= Tabs.Length) return new List<Item>();
            return Tabs[tab].Stock(Clamp(level), new WorldRandom(Seed).Fork(Tabs[tab].Name).Fork(roll));
        }

        /// <summary>One thing on a tab, or nothing if there's no such thing.</summary>
        public static Item? Offer(int tab, int roll, int level, int index)
        {
            List<Item> stock = Stock(tab, roll, level);
            return index >= 0 && index < stock.Count ? stock[index] : null;
        }

        /// <summary>Gives something on his table to a hero, into the cell asked for if it's empty, or else the first that is. It stays on the table.</summary>
        public static bool Take(HeroState hero, int tab, int roll, int level, int index, int? cell = null)
        {
            if (!hero.HasRoom() || Offer(tab, roll, level, index) is not Item item) return false;
            return hero.Pickup(item, cell);
        }

        private static Rarity RarityOf(WorldRandom random)
        {
            float roll = random.Float();
            return roll < NormalShare ? Rarity.Normal : roll < NormalShare + MagicShare ? Rarity.Magic : Rarity.Rare;
        }

        // Anything worn, in the order a hero is dressed: weapons by kind, then from the off hand to the fingers, each from plain to rare.
        private static List<Item> Gear(int level, WorldRandom random)
        {
            var stock = new List<Item>(GearCount);
            for (int i = 0; i < GearCount; i++)
            {
                WorldRandom one = random.Fork(i);
                stock.Add(ItemGenerator.Forge(level, one, RarityOf(one.Fork("rarity")), 0f));
            }
            return stock.OrderBy(item => item.Slot).ThenBy(item => item.Slot == ItemSlot.MainHand ? (int)item.Kind : 0).ThenBy(item => item.Rarity).ThenBy(item => item.Name, StringComparer.Ordinal).ToList();
        }

        private static List<Item> Folios(int level, WorldRandom random, int count, ItemSlot kind)
        {
            var stock = new List<Item>(count);
            for (int i = 0; i < count; i++)
            {
                WorldRandom one = random.Fork(i);
                stock.Add(ItemGenerator.Folio(level, one, RarityOf(one.Fork("rarity")), kind));
            }
            return stock.OrderBy(item => item.Rarity).ThenBy(item => item.Name, StringComparer.Ordinal).ThenBy(item => item.Places).ToList();
        }

        private static List<Item> Orders(int level, WorldRandom random)
        {
            var stock = new List<Item>(OrdersCount);
            for (int i = 0; i < OrdersCount; i++)
            {
                WorldRandom one = random.Fork(i);
                stock.Add(OrdersMaker.Roll(level, one, RarityOf(one.Fork("rarity"))));
            }
            return stock.OrderBy(item => item.Rarity).ThenBy(item => item.Base, StringComparer.Ordinal).ThenBy(item => item.Name, StringComparer.Ordinal).ToList();
        }

        // Every one of a kind there is, in the order each catalogue keeps them: what's worn, then each thing only a Unique sticker can do, then unique orders. Nothing is ever below the level it can first be found at.
        private static List<Item> Uniques(int level, WorldRandom random)
        {
            var stock = new List<Item>();
            foreach (UniqueDefinition unique in UniqueCatalog.All) stock.Add(UniqueCatalog.Make(unique, Math.Max(level, unique.MinLevel), random.Fork(unique.Id)));
            foreach (PageEffect effect in Stickers.Uniques)
            {
                WorldRandom one = random.Fork(effect.ToString());
                stock.Add(ItemGenerator.StickerItem(Stickers.Roll(Rarity.Unique, one, effect), ItemGenerator.NewSeed(one), level, Rarity.Unique));
            }
            foreach (UniqueOrders orders in OrdersCatalog.All) stock.Add(OrdersMaker.Make(orders, level, random.Fork(orders.Id)));
            return stock;
        }
    }
}

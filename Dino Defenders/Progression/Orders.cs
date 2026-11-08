using Dino_Defenders.Tooltips;
using Dino_Engine.World;

namespace Dino_Defenders.Progression
{
    /// <summary>A kind of orders: what the Reich wrote them for, which is the one reward they always bring, at what their level makes of it, as a base item has what it always has.</summary>
    public sealed record OrdersBase(string Name, OmenDefinition Brings, string Lore);

    /// <summary>
    /// Orders that are one of a kind, and do what nothing else can. Like every unique, each is a trade: what it gives and what it costs are said apart. What they do is read where it matters, by
    /// <see cref="LandTerms.Has(UniqueOrders)"/>. The id is saved, so it stays.
    /// </summary>
    public sealed class UniqueOrders
    {
        public string Id = "";
        public string Name = "";
        public string Base = "";
        public int MinLevel = 1;
        public string Lore = "";
        public string Gives = "";
        public string Costs = "";

        /// <summary>What it pays on top of the land's omens, as shares.</summary>
        public float Quantity;
        public float Rarity;

        /// <summary>How many levels freeing the land opens, counting its own.</summary>
        public int Climb = 1;
    }

    /// <summary>
    /// Every unique orders there is. What each does is checked for by name where it happens: <see cref="Server.EncounterSystem"/> for who stands where, <see cref="Server.ServerSession"/> for what
    /// the land asks and what it pays, <see cref="Server.ArenaSystem"/> for who comes through an arena's gates, and <see cref="Dino_Engine.World.Generation.PackStage"/> for a land with nothing on
    /// its roads.
    /// </summary>
    public static class OrdersCatalog
    {
        /// <summary>How many more stand at each place of a land held as the last garrison, and come through each gate of an arena, as a share.</summary>
        public const float GarrisonPeople = 2f;

        /// <summary>How much more a rare enemy drops on a land run by the ledger.</summary>
        public const float LedgerLoot = 2f;

        /// <summary>How much more health and damage the tyrant of a land given the wager has, in percent.</summary>
        public const float WagerTyrant = 60f;

        private const int WagerClimb = 2;
        private const float WagerQuantity = 0.3f;
        private const float LedgerRarity = 0.2f;
        private const float BredRarity = 0.6f;
        private const float TwinQuantity = 0.15f;
        private const float WritQuantity = 0.5f;

        private static readonly List<UniqueOrders> Everything = new();

        public static IReadOnlyList<UniqueOrders> All => Everything;

        private static UniqueOrders Add(UniqueOrders orders)
        {
            Everything.Add(orders);
            return orders;
        }

        public static readonly UniqueOrders Wager = Add(new UniqueOrders
        {
            Id = "kommandants-wager", Name = "The Kommandant's Wager", Base = "Hunting Orders", MinLevel = 3,
            Lore = "He bet his tyrant against any Defender born. He hasn't lost yet.",
            Gives = $"Freeing this land lets the map table open {Say.N(WagerClimb)} Levels deeper at once, though never past a tyrant's arena that hasn't been fought",
            Costs = $"The tyrant has {Say.Percent(WagerTyrant)} more Health and deals {Say.Percent(WagerTyrant)} more Damage",
            Quantity = WagerQuantity, Climb = WagerClimb,
        });

        public static readonly UniqueOrders Ledger = Add(new UniqueOrders
        {
            Id = "brutmeisters-ledger", Name = "The Brutmeister's Ledger", Base = "Breeding Orders", MinLevel = 6,
            Lore = "Every animal in the brood, by number, and what it cost.",
            Gives = $"Every pack has a Rare leader if it can, and Rare Enemies drop {Say.Share(LedgerLoot - 1f)} more",
            Costs = "Rare Enemies have one more trait than they would",
            Rarity = LedgerRarity,
        });

        public static readonly UniqueOrders Bred = Add(new UniqueOrders
        {
            Id = "bred-for-war", Name = "Bred for War", Base = "Breeding Orders", MinLevel = 10,
            Lore = "The Amber Institute's own seal. Nothing it signed for was ordinary.",
            Gives = $"{Say.Share(BredRarity)} more Item Rarity",
            Costs = "No Enemy here is Normal: every one is at least Magic",
            Rarity = BredRarity,
        });

        public static readonly UniqueOrders TwinPens = Add(new UniqueOrders
        {
            Id = "twin-pens", Name = "Orders for the Twin Pens", Base = "Hunting Orders", MinLevel = 8,
            Lore = "Two tyrants, two keepers, and one land that can't feed them both.",
            Gives = "A second tyrant holds one of the land's farthest places, or waits in the arena beside the first, and drops all that a tyrant drops",
            Costs = "Both tyrants have to fall before the land is free",
            Quantity = TwinQuantity,
        });

        public static readonly UniqueOrders Garrison = Add(new UniqueOrders
        {
            Id = "last-garrison", Name = "The Last Garrison", Base = "Muster Orders", MinLevel = 12,
            Lore = "Pull everyone back to the walls. Leave the roads to the crows.",
            Gives = "Nothing waits on the roads or between places, and every chest at a place holds what a keep's chest holds",
            Costs = $"Every place holds {Say.Share(GarrisonPeople)} more of those who hold it, and an arena's gates let as many more through",
        });

        public static readonly UniqueOrders Writ = Add(new UniqueOrders
        {
            Id = "ossuary-writ", Name = "The Ossuary's Writ", Base = "Patrol Orders", MinLevel = 15,
            Lore = "Signed in a cold green hand. Nobody here stays down.",
            Gives = $"{Say.Share(WritQuantity)} more Items",
            Costs = "Every Enemy that falls rises once more as bones, a moment later",
            Quantity = WritQuantity,
        });

        public static UniqueOrders? Find(string? id) => id == null ? null : Everything.FirstOrDefault(orders => orders.Id == id);

        /// <summary>One that can be found at a level, or none.</summary>
        public static UniqueOrders? Pick(int level, WorldRandom random)
        {
            var options = Everything.Where(orders => orders.MinLevel <= level).ToList();
            return options.Count > 0 ? random.Pick(options) : null;
        }
    }

    /// <summary>
    /// Sealed orders taken from the Reich, which say where a brood is bound and what it brings. Given to the map table in the Haven before the way is opened, they're used up, and the land it opens on
    /// is what they say. They're items like any other, of the same four rarities, and their lines are affixes like any other's: a prefix is a reward and a suffix a danger. Normal orders only bring
    /// what their kind is for, Magic ones a prefix and a suffix, Rare ones two of each, and a Unique does something nothing else can. Each line is an omen's id and how much of it. Read LANDS.md.
    /// </summary>
    public static class OrdersMaker
    {
        private const int MagicPerSide = 1;
        private const int RarePerSide = 2;

        /// <summary>Every kind of orders. The names are saved, so a new one goes on the end.</summary>
        public static readonly OrdersBase[] Bases =
        {
            new("Muster Orders", Omens.Teeming, "Raise every pack to strength."),
            new("Patrol Orders", Omens.Overrun, "Put more bands on the roads."),
            new("Supply Orders", Omens.Riches, "Fill the stores before the winter."),
            new("Breeding Orders", Omens.Bloodlines, "Send the pens their best."),
            new("Hunting Orders", Omens.Trophies, "Let the tyrant feed."),
            new("Survey Orders", Omens.Expanse, "Push the pens out past the last fence."),
        };

        public static OrdersBase? BaseOf(Item orders) => Bases.FirstOrDefault(b => b.Name == orders.Base);

        /// <summary>How many of each side's affixes something of this rarity has. Nothing normal has any, and a unique is what it is.</summary>
        public static int PerSide(Rarity rarity) => rarity switch
        {
            Rarity.Magic => MagicPerSide,
            Rarity.Rare => RarePerSide,
            _ => 0,
        };

        /// <summary>Everything orders bring: what their kind always does, then each line. A unique is only what it does.</summary>
        public static IEnumerable<(OmenDefinition Omen, float Value)> OmensOf(Item orders)
        {
            if (orders.Rarity != Rarity.Unique && BaseOf(orders) is OrdersBase kind) yield return (kind.Brings, kind.Brings.Fixed(orders.Level));
            foreach (ItemLine line in orders.Lines)
            {
                if (Omens.Find(line.Affix) is OmenDefinition omen) yield return (omen, line.Value);
            }
        }

        /// <summary>Orders of exactly this rarity, at this level, for what a loot table has decided.</summary>
        /// <param name="quality">From 0 to 1, how well they come out, as <see cref="ItemGenerator.Forge"/> means it.</param>
        public static Item Roll(int level, WorldRandom random, Rarity rarity, float quality = 0f)
        {
            level = Math.Max(level, 1);
            if (rarity == Rarity.Unique && OrdersCatalog.Pick(level, random.Fork("unique")) is UniqueOrders unique) return Make(unique, level, random);
            if (rarity == Rarity.Unique) rarity = Rarity.Rare;

            OrdersBase kind = random.Fork("kind").Pick(Bases);
            var orders = new Item
            {
                Name = kind.Name,
                Base = kind.Name,
                Slot = ItemSlot.Orders,
                Rarity = rarity,
                Level = level,
                Seed = ItemGenerator.NewSeed(random),
            };
            Deal(orders, random.Fork("lines"), quality);
            orders.Name = NameOf(orders);
            return orders;
        }

        /// <summary>Unique orders, as found.</summary>
        public static Item Make(UniqueOrders unique, int level, WorldRandom random) => new Item
        {
            Name = unique.Name,
            Base = unique.Base,
            Slot = ItemSlot.Orders,
            Rarity = Rarity.Unique,
            Level = Math.Max(level, unique.MinLevel),
            Seed = ItemGenerator.NewSeed(random),
            Unique = unique.Id,
        };

        // Tops each side up to what its rarity has, prefixes first, so that a card always reads the same way round. Nothing is what the kind already brings.
        private static void Deal(Item orders, WorldRandom random, float quality)
        {
            int each = PerSide(orders.Rarity);
            OmenDefinition? brings = BaseOf(orders)?.Brings;
            var dealt = new List<ItemLine>(each * 2);
            foreach (OmenSide side in new[] { OmenSide.Prefix, OmenSide.Suffix })
            {
                List<ItemLine> lines = orders.Lines.Where(line => Omens.Find(line.Affix)?.Side == side).ToList();
                var options = Omens.All.Where(omen => omen.Side == side && omen != brings && omen.FirstLevel <= orders.Level && lines.All(line => line.Affix != omen.Id)).ToList();
                while (lines.Count < each && options.Count > 0)
                {
                    OmenDefinition omen = PickWeighted(options, random);
                    options.Remove(omen);
                    lines.Add(new ItemLine(omen.Id, MathF.Round(omen.Roll(orders.Level, random.Fork(omen.Id), quality))));
                }
                dealt.AddRange(lines);
            }
            orders.Lines = dealt;
        }

        private static OmenDefinition PickWeighted(List<OmenDefinition> options, WorldRandom random)
        {
            float total = 0f;
            foreach (OmenDefinition omen in options) total += omen.Weight;
            float roll = random.Float() * total;
            foreach (OmenDefinition omen in options)
            {
                roll -= omen.Weight;
                if (roll < 0f) return omen;
            }
            return options[^1];
        }

        /// <summary>What they're called: what their first prefix reads as before what they are, and their first suffix after, as anything else of their rarity is named.</summary>
        public static string NameOf(Item orders)
        {
            if (orders.Rarity == Rarity.Unique && OrdersCatalog.Find(orders.Unique) is UniqueOrders unique) return unique.Name;
            string before = "";
            string after = "";
            foreach (ItemLine line in orders.Lines)
            {
                if (Omens.Find(line.Affix) is not OmenDefinition omen) continue;
                if (omen.IsPrefix && before.Length == 0) before = omen.Affix + " ";
                if (!omen.IsPrefix && after.Length == 0) after = " " + omen.Affix;
            }
            return before + orders.Base + after;
        }

        /// <summary>
        /// Brings orders from a save or a stranger up to the rules, as <see cref="ItemGenerator.Fix"/> does for anything worn: a line stays if it's an omen there is and its side has room, and what's missing
        /// is drawn from the orders' own seed, so that a Rare is still a Rare and the same one each time. Anything already within the rules isn't touched.
        /// </summary>
        public static void Fix(Item orders)
        {
            orders.Page = null;
            orders.Sticker = null;
            orders.Held = null;
            if (BaseOf(orders) == null) orders.Base = Bases[(int)(orders.LookSeed() % (ulong)Bases.Length)].Name;
            if (orders.Rarity == Rarity.Unique)
            {
                if (OrdersCatalog.Find(orders.Unique) is UniqueOrders unique)
                {
                    orders.Lines.Clear();
                    orders.Base = unique.Base;
                    orders.Name = unique.Name;
                    return;
                }
                orders.Unique = null;
                orders.Rarity = Rarity.Rare;
            }
            orders.Unique = null;
            if (!Enum.IsDefined(orders.Rarity)) orders.Rarity = Rarity.Rare;

            int each = PerSide(orders.Rarity);
            OmenDefinition? brings = BaseOf(orders)?.Brings;
            var prefixes = new List<ItemLine>(each);
            var suffixes = new List<ItemLine>(each);
            foreach (ItemLine line in orders.Lines)
            {
                if (Omens.Find(line.Affix) is not OmenDefinition omen || omen == brings || !float.IsFinite(line.Value) || line.Value <= 0f) continue;
                List<ItemLine> side = omen.IsPrefix ? prefixes : suffixes;
                if (side.Count >= each || prefixes.Concat(suffixes).Any(other => other.Affix == line.Affix)) continue;
                side.Add(new ItemLine(line.Affix, MathF.Min(line.Value, omen.Tiers[^1].High)));
            }

            // Prefixes before suffixes, as they're dealt, so that the same orders always read the same way round.
            List<ItemLine> kept = prefixes.Concat(suffixes).ToList();
            bool same = kept.Count == orders.Lines.Count && kept.Zip(orders.Lines).All(pair => pair.First.Affix == pair.Second.Affix && pair.First.Value == pair.Second.Value);
            if (!same || prefixes.Count != each || suffixes.Count != each)
            {
                orders.Lines = kept;
                Deal(orders, new WorldRandom(orders.LookSeed()).Fork("lines"), 0f);
            }

            // A name is only ever what the lines make of it, so whatever one came with is replaced.
            orders.Name = NameOf(orders);
        }
    }
}

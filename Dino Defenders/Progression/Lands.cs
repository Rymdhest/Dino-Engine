using Dino_Defenders.Stats;
using Dino_Defenders.Tooltips;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using System.Text.Json.Serialization;
using static Dino_Defenders.Stats.M;

namespace Dino_Defenders.Progression
{
    /// <summary>
    /// What a land holds beyond what it is: the omens the orders given for it put on it, the unique orders among them, and how many of its places have to be freed. Lands hold no omens of their own:
    /// everything about a land that isn't its seed and level is what orders made it, which is all that's saved or sent. Some omens decide how many stand on the land and how large it is, so
    /// <see cref="Shape"/> has to be what makes the request on both ends, or a friend grows a different land. Read LANDS.md.
    /// </summary>
    public sealed class LandTerms
    {
        public const int LeastPlaces = 3;
        public const int MostPlaces = 5;

        private const float MostPeople = 1.5f;
        private const float MostPacks = 1f;
        private const float MostLarger = 1.5f;
        private const float UniqueShare = 0.5f;        // Of what makes finds rarer, also on how likely a unique is.
        private const float QualityPerTier = 0.02f;    // How well items roll, for each tier above the first a prefix has reached.
        private const float MostQuality = 0.3f;
        private const float ExperienceShare = 0.5f;    // Of what more finds come to, as more experience.

        /// <summary>Each omen as its id and how much of it there is, in the order the orders brought them. The same omen twice is the two added up.</summary>
        public List<ItemLine> Omens { get; set; } = new();

        /// <summary>The unique orders given for this land, by id, each once.</summary>
        public List<string> Orders { get; set; } = new();

        /// <summary>How many of its places have to be freed, on top of its tyrant. An arena asks for nothing but its tyrant, whatever this says.</summary>
        public int Places { get; set; } = LeastPlaces;

        /// <summary>Whether there's anything to it at all.</summary>
        [JsonIgnore] public bool Any => Omens.Count > 0 || Orders.Count > 0;

        /// <summary>The unique orders given for this land, in the order they were given.</summary>
        public IEnumerable<UniqueOrders> Uniques()
        {
            foreach (string id in Orders)
            {
                if (OrdersCatalog.Find(id) is UniqueOrders unique) yield return unique;
            }
        }

        public bool Has(UniqueOrders unique) => Orders.Contains(unique.Id);

        /// <summary>How far freeing this land climbs.</summary>
        [JsonIgnore] public int Climb => Uniques().Select(unique => unique.Climb).DefaultIfEmpty(1).Max();

        /// <summary>How much of an omen the land holds, or nothing.</summary>
        public float ValueOf(OmenDefinition omen)
        {
            float value = 0f;
            foreach (ItemLine line in Omens)
            {
                if (line.Affix == omen.Id) value += line.Value;
            }
            return MathF.Min(value, omen.Most);
        }

        public bool Has(OmenDefinition omen) => ValueOf(omen) > 0f;

        /// <summary>How much of an omen there is as a chance of one, for those whose number is a share of everyone.</summary>
        public float Chance(OmenDefinition omen) => Math.Clamp(ValueOf(omen) / 100f, 0f, 1f);

        /// <summary>Every omen the land holds, with how much of it, always in the same order, whatever order they came in: the prefixes, then the suffixes.</summary>
        public IEnumerable<(OmenDefinition Omen, float Value)> Chosen()
        {
            foreach (OmenDefinition omen in Progression.Omens.All)
            {
                float value = ValueOf(omen);
                if (value > 0f) yield return (omen, value);
            }
        }

        /// <summary>How many omens there are, once those held twice are counted once.</summary>
        [JsonIgnore] public int Count => Chosen().Count();

        [JsonIgnore] public float MorePeople => Has(OrdersCatalog.Garrison) ? OrdersCatalog.GarrisonPeople + MathF.Min(Sum(omen => omen.People), MostPeople) : MathF.Min(Sum(omen => omen.People), MostPeople);

        [JsonIgnore] public float MorePacks => Has(OrdersCatalog.Garrison) ? WorldRequest.NoPacks : MathF.Min(Sum(omen => omen.Packs), MostPacks);

        /// <summary>How much larger than usual the land is, as a share: the world never grows past the most its kind can be, whatever this says.</summary>
        [JsonIgnore] public float Larger => MathF.Min(Sum(omen => omen.Size) + Progression.Omens.PackRoom * MathF.Max(MorePacks, 0f), MostLarger);

        private float Sum(Func<OmenDefinition, float> per)
        {
            float total = 0f;
            foreach ((OmenDefinition omen, float value) in Chosen()) total += per(omen) * value;
            return total;
        }

        /// <summary>How much further enemies notice a hero from.</summary>
        [JsonIgnore] public float Notice => 1f + ValueOf(Progression.Omens.Night) / 100f;

        /// <summary>
        /// The world this makes of a request. Who's on the land and how large it is are part of the world, so that both ends of a connection grow the same one. A request can ask for a larger land
        /// itself, as a scripted run or a lab does, and the larger of that and what orders ask for is what it is.
        /// </summary>
        public WorldRequest Shape(in WorldRequest request) => new WorldRequest(request.Seed, request.ArchetypeId, request.Level, MorePeople, MorePacks, MathF.Max(request.Larger, Larger));

        /// <summary>What everything hostile on this land wears on top of what it is.</summary>
        public List<Mod> FoeMods()
        {
            var mods = new List<Mod>();
            foreach ((OmenDefinition omen, float value) in Chosen()) mods.AddRange(omen.Foes(value));
            return mods;
        }

        /// <summary>What every hero on this land wears. It's part of their step, so both ends put it on in this order.</summary>
        public List<Mod> HeroMods()
        {
            var mods = new List<Mod>();
            foreach ((OmenDefinition omen, float value) in Chosen()) mods.AddRange(omen.Heroes(value));
            return mods;
        }

        /// <summary>What only the tyrant wears, on top of what everyone does.</summary>
        public List<Mod> TyrantMods()
        {
            var mods = new List<Mod>();
            float wrath = ValueOf(Progression.Omens.Wrath);
            if (wrath > 0f)
            {
                mods.Add(More(Stat.Health, wrath));
                mods.Add(More(Stat.Damage, wrath));
            }
            if (Has(OrdersCatalog.Wager))
            {
                mods.Add(More(Stat.Health, OrdersCatalog.WagerTyrant));
                mods.Add(More(Stat.Damage, OrdersCatalog.WagerTyrant));
            }
            return mods;
        }

        private float Factor(Stat stat)
        {
            float factor = 1f;
            foreach (Mod mod in FoeMods())
            {
                if (mod.Stat == stat && mod.Kind == ModKind.More && mod.Scope == Tags.None) factor *= 1f + mod.Value / 100f;
            }
            return factor;
        }

        /// <summary>What an enemy's health is multiplied by here. Taken out again wherever what something is worth is read from its health, or omens would turn fodder into champions.</summary>
        [JsonIgnore] public float EnemyHealth => Factor(Stat.Health);

        /// <summary>What an enemy's damage is multiplied by here.</summary>
        [JsonIgnore] public float EnemyDamage => Factor(Stat.Damage);

        /// <summary>What this pays on top of what the land gives anyway. Only prefixes and unique orders pay: a suffix is the price, not the reward.</summary>
        public LootBoon Boon()
        {
            float quantity = Sum(omen => omen.Quantity);
            float rarity = Sum(omen => omen.Rarity);
            float gold = Sum(omen => omen.Gold);
            float experience = Sum(omen => omen.Experience);
            foreach (UniqueOrders unique in Uniques())
            {
                quantity += unique.Quantity;
                rarity += unique.Rarity;
            }
            if (quantity <= 0f && rarity <= 0f && gold <= 0f && experience <= 0f) return LootBoon.None;

            int tiers = 0;
            foreach ((OmenDefinition omen, float value) in Chosen())
            {
                if (omen.IsPrefix) tiers += omen.TierOf(value);
            }
            return new LootBoon(
                Quantity: 1f + quantity,
                Rarity: 1f + rarity,
                Uniques: 1f + rarity * UniqueShare,
                Quality: MathF.Min(tiers * QualityPerTier, MostQuality),
                ExtraLevels: 0,
                Least: Rarity.Normal,
                Gold: 1f + quantity + gold,
                Experience: 1f + quantity * ExperienceShare + experience);
        }

        /// <summary>What it all comes to, as a few words: how many more finds, how much rarer, and what else. Whole shares, as these are weighed against each other at a glance.</summary>
        public string Pays()
        {
            LootBoon boon = Boon();
            var parts = new List<string>();
            if (boon.Quantity > 1f) parts.Add($"{Say.Share(Whole(boon.Quantity - 1f))} more Items");
            if (boon.Rarity > 1f) parts.Add($"{Say.Share(Whole(boon.Rarity - 1f))} more Item Rarity");
            if (boon.Gold > boon.Quantity) parts.Add($"{Say.Share(Whole(boon.Gold - boon.Quantity))} more Gold");
            if (MorePeople > 0f) parts.Add($"{Say.Share(Whole(MorePeople))} larger packs");
            if (MorePacks > 0f) parts.Add($"{Say.Share(Whole(MorePacks))} more packs");
            if (Larger > 0f) parts.Add($"a land up to {Say.Share(Whole(Larger))} larger");
            return string.Join(", ", parts);
        }

        private static float Whole(float share) => MathF.Max(MathF.Round(share * 100f), 1f) / 100f;

        /// <summary>Puts orders on the land: what their kind always brings, what's rolled on them, and what a unique one does.</summary>
        public void Give(Item orders)
        {
            foreach ((OmenDefinition omen, float value) in OrdersMaker.OmensOf(orders)) Omens.Add(new ItemLine(omen.Id, value));
            if (orders.Rarity == Rarity.Unique && OrdersCatalog.Find(orders.Unique) != null && !Orders.Contains(orders.Unique!)) Orders.Add(orders.Unique!);
        }

        public LandTerms Copy() => new LandTerms
        {
            Omens = Omens.Select(line => new ItemLine(line.Affix, line.Value)).ToList(),
            Orders = Orders.ToList(),
            Places = Places,
        };

        /// <summary>Whatever a save or a stranger has done to this, it comes out as something that can be.</summary>
        public void Sanitize()
        {
            Omens = (Omens ?? new List<ItemLine>()).Where(line => line != null && Progression.Omens.Find(line.Affix) != null && float.IsFinite(line.Value) && line.Value > 0f)
                .Select(line => new ItemLine(line.Affix, MathF.Min(line.Value, Progression.Omens.Find(line.Affix)!.Most)))
                .Take(Lands.MostOmenLines).ToList();
            Orders = (Orders ?? new List<string>()).Where(id => OrdersCatalog.Find(id) != null).Distinct().Take(OrdersCatalog.All.Count).ToList();
            Places = Math.Clamp(Places, LeastPlaces, MostPlaces);
        }
    }

    /// <summary>What the tyrant of a land is and who it belongs to, which can be read from its seed alone, so a land can be told of before it's grown.</summary>
    public readonly record struct Herald(string Beast, string BossKind, string Commander, string Faction);

    /// <summary>One of the lands the waygate in the Haven reaches, as the map table shows it.</summary>
    public sealed record LandOffer(WorldRequest Request, LandTerms Terms, WorldPlan Plan, Herald Herald)
    {
        public bool Arena => Plan.Archetype.Arena;
    }

    /// <summary>
    /// The loop the game is: the Haven, where nothing can reach a hero, and the lands the Saurian Reich holds, which a hero goes to through the waygate there. A land is a world grown for one visit,
    /// at a level of the hero's choosing: any they've freed, or the one after the deepest. Every tenth level is a boss's arena. Freeing a land opens the next level, and falling in one, or leaving
    /// it, costs nothing but the try. Read LANDS.md.
    /// </summary>
    public static class Lands
    {
        /// <summary>How many lands the map table shows for a level.</summary>
        public const int Choices = 3;

        /// <summary>The most omen lines a land can hold, however many orders were given for it.</summary>
        public const int MostOmenLines = 240;

        /// <summary>Every this many levels, a land is a boss's arena.</summary>
        public const int BossEvery = 10;

        /// <summary>The most orders a land can be given, which is as many as a pack holds, however deep a hero has gone.</summary>
        public const int MostOrderSlots = HeroState.InventorySize;

        /// <summary>The most a hero's freed lands count for, in talent points, as the most a hero can be is level a hundred.</summary>
        public const int MostCountedLand = 100;

        // What it costs to show three other lands for a level: gold that grows with the level, a little faster than what a land pays does.
        private const float RerollGold = 25f;
        private const float RerollGrowth = 25f;
        private const int MostRerollCost = 1_000_000_000;

        private const int LevelsBeforeVariety = 2;     // The first lands ask for the fewest places, as that's where someone learns what a land is.
        private const int MostSeedTries = 4096;

        /// <summary>The Haven: the one place the Reich has never found. It's grown like any world, from a seed of its own and a kind of world nothing else is, so it's the same place every time.</summary>
        public const int HavenSeed = 1610;
        public const string HavenArchetype = "haven";

        public static WorldRequest Haven => new WorldRequest(HavenSeed, HavenArchetype, 1);

        public static bool IsHaven(in WorldRequest request) => request.ArchetypeId == HavenArchetype;

        public static bool IsArena(in WorldRequest request) => ArenaCatalog.Is(request.ArchetypeId);

        /// <summary>Whether a land of this level is a boss's arena.</summary>
        public static bool IsBossLevel(int level) => level > 0 && level % BossEvery == 0;

        /// <summary>The deepest level a hero can open: the one after the deepest they've freed.</summary>
        public static int DeepestOpen(HeroState hero) => Math.Max(hero.BestLand, 0) + 1;

        /// <summary>How many orders a hero can give for one land: one, and one more for each boss they've brought down, which is every tenth level they've freed, as none can be passed by.</summary>
        public static int OrderSlots(HeroState hero) => Math.Min(1 + Math.Max(hero.BestLand, 0) / BossEvery, MostOrderSlots);

        /// <summary>What it costs to show three other lands for a level.</summary>
        public static int RerollCost(int level) => (int)Math.Min(Math.Round(RerollGold * (double)Math.Max(level, 1) * (1.0 + Math.Max(level, 1) / RerollGrowth)), MostRerollCost);

        /// <summary>
        /// How deep freeing a land of this level, that climbs this far, has a hero been: never past a boss's level that hasn't been fought, so that nothing a boss opens can be had without it.
        /// </summary>
        public static int FreedTo(int level, int climb)
        {
            int to = level + Math.Max(climb, 1) - 1;
            if (!IsBossLevel(level)) to = Math.Min(to, (level / BossEvery + 1) * BossEvery - 1);
            return to;
        }

        /// <summary>A land's terms, before any orders: how many places it asks to be freed. An arena asks only for its tyrant.</summary>
        public static LandTerms Roll(int level, WorldRandom random) => new LandTerms
        {
            Places = level <= LevelsBeforeVariety ? LandTerms.LeastPlaces : random.Fork("places").Int(LandTerms.LeastPlaces, LandTerms.MostPlaces),
        };

        /// <summary>
        /// The lands the waygate reaches for a hero at a level: each a different kind of world, with its own story, biomes and weather, or at a boss's level three arenas, each a different kind of
        /// arena with a different tyrant. They come from what's saved on the hero and nothing else, so both ends of a connection see the same three, and they stay the same however often the table is looked at and
        /// however many tries they take, until the level is freed again or gold is paid for others.
        /// </summary>
        public static IReadOnlyList<LandOffer> Offer(HeroState hero, int level)
        {
            level = Math.Max(level, 1);
            var random = new WorldRandom(hero.Offers).Fork("lands").Fork(level).Fork(hero.RerollsAt(level));
            var offers = new List<LandOffer>(Choices);
            if (IsBossLevel(level))
            {
                // Each is a different kind of arena holding a different kind of tyrant, so that the choice is where to fight and what.
                string[] kinds = Chronicle.TyrantKinds;
                var kindsOfArena = new List<WorldArchetype>(ArenaCatalog.All);
                for (int i = 0; i < Choices; i++)
                {
                    WorldRandom one = random.Fork(i);
                    WorldArchetype arena = kindsOfArena[one.Fork("arena").Int(kindsOfArena.Count)];
                    if (kindsOfArena.Count > 1) kindsOfArena.Remove(arena);
                    int seed = SeedFor(one.Fork("seed"), kinds[i % kinds.Length]);
                    var request = new WorldRequest(seed, arena.Id, level);
                    offers.Add(new LandOffer(request, Roll(level, one.Fork("terms")), WorldPlan.Create(request), Chronicle.HeraldOf(seed)));
                }
                return offers;
            }

            var kindsOfWorld = new List<WorldArchetype>(ArchetypeCatalog.All);
            for (int i = 0; i < Choices && kindsOfWorld.Count > 0; i++)
            {
                WorldRandom one = random.Fork(i);
                WorldArchetype kind = kindsOfWorld[one.Fork("kind").Int(kindsOfWorld.Count)];
                kindsOfWorld.Remove(kind);

                var request = new WorldRequest((int)(one.Fork("seed").NextULong() & int.MaxValue), kind.Id, level);
                offers.Add(new LandOffer(request, Roll(level, one.Fork("terms")), WorldPlan.Create(request), Chronicle.HeraldOf(request.Seed)));
            }
            return offers;
        }

        // How large a land reads on the map table, against the most its kind can be, and how wet and cold its biomes are between them, which is what its weather is drawn from.
        private const float SmallLand = 0.55f;
        private const float MiddlingLand = 0.7f;
        private const float Cold = 0.3f;
        private const float Wet = 0.62f;
        private const float Dry = 0.32f;

        /// <summary>How large a land is, in a few words, as the map table tells of it.</summary>
        public static string SizeOf(WorldPlan plan)
        {
            float share = plan.Size / MathF.Max(plan.Archetype.Size.Max, 1f);
            return share < SmallLand ? "A small land" : share < MiddlingLand ? "A middling land" : "A wide land";
        }

        /// <summary>What a land's weather is like, in a few words, from its biomes: it's worked out from them as the land is walked, so this is what it tends to.</summary>
        public static string WeatherOf(WorldPlan plan)
        {
            float moisture = 0f, temperature = 0f, total = 0f;
            for (int i = 0; i < plan.Biomes.Count; i++)
            {
                float share = i < plan.BiomeShares.Count ? plan.BiomeShares[i] : 1f;
                moisture += plan.Biomes[i].Moisture * share;
                temperature += plan.Biomes[i].Temperature * share;
                total += share;
            }
            if (total <= 0f) return "Fair weather";
            moisture /= total;
            temperature /= total;
            if (temperature <= Cold) return moisture >= Dry ? "Snow falls often" : "Cold and clear";
            if (moisture >= Wet) return "Rain comes often";
            if (moisture <= Dry) return "Dry, with little rain";
            return "Showers come and go";
        }

        // A seed whose tyrant is of this kind, the first drawn: every kind turns up often enough that this never looks far.
        private static int SeedFor(WorldRandom random, string kind)
        {
            int seed = 0;
            for (int attempt = 0; attempt < MostSeedTries; attempt++)
            {
                seed = (int)(random.Fork(attempt).NextULong() & int.MaxValue);
                if (Chronicle.BossKindOf(seed) == kind) return seed;
            }
            return seed;
        }
    }
}

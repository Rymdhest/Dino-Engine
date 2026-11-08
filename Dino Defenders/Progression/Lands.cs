using Dino_Defenders.Stats;
using Dino_Defenders.Tooltips;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using System.Text.Json.Serialization;
using static Dino_Defenders.Stats.M;

namespace Dino_Defenders.Progression
{
    /// <summary>
    /// What a land holds beyond what it is: its omens, the orders that were given to the waygate for it, and how many of its places have to be freed. Everything about a land that isn't its seed
    /// and level comes from these few things, which is all that's saved or sent. Some omens decide how many stand on the land, so <see cref="Shape"/> has to be what makes the request on both ends,
    /// or a friend grows a different land. Read LANDS.md.
    /// </summary>
    public sealed class LandTerms
    {
        public const int LeastPlaces = 4;
        public const int MostPlaces = 6;

        private const float MostPeople = 1.5f;
        private const float MostPacks = 1f;
        private const float UniqueShare = 0.5f;        // Of what makes finds rarer, also on how likely a unique is.
        private const float QualityPerTier = 0.02f;    // How well items roll, for each tier above the first an omen has reached.
        private const float MostQuality = 0.3f;
        private const float ExperienceShare = 0.5f;    // Of what more finds come to, as more experience.

        /// <summary>Each omen as its id and how much of it there is: the land's own, then what its orders brought. The same omen twice is the two added up.</summary>
        public List<ItemLine> Omens { get; set; } = new();

        /// <summary>The unique orders given for this land, if any, by id.</summary>
        public string? Orders { get; set; }

        /// <summary>How many of its places have to be freed, on top of its tyrant.</summary>
        public int Places { get; set; } = LeastPlaces;

        /// <summary>Whether there's anything to it at all.</summary>
        [JsonIgnore] public bool Any => Omens.Count > 0 || Orders != null;

        /// <summary>The unique orders given for this land, if any.</summary>
        public UniqueOrders? Unique() => OrdersCatalog.Find(Orders);

        /// <summary>How far freeing this land climbs, and failing it falls.</summary>
        [JsonIgnore] public int Climb => Unique()?.Climb ?? 1;

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

        /// <summary>Every omen the land holds, with how much of it, always in the same order, whatever order they came in.</summary>
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

        [JsonIgnore] public float MorePeople => Unique() == OrdersCatalog.Garrison ? OrdersCatalog.GarrisonPeople : MathF.Min(Sum(omen => omen.People), MostPeople);

        [JsonIgnore] public float MorePacks => Unique() == OrdersCatalog.Garrison ? WorldRequest.NoPacks : MathF.Min(Sum(omen => omen.Packs), MostPacks);

        private float Sum(Func<OmenDefinition, float> per)
        {
            float total = 0f;
            foreach ((OmenDefinition omen, float value) in Chosen()) total += per(omen) * value;
            return total;
        }

        /// <summary>How much further enemies notice a hero from.</summary>
        [JsonIgnore] public float Notice => 1f + ValueOf(Progression.Omens.Night) / 100f;

        /// <summary>The world this makes of a request. Who's on the land is part of the world, so that both ends of a connection grow the same one.</summary>
        public WorldRequest Shape(in WorldRequest request) => new WorldRequest(request.Seed, request.ArchetypeId, request.Level, MorePeople, MorePacks);

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
            float wrath = ValueOf(Progression.Omens.Wrath);
            return wrath > 0f ? new List<Mod> { More(Stat.Health, wrath), More(Stat.Damage, wrath) } : new List<Mod>();
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

        /// <summary>What this pays on top of what the land gives anyway.</summary>
        public LootBoon Boon()
        {
            float quantity = Sum(omen => omen.Quantity);
            float rarity = Sum(omen => omen.Rarity);
            if (Unique() is UniqueOrders unique)
            {
                quantity += unique.Quantity;
                rarity += unique.Rarity;
            }
            if (quantity <= 0f && rarity <= 0f) return LootBoon.None;

            int tiers = 0;
            foreach ((OmenDefinition omen, float value) in Chosen()) tiers += omen.TierOf(value);
            return new LootBoon(
                Quantity: 1f + quantity,
                Rarity: 1f + rarity,
                Uniques: 1f + rarity * UniqueShare,
                Quality: MathF.Min(tiers * QualityPerTier, MostQuality),
                ExtraLevels: 0,
                Least: Rarity.Normal,
                Gold: 1f + quantity,
                Experience: 1f + quantity * ExperienceShare);
        }

        /// <summary>What it all comes to, as a few words: how many more finds, and how much rarer.</summary>
        public string Pays()
        {
            LootBoon boon = Boon();
            var parts = new List<string>();
            if (boon.Quantity > 1f) parts.Add($"{Say.Share(boon.Quantity - 1f)} more Items");
            if (boon.Rarity > 1f) parts.Add($"{Say.Share(boon.Rarity - 1f)} more Item Rarity");
            if (MorePeople > 0f) parts.Add($"{Say.Share(MorePeople)} larger packs");
            if (MorePacks > 0f) parts.Add($"{Say.Share(MorePacks)} more packs");
            return string.Join(", ", parts);
        }

        /// <summary>Puts orders on the land: what their kind always brings, what's rolled on them, and what a unique one does.</summary>
        public void Give(Item orders)
        {
            foreach ((OmenDefinition omen, float value) in OrdersMaker.OmensOf(orders)) Omens.Add(new ItemLine(omen.Id, value));
            if (orders.Rarity == Rarity.Unique && OrdersCatalog.Find(orders.Unique) != null) Orders = orders.Unique;
        }

        public LandTerms Copy() => new LandTerms
        {
            Omens = Omens.Select(line => new ItemLine(line.Affix, line.Value)).ToList(),
            Orders = Orders,
            Places = Places,
        };

        /// <summary>Whatever a save or a stranger has done to this, it comes out as something that can be.</summary>
        public void Sanitize()
        {
            Omens = Omens.Where(line => Progression.Omens.Find(line.Affix) != null && float.IsFinite(line.Value) && line.Value > 0f)
                .Select(line => new ItemLine(line.Affix, MathF.Min(line.Value, Progression.Omens.Find(line.Affix)!.Most)))
                .Take(Lands.MostOmenLines).ToList();
            if (OrdersCatalog.Find(Orders) == null) Orders = null;
            Places = Math.Clamp(Places, LeastPlaces, MostPlaces);
        }
    }

    /// <summary>What the tyrant of a land is and who it belongs to, which can be read from its seed alone, so a land can be told of before it's grown.</summary>
    public readonly record struct Herald(string Beast, string BossKind, string Commander, string Faction);

    /// <summary>One of the lands the waygate in the Haven reaches, as the map table shows it.</summary>
    public sealed record LandOffer(WorldRequest Request, LandTerms Terms, WorldPlan Plan, Herald Herald);

    /// <summary>
    /// The loop the game is: the Haven, where nothing can reach a hero, and the lands the Saurian Reich holds, which a hero goes to through the waygate there and frees or loses. A land is a world grown
    /// for one visit. It has a level, which is the hero's climb, and omens, which grow with it. Freeing it climbs a level, and falling in it or leaving it unfree drops one, never below the first, so how
    /// hard the next land is follows how the last went. Read LANDS.md.
    /// </summary>
    public static class Lands
    {
        /// <summary>How many lands the map table shows at once.</summary>
        public const int Choices = 3;

        /// <summary>The most omen lines a land can hold: its own, and everything good orders can bring.</summary>
        public const int MostOmenLines = 16;

        private const int LeastOmens = 1;
        private const int MostOmens = 6;
        private const int LevelsPerOmen = 6;
        private const int LevelsBeforeVariety = 2;     // The first lands ask for the fewest places, as that's where someone learns what a land is.

        /// <summary>The Haven: the one place the Reich has never found. It's grown like any world, from a seed of its own and a kind of world nothing else is, so it's the same place every time.</summary>
        public const int HavenSeed = 1610;
        public const string HavenArchetype = "haven";

        public static WorldRequest Haven => new WorldRequest(HavenSeed, HavenArchetype, 1);

        public static bool IsHaven(in WorldRequest request) => request.ArchetypeId == HavenArchetype;

        /// <summary>The most omens a land of this level holds of its own. Each is also stronger the deeper the land.</summary>
        public static int MostOmensAt(int level) => Math.Clamp(1 + (Math.Max(level, 1) + LevelsBeforeVariety) / LevelsPerOmen, LeastOmens, MostOmens);

        /// <summary>A land's own omens and places, rolled for its level.</summary>
        public static LandTerms Roll(int level, WorldRandom random)
        {
            var terms = new LandTerms
            {
                Places = level <= LevelsBeforeVariety ? LandTerms.LeastPlaces : random.Fork("places").Int(LandTerms.LeastPlaces, LandTerms.MostPlaces),
            };

            int most = MostOmensAt(level);
            int count = random.Fork("count").Int(Math.Max(LeastOmens, most - 1), most);
            var pool = Omens.All.Where(omen => omen.FirstLevel <= level).ToList();
            var weights = new List<float>(pool.Count);
            WorldRandom draw = random.Fork("omens");
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                weights.Clear();
                foreach (OmenDefinition omen in pool) weights.Add(omen.Weight);
                int pick = draw.PickWeighted(weights);
                OmenDefinition chosen = pool[pick];
                pool.RemoveAt(pick);
                terms.Omens.Add(new ItemLine(chosen.Id, MathF.Round(chosen.Roll(level, draw.Fork(chosen.Id)))));
            }
            return terms;
        }

        /// <summary>
        /// The lands the waygate reaches for a hero: each a different kind of world, with its own story, biomes and omens, at the level of their climb. They come from what's saved on the hero and nothing
        /// else, so both ends of a connection see the same three, and they're the same three however often the table is looked at, until a land has been gone to.
        /// </summary>
        public static IReadOnlyList<LandOffer> Offer(HeroState hero)
        {
            var random = new WorldRandom(hero.Offers).Fork("lands");
            var kinds = new List<WorldArchetype>(ArchetypeCatalog.All);
            var offers = new List<LandOffer>(Choices);
            int level = Math.Max(hero.LandLevel, 1);
            for (int i = 0; i < Choices && kinds.Count > 0; i++)
            {
                WorldRandom one = random.Fork(i);
                WorldArchetype kind = kinds[one.Fork("kind").Int(kinds.Count)];
                kinds.Remove(kind);

                var request = new WorldRequest((int)(one.Fork("seed").NextULong() & int.MaxValue), kind.Id, level);
                offers.Add(new LandOffer(request, Roll(level, one.Fork("terms")), WorldPlan.Create(request), Chronicle.HeraldOf(request.Seed)));
            }
            return offers;
        }
    }
}

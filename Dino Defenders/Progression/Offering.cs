using Dino_Defenders.Stats;
using Dino_Defenders.Tooltips;
using Dino_Engine.World;
using System.Text.Json.Serialization;
using static Dino_Defenders.Stats.M;

namespace Dino_Defenders.Progression
{
    /// <summary>
    /// One thing that can be called down on a land. Those with <see cref="Mods"/> are worn by everything hostile there, exactly as a trait is, so anything one can do an enemy could do anyway.
    /// The id is saved, so it stays. Its name and numbers can change freely.
    /// </summary>
    public sealed class OmenDefinition
    {
        public int Number;
        public string Id = "";
        public string Name = "";
        public string? Text;            // What it does, where its modifiers can't say so for themselves.
        public Mod[] Mods = Array.Empty<Mod>();
        public float Toll;              // What it adds to the price, as a share.
        public float Quantity;          // What it adds to how much drops, as a share.
        public float Rarity;            // What it adds to how good that is, as a share.

        public string Describe() => Text ?? "Enemies: " + string.Join(", ", Mods.Select(mod => mod.Describe()));
    }

    /// <summary>Every omen there is. A new one goes on the end, as they're sent by number.</summary>
    public static class Omens
    {
        private static readonly List<OmenDefinition> Everything = new();

        public static IReadOnlyList<OmenDefinition> All => Everything;

        private static OmenDefinition Add(OmenDefinition omen)
        {
            omen.Number = Everything.Count;
            Everything.Add(omen);
            return omen;
        }

        // What makes every fight harder, paid for in how much comes of it.
        public static readonly OmenDefinition Ironhide = Add(new OmenDefinition { Id = "ironhide", Name = "Ironhide", Toll = 0.4f, Quantity = 0.15f, Rarity = 0.1f, Mods = new[] { More(Stat.Health, 35f), Inc(Stat.Armour, 50f) } });
        public static readonly OmenDefinition Bloodthirst = Add(new OmenDefinition { Id = "bloodthirst", Name = "Bloodthirst", Toll = 0.4f, Quantity = 0.1f, Rarity = 0.15f, Mods = new[] { More(Stat.Damage, 25f) } });
        public static readonly OmenDefinition Quickened = Add(new OmenDefinition { Id = "quickened", Name = "Quickened", Toll = 0.35f, Quantity = 0.1f, Rarity = 0.1f, Mods = new[] { Inc(Stat.MoveSpeed, 25f), Inc(Stat.AttackSpeed, 25f), Inc(Stat.CastSpeed, 25f) } });
        public static readonly OmenDefinition Ashfall = Add(new OmenDefinition { Id = "ashfall", Name = "Ashfall", Toll = 0.3f, Quantity = 0.1f, Rarity = 0.08f, Mods = new[] { GainAs(DamageType.Physical, DamageType.Fire, 50f), Chance(Tags.Burn, 30f) } });
        public static readonly OmenDefinition Hoarfrost = Add(new OmenDefinition { Id = "hoarfrost", Name = "Hoarfrost", Toll = 0.3f, Quantity = 0.1f, Rarity = 0.08f, Mods = new[] { GainAs(DamageType.Physical, DamageType.Cold, 50f), Inc(Stat.AilmentEffect, 50f, Tags.Chill) } });
        public static readonly OmenDefinition Stormfront = Add(new OmenDefinition { Id = "stormfront", Name = "Stormfront", Toll = 0.3f, Quantity = 0.1f, Rarity = 0.08f, Mods = new[] { GainAs(DamageType.Physical, DamageType.Lightning, 50f), Chance(Tags.Shock, 30f) } });
        public static readonly OmenDefinition Leeching = Add(new OmenDefinition { Id = "leeching", Name = "Leeching", Toll = 0.25f, Quantity = 0.08f, Rarity = 0.06f, Mods = new[] { Flat(Stat.HealthLeech, 15f) } });
        public static readonly OmenDefinition Briars = Add(new OmenDefinition { Id = "briars", Name = "Briars", Toll = 0.3f, Quantity = 0.1f, Rarity = 0.1f, Mods = new[] { Flat(Stat.Reflect, 15f) } });
        public static readonly OmenDefinition Keen = Add(new OmenDefinition { Id = "keen", Name = "Keen Edges", Toll = 0.35f, Quantity = 0.1f, Rarity = 0.12f, Mods = new[] { Flat(Stat.Penetration, 20f), Flat(Stat.CritChance, 10f) } });

        // What changes how a fight is fought, which is worth more than what only makes it longer.
        public static readonly OmenDefinition Volatile = Add(new OmenDefinition
        {
            Id = "volatile", Name = "Volatile", Toll = 0.5f, Quantity = 0.18f, Rarity = 0.18f, Mods = new[] { Rules(Rule.BurstsOnDeath) },
        });

        public static readonly OmenDefinition Rallying = Add(new OmenDefinition
        {
            Id = "rallying", Name = "Rallying", Toll = 0.5f, Quantity = 0.18f, Rarity = 0.18f, Mods = new[] { Rules(Rule.RalliesOnDeath) },
        });

        // What changes the land itself.
        public static readonly OmenDefinition Night = Add(new OmenDefinition
        {
            Id = "night", Name = "Endless Night", Toll = 0.45f, Quantity = 0.12f, Rarity = 0.15f,
            Text = $"The whole land counts as Darkness. Enemies notice you from {Say.Share(Rulebook.NightNotice - 1f)} further away.",
        });

        public static readonly OmenDefinition Warhost = Add(new OmenDefinition
        {
            Id = "warhost", Name = "Warhost", Toll = 0.7f,
            Text = $"Every pack has a leader if it can. Leaders are {Say.N(Rulebook.WarhostLeaderElites)} times as likely to be Elite, up to {Say.Share(Rulebook.MostWarhostLeaderElites)}. Site guards are Elite {Say.Share(Rulebook.WarhostEliteChance)} of the time instead of {Say.Share(Rulebook.EliteChance)}.",
        });

        public static readonly OmenDefinition Gilded = Add(new OmenDefinition
        {
            Id = "gilded", Name = "Gilded", Toll = 0.6f,
            Text = "Every chest holds the loot of the next tier up.",
        });

        public static OmenDefinition? Find(string id) => Everything.FirstOrDefault(omen => omen.Id == id);
    }

    /// <summary>
    /// What's been paid at a waygate to make the land beyond it worse, and richer. Everything about it comes from these few choices, which is all that's saved or sent. Peril is the climb: it
    /// makes every enemy stronger, and pays in what they drop. <see cref="Teeming"/> and <see cref="Overrun"/> put more of them on the land instead, no stronger than they were and paying no
    /// more each, so all they cost is gold and all they give is more to kill. Omens are the rest: dangers that pay in what drops, and things that change what the land is.
    /// </summary>
    public sealed class Offering
    {
        public const int MostPeril = 30;
        public const int MostCrowd = 3;
        public const int MostOmens = 5;
        public const int AllowedFalls = 3;
        public const int ReachBeyondProven = 2;

        // A step of peril is about one and a half worlds' worth of strength, and the lands a hero can hold grow slower than this does. That's what makes it a climb.
        private const float HealthStep = 1.15f;
        private const float DamageStep = 1.08f;

        private const float TollPerLevel = 40f;     // Gold, at a toll of one. A world pays out about 1300 gold a level to someone who walks half of it.
        private const float LeastToll = 2f;
        private const float PerilToll = 1.3f;

        // What a step of peril pays. It has to keep ahead of what it does to how long a fight takes, or nobody who can climb would want to: `Dino BuildLab offering` holds the sums.
        private const float QuantityStep = 0.15f;
        private const float RarityStep = 0.18f;
        private const float LegendStep = 0.15f;
        private const float QualityStep = 0.035f;
        private const float GoldStep = 0.12f;
        private const float ExperienceStep = 0.1f;
        private const int PerilPerLevel = 2;        // Steps of peril for each level items come out above.
        private const int NothingPlainFrom = 3;
        private const int PerilPerOmen = 3;

        private static readonly float[] PeopleShares = { 0f, 0.5f, 1f, 1.5f };
        private static readonly float[] PackShares = { 0f, 0.3f, 0.6f, 1f };
        private static readonly float[] CrowdTolls = { 0f, 2f, 5f, 10f };

        public int Peril { get; set; }
        public int Teeming { get; set; }
        public int Overrun { get; set; }

        /// <summary>By id, as they're saved.</summary>
        public List<string> Omens { get; set; } = new();

        /// <summary>Whether anything has been offered at all.</summary>
        [JsonIgnore] public bool Any => Peril > 0 || Teeming > 0 || Overrun > 0 || Omens.Count > 0;

        /// <summary>Whether it can be failed, as an offering of more enemies and nothing else can't be: there's nothing there for it to take back.</summary>
        [JsonIgnore] public bool AtStake => Peril > 0 || Omens.Count > 0;

        [JsonIgnore] public float MorePeople => PeopleShares[Teeming];
        [JsonIgnore] public float MorePacks => PackShares[Overrun];

        /// <summary>How much further enemies notice a hero from.</summary>
        [JsonIgnore] public float Notice => Has(Progression.Omens.Night) ? Rulebook.NightNotice : 1f;

        /// <summary>How many omens this much peril can carry.</summary>
        public static int OmenSlots(int peril) => Math.Min(1 + peril / PerilPerOmen, MostOmens);

        /// <summary>The most peril a hero can call down: a little past what they've come through.</summary>
        public static int Reach(HeroState hero) => Math.Min(hero.PerilProven + ReachBeyondProven, MostPeril);

        public bool Has(OmenDefinition omen) => Omens.Contains(omen.Id);

        public Offering Copy() => new Offering { Peril = Peril, Teeming = Teeming, Overrun = Overrun, Omens = new List<string>(Omens) };

        /// <summary>The world this makes of a request. More enemies are part of the world, so that both ends of a connection grow the same one.</summary>
        public WorldRequest Shape(in WorldRequest request) => new WorldRequest(request.Seed, request.ArchetypeId, request.Level, MorePeople, MorePacks);

        /// <summary>Whatever a save or a hand has done to this, it comes out as something that can be.</summary>
        public void Sanitize()
        {
            Peril = Math.Clamp(Peril, 0, MostPeril);
            Teeming = Math.Clamp(Teeming, 0, MostCrowd);
            Overrun = Math.Clamp(Overrun, 0, MostCrowd);
            Omens = Omens.Where(id => Progression.Omens.Find(id) != null).Distinct().Take(OmenSlots(Peril)).ToList();
        }

        /// <summary>What it costs for a land of this level, in gold.</summary>
        public long Price(int level)
        {
            if (!Any) return 0;

            float toll = LeastToll * MathF.Pow(PerilToll, Peril);
            float share = 1f + CrowdTolls[Teeming] + CrowdTolls[Overrun];
            foreach (OmenDefinition omen in Chosen()) share += omen.Toll;
            return (long)MathF.Ceiling(TollPerLevel * level * toll * share);
        }

        /// <summary>Those chosen, always in the same order, whatever order they were picked in.</summary>
        public IEnumerable<OmenDefinition> Chosen() => Progression.Omens.All.Where(Has);

        /// <summary>What everything hostile on this land wears on top of what it is.</summary>
        public List<Mod> EnemyMods()
        {
            var mods = new List<Mod>();
            if (Peril > 0)
            {
                mods.Add(More(Stat.Health, (MathF.Pow(HealthStep, Peril) - 1f) * 100f));
                mods.Add(More(Stat.Damage, (MathF.Pow(DamageStep, Peril) - 1f) * 100f));
            }
            foreach (OmenDefinition omen in Chosen()) mods.AddRange(omen.Mods);
            return mods;
        }

        private float Factor(Stat stat)
        {
            float factor = 1f;
            foreach (Mod mod in EnemyMods())
            {
                if (mod.Stat == stat && mod.Kind == ModKind.More && mod.Scope == Tags.None) factor *= 1f + mod.Value / 100f;
            }
            return factor;
        }

        /// <summary>What an enemy's health is multiplied by here. Taken out again wherever what something is worth is read from its health, or peril would turn fodder into champions.</summary>
        [JsonIgnore] public float EnemyHealth => Factor(Stat.Health);

        /// <summary>What an enemy's damage is multiplied by here.</summary>
        [JsonIgnore] public float EnemyDamage => Factor(Stat.Damage);

        /// <summary>What this pays on top of what the land gives anyway.</summary>
        public LootBoon Boon()
        {
            if (!AtStake) return LootBoon.None;

            float quantity = 0f;
            float rarity = 0f;
            foreach (OmenDefinition omen in Chosen())
            {
                quantity += omen.Quantity;
                rarity += omen.Rarity;
            }

            return new LootBoon(
                Quantity: (1f + QuantityStep * Peril) * (1f + quantity),
                Rarity: (1f + RarityStep * Peril) * (1f + rarity),
                Legend: 1f + LegendStep * Peril,
                Quality: QualityStep * Peril,
                ExtraLevels: Peril / PerilPerLevel,
                Least: Peril >= NothingPlainFrom ? LootGrade.Magic : LootGrade.Plain,
                Gold: 1f + GoldStep * Peril,
                Experience: 1f + ExperienceStep * Peril);
        }

        /// <summary>What it's called, as short as can be.</summary>
        public string Title()
        {
            var parts = new List<string>();
            if (Peril > 0) parts.Add($"Peril {Peril}");
            if (Teeming > 0) parts.Add($"Teeming {Teeming}");
            if (Overrun > 0) parts.Add($"Overrun {Overrun}");
            return parts.Count > 0 ? string.Join(", ", parts) : "An offering";
        }

        /// <summary>The omens by name, for whatever has room to say them.</summary>
        public string OmenNames() => string.Join(", ", Chosen().Select(omen => omen.Name));
    }
}

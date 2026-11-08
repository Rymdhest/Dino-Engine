using Dino_Defenders.Stats;
using Dino_Defenders.Tooltips;
using static Dino_Defenders.Stats.M;

namespace Dino_Defenders.Progression
{
    /// <summary>Which half of what a land holds an omen is about: what its enemies are, or what the land itself is. Orders carry one of each as a Magic, and two of each as a Rare.</summary>
    public enum OmenSide
    {
        Foe,
        Land,
    }

    /// <summary>
    /// Something a land holds that makes it worse, and richer: what its enemies wear, what it does to whoever comes, or what it is. Each is one number, rolled from tiers that open with the land's level, and
    /// everything about it grows with that number, what it pays included, so a deeper land's omens are both worse and worth more. The id and its place in <see cref="Omens"/> are what's saved and sent:
    /// names and numbers can change freely, and a new one goes on the end. Read LANDS.md.
    /// </summary>
    public sealed class OmenDefinition
    {
        public int Number;
        public string Id = "";
        public string Name = "";

        /// <summary>What it reads as in the name of orders that carry it: a word before what they are for what enemies are, and a few after for what the land is.</summary>
        public string Affix = "";
        public OmenSide Side;
        public Tier[] Tiers = Array.Empty<Tier>();

        /// <summary>What everything hostile on the land wears, for this much of it.</summary>
        public Func<float, Mod[]> Foes = _ => Array.Empty<Mod>();

        /// <summary>What every hero on the land wears, for this much of it. Both ends of a connection put these on, so they're part of what a hero's step is worked out from.</summary>
        public Func<float, Mod[]> Heroes = _ => Array.Empty<Mod>();

        /// <summary>What it does, for what its modifiers can't say for themselves.</summary>
        public Func<float, string>? Says;

        // What each point of it pays, as shares.
        public float Quantity;
        public float Rarity;

        // What each point of it puts on the land, as shares.
        public float People;
        public float Packs;

        /// <summary>How often a land draws it, against the others.</summary>
        public float Weight = 1f;

        /// <summary>The first land level it can turn up on.</summary>
        public int FirstLevel => Tiers[0].MinLevel;

        /// <summary>The most a land can hold of it, however it came to be there.</summary>
        public float Most => Tiers[^1].High * Omens.MostStacked;

        /// <summary>What it does, for this much of it, as a sentence.</summary>
        public string Describe(float value)
        {
            if (Says != null) return Says(value);
            var parts = new List<string>();
            Mod[] foes = Foes(value);
            Mod[] heroes = Heroes(value);
            if (foes.Length > 0) parts.Add("Enemies: " + string.Join(", ", foes.Select(mod => mod.Describe())));
            if (heroes.Length > 0) parts.Add("Heroes: " + string.Join(", ", heroes.Select(mod => mod.Describe())));
            return string.Join(". ", parts);
        }

        /// <summary>Every modifier it has, whoever wears them, for anything that has to read them all.</summary>
        public IEnumerable<Mod> Mods(float value) => Foes(value).Concat(Heroes(value));

        /// <summary>Which of its tiers this much of it reached, from 0.</summary>
        public int TierOf(float value)
        {
            int tier = 0;
            for (int i = 0; i < Tiers.Length; i++)
            {
                if (Tiers[i].Low <= value) tier = i;
            }
            return tier;
        }

        /// <summary>The best tier a land of this level can roll, or -1 if it can't have this at all.</summary>
        public int TopTierAt(int level)
        {
            int top = -1;
            for (int i = 0; i < Tiers.Length; i++)
            {
                if (Tiers[i].MinLevel <= level) top = i;
            }
            return top;
        }

        /// <summary>A roll of it for a land of this level: the best tier it can reach, often one or two below that, and anywhere in that tier.</summary>
        public float Roll(int level, Dino_Engine.World.WorldRandom random, float quality = 0f)
        {
            int tier = Math.Max(TopTierAt(level), 0);
            while (tier > 0 && random.Chance(Omens.TierDrop * (1f - quality))) tier--;
            Tier rolled = Tiers[tier];
            return rolled.Low + (rolled.High - rolled.Low) * random.Float();
        }

        /// <summary>What it comes to for something that isn't rolled, such as what an orders' base always carries: the middle of the best tier there is at this level.</summary>
        public float Fixed(int level)
        {
            Tier tier = Tiers[Math.Max(TopTierAt(level), 0)];
            return MathF.Round((tier.Low + tier.High) * 0.5f);
        }
    }

    /// <summary>Every omen there is. A new one goes on the end, as they're sent by number and saved by id.</summary>
    public static class Omens
    {
        /// <summary>How far down from the best tier a roll tends to fall, each step a chance of this.</summary>
        public const float TierDrop = 0.35f;

        /// <summary>How far past its best tier one omen can be pushed, by a land and its orders holding the same.</summary>
        public const float MostStacked = 1.5f;

        private static readonly List<OmenDefinition> Everything = new();

        public static IReadOnlyList<OmenDefinition> All => Everything;

        private static OmenDefinition Add(OmenDefinition omen)
        {
            omen.Number = Everything.Count;
            Everything.Add(omen);
            return omen;
        }

        private static Tier[] Ladder(params (int level, float low, float high)[] steps) => steps.Select(s => new Tier(s.level, s.low, s.high)).ToArray();

        // The land levels each tier opens at, so that omens deepen together.
        private const int Second = 6;
        private const int Third = 14;
        private const int Fourth = 24;
        private const int Fifth = 38;

        // ---- What enemies are. Everything hostile on the land wears these, bosses too, so there's nothing here a hero couldn't meet anyway.

        public static readonly OmenDefinition Ironhide = Add(new OmenDefinition
        {
            Id = "ironhide", Name = "Ironhide", Affix = "Ironhide", Side = OmenSide.Foe,
            Tiers = Ladder((1, 15, 25), (Second, 26, 35), (Third, 36, 48), (Fourth, 49, 62), (Fifth, 63, 80)),
            Foes = v => new[] { More(Stat.Health, v), Inc(Stat.Armour, v) },
            Quantity = 0.0035f, Rarity = 0.0025f,
        });

        public static readonly OmenDefinition Bloodthirst = Add(new OmenDefinition
        {
            Id = "bloodthirst", Name = "Bloodthirsty", Affix = "Bloodthirsty", Side = OmenSide.Foe,
            Tiers = Ladder((1, 10, 16), (Second, 17, 24), (Third, 25, 33), (Fourth, 34, 43), (Fifth, 44, 55)),
            Foes = v => new[] { More(Stat.Damage, v) },
            Quantity = 0.005f, Rarity = 0.004f,
        });

        public static readonly OmenDefinition Quickened = Add(new OmenDefinition
        {
            Id = "quickened", Name = "Quickened", Affix = "Quickened", Side = OmenSide.Foe,
            Tiers = Ladder((1, 8, 12), (Second, 13, 18), (Third, 19, 24), (Fourth, 25, 30), (Fifth, 31, 38)),
            Foes = v => new[] { Inc(Stat.MoveSpeed, v), Inc(Stat.AttackSpeed, v), Inc(Stat.CastSpeed, v) },
            Quantity = 0.007f, Rarity = 0.005f,
        });

        public static readonly OmenDefinition Ashfall = Add(new OmenDefinition
        {
            Id = "ashfall", Name = "Ashen", Affix = "Ashen", Side = OmenSide.Foe,
            Tiers = Ladder((1, 20, 30), (Second, 31, 45), (Third, 46, 60), (Fourth, 61, 80)),
            Foes = v => new[] { GainAs(DamageType.Physical, DamageType.Fire, v), Chance(Tags.Burn, MathF.Round(v * 0.5f)) },
            Quantity = 0.0025f, Rarity = 0.002f,
        });

        public static readonly OmenDefinition Hoarfrost = Add(new OmenDefinition
        {
            Id = "hoarfrost", Name = "Rimed", Affix = "Rimed", Side = OmenSide.Foe,
            Tiers = Ladder((1, 20, 30), (Second, 31, 45), (Third, 46, 60), (Fourth, 61, 80)),
            Foes = v => new[] { GainAs(DamageType.Physical, DamageType.Cold, v), Inc(Stat.AilmentEffect, v, Tags.Chill) },
            Quantity = 0.0025f, Rarity = 0.002f,
        });

        public static readonly OmenDefinition Stormfront = Add(new OmenDefinition
        {
            Id = "stormfront", Name = "Stormborn", Affix = "Stormborn", Side = OmenSide.Foe,
            Tiers = Ladder((1, 20, 30), (Second, 31, 45), (Third, 46, 60), (Fourth, 61, 80)),
            Foes = v => new[] { GainAs(DamageType.Physical, DamageType.Lightning, v), Chance(Tags.Shock, MathF.Round(v * 0.5f)) },
            Quantity = 0.0025f, Rarity = 0.002f,
        });

        public static readonly OmenDefinition Venomous = Add(new OmenDefinition
        {
            Id = "venomous", Name = "Venomous", Affix = "Venomous", Side = OmenSide.Foe,
            Tiers = Ladder((2, 20, 30), (Second, 31, 45), (Third, 46, 60), (Fourth, 61, 80)),
            Foes = v => new[] { GainAs(DamageType.Physical, DamageType.Poison, v), Chance(Tags.Venom, MathF.Round(v * 0.5f)) },
            Quantity = 0.0025f, Rarity = 0.002f,
        });

        public static readonly OmenDefinition Leeching = Add(new OmenDefinition
        {
            Id = "leeching", Name = "Leeching", Affix = "Leeching", Side = OmenSide.Foe,
            Tiers = Ladder((3, 4, 7), (Third, 8, 12), (Fourth, 13, 18)),
            Foes = v => new[] { Flat(Stat.HealthLeech, v) },
            Quantity = 0.012f, Rarity = 0.009f,
        });

        public static readonly OmenDefinition Briars = Add(new OmenDefinition
        {
            Id = "briars", Name = "Thorned", Affix = "Thorned", Side = OmenSide.Foe,
            Tiers = Ladder((3, 6, 10), (Third, 11, 16), (Fourth, 17, 24)),
            Foes = v => new[] { Flat(Stat.Reflect, v) },
            Quantity = 0.01f, Rarity = 0.008f,
        });

        public static readonly OmenDefinition Keen = Add(new OmenDefinition
        {
            Id = "keen", Name = "Keen-edged", Affix = "Keen-edged", Side = OmenSide.Foe,
            Tiers = Ladder((4, 8, 12), (Third, 13, 18), (Fourth, 19, 26)),
            Foes = v => new[] { Flat(Stat.Penetration, v), Flat(Stat.CritChance, MathF.Round(v * 0.5f)) },
            Quantity = 0.009f, Rarity = 0.007f,
        });

        public static readonly OmenDefinition Spellproof = Add(new OmenDefinition
        {
            Id = "spellproof", Name = "Spellproof", Affix = "Spellproof", Side = OmenSide.Foe,
            Tiers = Ladder((2, 10, 18), (Third, 19, 28), (Fourth, 29, 40)),
            Foes = v => new[] { Resist(Tags.Elemental, v) },
            Quantity = 0.006f, Rarity = 0.005f,
        });

        public static readonly OmenDefinition Plated = Add(new OmenDefinition
        {
            Id = "plated", Name = "Plated", Affix = "Plated", Side = OmenSide.Foe,
            Tiers = Ladder((2, 25, 40), (Third, 41, 65), (Fourth, 66, 100)),
            Foes = v => new[] { Inc(Stat.Armour, v * 2f), Flat(Stat.PhysicalReduction, MathF.Round(v * 0.1f)) },
            Quantity = 0.0025f, Rarity = 0.002f,
        });

        public static readonly OmenDefinition Hexing = Add(new OmenDefinition
        {
            Id = "hexing", Name = "Hexing", Affix = "Hexing", Side = OmenSide.Foe,
            Tiers = Ladder((Second, 15, 25), (Fourth, 26, 40)),
            Foes = v => new[] { Chance(Tags.Hex, v) },
            Quantity = 0.004f, Rarity = 0.0035f,
        });

        public static readonly OmenDefinition Bloodletting = Add(new OmenDefinition
        {
            Id = "bloodletting", Name = "Bloodletting", Affix = "Bloodletting", Side = OmenSide.Foe,
            Tiers = Ladder((1, 15, 25), (Third, 26, 40)),
            Foes = v => new[] { Chance(Tags.Bleed, v) },
            Quantity = 0.004f, Rarity = 0.0035f,
        });

        public static readonly OmenDefinition Unyielding = Add(new OmenDefinition
        {
            Id = "unyielding", Name = "Unyielding", Affix = "Unyielding", Side = OmenSide.Foe,
            Tiers = Ladder((4, 20, 30), (Third, 31, 45)),
            Foes = v => new[] { Inc(Stat.AilmentTaken, -v), Inc(Stat.StunThreshold, v * 2f) },
            Says = v => $"Ailments on Enemies wear off {Say.Percent(v)} sooner, and Enemies have {Say.Percent(v * 2f)} increased Stun Threshold",
            Quantity = 0.003f, Rarity = 0.0025f,
        });

        // ---- What the land is. Some of these change who's on it, and so are part of the world both ends grow from what they're told.

        public static readonly OmenDefinition Teeming = Add(new OmenDefinition
        {
            Id = "teeming", Name = "Teeming", Affix = "of the Muster", Side = OmenSide.Land,
            Tiers = Ladder((1, 15, 25), (Second, 26, 40), (Third, 41, 60), (Fourth, 61, 80)),
            Says = v => $"Packs and those who hold places are {Say.Percent(v)} larger, and no stronger for it",
            People = 0.01f,
        });

        public static readonly OmenDefinition Overrun = Add(new OmenDefinition
        {
            Id = "overrun", Name = "Overrun", Affix = "of the March", Side = OmenSide.Land,
            Tiers = Ladder((1, 10, 18), (Second, 19, 30), (Third, 31, 45), (Fourth, 46, 60)),
            Says = v => $"{Say.Percent(v)} more packs on the land, and no stronger for it",
            Packs = 0.01f,
        });

        public static readonly OmenDefinition Warhost = Add(new OmenDefinition
        {
            Id = "warhost", Name = "Warhost", Affix = "of the Warhost", Side = OmenSide.Land,
            Tiers = Ladder((3, 50, 80), (Third, 81, 120), (Fourth, 121, 180)),
            Says = v => $"Every pack has a leader if it can, and leaders are {Say.Percent(v)} more likely to be Rare, up to {Say.Share(Rulebook.MostWarhostLeaderRares)}. Those who hold places are Rare {Say.Share(Rulebook.WarhostRareChance)} of the time instead of {Say.Share(Rulebook.RareChance)}",
            Quantity = 0.001f, Rarity = 0.0015f,
        });

        public static readonly OmenDefinition Gilded = Add(new OmenDefinition
        {
            Id = "gilded", Name = "Gilded", Affix = "of Plunder", Side = OmenSide.Land,
            Tiers = Ladder((2, 25, 45), (Third, 46, 70), (Fourth, 71, 100)),
            Says = v => $"Each chest has a {Say.Percent(MathF.Min(v, 100f))} Chance to hold what the next kind of chest up holds",
            Weight = 0.8f,
        });

        public static readonly OmenDefinition Night = Add(new OmenDefinition
        {
            Id = "night", Name = "Endless Night", Affix = "of Darkness", Side = OmenSide.Land,
            Tiers = Ladder((5, 20, 30), (Third, 31, 45)),
            Says = v => $"The whole land counts as Darkness, and Enemies notice you from {Say.Percent(v)} further away",
            Quantity = 0.005f, Rarity = 0.005f, Weight = 0.7f,
        });

        public static readonly OmenDefinition Volatile = Add(new OmenDefinition
        {
            Id = "volatile", Name = "Volatile", Affix = "of Embers", Side = OmenSide.Land,
            Tiers = Ladder((4, 30, 45), (Third, 46, 65), (Fourth, 66, 90)),
            Foes = _ => new[] { Rules(Rule.BurstsOnDeath) },
            Says = v => $"{Say.Percent(MathF.Min(v, 100f))} of Enemies burst {Say.Seconds(Rulebook.BurstDelay)} after they die, for Fire Indirect Damage within {Say.Metres(Rulebook.BurstRadius)}, worth {Say.Share(Rulebook.BurstShare)} of an ordinary Enemy Hit",
            Quantity = 0.004f, Rarity = 0.004f,
        });

        public static readonly OmenDefinition Rallying = Add(new OmenDefinition
        {
            Id = "rallying", Name = "Rallying", Affix = "of Vengeance", Side = OmenSide.Land,
            Tiers = Ladder((Second, 40, 60), (Fourth, 61, 100)),
            Foes = _ => new[] { Rules(Rule.RalliesOnDeath) },
            Says = v => $"{Say.Percent(MathF.Min(v, 100f))} of Enemies drive the rest on as they die, giving Rallied to allies within {Say.Metres(Rulebook.RallyReach)}",
            Quantity = 0.003f, Rarity = 0.003f,
        });

        public static readonly OmenDefinition Bloodlines = Add(new OmenDefinition
        {
            Id = "bloodlines", Name = "Bloodlines", Affix = "of the Pens", Side = OmenSide.Land,
            Tiers = Ladder((2, 20, 35), (Third, 36, 60), (Fourth, 61, 100)),
            Says = v => $"Magic and Rare Enemies are {Say.Percent(v)} more common",
            Rarity = 0.004f,
        });

        public static readonly OmenDefinition Wrath = Add(new OmenDefinition
        {
            Id = "wrath", Name = "Tyrant's Wrath", Affix = "of the Tyrant", Side = OmenSide.Land,
            Tiers = Ladder((1, 15, 25), (10, 26, 40), (22, 41, 60)),
            Says = v => $"The tyrant has {Say.Percent(v)} more Health and deals {Say.Percent(v)} more Damage, and drops {Say.Percent(v)} more",
            Quantity = 0.002f,
        });

        // ---- What the land does to whoever comes. Both ends of a connection put these on a hero, as they're part of every step.

        public static readonly OmenDefinition Bleak = Add(new OmenDefinition
        {
            Id = "bleak", Name = "Bleak", Affix = "of Bleakness", Side = OmenSide.Land,
            Tiers = Ladder((3, 15, 25), (Third, 26, 40)),
            Heroes = v => new[] { More(Stat.Recovery, -v) },
            Quantity = 0.005f, Rarity = 0.004f,
        });

        public static readonly OmenDefinition Exposed = Add(new OmenDefinition
        {
            Id = "exposed", Name = "Exposed", Affix = "of Exposure", Side = OmenSide.Land,
            Tiers = Ladder((5, 8, 14), (16, 15, 22), (30, 23, 30)),
            Heroes = v => new[] { Resist(Tags.Elemental, -v) },
            Quantity = 0.01f, Rarity = 0.008f,
        });

        public static readonly OmenDefinition Frail = Add(new OmenDefinition
        {
            Id = "frail", Name = "Frail", Affix = "of Frailty", Side = OmenSide.Land,
            Tiers = Ladder((8, 6, 10), (20, 11, 16)),
            Heroes = v => new[] { More(Stat.DamageTaken, v) },
            Quantity = 0.015f, Rarity = 0.012f,
        });

        public static readonly OmenDefinition Parched = Add(new OmenDefinition
        {
            Id = "parched", Name = "Parched", Affix = "of Thirst", Side = OmenSide.Land,
            Tiers = Ladder((3, 15, 25), (Third, 26, 40)),
            Heroes = v => new[] { More(Stat.FlaskEffect, -v) },
            Quantity = 0.004f, Rarity = 0.003f,
        });

        public static readonly OmenDefinition Sapped = Add(new OmenDefinition
        {
            Id = "sapped", Name = "Sapped", Affix = "of Weariness", Side = OmenSide.Land,
            Tiers = Ladder((Second, 10, 16), (18, 17, 25)),
            Heroes = v => new[] { Inc(Stat.BookCost, v) },
            Quantity = 0.006f, Rarity = 0.005f,
        });

        public static readonly OmenDefinition Lagging = Add(new OmenDefinition
        {
            Id = "lagging", Name = "Lagging", Affix = "of the Long Wait", Side = OmenSide.Land,
            Tiers = Ladder((Second, 10, 16), (18, 17, 25)),
            Heroes = v => new[] { Inc(Stat.CooldownRecovery, -v) },
            Quantity = 0.006f, Rarity = 0.005f,
        });

        public static OmenDefinition? Find(string id) => Everything.FirstOrDefault(omen => omen.Id == id);
    }
}

using Dino_Defenders.Stats;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using static Dino_Defenders.Stats.M;

namespace Dino_Defenders.Progression
{
    public enum ItemSlot
    {
        Weapon,
        Shield,
        Head,
        Body,
        Feet,
        Ring,
        Amulet,
    }

    public enum Rarity
    {
        Common,
        Magic,
        Rare,
        Legendary,
    }

    public enum WeaponKind : byte
    {
        Sword,
        Axe,
        Mace,
        Dagger,
        Spear,
        Bow,
        Staff,
        Wand,
    }

    /// <summary>What a kind of weapon is like, whoever made it.</summary>
    public readonly record struct WeaponTraits(float AttackTime, float CritChance, bool TwoHanded, Tags Tag, Mod[] Mods);

    public static class WeaponKinds
    {
        private static readonly WeaponTraits[] Table =
        {
            new(0.6f, 5f, false, Tags.Sword, new[] { Flat(Stat.Accuracy, 20f) }),
            new(0.68f, 5f, false, Tags.Axe, new[] { Inc(Stat.Damage, 8f, Tags.Physical) }),
            new(0.82f, 5f, true, Tags.Mace, new[] { Chance(Tags.Stun, 10f) }),
            new(0.48f, 8f, false, Tags.Dagger, new[] { Flat(Stat.CritMultiplier, 20f) }),
            new(0.62f, 6f, true, Tags.Spear, new[] { Flat(Stat.MeleeReach, 1.5f) }),
            new(0.7f, 6f, true, Tags.Bow, new[] { Inc(Stat.ProjectileSpeed, 15f) }),
            new(0.75f, 7f, true, Tags.Staff, new[] { Inc(Stat.Damage, 18f, Tags.Spell) }),
            new(0.56f, 7f, false, Tags.Wand, new[] { Inc(Stat.Damage, 10f, Tags.Spell) }),
        };

        public static WeaponTraits Of(WeaponKind kind) => Table[(int)kind];
    }

    /// <summary>One line of an item: which affix it is, and how much of it rolled. What that does is the affix's business, so old items change when an affix does.</summary>
    public sealed class ItemLine
    {
        public string Affix { get; set; } = "";
        public float Value { get; set; }

        public ItemLine() { }

        public ItemLine(string affix, float value)
        {
            Affix = affix;
            Value = value;
        }
    }

    public sealed class Item
    {
        private const float WeaponSpread = 0.2f;

        public string Name { get; set; } = "";
        public ItemSlot Slot { get; set; }
        public Rarity Rarity { get; set; }
        public int Level { get; set; }
        public float WeaponDamage { get; set; }
        public WeaponKind Kind { get; set; }
        public float Armour { get; set; }
        public float Evasion { get; set; }
        public float Barrier { get; set; }
        public List<ItemLine> Lines { get; set; } = new();

        /// <summary>Which one of a kind this is, if it is.</summary>
        public string? Unique { get; set; }

        public int SellValue => (int)(4f * Level * (1f + (int)Rarity * 1.6f + Lines.Count * 0.5f));
        public int BuyValue => SellValue * 4;

        public bool IsTwoHanded => Slot == ItemSlot.Weapon && WeaponKinds.Of(Kind).TwoHanded;

        /// <summary>What this adds to whoever is wearing it. A line whose affix has gone from the game simply does nothing.</summary>
        public IEnumerable<Mod> Mods()
        {
            if (Armour > 0f) yield return Flat(Stat.Armour, Armour);
            if (Evasion > 0f) yield return Flat(Stat.Evasion, Evasion);
            if (Barrier > 0f) yield return Flat(Stat.Barrier, Barrier);
            if (Slot == ItemSlot.Weapon)
            {
                foreach (Mod mod in WeaponKinds.Of(Kind).Mods) yield return mod;
            }

            foreach (ItemLine line in Lines)
            {
                if (AffixCatalog.Find(line.Affix) is not Affix affix) continue;
                foreach (Mod mod in affix.Build(line.Value)) yield return mod;
            }

            if (Unique != null && UniqueCatalog.Find(Unique) is UniqueDefinition unique)
            {
                foreach (Mod mod in unique.Mods) yield return mod;
            }
        }

        /// <summary>This item in a hand, as far as an attack goes.</summary>
        public WeaponProfile Weapon()
        {
            WeaponTraits traits = WeaponKinds.Of(Kind);
            Tags tags = traits.Tag | (traits.TwoHanded ? Tags.TwoHanded : Tags.OneHanded);
            return new WeaponProfile(WeaponDamage * (1f - WeaponSpread), WeaponDamage * (1f + WeaponSpread), traits.AttackTime, traits.CritChance, tags);
        }

        /// <summary>What it is before anything has been rolled on it.</summary>
        public IEnumerable<string> DescribeBase()
        {
            if (WeaponDamage > 0f)
            {
                WeaponProfile weapon = Weapon();
                yield return $"{weapon.Min:0} to {weapon.Max:0} Damage, {1f / weapon.AttackTime:0.##} attacks a second";
            }
            if (Armour > 0f) yield return $"{Armour:0} Armour";
            if (Evasion > 0f) yield return $"{Evasion:0} Evasion";
            if (Barrier > 0f) yield return $"{Barrier:0} Barrier";
        }

        /// <summary>The lines a player reads, in the order they should read them.</summary>
        public IEnumerable<string> Describe()
        {
            foreach (ItemLine line in Lines)
            {
                if (AffixCatalog.Find(line.Affix) is not Affix affix) continue;
                foreach (Mod mod in affix.Build(line.Value)) yield return mod.Describe();
            }

            if (Unique != null && UniqueCatalog.Find(Unique) is UniqueDefinition unique)
            {
                foreach (Mod mod in unique.Mods) yield return mod.Describe();
            }
        }
    }

    public sealed record Tier(int MinLevel, float Low, float High);

    /// <summary>A thing an item can have: what it's called, where it can be, how much it can roll at each level, and what a roll means in modifiers.</summary>
    public sealed class Affix
    {
        public string Id = "";
        public bool Prefix;
        public string[] Names = Array.Empty<string>();
        public ItemSlot[] Slots = Array.Empty<ItemSlot>();
        public Tier[] Tiers = Array.Empty<Tier>();
        public Func<float, Mod[]> Build = _ => Array.Empty<Mod>();
    }

    /// <summary>
    /// Everything an item can roll. Ids are saved in items, so renaming one loses it from every item that has it; a value's meaning can change
    /// freely. Between them these cover every stat a build can want, so that gear can push any build further rather than only the obvious ones.
    /// </summary>
    public static class AffixCatalog
    {
        private static readonly ItemSlot[] Weapon = { ItemSlot.Weapon };
        private static readonly ItemSlot[] WeaponJewels = { ItemSlot.Weapon, ItemSlot.Ring, ItemSlot.Amulet };
        private static readonly ItemSlot[] Jewels = { ItemSlot.Ring, ItemSlot.Amulet };
        private static readonly ItemSlot[] Armours = { ItemSlot.Shield, ItemSlot.Head, ItemSlot.Body, ItemSlot.Feet };
        private static readonly ItemSlot[] Upper = { ItemSlot.Shield, ItemSlot.Head, ItemSlot.Body };
        private static readonly ItemSlot[] Clothes = { ItemSlot.Head, ItemSlot.Body, ItemSlot.Ring, ItemSlot.Amulet };
        private static readonly ItemSlot[] Worn = { ItemSlot.Shield, ItemSlot.Head, ItemSlot.Body, ItemSlot.Feet, ItemSlot.Ring, ItemSlot.Amulet };
        private static readonly ItemSlot[] Feet = { ItemSlot.Feet };
        private static readonly ItemSlot[] Shields = { ItemSlot.Shield };

        private static readonly Dictionary<string, Affix> ById = new();
        private static readonly List<Affix> Everything = new();

        public static IReadOnlyList<Affix> All => Everything;

        public static Affix? Find(string id) => ById.GetValueOrDefault(id);

        private static Tier[] Ladder(params (int level, float low, float high)[] steps) => steps.Select(s => new Tier(s.level, s.low, s.high)).ToArray();

        private static readonly Tier[] Percent = Ladder((1, 8, 15), (6, 16, 26), (12, 27, 40), (20, 41, 58), (30, 59, 80));
        private static readonly Tier[] SmallPercent = Ladder((1, 4, 8), (8, 9, 14), (16, 15, 22), (28, 23, 32));
        private static readonly Tier[] Added = Ladder((1, 1, 3), (6, 4, 7), (12, 8, 13), (20, 14, 22), (30, 23, 36), (42, 37, 55));
        private static readonly Tier[] Pool = Ladder((1, 10, 20), (6, 21, 40), (12, 41, 70), (20, 71, 110), (32, 111, 160), (45, 161, 230));
        private static readonly Tier[] Resistance = Ladder((1, 6, 12), (7, 13, 20), (14, 21, 30), (24, 31, 42));
        private static readonly Tier[] Rating = Ladder((1, 3, 7), (6, 8, 16), (12, 17, 30), (20, 31, 50), (30, 51, 85), (42, 86, 130));
        private static readonly Tier[] Chances = Ladder((2, 5, 10), (10, 11, 18), (20, 19, 28));

        private static void Add(string id, bool prefix, string[] names, ItemSlot[] slots, Tier[] tiers, Func<float, Mod[]> build)
        {
            var affix = new Affix { Id = id, Prefix = prefix, Names = names, Slots = slots, Tiers = tiers, Build = build };
            ById.Add(id, affix);
            Everything.Add(affix);
        }

        private static void Add(string id, bool prefix, string[] names, ItemSlot[] slots, Tier[] tiers, Func<float, Mod> build) => Add(id, prefix, names, slots, tiers, v => new[] { build(v) });

        static AffixCatalog()
        {
            // Damage, on what deals it. These ids are what saves from before this system map onto, so they stay.
            Add("damage", true, new[] { "Sharp", "Keen", "Cruel", "Merciless", "Ruinous" }, Weapon, Ladder((1, 8, 15), (5, 16, 28), (10, 29, 45), (16, 46, 70), (24, 71, 100)), v => Inc(Stat.Damage, v));
            Add("added-physical", true, new[] { "Jagged", "Barbed", "Rending", "Brutal", "Savage", "Flaying" }, WeaponJewels, Added, v => M.Added(DamageType.Physical, v));
            Add("added-fire", true, new[] { "Warm", "Smouldering", "Burning", "Blazing", "Infernal", "Solar" }, WeaponJewels, Added, v => M.Added(DamageType.Fire, v));
            Add("added-cold", true, new[] { "Chill", "Frosted", "Biting", "Glacial", "Polar", "Absolute" }, WeaponJewels, Added, v => M.Added(DamageType.Cold, v));
            Add("added-lightning", true, new[] { "Humming", "Sparking", "Crackling", "Arcing", "Thundering", "Heavenly" }, WeaponJewels, Added, v => M.Added(DamageType.Lightning, v));
            Add("added-magic", true, new[] { "Faint", "Charmed", "Ensorcelled", "Eldritch", "Unbound", "Sovereign" }, WeaponJewels, Added, v => M.Added(DamageType.Magic, v));
            Add("added-poison", true, new[] { "Tainted", "Sickly", "Foul", "Venomous", "Virulent", "Plaguebearing" }, WeaponJewels, Added, v => M.Added(DamageType.Poison, v));

            Add("spell-damage", true, new[] { "Apprentice's", "Adept's", "Scholar's", "Magus'", "Archmage's" }, new[] { ItemSlot.Weapon, ItemSlot.Amulet }, Percent, v => Inc(Stat.Damage, v, Tags.Spell));
            Add("attack-damage", true, new[] { "Soldier's", "Veteran's", "Champion's", "Warmaster's", "Conqueror's" }, new[] { ItemSlot.Weapon, ItemSlot.Amulet }, Percent, v => Inc(Stat.Damage, v, Tags.Attack));
            Add("physical-damage", true, new[] { "Heavy", "Weighted", "Massive", "Crushing", "Titanic" }, WeaponJewels, Percent, v => Inc(Stat.Damage, v, Tags.Physical));
            Add("fire-damage", true, new[] { "Ember", "Flame", "Pyre", "Conflagrant", "Cremating" }, WeaponJewels, Percent, v => Inc(Stat.Damage, v, Tags.Fire));
            Add("cold-damage", true, new[] { "Rime", "Hoar", "Winter", "Boreal", "Deathly Cold" }, WeaponJewels, Percent, v => Inc(Stat.Damage, v, Tags.Cold));
            Add("lightning-damage", true, new[] { "Static", "Galvanic", "Storm", "Tempest", "Thunderhead" }, WeaponJewels, Percent, v => Inc(Stat.Damage, v, Tags.Lightning));
            Add("magic-damage", true, new[] { "Hedge", "Runed", "Sigilled", "Occult", "Forbidden" }, WeaponJewels, Percent, v => Inc(Stat.Damage, v, Tags.Magic));
            Add("poison-damage", true, new[] { "Noxious", "Rancid", "Blighted", "Pestilent", "Rotting" }, WeaponJewels, Percent, v => Inc(Stat.Damage, v, Tags.Poison));
            Add("elemental-damage", true, new[] { "Prismatic", "Coloured", "Elemental", "Primal", "Primordial" }, Jewels, SmallPercent, v => Inc(Stat.Damage, v, Tags.Elemental));
            Add("area-damage", true, new[] { "Sweeping", "Rolling", "Broad", "Sundering", "Cataclysmic" }, Jewels, SmallPercent, v => Inc(Stat.Damage, v, Tags.Area));
            Add("projectile-damage", true, new[] { "Fletched", "Balanced", "Whistling", "Piercing", "Unerring" }, Jewels, SmallPercent, v => Inc(Stat.Damage, v, Tags.Projectile));
            Add("melee-damage", true, new[] { "Brawler's", "Duellist's", "Gladiator's", "Slaughterer's", "Warlord's" }, Jewels, SmallPercent, v => Inc(Stat.Damage, v, Tags.Melee));
            Add("lingering-damage", true, new[] { "Festering", "Lingering", "Wasting", "Slow", "Inexorable" }, WeaponJewels, Percent, v => Inc(Stat.Damage, v, Tags.DamageOverTime));
            Add("minion-damage", true, new[] { "Shepherd's", "Master's", "Tyrant's", "Lich's", "Necrarch's" }, new[] { ItemSlot.Weapon, ItemSlot.Ring, ItemSlot.Amulet, ItemSlot.Head }, Percent, v => Inc(Stat.Damage, v, Tags.Minion));

            Add("attack-speed", false, new[] { "of Haste", "of Fury", "of the Storm", "of the Whirlwind" }, new[] { ItemSlot.Weapon, ItemSlot.Ring }, Ladder((3, 4, 8), (10, 9, 14), (18, 15, 22), (30, 23, 30)), v => Inc(Stat.AttackSpeed, v));
            Add("cast-speed", false, new[] { "of Muttering", "of Fluency", "of Recitation", "of the Unbroken Word" }, new[] { ItemSlot.Weapon, ItemSlot.Ring, ItemSlot.Amulet }, Ladder((3, 4, 8), (10, 9, 14), (18, 15, 22), (30, 23, 30)), v => Inc(Stat.CastSpeed, v));
            Add("crit-chance", false, new[] { "of Precision", "of the Hawk", "of the Unerring", "of the Killing Eye" }, WeaponJewels, Ladder((2, 1, 3), (9, 3.5f, 6), (17, 6.5f, 10), (30, 10.5f, 14)), v => Flat(Stat.CritChance, v));
            Add("crit-increased", false, new[] { "of Focus", "of Intent", "of Certainty", "of Fate" }, WeaponJewels, Percent, v => Inc(Stat.CritChance, v));
            Add("crit-multiplier", false, new[] { "of Wounding", "of Slaughter", "of Ruin", "of Annihilation" }, new[] { ItemSlot.Weapon, ItemSlot.Amulet }, Ladder((4, 10, 20), (11, 21, 35), (19, 36, 60), (32, 61, 85)), v => Flat(Stat.CritMultiplier, v));
            Add("accuracy", false, new[] { "of the Marksman", "of the Sharpshooter", "of the Sniper" }, WeaponJewels, Pool, v => Flat(Stat.Accuracy, v));
            Add("penetration", false, new[] { "of Piercing", "of Unmaking", "of Undoing" }, new[] { ItemSlot.Weapon, ItemSlot.Amulet }, Ladder((8, 4, 7), (18, 8, 12), (30, 13, 18)), v => Flat(Stat.Penetration, v));

            Add("area", false, new[] { "of Reach", "of Breadth", "of the Expanse", "of Horizons" }, new[] { ItemSlot.Weapon, ItemSlot.Amulet, ItemSlot.Head }, SmallPercent, v => Inc(Stat.AreaOfEffect, v));
            Add("duration", false, new[] { "of Patience", "of Endurance", "of Ages" }, new[] { ItemSlot.Amulet, ItemSlot.Head, ItemSlot.Ring }, SmallPercent, v => Inc(Stat.SkillDuration, v));
            Add("cooldown", false, new[] { "of Readiness", "of Renewal", "of the Eternal Hour" }, new[] { ItemSlot.Amulet, ItemSlot.Head, ItemSlot.Ring }, Ladder((5, 4, 7), (15, 8, 12), (28, 13, 18)), v => Inc(Stat.CooldownRecovery, v));
            Add("cost", false, new[] { "of Thrift", "of Economy", "of the Miser" }, new[] { ItemSlot.Amulet, ItemSlot.Head, ItemSlot.Ring }, Ladder((3, 4, 7), (12, 8, 12), (24, 13, 18)), v => Inc(Stat.SkillCost, -v));
            Add("projectile-speed", false, new[] { "of Flight", "of the Arrow", "of the Falcon" }, new[] { ItemSlot.Weapon, ItemSlot.Ring }, SmallPercent, v => Inc(Stat.ProjectileSpeed, v));
            Add("skill-level", false, new[] { "of Learning", "of Mastery" }, new[] { ItemSlot.Weapon, ItemSlot.Amulet }, Ladder((10, 1, 1), (26, 2, 2)), v => Flat(Stat.SkillLevel, MathF.Round(v)));

            Add("burn-chance", false, new[] { "of Kindling", "of Immolation", "of the Pyre" }, WeaponJewels, Chances, v => Chance(Tags.Burn, v));
            Add("bleed-chance", false, new[] { "of Cuts", "of Lacerating", "of Exsanguination" }, WeaponJewels, Chances, v => Chance(Tags.Bleed, v));
            Add("venom-chance", false, new[] { "of the Asp", "of the Viper", "of the Cobra" }, WeaponJewels, Chances, v => Chance(Tags.Venom, v));
            Add("shock-chance", false, new[] { "of Jolting", "of Shocking", "of Paralysis" }, WeaponJewels, Chances, v => Chance(Tags.Shock, v));
            Add("freeze-chance", false, new[] { "of Numbing", "of Stillness", "of the Glacier" }, WeaponJewels, Chances, v => Chance(Tags.Freeze, v));
            Add("hex-chance", false, new[] { "of Ill Omen", "of Hexing", "of the Curse" }, WeaponJewels, Chances, v => Chance(Tags.Hex, v));
            Add("stun-chance", false, new[] { "of Staggering", "of Stunning", "of the Hammer" }, Weapon, Chances, v => Chance(Tags.Stun, v));
            Add("ailment-duration", false, new[] { "of Suffering", "of Torment", "of Agony" }, Jewels, SmallPercent, v => Inc(Stat.AilmentDuration, v));
            Add("reaction-damage", false, new[] { "of Catalysis", "of Reaction", "of the Crucible" }, new[] { ItemSlot.Weapon, ItemSlot.Amulet, ItemSlot.Ring }, Percent, v => Inc(Stat.ReactionDamage, v));

            Add("health-leech", false, new[] { "of the Leech", "of the Lamprey", "of the Vampire" }, new[] { ItemSlot.Weapon, ItemSlot.Ring }, Ladder((6, 0.3f, 0.6f), (16, 0.7f, 1.1f), (28, 1.2f, 1.8f)), v => Flat(Stat.HealthLeech, v));
            Add("mana-leech", false, new[] { "of Thirst", "of Draining", "of the Mind Eater" }, new[] { ItemSlot.Weapon, ItemSlot.Ring }, Ladder((6, 0.2f, 0.4f), (16, 0.5f, 0.8f), (28, 0.9f, 1.3f)), v => Flat(Stat.ManaLeech, v));
            Add("health-on-hit", false, new[] { "of Mending", "of Knitting" }, new[] { ItemSlot.Weapon, ItemSlot.Ring }, Ladder((4, 1, 2.5f), (13, 3, 6), (26, 7, 12)), v => Flat(Stat.HealthOnHit, v));
            Add("mana-on-hit", false, new[] { "of Siphoning", "of Tapping" }, new[] { ItemSlot.Weapon, ItemSlot.Ring }, Ladder((4, 0.5f, 1.5f), (13, 2, 3.5f), (26, 4, 7)), v => Flat(Stat.ManaOnHit, v));
            Add("barrier-on-kill", false, new[] { "of the Husk", "of the Carapace" }, new[] { ItemSlot.Weapon, ItemSlot.Ring, ItemSlot.Amulet }, Pool, v => Flat(Stat.BarrierOnKill, v * 0.4f));
            Add("health-on-kill", false, new[] { "of Harvest", "of Reaping" }, new[] { ItemSlot.Weapon, ItemSlot.Ring, ItemSlot.Amulet }, Pool, v => Flat(Stat.HealthOnKill, v * 0.5f));
            Add("mana-on-kill", false, new[] { "of Gleaning", "of Absorption" }, new[] { ItemSlot.Weapon, ItemSlot.Ring, ItemSlot.Amulet }, Pool, v => Flat(Stat.ManaOnKill, v * 0.3f));

            // Staying alive.
            Add("health", false, new[] { "of the Ox", "of the Bear", "of the Oak", "of the Mountain", "of the Titan", "of the Colossus" }, new[] { ItemSlot.Head, ItemSlot.Body, ItemSlot.Shield, ItemSlot.Ring, ItemSlot.Amulet }, Pool, v => Flat(Stat.Health, v));
            Add("health-percent", true, new[] { "Hale", "Robust", "Stalwart", "Hearty" }, Clothes, Ladder((8, 3, 5), (18, 6, 9), (30, 10, 14)), v => Inc(Stat.Health, v));
            Add("mana", false, new[] { "of the Mind", "of the Sage", "of Insight", "of the Well", "of the Deep", "of the Abyss" }, new[] { ItemSlot.Head, ItemSlot.Ring, ItemSlot.Amulet, ItemSlot.Weapon }, Pool, v => Flat(Stat.Mana, v * 0.7f));
            Add("mana-percent", true, new[] { "Lucid", "Brimming", "Boundless" }, Clothes, Ladder((8, 4, 7), (18, 8, 12), (30, 13, 18)), v => Inc(Stat.Mana, v));
            Add("barrier", true, new[] { "Warded", "Shielded", "Sheltered", "Enclosed", "Bastioned", "Inviolate" }, Worn, Pool, v => Flat(Stat.Barrier, v * 0.8f));
            Add("barrier-percent", true, new[] { "Shimmering", "Gleaming", "Radiant" }, Upper, Percent, v => Inc(Stat.Barrier, v));
            Add("armour", true, new[] { "Sturdy", "Reinforced", "Fortified", "Unyielding", "Adamant", "Impregnable" }, Armours, Rating, v => Flat(Stat.Armour, v));
            Add("armour-percent", true, new[] { "Iron", "Steel", "Mithril", "Dragonscale" }, Upper, Percent, v => Inc(Stat.Armour, v));
            Add("evasion", true, new[] { "Fleet", "Agile", "Lithe", "Elusive", "Phantasmal", "Vanishing" }, Armours, Rating, v => Flat(Stat.Evasion, v));
            Add("evasion-percent", true, new[] { "Shadowed", "Blurred", "Ghostly", "Unseen" }, Upper, Percent, v => Inc(Stat.Evasion, v));
            Add("block", false, new[] { "of Deflection", "of Parrying", "of Warding Off" }, Shields, Ladder((1, 3, 6), (12, 7, 11), (26, 12, 16)), v => Flat(Stat.BlockChance, v));
            Add("fire-resistance", false, new[] { "of the Salamander", "of the Hearth", "of the Furnace", "of the Sun" }, Worn, Resistance, v => Resist(Tags.Fire, v));
            Add("cold-resistance", false, new[] { "of the Seal", "of the Fur", "of the Tundra", "of the Pole" }, Worn, Resistance, v => Resist(Tags.Cold, v));
            Add("lightning-resistance", false, new[] { "of the Rod", "of Grounding", "of the Faraday", "of the Eye" }, Worn, Resistance, v => Resist(Tags.Lightning, v));
            Add("magic-resistance", false, new[] { "of Unbelief", "of Warding", "of the Null", "of the Void" }, Worn, Resistance, v => Resist(Tags.Magic, v));
            Add("poison-resistance", false, new[] { "of the Antidote", "of Purity", "of the Unblemished", "of the Immune" }, Worn, Resistance, v => Resist(Tags.Poison, v));
            Add("all-resistance", false, new[] { "of the Prism", "of the Spectrum", "of the Rainbow" }, Jewels, Ladder((6, 3, 6), (16, 7, 11), (28, 12, 16)), v => Resist(Tags.Elemental, v));
            Add("flat-reduction", false, new[] { "of Toughness", "of Hide", "of Scales", "of the Tortoise" }, Upper, Ladder((3, 1, 2), (12, 3, 6), (22, 7, 12), (34, 13, 20)), v => Flat(Stat.FlatReduction, v));
            Add("physical-reduction", false, new[] { "of Padding", "of Bulwarks" }, new[] { ItemSlot.Body, ItemSlot.Shield }, Ladder((10, 2, 3), (24, 4, 6)), v => Flat(Stat.PhysicalReduction, v));
            Add("taken-from-mana", false, new[] { "of the Mindful", "of Mental Fortitude" }, new[] { ItemSlot.Head, ItemSlot.Amulet }, Ladder((10, 3, 6), (24, 7, 11)), v => Flat(Stat.TakenFromMana, v));
            Add("health-regen", false, new[] { "of Renewal", "of Vigour", "of the Spring", "of the Hydra" }, new[] { ItemSlot.Body, ItemSlot.Head, ItemSlot.Ring, ItemSlot.Amulet }, Ladder((2, 0.5f, 1.2f), (9, 1.3f, 2.5f), (17, 2.6f, 4.5f), (30, 4.6f, 9f)), v => Flat(Stat.HealthRegen, v));
            Add("health-regen-percent", false, new[] { "of Convalescence", "of Regrowth" }, new[] { ItemSlot.Body, ItemSlot.Amulet }, Ladder((12, 0.3f, 0.6f), (26, 0.7f, 1.2f)), v => Flat(Stat.HealthRegenPercent, v));
            Add("mana-regen", false, new[] { "of Clarity", "of Meditation", "of Serenity", "of the Sea" }, new[] { ItemSlot.Head, ItemSlot.Ring, ItemSlot.Amulet, ItemSlot.Weapon }, Ladder((2, 0.4f, 1f), (9, 1.1f, 2f), (17, 2.1f, 3.5f), (30, 3.6f, 7f)), v => Flat(Stat.ManaRegen, v));
            Add("barrier-recharge", false, new[] { "of Restoration", "of the Rampart" }, new[] { ItemSlot.Body, ItemSlot.Amulet }, SmallPercent, v => Inc(Stat.BarrierRecharge, v));
            Add("recovery", false, new[] { "of the Physician", "of the Healer", "of the Saint" }, new[] { ItemSlot.Amulet, ItemSlot.Body }, SmallPercent, v => Inc(Stat.Recovery, v));
            Add("reflect", false, new[] { "of Thorns", "of Spines", "of the Briar" }, new[] { ItemSlot.Body, ItemSlot.Shield }, Ladder((3, 5, 10), (14, 11, 20), (28, 21, 35)), v => Flat(Stat.Reflect, v));
            Add("physical-taken-as-fire", false, new[] { "of the Cinder Hide" }, new[] { ItemSlot.Body, ItemSlot.Shield }, Ladder((8, 6, 12), (24, 13, 20)), v => TakenAs(DamageType.Physical, DamageType.Fire, v));
            Add("physical-taken-as-cold", false, new[] { "of the Rime Hide" }, new[] { ItemSlot.Body, ItemSlot.Shield }, Ladder((8, 6, 12), (24, 13, 20)), v => TakenAs(DamageType.Physical, DamageType.Cold, v));
            Add("physical-taken-as-lightning", false, new[] { "of the Storm Hide" }, new[] { ItemSlot.Body, ItemSlot.Shield }, Ladder((8, 6, 12), (24, 13, 20)), v => TakenAs(DamageType.Physical, DamageType.Lightning, v));
            Add("ailment-taken", false, new[] { "of Shrugging Off", "of the Stoic" }, new[] { ItemSlot.Body, ItemSlot.Feet }, SmallPercent, v => Inc(Stat.AilmentTaken, -v));
            Add("stun-threshold", false, new[] { "of the Boulder", "of the Anchor" }, new[] { ItemSlot.Body, ItemSlot.Feet }, Percent, v => Inc(Stat.StunThreshold, v));
            Add("ailment-avoid", false, new[] { "of Immunity", "of the Unmarked" }, new[] { ItemSlot.Body, ItemSlot.Feet, ItemSlot.Ring }, Ladder((6, 6, 12), (18, 13, 22), (30, 23, 32)), v => Flat(Stat.AilmentAvoid, v));
            Add("flask-effect", false, new[] { "of the Tippler", "of the Apothecary" }, new[] { ItemSlot.Amulet, ItemSlot.Ring }, SmallPercent, v => Inc(Stat.FlaskEffect, v));

            // Getting about, and doing well out of it.
            Add("move-speed", false, new[] { "of the Road", "of the Wind", "of the Gale", "of the Comet" }, Feet, Ladder((1, 4, 8), (8, 9, 13), (16, 14, 20), (30, 21, 28)), v => Inc(Stat.MoveSpeed, v));
            Add("dodge-cooldown", false, new[] { "of Quick Feet", "of the Dancer" }, Feet, SmallPercent, v => Inc(Stat.DodgeCooldown, -v));
            Add("gold-find", true, new[] { "Gilded", "Golden" }, Jewels, Ladder((1, 10, 25), (10, 26, 50)), v => Inc(Stat.GoldFind, v));
            Add("item-rarity", true, new[] { "Lucky", "Fated" }, Jewels, Ladder((3, 6, 14), (13, 15, 28)), v => Inc(Stat.ItemRarity, v));
            Add("experience", true, new[] { "Studious", "Enlightened" }, Jewels, Ladder((10, 2, 4), (24, 5, 8)), v => Inc(Stat.ExperienceGain, v));
            Add("stealth", true, new[] { "Muffled", "Hushed", "Silent" }, new[] { ItemSlot.Feet, ItemSlot.Head }, Ladder((4, 5, 10), (14, 11, 18), (26, 19, 28)), v => Flat(Stat.Stealth, v));
        }
    }

    /// <summary>
    /// Rolls items. As in the classic ARPGs, an item's level decides which tiers of each affix can appear on it, its rarity how
    /// many it gets, and the value within a tier is a roll of its own: so the same drop is never quite the same twice, and a
    /// deeper world holds better things without any item being placed by hand.
    /// </summary>
    public static class ItemGenerator
    {
        private sealed record BaseItem(string Name, ItemSlot Slot, int MinLevel, float Damage = 0f, float Armour = 0f, float Evasion = 0f, float Barrier = 0f, WeaponKind Kind = WeaponKind.Sword);

        private static readonly BaseItem[] Bases =
        {
            new("Rusted Blade", ItemSlot.Weapon, 1, 7f),
            new("Short Sword", ItemSlot.Weapon, 2, 10f),
            new("Arming Sword", ItemSlot.Weapon, 5, 15f),
            new("Longsword", ItemSlot.Weapon, 9, 22f),
            new("War Blade", ItemSlot.Weapon, 14, 32f),
            new("Runed Blade", ItemSlot.Weapon, 20, 46f),
            new("Knightly Blade", ItemSlot.Weapon, 30, 66f),
            new("Hatchet", ItemSlot.Weapon, 1, 9f, Kind: WeaponKind.Axe),
            new("Bearded Axe", ItemSlot.Weapon, 7, 21f, Kind: WeaponKind.Axe),
            new("Broad Axe", ItemSlot.Weapon, 18, 46f, Kind: WeaponKind.Axe),
            new("Reaver Axe", ItemSlot.Weapon, 30, 74f, Kind: WeaponKind.Axe),
            new("Maul", ItemSlot.Weapon, 2, 17f, Kind: WeaponKind.Mace),
            new("Warhammer", ItemSlot.Weapon, 9, 36f, Kind: WeaponKind.Mace),
            new("Great Maul", ItemSlot.Weapon, 20, 78f, Kind: WeaponKind.Mace),
            new("Titan's Hammer", ItemSlot.Weapon, 32, 122f, Kind: WeaponKind.Mace),
            new("Shiv", ItemSlot.Weapon, 1, 6f, Kind: WeaponKind.Dagger),
            new("Stiletto", ItemSlot.Weapon, 8, 15f, Kind: WeaponKind.Dagger),
            new("Kris", ItemSlot.Weapon, 18, 30f, Kind: WeaponKind.Dagger),
            new("Assassin's Blade", ItemSlot.Weapon, 30, 50f, Kind: WeaponKind.Dagger),
            new("Boar Spear", ItemSlot.Weapon, 2, 13f, Kind: WeaponKind.Spear),
            new("Partisan", ItemSlot.Weapon, 10, 30f, Kind: WeaponKind.Spear),
            new("Glaive", ItemSlot.Weapon, 22, 62f, Kind: WeaponKind.Spear),
            new("Short Bow", ItemSlot.Weapon, 1, 9f, Kind: WeaponKind.Bow),
            new("Hunting Bow", ItemSlot.Weapon, 6, 18f, Kind: WeaponKind.Bow),
            new("Yew Longbow", ItemSlot.Weapon, 15, 36f, Kind: WeaponKind.Bow),
            new("Horn Recurve", ItemSlot.Weapon, 28, 64f, Kind: WeaponKind.Bow),
            new("Walking Staff", ItemSlot.Weapon, 1, 8f, Kind: WeaponKind.Staff),
            new("Bound Staff", ItemSlot.Weapon, 9, 19f, Kind: WeaponKind.Staff),
            new("Runed Staff", ItemSlot.Weapon, 20, 40f, Kind: WeaponKind.Staff),
            new("Twig", ItemSlot.Weapon, 1, 5f, Kind: WeaponKind.Wand),
            new("Bone Wand", ItemSlot.Weapon, 8, 13f, Kind: WeaponKind.Wand),
            new("Sceptre", ItemSlot.Weapon, 20, 28f, Kind: WeaponKind.Wand),

            new("Buckler", ItemSlot.Shield, 1, Evasion: 6f),
            new("Round Shield", ItemSlot.Shield, 4, Armour: 11f),
            new("Heater Shield", ItemSlot.Shield, 10, Armour: 20f),
            new("Tower Shield", ItemSlot.Shield, 17, Armour: 34f),
            new("Wicker Ward", ItemSlot.Shield, 3, Barrier: 7f),
            new("Runed Ward", ItemSlot.Shield, 15, Barrier: 22f),
            new("Targe", ItemSlot.Shield, 12, Armour: 9f, Evasion: 12f),

            new("Hood", ItemSlot.Head, 1, Evasion: 3f),
            new("Leather Cap", ItemSlot.Head, 3, Evasion: 7f),
            new("Iron Helm", ItemSlot.Head, 8, Armour: 13f),
            new("Great Helm", ItemSlot.Head, 15, Armour: 23f),
            new("Circlet", ItemSlot.Head, 4, Barrier: 5f),
            new("Sage's Coif", ItemSlot.Head, 14, Barrier: 14f),

            new("Tunic", ItemSlot.Body, 1, Armour: 6f),
            new("Gambeson", ItemSlot.Body, 3, Armour: 11f),
            new("Lamellar Coat", ItemSlot.Body, 8, Armour: 22f),
            new("Plate Harness", ItemSlot.Body, 15, Armour: 38f),
            new("Leather Jerkin", ItemSlot.Body, 3, Evasion: 12f),
            new("Hunter's Coat", ItemSlot.Body, 12, Evasion: 30f),
            new("Linen Robe", ItemSlot.Body, 2, Barrier: 8f),
            new("Silk Vestments", ItemSlot.Body, 12, Barrier: 24f),
            new("Brigandine", ItemSlot.Body, 20, Armour: 26f, Evasion: 26f),

            new("Sandals", ItemSlot.Feet, 1, Evasion: 2f),
            new("Leather Boots", ItemSlot.Feet, 4, Evasion: 7f),
            new("Iron Greaves", ItemSlot.Feet, 11, Armour: 13f),
            new("Silk Slippers", ItemSlot.Feet, 8, Barrier: 7f),

            new("Ring", ItemSlot.Ring, 1),
            new("Amulet", ItemSlot.Amulet, 1),
        };

        /// <param name="rarityBonus">Added chance of better rarity, as a share: 0.5 is half as likely again.</param>
        public static Item Roll(int level, WorldRandom random, float rarityBonus = 0f, Rarity? least = null, ItemSlot? slot = null)
        {
            Rarity rarity = RollRarity(random, rarityBonus);
            if (least.HasValue && rarity < least.Value) rarity = least.Value;
            return Compose(level, random, rarity, slot, 0f, false);
        }

        /// <summary>
        /// An item of exactly this grade, for what's already been decided by a loot table. A legendary with no unique to be at this level comes out as the next best thing.
        /// </summary>
        /// <param name="quality">From 0 to 1, how well it comes out: from a fair roll of everything, to every line at the top of what its level can reach, on the newest make, with as many lines as it can have.</param>
        public static Item Forge(int level, WorldRandom random, LootGrade grade, float quality, ItemSlot? slot = null)
        {
            Rarity rarity = grade switch
            {
                LootGrade.Legendary => Rarity.Legendary,
                LootGrade.Masterwork or LootGrade.Rare => Rarity.Rare,
                LootGrade.Magic => Rarity.Magic,
                _ => Rarity.Common,
            };
            return Compose(level, random, rarity, slot, quality, grade >= LootGrade.Masterwork);
        }

        private static Item Compose(int level, WorldRandom random, Rarity rarity, ItemSlot? slot, float quality, bool masterwork)
        {
            ItemSlot chosen = slot ?? (ItemSlot)random.Int(Enum.GetValues<ItemSlot>().Length);
            if (rarity == Rarity.Legendary && UniqueCatalog.Pick(level, slot, random) is UniqueDefinition unique) return UniqueCatalog.Make(unique, level, random);
            if (rarity == Rarity.Legendary) rarity = Rarity.Rare;

            BaseItem baseItem = PickBase(chosen, level, random, quality);
            Item item = Make(baseItem, level, random, quality);
            item.Rarity = rarity;

            int affixes = rarity switch
            {
                Rarity.Common => 0,
                Rarity.Magic => Climb(random.Int(1, 2), 2, quality, random),
                _ => masterwork ? Loot.MasterworkLines : Climb(random.Int(3, 5), 5, quality, random),
            };

            var names = new List<(bool prefix, string text)>();
            var taken = new HashSet<string>();
            for (int i = 0; i < affixes; i++)
            {
                Affix? affix = PickAffix(chosen, level, taken, random);
                if (affix == null) break;
                taken.Add(affix.Id);

                int tier = BestTier(affix, level, random, quality);
                Tier rolled = affix.Tiers[tier];
                item.Lines.Add(new ItemLine(affix.Id, Rise(rolled.Low, rolled.High, quality, random)));
                names.Add((affix.Prefix, affix.Names[Math.Min(tier, affix.Names.Length - 1)]));
            }

            item.Name = Name(baseItem.Name, names);
            return item;
        }

        // A roll that good quality leans towards the top of. At none it's the plain roll.
        private static float Rise(float low, float high, float quality, WorldRandom random)
        {
            float share = 1f - MathF.Pow(1f - random.Float(), 1f + quality * 2.5f);
            return low + (high - low) * share;
        }

        // One more line, as often as quality says, up to what the item can have.
        private static int Climb(int lines, int most, float quality, WorldRandom random)
        {
            while (lines < most && quality > 0f && random.Chance(quality)) lines++;
            return lines;
        }

        private static Item Make(BaseItem baseItem, int level, WorldRandom random, float quality = 0f)
        {
            float roll = Rise(0.92f, 1.08f, quality, random);
            return new Item
            {
                Name = baseItem.Name,
                Slot = baseItem.Slot,
                Kind = baseItem.Kind,
                Level = level,
                WeaponDamage = baseItem.Damage * (1f + level * 0.03f) * roll,
                Armour = baseItem.Armour * (1f + level * 0.05f) * roll,
                Evasion = baseItem.Evasion * (1f + level * 0.05f) * roll,
                Barrier = baseItem.Barrier * (1f + level * 0.05f) * roll,
            };
        }

        /// <summary>The plainest thing of a kind that can be had at a level, for what the catalogue of uniques is built on.</summary>
        public static Item Plain(string baseName, int level, WorldRandom random)
        {
            foreach (BaseItem baseItem in Bases)
            {
                if (baseItem.Name == baseName) return Make(baseItem, level, random);
            }
            throw new ArgumentException($"No item called {baseName}.");
        }

        public static bool HasBase(string name) => Bases.Any(b => b.Name == name);

        private static Rarity RollRarity(WorldRandom random, float bonus)
        {
            float roll = random.Float() / (1f + bonus);
            if (roll < 0.012f) return Rarity.Legendary;
            if (roll < 0.11f) return Rarity.Rare;
            if (roll < 0.42f) return Rarity.Magic;
            return Rarity.Common;
        }

        private static BaseItem PickBase(ItemSlot slot, int level, WorldRandom random, float quality)
        {
            BaseItem? best = null;
            var candidates = new List<BaseItem>();
            foreach (BaseItem item in Bases)
            {
                if (item.Slot != slot) continue;
                if (item.MinLevel <= level) candidates.Add(item);
                best ??= item;
            }
            if (candidates.Count == 0) return best!;

            // Weapons and armour come in kinds, and nobody should be stuck with one. Newer makes are likelier, within the kind that comes up.
            BaseItem first = random.Pick(candidates);
            var kin = candidates.Where(c => c.Kind == first.Kind && (c.Armour > 0f) == (first.Armour > 0f) && (c.Evasion > 0f) == (first.Evasion > 0f) && (c.Barrier > 0f) == (first.Barrier > 0f)).ToList();
            return random.Chance(0.7f + 0.3f * quality) ? kin[^1] : first;
        }

        private static Affix? PickAffix(ItemSlot slot, int level, HashSet<string> taken, WorldRandom random)
        {
            var options = new List<Affix>();
            foreach (Affix affix in AffixCatalog.All)
            {
                if (taken.Contains(affix.Id) || !affix.Slots.Contains(slot)) continue;
                if (affix.Tiers[0].MinLevel <= level) options.Add(affix);
            }
            return options.Count == 0 ? null : random.Pick(options);
        }

        // The top tier a level reaches is the rarest roll; lower tiers stay possible.
        private static int BestTier(Affix affix, int level, WorldRandom random, float quality)
        {
            int top = 0;
            for (int i = 0; i < affix.Tiers.Length; i++)
            {
                if (affix.Tiers[i].MinLevel <= level) top = i;
            }
            int tier = top;
            while (tier > 0 && random.Chance(0.4f * (1f - quality))) tier--;
            return tier;
        }

        private static string Name(string baseName, List<(bool prefix, string text)> names)
        {
            string prefix = "";
            string suffix = "";
            foreach ((bool isPrefix, string text) in names)
            {
                if (isPrefix && prefix.Length == 0) prefix = text + " ";
                if (!isPrefix && suffix.Length == 0) suffix = " " + text;
            }
            return prefix + baseName + suffix;
        }
    }
}

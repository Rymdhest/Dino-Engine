using Dino_Defenders.Books;
using Dino_Defenders.Stats;
using Dino_Defenders.Tooltips;
using Dino_Engine.World;
using System.Text.Json.Serialization;
using Dino_Engine.World.Content;
using static Dino_Defenders.Stats.M;

namespace Dino_Defenders.Progression
{
    /// <summary>What an item is worn as, which is what bases and affixes talk about. A ring is a ring on either hand: where one has ended up is a <see cref="WornSlot"/>.</summary>
    public enum ItemSlot
    {
        MainHand,
        OffHand,
        Head,
        Neck,
        Body,
        Legs,
        Feet,
        Hands,
        Finger,
        Book,       // Worn nowhere: it goes on the bar. See BOOKS.md.
        Page,       // Goes in a book.
        Sticker,    // Goes on a page.
    }

    /// <summary>A place on a hero. These line up with <see cref="ItemSlot"/> as far as the first finger, which <see cref="WornSlots"/> relies on.</summary>
    public enum WornSlot
    {
        MainHand,
        OffHand,
        Head,
        Neck,
        Body,
        Legs,
        Feet,
        Hands,
        Finger1,
        Finger2,
    }

    public static class WornSlots
    {
        private static readonly WornSlot[] Fingers = { WornSlot.Finger1, WornSlot.Finger2 };

        /// <summary>What goes in a place.</summary>
        public static ItemSlot Holds(WornSlot place) => place == WornSlot.Finger2 ? ItemSlot.Finger : (ItemSlot)place;

        /// <summary>The first place something worn as this can go, which is the only one for anything but a ring.</summary>
        public static WornSlot First(ItemSlot slot) => (WornSlot)slot;

        /// <summary>Every place something worn as this can go, in the order they'd be filled.</summary>
        public static IReadOnlyList<WornSlot> For(ItemSlot slot) => slot == ItemSlot.Finger ? Fingers : slot >= ItemSlot.Book ? Array.Empty<WornSlot>() : new[] { First(slot) };
    }

    /// <summary>The three ways of dressing for a fight. What something looks like says which it is, and which it is says what it's good for.</summary>
    public enum GearFamily
    {
        Heavy,      // Metal, and armour.
        Leather,    // Hide, and evasion.
        Cloth,      // Robes, barrier and mana.
    }

    /// <summary>How much of each family something is, adding up to 1, or to nothing for what's no family at all. Halfway between two is a real place to be, and looks it.</summary>
    public readonly record struct FamilyShares(float Heavy, float Leather, float Cloth)
    {
        public static FamilyShares All(GearFamily family) => family switch
        {
            GearFamily.Heavy => new FamilyShares(1f, 0f, 0f),
            GearFamily.Leather => new FamilyShares(0f, 1f, 0f),
            _ => new FamilyShares(0f, 0f, 1f),
        };

        public float Of(GearFamily family) => family switch
        {
            GearFamily.Heavy => Heavy,
            GearFamily.Leather => Leather,
            _ => Cloth,
        };
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
    public readonly record struct WeaponTraits(float AttackTime, float CritChance, bool TwoHanded, Tags Tag, GearFamily Family, Mod[] Mods);

    public static class WeaponKinds
    {
        private static readonly WeaponTraits[] Table =
        {
            new(0.6f, 5f, false, Tags.Sword, GearFamily.Heavy, new[] { Flat(Stat.Accuracy, 20f) }),
            new(0.68f, 5f, false, Tags.Axe, GearFamily.Heavy, new[] { Inc(Stat.Damage, 8f, Tags.Physical) }),
            new(0.82f, 5f, true, Tags.Mace, GearFamily.Heavy, new[] { Chance(Tags.Stun, 10f) }),
            new(0.48f, 8f, false, Tags.Dagger, GearFamily.Leather, new[] { Flat(Stat.CritMultiplier, 20f) }),
            new(0.62f, 6f, true, Tags.Spear, GearFamily.Leather, new[] { Flat(Stat.MeleeReach, 1.5f) }),
            new(0.7f, 6f, true, Tags.Bow, GearFamily.Leather, new[] { Inc(Stat.ProjectileSpeed, 15f) }),
            new(0.75f, 7f, true, Tags.Staff, GearFamily.Cloth, new[] { Inc(Stat.Damage, 18f, Tags.Spell) }),
            new(0.56f, 7f, false, Tags.Wand, GearFamily.Cloth, new[] { Inc(Stat.Damage, 10f, Tags.Spell) }),
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

        /// <summary>What cloth holds for each point of barrier it gives. Worked out, not stored, so a robe found before this was a rule has it too.</summary>
        public const float ManaPerBarrier = 0.6f;

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

        /// <summary>What it looks like, as a seed. Nothing else about its look is kept: see GEAR.md. None means it's older than this, and <see cref="LookSeed"/> finds it one.</summary>
        public ulong Seed { get; set; }

        /// <summary>Which base it was made from. Empty on anything older than this, which <see cref="ItemGenerator.BaseOf"/> makes up for.</summary>
        public string Base { get; set; } = "";

        /// <summary>What's written on it, if it's a page.</summary>
        public Page? Page { get; set; }

        /// <summary>What it says, if it's a sticker.</summary>
        public Sticker? Sticker { get; set; }

        /// <summary>What's in it, a place each, with gaps: a book holds pages and a page holds stickers. How many places there are is what makes one better than another. See BOOKS.md.</summary>
        public Item?[]? Held { get; set; }

        /// <summary>What was written in a book before pages were items, which only a save that old has.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Book? Book { get; set; }

        public bool IsBook => Slot == ItemSlot.Book;
        public bool IsPage => Slot == ItemSlot.Page;
        public bool IsSticker => Slot == ItemSlot.Sticker;

        /// <summary>Whether it's something that goes in a book, or a book, rather than something worn.</summary>
        public bool IsFolio => Slot >= ItemSlot.Book;

        /// <summary>How many places it has for something else.</summary>
        public int Places => Held?.Length ?? 0;

        /// <summary>What it reads as, or nothing if there's nothing in it. It's a method so that it isn't saved.</summary>
        public Book? Written() => Scribe.Bind(this);

        // What's in something is sold with it.
        public int SellValue => (int)(4f * Level * (1f + (int)Rarity * 1.6f + (Lines.Count + Places) * 0.5f)) + (Held?.Sum(held => held?.SellValue ?? 0) ?? 0);
        public int BuyValue => SellValue * 4;

        public bool IsTwoHanded => Slot == ItemSlot.MainHand && WeaponKinds.Of(Kind).TwoHanded;

        /// <summary>
        /// What its look grows from. A unique is one thing wherever it turns up, so a boss's sword is the sword you pick up. Anything from before seeds were kept gets one from what
        /// it is, which can't change, so an old sword has a look of its own and keeps it. It's a method so that it isn't saved.
        /// </summary>
        public ulong LookSeed()
        {
            if (Unique != null) return Hash(Unique, 0xCBF29CE484222325UL);
            if (Seed != 0) return Seed;

            ulong hash = Hash(Name, 0xCBF29CE484222325UL);
            hash = Mix(hash, (uint)Level);
            hash = Mix(hash, BitConverter.SingleToUInt32Bits(WeaponDamage));
            hash = Mix(hash, BitConverter.SingleToUInt32Bits(Armour));
            hash = Mix(hash, BitConverter.SingleToUInt32Bits(Evasion));
            hash = Mix(hash, BitConverter.SingleToUInt32Bits(Barrier));
            return hash == 0 ? 1 : hash;
        }

        // FNV-1a, as string.GetHashCode changes from run to run.
        private static ulong Hash(string text, ulong hash)
        {
            foreach (char c in text) hash = Mix(hash, c);
            return hash;
        }

        private static ulong Mix(ulong hash, uint value) => (hash ^ value) * 0x100000001B3UL;

        /// <summary>
        /// How much of each family this is, from what its base gives: armour is heavy, evasion is leather, barrier is cloth, and something giving two is that much of each. Weapons
        /// go by their kind, and what gives none of these, such as a ring, is no family. Never stored, so it can't disagree with the item.
        /// </summary>
        public FamilyShares Shares()
        {
            if (Slot == ItemSlot.MainHand) return FamilyShares.All(WeaponKinds.Of(Kind).Family);
            float total = Armour + Evasion + Barrier;
            return total > 0f ? new FamilyShares(Armour / total, Evasion / total, Barrier / total) : default;
        }

        /// <summary>What this adds to whoever is wearing it. A line whose affix has gone from the game simply does nothing.</summary>
        public IEnumerable<Mod> Mods()
        {
            if (Armour > 0f) yield return Flat(Stat.Armour, Armour);
            if (Evasion > 0f) yield return Flat(Stat.Evasion, Evasion);
            if (Barrier > 0f)
            {
                yield return Flat(Stat.Barrier, Barrier);
                yield return Flat(Stat.Mana, Barrier * ManaPerBarrier);
            }
            if (Slot == ItemSlot.MainHand)
            {
                foreach (Mod mod in WeaponKinds.Of(Kind).Mods) yield return mod;
            }

            foreach (Mod mod in RolledMods()) yield return mod;
        }

        /// <summary>This item in a hand, as far as an attack goes.</summary>
        public WeaponProfile Weapon()
        {
            WeaponTraits traits = WeaponKinds.Of(Kind);
            Tags tags = traits.Tag | (traits.TwoHanded ? Tags.TwoHanded : Tags.OneHanded);
            return new WeaponProfile(WeaponDamage * (1f - WeaponSpread), WeaponDamage * (1f + WeaponSpread), traits.AttackTime, traits.CritChance, tags);
        }

        /// <summary>What it is before anything has been rolled on it, as names and numbers.</summary>
        public IEnumerable<(string name, string value)> BaseRows()
        {
            if (WeaponDamage > 0f)
            {
                WeaponProfile weapon = Weapon();
                yield return ("Weapon Damage", Say.Range(weapon.Min, weapon.Max));
                yield return ("Attacks per Second", Say.N(1f / weapon.AttackTime));
                yield return ("Critical Chance", Say.Percent(weapon.CritChance));
            }
            if (Armour > 0f) yield return ("Armour", Say.Whole(Armour));
            if (Evasion > 0f) yield return ("Evasion", Say.Whole(Evasion));
            if (Barrier > 0f)
            {
                yield return ("Barrier", Say.Whole(Barrier));
                yield return ("Mana", Say.Whole(Barrier * ManaPerBarrier));
            }
        }

        /// <summary>Its lines, in the order they should be read: everything on it that was rolled, or that's its own for being what it is.</summary>
        public IEnumerable<Mod> RolledMods()
        {
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
        private static readonly ItemSlot[] Weapon = { ItemSlot.MainHand };
        private static readonly ItemSlot[] WeaponJewels = { ItemSlot.MainHand, ItemSlot.Finger, ItemSlot.Neck };
        private static readonly ItemSlot[] Jewels = { ItemSlot.Finger, ItemSlot.Neck };
        private static readonly ItemSlot[] Grips = { ItemSlot.MainHand, ItemSlot.Hands, ItemSlot.Finger, ItemSlot.Neck };
        private static readonly ItemSlot[] Armours = { ItemSlot.OffHand, ItemSlot.Head, ItemSlot.Body, ItemSlot.Legs, ItemSlot.Feet, ItemSlot.Hands };
        private static readonly ItemSlot[] Upper = { ItemSlot.OffHand, ItemSlot.Head, ItemSlot.Body };
        private static readonly ItemSlot[] Clothes = { ItemSlot.Head, ItemSlot.Body, ItemSlot.Legs, ItemSlot.Finger, ItemSlot.Neck };
        private static readonly ItemSlot[] Worn = { ItemSlot.OffHand, ItemSlot.Head, ItemSlot.Body, ItemSlot.Legs, ItemSlot.Feet, ItemSlot.Hands, ItemSlot.Finger, ItemSlot.Neck };
        private static readonly ItemSlot[] Feet = { ItemSlot.Feet };
        private static readonly ItemSlot[] Shields = { ItemSlot.OffHand };

        /// <summary>How much the lines of something wholly of one family go to what suits it, as a share of all there is to draw from. What an item looks like should say what it's for, and at 1 that's over half of its lines, but anything can still roll anywhere.</summary>
        private const float Leaning = 1f;

        private static readonly Dictionary<string, Affix> ById = new();
        private static readonly List<Affix> Everything = new();
        private static readonly Dictionary<string, GearFamily> Suits = new();

        public static IReadOnlyList<Affix> All => Everything;

        /// <summary>Every affix that suits a family, and which.</summary>
        public static IReadOnlyDictionary<string, GearFamily> Suited => Suits;

        public static Affix? Find(string id) => ById.GetValueOrDefault(id);

        /// <summary>
        /// How likely each of these is on something of these shares. A family's share is of the whole draw, not of each affix: more affixes suit heavy armour on a body than suit
        /// leather, and a weight on each would only reward whichever family has the most names.
        /// </summary>
        public static void Weigh(IReadOnlyList<Affix> options, in FamilyShares shares, List<float> weights)
        {
            Span<int> suited = stackalloc int[3];
            foreach (Affix affix in options)
            {
                if (Suits.TryGetValue(affix.Id, out GearFamily family)) suited[(int)family]++;
            }

            weights.Clear();
            foreach (Affix affix in options)
            {
                bool suits = Suits.TryGetValue(affix.Id, out GearFamily family);
                weights.Add(suits ? 1f + shares.Of(family) * Leaning * options.Count / suited[(int)family] : 1f);
            }
        }

        private static void Suit(GearFamily family, params string[] ids)
        {
            foreach (string id in ids) Suits.Add(id, family);
        }

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

            Add("spell-damage", true, new[] { "Apprentice's", "Adept's", "Scholar's", "Magus'", "Archmage's" }, new[] { ItemSlot.MainHand, ItemSlot.Neck }, Percent, v => Inc(Stat.Damage, v, Tags.Spell));
            Add("attack-damage", true, new[] { "Soldier's", "Veteran's", "Champion's", "Warmaster's", "Conqueror's" }, new[] { ItemSlot.MainHand, ItemSlot.Neck }, Percent, v => Inc(Stat.Damage, v, Tags.Attack));
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
            Add("minion-damage", true, new[] { "Shepherd's", "Master's", "Tyrant's", "Lich's", "Necrarch's" }, new[] { ItemSlot.MainHand, ItemSlot.Finger, ItemSlot.Neck, ItemSlot.Head }, Percent, v => Inc(Stat.Damage, v, Tags.Minion));

            Add("attack-speed", false, new[] { "of Haste", "of Fury", "of the Storm", "of the Whirlwind" }, new[] { ItemSlot.MainHand, ItemSlot.Hands, ItemSlot.Finger }, Ladder((3, 4, 8), (10, 9, 14), (18, 15, 22), (30, 23, 30)), v => Inc(Stat.AttackSpeed, v));
            Add("cast-speed", false, new[] { "of Muttering", "of Fluency", "of Recitation", "of the Unbroken Word" }, new[] { ItemSlot.MainHand, ItemSlot.Hands, ItemSlot.Finger, ItemSlot.Neck }, Ladder((3, 4, 8), (10, 9, 14), (18, 15, 22), (30, 23, 30)), v => Inc(Stat.CastSpeed, v));
            Add("crit-chance", false, new[] { "of Precision", "of the Hawk", "of the Unerring", "of the Killing Eye" }, Grips, Ladder((2, 1, 3), (9, 3.5f, 6), (17, 6.5f, 10), (30, 10.5f, 14)), v => Flat(Stat.CritChance, v));
            Add("crit-increased", false, new[] { "of Focus", "of Intent", "of Certainty", "of Fate" }, WeaponJewels, Percent, v => Inc(Stat.CritChance, v));
            Add("crit-multiplier", false, new[] { "of Wounding", "of Slaughter", "of Ruin", "of Annihilation" }, new[] { ItemSlot.MainHand, ItemSlot.Neck }, Ladder((4, 10, 20), (11, 21, 35), (19, 36, 60), (32, 61, 85)), v => Flat(Stat.CritMultiplier, v));
            Add("accuracy", false, new[] { "of the Marksman", "of the Sharpshooter", "of the Sniper" }, Grips, Pool, v => Flat(Stat.Accuracy, v));
            Add("penetration", false, new[] { "of Piercing", "of Unmaking", "of Undoing" }, new[] { ItemSlot.MainHand, ItemSlot.Neck }, Ladder((8, 4, 7), (18, 8, 12), (30, 13, 18)), v => Flat(Stat.Penetration, v));

            Add("area", false, new[] { "of Reach", "of Breadth", "of the Expanse", "of Horizons" }, new[] { ItemSlot.MainHand, ItemSlot.Neck, ItemSlot.Head }, SmallPercent, v => Inc(Stat.AreaOfEffect, v));
            Add("duration", false, new[] { "of Patience", "of Endurance", "of Ages" }, new[] { ItemSlot.Neck, ItemSlot.Head, ItemSlot.Finger }, SmallPercent, v => Inc(Stat.PageDuration, v));
            Add("cooldown", false, new[] { "of Readiness", "of Renewal", "of the Eternal Hour" }, new[] { ItemSlot.Neck, ItemSlot.Head, ItemSlot.Finger }, Ladder((5, 4, 7), (15, 8, 12), (28, 13, 18)), v => Inc(Stat.CooldownRecovery, v));
            Add("cost", false, new[] { "of Thrift", "of Economy", "of the Miser" }, new[] { ItemSlot.Neck, ItemSlot.Head, ItemSlot.Finger }, Ladder((3, 4, 7), (12, 8, 12), (24, 13, 18)), v => Inc(Stat.BookCost, -v));
            Add("projectile-speed", false, new[] { "of Flight", "of the Arrow", "of the Falcon" }, new[] { ItemSlot.MainHand, ItemSlot.Finger }, SmallPercent, v => Inc(Stat.ProjectileSpeed, v));
            Add("potency", false, new[] { "of Ink", "of Eloquence" }, new[] { ItemSlot.MainHand, ItemSlot.Neck }, Ladder((10, 5, 8), (26, 9, 15)), v => Inc(Stat.Potency, v));

            Add("burn-chance", false, new[] { "of Kindling", "of Immolation", "of the Pyre" }, WeaponJewels, Chances, v => Chance(Tags.Burn, v));
            Add("bleed-chance", false, new[] { "of Cuts", "of Lacerating", "of Exsanguination" }, WeaponJewels, Chances, v => Chance(Tags.Bleed, v));
            Add("venom-chance", false, new[] { "of the Asp", "of the Viper", "of the Cobra" }, WeaponJewels, Chances, v => Chance(Tags.Venom, v));
            Add("shock-chance", false, new[] { "of Jolting", "of Shocking", "of Paralysis" }, WeaponJewels, Chances, v => Chance(Tags.Shock, v));
            Add("freeze-chance", false, new[] { "of Numbing", "of Stillness", "of the Glacier" }, WeaponJewels, Chances, v => Chance(Tags.Freeze, v));
            Add("hex-chance", false, new[] { "of Ill Omen", "of Hexing", "of the Curse" }, WeaponJewels, Chances, v => Chance(Tags.Hex, v));
            Add("stun-chance", false, new[] { "of Staggering", "of Stunning", "of the Hammer" }, Weapon, Chances, v => Chance(Tags.Stun, v));
            Add("ailment-duration", false, new[] { "of Suffering", "of Torment", "of Agony" }, Jewels, SmallPercent, v => Inc(Stat.AilmentDuration, v));
            Add("reaction-damage", false, new[] { "of Catalysis", "of Reaction", "of the Crucible" }, new[] { ItemSlot.MainHand, ItemSlot.Neck, ItemSlot.Finger }, Percent, v => Inc(Stat.ReactionDamage, v));

            Add("health-leech", false, new[] { "of the Leech", "of the Lamprey", "of the Vampire" }, new[] { ItemSlot.MainHand, ItemSlot.Finger }, Ladder((6, 0.3f, 0.6f), (16, 0.7f, 1.1f), (28, 1.2f, 1.8f)), v => Flat(Stat.HealthLeech, v));
            Add("mana-leech", false, new[] { "of Thirst", "of Draining", "of the Mind Eater" }, new[] { ItemSlot.MainHand, ItemSlot.Finger }, Ladder((6, 0.2f, 0.4f), (16, 0.5f, 0.8f), (28, 0.9f, 1.3f)), v => Flat(Stat.ManaLeech, v));
            Add("health-on-hit", false, new[] { "of Mending", "of Knitting" }, new[] { ItemSlot.MainHand, ItemSlot.Hands, ItemSlot.Finger }, Ladder((4, 1, 2.5f), (13, 3, 6), (26, 7, 12)), v => Flat(Stat.HealthOnHit, v));
            Add("mana-on-hit", false, new[] { "of Siphoning", "of Tapping" }, new[] { ItemSlot.MainHand, ItemSlot.Hands, ItemSlot.Finger }, Ladder((4, 0.5f, 1.5f), (13, 2, 3.5f), (26, 4, 7)), v => Flat(Stat.ManaOnHit, v));
            Add("barrier-on-kill", false, new[] { "of the Husk", "of the Carapace" }, new[] { ItemSlot.MainHand, ItemSlot.Finger, ItemSlot.Neck }, Pool, v => Flat(Stat.BarrierOnKill, v * 0.4f));
            Add("health-on-kill", false, new[] { "of Harvest", "of Reaping" }, new[] { ItemSlot.MainHand, ItemSlot.Finger, ItemSlot.Neck }, Pool, v => Flat(Stat.HealthOnKill, v * 0.5f));
            Add("mana-on-kill", false, new[] { "of Gleaning", "of Absorption" }, new[] { ItemSlot.MainHand, ItemSlot.Finger, ItemSlot.Neck }, Pool, v => Flat(Stat.ManaOnKill, v * 0.3f));

            Add("strength", false, new[] { "of the Brute", "of Might", "of Iron", "of the Giant", "of Titans", "of the Mountain King" }, Worn, Rating, v => Flat(Stat.Strength, v));
            Add("dexterity", false, new[] { "of the Fox", "of Grace", "of the Dancer", "of the Cat", "of Quicksilver", "of the Wind" }, Worn, Rating, v => Flat(Stat.Dexterity, v));
            Add("intellect", false, new[] { "of the Student", "of Wit", "of the Scholar", "of the Sage's Eye", "of Genius", "of the Oracle" }, Worn, Rating, v => Flat(Stat.Intellect, v));

            // Staying alive.
            Add("health", false, new[] { "of the Ox", "of the Bear", "of the Oak", "of the Mountain", "of the Titan", "of the Colossus" }, new[] { ItemSlot.Head, ItemSlot.Body, ItemSlot.Legs, ItemSlot.OffHand, ItemSlot.Finger, ItemSlot.Neck }, Pool, v => Flat(Stat.Health, v));
            Add("health-percent", true, new[] { "Hale", "Robust", "Stalwart", "Hearty" }, Clothes, Ladder((8, 3, 5), (18, 6, 9), (30, 10, 14)), v => Inc(Stat.Health, v));
            Add("mana", false, new[] { "of the Mind", "of the Sage", "of Insight", "of the Well", "of the Deep", "of the Abyss" }, new[] { ItemSlot.Head, ItemSlot.Legs, ItemSlot.Hands, ItemSlot.Finger, ItemSlot.Neck, ItemSlot.MainHand }, Pool, v => Flat(Stat.Mana, v * 0.7f));
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
            Add("physical-reduction", false, new[] { "of Padding", "of Bulwarks" }, new[] { ItemSlot.Body, ItemSlot.Legs, ItemSlot.OffHand }, Ladder((10, 2, 3), (24, 4, 6)), v => Flat(Stat.PhysicalReduction, v));
            Add("taken-from-mana", false, new[] { "of the Mindful", "of Mental Fortitude" }, new[] { ItemSlot.Head, ItemSlot.Neck }, Ladder((10, 3, 6), (24, 7, 11)), v => Flat(Stat.TakenFromMana, v));
            Add("health-regen", false, new[] { "of Renewal", "of Vigour", "of the Spring", "of the Hydra" }, new[] { ItemSlot.Body, ItemSlot.Legs, ItemSlot.Head, ItemSlot.Finger, ItemSlot.Neck }, Ladder((2, 0.5f, 1.2f), (9, 1.3f, 2.5f), (17, 2.6f, 4.5f), (30, 4.6f, 9f)), v => Flat(Stat.HealthRegen, v));
            Add("health-regen-percent", false, new[] { "of Convalescence", "of Regrowth" }, new[] { ItemSlot.Body, ItemSlot.Neck }, Ladder((12, 0.3f, 0.6f), (26, 0.7f, 1.2f)), v => Flat(Stat.HealthRegenPercent, v));
            Add("mana-regen", false, new[] { "of Clarity", "of Meditation", "of Serenity", "of the Sea" }, new[] { ItemSlot.Head, ItemSlot.Hands, ItemSlot.Finger, ItemSlot.Neck, ItemSlot.MainHand }, Ladder((2, 0.4f, 1f), (9, 1.1f, 2f), (17, 2.1f, 3.5f), (30, 3.6f, 7f)), v => Flat(Stat.ManaRegen, v));
            Add("barrier-recharge", false, new[] { "of Restoration", "of the Rampart" }, new[] { ItemSlot.Body, ItemSlot.Neck }, SmallPercent, v => Inc(Stat.BarrierRecharge, v));
            Add("recovery", false, new[] { "of the Physician", "of the Healer", "of the Saint" }, new[] { ItemSlot.Neck, ItemSlot.Body }, SmallPercent, v => Inc(Stat.Recovery, v));
            Add("reflect", false, new[] { "of Thorns", "of Spines", "of the Briar" }, new[] { ItemSlot.Body, ItemSlot.OffHand }, Ladder((3, 5, 10), (14, 11, 20), (28, 21, 35)), v => Flat(Stat.Reflect, v));
            Add("physical-taken-as-fire", false, new[] { "of the Cinder Hide" }, new[] { ItemSlot.Body, ItemSlot.OffHand }, Ladder((8, 6, 12), (24, 13, 20)), v => TakenAs(DamageType.Physical, DamageType.Fire, v));
            Add("physical-taken-as-cold", false, new[] { "of the Rime Hide" }, new[] { ItemSlot.Body, ItemSlot.OffHand }, Ladder((8, 6, 12), (24, 13, 20)), v => TakenAs(DamageType.Physical, DamageType.Cold, v));
            Add("physical-taken-as-lightning", false, new[] { "of the Storm Hide" }, new[] { ItemSlot.Body, ItemSlot.OffHand }, Ladder((8, 6, 12), (24, 13, 20)), v => TakenAs(DamageType.Physical, DamageType.Lightning, v));
            Add("ailment-taken", false, new[] { "of Shrugging Off", "of the Stoic" }, new[] { ItemSlot.Body, ItemSlot.Legs, ItemSlot.Feet }, SmallPercent, v => Inc(Stat.AilmentTaken, -v));
            Add("stun-threshold", false, new[] { "of the Boulder", "of the Anchor" }, new[] { ItemSlot.Body, ItemSlot.Legs, ItemSlot.Feet }, Percent, v => Inc(Stat.StunThreshold, v));
            Add("ailment-avoid", false, new[] { "of Immunity", "of the Unmarked" }, new[] { ItemSlot.Body, ItemSlot.Legs, ItemSlot.Feet, ItemSlot.Finger }, Ladder((6, 6, 12), (18, 13, 22), (30, 23, 32)), v => Flat(Stat.AilmentAvoid, v));
            Add("flask-effect", false, new[] { "of the Tippler", "of the Apothecary" }, new[] { ItemSlot.Neck, ItemSlot.Finger }, SmallPercent, v => Inc(Stat.FlaskEffect, v));

            // Getting about, and doing well out of it.
            Add("move-speed", false, new[] { "of the Road", "of the Wind", "of the Gale", "of the Comet" }, Feet, Ladder((1, 4, 8), (8, 9, 13), (16, 14, 20), (30, 21, 28)), v => Inc(Stat.MoveSpeed, v));
            Add("dodge-cooldown", false, new[] { "of Quick Feet", "of the Dancer" }, new[] { ItemSlot.Feet, ItemSlot.Legs }, SmallPercent, v => Inc(Stat.DodgeCooldown, -v));
            Add("gold-find", true, new[] { "Gilded", "Golden" }, Jewels, Ladder((1, 10, 25), (10, 26, 50)), v => Inc(Stat.GoldFind, v));
            Add("item-rarity", true, new[] { "Lucky", "Fated" }, Jewels, Ladder((3, 6, 14), (13, 15, 28)), v => Inc(Stat.ItemRarity, v));
            Add("experience", true, new[] { "Studious", "Enlightened" }, Jewels, Ladder((10, 2, 4), (24, 5, 8)), v => Inc(Stat.ExperienceGain, v));
            Add("stealth", true, new[] { "Muffled", "Hushed", "Silent" }, new[] { ItemSlot.Feet, ItemSlot.Legs, ItemSlot.Head }, Ladder((4, 5, 10), (14, 11, 18), (26, 19, 28)), v => Flat(Stat.Stealth, v));

            // What each family is good for. Weapons lean the same way as what they're carried with, so a staff tends to be a caster's.
            Suit(GearFamily.Heavy, "strength", "armour", "armour-percent", "health", "health-percent", "block", "flat-reduction", "physical-reduction", "reflect", "stun-threshold", "health-regen",
                "health-regen-percent", "physical-damage", "attack-damage", "stun-chance");
            Suit(GearFamily.Leather, "dexterity", "evasion", "evasion-percent", "attack-speed", "crit-chance", "crit-increased", "crit-multiplier", "accuracy", "move-speed", "dodge-cooldown", "stealth",
                "ailment-avoid", "projectile-speed", "bleed-chance", "venom-chance");
            Suit(GearFamily.Cloth, "intellect", "mana", "mana-percent", "mana-regen", "barrier", "barrier-percent", "barrier-recharge", "spell-damage", "cast-speed", "cooldown", "cost", "potency",
                "taken-from-mana", "duration", "minion-damage", "mana-on-hit", "mana-leech", "mana-on-kill");
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
            new("Rusted Blade", ItemSlot.MainHand, 1, 7f),
            new("Short Sword", ItemSlot.MainHand, 2, 10f),
            new("Arming Sword", ItemSlot.MainHand, 5, 15f),
            new("Longsword", ItemSlot.MainHand, 9, 22f),
            new("War Blade", ItemSlot.MainHand, 14, 32f),
            new("Runed Blade", ItemSlot.MainHand, 20, 46f),
            new("Knightly Blade", ItemSlot.MainHand, 30, 66f),
            new("Hatchet", ItemSlot.MainHand, 1, 9f, Kind: WeaponKind.Axe),
            new("Bearded Axe", ItemSlot.MainHand, 7, 21f, Kind: WeaponKind.Axe),
            new("Broad Axe", ItemSlot.MainHand, 18, 46f, Kind: WeaponKind.Axe),
            new("Reaver Axe", ItemSlot.MainHand, 30, 74f, Kind: WeaponKind.Axe),
            new("Maul", ItemSlot.MainHand, 2, 17f, Kind: WeaponKind.Mace),
            new("Warhammer", ItemSlot.MainHand, 9, 36f, Kind: WeaponKind.Mace),
            new("Great Maul", ItemSlot.MainHand, 20, 78f, Kind: WeaponKind.Mace),
            new("Titan's Hammer", ItemSlot.MainHand, 32, 122f, Kind: WeaponKind.Mace),
            new("Shiv", ItemSlot.MainHand, 1, 6f, Kind: WeaponKind.Dagger),
            new("Stiletto", ItemSlot.MainHand, 8, 15f, Kind: WeaponKind.Dagger),
            new("Kris", ItemSlot.MainHand, 18, 30f, Kind: WeaponKind.Dagger),
            new("Assassin's Blade", ItemSlot.MainHand, 30, 50f, Kind: WeaponKind.Dagger),
            new("Boar Spear", ItemSlot.MainHand, 2, 13f, Kind: WeaponKind.Spear),
            new("Partisan", ItemSlot.MainHand, 10, 30f, Kind: WeaponKind.Spear),
            new("Glaive", ItemSlot.MainHand, 22, 62f, Kind: WeaponKind.Spear),
            new("Short Bow", ItemSlot.MainHand, 1, 9f, Kind: WeaponKind.Bow),
            new("Hunting Bow", ItemSlot.MainHand, 6, 18f, Kind: WeaponKind.Bow),
            new("Yew Longbow", ItemSlot.MainHand, 15, 36f, Kind: WeaponKind.Bow),
            new("Horn Recurve", ItemSlot.MainHand, 28, 64f, Kind: WeaponKind.Bow),
            new("Walking Staff", ItemSlot.MainHand, 1, 10f, Kind: WeaponKind.Staff),
            new("Ash Staff", ItemSlot.MainHand, 4, 24f, Kind: WeaponKind.Staff),
            new("Bound Staff", ItemSlot.MainHand, 9, 44f, Kind: WeaponKind.Staff),
            new("Runed Staff", ItemSlot.MainHand, 20, 96f, Kind: WeaponKind.Staff),
            new("Twig", ItemSlot.MainHand, 1, 7f, Kind: WeaponKind.Wand),
            new("Carved Wand", ItemSlot.MainHand, 4, 17f, Kind: WeaponKind.Wand),
            new("Bone Wand", ItemSlot.MainHand, 8, 33f, Kind: WeaponKind.Wand),
            new("Sceptre", ItemSlot.MainHand, 20, 72f, Kind: WeaponKind.Wand),

            new("Buckler", ItemSlot.OffHand, 1, Evasion: 6f),
            new("Round Shield", ItemSlot.OffHand, 4, Armour: 11f),
            new("Heater Shield", ItemSlot.OffHand, 10, Armour: 20f),
            new("Tower Shield", ItemSlot.OffHand, 17, Armour: 34f),
            new("Wicker Ward", ItemSlot.OffHand, 3, Barrier: 7f),
            new("Runed Ward", ItemSlot.OffHand, 15, Barrier: 22f),
            new("Targe", ItemSlot.OffHand, 12, Armour: 9f, Evasion: 12f),

            new("Hood", ItemSlot.Head, 1, Evasion: 3f),
            new("Leather Cap", ItemSlot.Head, 3, Evasion: 7f),
            new("Mail Coif", ItemSlot.Head, 2, Armour: 6f),
            new("Iron Helm", ItemSlot.Head, 8, Armour: 13f),
            new("Great Helm", ItemSlot.Head, 15, Armour: 23f),
            new("Linen Cowl", ItemSlot.Head, 1, Barrier: 3f),
            new("Circlet", ItemSlot.Head, 4, Barrier: 5f),
            new("Sage's Coif", ItemSlot.Head, 14, Barrier: 14f),

            new("Leather Tunic", ItemSlot.Body, 1, Evasion: 8f),
            new("Leather Jerkin", ItemSlot.Body, 3, Evasion: 12f),
            new("Hunter's Coat", ItemSlot.Body, 12, Evasion: 30f),
            new("Gambeson", ItemSlot.Body, 3, Armour: 11f),
            new("Lamellar Coat", ItemSlot.Body, 8, Armour: 22f),
            new("Plate Harness", ItemSlot.Body, 15, Armour: 38f),
            new("Linen Robe", ItemSlot.Body, 2, Barrier: 8f),
            new("Silk Vestments", ItemSlot.Body, 12, Barrier: 24f),
            new("Brigandine", ItemSlot.Body, 20, Armour: 26f, Evasion: 26f),

            new("Hide Trousers", ItemSlot.Legs, 1, Evasion: 3f),
            new("Leather Breeches", ItemSlot.Legs, 6, Evasion: 8f),
            new("Ranger's Chaps", ItemSlot.Legs, 14, Evasion: 15f),
            new("Mail Chausses", ItemSlot.Legs, 3, Armour: 5f),
            new("Iron Cuisses", ItemSlot.Legs, 9, Armour: 10f),
            new("Plate Legguards", ItemSlot.Legs, 16, Armour: 18f),
            new("Linen Trousers", ItemSlot.Legs, 1, Barrier: 2f),
            new("Silk Leggings", ItemSlot.Legs, 10, Barrier: 8f),

            new("Sandals", ItemSlot.Feet, 1, Evasion: 2f),
            new("Leather Boots", ItemSlot.Feet, 4, Evasion: 7f),
            new("Mail Sabatons", ItemSlot.Feet, 4, Armour: 6f),
            new("Iron Greaves", ItemSlot.Feet, 11, Armour: 13f),
            new("Linen Wraps", ItemSlot.Feet, 1, Barrier: 2f),
            new("Silk Slippers", ItemSlot.Feet, 8, Barrier: 7f),

            new("Leather Gloves", ItemSlot.Hands, 1, Evasion: 2f),
            new("Archer's Bracers", ItemSlot.Hands, 10, Evasion: 7f),
            new("Mail Mitts", ItemSlot.Hands, 3, Armour: 4f),
            new("Iron Gauntlets", ItemSlot.Hands, 9, Armour: 8f),
            new("Plate Gauntlets", ItemSlot.Hands, 16, Armour: 14f),
            new("Cloth Wraps", ItemSlot.Hands, 1, Barrier: 2f),
            new("Silk Gloves", ItemSlot.Hands, 9, Barrier: 6f),

            new("Ring", ItemSlot.Finger, 1),
            new("Amulet", ItemSlot.Neck, 1),
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
            ItemSlot chosen = slot ?? (ItemSlot)random.Int((int)ItemSlot.Book);
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
                Affix? affix = PickAffix(chosen, level, taken, item.Shares(), random);
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
                Base = baseItem.Name,
                Seed = NewSeed(random),
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

        /// <summary>Every base there is, by name.</summary>
        public static IEnumerable<string> BaseNames => Bases.Select(b => b.Name);

        /// <summary>A base by number, which only means anything between two copies of one build, and 0 for none.</summary>
        /// <summary>What everyone starts with: a blow in front of them, for nothing, and room for one thing more.</summary>
        public static Item Book(ulong seed)
        {
            Item book = EmptyBook(StarterPlaces, "Worn Book", seed, 1, Rarity.Common);
            book.Held![0] = Leaf(Scribe.NewPage(PageKind.Area), StarterStickers, "Area Page", NextSeed(seed), 1, Rarity.Common);
            return book;
        }

        private static ulong NextSeed(ulong seed) => seed * 6364136223846793005UL + 1442695040888963407UL | 1UL;

        private static Item EmptyBook(int places, string name, ulong seed, int level, Rarity rarity) => new Item { Name = name, Slot = ItemSlot.Book, Level = level, Seed = seed, Rarity = rarity, Held = new Item?[places] };

        private static Item Leaf(Page page, int places, string name, ulong seed, int level, Rarity rarity) => new Item { Name = name, Slot = ItemSlot.Page, Level = level, Seed = seed, Rarity = rarity, Page = page, Held = new Item?[places] };

        /// <summary>A sticker as an item.</summary>
        public static Item StickerItem(Sticker sticker, ulong seed, int level, Rarity rarity) => new Item { Name = StickerName(sticker), Slot = ItemSlot.Sticker, Level = level, Seed = seed, Rarity = rarity, Sticker = sticker };

        private static string StickerName(Sticker sticker) => sticker.Good switch
        {
            PageEffect.Potency => "Sticker of Might",
            PageEffect.Speed => "Sticker of Haste",
            PageEffect.Area => "Sticker of Reach",
            PageEffect.Range => "Sticker of Distance",
            PageEffect.Cost => "Sticker of Thrift",
            PageEffect.UseTime => "Sticker of Pace",
            PageEffect.Projectiles => "Sticker of Plenty",
            PageEffect.Pierce => "Sticker of Piercing",
            PageEffect.Chains => "Sticker of Leaping",
            PageEffect.Stops => "Sticker of Aim",
            PageEffect.Rides => "Sticker of Flight",
            _ => "Sticker",
        };

        /// <summary>A whole book as items, for what's already written: each page as a page with room for just what's on it.</summary>
        public static Item Copied(Book book, string name, ulong seed, int level = 1)
        {
            Item item = EmptyBook(Math.Max(book.Pages.Count, 1), name, seed, level, Rarity.Common);
            for (int i = 0; i < book.Pages.Count; i++)
            {
                seed = NextSeed(seed);
                Page page = book.Pages[i].Copy();
                Item leaf = Leaf(page, Math.Max(page.Stickers.Count, 1), $"{page.Kind} Page", seed, level, Rarity.Common);
                for (int s = 0; s < page.Stickers.Count; s++)
                {
                    seed = NextSeed(seed);
                    leaf.Held![s] = StickerItem(page.Stickers[s], seed, level, Rarity.Common);
                }
                page.Stickers = new List<Sticker>();
                item.Held![i] = leaf;
            }
            return item;
        }

        /// <summary>
        /// Something that goes in a book, or a book. What it can hold is what it's worth, so the better it is the more places it has, and a sticker the better it rolls.
        /// A masterwork is told by its places, so a sticker can't be one.
        /// </summary>
        public static Item Folio(int level, WorldRandom random, LootGrade grade)
        {
            ulong seed = NewSeed(random);
            int table = (int)grade;
            Rarity rarity = grade switch
            {
                LootGrade.Legendary => Rarity.Legendary,
                >= LootGrade.Rare => Rarity.Rare,
                LootGrade.Magic => Rarity.Magic,
                _ => Rarity.Common,
            };

            float kind = random.Float();
            if (kind < StickerShare && grade != LootGrade.Masterwork) return StickerItem(Stickers.Roll(grade, random), seed, level, rarity);
            if (kind < StickerShare + PageShare)
            {
                PageKind pageKind = random.Chance(0.5f) ? PageKind.Projectile : PageKind.Area;
                return Leaf(Scribe.NewPage(pageKind), random.Int(LeastStickers[table], MostStickers[table]), $"{FolioWords[table]} {pageKind} Page", seed, level, rarity);
            }
            return EmptyBook(random.Int(LeastPages[table], MostPages[table]), $"{FolioWords[table]} Book", seed, level, rarity);
        }

        private const int StarterPlaces = 2;
        private const int StarterStickers = 1;
        private const float StickerShare = 0.55f;   // Of what's found that isn't worn. What's left after pages is books.
        private const float PageShare = 0.33f;

        // By grade, from plain to legendary. A book of more places than a page has stickers is the rarer thing, as it's worth more.
        private static readonly int[] LeastPages = { 1, 2, 3, 5, 6 };
        private static readonly int[] MostPages = { 2, 2, 4, 5, 6 };
        private static readonly int[] LeastStickers = { 1, 2, 3, 4, 5 };
        private static readonly int[] MostStickers = { 1, 2, 3, 4, 5 };
        private static readonly string[] FolioWords = { "Plain", "Fine", "Gilded", "Masterful", "Fabled" };

        public static int NumberOf(string baseName) => Array.FindIndex(Bases, b => b.Name == baseName) + 1;

        public static string NameOf(int number) => number > 0 && number <= Bases.Length ? Bases[number - 1].Name : "";

        /// <summary>A seed for how something looks. From a fork of its own, so that it takes nothing from what the rest of the item goes on to roll.</summary>
        public static ulong NewSeed(WorldRandom random)
        {
            ulong seed = random.Fork("look").NextULong();
            return seed == 0 ? 1 : seed;
        }

        /// <summary>What an item was made from. Anything from before that was kept says so in its name, and a base that's gone is nothing, which a look can start from as well.</summary>
        public static string BaseOf(Item item)
        {
            if (item.Base.Length > 0) return item.Base;
            if (item.Unique != null && UniqueCatalog.Find(item.Unique) is UniqueDefinition unique) return unique.Base;

            string found = "";
            foreach (BaseItem candidate in Bases)
            {
                if (candidate.Slot == item.Slot && candidate.Name.Length > found.Length && item.Name.Contains(candidate.Name)) found = candidate.Name;
            }
            return found;
        }

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

        private static Affix? PickAffix(ItemSlot slot, int level, HashSet<string> taken, in FamilyShares shares, WorldRandom random)
        {
            var options = new List<Affix>();
            foreach (Affix affix in AffixCatalog.All)
            {
                if (taken.Contains(affix.Id) || !affix.Slots.Contains(slot)) continue;
                if (affix.Tiers[0].MinLevel <= level) options.Add(affix);
            }
            if (options.Count == 0) return null;

            var weights = new List<float>(options.Count);
            AffixCatalog.Weigh(options, shares, weights);
            return options[random.PickWeighted(weights)];
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

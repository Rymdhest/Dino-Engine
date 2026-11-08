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
        Orders,     // Given to the waygate in the Haven, for the next land. See LANDS.md.
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

        private static readonly WornSlot[] Hands = { WornSlot.MainHand, WornSlot.OffHand };

        /// <summary>Every place something worn as this can go, in the order they'd be filled.</summary>
        public static IReadOnlyList<WornSlot> For(ItemSlot slot) => slot == ItemSlot.Finger ? Fingers : slot >= ItemSlot.Book ? Array.Empty<WornSlot>() : new[] { First(slot) };

        /// <summary>Every place this one thing can go: what it's worn as says, and a One Handed Weapon goes in either hand as well.</summary>
        public static IReadOnlyList<WornSlot> For(Item item) => item.IsOneHandedWeapon ? Hands : For(item.Slot);

        /// <summary>Whether this can be in that place.</summary>
        public static bool Fits(Item item, WornSlot place) => For(item).Contains(place);
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

    /// <summary>
    /// What an item is, which is what everything that shows one goes by: its colour, its sound, whether it has a beam. What's rolled on it follows from this
    /// (<see cref="AffixCatalog.PerSide"/>), and these four are all there are. Saved by name and sent by number.
    /// </summary>
    public enum Rarity : byte
    {
        Normal,     // Only what its base gives.
        Magic,      // A prefix and a suffix.
        Rare,       // Two of each.
        Unique,     // None of the above applies: see UniqueCatalog.
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

        /// <summary>How it goes off, if it's a book.</summary>
        public BookMode Mode { get; set; }

        /// <summary>What it's waiting for, if it's a Trigger Book.</summary>
        public BookTrigger? When { get; set; }

        /// <summary>What was written in a book before pages were items, which only a save that old has.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Book? Book { get; set; }

        public bool IsBook => Slot == ItemSlot.Book;
        public bool IsPage => Slot == ItemSlot.Page;
        public bool IsSticker => Slot == ItemSlot.Sticker;
        public bool IsOrders => Slot == ItemSlot.Orders;

        /// <summary>Whether it's something that goes in a book, or a book, rather than something worn.</summary>
        public bool IsFolio => Slot is ItemSlot.Book or ItemSlot.Page or ItemSlot.Sticker;

        /// <summary>Whether it's something that can be worn somewhere on a hero.</summary>
        public bool IsWorn => Slot < ItemSlot.Book;

        /// <summary>How many places it has for something else.</summary>
        public int Places => Held?.Length ?? 0;

        /// <summary>What it reads as, or nothing if there's nothing in it. It's a method so that it isn't saved.</summary>
        public Book? Written() => Scribe.Bind(this);

        // What's in something is sold with it.
        public int SellValue => (int)(4f * Level * (1f + (int)Rarity * 1.6f + (Lines.Count + Places) * 0.5f)) + (Held?.Sum(held => held?.SellValue ?? 0) ?? 0);
        public int BuyValue => SellValue * 4;

        public bool IsTwoHanded => Slot == ItemSlot.MainHand && WeaponKinds.Of(Kind).TwoHanded;

        /// <summary>Whether it's a weapon that leaves a hand free, and so can go in the off hand too.</summary>
        public bool IsOneHandedWeapon => Slot == ItemSlot.MainHand && !WeaponKinds.Of(Kind).TwoHanded;

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

        /// <summary>What it goes by among everything else on the ground: what it was made from, as the rest of its name is only what's rolled on it, and that's on its card. A name that wasn't rolled stays.</summary>
        public string PlainName() => Unique == null && Lines.Count > 0 && ItemGenerator.BaseOf(this) is { Length: > 0 } made ? made : Name;

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

        /// <summary>This item in a hand, as far as an attack goes. What it deals is Attack Damage of whatever type its base is.</summary>
        public WeaponProfile Weapon()
        {
            WeaponTraits traits = WeaponKinds.Of(Kind);
            Tags tags = traits.Tag | (traits.TwoHanded ? Tags.TwoHanded : Tags.OneHanded);
            return new WeaponProfile(WeaponDamage * (1f - WeaponSpread), WeaponDamage * (1f + WeaponSpread), traits.AttackTime, traits.CritChance, tags, ItemGenerator.DamageTypeOf(Base));
        }

        /// <summary>
        /// Two weapons in two hands, as far as an attack goes: they take turns, so it's what the two come to on average, struck <see cref="Rulebook.DualWieldSpeed"/> faster, and it's
        /// both kinds of weapon at once. It's the same whichever is in which hand.
        /// </summary>
        public static WeaponProfile Paired(WeaponProfile a, WeaponProfile b)
        {
            float time = (a.AttackTime + b.AttackTime) * 0.5f / (1f + Rulebook.DualWieldSpeed / 100f);
            if (b.Type < a.Type) (a, b) = (b, a);
            float offShare = a.Type == b.Type ? 0f : (b.Min + b.Max) / MathF.Max(a.Min + a.Max + b.Min + b.Max, 0.001f);
            return new WeaponProfile((a.Min + b.Min) * 0.5f, (a.Max + b.Max) * 0.5f, time, (a.CritChance + b.CritChance) * 0.5f, a.Tags | b.Tags, a.Type, b.Type, offShare);
        }

        /// <summary>What it is before anything has been rolled on it, as names and numbers.</summary>
        public IEnumerable<(string name, string value)> BaseRows()
        {
            if (WeaponDamage > 0f)
            {
                WeaponProfile weapon = Weapon();
                yield return ("Weapon Damage", $"{Say.Range(weapon.Min, weapon.Max)} {DamageKinds.Name(weapon.Type)}");
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

        /// <summary>Whether it's a prefix, which keeps a hero alive, or a suffix, which is what they kill with.</summary>
        public bool Prefix;
        public string[] Names = Array.Empty<string>();
        public ItemSlot[] Slots = Array.Empty<ItemSlot>();

        /// <summary>The only kinds of weapon it goes on, if it's for a weapon and not every one.</summary>
        public WeaponKind[]? Kinds;
        public Tier[] Tiers = Array.Empty<Tier>();
        public Func<float, Mod[]> Build = _ => Array.Empty<Mod>();

        /// <summary>Whether something of this slot, and this kind if it's a weapon, can have it.</summary>
        public bool Fits(ItemSlot slot, WeaponKind kind) => Slots.Contains(slot) && (Kinds == null || slot != ItemSlot.MainHand || Kinds.Contains(kind));
    }

    /// <summary>
    /// Everything an item can roll. Ids are saved in items, so renaming one loses it from every item that has it; a value's meaning can change
    /// freely. A prefix keeps a hero alive and a suffix is what they kill with, by what each changes (<see cref="StatSides"/>). Each stat goes only where it belongs, so that
    /// what's on an item says what kind of item it is: resistances are for rings, speed of foot for boots and speed of hand for gloves. BUILDS.md has the whole table.
    /// </summary>
    public static class AffixCatalog
    {
        private const int MagicPerSide = 1;
        private const int RarePerSide = 2;

        private static readonly ItemSlot[] Weapon = { ItemSlot.MainHand };
        private static readonly ItemSlot[] Shield = { ItemSlot.OffHand };
        private static readonly ItemSlot[] Ring = { ItemSlot.Finger };
        private static readonly ItemSlot[] Amulet = { ItemSlot.Neck };
        private static readonly ItemSlot[] Boots = { ItemSlot.Feet };
        private static readonly ItemSlot[] Gloves = { ItemSlot.Hands };
        private static readonly ItemSlot[] WeaponRing = { ItemSlot.MainHand, ItemSlot.Finger };
        private static readonly ItemSlot[] WeaponAmulet = { ItemSlot.MainHand, ItemSlot.Neck };
        private static readonly ItemSlot[] WeaponGloves = { ItemSlot.MainHand, ItemSlot.Hands };
        private static readonly ItemSlot[] HeadAmulet = { ItemSlot.Head, ItemSlot.Neck };
        private static readonly ItemSlot[] BodyAmulet = { ItemSlot.Body, ItemSlot.Neck };
        private static readonly ItemSlot[] Plates = { ItemSlot.OffHand, ItemSlot.Body };
        private static readonly ItemSlot[] Upper = { ItemSlot.OffHand, ItemSlot.Head, ItemSlot.Body };
        private static readonly ItemSlot[] Lower = { ItemSlot.Body, ItemSlot.Legs, ItemSlot.Feet };
        private static readonly ItemSlot[] Armours = { ItemSlot.OffHand, ItemSlot.Head, ItemSlot.Body, ItemSlot.Legs, ItemSlot.Feet, ItemSlot.Hands };
        private static readonly ItemSlot[] Worn = { ItemSlot.OffHand, ItemSlot.Head, ItemSlot.Body, ItemSlot.Legs, ItemSlot.Feet, ItemSlot.Hands, ItemSlot.Finger, ItemSlot.Neck };

        /// <summary>How much the lines of something wholly of one family go to what suits it, as a share of all there is to draw from on that side of its slot. What an item looks like should say what it's for, and at 1 that's over half of its lines, but anything its slot takes can still roll on it.</summary>
        private const float Leaning = 1f;

        private static readonly Dictionary<string, Affix> ById = new();
        private static readonly List<Affix> Everything = new();
        private static readonly Dictionary<string, GearFamily> Suits = new();

        public static IReadOnlyList<Affix> All => Everything;

        /// <summary>Every affix that suits a family, and which.</summary>
        public static IReadOnlyDictionary<string, GearFamily> Suited => Suits;

        public static Affix? Find(string id) => ById.GetValueOrDefault(id);

        /// <summary>How many prefixes something of this rarity has, and as many suffixes. Nothing normal has any, and a unique is what it is.</summary>
        public static int PerSide(Rarity rarity) => rarity switch
        {
            Rarity.Magic => MagicPerSide,
            Rarity.Rare => RarePerSide,
            _ => 0,
        };

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

        // Spell Damage comes from nowhere else, so what a caster's weapon rolls is what a caster lives on, as its base damage once was.
        private static readonly Tier[] SpellAdded = Ladder((1, 5, 15), (6, 20, 35), (12, 40, 65), (20, 70, 110), (30, 115, 180), (42, 185, 275));
        private static readonly WeaponKind[] Casters = { WeaponKind.Wand, WeaponKind.Staff };
        private static readonly Tier[] Pool = Ladder((1, 10, 20), (6, 21, 40), (12, 41, 70), (20, 71, 110), (32, 111, 160), (45, 161, 230));
        private static readonly Tier[] Resistance = Ladder((1, 6, 12), (7, 13, 20), (14, 21, 30), (24, 31, 42));
        private static readonly Tier[] Rating = Ladder((1, 3, 7), (6, 8, 16), (12, 17, 30), (20, 31, 50), (30, 51, 85), (42, 86, 130));
        private static readonly Tier[] Chances = Ladder((2, 5, 10), (10, 11, 18), (20, 19, 28));

        // Which side it's on goes by what it builds, so that it can't be written down as the other.
        private static void Add(string id, string[] names, ItemSlot[] slots, Tier[] tiers, Func<float, Mod[]> build, WeaponKind[]? kinds = null)
        {
            var affix = new Affix { Id = id, Prefix = StatSides.IsDefensive(build(1f)[0].Stat), Names = names, Slots = slots, Kinds = kinds, Tiers = tiers, Build = build };
            ById.Add(id, affix);
            Everything.Add(affix);
        }

        private static void Add(string id, string[] names, ItemSlot[] slots, Tier[] tiers, Func<float, Mod> build, WeaponKind[]? kinds = null) => Add(id, names, slots, tiers, v => new[] { build(v) }, kinds);

        static AffixCatalog()
        {
            // Prefixes: what keeps a hero alive. First what stands between them and a blow, on whatever they wear.
            Add("armour", new[] { "Sturdy", "Reinforced", "Fortified", "Unyielding", "Adamant", "Impregnable" }, Armours, Rating, v => Flat(Stat.Armour, v));
            Add("armour-percent", new[] { "Iron", "Steel", "Mithril", "Dragonscale" }, Upper, Percent, v => Inc(Stat.Armour, v));
            Add("evasion", new[] { "Fleet", "Agile", "Lithe", "Elusive", "Phantasmal", "Vanishing" }, Armours, Rating, v => Flat(Stat.Evasion, v));
            Add("evasion-percent", new[] { "Shadowed", "Blurred", "Ghostly", "Unseen" }, Upper, Percent, v => Inc(Stat.Evasion, v));
            Add("barrier", new[] { "Warded", "Shielded", "Sheltered", "Enclosed", "Bastioned", "Inviolate" }, Armours, Pool, v => Flat(Stat.Barrier, v * 0.8f));
            Add("barrier-percent", new[] { "Shimmering", "Gleaming", "Radiant" }, Upper, Percent, v => Inc(Stat.Barrier, v));
            Add("block", new[] { "Deflecting", "Parrying", "Turning" }, Shield, Ladder((1, 3, 6), (12, 7, 11), (26, 12, 16)), v => Flat(Stat.BlockChance, v));
            Add("flat-reduction", new[] { "Tough", "Hidebound", "Scaled", "Tortoiseshell" }, Upper, Ladder((3, 1, 2), (12, 3, 6), (22, 7, 12), (34, 13, 20)), v => Flat(Stat.FlatReduction, v));
            Add("physical-reduction", new[] { "Padded", "Bulwarked" }, new[] { ItemSlot.OffHand, ItemSlot.Body, ItemSlot.Legs }, Ladder((10, 2, 3), (24, 4, 6)), v => Flat(Stat.PhysicalReduction, v));
            Add("physical-taken-as-fire", new[] { "Cinderhide" }, Plates, Ladder((8, 6, 12), (24, 13, 20)), v => TakenAs(DamageType.Physical, DamageType.Fire, v));
            Add("physical-taken-as-cold", new[] { "Rimehide" }, Plates, Ladder((8, 6, 12), (24, 13, 20)), v => TakenAs(DamageType.Physical, DamageType.Cold, v));
            Add("physical-taken-as-lightning", new[] { "Stormhide" }, Plates, Ladder((8, 6, 12), (24, 13, 20)), v => TakenAs(DamageType.Physical, DamageType.Lightning, v));

            // Resistances are what rings are for, and only rings.
            Add("fire-resistance", new[] { "Salamander's", "Hearthborn", "Furnaced", "Sunward" }, Ring, Resistance, v => Resist(Tags.Fire, v));
            Add("cold-resistance", new[] { "Seal's", "Furred", "Tundra", "Polar" }, Ring, Resistance, v => Resist(Tags.Cold, v));
            Add("lightning-resistance", new[] { "Insulated", "Grounded", "Earthed", "Stormproof" }, Ring, Resistance, v => Resist(Tags.Lightning, v));
            Add("magic-resistance", new[] { "Unbelieving", "Spellproof", "Nullifying", "Voidbound" }, Ring, Resistance, v => Resist(Tags.Magic, v));
            Add("poison-resistance", new[] { "Antidotal", "Pure", "Unblemished", "Immune" }, Ring, Resistance, v => Resist(Tags.Poison, v));
            Add("all-resistance", new[] { "Prismatic", "Spectral", "Rainbow" }, Ring, Ladder((6, 3, 6), (16, 7, 11), (28, 12, 16)), v => Resist(Tags.Elemental, v));

            // The pools, and getting them back.
            Add("health", new[] { "Stout", "Bearish", "Oaken", "Mountainous", "Titan's", "Colossal" }, new[] { ItemSlot.OffHand, ItemSlot.Head, ItemSlot.Body, ItemSlot.Legs, ItemSlot.Neck }, Pool, v => Flat(Stat.Health, v));
            Add("health-percent", new[] { "Hale", "Robust", "Stalwart", "Hearty" }, BodyAmulet, Ladder((8, 3, 5), (18, 6, 9), (30, 10, 14)), v => Inc(Stat.Health, v));
            Add("mana", new[] { "Minded", "Sagely", "Insightful", "Welling", "Deep", "Abyssal" }, new[] { ItemSlot.Head, ItemSlot.Hands, ItemSlot.Neck }, Pool, v => Flat(Stat.Mana, v * 0.7f));
            Add("mana-percent", new[] { "Lucid", "Brimming", "Boundless" }, HeadAmulet, Ladder((8, 4, 7), (18, 8, 12), (30, 13, 18)), v => Inc(Stat.Mana, v));
            Add("taken-from-mana", new[] { "Composed", "Unshaken" }, HeadAmulet, Ladder((10, 3, 6), (24, 7, 11)), v => Flat(Stat.TakenFromMana, v));
            Add("health-regen", new[] { "Renewing", "Vigorous", "Springing", "Hydra's" }, new[] { ItemSlot.Body, ItemSlot.Legs, ItemSlot.Neck }, Ladder((2, 0.5f, 1.2f), (9, 1.3f, 2.5f), (17, 2.6f, 4.5f), (30, 4.6f, 9f)), v => Flat(Stat.HealthRegen, v));
            Add("health-regen-percent", new[] { "Convalescent", "Regrowing" }, BodyAmulet, Ladder((12, 0.3f, 0.6f), (26, 0.7f, 1.2f)), v => Flat(Stat.HealthRegenPercent, v));
            Add("mana-regen", new[] { "Clear", "Meditative", "Serene", "Tidal" }, new[] { ItemSlot.Head, ItemSlot.Hands, ItemSlot.Neck }, Ladder((2, 0.4f, 1f), (9, 1.1f, 2f), (17, 2.1f, 3.5f), (30, 3.6f, 7f)), v => Flat(Stat.ManaRegen, v));
            Add("barrier-recharge", new[] { "Restoring", "Ramparted" }, BodyAmulet, SmallPercent, v => Inc(Stat.BarrierRecharge, v));
            Add("recovery", new[] { "Physician's", "Healer's", "Saint's" }, BodyAmulet, SmallPercent, v => Inc(Stat.Recovery, v));
            Add("flask-effect", new[] { "Tippler's", "Apothecary's" }, Amulet, SmallPercent, v => Inc(Stat.FlaskEffect, v));

            // What a weapon does for whoever's holding it: what it takes out of what it cuts.
            Add("health-leech", new[] { "Leeching", "Lamprey's", "Vampiric" }, Weapon, Ladder((6, 0.3f, 0.6f), (16, 0.7f, 1.1f), (28, 1.2f, 1.8f)), v => Flat(Stat.HealthLeech, v));
            Add("mana-leech", new[] { "Thirsting", "Draining", "Mind-Eating" }, Weapon, Ladder((6, 0.2f, 0.4f), (16, 0.5f, 0.8f), (28, 0.9f, 1.3f)), v => Flat(Stat.ManaLeech, v));
            Add("health-on-hit", new[] { "Mending", "Knitting" }, WeaponGloves, Ladder((4, 1, 2.5f), (13, 3, 6), (26, 7, 12)), v => Flat(Stat.HealthOnHit, v));
            Add("mana-on-hit", new[] { "Siphoning", "Tapping" }, WeaponGloves, Ladder((4, 0.5f, 1.5f), (13, 2, 3.5f), (26, 4, 7)), v => Flat(Stat.ManaOnHit, v));
            Add("health-on-kill", new[] { "Harvesting", "Reaping" }, WeaponAmulet, Pool, v => Flat(Stat.HealthOnKill, v * 0.5f));
            Add("mana-on-kill", new[] { "Gleaning", "Absorbing" }, WeaponAmulet, Pool, v => Flat(Stat.ManaOnKill, v * 0.3f));
            Add("barrier-on-kill", new[] { "Husked", "Carapaced" }, WeaponAmulet, Pool, v => Flat(Stat.BarrierOnKill, v * 0.4f));

            // Staying on your feet, and getting away. How fast a hero goes is for boots, and nothing else.
            Add("ailment-taken", new[] { "Shrugging", "Stoic" }, Lower, SmallPercent, v => Inc(Stat.AilmentTaken, -v));
            Add("stun-threshold", new[] { "Boulder", "Anchored" }, Lower, Percent, v => Inc(Stat.StunThreshold, v));
            Add("ailment-avoid", new[] { "Untouched", "Unmarked" }, Lower, Ladder((6, 6, 12), (18, 13, 22), (30, 23, 32)), v => Flat(Stat.AilmentAvoid, v));
            Add("move-speed", new[] { "Roadworn", "Windswept", "Galeborn", "Cometary" }, Boots, Ladder((1, 4, 8), (8, 9, 13), (16, 14, 20), (30, 21, 28)), v => Inc(Stat.MoveSpeed, v));
            Add("dodge-cooldown", new[] { "Quickfooted", "Dancing" }, new[] { ItemSlot.Feet, ItemSlot.Legs }, SmallPercent, v => Inc(Stat.DodgeCooldown, -v));
            Add("stealth", new[] { "Muffled", "Hushed", "Silent" }, new[] { ItemSlot.Feet, ItemSlot.Legs, ItemSlot.Head }, Ladder((4, 5, 10), (14, 11, 18), (26, 19, 28)), v => Flat(Stat.Stealth, v));

            // Suffixes: what a hero kills with. First what a blow is made of. These ids are what saves from before this system map onto, so they stay.
            Add("damage", new[] { "of Sharpness", "of the Edge", "of Cruelty", "of No Mercy", "of Ruination" }, Weapon, Ladder((1, 8, 15), (5, 16, 28), (10, 29, 45), (16, 46, 70), (24, 71, 100)), v => Inc(Stat.Damage, v));
            Add("added-physical", new[] { "of Jags", "of Barbs", "of Rending", "of Brutality", "of Savagery", "of Flaying" }, WeaponRing, Added, v => M.Added(DamageType.Physical, v, Tags.Attack));
            Add("added-fire", new[] { "of Warmth", "of Smouldering", "of Burning", "of the Blaze", "of the Inferno", "of the Sun" }, WeaponRing, Added, v => M.Added(DamageType.Fire, v, Tags.Attack));
            Add("added-cold", new[] { "of Chill", "of Frost", "of Biting Cold", "of Ice", "of Polar Night", "of Absolute Zero" }, WeaponRing, Added, v => M.Added(DamageType.Cold, v, Tags.Attack));
            Add("added-lightning", new[] { "of Humming", "of Sparks", "of Crackling", "of Arcs", "of Thunder", "of the Heavens" }, WeaponRing, Added, v => M.Added(DamageType.Lightning, v, Tags.Attack));
            Add("added-magic", new[] { "of Whispers", "of Charms", "of Ensorcelling", "of the Eldritch", "of the Unbound", "of Sovereignty" }, WeaponRing, Added, v => M.Added(DamageType.Magic, v, Tags.Attack));
            Add("added-poison", new[] { "of Taint", "of Sickness", "of Foulness", "of Venom", "of Virulence", "of Plague" }, WeaponRing, Added, v => M.Added(DamageType.Poison, v, Tags.Attack));

            // Spell Damage, which only a wand or a staff has.
            Add("spell-physical", new[] { "of the Stone Word", "of the Iron Word", "of the Grinding Word", "of the Crushing Word", "of the Shattering Word", "of the Breaking Word" }, Weapon, SpellAdded, v => M.Added(DamageType.Physical, v, Tags.Spell), Casters);
            Add("spell-fire", new[] { "of Kindling", "of Cinders", "of Flames", "of the Pyre", "of the Furnace", "of the Firestorm" }, Weapon, SpellAdded, v => M.Added(DamageType.Fire, v, Tags.Spell), Casters);
            Add("spell-cold", new[] { "of Rimefall", "of Sleet", "of Glaciers", "of the Long Winter", "of the Frozen Deep", "of the White Death" }, Weapon, SpellAdded, v => M.Added(DamageType.Cold, v, Tags.Spell), Casters);
            Add("spell-lightning", new[] { "of Crackle", "of Sparking", "of Lightning", "of the Storm", "of the Tempest", "of the Thunderbolt" }, Weapon, SpellAdded, v => M.Added(DamageType.Lightning, v, Tags.Spell), Casters);
            Add("spell-magic", new[] { "of Incantation", "of Invocation", "of Conjuring", "of the Arcane", "of the Mystic", "of the Starlit Word" }, Weapon, SpellAdded, v => M.Added(DamageType.Magic, v, Tags.Spell), Casters);
            Add("spell-poison", new[] { "of Miasma", "of Mildew", "of Contagion", "of the Plague Word", "of the Bog", "of the Black Rot" }, Weapon, SpellAdded, v => M.Added(DamageType.Poison, v, Tags.Spell), Casters);

            Add("spell-damage", new[] { "of the Apprentice", "of the Adept", "of the Learned", "of the Magus", "of the Archmage" }, WeaponAmulet, Percent, v => Inc(Stat.Damage, v, Tags.Spell));
            Add("attack-damage", new[] { "of the Soldier", "of the Veteran", "of the Champion", "of the Warmaster", "of the Conqueror" }, WeaponAmulet, Percent, v => Inc(Stat.Damage, v, Tags.Attack));
            Add("physical-damage", new[] { "of Weight", "of Heft", "of Mass", "of Crushing", "of the Titanic" }, WeaponAmulet, Percent, v => Inc(Stat.Damage, v, Tags.Physical));
            Add("fire-damage", new[] { "of Embers", "of Flame", "of Pyres", "of Conflagration", "of Cremation" }, WeaponAmulet, Percent, v => Inc(Stat.Damage, v, Tags.Fire));
            Add("cold-damage", new[] { "of Rime", "of Hoarfrost", "of Winter", "of the Boreal", "of Deathly Cold" }, WeaponAmulet, Percent, v => Inc(Stat.Damage, v, Tags.Cold));
            Add("lightning-damage", new[] { "of Static", "of Galvanism", "of Stormlight", "of Tempests", "of the Thunderhead" }, WeaponAmulet, Percent, v => Inc(Stat.Damage, v, Tags.Lightning));
            Add("magic-damage", new[] { "of Hedge Magic", "of Runes", "of Sigils", "of the Occult", "of the Forbidden" }, WeaponAmulet, Percent, v => Inc(Stat.Damage, v, Tags.Magic));
            Add("poison-damage", new[] { "of Noxious Air", "of Rancour", "of Blight", "of Pestilence", "of Rot" }, WeaponAmulet, Percent, v => Inc(Stat.Damage, v, Tags.Poison));
            Add("elemental-damage", new[] { "of Colours", "of the Elements", "of the Primal", "of the Primordial" }, Amulet, SmallPercent, v => Inc(Stat.Damage, v, Tags.Elemental));
            Add("area-damage", new[] { "of Sweeping", "of the Broadside", "of Sundering", "of Cataclysm" }, Amulet, SmallPercent, v => Inc(Stat.Damage, v, Tags.Area));
            Add("projectile-damage", new[] { "of Fletching", "of Balance", "of Whistling", "of the True Shot" }, Amulet, SmallPercent, v => Inc(Stat.Damage, v, Tags.Projectile));
            Add("melee-damage", new[] { "of the Brawler", "of the Duellist", "of the Gladiator", "of the Warlord" }, Amulet, SmallPercent, v => Inc(Stat.Damage, v, Tags.Melee));
            Add("lingering-damage", new[] { "of Festering", "of Lingering", "of Wasting", "of Slow Death", "of the Inexorable" }, new[] { ItemSlot.MainHand, ItemSlot.Neck, ItemSlot.Legs }, Percent, v => Inc(Stat.Damage, v, Tags.DamageOverTime));
            Add("minion-damage", new[] { "of the Shepherd", "of the Master", "of the Tyrant", "of the Lich", "of the Necrarch" }, new[] { ItemSlot.MainHand, ItemSlot.Neck, ItemSlot.Head }, Percent, v => Inc(Stat.Damage, v, Tags.Minion));

            // How fast a hero strikes and casts is for gloves, and nothing else.
            Add("attack-speed", new[] { "of Haste", "of Fury", "of the Storm", "of the Whirlwind" }, Gloves, Ladder((3, 4, 8), (10, 9, 14), (18, 15, 22), (30, 23, 30)), v => Inc(Stat.AttackSpeed, v));
            Add("cast-speed", new[] { "of Muttering", "of Fluency", "of Recitation", "of the Unbroken Word" }, Gloves, Ladder((3, 4, 8), (10, 9, 14), (18, 15, 22), (30, 23, 30)), v => Inc(Stat.CastSpeed, v));
            Add("crit-chance", new[] { "of Precision", "of the Hawk", "of the Unerring", "of the Killing Eye" }, WeaponGloves, Ladder((2, 1, 3), (9, 3.5f, 6), (17, 6.5f, 10), (30, 10.5f, 14)), v => Flat(Stat.CritChance, v));
            Add("crit-increased", new[] { "of Focus", "of Intent", "of Certainty", "of Fate" }, WeaponAmulet, Percent, v => Inc(Stat.CritChance, v));
            Add("crit-multiplier", new[] { "of Wounding", "of Slaughter", "of Ruin", "of Annihilation" }, WeaponAmulet, Ladder((4, 10, 20), (11, 21, 35), (19, 36, 60), (32, 61, 85)), v => Flat(Stat.CritMultiplier, v));
            Add("accuracy", new[] { "of the Marksman", "of the Sharpshooter", "of the Sniper" }, new[] { ItemSlot.Hands, ItemSlot.Head }, Pool, v => Flat(Stat.Accuracy, v));
            Add("penetration", new[] { "of Piercing", "of Unmaking", "of Undoing" }, WeaponAmulet, Ladder((8, 4, 7), (18, 8, 12), (30, 13, 18)), v => Flat(Stat.Penetration, v));

            // How a book goes.
            Add("area", new[] { "of Reach", "of Breadth", "of the Expanse", "of Horizons" }, HeadAmulet, SmallPercent, v => Inc(Stat.AreaOfEffect, v));
            Add("duration", new[] { "of Patience", "of Endurance", "of Ages" }, HeadAmulet, SmallPercent, v => Inc(Stat.PageDuration, v));
            Add("cooldown", new[] { "of Readiness", "of Renewal", "of the Eternal Hour" }, HeadAmulet, Ladder((5, 4, 7), (15, 8, 12), (28, 13, 18)), v => Inc(Stat.CooldownRecovery, v));
            Add("cost", new[] { "of Thrift", "of Economy", "of the Miser" }, HeadAmulet, Ladder((3, 4, 7), (12, 8, 12), (24, 13, 18)), v => Inc(Stat.BookCost, -v));
            Add("projectile-speed", new[] { "of Flight", "of the Arrow", "of the Falcon" }, WeaponGloves, SmallPercent, v => Inc(Stat.ProjectileSpeed, v));
            Add("potency", new[] { "of Ink", "of Eloquence" }, WeaponAmulet, Ladder((10, 5, 8), (26, 9, 15)), v => Inc(Stat.Potency, v));

            // What it leaves behind.
            Add("burn-chance", new[] { "of Kindling", "of Immolation", "of the Pyre" }, WeaponAmulet, Chances, v => Chance(Tags.Burn, v));
            Add("bleed-chance", new[] { "of Cuts", "of Lacerating", "of Exsanguination" }, WeaponAmulet, Chances, v => Chance(Tags.Bleed, v));
            Add("venom-chance", new[] { "of the Asp", "of the Viper", "of the Cobra" }, WeaponAmulet, Chances, v => Chance(Tags.Venom, v));
            Add("shock-chance", new[] { "of Jolting", "of Shocking", "of Paralysis" }, WeaponAmulet, Chances, v => Chance(Tags.Shock, v));
            Add("freeze-chance", new[] { "of Numbing", "of Stillness", "of the Glacier" }, WeaponAmulet, Chances, v => Chance(Tags.Freeze, v));
            Add("hex-chance", new[] { "of Ill Omen", "of Hexing", "of the Curse" }, WeaponAmulet, Chances, v => Chance(Tags.Hex, v));
            Add("stun-chance", new[] { "of Staggering", "of Stunning", "of the Hammer" }, Weapon, Chances, v => Chance(Tags.Stun, v));
            Add("ailment-duration", new[] { "of Suffering", "of Torment", "of Agony" }, new[] { ItemSlot.Neck, ItemSlot.Legs }, SmallPercent, v => Inc(Stat.AilmentDuration, v));
            Add("reaction-damage", new[] { "of Catalysis", "of Reaction", "of the Crucible" }, WeaponAmulet, Percent, v => Inc(Stat.ReactionDamage, v));
            Add("reflect", new[] { "of Thorns", "of Spines", "of the Briar" }, Plates, Ladder((3, 5, 10), (14, 11, 20), (28, 21, 35)), v => Flat(Stat.Reflect, v));

            // Attributes are where power comes from, so they go on anything worn, which also means there's something to kill with on every piece of armour.
            Add("strength", new[] { "of the Brute", "of Might", "of Iron", "of the Giant", "of Titans", "of the Mountain King" }, Worn, Rating, v => Flat(Stat.Strength, v));
            Add("dexterity", new[] { "of the Fox", "of Grace", "of the Dancer", "of the Cat", "of Quicksilver", "of the Wind" }, Worn, Rating, v => Flat(Stat.Dexterity, v));
            Add("intellect", new[] { "of the Student", "of Wit", "of the Scholar", "of the Sage's Eye", "of Genius", "of the Oracle" }, Worn, Rating, v => Flat(Stat.Intellect, v));

            // Doing well out of it.
            Add("gold-find", new[] { "of Gilding", "of Gold" }, new[] { ItemSlot.Finger, ItemSlot.Feet }, Ladder((1, 10, 25), (10, 26, 50)), v => Inc(Stat.GoldFind, v));
            Add("item-rarity", new[] { "of Luck", "of Fortune" }, Ring, Ladder((3, 6, 14), (13, 15, 28)), v => Inc(Stat.ItemRarity, v));
            Add("experience", new[] { "of Study", "of Enlightenment" }, new[] { ItemSlot.Finger, ItemSlot.Neck }, Ladder((10, 2, 4), (24, 5, 8)), v => Inc(Stat.ExperienceGain, v));

            // What each family is good for. Weapons lean the same way as what they're carried with, so a staff tends to be a caster's.
            Suit(GearFamily.Heavy, "strength", "armour", "armour-percent", "health", "health-percent", "block", "flat-reduction", "physical-reduction", "reflect", "stun-threshold", "health-regen",
                "health-regen-percent", "physical-damage", "attack-damage", "stun-chance");
            Suit(GearFamily.Leather, "dexterity", "evasion", "evasion-percent", "attack-speed", "crit-chance", "crit-increased", "crit-multiplier", "accuracy", "move-speed", "dodge-cooldown", "stealth",
                "ailment-avoid", "projectile-speed", "bleed-chance", "venom-chance");
            Suit(GearFamily.Cloth, "intellect", "mana", "mana-percent", "mana-regen", "barrier", "barrier-percent", "barrier-recharge", "spell-damage", "spell-physical", "spell-fire", "spell-cold", "spell-lightning", "spell-magic", "spell-poison", "cast-speed", "cooldown", "cost", "potency",
                "taken-from-mana", "duration", "minion-damage", "mana-on-hit", "mana-leech", "mana-on-kill");
        }
    }

    /// <summary>
    /// Rolls items. As in the classic ARPGs, an item's level decides which tiers of each affix can appear on it, its rarity how
    /// many it gets of each kind, its slot which can, and the value within a tier is a roll of its own: so the same drop is never
    /// quite the same twice, and a deeper world holds better things without any item being placed by hand.
    /// </summary>
    public static class ItemGenerator
    {
        private sealed record BaseItem(string Name, ItemSlot Slot, int MinLevel, float Damage = 0f, float Armour = 0f, float Evasion = 0f, float Barrier = 0f, WeaponKind Kind = WeaponKind.Sword, DamageType Type = DamageType.Physical);

        /// <summary>What type of Attack Damage a base deals, which is physical for anything that isn't a caster's.</summary>
        public static DamageType DamageTypeOf(string baseName) => Array.Find(Bases, item => item.Name == baseName)?.Type ?? DamageType.Physical;

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
            // A caster's weapon strikes softly, as what it's for is Spell Damage, which its suffixes are. What it does strike with is the same element as the look of it.
            new("Walking Staff", ItemSlot.MainHand, 1, 6f, Kind: WeaponKind.Staff),
            new("Ash Staff", ItemSlot.MainHand, 4, 14f, Kind: WeaponKind.Staff, Type: DamageType.Fire),
            new("Bound Staff", ItemSlot.MainHand, 9, 26f, Kind: WeaponKind.Staff, Type: DamageType.Cold),
            new("Runed Staff", ItemSlot.MainHand, 20, 58f, Kind: WeaponKind.Staff, Type: DamageType.Lightning),
            new("Twig", ItemSlot.MainHand, 1, 4f, Kind: WeaponKind.Wand, Type: DamageType.Lightning),
            new("Carved Wand", ItemSlot.MainHand, 4, 10f, Kind: WeaponKind.Wand, Type: DamageType.Fire),
            new("Bone Wand", ItemSlot.MainHand, 8, 20f, Kind: WeaponKind.Wand, Type: DamageType.Poison),
            new("Sceptre", ItemSlot.MainHand, 20, 43f, Kind: WeaponKind.Wand, Type: DamageType.Magic),

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
            return Compose(level, random, rarity, slot, 0f);
        }

        /// <summary>An item of exactly this rarity, for what's already been decided by a loot table. A unique with nothing to be at this level comes out as the next best thing.</summary>
        /// <param name="quality">From 0 to 1, how well it comes out: from a fair roll of everything, to every line at the top of what its level can reach, on the newest make.</param>
        public static Item Forge(int level, WorldRandom random, Rarity rarity, float quality, ItemSlot? slot = null) => Compose(level, random, rarity, slot, quality);

        private static Item Compose(int level, WorldRandom random, Rarity rarity, ItemSlot? slot, float quality)
        {
            ItemSlot chosen = slot ?? (ItemSlot)random.Int((int)ItemSlot.Book);
            if (rarity == Rarity.Unique && UniqueCatalog.Pick(level, slot, random) is UniqueDefinition unique) return UniqueCatalog.Make(unique, level, random);
            if (rarity == Rarity.Unique) rarity = Rarity.Rare;

            BaseItem baseItem = PickBase(chosen, level, random, quality);
            Item item = Make(baseItem, level, random, quality);
            item.Rarity = rarity;
            Deal(item, random, quality);
            item.Name = Name(baseItem.Name, item.Lines);
            return item;
        }

        // Tops up each side to what its rarity has, prefixes first, so that a card always reads the same way round.
        private static void Deal(Item item, WorldRandom random, float quality)
        {
            int each = AffixCatalog.PerSide(item.Rarity);
            int level = Math.Max(item.Level, 1);
            var dealt = new List<ItemLine>(each * 2);
            foreach (bool prefix in new[] { true, false })
            {
                List<ItemLine> side = item.Lines.Where(line => AffixCatalog.Find(line.Affix)!.Prefix == prefix).ToList();
                while (side.Count < each && PickAffix(item.Slot, item.Kind, prefix, level, side, item.Shares(), random) is Affix affix)
                {
                    Tier rolled = affix.Tiers[BestTier(affix, level, random, quality)];
                    side.Add(new ItemLine(affix.Id, Rise(rolled.Low, rolled.High, quality, random)));
                }
                dealt.AddRange(side);
            }
            item.Lines = dealt;
        }

        /// <summary>
        /// Brings something from a save up to the rules, whatever it was made under. A line stays if its slot can have it and its side has room, in the order it's written. What's left
        /// to fill is drawn from the item's own seed, so a hero's rare is still a rare and the same one each time. Anything already within the rules isn't touched, and a unique answers to nothing.
        /// </summary>
        public static void Fix(Item item)
        {
            if (item.IsOrders)
            {
                OrdersMaker.Fix(item);
                return;
            }
            if (item.Unique != null && UniqueCatalog.Find(item.Unique) == null) item.Unique = null;
            if (item.Unique != null)
            {
                item.Rarity = Rarity.Unique;
                return;
            }

            // A sticker is Unique for what's on it and nothing else is that's for a book, so a sticker's rarity is what it says.
            if (item.Sticker is Sticker held && item.IsSticker && (item.Rarity == Rarity.Unique || Stickers.IsUnique(held.Good)))
            {
                item.Rarity = Stickers.IsUnique(held.Good) ? Rarity.Unique : Rarity.Rare;
                return;
            }
            if (!Enum.IsDefined(item.Rarity) || item.Rarity == Rarity.Unique) item.Rarity = Rarity.Rare;
            if (item.IsFolio)
            {
                foreach (string word in OldFolioWords) item.Name = item.Name.Replace(word, FolioWords[^1]);
                return;
            }

            int each = AffixCatalog.PerSide(item.Rarity);
            var kept = new List<ItemLine>(each * 2);
            int prefixes = 0, suffixes = 0;
            foreach (ItemLine line in item.Lines)
            {
                if (AffixCatalog.Find(line.Affix) is not Affix affix || !affix.Fits(item.Slot, item.Kind) || kept.Any(other => other.Affix == line.Affix)) continue;
                if ((affix.Prefix ? prefixes : suffixes) >= each) continue;
                if (affix.Prefix) prefixes++;
                else suffixes++;
                kept.Add(line);
            }
            if (kept.Count == item.Lines.Count && prefixes == each && suffixes == each) return;

            item.Lines = kept;
            Deal(item, new WorldRandom(item.LookSeed()).Fork("lines"), 0f);
            string baseName = BaseOf(item);
            if (baseName.Length > 0) item.Name = Name(baseName, item.Lines);
        }

        // A roll that good quality leans towards the top of. At none it's the plain roll.
        private static float Rise(float low, float high, float quality, WorldRandom random)
        {
            float share = 1f - MathF.Pow(1f - random.Float(), 1f + quality * 2.5f);
            return low + (high - low) * share;
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
            Item book = EmptyBook(StarterPlaces, "Worn Book", seed, 1, Rarity.Normal);
            book.Held![0] = Leaf(Scribe.NewPage(PageKind.Area), StarterStickers, "Attack Area Page", NextSeed(seed), 1, Rarity.Normal);
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
            PageEffect.Grounded => "Stonecatcher Sticker",
            PageEffect.FromCursor => "Farcaster Sticker",
            PageEffect.FromNearest => "Huntsman's Sticker",
            PageEffect.FromRandom => "Gambler's Sticker",
            PageEffect.ToNearest => "Seeker's Sticker",
            PageEffect.ToYou => "Homing Sticker",
            PageEffect.ToAnywhere => "Madcap Sticker",
            PageEffect.Falls => "Skyfall Sticker",
            _ => "Sticker",
        };

        /// <summary>A whole book as items, for what's already written: each page as a page with room for just what's on it.</summary>
        public static Item Copied(Book book, string name, ulong seed, int level = 1)
        {
            Item item = EmptyBook(Math.Max(book.Pages.Count, 1), name, seed, level, Rarity.Normal);
            item.Mode = book.Mode;
            item.When = book.When?.Copy();
            for (int i = 0; i < book.Pages.Count; i++)
            {
                seed = NextSeed(seed);
                Page page = book.Pages[i].Copy();
                Item leaf = Leaf(page, Math.Max(page.Stickers.Count, 1), PageName(page), seed, level, Rarity.Normal);
                for (int s = 0; s < page.Stickers.Count; s++)
                {
                    seed = NextSeed(seed);
                    leaf.Held![s] = StickerItem(page.Stickers[s], seed, level, Stickers.IsUnique(page.Stickers[s].Good) ? Rarity.Unique : Rarity.Normal);
                }
                page.Stickers = new List<Sticker>();
                item.Held![i] = leaf;
            }
            return item;
        }

        // What a page is called, by what it does.
        private static string PageName(Page page)
        {
            Deed deed = Sentences.Find(page.Outcomes.FirstOrDefault()?.Deed)?.Deed ?? Deed.Deal;
            string what = deed switch
            {
                Deed.Deal => page.Hand.ToString(),
                Deed.Raise => "Raising",
                Deed.Move => "Blinking",
                _ => "",
            };
            return $"{what} {page.Kind} Page".TrimStart();
        }

        /// <summary>
        /// Something that goes in a book, or a book. What it can hold is what it's worth, so the better it is the more places it has, and a sticker the better it rolls. What a page says is found with it and never changes.
        /// Only a sticker can be Unique, for something on it nothing else can have, and what's found as Unique is a sticker or nothing: see <see cref="Loot"/>.
        /// </summary>
        /// <param name="only">Which of the three it's to be, for what has to have one in particular. It's drawn for all the same, so nothing else about it changes.</param>
        public static Item Folio(int level, WorldRandom random, Rarity rarity, ItemSlot? only = null)
        {
            ulong seed = NewSeed(random);
            if (rarity == Rarity.Unique) return StickerItem(Stickers.Roll(rarity, random), seed, level, rarity);
            int table = (int)rarity;

            float kind = random.Float();
            if (only is ItemSlot wanted) kind = wanted == ItemSlot.Sticker ? 0f : wanted == ItemSlot.Page ? StickerShare : 1f;
            if (kind < StickerShare) return StickerItem(Stickers.Roll(rarity, random), seed, level, rarity);
            if (kind < StickerShare + PageShare)
            {
                Page page = Scribe.Roll(random.Fork("page"));
                return Leaf(page, PlacesOf(LeastStickers[table], MostStickers[table], random), $"{FolioWords[table]} {PageName(page)}", seed, level, rarity);
            }

            float mode = random.Float();
            BookMode bookMode = mode < TriggerShare ? BookMode.Trigger : mode < TriggerShare + ToggleShare ? BookMode.Toggle : BookMode.Active;
            string word = bookMode == BookMode.Active ? "" : $"{bookMode} ";
            Item book = EmptyBook(PlacesOf(LeastPages[table], MostPages[table], random), $"{FolioWords[table]} {word}Book", seed, level, rarity);
            book.Mode = bookMode;
            if (bookMode == BookMode.Trigger) book.When = BookTriggers.Roll(rarity, random);
            return book;
        }

        // The lower of two draws, as all the places there can be is what's worth having, and shouldn't turn up as often as the fewest.
        private static int PlacesOf(int least, int most, WorldRandom random) => Math.Min(random.Int(least, most), random.Int(least, most));

        private const int StarterPlaces = 2;
        private const int StarterStickers = 1;
        private const float StickerShare = 0.55f;   // Of what's found that isn't worn. What's left after pages is books.
        private const float PageShare = 0.33f;
        private const float TriggerShare = 0.3f;    // Of books. What's left after toggles is pressed by hand.
        private const float ToggleShare = 0.2f;

        // By rarity, from normal to rare. A book of more places than a page has stickers is the rarer thing, as it's worth more.
        private static readonly int[] LeastPages = { 1, 2, 3 };
        private static readonly int[] MostPages = { 2, 2, 6 };
        private static readonly int[] LeastStickers = { 1, 2, 3 };
        private static readonly int[] MostStickers = { 1, 2, 5 };
        private static readonly string[] FolioWords = { "Plain", "Fine", "Gilded" };
        private static readonly string[] OldFolioWords = { "Masterful", "Fabled" };

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
            if (roll < 0.012f) return Rarity.Unique;
            if (roll < 0.11f) return Rarity.Rare;
            if (roll < 0.42f) return Rarity.Magic;
            return Rarity.Normal;
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

        private static Affix? PickAffix(ItemSlot slot, WeaponKind kind, bool prefix, int level, List<ItemLine> taken, in FamilyShares shares, WorldRandom random)
        {
            var options = new List<Affix>();
            foreach (Affix affix in AffixCatalog.All)
            {
                if (affix.Prefix != prefix || !affix.Fits(slot, kind) || taken.Any(line => line.Affix == affix.Id)) continue;
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

        // What it's called comes from the first of each kind, by the tier its roll reached.
        private static string Name(string baseName, List<ItemLine> lines)
        {
            string prefix = "";
            string suffix = "";
            foreach (ItemLine line in lines)
            {
                if (AffixCatalog.Find(line.Affix) is not Affix affix) continue;
                string word = affix.Names[Math.Min(TierOf(affix, line.Value), affix.Names.Length - 1)];
                if (affix.Prefix && prefix.Length == 0) prefix = word + " ";
                if (!affix.Prefix && suffix.Length == 0) suffix = " " + word;
            }
            return prefix + baseName + suffix;
        }

        private static int TierOf(Affix affix, float value)
        {
            int tier = 0;
            for (int i = 0; i < affix.Tiers.Length; i++)
            {
                if (affix.Tiers[i].Low <= value) tier = i;
            }
            return tier;
        }
    }
}

using System.Runtime.CompilerServices;

namespace Dino_Defenders.Stats
{
    /// <summary>The kinds of harm there are. Each has a resistance of its own, and an ailment of its own.</summary>
    public enum DamageType : byte
    {
        Physical,
        Fire,
        Cold,
        Lightning,
        Magic,
        Poison,
    }

    /// <summary>
    /// What something is. A skill carries these, and so does the damage it deals; a modifier names the ones it cares about, and applies
    /// wherever all of them are present. "Increased fire spell damage" is Damage scoped to Fire and Spell. Damage remembers every type it has
    /// been, so physical damage turned to fire is helped by both.
    /// </summary>
    [Flags]
    public enum Tags : ulong
    {
        None = 0,

        // The damage types, in the order of DamageType.
        Physical = 1ul << 0,
        Fire = 1ul << 1,
        Cold = 1ul << 2,
        Lightning = 1ul << 3,
        Magic = 1ul << 4,
        Poison = 1ul << 5,
        Elemental = 1ul << 6,       // Carried by fire, cold and lightning.

        // What kind of action it is.
        Attack = 1ul << 7,
        Spell = 1ul << 8,
        Hit = 1ul << 9,
        DamageOverTime = 1ul << 10,
        Triggered = 1ul << 11,
        Reaction = 1ul << 12,

        // How it gets there.
        Melee = 1ul << 13,
        Projectile = 1ul << 14,
        Area = 1ul << 15,
        Zone = 1ul << 16,
        Chaining = 1ul << 17,
        Movement = 1ul << 18,
        Buff = 1ul << 19,
        Minion = 1ul << 20,

        // What it leaves behind.
        Burn = 1ul << 21,
        Bleed = 1ul << 22,
        Venom = 1ul << 23,
        Chill = 1ul << 24,
        Freeze = 1ul << 25,
        Shock = 1ul << 26,
        Hex = 1ul << 27,
        Stun = 1ul << 28,

        // Where it is in a book, and what's written on it.
        Opening = 1ul << 29,        // The first page.
        Turned = 1ul << 30,         // Any page after that.
        Closing = 1ul << 31,        // The last page.

        // What it's done with.
        Sword = 1ul << 32,
        Axe = 1ul << 33,
        Mace = 1ul << 34,
        Dagger = 1ul << 35,
        Spear = 1ul << 36,
        Bow = 1ul << 37,
        Staff = 1ul << 38,
        Wand = 1ul << 39,
        Unarmed = 1ul << 40,
        OneHanded = 1ul << 41,
        TwoHanded = 1ul << 42,

        Wild = 1ul << 43,           // Something on it is left to chance.
        Falling = 1ul << 44,        // It comes down out of the sky.

        DamageTypes = Physical | Fire | Cold | Lightning | Magic | Poison,
        Ailments = Burn | Bleed | Venom | Chill | Freeze | Shock | Hex | Stun,
        Weapons = Sword | Axe | Mace | Dagger | Spear | Bow | Staff | Wand | Unarmed,
    }

    /// <summary>How much of something, for each type of damage. It's a value, so a hit can carry one without anything being allocated.</summary>
    [InlineArray(DamageKinds.Count)]
    public struct DamageTable
    {
        private float _first;

        public float Total
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < DamageKinds.Count; i++) total += this[i];
                return total;
            }
        }
    }

    public static class DamageKinds
    {
        public const int Count = 6;

        public static readonly DamageType[] All = Enum.GetValues<DamageType>();

        public static Tags Tag(DamageType type) => (Tags)(1ul << (int)type);

        /// <summary>Everything a type of damage counts as.</summary>
        public static Tags TagsOf(DamageType type)
        {
            Tags tag = Tag(type);
            return type is DamageType.Fire or DamageType.Cold or DamageType.Lightning ? tag | Tags.Elemental : tag;
        }

        /// <summary>The bits of a set of damage types, which is what a damage flow remembers about where it has been.</summary>
        public static Tags TagsOf(int lineage)
        {
            Tags tags = (Tags)(uint)lineage;
            return (tags & (Tags.Fire | Tags.Cold | Tags.Lightning)) != 0 ? tags | Tags.Elemental : tags;
        }

        public static string Name(DamageType type) => type.ToString();
    }

    /// <summary>
    /// What can be true of someone at a moment. Modifiers can ask for any of these of the one who has them, or of whoever they're hitting.
    /// What's true of the land under someone's feet is here like anything else, which is what lets a build be made for the place it fights in.
    /// </summary>
    [Flags]
    public enum Condition : ulong
    {
        None = 0,

        FullHealth = 1ul << 0,
        LowHealth = 1ul << 1,
        FullMana = 1ul << 2,
        LowMana = 1ul << 3,
        HasBarrier = 1ul << 4,
        NoBarrier = 1ul << 5,

        Moving = 1ul << 6,
        Stationary = 1ul << 7,

        RecentlyHit = 1ul << 8,         // Took a hit in the last few seconds.
        RecentlyKilled = 1ul << 9,
        RecentlyCrit = 1ul << 10,
        RecentlyBlocked = 1ul << 11,
        RecentlyDodged = 1ul << 12,
        RecentlyUsedBook = 1ul << 13,

        Burning = 1ul << 14,
        Bleeding = 1ul << 15,
        Envenomed = 1ul << 16,
        Chilled = 1ul << 17,
        Frozen = 1ul << 18,
        Shocked = 1ul << 19,
        Hexed = 1ul << 20,
        Stunned = 1ul << 21,

        InWater = 1ul << 22,
        UnderTrees = 1ul << 23,
        OnRoad = 1ul << 24,
        InDarkness = 1ul << 25,
        HotLand = 1ul << 26,
        ColdLand = 1ul << 27,
        WetLand = 1ul << 28,
        HighLand = 1ul << 29,

        Wounded = 1ul << 30,            // Not at full health.
        Overdrawn = 1ul << 31,          // Mana spent below nothing.

        Rare = 1ul << 32,
        Boss = 1ul << 33,

        HoldingShield = 1ul << 34,
        TwoHanded = 1ul << 35,
        Unarmed = 1ul << 36,

        // Who's around, as only a hero counts it.
        Surrounded = 1ul << 37,
        Alone = 1ul << 38,

        // What the sky is doing, which is the same on every machine.
        InRain = 1ul << 39,
        InSnow = 1ul << 40,

        // What's true between whoever's hitting and whoever's hit, worked out by the blow and only for those who ask.
        Marked = 1ul << 41,
        Unmarked = 1ul << 42,
        Unaware = 1ul << 43,            // Hasn't noticed anyone yet.
        Aware = 1ul << 44,
        UpClose = 1ul << 45,
        AtRange = 1ul << 46,
        Isolated = 1ul << 47,           // Nobody else on its side near it.
        Crowded = 1ul << 48,

        DualWielding = 1ul << 49,       // A One Handed Weapon in each hand.

        /// <summary>What the land decides. Worth looking up only for those who care.</summary>
        Land = InWater | UnderTrees | OnRoad | InDarkness | HotLand | ColdLand | WetLand | HighLand,

        /// <summary>What the weather decides, which costs more to ask than the land does.</summary>
        Weather = InRain | InSnow,

        /// <summary>What a blow works out about the two of them.</summary>
        Relations = Marked | Unmarked | Unaware | Aware | UpClose | AtRange | Isolated | Crowded,

        /// <summary>Any ailment at all.</summary>
        Afflicted = Burning | Bleeding | Envenomed | Chilled | Frozen | Shocked | Hexed | Stunned,
    }

    /// <summary>
    /// A number something else can be measured against: "1% more damage per 100 armour", "per 500 gold carried". The first group are the
    /// actor's own totals. The rest are counters, which change during play.
    /// </summary>
    public enum Per : byte
    {
        None,

        Armour,
        Evasion,
        MaxHealth,
        MaxMana,
        MaxBarrier,
        MoveSpeedAboveBase,     // Percent.
        Level,
        Strength,
        Dexterity,
        Intellect,

        // Counters.
        Gold,
        ShrinesKindled,
        RecentKills,
        HealthSpent,            // Percent of maximum health, lately.
        Minions,
        AlliesNear,
        EnemiesNear,
        Ailments,               // On you, granted ones included.
        EmptySlots,             // Places on a hero with nothing worn there.
        NormalWorn,
        UniquesWorn,
        Carried,                // Things in the pack.
        LandLevel,              // Of the land you're in.
        Omens,                  // On the land you're in.
        RaresSlain,             // Rare enemies and bosses, since you last fell.
        FightSeconds,           // Spent fighting without a break.
        Streak,                 // The same book used by hand, again and again.
        Variety,                // Different books used by hand Recently.
    }

    /// <summary>Where counting ends. A new counter goes on the end of <see cref="Per"/>, and this moves with it.</summary>
    public static class Counting
    {
        public const Per Last = Per.Variety;
    }

    /// <summary>The counters a sheet can read: what changes during play, as it stood when the sheet was made. Compared whole, to know when to make a new one.</summary>
    public struct Counters : IEquatable<Counters>
    {
        public int Gold;
        public byte ShrinesKindled;
        public byte RecentKills;
        public byte HealthSpent;
        public byte Minions;
        public byte AlliesNear;
        public byte EnemiesNear;
        public byte Ailments;
        public byte EmptySlots;
        public byte NormalWorn;
        public byte UniquesWorn;
        public byte Carried;
        public byte LandLevel;
        public byte Omens;
        public byte RaresSlain;
        public byte FightSeconds;
        public byte Streak;
        public byte Variety;

        public readonly bool Equals(Counters other)
        {
            return Gold == other.Gold && ShrinesKindled == other.ShrinesKindled && RecentKills == other.RecentKills
                && HealthSpent == other.HealthSpent && Minions == other.Minions && AlliesNear == other.AlliesNear
                && EnemiesNear == other.EnemiesNear && Ailments == other.Ailments && EmptySlots == other.EmptySlots && NormalWorn == other.NormalWorn
                && UniquesWorn == other.UniquesWorn && Carried == other.Carried && LandLevel == other.LandLevel && Omens == other.Omens
                && RaresSlain == other.RaresSlain && FightSeconds == other.FightSeconds && Streak == other.Streak && Variety == other.Variety;
        }

        public override readonly bool Equals(object? obj) => obj is Counters other && Equals(other);

        public override readonly int GetHashCode()
        {
            var hash = new HashCode();
            for (Per per = Per.Gold; per <= Counting.Last; per++) hash.Add(Read(per));
            return hash.ToHashCode();
        }

        public readonly int Read(Per per) => per switch
        {
            Per.Gold => Gold,
            Per.ShrinesKindled => ShrinesKindled,
            Per.RecentKills => RecentKills,
            Per.HealthSpent => HealthSpent,
            Per.Minions => Minions,
            Per.AlliesNear => AlliesNear,
            Per.EnemiesNear => EnemiesNear,
            Per.Ailments => Ailments,
            Per.EmptySlots => EmptySlots,
            Per.NormalWorn => NormalWorn,
            Per.UniquesWorn => UniquesWorn,
            Per.Carried => Carried,
            Per.LandLevel => LandLevel,
            Per.Omens => Omens,
            Per.RaresSlain => RaresSlain,
            Per.FightSeconds => FightSeconds,
            Per.Streak => Streak,
            Per.Variety => Variety,
            _ => 0,
        };

        /// <summary>A bit for a counter, among those a loadout reads.</summary>
        public static uint Bit(Per per) => per >= Per.Gold ? 1u << (per - Per.Gold) : 0u;

        /// <summary>Only what's been asked for, the rest nothing: a hero who reads their gold shouldn't need a new sheet whenever an enemy comes near.</summary>
        public readonly Counters Only(uint read)
        {
            Counters only = default;
            if ((read & Bit(Per.Gold)) != 0) only.Gold = Gold;
            if ((read & Bit(Per.ShrinesKindled)) != 0) only.ShrinesKindled = ShrinesKindled;
            if ((read & Bit(Per.RecentKills)) != 0) only.RecentKills = RecentKills;
            if ((read & Bit(Per.HealthSpent)) != 0) only.HealthSpent = HealthSpent;
            if ((read & Bit(Per.Minions)) != 0) only.Minions = Minions;
            if ((read & Bit(Per.AlliesNear)) != 0) only.AlliesNear = AlliesNear;
            if ((read & Bit(Per.EnemiesNear)) != 0) only.EnemiesNear = EnemiesNear;
            if ((read & Bit(Per.Ailments)) != 0) only.Ailments = Ailments;
            if ((read & Bit(Per.EmptySlots)) != 0) only.EmptySlots = EmptySlots;
            if ((read & Bit(Per.NormalWorn)) != 0) only.NormalWorn = NormalWorn;
            if ((read & Bit(Per.UniquesWorn)) != 0) only.UniquesWorn = UniquesWorn;
            if ((read & Bit(Per.Carried)) != 0) only.Carried = Carried;
            if ((read & Bit(Per.LandLevel)) != 0) only.LandLevel = LandLevel;
            if ((read & Bit(Per.Omens)) != 0) only.Omens = Omens;
            if ((read & Bit(Per.RaresSlain)) != 0) only.RaresSlain = RaresSlain;
            if ((read & Bit(Per.FightSeconds)) != 0) only.FightSeconds = FightSeconds;
            if ((read & Bit(Per.Streak)) != 0) only.Streak = Streak;
            if ((read & Bit(Per.Variety)) != 0) only.Variety = Variety;
            return only;
        }

        public readonly void Write(Dino_Engine.Networking.BitWriter writer)
        {
            writer.WriteVarInt(Gold);
            writer.WriteByte(ShrinesKindled);
            writer.WriteByte(RecentKills);
            writer.WriteByte(HealthSpent);
            writer.WriteByte(Minions);
            writer.WriteByte(AlliesNear);
            writer.WriteByte(EnemiesNear);
            writer.WriteByte(Ailments);
            writer.WriteByte(EmptySlots);
            writer.WriteByte(NormalWorn);
            writer.WriteByte(UniquesWorn);
            writer.WriteByte(Carried);
            writer.WriteByte(LandLevel);
            writer.WriteByte(Omens);
            writer.WriteByte(RaresSlain);
            writer.WriteByte(FightSeconds);
            writer.WriteByte(Streak);
            writer.WriteByte(Variety);
        }

        public static Counters Read(Dino_Engine.Networking.BitReader reader) => new Counters
        {
            Gold = reader.ReadVarInt(),
            ShrinesKindled = reader.ReadByte(),
            RecentKills = reader.ReadByte(),
            HealthSpent = reader.ReadByte(),
            Minions = reader.ReadByte(),
            AlliesNear = reader.ReadByte(),
            EnemiesNear = reader.ReadByte(),
            Ailments = reader.ReadByte(),
            EmptySlots = reader.ReadByte(),
            NormalWorn = reader.ReadByte(),
            UniquesWorn = reader.ReadByte(),
            Carried = reader.ReadByte(),
            LandLevel = reader.ReadByte(),
            Omens = reader.ReadByte(),
            RaresSlain = reader.ReadByte(),
            FightSeconds = reader.ReadByte(),
            Streak = reader.ReadByte(),
            Variety = reader.ReadByte(),
        };
    }
}

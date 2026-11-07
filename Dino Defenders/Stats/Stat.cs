namespace Dino_Defenders.Stats
{
    /// <summary>
    /// Everything a modifier can change. Every one of these is read somewhere, and BuildLab fails if one stops being. A new stat is a line
    /// here, a line in <see cref="Tooltips.StatNames"/> and a term in <see cref="Tooltips.Lexicon"/>, then whatever reads it. Stats take scopes, so one entry covers everything it can be
    /// narrowed to: Damage is also fire damage, spell damage and fire spell damage with staves.
    /// </summary>
    public enum Stat : ushort
    {
        // What gives other stats: see Attributes.
        Strength,
        Dexterity,
        Intellect,

        // The pools.
        Health,
        Mana,
        Barrier,

        // Getting them back.
        HealthRegen,            // A second.
        HealthRegenPercent,     // Of maximum health, a second.
        ManaRegen,
        ManaRegenPercent,
        BarrierRecharge,        // Percent of maximum barrier, a second.
        BarrierDelay,           // Seconds after being hit before it starts.
        Recovery,               // Everything that heals, leeches and regenerates.
        HealthLeech,            // Percent of damage dealt.
        ManaLeech,
        HealthOnHit,
        ManaOnHit,
        HealthOnKill,
        ManaOnKill,
        BarrierOnKill,
        FlaskEffect,

        // Moving.
        MoveSpeed,
        DodgeCooldown,
        DodgeSpeed,

        // What a blow is made of.
        Damage,
        AddedPhysical,
        AddedFire,
        AddedCold,
        AddedLightning,
        AddedMagic,
        AddedPoison,
        Convert,                // From one type to another.
        GainAs,                 // As Convert, but keeping what was there.
        AttackSpeed,
        CastSpeed,
        CritChance,
        CritMultiplier,
        Accuracy,
        Penetration,            // Percentage points of resistance ignored.
        ArmourIgnore,           // Percent of the target's armour ignored.

        // How a page reaches.
        AreaOfEffect,
        MeleeReach,
        ProjectileSpeed,
        ProjectileCount,
        Pierce,
        Chain,
        PageDuration,
        CooldownRecovery,
        BookCost,
        Potency,                // Everything a page does.

        // What it leaves behind.
        AilmentChance,
        AilmentDuration,
        AilmentEffect,
        Execute,                // Percent of maximum health under which a critical hit kills.
        ReactionDamage,
        ReactionArea,

        // Not being hurt.
        Armour,
        ArmourApplies,          // Percent of armour that also counts against a type.
        Evasion,
        BlockChance,
        MaxBlock,
        Resistance,
        MaxResistance,
        PhysicalReduction,      // Percent, added to what armour gives.
        FlatReduction,          // Off each hit.
        DamageTaken,
        TakenAs,                // From one type to another, as it arrives.
        TakenFromMana,          // Percent, paid out of mana before health.
        AilmentAvoid,
        AilmentTaken,           // How long ailments last on you.
        StunThreshold,
        Reflect,                // Percent of melee damage taken, sent back.

        // Company.
        MinionCount,
        MinionDuration,
        RaiseFallen,            // Percent chance an enemy killed rises to fight beside you.

        // Fortune.
        ItemRarity,
        GoldFind,
        ExperienceGain,
        Stealth,                // Percent off how far enemies notice you from.
        ExplodeOnKill,          // Percent of a victim's maximum health, dealt around it as fire.

        Count,
    }

    /// <summary>
    /// Which side of a hero each stat is on. What keeps them alive and going is defensive, and is what a prefix can be: pools and getting them back, not being hurt, and getting
    /// away. What they kill with is offensive, and is what a suffix can be: everything about a blow, how a page reaches, what it leaves behind, the attributes power comes from, and what's got out of a kill.
    /// Every stat is on exactly one of these, which BuildLab holds.
    /// </summary>
    public static class StatSides
    {
        public static readonly Stat[] Defensive =
        {
            Stat.Health, Stat.Mana, Stat.Barrier,
            Stat.HealthRegen, Stat.HealthRegenPercent, Stat.ManaRegen, Stat.ManaRegenPercent, Stat.BarrierRecharge, Stat.BarrierDelay, Stat.Recovery,
            Stat.HealthLeech, Stat.ManaLeech, Stat.HealthOnHit, Stat.ManaOnHit, Stat.HealthOnKill, Stat.ManaOnKill, Stat.BarrierOnKill, Stat.FlaskEffect,
            Stat.MoveSpeed, Stat.DodgeCooldown, Stat.DodgeSpeed, Stat.Stealth,
            Stat.Armour, Stat.ArmourApplies, Stat.Evasion, Stat.BlockChance, Stat.MaxBlock, Stat.Resistance, Stat.MaxResistance, Stat.PhysicalReduction, Stat.FlatReduction,
            Stat.DamageTaken, Stat.TakenAs, Stat.TakenFromMana, Stat.AilmentAvoid, Stat.AilmentTaken, Stat.StunThreshold,
        };

        public static readonly Stat[] Offensive =
        {
            Stat.Strength, Stat.Dexterity, Stat.Intellect,
            Stat.Damage, Stat.AddedPhysical, Stat.AddedFire, Stat.AddedCold, Stat.AddedLightning, Stat.AddedMagic, Stat.AddedPoison, Stat.Convert, Stat.GainAs,
            Stat.AttackSpeed, Stat.CastSpeed, Stat.CritChance, Stat.CritMultiplier, Stat.Accuracy, Stat.Penetration, Stat.ArmourIgnore,
            Stat.AreaOfEffect, Stat.MeleeReach, Stat.ProjectileSpeed, Stat.ProjectileCount, Stat.Pierce, Stat.Chain, Stat.PageDuration, Stat.CooldownRecovery, Stat.BookCost, Stat.Potency,
            Stat.AilmentChance, Stat.AilmentDuration, Stat.AilmentEffect, Stat.Execute, Stat.ReactionDamage, Stat.ReactionArea, Stat.Reflect, Stat.ExplodeOnKill,
            Stat.MinionCount, Stat.MinionDuration, Stat.RaiseFallen,
            Stat.ItemRarity, Stat.GoldFind, Stat.ExperienceGain,
        };

        public static bool IsDefensive(Stat stat) => Array.IndexOf(Defensive, stat) >= 0;
    }

    /// <summary>How a modifier acts on a stat.</summary>
    public enum ModKind : byte
    {
        Flat,           // Added to the base: +5 health.
        Increased,      // All of these add up into one pool, then multiply. Below zero it's "reduced".
        More,           // Each is a multiplier of its own. Below zero it's "less".
        Override,       // Sets the stat outright. Where there are several, the lowest stands.
        Rule,           // Changes how something works, rather than a number.
        Grant,          // Makes a condition true.
        Divert,         // Increases and mores of one stat apply to another instead.
        Echo,           // As Divert, but they apply to both.
        Trigger,        // When something happens, a book goes off.
    }

    /// <summary>What can set off a book by itself.</summary>
    public enum TriggerEvent : byte
    {
        Hit,
        Critical,
        Kill,
        Struck,     // You took a hit.
        Block,
        Dodge,
        Flask,
    }

    /// <summary>
    /// The ways a build can change how the game works. Keystones and unique items carry these. Heroes and enemies read them alike, as
    /// anything a hero can be, an enemy can too.
    /// </summary>
    [Flags]
    public enum Rule : ulong
    {
        None = 0,

        CostsHealth = 1ul << 0,             // Books cost health instead of mana.
        DeferredDamage = 1ul << 1,          // Damage taken is paid over time.
        OverhealBarrier = 1ul << 2,         // Healing past full becomes barrier.
        BlockReflects = 1ul << 3,           // A blocked hit goes back at whoever threw it.
        Guardian = 1ul << 4,                // Part of what allies take comes to you.
        Puppeteer = 1ul << 5,               // Your minions have your modifiers; you deal no damage yourself.
        EndlessVenom = 1ul << 6,            // Venom has no limit on stacks.
        Carrier = 1ul << 7,                 // Immune to venom, and it spreads from you.
        AilmentsLeap = 1ul << 8,            // When an enemy dies, its ailments spread.
        FireFeedsBurn = 1ul << 9,           // Fire hits go wholly into Burn.
        BurningTrail = 1ul << 10,           // The ground burns behind you.
        Rebirth = 1ul << 11,                // Death is put off, now and then.
        SpellEcho = 1ul << 12,              // Spells go off twice.
        OneCost = 1ul << 13,                // Only a book's first page has a cost.
        StaticCharge = 1ul << 14,           // Moving builds a charge the next hit spends.
        ChainsGrow = 1ul << 15,             // Chains strengthen as they go.
        FreezeStores = 1ul << 16,           // Damage to the frozen is saved up for when the ice breaks.
        HighGround = 1ul << 17,             // Height over a target is damage.
        LongShot = 1ul << 18,               // Projectiles strengthen with distance.
        ProjectilesReturn = 1ul << 19,      // Projectiles come back.
        Steady = 1ul << 20,                 // What's left to chance isn't, and pays the same.
        DodgeCrit = 1ul << 21,              // A dodge makes the next hit critical.
        WaterTrail = 1ul << 22,             // Spells leave water behind.
        NoLeech = 1ul << 23,
        NoRegen = 1ul << 24,
        NeverCrit = 1ul << 25,
        CannotEvade = 1ul << 26,
        CannotBeStunned = 1ul << 27,
        CannotBeFrozen = 1ul << 28,
        CannotBeChilled = 1ul << 29,
        ImmuneToBurn = 1ul << 30,
        AlwaysShocked = 1ul << 31,
        ChillNeverFades = 1ul << 32,
        FireHeals = 1ul << 33,              // Part of fire damage taken heals instead.
        NoOwnDamage = 1ul << 34,            // Your own hits deal nothing.
        LuckyHits = 1ul << 35,              // Damage is rolled twice, keeping the better.
        BurstsOnDeath = 1ul << 36,          // A moment after dying, you go up in flames.
        RalliesOnDeath = 1ul << 37,         // Your death drives those around you on.
    }
}

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
        HealthDrain,            // Percent of maximum health lost a second, full or not.

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
            Stat.HealthLeech, Stat.ManaLeech, Stat.HealthOnHit, Stat.ManaOnHit, Stat.HealthOnKill, Stat.ManaOnKill, Stat.BarrierOnKill, Stat.FlaskEffect, Stat.HealthDrain,
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

        // Nothing past here is something a Trigger Book can wait for: those are BookWhen, which shares the numbers above.
        Stun,           // You stunned someone.
        Reaction,       // You set off a reaction.
        BarrierBroke,   // Your barrier has just run out.
        Dash,           // You started a dodge.
        MinionDied,     // One of yours has fallen, or its time is up.
        Used,           // You used a book by hand.
        Time,           // Every so many seconds, while what it asks for holds.
        Walked,         // Every so many metres.
    }

    /// <summary>
    /// The ways a build can change how the game works. Keystones and unique items carry these. Heroes and enemies read them alike, as
    /// anything a hero can be, an enemy can too. They're numbers rather than flags, so there's room for as many as there are uniques: a modifier
    /// carries one, and a sheet keeps which hold in a <see cref="RuleSet"/>. None of them is saved or sent, so a new one can go anywhere in the list.
    /// </summary>
    public enum Rule : byte
    {
        None,

        CostsHealth,            // Books cost health instead of mana.
        DeferredDamage,         // Damage taken is paid over time.
        OverhealBarrier,        // Healing past full becomes barrier.
        BlockReflects,          // A blocked hit goes back at whoever threw it.
        Guardian,               // Part of what allies take comes to you.
        Puppeteer,              // Your minions have your modifiers; you deal no damage yourself.
        EndlessVenom,           // Venom has no limit on stacks.
        Carrier,                // Immune to venom, and it spreads from you.
        AilmentsLeap,           // When an enemy dies, its ailments spread.
        FireFeedsBurn,          // Fire hits go wholly into Burn.
        BurningTrail,           // The ground burns behind you.
        Rebirth,                // Death is put off, now and then.
        SpellEcho,              // Spells go off twice.
        OneCost,                // Only a book's first page has a cost.
        StaticCharge,           // Moving builds a charge the next hit spends.
        ChainsGrow,             // Chains strengthen as they go.
        FreezeStores,           // Damage to the frozen is saved up for when the ice breaks.
        HighGround,             // Height over a target is damage.
        LongShot,               // Projectiles strengthen with distance.
        ProjectilesReturn,      // Projectiles come back.
        Steady,                 // What's left to chance isn't, and pays the same.
        DodgeCrit,              // A dodge makes the next hit critical.
        WaterTrail,             // Spells leave water behind.
        NoLeech,
        NoRegen,
        NeverCrit,
        CannotEvade,
        CannotBeStunned,
        CannotBeFrozen,
        CannotBeChilled,
        ImmuneToBurn,
        AlwaysShocked,
        ChillNeverFades,
        FireHeals,              // Part of fire damage taken heals instead.
        NoOwnDamage,            // Your own hits deal nothing.
        LuckyHits,              // Damage is rolled twice, keeping the better.
        BurstsOnDeath,          // A moment after dying, you go up in flames.
        RalliesOnDeath,         // Your death drives those around you on.

        // What only uniques do. Each is one item's reason to exist, or what it costs.
        NeverHealed,            // Health stops at low.
        Marks,                  // The first enemy hit is marked until it dies.
        BleedBurst,             // What dies bleeding bursts.
        FullCircle,             // Melee cones go all the way round.
        ExecuteAny,             // Any hit can execute, not only a critical one.
        Overkill,               // What's left over after a kill goes on to the next.
        StunSpreads,            // A stun passes to those around.
        Leveller,               // Hits deal a share of the target's maximum health.
        OnlyLevels,             // And nothing else.
        VenomBursts,            // A full stack of venom lands all at once.
        VenomSlows,             // Each stack of venom slows.
        AllOrNothing,           // Hits that aren't critical deal nothing.
        LeechesOverTime,        // Damage over time leeches.
        Drags,                  // Projectiles drag what they hit towards you.
        Lobbed,                 // Projectiles go up and come down where they're aimed.
        Forks,                  // Projectiles split on their first hit.
        PierceGrows,            // A projectile strengthens with each it passes through.
        FullVoice,              // Toggle Books pulse at full strength.
        NoActive,               // Active Books can't be used.
        ManaPower,              // What a book spends in mana it deals as damage.
        SpendsAll,              // Every book spends all the mana there is.
        Choir,                  // Minions use what you use.
        DeepHex,                // Hex goes deeper.
        Mirror,                 // Books also go off the other way.
        RunOn,                  // A finished book starts again, once.
        Overdraw,               // Mana can be spent below nothing.
        Conducts,               // Conduction runs through the shocked as through water.
        ChainsBack,             // Chains can go back to whoever they came from before.
        SpellsTwice,            // Spells hit everything twice.
        DeathTakesAll,          // A fall takes all the gold.
        Retort,                 // What's blocked is kept, and the next hit gives it back.
        Wall,                   // Standing still, nothing that flies gets past.
        BlockFeedsMana,         // Blocked spells are mana.
        ThirdBlock,             // Every third hit is blocked.
        Barricaded,             // No ailments while barrier holds.
        ThornsAlways,           // Thorns answer every hit, not only melee.
        MinionsShoulder,        // Minions take part of what you take.
        GoldAbsorbs,            // Gold pays for part of what you take.
        CostIsTime,             // Books cost rest instead of mana.
        Blind,                  // Enemies' ground marks aren't shown.
        NoRepeat,               // A book used twice running is weaker.
        Remote,                 // Melee lands where you aim.
        LastGasp,               // Death waits a few seconds for a kill.
        AllAsFire,              // Everything arrives as fire.
        MinionShield,           // A minion dies in your place.
        Adapts,                 // You harden to what last hit you.
        HitCap,                 // No hit takes more than a share of your health.
        FlaskNoHeal,            // Flasks heal nothing.
        NoSafeDodge,            // A dodge moves you and protects you from nothing.
        DodgeCostsMana,         // A dodge costs mana.
        NoDodge,                // You can't dodge.
        NoItems,                // What you kill drops no items.
        Fluke,                  // Books sometimes go off twice.
        Fumble,                 // Books sometimes do nothing.
        Wildfire,               // Flashfire spreads venom and leaves it.
        HealsHarm,              // What you heal also hurts what's around.
        EvenResist,             // Elemental resistances are all your best.
        CostsGold,              // Books cost gold instead of mana.
        Exchange,               // Strength and Intellect swap what they give.
        NoCooldowns,            // Books don't rest.
        TimeIsCost,             // Rest is paid for in mana instead.
        QuickTrigger,           // Trigger Books fill faster.
        IceBursts,              // What dies frozen shatters.
        KillResets,             // A kill takes the book that did it off cooldown.
        Lodestone,              // Enemies around are drawn in.
        MinionsBurst,           // Minions burst when they're gone.
        FlockPays,              // Books cost nothing.
        FlockBleeds,            // Each use costs a minion.
        Inoculated,             // No ailments at all.

        Count,
    }

    /// <summary>Which rules hold for someone, one bit each. It's a value with room for every rule, so a sheet can carry one without anything being allocated.</summary>
    [System.Runtime.CompilerServices.InlineArray(Words)]
    public struct RuleSet
    {
        public const int Words = 4;

        private ulong _first;

        public readonly bool Has(Rule rule) => (this[(int)rule >> 6] & 1ul << ((int)rule & 63)) != 0;

        public void Add(Rule rule) => this[(int)rule >> 6] |= 1ul << ((int)rule & 63);
    }
}

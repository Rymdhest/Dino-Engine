using Dino_Defenders.Books;
using Dino_Defenders.Stats;
using static Dino_Defenders.Stats.M;

namespace Dino_Defenders.Talents
{
    /// <summary>The bottom of the tree, opposite the scholar: the smith, the wall and the blood.</summary>
    public static partial class TalentContent
    {
        // Melee, weapons and raw force: the smith, who hits hard and hits everything.
        private static Region Forge() => new Region
        {
            Id = "forge", Name = "The Forge", Adjective = "Iron", Noun = "Smith", Colour = Rgb(190, 192, 202),
            Centre = 180f, Width = 32f, Attribute = Stat.Strength,
            Entry = N("first-blow", "First Blow", Inc(Stat.Damage, 12f, Tags.Melee), Inc(Stat.Damage, 10f, Tags.Physical)),
            Lanes = new[]
            {
                Lane("hearth", null, 0f, 0f, 1100f, -0.5f,
                    P("broad-strokes", "Broad Strokes", Inc(Stat.Damage, 10f, Tags.Melee), Inc(Stat.AreaOfEffect, 15f, Tags.Melee), Inc(Stat.Damage, 12f, Tags.Melee)),
                    P2("heavy-hands", "Heavy Hands", Inc(Stat.Damage, 10f, Tags.Physical), Inc(Stat.Damage, 20f, Tags.Physical), Chance(Tags.Stun, 6f)),
                    P("battle-hardened", "Battle Hardened", Flat(Stat.Health, 12f), Inc(Stat.Health, 8f), Inc(Stat.Armour, 15f)),
                    P2("forged-in-fire", "Forged in Fire", Resist(Tags.Fire, 6f), Resist(Tags.Fire, 15f), Inc(Stat.Armour, 15f))),
                Lane("edge", "hearth", -8f, 1100f, 2350f, -1f,
                    P("whetstone", "Whetstone", Inc(Stat.Damage, 10f, Tags.Sword), Inc(Stat.Damage, 25f, Tags.Sword), Flat(Stat.CritChance, 1f, Tags.Sword)),
                    P("cleaver", "Cleaver", Inc(Stat.Damage, 10f, Tags.Axe), Inc(Stat.Damage, 25f, Tags.Axe), Chance(Tags.Bleed, 8f, Tags.Axe)),
                    P2("keen-edge", "Keen Edge", Inc(Stat.CritChance, 12f, Tags.Melee), Inc(Stat.CritChance, 30f, Tags.Melee), Flat(Stat.CritMultiplier, 15f, Tags.Melee)),
                    P("momentum", "Momentum", Inc(Stat.AttackSpeed, 3f, Tags.Melee), Inc(Stat.AttackSpeed, 10f, Tags.Melee), Inc(Stat.Damage, 10f, Tags.Melee)),
                    P("sundering", "Sundering", Inc(Stat.Damage, 10f, Tags.Physical), Flat(Stat.ArmourIgnore, 12f), Inc(Stat.Damage, 12f, Tags.Physical)),
                    P("bladework", "Bladework", Inc(Stat.Damage, 10f, Tags.Melee), Inc(Stat.Damage, 20f, Tags.Melee), Inc(Stat.Accuracy, 15f))),
                Lane("hammer", "hearth", 6f, 1100f, 2300f, 0.5f,
                    P("hammerfall", "Hammerfall", Inc(Stat.Damage, 10f, Tags.Mace), Inc(Stat.Damage, 28f, Tags.Mace), Chance(Tags.Stun, 10f, Tags.Mace)),
                    P("both-hands", "Both Hands", Inc(Stat.Damage, 10f, Tags.TwoHanded), Inc(Stat.Damage, 28f, Tags.TwoHanded), Inc(Stat.AttackSpeed, 6f, Tags.TwoHanded)),
                    P2("crushing-blows", "Crushing Blows", Chance(Tags.Stun, 3f), Chance(Tags.Stun, 10f), Inc(Stat.Damage, 20f).Against(Condition.Stunned)),
                    P("long-arms", "Long Arms", Flat(Stat.MeleeReach, 0.2f), Flat(Stat.MeleeReach, 1f), Inc(Stat.AreaOfEffect, 12f, Tags.Melee)),
                    P4("titans-grip", "Titan's Grip", Inc(Stat.Damage, 10f, Tags.TwoHanded), Inc(Stat.Damage, 1f, Tags.Melee).For(Per.Strength, 10f, 40f), Flat(Stat.Strength, 15f)),
                    P("skullcracker", "Skullcracker", Inc(Stat.Damage, 10f, Tags.Mace), Inc(Stat.Damage, 20f, Tags.Mace), Inc(Stat.Damage, 15f).Against(Condition.Stunned))),
                Lane("fury", "edge", -7f, 2350f, 3400f, -1f,
                    P("sweeping-arc", "Sweeping Arc", Inc(Stat.AreaOfEffect, 5f, Tags.Melee), Inc(Stat.Damage, 20f, Tags.Melee | Tags.Area), Flat(Stat.MeleeReach, 0.6f)),
                    P("final-blow", "Final Blow", Inc(Stat.Potency, 4f, Tags.Closing), Inc(Stat.Potency, 15f, Tags.Closing), Inc(Stat.Damage, 12f, Tags.Melee)),
                    P2("war-cry", "War Cry", Inc(Stat.Damage, 6f), Inc(Stat.Damage, 20f).While(Condition.Surrounded), Inc(Stat.Armour, 25f).While(Condition.Surrounded)),
                    P("berserk", "Berserk", Inc(Stat.AttackSpeed, 3f), Inc(Stat.AttackSpeed, 15f).While(Condition.LowHealth), Inc(Stat.Damage, 20f).While(Condition.LowHealth)),
                    P("cleave", "Cleave", Inc(Stat.Damage, 10f, Tags.Melee), Inc(Stat.Damage, 20f, Tags.Melee), Inc(Stat.AreaOfEffect, 10f, Tags.Melee))),
                Lane("rampage", "edge", 1f, 2350f, 3300f, 0.5f,
                    P("bloodlust", "Bloodlust", Inc(Stat.Damage, 6f), Inc(Stat.Damage, 20f).While(Condition.RecentlyKilled), Inc(Stat.AttackSpeed, 6f).While(Condition.RecentlyKilled)),
                    P("rending", "Rending", Chance(Tags.Bleed, 3f, Tags.Melee), Chance(Tags.Bleed, 10f, Tags.Melee), Inc(Stat.Damage, 20f, Tags.Bleed)),
                    P2("warpath", "Warpath", Inc(Stat.MoveSpeed, 2f), Inc(Stat.MoveSpeed, 8f), Inc(Stat.Damage, 10f, Tags.Melee)),
                    P("iron-will", "Iron Will", Inc(Stat.StunThreshold, 10f), Inc(Stat.StunThreshold, 30f), Flat(Stat.AilmentAvoid, 10f)),
                    P("headsman", "Headsman", Inc(Stat.Damage, 10f, Tags.TwoHanded), Inc(Stat.Damage, 25f, Tags.TwoHanded).Against(Condition.LowHealth), Flat(Stat.Execute, 3f))),
                Lane("ironwork", "hammer", 5f, 2300f, 3300f, 0.5f,
                    P("tempered", "Tempered", Inc(Stat.Armour, 10f), Inc(Stat.Armour, 25f), Flat(Stat.PhysicalReduction, 3f)),
                    P2("brute-force", "Brute Force", Inc(Stat.Damage, 10f, Tags.Physical), Inc(Stat.Damage, 25f, Tags.Physical), Flat(Stat.Strength, 12f)),
                    P("shieldbreaker", "Shieldbreaker", Inc(Stat.Damage, 10f, Tags.Physical), Flat(Stat.ArmourIgnore, 15f), Inc(Stat.Damage, 15f).Against(Condition.Rare)),
                    P("unstoppable", "Unstoppable", Inc(Stat.StunThreshold, 10f), Inc(Stat.StunThreshold, 40f), Inc(Stat.MoveSpeed, 5f)),
                    P("ironclad-fury", "Ironclad Fury", Inc(Stat.Armour, 10f), Inc(Stat.Damage, 1f, Tags.Melee).For(Per.Armour, 100f, 30f), Inc(Stat.Armour, 15f))),
            },
            Wheels = new[]
            {
                Wheel("anvil", "The Anvil", 1780f, 181f,
                    new[] { Flat(Stat.ArmourIgnore, 2f), Inc(Stat.Damage, 8f, Tags.Physical) },
                    Gives(Flat(Stat.ArmourIgnore, 15f), Inc(Stat.Damage, 25f, Tags.Physical), Chance(Tags.Stun, 8f)),
                    N("molten-core", "Molten Core", Convert(DamageType.Physical, DamageType.Fire, 20f, Tags.Melee), Inc(Stat.Damage, 12f, Tags.Fire)),
                    N("steel-resolve", "Steel Resolve", Inc(Stat.Armour, 25f), Inc(Stat.StunThreshold, 25f)),
                    N("bone-breaker", "Bone Breaker", Inc(Stat.Damage, 20f, Tags.Physical).Against(Condition.Stunned), Chance(Tags.Stun, 6f))),
                Wheel("whirlwind", "The Whirlwind", 2900f, 190f,
                    new[] { Inc(Stat.AreaOfEffect, 4f, Tags.Melee), Inc(Stat.AttackSpeed, 2f, Tags.Melee) },
                    Gives(Inc(Stat.AreaOfEffect, 15f, Tags.Melee), Inc(Stat.AttackSpeed, 8f, Tags.Melee), Inc(Stat.Damage, 15f, Tags.Melee)),
                    N("cyclone", "Cyclone", Inc(Stat.AreaOfEffect, 15f, Tags.Melee), Flat(Stat.MeleeReach, 0.5f)),
                    N("relentless", "Relentless", Inc(Stat.AttackSpeed, 10f, Tags.Melee), Inc(Stat.Damage, 10f, Tags.Melee)),
                    N("crowd-control", "Crowd Control", Inc(Stat.Damage, 20f, Tags.Melee).While(Condition.Surrounded), Inc(Stat.Armour, 15f))),
            },
            Keystones = new[]
            {
                Key("deliberate-hand", "Deliberate Hand", "Executioner", TalentGlyph.Hand, 3500f, 172f, "There's no hurry. There never was.",
                    Divert(Stat.AttackSpeed, Stat.Damage, 200f, Tags.Attack), Inc(Stat.Damage, 20f, Tags.Attack)),
                Key("shatterpoint", "Shatterpoint", null, TalentGlyph.Crack, 3440f, 186.5f, "Everything has a place where it comes apart.",
                    More(Stat.Damage, 50f).Against(Condition.Stunned), Chance(Tags.Stun, 25f, Tags.Melee), Rules(Rule.NeverCrit)),
                Key("earthshaker", "Earthshaker", "Earthshaker", TalentGlyph.Quake, 2660f, 179.5f, "You don't hit one of them. You hit the ground they stand on.",
                    Rules(Rule.MeleeSplash), More(Stat.AreaOfEffect, -50f, Tags.Melee)),
                Key("last-chapter", "The Last Chapter", "Author", TalentGlyph.Book, 3080f, 179f, "It was always going to end like this.",
                    More(Stat.Potency, 60f, Tags.Closing), More(Stat.Potency, -30f, Tags.Opening)),
            },
        };

        // Armour, shields and staying put: the warden, the wall the rest stand behind.
        private static Region Bulwark() => new Region
        {
            Id = "bulwark", Name = "The Bulwark", Adjective = "Stalwart", Noun = "Warden", Colour = Rgb(212, 172, 100),
            Centre = 210f, Width = 28f, Attribute = Stat.Strength,
            Entry = N("first-stand", "First Stand", Flat(Stat.BlockChance, 3f), Inc(Stat.Armour, 15f)),
            Lanes = new[]
            {
                Lane("wall", null, 0f, 0f, 1300f, 0.5f,
                    P("shield-arm", "Shield Arm", Flat(Stat.BlockChance, 1.5f), Flat(Stat.BlockChance, 5f), Inc(Stat.Damage, 15f, Tags.Melee).While(Condition.HoldingShield)),
                    P2("thick-skin", "Thick Skin", Flat(Stat.FlatReduction, 1f), Flat(Stat.FlatReduction, 4f), Inc(Stat.Health, 6f)),
                    P("stalwart", "Stalwart", Inc(Stat.Health, 5f), Inc(Stat.Health, 12f), Flat(Stat.HealthRegen, 3f)),
                    P2("iron-heart", "Iron Heart", Inc(Stat.Health, 5f), Inc(Stat.Health, 10f), Inc(Stat.Armour, 15f))),
                Lane("shield", "wall", -6f, 1300f, 2350f, -0.5f,
                    P("shield-wall", "Shield Wall", Flat(Stat.BlockChance, 1.5f), Flat(Stat.BlockChance, 6f), Inc(Stat.Armour, 25f).While(Condition.HoldingShield)),
                    P2("deflection", "Deflection", Flat(Stat.BlockChance, 1.5f, Tags.Spell), Flat(Stat.BlockChance, 6f, Tags.Spell), Flat(Stat.MaxBlock, 3f)),
                    P("counterstroke", "Counterstroke", Inc(Stat.Damage, 10f, Tags.Attack), Trigger(TriggerEvent.Block, Shelf.Retaliate), Inc(Stat.Damage, 20f).While(Condition.RecentlyBlocked)),
                    P("shield-bash", "Shield Bash", Inc(Stat.Damage, 10f, Tags.Melee), Chance(Tags.Stun, 10f).While(Condition.HoldingShield), Inc(Stat.Damage, 15f, Tags.Melee).While(Condition.HoldingShield)),
                    P("unbreakable-guard", "Unbreakable Guard", Flat(Stat.BlockChance, 1.5f), Flat(Stat.MaxBlock, 4f), Flat(Stat.BlockChance, 5f)),
                    P("tower-shield", "Tower Shield", Flat(Stat.BlockChance, 1.5f), Flat(Stat.BlockChance, 4f), Flat(Stat.Armour, 40f).While(Condition.HoldingShield))),
                Lane("rampart", "wall", 6f, 1300f, 2300f, 0.5f,
                    P("bedrock", "Bedrock", Inc(Stat.Armour, 10f), Inc(Stat.Armour, 25f), Inc(Stat.PageDuration, 15f, Tags.Buff)),
                    P2("iron-skin", "Iron Skin", Flat(Stat.Armour, 30f), Flat(Stat.Armour, 80f), Flat(Stat.PhysicalReduction, 2f)),
                    P("vigour", "Vigour", Inc(Stat.Health, 5f), Inc(Stat.Health, 15f), Inc(Stat.Recovery, 8f)),
                    P("unmoved", "Unmoved", Inc(Stat.StunThreshold, 10f), Inc(Stat.StunThreshold, 40f), Flat(Stat.AilmentAvoid, 15f, Tags.Stun)),
                    P4("fortitude", "Fortitude", Flat(Stat.Health, 12f), Inc(Stat.Health, 10f), Flat(Stat.HealthRegenPercent, 0.5f)),
                    P("like-a-mountain", "Like a Mountain", Inc(Stat.Armour, 10f), Inc(Stat.Armour, 20f), Inc(Stat.StunThreshold, 25f))),
                Lane("bastion", "shield", -5f, 2350f, 3330f, -0.5f,
                    P("bastion-ward", "Bastion", Resist(Tags.Elemental, 3f), Resist(Tags.Elemental, 12f), Flat(Stat.MaxResistance, 1f, Tags.Elemental)),
                    P2("fireproof", "Fireproof", Resist(Tags.Fire, 6f), Resist(Tags.Fire, 20f), Flat(Stat.MaxResistance, 2f, Tags.Fire)),
                    P2("frostproof", "Frostproof", Resist(Tags.Cold, 6f), Resist(Tags.Cold, 20f), Flat(Stat.MaxResistance, 2f, Tags.Cold)),
                    P2("stormproof", "Stormproof", Resist(Tags.Lightning, 6f), Resist(Tags.Lightning, 20f), Flat(Stat.MaxResistance, 2f, Tags.Lightning)),
                    P("warded-plate", "Warded Plate", Inc(Stat.Armour, 10f), Flat(Stat.ArmourApplies, 15f, Tags.Elemental), Inc(Stat.Armour, 15f))),
                Lane("gatehouse", "shield", 1f, 2350f, 3300f, 0f,
                    P("portcullis", "Portcullis", Flat(Stat.BlockChance, 1.5f), Flat(Stat.BlockChance, 5f), Flat(Stat.FlatReduction, 3f)),
                    P("spiked-shield", "Spiked Shield", Flat(Stat.Reflect, 3f), Flat(Stat.Reflect, 12f).While(Condition.HoldingShield), Inc(Stat.Damage, 15f, Tags.Melee)),
                    P2("rallying-cry", "Rallying Cry", Inc(Stat.PageDuration, 6f, Tags.Buff), Inc(Stat.PageDuration, 20f, Tags.Buff), Inc(Stat.Health, 6f)),
                    P("ward-stones", "Ward Stones", Resist(Tags.Elemental, 3f), Resist(Tags.Elemental, 10f), Inc(Stat.Armour, 15f)),
                    P("immovable", "Immovable", Inc(Stat.StunThreshold, 10f), Flat(Stat.AilmentAvoid, 30f, Tags.Stun), Inc(Stat.DamageTaken, -4f))),
                Lane("keep", "rampart", 5f, 2300f, 3380f, 0.5f,
                    P("hold-the-line", "Hold the Line", Inc(Stat.Armour, 10f), Inc(Stat.DamageTaken, -6f).While(Condition.Stationary), Inc(Stat.Armour, 30f).While(Condition.Stationary)),
                    P2("briar-coat", "Briar Coat", Flat(Stat.Reflect, 3f), Flat(Stat.Reflect, 15f), Flat(Stat.Armour, 40f)),
                    P("sentinel", "Sentinel", Inc(Stat.Health, 5f), Inc(Stat.Health, 10f), Inc(Stat.DamageTaken, -5f).While(Condition.Surrounded)),
                    P("second-wind", "Second Wind", Inc(Stat.Recovery, 5f), Inc(Stat.Recovery, 20f).While(Condition.LowHealth), Flat(Stat.HealthRegenPercent, 1f).While(Condition.LowHealth)),
                    P("garrison", "Garrison", Flat(Stat.Armour, 30f), Inc(Stat.Armour, 20f), Flat(Stat.FlatReduction, 3f))),
            },
            Wheels = new[]
            {
                Wheel("citadel", "The Citadel", 1650f, 211f,
                    new[] { Flat(Stat.MaxResistance, 1f, Tags.Elemental), Resist(Tags.Elemental, 4f) },
                    Gives(Flat(Stat.MaxResistance, 3f, Tags.Elemental), Resist(Tags.Elemental, 15f), Inc(Stat.Health, 8f)),
                    N("steadfast", "Steadfast", Flat(Stat.MaxResistance, 2f, Tags.Fire), Flat(Stat.MaxResistance, 2f, Tags.Cold), Flat(Stat.MaxResistance, 2f, Tags.Lightning)),
                    N("weatherproof", "Weatherproof", Resist(Tags.Elemental, 12f), Flat(Stat.AilmentAvoid, 15f)),
                    N("pure-will", "Pure Will", Resist(Tags.Magic, 15f), Resist(Tags.Poison, 15f))),
            },
            Keystones = new[]
            {
                Key("living-anvil", "Living Anvil", "Anvil", TalentGlyph.Anvil, 3480f, 204f, "Everything that's struck you has left you harder.",
                    Flat(Stat.ArmourApplies, 50f, Tags.Fire), Flat(Stat.ArmourApplies, 50f, Tags.Cold), Flat(Stat.ArmourApplies, 50f, Tags.Lightning), Inc(Stat.Armour, 20f), Rules(Rule.CannotEvade)),
                Key("turn-the-blade", "Turn the Blade", "Mirror", TalentGlyph.Mirror, 2650f, 210f, "It was never aimed at you, not really.",
                    Rules(Rule.BlockReflects), Flat(Stat.BlockChance, 10f), Flat(Stat.MaxBlock, -25f)),
                Key("rooted", "Rooted", null, TalentGlyph.Roots, 3540f, 215.5f, "The ground has never lost a fight.",
                    More(Stat.DamageTaken, -40f).While(Condition.Stationary), Flat(Stat.HealthRegenPercent, 3f).While(Condition.Stationary), More(Stat.DamageTaken, 25f).While(Condition.Moving)),
                Key("shared-burden", "Shared Burden", "Shieldbearer", TalentGlyph.Aegis, 3060f, 210f, "They came with you. That was the agreement.",
                    Rules(Rule.Guardian), Inc(Stat.Health, 15f)),
            },
        };

        // Life, leech and bleeding: the sanguine, who gives blood and takes more back.
        private static Region Marrow() => new Region
        {
            Id = "marrow", Name = "The Marrow", Adjective = "Crimson", Noun = "Sanguine", Colour = Rgb(196, 40, 44),
            Centre = 237f, Width = 26f, Attribute = Stat.Strength,
            Entry = N("first-pulse", "First Pulse", Inc(Stat.Health, 8f), Flat(Stat.HealthLeech, 0.3f)),
            Lanes = new[]
            {
                Lane("vein", null, 0f, 0f, 1100f, 0f,
                    P("deep-veins", "Deep Veins", Inc(Stat.Health, 5f), Inc(Stat.Health, 12f), Flat(Stat.HealthRegenPercent, 0.4f)),
                    P2("long-cuts", "Long Cuts", Chance(Tags.Bleed, 3f), Chance(Tags.Bleed, 10f), Inc(Stat.Damage, 18f, Tags.Bleed)),
                    P("sap", "Sap", Flat(Stat.HealthLeech, 0.2f), Flat(Stat.HealthLeech, 0.6f), Flat(Stat.ManaLeech, 0.3f)),
                    P2("warm-blood", "Warm Blood", Flat(Stat.HealthRegen, 1.5f), Flat(Stat.HealthRegen, 5f), Resist(Tags.Cold, 10f))),
                Lane("artery", "vein", -6f, 1100f, 2350f, -1f,
                    P("strong-heart", "Strong Heart", Flat(Stat.HealthRegen, 1.5f), Flat(Stat.HealthRegenPercent, 1f), Inc(Stat.FlaskEffect, 15f)),
                    P2("lifeblood", "Lifeblood", Inc(Stat.Health, 5f), Inc(Stat.Health, 15f), Flat(Stat.Health, 30f)),
                    P("gorge", "Gorge", Flat(Stat.HealthOnKill, 5f), Flat(Stat.HealthOnKill, 18f), Flat(Stat.ManaOnKill, 6f)),
                    P("transfusion", "Transfusion", Inc(Stat.Recovery, 5f), Inc(Stat.Recovery, 15f), Flat(Stat.HealthOnHit, 2f)),
                    P("vital-spark", "Vital Spark", Inc(Stat.Damage, 6f).While(Condition.FullHealth), Inc(Stat.Damage, 25f).While(Condition.FullHealth), Inc(Stat.MoveSpeed, 6f).While(Condition.FullHealth)),
                    P("quickened-pulse", "Quickened Pulse", Inc(Stat.MoveSpeed, 2f), Inc(Stat.MoveSpeed, 6f), Flat(Stat.HealthRegenPercent, 0.4f))),
                Lane("fang", "vein", 6f, 1100f, 2300f, 0.5f,
                    P("the-leech", "The Leech", Flat(Stat.HealthLeech, 0.3f), Flat(Stat.HealthLeech, 1f), Inc(Stat.Recovery, 10f)),
                    P2("bloodletting", "Bloodletting", Inc(Stat.Damage, 10f, Tags.Bleed), Inc(Stat.Damage, 20f).Against(Condition.Bleeding), Inc(Stat.AilmentDuration, 15f, Tags.Bleed)),
                    P("open-wounds", "Open Wounds", Chance(Tags.Bleed, 3f), Inc(Stat.Damage, 28f, Tags.Bleed), Inc(Stat.AilmentDuration, 20f, Tags.Bleed)),
                    P("crimson-dance", "Crimson Dance", Inc(Stat.Damage, 10f, Tags.Bleed), Inc(Stat.Damage, 30f, Tags.Bleed).Against(Condition.Moving), Inc(Stat.MoveSpeed, 5f)),
                    P4("blood-frenzy", "Blood Frenzy", Inc(Stat.AttackSpeed, 3f), Inc(Stat.AttackSpeed, 10f).While(Condition.RecentlyKilled), Flat(Stat.HealthOnKill, 10f)),
                    P("lacerate", "Lacerate", Inc(Stat.Damage, 10f, Tags.Bleed), Inc(Stat.Damage, 25f, Tags.Bleed), Chance(Tags.Bleed, 5f))),
                Lane("heartwood", "artery", -5f, 2350f, 3350f, -0.5f,
                    P("frenzy", "Frenzy", Inc(Stat.Damage, 6f).While(Condition.LowHealth), Inc(Stat.Damage, 30f).While(Condition.LowHealth), Inc(Stat.AttackSpeed, 10f).While(Condition.LowHealth)),
                    P("pain-attunement", "Pain Attunement", Inc(Stat.Damage, 10f, Tags.Spell), Inc(Stat.Damage, 30f, Tags.Spell).While(Condition.LowHealth), Inc(Stat.CastSpeed, 8f).While(Condition.LowHealth)),
                    P2("last-stand", "Last Stand", Inc(Stat.Armour, 10f), Inc(Stat.DamageTaken, -10f).While(Condition.LowHealth), Inc(Stat.Armour, 30f).While(Condition.LowHealth)),
                    P("undying", "Undying", Flat(Stat.HealthRegenPercent, 0.25f), Flat(Stat.HealthRegenPercent, 1.5f).While(Condition.LowHealth), Inc(Stat.Recovery, 10f)),
                    P("red-mist", "Red Mist", Inc(Stat.Damage, 6f), Inc(Stat.Damage, 1f).For(Per.HealthSpent, 1f, 40f), Inc(Stat.Health, 8f))),
                Lane("covenant", "artery", 1f, 2350f, 3300f, 0f,
                    P("crimson-tide", "Crimson Tide", Flat(Stat.HealthLeech, 0.2f), Flat(Stat.HealthLeech, 0.5f), Inc(Stat.Damage, 15f).While(Condition.FullHealth)),
                    P2("oak-heart", "Oak Heart", Inc(Stat.Health, 5f), Inc(Stat.Health, 12f), Flat(Stat.HealthRegen, 4f)),
                    P("sanguine-ward", "Sanguine Ward", Flat(Stat.Barrier, 12f), Flat(Stat.Barrier, 1f).For(Per.MaxHealth, 20f), Inc(Stat.Health, 6f)),
                    P("bleeding-edge", "Bleeding Edge", Chance(Tags.Bleed, 3f), Chance(Tags.Bleed, 8f), Inc(Stat.Damage, 15f).Against(Condition.Bleeding)),
                    P("endurance", "Endurance", Inc(Stat.Recovery, 5f), Inc(Stat.Recovery, 12f), Inc(Stat.FlaskEffect, 15f))),
                Lane("hunger", "fang", 5f, 2300f, 3400f, 1f,
                    P("exsanguinate", "Exsanguinate", Inc(Stat.AilmentDuration, 10f, Tags.Bleed), Inc(Stat.AilmentDuration, 30f, Tags.Bleed), Inc(Stat.Damage, 15f, Tags.Bleed)),
                    P2("haemophilia", "Haemophilia", Chance(Tags.Bleed, 3f), Chance(Tags.Bleed, 8f), Inc(Stat.Damage, 20f).Against(Condition.Bleeding)),
                    P("vampiric", "Vampiric", Flat(Stat.HealthLeech, 0.2f), Flat(Stat.HealthLeech, 0.6f), Flat(Stat.HealthOnHit, 3f)),
                    P("bone-marrow", "Bone Marrow", Flat(Stat.Health, 12f), Inc(Stat.Health, 10f), Inc(Stat.StunThreshold, 25f)),
                    P("thirst", "Thirst", Flat(Stat.HealthOnKill, 5f), Inc(Stat.Damage, 20f).While(Condition.RecentlyKilled), Flat(Stat.HealthOnKill, 12f))),
            },
            Wheels = new[]
            {
                Wheel("lifespring", "The Lifespring", 1850f, 236f,
                    new[] { Inc(Stat.Health, 4f), Flat(Stat.HealthRegenPercent, 0.2f) },
                    Gives(Inc(Stat.Health, 15f), Flat(Stat.HealthRegenPercent, 1.2f), Inc(Stat.Recovery, 10f)),
                    N("robust", "Robust", Inc(Stat.Health, 12f), Flat(Stat.Health, 40f)),
                    N("regrowth", "Regrowth", Flat(Stat.HealthRegen, 8f), Inc(Stat.FlaskEffect, 15f)),
                    N("sanguine-flow", "Sanguine Flow", Flat(Stat.HealthLeech, 0.6f), Inc(Stat.Recovery, 12f))),
            },
            Keystones = new[]
            {
                Key("blood-tithe", "Blood Tithe", "Tithebearer", TalentGlyph.Fang, 3500f, 242f, "Everything has a price, and you've stopped pretending otherwise.",
                    Rules(Rule.CostsHealth), More(Stat.Damage, 2f).For(Per.HealthSpent, 1f, 60f), Inc(Stat.Health, 10f)),
                Key("debtor", "Debtor", "Debtor", TalentGlyph.Coin, 2640f, 237f, "It'll have to be paid eventually. Not today, though.",
                    Rules(Rule.DeferredDamage), Rules(Rule.NoLeech), Inc(Stat.Recovery, 15f)),
                Key("overflowing-cup", "Overflowing Cup", null, TalentGlyph.Chalice, 3460f, 231.5f, "More than you can hold is still yours.",
                    Rules(Rule.OverhealBarrier), Inc(Stat.Recovery, 30f), Set(Stat.BarrierRecharge, 0f)),
            },
        };
    }
}

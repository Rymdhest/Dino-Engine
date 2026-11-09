using Dino_Defenders.Stats;
using static Dino_Defenders.Stats.M;

namespace Dino_Defenders.Talents
{
    /// <summary>The right of the tree: the quick, from the poisoner beside the storm round to the knave beside the forge.</summary>
    public static partial class TalentContent
    {
        // Poison, lingering harm and the fen: the plaguebearer, patient and hard to pin down.
        private static Region Mire() => new Region
        {
            Id = "mire", Name = "The Mire", Adjective = "Festering", Noun = "Plaguebearer", Colour = Rgb(116, 190, 60),
            Centre = 66f, Width = 28f, Attribute = Stat.Dexterity, Second = Stat.Intellect,
            Lanes = new[]
            {
                Lane("bog", null, 0f, 0f, 1050f, 1f,
                    P("spittle", "Spittle", Inc(Stat.Damage, 10f, Tags.Poison), Inc(Stat.Damage, 20f, Tags.Poison), Chance(Tags.Venom, 6f, Tags.Projectile)),
                    P2("foul-air", "Foul Air", Inc(Stat.Damage, 10f, Tags.DamageOverTime), Inc(Stat.Damage, 18f, Tags.DamageOverTime), Inc(Stat.PageDuration, 12f, Tags.Zone)),
                    P("mudslick", "Mudslick", Inc(Stat.Evasion, 10f), Inc(Stat.Evasion, 25f).While(Condition.WetLand), Inc(Stat.MoveSpeed, 8f).While(Condition.WetLand)),
                    P2("fen-dweller", "Fen Dweller", Resist(Tags.Poison, 6f), Resist(Tags.Poison, 15f), Inc(Stat.Health, 6f))),
                Lane("venom", "bog", -6f, 1050f, 2350f, -1.5f,
                    P("toxic-blood", "Toxic Blood", Chance(Tags.Venom, 3f), Chance(Tags.Venom, 10f), Inc(Stat.Damage, 25f, Tags.Venom)),
                    P("weakness", "Weakness", Inc(Stat.Damage, 10f, Tags.Poison), Flat(Stat.Penetration, 10f, Tags.Poison), Inc(Stat.Damage, 15f, Tags.Poison)),
                    P2("virulence", "Virulence", Inc(Stat.AilmentDuration, 10f, Tags.Venom), Inc(Stat.AilmentDuration, 30f, Tags.Venom), Inc(Stat.Damage, 15f, Tags.Venom)),
                    P("feeding-on-the-sick", "Feeding on the Sick", Flat(Stat.HealthOnHit, 1f), Inc(Stat.Damage, 25f).Against(Condition.Envenomed), Flat(Stat.HealthOnKill, 12f)),
                    P4("creeping-death", "Creeping Death", Inc(Stat.Damage, 10f, Tags.Venom), Inc(Stat.Damage, 30f, Tags.Venom), Chance(Tags.Venom, 6f)),
                    P("venomous-edge", "Venomous Edge", Chance(Tags.Venom, 3f), Chance(Tags.Venom, 8f), Inc(Stat.Damage, 15f, Tags.Poison))),
                Lane("rot", "bog", 7f, 1050f, 2300f, 1f,
                    P("slow-sickness", "Slow Sickness", Inc(Stat.AilmentDuration, 8f), Inc(Stat.AilmentDuration, 25f), Inc(Stat.Damage, 12f, Tags.DamageOverTime)),
                    P("rotting", "Rot", Inc(Stat.Damage, 10f, Tags.DamageOverTime), Inc(Stat.Damage, 30f, Tags.DamageOverTime), Flat(Stat.Penetration, 6f, Tags.Poison)),
                    P2("spreading-stain", "Spreading Stain", Inc(Stat.AreaOfEffect, 5f, Tags.Zone), Inc(Stat.AreaOfEffect, 20f, Tags.Zone), Inc(Stat.PageDuration, 20f, Tags.Zone)),
                    P("antibodies", "Antibodies", Resist(Tags.Poison, 6f), Resist(Tags.Poison, 20f), Flat(Stat.AilmentAvoid, 20f, Tags.Venom), Inc(Stat.AilmentTaken, -20f)),
                    P("decay", "Decay", Inc(Stat.Damage, 10f, Tags.DamageOverTime), Inc(Stat.Damage, 1f, Tags.DamageOverTime).For(Per.Dexterity, 10f, 30f), Inc(Stat.AilmentDuration, 10f)),
                    P("blight", "Blight", Inc(Stat.Damage, 10f, Tags.DamageOverTime), Inc(Stat.Damage, 20f, Tags.DamageOverTime), Inc(Stat.AreaOfEffect, 8f, Tags.Zone))),
                Lane("plague", "venom", -7f, 2350f, 3280f, -1f,
                    P("pestilence", "Pestilence", Inc(Stat.Damage, 10f, Tags.Poison), Inc(Stat.Damage, 20f, Tags.Poison | Tags.Area), Inc(Stat.AreaOfEffect, 10f)),
                    P2("black-bile", "Black Bile", Chance(Tags.Venom, 3f), Inc(Stat.Damage, 25f, Tags.Poison).Against(Condition.Envenomed), Chance(Tags.Venom, 6f)),
                    P("miasmic-ward", "Miasmic Ward", Inc(Stat.Evasion, 10f), Inc(Stat.Evasion, 20f), Flat(Stat.SpellEvasion, 12f)),
                    P("lingering-fog", "Lingering Fog", Inc(Stat.PageDuration, 6f, Tags.Zone), Inc(Stat.PageDuration, 20f, Tags.Zone), Inc(Stat.Damage, 15f, Tags.Zone)),
                    P("patient-poisoner", "Patient Poisoner", Inc(Stat.AilmentDuration, 10f, Tags.Venom), Inc(Stat.AilmentDuration, 25f, Tags.Venom), Inc(Stat.Damage, 15f, Tags.DamageOverTime))),
                Lane("sump", "venom", 1f, 2350f, 3300f, 0.5f,
                    P("noxious", "Noxious", Inc(Stat.Damage, 10f, Tags.Poison), Inc(Stat.Damage, 25f, Tags.Poison), Flat(Stat.Penetration, 4f, Tags.Poison)),
                    P2("lingering-death", "Lingering Death", Inc(Stat.AilmentDuration, 8f), Inc(Stat.AilmentDuration, 20f), Inc(Stat.Damage, 10f, Tags.DamageOverTime)),
                    P("carrion", "Carrion", Flat(Stat.HealthOnKill, 5f), Flat(Stat.HealthOnKill, 15f), Inc(Stat.Damage, 15f).Against(Condition.Envenomed)),
                    P("toxic-haze", "Toxic Haze", Inc(Stat.Evasion, 10f), Inc(Stat.Evasion, 20f).While(Condition.Moving), Flat(Stat.SpellEvasion, 6f)),
                    P("venom-sac", "Venom Sac", Chance(Tags.Venom, 3f), Chance(Tags.Venom, 10f), Inc(Stat.AilmentDuration, 15f, Tags.Venom))),
                Lane("fen", "rot", 6f, 2300f, 3380f, 0.5f,
                    P2("bogwalker", "Bogwalker", Inc(Stat.MoveSpeed, 2f), Inc(Stat.MoveSpeed, 10f).While(Condition.InWater), Inc(Stat.Evasion, 20f).While(Condition.InWater)),
                    P("leech-pit", "Leech Pit", Flat(Stat.HealthLeech, 0.2f), Flat(Stat.HealthLeech, 0.8f), Inc(Stat.Damage, 15f, Tags.Poison)),
                    P("will-o-wisp", "Will o' the Wisp", Flat(Stat.Stealth, 4f), Flat(Stat.Stealth, 15f), Inc(Stat.Damage, 20f).Against(Condition.Unaware)),
                    P("swamp-fever", "Swamp Fever", Inc(Stat.Damage, 10f, Tags.DamageOverTime), Inc(Stat.Damage, 25f, Tags.DamageOverTime).While(Condition.WetLand), Resist(Tags.Poison, 10f)),
                    P("corrosion", "Corrosion", Inc(Stat.Damage, 10f, Tags.Poison), Flat(Stat.ArmourIgnore, 10f), Flat(Stat.Penetration, 6f, Tags.Poison))),
            },
            Wheels = new[]
            {
                Wheel("miasma", "The Miasma", 1580f, 67f,
                    new[] { Flat(Stat.Penetration, 2f, Tags.Poison), Chance(Tags.Venom, 2f) },
                    Gives(Flat(Stat.Penetration, 12f, Tags.Poison), Inc(Stat.Damage, 25f, Tags.Poison), Inc(Stat.AilmentDuration, 15f, Tags.Venom)),
                    N("toxic-bloom", "Toxic Bloom", Chance(Tags.Venom, 8f), Inc(Stat.Damage, 15f, Tags.Venom)),
                    N("necrotic-touch", "Necrotic Touch", Convert(DamageType.Physical, DamageType.Poison, 25f), Inc(Stat.Damage, 10f, Tags.Poison)),
                    N("plague-ward", "Plague Ward", Resist(Tags.Poison, 15f), Flat(Stat.MaxResistance, 3f, Tags.Poison))),
            },
            Keystones = new[]
            {
                Key("patient-death", "Patient Death", "Patient Death", TalentGlyph.Hourglass, 3450f, 58f, "There's no need to hurry anyone along.",
                    More(Stat.Damage, -60f, Tags.Hit), Rules(Rule.EndlessVenom), Inc(Stat.AilmentDuration, 100f, Tags.Venom), Chance(Tags.Venom, 30f)),
                Key("carrier", "Carrier", "Carrier", TalentGlyph.Skull, 2640f, 66f, "You feel perfectly well. It's everyone else.",
                    Rules(Rule.Carrier), Inc(Stat.Health, 10f), More(Stat.Recovery, -25f)),
                Key("contagion", "Contagion", null, TalentGlyph.Web, 3540f, 73f, "It's never just the one.",
                    Rules(Rule.AilmentsLeap), Inc(Stat.Damage, 20f, Tags.DamageOverTime), More(Stat.AilmentDuration, -25f)),
                Key("mirage", "Mirage", "Mirage", TalentGlyph.Moon, 3120f, 65f, "They aimed at where you were. They always do.",
                    Flat(Stat.SpellEvasion, 100f), Inc(Stat.Evasion, 20f), Set(Stat.BlockChance, 0f)),
            },
        };

        // Bows, flight and the far shot: the hunter, who wants distance and gets it.
        private static Region Hunt() => new Region
        {
            Id = "hunt", Name = "The Hunt", Adjective = "Hunting", Noun = "Stalker", Colour = Rgb(170, 210, 76),
            Centre = 96f, Width = 32f, Attribute = Stat.Dexterity,
            Lanes = new[]
            {
                Lane("track", null, 0f, 0f, 950f, -0.5f,
                    P2("light-foot", "Light Foot", Inc(Stat.Evasion, 10f), Inc(Stat.Evasion, 25f), Inc(Stat.MoveSpeed, 5f)),
                    P("steady-draw", "Steady Draw", Inc(Stat.Damage, 10f, Tags.Projectile), Inc(Stat.Damage, 20f, Tags.Projectile), Inc(Stat.Accuracy, 15f)),
                    P("keen-eye", "Keen Eye", Inc(Stat.Accuracy, 10f), Inc(Stat.Accuracy, 30f), Flat(Stat.CritChance, 1f, Tags.Projectile)),
                    P2("patience-of-the-hunt", "Patience of the Hunt", Inc(Stat.Damage, 6f), Inc(Stat.Damage, 20f).While(Condition.Stationary), Inc(Stat.Accuracy, 15f))),
                Lane("draw", "track", -9f, 950f, 2350f, 0.5f,
                    P("fletching", "Fletching", Inc(Stat.Damage, 10f, Tags.Bow), Inc(Stat.Damage, 28f, Tags.Bow), Inc(Stat.AttackSpeed, 6f, Tags.Bow)),
                    P2("heavy-draw", "Heavy Draw", Inc(Stat.Damage, 10f, Tags.Bow), Flat(Stat.CritMultiplier, 25f, Tags.Bow), Inc(Stat.Damage, 12f, Tags.Projectile)),
                    P("dead-eye", "Dead Eye", Inc(Stat.CritChance, 12f, Tags.Projectile), Flat(Stat.CritChance, 1.5f, Tags.Projectile), Inc(Stat.Accuracy, 25f)),
                    P("first-blood", "First Blood", Inc(Stat.Damage, 10f, Tags.Projectile), Inc(Stat.Damage, 35f).Against(Condition.FullHealth), Chance(Tags.Bleed, 8f, Tags.Projectile)),
                    P4("hunters-instinct", "Hunter's Instinct", Inc(Stat.Damage, 10f, Tags.Projectile), Inc(Stat.Damage, 1f, Tags.Projectile).For(Per.Dexterity, 10f, 40f), Flat(Stat.Dexterity, 15f)),
                    P("taut-string", "Taut String", Inc(Stat.AttackSpeed, 3f, Tags.Bow), Inc(Stat.Damage, 15f, Tags.Bow), Inc(Stat.ProjectileSpeed, 10f))),
                Lane("flight", "track", 7f, 950f, 2300f, 1f,
                    P("through-and-through", "Through and Through", Inc(Stat.ProjectileSpeed, 8f), Flat(Stat.Pierce, 1f), Inc(Stat.Damage, 15f, Tags.Projectile)),
                    P2("hawk-feathers", "Hawk Feathers", Inc(Stat.ProjectileSpeed, 8f), Inc(Stat.ProjectileSpeed, 25f), Inc(Stat.Damage, 15f, Tags.Projectile)),
                    P("skirmisher", "Skirmisher", Inc(Stat.Damage, 10f, Tags.Projectile), Inc(Stat.Damage, 25f, Tags.Projectile).While(Condition.Moving), Inc(Stat.Evasion, 20f).While(Condition.Moving)),
                    P("split-shaft", "Split Shaft", Inc(Stat.Damage, 10f, Tags.Projectile), Flat(Stat.ProjectileCount, 1f, Tags.Bow), Inc(Stat.Damage, -10f, Tags.Bow)),
                    P("broadheads", "Broadheads", Chance(Tags.Bleed, 3f, Tags.Projectile), Chance(Tags.Bleed, 12f, Tags.Projectile), Inc(Stat.Damage, 20f, Tags.Bleed)),
                    P("fletchers-craft", "Fletcher's Craft", Inc(Stat.Damage, 10f, Tags.Projectile), Inc(Stat.Damage, 15f, Tags.Projectile), Inc(Stat.Accuracy, 15f))),
                Lane("string", "draw", -8f, 2350f, 3300f, -1f,
                    P("volley-master", "Volley Master", Inc(Stat.AttackSpeed, 3f, Tags.Bow), Inc(Stat.AttackSpeed, 10f, Tags.Bow), Inc(Stat.Damage, 12f, Tags.Bow)),
                    P2("close-quarters", "Close Quarters", Inc(Stat.Damage, 10f, Tags.Projectile), Inc(Stat.Damage, 25f, Tags.Projectile).Against(Condition.UpClose), Inc(Stat.Evasion, 15f)),
                    P("pinning-shot", "Pinning Shot", Inc(Stat.Damage, 10f, Tags.Projectile), Chance(Tags.Stun, 8f, Tags.Projectile), Inc(Stat.Damage, 20f).Against(Condition.Stunned)),
                    P("rangers-ward", "Ranger's Ward", Resist(Tags.Elemental, 3f), Resist(Tags.Elemental, 12f), Inc(Stat.Evasion, 15f)),
                    P("marksman", "Marksman", Inc(Stat.Accuracy, 10f), Inc(Stat.Accuracy, 25f), Flat(Stat.CritMultiplier, 20f, Tags.Projectile))),
                Lane("far", "flight", 5f, 2300f, 3420f, 0f,
                    P("long-sight", "Long Sight", Inc(Stat.Damage, 10f, Tags.Projectile), Inc(Stat.Damage, 30f, Tags.Projectile).Against(Condition.AtRange), Inc(Stat.ProjectileSpeed, 10f)),
                    P2("ricochet", "Ricochet", Inc(Stat.Damage, 10f, Tags.Chaining), Flat(Stat.Chain, 1f, Tags.Projectile), Inc(Stat.Damage, 10f, Tags.Projectile)),
                    P("lone-hunter", "Lone Hunter", Inc(Stat.Damage, 10f, Tags.Projectile), Inc(Stat.Damage, 25f).Against(Condition.Isolated), Inc(Stat.MoveSpeed, 5f)),
                    P("eagle-eye", "Eagle Eye", Inc(Stat.CritChance, 12f, Tags.Projectile), Inc(Stat.CritChance, 35f, Tags.Projectile), Inc(Stat.Accuracy, 15f)),
                    P("windrunner", "Windrunner", Inc(Stat.MoveSpeed, 2f), Inc(Stat.MoveSpeed, 8f), Inc(Stat.ProjectileSpeed, 15f))),
                Lane("trail", "flight", 11f, 2300f, 3250f, 1f,
                    P("tracker", "Tracker", Inc(Stat.MoveSpeed, 2f), Inc(Stat.MoveSpeed, 8f), Inc(Stat.Damage, 15f).While(Condition.UnderTrees)),
                    P("hunting-party", "Hunting Party", Inc(Stat.Damage, 10f, Tags.Minion), Inc(Stat.Damage, 20f, Tags.Minion), Inc(Stat.Damage, 10f, Tags.Projectile)),
                    P2("quarry", "Quarry", Inc(Stat.Damage, 10f, Tags.Projectile), Inc(Stat.Damage, 25f).Against(Condition.Rare), Flat(Stat.CritChance, 1f)),
                    P("camouflage", "Camouflage", Flat(Stat.Stealth, 4f), Flat(Stat.Stealth, 15f), Inc(Stat.Evasion, 15f)),
                    P("hawk-strike", "Hawk Strike", Flat(Stat.CritMultiplier, 8f, Tags.Projectile), Flat(Stat.CritMultiplier, 25f, Tags.Projectile), Inc(Stat.CritChance, 15f, Tags.Projectile))),
            },
            Wheels = new[]
            {
                Wheel("quiver", "The Quiver", 1880f, 96.5f,
                    new[] { Inc(Stat.Damage, 8f, Tags.Projectile), Inc(Stat.ProjectileSpeed, 6f) },
                    Gives(Flat(Stat.ProjectileCount, 1f), Inc(Stat.Damage, -10f, Tags.Projectile)),
                    N("barbed-points", "Barbed Points", Flat(Stat.Pierce, 1f), Inc(Stat.Damage, 10f, Tags.Projectile)),
                    N("swift-shafts", "Swift Shafts", Inc(Stat.ProjectileSpeed, 20f), Inc(Stat.AttackSpeed, 5f)),
                    N("hail", "Hail", Inc(Stat.Damage, 15f, Tags.Projectile), Inc(Stat.Potency, 8f, Tags.Projectile))),
            },
            Keystones = new[]
            {
                Key("long-shot", "Long Shot", "Long Shot", TalentGlyph.Eye, 3600f, 101f, "They always think they're safe at that range.",
                    Rules(Rule.LongShot)),
                Key("peakborn", "Peakborn", "Peakborn", TalentGlyph.Peak, 2700f, 94f, "You've always looked down on people.",
                    Rules(Rule.HighGround), Inc(Stat.Damage, 10f, Tags.Projectile)),
                Key("starburst", "Starburst", "Starfall", TalentGlyph.Sun, 3420f, 86f, "Why aim, when you could simply be everywhere at once?",
                    Rules(Rule.Starburst), Flat(Stat.ProjectileCount, 2f), More(Stat.ProjectileSpeed, -25f)),
            },
        };

        // The land itself: trees, water, roads and weather, and the beasts that walk with a warden.
        private static Region Wilds() => new Region
        {
            Id = "wilds", Name = "The Wilds", Adjective = "Wild", Noun = "Warden of the Land", Colour = Rgb(72, 168, 130),
            Centre = 125f, Width = 26f, Attribute = Stat.Dexterity,
            Lanes = new[]
            {
                Lane("path", null, 0f, 0f, 1200f, -1f,
                    P("forager", "Forager", Inc(Stat.FlaskEffect, 8f), Inc(Stat.FlaskEffect, 25f), Flat(Stat.HealthRegenPercent, 0.3f)),
                    P2("wild-heart", "Wild Heart", Flat(Stat.Health, 12f), Inc(Stat.Health, 10f), Flat(Stat.HealthRegen, 3f)),
                    P("fleet", "Fleet", Inc(Stat.MoveSpeed, 2f), Inc(Stat.MoveSpeed, 7f), Inc(Stat.Evasion, 12f)),
                    P2("deep-roots", "Deep Roots", Flat(Stat.Health, 12f), Inc(Stat.Health, 10f), Inc(Stat.StunThreshold, 20f))),
                Lane("grove", "path", -5f, 1200f, 2350f, -0.5f,
                    P("forest-floor", "Forest Floor", Inc(Stat.Evasion, 10f), Inc(Stat.Evasion, 35f).While(Condition.UnderTrees), Flat(Stat.Stealth, 15f).While(Condition.UnderTrees)),
                    P2("barkskin", "Barkskin", Flat(Stat.Armour, 30f), Inc(Stat.Armour, 20f), Inc(Stat.Evasion, 20f)),
                    P("verdant-blood", "Verdant Blood", Flat(Stat.HealthRegenPercent, 0.25f), Flat(Stat.HealthRegenPercent, 1f), Inc(Stat.Recovery, 10f)),
                    P("whistle", "Whistle", Inc(Stat.Damage, 10f, Tags.Minion), Inc(Stat.MoveSpeed, 15f, Tags.Minion), Inc(Stat.Health, 15f, Tags.Minion)),
                    P("pack-bond", "Pack Bond", Inc(Stat.Health, 10f, Tags.Minion), Inc(Stat.Damage, 20f, Tags.Minion), Inc(Stat.Damage, 2f).For(Per.Minions, 1f, 12f)),
                    P("sylvan-step", "Sylvan Step", Inc(Stat.Evasion, 10f), Inc(Stat.Evasion, 20f), Inc(Stat.MoveSpeed, 6f).While(Condition.UnderTrees))),
                Lane("river", "path", 7f, 1200f, 2300f, 1f,
                    P("river-sense", "River Sense", Inc(Stat.MoveSpeed, 2f), Inc(Stat.MoveSpeed, 12f).While(Condition.InWater), Inc(Stat.Damage, 20f).While(Condition.InWater)),
                    P2("clear-water", "Clear Water", Inc(Stat.Recovery, 5f), Inc(Stat.Recovery, 15f).While(Condition.InWater), Inc(Stat.Potency, 12f).While(Condition.InWater)),
                    P("rain-dance", "Rain Dance", Inc(Stat.ReactionDamage, 10f), Inc(Stat.ReactionDamage, 35f), Inc(Stat.ReactionArea, 25f)),
                    P("tidecaller", "Tidecaller", Inc(Stat.Damage, 10f, Tags.Cold), Inc(Stat.Damage, 20f, Tags.Cold).While(Condition.InWater), Inc(Stat.Damage, 20f, Tags.Lightning).While(Condition.InWater)),
                    P("slippery", "Slippery", Inc(Stat.DodgeCooldown, -4f), Inc(Stat.DodgeCooldown, -15f), Inc(Stat.DodgeSpeed, 15f)),
                    P("riverstone", "Riverstone", Inc(Stat.Armour, 10f), Inc(Stat.Armour, 25f).While(Condition.InWater), Resist(Tags.Lightning, 10f))),
                Lane("deepwood", "grove", -6f, 2350f, 3400f, -1.5f,
                    P("dusk", "Dusk", Flat(Stat.Stealth, 4f), Flat(Stat.Stealth, 15f), Inc(Stat.Damage, 30f).While(Condition.InDarkness)),
                    P2("old-growth", "Old Growth", Inc(Stat.Health, 5f), Inc(Stat.Health, 12f), Flat(Stat.HealthRegenPercent, 0.5f).While(Condition.UnderTrees)),
                    P("thornwood", "Thornwood", Flat(Stat.Reflect, 3f), Flat(Stat.Reflect, 15f), Inc(Stat.Armour, 15f)),
                    P("beast-tamer", "Beast Tamer", Inc(Stat.Damage, 10f, Tags.Minion), Flat(Stat.MinionCount, 1f), Inc(Stat.Health, 10f, Tags.Minion)),
                    P("moonlit", "Moonlit", Inc(Stat.Evasion, 10f), Inc(Stat.Evasion, 25f).While(Condition.InDarkness), Inc(Stat.Damage, 15f).While(Condition.InDarkness))),
                Lane("canopy", "grove", -12f, 2350f, 3300f, -0.5f,
                    P("natures-ward", "Nature's Ward", Resist(Tags.Elemental, 3f), Resist(Tags.Elemental, 10f), Flat(Stat.HealthRegenPercent, 0.4f)),
                    P("wild-growth", "Wild Growth", Flat(Stat.HealthRegenPercent, 0.25f), Flat(Stat.HealthRegenPercent, 1f).While(Condition.UnderTrees), Inc(Stat.Health, 8f)),
                    P("stalking-beast", "Stalking Beast", Inc(Stat.Damage, 10f, Tags.Minion), Inc(Stat.AttackSpeed, 10f, Tags.Minion), Inc(Stat.MoveSpeed, 10f, Tags.Minion)),
                    P2("bramble", "Bramble", Flat(Stat.Reflect, 3f), Flat(Stat.Reflect, 10f), Inc(Stat.Damage, 15f).While(Condition.UnderTrees)),
                    P("old-wood", "Old Wood", Inc(Stat.Health, 5f), Inc(Stat.Health, 12f), Inc(Stat.Recovery, 10f).While(Condition.UnderTrees))),
                Lane("wayfarer", "river", 6f, 2300f, 3280f, 1f,
                    P("roadwise", "Wayfarer", Inc(Stat.MoveSpeed, 2f), Inc(Stat.MoveSpeed, 15f).While(Condition.OnRoad), Inc(Stat.Damage, 15f).While(Condition.OnRoad)),
                    P2("pilgrim", "Pilgrim", Inc(Stat.Damage, 6f), Inc(Stat.Damage, 3f).For(Per.ShrinesKindled, 1f, 45f), Flat(Stat.Health, 8f).For(Per.ShrinesKindled, 1f, 120f)),
                    P("lay-of-the-land", "Lay of the Land", Inc(Stat.Damage, 6f), Inc(Stat.Damage, 15f).While(Condition.HotLand), Inc(Stat.Damage, 15f).While(Condition.ColdLand), Inc(Stat.Damage, 15f).While(Condition.WetLand), Inc(Stat.Damage, 15f).While(Condition.HighLand)),
                    P("weathered", "Weathered", Resist(Tags.Elemental, 3f), Resist(Tags.Elemental, 10f), Inc(Stat.Damage, 12f).While(Condition.InRain), Inc(Stat.Damage, 12f).While(Condition.InSnow))),
            },
            Wheels = new[]
            {
                new Wheel
                {
                    Id = "seasons", Name = "The Seasons", Radius = 1720f * Breadth, Angle = 124f, Spokes = 2,
                    Ring = new[] { Inc(Stat.Damage, 6f), Inc(Stat.MoveSpeed, 2f) },
                    Centre = Gives(Inc(Stat.Damage, 25f).While(Condition.UnderTrees), Inc(Stat.Damage, 25f).While(Condition.InWater), Inc(Stat.MoveSpeed, 8f)),
                    Notables = new[]
                    {
                        N("high-summer", "High Summer", Inc(Stat.Damage, 25f, Tags.Fire).While(Condition.HotLand), Resist(Tags.Fire, 10f)),
                        N("deep-snow", "Deep Snow", Inc(Stat.Damage, 25f, Tags.Cold).While(Condition.ColdLand), Resist(Tags.Cold, 10f)),
                        N("monsoon", "Monsoon", Inc(Stat.Damage, 25f, Tags.Lightning).While(Condition.WetLand), Resist(Tags.Lightning, 10f)),
                        N("mountain-air", "Mountain Air", Inc(Stat.Damage, 20f).While(Condition.HighLand), Inc(Stat.MoveSpeed, 6f).While(Condition.HighLand)),
                    },
                },
            },
            Keystones = new[]
            {
                Key("tidewalker", "Tidewalker", "Tidewalker", TalentGlyph.Wave, 3440f, 131f, "You've come to think of land as a long delay between seas.",
                    More(Stat.Damage, 50f).While(Condition.InWater), Flat(Stat.HealthRegenPercent, 3f).While(Condition.InWater), More(Stat.Damage, -15f).Unless(Condition.InWater)),
                Key("child-of-the-wood", "Child of the Wood", "Child of the Wood", TalentGlyph.Leaf, 3560f, 118f, "They've stopped noticing you. It's the nicest thing they've ever done.",
                    More(Stat.Evasion, 40f).While(Condition.UnderTrees), Inc(Stat.MoveSpeed, 30f).While(Condition.UnderTrees), More(Stat.Damage, 30f).While(Condition.UnderTrees), More(Stat.Damage, -15f).Unless(Condition.UnderTrees)),
                Key("spirit-of-the-place", "Spirit of the Place", "Genius Loci", TalentGlyph.Spiral, 2680f, 124.5f, "You don't bring anything to a fight. It's all already here.",
                    Convert(DamageType.Physical, DamageType.Fire, 100f).While(Condition.HotLand), Convert(DamageType.Physical, DamageType.Cold, 100f).While(Condition.ColdLand),
                    Convert(DamageType.Physical, DamageType.Poison, 100f).While(Condition.WetLand), Convert(DamageType.Physical, DamageType.Lightning, 100f).While(Condition.HighLand), Inc(Stat.Damage, 30f), More(Stat.Damage, -20f, Tags.Physical)),
                Key("unbroken-stride", "Unbroken Stride", "Strider", TalentGlyph.Wing, 3100f, 125f, "Stopping was only ever a habit.",
                    Rules(Rule.Unbroken), More(Stat.UseSpeed, -20f)),
            },
        };

        // Knives, luck and a quick exit: the knave, who strikes first, strikes hard, and is gone.
        private static Region Knave() => new Region
        {
            Id = "knave", Name = "The Knave", Adjective = "Sly", Noun = "Knave", Colour = Rgb(186, 132, 200),
            Centre = 151f, Width = 26f, Attribute = Stat.Dexterity, Second = Stat.Strength,
            Lanes = new[]
            {
                Lane("alley", null, 0f, 0f, 1000f, 0.5f,
                    P("lunging-step", "Lunging Step", Inc(Stat.AttackSpeed, 3f), Inc(Stat.Potency, 15f, Tags.Movement), Inc(Stat.BookCost, -15f, Tags.Movement)),
                    P2("cold-calculation", "Cold Calculation", Inc(Stat.CritChance, 12f), Inc(Stat.CritChance, 30f), Flat(Stat.CritChance, 1f)),
                    P("sidestep", "Sidestep", Inc(Stat.Evasion, 10f), Inc(Stat.DodgeCooldown, -15f), Inc(Stat.DodgeSpeed, 12f)),
                    P2("street-smarts", "Street Smarts", Inc(Stat.ItemRarity, 6f), Inc(Stat.ItemRarity, 15f), Inc(Stat.MoveSpeed, 4f))),
                Lane("blade", "alley", -6f, 1000f, 2350f, -0.5f,
                    P("twist-the-knife", "Twist the Knife", Inc(Stat.Damage, 10f, Tags.Dagger), Inc(Stat.Damage, 28f, Tags.Dagger), Flat(Stat.CritMultiplier, 20f, Tags.Dagger)),
                    P("cut-throat", "Cut Throat", Flat(Stat.CritMultiplier, 8f), Flat(Stat.CritMultiplier, 30f), Inc(Stat.Damage, 12f).While(Condition.RecentlyCrit)),
                    P2("poisoned-blade", "Poisoned Blade", Chance(Tags.Venom, 3f, Tags.Attack), Chance(Tags.Venom, 12f, Tags.Attack), Inc(Stat.Damage, 20f, Tags.Venom)),
                    P("assassins-mark", "Assassin's Mark", Inc(Stat.CritChance, 12f), Inc(Stat.CritChance, 40f).Against(Condition.LowHealth), Flat(Stat.CritMultiplier, 15f)),
                    P("quick-hands", "Quick Hands", Inc(Stat.AttackSpeed, 3f), Inc(Stat.AttackSpeed, 10f), Inc(Stat.Damage, 10f, Tags.Dagger)),
                    P("serrated", "Serrated", Chance(Tags.Bleed, 3f, Tags.Attack), Chance(Tags.Bleed, 10f, Tags.Attack), Inc(Stat.Damage, 15f, Tags.Bleed))),
                Lane("shadow", "alley", 6f, 1000f, 2300f, 1f,
                    P("shadow-work", "Shadow Work", Flat(Stat.Stealth, 4f), Flat(Stat.Stealth, 15f), Inc(Stat.Damage, 30f).Against(Condition.Unaware)),
                    P2("bait-and-switch", "Bait and Switch", Inc(Stat.Evasion, 10f), Rules(Rule.DodgeCrit), Inc(Stat.Damage, 20f).While(Condition.RecentlyDodged)),
                    P("smoke-and-mirrors", "Smoke and Mirrors", Inc(Stat.Evasion, 10f), Inc(Stat.Evasion, 30f), Flat(Stat.SpellEvasion, 8f)),
                    P("nimble", "Nimble", Inc(Stat.DodgeCooldown, -4f), Inc(Stat.DodgeCooldown, -12f), Inc(Stat.MoveSpeed, 6f)),
                    P4("vanishing-act", "Vanishing Act", Flat(Stat.Stealth, 4f), Flat(Stat.Stealth, 10f), Inc(Stat.MoveSpeed, 10f).While(Condition.RecentlyKilled)),
                    P("fade", "Fade", Inc(Stat.Evasion, 10f), Inc(Stat.Evasion, 25f), Inc(Stat.DodgeSpeed, 10f))),
                Lane("cutpurse", "blade", -5f, 2350f, 3300f, -0.5f,
                    P("pickpocket", "Pickpocket", Inc(Stat.GoldFind, 8f), Inc(Stat.GoldFind, 30f), Inc(Stat.ItemRarity, 15f)),
                    P2("gamblers-luck", "Gambler's Luck", Inc(Stat.Potency, 4f, Tags.Wild), Inc(Stat.Potency, 15f, Tags.Wild), Flat(Stat.CritChance, 1f)),
                    P("ambush", "Ambush", Inc(Stat.Damage, 6f), Inc(Stat.Damage, 25f).Against(Condition.FullHealth), Flat(Stat.CritChance, 1.5f)),
                    P("coup-de-grace", "Coup de Grace", Inc(Stat.Damage, 6f), Inc(Stat.Damage, 30f).Against(Condition.LowHealth), Flat(Stat.Execute, 4f)),
                    P("fortunes-favour", "Fortune's Favour", Inc(Stat.ItemRarity, 6f), Inc(Stat.ItemRarity, 20f), Inc(Stat.CritChance, 15f))),
                Lane("gutter", "blade", 1f, 2350f, 3300f, 0f,
                    P("backstab", "Backstab", Inc(Stat.Damage, 10f, Tags.Dagger), Inc(Stat.Damage, 30f, Tags.Dagger).Against(Condition.Unaware), Flat(Stat.CritChance, 1f)),
                    P2("dirty-tricks", "Dirty Tricks", Chance(Tags.Stun, 3f), Chance(Tags.Stun, 8f), Inc(Stat.Damage, 15f).Against(Condition.Stunned)),
                    P("weighted-blades", "Weighted Blades", Inc(Stat.Damage, 10f, Tags.Dagger), Flat(Stat.ProjectileCount, 1f, Tags.Dagger), Inc(Stat.Damage, -10f, Tags.Dagger)),
                    P("grifter", "Grifter", Inc(Stat.GoldFind, 8f), Inc(Stat.GoldFind, 25f), Flat(Stat.Stealth, 8f)),
                    P("nightblade", "Nightblade", Inc(Stat.Damage, 6f), Inc(Stat.Damage, 25f).While(Condition.InDarkness), Flat(Stat.CritChance, 1f).While(Condition.InDarkness))),
                Lane("duelist", "shadow", 5f, 2300f, 3250f, 1f,
                    P("two-fangs", "Two Fangs", Inc(Stat.Damage, 8f).While(Condition.DualWielding), Inc(Stat.AttackSpeed, 10f).While(Condition.DualWielding), Inc(Stat.Damage, 15f).While(Condition.DualWielding)),
                    P("riposte", "Riposte", Inc(Stat.Damage, 10f, Tags.Melee), Inc(Stat.Damage, 20f).While(Condition.RecentlyDodged), Inc(Stat.CritChance, 20f).While(Condition.RecentlyDodged)),
                    P2("parry", "Parry", Inc(Stat.Evasion, 10f), Flat(Stat.BlockChance, 6f).While(Condition.DualWielding), Inc(Stat.Evasion, 15f)),
                    P("flurry", "Flurry", Inc(Stat.AttackSpeed, 3f), Inc(Stat.AttackSpeed, 12f).While(Condition.RecentlyKilled), Inc(Stat.Damage, 10f, Tags.Melee)),
                    P("fencers-grace", "Fencer's Grace", Inc(Stat.Damage, 10f, Tags.Sword), Inc(Stat.Damage, 25f, Tags.Sword), Inc(Stat.Accuracy, 20f))),
            },
            Wheels = new[]
            {
                Wheel("blade-dance", "The Blade Dance", 1600f, 152f,
                    new[] { Flat(Stat.CritMultiplier, 6f), Inc(Stat.CritChance, 8f) },
                    Gives(Flat(Stat.CritMultiplier, 30f), Inc(Stat.CritChance, 25f), Inc(Stat.AttackSpeed, 5f)),
                    N("killing-blow", "Killing Blow", Flat(Stat.Execute, 5f), Inc(Stat.Damage, 15f).Against(Condition.LowHealth)),
                    N("precision-strikes", "Precision Strikes", Flat(Stat.CritChance, 1.5f), Inc(Stat.Accuracy, 20f)),
                    N("bloodletter", "Bloodletter", Chance(Tags.Bleed, 8f), Inc(Stat.Damage, 15f).Against(Condition.Bleeding))),
            },
            Keystones = new[]
            {
                Key("loaded-dice", "Loaded Dice", "Cardsharp", TalentGlyph.Dice, 3420f, 146f, "Luck is a thing that happens to other people.",
                    Set(Stat.CritChance, 50f), Divert(Stat.CritChance, Stat.CritMultiplier, 100f)),
                Key("final-word", "Final Word", "Reaper", TalentGlyph.Scythe, 3380f, 157f, "Nobody's listening by then.",
                    Flat(Stat.Execute, 25f), More(Stat.Damage, -25f, Tags.Hit)),
                Key("wild-margins", "Wild Margins", "Marginalist", TalentGlyph.Star, 2690f, 151f, "Nobody reads the notes. That's where you hide things.",
                    More(Stat.Potency, 40f, Tags.Wild), Inc(Stat.BookCost, 25f)),
                Key("narrow-way", "The Narrow Way", "Ascetic", TalentGlyph.Key, 820f, 138.5f, "Three roads, and the strength to walk only one of them all the way.",
                    Rules(Rule.NarrowWay)),
            },
        };
    }
}

using Dino_Defenders.Stats;
using static Dino_Defenders.Stats.M;

namespace Dino_Defenders.Talents
{
    /// <summary>The left of the tree, between the warrior and the mage: those who are both, with fire in one hand and the dead at their back.</summary>
    public static partial class TalentContent
    {
        // Fire, and the blade and the spell together: the pyromancer, and the battle mage who swings a burning sword.
        private static Region Pyre() => new Region
        {
            Id = "pyre", Name = "The Pyre", Adjective = "Ember", Noun = "Pyromancer", Colour = Rgb(242, 108, 32),
            Centre = 265f, Width = 30f, Attribute = Stat.Strength, Second = Stat.Intellect,
            Entry = N("first-flame", "First Flame", Inc(Stat.Damage, 12f, Tags.Fire), Chance(Tags.Burn, 5f)),
            Lanes = new[]
            {
                Lane("kindling", null, 0f, 0f, 1000f, -1f,
                    P("kindled-spark", "Kindled Spark", Inc(Stat.Damage, 10f, Tags.Fire), Inc(Stat.Damage, 20f, Tags.Fire), Chance(Tags.Burn, 5f)),
                    P2("ember-ward", "Ember Ward", Flat(Stat.Barrier, 15f), Flat(Stat.Barrier, 35f), Resist(Tags.Fire, 10f)),
                    P("battle-trance", "Battle Trance", Inc(Stat.UseSpeed, 2f), Inc(Stat.UseSpeed, 6f), Inc(Stat.Damage, 10f)),
                    P2("hearthfire", "Hearthfire", Flat(Stat.Health, 12f), Inc(Stat.Health, 8f), Resist(Tags.Fire, 10f))),
                Lane("flame", "kindling", -7f, 1000f, 2350f, -1f,
                    P("kindle", "Kindling", Chance(Tags.Burn, 3f), Chance(Tags.Burn, 12f), Inc(Stat.Damage, 22f, Tags.Burn)),
                    P("searing-heat", "Searing Heat", Inc(Stat.Damage, 10f, Tags.Fire), Inc(Stat.Damage, 28f, Tags.Fire), Flat(Stat.Penetration, 5f, Tags.Fire)),
                    P2("long-burn", "Long Burn", Inc(Stat.AilmentDuration, 10f, Tags.Burn), Inc(Stat.AilmentDuration, 30f, Tags.Burn), Inc(Stat.Damage, 12f, Tags.Burn)),
                    P("burn-it-down", "Burn it Down", Inc(Stat.Damage, 10f, Tags.Fire), Inc(Stat.Damage, 28f).Against(Condition.Burning), Flat(Stat.ExplodeOnKill, 4f)),
                    P("ash-skin", "Ash Skin", Resist(Tags.Fire, 6f), Resist(Tags.Fire, 18f), Flat(Stat.MaxResistance, 2f, Tags.Fire), TakenAs(DamageType.Cold, DamageType.Fire, 20f)),
                    P("ignition", "Ignition", Chance(Tags.Burn, 3f), Chance(Tags.Burn, 8f), Inc(Stat.Damage, 15f, Tags.Fire))),
                Lane("spellblade", "kindling", 6f, 1000f, 2300f, 0.5f,
                    P("arcane-steel", "Arcane Steel", Inc(Stat.Damage, 10f, Tags.Attack), Inc(Stat.Damage, 15f, Tags.Attack), Inc(Stat.Damage, 15f, Tags.Spell)),
                    P2("spellstrike", "Spellstrike", Inc(Stat.CastSpeed, 3f), Inc(Stat.CastSpeed, 8f), Inc(Stat.AttackSpeed, 8f)),
                    P("runed-armour", "Runed Armour", Flat(Stat.Barrier, 15f), Flat(Stat.Barrier, 1f).For(Per.Armour, 20f), Inc(Stat.Armour, 15f)),
                    P("ward-and-blade", "Ward and Blade", Inc(Stat.Damage, 10f, Tags.Melee), Inc(Stat.Damage, 20f, Tags.Melee).While(Condition.HasBarrier), Inc(Stat.BarrierRecharge, 15f)),
                    P4("mind-and-muscle", "Mind and Muscle", Inc(Stat.Damage, 6f), Flat(Stat.Strength, 12f), Flat(Stat.Intellect, 12f)),
                    P("eldritch-edge", "Eldritch Edge", Inc(Stat.Damage, 10f, Tags.Melee), GainAs(DamageType.Physical, DamageType.Fire, 8f, Tags.Melee), Inc(Stat.Damage, 12f, Tags.Fire))),
                Lane("inferno", "flame", -6f, 2350f, 3380f, -1f,
                    P("wreath-of-flame", "Wreath of Flame", Inc(Stat.AreaOfEffect, 5f), Inc(Stat.Damage, 20f, Tags.Fire | Tags.Area), Chance(Tags.Burn, 6f, Tags.Area)),
                    P2("falling-embers", "Falling Embers", Inc(Stat.Potency, 4f, Tags.Falling), Inc(Stat.Potency, 15f, Tags.Falling), Inc(Stat.Damage, 12f, Tags.Fire)),
                    P("wide-flame", "Wide Flame", Inc(Stat.AreaOfEffect, 5f), Inc(Stat.AreaOfEffect, 20f), Inc(Stat.Damage, 12f, Tags.Area)),
                    P("conflagration", "Conflagration", Inc(Stat.Damage, 10f, Tags.Burn), Inc(Stat.Damage, 25f, Tags.Burn), Inc(Stat.AreaOfEffect, 8f)),
                    P("flashpoint", "Flashpoint", Inc(Stat.ReactionDamage, 10f), Inc(Stat.ReactionDamage, 30f), Inc(Stat.Damage, 15f, Tags.Burn))),
                Lane("ashfall", "flame", -13f, 2350f, 3300f, -0.5f,
                    P("starfire", "Starfire", Inc(Stat.Potency, 4f, Tags.Falling), Inc(Stat.Potency, 12f, Tags.Falling), Inc(Stat.AreaOfEffect, 10f)),
                    P2("smouldering", "Smouldering", Inc(Stat.AilmentDuration, 10f, Tags.Burn), Inc(Stat.AilmentDuration, 25f, Tags.Burn), Inc(Stat.Damage, 10f, Tags.Burn)),
                    P("cinders", "Cinders", Flat(Stat.ExplodeOnKill, 1f), Flat(Stat.ExplodeOnKill, 4f), Inc(Stat.Damage, 10f, Tags.Fire)),
                    P("flame-ward", "Flame Ward", Resist(Tags.Fire, 6f), Resist(Tags.Fire, 15f), Flat(Stat.Barrier, 25f)),
                    P("pyrelight", "Pyrelight", Inc(Stat.Damage, 10f, Tags.Fire), Inc(Stat.Damage, 25f, Tags.Fire).While(Condition.InDarkness), Inc(Stat.AreaOfEffect, 6f))),
                Lane("crucible-lane", "spellblade", 4f, 2300f, 3330f, 0.5f,
                    P("heatstroke", "Heatstroke", Inc(Stat.Damage, 10f, Tags.Fire), Inc(Stat.Damage, 25f, Tags.Fire).While(Condition.HotLand), Inc(Stat.CastSpeed, 8f).While(Condition.HotLand)),
                    P2("forge-fire", "Forge Fire", Inc(Stat.Damage, 10f, Tags.Fire), Convert(DamageType.Physical, DamageType.Fire, 20f, Tags.Attack), Inc(Stat.Damage, 12f, Tags.Fire)),
                    P("fire-in-the-blood", "Fire in the Blood", Inc(Stat.Damage, 10f, Tags.Fire), Inc(Stat.Damage, 1f, Tags.Fire).For(Per.Strength, 10f, 40f), Flat(Stat.Strength, 12f)),
                    P("cinder-ward", "Cinder Ward", Flat(Stat.Barrier, 15f), Inc(Stat.Barrier, 20f), Inc(Stat.BarrierRecharge, 15f)),
                    P("pyromania", "Pyromania", Chance(Tags.Burn, 3f), Chance(Tags.Burn, 10f), Inc(Stat.CastSpeed, 6f))),
            },
            Wheels = new[]
            {
                Wheel("crucible", "The Crucible", 1600f, 264f,
                    new[] { Flat(Stat.Penetration, 2f, Tags.Fire), Inc(Stat.Damage, 8f, Tags.Fire) },
                    Gives(Flat(Stat.Penetration, 12f, Tags.Fire), Inc(Stat.Damage, 25f, Tags.Fire), Chance(Tags.Burn, 8f)),
                    N("smelter", "Smelter", Convert(DamageType.Physical, DamageType.Fire, 25f), Inc(Stat.Damage, 10f, Tags.Fire)),
                    N("firewalker", "Firewalker", Resist(Tags.Fire, 15f), Flat(Stat.MaxResistance, 3f, Tags.Fire)),
                    N("blazing-will", "Blazing Will", Inc(Stat.Damage, 20f, Tags.Fire).While(Condition.HasBarrier), Flat(Stat.Barrier, 30f))),
                new Wheel
                {
                    Id = "expanse", Name = "The Expanse", Radius = 3010f * Breadth, Angle = 264.5f,
                    Ring = new[] { Inc(Stat.AreaOfEffect, 4f), Inc(Stat.Damage, 8f, Tags.Area) },
                    Heart = Key("scorched-earth", "Scorched Earth", "Scorcher", TalentGlyph.Scorch, 0f, 0f, "Nothing will grow where you've been.",
                        Rules(Rule.BurningTrail), Rules(Rule.ImmuneToBurn), More(Stat.Damage, -15f, Tags.Hit)),
                    Notables = new[]
                    {
                        N("vast", "Vast", Inc(Stat.AreaOfEffect, 15f), Inc(Stat.Damage, 10f, Tags.Area)),
                        N("blast-radius", "Blast Radius", Inc(Stat.AreaOfEffect, 12f, Tags.Spell), Inc(Stat.ReactionArea, 20f)),
                        N("shockwave", "Shockwave", Inc(Stat.AreaOfEffect, 12f, Tags.Melee), Chance(Tags.Stun, 6f, Tags.Area)),
                    },
                },
            },
            Keystones = new[]
            {
                Key("slow-burn", "Slow Burn", "Slow Burn", TalentGlyph.Ember, 3520f, 258.5f, "The fire isn't the point. What it leaves is.",
                    Rules(Rule.FireFeedsBurn), More(Stat.Damage, 100f, Tags.Burn)),
                Key("thousand-embers", "Thousand Embers", "Kindler", TalentGlyph.Sparks, 3460f, 270f, "One spark is a nuisance. A thousand is a sky on fire.",
                    Rules(Rule.BurnsStack), More(Stat.Damage, -40f, Tags.Burn)),
                Key("phoenix-debt", "Phoenix Debt", "Phoenix", TalentGlyph.Phoenix, 2600f, 265f, "You've done it before. You remember how much it hurts.",
                    Rules(Rule.Rebirth), Inc(Stat.Health, -20f)),
                Key("arcane-edge", "Arcane Edge", "Spellblade", TalentGlyph.Spellblade, 2020f, 274.5f, "The blade is only where the spell leaves you.",
                    Rules(Rule.SpellsArmTheBlade), More(Stat.BookCost, 40f)),
            },
        };

        // The dead and the summoned: the shepherd, who never fights alone, between the battle mage and the frost.
        private static Region Host() => new Region
        {
            Id = "host", Name = "The Host", Adjective = "Hollow", Noun = "Shepherd", Colour = Rgb(140, 160, 128),
            Centre = 295f, Width = 30f, Attribute = Stat.Intellect, Second = Stat.Strength,
            Entry = N("first-rite", "First Rite", Inc(Stat.Health, 12f, Tags.Minion), Inc(Stat.Damage, 12f, Tags.Minion)),
            Lanes = new[]
            {
                Lane("grave", null, 0f, 0f, 1150f, 0.5f,
                    P("marshal", "Marshal", Inc(Stat.Damage, 10f, Tags.Minion), Inc(Stat.Damage, 22f, Tags.Minion), Inc(Stat.MinionDuration, 15f)),
                    P2("gravedigger", "Gravedigger", Inc(Stat.Health, 10f, Tags.Minion), Flat(Stat.MinionCount, 1f), Inc(Stat.Health, 15f, Tags.Minion)),
                    P("bone-splinter", "Bone Splinter", Inc(Stat.Damage, 10f, Tags.Physical), Flat(Stat.Pierce, 1f), Inc(Stat.Damage, 15f, Tags.Physical | Tags.Projectile)),
                    P2("grave-dust", "Grave Dust", Inc(Stat.MinionDuration, 8f), Inc(Stat.MinionDuration, 20f), Inc(Stat.Health, 10f, Tags.Minion))),
                Lane("legion", "grave", -6f, 1150f, 2350f, -1f,
                    P("command", "Command", Inc(Stat.Damage, 10f, Tags.Minion), Inc(Stat.Damage, 30f, Tags.Minion), Inc(Stat.Damage, 2f, Tags.Minion).For(Per.Minions, 1f, 20f)),
                    P2("rally", "Rally", Inc(Stat.MoveSpeed, 5f, Tags.Minion), Inc(Stat.MoveSpeed, 15f, Tags.Minion), Inc(Stat.AttackSpeed, 10f, Tags.Minion)),
                    P("drill-sergeant", "Drill Sergeant", Inc(Stat.AttackSpeed, 3f, Tags.Minion), Inc(Stat.AttackSpeed, 10f, Tags.Minion), Inc(Stat.CastSpeed, 10f, Tags.Minion)),
                    P("sharpened-bones", "Sharpened Bones", Inc(Stat.CritChance, 12f, Tags.Minion), Inc(Stat.CritChance, 30f, Tags.Minion), Flat(Stat.CritMultiplier, 20f, Tags.Minion)),
                    P("vanguard", "Vanguard", Inc(Stat.Damage, 10f, Tags.Minion), Inc(Stat.Damage, 20f, Tags.Minion), Inc(Stat.Health, 20f, Tags.Minion)),
                    P("shieldbearers", "Shieldbearers", Inc(Stat.Health, 10f, Tags.Minion), Inc(Stat.Armour, 20f, Tags.Minion), Inc(Stat.Health, 10f, Tags.Minion))),
                Lane("barrow", "grave", 7f, 1150f, 2300f, 1f,
                    P("old-bones", "Old Bones", Inc(Stat.MinionDuration, 8f), Inc(Stat.MinionDuration, 35f), Inc(Stat.Damage, 15f, Tags.Minion)),
                    P2("bone-ward", "Bone Ward", Flat(Stat.Barrier, 12f), Flat(Stat.Barrier, 6f).For(Per.Minions, 1f, 60f), Flat(Stat.BarrierOnKill, 10f)),
                    P("thick-hides", "Thick Hides", Inc(Stat.Health, 10f, Tags.Minion), Inc(Stat.Health, 25f, Tags.Minion), Flat(Stat.Resistance, 15f, Tags.Elemental | Tags.Minion)),
                    P("the-flock", "The Flock", Flat(Stat.Mana, 12f), Flat(Stat.MinionCount, 1f), Inc(Stat.DamageTaken, -2f).For(Per.Minions, 1f, 16f)),
                    P4("grave-ward", "Grave Ward", Inc(Stat.Armour, 10f, Tags.Minion), Inc(Stat.Armour, 25f, Tags.Minion), Inc(Stat.Health, 15f, Tags.Minion)),
                    P2("barrow-warden", "Barrow Warden", Flat(Stat.Barrier, 12f), Flat(Stat.Barrier, 30f), Inc(Stat.Damage, 10f, Tags.Minion))),
                Lane("warlord", "legion", -5f, 2350f, 3330f, -0.5f,
                    P("banner-bearer", "Banner Bearer", Inc(Stat.PageDuration, 6f, Tags.Buff), Inc(Stat.PageDuration, 20f, Tags.Buff), Inc(Stat.Damage, 12f, Tags.Minion)),
                    P2("lead-from-the-front", "Lead from the Front", Inc(Stat.Damage, 6f), Inc(Stat.Damage, 2f).For(Per.Minions, 1f, 20f), Inc(Stat.Armour, 15f)),
                    P("chanter", "Chanter", Flat(Stat.ManaRegen, 1.5f), Flat(Stat.ManaRegen, 6f), Inc(Stat.Mana, 10f)),
                    P("soul-link", "Soul Link", Inc(Stat.Health, 10f, Tags.Minion), Flat(Stat.HealthLeech, 0.6f, Tags.Minion), Inc(Stat.Health, 15f, Tags.Minion)),
                    P("dread-host", "Dread Host", Inc(Stat.Damage, 10f, Tags.Minion), Inc(Stat.Damage, 25f, Tags.Minion), Inc(Stat.AreaOfEffect, 15f, Tags.Minion))),
                Lane("wake", "barrow", 6f, 2300f, 3400f, 1f,
                    P("death-rattle", "Death Rattle", Flat(Stat.RaiseFallen, 2f), Flat(Stat.RaiseFallen, 8f), Inc(Stat.Damage, 15f, Tags.Minion)),
                    P2("restless-dead", "Restless Dead", Flat(Stat.RaiseFallen, 2f), Flat(Stat.RaiseFallen, 6f), Inc(Stat.MinionDuration, 20f)),
                    P("bone-storm", "Bone Storm", Inc(Stat.Damage, 10f, Tags.Physical | Tags.Projectile), Inc(Stat.Damage, 25f, Tags.Physical | Tags.Projectile), Flat(Stat.Pierce, 1f)),
                    P("corpse-fire", "Corpse Fire", Flat(Stat.ExplodeOnKill, 1f), Flat(Stat.ExplodeOnKill, 5f), Inc(Stat.Damage, 15f, Tags.Fire)),
                    P("spoils", "Spoils", Inc(Stat.ItemRarity, 6f), Inc(Stat.ItemRarity, 20f), Inc(Stat.GoldFind, 25f))),
                Lane("catacomb", "barrow", 12f, 2300f, 3300f, 0.5f,
                    P("whispers", "Whispers", Inc(Stat.Damage, 10f, Tags.Minion), Inc(Stat.Damage, 20f, Tags.Minion), Inc(Stat.CritChance, 15f, Tags.Minion)),
                    P("bone-armour", "Bone Armour", Inc(Stat.Armour, 10f), Inc(Stat.Armour, 15f), Flat(Stat.Barrier, 4f).For(Per.Minions, 1f, 40f)),
                    P2("deathless", "Deathless", Inc(Stat.MinionDuration, 8f), Inc(Stat.MinionDuration, 25f), Inc(Stat.Health, 15f, Tags.Minion)),
                    P("grave-hunger", "Grave Hunger", Flat(Stat.RaiseFallen, 2f), Flat(Stat.RaiseFallen, 5f), Flat(Stat.HealthOnKill, 10f)),
                    P("pallbearer", "Pallbearer", Inc(Stat.Health, 10f, Tags.Minion), Inc(Stat.Health, 20f, Tags.Minion), Inc(Stat.MoveSpeed, 10f, Tags.Minion))),
            },
            Wheels = new[]
            {
                Wheel("bone-circle", "The Bone Circle", 1760f, 296f,
                    new[] { Inc(Stat.Damage, 8f, Tags.Minion), Inc(Stat.Health, 8f, Tags.Minion) },
                    Gives(Flat(Stat.MinionCount, 1f), Inc(Stat.Damage, 20f, Tags.Minion), Inc(Stat.Health, 20f, Tags.Minion)),
                    N("gathering", "Gathering", Flat(Stat.MinionCount, 1f), Inc(Stat.MinionDuration, -15f)),
                    N("mass-grave", "Mass Grave", Flat(Stat.RaiseFallen, 5f), Inc(Stat.Damage, 10f, Tags.Minion)),
                    N("ironbound", "Ironbound", Inc(Stat.Armour, 30f, Tags.Minion), Inc(Stat.Health, 15f, Tags.Minion))),
                Wheel("deathwatch", "The Deathwatch", 2900f, 287f,
                    new[] { Inc(Stat.Health, 8f, Tags.Minion), Flat(Stat.Resistance, 4f, Tags.Elemental | Tags.Minion) },
                    Gives(Inc(Stat.Health, 25f, Tags.Minion), Flat(Stat.Resistance, 15f, Tags.Elemental | Tags.Minion), Inc(Stat.Armour, 20f, Tags.Minion)),
                    N("unyielding-dead", "Unyielding Dead", Inc(Stat.Health, 20f, Tags.Minion), Inc(Stat.MinionDuration, 15f)),
                    N("bound-souls", "Bound Souls", Flat(Stat.HealthLeech, 0.5f, Tags.Minion), Inc(Stat.Damage, 10f, Tags.Minion)),
                    N("grim-phalanx", "Grim Phalanx", Inc(Stat.Armour, 25f, Tags.Minion), Inc(Stat.DamageTaken, -3f).For(Per.Minions, 1f, 15f))),
            },
            Keystones = new[]
            {
                Key("puppeteer", "Puppeteer", "Puppeteer", TalentGlyph.Puppet, 3440f, 290f, "You've never needed to lift a finger, only the strings.",
                    Rules(Rule.Puppeteer), Rules(Rule.NoOwnDamage), Inc(Stat.Health, 10f)),
                Key("legion-of-one", "Legion of One", null, TalentGlyph.Crown, 3520f, 301.5f, "Why keep a crowd? Keep something that doesn't need one.",
                    Set(Stat.MinionCount, 1f), More(Stat.Health, 300f, Tags.Minion), More(Stat.Damage, 150f, Tags.Minion)),
                Key("mutiny", "Mutiny", "Oathbreaker", TalentGlyph.Banner, 2680f, 295.5f, "They were never very loyal. You're simply the last to ask.",
                    Flat(Stat.RaiseFallen, 30f), More(Stat.Damage, -20f, Tags.Minion)),
                Key("litany", "Litany", "Chanter", TalentGlyph.Lantern, 3070f, 296f, "Say it once and it's a word. Keep saying it and it's a wall.",
                    Rules(Rule.ToggleReserves)),
            },
        };
    }
}

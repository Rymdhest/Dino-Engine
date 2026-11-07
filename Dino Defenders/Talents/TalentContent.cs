using Dino_Defenders.Stats;
using OpenTK.Mathematics;
using static Dino_Defenders.Stats.M;

namespace Dino_Defenders.Talents
{
    /// <summary>A cluster hanging off a branch: a couple of small nodes and something worth reaching. A close one is one small node and sits tighter to its branch.</summary>
    public sealed record PodEntry(string Id, string Name, Mod Small, Mod[] Mods, bool Close = false);

    /// <summary>A node that changes how a character plays, rather than how much.</summary>
    public sealed record KeystoneEntry(string Id, string Name, string Lore, string? Title, Mod[] Mods);

    /// <summary>Where two sectors meet, for those who've walked both.</summary>
    public sealed record BridgeEntry(string Name, Mod[] Mods);

    public sealed class Sector
    {
        public string Id = "";
        public string Name = "";
        public string Adjective = "";
        public string Noun = "";
        public Vector3 Colour;
        public Mod[][] Small = Array.Empty<Mod[]>();
        public PodEntry[] Pods = Array.Empty<PodEntry>();
        public KeystoneEntry[] Keystones = Array.Empty<KeystoneEntry>();
    }

    /// <summary>
    /// What the tree is made of. Each sector is a way of fighting; none of them is a class. Pods are in the order they hang from the sector,
    /// nearest the root first, so what's near the top of a list is within reach of a new hero.
    ///
    /// "More" and "less" are kept for keystones and a few other special things. Everything else adds to the common pool.
    /// </summary>
    public static class TalentContent
    {
        private static Mod[] L(params Mod[] mods) => mods;
        private static PodEntry P(string id, string name, Mod small, params Mod[] mods) => new PodEntry(id, name, small, mods);
        private static PodEntry C(string id, string name, Mod small, params Mod[] mods) => new PodEntry(id, name, small, mods, true);
        private static KeystoneEntry K(string id, string name, string lore, string? title, params Mod[] mods) => new KeystoneEntry(id, name, lore, title, mods);
        private static BridgeEntry B(string name, params Mod[] mods) => new BridgeEntry(name, mods);

        public static readonly Sector[] Sectors =
        {
            new Sector
            {
                Id = "forge", Name = "The Forge", Adjective = "Iron", Noun = "Smith", Colour = new Vector3(0.72f, 0.74f, 0.78f),
                Small = new[] { L(Inc(Stat.Damage, 8f, Tags.Melee)), L(Inc(Stat.Armour, 10f)), L(Inc(Stat.Damage, 8f, Tags.Physical)), L(Flat(Stat.Health, 10f)) },
                Pods = new[]
                {
                    C("broad-strokes", "Broad Strokes", Inc(Stat.Damage, 8f, Tags.Attack), Inc(Stat.AreaOfEffect, 20f, Tags.Melee), Inc(Stat.Damage, 15f, Tags.Melee)),
                    C("sundering", "Sundering", Inc(Stat.Damage, 8f, Tags.Physical), Flat(Stat.ArmourIgnore, 12f), Inc(Stat.Damage, 15f, Tags.Physical)),
                    P("heavy-hands", "Heavy Hands", Inc(Stat.Damage, 8f, Tags.Physical), Inc(Stat.Damage, 22f, Tags.Physical), Chance(Tags.Stun, 10f)),
                    C("bedrock", "Bedrock", Inc(Stat.Armour, 10f), Inc(Stat.Armour, 25f), Inc(Stat.PageDuration, 15f, Tags.Buff)),
                    P("tempered", "Tempered", Inc(Stat.Armour, 12f), Inc(Stat.Armour, 30f), Flat(Stat.PhysicalReduction, 3f)),
                    P("hammerfall", "Hammerfall", Inc(Stat.Damage, 8f, Tags.Mace), Inc(Stat.Damage, 30f, Tags.Mace), Chance(Tags.Stun, 15f, Tags.Mace)),
                    P("whetstone", "Whetstone", Inc(Stat.Damage, 8f, Tags.Sword), Inc(Stat.Damage, 26f, Tags.Sword), Inc(Stat.Damage, 26f, Tags.Axe), Flat(Stat.CritChance, 1f, Tags.Sword)),
                    P("long-arms", "Long Arms", Inc(Stat.AreaOfEffect, 6f, Tags.Melee), Flat(Stat.MeleeReach, 1.2f), Inc(Stat.AreaOfEffect, 18f, Tags.Melee)),
                    P("butchers-work", "Butcher's Work", Chance(Tags.Bleed, 4f, Tags.Melee), Chance(Tags.Bleed, 15f, Tags.Melee), Inc(Stat.Damage, 28f, Tags.Bleed)),
                    P("two-hands", "Both Hands", Inc(Stat.Damage, 9f, Tags.TwoHanded), Inc(Stat.Damage, 30f, Tags.TwoHanded), Inc(Stat.AttackSpeed, 7f, Tags.TwoHanded)),
                    C("sweeping-arc", "Sweeping Arc", Inc(Stat.AreaOfEffect, 6f, Tags.Melee), Inc(Stat.Damage, 20f, Tags.Melee | Tags.Area), Flat(Stat.MeleeReach, 0.8f)),
                },
                Keystones = new[]
                {
                    K("deliberate-hand", "Deliberate Hand", "There's no hurry. There never was.", "Executioner",
                        Divert(Stat.AttackSpeed, Stat.Damage, 200f, Tags.Attack), Inc(Stat.Damage, 20f, Tags.Attack)),
                    K("living-anvil", "Living Anvil", "Everything that's struck you has left you harder.", "Anvil",
                        Flat(Stat.ArmourApplies, 50f, Tags.Fire), Flat(Stat.ArmourApplies, 50f, Tags.Cold), Flat(Stat.ArmourApplies, 50f, Tags.Lightning), Inc(Stat.Armour, 20f), Rules(Rule.CannotEvade)),
                    K("shatterpoint", "Shatterpoint", "Everything has a place where it comes apart.", null,
                        More(Stat.Damage, 50f).Against(Condition.Stunned), Chance(Tags.Stun, 25f, Tags.Melee), Rules(Rule.NeverCrit)),
                },
            },
            new Sector
            {
                Id = "bulwark", Name = "The Bulwark", Adjective = "Stalwart", Noun = "Warden", Colour = new Vector3(0.82f, 0.68f, 0.4f),
                Small = new[] { L(Flat(Stat.Health, 12f)), L(Flat(Stat.BlockChance, 1.5f)), L(Inc(Stat.Armour, 8f)), L(Flat(Stat.FlatReduction, 1f)) },
                Pods = new[]
                {
                    C("shield-arm", "Shield Arm", Inc(Stat.Damage, 8f, Tags.Melee), Chance(Tags.Stun, 15f, Tags.Melee), Inc(Stat.Damage, 20f, Tags.Melee).While(Condition.HoldingShield)),
                    C("sanctum", "Sanctum", Inc(Stat.Health, 3f), Inc(Stat.PageDuration, 20f, Tags.Buff), Inc(Stat.Recovery, 12f)),
                    P("shield-wall", "Shield Wall", Flat(Stat.BlockChance, 2f), Flat(Stat.BlockChance, 8f), Inc(Stat.Armour, 30f).While(Condition.HoldingShield)),
                    P("unmoved", "Unmoved", Inc(Stat.StunThreshold, 15f), Inc(Stat.StunThreshold, 50f), Flat(Stat.AilmentAvoid, 15f, Tags.Stun)),
                    P("thick-hide", "Thick Hide", Flat(Stat.FlatReduction, 1f), Flat(Stat.FlatReduction, 5f), Inc(Stat.Health, 6f)),
                    P("briar-coat", "Briar Coat", Flat(Stat.Reflect, 5f), Flat(Stat.Reflect, 20f), Flat(Stat.Armour, 40f)),
                    P("hold-the-line", "Hold the Line", Inc(Stat.Armour, 8f), Inc(Stat.DamageTaken, -8f).While(Condition.Stationary), Inc(Stat.Armour, 40f).While(Condition.Stationary)),
                    P("deflection", "Deflection", Flat(Stat.BlockChance, 1.5f), Flat(Stat.BlockChance, 6f, Tags.Spell), Flat(Stat.MaxBlock, 4f)),
                    P("counterstroke", "Counterstroke", Inc(Stat.Damage, 8f, Tags.Attack), Trigger(TriggerEvent.Block, Books.Shelf.Retaliate), Inc(Stat.Damage, 25f).While(Condition.RecentlyBlocked)),
                    P("bastion", "Bastion", Flat(Stat.Resistance, 3f, Tags.Elemental), Flat(Stat.Resistance, 12f, Tags.Elemental), Flat(Stat.MaxResistance, 2f, Tags.Elemental)),
                },
                Keystones = new[]
                {
                    K("turn-the-blade", "Turn the Blade", "It was never aimed at you, not really.", "Mirror",
                        Rules(Rule.BlockReflects), Flat(Stat.BlockChance, 10f), Flat(Stat.MaxBlock, -25f)),
                    K("shared-burden", "Shared Burden", "They came with you. That was the agreement.", "Shieldbearer",
                        Rules(Rule.Guardian), Inc(Stat.Health, 15f)),
                    K("rooted", "Rooted", "The ground has never lost a fight.", null,
                        More(Stat.DamageTaken, -40f).While(Condition.Stationary), Flat(Stat.HealthRegenPercent, 3f).While(Condition.Stationary), More(Stat.DamageTaken, 25f).While(Condition.Moving)),
                },
            },
            new Sector
            {
                Id = "marrow", Name = "The Marrow", Adjective = "Crimson", Noun = "Sanguine", Colour = new Vector3(0.74f, 0.13f, 0.14f),
                Small = new[] { L(Inc(Stat.Health, 4f)), L(Flat(Stat.HealthRegen, 1f)), L(Inc(Stat.Damage, 7f)), L(Inc(Stat.Recovery, 5f)) },
                Pods = new[]
                {
                    C("long-cuts", "Long Cuts", Chance(Tags.Bleed, 4f), Chance(Tags.Bleed, 12f), Inc(Stat.Damage, 18f, Tags.Bleed)),
                    C("sap", "Sap", Inc(Stat.Health, 3f), Flat(Stat.HealthLeech, 1f), Flat(Stat.ManaLeech, 0.5f)),
                    C("bloodletting", "Bloodletting", Inc(Stat.Damage, 7f, Tags.Bleed), Inc(Stat.Damage, 20f).Against(Condition.Bleeding), Inc(Stat.AilmentDuration, 15f, Tags.Bleed)),
                    P("deep-veins", "Deep Veins", Inc(Stat.Health, 4f), Inc(Stat.Health, 14f), Flat(Stat.HealthRegenPercent, 0.5f)),
                    P("leech", "The Leech", Flat(Stat.HealthLeech, 0.3f), Flat(Stat.HealthLeech, 1.2f), Inc(Stat.Recovery, 12f)),
                    P("gorge", "Gorge", Flat(Stat.HealthOnKill, 4f), Flat(Stat.HealthOnKill, 16f), Flat(Stat.ManaOnKill, 6f)),
                    P("open-wounds", "Open Wounds", Chance(Tags.Bleed, 5f), Inc(Stat.Damage, 30f, Tags.Bleed), Inc(Stat.AilmentDuration, 25f, Tags.Bleed)),
                    P("frenzy", "Frenzy", Inc(Stat.Damage, 8f).While(Condition.LowHealth), Inc(Stat.Damage, 35f).While(Condition.LowHealth), Inc(Stat.AttackSpeed, 12f).While(Condition.LowHealth)),
                    P("strong-heart", "Strong Heart", Flat(Stat.HealthRegen, 1.5f), Flat(Stat.HealthRegenPercent, 1f), Inc(Stat.FlaskEffect, 20f)),
                    P("vital-spark", "Vital Spark", Inc(Stat.Damage, 7f).While(Condition.FullHealth), Inc(Stat.Damage, 30f).While(Condition.FullHealth), Inc(Stat.MoveSpeed, 6f).While(Condition.FullHealth)),
                },
                Keystones = new[]
                {
                    K("blood-tithe", "Blood Tithe", "Everything has a price, and you've stopped pretending otherwise.", "Tithebearer",
                        Rules(Rule.CostsHealth), More(Stat.Damage, 2f).For(Per.HealthSpent, 1f, 60f), Inc(Stat.Health, 10f)),
                    K("debtor", "Debtor", "It'll have to be paid eventually. Not today, though.", "Debtor",
                        Rules(Rule.DeferredDamage), Rules(Rule.NoLeech), Inc(Stat.Recovery, 15f)),
                    K("overflowing-cup", "Overflowing Cup", "More than you can hold is still yours.", null,
                        Rules(Rule.OverhealBarrier), Inc(Stat.Recovery, 30f)),
                },
            },
            new Sector
            {
                Id = "host", Name = "The Host", Adjective = "Hollow", Noun = "Shepherd", Colour = new Vector3(0.55f, 0.62f, 0.5f),
                Small = new[] { L(Inc(Stat.Damage, 9f, Tags.Minion)), L(Inc(Stat.Health, 9f, Tags.Minion)), L(Inc(Stat.AttackSpeed, 4f, Tags.Minion)), L(Flat(Stat.Mana, 8f)) },
                Pods = new[]
                {
                    C("marshal", "Marshal", Inc(Stat.Damage, 9f, Tags.Minion), Inc(Stat.Damage, 25f, Tags.Minion), Inc(Stat.MinionDuration, 20f)),
                    C("bone-splinter", "Bone Splinter", Inc(Stat.Damage, 8f, Tags.Physical), Flat(Stat.Pierce, 1f), Inc(Stat.Damage, 15f, Tags.Physical | Tags.Projectile)),
                    P("gravedigger", "Gravedigger", Inc(Stat.Health, 9f, Tags.Minion), Flat(Stat.MinionCount, 1f), Inc(Stat.Health, 20f, Tags.Minion)),
                    P("rally", "Rally", Inc(Stat.MoveSpeed, 5f, Tags.Minion), Inc(Stat.MoveSpeed, 20f, Tags.Minion), Inc(Stat.AttackSpeed, 15f, Tags.Minion)),
                    P("old-bones", "Old Bones", Inc(Stat.MinionDuration, 12f), Inc(Stat.MinionDuration, 50f), Inc(Stat.Damage, 20f, Tags.Minion)),
                    P("command", "Command", Inc(Stat.Damage, 9f, Tags.Minion), Inc(Stat.Damage, 35f, Tags.Minion), Inc(Stat.Damage, 2f, Tags.Minion).For(Per.Minions, 1f, 20f)),
                    P("flock", "The Flock", Flat(Stat.Mana, 8f), Flat(Stat.MinionCount, 1f), Inc(Stat.DamageTaken, -2f).For(Per.Minions, 1f, 16f)),
                    P("death-rattle", "Death Rattle", Flat(Stat.RaiseFallen, 2f), Flat(Stat.RaiseFallen, 8f), Inc(Stat.Damage, 15f, Tags.Minion)),
                    P("spoils", "Spoils", Inc(Stat.ItemRarity, 6f), Inc(Stat.ItemRarity, 25f), Inc(Stat.GoldFind, 25f)),
                    P("bone-ward", "Bone Ward", Flat(Stat.Barrier, 12f), Flat(Stat.Barrier, 6f).For(Per.Minions, 1f, 60f), Flat(Stat.BarrierOnKill, 12f)),
                },
                Keystones = new[]
                {
                    K("puppeteer", "Puppeteer", "You've never needed to lift a finger, only the strings.", "Puppeteer",
                        Rules(Rule.Puppeteer), Rules(Rule.NoOwnDamage), Inc(Stat.Health, 10f)),
                    K("mutiny", "Mutiny", "They were never very loyal. You're simply the last to ask.", "Oathbreaker",
                        Flat(Stat.RaiseFallen, 30f), More(Stat.Damage, -20f, Tags.Minion)),
                    K("legion-of-one", "Legion of One", "Why keep a crowd? Keep something that doesn't need one.", null,
                        Set(Stat.MinionCount, 1f), More(Stat.Health, 300f, Tags.Minion), More(Stat.Damage, 150f, Tags.Minion)),
                },
            },
            new Sector
            {
                Id = "mire", Name = "The Mire", Adjective = "Festering", Noun = "Plaguebearer", Colour = new Vector3(0.42f, 0.72f, 0.2f),
                Small = new[] { L(Inc(Stat.Damage, 9f, Tags.Poison)), L(Inc(Stat.Damage, 9f, Tags.DamageOverTime)), L(Chance(Tags.Venom, 4f)), L(Flat(Stat.Resistance, 4f, Tags.Poison)) },
                Pods = new[]
                {
                    C("spittle", "Spittle", Inc(Stat.Damage, 9f, Tags.Poison), Chance(Tags.Venom, 12f, Tags.Projectile), Inc(Stat.ProjectileSpeed, 10f)),
                    C("foul-air", "Foul Air", Inc(Stat.Damage, 9f, Tags.DamageOverTime), Inc(Stat.PageDuration, 20f, Tags.Zone), Inc(Stat.Potency, 12f, Tags.Zone)),
                    P("toxic-blood", "Toxic Blood", Inc(Stat.Damage, 9f, Tags.Venom), Inc(Stat.Damage, 30f, Tags.Venom), Chance(Tags.Venom, 10f)),
                    P("slow-sickness", "Slow Sickness", Inc(Stat.AilmentDuration, 8f), Inc(Stat.AilmentDuration, 30f), Inc(Stat.Damage, 15f, Tags.DamageOverTime)),
                    P("rot", "Rot", Inc(Stat.Damage, 9f, Tags.DamageOverTime), Inc(Stat.Damage, 32f, Tags.DamageOverTime), Flat(Stat.Penetration, 8f, Tags.Poison)),
                    P("immune", "Antibodies", Flat(Stat.Resistance, 5f, Tags.Poison), Flat(Stat.Resistance, 20f, Tags.Poison), Flat(Stat.AilmentAvoid, 25f, Tags.Venom), Inc(Stat.AilmentTaken, -25f)),
                    P("mudslick", "Mudslick", Inc(Stat.Evasion, 10f), Inc(Stat.Evasion, 30f).While(Condition.WetLand), Inc(Stat.MoveSpeed, 10f).While(Condition.WetLand)),
                    P("feeding-on-the-sick", "Feeding on the Sick", Flat(Stat.HealthOnHit, 1f), Inc(Stat.Damage, 30f).Against(Condition.Envenomed), Flat(Stat.HealthOnKill, 12f)),
                    P("spreading-stain", "Spreading Stain", Inc(Stat.AreaOfEffect, 6f), Inc(Stat.AreaOfEffect, 25f, Tags.Zone), Inc(Stat.PageDuration, 25f, Tags.Zone)),
                    P("weakness", "Weakness", Inc(Stat.Damage, 9f, Tags.Poison), Flat(Stat.Penetration, 12f, Tags.Poison), Inc(Stat.Damage, 20f, Tags.Poison)),
                },
                Keystones = new[]
                {
                    K("patient-death", "Patient Death", "There's no need to hurry anyone along.", "Patient Death",
                        More(Stat.Damage, -60f, Tags.Hit), Rules(Rule.EndlessVenom), Inc(Stat.AilmentDuration, 100f, Tags.Venom), Chance(Tags.Venom, 30f)),
                    K("carrier", "Carrier", "You feel perfectly well. It's everyone else.", "Carrier",
                        Rules(Rule.Carrier), Inc(Stat.Health, 10f)),
                    K("contagion", "Contagion", "It's never just the one.", null,
                        Rules(Rule.AilmentsLeap), Inc(Stat.Damage, 20f, Tags.DamageOverTime)),
                },
            },
            new Sector
            {
                Id = "pyre", Name = "The Pyre", Adjective = "Ember", Noun = "Pyromancer", Colour = new Vector3(0.95f, 0.42f, 0.1f),
                Small = new[] { L(Inc(Stat.Damage, 9f, Tags.Fire)), L(Chance(Tags.Burn, 3f)), L(Inc(Stat.AreaOfEffect, 5f)), L(Flat(Stat.Resistance, 4f, Tags.Fire)) },
                Pods = new[]
                {
                    C("kindled-bolt", "Kindled Bolt", Inc(Stat.Damage, 9f, Tags.Fire), Inc(Stat.Damage, 22f, Tags.Fire | Tags.Projectile), Chance(Tags.Burn, 8f, Tags.Projectile)),
                    C("wreath-of-flame", "Wreath of Flame", Inc(Stat.AreaOfEffect, 5f), Inc(Stat.Damage, 22f, Tags.Fire | Tags.Area), Chance(Tags.Burn, 8f, Tags.Area)),
                    C("falling-embers", "Falling Embers", Inc(Stat.Damage, 9f, Tags.Fire), Inc(Stat.Potency, 20f, Tags.Falling), Inc(Stat.AreaOfEffect, 10f, Tags.Turned)),
                    P("kindling", "Kindling", Chance(Tags.Burn, 4f), Chance(Tags.Burn, 15f), Inc(Stat.Damage, 26f, Tags.Burn)),
                    P("searing-heat", "Searing Heat", Inc(Stat.Damage, 9f, Tags.Fire), Inc(Stat.Damage, 30f, Tags.Fire), Flat(Stat.Penetration, 6f, Tags.Fire)),
                    P("wide-flame", "Wide Flame", Inc(Stat.AreaOfEffect, 6f), Inc(Stat.AreaOfEffect, 25f), Inc(Stat.Damage, 15f, Tags.Area)),
                    P("long-burn", "Long Burn", Inc(Stat.AilmentDuration, 8f, Tags.Burn), Inc(Stat.AilmentDuration, 35f, Tags.Burn), Inc(Stat.Damage, 15f, Tags.Burn)),
                    P("heatstroke", "Heatstroke", Inc(Stat.Damage, 8f).While(Condition.HotLand), Inc(Stat.Damage, 30f, Tags.Fire).While(Condition.HotLand), Inc(Stat.CastSpeed, 10f).While(Condition.HotLand)),
                    P("burn-it-down", "Burn it Down", Inc(Stat.Damage, 9f, Tags.Fire), Inc(Stat.Damage, 35f).Against(Condition.Burning), Flat(Stat.ExplodeOnKill, 4f)),
                    P("ash-skin", "Ash Skin", Flat(Stat.Resistance, 5f, Tags.Fire), Flat(Stat.Resistance, 18f, Tags.Fire), Flat(Stat.MaxResistance, 3f, Tags.Fire), TakenAs(DamageType.Cold, DamageType.Fire, 25f)),
                },
                Keystones = new[]
                {
                    K("slow-burn", "Slow Burn", "The fire isn't the point. What it leaves is.", "Slow Burn",
                        Rules(Rule.FireFeedsBurn), More(Stat.Damage, 100f, Tags.Burn)),
                    K("scorched-earth", "Scorched Earth", "Nothing will grow where you've been.", "Scorcher",
                        Rules(Rule.BurningTrail), Rules(Rule.ImmuneToBurn), More(Stat.Damage, -15f, Tags.Hit)),
                    K("phoenix-debt", "Phoenix Debt", "You've done it before. You remember how much it hurts.", "Phoenix",
                        Rules(Rule.Rebirth), Inc(Stat.Health, -20f)),
                },
            },
            new Sector
            {
                Id = "arcanum", Name = "The Arcanum", Adjective = "Arcane", Noun = "Magus", Colour = new Vector3(0.6f, 0.34f, 0.9f),
                Small = new[] { L(Inc(Stat.Damage, 8f, Tags.Spell)), L(Inc(Stat.Mana, 7f)), L(Inc(Stat.CastSpeed, 3f)), L(Flat(Stat.Barrier, 10f)) },
                Pods = new[]
                {
                    C("plain-force", "Plain Force", Inc(Stat.Damage, 8f, Tags.Magic), Inc(Stat.Damage, 25f, Tags.Magic), Chance(Tags.Hex, 8f)),
                    C("second-reading", "Second Reading", Inc(Stat.Damage, 8f, Tags.Spell), Inc(Stat.Potency, 15f, Tags.Turned), Inc(Stat.BookCost, -8f)),
                    C("fluent-hand", "Fluent Hand", Inc(Stat.CooldownRecovery, 4f), Inc(Stat.BookCost, -10f, Tags.Movement), Inc(Stat.CastSpeed, 8f)),
                    P("deep-well", "Deep Well", Inc(Stat.Mana, 8f), Inc(Stat.Mana, 25f), Flat(Stat.ManaRegenPercent, 0.4f)),
                    P("spellweaver", "Spellweaver", Inc(Stat.CastSpeed, 3f), Inc(Stat.CastSpeed, 12f), Inc(Stat.Damage, 20f, Tags.Spell)),
                    P("ward-of-glass", "Ward of Glass", Flat(Stat.Barrier, 12f), Inc(Stat.Barrier, 35f), Inc(Stat.BarrierRecharge, 30f), TakenAs(DamageType.Physical, DamageType.Magic, 15f)),
                    P("unravelling", "Unravelling", Chance(Tags.Hex, 4f), Chance(Tags.Hex, 15f), Inc(Stat.AilmentEffect, 30f, Tags.Hex)),
                    P("economy-of-words", "Economy of Words", Inc(Stat.BookCost, -3f), Inc(Stat.BookCost, -14f), Inc(Stat.CooldownRecovery, 10f)),
                    P("sharp-mind", "Sharp Mind", Flat(Stat.CritChance, 0.5f, Tags.Spell), Inc(Stat.CritChance, 40f, Tags.Spell), Flat(Stat.CritMultiplier, 25f, Tags.Spell)),
                    P("full-of-it", "Brimming", Inc(Stat.Damage, 8f).While(Condition.FullMana), Inc(Stat.Damage, 35f, Tags.Spell).While(Condition.FullMana), Inc(Stat.Potency, 10f, Tags.Magic)),
                },
                Keystones = new[]
                {
                    K("manaskin", "Manaskin", "You stopped being a body some time ago.", "Manaskin",
                        Flat(Stat.TakenFromMana, 60f), Inc(Stat.Mana, 50f), Inc(Stat.Health, -30f)),
                    K("echoing-word", "Echoing Word", "Say it again, and they'll believe it.", "Cantor",
                        Rules(Rule.SpellEcho), Inc(Stat.BookCost, 50f, Tags.Spell)),
                    K("palimpsest", "Palimpsest", "Written over and over, and only ever paid for once.", "Palimpsest",
                        Rules(Rule.OneCost), More(Stat.Potency, -20f)),
                },
            },
            new Sector
            {
                Id = "storm", Name = "The Storm", Adjective = "Storm", Noun = "Caller", Colour = new Vector3(0.45f, 0.72f, 1f),
                Small = new[] { L(Inc(Stat.Damage, 9f, Tags.Lightning)), L(Inc(Stat.CastSpeed, 3f)), L(Inc(Stat.MoveSpeed, 2f)), L(Flat(Stat.Resistance, 4f, Tags.Lightning)) },
                Pods = new[]
                {
                    C("forked-path", "Forked Path", Inc(Stat.Damage, 9f, Tags.Lightning), Inc(Stat.Damage, 15f, Tags.Lightning), Inc(Stat.Damage, 15f, Tags.Turned)),
                    C("rolling-thunder", "Rolling Thunder", Inc(Stat.PageDuration, 6f), Inc(Stat.Potency, 15f, Tags.Zone), Inc(Stat.Damage, 15f, Tags.Lightning)),
                    C("quickened", "Quickened", Inc(Stat.MoveSpeed, 2f), Inc(Stat.MoveSpeed, 8f), Inc(Stat.CastSpeed, 8f)),
                    P("high-voltage", "High Voltage", Inc(Stat.Damage, 9f, Tags.Lightning), Inc(Stat.Damage, 30f, Tags.Lightning), Flat(Stat.Penetration, 6f, Tags.Lightning)),
                    P("shocking-touch", "Shocking Touch", Chance(Tags.Shock, 4f), Chance(Tags.Shock, 15f), Inc(Stat.AilmentEffect, 30f, Tags.Shock)),
                    P("arc-flash", "Arc Flash", Inc(Stat.Damage, 9f, Tags.Chaining), Flat(Stat.Chain, 1f), Inc(Stat.Damage, 15f, Tags.Chaining)),
                    P("tailwind", "Tailwind", Inc(Stat.MoveSpeed, 3f), Inc(Stat.MoveSpeed, 10f), Inc(Stat.DodgeCooldown, -15f)),
                    P("thundercrack", "Thundercrack", Inc(Stat.CritChance, 12f, Tags.Lightning), Inc(Stat.CritChance, 50f, Tags.Lightning), Flat(Stat.CritMultiplier, 30f, Tags.Lightning)),
                    P("rising-storm", "Rising Storm", Inc(Stat.Damage, 8f).While(Condition.HighLand), Inc(Stat.Damage, 30f, Tags.Lightning).While(Condition.HighLand), Inc(Stat.CastSpeed, 10f).While(Condition.HighLand)),
                    P("grounding", "Grounding", Flat(Stat.Resistance, 5f, Tags.Lightning), Flat(Stat.Resistance, 18f, Tags.Lightning), TakenAs(DamageType.Fire, DamageType.Lightning, 25f)),
                },
                Keystones = new[]
                {
                    K("static-stride", "Static Stride", "The world's a long carpet, and you're wearing socks.", "Stormrunner",
                        Rules(Rule.StaticCharge), Inc(Stat.MoveSpeed, 10f)),
                    K("forked-sky", "Forked Sky", "It finds a way, and it keeps finding them.", null,
                        Flat(Stat.Chain, 2f), Rules(Rule.ChainsGrow), More(Stat.Damage, -25f, Tags.Chaining)),
                    K("lightning-rod", "Lightning Rod", "You'd be surprised what you can get used to.", "Lightning Rod",
                        Rules(Rule.AlwaysShocked), More(Stat.Damage, 40f, Tags.Lightning)),
                },
            },
            new Sector
            {
                Id = "winter", Name = "The Long Winter", Adjective = "Frost", Noun = "Rimewalker", Colour = new Vector3(0.62f, 0.9f, 0.96f),
                Small = new[] { L(Inc(Stat.Damage, 9f, Tags.Cold)), L(Inc(Stat.Barrier, 6f)), L(Inc(Stat.AilmentEffect, 6f, Tags.Chill)), L(Flat(Stat.Resistance, 4f, Tags.Cold)) },
                Pods = new[]
                {
                    C("splinters", "Splinters", Inc(Stat.Damage, 9f, Tags.Cold), Inc(Stat.Damage, 22f, Tags.Cold | Tags.Projectile), Inc(Stat.ProjectileSpeed, 10f)),
                    C("white-ring", "White Ring", Inc(Stat.AilmentEffect, 6f, Tags.Chill), Inc(Stat.Damage, 22f, Tags.Cold | Tags.Area), Inc(Stat.AreaOfEffect, 10f)),
                    C("long-ice", "Long Ice", Inc(Stat.Damage, 9f, Tags.Cold), Chance(Tags.Freeze, 8f), Inc(Stat.Damage, 15f, Tags.Cold).Against(Condition.Chilled)),
                    P("biting-cold", "Biting Cold", Inc(Stat.Damage, 9f, Tags.Cold), Inc(Stat.Damage, 30f, Tags.Cold), Flat(Stat.Penetration, 6f, Tags.Cold)),
                    P("permafrost", "Permafrost", Chance(Tags.Freeze, 3f), Chance(Tags.Freeze, 12f), Inc(Stat.AilmentDuration, 30f, Tags.Freeze)),
                    P("numbing", "Numbing", Inc(Stat.AilmentEffect, 8f, Tags.Chill), Inc(Stat.AilmentEffect, 30f, Tags.Chill), Inc(Stat.AilmentDuration, 30f, Tags.Chill)),
                    P("rime-shell", "Rime Shell", Flat(Stat.Barrier, 14f), Inc(Stat.Barrier, 30f), Flat(Stat.BarrierDelay, -0.6f)),
                    P("cold-snap", "Cold Snap", Inc(Stat.Damage, 9f, Tags.Cold), Inc(Stat.Damage, 35f).Against(Condition.Chilled), Inc(Stat.Damage, 35f).Against(Condition.Frozen)),
                    P("snowbound", "Snowbound", Inc(Stat.Damage, 8f).While(Condition.ColdLand), Inc(Stat.Damage, 30f, Tags.Cold).While(Condition.ColdLand), Inc(Stat.Barrier, 25f).While(Condition.ColdLand)),
                    P("thick-fur", "Thick Fur", Flat(Stat.Resistance, 5f, Tags.Cold), Flat(Stat.Resistance, 18f, Tags.Cold), Rules(Rule.CannotBeChilled)),
                },
                Keystones = new[]
                {
                    K("still-point", "Still Point", "Everything stops. It doesn't stop being owed.", "Still Point",
                        Rules(Rule.FreezeStores), Chance(Tags.Freeze, 15f)),
                    K("deep-winter", "Deep Winter", "It doesn't end. You only hope to outlast it.", "Long Winter",
                        Rules(Rule.ChillNeverFades), Rules(Rule.CannotBeChilled), Rules(Rule.CannotBeFrozen), Inc(Stat.AilmentEffect, 60f, Tags.Chill)),
                    K("cold-comfort", "Cold Comfort", "You don't heal any more. You just stop being hurt.", null,
                        Inc(Stat.Barrier, 60f), Flat(Stat.BarrierDelay, -2f), Rules(Rule.NoRegen)),
                },
            },
            new Sector
            {
                Id = "wilds", Name = "The Wilds", Adjective = "Wild", Noun = "Warden of the Land", Colour = new Vector3(0.28f, 0.62f, 0.5f),
                Small = new[] { L(Inc(Stat.MoveSpeed, 2f)), L(Inc(Stat.Evasion, 8f)), L(Flat(Stat.Health, 8f), Flat(Stat.Mana, 4f)), L(Inc(Stat.Damage, 6f)) },
                Pods = new[]
                {
                    C("wellspring", "Wellspring", Inc(Stat.MoveSpeed, 2f), Inc(Stat.Recovery, 12f).While(Condition.InWater), Inc(Stat.Potency, 15f).While(Condition.InWater)),
                    C("whistle", "Whistle", Inc(Stat.Damage, 9f, Tags.Minion), Inc(Stat.MoveSpeed, 15f, Tags.Minion), Inc(Stat.Health, 15f, Tags.Minion)),
                    P("forest-floor", "Forest Floor", Inc(Stat.Evasion, 10f), Inc(Stat.Evasion, 40f).While(Condition.UnderTrees), Flat(Stat.Stealth, 20f).While(Condition.UnderTrees)),
                    P("river-sense", "River Sense", Inc(Stat.MoveSpeed, 3f), Inc(Stat.MoveSpeed, 15f).While(Condition.InWater), Inc(Stat.Damage, 25f).While(Condition.InWater)),
                    P("the-long-road", "The Long Road", Inc(Stat.MoveSpeed, 3f), Inc(Stat.MoveSpeed, 18f).While(Condition.OnRoad), Inc(Stat.Damage, 20f).While(Condition.OnRoad)),
                    P("dusk", "Dusk", Flat(Stat.Stealth, 4f), Flat(Stat.Stealth, 18f), Inc(Stat.Damage, 35f).While(Condition.InDarkness)),
                    P("pilgrim", "Pilgrim", Inc(Stat.Damage, 6f), Inc(Stat.Damage, 3f).For(Per.ShrinesKindled, 1f, 45f), Flat(Stat.Health, 8f).For(Per.ShrinesKindled, 1f, 120f)),
                    P("lay-of-the-land", "Lay of the Land", Inc(Stat.Damage, 6f), Inc(Stat.Damage, 18f).While(Condition.HotLand), Inc(Stat.Damage, 18f).While(Condition.ColdLand), Inc(Stat.Damage, 18f).While(Condition.WetLand), Inc(Stat.Damage, 18f).While(Condition.HighLand)),
                    P("rain-dance", "Rain Dance", Inc(Stat.ReactionDamage, 12f), Inc(Stat.ReactionDamage, 40f), Inc(Stat.ReactionArea, 30f)),
                    P("forager", "Forager", Inc(Stat.FlaskEffect, 8f), Inc(Stat.FlaskEffect, 30f), Inc(Stat.Recovery, 15f).While(Condition.UnderTrees)),
                },
                Keystones = new[]
                {
                    K("tidewalker", "Tidewalker", "You've come to think of land as a long delay between seas.", "Tidewalker",
                        More(Stat.Damage, 50f).While(Condition.InWater), Flat(Stat.HealthRegenPercent, 3f).While(Condition.InWater), More(Stat.Damage, -15f).Unless(Condition.InWater)),
                    K("child-of-the-wood", "Child of the Wood", "They've stopped noticing you. It's the nicest thing they've ever done.", "Child of the Wood",
                        More(Stat.Evasion, 40f).While(Condition.UnderTrees), Inc(Stat.MoveSpeed, 30f).While(Condition.UnderTrees), More(Stat.Damage, 30f).While(Condition.UnderTrees), More(Stat.Damage, -15f).Unless(Condition.UnderTrees)),
                    K("spirit-of-the-place", "Spirit of the Place", "You don't bring anything to a fight. It's all already here.", "Genius Loci",
                        Convert(DamageType.Physical, DamageType.Fire, 100f).While(Condition.HotLand), Convert(DamageType.Physical, DamageType.Cold, 100f).While(Condition.ColdLand),
                        Convert(DamageType.Physical, DamageType.Poison, 100f).While(Condition.WetLand), Convert(DamageType.Physical, DamageType.Lightning, 100f).While(Condition.HighLand), Inc(Stat.Damage, 30f)),
                },
            },
            new Sector
            {
                Id = "hunt", Name = "The Hunt", Adjective = "Hunting", Noun = "Stalker", Colour = new Vector3(0.62f, 0.8f, 0.3f),
                Small = new[] { L(Inc(Stat.Damage, 8f, Tags.Projectile)), L(Inc(Stat.Evasion, 10f)), L(Inc(Stat.AttackSpeed, 3f)), L(Inc(Stat.ProjectileSpeed, 6f)) },
                Pods = new[]
                {
                    C("steady-draw", "Steady Draw", Inc(Stat.Damage, 8f, Tags.Projectile), Inc(Stat.Damage, 22f, Tags.Projectile), Inc(Stat.Accuracy, 15f)),
                    C("loose-together", "Loose Together", Inc(Stat.ProjectileSpeed, 6f), Flat(Stat.ProjectileCount, 1f, Tags.Bow), Inc(Stat.Damage, -8f, Tags.Bow)),
                    P("fletching", "Fletching", Inc(Stat.Damage, 8f, Tags.Bow), Inc(Stat.Damage, 30f, Tags.Bow), Inc(Stat.AttackSpeed, 8f, Tags.Bow)),
                    P("through-and-through", "Through and Through", Inc(Stat.ProjectileSpeed, 6f), Flat(Stat.Pierce, 1f), Inc(Stat.Damage, 15f, Tags.Projectile)),
                    P("extra-quiver", "Extra Quiver", Inc(Stat.AttackSpeed, 3f), Flat(Stat.ProjectileCount, 1f), Inc(Stat.Damage, -10f, Tags.Projectile)),
                    P("light-foot", "Light Foot", Inc(Stat.Evasion, 10f), Inc(Stat.Evasion, 30f), Inc(Stat.MoveSpeed, 6f)),
                    P("dead-eye", "Dead Eye", Inc(Stat.Accuracy, 12f), Inc(Stat.Accuracy, 35f), Flat(Stat.CritChance, 1.5f, Tags.Projectile)),
                    P("skirmisher", "Skirmisher", Inc(Stat.Damage, 8f).While(Condition.Moving), Inc(Stat.Damage, 28f, Tags.Projectile).While(Condition.Moving), Inc(Stat.Evasion, 25f).While(Condition.Moving)),
                    P("first-blood", "First Blood", Inc(Stat.Damage, 8f, Tags.Projectile), Inc(Stat.Damage, 40f).Against(Condition.FullHealth), Chance(Tags.Bleed, 10f, Tags.Projectile)),
                    P("hawkeye", "Hawkeye", Inc(Stat.ProjectileSpeed, 8f), Inc(Stat.ProjectileSpeed, 30f), Inc(Stat.Damage, 20f, Tags.Projectile)),
                },
                Keystones = new[]
                {
                    K("long-shot", "Long Shot", "They always think they're safe at that range.", "Long Shot",
                        Rules(Rule.LongShot)),
                    K("peakborn", "Peakborn", "You've always looked down on people.", "Peakborn",
                        Rules(Rule.HighGround), Inc(Stat.Damage, 10f, Tags.Projectile)),
                    K("last-chapter", "The Last Chapter", "It was always going to end like this.", "Author",
                        More(Stat.Potency, 60f, Tags.Closing), More(Stat.Potency, -30f, Tags.Opening)),
                },
            },
            new Sector
            {
                Id = "knave", Name = "The Knave", Adjective = "Sly", Noun = "Knave", Colour = new Vector3(0.7f, 0.5f, 0.76f),
                Small = new[] { L(Flat(Stat.CritMultiplier, 6f)), L(Inc(Stat.CritChance, 10f)), L(Inc(Stat.AttackSpeed, 3f)), L(Inc(Stat.Evasion, 8f)) },
                Pods = new[]
                {
                    C("lunging-step", "Lunging Step", Inc(Stat.AttackSpeed, 3f), Inc(Stat.Potency, 20f, Tags.Movement), Inc(Stat.BookCost, -15f, Tags.Movement)),
                    C("balanced-blades", "Balanced Blades", Inc(Stat.CritChance, 10f), Inc(Stat.Damage, 20f, Tags.Projectile | Tags.Attack), Flat(Stat.CritChance, 1f, Tags.Projectile)),
                    P("twist-the-knife", "Twist the Knife", Inc(Stat.Damage, 8f, Tags.Dagger), Inc(Stat.Damage, 30f, Tags.Dagger), Flat(Stat.CritMultiplier, 20f, Tags.Dagger)),
                    P("cold-calculation", "Cold Calculation", Inc(Stat.CritChance, 10f), Inc(Stat.CritChance, 40f), Flat(Stat.CritChance, 1f)),
                    P("cut-throat", "Cut Throat", Flat(Stat.CritMultiplier, 8f), Flat(Stat.CritMultiplier, 35f), Inc(Stat.Damage, 15f).While(Condition.RecentlyCrit)),
                    P("sidestep", "Sidestep", Inc(Stat.Evasion, 8f), Inc(Stat.DodgeCooldown, -20f), Inc(Stat.DodgeSpeed, 15f)),
                    P("bait-and-switch", "Bait and Switch", Inc(Stat.Damage, 8f).While(Condition.RecentlyDodged), Rules(Rule.DodgeCrit), Inc(Stat.Damage, 20f).While(Condition.RecentlyDodged)),
                    P("poisoned-blade", "Poisoned Blade", Chance(Tags.Venom, 4f, Tags.Melee), Chance(Tags.Venom, 18f, Tags.Attack), Inc(Stat.Damage, 20f, Tags.Venom)),
                    P("shadow-work", "Shadow Work", Flat(Stat.Stealth, 4f), Flat(Stat.Stealth, 15f), Inc(Stat.Damage, 30f).Against(Condition.FullHealth)),
                    P("pickpocket", "Pickpocket", Inc(Stat.GoldFind, 8f), Inc(Stat.GoldFind, 30f), Inc(Stat.ItemRarity, 15f)),
                },
                Keystones = new[]
                {
                    K("loaded-dice", "Loaded Dice", "Luck is a thing that happens to other people.", "Cardsharp",
                        Set(Stat.CritChance, 50f), Divert(Stat.CritChance, Stat.CritMultiplier, 100f)),
                    K("final-word", "Final Word", "Nobody's listening by then.", "Reaper",
                        Flat(Stat.Execute, 25f), More(Stat.Damage, -25f, Tags.Hit)),
                    K("wild-margins", "Wild Margins", "Nobody reads the notes. That's where you hide things.", "Marginalist",
                        More(Stat.Potency, 40f, Tags.Wild), Inc(Stat.BookCost, 25f)),
                },
            },
        };

        /// <summary>What every node in a sector gives on top of what it says, as strength, dexterity and intellect. Each adds up to the same, so that no way of fighting is worth more points.</summary>
        public static Mod[] AttributesOf(string sector)
        {
            (int strength, int dexterity, int intellect) = sector switch
            {
                "forge" or "bulwark" or "marrow" => (Rulebook.NodeAttribute, 0, 0),
                "hunt" or "knave" => (0, Rulebook.NodeAttribute, 0),
                "host" or "pyre" or "arcanum" or "winter" => (0, 0, Rulebook.NodeAttribute),
                "mire" => (0, Rulebook.NodeAttribute - 2, 2),
                "storm" => (0, 2, Rulebook.NodeAttribute - 2),
                _ => (2, Rulebook.NodeAttribute - 2, 0),
            };

            var mods = new List<Mod>();
            if (strength > 0) mods.Add(Flat(Stat.Strength, strength));
            if (dexterity > 0) mods.Add(Flat(Stat.Dexterity, dexterity));
            if (intellect > 0) mods.Add(Flat(Stat.Intellect, intellect));
            return mods.ToArray();
        }

        /// <summary>The middle of each ring between one sector and the next, in the order of <see cref="Sectors"/>.</summary>
        public static readonly BridgeEntry[] InnerBridges =
        {
            B("Iron Discipline", Inc(Stat.Armour, 20f), Inc(Stat.Damage, 15f, Tags.Melee).While(Condition.HoldingShield)),
            B("Scar Tissue", Flat(Stat.FlatReduction, 3f), Inc(Stat.Health, 8f)),
            B("Blood Pact", Inc(Stat.Health, 18f, Tags.Minion), Inc(Stat.Health, 6f)),
            B("Grave Rot", Chance(Tags.Venom, 15f, Tags.Minion), Inc(Stat.Damage, 20f, Tags.Minion)),
            B("Flashpoint", Inc(Stat.ReactionDamage, 40f), Inc(Stat.Damage, 15f, Tags.Burn), Inc(Stat.Damage, 15f, Tags.Venom)),
            B("Kindled Mind", Inc(Stat.Damage, 15f, Tags.Fire), Inc(Stat.Mana, 10f)),
            B("Quick Study", Inc(Stat.CastSpeed, 8f), Flat(Stat.ManaRegenPercent, 0.3f)),
            B("Static Frost", Inc(Stat.Damage, 15f, Tags.Cold), Inc(Stat.Damage, 15f, Tags.Lightning)),
            B("Snowblind", Inc(Stat.Evasion, 20f), Inc(Stat.Damage, 15f, Tags.Cold).While(Condition.ColdLand)),
            B("Tracker", Inc(Stat.MoveSpeed, 6f), Inc(Stat.Damage, 15f, Tags.Projectile).While(Condition.UnderTrees)),
            B("Sharp Eyes", Flat(Stat.CritChance, 1f), Inc(Stat.Accuracy, 20f)),
            B("Dirty Fighting", Inc(Stat.Damage, 15f, Tags.Melee), Chance(Tags.Bleed, 10f)),
        };

        public static readonly BridgeEntry[] OuterBridges =
        {
            B("Unbroken", Inc(Stat.StunThreshold, 40f), Inc(Stat.Armour, 30f)),
            B("Second Wind", Inc(Stat.Recovery, 15f), Flat(Stat.HealthRegenPercent, 1f).While(Condition.LowHealth)),
            B("Sacrifice", Inc(Stat.Damage, 30f, Tags.Minion).While(Condition.LowHealth), Flat(Stat.HealthOnKill, 10f)),
            B("Plague Rats", Inc(Stat.Damage, 20f, Tags.Minion), Inc(Stat.Damage, 20f, Tags.DamageOverTime)),
            B("Wildfire", Inc(Stat.Damage, 25f, Tags.DamageOverTime), Inc(Stat.AreaOfEffect, 10f)),
            B("Thermal Word", Flat(Stat.Penetration, 6f, Tags.Fire), Flat(Stat.Penetration, 6f, Tags.Magic), Inc(Stat.Damage, 15f, Tags.Spell)),
            B("Overcharge", Inc(Stat.Damage, 25f, Tags.Spell).While(Condition.FullMana), Inc(Stat.CooldownRecovery, 8f)),
            B("Superconductor", Inc(Stat.ReactionDamage, 40f), Inc(Stat.AilmentEffect, 20f, Tags.Shock), Inc(Stat.AilmentEffect, 20f, Tags.Chill)),
            B("Long Night", Inc(Stat.Barrier, 20f), Inc(Stat.Damage, 25f).While(Condition.InDarkness)),
            B("Lie of the Land", Inc(Stat.Damage, 20f, Tags.Projectile), Inc(Stat.Evasion, 20f).While(Condition.InWater)),
            B("Ambush", Inc(Stat.Damage, 30f).Against(Condition.FullHealth), Flat(Stat.CritChance, 1.5f)),
            B("Butcher's Hand", Inc(Stat.Damage, 25f).Against(Condition.LowHealth), Flat(Stat.CritMultiplier, 15f)),
        };

        /// <summary>Which waygates open onto which: far apart, and not always onto what's expected.</summary>
        public static readonly (int first, int second)[] GatePairs = { (0, 5), (1, 8), (2, 7), (3, 10), (4, 9), (6, 11) };
    }
}

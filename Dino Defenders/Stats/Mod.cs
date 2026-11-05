using Dino_Defenders.Skills;
using System.Globalization;
using System.Text;

namespace Dino_Defenders.Stats
{
    /// <summary>
    /// One thing that changes one stat. Talents, items, buffs and enemy traits are all lists of these, and nothing else is needed for a new
    /// source of power. They're read when a sheet is compiled, never when a blow lands.
    /// </summary>
    public readonly record struct Mod
    {
        public Stat Stat { get; init; }
        public ModKind Kind { get; init; }
        public float Value { get; init; }

        /// <summary>What this applies to: everything named here has to be on the action.</summary>
        public Tags Scope { get; init; }

        /// <summary>What must hold of whoever has this, and what mustn't.</summary>
        public Condition Need { get; init; }
        public Condition Without { get; init; }

        /// <summary>What must hold of whoever is being hit.</summary>
        public Condition Foe { get; init; }

        /// <summary>One skill alone, if it isn't <see cref="SkillDefinition.None"/>.</summary>
        public ushort Skill { get; init; }

        /// <summary>For conversions: from one type to another.</summary>
        public DamageType From { get; init; }
        public DamageType To { get; init; }

        /// <summary>Multiplies the value by how many steps of something there are.</summary>
        public Per Per { get; init; }
        public float PerStep { get; init; }
        public float PerCap { get; init; }

        public Rule Rule { get; init; }

        /// <summary>For <see cref="ModKind.Divert"/> and <see cref="ModKind.Echo"/>: the stat it goes to.</summary>
        public Stat Into { get; init; }

        public Condition Grants { get; init; }
        public TriggerEvent On { get; init; }

        public Mod While(Condition condition) => this with { Need = Need | condition };
        public Mod Unless(Condition condition) => this with { Without = Without | condition };
        public Mod Against(Condition condition) => this with { Foe = Foe | condition };
        public Mod ForSkill(SkillDefinition skill) => this with { Skill = skill.Number };
        public Mod For(Per per, float step, float cap = 0f) => this with { Per = per, PerStep = step, PerCap = cap };

        /// <summary>What a player reads. Anything worth having has to be able to say what it does.</summary>
        public string Describe() => ModText.Describe(this);
    }

    /// <summary>Short forms for writing content, which is mostly tables of these.</summary>
    public static class M
    {
        public static Mod Flat(Stat stat, float value, Tags scope = Tags.None) => new Mod { Stat = stat, Kind = ModKind.Flat, Value = value, Scope = scope };
        public static Mod Inc(Stat stat, float value, Tags scope = Tags.None) => new Mod { Stat = stat, Kind = ModKind.Increased, Value = value, Scope = scope };
        public static Mod More(Stat stat, float value, Tags scope = Tags.None) => new Mod { Stat = stat, Kind = ModKind.More, Value = value, Scope = scope };
        public static Mod Set(Stat stat, float value, Tags scope = Tags.None) => new Mod { Stat = stat, Kind = ModKind.Override, Value = value, Scope = scope };

        public static Mod Added(DamageType type, float value, Tags scope = Tags.None) => Flat(AddedStat(type), value, scope);
        public static Mod Resist(Tags type, float value) => Flat(Stat.Resistance, value, type);
        public static Mod Pen(Tags type, float value) => Flat(Stat.Penetration, value, type);
        public static Mod Chance(Tags ailment, float value, Tags scope = Tags.None) => Flat(Stat.AilmentChance, value, ailment | scope);

        public static Mod Convert(DamageType from, DamageType to, float percent, Tags scope = Tags.None)
        {
            return new Mod { Stat = Stat.Convert, Kind = ModKind.Flat, Value = percent, From = from, To = to, Scope = scope };
        }

        public static Mod GainAs(DamageType from, DamageType to, float percent, Tags scope = Tags.None)
        {
            return new Mod { Stat = Stat.GainAs, Kind = ModKind.Flat, Value = percent, From = from, To = to, Scope = scope };
        }

        public static Mod TakenAs(DamageType from, DamageType to, float percent)
        {
            return new Mod { Stat = Stat.TakenAs, Kind = ModKind.Flat, Value = percent, From = from, To = to };
        }

        public static Mod Rules(Rule rule) => new Mod { Kind = ModKind.Rule, Rule = rule };
        public static Mod Grant(Condition condition) => new Mod { Kind = ModKind.Grant, Grants = condition };
        public static Mod Skill(SkillDefinition skill) => new Mod { Kind = ModKind.GrantSkill, Skill = skill.Number };

        /// <summary>When something happens, a skill goes off: <paramref name="chance"/> percent of the time.</summary>
        public static Mod Trigger(TriggerEvent on, SkillDefinition skill, float chance = 100f)
        {
            return new Mod { Kind = ModKind.Trigger, On = on, Skill = skill.Number, Value = chance };
        }

        /// <summary>Increases and mores of one stat apply to another instead, at <paramref name="percent"/> of their value, for what's in <paramref name="scope"/>.</summary>
        public static Mod Divert(Stat from, Stat into, float percent = 100f, Tags scope = Tags.None)
        {
            return new Mod { Stat = from, Kind = ModKind.Divert, Into = into, Value = percent, Scope = scope };
        }

        /// <summary>Increases and mores of one stat also apply to another.</summary>
        public static Mod Echo(Stat from, Stat into, float percent = 100f, Tags scope = Tags.None)
        {
            return new Mod { Stat = from, Kind = ModKind.Echo, Into = into, Value = percent, Scope = scope };
        }

        public static Stat AddedStat(DamageType type) => type switch
        {
            DamageType.Physical => Stat.AddedPhysical,
            DamageType.Fire => Stat.AddedFire,
            DamageType.Cold => Stat.AddedCold,
            DamageType.Lightning => Stat.AddedLightning,
            DamageType.Magic => Stat.AddedMagic,
            _ => Stat.AddedPoison,
        };
    }

    /// <summary>How stats are shown: what one is called, and whether a flat amount of it is a percentage.</summary>
    public readonly record struct StatName(string Name, bool Percent = false);

    public static class StatNames
    {
        private static readonly StatName[] Table = Build();

        public static StatName Of(Stat stat) => Table[(int)stat];

        private static StatName[] Build()
        {
            var table = new StatName[(int)Stat.Count];
            void Name(Stat stat, string name, bool percent = false) => table[(int)stat] = new StatName(name, percent);

            Name(Stat.Health, "Health");
            Name(Stat.Mana, "Mana");
            Name(Stat.Barrier, "Barrier");
            Name(Stat.HealthRegen, "Health Regeneration a second");
            Name(Stat.HealthRegenPercent, "of Health Regenerated a second", true);
            Name(Stat.ManaRegen, "Mana Regeneration a second");
            Name(Stat.ManaRegenPercent, "of Mana Regenerated a second", true);
            Name(Stat.BarrierRecharge, "Barrier Recharge", true);
            Name(Stat.BarrierDelay, "seconds before Barrier Recharges");
            Name(Stat.Recovery, "Recovery");
            Name(Stat.HealthLeech, "of Damage Leeched as Health", true);
            Name(Stat.ManaLeech, "of Damage Leeched as Mana", true);
            Name(Stat.HealthOnHit, "Health on Hit");
            Name(Stat.ManaOnHit, "Mana on Hit");
            Name(Stat.HealthOnKill, "Health on Kill");
            Name(Stat.ManaOnKill, "Mana on Kill");
            Name(Stat.BarrierOnKill, "Barrier on Kill");
            Name(Stat.FlaskEffect, "Flask Effect");
            Name(Stat.MoveSpeed, "Movement Speed");
            Name(Stat.DodgeCooldown, "Dodge Cooldown");
            Name(Stat.DodgeSpeed, "Dodge Speed");
            Name(Stat.Damage, "Damage");
            Name(Stat.AddedPhysical, "Physical Damage");
            Name(Stat.AddedFire, "Fire Damage");
            Name(Stat.AddedCold, "Cold Damage");
            Name(Stat.AddedLightning, "Lightning Damage");
            Name(Stat.AddedMagic, "Magic Damage");
            Name(Stat.AddedPoison, "Poison Damage");
            Name(Stat.Convert, "Damage Converted");
            Name(Stat.GainAs, "Damage Gained");
            Name(Stat.AttackSpeed, "Attack Speed");
            Name(Stat.CastSpeed, "Cast Speed");
            Name(Stat.CritChance, "Critical Chance", true);
            Name(Stat.CritMultiplier, "Critical Multiplier", true);
            Name(Stat.Accuracy, "Accuracy");
            Name(Stat.Penetration, "Resistance Penetration", true);
            Name(Stat.ArmourIgnore, "of Enemy Armour Ignored", true);
            Name(Stat.AreaOfEffect, "Area of Effect");
            Name(Stat.MeleeReach, "metres of Melee Reach");
            Name(Stat.ProjectileSpeed, "Projectile Speed");
            Name(Stat.ProjectileCount, "Projectiles");
            Name(Stat.Pierce, "Enemies Pierced");
            Name(Stat.Chain, "Chains");
            Name(Stat.SkillDuration, "Skill Duration");
            Name(Stat.CooldownRecovery, "Cooldown Recovery");
            Name(Stat.SkillCost, "Skill Cost");
            Name(Stat.SkillLevel, "Levels of Skills");
            Name(Stat.AilmentChance, "Chance", true);
            Name(Stat.AilmentDuration, "Duration");
            Name(Stat.AilmentEffect, "Effect");
            Name(Stat.Execute, "Critical Hits Kill Enemies below this Health", true);
            Name(Stat.ReactionDamage, "Reaction Damage");
            Name(Stat.ReactionArea, "Reaction Area");
            Name(Stat.Armour, "Armour");
            Name(Stat.ArmourApplies, "of Armour Applies", true);
            Name(Stat.Evasion, "Evasion");
            Name(Stat.BlockChance, "Chance to Block", true);
            Name(Stat.MaxBlock, "Maximum Chance to Block", true);
            Name(Stat.Resistance, "Resistance", true);
            Name(Stat.MaxResistance, "Maximum Resistance", true);
            Name(Stat.PhysicalReduction, "Additional Physical Damage Reduction", true);
            Name(Stat.FlatReduction, "Damage Taken from Hits");
            Name(Stat.DamageTaken, "Damage Taken");
            Name(Stat.TakenAs, "Damage Taken");
            Name(Stat.TakenFromMana, "of Damage Taken from Mana before Health", true);
            Name(Stat.AilmentAvoid, "Chance to Avoid", true);
            Name(Stat.AilmentTaken, "Duration on You");
            Name(Stat.StunThreshold, "Stun Threshold");
            Name(Stat.Reflect, "of Melee Damage Taken Reflected", true);
            Name(Stat.MinionCount, "Minions");
            Name(Stat.MinionDuration, "Minion Duration");
            Name(Stat.RaiseFallen, "Chance for Enemies you Kill to Rise beside you", true);
            Name(Stat.ItemRarity, "Item Rarity");
            Name(Stat.GoldFind, "Gold Found");
            Name(Stat.ExperienceGain, "Experience Gained");
            Name(Stat.Stealth, "Stealth");
            Name(Stat.ExplodeOnKill, "of a Victim's Health Dealt as Fire around it on Kill", true);
            return table;
        }
    }

    /// <summary>Turns a modifier into a line of text, so that nobody has to write one out by hand and have it drift from what the numbers do.</summary>
    public static class ModText
    {
        private static readonly (Tags tag, string word)[] Adjectives =
        {
            (Tags.Melee, "Melee"), (Tags.Projectile, "Projectile"), (Tags.Area, "Area"), (Tags.Zone, "Ground"), (Tags.Chaining, "Chaining"), (Tags.Movement, "Movement"), (Tags.Buff, "Buff"),
            (Tags.Triggered, "Triggered"), (Tags.Reaction, "Reaction"),
            (Tags.Elemental, "Elemental"), (Tags.Physical, "Physical"), (Tags.Fire, "Fire"), (Tags.Cold, "Cold"), (Tags.Lightning, "Lightning"), (Tags.Magic, "Magic"), (Tags.Poison, "Poison"),
            (Tags.Attack, "Attack"), (Tags.Spell, "Spell"), (Tags.Hit, "Hit"),
            (Tags.Burn, "Burn"), (Tags.Bleed, "Bleed"), (Tags.Venom, "Venom"), (Tags.Chill, "Chill"), (Tags.Freeze, "Freeze"), (Tags.Shock, "Shock"), (Tags.Hex, "Hex"), (Tags.Stun, "Stun"),
            (Tags.DamageOverTime, "Lingering"),
        };

        private static readonly (Tags tag, string word)[] Weapons =
        {
            (Tags.Sword, "Swords"), (Tags.Axe, "Axes"), (Tags.Mace, "Maces"), (Tags.Dagger, "Daggers"), (Tags.Spear, "Spears"), (Tags.Bow, "Bows"), (Tags.Staff, "Staves"), (Tags.Wand, "Wands"),
            (Tags.Unarmed, "Bare Hands"), (Tags.OneHanded, "One Handed Weapons"), (Tags.TwoHanded, "Two Handed Weapons"),
        };

        private static readonly (Condition condition, string own, string foe)[] Conditions =
        {
            (Condition.FullHealth, "while at Full Health", "at Full Health"),
            (Condition.LowHealth, "while on Low Health", "on Low Health"),
            (Condition.FullMana, "while at Full Mana", "at Full Mana"),
            (Condition.LowMana, "while on Low Mana", "on Low Mana"),
            (Condition.HasBarrier, "while you have Barrier", "with Barrier"),
            (Condition.NoBarrier, "while you have no Barrier", "without Barrier"),
            (Condition.Moving, "while Moving", "that are Moving"),
            (Condition.Stationary, "while Standing Still", "that are Standing Still"),
            (Condition.RecentlyHit, "if you've been Hit Recently", "that were Hit Recently"),
            (Condition.RecentlyKilled, "if you've Killed Recently", "that have Killed Recently"),
            (Condition.RecentlyCrit, "if you've Crit Recently", "that have Crit Recently"),
            (Condition.RecentlyBlocked, "if you've Blocked Recently", "that have Blocked Recently"),
            (Condition.RecentlyDodged, "if you've Dodged Recently", "that have Dodged Recently"),
            (Condition.RecentlyUsedSkill, "if you've used a Skill Recently", "that have used a Skill Recently"),
            (Condition.Burning, "while Burning", "that are Burning"),
            (Condition.Bleeding, "while Bleeding", "that are Bleeding"),
            (Condition.Envenomed, "while Envenomed", "that are Envenomed"),
            (Condition.Chilled, "while Chilled", "that are Chilled"),
            (Condition.Frozen, "while Frozen", "that are Frozen"),
            (Condition.Shocked, "while Shocked", "that are Shocked"),
            (Condition.Hexed, "while Hexed", "that are Hexed"),
            (Condition.Stunned, "while Stunned", "that are Stunned"),
            (Condition.InWater, "while standing in Water", "standing in Water"),
            (Condition.UnderTrees, "while under the Trees", "under the Trees"),
            (Condition.OnRoad, "while on a Road", "on a Road"),
            (Condition.InDarkness, "while in Darkness", "in Darkness"),
            (Condition.HotLand, "in Hot Lands", "in Hot Lands"),
            (Condition.ColdLand, "in Cold Lands", "in Cold Lands"),
            (Condition.WetLand, "in Wet Lands", "in Wet Lands"),
            (Condition.HighLand, "in the High Country", "in the High Country"),
            (Condition.Alone, "while Alone", "that are Alone"),
            (Condition.WithAllies, "while near an Ally", "near an Ally"),
            (Condition.Elite, "while Elite", "that are Elite"),
            (Condition.Boss, "while a Boss", "that are Bosses"),
            (Condition.HoldingShield, "while holding a Shield", "holding a Shield"),
            (Condition.TwoHanded, "while wielding a Two Handed Weapon", "wielding a Two Handed Weapon"),
            (Condition.Unarmed, "while Unarmed", "that are Unarmed"),
        };

        private static readonly (Rule rule, string text)[] RuleTexts =
        {
            (Rule.CostsHealth, "Skills cost Health instead of Mana"),
            (Rule.DeferredDamage, "Damage you take is paid over time instead of at once, and each kill forgives some of what you owe"),
            (Rule.OverhealBarrier, "Healing past full Health becomes Barrier"),
            (Rule.BlockReflects, "Blocked Hits are turned back on the attacker"),
            (Rule.Guardian, "Allies near you take 30% less Damage, and you take it instead"),
            (Rule.Puppeteer, "Your Minions have all your Damage, Speed and Critical Modifiers"),
            (Rule.EndlessVenom, "Venom has no limit on Stacks"),
            (Rule.Carrier, "You are immune to Venom, and it spreads from you to Enemies nearby"),
            (Rule.AilmentsLeap, "When an Enemy dies, its Ailments leap to Enemies nearby"),
            (Rule.FireFeedsBurn, "Your Fire Hits deal no Damage: all of it goes into a Burn"),
            (Rule.BurningTrail, "The Ground burns behind you"),
            (Rule.Rebirth, "When you would die, you rise at half Health instead, once every 90 seconds"),
            (Rule.SpellEcho, "Spells go off a second time, a moment later, at half strength"),
            (Rule.NoCooldowns, "Skills have no Cooldown, and cost 25% more for each second of it they'd have had"),
            (Rule.StaticCharge, "Moving builds a Charge. Your next Hit spends it, for 1% more Damage per metre"),
            (Rule.ChainsGrow, "Chains deal 25% more Damage with each leap instead of less"),
            (Rule.FreezeStores, "Damage to Frozen Enemies is stored, and lands at once, 60% stronger, when the ice breaks"),
            (Rule.HighGround, "4% more Damage for each metre you stand above your target, up to 60%, and 2% less for each metre below"),
            (Rule.LongShot, "Projectiles deal 1.5% more Damage for each metre flown, up to 90%, and 40% less within 10 metres"),
            (Rule.ProjectilesReturn, "Projectiles fly back to you when they're spent"),
            (Rule.Mimic, "The last Skill to hit you is yours to use, on your stats, until another takes its place"),
            (Rule.DodgeCrit, "Your next Hit after a Dodge is a Critical Hit"),
            (Rule.WaterTrail, "Your Spells leave Water where they land"),
            (Rule.NoLeech, "You can't Leech"),
            (Rule.NoRegen, "You can't Regenerate Health"),
            (Rule.NeverCrit, "You can't deal Critical Hits"),
            (Rule.CannotEvade, "You can't Evade"),
            (Rule.CannotBeStunned, "You can't be Stunned"),
            (Rule.CannotBeFrozen, "You can't be Frozen"),
            (Rule.CannotBeChilled, "You can't be Chilled"),
            (Rule.ImmuneToBurn, "You can't be Burned"),
            (Rule.AlwaysShocked, "You are always Shocked"),
            (Rule.ChillNeverFades, "Your Chill doesn't fade while the Enemy is within 25 metres of you"),
            (Rule.FireHeals, "A quarter of the Fire Damage you take heals you instead"),
            (Rule.NoOwnDamage, "Your own Hits deal no Damage"),
            (Rule.LuckyHits, "Your Damage is rolled twice, keeping the better"),
        };

        private static string Number(float value)
        {
            float magnitude = MathF.Abs(value);
            return magnitude >= 10f || magnitude == MathF.Floor(magnitude)
                ? magnitude.ToString("0", CultureInfo.InvariantCulture)
                : magnitude.ToString("0.#", CultureInfo.InvariantCulture);
        }

        private static string Adjective(Tags scope)
        {
            var words = new StringBuilder();
            foreach ((Tags tag, string word) in Adjectives)
            {
                if ((scope & tag) == 0) continue;
                // Fire already says it's elemental.
                if (tag == Tags.Elemental && (scope & (Tags.Fire | Tags.Cold | Tags.Lightning)) != 0) continue;
                words.Append(word).Append(' ');
            }
            return words.ToString();
        }

        private static string WeaponWords(Tags scope)
        {
            foreach ((Tags tag, string word) in Weapons)
            {
                if ((scope & tag) != 0) return $" with {word}";
            }
            return "";
        }

        private static string Own(Condition condition, string joiner = " ")
        {
            var words = new StringBuilder();
            foreach ((Condition one, string own, _) in Conditions)
            {
                if ((condition & one) != 0) words.Append(joiner).Append(own);
            }
            return words.ToString();
        }

        private static string Foes(Condition condition)
        {
            if (condition == Condition.None) return "";
            var words = new List<string>();
            foreach ((Condition one, _, string foe) in Conditions)
            {
                if ((condition & one) != 0) words.Add(foe);
            }
            return $" against Enemies {string.Join(" and ", words)}";
        }

        private static string Tail(in Mod mod)
        {
            var tail = new StringBuilder();
            tail.Append(WeaponWords(mod.Scope));
            if (mod.Skill != SkillDefinition.None && mod.Kind is ModKind.Flat or ModKind.Increased or ModKind.More) tail.Append(" with ").Append(SkillCatalog.Get(mod.Skill).Name);
            tail.Append(Foes(mod.Foe));
            tail.Append(Own(mod.Need));
            if (mod.Without != Condition.None)
            {
                if ((mod.Without & Condition.InWater) != 0) tail.Append(" while on Dry Land");
                else tail.Append(" unless").Append(Own(mod.Without).Replace(" while ", " ").Replace(" if ", " "));
            }

            if (mod.Per != Per.None) tail.Append(PerWords(mod));
            return tail.ToString();
        }

        private static string PerWords(in Mod mod)
        {
            string step = mod.PerStep > 1f ? $" {Number(mod.PerStep)}" : "";
            if (mod.Per is Per.MoveSpeedAboveBase or Per.HealthSpent && step.Length == 0) step = " 1";
            string name = mod.Per switch
            {
                Per.Armour => "Armour",
                Per.Evasion => "Evasion",
                Per.MaxHealth => "Maximum Health",
                Per.MaxMana => "Maximum Mana",
                Per.MaxBarrier => "Maximum Barrier",
                Per.MoveSpeedAboveBase => "% of Movement Speed above normal",
                Per.Level => "Level",
                Per.Gold => "Gold carried",
                Per.ShrinesKindled => mod.PerStep > 1f ? "Shrines kindled in this land" : "Shrine kindled in this land",
                Per.RecentKills => mod.PerStep > 1f ? "Enemies killed Recently" : "Enemy killed Recently",
                Per.HealthSpent => "% of Health spent Recently",
                Per.Minions => mod.PerStep > 1f ? "Minions" : "Minion",
                Per.AlliesNear => mod.PerStep > 1f ? "Allies near you" : "Ally near you",
                _ => "",
            };
            bool percent = mod.Kind != ModKind.Flat || StatNames.Of(mod.Stat).Percent;
            string cap = mod.PerCap > 0f ? $", up to {Number(mod.PerCap)}{(percent ? "%" : "")}" : "";
            return $" per{step} {name}{cap}".Replace(" %", "%");
        }

        public static string Describe(in Mod mod)
        {
            return mod.Kind switch
            {
                ModKind.Flat => DescribeFlat(mod),
                ModKind.Increased => DescribeScaled(mod, "increased", "reduced"),
                ModKind.More => DescribeScaled(mod, "more", "less"),
                ModKind.Override when mod.Stat == Stat.MinionCount => $"You can have only {Number(mod.Value)} Minion of each kind{Tail(mod)}",
                ModKind.Override => $"{Subject(mod)}is {Number(mod.Value)}{(StatNames.Of(mod.Stat).Percent ? "%" : "")}{Tail(mod)}",
                ModKind.Rule => DescribeRule(mod),
                ModKind.Grant => DescribeGrant(mod),
                ModKind.GrantSkill => $"Grants the Skill {SkillCatalog.Get(mod.Skill).Name}",
                ModKind.Divert => $"Increases and Reductions to {StatNames.Of(mod.Stat).Name} apply to {Adjective(mod.Scope)}{StatNames.Of(mod.Into).Name} instead{Rate(mod)}",
                ModKind.Echo => $"Increases and Reductions to {StatNames.Of(mod.Stat).Name} also apply to {Adjective(mod.Scope)}{StatNames.Of(mod.Into).Name}{Rate(mod)}",
                ModKind.Trigger => DescribeTrigger(mod),
                _ => "",
            };
        }

        private static string Rate(in Mod mod) => mod.Value == 100f ? "" : $", at {Number(mod.Value)}% of their value";

        private static string Subject(in Mod mod) => $"{Adjective(mod.Scope)}{StatNames.Of(mod.Stat).Name} ";

        private static string DescribeRule(in Mod mod)
        {
            var lines = new List<string>();
            foreach ((Rule rule, string text) in RuleTexts)
            {
                if ((mod.Rule & rule) != 0) lines.Add(text);
            }
            return string.Join(". ", lines) + Own(mod.Need);
        }

        private static string DescribeGrant(in Mod mod)
        {
            var words = new List<string>();
            foreach ((Condition one, _, string foe) in Conditions)
            {
                if ((mod.Grants & one) != 0) words.Add(foe);
            }
            return $"You always count as {string.Join(" and ", words)}";
        }

        private static string DescribeTrigger(in Mod mod)
        {
            string when = mod.On switch
            {
                TriggerEvent.Hit => "When you Hit",
                TriggerEvent.Critical => "When you Crit",
                TriggerEvent.Kill => "When you Kill",
                TriggerEvent.Struck => "When you're Hit",
                TriggerEvent.Block => "When you Block",
                TriggerEvent.Dodge => "When you Dodge",
                _ => "When you use a Flask",
            };
            string chance = mod.Value >= 100f ? ", cast" : $", {Number(mod.Value)}% chance to cast";
            return $"{when}{chance} {SkillCatalog.Get(mod.Skill).Name}";
        }

        private static string DescribeFlat(in Mod mod)
        {
            if ((mod.Scope & Tags.Minion) != 0) return $"Minions have {DescribeFlat(mod with { Scope = mod.Scope & ~Tags.Minion })}";

            StatName name = StatNames.Of(mod.Stat);
            if (MathF.Abs(mod.Value) == 1f) name = name with { Name = Singular(name.Name) };
            string sign = mod.Value >= 0f ? "+" : "-";
            string number = Number(mod.Value) + (name.Percent ? "%" : "");
            Tags scope = mod.Scope;
            bool toAttacks = (scope & Tags.Attack) != 0;
            bool toSpells = (scope & Tags.Spell) != 0;

            switch (mod.Stat)
            {
                case Stat.AddedPhysical or Stat.AddedFire or Stat.AddedCold or Stat.AddedLightning or Stat.AddedMagic or Stat.AddedPoison:
                    string to = toAttacks ? " to Attacks" : toSpells ? " to Spells" : "";
                    return $"{sign}{number} {Adjective(scope & ~(Tags.Attack | Tags.Spell))}{name.Name}{to}{Tail(mod)}";

                case Stat.Convert:
                    return $"{Number(mod.Value)}% of {Adjective(scope)}{mod.From} Damage is Converted to {mod.To}{Tail(mod)}";

                case Stat.GainAs:
                    return $"Gain {Number(mod.Value)}% of {Adjective(scope)}{mod.From} Damage as extra {mod.To}{Tail(mod)}";

                case Stat.TakenAs:
                    return $"{Number(mod.Value)}% of {mod.From} Damage is Taken as {mod.To}{Tail(mod)}";

                case Stat.Resistance:
                    return $"{sign}{number} {ResistanceName(scope)}{Tail(mod)}";

                case Stat.MaxResistance:
                    return $"{sign}{number} Maximum {ResistanceName(scope)}{Tail(mod)}";

                case Stat.Penetration:
                    return $"{Adjective(scope & ~(Tags.Attack | Tags.Spell))}Damage Penetrates {Number(mod.Value)}% Resistance{Tail(mod)}";

                case Stat.ArmourApplies:
                    return $"{Number(mod.Value)}% of Armour also applies to {Adjective(scope)}Damage{Tail(mod)}";

                case Stat.AilmentChance:
                    Tags ailment = scope & Tags.Ailments;
                    string verb = ailment == Tags.Venom ? "Envenom" : Adjective(ailment).Trim();
                    return $"{sign}{number} Chance to {verb}{(toAttacks ? " with Attacks" : toSpells ? " with Spells" : "")}{Tail(mod)}";

                case Stat.AilmentAvoid:
                    return $"{number} Chance to Avoid {(scope == Tags.None ? "Ailments" : Adjective(scope).Trim())}{Tail(mod)}";

                case Stat.FlatReduction:
                    return $"-{Number(mod.Value)} {Adjective(scope)}{name.Name}{Tail(mod)}";

                case Stat.ProjectileCount:
                    return $"{sign}{number} {Adjective(scope)}{name.Name}{Tail(mod)}";

                case Stat.BlockChance:
                    return $"{sign}{number} {(toSpells ? "Chance to Block Spells" : toAttacks ? "Chance to Block Attacks" : name.Name)}{Tail(mod)}";

                case Stat.TakenFromMana or Stat.HealthLeech or Stat.ManaLeech or Stat.Reflect or Stat.ArmourIgnore or Stat.RaiseFallen or Stat.Execute or Stat.ExplodeOnKill or Stat.PhysicalReduction or Stat.HealthRegenPercent or Stat.ManaRegenPercent:
                    return $"{number} {Adjective(scope)}{name.Name}{Tail(mod)}";
            }

            return $"{sign}{number} {Adjective(scope)}{name.Name}{Tail(mod)}";
        }

        private static string Singular(string name) => name switch
        {
            "Minions" => "Minion",
            "Projectiles" => "Projectile",
            "Chains" => "Chain",
            "Enemies Pierced" => "Enemy Pierced",
            "Levels of Skills" => "Level of Skills",
            _ => name,
        };

        private static string ResistanceName(Tags scope)
        {
            if (scope == Tags.None) return "to all Resistances";
            if (scope == Tags.Elemental) return "Elemental Resistance";
            return $"{Adjective(scope)}Resistance";
        }

        private static string DescribeScaled(in Mod mod, string up, string down)
        {
            string word = mod.Value >= 0f ? up : down;
            StatName name = StatNames.Of(mod.Stat);
            Tags scope = mod.Scope;

            if ((scope & Tags.Minion) != 0)
            {
                return $"Minions have {Number(mod.Value)}% {word} {Adjective(scope & ~Tags.Minion)}{name.Name}{Tail(mod)}";
            }

            if (mod.Stat is Stat.AilmentDuration or Stat.AilmentEffect or Stat.AilmentTaken)
            {
                string ailment = (scope & Tags.Ailments) != 0 ? Adjective(scope & Tags.Ailments).Trim() : "Ailment";
                return mod.Stat == Stat.AilmentEffect
                    ? $"{Number(mod.Value)}% {word} Effect of {ailment}{Tail(mod)}"
                    : $"{Number(mod.Value)}% {word} {ailment} {name.Name}{Tail(mod)}";
            }

            return $"{Number(mod.Value)}% {word} {Adjective(scope)}{name.Name}{Tail(mod)}";
        }
    }
}

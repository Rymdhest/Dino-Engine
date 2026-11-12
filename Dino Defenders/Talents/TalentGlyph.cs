using Dino_Defenders.Stats;

namespace Dino_Defenders.Talents
{
    /// <summary>
    /// Every picture a node on the tree can have. A small node's is worked out from what it gives (<see cref="TalentIcons.Of(in Mod)"/>), so two nodes
    /// that give the same thing always look the same, and a keystone names its own. How each is drawn is the interface's business.
    /// </summary>
    public enum TalentGlyph : byte
    {
        None,

        // Attributes.
        Strength, Dexterity, Intellect,

        // The pools, and getting them back.
        Health, Mana, Barrier, HealthRegen, ManaRegen, BarrierRecharge, Recovery, Leech, OnHit, OnKill, Flask,

        // Not being hurt.
        Armour, Evasion, Block, Resist, MaxResist, Reduction, TakenFromMana, Avoid, StunThreshold, Reflect,

        // Kinds of harm.
        Physical, Fire, Cold, Lightning, Magic, Poison, Elemental, DamageOverTime,

        // What a blow leaves behind.
        Burn, Bleed, Venom, Chill, Freeze, Shock, Hex, Stun,

        // What a blow is.
        Damage, Attack, Spell, Crit, CritMulti, Accuracy, Penetration, ArmourIgnore, AttackSpeed, CastSpeed, UseSpeed, Execute, Explode, Conversion,

        // How it gets there.
        Melee, Projectile, Area, Zone, Chain, Pierce, ProjectileSpeed, ProjectileCount, MeleeReach,

        // What it's done with.
        Sword, Axe, Mace, Dagger, Spear, Bow, Staff, Wand, Unarmed, TwoHanded, DualWield, Shield,

        // Books.
        Potency, Opening, Turned, Closing, Wild, Falling, Cost, Cooldown, Duration,

        // Company.
        Minion, MinionCount, Raise,

        // Getting about.
        Move, Dodge, Stealth,

        // The land.
        Water, Trees, Road, Darkness, HotLand, ColdLand, WetLand, HighLand, Rain,

        // Fortune.
        Gold, Rarity, Experience, Reaction,

        // Small marks in a corner, for what a node's main picture is narrowed by.
        Chance, Longer, Stronger, Up,

        // The root and the waygates.
        Root, Waygate,

        // Keystones: one each, and nothing else uses them.
        Mask, Bell, Quill, Feather, Seal, Prism, Comet, Fork, Tower, Hourglass, Skull, Web, Moon, Eye, Peak, Sun, Wave, Leaf, Spiral, Wing, Dice, Scythe, Star,
        Hand, Crack, Quake, Book, Anvil, Mirror, Roots, Aegis, Fang, Coin, Chalice, Ember, Sparks, Phoenix, Scorch, Spellblade, Puppet, Crown, Banner, Lantern,
        Crystal, Glacier, Pool, Sundial, Scales, Key,
    }

    /// <summary>A node's picture: the main one, and a small one in the corner for what it's narrowed by, if anything.</summary>
    public readonly record struct TalentIcon(TalentGlyph Main, TalentGlyph Badge = TalentGlyph.None);

    /// <summary>
    /// What a node looks like, from what it gives. It goes by the stat first and then by what the stat is narrowed to, so it never depends on a number:
    /// +5 Strength and +8 Strength are the same picture, and anything that gives the same thing looks the same wherever it is on the tree.
    /// </summary>
    public static class TalentIcons
    {
        public static TalentIcon Of(TalentNode node)
        {
            if (node.Kind == TalentKind.Root) return new TalentIcon(TalentGlyph.Root);
            if (node.Kind == TalentKind.Gate) return new TalentIcon(TalentGlyph.Waygate);
            if (node.Kind == TalentKind.Keystone) return new TalentIcon(node.Emblem);
            if (node.Mods.Length == 0) return new TalentIcon(TalentGlyph.None);
            TalentIcon first = Of(node.Mods[0]);
            if (node.Kind != TalentKind.Notable || node.Mods.Length < 2) return first;

            // A notable shows what it's mostly about, and the next thing it gives in the corner, if that's something else.
            for (int i = 1; i < node.Mods.Length; i++)
            {
                TalentGlyph next = Of(node.Mods[i]).Main;
                if (next != first.Main && next != TalentGlyph.None) return new TalentIcon(first.Main, next);
            }
            return first;
        }

        /// <summary>What a run of modifiers looks like, as a notable does: what the first gives, and the next thing it gives in the corner, if that's something else.</summary>
        public static TalentIcon Of(IReadOnlyList<Mod> mods)
        {
            if (mods.Count == 0) return new TalentIcon(TalentGlyph.None);
            TalentIcon first = Of(mods[0]);
            for (int i = 1; i < mods.Count; i++)
            {
                TalentGlyph next = Of(mods[i]).Main;
                if (next != first.Main && next != TalentGlyph.None) return new TalentIcon(first.Main, next);
            }
            return first;
        }

        public static TalentIcon Of(in Mod mod)
        {
            Tags scope = mod.Scope;
            bool minion = (scope & Tags.Minion) != 0;
            TalentGlyph element = ElementOf(scope);

            switch (mod.Kind)
            {
                case ModKind.Rule: return new TalentIcon(RuleGlyph(mod.Rule));
                case ModKind.Grant: return new TalentIcon(ConditionGlyph(mod.Grants));
                case ModKind.Trigger: return new TalentIcon(mod.On switch
                {
                    TriggerEvent.Block => TalentGlyph.Block,
                    TriggerEvent.Kill => TalentGlyph.OnKill,
                    TriggerEvent.Struck => TalentGlyph.Reflect,
                    TriggerEvent.Dodge or TriggerEvent.Dash => TalentGlyph.Dodge,
                    TriggerEvent.Critical => TalentGlyph.Crit,
                    _ => TalentGlyph.OnHit,
                }, TalentGlyph.Up);
                case ModKind.Divert or ModKind.Echo: return new TalentIcon(TalentGlyph.Conversion, StatGlyph(mod.Into, Tags.None));
            }

            switch (mod.Stat)
            {
                case Stat.Resistance: return new TalentIcon(TalentGlyph.Resist, element);
                case Stat.MaxResistance: return new TalentIcon(TalentGlyph.MaxResist, element);
                case Stat.Penetration: return new TalentIcon(TalentGlyph.Penetration, element);
                case Stat.ArmourApplies: return new TalentIcon(TalentGlyph.Armour, element);
                case Stat.SpellEvasion: return new TalentIcon(TalentGlyph.Evasion, TalentGlyph.Spell);
                case Stat.Convert or Stat.GainAs: return new TalentIcon(TalentGlyph.Conversion, DamageGlyph(mod.To));
                case Stat.TakenAs: return new TalentIcon(TalentGlyph.Reduction, DamageGlyph(mod.To));
                case Stat.AilmentChance: return new TalentIcon(AilmentOf(scope), TalentGlyph.Chance);
                case Stat.AilmentDuration: return new TalentIcon(AilmentOf(scope), TalentGlyph.Longer);
                case Stat.AilmentEffect: return new TalentIcon(AilmentOf(scope), TalentGlyph.Stronger);
                case Stat.AilmentAvoid or Stat.AilmentTaken: return new TalentIcon(TalentGlyph.Avoid, (scope & Tags.Ailments) != 0 ? AilmentOf(scope) : TalentGlyph.None);
                case Stat.MinionDuration: return new TalentIcon(TalentGlyph.Minion, TalentGlyph.Longer);
                case Stat.AddedPhysical: return new TalentIcon(TalentGlyph.Physical, TalentGlyph.Up);
                case Stat.AddedFire: return new TalentIcon(TalentGlyph.Fire, TalentGlyph.Up);
                case Stat.AddedCold: return new TalentIcon(TalentGlyph.Cold, TalentGlyph.Up);
                case Stat.AddedLightning: return new TalentIcon(TalentGlyph.Lightning, TalentGlyph.Up);
                case Stat.AddedMagic: return new TalentIcon(TalentGlyph.Magic, TalentGlyph.Up);
                case Stat.AddedPoison: return new TalentIcon(TalentGlyph.Poison, TalentGlyph.Up);
            }

            TalentGlyph main = StatGlyph(mod.Stat, scope);
            if (minion && main != TalentGlyph.Minion) return new TalentIcon(main, TalentGlyph.Minion);
            return new TalentIcon(main);
        }

        // What a stat looks like, narrowed by what it's for where that's the point of it.
        private static TalentGlyph StatGlyph(Stat stat, Tags scope) => stat switch
        {
            Stat.Strength => TalentGlyph.Strength,
            Stat.Dexterity => TalentGlyph.Dexterity,
            Stat.Intellect => TalentGlyph.Intellect,
            Stat.Health => TalentGlyph.Health,
            Stat.Mana => TalentGlyph.Mana,
            Stat.Barrier => TalentGlyph.Barrier,
            Stat.HealthRegen or Stat.HealthRegenPercent => TalentGlyph.HealthRegen,
            Stat.ManaRegen or Stat.ManaRegenPercent => TalentGlyph.ManaRegen,
            Stat.BarrierRecharge or Stat.BarrierDelay => TalentGlyph.BarrierRecharge,
            Stat.Recovery => TalentGlyph.Recovery,
            Stat.HealthLeech or Stat.ManaLeech => TalentGlyph.Leech,
            Stat.HealthOnHit or Stat.ManaOnHit => TalentGlyph.OnHit,
            Stat.HealthOnKill or Stat.ManaOnKill or Stat.BarrierOnKill => TalentGlyph.OnKill,
            Stat.FlaskEffect => TalentGlyph.Flask,
            Stat.HealthDrain => TalentGlyph.Bleed,
            Stat.MoveSpeed => TalentGlyph.Move,
            Stat.DodgeCooldown or Stat.DodgeSpeed => TalentGlyph.Dodge,
            Stat.Damage => DamageScope(scope),
            Stat.AttackSpeed => TalentGlyph.AttackSpeed,
            Stat.CastSpeed => TalentGlyph.CastSpeed,
            Stat.UseSpeed => TalentGlyph.UseSpeed,
            Stat.CritChance => TalentGlyph.Crit,
            Stat.CritMultiplier => TalentGlyph.CritMulti,
            Stat.Accuracy => TalentGlyph.Accuracy,
            Stat.ArmourIgnore => TalentGlyph.ArmourIgnore,
            Stat.AreaOfEffect => TalentGlyph.Area,
            Stat.MeleeReach => TalentGlyph.MeleeReach,
            Stat.ProjectileSpeed => TalentGlyph.ProjectileSpeed,
            Stat.ProjectileCount => TalentGlyph.ProjectileCount,
            Stat.Pierce => TalentGlyph.Pierce,
            Stat.Chain => TalentGlyph.Chain,
            Stat.PageDuration => TalentGlyph.Duration,
            Stat.CooldownRecovery => TalentGlyph.Cooldown,
            Stat.BookCost => TalentGlyph.Cost,
            Stat.Potency => PotencyScope(scope),
            Stat.Execute => TalentGlyph.Execute,
            Stat.ReactionDamage or Stat.ReactionArea => TalentGlyph.Reaction,
            Stat.Armour or Stat.PhysicalReduction => TalentGlyph.Armour,
            Stat.Evasion => TalentGlyph.Evasion,
            Stat.BlockChance or Stat.MaxBlock => TalentGlyph.Block,
            Stat.FlatReduction or Stat.DamageTaken => TalentGlyph.Reduction,
            Stat.TakenFromMana => TalentGlyph.TakenFromMana,
            Stat.StunThreshold => TalentGlyph.StunThreshold,
            Stat.Reflect => TalentGlyph.Reflect,
            Stat.MinionCount => TalentGlyph.MinionCount,
            Stat.RaiseFallen => TalentGlyph.Raise,
            Stat.ItemRarity => TalentGlyph.Rarity,
            Stat.GoldFind => TalentGlyph.Gold,
            Stat.ExperienceGain => TalentGlyph.Experience,
            Stat.Stealth => TalentGlyph.Stealth,
            Stat.ExplodeOnKill => TalentGlyph.Explode,
            _ => TalentGlyph.Damage,
        };

        // Damage looks like what it's narrowed to: whose it is first, then what it leaves, then what it is, then what it's done with, then how.
        private static TalentGlyph DamageScope(Tags scope)
        {
            if ((scope & Tags.Minion) != 0) return TalentGlyph.Minion;
            if ((scope & Tags.Ailments) != 0) return AilmentOf(scope);
            if ((scope & Tags.DamageOverTime) != 0) return TalentGlyph.DamageOverTime;
            if (ElementOf(scope) is var element and not TalentGlyph.None) return element;
            if (WeaponOf(scope) is var weapon and not TalentGlyph.None) return weapon;
            if ((scope & Tags.Melee) != 0) return TalentGlyph.Melee;
            if ((scope & Tags.Projectile) != 0) return TalentGlyph.Projectile;
            if ((scope & Tags.Zone) != 0) return TalentGlyph.Zone;
            if ((scope & Tags.Area) != 0) return TalentGlyph.Area;
            if ((scope & Tags.Chaining) != 0) return TalentGlyph.Chain;
            if ((scope & Tags.Falling) != 0) return TalentGlyph.Falling;
            if ((scope & Tags.Reaction) != 0) return TalentGlyph.Reaction;
            if ((scope & Tags.Attack) != 0) return TalentGlyph.Attack;
            if ((scope & Tags.Spell) != 0) return TalentGlyph.Spell;
            return TalentGlyph.Damage;
        }

        private static TalentGlyph PotencyScope(Tags scope)
        {
            if ((scope & Tags.Opening) != 0) return TalentGlyph.Opening;
            if ((scope & Tags.Turned) != 0) return TalentGlyph.Turned;
            if ((scope & Tags.Closing) != 0) return TalentGlyph.Closing;
            if ((scope & Tags.Wild) != 0) return TalentGlyph.Wild;
            if ((scope & Tags.Falling) != 0) return TalentGlyph.Falling;
            if ((scope & Tags.Zone) != 0) return TalentGlyph.Zone;
            if ((scope & Tags.Movement) != 0) return TalentGlyph.Move;
            return TalentGlyph.Potency;
        }

        public static TalentGlyph ElementOf(Tags scope)
        {
            if ((scope & Tags.Fire) != 0) return TalentGlyph.Fire;
            if ((scope & Tags.Cold) != 0) return TalentGlyph.Cold;
            if ((scope & Tags.Lightning) != 0) return TalentGlyph.Lightning;
            if ((scope & Tags.Magic) != 0) return TalentGlyph.Magic;
            if ((scope & Tags.Poison) != 0) return TalentGlyph.Poison;
            if ((scope & Tags.Physical) != 0) return TalentGlyph.Physical;
            if ((scope & Tags.Elemental) != 0) return TalentGlyph.Elemental;
            return TalentGlyph.None;
        }

        private static TalentGlyph WeaponOf(Tags scope)
        {
            if ((scope & Tags.Sword) != 0) return TalentGlyph.Sword;
            if ((scope & Tags.Axe) != 0) return TalentGlyph.Axe;
            if ((scope & Tags.Mace) != 0) return TalentGlyph.Mace;
            if ((scope & Tags.Dagger) != 0) return TalentGlyph.Dagger;
            if ((scope & Tags.Spear) != 0) return TalentGlyph.Spear;
            if ((scope & Tags.Bow) != 0) return TalentGlyph.Bow;
            if ((scope & Tags.Staff) != 0) return TalentGlyph.Staff;
            if ((scope & Tags.Wand) != 0) return TalentGlyph.Wand;
            if ((scope & Tags.Unarmed) != 0) return TalentGlyph.Unarmed;
            if ((scope & Tags.TwoHanded) != 0) return TalentGlyph.TwoHanded;
            return TalentGlyph.None;
        }

        private static TalentGlyph AilmentOf(Tags scope)
        {
            if ((scope & Tags.Burn) != 0) return TalentGlyph.Burn;
            if ((scope & Tags.Bleed) != 0) return TalentGlyph.Bleed;
            if ((scope & Tags.Venom) != 0) return TalentGlyph.Venom;
            if ((scope & Tags.Freeze) != 0) return TalentGlyph.Freeze;
            if ((scope & Tags.Chill) != 0) return TalentGlyph.Chill;
            if ((scope & Tags.Shock) != 0) return TalentGlyph.Shock;
            if ((scope & Tags.Hex) != 0) return TalentGlyph.Hex;
            if ((scope & Tags.Stun) != 0) return TalentGlyph.Stun;
            return TalentGlyph.Avoid;
        }

        /// <summary>What an ailment someone is suffering looks like: the same picture as anything on the tree that's about it.</summary>
        public static TalentGlyph AilmentGlyph(Condition ailment) => ailment switch
        {
            Condition.Burning => TalentGlyph.Burn,
            Condition.Bleeding => TalentGlyph.Bleed,
            Condition.Envenomed => TalentGlyph.Venom,
            Condition.Chilled => TalentGlyph.Chill,
            Condition.Frozen => TalentGlyph.Freeze,
            Condition.Shocked => TalentGlyph.Shock,
            Condition.Hexed => TalentGlyph.Hex,
            Condition.Stunned => TalentGlyph.Stun,
            _ => TalentGlyph.None,
        };

        public static TalentGlyph DamageGlyph(DamageType type) => type switch
        {
            DamageType.Fire => TalentGlyph.Fire,
            DamageType.Cold => TalentGlyph.Cold,
            DamageType.Lightning => TalentGlyph.Lightning,
            DamageType.Magic => TalentGlyph.Magic,
            DamageType.Poison => TalentGlyph.Poison,
            _ => TalentGlyph.Physical,
        };

        private static TalentGlyph ConditionGlyph(Condition condition)
        {
            if ((condition & Condition.InWater) != 0) return TalentGlyph.Water;
            if ((condition & Condition.UnderTrees) != 0) return TalentGlyph.Trees;
            if ((condition & Condition.OnRoad) != 0) return TalentGlyph.Road;
            if ((condition & Condition.InDarkness) != 0) return TalentGlyph.Darkness;
            if ((condition & Condition.HotLand) != 0) return TalentGlyph.HotLand;
            if ((condition & Condition.ColdLand) != 0) return TalentGlyph.ColdLand;
            if ((condition & Condition.WetLand) != 0) return TalentGlyph.WetLand;
            if ((condition & Condition.HighLand) != 0) return TalentGlyph.HighLand;
            return TalentGlyph.Potency;
        }

        private static TalentGlyph RuleGlyph(Rule rule) => rule switch
        {
            Rule.CannotBeChilled or Rule.CannotBeFrozen => TalentGlyph.Avoid,
            Rule.DodgeCrit => TalentGlyph.Dodge,
            _ => TalentGlyph.Potency,
        };
    }
}

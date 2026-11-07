using Dino_Defenders.Books;
using Dino_Defenders.Stats;
using System.Text;
using static Dino_Defenders.Stats.Rulebook;

namespace Dino_Defenders.Tooltips
{
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

            Name(Stat.Strength, "Strength");
            Name(Stat.Dexterity, "Dexterity");
            Name(Stat.Intellect, "Intellect");
            Name(Stat.Health, "Health");
            Name(Stat.Mana, "Mana");
            Name(Stat.Barrier, "Barrier");
            Name(Stat.HealthRegen, "Health Regeneration a second");
            Name(Stat.HealthRegenPercent, "of Health Regenerated a second", true);
            Name(Stat.ManaRegen, "Mana Regeneration a second");
            Name(Stat.ManaRegenPercent, "of Mana Regenerated a second", true);
            Name(Stat.BarrierRecharge, "Barrier Recharge", true);
            Name(Stat.BarrierDelay, "s Barrier Delay");
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
            Name(Stat.MeleeReach, "m Melee Reach");
            Name(Stat.ProjectileSpeed, "Projectile Speed");
            Name(Stat.ProjectileCount, "Projectiles");
            Name(Stat.Pierce, "Pierce");
            Name(Stat.Chain, "Chains");
            Name(Stat.PageDuration, "Page Duration");
            Name(Stat.CooldownRecovery, "Cooldown Recovery");
            Name(Stat.BookCost, "Book Cost");
            Name(Stat.Potency, "Potency");
            Name(Stat.AilmentChance, "Chance", true);
            Name(Stat.AilmentDuration, "Duration");
            Name(Stat.AilmentEffect, "Effect");
            Name(Stat.Execute, "Execute", true);
            Name(Stat.ReactionDamage, "Reaction Damage");
            Name(Stat.ReactionArea, "Reaction Area");
            Name(Stat.Armour, "Armour");
            Name(Stat.ArmourApplies, "of Armour Applies", true);
            Name(Stat.Evasion, "Evasion");
            Name(Stat.BlockChance, "Chance to Block", true);
            Name(Stat.MaxBlock, "Maximum Chance to Block", true);
            Name(Stat.Resistance, "Resistance", true);
            Name(Stat.MaxResistance, "Maximum Resistance", true);
            Name(Stat.PhysicalReduction, "Physical Damage Reduction", true);
            Name(Stat.FlatReduction, "Damage from Hits");
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
            Name(Stat.Stealth, "Stealth", true);
            Name(Stat.ExplodeOnKill, "of the Maximum Health of Enemies you Kill Explodes as Fire", true);
            return table;
        }
    }

    /// <summary>A change to how the game works, in words. Numbers in <see cref="Text"/> come from where the rule reads them.</summary>
    /// <param name="Touches">What it's about: it shows on anything with all of these, and among a talent's tags.</param>
    /// <param name="Casts">A book it sets off, shown beside it with that book's own numbers.</param>
    /// <param name="Gives">A buff it hands out, shown the same way.</param>
    public sealed record RuleInfo(Rule Rule, string Text, Tags Touches = Tags.None, BookPlan? Casts = null, BuffDefinition? Gives = null);

    /// <summary>
    /// Turns a modifier into a line of text by the rules in TOOLTIPS.md, so that nobody has to write one out by hand and have it drift from what the numbers do. Each part of a modifier has one
    /// way of being said, always the same: <c>+5</c> is flat, <c>increased</c> and <c>reduced</c> share a sum, <c>more</c> and <c>less</c> are multipliers of their own, and <c>is set to</c> replaces a stat.
    /// </summary>
    public static class ModText
    {
        private static readonly (Tags tag, string word)[] Adjectives =
        {
            (Tags.Melee, "Melee"), (Tags.Projectile, "Projectile"), (Tags.Area, "Area"), (Tags.Zone, "Zone"), (Tags.Chaining, "Chaining"), (Tags.Movement, "Movement"), (Tags.Buff, "Buff"),
            (Tags.Triggered, "Triggered"), (Tags.Reaction, "Reaction"), (Tags.Opening, "Opening Page"), (Tags.Turned, "Turned Page"), (Tags.Closing, "Closing Page"), (Tags.Wild, "Wild"), (Tags.Falling, "Falling"),
            (Tags.Elemental, "Elemental"), (Tags.Physical, "Physical"), (Tags.Fire, "Fire"), (Tags.Cold, "Cold"), (Tags.Lightning, "Lightning"), (Tags.Magic, "Magic"), (Tags.Poison, "Poison"),
            (Tags.Attack, "Attack"), (Tags.Spell, "Spell"), (Tags.Hit, "Hit"),
            (Tags.Burn, "Burn"), (Tags.Bleed, "Bleed"), (Tags.Venom, "Venom"), (Tags.Chill, "Chill"), (Tags.Freeze, "Freeze"), (Tags.Shock, "Shock"), (Tags.Hex, "Hex"), (Tags.Stun, "Stun"),
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
            (Condition.RecentlyCrit, "if you've dealt a Critical Hit Recently", "that have dealt a Critical Hit Recently"),
            (Condition.RecentlyBlocked, "if you've Blocked Recently", "that have Blocked Recently"),
            (Condition.RecentlyDodged, "if you've Dodged a Hit Recently", "that have Dodged a Hit Recently"),
            (Condition.RecentlyUsedBook, "if you've used a Book Recently", "that have used a Book Recently"),
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
            (Condition.Rare, "while Rare", "that are Rare"),
            (Condition.Boss, "while a Boss", "that are Bosses"),
            (Condition.HoldingShield, "while holding a Shield", "holding a Shield"),
            (Condition.TwoHanded, "while wielding a Two Handed Weapon", "wielding a Two Handed Weapon"),
            (Condition.Unarmed, "while Unarmed", "that are Unarmed"),
        };

        private static readonly RuleInfo[] RuleTable =
        {
            new(Rule.CostsHealth, "Books Cost Health instead of Mana"),
            new(Rule.DeferredDamage, $"Damage you take becomes Debt, which drains your Health by {Say.Share(1f / Hits.DebtSeconds)} of itself a second, at least {Say.Share(DebtLeast)} of Maximum Health. Each Kill removes {Say.Share(DebtForgiven)} of it"),
            new(Rule.OverhealBarrier, $"Healing above Maximum Health becomes Barrier, up to {Say.Share(OverhealMost)} of Maximum Health over your Maximum Barrier"),
            new(Rule.BlockReflects, "Hits you Block are sent back as Physical Indirect Damage"),
            new(Rule.Guardian, $"Other Heroes within {Say.Metres(GuardianReach)} take {Say.Share(GuardianShare)} less Damage from Hits. You take that share instead"),
            new(Rule.Puppeteer, "Your Minions also get your Damage, Attack Speed, Cast Speed, Critical Chance and Critical Multiplier modifiers", Tags.Minion),
            new(Rule.EndlessVenom, $"Your Venom can have {AilmentRules.MostEndlessVenom} Stacks instead of {AilmentRules.MostVenom}", Tags.Venom),
            new(Rule.Carrier, $"You can't be Envenomed. Every {Say.Seconds(CarrierInterval)}, Trigger Contagion within {Say.Metres(Shelf.VenomCloud.First.Length)}", Tags.Venom, Casts: Shelf.VenomCloud),
            new(Rule.AilmentsLeap, $"Enemies you Kill pass their Burn, Bleed, Venom, Chill and Shock to Enemies within {Say.Metres(AilmentLeapReach)}"),
            new(Rule.FireFeedsBurn, "Your Fire Hits deal no Damage. All of it becomes a guaranteed Burn", Tags.Fire | Tags.Burn),
            new(Rule.BurningTrail, $"Leave Burning Ground every {Say.Metres(TrailSpacing)} you move", Tags.Fire, Casts: Shelf.CinderTrail),
            new(Rule.Rebirth, $"Once every {Say.Seconds(RebirthCooldown)}, when you would die, return with {Say.Share(RebirthHealth)} of Maximum Health and deal Fire Indirect Damage equal to {Say.Share(RebirthBlast)} of Maximum Health within {Say.Metres(RebirthRadius)}", Tags.Fire),
            new(Rule.SpellEcho, $"Spells repeat after {Say.Seconds(EchoDelay)} at {Say.Share(EchoStrength)} Damage, with no Cost. Triggered Spells don't repeat", Tags.Spell),
            new(Rule.OneCost, "Only the first Page of a Book has a Cost"),
            new(Rule.StaticCharge, $"Moving builds one charge per metre, up to {Say.Whole(MostStatic)}. Your next Hit uses all of them for {Say.Share(StaticPerCharge)} more Damage each", Tags.Hit),
            new(Rule.ChainsGrow, $"Each Chain deals {Say.Share(ChainGrowth - 1f)} more Damage instead of {Say.Share(1f - ChainFade)} less", Tags.Chaining),
            new(Rule.FreezeStores, $"Damage to Frozen Enemies is stored. When the Freeze ends it lands as Cold Indirect Damage with {Say.Share(StoredBonus)} more", Tags.Cold),
            new(Rule.HighGround, $"{Say.Share(HighGroundPerMetre)} more Damage per metre above your target, up to {Say.Share(HighGroundMost)}. {Say.Share(LowGroundPerMetre)} less per metre below, up to {Say.Share(LowGroundMost)}"),
            new(Rule.LongShot, $"Projectiles deal {Say.Share(LongShotPerMetre)} more Damage per metre flown, up to {Say.Share(LongShotMost)}, and {Say.Share(LongShotNearLess)} less within {Say.Metres(LongShotNear)}", Tags.Projectile),
            new(Rule.ProjectilesReturn, "Projectiles fly back to you when spent, and can Hit everything again", Tags.Projectile),
            new(Rule.Steady, "Nothing on your Pages is random: they find the nearest Enemy and go where they're aimed, and are worth what they would be otherwise", Tags.Wild),
            new(Rule.DodgeCrit, "After you Dodge a Hit, your next Hit is a Critical Hit"),
            new(Rule.WaterTrail, "Spells leave Standing Water where aimed. Triggered Spells don't", Tags.Spell, Casts: Shelf.Puddle),
            new(Rule.NoLeech, "You can't Leech"),
            new(Rule.NoRegen, "You can't Regenerate Health"),
            new(Rule.NeverCrit, "You can't deal Critical Hits"),
            new(Rule.CannotEvade, "You can't Evade"),
            new(Rule.CannotBeStunned, "You can't be Stunned"),
            new(Rule.CannotBeFrozen, "You can't be Frozen"),
            new(Rule.CannotBeChilled, "You can't be Chilled"),
            new(Rule.ImmuneToBurn, "You can't be Burned"),
            new(Rule.AlwaysShocked, $"You are always Shocked, taking at least {Say.Share(AlwaysShockedTaken)} more Damage", Tags.Shock),
            new(Rule.ChillNeverFades, $"Your Chill doesn't fade while the Enemy is within {Say.Metres(ChillReach)}", Tags.Chill),
            new(Rule.FireHeals, $"{Say.Share(FireHealShare)} of Fire Damage from Hits heals you instead", Tags.Fire),
            new(Rule.NoOwnDamage, "Your Books deal no Damage"),
            new(Rule.LuckyHits, "Damage is rolled twice, keeping the better roll"),
            new(Rule.BurstsOnDeath, $"Bursts {Say.Seconds(BurstDelay)} after death for Fire Indirect Damage within {Say.Metres(BurstRadius)}, worth {Say.Share(BurstShare)} of a normal Enemy Hit", Tags.Fire),
            new(Rule.RalliesOnDeath, $"On death, gives Rallied to allies within {Say.Metres(RallyReach)}", Gives: BuffCatalog.Rallied),
        };

        public static IReadOnlyList<RuleInfo> Rules => RuleTable;

        /// <summary>What's said about a rule. Null only if someone has added one and given it no words, which BuildLab fails.</summary>
        public static RuleInfo? Of(Rule rule)
        {
            foreach (RuleInfo info in RuleTable)
            {
                if (info.Rule == rule) return info;
            }
            return null;
        }

        private static string Number(float value) => Say.N(MathF.Abs(value));

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
            var words = new List<string>();
            foreach ((Tags tag, string word) in Weapons)
            {
                if ((scope & tag) != 0) words.Add(word);
            }
            return words.Count > 0 ? $" with {string.Join(" and ", words)}" : "";
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
            tail.Append(Foes(mod.Foe));
            tail.Append(Own(mod.Need));
            if (mod.Without != Condition.None) tail.Append(" unless").Append(Own(mod.Without).Replace(" while ", " ").Replace(" if ", " "));
            if (mod.Per != Per.None) tail.Append(PerWords(mod));
            return tail.ToString();
        }

        private static string PerWords(in Mod mod)
        {
            bool one = mod.PerStep <= 1f;
            string step = one ? "" : $" {Number(mod.PerStep)}";
            if (mod.Per is Per.MoveSpeedAboveBase or Per.HealthSpent && step.Length == 0) step = $" {Number(1f)}";
            string name = mod.Per switch
            {
                Per.Armour => "Armour",
                Per.Evasion => "Evasion",
                Per.MaxHealth => "Maximum Health",
                Per.MaxMana => "Maximum Mana",
                Per.MaxBarrier => "Maximum Barrier",
                Per.MoveSpeedAboveBase => "% of Movement Speed above base",
                Per.Level => "Level",
                Per.Strength => "Strength",
                Per.Dexterity => "Dexterity",
                Per.Intellect => "Intellect",
                Per.Gold => "Gold carried",
                Per.ShrinesKindled => one ? "Shrine kindled in this land" : "Shrines kindled in this land",
                Per.RecentKills => one ? "Enemy Killed Recently" : "Enemies Killed Recently",
                Per.HealthSpent => "% of Health spent Recently",
                Per.Minions => one ? "Minion" : "Minions",
                Per.AlliesNear => one ? "Ally near you" : "Allies near you",
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
                ModKind.Override when mod.Stat == Stat.MinionCount => $"Pages raise only {Say.Whole(mod.Value)} of their Minions{Tail(mod)}",
                ModKind.Override => $"{Subject(mod)}is set to {Number(mod.Value)}{(StatNames.Of(mod.Stat).Percent ? "%" : "")}{Tail(mod)}",
                ModKind.Rule => DescribeRule(mod),
                ModKind.Grant => DescribeGrant(mod),
                ModKind.Divert => $"increased, reduced, more and less {StatNames.Of(mod.Stat).Name} apply to {Adjective(mod.Scope)}{StatNames.Of(mod.Into).Name} instead{Rate(mod)}",
                ModKind.Echo => $"increased, reduced, more and less {StatNames.Of(mod.Stat).Name} also apply to {Adjective(mod.Scope)}{StatNames.Of(mod.Into).Name}{Rate(mod)}",
                ModKind.Trigger => DescribeTrigger(mod),
                _ => "",
            };
        }

        private static string Rate(in Mod mod) => mod.Value == 100f ? "" : $", at {Number(mod.Value)}% of their value";

        private static string Subject(in Mod mod) => $"{Adjective(mod.Scope)}{StatNames.Of(mod.Stat).Name} ";

        private static string DescribeRule(in Mod mod)
        {
            var lines = new List<string>();
            foreach (RuleInfo info in RuleTable)
            {
                if ((mod.Rule & info.Rule) != 0) lines.Add(info.Text);
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
                TriggerEvent.Critical => "When you deal a Critical Hit",
                TriggerEvent.Kill => "When you Kill",
                TriggerEvent.Struck => "When you're Hit",
                TriggerEvent.Block => "When you Block",
                TriggerEvent.Dodge => "When you Dodge a Hit",
                _ => "When you use a Flask",
            };
            string chance = mod.Value >= 100f ? ", Trigger" : $", {Number(mod.Value)}% Chance to Trigger";
            return $"{when}{chance} {Shelf.Get(mod.Book).Name}";
        }

        // Damage narrowed to what lingers has a name of its own, and "Damage over Time Damage" isn't it.
        private static string Named(in Mod mod, string name)
        {
            return mod.Stat == Stat.Damage && (mod.Scope & Tags.DamageOverTime) != 0 ? "Damage over Time" : name;
        }

        private static string DescribeFlat(in Mod mod)
        {
            if ((mod.Scope & Tags.Minion) != 0) return $"Minions have {DescribeFlat(mod with { Scope = mod.Scope & ~Tags.Minion })}";

            StatName name = StatNames.Of(mod.Stat);
            if (MathF.Abs(mod.Value) == 1f) name = name with { Name = SingularOf(name.Name) };
            string sign = mod.Value >= 0f ? "+" : "-";
            string number = Number(mod.Value) + (name.Percent ? "%" : "");
            Tags scope = mod.Scope;
            bool toAttacks = (scope & Tags.Attack) != 0;
            bool toSpells = (scope & Tags.Spell) != 0;

            switch (mod.Stat)
            {
                case Stat.AddedPhysical or Stat.AddedFire or Stat.AddedCold or Stat.AddedLightning or Stat.AddedMagic or Stat.AddedPoison:
                    string to = toAttacks ? " to Attacks" : toSpells ? " to Spells" : "";
                    return $"{sign}{number} Added {Adjective(scope & ~(Tags.Attack | Tags.Spell))}{name.Name}{to}{Tail(mod)}";

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
                    return $"{sign}{number} Chance to {verb}{DoneWith(scope & ~Tags.Ailments)}{Tail(mod)}";

                case Stat.AilmentAvoid:
                    return $"{number} Chance to Avoid {(scope == Tags.None ? "Ailments" : Adjective(scope).Trim())}{Tail(mod)}";

                case Stat.FlatReduction:
                    return $"-{Number(mod.Value)} {Adjective(scope)}{name.Name}{Tail(mod)}";

                case Stat.BlockChance:
                    return $"{sign}{number} {(toSpells ? "Chance to Block Spells" : toAttacks ? "Chance to Block Attacks" : name.Name)}{Tail(mod)}";

                case Stat.Execute:
                    return $"Critical Hits Execute Enemies below {number} of Maximum Health{Tail(mod)}";

                case Stat.TakenFromMana or Stat.HealthLeech or Stat.ManaLeech or Stat.Reflect or Stat.ArmourIgnore or Stat.RaiseFallen or Stat.ExplodeOnKill or Stat.PhysicalReduction or Stat.HealthRegenPercent or Stat.ManaRegenPercent:
                    return $"{number} {Adjective(scope)}{name.Name}{Tail(mod)}";

                case Stat.BarrierDelay or Stat.MeleeReach:
                    return $"{sign}{number} {name.Name}{Tail(mod)}";
            }

            return $"{sign}{number} {Adjective(scope)}{name.Name}{Tail(mod)}";
        }

        // Whatever a chance is narrowed to, past the ailment and the weapon.
        private static string DoneWith(Tags scope)
        {
            Tags rest = scope & ~(Tags.Weapons | Tags.OneHanded | Tags.TwoHanded);
            if (rest == Tags.None) return "";
            if (rest == Tags.Attack) return " with Attacks";
            if (rest == Tags.Spell) return " with Spells";
            return $" with {Adjective(rest)}Pages";
        }

        private static string SingularOf(string name) => name switch
        {
            "Minions" => "Minion",
            "Projectiles" => "Projectile",
            "Chains" => "Chain",
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
            string name = Named(mod, StatNames.Of(mod.Stat).Name);
            Tags scope = mod.Scope & ~Tags.DamageOverTime;

            if ((scope & Tags.Minion) != 0)
            {
                return $"Minions have {Number(mod.Value)}% {word} {Adjective(scope & ~Tags.Minion)}{name}{Tail(mod)}";
            }

            if (mod.Stat is Stat.AilmentDuration or Stat.AilmentEffect or Stat.AilmentTaken)
            {
                string ailment = (scope & Tags.Ailments) != 0 ? Adjective(scope & Tags.Ailments).Trim() : "Ailment";
                return mod.Stat == Stat.AilmentEffect
                    ? $"{Number(mod.Value)}% {word} Effect of {ailment}{Tail(mod)}"
                    : $"{Number(mod.Value)}% {word} {ailment} {name}{Tail(mod)}";
            }

            return $"{Number(mod.Value)}% {word} {Adjective(scope)}{name}{Tail(mod)}";
        }
    }
}

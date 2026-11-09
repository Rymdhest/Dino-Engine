using Dino_Defenders.Progression;
using Dino_Defenders.Server;
using Dino_Defenders.Stats;
using static Dino_Defenders.Tooltips.Say;

namespace Dino_Defenders.Tooltips
{
    /// <summary>
    /// One line of the hero's sheet. <see cref="Stats"/> are what it adds up from, the first of them giving it its definition, and <see cref="Within"/> narrows those to what counts here: a resistance is one stat
    /// narrowed to a type. <see cref="Reads"/> is the finished number, always straight off a sheet, and <see cref="Notes"/> is whatever that number comes to in practice. A hero with nothing on reads zero on every
    /// line but Health and Mana: where the game has a fixed rule underneath, such as how fast anyone walks, the line is the bonus on top of it.
    /// </summary>
    public sealed class StatRow
    {
        public string Group = "";
        public string Name = "";
        public Stat[] Stats = Array.Empty<Stat>();
        public Func<ActorSheet, string> Reads = _ => "";

        /// <summary>Shown whatever it comes to. Anything else only shows if something in the build touches it.</summary>
        public bool Always;

        /// <summary>What a hero has of this with nothing on, as a number of its own in the sum.</summary>
        public float Base;

        /// <summary>What's on every hero as a rule rather than a stat, and so isn't part of the sum at all.</summary>
        public float Fixed;

        /// <summary>Whether the total is flat times what scales it, so that the sum can be written out.</summary>
        public bool Adds;

        /// <summary>The tags something has to be within to count here. Null for all of it.</summary>
        public Tags? Within;

        public Func<ActorSheet, IEnumerable<string>>? Notes;

        public bool Shows(ActorSheet sheet)
        {
            if (Always) return true;
            foreach (Stat stat in Stats)
            {
                foreach (ref readonly Mod mod in sheet.Loadout.On(stat))
                {
                    if (Counts(mod)) return true;
                }
            }
            return false;
        }

        public bool Counts(in Mod mod) => Within is not Tags within || (mod.Scope & ~within) == 0;
    }

    /// <summary>Everything on the hero's sheet, in order. A new stat needs a line here or BuildLab fails, so nothing a build can have is left off it.</summary>
    public static class StatBook
    {
        private static readonly List<StatRow> Everything = new();

        public static IReadOnlyList<StatRow> Rows => Everything;

        private static void Add(string group, string name, Stat stat, Func<ActorSheet, string> reads, bool always = false, float start = 0f, float rule = 0f, bool adds = false, Tags? within = null, Func<ActorSheet, IEnumerable<string>>? notes = null, params Stat[] also)
        {
            Everything.Add(new StatRow { Group = group, Name = name, Stats = also.Prepend(stat).ToArray(), Reads = reads, Always = always, Base = start, Fixed = rule, Adds = adds, Within = within, Notes = notes });
        }

        /// <summary>What a stat with no flat amount of its own comes to, as a change.</summary>
        private static string Change(ActorSheet sheet, Stat stat) => Signed(MathF.Round((sheet.Collect(stat, Tags.None).Scale - 1f) * 1000f) / 10f) + "%";

        /// <summary>What's added up with nothing narrowing it.</summary>
        private static string Sum(ActorSheet sheet, Stat stat) => Amount(sheet.Total(stat));

        /// <summary>What's on top of a fixed rule.</summary>
        private static float Above(ActorSheet sheet, Stat stat, float rule) => sheet.Total(stat, rule) - rule;

        private static IEnumerable<string> ArmourNotes(ActorSheet sheet)
        {
            float hit = Bestiary.DamageAt(sheet.Loadout.Level);
            yield return $"An ordinary Hit at your Level ({Whole(hit)}): {Rate(Hits.ArmourReduction(sheet.Armour, hit) * 100f)} reduced";
        }

        private static IEnumerable<string> EvasionNotes(ActorSheet sheet)
        {
            float accuracy = Bestiary.AccuracyAt(sheet.Loadout.Level);
            yield return $"An ordinary Attack at your Level ({Whole(accuracy)} Accuracy): {Rate(Hits.EvadeChance(sheet.Evasion, accuracy) * 100f)} miss";
        }

        private static IEnumerable<string> ResistanceNotes(ActorSheet sheet, DamageType type)
        {
            yield return $"Limit {Percent(sheet.MaxResistance[(int)type])}";
        }

        private static IEnumerable<string> AilmentNotes(float[] values, bool share)
        {
            for (int i = 0; i < AilmentRules.Count; i++)
            {
                float value = share ? values[i] : (values[i] - 1f) * 100f;
                if (value != 0f) yield return $"{Lexicon.Of((Ailment)i)!.Name}: {(share ? Rate(value) : Signed(value) + "%")}";
            }
        }

        // What a stat that's always narrowed to an ailment comes to for each of them.
        private static float[] EachAilment(ActorSheet sheet, Stat stat, float start)
        {
            var values = new float[AilmentRules.Count];
            for (int i = 0; i < values.Length; i++) values[i] = sheet.Total(stat, start, AilmentRules.Tag((Ailment)i));
            return values;
        }

        private static string Best(float[] values, bool share)
        {
            float best = 0f;
            foreach (float value in values)
            {
                float change = share ? value : (value - 1f) * 100f;
                if (MathF.Abs(change) > MathF.Abs(best)) best = change;
            }
            return share ? Rate(best) : Signed(best) + "%";
        }

        static StatBook()
        {
            const string attributes = "Attributes";
            Add(attributes, "Strength", Stat.Strength, s => Sum(s, Stat.Strength), always: true, adds: true);
            Add(attributes, "Dexterity", Stat.Dexterity, s => Sum(s, Stat.Dexterity), always: true, adds: true);
            Add(attributes, "Intellect", Stat.Intellect, s => Sum(s, Stat.Intellect), always: true, adds: true);

            const string pools = "Life";
            Add(pools, "Health", Stat.Health, s => Whole(s.MaxHealth), always: true, start: Rulebook.BaseHealth, adds: true);
            Add(pools, "Mana", Stat.Mana, s => Whole(s.MaxMana), always: true, start: Rulebook.BaseMana, adds: true);
            Add(pools, "Barrier", Stat.Barrier, s => Whole(s.MaxBarrier), always: true, adds: true);

            const string recovery = "Recovery";
            Add(recovery, "Health Regeneration", Stat.HealthRegen, s => $"{Amount(s.HealthRegen)} a second", always: true, also: Stat.HealthRegenPercent);
            Add(recovery, "Mana Regeneration", Stat.ManaRegen, s => $"{Amount(s.ManaRegenBonus)} a second", always: true, also: Stat.ManaRegenPercent);
            Add(recovery, "Barrier Recharge", Stat.BarrierRecharge, s => Signed(Above(s, Stat.BarrierRecharge, ActorSheet.BaseBarrierRecharge)) + "%");
            Add(recovery, "Barrier Delay", Stat.BarrierDelay, s => Signed(Above(s, Stat.BarrierDelay, ActorSheet.BaseBarrierDelay)) + " s");
            Add(recovery, "Recovery", Stat.Recovery, s => Change(s, Stat.Recovery));
            Add(recovery, "Health Leech", Stat.HealthLeech, s => Rate(s.Total(Stat.HealthLeech)));
            Add(recovery, "Mana Leech", Stat.ManaLeech, s => Rate(s.Total(Stat.ManaLeech)));
            Add(recovery, "Health on Hit", Stat.HealthOnHit, s => Sum(s, Stat.HealthOnHit));
            Add(recovery, "Mana on Hit", Stat.ManaOnHit, s => Sum(s, Stat.ManaOnHit));
            Add(recovery, "Health on Kill", Stat.HealthOnKill, s => Sum(s, Stat.HealthOnKill));
            Add(recovery, "Mana on Kill", Stat.ManaOnKill, s => Sum(s, Stat.ManaOnKill));
            Add(recovery, "Barrier on Kill", Stat.BarrierOnKill, s => Sum(s, Stat.BarrierOnKill));
            Add(recovery, "Flask Effect", Stat.FlaskEffect, s => Change(s, Stat.FlaskEffect));
            Add(recovery, "Health Drain", Stat.HealthDrain, s => Rate(s.HealthDrain));

            const string defence = "Defence";
            Add(defence, "Armour", Stat.Armour, s => Whole(s.Armour), always: true, adds: true, notes: ArmourNotes);
            Add(defence, "Armour also applies", Stat.ArmourApplies, s => Rate(MathF.Max(s.ArmourApplies[(int)DamageType.Fire], MathF.Max(s.ArmourApplies[(int)DamageType.Cold], s.ArmourApplies[(int)DamageType.Lightning]))));
            Add(defence, "Physical Damage Reduction", Stat.PhysicalReduction, s => Rate(s.PhysicalReduction));
            Add(defence, "Evasion", Stat.Evasion, s => Whole(s.Evasion), always: true, adds: true, notes: EvasionNotes);
            Add(defence, "Evasion against Spells", Stat.SpellEvasion, s => Rate(s.SpellEvasion));
            Add(defence, "Chance to Block Attacks", Stat.BlockChance, s => Rate(s.BlockAttacks), always: true, within: Tags.Attack);
            Add(defence, "Chance to Block Spells", Stat.BlockChance, s => Rate(s.BlockSpells), always: true, within: Tags.Spell);
            Add(defence, "Maximum Chance to Block", Stat.MaxBlock, s => Signed(Above(s, Stat.MaxBlock, ActorSheet.BaseMaxBlock)) + "%");
            foreach (DamageType type in DamageKinds.All)
            {
                if (type == DamageType.Physical) continue;
                DamageType kind = type;
                Add(defence, $"{type} Resistance", Stat.Resistance, s => Rate(s.Resistance[(int)kind]), always: true, within: DamageKinds.TagsOf(kind), notes: s => ResistanceNotes(s, kind), also: Stat.MaxResistance);
            }
            Add(defence, "Damage from Hits", Stat.FlatReduction, s => Signed(-s.FlatReductionAll));
            Add(defence, "Damage Taken", Stat.DamageTaken, s => Change(s, Stat.DamageTaken));
            Add(defence, "Damage Taken as", Stat.TakenAs, s => Rate(s.Total(Stat.TakenAs)));
            Add(defence, "Taken from Mana", Stat.TakenFromMana, s => Rate(s.TakenFromMana));
            Add(defence, "Stun Threshold", Stat.StunThreshold, s => Signed(Above(s, Stat.StunThreshold, ActorSheet.BaseStunThreshold)) + "%");
            Add(defence, "Reflect", Stat.Reflect, s => Rate(s.Reflect));
            Add(defence, "Chance to Avoid", Stat.AilmentAvoid, s => Best(s.AilmentAvoid, true), notes: s => AilmentNotes(s.AilmentAvoid, true));
            Add(defence, "Duration on You", Stat.AilmentTaken, s => Best(s.AilmentTaken, false), notes: s => AilmentNotes(s.AilmentTaken, false));

            const string offence = "Offence";
            Add(offence, "Damage", Stat.Damage, s => Change(s, Stat.Damage));
            foreach (DamageType type in DamageKinds.All)
            {
                Stat added = M.AddedStat(type);
                Add(offence, $"Added {type} Damage", added, s => Sum(s, added));
            }
            Add(offence, "Damage Converted", Stat.Convert, s => Rate(s.Total(Stat.Convert)));
            Add(offence, "Damage Gained as extra", Stat.GainAs, s => Rate(s.Total(Stat.GainAs)));
            Add(offence, "Attack Speed", Stat.AttackSpeed, s => Change(s, Stat.AttackSpeed));
            Add(offence, "Cast Speed", Stat.CastSpeed, s => Change(s, Stat.CastSpeed));
            Add(offence, "Use Speed", Stat.UseSpeed, s => Change(s, Stat.UseSpeed));
            Add(offence, "Critical Chance", Stat.CritChance, s => Change(s, Stat.CritChance));
            Add(offence, "Critical Multiplier", Stat.CritMultiplier, s => Signed(Above(s, Stat.CritMultiplier, ActorSheet.BaseCritMultiplier)) + "%");
            Add(offence, "Accuracy", Stat.Accuracy, s => Whole(s.Total(Stat.Accuracy)), always: true, adds: true);
            Add(offence, "Resistance Penetration", Stat.Penetration, s => Rate(s.Total(Stat.Penetration)));
            Add(offence, "Enemy Armour Ignored", Stat.ArmourIgnore, s => Rate(s.Total(Stat.ArmourIgnore)));
            Add(offence, "Ailment Chance", Stat.AilmentChance, s => Best(EachAilment(s, Stat.AilmentChance, 0f), true), notes: s => AilmentNotes(EachAilment(s, Stat.AilmentChance, 0f), true));
            Add(offence, "Ailment Duration", Stat.AilmentDuration, s => Best(EachAilment(s, Stat.AilmentDuration, 1f), false), notes: s => AilmentNotes(EachAilment(s, Stat.AilmentDuration, 1f), false));
            Add(offence, "Ailment Effect", Stat.AilmentEffect, s => Best(EachAilment(s, Stat.AilmentEffect, 1f), false), notes: s => AilmentNotes(EachAilment(s, Stat.AilmentEffect, 1f), false));
            Add(offence, "Execute", Stat.Execute, s => Rate(s.Total(Stat.Execute)));
            Add(offence, "Reaction Damage", Stat.ReactionDamage, s => Change(s, Stat.ReactionDamage));
            Add(offence, "Reaction Area", Stat.ReactionArea, s => Change(s, Stat.ReactionArea));
            Add(offence, "Explosion on Kill", Stat.ExplodeOnKill, s => Rate(s.ExplodeOnKill));

            const string skills = "Books";
            Add(skills, "Area of Effect", Stat.AreaOfEffect, s => Change(s, Stat.AreaOfEffect));
            Add(skills, "Melee Reach", Stat.MeleeReach, s => Metres(s.Total(Stat.MeleeReach)));
            Add(skills, "Projectile Speed", Stat.ProjectileSpeed, s => Change(s, Stat.ProjectileSpeed));
            Add(skills, "Projectiles", Stat.ProjectileCount, s => Signed(s.Total(Stat.ProjectileCount)));
            Add(skills, "Pierce", Stat.Pierce, s => Signed(s.Total(Stat.Pierce)));
            Add(skills, "Chains", Stat.Chain, s => Signed(s.Total(Stat.Chain)));
            Add(skills, "Page Duration", Stat.PageDuration, s => Change(s, Stat.PageDuration));
            Add(skills, "Cooldown Recovery", Stat.CooldownRecovery, s => Signed(Above(s, Stat.CooldownRecovery, 100f)) + "%");
            Add(skills, "Book Cost", Stat.BookCost, s => Change(s, Stat.BookCost));
            Add(skills, "Potency", Stat.Potency, s => Change(s, Stat.Potency));

            const string company = "Minions";
            Add(company, "Minions", Stat.MinionCount, s => Signed(s.Total(Stat.MinionCount)));
            Add(company, "Minion Duration", Stat.MinionDuration, s => Change(s, Stat.MinionDuration));
            Add(company, "Chance to Rise", Stat.RaiseFallen, s => Rate(s.RaiseFallen));

            const string about = "Utility";
            Add(about, "Movement Speed", Stat.MoveSpeed, s => Change(s, Stat.MoveSpeed), always: true, rule: HeroState.BaseMoveSpeed);
            Add(about, "Dodge Cooldown", Stat.DodgeCooldown, s => Change(s, Stat.DodgeCooldown), always: true);
            Add(about, "Dodge Speed", Stat.DodgeSpeed, s => Change(s, Stat.DodgeSpeed));
            Add(about, "Stealth", Stat.Stealth, s => Rate(s.Stealth));
            Add(about, "Item Rarity", Stat.ItemRarity, s => Rate(s.ItemRarity));
            Add(about, "Gold Found", Stat.GoldFind, s => Rate(s.GoldFind));
            Add(about, "Experience Gained", Stat.ExperienceGain, s => Rate(s.ExperienceGain));
        }
    }
}

using Dino_Defenders.Skills;

namespace Dino_Defenders.Stats
{
    /// <summary>
    /// A share of a blow that has become one type, by way of whatever it has been. Modifiers apply to everything it remembers being, so its
    /// tags are the whole of its past, and it comes with what they added up to.
    /// </summary>
    public struct DamageFlow
    {
        public DamageType Type;
        public int Lineage;         // A bit for each type it has been.
        public float Min;
        public float Max;
        public float Increased;
        public float More;
    }

    /// <summary>Something that only counts against certain foes, so it has to wait for a blow to land. Most skills have none of these.</summary>
    public readonly record struct FoeBonus(Stat Stat, ModKind Kind, Condition Foe, float Value, uint Flows);

    /// <summary>What a skill does about one ailment.</summary>
    public struct AilmentProfile
    {
        public float Chance;        // Percent.
        public float Duration;      // Multiple of normal.
        public float Effect;        // Multiple of normal.
    }

    /// <summary>
    /// One actor's numbers for one skill. This is the point of the whole system: everything that can be worked out ahead of a blow is worked out
    /// here, once, so a blow itself is a few multiplications however many modifiers went into it. Sheets are shared, and so these are too,
    /// so a hundred archers of one kind are one of these.
    /// </summary>
    public sealed class SkillProfile
    {
        private const int MostFlows = 24;

        private readonly DamageFlow[] _flows;

        public readonly ActorSheet Sheet;
        public readonly SkillDefinition Skill;
        public readonly int Index;

        /// <summary>Everything this skill counts as, including every type of damage it turns out to deal.</summary>
        public readonly Tags Tags;

        public readonly float UseTime;
        public readonly float ImpactTime;
        public readonly float Cooldown;
        public readonly float Cost;
        public readonly bool CostsHealth;

        public readonly float Reach;
        public readonly float Radius;
        public readonly float Range;
        public readonly float Speed;
        public readonly float Duration;
        public readonly float Interval;
        public readonly float Delay;
        public readonly int Projectiles;
        public readonly int Pierce;
        public readonly int Chains;
        public readonly int MinionCount;

        public readonly float CritChance;
        public readonly float CritMultiplier;
        public readonly float Accuracy;
        public readonly float ArmourIgnore;
        public DamageTable Penetration;
        public readonly FoeBonus[] FoeBonuses;
        public readonly Condition FoeConditions;

        public readonly float HealthLeech;
        public readonly float ManaLeech;
        public readonly float HealthOnHit;
        public readonly float ManaOnHit;
        public readonly float HealthOnKill;
        public readonly float ManaOnKill;
        public readonly float BarrierOnKill;
        public readonly float Execute;
        public readonly float ReactionScale;
        public readonly float ReactionArea;

        public readonly AilmentProfile[] Ailments = new AilmentProfile[AilmentRules.Count];

        /// <summary>What lingering damage gets on top of the hit it came from, for each ailment that does any.</summary>
        public readonly float[] LingerScale = new float[AilmentRules.Count];

        /// <summary>What it starts from, by type, before flat damage is added to it: its share of the weapon, or what a spell has at this level.</summary>
        public DamageTable BaseMin;
        public DamageTable BaseMax;

        /// <summary>Flat damage it gets, by type, after its own share of that.</summary>
        public DamageTable Added;

        /// <summary>The level a spell's damage is read at, with any levels of skills.</summary>
        public float SkillLevel;

        public ReadOnlySpan<DamageFlow> Flows => _flows;
        public bool DealsDamage => _flows.Length > 0;

        internal SkillProfile(ActorSheet sheet, SkillDefinition skill, int index)
        {
            Sheet = sheet;
            Skill = skill;
            Index = index;
            WeaponProfile weapon = sheet.Loadout.Weapon;

            // A weapon's kind only says anything about what's done with it.
            Tags tags = skill.Tags;
            if (skill.IsAttack) tags |= weapon.Tags;

            _flows = BuildFlows(sheet, skill, tags, weapon);

            int lineage = 0;
            foreach (ref readonly DamageFlow flow in _flows.AsSpan()) lineage |= flow.Lineage;
            Tags all = tags | DamageKinds.TagsOf(lineage);
            Tags = all;

            for (int i = 0; i < _flows.Length; i++)
            {
                ref DamageFlow flow = ref _flows[i];
                StatSum sum = sheet.Collect(Stat.Damage, tags | Tags.Hit | DamageKinds.TagsOf(flow.Lineage), skill);
                flow.Increased = sum.Increased;
                flow.More = sum.More;
            }

            // Using it.
            float useTime = skill.UseTime;
            if (skill.IsAttack)
            {
                useTime *= weapon.AttackTime / WeaponProfile.BareTime;
                useTime /= MathF.Max(sheet.Total(Stat.AttackSpeed, 1f, all, skill), 0.05f);
            }
            else if (skill.IsSpell)
            {
                useTime /= MathF.Max(sheet.Total(Stat.CastSpeed, 1f, all, skill), 0.05f);
            }
            UseTime = MathF.Max(useTime, Rulebook.ShortestUse);
            ImpactTime = UseTime * skill.ImpactAt;

            float cost = sheet.Total(Stat.SkillCost, skill.Cost * CostAtLevel(sheet.Loadout.Level), all, skill);
            float cooldown = skill.Cooldown / MathF.Max(sheet.Total(Stat.CooldownRecovery, 100f, all, skill) / 100f, 0.05f);
            if (sheet.Has(Rule.NoCooldowns) && (skill.Tags & Tags.Movement) == 0)
            {
                cost *= 1f + Rulebook.NoCooldownCost * cooldown;
                cooldown = 0f;
            }
            Cost = MathF.Max(cost, 0f);
            Cooldown = cooldown;
            CostsHealth = sheet.Has(Rule.CostsHealth);

            // Reaching.
            float areaScale = MathF.Sqrt(MathF.Max(sheet.Collect(Stat.AreaOfEffect, all, skill).Scale, 0.01f));
            Radius = skill.Radius * areaScale;
            Reach = (skill.Reach + ((all & Tags.Melee) != 0 ? sheet.Total(Stat.MeleeReach, 0f, all, skill) : 0f)) * ((all & Tags.Area) != 0 ? areaScale : 1f);
            Range = skill.Range;
            Speed = sheet.Total(Stat.ProjectileSpeed, skill.Speed, all, skill);
            Duration = sheet.Total(Stat.SkillDuration, skill.Buff?.Duration ?? skill.Duration, all, skill);
            Interval = skill.Interval;
            Delay = skill.Delay;
            Projectiles = Math.Clamp((int)MathF.Round(sheet.Total(Stat.ProjectileCount, skill.Projectiles, all, skill)), 1, 32);
            Pierce = Math.Max((int)MathF.Round(sheet.Total(Stat.Pierce, skill.Pierce, all, skill)), 0);
            Chains = Math.Max((int)MathF.Round(sheet.Total(Stat.Chain, skill.Chains, all, skill)), 0);
            MinionCount = Math.Max((int)MathF.Round(sheet.Total(Stat.MinionCount, skill.MinionCount, all, skill)), 0);

            // Landing.
            float baseCrit = skill.IsAttack ? weapon.CritChance : skill.CritChance;
            CritChance = sheet.Has(Rule.NeverCrit) ? 0f : Math.Clamp(sheet.Total(Stat.CritChance, baseCrit, all, skill), 0f, 100f);
            CritMultiplier = MathF.Max(sheet.Total(Stat.CritMultiplier, ActorSheet.BaseCritMultiplier, all, skill), 100f);
            Accuracy = MathF.Max(sheet.Total(Stat.Accuracy, 0f, all, skill), 0f);
            ArmourIgnore = Math.Clamp(sheet.Total(Stat.ArmourIgnore, 0f, all, skill), 0f, 100f);
            for (int i = 0; i < DamageKinds.Count; i++)
            {
                Penetration[i] = sheet.Total(Stat.Penetration, 0f, tags | DamageKinds.TagsOf((DamageType)i), skill);
            }
            (FoeBonuses, FoeConditions) = BuildFoeBonuses(sheet, skill, tags);

            // What follows.
            bool leeches = !sheet.Has(Rule.NoLeech);
            HealthLeech = leeches ? sheet.Total(Stat.HealthLeech, 0f, all, skill) / 100f : 0f;
            ManaLeech = leeches ? sheet.Total(Stat.ManaLeech, 0f, all, skill) / 100f : 0f;
            HealthOnHit = sheet.Total(Stat.HealthOnHit, 0f, all, skill);
            ManaOnHit = sheet.Total(Stat.ManaOnHit, 0f, all, skill);
            HealthOnKill = sheet.Total(Stat.HealthOnKill, 0f, all, skill);
            ManaOnKill = sheet.Total(Stat.ManaOnKill, 0f, all, skill);
            BarrierOnKill = sheet.Total(Stat.BarrierOnKill, 0f, all, skill);
            Execute = Math.Clamp(sheet.Total(Stat.Execute, 0f, all, skill), 0f, 100f);
            ReactionScale = sheet.Total(Stat.ReactionDamage, 1f, all, skill);
            ReactionArea = MathF.Sqrt(MathF.Max(sheet.Total(Stat.ReactionArea, 1f, all, skill), 0.01f));

            for (int i = 0; i < AilmentRules.Count; i++)
            {
                var ailment = (Ailment)i;
                Tags tag = AilmentRules.Tag(ailment);
                Ailments[i] = new AilmentProfile
                {
                    Chance = Math.Clamp(sheet.Total(Stat.AilmentChance, 0f, all | tag, skill), 0f, 100f),
                    Duration = MathF.Max(sheet.Total(Stat.AilmentDuration, 1f, all | tag, skill), 0f),
                    Effect = MathF.Max(sheet.Total(Stat.AilmentEffect, 1f, all | tag, skill), 0f),
                };
                LingerScale[i] = LingerFor(sheet, skill, all, tag);
            }
        }

        public static float CostAtLevel(int level) => 1f + Rulebook.CostPerLevel * Math.Max(level - 1, 0);

        // A lingering ailment feeds on the hit that caused it, so whatever made that stronger has done its work already. What's left to add
        // is whatever names lingering damage or the ailment itself, which never touched the hit. That way nothing counts twice.
        private static float LingerFor(ActorSheet sheet, SkillDefinition skill, Tags all, Tags ailment)
        {
            StatSum sum = StatSum.Empty;
            Tags naming = Tags.DamageOverTime | ailment;
            Tags tags = all | naming;
            foreach (ref readonly Mod mod in sheet.Loadout.On(Stat.Damage))
            {
                if ((mod.Scope & naming) == 0 || (mod.Scope & ~tags) != 0 || mod.Foe != Condition.None) continue;
                if (mod.Skill != SkillDefinition.None && mod.Skill != skill.Number) continue;
                if (sheet.WorthIfHeld(mod) is float value) sum.Add(mod, value);
            }
            return sum.Scale;
        }

        private DamageFlow[] BuildFlows(ActorSheet sheet, SkillDefinition skill, Tags tags, in WeaponProfile weapon)
        {
            if (sheet.Has(Rule.NoOwnDamage) || !skill.DealsDamage) return Array.Empty<DamageFlow>();

            DamageTable min = default;
            DamageTable max = default;

            if (skill.IsAttack)
            {
                min[(int)DamageType.Physical] = weapon.Min * skill.WeaponShare;
                max[(int)DamageType.Physical] = weapon.Max * skill.WeaponShare;
            }

            // Nothing is known yet of what it might turn into, but what it starts as is, and levels can be for that.
            Tags starts = tags;
            foreach (BaseDamage part in skill.Damage) starts |= DamageKinds.TagsOf(part.Type);
            SkillLevel = sheet.Loadout.Level + sheet.Total(Stat.SkillLevel, 0f, starts, skill);
            float power = SkillDefinition.SkillPower(SkillLevel);
            foreach (BaseDamage part in skill.Damage)
            {
                min[(int)part.Type] += part.Min * power;
                max[(int)part.Type] += part.Max * power;
            }

            BaseMin = min;
            BaseMax = max;

            for (int i = 0; i < DamageKinds.Count; i++)
            {
                var type = (DamageType)i;
                float added = sheet.Total(M.AddedStat(type), 0f, tags | DamageKinds.TagsOf(type), skill) * skill.AddedEffect;
                Added[i] = added;
                min[i] += added * (1f - Rulebook.AddedSpread);
                max[i] += added * (1f + Rulebook.AddedSpread);
            }

            var flows = new List<DamageFlow>(8);
            var converts = new float[DamageKinds.Count * DamageKinds.Count];
            var gains = new float[DamageKinds.Count * DamageKinds.Count];
            ReadShifts(sheet, skill, tags, Stat.Convert, converts);
            ReadShifts(sheet, skill, tags, Stat.GainAs, gains);

            for (int i = 0; i < DamageKinds.Count; i++)
            {
                if (max[i] <= 0f) continue;
                Follow(flows, converts, gains, i, 1 << i, min[i], max[i]);
            }
            return flows.ToArray();
        }

        private static void ReadShifts(ActorSheet sheet, SkillDefinition skill, Tags tags, Stat stat, float[] table)
        {
            foreach (ref readonly Mod mod in sheet.Loadout.On(stat)) Read(sheet, mod, skill, tags, table);
            foreach (ref readonly Mod mod in skill.Mods.AsSpan())
            {
                if (mod.Stat == stat && mod.Kind == ModKind.Flat) Read(sheet, mod, null, tags, table);
            }
        }

        private static void Read(ActorSheet sheet, in Mod mod, SkillDefinition? skill, Tags tags, float[] table)
        {
            if (mod.From == mod.To) return;
            if (mod.Skill != SkillDefinition.None && mod.Skill != skill?.Number) return;
            Tags from = tags | DamageKinds.TagsOf(mod.From);
            if ((mod.Scope & ~from) != 0 || mod.Foe != Condition.None) return;
            if (sheet.WorthIfHeld(mod) is float value) table[(int)mod.From * DamageKinds.Count + (int)mod.To] += value / 100f;
        }

        // Damage can become anything it hasn't been. It can't turn back into what it has been, so however conversions point at each other, this
        // ends. Whatever is gained as extra is taken from what's here before any of it moves on.
        private static void Follow(List<DamageFlow> flows, float[] converts, float[] gains, int type, int lineage, float min, float max)
        {
            int row = type * DamageKinds.Count;
            for (int to = 0; to < DamageKinds.Count; to++)
            {
                float gain = gains[row + to];
                if (gain > 0f && (lineage & (1 << to)) == 0) Follow(flows, converts, gains, to, lineage | (1 << to), min * gain, max * gain);
            }

            float total = 0f;
            for (int to = 0; to < DamageKinds.Count; to++)
            {
                if ((lineage & (1 << to)) == 0) total += converts[row + to];
            }

            // Past all of it, conversions share what there is between them.
            float scale = total > 1f ? 1f / total : 1f;
            for (int to = 0; to < DamageKinds.Count; to++)
            {
                float share = converts[row + to] * scale;
                if (share > 0f && (lineage & (1 << to)) == 0) Follow(flows, converts, gains, to, lineage | (1 << to), min * share, max * share);
            }

            float left = 1f - total * scale;
            if (left > 1e-4f) Keep(flows, type, lineage, min * left, max * left);
        }

        private static void Keep(List<DamageFlow> flows, int type, int lineage, float min, float max)
        {
            for (int i = 0; i < flows.Count; i++)
            {
                DamageFlow flow = flows[i];
                if ((int)flow.Type != type || flow.Lineage != lineage) continue;
                flow.Min += min;
                flow.Max += max;
                flows[i] = flow;
                return;
            }

            if (flows.Count >= MostFlows)
            {
                // Someone with every conversion there is. Nothing is lost; it just keeps a simpler memory of how it got here.
                for (int i = 0; i < flows.Count; i++)
                {
                    DamageFlow flow = flows[i];
                    if ((int)flow.Type != type) continue;
                    flow.Min += min;
                    flow.Max += max;
                    flows[i] = flow;
                    return;
                }
            }

            flows.Add(new DamageFlow { Type = (DamageType)type, Lineage = lineage, Min = min, Max = max, More = 1f });
        }

        private (FoeBonus[], Condition) BuildFoeBonuses(ActorSheet sheet, SkillDefinition skill, Tags tags)
        {
            List<FoeBonus>? bonuses = null;
            Condition asked = Condition.None;

            void Gather(Stat stat, ReadOnlySpan<Mod> mods, bool own)
            {
                foreach (ref readonly Mod mod in mods)
                {
                    if (mod.Stat != stat || mod.Foe == Condition.None || mod.Kind > ModKind.More) continue;
                    if (!own && mod.Skill != SkillDefinition.None && mod.Skill != skill.Number) continue;
                    if (sheet.WorthIfHeld(mod) is not float value) continue;

                    uint reaches = 0;
                    for (int i = 0; i < _flows.Length && i < 32; i++)
                    {
                        Tags flowTags = tags | Tags.Hit | DamageKinds.TagsOf(_flows[i].Lineage);
                        if ((mod.Scope & ~flowTags) == 0) reaches |= 1u << i;
                    }
                    if (reaches == 0) continue;

                    (bonuses ??= new()).Add(new FoeBonus(stat, mod.Kind, mod.Foe, value, reaches));
                    asked |= mod.Foe;
                }
            }

            foreach (Stat stat in new[] { Stat.Damage, Stat.CritChance, Stat.CritMultiplier })
            {
                Gather(stat, sheet.Loadout.On(stat), own: false);
                Gather(stat, skill.Mods, own: true);
            }
            return (bonuses?.ToArray() ?? Array.Empty<FoeBonus>(), asked);
        }

        /// <summary>What a typical hit comes to, for a tooltip.</summary>
        public float AverageHit
        {
            get
            {
                float total = 0f;
                foreach (ref readonly DamageFlow flow in _flows.AsSpan())
                {
                    total += (flow.Min + flow.Max) * 0.5f * MathF.Max(1f + flow.Increased / 100f, 0f) * flow.More;
                }
                return total * (1f + CritChance / 100f * (CritMultiplier / 100f - 1f));
            }
        }

        /// <summary>How many times one use reaches something standing in it the whole time.</summary>
        public int Pulses => Skill.Form == SkillForm.Zone ? 1 + (int)((MathF.Max(Duration, Rulebook.ShortestZone) - Rulebook.FirstPulse) / MathF.Max(Interval, 0.01f)) : 1;

        /// <summary>What it comes to in a second of use on one target, for a tooltip.</summary>
        public float DamagePerSecond => AverageHit * Pulses / MathF.Max(UseTime, Cooldown);

        /// <summary>What a share of a blow comes to as it leaves, before any critical hit.</summary>
        public static float Scale(in DamageFlow flow) => MathF.Max(1f + flow.Increased / 100f, 0f) * MathF.Max(flow.More, 0f);
    }
}

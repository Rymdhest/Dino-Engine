using Dino_Defenders.Books;

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

    /// <summary>Something that only counts against certain foes, so it has to wait for a blow to land. Most pages have none of these.</summary>
    public readonly record struct FoeBonus(Stat Stat, ModKind Kind, Condition Foe, float Value, uint Flows);

    /// <summary>What a page does about one ailment.</summary>
    public struct AilmentProfile
    {
        public float Chance;        // Percent.
        public float Duration;      // Multiple of normal.
        public float Effect;        // Multiple of normal.
    }

    /// <summary>
    /// One actor's numbers for one page. This is the point of the whole system: everything that can be worked out ahead of a blow is worked out here, once, so a blow itself is a few multiplications however many
    /// modifiers went into it. Sheets are shared, and so these are too, so a hundred archers of one kind are one of these.
    /// </summary>
    public sealed class PageProfile
    {
        private const int MostFlows = 24;

        private readonly DamageFlow[] _flows;

        public readonly ActorSheet Sheet;
        public readonly PagePlan Page;

        /// <summary>Everything this page counts as, including every type of damage it turns out to deal.</summary>
        public readonly Tags Tags;

        /// <summary>What it looks like to anyone watching.</summary>
        public readonly PageSign Sign;

        public readonly float UseTime;
        public readonly float ImpactTime;
        public readonly float Cooldown;
        public readonly float Cost;

        /// <summary>How far a shape reaches, or its radius, and how wide it is: radians for a cone, and otherwise metres either side of the middle.</summary>
        public readonly float Length;
        public readonly float Width;
        public readonly float Range;
        public readonly float Speed;
        public readonly float Duration;
        public readonly float Interval;
        public readonly int Projectiles;
        public readonly int Pierce;
        public readonly int Chains;
        public readonly int MinionCount;

        /// <summary>What sentences and the rest of a build have made of everything it does.</summary>
        public readonly float Potency;

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

        /// <summary>What it puts on someone outright, each as strong as a hit of this much would have left. Nothing means it doesn't.</summary>
        public readonly float[] Inflicts = new float[AilmentRules.Count];

        /// <summary>What each of its heals comes to, in the order of <see cref="PagePlan.Heals"/>.</summary>
        public readonly float[] Heals;

        /// <summary>How long what it gives lasts, in the order of <see cref="PagePlan.Gifts"/>.</summary>
        public readonly float[] GiftSeconds;

        /// <summary>What it starts from, by type, before flat damage is added to it.</summary>
        public DamageTable BaseMin;
        public DamageTable BaseMax;

        /// <summary>Flat damage it gets, by type.</summary>
        public DamageTable Added;

        public ReadOnlySpan<DamageFlow> Flows => _flows;
        public bool DealsDamage => _flows.Length > 0;

        /// <summary>Whether it's an Attack, which is what's in the hands and not what's on the page.</summary>
        public bool IsAttack => (Tags & Tags.Attack) != 0;

        internal PageProfile(ActorSheet sheet, PagePlan page)
        {
            Sheet = sheet;
            Page = page;
            WeaponProfile weapon = sheet.Loadout.Weapon;

            // A wand or a staff makes it a Spell, and anything else an Attack. What's thrown or shot is no more Melee than a Spell is.
            bool spell = (weapon.Tags & (Tags.Wand | Tags.Staff)) != 0;
            Tags tags = page.Tags | weapon.Tags | (spell ? Tags.Spell : Tags.Attack);
            if (!spell && page.Kind == PageKind.Area && page.From == Start.You && (weapon.Tags & Tags.Bow) == 0) tags |= Tags.Melee;

            _flows = BuildFlows(sheet, page, tags, weapon);

            int lineage = 0;
            foreach (ref readonly DamageFlow flow in _flows.AsSpan()) lineage |= flow.Lineage;
            Tags all = tags | DamageKinds.TagsOf(lineage);
            Tags = all;

            // It's the same in everything a page does, and goes by everything it turns out to be.
            Potency = Math.Clamp(page.Potency * sheet.Collect(Stat.Potency, all, page).Scale, Rulebook.LeastPotency, Rulebook.MostPotency);

            for (int i = 0; i < _flows.Length; i++)
            {
                ref DamageFlow flow = ref _flows[i];
                StatSum sum = sheet.Collect(Stat.Damage, tags | Tags.Hit | DamageKinds.TagsOf(flow.Lineage), page);
                flow.Increased = sum.Increased;
                flow.More = sum.More * Potency;
            }

            // Using it. It's the weapon that sets the pace, whichever of the two this is.
            float useTime = page.UseTime * weapon.AttackTime / WeaponProfile.BareTime;
            useTime /= MathF.Max(sheet.Total(spell ? Stat.CastSpeed : Stat.AttackSpeed, 1f, all, page), 0.05f);
            UseTime = MathF.Max(useTime, Rulebook.ShortestUse);
            ImpactTime = UseTime * page.ImpactAt;

            Cost = MathF.Max(sheet.Total(Stat.BookCost, page.Cost * CostAtLevel(sheet.Loadout.Level), all, page), 0f);
            Cooldown = page.Cooldown / MathF.Max(sheet.Total(Stat.CooldownRecovery, 100f, all, page) / 100f, 0.05f);

            // Reaching.
            float areaScale = MathF.Sqrt(MathF.Max(sheet.Collect(Stat.AreaOfEffect, all, page).Scale, 0.01f));
            float reach = (all & Tags.Melee) != 0 ? sheet.Total(Stat.MeleeReach, 0f, all, page) : 0f;
            Length = (page.Length + reach) * areaScale;
            Width = page.Shape == AreaShape.Cone ? page.Width : page.Width * areaScale;
            Range = page.Range;
            Speed = sheet.Total(Stat.ProjectileSpeed, page.Speed, all, page);
            Duration = sheet.Total(Stat.PageDuration, page.Duration, all, page);
            Interval = page.Interval;
            Projectiles = Math.Clamp((int)MathF.Round(sheet.Total(Stat.ProjectileCount, page.Projectiles, all, page)), 1, Rulebook.MostMade);
            Pierce = Math.Max((int)MathF.Round(sheet.Total(Stat.Pierce, page.Pierce, all, page)), 0);
            Chains = Math.Max((int)MathF.Round(sheet.Total(Stat.Chain, page.Chains, all, page)), 0);
            MinionCount = Math.Max((int)MathF.Round(sheet.Total(Stat.MinionCount, page.MinionCount, all, page)), 0);

            // Landing.
            CritChance = sheet.Has(Rule.NeverCrit) ? 0f : Math.Clamp(sheet.Total(Stat.CritChance, weapon.CritChance, all, page), 0f, 100f);
            CritMultiplier = MathF.Max(sheet.Total(Stat.CritMultiplier, ActorSheet.BaseCritMultiplier, all, page), 100f);
            Accuracy = MathF.Max(sheet.Total(Stat.Accuracy, 0f, all, page), 0f);
            ArmourIgnore = Math.Clamp(sheet.Total(Stat.ArmourIgnore, 0f, all, page), 0f, 100f);
            for (int i = 0; i < DamageKinds.Count; i++)
            {
                Penetration[i] = sheet.Total(Stat.Penetration, 0f, tags | DamageKinds.TagsOf((DamageType)i), page);
            }
            (FoeBonuses, FoeConditions) = BuildFoeBonuses(sheet, page, tags);

            // What follows.
            bool leeches = !sheet.Has(Rule.NoLeech);
            HealthLeech = leeches ? sheet.Total(Stat.HealthLeech, 0f, all, page) / 100f : 0f;
            ManaLeech = leeches ? sheet.Total(Stat.ManaLeech, 0f, all, page) / 100f : 0f;
            HealthOnHit = sheet.Total(Stat.HealthOnHit, 0f, all, page);
            ManaOnHit = sheet.Total(Stat.ManaOnHit, 0f, all, page);
            HealthOnKill = sheet.Total(Stat.HealthOnKill, 0f, all, page);
            ManaOnKill = sheet.Total(Stat.ManaOnKill, 0f, all, page);
            BarrierOnKill = sheet.Total(Stat.BarrierOnKill, 0f, all, page);
            Execute = Math.Clamp(sheet.Total(Stat.Execute, 0f, all, page), 0f, 100f);
            ReactionScale = sheet.Total(Stat.ReactionDamage, 1f, all, page);
            ReactionArea = MathF.Sqrt(MathF.Max(sheet.Total(Stat.ReactionArea, 1f, all, page), 0.01f));

            for (int i = 0; i < AilmentRules.Count; i++)
            {
                var ailment = (Ailment)i;
                Tags tag = AilmentRules.Tag(ailment);
                Ailments[i] = new AilmentProfile
                {
                    Chance = Math.Clamp(sheet.Total(Stat.AilmentChance, 0f, all | tag, page), 0f, 100f),
                    Duration = MathF.Max(sheet.Total(Stat.AilmentDuration, 1f, all | tag, page), 0f),
                    Effect = MathF.Max(sheet.Total(Stat.AilmentEffect, 1f, all | tag, page), 0f),
                };
                LingerScale[i] = LingerFor(sheet, all, tag);
            }

            foreach (AilmentPart part in page.Ailments)
            {
                (float low, float high) = Reading(sheet, part.Measure, weapon);
                Inflicts[(int)part.Ailment] += (low + high) * 0.5f * part.Share * Potency;
            }

            Heals = new float[page.Heals.Length];
            for (int i = 0; i < Heals.Length; i++)
            {
                (float low, float high) = Reading(sheet, page.Heals[i].Measure, weapon);
                Heals[i] = (low + high) * 0.5f * page.Heals[i].Share * Potency;
            }

            GiftSeconds = new float[page.Gifts.Length];
            for (int i = 0; i < GiftSeconds.Length; i++) GiftSeconds[i] = sheet.Total(Stat.PageDuration, page.Gifts[i].Buff.Duration, all, page);

            Sign = PageSign.Of(page, spell, UseTime);
        }

        /// <summary>What a measure comes to on this sheet, as the two ends of a roll: only a weapon has any spread.</summary>
        private static (float low, float high) Reading(ActorSheet sheet, Measure measure, in WeaponProfile weapon)
        {
            float value = measure switch
            {
                Measure.Health => sheet.MaxHealth,
                Measure.Mana => sheet.MaxMana,
                Measure.Barrier => sheet.MaxBarrier,
                Measure.Armour => sheet.Armour,
                Measure.Evasion => sheet.Evasion,
                Measure.Strength => sheet.Total(Stat.Strength),
                Measure.Dexterity => sheet.Total(Stat.Dexterity),
                Measure.Intellect => sheet.Total(Stat.Intellect),
                _ => -1f,
            };
            return value >= 0f ? (value, value) : (weapon.Min, weapon.Max);
        }

        public static float CostAtLevel(int level) => 1f + Rulebook.CostPerLevel * Math.Max(level - 1, 0);

        // A lingering ailment feeds on the hit that caused it, so whatever made that stronger has done its work already. What's left to add
        // is whatever names lingering damage or the ailment itself, which never touched the hit. That way nothing counts twice.
        private static float LingerFor(ActorSheet sheet, Tags all, Tags ailment)
        {
            StatSum sum = StatSum.Empty;
            Tags naming = Tags.DamageOverTime | ailment;
            Tags tags = all | naming;
            foreach (ref readonly Mod mod in sheet.Loadout.On(Stat.Damage))
            {
                if ((mod.Scope & naming) == 0 || (mod.Scope & ~tags) != 0 || mod.Foe != Condition.None) continue;
                if (sheet.WorthIfHeld(mod) is float value) sum.Add(mod, value);
            }
            return sum.Scale;
        }

        private DamageFlow[] BuildFlows(ActorSheet sheet, PagePlan page, Tags tags, in WeaponProfile weapon)
        {
            if (sheet.Has(Rule.NoOwnDamage) || !page.DealsDamage) return Array.Empty<DamageFlow>();

            DamageTable min = default;
            DamageTable max = default;
            foreach (DamagePart part in page.Damage)
            {
                (float low, float high) = Reading(sheet, part.Measure, weapon);
                min[(int)part.Type] += low * part.Share;
                max[(int)part.Type] += high * part.Share;
            }

            BaseMin = min;
            BaseMax = max;

            for (int i = 0; i < DamageKinds.Count; i++)
            {
                var type = (DamageType)i;
                float added = sheet.Total(M.AddedStat(type), 0f, tags | DamageKinds.TagsOf(type), page);
                Added[i] = added;
                min[i] += added * (1f - Rulebook.AddedSpread);
                max[i] += added * (1f + Rulebook.AddedSpread);
            }

            var flows = new List<DamageFlow>(8);
            var converts = new float[DamageKinds.Count * DamageKinds.Count];
            var gains = new float[DamageKinds.Count * DamageKinds.Count];
            ReadShifts(sheet, page, tags, Stat.Convert, converts);
            ReadShifts(sheet, page, tags, Stat.GainAs, gains);

            for (int i = 0; i < DamageKinds.Count; i++)
            {
                if (max[i] <= 0f) continue;
                Follow(flows, converts, gains, i, 1 << i, min[i], max[i]);
            }
            return flows.ToArray();
        }

        private static void ReadShifts(ActorSheet sheet, PagePlan page, Tags tags, Stat stat, float[] table)
        {
            foreach (ref readonly Mod mod in sheet.Loadout.On(stat)) Read(sheet, mod, tags, table);
            foreach (ref readonly Mod mod in page.Mods.AsSpan())
            {
                if (mod.Stat == stat && mod.Kind == ModKind.Flat) Read(sheet, mod, tags, table);
            }
        }

        private static void Read(ActorSheet sheet, in Mod mod, Tags tags, float[] table)
        {
            if (mod.From == mod.To) return;
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

        private (FoeBonus[], Condition) BuildFoeBonuses(ActorSheet sheet, PagePlan page, Tags tags)
        {
            List<FoeBonus>? bonuses = null;
            Condition asked = Condition.None;

            void Gather(Stat stat, ReadOnlySpan<Mod> mods)
            {
                foreach (ref readonly Mod mod in mods)
                {
                    if (mod.Stat != stat || mod.Foe == Condition.None || mod.Kind > ModKind.More) continue;
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
                Gather(stat, sheet.Loadout.On(stat));
                Gather(stat, page.Mods);
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

        /// <summary>How many times something standing in it the whole time is reached.</summary>
        public int Pulses => Page.Lingers ? 1 + (int)((MathF.Max(Duration, Rulebook.ShortestZone) - Rulebook.FirstPulse) / MathF.Max(Interval, 0.01f)) : 1;

        /// <summary>What each of those comes to, of what the page says: what lingers does what it says over its whole length, for a premium.</summary>
        public float PulseShare => Page.Lingers ? Rulebook.LingerPremium / Pulses : 1f;

        /// <summary>What a share of a blow comes to as it leaves, before any critical hit.</summary>
        public static float Scale(in DamageFlow flow) => MathF.Max(1f + flow.Increased / 100f, 0f) * MathF.Max(flow.More, 0f);
    }
}

using Dino_Engine.World;

namespace Dino_Defenders.Stats
{
    /// <summary>What can be left on someone by a hit. Each type of damage has one, and physical has two.</summary>
    public enum Ailment : byte
    {
        Burn,       // Fire: damage over time. The strongest one stands.
        Bleed,      // Physical: damage over time, worse while the bleeder moves.
        Venom,      // Poison: damage over time, in stacks.
        Chill,      // Cold: everything they do is slower.
        Freeze,     // Cold: they do nothing at all.
        Shock,      // Lightning: they take more damage.
        Hex,        // Magic: their resistances come apart.
        Stun,       // Physical: a blow that stops them for a moment.
    }

    /// <summary>The numbers behind ailments, which hold for everyone. What a build changes is how often and how hard.</summary>
    public static class AilmentRules
    {
        public const int Count = 8;

        public const float BurnSeconds = 4f;
        public const float BurnShare = 0.9f;        // Of the fire damage of the hit, over its whole length.
        public const float BleedSeconds = 5f;
        public const float BleedShare = 0.7f;       // Of its physical damage.
        public const float VenomSeconds = 2.5f;
        public const float VenomShare = 0.3f;       // Of its physical and poison damage, per stack.
        public const int MostVenom = 10;
        public const int MostEndlessVenom = 64;

        public const float ChillSeconds = 2f;
        public const float MostChill = 0.5f;        // Off action speed.
        public const float LeastChill = 0.1f;
        public const float ShockSeconds = 3f;
        public const float MostShock = 0.5f;        // Onto damage taken.
        public const float LeastShock = 0.05f;
        public const float FreezeSeconds = 1.2f;
        public const float FreezeThreshold = 0.25f; // Share of maximum health a hit has to deal to freeze outright.
        public const float HexSeconds = 6f;
        public const float HexPerStack = 6f;        // Percentage points off every resistance.
        public const int MostHex = 5;
        public const float StunSeconds = 0.35f;
        public const float StunThreshold = 0.2f;    // Share of what the target can bear, in one blow.

        public static Tags Tag(Ailment ailment) => ailment switch
        {
            Ailment.Burn => Tags.Burn,
            Ailment.Bleed => Tags.Bleed,
            Ailment.Venom => Tags.Venom,
            Ailment.Chill => Tags.Chill,
            Ailment.Freeze => Tags.Freeze,
            Ailment.Shock => Tags.Shock,
            Ailment.Hex => Tags.Hex,
            _ => Tags.Stun,
        };

        public static Condition ConditionOf(Ailment ailment) => ailment switch
        {
            Ailment.Burn => Condition.Burning,
            Ailment.Bleed => Condition.Bleeding,
            Ailment.Venom => Condition.Envenomed,
            Ailment.Chill => Condition.Chilled,
            Ailment.Freeze => Condition.Frozen,
            Ailment.Shock => Condition.Shocked,
            Ailment.Hex => Condition.Hexed,
            _ => Condition.Stunned,
        };

        public static DamageType TypeOf(Ailment ailment) => ailment switch
        {
            Ailment.Burn => DamageType.Fire,
            Ailment.Venom => DamageType.Poison,
            Ailment.Chill or Ailment.Freeze => DamageType.Cold,
            Ailment.Shock => DamageType.Lightning,
            Ailment.Hex => DamageType.Magic,
            _ => DamageType.Physical,
        };
    }

    /// <summary>What someone has left. A hero's and an enemy's are the same thing.</summary>
    public struct Vitals
    {
        public float Health;
        public float Mana;
        public float Barrier;

        /// <summary>Damage owed, for those who put off paying it.</summary>
        public float Debt;

        public static Vitals Full(ActorSheet sheet) => new Vitals { Health = sheet.MaxHealth, Mana = sheet.MaxMana, Barrier = sheet.MaxBarrier };
    }

    /// <summary>A blow on its way. Nothing in here is on the heap: a screenful of these costs the collector nothing.</summary>
    public struct HitPacket
    {
        public DamageTable Damage;      // As it left the attacker.
        public DamageTable Dealt;       // As it got through.
        public DamageTable Penetration;
        public Tags Tags;
        public float ArmourIgnore;
        public float Total;
        public bool Critical;
    }

    /// <summary>
    /// How a blow goes, from either side. This is the one place damage is worked out, for everyone, and it reads only what sheets and profiles
    /// have finished. It allocates nothing and asks nothing of the world, which also means it can be tested on its own.
    ///
    /// Going out: roll the base, add what counts against this target, crit.
    /// Coming in: what arrives as another type does so, then resistance, armour, flat reduction, then whatever scales damage taken, and it's paid
    /// from barrier, then mana, then health. Armour reads what got past resistance, as it's the size of the blow that it fights.
    /// </summary>
    public static class Hits
    {
        /// <summary>Armour against a blow of its own size stops a sixth of it; five times that size is what stops half.</summary>
        public const float ArmourFactor = 5f;
        public const float MostReduction = 0.9f;
        public const float LeastResistance = -200f;
        public const float MostEvasion = 0.85f;
        public const float EvasionFactor = 3f;
        public const float DebtSeconds = 4f;
        public const float LowShare = 0.35f;

        public static void Roll(SkillProfile skill, Condition foe, WorldRandom luck, ref HitPacket hit, float strength = 1f, bool forceCrit = false)
        {
            hit.Damage = default;
            hit.Dealt = default;
            hit.Penetration = skill.Penetration;
            hit.Tags = skill.Tags | Tags.Hit;
            hit.ArmourIgnore = skill.ArmourIgnore;
            hit.Total = 0f;

            float crit = skill.CritChance;
            float multiplier = skill.CritMultiplier;
            FoeBonus[] bonuses = skill.FoeBonuses;

            // Crit is rolled whatever this skill's flows are like, as it applies to the whole of the blow.
            if (bonuses.Length > 0 && (skill.FoeConditions & foe) != 0)
            {
                crit = Bonus(bonuses, Stat.CritChance, foe, crit);
                multiplier = Bonus(bonuses, Stat.CritMultiplier, foe, multiplier);
            }
            hit.Critical = forceCrit || (crit > 0f && luck.Float() * 100f < crit);

            float roll = luck.Float();
            if (skill.Sheet.Has(Rule.LuckyHits)) roll = MathF.Max(roll, luck.Float());

            ReadOnlySpan<DamageFlow> flows = skill.Flows;
            bool asked = bonuses.Length > 0 && (skill.FoeConditions & foe) != 0;
            for (int i = 0; i < flows.Length; i++)
            {
                ref readonly DamageFlow flow = ref flows[i];
                float increased = flow.Increased;
                float more = flow.More;

                if (asked)
                {
                    for (int b = 0; b < bonuses.Length; b++)
                    {
                        ref readonly FoeBonus bonus = ref bonuses[b];
                        if (bonus.Stat != Stat.Damage || (bonus.Foe & ~foe) != 0 || (bonus.Flows & (1u << i)) == 0) continue;
                        if (bonus.Kind == ModKind.More) more *= 1f + bonus.Value / 100f;
                        else if (bonus.Kind == ModKind.Increased) increased += bonus.Value;
                    }
                }

                float amount = (flow.Min + (flow.Max - flow.Min) * roll) * MathF.Max(1f + increased / 100f, 0f) * MathF.Max(more, 0f);
                hit.Damage[(int)flow.Type] += amount * strength;
            }

            if (!hit.Critical) return;
            float scale = multiplier / 100f;
            for (int i = 0; i < DamageKinds.Count; i++) hit.Damage[i] *= scale;
        }

        // Flat, increased and more of one stat at once, on top of a total: for the few that only count against certain foes.
        private static float Bonus(FoeBonus[] bonuses, Stat stat, Condition foe, float start)
        {
            float flat = 0f;
            float increased = 0f;
            float more = 1f;
            for (int b = 0; b < bonuses.Length; b++)
            {
                ref readonly FoeBonus bonus = ref bonuses[b];
                if (bonus.Stat != stat || (bonus.Foe & ~foe) != 0) continue;
                if (bonus.Kind == ModKind.Flat) flat += bonus.Value;
                else if (bonus.Kind == ModKind.Increased) increased += bonus.Value;
                else more *= 1f + bonus.Value / 100f;
            }
            return (start + flat) * MathF.Max(1f + increased / 100f, 0f) * more;
        }

        /// <summary>The chance of an attack missing, for someone this evasive against someone this accurate.</summary>
        public static float EvadeChance(float evasion, float accuracy)
        {
            if (evasion <= 0f) return 0f;
            return MathF.Min(evasion / (evasion + EvasionFactor * accuracy), MostEvasion);
        }

        public static float ArmourReduction(float armour, float hit)
        {
            if (armour <= 0f || hit <= 0f) return 0f;
            return armour / (armour + ArmourFactor * hit);
        }

        /// <summary>
        /// What a blow comes to against a target, by type, into <see cref="HitPacket.Dealt"/>.
        /// <paramref name="resistanceLost"/> and <paramref name="takenIncrease"/> are what ailments on the target are doing to it.
        /// </summary>
        public static float Mitigate(ActorSheet target, ref HitPacket hit, Condition attacker = Condition.None, float resistanceLost = 0f, float takenIncrease = 0f)
        {
            if (target.TakenAs is float[] shift) Shift(shift, ref hit);

            float total = 0f;
            ScopedDefence[] scoped = target.ScopedTaken;
            for (int i = 0; i < DamageKinds.Count; i++)
            {
                float amount = hit.Damage[i];
                if (amount <= 0f)
                {
                    hit.Dealt[i] = 0f;
                    continue;
                }

                float resistance = target.Resistance[i] - resistanceLost;
                if (resistance > 0f) resistance = MathF.Max(resistance - hit.Penetration[i], 0f);
                float through = amount * (1f - MathF.Max(resistance, LeastResistance) / 100f);

                float reduction = i == (int)DamageType.Physical ? target.PhysicalReduction / 100f : 0f;
                float share = target.ArmourApplies[i] / 100f;
                if (share > 0f) reduction += ArmourReduction(target.Armour * share * (1f - hit.ArmourIgnore / 100f), through);
                through *= 1f - MathF.Min(MathF.Max(reduction, 0f), MostReduction);

                through = MathF.Max(through - target.FlatReduction[i], 0f);

                float scale = target.Taken[i];
                if (scoped.Length > 0) scale = ScaleBy(scoped, hit.Tags | DamageKinds.TagsOf((DamageType)i), attacker, scale);
                through *= scale * (1f + takenIncrease);

                hit.Dealt[i] = through;
                total += through;
            }

            if (target.FlatReductionAll > 0f && total > 0f)
            {
                float kept = MathF.Max(total - target.FlatReductionAll, 0f) / total;
                for (int i = 0; i < DamageKinds.Count; i++) hit.Dealt[i] *= kept;
                total *= kept;
            }

            hit.Total = total;
            return total;
        }

        private static void Shift(float[] shift, ref HitPacket hit)
        {
            DamageTable moved = default;
            for (int from = 0; from < DamageKinds.Count; from++)
            {
                float amount = hit.Damage[from];
                if (amount <= 0f) continue;

                float left = 1f;
                for (int to = 0; to < DamageKinds.Count; to++)
                {
                    float share = shift[from * DamageKinds.Count + to];
                    if (share <= 0f) continue;
                    moved[to] += amount * share;
                    left -= share;
                }
                moved[from] += amount * MathF.Max(left, 0f);
            }
            hit.Damage = moved;
        }

        private static float ScaleBy(ScopedDefence[] scoped, Tags tags, Condition attacker, float scale)
        {
            float increased = 0f;
            float more = 1f;
            for (int i = 0; i < scoped.Length; i++)
            {
                ref readonly ScopedDefence defence = ref scoped[i];
                if ((defence.Scope & ~tags) != 0 || (defence.Foe & ~attacker) != 0) continue;
                if (defence.Kind == ModKind.More) more *= 1f + defence.Value / 100f;
                else if (defence.Kind == ModKind.Increased) increased += defence.Value;
            }
            return scale * MathF.Max(1f + increased / 100f, 0f) * MathF.Max(more, 0f);
        }

        /// <summary>
        /// Takes what got through out of what someone has: barrier first, then their share from mana, then health. Poison seeps past barrier. Returns
        /// what health lost.
        /// </summary>
        public static float Pay(ActorSheet target, ref Vitals vitals, in HitPacket hit)
        {
            return Pay(target, ref vitals, hit.Total, hit.Dealt[(int)DamageType.Poison]);
        }

        public static float Pay(ActorSheet target, ref Vitals vitals, float total, float pastBarrier)
        {
            if (target.Has(Rule.DeferredDamage))
            {
                vitals.Debt += total;
                return 0f;
            }

            float owed = total;
            if (vitals.Barrier > 0f)
            {
                float taken = MathF.Min(vitals.Barrier, MathF.Max(total - pastBarrier, 0f));
                vitals.Barrier -= taken;
                owed -= taken;
            }

            if (target.TakenFromMana > 0f && vitals.Mana > 0f)
            {
                float taken = MathF.Min(vitals.Mana, owed * target.TakenFromMana / 100f);
                vitals.Mana -= taken;
                owed -= taken;
            }

            vitals.Health -= owed;
            return owed;
        }

        /// <summary>Gives health back. Returns what was actually restored; any past full goes to barrier, for those who've arranged that.</summary>
        public static float Heal(ActorSheet sheet, ref Vitals vitals, float amount)
        {
            if (amount <= 0f || vitals.Health <= 0f) return 0f;

            float room = sheet.MaxHealth - vitals.Health;
            float healed = MathF.Min(room, amount);
            vitals.Health += healed;

            if (sheet.Has(Rule.OverhealBarrier) && amount > room)
            {
                vitals.Barrier = MathF.Min(vitals.Barrier + amount - room, sheet.MaxBarrier + sheet.MaxHealth * 0.5f);
            }
            return healed;
        }

        /// <summary>A moment passing for someone: their health and barrier coming back, and what they owe falling due. Mana is apart, as a hero's step has to predict it.</summary>
        public static void Recover(ActorSheet sheet, ref Vitals vitals, float seconds, float sinceHit)
        {
            if (vitals.Health <= 0f) return;

            if (vitals.Debt > 0f)
            {
                float due = MathF.Min(vitals.Debt, MathF.Max(vitals.Debt / DebtSeconds, sheet.MaxHealth * 0.02f) * seconds);
                vitals.Debt -= due;
                vitals.Health -= due;
                if (vitals.Health <= 0f) return;
            }

            if (vitals.Health < sheet.MaxHealth) vitals.Health = MathF.Min(vitals.Health + sheet.HealthRegen * seconds, sheet.MaxHealth);
            if (sinceHit >= sheet.BarrierDelay && vitals.Barrier < sheet.MaxBarrier)
            {
                vitals.Barrier = MathF.Min(vitals.Barrier + sheet.BarrierRecharge * seconds, sheet.MaxBarrier);
            }
        }

        /// <summary>What's true of someone's pools, which is where a good part of their state comes from.</summary>
        public static Condition PoolState(ActorSheet sheet, in Vitals vitals)
        {
            Condition state = Condition.None;
            if (vitals.Health >= sheet.MaxHealth - 0.01f) state |= Condition.FullHealth;
            else if (vitals.Health <= sheet.MaxHealth * LowShare) state |= Condition.LowHealth;
            if (vitals.Mana >= sheet.MaxMana - 0.01f) state |= Condition.FullMana;
            else if (vitals.Mana <= sheet.MaxMana * LowShare) state |= Condition.LowMana;
            state |= vitals.Barrier > 0f ? Condition.HasBarrier : Condition.NoBarrier;
            return state;
        }
    }
}

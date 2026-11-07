using Dino_Defenders.Books;

namespace Dino_Defenders.Stats
{
    /// <summary>What a set of modifiers on a stat comes to.</summary>
    public struct StatSum
    {
        public float Flat;
        public float Increased;
        public float More;
        public float Set;

        public static StatSum Empty => new StatSum { More = 1f, Set = float.PositiveInfinity };

        public readonly bool Overridden => !float.IsPositiveInfinity(Set);

        /// <summary>The rule all stats follow: (base + flat) x (1 + all increases) x each more on its own.</summary>
        public readonly float Apply(float start)
        {
            if (Overridden) return Set;
            return (start + Flat) * Scale;
        }

        public readonly float Scale => MathF.Max(1f + Increased / 100f, 0f) * MathF.Max(More, 0f);

        public void Add(in Mod mod, float value)
        {
            switch (mod.Kind)
            {
                case ModKind.Flat:
                    Flat += value;
                    break;
                case ModKind.Increased:
                    Increased += value;
                    break;
                case ModKind.More:
                    More *= 1f + value / 100f;
                    break;
                case ModKind.Override:
                    Set = MathF.Min(Set, value);
                    break;
            }
        }
    }

    /// <summary>A defence that depends on what's coming: "less damage from projectiles", or from those on fire.</summary>
    public readonly record struct ScopedDefence(Tags Scope, Condition Foe, ModKind Kind, float Value);

    /// <summary>
    /// What an actor comes to, as of one state of things. It's finished numbers and nothing else: everything that happens in a fight reads these
    /// and adds nothing up. A sheet never changes once made; when something does, the loadout hands out another.
    /// </summary>
    public sealed class ActorSheet
    {
        public const float BaseCritMultiplier = 150f;
        public const float BaseMaxResistance = 75f;
        public const float BaseMaxBlock = 75f;
        public const float BaseBarrierRecharge = 20f;
        public const float BaseBarrierDelay = 3f;
        public const float BaseDodgeCooldown = 2.4f;
        public const float BaseDodgeSpeed = 34f;
        public const float BaseStunThreshold = 100f;

        private readonly BookProfile?[] _profiles;
        private readonly float[] _per = new float[(int)Counting.Last + 1];
        private readonly RuleSet _rules;
        private bool _perReady;

        public readonly Loadout Loadout;
        public readonly Condition State;
        public readonly Counters Counters;

        public readonly float MaxHealth;
        public readonly float MaxMana;
        public readonly float MaxBarrier;

        public readonly float HealthRegen;
        public readonly float ManaRegen;

        /// <summary>Percent of maximum health lost a second, whatever else is going on.</summary>
        public readonly float HealthDrain;

        /// <summary>What a build adds to the mana everyone gets back.</summary>
        public readonly float ManaRegenBonus;
        public readonly float BarrierRecharge;
        public readonly float BarrierDelay;
        public readonly float RecoveryScale;
        public readonly float FlaskScale;

        public readonly float MoveSpeed;
        public readonly float DodgeCooldown;
        public readonly float DodgeSpeed;

        public readonly float Armour;
        public readonly float PhysicalReduction;
        public readonly float Evasion;
        public readonly float BlockAttacks;
        public readonly float BlockSpells;
        public readonly float TakenFromMana;
        public readonly float Reflect;
        public readonly float StunThreshold;
        public readonly float FlatReductionAll;

        public DamageTable Resistance;
        public DamageTable MaxResistance;
        public DamageTable ArmourApplies;
        public DamageTable FlatReduction;
        public DamageTable Taken;

        /// <summary>Shares of each type arriving as another, from*6+to, or null if everything arrives as it is.</summary>
        public readonly float[]? TakenAs;

        public readonly ScopedDefence[] ScopedTaken;

        /// <summary>Each ailment's: chance of shaking it off, and how long it lasts here as a multiple of normal.</summary>
        public readonly float[] AilmentAvoid = new float[AilmentRules.Count];
        public readonly float[] AilmentTaken = new float[AilmentRules.Count];

        public readonly float ItemRarity;
        public readonly float GoldFind;
        public readonly float ExperienceGain;
        public readonly float Stealth;
        public readonly float MinionDuration;
        public readonly float RaiseFallen;
        public readonly float ExplodeOnKill;

        public bool Has(Rule rule) => _rules.Has(rule);

        internal ActorSheet(Loadout loadout, Condition state, in Counters counters)
        {
            Loadout = loadout;
            State = state;
            Counters = counters;
            _profiles = new BookProfile?[loadout.Books.Length];

            foreach (Mod mod in loadout.RuleMods)
            {
                if (Holds(mod)) _rules.Add(mod.Rule);
            }

            // Anything measured against a total is measured against what it was before such things were counted. So one can't feed another,
            // and it doesn't matter what order they were picked up in or whether two of them point at each other.
            _per[(int)Per.Armour] = Total(Stat.Armour);
            _per[(int)Per.Evasion] = Total(Stat.Evasion);
            _per[(int)Per.MaxHealth] = Total(Stat.Health);
            _per[(int)Per.MaxMana] = Total(Stat.Mana);
            _per[(int)Per.MaxBarrier] = Total(Stat.Barrier);
            StatSum speed = Collect(Stat.MoveSpeed, Tags.None);
            _per[(int)Per.MoveSpeedAboveBase] = MathF.Max(speed.Scale - 1f, 0f) * 100f;
            _per[(int)Per.Level] = loadout.Level;
            _per[(int)Per.Strength] = Total(Stat.Strength);
            _per[(int)Per.Dexterity] = Total(Stat.Dexterity);
            _per[(int)Per.Intellect] = Total(Stat.Intellect);
            for (Per per = Per.Gold; per <= Counting.Last; per++) _per[(int)per] = counters.Read(per);
            _perReady = true;

            MaxHealth = MathF.Max(Total(Stat.Health), 1f);
            MaxMana = MathF.Max(Total(Stat.Mana), 0f);
            MaxBarrier = MathF.Max(Total(Stat.Barrier), 0f);

            RecoveryScale = Total(Stat.Recovery, 1f);
            FlaskScale = Total(Stat.FlaskEffect, 1f);
            float healthRegen = Total(Stat.HealthRegen) + MaxHealth * Total(Stat.HealthRegenPercent) / 100f;
            HealthRegen = Has(Rule.NoRegen) ? 0f : healthRegen * RecoveryScale;
            HealthDrain = MathF.Max(Total(Stat.HealthDrain), 0f);
            ManaRegenBonus = (Total(Stat.ManaRegen) + MaxMana * Total(Stat.ManaRegenPercent) / 100f) * RecoveryScale;
            ManaRegen = ManaRegenBonus + MaxMana * Rulebook.ManaRefill / 100f * RecoveryScale;
            BarrierRecharge = MaxBarrier * Total(Stat.BarrierRecharge, BaseBarrierRecharge) / 100f * RecoveryScale;
            BarrierDelay = MathF.Max(Total(Stat.BarrierDelay, BaseBarrierDelay), 0f);

            MoveSpeed = MathF.Max(Total(Stat.MoveSpeed), Rulebook.LeastMoveSpeed);
            DodgeCooldown = MathF.Max(Total(Stat.DodgeCooldown, BaseDodgeCooldown), Rulebook.LeastDodgeCooldown);
            DodgeSpeed = Total(Stat.DodgeSpeed, BaseDodgeSpeed);

            Armour = MathF.Max(Total(Stat.Armour), 0f);
            PhysicalReduction = Total(Stat.PhysicalReduction);
            Evasion = Has(Rule.CannotEvade) ? 0f : MathF.Max(Total(Stat.Evasion), 0f);
            float maxBlock = MathF.Min(Total(Stat.MaxBlock, BaseMaxBlock), Rulebook.MostBlock);
            BlockAttacks = Math.Clamp(Total(Stat.BlockChance, 0f, Tags.Attack), 0f, maxBlock);
            BlockSpells = Math.Clamp(Total(Stat.BlockChance, 0f, Tags.Spell), 0f, maxBlock);
            TakenFromMana = Math.Clamp(Total(Stat.TakenFromMana), 0f, 100f);
            Reflect = MathF.Max(Total(Stat.Reflect), 0f);
            StunThreshold = MathF.Max(Total(Stat.StunThreshold, BaseStunThreshold), 1f);

            for (int i = 0; i < DamageKinds.Count; i++)
            {
                var type = (DamageType)i;
                Tags tags = DamageKinds.TagsOf(type);
                MaxResistance[i] = MathF.Min(Total(Stat.MaxResistance, BaseMaxResistance, tags), Rulebook.MostResistance);
                Resistance[i] = type == DamageType.Physical ? 0f : MathF.Min(Total(Stat.Resistance, 0f, tags), MaxResistance[i]);
                ArmourApplies[i] = (type == DamageType.Physical ? 100f : 0f) + Total(Stat.ArmourApplies, 0f, tags);
                FlatReduction[i] = TotalNaming(Stat.FlatReduction, tags);
                Taken[i] = TakenScale(tags);
            }
            if (Has(Rule.EvenResist)) EvenOut();
            FlatReductionAll = TotalNotNaming(Stat.FlatReduction, Tags.DamageTypes);
            TakenAs = ShiftTable();
            ScopedTaken = ScopedDefences();

            for (int i = 0; i < AilmentRules.Count; i++)
            {
                Tags tag = AilmentRules.Tag((Ailment)i);
                AilmentAvoid[i] = Math.Clamp(Total(Stat.AilmentAvoid, 0f, tag), 0f, 100f);
                AilmentTaken[i] = MathF.Max(Total(Stat.AilmentTaken, 1f, tag), 0f);
            }

            ItemRarity = Bonus(Stat.ItemRarity);
            GoldFind = Bonus(Stat.GoldFind);
            ExperienceGain = Bonus(Stat.ExperienceGain);
            Stealth = Math.Clamp(Total(Stat.Stealth), -100f, Rulebook.MostStealth);
            MinionDuration = Total(Stat.MinionDuration, 1f);
            RaiseFallen = Math.Clamp(Total(Stat.RaiseFallen), 0f, 100f);
            ExplodeOnKill = MathF.Max(Total(Stat.ExplodeOnKill), 0f);
        }

        private bool Holds(in Mod mod) => (mod.Need & ~(State | Loadout.Granted)) == 0 && (mod.Without & (State | Loadout.Granted)) == 0;

        // What a modifier is worth here: its value, times however many of whatever it's counted per. Null if it doesn't count yet.
        private float? Worth(in Mod mod)
        {
            if (mod.Per == Per.None) return mod.Value;
            if (!_perReady && mod.Per < Per.Gold) return null;

            float steps = MathF.Floor(_per[(int)mod.Per] / MathF.Max(mod.PerStep, 1e-3f));
            float value = mod.Value * steps;
            return mod.PerCap > 0f ? MathF.CopySign(MathF.Min(MathF.Abs(value), mod.PerCap), value) : value;
        }

        /// <summary>What one modifier comes to here, or null if what it asks for doesn't hold.</summary>
        public float? WorthIfHeld(in Mod mod) => Holds(mod) ? Worth(mod) : null;

        /// <summary>Adds up what applies to something with these <paramref name="tags"/>, ignoring what only counts against particular foes.</summary>
        public StatSum Collect(Stat stat, Tags tags, PagePlan? page = null)
        {
            StatSum sum = StatSum.Empty;
            foreach (ref readonly Mod mod in Loadout.On(stat))
            {
                if (mod.Foe != Condition.None || (mod.Scope & ~tags) != 0 || !Holds(mod)) continue;
                if (Worth(mod) is float value) sum.Add(mod, value);
            }

            if (page == null) return sum;
            foreach (ref readonly Mod mod in page.Mods.AsSpan())
            {
                if (mod.Stat != stat || mod.Kind > ModKind.Override || mod.Foe != Condition.None || (mod.Scope & ~tags) != 0 || !Holds(mod)) continue;
                if (Worth(mod) is float value) sum.Add(mod, value);
            }
            return sum;
        }

        /// <summary>What a stat comes to for something with these tags.</summary>
        public float Total(Stat stat, float start = 0f, Tags tags = Tags.None, PagePlan? page = null) => Collect(stat, tags, page).Apply(start);

        // Some stats are nothing but a percentage on top of something else, so there's no base for an increase to multiply.
        private float Bonus(Stat stat)
        {
            StatSum sum = Collect(stat, Tags.None);
            return sum.Flat + sum.Increased;
        }

        private float TotalNaming(Stat stat, Tags tags)
        {
            float total = 0f;
            foreach (ref readonly Mod mod in Loadout.On(stat))
            {
                if ((mod.Scope & Tags.DamageTypes) == 0 || mod.Foe != Condition.None || (mod.Scope & ~tags) != 0 || !Holds(mod)) continue;
                if (Worth(mod) is float value) total += value;
            }
            return total;
        }

        private float TotalNotNaming(Stat stat, Tags tags)
        {
            float total = 0f;
            foreach (ref readonly Mod mod in Loadout.On(stat))
            {
                if ((mod.Scope & tags) != 0 || mod.Foe != Condition.None || !Holds(mod)) continue;
                if (Worth(mod) is float value) total += value;
            }
            return total;
        }

        private float TakenScale(Tags tags)
        {
            StatSum sum = StatSum.Empty;
            foreach (ref readonly Mod mod in Loadout.On(Stat.DamageTaken))
            {
                // What depends on how a blow arrives is worked out when it does.
                if ((mod.Scope & ~tags) != 0 || mod.Foe != Condition.None || !Holds(mod)) continue;
                if (Worth(mod) is float value) sum.Add(mod, value);
            }
            return sum.Scale;
        }

        private ScopedDefence[] ScopedDefences()
        {
            List<ScopedDefence>? list = null;
            foreach (ref readonly Mod mod in Loadout.On(Stat.DamageTaken))
            {
                bool named = (mod.Scope & ~(Tags.DamageTypes | Tags.Elemental)) != 0 || mod.Foe != Condition.None;
                if (!named || !Holds(mod)) continue;
                if (Worth(mod) is float value) (list ??= new()).Add(new ScopedDefence(mod.Scope, mod.Foe, mod.Kind, value));
            }
            return list?.ToArray() ?? Array.Empty<ScopedDefence>();
        }

        // Fire, cold and lightning, each as good as the best of them, as far as each is allowed to go.
        private void EvenOut()
        {
            float best = MathF.Max(Resistance[(int)DamageType.Fire], MathF.Max(Resistance[(int)DamageType.Cold], Resistance[(int)DamageType.Lightning]));
            foreach (DamageType type in new[] { DamageType.Fire, DamageType.Cold, DamageType.Lightning }) Resistance[(int)type] = MathF.Min(best, MaxResistance[(int)type]);
        }

        private float[]? ShiftTable()
        {
            float[]? table = null;
            if (Has(Rule.AllAsFire))
            {
                // Everything is fire by the time it arrives, and nothing else can move it on.
                table = new float[DamageKinds.Count * DamageKinds.Count];
                for (int from = 0; from < DamageKinds.Count; from++)
                {
                    if (from != (int)DamageType.Fire) table[from * DamageKinds.Count + (int)DamageType.Fire] = 1f;
                }
                return table;
            }

            foreach (ref readonly Mod mod in Loadout.On(Stat.TakenAs))
            {
                if (!Holds(mod) || mod.From == mod.To || Worth(mod) is not float value || value <= 0f) continue;
                table ??= new float[DamageKinds.Count * DamageKinds.Count];
                table[(int)mod.From * DamageKinds.Count + (int)mod.To] += value / 100f;
            }
            if (table == null) return null;

            // No more can arrive as something else than arrived at all.
            for (int from = 0; from < DamageKinds.Count; from++)
            {
                float total = 0f;
                for (int to = 0; to < DamageKinds.Count; to++) total += table[from * DamageKinds.Count + to];
                if (total <= 1f) continue;
                for (int to = 0; to < DamageKinds.Count; to++) table[from * DamageKinds.Count + to] /= total;
            }
            return table;
        }

        /// <summary>This actor's own numbers for a book, by its place in the loadout. Worked out the first time it's asked for.</summary>
        public BookProfile Profile(int index) => _profiles[index] ??= new BookProfile(this, Loadout.Books[index]);

        public BookProfile? Profile(BookPlan book)
        {
            int index = Loadout.IndexOf(book);
            return index < 0 ? null : Profile(index);
        }

        /// <summary>What a book would come to in this actor's hands, on the bar or not: what's being written, or what an item would set off. Nothing that happens in a fight asks this.</summary>
        public BookProfile Preview(BookPlan book) => Profile(book) ?? new BookProfile(this, book);
    }
}

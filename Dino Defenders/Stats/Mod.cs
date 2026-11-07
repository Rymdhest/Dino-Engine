using Dino_Defenders.Books;
using Dino_Defenders.Tooltips;

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

        /// <summary>What it sets off, as a place on the <see cref="Shelf"/>.</summary>
        public ushort Book { get; init; }

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

        /// <summary>For triggers that hand out a buff rather than set off a book: which, as a place in <see cref="BuffCatalog"/>.</summary>
        public ushort Gift { get; init; }

        /// <summary>For those: whether it takes all of the buff away instead.</summary>
        public bool Spends { get; init; }

        public Mod While(Condition condition) => this with { Need = Need | condition };
        public Mod Unless(Condition condition) => this with { Without = Without | condition };
        public Mod Against(Condition condition) => this with { Foe = Foe | condition };
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

        /// <summary>When something happens, a written book goes off: <paramref name="chance"/> percent of the time.</summary>
        public static Mod Trigger(TriggerEvent on, BookPlan book, float chance = 100f)
        {
            return new Mod { Kind = ModKind.Trigger, On = on, Book = book.Number, Value = chance };
        }

        /// <summary>When something happens, a stack of a buff: what a unique counts up with, as a buff is something a player can see.</summary>
        public static Mod Gain(TriggerEvent on, BuffDefinition buff, float chance = 100f)
        {
            return new Mod { Kind = ModKind.Trigger, On = on, Gift = buff.Number, Value = chance };
        }

        /// <summary>When something happens, all of a buff goes.</summary>
        public static Mod Lose(TriggerEvent on, BuffDefinition buff) => Gain(on, buff) with { Spends = true };

        /// <summary>Every so many seconds, a written book goes off, for as long as what it asks for holds. Waiting starts over whenever it doesn't.</summary>
        public static Mod Every(float seconds, BookPlan book) => Trigger(TriggerEvent.Time, book) with { PerStep = seconds };

        public static Mod EveryGain(float seconds, BuffDefinition buff) => Gain(TriggerEvent.Time, buff) with { PerStep = seconds };

        /// <summary>Every so many metres walked, a written book goes off.</summary>
        public static Mod EveryMetres(float metres, BookPlan book) => Trigger(TriggerEvent.Walked, book) with { PerStep = metres };

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
}

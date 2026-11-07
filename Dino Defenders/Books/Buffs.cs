using Dino_Defenders.Stats;
using static Dino_Defenders.Stats.M;

namespace Dino_Defenders.Books
{
    /// <summary>Something that changes its bearer for a while. A list of modifiers with a clock on it.</summary>
    public sealed class BuffDefinition
    {
        public ushort Number;
        public string Id = "";
        public string Name = "";
        public Mod[] Mods = Array.Empty<Mod>();
        public float Duration = 6f;
        public int MostStacks = 1;
    }

    /// <summary>Lasting effects pages can put on someone. Modifiers with a clock on them, so a new one is a few lines and nothing else.</summary>
    public static class BuffCatalog
    {
        private static readonly List<BuffDefinition> Everything = new();

        public static IReadOnlyList<BuffDefinition> All => Everything;

        public static readonly BuffDefinition StoneSkin = Add(new BuffDefinition
        {
            Id = "stone-skin",
            Name = "Stone Skin",
            Duration = 8f,
            Mods = new[] { More(Stat.Armour, 80f), Flat(Stat.PhysicalReduction, 10f), Rules(Rule.CannotBeStunned) },
        });

        public static readonly BuffDefinition Quickening = Add(new BuffDefinition
        {
            Id = "quickening",
            Name = "Quickening",
            Duration = 6f,
            Mods = new[] { Inc(Stat.MoveSpeed, 30f), Inc(Stat.AttackSpeed, 25f), Inc(Stat.CastSpeed, 25f) },
        });

        public static readonly BuffDefinition Bloodlust = Add(new BuffDefinition
        {
            Id = "bloodlust",
            Name = "Bloodlust",
            Duration = 4f,
            MostStacks = 5,
            Mods = new[] { Inc(Stat.Damage, 8f), Inc(Stat.AttackSpeed, 3f) },
        });

        public static readonly BuffDefinition Sanctuary = Add(new BuffDefinition
        {
            Id = "sanctuary",
            Name = "Sanctuary",
            Duration = 5f,
            Mods = new[] { Flat(Stat.HealthRegenPercent, 4f), More(Stat.DamageTaken, -15f) },
        });

        public static readonly BuffDefinition Rallied = Add(new BuffDefinition
        {
            Id = "rallied",
            Name = "Rallied",
            Duration = 10f,
            MostStacks = 5,
            Mods = new[] { Inc(Stat.Damage, 12f), Inc(Stat.AttackSpeed, 6f), Inc(Stat.CastSpeed, 6f), Inc(Stat.MoveSpeed, 6f) },
        });

        public static readonly BuffDefinition Afterglow = Add(new BuffDefinition
        {
            Id = "afterglow",
            Name = "Afterglow",
            Duration = 3f,
            Mods = new[] { More(Stat.Damage, 20f), Inc(Stat.MoveSpeed, 15f) },
        });

        // What uniques hand out. A count a player has to keep in their head is one they can't see, so whatever builds up is a buff with stacks.
        public static readonly BuffDefinition Grudge = Add(new BuffDefinition
        {
            Id = "grudge",
            Name = "Grudge",
            Duration = 20f,
            MostStacks = 6,
            Mods = new[] { Inc(Stat.Damage, 40f) },
        });

        public static readonly BuffDefinition Verdict = Add(new BuffDefinition
        {
            Id = "verdict",
            Name = "Verdict",
            Duration = 30f,
            Mods = new[] { More(Stat.Damage, 300f) },
        });

        public static readonly BuffDefinition Aim = Add(new BuffDefinition
        {
            Id = "aim",
            Name = "Aim",
            Duration = 10f,
            MostStacks = 3,
            Mods = new[] { More(Stat.Damage, 40f, Tags.Projectile), Flat(Stat.CritChance, 10f) },
        });

        public static readonly BuffDefinition Riposte = Add(new BuffDefinition
        {
            Id = "riposte",
            Name = "Riposte",
            Duration = 4f,
            Mods = new[] { Flat(Stat.CritChance, 100f), More(Stat.Damage, 100f, Tags.Attack) },
        });

        public static readonly BuffDefinition Stride = Add(new BuffDefinition
        {
            Id = "stride",
            Name = "Long Stride",
            Duration = 2f,
            MostStacks = 10,
            Mods = new[] { Inc(Stat.Damage, 6f), Inc(Stat.MoveSpeed, 3f) },
        });

        public static readonly BuffDefinition Dance = Add(new BuffDefinition
        {
            Id = "dance",
            Name = "Dance",
            Duration = 8f,
            MostStacks = 5,
            Mods = new[] { Inc(Stat.Damage, 15f), Inc(Stat.AttackSpeed, 5f), Inc(Stat.CastSpeed, 5f) },
        });

        public static readonly BuffDefinition Courage = Add(new BuffDefinition
        {
            Id = "courage",
            Name = "Dutch Courage",
            Duration = 8f,
            Mods = new[] { More(Stat.Damage, 40f), Inc(Stat.MoveSpeed, 15f) },
        });

        public static readonly BuffDefinition Mourning = Add(new BuffDefinition
        {
            Id = "mourning",
            Name = "Mourning",
            Duration = 8f,
            MostStacks = 5,
            Mods = new[] { Inc(Stat.Damage, 15f), Inc(Stat.CastSpeed, 5f) },
        });

        public static readonly BuffDefinition Vanish = Add(new BuffDefinition
        {
            Id = "vanish",
            Name = "Unseen",
            Duration = 3f,
            Mods = new[] { Flat(Stat.Stealth, 90f) },
        });

        public static readonly BuffDefinition LastGasp = Add(new BuffDefinition
        {
            Id = "last-gasp",
            Name = "Last Gasp",
            Duration = Rulebook.GaspSeconds,
            Mods = new[] { More(Stat.Damage, 60f) },
        });

        /// <summary>Hardened against one type of damage, by type: what last got through.</summary>
        public static readonly BuffDefinition[] Inured = DamageKinds.All.Select(type => Add(new BuffDefinition
        {
            Id = $"inured-{type.ToString().ToLowerInvariant()}",
            Name = $"Inured to {type}",
            Duration = 6f,
            Mods = new[] { More(Stat.DamageTaken, -50f, DamageKinds.Tag(type)) },
        })).ToArray();

        private static BuffDefinition Add(BuffDefinition buff)
        {
            Everything.Add(buff);
            buff.Number = (ushort)Everything.Count;
            return buff;
        }

        public static BuffDefinition? Get(int number) => number >= 1 && number <= Everything.Count ? Everything[number - 1] : null;
    }
}

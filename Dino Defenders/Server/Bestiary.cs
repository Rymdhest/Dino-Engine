using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Skills;
using Dino_Defenders.Stats;
using Dino_Defenders.Talents;
using Dino_Engine.World;
using static Dino_Defenders.Stats.M;

namespace Dino_Defenders.Server
{
    /// <summary>
    /// What a kind of creature is. It has a level, skills from the same catalogue as anyone, and a list of modifiers, exactly as a hero does: there's
    /// nothing else to an enemy. Everything that makes a bandit a bandit is written here as stats.
    /// </summary>
    public sealed class EnemyKind
    {
        public string Id = "";
        public string Name = "";
        public SkillDefinition[] Skills = Array.Empty<SkillDefinition>();
        public Mod[] Mods = Array.Empty<Mod>();
        public Tags Weapon = Tags.Sword | Tags.OneHanded;
        public float MoveSpeed = 6.8f;
        public float AggroRadius = 21f;
        public float LeashRadius = 60f;
        public bool KeepsAway;      // Would sooner stand off and shoot.
        public float Health = 1f;
        public float Damage = 1f;
    }

    /// <summary>A quality an enemy can have on top of what it is. Elites get one and bosses several. They're the same modifiers a hero's gear is made of.</summary>
    public readonly record struct EnemyTrait(string Name, Mod[] Mods);

    /// <summary>
    /// The creatures of the world, and how strong they are at each level. Enemies of one kind, level and trait share a single loadout and so a single
    /// compiled sheet. That is what makes a horde cheap: three hundred skeletons are one set of numbers.
    /// </summary>
    public sealed class Bestiary
    {
        public const int NoTrait = -1;

        // Measured against sample heroes in BuildLab's balance table, who know nothing of keystones and don't choose their gear. A hero gains most
        // early, as gear comes in tiers, so these climb the same way, and what's exponential in them is what keeps lands past the level cap getting harder.
        // Against these a sample hero needs about three hits and survives about ten. Anything a real build does better than that is the build working.
        public static float HealthAt(int level) => (26f + 0.75f * level * level) * MathF.Pow(1.02f, level);
        public static float DamageAt(int level) => (7f + 1.6f * level + 0.01f * level * level) * MathF.Pow(1.006f, level);
        public static int ExperienceAt(int level, float rankHealth) => (int)(14f * MathF.Pow(1.11f, level) * rankHealth);

        public static readonly EnemyKind Outlaw = new EnemyKind
        {
            Id = "outlaw", Name = "Outlaw", Skills = new[] { SkillCatalog.Strike },
        };

        public static readonly EnemyKind Archer = new EnemyKind
        {
            Id = "archer", Name = "Outlaw Archer", Skills = new[] { SkillCatalog.Shot }, Weapon = Tags.Bow | Tags.TwoHanded,
            KeepsAway = true, AggroRadius = 30f, Damage = 0.75f,
        };

        public static readonly EnemyKind Sentry = new EnemyKind
        {
            Id = "sentry", Name = "Sentry", Skills = new[] { SkillCatalog.Strike }, Mods = new[] { More(Stat.Armour, 50f), Flat(Stat.BlockChance, 15f) },
        };

        public static readonly EnemyKind Restless = new EnemyKind
        {
            Id = "restless", Name = "Restless Dead", Skills = new[] { SkillCatalog.Bite }, MoveSpeed = 5.2f,
            Mods = new[] { Resist(Tags.Poison, 40f), Resist(Tags.Cold, 30f), Resist(Tags.Fire, -20f), Flat(Stat.AilmentAvoid, 100f, Tags.Bleed) },
        };

        public static readonly EnemyKind Firebrand = new EnemyKind
        {
            Id = "firebrand", Name = "Firebrand", Skills = new[] { SkillCatalog.Immolate, SkillCatalog.Firebolt }, KeepsAway = true, AggroRadius = 26f,
            Health = 0.8f, Mods = new[] { Resist(Tags.Fire, 40f) },
        };

        public static readonly EnemyKind Rimewitch = new EnemyKind
        {
            Id = "rimewitch", Name = "Rimewitch", Skills = new[] { SkillCatalog.FrostNova, SkillCatalog.FrostShards }, KeepsAway = true, AggroRadius = 26f,
            Health = 0.8f, Mods = new[] { Resist(Tags.Cold, 40f) },
        };

        public static readonly EnemyKind Stormcaller = new EnemyKind
        {
            Id = "stormcaller", Name = "Stormcaller", Skills = new[] { SkillCatalog.Spark }, KeepsAway = true, AggroRadius = 26f,
            Health = 0.75f, Mods = new[] { Resist(Tags.Lightning, 40f) },
        };

        public static readonly EnemyKind Bogseer = new EnemyKind
        {
            Id = "bogseer", Name = "Bogseer", Skills = new[] { SkillCatalog.Miasma, SkillCatalog.VenomSpit }, KeepsAway = true, AggroRadius = 26f,
            Health = 0.85f, Mods = new[] { Resist(Tags.Poison, 60f) },
        };

        public static readonly EnemyKind Mourner = new EnemyKind
        {
            Id = "mourner", Name = "Hollow Mourner", Skills = new[] { SkillCatalog.Unmaking, SkillCatalog.ArcaneDart }, KeepsAway = true, MoveSpeed = 5f, AggroRadius = 26f,
            Health = 0.85f, Mods = new[] { Resist(Tags.Magic, 40f), Resist(Tags.Fire, -20f) },
        };

        public static readonly EnemyKind Warlord = new EnemyKind
        {
            Id = "warlord", Name = "Warlord", Skills = new[] { SkillCatalog.Whirl, SkillCatalog.Maul, SkillCatalog.Strike }, AggroRadius = 26f, LeashRadius = 70f,
            Mods = new[] { More(Stat.Armour, 100f), Inc(Stat.AilmentTaken, -50f) },
        };

        public static readonly EnemyKind[] Casters = { Firebrand, Rimewitch, Stormcaller, Bogseer };

        // What people raise and whistle for. Minions are enemies on your side, and share everything else with them.
        public static readonly EnemyKind Bones = new EnemyKind
        {
            Id = "bones", Name = "Risen Bones", Skills = new[] { SkillCatalog.Strike }, MoveSpeed = 7f, Health = 0.8f, Damage = 0.8f,
            Mods = new[] { Resist(Tags.Poison, 40f) },
        };

        public static readonly EnemyKind Hound = new EnemyKind
        {
            Id = "hound", Name = "Pack Hound", Skills = new[] { SkillCatalog.Bite }, MoveSpeed = 10f, Health = 0.6f, Damage = 0.7f,
            Mods = new[] { Inc(Stat.AttackSpeed, 30f) },
        };

        public static EnemyKind? Minion(string? id) => id switch
        {
            "bones" => Bones,
            "hound" => Hound,
            _ => null,
        };

        public static readonly EnemyTrait[] Traits =
        {
            new("Burning", new[] { Convert(DamageType.Physical, DamageType.Fire, 100f), Resist(Tags.Fire, 50f), Chance(Tags.Burn, 40f) }),
            new("Frostbound", new[] { Convert(DamageType.Physical, DamageType.Cold, 100f), Resist(Tags.Cold, 50f), Inc(Stat.AilmentEffect, 50f, Tags.Chill) }),
            new("Stormtouched", new[] { Convert(DamageType.Physical, DamageType.Lightning, 100f), Resist(Tags.Lightning, 50f), Chance(Tags.Shock, 40f) }),
            new("Venomous", new[] { GainAs(DamageType.Physical, DamageType.Poison, 50f), Chance(Tags.Venom, 60f), Resist(Tags.Poison, 40f) }),
            new("Vampiric", new[] { Flat(Stat.HealthLeech, 25f), Inc(Stat.Health, 20f) }),
            new("Ironclad", new[] { More(Stat.Armour, 200f), Flat(Stat.PhysicalReduction, 15f), Flat(Stat.BlockChance, 20f) }),
            new("Swift", new[] { Inc(Stat.MoveSpeed, 40f), Inc(Stat.AttackSpeed, 30f), Inc(Stat.CastSpeed, 30f) }),
            new("Thorned", new[] { Flat(Stat.Reflect, 40f), Inc(Stat.Armour, 50f) }),
            new("Warded", new[] { Inc(Stat.Health, -30f), Flat(Stat.BarrierRecharge, 30f) }),
            new("Deadeye", new[] { Flat(Stat.CritChance, 20f), Flat(Stat.CritMultiplier, 50f), Flat(Stat.Accuracy, 200f) }),
            new("Unmaking", new[] { Chance(Tags.Hex, 50f), Added(DamageType.Magic, 6f), Flat(Stat.Penetration, 15f) }),
            new("Fearsome", new[] { More(Stat.Damage, 20f), Inc(Stat.Health, 30f) }),
        };

        private readonly Dictionary<(string kind, int level, EnemyRank rank, int trait), Loadout> _shared = new();

        private static IEnumerable<Mod> Base(EnemyKind kind, int level, float rankHealth)
        {
            yield return Flat(Stat.Health, HealthAt(level) * rankHealth * kind.Health);
            yield return Flat(Stat.Mana, 400f + 40f * level);
            yield return Flat(Stat.ManaRegen, 40f);
            yield return Flat(Stat.MoveSpeed, kind.MoveSpeed);
            yield return Flat(Stat.Armour, 6f * level);
            yield return Flat(Stat.Evasion, 5f * level);
            yield return Flat(Stat.Accuracy, 8f * level);
            yield return Inc(Stat.Damage, -35f, Tags.Spell);
            foreach (Mod mod in kind.Mods) yield return mod;
        }

        private static WeaponProfile WeaponFor(EnemyKind kind, int level, float rankDamage)
        {
            float damage = DamageAt(level) * rankDamage * kind.Damage;
            return new WeaponProfile(damage * 0.85f, damage * 1.15f, WeaponProfile.BareTime, 5f, kind.Weapon);
        }

        /// <summary>The numbers for this kind of enemy at this level, shared by every one of them there is.</summary>
        public Loadout Shared(EnemyKind kind, int level, EnemyRank rank, int trait)
        {
            if (_shared.TryGetValue((kind.Id, level, rank, trait), out Loadout? loadout)) return loadout;

            float rankHealth = rank == EnemyRank.Elite ? 2.6f : 1f;
            float rankDamage = rank == EnemyRank.Elite ? 1.4f : 1f;
            IEnumerable<Mod> mods = Base(kind, level, rankHealth);
            if (trait != NoTrait)
            {
                mods = mods.Concat(Traits[trait].Mods);
                if (Traits[trait].Name == "Warded") mods = mods.Append(Flat(Stat.Barrier, HealthAt(level) * rankHealth));
            }

            loadout = new Loadout(level, WeaponFor(kind, level, rankDamage), mods, kind.Skills);
            _shared.Add((kind.Id, level, rank, trait), loadout);
            return loadout;
        }

        /// <summary>
        /// A boss is the only one of itself, so it gets a loadout to itself. It wears real gear and walks the same tree a hero does. It'll fight with all
        /// of that, and what it wore is what it drops.
        /// </summary>
        public static Loadout Boss(EnemyKind kind, int level, WorldRandom random, out Item[] gear, out string? title)
        {
            var mods = new List<Mod>(Base(kind, level, 11f));
            WorldRandom gearDraw = random.Fork("gear");

            ItemSlot[] slots = { ItemSlot.Amulet, ItemSlot.Body, ItemSlot.Ring };
            var worn = new List<Item>();
            int pieces = gearDraw.Int(1, 2);
            for (int i = 0; i < pieces; i++)
            {
                ItemSlot slot = slots[gearDraw.Int(slots.Length)];
                Rarity least = gearDraw.Chance(0.45f) ? Rarity.Legendary : Rarity.Rare;
                Item item = ItemGenerator.Roll(level + 1, gearDraw.Fork(i), 0.5f, least, slot);
                worn.Add(item);
                mods.AddRange(item.Mods());
            }
            gear = worn.ToArray();

            WorldRandom keystoneDraw = random.Fork("keystone");
            title = null;
            if (level >= 3)
            {
                List<TalentNode> options = TalentTree.Nodes.Where(n => n.Kind == TalentKind.Keystone && FitsABoss(n)).ToList();
                TalentNode keystone = keystoneDraw.Pick(options);
                mods.AddRange(keystone.Mods);
                title = keystone.Title ?? keystone.Name;
            }

            WorldRandom traitDraw = random.Fork("traits");
            mods.AddRange(traitDraw.Pick(Traits).Mods);
            return new Loadout(level, WeaponFor(kind, level, 1.9f), mods, kind.Skills);
        }

        // Some keystones only mean something with a player's hands on them.
        private static bool FitsABoss(TalentNode node)
        {
            foreach (Mod mod in node.Mods)
            {
                if (mod.Kind == ModKind.Rule && (mod.Rule & (Rule.NoOwnDamage | Rule.Mimic | Rule.Guardian | Rule.CostsHealth)) != 0) return false;
                if (mod.Scope.HasFlag(Tags.Minion) || mod.Stat == Stat.RaiseFallen) return false;
            }
            return true;
        }

        /// <summary>What a minion comes to, for someone with this loadout. Whatever its owner has for minions it has for itself.</summary>
        public static Loadout MinionOf(Loadout owner, ActorSheet sheet, EnemyKind kind)
        {
            var mods = new List<Mod>(Base(kind, owner.Level, 1f));
            bool puppet = sheet.Has(Rule.Puppeteer);
            foreach (Mod mod in owner.Mods)
            {
                if ((mod.Scope & Tags.Minion) != 0)
                {
                    mods.Add(mod with { Scope = mod.Scope & ~Tags.Minion });
                    continue;
                }
                if (puppet && mod.Stat is Stat.Damage or Stat.AttackSpeed or Stat.CastSpeed or Stat.CritChance or Stat.CritMultiplier) mods.Add(mod);
            }
            return new Loadout(owner.Level, WeaponFor(kind, owner.Level, 1f), mods, kind.Skills);
        }

        /// <summary>What a fallen enemy can do on your side: everything it could do before, in your colours, a little worn.</summary>
        public static Loadout Risen(Loadout enemy, int level)
        {
            var mods = new List<Mod>(enemy.Mods.Length + 1);
            mods.AddRange(enemy.Mods);
            mods.Add(More(Stat.Health, -40f));
            return new Loadout(level, enemy.Weapon, mods, enemy.Skills);
        }
    }
}

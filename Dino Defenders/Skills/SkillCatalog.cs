using Dino_Defenders.Stats;
using static Dino_Defenders.Stats.M;

namespace Dino_Defenders.Skills
{
    /// <summary>Lasting effects skills can put on someone. Modifiers with a clock on them, so a new one is a few lines and nothing else.</summary>
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

        public static readonly BuffDefinition Afterglow = Add(new BuffDefinition
        {
            Id = "afterglow",
            Name = "Afterglow",
            Duration = 3f,
            Mods = new[] { More(Stat.Damage, 20f), Inc(Stat.MoveSpeed, 15f) },
        });

        private static BuffDefinition Add(BuffDefinition buff)
        {
            Everything.Add(buff);
            buff.Number = (ushort)Everything.Count;
            return buff;
        }

        public static BuffDefinition? Get(int number) => number >= 1 && number <= Everything.Count ? Everything[number - 1] : null;
    }

    /// <summary>
    /// Every skill there is. Enemies use these too: a witch throwing a firebolt is throwing the same firebolt as a hero, reading her own sheet, so
    /// anything added here is at once something the world can do to a player. Numbers are for level 1, and the sheet behind a skill does the rest.
    /// </summary>
    public static class SkillCatalog
    {
        private static readonly List<SkillDefinition> Everything = new();

        public static IReadOnlyList<SkillDefinition> All => Everything;

        // Blades.
        public static readonly SkillDefinition Strike = Add(new SkillDefinition
        {
            Id = "strike", Name = "Strike", Form = SkillForm.Strike, Tags = Tags.Attack | Tags.Melee, Look = DamageType.Physical,
            Text = "A plain blow with what's in your hands.",
        });

        public static readonly SkillDefinition Whirl = Add(new SkillDefinition
        {
            Id = "whirl", Name = "Whirl", Form = SkillForm.Strike, Tags = Tags.Attack | Tags.Melee | Tags.Area, Look = DamageType.Physical,
            Text = "A full turn with the blade out, striking everything around you.",
            UseTime = 0.34f, ImpactAt = 0.3f, MoveFactor = 1f, Cooldown = 8f, WeaponShare = 1.9f, Reach = 8.5f, Arc = MathF.Tau,
        });

        public static readonly SkillDefinition Cleave = Add(new SkillDefinition
        {
            Id = "cleave", Name = "Cleave", Form = SkillForm.Strike, Tags = Tags.Attack | Tags.Melee | Tags.Area, Look = DamageType.Physical,
            Text = "A wide sweep that catches everything in front of you, at the cost of some weight.",
            UseTime = 0.75f, WeaponShare = 0.85f, Reach = 7.5f, Arc = 3.4f, Cost = 3f,
        });

        public static readonly SkillDefinition Sunder = Add(new SkillDefinition
        {
            Id = "sunder", Name = "Sunder", Form = SkillForm.Strike, Tags = Tags.Attack | Tags.Melee, Look = DamageType.Physical,
            Text = "A slow, heavy blow. It ignores a share of armour, and stuns what it doesn't kill.",
            UseTime = 1.05f, ImpactAt = 0.55f, MoveFactor = 0.25f, WeaponShare = 2.4f, Reach = 6.8f, Arc = 1.5f, Cost = 5f,
            Mods = new[] { Flat(Stat.ArmourIgnore, 40f), Chance(Tags.Stun, 60f) },
        });

        public static readonly SkillDefinition Rend = Add(new SkillDefinition
        {
            Id = "rend", Name = "Rend", Form = SkillForm.Strike, Tags = Tags.Attack | Tags.Melee, Look = DamageType.Physical,
            Text = "A dragging cut that leaves the target bleeding.",
            UseTime = 0.55f, WeaponShare = 0.75f, Arc = 2f, Cost = 3f,
            Mods = new[] { Chance(Tags.Bleed, 70f) },
        });

        public static readonly SkillDefinition Lunge = Add(new SkillDefinition
        {
            Id = "lunge", Name = "Lunge", Form = SkillForm.Leap, Tags = Tags.Attack | Tags.Melee | Tags.Movement, Look = DamageType.Physical,
            Text = "A rush at where you're aiming, striking what's there when you arrive.",
            UseTime = 0.36f, ImpactAt = 0.95f, Cooldown = 3f, WeaponShare = 1.3f, Reach = 6f, Arc = 3f, Range = 16f, Cost = 4f, LeapSpeed = 46f,
        });

        // Things thrown and shot.
        public static readonly SkillDefinition Shot = Add(new SkillDefinition
        {
            Id = "shot", Name = "Loose", Needs = Tags.Bow, Form = SkillForm.Bolt, Tags = Tags.Attack | Tags.Projectile, Look = DamageType.Physical,
            Text = "A single arrow, or whatever else you have that flies.",
            UseTime = 0.65f, ImpactAt = 0.5f, MoveFactor = 0.7f, Speed = 56f, Range = 32f,
        });

        public static readonly SkillDefinition Volley = Add(new SkillDefinition
        {
            Id = "volley", Name = "Volley", Needs = Tags.Bow, Form = SkillForm.Bolt, Tags = Tags.Attack | Tags.Projectile, Look = DamageType.Physical,
            Text = "A fan of shots at once.",
            UseTime = 0.8f, ImpactAt = 0.5f, MoveFactor = 0.6f, Cooldown = 3f, WeaponShare = 0.6f, AddedEffect = 0.6f, Speed = 50f, Projectiles = 5, Spread = 0.16f, Range = 26f, Cost = 6f,
        });

        public static readonly SkillDefinition Knives = Add(new SkillDefinition
        {
            Id = "knives", Name = "Throwing Knives", Form = SkillForm.Bolt, Tags = Tags.Attack | Tags.Projectile, Look = DamageType.Physical,
            Text = "Three quick blades, for anyone with a free hand.",
            UseTime = 0.5f, ImpactAt = 0.4f, MoveFactor = 0.8f, WeaponShare = 0.4f, AddedEffect = 0.4f, Speed = 46f, Projectiles = 3, Spread = 0.12f, Range = 20f, Cost = 2f,
            Mods = new[] { Chance(Tags.Bleed, 20f) },
        });

        // Fire.
        public static readonly SkillDefinition Firebolt = Add(new SkillDefinition
        {
            Id = "firebolt", Name = "Firebolt", Form = SkillForm.Bolt, Tags = Tags.Spell | Tags.Projectile | Tags.Area, Look = DamageType.Fire,
            Text = "A ball of flame that bursts where it lands.",
            UseTime = 0.7f, ImpactAt = 0.55f, Speed = 34f, Radius = 3.4f, Range = 28f, Cost = 4f,
            Damage = new[] { new BaseDamage(DamageType.Fire, 7f, 11f) },
            Mods = new[] { Chance(Tags.Burn, 25f) },
        });

        public static readonly SkillDefinition Immolate = Add(new SkillDefinition
        {
            Id = "immolate", Name = "Immolate", Form = SkillForm.Nova, Tags = Tags.Spell | Tags.Area, Look = DamageType.Fire,
            Text = "The air around you catches.",
            UseTime = 0.75f, ImpactAt = 0.6f, Radius = 8f, Cost = 7f, Range = 7f,
            Damage = new[] { new BaseDamage(DamageType.Fire, 9f, 13f) },
            Mods = new[] { Chance(Tags.Burn, 35f) },
        });

        public static readonly SkillDefinition Emberfall = Add(new SkillDefinition
        {
            Id = "emberfall", Name = "Emberfall", Form = SkillForm.Burst, Tags = Tags.Spell | Tags.Area, Look = DamageType.Fire,
            Text = "Burning stone comes down where you point, a moment later.",
            UseTime = 0.85f, ImpactAt = 0.7f, Delay = 0.8f, Radius = 6.5f, Cooldown = 4f, Range = 30f, Cost = 10f,
            Damage = new[] { new BaseDamage(DamageType.Fire, 24f, 34f) },
            Mods = new[] { Chance(Tags.Burn, 50f) },
        });

        // Cold.
        public static readonly SkillDefinition FrostShards = Add(new SkillDefinition
        {
            Id = "frost-shards", Name = "Frost Shards", Form = SkillForm.Bolt, Tags = Tags.Spell | Tags.Projectile, Look = DamageType.Cold,
            Text = "Three splinters of ice that pass through what they hit.",
            UseTime = 0.65f, ImpactAt = 0.5f, Speed = 40f, Projectiles = 3, Spread = 0.2f, Pierce = 1, Range = 24f, Cost = 5f, AddedEffect = 0.5f,
            Damage = new[] { new BaseDamage(DamageType.Cold, 4f, 6f) },
        });

        public static readonly SkillDefinition FrostNova = Add(new SkillDefinition
        {
            Id = "frost-nova", Name = "Frost Nova", Form = SkillForm.Nova, Tags = Tags.Spell | Tags.Area, Look = DamageType.Cold,
            Text = "A ring of cold that drives out from you, chilling all it touches.",
            UseTime = 0.6f, ImpactAt = 0.5f, Radius = 10f, Cooldown = 5f, Cost = 8f, Range = 8f,
            Damage = new[] { new BaseDamage(DamageType.Cold, 9f, 12f) },
            Mods = new[] { Inc(Stat.AilmentEffect, 60f, Tags.Chill), Chance(Tags.Freeze, 15f) },
        });

        public static readonly SkillDefinition GlacialLance = Add(new SkillDefinition
        {
            Id = "glacial-lance", Name = "Glacial Lance", Form = SkillForm.Beam, Tags = Tags.Spell | Tags.Area, Look = DamageType.Cold,
            Text = "A spear of ice through everything in a line.",
            UseTime = 0.85f, ImpactAt = 0.6f, Reach = 24f, Radius = 1.6f, Range = 22f, Cost = 8f,
            Damage = new[] { new BaseDamage(DamageType.Cold, 14f, 20f) },
            Mods = new[] { Chance(Tags.Freeze, 20f) },
        });

        // Lightning.
        public static readonly SkillDefinition Spark = Add(new SkillDefinition
        {
            Id = "spark", Name = "Forked Spark", Form = SkillForm.Chain, Tags = Tags.Spell | Tags.Chaining, Look = DamageType.Lightning,
            Text = "A bolt that leaps from one enemy to the next. Wild, but quick.",
            UseTime = 0.55f, ImpactAt = 0.4f, Reach = 20f, Radius = 13f, Chains = 3, Range = 20f, Cost = 4f, AddedEffect = 0.7f,
            Damage = new[] { new BaseDamage(DamageType.Lightning, 1f, 17f) },
            Mods = new[] { Chance(Tags.Shock, 30f) },
        });

        public static readonly SkillDefinition Stormcall = Add(new SkillDefinition
        {
            Id = "stormcall", Name = "Stormcall", Form = SkillForm.Zone, Tags = Tags.Spell | Tags.Area | Tags.Zone, Look = DamageType.Lightning,
            Text = "A patch of sky turns on the ground beneath it, striking again and again.",
            UseTime = 0.8f, ImpactAt = 0.65f, Radius = 8f, Duration = 5f, Interval = 0.55f, Cooldown = 6f, Range = 26f, Cost = 13f, AddedEffect = 0.25f,
            Damage = new[] { new BaseDamage(DamageType.Lightning, 3f, 10f) },
            Mods = new[] { Chance(Tags.Shock, 20f) },
        });

        // Magic.
        public static readonly SkillDefinition ArcaneDart = Add(new SkillDefinition
        {
            Id = "arcane-dart", Name = "Arcane Dart", Form = SkillForm.Bolt, Tags = Tags.Spell | Tags.Projectile, Look = DamageType.Magic,
            Text = "Plain force, thrown quickly, that loosens what it touches.",
            UseTime = 0.48f, ImpactAt = 0.5f, Speed = 46f, Range = 26f, Cost = 3f, AddedEffect = 0.8f, CritChance = 7f,
            Damage = new[] { new BaseDamage(DamageType.Magic, 5f, 8f) },
            Mods = new[] { Chance(Tags.Hex, 20f) },
        });

        public static readonly SkillDefinition Unmaking = Add(new SkillDefinition
        {
            Id = "unmaking", Name = "Unmaking", Form = SkillForm.Burst, Tags = Tags.Spell | Tags.Area, Look = DamageType.Magic,
            Text = "A point in the air gives way, taking what's near it along.",
            UseTime = 0.9f, ImpactAt = 0.65f, Delay = 0.35f, Radius = 5.5f, Cooldown = 3f, Range = 24f, Cost = 9f,
            Damage = new[] { new BaseDamage(DamageType.Magic, 19f, 25f) },
            Mods = new[] { Chance(Tags.Hex, 60f) },
        });

        // Poison.
        public static readonly SkillDefinition VenomSpit = Add(new SkillDefinition
        {
            Id = "venom-spit", Name = "Venom Spit", Form = SkillForm.Bolt, Tags = Tags.Spell | Tags.Projectile, Look = DamageType.Poison,
            Text = "A gobbet of poison that does most of its work later.",
            UseTime = 0.58f, ImpactAt = 0.5f, Speed = 32f, Range = 22f, Cost = 3f,
            Damage = new[] { new BaseDamage(DamageType.Poison, 5f, 8f) },
            Mods = new[] { Chance(Tags.Venom, 70f) },
        });

        public static readonly SkillDefinition Miasma = Add(new SkillDefinition
        {
            Id = "miasma", Name = "Miasma", Form = SkillForm.Zone, Tags = Tags.Spell | Tags.Area | Tags.Zone, Look = DamageType.Poison,
            Text = "Foul air that hangs where you leave it.",
            UseTime = 0.75f, ImpactAt = 0.6f, Radius = 7f, Duration = 6f, Interval = 0.75f, Cooldown = 5f, Range = 24f, Cost = 9f, AddedEffect = 0.25f,
            Damage = new[] { new BaseDamage(DamageType.Poison, 2f, 4f) },
            Mods = new[] { Chance(Tags.Venom, 100f) },
        });

        // Bone and blood.
        public static readonly SkillDefinition BoneSpear = Add(new SkillDefinition
        {
            Id = "bone-spear", Name = "Bone Spear", Form = SkillForm.Bolt, Tags = Tags.Spell | Tags.Projectile, Look = DamageType.Physical,
            Text = "A shard of something's skeleton, driven through everything in its path.",
            UseTime = 0.75f, ImpactAt = 0.55f, Speed = 52f, Pierce = 3, Range = 26f, Cost = 5f, CritChance = 8f,
            Damage = new[] { new BaseDamage(DamageType.Physical, 10f, 14f) },
            Mods = new[] { Chance(Tags.Bleed, 25f) },
        });

        public static readonly SkillDefinition Bloodburst = Add(new SkillDefinition
        {
            Id = "bloodburst", Name = "Bloodburst", Form = SkillForm.Nova, Tags = Tags.Spell | Tags.Area, Look = DamageType.Physical,
            Text = "Whatever is bleeding nearby comes open all at once.",
            UseTime = 0.7f, ImpactAt = 0.55f, Radius = 9f, Cooldown = 4f, Cost = 8f, Range = 8f,
            Damage = new[] { new BaseDamage(DamageType.Physical, 14f, 20f) },
            Mods = new[] { Chance(Tags.Bleed, 40f), More(Stat.Damage, 60f).Against(Condition.Bleeding) },
        });

        // Oneself.
        public static readonly SkillDefinition StoneSkinCast = Add(new SkillDefinition
        {
            Id = "stone-skin", Name = "Stone Skin", Form = SkillForm.Buff, Tags = Tags.Spell | Tags.Buff, Look = DamageType.Physical,
            Text = "Your skin takes on the weight of rock for a while.",
            UseTime = 0.4f, ImpactAt = 0.6f, Cooldown = 20f, Cost = 10f, MoveFactor = 1f, Buff = BuffCatalog.StoneSkin, Range = 12f,
        });

        public static readonly SkillDefinition QuickeningCast = Add(new SkillDefinition
        {
            Id = "quickening", Name = "Quickening", Form = SkillForm.Buff, Tags = Tags.Spell | Tags.Buff, Look = DamageType.Lightning,
            Text = "Everything you do comes a little sooner.",
            UseTime = 0.3f, ImpactAt = 0.6f, Cooldown = 15f, Cost = 7f, MoveFactor = 1f, Buff = BuffCatalog.Quickening, Range = 12f,
        });

        public static readonly SkillDefinition Blink = Add(new SkillDefinition
        {
            Id = "blink", Name = "Blink", Form = SkillForm.Leap, Tags = Tags.Spell | Tags.Movement, Look = DamageType.Magic,
            Text = "You're simply somewhere else, a little way off.",
            UseTime = 0.2f, ImpactAt = 0.1f, Cooldown = 4f, Range = 18f, Cost = 6f, LeapSpeed = 110f, MoveFactor = 1f,
        });

        public static readonly SkillDefinition SanctuaryCast = Add(new SkillDefinition
        {
            Id = "sanctuary", Name = "Sanctuary", Form = SkillForm.Buff, Tags = Tags.Spell | Tags.Buff, Look = DamageType.Magic,
            Text = "A moment's shelter, wherever you happen to be standing.",
            UseTime = 0.45f, ImpactAt = 0.6f, Cooldown = 18f, Cost = 9f, MoveFactor = 0.8f, Buff = BuffCatalog.Sanctuary, Range = 12f,
        });

        public static readonly SkillDefinition ShieldBash = Add(new SkillDefinition
        {
            Id = "shield-bash", Name = "Shield Bash", Form = SkillForm.Strike, Tags = Tags.Attack | Tags.Melee, Look = DamageType.Physical,
            Text = "Your whole weight behind whatever you're carrying. The more armour you have, the harder it lands.",
            UseTime = 0.6f, ImpactAt = 0.4f, Cooldown = 2f, WeaponShare = 0.9f, Reach = 5.6f, Arc = 2.2f, Cost = 3f,
            Mods = new[] { Chance(Tags.Stun, 50f), Inc(Stat.Damage, 2f).For(Per.Armour, 50f, 200f) },
        });

        public static readonly SkillDefinition Sap = Add(new SkillDefinition
        {
            Id = "sap", Name = "Sap", Form = SkillForm.Beam, Tags = Tags.Spell, Look = DamageType.Magic,
            Text = "A thin line of force that drinks what it touches.",
            UseTime = 0.6f, ImpactAt = 0.5f, Reach = 16f, Radius = 1.2f, Range = 15f, Cost = 5f,
            Damage = new[] { new BaseDamage(DamageType.Magic, 6f, 9f) },
            Mods = new[] { Flat(Stat.HealthLeech, 12f), Flat(Stat.ManaLeech, 6f) },
        });

        public static readonly SkillDefinition Tidepool = Add(new SkillDefinition
        {
            Id = "tidepool", Name = "Tidepool", Form = SkillForm.Zone, Tags = Tags.Spell | Tags.Area | Tags.Zone, Look = DamageType.Cold,
            Text = "Water wells up out of the ground. It counts as water for everyone standing in it, for better or worse.",
            UseTime = 0.5f, ImpactAt = 0.6f, Radius = 8f, Duration = 10f, Interval = 1f, Cooldown = 10f, Range = 20f, Cost = 8f,
        });

        // Others.
        public static readonly SkillDefinition RaiseBones = Add(new SkillDefinition
        {
            Id = "raise-bones", Name = "Raise Bones", Form = SkillForm.Summon, Tags = Tags.Spell | Tags.Minion, Look = DamageType.Magic,
            Text = "The dead get up and stand with you.",
            UseTime = 0.9f, ImpactAt = 0.7f, Cooldown = 2f, Cost = 12f, Minion = "bones", MinionCount = 2, Duration = 40f, Range = 12f,
        });

        public static readonly SkillDefinition CallHound = Add(new SkillDefinition
        {
            Id = "call-hound", Name = "Call the Pack", Form = SkillForm.Summon, Tags = Tags.Spell | Tags.Minion, Look = DamageType.Physical,
            Text = "Something quick comes when you whistle.",
            UseTime = 0.5f, ImpactAt = 0.6f, Cooldown = 4f, Cost = 10f, Minion = "hound", MinionCount = 1, Duration = 30f, Range = 12f,
        });

        // What other things call on, and aren't learned.
        public static readonly SkillDefinition CinderTrail = Add(new SkillDefinition
        {
            Id = "cinder-trail", Name = "Burning Ground", Form = SkillForm.Zone, Tags = Tags.Spell | Tags.Area | Tags.Zone | Tags.Triggered, Look = DamageType.Fire, Hidden = true,
            Radius = 3.2f, Duration = 3f, Interval = 0.5f, AddedEffect = 0.1f,
            Damage = new[] { new BaseDamage(DamageType.Fire, 2f, 3f) },
            Mods = new[] { Chance(Tags.Burn, 40f) },
        });

        public static readonly SkillDefinition Puddle = Add(new SkillDefinition
        {
            Id = "puddle", Name = "Standing Water", Form = SkillForm.Zone, Tags = Tags.Spell | Tags.Area | Tags.Zone | Tags.Triggered, Look = DamageType.Cold, Hidden = true,
            Radius = 5f, Duration = 8f, Interval = 1f,
        });

        public static readonly SkillDefinition VenomCloud = Add(new SkillDefinition
        {
            Id = "venom-cloud", Name = "Contagion", Form = SkillForm.Nova, Tags = Tags.Spell | Tags.Area | Tags.Triggered, Look = DamageType.Poison, Hidden = true,
            Radius = 8f, AddedEffect = 0.1f,
            Damage = new[] { new BaseDamage(DamageType.Poison, 1f, 2f) },
            Mods = new[] { Chance(Tags.Venom, 100f) },
        });

        public static readonly SkillDefinition Retaliate = Add(new SkillDefinition
        {
            Id = "retaliate", Name = "Retaliation", Form = SkillForm.Nova, Tags = Tags.Attack | Tags.Area | Tags.Melee | Tags.Triggered, Look = DamageType.Physical, Hidden = true,
            Radius = 7f, WeaponShare = 1.2f,
        });

        public static readonly SkillDefinition Bite = Add(new SkillDefinition
        {
            Id = "bite", Name = "Bite", Form = SkillForm.Strike, Tags = Tags.Attack | Tags.Melee, Look = DamageType.Physical, Hidden = true,
            UseTime = 0.5f, Reach = 5f, Arc = 2f,
            Mods = new[] { Chance(Tags.Bleed, 25f) },
        });

        public static readonly SkillDefinition Maul = Add(new SkillDefinition
        {
            Id = "maul", Name = "Maul", Form = SkillForm.Strike, Tags = Tags.Attack | Tags.Melee | Tags.Area, Look = DamageType.Physical, Hidden = true,
            UseTime = 1f, ImpactAt = 0.6f, Reach = 7.5f, Arc = 2.4f, WeaponShare = 1.6f,
            Mods = new[] { Chance(Tags.Stun, 50f) },
        });

        private static SkillDefinition Add(SkillDefinition skill)
        {
            Everything.Add(skill);
            skill.Number = (ushort)Everything.Count;
            return skill;
        }

        public static SkillDefinition Get(int number) => Everything[number - 1];

        /// <summary>For a number that came from somewhere else. Something nobody has heard of looks like a plain blow, rather than bringing anything down.</summary>
        public static SkillDefinition Named(int number) => IsNumber(number) ? Everything[number - 1] : Strike;

        public static bool IsNumber(int number) => number >= 1 && number <= Everything.Count;

        public static SkillDefinition? Find(string id)
        {
            foreach (SkillDefinition skill in Everything)
            {
                if (skill.Id == id) return skill;
            }
            return null;
        }
    }
}

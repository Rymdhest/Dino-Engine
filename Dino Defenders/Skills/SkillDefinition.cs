using Dino_Defenders.Stats;

namespace Dino_Defenders.Skills
{
    /// <summary>How a skill gets from its user to whatever it reaches.</summary>
    public enum SkillForm : byte
    {
        Strike,     // A sweep in front of the user.
        Nova,       // A ring around the user.
        Bolt,       // Projectiles.
        Burst,      // A blast at a place, a moment later.
        Zone,       // Ground that does something for a while.
        Chain,      // Leaps from one target to the next.
        Beam,       // A straight line, at once.
        Buff,       // On the user.
        Summon,     // Others, to fight beside them.
        Leap,       // Carries the user to a place, and strikes where they land.
    }

    /// <summary>A share of what a spell does, before anything else has touched it.</summary>
    public readonly record struct BaseDamage(DamageType Type, float Min, float Max);

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

    /// <summary>
    /// A skill, the same whoever uses it: heroes and enemies draw on one catalogue, and what differs is the sheet behind it. Everything here is
    /// the start of a calculation, which <see cref="SkillProfile"/> finishes once for each user rather than each time it's used.
    /// </summary>
    public sealed class SkillDefinition
    {
        public const ushort None = 0;

        public ushort Number;
        public string Id = "";
        public string Name = "";
        public string Text = "";
        public SkillForm Form;
        public Tags Tags;

        /// <summary>What it looks and sounds like, whatever it has been turned into since.</summary>
        public DamageType Look;

        /// <summary>Seconds a use takes at normal speed, how far into that it lands, and how much the user can walk meanwhile.</summary>
        public float UseTime = 0.6f;
        public float ImpactAt = 0.25f;
        public float MoveFactor = 0.55f;
        public float Cooldown;
        public float Cost;

        /// <summary>For attacks: how much of the weapon's damage each blow does.</summary>
        public float WeaponShare = 1f;

        /// <summary>For spells: what a use does at level 1. <see cref="SkillPower"/> takes it from there.</summary>
        public BaseDamage[] Damage = Array.Empty<BaseDamage>();

        /// <summary>How much of any flat damage added to it this skill gets. Quick skills get less, or adding flat damage would outrun everything else.</summary>
        public float AddedEffect = 1f;

        /// <summary>For spells: percent. Attacks use the weapon's.</summary>
        public float CritChance = 5f;

        /// <summary>What's particular to this skill. These count only when it's being used.</summary>
        public Mod[] Mods = Array.Empty<Mod>();

        // Its shape. Which of these matter depends on the form.
        public float Reach = 6.4f;      // Metres, for a strike or a line.
        public float Arc = 2.4f;        // Radians, for a strike.
        public float Radius;            // Metres, for anything that fills an area.
        public float Speed = 40f;       // Metres a second, for what flies.
        public float Range = 26f;       // Metres. How far from its user it can be aimed, and how near an enemy wants to be.
        public float Duration;          // Seconds, for what lasts.
        public float Interval = 0.5f;   // Seconds between a zone's pulses.
        public float Delay;             // Seconds between a burst being called and it landing.
        public int Projectiles = 1;
        public float Spread = 0.2f;     // Radians between neighbouring projectiles.
        public int Pierce;
        public int Chains;

        public BuffDefinition? Buff;
        public string? Minion;
        public int MinionCount = 1;

        /// <summary>What has to be in the hands to use it, if anything.</summary>
        public Tags Needs;

        /// <summary>A place a skill can't be learned, only seen: for what's meant for beasts and for what other skills call on.</summary>
        public bool Hidden;

        /// <summary>A movement skill's speed in metres a second, carried out by the hero's own step so that it can be predicted.</summary>
        public float LeapSpeed = 44f;

        public bool IsAttack => (Tags & Tags.Attack) != 0;
        public bool IsSpell => (Tags & Tags.Spell) != 0;
        public bool DealsDamage => WeaponShare > 0f && IsAttack || Damage.Length > 0;

        /// <summary>What a spell's base damage is multiplied by, at a level. Weapons do this for attacks, by getting better.</summary>
        public static float SkillPower(float level) => 1f + 0.38f * MathF.Max(level - 1f, 0f);
    }
}

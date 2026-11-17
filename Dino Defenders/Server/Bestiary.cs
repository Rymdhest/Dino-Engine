using Dino_Defenders.Books;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Stats;
using Dino_Defenders.Talents;
using Dino_Engine.World;
using static Dino_Defenders.Stats.M;

namespace Dino_Defenders.Server
{
    /// <summary>
    /// What a kind of creature is. It has a level, books written in the same words as anyone's, and a list of modifiers, exactly as a hero does: there's
    /// nothing else to an enemy. Everything that makes a bandit a bandit is written here as stats.
    /// </summary>
    public sealed class EnemyKind
    {
        public string Id = "";
        public string Name = "";
        public BookPlan[] Books = Array.Empty<BookPlan>();
        public Mod[] Mods = Array.Empty<Mod>();
        public Tags Weapon = Tags.Sword | Tags.OneHanded;
        public float MoveSpeed = 6.8f;
        public float AggroRadius = 21f;
        public float LeashRadius = 60f;
        public bool KeepsAway;      // Would sooner stand off and shoot.
        public bool Skirmishes;     // Strikes and is gone again, to come back when it's ready to: a runner, which nothing catches standing.

        /// <summary>A Barrier it has, as a share of what's ordinary Health at its level: a shell of light on what the Ossuary called up, which comes back if it's let.</summary>
        public float Barrier;
        public float Health = 1f;
        public float Damage = 1f;
        public string Look = "bandit";  // What it's drawn as, which CharacterFactory.Resolve has to know.
        public int Variants;        // How many ways it's drawn, numbered onto Look.
        public float Scale = 1f;

        /// <summary>How far its body reaches from where it stands, in metres at a scale of one, for what's solid and what's struck. Nothing means a person's, which grows a little with them and no further.</summary>
        public float Bulk;

        /// <summary>Whether it stands where it's put and never moves, as a turret does.</summary>
        public bool Rooted => MoveSpeed <= 0f;

        /// <summary>How far its body reaches at this scale.</summary>
        public float RadiusAt(float scale) => Bulk > 0f ? Bulk * scale : WorldData.CharacterRadius * MathF.Min(scale, 1.4f);
    }

    /// <summary>A quality an enemy can have on top of what it is, and something it can do because of it. They're the same modifiers a hero's gear is made of, and the same books.</summary>
    public readonly record struct EnemyTrait(string Name, Mod[] Mods, BookPlan Book);

    /// <summary>What a rarity makes of a creature, on top of what it is. How many traits is how many more things it can do.</summary>
    public readonly record struct EnemyPower(float Health, float Damage, float Scale, int Traits, float Importance);

    /// <summary>
    /// The creatures of the world, and how strong they are at each level. Enemies of one kind, level and trait share a single loadout and so a single
    /// compiled sheet. That is what makes a horde cheap: three hundred skeletons are one set of numbers.
    /// </summary>
    public sealed class Bestiary
    {
        public const int NoTraits = 0;
        private const int HeroOnlyRerolls = 8;

        private static readonly EnemyPower[] Powers =
        {
            new(1f, 1f, 1f, 0, 1.5f),
            new(1.6f, 1.2f, 1.1f, 1, 1.75f),
            new(2.6f, 1.4f, 1.22f, 2, 2f),
            new(11f, 1.9f, 1.7f, 3, 4f),
        };

        public static EnemyPower PowerOf(Rarity rarity) => Powers[(int)rarity];

        // Measured against sample heroes in BuildLab's balance table, who know nothing of keystones and don't choose their gear. A hero gains most
        // early, as gear comes in tiers, so these climb the same way, and what's exponential in them is what keeps lands past the level cap getting harder.
        // Against these a sample hero needs about three hits and survives about ten. Anything a real build does better than that is the build working.
        public const float AccuracyPerLevel = 8f;
        public const float AccuracyAtFirst = 60f;

        public static float AccuracyAt(int level) => AccuracyAtFirst + AccuracyPerLevel * level;

        public static float HealthAt(int level) => (26f + 0.75f * level * level) * MathF.Pow(1.02f, level) * Outfitted(level, 0.06f);
        public static float DamageAt(int level) => (7f + 1.6f * level + 0.01f * level * level) * MathF.Pow(1.006f, level) * Outfitted(level, 0.13f);

        // Heroes wear legs, hands and a second ring as well, and the table stood without them. That only tells once they've found them, so it comes in over the first dozen levels.
        private static float Outfitted(int level, float most) => 1f + most * level * level / (level * level + 100f);

        // Worked out for a world of a thousand enemies, of which a player meets a good part, and a hero who should come out of it a few levels up. A
        // kill is worth what it took, so a pack's followers are worth less than what leads them.
        // It stops growing where heroes stop, at the hundredth level, as nobody past that can use it and it would soon be more than a number holds.
        public static int ExperienceAt(int level, float rankHealth) => (int)MathF.Ceiling(6f * MathF.Pow(1.11f, Math.Min(level, HeroState.MaxLevel)) * rankHealth);

        private static readonly Mod[] Undead = { Resist(Tags.Poison, 40f), Resist(Tags.Cold, 30f), Resist(Tags.Fire, -20f), Flat(Stat.AilmentAvoid, 100f, Tags.Bleed) };

        private const float Spikes = 25f;       // Percent of what a spikeback takes from a blow struck within reach of it that it gives back.

        public static readonly EnemyKind Outlaw = new EnemyKind
        {
            Id = "outlaw", Name = "Outlaw", Books = new[] { Shelf.Strike },
        };

        public static readonly EnemyKind Archer = new EnemyKind
        {
            Id = "archer", Name = "Outlaw Archer", Books = new[] { Shelf.Shot }, Weapon = Tags.Bow | Tags.TwoHanded,
            KeepsAway = true, AggroRadius = 30f, Damage = 0.75f,
        };

        // Most of a pack is here to be cut down. Against a build that's working these go in a blow or two, and it's what's among them that takes thought.
        public static readonly EnemyKind Cutthroat = new EnemyKind
        {
            Id = "cutthroat", Name = "Cutthroat", Books = new[] { Shelf.Strike }, MoveSpeed = 7.8f, Health = 0.5f, Damage = 0.42f,
            Look = "cutthroat", Variants = 4,
        };

        public static readonly EnemyKind Brute = new EnemyKind
        {
            Id = "brute", Name = "Outlaw Brute", Books = new[] { Shelf.Maul, Shelf.Strike }, MoveSpeed = 5.6f, Health = 2.2f, Damage = 1.3f,
            Mods = new[] { More(Stat.Armour, 30f) }, Look = "brute", Variants = 3, Scale = 1.28f,
        };

        public static readonly EnemyKind Sentry = new EnemyKind
        {
            Id = "sentry", Name = "Sentry", Books = new[] { Shelf.Strike }, Mods = new[] { More(Stat.Armour, 50f), Flat(Stat.BlockChance, 15f) },
        };

        public static readonly EnemyKind Restless = new EnemyKind
        {
            Id = "restless", Name = "Restless Dead", Books = new[] { Shelf.Bite }, MoveSpeed = 5.2f,
            Mods = Undead, Look = "skeleton", Variants = 6,
        };

        public static readonly EnemyKind Shambler = new EnemyKind
        {
            Id = "shambler", Name = "Shambler", Books = new[] { Shelf.Bite }, MoveSpeed = 4.6f, Health = 0.5f, Damage = 0.42f,
            Mods = Undead, Look = "skeleton", Variants = 6, Scale = 0.94f,
        };

        public static readonly EnemyKind BoneArcher = new EnemyKind
        {
            Id = "bone-archer", Name = "Bone Archer", Books = new[] { Shelf.Shot }, Weapon = Tags.Bow | Tags.TwoHanded, MoveSpeed = 5.2f,
            KeepsAway = true, AggroRadius = 30f, Health = 0.55f, Damage = 0.65f, Mods = Undead, Look = "skeleton:bow", Variants = 4,
        };

        public static readonly EnemyKind GraveKnight = new EnemyKind
        {
            Id = "grave-knight", Name = "Grave Knight", Books = new[] { Shelf.Maul, Shelf.Strike }, MoveSpeed = 5f, Health = 2.4f, Damage = 1.3f,
            Mods = Undead.Append(More(Stat.Armour, 60f)).ToArray(), Look = "revenant", Scale = 1.2f,
        };

        // What lives out where nobody goes. Quick, and in numbers, with something much worse at the head of them now and then.
        public static readonly EnemyKind Wolf = new EnemyKind
        {
            Id = "wolf", Name = "Wolf", Books = new[] { Shelf.Bite }, MoveSpeed = 9.2f, Health = 0.42f, Damage = 0.38f, AggroRadius = 24f,
            Mods = new[] { Inc(Stat.AttackSpeed, 20f), Resist(Tags.Cold, 20f) }, Look = "wolf", Variants = 6,
        };

        public static readonly EnemyKind DireWolf = new EnemyKind
        {
            Id = "dire-wolf", Name = "Dire Wolf", Books = new[] { Shelf.Bite }, MoveSpeed = 9.2f, Health = 1.9f, Damage = 1.2f, AggroRadius = 24f,
            Mods = new[] { Inc(Stat.AttackSpeed, 20f), Resist(Tags.Cold, 30f) }, Look = "direwolf", Variants = 4,
        };

        public static readonly EnemyKind Boar = new EnemyKind
        {
            Id = "boar", Name = "Wild Boar", Books = new[] { Shelf.Bite }, MoveSpeed = 8f, Health = 0.8f, Damage = 0.75f, AggroRadius = 17f,
            Mods = new[] { More(Stat.Armour, 40f), Flat(Stat.PhysicalReduction, 8f) }, Look = "boar", Variants = 6,
        };

        public static readonly EnemyKind Bear = new EnemyKind
        {
            Id = "bear", Name = "Cave Bear", Books = new[] { Shelf.Maul, Shelf.Bite }, MoveSpeed = 6.6f, Health = 3f, Damage = 1.5f,
            Mods = new[] { More(Stat.Armour, 50f), Inc(Stat.AilmentTaken, -30f) }, Look = "bear", Variants = 4,
        };

        // The ones who come to the land's powers with nothing yet but a blade and a good opinion of themselves.
        private static EnemyKind Acolyte(string id, string name, DamageType element, Tags resist, string look) => new EnemyKind
        {
            Id = id, Name = name, Books = new[] { Shelf.Strike }, MoveSpeed = 7f, Health = 0.5f, Damage = 0.42f,
            Mods = new[] { Convert(DamageType.Physical, element, 100f), Resist(resist, 40f) }, Look = look,
        };

        public static readonly EnemyKind EmberAcolyte = Acolyte("ember-acolyte", "Ember Acolyte", DamageType.Fire, Tags.Fire, "acolyte:Fire");
        public static readonly EnemyKind RimeAcolyte = Acolyte("rime-acolyte", "Rime Acolyte", DamageType.Cold, Tags.Cold, "acolyte:Cold");
        public static readonly EnemyKind StormAcolyte = Acolyte("storm-acolyte", "Storm Acolyte", DamageType.Lightning, Tags.Lightning, "acolyte:Lightning");
        public static readonly EnemyKind MireAcolyte = Acolyte("mire-acolyte", "Mire Acolyte", DamageType.Poison, Tags.Poison, "acolyte:Poison");

        public static readonly EnemyKind Firebrand = new EnemyKind
        {
            Id = "firebrand", Name = "Firebrand", Weapon = Tags.Wand | Tags.OneHanded, Books = new[] { Shelf.Immolate, Shelf.Firebolt }, KeepsAway = true, AggroRadius = 26f,
            Health = 0.8f, Mods = new[] { Resist(Tags.Fire, 40f) }, Look = "robed:Fire",
        };

        public static readonly EnemyKind Rimewitch = new EnemyKind
        {
            Id = "rimewitch", Name = "Rimewitch", Weapon = Tags.Wand | Tags.OneHanded, Books = new[] { Shelf.FrostNova, Shelf.FrostShards }, KeepsAway = true, AggroRadius = 26f,
            Health = 0.8f, Mods = new[] { Resist(Tags.Cold, 40f) }, Look = "robed:Cold",
        };

        public static readonly EnemyKind Stormcaller = new EnemyKind
        {
            Id = "stormcaller", Name = "Stormcaller", Weapon = Tags.Wand | Tags.OneHanded, Books = new[] { Shelf.Spark }, KeepsAway = true, AggroRadius = 26f,
            Health = 0.75f, Mods = new[] { Resist(Tags.Lightning, 40f) }, Look = "robed:Lightning",
        };

        public static readonly EnemyKind Bogseer = new EnemyKind
        {
            Id = "bogseer", Name = "Bogseer", Weapon = Tags.Wand | Tags.OneHanded, Books = new[] { Shelf.Miasma, Shelf.VenomSpit }, KeepsAway = true, AggroRadius = 26f,
            Health = 0.85f, Mods = new[] { Resist(Tags.Poison, 60f) }, Look = "robed:Poison",
        };

        public static readonly EnemyKind Mourner = new EnemyKind
        {
            Id = "mourner", Name = "Hollow Mourner", Weapon = Tags.Wand | Tags.OneHanded, Books = new[] { Shelf.Unmaking, Shelf.ArcaneDart }, KeepsAway = true, MoveSpeed = 5f, AggroRadius = 26f,
            Health = 0.85f, Mods = new[] { Resist(Tags.Magic, 40f), Resist(Tags.Fire, -20f) }, Look = "robed:Magic",
        };

        public static readonly EnemyKind Warlord = new EnemyKind
        {
            Id = "warlord", Name = "Warlord", Books = new[] { Shelf.Whirl, Shelf.Maul, Shelf.Strike }, AggroRadius = 26f, LeashRadius = 120f,
            Mods = new[] { More(Stat.Armour, 100f), Inc(Stat.AilmentTaken, -50f) }, Look = "warlord",
        };

        public static readonly EnemyKind[] Casters = { Firebrand, Rimewitch, Stormcaller, Bogseer };

        // ---- The Saurian Reich, and what it has raised. Read LORE.md.

        // What the Reich's beasts have against the land's: tough hides, and blood bred hot.
        private static readonly Mod[] Hide = { Resist(Tags.Poison, 15f), Resist(Tags.Cold, -10f) };

        // What the Ossuary's failures have: rot that poisons what it bites, no blood to lose, and no love of fire.
        private static readonly Mod[] Rot = { GainAs(DamageType.Physical, DamageType.Poison, 40f), Chance(Tags.Venom, 35f), Resist(Tags.Poison, 60f), Resist(Tags.Fire, -25f), Flat(Stat.AilmentAvoid, 100f, Tags.Bleed) };

        // What each way of being met does, on top of what the animal is, so that the same beast is a different fight in harness, wild, as bones, rotting and as a ghost. A tyrant has none of
        // these, as a land's boss is its own fight.

        // Bare bone: what flies goes between its ribs, and there's nothing to it but what holds it together, so what stops it stops it for longer. Arrows are the wrong thing to bring, and a hammer the right one.
        private static readonly Mod[] BareBones = Undead.Append(More(Stat.DamageTaken, -35f, Tags.Projectile)).Append(Inc(Stat.AilmentTaken, 50f, Tags.Stun)).ToArray();

        // The glass of bile on a rotting beast's back breaks as it falls, and what kept it going lies where it lay (Shelf.BilePool): somewhere not to stand, and a reason not to fight a pack of them in a heap.
        private static Mod[] Bile(BookPlan spilled) => Rot.Append(Trigger(TriggerEvent.Fell, spilled)).ToArray();

        private static readonly Mod[] Rotten = Bile(Shelf.BilePool);        // Something a person's size, or a raptor's.
        private static readonly Mod[] RottenGreat = Bile(Shelf.BileFlood);  // What leads them, which carries more of it.

        // What swarms is little by itself and a great deal together: a skitter bites harder at whoever's Surrounded.
        private static readonly Mod Swarming = More(Stat.Damage, 60f).Against(Condition.Surrounded);

        // A reaper's scythes are at the ends of its arms, so it's worst at the edge of its reach, and whoever gets in under them takes the least of it.
        private static readonly Mod ScytheTips = More(Stat.Damage, 50f).Against(Condition.AtRange);

        // What the Reich hunts with is taught on whatever runs: its coursers and its great raptors are worst for whoever's Moving, and a hunt is best stood up to.
        private static readonly Mod RunsDown = More(Stat.Damage, 40f).Against(Condition.Moving);

        // A wader is in its element standing in water, and is best fought on dry ground.
        private static readonly Mod[] Fisher = { Inc(Stat.MoveSpeed, 25f).While(Condition.InWater), More(Stat.Damage, 30f).While(Condition.InWater) };

        /// <summary>The Reich's hounds. They hunt in packs ahead of its soldiers, and most of a war-band is these.</summary>
        public static readonly EnemyKind WarRaptor = new EnemyKind
        {
            Id = "war-raptor", Name = "War Raptor", Books = new[] { Shelf.RakingBite }, MoveSpeed = 9.6f, Health = 0.45f, Damage = 0.42f, AggroRadius = 24f,
            Mods = Hide.Append(Inc(Stat.AttackSpeed, 20f)).ToArray(), Look = "dino:raptor.reich.", Variants = 6, Bulk = 1.25f,
        };

        /// <summary>Raptors the Reich lost, breeding in the deep woods. Life finds a way out of any pen.</summary>
        public static readonly EnemyKind Raptor = new EnemyKind
        {
            Id = "raptor", Name = "Raptor", Books = new[] { Shelf.RakingBite }, MoveSpeed = 9.6f, Health = 0.45f, Damage = 0.42f, AggroRadius = 24f,
            // Nobody has taught a wild one anything: it goes for whatever's bleeding, as a pack does, and the more of them have drawn blood the worse each is.
            Mods = Hide.Append(Inc(Stat.AttackSpeed, 20f)).Append(More(Stat.Damage, 30f).Against(Condition.Bleeding)).ToArray(), Look = "dino:raptor.", Variants = 6, Bulk = 1.25f,
        };

        /// <summary>Little scavengers in their dozens, wherever anything has died, which is everywhere the Reich has been.</summary>
        public static readonly EnemyKind Skitter = new EnemyKind
        {
            Id = "skitter", Name = "Skitter", Books = new[] { Shelf.HungryBite }, MoveSpeed = 10.4f, Health = 0.22f, Damage = 0.22f, AggroRadius = 20f,
            Mods = new[] { Inc(Stat.AttackSpeed, 30f), Inc(Stat.Evasion, 40f), Swarming }, Look = "dino:skitter.", Variants = 5, Bulk = 0.6f,
        };

        /// <summary>Venom the blood-doctors bred into it, spat from behind the pack. Its bite comes first in its list, as that's for whoever gets in among them: its spit never rests, so anything after it would never be reached for.</summary>
        public static readonly EnemyKind Spitter = new EnemyKind
        {
            Id = "spitter", Name = "Spitter", Books = new[] { Shelf.RakingBite, Shelf.VenomSpit }, MoveSpeed = 8f, Health = 0.6f, Damage = 0.6f, AggroRadius = 28f, KeepsAway = true,
            Mods = new[] { Resist(Tags.Poison, 50f) }, Look = "dino:spitter.reich.", Variants = 4, Bulk = 1.3f,
        };

        public static readonly EnemyKind FeralSpitter = new EnemyKind
        {
            Id = "feral-spitter", Name = "Wild Spitter", Books = new[] { Shelf.RakingBite, Shelf.VenomSpit }, MoveSpeed = 8f, Health = 0.6f, Damage = 0.6f, AggroRadius = 28f, KeepsAway = true,
            // A wild one spits to bring something down, not on a handler's word: once its venom has hold of someone, everything it does to them is worse.
            Mods = new[] { Resist(Tags.Poison, 50f), More(Stat.Damage, 40f).Against(Condition.Envenomed) }, Look = "dino:spitter.", Variants = 4, Bulk = 1.3f,
        };

        /// <summary>What leads a war-band when raptors won't do: a heavy-skulled killer with horns over its eyes.</summary>
        public static readonly EnemyKind Ravager = new EnemyKind
        {
            Id = "ravager", Name = "Ravager", Books = new[] { Shelf.Stomp, Shelf.RakingBite }, MoveSpeed = 7.6f, Health = 2.3f, Damage = 1.3f, AggroRadius = 26f,
            Mods = Hide.Append(More(Stat.Armour, 30f)).Append(Inc(Stat.AilmentTaken, -20f)).ToArray(), Look = "dino:ravager.reich.", Variants = 4, Scale = 0.85f, Bulk = 1.9f,
        };

        /// <summary>One that got away from its handlers and leads what else did.</summary>
        public static readonly EnemyKind WildRavager = new EnemyKind
        {
            Id = "wild-ravager", Name = "Wild Ravager", Books = new[] { Shelf.Stomp, Shelf.RakingBite }, MoveSpeed = 7.8f, Health = 2.1f, Damage = 1.3f, AggroRadius = 26f,
            // Nothing has ever made one of these back down. Hurt badly, it's at its worst, so what's started with it is best finished.
            Mods = Hide.Append(More(Stat.Armour, 20f)).Append(More(Stat.Damage, 35f).While(Condition.LowHealth)).Append(Inc(Stat.AttackSpeed, 25f).While(Condition.LowHealth)).ToArray(), Look = "dino:ravager.", Variants = 4, Scale = 0.85f, Bulk = 1.9f,
        };

        /// <summary>Driven at walls, and walls go.</summary>
        public static readonly EnemyKind Hornback = new EnemyKind
        {
            Id = "hornback", Name = "Hornback", Books = new[] { Shelf.HornCharge, Shelf.Stomp, Shelf.Gore }, MoveSpeed = 7.2f, Health = 2.8f, Damage = 1.35f, AggroRadius = 22f,
            Mods = Hide.Append(More(Stat.Armour, 60f)).Append(Flat(Stat.PhysicalReduction, 8f)).ToArray(), Look = "dino:hornback.reich.", Variants = 4, Scale = 0.85f, Bulk = 2.1f,
        };

        /// <summary>
        /// A commander's war-beast, and what holds a land for the Reich: the greatest hunter there is, grown to twice a person's height at the hip and dressed in the Reich's regalia. A commander's
        /// tyrant is only ever met as what a land's freedom depends on, and it fights like it: a roar that stops whoever hears it, a tail that clears the ground round it, a stamp, and jaws that go
        /// through iron. The only other there is is one nobody claimed (<see cref="WildTyrant"/>).
        /// </summary>
        public static readonly EnemyKind Tyrant = new EnemyKind
        {
            Id = "tyrant", Name = "Tyrant", Books = new[] { Shelf.Roar, Shelf.TailSweep, Shelf.Stomp, Shelf.CrushingJaws }, MoveSpeed = 7f, Health = 1.5f, Damage = 1.25f, AggroRadius = 32f, LeashRadius = 120f,
            Mods = Hide.Append(More(Stat.Armour, 80f)).Append(Inc(Stat.AilmentTaken, -50f)).ToArray(), Look = "dino:tyrant.reich.", Scale = 0.95f, Bulk = 1.7f,
        };

        /// <summary>A tyrant the Ossuary raised straight out of the rock it lay in, with nothing on its bones but the light that moves them.</summary>
        public static readonly EnemyKind BoneTyrant = new EnemyKind
        {
            Id = "bone-tyrant", Name = "Bone Tyrant", Books = new[] { Shelf.Roar, Shelf.TailSweep, Shelf.Stomp, Shelf.CrushingJaws }, MoveSpeed = 6.2f, Health = 1.25f, Damage = 1.2f, AggroRadius = 30f, LeashRadius = 120f,
            Mods = Undead.Append(More(Stat.Armour, 60f)).Append(Inc(Stat.AilmentTaken, -50f)).ToArray(), Look = "dino:tyrant.bones.", Scale = 0.9f, Bulk = 1.5f,
        };

        /// <summary>A tyrant that hatched dying, and wasn't let die.</summary>
        public static readonly EnemyKind RottingTyrant = new EnemyKind
        {
            Id = "rotting-tyrant", Name = "Rotting Tyrant", Books = new[] { Shelf.Roar, Shelf.TailSweep, Shelf.Stomp, Shelf.CrushingJaws }, MoveSpeed = 5.8f, Health = 1.5f, Damage = 1.1f, AggroRadius = 30f, LeashRadius = 120f,
            Mods = Rot.Append(More(Stat.Armour, 40f)).Append(Inc(Stat.AilmentTaken, -50f)).ToArray(), Look = "dino:tyrant.rot.", Scale = 0.9f, Bulk = 1.7f,
        };

        public static readonly EnemyKind BoneRaptor = new EnemyKind
        {
            Id = "bone-raptor", Name = "Bone Raptor", Books = new[] { Shelf.RakingBite }, MoveSpeed = 8.2f, Health = 0.5f, Damage = 0.45f,
            Mods = BareBones, Look = "dino:raptor.bones.", Variants = 4, Bulk = 1.2f,
        };

        public static readonly EnemyKind BoneRavager = new EnemyKind
        {
            Id = "bone-ravager", Name = "Bone Ravager", Books = new[] { Shelf.Stomp, Shelf.RakingBite }, MoveSpeed = 6.8f, Health = 2.4f, Damage = 1.3f,
            Mods = BareBones.Append(More(Stat.Armour, 40f)).ToArray(), Look = "dino:ravager.bones.", Variants = 3, Scale = 0.85f, Bulk = 1.8f,
        };

        public static readonly EnemyKind RottingRaptor = new EnemyKind
        {
            Id = "rotting-raptor", Name = "Rotting Raptor", Books = new[] { Shelf.RakingBite }, MoveSpeed = 6.2f, Health = 0.7f, Damage = 0.45f,
            Mods = Rotten, Look = "dino:raptor.rot.", Variants = 4, Bulk = 1.25f,
        };

        public static readonly EnemyKind RottingHornback = new EnemyKind
        {
            Id = "rotting-hornback", Name = "Rotting Hornback", Books = new[] { Shelf.HornCharge, Shelf.Stomp, Shelf.Gore }, MoveSpeed = 5.8f, Health = 3f, Damage = 1.3f,
            Mods = RottenGreat.Append(More(Stat.Armour, 40f)).ToArray(), Look = "dino:hornback.rot.", Variants = 3, Scale = 0.85f, Bulk = 2.1f,
        };

        /// <summary>The great raptor, kept by the Reich to run down what its raptors can't. Its tail is a rod, and it swings it before it rakes.</summary>
        public static readonly EnemyKind WarRipper = new EnemyKind
        {
            Id = "war-ripper", Name = "War Ripper", Books = new[] { Shelf.Pounce, Shelf.TailSweep, Shelf.RakingBite }, MoveSpeed = 9f, Health = 1.35f, Damage = 0.95f, AggroRadius = 26f,
            Mods = Hide.Append(Inc(Stat.AttackSpeed, 15f)).Append(RunsDown).ToArray(), Look = "dino:ripper.reich.", Variants = 3, Scale = 0.9f, Bulk = 1.75f,
        };

        /// <summary>Great raptors that got out of the pens, leading what else did through the deep woods.</summary>
        public static readonly EnemyKind Ripper = new EnemyKind
        {
            Id = "ripper", Name = "Ripper", Books = new[] { Shelf.Pounce, Shelf.TailSweep, Shelf.RakingBite }, MoveSpeed = 9f, Health = 1.3f, Damage = 0.95f, AggroRadius = 26f,
            // A raptor still, and a wild one: it goes for whatever's bleeding, as the pack it leads does.
            Mods = Hide.Append(Inc(Stat.AttackSpeed, 15f)).Append(More(Stat.Damage, 30f).Against(Condition.Bleeding)).ToArray(), Look = "dino:ripper.", Variants = 3, Scale = 0.9f, Bulk = 1.75f,
        };

        /// <summary>A long-snouted fisher of the fens, grown for the wet country and lost to it. It takes whatever comes to the water's edge, from further off than it looks.</summary>
        public static readonly EnemyKind Wader = new EnemyKind
        {
            Id = "wader", Name = "Wader", Books = new[] { Shelf.Lunge, Shelf.TailSweep, Shelf.CrushingJaws }, MoveSpeed = 7.4f, Health = 2f, Damage = 1.15f, AggroRadius = 24f,
            Mods = Hide.Concat(Fisher).Append(More(Stat.Armour, 20f)).ToArray(), Look = "dino:wader.", Variants = 3, Scale = 0.85f, Bulk = 1.8f,
        };

        /// <summary>The Ossuary dumps its failures where the land is wet enough to take them, and a wader that hatched dying is kept going there with the rest.</summary>
        public static readonly EnemyKind RottingWader = new EnemyKind
        {
            Id = "rotting-wader", Name = "Rotting Wader", Books = new[] { Shelf.Lunge, Shelf.TailSweep, Shelf.CrushingJaws }, MoveSpeed = 6f, Health = 2.2f, Damage = 1.1f,
            Mods = RottenGreat.Concat(Fisher).Append(More(Stat.Armour, 20f)).ToArray(), Look = "dino:wader.rot.", Variants = 2, Scale = 0.85f, Bulk = 1.8f,
        };

        /// <summary>Bred for the spikes down its back and tail, and driven ahead of a war-band where a hornback won't go.</summary>
        public static readonly EnemyKind Spikeback = new EnemyKind
        {
            Id = "spikeback", Name = "Spikeback", Books = new[] { Shelf.TailSweep, Shelf.Stomp, Shelf.Bite }, MoveSpeed = 6.8f, Health = 2.6f, Damage = 1.25f, AggroRadius = 22f,
            // What it was bred for: whoever strikes it from within reach of its spikes is struck by them.
            Mods = Hide.Append(More(Stat.Armour, 60f)).Append(Inc(Stat.AilmentTaken, -20f)).Append(Flat(Stat.Reflect, Spikes)).ToArray(), Look = "dino:spikeback.reich.", Variants = 3, Scale = 0.85f, Bulk = 1.9f,
        };

        // What the Ossuary called up in light: cold to the touch and colder to be touched by, no blood to lose, hard to land a blow on, and nothing for venom to work on. Storms go through it.
        private static readonly Mod[] Spectral = { Resist(Tags.Cold, 50f), Resist(Tags.Poison, 60f), Resist(Tags.Lightning, -25f), Inc(Stat.Evasion, 60f), GainAs(DamageType.Physical, DamageType.Cold, 40f), Flat(Stat.AilmentAvoid, 100f, Tags.Bleed) };

        // Blows go through a ghost, and a ghost's go through what's worn against them: iron is no help, and not being where it bites is. What was on a ghost before this made it hardly
        // harder to lay a blade on than the animal it had been (a blow in eight missed the one and a blow in six the other, at the tenth level), so this is as much Evasion again: about four
        // blows in ten go through one for someone with ordinary Accuracy, more for someone with none, and no Spell does, as nothing evades those.
        private static readonly Mod[] Ghostly = Spectral.Append(More(Stat.Evasion, 100f)).Append(Flat(Stat.ArmourIgnore, 40f)).ToArray();

        /// <summary>The Institute's eight-legged hunter, lost from its pens into the deep woods, where it waits for whatever comes under it and spits before it bites.</summary>
        public static readonly EnemyKind Weaver = new EnemyKind
        {
            Id = "weaver", Name = "Weaver", Books = new[] { Shelf.WeaverSpit, Shelf.HungryBite }, MoveSpeed = 9.4f, Health = 0.75f, Damage = 0.62f, AggroRadius = 26f,
            // Its venom is for holding what it's caught: each dose of it slows whoever carries it.
            Mods = new[] { Resist(Tags.Poison, 60f), Chance(Tags.Venom, 30f), Inc(Stat.AttackSpeed, 15f), Rules(Rule.VenomSlows) }, Look = "dino:weaver.", Variants = 3, Bulk = 1.6f,
        };

        /// <summary>The Institute's answer to the snow: a tyrant's kin in a shaggy coat, driven by the Reich where its cold-blooded beasts die.</summary>
        public static readonly EnemyKind Shagback = new EnemyKind
        {
            Id = "shagback", Name = "Shagback", Books = new[] { Shelf.Stomp, Shelf.CrushingJaws }, MoveSpeed = 7.4f, Health = 2.6f, Damage = 1.35f, AggroRadius = 26f,
            Mods = new[] { Resist(Tags.Cold, 50f), Resist(Tags.Poison, 15f), More(Stat.Armour, 30f), Inc(Stat.AilmentTaken, -20f) }, Look = "dino:shagback.reich.", Variants = 3, Scale = 0.85f, Bulk = 1.9f,
        };

        /// <summary>Shagbacks that got away into the high pines, where nothing else that big can live.</summary>
        public static readonly EnemyKind WildShagback = new EnemyKind
        {
            Id = "wild-shagback", Name = "Wild Shagback", Books = new[] { Shelf.Stomp, Shelf.CrushingJaws }, MoveSpeed = 7.6f, Health = 2.4f, Damage = 1.35f, AggroRadius = 26f,
            // Its coat and its blood keep it going where nothing else lives: left alone it mends, so it's brought down in one go or not at all.
            Mods = new[] { Resist(Tags.Cold, 50f), Resist(Tags.Poison, 15f), More(Stat.Armour, 20f), Flat(Stat.HealthRegenPercent, 2f) }, Look = "dino:shagback.", Variants = 3, Scale = 0.85f, Bulk = 1.9f,
        };

        /// <summary>A grazer the Institute grew into a war-beast: arms as long as it is tall, with a scythe for a claw on each, which the Reich sets on lines of spears.</summary>
        public static readonly EnemyKind Reaper = new EnemyKind
        {
            Id = "reaper", Name = "Reaper", Books = new[] { Shelf.Reap, Shelf.ScytheClaw }, MoveSpeed = 7.6f, Health = 2.2f, Damage = 1.2f, AggroRadius = 24f,
            Mods = Hide.Append(Inc(Stat.AttackSpeed, 10f)).Append(Flat(Stat.CritChance, 8f)).Append(ScytheTips).ToArray(), Look = "dino:reaper.reich.", Variants = 3, Scale = 0.9f, Bulk = 1.7f,
        };

        /// <summary>Flocks of long-legged runners, out of the pens and faster than anything in the open country, that take whatever they can catch and are gone.</summary>
        public static readonly EnemyKind Strider = new EnemyKind
        {
            Id = "strider", Name = "Strider", Books = new[] { Shelf.HungryBite }, MoveSpeed = 9.9f, Health = 0.4f, Damage = 0.45f, AggroRadius = 28f, Skirmishes = true,
            Mods = new[] { Inc(Stat.Evasion, 50f), Inc(Stat.AttackSpeed, 20f) }, Look = "dino:strider.", Variants = 3, Bulk = 1.1f,
        };

        /// <summary>Low and broad as a cart, its back a shell of bone and its tail a club. The Reich drives them into lines of spears, which break.</summary>
        public static readonly EnemyKind Clubtail = new EnemyKind
        {
            Id = "clubtail", Name = "Clubtail", Books = new[] { Shelf.TailClub, Shelf.Gore }, MoveSpeed = 6.4f, Health = 3.2f, Damage = 1.4f, AggroRadius = 20f,
            Mods = Hide.Append(More(Stat.Armour, 90f)).Append(Flat(Stat.PhysicalReduction, 12f)).Append(Inc(Stat.AilmentTaken, -25f)).ToArray(), Look = "dino:clubtail.reich.", Variants = 3, Scale = 0.85f, Bulk = 2.2f,
        };

        /// <summary>A grazer on two legs with a dome of bone for a skull, which it lowers and runs at whatever's in its way. Herds of them got out, and keep to the open hills.</summary>
        public static readonly EnemyKind Domehead = new EnemyKind
        {
            Id = "domehead", Name = "Domehead", Books = new[] { Shelf.Ram, Shelf.Gore }, MoveSpeed = 8.6f, Health = 1.2f, Damage = 0.9f, AggroRadius = 22f,
            Mods = new[] { More(Stat.Armour, 30f), Inc(Stat.AilmentTaken, -15f) }, Look = "dino:domehead.", Variants = 3, Bulk = 1.3f,
        };

        /// <summary>What the Ossuary called up where a raptor's bones were too far gone to stand: its light alone, round what's left of them, still hunting.</summary>
        public static readonly EnemyKind GhostRaptor = new EnemyKind
        {
            Id = "ghost-raptor", Name = "Ghost Raptor", Books = new[] { Shelf.RakingBite }, MoveSpeed = 9f, Health = 0.45f, Damage = 0.45f, AggroRadius = 24f,
            Mods = Ghostly, Look = "dino:raptor.ghost.", Variants = 3, Bulk = 1.2f,
        };

        public static readonly EnemyKind GhostRavager = new EnemyKind
        {
            Id = "ghost-ravager", Name = "Ghost Ravager", Books = new[] { Shelf.Stomp, Shelf.RakingBite }, MoveSpeed = 7.2f, Health = 2f, Damage = 1.3f, AggroRadius = 26f,
            Mods = Ghostly.Append(More(Stat.Armour, 20f)).ToArray(), Look = "dino:ravager.ghost.", Variants = 2, Scale = 0.85f, Bulk = 1.8f,
        };

        public static readonly EnemyKind GhostRipper = new EnemyKind
        {
            Id = "ghost-ripper", Name = "Ghost Ripper", Books = new[] { Shelf.Pounce, Shelf.TailSweep, Shelf.RakingBite }, MoveSpeed = 8.6f, Health = 1.2f, Damage = 0.95f, AggroRadius = 26f,
            Mods = Ghostly, Look = "dino:ripper.ghost.", Variants = 2, Scale = 0.9f, Bulk = 1.7f,
        };

        public static readonly EnemyKind GhostWeaver = new EnemyKind
        {
            Id = "ghost-weaver", Name = "Ghost Weaver", Books = new[] { Shelf.HungryBite }, MoveSpeed = 9f, Health = 0.6f, Damage = 0.55f, AggroRadius = 24f,
            // Its venom held what it bit, and its cold does now: nothing a ghost does slows anyone as this does.
            Mods = Ghostly.Append(Inc(Stat.AilmentEffect, 100f, Tags.Chill)).ToArray(), Look = "dino:weaver.ghost.", Variants = 2, Bulk = 1.6f,
        };

        /// <summary>A tyrant the Ossuary called back in light where its bones lay too broken to raise: what holds a land now and then, and the coldest thing in it.</summary>
        public static readonly EnemyKind GhostTyrant = new EnemyKind
        {
            Id = "ghost-tyrant", Name = "Ghost Tyrant", Books = new[] { Shelf.Roar, Shelf.TailSweep, Shelf.Stomp, Shelf.CrushingJaws }, MoveSpeed = 6.8f, Health = 1.3f, Damage = 1.25f, AggroRadius = 30f, LeashRadius = 120f,
            Mods = Spectral.Append(Inc(Stat.AilmentTaken, -50f)).ToArray(), Look = "dino:tyrant.ghost.", Scale = 0.95f, Bulk = 1.6f,
        };

        public static readonly EnemyKind BoneHornback = new EnemyKind
        {
            Id = "bone-hornback", Name = "Bone Hornback", Books = new[] { Shelf.HornCharge, Shelf.Stomp, Shelf.Gore }, MoveSpeed = 6.2f, Health = 2.6f, Damage = 1.3f,
            Mods = BareBones.Append(More(Stat.Armour, 50f)).ToArray(), Look = "dino:hornback.bones.", Variants = 2, Scale = 0.85f, Bulk = 2f,
        };

        public static readonly EnemyKind BoneSpikeback = new EnemyKind
        {
            Id = "bone-spikeback", Name = "Bone Spikeback", Books = new[] { Shelf.TailSweep, Shelf.Stomp, Shelf.Bite }, MoveSpeed = 6.2f, Health = 2.4f, Damage = 1.25f,
            Mods = BareBones.Append(More(Stat.Armour, 40f)).Append(Flat(Stat.Reflect, Spikes)).ToArray(), Look = "dino:spikeback.bones.", Variants = 2, Scale = 0.85f, Bulk = 1.8f,
        };

        public static readonly EnemyKind BoneWeaver = new EnemyKind
        {
            Id = "bone-weaver", Name = "Bone Weaver", Books = new[] { Shelf.HungryBite }, MoveSpeed = 8.4f, Health = 0.65f, Damage = 0.55f,
            // It lies as a heap of bones until something comes over it, and its first bite is the one that counts: once anything has touched it, it's only bones.
            Mods = BareBones.Append(More(Stat.Damage, 60f).While(Condition.FullHealth)).ToArray(), Look = "dino:weaver.bones.", Variants = 2, Bulk = 1.5f,
        };

        public static readonly EnemyKind RottingRavager = new EnemyKind
        {
            Id = "rotting-ravager", Name = "Rotting Ravager", Books = new[] { Shelf.Stomp, Shelf.RakingBite }, MoveSpeed = 6.2f, Health = 2.5f, Damage = 1.25f,
            Mods = RottenGreat.Append(More(Stat.Armour, 20f)).ToArray(), Look = "dino:ravager.rot.", Variants = 2, Scale = 0.85f, Bulk = 1.9f,
        };

        public static readonly EnemyKind RottingShagback = new EnemyKind
        {
            Id = "rotting-shagback", Name = "Rotting Shagback", Books = new[] { Shelf.Stomp, Shelf.CrushingJaws }, MoveSpeed = 6f, Health = 2.6f, Damage = 1.25f,
            Mods = RottenGreat.Append(More(Stat.Armour, 20f)).ToArray(), Look = "dino:shagback.rot.", Variants = 2, Scale = 0.85f, Bulk = 1.9f,
        };

        // ---- Every beast, every way it's met (LORE.md). Each of these is a species' own way of fighting with what its condition does to it, and something of its own on top, so that no two are
        // the same fight and each has a reason to be in the pack it's in (Warbands). The same seed is the same animal, so a strider's bones are that strider's.

        // -- Skitters. Little by themselves and a great deal together: all of them bite harder at whoever's Surrounded.

        /// <summary>The pens' ratters, in a collar and a strap: the Reich looses them by the dozen ahead of a hunt to worry whatever it's after. Every bite opens something, and what bleeds is easier to run down.</summary>
        public static readonly EnemyKind WarSkitter = new EnemyKind
        {
            Id = "war-skitter", Name = "War Skitter", Books = new[] { Shelf.HungryBite }, MoveSpeed = 10.4f, Health = 0.22f, Damage = 0.2f, AggroRadius = 22f,
            Mods = Hide.Append(Inc(Stat.AttackSpeed, 30f)).Append(Inc(Stat.Evasion, 40f)).Append(Swarming).Append(Chance(Tags.Bleed, 80f)).ToArray(), Look = "dino:skitter.reich.", Variants = 3, Bulk = 0.6f,
        };

        /// <summary>The Ossuary raises even these, a pit of them at a time, as they cost it nothing. One light moves the lot: when one falls, what moved it goes to the rest, so they're best put down together.</summary>
        public static readonly EnemyKind BoneSkitter = new EnemyKind
        {
            Id = "bone-skitter", Name = "Bone Skitter", Books = new[] { Shelf.HungryBite }, MoveSpeed = 9f, Health = 0.2f, Damage = 0.22f, AggroRadius = 20f,
            Mods = BareBones.Append(Inc(Stat.AttackSpeed, 30f)).Append(Swarming).Append(Rules(Rule.RalliesOnDeath)).ToArray(), Look = "dino:skitter.bones.", Variants = 3, Bulk = 0.6f,
        };

        /// <summary>The Institute's smallest failures, tipped out by the barrow-load where the ground is wet. There's little bile in one, and it all comes out where it drops.</summary>
        public static readonly EnemyKind RottingSkitter = new EnemyKind
        {
            Id = "rotting-skitter", Name = "Rotting Skitter", Books = new[] { Shelf.HungryBite }, MoveSpeed = 7f, Health = 0.3f, Damage = 0.2f, AggroRadius = 18f,
            Mods = Bile(Shelf.BileSplash).Append(Swarming).ToArray(), Look = "dino:skitter.rot.", Variants = 3, Bulk = 0.6f,
        };

        /// <summary>A whole brood's light, where a pit of them was dug through. Small, quick and hardly there: a blade finds one of them about as often as it finds smoke.</summary>
        public static readonly EnemyKind GhostSkitter = new EnemyKind
        {
            Id = "ghost-skitter", Name = "Ghost Skitter", Books = new[] { Shelf.HungryBite }, MoveSpeed = 10.8f, Health = 0.18f, Damage = 0.22f, AggroRadius = 20f,
            Mods = Ghostly.Append(Inc(Stat.AttackSpeed, 30f)).Append(Inc(Stat.Evasion, 40f)).Append(Swarming).ToArray(), Look = "dino:skitter.ghost.", Variants = 2, Bulk = 0.6f,
        };

        // -- Spitters. What each has left to spit.

        /// <summary>There's no venom left in a skull, so it throws a mouthful of its own teeth, and the light grows them back.</summary>
        public static readonly EnemyKind BoneSpitter = new EnemyKind
        {
            Id = "bone-spitter", Name = "Bone Spitter", Books = new[] { Shelf.RakingBite, Shelf.SpatTeeth }, MoveSpeed = 7f, Health = 0.55f, Damage = 0.6f, AggroRadius = 28f, KeepsAway = true,
            Mods = BareBones, Look = "dino:spitter.bones.", Variants = 2, Bulk = 1.25f,
        };

        /// <summary>Its venom went bad with the rest of it, so it spits the bile it runs on, which lies where it lands. A pack of the rotting with one behind it leaves less and less ground to stand on.</summary>
        public static readonly EnemyKind RottingSpitter = new EnemyKind
        {
            Id = "rotting-spitter", Name = "Rotting Spitter", Books = new[] { Shelf.BileSpit, Shelf.VenomSpit }, MoveSpeed = 6f, Health = 0.8f, Damage = 0.55f, AggroRadius = 28f, KeepsAway = true,
            Mods = Rotten, Look = "dino:spitter.rot.", Variants = 2, Bulk = 1.3f,
        };

        /// <summary>It has only its own cold to spit, and the ground keeps it: whoever stands where it fell is slowed for the rest of them.</summary>
        public static readonly EnemyKind GhostSpitter = new EnemyKind
        {
            Id = "ghost-spitter", Name = "Ghost Spitter", Weapon = Tags.Wand | Tags.OneHanded, Books = new[] { Shelf.GraveCold, Shelf.Wisp }, MoveSpeed = 8f, Health = 0.5f, Damage = 0.6f, AggroRadius = 28f, KeepsAway = true,
            Mods = Ghostly, Look = "dino:spitter.ghost.", Variants = 2, Bulk = 1.25f,
        };

        // -- The tyrant nobody has.

        /// <summary>
        /// A tyrant no commander ever claimed: it broke its pen at the Institute before it was grown, and has lived on what else got out ever since. Not yet the size of a commander's, and nobody's. It's
        /// heard before it's seen, and its roar stops whoever hears it, as any tyrant's does.
        /// </summary>
        public static readonly EnemyKind WildTyrant = new EnemyKind
        {
            Id = "wild-tyrant", Name = "Wild Tyrant", Books = new[] { Shelf.Roar, Shelf.Stomp, Shelf.CrushingJaws }, MoveSpeed = 7.2f, Health = 2.9f, Damage = 1.4f, AggroRadius = 28f, LeashRadius = 80f,
            Mods = Hide.Append(More(Stat.Armour, 40f)).Append(Inc(Stat.AilmentTaken, -30f)).ToArray(), Look = "dino:tyrant.", Variants = 2, Scale = 0.6f, Bulk = 2.6f,
        };

        // -- Hornbacks charge, whatever's been done to them.

        /// <summary>The charge of one that was turned to stone mid-stride, called back in light: it comes through whatever's worn against it, and it's cold where it's been.</summary>
        public static readonly EnemyKind GhostHornback = new EnemyKind
        {
            Id = "ghost-hornback", Name = "Ghost Hornback", Books = new[] { Shelf.HornCharge, Shelf.Stomp, Shelf.Gore }, MoveSpeed = 7f, Health = 2.3f, Damage = 1.3f, AggroRadius = 24f,
            Mods = Ghostly.Append(More(Stat.Armour, 20f)).ToArray(), Look = "dino:hornback.ghost.", Variants = 2, Scale = 0.85f, Bulk = 2f,
        };

        // -- Rippers pounce.

        /// <summary>The great raptor's bones, and the one thing among the dead that crosses the ground between it and whoever's backing away from them.</summary>
        public static readonly EnemyKind BoneRipper = new EnemyKind
        {
            Id = "bone-ripper", Name = "Bone Ripper", Books = new[] { Shelf.Pounce, Shelf.TailSweep, Shelf.RakingBite }, MoveSpeed = 7.8f, Health = 1.4f, Damage = 0.95f, AggroRadius = 26f,
            Mods = BareBones, Look = "dino:ripper.bones.", Variants = 2, Scale = 0.9f, Bulk = 1.7f,
        };

        /// <summary>A great raptor that hatched dying. It's slow on its feet now and still pounces as it was grown to, with rot on its claw.</summary>
        public static readonly EnemyKind RottingRipper = new EnemyKind
        {
            Id = "rotting-ripper", Name = "Rotting Ripper", Books = new[] { Shelf.Pounce, Shelf.TailSweep, Shelf.RakingBite }, MoveSpeed = 6.4f, Health = 1.7f, Damage = 0.9f, AggroRadius = 24f,
            Mods = RottenGreat, Look = "dino:ripper.rot.", Variants = 2, Scale = 0.9f, Bulk = 1.75f,
        };

        // -- Waders, which are worst standing in water.

        /// <summary>The few the Reich kept hold of, chained at its fords and its fishing villages with an iron mask over the snout. It takes whoever comes to the water's edge, from further off than it looks.</summary>
        public static readonly EnemyKind WarWader = new EnemyKind
        {
            Id = "war-wader", Name = "War Wader", Books = new[] { Shelf.Lunge, Shelf.TailSweep, Shelf.CrushingJaws }, MoveSpeed = 7.4f, Health = 2.3f, Damage = 1.2f, AggroRadius = 26f,
            // With iron on its snout, what it reaches is stopped as often as not.
            Mods = Hide.Concat(Fisher).Append(More(Stat.Armour, 50f)).Append(Inc(Stat.AilmentTaken, -20f)).Append(Chance(Tags.Stun, 50f)).ToArray(), Look = "dino:wader.reich.", Variants = 3, Scale = 0.85f, Bulk = 1.8f,
        };

        /// <summary>A fisher's bones out of an old riverbed. The water means nothing to it now, and its reach is what it was.</summary>
        public static readonly EnemyKind BoneWader = new EnemyKind
        {
            Id = "bone-wader", Name = "Bone Wader", Books = new[] { Shelf.Lunge, Shelf.TailSweep, Shelf.CrushingJaws }, MoveSpeed = 6.6f, Health = 2.1f, Damage = 1.15f, AggroRadius = 24f,
            Mods = BareBones.Append(More(Stat.Armour, 20f)).ToArray(), Look = "dino:wader.bones.", Variants = 2, Scale = 0.85f, Bulk = 1.7f,
        };

        /// <summary>It's still fishing. What it was called up over was a river once, and it stands in whatever water there is as it did then.</summary>
        public static readonly EnemyKind GhostWader = new EnemyKind
        {
            Id = "ghost-wader", Name = "Ghost Wader", Books = new[] { Shelf.Lunge, Shelf.TailSweep, Shelf.CrushingJaws }, MoveSpeed = 7.2f, Health = 1.8f, Damage = 1.15f, AggroRadius = 24f,
            Mods = Ghostly.Concat(Fisher).ToArray(), Look = "dino:wader.ghost.", Variants = 2, Scale = 0.85f, Bulk = 1.7f,
        };

        // -- Spikebacks give back what's struck at them from within reach of their spikes.

        /// <summary>Its spikes outlasted its hide. What strikes it up close is struck back, and what's on them now is rot.</summary>
        public static readonly EnemyKind RottingSpikeback = new EnemyKind
        {
            Id = "rotting-spikeback", Name = "Rotting Spikeback", Books = new[] { Shelf.TailSweep, Shelf.Stomp, Shelf.Bite }, MoveSpeed = 5.6f, Health = 2.9f, Damage = 1.2f,
            Mods = RottenGreat.Append(More(Stat.Armour, 30f)).Append(Flat(Stat.Reflect, Spikes)).ToArray(), Look = "dino:spikeback.rot.", Variants = 2, Scale = 0.85f, Bulk = 1.9f,
        };

        /// <summary>Spikes of light, that answer every blow from however far off it came: an arrow or a spell is given back as a sword is.</summary>
        public static readonly EnemyKind GhostSpikeback = new EnemyKind
        {
            Id = "ghost-spikeback", Name = "Ghost Spikeback", Books = new[] { Shelf.TailSweep, Shelf.Stomp, Shelf.Bite }, MoveSpeed = 6.6f, Health = 2.2f, Damage = 1.25f, AggroRadius = 22f,
            Mods = Ghostly.Append(Flat(Stat.Reflect, Spikes)).Append(Rules(Rule.ThornsAlways)).ToArray(), Look = "dino:spikeback.ghost.", Variants = 2, Scale = 0.85f, Bulk = 1.8f,
        };

        // -- Weavers, whose venom slows.

        /// <summary>The Institute didn't lose all of them. What it kept it keeps at its own doors, taught to lay its venom down under whoever comes and hold them there for the handlers.</summary>
        public static readonly EnemyKind WarWeaver = new EnemyKind
        {
            Id = "war-weaver", Name = "War Weaver", Books = new[] { Shelf.Snare, Shelf.HungryBite }, MoveSpeed = 9.2f, Health = 0.85f, Damage = 0.62f, AggroRadius = 28f,
            Mods = new[] { Resist(Tags.Poison, 60f), Chance(Tags.Venom, 30f), Inc(Stat.AttackSpeed, 15f), More(Stat.Armour, 20f), Rules(Rule.VenomSlows) }, Look = "dino:weaver.reich.", Variants = 2, Bulk = 1.6f,
        };

        /// <summary>What it was grown to carry has gone bad in it, and still holds what it gets into: the one rotting thing whose venom slows, so whoever it's spat on is still there when the rest of them come up.</summary>
        public static readonly EnemyKind RottingWeaver = new EnemyKind
        {
            Id = "rotting-weaver", Name = "Rotting Weaver", Books = new[] { Shelf.WeaverSpit, Shelf.HungryBite }, MoveSpeed = 6.4f, Health = 1f, Damage = 0.55f, AggroRadius = 24f,
            Mods = Rotten.Append(Rules(Rule.VenomSlows)).ToArray(), Look = "dino:weaver.rot.", Variants = 2, Bulk = 1.6f,
        };

        // -- Shagbacks, which the cold never left.

        /// <summary>What the frost kept and the Ossuary dug out of it, with the ice still in its ribs: the one thing among the bones whose bite is cold.</summary>
        public static readonly EnemyKind BoneShagback = new EnemyKind
        {
            Id = "bone-shagback", Name = "Bone Shagback", Books = new[] { Shelf.Stomp, Shelf.CrushingJaws }, MoveSpeed = 6.6f, Health = 2.6f, Damage = 1.3f, AggroRadius = 26f,
            Mods = BareBones.Append(More(Stat.Armour, 40f)).Append(Resist(Tags.Cold, 30f)).Append(GainAs(DamageType.Physical, DamageType.Cold, 40f)).ToArray(), Look = "dino:shagback.bones.", Variants = 2, Scale = 0.85f, Bulk = 1.8f,
        };

        /// <summary>It breathes out the cold it died in, and whoever that reaches stands frozen for whatever's with it, whose first blow breaks the ice. The coldest thing in the snow but a tyrant's ghost.</summary>
        public static readonly EnemyKind GhostShagback = new EnemyKind
        {
            Id = "ghost-shagback", Name = "Ghost Shagback", Books = new[] { Shelf.KillingFrost, Shelf.Stomp, Shelf.CrushingJaws }, MoveSpeed = 7.2f, Health = 2.2f, Damage = 1.35f, AggroRadius = 26f,
            Mods = Ghostly.Append(Inc(Stat.AilmentTaken, -20f)).ToArray(), Look = "dino:shagback.ghost.", Variants = 2, Scale = 0.85f, Bulk = 1.8f,
        };

        // -- Reapers, which are worst at the ends of their arms.

        /// <summary>One that got out and went back to grazing, at the edge of a flock. It's still a grazer, and it still has the arms the Institute gave it, for whatever comes too near what it's with.</summary>
        public static readonly EnemyKind WildReaper = new EnemyKind
        {
            Id = "wild-reaper", Name = "Wild Reaper", Books = new[] { Shelf.Reap, Shelf.ScytheClaw }, MoveSpeed = 7.4f, Health = 2.5f, Damage = 1.2f, AggroRadius = 20f,
            Mods = Hide.Append(ScytheTips).ToArray(), Look = "dino:reaper.", Variants = 3, Scale = 0.9f, Bulk = 1.7f,
        };

        /// <summary>Bones with a scythe on each hand. It comes for whoever's nearly gone, and what it does to them is worse than what it does to anyone else.</summary>
        public static readonly EnemyKind BoneReaper = new EnemyKind
        {
            Id = "bone-reaper", Name = "Bone Reaper", Books = new[] { Shelf.Reap, Shelf.ScytheClaw }, MoveSpeed = 6.8f, Health = 2.1f, Damage = 1.2f, AggroRadius = 24f,
            Mods = BareBones.Append(ScytheTips).Append(More(Stat.Damage, 40f).Against(Condition.LowHealth)).ToArray(), Look = "dino:reaper.bones.", Variants = 2, Scale = 0.9f, Bulk = 1.6f,
        };

        /// <summary>Its feathers went first. What it sweeps it poisons, and it sweeps a great deal.</summary>
        public static readonly EnemyKind RottingReaper = new EnemyKind
        {
            Id = "rotting-reaper", Name = "Rotting Reaper", Books = new[] { Shelf.Reap, Shelf.ScytheClaw }, MoveSpeed = 6f, Health = 2.5f, Damage = 1.1f, AggroRadius = 22f,
            Mods = RottenGreat.Append(ScytheTips).ToArray(), Look = "dino:reaper.rot.", Variants = 2, Scale = 0.9f, Bulk = 1.7f,
        };

        /// <summary>Scythes of light, which go all the way round it: there's no getting behind one, only in under its arms.</summary>
        public static readonly EnemyKind GhostReaper = new EnemyKind
        {
            Id = "ghost-reaper", Name = "Ghost Reaper", Books = new[] { Shelf.Reap, Shelf.ScytheClaw }, MoveSpeed = 7.4f, Health = 1.9f, Damage = 1.2f, AggroRadius = 24f,
            Mods = Ghostly.Append(ScytheTips).Append(Rules(Rule.FullCircle)).ToArray(), Look = "dino:reaper.ghost.", Variants = 2, Scale = 0.9f, Bulk = 1.6f,
        };

        // -- Striders, which strike and are gone.

        /// <summary>The Reich's coursers, in a collar and no more, as anything heavier slows them. A raptor can be got away from by someone who stops fighting, and these are what the Reich runs after whoever does.</summary>
        public static readonly EnemyKind WarStrider = new EnemyKind
        {
            Id = "war-strider", Name = "War Strider", Books = new[] { Shelf.HungryBite }, MoveSpeed = 10.8f, Health = 0.45f, Damage = 0.45f, AggroRadius = 30f, Skirmishes = true,
            Mods = Hide.Append(Inc(Stat.Evasion, 40f)).Append(Inc(Stat.AttackSpeed, 20f)).Append(RunsDown).ToArray(), Look = "dino:strider.reich.", Variants = 3, Bulk = 1.1f,
        };

        /// <summary>The quickest of the dead, by a long way: bone on long legs, in and out again before the rest of them have come up.</summary>
        public static readonly EnemyKind BoneStrider = new EnemyKind
        {
            Id = "bone-strider", Name = "Bone Strider", Books = new[] { Shelf.HungryBite }, MoveSpeed = 9.2f, Health = 0.42f, Damage = 0.45f, AggroRadius = 26f, Skirmishes = true,
            Mods = BareBones.Append(Inc(Stat.AttackSpeed, 20f)).ToArray(), Look = "dino:strider.bones.", Variants = 2, Bulk = 1.05f,
        };

        /// <summary>Its legs have outlasted the rest of it. It's the one rotting thing that can keep up with someone walking away from the others, and it stays on them once it has.</summary>
        public static readonly EnemyKind RottingStrider = new EnemyKind
        {
            Id = "rotting-strider", Name = "Rotting Strider", Books = new[] { Shelf.HungryBite }, MoveSpeed = 8.2f, Health = 0.6f, Damage = 0.42f, AggroRadius = 24f,
            Mods = Rotten.Append(Inc(Stat.AttackSpeed, 20f)).ToArray(), Look = "dino:strider.rot.", Variants = 2, Bulk = 1.1f,
        };

        /// <summary>It weighs nothing, and runs like it. Nothing in any land is faster, and nothing is harder to lay a blade on.</summary>
        public static readonly EnemyKind GhostStrider = new EnemyKind
        {
            Id = "ghost-strider", Name = "Ghost Strider", Books = new[] { Shelf.HungryBite }, MoveSpeed = 11.4f, Health = 0.36f, Damage = 0.45f, AggroRadius = 28f, Skirmishes = true,
            Mods = Ghostly.Append(Inc(Stat.AttackSpeed, 20f)).ToArray(), Look = "dino:strider.ghost.", Variants = 2, Bulk = 1.05f,
        };

        // -- Clubtails, and what's left of their shells.

        /// <summary>Its shell was bone already. No one blow takes much of it, however hard it's struck, so it has to be worn down.</summary>
        public static readonly EnemyKind BoneClubtail = new EnemyKind
        {
            Id = "bone-clubtail", Name = "Bone Clubtail", Books = new[] { Shelf.TailClub, Shelf.Gore }, MoveSpeed = 5.8f, Health = 3f, Damage = 1.35f, AggroRadius = 20f,
            Mods = Undead.Append(More(Stat.Armour, 70f)).Append(Flat(Stat.PhysicalReduction, 10f)).Append(Inc(Stat.AilmentTaken, -25f)).Append(Rules(Rule.HitCap)).ToArray(), Look = "dino:clubtail.bones.", Variants = 2, Scale = 0.85f, Bulk = 2.1f,
        };

        /// <summary>The shell has rotted through, and what's under it slops out wherever it's struck. Whoever stands close enough to hit one is standing in it.</summary>
        public static readonly EnemyKind RottingClubtail = new EnemyKind
        {
            Id = "rotting-clubtail", Name = "Rotting Clubtail", Books = new[] { Shelf.TailClub, Shelf.Gore }, MoveSpeed = 5.2f, Health = 3.4f, Damage = 1.3f, AggroRadius = 20f,
            Mods = RottenGreat.Append(More(Stat.Armour, 40f)).Append(Trigger(TriggerEvent.Struck, Shelf.BileSlop)).ToArray(), Look = "dino:clubtail.rot.", Variants = 2, Scale = 0.85f, Bulk = 2.2f,
        };

        /// <summary>Its shell is light now, and comes back if it's let: whatever's broken of it has to be followed up before it closes.</summary>
        public static readonly EnemyKind GhostClubtail = new EnemyKind
        {
            Id = "ghost-clubtail", Name = "Ghost Clubtail", Books = new[] { Shelf.TailClub, Shelf.Gore }, MoveSpeed = 6.2f, Health = 2.4f, Damage = 1.35f, AggroRadius = 20f, Barrier = 0.8f,
            Mods = Ghostly.Append(Flat(Stat.BarrierRecharge, 25f)).ToArray(), Look = "dino:clubtail.ghost.", Variants = 2, Scale = 0.85f, Bulk = 2.1f,
        };

        // -- Domeheads ram.

        /// <summary>The Reich's rams, with an iron cap bolted over the dome, driven at doors and at whoever's behind them. What one runs into stops where it stands.</summary>
        public static readonly EnemyKind WarDomehead = new EnemyKind
        {
            Id = "war-domehead", Name = "War Domehead", Books = new[] { Shelf.Ram, Shelf.Gore }, MoveSpeed = 8.6f, Health = 1.4f, Damage = 0.95f, AggroRadius = 24f,
            // Iron on bone goes through what's worn against it.
            Mods = Hide.Append(More(Stat.Armour, 50f)).Append(Inc(Stat.AilmentTaken, -15f)).Append(Flat(Stat.ArmourIgnore, 50f)).ToArray(), Look = "dino:domehead.reich.", Variants = 3, Bulk = 1.3f,
        };

        /// <summary>A skull that was most of the animal, on what's left of the rest. It still lowers it and runs.</summary>
        public static readonly EnemyKind BoneDomehead = new EnemyKind
        {
            Id = "bone-domehead", Name = "Bone Domehead", Books = new[] { Shelf.Ram, Shelf.Gore }, MoveSpeed = 7.6f, Health = 1.2f, Damage = 0.9f, AggroRadius = 22f,
            Mods = BareBones.Append(More(Stat.Armour, 30f)).ToArray(), Look = "dino:domehead.bones.", Variants = 2, Bulk = 1.25f,
        };

        /// <summary>It doesn't know its dome has split. It runs at whatever's in front of it as it always did, and there's bile where it ends up.</summary>
        public static readonly EnemyKind RottingDomehead = new EnemyKind
        {
            Id = "rotting-domehead", Name = "Rotting Domehead", Books = new[] { Shelf.Ram, Shelf.Gore }, MoveSpeed = 6.2f, Health = 1.6f, Damage = 0.85f, AggroRadius = 20f,
            Mods = Rotten.Append(More(Stat.Armour, 20f)).ToArray(), Look = "dino:domehead.rot.", Variants = 2, Bulk = 1.3f,
        };

        /// <summary>A ram of light. It comes through a shield as if it weren't held up, and whoever it reaches is stopped all the same.</summary>
        public static readonly EnemyKind GhostDomehead = new EnemyKind
        {
            Id = "ghost-domehead", Name = "Ghost Domehead", Books = new[] { Shelf.Ram, Shelf.Gore }, MoveSpeed = 8.4f, Health = 1f, Damage = 0.9f, AggroRadius = 22f,
            Mods = Ghostly, Look = "dino:domehead.ghost.", Variants = 2, Bulk = 1.25f,
        };

        /// <summary>The Reich's soldiers: greycoats in iron helms, who march behind the beasts.</summary>
        public static readonly EnemyKind Legionary = new EnemyKind
        {
            Id = "legionary", Name = "Reich Legionary", Books = new[] { Shelf.Strike }, Damage = 0.9f, Mods = new[] { More(Stat.Armour, 50f), Flat(Stat.BlockChance, 10f) }, Look = "reich:legionary",
        };

        public static readonly EnemyKind Marksman = new EnemyKind
        {
            Id = "marksman", Name = "Reich Marksman", Books = new[] { Shelf.Shot }, Weapon = Tags.Bow | Tags.TwoHanded, KeepsAway = true, AggroRadius = 30f, Damage = 0.75f, Look = "reich:marksman",
        };

        /// <summary>A beast-handler with a goad, who drives a war-band and knows what each of its animals will do.</summary>
        public static readonly EnemyKind Wrangler = new EnemyKind
        {
            Id = "wrangler", Name = "Reich Wrangler", Books = new[] { Shelf.Goad, Shelf.Maul, Shelf.Strike }, MoveSpeed = 6.6f, Health = 1.8f, Damage = 1.1f, Mods = new[] { More(Stat.Armour, 40f) },
            Look = "reich:wrangler", Scale = 1.12f,
        };

        /// <summary>A kind by its id, as a land's story names the tyrant that holds it.</summary>
        public static EnemyKind? Find(string id) => Array.Find(Everyone, kind => kind.Id == id);

        // What people raise and whistle for. Minions are enemies on your side, and share everything else with them.
        public static readonly EnemyKind Bones = new EnemyKind
        {
            Id = "bones", Name = "Risen Bones", Books = new[] { Shelf.Strike }, MoveSpeed = 7f, Health = 0.8f, Damage = 0.8f,
            Mods = new[] { Resist(Tags.Poison, 40f) }, Look = "skeleton",
        };

        public static readonly EnemyKind Hound = new EnemyKind
        {
            Id = "hound", Name = "Pack Hound", Books = new[] { Shelf.Bite }, MoveSpeed = 10f, Health = 0.6f, Damage = 0.7f,
            Mods = new[] { Inc(Stat.AttackSpeed, 30f) }, Look = "wolf",
        };

        /// <summary>
        /// What a Defender sets down to fight beside them: a stand of timber, rope and grey iron that never moves from where it was set, and over it, in the air, whatever weapon its page gave it, wielded by nobody that can be seen. It uses the book its page
        /// gives it for nothing, as it has no Mana to pay with. It isn't anyone the world fields and isn't drawn as a body, so it isn't in <see cref="Everyone"/>.
        /// </summary>
        public static readonly EnemyKind Turret = new EnemyKind
        {
            Id = "turret", Name = "Turret", Books = new[] { Shelf.Shot }, MoveSpeed = 0f, Health = 1.2f, Damage = 0.6f, AggroRadius = 26f, Look = "turret", Bulk = 0.8f,
            Weapon = Tags.Bow | Tags.TwoHanded,
        };

        /// <summary>Every kind of creature there is.</summary>
        /// <summary>
        /// Something to try a build on, in the Haven. It stands and takes it, has more health than anything it would be fair to fight and nothing to fight back with, and is drawn as straw on a post. It
        /// isn't anyone the world can field, so it isn't in <see cref="Everyone"/>.
        /// </summary>
        public static readonly EnemyKind Dummy = new EnemyKind
        {
            Id = "dummy", Name = "Training Dummy", Books = Array.Empty<BookPlan>(), MoveSpeed = 0f, Health = 40f, Damage = 0f, Look = "dummy", Bulk = 0.9f,
        };

        public static readonly EnemyKind[] Everyone =
        {
            Outlaw, Cutthroat, Brute, Archer, Sentry, Firebrand, Rimewitch, Stormcaller, Bogseer, EmberAcolyte, RimeAcolyte, StormAcolyte, MireAcolyte,
            Restless, Shambler, BoneArcher, GraveKnight, Mourner, Wolf, DireWolf, Boar, Bear, Warlord, Bones, Hound,
            WarRaptor, Raptor, Skitter, Spitter, FeralSpitter, Ravager, WildRavager, Hornback, Tyrant, BoneTyrant, RottingTyrant, BoneRaptor, BoneRavager, RottingRaptor, RottingHornback,
            Legionary, Marksman, Wrangler,
            WarRipper, Ripper, Wader, RottingWader, Spikeback,
            Weaver, Shagback, WildShagback, Reaper, Strider, Clubtail, Domehead,
            GhostRaptor, GhostRavager, GhostRipper, GhostWeaver, GhostTyrant, BoneHornback, BoneSpikeback, BoneWeaver, RottingRavager, RottingShagback,
            WarSkitter, BoneSkitter, RottingSkitter, GhostSkitter, BoneSpitter, RottingSpitter, GhostSpitter, WildTyrant, GhostHornback, BoneRipper, RottingRipper,
            WarWader, BoneWader, GhostWader, RottingSpikeback, GhostSpikeback, WarWeaver, RottingWeaver, BoneShagback, GhostShagback,
            WildReaper, BoneReaper, RottingReaper, GhostReaper, WarStrider, BoneStrider, RottingStrider, GhostStrider,
            BoneClubtail, RottingClubtail, GhostClubtail, WarDomehead, BoneDomehead, RottingDomehead, GhostDomehead,
        };

        public static EnemyKind? Minion(string? id) => id switch
        {
            "bones" => Bones,
            "turret" => Turret,
            _ => null,
        };

        public static readonly EnemyTrait[] Traits =
        {
            new("Burning", new[] { Convert(DamageType.Physical, DamageType.Fire, 100f), Resist(Tags.Fire, 50f), Chance(Tags.Burn, 40f) }, Shelf.Immolate),
            new("Frostbound", new[] { Convert(DamageType.Physical, DamageType.Cold, 100f), Resist(Tags.Cold, 50f), Inc(Stat.AilmentEffect, 50f, Tags.Chill) }, Shelf.FrostNova),
            new("Stormtouched", new[] { Convert(DamageType.Physical, DamageType.Lightning, 100f), Resist(Tags.Lightning, 50f), Chance(Tags.Shock, 40f) }, Shelf.Spark),
            new("Venomous", new[] { GainAs(DamageType.Physical, DamageType.Poison, 50f), Chance(Tags.Venom, 60f), Resist(Tags.Poison, 40f) }, Shelf.VenomSpit),
            new("Vampiric", new[] { Flat(Stat.HealthLeech, 25f), Inc(Stat.Health, 20f) }, Shelf.Sap),
            new("Ironclad", new[] { More(Stat.Armour, 200f), Flat(Stat.PhysicalReduction, 15f), Flat(Stat.BlockChance, 20f) }, Shelf.Sunder),
            new("Swift", new[] { Inc(Stat.MoveSpeed, 40f), Inc(Stat.AttackSpeed, 30f), Inc(Stat.CastSpeed, 30f) }, Shelf.Knives),
            new("Thorned", new[] { Flat(Stat.Reflect, 40f), Inc(Stat.Armour, 50f) }, Shelf.Cleave),
            new("Warded", new[] { Inc(Stat.Health, -30f), Flat(Stat.BarrierRecharge, 30f) }, Shelf.ArcaneDart),
            new("Deadeye", new[] { Flat(Stat.CritChance, 20f), Flat(Stat.CritMultiplier, 50f), Flat(Stat.Accuracy, 200f) }, Shelf.Shot),
            new("Unmaking", new[] { Chance(Tags.Hex, 50f), Added(DamageType.Magic, 6f), Flat(Stat.Penetration, 15f) }, Shelf.Unmaking),
            new("Fearsome", new[] { More(Stat.Damage, 20f), Inc(Stat.Health, 30f) }, Shelf.Maul),
        };

        /// <summary>One trait, as a set of them.</summary>
        public static int Trait(int index) => 1 << index;

        /// <summary>Every trait in a set, in the order they're listed.</summary>
        public static IEnumerable<EnemyTrait> TraitsOf(int traits)
        {
            for (int i = 0; i < Traits.Length; i++)
            {
                if ((traits & Trait(i)) != 0) yield return Traits[i];
            }
        }

        /// <summary>Adds to a set whatever a rarity has room for, so that something rare that's taken after its pack is still as much as any other.</summary>
        public static int Fill(int traits, Rarity rarity, WorldRandom random)
        {
            int wanted = PowerOf(rarity).Traits;
            while (System.Numerics.BitOperations.PopCount((uint)traits) < wanted) traits |= Trait(random.Int(Traits.Length));
            return traits;
        }

        /// <summary>A set with one more trait in it than it had, if there's one it hasn't got.</summary>
        public static int OneMore(int traits, WorldRandom random)
        {
            int wanted = System.Numerics.BitOperations.PopCount((uint)traits) + 1;
            for (int attempt = 0; attempt < Traits.Length * 4 && System.Numerics.BitOperations.PopCount((uint)traits) < wanted; attempt++) traits |= Trait(random.Int(Traits.Length));
            return traits;
        }

        /// <summary>What it's called, by what it's become.</summary>
        public static string NameOf(EnemyKind kind, int traits) => string.Join(' ', TraitsOf(traits).Select(trait => trait.Name).Append(kind.Name));

        // What a trait gave it comes first, as books are listed best first. Anything it could do already isn't a second thing to do.
        private static IEnumerable<BookPlan> Abilities(EnemyKind kind, int traits) => TraitsOf(traits).Select(trait => trait.Book).Distinct().Except(kind.Books);

        /// <summary>How many of its books its traits gave it, which are the first of them.</summary>
        public static int AbilitiesOf(EnemyKind kind, int traits) => Abilities(kind, traits).Count();

        private static IEnumerable<BookPlan> BooksOf(EnemyKind kind, int traits) => Abilities(kind, traits).Concat(kind.Books);

        private static IEnumerable<Mod> ModsOf(int traits, int level, float rarityHealth)
        {
            foreach (EnemyTrait trait in TraitsOf(traits))
            {
                foreach (Mod mod in trait.Mods) yield return mod;
                if (trait.Name == "Warded") yield return Flat(Stat.Barrier, HealthAt(level) * rarityHealth);
            }
        }

        private readonly Dictionary<(string kind, int level, Rarity rarity, int traits), Loadout> _shared = new();

        /// <summary>What everything hostile in this world wears on top of what it is, on a land that's had something paid on it.</summary>
        public readonly IReadOnlyList<Mod> Worn;

        public Bestiary(IReadOnlyList<Mod>? worn = null)
        {
            Worn = worn ?? Array.Empty<Mod>();
        }

        private static IEnumerable<Mod> Base(EnemyKind kind, int level, float rarityHealth)
        {
            yield return Flat(Stat.Health, HealthAt(level) * rarityHealth * kind.Health);
            yield return Flat(Stat.Mana, 400f + 40f * level);
            yield return Flat(Stat.ManaRegen, 40f);
            yield return Flat(Stat.MoveSpeed, kind.MoveSpeed);
            yield return Flat(Stat.Armour, 6f * level);
            yield return Flat(Stat.Evasion, 5f * level);
            yield return Flat(Stat.Accuracy, AccuracyAt(level));
            yield return Inc(Stat.Damage, -35f, Tags.Spell);
            if (kind.Barrier > 0f) yield return Flat(Stat.Barrier, HealthAt(level) * rarityHealth * kind.Barrier);
            foreach (Mod mod in kind.Mods) yield return mod;
        }

        private static WeaponProfile WeaponFor(EnemyKind kind, int level, float rarityDamage)
        {
            float damage = DamageAt(level) * rarityDamage * kind.Damage;
            return new WeaponProfile(damage * 0.85f, damage * 1.15f, WeaponProfile.BareTime, 5f, kind.Weapon);
        }

        // As much Spell Damage as it has Attack Damage, as its Spell Pages are as much of what it does as its Attack Pages. Whatever it's made of turns that into its own kind of damage, as it does a blow.
        private static Mod SpellFor(EnemyKind kind, int level, float rarityDamage) => Added(DamageType.Physical, DamageAt(level) * rarityDamage * kind.Damage, Tags.Spell);

        private const int BanditOutfits = 8;     // What a bandit can be numbered, which a client folds into the few it draws.

        /// <summary>What a body of this kind is drawn as. Some come in several, so that a pack isn't one person many times over.</summary>
        public static string ModelOf(EnemyKind kind, WorldRandom random)
        {
            if (kind.Look == "bandit") return $"bandit{random.Int(BanditOutfits)}:{kind == Archer}";
            return kind.Variants > 0 ? $"{kind.Look}{random.Int(kind.Variants)}" : kind.Look;
        }

        /// <summary>Everything <see cref="ModelOf"/> can draw a body of this kind as, for a client to grow before it's sent any.</summary>
        public static IEnumerable<string> ModelsOf(EnemyKind kind)
        {
            if (kind.Look == "bandit")
            {
                for (int outfit = 0; outfit < BanditOutfits; outfit++) yield return $"bandit{outfit}:{kind == Archer}";
            }
            else if (kind.Variants > 0)
            {
                for (int variant = 0; variant < kind.Variants; variant++) yield return $"{kind.Look}{variant}";
            }
            else yield return kind.Look;
        }

        /// <summary>The numbers for this kind of enemy at this level, shared by every one of them there is.</summary>
        /// <param name="traits">A set of them: see <see cref="Trait"/>.</param>
        public Loadout Shared(EnemyKind kind, int level, Rarity rarity, int traits)
        {
            if (_shared.TryGetValue((kind.Id, level, rarity, traits), out Loadout? loadout)) return loadout;

            EnemyPower power = PowerOf(rarity);
            IEnumerable<Mod> mods = Base(kind, level, power.Health).Append(SpellFor(kind, level, power.Damage)).Concat(ModsOf(traits, level, power.Health));
            loadout = new Loadout(level, WeaponFor(kind, level, power.Damage), mods.Concat(Worn), BooksOf(kind, traits));
            _shared.Add((kind.Id, level, rarity, traits), loadout);
            return loadout;
        }

        /// <summary>
        /// A boss is the only one of itself, so it gets a loadout to itself. It wears real gear and walks the same tree a hero does. It'll fight with all
        /// of that, and what it wore is what it drops.
        /// </summary>
        /// <param name="landWide">What everything hostile here wears, which a boss wears too.</param>
        public static Loadout Boss(EnemyKind kind, int level, int traits, WorldRandom random, out Item[] gear, out string? title, IReadOnlyList<Mod>? landWide = null)
        {
            EnemyPower power = PowerOf(Rarity.Unique);
            var mods = new List<Mod>(Base(kind, level, power.Health)) { SpellFor(kind, level, power.Damage) };
            WorldRandom gearDraw = random.Fork("gear");

            ItemSlot[] slots = { ItemSlot.Neck, ItemSlot.Body, ItemSlot.Finger };
            var worn = new List<Item>();
            int pieces = gearDraw.Int(1, 2);
            for (int i = 0; i < pieces; i++)
            {
                ItemSlot slot = slots[gearDraw.Int(slots.Length)];
                Rarity least = gearDraw.Chance(0.45f) ? Rarity.Unique : Rarity.Rare;
                Item item = ItemGenerator.Roll(level + 1, gearDraw.Fork(i), 0.5f, least, slot);

                // Some uniques only mean anything in a player's hands, or would make a boss no boss at all.
                for (int again = 1; again < HeroOnlyRerolls && item.Unique != null && UniqueCatalog.Find(item.Unique) is { HeroOnly: true }; again++)
                {
                    item = ItemGenerator.Roll(level + 1, gearDraw.Fork(pieces + i * HeroOnlyRerolls + again), 0.5f, least, slot);
                }
                if (item.Unique != null && UniqueCatalog.Find(item.Unique) is { HeroOnly: true }) item = ItemGenerator.Roll(level + 1, gearDraw.Fork(i), 0.5f, Rarity.Rare, slot);
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

            mods.AddRange(ModsOf(traits, level, power.Health));
            if (landWide != null) mods.AddRange(landWide);
            return new Loadout(level, WeaponFor(kind, level, power.Damage), mods, BooksOf(kind, traits));
        }

        // Some keystones only mean something with a player's hands on them.
        private static bool FitsABoss(TalentNode node)
        {
            foreach (Mod mod in node.Mods)
            {
                // Costs, Toggle Books and attributes are a hero's business: a tyrant pays for nothing and toggles nothing, and has no attributes to choose between.
                if (mod.Kind == ModKind.Rule && mod.Rule is Rule.NoOwnDamage or Rule.Guardian or Rule.CostsHealth or Rule.ToggleReserves or Rule.BarrierPays or Rule.NarrowWay or Rule.Shorthand) return false;
                if (mod.Scope.HasFlag(Tags.Minion) || mod.Stat == Stat.RaiseFallen) return false;
            }
            return true;
        }

        /// <summary>
        /// What a minion comes to, for someone with this loadout. Whatever its owner has for minions it has for itself. Given a weapon it strikes with that, and its Spell Damage is what that weapon gives, as
        /// a hero's is. Given a book, that's all it does, and it pays for it as a hero does: a hero's Mana, coming back as a hero's does (<see cref="Rulebook.BaseMana"/>, <see cref="Rulebook.ManaRefill"/>).
        /// </summary>
        public static Loadout MinionOf(Loadout owner, ActorSheet sheet, EnemyKind kind, Item? weapon = null, Item? offHand = null, BookPlan? book = null)
        {
            // What's in its hands is as it would be on a hero: a weapon in each is one weapon of both, and a shield is what a shield gives. A weapon alone in the other hand is simply the weapon.
            if (weapon == null && offHand is { IsOneHandedWeapon: true })
            {
                weapon = offHand;
                offHand = null;
            }
            Item? second = offHand is { IsOneHandedWeapon: true } ? offHand : null;

            // A turret pays for nothing, so only what moves and is given a book needs a hero's Mana.
            bool pays = book != null && !kind.Rooted;
            var mods = new List<Mod>(Base(kind, owner.Level, 1f).Where(mod => !pays || mod.Stat is not (Stat.Mana or Stat.ManaRegen)));
            if (pays) mods.Add(Flat(Stat.Mana, Rulebook.BaseMana));
            if (weapon != null) mods.AddRange(weapon.Mods());
            else mods.Add(SpellFor(kind, owner.Level, 1f));
            if (offHand != null) mods.AddRange(offHand.Mods());
            if (second != null) mods.Add(Grant(Condition.DualWielding));
            else if (offHand != null) mods.Add(Grant(Condition.HoldingShield));
            bool puppet = sheet.Has(Rule.Puppeteer);
            foreach (Mod mod in owner.Mods)
            {
                if ((mod.Scope & Tags.Minion) != 0)
                {
                    mods.Add(mod with { Scope = mod.Scope & ~Tags.Minion });
                    continue;
                }
                if (puppet && mod.Stat is Stat.Damage or Stat.AttackSpeed or Stat.CastSpeed or Stat.UseSpeed or Stat.CritChance or Stat.CritMultiplier) mods.Add(mod);
            }
            WeaponProfile held = weapon == null ? WeaponFor(kind, owner.Level, 1f) : second != null ? Item.Paired(weapon.Weapon(), second.Weapon()) : weapon.Weapon();
            return new Loadout(owner.Level, held, mods, book != null ? new[] { book } : kind.Books);
        }

        /// <summary>What a fallen enemy can do on your side: everything it could do before, in your colours, a little worn.</summary>
        public static Loadout Risen(Loadout enemy, int level)
        {
            var mods = new List<Mod>(enemy.Mods.Length + 1);
            mods.AddRange(enemy.Mods);
            mods.Add(More(Stat.Health, -Rulebook.RisenHealthLess));
            return new Loadout(level, enemy.Weapon, mods, enemy.Books);
        }
    }
}

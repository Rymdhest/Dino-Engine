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
        public float Health = 1f;
        public float Damage = 1f;
        public string Look = "bandit";  // What it's drawn as, which CharacterFactory.Resolve has to know.
        public int Variants;        // How many ways it's drawn, numbered onto Look.
        public float Scale = 1f;

        /// <summary>How far its body reaches from where it stands, in metres at a scale of one, for what's solid and what's struck. Nothing means a person's, which grows a little with them and no further.</summary>
        public float Bulk;

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
        public static int ExperienceAt(int level, float rankHealth) => (int)MathF.Ceiling(6f * MathF.Pow(1.11f, level) * rankHealth);

        private static readonly Mod[] Undead = { Resist(Tags.Poison, 40f), Resist(Tags.Cold, 30f), Resist(Tags.Fire, -20f), Flat(Stat.AilmentAvoid, 100f, Tags.Bleed) };

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
            Mods = Undead, Look = "skeleton",
        };

        public static readonly EnemyKind Shambler = new EnemyKind
        {
            Id = "shambler", Name = "Shambler", Books = new[] { Shelf.Bite }, MoveSpeed = 4.6f, Health = 0.5f, Damage = 0.42f,
            Mods = Undead, Look = "skeleton", Scale = 0.94f,
        };

        public static readonly EnemyKind BoneArcher = new EnemyKind
        {
            Id = "bone-archer", Name = "Bone Archer", Books = new[] { Shelf.Shot }, Weapon = Tags.Bow | Tags.TwoHanded, MoveSpeed = 5.2f,
            KeepsAway = true, AggroRadius = 30f, Health = 0.55f, Damage = 0.65f, Mods = Undead, Look = "skeleton:bow",
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
            Mods = new[] { Inc(Stat.AttackSpeed, 20f), Resist(Tags.Cold, 20f) }, Look = "wolf",
        };

        public static readonly EnemyKind DireWolf = new EnemyKind
        {
            Id = "dire-wolf", Name = "Dire Wolf", Books = new[] { Shelf.Bite }, MoveSpeed = 9.2f, Health = 1.9f, Damage = 1.2f, AggroRadius = 24f,
            Mods = new[] { Inc(Stat.AttackSpeed, 20f), Resist(Tags.Cold, 30f) }, Look = "direwolf",
        };

        public static readonly EnemyKind Boar = new EnemyKind
        {
            Id = "boar", Name = "Wild Boar", Books = new[] { Shelf.Bite }, MoveSpeed = 8f, Health = 0.8f, Damage = 0.75f, AggroRadius = 17f,
            Mods = new[] { More(Stat.Armour, 40f), Flat(Stat.PhysicalReduction, 8f) }, Look = "boar",
        };

        public static readonly EnemyKind Bear = new EnemyKind
        {
            Id = "bear", Name = "Cave Bear", Books = new[] { Shelf.Maul, Shelf.Bite }, MoveSpeed = 6.6f, Health = 3f, Damage = 1.5f,
            Mods = new[] { More(Stat.Armour, 50f), Inc(Stat.AilmentTaken, -30f) }, Look = "bear",
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
            Id = "warlord", Name = "Warlord", Books = new[] { Shelf.Whirl, Shelf.Maul, Shelf.Strike }, AggroRadius = 26f, LeashRadius = 70f,
            Mods = new[] { More(Stat.Armour, 100f), Inc(Stat.AilmentTaken, -50f) }, Look = "warlord",
        };

        public static readonly EnemyKind[] Casters = { Firebrand, Rimewitch, Stormcaller, Bogseer };

        // ---- The Saurian Reich, and what it has raised. Read LORE.md.

        // What the Reich's beasts have against the land's: tough hides, and blood bred hot.
        private static readonly Mod[] Hide = { Resist(Tags.Poison, 15f), Resist(Tags.Cold, -10f) };

        // What the Ossuary's failures have: rot that poisons what it bites, no blood to lose, and no love of fire.
        private static readonly Mod[] Rot = { GainAs(DamageType.Physical, DamageType.Poison, 40f), Chance(Tags.Venom, 35f), Resist(Tags.Poison, 60f), Resist(Tags.Fire, -25f), Flat(Stat.AilmentAvoid, 100f, Tags.Bleed) };

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
            Mods = Hide.Append(Inc(Stat.AttackSpeed, 20f)).ToArray(), Look = "dino:raptor.", Variants = 6, Bulk = 1.25f,
        };

        /// <summary>Little scavengers in their dozens, wherever anything has died, which is everywhere the Reich has been.</summary>
        public static readonly EnemyKind Skitter = new EnemyKind
        {
            Id = "skitter", Name = "Skitter", Books = new[] { Shelf.HungryBite }, MoveSpeed = 10.4f, Health = 0.22f, Damage = 0.22f, AggroRadius = 20f,
            Mods = new[] { Inc(Stat.AttackSpeed, 30f), Inc(Stat.Evasion, 40f) }, Look = "dino:skitter.", Variants = 5, Bulk = 0.6f,
        };

        /// <summary>Venom the blood-doctors bred into it, spat from behind the pack.</summary>
        public static readonly EnemyKind Spitter = new EnemyKind
        {
            Id = "spitter", Name = "Spitter", Books = new[] { Shelf.VenomSpit, Shelf.RakingBite }, MoveSpeed = 8f, Health = 0.6f, Damage = 0.6f, AggroRadius = 28f, KeepsAway = true,
            Mods = new[] { Resist(Tags.Poison, 50f) }, Look = "dino:spitter.reich.", Variants = 4, Bulk = 1.3f,
        };

        public static readonly EnemyKind FeralSpitter = new EnemyKind
        {
            Id = "feral-spitter", Name = "Wild Spitter", Books = new[] { Shelf.VenomSpit, Shelf.RakingBite }, MoveSpeed = 8f, Health = 0.6f, Damage = 0.6f, AggroRadius = 28f, KeepsAway = true,
            Mods = new[] { Resist(Tags.Poison, 50f) }, Look = "dino:spitter.", Variants = 4, Bulk = 1.3f,
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
            Mods = Hide.Append(More(Stat.Armour, 20f)).ToArray(), Look = "dino:ravager.", Variants = 4, Scale = 0.85f, Bulk = 1.9f,
        };

        /// <summary>Driven at walls, and walls go.</summary>
        public static readonly EnemyKind Hornback = new EnemyKind
        {
            Id = "hornback", Name = "Hornback", Books = new[] { Shelf.Gore, Shelf.Stomp }, MoveSpeed = 7.2f, Health = 2.8f, Damage = 1.35f, AggroRadius = 22f,
            Mods = Hide.Append(More(Stat.Armour, 60f)).Append(Flat(Stat.PhysicalReduction, 8f)).ToArray(), Look = "dino:hornback.reich.", Variants = 4, Scale = 0.85f, Bulk = 2.1f,
        };

        /// <summary>A commander's war-beast, and what holds a land for the Reich. Tyrants are only ever met as what a land's freedom depends on.</summary>
        public static readonly EnemyKind Tyrant = new EnemyKind
        {
            Id = "tyrant", Name = "Tyrant", Books = new[] { Shelf.Roar, Shelf.TailSweep, Shelf.Stomp, Shelf.CrushingJaws }, MoveSpeed = 6.6f, Health = 1.35f, Damage = 1.15f, AggroRadius = 30f, LeashRadius = 75f,
            Mods = Hide.Append(More(Stat.Armour, 80f)).Append(Inc(Stat.AilmentTaken, -50f)).ToArray(), Look = "dino:tyrant.reich.", Scale = 0.9f, Bulk = 1.7f,
        };

        /// <summary>A tyrant the Ossuary raised straight out of the rock it lay in, with nothing on its bones but the light that moves them.</summary>
        public static readonly EnemyKind BoneTyrant = new EnemyKind
        {
            Id = "bone-tyrant", Name = "Bone Tyrant", Books = new[] { Shelf.Roar, Shelf.TailSweep, Shelf.Stomp, Shelf.CrushingJaws }, MoveSpeed = 6.2f, Health = 1.25f, Damage = 1.2f, AggroRadius = 30f, LeashRadius = 75f,
            Mods = Undead.Append(More(Stat.Armour, 60f)).Append(Inc(Stat.AilmentTaken, -50f)).ToArray(), Look = "dino:tyrant.bones.", Scale = 0.9f, Bulk = 1.5f,
        };

        /// <summary>A tyrant that hatched dying, and wasn't let die.</summary>
        public static readonly EnemyKind RottingTyrant = new EnemyKind
        {
            Id = "rotting-tyrant", Name = "Rotting Tyrant", Books = new[] { Shelf.Roar, Shelf.TailSweep, Shelf.Stomp, Shelf.CrushingJaws }, MoveSpeed = 5.8f, Health = 1.5f, Damage = 1.1f, AggroRadius = 30f, LeashRadius = 75f,
            Mods = Rot.Append(More(Stat.Armour, 40f)).Append(Inc(Stat.AilmentTaken, -50f)).ToArray(), Look = "dino:tyrant.rot.", Scale = 0.9f, Bulk = 1.7f,
        };

        public static readonly EnemyKind BoneRaptor = new EnemyKind
        {
            Id = "bone-raptor", Name = "Bone Raptor", Books = new[] { Shelf.RakingBite }, MoveSpeed = 8.2f, Health = 0.5f, Damage = 0.45f,
            Mods = Undead, Look = "dino:raptor.bones.", Variants = 4, Bulk = 1.2f,
        };

        public static readonly EnemyKind BoneRavager = new EnemyKind
        {
            Id = "bone-ravager", Name = "Bone Ravager", Books = new[] { Shelf.Stomp, Shelf.RakingBite }, MoveSpeed = 6.8f, Health = 2.4f, Damage = 1.3f,
            Mods = Undead.Append(More(Stat.Armour, 40f)).ToArray(), Look = "dino:ravager.bones.", Variants = 3, Scale = 0.85f, Bulk = 1.8f,
        };

        public static readonly EnemyKind RottingRaptor = new EnemyKind
        {
            Id = "rotting-raptor", Name = "Rotting Raptor", Books = new[] { Shelf.RakingBite }, MoveSpeed = 6.2f, Health = 0.7f, Damage = 0.45f,
            Mods = Rot, Look = "dino:raptor.rot.", Variants = 4, Bulk = 1.25f,
        };

        public static readonly EnemyKind RottingHornback = new EnemyKind
        {
            Id = "rotting-hornback", Name = "Rotting Hornback", Books = new[] { Shelf.Gore, Shelf.Stomp }, MoveSpeed = 5.8f, Health = 3f, Damage = 1.3f,
            Mods = Rot.Append(More(Stat.Armour, 40f)).ToArray(), Look = "dino:hornback.rot.", Variants = 3, Scale = 0.85f, Bulk = 2.1f,
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
            Id = "wrangler", Name = "Reich Wrangler", Books = new[] { Shelf.Maul, Shelf.Strike }, MoveSpeed = 6.6f, Health = 1.8f, Damage = 1.1f, Mods = new[] { More(Stat.Armour, 40f) },
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
        };

        public static EnemyKind? Minion(string? id) => id switch
        {
            "bones" => Bones,
            "hound" => Hound,
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
            foreach (Mod mod in kind.Mods) yield return mod;
        }

        private static WeaponProfile WeaponFor(EnemyKind kind, int level, float rarityDamage)
        {
            float damage = DamageAt(level) * rarityDamage * kind.Damage;
            return new WeaponProfile(damage * 0.85f, damage * 1.15f, WeaponProfile.BareTime, 5f, kind.Weapon);
        }

        /// <summary>What a body of this kind is drawn as. Some come in several, so that a pack isn't one person many times over.</summary>
        public static string ModelOf(EnemyKind kind, WorldRandom random)
        {
            if (kind.Look == "bandit") return $"bandit{random.Int(8)}:{kind == Archer}";
            return kind.Variants > 0 ? $"{kind.Look}{random.Int(kind.Variants)}" : kind.Look;
        }

        /// <summary>The numbers for this kind of enemy at this level, shared by every one of them there is.</summary>
        /// <param name="traits">A set of them: see <see cref="Trait"/>.</param>
        public Loadout Shared(EnemyKind kind, int level, Rarity rarity, int traits)
        {
            if (_shared.TryGetValue((kind.Id, level, rarity, traits), out Loadout? loadout)) return loadout;

            EnemyPower power = PowerOf(rarity);
            IEnumerable<Mod> mods = Base(kind, level, power.Health).Concat(ModsOf(traits, level, power.Health));
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
            var mods = new List<Mod>(Base(kind, level, power.Health));
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
                if (mod.Kind == ModKind.Rule && mod.Rule is Rule.NoOwnDamage or Rule.Guardian or Rule.CostsHealth) return false;
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
            return new Loadout(owner.Level, WeaponFor(kind, owner.Level, 1f), mods, kind.Books);
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

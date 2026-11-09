using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Saurian
{
    /// <summary>The ways of being built that a dinosaur's body is grown from. Everything else about one is numbers.</summary>
    public enum SaurianForm
    {
        Theropod,   // On two legs, a beam balanced on the hips, with a mouth to fight with.
        Horned,     // On four, with a beak, horns and a frill, and the weight all in front.
        Sauropod,   // On four pillars, higher at the shoulder than the hip, with a neck longer than the body in front of the tail, held up as a giraffe's is.
    }

    /// <summary>What's been done to one, which is a body of its own.</summary>
    public enum SaurianCondition
    {
        Living,     // Grown from blood in amber, as the Reich grows them.
        Rotting,    // Raised before the blood took, and kept going by iron and bile.
        Bones,      // Dug out of the ground and raised as it lay there.
    }

    /// <summary>Patterns a hide can carry. Every one of them is something living animals wear, and countershading and a banded tail are what Sinosauropteryx is known to have had.</summary>
    [Flags]
    public enum HidePattern
    {
        None = 0,
        Stripes = 1,    // Bands across the back, down the flanks.
        Spots = 2,      // Rosettes and blotches.
        Bands = 4,      // Rings round the tail.
        Mask = 8,       // A dark stripe through the eye, from snout to ear.
        Saddles = 16,   // A few big dark patches along the back.
        Mottle = 32,    // Fine broken colour all over.
    }

    /// <summary>One colour scheme a species can wear. Colours are albedos under a sun of about 13, so all of them are deep: anything pale goes white.</summary>
    public sealed class Hide
    {
        public Vector3 Back;                // Along the top.
        public Vector3 Belly;               // Underneath: lighter, as countershading is, but never pale.
        public Vector3 Pattern;             // What the stripes and spots are.
        public Vector3 Display;             // Crests, throat, frill: what's shown off.
        public HidePattern Patterns;
        public float Drift = 0.12f;         // How far one animal's colours wander from these.
        public float Contrast = 1f;         // How strongly the patterns stand out, as something that has nothing to hide from or ambush has no need of bold ones.
    }

    /// <summary>
    /// A kind of dinosaur, as ranges a body is drawn from. Lengths are in the game's metres, where a person stands 4.7 m tall, at a scale of one. Proportions are shares of the whole length,
    /// taken from the skeletons of the animals each is after and then bent towards what reads from 33 m up, which is mostly the head, the tail and how it stands. A species is the only
    /// hand-written thing about a dinosaur: everything else is grown from it and a seed.
    /// </summary>
    public sealed class SaurianSpecies
    {
        public string Id = "";
        public string Name = "";
        public SaurianForm Form;

        /// <summary>Never had by the Reich or raised by the Ossuary, so never in harness, rotting or bones: <see cref="SaurianSpec.Parse"/> refuses to name one that way.</summary>
        public bool Free;

        // ---- Size and proportion.
        public FloatRange Length;           // Snout to tail tip.
        public FloatRange Hip;              // Height of the hip joint, as a share of length.
        public FloatRange Withers;          // How much higher the shoulder joint stands than the hip, as a share of the hip's height, on what has forelegs longer than its hind legs. Nothing by default.
        public FloatRange Skull;            // Shares of length along the body. The tail is what's left.
        public FloatRange Neck;
        public FloatRange Trunk;
        public FloatRange Girth;            // Half the depth of the chest, as a share of length.
        public FloatRange Breadth;          // Half the width of the chest, as a share of half its depth: under 1 is narrow, as most theropods were.
        public FloatRange Belly = new FloatRange(0.15f, 0.3f);  // How far the belly hangs below a line from chest to hips, as a share of chest depth.
        public FloatRange BackSlope = new FloatRange(-0.06f, 0.02f); // Radians the back rises from hips to shoulders. A theropod is highest at the hips.
        public FloatRange NeckRise = new FloatRange(0.7f, 0.95f);   // Radians the neck leaves the shoulders at, above level.
        public FloatRange NeckGirth = new FloatRange(0.55f, 0.7f);  // Half-depth of the neck at its root, as a share of chest half-depth.
        public FloatRange TailRoot = new FloatRange(0.7f, 0.85f);   // Half-depth of the tail at the hips, as a share of chest half-depth.
        public float TailRod;                                       // 0 thins evenly to a whip, 1 thins fast and runs on as a rod, as a raptor's did.
        public FloatRange TailDroop = new FloatRange(-0.05f, 0.08f);// Radians it falls away below level. Tails were carried clear of the ground.

        // ---- Legs: femur, tibia and metatarsus, as shares of the leg, and how folded they stand. Small fast animals had long shins and stood folded; giants stood on near columns.
        public Vector3 LegBones = new Vector3(0.38f, 0.38f, 0.24f);
        public FloatRange Crouch = new FloatRange(0.3f, 0.4f);
        public FloatRange Thigh = new FloatRange(0.55f, 0.7f);      // Half-thickness of the thigh at the hip, as a share of chest half-depth.
        public FloatRange Shin = new FloatRange(0.3f, 0.38f);       // And of the shin under the knee.
        public FloatRange Toe = new FloatRange(0.32f, 0.4f);        // Toe length, as a share of the metatarsus.
        public float Sickle;                                        // How big the raised claw on the second toe is: a dromaeosaur's killing claw.

        // ---- Arms, as shares of the femur, and on four legs these are forelegs.
        public FloatRange Arm = new FloatRange(0.5f, 0.6f);
        public int Fingers = 3;
        public FloatRange Claw = new FloatRange(0.1f, 0.14f);       // Of the arm.
        public FloatRange ArmGirth = new FloatRange(0.16f, 0.22f);  // Half-thickness of the upper arm, as a share of chest half-depth.

        // ---- Head, as shares of the skull's length.
        public FloatRange SkullDepth = new FloatRange(0.36f, 0.44f);   // Height at the back of the skull.
        public FloatRange SkullWidth = new FloatRange(0.28f, 0.34f);   // The whole width across the back of the skull.
        public FloatRange Snout = new FloatRange(0.32f, 0.42f);        // How wide the snout is at its tip, of the back.
        public FloatRange SnoutDepth = new FloatRange(0.38f, 0.48f);   // How deep, of the back.
        public FloatRange Profile = new FloatRange(-0.02f, 0.04f);     // How the top of the snout bows: over 0 is a Roman nose, under 0 is dished, as Velociraptor's is.
        public FloatRange Brow = new FloatRange(0.03f, 0.06f);         // Bosses over the eyes.
        public FloatRange BrowHorns;                                   // Horns over the eyes, as Carnotaurus had. Nothing by default.
        public FloatRange Crest;                                       // A pair of thin crests along the snout, as Dilophosaurus had. Nothing by default.
        public FloatRange Eye = new FloatRange(0.055f, 0.07f);
        public IntRange Teeth = new IntRange(10, 13);                  // Along each side of each jaw.
        public FloatRange Tooth = new FloatRange(0.05f, 0.07f);        // The longest, as a share of the skull.
        public float Lips = 0.5f;                                      // 0 shows every tooth with the mouth shut, 1 hides them all. Cullen and others make a good case for lips.
        public FloatRange Jowls = new FloatRange(0f, 0.04f);           // Muscle bulging behind the eye, where the jaw closes from.
        public FloatRange Throat;                                      // A pouch under the throat that's shown off. Nothing by default.
        public FloatRange Arch;                                        // A dome over the nostrils, standing above the rest of the skull, as Giraffatitan's does. Nothing by default.
        public Vector3[]? Eyes;                                        // What colour its eyes can be. Nothing: a hunter's ambers and yellows.

        // ---- Horned heads.
        public FloatRange Beak;                                        // How far the beak hooks down.
        public FloatRange NoseHorn;
        public FloatRange Frill;                                       // How far back it reaches, as a share of the skull.
        public FloatRange FrillSpikes;                                 // Spikes off its edge, of the skull.
        public IntRange FrillPoints = new IntRange(0, 0);

        // ---- What grows out of the hide.
        public float QuillChance;                                      // A mane of quills down the neck and back, and short feathers on the arms.
        public FloatRange Quills = new FloatRange(0.03f, 0.05f);       // Of length.
        public float ScuteChance;                                      // A row of low spines down the back.
        public FloatRange Scutes = new FloatRange(0.006f, 0.01f);

        public Hide[] Hides = Array.Empty<Hide>();

        /// <summary>How many triangles its skin is allowed, at a scale of one. A thing's triangles are paid for over and over, and these are seen from 33 m up.</summary>
        public int Budget = 6000;
    }

    /// <summary>Every kind of dinosaur there is. A new one is an entry here, and a word for it in the game's bestiary.</summary>
    public static class SaurianCatalog
    {
        private static Vector3 V(float r, float g, float b) => new Vector3(r, g, b);
        private static FloatRange R(float min, float max) => new FloatRange(min, max);

        /// <summary>
        /// A pack hunter, after Deinonychus: the size of a person at the head, long in the arm, with a killing claw held up off the ground on each foot and a tail stiffened by tendons into a rod.
        /// The Reich breeds more of these than anything else.
        /// </summary>
        public static readonly SaurianSpecies Raptor = new SaurianSpecies
        {
            Id = "raptor", Name = "Raptor", Form = SaurianForm.Theropod,
            Length = R(7.6f, 8.6f), Hip = R(0.235f, 0.26f), Skull = R(0.115f, 0.125f), Neck = R(0.15f, 0.17f), Trunk = R(0.2f, 0.22f), NeckRise = R(0.85f, 1f), BackSlope = R(-0.08f, 0f),
            Girth = R(0.062f, 0.07f), Breadth = R(0.72f, 0.82f), TailRod = 0.85f, TailRoot = R(0.62f, 0.72f), TailDroop = R(-0.08f, 0.02f),
            LegBones = new Vector3(0.36f, 0.43f, 0.21f), Crouch = R(0.42f, 0.5f), Thigh = R(0.62f, 0.72f), Shin = R(0.3f, 0.36f), Toe = R(0.42f, 0.5f), Sickle = 1f,
            Arm = R(1.15f, 1.3f), Fingers = 3, Claw = R(0.13f, 0.16f), ArmGirth = R(0.17f, 0.21f),
            SkullDepth = R(0.3f, 0.36f), SkullWidth = R(0.26f, 0.31f), Snout = R(0.3f, 0.36f), SnoutDepth = R(0.36f, 0.44f), Profile = R(-0.05f, 0.01f),
            Brow = R(0.02f, 0.04f), Eye = R(0.07f, 0.085f), Teeth = new IntRange(9, 12), Tooth = R(0.045f, 0.06f), Lips = 0.4f,
            QuillChance = 0.7f, Quills = R(0.035f, 0.055f),
            Hides = new[]
            {
                new Hide { Back = V(0.2f, 0.13f, 0.06f), Belly = V(0.4f, 0.31f, 0.18f), Pattern = V(0.05f, 0.035f, 0.025f), Display = V(0.42f, 0.09f, 0.03f), Patterns = HidePattern.Stripes | HidePattern.Bands | HidePattern.Mask },
                new Hide { Back = V(0.12f, 0.14f, 0.06f), Belly = V(0.32f, 0.31f, 0.18f), Pattern = V(0.035f, 0.04f, 0.02f), Display = V(0.38f, 0.26f, 0.04f), Patterns = HidePattern.Spots | HidePattern.Mask | HidePattern.Mottle },
                new Hide { Back = V(0.14f, 0.13f, 0.12f), Belly = V(0.34f, 0.31f, 0.26f), Pattern = V(0.04f, 0.035f, 0.035f), Display = V(0.08f, 0.16f, 0.36f), Patterns = HidePattern.Stripes | HidePattern.Bands },
                new Hide { Back = V(0.26f, 0.09f, 0.04f), Belly = V(0.42f, 0.27f, 0.15f), Pattern = V(0.07f, 0.025f, 0.02f), Display = V(0.36f, 0.3f, 0.06f), Patterns = HidePattern.Saddles | HidePattern.Bands | HidePattern.Mask },
            },
            Budget = 6500,
        };

        /// <summary>Little scavengers, after Compsognathus, that come in dozens: a long neck, a long tail and not much between them. Escaped broods of these are everywhere the Reich has been.</summary>
        public static readonly SaurianSpecies Skitter = new SaurianSpecies
        {
            Id = "skitter", Name = "Skitter", Form = SaurianForm.Theropod,
            Length = R(2.2f, 2.7f), Hip = R(0.2f, 0.23f), Skull = R(0.085f, 0.1f), Neck = R(0.16f, 0.18f), Trunk = R(0.19f, 0.22f), NeckRise = R(0.7f, 0.85f),
            Girth = R(0.058f, 0.066f), Breadth = R(0.75f, 0.85f), TailRod = 0.3f, TailRoot = R(0.6f, 0.7f),
            LegBones = new Vector3(0.33f, 0.4f, 0.27f), Crouch = R(0.42f, 0.52f), Thigh = R(0.62f, 0.72f), Toe = R(0.4f, 0.48f),
            Arm = R(0.55f, 0.65f), Fingers = 2, Claw = R(0.12f, 0.15f),
            SkullDepth = R(0.32f, 0.38f), SkullWidth = R(0.3f, 0.36f), Snout = R(0.28f, 0.34f), SnoutDepth = R(0.36f, 0.42f), Profile = R(-0.02f, 0.02f),
            Brow = R(0.01f, 0.02f), Eye = R(0.11f, 0.13f), Teeth = new IntRange(0, 0), Lips = 1f,
            QuillChance = 0.45f, Quills = R(0.03f, 0.045f),
            Hides = new[]
            {
                new Hide { Back = V(0.1f, 0.14f, 0.05f), Belly = V(0.3f, 0.32f, 0.16f), Pattern = V(0.03f, 0.05f, 0.02f), Display = V(0.4f, 0.3f, 0.05f), Patterns = HidePattern.Stripes | HidePattern.Bands | HidePattern.Mask },
                new Hide { Back = V(0.18f, 0.12f, 0.06f), Belly = V(0.36f, 0.28f, 0.16f), Pattern = V(0.05f, 0.03f, 0.02f), Display = V(0.36f, 0.08f, 0.04f), Patterns = HidePattern.Spots | HidePattern.Bands },
                new Hide { Back = V(0.08f, 0.1f, 0.09f), Belly = V(0.26f, 0.28f, 0.22f), Pattern = V(0.02f, 0.03f, 0.03f), Display = V(0.1f, 0.24f, 0.3f), Patterns = HidePattern.Mottle | HidePattern.Mask },
            },
            Budget = 2600,
        };

        /// <summary>
        /// A lean hunter after Dilophosaurus, taller than a person, with a pair of thin crests along its snout and a bright pouch under its throat. The Reich's blood-doctors gave these a venom they spit, which
        /// no fossil ever said they had.
        /// </summary>
        public static readonly SaurianSpecies Spitter = new SaurianSpecies
        {
            Id = "spitter", Name = "Spitter", Form = SaurianForm.Theropod,
            Length = R(8.4f, 9.4f), Hip = R(0.245f, 0.27f), Skull = R(0.085f, 0.095f), Neck = R(0.15f, 0.17f), Trunk = R(0.2f, 0.22f), NeckRise = R(0.85f, 1f),
            Girth = R(0.058f, 0.065f), Breadth = R(0.7f, 0.78f), TailRod = 0.25f, TailRoot = R(0.62f, 0.72f),
            LegBones = new Vector3(0.37f, 0.4f, 0.23f), Crouch = R(0.34f, 0.42f), Thigh = R(0.58f, 0.68f), Toe = R(0.36f, 0.42f),
            Arm = R(0.85f, 1f), Fingers = 3, Claw = R(0.12f, 0.15f),
            SkullDepth = R(0.3f, 0.35f), SkullWidth = R(0.24f, 0.29f), Snout = R(0.3f, 0.36f), SnoutDepth = R(0.42f, 0.5f), Profile = R(0.0f, 0.03f),
            Brow = R(0.015f, 0.03f), Crest = R(0.26f, 0.36f), Eye = R(0.06f, 0.07f), Teeth = new IntRange(9, 11), Tooth = R(0.04f, 0.05f), Lips = 0.7f,
            Throat = R(0.22f, 0.32f),
            Hides = new[]
            {
                new Hide { Back = V(0.12f, 0.13f, 0.08f), Belly = V(0.3f, 0.3f, 0.2f), Pattern = V(0.03f, 0.035f, 0.02f), Display = V(0.42f, 0.32f, 0.03f), Patterns = HidePattern.Stripes | HidePattern.Mask },
                new Hide { Back = V(0.15f, 0.1f, 0.12f), Belly = V(0.33f, 0.26f, 0.24f), Pattern = V(0.04f, 0.025f, 0.035f), Display = V(0.4f, 0.06f, 0.04f), Patterns = HidePattern.Spots | HidePattern.Bands },
                new Hide { Back = V(0.06f, 0.11f, 0.1f), Belly = V(0.22f, 0.28f, 0.24f), Pattern = V(0.015f, 0.03f, 0.03f), Display = V(0.08f, 0.22f, 0.42f), Patterns = HidePattern.Stripes | HidePattern.Bands },
            },
            Budget = 6500,
        };

        /// <summary>A heavy-skulled killer between Allosaurus and Carnotaurus: horns or crests over the eyes, short arms, and a deep, muscled head. What leads a Reich war-pack when a raptor won't do.</summary>
        public static readonly SaurianSpecies Ravager = new SaurianSpecies
        {
            Id = "ravager", Name = "Ravager", Form = SaurianForm.Theropod,
            Length = R(11.5f, 13f), Hip = R(0.24f, 0.26f), Skull = R(0.095f, 0.11f), Neck = R(0.1f, 0.12f), Trunk = R(0.23f, 0.25f), NeckRise = R(0.65f, 0.8f),
            Girth = R(0.072f, 0.08f), Breadth = R(0.74f, 0.84f), TailRod = 0.1f, TailRoot = R(0.72f, 0.82f),
            LegBones = new Vector3(0.39f, 0.38f, 0.23f), Crouch = R(0.24f, 0.32f), Thigh = R(0.6f, 0.7f), Toe = R(0.32f, 0.38f),
            Arm = R(0.4f, 0.58f), Fingers = 3, Claw = R(0.14f, 0.18f), ArmGirth = R(0.13f, 0.17f),
            SkullDepth = R(0.44f, 0.52f), SkullWidth = R(0.34f, 0.42f), Snout = R(0.38f, 0.46f), SnoutDepth = R(0.48f, 0.56f), Profile = R(0.0f, 0.04f),
            Brow = R(0.04f, 0.07f), BrowHorns = R(0.22f, 0.32f), Eye = R(0.05f, 0.06f), Teeth = new IntRange(11, 14), Tooth = R(0.06f, 0.075f), Lips = 0.45f, Jowls = R(0.03f, 0.06f),
            ScuteChance = 0.6f, Scutes = R(0.006f, 0.01f),
            Hides = new[]
            {
                new Hide { Back = V(0.07f, 0.06f, 0.055f), Belly = V(0.24f, 0.19f, 0.14f), Pattern = V(0.025f, 0.02f, 0.02f), Display = V(0.36f, 0.05f, 0.03f), Patterns = HidePattern.Stripes | HidePattern.Mottle },
                new Hide { Back = V(0.16f, 0.1f, 0.06f), Belly = V(0.34f, 0.26f, 0.16f), Pattern = V(0.05f, 0.03f, 0.02f), Display = V(0.4f, 0.24f, 0.04f), Patterns = HidePattern.Saddles | HidePattern.Bands },
                new Hide { Back = V(0.09f, 0.1f, 0.08f), Belly = V(0.26f, 0.26f, 0.2f), Pattern = V(0.03f, 0.03f, 0.025f), Display = V(0.32f, 0.08f, 0.1f), Patterns = HidePattern.Spots | HidePattern.Mask },
            },
            Budget = 9000,
        };

        /// <summary>
        /// The tyrant, after Tyrannosaurus: the widest, deepest head of anything that walked, eyes that look forward, arms that are hardly there and a tail as long as the rest of it. The Reich's commanders
        /// keep one each, and it's what holds a land. Teeth twelve to a side above and thirteen below, as Sue had.
        /// </summary>
        public static readonly SaurianSpecies Tyrant = new SaurianSpecies
        {
            Id = "tyrant", Name = "Tyrant", Form = SaurianForm.Theropod,
            // From Hartman's skeletal: the hip joint at under a quarter of its length, the shoulders lower than the hips, a short neck that climbs steeply to put the skull level with the hips, and a
            // keel of belly ribs hanging between the legs and the arms.
            Length = R(14.2f, 15.4f), Hip = R(0.232f, 0.245f), Skull = R(0.112f, 0.122f), Neck = R(0.088f, 0.098f), Trunk = R(0.22f, 0.24f), NeckRise = R(0.8f, 0.95f),
            Girth = R(0.074f, 0.08f), Breadth = R(0.72f, 0.8f), Belly = R(0.16f, 0.24f), BackSlope = R(-0.16f, -0.1f), NeckGirth = R(0.5f, 0.56f), TailRoot = R(0.74f, 0.82f), TailRod = 0f,
            LegBones = new Vector3(0.42f, 0.37f, 0.21f), Crouch = R(0.18f, 0.24f), Thigh = R(0.62f, 0.68f), Shin = R(0.38f, 0.44f), Toe = R(0.3f, 0.36f),
            Arm = R(0.42f, 0.48f), Fingers = 2, Claw = R(0.12f, 0.15f), ArmGirth = R(0.11f, 0.13f),
            SkullDepth = R(0.48f, 0.54f), SkullWidth = R(0.56f, 0.64f), Snout = R(0.36f, 0.42f), SnoutDepth = R(0.5f, 0.56f), Profile = R(0.02f, 0.05f),
            Brow = R(0.05f, 0.08f), Eye = R(0.045f, 0.052f), Teeth = new IntRange(12, 13), Tooth = R(0.085f, 0.1f), Lips = 0.3f, Jowls = R(0.05f, 0.08f),
            ScuteChance = 0.3f, Scutes = R(0.004f, 0.007f),
            Hides = new[]
            {
                new Hide { Back = V(0.12f, 0.1f, 0.07f), Belly = V(0.3f, 0.25f, 0.17f), Pattern = V(0.04f, 0.03f, 0.022f), Display = V(0.36f, 0.08f, 0.04f), Patterns = HidePattern.Stripes | HidePattern.Mottle },
                new Hide { Back = V(0.09f, 0.1f, 0.06f), Belly = V(0.27f, 0.26f, 0.17f), Pattern = V(0.03f, 0.035f, 0.02f), Display = V(0.38f, 0.28f, 0.05f), Patterns = HidePattern.Saddles | HidePattern.Mottle },
                new Hide { Back = V(0.08f, 0.075f, 0.075f), Belly = V(0.24f, 0.21f, 0.18f), Pattern = V(0.025f, 0.022f, 0.022f), Display = V(0.3f, 0.06f, 0.06f), Patterns = HidePattern.Stripes | HidePattern.Bands },
                new Hide { Back = V(0.18f, 0.11f, 0.06f), Belly = V(0.36f, 0.27f, 0.16f), Pattern = V(0.06f, 0.035f, 0.02f), Display = V(0.26f, 0.06f, 0.03f), Patterns = HidePattern.Saddles | HidePattern.Mask },
            },
            Budget = 15000,
        };

        /// <summary>
        /// A horned grazer after Triceratops and its kin, smaller and meaner than any of them: a beak, horns over the eyes and on the nose, and a frill that's solid on some and spiked on others. The Reich
        /// drives these at walls, and walls go.
        /// </summary>
        public static readonly SaurianSpecies Hornback = new SaurianSpecies
        {
            Id = "hornback", Name = "Hornback", Form = SaurianForm.Horned,
            Length = R(9.4f, 10.6f), Hip = R(0.27f, 0.3f), Skull = R(0.2f, 0.23f), Neck = R(0.05f, 0.06f), Trunk = R(0.36f, 0.4f),
            Girth = R(0.13f, 0.145f), Breadth = R(0.95f, 1.08f), Belly = R(0.18f, 0.26f), BackSlope = R(-0.12f, -0.05f), NeckRise = R(0.05f, 0.2f), NeckGirth = R(0.62f, 0.7f),
            TailRoot = R(0.5f, 0.6f), TailRod = 0f, TailDroop = R(0.1f, 0.18f),
            LegBones = new Vector3(0.44f, 0.36f, 0.2f), Crouch = R(0.12f, 0.18f), Thigh = R(0.5f, 0.58f), Shin = R(0.28f, 0.33f), Toe = R(0.26f, 0.32f),
            Arm = R(0.88f, 0.96f), Fingers = 5, Claw = R(0.05f, 0.07f), ArmGirth = R(0.26f, 0.32f),
            SkullDepth = R(0.42f, 0.5f), SkullWidth = R(0.42f, 0.5f), Snout = R(0.28f, 0.34f), SnoutDepth = R(0.5f, 0.6f), Profile = R(0.02f, 0.06f),
            Brow = R(0.02f, 0.04f), BrowHorns = R(0.32f, 0.5f), Eye = R(0.04f, 0.05f), Teeth = new IntRange(0, 0), Lips = 1f,
            Beak = R(0.08f, 0.14f), NoseHorn = R(0.1f, 0.3f), Frill = R(0.55f, 0.8f), FrillSpikes = R(0f, 0.28f), FrillPoints = new IntRange(5, 9),
            Hides = new[]
            {
                new Hide { Back = V(0.16f, 0.13f, 0.08f), Belly = V(0.32f, 0.27f, 0.18f), Pattern = V(0.05f, 0.04f, 0.025f), Display = V(0.42f, 0.12f, 0.04f), Patterns = HidePattern.Stripes | HidePattern.Mottle },
                new Hide { Back = V(0.11f, 0.12f, 0.09f), Belly = V(0.28f, 0.28f, 0.2f), Pattern = V(0.035f, 0.04f, 0.03f), Display = V(0.4f, 0.32f, 0.06f), Patterns = HidePattern.Saddles | HidePattern.Mottle },
                new Hide { Back = V(0.2f, 0.11f, 0.07f), Belly = V(0.36f, 0.25f, 0.17f), Pattern = V(0.07f, 0.035f, 0.025f), Display = V(0.12f, 0.2f, 0.36f), Patterns = HidePattern.Spots },
            },
            Budget = 10000,
        };

        /// <summary>
        /// The Haven's giants, after Giraffatitan and Brachiosaurus: forelegs longer than the hind, so the back climbs from the hips to the shoulders, a neck as long as everything between it and
        /// the tail, carried steeply up as a giraffe's is (Taylor, Wedel and Naish 2009), and a small head with a dome over the nostrils. Bigger than anything the Reich has, at about 1.7 times life,
        /// so that a head held up is over the tallest trees there are. The only kind it never had: the Haven's own, and free. Read LORE.md.
        /// </summary>
        public static readonly SaurianSpecies Giant = new SaurianSpecies
        {
            Id = "giant", Name = "Giant", Form = SaurianForm.Sauropod, Free = true,
            // From Taylor's and Paul's skeletals: the hip joint at about a sixth of the length, the shoulder a quarter higher, and the tail a little shorter than the neck. The head is half as big
            // again as the skull says, as it's what the animal's face is read from, from up where the camera is.
            Length = R(37f, 41f), Hip = R(0.174f, 0.184f), Withers = R(0.2f, 0.28f), Skull = R(0.058f, 0.064f), Neck = R(0.36f, 0.39f), Trunk = R(0.19f, 0.21f),
            Girth = R(0.062f, 0.068f), Breadth = R(0.98f, 1.06f), Belly = R(0.08f, 0.14f), NeckRise = R(1f, 1.12f), NeckGirth = R(0.5f, 0.56f), TailRoot = R(0.6f, 0.68f), TailRod = 0f, TailDroop = R(0.2f, 0.28f),
            LegBones = new Vector3(0.53f, 0.33f, 0.14f), Crouch = R(0.02f, 0.06f), Thigh = R(0.46f, 0.52f), Shin = R(0.34f, 0.4f), Toe = R(0.5f, 0.65f),
            ArmGirth = R(0.4f, 0.46f), Fingers = 1, Claw = R(0.05f, 0.07f),
            // A tall dome just in front of the eyes, and a blunt snout below it. Its lips cover what few teeth it has, as a browser's do, so it's a face with no teeth showing.
            SkullDepth = R(0.27f, 0.31f), SkullWidth = R(0.4f, 0.45f), Snout = R(0.84f, 0.92f), SnoutDepth = R(0.76f, 0.84f), Profile = R(0f, 0.02f),
            Brow = R(0.02f, 0.035f), Eye = R(0.08f, 0.092f), Teeth = new IntRange(0, 0), Lips = 1f, Jowls = R(0.02f, 0.04f), Arch = R(0.27f, 0.34f),
            // Dark and soft, as a grazer's are: nothing about them hunts.
            Eyes = new[] { V(0.11f, 0.06f, 0.025f), V(0.16f, 0.1f, 0.035f), V(0.09f, 0.07f, 0.04f) },
            Hides = new[]
            {
                new Hide { Back = V(0.14f, 0.115f, 0.095f), Belly = V(0.33f, 0.28f, 0.21f), Pattern = V(0.075f, 0.06f, 0.05f), Display = V(0.3f, 0.14f, 0.08f), Patterns = HidePattern.Saddles | HidePattern.Mottle, Contrast = 0.55f },
                new Hide { Back = V(0.105f, 0.125f, 0.095f), Belly = V(0.3f, 0.3f, 0.22f), Pattern = V(0.055f, 0.07f, 0.05f), Display = V(0.3f, 0.22f, 0.08f), Patterns = HidePattern.Stripes | HidePattern.Mottle, Contrast = 0.45f },
                new Hide { Back = V(0.105f, 0.11f, 0.12f), Belly = V(0.29f, 0.28f, 0.26f), Pattern = V(0.06f, 0.06f, 0.068f), Display = V(0.27f, 0.15f, 0.16f), Patterns = HidePattern.Spots | HidePattern.Mottle, Contrast = 0.5f },
                new Hide { Back = V(0.16f, 0.1f, 0.07f), Belly = V(0.35f, 0.26f, 0.18f), Pattern = V(0.085f, 0.055f, 0.04f), Display = V(0.16f, 0.17f, 0.2f), Patterns = HidePattern.Saddles | HidePattern.Bands, Contrast = 0.5f },
            },
            Budget = 50000,
        };

        public static readonly SaurianSpecies[] All = { Raptor, Skitter, Spitter, Ravager, Tyrant, Hornback, Giant };

        public static SaurianSpecies? Find(string id) => Array.Find(All, species => species.Id == id);
    }
}

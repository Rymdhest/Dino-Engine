using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.Modelling.Procedural.Saurian;
using OpenTK.Mathematics;
using System.Globalization;

namespace Dino_Engine.Modelling.Procedural.Character
{
    /// <summary>What a skeleton has in its hands, which is how it carries itself and what it swings.</summary>
    public enum SkeletonArms
    {
        Nothing,
        SwordAndShield,
        Sword,
        Spear,
        Bow,
    }

    /// <summary>What a skeleton was buried in, and still has on: nothing, a hood of rotten cloth, or a rusted iron cap.</summary>
    public enum SkeletonHeadwear
    {
        None,
        Hood,
        Cap,
    }

    /// <summary>
    /// Which skeleton a model's name means. "skeleton" and "skeleton:bow" are the first of each, "skeleton3" and "skeleton:bow2" others, each grown from a seed of its own, so a name is all it takes
    /// to grow the same one again on any machine and any thread. What one of the melee dead holds is part of who it was, drawn from its seed.
    /// </summary>
    public readonly record struct SkeletonSpec(int Variant, ulong Seed, SkeletonArms Arms, bool Archer)
    {
        public const string Prefix = "skeleton";
        public const string ArcherPrefix = "skeleton:bow";

        /// <summary>The skeleton a name means, or nothing if it doesn't name one.</summary>
        public static SkeletonSpec? Parse(string key)
        {
            bool archer = key.StartsWith(ArcherPrefix, StringComparison.Ordinal);
            if (!archer && !key.StartsWith(Prefix, StringComparison.Ordinal)) return null;
            int length = archer ? ArcherPrefix.Length : Prefix.Length;
            int variant = 0;
            if (key.Length > length && !int.TryParse(key.AsSpan(length), NumberStyles.None, CultureInfo.InvariantCulture, out variant)) return null;
            ulong seed = SeedOf(archer ? 0xB0_5EEDUL : 0x5CE1_E70AUL, (ulong)variant);
            SkeletonArms arms = archer ? SkeletonArms.Bow : (SkeletonArms)(1 + new Genes(seed).Pick("arms", 0.55f, 0.2f, 0.25f));
            return new SkeletonSpec(variant, seed, arms, archer);
        }

        /// <summary>
        /// What a hero raises, holding whatever its page gave it (Outfit.Hold) and nothing of its own. Which of the dead it was comes from the name of its model, so Minions holding different things
        /// aren't all the same one.
        /// </summary>
        public static SkeletonSpec Risen(string key)
        {
            ulong hash = 0xCBF29CE484222325UL;
            foreach (char c in key) hash = (hash ^ c) * 0x100000001B3UL;
            int variant = (int)(hash % 8);
            return new SkeletonSpec(variant, SeedOf(0x5CE1_E70AUL, (ulong)variant), SkeletonArms.Nothing, false);
        }

        private static ulong SeedOf(ulong kind, ulong variant)
        {
            ulong seed = (kind ^ variant) * 0x100000001B3UL;
            return (seed ^ (seed >> 29)) * 0xBF58476D1CE4E5B9UL;
        }

        /// <summary>The name its model is kept by, which every way of writing it comes to.</summary>
        public string Key => $"{(Archer ? ArcherPrefix : Prefix)}{Variant}";
    }

    /// <summary>
    /// One of the land's dead, as the Ossuary's digging stirred it up: who it was, drawn from its seed, in the rig's space (the ground at y = 0, facing +z, +x on its left, a person's joints where
    /// <see cref="Animation.Rig.Humanoid"/> has them, as <see cref="BodyForm"/> measures them). Every joint is where a person's is, so a skeleton shares every animation, and what changes from one to
    /// the next is how heavy its bones are, the shape of its skull, how long it lay in the earth, what's broken or gone, and what it was buried in.
    /// </summary>
    public sealed class SkeletonBody
    {
        // A person's joints, which a skeleton can't move: Rig.Humanoid's.
        public const float ShoulderY = BodyForm.Shoulder;
        public const float ShoulderX = BodyForm.ShoulderX;
        public const float ElbowX = BodyForm.ElbowX;
        public const float WristX = BodyForm.WristX;
        public const float HipX = BodyForm.HipX;
        public const float Hip = BodyForm.Hip;
        public const float Knee = BodyForm.Knee;
        public const float Ankle = 0.24f;

        /// <summary>The light the Ossuary puts where the eyes and the heart were, and all that moves the dead (LORE.md): cyan-green, the Ossuary's bones' and nobody else's.</summary>
        public static readonly Vector3 Light = new Vector3(0.3f, 0.95f, 0.85f);

        public readonly SkeletonSpec Spec;
        public readonly Genes Genes;

        /// <summary>How heavy its bones are, about 1: a big man's are a fifth thicker than a slight woman's.</summary>
        public readonly float Stout;

        /// <summary>Its skull's size, how wide its face and jaw are, and how heavy its brow.</summary>
        public readonly float SkullSize, FaceWidth, Brow;

        /// <summary>How far its jaw hangs open, in radians.</summary>
        public readonly float Gape;

        /// <summary>Bone, bleached or as dark as the earth it lay in, as an albedo under a sun of about 13: anything paler goes white.</summary>
        public readonly Vector3 Bone;

        /// <summary>What it was buried in: cloth gone the colour of earth, the leather of a belt, and iron gone to rust.</summary>
        public readonly Vector3 Cloth, Leather, Rust;

        public readonly SkeletonHeadwear Headwear;
        public readonly bool Rags, Belt, Pauldron, Arrow;

        /// <summary>A rib snapped off, as a blow in life or the spade that dug it up left it, and on which side, or -1 for none.</summary>
        public readonly int BrokenRib;
        public readonly float BrokenSide;

        public SkeletonBody(SkeletonSpec spec)
        {
            Spec = spec;
            Genes = new Genes(spec.Seed);
            Genes look = Genes.Part("look");

            Stout = look.Around("stout", 1f, 0.07f);
            SkullSize = look.Around("skull", 1f, 0.025f);
            FaceWidth = look.Around("face", 1f, 0.05f);
            Brow = look.Unit("brow");
            Gape = look.Range("gape", 0.02f, 0.16f);

            // Some came out of a dry barrow nearly white, and some out of wet clay the colour of it.
            float earth = MathF.Pow(look.Unit("earth"), 1.3f);
            Bone = Vector3.Lerp(new Vector3(0.47f, 0.43f, 0.35f), new Vector3(0.29f, 0.24f, 0.17f), earth);

            Genes grave = Genes.Part("grave");
            Headwear = (SkeletonHeadwear)grave.Pick("head", 0.45f, 0.25f, 0.3f);
            Rags = grave.Chance("rags", 0.55f);
            Belt = grave.Chance("belt", 0.45f);
            Pauldron = grave.Chance("pauldron", 0.2f);
            Arrow = grave.Chance("arrow", 0.18f);
            BrokenRib = grave.Chance("broken", 0.35f) ? grave.Count("rib", 1, 5) : -1;
            BrokenSide = grave.Sign("side");

            // Undyed linen gone grey, earth brown, moss-stained green or a faded blue-grey: what's been in the ground for a hundred years, and never the Reich's oxblood.
            Vector3[] shrouds =
            {
                new Vector3(0.34f, 0.31f, 0.25f),
                new Vector3(0.22f, 0.17f, 0.12f),
                new Vector3(0.2f, 0.21f, 0.15f),
                new Vector3(0.18f, 0.19f, 0.22f),
            };
            Cloth = GearColours.ClearOfReich(shrouds[grave.Pick("cloth", 0.35f, 0.3f, 0.2f, 0.15f)] * grave.Range("fade", 0.85f, 1.1f), false);
            Leather = GearColours.ClearOfReich(new Vector3(0.17f, 0.11f, 0.07f) * grave.Range("leather", 0.8f, 1.15f), false);
            Rust = GearColours.ClearOfReich(Vector3.Lerp(new Vector3(0.32f, 0.17f, 0.09f), new Vector3(0.22f, 0.15f, 0.11f), grave.Unit("rust")), true);
        }

        /// <summary>The skull's middle, which everything of the head is laid out from: above where the neck meets it, a little behind the middle of the head.</summary>
        public static readonly Vector3 SkullAt = new Vector3(0f, 4.46f, -0.04f);

        // Where the backbone runs, front to back, by height: forward in the small of the back, back behind the chest, and forward again up the neck, as a spine is an S.
        private static readonly (float Y, float Z)[] SpineLine =
        {
            (2.0f, -0.18f), (2.35f, -0.11f), (2.75f, -0.09f), (3.1f, -0.16f), (3.5f, -0.21f), (3.82f, -0.17f), (4.0f, -0.1f), (4.18f, -0.06f),
        };

        /// <summary>How far forward the middle of the backbone is at a height.</summary>
        public static float SpineZ(float y)
        {
            if (y <= SpineLine[0].Y) return SpineLine[0].Z;
            for (int i = 1; i < SpineLine.Length; i++)
            {
                if (y > SpineLine[i].Y) continue;
                float t = (y - SpineLine[i - 1].Y) / (SpineLine[i].Y - SpineLine[i - 1].Y);
                t = t * t * (3f - 2f * t);
                return SpineLine[i - 1].Z + (SpineLine[i].Z - SpineLine[i - 1].Z) * t;
            }
            return SpineLine[^1].Z;
        }

        /// <summary>
        /// One bone's colour: the body's, a little lighter or darker than its neighbours, as no two bones lie in the same earth. A name of its own for each, so that adding a bone moves no other's.
        /// </summary>
        public Vector3 Tint(string bone) => Bone * Genes.Part("tint").Range(bone, 0.9f, 1.07f);

        // ---- Which bones what's made hangs from.

        /// <summary>
        /// All of it on one bone. Everything a skeleton is made of is rigid, and has to stay so: a vertex wholly on the chest or head is spread up and down the back by height (BodyForm.Spread), which
        /// would bend a rib, so it's given the same bone twice, which nothing spreads.
        /// </summary>
        public static Hang On(BoneType bone) => new Hang(bone, bone, 0.99f);

        /// <summary>A whole piece of the back where it is at this height (BodyForm.Back), so that every rib and vertebra moves with the back as one piece.</summary>
        public static Hang Back(float y) => From(BodyForm.Back(y));

        /// <summary>A whole piece of the neck where it is at this height (BodyForm.Throat).</summary>
        public static Hang Throat(float y) => From(BodyForm.Throat(y));

        public static Hang From((Vector2 bones, Vector2 weights) skin)
        {
            var first = (BoneType)(int)skin.bones.X;
            if (skin.weights.Y <= 0f) return On(first);
            return new Hang(first, (BoneType)(int)skin.bones.Y, skin.weights.X);
        }
    }
}

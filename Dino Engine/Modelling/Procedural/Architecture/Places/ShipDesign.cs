using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Places
{
    /// <summary>
    /// A ship, from its seed: its lines, what it's built of and where it broke, which the wreck's layout and every part grown of it read alike, so a bow and a stern a dozen metres apart on
    /// the sand are plainly one ship (PLACES.md). The Reich's is one of its iron ships, timber clad in riveted black iron with an oxblood band and brass along her rails; the land's own is
    /// planked and painted in one of the land's colours. Its frame: x across it (+x to starboard), y up from the bottom of its keel, z along it with the bow towards +z. Everything is at the
    /// world's scale, twice life. Headless.
    /// </summary>
    public sealed class ShipDesign
    {
        public readonly uint Seed;
        public readonly bool Reich;
        public readonly float Length;       // Stem to stern.
        public readonly float Beam;         // At her widest.
        public readonly float Depth;        // Keel to deck, amidships.
        public readonly float Sheer;        // How much higher her deck is at the ends.
        public readonly float Transom;      // Her stern's width, of her beam.
        public readonly float Fullness;     // How full her bilge is, 0 slack to 1 hard.
        public readonly float BreakAt;      // Where along her she broke, from the stern, 0 to 1.
        public readonly float Gap;          // How much of her the sea took at the break, as a share of her length.
        public readonly float[] Masts;      // Where her masts were stepped, along her from the stern.
        public readonly Vector3 Paint;      // The land's: her topsides' colour.
        public readonly Vector3 Band;       // The land's: the band along her.

        private ShipDesign(uint seed, bool reich)
        {
            Seed = seed;
            Reich = reich;
            var random = new WorldRandom(seed).Fork("ship");
            Length = random.Range(30f, 38f) * (reich ? 1.05f : 0.95f);
            Beam = Length * random.Range(0.25f, 0.3f);
            Depth = random.Range(5f, 6.2f) * (reich ? 1.06f : 1f);
            Sheer = random.Range(0.9f, 1.9f);
            Transom = random.Range(0.52f, 0.68f);
            Fullness = random.Range(0.35f, 0.8f);
            BreakAt = random.Range(0.4f, 0.56f);
            Gap = random.Range(0.08f, 0.14f);
            int masts = random.Chance(reich ? 0.6f : 0.35f) ? 3 : 2;
            Masts = masts == 3 ? new[] { 0.22f, 0.5f, 0.76f } : new[] { 0.3f, 0.68f };
            Paint = PlaceMaterials.BoatPaints[random.Int(PlaceMaterials.BoatPaints.Length)];
            Band = random.Chance(0.6f) ? PlaceMaterials.BoatWhite : PlaceMaterials.BoatPaints[random.Int(PlaceMaterials.BoatPaints.Length)] * 1.2f;
        }

        public static ShipDesign Of(uint seed, bool reich) => new ShipDesign(seed, reich);

        /// <summary>How much of her beam she is at <paramref name="x"/> along her (0 her stern, 1 her stem) and <paramref name="y"/> up her side (0 her keel, 1 her deck), each side of her middle.</summary>
        public float HalfBreadth(float x, float y)
        {
            x = Math.Clamp(x, 0f, 1f);
            y = Math.Clamp(y, 0f, 1f);
            // Her waterplane: a transom stern, widest a little abaft the middle, fining to her stem.
            float plan = x < 0.42f ? MathHelper.Lerp(Transom, 1f, Smooth(x / 0.42f)) : MathF.Pow(MathF.Max(0f, 1f - MathF.Pow((x - 0.42f) / 0.58f, 2.1f)), 0.62f);
            // Her section: a keel, a bilge that turns hard or slack, sides that come in a little at the top; finer towards her ends.
            float fine = 1f + 1.6f * MathF.Pow(MathF.Abs(x - 0.45f) / 0.55f, 1.6f);
            float turn = 0.58f + 0.1f * (1f - Fullness);
            float section = y < turn ? Keel / (Beam * 0.5f) + (1f - Keel / (Beam * 0.5f)) * (1f - MathF.Pow(1f - y / turn, 1.6f + Fullness * 1.4f * fine * 0.6f)) : 1f - 0.05f * MathF.Pow((y - turn) / (1f - turn), 2f);
            section = MathF.Pow(Math.Clamp(section, 0f, 1f), y < turn ? fine * 0.55f + 0.45f : 1f);
            return MathF.Max(Beam * 0.5f * plan * section, x > 0.985f ? 0f : 0.05f);
        }

        /// <summary>Half the width of her keel.</summary>
        public const float Keel = 0.28f;

        /// <summary>How high the bottom of her keel is at <paramref name="x"/>: level, but where it rises into her forefoot and is cut up under her stern.</summary>
        public float KeelAt(float x) => x > 0.86f ? MathF.Pow((x - 0.86f) / 0.14f, 2f) * Depth * 0.72f : x < 0.08f ? (0.08f - x) / 0.08f * Depth * 0.28f : 0f;

        /// <summary>How high her deck's edge is at <paramref name="x"/>.</summary>
        public float DeckAt(float x) => Depth + Sheer * MathF.Pow(MathF.Abs(x - 0.47f) / 0.53f, 2f);

        /// <summary>A point of her side: <paramref name="side"/> 1 to starboard and -1 to port, <paramref name="inset"/> metres in from her skin.</summary>
        public Vector3 Point(float x, float y, float side, float inset = 0f)
        {
            float keel = KeelAt(x), deck = DeckAt(x);
            float half = MathF.Max(HalfBreadth(x, y) - inset, 0.02f);
            float lift = inset * (1f - y) * 0.8f;
            return new Vector3(side * half, keel + (deck - keel) * y + lift, (x - 0.5f) * Length);
        }

        /// <summary>Where along her (as a share) a point of her length is.</summary>
        public float ShareAt(float z) => z / Length + 0.5f;

        /// <summary>The ends of her two parts along her, from the stern: the stern's runs from 0 to <see cref="SternEnd"/>, the bow's from <see cref="BowStart"/> to 1.</summary>
        public float SternEnd => BreakAt - Gap * 0.5f;
        public float BowStart => BreakAt + Gap * 0.5f;

        /// <summary>A part's own middle along her, which is where it's placed from, and its length.</summary>
        public float MiddleOf(int part) => part == 0 ? (BowStart + 1f) * 0.5f : SternEnd * 0.5f;
        public float LengthOf(int part) => (part == 0 ? 1f - BowStart : SternEnd) * Length;

        private static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}

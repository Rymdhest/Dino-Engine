using OpenTK.Mathematics;
using static Dino_Engine.World.WorldMath;

namespace Dino_Engine.World
{
    /// <summary>
    /// The picture behind the main menu, laid out once and for all (MENU.md). A person settled on it: the hero in front with their back to the camera, looking out over a meadow of long grass that
    /// runs down to a lake, the Haven's giants grazing a meadow on its far shore, woods all round, hills rising behind the woods and a mountain far off beyond them. Every other land is drawn from
    /// a seed and searched for somewhere to stand, so every change to how lands grow moved the picture; this one is composed, and only its noise comes from the seed, so it stays where it is.
    /// The camera looks north (+z), as the game's does, so the mountain shows its lit south face. Headless, and the same on any thread.
    /// </summary>
    public sealed class Vista
    {
        /// <summary>Metres a side.</summary>
        public const float Size = 1280f;

        /// <summary>Where the hero stands, and the way to the camera from them: it's behind them, to the south.</summary>
        public static readonly Vector2 Hero = new Vector2(640f, 272f);
        public static readonly Vector2 Facing = -Vector2.UnitY;

        /// <summary>The lake: its middle and its half-widths across and along the view.</summary>
        public static readonly Vector2 Lake = new Vector2(676f, 366f);
        public static readonly Vector2 LakeHalf = new Vector2(122f, 44f);

        /// <summary>The meadow on the far shore, where the giants graze, and how far they wander from its middle.</summary>
        public static readonly Vector2 Herd = new Vector2(648f, 440f);
        public const float HerdReach = 30f;

        /// <summary>The mountain, far off a little to the right of the middle of the picture, and how wide its foot is.</summary>
        public static readonly Vector2 Mountain = new Vector2(560f, 1000f);
        public const float MountainRadius = 200f;

        private static readonly Vector2 Meadow = new Vector2(648f, 300f);
        private static readonly Vector2 MeadowHalf = new Vector2(74f, 52f);
        private static readonly Vector2 HerdHalf = new Vector2(100f, 30f);

        private const float LakeFloor = 3.2f;       // Metres below the water at its deepest.
        private const float StandHeight = 8.5f;     // Of the ground where the hero stands, high enough over the water to see across it.
        private const float HeroAboveShore = 48f;   // Metres from the water at which the meadow stops falling, which is just in front of the hero.
        private const float FarShore = 2.2f;        // Of the meadow across the water.
        private const float HillsRise = 46f;        // What the land behind the far woods climbs to.
        private const float RimWidth = 150f;        // Metres in from the edge of the world over which it closes off.

        private readonly WorldNoise _noise;

        public Vista(WorldRandom random)
        {
            _noise = new WorldNoise(random.Fork("vista"));
        }

        /// <summary>How far inside the lake's shore a point is, in metres, and less than nothing outside it, along a shore that wanders as a real one does.</summary>
        public float InLake(float x, float z)
        {
            Vector2 warp = _noise.Warp(x + 71f, z - 13f, 90f, 9f);
            return Inside(new Vector2(x, z) + warp, Lake, LakeHalf) + _noise.Fbm(x - 300f, z + 41f, 60f, 3) * 4f;
        }

        /// <summary>Land above nothing, water below: the land's outline.</summary>
        public float Outline(float x, float z) => -InLake(x, z);

        /// <summary>Metres above the water.</summary>
        public float Height(float x, float z)
        {
            float lake = InLake(x, z);
            if (lake > 0f) return -0.15f - LakeFloor * Smoothstep(0f, 26f, lake);

            float shore = -lake;
            float rolling = _noise.Fbm(x, z, 180f, 4) * 0.5f + _noise.Fbm(x + 77f, z - 31f, 60f, 3) * 0.15f;

            // In front of the water, the meadow falls away from the hero's feet, steeply and then more gently down to the shore, so that the long grass never hides the water: ground that
            // fell gently first would be a brow, and the grass on it a horizon. Across the water the far meadow lies low and level, and behind that the land climbs into hills.
            float fall = Saturate(shore / HeroAboveShore);
            float near = 0.4f + (StandHeight - 0.4f) * fall * fall + rolling * 2.5f * Smoothstep(HeroAboveShore + 10f, HeroAboveShore + 60f, shore);
            float behind = FarShore + HillsRise * Smoothstep(470f, 820f, z) + rolling * 22f * Smoothstep(450f, 640f, z) + Smoothstep(0f, 24f, shore) * 0.8f;
            float side = MathF.Abs(x - Lake.X);
            float flanks = 3f + 10f * Smoothstep(160f, 420f, side) + rolling * 6f;

            float height = Lerp(near, behind, Smoothstep(Lake.Y - 50f, Lake.Y + 30f, z));
            // Away to either side the land rises a little, so the picture is held between low hills.
            float edge = Smoothstep(LakeHalf.X + 20f, LakeHalf.X + 140f, side);
            height = Lerp(height, MathF.Max(height, flanks), edge);
            return MathF.Max(height, 0.25f);
        }

        /// <summary>
        /// Where the land closes off and rock stands, 0..1: the mountain, and a wall round the edge of the world, highest across the north, where it's a range behind the mountain. What
        /// <c>MountainStage</c> raises from, so the mountain is a real one, its ridges and valleys what water would cut.
        /// </summary>
        public float Rock(float x, float z)
        {
            Vector2 warp = _noise.Warp(x - 211f, z + 97f, 260f, 60f);
            Vector2 p = new Vector2(x, z) + warp;
            float mountain = 1f - Smoothstep(MountainRadius * 0.35f, MountainRadius, (p - Mountain).Length);
            float border = MathF.Min(MathF.Min(x, Size - x), MathF.Min(z, Size - z));
            float rim = 1f - Smoothstep(0f, RimWidth, border + _noise.Fbm(x + 900f, z - 400f, 200f, 3) * 50f);
            // The north edge is far enough off to stand as low peaks behind the mountain, which is what the eye goes to; nearer edges stay low, and out of the picture.
            float north = Smoothstep(Size - 170f, Size - 40f, z + _noise.Fbm(x - 50f, z, 160f, 3) * 50f);
            return MathF.Max(mountain, MathF.Max(rim * 0.7f, north * 0.8f));
        }

        /// <summary>
        /// How thickly trees stand, 0..1: woods everywhere but the two meadows and the water, the woods' edges broken, and unbroken over the hills, as a wood is seen from far off.
        /// </summary>
        public float Woods(float x, float z)
        {
            var p = new Vector2(x, z);
            Vector2 warp = _noise.Warp(x + 5f, z - 9f, 70f, 12f);
            float meadow = Smoothstep(-14f, 6f, Inside(p + warp, Meadow, MeadowHalf));
            float herd = Smoothstep(-18f, 4f, Inside(p + warp, Herd, HerdHalf));
            float shore = Smoothstep(-16f, 0f, InLake(x, z));
            float open = MathF.Max(MathF.Max(meadow, herd), shore);
            return Saturate(1f - open);
        }

        // How far inside an ellipse a point is, in metres near its edge.
        private static float Inside(Vector2 p, Vector2 centre, Vector2 half)
        {
            Vector2 d = (p - centre) / half;
            float length = d.Length;
            return (1f - length) * MathF.Min(half.X, half.Y) * (length > 1f ? 1.6f : 1f);
        }
    }
}

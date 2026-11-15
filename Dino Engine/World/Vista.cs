using OpenTK.Mathematics;
using static Dino_Engine.World.WorldMath;

namespace Dino_Engine.World
{
    /// <summary>
    /// The picture behind the main menu, laid out once and for all (MENU.md). A person settled on it: the hero in front with their back to the camera, looking out over a meadow of long grass that
    /// runs down to a lake, the Haven's giants grazing a meadow on its far shore, woods all round, hills rising behind the woods and a mountain far off beyond them. Every other land is drawn from
    /// a seed and searched for somewhere to stand, so every change to how lands grow moved the picture; this one is composed, and only its noise comes from the seed, so it stays where it is.
    /// The camera looks north (+z), as the game's does. It's laid out for depth, as a person asked that the giants look a long way off: the water between is broad, the land behind them climbs in
    /// ridges one behind another with low ground between for the air to lie in, and the sun is low ahead and to the right (the archetype's light), so that every one of those is a paler shape
    /// behind a darker. Headless, and the same on any thread.
    /// </summary>
    public sealed class Vista
    {
        /// <summary>Metres a side.</summary>
        public const float Size = 1280f;

        /// <summary>Where the hero stands, and the way to the camera from them: it's behind them, to the south.</summary>
        public static readonly Vector2 Hero = new Vector2(640f, 272f);
        public static readonly Vector2 Facing = -Vector2.UnitY;

        /// <summary>
        /// The lake: its middle and its half-widths across and along the view. All of it has to lie inside the square of terrain the menu keeps (<c>TitleScene.ChunkRadius</c>), as water is only
        /// drawn over that.
        /// </summary>
        public static readonly Vector2 Lake = new Vector2(664f, 396f);
        public static readonly Vector2 LakeHalf = new Vector2(136f, 74f);

        /// <summary>
        /// The meadow on the far shore, where the giants graze, and how far they wander from its middle: some 250 m from the camera, which is far enough to be a long way off and near
        /// enough to be seen for what they are, and to the left of where the camera looks by enough that none of them is ever behind the hero, who fills the picture from a little
        /// right of its middle, nor under the menu's words.
        /// </summary>
        public static readonly Vector2 Herd = new Vector2(656f, 516f);
        public const float HerdReach = 30f;

        /// <summary>The mountain, far off a little to the right of the middle of the picture, and how wide its foot is.</summary>
        public static readonly Vector2 Mountain = new Vector2(560f, 1000f);
        public const float MountainRadius = 200f;

        /// <summary>The level of the water, which the morning's mist lies on.</summary>
        public const float WaterLevel = WorldPlan.SeaLevel;

        /// <summary>
        /// Where the sun stands (<c>WorldMood</c>'s azimuth is the way to it round from +x through +z, and its height a share of the way up to overhead): ahead of the camera and 28 degrees to its
        /// right, 12.5 up. That's past the mountain's right shoulder and a few degrees over the ridges there, so nothing in the land stands between it and the picture, and inside the
        /// picture's right edge on any screen, over the hero's shoulder and away from the menu's words.
        /// </summary>
        public const float SunAzimuth = MathF.PI * 0.5f + 0.49f;
        public const float SunHeight = 0.139f;

        /// <summary>
        /// Whether somewhere lies between the camera and the water, in the middle of what it sees. Nothing taller than the grass grows there (<c>VegetationStage</c>): shrubs and lone
        /// trees are scattered by chance, and one that came up there stood in front of the lake and the giants in it. Out to the sides they stay, where they frame the picture.
        /// </summary>
        public static bool InSight(Vector2 at)
        {
            Vector2 off = at - (Hero + Facing * SightFrom);
            float ahead = Vector2.Dot(off, -Facing);
            if (ahead < 0f || ahead > Lake.Y - LakeHalf.Y + SightPastShore - Hero.Y + SightFrom) return false;
            float across = MathF.Abs(off.X * Facing.Y - off.Y * Facing.X);
            return across < SightBeside + ahead * SightSpread;
        }

        private const float SightFrom = 12f;        // Metres behind the hero that it's reckoned from, which is where the camera is.
        private const float SightBeside = 5f;       // Metres to either side of the camera that it's kept clear at the camera,
        private const float SightSpread = 0.5f;     // and how much wider for every metre ahead: 27 degrees either side, most of a picture four wide to three high.
        private const float SightPastShore = 14f;   // Metres past the near shore that it reaches, where only reeds stand.

        private static readonly Vector2 Meadow = new Vector2(648f, 300f);
        private static readonly Vector2 MeadowHalf = new Vector2(74f, 52f);
        private static readonly Vector2 HerdHalf = new Vector2(112f, 34f);

        private const float LakeFloor = 3.2f;       // Metres below the water at its deepest.
        private const float StandHeight = 8.5f;     // Of the ground where the hero stands, high enough over the water to see across it.
        private const float HeroAboveShore = 48f;   // Metres from the water at which the meadow stops falling, which is just in front of the hero.
        private const float FarShore = 2.2f;        // Of the meadow across the water.
        private const float RimWidth = 150f;        // Metres in from the edge of the world over which it closes off.

        // The ridges behind the far meadow, nearest first: how far north each crest runs, how wide it is from foot to foot (as a bell is), how high it stands and how far its line wanders.
        // Each has to show over the one before from where the camera is, 13 m over the water, and the mountain over them all, and none may stand so near the far meadow or so high that
        // its shadow lies over the giants with the sun where it is: the first one's ends at the trees behind them.
        private static readonly (float At, float Width, float Height, float Wander)[] Ridges =
        {
            (690f, 46f, 20f, 28f),
            (830f, 58f, 42f, 40f),
        };
        private const float FootRise = 26f;         // What the land behind the last ridge climbs to, towards the mountain's foot.

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
            // fell gently first would be a brow, and the grass on it a horizon. Across the water the far meadow lies low and level, and behind that the land climbs in ridges.
            float fall = Saturate(shore / HeroAboveShore);
            float near = 0.4f + (StandHeight - 0.4f) * fall * fall + rolling * 2.5f * Smoothstep(HeroAboveShore + 10f, HeroAboveShore + 60f, shore);
            float behind = FarShore + Smoothstep(0f, 24f, shore) * 0.8f + Hills(x, z, rolling);
            float side = MathF.Abs(x - Lake.X);
            float flanks = 3f + 10f * Smoothstep(160f, 420f, side) + rolling * 6f;

            float height = Lerp(near, behind, Smoothstep(Lake.Y - 50f, Lake.Y + 30f, z));
            // Away to either side the land rises a little, so the picture is held between low hills.
            float edge = Smoothstep(LakeHalf.X + 20f, LakeHalf.X + 140f, side);
            height = Lerp(height, MathF.Max(height, flanks), edge);
            return MathF.Max(height, 0.25f);
        }

        // What the land behind the far meadow stands above it. One long slope is a wall from in front, with nothing to say how far back it goes: ridges, each hiding low ground behind it, are
        // seen as one shape over another, and the air lying in the low ground between is what makes the further one paler. Their crests wander, and stand and sink along their length.
        private float Hills(float x, float z, float rolling)
        {
            float woods = Herd.Y + HerdHalf.Y + 20f;
            float rise = rolling * 10f * Smoothstep(woods, woods + 120f, z);
            for (int i = 0; i < Ridges.Length; i++)
            {
                (float at, float width, float height, float wander) = Ridges[i];
                float crest = at + _noise.Fbm(x + 311f * (i + 1), 97f * i, 300f, 3) * wander;
                float across = (z - crest) / width;
                float along = Lerp(0.6f, 1.15f, _noise.Fbm01(x - 150f * i, 40f + 213f * i, 170f, 3));
                rise += height * along * MathF.Exp(-across * across);
            }
            (float last, float lastWidth, _, _) = Ridges[^1];
            return rise + FootRise * Smoothstep(last + lastWidth * 0.5f, last + lastWidth * 0.5f + 170f, z);
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

using OpenTK.Mathematics;

namespace Dino_Engine.Rendering.Shells
{
    /// <summary>
    /// Every growth there is, by a number a model carries (<c>glModel.Shell</c>, <c>Mesh.ShellStyle</c>, <c>FloraMesh.ShellStyle</c>): 0 is none. A new one is a
    /// number here and a line in <see cref="All"/>, at the same place. Headless, so generators on any thread can say which theirs is.
    /// </summary>
    public static class ShellStyles
    {
        public const byte None = 0;
        public const byte RockMoss = 1;
        public const byte GroundMoss = 2;
        public const byte BarkMoss = 3;
        public const byte WolfFur = 4;
        public const byte DireFur = 5;
        public const byte BearFur = 6;
        public const byte Bristles = 7;
        public const byte RabbitFur = 8;

        /// <summary>The most there can be, which the GPU's table is sized for (Shells/ShellStyle.glsl's SHELL_STYLES).</summary>
        public const int Most = 16;

        // Moss is a deep green to a yellow green, its colour from what it grows on (geology's moss for stone, the ground's moss, a tree's own in its bark), so what
        // these say is how it's built: cushions a few centimetres high, packed into a carpet, sprigs standing out of them, and fresh growth paler at the tips.
        private static readonly ShellStyle StoneMoss = new()
        {
            Name = "rock moss",
            Growth = ShellGrowth.Moss,
            Mask = ShellMask.Rock,
            Length = 0.085f,
            ClumpSize = 0.17f,
            ClumpWidth = 0.5f,
            ClumpFill = 1f,
            Carpet = 0.42f,
            HeightVariety = 0.5f,
            Shape = 0.75f,
            Fringe = 0.3f,
            StrandSize = 0.014f,
            Variety = 0.12f,
            Crease = 0.45f,
            Lattices = 2,
            RootShade = 0.45f,
            TipTint = new Vector3(1.0f, 1.04f, 0.9f),
            RootOcclusion = 0.32f,
            Translucency = 0.35f,
            Roughness = 0.93f,
            Porosity = 0.95f,
            MostShells = 12,
        };

        private static readonly ShellStyle ForestMoss = StoneMoss with
        {
            Name = "ground moss",
            Lattices = 1,
            Mask = ShellMask.Ground,
            Length = 0.09f,
            ClumpSize = 0.2f,
            ClumpFill = 0.85f,
            Carpet = 0.5f,
            Fringe = 0.32f,
            Floor = 0.4f,
            LeastShells = 3,
            MostShells = 6,
        };

        private static readonly ShellStyle TrunkMoss = StoneMoss with
        {
            Name = "bark moss",
            Lattices = 1,
            Mask = ShellMask.Vertex,
            Length = 0.05f,
            ClumpSize = 0.1f,
            Carpet = 0.36f,
            Fringe = 0.34f,
            StrandSize = 0.011f,
            LeastShells = 3,
            MostShells = 10,
        };

        // Fur takes the colour of the coat under it. A lock is a tuft drawn to a point and laid back along the body, over an undercoat that hides the skin, with
        // guard hairs reaching past both: that's the fringe, which is what makes a silhouette soft.
        private static readonly ShellStyle Wolf = new()
        {
            Name = "wolf fur",
            Growth = ShellGrowth.Fur,
            Mask = ShellMask.Vertex,
            Length = 0.07f,
            ClumpSize = 0.11f,
            ClumpWidth = 0.5f,
            ClumpFill = 1f,
            Carpet = 0.3f,
            HeightVariety = 0.3f,
            Shape = 0.8f,
            Stretch = 2.4f,
            Fringe = 0.3f,
            StrandSize = 0.01f,
            Variety = 0.14f,
            Crease = 0.6f,
            Lattices = 2,
            Floor = 0.25f,
            RootShade = 0.55f,
            TipTint = new Vector3(1.08f, 1.06f, 1.03f),
            RootOcclusion = 0.45f,
            Translucency = 0.22f,
            Roughness = 0.82f,
            Porosity = 0.7f,
            Lean = 0.55f,
            Droop = 0.25f,
            Sway = 0.12f,
            MostShells = 12,
        };

        private static readonly ShellStyle Dire = Wolf with
        {
            Name = "dire wolf fur",
            Length = 0.095f,
            ClumpSize = 0.13f,
            Fringe = 0.34f,
            Lean = 0.6f,
            Droop = 0.3f,
            MostShells = 14,
        };

        private static readonly ShellStyle Bear = Wolf with
        {
            Name = "bear fur",
            Length = 0.12f,
            ClumpSize = 0.16f,
            Carpet = 0.28f,
            HeightVariety = 0.4f,
            Shape = 0.75f,
            Stretch = 2.6f,
            Fringe = 0.32f,
            StrandSize = 0.013f,
            Variety = 0.18f,
            RootShade = 0.5f,
            Lean = 0.5f,
            Droop = 0.4f,
            Sway = 0.1f,
            MostShells = 16,
        };

        // A boar's coat is coarse and sparse: bristles standing off the hide in rows, more of them where the hump is, little undercoat.
        private static readonly ShellStyle Boar = Wolf with
        {
            Name = "bristles",
            Length = 0.055f,
            ClumpSize = 0.075f,
            ClumpWidth = 0.42f,
            ClumpFill = 0.9f,
            Carpet = 0.25f,
            HeightVariety = 0.5f,
            Shape = 1.2f,
            Stretch = 2.8f,
            Fringe = 0.45f,
            StrandSize = 0.008f,
            Variety = 0.2f,
            RootShade = 0.6f,
            TipTint = new Vector3(1.15f, 1.1f, 1.05f),
            Lean = 0.45f,
            Droop = 0.15f,
            Sway = 0.06f,
        };

        private static readonly ShellStyle Rabbit = Wolf with
        {
            Name = "rabbit fur",
            Length = 0.03f,
            ClumpSize = 0.045f,
            Carpet = 0.36f,
            HeightVariety = 0.3f,
            Shape = 0.8f,
            Stretch = 2f,
            Fringe = 0.35f,
            StrandSize = 0.006f,
            Variety = 0.1f,
            RootShade = 0.62f,
            Lean = 0.4f,
            Droop = 0.15f,
            Sway = 0.15f,
            MostShells = 8,
            FadeFrom = 60f,
            FadeTo = 85f,
        };

        /// <summary>Every style by its number. Index 0 is none, and is never drawn.</summary>
        public static readonly ShellStyle?[] All = { null, StoneMoss, ForestMoss, TrunkMoss, Wolf, Dire, Bear, Boar, Rabbit };

        public static ShellStyle? Of(byte style) => style < All.Length ? All[style] : null;

        /// <summary>The farthest anything of a style stands off its surface, for culling.</summary>
        public static float ReachOf(byte style)
        {
            ShellStyle? s = Of(style);
            if (s == null) return 0f;
            return s.Length * (1f + MathF.Max(s.Lean, 0f) + MathF.Max(s.Droop, 0f) + MathF.Max(s.Sway, 0f) * 3f);
        }
    }
}

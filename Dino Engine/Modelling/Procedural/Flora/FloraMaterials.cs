namespace Dino_Engine.Modelling.Procedural.Flora
{
    /// <summary>
    /// The few numbers plants need from the material arrays. A generator can't read them from <see cref="Textures.TextureGenerator"/>, as any static on that class compiles shaders:
    /// plants grow on worker threads and in labs with no GPU. The client fills these in when its materials exist, and until then everything is material 0, which is fine for measuring.
    /// </summary>
    public static class FloraMaterials
    {
        private static readonly int[] Barks = new int[Enum.GetValues<BarkKind>().Length];

        /// <summary>The material number of the first layer of cards. A card's layer is added to this.</summary>
        public static int FirstCard;

        /// <summary>A plain surface, for what's coloured by its vertices alone: a mushroom's cap, a flower's heart.</summary>
        public static int Flesh;

        /// <summary>The same, giving off light.</summary>
        public static int Glow;

        public static int Bark(BarkKind kind) => Barks[(int)kind];

        public static void SetBark(BarkKind kind, int material) => Barks[(int)kind] = material;
    }
}

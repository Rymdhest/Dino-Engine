namespace Dino_Engine.Modelling.Procedural
{
    /// <summary>
    /// The Saurian Reich's mark: the skull of the first tyrant the Amber Institute ever raised, in profile, brass on oxblood. It looks like nothing but what it is, which matters: a curved claw on a red field
    /// read from across a battlefield as a crescent, and a flag that looks like a real country's has no place on an enemy (LORE.md). One outline for everything that carries it, a war-beast's pennant and the
    /// banners of its arenas alike, in a square of side 1 about its middle, x towards the snout and y up.
    /// </summary>
    public static class ReichMark
    {
        /// <summary>The skull: a deep rounded back, a long tapering snout and a jaw line, with its teeth along the bottom, in order round it.</summary>
        public static readonly (float X, float Y)[] Skull =
        {
            (-0.5f, 0.05f), (-0.46f, 0.24f), (-0.32f, 0.36f), (-0.1f, 0.33f), (0.12f, 0.24f), (0.34f, 0.14f), (0.5f, 0.06f), (0.52f, -0.04f),
            (0.42f, -0.08f), (0.36f, -0.16f), (0.28f, -0.09f), (0.2f, -0.17f), (0.12f, -0.1f), (0.04f, -0.18f), (-0.04f, -0.11f), (-0.12f, -0.19f),
            (-0.2f, -0.12f), (-0.3f, -0.22f), (-0.42f, -0.2f), (-0.5f, -0.1f),
        };

        /// <summary>A point of the skull's inside that every point of its outline can be seen from, which is where a fan of it is drawn from.</summary>
        public static readonly (float X, float Y) Middle = (-0.08f, 0.06f);

        /// <summary>Its eye, the opening in front of it and the one behind, as ellipses: middle and radii, in the cloth's colour.</summary>
        public static readonly (float X, float Y, float RadiusX, float RadiusY)[] Holes =
        {
            (-0.27f, 0.13f, 0.075f, 0.085f),
            (0.04f, 0.07f, 0.11f, 0.06f),
            (-0.4f, 0.01f, 0.05f, 0.07f),
        };
    }
}

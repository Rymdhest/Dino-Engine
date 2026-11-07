using Dino_Engine.World;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>
    /// Where every number in a look comes from. Each is drawn from a stream named after it and nothing is drawn in order, so another parameter can be added without moving any
    /// of the others, and an item keeps its look when a generator learns something new. A part gets a <see cref="Part"/> of its own, so that one of them can't move another.
    /// </summary>
    public sealed class Genes
    {
        private readonly WorldRandom _random;

        public Genes(ulong seed) : this(new WorldRandom(seed))
        {
        }

        private Genes(WorldRandom random)
        {
            _random = random;
        }

        public Genes Part(string name) => new Genes(_random.Fork(name));

        public Genes Part(int index) => new Genes(_random.Fork(index));

        /// <summary>From 0 to 1.</summary>
        public float Unit(string name) => _random.Fork(name).Float();

        public float Range(string name, float low, float high) => low + (high - low) * Unit(name);

        public float Range(string name, FloatRange range) => Range(name, range.Min, range.Max);

        /// <summary>A stream of its own, for when numbers are wanted by the hundred and naming each would cost more than the thing they make.</summary>
        public WorldRandom Stream(string name) => _random.Fork(name);

        /// <summary>Near <paramref name="centre"/>, by about <paramref name="spread"/> either way, and never more than twice that.</summary>
        public float Around(string name, float centre, float spread)
        {
            return centre + Math.Clamp(_random.Fork(name).Gaussian(), -2f, 2f) * spread;
        }

        public bool Chance(string name, float chance) => Unit(name) < chance;

        /// <summary>A whole number from <paramref name="low"/> up to and including <paramref name="high"/>.</summary>
        public int Count(string name, int low, int high) => low + (int)(Unit(name) * (high - low + 1 - 1e-4f));

        /// <summary>Plus or minus one, which way something goes.</summary>
        public float Sign(string name) => Unit(name) < 0.5f ? -1f : 1f;

        /// <summary>Which of these, by how likely each is.</summary>
        public int Pick(string name, params float[] weights)
        {
            int picked = _random.Fork(name).PickWeighted(weights);
            return picked < 0 ? 0 : picked;
        }
    }
}

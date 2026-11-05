using Dino_Engine.Sound.Dsp;
using Dino_Engine.Sound.Synthesis;

namespace Dino_Engine.Sound.Content
{
    /// <summary>The group a sound is mixed in, each with a volume of its own.</summary>
    public enum SoundBus
    {
        Foley,      // Bodies moving through the world: steps, cloth, doors, pickups.
        Combat,     // Blows and whatever they do.
        Voice,      // Throats.
        Ambience,   // The place itself: wind, water, life. Always gives way to the rest.
        Interface,  // Heard by the player, not the hero, so it has no place in the world.
        Music,
    }

    /// <summary>A recipe: makes one take of a sound. It's run on a worker thread, as often as fresh takes are wanted.</summary>
    public delegate void SoundRecipe(Take take);

    /// <summary>
    /// One sound the game can ask for, and everything about how it's played. Sounds are made, not stored, so this is a recipe
    /// and a set of manners. Adding one is a recipe method and an entry in a catalogue: see <c>SOUND.md</c>.
    /// </summary>
    public sealed class SoundDefinition
    {
        public readonly string Id;
        public readonly SoundRecipe Recipe;

        public SoundBus Bus = SoundBus.Foley;
        public bool Stereo;                 // Otherwise mono, placed in the world by the mixer. Wide sounds and the interface want this.
        public float Strength = 0.5f;       // Handed to the recipe as Take.Strength.
        public Mastering Mastering = Mastering.Default;

        // --- Variation. Every take differs already; these vary each playing of it as well. ---
        public int Takes = 6;               // How many are kept ready at once.
        public int PlaysPerTake = 3;        // How often one is heard before it's replaced by a new one.
        public float PitchSpread = 0.3f;    // Semitones either way.
        public float GainSpread = 1f;       // Decibels either way.

        // --- The mix. ---
        public float Presence;              // Decibels above an everyday sound. The mix makes way for the loudest thing happening.
        public int Priority = 50;           // When there are too many voices, the lowest go first.
        public int MostAtOnce = 6;          // Past this a new one replaces the oldest of the same sound.
        public float Spacing = 0.03f;       // Seconds. Two in less than this would only blur into one.

        // --- In the world. ---
        public bool Positional = true;      // Otherwise it's in the player's head, not at a place.
        public float NearDistance = 9f;     // Metres from the listener inside which it's at full level.
        public float FarDistance = 70f;     // Metres at which it has faded out.
        public float ReverbSend = 0.22f;    // How much reaches the reverb when near. It grows with distance, as it does in a real place.
        public bool Doppler;                // Bends with speed, for things flying past.

        public SoundDefinition(string id, SoundRecipe recipe)
        {
            Id = id;
            Recipe = recipe;
        }

        /// <summary>Makes one take. Whatever seed is given, it comes out the same.</summary>
        public SoundBuffer Render(ulong seed)
        {
            using var take = new Take(seed, Stereo, Strength);
            Recipe(take);
            return take.Finish(Mastering);
        }

        public override string ToString() => Id;
    }

    /// <summary>
    /// Every sound there is. Each one enters itself here when its catalogue class is first touched, so tools can list them all with nothing kept
    /// in step by hand; <see cref="LoadAll"/> touches them.
    /// </summary>
    public static class SoundCatalog
    {
        private static readonly List<SoundDefinition> Definitions = new();
        private static readonly List<LiveDefinition> LiveDefinitions = new();

        public static IReadOnlyList<SoundDefinition> All
        {
            get
            {
                LoadAll();
                return Definitions;
            }
        }

        public static IReadOnlyList<LiveDefinition> AllLive
        {
            get
            {
                LoadAll();
                return LiveDefinitions;
            }
        }

        internal static SoundDefinition Add(SoundDefinition definition)
        {
            lock (Definitions) Definitions.Add(definition);
            return definition;
        }

        internal static LiveDefinition Add(LiveDefinition definition)
        {
            lock (LiveDefinitions) LiveDefinitions.Add(definition);
            return definition;
        }

        private static bool _loaded;

        /// <summary>Runs the static constructor of every catalogue, in a fixed order so a listing is always the same.</summary>
        public static void LoadAll()
        {
            if (_loaded) return;
            _loaded = true;
            foreach (Type type in typeof(SoundCatalog).Assembly.GetTypes().Where(t => t.GetCustomAttributes(typeof(SoundCollectionAttribute), false).Length > 0).OrderBy(t => t.Name))
            {
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(type.TypeHandle);
            }
        }

        public static SoundDefinition? Find(string id) => All.FirstOrDefault(d => d.Id == id);
    }

    /// <summary>Marks a class holding sound definitions, so <see cref="SoundCatalog"/> finds it.</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class SoundCollectionAttribute : Attribute
    {
    }
}

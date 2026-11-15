using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using System.Reflection;

namespace Dino_Engine.World.Lexicon
{
    /// <summary>What stands on an exhibit's plot, and so who puts it there.</summary>
    public enum LexiconKind
    {
        /// <summary>A body the game stands there when the room is entered (its <c>LexiconServer</c>), drawn from <see cref="Exhibit.Look"/>.</summary>
        Creature,
        /// <summary>Anything that grows or lies on the land, a plant of <see cref="Modelling.Procedural.Flora.FloraCatalog"/> or a stone, planted by the world: <see cref="Exhibit.Look"/> is its asset id.</summary>
        Asset,
        /// <summary>A place laid out by its own builder, as it would be in any land: <see cref="Exhibit.Place"/> says which kind.</summary>
        Place,
        /// <summary>An empty plot, for what could stand there and doesn't yet: its plaque says what's missing.</summary>
        Missing,
    }

    /// <summary>What a section's plots are laid on, so that what stands on each reads clearly from the game's camera.</summary>
    public enum ExhibitGround
    {
        /// <summary>The room's own grass: places lay out their own ground.</summary>
        Grass,
        /// <summary>A pad of packed gravel under each plot, which nothing grows through, for anything that walks.</summary>
        Gravel,
        /// <summary>A tended bed under each plot, its grass short and green, for anything that grows.</summary>
        Beds,
    }

    /// <summary>
    /// One thing on show in the asset lexicon (LEXICON.md): what it is, what its plaque says, and how much room it takes. A shelf says these, and <see cref="LexiconLayout"/> gives each its plot.
    /// </summary>
    public sealed class Exhibit
    {
        /// <summary>Unique in the whole lexicon, and the same build to build, as "creature/war-raptor" or "asset/pine.scots": what the index and the game know it by.</summary>
        public string Key = "";

        /// <summary>What its plaque says in large: what it's called.</summary>
        public string Name = "";

        /// <summary>What its plaque says under that, small: its ids, its look, where it's met. One line.</summary>
        public string Detail = "";

        public LexiconKind Kind;

        /// <summary>A creature's model name, as the server would send it, or an asset's id (a plant's species or a stone's).</summary>
        public string Look = "";

        /// <summary>For a place: which kind it is.</summary>
        public SiteKind Place;

        /// <summary>How big it's shown, as the game would draw it.</summary>
        public float Scale = 1f;

        /// <summary>For a creature: radians it's turned, its front (+z) from north. Most stand three quarters on to the camera, facing its left, as the game's parades do.</summary>
        public float Yaw;

        /// <summary>For a creature: drawn as a still thing rather than a body with joints, as a training dummy is.</summary>
        public bool Prop;

        /// <summary>Metres its plot takes across (east to west) and deep (south to north), its plaque not counted. What it is stands in the middle of it, or <see cref="Offset"/> from there.</summary>
        public Vector2 Size = new Vector2(6f);

        /// <summary>
        /// Metres from the middle of its plot to where it stands, in the world's frame (x to the west, z to the north): a body isn't always round its own origin, as a dinosaur turned three
        /// quarters on reaches further with its tail than its snout, so this is what puts the whole of it in the middle of its plot.
        /// </summary>
        public Vector2 Offset;

        /// <summary>Metres it rises, about, as shown: someone sent to look at something tall is stood nearer it, so that it's in the middle of the screen and not up under the minimap.</summary>
        public float Height;

        /// <summary>
        /// Metres south of where it stands that someone looking at it can come before they'd be in it, from the middle of its plot: a rock's or a trunk's solid part, a body as it's turned.
        /// Whoever's sent to look at it is stood no nearer than this, and a little more.
        /// </summary>
        public float Clear;

        /// <summary>The words that go with it in the index, past what its plaque says, so that it can be found by them.</summary>
        public string Keywords = "";

        public override string ToString() => Key;
    }

    /// <summary>Exhibits that belong together, side by side in a row: one species of dinosaur in every way it's met, one catalogue of plants, one kind of place.</summary>
    public sealed class ExhibitFamily
    {
        public string Name = "";

        /// <summary>A line under its name: what holds them together.</summary>
        public string Detail = "";

        public readonly List<Exhibit> Exhibits = new();
    }

    /// <summary>
    /// A part of the lexicon with a sign of its own at its south edge, where it's come into on the walk north from the gate: the dinosaurs, the trees, the places. Its families are laid out in rows,
    /// west to east, which is left to right on the screen, and the rows from south to north.
    /// </summary>
    public sealed class ExhibitSection
    {
        public string Key = "";
        public string Name = "";

        /// <summary>What its sign says under its name: what's here, and how it's laid out.</summary>
        public string Blurb = "";

        /// <summary>Where it stands on the walk north from the gate: lower is nearer.</summary>
        public float Order;

        /// <summary>Whether its plots are a place's size, so that it goes in the column of places to the east, rather than the column of things to the west.</summary>
        public bool Large;

        public ExhibitGround Ground;

        public readonly List<ExhibitFamily> Families = new();

        /// <summary>Every exhibit in it, family by family.</summary>
        public IEnumerable<Exhibit> Exhibits => Families.SelectMany(family => family.Exhibits);
    }

    /// <summary>
    /// Says what goes in the lexicon. Each class marked <see cref="LexiconShelfAttribute"/> with a parameterless constructor is found by reflection, in this assembly and any loaded one that
    /// references it, with nothing to register, as the game's creatures are said by a shelf in the game. Everything a shelf says has to come from what the game has, read as it is, so
    /// that anything added to a catalogue turns up in the lexicon by itself, in its place.
    /// </summary>
    public interface ILexiconShelf
    {
        IEnumerable<ExhibitSection> Sections();
    }

    /// <summary>Marks an <see cref="ILexiconShelf"/> to be found.</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class LexiconShelfAttribute : Attribute
    {
    }

    /// <summary>
    /// Every section of the asset lexicon, from every shelf there is, in the order they're walked. Found once, the first time anything asks, and the same from then on. A shelf that fails says
    /// so on a plot of its own rather than taking the room down with it.
    /// </summary>
    public static class LexiconCatalog
    {
        private static readonly Lazy<IReadOnlyList<ExhibitSection>> _sections = new(Gather, LazyThreadSafetyMode.ExecutionAndPublication);

        public static IReadOnlyList<ExhibitSection> Sections => _sections.Value;

        /// <summary>Every exhibit, section by section.</summary>
        public static IEnumerable<Exhibit> All => Sections.SelectMany(section => section.Exhibits);

        private static IReadOnlyList<ExhibitSection> Gather()
        {
            var sections = new List<ExhibitSection>();
            foreach (Type type in ShelfTypes())
            {
                try
                {
                    var shelf = (ILexiconShelf)Activator.CreateInstance(type)!;
                    sections.AddRange(shelf.Sections());
                }
                catch (Exception e)
                {
                    Console.WriteLine($"The lexicon's shelf {type.FullName} failed: {e}");
                    sections.Add(Broken(type, e));
                }
            }

            // Two exhibits with one key would be one in the index, so a second one is told apart rather than lost.
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (Exhibit exhibit in sections.SelectMany(section => section.Exhibits))
            {
                string key = exhibit.Key;
                for (int again = 2; !keys.Add(key); again++) key = $"{exhibit.Key}#{again}";
                if (key != exhibit.Key) Console.WriteLine($"The lexicon has two exhibits called {exhibit.Key}: the second is {key}.");
                exhibit.Key = key;
            }

            foreach (ExhibitSection section in sections) section.Families.RemoveAll(family => family.Exhibits.Count == 0);
            sections.RemoveAll(section => section.Families.Count == 0);
            return sections.OrderBy(section => section.Order).ThenBy(section => section.Key, StringComparer.Ordinal).ToList();
        }

        // Every shelf in this assembly and in any loaded one that knows about it, in an order that's the same on every machine.
        private static List<Type> ShelfTypes()
        {
            Assembly engine = typeof(LexiconCatalog).Assembly;
            string engineName = engine.GetName().Name ?? "";
            var found = new List<Type>();
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic) continue;
                if (assembly != engine && !assembly.GetReferencedAssemblies().Any(name => name.Name == engineName)) continue;
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types.Where(type => type != null).ToArray()!;
                }
                foreach (Type type in types)
                {
                    if (type.IsAbstract || !typeof(ILexiconShelf).IsAssignableFrom(type) || type.GetCustomAttribute<LexiconShelfAttribute>() == null) continue;
                    if (type.GetConstructor(Type.EmptyTypes) == null) continue;
                    found.Add(type);
                }
            }
            return found.OrderBy(type => type.FullName, StringComparer.Ordinal).ToList();
        }

        private static ExhibitSection Broken(Type type, Exception e)
        {
            var section = new ExhibitSection { Key = "broken/" + type.FullName, Name = "Broken shelf", Blurb = $"{type.Name} failed while saying what it has. The game's log has why.", Order = -1f, Ground = ExhibitGround.Gravel };
            var family = new ExhibitFamily { Name = type.Name, Detail = "Failed" };
            family.Exhibits.Add(new Exhibit { Key = "broken/" + type.FullName, Name = type.Name, Detail = e.GetType().Name + ": " + e.Message, Kind = LexiconKind.Missing, Size = new Vector2(12f, 6f) });
            section.Families.Add(family);
            return section;
        }
    }

    /// <summary>Words for plaques, made the same way everywhere in the lexicon.</summary>
    public static class LexiconWords
    {
        /// <summary>A name written as code writes it, ScotsPine or FishingHamlet, as words: Scots Pine, Fishing Hamlet.</summary>
        public static string Spaced(string name)
        {
            var words = new System.Text.StringBuilder(name.Length + 8);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                bool upper = char.IsUpper(c);
                // A new word starts at a capital after a small letter, and at the last capital of a run before a small letter, as in "UFOSighting".
                if (i > 0 && upper && (char.IsLower(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1]) && char.IsUpper(name[i - 1])))) words.Append(' ');
                words.Append(c);
            }
            return words.ToString();
        }

        /// <summary>The first letter of something in capitals, for the start of a plaque.</summary>
        public static string Capital(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);

        /// <summary>How many kinds a family holds, as its sign says it.</summary>
        public static string Kinds(int count) => count == 1 ? "1 kind" : $"{count} kinds";

        /// <summary>A number from a name that's the same on every machine and in every run, as a string's own hash isn't.</summary>
        public static uint Hash(string text)
        {
            uint hash = 2166136261u;
            foreach (char c in text) hash = (hash ^ c) * 16777619u;
            return hash;
        }
    }
}

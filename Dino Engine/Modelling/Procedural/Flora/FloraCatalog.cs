using System.Reflection;

namespace Dino_Engine.Modelling.Procedural.Flora
{
    /// <summary>Marks a class as a catalogue of plants: every <see cref="PlantSpecies"/> and <see cref="CardForm"/> on it is found by itself, so a new one needs no registering.</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class FloraCollectionAttribute : Attribute
    {
    }

    /// <summary>Every plant and every card there is. Sorted by id, as the order classes come back in is nobody's to rely on, and a card's layer is its place on this list.</summary>
    public static class FloraCatalog
    {
        private static readonly List<PlantSpecies> _species = new();
        private static readonly List<CardForm> _cards = new();
        private static readonly Dictionary<string, PlantSpecies> _byId = new();

        static FloraCatalog()
        {
            foreach (Type type in typeof(FloraCatalog).Assembly.GetTypes())
            {
                if (type.GetCustomAttribute<FloraCollectionAttribute>() == null) continue;
                foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
                {
                    object? value = field.GetValue(null);
                    if (value is PlantSpecies species) _species.Add(species);
                    if (value is CardForm card)
                    {
                        if (card.Id.Length == 0) card.Id = field.Name;
                        _cards.Add(card);
                    }
                }
            }

            _species.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            _cards.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            for (int i = 0; i < _cards.Count; i++) _cards[i].Layer = i;
            foreach (PlantSpecies species in _species) _byId.Add(species.Id, species);
        }

        public static IReadOnlyList<PlantSpecies> Species => _species;
        public static IReadOnlyList<CardForm> Cards => _cards;

        public static PlantSpecies Get(string id) => _byId[id];
        public static bool TryGet(string id, out PlantSpecies species) => _byId.TryGetValue(id, out species!);
    }

    /// <summary>Where plants come from: a species and a seed go in, and the same plant comes out on any machine, on any thread, as often as it's asked.</summary>
    public static class FloraForge
    {
        public static FloraMesh Grow(PlantSpecies species, ulong seed)
        {
            var mesh = new FloraMesh();
            species.Form.Grow(mesh, new Gear.Genes(seed).Part(species.Id));
            if (!species.Sways) mesh.Still();
            return mesh;
        }
    }
}

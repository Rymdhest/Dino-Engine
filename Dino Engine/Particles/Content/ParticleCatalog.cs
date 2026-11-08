using System.Reflection;

namespace Dino_Engine.Particles.Content
{
    /// <summary>Marks a class of effects and styles, found by reflection like the sound and flora catalogues: a new one needs nothing registering.</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ParticleCollectionAttribute : Attribute
    {
    }

    /// <summary>Every effect and style in the [ParticleCollection] catalogues, by the name of its class and field: "Fire.Campfire".</summary>
    public static class ParticleCatalog
    {
        private static Dictionary<string, ParticleEffect>? _effects;
        private static Dictionary<string, ParticleStyle>? _styles;

        public static IReadOnlyDictionary<string, ParticleEffect> Effects => _effects ??= Gather<ParticleEffect>((effect, name) => effect.Name = name);
        public static IReadOnlyDictionary<string, ParticleStyle> Styles => _styles ??= Gather<ParticleStyle>((style, name) => style.Name = name);

        public static ParticleEffect? Effect(string name) => Effects.TryGetValue(name, out ParticleEffect? effect) ? effect : Effects.Values.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));

        private static Dictionary<string, T> Gather<T>(Action<T, string> named) where T : class
        {
            var found = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
            foreach (Type type in typeof(ParticleCatalog).Assembly.GetTypes())
            {
                if (type.GetCustomAttribute<ParticleCollectionAttribute>() == null) continue;
                foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
                {
                    if (field.GetValue(null) is not T item) continue;
                    string name = type.Name + "." + field.Name;
                    named(item, name);
                    found[name] = item;
                }
            }
            return found;
        }
    }
}

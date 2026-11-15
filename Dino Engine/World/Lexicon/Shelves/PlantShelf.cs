using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using System.Reflection;

namespace Dino_Engine.World.Lexicon
{
    /// <summary>
    /// Everything that grows, from <see cref="FloraCatalog"/>: a section for each kind of plant, the trees first, and in each a family for each catalogue the plants are written in
    /// (<see cref="FloraCollectionAttribute"/>), so a new species turns up beside the ones written with it. What lives in the water is left out, as the room has none to stand it in.
    /// Each is the first plant of its kind the room grows, at a little over its middle size, on a bed of its own sized to its crown.
    /// </summary>
    [LexiconShelf]
    public sealed class PlantShelf : ILexiconShelf
    {
        private const float Grown = 0.6f;           // Where between its smallest and largest each is shown.
        private const float LeastPlot = 5f;         // Metres: room for its plaque, however small it is.
        private const float Room = 2.5f;            // Metres between a crown's edge and the next bed's.

        public IEnumerable<ExhibitSection> Sections()
        {
            var plants = new List<(PlantSpecies Species, string Collection, string Field)>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (Type type in typeof(FloraCatalog).Assembly.GetTypes().Where(type => type.GetCustomAttribute<FloraCollectionAttribute>() != null).OrderBy(type => type.Name, StringComparer.Ordinal))
            {
                // In the order they're written, so that a plant named twice is always named by its first field.
                foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Static).OrderBy(field => field.MetadataToken))
                {
                    if (field.GetValue(null) is not PlantSpecies species || !seen.Add(species.Id)) continue;
                    plants.Add((species, type.Name, field.Name));
                }
            }

            foreach (IGrouping<WorldAssetKind, (PlantSpecies Species, string Collection, string Field)> kind in plants.GroupBy(plant => plant.Species.Kind).OrderBy(group => (int)group.Key))
            {
                var section = new ExhibitSection
                {
                    Key = "plants/" + kind.Key.ToString().ToLowerInvariant(),
                    Name = SectionName(kind.Key),
                    Blurb = SectionBlurb(kind.Key),
                    Order = 40f + (int)kind.Key,
                    // A tree stands in a tended bed, as one would; anything small on bare gravel, as the flowers of a bed would be taken for part of it.
                    Ground = kind.Key == WorldAssetKind.Tree ? ExhibitGround.Beds : ExhibitGround.Gravel,
                };
                foreach (IGrouping<string, (PlantSpecies Species, string Collection, string Field)> collection in kind.GroupBy(plant => plant.Collection).OrderBy(group => group.Key, StringComparer.Ordinal))
                {
                    var family = new ExhibitFamily { Name = LexiconWords.Spaced(collection.Key), Detail = LexiconWords.Kinds(collection.Count()) };
                    foreach ((PlantSpecies species, string _, string field) in collection.OrderBy(plant => plant.Species.Id, StringComparer.Ordinal)) family.Exhibits.Add(Specimen(species, collection.Key, field));
                    section.Families.Add(family);
                }
                yield return section;
            }
        }

        private static Exhibit Specimen(PlantSpecies species, string collection, string field)
        {
            WorldAsset asset = species.Asset;
            float scale = species.Scale.Lerp(Grown);
            NatureCatalog.SpeciesOf(LexiconAssets.ModelOf(asset), out ulong seed);
            float reach = asset.Footprint * scale, tall = 0f;
            string size = "";
            if (species.TryCrown(seed, out float height, out _, out float crown))
            {
                reach = MathF.Max(reach, crown * scale);
                tall = height * scale;
                size = FormattableString.Invariant($" · {tall:0} m tall");
            }
            float plot = MathF.Max(reach * 2f + Room, LeastPlot);
            string name = LexiconWords.Spaced(field);
            if (name.EndsWith(" Plant", StringComparison.Ordinal)) name = name.Substring(0, name.Length - " Plant".Length);
            return new Exhibit
            {
                Key = "asset/" + species.Id,
                Name = name,
                Detail = species.Id + size,
                Kind = LexiconKind.Asset,
                Look = species.Id,
                Scale = scale,
                Yaw = LexiconWords.Hash(species.Id) / (float)uint.MaxValue * MathF.Tau,
                Size = new Vector2(plot),
                Height = tall,
                // Only a trunk or a log is in the way: anyone can stand under a crown, or in a bush.
                Clear = MathF.Max(asset.SolidRadius, asset.SolidLength * 0.5f) * scale,
                Keywords = $"{collection} {species.Kind} {field}",
            };
        }

        private static string SectionName(WorldAssetKind kind) => kind switch
        {
            WorldAssetKind.Tree => "Trees",
            WorldAssetKind.Shrub => "Shrubs",
            WorldAssetKind.GroundCover => "Ground cover",
            WorldAssetKind.Crop => "Crops",
            WorldAssetKind.Deadwood => "Deadwood",
            _ => LexiconWords.Spaced(kind.ToString()),
        };

        private static string SectionBlurb(WorldAssetKind kind) => kind switch
        {
            WorldAssetKind.Tree => "Every tree that grows, one of each, by the catalogue it's written in. Each is the first the room grows of its kind, a little over its middle size.",
            WorldAssetKind.Shrub => "Every shrub, one of each, by the catalogue it's written in.",
            WorldAssetKind.GroundCover => "Everything small that grows on the ground: ferns, flowers, grasses, mushrooms. What lives in the water is left out, as the room has none.",
            WorldAssetKind.Crop => "What's grown in fields.",
            WorldAssetKind.Deadwood => "Stumps, logs and fallen trunks.",
            _ => "One of each, by the catalogue it's written in.",
        };
    }
}

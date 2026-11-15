using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.World.Lexicon
{
    /// <summary>
    /// Every kind of place a land can have, from <see cref="SiteCatalog"/>, each laid out by its own builder exactly as in a land, at the least size its kind ever is, its way in facing the camera,
    /// with whatever it needs of the land round it on its plot (<see cref="LexiconDioramas"/>). Nobody holds any of them. The landing isn't here: it's where the room is come into, at its south end.
    /// </summary>
    [LexiconShelf]
    public sealed class PlaceShelf : ILexiconShelf
    {
        private const float Spread = 1.3f;      // Of a place's radius, for what its builder puts down a little past it: a churchyard's corners, a keep's banners, a windmill by a village.
        private const float Margin = 14f;       // Metres of lawn round that, between one place and the next.

        public IEnumerable<ExhibitSection> Sections()
        {
            var section = new ExhibitSection
            {
                Key = "places",
                Name = "Places",
                Blurb = "Every kind of place a land can have, laid out by its own builder as in any land, at the least size it's ever drawn, its way in facing you. Nobody holds them. What needs the sea is on the north shore.",
                Order = 60f,
                Large = true,
                Ground = ExhibitGround.Grass,
            };

            var settlements = new ExhibitFamily { Name = "Settlements", Detail = "Where the land's people live" };
            var everyLand = new ExhibitFamily { Name = "In every land", Detail = "Landmarks, waygates and what holds them" };
            var stories = new ExhibitFamily { Name = "Places with a story", Detail = "A land has a few, chosen by its ground" };
            var kinds = new List<SiteDefinition>();
            foreach (SiteDefinition definition in SiteCatalog.All.Concat(SiteCatalog.Stories))
            {
                if (definition.Kind == SiteKind.Landing || kinds.Any(kind => kind.Kind == definition.Kind)) continue;
                kinds.Add(definition);
            }
            foreach (SiteDefinition definition in kinds)
            {
                ExhibitFamily family = definition.IsSettlement ? settlements : SiteCatalog.Stories.Contains(definition) ? stories : everyLand;
                family.Exhibits.Add(Place(definition));
            }
            foreach (ExhibitFamily family in new[] { settlements, everyLand, stories })
            {
                family.Detail += $" · {LexiconWords.Kinds(family.Exhibits.Count)}";
                section.Families.Add(family);
            }
            yield return section;
        }

        // As wide as what it puts down a little past its edge, either way, and as deep: near the front, where its plaque is (LexiconLayout.PlaceFront), and that again behind it.
        private static Exhibit Place(SiteDefinition definition)
        {
            float radius = LexiconDioramas.RadiusOf(definition.Kind);
            float across = (radius * Spread + Margin) * 2f;
            float deep = radius + LexiconLayout.PlaceFront + radius * Spread + Margin;
            string setting = LexiconDioramas.Of(definition.Kind) switch
            {
                Diorama.Coast => " · on the beach",
                Diorama.Hill => " · cut into a hillside",
                Diorama.Snow => " · in the snow",
                Diorama.WoodsBehind => " · at the edge of a wood",
                Diorama.WoodsAround => " · deep in a wood",
                _ => "",
            };
            return new Exhibit
            {
                Key = "place/" + definition.Kind,
                Name = LexiconWords.Spaced(definition.Kind.ToString()),
                Detail = $"SiteKind.{definition.Kind} · {radius * 2f:0} m across, its least{setting}",
                Kind = LexiconKind.Place,
                Place = definition.Kind,
                Size = new Vector2(across, deep),
                Keywords = $"{definition.Label} site place point of interest",
            };
        }
    }
}

using Dino_Engine.Modelling.Procedural.Rock;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.World.Lexicon
{
    /// <summary>
    /// Every form a stone can take (<see cref="RockForm"/>), from a pebble to a bluff, in the room's own rock. A stone is shaded by its world's rock (TERRAIN.md), so one of another rock here
    /// would be drawn as this one: the room's rock is what's shown, and a stone of another is to be seen in a land of it.
    /// </summary>
    [LexiconShelf]
    public sealed class StoneShelf : ILexiconShelf
    {
        private const float Grown = 0.6f;
        private const float LeastPlot = 5f;
        private const float Spread = 1.3f;      // Of a stone's footprint, as what it's grown into reaches a little past what it keeps clear.
        private const float Room = 2.5f;

        public IEnumerable<ExhibitSection> Sections()
        {
            RockKind rock = LexiconWorld.Rock;
            var section = new ExhibitSection
            {
                Key = "stones",
                Name = "Stones",
                Blurb = $"Every form a stone can take, smallest first, in {rock.ToString().ToLowerInvariant()}, the room's own rock: a stone is shaded by its world's rock, so this is how each looks in a land of it.",
                Order = 50f,
                Ground = ExhibitGround.Gravel,
            };
            var family = new ExhibitFamily { Name = rock.ToString(), Detail = $"The room's rock · {LexiconWords.Kinds(Enum.GetValues<RockForm>().Length)}" };
            foreach (RockForm form in Enum.GetValues<RockForm>())
            {
                WorldAsset asset = NatureCatalog.RockOf(rock, form);
                float scale = asset.Scale.Lerp(Grown);
                float reach = asset.Footprint * scale * Spread;
                float plot = MathF.Max(reach * 2f + Room, LeastPlot);
                float tall = RockForge.TopOf(form) * scale;
                family.Exhibits.Add(new Exhibit
                {
                    Key = "asset/" + asset.Id,
                    Name = LexiconWords.Spaced(form.ToString()),
                    Detail = FormattableString.Invariant($"{asset.Id} · {tall:0.#} m tall"),
                    Kind = LexiconKind.Asset,
                    Look = asset.Id,
                    Scale = scale,
                    Yaw = LexiconWords.Hash(asset.Id) / (float)uint.MaxValue * MathF.Tau,
                    Size = new Vector2(plot),
                    Height = tall,
                    // A stone is as solid as it looks, all the way out.
                    Clear = reach,
                    Keywords = $"rock stone {rock} {form}",
                });
            }
            section.Families.Add(family);
            yield return section;
        }
    }
}

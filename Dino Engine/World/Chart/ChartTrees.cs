using Dino_Engine.Modelling.Procedural.Flora;
using OpenTK.Mathematics;

namespace Dino_Engine.World.Chart
{
    /// <summary>The shapes a tree is drawn in on a chart, as an engraver would: by its silhouette alone.</summary>
    internal enum TreeShape
    {
        Round,      // A broadleaf's crown on a short trunk.
        Spire,      // A conifer in tiers to a point.
        Column,     // A cypress, a poplar: tall and narrow.
        Umbrella,   // Flat on top and spreading, high on its trunk: an acacia, a stone pine.
        Vase,       // Narrow below and widest at the top.
        Weeping,    // A willow's curtain.
        Palm,       // A stem and a head of fronds: palms, tree ferns, cycads.
        Snag,       // Dead wood, bare.
        Bamboo,     // A clump of canes.
        Cactus,     // A saguaro and its arms.
    }

    /// <summary>How one species is drawn: its shape, its proportions and its colours, worked out from what grows it, so a new species is drawn right with nothing added.</summary>
    internal sealed class TreeLook
    {
        public TreeShape Shape;
        public float Aspect = 1.2f;     // Height for width.
        public float Trunk = 0.25f;     // Share of the height that's bare trunk.
        public float Width = 1f;        // Against the usual tree, by how wide its crown really is.
        public Vector3 Shade, Body, Sun;
        public Vector3 Wood;
        public float MetresTall = 18f;  // A usual one, at a scale of one, for how big each is drawn against the rest.
    }

    internal static class ChartTrees
    {
        private static readonly Dictionary<string, TreeLook?> _looks = new();
        private static readonly object _lock = new();

        /// <summary>How the plant of a model id is drawn, or null if it isn't a tree.</summary>
        public static TreeLook? LookOf(string modelId)
        {
            int hash = modelId.IndexOf('#');
            string id = hash >= 0 ? modelId[..hash] : modelId;
            lock (_lock)
            {
                if (_looks.TryGetValue(id, out TreeLook? known)) return known;
                TreeLook? look = FloraCatalog.TryGet(id, out PlantSpecies species) ? Make(species) : null;
                _looks[id] = look;
                return look;
            }
        }

        /// <summary>The seed of a model id, as its plant was grown from, for how tall this one is.</summary>
        public static ulong SeedOf(string modelId)
        {
            int hash = modelId.IndexOf('#');
            if (hash < 0) return 0;
            return ulong.TryParse(modelId.AsSpan(hash + 1), System.Globalization.NumberStyles.HexNumber, null, out ulong seed) ? seed : 0;
        }

        private static TreeLook? Make(PlantSpecies species)
        {
            if (species.Kind != Content.WorldAssetKind.Tree || species.Form is not TreeForm tree) return null;
            float height = tree.Height.Lerp(0.5f);
            float slender = height / MathF.Max(tree.CrownRadius.Lerp(0.5f) * height * 2f, 0.5f);
            float crownBase = tree.CrownBase.Lerp(0.5f);
            float crownRadius = tree.CrownRadius.Lerp(0.5f) * height;
            var look = new TreeLook
            {
                MetresTall = height,
                Trunk = Math.Clamp(crownBase, 0.12f, 0.55f),
                Width = Math.Clamp(crownRadius * 2f / 9f, 0.7f, 1.35f),
                Aspect = Math.Clamp(height / MathF.Max(crownRadius * 2f, 0.5f), 1.05f, 2.6f),
                Wood = ChartInk.Tone(tree.Bark.Colour, 0.32f),
            };

            Palette? leaves = tree.Foliage?.Colours ?? tree.Head?.Colours;
            if (leaves != null)
            {
                look.Shade = ChartInk.Tone(leaves.Shade, 0.24f);
                look.Body = ChartInk.Tone(leaves.Body, 0.4f);
                look.Sun = ChartInk.Tone(leaves.Sun, 0.58f);
            }

            string id = species.Id;
            if (id.StartsWith("bamboo"))
            {
                look.Shape = TreeShape.Bamboo;
                look.Aspect = 1.9f;
                look.Width = 0.9f;
            }
            else if (id.StartsWith("cactus"))
            {
                look.Shape = TreeShape.Cactus;
                look.Aspect = 1.45f;
                look.Width = 0.9f;
                // A cactus is its own green: its stem is all there is of it.
                look.Shade = ChartInk.Tone(tree.Bark.Colour, 0.22f);
                look.Body = ChartInk.Tone(tree.Bark.Colour, 0.38f);
                look.Sun = ChartInk.Tone(tree.Bark.Colour, 0.52f);
            }
            else if (tree.Dead != null || leaves == null)
            {
                look.Shape = TreeShape.Snag;
                look.Aspect = 1.45f;
                look.Width = 0.85f;
            }
            else if (tree.Head != null)
            {
                look.Shape = TreeShape.Palm;
                // A tree fern or a cycad stands short under its fronds, a palm tall.
                look.Aspect = height < 9f ? 1.05f : 1.5f;
                look.Width = 1.15f;
            }
            else if (tree.Foliage!.Pose == CardPose.Hanging)
            {
                look.Shape = TreeShape.Weeping;
                look.Aspect = Math.Clamp(look.Aspect, 1.05f, 1.3f);
            }
            else
            {
                look.Shape = tree.Crown switch
                {
                    CrownShape.Conical => TreeShape.Spire,
                    CrownShape.Flame or CrownShape.TaperedColumn or CrownShape.Cylindrical => slender > 2.8f ? TreeShape.Column : TreeShape.Spire,
                    // Flat on top only when the crown sits high on its trunk: a cherry's low wide crown is a dome.
                    CrownShape.Umbrella => crownBase > 0.36f ? TreeShape.Umbrella : TreeShape.Round,
                    CrownShape.InverseCone => TreeShape.Vase,
                    _ => TreeShape.Round,
                };
                switch (look.Shape)
                {
                    case TreeShape.Spire:
                        look.Aspect = Math.Clamp(look.Aspect, 1.6f, 2.4f);
                        look.Width = Math.Clamp(look.Width, 0.7f, 1.05f);
                        look.Trunk = MathF.Min(look.Trunk, 0.14f);
                        break;
                    case TreeShape.Column:
                        look.Aspect = Math.Clamp(look.Aspect, 2.1f, 2.7f);
                        look.Width = 0.62f;
                        look.Trunk = 0.08f;
                        break;
                    case TreeShape.Umbrella:
                        look.Aspect = Math.Clamp(look.Aspect, 1f, 1.15f);
                        look.Width = MathF.Max(look.Width, 1.15f);
                        look.Trunk = Math.Clamp(look.Trunk, 0.3f, 0.42f);
                        break;
                    default:
                        // A crown at least about as tall as it's wide, or a wide one on its trunk reads as a mushroom.
                        look.Aspect = Math.Clamp(MathF.Max(look.Aspect, 0.95f / (1f - look.Trunk)), 1.1f, 1.7f);
                        break;
                }
            }
            return look;
        }
    }
}

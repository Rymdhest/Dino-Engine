using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.World;
using Dino_Engine.World.Content;

namespace Dino_Engine.Modelling.Procedural.Fence
{
    /// <summary>How a fence is put together. Each is a way of building of its own in <see cref="FenceForge"/>.</summary>
    public enum FenceForm
    {
        Rails,      // Split rails let into hewn posts: a field's fence.
        Paling,     // Pointed pales nailed to two rails: a garden's.
        Wattle,     // Rods woven between stakes, in hurdles lashed to posts.
        Bamboo,     // A lattice of bamboo tied with black cord at every crossing, as the East fences a garden.
        Stockade,   // Sharpened logs stood side by side and lashed: what a village keeps its people behind.
        Pen,        // The Reich's: taller, tarred, and bound with black iron. What a brood keeps its beasts in.
        DryStone,   // Field stones laid without mortar, with a row of copes on edge along the top.
        Churchyard, // Coursed and mortared, under a saddleback coping, with capped piers at its ends.
        Roofed,     // Plaster over a stone plinth under a little roof of its own, round holy ground in the East.
        Curtain,    // A keep's wall: battered at the foot, with a walk along the top behind crenellations.
    }

    /// <summary>What a fence is for, which with how a world builds decides what kind it is.</summary>
    public enum FenceUse
    {
        Field,
        Garden,
        Yard,
        Stockade,
        Pen,
        Churchyard,
        Curtain,
    }

    /// <summary>
    /// A kind of fence: how it's built, and the numbers the world needs with no geometry at all. A fence is posts and spans between them, and a span is built
    /// for the slope it stands on, in steps of <see cref="Step"/>, so that what stands upright stays upright: see <see cref="FenceLayout"/>.
    /// </summary>
    public sealed class FenceKind
    {
        public readonly string Id;
        public readonly FenceForm Form;

        /// <summary>Half its width on the ground, which is what's solid.</summary>
        public readonly float HalfThickness;

        /// <summary>Metres up that it stops what flies, which goes at 3.2 m: a field's fence is passed over, as anything waist-high would be, and a stockade isn't.</summary>
        public readonly float Height;

        /// <summary>Metres from one post to the next, from which each style draws its own.</summary>
        public readonly FloatRange Bay;

        /// <summary>The slopes a span is built for are this far apart, as rise over run. What's left over tilts it, uprights and all, so finer is straighter and costs more models.</summary>
        public readonly float Step;

        /// <summary>Whether a post stands between one span and the next along a straight run. A wall's spans just meet.</summary>
        public readonly bool LinePosts;

        /// <summary>How far below the line from post to post it reaches into the ground, so that a dip between two posts shows no daylight under it.</summary>
        public readonly float Sink;

        public readonly int SpanVariants;
        public readonly int PostVariants;

        public FenceKind(string id, FenceForm form, float halfThickness, float height, FloatRange bay, float step, bool linePosts, float sink, int spanVariants = 2, int postVariants = 2)
        {
            Id = id;
            Form = form;
            HalfThickness = halfThickness;
            Height = height;
            Bay = bay;
            Step = step;
            LinePosts = linePosts;
            Sink = sink;
            SpanVariants = spanVariants;
            PostVariants = postVariants;
        }

        public override string ToString() => Id;
    }

    /// <summary>
    /// Every kind of fence there is. Nothing about a fence is stored: a world draws a few styles of each kind from its seed, a site picks one, and a model id says the kind, the style,
    /// the piece and the slope it's built for, which is all it takes to build it again (<see cref="FenceSpec"/>).
    /// </summary>
    public static class FenceCatalog
    {
        /// <summary>Styles of each kind in a world. Two villages of one world fence their fields differently, as two families would.</summary>
        public const int StylesPerWorld = 3;

        public static readonly FenceKind Rails = new("rails", FenceForm.Rails, 0.25f, 3f, new FloatRange(3.4f, 4.2f), 0.05f, linePosts: true, sink: 0.6f);
        public static readonly FenceKind Paling = new("paling", FenceForm.Paling, 0.25f, 2.8f, new FloatRange(3f, 3.6f), 0.05f, linePosts: true, sink: 0.4f);
        public static readonly FenceKind Wattle = new("wattle", FenceForm.Wattle, 0.25f, 2.7f, new FloatRange(3.3f, 4f), 0.05f, linePosts: true, sink: 0.35f);
        public static readonly FenceKind Bamboo = new("bamboo", FenceForm.Bamboo, 0.25f, 3f, new FloatRange(3.4f, 4f), 0.05f, linePosts: true, sink: 0.5f);
        public static readonly FenceKind Stockade = new("stockade", FenceForm.Stockade, 0.55f, 7f, new FloatRange(3.8f, 4.4f), 0.04f, linePosts: true, sink: 0.9f);
        public static readonly FenceKind Pen = new("pen", FenceForm.Pen, 0.55f, 8f, new FloatRange(4f, 4.6f), 0.04f, linePosts: true, sink: 0.9f);
        public static readonly FenceKind DryStone = new("dry-stone", FenceForm.DryStone, 0.6f, 3f, new FloatRange(3.4f, 4f), 0.05f, linePosts: false, sink: 0.6f, spanVariants: 3);
        public static readonly FenceKind Churchyard = new("churchyard", FenceForm.Churchyard, 0.8f, 3.1f, new FloatRange(4.4f, 5.2f), 0.04f, linePosts: false, sink: 1f);
        public static readonly FenceKind Roofed = new("roofed", FenceForm.Roofed, 0.8f, 4.2f, new FloatRange(4.4f, 5.2f), 0.04f, linePosts: false, sink: 1f);
        public static readonly FenceKind Curtain = new("curtain", FenceForm.Curtain, 0.9f, 9f, new FloatRange(4.6f, 5.4f), 0.03f, linePosts: false, sink: 1.6f);

        public static readonly FenceKind[] All = { Rails, Paling, Wattle, Bamboo, Stockade, Pen, DryStone, Churchyard, Roofed, Curtain };

        private static readonly Dictionary<string, FenceKind> ById = All.ToDictionary(kind => kind.Id);

        public static bool TryGet(string id, out FenceKind kind) => ById.TryGetValue(id, out kind!);

        public static FenceKind Get(string id) => ById[id];

        /// <summary>What a site of a world built this way puts up for a use. <paramref name="random"/> is a fork of the site's own, used for this alone.</summary>
        public static FenceKind Choose(FenceUse use, BuildingStyle style, WorldRandom random)
        {
            float roll = random.Float();
            return use switch
            {
                // A field is fenced light and in wood, as it's long and has only to keep beasts in: stone is for walls round holy ground and gardens by a house.
                FenceUse.Field => style switch
                {
                    BuildingStyle.Eastern => roll < 0.55f ? Bamboo : Rails,
                    // Split rails where the trees are tall, brush woven on stakes where they're few, cane over the wet.
                    BuildingStyle.Log => roll < 0.7f ? Rails : Wattle,
                    BuildingStyle.Adobe => roll < 0.6f ? Wattle : Rails,
                    BuildingStyle.Stilt => roll < 0.6f ? Bamboo : Wattle,
                    BuildingStyle.Tiled => roll < 0.6f ? Rails : Paling,
                    _ => roll < 0.65f ? Rails : roll < 0.85f ? Wattle : Paling,
                },
                FenceUse.Garden => style switch
                {
                    BuildingStyle.Eastern => Bamboo,
                    BuildingStyle.Stone => roll < 0.45f ? Paling : roll < 0.75f ? DryStone : Wattle,
                    BuildingStyle.Log => roll < 0.5f ? Paling : Wattle,
                    // The dry country and the sun's walls its gardens with the stones it clears from them.
                    BuildingStyle.Adobe => roll < 0.55f ? DryStone : Wattle,
                    BuildingStyle.Stilt => Bamboo,
                    BuildingStyle.Tiled => roll < 0.6f ? DryStone : Paling,
                    _ => roll < 0.5f ? Wattle : Paling,
                },
                FenceUse.Yard => style switch
                {
                    BuildingStyle.Eastern or BuildingStyle.Stilt => Bamboo,
                    BuildingStyle.Adobe => Wattle,
                    _ => Rails,
                },
                FenceUse.Stockade => Stockade,
                FenceUse.Pen => Pen,
                FenceUse.Churchyard => style == BuildingStyle.Eastern ? Roofed : Churchyard,
                _ => Curtain,
            };
        }

        /// <summary>The seed of the <paramref name="index"/>th style of a kind in a world.</summary>
        public static ulong StyleSeed(int worldSeed, FenceKind kind, int index)
        {
            uint name = 0;
            foreach (char c in kind.Id) name = FloraNoise.Hash(name ^ c);
            uint low = FloraNoise.Hash((uint)worldSeed * 0x85EBCA6Bu ^ name ^ (uint)index * 0xC2B2AE35u);
            return low & 0xFFFFFF;
        }
    }
}

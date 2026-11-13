using OpenTK.Mathematics;

namespace Dino_Engine.Rendering.Shells
{
    /// <summary>What a growth is, which decides how it's drawn: moss lies still and goes under snow, fur is combed back along a body and moves with it.</summary>
    public enum ShellGrowth : byte
    {
        Moss,
        Fur,
    }

    /// <summary>
    /// Where on a surface it grows. <see cref="Vertex"/> reads each vertex's own amount (<c>Vertex.Shell</c>, <c>FloraVertex.Shell</c>); <see cref="Rock"/> grows wherever
    /// Rock.glsl paints moss on stone, by the same rule (RockGrowth.glsl), so the two can't disagree; <see cref="Ground"/> grows on the ground's moss and on its
    /// mossy rock, by the terrain's own weights.
    /// </summary>
    public enum ShellMask : byte
    {
        Vertex,
        Rock,
        Ground,
    }

    /// <summary>
    /// One kind of growth drawn as shells (SHELLS.md): a stack of layers pushed out along the surface, each cut from one height field of clumps (moss's cushions,
    /// fur's locks) and the fine strands over them. Lengths are metres at the world's scale, which is about twice real, as everything else is. Headless: a style
    /// is data, and the GPU's copy of it is <c>ShellRenderer</c>'s.
    /// </summary>
    public sealed record ShellStyle
    {
        public required string Name { get; init; }
        public ShellGrowth Growth { get; init; }
        public ShellMask Mask { get; init; }

        /// <summary>Metres from the surface to the tallest tip.</summary>
        public float Length { get; init; } = 0.06f;

        /// <summary>Metres between the clumps' cells: about how far apart cushions or locks are.</summary>
        public float ClumpSize { get; init; } = 0.08f;

        /// <summary>How wide a clump is, as a share of its cell: at most half, which is what keeps the search to the eight nearest cells exact.</summary>
        public float ClumpWidth { get; init; } = 0.45f;

        /// <summary>The share of cells holding a clump where it grows thickest. Where it thins, clumps go first from the edges in.</summary>
        public float ClumpFill { get; init; } = 0.85f;

        /// <summary>How high the even layer between clumps stands, as a share of the length: moss's carpet, fur's undercoat.</summary>
        public float Carpet { get; init; } = 0.4f;

        /// <summary>How much shorter one clump can be than the tallest, as a share.</summary>
        public float HeightVariety { get; init; } = 0.45f;

        /// <summary>A clump's profile as a power of a dome: 1 is a cushion, more is a tuft drawn to a point.</summary>
        public float Shape { get; init; } = 1f;

        /// <summary>How much longer a clump is along the way it's combed than across it: 1 for moss, which isn't combed.</summary>
        public float Stretch { get; init; } = 1f;

        /// <summary>How far the fine strands reach above a clump's top, as a share of the length, thinning as they go: the fuzz of a silhouette.</summary>
        public float Fringe { get; init; } = 0.25f;

        /// <summary>Metres across a wisp of the fine strands, which only shows where it's more than a pixel or two.</summary>
        public float StrandSize { get; init; } = 0.012f;

        /// <summary>How much darker it is down between clumps than on their crowns, 0 to 1: the creases between cushions, the partings between locks.</summary>
        public float Crease { get; init; } = 0.5f;

        /// <summary>How many lattices of clumps there are, one or two: a second, laid between the first's, crowds them as a coat's locks are crowded, at twice the cost.</summary>
        public int Lattices { get; init; } = 1;

        /// <summary>
        /// The height the layers start from, as a share of the length: below it they'd only ever be inside what grows, under the layers that hide them. Whatever's
        /// lower than it is the surface's own painted colour, which from the game's camera is less than a pixel off.
        /// </summary>
        public float Floor { get; init; } = 0.15f;

        /// <summary>How much a clump's shade differs from the next one's, as a share.</summary>
        public float Variety { get; init; } = 0.18f;

        /// <summary>What the root's colour is of what grows, as a share: the light that gets down there.</summary>
        public float RootShade { get; init; } = 0.5f;

        /// <summary>The tip's colour as a multiple of what grows, per channel: new growth is paler and warmer.</summary>
        public Vector3 TipTint { get; init; } = new Vector3(1.15f, 1.15f, 1.0f);

        /// <summary>How much of the sky the root sees, 0..1, rising to all of it at the tips.</summary>
        public float RootOcclusion { get; init; } = 0.4f;

        /// <summary>How much light the tips let through, as a leaf does (gBufferUtil.glsl's transmittance).</summary>
        public float Translucency { get; init; } = 0.3f;

        public float Roughness { get; init; } = 0.9f;

        /// <summary>How much water it drinks in the rain (Weather/Wetness.glsl), as a material's porosity.</summary>
        public float Porosity { get; init; } = 0.85f;

        /// <summary>How far the tips are laid back along the comb, in lengths.</summary>
        public float Lean { get; init; }

        /// <summary>How far the tips hang down, in lengths.</summary>
        public float Droop { get; init; }

        /// <summary>How far the tips go with the wind, in lengths at the vegetation's reference speed.</summary>
        public float Sway { get; init; }

        /// <summary>The most layers it's ever drawn with. Fewer are drawn when its length covers fewer pixels: two to a pixel is as fine as anything can be seen.</summary>
        public int MostShells { get; init; } = 12;

        /// <summary>The fewest, however small it is on screen: below about four the layers read as steps.</summary>
        public int LeastShells { get; init; } = 4;

        /// <summary>Metres from the camera past which it starts to lie down to nothing, and where it's gone.</summary>
        public float FadeFrom { get; init; } = 80f;
        public float FadeTo { get; init; } = 110f;
    }
}

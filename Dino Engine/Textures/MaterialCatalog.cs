namespace Dino_Engine.Textures
{
    /// <summary>
    /// What a material is, as far as anything but the GPU needs to know: its name, how big a tile of it is in the world, how deep its relief goes, and how much water
    /// it drinks. The relief is a depth in metres at that size, and it's the one number the normal map, the ambient occlusion and the parallax are all made from
    /// (<see cref="ReliefRatio"/>), so they can never disagree: a mesh that maps the material at half the size gets half the depth and the same slopes. Headless, so a
    /// generator on another thread can ask how big to make its texture coordinates. See TEXTURES.md.
    /// </summary>
    public sealed class MaterialSpec
    {
        public string Name { get; }

        /// <summary>Metres a tile of it spans, both ways, at the size it was made for.</summary>
        public float TileMetres { get; }

        /// <summary>Metres from its highest point to its lowest at that size. 0 for a flat material, which gets no parallax.</summary>
        public float ReliefMetres { get; }

        /// <summary>How much water it takes in, 0 to 1 (Weather/Wetness.glsl): sand and cloth drink it up and darken, stone takes a little and shines, metal takes none.</summary>
        public float Porosity { get; }

        /// <summary>Its layer in the material arrays.</summary>
        public int Index { get; internal set; } = -1;

        /// <summary>Relief over tile width: the slope a height of 1 over a whole tile would have. What the normals are made with and what the parallax marches to.</summary>
        public float ReliefRatio => ReliefMetres / TileMetres;

        /// <summary>Texture repeats per metre for a mesh that wants it at its natural size.</summary>
        public float TilesPerMetre => 1f / TileMetres;

        internal MaterialSpec(string name, float tileMetres, float reliefMetres, float porosity)
        {
            Name = name;
            TileMetres = tileMetres;
            ReliefMetres = reliefMetres;
            Porosity = porosity;
        }
    }

    /// <summary>
    /// Every tiling material there is, in the order of their layers. How each looks is its recipe (<see cref="MaterialRecipes"/>); everything here is what the rest of the
    /// engine may know without a GPU. A new material is a line here and a recipe there. The world's scale is about twice a real one's, as a person is four metres tall,
    /// so sizes are a real material's times that.
    /// </summary>
    public static class MaterialCatalog
    {
        private static readonly List<MaterialSpec> _all = new();

        // Plain and glowing: no relief, as nothing about them stands out.
        public static readonly MaterialSpec Flat = Add("flat", 1f, 0f, 0.3f);
        public static readonly MaterialSpec FlatGlow = Add("flat-glow", 1f, 0f, 0f);
        public static readonly MaterialSpec Mirror = Add("mirror", 1f, 0f, 0f);
        public static readonly MaterialSpec Gem = Add("gem", 1f, 0f, 0f);
        public static readonly MaterialSpec Wax = Add("wax", 1f, 0.002f, 0.04f);
        public static readonly MaterialSpec Grain = Add("grain", 1f, 0f, 0.7f);
        public static readonly MaterialSpec FoliageWhite = Add("foliage-white", 1f, 0f, 0.12f);

        // The ground (TerrainSurface's palette), seen from 33 m up at 67 degrees: nothing finer than a few centimetres reads.
        public static readonly MaterialSpec Soil = Add("soil", 3f, 0.05f, 0.8f);
        public static readonly MaterialSpec Grass = Add("grass", 3f, 0.045f, 0.35f);
        public static readonly MaterialSpec GrassySoil = Add("grassy-soil", 3f, 0.05f, 0.7f);
        public static readonly MaterialSpec Sand = Add("sand", 2.4f, 0.03f, 0.9f);
        public static readonly MaterialSpec SandDunes = Add("sand-dunes", 8f, 0.12f, 0.85f);
        public static readonly MaterialSpec CrackedDesert = Add("cracked-earth", 4f, 0.06f, 0.85f);
        public static readonly MaterialSpec CrackedLava = Add("ash", 4f, 0.08f, 0.6f);
        public static readonly MaterialSpec Snow = Add("snow", 5f, 0.07f, 0f);
        public static readonly MaterialSpec Ice = Add("ice", 6f, 0.012f, 0f);
        public static readonly MaterialSpec Mud = Add("mud", 3f, 0.05f, 0.4f);
        public static readonly MaterialSpec Moss = Add("moss", 2.4f, 0.04f, 0.7f);
        public static readonly MaterialSpec Gravel = Add("gravel", 1.6f, 0.05f, 0.5f);
        public static readonly MaterialSpec Pebbles = Add("pebbles", 2.4f, 0.09f, 0.5f);
        public static readonly MaterialSpec Cobble = Add("cobble", 3.2f, 0.15f, 0.42f);
        // Roads, read in their own frame (World/RoadFrames.cs). A street's tile is its width, kerb to kerb, so its setts come out square; a dirt road is deep
        // enough for the ruts the terrain sinks into it (Terrain.frag's roadShape) and the stones trodden into it, and is the trodden ground of a village too.
        public static readonly MaterialSpec Setts = Add("setts", 6f, 0.1f, 0.35f);
        public static readonly MaterialSpec RoadDirt = Add("road-dirt", 3f, 0.1f, 0.75f);
        public static readonly MaterialSpec Farmland = Add("farmland", 4f, 0.14f, 0.85f);

        /// <summary>The world's bedrock, projected three ways over cliffs and stones (Rock.glsl) at its own two sizes, and never parallaxed: its relief is only its normals'.</summary>
        public static readonly MaterialSpec Cliff = Add("cliff", 11.3f, 1.4f, 0.35f);
        public static readonly MaterialSpec Rock = Add("rock", 3f, 0.07f, 0.35f);

        // What's built. A wall is seen at 67 degrees or more, so its relief shows mostly as the parallax of its joints.
        // Deep, as Crimson Desert's walls are: a stone stands ten centimetres (real) out of its joint, a slate's tail a few out of the course below, and that, in
        // the shadows it throws and the light rolling over its edges, is what makes a wall look built and not painted (Surface/Relief.glsl keeps it steady).
        public static readonly MaterialSpec StoneWall = Add("stone-wall", 4f, 0.15f, 0.45f);
        public static readonly MaterialSpec Brick = Add("brick", 2.6f, 0.07f, 0.7f);
        public static readonly MaterialSpec Plaster = Add("plaster", 4f, 0.03f, 0.75f);
        public static readonly MaterialSpec Wood = Add("wood", 2.4f, 0.035f, 0.65f);
        public static readonly MaterialSpec Thatch = Add("thatch", 3f, 0.12f, 0.8f);
        public static readonly MaterialSpec Shingle = Add("shingle", 2.4f, 0.08f, 0.5f);
        public static readonly MaterialSpec Slate = Add("slate", 2.4f, 0.07f, 0.15f);
        public static readonly MaterialSpec MetalFloor = Add("metal-floor", 2f, 0.02f, 0f);
        public static readonly MaterialSpec Iron = Add("iron", 2f, 0.006f, 0f);

        // Bark, pale, as a tree's colour is in its vertices. BarkSkin wraps a whole number of tiles round a trunk.
        public static readonly MaterialSpec BarkFurrowed = Add("bark-furrowed", 2.2f, 0.07f, 0.7f);
        public static readonly MaterialSpec BarkPlated = Add("bark-plated", 2f, 0.055f, 0.7f);
        public static readonly MaterialSpec BarkPapery = Add("bark-papery", 2.2f, 0.016f, 0.6f);
        public static readonly MaterialSpec BarkSmooth = Add("bark-smooth", 2f, 0.01f, 0.45f);
        public static readonly MaterialSpec BarkFibrous = Add("bark-fibrous", 2.4f, 0.05f, 0.75f);
        public static readonly MaterialSpec BarkRinged = Add("bark-ringed", 2f, 0.045f, 0.6f);
        public static readonly MaterialSpec BarkBare = Add("bark-bare", 2f, 0.022f, 0.6f);
        public static readonly MaterialSpec BarkCharred = Add("bark-charred", 2f, 0.04f, 0.5f);
        public static readonly MaterialSpec PineBark = Add("bark-pine", 2f, 0.055f, 0.7f);

        // What's worn and carried, white or near it, as what colour it is goes in its vertices (GEAR.md).
        public static readonly MaterialSpec Plate = Add("plate", 1.5f, 0.005f, 0f);
        public static readonly MaterialSpec Mail = Add("mail", 0.6f, 0.014f, 0.05f);
        public static readonly MaterialSpec Leather = Add("leather", 1f, 0.005f, 0.45f);
        public static readonly MaterialSpec Cloth = Add("cloth", 0.6f, 0.004f, 0.9f);
        public static readonly MaterialSpec Bone = Add("bone", 1f, 0.006f, 0.3f);

        // The waygates' own stone, dressed into slabs and laid as paving, as the Haven lays its forecourt and its square (WAYGATES.md): the same face as the gates' blocks,
        // in their colour, with the same bevel to every joint. Last, so that adding it moved no other material's layer. Its tile is wide, as slabs a metre across repeat
        // as wallpaper on anything smaller.
        public static readonly MaterialSpec Flagstones = Add("flagstones", 8f, 0.08f, 0.35f);

        public static IReadOnlyList<MaterialSpec> All => _all;

        public static int Count => _all.Count;

        /// <summary>The material in a layer, or null for a card or anything else that isn't one of these.</summary>
        public static MaterialSpec? Of(int index) => index >= 0 && index < _all.Count ? _all[index] : null;

        /// <summary>Texture repeats per metre for the material in a layer, or 1 for anything that isn't a tiling material.</summary>
        public static float TilesPerMetre(int index) => Of(index)?.TilesPerMetre ?? 1f;

        private static MaterialSpec Add(string name, float tileMetres, float reliefMetres, float porosity)
        {
            var spec = new MaterialSpec(name, tileMetres, reliefMetres, porosity) { Index = _all.Count };
            _all.Add(spec);
            return spec;
        }
    }
}

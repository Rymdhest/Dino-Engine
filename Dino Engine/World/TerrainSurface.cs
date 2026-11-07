using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.World.WorldMath;

namespace Dino_Engine.World
{
    /// <summary>What the grass is like at a point of the ground.</summary>
    public struct GrassCell
    {
        public float Cover;     // 0..1, how well it grows.
        public float Height;    // 0..1 of the world's longest blades.
        public float Dryness;   // 0..1, how much of it has turned to straw.
        public Vector3 Colour;
        public float Plumes;    // 0..1, share of its tufts carrying seed heads.
        public float Bloom;     // 0..1, share of its blades that are flowers.
        public Vector3 BloomColour;
        public float Hollow;    // 0..1, how soon water stands here: the last places to fill are near 0.
    }

    /// <summary>
    /// Decides what the ground is made of at any point: how much of each material of the world's palette, and what the grass
    /// is like. This is where biomes meet the renderer. The terrain shader only blends what it is told, so everything that
    /// decides how the land looks can be read and changed here, and no material is ever tied to an altitude in a shader.
    /// </summary>
    public sealed class TerrainSurface
    {
        /// <summary>Most materials a world can paint with. The terrain shader reads this many weights per texel.</summary>
        public const int MaxMaterials = 12;

        private readonly WorldData _world;
        private readonly WorldNoise _noise;
        private readonly int[] _slot = new int[Enum.GetValues<SurfaceMaterial>().Length];
        private readonly BiomeDefinition[] _biomes;

        /// <summary>The materials of this world; a material's position here is its weight's index.</summary>
        public readonly List<SurfaceMaterial> Palette = new();

        public TerrainSurface(WorldData world)
        {
            _world = world;
            _noise = new WorldNoise(world.Random.Fork("surface"));
            _biomes = world.Plan.Biomes.ToArray();
            BuildPalette();
        }

        private void BuildPalette()
        {
            Array.Fill(_slot, -1);

            // Road surfaces and farmland first: they are the ones that can't be mistaken for something else.
            AddMaterial(SurfaceMaterial.Cobble);
            AddMaterial(SurfaceMaterial.Dirt);
            AddMaterial(SurfaceMaterial.Farmland);
            AddMaterial(SurfaceMaterial.Gravel);
            AddMaterial(SurfaceMaterial.Rock);
            foreach (BiomeDefinition biome in _biomes)
            {
                AddMaterial(biome.Ground);
                AddMaterial(biome.Shore);
                AddMaterial(biome.Slope);
                AddMaterial(biome.Understory);
                AddMaterial(biome.Path);
            }

            // A world with more materials than slots paints the last few with their nearest cousin.
            for (int i = 0; i < _slot.Length; i++)
            {
                if (_slot[i] >= 0) continue;
                SurfaceMaterial cousin = Cousin((SurfaceMaterial)i);
                _slot[i] = _slot[(int)cousin] >= 0 ? _slot[(int)cousin] : _slot[(int)SurfaceMaterial.Rock];
            }
        }

        private void AddMaterial(SurfaceMaterial material)
        {
            if (_slot[(int)material] >= 0 || Palette.Count >= MaxMaterials) return;
            _slot[(int)material] = Palette.Count;
            Palette.Add(material);
        }

        private static SurfaceMaterial Cousin(SurfaceMaterial material) => material switch
        {
            SurfaceMaterial.GrassySoil or SurfaceMaterial.Moss => SurfaceMaterial.Soil,
            SurfaceMaterial.Soil or SurfaceMaterial.Mud => SurfaceMaterial.Dirt,
            SurfaceMaterial.Dunes => SurfaceMaterial.Sand,
            SurfaceMaterial.Sand or SurfaceMaterial.CrackedEarth => SurfaceMaterial.Gravel,
            SurfaceMaterial.Ice => SurfaceMaterial.Snow,
            _ => SurfaceMaterial.Rock,
        };

        /// <summary>
        /// Fills <paramref name="weights"/> (<see cref="MaxMaterials"/> long, not normalized) for a point of the ground, and
        /// <paramref name="grass"/> with its grass. <paramref name="normalY"/> is the ground normal's y.
        /// </summary>
        public void Evaluate(float x, float z, float height, float normalY, Span<float> weights, out GrassCell grass)
        {
            weights.Clear();
            WorldData world = _world;
            Vector2 position = new Vector2(x, z);

            float paved = world.PavedRoad.Sample(x, z);
            float trail = world.TrailRoad.Sample(x, z);
            float road = MathF.Max(paved, trail);
            float yard = world.Yard.Sample(x, z);
            float plaza = world.Plaza.Sample(x, z);
            float farm = world.Farmland.Sample(x, z);
            float canopy = world.Canopy.Sample(x, z);

            // Noise at a couple of scales keeps every border below from being a clean line.
            float breakup = _noise.Fbm(x, z, 9f, 2);
            float drift = _noise.Fbm(x - 311f, z + 77f, 37f, 2);

            float slope = MathF.Sqrt(MathF.Max(1f - normalY * normalY, 0f)) / MathF.Max(normalY, 0.05f);
            float rock = Smoothstep(0.62f, 1.02f, slope + breakup * 0.14f);

            // A mountain is bare down to quite gentle ground, and what lies back on it, a ledge or a bench, is where anything grows.
            float bare = world.Rock.Sample(x, z);
            rock = MathF.Max(rock, bare * Smoothstep(0.26f, 0.5f, slope + breakup * 0.2f));
            float scree = world.Scree.Sample(x, z) * (1f - Smoothstep(0.75f, 1.25f, slope));

            float above = height - world.WaterLevelAt(position);
            float shore = 1f - Smoothstep(0.3f, 1.8f, above + breakup * 0.45f + drift * 0.4f);

            // Trodden ground has no clean edge: it frays out into the grass around it.
            yard = Smoothstep(0.22f, 0.78f, yard + (breakup * 0.55f + drift * 0.5f) * Saturate(yard * 3f));
            plaza = Smoothstep(0.22f, 0.78f, plaza + (breakup * 0.4f + drift * 0.3f) * Saturate(plaza * 3f));

            float notRoad = 1f - road;
            float natural = notRoad * (1f - rock) * (1f - shore) * (1f - plaza) * (1f - farm) * (1f - yard * 0.85f);
            float shade = Saturate(canopy * 1.25f + drift * 0.3f);

            float cover = 0f;
            float blade = 0f;
            float dry = 0f;
            Vector3 colour = Vector3.Zero;
            float plumes = 0f;
            float bloom = 0f;
            Vector3 bloomColour = Vector3.Zero;
            // Flowers grow in drifts, each of one colour.
            float bloomPick = Smoothstep(0.4f, 0.6f, _noise.Fbm01(x + 211f, z + 47f, 19f, 2));

            for (int b = 0; b < _biomes.Length; b++)
            {
                float share = world.BiomeWeights[b].Sample(x, z);
                if (share <= 0.002f) continue;
                BiomeDefinition biome = _biomes[b];

                // Sand doesn't lie on a ledge: what's there is what's come off the rock above it.
                bool loose = biome.Ground is SurfaceMaterial.Sand or SurfaceMaterial.Dunes or SurfaceMaterial.CrackedEarth;
                float ledge = loose ? bare : 0f;
                weights[_slot[(int)biome.Ground]] += share * natural * (1f - shade) * (1f - ledge);
                weights[_slot[(int)biome.Understory]] += share * natural * shade * (1f - ledge);
                weights[_slot[(int)SurfaceMaterial.Gravel]] += share * natural * ledge;
                weights[_slot[(int)biome.Shore]] += share * notRoad * shore * (1f - rock);
                weights[_slot[(int)biome.Slope]] += share * notRoad * rock;
                weights[_slot[(int)biome.Path]] += share * trail;

                // Little grass grows where a canopy takes the light.
                cover += share * biome.GrassCover * (1f - canopy * 0.55f);
                blade += share * biome.GrassHeight;
                dry += share * biome.GrassDryness;
                colour += share * biome.GrassColour;
                plumes += share * biome.GrassPlumes;
                bloom += share * biome.GrassBloom;
                bloomColour += share * Vector3.Lerp(biome.GrassBloomColour, biome.GrassBloomColourTwo, bloomPick);
            }

            weights[_slot[(int)SurfaceMaterial.Gravel]] += scree * rock * notRoad * 2f;
            weights[_slot[(int)SurfaceMaterial.Cobble]] += paved;
            weights[_slot[(int)SurfaceMaterial.Dirt]] += yard * notRoad * (1f - plaza);
            weights[_slot[(int)SurfaceMaterial.Gravel]] += plaza * notRoad;
            weights[_slot[(int)SurfaceMaterial.Farmland]] += farm * notRoad;

            float small = 0.8f + 0.2f * _noise.Fbm01(x, z, 2.2f, 2);
            float broad = 0.72f + 0.28f * _noise.Fbm01(x + 91f, z - 13f, 6f, 2);
            float grows = (1f - rock) * (1f - bare * 0.55f) * notRoad * (1f - plaza) * (1f - farm) * (1f - yard * 0.7f);
            // Grass thins out over the last of the dry ground and stops at the water.
            grows *= Smoothstep(0.02f, 0.5f, above);

            float hollow = Hollow(position, height, slope, MathF.Max(road, MathF.Max(MathF.Max(yard, plaza), farm)), shore);

            // Broad drifts of lighter and darker grass, so that a field has an ebb and flow to it.
            float tint = 1f + drift * 0.14f;
            grass = new GrassCell
            {
                Cover = Saturate(cover * small * broad * grows * normalY),
                Height = Saturate(blade * (1f - canopy * 0.3f)),
                Dryness = Saturate(dry + drift * 0.08f),
                Colour = colour * tint,
                Plumes = Saturate(plumes),
                Bloom = Saturate(bloom * Smoothstep(0.42f, 0.62f, _noise.Fbm01(x - 57f, z + 133f, 31f, 2)) * (1f - canopy)),
                BloomColour = bloomColour,
                Hollow = hollow,
            };
        }

        /// <summary>How soon water stands at a point of the ground, 0..1, the last places to fill being near 0. It's what the terrain is painted with, for anything that has to agree with it.</summary>
        public float HollowAt(Vector2 position)
        {
            float x = position.X, z = position.Y;
            float height = _world.Height.Sample(position);
            float slope = _world.Height.SlopeAt(position);
            float trodden = MathF.Max(MathF.Max(_world.PavedRoad.Sample(x, z), _world.TrailRoad.Sample(x, z)), MathF.Max(MathF.Max(_world.Yard.Sample(x, z), _world.Plaza.Sample(x, z)), _world.Farmland.Sample(x, z)));
            float shore = 1f - Smoothstep(0.3f, 1.8f, height - _world.WaterLevelAt(position));
            return Hollow(position, height, slope, trodden, shore);
        }

        // Trodden ground dips and ruts and holds water, and the ground's own hollows do too, wherever it doesn't drain away.
        private float Hollow(Vector2 position, float height, float slope, float trodden, float shore)
        {
            float x = position.X, z = position.Y;
            float blotches = Smoothstep(0.42f, 0.74f, _noise.Fbm01(x + 613f, z - 271f, 7f, 3)) + Smoothstep(0.5f, 0.8f, _noise.Fbm01(x - 77f, z + 401f, 2.6f, 2)) * 0.35f;
            float drains = Saturate(slope * 3.5f) + shore * 0.5f;
            return Saturate((Saturate(trodden) * 0.72f + 0.16f) * blotches + Hollowness(position, height)) * (1f - Saturate(drains));
        }

        // How far the ground here lies below what's around it, as a share of what would make a real pool.
        private float Hollowness(Vector2 position, float height)
        {
            const float Reach = 3.5f;
            const float Pool = 0.22f;
            ScalarField ground = _world.Height;
            float around = ground.Sample(position + new Vector2(Reach, 0f)) + ground.Sample(position - new Vector2(Reach, 0f))
                + ground.Sample(position + new Vector2(0f, Reach)) + ground.Sample(position - new Vector2(0f, Reach));
            return Saturate((around * 0.25f - height) / Pool);
        }
    }
}

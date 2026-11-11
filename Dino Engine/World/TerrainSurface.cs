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
            AddMaterial(SurfaceMaterial.Setts);
            AddMaterial(SurfaceMaterial.Road);
            // Trodden ground is the road's own earth, read the world's way instead of the road's wherever no road runs (Terrain.frag).
            _slot[(int)SurfaceMaterial.Dirt] = _slot[(int)SurfaceMaterial.Road];
            AddMaterial(SurfaceMaterial.Cobble);
            // The waygates' stone, only where something's paved with it, as it's no use to a land that has none.
            if (_world.Paved) AddMaterial(SurfaceMaterial.Flagstones);
            // An arena's floor, a pen's and the Onslaught's ash, only where they're laid, and ahead of what a biome brings, as they're the whole of a fight's ground.
            if (_world.Sanded) AddMaterial(SurfaceMaterial.Sand);
            if (_world.Mudded) AddMaterial(SurfaceMaterial.Mud);
            if (_world.Ashen) AddMaterial(SurfaceMaterial.Ash);
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

            // A world with more materials than slots paints the last few with their nearest cousin, and that one's, until one has a slot: a floor of needles goes to soil and then to
            // trodden earth before it would ever go to rock. Rock always has a slot, and every chain ends there.
            for (int i = 0; i < _slot.Length; i++)
            {
                if (_slot[i] >= 0) continue;
                SurfaceMaterial cousin = Cousin((SurfaceMaterial)i);
                for (int step = 0; step < 4 && _slot[(int)cousin] < 0; step++) cousin = Cousin(cousin);
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
            SurfaceMaterial.GrassySoil or SurfaceMaterial.Moss or SurfaceMaterial.Needles or SurfaceMaterial.Litter => SurfaceMaterial.Soil,
            SurfaceMaterial.Soil or SurfaceMaterial.Mud or SurfaceMaterial.RedEarth => SurfaceMaterial.Dirt,
            SurfaceMaterial.Shingle => SurfaceMaterial.Gravel,
            SurfaceMaterial.Dunes => SurfaceMaterial.Sand,
            SurfaceMaterial.Sand or SurfaceMaterial.CrackedEarth => SurfaceMaterial.Gravel,
            SurfaceMaterial.Ice => SurfaceMaterial.Snow,
            SurfaceMaterial.Flagstones => SurfaceMaterial.Cobble,
            _ => SurfaceMaterial.Rock,
        };

        /// <summary>
        /// Fills <paramref name="weights"/> (<see cref="MaxMaterials"/> long, not normalized) for a point of the ground, and
        /// <paramref name="grass"/> with its grass. <paramref name="normalY"/> is the ground normal's y, and <paramref name="road"/> where the point lies on the roads.
        /// </summary>
        public void Evaluate(float x, float z, float height, float normalY, in RoadFrame road, Span<float> weights, out GrassCell grass)
        {
            weights.Clear();
            WorldData world = _world;
            Vector2 position = new Vector2(x, z);

            float paved = world.PavedRoad.Sample(x, z);
            float trail = world.TrailRoad.Sample(x, z);
            float onRoad = MathF.Max(paved, trail);
            float yard = world.Yard.Sample(x, z);
            float plaza = world.Plaza.Sample(x, z);
            float farm = world.Farmland.Sample(x, z);
            float paving = world.Paved ? world.Paving.Sample(x, z) : 0f;
            float canopy = world.Canopy.Sample(x, z);
            // A bed someone tends is nothing trodden, whatever was painted round it.
            float garden = world.Garden.Sample(x, z);
            // What a floor is laid with, where an arena or a pen lays it (ARENAS.md).
            float sand = world.Sanded ? world.Sand.Sample(x, z) : 0f;
            float mud = world.Mudded ? world.Mud.Sample(x, z) : 0f;
            float ash = world.Ashen ? world.Ash.Sample(x, z) : 0f;
            paving *= 1f - garden;
            plaza *= 1f - garden;
            yard *= 1f - garden;

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
            // Gravel is kept, so it frays only at its edge, where it's painted fading out: a square's middle stays a square.
            plaza = Smoothstep(0.22f, 0.78f, plaza + (breakup * 0.4f + drift * 0.3f) * Saturate(plaza * 3f) * Saturate((1f - plaza) * 3f));
            // Paving is laid, not worn: it gives way to what's round it over a stride or so, and where it's shared the slabs stand proudest, so the edge is
            // where its slabs end.
            paving = Smoothstep(0.28f, 0.72f, paving + (breakup * 0.32f + drift * 0.12f) * Saturate(paving * 3f) * Saturate((1f - paving) * 3f));

            float notRoad = 1f - onRoad;
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
            // A square is cobbled, and frays out into the land through a margin of gravel.
            float square = Smoothstep(0.45f, 0.8f, plaza);
            // A street is setts laid square across it between kerbs, read along the road; where roads meet, where it ends or where it runs into a square there's
            // no one way across, and it's cobbles.
            float setts = paved * (1f - road.Junction) * (1f - square);
            weights[_slot[(int)SurfaceMaterial.Setts]] += setts;
            weights[_slot[(int)SurfaceMaterial.Cobble]] += paved - setts;
            weights[_slot[(int)SurfaceMaterial.Dirt]] += yard * notRoad * (1f - plaza);
            weights[_slot[(int)SurfaceMaterial.Cobble]] += square * notRoad;
            weights[_slot[(int)SurfaceMaterial.Gravel]] += (plaza - square) * notRoad;
            weights[_slot[(int)SurfaceMaterial.Farmland]] += farm * notRoad;

            // Under a hall and close round its walls the ground's trodden bare: blades growing there would come up through its floor and stand against its walls inside.
            float hall = HallGround(position);
            weights[_slot[(int)SurfaceMaterial.Dirt]] += hall * notRoad * (1f - plaza);

            // Paving is laid over whatever was there, roads and square alike: a street that runs into it becomes it.
            if (paving > 0f)
            {
                for (int i = 0; i < weights.Length; i++) weights[i] *= 1f - paving;
                weights[_slot[(int)SurfaceMaterial.Flagstones]] += paving;
            }

            // A floor of sand, mud or ash is laid over whatever was there too, paving and all, with an edge a stride wide that frays a little, as anything loose does. Sand is raked, but trodden
            // dark in patches where the fighting goes, and ash is broken by the cracked crust of what's cooled.
            float floor = Saturate(sand + mud + ash);
            if (floor > 0f)
            {
                float fray = (breakup * 0.3f + drift * 0.15f) * Saturate(floor * 3f) * Saturate((1f - floor) * 3f);
                float laid = Smoothstep(0.25f, 0.75f, floor + fray);
                for (int i = 0; i < weights.Length; i++) weights[i] *= 1f - laid;
                float share = laid / floor;
                float trodden = Smoothstep(0.58f, 0.82f, _noise.Fbm01(x + 157f, z - 311f, 5.5f, 3)) * 0.45f;
                float crust = Smoothstep(0.5f, 0.75f, _noise.Fbm01(x - 421f, z + 88f, 9f, 2)) * 0.55f;
                weights[_slot[(int)SurfaceMaterial.Sand]] += sand * share * (1f - trodden);
                weights[_slot[(int)SurfaceMaterial.Dirt]] += sand * share * trodden;
                weights[_slot[(int)SurfaceMaterial.Mud]] += mud * share;
                weights[_slot[(int)SurfaceMaterial.Ash]] += ash * share * (1f - crust);
                weights[_slot[(int)SurfaceMaterial.CrackedEarth]] += ash * share * crust;
            }

            float small = 0.8f + 0.2f * _noise.Fbm01(x, z, 2.2f, 2);
            float broad = 0.72f + 0.28f * _noise.Fbm01(x + 91f, z - 13f, 6f, 2);
            float grows = (1f - rock) * (1f - bare * 0.55f) * notRoad * (1f - plaza) * (1f - farm) * (1f - yard * 0.7f) * (1f - hall) * (1f - paving) * (1f - Saturate(floor * 1.6f));
            // Grass thins out over the last of the dry ground and stops at the water.
            grows *= Smoothstep(0.02f, 0.5f, above);

            // Sand and ash drink whatever falls on them, and mud holds it, as a pen's floor does.
            float hollow = Hollow(position, height, slope, MathF.Max(MathF.Max(MathF.Max(onRoad, paving), MathF.Max(MathF.Max(yard, plaza), farm)), mud), shore) * (1f - Saturate(sand + ash));

            // A track carts seldom use keeps a crown of short grass between its ruts (RoadProfile, where Terrain.frag draws the ruts themselves).
            float crown = RoadProfile.CrownGrass(road, trail * (1f - paved)) * (1f - paving);

            // Broad drifts of lighter and darker grass, so that a field has an ebb and flow to it.
            float tint = 1f + drift * 0.14f;
            grass = new GrassCell
            {
                Cover = Saturate(MathF.Max(cover * small * broad * grows, cover * small * crown) * normalY),
                Height = Saturate(blade * (1f - canopy * 0.3f) * (1f - 0.45f * Saturate(crown * 2f))),
                Dryness = Saturate(dry + drift * 0.08f),
                Colour = colour * tint,
                Plumes = Saturate(plumes),
                Bloom = Saturate(bloom * Smoothstep(0.42f, 0.62f, _noise.Fbm01(x - 57f, z + 133f, 31f, 2)) * (1f - canopy)),
                BloomColour = bloomColour,
                Hollow = hollow,
            };
            if (garden > 0f) Tend(ref grass, garden, x, z, normalY, small);
        }

        // A tended bed's grass: thick, kept short and green, and in flower in small drifts of a few colours, as a gardener plants them, where the land round it grows
        // whatever it grows. Flowers are albedos under a sun of about 13, so they stay deep.
        private static readonly Vector3 GardenGrass = new Vector3(0.15f, 0.3f, 0.065f);
        private static readonly Vector3[] GardenFlowers =
        {
            new Vector3(0.56f, 0.2f, 0.34f),    // blossom pink
            new Vector3(0.52f, 0.5f, 0.47f),    // cream
            new Vector3(0.34f, 0.22f, 0.54f),   // violet
            new Vector3(0.56f, 0.38f, 0.05f),   // marigold
        };
        private const float GardenHeight = 0.42f;
        private const float GardenBloom = 0.42f;

        private void Tend(ref GrassCell grass, float garden, float x, float z, float normalY, float small)
        {
            float drift = _noise.Fbm01(x + 401f, z - 233f, 4.5f, 2);
            float which = _noise.Fbm01(x - 919f, z + 71f, 7f, 2) * 4f;
            int first = Math.Clamp((int)which, 0, 3);
            Vector3 flower = Vector3.Lerp(GardenFlowers[first], GardenFlowers[(first + 1) & 3], Smoothstep(0.4f, 0.6f, which - first));
            grass.Cover = Lerp(grass.Cover, Saturate((0.82f + 0.18f * small) * normalY), garden);
            grass.Height = Lerp(grass.Height, GardenHeight * (0.85f + 0.3f * drift), garden);
            grass.Dryness = Lerp(grass.Dryness, 0f, garden);
            grass.Colour = Vector3.Lerp(grass.Colour, GardenGrass * (0.92f + 0.16f * drift), garden);
            grass.Plumes = Lerp(grass.Plumes, 0f, garden);
            grass.Bloom = Lerp(grass.Bloom, GardenBloom * Smoothstep(0.3f, 0.62f, drift), garden);
            grass.BloomColour = Vector3.Lerp(grass.BloomColour, flower, garden);
            // Its soil drains, so water never stands in it (HollowAt says the same).
            grass.Hollow = Lerp(grass.Hollow, 0f, garden);
        }

        // 1 under a hall and for a little way round its walls, fading to nothing a few metres out.
        private float HallGround(Vector2 position)
        {
            float most = 0f;
            foreach (WorldHall hall in _world.Halls)
            {
                Vector2 local = hall.ToLocal(position);
                float dx = MathF.Max(MathF.Abs(local.X) - hall.Design.Half.X, 0f);
                float dz = MathF.Max(MathF.Abs(local.Y) - hall.Design.Half.Y, 0f);
                float outside = MathF.Sqrt(dx * dx + dz * dz);
                most = MathF.Max(most, 1f - Smoothstep(1.4f, 3.2f, outside));
            }
            return most;
        }

        /// <summary>How soon water stands at a point of the ground, 0..1, the last places to fill being near 0. It's what the terrain is painted with, for anything that has to agree with it.</summary>
        public float HollowAt(Vector2 position)
        {
            float x = position.X, z = position.Y;
            float height = _world.Height.Sample(position);
            float slope = _world.Height.SlopeAt(position);
            float trodden = MathF.Max(MathF.Max(_world.PavedRoad.Sample(x, z), _world.TrailRoad.Sample(x, z)), MathF.Max(MathF.Max(_world.Yard.Sample(x, z), _world.Plaza.Sample(x, z)), _world.Farmland.Sample(x, z)));
            if (_world.Paved) trodden = MathF.Max(trodden, _world.Paving.Sample(x, z));
            if (_world.Mudded) trodden = MathF.Max(trodden, _world.Mud.Sample(x, z));
            float drinks = (_world.Sanded ? _world.Sand.Sample(x, z) : 0f) + (_world.Ashen ? _world.Ash.Sample(x, z) : 0f);
            float shore = 1f - Smoothstep(0.3f, 1.8f, height - _world.WaterLevelAt(position));
            return Hollow(position, height, slope, trodden, shore) * (1f - _world.Garden.Sample(x, z)) * (1f - Saturate(drinks));
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

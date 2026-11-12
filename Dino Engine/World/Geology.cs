using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.World.WorldMath;

namespace Dino_Engine.World
{
    /// <summary>
    /// What a world's bedrock is. Rock isn't noise: the shape of a cliff is the shape of the fractures in it, and those come in a few families
    /// (Paris et al., "Modeling Rocky Scenery using Implicit Blocks"), each named here by a rock that breaks that way.
    /// </summary>
    public enum RockKind
    {
        Granite,    // Equidimensional: three sets of joints near square to each other, weathered round. Domes, tors and big blocks.
        Limestone,  // Tabular: one set of bedding planes, thin and many, with ledges of grass between them.
        Sandstone,  // Tabular, thick: cliffs in tall bands of colour.
        Schist,     // Rhombohedral: oblique sets, tilted hard over, so that the rock comes up in leaning ribs and blades.
        Basalt,     // Columnar: many sets of long parallel fractures, the rock standing in polygons.
    }

    /// <summary>
    /// The rock of one world, and the only description of it: the height map is sculpted from this, the terrain and every stone are
    /// coloured by it, and stones are broken along its fractures. That's what makes a boulder look as if it came off the cliff behind it.
    /// Nothing here touches the GPU. <c>Shaders/Geology.glsl</c> copies <see cref="LayerAt"/> and <see cref="BedAt"/>, and <c>Shaders/Rock.glsl</c> draws the joints from
    /// <see cref="Joint"/>: change them together.
    /// </summary>
    public sealed class Geology
    {
        /// <summary>The lowest layer of rock, in metres.</summary>
        public const float Bottom = -64f;
        /// <summary>The highest. Nothing is built that tall, and a layer above it reads as the top one.</summary>
        public const float Top = 448f;
        /// <summary>Entries in the tables of layers, a quarter of a metre each.</summary>
        public const int Steps = 2048;
        /// <summary>The most entries a bed can be counted over in <see cref="Beds"/>, as its counts are bytes: 63.75 m, well over the thickest bed.</summary>
        public const int LongestCount = 255;

        private const float Step = (Top - Bottom) / Steps;

        public readonly RockKind Kind;
        /// <summary>How far the layers climb for each metre along x and z.</summary>
        public readonly Vector2 Dip;
        /// <summary>Which way the main joints run, in radians: square to the way the beds dip, as strike is. Ridges and blocks line up with it.</summary>
        public readonly float Strike;
        /// <summary>Where this world's layers wander, for <see cref="LayerAt"/>.</summary>
        public readonly Vector3 Phase;
        /// <summary>Metres across a joint block, the first set and the second.</summary>
        public readonly Vector2 BlockSize;
        /// <summary>How much of the shape the joints get: 1 is blocks that stand apart, 0 is none at all.</summary>
        public readonly float Blockiness;
        /// <summary>How hard a face is pulled round to lie along a joint, 0..1. It's what makes a mountainside show the same few planes over and over.</summary>
        public readonly float Planing;
        /// <summary>The planes this rock breaks along, as unit normals: its beds, then its sets of joints. A stone is cut by these as a cliff is.</summary>
        public readonly Vector3[] Joints;
        /// <summary>How much of a slope's height goes into ledges and benches.</summary>
        public readonly float Bedding;
        /// <summary>How round its edges have weathered, 0..1.</summary>
        public readonly float Rounding;
        /// <summary>How hard the gullies cut into it, 0..1.</summary>
        public readonly float Gullies;
        /// <summary>The rock's colour, an albedo.</summary>
        public readonly Vector3 Colour;
        /// <summary>How the joints of a bed are drawn on bare rock (Rock.glsl), set by the kind of rock.</summary>
        public readonly JointStyle Joint;
        /// <summary>What grows on bare rock, an albedo, and how much of it does: moss where the land is wet, a grey crust of lichen where it isn't.</summary>
        public Vector3 Moss { get; private set; } = new Vector3(0.2f, 0.21f, 0.16f);
        public float MossCover { get; private set; }
        /// <summary>How much of whatever bare rock faces the sky holds snow, by how much of the land lies under it.</summary>
        public float SnowCover { get; private set; }

        /// <summary>Per entry: how far a surface at this layer sits from where it would have been, in metres. Hard layers stand up, soft ones lie back.</summary>
        private readonly float[] _shift = new float[Steps];

        /// <summary>Per entry, red, green, blue and hardness, 0..255. It's what goes to the GPU, and what colours a stone.</summary>
        public readonly byte[] Layers = new byte[Steps * 4];

        /// <summary>
        /// Per entry, two bytes: how many entries its bed has run below it, and how many it has still to run above it. Every entry of a bed shares the entry its bed starts at, which is
        /// what tells one bed from the next and staggers their joints, and the counts say how far it is to the partings above and below. It goes to the GPU after <see cref="Layers"/>.
        /// </summary>
        public readonly byte[] Beds = new byte[Steps * 2];

        public Geology(RockKind kind, WorldRandom random)
        {
            Kind = kind;
            Phase = new Vector3(random.Float(), random.Float(), random.Float()) * MathF.Tau;
            float dipAngle = random.Float() * MathF.Tau;
            Vector2 dipDirection = new Vector2(MathF.Cos(dipAngle), MathF.Sin(dipAngle));
            // Strike is the way a bed runs level, which is square to the way it dips.
            Strike = (dipAngle + MathF.PI * 0.5f) % MathF.PI;

            switch (kind)
            {
                case RockKind.Granite:
                    Dip = dipDirection * random.Range(0f, 0.05f);
                    BlockSize = new Vector2(random.Range(17f, 26f), random.Range(7f, 10f));
                    Blockiness = 0.85f;
                    Planing = 0.45f;
                    Bedding = 0.28f;
                    Rounding = 0.8f;
                    Gullies = 0.7f;
                    Colour = new Vector3(0.3f, 0.29f, 0.28f);
                    break;
                case RockKind.Limestone:
                    Dip = dipDirection * random.Range(0.02f, 0.1f);
                    BlockSize = new Vector2(random.Range(13f, 20f), random.Range(5f, 8f));
                    Blockiness = 0.5f;
                    Planing = 0.7f;
                    Bedding = 1f;
                    Rounding = 0.3f;
                    Gullies = 0.8f;
                    Colour = new Vector3(0.38f, 0.36f, 0.32f);
                    break;
                case RockKind.Sandstone:
                    Dip = dipDirection * random.Range(0.01f, 0.06f);
                    BlockSize = new Vector2(random.Range(18f, 30f), random.Range(7f, 11f));
                    Blockiness = 0.6f;
                    Planing = 0.6f;
                    Bedding = 1f;
                    Rounding = 0.45f;
                    Gullies = 1f;
                    Colour = new Vector3(0.35f, 0.2f, 0.115f);
                    break;
                case RockKind.Schist:
                    Dip = dipDirection * random.Range(0.45f, 0.95f);
                    BlockSize = new Vector2(random.Range(11f, 16f), random.Range(4.5f, 7f));
                    Blockiness = 0.7f;
                    Planing = 0.85f;
                    Bedding = 0.8f;
                    Rounding = 0.12f;
                    Gullies = 0.9f;
                    Colour = new Vector3(0.25f, 0.255f, 0.25f);
                    break;
                default:
                    Dip = dipDirection * random.Range(0f, 0.02f);
                    BlockSize = new Vector2(random.Range(6f, 8f), random.Range(3.5f, 4.5f));
                    Blockiness = 1f;
                    Planing = 0.9f;
                    Bedding = 0.6f;
                    Rounding = 0.06f;
                    Gullies = 0.55f;
                    // Darker than any other rock, but no darker than reads under an ash sky from this camera, where a column has to show its edges.
                    Colour = new Vector3(0.215f, 0.22f, 0.24f);
                    break;
            }

            Joint = JointStyle.Of(kind);
            Joints = BuildJoints(random);
            BuildLayers(random);
        }

        // Three sets near square to each other, as cooling and unloading crack most rock, and leaning well over in rock that's been folded.
        private Vector3[] BuildJoints(WorldRandom random)
        {
            Vector3 bed = Vector3.Normalize(new Vector3(-Dip.X, 1f, -Dip.Y));
            Vector3 along = new Vector3(MathF.Cos(Strike), 0f, MathF.Sin(Strike));
            Vector3 across = Vector3.Normalize(Vector3.Cross(bed, along));
            along = Vector3.Normalize(Vector3.Cross(across, bed));

            float lean = Kind == RockKind.Schist ? random.Range(0.45f, 0.7f) : random.Range(0f, 0.16f);
            Vector3 first = Vector3.Normalize(along + bed * lean);
            Vector3 second = Vector3.Normalize(across - bed * lean * 0.7f);
            if (Kind != RockKind.Basalt) return new[] { bed, first, second };

            // Columns: a flat top, and sides at sixty degrees to each other.
            Vector3 third = Vector3.Normalize(along * 0.5f + across * 0.866f);
            Vector3 fourth = Vector3.Normalize(along * 0.5f - across * 0.866f);
            return new[] { bed, along, third, fourth };
        }

        /// <summary>The rock of a world, from what its biomes stand on, with what grows on it from how wet they are.</summary>
        public static Geology Draw(WorldPlan plan, WorldRandom random)
        {
            float pick = random.Float();
            RockKind kind = plan.Biomes[^1].Bedrock;
            for (int i = 0; i < plan.Biomes.Count; i++)
            {
                pick -= plan.BiomeShares[i];
                if (pick > 0f) continue;
                kind = plan.Biomes[i].Bedrock;
                break;
            }
            var geology = new Geology(plan.Archetype.Bedrock ?? kind, random);
            geology.Overgrow(plan);
            return geology;
        }

        // Moss is a deep green on wet rock, and on dry rock lichen is a pale grey-green crust: a land's is what its biomes make between them.
        private void Overgrow(WorldPlan plan)
        {
            Vector3 lichen = new Vector3(0.3f, 0.3f, 0.24f);
            Vector3 moss = new Vector3(0.085f, 0.13f, 0.04f);
            Vector3 colour = Vector3.Zero;
            float cover = 0f;
            float snow = 0f;
            float total = 0f;
            for (int i = 0; i < plan.Biomes.Count; i++)
            {
                BiomeDefinition biome = plan.Biomes[i];
                float share = plan.BiomeShares[i];
                float wet = Saturate(biome.Moisture);
                colour += Vector3.Lerp(lichen, moss, Smoothstep(0.3f, 0.8f, wet)) * share;
                // Snow and sand keep their rock bare, and wet woods cover it.
                float bare = biome.Ground is SurfaceMaterial.Snow or SurfaceMaterial.Ice or SurfaceMaterial.Dunes or SurfaceMaterial.Sand or SurfaceMaterial.Ash ? 0.25f : 1f;
                cover += Lerp(0.18f, 0.85f, Smoothstep(0.15f, 0.85f, wet)) * bare * share;
                if (biome.Ground is SurfaceMaterial.Snow or SurfaceMaterial.Ice) snow += share;
                total += share;
            }
            if (total <= 0f) return;
            Moss = colour / total;
            MossCover = Saturate(cover / total);
            // Snow lies on the heights before it lies anywhere else, so a land with any snowfields at all has it on its rock.
            SnowCover = Saturate(snow / total * 2.2f);
        }

        /// <summary>
        /// Which layer of rock a point is in, as the height it was laid down at. Beds tilt with <see cref="Dip"/> and wander a little,
        /// so that a ledge doesn't run dead level like a contour line, which is what makes terracing look like a stair.
        /// </summary>
        public float LayerAt(float x, float y, float z)
        {
            float wander = 2.6f * MathF.Sin(x * 0.0193f + 1.7f * MathF.Sin(z * 0.0131f + Phase.X))
                + 1.7f * MathF.Sin(z * 0.0347f + 1.3f * MathF.Sin(x * 0.0171f + Phase.Y))
                + 0.6f * MathF.Sin((x + z) * 0.0871f + Phase.Z);
            return y + x * Dip.X + z * Dip.Y + wander;
        }

        /// <summary>
        /// Which bed a layer falls in: metres up from its foot and down to its head, along the layers, and the entry its foot is in, which is the same for all of it.
        /// <c>geologyBedAt</c> in Geology.glsl is the same function.
        /// </summary>
        public void BedAt(float layer, out float since, out float until, out int bed)
        {
            float place = PlaceOf(layer);
            int i = Math.Clamp((int)MathF.Floor(place), 0, Steps - 1);
            float within = Math.Clamp(place - i, 0f, 1f);
            int below = Beds[i * 2];
            int above = Beds[i * 2 + 1];
            since = (below + within) * Step;
            until = (above + 1f - within) * Step;
            bed = i - below;
        }

        /// <summary>
        /// Where in the table a layer is, in entries. The stack comes round again past either end, as beds that dip steeply reach hundreds of metres past it across a land, and
        /// they'd otherwise all be the bottom bed there. <c>geologyPlace</c> in Geology.glsl is the same.
        /// </summary>
        public static float PlaceOf(float layer)
        {
            float place = (layer - Bottom) / Step;
            return place - Steps * MathF.Floor(place / Steps);
        }

        /// <summary>How far hard and soft rock have moved a surface in this layer from a smooth slope, in metres. Positive stands proud.</summary>
        public float ShiftAt(float layer)
        {
            float place = Math.Clamp((layer - Bottom) / Step, 0f, Steps - 1.001f);
            int i = (int)place;
            return Lerp(_shift[i], _shift[i + 1], place - i);
        }

        /// <summary>The colour of the rock in this layer, an albedo, with its hardness in w.</summary>
        public Vector4 ToneAt(float layer)
        {
            int i = Math.Clamp((int)PlaceOf(layer), 0, Steps - 1) * 4;
            return new Vector4(Layers[i] / 255f, Layers[i + 1] / 255f, Layers[i + 2] / 255f, Layers[i + 3] / 255f);
        }

        // A stack of beds, laid down from the bottom. Each is a short soft band under a tall hard one: the hard band stands as a cliff
        // and the soft one wears back to a bench. A surface keeps its height at every boundary between beds, so the land stays where it was.
        private void BuildLayers(WorldRandom random)
        {
            (FloatRange hard, FloatRange soft, float tint) = Kind switch
            {
                RockKind.Granite => (new FloatRange(7f, 16f), new FloatRange(0.6f, 1.2f), 0.05f),
                RockKind.Limestone => (new FloatRange(2.4f, 7f), new FloatRange(0.7f, 1.8f), 0.12f),
                RockKind.Sandstone => (new FloatRange(5f, 15f), new FloatRange(0.9f, 2.4f), 0.3f),
                RockKind.Schist => (new FloatRange(0.9f, 2.6f), new FloatRange(0.3f, 0.8f), 0.14f),
                _ => (new FloatRange(6f, 13f), new FloatRange(0.5f, 1.3f), 0.07f),
            };

            // Each entry belongs to the bed its start lies in, so a bed's entries are those from the first whose start is at or over its foot.
            int[] owner = new int[Steps];
            var beds = new List<(float foot, float height, float fall)>();
            float bed = Bottom;
            float drift = 0f;
            int entry = 0;
            while (bed < Top)
            {
                float rise = random.Range(hard);
                float fall = random.Range(soft);
                float height = rise + fall;
                // A slow walk through the colours, with a jump now and then: a change of sea, a run of ash.
                drift = Math.Clamp(drift * 0.8f + random.Gaussian() * 0.5f, -1.4f, 1.4f);
                if (random.Chance(0.14f)) drift = random.Gaussian() * 1.4f;
                Vector3 band = Banded(drift * tint, random);
                Vector3 parting = band * random.Range(0.45f, 0.62f);

                int index = beds.Count;
                beds.Add((bed, height, fall));
                for (; entry < Steps && Bottom + entry * Step < bed + height; entry++)
                {
                    owner[entry] = index;
                    float at = (Bottom + entry * Step - bed) / height;
                    float bench = fall / height;
                    // In a metre of height the bench is crossed in a long stride and the cliff in a short one.
                    float climbed = Smoothstep(bench * 0.7f, bench + (1f - bench) * 0.42f, at);
                    _shift[entry] = (climbed - at) * height;

                    // Thin partings show as dark lines in a pale cliff, and every band has finer ones within it.
                    float within = 1f + 0.05f * Fine(entry, Phase.X);
                    bool softHere = at < bench;
                    Vector3 colour = (softHere ? parting : band) * within;
                    Paint(entry, colour, softHere ? 0.25f : 0.9f);
                }
                bed += height;
            }

            // How far each entry is from the ends of its bed, counted in entries.
            int first = 0;
            for (int i = 0; i <= Steps; i++)
            {
                if (i < Steps && owner[i] == owner[first]) continue;
                int last = i - 1;
                for (int j = first; j <= last; j++)
                {
                    Beds[j * 2] = (byte)Math.Min(j - first, LongestCount);
                    Beds[j * 2 + 1] = (byte)Math.Min(last - j, LongestCount);
                }
                first = i;
            }
        }

        private Vector3 Banded(float amount, WorldRandom random)
        {
            // Colour moves along what iron and carbon do to rock: warmer and redder one way, greyer and darker the other.
            Vector3 warm = new Vector3(1.32f, 0.98f, 0.74f);
            Vector3 cool = new Vector3(0.74f, 0.8f, 0.86f);
            Vector3 towards = amount > 0f ? warm : cool;
            float light = 1f + random.Gaussian() * 0.1f * MathF.Min(MathF.Abs(amount) * 4f + 0.3f, 1f);
            return Vector3.Lerp(Colour, Colour * towards, MathF.Min(MathF.Abs(amount), 1f)) * light;
        }

        private static float Fine(int i, float phase)
        {
            return MathF.Sin(i * 0.71f + phase) * 0.5f + MathF.Sin(i * 1.93f + phase * 2f) * 0.3f + MathF.Sin(i * 0.23f - phase) * 0.2f;
        }

        private void Paint(int i, Vector3 colour, float hardness)
        {
            Layers[i * 4] = (byte)Math.Clamp(colour.X * 255f, 0f, 255f);
            Layers[i * 4 + 1] = (byte)Math.Clamp(colour.Y * 255f, 0f, 255f);
            Layers[i * 4 + 2] = (byte)Math.Clamp(colour.Z * 255f, 0f, 255f);
            Layers[i * 4 + 3] = (byte)Math.Clamp(hardness * 255f, 0f, 255f);
        }
    }

    /// <summary>
    /// How a kind of rock is jointed, as bare rock shows it (Rock.glsl): the joints of a bed are spaced by how thick the bed is, as real joints are, one set running unbroken along
    /// the strike and the other crossing between them, staggered, the way joints end against each other. Columns are their own case, a net of hexagons.
    /// </summary>
    public readonly record struct JointStyle(
        float Along,        // Metres between the joints that cross the strike, for each metre the bed is thick.
        float Across,       // And between those that run along it.
        float Least,        // The closest any joints come, in metres,
        float Most,         // and the farthest.
        float Gap,          // Metres a joint stands open at the surface.
        float Bevel,        // Metres over which a block's edge is rounded into its joint.
        float Tilt,         // How far a block's face is tipped from the rock's own, and pulled to lie along a joint.
        float Recess,       // How deeply a soft bed is shadowed under the hard one over it.
        float CrossBedding, // How strongly the laminae inside a bed lean, as sand laid by wind and water does.
        float Foliation,    // How strongly a bed is striped by the fine sheets it's made of.
        float Varnish,      // How dark the streaks are that water and desert leave down a face.
        float Karst)        // How much water has widened the joints on top of the rock.
    {
        public static JointStyle Of(RockKind kind) => kind switch
        {
            // From the game's camera a metre is thirty pixels, so a joint has to stand open a hand's width, weathered and in its own shadow, to be seen as one at all.
            RockKind.Granite => new JointStyle(0.36f, 0.3f, 1.8f, 6f, 0.13f, 0.5f, 0.14f, 0.25f, 0f, 0f, 0.3f, 0f),
            RockKind.Limestone => new JointStyle(0.7f, 0.5f, 1.05f, 4.6f, 0.11f, 0.12f, 0.2f, 0.6f, 0f, 0.15f, 0.2f, 1f),
            RockKind.Sandstone => new JointStyle(0.45f, 0.32f, 1.6f, 6.5f, 0.12f, 0.25f, 0.14f, 0.5f, 1f, 0f, 0.85f, 0f),
            RockKind.Schist => new JointStyle(0.9f, 0.65f, 0.8f, 3.2f, 0.07f, 0.05f, 0.26f, 0.35f, 0f, 1f, 0.25f, 0f),
            _ => new JointStyle(0.12f, 0.12f, 0.8f, 1.6f, 0.06f, 0.06f, 0.1f, 0.45f, 0f, 0f, 0.1f, 0f),
        };
    }
}

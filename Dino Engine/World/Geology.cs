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
    /// Nothing here touches the GPU. <c>Shaders/Geology.glsl</c> copies <see cref="LayerAt"/>: change them together.
    /// </summary>
    public sealed class Geology
    {
        /// <summary>The lowest layer of rock, in metres.</summary>
        public const float Bottom = -64f;
        /// <summary>The highest. Nothing is built that tall, and a layer above it reads as the top one.</summary>
        public const float Top = 448f;
        /// <summary>Entries in the tables of layers, a quarter of a metre each.</summary>
        public const int Steps = 2048;

        private const float Step = (Top - Bottom) / Steps;

        public readonly RockKind Kind;
        /// <summary>How far the layers climb for each metre along x and z.</summary>
        public readonly Vector2 Dip;
        /// <summary>Which way the main joints run, in radians. Ridges and blocks line up with it.</summary>
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

        /// <summary>Per entry: how far a surface at this layer sits from where it would have been, in metres. Hard layers stand up, soft ones lie back.</summary>
        private readonly float[] _shift = new float[Steps];

        /// <summary>Per entry, red, green, blue and hardness, 0..255. It's what goes to the GPU, and what colours a stone.</summary>
        public readonly byte[] Layers = new byte[Steps * 4];

        public Geology(RockKind kind, WorldRandom random)
        {
            Kind = kind;
            Strike = random.Float() * MathF.PI;
            Phase = new Vector3(random.Float(), random.Float(), random.Float()) * MathF.Tau;
            float dipAngle = random.Float() * MathF.Tau;
            Vector2 dipDirection = new Vector2(MathF.Cos(dipAngle), MathF.Sin(dipAngle));

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
                    Colour = new Vector3(0.36f, 0.34f, 0.3f);
                    break;
                case RockKind.Sandstone:
                    Dip = dipDirection * random.Range(0.01f, 0.06f);
                    BlockSize = new Vector2(random.Range(18f, 30f), random.Range(7f, 11f));
                    Blockiness = 0.6f;
                    Planing = 0.6f;
                    Bedding = 1f;
                    Rounding = 0.45f;
                    Gullies = 1f;
                    Colour = new Vector3(0.33f, 0.19f, 0.11f);
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
                    Colour = new Vector3(0.17f, 0.175f, 0.195f);
                    break;
            }

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

        /// <summary>The rock of a world, from what its biomes stand on.</summary>
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
            return new Geology(plan.Archetype.Bedrock ?? kind, random);
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
            int i = Math.Clamp((int)((layer - Bottom) / Step), 0, Steps - 1) * 4;
            return new Vector4(Layers[i] / 255f, Layers[i + 1] / 255f, Layers[i + 2] / 255f, Layers[i + 3] / 255f);
        }

        // A stack of beds, laid down from the bottom. Each is a tall hard band over a short soft one: the hard band stands as a cliff
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

            float bed = Bottom;
            float drift = 0f;
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

                int first = Math.Max((int)((bed - Bottom) / Step), 0);
                int last = Math.Min((int)((bed + height - Bottom) / Step) + 1, Steps);
                for (int i = first; i < last; i++)
                {
                    float at = (Bottom + i * Step - bed) / height;
                    float bench = fall / height;
                    // In a metre of height the bench is crossed in a long stride and the cliff in a short one.
                    float climbed = Smoothstep(bench * 0.7f, bench + (1f - bench) * 0.42f, at);
                    _shift[i] = (climbed - at) * height;

                    // Thin partings show as dark lines in a pale cliff, and every band has finer ones within it.
                    float within = 1f + 0.05f * Fine(i, Phase.X);
                    bool soft1 = at < bench;
                    Vector3 colour = (soft1 ? parting : band) * within;
                    Paint(i, colour, soft1 ? 0.25f : 0.9f);
                }
                bed += height;
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
}

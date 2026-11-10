using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Waygates
{
    /// <summary>
    /// What's seen in an open way, all of it grown from the land it goes to and a seed: a throat of light bored into the gate, streaming down to that land, which shows at the end of it under its
    /// own sky. The throat is the colours of the land's biomes; the far end is its sky and its sun at the hour it's always at, its ridges in the colours of its ground, the sea if it stands in one, and
    /// snow if it's cold; what drifts out of the way is what drifts in the air there. How the throat moves comes from <see cref="Genes"/>, each number by a name of its own, so no two ways turn alike,
    /// and the higher a land's level the less settled its way. Colours are hues with their brightest channel 1: Way/Way.frag says how bright each part is. Headless.
    /// </summary>
    public sealed class WayLook
    {
        public Vector3 Rim;         // The brightest: the way's edge and the streams in its throat.
        public Vector3 Accent;      // Some of the streams.
        public Vector3 Deep;        // The throat's walls between them.
        public Vector3 Core;        // The light round the far end.
        public Vector3 SkyHigh;     // The far land's sky overhead,
        public Vector3 SkyLow;      // at its horizon,
        public Vector3 Sun;         // its sun,
        public Vector3 Haze;        // and the air its far ridges fade into.
        public Vector3 Near, Middle, Far;   // Its ridges, from nearest.
        public float SunHeight;     // Where its sun stands at the far end, from -1 under the horizon to 1 high.
        public float SunAcross;     // and across, -1 to 1.
        public float Sea;           // 1 where the land stands in the sea, a little for a lake, 0 for none.
        public float Rugged;        // 0 for rolling hills, 1 for peaks.
        public float Height;        // How high its land stands.
        public float Terraces;      // 0 to 1, how much it steps like mesas.
        public float Snow;          // 0 to 1, how white its tops are.

        public int Arms;            // Streams spiralling down the throat.
        public float Twist;         // How far they turn on the way down, radians for each unit of the throat's depth.
        public float Spin;          // Turns a second the throat goes round, either way.
        public float Flow;          // How fast the streams pour down it.
        public float Sharpness;     // How fine they are.
        public float Mist;          // How much glowing mist fills it.
        public float Unrest;        // How much the throat churns: more the more dangerous the land.
        public float Breath;        // Seconds of the way's slow breathing, which its lights keep to.
        public float BreathPhase;
        public float Seed;          // For the shader's own noise, 0 to 1000.

        public AmbientMotes Motes;  // What comes through.
        public Vector3 MoteColour;

        /// <summary>The colour the way lights what's around it in: its own, softened, so that it tints what it falls on rather than painting it.</summary>
        public Vector3 Light => Brightest(Vector3.Lerp(Brightest(Vector3.Lerp(Rim, Core, 0.3f)), Vector3.One, 0.35f));

        /// <summary>How bright the way is at <paramref name="age"/> seconds, from its breathing alone: 1 on average. Way/Way.frag breathes the same.</summary>
        public float Breathing(float age) => 1f + 0.12f * MathF.Sin(age * MathF.Tau / Breath + BreathPhase);

        /// <summary>The look of a way to <paramref name="land"/>, with <paramref name="seed"/> for how it moves.</summary>
        public static WayLook Of(WorldPlan land, ulong seed)
        {
            var genes = new Genes(seed);
            IReadOnlyList<BiomeDefinition> biomes = land.Biomes;
            BiomeDefinition first = biomes.Count > 0 ? biomes[0] : BiomeCatalog.Meadow;
            BiomeDefinition second = biomes.Count > 1 ? biomes[1] : first;
            BiomeDefinition third = biomes.Count > 2 ? biomes[2] : second;
            WorldMood mood = land.Mood;
            WorldArchetype kind = land.Archetype;

            var look = new WayLook();

            // The throat: what the land is known for, made vivid enough to keep its hue however bright it goes, and its sky between the streams.
            look.Rim = Vivid(first.MapColour, 2.4f);
            look.Accent = Vivid(Vector3.Lerp(second.MapColour, first.MapColour, 0.25f), 2.2f);
            look.Deep = Vivid(Vector3.Lerp(first.MapColour, mood.SkyColour, 0.55f), 1.9f);
            look.Core = Brightest(Vector3.Lerp(Vivid(mood.SunColour, 1.4f), look.Rim, 0.35f));

            // The far end: the land at the hour it's always seen at.
            Vector3 towardsSun = -mood.SunDirection;
            look.SkyHigh = Brightest(mood.SkyColour);
            look.SkyLow = Brightest(Vector3.Lerp(mood.SkyColour, mood.SunColour, 0.55f + 0.3f * (1f - Math.Clamp(towardsSun.Y * 1.6f, 0f, 1f))));
            look.Sun = Brightest(mood.SunColour);
            look.Haze = Brightest(Vector3.Lerp(mood.FogColour, look.SkyLow, 0.5f));
            look.SunHeight = Math.Clamp(towardsSun.Y * 1.5f - 0.15f, -0.2f, 0.9f);
            look.SunAcross = genes.Range("sun across", -0.55f, 0.55f);
            look.Near = Earth(first);
            look.Middle = Earth(second);
            look.Far = Earth(third);
            look.Sea = kind.Landform is LandformKind.Island or LandformKind.Archipelago or LandformKind.Peninsula ? 1f : kind.StillWater ? 0.35f : 0f;
            look.Rugged = Math.Clamp(land.MountainCoverage * 1.6f + land.MountainHeight / 140f + (kind.RimHeight > 0f ? 0.2f : 0f), 0f, 1f);
            look.Height = Math.Clamp(0.45f + land.HillHeight / 60f + land.MountainHeight / 220f, 0.4f, 1f);
            look.Terraces = Math.Clamp(land.Terraces * 1.4f, 0f, 1f);
            look.Snow = Math.Clamp((0.3f - biomes.Min(b => b.Temperature)) / 0.25f, 0f, 1f);

            // How it moves.
            look.Arms = genes.Count("arms", 3, 6);
            look.Twist = genes.Range("twist", 0.5f, 1.4f) * genes.Sign("twist way");
            look.Spin = genes.Range("spin", 0.03f, 0.08f) * genes.Sign("spin way");
            look.Flow = genes.Range("flow", 0.16f, 0.32f);
            look.Sharpness = genes.Range("sharpness", 2.4f, 4.2f);
            look.Mist = genes.Range("mist", 0.4f, 0.85f);
            look.Unrest = Math.Clamp((kind.Peaceful ? 0.08f : 0.22f + land.Level / 110f) + genes.Range("unrest", 0f, 0.12f), 0f, 0.75f);
            look.Breath = genes.Range("breath", 5.5f, 8f);
            look.BreathPhase = genes.Range("breath phase", 0f, MathF.Tau);
            look.Seed = genes.Range("noise", 0f, 1000f);

            look.Motes = first.Motes;
            look.MoteColour = first.MoteColour ?? MoteColourOf(first);
            return look;
        }

        /// <summary>How far from white the brightest colour of a way has to be, its least channel at most 1 minus this: light that's near white goes white once it's bright.</summary>
        private const float LeastChroma = 0.5f;
        private static readonly Vector3 NoHue = new Vector3(0.42f, 0.76f, 1f);     // What a grey becomes: the cold light of the old roads.

        // A soft colour pushed away from grey, so that it keeps its hue however bright it's made, with its brightest channel 1. A pale one, a snowfield's, is pushed out until it's a
        // colour at all, the way it already leans: snow's faint blue becomes ice.
        private static Vector3 Vivid(Vector3 colour, float saturation)
        {
            float grey = (colour.X + colour.Y + colour.Z) / 3f;
            Vector3 c = Brightest(Vector3.ComponentMax(new Vector3(grey) + (colour - new Vector3(grey)) * saturation, new Vector3(0.02f)));
            float chroma = 1f - MathF.Min(c.X, MathF.Min(c.Y, c.Z));
            if (chroma < 0.04f) return NoHue;
            if (chroma < LeastChroma) c = Vector3.One - (Vector3.One - c) * (LeastChroma / chroma);
            return Warm(c);
        }

        // A deep red glows scarlet: light that red reads as blood, and blood-red is the Reich's (LORE.md). Red stays red; it only gets enough orange in it to be fire and leaves.
        private static Vector3 Warm(Vector3 c)
        {
            if (c.X < 0.999f || c.Y >= 0.34f) return c;
            return new Vector3(c.X, MathF.Max(c.Y, 0.34f), MathF.Min(c.Z, c.Y + 0.1f));
        }

        private static Vector3 Brightest(Vector3 colour)
        {
            float most = MathF.Max(colour.X, MathF.Max(colour.Y, colour.Z));
            return colour / MathF.Max(most, 1e-4f);
        }

        // The colour of a land's ground seen from far off: its map colour, a little vivid, and the dark of a forest where it's wooded.
        private static Vector3 Earth(BiomeDefinition biome) => Vivid(biome.MapColour, 1.3f) * (1f - 0.35f * biome.ForestCover);

        private static Vector3 MoteColourOf(BiomeDefinition biome) => biome.Motes switch
        {
            AmbientMotes.Petals => Brightest(Vector3.Lerp(biome.GrassBloomColour, new Vector3(1f, 0.72f, 0.82f), 0.6f)),
            AmbientMotes.Snow => new Vector3(0.95f, 0.97f, 1f),
            AmbientMotes.Embers => new Vector3(1f, 0.55f, 0.2f),
            AmbientMotes.Fireflies => new Vector3(0.75f, 1f, 0.35f),
            AmbientMotes.Dust => new Vector3(0.85f, 0.72f, 0.5f),
            AmbientMotes.Wisps => new Vector3(0.6f, 1f, 0.85f),
            AmbientMotes.Pollen => new Vector3(1f, 0.92f, 0.55f),
            AmbientMotes.FallingLeaves => Brightest(biome.MapColour),
            _ => Vector3.One,
        };
    }
}

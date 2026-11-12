using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.World.WorldMath;

namespace Dino_Engine.World
{
    /// <summary>
    /// The rock in the land people walk on, which isn't mountains: bedrock showing through where the soil is thin, as a pavement of its blocks, and the low knolls it stands up in, all
    /// as rocky as the biomes there are (<see cref="BiomeDefinition.BoulderDensity"/>), none of it in a hollow where soil gathers, and none of it anywhere near what's built or laid out,
    /// so a road, a field or a village never has a rock in it. It never makes ground anyone could walk on steeper than they can walk, and never stands up enough to hide anyone.
    /// The height map takes the knolls when it's baked (TerrainBakeStage), and the ground is painted with the pavement as it's drawn (TerrainSurface), from this one description.
    /// Headless, and safe from any thread once it's built.
    /// </summary>
    public sealed class GroundRock
    {
        private const float Room = 7f;              // Metres from anything built or laid out before rock begins to show,
        private const float FullRoom = 16f;         // and beyond which it shows as much as the land has.
        private const float KnollHeight = 1.4f;     // The tallest a knoll stands out of the land round it, in metres.
        private const float Proud = 0.18f;          // How far a pavement's blocks stand up out of the soil round them.

        private readonly WorldData _world;
        private readonly WorldNoise _noise;
        private readonly ScalarField _room;
        private readonly float[] _rockiness;

        public GroundRock(WorldData world, WorldRandom random)
        {
            _world = world;
            _noise = new WorldNoise(random.Fork("ground rock"));
            // A world of one place is laid out to the metre, the Haven round its square and its giants' grazing, an arena round its floor: its land has no rock of its own.
            _rockiness = world.Plan.Biomes.Select(biome => world.Plan.Archetype.Enclosed ? 0f : Saturate(biome.BoulderDensity * 1.25f)).ToArray();
            _room = RoomOf(world);
        }

        // Metres to the nearest thing anyone built or laid out: a road, a site, a pack's haunt, a hall, a pad, a field or a yard, or the water.
        private static ScalarField RoomOf(WorldData world)
        {
            const float Cell = WorldData.CoarseCell;
            ScalarField grid = ScalarField.ForWorld(world.Size, Cell);
            int width = grid.Width;
            int depth = grid.Depth;
            bool[] built = new bool[width * depth];

            void Mark(Vector2 centre, float radius)
            {
                int minX = Math.Max((int)MathF.Floor((centre.X - radius) / Cell), 0), maxX = Math.Min((int)MathF.Ceiling((centre.X + radius) / Cell), width - 1);
                int minZ = Math.Max((int)MathF.Floor((centre.Y - radius) / Cell), 0), maxZ = Math.Min((int)MathF.Ceiling((centre.Y + radius) / Cell), depth - 1);
                for (int z = minZ; z <= maxZ; z++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        if ((grid.WorldPosition(x, z) - centre).LengthSquared <= radius * radius) built[z * width + x] = true;
                    }
                }
            }

            foreach (WorldRoad road in world.Roads)
            {
                float reach = road.HalfWidth + road.Shoulder + Cell;
                for (int i = 1; i < road.Points.Count; i++)
                {
                    Vector2 from = road.Points[i - 1].Xz, to = road.Points[i].Xz;
                    float length = (to - from).Length;
                    int steps = Math.Max((int)MathF.Ceiling(length / Cell), 1);
                    for (int s = 0; s <= steps; s++) Mark(Vector2.Lerp(from, to, s / (float)steps), reach);
                }
            }
            foreach (WorldSite site in world.Sites) Mark(site.Position, site.Radius);
            foreach (WorldSite haunt in world.Haunts) Mark(haunt.Position, haunt.Radius);
            foreach (GroundPad pad in world.Pads) Mark(pad.Footprint.Centre, pad.Footprint.BoundingRadius + pad.Margin);
            foreach (GroundPad terrace in world.Terraces) Mark(terrace.Footprint.Centre, terrace.Footprint.BoundingRadius + terrace.Margin);
            foreach (PaintPatch patch in world.Paint) Mark(patch.Shape.Centre, patch.Shape.BoundingRadius + patch.Softness);
            foreach (Clearing clearing in world.Clearings) Mark(clearing.Centre, clearing.Radius);
            foreach (WorldHall hall in world.Halls) Mark(hall.Footprint.Centre, hall.Footprint.BoundingRadius + Cell);

            float[] inland = world.Inland.Data;
            for (int i = 0; i < built.Length; i++)
            {
                if (inland[i] < 6f) built[i] = true;
            }
            return ScalarField.DistanceTo(width, depth, Cell, i => built[i]);
        }

        /// <summary>Metres from a point to the nearest thing anyone built or laid out, or the water.</summary>
        public float RoomAt(float x, float z) => _room.Sample(x, z);

        /// <summary>How rocky the land is at a point, 0..1: the biomes' own, none where water gathers and the soil with it, and none near anything built.</summary>
        public float Rockiness(float x, float z)
        {
            float room = _room.Sample(x, z);
            if (room <= Room) return 0f;
            float rocky = 0f;
            for (int b = 0; b < _rockiness.Length; b++) rocky += _world.BiomeWeights[b].Sample(x, z) * _rockiness[b];
            rocky *= 1f - Smoothstep(0.3f, 0.7f, _world.Flow.Sample(x, z));
            return rocky * Smoothstep(Room, FullRoom, room);
        }

        /// <summary>How much of a point is bedrock showing through the ground, 0..1: patches, larger and more of them the rockier the land, and the tops of knolls most of all.</summary>
        public float Pavement(float x, float z)
        {
            float rocky = Rockiness(x, z);
            if (rocky <= 0.02f) return 0f;
            float patches = _noise.Fbm01(x, z, 23f, 3) * 0.8f + _noise.Fbm01(x + 51f, z - 17f, 7f, 2) * 0.2f + Knoll(x, z) * 0.18f;
            float threshold = Lerp(0.8f, 0.6f, rocky);
            return Smoothstep(threshold, threshold + 0.06f, patches);
        }

        /// <summary>
        /// How far the ground stands up out of the land at a point, in metres: a low knoll where the land's rocky, and its bedrock a hand's width proud of the soil. Never steeper than
        /// a third, so nobody's stopped by it and nobody behind it is hidden.
        /// </summary>
        public float Relief(float x, float z)
        {
            float rocky = Rockiness(x, z);
            if (rocky <= 0.02f) return 0f;
            // Ground that's already a slope keeps to it: a knoll on it could make it steeper than anyone can climb.
            float level = 1f - Smoothstep(0.15f, 0.45f, _world.Elevation.SlopeAt(new Vector2(x, z)));
            return Knoll(x, z) * KnollHeight * rocky * level + Pavement(x, z) * Proud;
        }

        // A knoll: the land rising in a broad dome where the noise is high, a fifth of the way from nothing to its top across a band of the noise wide enough to keep it gentle.
        private float Knoll(float x, float z)
        {
            float rise = _noise.Fbm01(x - 300f, z + 911f, 38f, 3);
            return Smoothstep(0.56f, 0.86f, rise);
        }
    }
}

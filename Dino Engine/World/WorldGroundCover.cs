using Dino_Engine.Modelling.Procedural.Rock;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.World.WorldMath;

namespace Dino_Engine.World
{
    /// <summary>A small plant or stone on the ground.</summary>
    public struct GroundPlant
    {
        public WorldAsset Asset;
        public string Model;
        public Vector3 Position;
        public float Yaw;
        public float Scale;
    }

    /// <summary>
    /// Grows the small plants of a chunk when it streams in; a whole world of ferns and flowers would be far too many to keep.
    /// Plants sit one to a cell of a jittered grid fixed in world space, and one random draw per cell picks which kind grows
    /// there, if any: this is the "layered dithering" of Horizon Zero Dawn. No two plants can collide, whichever order chunks
    /// load in, and a cell always grows the same plant when it comes back.
    /// </summary>
    public sealed class WorldGroundCover
    {
        /// <summary>Metres between plants. Divides the terrain chunk size, so cells never straddle a chunk.</summary>
        public const float Cell = 1.6f;

        private const PlacementLayer Avoid = PlacementLayer.Built | PlacementLayer.Road | PlacementLayer.Rock | PlacementLayer.Deadwood | PlacementLayer.Shrub;

        private readonly WorldData _world;
        private readonly uint _seed;
        private readonly WorldNoise _drifts;

        public WorldGroundCover(WorldData world)
        {
            _world = world;
            _seed = (uint)world.Plan.Seed * 2654435761u + 0x9E3779B9u;
            _drifts = new WorldNoise(world.Random.Fork("ground cover"));
        }

        /// <summary>Fills <paramref name="results"/> with the plants of the square from <paramref name="origin"/> with sides of <paramref name="size"/>.</summary>
        public void Generate(Vector2 origin, float size, List<GroundPlant> results)
        {
            results.Clear();
            int first = (int)MathF.Round(origin.X / Cell);
            int firstZ = (int)MathF.Round(origin.Y / Cell);
            int cells = (int)MathF.Round(size / Cell);

            for (int z = firstZ; z < firstZ + cells; z++)
            {
                for (int x = first; x < first + cells; x++)
                {
                    TryGrow(x, z, results);
                }
            }
        }

        private void TryGrow(int cellX, int cellZ, List<GroundPlant> results)
        {
            uint state = Hash(cellX, cellZ);
            float jitterX = Random01(ref state);
            float jitterZ = Random01(ref state);
            // Jitter keeps clear of the cell's rim, which is what guarantees neighbours never meet.
            Vector2 point = new Vector2(cellX + 0.12f + jitterX * 0.76f, cellZ + 0.12f + jitterZ * 0.76f) * Cell;
            if (!_world.InBounds(point)) return;

            WorldData world = _world;
            if (world.PavedRoad.Sample(point) + world.TrailRoad.Sample(point) + world.Plaza.Sample(point) + world.Yard.Sample(point) + world.Farmland.Sample(point) > 0.25f) return;
            if (world.Paved && world.Paving.Sample(point) > 0.15f) return;

            float height = world.Height.Sample(point);
            float above = height - world.WaterLevelAt(point);
            if (above < 0.12f) return;

            float slope = world.Height.SlopeAt(point);

            // A scree is nothing but small stones, on ground far too steep for anything else.
            float scree = world.Scree.Sample(point);
            if (scree > 0.25f && slope < 1.7f)
            {
                if (Random01(ref state) >= scree * 0.75f) return;
                WorldAsset chip = NatureCatalog.RockOf(world.Geology.Kind, RockForm.Chip);
                results.Add(new GroundPlant
                {
                    Asset = chip,
                    Model = NatureCatalog.ModelAt(chip, world.Plan.Seed, point),
                    Position = new Vector3(point.X, height - 0.03f, point.Y),
                    Yaw = Random01(ref state) * MathF.Tau,
                    Scale = chip.Scale.Lerp(Random01(ref state)),
                });
                return;
            }
            if (slope > 0.6f) return;

            float pick = Random01(ref state);
            BiomeDefinition biome = PickBiome(point, ref state);
            float canopy = world.Canopy.Sample(point);

            // Flowers grow in drifts, not evenly; these are what make a meadow read from far off.
            float drift = Smoothstep(0.42f, 0.7f, _drifts.Fbm01(point.X, point.Y, 34f, 3));
            float floor = biome.FloorDensity * canopy;
            float open = biome.OpenDensity * (1f - canopy) * (0.12f + 0.88f * drift);
            float waterside = biome.Waterside.Length > 0 ? 0.85f * (1f - Smoothstep(0.25f, 1.3f, above)) : 0f;

            // One draw, three bands: where it falls decides the layer, so layers can't land on the same spot.
            Species[] list;
            if (pick < waterside) list = biome.Waterside;
            else if (pick < waterside + floor) list = biome.ForestFloor;
            else if (pick < waterside + floor + open) list = biome.OpenGround;
            else return;
            if (list.Length == 0) return;

            WorldAsset asset = NatureCatalog.Resolve(PickSpecies(list, ref state), world.Geology.Kind);
            float scale = asset.Scale.Lerp(Random01(ref state));
            float reach = asset.Footprint * scale * 0.7f;
            Footprint footprint = Footprint.Circle(point, reach);
            if (world.Placement.Overlaps(footprint, Avoid) || world.Placement.OverlapsSolid(footprint)) return;

            results.Add(new GroundPlant
            {
                Asset = asset,
                Model = NatureCatalog.ModelAt(asset, world.Plan.Seed, point),
                Position = new Vector3(point.X, height - 0.02f, point.Y),
                Yaw = Random01(ref state) * MathF.Tau,
                Scale = scale,
            });
        }

        private BiomeDefinition PickBiome(Vector2 point, ref uint state)
        {
            float pick = Random01(ref state);
            for (int b = 0; b < _world.BiomeWeights.Length; b++)
            {
                pick -= _world.BiomeWeights[b].Sample(point);
                if (pick <= 0f) return _world.Plan.Biomes[b];
            }
            return _world.Plan.Biomes[^1];
        }

        private static WorldAsset PickSpecies(Species[] list, ref uint state)
        {
            float total = 0f;
            foreach (Species species in list) total += species.Weight;
            float pick = Random01(ref state) * total;
            foreach (Species species in list)
            {
                pick -= species.Weight;
                if (pick <= 0f) return species.Asset;
            }
            return list[^1].Asset;
        }

        private uint Hash(int x, int z)
        {
            unchecked
            {
                uint h = _seed ^ (uint)x * 0x85EBCA6Bu ^ (uint)z * 0xC2B2AE35u;
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                h *= 0x297A2D39u;
                h ^= h >> 15;
                return h;
            }
        }

        private static float Random01(ref uint state)
        {
            unchecked
            {
                state = state * 1664525u + 1013904223u;
                uint x = state;
                x ^= x >> 16;
                x *= 0x7FEB352Du;
                x ^= x >> 15;
                return (x >> 8) * (1f / 16777216f);
            }
        }
    }
}

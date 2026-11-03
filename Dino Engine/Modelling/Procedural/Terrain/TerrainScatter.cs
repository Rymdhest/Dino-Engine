using Dino_Engine.Modelling.Model;
using Dino_Engine.Physics;
using Dino_Engine.Rendering.Renderers.Geometry;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;

namespace Dino_Engine.Modelling.Procedural.Terrain
{
    /// <summary>
    /// Decides whether a candidate point keeps its instance, and how that instance is scaled.
    /// Only draw randomness from <paramref name="rng"/>: it is seeded per chunk, which is what makes
    /// a chunk regenerate identical vegetation every time it streams back in.
    /// </summary>
    public delegate bool TerrainScatterRule(Vector3 position, Vector3 normal, Random rng, out Vector3 scale);

    /// <summary>
    /// One kind of object (a tree, a rock, ...) scattered over the terrain.
    /// </summary>
    public class TerrainScatterLayer
    {
        public readonly glModel Model;
        public readonly float Density;        // Candidate points per square world unit (before the rule rejects any).
        public readonly float RoadClearance;  // Extra distance kept clear beyond the road shoulder.
        public readonly TerrainScatterRule Rule;
        public readonly bool AllowUnderwater; // Rocks can lie in water; plants are kept out of the sea, ponds and puddles.

        public TerrainScatterLayer(glModel model, float density, float roadClearance, TerrainScatterRule rule, bool allowUnderwater = false)
        {
            Model = model;
            Density = density;
            RoadClearance = roadClearance;
            Rule = rule;
            AllowUnderwater = allowUnderwater;
        }
    }

    public struct TerrainScatterInstance
    {
        public glModel Model;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
    }

    /// <summary>
    /// Generates the scattered objects of a single terrain chunk. The result depends only on the seed,
    /// the chunk cell and the layers, so chunks can be generated lazily and in any order.
    /// </summary>
    public static class TerrainScatter
    {
        /// <param name="water">Where the water is; instances of layers that don't allow it are kept out of it. Null: no water.</param>
        public static void GenerateChunk(TerrainGenerator generator, IReadOnlyList<TerrainScatterLayer> layers, int seed,
            Vector2i cell, float chunkSize, Vector2 boundsMin, Vector2 boundsMax, List<TerrainScatterInstance> results, WaterQuery? water = null)
        {
            if (water != null && water.IsEmpty) water = null;
            results.Clear();
            if (layers.Count == 0) return;

            Vector2 origin = new Vector2(cell.X * chunkSize, cell.Y * chunkSize);
            if (origin.X >= boundsMax.X || origin.Y >= boundsMax.Y ||
                origin.X + chunkSize <= boundsMin.X || origin.Y + chunkSize <= boundsMin.Y)
                return;

            float maxClearance = 0f;
            for (int i = 0; i < layers.Count; i++)
                maxClearance = MathF.Max(maxClearance, layers[i].RoadClearance);

            // Gather road segments once per chunk instead of three times per candidate point. The padding
            // covers the widest road check plus the sample offset GetNormalAt uses.
            float padding = generator.RoadWidth + generator.RoadShoulder + maxClearance + 1f;
            var roads = generator.GetCandidateSegmentsForChunk(origin, origin + new Vector2(chunkSize), padding);

            float area = chunkSize * chunkSize;
            for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
            {
                TerrainScatterLayer layer = layers[layerIndex];
                Random rng = new Random(ChunkSeed(seed, cell.X, cell.Y, layerIndex));

                // Uniform points with a Poisson count per chunk is the same distribution as scattering
                // (density * total area) uniform points over the whole map at once.
                int count = SamplePoisson(rng, layer.Density * area);
                for (int i = 0; i < count; i++)
                {
                    Vector2 p = origin + new Vector2(rng.NextSingle(), rng.NextSingle()) * chunkSize;
                    if (p.X < boundsMin.X || p.Y < boundsMin.Y || p.X >= boundsMax.X || p.Y >= boundsMax.Y) continue;
                    if (generator.IsOnRoad(p, roads, layer.RoadClearance)) continue;

                    Vector3 position = new Vector3(p.X, generator.getHeightAt(p, roads), p.Y);
                    Vector3 normal = generator.GetNormalAt(p.X, p.Y, roads);
                    if (!layer.Rule(position, normal, rng, out Vector3 scale)) continue;
                    Quaternion rotation = new Quaternion(new Vector3(0f, rng.NextSingle() * MathF.Tau, 0f));
                    // Last, after every random draw, so water never changes what grows anywhere else in the chunk.
                    if (water != null && !layer.AllowUnderwater
                        && water.TryGetSurfaceAbove(position + new Vector3(0f, TerrainRenderer.SurfaceOffset, 0f), 0.01f, out _, out _)) continue;

                    results.Add(new TerrainScatterInstance
                    {
                        Model = layer.Model,
                        Position = position,
                        Rotation = rotation,
                        Scale = scale
                    });
                }
            }
        }

        private static int SamplePoisson(Random rng, double mean)
        {
            // Knuth's method, in slices so exp(-slice) never underflows. Poisson counts add up, so the sum
            // of the slices is still exactly Poisson(mean).
            int count = 0;
            while (mean > 0.0)
            {
                double slice = Math.Min(mean, 30.0);
                mean -= slice;
                double limit = Math.Exp(-slice);
                double p = rng.NextDouble();
                while (p > limit)
                {
                    count++;
                    p *= rng.NextDouble();
                }
            }
            return count;
        }

        // Fixed hash (HashCode.Combine is randomized per process) so a seed always gives the same world.
        private static int ChunkSeed(int seed, int cellX, int cellZ, int layerIndex)
        {
            unchecked
            {
                uint h = Mix((uint)seed);
                h = Mix(h ^ (uint)cellX);
                h = Mix(h ^ (uint)cellZ);
                h = Mix(h ^ (uint)layerIndex);
                return (int)h;
            }
        }

        private static uint Mix(uint h)
        {
            unchecked
            {
                h += 0x9E3779B9u;
                h ^= h >> 16;
                h *= 0x85EBCA6Bu;
                h ^= h >> 13;
                h *= 0xC2B2AE35u;
                h ^= h >> 16;
                return h;
            }
        }
    }
}

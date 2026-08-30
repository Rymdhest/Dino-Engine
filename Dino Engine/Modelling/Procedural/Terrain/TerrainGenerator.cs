using Dino_Engine.Util;
using Dino_Engine.Util.Data_Structures.Grids;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using Util.Noise;

namespace Dino_Engine.Modelling.Procedural.Terrain
{
    public class TerrainGenerator
    {
        public float _mountainCoverage = 0.1f;
        public float _frequenzy = 0.01f;
        public int _octaves = 13;
        public float mountainScale = 500f;
        public float noiseScale = 5f;

        public float RoadWidth = 1.0f;
        public float RoadShoulder =4.0f;

        private OpenSimplexNoise noise;
        private OpenSimplexNoise grassNoise;

        // Reference to your global road graph/splines
        public List<RoadSpline> ActiveRoadSplines { get; set; } = new List<RoadSpline>();

        public TerrainGenerator()
        {
            noise = new OpenSimplexNoise();
            grassNoise = new OpenSimplexNoise();
        }

        public TerrainGenerator(long seed)
        {
            noise = new OpenSimplexNoise(seed);
        }

        /// <summary>
        /// Checks if a 2D world position is on or near a road.
        /// </summary>
        /// <param name="position">World XZ position.</param>
        /// <param name="clearanceMargin">Extra distance around the road to keep clear (e.g., 2.0f so trees aren't right on the edge).</param>
        /// <param name="includeShoulder">If true, treats the shoulder as part of the road zone.</param>
        public bool IsOnRoad(Vector2 position, float clearanceMargin = 0f, bool includeShoulder = true)
        {
            float thresholdRadius = (includeShoulder ? (RoadWidth + RoadShoulder) : RoadWidth) + clearanceMargin;
            List<RoadSegment2D> candidates = GetCandidateSegmentsForChunk(position, position, thresholdRadius);
            return IsOnRoad(position, candidates, clearanceMargin, includeShoulder);
        }

        /// <summary>
        /// Optimized overload for batch object spawning during chunk generation.
        /// </summary>
        public bool IsOnRoad(Vector2 position, List<RoadSegment2D> candidateSegments, float clearanceMargin = 0f, bool includeShoulder = true)
        {
            if (candidateSegments == null || candidateSegments.Count == 0)
                return false;

            float roadRadius = includeShoulder ? (RoadWidth + RoadShoulder) : RoadWidth;
            float maxDist = roadRadius + clearanceMargin;
            float maxDistSq = maxDist * maxDist;

            for (int i = 0; i < candidateSegments.Count; i++)
            {
                var seg = candidateSegments[i];

                float t = 0f;
                if (seg.ABLenSq > 1e-6f)
                {
                    t = Vector2.Dot(position - seg.A, seg.AB) / seg.ABLenSq;
                    t = MathHelper.Clamp(t, 0f, 1f);
                }

                Vector2 closest2D = seg.A + seg.AB * t;
                float distSq = Vector2.DistanceSquared(position, closest2D);

                if (distSq <= maxDistSq)
                {
                    return true; // Point is on/near the road
                }
            }

            return false;
        }

        public Vector3 GetNormalAt(float x, float z)
        {
            float eps = 0.1f;
            float padding = RoadWidth + RoadShoulder;

            // Filter road segments around the sample point (including eps offset)
            Vector2 min = new Vector2(x - eps, z - eps);
            Vector2 max = new Vector2(x + eps, z + eps);
            List<RoadSegment2D> candidates = GetCandidateSegmentsForChunk(min, max, padding);

            return GetNormalAt(x, z, candidates);
        }

        // Overload for batch/chunk generation when candidate segments are already available
        public Vector3 GetNormalAt(float x, float z, List<RoadSegment2D> candidateSegments)
        {
            float eps = 0.1f;

            float hL = getHeightAt(new Vector2(x - eps, z), candidateSegments);
            float hR = getHeightAt(new Vector2(x + eps, z), candidateSegments);
            float hD = getHeightAt(new Vector2(x, z - eps), candidateSegments);
            float hU = getHeightAt(new Vector2(x, z + eps), candidateSegments);

            float dX = (hR - hL) / (2f * eps);
            float dZ = (hU - hD) / (2f * eps);

            Vector3 tangentX = new Vector3(1f, dX, 0f);
            Vector3 tangentZ = new Vector3(0f, dZ, 1f);

            return Vector3.Normalize(Vector3.Cross(tangentZ, tangentX));
        }

        /// <summary>
        /// Generates the road weight grid passed into channel B of normalHeightTextureArray.
        /// </summary>
        public FloatGrid generateRoadMaskGrid(Vector2 chunkPositionWorld, Vector2 sizeWorld, Vector2i resolution)
        {
            FloatGrid maskGrid = new FloatGrid(resolution);

            float padding = RoadWidth + RoadShoulder;
            List<RoadSegment2D> candidates = GetCandidateSegmentsForChunk(chunkPositionWorld, chunkPositionWorld + sizeWorld, padding);

            // Fast-path: Return the zeroed mask immediately if no roads touch this chunk
            if (candidates.Count == 0)
                return maskGrid;

            Vector2 cellSizeWorld = sizeWorld / (resolution - new Vector2(1));

            for (int z = 0; z < maskGrid.Resolution.Y; z++)
            {
                for (int x = 0; x < maskGrid.Resolution.X; x++)
                {
                    Vector2 worldPos = chunkPositionWorld + new Vector2(x, z) * cellSizeWorld;
                    maskGrid.Values[x, z] = getRoadMaskAt(worldPos, candidates);
                }
            }

            return maskGrid;
        }

        public FloatGrid generateChunk(Vector2 chunkPositionWorld, Vector2 sizeWorld, Vector2i resolution)
        {
            FloatGrid grid = new FloatGrid(resolution);
            Vector2 cellSizeWorld = sizeWorld / (resolution - new Vector2(1));

            // Gather candidate segments touching this chunk
            float padding = RoadWidth + RoadShoulder;
            List<RoadSegment2D> candidates = GetCandidateSegmentsForChunk(chunkPositionWorld, chunkPositionWorld + sizeWorld, padding);

            for (int z = 0; z < grid.Resolution.Y; z++)
            {
                for (int x = 0; x < grid.Resolution.X; x++)
                {
                    Vector2 worldPos = chunkPositionWorld + new Vector2(x, z) * cellSizeWorld;
                    grid.Values[x, z] = getHeightAt(worldPos, candidates);
                }
            }

            return grid;
        }

        public Vector3Grid generateNormalGridFor(FloatGrid heightMap, Vector3 size, Vector2 worldOrigin, out FloatGrid grassGrid)
        {
            Vector2 chunkSizeWorld = new Vector2(size.X, size.Z);
            float padding = RoadWidth + RoadShoulder;
            List<RoadSegment2D> candidates = GetCandidateSegmentsForChunk(worldOrigin, worldOrigin + chunkSizeWorld, padding);

            size.X /= (heightMap.Resolution.X - 1);
            size.Z /= (heightMap.Resolution.Y - 1);
            Vector3Grid normalGrid = new Vector3Grid(heightMap.Resolution);
            grassGrid = new FloatGrid(heightMap.Resolution);

            for (int z = 0; z < normalGrid.Resolution.Y; z++)
            {
                for (int x = 0; x < normalGrid.Resolution.X; x++)
                {
                    float worldX = worldOrigin.X + x * size.X;
                    float worldZ = worldOrigin.Y + z * size.Z;
                    Vector2 worldPos = new Vector2(worldX, worldZ);

                    float hL, hR, hD, hU;

                    if (x - 1 >= 0)
                        hL = heightMap.Values[x - 1, z] * size.Y;
                    else
                        hL = getHeightAt(new Vector2(worldX - size.X, worldZ), candidates) * size.Y;

                    if (x + 1 < heightMap.Resolution.X)
                        hR = heightMap.Values[x + 1, z] * size.Y;
                    else
                        hR = getHeightAt(new Vector2(worldX + size.X, worldZ), candidates) * size.Y;

                    if (z - 1 >= 0)
                        hD = heightMap.Values[x, z - 1] * size.Y;
                    else
                        hD = getHeightAt(new Vector2(worldX, worldZ - size.Z), candidates) * size.Y;

                    if (z + 1 < heightMap.Resolution.Y)
                        hU = heightMap.Values[x, z + 1] * size.Y;
                    else
                        hU = getHeightAt(new Vector2(worldX, worldZ + size.Z), candidates) * size.Y;

                    float dX = (hR - hL) / (2f * size.X);
                    float dz = (hU - hD) / (2f * size.Z);

                    Vector3 tangentX = new Vector3(1f, dX, 0f);
                    Vector3 tangentZ = new Vector3(0f, dz, 1f);

                    Vector3 normal = Vector3.Normalize(Vector3.Cross(tangentZ, tangentX));

                    float roadWeight = getRoadMaskAt(worldPos, candidates);

                    normalGrid.Values[x, z] = new Vector3(normal.X, normal.Z, roadWeight);

                    float flatness = Vector3.Dot(normal, new Vector3(0f, 1f, 0f));
                    float smallPatch =  0.5f + 0.5f * MathF.Pow(grassNoise.FBM01(worldX, worldZ, 0.55f, 3), 1.0f);
                    float bigPatch =    0.3f + 0.7f * MathF.Pow(grassNoise.FBM01(worldX, worldZ, 0.2f, 3), 1.0f);
                    grassGrid.Values[x, z] = flatness* smallPatch * bigPatch;
                }
            }

            return normalGrid;
        }

        public bool TryGetRoadInfoAt(Vector2 position, List<RoadSegment2D> candidateSegments, out float roadTargetHeight, out float carveBlendFactor, out float maskWeight)
        {
            roadTargetHeight = 0f;
            carveBlendFactor = 0f;
            maskWeight = 0f;

            if (candidateSegments == null || candidateSegments.Count == 0)
                return false;

            float totalRadius = RoadWidth + RoadShoulder;
            float minDistanceSq = totalRadius * totalRadius;
            float bestTargetHeight = 0f;
            bool foundRoad = false;

            for (int i = 0; i < candidateSegments.Count; i++)
            {
                var seg = candidateSegments[i];

                float t = 0f;
                if (seg.ABLenSq > 1e-6f)
                {
                    t = Vector2.Dot(position - seg.A, seg.AB) / seg.ABLenSq;
                    t = MathHelper.Clamp(t, 0f, 1f);
                }

                Vector2 closest2D = seg.A + seg.AB * t;
                float distSq = Vector2.DistanceSquared(position, closest2D);

                if (distSq < minDistanceSq)
                {
                    minDistanceSq = distSq;
                    bestTargetHeight = MathHelper.Lerp(seg.P0.Y, seg.P1.Y, t);
                    foundRoad = true;
                }
            }

            if (!foundRoad)
                return false;

            float minDistance = MathF.Sqrt(minDistanceSq);
            roadTargetHeight = bestTargetHeight;

            if (minDistance <= RoadWidth)
            {
                carveBlendFactor = 1.0f;
                maskWeight = 1.0f;
            }
            else
            {
                float normDist = (minDistance - RoadWidth) / RoadShoulder;
                float smoothT = normDist * normDist * (3.0f - 2.0f * normDist);
                float weight = 1.0f - smoothT;

                carveBlendFactor = weight;
                maskWeight = weight;
            }

            return true;
        }
        public struct RoadSegment2D
        {
            public Vector3 P0;
            public Vector3 P1;
            public Vector2 A;
            public Vector2 B;
            public Vector2 AB;
            public float ABLenSq;
        }

        private List<RoadSegment2D> GetCandidateSegmentsForChunk(Vector2 chunkMin, Vector2 chunkMax, float padding)
        {
            var candidates = new List<RoadSegment2D>();
            if (ActiveRoadSplines == null || ActiveRoadSplines.Count == 0)
                return candidates;

            Vector2 pMin = chunkMin - new Vector2(padding);
            Vector2 pMax = chunkMax + new Vector2(padding);

            foreach (var spline in ActiveRoadSplines)
            {
                if (spline == null || spline.BakedPoints == null || spline.BakedPoints.Count < 2)
                    continue;

                // Quick rejection for the entire spline
                if (pMax.X < spline.MinBounds.X || pMin.X > spline.MaxBounds.X ||
                    pMax.Y < spline.MinBounds.Y || pMin.Y > spline.MaxBounds.Y)
                    continue;

                var points = spline.BakedPoints;
                for (int i = 0; i < points.Count - 1; i++)
                {
                    Vector3 p0 = points[i];
                    Vector3 p1 = points[i + 1];

                    Vector2 a = new Vector2(p0.X, p0.Z);
                    Vector2 b = new Vector2(p1.X, p1.Z);

                    Vector2 segMin = Vector2.ComponentMin(a, b);
                    Vector2 segMax = Vector2.ComponentMax(a, b);

                    // Keep segment if its bounding box intersects the chunk bounds + padding
                    if (pMax.X >= segMin.X && pMin.X <= segMax.X &&
                        pMax.Y >= segMin.Y && pMin.Y <= segMax.Y)
                    {
                        Vector2 ab = b - a;
                        candidates.Add(new RoadSegment2D
                        {
                            P0 = p0,
                            P1 = p1,
                            A = a,
                            B = b,
                            AB = ab,
                            ABLenSq = ab.LengthSquared
                        });
                    }
                }
            }

            return candidates;
        }
        public float getHeightAt(Vector2 position)
        {
            float padding = RoadWidth + RoadShoulder;
            List<RoadSegment2D> candidates = GetCandidateSegmentsForChunk(position, position, padding);
            return getHeightAt(position, candidates);
        }
        public float getHeightAt(Vector2 position, List<RoadSegment2D> candidateSegments)
        {
            float baseTerrainHeight = getRawNoiseHeightAt(position);

            if (TryGetRoadInfoAt(position, candidateSegments, out float roadTargetHeight, out float carveBlendFactor, out _))
            {
                return MathHelper.Lerp(baseTerrainHeight, roadTargetHeight, carveBlendFactor);
            }

            return baseTerrainHeight;
        }

        public float getRoadMaskAt(Vector2 position, List<RoadSegment2D> candidateSegments)
        {
            if (TryGetRoadInfoAt(position, candidateSegments, out _, out _, out float maskWeight))
            {
                return maskWeight;
            }
            return 0.0f;
        }

        public float getRawNoiseHeightAt(Vector2 position)
        {
            float x = position.X;
            float z = position.Y;
            float y = 0f;
            float frequency = 0.05f;
            float amplitude = 1f;
            float totalAmplitude = 0f;

            for (int i = 0; i < 13; i++)
            {
                y += (noise.Evaluate(x * frequency, z * frequency) * 0.5f + 0.5f) * amplitude;
                totalAmplitude += amplitude;
                frequency *= 2f;
                amplitude *= 0.5f;
            }
            y /= totalAmplitude;
            y *= 3f;

            amplitude = 1f;
            totalAmplitude = 0f;
            frequency = 0.002f;
            float exponent = 3.6f;
            float yMountain = 0f;

            for (int i = 0; i < 11; i++)
            {
                yMountain += MathF.Pow((1.0f - MathF.Abs(noise.Evaluate(x * frequency, z * frequency))), exponent) * amplitude;
                totalAmplitude += amplitude;
                frequency *= 2f;
                amplitude *= 0.5f;
                exponent *= 0.8f;
            }
            yMountain /= totalAmplitude;

            amplitude = 1f;
            totalAmplitude = 0f;
            frequency = 0.005f;
            exponent = 3.0f;
            float mountainFactor = 0f;

            for (int i = 0; i < 2; i++)
            {
                mountainFactor += MathF.Pow((noise.Evaluate(x * frequency, z * frequency) * 0.5f + 0.5f), exponent) * amplitude;
                totalAmplitude += amplitude;
                frequency *= 2f;
                amplitude *= 0.5f;
                exponent *= 0.8f;
            }
            mountainFactor /= totalAmplitude;

            yMountain *= mountainFactor;
            yMountain *= 300f;

            y += yMountain;

            float smoothEdgeRange = 50f;
            if (position.X < smoothEdgeRange) y *= MyMath.lerp(0, 1, position.X / smoothEdgeRange);
            if (position.Y < smoothEdgeRange) y *= MyMath.lerp(0, 1, position.Y / smoothEdgeRange);

            if (position.X < 500 & position.Y < 500) y = 0.0f;

            return y;
        }
    }
}
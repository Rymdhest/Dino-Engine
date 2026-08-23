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

        public float RoadWidth = 1.4f;
        public float RoadShoulder =3.0f;

        private OpenSimplexNoise noise;

        // Reference to your global road graph/splines
        public List<RoadSpline> ActiveRoadSplines { get; set; } = new List<RoadSpline>();

        public TerrainGenerator()
        {
            noise = new OpenSimplexNoise();
        }

        public TerrainGenerator(long seed)
        {
            noise = new OpenSimplexNoise(seed);
        }

        public Vector3 GetNormalAt(float x, float z)
        {
            float eps = 0.1f;

            // Samples heights around target coordinate (includes road carving)
            float hL = getHeightAt(new Vector2(x - eps, z));
            float hR = getHeightAt(new Vector2(x + eps, z));
            float hD = getHeightAt(new Vector2(x, z - eps));
            float hU = getHeightAt(new Vector2(x, z + eps));

            // Gradient calculation
            float dX = (hR - hL) / (2f * eps);
            float dZ = (hU - hD) / (2f * eps);

            Vector3 tangentX = new Vector3(1f, dX, 0f);
            Vector3 tangentZ = new Vector3(0f, dZ, 1f);

            Vector3 normal = Vector3.Cross(tangentZ, tangentX);
            return Vector3.Normalize(normal);
        }

        /// <summary>
        /// Gets the final carved height at any world coordinate.
        /// </summary>
        public float getHeightAt(Vector2 position)
        {
            float baseTerrainHeight = getRawNoiseHeightAt(position);

            // Check distance to nearby road splines
            if (TryGetRoadInfoAt(position, out float roadTargetHeight, out float carveBlendFactor, out _))
            {
                // Smoothly blend from road surface elevation to natural noise elevation
                return MathHelper.Lerp(baseTerrainHeight, roadTargetHeight, carveBlendFactor);
            }

            return baseTerrainHeight;
        }

        /// <summary>
        /// Gets the road mask weight (1.0 = center of road, 0.0 = off road) for shader blending.
        /// </summary>
        public float getRoadMaskAt(Vector2 position)
        {
            if (TryGetRoadInfoAt(position, out _, out _, out float maskWeight))
            {
                return maskWeight;
            }
            return 0.0f;
        }

        /// <summary>
        /// Generates the road weight grid passed into channel B of normalHeightTextureArray.
        /// </summary>
        public FloatGrid generateRoadMaskGrid(Vector2 chunkPositionWorld, Vector2 sizeWorld, Vector2i resolution)
        {
            FloatGrid maskGrid = new FloatGrid(resolution);
            Vector2 cellSizeWorld = sizeWorld / (resolution - new Vector2(1));

            for (int z = 0; z < maskGrid.Resolution.Y; z++)
            {
                for (int x = 0; x < maskGrid.Resolution.X; x++)
                {
                    Vector2 worldPos = chunkPositionWorld + new Vector2(x, z) * cellSizeWorld;
                    maskGrid.Values[x, z] = getRoadMaskAt(worldPos);
                }
            }

            return maskGrid;
        }

        public FloatGrid generateChunk(Vector2 chunkPositionWorld, Vector2 sizeWorld, Vector2i resolution)
        {
            FloatGrid grid = new FloatGrid(resolution);
            Vector2 cellSizeWorld = sizeWorld / (resolution - new Vector2(1));

            for (int z = 0; z < grid.Resolution.Y; z++)
            {
                for (int x = 0; x < grid.Resolution.X; x++)
                {
                    Vector2 worldPos = chunkPositionWorld + new Vector2(x, z) * cellSizeWorld;
                    grid.Values[x, z] = getHeightAt(worldPos);
                }
            }

            return grid;
        }

        public Vector3Grid generateNormalGridFor(FloatGrid heightMap, Vector3 size, Vector2 worldOrigin)
        {
            size.X /= (heightMap.Resolution.X - 1);
            size.Z /= (heightMap.Resolution.Y - 1);
            Vector3Grid normalGrid = new Vector3Grid(heightMap.Resolution);

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
                        hL = getHeightAt(new Vector2(worldX - size.X, worldZ)) * size.Y;

                    if (x + 1 < heightMap.Resolution.X)
                        hR = heightMap.Values[x + 1, z] * size.Y;
                    else
                        hR = getHeightAt(new Vector2(worldX + size.X, worldZ)) * size.Y;

                    if (z - 1 >= 0)
                        hD = heightMap.Values[x, z - 1] * size.Y;
                    else
                        hD = getHeightAt(new Vector2(worldX, worldZ - size.Z)) * size.Y;

                    if (z + 1 < heightMap.Resolution.Y)
                        hU = heightMap.Values[x, z + 1] * size.Y;
                    else
                        hU = getHeightAt(new Vector2(worldX, worldZ + size.Z)) * size.Y;

                    float dX = (hR - hL) / (2f * size.X);
                    float dz = (hU - hD) / (2f * size.Z);

                    Vector3 tangentX = new Vector3(1f, dX, 0f);
                    Vector3 tangentZ = new Vector3(0f, dz, 1f);

                    Vector3 normal = Vector3.Normalize(Vector3.Cross(tangentZ, tangentX));

                    // Query road weight at this texel coordinate
                    float roadWeight = getRoadMaskAt(worldPos);

                    // X = Normal.X, Y = Normal.Z, Z = RoadWeight (0.0 to 1.0)
                    normalGrid.Values[x, z] = new Vector3(normal.X, normal.Z, roadWeight);
                }
            }

            return normalGrid;
        }


        public bool TryGetRoadInfoAt(Vector2 position, out float roadTargetHeight, out float carveBlendFactor, out float maskWeight)
        {
            roadTargetHeight = 0f;
            carveBlendFactor = 0f;
            maskWeight = 0f;

            if (ActiveRoadSplines == null || ActiveRoadSplines.Count == 0)
                return false;

            float totalRadius = RoadWidth + RoadShoulder;
            float minDistanceSq = totalRadius * totalRadius;
            float bestTargetHeight = 0f;
            bool foundRoad = false;

            foreach (var spline in ActiveRoadSplines)
            {
                if (spline == null || spline.BakedPoints == null || spline.BakedPoints.Count < 2)
                    continue;

                // Quick AABB rejection test (with shoulder margin)
                if (position.X < spline.MinBounds.X - totalRadius ||
                    position.X > spline.MaxBounds.X + totalRadius ||
                    position.Y < spline.MinBounds.Y - totalRadius ||
                    position.Y > spline.MaxBounds.Y + totalRadius)
                {
                    continue;
                }

                var points = spline.BakedPoints;
                for (int i = 0; i < points.Count - 1; i++)
                {
                    Vector3 p0 = points[i];
                    Vector3 p1 = points[i + 1];

                    Vector2 a = new Vector2(p0.X, p0.Z);
                    Vector2 b = new Vector2(p1.X, p1.Z);

                    Vector2 ab = b - a;
                    float abLenSq = ab.LengthSquared;

                    float t = 0f;
                    if (abLenSq > 1e-6f)
                    {
                        t = Vector2.Dot(position - a, ab) / abLenSq;
                        t = MathHelper.Clamp(t, 0f, 1f);
                    }

                    Vector2 closest2D = a + ab * t;
                    float distSq = Vector2.DistanceSquared(position, closest2D);

                    if (distSq < minDistanceSq)
                    {
                        minDistanceSq = distSq;
                        bestTargetHeight = MathHelper.Lerp(p0.Y, p1.Y, t);
                        foundRoad = true;
                    }
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
                // Smoothstep falloff across the shoulder zone: 1.0 at road edge -> 0.0 at shoulder edge
                float normDist = (minDistance - RoadWidth) / RoadShoulder;
                float smoothT = normDist * normDist * (3.0f - 2.0f * normDist);
                float weight = 1.0f - smoothT;

                carveBlendFactor = weight;
                maskWeight = weight;
            }

            return true;
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

            return y;
        }
    }
}
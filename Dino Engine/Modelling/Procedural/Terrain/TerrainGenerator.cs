using Dino_Engine.Core;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Physics;
using Dino_Engine.Util;
using Dino_Engine.Util.Data_Structures.Grids;
using OpenTK.Mathematics;
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

        private OpenSimplexNoise noise;

        public TerrainGenerator()
        {
            noise = new OpenSimplexNoise();
        }
        public TerrainGenerator(long seed)
        {
            noise = new OpenSimplexNoise(seed);
        }

        public Vector3Grid generateNormalGridFor(FloatGrid heightMap, Vector3 size, Vector2 worldOrigin)
        {
            size.X /= (heightMap.Resolution.X-1);
            size.Z /= (heightMap.Resolution.Y-1);
            Vector3Grid normalGrid = new Vector3Grid(heightMap.Resolution);
            for (int z = 0; z < normalGrid.Resolution.Y; z++)
            {
                for (int x = 0; x < normalGrid.Resolution.X; x++)
                {
                    float worldX = worldOrigin.X + x * size.X;
                    float worldZ = worldOrigin.Y + z * size.Z;
                    float hL, hR, hD, hU;

                    // Left neighbor
                    if (x - 1 >= 0)
                        hL = heightMap.Values[x - 1, z] * size.Y;
                    else
                        hL = getHeightAt(new Vector2(worldX - size.X, worldZ)) * size.Y;

                    // Right neighbor
                    if (x + 1 < heightMap.Resolution.X)
                        hR = heightMap.Values[x + 1, z] * size.Y;
                    else
                        hR = getHeightAt(new Vector2(worldX + size.X, worldZ)) * size.Y;

                    // Down neighbor
                    if (z - 1 >= 0)
                        hD = heightMap.Values[x, z - 1] * size.Y;
                    else
                        hD = getHeightAt(new Vector2(worldX, worldZ - size.Z)) * size.Y;

                    // Up neighbor
                    if (z + 1 < heightMap.Resolution.Y)
                        hU = heightMap.Values[x, z + 1] * size.Y;
                    else
                        hU = getHeightAt(new Vector2(worldX, worldZ + size.Z)) * size.Y;

                    float dX = (hR - hL) / (2f * size.X);
                    float dz = (hU - hD) / (2f * size.Z);

                    Vector3 tangentX = new Vector3(1f, dX, 0f);
                    Vector3 tangentZ = new Vector3(0f, dz, 1f);

                    Vector3 normal = Vector3.Cross(tangentZ, tangentX);
                    normal = Vector3.Normalize(normal);

                    normalGrid.Values[x, z] = normal;
                }
            }

            return normalGrid;
        }

        public FloatGrid generateChunk(Vector2 chunkPositionWorld, Vector2 sizeWorld, Vector2i resolution)
        {
            FloatGrid grid = new FloatGrid(resolution);
            Vector2 cellSizeWorld = sizeWorld / (resolution-new Vector2(1));

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

        public float getHeightAt(Vector2 position)
        {
            float x = position.X;
            float z = position.Y;
            float y = 0f;
            float frequency = _frequenzy;
            float amplitude = 1f;
            float totalAmplitude = 0f;

            frequency = 0.05f;
            noiseScale = 3f;
            _octaves = 13;
            mountainScale = 300f;


            for (int i = 0; i < _octaves; i++)
            {
                y += (noise.Evaluate(x * frequency, z * frequency)*0.5f+0.5f) * amplitude;
                totalAmplitude += amplitude;
                frequency *= 2f;
                amplitude *= 0.5f;
            }
            y /= totalAmplitude;
            y *= noiseScale;


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
                mountainFactor += MathF.Pow((noise.Evaluate(x * frequency, z * frequency)*0.5f+0.5f), exponent) * amplitude;
                totalAmplitude += amplitude;
                frequency *= 2f;
                amplitude *= 0.5f;
                exponent *= 0.8f;
            }
            mountainFactor /= totalAmplitude;

            yMountain *= mountainFactor;
            yMountain *= mountainScale;

            y += yMountain;

            //y -= 5f;
            //if (y < 0f) y *= 0.2f;



            float smoothEdgeRange = 50f;
            if (position.X < smoothEdgeRange) y *= MyMath.lerp(0, 1, position.X/ smoothEdgeRange);
            if (position.Y < smoothEdgeRange) y *= MyMath.lerp(0, 1, position.Y/ smoothEdgeRange);

            return y;
        }

        
    }
}

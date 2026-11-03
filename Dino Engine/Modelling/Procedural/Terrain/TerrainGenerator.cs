using Dino_Engine.Util.Data_Structures.Grids;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Terrain
{
    /// <summary>What a terrain chunk's ground is made of, one entry per texel of its maps.</summary>
    public sealed class TerrainChunkSurface
    {
        /// <summary>Material weights of the world's <see cref="TerrainSurface.Palette"/>, four per texel in each of three maps.</summary>
        public readonly float[][] Weights = new float[3][];
        /// <summary>Per texel: cover (r, which the grass carve erases under objects), height (g), dryness (b).</summary>
        public readonly float[] Grass;
        /// <summary>Per texel: the grass's colour.</summary>
        public readonly float[] GrassColour;

        public TerrainChunkSurface(int texels)
        {
            for (int i = 0; i < Weights.Length; i++) Weights[i] = new float[texels * 4];
            Grass = new float[texels * 4];
            GrassColour = new float[texels * 4];
        }
    }

    /// <summary>
    /// Answers questions about the ground of the current world, for the chunk streamer, physics and water. Everything comes from
    /// the maps baked into its <see cref="WorldData"/>, so every question is a lookup however many roads and buildings there are.
    /// </summary>
    public class TerrainGenerator
    {
        private const float NormalStep = 0.5f;

        private readonly TerrainSurface _surface;

        public WorldData World { get; }
        public IReadOnlyList<SurfaceMaterial> Palette => _surface.Palette;

        public TerrainGenerator(WorldData world)
        {
            World = world;
            _surface = new TerrainSurface(world);
        }

        public float getHeightAt(Vector2 position)
        {
            return World.Height.SampleCubic(position.X, position.Y);
        }

        public Vector3 GetNormalAt(float x, float z)
        {
            float hL = World.Height.SampleCubic(x - NormalStep, z);
            float hR = World.Height.SampleCubic(x + NormalStep, z);
            float hD = World.Height.SampleCubic(x, z - NormalStep);
            float hU = World.Height.SampleCubic(x, z + NormalStep);
            return new Vector3(hL - hR, 2f * NormalStep, hD - hU).Normalized();
        }

        public float getRoadMaskAt(Vector2 position)
        {
            return MathF.Max(World.PavedRoad.Sample(position), World.TrailRoad.Sample(position));
        }

        public FloatGrid generateChunk(Vector2 chunkPositionWorld, Vector2 sizeWorld, Vector2i resolution)
        {
            FloatGrid grid = new FloatGrid(resolution);
            Vector2 cellSizeWorld = sizeWorld / (resolution - new Vector2(1));

            for (int z = 0; z < grid.Resolution.Y; z++)
            {
                for (int x = 0; x < grid.Resolution.X; x++)
                {
                    grid.Values[x, z] = getHeightAt(chunkPositionWorld + new Vector2(x, z) * cellSizeWorld);
                }
            }

            return grid;
        }

        /// <summary>Normals (x and z) and the road weight of a chunk. Normals are taken from the world's maps, so chunks match along their edges.</summary>
        public Vector3Grid generateNormalGridFor(Vector2 chunkPositionWorld, Vector2 sizeWorld, Vector2i resolution)
        {
            Vector3Grid normalGrid = new Vector3Grid(resolution);
            Vector2 cellSizeWorld = sizeWorld / (resolution - new Vector2(1));

            for (int z = 0; z < resolution.Y; z++)
            {
                for (int x = 0; x < resolution.X; x++)
                {
                    Vector2 worldPos = chunkPositionWorld + new Vector2(x, z) * cellSizeWorld;
                    Vector3 normal = GetNormalAt(worldPos.X, worldPos.Y);
                    normalGrid.Values[x, z] = new Vector3(normal.X, normal.Z, getRoadMaskAt(worldPos));
                }
            }

            return normalGrid;
        }

        public TerrainChunkSurface generateSurface(Vector2 chunkPositionWorld, Vector2 sizeWorld, FloatGrid heightGrid, Vector3Grid normalGrid)
        {
            Vector2i resolution = heightGrid.Resolution;
            Vector2 cellSizeWorld = sizeWorld / (resolution - new Vector2(1));
            var surface = new TerrainChunkSurface(resolution.X * resolution.Y);
            Span<float> weights = stackalloc float[TerrainSurface.MaxMaterials];

            for (int z = 0; z < resolution.Y; z++)
            {
                for (int x = 0; x < resolution.X; x++)
                {
                    Vector2 worldPos = chunkPositionWorld + new Vector2(x, z) * cellSizeWorld;
                    Vector3 stored = normalGrid.Values[x, z];
                    float normalY = MathF.Sqrt(MathF.Max(1f - stored.X * stored.X - stored.Y * stored.Y, 0f));

                    _surface.Evaluate(worldPos.X, worldPos.Y, heightGrid.Values[x, z], normalY, weights, out GrassCell grass);

                    int texel = (z * resolution.X + x) * 4;
                    for (int i = 0; i < TerrainSurface.MaxMaterials; i++)
                    {
                        surface.Weights[i / 4][texel + i % 4] = weights[i];
                    }
                    surface.Grass[texel] = grass.Cover;
                    surface.Grass[texel + 1] = grass.Height;
                    surface.Grass[texel + 2] = grass.Dryness;
                    surface.GrassColour[texel] = grass.Colour.X;
                    surface.GrassColour[texel + 1] = grass.Colour.Y;
                    surface.GrassColour[texel + 2] = grass.Colour.Z;
                }
            }

            return surface;
        }
    }
}

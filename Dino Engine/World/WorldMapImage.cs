using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.World.WorldMath;

namespace Dino_Engine.World
{
    /// <summary>
    /// Draws a world as a map: biome colours, hill shading and water, then whatever was built on it. The game shows the result
    /// as its world map, and the WorldLab tool writes it to a file.
    /// </summary>
    public static class WorldMapImage
    {
        private static readonly Vector3 DeepWater = new Vector3(0.1f, 0.23f, 0.36f);
        private static readonly Vector3 ShallowWater = new Vector3(0.26f, 0.5f, 0.56f);
        private static readonly Vector3 StoneColour = new Vector3(0.45f, 0.44f, 0.43f);
        private static readonly Vector3 SunDirection = Vector3.Normalize(new Vector3(-0.6f, 0.65f, -0.5f));

        /// <summary>RGBA rows, four bytes per pixel, north (z = Size) at the top.</summary>
        public static byte[] Render(WorldData world, int resolution)
        {
            byte[] pixels = new byte[resolution * resolution * 4];
            float size = world.Size;
            float metresPerPixel = size / resolution;
            ScalarField height = world.Elevation;
            int biomeCount = world.Plan.Biomes.Count;

            Parallel.For(0, resolution, row =>
            {
                float z = size - (row + 0.5f) * metresPerPixel;
                for (int column = 0; column < resolution; column++)
                {
                    float x = (column + 0.5f) * metresPerPixel;
                    float h = height.Sample(x, z);

                    Vector3 colour;
                    if (h <= WorldPlan.SeaLevel)
                    {
                        colour = Vector3.Lerp(ShallowWater, DeepWater, Smoothstep(0f, 9f, -h));
                    }
                    else
                    {
                        colour = Vector3.Zero;
                        for (int b = 0; b < biomeCount; b++)
                        {
                            colour += world.Plan.Biomes[b].MapColour * world.BiomeWeights[b].Sample(x, z);
                        }

                        float step = MathF.Max(metresPerPixel, height.CellSize);
                        float dx = (height.Sample(x + step, z) - height.Sample(x - step, z)) / (2f * step);
                        float dz = (height.Sample(x, z + step) - height.Sample(x, z - step)) / (2f * step);
                        Vector3 normal = Vector3.Normalize(new Vector3(-dx, 1f, -dz));

                        float slope = MathF.Sqrt(dx * dx + dz * dz);
                        colour = Vector3.Lerp(colour, StoneColour, Smoothstep(0.55f, 1.1f, slope) * 0.75f);

                        float light = Vector3.Dot(normal, SunDirection) / SunDirection.Y;
                        colour *= Math.Clamp(0.45f + 0.55f * light, 0.35f, 1.3f);
                    }

                    int i = (row * resolution + column) * 4;
                    pixels[i] = ToByte(colour.X);
                    pixels[i + 1] = ToByte(colour.Y);
                    pixels[i + 2] = ToByte(colour.Z);
                    pixels[i + 3] = 255;
                }
            });
            DrawRoads(pixels, resolution, world);
            DrawSites(pixels, resolution, world);
            return pixels;
        }

        private static void DrawRoads(byte[] pixels, int resolution, WorldData world)
        {
            float scale = resolution / world.Size;
            foreach (WorldRoad road in world.Roads)
            {
                Vector3 colour = road.Class switch
                {
                    RoadClass.Highway => new Vector3(0.95f, 0.88f, 0.72f),
                    RoadClass.Track => new Vector3(0.82f, 0.7f, 0.52f),
                    _ => new Vector3(0.62f, 0.52f, 0.4f),
                };
                float radius = road.Class == RoadClass.Highway ? 1.1f : 0.7f;
                foreach (Vector3 point in road.Points)
                {
                    FillDisc(pixels, resolution, point.X * scale, resolution - point.Z * scale, radius, colour);
                }
            }
        }

        private static void DrawSites(byte[] pixels, int resolution, WorldData world)
        {
            float scale = resolution / world.Size;
            foreach (WorldSite site in world.Sites)
            {
                float radius = MathF.Max(site.Radius * scale * 0.5f, resolution / 240f);
                float x = site.Position.X * scale;
                float y = resolution - site.Position.Y * scale;
                FillDisc(pixels, resolution, x, y, radius + 1.2f, Vector3.Zero);
                FillDisc(pixels, resolution, x, y, radius, site.Definition.MapColour);
            }
        }

        private static void FillDisc(byte[] pixels, int resolution, float centreX, float centreY, float radius, Vector3 colour)
        {
            int minX = Math.Max((int)(centreX - radius), 0);
            int maxX = Math.Min((int)(centreX + radius) + 1, resolution - 1);
            int minY = Math.Max((int)(centreY - radius), 0);
            int maxY = Math.Min((int)(centreY + radius) + 1, resolution - 1);
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float dx = x + 0.5f - centreX;
                    float dy = y + 0.5f - centreY;
                    if (dx * dx + dy * dy > radius * radius) continue;
                    int i = (y * resolution + x) * 4;
                    pixels[i] = ToByte(colour.X);
                    pixels[i + 1] = ToByte(colour.Y);
                    pixels[i + 2] = ToByte(colour.Z);
                }
            }
        }

        private static byte ToByte(float value) => (byte)Math.Clamp((int)(value * 255f + 0.5f), 0, 255);
    }
}

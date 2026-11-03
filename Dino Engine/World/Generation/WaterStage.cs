using Dino_Engine.Physics;
using Dino_Engine.Rendering.Renderers.Geometry;
using OpenTK.Mathematics;

namespace Dino_Engine.World.Generation
{
    /// <summary>
    /// Fills the hollows of the finished ground with still water. Everything below sea level is already water; this finds the
    /// ponds and small lakes above it, with the engine's <see cref="WaterBasins"/>, since their shores follow the ground exactly.
    /// </summary>
    public sealed class WaterStage : IWorldStage
    {
        private const float SearchCell = 3f;
        private const float MinDepth = 0.3f;
        private const float MinArea = 28f;
        private const int MaxPonds = 26;

        public string Description => "Filling the lakes";
        public float Cost => 1f;

        public void Run(WorldData world)
        {
            ScalarField height = world.Height;
            List<WaterBasin> basins = WaterBasins.Find(
                p => height.Sample(p) + TerrainRenderer.SurfaceOffset,
                Vector2.Zero, new Vector2(world.Size), SearchCell, MinDepth, MinArea);

            ScalarField levels = ScalarField.ForWorld(world.Size, WorldData.CoarseCell);
            levels.Fill(WorldData.NoPond);

            foreach (WaterBasin basin in basins)
            {
                if (world.Ponds.Count >= MaxPonds) break;
                if (basin.Level <= WorldPlan.SeaLevel + 0.4f) continue;
                if (world.Reachable.Sample(basin.Center) < 0.5f) continue;
                if (world.Placement.Overlaps(Footprint.Box(basin.Center, basin.HalfExtents, 0f), PlacementLayer.Built | PlacementLayer.Road, 1f)) continue;

                world.Ponds.Add(new WorldPond(basin.Center, basin.HalfExtents, basin.Level, basin.Area, basin.MaxDepth));
                Rasterize(levels, basin);
            }
            world.PondLevel = levels;
        }

        private static void Rasterize(ScalarField levels, in WaterBasin basin)
        {
            int minX = Math.Max((int)MathF.Floor(basin.Min.X / levels.CellSize), 0);
            int maxX = Math.Min((int)MathF.Ceiling(basin.Max.X / levels.CellSize), levels.Width - 1);
            int minZ = Math.Max((int)MathF.Floor(basin.Min.Y / levels.CellSize), 0);
            int maxZ = Math.Min((int)MathF.Ceiling(basin.Max.Y / levels.CellSize), levels.Depth - 1);
            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    levels[x, z] = MathF.Max(levels[x, z], basin.Level);
                }
            }
        }
    }
}

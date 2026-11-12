using Dino_Engine.Physics;
using Dino_Engine.Rendering.Renderers.Geometry;
using OpenTK.Mathematics;
using static Dino_Engine.World.WorldMath;

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
            BuildShore(world, levels);
        }

        // A pond's level is kept over its whole rectangle, which is exact for depth, as nothing in it but the pond lies below the level. What a shore is, ground a little above the water,
        // can't be read from it, as gentle ground anywhere in the rectangle is a little above it, and the sand laid there stopped dead at its straight edge. So the shore reads the pond's
        // level near where water really lies, and the sea's further off, fading between them.
        private const float ShoreFull = 4f;     // Metres from a pond's water within which its shore reads its level alone,
        private const float ShoreGone = 16f;    // and beyond which the sea's.

        private static void BuildShore(WorldData world, ScalarField levels)
        {
            ScalarField shoreLevel = ScalarField.ForWorld(world.Size, WorldData.CoarseCell);
            shoreLevel.Fill(WorldData.NoPond);
            foreach (WorldPond pond in world.Ponds)
            {
                Vector2 reach = pond.HalfExtents + new Vector2(ShoreGone + WorldData.CoarseCell);
                int minX = Math.Max((int)MathF.Floor((pond.Centre.X - reach.X) / WorldData.CoarseCell), 0);
                int maxX = Math.Min((int)MathF.Ceiling((pond.Centre.X + reach.X) / WorldData.CoarseCell), shoreLevel.Width - 1);
                int minZ = Math.Max((int)MathF.Floor((pond.Centre.Y - reach.Y) / WorldData.CoarseCell), 0);
                int maxZ = Math.Min((int)MathF.Ceiling((pond.Centre.Y + reach.Y) / WorldData.CoarseCell), shoreLevel.Depth - 1);
                for (int z = minZ; z <= maxZ; z++)
                {
                    for (int x = minX; x <= maxX; x++) shoreLevel[x, z] = MathF.Max(shoreLevel[x, z], pond.Level);
                }
            }

            ScalarField height = world.Height;
            ScalarField distance = ScalarField.DistanceTo(levels.Width, levels.Depth, levels.CellSize, i =>
                levels.Data[i] > WorldData.NoPond && height.Sample(levels.WorldPosition(i % levels.Width, i / levels.Width)) < levels.Data[i]);
            ScalarField fade = ScalarField.ForWorld(world.Size, WorldData.CoarseCell);
            for (int i = 0; i < fade.Data.Length; i++) fade.Data[i] = 1f - Smoothstep(ShoreFull, ShoreGone, distance.Data[i]);
            world.ShoreLevel = shoreLevel;
            world.PondShore = fade;
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

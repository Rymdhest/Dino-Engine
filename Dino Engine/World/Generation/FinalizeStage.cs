using OpenTK.Mathematics;

namespace Dino_Engine.World.Generation
{
    /// <summary>
    /// Settles everything onto the finished ground: buildings and props take their height, walls step along the land, and the
    /// walkable ground is worked out, flooding out from the landing so that everything a character can stand on really is
    /// connected to it.
    /// </summary>
    public sealed class FinalizeStage : IWorldStage
    {
        private const float MaxSpawnShift = 16f;

        public string Description => "Waking the land";
        public float Cost => 1f;

        public void Run(WorldData world)
        {
            WorldObjects objects = world.Objects;
            foreach (PendingObject item in world.Pending)
            {
                float height = item.Height ?? world.Height.Sample(item.Position);
                Quaternion rotation = Quaternion.FromAxisAngle(Vector3.UnitY, item.Yaw);
                objects.Add(item.Asset, new Vector3(item.Position.X, height, item.Position.Y), rotation, item.Scale);
            }

            foreach (WallRun run in world.Walls) AddWall(world, objects, run);

            BuildWalkable(world);
            BuildPassable(world);
            RepairSpawns(world);
        }

        // A wall's model runs one metre along local +x from its origin, so each piece stretches from post to post, tilted to the slope.
        private static void AddWall(WorldData world, WorldObjects objects, in WallRun run)
        {
            Vector2 span = run.To - run.From;
            float length = span.Length;
            int pieces = Math.Max((int)MathF.Round(length / run.SegmentLength), 1);
            Vector2 direction = span / length;
            float yaw = MathF.Atan2(-direction.Y, direction.X);
            float piece = length / pieces;

            for (int i = 0; i < pieces; i++)
            {
                Vector2 from = run.From + direction * (piece * i);
                Vector2 to = from + direction * piece;
                float fromHeight = world.Height.Sample(from);
                float toHeight = world.Height.Sample(to);
                float rise = toHeight - fromHeight;
                float pitch = MathF.Atan2(rise, piece);

                Quaternion rotation = Quaternion.FromAxisAngle(Vector3.UnitY, yaw) * Quaternion.FromAxisAngle(Vector3.UnitZ, pitch);
                float stretched = MathF.Sqrt(piece * piece + rise * rise);
                objects.Add(run.Asset, new Vector3(from.X, fromHeight, from.Y), rotation, new Vector3(stretched, run.Scale, run.Scale));
            }
        }

        private static void BuildWalkable(WorldData world)
        {
            ByteField walkable = ByteField.ForWorld(world.Size, WorldData.WalkCell);
            int width = walkable.Width;
            int depth = walkable.Depth;
            bool[] open = new bool[width * depth];

            Parallel.For(0, depth, z =>
            {
                for (int x = 0; x < width; x++)
                {
                    Vector2 position = new Vector2(x, z) * WorldData.WalkCell;
                    open[z * width + x] = world.WaterDepthAt(position) <= WorldData.WadingDepth
                        && world.Height.SlopeAt(position) <= WorldData.WalkableSlope
                        && world.Boundary.Sample(position) < 0.6f
                        && world.Rock.Sample(position) < 0.5f;
                }
            });

            Vector2 landing = world.Sites[0].Position;
            int startX = Math.Clamp((int)(landing.X / WorldData.WalkCell), 0, width - 1);
            int startZ = Math.Clamp((int)(landing.Y / WorldData.WalkCell), 0, depth - 1);

            var stack = new Stack<int>();
            int start = startZ * width + startX;
            walkable.Data[start] = 255;
            stack.Push(start);
            while (stack.Count > 0)
            {
                int i = stack.Pop();
                int x = i % width;
                int z = i / width;
                if (x > 0) Visit(i - 1);
                if (x < width - 1) Visit(i + 1);
                if (z > 0) Visit(i - width);
                if (z < depth - 1) Visit(i + width);
            }

            void Visit(int next)
            {
                if (!open[next] || walkable.Data[next] != 0) return;
                walkable.Data[next] = 255;
                stack.Push(next);
            }

            world.Walkable = walkable;
        }

        // Where a character of ordinary width can really get to: walkable ground, around everything solid, flooded out from the
        // landing. Enemies path over this, and a spawn that ends up off it could never be reached by anyone.
        private static void BuildPassable(WorldData world)
        {
            ByteField passable = ByteField.ForWorld(world.Size, WorldData.PassCell);
            int width = passable.Width;
            int depth = passable.Depth;
            bool[] blocked = new bool[width * depth];

            Parallel.For(0, depth, z =>
            {
                for (int x = 0; x < width; x++)
                {
                    blocked[z * width + x] = !world.Walkable.IsSet(new Vector2(x, z) * WorldData.PassCell);
                }
            });

            float cell = WorldData.PassCell;
            world.Placement.ForEach((footprint, layer, solid) =>
            {
                if (!solid) return;
                float reach = footprint.BoundingRadius + WorldData.CharacterRadius;
                int minX = Math.Max((int)((footprint.Centre.X - reach) / cell), 0);
                int maxX = Math.Min((int)((footprint.Centre.X + reach) / cell) + 1, width - 1);
                int minZ = Math.Max((int)((footprint.Centre.Y - reach) / cell), 0);
                int maxZ = Math.Min((int)((footprint.Centre.Y + reach) / cell) + 1, depth - 1);
                for (int z = minZ; z <= maxZ; z++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        if (footprint.Overlaps(Footprint.Circle(new Vector2(x, z) * cell, WorldData.CharacterRadius))) blocked[z * width + x] = true;
                    }
                }
            });

            int start = NearestOpen(blocked, width, depth, world.Sites[0].Position, cell, 24);
            if (start >= 0)
            {
                var stack = new Stack<int>();
                passable.Data[start] = 255;
                stack.Push(start);
                while (stack.Count > 0)
                {
                    int i = stack.Pop();
                    int x = i % width;
                    int z = i / width;
                    if (x > 0) Visit(i - 1);
                    if (x < width - 1) Visit(i + 1);
                    if (z > 0) Visit(i - width);
                    if (z < depth - 1) Visit(i + width);
                }

                void Visit(int next)
                {
                    if (blocked[next] || passable.Data[next] != 0) return;
                    passable.Data[next] = 255;
                    stack.Push(next);
                }
            }
            world.Passable = passable;
        }

        private static int NearestOpen(bool[] blocked, int width, int depth, Vector2 position, float cell, int reach)
        {
            int centreX = (int)(position.X / cell);
            int centreZ = (int)(position.Y / cell);
            for (int radius = 0; radius <= reach; radius++)
            {
                for (int z = centreZ - radius; z <= centreZ + radius; z++)
                {
                    for (int x = centreX - radius; x <= centreX + radius; x++)
                    {
                        if (x < 0 || z < 0 || x >= width || z >= depth) continue;
                        if (!blocked[z * width + x]) return z * width + x;
                    }
                }
            }
            return -1;
        }

        // Layouts are drawn up before trees and boulders exist, so now and then something ends up behind them. Those spawns
        // move to the nearest ground anyone can actually reach, or go if there is none.
        private static void RepairSpawns(WorldData world)
        {
            ByteField passable = world.Passable;
            foreach (WorldSite site in world.Sites)
            {
                for (int i = site.Spawns.Count - 1; i >= 0; i--)
                {
                    SiteSpawn spawn = site.Spawns[i];
                    if (passable.IsSet(spawn.Position)) continue;

                    if (TryFindPassable(passable, spawn.Position, MaxSpawnShift, out Vector2 moved))
                    {
                        site.Spawns[i] = new SiteSpawn(spawn.Role, moved);
                    }
                    else
                    {
                        site.Spawns.RemoveAt(i);
                    }
                }
            }
        }

        private static bool TryFindPassable(ByteField passable, Vector2 around, float reach, out Vector2 found)
        {
            float cell = passable.CellSize;
            for (float radius = cell; radius <= reach; radius += cell)
            {
                int steps = Math.Max((int)(radius * 2f), 8);
                for (int i = 0; i < steps; i++)
                {
                    float angle = i * (MathF.Tau / steps);
                    Vector2 candidate = around + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                    if (!passable.IsSet(candidate)) continue;
                    found = candidate;
                    return true;
                }
            }
            found = around;
            return false;
        }
    }
}

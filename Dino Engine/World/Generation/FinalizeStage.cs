using OpenTK.Mathematics;

namespace Dino_Engine.World.Generation
{
    /// <summary>
    /// Settles everything onto the finished ground: buildings and props take their height, fences and walls stand along the land, and the
    /// walkable ground is worked out, flooding out from the landing so that everything a character can stand on really is
    /// connected to it, and from the floor of the arena sealed in a keep, which is only ever reached by its way down (ARENAS.md).
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

            FenceLayout.Lay(world.Fences, world.Height.Sample, objects.Add);

            BuildWalkable(world);
            BuildPassable(world);
            RepairSpawns(world);
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

            var stack = new Stack<int>();
            foreach (Vector2 from in FloodsFrom(world))
            {
                int startX = Math.Clamp((int)(from.X / WorldData.WalkCell), 0, width - 1);
                int startZ = Math.Clamp((int)(from.Y / WorldData.WalkCell), 0, depth - 1);
                int start = startZ * width + startX;
                if (walkable.Data[start] != 0) continue;
                walkable.Data[start] = 255;
                stack.Push(start);
            }
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

            var stack = new Stack<int>();
            foreach (Vector2 from in FloodsFrom(world))
            {
                int start = NearestOpen(blocked, width, depth, from, cell, 24);
                if (start < 0 || passable.Data[start] != 0) continue;
                passable.Data[start] = 255;
                stack.Push(start);
            }
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
            world.Passable = passable;
        }

        // Where the walkable ground is flooded out from: the landing, and the floor of any keep's arena, which is sealed from the land round it.
        private static IEnumerable<Vector2> FloodsFrom(WorldData world)
        {
            yield return world.Sites[0].Landing;
            foreach (WorldSite site in world.Sites)
            {
                if (site.Arena is { Keep: true } arena) yield return arena.Arrival;
            }
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

                    // Whoever watches from an arena's stands stands where nobody else can go, and is meant to.
                    if (spawn.Role == SpawnRole.Spectator) continue;

                    // A keep's arena is sealed: nobody is moved onto its floor from outside, or off it.
                    bool onFloor = site.Arena is { Keep: true } arena && arena.Contains(spawn.Position);
                    if (TryFindPassable(passable, spawn.Position, MaxSpawnShift, candidate => site.Arena is not { Keep: true } sealedIn || sealedIn.Contains(candidate) == onFloor, out Vector2 moved))
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

        private static bool TryFindPassable(ByteField passable, Vector2 around, float reach, Func<Vector2, bool> allowed, out Vector2 found)
        {
            float cell = passable.CellSize;
            for (float radius = cell; radius <= reach; radius += cell)
            {
                int steps = Math.Max((int)(radius * 2f), 8);
                for (int i = 0; i < steps; i++)
                {
                    float angle = i * (MathF.Tau / steps);
                    Vector2 candidate = around + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                    if (!passable.IsSet(candidate) || !allowed(candidate)) continue;
                    found = candidate;
                    return true;
                }
            }
            found = around;
            return false;
        }
    }
}

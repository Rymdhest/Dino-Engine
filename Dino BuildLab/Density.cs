using Dino_Defenders.Gameplay;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using Dino_Engine.World.Generation;
using OpenTK.Mathematics;

namespace Dino_BuildLab
{
    /// <summary>
    /// How much there is to fight in a world, and how far apart it stands. What a player feels is the time between fights, so the numbers that matter
    /// are the gaps along the way anyone walks, not the head count.
    /// </summary>
    internal static class Density
    {
        private const float Sight = 32f;            // Close enough to a band to be in a fight with it.
        private const float WalkStep = 4f;
        private const float WalkSpeed = 10f;        // HeroState.BaseMoveSpeed.

        private sealed class Tally : WorldProgress
        {
            public Tally(WorldData world, WorldRequest request) : base(world, request) { }
        }

        public static int Run(Options options)
        {
            int firstSeed = options.Int("seed", 1);
            int count = options.Int("count", ArchetypeCatalog.All.Count);
            int level = options.Int("level", 1);
            string? archetype = options.Text("archetype");

            Console.WriteLine("seed archetype     km2  hostiles  /km2  bands  first  gap50  gap90  gapmax  covered");
            var all = new List<(float perKm, float median, float p90, float covered, int hostiles)>();
            for (int i = 0; i < count; i++)
            {
                string id = archetype ?? ArchetypeCatalog.All[i % ArchetypeCatalog.All.Count].Id;
                var request = new WorldRequest(firstSeed + i, id, level, options.Int("people", 0) / 100f, options.Int("packs", 0) / 100f);
                WorldData world = new WorldGenerator().Generate(request);
                var tally = new Tally(world, request);

                var hostiles = new List<Vector2>();
                int bands = 0;
                foreach (SiteState state in tally.Sites)
                {
                    if (state.HostilesTotal > 0) bands++;
                    foreach (SiteSpawn spawn in state.Site.Spawns)
                    {
                        if (spawn.Role.IsHostile()) hostiles.Add(spawn.Position);
                    }
                }

                var near = new Near(hostiles, world.Size);
                float area = world.ReachableArea / 1_000_000f;
                List<float> gaps = RoadGaps(world, near);
                gaps.Sort();
                float median = Percentile(gaps, 0.5f);
                float p90 = Percentile(gaps, 0.9f);
                float longest = gaps.Count > 0 ? gaps[^1] : 0f;
                float first = hostiles.Count > 0 ? hostiles.Min(h => (h - world.Sites[0].Position).Length) : 0f;
                float covered = Covered(world, near);

                Console.WriteLine($"{firstSeed + i,4} {id,-11} {area,5:0.0} {hostiles.Count,8} {hostiles.Count / area,6:0} {bands,5} {first,5:0}m {median / WalkSpeed,4:0.0}s {p90 / WalkSpeed,5:0.0}s {longest / WalkSpeed,6:0.0}s {covered,6:0%}");
                all.Add((hostiles.Count / area, median, p90, covered, hostiles.Count));
            }

            Console.WriteLine($"\nOn average {all.Average(a => a.hostiles):0} hostiles, {all.Average(a => a.perKm):0} per km2; between fights on the roads {all.Average(a => a.median) / WalkSpeed:0.0} s, one in ten over {all.Average(a => a.p90) / WalkSpeed:0.0} s; {all.Average(a => a.covered):0%} of the land is near something.");
            return 0;
        }

        // Metres walked along each road with nothing in sight, one entry for each such stretch.
        private static List<float> RoadGaps(WorldData world, Near near)
        {
            var gaps = new List<float>();
            foreach (WorldRoad road in world.Roads)
            {
                float bare = 0f;
                float walked = 0f;
                for (int i = 1; i < road.Points.Count; i++)
                {
                    Vector2 from = road.Points[i - 1].Xz;
                    Vector2 to = road.Points[i].Xz;
                    walked += (to - from).Length;
                    if (walked < WalkStep) continue;
                    if (near.Within(to, Sight))
                    {
                        if (bare > 0f) gaps.Add(bare);
                        bare = 0f;
                    }
                    else bare += walked;
                    walked = 0f;
                }
                if (bare > 0f) gaps.Add(bare);
            }
            return gaps;
        }

        // The share of the land a hero wandering anywhere would find a fight on.
        private static float Covered(WorldData world, Near near)
        {
            int land = 0;
            int nearby = 0;
            for (float z = 10f; z < world.Size; z += 20f)
            {
                for (float x = 10f; x < world.Size; x += 20f)
                {
                    var at = new Vector2(x, z);
                    if (!world.Passable.IsSet(at)) continue;
                    land++;
                    if (near.Within(at, Sight * 1.5f)) nearby++;
                }
            }
            return land > 0 ? (float)nearby / land : 0f;
        }

        private static float Percentile(List<float> sorted, float share) => sorted.Count == 0 ? 0f : sorted[Math.Min((int)(sorted.Count * share), sorted.Count - 1)];

        private sealed class Near
        {
            private const float Cell = 32f;
            private readonly int _columns;
            private readonly List<Vector2>?[] _cells;

            public Near(List<Vector2> points, float size)
            {
                _columns = (int)(size / Cell) + 2;
                _cells = new List<Vector2>?[_columns * _columns];
                foreach (Vector2 point in points) (_cells[Index(point)] ??= new List<Vector2>()).Add(point);
            }

            private int Index(Vector2 point) => Math.Clamp((int)(point.Y / Cell), 0, _columns - 1) * _columns + Math.Clamp((int)(point.X / Cell), 0, _columns - 1);

            public bool Within(Vector2 at, float reach)
            {
                int spread = (int)(reach / Cell) + 1;
                int centreX = (int)(at.X / Cell);
                int centreZ = (int)(at.Y / Cell);
                for (int z = Math.Max(centreZ - spread, 0); z <= Math.Min(centreZ + spread, _columns - 1); z++)
                {
                    for (int x = Math.Max(centreX - spread, 0); x <= Math.Min(centreX + spread, _columns - 1); x++)
                    {
                        if (_cells[z * _columns + x] is not List<Vector2> cell) continue;
                        foreach (Vector2 point in cell)
                        {
                            if ((point - at).LengthSquared <= reach * reach) return true;
                        }
                    }
                }
                return false;
            }
        }
    }
}

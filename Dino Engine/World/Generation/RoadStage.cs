using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.World.WorldMath;

namespace Dino_Engine.World.Generation
{
    /// <summary>
    /// Lays the roads. Each one is a least-cost path after Galin et al. (2010): the search moves in long steps of many
    /// directions (their k-neighbourhood masks), so a road can hold a shallow line across a hillside, and the cost of a step
    /// depends on the grade along it rather than the steepness of the ground, which is what makes roads contour around hills
    /// instead of charging straight up them. Existing road costs a fraction of new ground (Emilien et al.'s road re-use),
    /// so roads merge into junctions by themselves, and a road that runs along another is cut down to the part that is new.
    /// Major places are joined first, with a few extra links wherever the way round would be far longer than the way across.
    /// </summary>
    public sealed class RoadStage : IWorldStage
    {
        private const float Cell = 8f;
        private const int MaskRadius = 3;
        private const float ReuseCost = 0.18f;
        private const float MaxGrade = 0.17f;       // Steepest grade a road holds along its length.
        private const float ComfortGrade = 0.06f;
        private const float SampleSpacing = 3f;
        private const float JunctionLanding = 12f;  // Metres of level road before a junction.
        private const float DetourLimit = 1.75f;    // Worst ratio of road distance to straight distance between villages.

        private ScalarField _height = null!;
        private float[] _groundCost = null!;
        private int[] _roadAt = null!;      // Index + 1 of the road passing each cell, 0 if none.
        // Sites already on the network. They count as destinations too, or the first road would have nothing to join.
        private readonly Dictionary<int, Vector2> _anchors = new();
        private int _width;
        private int _depth;
        private (int x, int z, float length)[] _mask = null!;

        public string Description => "Laying the roads";
        public float Cost => 2f;

        public void Run(WorldData world)
        {
            PrepareGrid(world);
            var random = world.Random.Fork("roads");

            var connected = new List<WorldSite>();
            var waiting = new List<WorldSite>();
            foreach (WorldSite site in world.Sites)
            {
                if (site.Kind is SiteKind.Landing or SiteKind.Haven) connected.Add(site);
                else waiting.Add(site);
            }
            connected[0].OnRoad = true;
            _anchors[CellAt(connected[0].Position)] = connected[0].Position;

            // Tier by tier, nearest first: the main road comes into being before anything hangs off it.
            ConnectTier(world, connected, waiting, RoadClass.Highway, random);
            ConnectTier(world, connected, waiting, RoadClass.Track, random);
            ConnectTier(world, connected, waiting, RoadClass.Footpath, random);

            AddLoops(world, connected);

            foreach (WorldSite site in world.Sites)
            {
                if (!site.OnRoad) continue;
                site.RoadDirection = DirectionToRoad(world, site);
            }
        }

        private void PrepareGrid(WorldData world)
        {
            _width = (int)MathF.Ceiling(world.Size / Cell) + 1;
            _depth = _width;
            _height = new ScalarField(_width, _depth, Cell);
            _groundCost = new float[_width * _depth];
            _roadAt = new int[_width * _depth];

            ScalarField elevation = world.Elevation;
            var wander = new WorldNoise(world.Random.Fork("road wander"));
            ScalarField reachable = world.Reachable;

            Parallel.For(0, _depth, z =>
            {
                for (int x = 0; x < _width; x++)
                {
                    int i = z * _width + x;
                    float worldX = x * Cell;
                    float worldZ = z * Cell;
                    _height.Data[i] = elevation.Sample(worldX, worldZ);

                    if (reachable.Sample(worldX, worldZ) < 0.5f)
                    {
                        _groundCost[i] = float.PositiveInfinity;
                        continue;
                    }

                    float slope = elevation.SlopeAt(new Vector2(worldX, worldZ));
                    float cut = 1f + 5f * Smoothstep(0.14f, 0.5f, slope);
                    float shore = 1f + 2.5f * (1f - Smoothstep(6f, 30f, world.Inland.Sample(worldX, worldZ)));
                    // Uneven cost leaves room for a road to wander, as if around things long gone.
                    float bends = 1f + 0.8f * wander.Fbm01(worldX, worldZ, 95f, 3);
                    _groundCost[i] = cut * shore * bends;
                }
            });

            var mask = new List<(int, int, float)>();
            for (int z = -MaskRadius; z <= MaskRadius; z++)
            {
                for (int x = -MaskRadius; x <= MaskRadius; x++)
                {
                    if (x == 0 && z == 0) continue;
                    if (GreatestCommonDivisor(Math.Abs(x), Math.Abs(z)) != 1) continue;
                    mask.Add((x, z, MathF.Sqrt(x * x + z * z) * Cell));
                }
            }
            _mask = mask.ToArray();
        }

        private static int GreatestCommonDivisor(int a, int b)
        {
            while (b != 0) (a, b) = (b, a % b);
            return a;
        }

        private void ConnectTier(WorldData world, List<WorldSite> connected, List<WorldSite> waiting, RoadClass tier, WorldRandom random)
        {
            while (true)
            {
                WorldSite? next = null;
                float nearest = float.PositiveInfinity;
                foreach (WorldSite site in waiting)
                {
                    if (site.Definition.Road != tier) continue;
                    foreach (WorldSite other in connected)
                    {
                        float distance = (site.Position - other.Position).LengthSquared;
                        if (distance < nearest)
                        {
                            nearest = distance;
                            next = site;
                        }
                    }
                }
                if (next == null) return;

                waiting.Remove(next);
                // Some places stay off the road, to be found by leaving it.
                if (!random.Fork(next.Id).Chance(next.Definition.RoadChance)) continue;

                List<Vector2>? path = FindPath(next.Position, null);
                if (path == null) continue;

                AddRoads(world, path, tier);
                next.OnRoad = true;
                connected.Add(next);
                _anchors[CellAt(next.Position)] = next.Position;
            }
        }

        // Extra roads where villages are near each other but far apart by road, so that the network has loops.
        private void AddLoops(WorldData world, List<WorldSite> connected)
        {
            var hubs = new List<WorldSite>();
            foreach (WorldSite site in connected)
            {
                if (site.Definition.Road == RoadClass.Highway) hubs.Add(site);
            }

            for (int a = 0; a < hubs.Count; a++)
            {
                for (int b = a + 1; b < hubs.Count; b++)
                {
                    float direct = (hubs[a].Position - hubs[b].Position).Length;
                    if (direct > world.Size * 0.5f) continue;

                    // On existing road the search is nearly free, so its cost is close to what travelling the network takes.
                    List<Vector2>? existing = FindPath(hubs[a].Position, hubs[b].Position, roadsOnly: true);
                    if (existing != null && PathLength(existing) < direct * DetourLimit) continue;

                    List<Vector2>? link = FindPath(hubs[a].Position, hubs[b].Position);
                    if (link != null) AddRoads(world, link, RoadClass.Track);
                }
            }
        }

        private static float PathLength(List<Vector2> path)
        {
            float length = 0f;
            for (int i = 1; i < path.Count; i++) length += (path[i] - path[i - 1]).Length;
            return length;
        }

        /// <summary>A* from <paramref name="start"/> to <paramref name="target"/>, or to the nearest road or joined site if there is no target.</summary>
        private List<Vector2>? FindPath(Vector2 start, Vector2? target, bool roadsOnly = false)
        {
            int startCell = CellAt(start);
            int goalCell = target.HasValue ? CellAt(target.Value) : -1;
            int goalX = goalCell % _width;
            int goalZ = goalCell / _width;

            float[] cost = new float[_width * _depth];
            int[] previous = new int[_width * _depth];
            Array.Fill(cost, float.PositiveInfinity);
            Array.Fill(previous, -1);
            cost[startCell] = 0f;

            var frontier = new PriorityQueue<int, float>();
            frontier.Enqueue(startCell, 0f);

            int reached = -1;
            while (frontier.TryDequeue(out int current, out float _))
            {
                bool arrived = goalCell >= 0
                    ? current == goalCell
                    : current != startCell && (_roadAt[current] != 0 || _anchors.ContainsKey(current));
                if (arrived)
                {
                    reached = current;
                    break;
                }

                int x = current % _width;
                int z = current / _width;
                float here = cost[current];
                for (int m = 0; m < _mask.Length; m++)
                {
                    (int dx, int dz, float length) = _mask[m];
                    int nx = x + dx;
                    int nz = z + dz;
                    if (nx < 0 || nz < 0 || nx >= _width || nz >= _depth) continue;
                    int neighbour = nz * _width + nx;

                    float stepCost = StepCost(x, z, dx, dz, length, roadsOnly);
                    if (float.IsPositiveInfinity(stepCost)) continue;

                    float total = here + stepCost;
                    if (total >= cost[neighbour]) continue;
                    cost[neighbour] = total;
                    previous[neighbour] = current;

                    float heuristic = 0f;
                    if (goalCell >= 0)
                    {
                        float ox = (goalX - nx) * Cell;
                        float oz = (goalZ - nz) * Cell;
                        heuristic = MathF.Sqrt(ox * ox + oz * oz) * ReuseCost;
                    }
                    frontier.Enqueue(neighbour, total + heuristic);
                }
            }

            if (reached < 0) return null;

            var path = new List<Vector2>();
            for (int cell = reached; cell >= 0; cell = previous[cell])
            {
                path.Add(new Vector2(cell % _width, cell / _width) * Cell);
            }
            path.Reverse();
            // The search works on grid nodes; the real ends of the road are the sites themselves.
            path[0] = start;
            if (target.HasValue) path[^1] = target.Value;
            else if (_anchors.TryGetValue(reached, out Vector2 anchor)) path[^1] = anchor;
            return path;
        }

        private float StepCost(int x, int z, int dx, int dz, float length, bool roadsOnly)
        {
            int steps = Math.Max(Math.Abs(dx), Math.Abs(dz));
            float ground = 0f;
            bool onRoad = true;
            for (int s = 1; s <= steps; s++)
            {
                // The cells the step passes over, so a long step can't jump a cliff or a stretch of water.
                int cx = x + dx * s / steps;
                int cz = z + dz * s / steps;
                int cell = cz * _width + cx;
                float value = _groundCost[cell];
                if (float.IsPositiveInfinity(value) && _roadAt[cell] == 0) return float.PositiveInfinity;
                ground += value;
                onRoad &= _roadAt[cell] != 0;
            }
            if (roadsOnly && !onRoad) return float.PositiveInfinity;
            if (onRoad) return length * ReuseCost;
            ground /= steps;

            float rise = MathF.Abs(_height[x + dx, z + dz] - _height[x, z]);
            float grade = rise / length;
            if (grade > MaxGrade * 1.9f) return float.PositiveInfinity;
            float steepness = grade / ComfortGrade;
            return length * ground * (1f + 0.7f * steepness * steepness);
        }

        private int CellAt(Vector2 position)
        {
            int x = Math.Clamp((int)MathF.Round(position.X / Cell), 0, _width - 1);
            int z = Math.Clamp((int)MathF.Round(position.Y / Cell), 0, _depth - 1);
            return z * _width + x;
        }

        // Keeps the stretches of the path that aren't road yet, each ending on the road it joins.
        private void AddRoads(WorldData world, List<Vector2> path, RoadClass roadClass)
        {
            var stretch = new List<Vector2>();
            float? startHeight = null;
            for (int i = 0; i < path.Count; i++)
            {
                bool existing = _roadAt[CellAt(path[i])] != 0;
                if (!existing)
                {
                    if (stretch.Count == 0 && i > 0)
                    {
                        Vector3 junction = SnapToRoad(world, path[i - 1]);
                        stretch.Add(junction.Xz);
                        startHeight = junction.Y;
                    }
                    stretch.Add(path[i]);
                    continue;
                }

                if (stretch.Count > 0)
                {
                    Vector3 junction = SnapToRoad(world, path[i]);
                    stretch.Add(junction.Xz);
                    FinishRoad(world, stretch, roadClass, startHeight, junction.Y);
                    stretch = new List<Vector2>();
                    startHeight = null;
                }
            }
            if (stretch.Count >= 2) FinishRoad(world, stretch, roadClass, startHeight, null);
        }

        // The nearest point of the road network: where a new road ends, so that junctions meet exactly, in height too.
        private static Vector3 SnapToRoad(WorldData world, Vector2 position)
        {
            float best = float.PositiveInfinity;
            Vector3 snapped = new Vector3(position.X, world.Elevation.Sample(position), position.Y);
            foreach (WorldRoad road in world.Roads)
            {
                foreach (Vector3 point in road.Points)
                {
                    float distance = (point.Xz - position).LengthSquared;
                    if (distance < best)
                    {
                        best = distance;
                        snapped = point;
                    }
                }
            }
            return snapped;
        }

        private void FinishRoad(WorldData world, List<Vector2> corners, RoadClass roadClass, float? startHeight, float? endHeight)
        {
            WorldRoad? road = BuildRoad(world, corners, roadClass, startHeight, endHeight);
            if (road == null) return;
            world.Roads.Add(road);
            MarkCells(road, world.Roads.Count);
        }

        /// <summary>
        /// A finished road through <paramref name="corners"/>: rounded off, evenly sampled and with its height profile set.
        /// Where it starts or ends on another road, the heights given there make the two meet.
        /// </summary>
        internal static WorldRoad? BuildRoad(WorldData world, List<Vector2> corners, RoadClass roadClass, float? startHeight = null, float? endHeight = null)
        {
            if (corners.Count < 2) return null;

            List<Vector2> smooth = Chaikin(Chaikin(Chaikin(corners)));
            List<Vector2> even = Resample(smooth, SampleSpacing);
            if (even.Count < 2) return null;

            var road = new WorldRoad { Class = roadClass };
            float[] heights = ProfileHeights(world, even, startHeight, endHeight);
            for (int i = 0; i < even.Count; i++)
            {
                road.Points.Add(new Vector3(even[i].X, heights[i], even[i].Y));
            }
            return road;
        }

        // Corner cutting, which rounds the search's straight steps into curves. The ends stay where they are.
        private static List<Vector2> Chaikin(List<Vector2> points)
        {
            if (points.Count < 3) return points;
            var result = new List<Vector2>(points.Count * 2) { points[0] };
            for (int i = 0; i < points.Count - 1; i++)
            {
                result.Add(Vector2.Lerp(points[i], points[i + 1], 0.25f));
                result.Add(Vector2.Lerp(points[i], points[i + 1], 0.75f));
            }
            result.Add(points[^1]);
            return result;
        }

        // Points exactly evenly spaced from end to end. The grade limit counts on every step being the same length.
        private static List<Vector2> Resample(List<Vector2> points, float spacing)
        {
            float total = 0f;
            for (int i = 1; i < points.Count; i++) total += (points[i] - points[i - 1]).Length;
            int steps = Math.Max((int)MathF.Round(total / spacing), 1);
            float step = total / steps;

            var result = new List<Vector2>(steps + 1) { points[0] };
            float target = step;
            float walked = 0f;
            for (int i = 1; i < points.Count && result.Count < steps; i++)
            {
                Vector2 from = points[i - 1];
                Vector2 to = points[i];
                float length = (to - from).Length;
                while (target <= walked + length && result.Count < steps)
                {
                    result.Add(Vector2.Lerp(from, to, (target - walked) / length));
                    target += step;
                }
                walked += length;
            }
            result.Add(points[^1]);
            return result;
        }

        // Follows the land, but with its bumps averaged away and no stretch steeper than the road can take.
        private static float[] ProfileHeights(WorldData world, List<Vector2> points, float? startHeight, float? endHeight)
        {
            int count = points.Count;
            float[] heights = new float[count];
            for (int i = 0; i < count; i++)
            {
                heights[i] = MathF.Max(world.Elevation.Sample(points[i]), WorldPlan.SeaLevel + 0.5f);
            }

            float first = heights[0];
            float last = heights[^1];
            float[] smoothed = new float[count];
            const int Window = 7;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < count; i++)
                {
                    float total = 0f;
                    int samples = 0;
                    for (int j = Math.Max(i - Window, 0); j <= Math.Min(i + Window, count - 1); j++)
                    {
                        total += heights[j];
                        samples++;
                    }
                    smoothed[i] = total / samples;
                }
                Array.Copy(smoothed, heights, count);
            }
            heights[0] = startHeight ?? first;
            heights[^1] = endHeight ?? last;

            // A road joining another levels out for its last few metres, or it would meet it with a lip at the kerb.
            int landing = Math.Min((int)MathF.Ceiling(JunctionLanding / SampleSpacing), (count - 1) / 3);
            int from = startHeight.HasValue ? landing : 0;
            int to = endHeight.HasValue ? count - 1 - landing : count - 1;
            for (int i = 0; i < from; i++) heights[i] = heights[0];
            for (int i = to + 1; i < count; i++) heights[i] = heights[^1];
            if (from > 0) heights[from] = heights[0];
            if (to < count - 1) heights[to] = heights[^1];

            LimitGrade(heights.AsSpan(from, to - from + 1), MaxGrade * (points[1] - points[0]).Length);
            return heights;
        }

        // Makes every step at most maxRise while leaving both ends where they are. From each end, a height can only be reached
        // if it lies within maxRise per step of that end, so those two cones bound every point; a point inside both can always
        // be joined up to its neighbours. Ends too far apart for the limit to hold are joined by a straight ramp.
        private static void LimitGrade(Span<float> heights, float maxRise)
        {
            int last = heights.Length - 1;
            float start = heights[0];
            float end = heights[last];

            if (MathF.Abs(end - start) > maxRise * last)
            {
                for (int i = 1; i < last; i++) heights[i] = Lerp(start, end, (float)i / last);
                return;
            }

            for (int i = 1; i < last; i++)
            {
                float lowest = MathF.Max(start - i * maxRise, end - (last - i) * maxRise);
                float highest = MathF.Min(start + i * maxRise, end + (last - i) * maxRise);
                heights[i] = Math.Clamp(heights[i], lowest, highest);
            }
            for (int i = 1; i < last; i++) heights[i] = Math.Clamp(heights[i], heights[i - 1] - maxRise, heights[i - 1] + maxRise);
            for (int i = last - 1; i > 0; i--) heights[i] = Math.Clamp(heights[i], heights[i + 1] - maxRise, heights[i + 1] + maxRise);
        }

        private void MarkCells(WorldRoad road, int roadNumber)
        {
            foreach (Vector3 point in road.Points)
            {
                _roadAt[CellAt(point.Xz)] = roadNumber;
            }
        }

        private static Vector2 DirectionToRoad(WorldData world, WorldSite site)
        {
            // Where the road crosses the edge of the site is where it comes in from.
            float best = float.PositiveInfinity;
            Vector2 direction = Vector2.UnitX;
            foreach (WorldRoad road in world.Roads)
            {
                foreach (Vector3 point in road.Points)
                {
                    Vector2 offset = point.Xz - site.Position;
                    float length = offset.Length;
                    float miss = MathF.Abs(length - site.Radius);
                    if (miss < best && length > 1f)
                    {
                        best = miss;
                        direction = offset / length;
                    }
                }
            }
            return direction;
        }
    }
}

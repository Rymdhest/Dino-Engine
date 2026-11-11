using Dino_Defenders.Progression;
using Dino_Defenders.Stats;
using OpenTK.Mathematics;
using System.Runtime.CompilerServices;

namespace Dino_Defenders.Talents
{
    public enum TalentKind : byte
    {
        Root,
        Small,
        Notable,
        Keystone,
        Gate,
    }

    public sealed class TalentNode
    {
        public int Index;
        public string Id = "";
        public string Name = "";
        public TalentKind Kind;
        public int Region = -1;
        public Vector2 Position;
        public Mod[] Mods = Array.Empty<Mod>();
        public string? Lore;
        public string? Title;       // What having it makes you, for a keystone with an opinion on the matter.
        public int Twin = -1;       // For a waygate: the one it opens onto.
        public readonly List<int> Links = new();

        /// <summary>Only there to get somewhere: it gives a little of the land's attribute.</summary>
        public bool Travel;

        /// <summary>On one of the roads, whose nodes are far apart.</summary>
        public bool Highway;

        /// <summary>One of the six notables that the ways out of the middle open with.</summary>
        public bool Beginning;

        /// <summary>The notable a region opens with, where its way out of the middle forks.</summary>
        public bool Entry;

        /// <summary>The circle it sits on, if it's part of a ring: two linked nodes on the same circle are joined along it rather than straight.</summary>
        public Vector2 Orbit;
        public float OrbitRadius;

        /// <summary>A keystone's own picture.</summary>
        public TalentGlyph Emblem;

        /// <summary>What it looks like, worked out from what it gives.</summary>
        public TalentIcon Icon;
    }

    /// <summary>
    /// The one tree every hero grows through. There are no classes: where someone spends their points is what they are. It's grown, not drawn: a ring
    /// around the root opens onto every region, each region branches out in lanes with clusters curled off them, wheels of rarer things sit between
    /// them, two roads circle the whole of it with spokes in to the middle, and keystones and waygates stand where their ideas live.
    ///
    /// Everything is worked out from <see cref="TalentContent"/> the same way every time, with no randomness but a hash of names, so the same content is
    /// always the same tree. Node ids are saved in heroes and come from names: a lane's nodes from the lane and how far along it they are, a cluster's
    /// from the cluster. <see cref="Sanitize"/> looks after anything that goes missing, so a change costs someone a few points back, never their hero.
    /// </summary>
    public static class TalentTree
    {
        /// <summary>The least two nodes keep between them, which is how far apart a cluster's own are.</summary>
        public const float Spacing = 100f;

        /// <summary>Between travel nodes, on a lane or any path that's only there to get somewhere. They give the least, so there are fewer of them.</summary>
        public const float LaneSpacing = 165f;

        /// <summary>Between nodes on a road, which is the point of a road.</summary>
        public const float RoadSpacing = 420f;

        /// <summary>How far out each way out of the middle has its travel node, its notable, and the fork into its two regions.</summary>
        public const float FirstStep = 170f;
        public const float StartNotable = 345f;
        public const float Fork = 520f;

        private const float PodReach = 205f;        // From the lane to a cluster's notable.
        private const float PodArc = 92f;           // How far its small nodes curl around it.
        private const float PodTurn = 46f;          // Degrees between them.
        private const float PodRoom = 205f;         // What two clusters' notables keep between them.
        private const float Clear = 52f;            // What any node keeps from any other while the tree grows.
        private const float RimRadius = 172f;
        private const float BranchBlend = 170f;     // How long a branch takes to settle at its own angle.
        private const float TrunkBlend = 380f;      // And a region's first lane, which leaves its way out of the middle at that way's angle.
        private const float BridgeSpacing = 130f;   // Between a bridge's nodes, which give something but are mostly a way across.
        private const float AttributeStep = 5f;     // What a travel node gives.

        private static readonly List<TalentNode> Built = new();
        private static readonly Dictionary<string, int> ById = new();
        private static readonly List<(TalentNode a, TalentNode b)> Segments = new();
        private static Space? _space = new();

        public static readonly TalentNode[] Nodes;
        public static readonly TalentNode Root;

        /// <summary>How many points it takes to get from the root to each node, walking.</summary>
        public static readonly int[] DepthOf;

        /// <summary>Where each road runs, for drawing them.</summary>
        public static IReadOnlyList<Road> Roads => TalentContent.Roads;

        static TalentTree()
        {
            Root = Add(new TalentNode { Id = "root", Name = "The Root", Kind = TalentKind.Root, Position = Vector2.Zero });
            new Grower().Grow();
            _space = null;
            Prune();
            Nodes = Built.ToArray();
            foreach (TalentNode node in Nodes) node.Icon = TalentIcons.Of(node);
            DepthOf = Depths();
        }

        // A path that goes nowhere ends on something worth going to. Small nodes left at the end of one, where a lane ran on past its last cluster, are
        // taken away back to where it meets something, and what's left is numbered again.
        private static void Prune()
        {
            var gone = new bool[Built.Count];
            var ends = new Stack<TalentNode>(Built.Where(n => n.Kind == TalentKind.Small && n.Links.Count == 1));
            while (ends.TryPop(out TalentNode? node))
            {
                if (gone[node.Index] || node.Kind != TalentKind.Small || node.Links.Count != 1) continue;
                gone[node.Index] = true;
                TalentNode other = Built[node.Links[0]];
                other.Links.Remove(node.Index);
                node.Links.Clear();
                ends.Push(other);
            }

            var map = new int[Built.Count];
            var kept = new List<TalentNode>();
            foreach (TalentNode node in Built)
            {
                map[node.Index] = gone[node.Index] ? -1 : kept.Count;
                if (!gone[node.Index]) kept.Add(node);
            }
            ById.Clear();
            foreach (TalentNode node in kept)
            {
                for (int i = 0; i < node.Links.Count; i++) node.Links[i] = map[node.Links[i]];
                if (node.Twin >= 0) node.Twin = map[node.Twin];
                node.Index = map[node.Index];
                ById.Add(node.Id, node.Index);
            }
            Built.Clear();
            Built.AddRange(kept);
        }

        private static TalentNode Add(TalentNode node)
        {
            node.Mods = Array.ConvertAll(node.Mods, StatPrecision.Tidy);
            node.Index = Built.Count;
            Built.Add(node);
            ById.Add(node.Id, node.Index);
            _space?.Put(node);
            return node;
        }

        private static void Link(TalentNode a, TalentNode b)
        {
            if (a == b || a.Links.Contains(b.Index)) return;
            a.Links.Add(b.Index);
            b.Links.Add(a.Index);
            Segments.Add((a, b));
            _space?.Put(a, b);
        }

        /// <summary>Where nodes and links are, in cells, so that growing the tree only ever looks at what's near.</summary>
        private sealed class Space
        {
            private const float Cell = 240f;
            private readonly Dictionary<long, List<TalentNode>> _nodes = new();
            private readonly Dictionary<long, List<Drawn>> _links = new();
            private int _query;
            private readonly List<Drawn> _found = new();
            private readonly List<TalentNode> _near = new();

            private static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;
            private static int C(float value) => (int)MathF.Floor(value / Cell);

            public void Put(TalentNode node)
            {
                long key = Key(C(node.Position.X), C(node.Position.Y));
                if (!_nodes.TryGetValue(key, out List<TalentNode>? list)) _nodes[key] = list = new();
                list.Add(node);
            }

            // With room around it for the bow of an arc, as a road's ends only find out they share a circle after they're joined.
            public void Put(TalentNode a, TalentNode b)
            {
                const float Bow = 60f;
                var drawn = new Drawn(a, b);
                for (int x = C(MathF.Min(a.Position.X, b.Position.X) - Bow); x <= C(MathF.Max(a.Position.X, b.Position.X) + Bow); x++)
                {
                    for (int y = C(MathF.Min(a.Position.Y, b.Position.Y) - Bow); y <= C(MathF.Max(a.Position.Y, b.Position.Y) + Bow); y++)
                    {
                        long key = Key(x, y);
                        if (!_links.TryGetValue(key, out var list)) _links[key] = list = new();
                        list.Add(drawn);
                    }
                }
            }

            /// <summary>Every node in the cells round a spot, in one list used again by every query, as nothing that reads one asks for another before it's done.</summary>
            [MethodImpl(MethodImplOptions.AggressiveOptimization)]
            public List<TalentNode> Near(Vector2 at, float reach)
            {
                List<TalentNode> found = _near;
                found.Clear();
                for (int x = C(at.X - reach); x <= C(at.X + reach); x++)
                {
                    for (int y = C(at.Y - reach); y <= C(at.Y + reach); y++)
                    {
                        if (_nodes.TryGetValue(Key(x, y), out List<TalentNode>? list)) found.AddRange(list);
                    }
                }
                return found;
            }

            /// <summary>Every link in the cells a box covers, each once, with its drawn shape up to date.</summary>
            [MethodImpl(MethodImplOptions.AggressiveOptimization)]
            public List<Drawn> Along(Vector2 from, Vector2 to)
            {
                // One list, used again by every query: nothing that reads one asks for another before it's done.
                List<Drawn> found = _found;
                found.Clear();
                int query = ++_query;
                for (int x = C(MathF.Min(from.X, to.X)); x <= C(MathF.Max(from.X, to.X)); x++)
                {
                    for (int y = C(MathF.Min(from.Y, to.Y)); y <= C(MathF.Max(from.Y, to.Y)); y++)
                    {
                        if (!_links.TryGetValue(Key(x, y), out var list)) continue;
                        foreach (Drawn link in list)
                        {
                            if (link.Seen == query) continue;
                            link.Seen = query;
                            link.Refresh();
                            found.Add(link);
                        }
                    }
                }
                return found;
            }
        }

        // Growing the tree is one long run of these few methods, on the main thread before the first frame, so they're compiled at their best from the
        // first call rather than starting slow and being compiled again.

        /// <summary>A link as it will be drawn, worked out once and again only if its ends find out later that they share a circle, as a road's do.</summary>
        private sealed class Drawn
        {
            public readonly TalentNode A, B;
            public Vector2[] Shape = Array.Empty<Vector2>();
            public Vector2 Min, Max;
            public int Seen;
            private bool _arced;
            private Vector2 _orbit;
            private float _radius;

            public Drawn(TalentNode a, TalentNode b)
            {
                A = a;
                B = b;
            }

            [MethodImpl(MethodImplOptions.AggressiveOptimization)]
            public void Refresh()
            {
                bool arced = Arcs(A, B);
                if (Shape.Length > 0 && arced == _arced && (!arced || (A.Orbit == _orbit && A.OrbitRadius == _radius))) return;
                _arced = arced;
                _orbit = A.Orbit;
                _radius = A.OrbitRadius;
                Shape = TalentTree.Shape(A, B);
                (Min, Max) = Bounds(Shape);
            }

            /// <summary>Whether a point is at least this far from this link's box, and so from the link.</summary>
            public bool Beyond(Vector2 at, float distance)
            {
                if (distance <= 0f) return true;
                float dx = MathF.Max(MathF.Max(Min.X - at.X, at.X - Max.X), 0f);
                float dy = MathF.Max(MathF.Max(Min.Y - at.Y, at.Y - Max.Y), 0f);
                return dx * dx + dy * dy >= distance * distance;
            }
        }

        public static Vector2 At(float radius, float degrees)
        {
            float angle = MathHelper.DegreesToRadians(degrees);
            return new Vector2(MathF.Sin(angle), -MathF.Cos(angle)) * radius;
        }

        public static float AngleOf(Vector2 at) => MathHelper.RadiansToDegrees(MathF.Atan2(at.X, -at.Y));

        /// <summary>The way from one angle to another, the short way round, in degrees.</summary>
        public static float Turn(float from, float to) => MathF.IEEERemainder(to - from, 360f);

        /// <summary>The region an angle falls in, by where each region's middle is.</summary>
        public static int RegionAt(float degrees)
        {
            int best = 0;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < TalentContent.Regions.Length; i++)
            {
                Region region = TalentContent.Regions[i];
                float off = MathF.Abs(Turn(region.Centre, degrees)) / MathF.Max(region.Width, 1f);
                if (off >= nearest) continue;
                nearest = off;
                best = i;
            }
            return best;
        }

        // A hash of a name that's the same on every machine and in every run, which string.GetHashCode isn't.
        private static uint Hash(string text)
        {
            uint hash = 2166136261u;
            foreach (char c in text)
            {
                hash ^= c;
                hash *= 16777619u;
            }
            hash ^= hash >> 15;
            hash *= 0x2c1b3c6du;
            hash ^= hash >> 12;
            return hash;
        }

        private static float Unit(string text) => (Hash(text) & 0xffffff) / (float)0x1000000;

        /// <summary>Everything that grows the tree, which only exists while it's growing.</summary>
        private sealed class Grower
        {
            private const float InnerQuiet = 560f;      // No cluster hangs nearer the root than this: the middle is for choosing a way, not stopping on it.
            private const float WheelRoom = RimRadius + Clear + 4f;
            private const float KeystoneRoom = 120f;
            private const float GateRoom = 100f;

            private readonly Dictionary<string, List<TalentNode>> _lanes = new();
            private readonly List<TalentNode> _paths = new();      // Lanes and roads: what anything else is joined onto.
            private readonly List<Vector2> _pods = new();
            private readonly List<(Vector2 at, float room)> _blocks = new();
            private readonly TalentNode[] _starts = new TalentNode[TalentContent.Regions.Length];
            private readonly int[] _way = Enumerable.Repeat(-1, TalentContent.Regions.Length).ToArray();      // Which way out of the middle each region opens off.
            private readonly float[] _rings;
            private readonly Dictionary<Wheel, Vector2> _wheels = new();     // Where each wheel stands, decided before anything else, so lanes go round them.

            public Grower()
            {
                _rings = TalentContent.Roads.Where(road => road.Ring).Select(road => road.Radius).OrderBy(r => r).ToArray();
            }

            private static IEnumerable<(Region region, int index)> Each() => TalentContent.Regions.Select((r, i) => (r, i));

            public void Grow()
            {
                foreach (Region region in TalentContent.Regions)
                {
                    foreach (Wheel wheel in region.Wheels) _wheels[wheel] = At(wheel.Radius, wheel.Angle);
                }
                GrowStarts();
                foreach ((Region region, int index) in Each())
                {
                    foreach (Lane lane in region.Lanes) GrowLane(region, index, lane);
                }
                RingRoads();
                SpokeRoads();
                foreach ((Region region, int index) in Each())
                {
                    foreach (Wheel wheel in region.Wheels) GrowWheel(region, index, wheel);
                }
                foreach ((Region region, int index) in Each())
                {
                    foreach (Keystone keystone in region.Keystones) GrowKeystone(region, index, keystone);
                }
                foreach (Gate gate in TalentContent.Gates) GrowGate(gate);
                foreach (Bridge bridge in TalentContent.Bridges) GrowBridge(bridge);
                CrossLinks();
                foreach ((Region region, int index) in Each())
                {
                    foreach (Lane lane in region.Lanes) HangPods(region, index, lane);
                }
                Attributes();
            }

            // ------------------------------------------------------------ the ways out of the middle

            // Six ways out of the root and nothing else, evenly round it: a travel node, a notable that says what kind of hero this is going to be, and a fork
            // into the two regions it opens onto. Nothing joins one way to the next on the way out, so where a hero starts is a choice that stays made.
            private void GrowStarts()
            {
                for (int way = 0; way < TalentContent.Beginnings.Length; way++)
                {
                    Beginning start = TalentContent.Beginnings[way];
                    int[] regions = start.Regions.Select(id => Array.FindIndex(TalentContent.Regions, r => r.Id == id)).ToArray();
                    int home = regions[0];
                    string name = TalentContent.Regions[home].Name;
                    var step = Add(new TalentNode { Id = $"start.{start.Id}.1", Name = name, Kind = TalentKind.Small, Region = home, Travel = true, Position = At(FirstStep, start.Angle) });
                    var notable = Add(new TalentNode
                    {
                        Id = $"start.{start.Id}", Name = start.Name, Kind = TalentKind.Notable, Region = home, Position = At(StartNotable, start.Angle), Mods = start.Mods, Lore = start.Lore, Beginning = true,
                    });
                    var fork = Add(new TalentNode { Id = $"start.{start.Id}.2", Name = name, Kind = TalentKind.Small, Region = home, Travel = true, Position = At(Fork, start.Angle) });
                    Link(Root, step);
                    Link(step, notable);
                    Link(notable, fork);
                    foreach (int region in regions)
                    {
                        _starts[region] = fork;
                        _way[region] = way;
                    }
                }
                int lost = Array.IndexOf(_way, -1);
                if (lost >= 0) throw new InvalidOperationException($"{TalentContent.Regions[lost].Name} opens off none of the ways out of the middle");
            }

            private static float Normal(float degrees) => (degrees % 360f + 360f) % 360f;

            // ------------------------------------------------------------ lanes

            private void GrowLane(Region region, int index, Lane lane)
            {
                TalentNode start;
                if (lane.From == null) start = _starts[index];
                else
                {
                    // A lane that leaves its parent near its end leaves from the end, so the two never run side by side.
                    List<TalentNode> parent = _lanes[$"{region.Id}.{lane.From}"];
                    start = parent.MinBy(n => MathF.Abs(n.Position.Length - lane.Start))!;
                    if (parent[^1].Position.Length - start.Position.Length < 160f) start = parent[^1];

                    // One that would leave just inside a road leaves from where its parent meets the road, or it would cross the road without stopping on it.
                    foreach (float ring in _rings)
                    {
                        float at = start.Position.Length;
                        if (ring <= at || ring > at + 60f) continue;
                        if (parent.FirstOrDefault(n => MathF.Abs(n.Position.Length - ring) < 0.5f) is TalentNode onRoad) start = onRoad;
                    }
                }

                float from = start.Position.Length;
                float startAngle = AngleOf(start.Position);
                string seed = $"{region.Id}.{lane.Id}";
                float AngleAt(float radius)
                {
                    float aim = region.Centre + lane.Angle + lane.Bend * (radius - from) / 1000f + Wiggle(seed, radius);
                    float settle = MathHelper.Clamp((radius - from) / (lane.From == null ? TrunkBlend : BranchBlend), 0f, 1f);
                    settle = settle * settle * (3f - 2f * settle);
                    float angle = Around(startAngle + Turn(startAngle, aim) * settle, radius, region.Centre + lane.Angle);
                    return radius > from + 250f ? Apart(angle, radius, seed) : angle;
                }

                // The roads this lane would cross, where they run at the angle it crosses them at.
                bool Crossed(Road road) => road.Ring && road.Radius > from + 60f && road.Covers(AngleAt(road.Radius));

                // A lane that would end just short of a road, or just past it, ends on it.
                float end = lane.End;
                float nearest = 90f;
                foreach (Road road in TalentContent.Roads.Where(Crossed))
                {
                    if (MathF.Abs(lane.End - road.Radius) >= nearest) continue;
                    nearest = MathF.Abs(lane.End - road.Radius);
                    end = road.Radius;
                }

                var stops = new List<float> { from };
                foreach (Road road in TalentContent.Roads.Where(Crossed).OrderBy(r => r.Radius))
                {
                    if (road.Radius < end - 60f) stops.Add(road.Radius);
                }
                stops.Add(end);

                var nodes = new List<TalentNode>();
                TalentNode previous = start;
                int k = 0;
                Pod? entry = lane.From == null ? region.Entry : null;
                for (int s = 1; s < stops.Count; s++)
                {
                    float a = stops[s - 1], b = stops[s];
                    int steps = Math.Max(1, (int)MathF.Round((b - a) / LaneSpacing));
                    for (int i = 1; i <= steps; i++)
                    {
                        float radius = a + (b - a) * i / steps;

                        // A region opens with a notable where its way forks, so that the fork is a choice between two. Nothing hangs off it or joins onto it.
                        if (entry != null)
                        {
                            var opening = Add(new TalentNode { Id = $"{region.Id}.{entry.Id}", Name = entry.Name, Kind = TalentKind.Notable, Region = index, Position = At(radius, AngleAt(radius)), Mods = entry.Mods, Entry = true });
                            Link(previous, opening);
                            previous = opening;
                            entry = null;
                            continue;
                        }

                        var node = Add(new TalentNode
                        {
                            Id = $"{seed}.{++k}", Name = region.Name, Kind = TalentKind.Small, Region = index, Travel = true, Position = At(radius, AngleAt(radius)),
                        });
                        Link(previous, node);
                        previous = node;
                        nodes.Add(node);
                        _paths.Add(node);
                    }
                }
                _lanes[seed] = nodes;
            }

            // A lane never runs quite straight.
            private static float Wiggle(string seed, float radius)
            {
                float a = Unit(seed + "a") * MathF.Tau;
                float b = Unit(seed + "b") * MathF.Tau;
                return 1.4f * MathF.Sin(radius / 430f + a) + 0.7f * MathF.Sin(radius / 190f + b);
            }

            // A lane that would pass through a wheel goes round it instead, on the side it was mostly on, so the wheel sits in the bend of it.
            private float Around(float angle, float radius, float heading)
            {
                const float Room = RimRadius + 88f;
                foreach (Vector2 centre in _wheels.Values)
                {
                    if ((At(radius, angle) - centre).Length >= Room) continue;
                    float side = Turn(AngleOf(centre), heading) >= 0f ? 1f : -1f;
                    for (int step = 0; step < 240 && (At(radius, angle) - centre).Length < Room; step++) angle += side * 0.25f;
                }
                return angle;
            }

            // A lane keeps clear of lanes already grown, sliding round whichever it comes near on the side it's on, so two never run into each other.
            private float Apart(float angle, float radius, string seed)
            {
                const float Room = 175f;
                for (int pass = 0; pass < 3; pass++)
                {
                    Vector2 at = At(radius, angle);
                    TalentNode? nearest = null;
                    float best = Room;
                    foreach (TalentNode node in _space!.Near(at, Room))
                    {
                        if (!node.Travel || node.Highway || node.Id.StartsWith(seed + ".")) continue;
                        float distance = (node.Position - at).Length;
                        if (distance >= best) continue;
                        best = distance;
                        nearest = node;
                    }
                    if (nearest == null) break;
                    float side = Turn(AngleOf(nearest.Position), angle) >= 0f ? 1f : -1f;
                    for (int step = 0; step < 160 && (At(radius, angle) - nearest.Position).Length < Room; step++) angle += side * 0.25f;
                }
                return angle;
            }

            // ------------------------------------------------------------ roads

            // A ring road passes through every lane that crosses it, and every spoke, and has its own nodes far apart between them.
            private void RingRoads()
            {
                foreach (Road road in TalentContent.Roads.Where(r => r.Ring))
                {
                    var stops = new List<(float angle, TalentNode node)>();
                    foreach (TalentNode node in _paths)
                    {
                        if (MathF.Abs(node.Position.Length - road.Radius) < 0.5f && road.Covers(AngleOf(node.Position))) stops.Add((road.Whole ? Normal(AngleOf(node.Position)) : road.Along(AngleOf(node.Position)), node));
                    }
                    foreach (Road spoke in TalentContent.Roads.Where(r => !r.Ring && r.From < road.Radius && r.To > road.Radius && road.Covers(r.Angle)))
                    {
                        var node = Add(new TalentNode
                        {
                            Id = $"way.{spoke.Id}.{road.Id}", Name = spoke.Name, Kind = TalentKind.Small, Region = RegionAt(spoke.Angle), Travel = true, Highway = true,
                            Position = At(road.Radius, spoke.Angle),
                        });
                        stops.Add((road.Whole ? Normal(spoke.Angle) : road.Along(spoke.Angle), node));
                        _paths.Add(node);
                    }
                    stops.Sort((a, b) => a.angle.CompareTo(b.angle));

                    // Part of the way round, it starts and ends where it meets its first and last lane.
                    int count = 0;
                    for (int i = 0; i < stops.Count; i++)
                    {
                        (float angle, TalentNode node) = stops[i];
                        node.Highway = true;
                        node.Orbit = Vector2.Zero;
                        node.OrbitRadius = road.Radius;
                        if (!road.Whole && i == stops.Count - 1) break;

                        (float nextAngle, TalentNode next) = stops[(i + 1) % stops.Count];
                        float gap = road.Whole ? Normal(nextAngle - angle) : nextAngle - angle;
                        if (stops.Count == 1) gap = 360f;
                        if (!road.Whole) angle += road.ArcFrom;

                        float length = MathHelper.DegreesToRadians(gap) * road.Radius;
                        int between = Math.Max(0, (int)MathF.Round(length / RoadSpacing) - 1);
                        TalentNode previous = node;
                        for (int j = 1; j <= between; j++)
                        {
                            float at = angle + gap * j / (between + 1);
                            var station = Add(new TalentNode
                            {
                                Id = $"way.{road.Id}.{++count}", Name = road.Name, Kind = TalentKind.Small, Region = RegionAt(at), Travel = true, Highway = true,
                                Position = At(road.Radius, at), Orbit = Vector2.Zero, OrbitRadius = road.Radius,
                            });
                            Link(previous, station);
                            previous = station;
                            _paths.Add(station);
                        }
                        Link(previous, next);
                    }
                }
            }

            // A spoke runs straight out, through the ring roads it crosses, with its nodes far apart.
            private void SpokeRoads()
            {
                foreach (Road road in TalentContent.Roads.Where(r => !r.Ring))
                {
                    var stops = new List<(float radius, TalentNode node)>();
                    if (Find($"way.{road.Id}.0") is TalentNode first) stops.Add((first.Position.Length, first));
                    foreach (Road ring in TalentContent.Roads.Where(r => r.Ring && r.Radius > road.From && r.Radius < road.To))
                    {
                        if (Find($"way.{road.Id}.{ring.Id}") is TalentNode crossing) stops.Add((ring.Radius, crossing));
                    }
                    stops.Sort((a, b) => a.radius.CompareTo(b.radius));
                    if (stops.Count == 0) continue;

                    int count = 0;
                    for (int i = 0; i < stops.Count; i++)
                    {
                        float from = stops[i].radius;
                        float to = i + 1 < stops.Count ? stops[i + 1].radius : road.To;
                        int between = Math.Max(0, (int)MathF.Round((to - from) / RoadSpacing) - (i + 1 < stops.Count ? 1 : 0));
                        TalentNode previous = stops[i].node;
                        for (int j = 1; j <= between; j++)
                        {
                            float radius = i + 1 < stops.Count ? from + (to - from) * j / (between + 1) : from + (to - from) * j / between;
                            var station = Add(new TalentNode
                            {
                                Id = $"way.{road.Id}.{++count}", Name = road.Name, Kind = TalentKind.Small, Region = RegionAt(road.Angle), Travel = true, Highway = true,
                                Position = At(radius, road.Angle),
                            });
                            Link(previous, station);
                            previous = station;
                            _paths.Add(station);
                        }
                        if (i + 1 < stops.Count) Link(previous, stops[i + 1].node);
                    }
                }
            }

            // ------------------------------------------------------------ finding room

            /// <summary>How far a spot is from the nearest node, link or block, less what it needs around it. Below zero, it doesn't fit.</summary>
            [MethodImpl(MethodImplOptions.AggressiveOptimization)]
            private float Clearance(Vector2 at, float size)
            {
                float clear = 200f;
                foreach (TalentNode node in _space!.Near(at, size + 200f)) clear = MathF.Min(clear, (node.Position - at).Length - size);
                foreach (Drawn link in _space.Along(at - new Vector2(size), at + new Vector2(size)))
                {
                    if (!link.Beyond(at, clear + size)) clear = MathF.Min(clear, DistanceToShape(at, link.Shape) - size);
                }
                foreach ((Vector2 block, float room) in _blocks) clear = MathF.Min(clear, (block - at).Length - room - size + Clear);
                foreach (Vector2 pod in _pods) clear = MathF.Min(clear, (pod - at).Length - PodArc - size);
                return clear;
            }

            /// <summary>The nearest spot to where something was meant to stand that has the room it needs, looking a few hundred units around.</summary>
            private Vector2 ClearSpot(Vector2 wanted, float size)
            {
                Vector2 best = wanted;
                float bestScore = float.NegativeInfinity;
                float radius = wanted.Length;
                float angle = AngleOf(wanted);
                foreach (float out_ in new[] { 0f, 80f, -80f, 160f, -160f, 240f, -240f, 340f })
                {
                    foreach (float across in new[] { 0f, 80f, -80f, 160f, -160f, 240f, -240f, 340f, -340f })
                    {
                        float r = MathF.Max(radius + out_, Fork + size + 60f);
                        Vector2 at = At(r, angle + MathHelper.RadiansToDegrees(across / r));
                        float clear = Clearance(at, size);
                        float score = (clear >= 0f ? 1000f : 0f) + MathF.Min(clear, 40f) - (MathF.Abs(out_) + MathF.Abs(across)) * 0.2f;
                        if (score <= bestScore) continue;
                        bestScore = score;
                        best = at;
                    }
                }
                return best;
            }

            // ------------------------------------------------------------ wheels, keystones and gates

            private void GrowWheel(Region region, int index, Wheel wheel)
            {
                Vector2 centre = _wheels[wheel];
                string id = $"{region.Id}.{wheel.Id}";
                float inward = AngleOf(-centre);

                TalentNode heart = wheel.Heart != null
                    ? Keystone(region, index, wheel.Heart, centre)
                    : Add(new TalentNode { Id = id, Name = wheel.Name, Kind = TalentKind.Notable, Region = index, Position = centre, Mods = wheel.Centre });
                _blocks.Add((centre, RimRadius + 120f));

                int rim = wheel.RimNodes;
                var nodes = new TalentNode[rim];
                var notableAt = new int[wheel.Notables.Length];
                for (int i = 0; i < notableAt.Length; i++) notableAt[i] = (int)MathF.Round((i + 0.5f) * rim / notableAt.Length) % rim;
                int ring = 0;
                for (int i = 0; i < rim; i++)
                {
                    float angle = inward + 360f * i / rim;
                    int notable = Array.IndexOf(notableAt, i);
                    Vector2 at = centre + At(RimRadius, angle);
                    nodes[i] = notable >= 0
                        ? Add(new TalentNode { Id = $"{region.Id}.{wheel.Notables[notable].Id}", Name = wheel.Notables[notable].Name, Kind = TalentKind.Notable, Region = index, Position = at, Mods = wheel.Notables[notable].Mods })
                        : Add(new TalentNode { Id = $"{id}.r{i}", Name = wheel.Name, Kind = TalentKind.Small, Region = index, Position = at, Mods = new[] { wheel.Ring[ring++ % wheel.Ring.Length] } });
                    nodes[i].Orbit = centre;
                    nodes[i].OrbitRadius = RimRadius;
                    if (i > 0) Link(nodes[i - 1], nodes[i]);
                }
                Link(nodes[^1], nodes[0]);

                // Spokes go in from between the notables, so the heart is a few points past the rim from anywhere on it.
                for (int s = 0; s < wheel.Spokes; s++)
                {
                    int at = (int)MathF.Round((s + 0.25f) * rim / wheel.Spokes) % rim;
                    if (Array.IndexOf(notableAt, at) >= 0) at = (at + 1) % rim;
                    var spoke = Add(new TalentNode
                    {
                        Id = $"{id}.s{s}", Name = wheel.Name, Kind = TalentKind.Small, Region = index,
                        Position = centre + At(RimRadius * 0.5f, AngleOf(nodes[at].Position - centre)), Mods = new[] { wheel.Ring[s % wheel.Ring.Length] },
                    });
                    Link(nodes[at], spoke);
                    Link(spoke, heart);
                }

                // Joined to what's around it on two sides, so that it's on the way somewhere.
                int joins = 0;
                foreach ((TalentNode from, TalentNode to) in Nearest(nodes, _paths, 2, 80f, 620f, 100f).ToList())
                {
                    Path(from, to, region, index, $"{id}.j{joins++}");
                }
            }

            private TalentNode Keystone(Region region, int index, Keystone keystone, Vector2 at)
            {
                return Add(new TalentNode
                {
                    Id = $"key.{keystone.Id}", Name = keystone.Name, Kind = TalentKind.Keystone, Region = index, Position = at,
                    Mods = keystone.Mods, Lore = keystone.Lore, Title = keystone.Title, Emblem = keystone.Emblem,
                });
            }

            private void GrowKeystone(Region region, int index, Keystone keystone)
            {
                Vector2 at = ClearSpot(At(keystone.Radius, keystone.Angle), KeystoneRoom);
                TalentNode node = Keystone(region, index, keystone, at);
                _blocks.Add((at, 150f));
                foreach ((TalentNode from, TalentNode to) in Nearest(new[] { node }, _paths, 1, 90f, 1100f, 0f).ToList())
                {
                    Path(to, from, region, index, $"key.{keystone.Id}", keystone.Path);
                }
            }

            private void GrowGate(Gate gate)
            {
                TalentNode a = GateNode(gate, "a", gate.RadiusA, gate.AngleA);
                TalentNode b = GateNode(gate, "b", gate.RadiusB, gate.AngleB);
                a.Twin = b.Index;
                b.Twin = a.Index;
            }

            private TalentNode GateNode(Gate gate, string end, float radius, float angle)
            {
                Vector2 at = ClearSpot(At(radius, angle), GateRoom);
                int region = RegionAt(angle);
                var node = Add(new TalentNode
                {
                    Id = $"gate.{gate.Id}.{end}", Name = "Waygate", Kind = TalentKind.Gate, Region = region, Position = at,
                    Mods = new[] { M.Inc(Stat.MoveSpeed, 3f) }, Lore = "Light this, and its twin across the tree can be lit from here without walking there.",
                });
                _blocks.Add((at, 120f));
                foreach ((TalentNode from, TalentNode to) in Nearest(new[] { node }, _paths, 1, 90f, 1100f, 0f).ToList())
                {
                    Path(to, from, TalentContent.Regions[region], region, $"gate.{gate.Id}.{end}");
                }
                return node;
            }

            // ------------------------------------------------------------ bridges

            // Where two regions meet, a path between their nearest lanes, with a notable for those who walk both in the middle of it. It goes wherever near
            // where it was meant to that it can cross nothing and crowd nothing.
            private void GrowBridge(Bridge bridge)
            {
                int right = (bridge.Left + 1) % TalentContent.Regions.Length;
                Region left = TalentContent.Regions[bridge.Left];
                Region next = TalentContent.Regions[right];
                float boundary = left.Centre + Turn(left.Centre, next.Centre) * left.Width / (left.Width + next.Width);
                List<TalentNode> ours = LaneNodes(left);
                List<TalentNode> theirs = LaneNodes(next);

                foreach (float off in new[] { 0f, 110f, -110f, 220f, -220f, 330f, -330f })
                {
                    Vector2 point = At(bridge.Radius + off, boundary);
                    foreach (TalentNode a in ours.OrderBy(n => (n.Position - point).LengthSquared).Take(5))
                    {
                        foreach (TalentNode b in theirs.OrderBy(n => (n.Position - point).LengthSquared).Take(5))
                        {
                            float length = (b.Position - a.Position).Length;
                            if (length < 280f || length > 760f) continue;
                            if (Crosses(a.Position, b.Position, a, b) || Crowds(a.Position, b.Position, a, b)) continue;
                            Span(bridge, a, b, right);
                            return;
                        }
                    }
                }
            }

            private void Span(Bridge bridge, TalentNode a, TalentNode b, int right)
            {
                int steps = Math.Max(2, (int)MathF.Round((b.Position - a.Position).Length / BridgeSpacing));
                int half = steps / 2;
                TalentNode previous = a;
                for (int i = 1; i < steps; i++)
                {
                    Vector2 at = Vector2.Lerp(a.Position, b.Position, i / (float)steps);
                    TalentNode node = i == half
                        ? Add(new TalentNode { Id = $"bridge.{bridge.Id}", Name = bridge.Name, Kind = TalentKind.Notable, Region = bridge.Left, Position = at, Mods = bridge.Mods })
                        : Add(new TalentNode { Id = $"bridge.{bridge.Id}.{i}", Name = bridge.Name, Kind = TalentKind.Small, Region = i < half ? bridge.Left : right, Position = at, Mods = new[] { bridge.Small } });
                    Link(previous, node);
                    previous = node;
                }
                Link(previous, b);
            }

            private List<TalentNode> LaneNodes(Region region)
            {
                var nodes = new List<TalentNode>();
                foreach (Lane lane in region.Lanes) nodes.AddRange(_lanes[$"{region.Id}.{lane.Id}"]);
                return nodes;
            }

            // ------------------------------------------------------------ clusters

            // Clusters hang off a lane, spread along the part of it away from the root, each where there's room: the side and distance with the most, near
            // where it ought to be. One that fits nowhere grows the lane on and tries again, and then tries the region's other lanes.
            private void HangPods(Region region, int index, Lane lane)
            {
                List<TalentNode> all = _lanes[$"{region.Id}.{lane.Id}"];
                int count = lane.Pods.Length;
                if (count == 0) return;
                List<TalentNode> nodes = Quiet(all);

                float side = Unit($"{region.Id}.{lane.Id}.side") < 0.5f ? -1f : 1f;
                for (int p = 0; p < count; p++)
                {
                    Pod pod = lane.Pods[p];
                    Candidate? chosen = null;
                    List<TalentNode> onto = nodes;
                    for (int grown = 0; grown <= 3 && chosen is not { Room: >= 0f }; grown++)
                    {
                        if (grown > 0)
                        {
                            if (!Extend(region, index, lane, all)) break;
                            nodes = Quiet(all);
                            onto = nodes;
                        }
                        float ideal = 1f + (p + 0.5f) * (nodes.Count - 1.5f) / count;
                        Candidate? found = Place(region, pod, nodes, ideal, side);
                        if (found is Candidate candidate && (chosen == null || candidate.Room > chosen.Value.Room)) chosen = candidate;
                    }

                    // Somewhere else in the region, if this lane has no room left at all.
                    if (chosen is not { Room: >= 0f })
                    {
                        foreach (Lane other in region.Lanes.Where(l => l != lane))
                        {
                            List<TalentNode> elsewhere = Quiet(_lanes[$"{region.Id}.{other.Id}"]);
                            Candidate? found = Place(region, pod, elsewhere, elsewhere.Count * 0.5f, side);
                            if (found is not Candidate candidate || candidate.Room < (chosen?.Room ?? float.NegativeInfinity)) continue;
                            chosen = candidate;
                            onto = elsewhere;
                            if (candidate.Room >= 0f) break;
                        }
                    }

                    // And failing that, out past the end of the region's furthest lane, which grows until there's room, as there always is further out.
                    if (chosen is not { Room: >= 0f })
                    {
                        foreach (Lane other in region.Lanes.OrderByDescending(l => _lanes[$"{region.Id}.{l.Id}"][^1].Position.Length))
                        {
                            List<TalentNode> far = _lanes[$"{region.Id}.{other.Id}"];
                            for (int grown = 0; grown < 12 && chosen is not { Room: >= 0f }; grown++)
                            {
                                if (!Extend(region, index, other, far)) break;
                                List<TalentNode> tip = far.GetRange(Math.Max(far.Count - 3, 0), Math.Min(3, far.Count));
                                Candidate? found = Place(region, pod, tip, tip.Count - 1, side);
                                if (found is not Candidate candidate || candidate.Room < (chosen?.Room ?? float.NegativeInfinity)) continue;
                                chosen = candidate;
                                onto = tip;
                            }
                            if (chosen is { Room: >= 0f }) break;
                        }
                    }

                    Hang(region, index, pod, onto[chosen!.Value.At], chosen.Value);
                    side = -chosen.Value.Facing;
                }
            }

            // The part of a lane far enough out for clusters, or all of it if none is.
            private static List<TalentNode> Quiet(List<TalentNode> lane)
            {
                List<TalentNode> quiet = lane.Where(n => n.Position.Length >= InnerQuiet).ToList();
                return quiet.Count >= 2 ? quiet : lane;
            }

            // The first place that has plenty of room, going out from where it ought to be, or else the one with the most.
            [MethodImpl(MethodImplOptions.AggressiveOptimization)]
            private Candidate? Place(Region region, Pod pod, List<TalentNode> lane, float ideal, float side)
            {
                bool chain = Unit($"{region.Id}.{pod.Id}.shape") < 0.22f;
                float curl = Unit($"{region.Id}.{pod.Id}.curl") < 0.5f ? -1f : 1f;
                Candidate? best = null;
                int middle = Math.Clamp((int)MathF.Round(ideal), 0, lane.Count - 1);
                foreach (bool shape in new[] { chain, !chain })
                {
                    for (int step = 0; step < lane.Count * 2; step++)
                    {
                        int offset = (step + 1) / 2 * (step % 2 == 0 ? 1 : -1);
                        int at = middle + offset;
                        if (at < 0 || at >= lane.Count) continue;
                        if (MathF.Abs(offset) > 6 && best is { Room: >= 0f }) break;
                        foreach (float facing in new[] { side, -side })
                        {
                            foreach (float reach in shape ? new[] { (pod.Smalls + 1) * Spacing } : new[] { PodReach, PodReach + 70f, PodReach + 150f })
                            {
                                foreach (float turn in new[] { curl, -curl })
                                {
                                    Candidate candidate = Try(pod, lane, at, facing, reach, turn, shape);
                                    if (candidate.Room >= 45f) return candidate;
                                    if (best == null || candidate.Room > best.Value.Room) best = candidate;
                                }
                            }
                        }
                    }
                }
                return best;
            }

            // Grows a lane on by a node, straight on from where it was going, or a little to either side, wherever there's room.
            private bool Extend(Region region, int index, Lane lane, List<TalentNode> nodes)
            {
                TalentNode last = nodes[^1];
                Vector2 before = nodes.Count > 1 ? nodes[^2].Position : last.Position * 0.9f;
                Vector2 way = (last.Position - before).Normalized();
                foreach (float bend in new[] { 0f, 18f, -18f, 36f, -36f })
                {
                    float angle = MathHelper.DegreesToRadians(bend);
                    var turned = new Vector2(way.X * MathF.Cos(angle) - way.Y * MathF.Sin(angle), way.X * MathF.Sin(angle) + way.Y * MathF.Cos(angle));
                    Vector2 at = last.Position + turned * LaneSpacing;
                    bool crowded = false;
                    foreach (TalentNode node in _space!.Near(at, 2f * Clear))
                    {
                        if (node != last && (node.Position - at).Length < Clear * 1.2f) crowded = true;
                    }
                    foreach (Vector2 pod in _pods) crowded |= (pod - at).Length < PodArc + Clear;
                    if (crowded || LinkRoom(at, last) < SmallRoom || Crosses(last.Position, at, last)) continue;

                    var grown = Add(new TalentNode { Id = $"{region.Id}.{lane.Id}.{nodes.Count + 1}", Name = region.Name, Kind = TalentKind.Small, Region = index, Travel = true, Position = at });
                    Link(last, grown);
                    nodes.Add(grown);
                    _paths.Add(grown);
                    return true;
                }
                return false;
            }

            private readonly record struct Candidate(int At, float Facing, Vector2 Anchor, Vector2[] Smalls, Vector2[] Stem, float Room, bool Chain);

            // Where a cluster would go, and how much room it would have there. No room at all, or a link across another, is as bad as can be.
            [MethodImpl(MethodImplOptions.AggressiveOptimization)]
            private Candidate Try(Pod pod, List<TalentNode> lane, int at, float facing, float reach, float curl, bool chain)
            {
                TalentNode attach = lane[at];
                Vector2 before = lane[Math.Max(at - 1, 0)].Position;
                Vector2 after = lane[Math.Min(at + 1, lane.Count - 1)].Position;
                Vector2 along = (after - before).LengthSquared > 1f ? (after - before).Normalized() : attach.Position.Normalized();
                Vector2 normal = new Vector2(-along.Y, along.X) * facing;

                // Leaning outward a little, as a tree's do, so clusters don't stand square to their lane like rungs.
                Vector2 outward = attach.Position.LengthSquared > 1f ? attach.Position.Normalized() : normal;
                Vector2 lean = (normal + outward * 0.35f).Normalized();
                Vector2 anchor = attach.Position + lean * reach;

                int smalls = Math.Max(pod.Smalls, 1);
                var points = new Vector2[smalls];
                Vector2[] stem;
                if (chain)
                {
                    for (int i = 0; i < smalls; i++) points[i] = Vector2.Lerp(attach.Position, anchor, (i + 1f) / (smalls + 1f));
                    stem = Array.Empty<Vector2>();
                }
                else
                {
                    float inward = AngleOf(attach.Position - anchor);
                    for (int i = 0; i < smalls; i++) points[i] = anchor + At(PodArc, inward + curl * PodTurn * i);
                    int between = Math.Max(0, (int)MathF.Round((points[0] - attach.Position).Length / Spacing) - 1);
                    stem = new Vector2[between];
                    for (int i = 0; i < between; i++) stem[i] = Vector2.Lerp(attach.Position, points[0], (i + 1f) / (between + 1f));
                }

                // No new link may cross an old one, or pass too near a node on its way, as it will be drawn: a cluster's small nodes are joined round its arc.
                // One that does is as bad as can be whatever else is true, so nothing more need be asked of it.
                Vector2 last = attach.Position;
                int index = 0;
                foreach (Vector2 point in stem.Concat(points))
                {
                    // Only the first link starts at the lane, so only it may touch what else meets the lane there.
                    bool round = !chain && index > stem.Length;
                    if (Crosses(last, point, index == 0 ? attach : null, null, round ? anchor : null, PodArc)) return new Candidate(at, facing, anchor, points, stem, -1000f, chain);
                    last = point;
                    index++;
                }
                if (Crosses(last, anchor, null)) return new Candidate(at, facing, anchor, points, stem, -1000f, chain);

                // A node at least the room there is so far from the box round all of it, and twice the clearance further from the notable, can't make it
                // any less, so most need nothing worked out.
                Vector2 low = anchor, high = anchor;
                foreach (Vector2 point in points) { low = Vector2.ComponentMin(low, point); high = Vector2.ComponentMax(high, point); }
                foreach (Vector2 point in stem) { low = Vector2.ComponentMin(low, point); high = Vector2.ComponentMax(high, point); }
                float room = 160f;
                foreach (TalentNode node in _space!.Near(anchor, reach + 2f * Clear + 60f))
                {
                    if (node == attach) continue;
                    Vector2 p = node.Position;
                    float dx = MathF.Max(MathF.Max(low.X - p.X, p.X - high.X), 0f);
                    float dy = MathF.Max(MathF.Max(low.Y - p.Y, p.Y - high.Y), 0f);
                    float far = room + Clear;
                    float apart = room + 2f * Clear;
                    if (far > 0f && dx * dx + dy * dy >= far * far && (p - anchor).LengthSquared >= apart * apart) continue;
                    room = MathF.Min(room, (p - anchor).Length - 2f * Clear);
                    foreach (Vector2 point in points) room = MathF.Min(room, (p - point).Length - Clear);
                    foreach (Vector2 point in stem) room = MathF.Min(room, (p - point).Length - Clear);
                }
                foreach (Vector2 other in _pods)
                {
                    float apart = room + PodRoom;
                    if (apart > 0f && (other - anchor).LengthSquared >= apart * apart) continue;
                    room = MathF.Min(room, (other - anchor).Length - PodRoom);
                }
                foreach ((Vector2 block, float size) in _blocks) room = MathF.Min(room, (block - anchor).Length - size - PodArc);

                // And no new node may sit on an old link.
                foreach (Vector2 point in stem.Concat(points)) room = MathF.Min(room, LinkRoom(point, attach) - SmallRoom);
                room = MathF.Min(room, LinkRoom(anchor, attach) - NotableRoom);
                return new Candidate(at, facing, anchor, points, stem, room, chain);
            }

            private void Hang(Region region, int index, Pod pod, TalentNode attach, Candidate where)
            {
                _pods.Add(where.Anchor);
                TalentNode previous = attach;
                for (int i = 0; i < where.Stem.Length; i++)
                {
                    var step = Add(new TalentNode { Id = $"{region.Id}.{pod.Id}.stem{i}", Name = region.Name, Kind = TalentKind.Small, Region = index, Travel = true, Position = where.Stem[i] });
                    Link(previous, step);
                    previous = step;
                }
                for (int i = 0; i < where.Smalls.Length; i++)
                {
                    var small = Add(new TalentNode
                    {
                        Id = $"{region.Id}.{pod.Id}.{i}", Name = pod.Name, Kind = TalentKind.Small, Region = index, Position = where.Smalls[i], Mods = new[] { pod.Small },
                        Orbit = where.Chain ? Vector2.Zero : where.Anchor, OrbitRadius = where.Chain ? 0f : PodArc,
                    });
                    Link(previous, small);
                    previous = small;
                }
                var notable = Add(new TalentNode { Id = $"{region.Id}.{pod.Id}", Name = pod.Name, Kind = TalentKind.Notable, Region = index, Position = where.Anchor, Mods = pod.Mods });
                Link(previous, notable);
            }

            // ------------------------------------------------------------ joining things up

            /// <summary>The nearest pairs of one of <paramref name="from"/> and one of <paramref name="onto"/>, that a straight path between would cross nothing, each at least <paramref name="apart"/> from the last chosen.</summary>
            private IEnumerable<(TalentNode from, TalentNode to)> Nearest(IReadOnlyList<TalentNode> from, List<TalentNode> onto, int wanted, float least, float most, float apart)
            {
                var pairs = new List<(float distance, TalentNode from, TalentNode to)>();
                foreach (TalentNode a in from)
                {
                    foreach (TalentNode b in onto)
                    {
                        float distance = (a.Position - b.Position).Length;
                        if (distance < least || distance > most) continue;
                        pairs.Add((distance, a, b));
                    }
                }
                pairs.Sort((x, y) => x.distance.CompareTo(y.distance));

                var chosen = new List<(TalentNode from, TalentNode to)>();
                foreach ((float distance, TalentNode a, TalentNode b) in pairs)
                {
                    if (chosen.Count >= wanted) break;
                    if (chosen.Exists(c => (c.to.Position - b.Position).Length < apart * 3f || c.from == a)) continue;
                    if (Crosses(a.Position, b.Position, a, b) || Crowds(a.Position, b.Position, a, b)) continue;
                    chosen.Add((a, b));
                    yield return (a, b);
                }
            }

            // A straight path of travel nodes from one to the other.
            private void Path(TalentNode from, TalentNode to, Region region, int index, string id, Mod? gives = null)
            {
                int steps = Math.Max(1, (int)MathF.Round((to.Position - from.Position).Length / LaneSpacing));
                TalentNode previous = from;
                for (int i = 1; i < steps; i++)
                {
                    var node = Add(new TalentNode
                    {
                        Id = $"{id}.{i}", Name = gives != null ? to.Name : region.Name, Kind = TalentKind.Small, Region = index,
                        Position = Vector2.Lerp(from.Position, to.Position, i / (float)steps), Travel = gives == null, Mods = gives is Mod mod ? new[] { mod } : Array.Empty<Mod>(),
                    });
                    Link(previous, node);
                    previous = node;
                }
                Link(previous, to);
            }

            private const float SmallRoom = 30f;      // What a new node keeps from any link that isn't its own, as drawn,
            private const float NotableRoom = 42f;    // and a notable.

            // Whether a new link would cross an old one, or pass through a node that isn't one of its ends, as both will be drawn.
            [MethodImpl(MethodImplOptions.AggressiveOptimization)]
            private static bool Crosses(Vector2 a, Vector2 b, TalentNode? ignoreA, TalentNode? ignoreB = null, Vector2? orbit = null, float radius = 0f)
            {
                Vector2[] shape = Shape(a, b, orbit, radius);
                (Vector2 min, Vector2 max) = Bounds(shape);
                foreach (Drawn link in _space!.Along(min, max))
                {
                    if (link.A == ignoreA || link.B == ignoreA || link.A == ignoreB || link.B == ignoreB) continue;
                    if (link.Max.X < min.X || link.Min.X > max.X || link.Max.Y < min.Y || link.Min.Y > max.Y) continue;
                    if (ShapesCross(shape, link.Shape)) return true;
                }
                Vector2 middle = (min + max) * 0.5f;
                foreach (TalentNode node in _space.Near(middle, (max - min).Length * 0.5f + 60f))
                {
                    if (node == ignoreA || node == ignoreB || (node.Position - a).LengthSquared < 1f || (node.Position - b).LengthSquared < 1f) continue;
                    float room = RoomOf(node);
                    Vector2 at = node.Position;
                    if (at.X < min.X - room || at.X > max.X + room || at.Y < min.Y - room || at.Y > max.Y + room) continue;
                    if (DistanceToShape(at, shape) < room) return true;
                }
                return false;
            }

            // How near the nearest link is to a spot, as drawn. Even those from the node it will be joined to count, as a link that runs on towards
            // it would be drawn over it.
            [MethodImpl(MethodImplOptions.AggressiveOptimization)]
            private static float LinkRoom(Vector2 at, TalentNode? joinedTo)
            {
                float nearest = float.PositiveInfinity;
                foreach (Drawn link in _space!.Along(at - new Vector2(80f), at + new Vector2(80f)))
                {
                    if (!link.Beyond(at, nearest)) nearest = MathF.Min(nearest, DistanceToShape(at, link.Shape));
                }
                return nearest;
            }

            // Whether a straight path would pass too near anything along the way.
            [MethodImpl(MethodImplOptions.AggressiveOptimization)]
            private static bool Crowds(Vector2 a, Vector2 b, TalentNode ignoreA, TalentNode ignoreB)
            {
                Vector2 middle = (a + b) * 0.5f;
                foreach (TalentNode node in _space!.Near(middle, (b - a).Length * 0.5f + Clear))
                {
                    if (node == ignoreA || node == ignoreB) continue;
                    if (DistanceToSegment(node.Position, a, b) < Clear * 0.8f) return true;
                }
                return false;
            }

            // ------------------------------------------------------------ cross links

            // Paths across between neighbouring lanes, wherever two come near each other and are a long walk apart by the tree: they're what make it a web
            // and not a fan, and the reason a build can turn sideways.
            private void CrossLinks()
            {
                const float Least = 190f;
                const float Most = 360f;
                const float Apart = 520f;
                const int Detour = 7;
                float inner = _rings.Length > 0 ? _rings[0] + 150f : 0f;

                var lanes = new List<(TalentNode node, string lane)>();
                foreach ((string lane, List<TalentNode> nodes) in _lanes)
                {
                    foreach (TalentNode node in nodes)
                    {
                        if (node.Position.Length >= InnerQuiet + 200f) lanes.Add((node, lane));
                    }
                }

                var pairs = new List<(float distance, TalentNode a, TalentNode b)>();
                for (int i = 0; i < lanes.Count; i++)
                {
                    foreach (TalentNode other in _space!.Near(lanes[i].node.Position, Most))
                    {
                        if (other.Index <= lanes[i].node.Index || !other.Travel || other.Highway) continue;
                        int j = lanes.FindIndex(l => l.node == other);
                        if (j < 0 || lanes[j].lane == lanes[i].lane) continue;
                        float distance = (other.Position - lanes[i].node.Position).Length;
                        if (distance >= Least && distance <= Most) pairs.Add((distance, lanes[i].node, other));
                    }
                }
                pairs.Sort((x, y) => x.distance.CompareTo(y.distance));

                var made = new List<Vector2>();
                int count = 0;
                foreach ((float distance, TalentNode a, TalentNode b) in pairs)
                {
                    // Two ways out of the middle only ever meet past the Old Road.
                    if (_way[Math.Max(a.Region, 0)] != _way[Math.Max(b.Region, 0)] && MathF.Min(a.Position.Length, b.Position.Length) < inner) continue;

                    Vector2 middle = (a.Position + b.Position) * 0.5f;
                    if (made.Exists(m => (m - middle).Length < Apart)) continue;
                    if (Hops(a, b, Detour) <= Detour) continue;
                    if (Crosses(a.Position, b.Position, a, b) || Crowds(a.Position, b.Position, a, b)) continue;
                    int region = Math.Max(a.Region, 0);
                    Path(a, b, TalentContent.Regions[region], region, $"cross.{++count}");
                    made.Add(middle);
                }
            }

            // How many steps apart two nodes are, counting no further than a limit.
            private static int Hops(TalentNode from, TalentNode to, int limit)
            {
                var seen = new HashSet<int> { from.Index };
                var frontier = new List<int> { from.Index };
                for (int depth = 1; depth <= limit; depth++)
                {
                    var next = new List<int>();
                    foreach (int index in frontier)
                    {
                        foreach (int link in Built[index].Links)
                        {
                            if (link == to.Index) return depth;
                            if (seen.Add(link)) next.Add(link);
                        }
                    }
                    frontier = next;
                }
                return limit + 1;
            }

            // ------------------------------------------------------------ what travel gives

            // A travel node gives a little of its region's attribute; between two, every other one gives the second.
            private static void Attributes()
            {
                foreach (TalentNode node in Built)
                {
                    if (!node.Travel || node.Mods.Length > 0) continue;
                    Region region = TalentContent.Regions[Math.Max(node.Region, 0)];
                    Stat stat = region.Second is Stat second && (Hash(node.Id) & 1) == 1 ? second : region.Attribute;
                    node.Mods = new[] { M.Flat(stat, AttributeStep) };
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static bool Intersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            static float Cross(Vector2 o, Vector2 p, Vector2 q) => (p.X - o.X) * (q.Y - o.Y) - (p.Y - o.Y) * (q.X - o.X);
            float d1 = Cross(c, d, a), d2 = Cross(c, d, b), d3 = Cross(a, b, c), d4 = Cross(a, b, d);
            return (d1 > 0f) != (d2 > 0f) && (d3 > 0f) != (d4 > 0f) && MathF.Abs(d1) > 1e-3f && MathF.Abs(d2) > 1e-3f && MathF.Abs(d3) > 1e-3f && MathF.Abs(d4) > 1e-3f;
        }

        /// <summary>Whether a link is drawn along the circle both its ends sit on, which a road's and a wheel's are, rather than straight.</summary>
        public static bool Arcs(TalentNode a, TalentNode b) => a.OrbitRadius > 0f && MathF.Abs(a.OrbitRadius - b.OrbitRadius) < 0.5f && (a.Orbit - b.Orbit).LengthSquared < 0.25f;

        /// <summary>A link as it's drawn: its two ends, or points along its arc no more than a few units off it.</summary>
        public static Vector2[] Shape(TalentNode a, TalentNode b) => Shape(a.Position, b.Position, Arcs(a, b) ? a.Orbit : null, a.OrbitRadius);

        private static Vector2[] Shape(Vector2 a, Vector2 b, Vector2? orbit, float radius)
        {
            if (orbit is not Vector2 centre) return new[] { a, b };
            float start = MathF.Atan2(a.Y - centre.Y, a.X - centre.X);
            float sweep = MathF.IEEERemainder(MathF.Atan2(b.Y - centre.Y, b.X - centre.X) - start, MathF.Tau);
            int steps = Math.Clamp((int)MathF.Ceiling(MathF.Abs(sweep) * radius / 20f), 1, 64);
            var points = new Vector2[steps + 1];
            points[0] = a;
            points[steps] = b;
            for (int i = 1; i < steps; i++)
            {
                float angle = start + sweep * i / steps;
                points[i] = centre + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            }
            return points;
        }

        /// <summary>How near a link, as it's drawn, comes to a point.</summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static float DistanceToShape(Vector2 p, Vector2[] shape)
        {
            float best = float.PositiveInfinity;
            for (int i = 1; i < shape.Length; i++) best = MathF.Min(best, DistanceToSegment(p, shape[i - 1], shape[i]));
            return best;
        }

        /// <summary>Whether two links, as they're drawn, cross.</summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static bool ShapesCross(Vector2[] one, Vector2[] other)
        {
            for (int i = 1; i < one.Length; i++)
            {
                Vector2 p = one[i - 1], q = one[i];
                float left = MathF.Min(p.X, q.X), right = MathF.Max(p.X, q.X), top = MathF.Min(p.Y, q.Y), bottom = MathF.Max(p.Y, q.Y);
                for (int j = 1; j < other.Length; j++)
                {
                    Vector2 r = other[j - 1], s = other[j];

                    // Two pieces whose boxes don't meet can't cross, which is most of them.
                    if (MathF.Max(r.X, s.X) < left || MathF.Min(r.X, s.X) > right || MathF.Max(r.Y, s.Y) < top || MathF.Min(r.Y, s.Y) > bottom) continue;
                    if (Intersect(p, q, r, s)) return true;
                }
            }
            return false;
        }

        /// <summary>The box round a link's shape.</summary>
        public static (Vector2 min, Vector2 max) Bounds(Vector2[] shape)
        {
            Vector2 min = shape[0], max = shape[0];
            foreach (Vector2 point in shape)
            {
                min = Vector2.ComponentMin(min, point);
                max = Vector2.ComponentMax(max, point);
            }
            return (min, max);
        }

        /// <summary>How much room a node takes, as drawn: no link but its own may come nearer it than this.</summary>
        public static float RoomOf(TalentNode node) => node.Kind switch
        {
            TalentKind.Keystone => 58f,
            TalentKind.Root => 40f,
            TalentKind.Gate => 36f,
            TalentKind.Notable => 32f,
            _ => 20f,
        };

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.LengthSquared > 1e-6f ? MathHelper.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared, 0f, 1f) : 0f;
            return (p - (a + ab * t)).Length;
        }

        private static int[] Depths()
        {
            var depth = new int[Nodes.Length];
            Array.Fill(depth, -1);
            depth[Root.Index] = 0;
            var frontier = new Queue<int>();
            frontier.Enqueue(Root.Index);
            while (frontier.TryDequeue(out int index))
            {
                foreach (int link in Nodes[index].Links)
                {
                    if (depth[link] >= 0) continue;
                    depth[link] = depth[index] + 1;
                    frontier.Enqueue(link);
                }
            }
            return depth;
        }

        public static TalentNode? Find(string id) => ById.TryGetValue(id, out int index) ? (Nodes != null ? Nodes[index] : Built[index]) : null;

        /// <summary>The nodes a hero has, as places in <see cref="Nodes"/>, the root among them. Sorted, so everything adds up in the same order everywhere.</summary>
        public static List<int> Allocated(HeroState hero)
        {
            var allocated = new List<int> { Root.Index };
            foreach (string id in hero.Talents)
            {
                if (ById.TryGetValue(id, out int index) && index != Root.Index) allocated.Add(index);
            }
            allocated.Sort();
            return allocated;
        }

        private static bool[] Marks(IEnumerable<int> allocated)
        {
            var marks = new bool[Nodes.Length];
            foreach (int index in allocated) marks[index] = true;
            return marks;
        }

        private static bool Reaches(bool[] allocated, TalentNode node)
        {
            foreach (int link in node.Links)
            {
                if (allocated[link]) return true;
            }
            return node.Twin >= 0 && allocated[node.Twin];
        }

        /// <summary>Whether a node can be taken next: it has to touch what's already there, or be the far side of a waygate that's lit.</summary>
        public static bool CanAllocate(HeroState hero, int index)
        {
            if (index < 0 || index >= Nodes.Length || hero.UnspentPoints <= 0) return false;
            bool[] allocated = Marks(Allocated(hero));
            return !allocated[index] && Reaches(allocated, Nodes[index]);
        }

        /// <summary>Whether a node can be given back: only if everything else stays joined to the root without it.</summary>
        public static bool CanRefund(HeroState hero, int index)
        {
            if (index <= 0 || index >= Nodes.Length) return false;
            bool[] allocated = Marks(Allocated(hero));
            if (!allocated[index]) return false;

            allocated[index] = false;
            bool[] joined = Joined(allocated);
            for (int i = 0; i < Nodes.Length; i++)
            {
                if (allocated[i] && !joined[i]) return false;
            }
            return true;
        }

        // Everything that can be got to from the root by what's allocated, walking or stepping through a lit gate.
        private static bool[] Joined(bool[] allocated)
        {
            var joined = new bool[Nodes.Length];
            joined[Root.Index] = true;
            var frontier = new Stack<int>();
            frontier.Push(Root.Index);
            while (frontier.TryPop(out int index))
            {
                TalentNode node = Nodes[index];
                foreach (int link in node.Links)
                {
                    if (!allocated[link] || joined[link]) continue;
                    joined[link] = true;
                    frontier.Push(link);
                }
                if (node.Twin >= 0 && allocated[node.Twin] && !joined[node.Twin])
                {
                    joined[node.Twin] = true;
                    frontier.Push(node.Twin);
                }
            }
            return joined;
        }

        /// <summary>The cheapest way from what's already taken to a node, ending with it. Empty if it's taken already.</summary>
        public static List<int> PathTo(HeroState hero, int target)
        {
            var path = new List<int>();
            if (target < 0 || target >= Nodes.Length) return path;

            bool[] allocated = Marks(Allocated(hero));
            if (allocated[target]) return path;

            var from = new int[Nodes.Length];
            Array.Fill(from, -1);
            var frontier = new Queue<int>();
            for (int i = 0; i < Nodes.Length; i++)
            {
                if (!allocated[i]) continue;
                from[i] = i;
                frontier.Enqueue(i);
            }

            while (frontier.TryDequeue(out int index))
            {
                if (index == target) break;
                TalentNode node = Nodes[index];
                foreach (int link in node.Links)
                {
                    if (from[link] >= 0) continue;
                    from[link] = index;
                    frontier.Enqueue(link);
                }

                // A gate's twin is one step off, as a path takes its nodes in order and so lights the gate on its way through.
                if (node.Twin >= 0 && from[node.Twin] < 0)
                {
                    from[node.Twin] = index;
                    frontier.Enqueue(node.Twin);
                }
            }

            if (from[target] < 0) return path;
            for (int at = target; !allocated[at]; at = from[at]) path.Add(at);
            path.Reverse();
            return path;
        }

        /// <summary>
        /// Puts a hero right, whatever a save or a stranger says it has. What's unknown goes, what's cut off from the root goes, and what can't
        /// be paid for goes. So a change to the tree costs someone a few points back in their pocket, never their hero.
        /// </summary>
        public static void Sanitize(HeroState hero)
        {
            bool[] allocated = Marks(Allocated(hero));
            bool[] joined = Joined(allocated);

            var kept = new List<string>();
            var seen = new HashSet<int>();
            foreach (string id in hero.Talents)
            {
                if (!ById.TryGetValue(id, out int index) || index == Root.Index || !joined[index] || !seen.Add(index)) continue;
                kept.Add(id);
            }

            // Too many for their level: the last taken go first, which can't cut off anything taken before them.
            int budget = hero.TalentPoints;
            if (kept.Count > budget) kept.RemoveRange(budget, kept.Count - budget);
            hero.Talents = kept;

            bool[] trimmed = Marks(Allocated(hero));
            bool[] stillJoined = Joined(trimmed);
            hero.Talents.RemoveAll(id => !stillJoined[ById[id]]);
        }

        /// <summary>Everything a hero's talents add up to.</summary>
        public static IEnumerable<Mod> Mods(HeroState hero)
        {
            foreach (int index in Allocated(hero))
            {
                foreach (Mod mod in Nodes[index].Mods) yield return mod;
            }
        }

        /// <summary>
        /// What someone is, by where they've spent their points: the region they've given most to names them, and the next one colours it. A
        /// keystone can have the last word.
        /// </summary>
        public static string TitleOf(HeroState hero)
        {
            var points = new int[TalentContent.Regions.Length];
            string? named = null;
            int spent = 0;
            foreach (int index in Allocated(hero))
            {
                TalentNode node = Nodes[index];
                if (node.Region < 0) continue;
                points[node.Region]++;
                spent++;
                if (node.Title != null) named = node.Title;
            }

            if (named != null) return named;
            if (spent < 6) return "Wanderer";

            int first = 0;
            for (int i = 1; i < points.Length; i++)
            {
                if (points[i] > points[first]) first = i;
            }

            int second = -1;
            for (int i = 0; i < points.Length; i++)
            {
                if (i != first && points[i] > 0 && (second < 0 || points[i] > points[second])) second = i;
            }

            Region main = TalentContent.Regions[first];
            if (second < 0 || points[second] * 3 < points[first]) return $"{main.Adjective} {main.Noun}";
            return $"{TalentContent.Regions[second].Adjective} {main.Noun}";
        }
    }
}

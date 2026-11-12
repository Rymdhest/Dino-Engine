using OpenTK.Mathematics;
using System.Buffers;

namespace Dino_Engine.Modelling.Procedural.Rock
{
    /// <summary>
    /// Takes a mesh down to a budget by collapsing whichever edge costs least (Garland and Heckbert's quadric error metrics). Every vertex keeps the planes of the faces that met there, and
    /// moving it off them is what costs. That suits rock: a flat face is a single plane however many triangles it had, so it costs nothing to empty it, while a crack or an edge is many
    /// planes at once and keeps its triangles. A rock of a thousand triangles spent like this looks like one of tens of thousands.
    /// <para>
    /// It runs on worker threads while a game is being played (the rock's skin, <see cref="RockSkin"/>), so it makes next to nothing for the collector: its arrays are rented and given
    /// back, the faces round each point are runs of one shared array rather than a list each, and what it needs from one mesh to the next is kept per thread. A hundred thousand
    /// lists that lived through a whole simplification were promoted and copied by every collection, and the game paused forty milliseconds at a time.
    /// </para>
    /// </summary>
    internal sealed class Decimator
    {
        private const int QuadricSize = 10;     // A symmetric 4 x 4, kept as a2 ab ac ad b2 bc bd c2 cd d2.
        private const double SmallestTurn = 0.2;    // Cosine of the most a face may turn in one collapse. Further than this and it's folding over.
        private const int MostPasses = 12;
        private const double BorderWeight = 8.0;   // How hard an open edge holds its line, against the faces beside it.

        private readonly record struct Candidate(int A, int B, int VersionA, int VersionB, Vector3d Target);

        // What one thread keeps from one mesh to the next.
        [ThreadStatic] private static PriorityQueue<Candidate, double>? _sharedQueue;
        [ThreadStatic] private static HashSet<long>? _sharedEdges;
        [ThreadStatic] private static Dictionary<long, int>? _sharedUses;
        [ThreadStatic] private static List<int>? _sharedNeighbours;

        private readonly int _pointCount;
        private readonly int _triangleCount;
        private readonly Vector3d[] _points;
        private readonly double[] _quadrics;
        private readonly int[] _triangles;
        private readonly bool[] _dead;
        private readonly int[] _version;
        private readonly bool[] _gone;
        // Which points have been seen this time round, by a stamp each, as a flat face simplifies into a fan round one point and a list it searched made that cost the square of its size.
        private readonly int[] _stamp;
        private readonly int[] _seenStamp;
        private int _now, _seenNow;

        // The faces round each point: a run of _arena from _start, _count long, with room for _room.
        private readonly int[] _start;
        private readonly int[] _count;
        private readonly int[] _room;
        private int[] _arena;
        private int _used;

        private readonly PriorityQueue<Candidate, double> _queue;
        private readonly List<int> _neighbours;
        private double _longestSquared = double.PositiveInfinity;
        private bool[]? _pinned;
        private bool _open;
        private int _mostNeighbours = int.MaxValue;
        private double _mostError = double.PositiveInfinity;

        private Decimator(List<Vector3> points, List<int> triangles)
        {
            _pointCount = points.Count;
            _triangleCount = triangles.Count / 3;
            _points = ArrayPool<Vector3d>.Shared.Rent(_pointCount);
            for (int i = 0; i < _pointCount; i++) _points[i] = new Vector3d(points[i].X, points[i].Y, points[i].Z);

            _triangles = ArrayPool<int>.Shared.Rent(triangles.Count);
            triangles.CopyTo(_triangles);
            _dead = Rented<bool>(_triangleCount);
            _quadrics = Rented<double>(_pointCount * QuadricSize);
            _version = Rented<int>(_pointCount);
            _gone = Rented<bool>(_pointCount);
            _stamp = Rented<int>(_pointCount);
            _seenStamp = Rented<int>(_pointCount);
            _start = Rented<int>(_pointCount);
            _count = Rented<int>(_pointCount);
            _room = Rented<int>(_pointCount);

            _queue = _sharedQueue ??= new PriorityQueue<Candidate, double>();
            _queue.Clear();
            _neighbours = _sharedNeighbours ??= new List<int>();

            // Room for the faces each point starts with and as many again, as a point that's kept gathers the faces of those collapsed into it.
            for (int t = 0; t < _triangleCount; t++)
            {
                _room[_triangles[t * 3]]++;
                _room[_triangles[t * 3 + 1]]++;
                _room[_triangles[t * 3 + 2]]++;
            }
            int total = 0;
            for (int v = 0; v < _pointCount; v++)
            {
                _room[v] = Math.Max(_room[v] * 2, 4);
                _start[v] = total;
                total += _room[v];
            }
            _arena = ArrayPool<int>.Shared.Rent(Math.Max(total * 2, 16));
            _used = total;

            for (int t = 0; t < _triangleCount; t++)
            {
                int a = _triangles[t * 3], b = _triangles[t * 3 + 1], c = _triangles[t * 3 + 2];
                AddAround(a, t);
                AddAround(b, t);
                AddAround(c, t);

                Vector3d cross = Vector3d.Cross(_points[b] - _points[a], _points[c] - _points[a]);
                double area = cross.Length;
                if (area < 1e-14) continue;
                Vector3d n = cross / area;
                double d = -Vector3d.Dot(n, _points[a]);
                AddPlane(a, n, d, area);
                AddPlane(b, n, d, area);
                AddPlane(c, n, d, area);
            }
        }

        private static T[] Rented<T>(int length)
        {
            T[] array = ArrayPool<T>.Shared.Rent(Math.Max(length, 1));
            Array.Clear(array, 0, length);
            return array;
        }

        private void GiveBack()
        {
            ArrayPool<Vector3d>.Shared.Return(_points);
            ArrayPool<int>.Shared.Return(_triangles);
            ArrayPool<bool>.Shared.Return(_dead);
            ArrayPool<double>.Shared.Return(_quadrics);
            ArrayPool<int>.Shared.Return(_version);
            ArrayPool<bool>.Shared.Return(_gone);
            ArrayPool<int>.Shared.Return(_stamp);
            ArrayPool<int>.Shared.Return(_seenStamp);
            ArrayPool<int>.Shared.Return(_start);
            ArrayPool<int>.Shared.Return(_count);
            ArrayPool<int>.Shared.Return(_room);
            ArrayPool<int>.Shared.Return(_arena);
            _queue.Clear();
        }

        private void AddAround(int vertex, int triangle)
        {
            if (_count[vertex] == _room[vertex])
            {
                // Out of room: a run twice as long at the end of the arena, and the old one is left where it was.
                int room = _room[vertex] * 2;
                if (_used + room > _arena.Length)
                {
                    int[] larger = ArrayPool<int>.Shared.Rent(Math.Max(_arena.Length * 2, _used + room));
                    Array.Copy(_arena, larger, _used);
                    ArrayPool<int>.Shared.Return(_arena);
                    _arena = larger;
                }
                Array.Copy(_arena, _start[vertex], _arena, _used, _count[vertex]);
                _start[vertex] = _used;
                _room[vertex] = room;
                _used += room;
            }
            _arena[_start[vertex] + _count[vertex]] = triangle;
            _count[vertex]++;
        }

        private Span<int> Around(int vertex) => _arena.AsSpan(_start[vertex], _count[vertex]);

        /// <summary>Replaces <paramref name="points"/> and <paramref name="triangles"/> with no more than <paramref name="target"/> triangles, if it can get there.</summary>
        /// <param name="longestEdge">How long an edge may get. Something that bends where it's skinned needs vertices along it to bend at, however flat it is there, and a rock needs none.</param>
        /// <param name="pinned">Points that mustn't move, by index, as where one piece of a surface meets the next: no edge of one ever goes.</param>
        /// <param name="open">Whether the surface may have edges with a face on one side only, which then go like any other, held to their line, rather than never.</param>
        /// <param name="mostError">The most a collapse may cost, in square metres times metres of the faces it moves: it stops there, whatever the budget.</param>
        /// <param name="mostNeighbours">The most points one may be joined to, as a flat face would otherwise empty into a fan of needles round one.</param>
        public static void Reduce(List<Vector3> points, List<int> triangles, int target, float longestEdge = float.PositiveInfinity, bool[]? pinned = null, bool open = false,
            double mostError = double.PositiveInfinity, int mostNeighbours = int.MaxValue)
        {
            if (triangles.Count / 3 <= target) return;
            var decimator = new Decimator(points, triangles) { _longestSquared = (double)longestEdge * longestEdge, _pinned = pinned, _open = open, _mostError = mostError, _mostNeighbours = mostNeighbours };
            try
            {
                if (open) decimator.HoldBorders();
                decimator.Run(target);
                decimator.Write(points, triangles);
            }
            finally
            {
                decimator.GiveBack();
            }
        }

        // An open edge gets a plane of its own, upright on its face along it, so that a collapse that would pull it off its line costs what it should (Garland and Heckbert).
        private void HoldBorders()
        {
            Dictionary<long, int> uses = _sharedUses ??= new Dictionary<long, int>(EdgeKeys.Instance);
            uses.Clear();
            for (int t = 0; t < _triangleCount; t++)
            {
                for (int i = 0; i < 3; i++)
                {
                    long key = Key(_triangles[t * 3 + i], _triangles[t * 3 + (i + 1) % 3]);
                    uses[key] = uses.GetValueOrDefault(key) + 1;
                }
            }
            for (int t = 0; t < _triangleCount; t++)
            {
                int a = _triangles[t * 3], b = _triangles[t * 3 + 1], c = _triangles[t * 3 + 2];
                Vector3d face = Vector3d.Cross(_points[b] - _points[a], _points[c] - _points[a]);
                if (face.LengthSquared < 1e-24) continue;
                face = face.Normalized();
                for (int i = 0; i < 3; i++)
                {
                    int p = _triangles[t * 3 + i], q = _triangles[t * 3 + (i + 1) % 3];
                    if (uses[Key(p, q)] != 1) continue;
                    Vector3d edge = _points[q] - _points[p];
                    Vector3d n = Vector3d.Cross(edge, face);
                    double length = n.Length;
                    if (length < 1e-12) continue;
                    n /= length;
                    double d = -Vector3d.Dot(n, _points[p]);
                    double weight = edge.LengthSquared * BorderWeight;
                    AddPlane(p, n, d, weight);
                    AddPlane(q, n, d, weight);
                }
            }
            uses.Clear();
        }

        // An edge's key is its two ends side by side, and a long's own hash folds its halves together, which for two points made near each other is nearly always
        // the same few numbers: a set of a mesh's edges was a few long chains, and building one took most of a second.
        private sealed class EdgeKeys : IEqualityComparer<long>
        {
            public static readonly EdgeKeys Instance = new();
            public bool Equals(long a, long b) => a == b;
            public int GetHashCode(long key)
            {
                ulong h = (ulong)key * 0x9E3779B97F4A7C15ul;
                return (int)(h >> 32) ^ (int)h;
            }
        }

        private static long Key(int a, int b) => a < b ? (long)a << 32 | (uint)b : (long)b << 32 | (uint)a;

        private void AddPlane(int vertex, Vector3d n, double d, double weight)
        {
            int q = vertex * QuadricSize;
            _quadrics[q] += n.X * n.X * weight;
            _quadrics[q + 1] += n.X * n.Y * weight;
            _quadrics[q + 2] += n.X * n.Z * weight;
            _quadrics[q + 3] += n.X * d * weight;
            _quadrics[q + 4] += n.Y * n.Y * weight;
            _quadrics[q + 5] += n.Y * n.Z * weight;
            _quadrics[q + 6] += n.Y * d * weight;
            _quadrics[q + 7] += n.Z * n.Z * weight;
            _quadrics[q + 8] += n.Z * d * weight;
            _quadrics[q + 9] += d * d * weight;
        }

        private double Error(int a, int b, Vector3d v)
        {
            Span<double> q = stackalloc double[QuadricSize];
            for (int i = 0; i < QuadricSize; i++) q[i] = _quadrics[a * QuadricSize + i] + _quadrics[b * QuadricSize + i];
            return q[0] * v.X * v.X + 2.0 * q[1] * v.X * v.Y + 2.0 * q[2] * v.X * v.Z + 2.0 * q[3] * v.X
                + q[4] * v.Y * v.Y + 2.0 * q[5] * v.Y * v.Z + 2.0 * q[6] * v.Y
                + q[7] * v.Z * v.Z + 2.0 * q[8] * v.Z + q[9];
        }

        // The point that strays least from every plane either end has, or failing that the best of the ends and the middle.
        private void Push(int a, int b)
        {
            // A pinned point keeps every edge it has: anything collapsed into one would change which faces meet it, and the piece across the seam has the faces it had.
            if (_pinned != null && (_pinned[a] || _pinned[b])) return;
            int qa = a * QuadricSize, qb = b * QuadricSize;
            double a2 = _quadrics[qa] + _quadrics[qb], ab = _quadrics[qa + 1] + _quadrics[qb + 1], ac = _quadrics[qa + 2] + _quadrics[qb + 2], ad = _quadrics[qa + 3] + _quadrics[qb + 3];
            double b2 = _quadrics[qa + 4] + _quadrics[qb + 4], bc = _quadrics[qa + 5] + _quadrics[qb + 5], bd = _quadrics[qa + 6] + _quadrics[qb + 6];
            double c2 = _quadrics[qa + 7] + _quadrics[qb + 7], cd = _quadrics[qa + 8] + _quadrics[qb + 8];

            double det = a2 * (b2 * c2 - bc * bc) - ab * (ab * c2 - bc * ac) + ac * (ab * bc - b2 * ac);
            Vector3d best;
            double cost;
            Vector3d middle = (_points[a] + _points[b]) * 0.5;
            double scale = a2 * b2 * c2 + 1e-30;
            if (Math.Abs(det) > 1e-6 * Math.Abs(scale))
            {
                double inverse = 1.0 / det;
                best = new Vector3d(
                    -inverse * (ad * (b2 * c2 - bc * bc) - ab * (bd * c2 - bc * cd) + ac * (bd * bc - b2 * cd)),
                    -inverse * (a2 * (bd * c2 - bc * cd) - ad * (ab * c2 - bc * ac) + ac * (ab * cd - bd * ac)),
                    -inverse * (a2 * (b2 * cd - bd * bc) - ab * (ab * cd - bd * ac) + ad * (ab * bc - b2 * ac)));

                // Near-parallel planes meet a long way off, and that's no place for a corner of a rock.
                double reach = (_points[a] - _points[b]).LengthSquared * 4.0;
                if ((best - middle).LengthSquared > reach) best = middle;
                cost = Error(a, b, best);
            }
            else
            {
                best = middle;
                cost = Error(a, b, middle);
                double costA = Error(a, b, _points[a]);
                double costB = Error(a, b, _points[b]);
                if (costA < cost) { cost = costA; best = _points[a]; }
                if (costB < cost) { cost = costB; best = _points[b]; }
            }
            _queue.Enqueue(new Candidate(a, b, _version[a], _version[b], best), Math.Max(cost, 0.0));
        }

        private void Run(int target)
        {
            int alive = _triangleCount;

            // Whether an edge can go depends on what's around it, so one that couldn't a moment ago may be able to now, with nothing about it having changed. So it goes round again until nothing moves.
            for (int pass = 0; pass < MostPasses && alive > target; pass++)
            {
                PushEveryEdge();
                int before = alive;
                while (alive > target && _queue.TryDequeue(out Candidate edge, out double cost))
                {
                    if (cost > _mostError) break;
                    if (_gone[edge.A] || _gone[edge.B] || _version[edge.A] != edge.VersionA || _version[edge.B] != edge.VersionB) continue;
                    if (!Allowed(edge.A, edge.B, edge.Target)) continue;
                    alive -= Collapse(edge.A, edge.B, edge.Target);
                }
                if (alive == before) break;
            }
        }

        private void PushEveryEdge()
        {
            _queue.Clear();
            HashSet<long> edges = _sharedEdges ??= new HashSet<long>(EdgeKeys.Instance);
            edges.Clear();
            for (int t = 0; t < _triangleCount; t++)
            {
                if (_dead[t]) continue;
                for (int i = 0; i < 3; i++)
                {
                    int a = _triangles[t * 3 + i], b = _triangles[t * 3 + (i + 1) % 3];
                    if (edges.Add(Key(a, b))) Push(Math.Min(a, b), Math.Max(a, b));
                }
            }
            edges.Clear();
        }

        private bool Allowed(int a, int b, Vector3d target)
        {
            // An edge has two faces, so its ends can share two neighbours and no more. Anything else is a pinch, and collapsing that would tear the surface.
            _seenNow++;
            Neighbours(a);
            foreach (int v in _neighbours) _seenStamp[v] = _seenNow;
            Neighbours(b);
            int shared = 0;
            foreach (int v in _neighbours) if (_seenStamp[v] == _seenNow) shared++;
            if (_mostNeighbours < int.MaxValue)
            {
                int ofB = 0;
                foreach (int v in _neighbours) if (_seenStamp[v] != _seenNow) ofB++;
                if (ofB + CountOf(a) - 2 > _mostNeighbours) return false;
            }
            if (_open)
            {
                // An open edge has one face, and its ends one neighbour in common. An edge across the middle of a strip joins two open edges, and taking it would pinch the strip.
                int faces = FacesOf(a, b);
                if (faces == 1 ? shared != 1 : shared != 2 || OnBorder(a) && OnBorder(b)) return false;
            }
            else if (shared != 2) return false;

            if (_longestSquared < double.PositiveInfinity)
            {
                Neighbours(b);
                foreach (int v in _neighbours) if ((_points[v] - target).LengthSquared > _longestSquared) return false;
                Neighbours(a);
                foreach (int v in _neighbours) if ((_points[v] - target).LengthSquared > _longestSquared) return false;
            }

            return Keeps(a, b, target) && Keeps(b, a, target);
        }

        private int CountOf(int vertex)
        {
            int count = 0;
            _now++;
            foreach (int t in Around(vertex))
            {
                if (_dead[t]) continue;
                for (int i = 0; i < 3; i++)
                {
                    int v = _triangles[t * 3 + i];
                    if (v == vertex || _stamp[v] == _now) continue;
                    _stamp[v] = _now;
                    count++;
                }
            }
            return count;
        }

        private int FacesOf(int a, int b)
        {
            int faces = 0;
            foreach (int t in Around(a))
            {
                if (_dead[t]) continue;
                if (_triangles[t * 3] == b || _triangles[t * 3 + 1] == b || _triangles[t * 3 + 2] == b) faces++;
            }
            return faces;
        }

        // A point in the middle of a surface has as many neighbours as faces round it, and one on an open edge one more.
        private bool OnBorder(int vertex)
        {
            int faces = 0;
            foreach (int t in Around(vertex)) if (!_dead[t]) faces++;
            return CountOf(vertex) != faces;
        }

        private bool Keeps(int moved, int other, Vector3d target)
        {
            foreach (int t in Around(moved))
            {
                if (_dead[t]) continue;
                int a = _triangles[t * 3], b = _triangles[t * 3 + 1], c = _triangles[t * 3 + 2];
                if (a == other || b == other || c == other) continue;

                Vector3d before = Vector3d.Cross(_points[b] - _points[a], _points[c] - _points[a]);
                Vector3d pa = a == moved ? target : _points[a];
                Vector3d pb = b == moved ? target : _points[b];
                Vector3d pc = c == moved ? target : _points[c];
                Vector3d after = Vector3d.Cross(pb - pa, pc - pa);
                double lengths = before.Length * after.Length;
                if (lengths < 1e-24 || Vector3d.Dot(before, after) < SmallestTurn * lengths) return false;
            }
            return true;
        }

        private void Neighbours(int vertex)
        {
            _neighbours.Clear();
            _now++;
            foreach (int t in Around(vertex))
            {
                if (_dead[t]) continue;
                for (int i = 0; i < 3; i++)
                {
                    int v = _triangles[t * 3 + i];
                    if (v == vertex || _stamp[v] == _now) continue;
                    _stamp[v] = _now;
                    _neighbours.Add(v);
                }
            }
        }

        private int Collapse(int a, int b, Vector3d target)
        {
            int removed = 0;
            _points[a] = target;
            for (int i = 0; i < QuadricSize; i++) _quadrics[a * QuadricSize + i] += _quadrics[b * QuadricSize + i];

            for (int k = 0; k < _count[b]; k++)
            {
                int t = _arena[_start[b] + k];
                if (_dead[t]) continue;
                int first = t * 3;
                if (_triangles[first] == a || _triangles[first + 1] == a || _triangles[first + 2] == a)
                {
                    _dead[t] = true;
                    removed++;
                    continue;
                }
                for (int i = 0; i < 3; i++) if (_triangles[first + i] == b) _triangles[first + i] = a;
                AddAround(a, t);
            }
            _gone[b] = true;
            _count[b] = 0;
            int kept = 0, start = _start[a];
            for (int i = 0; i < _count[a]; i++) if (!_dead[_arena[start + i]]) _arena[start + kept++] = _arena[start + i];
            _count[a] = kept;

            // Only what touches the moved vertex has a new cost. Every other edge is as it was, and stays in the queue.
            _version[a]++;
            Neighbours(a);
            for (int i = 0; i < _neighbours.Count; i++)
            {
                Push(Math.Min(a, _neighbours[i]), Math.Max(a, _neighbours[i]));
            }
            return removed;
        }

        private void Write(List<Vector3> points, List<int> triangles)
        {
            int[] moved = ArrayPool<int>.Shared.Rent(_pointCount);
            Array.Fill(moved, -1, 0, _pointCount);
            points.Clear();
            triangles.Clear();
            for (int t = 0; t < _triangleCount; t++)
            {
                if (_dead[t]) continue;
                for (int i = 0; i < 3; i++)
                {
                    int v = _triangles[t * 3 + i];
                    if (moved[v] < 0)
                    {
                        moved[v] = points.Count;
                        points.Add(new Vector3((float)_points[v].X, (float)_points[v].Y, (float)_points[v].Z));
                    }
                    triangles.Add(moved[v]);
                }
            }
            ArrayPool<int>.Shared.Return(moved);
        }
    }
}

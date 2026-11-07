using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Rock
{
    /// <summary>
    /// Takes a mesh down to a budget by collapsing whichever edge costs least (Garland and Heckbert's quadric error metrics). Every vertex keeps the planes of the faces that met there, and
    /// moving it off them is what costs. That suits rock: a flat face is a single plane however many triangles it had, so it costs nothing to empty it, while a crack or an edge is many
    /// planes at once and keeps its triangles. A rock of a thousand triangles spent like this looks like one of tens of thousands.
    /// </summary>
    internal sealed class Decimator
    {
        private const int QuadricSize = 10;     // A symmetric 4 x 4, kept as a2 ab ac ad b2 bc bd c2 cd d2.
        private const double SmallestTurn = 0.2;    // Cosine of the most a face may turn in one collapse. Further than this and it's folding over.
        private const int MostPasses = 12;

        private readonly Vector3d[] _points;
        private readonly double[] _quadrics;
        private readonly int[] _triangles;
        private readonly bool[] _dead;
        private readonly List<int>[] _around;
        private readonly int[] _version;
        private readonly bool[] _gone;
        private readonly PriorityQueue<Candidate, double> _queue = new();
        private readonly List<int> _neighbours = new();
        private readonly HashSet<int> _seen = new();
        private double _longestSquared = double.PositiveInfinity;

        private readonly record struct Candidate(int A, int B, int VersionA, int VersionB, Vector3d Target);

        private Decimator(List<Vector3> points, List<int> triangles)
        {
            _points = new Vector3d[points.Count];
            for (int i = 0; i < points.Count; i++) _points[i] = new Vector3d(points[i].X, points[i].Y, points[i].Z);

            _triangles = triangles.ToArray();
            _dead = new bool[_triangles.Length / 3];
            _around = new List<int>[points.Count];
            for (int i = 0; i < _around.Length; i++) _around[i] = new List<int>(8);
            _quadrics = new double[points.Count * QuadricSize];
            _version = new int[points.Count];
            _gone = new bool[points.Count];

            for (int t = 0; t < _dead.Length; t++)
            {
                int a = _triangles[t * 3], b = _triangles[t * 3 + 1], c = _triangles[t * 3 + 2];
                _around[a].Add(t);
                _around[b].Add(t);
                _around[c].Add(t);

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

        /// <summary>Replaces <paramref name="points"/> and <paramref name="triangles"/> with no more than <paramref name="target"/> triangles, if it can get there.</summary>
        /// <param name="longestEdge">How long an edge may get. Something that bends where it's skinned needs vertices along it to bend at, however flat it is there, and a rock needs none.</param>
        public static void Reduce(List<Vector3> points, List<int> triangles, int target, float longestEdge = float.PositiveInfinity)
        {
            if (triangles.Count / 3 <= target) return;
            var decimator = new Decimator(points, triangles) { _longestSquared = (double)longestEdge * longestEdge };
            decimator.Run(target);
            decimator.Write(points, triangles);
        }

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
            int alive = _dead.Length;

            // Whether an edge can go depends on what's around it, so one that couldn't a moment ago may be able to now, with nothing about it having changed. So it goes round again until nothing moves.
            for (int pass = 0; pass < MostPasses && alive > target; pass++)
            {
                PushEveryEdge();
                int before = alive;
                while (alive > target && _queue.TryDequeue(out Candidate edge, out _))
                {
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
            var edges = new HashSet<long>();
            for (int t = 0; t < _dead.Length; t++)
            {
                if (_dead[t]) continue;
                for (int i = 0; i < 3; i++)
                {
                    int a = _triangles[t * 3 + i], b = _triangles[t * 3 + (i + 1) % 3];
                    long key = a < b ? (long)a << 32 | (uint)b : (long)b << 32 | (uint)a;
                    if (edges.Add(key)) Push(Math.Min(a, b), Math.Max(a, b));
                }
            }
        }

        private bool Allowed(int a, int b, Vector3d target)
        {
            // An edge has two faces, so its ends can share two neighbours and no more. Anything else is a pinch, and collapsing that would tear the surface.
            _seen.Clear();
            Neighbours(a);
            foreach (int v in _neighbours) _seen.Add(v);
            Neighbours(b);
            int shared = 0;
            foreach (int v in _neighbours) if (_seen.Contains(v)) shared++;
            if (shared != 2) return false;

            if (_longestSquared < double.PositiveInfinity)
            {
                foreach (int v in _neighbours) if ((_points[v] - target).LengthSquared > _longestSquared) return false;
                Neighbours(a);
                foreach (int v in _neighbours) if ((_points[v] - target).LengthSquared > _longestSquared) return false;
            }

            return Keeps(a, b, target) && Keeps(b, a, target);
        }

        private bool Keeps(int moved, int other, Vector3d target)
        {
            foreach (int t in _around[moved])
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
            foreach (int t in _around[vertex])
            {
                if (_dead[t]) continue;
                for (int i = 0; i < 3; i++)
                {
                    int v = _triangles[t * 3 + i];
                    if (v != vertex && !_neighbours.Contains(v)) _neighbours.Add(v);
                }
            }
        }

        private int Collapse(int a, int b, Vector3d target)
        {
            int removed = 0;
            _points[a] = target;
            for (int i = 0; i < QuadricSize; i++) _quadrics[a * QuadricSize + i] += _quadrics[b * QuadricSize + i];

            foreach (int t in _around[b])
            {
                if (_dead[t]) continue;
                int first = t * 3;
                if (_triangles[first] == a || _triangles[first + 1] == a || _triangles[first + 2] == a)
                {
                    _dead[t] = true;
                    removed++;
                    continue;
                }
                for (int i = 0; i < 3; i++) if (_triangles[first + i] == b) _triangles[first + i] = a;
                _around[a].Add(t);
            }
            _gone[b] = true;
            _around[b].Clear();
            _around[a].RemoveAll(t => _dead[t]);

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
            int[] moved = new int[_points.Length];
            Array.Fill(moved, -1);
            points.Clear();
            triangles.Clear();
            for (int t = 0; t < _dead.Length; t++)
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
        }
    }
}

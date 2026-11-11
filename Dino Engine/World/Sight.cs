using OpenTK.Mathematics;

namespace Dino_Engine.World
{
    /// <summary>
    /// What a blow can reach from where it goes off: everything a straight line from there gets to without crossing anything solid that stands taller than <see cref="Above"/>. The server tests every area
    /// against this (<c>Combat.Within</c>) and a client cuts the very same blockers out of the mark it draws (<see cref="Shadow"/>), so someone behind a pillar is out of what's marked and out of what lands.
    /// Headless, and the same on every machine, as both ends read the same world.
    /// </summary>
    public static class Sight
    {
        /// <summary>Metres something solid has to stand above the ground before it's in the way: a fence, a pillar, a wall or a boulder, but not a stone in the grass. Posts and poles are none (their height is 0).</summary>
        public const float Above = 1f;

        /// <summary>The most blockers one blow minds, nearest first. It's the same number on both ends, so it can't make them disagree.</summary>
        public const int MostBlockers = 128;

        /// <summary>Metres a box is grown by before it's in the way, so that two that meet leave no line between them.</summary>
        public const float Seal = 0.1f;

        /// <summary>Metres a shadow is drawn out to, past anything a blow could reach.</summary>
        public const float Far = 400f;

        /// <summary>The most corners a <see cref="Shadow"/> has: a box seen corner on, and the far ends of all four of its corners.</summary>
        public const int MostCorners = 8;

        [ThreadStatic] private static List<(Footprint Shape, float Height)>? _solids;
        [ThreadStatic] private static List<(float Distance, Footprint Shape)>? _sorted;

        /// <summary>
        /// Everything that can stand between <paramref name="origin"/> and something within <paramref name="reach"/> of it, nearest first. Whatever the origin is inside doesn't count, as a blow going off against a wall
        /// still reaches out of it on the open side.
        /// </summary>
        public static void Gather(PlacementIndex placement, Vector2 origin, float reach, List<Footprint> blockers)
        {
            blockers.Clear();
            List<(Footprint Shape, float Height)> solids = _solids ??= new();
            List<(float Distance, Footprint Shape)> sorted = _sorted ??= new();
            solids.Clear();
            sorted.Clear();

            placement.CollectSolids(origin, reach, solids);
            Footprint area = Footprint.Circle(origin, reach);
            foreach ((Footprint solid, float height) in solids)
            {
                // Runs of fence and wall meet end to end, and a line through just where they meet only grazes each: a box is grown a little, so neighbours overlap and nothing leaks between them.
                Footprint shape = solid.IsCircle ? solid : solid.Grown(Seal);
                if (height <= Above || Contains(shape, origin) || !area.Overlaps(shape)) continue;
                sorted.Add((Nearest(shape, origin), shape));
            }
            sorted.Sort(static (a, b) => a.Distance.CompareTo(b.Distance));
            int count = Math.Min(sorted.Count, MostBlockers);
            for (int i = 0; i < count; i++) blockers.Add(sorted[i].Shape);
        }

        /// <summary>Whether a straight line from <paramref name="origin"/> gets to <paramref name="point"/> without going into any of <paramref name="blockers"/>.</summary>
        public static bool Clear(List<Footprint> blockers, Vector2 origin, Vector2 point)
        {
            for (int i = 0; i < blockers.Count; i++)
            {
                if (Crosses(blockers[i], origin, point)) return false;
            }
            return true;
        }

        /// <summary>Whether the segment from <paramref name="a"/> to <paramref name="b"/> goes into <paramref name="shape"/>.</summary>
        public static bool Crosses(in Footprint shape, Vector2 a, Vector2 b)
        {
            if (shape.IsCircle)
            {
                Vector2 along = b - a;
                float lengthSquared = along.LengthSquared;
                float t = lengthSquared > 1e-10f ? Math.Clamp(Vector2.Dot(shape.Centre - a, along) / lengthSquared, 0f, 1f) : 0f;
                float radius = shape.HalfExtents.X;
                return (a + along * t - shape.Centre).LengthSquared < radius * radius;
            }

            // Slabs, in the box's own frame.
            Vector2 from = shape.ToLocal(a);
            Vector2 to = shape.ToLocal(b);
            Vector2 way = to - from;
            float enter = 0f, leave = 1f;
            for (int axis = 0; axis < 2; axis++)
            {
                float start = axis == 0 ? from.X : from.Y;
                float step = axis == 0 ? way.X : way.Y;
                float half = axis == 0 ? shape.HalfExtents.X : shape.HalfExtents.Y;
                if (MathF.Abs(step) < 1e-9f)
                {
                    if (MathF.Abs(start) >= half) return false;
                    continue;
                }
                float t0 = (-half - start) / step;
                float t1 = (half - start) / step;
                if (t0 > t1) (t0, t1) = (t1, t0);
                enter = MathF.Max(enter, t0);
                leave = MathF.Min(leave, t1);
                if (enter >= leave) return false;
            }
            return true;
        }

        /// <summary>How far along the line from <paramref name="a"/> to <paramref name="b"/> it first goes into anything, from 0 to 1, or 1 if it never does.</summary>
        public static float FirstBlocked(List<Footprint> blockers, Vector2 a, Vector2 b)
        {
            float clear = 0f, inside = 1f;
            if (Clear(blockers, a, b)) return 1f;
            // Halving: the first crossing is where a shorter line stops being clear.
            for (int i = 0; i < 20; i++)
            {
                float middle = (clear + inside) * 0.5f;
                if (Clear(blockers, a, a + (b - a) * middle)) clear = middle;
                else inside = middle;
            }
            return clear;
        }

        public static bool Contains(in Footprint shape, Vector2 point)
        {
            if (shape.IsCircle) return (point - shape.Centre).LengthSquared < shape.HalfExtents.X * shape.HalfExtents.X;
            Vector2 local = shape.ToLocal(point);
            return MathF.Abs(local.X) < shape.HalfExtents.X && MathF.Abs(local.Y) < shape.HalfExtents.Y;
        }

        private static float Nearest(in Footprint shape, Vector2 point)
        {
            if (shape.IsCircle) return MathF.Max((point - shape.Centre).Length - shape.HalfExtents.X, 0f);
            Vector2 local = shape.ToLocal(point);
            float dx = MathF.Max(MathF.Abs(local.X) - shape.HalfExtents.X, 0f);
            float dz = MathF.Max(MathF.Abs(local.Y) - shape.HalfExtents.Y, 0f);
            return MathF.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// Everything a blocker hides from <paramref name="origin"/>, for drawing: the set of points a line from the origin reaches only through it, which is convex. It's a polygon of up to <see cref="MostCorners"/>
        /// <paramref name="corners"/>, anticlockwise, and for a round blocker the circle itself as well (<paramref name="circle"/>, its radius in w, or 0 for none). The points whose line crosses it
        /// are exactly those in the union, which is what <see cref="Crosses"/> tests.
        /// </summary>
        /// <returns>How many corners.</returns>
        public static int Shadow(in Footprint shape, Vector2 origin, Span<Vector2> corners, out Vector3 circle)
        {
            Span<Vector2> points = stackalloc Vector2[8];
            int count;
            if (shape.IsCircle)
            {
                Vector2 offset = shape.Centre - origin;
                float distance = offset.Length;
                float radius = shape.HalfExtents.X;
                circle = new Vector3(shape.Centre.X, shape.Centre.Y, radius);
                if (distance <= radius + 1e-4f)
                {
                    // Gather never gives one the origin is in.
                    return 0;
                }
                // Where the lines from the origin touch it.
                float sine = radius / distance;
                float cosine = MathF.Sqrt(MathF.Max(1f - sine * sine, 0f));
                Vector2 toward = offset / distance;
                float tangent = distance * cosine;
                Vector2 left = Rotate(toward, cosine, sine) * tangent + origin;
                Vector2 right = Rotate(toward, cosine, -sine) * tangent + origin;
                points[0] = left;
                points[1] = right;
                points[2] = Out(left, origin);
                points[3] = Out(right, origin);
                count = 4;
            }
            else
            {
                circle = Vector3.Zero;
                Vector2 h = shape.HalfExtents;
                for (int i = 0; i < 4; i++)
                {
                    Vector2 corner = shape.ToWorld(new Vector2((i & 1) == 0 ? -h.X : h.X, (i & 2) == 0 ? -h.Y : h.Y));
                    points[i] = corner;
                    points[i + 4] = Out(corner, origin);
                }
                count = 8;
            }
            return Hull(points[..count], corners);
        }

        private static Vector2 Rotate(Vector2 v, float cosine, float sine) => new Vector2(v.X * cosine - v.Y * sine, v.X * sine + v.Y * cosine);

        private static Vector2 Out(Vector2 point, Vector2 origin)
        {
            Vector2 way = point - origin;
            float length = way.Length;
            return length > 1e-6f ? point + way / length * Far : point;
        }

        // Andrew's monotone chain, anticlockwise.
        private static int Hull(Span<Vector2> points, Span<Vector2> hull)
        {
            // Insertion sort by x then y: there are at most eight.
            for (int i = 1; i < points.Length; i++)
            {
                Vector2 p = points[i];
                int j = i - 1;
                while (j >= 0 && (points[j].X > p.X || (points[j].X == p.X && points[j].Y > p.Y)))
                {
                    points[j + 1] = points[j];
                    j--;
                }
                points[j + 1] = p;
            }

            Span<Vector2> chain = stackalloc Vector2[points.Length * 2];
            int k = 0;
            for (int i = 0; i < points.Length; i++)
            {
                while (k >= 2 && Cross(chain[k - 2], chain[k - 1], points[i]) <= 0f) k--;
                chain[k++] = points[i];
            }
            for (int i = points.Length - 2, lower = k + 1; i >= 0; i--)
            {
                while (k >= lower && Cross(chain[k - 2], chain[k - 1], points[i]) <= 0f) k--;
                chain[k++] = points[i];
            }
            int count = Math.Min(k - 1, hull.Length);
            for (int i = 0; i < count; i++) hull[i] = chain[i];
            return count;
        }

        private static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
    }
}

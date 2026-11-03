using OpenTK.Mathematics;

namespace Dino_Engine.World
{
    /// <summary>What a footprint belongs to, so that a check can pick what it collides with.</summary>
    [Flags]
    public enum PlacementLayer
    {
        None = 0,
        Tree = 1 << 0,
        Shrub = 1 << 1,
        Rock = 1 << 2,
        Deadwood = 1 << 3,
        Structure = 1 << 4,
        Prop = 1 << 5,
        Fence = 1 << 6,
        Field = 1 << 7,
        Keepout = 1 << 8,   // Reserved ground with nothing on it: yards, plazas, the mouth of a gate.
        Road = 1 << 9,

        Vegetation = Tree | Shrub | Deadwood,
        Built = Structure | Prop | Fence | Field | Keepout,
        All = ~0,
    }

    /// <summary>The ground an object stands on: a circle, or a box turned about the vertical axis.</summary>
    public readonly struct Footprint
    {
        public readonly Vector2 Centre;
        public readonly Vector2 HalfExtents;    // Along the box's own axes. For a circle, X is the radius and Y is 0.
        public readonly Vector2 Axis;           // Unit, the box's local x axis on the xz plane.
        public readonly bool IsCircle;

        private Footprint(Vector2 centre, Vector2 halfExtents, Vector2 axis, bool isCircle)
        {
            Centre = centre;
            HalfExtents = halfExtents;
            Axis = axis;
            IsCircle = isCircle;
        }

        public static Footprint Circle(Vector2 centre, float radius)
        {
            return new Footprint(centre, new Vector2(radius, 0f), Vector2.UnitX, true);
        }

        /// <param name="yaw">Radians about +y, as in a Y rotation of the object: the box's x axis ends up at (cos, -sin) on xz.</param>
        public static Footprint Box(Vector2 centre, Vector2 halfExtents, float yaw)
        {
            return new Footprint(centre, halfExtents, new Vector2(MathF.Cos(yaw), -MathF.Sin(yaw)), false);
        }

        /// <summary>A thin box along a line, for walls and fences.</summary>
        public static Footprint Segment(Vector2 a, Vector2 b, float halfThickness)
        {
            Vector2 along = b - a;
            float length = along.Length;
            Vector2 axis = length > 1e-5f ? along / length : Vector2.UnitX;
            return new Footprint((a + b) * 0.5f, new Vector2(length * 0.5f, halfThickness), axis, false);
        }

        /// <summary>Radius of the smallest circle around <see cref="Centre"/> holding all of it.</summary>
        public float BoundingRadius => IsCircle ? HalfExtents.X : HalfExtents.Length;

        public Vector2 PerpendicularAxis => new Vector2(-Axis.Y, Axis.X);

        public Vector2 ToLocal(Vector2 point)
        {
            Vector2 offset = point - Centre;
            return new Vector2(Vector2.Dot(offset, Axis), Vector2.Dot(offset, PerpendicularAxis));
        }

        public Vector2 ToWorld(Vector2 local) => Centre + Axis * local.X + PerpendicularAxis * local.Y;

        public bool Overlaps(in Footprint other, float padding = 0f)
        {
            if (IsCircle && other.IsCircle)
            {
                float reach = HalfExtents.X + other.HalfExtents.X + padding;
                return reach > 0f && (Centre - other.Centre).LengthSquared < reach * reach;
            }
            if (IsCircle) return other.OverlapsCircle(Centre, HalfExtents.X + padding);
            if (other.IsCircle) return OverlapsCircle(other.Centre, other.HalfExtents.X + padding);
            return OverlapsBox(other, padding);
        }

        private bool OverlapsCircle(Vector2 centre, float radius)
        {
            if (radius <= 0f) return false;
            Vector2 local = ToLocal(centre);
            float dx = MathF.Max(MathF.Abs(local.X) - HalfExtents.X, 0f);
            float dz = MathF.Max(MathF.Abs(local.Y) - HalfExtents.Y, 0f);
            return dx * dx + dz * dz < radius * radius;
        }

        // Separating axis test: two boxes are apart if and only if one of their four edge directions separates them.
        private bool OverlapsBox(in Footprint other, float padding)
        {
            Vector2 offset = other.Centre - Centre;
            return !Separated(Axis, offset, other, padding)
                && !Separated(PerpendicularAxis, offset, other, padding)
                && !Separated(other.Axis, offset, other, padding)
                && !Separated(other.PerpendicularAxis, offset, other, padding);
        }

        private bool Separated(Vector2 axis, Vector2 offset, in Footprint other, float padding)
        {
            float reach = ProjectedReach(axis) + other.ProjectedReach(axis) + padding;
            return MathF.Abs(Vector2.Dot(offset, axis)) >= reach;
        }

        private float ProjectedReach(Vector2 axis)
        {
            return HalfExtents.X * MathF.Abs(Vector2.Dot(Axis, axis)) + HalfExtents.Y * MathF.Abs(Vector2.Dot(PerpendicularAxis, axis));
        }

        /// <summary>
        /// The shortest push that moves a circle out of this footprint. False if the circle isn't touching it.
        /// </summary>
        public bool TryPushOut(Vector2 centre, float radius, out Vector2 push)
        {
            if (IsCircle)
            {
                Vector2 offset = centre - Centre;
                float reach = HalfExtents.X + radius;
                float distanceSquared = offset.LengthSquared;
                if (distanceSquared >= reach * reach)
                {
                    push = default;
                    return false;
                }
                float distance = MathF.Sqrt(distanceSquared);
                Vector2 direction = distance > 1e-5f ? offset / distance : Vector2.UnitX;
                push = direction * (reach - distance);
                return true;
            }

            Vector2 local = ToLocal(centre);
            float clampedX = Math.Clamp(local.X, -HalfExtents.X, HalfExtents.X);
            float clampedZ = Math.Clamp(local.Y, -HalfExtents.Y, HalfExtents.Y);
            Vector2 outside = local - new Vector2(clampedX, clampedZ);
            float outsideSquared = outside.LengthSquared;

            if (outsideSquared > 1e-10f)
            {
                if (outsideSquared >= radius * radius)
                {
                    push = default;
                    return false;
                }
                float distance = MathF.Sqrt(outsideSquared);
                Vector2 localPush = outside / distance * (radius - distance);
                push = Axis * localPush.X + PerpendicularAxis * localPush.Y;
                return true;
            }

            // The centre is inside the box: leave through the nearest side.
            float toX = HalfExtents.X - MathF.Abs(local.X);
            float toZ = HalfExtents.Y - MathF.Abs(local.Y);
            push = toX < toZ
                ? Axis * (MathF.Sign(local.X == 0f ? 1f : local.X) * (toX + radius))
                : PerpendicularAxis * (MathF.Sign(local.Y == 0f ? 1f : local.Y) * (toZ + radius));
            return true;
        }
    }

    /// <summary>
    /// Every footprint in a world, in a uniform grid. All placement goes through it, across every layer and the whole world
    /// at once, so nothing can end up inside anything else; a footprint marked solid also blocks characters at run time.
    /// </summary>
    public sealed class PlacementIndex
    {
        private const float CellSize = 8f;

        private readonly struct Entry
        {
            public readonly Footprint Footprint;
            public readonly PlacementLayer Layer;
            public readonly bool Solid;

            public Entry(in Footprint footprint, PlacementLayer layer, bool solid)
            {
                Footprint = footprint;
                Layer = layer;
                Solid = solid;
            }
        }

        private readonly int _cellsPerSide;
        private readonly List<int>?[] _cells;
        private readonly List<Entry> _entries = new();
        // Marks entries already seen in the current query; big footprints sit in several cells.
        private readonly List<int> _visitStamp = new();
        private int _stamp;

        public PlacementIndex(float worldSize)
        {
            _cellsPerSide = (int)MathF.Ceiling(worldSize / CellSize) + 1;
            _cells = new List<int>?[_cellsPerSide * _cellsPerSide];
        }

        public int Count => _entries.Count;

        public void Add(in Footprint footprint, PlacementLayer layer, bool solid = false)
        {
            int index = _entries.Count;
            _entries.Add(new Entry(footprint, layer, solid));
            _visitStamp.Add(0);

            float reach = footprint.BoundingRadius;
            CellRange(footprint.Centre, reach, out int minX, out int minZ, out int maxX, out int maxZ);
            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int cell = z * _cellsPerSide + x;
                    (_cells[cell] ??= new List<int>(4)).Add(index);
                }
            }
        }

        /// <summary>Whether <paramref name="footprint"/>, grown by <paramref name="padding"/>, touches anything in <paramref name="layers"/>.</summary>
        public bool Overlaps(in Footprint footprint, PlacementLayer layers, float padding = 0f)
        {
            _stamp++;
            float reach = footprint.BoundingRadius + padding + MaxReachInCell;
            CellRange(footprint.Centre, reach, out int minX, out int minZ, out int maxX, out int maxZ);
            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    List<int>? cell = _cells[z * _cellsPerSide + x];
                    if (cell == null) continue;
                    for (int i = 0; i < cell.Count; i++)
                    {
                        int index = cell[i];
                        if (_visitStamp[index] == _stamp) continue;
                        _visitStamp[index] = _stamp;
                        Entry entry = _entries[index];
                        if ((entry.Layer & layers) != 0 && footprint.Overlaps(entry.Footprint, padding)) return true;
                    }
                }
            }
            return false;
        }

        // Entries sit in every cell they reach, so a query only needs its own reach, with a hair of slack for rounding.
        private const float MaxReachInCell = 0.01f;

        /// <summary>Whether <paramref name="footprint"/> touches anything solid.</summary>
        public bool OverlapsSolid(in Footprint footprint)
        {
            _stamp++;
            CellRange(footprint.Centre, footprint.BoundingRadius + MaxReachInCell, out int minX, out int minZ, out int maxX, out int maxZ);
            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    List<int>? cell = _cells[z * _cellsPerSide + x];
                    if (cell == null) continue;
                    for (int i = 0; i < cell.Count; i++)
                    {
                        int index = cell[i];
                        if (_visitStamp[index] == _stamp) continue;
                        _visitStamp[index] = _stamp;
                        Entry entry = _entries[index];
                        if (entry.Solid && footprint.Overlaps(entry.Footprint)) return true;
                    }
                }
            }
            return false;
        }

        /// <summary>Adds every solid footprint near <paramref name="centre"/> to <paramref name="results"/>. Safe to call from one thread at a time.</summary>
        public void CollectSolids(Vector2 centre, float radius, List<Footprint> results)
        {
            _stamp++;
            CellRange(centre, radius, out int minX, out int minZ, out int maxX, out int maxZ);
            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    List<int>? cell = _cells[z * _cellsPerSide + x];
                    if (cell == null) continue;
                    for (int i = 0; i < cell.Count; i++)
                    {
                        int index = cell[i];
                        if (_visitStamp[index] == _stamp) continue;
                        _visitStamp[index] = _stamp;
                        Entry entry = _entries[index];
                        if (entry.Solid) results.Add(entry.Footprint);
                    }
                }
            }
        }

        /// <summary>Calls <paramref name="visit"/> for every footprint, for validation and debug drawing.</summary>
        public void ForEach(Action<Footprint, PlacementLayer, bool> visit)
        {
            foreach (Entry entry in _entries) visit(entry.Footprint, entry.Layer, entry.Solid);
        }

        private void CellRange(Vector2 centre, float reach, out int minX, out int minZ, out int maxX, out int maxZ)
        {
            minX = Clamp((int)MathF.Floor((centre.X - reach) / CellSize));
            minZ = Clamp((int)MathF.Floor((centre.Y - reach) / CellSize));
            maxX = Clamp((int)MathF.Floor((centre.X + reach) / CellSize));
            maxZ = Clamp((int)MathF.Floor((centre.Y + reach) / CellSize));
        }

        private int Clamp(int cell) => cell < 0 ? 0 : cell >= _cellsPerSide ? _cellsPerSide - 1 : cell;
    }
}

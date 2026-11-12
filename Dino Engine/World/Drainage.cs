using OpenTK.Mathematics;
using static Dino_Engine.World.WorldMath;

namespace Dino_Engine.World
{
    /// <summary>
    /// The ways water leaves the mountains (MountainStage): every cell of the coarse map in rock that gathers enough water to cut a channel, joined to the one it drains into,
    /// with how much drains through it. RockSculpt cuts ravines along these, never letting a floor sink below where its water leaves the rock, so that no ravine is a pit.
    /// Headless, and read from many threads at once once it's built.
    /// </summary>
    public sealed class Drainage
    {
        /// <summary>Cells of the coarse map that have to drain through a cell before it cuts a channel worth showing.</summary>
        public const float LeastArea = 6f;
        /// <summary>And before it cuts a ravine as deep and wide as a form's can go.</summary>
        public const float BiggestArea = 420f;
        /// <summary>How steeply a ravine's floor has to keep falling towards where it leaves the rock, at the least.</summary>
        public const float LeastFall = 0.025f;

        /// <summary>One reach of a channel, from a cell to the one it drains into, smoothed out of the map's eight directions.</summary>
        public readonly record struct Reach(Vector2 From, Vector2 To, float Area, float AreaTo, float Height, float HeightTo, float Floor, float FloorTo);

        public readonly List<Reach> Reaches = new();

        private const float IndexCell = 8f;
        private int _width;
        private int[] _start = Array.Empty<int>();
        private int[] _items = Array.Empty<int>();

        /// <summary>How big a channel is for its area, 0 for one barely worth showing and 1 for the biggest.</summary>
        public static float Size(float area) => Saturate(MathF.Log(MathF.Max(area, LeastArea) / LeastArea) / MathF.Log(BiggestArea / LeastArea));

        /// <summary>Indexes the reaches for <see cref="Nearest"/>. Call once every reach is in.</summary>
        public void Index(float worldSize)
        {
            _width = Math.Max((int)MathF.Ceiling(worldSize / IndexCell), 1);
            int cells = _width * _width;
            var lists = new List<int>[cells];
            for (int r = 0; r < Reaches.Count; r++)
            {
                Reach reach = Reaches[r];
                Vector2 min = Vector2.ComponentMin(reach.From, reach.To);
                Vector2 max = Vector2.ComponentMax(reach.From, reach.To);
                int x0 = Math.Clamp((int)(min.X / IndexCell), 0, _width - 1), x1 = Math.Clamp((int)(max.X / IndexCell), 0, _width - 1);
                int z0 = Math.Clamp((int)(min.Y / IndexCell), 0, _width - 1), z1 = Math.Clamp((int)(max.Y / IndexCell), 0, _width - 1);
                for (int z = z0; z <= z1; z++)
                {
                    for (int x = x0; x <= x1; x++) (lists[z * _width + x] ??= new List<int>()).Add(r);
                }
            }
            _start = new int[cells + 1];
            for (int i = 0; i < cells; i++) _start[i + 1] = _start[i] + (lists[i]?.Count ?? 0);
            _items = new int[_start[cells]];
            for (int i = 0; i < cells; i++)
            {
                if (lists[i] == null) continue;
                lists[i].CopyTo(_items, _start[i]);
            }
        }

        /// <summary>The reach nearest a point within <paramref name="within"/> metres, how far it is, and how far along it the nearest point is.</summary>
        public bool Nearest(Vector2 p, float within, out Reach nearest, out float distance, out float along)
        {
            nearest = default;
            distance = float.MaxValue;
            along = 0f;
            if (_items.Length == 0) return false;
            int reachCells = (int)MathF.Ceiling(within / IndexCell);
            int cx = (int)(p.X / IndexCell), cz = (int)(p.Y / IndexCell);
            int found = -1;
            for (int z = Math.Max(cz - reachCells, 0); z <= Math.Min(cz + reachCells, _width - 1); z++)
            {
                for (int x = Math.Max(cx - reachCells, 0); x <= Math.Min(cx + reachCells, _width - 1); x++)
                {
                    int cell = z * _width + x;
                    for (int k = _start[cell]; k < _start[cell + 1]; k++)
                    {
                        int r = _items[k];
                        Reach reach = Reaches[r];
                        float d = DistanceToSegment(p, reach.From, reach.To, out float t);
                        if (d >= distance || d > within) continue;
                        distance = d;
                        along = t;
                        found = r;
                    }
                }
            }
            if (found < 0) return false;
            nearest = Reaches[found];
            return true;
        }
    }
}

using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Physics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Dino_Engine.Util
{
    public class SpatialGrid
    {
        private readonly float _cellSize;
        private readonly int _maxCellSpanPerAxis;

        // Hash map of 3D cell coordinates to entities occupying that cell
        private readonly Dictionary<Vector3i, List<Entity>> _gridCells = new();

        // Tracks the current cell footprint of every registered entity for fast removal/updates
        private readonly Dictionary<Entity, CellRange> _entityCellRanges = new();

        // Catch-all list for oversized entities that span too many cells (e.g. mega-structures)
        private readonly List<Entity> _globalOverflowEntities = new();

        // Cached set used solely to deduplicate multi-cell entities during queries (zero GC allocations)
        private readonly HashSet<Entity> _queryVisitedSet = new();

        public struct CellRange : IEquatable<CellRange>
        {
            public Vector3i MinCell;
            public Vector3i MaxCell;
            public bool IsOverflow;

            public bool Equals(CellRange other) =>
                MinCell.Equals(other.MinCell) &&
                MaxCell.Equals(other.MaxCell) &&
                IsOverflow == other.IsOverflow;

            public override bool Equals(object obj) => obj is CellRange other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(MinCell, MaxCell, IsOverflow);
        }

        /// <summary>
        /// Creates a spatial grid with specified cell sizing.
        /// </summary>
        /// <param name="cellSize">Size of each cubic cell in world units (e.g. 25.0f for rendering, 5.0f for physics).</param>
        /// <param name="maxCellSpanPerAxis">Maximum cell span along any single axis before an entity is routed to global overflow.</param>
        public SpatialGrid(float cellSize = 25.0f, int maxCellSpanPerAxis = 4)
        {
            _cellSize = cellSize;
            _maxCellSpanPerAxis = maxCellSpanPerAxis;
        }

        /// <summary>
        /// Converts a world-space position into a 3D grid cell coordinate.
        /// </summary>
        public Vector3i GetCellCoords(Vector3 worldPos)
        {
            return new Vector3i(
                (int)MathF.Floor(worldPos.X / _cellSize),
                (int)MathF.Floor(worldPos.Y / _cellSize),
                (int)MathF.Floor(worldPos.Z / _cellSize)
            );
        }

        /// <summary>
        /// Inserts a new entity or updates an existing entity's cell range based on its world-space AABB.
        /// Returns early if the entity has not crossed a cell boundary.
        /// </summary>
        public void InsertOrUpdate(Entity entity, AABB worldBounds)
        {
            Vector3i minCell = GetCellCoords(worldBounds.Min);
            Vector3i maxCell = GetCellCoords(worldBounds.Max);

            int spanX = maxCell.X - minCell.X + 1;
            int spanY = maxCell.Y - minCell.Y + 1;
            int spanZ = maxCell.Z - minCell.Z + 1;

            bool isOverflow = spanX > _maxCellSpanPerAxis ||
                              spanY > _maxCellSpanPerAxis ||
                              spanZ > _maxCellSpanPerAxis;

            CellRange newRange = new CellRange
            {
                MinCell = minCell,
                MaxCell = maxCell,
                IsOverflow = isOverflow
            };

            // 1. Check if the entity is already indexed
            if (_entityCellRanges.TryGetValue(entity, out var currentRange))
            {
                // EARLY EXIT: If position changed slightly but cell range is identical, do nothing!
                if (currentRange.Equals(newRange))
                    return;

                // Remove from old cells before applying new range
                RemoveEntityFromRange(entity, currentRange);
            }

            // 2. Register the new cell range
            _entityCellRanges[entity] = newRange;

            // 3. Insert into grid cells or overflow list
            if (isOverflow)
            {
                _globalOverflowEntities.Add(entity);
            }
            else
            {
                for (int x = minCell.X; x <= maxCell.X; x++)
                    for (int y = minCell.Y; y <= maxCell.Y; y++)
                        for (int z = minCell.Z; z <= maxCell.Z; z++)
                        {
                            Vector3i cellKey = new(x, y, z);
                            if (!_gridCells.TryGetValue(cellKey, out var list))
                            {
                                list = new List<Entity>(16);
                                _gridCells[cellKey] = list;
                            }
                            list.Add(entity);
                        }
            }
        }

        /// <summary>
        /// Completely removes an entity from the grid (call upon entity destruction).
        /// </summary>
        public void Remove(Entity entity)
        {
            if (_entityCellRanges.TryGetValue(entity, out var range))
            {
                RemoveEntityFromRange(entity, range);
                _entityCellRanges.Remove(entity);
            }
        }

        private void RemoveEntityFromRange(Entity entity, CellRange range)
        {
            if (range.IsOverflow)
            {
                _globalOverflowEntities.Remove(entity);
            }
            else
            {
                for (int x = range.MinCell.X; x <= range.MaxCell.X; x++)
                    for (int y = range.MinCell.Y; y <= range.MaxCell.Y; y++)
                        for (int z = range.MinCell.Z; z <= range.MaxCell.Z; z++)
                        {
                            Vector3i cellKey = new(x, y, z);
                            if (_gridCells.TryGetValue(cellKey, out var list))
                            {
                                list.Remove(entity);
                            }
                        }
            }
        }

        // --- QUERY API ---

        /// <summary>
        /// Queries all entities overlapping a spherical area (e.g., Point Light radius).
        /// Automatically deduplicates entities spanning multiple cells.
        /// </summary>
        public void QuerySphere(Vector3 center, float radius, List<Entity> results)
        {
            results.Clear();
            _queryVisitedSet.Clear();

            // Always add global overflow entities (deduplicated)
            for (int i = 0; i < _globalOverflowEntities.Count; i++)
            {
                Entity overflowEntity = _globalOverflowEntities[i];
                if (_queryVisitedSet.Add(overflowEntity))
                {
                    results.Add(overflowEntity);
                }
            }

            Vector3i minCell = GetCellCoords(center - new Vector3(radius));
            Vector3i maxCell = GetCellCoords(center + new Vector3(radius));

            for (int x = minCell.X; x <= maxCell.X; x++)
                for (int y = minCell.Y; y <= maxCell.Y; y++)
                    for (int z = minCell.Z; z <= maxCell.Z; z++)
                    {
                        Vector3i cellKey = new(x, y, z);
                        if (_gridCells.TryGetValue(cellKey, out var list))
                        {
                            for (int i = 0; i < list.Count; i++)
                            {
                                Entity e = list[i];
                                // HashSet.Add returns true if item was not present; prevents duplicate additions
                                if (_queryVisitedSet.Add(e))
                                {
                                    results.Add(e);
                                }
                            }
                        }
                    }
        }

        /// <summary>
        /// Queries all entities overlapping a 3D Axis-Aligned Bounding Box.
        /// </summary>
        public void QueryAABB(AABB bounds, List<Entity> results)
        {
            results.Clear();
            _queryVisitedSet.Clear();

            for (int i = 0; i < _globalOverflowEntities.Count; i++)
            {
                Entity overflowEntity = _globalOverflowEntities[i];
                if (_queryVisitedSet.Add(overflowEntity))
                {
                    results.Add(overflowEntity);
                }
            }

            Vector3i minCell = GetCellCoords(bounds.Min);
            Vector3i maxCell = GetCellCoords(bounds.Max);

            for (int x = minCell.X; x <= maxCell.X; x++)
                for (int y = minCell.Y; y <= maxCell.Y; y++)
                    for (int z = minCell.Z; z <= maxCell.Z; z++)
                    {
                        Vector3i cellKey = new(x, y, z);
                        if (_gridCells.TryGetValue(cellKey, out var list))
                        {
                            for (int i = 0; i < list.Count; i++)
                            {
                                Entity e = list[i];
                                if (_queryVisitedSet.Add(e))
                                {
                                    results.Add(e);
                                }
                            }
                        }
                    }
        }



        /// <summary>
        /// Clears all stored grid state.
        /// </summary>
        public void Clear()
        {
            _gridCells.Clear();
            _entityCellRanges.Clear();
            _globalOverflowEntities.Clear();
            _queryVisitedSet.Clear();
        }
    }
}

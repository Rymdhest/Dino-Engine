using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.World
{
    /// <summary>One placed object. Stored compactly; the game only makes entities for the ones near the camera.</summary>
    public struct WorldObject
    {
        public int Asset;   // Index into WorldObjects.AssetIds.
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
    }

    /// <summary>An object placed before the terrain is final; its height is settled once the ground is.</summary>
    public readonly struct PendingObject
    {
        public readonly string Asset;
        public readonly Vector2 Position;
        public readonly float Yaw;
        public readonly Vector3 Scale;
        public readonly float? Height;  // Fixed height, for things on a levelled pad. Null: wherever the ground ends up.

        public PendingObject(string asset, Vector2 position, float yaw, Vector3 scale, float? height)
        {
            Asset = asset;
            Position = position;
            Yaw = yaw;
            Scale = scale;
            Height = height;
        }
    }

    /// <summary>Levelled ground under a building, or ground laid to an even slope, as under a flight of steps.</summary>
    public readonly struct GroundPad
    {
        public readonly Footprint Footprint;
        public readonly float Height;   // At the footprint's centre.
        public readonly float Margin;   // Metres around the footprint over which it blends back into the land.
        public readonly Vector2 Rise;   // Metres it climbs per metre east and north from its centre: nothing, for level ground.

        public GroundPad(in Footprint footprint, float height, float margin, Vector2 rise = default)
        {
            Footprint = footprint;
            Height = height;
            Margin = margin;
            Rise = rise;
        }

        /// <summary>The height it lays the ground to at a point.</summary>
        public float HeightAt(Vector2 position) => Height + Vector2.Dot(position - Footprint.Centre, Rise);
    }

    /// <summary>A kind of ground painted by people rather than by nature.</summary>
    public enum GroundPaint
    {
        Yard,       // Trampled earth around houses.
        Plaza,      // Packed gravel of market squares and camps.
        Farmland,
        Paving,     // The waygates' own stone laid in slabs (MaterialCatalog.Flagstones): the Haven's forecourt and square.
        Garden,     // A bed someone tends: its grass kept short, green and full of flowers, whatever the land around it grows.
        Sand,       // An arena's floor, raked sand that nothing grows through (ARENAS.md).
        Mud,        // A pen's floor, trodden mud and straw.
        Ash,        // The Onslaught's floor, the ash the charge was buried in.
    }

    /// <summary>A plant put somewhere on purpose by whoever built a site, as a garden's trees are. They're planted before the woods, which grow around them, and only if nothing built stands there.</summary>
    public readonly record struct Planting(WorldAsset Asset, Vector2 Position, float Scale, float Yaw);

    /// <summary>A soft-edged patch of painted ground.</summary>
    public readonly struct PaintPatch
    {
        public readonly GroundPaint Paint;
        public readonly Footprint Shape;
        public readonly float Softness; // Metres its edge fades over.

        public PaintPatch(GroundPaint paint, in Footprint shape, float softness)
        {
            Paint = paint;
            Shape = shape;
            Softness = softness;
        }
    }

    /// <summary>A place forests stay out of.</summary>
    public readonly struct Clearing
    {
        public readonly Vector2 Centre;
        public readonly float Radius;
        public readonly float Falloff;  // Metres beyond the radius over which trees return.

        public Clearing(Vector2 centre, float radius, float falloff)
        {
            Centre = centre;
            Radius = radius;
            Falloff = falloff;
        }
    }

    /// <summary>
    /// Every placed object of a world, sorted into square sectors. Worlds hold far more objects than should ever exist as
    /// entities, so the game streams sectors in and out around the camera (see <see cref="ECS.Systems.TerrainChunkSystem"/>).
    /// </summary>
    public sealed class WorldObjects
    {
        public const float SectorSize = 32f;

        private readonly Dictionary<string, int> _assetIndex = new();
        private readonly List<WorldObject>?[] _sectors;

        public readonly List<string> AssetIds = new();
        public readonly int SectorsPerSide;
        public int Count { get; private set; }

        public WorldObjects(float worldSize)
        {
            SectorsPerSide = (int)MathF.Ceiling(worldSize / SectorSize) + 1;
            _sectors = new List<WorldObject>?[SectorsPerSide * SectorsPerSide];
        }

        public void Add(string assetId, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (!_assetIndex.TryGetValue(assetId, out int asset))
            {
                asset = AssetIds.Count;
                AssetIds.Add(assetId);
                _assetIndex.Add(assetId, asset);
            }

            Vector2i sector = SectorOf(position.Xz);
            int index = sector.Y * SectorsPerSide + sector.X;
            (_sectors[index] ??= new List<WorldObject>()).Add(new WorldObject
            {
                Asset = asset,
                Position = position,
                Rotation = rotation,
                Scale = scale,
            });
            Count++;
        }

        public Vector2i SectorOf(Vector2 position)
        {
            return new Vector2i(
                Math.Clamp((int)MathF.Floor(position.X / SectorSize), 0, SectorsPerSide - 1),
                Math.Clamp((int)MathF.Floor(position.Y / SectorSize), 0, SectorsPerSide - 1));
        }

        /// <summary>The objects of a sector, or null if there are none.</summary>
        public IReadOnlyList<WorldObject>? InSector(Vector2i sector)
        {
            if (sector.X < 0 || sector.Y < 0 || sector.X >= SectorsPerSide || sector.Y >= SectorsPerSide) return null;
            return _sectors[sector.Y * SectorsPerSide + sector.X];
        }

        /// <summary>The nearest of an asset to <paramref name="position"/>, if there's one within <paramref name="reach"/>.</summary>
        public bool TryNearest(string assetId, Vector2 position, float reach, out WorldObject found)
        {
            found = default;
            if (!_assetIndex.TryGetValue(assetId, out int asset)) return false;

            bool any = false;
            float best = reach * reach;
            Vector2i from = SectorOf(position - new Vector2(reach));
            Vector2i to = SectorOf(position + new Vector2(reach));
            for (int y = from.Y; y <= to.Y; y++)
            {
                for (int x = from.X; x <= to.X; x++)
                {
                    if (InSector(new Vector2i(x, y)) is not IReadOnlyList<WorldObject> objects) continue;
                    foreach (WorldObject item in objects)
                    {
                        float distance = (item.Position.Xz - position).LengthSquared;
                        if (item.Asset != asset || distance > best) continue;
                        best = distance;
                        found = item;
                        any = true;
                    }
                }
            }
            return any;
        }

        public IEnumerable<WorldObject> All()
        {
            foreach (List<WorldObject>? sector in _sectors)
            {
                if (sector == null) continue;
                foreach (WorldObject item in sector) yield return item;
            }
        }
    }
}

using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.World
{
    /// <summary>What a game puts at a marked spot of a site.</summary>
    public enum SpawnRole
    {
        Villager,
        Merchant,
        Elder,          // Gives the site's task.
        Guard,          // A hostile standing watch.
        Raider,         // A hostile roaming the site.
        Undead,
        Boss,
        Chest,
        Altar,          // A shrine's place of prayer.
        Waygate,        // The way in and out of the world.
        Beast,          // A hostile that lives wild.
        Cultist,        // A hostile in the service of the land's powers.
        Stash,          // Where what's put away can be got at.
        MapTable,       // Where the next land is chosen, in the Haven.
        Dummy,          // Something to try a build on, in the Haven, that stands and takes it.
        BetaVendor,     // Gives anything away, for testing the game, in the Haven.
    }

    public static class SpawnRoles
    {
        /// <summary>Whether someone standing here has to be fought.</summary>
        public static bool IsHostile(this SpawnRole role) => role is SpawnRole.Guard or SpawnRole.Raider or SpawnRole.Undead or SpawnRole.Boss or SpawnRole.Beast or SpawnRole.Cultist;
    }

    /// <summary>Why a pack is where it is. Places on the map have a <see cref="SiteKind"/>; these are what stands between them.</summary>
    public enum HauntKind
    {
        None,
        Camp,           // Somewhere of their own, with something to show for it.
        Picket,         // Watching the ways into a place.
        Ambush,         // Waiting beside a road.
        Den,            // Out in the wild, where they live.
    }

    public readonly struct SiteSpawn
    {
        public readonly SpawnRole Role;
        public readonly Vector2 Position;

        public SiteSpawn(SpawnRole role, Vector2 position)
        {
            Role = role;
            Position = position;
        }
    }

    /// <summary>A place in the world: a village, a shrine, a stronghold.</summary>
    public sealed class WorldSite
    {
        public int Id;
        public SiteDefinition Definition = null!;
        public string Name = "";
        public Vector2 Position;
        public float Radius;
        public float Height;        // Ground level at the site's centre.
        public float Remoteness;    // 0 by the landing, 1 the far end of the world.
        public int Biome;           // Index into the plan's biomes.
        public bool OnRoad;
        public Vector2 RoadDirection = Vector2.UnitX;   // Unit, from the site's centre towards where its road comes in.
        public bool HasHall;
        public bool HasInn;
        public HauntKind Haunt;     // For a pack: why it's here.
        public SpawnRole Folk;      // For a pack: who it is.

        /// <summary>Where a game should put characters and things. Worldgen only says where and what for.</summary>
        public readonly List<SiteSpawn> Spawns = new();

        public SiteKind Kind => Definition.Kind;
    }

    /// <summary>A body of still water filling a hollow in the land.</summary>
    public readonly struct WorldPond
    {
        public readonly Vector2 Centre;
        public readonly Vector2 HalfExtents;
        public readonly float Level;
        public readonly float Area;
        public readonly float Depth;

        public WorldPond(Vector2 centre, Vector2 halfExtents, float level, float area, float depth)
        {
            Centre = centre;
            HalfExtents = halfExtents;
            Level = level;
            Area = area;
            Depth = depth;
        }
    }

    /// <summary>A road: a smooth centre line with a height along it.</summary>
    public sealed class WorldRoad
    {
        public RoadClass Class;
        /// <summary>Evenly spaced points of the centre line, with y on the road's surface.</summary>
        public readonly List<Vector3> Points = new();

        public float HalfWidth => Class switch
        {
            RoadClass.Highway => 3f,
            RoadClass.Track => 2.1f,
            _ => 1.3f,
        };

        /// <summary>Metres beyond the road's edge over which it blends into the land.</summary>
        public float Shoulder => Class switch
        {
            RoadClass.Highway => 5f,
            RoadClass.Track => 4f,
            _ => 2.5f,
        };

        public float Length
        {
            get
            {
                float length = 0f;
                for (int i = 1; i < Points.Count; i++) length += (Points[i] - Points[i - 1]).Xz.Length;
                return length;
            }
        }
    }
}

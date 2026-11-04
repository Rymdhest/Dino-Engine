using Dino_Defenders.Progression;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Defenders.Gameplay
{
    /// <summary>What has happened at a site. Enemies come and go with distance; this is what stays true while they are away.</summary>
    public sealed class SiteState
    {
        public WorldSite Site = null!;
        public bool Wild;           // A roaming band rather than a place on the map.
        public bool Active;         // Its people are in the world right now.
        public bool Discovered;
        public bool Cleared;
        public bool Kindled;        // A shrine whose light has been relit.
        public bool Rewarded;       // A village's thanks have been given.
        public int HostilesTotal;
        public readonly HashSet<int> Dead = new();
        public readonly HashSet<int> Opened = new();
        public readonly List<Entity> Live = new();
        public readonly List<SiteFeature> Features = new();
        public readonly List<Item> Stock = new();
        public int ThreatIndex = -1;    // For settlements: the hostile site that preys on it.
        public bool Changed;        // Clients haven't heard the latest yet.
    }

    public enum SiteFeatureKind
    {
        Chimney,
        Campfire,
        Lantern,
    }

    /// <summary>Something at a site that comes alive when the hero is near: a thread of smoke, a fire, a lit lantern.</summary>
    public readonly record struct SiteFeature(SiteFeatureKind Kind, Vector3 Position);

    /// <summary>
    /// What is the same for everyone in a world, and worked out by each from the world's seed: its story, its places and the bands on
    /// its roads. So none of this is ever sent, only what has happened to it since.
    /// </summary>
    public abstract class WorldProgress
    {
        public readonly WorldData World;
        public readonly WorldRequest Request;
        public readonly Chronicle Chronicle;
        public readonly List<SiteState> Sites = new();

        public bool Liberated;      // The warlord has fallen.

        protected WorldProgress(WorldData world, WorldRequest request)
        {
            World = world;
            Request = request;
            Chronicle = Chronicle.Write(world);

            foreach (WorldSite site in world.Sites) Sites.Add(CreateState(site, wild: false));
            AddWildBands();
            LinkThreats();
        }

        private static SiteState CreateState(WorldSite site, bool wild)
        {
            var state = new SiteState { Site = site, Wild = wild };
            foreach (SiteSpawn spawn in site.Spawns)
            {
                if (spawn.Role is SpawnRole.Raider or SpawnRole.Guard or SpawnRole.Undead or SpawnRole.Boss) state.HostilesTotal++;
            }
            return state;
        }

        // Bands that roam the roads between places, so that a journey is never only a walk. They aren't on the map; they just
        // happen to you, more often the further from the landing you go.
        private void AddWildBands()
        {
            var random = new WorldRandom(World.Plan.Seed).Fork("wild bands");
            int bands = (int)(World.ReachableArea / 1_000_000f * 3.2f * World.Plan.Archetype.Dangers);
            var placed = new List<Vector2>();

            for (int attempt = 0; attempt < bands * 40 && placed.Count < bands; attempt++)
            {
                WorldRoad road = random.Pick(World.Roads);
                if (road.Points.Count < 4) continue;
                Vector2 spot = road.Points[random.Int(road.Points.Count)].Xz + new Vector2(random.Range(-14f, 14f), random.Range(-14f, 14f));
                float remoteness = World.Remoteness.Sample(spot);
                if (remoteness < 0.22f || !World.Passable.IsSet(spot)) continue;

                bool crowded = false;
                foreach (WorldSite site in World.Sites) crowded |= (site.Position - spot).Length < site.Radius + 70f;
                foreach (Vector2 other in placed) crowded |= (other - spot).Length < 170f;
                if (crowded) continue;

                var band = new WorldSite
                {
                    Id = -1,
                    Definition = SiteCatalog.BanditCamp,
                    Name = "Roving band",
                    Position = spot,
                    Radius = 8f,
                    Remoteness = remoteness,
                };
                int size = random.Int(2, 4);
                SpawnRole role = random.Chance(0.25f) ? SpawnRole.Undead : SpawnRole.Raider;
                for (int i = 0; i < size; i++)
                {
                    Vector2 at = spot + new Vector2(random.Range(-5f, 5f), random.Range(-5f, 5f));
                    if (World.Passable.IsSet(at)) band.Spawns.Add(new SiteSpawn(role, at));
                }
                if (band.Spawns.Count == 0) continue;

                placed.Add(spot);
                Sites.Add(CreateState(band, wild: true));
            }
        }

        // Each settlement fears the nearest place that preys on it, and that fear is what the hero can lift.
        private void LinkThreats()
        {
            for (int i = 0; i < Sites.Count; i++)
            {
                SiteState state = Sites[i];
                if (state.Wild || !state.Site.Definition.IsSettlement) continue;

                float best = float.PositiveInfinity;
                for (int j = 0; j < Sites.Count; j++)
                {
                    SiteState other = Sites[j];
                    if (other.Wild || other.Site.Kind is not (SiteKind.BanditCamp or SiteKind.Keep)) continue;
                    float distance = (other.Site.Position - state.Site.Position).LengthSquared;
                    if (distance < best)
                    {
                        best = distance;
                        state.ThreatIndex = j;
                    }
                }
            }
        }

        /// <summary>Whether a place no longer lives in fear: of those who prey on it, or, with no one nearer, of the warlord.</summary>
        public bool IsSafe(SiteState state)
        {
            return state.ThreatIndex >= 0 ? Sites[state.ThreatIndex].Cleared : Liberated;
        }
    }
}

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
        public bool Lit;            // A waygate someone has come near, which has woken and can be travelled to from any other.
        public int HostilesTotal;
        public readonly HashSet<int> Dead = new();
        public readonly HashSet<int> Opened = new();
        public readonly List<Entity> Live = new();
        public readonly List<SiteFeature> Features = new();
        public readonly MerchantTable Table = new();    // What a merchant has out for this player, as far as they've been told.
        public bool Changed;        // Clients haven't heard the latest yet.
    }

    public enum SiteFeatureKind
    {
        Chimney,
        Campfire,
    }

    /// <summary>Something at a site that comes alive when the hero is near: a thread of smoke, a fire.</summary>
    public readonly record struct SiteFeature(SiteFeatureKind Kind, Vector3 Position);

    /// <summary>
    /// What is the same for everyone in a world, and worked out by each from the world's seed: its story, its places and the bands on
    /// its roads. So none of this is ever sent, only what has happened to it since.
    /// </summary>
    public abstract class WorldProgress
    {
        public readonly WorldData World;
        public readonly WorldRequest Request;
        public readonly LandTerms Terms;
        public readonly Chronicle Chronicle;
        public readonly List<SiteState> Sites = new();

        /// <summary>Whether this is the Haven, where nothing can reach anyone, rather than a land.</summary>
        public readonly bool IsHaven;

        public bool Liberated;      // Everything the land asks has been done, and it's free.
        public bool Lost;           // Everyone here has fallen, or they left it unfree.
        public int Falls;           // Heroes who've fallen here.

        protected WorldProgress(WorldData world, WorldRequest request, LandTerms? terms = null)
        {
            World = world;
            Request = request;
            Terms = terms ?? new LandTerms();
            IsHaven = Lands.IsHaven(request);
            Chronicle = Chronicle.Write(world, Terms, IsHaven);

            foreach (WorldSite site in world.Sites) Sites.Add(CreateState(site, wild: false));
            foreach (WorldSite haunt in world.Haunts) Sites.Add(CreateState(haunt, wild: true));
        }

        private static SiteState CreateState(WorldSite site, bool wild)
        {
            var state = new SiteState { Site = site, Wild = wild };
            foreach (SiteSpawn spawn in site.Spawns)
            {
                if (spawn.Role.IsHostile()) state.HostilesTotal++;
            }
            return state;
        }

        /// <summary>Whether people can breathe here: anywhere in the Haven, and anywhere in a land once it's free. Until then its people keep their heads down.</summary>
        public bool IsSafe(SiteState state) => IsHaven || Liberated;

        /// <summary>Whether a place is one of those a land asks to be freed: anywhere that's held and isn't the keep.</summary>
        public static bool Counts(SiteState state) => !state.Wild && state.HostilesTotal > 0 && state.Site.Kind != SiteKind.Keep;

        /// <summary>Whether a place has a waygate in it, which a hero can travel to once it's lit.</summary>
        public static bool HasWaygate(SiteState state) => !state.Wild && state.Site.Spawns.Any(spawn => spawn.Role == SpawnRole.Waygate);

        /// <summary>Whether a place's waygate can be travelled to: it has one, and someone has come near enough to wake it.</summary>
        public static bool CanTravelTo(SiteState state) => state.Lit && HasWaygate(state);
    }
}

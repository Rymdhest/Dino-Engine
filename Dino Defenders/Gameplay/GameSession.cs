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
        public int ThreatIndex = -1;    // For settlements: the hostile site that preys on it.
    }

    /// <summary>A line of text that drifts up from something in the world.</summary>
    public struct FloatingText
    {
        public string Text;
        public Vector3 Position;
        public float Age;
        public float Life;
        public Vector3 Colour;
        public float Size;
    }

    /// <summary>A message across the screen.</summary>
    public struct Banner
    {
        public string Title;
        public string Subtitle;
        public float Age;
        public float Life;
    }

    /// <summary>
    /// The game in progress: the hero, the world they are in, and what has happened to it. Systems and panels all work on this,
    /// and it goes when the world does, except for the hero.
    /// </summary>
    public sealed class GameSession
    {
        public static GameSession Current = null!;

        private const float DiscoverRadius = 60f;

        public readonly HeroState Hero;
        public readonly WorldData World;
        public readonly Chronicle Chronicle;
        public readonly List<SiteState> Sites = new();
        public readonly List<FloatingText> FloatingTexts = new();
        public readonly List<Banner> Banners = new();
        public readonly List<string> Log = new();

        public Entity Player;
        public HeroStats Stats;
        public float Health;
        public Vector2 Respawn;
        public Vector2 PlayerPosition;
        public float DashCooldown;
        public float WhirlCooldown;
        public float SwingCooldown;
        public float GuideCooldown;
        public string? InteractPrompt;
        public bool WorldOffered;

        /// <summary>Opened by interacting: the game shows the panel that goes with it.</summary>
        public Action<InteractKind, int>? InteractionOpened;
        public Action? WorldCleared;

        public GameSession(HeroState hero, WorldData world)
        {
            Hero = hero;
            World = world;
            Chronicle = Chronicle.Write(world);
            Stats = hero.Compute();
            Health = Stats.MaxHealth;
            Respawn = world.Sites[0].Position;
            PlayerPosition = Respawn;

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

        /// <summary>Whether a settlement no longer lives in fear.</summary>
        public bool IsSafe(SiteState state)
        {
            return state.ThreatIndex < 0 || Sites[state.ThreatIndex].Cleared;
        }

        public void RefreshStats()
        {
            float healthShare = Health / Math.Max(Stats.MaxHealth, 1f);
            Stats = Hero.Compute();
            Health = Math.Min(Stats.MaxHealth * healthShare, Stats.MaxHealth);
        }

        public void Say(Vector3 position, string text, Vector3 colour, float size = 15f, float life = 2.4f)
        {
            FloatingTexts.Add(new FloatingText { Text = text, Position = position, Colour = colour, Size = size, Life = life });
        }

        public void Announce(string title, string subtitle = "", float life = 4f)
        {
            Banners.Add(new Banner { Title = title, Subtitle = subtitle, Life = life });
        }

        public void Note(string text)
        {
            Log.Add(text);
            if (Log.Count > 6) Log.RemoveAt(0);
        }

        public void GiveExperience(int amount)
        {
            int levels = Hero.GainExperience(amount);
            if (levels <= 0) return;
            RefreshStats();
            Health = Stats.MaxHealth;
            Announce($"Level {Hero.Level}", "Three points to spend. Press C.");
        }

        public void UpdateDiscovery()
        {
            foreach (SiteState state in Sites)
            {
                if (state.Discovered || state.Wild) continue;
                float reach = state.Site.Radius + DiscoverRadius;
                if ((state.Site.Position - PlayerPosition).LengthSquared > reach * reach) continue;

                state.Discovered = true;
                Announce(state.Site.Name, Chronicle.Describe(state.Site, World), 4.5f);
                GiveExperience(6 * World.Plan.Level);
            }
        }

        /// <summary>A hostile at a site has fallen; if it was the last, the site is free.</summary>
        public void OnHostileKilled(SiteState state)
        {
            if (state.Cleared || state.Dead.Count < state.HostilesTotal) return;
            state.Cleared = true;
            if (state.Wild) return;

            Note($"{state.Site.Name} is cleared.");
            Advance(state.Site.Kind == SiteKind.Keep ? ObjectiveKind.SlayWarlord : ObjectiveKind.ClearSites, state.Site.Kind);
            MarkFreedVillages(state);
        }

        private void MarkFreedVillages(SiteState cleared)
        {
            foreach (SiteState state in Sites)
            {
                if (state.ThreatIndex < 0 || Sites[state.ThreatIndex] != cleared) continue;
                if (state.Site.Definition.Kind is SiteKind.Village or SiteKind.Hamlet)
                {
                    Note($"{state.Site.Name} breathes easier.");
                }
            }
        }

        public void Advance(ObjectiveKind kind, SiteKind site)
        {
            foreach (Objective objective in Chronicle.Objectives)
            {
                if (objective.Kind != kind || objective.Site != site || objective.Done) continue;
                objective.Progress++;
                if (!objective.Done) continue;

                GiveExperience(objective.RewardExperience);
                Hero.Gold += objective.RewardGold;
                Announce(objective.IsMain ? "The land is free" : "Chronicle fulfilled", objective.Text, 5f);
                if (objective.IsMain) WorldCleared?.Invoke();
            }
        }

        /// <summary>The nearest goal still open, for the guiding wind and the marker on the screen.</summary>
        public Vector2? GuidanceTarget()
        {
            return Chronicle.NextTarget(World, site =>
            {
                SiteState state = Sites[site.Id];
                return site.Kind == SiteKind.Shrine ? state.Kindled : state.Cleared;
            }, PlayerPosition);
        }
    }
}

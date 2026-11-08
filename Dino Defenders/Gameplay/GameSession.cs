using Dino_Defenders.Client;
using Dino_Defenders.Progression;
using Dino_Defenders.Stats;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Defenders.Gameplay
{
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

    /// <summary>A line in the running account of what just happened.</summary>
    public struct LogLine
    {
        public string Text;
        public float Age;
    }

    /// <summary>A great find, named across the top of the screen.</summary>
    public struct LootCallout
    {
        public string Text;
        public Rarity Rarity;
        public float Age;
    }

    /// <summary>
    /// The game as this player sees it: their hero, the world they are in, and what has happened to it. The interface reads all of
    /// this and none of it is the truth: that is on the server, and everything here is what it last said, kept up to date by
    /// <see cref="GameClient"/>. To change anything the player asks <see cref="Client"/>, and the answer comes back.
    /// </summary>
    public sealed class GameSession : WorldProgress
    {
        public static GameSession Current = null!;

        public readonly GameClient Client;
        public readonly List<FloatingText> FloatingTexts = new();
        public readonly List<Banner> Banners = new();
        public readonly List<LogLine> Log = new();
        public readonly List<LootCallout> Callouts = new();
        public readonly Dictionary<int, Item> Ground = new();   // What each find on the ground is, as far as this player has been told.
        public readonly Dictionary<int, long> Taken = new();    // What's been asked for and is drawn as gone, until when, by the client's clock.
        public readonly HashSet<int> Heard = new();             // Finds this player has already been told about, with a sound and all.
        public readonly LootFetch Fetch = new();
        public readonly DummyMeter Meter = new();

        public HeroState Hero => Client.Hero;
        public ActorSheet Sheet => Client.Sheet;

        public Entity Player;
        public HeroIntent Intent;
        public (float speed, float direction)? ScriptedWind;    // What a scripted run has the wind do, in place of what the land gives.
        public WeatherSample? ScriptedWeather;                  // Weather a scripted run has everywhere, in place of what the world's would be.
        public double WeatherClockOffset;                       // Seconds a scripted run starts into the world's weather.
        public Func<HeroIntent>? Autopilot;
        public bool Paused;
        public Vector2? Guidance;
        public float Health;
        public float Barrier;
        public float Debt;
        public Vector2 PlayerPosition;
        public HeroSimState Sim;
        public float GuideCooldown;
        public string? InteractPrompt;
        public InteractKind? Offered;
        public Entity OfferedThing;     // What the key would use: the same one the server will pick.

        public GameSession(GameClient client, WorldData world, WorldRequest request, LandTerms terms) : base(world, request, terms)
        {
            Client = client;
            Health = Sheet.MaxHealth;
            PlayerPosition = world.Sites[0].Position;
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
            Log.Add(new LogLine { Text = text });
            if (Log.Count > 6) Log.RemoveAt(0);
        }

        /// <summary>
        /// Where to go next, for the guiding wind and the mark on the screen: the nearest place still to free while the land asks for more, then its tyrant, and once the land is free the nearest
        /// waygate home. In the Haven, the map table, until the way is open, and then the waygate.
        /// </summary>
        public void UpdateGuidance()
        {
            Guidance = null;
            Vector2 at = PlayerPosition;
            if (IsHaven)
            {
                SpawnRole wanted = Client.Portal != null ? SpawnRole.Waygate : SpawnRole.MapTable;
                foreach (SiteSpawn spawn in World.Sites[0].Spawns)
                {
                    if (spawn.Role == wanted) Guidance = spawn.Position;
                }
                return;
            }

            if (Liberated || Client.Out)
            {
                Guidance = Nearest(at, state => HasWaygate(state) && state.Discovered) ?? Nearest(at, HasWaygate);
                return;
            }

            Objective? places = Chronicle.Objectives.FirstOrDefault(objective => objective.Kind == ObjectiveKind.FreePlaces);
            if (places is { Done: false })
            {
                Guidance = Nearest(at, state => Counts(state) && !state.Cleared);
                if (Guidance != null) return;
            }

            Guidance = Nearest(at, state => state.Site.Kind == SiteKind.Keep && !state.Cleared) ?? Nearest(at, state => state.Site.Kind == SiteKind.Keep);
        }

        private Vector2? Nearest(Vector2 from, Func<SiteState, bool> wanted)
        {
            Vector2? best = null;
            float distance = float.PositiveInfinity;
            foreach (SiteState state in Sites)
            {
                if (state.Wild || !wanted(state)) continue;
                float away = (state.Site.Position - from).LengthSquared;
                if (away >= distance) continue;
                distance = away;
                best = state.Site.Position;
            }
            return best;
        }
    }
}

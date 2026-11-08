using Dino_Defenders.Client;
using Dino_Defenders.Progression;
using Dino_Defenders.Stats;
using Dino_Engine.Networking;
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
        public int Slot;            // Which place around its spot it takes, so that hits landing together don't cover each other.
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
        public uint? CallingSince;      // The tick this player began calling the old roads on, by their own clock, as far as they can tell: the server has the last word.
        public Vector2 CallingFrom;
        public float CallingHealth;
        public string? InteractPrompt;
        public InteractKind? Offered;
        public Entity OfferedThing;     // What the key would use: the same one the server will pick.

        public GameSession(GameClient client, WorldData world, WorldRequest request, LandTerms terms) : base(world, request, terms)
        {
            Client = client;
            Health = Sheet.MaxHealth;
            PlayerPosition = world.Sites[0].Position;
            _twin = Terms.Unique() == OrdersCatalog.TwinPens ? Chronicle.TwinSite(world) : null;
        }

        private readonly WorldSite? _twin;    // Where a second tyrant stands, on a land whose orders gave it two.

        public void Say(Vector3 position, string text, Vector3 colour, float size = 15f, float life = 2.4f)
        {
            FloatingTexts.Add(new FloatingText { Text = text, Position = position, Colour = colour, Size = size, Life = life, Slot = FreeSlot(position) });
        }

        // Texts that have just come up near here hold their places: a new one takes the first that's free. One that's old enough has
        // drifted clear, so its place goes back.
        private int FreeSlot(Vector3 position)
        {
            const float Near = 1.5f, Young = 0.45f;
            ulong taken = 0;
            foreach (FloatingText other in FloatingTexts)
            {
                if (other.Age >= Young || other.Slot >= 64) continue;
                float dx = other.Position.X - position.X, dz = other.Position.Z - position.Z;
                if (dx * dx + dz * dz < Near * Near) taken |= 1UL << other.Slot;
            }
            int slot = 0;
            while (slot < 63 && (taken & (1UL << slot)) != 0) slot++;
            return slot;
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

        /// <summary>Whether the old roads can be called from where the hero is now.</summary>
        public bool CanCall => !IsHaven && !Client.Out && Sim.Health > 0f && Sim.BusyFor <= 0f && CallingSince == null;

        /// <summary>Begins calling the old roads, which is standing still and unhurt for a while, and then the map opens to travel by.</summary>
        public void BeginCalling()
        {
            if (!CanCall) return;
            CallingSince = Client.Commands.Tick;
            CallingFrom = Sim.Position;
            CallingHealth = Sim.Health;
            Client.CallRoads();
        }

        /// <summary>How far through a call this player is, from nothing to done, or nothing if they aren't calling.</summary>
        public float? Calling => CallingSince is uint since ? Math.Clamp(CalledFor(since) / Rulebook.CallRoads, 0f, 1f) : null;

        private float CalledFor(uint since) => (float)((Client.Commands.Tick - since) * NetTime.TickSecondsPrecise);

        /// <summary>Breaks a call by the server's rule, and lets it go a little after it's done, by when the map has opened. Asked each time the hero has been predicted.</summary>
        public void UpdateCalling()
        {
            if (CallingSince is not uint since) return;
            float gone = CalledFor(since);
            if (gone >= Rulebook.CallRoads + CallingLinger || (gone < Rulebook.CallRoads && (Client.Out || Roads.Broken(Sim, CallingFrom, CallingHealth, Sheet.MaxHealth))))
            {
                CallingSince = null;
                return;
            }
            CallingHealth = MathF.Max(CallingHealth, Sim.Health);
        }

        private const float CallingLinger = 0.6f;

        /// <summary>
        /// Whether an enemy is one the land asks to be beaten, and is marked as such: anyone holding a place it names while it asks for those, and its tyrants, whenever they stand. Nothing is marked
        /// once the land is free, or for a hero who's out of it.
        /// </summary>
        public bool Sought(in EnemyComponent enemy)
        {
            int site = enemy.Holds - 1;
            if (enemy.Friendly || site < 0 || site >= Sites.Count || IsHaven || Liberated || Client.Out) return false;
            SiteState state = Sites[site];
            if (enemy.Rarity == Rarity.Unique && (state.Site.Kind == SiteKind.Keep || state.Site == _twin)) return true;
            return PlacesAsked && !state.Cleared && Chronicle.Asks(state.Site);
        }

        /// <summary>Whether the land still asks for more of its places to be freed before its tyrant.</summary>
        public bool PlacesAsked => Chronicle.Objectives.Any(objective => objective.Kind == ObjectiveKind.FreePlaces && !objective.Done);

        /// <summary>
        /// Whether a place is one the land asks of the hero now, found or not: each of the places it names that's still held, and once those are free, where its tyrants are. The map shows these and
        /// nothing else that hasn't been found, and the mark on the screen points to the nearest.
        /// </summary>
        public bool IsGoal(SiteState state)
        {
            if (state.Wild || state.Cleared || IsHaven || Liberated || Client.Out) return false;
            if (PlacesAsked) return Chronicle.Asks(state.Site);
            return state.Site.Kind == SiteKind.Keep || state.Site == _twin;
        }

        /// <summary>
        /// Where to go next, for the guiding wind and the mark on the screen: the nearest of the land's goals, and once the land is free the nearest waygate home. In the Haven, the map table, until
        /// the way is open, and then the waygate.
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

            Guidance = Nearest(at, IsGoal) ?? Nearest(at, state => state.Site.Kind == SiteKind.Keep);
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

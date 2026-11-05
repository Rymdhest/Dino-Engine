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

        public HeroState Hero => Client.Hero;
        public ActorSheet Sheet => Client.Sheet;

        public Entity Player;
        public HeroIntent Intent;
        public (float speed, float direction)? ScriptedWind;    // What a scripted run has the wind do, in place of what the land gives.
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

        public GameSession(GameClient client, WorldData world, WorldRequest request) : base(world, request)
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

        /// <summary>The nearest goal still open, for the guiding wind and the marker on the screen. Once they are all done, the way out.</summary>
        public void UpdateGuidance()
        {
            Guidance = Chronicle.NextTarget(World, site =>
            {
                SiteState state = Sites[site.Id];
                return site.Kind == SiteKind.Shrine ? state.Kindled : state.Cleared;
            }, PlayerPosition) ?? World.Sites[0].Position;
        }
    }
}

using Dino_Defenders.Server;
using Dino_Engine.World;
using Dino_Engine.World.Content;

namespace Dino_Defenders.Progression
{
    /// <summary>
    /// The game's half of the tutorial, the land every new hero starts in (<see cref="TutorialLand"/> is its ground, and TUTORIAL.md says the whole of it). Everything about how it plays that a
    /// person might want to tune is here: its level, what waits at the end, and what's said. It's a land like any other in every rule that isn't named here: its two places are freed as any are,
    /// and its beast is the land's main objective as a tyrant would be.
    /// <list type="bullet">
    /// <item>Nothing in it is Magic or Rare but its beast, which is Rare and nothing more (<c>EncounterSystem.Field</c>).</item>
    /// <item>Freeing it climbs nothing: a hero comes out of it with every level of the climb still before them (<c>ServerSession.Judge</c>).</item>
    /// <item>Falling there is as falling in the Haven: the hero rises at the landing, and nothing is lost but the walk (<c>ServerRoom.StandUp</c>).</item>
    /// <item>The waygates can't be called there, as there are none to call (<c>GameSession.CanCall</c>, <c>HeroAction.CallRoads</c>).</item>
    /// <item>Its one waygate is dark until its beast is down, and stepping through it goes straight home (<c>EncounterSystem.Freed</c>, <c>InteractionSystem.Use</c>).</item>
    /// </list>
    /// </summary>
    public static class Tutorial
    {
        /// <summary>Its level, which is how strong everything in it is and what it drops.</summary>
        public const int Level = 1;

        /// <summary>What waits at the end of the way, which is Rare: a ripper, one of the great raptors that got out of the Reich's pens, as it pounces and sweeps and shows a new player what a mark on the ground means.</summary>
        public static EnemyKind Beast => Bestiary.Ripper;

        /// <summary>The world it's grown from, which is always the same one.</summary>
        public static WorldRequest Request => new WorldRequest(TutorialLand.Seed, TutorialLand.Id, Level);

        public static bool Is(in WorldRequest request) => TutorialLand.Is(request);

        /// <summary>
        /// Whether a hero who isn't in a land starts in the tutorial rather than the Haven: one who's never been anywhere. Nobody comes out of the tutorial without experience to show for it, so
        /// nobody is sent back; a hero who leaves the game halfway through it is in a land, and comes back to it as anyone does.
        /// </summary>
        public static bool Starts(HeroState hero) => hero.Level == 1 && hero.Experience == 0 && hero.WorldsCleared == 0 && hero.BestLand <= 0;

        /// <summary>What the corner says the land is, as a land's prologue.</summary>
        public const string Prologue = "Where every Defender comes ashore. A raptor that got out of the Reich's pens has made this vale its own, and the Reich holds the way to it. Behind it stands an old waygate, dark, which goes home once it's lit.";

        /// <summary>What the land asks first: its places, freed.</summary>
        public const string FreeText = "Free the places marked on your map from those who hold them";

        /// <summary>And then its beast, at <paramref name="lair"/>.</summary>
        public static string SlayText(string lair) => $"Bring down the {Beast.Name} at {lair}";

        /// <summary>Said across the screen when its beast falls and its waygate lights.</summary>
        public const string GateLit = "The waygate is lit";
        public const string GateLitSubtitle = "Open the chest, then step through the waygate to the Haven.";

        /// <summary>What its waygate's mark offers.</summary>
        public const string GatePrompt = "Step through to the Haven";

        /// <summary>The first banner a new hero sees, as they come ashore.</summary>
        public const string Welcome = "Free the vale";
        public const string WelcomeSubtitle = "Follow the way. Free the places marked on your map, then bring down the beast at the end of it.";
    }
}

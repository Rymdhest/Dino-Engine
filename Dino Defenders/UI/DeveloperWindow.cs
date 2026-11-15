using Dino_Defenders.AssetLexicon;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Net;
using Dino_Defenders.Progression;
using Dino_Engine.GUI;
using Dino_Engine.GUI.Debug;
using Dino_Engine.GUI.Widgets;

namespace Dino_Defenders.UI
{
    /// <summary>
    /// The game's own window in the F3 overlay, for developers and testers: cheats, and the way into the asset test level. It speaks the engine's debug language rather than the
    /// game's, as it's no part of the game. Everything in it is asked of the server, which only gives a cheat while the game is being tested (<see cref="BetaVendor.Standing"/>), so
    /// it's always the rules that do it, as they would in play.
    /// </summary>
    public static class DeveloperWindow
    {
        public static GuiWindow Build()
        {
            var window = new GuiWindow("Developer", 300f) { X = 324f, Y = 56f };
            window.Closable = true;

            window.Content.Add(DebugUi.Heading("Hero"));
            var grid = window.Content.Add(DebugUi.KeyValueGrid());
            DebugUi.AddRow(grid, "Level", () => GameSession.Current is GameSession session ? session.Hero.Level.ToString() : "-");
            DebugUi.AddRow(grid, "Experience", () => GameSession.Current is GameSession session
                ? session.Hero.Level >= HeroState.MaxLevel ? "the most there is" : $"{session.Hero.Experience} / {session.Hero.ExperienceToNext}"
                : "-");

            Button levelUp = window.Content.Add(Action("Level up", () => GameSession.Current?.Client.Cheat(DeveloperCheat.LevelUp)));
            levelUp.Tooltip = "Gives the rest of this level's experience, so the hero goes up one, as killing would";
            levelUp.Updated += _ => levelUp.Enabled = BetaVendor.Standing && GameSession.Current is GameSession session && session.Hero.Level < HeroState.MaxLevel;

            // Lexicon (developer tool, LEXICON.md): the way into the asset lexicon, a room with one of everything the game has on show, labelled, and home from it.
            if (LexiconServer.Open)
            {
                window.Content.Add(DebugUi.Heading("Asset test level"));
                Button lexicon = window.Content.Add(Action("Go to the asset test level", GoToLexicon));
                lexicon.Tooltip = "One of everything the game has, labelled. Takes everyone in the world there, from the Haven";
                Label where = window.Content.Add(DebugUi.Muted("", wrap: true));
                lexicon.Updated += _ =>
                {
                    GameSession? session = GameSession.Current;
                    bool inside = session?.IsLexicon == true;
                    bool wayOpen = session?.Client.Portal != null;
                    lexicon.Text = inside ? "Back to the Haven" : "Go to the asset test level";
                    lexicon.Enabled = session != null && (inside || (session.IsHaven && !wayOpen));
                    where.Text = session == null ? "No hero in a world."
                        : inside ? "Everyone goes home with you."
                        : !session.IsHaven ? "Only from the Haven."
                        : wayOpen ? "A way to a land is open: close it at the map table first, or it's lost."
                        : "Everyone in the world goes with you.";
                };
            }

            if (!BetaVendor.Standing) window.Content.Add(DebugUi.Muted("The game isn't being tested, so the server gives no cheats.", wrap: true));
            return window;
        }

        private static Button Action(string text, Action pressed) => new Button(text, pressed) { Width = UIValue.Fill, Height = 26f, Margin = new Thickness(0f, 4f, 0f, 0f) };

        // Lexicon (developer tool, LEXICON.md).
        private static void GoToLexicon()
        {
            GameSession? session = GameSession.Current;
            if (session == null) return;
            GameUi.Current.CloseAll();
            session.Client.AskLexicon(session.IsLexicon ? LexiconAsk.Leave : LexiconAsk.Visit);
        }
    }
}

using Dino_Defenders.Client;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Net;
using Dino_Defenders.Progression;
using Dino_Defenders.Books;
using Dino_Defenders.Stats;
using Dino_Defenders.Talents;
using Dino_Defenders.Tooltips;
using Dino_Engine.GUI;
using Dino_Engine.GUI.Rendering;
using Dino_Engine.GUI.Widgets;
using Dino_Engine.Sound;
using Dino_Engine.Sound.Content;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Dino_Defenders.UI
{
    /// <summary>
    /// The land as a map, turned the way the land is seen: the hero knows its shape, but a place only appears on it once they have found it. Opened at a waygate, it's also the way to travel: every gate
    /// found in the land can be clicked, and so can the way home to the Haven, which asks first if the land isn't free.
    /// </summary>
    internal sealed class MapPanel : GuiElement
    {
        public const int Resolution = 1024;
        private const float PickRadius = 16f;
        private const float GateIcon = 7f;

        private int _texture;
        private readonly Action _close;
        private readonly Button _home;
        private readonly LeavePrompt _prompt;

        /// <summary>Whether it was opened at a waygate, from which the land's roads can be taken.</summary>
        public bool Travelling { get; private set; }

        public MapPanel(Action close)
        {
            _close = close;
            Width = UIValue.Fill;
            Height = UIValue.Fill;
            BackgroundColor = GuiColors.Rgba(8, 6, 5, 215);
            HitTest = HitTestMode.Block;
            Visible = false;
            Clicked += OnPick;

            _home = Add(Ui.Action("Go home to the Haven", GoHome));
            _home.Anchor = Anchor.Bottom;
            _home.Width = 280f;
            _home.Height = 34f;
            _home.Y = -46f;
            _home.Visible = false;

            _prompt = Add(new LeavePrompt(Leave, () => _prompt!.Visible = false));
        }

        /// <summary>Opens it to look at, from anywhere.</summary>
        public void Look()
        {
            Travelling = false;
            _home.Visible = false;
            _prompt.Visible = false;
        }

        /// <summary>Opens it at a waygate, to travel from.</summary>
        public void Travel()
        {
            Travelling = true;
            _home.Visible = true;
            _prompt.Visible = false;
            _home.Label.Bind(() => GameSession.Current is { Liberated: true } ? "Go home to the Haven" : "Leave the land, and go home");
        }

        private void GoHome()
        {
            if (GameSession.Current is not { } session) return;
            if (session.Liberated)
            {
                Leave();
                return;
            }
            Audio.Play(Interface.Open);
            _prompt.Ask();
        }

        private void Leave()
        {
            _prompt.Visible = false;
            GameSession.Current?.Client.ReturnHome();
            _close();
        }

        public void SetImage(byte[] pixels)
        {
            if (_texture != 0) GL.DeleteTexture(_texture);
            _texture = GL.GenTexture();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _texture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, Resolution, Resolution, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        private GuiRect MapRect()
        {
            float side = MathF.Min(Bounds.Width, Bounds.Height) * 0.8f;
            return new GuiRect((Bounds.Width - side) * 0.5f, (Bounds.Height - side) * 0.5f, side, side);
        }

        // +x runs to the left of the screen in play, so the map runs that way too, and what's ahead on the land is up on the map.
        private static Vector2 ToMap(GuiRect map, Vector2 position, float worldSize)
        {
            return new Vector2(map.Right - position.X / worldSize * map.Width, map.Bottom - position.Y / worldSize * map.Height);
        }

        private void OnPick(GuiMouseEventArgs e)
        {
            GameSession session = GameSession.Current;
            if (!Travelling || _prompt.Visible || session.IsHaven) return;
            GuiRect map = MapRect();
            for (int i = 0; i < session.Sites.Count; i++)
            {
                SiteState state = session.Sites[i];
                if (!CanTravelTo(state)) continue;
                Vector2 at = ToMap(map, state.Site.Position, session.World.Size);
                if ((at - e.Position).Length > PickRadius) continue;

                session.Client.TravelToSite(i);
                _close();
                return;
            }
        }

        private static bool CanTravelTo(SiteState state) => state.Discovered && WorldProgress.HasWaygate(state);

        protected override void OnDraw(GuiDrawList draw)
        {
            base.OnDraw(draw);
            GameSession session = GameSession.Current;
            if (session == null || _texture == 0 || Context == null) return;

            GuiRect map = MapRect();
            float size = session.World.Size;
            draw.AddRect(new GuiRect(map.X - 3f, map.Y - 3f, map.Width + 6f, map.Height + 6f), Ui.Ink, 2f, 1f, GuiColors.Rgba(120, 96, 58));
            draw.AddImage(_texture, map, new Vector2(1f, 0f), new Vector2(0f, 1f), GuiColors.White);

            Ui.Text(Context, draw, session.World.Plan.Name, 26f, new GuiRect(0f, map.Y - 40f, Bounds.Width, 32f), Ui.Parchment, Alignment.Center);

            Vector2 mouse = Context.MousePosition;
            foreach (SiteState state in session.Sites)
            {
                if (state.Wild || !state.Discovered) continue;
                Vector2 at = ToMap(map, state.Site.Position, size);
                bool gate = WorldProgress.HasWaygate(state);
                bool hovered = Travelling && gate && (at - mouse).Length <= PickRadius;
                if (gate) DrawGate(draw, at, hovered ? GuiColors.Rgba(190, 236, 255) : GuiColors.Rgba(104, 206, 255));
                else
                {
                    Vector3 c = state.Site.Definition.MapColour;
                    Colour colour = state.Cleared ? Ui.Faded : GuiColors.Rgba(c.X, c.Y, c.Z);
                    draw.AddCircle(at, 4.5f, colour, 1.5f, GuiColors.Rgba(0, 0, 0, 200));
                }
                Ui.Text(Context, draw, state.Site.Name, 13.5f, new GuiRect(at.X - 110f, at.Y + 7f, 220f, 18f), hovered ? Ui.Bright : Ui.Parchment, Alignment.Center);
            }

            if (session.Guidance is Vector2 goal)
            {
                Vector2 at = ToMap(map, goal, size);
                draw.AddCircle(at, 9f, GuiColors.Transparent, 2f, Ui.Gold);
            }

            Vector2 hero = ToMap(map, session.PlayerPosition, size);
            draw.AddCircle(hero, 5f, Ui.Parchment, 2f, GuiColors.Rgba(150, 30, 24));

            string help = Travelling
                ? "Click a waygate you've found to go there."
                : session.IsHaven ? $"{Bindings.Label(GameAction.Map)} to close." : $"{Bindings.Label(GameAction.Map)} to close.  Waygates are travelled between, and home from, at any waygate.";
            Ui.Text(Context, draw, help, 14f, new GuiRect(0f, map.Bottom + 6f, Bounds.Width, 20f), Ui.Faded, Alignment.Center);
        }

        // A little arch, as the mark on a gate is, so that a gate reads as one wherever it is.
        private static void DrawGate(GuiDrawList draw, Vector2 at, Colour colour)
        {
            draw.AddCircle(at, GateIcon + 2.5f, GuiColors.Rgba(0, 0, 0, 170));
            var line = new List<Vector2>();
            for (int i = 0; i <= 10; i++)
            {
                float angle = MathF.PI * i / 10f;
                line.Add(at + new Vector2(MathF.Cos(angle) * GateIcon * 0.8f, GateIcon * 0.35f - MathF.Sin(angle) * GateIcon));
            }
            draw.AddPolyline(line, colour, 2f);
            draw.AddLine(at + new Vector2(-GateIcon * 0.8f, GateIcon * 0.35f), at + new Vector2(-GateIcon * 0.8f, GateIcon * 0.8f), colour, 2f);
            draw.AddLine(at + new Vector2(GateIcon * 0.8f, GateIcon * 0.35f), at + new Vector2(GateIcon * 0.8f, GateIcon * 0.8f), colour, 2f);
        }
    }

    /// <summary>The story of the land so far, and, from an elder, a word of thanks when it has been earned.</summary>
    internal sealed class ChroniclePanel : GuiElement
    {
        private readonly StackPanel _body;

        public ChroniclePanel()
        {
            Anchor = Anchor.Center;
            Width = 640f;
            Height = UIValue.Auto;
            Padding = new Thickness(24f);
            Ui.Dress(this);
            Visible = false;
            _body = Add(new StackPanel(Orientation.Vertical, 8f) { Width = UIValue.Fill, Height = UIValue.Auto });
        }

        public void Open(SiteState? elder)
        {
            GameSession session = GameSession.Current;
            Chronicle chronicle = session.Chronicle;

            _body.ClearChildren();
            _body.Add(Ui.Heading(session.World.Plan.Name, 26f));
            if (session.IsHaven)
            {
                _body.Add(Ui.Paragraph(chronicle.Prologue, 16f));
                _body.Add(Ui.Line($"Your next land is level {session.Hero.LandLevel}. You've freed {session.Hero.WorldsCleared} in all, as deep as level {session.Hero.BestLand}.", 14f, Ui.Gold));
                _body.Add(new Spacer(height: 4f));
                _body.Add(Ui.Line("", 13f, Ui.Faded).Bind(() => $"{Bindings.Label(GameAction.Chronicle)} to close"));
                return;
            }
            _body.Add(Ui.Line($"{session.World.Plan.Archetype.Name}, under {chronicle.Faction}. Level {session.World.Plan.Level}.", 14f, Ui.Faded));
            foreach ((OmenDefinition omen, float value) in session.Terms.Chosen()) _body.Add(Ui.Paragraph($"{omen.Name}. {omen.Describe(value)}", 13f, Ui.Faded));
            if (session.Terms.Unique() is UniqueOrders unique) _body.Add(Ui.Paragraph($"{unique.Name}. {unique.Gives}. {unique.Costs}.", 13f, Ui.RarityColour(Rarity.Unique)));
            _body.Add(Ui.Paragraph(chronicle.Prologue, 16f));
            _body.Add(new Spacer(height: 6f));

            foreach (Objective objective in chronicle.Objectives)
            {
                Colour colour = objective.Done ? Ui.Faded : Ui.Parchment;
                _body.Add(Ui.Paragraph(objective.Summary, 15f, colour));
                _body.Add(Ui.Line($"{objective.RewardExperience} experience, {objective.RewardGold} gold", 13f, Ui.Gold.MultiplyAlpha(objective.Done ? 0.4f : 0.9f)));
            }

            if (elder != null) AddElder(session, elder);
            _body.Add(new Spacer(height: 4f));
            _body.Add(Ui.Line("", 13f, Ui.Faded).Bind(() => $"{Bindings.Label(GameAction.Chronicle)} to close"));
        }

        private void AddElder(GameSession session, SiteState elder)
        {
            _body.Add(new Spacer(height: 8f));
            _body.Add(Ui.Line($"The elder of {elder.Site.Name}", 16f, Ui.Gold));

            if (!session.IsSafe(elder))
            {
                _body.Add(Ui.Paragraph($"\"While {session.Chronicle.Beast} holds this land we keep our heads down and our doors barred. Free us, and you'll have our thanks and more than thanks.\"", 15f));
                return;
            }

            if (elder.Rewarded)
            {
                _body.Add(Ui.Paragraph("\"The children play on the road again. That was you.\"", 15f));
                return;
            }

            int gift = 45 * session.World.Plan.Level;
            session.Client.ThankElder(session.Sites.IndexOf(elder));
            _body.Add(Ui.Paragraph($"\"We didn't think anyone would come. Take this, it's what we have.\"", 15f));
            _body.Add(Ui.Line($"{gift} gold", 14f, Ui.Gold));
        }
    }

    /// <summary>Shown while a new land takes shape.</summary>
    internal sealed class LoadingScreen : GuiElement
    {
        public string Title = "";
        public string Detail = "";
        public string Stage = "";
        public float Progress;

        public LoadingScreen()
        {
            Width = UIValue.Fill;
            Height = UIValue.Fill;
            BackgroundColor = GuiColors.Rgba(9, 7, 6);
            HitTest = HitTestMode.Block;
            Visible = false;
        }

        protected override void OnDraw(GuiDrawList draw)
        {
            base.OnDraw(draw);
            if (Context == null) return;

            float y = Bounds.Height * 0.42f;
            Ui.Text(Context, draw, Title, 42f, new GuiRect(0f, y, Bounds.Width, 52f), Ui.Parchment, Alignment.Center);
            y += 56f;
            Ui.Text(Context, draw, Detail, 17f, new GuiRect(0f, y, Bounds.Width, 24f), Ui.Faded, Alignment.Center);
            y += 44f;

            float width = MathF.Min(Bounds.Width * 0.36f, 460f);
            GuiRect bar = new GuiRect((Bounds.Width - width) * 0.5f, y, width, 3f);
            draw.AddRect(bar, GuiColors.Rgba(60, 50, 40));
            draw.AddRect(new GuiRect(bar.X, bar.Y, bar.Width * Math.Clamp(Progress, 0f, 1f), bar.Height), Ui.Gold);
            Ui.Text(Context, draw, Stage, 14f, new GuiRect(0f, y + 12f, Bounds.Width, 20f), Ui.Faded, Alignment.Center);
        }
    }

    /// <summary>All of the game's interface, and which of it is showing.</summary>
    internal sealed class GameUi
    {
        public static GameUi Current = null!;

        public readonly Wardrobe Wardrobe;
        public readonly UseMarks Marks = new();
        public readonly Hud Hud = new();
        public readonly LootLabels Labels = new();
        public readonly MapPanel Map;
        public readonly HeroPanel Hero = new();
        public readonly TalentPanel Talents = new();
        public readonly BookPanel Book;
        public readonly GlossaryPage Glossary = new();
        public readonly SettingsPage Settings = new();
        public readonly ChroniclePanel Chronicle = new();
        public readonly MapTablePanel MapTable = new();
        public readonly PauseMenu Pause;
        public readonly MultiplayerPage Multiplayer;
        public readonly LoadingScreen Loading = new();
        public readonly TipLayer Tips = new();
        public readonly GroundCatcher Ground = new();
        public readonly HandLayer Hand = new();
        public readonly ActionBar Bar;
        public readonly MenuScreen Menu;

        private readonly GuiElement[] _panels;

        public GameUi(GuiContext gui, ArpgGame game, Action quit, Action heroSelect)
        {
            Current = this;
            Wardrobe = game.Wardrobe;
            Book = new BookPanel(Hero);
            Map = new MapPanel(CloseAll);
            Pause = new PauseMenu(CloseAll, () => Into(Settings), () => Into(Glossary), () => Into(Multiplayer!), heroSelect, quit);
            Multiplayer = new MultiplayerPage(game);
            Bar = new ActionBar(Hero);
            Menu = new MenuScreen(game);
            _panels = new GuiElement[] { Map, Hero, Talents, Book, Glossary, Chronicle, MapTable, Pause, Multiplayer, Settings };

            gui.Root.Add(Marks);
            gui.Root.Add(Hud);
            gui.Root.Add(Labels);
            gui.Root.Add(Ground);
            foreach (GuiElement panel in _panels) gui.Root.Add(panel);
            gui.Root.Add(Bar);
            gui.Root.Add(Menu);
            Lift(gui);
            gui.Root.Add(Tips);
            gui.Root.Add(Hand);
            gui.Root.Add(Loading);
        }

        // Pages cover everything, the bar and the main menu among them.
        private void Lift(GuiContext gui)
        {
            foreach (GuiElement page in new GuiElement[] { Pause, Glossary, Multiplayer, Settings }) gui.Root.Add(page);
        }

        /// <summary>Whether it's the main menu that's showing, and nobody is being played.</summary>
        public bool AtMenu { get; private set; }

        /// <summary>Whether one of the screens that cover everything is open.</summary>
        public bool OnPage => Pause.Visible || Glossary.Visible || Multiplayer.Visible || Settings.Visible;

        private GuiElement? _cameFrom;

        /// <summary>Goes to a page from wherever this is, which is what Back goes to.</summary>
        public void Into(GuiElement page)
        {
            _cameFrom = Pause.Visible ? Pause : AtMenu ? Menu : null;
            CloseAll();
            Menu.Visible = false;
            Audio.Play(Interface.Open);
            page.Visible = true;
            if (page == Settings) Settings.Open();
            if (page == Multiplayer) Multiplayer.Open();
        }

        /// <summary>Back from a page, to whatever it was opened from.</summary>
        public void Back()
        {
            GuiElement? to = _cameFrom;
            _cameFrom = null;
            CloseAll();
            if (to == null) return;
            to.Visible = true;
        }

        /// <summary>Whether there's a game going on, as everything that's part of one goes when the main menu comes.</summary>
        public void Playing(bool playing)
        {
            AtMenu = !playing;
            CloseAll();
            Menu.Visible = !playing;
            Clear();
        }

        /// <summary>What's on the world goes while a page is over it, as a page is all there should be to read. It's asked every frame, as anything can open one.</summary>
        public void Clear()
        {
            bool showing = !AtMenu && !OnPage;
            Hud.Visible = showing;
            Bar.Visible = showing;
            Marks.Visible = showing;
            Labels.Visible = showing;
            Ground.Visible = showing;
        }

        public bool AnyOpen => _panels.Any(p => p.Visible);

        public void CloseAll()
        {
            if (AnyOpen) Audio.Play(Interface.Close);
            foreach (GuiElement panel in _panels) panel.Visible = false;
            Tips.Clear();
            Hero.Forget();
        }

        public void Toggle(GuiElement panel)
        {
            bool open = panel.Visible;
            CloseAll();
            if (open) return;

            Audio.Play(Interface.Open);
            panel.Visible = true;
            if (panel == Hero || panel == Book) Hero.Open(null);
            if (panel == Book) Book.OpenAny();
            _cameFrom = null;
            if (panel == Chronicle) Chronicle.Open(null);
            if (panel == Map) Map.Look();
        }

        /// <summary>Opens a book, in place of whatever else is open, or shuts it if it's the one that's open.</summary>
        public void Read(ItemPlace place)
        {
            bool reading = Book.IsOpen(Book.At(place));
            CloseAll();
            if (reading) return;

            Audio.Play(Interface.Open);
            Hero.Open(null);
            Book.Visible = true;
            Book.Show(place);
        }

        /// <summary>Opens what goes with something the hero used in the world.</summary>
        public void Open(InteractKind kind, SiteState site)
        {
            CloseAll();
            Audio.Play(Interface.Open);
            switch (kind)
            {
                case InteractKind.Merchant:
                    Hero.Visible = true;
                    Hero.Open(site);
                    break;
                case InteractKind.Elder:
                    Chronicle.Visible = true;
                    Chronicle.Open(site);
                    break;
                case InteractKind.Waygate:
                    Map.Visible = true;
                    Map.Travel();
                    break;
                case InteractKind.MapTable:
                    MapTable.Visible = true;
                    MapTable.Open();
                    break;
                case InteractKind.Stash:
                    Hero.Visible = true;
                    Hero.OpenStash();
                    break;
            }
        }
    }
}

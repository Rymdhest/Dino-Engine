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
    /// The land as a map, turned the way the land is seen: the hero knows its shape, but a place only appears on it once they
    /// have found it. Shrines that have been kindled can be travelled to.
    /// </summary>
    internal sealed class MapPanel : GuiElement
    {
        public const int Resolution = 1024;
        private const float PickRadius = 16f;

        private int _texture;
        private readonly Action _close;

        public MapPanel(Action close)
        {
            _close = close;
            Width = UIValue.Fill;
            Height = UIValue.Fill;
            BackgroundColor = GuiColors.Rgba(8, 6, 5, 215);
            HitTest = HitTestMode.Block;
            Visible = false;
            Clicked += OnPick;
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
            float side = MathF.Min(Bounds.Width, Bounds.Height) * 0.84f;
            return new GuiRect((Bounds.Width - side) * 0.5f, (Bounds.Height - side) * 0.5f + 12f, side, side);
        }

        // +x runs to the left of the screen in play, so the map runs that way too, and what's ahead on the land is up on the map.
        private static Vector2 ToMap(GuiRect map, Vector2 position, float worldSize)
        {
            return new Vector2(map.Right - position.X / worldSize * map.Width, map.Bottom - position.Y / worldSize * map.Height);
        }

        private void OnPick(GuiMouseEventArgs e)
        {
            GameSession session = GameSession.Current;
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

        private static bool CanTravelTo(SiteState state)
        {
            return state.Kindled || state.Site.Kind == SiteKind.Landing;
        }

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

            foreach (SiteState state in session.Sites)
            {
                if (state.Wild || !state.Discovered) continue;
                Vector2 at = ToMap(map, state.Site.Position, size);
                Vector3 c = state.Site.Definition.MapColour;
                Colour colour = state.Cleared ? Ui.Faded : GuiColors.Rgba(c.X, c.Y, c.Z);
                float radius = CanTravelTo(state) ? 6f : 4.5f;
                draw.AddCircle(at, radius, colour, 1.5f, GuiColors.Rgba(0, 0, 0, 200));
                Ui.Text(Context, draw, state.Site.Name, 13.5f, new GuiRect(at.X - 110f, at.Y + 6f, 220f, 18f), Ui.Parchment, Alignment.Center);
            }

            if (session.Guidance is Vector2 goal)
            {
                Vector2 at = ToMap(map, goal, size);
                draw.AddCircle(at, 9f, GuiColors.Transparent, 2f, Ui.Gold);
            }

            Vector2 hero = ToMap(map, session.PlayerPosition, size);
            draw.AddCircle(hero, 5f, Ui.Parchment, 2f, GuiColors.Rgba(150, 30, 24));

            string help = $"{Bindings.Label(GameAction.Map)} to close.  Click a kindled shrine or the landing to travel there.";
            Ui.Text(Context, draw, help, 14f, new GuiRect(0f, map.Bottom + 6f, Bounds.Width, 20f), Ui.Faded, Alignment.Center);
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
            _body.Add(Ui.Line($"{session.World.Plan.Archetype.Name}, under {chronicle.Faction}. Level {session.World.Plan.Level}.", 14f, Ui.Faded));
            if (session.Offering.Any)
            {
                _body.Add(Ui.Line(WaygatePanel.Standing(session), 14f, session.Broken ? Ui.Blood : Ui.Gold));
                foreach (OmenDefinition omen in session.Offering.Chosen()) _body.Add(Ui.Paragraph($"{omen.Name}. {omen.Describe()}", 13f, Ui.Faded));
            }
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
                string threat = elder.ThreatIndex >= 0 ? session.Sites[elder.ThreatIndex].Site.Name : "the keep";
                _body.Add(Ui.Paragraph($"\"They come down from {threat} when the mood takes them. Break them there, and you'll have our thanks and more than thanks.\"", 15f));
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

    /// <summary>
    /// Where the waygate can lead: other lands, each its own place, and harder once this one is free. Whatever is set here is what's offered on the way through to any of them, and
    /// it's kept from one visit to the next, as a player who's found how much they can take will want that again.
    /// </summary>
    internal sealed class WaygatePanel : GuiElement
    {
        private const int Choices = 3;
        private const int OmenColumns = 7;
        private const string OmenHelp = "Rest your eyes on an omen to read it.";

        private static readonly Colour Chosen = GuiColors.Rgba(112, 74, 28);

        private readonly StackPanel _cards;
        private readonly Label _note;
        private readonly Label _cost;
        private readonly Action<WorldRequest, Offering> _travel;
        private readonly Offering _offering = new();
        private readonly List<(OmenDefinition omen, Button button)> _omens = new();
        private int _level = 1;
        private OmenDefinition? _lookingAt;
        private GuiRect _lookingFrom;

        private static HeroState Hero => GameSession.Current.Hero;

        /// <summary>How an offering stands, in a line.</summary>
        public static string Standing(GameSession session)
        {
            Offering offering = session.Offering;
            if (session.Broken) return $"{offering.Title()}: the offering is broken";
            if (!offering.AtStake || session.Liberated) return offering.Title();
            return session.FallsLeft > 0 ? $"{offering.Title()}: {session.FallsLeft} {(session.FallsLeft == 1 ? "fall" : "falls")} left" : $"{offering.Title()}: no more falls";
        }

        public WaygatePanel(Action<WorldRequest, Offering> travel)
        {
            _travel = travel;
            Anchor = Anchor.Center;
            Width = 960f;
            Height = UIValue.Auto;
            Padding = new Thickness(24f);
            Ui.Dress(this);
            Visible = false;

            var body = Add(new StackPanel(Orientation.Vertical, 12f) { Width = UIValue.Fill, Height = UIValue.Auto });
            body.Add(Ui.Heading("The Waygate", 26f));
            _note = body.Add(Ui.Paragraph("", 15f, Ui.Faded));
            _cards = body.Add(new StackPanel(Orientation.Horizontal, 14f) { Width = UIValue.Fill, Height = UIValue.Auto });

            body.Add(Ui.Heading("The Offering", 20f));
            body.Add(Ui.Paragraph("Gold burned at the gate makes the land beyond worse, and richer. Fall too often there and what was paid is gone. See the land free to climb higher.", 14f, Ui.Faded));

            Dial(body, "Peril", () => _offering.Peril, value => _offering.Peril = value, () => Offering.Reach(Hero), DescribePeril);
            Dial(body, "Teeming", () => _offering.Teeming, value => _offering.Teeming = value, () => Offering.MostCrowd,
                () => _offering.Teeming == 0 ? "Packs as they come" : $"Packs {_offering.MorePeople * 100f:0}% larger, and no stronger for it");
            Dial(body, "Overrun", () => _offering.Overrun, value => _offering.Overrun = value, () => Offering.MostCrowd,
                () => _offering.Overrun == 0 ? "As many packs as ever" : $"{_offering.MorePacks * 100f:0}% more packs on the land, and no stronger for it");

            body.Add(Ui.Line("", 14f, Ui.Gold).Bind(() => $"Omens: {_offering.Omens.Count} of {Offering.OmenSlots(_offering.Peril)}. More peril carries more."));
            var grid = body.Add(new GridPanel(OmenColumns, 6f, 6f));
            foreach (OmenDefinition omen in Omens.All) _omens.Add((omen, grid.Add(OmenButton(omen))));
            body.Add(Ui.Paragraph(OmenHelp, 14f, Ui.Faded));

            body.Add(Ui.Line("", 14f).Bind(DescribeRewards));
            _cost = body.Add(Ui.Line("", 15f, Ui.Gold).Bind(() => $"Costs {_offering.Price(_level)} gold of your {Hero.Gold}"));

            // Decided here rather than by each of them, as this is what knows how many are chosen.
            Updated += _ =>
            {
                if (GameSession.Current == null) return;
                bool room = _offering.Omens.Count < Offering.OmenSlots(_offering.Peril);
                foreach ((OmenDefinition omen, Button button) in _omens)
                {
                    bool chosen = _offering.Has(omen);
                    button.NormalColor = chosen ? Chosen : Ui.InkLight;
                    button.Enabled = chosen || room;
                }
                _cost.Color = _offering.Price(_level) > Hero.Gold ? Ui.Blood : Ui.Gold;
            };
        }

        private void Dial(GuiElement body, string name, Func<int> value, Action<int> set, Func<int> most, Func<string> says)
        {
            var row = body.Add(new StackPanel(Orientation.Horizontal, 8f) { Width = UIValue.Fill, Height = 28f });
            row.Add(Ui.Line(name, 15f, Ui.Gold)).Width = 80f;
            row.Add(Step("-", () => Turn(set, value() - 1, most())));
            row.Add(Ui.Line("", 16f).Bind(() => value().ToString())).Width = 30f;
            row.Add(Step("+", () => Turn(set, value() + 1, most())));
            row.Add(Ui.Line("", 14f, Ui.Parchment).Bind(says)).Width = UIValue.Fill;
        }

        private static Button Step(string text, Action pressed)
        {
            Button button = Ui.Action(text, pressed);
            button.Width = 34f;
            button.Height = 26f;
            return button;
        }

        private void Turn(Action<int> set, int value, int most)
        {
            set(Math.Clamp(value, 0, most));
            _offering.Sanitize();
        }

        private Button OmenButton(OmenDefinition omen)
        {
            Button button = Ui.Action(omen.Name, () => Toggle(omen));
            button.Height = 28f;
            button.MouseEnter += () =>
            {
                _lookingAt = omen;
                _lookingFrom = button.Bounds;
            };
            button.MouseLeave += () => _lookingAt = null;
            return button;
        }

        protected override void OnDrawOverlay(GuiDrawList draw)
        {
            base.OnDrawOverlay(draw);
            if (_lookingAt is not OmenDefinition omen) return;
            GameUi.Current.Tips.Show(() => Cards.Omen(omen, GameSession.Current.Sheet), _lookingFrom);
        }

        private void Toggle(OmenDefinition omen)
        {
            if (!_offering.Omens.Remove(omen.Id) && _offering.Omens.Count < Offering.OmenSlots(_offering.Peril)) _offering.Omens.Add(omen.Id);
        }

        private string DescribePeril()
        {
            if (_offering.Peril == 0) return $"Enemies as they are. You can reach Peril {Offering.Reach(Hero)}";
            return $"Enemies: {(_offering.EnemyHealth - 1f) * 100f:0}% more health, {(_offering.EnemyDamage - 1f) * 100f:0}% more damage";
        }

        private string DescribeRewards()
        {
            if (!_offering.AtStake) return _offering.Any ? "The same from each, and more of them to get it from." : "Nothing offered, and nothing more than the land gives.";
            LootBoon boon = _offering.Boon();
            string levels = boon.ExtraLevels > 1 ? $", items {boon.ExtraLevels} levels up" : boon.ExtraLevels == 1 ? ", items a level up" : "";
            string plain = boon.Least > LootGrade.Plain ? ", nothing plain" : "";
            return $"Drops: {(boon.Quantity - 1f) * 100f:0}% more, {(boon.Rarity - 1f) * 100f:0}% rarer{levels}{plain}. {Offering.AllowedFalls} falls allowed.";
        }

        private void StepThrough(WorldRequest request)
        {
            if (_offering.Price(request.Level) > Hero.Gold)
            {
                GameSession.Current.Note("You can't pay for that offering.");
                return;
            }
            _travel(request, _offering.Copy());
        }

        public void Open()
        {
            GameSession session = GameSession.Current;
            bool free = session.Chronicle.WorldCleared;
            int level = session.World.Plan.Level + (free ? 1 : 0);
            _level = level;
            _offering.Peril = Math.Min(_offering.Peril, Offering.Reach(session.Hero));
            _offering.Sanitize();
            _note.Text = free
                ? "With this land free, the old roads open further. Beyond them the lands are harder, and richer."
                : $"While {session.Chronicle.Warlord} holds this land, the gate reaches only as far as lands like it. You may go, and it will not wait for you.";

            _cards.ClearChildren();
            var random = new WorldRandom(session.World.Plan.Seed).Fork("roads onward").Fork(level);
            for (int i = 0; i < Choices; i++)
            {
                var request = new WorldRequest(random.Int(int.MaxValue), null, level);
                _cards.Add(Card(request));
            }
        }

        private GuiElement Card(WorldRequest request)
        {
            WorldPlan plan = WorldPlan.Create(request);
            var card = new StackPanel(Orientation.Vertical, 6f)
            {
                Width = UIValue.Relative(1f / Choices, -10f),
                Height = UIValue.Auto,
                Padding = new Thickness(14f),
                BackgroundColor = Ui.InkLight,
                BorderColor = GuiColors.Rgba(84, 68, 46),
                BorderWidth = 1f,
                CornerRadius = 2f,
            };

            card.Add(Ui.Paragraph(plan.Name, 20f, Ui.Parchment));
            card.Add(Ui.Line(plan.Archetype.Name, 14f, Ui.Gold));
            card.Add(Ui.Line($"Level {plan.Level}", 14f, Ui.Faded));
            card.Add(Ui.Paragraph(string.Join(", ", plan.Biomes.Select(b => b.Name)), 14f));
            card.Add(Ui.Line(plan.Mood.Name, 14f, Ui.Faded));
            Button step = card.Add(Ui.Action("Step through", () => StepThrough(request)));
            step.Label.Bind(() => _offering.Any ? $"Step through for {_offering.Price(request.Level)} gold" : "Step through");
            return card;
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
        public readonly WaygatePanel Waygate;
        public readonly PauseMenu Pause;
        public readonly MultiplayerPage Multiplayer;
        public readonly LoadingScreen Loading = new();
        public readonly TipLayer Tips = new();
        public readonly GroundCatcher Ground = new();
        public readonly HandLayer Hand = new();
        public readonly ActionBar Bar;
        public readonly MenuScreen Menu;

        private readonly GuiElement[] _panels;

        public GameUi(GuiContext gui, ArpgGame game, Action<WorldRequest, Offering> travel, Action quit, Action heroSelect)
        {
            Current = this;
            Wardrobe = game.Wardrobe;
            Book = new BookPanel(Hero);
            Map = new MapPanel(CloseAll);
            Waygate = new WaygatePanel(travel);
            Pause = new PauseMenu(CloseAll, () => Into(Settings), () => Into(Glossary), () => Into(Multiplayer!), heroSelect, quit);
            Multiplayer = new MultiplayerPage(game);
            Bar = new ActionBar(Hero);
            Menu = new MenuScreen(game);
            _panels = new GuiElement[] { Map, Hero, Talents, Book, Glossary, Chronicle, Waygate, Pause, Multiplayer, Settings };

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
                    Waygate.Visible = true;
                    Waygate.Open();
                    break;
            }
        }
    }
}

using Dino_Defenders.Client;
using Dino_Defenders.Net;
using Dino_Defenders.Tooltips;
using Dino_Engine.Core;
using Dino_Engine.GUI;
using Dino_Engine.GUI.Rendering;
using Dino_Engine.GUI.Widgets;
using Dino_Engine.Rendering;
using Dino_Engine.Sound;
using Dino_Engine.Sound.Content;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Dino_Defenders.UI
{
    /// <summary>The game held still, and everywhere there is to go from here.</summary>
    internal sealed class PauseMenu : MenuPage
    {
        public PauseMenu(Action resume, Action settings, Action glossary, Action multiplayer, Action heroSelect, Action quit)
            : base("Paused", back: false)
        {
            void Word(string text, Action pressed) => Rail.Add(new MenuWord(text, Kit.NavSize, Ui.Parchment, pressed, Kit.NavHeight));

            Word("Continue", resume);
            Word("Settings", settings);
            Word("Glossary", glossary);
            Word("Multiplayer", multiplayer);
            Word("Hero Select", heroSelect);
            Word("Quit Game", quit);
        }
    }

    /// <summary>A key on the Input page: its name, and what's pressed for it, which a click asks to change.</summary>
    internal sealed class KeyRow : GuiElement
    {
        private const float CapHeight = 30f;
        private const float LeastCap = 74f;

        private readonly GameAction _action;
        private readonly string _name;
        private readonly SettingsPage _page;

        public KeyRow(GameAction action, string name, SettingsPage page)
        {
            _action = action;
            _name = name;
            _page = page;
            Width = UIValue.Fill;
            Height = 38f;
            HitTest = HitTestMode.Block;
            Cursor = MouseCursor.Hand;
            Clicked += e =>
            {
                if (e.Button != MouseButton.Left) return;
                Audio.Play(Interface.Click);
                _page.Listen(_action);
            };
        }

        protected override void OnDraw(GuiDrawList draw)
        {
            base.OnDraw(draw);
            if (Context == null) return;

            bool listening = _page.Listening == _action;
            string label = listening ? "Press a key" : Bindings.Label(_action);
            float width = MathF.Max(LeastCap, GuiText.MeasureWidth(Context, label, Ui.Face, Kit.RowSize - 1f) + 24f);
            GuiRect cap = new GuiRect(Bounds.Right - width, Bounds.Center.Y - CapHeight * 0.5f, width, CapHeight);

            Ui.Text(Context, draw, _name, Kit.RowSize, new GuiRect(Bounds.X, Bounds.Y, cap.X - Bounds.X - 12f, Bounds.Height), IsHovered ? Ui.Gold : Ui.Parchment);
            draw.AddRect(cap, GuiColors.Rgba(14, 11, 9, 170), 4f, 1f, listening || IsHovered ? Ui.Gold : Kit.Line);
            Ui.Text(Context, draw, label, Kit.RowSize - 1f, new GuiRect(cap.X, cap.Y + 1.5f, cap.Width, cap.Height), listening ? Ui.Gold : Ui.Parchment, Alignment.Center);
        }
    }

    /// <summary>
    /// Everything a player can set, in four parts. Whatever's changed happens at once and is kept at once, so there's nothing to confirm. A new part is a word on the rail and a panel of rows, and a new setting a row.
    /// While a key is being listened for everything else is out of reach, as a click may be what's being given.
    /// </summary>
    internal sealed class SettingsPage : MenuPage
    {
        private readonly GuiElement[] _parts;
        private readonly MenuWord[] _words;
        private readonly GuiElement _catcher;
        private readonly Label _note;
        private readonly Keys[] _everyKey = Enum.GetValues<Keys>().Where(Bindings.CanBind).Distinct().ToArray();
        private readonly List<string> _sizeNames = new();
        private readonly List<Vector2i> _sizes = new();
        private bool _justHeard;

        /// <summary>What's waiting for a key, if anything.</summary>
        public GameAction? Listening { get; private set; }

        /// <summary>Whether a key that's just been pressed was for this, and means nothing else.</summary>
        public bool Busy => Listening != null || _justHeard;

        public SettingsPage()
            : base("Settings")
        {
            string[] names = { "Graphics", "Sound", "Interface", "Input" };
            _parts = new[] { Graphics(), SoundPart(), InterfacePart(), Input() };
            _words = new MenuWord[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                int part = i;
                _words[i] = Rail.Add(new MenuWord(names[i], Kit.NavSize, Ui.Parchment, () => Show(part), Kit.NavHeight));
                Body.Add(_parts[i]);
            }

            // Over everything, and it's what a click lands on while one is wanted.
            _catcher = Add(new GuiElement { Width = UIValue.Fill, Height = UIValue.Fill, HitTest = HitTestMode.Block, Visible = false });
            _note = Add(new Label("") { Font = Ui.Face, FontSize = Kit.SmallSize + 1f, Color = Ui.Gold, Anchor = Anchor.Bottom, Margin = new Thickness(0f, 0f, 0f, 26f) });
            Show(0);
        }

        private void Show(int part)
        {
            for (int i = 0; i < _parts.Length; i++)
            {
                _parts[i].Visible = i == part;
                _words[i].Chosen = i == part;
            }
        }

        /// <summary>Opens at the beginning, with nothing left over.</summary>
        public void Open()
        {
            Listening = null;
            _note.Text = "";
            Show(0);
            ReadSizes();
        }

        private static GuiElement Part() => new StackPanel(Orientation.Vertical, 6f) { Width = UIValue.Fill, Height = UIValue.Fill };

        // ---- Graphics.

        private GuiElement Graphics()
        {
            VideoSettings video = VideoSettings.Load();
            string[] modes = { "Windowed", "Borderless", "Fullscreen" };
            string[] limits = VideoSettings.FrameLimits.Select(limit => limit == 0 ? "Unlimited" : $"{limit} FPS").ToArray();
            string[] qualities = { "Low", "Medium", "High" };

            GuiElement part = Part();
            part.Add(new MenuRow("Display", new MenuStepper(() => modes, () => (int)video.Mode, at => Change(() =>
            {
                // What a window's size was has to be known before it's gone, to come back to.
                if (video.Mode == DisplayMode.Windowed) (video.Width, video.Height) = Engine.Resolution;
                video.Mode = (DisplayMode)at;
            }, video.ApplyDisplay))));

            var size = new MenuStepper(() => _sizeNames, SizeAt, ChooseSize) { Enabled = () => video.Mode != DisplayMode.Borderless };
            part.Add(new MenuRow("Resolution", size));

            part.Add(new MenuRow("Frame rate limit", new MenuStepper(() => limits, () => Math.Max(Array.IndexOf(VideoSettings.FrameLimits, video.FrameLimit), 0), at => Change(() => video.FrameLimit = VideoSettings.FrameLimits[at], video.ApplyFrames))));
            part.Add(new MenuRow("Vertical sync", new MenuToggle(() => video.VSync, on => Change(() => video.VSync = on, video.ApplyFrames))));
            part.Add(new Spacer(height: 10f));

            var quality = new MenuStops(qualities, () => (int)video.Quality, at => Change(() => video.Quality = (GraphicsQuality)at, video.ApplyQuality));
            part.Add(new MenuRow("Graphics quality", quality) { Height = 56f });
            return part;
        }

        private static void Change(Action change, Action apply)
        {
            change();
            apply();
            VideoSettings.Load().Store();
        }

        private void ReadSizes()
        {
            _sizes.Clear();
            _sizes.AddRange(Engine.WindowHandler.Resolutions());

            // A window can be dragged to anything, and whatever it is has to be something that's shown.
            Vector2i current = Engine.Resolution;
            if (!_sizes.Contains(current))
            {
                _sizes.Add(current);
                _sizes.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
            }
            _sizeNames.Clear();
            _sizeNames.AddRange(_sizes.Select(size => $"{size.X} x {size.Y}"));
        }

        private int SizeAt()
        {
            if (!_sizes.Contains(Engine.Resolution)) ReadSizes();
            return _sizes.IndexOf(Engine.Resolution);
        }

        private void ChooseSize(int at)
        {
            VideoSettings video = VideoSettings.Load();
            Change(() =>
            {
                video.Width = _sizes[at].X;
                video.Height = _sizes[at].Y;
            }, video.ApplyDisplay);
        }

        // ---- Sound.

        private static GuiElement SoundPart()
        {
            SoundSettings sound = SoundSettings.Load();
            GuiElement part = Part();
            void Row(string name, Func<float> value, Action<float> set) => part.Add(new MenuRow(name, new MenuSlider(value, v => sound.Change(() => set(v)))));

            Row("Master", () => sound.Master, v => sound.Master = v);
            Row("Music", () => sound.Music, v => sound.Music = v);
            Row("Effects", () => sound.Effects, v => sound.Effects = v);
            Row("Ambience", () => sound.World, v => sound.World = v);
            Row("Interface", () => sound.Interface, v => sound.Interface = v);
            part.Add(new Spacer(height: 10f));
            part.Add(new MenuRow("Headphones", new MenuToggle(() => sound.Headphones, on => sound.Change(() => sound.Headphones = on))));
            return part;
        }

        // ---- Interface.

        private static GuiElement InterfacePart()
        {
            InterfaceSettings shown = InterfaceSettings.Load();
            GuiElement part = Part();
            part.Add(new MenuRow("Health, Mana and Barrier over your hero", new MenuToggle(() => shown.PoolsOverHero, on => shown.Change(() => shown.PoolsOverHero = on))));
            part.Add(new MenuRow("Attacks marked on the ground: Normal enemies", new MenuToggle(() => shown.NormalMarks, on => shown.Change(() => shown.NormalMarks = on))));
            part.Add(new MenuRow("Attacks marked on the ground: Magic enemies", new MenuToggle(() => shown.MagicMarks, on => shown.Change(() => shown.MagicMarks = on))));
            part.Add(new MenuRow("Attacks marked on the ground: Rare enemies", new MenuToggle(() => shown.RareMarks, on => shown.Change(() => shown.RareMarks = on))));
            part.Add(new MenuRow("Attacks marked on the ground: Unique enemies", new MenuToggle(() => shown.UniqueMarks, on => shown.Change(() => shown.UniqueMarks = on))));
            return part;
        }

        // ---- Input.

        private GuiElement Input()
        {
            GuiElement part = Part();
            var scroll = part.Add(Kit.Scroll());
            var rows = scroll.Content.Add(new StackPanel(Orientation.Vertical, 0f) { Width = UIValue.Fill, Height = UIValue.Auto, Padding = new Thickness(0f, 0f, 16f, 0f) });
            foreach ((string group, (GameAction action, string name)[] actions) in Bindings.Page)
            {
                rows.Add(Kit.Group(group));
                foreach ((GameAction action, string name) in actions) rows.Add(new KeyRow(action, name, this));
                rows.Add(new Spacer(height: 12f));
            }
            part.Add(new MenuWord("Reset all keys", 16f, Kit.Quiet, ResetKeys, 34f));
            return part;
        }

        private void ResetKeys()
        {
            Bindings.Reset();
            _note.Text = "";
        }

        /// <summary>Waits for a key for something.</summary>
        public void Listen(GameAction action)
        {
            Listening = action;
            _note.Text = "Press any key or mouse button, or Esc to cancel";
        }

        protected override void OnUpdate(float deltaTime)
        {
            _justHeard = false;
            _catcher.Visible = Listening != null;
            if (Listening is not GameAction action) return;

            var window = Engine.WindowHandler;
            if (window.IsKeyPressed(Keys.Escape))
            {
                Hear(null, action);
                return;
            }

            foreach (Keys key in _everyKey)
            {
                if (!window.IsKeyPressed(key)) continue;
                Hear(new Bound(key), action);
                return;
            }
            for (MouseButton button = MouseButton.Button1; button <= MouseButton.Button8; button++)
            {
                if (!window.IsMouseButtonPressed(button)) continue;
                Hear(Bound.Mouse(button), action);
                return;
            }
        }

        private void Hear(Bound? bound, GameAction action)
        {
            Listening = null;
            _justHeard = true;
            _catcher.Visible = false;
            _note.Text = "";
            if (bound is not Bound given) return;

            Audio.Play(Interface.Click);
            if (Bindings.Set(action, given) is not GameAction swapped) return;
            _note.Text = $"{NameOf(swapped)} is now on {Bindings.Label(swapped)}";
        }

        private static string NameOf(GameAction action)
        {
            foreach ((string _, (GameAction action, string name)[] rows) in Bindings.Page)
            {
                foreach ((GameAction each, string name) in rows)
                {
                    if (each == action) return name;
                }
            }
            return action.ToString();
        }
    }

    /// <summary>Every word that means something, to look up.</summary>
    internal sealed class GlossaryPage : MenuPage
    {
        private readonly List<(Term term, GuiElement entry)> _entries = new();
        private readonly TextBox _search;

        public GlossaryPage()
            : base("Glossary")
        {
            _search = Body.Add(Kit.Field("", "Search"));
            _search.TextChanged += _ => Narrow();
            Body.Add(new Spacer(height: 14f));

            var scroll = Body.Add(Kit.Scroll());
            var list = scroll.Content.Add(new StackPanel(Orientation.Vertical, 12f) { Width = UIValue.Fill, Height = UIValue.Auto, Padding = new Thickness(0f, 0f, 16f, 0f) });
            foreach (Term term in Lexicon.All.OrderBy(term => term.Name, StringComparer.OrdinalIgnoreCase))
            {
                var entry = list.Add(new StackPanel(Orientation.Vertical, 2f) { Width = UIValue.Fill, Height = UIValue.Auto });
                entry.Add(Ui.Line(term.Name, Kit.RowSize, Ui.Gold));
                entry.Add(Ui.Paragraph(term.Says, Kit.SmallSize + 1f, Ui.Parchment));
                _entries.Add((term, entry));
            }
        }

        private void Narrow()
        {
            string wanted = _search.Text.Trim();
            foreach ((Term term, GuiElement entry) in _entries)
            {
                entry.Visible = wanted.Length == 0 || term.Forms.Any(form => form.Contains(wanted, StringComparison.OrdinalIgnoreCase)) || term.Says.Contains(wanted, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>Playing together. A game is opened to friends from here, with a code to send them, and the code someone's been sent goes in here.</summary>
    internal sealed class MultiplayerPage : MenuPage
    {
        private readonly ArpgGame _game;
        private readonly TextBox _name;
        private readonly TextBox _code;
        private readonly Label _problem;

        public MultiplayerPage(ArpgGame game)
            : base("Multiplayer")
        {
            _game = game;

            var hosting = Body.Add(new StackPanel(Orientation.Vertical, 6f) { Width = UIValue.Fill, Height = UIValue.Auto });
            hosting.Add(Kit.Group("Host"));
            var open = hosting.Add(new MenuWord("Open this world to friends", Kit.RowSize + 1f, Ui.Parchment, () => _game.OpenToFriends(), 34f));
            var invite = hosting.Add(Ui.Line("", Kit.RowSize, Ui.Gold).Bind(() => _game.InviteText ?? ""));
            var copy = hosting.Add(new MenuWord("Copy the invite", Kit.RowSize, Ui.Parchment, () => Context!.Clipboard = _game.InviteText ?? "", 32f));
            var status = hosting.Add(Ui.Paragraph("", Kit.SmallSize, Kit.Quiet).Bind(() => _game.InviteStatus));

            Body.Add(new Spacer(height: 22f));
            var joining = Body.Add(new StackPanel(Orientation.Vertical, 8f) { Width = UIValue.Fill, Height = UIValue.Auto });
            joining.Add(Kit.Group("Join"));
            _name = joining.Add(Kit.Field(_game.HeroName, "Your name, for them to see", 24));
            _name.TextChanged += text => _game.HeroName = text;
            _code = joining.Add(Kit.Field("", "Paste the invite here"));
            _code.Submitted += _ => Join();
            joining.Add(new MenuWord("Join", Kit.RowSize + 1f, Ui.Parchment, Join, 34f));
            _problem = joining.Add(Ui.Paragraph("", Kit.SmallSize, Ui.Blood));

            var leave = Body.Add(new MenuWord("Go back to your own world", Kit.RowSize + 1f, Ui.Parchment, () => _game.ReturnHome(null), 34f));

            Body.Add(new Spacer(height: 22f));
            Body.Add(Kit.Group("In this game"));
            Body.Add(Ui.Paragraph("", Kit.RowSize, Ui.Parchment).Bind(() => string.Join(", ", (_game.Client?.Party ?? new()).Select(p => p.name))));
            Body.Add(new Spacer(height: 8f));
            Body.Add(Ui.Paragraph($"Up to {Protocol.MaxPlayers} heroes can share a world. Your hero and what they find come home with you.", Kit.SmallSize, Kit.Quiet));

            // Decided here rather than by each of them, as a hidden element isn't updated and could never show itself again.
            Updated += _ =>
            {
                open.Visible = !_game.Hosting && !_game.InFriendsGame;
                invite.Visible = _game.Hosting;
                copy.Visible = _game.Hosting;
                status.Visible = _game.Hosting;
                hosting.Visible = !_game.InFriendsGame;
                joining.Visible = !_game.Hosting;
                leave.Visible = _game.InFriendsGame;
            };
        }

        /// <summary>This page is made before anyone's chosen who to play.</summary>
        public void Open()
        {
            _name.Text = _game.HeroName;
            _problem.Text = "";
        }

        private void Join()
        {
            _problem.Text = _game.Join(_code.Text) ? "" : "That doesn't look like an invite.";
        }
    }
}

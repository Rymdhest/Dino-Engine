using Dino_Defenders.Progression;
using Dino_Engine.GUI;
using Dino_Engine.GUI.Rendering;
using Dino_Engine.GUI.Widgets;
using Dino_Engine.Sound;
using Dino_Engine.Sound.Content;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Dino_Defenders.UI
{
    /// <summary>What the main menu needs from the game around it.</summary>
    internal interface IMenuHost
    {
        IReadOnlyList<RosterEntry> Heroes { get; }
        RosterEntry? Chosen { get; }
        void Choose(RosterEntry entry);
        void Create(string name);
        void Delete(RosterEntry entry);
        void Play();
        void Quit();
    }

    /// <summary>One hero on the list: their name and how far they've come, and a line beside whoever's chosen.</summary>
    internal sealed class MenuHero : GuiElement
    {
        public const float RowHeight = 58f;

        private readonly IMenuHost _host;
        private readonly RosterEntry _entry;

        public MenuHero(IMenuHost host, RosterEntry entry)
        {
            _host = host;
            _entry = entry;
            Width = UIValue.Fill;
            Height = RowHeight;
            HitTest = HitTestMode.Block;
            Cursor = MouseCursor.Hand;
            Clicked += e =>
            {
                if (e.Button != MouseButton.Left) return;
                Audio.Play(Interface.Click);
                _host.Choose(_entry);
            };
        }

        protected override void OnDraw(GuiDrawList draw)
        {
            base.OnDraw(draw);
            if (Context == null) return;

            bool chosen = _host.Chosen == _entry;
            if (chosen) draw.AddRect(new GuiRect(Bounds.X, Bounds.Y + 9f, 2f, Bounds.Height - 18f), Ui.Gold);

            float x = Bounds.X + 18f;
            float width = Bounds.Width - 18f;
            Ui.Text(Context, draw, _entry.Hero.Name, 21f, new GuiRect(x, Bounds.Y + 7f, width, 28f), chosen ? Ui.Gold : IsHovered ? Ui.Parchment : Ui.Parchment.MultiplyAlpha(0.72f));
            Ui.Text(Context, draw, $"Level {_entry.Hero.Level}", 14.5f, new GuiRect(x, Bounds.Y + 33f, width, 20f), Ui.Parchment.MultiplyAlpha(0.75f));
        }
    }

    /// <summary>
    /// The first thing anyone sees, so there's as little of it as there can be: the place is the picture, and what's laid over it is a name, a list and one thing to do. Nothing has a box round it but what's being typed in.
    /// </summary>
    internal sealed class MenuScreen : GuiElement
    {
        private const float ColumnWidth = 320f;
        private const float Edge = Kit.Edge;
        private const float MostRows = 8f;
        private const float ShadeWidth = 620f;

        private readonly IMenuHost _host;
        private readonly StackPanel _rows;
        private readonly ScrollPanel _list;
        private readonly MenuWord _new;
        private readonly TextBox _name;
        private readonly GuiElement _action;
        private readonly MenuWord _play;
        private readonly MenuWord _delete;
        private readonly GuiElement _confirm;
        private readonly MenuWord _confirmText;
        private bool _naming;
        private bool _deleting;

        public MenuScreen(IMenuHost host)
        {
            _host = host;
            Width = UIValue.Fill;
            Height = UIValue.Fill;
            HitTest = HitTestMode.PassThrough;
            Visible = false;

            var column = Add(new StackPanel(Orientation.Vertical, 4f) { Width = ColumnWidth, Height = UIValue.Fill, Margin = new Thickness(Edge, Edge, 0f, Edge) });
            column.Add(new Label(BuildInfo.Name) { Font = Ui.Face, FontSize = 42f, Color = Ui.Parchment, Height = 56f });
            column.Add(new Spacer(height: 34f));

            _list = column.Add(new ScrollPanel { Width = UIValue.Fill, Height = UIValue.Fill });
            _rows = _list.Content.Add(new StackPanel(Orientation.Vertical, 2f) { Width = UIValue.Fill, Height = UIValue.Auto });

            _new = column.Add(new MenuWord("New hero", 18f, Ui.Parchment, BeginNaming, 36f));
            _name = column.Add(new TextBox("", "Name your hero") { Width = UIValue.Fill, Height = 34f, MaxLength = HeroRoster.LongestName, Visible = false });
            _name.BackgroundColor = GuiColors.Rgba(14, 11, 9, 200);
            _name.Submitted += text => FinishNaming(text);

            column.Add(new Spacer(height: 14f));
            column.Add(new MenuWord("Settings", 16f, Ui.Parchment.MultiplyAlpha(0.8f), () => GameUi.Current.Into(GameUi.Current.Settings), 28f));
            column.Add(new MenuWord("Quit", 16f, Ui.Parchment.MultiplyAlpha(0.8f), host.Quit, 28f));

            _action = Add(new StackPanel(Orientation.Vertical, 2f) { Anchor = Anchor.BottomRight, Width = ColumnWidth, Height = UIValue.Auto, Margin = new Thickness(0f, 0f, Edge, Edge) });
            _play = _action.Add(new MenuWord("Enter the world", 30f, Ui.Parchment, host.Play, 44f, Alignment.End));
            _delete = _action.Add(new MenuWord("Delete this hero", 15f, Ui.Parchment.MultiplyAlpha(0.8f), () => _deleting = true, 24f, Alignment.End));

            _confirm = _action.Add(new StackPanel(Orientation.Vertical, 2f) { Width = UIValue.Fill, Height = UIValue.Auto, Visible = false });
            _confirmText = _confirm.Add(new MenuWord("", 20f, Ui.Parchment, () => { }, 30f, Alignment.End) { HitTest = HitTestMode.None });
            _confirm.Add(new MenuWord("Delete", 18f, GuiColors.Rgba(236, 120, 96), DeleteChosen, 28f, Alignment.End));
            _confirm.Add(new MenuWord("Keep them", 16f, Ui.Parchment, () => _deleting = false, 26f, Alignment.End));
        }

        private void BeginNaming()
        {
            _naming = true;
            _name.Text = "";

            // Before it's focused, as what's hidden loses the focus.
            _name.Visible = true;
            _new.Visible = false;
            _name.Focus();
        }

        private void FinishNaming(string text)
        {
            _naming = false;
            if (HeroRoster.Clean(text) == null) return;
            Audio.Play(Interface.Equip);
            _host.Create(text);
            Rebuild();
        }

        private void DeleteChosen()
        {
            _deleting = false;
            if (_host.Chosen is not RosterEntry entry) return;
            _host.Delete(entry);
            Rebuild();
        }

        /// <summary>Goes through who there is, again.</summary>
        public void Rebuild()
        {
            _rows.ClearChildren();
            foreach (RosterEntry entry in _host.Heroes) _rows.Add(new MenuHero(_host, entry));
            _deleting = false;

            // With nobody to choose, the only thing to do is to make someone.
            if (_host.Heroes.Count == 0) BeginNaming();
        }

        protected override void OnUpdate(float deltaTime)
        {
            _list.Height = MathF.Min(_host.Heroes.Count, MostRows) * (MenuHero.RowHeight + 2f);
            _new.Visible = !_naming;
            _name.Visible = _naming;

            // Leaving the box without a name is thinking better of it, as long as there's someone to go back to.
            if (_naming && !_name.IsFocused && _host.Heroes.Count > 0) _naming = false;

            bool chosen = _host.Chosen != null;
            _action.Visible = chosen;
            _play.Visible = chosen && !_deleting;
            _delete.Visible = chosen && !_deleting;
            _confirm.Visible = chosen && _deleting;
            if (_host.Chosen is RosterEntry entry) _confirmText.Text = $"Delete {entry.Hero.Name}?";
        }

        // Only enough dark down the side for the words to stand on, fading to nothing well before the hero.
        protected override void OnDraw(GuiDrawList draw)
        {
            base.OnDraw(draw);
            draw.AddRect(new GuiRect(Bounds.X - ShadeWidth, Bounds.Y - ShadeWidth, ShadeWidth * 1.6f, Bounds.Height + ShadeWidth * 2f), GuiColors.Rgba(8, 6, 5, 190), 0f, 0f, default, ShadeWidth);
            draw.AddRect(new GuiRect(Bounds.Right - ShadeWidth * 0.5f, Bounds.Bottom - 120f, ShadeWidth, 320f), GuiColors.Rgba(8, 6, 5, 130), 0f, 0f, default, 240f);
        }
    }
}

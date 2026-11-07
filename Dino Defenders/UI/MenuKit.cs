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
    /// <summary>
    /// Everything the menus are made of, so that they're all one thing: words on a dark scrim, a rail of them down the left, and rows of a name and a control. One size for each kind of text, one colour for what's
    /// chosen and one for what isn't, and no box round anything that doesn't take typing. A new page is a <see cref="MenuPage"/> with rows on it. See MENU.md.
    /// </summary>
    internal static class Kit
    {
        public const float Edge = 64f;
        public const float RailWidth = 300f;
        public const float BodyWidth = 720f;
        public const float BodyGap = 56f;
        public const float TitleSize = 42f;
        public const float NavSize = 24f;
        public const float NavHeight = 42f;
        public const float GroupSize = 15f;
        public const float RowSize = 17f;
        public const float SmallSize = 14f;
        public const float RowHeight = 44f;
        public const float ControlWidth = 300f;

        public static readonly Colour Scrim = GuiColors.Rgba(9, 7, 6, 214);
        public static readonly Colour Quiet = GuiColors.Rgba(228, 214, 186, 150);
        public static readonly Colour Line = GuiColors.Rgba(228, 214, 186, 50);

        /// <summary>A place to type, the only thing here with an edge.</summary>
        public static TextBox Field(string text, string placeholder, int longest = 64)
        {
            var box = new TextBox(text, placeholder) { Width = UIValue.Fill, Height = 36f, MaxLength = longest, FocusBorderColor = Ui.Gold };
            box.BackgroundColor = GuiColors.Rgba(14, 11, 9, 210);
            box.BorderColor = Line;
            return box;
        }

        /// <summary>A heading for a group of rows.</summary>
        public static ScrollPanel Scroll() => new ScrollPanel { Width = UIValue.Fill, Height = UIValue.Fill, ThumbColor = Line, ThumbHoverColor = Quiet };

        public static Label Group(string text) => new Label(text) { Font = Ui.Face, FontSize = GroupSize, Color = Ui.Gold, Height = 34f };
    }

    /// <summary>Text that's a button, as nearly everything on these screens is: it brightens when it's looked at, and there's nothing else to it.</summary>
    internal sealed class MenuWord : GuiElement
    {
        private readonly float _size;
        private readonly Colour _colour;
        private readonly Action _pressed;
        private readonly Alignment _align;

        public string Text;

        /// <summary>Marks it as where the rail is at.</summary>
        public bool Chosen;

        public MenuWord(string text, float size, Colour colour, Action pressed, float height, Alignment align = Alignment.Start)
        {
            Text = text;
            _size = size;
            _colour = colour;
            _pressed = pressed;
            _align = align;
            Width = UIValue.Fill;
            Height = height;
            HitTest = HitTestMode.Block;
            Cursor = MouseCursor.Hand;
            Clicked += e =>
            {
                if (e.Button != MouseButton.Left) return;
                Audio.Play(Interface.Click);
                _pressed();
            };
        }

        protected override void OnDraw(GuiDrawList draw)
        {
            base.OnDraw(draw);
            if (Context == null) return;

            if (Chosen) draw.AddRect(new GuiRect(Bounds.X - 16f, Bounds.Y + 9f, 2f, Bounds.Height - 18f), Ui.Gold);
            Ui.Text(Context, draw, Text, _size, Bounds, IsHovered || Chosen ? Ui.Gold : _colour, _align);
        }
    }

    /// <summary>
    /// A whole screen of menu: a title and a rail of words on the left, and whatever the page is about beside it. Everything behind it is dimmed but still there, as the game is only held still. Whatever opened it
    /// is what Back goes to: see <see cref="GameUi.Back"/>.
    /// </summary>
    internal class MenuPage : GuiElement
    {
        protected readonly StackPanel Rail;
        protected readonly GuiElement Body;

        public MenuPage(string title, bool back = true)
        {
            Width = UIValue.Fill;
            Height = UIValue.Fill;
            BackgroundColor = Kit.Scrim;
            HitTest = HitTestMode.Block;
            Visible = false;

            var columns = Add(new StackPanel(Orientation.Horizontal, Kit.BodyGap) { Width = UIValue.Fill, Height = UIValue.Fill, Margin = new Thickness(Kit.Edge, Kit.Edge, Kit.Edge, Kit.Edge) });
            var side = columns.Add(new StackPanel(Orientation.Vertical, 2f) { Width = Kit.RailWidth, Height = UIValue.Fill });
            side.Add(new Label(title) { Font = Ui.Face, FontSize = Kit.TitleSize, Color = Ui.Parchment, Height = 56f });
            side.Add(new Spacer(height: 34f));
            Rail = side.Add(new StackPanel(Orientation.Vertical, 2f) { Width = UIValue.Fill, Height = UIValue.Auto });
            side.Add(new Spacer(height: UIValue.Fill));
            if (back) side.Add(new MenuWord("Back", 18f, Ui.Parchment, () => GameUi.Current.Back(), 34f));

            Body = columns.Add(new StackPanel(Orientation.Vertical, 2f) { Width = Kit.BodyWidth, Height = UIValue.Fill, Margin = new Thickness(0f, 90f, 0f, 0f) });
        }
    }

    /// <summary>A name on the left and a control on the right, which is what every setting is.</summary>
    internal sealed class MenuRow : GuiElement
    {
        private readonly string _name;

        public MenuRow(string name, GuiElement control)
        {
            _name = name;
            Width = UIValue.Fill;
            Height = Kit.RowHeight;
            HitTest = HitTestMode.PassThrough;
            control.Anchor = Anchor.Right;
            control.Width = Kit.ControlWidth;
            Add(control);
        }

        protected override void OnDraw(GuiDrawList draw)
        {
            base.OnDraw(draw);
            if (Context != null) Ui.Text(Context, draw, _name, Kit.RowSize, new GuiRect(Bounds.X, Bounds.Y, Bounds.Width - Kit.ControlWidth, Bounds.Height), Ui.Parchment);
        }
    }

    /// <summary>One of a few things, stepped through with an arrow each side.</summary>
    internal sealed class MenuStepper : GuiElement
    {
        private const float ArrowRoom = 34f;

        private readonly Func<IReadOnlyList<string>> _options;
        private readonly Func<int> _at;
        private readonly Action<int> _chosen;

        /// <summary>Whether it can be changed at all, as a borderless window has no size of its own.</summary>
        public Func<bool> Enabled = () => true;

        public MenuStepper(Func<IReadOnlyList<string>> options, Func<int> at, Action<int> chosen)
        {
            _options = options;
            _at = at;
            _chosen = chosen;
            Height = 32f;
            HitTest = HitTestMode.Block;
            Cursor = MouseCursor.Hand;
            Clicked += e =>
            {
                if (e.Button != MouseButton.Left || !Enabled()) return;
                int count = _options().Count;
                bool left = e.Position.X < Bounds.Center.X;
                int next = Math.Clamp(_at() + (left ? -1 : 1), 0, count - 1);
                if (next == _at()) return;
                Audio.Play(Interface.Click);
                _chosen(next);
            };
        }

        protected override void OnDraw(GuiDrawList draw)
        {
            base.OnDraw(draw);
            if (Context == null) return;

            IReadOnlyList<string> options = _options();
            int at = _at();
            bool on = Enabled();
            Vector2 mouse = Context.MousePosition;
            bool left = IsHovered && mouse.X < Bounds.Center.X;

            Ui.Text(Context, draw, at >= 0 && at < options.Count ? options[at] : "", Kit.RowSize, Bounds, on ? Ui.Parchment : Kit.Quiet, Alignment.Center);
            if (!on) return;
            Arrow(draw, new Vector2(Bounds.X + ArrowRoom * 0.5f, Bounds.Center.Y), -1f, at > 0, IsHovered && left);
            Arrow(draw, new Vector2(Bounds.Right - ArrowRoom * 0.5f, Bounds.Center.Y), 1f, at < options.Count - 1, IsHovered && !left);
        }

        private static void Arrow(GuiDrawList draw, Vector2 at, float way, bool usable, bool lit)
        {
            Colour colour = !usable ? Kit.Line : lit ? Ui.Gold : Kit.Quiet;
            draw.AddLine(at + new Vector2(-4f * way, -7f), at + new Vector2(4f * way, 0f), colour, 1.5f);
            draw.AddLine(at + new Vector2(4f * way, 0f), at + new Vector2(-4f * way, 7f), colour, 1.5f);
        }
    }

    /// <summary>A line with a dot on it, from nothing to all of it.</summary>
    internal sealed class MenuSlider : GuiElement
    {
        private const float NumberRoom = 52f;

        private readonly Func<float> _value;
        private readonly Action<float> _changed;

        public MenuSlider(Func<float> value, Action<float> changed)
        {
            _value = value;
            _changed = changed;
            Height = 32f;
            HitTest = HitTestMode.Block;
            Cursor = MouseCursor.Hand;
            MouseDown += e => Take(e.Position.X);
            Dragged += e => Take(e.Position.X);
        }

        private void Take(float x)
        {
            float share = Math.Clamp((x - Bounds.X) / MathF.Max(Bounds.Width - NumberRoom, 1f), 0f, 1f);
            _changed(share);
        }

        protected override void OnDraw(GuiDrawList draw)
        {
            base.OnDraw(draw);
            if (Context == null) return;

            float value = Math.Clamp(_value(), 0f, 1f);
            float width = Bounds.Width - NumberRoom;
            float y = Bounds.Center.Y;
            draw.AddRect(new GuiRect(Bounds.X, y - 1f, width, 2f), Kit.Line);
            draw.AddRect(new GuiRect(Bounds.X, y - 1f, width * value, 2f), Ui.Gold);
            draw.AddCircle(new Vector2(Bounds.X + width * value, y), IsHovered ? 8f : 7f, Ui.Gold);
            Ui.Text(Context, draw, $"{value * 100f:0}%", Kit.SmallSize, new GuiRect(Bounds.Right - NumberRoom, Bounds.Y, NumberRoom, Bounds.Height), Kit.Quiet, Alignment.End);
        }
    }

    /// <summary>The same line for a few fixed stops, each named under where it is.</summary>
    internal sealed class MenuStops : GuiElement
    {
        private readonly string[] _names;
        private readonly Func<int> _at;
        private readonly Action<int> _chosen;

        public MenuStops(string[] names, Func<int> at, Action<int> chosen)
        {
            _names = names;
            _at = at;
            _chosen = chosen;
            Height = 44f;
            HitTest = HitTestMode.Block;
            Cursor = MouseCursor.Hand;
            MouseDown += e => Take(e.Position.X);
            Dragged += e => Take(e.Position.X);
        }

        private float Inset => Bounds.Width / (_names.Length * 2f);

        private float XOf(int stop) => Bounds.X + Inset + (Bounds.Width - Inset * 2f) * stop / (_names.Length - 1f);

        private void Take(float x)
        {
            int nearest = 0;
            for (int stop = 1; stop < _names.Length; stop++)
            {
                if (MathF.Abs(x - XOf(stop)) < MathF.Abs(x - XOf(nearest))) nearest = stop;
            }
            if (nearest == _at()) return;
            Audio.Play(Interface.Click);
            _chosen(nearest);
        }

        protected override void OnDraw(GuiDrawList draw)
        {
            base.OnDraw(draw);
            if (Context == null) return;

            int at = _at();
            float y = Bounds.Y + 12f;
            draw.AddRect(new GuiRect(XOf(0), y - 1f, XOf(_names.Length - 1) - XOf(0), 2f), Kit.Line);
            draw.AddRect(new GuiRect(XOf(0), y - 1f, XOf(at) - XOf(0), 2f), Ui.Gold);
            for (int stop = 0; stop < _names.Length; stop++)
            {
                bool chosen = stop == at;
                draw.AddCircle(new Vector2(XOf(stop), y), chosen ? 7f : 3.5f, stop <= at ? Ui.Gold : Kit.Quiet);
                float width = Inset * 2f;
                Ui.Text(Context, draw, _names[stop], Kit.SmallSize + 1f, new GuiRect(XOf(stop) - width * 0.5f, Bounds.Y + 22f, width, 22f), chosen ? Ui.Gold : Kit.Quiet, Alignment.Center);
            }
        }
    }

    /// <summary>On or off.</summary>
    internal sealed class MenuToggle : GuiElement
    {
        private const float PillWidth = 40f;
        private const float PillHeight = 20f;

        private readonly Func<bool> _on;

        public MenuToggle(Func<bool> on, Action<bool> changed)
        {
            _on = on;
            Height = 32f;
            HitTest = HitTestMode.Block;
            Cursor = MouseCursor.Hand;
            Clicked += e =>
            {
                if (e.Button != MouseButton.Left) return;
                Audio.Play(Interface.Click);
                changed(!_on());
            };
        }

        protected override void OnDraw(GuiDrawList draw)
        {
            base.OnDraw(draw);
            if (Context == null) return;

            bool on = _on();
            GuiRect pill = new GuiRect(Bounds.Right - PillWidth, Bounds.Center.Y - PillHeight * 0.5f, PillWidth, PillHeight);
            draw.AddRect(pill, on ? Ui.Gold : GuiColors.Transparent, PillHeight * 0.5f, 1.5f, on ? Ui.Gold : Kit.Quiet);
            draw.AddCircle(new Vector2(on ? pill.Right - PillHeight * 0.5f : pill.X + PillHeight * 0.5f, pill.Center.Y), PillHeight * 0.5f - 4f, on ? GuiColors.Rgba(14, 11, 9) : Kit.Quiet);
            Ui.Text(Context, draw, on ? "On" : "Off", Kit.SmallSize + 1f, new GuiRect(Bounds.X, Bounds.Y, Bounds.Width - PillWidth - 12f, Bounds.Height), Kit.Quiet, Alignment.End);
        }
    }
}

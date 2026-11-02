using Dino_Engine.GUI.Rendering;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Dino_Engine.GUI.Widgets
{
    /// <summary>Element whose children are stacked vertically or horizontally.</summary>
    public class StackPanel : GuiElement
    {
        public StackPanel(Orientation orientation = Orientation.Vertical, float spacing = 4f)
        {
            Layout = new StackLayout(orientation, spacing);
        }

        public StackLayout? Stack => Layout as StackLayout;
    }

    /// <summary>Element whose children are laid out in a grid of columns.</summary>
    public class GridPanel : GuiElement
    {
        public GridPanel(int columns, float columnSpacing = 6f, float rowSpacing = 2f, params float[] columnWeights)
        {
            Layout = new GridLayout(columns, columnSpacing, rowSpacing) { ColumnWeights = columnWeights.Length > 0 ? columnWeights : null };
            Width = UIValue.Fill;
        }

        public GridLayout? Grid => Layout as GridLayout;
    }

    /// <summary>Thin horizontal (or vertical) divider line.</summary>
    public class Separator : GuiElement
    {
        public Separator(Orientation orientation = Orientation.Horizontal)
        {
            GuiTheme theme = Theme;
            BackgroundColor = theme.Separator;
            if (orientation == Orientation.Horizontal)
            {
                Width = UIValue.Fill;
                Height = 1f;
                Margin = new Thickness(0f, 3f);
            }
            else
            {
                Width = 1f;
                Height = UIValue.Fill;
                Margin = new Thickness(3f, 0f);
            }
            HitTest = HitTestMode.None;
        }
    }

    /// <summary>Empty element that takes space; with Fill it pushes siblings apart in a stack.</summary>
    public class Spacer : GuiElement
    {
        public Spacer(UIValue? width = null, UIValue? height = null)
        {
            Width = width ?? UIValue.Fill;
            Height = height ?? UIValue.Fill;
            HitTest = HitTestMode.None;
        }
    }

    /// <summary>Collapsible section: a clickable header with an arrow, and a <see cref="Content"/> stack shown when expanded.</summary>
    public class Foldout : GuiElement
    {
        private readonly HeaderElement _header;

        public GuiElement Content { get; }
        public Label HeaderLabel { get; }
        public string Title { get => HeaderLabel.Text; set => HeaderLabel.Text = value; }

        public event Action<bool>? ExpandedChanged;

        public Foldout(string title, bool expanded = true)
        {
            GuiTheme theme = Theme;
            Width = UIValue.Fill;
            Layout = new StackLayout(Orientation.Vertical, 2f);
            _header = Add(new HeaderElement(this)
            {
                Width = UIValue.Fill,
                Height = theme.ControlHeight - 2f,
                Padding = new Thickness(20f, 0f, 6f, 0f),
                CornerRadius = theme.CornerRadius,
            });
            HeaderLabel = _header.Add(new Label(title) { Anchor = Anchor.Left, HitTest = HitTestMode.None, Color = theme.Text });
            Content = Add(new GuiElement
            {
                Name = "FoldoutContent",
                Width = UIValue.Fill,
                Padding = new Thickness(8f, 2f, 0f, 4f),
                Layout = new StackLayout(Orientation.Vertical, theme.Spacing),
                Visible = expanded,
            });
        }

        public bool Expanded
        {
            get => Content.Visible;
            set
            {
                if (Content.Visible == value) return;
                Content.Visible = value;
                ExpandedChanged?.Invoke(value);
            }
        }

        private sealed class HeaderElement : GuiElement
        {
            private readonly Foldout _owner;

            public HeaderElement(Foldout owner)
            {
                _owner = owner;
                CapturesMouse = true;
                Cursor = MouseCursor.Hand;
            }

            protected override void OnClick(GuiMouseEventArgs e)
            {
                if (e.Button == MouseButton.Left) _owner.Expanded = !_owner.Expanded;
            }

            protected override void OnDraw(GuiDrawList drawList)
            {
                GuiTheme theme = Theme;
                drawList.AddRect(Bounds, IsHovered ? theme.ControlHover : theme.Control.WithAlpha(0.6f), CornerRadius);
                GuiRect arrow = new GuiRect(Bounds.X + 7f, Bounds.Center.Y - 4f, 8f, 8f);
                GuiIcons.Draw(drawList, _owner.Expanded ? GuiIcon.ChevronDown : GuiIcon.ChevronRight, arrow, theme.TextMuted);
            }
        }
    }
}

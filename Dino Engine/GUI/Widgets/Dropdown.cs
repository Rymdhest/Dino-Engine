using Dino_Engine.GUI.Rendering;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Dino_Engine.GUI.Widgets
{
    /// <summary>
    /// Shows the selected item; clicking opens a list in the context's popup layer (above everything else).
    /// The list flips above the dropdown when there is no room below and scrolls after <see cref="MaxVisibleItems"/>.
    /// </summary>
    public class Dropdown : GuiElement
    {
        private readonly List<string> _items = new();
        private int _selectedIndex = -1;
        private GuiElement? _popup;
        private ScrollPanel? _list;

        public Label Label { get; }
        public string Placeholder { get; set; } = "Select...";
        public int MaxVisibleItems { get; set; } = 10;
        public IReadOnlyList<string> Items => _items;
        public bool IsOpen => _popup != null && Context != null && Context.IsPopupOpen(_popup);

        public event Action<int>? SelectionChanged;

        public Dropdown(IEnumerable<string>? items = null, int selectedIndex = -1)
        {
            GuiTheme theme = Theme;
            CapturesMouse = true;
            Cursor = MouseCursor.Hand;
            Width = 160f;
            Height = theme.ControlHeight;
            Padding = new Thickness(8f, 0f, 24f, 0f);
            CornerRadius = theme.CornerRadius;
            BorderWidth = theme.BorderWidth;
            BorderColor = theme.ControlBorder;
            ClipChildren = true;
            Label = Add(new Label("") { Width = UIValue.Fill, Anchor = Anchor.Left, HitTest = HitTestMode.None });
            if (items != null) _items.AddRange(items);
            _selectedIndex = selectedIndex >= 0 && selectedIndex < _items.Count ? selectedIndex : -1;
            RefreshLabel();
        }

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                int index = value >= 0 && value < _items.Count ? value : -1;
                if (index == _selectedIndex) return;
                _selectedIndex = index;
                RefreshLabel();
                SelectionChanged?.Invoke(index);
            }
        }

        public string? SelectedItem => _selectedIndex >= 0 ? _items[_selectedIndex] : null;

        public void SetItems(IEnumerable<string> items, int selectedIndex = -1)
        {
            Close();
            _items.Clear();
            _items.AddRange(items);
            _selectedIndex = -1;
            SelectedIndex = selectedIndex;
            RefreshLabel();
        }

        public void AddItem(string item) => _items.Add(item);

        private void RefreshLabel()
        {
            bool hasSelection = _selectedIndex >= 0;
            Label.Text = hasSelection ? _items[_selectedIndex] : Placeholder;
            Label.Color = hasSelection ? null : Theme.TextMuted;
        }

        protected override void OnClick(GuiMouseEventArgs e)
        {
            if (e.Button != MouseButton.Left) return;
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (Context == null || _items.Count == 0) return;
            GuiTheme theme = Theme;
            float rowHeight = theme.ControlHeight - 2f;
            _popup = new GuiElement
            {
                Name = "DropdownPopup",
                BackgroundColor = theme.PanelBackground.WithAlpha(1f),
                BorderColor = theme.ControlBorder,
                BorderWidth = 1f,
                CornerRadius = theme.CornerRadius,
                ShadowSize = 10f,
                Padding = 3f,
                HitTest = HitTestMode.Block,
            };
            _list = _popup.Add(new ScrollPanel { Width = UIValue.Fill, MaxHeight = rowHeight * MaxVisibleItems + 2f });
            ((StackLayout)_list.Content.Layout!).Spacing = 0f;
            for (int i = 0; i < _items.Count; i++)
            {
                int index = i;
                var row = _list.Content.Add(new DropdownRow(this, index) { Width = UIValue.Fill, Height = rowHeight, Padding = new Thickness(8f, 0f) });
                row.Add(new Label(_items[i]) { Anchor = Anchor.Left, HitTest = HitTestMode.None });
            }
            Context.OpenPopup(_popup, this, () => { _popup = null; _list = null; });
            InvalidateLayout();
        }

        public void Close()
        {
            if (_popup != null) Context?.ClosePopup(_popup);
            _popup = null;
            _list = null;
        }

        internal void Choose(int index)
        {
            SelectedIndex = index;
            Close();
        }

        protected override void ArrangeContent(GuiRect content)
        {
            base.ArrangeContent(content);
            if (_popup == null || Context == null) return;
            // Position the popup under (or above) the header. The popup layer is arranged after the root layer.
            float popupHeight = _popup.DesiredSize.Y;
            float below = Bounds.Bottom + 2f;
            float y = below + popupHeight > Context.ScreenSize.Y - 4f && Bounds.Y - popupHeight - 2f >= 0f ? Bounds.Y - popupHeight - 2f : below;
            _popup.X = Bounds.X;
            _popup.Y = y;
            _popup.Width = Bounds.Width;
        }

        protected override void OnDraw(GuiDrawList drawList)
        {
            GuiTheme theme = Theme;
            Colour background = !IsEnabledInHierarchy ? theme.ControlDisabled : (IsHovered || IsOpen ? theme.ControlHover : theme.Control);
            DrawBackground(drawList, background);
        }

        protected override void OnDrawOverlay(GuiDrawList drawList)
        {
            GuiTheme theme = Theme;
            GuiRect arrow = new GuiRect(Bounds.Right - 18f, Bounds.Center.Y - 4f, 8f, 8f);
            GuiIcons.Draw(drawList, IsOpen ? GuiIcon.ChevronUp : GuiIcon.ChevronDown, arrow, IsEnabledInHierarchy ? theme.TextMuted : theme.TextDisabled);
        }

        private sealed class DropdownRow : GuiElement
        {
            private readonly Dropdown _owner;
            private readonly int _index;

            public DropdownRow(Dropdown owner, int index)
            {
                _owner = owner;
                _index = index;
                CapturesMouse = true;
                Cursor = MouseCursor.Hand;
            }

            protected override void OnClick(GuiMouseEventArgs e)
            {
                if (e.Button == MouseButton.Left) _owner.Choose(_index);
            }

            protected override void OnDraw(GuiDrawList drawList)
            {
                GuiTheme theme = Theme;
                if (_index == _owner.SelectedIndex) drawList.AddRect(Bounds, theme.Selection, theme.CornerRadius - 1f);
                if (IsHovered) drawList.AddRect(Bounds, theme.Highlight, theme.CornerRadius - 1f);
            }
        }
    }
}

using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Tooltips;
using Dino_Engine.GUI;
using Dino_Engine.GUI.Rendering;
using Dino_Engine.GUI.Widgets;
using Dino_Engine.Sound;
using Dino_Engine.Sound.Content;
using Dino_Engine.Util;

namespace Dino_Defenders.UI
{
    /// <summary>
    /// The beta vendor's side of the hero's panel, where the hero's own picture would be: a tab for each sort of thing he has (<see cref="BetaVendor.Tabs"/>), the level he stocks for, and a roll that
    /// changes everything he has out when asked. Anything of his is taken the way anything is bought, onto the cursor and into the pack, or at once with a right-click, and it stays where it was.
    /// His uniques are a lookup rather than a table: every one there is, as a list that says what each gives and what it costs, searched by everything about them and narrowed by what sort of thing and
    /// where it goes (<see cref="UniqueLookup"/>). What's out only changes when the level or the roll does, so it's kept until then.
    /// </summary>
    internal sealed class BetaVendorPanel : StackPanel
    {
        /// <summary>How far apart each tab's places are numbered, so that a place in the hero's panel says which tab it's on.</summary>
        public const int Stride = 4096;

        private const int Columns = 11;
        private const float CellSize = 58f;
        private const float CellGap = 6f;
        private const int ChipColumns = 6;
        private const float ChipHeight = 28f;
        private const float RowLabel = 52f;

        private static readonly Colour Chosen = GuiColors.Rgba(88, 66, 34);
        private static readonly Colour Unchosen = GuiColors.Rgba(120, 96, 58);

        private readonly HeroPanel _panel;
        private readonly int _lookupTab;
        private readonly List<Button> _tabs = new();
        private readonly ScrollPanel _shelf;
        private readonly List<ItemCell> _cells = new();
        private readonly GuiElement _lookup;
        private readonly TextBox _search;
        private readonly Button _clear;
        private readonly List<(Button chip, LookupType type)> _types = new();
        private readonly GridPanel _seconds;
        private readonly Label _secondsName;
        private readonly List<(Button chip, string? value)> _secondChips = new();
        private readonly List<(Button chip, LookupOrder order)> _orders = new();
        private readonly ScrollPanel _list;
        private readonly List<UniqueRow> _rows = new();
        private readonly List<Item>?[] _stock;
        private List<LookupEntry>? _entries;
        private LookupWord[] _words = Array.Empty<LookupWord>();
        private LookupType _type;
        private string? _second;
        private LookupOrder _order;
        private bool _sift = true;          // What the lookup shows has to be worked out again.
        private bool _resort = true;
        private int _shown;
        private int _tab;
        private int _roll = NewRoll();
        private int _level = 1;
        private bool _opened;

        public BetaVendorPanel(HeroPanel panel, float width) : base(Orientation.Vertical, 6f)
        {
            _panel = panel;
            Width = width;
            Height = UIValue.Fill;
            Visible = false;
            MouseDown += e => panel.PressOn(this, e);
            _lookupTab = Array.FindIndex(BetaVendor.Tabs, tab => tab.Lookup);
            _stock = new List<Item>?[BetaVendor.Tabs.Length];

            Add(Ui.Heading("Beta Vendor"));
            Add(Ui.Line("Here for testing the game. Everything is free, and stays on the table to be taken again.", 13.5f, Ui.Faded));

            var tabs = Add(new StackPanel(Orientation.Horizontal, 4f) { Width = UIValue.Fill, Height = 30f });
            for (int t = 0; t < BetaVendor.Tabs.Length; t++)
            {
                int tab = t;
                Button button = tabs.Add(Ui.Action(BetaVendor.Tabs[t].Name, () => ShowTab(tab)));
                button.Width = UIValue.Relative(1f / BetaVendor.Tabs.Length, -4f);
                button.Height = 30f;
                _tabs.Add(button);
            }

            var controls = Add(new StackPanel(Orientation.Horizontal, 4f) { Width = UIValue.Fill, Height = 30f });
            controls.Stack!.CrossAlignment = Alignment.Center;
            controls.Add(Ui.Line("Item Level", 15f, Ui.Gold)).Width = 84f;
            Step(controls, "«", -10);
            Step(controls, "‹", -1);
            Label level = controls.Add(Ui.Line("", 16f, Ui.Parchment).Bind(() => _level.ToString()));
            level.Width = 38f;
            level.HorizontalTextAlignment = Alignment.Center;
            Step(controls, "›", 1);
            Step(controls, "»", 10);
            controls.Add(new Spacer(height: 1f));
            Button reroll = controls.Add(Ui.Action("Reroll everything", Reroll));
            reroll.Width = 180f;
            reroll.Height = 30f;

            _shelf = Add(Kit.Scroll());
            _shelf.MouseDown += e => panel.PressOn(_shelf, e);
            var grid = _shelf.Content.Add(new GridPanel(Columns, CellGap, CellGap) { Width = Columns * CellSize + (Columns - 1) * CellGap });
            int most = Enumerable.Range(0, BetaVendor.Tabs.Length).Where(t => t != _lookupTab).Select(t => BetaVendor.Stock(t, 0, 1).Count).DefaultIfEmpty(0).Max();
            for (int i = 0; i < most; i++)
            {
                int index = i;
                _cells.Add(grid.Add(new ItemCell(panel, new Spot(Shelf.Vendor, index), CellSize, spotNow: () => new Spot(Shelf.Vendor, _tab * Stride + index))));
            }

            _lookup = Add(new StackPanel(Orientation.Vertical, 6f) { Width = UIValue.Fill, Height = UIValue.Fill, Visible = false });
            var find = _lookup.Add(new GuiElement { Width = UIValue.Fill, Height = 28f });
            _search = find.Add(new TextBox("", $"Search everything about them, or only one side with {UniqueLookup.GivesMark} or {UniqueLookup.CostsMark}") { Width = UIValue.Fill, Height = 28f, MaxLength = 64 });
            _search.TextChanged += text =>
            {
                _words = UniqueLookup.Words(text);
                _sift = true;
            };
            _search.Submitted += _ => _search.Unfocus();
            _clear = find.Add(Ui.Action("x", () => _search.Text = ""));
            _clear.Anchor = Anchor.Right;
            _clear.X = 3f;
            _clear.Width = 22f;
            _clear.Height = 22f;
            _clear.Padding = new Thickness(0f);
            _clear.MinWidth = 0f;
            _clear.Visible = false;

            var typeRow = _lookup.Add(new StackPanel(Orientation.Horizontal, 6f) { Width = UIValue.Fill, Height = ChipHeight });
            typeRow.Stack!.CrossAlignment = Alignment.Center;
            typeRow.Add(Ui.Line("Type", 14f, Ui.Gold)).Width = RowLabel;
            LookupType[] types = Enum.GetValues<LookupType>();
            var typeChips = typeRow.Add(new GridPanel(types.Length, 4f, 4f));
            foreach (LookupType type in types) _types.Add((typeChips.Add(Chip(() => Choose(type))), type));

            var secondRow = _lookup.Add(new StackPanel(Orientation.Horizontal, 6f) { Width = UIValue.Fill, Height = UIValue.Auto });
            _secondsName = secondRow.Add(Ui.Line("Slot", 14f, Ui.Gold));
            _secondsName.Width = RowLabel;
            _secondsName.Height = ChipHeight;
            _seconds = secondRow.Add(new GridPanel(ChipColumns, 4f, 4f));

            var tally = _lookup.Add(new StackPanel(Orientation.Horizontal, 4f) { Width = UIValue.Fill, Height = ChipHeight });
            tally.Stack!.CrossAlignment = Alignment.Center;
            tally.Add(Ui.Line("", 14f, Ui.Faded).Bind(() => $"{_shown} of {_rows.Count} shown"));
            tally.Add(new Spacer(height: 1f));
            foreach (LookupOrder order in Enum.GetValues<LookupOrder>())
            {
                Button chip = tally.Add(Chip(() => Sort(order)));
                chip.Width = 96f;
                chip.Label.Text = UniqueLookup.NameOf(order);
                _orders.Add((chip, order));
            }

            _list = _lookup.Add(Kit.Scroll());
            _list.MouseDown += e => panel.PressOn(_list, e);
            int uniques = _lookupTab >= 0 ? BetaVendor.Stock(_lookupTab, 0, 1).Count : 0;
            for (int i = 0; i < uniques; i++)
            {
                int index = i;
                _rows.Add(_list.Content.Add(new UniqueRow(panel, () => new Spot(Shelf.Vendor, _lookupTab * Stride + index), () => _list.Bounds)));
            }
        }

        private static int NewRoll() => Random.Shared.Next();

        private void Step(GuiElement row, string text, int by)
        {
            Button button = row.Add(Ui.Action(text, () => ChangeLevel(by)));
            button.Width = 30f;
            button.Height = 28f;
            button.MinWidth = 0f;
            button.Padding = new Thickness(0f);
        }

        private static Button Chip(Action pressed)
        {
            Button chip = Ui.Action("", pressed);
            chip.Height = ChipHeight;
            chip.MinWidth = 0f;
            chip.Padding = new Thickness(4f, 0f);
            chip.Label.FontSize = 14f;
            return chip;
        }

        // The one that's chosen stands out in gold, the same way whichever row it's in.
        private static void Mark(Button chip, bool chosen)
        {
            chip.NormalColor = chosen ? Chosen : Ui.InkLight;
            chip.BorderColor = chosen ? Ui.Gold : Unchosen;
            chip.Label.Color = chosen ? Ui.Gold : Ui.Parchment;
        }

        /// <summary>Whether something's being typed into the search, which is what Escape leaves first.</summary>
        public bool Typing => _search.IsFocused;

        /// <summary>Whether the uniques are what's showing, which is where a search goes.</summary>
        public bool LookingUp => _tab == _lookupTab;

        /// <summary>Turns to the uniques.</summary>
        public void ShowLookup() => _tab = Math.Max(_lookupTab, 0);

        public void FocusSearch()
        {
            _search.Focus();
            _search.SelectAll();
        }

        /// <summary>He's been walked up to. The first time, he stocks for the land the hero is going to next.</summary>
        public void Opened()
        {
            if (_opened || GameSession.Current == null) return;
            _opened = true;
            _level = BetaVendor.Clamp(GameSession.Current.Hero.LandLevel);
            Restock();
        }

        /// <summary>Everything on the tab that's showing, for the search of what's carried to count.</summary>
        public IEnumerable<Item> Showing() => LookingUp ? Array.Empty<Item>() : StockOf(_tab);

        /// <summary>What's in one of his places.</summary>
        public Item? At(int index)
        {
            int tab = index / Stride;
            int at = index % Stride;
            if (index < 0 || tab >= _stock.Length) return null;
            List<Item> stock = StockOf(tab);
            return at < stock.Count ? stock[at] : null;
        }

        /// <summary>Asks for one of his things, into a cell of the pack or the first that's free: it shows at once, and the server, which makes the same thing again, has the last word.</summary>
        public void Take(int index, int? cell) => GameSession.Current.Client.TakeOffer(index / Stride, _roll, _level, index % Stride, cell);

        private List<Item> StockOf(int tab) => _stock[tab] ??= BetaVendor.Stock(tab, _roll, _level);

        private List<LookupEntry> Entries()
        {
            if (_entries != null) return _entries;
            _entries = UniqueLookup.Entries(_lookupTab >= 0 ? StockOf(_lookupTab) : new List<Item>());
            for (int i = 0; i < _rows.Count && i < _entries.Count; i++) _rows[i].Entry = _entries[i];
            _sift = true;
            return _entries;
        }

        // What he has is made again for the roll and level, and anything of his on the cursor is put back, as it would be something else now.
        private void Restock()
        {
            Array.Clear(_stock);
            _entries = null;
            _sift = true;
            _panel.ForgetVendor();
        }

        private void ShowTab(int tab)
        {
            Audio.Play(Interface.Click);
            _tab = tab;
        }

        private void ChangeLevel(int by)
        {
            int level = BetaVendor.Clamp(_level + by);
            Audio.Play(Interface.Click);
            if (level == _level) return;
            _level = level;
            Restock();
        }

        private void Reroll()
        {
            Audio.Play(Interface.Click);
            _roll = NewRoll();
            Restock();
        }

        private void Choose(LookupType type)
        {
            Audio.Play(Interface.Click);
            if (type == _type) return;
            _type = type;
            _second = null;
            BuildSeconds();
        }

        private void ChooseSecond(string? value)
        {
            Audio.Play(Interface.Click);
            _second = value;
            _sift = true;
        }

        private void Sort(LookupOrder order)
        {
            Audio.Play(Interface.Click);
            _order = order;
            _resort = true;
        }

        // The second filter's choices are whatever there is of the type chosen, and none of them to begin with.
        private void BuildSeconds()
        {
            _seconds.ClearChildren();
            _secondChips.Clear();
            var values = new List<string?> { null };
            values.AddRange(UniqueLookup.Seconds(Entries(), _type));
            foreach (string? value in values) _secondChips.Add((_seconds.Add(Chip(() => ChooseSecond(value))), value));
            _secondsName.Text = _type == LookupType.Any ? "Slot" : "Kind";
            _sift = true;
        }

        protected override void OnUpdate(float deltaTime)
        {
            if (GameSession.Current == null) return;

            // Decided here rather than by each of them, as a hidden element isn't updated and could never show itself again.
            bool lookingUp = LookingUp;
            _shelf.Visible = !lookingUp;
            _lookup.Visible = lookingUp;
            for (int t = 0; t < _tabs.Count; t++) Mark(_tabs[t], t == _tab);
            if (!lookingUp)
            {
                int count = StockOf(_tab).Count;
                for (int i = 0; i < _cells.Count; i++) _cells[i].Visible = i < count;
                return;
            }

            _clear.Visible = _search.Text.Length > 0;
            List<LookupEntry> entries = Entries();
            if (_secondChips.Count == 0) BuildSeconds();
            if (_resort)
            {
                _resort = false;
                foreach (LookupEntry entry in UniqueLookup.Sorted(entries, _order)) _list.Content.Add(_rows[entry.Index]);
            }
            if (_sift)
            {
                _sift = false;
                Sift(entries);
            }
            foreach ((Button chip, LookupType type) in _types) Mark(chip, type == _type);
            foreach ((Button chip, string? value) in _secondChips) Mark(chip, value == _second);
            foreach ((Button chip, LookupOrder order) in _orders) Mark(chip, order == _order);
        }

        // Which rows show, and how many each choice would show with what's typed, so that it's plain where things are before choosing.
        private void Sift(List<LookupEntry> entries)
        {
            _shown = 0;
            for (int i = 0; i < _rows.Count && i < entries.Count; i++)
            {
                bool passes = UniqueLookup.Passes(entries[i], _type, _second, _words);
                _rows[i].Visible = passes;
                if (passes) _shown++;
            }
            foreach ((Button chip, LookupType type) in _types) chip.Label.Text = $"{UniqueLookup.NameOf(type)} {entries.Count(entry => UniqueLookup.Passes(entry, type, null, _words))}";
            foreach ((Button chip, string? value) in _secondChips) chip.Label.Text = $"{value ?? "All"} {entries.Count(entry => UniqueLookup.Passes(entry, _type, value, _words))}";
        }
    }

    /// <summary>
    /// One unique in the beta vendor's lookup: its picture, which can be taken like anything of his, and beside it what it's called, what it is, the least it's found at, and what it gives and what it costs,
    /// in the colours its card uses for each. Its height is whatever that comes to at the width it's given.
    /// </summary>
    internal sealed class UniqueRow : ItemCell
    {
        private const float Icon = 52f;
        private const float Pad = 6f;
        private const float NameSize = 16f;
        private const float SmallSize = 13f;
        private const float LineSize = 14f;
        private const float LineStep = 17f;
        private const float TextGap = 12f;
        private const float GuessedWidth = 700f;

        private readonly HeroPanel _panel;
        private readonly Func<GuiRect> _view;
        private readonly List<(string text, bool cost)> _lines = new();
        private LookupEntry? _entry;
        private float _wrappedFor = -1f;

        /// <summary>What it's showing, which is made again whenever what the vendor has is.</summary>
        public LookupEntry? Entry
        {
            get => _entry;
            set
            {
                _entry = value;
                _wrappedFor = -1f;
            }
        }

        /// <param name="view">What can be seen of the list, as nothing outside it needs drawing, and a picture asked for is a picture made.</param>
        public UniqueRow(HeroPanel panel, Func<Spot> spot, Func<GuiRect> view) : base(panel, spot(), Icon, spotNow: spot)
        {
            _panel = panel;
            _view = view;
            Width = UIValue.Fill;
            Height = Icon + Pad * 2f;
            ShowsPlaces = false;
        }

        protected override void OnUpdate(float deltaTime)
        {
            if (Context == null || _entry == null) return;
            float width = Bounds.Width > 0f ? Bounds.Width : GuessedWidth;
            if (MathF.Abs(width - _wrappedFor) < 1f) return;

            _wrappedFor = width;
            _lines.Clear();
            float room = MathF.Max(width - Pad - Icon - TextGap - Pad * 2f, 1f);
            foreach (string give in _entry.Gives)
            {
                foreach (string line in Ui.Wrap(Context, give, LineSize, room)) _lines.Add((line, false));
            }
            foreach (string cost in _entry.Costs)
            {
                foreach (string line in Ui.Wrap(Context, cost, LineSize, room)) _lines.Add((line, true));
            }
            Height = MathF.Max(Icon, NameSize + 4f + LineStep * (1 + _lines.Count)) + Pad * 2f;
        }

        protected override void OnDraw(GuiDrawList draw)
        {
            if (!_view().Intersects(Bounds)) return;
            DrawBackground(draw, BackgroundColor);
            if (Context == null || _entry == null || _panel.ItemAt(Spot) is not Item item) return;

            CellMood mood = _panel.MoodOf(this, item);
            bool faint = mood is CellMood.Lifted or CellMood.Missed;
            Colour colour = Ui.RarityColour(item.Rarity);
            if (IsHovered && !faint) draw.AddRect(Bounds, GuiColors.Rgba(255, 240, 200, 18), 2f);

            GuiRect icon = new GuiRect(Bounds.X + Pad, Bounds.Y + Pad, Icon, Icon);
            draw.AddRect(icon, colour.MultiplyAlpha(0.1f), 2f);
            Ui.DrawItem(draw, item, icon, colour, faint ? GuiColors.Rgba(255, 255, 255, 46) : GuiColors.White);
            draw.AddRect(icon, GuiColors.Transparent, 2f, 1.5f, colour.MultiplyAlpha(faint ? 0.3f : 0.8f));

            float alpha = faint ? 0.45f : 1f;
            float x = icon.Right + TextGap;
            float width = Bounds.Right - Pad * 2f - x;
            float y = Bounds.Y + Pad;
            Ui.Text(Context, draw, _entry.Name, NameSize, new GuiRect(x, y, width, NameSize + 4f), colour.MultiplyAlpha(alpha));
            Ui.Text(Context, draw, $"Found from level {_entry.Level}", SmallSize, new GuiRect(x, y, width, NameSize + 4f), Ui.Faded.MultiplyAlpha(alpha), Alignment.End);
            y += NameSize + 4f;

            string kind = $"{Cards.KindOf(item)} · {(item.IsSticker ? _entry.Kind : item.Base)}";
            if (_entry.HeroOnly) kind += " · never worn by an enemy";
            Ui.Text(Context, draw, kind, SmallSize, new GuiRect(x, y, width, LineStep), Ui.Faded.MultiplyAlpha(alpha));
            y += LineStep;

            foreach ((string text, bool cost) in _lines)
            {
                Ui.Text(Context, draw, text, LineSize, new GuiRect(x, y, width, LineStep), (cost ? Ui.Blood : colour).MultiplyAlpha(alpha));
                y += LineStep;
            }
        }
    }
}

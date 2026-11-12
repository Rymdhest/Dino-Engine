using Dino_Defenders.Client;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Engine.GUI;
using Dino_Engine.GUI.Widgets;
using Dino_Engine.Sound;
using Dino_Engine.Sound.Content;

namespace Dino_Defenders.UI
{
    /// <summary>
    /// The Forge's crafting bench, beside the pack where the sheet would be. For now it does one thing, fusing (<see cref="Crafting"/>): two Magic things of the same sort go on it, and the first keeps
    /// its base and takes the second's lines, as a Rare, for gold. Nothing is moved onto the bench: each of its places only says which cell of the pack it's showing, and that thing stays where it is,
    /// faint, until it's fused or taken off, so closing the panel can't lose anything. The third place is what fusing would make, to be looked at before it's paid for.
    /// </summary>
    internal sealed class CraftingBench : StackPanel
    {
        public const int First = 0;
        public const int Second = 1;
        public const int Made = 2;

        private const float Cell = 68f;

        private readonly int?[] _cells = new int?[2];
        private Item? _madeFrom, _madeWith, _made;     // What fusing what's on the bench would make, worked out once for each pair.
        private string _lastMade = "";

        private static HeroState Hero => GameSession.Current.Hero;

        public CraftingBench(HeroPanel panel)
            : base(Orientation.Vertical, 6f)
        {
            Width = UIValue.Fill;
            Height = UIValue.Fill;
            Visible = false;
            MouseDown += e => panel.PressOn(this, e);

            Add(Ui.Line("Fuse", 15f, Ui.Gold));
            Add(Ui.Paragraph("Two Magic things of the same sort become one Rare. The first keeps its base and its lines, and takes the second's, which is used up.", 13.5f, Ui.Faded));
            Add(new Spacer(height: 4f));

            var row = Add(new StackPanel(Orientation.Horizontal, 6f) { Width = UIValue.Fill, Height = Cell });
            row.Stack!.CrossAlignment = Alignment.Center;
            row.Add(new ItemCell(panel, new Spot(Shelf.Bench, First), Cell));
            Sign(row, "+");
            row.Add(new ItemCell(panel, new Spot(Shelf.Bench, Second), Cell));
            Sign(row, "=");
            row.Add(new ItemCell(panel, new Spot(Shelf.Bench, Made), Cell));

            var captions = Add(new StackPanel(Orientation.Horizontal, 6f) { Width = UIValue.Fill, Height = 18f });
            Caption(captions, "Base");
            captions.Add(new Spacer(width: 18f));
            Caption(captions, "Lines");
            captions.Add(new Spacer(width: 18f));
            Caption(captions, "Makes");

            Add(new Spacer(height: 4f));
            Add(Ui.Paragraph("", 14f, Ui.Parchment).Bind(Says));
            Add(Ui.Paragraph("", 13.5f, Ui.Blood).Bind(() => WhyNot() ?? ""));
            Button fuse = Add(Ui.Action("Fuse", Fuse));
            fuse.Width = 160f;
            fuse.Height = 34f;
            fuse.Label.Bind(() => At(First) is Item first ? $"Fuse  ·  {Crafting.FuseCost(first):N0} gold" : "Fuse");

            Add(new Spacer(height: 8f));
            Add(Ui.Line("", 13f, Ui.Faded).Bind(() => $"{Bindings.Label(GameAction.Details)}-click something in your pack to put it on the bench"));
            Add(Ui.Line("Or drag it to a place here. Click one to take it off", 13f, Ui.Faded));

            // A cell that's emptied, by selling or dropping, is nothing on the bench either. Decided here, as this stays visible while the bench is open.
            Updated += _ =>
            {
                if (GameSession.Current == null) return;
                for (int i = 0; i < _cells.Length; i++)
                {
                    if (_cells[i] is int cell && Hero.Inventory[cell] == null) _cells[i] = null;
                }
                fuse.Enabled = WhyNot() == null && _cells[First] != null && _cells[Second] != null;
            };
        }

        private static void Sign(StackPanel row, string sign)
        {
            Label label = row.Add(Ui.Line(sign, 20f, Ui.Gold));
            label.Width = 18f;
            label.HorizontalTextAlignment = Alignment.Center;
        }

        private static void Caption(StackPanel row, string text)
        {
            Label label = row.Add(Ui.Line(text, 12.5f, Ui.Faded));
            label.Width = Cell;
            label.HorizontalTextAlignment = Alignment.Center;
        }

        /// <summary>Takes everything off the bench, for when the panel opens again or the pack's been sorted under it.</summary>
        public void Clear()
        {
            _cells[First] = null;
            _cells[Second] = null;
            _lastMade = "";
        }

        /// <summary>What's in one of the bench's places: what's on it from the pack, or what fusing them would make.</summary>
        public Item? At(int index)
        {
            if (GameSession.Current == null) return null;
            if (index is First or Second) return _cells[index] is int cell ? Hero.Inventory[cell] : null;
            if (index != Made || At(First) is not Item first || At(Second) is not Item second || Crafting.WhyNot(first, second) != null) return null;
            if (!ReferenceEquals(first, _madeFrom) || !ReferenceEquals(second, _madeWith))
            {
                _madeFrom = first;
                _madeWith = second;
                _made = Crafting.Fused(first, second);
            }
            return _made;
        }

        /// <summary>Whether this cell of the pack is on the bench.</summary>
        public bool Holds(int cell) => _cells[First] == cell || _cells[Second] == cell;

        /// <summary>Whether something could go on the bench with what's on it already, for dimming what couldn't.</summary>
        public bool Takes(Item item)
        {
            if (Crafting.WhyNotFusing(item) != null) return false;
            Item? other = At(First) ?? At(Second);
            return other == null || ReferenceEquals(other, item) || Crafting.WhyNot(other, item) == null || Crafting.WhyNot(item, other) == null;
        }

        /// <summary>Why something from this cell of the pack can't go in this place on the bench, or nothing if it can.</summary>
        public string? WhyNotPut(int cell, int index)
        {
            if (index is not (First or Second)) return "Put it in the first or second place";
            if (Hero.Inventory[cell] is not Item item) return "";
            if (Crafting.WhyNotFusing(item) is string why) return why;
            int otherIndex = index == First ? Second : First;
            if (_cells[otherIndex] is int otherCell && otherCell != cell && Hero.Inventory[otherCell] is Item other)
            {
                string? refused = index == First ? Crafting.WhyNot(item, other) : Crafting.WhyNot(other, item);
                if (refused != null) return refused;
            }
            return null;
        }

        /// <summary>Puts what's in a cell of the pack in a place on the bench. If it was in the other place already, the two change places.</summary>
        public void Put(int cell, int index)
        {
            int otherIndex = index == First ? Second : First;
            if (_cells[otherIndex] == cell) _cells[otherIndex] = _cells[index];
            _cells[index] = cell;
            _lastMade = "";
            Audio.Play(Interface.Click);
        }

        /// <summary>The first free place on the bench, or nothing if both are taken.</summary>
        public int? FreePlace => _cells[First] == null ? First : _cells[Second] == null ? Second : null;

        public void TakeOff(int index)
        {
            if (index is First or Second) _cells[index] = null;
            Audio.Play(Interface.Click);
        }

        private string? WhyNot()
        {
            if (_cells[First] is not int first || _cells[Second] is not int second) return null;
            return Crafting.WhyNot(Hero, first, second);
        }

        private string Says()
        {
            if (_lastMade.Length > 0) return $"Made {_lastMade}.";
            if (At(Made) is Item made) return $"Makes {made.Name}.";
            if (_cells[First] == null) return "Put the one whose base you'd keep in the first place.";
            return _cells[Second] == null ? "Put the one whose lines it takes in the second place." : "";
        }

        private void Fuse()
        {
            if (_cells[First] is not int first || _cells[Second] is not int second) return;
            if (Crafting.WhyNot(Hero, first, second) is string why)
            {
                Audio.Play(Interface.Refuse);
                GameSession.Current.Note($"{why}.");
                return;
            }
            string made = At(Made)?.Name ?? "";
            Audio.Play(Interface.Trade);
            GameSession.Current.Client.Fuse(first, second);
            Clear();
            _lastMade = made;
        }
    }
}

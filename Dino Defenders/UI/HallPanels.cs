using Dino_Defenders.Client;
using Dino_Defenders.Gameplay;
using Dino_Engine.GUI;
using Dino_Engine.GUI.Widgets;
using Dino_Engine.World;
using Dino_Engine.World.Content;

namespace Dino_Defenders.UI
{
    /// <summary>
    /// What opens at what's used in the Haven's halls: the Forge's crafting bench and the Gaming House's table. Neither does anything yet; each says what it's for, and closes.
    /// When crafting and the games are built, they're opened from here (HALLS.md).
    /// </summary>
    internal sealed class HallPanel : GuiElement
    {
        private readonly StackPanel _body;
        private readonly Action _close;

        public HallPanel(Action close)
        {
            _close = close;
            Anchor = Anchor.Center;
            Width = 560f;
            Height = UIValue.Auto;
            Padding = new Thickness(26f);
            Ui.Dress(this);
            Visible = false;
            _body = Add(new StackPanel(Orientation.Vertical, 10f) { Width = UIValue.Fill, Height = UIValue.Auto });
        }

        public void Open(InteractKind kind)
        {
            _body.ClearChildren();
            (string name, HallKind hall, string what) = kind switch
            {
                InteractKind.Workbench => ("Crafting Bench", HallKind.Forge, "The smith's bench, where gear will be reworked into something better. The bench is ready and the fire's in; what can be done at it is still being decided."),
                _ => ("Gaming Table", HallKind.GamingHouse, "Where gold will be played for, against prizes worth the risk. The table's set and the dealer's waiting; the games themselves are still being decided."),
            };
            _body.Add(Ui.Heading(name, 26f));
            _body.Add(Ui.Line(HallDesigns.Of(hall).Name, 14f, Ui.Gold));
            _body.Add(Ui.Paragraph(what, 16f));
            _body.Add(new Spacer(height: 6f));
            Button close = _body.Add(Ui.Action("Close", _close));
            close.Width = 160f;
            close.Height = 36f;
        }
    }
}

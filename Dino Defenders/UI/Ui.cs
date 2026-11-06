using Dino_Defenders.Progression;
using Dino_Engine.Core;
using Dino_Engine.GUI;
using Dino_Engine.GUI.Rendering;
using Dino_Engine.GUI.Text;
using Dino_Engine.GUI.Widgets;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Defenders.UI
{
    /// <summary>The look of the game's interface: ink and parchment, a serif face, and gold kept for what matters.</summary>
    internal static class Ui
    {
        public static readonly Colour Ink = GuiColors.Rgba(15, 12, 10, 232);
        public static readonly Colour InkLight = GuiColors.Rgba(38, 31, 25, 235);
        public static readonly Colour Parchment = GuiColors.Rgba(228, 214, 186);
        public static readonly Colour Faded = GuiColors.Rgba(164, 150, 126);
        public static readonly Colour Gold = GuiColors.Rgba(214, 172, 92);
        public static readonly Colour Blood = GuiColors.Rgba(172, 40, 30);
        public static readonly Colour Life = GuiColors.Rgba(150, 34, 28);
        public static readonly Colour Experience = GuiColors.Rgba(196, 158, 80);
        public static readonly Colour Good = GuiColors.Rgba(126, 190, 118);

        private static FontFace? _face;

        public static FontFace Face => _face ??= SystemFonts.TryLoad("Palatino Linotype", "Book Antiqua", "Georgia", "Cambria", "Times New Roman") ?? Engine.Gui.Theme.Font;

        public static Colour GradeColour(LootGrade grade) => grade switch
        {
            LootGrade.Magic => GuiColors.Rgba(112, 150, 230),
            LootGrade.Rare => GuiColors.Rgba(232, 204, 92),
            LootGrade.Masterwork => GuiColors.Rgba(190, 120, 255),
            LootGrade.Legendary => GuiColors.Rgba(255, 138, 46),
            _ => Parchment,
        };

        /// <summary>What's behind a find's name on the ground: dark enough to read over anything, with enough of its colour in it to tell at a glance.</summary>
        public static Colour GradeFill(LootGrade grade) => grade switch
        {
            LootGrade.Magic => GuiColors.Rgba(10, 16, 36, 210),
            LootGrade.Rare => GuiColors.Rgba(38, 30, 8, 220),
            LootGrade.Masterwork => GuiColors.Rgba(36, 14, 58, 232),
            LootGrade.Legendary => GuiColors.Rgba(66, 26, 4, 238),
            _ => GuiColors.Rgba(12, 10, 8, 180),
        };

        public static string GradeName(Item item) => item.Unique != null ? "Unique" : Loot.GradeOf(item) switch
        {
            LootGrade.Plain => "Common",
            LootGrade.Magic => "Magic",
            LootGrade.Rare => "Rare",
            LootGrade.Masterwork => "Masterwork",
            _ => "Legendary",
        };

        /// <summary>What kind of thing something is, in a word or two.</summary>
        public static string SlotName(Item item) => item.Slot switch
        {
            ItemSlot.MainHand => item.IsTwoHanded ? $"Two-handed {item.Kind}" : item.Kind.ToString(),
            ItemSlot.OffHand => "Off hand",
            ItemSlot.Finger => "Ring",
            ItemSlot.Neck => "Amulet",
            _ => item.Slot.ToString(),
        };

        /// <summary>One line of text with a soft shadow, so it reads over anything the world puts behind it.</summary>
        public static void Text(GuiContext context, GuiDrawList draw, string text, float size, GuiRect rect, Colour colour,
            Alignment horizontal = Alignment.Start, Alignment vertical = Alignment.Center)
        {
            GuiRect shadow = new GuiRect(rect.X + 1f, rect.Y + 1f, rect.Width, rect.Height);
            GuiText.Draw(context, draw, text, Face, size, shadow, GuiColors.Rgba(0, 0, 0, (int)(190 * colour.Alpha)), horizontal, vertical);
            GuiText.Draw(context, draw, text, Face, size, rect, colour, horizontal, vertical);
        }

        /// <summary>Breaks text into lines no wider than <paramref name="width"/>.</summary>
        public static List<string> Wrap(GuiContext context, string text, float size, float width)
        {
            var lines = new List<string>();
            string line = "";
            foreach (string word in text.Split(' '))
            {
                string attempt = line.Length == 0 ? word : line + " " + word;
                if (GuiText.MeasureWidth(context, attempt, Face, size) <= width)
                {
                    line = attempt;
                    continue;
                }
                if (line.Length > 0) lines.Add(line);
                line = word;
            }
            if (line.Length > 0) lines.Add(line);
            return lines;
        }

        public static Label Heading(string text, float size = 22f)
        {
            return new Label(text) { Font = Face, FontSize = size, Color = Gold };
        }

        public static Label Line(string text = "", float size = 15f, Colour? colour = null)
        {
            return new Label(text) { Font = Face, FontSize = size, Color = colour ?? Parchment };
        }

        public static Label Paragraph(string text, float size = 15f, Colour? colour = null)
        {
            return new Label(text) { Font = Face, FontSize = size, Color = colour ?? Parchment, Wrap = true, Width = UIValue.Fill };
        }

        public static Button Action(string text, Action pressed)
        {
            var button = new Button(text, pressed)
            {
                NormalColor = InkLight,
                HoverColor = GuiColors.Rgba(70, 54, 36),
                PressedColor = GuiColors.Rgba(26, 20, 16),
                BorderColor = GuiColors.Rgba(120, 96, 58),
                BorderWidth = 1f,
                Width = UIValue.Fill,
                Height = 32f,
            };
            button.Label.Font = Face;
            button.Label.FontSize = 15f;
            button.Label.Color = Parchment;
            return button;
        }

        /// <summary>The frame all panels share.</summary>
        public static void Dress(GuiElement panel)
        {
            panel.BackgroundColor = Ink;
            panel.BorderColor = GuiColors.Rgba(120, 96, 58);
            panel.BorderWidth = 1f;
            panel.CornerRadius = 3f;
            panel.ShadowSize = 26f;
            panel.ShadowColor = GuiColors.Rgba(0, 0, 0, 170);
            panel.HitTest = HitTestMode.Block;
        }

        /// <summary>A small drawing of what goes in a slot, for items and the empty places where they go.</summary>
        public static void DrawSlotIcon(GuiDrawList draw, ItemSlot slot, GuiRect rect, Colour colour)
        {
            Vector2 c = rect.Center;
            float s = MathF.Min(rect.Width, rect.Height) * 0.3f;
            switch (slot)
            {
                case ItemSlot.MainHand:
                    draw.AddLine(c + new Vector2(-s, s), c + new Vector2(s, -s), colour, 2.4f);
                    draw.AddLine(c + new Vector2(-s * 0.85f, s * 0.2f), c + new Vector2(-s * 0.2f, s * 0.85f), colour, 2.4f);
                    break;
                case ItemSlot.OffHand:
                    draw.AddLine(c + new Vector2(-s, -s), c + new Vector2(s, -s), colour, 2.2f);
                    draw.AddLine(c + new Vector2(-s, -s), c + new Vector2(0f, s * 1.2f), colour, 2.2f);
                    draw.AddLine(c + new Vector2(s, -s), c + new Vector2(0f, s * 1.2f), colour, 2.2f);
                    break;
                case ItemSlot.Head:
                    draw.AddCircle(c + new Vector2(0f, -s * 0.1f), s * 0.8f, GuiColors.Transparent, 2f, colour);
                    draw.AddLine(c + new Vector2(-s, s * 0.45f), c + new Vector2(s, s * 0.45f), colour, 2.2f);
                    break;
                case ItemSlot.Body:
                    draw.AddLine(c + new Vector2(-s, -s), c + new Vector2(s, -s), colour, 2.2f);
                    draw.AddLine(c + new Vector2(-s * 0.7f, -s), c + new Vector2(-s * 0.7f, s), colour, 2.2f);
                    draw.AddLine(c + new Vector2(s * 0.7f, -s), c + new Vector2(s * 0.7f, s), colour, 2.2f);
                    draw.AddLine(c + new Vector2(-s * 0.7f, s), c + new Vector2(s * 0.7f, s), colour, 2.2f);
                    break;
                case ItemSlot.Legs:
                    draw.AddLine(c + new Vector2(-s * 0.7f, -s), c + new Vector2(s * 0.7f, -s), colour, 2.2f);
                    draw.AddLine(c + new Vector2(-s * 0.7f, -s), c + new Vector2(-s * 0.6f, s), colour, 2.2f);
                    draw.AddLine(c + new Vector2(s * 0.7f, -s), c + new Vector2(s * 0.6f, s), colour, 2.2f);
                    draw.AddLine(c + new Vector2(0f, -s * 0.3f), c + new Vector2(-s * 0.15f, s), colour, 2.2f);
                    draw.AddLine(c + new Vector2(0f, -s * 0.3f), c + new Vector2(s * 0.15f, s), colour, 2.2f);
                    break;
                case ItemSlot.Hands:
                    draw.AddLine(c + new Vector2(-s * 0.6f, s), c + new Vector2(s * 0.6f, s), colour, 2.4f);
                    for (int finger = -1; finger <= 2; finger++) draw.AddLine(c + new Vector2(finger * s * 0.32f - s * 0.16f, s), c + new Vector2(finger * s * 0.32f - s * 0.16f, -s * 0.7f), colour, 2f);
                    break;
                case ItemSlot.Feet:
                    draw.AddLine(c + new Vector2(-s * 0.3f, -s), c + new Vector2(-s * 0.3f, s * 0.7f), colour, 2.6f);
                    draw.AddLine(c + new Vector2(-s * 0.3f, s * 0.7f), c + new Vector2(s, s * 0.7f), colour, 2.6f);
                    break;
                case ItemSlot.Finger:
                    draw.AddCircle(c, s * 0.75f, GuiColors.Transparent, 2.4f, colour);
                    break;
                case ItemSlot.Neck:
                    draw.AddLine(c + new Vector2(-s, -s), c + new Vector2(0f, s * 0.3f), colour, 1.6f);
                    draw.AddLine(c + new Vector2(s, -s), c + new Vector2(0f, s * 0.3f), colour, 1.6f);
                    draw.AddCircle(c + new Vector2(0f, s * 0.6f), s * 0.34f, colour);
                    break;
            }
        }
    }
}

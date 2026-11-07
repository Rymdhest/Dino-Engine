using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Tooltips;
using Dino_Engine.Core;
using Dino_Engine.GUI;
using Dino_Engine.GUI.Rendering;
using Dino_Engine.GUI.Text;
using Dino_Engine.GUI.Widgets;
using Dino_Engine.Rendering.Renderers.Studio;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Defenders.UI
{
    /// <summary>The look of the game's interface: ink and parchment, a serif face, and gold kept for what matters.</summary>
    internal static class Ui
    {
        public static readonly Colour Ink = GuiColors.Rgba(15, 12, 10, 232);
        public static readonly Colour InkLight = GuiColors.Rgba(38, 31, 25, 235);
        public static readonly Colour Paper = GuiColors.Rgba(15, 12, 10, 246);
        public static readonly Colour Parchment = GuiColors.Rgba(228, 214, 186);
        public static readonly Colour Bright = GuiColors.Rgba(255, 248, 232);
        public static readonly Colour TermInk = GuiColors.Rgba(238, 196, 120);
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

        /// <summary>One line of text with a soft shadow, so it reads over anything the world puts behind it.</summary>
        public static void Text(GuiContext context, GuiDrawList draw, string text, float size, GuiRect rect, Colour colour,
            Alignment horizontal = Alignment.Start, Alignment vertical = Alignment.Center)
        {
            GuiRect shadow = new GuiRect(rect.X + 1f, rect.Y + 1f, rect.Width, rect.Height);
            GuiText.Draw(context, draw, text, Face, size, shadow, GuiColors.Rgba(0, 0, 0, (int)(190 * colour.Alpha)), horizontal, vertical);
            GuiText.Draw(context, draw, text, Face, size, rect, colour, horizontal, vertical);
        }

        private static readonly List<TermMark> Marks = new();
        private static readonly List<(int start, int length, Term? term, Card? card)> Hot = new();

        /// <summary>One line of a tooltip, with each word that means something marked and each number standing out.</summary>
        /// <param name="links">Cards whose names in this line open them.</param>
        /// <param name="spots">Gets where each marked word ended up, so that it can be rested on.</param>
        public static void Rich(GuiContext context, GuiDrawList draw, string text, float size, float x, float y, Colour colour, IReadOnlyList<Card> links, List<HotSpot> spots)
        {
            Lexicon.Find(text, Marks);
            Hot.Clear();
            foreach (TermMark mark in Marks)
            {
                if (!mark.Term.Basic && mark.Length > 1) Hot.Add((mark.Start, mark.Length, mark.Term, null));
            }

            foreach (Card card in links)
            {
                int found = text.IndexOf(card.Title, StringComparison.Ordinal);
                if (found < 0) continue;
                int end = found + card.Title.Length;
                Hot.RemoveAll(hot => hot.start < end && hot.start + hot.length > found);
                Hot.Add((found, card.Title.Length, null, card));
            }
            Hot.Sort((a, b) => a.start.CompareTo(b.start));

            int at = 0;
            foreach ((int start, int length, Term? term, Card? card) in Hot)
            {
                x = Numbered(context, draw, text, at, start, size, x, y, colour);
                float from = x;
                x = Piece(context, draw, text.Substring(start, length), size, x, y, TermInk);
                spots.Add(new HotSpot(new GuiRect(from, y, x - from, size + 5f), term, card));
                at = start + length;
            }
            Numbered(context, draw, text, at, text.Length, size, x, y, colour);
        }

        private static float Numbered(GuiContext context, GuiDrawList draw, string text, int start, int end, float size, float x, float y, Colour colour)
        {
            int at = start;
            for (int i = start; i < end; i++)
            {
                if (!char.IsDigit(text[i])) continue;

                int first = i > start && text[i - 1] is '+' or '-' ? i - 1 : i;
                int last = i;
                while (last + 1 < end && (char.IsDigit(text[last + 1]) || text[last + 1] is '.' or '%' or '\u2013')) last++;
                x = Piece(context, draw, text.Substring(at, first - at), size, x, y, colour);
                x = Piece(context, draw, text.Substring(first, last - first + 1), size, x, y, Bright);
                at = last + 1;
                i = last;
            }
            return Piece(context, draw, text.Substring(at, end - at), size, x, y, colour);
        }

        private static float Piece(GuiContext context, GuiDrawList draw, string text, float size, float x, float y, Colour colour)
        {
            if (text.Length == 0) return x;
            float width = GuiText.MeasureWidth(context, text, Face, size);
            GuiText.Draw(context, draw, text, Face, size, new GuiRect(x, y, width + 2f, size + 5f), colour, Alignment.Start, Alignment.Start);
            return x + width;
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

        /// <summary>The picture of something, or a drawing of what kind of thing it is until there is one.</summary>
        public static void DrawItem(GuiDrawList draw, Item item, GuiRect rect, Colour colour, Colour tint)
        {
            if (GameUi.Current.Wardrobe.Picture(item, Halo(item)) is StudioPicture picture) draw.AddImage(picture.Texture, rect, picture.From, picture.To, tint);
            else DrawSlotIcon(draw, item.Slot, rect, colour);
        }

        // What's behind a picture of something, for what's worth noticing. It's part of the picture, so it has to go by what's in the picture's key.
        private static Colour Halo(Item item)
        {
            LootGrade grade = Loot.GradeOf(item);
            if (grade < LootGrade.Rare) return default;
            Colour glow = GradeColour(grade);
            return new Colour(glow.Red, glow.Green, glow.Blue, 1f, 0.55f);
        }

        /// <summary>What something is, beside whatever's showing it, set against what's worn in its place. Everything that shows an item shows it the same way.</summary>
        /// <param name="beside">What it's showing beside.</param>
        /// <param name="price">What it's worth here, if anything.</param>
        /// <param name="compare">Whether to set it against what it would replace, in a line or two, and in full beside it while Alt is held.</param>
        /// <param name="loose">Whether it's in a pack, where Ctrl is for clicking: see <see cref="TipLayer"/>.</param>
        public static void DrawTooltip(HeroState hero, Item item, GuiRect beside, string? price, bool compare, bool loose = false)
        {
            GameUi.Current.Tips.Show(() =>
            {
                Card card = Cards.Item(item, GameSession.Current?.Sheet, Cards.KindOf(item));
                if (price != null)
                {
                    card.Gap();
                    card.Text(price, LineLook.Price);
                }

                if (compare && hero.WornBeside(item) is Item worn && Differences(item, worn).Any())
                {
                    card.Heading($"Compared to {worn.Name}");
                    foreach ((string name, float difference) in Differences(item, worn)) card.Text($"{Say.Signed(difference)} {name}", difference > 0f ? LineLook.Good : LineLook.Bad);
                }
                return card;
            }, beside, loose: loose, alongside: compare && !item.IsBook ? () => WornCards(hero, item) : null);
        }

        // What's on the hero in its place, each as a card of its own, for setting side by side.
        private static List<Card> WornCards(HeroState hero, Item item)
        {
            var cards = new List<Card>();
            foreach (Item worn in hero.WornInPlaceOf(item)) cards.Add(Cards.Item(worn, GameSession.Current?.Sheet, $"Equipped: {Cards.KindOf(worn)}"));
            if (cards.Count == 0) cards.Add(new Card { Title = "Nothing equipped", Kind = "This place is empty" });
            return cards;
        }

        /// <summary>What's different about something from what it would replace, in what they're made of.</summary>
        public static IEnumerable<(string name, float difference)> Differences(Item item, Item worn)
        {
            float damage = item.WeaponDamage - worn.WeaponDamage;
            if (MathF.Abs(damage) >= 0.05f) yield return ("Damage", damage);
            float armour = item.Armour - worn.Armour;
            if (MathF.Abs(armour) >= 0.05f) yield return ("Armour", armour);
            float evasion = item.Evasion - worn.Evasion;
            if (MathF.Abs(evasion) >= 0.05f) yield return ("Evasion", evasion);
            float barrier = item.Barrier - worn.Barrier;
            if (MathF.Abs(barrier) >= 0.05f) yield return ("Barrier", barrier);
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

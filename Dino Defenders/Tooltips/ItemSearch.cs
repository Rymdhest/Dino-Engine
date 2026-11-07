using Dino_Defenders.Progression;
using System.Runtime.CompilerServices;
using System.Text;

namespace Dino_Defenders.Tooltips
{
    /// <summary>
    /// Finding things by what's written on them. What's searched is an item's own card, read by nobody in particular, so anything a player can read on one can be typed, in any order: "fire res"
    /// finds Fire Resistance, and "rare ring" finds just that. Words are found from their start, or "ring" lights up everything of the Spring. Each item is only read once, as a pack is
    /// asked about every frame while there's something in the box.
    /// </summary>
    public static class ItemSearch
    {
        private static readonly ConditionalWeakTable<Item, string> Read = new();

        /// <summary>What's been typed, as what's being looked for.</summary>
        public static string[] Words(string typed) => typed.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        /// <summary>Whether something has all of these on it somewhere. Asking for nothing finds everything.</summary>
        public static bool Finds(Item item, string[] words)
        {
            if (words.Length == 0) return true;
            string text = Read.GetValue(item, TextOf);
            foreach (string word in words)
            {
                if (!HasWordStarting(text, word)) return false;
            }
            return true;
        }

        private static bool HasWordStarting(string text, string word)
        {
            for (int at = text.IndexOf(word, StringComparison.OrdinalIgnoreCase); at >= 0; at = text.IndexOf(word, at + 1, StringComparison.OrdinalIgnoreCase))
            {
                if (at == 0 || !char.IsLetterOrDigit(text[at - 1])) return true;
            }
            return false;
        }

        private static string TextOf(Item item)
        {
            Card card = Cards.Item(item, null, Cards.KindOf(item));
            var text = new StringBuilder();
            text.Append(card.Title).Append('\n').Append(card.Kind).Append('\n').Append(item.Base).Append('\n').Append(item.Rarity).Append('\n');
            foreach (string word in card.Words()) text.Append(word).Append('\n');
            foreach (CardLine line in card.Lines) text.Append(line.Value).Append('\n');

            FamilyShares shares = item.Shares();
            foreach (GearFamily family in Enum.GetValues<GearFamily>())
            {
                if (shares.Of(family) >= 0.5f) text.Append(family).Append('\n');
            }
            return text.ToString();
        }
    }
}

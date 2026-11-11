using Dino_Defenders.Books;
using Dino_Defenders.Progression;
using Dino_Defenders.Stats;
using System.Text;

namespace Dino_Defenders.Tooltips
{
    /// <summary>What sort of thing a unique is, for the first of the lookup's filters.</summary>
    public enum LookupType
    {
        Any,
        Weapon,
        Armour,     // Anything worn that isn't a weapon or jewellery, shields and wards among it.
        Jewellery,
        Sentence,
        Orders,
    }

    /// <summary>How the lookup lists what it finds.</summary>
    public enum LookupOrder
    {
        Slot,       // As the catalogues keep them, which is by where they go.
        Name,
        Level,      // The least each is found at, then by name.
    }

    /// <summary>Which side of a unique a word has to be found on: anywhere, what it gives, or what it costs.</summary>
    public enum LookupSide
    {
        Anywhere,
        Gives,
        Costs,
    }

    /// <summary>One thing typed into the lookup: the word as typed, and the forms of it that count.</summary>
    public readonly record struct LookupWord(LookupSide Side, string[] Forms);

    /// <summary>One unique as the lookup knows it: what it's called, what sort of thing it is, what it gives and costs, and every word that finds it.</summary>
    public sealed class LookupEntry
    {
        public Item Item = null!;

        /// <summary>Where it is in what it was made from, which is how it's asked for.</summary>
        public int Index;
        public string Name = "";
        public LookupType Type;

        /// <summary>Where it goes, for the second filter when any type will do.</summary>
        public string Slot = "";

        /// <summary>What sort of its type it is, for the second filter once a type has been chosen.</summary>
        public string Kind = "";

        /// <summary>The least it's ever found at.</summary>
        public int Level;

        /// <summary>Whether enemies are never given it.</summary>
        public bool HeroOnly;

        public readonly List<string> Gives = new();
        public readonly List<string> Costs = new();

        // What the second filter's choices are put in order by.
        internal int SlotOrder;
        internal int KindOrder;

        internal string[] Everywhere = Array.Empty<string>();
        internal string[] OnGives = Array.Empty<string>();
        internal string[] OnCosts = Array.Empty<string>();
    }

    /// <summary>
    /// Every unique in the game laid out to be searched, for whoever has to keep track of them all. What finds one is everything a player can read on its card, what it sets off and hands out, its lore,
    /// what sort of thing it is, and the names its rules go by in the code, so "gold" finds everything that does something with gold and "carry" everything that cares what's carried. Words are found
    /// from their start, in any order, and every one has to be there. A word is also found in its other forms, so "carry" finds "carried", and a few that players type for what the game calls something
    /// else are taken as that. Typed after "gives:" or "costs:", a word has to be on that side of it. Nothing here is the game's: it only reads what's already there.
    /// </summary>
    public static class UniqueLookup
    {
        public const string GivesMark = "gives:";
        public const string CostsMark = "costs:";

        // What a player is likely to type, for what the game calls something else.
        private static readonly Dictionary<string, string[]> Aliases = new()
        {
            ["life"] = new[] { "health" },
            ["hp"] = new[] { "health" },
            ["inventory"] = new[] { "carried", "pack" },
            ["bag"] = new[] { "carried", "pack" },
            ["aoe"] = new[] { "area" },
            ["summon"] = new[] { "minion" },
            ["pet"] = new[] { "minion" },
            ["money"] = new[] { "gold" },
            ["coin"] = new[] { "gold" },
        };

        /// <summary>Every unique there is, as the beta vendor has them out: what's worn, what only a Unique sentence does, and unique orders.</summary>
        public static List<LookupEntry> Entries(IReadOnlyList<Item> uniques)
        {
            var entries = new List<LookupEntry>(uniques.Count);
            for (int i = 0; i < uniques.Count; i++) entries.Add(Entry(uniques[i], i));
            return entries;
        }

        public static string NameOf(LookupType type) => type switch
        {
            LookupType.Weapon => "Weapons",
            LookupType.Armour => "Armour",
            LookupType.Jewellery => "Jewellery",
            LookupType.Sentence => "Sentences",
            LookupType.Orders => "Orders",
            _ => "Any",
        };

        public static string NameOf(LookupOrder order) => order switch
        {
            LookupOrder.Name => "By name",
            LookupOrder.Level => "By level",
            _ => "By slot",
        };

        /// <summary>What the second filter goes by for an entry, once this type is chosen.</summary>
        public static string SecondOf(LookupEntry entry, LookupType type) => type == LookupType.Any ? entry.Slot : entry.Kind;

        /// <summary>Every choice the second filter has once this type is chosen, in the order a hero is dressed.</summary>
        public static List<string> Seconds(IEnumerable<LookupEntry> entries, LookupType type)
        {
            return entries.Where(entry => type == LookupType.Any || entry.Type == type)
                .GroupBy(entry => SecondOf(entry, type))
                .OrderBy(group => group.Min(entry => type == LookupType.Any ? entry.SlotOrder : entry.KindOrder))
                .Select(group => group.Key)
                .ToList();
        }

        /// <summary>Whether an entry is of this type, of this second choice if there is one, and has every word typed on it.</summary>
        public static bool Passes(LookupEntry entry, LookupType type, string? second, LookupWord[] words)
        {
            if (type != LookupType.Any && entry.Type != type) return false;
            if (second != null && SecondOf(entry, type) != second) return false;
            return Finds(entry, words);
        }

        public static IEnumerable<LookupEntry> Sorted(IEnumerable<LookupEntry> entries, LookupOrder order) => order switch
        {
            LookupOrder.Name => entries.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase),
            LookupOrder.Level => entries.OrderBy(entry => entry.Level).ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase),
            _ => entries.OrderBy(entry => entry.Index),
        };

        /// <summary>What's been typed, as what's being looked for.</summary>
        public static LookupWord[] Words(string typed)
        {
            var words = new List<LookupWord>();
            LookupSide pending = LookupSide.Anywhere;
            foreach (string chunk in typed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            {
                string rest = chunk;
                LookupSide side = pending;
                pending = LookupSide.Anywhere;
                if (rest.StartsWith(GivesMark, StringComparison.OrdinalIgnoreCase))
                {
                    side = LookupSide.Gives;
                    rest = rest[GivesMark.Length..];
                }
                else if (rest.StartsWith(CostsMark, StringComparison.OrdinalIgnoreCase))
                {
                    side = LookupSide.Costs;
                    rest = rest[CostsMark.Length..];
                }

                // On its own, a side is for the word after it.
                if (rest.Length == 0)
                {
                    pending = side;
                    continue;
                }
                foreach (string token in Tokens(rest))
                {
                    var forms = new List<string> { token };
                    string stem = Stem(token);
                    if (stem != token) forms.Add(stem);
                    if (Aliases.TryGetValue(token, out string[]? meant)) forms.AddRange(meant);
                    words.Add(new LookupWord(side, forms.ToArray()));
                }
            }
            return words.ToArray();
        }

        /// <summary>Whether every word is on it somewhere, on the side it was asked for. Asking for nothing finds everything.</summary>
        public static bool Finds(LookupEntry entry, LookupWord[] words)
        {
            foreach (LookupWord word in words)
            {
                string[] text = word.Side switch
                {
                    LookupSide.Gives => entry.OnGives,
                    LookupSide.Costs => entry.OnCosts,
                    _ => entry.Everywhere,
                };
                if (!Has(text, word.Forms)) return false;
            }
            return true;
        }

        private static bool Has(string[] text, string[] forms)
        {
            foreach (string known in text)
            {
                foreach (string form in forms)
                {
                    if (known.StartsWith(form, StringComparison.Ordinal)) return true;
                }
            }
            return false;
        }

        private static LookupEntry Entry(Item item, int index)
        {
            var entry = new LookupEntry { Item = item, Index = index, Name = item.Name, Level = 1 };
            Card card = Cards.Item(item, null, Cards.KindOf(item));
            var everywhere = new StringBuilder();
            var gives = new StringBuilder();
            var costs = new StringBuilder();

            if (item.Sentence is Sentence sentence)
            {
                entry.Type = LookupType.Sentence;
                PageKind? fits = Sentences.Fits(sentence);
                entry.Kind = fits is PageKind kind ? $"{kind} Page" : "Either Page";
                entry.KindOrder = fits is PageKind order ? (int)order + 1 : 0;
                entry.Gives.Add(BookText.Good(sentence));
                gives.Append(sentence.Good).Append('\n');
                if (sentence.Bad != PageEffect.None)
                {
                    entry.Costs.Add(BookText.Bad(sentence));
                    costs.Append(sentence.Bad).Append('\n');
                }
            }
            else if (item.IsOrders)
            {
                entry.Type = LookupType.Orders;
                entry.Kind = item.Base;
                entry.KindOrder = Array.FindIndex(OrdersMaker.Bases, basis => basis.Name == item.Base);
                if (OrdersCatalog.Find(item.Unique) is UniqueOrders orders)
                {
                    entry.Level = orders.MinLevel;
                    entry.Gives.Add(orders.Gives);
                    entry.Costs.Add(orders.Costs);
                    everywhere.Append(orders.Id).Append('\n');
                }
            }
            else
            {
                entry.Type = item.Slot == ItemSlot.MainHand ? LookupType.Weapon : item.Slot is ItemSlot.Finger or ItemSlot.Neck ? LookupType.Jewellery : LookupType.Armour;
                entry.Kind = item.Slot == ItemSlot.MainHand ? item.Kind.ToString() : SlotName(item.Slot);
                entry.KindOrder = (int)item.Slot * 16 + (item.Slot == ItemSlot.MainHand ? (int)item.Kind : 0);
                foreach (CardLine line in card.Lines)
                {
                    if (line.Look == LineLook.Rolled) entry.Gives.Add(line.Text);
                    if (line.Look == LineLook.Burden) entry.Costs.Add(line.Text);
                }
                if (UniqueCatalog.Find(item.Unique ?? "") is UniqueDefinition unique)
                {
                    entry.Level = unique.MinLevel;
                    entry.HeroOnly = unique.HeroOnly;
                    everywhere.Append(unique.Id).Append('\n');
                    foreach (Mod mod in unique.Boon) Identify(mod, gives);
                    foreach (Mod mod in unique.Burden) Identify(mod, costs);
                }
            }
            entry.Slot = SlotName(item.Slot);
            entry.SlotOrder = (int)item.Slot;

            foreach (string line in entry.Gives) gives.Append(line).Append('\n');
            foreach (string line in entry.Costs) costs.Append(line).Append('\n');

            // Everything on its card and behind it, its lore among it, which a card's words leave out.
            everywhere.Append(item.Name).Append('\n').Append(item.Base).Append('\n').Append(card.Title).Append('\n').Append(card.Kind).Append('\n');
            everywhere.Append(item.Rarity).Append('\n').Append(NameOf(entry.Type)).Append('\n').Append(entry.Slot).Append('\n').Append(entry.Kind).Append('\n');
            foreach (string word in card.Words()) everywhere.Append(word).Append('\n');
            foreach (CardLine line in card.Lines)
            {
                if (line.Look == LineLook.Lore) everywhere.Append(line.Text).Append('\n');
                if (line.Value.Length > 0) everywhere.Append(line.Value).Append('\n');
            }
            if (entry.HeroOnly) everywhere.Append("hero only").Append('\n');
            everywhere.Append(gives).Append(costs);

            entry.Everywhere = Forms(everywhere.ToString());
            entry.OnGives = Forms(gives.ToString());
            entry.OnCosts = Forms(costs.ToString());
            return entry;
        }

        private static string SlotName(ItemSlot slot) => slot switch
        {
            ItemSlot.MainHand => "Main Hand",
            ItemSlot.OffHand => "Off Hand",
            ItemSlot.Finger => "Ring",
            ItemSlot.Neck => "Amulet",
            _ => slot.ToString(),
        };

        // The names a mod's parts go by in the code, which are as good as keywords to someone looking after them. Only what it really has: a rule has no stat, whatever its stat says.
        private static void Identify(Mod mod, StringBuilder text)
        {
            switch (mod.Kind)
            {
                case ModKind.Rule:
                    text.Append(mod.Rule).Append('\n');
                    break;
                case ModKind.Grant:
                    text.Append(mod.Grants).Append('\n');
                    break;
                case ModKind.Trigger:
                    text.Append(mod.On).Append('\n');
                    break;
                case ModKind.Divert:
                case ModKind.Echo:
                    text.Append(mod.Stat).Append('\n').Append(mod.Into).Append('\n');
                    break;
                default:
                    text.Append(mod.Stat).Append('\n');
                    if (mod.Stat is Stat.Convert or Stat.GainAs) text.Append(mod.From).Append('\n').Append(mod.To).Append('\n');
                    break;
            }
            if (mod.Scope != Tags.None) text.Append(mod.Scope).Append('\n');
            if (mod.Need != Condition.None) text.Append(mod.Need).Append('\n');
            if (mod.Without != Condition.None) text.Append(mod.Without).Append('\n');
            if (mod.Foe != Condition.None) text.Append(mod.Foe).Append('\n');
            if (mod.Per != Per.None) text.Append(mod.Per).Append('\n');
        }

        // Every word, and every other form of it that should find it.
        private static string[] Forms(string text)
        {
            var forms = new HashSet<string>(StringComparer.Ordinal);
            foreach (string token in Tokens(text))
            {
                forms.Add(token);
                forms.Add(Stem(token));

                // What's going on is found by what it is: "moving" by "move", and "stunning" by "stun".
                if (token.Length > 5 && token.EndsWith("ing", StringComparison.Ordinal))
                {
                    string root = Undouble(token[..^3]);
                    forms.Add(root);
                    forms.Add(root + "e");
                }
            }
            return forms.ToArray();
        }

        // Words in lower case, split where a name in the code changes case or runs into a number, and kept whole across an apostrophe.
        private static IEnumerable<string> Tokens(string text)
        {
            var token = new StringBuilder();
            char last = ' ';
            foreach (char c in text)
            {
                bool part = char.IsLetterOrDigit(c) || (c == '\'' && token.Length > 0);
                bool split = token.Length > 0 && part && ((char.IsUpper(c) && char.IsLower(last)) || (char.IsDigit(c) != char.IsDigit(last) && char.IsLetterOrDigit(last)));
                if ((!part || split) && token.Length > 0)
                {
                    yield return token.ToString().TrimEnd('\'');
                    token.Clear();
                }
                if (part) token.Append(char.ToLowerInvariant(c));
                last = c;
            }
            if (token.Length > 0) yield return token.ToString().TrimEnd('\'');
        }

        // A word with its endings taken off, carefully: it's only ever compared from its start, so taking off too little costs nothing and too much finds the wrong things.
        internal static string Stem(string word)
        {
            if (word.EndsWith("'s", StringComparison.Ordinal)) return word[..^2];
            if (word.Length > 4 && (word.EndsWith("ies", StringComparison.Ordinal) || word.EndsWith("ied", StringComparison.Ordinal))) return word[..^3] + "y";
            if (word.Length > 4 && word.EndsWith("ed", StringComparison.Ordinal)) return Undouble(word[..^2]);
            if (word.Length > 3 && word.EndsWith('s') && !word.EndsWith("ss", StringComparison.Ordinal)) return word[..^1];
            return word;
        }

        // "stunn" is "stun", and "dropp" is "drop", but "kill" stays as it is.
        private static string Undouble(string root)
        {
            if (root.Length < 3 || root[^1] != root[^2]) return root;
            return "bdgmnprt".Contains(root[^1]) ? root[..^1] : root;
        }
    }
}

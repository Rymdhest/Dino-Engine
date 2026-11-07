using Dino_Defenders.Books;

namespace Dino_Defenders.Stats
{
    /// <summary>What a weapon brings to an attack. Someone with nothing in their hands has one of these as well.</summary>
    public readonly record struct WeaponProfile(float Min, float Max, float AttackTime, float CritChance, Tags Tags)
    {
        public const float BareTime = 0.6f;

        public static readonly WeaponProfile Fists = new WeaponProfile(2f, 4f, BareTime, 5f, Tags.Unarmed);
    }

    /// <summary>
    /// Everything that makes one actor what it is: the modifiers from all it has and wears, in the order a sheet wants them. Heroes get one
    /// from their tree and gear, enemies from what they are. Nothing tells the two apart from here on, and enemies of one kind and level
    /// share one, which is why a crowd costs so little.
    /// </summary>
    public sealed class Loadout
    {
        private const int MostSheets = 24;

        private readonly int[] _first = new int[(int)Stat.Count + 1];
        private readonly Dictionary<(Condition, Counters), ActorSheet> _sheets = new();

        public readonly int Level;
        public readonly WeaponProfile Weapon;

        /// <summary>Sorted by stat. Anything that isn't a number on a stat is in a list of its own below.</summary>
        public readonly Mod[] Mods;

        public readonly Mod[] RuleMods;
        public readonly Mod[] Triggers;

        /// <summary>What it can use. Slot numbers and profiles refer to places in this.</summary>
        public readonly BookPlan[] Books;

        /// <summary>Conditions that are always true of this actor.</summary>
        public readonly Condition Granted;

        /// <summary>The conditions that change a sheet: everything else can come and go without any recalculation.</summary>
        public readonly Condition Relevant;

        /// <summary>Whether a counter changing means a new sheet.</summary>
        public readonly bool ReadsCounters;

        public Loadout(int level, WeaponProfile weapon, IEnumerable<Mod> mods, IEnumerable<BookPlan> books)
        {
            Level = level;
            Weapon = weapon;

            var numbers = new List<Mod>();
            var rules = new List<Mod>();
            var triggers = new List<Mod>();
            var diverted = new List<Mod>();
            var known = new List<BookPlan>(books);
            foreach (Mod mod in mods)
            {
                switch (mod.Kind)
                {
                    case ModKind.Rule:
                        rules.Add(mod);
                        break;
                    case ModKind.Grant:
                        Granted |= mod.Grants;
                        break;
                    case ModKind.Trigger:
                        triggers.Add(mod);
                        break;
                    case ModKind.Divert:
                    case ModKind.Echo:
                        diverted.Add(mod);
                        break;
                    default:
                        numbers.Add(mod);
                        break;
                }
            }

            // Before anything is diverted, as what an attribute gives is as much a part of a stat as anything else.
            if (numbers.Exists(mod => Attributes.Is(mod.Stat)) && !numbers.Contains(Attributes.Mods[0])) numbers.AddRange(Attributes.Mods);

            ApplyDiversions(numbers, diverted);

            // What goes off by itself has to be somewhere for a profile to be made of it, though nobody picks it from a list.
            foreach (Mod trigger in triggers)
            {
                BookPlan book = Shelf.Get(trigger.Book);
                if (!known.Contains(book)) known.Add(book);
            }

            foreach (Mod rule in rules)
            {
                if ((rule.Rule & Rule.BurningTrail) != 0 && !known.Contains(Shelf.CinderTrail)) known.Add(Shelf.CinderTrail);
                if ((rule.Rule & Rule.WaterTrail) != 0 && !known.Contains(Shelf.Puddle)) known.Add(Shelf.Puddle);
                if ((rule.Rule & Rule.Carrier) != 0 && !known.Contains(Shelf.VenomCloud)) known.Add(Shelf.VenomCloud);
            }

            Books = known.ToArray();
            RuleMods = rules.ToArray();
            Triggers = triggers.ToArray();
            Mods = SortByStat(numbers, _first);

            Condition relevant = Condition.None;
            bool readsCounters = false;
            void Note(in Mod mod)
            {
                relevant |= mod.Need | mod.Without;
                readsCounters |= mod.Per >= Per.Gold;
            }

            foreach (Mod mod in Mods) Note(mod);
            foreach (Mod mod in RuleMods) Note(mod);
            foreach (BookPlan book in Books)
            {
                foreach (PagePlan page in book.Pages)
                {
                    foreach (Mod mod in page.Mods) Note(mod);
                }
            }
            Relevant = relevant;
            ReadsCounters = readsCounters;
        }

        // A stat's increases can be sent somewhere else. Only what was written on the stat itself goes: what's been moved doesn't move again, so
        // two of these pointing at each other swap rather than loop.
        private static void ApplyDiversions(List<Mod> numbers, List<Mod> diverted)
        {
            if (diverted.Count == 0) return;

            int original = numbers.Count;
            var moved = new bool[original];
            foreach (Mod link in diverted)
            {
                for (int i = 0; i < original; i++)
                {
                    Mod mod = numbers[i];
                    if (mod.Stat != link.Stat || mod.Kind is not (ModKind.Increased or ModKind.More)) continue;
                    numbers.Add(mod with { Stat = link.Into, Value = mod.Value * link.Value / 100f, Scope = mod.Scope | link.Scope });
                    if (link.Kind == ModKind.Divert) moved[i] = true;
                }
            }

            for (int i = original - 1; i >= 0; i--)
            {
                if (moved[i]) numbers.RemoveAt(i);
            }
        }

        // A counting sort keeps equal stats in the order they came, so that the same things always add up in the same order, on every machine.
        private static Mod[] SortByStat(List<Mod> mods, int[] first)
        {
            foreach (Mod mod in mods) first[(int)mod.Stat + 1]++;
            for (int i = 1; i < first.Length; i++) first[i] += first[i - 1];

            var sorted = new Mod[mods.Count];
            var next = new int[first.Length];
            Array.Copy(first, next, first.Length);
            foreach (Mod mod in mods) sorted[next[(int)mod.Stat]++] = mod;
            return sorted;
        }

        /// <summary>The modifiers on one stat.</summary>
        public ReadOnlySpan<Mod> On(Stat stat) => Mods.AsSpan(_first[(int)stat], _first[(int)stat + 1] - _first[(int)stat]);

        /// <summary>
        /// What this actor adds up to, given what's true of it now. Asking twice costs nothing. Most actors have nothing that depends on their
        /// state, so they have one sheet, however much changes around them.
        /// </summary>
        public ActorSheet SheetFor(Condition state, in Counters counters = default)
        {
            Condition key = (state | Granted) & Relevant;
            Counters read = ReadsCounters ? counters : default;
            if (_sheets.TryGetValue((key, read), out ActorSheet? sheet)) return sheet;

            // Counters can run through a lot of values. What's been outgrown isn't worth keeping.
            if (_sheets.Count >= MostSheets) _sheets.Clear();

            sheet = new ActorSheet(this, key, read);
            _sheets.Add((key, read), sheet);
            return sheet;
        }

        public int IndexOf(BookPlan book) => Array.IndexOf(Books, book);
    }
}

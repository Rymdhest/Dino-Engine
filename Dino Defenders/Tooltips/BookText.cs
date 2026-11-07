using Dino_Defenders.Books;
using static Dino_Defenders.Tooltips.Say;

namespace Dino_Defenders.Tooltips
{
    /// <summary>Which part of a page something is, for whoever wants to change it: a blank, and if there are several of those, which.</summary>
    public readonly record struct PagePlace(Blank Blank, int Outcome = -1, int Clause = -1, bool Towards = false);

    /// <summary>A stretch of a page as it reads. Something that was chosen says where it is, and what only joins those up doesn't.</summary>
    public readonly record struct PagePart(string Text, PagePlace? Place = null);

    /// <summary>
    /// How a page reads, which is only ever its sentences one after another. A card reads this as a sentence and a book's own page as parts that can each be changed, so what's shown in the one is what's chosen in
    /// the other, and neither has any words of its own for what a page does.
    /// </summary>
    public static class BookText
    {
        private static PagePart Chosen(string? id, PagePlace place) => new PagePart(Sentences.Find(id)!.Words, place);

        /// <summary>A page, a line at a time: where it starts, where it goes, what's been added to that, and each Outcome.</summary>
        public static List<List<PagePart>> Parts(Page page, int index)
        {
            var lines = new List<List<PagePart>>();

            var start = new List<PagePart>();
            start.Add(index == 0 ? Chosen(page.From, new PagePlace(Blank.From)) : new PagePart("from where it struck"));
            if (page.FromWithin != null) start.Add(Chosen(page.FromWithin, new PagePlace(Blank.Within)));
            lines.Add(start);

            var then = new List<PagePart>();
            if (page.Kind == PageKind.Projectile)
            {
                then.Add(Chosen(page.Towards, new PagePlace(Blank.Towards)));
                if (page.TowardsWithin != null) then.Add(Chosen(page.TowardsWithin, new PagePlace(Blank.Within, Towards: true)));
            }
            else
            {
                then.Add(Chosen(page.Shape, new PagePlace(Blank.Shape)));
                then.Add(Chosen(page.Timing, new PagePlace(Blank.Timing)));
            }
            lines.Add(then);

            if (page.Clauses.Count > 0)
            {
                var clauses = new List<PagePart>();
                for (int i = 0; i < page.Clauses.Count; i++) clauses.Add(Chosen(page.Clauses[i], new PagePlace(Blank.Clause, Clause: i)));
                lines.Add(clauses);
            }

            for (int i = 0; i < page.Outcomes.Count; i++) lines.Add(OutcomeParts(page.Outcomes[i], i));
            return lines;
        }

        private static List<PagePart> OutcomeParts(Outcome outcome, int index)
        {
            PagePart Word(Blank blank) => new PagePart(Scribe.Word(outcome, blank).Words, new PagePlace(blank, index));

            var parts = new List<PagePart>();
            Sentence deed = Sentences.Find(outcome.Deed)!;
            parts.Add(new PagePart(deed.Words, new PagePlace(Blank.Deed, index)));
            switch (deed.Deed)
            {
                case Deed.Deal:
                    parts.Add(Word(Blank.Element));
                    parts.Add(new PagePart("Damage equal to"));
                    parts.Add(Word(Blank.Measure));
                    break;
                case Deed.Heal:
                    parts.Add(Word(Blank.Pool));
                    parts.Add(new PagePart("to"));
                    parts.Add(Word(Blank.Whom));
                    parts.Add(new PagePart("equal to"));
                    parts.Add(Word(Blank.Measure));
                    break;
                case Deed.Afflict:
                    parts.Add(Word(Blank.Ailment));
                    parts.Add(new PagePart("as strong as a Hit of"));
                    parts.Add(Word(Blank.Measure));
                    break;
                case Deed.Give:
                    parts.Add(Word(Blank.Gift));
                    parts.Add(new PagePart("to"));
                    parts.Add(Word(Blank.Whom));
                    break;
                case Deed.Raise:
                    parts.Add(Word(Blank.Kin));
                    break;
            }
            return parts;
        }

        private static string Joined(List<PagePart> parts) => string.Join(" ", parts.Select(part => part.Text));

        /// <summary>A page as a sentence or two, for a card.</summary>
        public static string Says(Page page, int index)
        {
            List<List<PagePart>> lines = Parts(page, index);
            int before = lines.Count - page.Outcomes.Count;
            string way = string.Join(", ", lines.Take(before).Select(Joined));
            string outcomes = List(lines.Skip(before).Select(Joined).ToList());
            return $"{char.ToUpperInvariant(way[0])}{way.Substring(1)}. On Impact, {outcomes}";
        }

        private static IEnumerable<string> Scaled(float percent, string what)
        {
            if (percent > 0f) yield return $"{N(percent)}% more {what}";
            if (percent < 0f) yield return $"{N(-percent)}% less {what}";
        }

        /// <summary>What a sentence does to the page it's on, in the words those things are always said in.</summary>
        public static IEnumerable<string> Trade(in Weights weights)
        {
            var lines = new List<string>();
            if (weights.Added != 0f) lines.Add($"{Signed(weights.Added)} Cost");
            lines.AddRange(Scaled(weights.Cost, "Cost"));
            lines.AddRange(Scaled(weights.Potency, "Potency"));
            lines.AddRange(Scaled(weights.Speed, "Projectile Speed"));
            lines.AddRange(Scaled(weights.Area, "Area"));
            lines.AddRange(Scaled(weights.Range, "Range"));
            lines.AddRange(Scaled(weights.UseTime, "Use Time"));
            return lines;
        }

        /// <summary>What a blank on a page is called, for whoever's choosing what goes in it.</summary>
        public static string NameOf(Blank blank) => blank switch
        {
            Blank.From => "Where it begins",
            Blank.Towards => "Where it goes",
            Blank.Shape => "Its shape",
            Blank.Timing => "How long it stays",
            Blank.Within => "How far off",
            Blank.Clause => "What else",
            Blank.Deed => "What happens",
            Blank.Element => "What kind",
            Blank.Measure => "How much",
            Blank.Ailment => "Which",
            Blank.Gift => "Which",
            Blank.Kin => "Who",
            Blank.Pool => "What",
            _ => "Who",
        };

        /// <summary>What a measure is called, for how a number was come to.</summary>
        public static string NameOf(Measure measure) => measure switch
        {
            Measure.Health => "Maximum Health",
            Measure.Mana => "Maximum Mana",
            Measure.Barrier => "Maximum Barrier",
            Measure.Damage => "Weapon Damage",
            _ => measure.ToString(),
        };
    }
}

using Dino_Defenders.Books;
using static Dino_Defenders.Tooltips.Say;

namespace Dino_Defenders.Tooltips
{
    /// <summary>
    /// How a page reads, which is only ever what it comes to, one part after another: where it starts, where it goes or what it covers, and what happens on Impact. It reads what a page comes to and not what's written on
    /// it, as a sentence can change where it starts and where it goes, and a card and a book's own page both say the same.
    /// </summary>
    public static class BookText
    {
        /// <summary>A page, a line at a time: where it starts, where it goes or what it covers, and what it does on Impact.</summary>
        /// <param name="written">What it was found as, for the words of what it does.</param>
        /// <param name="potency">What it deals, of all of its Damage.</param>
        public static List<string> Lines(Page written, PagePlan page, float potency)
        {
            if (page.Kind == PageKind.Summon)
            {
                // It doesn't go anywhere or cover anything: it only raises, there and then, beside you or where the page before it struck.
                string raise = Deed(written.Outcomes[0], page, potency);
                return new List<string> { page.Index == 0 ? "Beside you" : "Where the last struck", "", char.ToUpperInvariant(raise[0]) + raise[1..] };
            }

            var lines = new List<string> { StartOf(page) };
            if (page.Kind == PageKind.Projectile) lines.Add(page.Falls ? FallOf(page) : WayOf(page));
            else lines.Add(page.Lingers ? $"{Phrases.Find(written.Shape)!.Words}, lingering for {Seconds(page.Duration)}" : $"{Phrases.Find(written.Shape)!.Words}, {Phrases.Find(written.Timing)!.Words}");
            lines.Add($"On Impact, {Deed(written.Outcomes[0], page, potency)}");
            return lines;
        }

        private static string StartOf(PagePlan page) => page.From switch
        {
            Start.Impact => "From where the last struck",
            Start.Cursor => "From the cursor",
            Start.Nearest => "From the nearest Enemy",
            Start.Random => "From a random Enemy nearby",
            _ => "From you",
        };

        private static string WayOf(PagePlan page) => page.To switch
        {
            Bearing.Onward => "the way the last was going",
            Bearing.Nearest => "towards the nearest Enemy",
            Bearing.You => "towards you",
            Bearing.Anywhere => "in a random direction",
            _ => "towards the cursor",
        };

        private static string FallOf(PagePlan page) => page.To switch
        {
            Bearing.Onward => "down out of the sky, ahead of where the last struck",
            Bearing.Nearest => "down out of the sky onto the nearest Enemy",
            Bearing.You => "down out of the sky onto you",
            Bearing.Anywhere => "down out of the sky somewhere at random",
            _ => "down out of the sky onto the cursor",
        };

        private static string Deed(Outcome outcome, PagePlan page, float potency)
        {
            string Word(Blank blank) => Scribe.Word(outcome, blank).Words;

            Phrase deed = Phrases.Find(outcome.Deed)!;
            return deed.Deed switch
            {
                Books.Deed.Deal => $"deal {Share(potency)} of your {page.Hand} Damage",
                Books.Deed.Heal => $"{deed.Words} {Word(Blank.Pool)} to {Word(Blank.Whom)} equal to {Word(Blank.Measure)}",
                Books.Deed.Afflict => $"{deed.Words} {Word(Blank.Ailment)} as strong as a Hit of {Word(Blank.Measure)}",
                Books.Deed.Give => $"{deed.Words} {Word(Blank.Gift)} to {Word(Blank.Whom)}",
                Books.Deed.Raise => $"{deed.Words} {Word(Blank.Kin)}",
                _ => deed.Words,
            };
        }

        /// <summary>A page as a sentence or two, for a card.</summary>
        public static string Says(Page written, PagePlan page, float potency)
        {
            List<string> lines = Lines(written, page, potency);
            return lines[1].Length == 0 ? $"{lines[0]}. {lines[2]}" : $"{lines[0]}, {lines[1]}. {lines[2]}";
        }

        /// <summary>What a Trigger Book waits for.</summary>
        public static string Waits(BookTrigger trigger) => trigger.When switch
        {
            BookWhen.Hit => $"After {N(trigger.Amount)} Hits",
            BookWhen.Critical => $"After {N(trigger.Amount)} Critical Hits",
            BookWhen.Kill => $"After {N(trigger.Amount)} Kills",
            BookWhen.Struck => $"After you're Hit {N(trigger.Amount)} times",
            BookWhen.Block => $"After you Block {N(trigger.Amount)} times",
            BookWhen.Dodge => $"After you Dodge {N(trigger.Amount)} Hits",
            BookWhen.Flask => $"After {N(trigger.Amount)} Flasks",
            BookWhen.Damaged => $"After Hits take {N(trigger.Amount)}% of your Maximum Health",
            BookWhen.Moved => $"After you move {Metres(trigger.Amount)}",
            _ => $"After you use {N(trigger.Amount)} Books",
        };

        /// <summary>How a book goes off, in a line, for any that aren't simply pressed.</summary>
        public static string Mode(BookMode mode, BookTrigger? when) => mode switch
        {
            BookMode.Trigger => $"Trigger Book: {(when != null ? Waits(when) : "")}",
            BookMode.Toggle => "Toggle Book: holds Area Pages, every one going at once while it's on",
            _ => "",
        };

        /// <summary>What's good about a sentence, in the words those things are always said in.</summary>
        public static string Good(Sentence sentence) => sentence.Good switch
        {
            PageEffect.Potency => $"{N(sentence.GoodBy)}% more Potency",
            PageEffect.Speed => $"{N(sentence.GoodBy)}% more Projectile Speed",
            PageEffect.Area => $"{N(sentence.GoodBy)}% more Area",
            PageEffect.Duration => $"Lingers {N(sentence.GoodBy)}% longer",
            PageEffect.Range => $"{N(sentence.GoodBy)}% more Range",
            PageEffect.Cost => $"{N(sentence.GoodBy)}% less Cost",
            PageEffect.UseTime => $"{N(sentence.GoodBy)}% less Use Time",
            PageEffect.Projectiles => $"{Signed(sentence.GoodBy)} Projectiles",
            PageEffect.Pierce => $"Pierces {N(sentence.GoodBy)} more Enemies",
            PageEffect.Chains => $"Chains to {N(sentence.GoodBy)} more Enemies",
            PageEffect.Stops => "Stops at its target",
            PageEffect.Rides => "Carries you with it, from an Opening Page that starts from you",
            PageEffect.Grounded => "Hitting the ground or anything solid is an Impact",
            PageEffect.FromCursor => "As the Opening Page, starts from the cursor",
            PageEffect.FromNearest => "As the Opening Page, starts from the nearest Enemy",
            PageEffect.FromRandom => $"As the Opening Page, starts from a random Enemy nearby, with {Trade(Sentences.Weighting(sentence.Good))}",
            PageEffect.ToNearest => "Flies towards the nearest Enemy",
            PageEffect.ToYou => "Flies back towards you",
            PageEffect.ToAnywhere => $"Flies in a random direction, with {Trade(Sentences.Weighting(sentence.Good))}",
            PageEffect.Falls => "Falls out of the sky onto where it's going",
            _ => "",
        };

        /// <summary>What's bad about a sentence. Some written ones have nothing, and then this says nothing.</summary>
        public static string Bad(Sentence sentence) => sentence.Bad switch
        {
            PageEffect.Potency => $"{N(sentence.BadBy)}% less Potency",
            PageEffect.Speed => $"{N(sentence.BadBy)}% less Projectile Speed",
            PageEffect.Area => $"{N(sentence.BadBy)}% less Area",
            PageEffect.Duration => $"Lingers {N(sentence.BadBy)}% shorter",
            PageEffect.Range => $"{N(sentence.BadBy)}% less Range",
            PageEffect.Cost => $"{Signed(sentence.BadBy)} Cost",
            PageEffect.UseTime => $"{N(sentence.BadBy)}% more Use Time",
            PageEffect.Rest => $"{Seconds(sentence.BadBy)} Cooldown",
            _ => "",
        };

        private static IEnumerable<string> Scaled(float percent, string what)
        {
            if (percent > 0f) yield return $"{N(percent)}% more {what}";
            if (percent < 0f) yield return $"{N(-percent)}% less {what}";
        }

        /// <summary>What a phrase does to the page it's on, in the words those things are always said in, as one line.</summary>
        public static string Trade(in Weights weights) => List(Trades(weights).ToList());

        /// <summary>The same, a line for each.</summary>
        public static IEnumerable<string> Trades(in Weights weights)
        {
            var lines = new List<string>();
            if (weights.Added != 0f) lines.Add($"{Signed(weights.Added)} Cost");
            lines.AddRange(Scaled(weights.Cost, "Cost"));
            lines.AddRange(Scaled(weights.Potency, "Potency"));
            lines.AddRange(Scaled(weights.Speed, "Projectile Speed"));
            lines.AddRange(Scaled(weights.Area, "Area"));
            lines.AddRange(Scaled(weights.Range, "Range"));
            lines.AddRange(Scaled(weights.UseTime, "Use Time"));
            lines.AddRange(Scaled(weights.Duration, "Page Duration"));
            return lines;
        }

        /// <summary>What a blank on a page is called, for a list of them.</summary>
        public static string NameOf(Blank blank) => blank switch
        {
            Blank.Shape => "Its shape",
            Blank.Timing => "How long it stays",
            Blank.Deed => "What happens",
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
            Measure.Damage => "Damage",
            _ => measure.ToString(),
        };
    }
}

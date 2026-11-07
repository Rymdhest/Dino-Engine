namespace Dino_Defenders.Books
{
    /// <summary>The two kinds of page. What comes before a page's Outcomes is different on each, and an area page can only be the last.</summary>
    public enum PageKind : byte
    {
        Projectile,
        Area,
    }

    /// <summary>One thing that happens at an impact, as sentences. Which of these mean anything goes by the deed: see <see cref="Sentences.BlanksOf"/>.</summary>
    public sealed class Outcome
    {
        public string Deed { get; set; } = Sentences.PlainDeed;
        public string Element { get; set; } = Sentences.PlainElement;
        public string Measure { get; set; } = Sentences.PlainMeasure;
        public string Ailment { get; set; } = Sentences.PlainAilment;
        public string Gift { get; set; } = Sentences.PlainGift;
        public string Kin { get; set; } = Sentences.PlainKin;
        public string Pool { get; set; } = Sentences.PlainPool;
        public string Whom { get; set; } = Sentences.PlainWhom;

        public Outcome Copy() => (Outcome)MemberwiseClone();
    }

    /// <summary>A page as it's written, which is sentence ids and nothing else. It's what's saved and what's sent, and what it comes to is <see cref="Scribe"/>'s business, so a change to a sentence reaches every book there is.</summary>
    public sealed class Page
    {
        public PageKind Kind { get; set; }

        /// <summary>Where it begins. A page that follows another begins where that one struck, whatever this says.</summary>
        public string From { get; set; } = Sentences.PlainFrom;
        public string? FromWithin { get; set; }

        /// <summary>Where a projectile goes.</summary>
        public string Towards { get; set; } = Sentences.PlainTowards;
        public string? TowardsWithin { get; set; }

        /// <summary>What an area covers, and for how long.</summary>
        public string Shape { get; set; } = Sentences.PlainShape;
        public string Timing { get; set; } = Sentences.PlainTiming;

        public List<string> Clauses { get; set; } = new();
        public List<Outcome> Outcomes { get; set; } = new();

        public Page Copy()
        {
            var page = (Page)MemberwiseClone();
            page.Clauses = new List<string>(Clauses);
            page.Outcomes = Outcomes.Select(outcome => outcome.Copy()).ToList();
            return page;
        }
    }

    /// <summary>What's in a book. It belongs to an <see cref="Progression.Item"/>, which is where its name and its look are, and it only ever changes as a whole: see <see cref="Progression.HeroState.Write"/>.</summary>
    public sealed class Book
    {
        public List<Page> Pages { get; set; } = new();

        public Book Copy() => new Book { Pages = Pages.Select(page => page.Copy()).ToList() };
    }
}

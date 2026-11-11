using Dino_Defenders.Stats;
using System.Text.Json.Serialization;

namespace Dino_Defenders.Books
{
    /// <summary>
    /// The kinds of page. What comes before a page's Outcomes is different on each. A Summon Page does nothing but raise Minions beside whoever used it, so it has no flight and no shape, and raising is all it
    /// can do. A Summon Page has no impact to turn from, so it can only be the last; an area turns from everyone it reaches. Saved by name and sent by number, so a new one goes on the end.
    /// </summary>
    public enum PageKind : byte
    {
        Projectile,
        Area,
        Summon,
    }

    /// <summary>What a page deals, if it deals anything: all of your Attack Damage or all of your Spell Damage, whatever types those are. It's found that way and never changes, and it's also what decides how long it takes.</summary>
    public enum Hand : byte
    {
        Attack,
        Spell,
    }

    /// <summary>One thing that happens at an impact, as phrases. Which of these mean anything goes by the deed: see <see cref="Phrases.BlanksOf"/>.</summary>
    public sealed class Outcome
    {
        public string Deed { get; set; } = Phrases.PlainDeed;
        public string Measure { get; set; } = Phrases.PlainMeasure;
        public string Ailment { get; set; } = Phrases.PlainAilment;
        public string Gift { get; set; } = Phrases.PlainGift;
        public string Kin { get; set; } = Phrases.PlainKin;
        public string Pool { get; set; } = Phrases.PlainPool;
        public string Whom { get; set; } = Phrases.PlainWhom;

        /// <summary>What kind of damage it dealt, which only a save from before pages were found as they are has. See <see cref="Progression.SaveGame"/>.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Element { get; set; }

        public Outcome Copy() => (Outcome)MemberwiseClone();
    }

    /// <summary>
    /// A page as it was found, which is what kind it is, what it deals and phrase ids. Nothing about it can be changed: it's what's saved and what's sent, and what it comes to is <see cref="Scribe"/>'s business, so a change
    /// to a phrase reaches every book there is. Where it starts and where it goes aren't written, as every page starts from you or from where the last struck, and goes towards the cursor or on the way the last was going:
    /// only a sentence changes that. Its sentences are items of their own, held by the item the page belongs to, and are only here once <see cref="Scribe.Bind"/> has gathered them, or on what's already written.
    /// </summary>
    public sealed class Page
    {
        public PageKind Kind { get; set; }

        public Hand Hand { get; set; }

        /// <summary>What an area covers, and for how long.</summary>
        public string Shape { get; set; } = Phrases.PlainShape;
        public string Timing { get; set; } = Phrases.PlainTiming;

        [JsonIgnore]
        public List<Sentence> Sentences { get; set; } = new();

        /// <summary>What a Summon Page gives its Minions to fight with: a weapon, and a book they use as often as they can. Only here once <see cref="Scribe.Bind"/> has gathered them.</summary>
        [JsonIgnore]
        public Progression.Item? MinionWeapon { get; set; }

        [JsonIgnore]
        public Progression.Item? MinionOffHand { get; set; }

        [JsonIgnore]
        public Progression.Item? MinionBook { get; set; }

        /// <summary>What's rolled on the page as an item, which counts on it alone. Only here once <see cref="Scribe.Bind"/> has gathered it: see <see cref="Progression.FolioAffixes"/>.</summary>
        [JsonIgnore]
        public Mod[] Mods { get; set; } = Array.Empty<Mod>();

        public List<Outcome> Outcomes { get; set; } = new();

        /// <summary>What a page said before sentences, which only a save that old has. See <see cref="Progression.SaveGame"/>.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Clauses { get; set; }

        public Page Copy()
        {
            var page = (Page)MemberwiseClone();
            page.Sentences = Sentences.Select(sentence => sentence.Copy()).ToList();
            page.Outcomes = Outcomes.Select(outcome => outcome.Copy()).ToList();
            return page;
        }
    }

    /// <summary>A book as it reads: its pages in order, with the sentences on them. A hero's is worked out from their items by <see cref="Scribe.Bind"/>, and what's on <see cref="Shelf"/> is only ever this.</summary>
    public sealed class Book
    {
        public List<Page> Pages { get; set; } = new();

        /// <summary>How it goes off.</summary>
        public BookMode Mode { get; set; }

        /// <summary>What it's waiting for, if it's a Trigger Book.</summary>
        public BookTrigger? When { get; set; }

        /// <summary>What's rolled on the book as an item, which counts on every page in it.</summary>
        [JsonIgnore]
        public Mod[] Mods { get; set; } = Array.Empty<Mod>();

        public Book Copy() => new Book { Pages = Pages.Select(page => page.Copy()).ToList(), Mode = Mode, When = When?.Copy(), Mods = Mods };
    }
}

using Dino_Defenders.Stats;

namespace Dino_Defenders.Books
{
    /// <summary>The start of working out what a page deals: how much of what it's measured against, as what.</summary>
    public readonly record struct DamagePart(DamageType Type, Measure Measure, float Share);

    public readonly record struct HealPart(Pool Pool, Whom Whom, Measure Measure, float Share);

    /// <summary>An ailment a page puts on someone outright, as strong as a hit of this much would have left.</summary>
    public readonly record struct AilmentPart(Ailment Ailment, Measure Measure, float Share);

    public readonly record struct GiftPart(BuffDefinition Buff, Whom Whom);

    /// <summary>
    /// What a page says, whoever is holding it. It's the start of a calculation: <see cref="PageProfile"/> finishes it once for each actor, and nothing here knows anything about who that is. Attack or Spell isn't here
    /// for that reason, as it goes by what's in their hands.
    /// </summary>
    public sealed class PagePlan
    {
        public int Index;
        public bool Last;
        public PageKind Kind;

        public Start From;
        public float FromWithin;
        public Bearing To;
        public float ToWithin;

        public AreaShape Shape;
        public float Length;
        public float Width;
        public float Duration;
        public float Interval = Rulebook.PulseInterval;

        public float Speed;
        public float Range = Rulebook.PageRange;
        public int Projectiles = 1;
        public float Spread = Rulebook.ProjectileSpread;
        public int Pierce;
        public int Chains;
        public bool Stops;
        public bool Rides;

        /// <summary>What sentences have made of its strength, its Cost and how long it takes.</summary>
        public float Potency = 1f;
        public float Cost;
        public float UseTime = Rulebook.PageUseTime;
        public float ImpactAt = Rulebook.PageImpactAt;
        public float MoveFactor = Rulebook.PageMoveFactor;
        public float Cooldown;

        public Tags Tags;
        public DamageType Look;

        public DamagePart[] Damage = Array.Empty<DamagePart>();
        public HealPart[] Heals = Array.Empty<HealPart>();
        public AilmentPart[] Ailments = Array.Empty<AilmentPart>();
        public GiftPart[] Gifts = Array.Empty<GiftPart>();
        public string? Minion;
        public int MinionCount;
        public bool Moves;
        public bool MakesWater;
        public float Push;              // Metres a second, and below nothing it's a pull.

        /// <summary>What's particular to this page. These count only on it.</summary>
        public Mod[] Mods = Array.Empty<Mod>();

        public bool Lingers => Duration > 0f;

        /// <summary>Whether anything it does falls on an Enemy, which is who a projectile stops at.</summary>
        public bool Hostile => Damage.Length > 0 || Ailments.Length > 0 || Push != 0f;

        /// <summary>Whether what it does is only for friends, in which case that's who a projectile stops at.</summary>
        public bool SeeksFriends => !Hostile && Array.Exists(Heals, heal => heal.Whom == Whom.Friends) | Array.Exists(Gifts, gift => gift.Whom == Whom.Friends);

        public bool DealsDamage => Damage.Length > 0;
    }

    /// <summary>What a book says. Written ones are shared by everyone who has them, and a hero's are read again whenever anything about them changes, which costs next to nothing.</summary>
    public sealed class BookPlan
    {
        public const ushort None = 0;

        public ushort Number;           // Its place on the shelf, for those that are named by something else.
        public string Id = "";
        public string Name = "";
        public PagePlan[] Pages = Array.Empty<PagePlan>();

        /// <summary>What it was read from, for those that can be copied out.</summary>
        public Book? Source;

        public PagePlan First => Pages[0];
    }

    /// <summary>
    /// Reads books. What's written is only ever sentence ids, so anything can come in here, from a save, a stranger or an older build: <see cref="Fix"/> turns whatever arrives into something that can be read, and
    /// <see cref="Read"/> works out what it says, the same way on every machine.
    /// </summary>
    public static class Scribe
    {
        /// <summary>What everyone starts with: a blow in front of them, for nothing.</summary>
        public static Book Starter() => new Book { Pages = { NewPage(PageKind.Area) } };

        public static Page NewPage(PageKind kind)
        {
            var page = new Page { Kind = kind };
            page.Outcomes.Add(new Outcome());
            return page;
        }

        private static string Valid(string? id, Blank blank, string plain, PageKind kind, bool following = false)
        {
            Sentence? sentence = Sentences.Find(id);
            bool fits = sentence != null && sentence.Blank == blank && sentence.Fits is null || sentence?.Fits == kind && sentence.Blank == blank;
            return fits && (following || !sentence!.FollowsOnly) ? id! : plain;
        }

        private static string? ValidWithin(string? id)
        {
            return Sentences.Find(id)?.Blank == Blank.Within ? id : null;
        }

        /// <summary>
        /// Makes a book into one that can be read, leaving anything right as it was: what's unknown goes back to plain, and whatever doesn't belong comes out. Doing this twice changes nothing. Both ends do this to whatever
        /// they're given, so it decides what's allowed.
        /// </summary>
        public static void Fix(Book book)
        {
            book.Pages ??= new List<Page>();
            book.Pages.RemoveAll(page => page == null);
            if (book.Pages.Count == 0) book.Pages.Add(NewPage(PageKind.Area));
            if (book.Pages.Count > Rulebook.MostPages) book.Pages.RemoveRange(Rulebook.MostPages, book.Pages.Count - Rulebook.MostPages);

            // An area has no impact to turn from.
            int firstArea = book.Pages.FindIndex(page => page.Kind == PageKind.Area);
            if (firstArea >= 0 && firstArea < book.Pages.Count - 1) book.Pages.RemoveRange(firstArea + 1, book.Pages.Count - firstArea - 1);

            for (int i = 0; i < book.Pages.Count; i++) Fix(book.Pages[i], i);
        }

        private static void Fix(Page page, int index)
        {
            if (!Enum.IsDefined(page.Kind)) page.Kind = PageKind.Area;
            page.From = Valid(page.From, Blank.From, Sentences.PlainFrom, page.Kind);
            page.FromWithin = ValidWithin(page.FromWithin);
            page.Towards = Valid(page.Towards, Blank.Towards, Sentences.PlainTowards, PageKind.Projectile, following: index > 0);
            page.TowardsWithin = ValidWithin(page.TowardsWithin);
            page.Shape = Valid(page.Shape, Blank.Shape, Sentences.PlainShape, PageKind.Area);
            page.Timing = Valid(page.Timing, Blank.Timing, Sentences.PlainTiming, PageKind.Area);

            page.Clauses ??= new List<string>();
            var kept = new List<string>();
            var groups = new HashSet<string>();
            foreach (string id in page.Clauses)
            {
                if (kept.Count >= Rulebook.MostClauses || kept.Contains(id) || Sentences.Find(id) is not Sentence clause || clause.Blank != Blank.Clause) continue;
                if (clause.Fits is PageKind fits && fits != page.Kind) continue;
                if (clause.Group != null && !groups.Add(clause.Group)) continue;

                // Only the first page can carry anyone, as it's from them that it has to go.
                if (clause.Rides && (index > 0 || Sentences.Find(page.From)!.Start != Start.You || Sentences.Find(page.Towards)!.Bearing == Bearing.Down)) continue;
                kept.Add(id);
            }
            page.Clauses = kept;

            page.Outcomes ??= new List<Outcome>();
            page.Outcomes.RemoveAll(outcome => outcome == null);
            if (page.Outcomes.Count == 0) page.Outcomes.Add(new Outcome());
            if (page.Outcomes.Count > Rulebook.MostOutcomes) page.Outcomes.RemoveRange(Rulebook.MostOutcomes, page.Outcomes.Count - Rulebook.MostOutcomes);
            foreach (Outcome outcome in page.Outcomes) Fix(outcome);
        }

        private static void Fix(Outcome outcome)
        {
            outcome.Deed = Valid(outcome.Deed, Blank.Deed, Sentences.PlainDeed, PageKind.Area);
            outcome.Element = Valid(outcome.Element, Blank.Element, Sentences.PlainElement, PageKind.Area);
            outcome.Measure = Valid(outcome.Measure, Blank.Measure, Sentences.PlainMeasure, PageKind.Area);
            outcome.Ailment = Valid(outcome.Ailment, Blank.Ailment, Sentences.PlainAilment, PageKind.Area);
            outcome.Gift = Valid(outcome.Gift, Blank.Gift, Sentences.PlainGift, PageKind.Area);
            outcome.Kin = Valid(outcome.Kin, Blank.Kin, Sentences.PlainKin, PageKind.Area);
            outcome.Pool = Valid(outcome.Pool, Blank.Pool, Sentences.PlainPool, PageKind.Area);
            outcome.Whom = Valid(outcome.Whom, Blank.Whom, Sentences.PlainWhom, PageKind.Area);
        }

        /// <summary>Everything on a page that counts towards its numbers: what's chosen, and of an Outcome only what that deed says.</summary>
        public static IEnumerable<Sentence> SentencesOn(Page page, int index)
        {
            if (index == 0) yield return Sentences.Find(page.From)!;
            if (Sentences.Find(page.FromWithin) is Sentence fromWithin) yield return fromWithin;
            if (page.Kind == PageKind.Projectile)
            {
                yield return Sentences.Find(page.Towards)!;
                if (Sentences.Find(page.TowardsWithin) is Sentence towardsWithin) yield return towardsWithin;
            }
            else
            {
                yield return Sentences.Find(page.Shape)!;
                yield return Sentences.Find(page.Timing)!;
            }

            foreach (string id in page.Clauses) yield return Sentences.Find(id)!;
            foreach (Outcome outcome in page.Outcomes)
            {
                Sentence deed = Sentences.Find(outcome.Deed)!;
                yield return deed;
                foreach (Blank blank in Sentences.BlanksOf(deed.Deed)) yield return Word(outcome, blank);
            }
        }

        /// <summary>What an Outcome has in one of its blanks.</summary>
        public static Sentence Word(Outcome outcome, Blank blank) => Sentences.Find(blank switch
        {
            Blank.Element => outcome.Element,
            Blank.Measure => outcome.Measure,
            Blank.Ailment => outcome.Ailment,
            Blank.Gift => outcome.Gift,
            Blank.Kin => outcome.Kin,
            Blank.Pool => outcome.Pool,
            Blank.Whom => outcome.Whom,
            _ => outcome.Deed,
        })!;

        /// <summary>What a book says. It's read as it stands, so it has to have been through <see cref="Fix"/>.</summary>
        public static BookPlan Read(Book book, string name = "")
        {
            var plan = new BookPlan { Name = name, Source = book, Pages = new PagePlan[book.Pages.Count] };
            for (int i = 0; i < plan.Pages.Length; i++) plan.Pages[i] = Read(book.Pages[i], i, i == plan.Pages.Length - 1);
            return plan;
        }

        private static PagePlan Read(Page written, int index, bool last)
        {
            var page = new PagePlan { Index = index, Last = last, Kind = written.Kind };
            bool area = written.Kind == PageKind.Area;

            page.From = index == 0 ? Sentences.Find(written.From)!.Start : Start.Impact;
            page.FromWithin = Sentences.Find(written.FromWithin)?.Scatter ?? 0f;
            if (!area)
            {
                page.To = Sentences.Find(written.Towards)!.Bearing;
                page.ToWithin = Sentences.Find(written.TowardsWithin)?.Scatter ?? 0f;
            }
            else
            {
                Sentence shape = Sentences.Find(written.Shape)!;
                page.Shape = shape.Shape;
                page.Length = shape.Length;
                page.Width = shape.Width;
                page.Duration = Sentences.Find(written.Timing)!.Duration;
            }

            float added = area ? 0f : Rulebook.ProjectileCost;
            float cost = 1f;
            float potency = 1f;
            float speed = Rulebook.ProjectileSpeed;
            float scale = 1f;
            float range = Rulebook.PageRange;
            float useTime = Rulebook.PageUseTime;
            bool wild = false;

            foreach (Sentence sentence in SentencesOn(written, index))
            {
                Weights weights = sentence.Weights;
                added += weights.Added;
                cost *= 1f + weights.Cost / 100f;
                potency *= 1f + weights.Potency / 100f;
                speed *= 1f + weights.Speed / 100f;
                scale *= 1f + weights.Area / 100f;
                range *= 1f + weights.Range / 100f;
                useTime *= 1f + weights.UseTime / 100f;
                wild |= sentence.Wild;

                page.Projectiles = Math.Max(page.Projectiles, sentence.Projectiles);
                page.Pierce = Math.Max(page.Pierce, sentence.Pierce);
                page.Chains = Math.Max(page.Chains, sentence.Chains);
                page.Stops |= sentence.Stops;
                page.Rides |= sentence.Rides;
                page.Cooldown = MathF.Max(page.Cooldown, sentence.Cooldown);
            }

            // Past the first, an Outcome is a thing more to pay for, whatever it is.
            added += Rulebook.OutcomeCost * (written.Outcomes.Count - 1);

            page.Cost = added * cost;
            page.Potency = Math.Clamp(potency, Rulebook.LeastPotency, Rulebook.MostPotency);
            page.Speed = speed;
            page.Range = range;
            page.UseTime = useTime;

            // An area's size is its area, so what goes on a side is the root of that. A cone's angle stays as it was, as only how far it reaches means anything.
            float reach = MathF.Sqrt(scale);
            page.Length *= reach;
            if (page.Shape is AreaShape.Line or AreaShape.Rectangle) page.Width *= reach;

            // What goes in the hands takes longer to come round to than what's let go of.
            if (area && page.From == Start.You) page.ImpactAt = Rulebook.PageStrikeAt;

            ReadOutcomes(written, page);
            page.Tags = TagsOf(page, index, last, wild);
            page.Look = LookOf(page);
            return page;
        }

        private static void ReadOutcomes(Page written, PagePlan page)
        {
            var damage = new List<DamagePart>();
            var heals = new List<HealPart>();
            var ailments = new List<AilmentPart>();
            var gifts = new List<GiftPart>();

            foreach (Outcome outcome in written.Outcomes)
            {
                Sentence measure = Sentences.Find(outcome.Measure)!;
                Sentence whom = Sentences.Find(outcome.Whom)!;
                switch (Sentences.Find(outcome.Deed)!.Deed)
                {
                    case Deed.Deal:
                        damage.Add(new DamagePart(Sentences.Find(outcome.Element)!.Element, measure.Measure, measure.Share));
                        break;
                    case Deed.Heal:
                        heals.Add(new HealPart(Sentences.Find(outcome.Pool)!.Pool, whom.Whom, measure.Measure, measure.Share));
                        break;
                    case Deed.Afflict:
                        ailments.Add(new AilmentPart(Sentences.Find(outcome.Ailment)!.Ailment, measure.Measure, measure.Share));
                        break;
                    case Deed.Give:
                        gifts.Add(new GiftPart(Sentences.Find(outcome.Gift)!.Buff!, whom.Whom));
                        break;
                    case Deed.Raise:
                        Sentence kin = Sentences.Find(outcome.Kin)!;
                        page.Minion = kin.Minion;
                        page.MinionCount = Math.Max(page.MinionCount, kin.MinionCount);
                        break;
                    case Deed.Move:
                        page.Moves = true;
                        break;
                    case Deed.Water:
                        page.MakesWater = true;
                        break;
                    case Deed.Push:
                        page.Push = Rulebook.PushSpeed;
                        break;
                    case Deed.Pull:
                        page.Push = -Rulebook.PushSpeed;
                        break;
                }
            }

            page.Damage = damage.ToArray();
            page.Heals = heals.ToArray();
            page.Ailments = ailments.ToArray();
            page.Gifts = gifts.ToArray();
        }

        private static Tags TagsOf(PagePlan page, int index, bool last, bool wild)
        {
            Tags tags = page.Kind == PageKind.Projectile ? Tags.Projectile : Tags.Area;
            if (page.Lingers) tags |= Tags.Zone;
            if (page.Chains > 0) tags |= Tags.Chaining;
            if (page.Rides || page.Moves) tags |= Tags.Movement;
            if (page.Gifts.Length > 0) tags |= Tags.Buff;
            if (page.Minion != null) tags |= Tags.Minion;
            if (page.Kind == PageKind.Projectile && page.To == Bearing.Down) tags |= Tags.Falling;
            if (wild) tags |= Tags.Wild;
            tags |= index == 0 ? Tags.Opening : Tags.Turned;
            if (last) tags |= Tags.Closing;
            return tags;
        }

        // What it looks and sounds like: what it deals if it deals anything, and otherwise what it leaves.
        private static DamageType LookOf(PagePlan page)
        {
            if (page.Damage.Length > 0) return page.Damage[0].Type;
            if (page.Ailments.Length > 0) return AilmentRules.TypeOf(page.Ailments[0].Ailment);
            return DamageType.Magic;
        }
    }
}

using Dino_Defenders.Stats;
using Dino_Engine.World;

namespace Dino_Defenders.Books
{
    public readonly record struct HealPart(Pool Pool, Whom Whom, Measure Measure, float Share);

    /// <summary>An ailment a page puts on someone outright, as strong as a hit of this much would have left.</summary>
    public readonly record struct AilmentPart(Ailment Ailment, Measure Measure, float Share);

    public readonly record struct GiftPart(BuffDefinition Buff, Whom Whom);

    /// <summary>
    /// What a page says, whoever is holding it. It's the start of a calculation: <see cref="PageProfile"/> finishes it once for each actor, and nothing here knows anything about who that is, so what an Attack Page deals
    /// is whatever Attack Damage they have.
    /// </summary>
    public sealed class PagePlan
    {
        public int Index;
        public bool Last;
        public PageKind Kind;
        public Hand Hand;

        public Start From;
        public Bearing To;

        /// <summary>Comes down out of the sky onto where it's going, rather than flying there.</summary>
        public bool Falls;

        /// <summary>Reaching something solid, or the ground it falls on, is an impact, as reaching someone is.</summary>
        public bool Grounded;

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

        /// <summary>What sentences and stickers have made of its strength, its Cost and how long it takes. Its strength goes on from the page before: see <see cref="Carried"/>.</summary>
        public float Potency = 1f;

        /// <summary>The Potency it started from, which is where the page before it left off.</summary>
        public float Carried = 1f;
        public float Cost;

        /// <summary>
        /// Seconds it adds to a book's use: for an Attack Page, what it takes with bare hands, which a weapon goes by and which only counts on the first page, and for a Spell Page what it takes to cast. What it's
        /// multiplied by is <see cref="UseScale"/>, which goes for the whole book.
        /// </summary>
        public float UseTime = Rulebook.PageUseTime;
        public float UseScale = 1f;
        public float ImpactAt = Rulebook.PageImpactAt;
        public float MoveFactor = Rulebook.PageMoveFactor;
        public float Cooldown;

        public Tags Tags;

        /// <summary>Whether it deals all of whichever Damage its <see cref="Hand"/> says.</summary>
        public bool Deals;
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
        public bool Hostile => Deals || Ailments.Length > 0 || Push != 0f;

        /// <summary>Whether what it does is only for friends, in which case that's who a projectile stops at.</summary>
        public bool SeeksFriends => !Hostile && Array.Exists(Heals, heal => heal.Whom == Whom.Friends) | Array.Exists(Gifts, gift => gift.Whom == Whom.Friends);

        public bool DealsDamage => Deals;

        /// <summary>What it looks like before anyone's holding it, which is what it'll deal.</summary>
        public DamageType Look => Deals || Ailments.Length > 0 ? (Ailments.Length > 0 && !Deals ? AilmentRules.TypeOf(Ailments[0].Ailment) : Hand == Hand.Spell ? DamageType.Magic : DamageType.Physical) : DamageType.Magic;
    }

    /// <summary>What a book says. Written ones are shared by everyone who has them, and a hero's are read again whenever anything about them changes, which costs next to nothing.</summary>
    public sealed class BookPlan
    {
        public const ushort None = 0;

        public ushort Number;           // Its place on the shelf, for those that are named by something else.
        public BookMode Mode;
        public BookTrigger? When;
        public string Id = "";
        public string Name = "";
        public PagePlan[] Pages = Array.Empty<PagePlan>();

        /// <summary>What it was read from, for those that can be copied out.</summary>
        public Book? Source;

        public PagePlan First => Pages[0];
    }

    /// <summary>
    /// Reads books. What's written is only ever sentence ids, so anything can come in here, from a save, a stranger or an older build: <see cref="Fix"/> turns whatever arrives into something that can be read, and
    /// <see cref="Read"/> works out what it says, the same way on every machine. Nobody writes a page: <see cref="Roll"/> is what one is found with.
    /// </summary>
    public static class Scribe
    {
        public static Page NewPage(PageKind kind, Hand hand = Hand.Attack)
        {
            var page = new Page { Kind = kind, Hand = hand };
            page.Outcomes.Add(new Outcome());
            return page;
        }

        /// <summary>
        /// What a found page says, which it keeps for good: its kind, Attack or Spell, its shape if it's an area, and what it does, which is nearly always to deal damage. What isn't dealing is cast, so it's a Spell,
        /// and moving goes by a projectile that reached someone, as an area that starts from you would leave you where you are.
        /// </summary>
        public static Page Roll(WorldRandom random)
        {
            string deed = Draw(Blank.Deed, random);
            Deed doing = Sentences.Find(deed)!.Deed;
            PageKind kind = doing == Deed.Move || random.Chance(0.5f) ? PageKind.Projectile : PageKind.Area;
            var page = NewPage(kind, doing != Deed.Deal || random.Chance(0.5f) ? Hand.Spell : Hand.Attack);
            Outcome outcome = page.Outcomes[0];
            outcome.Deed = deed;
            if (doing == Deed.Raise) outcome.Kin = random.Chance(0.5f) ? Sentences.PlainKin : "hound";
            if (kind == PageKind.Area)
            {
                page.Shape = Draw(Blank.Shape, random);
                page.Timing = Draw(Blank.Timing, random);
            }
            return page;
        }

        // One of what a found page can have in a blank, by how often each turns up.
        private static string Draw(Blank blank, WorldRandom random)
        {
            Sentence[] options = Sentences.For(blank).Where(sentence => sentence.Found > 0f).ToArray();
            float pick = random.Float() * options.Sum(sentence => sentence.Found);
            foreach (Sentence sentence in options)
            {
                pick -= sentence.Found;
                if (pick < 0f) return sentence.Id;
            }
            return options[^1].Id;
        }

        private static string Valid(string? id, Blank blank, string plain, PageKind kind)
        {
            Sentence? sentence = Sentences.Find(id);
            return sentence != null && sentence.Blank == blank && (sentence.Fits is null || sentence.Fits == kind) ? id! : plain;
        }

        /// <summary>What an item comes to as a book: the pages it holds, in order, each with what's stuck on it. A page on its own reads as a book of one. Nothing if there's nothing in it to read.</summary>
        public static Book? Bind(Progression.Item item)
        {
            var book = new Book { Mode = item.Mode, When = item.When };
            if (item.IsPage) AddPage(book, item);
            else if (item.IsBook)
            {
                foreach (Progression.Item? held in item.Held ?? Array.Empty<Progression.Item?>())
                {
                    if (held is { IsPage: true }) AddPage(book, held);
                }
            }

            if (book.Pages.Count == 0) return null;
            Fix(book);
            return book;
        }

        private static void AddPage(Book book, Progression.Item item)
        {
            if (item.Page == null) return;
            Page page = item.Page.Copy();
            foreach (Progression.Item? held in item.Held ?? Array.Empty<Progression.Item?>())
            {
                if (held?.Sticker is Sticker sticker) page.Stickers.Add(sticker.Copy());
            }
            book.Pages.Add(page);
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

            // An area has no impact to turn from. What's left on goes by nothing but pulses, so that's all it can hold, every one of them going at once.
            if (book.Mode == BookMode.Toggle)
            {
                book.Pages.RemoveAll(page => page.Kind != PageKind.Area);
                if (book.Pages.Count == 0) book.Pages.Add(NewPage(PageKind.Area));
            }
            else
            {
                int firstArea = book.Pages.FindIndex(page => page.Kind == PageKind.Area);
                if (firstArea >= 0 && firstArea < book.Pages.Count - 1) book.Pages.RemoveRange(firstArea + 1, book.Pages.Count - firstArea - 1);
            }

            for (int i = 0; i < book.Pages.Count; i++)
            {
                Fix(book.Pages[i]);
                if (book.Mode == BookMode.Toggle) book.Pages[i].Timing = Sentences.PlainTiming;
            }
        }

        /// <summary>
        /// The same for one page. Anything any sentence can say stays, as nobody writes a page: it's what was found, or what's already written and copied out, and a save from before pages were found is brought up to
        /// what a found page can say once, in <see cref="Progression.SaveGame"/>.
        /// </summary>
        public static void Fix(Page page)
        {
            if (!Enum.IsDefined(page.Kind)) page.Kind = PageKind.Area;
            if (!Enum.IsDefined(page.Hand)) page.Hand = Hand.Attack;
            page.Shape = Valid(page.Shape, Blank.Shape, Sentences.PlainShape, PageKind.Area);
            page.Timing = Valid(page.Timing, Blank.Timing, Sentences.PlainTiming, PageKind.Area);

            page.Stickers ??= new List<Sticker>();
            page.Stickers.RemoveAll(sticker => sticker == null || !Stickers.GoesOn(sticker, page.Kind));
            if (page.Stickers.Count > Rulebook.MostStickers) page.Stickers.RemoveRange(Rulebook.MostStickers, page.Stickers.Count - Rulebook.MostStickers);

            // A page does one thing.
            page.Outcomes ??= new List<Outcome>();
            page.Outcomes.RemoveAll(outcome => outcome == null);
            if (page.Outcomes.Count == 0) page.Outcomes.Add(new Outcome());
            if (page.Outcomes.Count > 1) page.Outcomes.RemoveRange(1, page.Outcomes.Count - 1);
            Fix(page.Outcomes[0]);
        }

        private static void Fix(Outcome outcome)
        {
            outcome.Element = null;
            outcome.Deed = Valid(outcome.Deed, Blank.Deed, Sentences.PlainDeed, PageKind.Area);
            outcome.Measure = Valid(outcome.Measure, Blank.Measure, Sentences.PlainMeasure, PageKind.Area);
            outcome.Ailment = Valid(outcome.Ailment, Blank.Ailment, Sentences.PlainAilment, PageKind.Area);
            outcome.Gift = Valid(outcome.Gift, Blank.Gift, Sentences.PlainGift, PageKind.Area);
            outcome.Kin = Valid(outcome.Kin, Blank.Kin, Sentences.PlainKin, PageKind.Area);
            outcome.Pool = Valid(outcome.Pool, Blank.Pool, Sentences.PlainPool, PageKind.Area);
            outcome.Whom = Valid(outcome.Whom, Blank.Whom, Sentences.PlainWhom, PageKind.Area);
        }

        /// <summary>Everything on a page that counts towards its numbers: its shape if it has one, and of an Outcome only what that deed says.</summary>
        public static IEnumerable<Sentence> SentencesOn(Page page)
        {
            if (page.Kind == PageKind.Area)
            {
                yield return Sentences.Find(page.Shape)!;
                yield return Sentences.Find(page.Timing)!;
            }

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
            var plan = new BookPlan { Name = name, Source = book, Pages = new PagePlan[book.Pages.Count], Mode = book.Mode, When = book.When };

            // Strength goes on from page to page: what's left at the end of one is where the next starts from.
            float carried = 1f;
            for (int i = 0; i < plan.Pages.Length; i++)
            {
                plan.Pages[i] = Read(book.Pages[i], i, i == plan.Pages.Length - 1 || book.Mode == BookMode.Toggle, carried);
                carried = plan.Pages[i].Potency;
            }
            return plan;
        }

        private static PagePlan Read(Page written, int index, bool last, float carried)
        {
            var page = new PagePlan { Index = index, Last = last, Kind = written.Kind, Hand = written.Hand, Carried = carried };
            bool area = written.Kind == PageKind.Area;

            // Everything starts from you and goes towards the cursor, and everything after that from where the last struck, on the way it was going. Only stickers say otherwise.
            page.From = index == 0 ? Start.You : Start.Impact;
            page.To = index == 0 ? Bearing.Cursor : Bearing.Onward;
            page.UseTime = written.Hand == Hand.Spell ? Rulebook.CastTime : Rulebook.PageUseTime;
            if (area)
            {
                Sentence shape = Sentences.Find(written.Shape)!;
                page.Shape = shape.Shape;
                page.Length = shape.Length;
                page.Width = shape.Width;
                page.Duration = Sentences.Find(written.Timing)!.Duration;
            }

            float added = area ? 0f : Rulebook.ProjectileCost;
            float cost = 1f;
            float potency = carried;
            float speed = Rulebook.ProjectileSpeed;
            float scale = 1f;
            float range = Rulebook.PageRange;
            float useScale = 1f;
            bool wild = false;

            void Weigh(in Weights weights)
            {
                added += weights.Added;
                cost *= 1f + weights.Cost / 100f;
                potency *= 1f + weights.Potency / 100f;
                speed *= 1f + weights.Speed / 100f;
                scale *= 1f + weights.Area / 100f;
                range *= 1f + weights.Range / 100f;
                useScale *= 1f + weights.UseTime / 100f;
            }

            foreach (Sentence sentence in SentencesOn(written)) Weigh(sentence.Weights);

            foreach (Sticker sticker in written.Stickers)
            {
                Weigh(Stickers.WeightsOf(sticker));
                wild |= Stickers.IsWild(sticker);
                Stick(page, index, sticker.Good, sticker.GoodBy);
                Stick(page, index, sticker.Bad, sticker.BadBy);
            }
            page.Projectiles = Math.Min(page.Projectiles, Rulebook.MostProjectiles);

            // Only the first page can carry anyone, as it's from them that it has to go, and nobody rides what falls out of the sky.
            page.Rides &= index == 0 && page.From == Start.You && !page.Falls;

            page.Cost = added * cost;
            page.Potency = Math.Clamp(potency, Rulebook.LeastPotency, Rulebook.MostPotency);
            page.Speed = speed;
            page.Range = range;
            page.UseScale = useScale;

            // An area's size is its area, so what goes on a side is the root of that. A cone's angle stays as it was, as only how far it reaches means anything.
            float reach = MathF.Sqrt(scale);
            page.Length *= reach;
            if (page.Shape is AreaShape.Line or AreaShape.Rectangle) page.Width *= reach;

            // What goes in the hands takes longer to come round to than what's let go of.
            if (area && page.From == Start.You) page.ImpactAt = Rulebook.PageStrikeAt;

            ReadOutcomes(written, page);
            page.Tags = TagsOf(page, index, last, wild);
            return page;
        }

        // What a sticker does that isn't a number on a page, and that stacks with another that does the same. Where a page starts only means anything on the first, as every other starts where the last struck.
        private static void Stick(PagePlan page, int index, PageEffect effect, float by)
        {
            switch (effect)
            {
                case PageEffect.Projectiles: page.Projectiles += (int)by; break;
                case PageEffect.Pierce: page.Pierce += (int)by; break;
                case PageEffect.Chains: page.Chains += (int)by; break;
                case PageEffect.Stops: page.Stops = true; break;
                case PageEffect.Rest: page.Cooldown += by; break;
                case PageEffect.Rides: page.Rides = true; break;
                case PageEffect.Grounded: page.Grounded = true; break;
                case PageEffect.Falls: page.Falls = true; break;
                case PageEffect.ToNearest: page.To = Bearing.Nearest; break;
                case PageEffect.ToYou: page.To = Bearing.You; break;
                case PageEffect.ToAnywhere: page.To = Bearing.Anywhere; break;
                case PageEffect.FromCursor when index == 0: page.From = Start.Cursor; break;
                case PageEffect.FromNearest when index == 0: page.From = Start.Nearest; break;
                case PageEffect.FromRandom when index == 0: page.From = Start.Random; break;
            }
        }

        /// <summary>Whether a sticker means anything on a page that's this one of its book, for one that only says where the first starts.</summary>
        public static bool Counts(Sticker sticker, int index) => index == 0 || sticker.Good is not (PageEffect.FromCursor or PageEffect.FromNearest or PageEffect.FromRandom);

        private static void ReadOutcomes(Page written, PagePlan page)
        {
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
                        page.Deals = true;
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

            page.Heals = heals.ToArray();
            page.Ailments = ailments.ToArray();
            page.Gifts = gifts.ToArray();
        }

        private static Tags TagsOf(PagePlan page, int index, bool last, bool wild)
        {
            Tags tags = page.Kind == PageKind.Projectile ? Tags.Projectile : Tags.Area;
            tags |= page.Hand == Hand.Spell ? Tags.Spell : Tags.Attack;
            if (page.Lingers) tags |= Tags.Zone;
            if (page.Chains > 0) tags |= Tags.Chaining;
            if (page.Rides || page.Moves) tags |= Tags.Movement;
            if (page.Gifts.Length > 0) tags |= Tags.Buff;
            if (page.Minion != null) tags |= Tags.Minion;
            if (page.Kind == PageKind.Projectile && page.Falls) tags |= Tags.Falling;
            if (wild) tags |= Tags.Wild;
            tags |= index == 0 ? Tags.Opening : Tags.Turned;
            if (last) tags |= Tags.Closing;
            return tags;
        }
    }
}

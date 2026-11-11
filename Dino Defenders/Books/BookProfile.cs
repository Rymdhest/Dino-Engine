using Dino_Defenders.Stats;

namespace Dino_Defenders.Books
{
    /// <summary>The kinds of thing a page looks like, which is all a client needs to know of what a body is doing.</summary>
    public enum PageForm : byte
    {
        Strike,     // A sweep in front of someone.
        Nova,       // A ring around them.
        Bolt,       // Something let go of.
        Burst,      // Something happening somewhere else.
        Zone,       // Ground that goes on doing something.
        Beam,       // A straight line, at once.
        Buff,       // Something for themselves.
        Summon,     // Others, to fight beside them.
        Leap,       // Going with it.
    }

    /// <summary>
    /// What a page looks and sounds like, in one number, for whoever's watching. It stands in for what a client can't look up, as what's written in someone else's book is none of its business: this is all that crosses the
    /// network about anything anyone is doing. Nothing is zero, as nothing is what says nobody's doing anything.
    /// </summary>
    public readonly record struct PageSign(PageForm Form, DamageType Look, bool Spell, bool Heavy, bool Wide)
    {
        public const int None = 0;

        private const int LookShift = 4;
        private const int SpellBit = 1 << 8;
        private const int HeavyBit = 1 << 9;
        private const int WideBit = 1 << 10;
        private const int Present = 1 << 11;

        public int Number => (int)Form | (int)Look << LookShift | (Spell ? SpellBit : 0) | (Heavy ? HeavyBit : 0) | (Wide ? WideBit : 0) | Present;

        /// <summary>What a number means. Something nobody has heard of looks like a plain blow, rather than bringing anything down.</summary>
        public static PageSign Read(int number)
        {
            int form = number & 15;
            int look = number >> LookShift & 15;
            return new PageSign(form <= (int)PageForm.Leap ? (PageForm)form : PageForm.Strike, look < DamageKinds.Count ? (DamageType)look : DamageType.Physical, (number & SpellBit) != 0, (number & HeavyBit) != 0, (number & WideBit) != 0);
        }

        /// <summary>A burst of something, for what isn't on any page: a body going up.</summary>
        public static PageSign Blast(DamageType look) => new PageSign(PageForm.Nova, look, true, false, false);

        public static PageSign Of(PagePlan page, DamageType look, float useTime) => new PageSign(FormOf(page), look, page.Hand == Hand.Spell, useTime >= Rulebook.HeavyUse, page.Length >= Rulebook.WideReach);

        private static PageForm FormOf(PagePlan page)
        {
            if (page.Kind == PageKind.Summon) return PageForm.Summon;
            if (page.Kind == PageKind.Projectile) return page.Rides ? PageForm.Leap : page.Falls ? PageForm.Burst : PageForm.Bolt;

            bool here = page.From == Start.You;
            if (page.Minion != null) return PageForm.Summon;
            if (page.Lingers) return PageForm.Zone;
            if (here && !page.Hostile) return PageForm.Buff;
            if (page.Shape is AreaShape.Line or AreaShape.Rectangle) return PageForm.Beam;
            if (!here) return PageForm.Burst;
            return page.Shape == AreaShape.Cone ? PageForm.Strike : PageForm.Nova;
        }
    }

    /// <summary>What something's thrown as, for whatever has no way of shooting: a weapon's kind is a short word, so that it can go in a model's name, which is sent as a table that only grows.</summary>
    public static class Thrown
    {
        public const string Prefix = "thrown:";

        public static string Of(Tags weapon)
        {
            if ((weapon & Tags.Axe) != 0) return "Axe";
            if ((weapon & Tags.Mace) != 0) return "Mace";
            if ((weapon & Tags.Dagger) != 0) return "Dagger";
            if ((weapon & Tags.Spear) != 0) return "Spear";
            if ((weapon & Tags.Sword) != 0) return "Sword";
            return "Stone";
        }
    }

    /// <summary>One actor's numbers for one book, which is a page's for each page and what it comes to together. A hero's step reads these, as both ends have them.</summary>
    public sealed class BookProfile
    {
        public readonly ActorSheet Sheet;
        public readonly BookPlan Book;
        public readonly PageProfile[] Pages;

        /// <summary>Every page's added together, for those who pay for every page.</summary>
        public readonly float Cost;
        public readonly bool CostsHealth;

        /// <summary>Paid in gold, <see cref="Rulebook.GoldPerCost"/> for each point of it, instead of mana.</summary>
        public readonly bool CostsGold;

        /// <summary>Using it takes a minion of yours, and it can't be used without one.</summary>
        public readonly bool CostsMinion;
        public readonly float Cooldown;

        /// <summary>
        /// Seconds it takes to use: what its first page takes if that's an Attack Page, which is a weapon's own pace, and what every Spell Page in it takes to cast, all of it more or less by what anything on any of its
        /// pages says about Use Time.
        /// </summary>
        public readonly float UseTime;

        public PageProfile First => Pages[0];
        public float ImpactTime => UseTime * First.Page.ImpactAt;
        public float MoveFactor => First.Page.MoveFactor;
        public bool Rides => First.Page.Rides;
        public PageSign Sign => First.Sign with { Heavy = UseTime >= Rulebook.HeavyUse };

        /// <summary>Whether its first page comes down out of the sky, written that way or lobbed.</summary>
        public bool Lobs => First.Page.Kind == PageKind.Projectile && !First.Page.Rides && (First.Page.Falls || Sheet.Has(Rule.Lobbed));

        /// <summary>How long after it's let go that what's marked lands, for what has a way to fall.</summary>
        public float Delay => Lobs ? Rulebook.SkyHeight / MathF.Max(First.Speed, 1f) : 0f;

        /// <summary>What a Toggle Book's pulse does, of what its pages would by hand.</summary>
        public float AuraStrength => Sheet.Has(Rule.FullVoice) ? 1f : Rulebook.AuraStrength;

        internal BookProfile(ActorSheet sheet, BookPlan book)
        {
            Sheet = sheet;
            Book = book;
            Pages = new PageProfile[book.Pages.Length];
            float useTime = 0f;
            float useScale = 1f;
            float potency = Resting(sheet, book);
            for (int i = 0; i < Pages.Length; i++)
            {
                Pages[i] = new PageProfile(sheet, book.Pages[i], potency);
                if (i == 0 || !sheet.Has(Rule.OneCost)) Cost += Pages[i].Cost;
                Cooldown += Pages[i].Cooldown;
                useTime += Pages[i].UseTime;
                useScale *= book.Pages[i].UseScale;
            }
            UseTime = MathF.Max(useTime * useScale, Rulebook.ShortestUse);
            CostsHealth = sheet.Has(Rule.CostsHealth);
            CostsGold = sheet.Has(Rule.CostsGold);

            // What a book is paid with can be something else altogether. Raising the flock is never paid for with the flock.
            if (Mode != BookMode.Toggle)
            {
                bool raises = Array.Exists(Pages, page => page.Page.Minion != null);
                if (sheet.Has(Rule.TimeIsCost)) Cost += Cooldown * Rulebook.RestToCost;
                if (sheet.Has(Rule.NoCooldowns)) Cooldown = 0f;
                if (sheet.Has(Rule.CostIsTime))
                {
                    Cooldown += Cost * Rulebook.CostToRest;
                    Cost = 0f;
                }
                if (sheet.Has(Rule.FlockPays) && !raises) Cost = 0f;
                CostsMinion = sheet.Has(Rule.FlockBleeds) && !raises;
            }
            if (Mode == BookMode.Toggle) Cost = MathF.Max(Cost, Rulebook.LeastToggleCost);
        }

        public BookMode Mode => Book.Mode;

        // For those whose books are worth more for resting: what every page's Potency is made of the book's whole Cooldown, which nothing has shortened.
        private static float Resting(ActorSheet sheet, BookPlan book)
        {
            if (!sheet.Has(Rule.LongCount)) return 1f;
            float rest = 0f;
            foreach (PagePlan page in book.Pages) rest += page.Cooldown;
            return rest > 0f ? 1f + MathF.Min(rest * Rulebook.LongCountPer, Rulebook.LongCountMost) : 1f - Rulebook.LongCountIdle;
        }

        /// <summary>Seconds between a Toggle Book's pulses. Whatever quickens a use quickens it, and whatever makes a book rest holds it back.</summary>
        public float Pulse => MathF.Max(MathF.Max(Rulebook.AuraPulse * UseTime / Rulebook.PageUseTime, Cooldown), Rulebook.FastestAura);

        /// <summary>Whether anything on it is worth drawing on the ground before it lands. A plain blow isn't: nobody needs telling what a sword reaches, and there'd be no end of them.</summary>
        public bool ShowsArea
        {
            get
            {
                PagePlan page = First.Page;
                if (page.Kind == PageKind.Summon) return false;
                if (!page.Hostile && Pages.Length == 1) return false;
                if (page.From is Start.Nearest or Start.Random) return false;
                if (page.Kind == PageKind.Area) return page.From != Start.You || page.Shape != AreaShape.Cone || First.Length >= Rulebook.WideReach;
                return Lobs || page.Rides;
            }
        }

        /// <summary>What it comes to in a second of use on one target, for a tooltip.</summary>
        public float DamagePerSecond
        {
            get
            {
                float total = 0f;
                foreach (PageProfile page in Pages) total += page.AverageHit * page.PulseShare * page.Pulses;
                return Mode == BookMode.Toggle ? total * AuraStrength / Pulse : total / MathF.Max(UseTime, Cooldown);
            }
        }
    }
}

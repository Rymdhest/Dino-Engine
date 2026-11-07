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

        public static PageSign Of(PagePlan page, bool spell, float useTime) => new PageSign(FormOf(page), page.Look, spell, useTime >= Rulebook.HeavyUse, page.Length >= Rulebook.WideReach);

        private static PageForm FormOf(PagePlan page)
        {
            if (page.Kind == PageKind.Projectile) return page.Rides ? PageForm.Leap : page.To == Bearing.Down ? PageForm.Burst : PageForm.Bolt;

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
        public readonly float Cooldown;

        public PageProfile First => Pages[0];
        public float UseTime => First.UseTime;
        public float ImpactTime => First.ImpactTime;
        public float MoveFactor => First.Page.MoveFactor;
        public bool Rides => First.Page.Rides;
        public PageSign Sign => First.Sign;

        /// <summary>How long after it's let go that what's marked lands, for what has a way to fall.</summary>
        public float Delay => First.Page.Kind == PageKind.Projectile && First.Page.To == Bearing.Down ? Rulebook.SkyHeight / MathF.Max(First.Speed, 1f) : 0f;

        internal BookProfile(ActorSheet sheet, BookPlan book)
        {
            Sheet = sheet;
            Book = book;
            Pages = new PageProfile[book.Pages.Length];
            for (int i = 0; i < Pages.Length; i++)
            {
                Pages[i] = new PageProfile(sheet, book.Pages[i]);
                if (i == 0 || !sheet.Has(Rule.OneCost)) Cost += Pages[i].Cost;
                Cooldown += Pages[i].Cooldown;
            }
            CostsHealth = sheet.Has(Rule.CostsHealth);
        }

        /// <summary>Whether anything on it is worth drawing on the ground before it lands. A plain blow isn't: nobody needs telling what a sword reaches, and there'd be no end of them.</summary>
        public bool ShowsArea
        {
            get
            {
                PagePlan page = First.Page;
                if (!page.Hostile && Pages.Length == 1) return false;
                if (page.From is Start.Nearest or Start.Random) return false;
                if (page.Kind == PageKind.Area) return page.From != Start.You || page.Shape != AreaShape.Cone || First.Length >= Rulebook.WideReach;
                return page.To == Bearing.Down || page.Rides;
            }
        }

        /// <summary>What it comes to in a second of use on one target, for a tooltip.</summary>
        public float DamagePerSecond
        {
            get
            {
                float total = 0f;
                foreach (PageProfile page in Pages) total += page.AverageHit * page.PulseShare * page.Pulses;
                return total / MathF.Max(UseTime, Cooldown);
            }
        }
    }
}

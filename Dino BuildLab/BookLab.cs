using Dino_Defenders.Books;
using Dino_Defenders.Progression;
using Dino_Defenders.Stats;
using Dino_Defenders.Tooltips;

namespace Dino_BuildLab
{
    /// <summary>
    /// What every sentence comes to, written on a plain page in the hands of a plain hero, and what the books on the shelf come to in the same hands. Nobody has played any
    /// of these, so these numbers are only what to start arguing from. A sentence that's far better than its neighbours in its own list stands out here.
    /// </summary>
    internal static class BookLab
    {
        public static int Run(Options options)
        {
            int level = options.Int("level", 30);
            bool bow = options.Text("weapon") == "bow";
            bool wand = options.Text("weapon") == "wand";
            Tags held = bow ? Tags.Bow | Tags.TwoHanded : wand ? Tags.Wand | Tags.OneHanded : Tags.Sword | Tags.OneHanded;
            float damage = 10f + level * 4f;
            var weapon = new WeaponProfile(damage * 0.8f, damage * 1.2f, 0.6f, 5f, held);

            ActorSheet sheet = new Loadout(level, weapon, Array.Empty<Mod>(), Array.Empty<BookPlan>()).SheetFor(Condition.None);
            Console.WriteLine($"Level {level}, {damage:0} a hit from the weapon, nothing else.");

            foreach (PageKind kind in Enum.GetValues<PageKind>())
            {
                Page plain = Scribe.NewPage(kind);
                if (kind == PageKind.Area) plain.Shape = "circle";

                Console.WriteLine($"\n{kind} page, with one plain Outcome");
                Console.WriteLine($"{"",-46}  {"cost",5}  {"hit",6}  {"per sec",7}  {"per cost",8}  {"use",5}  {"reach",6}");
                Row("as it comes", Read(sheet, plain));

                foreach (Blank blank in new[] { Blank.From, Blank.Towards, Blank.Shape, Blank.Timing, Blank.Within, Blank.Clause })
                {
                    foreach (Sentence sentence in Sentences.For(blank))
                    {
                        if (sentence.Fits != null && sentence.Fits != kind) continue;
                        if (sentence.FollowsOnly) continue;
                        if (blank is Blank.Towards && kind == PageKind.Area) continue;
                        if (blank is Blank.Shape or Blank.Timing && kind == PageKind.Projectile) continue;

                        Page page = plain.Copy();
                        Write(page, sentence);
                        Row(sentence.Words, Read(sheet, page));
                    }
                }
            }

            Console.WriteLine("\nOn the shelf");
            Console.WriteLine($"{"",-46}  {"cost",5}  {"hit",6}  {"per sec",7}  {"per cost",8}  {"use",5}  {"pages",6}");
            foreach (BookPlan plan in Shelf.All.OrderBy(plan => plan.Name, StringComparer.Ordinal))
            {
                BookProfile book = sheet.Preview(plan);
                float hit = book.Pages.Sum(page => page.AverageHit * page.PulseShare * page.Pulses);
                Console.WriteLine($"{plan.Name,-46}  {book.Cost,5:0}  {hit,6:0}  {book.DamagePerSecond,7:0}  {PerCost(hit, book),8:0.0}  {book.UseTime,5:0.00}  {book.Pages.Length,6}");
            }
            return 0;
        }

        private static BookProfile Read(ActorSheet sheet, Page page)
        {
            page = page.Copy();
            if (page.Outcomes.Count == 0) page.Outcomes.Add(new Outcome());
            var book = new Book { Pages = { page } };
            Scribe.Fix(book);
            return sheet.Preview(Scribe.Read(book, "Lab"));
        }

        private static void Write(Page page, Sentence sentence)
        {
            switch (sentence.Blank)
            {
                case Blank.From: page.From = sentence.Id; break;
                case Blank.Towards: page.Towards = sentence.Id; break;
                case Blank.Shape: page.Shape = sentence.Id; break;
                case Blank.Timing: page.Timing = sentence.Id; break;
                case Blank.Within: page.FromWithin = sentence.Id; break;
                default: page.Clauses.Add(sentence.Id); break;
            }
        }

        private static float PerCost(float hit, BookProfile book) => book.Cost > 0f ? hit / book.Cost : 0f;

        private static float Reach(BookProfile book) => book.First.Page.Kind == PageKind.Projectile ? book.First.Range : book.First.Length;

        private static void Row(string name, BookProfile book)
        {
            PageProfile page = book.First;
            float hit = page.AverageHit * page.PulseShare * page.Pulses;
            string shown = name.Length > 46 ? name[..43] + "..." : name;
            Console.WriteLine($"{shown,-46}  {book.Cost,5:0.0}  {hit,6:0}  {book.DamagePerSecond,7:0}  {PerCost(hit, book),8:0.0}  {book.UseTime,5:0.00}  {Reach(book),6:0.0}");
        }
    }
}

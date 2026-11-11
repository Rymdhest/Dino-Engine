using Dino_Defenders.Books;
using Dino_Defenders.Progression;
using Dino_Defenders.Stats;
using Dino_Defenders.Tooltips;
using static Dino_Defenders.Stats.M;

namespace Dino_BuildLab
{
    /// <summary>
    /// What every shape, timing and sentence comes to, on a plain Attack or Spell page in the hands of a plain hero, and what the books on the shelf come to in the same hands. Nobody has played any
    /// of these, so these numbers are only what to start arguing from. A phrase that's far better than its neighbours in its own list stands out here.
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

            ActorSheet sheet = new Loadout(level, weapon, new[] { Added(DamageType.Physical, damage, Tags.Spell) }, Array.Empty<BookPlan>()).SheetFor(Condition.None);
            Console.WriteLine($"Level {level}, {damage:0} a hit from the weapon and as much Spell Damage, nothing else.");

            foreach (Hand hand in Enum.GetValues<Hand>())
            {
                foreach (PageKind kind in Enum.GetValues<PageKind>())
                {
                    Page plain = Scribe.NewPage(kind, hand);
                    if (kind == PageKind.Area) plain.Shape = "circle";

                    Console.WriteLine($"\n{hand} {kind} page");
                    Console.WriteLine($"{"",-46}  {"cost",5}  {"hit",6}  {"per sec",7}  {"per cost",8}  {"use",5}  {"reach",6}");
                    Row("as it comes", Read(sheet, plain));

                    foreach (Blank blank in new[] { Blank.Shape, Blank.Timing })
                    {
                        if (kind == PageKind.Projectile) continue;
                        foreach (Phrase phrase in Phrases.For(blank))
                        {
                            Page page = plain.Copy();
                            if (blank == Blank.Shape) page.Shape = phrase.Id;
                            else page.Timing = phrase.Id;
                            Row(phrase.Words, Read(sheet, page));
                        }
                    }

                    foreach (string name in Sentences.Names)
                    {
                        Sentence sentence = Sentences.Written(name)!;
                        if (!Sentences.GoesOn(sentence, kind)) continue;
                        Page page = plain.Copy();
                        page.Sentences.Add(sentence);
                        string bad = BookText.Bad(sentence);
                        Row(bad.Length > 0 ? $"{BookText.Good(sentence)}, {bad}" : BookText.Good(sentence), Read(sheet, page));
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

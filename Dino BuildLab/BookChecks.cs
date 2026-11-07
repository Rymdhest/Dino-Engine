using Dino_Defenders.Books;
using Dino_Defenders.Net;
using Dino_Defenders.Progression;
using Dino_Defenders.Server;
using Dino_Defenders.Stats;
using Dino_Defenders.Tooltips;
using Dino_Engine.Networking;
using System.Text.Json;
using static Dino_Defenders.Stats.M;

namespace Dino_BuildLab
{
    /// <summary>
    /// What's true of any book, however it's written. Anyone can write anything, and it comes in from saves and strangers, so nothing here can trust what it's given: whatever goes in has to come out as something that
    /// can be read, the same on every machine, and every sentence has to be able to say what it does and be worth the page it's on.
    /// </summary>
    internal static class BookChecks
    {
        private static readonly WeaponProfile Sword = new WeaponProfile(20f, 30f, 0.6f, 5f, Tags.Sword | Tags.OneHanded);
        private static readonly WeaponProfile Wand = new WeaponProfile(20f, 30f, 0.6f, 5f, Tags.Wand | Tags.OneHanded);
        private static readonly WeaponProfile Bow = new WeaponProfile(20f, 30f, 0.6f, 5f, Tags.Bow | Tags.TwoHanded);
        private static readonly WeaponProfile Staff = new WeaponProfile(20f, 30f, 0.6f, 5f, Tags.Staff | Tags.TwoHanded);

        public static void Run(Report report)
        {
            report.Section("Books: sentences");
            Words(report);

            report.Section("Books: what can be written");
            Fixing(report);
            Fuzz(report);

            report.Section("Books: what they come to");
            Sums(report);
            Weapons(report);

            report.Section("Books: every skill there was can be written");
            Recipes(report);

            report.Section("Books: carrying them");
            Carrying(report);

            report.Section("Books: saves and the wire");
            Saving(report);
        }

        private static void Words(Report report)
        {
            report.Check("Ids are all different", Sentences.All.Select(s => s.Id).Distinct().Count() == Sentences.All.Count);
            report.Check("Numbers run from one, in order, as that's what's sent", Sentences.All.Select((s, i) => s.Number == i + 1).All(x => x));
            report.Check("Every blank has something to go in it", Enum.GetValues<Blank>().All(blank => Sentences.For(blank).Any()));
            report.Check("Every deed has a way of being said", Enum.GetValues<Deed>().All(deed => Sentences.For(Blank.Deed).Any(s => s.Deed == deed)));
            report.Check("Every Start and every way of going can be chosen", Enum.GetValues<Start>().Where(s => s != Start.Impact).All(start => Sentences.For(Blank.From).Any(s => s.Start == start)) && Enum.GetValues<Bearing>().All(bearing => Sentences.For(Blank.Towards).Any(s => s.Bearing == bearing)));
            report.Check("Every shape can be chosen", Enum.GetValues<AreaShape>().All(shape => Sentences.For(Blank.Shape).Any(s => s.Shape == shape)));
            report.Check("Every type of Damage and every ailment can be chosen", DamageKinds.All.All(type => Sentences.For(Blank.Element).Any(s => s.Element == type)) && Enum.GetValues<Ailment>().All(ailment => Sentences.For(Blank.Ailment).Any(s => s.Ailment == ailment)));
            report.Check("Every measure can be chosen", Enum.GetValues<Measure>().All(measure => Sentences.For(Blank.Measure).Any(s => s.Measure == measure)));
            report.Check("What every plain sentence says exists, in its own blank", new[] { (Sentences.PlainFrom, Blank.From), (Sentences.PlainTowards, Blank.Towards), (Sentences.PlainShape, Blank.Shape), (Sentences.PlainTiming, Blank.Timing), (Sentences.PlainDeed, Blank.Deed), (Sentences.PlainElement, Blank.Element), (Sentences.PlainMeasure, Blank.Measure), (Sentences.PlainAilment, Blank.Ailment), (Sentences.PlainGift, Blank.Gift), (Sentences.PlainKin, Blank.Kin), (Sentences.PlainPool, Blank.Pool), (Sentences.PlainWhom, Blank.Whom) }.All(pair => Sentences.Find(pair.Item1)?.Blank == pair.Item2));

            // Something that's worse for you has to pay for it, and something that's better has to cost. That's what keeps a free hand from being a free lunch.
            var unpaid = Sentences.All.Where(s => s.Wild && s.Weights.Cost >= 0f && s.Weights.Potency <= 0f).Select(s => s.Id).ToList();
            report.Check("Anything left to chance pays for itself", unpaid.Count == 0, string.Join(", ", unpaid));

            var free = Sentences.For(Blank.Clause).Where(s => s.Projectiles > 1 || s.Pierce > 0 || s.Chains > 0 || s.Weights.Speed > 0f || s.Weights.Area > 0f).Where(s => s.Weights.Potency >= 0f && s.Weights.Added <= 0f && s.Weights.Cost <= 0f).Select(s => s.Id).ToList();
            report.Check("Anything that only helps costs something", free.Count == 0, string.Join(", ", free));

            var aimed = Sentences.All.Where(s => s.Blank is Blank.From or Blank.Towards && s.Start == Start.Nearest | s.Bearing == Bearing.Nearest).Where(s => s.Weights.Potency >= 0f).Select(s => s.Id).ToList();
            report.Check("Finding someone by itself takes something off", aimed.Count == 0, string.Join(", ", aimed));

            var rest = Sentences.All.Where(s => s.Cooldown > 0f && s.Weights.Potency <= 0f).Select(s => s.Id).ToList();
            report.Check("Waiting for a book pays", rest.Count == 0, string.Join(", ", rest));

            float lessRest = Sentences.Find("rest")!.Weights.Potency;
            float moreRest = Sentences.Find("rest-long")!.Weights.Potency;
            report.Check("And waiting longer pays more", moreRest > lessRest);
        }

        private static void Fixing(Report report)
        {
            Book empty = new Book();
            Scribe.Fix(empty);
            report.Check("A book with nothing in it gets a plain page", empty.Pages.Count == 1 && empty.Pages[0].Outcomes.Count == 1);

            var nonsense = new Book { Pages = { new Page { Kind = (PageKind)99, From = "no-such", FromWithin = "no-such", Towards = "from-you", Shape = "deal", Timing = "", Clauses = { "no-such", "two-at-once", "two-at-once" }, Outcomes = { new Outcome { Deed = "cone", Element = "cursor" } } } } };
            Scribe.Fix(nonsense);
            Page fixedUp = nonsense.Pages[0];
            report.Check("Nonsense goes back to plain", fixedUp.Kind == PageKind.Area && fixedUp.From == Sentences.PlainFrom && fixedUp.FromWithin == null && fixedUp.Towards == Sentences.PlainTowards && fixedUp.Shape == Sentences.PlainShape && fixedUp.Timing == Sentences.PlainTiming);
            report.Check("Nothing of one blank can sit in another", fixedUp.Outcomes[0].Deed == Sentences.PlainDeed && fixedUp.Outcomes[0].Element == Sentences.PlainElement);
            report.Check("A clause for another kind of page comes out", !fixedUp.Clauses.Contains("two-at-once"));

            var tall = new Book();
            for (int i = 0; i < Rulebook.MostPages + 5; i++) tall.Pages.Add(Scribe.NewPage(PageKind.Projectile));
            Scribe.Fix(tall);
            report.Check($"A book has {Rulebook.MostPages} pages at most", tall.Pages.Count == Rulebook.MostPages);

            var ending = new Book { Pages = { Scribe.NewPage(PageKind.Projectile), Scribe.NewPage(PageKind.Area), Scribe.NewPage(PageKind.Projectile) } };
            Scribe.Fix(ending);
            report.Check("An area can only be the last page", ending.Pages.Count == 2 && ending.Pages[^1].Kind == PageKind.Area);

            Page crowded = Scribe.NewPage(PageKind.Projectile);
            for (int i = 0; i < 9; i++) crowded.Outcomes.Add(new Outcome());
            crowded.Clauses.AddRange(new[] { "two-at-once", "three-at-once", "five-at-once" });
            var book = new Book { Pages = { crowded } };
            Scribe.Fix(book);
            report.Check($"A page has {Rulebook.MostOutcomes} Outcomes at most", crowded.Outcomes.Count == Rulebook.MostOutcomes);
            report.Check("And can't be two at once and three at once", crowded.Clauses.Count == 1 && crowded.Clauses[0] == "two-at-once");

            Page turned = Scribe.NewPage(PageKind.Projectile);
            turned.Clauses.Add("rides");
            var carried = new Book { Pages = { Scribe.NewPage(PageKind.Projectile), turned } };
            carried.Pages[0].Towards = "onward";
            Scribe.Fix(carried);
            report.Check("Nothing carries anyone but the first page", carried.Pages[1].Clauses.Count == 0);
            report.Check("The first page has no way it was going", carried.Pages[0].Towards == Sentences.PlainTowards);

            Page down = Scribe.NewPage(PageKind.Projectile);
            down.Towards = "downwards";
            down.Clauses.Add("rides");
            var fall = new Book { Pages = { down } };
            Scribe.Fix(fall);
            report.Check("Nor does what falls out of the sky", down.Clauses.Count == 0);
        }

        // Anything at all, thrown at the one thing that has to make sense of it.
        private static Book Random(Random random)
        {
            string Any(Blank blank)
            {
                List<Sentence> list = Sentences.For(blank).ToList();
                return random.Next(8) == 0 ? "nonsense" : list[random.Next(list.Count)].Id;
            }

            var book = new Book();
            int pages = random.Next(9);
            for (int i = 0; i < pages; i++)
            {
                var page = new Page
                {
                    Kind = random.Next(3) == 0 ? PageKind.Area : PageKind.Projectile,
                    From = Any(Blank.From),
                    FromWithin = random.Next(3) == 0 ? Any(Blank.Within) : null,
                    Towards = Any(Blank.Towards),
                    TowardsWithin = random.Next(3) == 0 ? Any(Blank.Within) : null,
                    Shape = Any(Blank.Shape),
                    Timing = Any(Blank.Timing),
                };
                int clauses = random.Next(8);
                for (int c = 0; c < clauses; c++) page.Clauses.Add(Any(Blank.Clause));
                int outcomes = random.Next(6);
                for (int o = 0; o < outcomes; o++)
                {
                    page.Outcomes.Add(new Outcome { Deed = Any(Blank.Deed), Element = Any(Blank.Element), Measure = Any(Blank.Measure), Ailment = Any(Blank.Ailment), Gift = Any(Blank.Gift), Kin = Any(Blank.Kin), Pool = Any(Blank.Pool), Whom = Any(Blank.Whom) });
                }
                book.Pages.Add(page);
            }
            return book;
        }

        private static void Fuzz(Report report)
        {
            var random = new Random(41);
            ActorSheet sheet = new Loadout(30, Sword, new[] { Flat(Stat.Health, 500f), Flat(Stat.Mana, 300f), Flat(Stat.Armour, 100f), Flat(Stat.Barrier, 50f), Flat(Stat.Strength, 40f) }, Array.Empty<BookPlan>()).SheetFor(Condition.None);
            ActorSheet spell = new Loadout(30, Wand, new[] { Flat(Stat.Health, 500f), Flat(Stat.Mana, 300f) }, Array.Empty<BookPlan>()).SheetFor(Condition.None);

            int wobbly = 0, bad = 0, unread = 0, unsaid = 0, area = 0;
            float highest = 0f;
            for (int i = 0; i < 6000; i++)
            {
                Book book = Random(random);
                Scribe.Fix(book);
                string once = JsonSerializer.Serialize(book);
                Scribe.Fix(book);
                if (JsonSerializer.Serialize(book) != once) wobbly++;

                if (book.Pages.Count == 0 || book.Pages.Count > Rulebook.MostPages) bad++;
                for (int p = 0; p < book.Pages.Count - 1; p++)
                {
                    if (book.Pages[p].Kind == PageKind.Area) area++;
                }

                BookPlan plan = Scribe.Read(book, "Fuzz");
                foreach (ActorSheet held in new[] { sheet, spell })
                {
                    BookProfile profile = held.Preview(plan);
                    if (!float.IsFinite(profile.Cost) || profile.Cost < 0f || !float.IsFinite(profile.UseTime) || profile.UseTime <= 0f || !float.IsFinite(profile.Cooldown)) unread++;
                    foreach (PageProfile page in profile.Pages)
                    {
                        highest = MathF.Max(highest, page.Potency);
                        if (!float.IsFinite(page.Potency) || page.Potency <= 0f || !float.IsFinite(page.AverageHit) || !float.IsFinite(page.Speed) || page.Speed <= 0f || !float.IsFinite(page.Length) || page.Length < 0f || page.PulseShare <= 0f) unread++;
                    }

                    Card card = Cards.Book(profile);
                    if (card.Lines.Any(line => line.Text.Contains("NaN") || line.Text.Contains("Infinity") || line.Text.Contains("  ") || line.Value.Contains("NaN"))) unsaid++;
                }
            }

            report.Check("Putting a book right twice changes nothing the second time", wobbly == 0, $"{wobbly}");
            report.Check("Six thousand books of anything at all can be read", bad == 0 && area == 0, $"{bad} the wrong length, {area} with an area before the end");
            report.Check("And every one comes to real numbers", unread == 0, $"{unread}");
            report.Check("And says so in real words", unsaid == 0, $"{unsaid}");
            report.Check("Nothing stacks past what a page can have", highest <= Rulebook.MostPotency, $"{highest}");
            report.Note($"The highest Potency anything came to is {Say.Share(highest)}");
        }

        private static Sentence S(string id) => Sentences.Find(id)!;

        private static BookProfile Profile(Book book, WeaponProfile weapon, params Mod[] mods)
        {
            Scribe.Fix(book);
            return new Loadout(1, weapon, mods.Append(Flat(Stat.Health, 100f)), new[] { Scribe.Read(book, "Probe") }).SheetFor(Condition.None).Profile(0);
        }

        private static void Sums(Report report)
        {
            BookProfile plain = Profile(Scribe.Starter(), Sword);
            report.Check("The plain book is a free cone of what the weapon deals", plain.Cost == 0f && plain.First.Page.Shape == AreaShape.Cone && plain.First.Potency == 1f && plain.First.Flows[0].Type == DamageType.Physical);
            report.Near("And reaches what a sword always has", plain.First.Length, Rulebook.ConeReach);

            Page bolt = Scribe.NewPage(PageKind.Projectile);
            BookProfile flying = Profile(new Book { Pages = { bolt } }, Sword);
            report.Near("A projectile costs something, for flying", flying.Cost, Rulebook.ProjectileCost);

            Page wild = Scribe.NewPage(PageKind.Projectile);
            wild.Towards = "anywhere";
            BookProfile random = Profile(new Book { Pages = { wild } }, Sword);
            report.Near("A sentence's Cost is what it says", random.Cost, Rulebook.ProjectileCost * (1f + S("anywhere").Weights.Cost / 100f));
            report.Near("And so is its Potency", random.First.Potency, 1f + S("anywhere").Weights.Potency / 100f);

            Page twice = Scribe.NewPage(PageKind.Projectile);
            twice.Towards = "anywhere";
            twice.TowardsWithin = "within-far";
            twice.Clauses.Add("short");
            float each = (1f + S("anywhere").Weights.Potency / 100f) * (1f + S("within-far").Weights.Potency / 100f) * (1f + S("short").Weights.Potency / 100f);
            report.Near("Each is a multiplier of its own, as more and less always are", Profile(new Book { Pages = { twice } }, Sword).First.Potency, each);

            var pages = new Book { Pages = { Scribe.NewPage(PageKind.Projectile), Scribe.NewPage(PageKind.Projectile), Scribe.NewPage(PageKind.Area) } };
            BookProfile three = Profile(pages, Sword);
            report.Near("A book's Cost is every page's added up", three.Cost, three.Pages.Sum(page => page.Cost));
            report.Check("And that's more than any of them", three.Cost > three.Pages.Max(page => page.Cost));
            report.Near("Unless a book only pays for its first", Profile(new Book { Pages = { Scribe.NewPage(PageKind.Projectile), Scribe.NewPage(PageKind.Projectile) } }, Sword, Rules(Rule.OneCost)).Cost, Rulebook.ProjectileCost);

            Page more = Scribe.NewPage(PageKind.Area);
            more.Outcomes.Add(new Outcome { Deed = "push" });
            report.Near("An Outcome past the first is a thing more to pay for", Profile(new Book { Pages = { more } }, Sword).Cost, Rulebook.OutcomeCost + S("push").Weights.Added);

            float hit = plain.First.AverageHit;
            Page split = Scribe.NewPage(PageKind.Area);
            split.Outcomes.Add(new Outcome { Element = "fire" });
            BookProfile both = Profile(new Book { Pages = { split } }, Sword);
            report.Near("Two lots of Damage on one page are one Hit of both", both.First.AverageHit, hit * 2f, 0.01f);
            report.Check("Of each type", both.First.Flows.Length == 2);

            Page health = Scribe.NewPage(PageKind.Area);
            health.Outcomes[0].Measure = "of-health";
            report.Near("Damage can be worked out from anything", Profile(new Book { Pages = { health } }, Sword).First.Flows[0].Max, 100f * S("of-health").Share);

            Page heal = Scribe.NewPage(PageKind.Area);
            heal.Outcomes[0] = new Outcome { Deed = "heal", Measure = "of-mana" };
            BookProfile healing = Profile(new Book { Pages = { heal } }, Sword, Flat(Stat.Mana, 200f));
            report.Near("Healing is what it says, of whatever it says", healing.First.Heals[0], 200f * S("of-mana").Share);
            report.Check("And isn't a blow", !healing.First.Page.Hostile && !healing.First.DealsDamage);

            Page once = Scribe.NewPage(PageKind.Area);
            Page lasting = Scribe.NewPage(PageKind.Area);
            lasting.Timing = "lingering";
            BookProfile laid = Profile(new Book { Pages = { lasting } }, Sword);
            report.Near("What lingers does what it says over its whole length, and a premium for being there to be left", laid.First.PulseShare * laid.First.Pulses, Rulebook.LingerPremium);
            report.Check("In pulses", laid.First.Pulses > 1);

            Page rested = Scribe.NewPage(PageKind.Area);
            rested.Clauses.Add("rest");
            BookProfile resting = Profile(new Book { Pages = { rested } }, Sword);
            report.Near("A book that has to rest does", resting.Cooldown, Rulebook.RestShort);
            report.Near("And is stronger for it", resting.First.Potency, 1f + S("rest").Weights.Potency / 100f);

            BookProfile tagged = Profile(new Book { Pages = { Scribe.NewPage(PageKind.Projectile), Scribe.NewPage(PageKind.Projectile), Scribe.NewPage(PageKind.Area) } }, Sword, More(Stat.Potency, 100f, Tags.Closing), More(Stat.Potency, 50f, Tags.Turned));
            report.Near("Potency can be for where a page is in a book: the first gets neither", tagged.Pages[0].Potency, 1f);
            report.Near("A middle page is only Turned", tagged.Pages[1].Potency, 1.5f);
            report.Near("And the last is both", tagged.Pages[2].Potency, 3f);

            BookProfile single = Profile(Scribe.Starter(), Sword, More(Stat.Potency, 100f, Tags.Closing), More(Stat.Potency, 100f, Tags.Opening));
            report.Near("A book of one page is its first and its last", single.First.Potency, 4f);
            report.Near("Potency has a ceiling, whatever's stacked on it", Profile(Scribe.Starter(), Sword, More(Stat.Potency, 100000f)).First.Potency, Rulebook.MostPotency);
        }

        private static void Weapons(Report report)
        {
            Book bolt = new Book { Pages = { Scribe.NewPage(PageKind.Projectile) } };
            Book cone = Scribe.Starter();

            report.Check("A sword makes an Attack", Profile(cone, Sword).First.IsAttack);
            report.Check("Which is Melee by hand", (Profile(cone, Sword).First.Tags & Tags.Melee) != 0);
            report.Check("And not when it's thrown", (Profile(bolt, Sword).First.Tags & Tags.Melee) == 0 && (Profile(bolt, Sword).First.Tags & Tags.Projectile) != 0);
            Book away = Scribe.Starter();
            away.Pages[0].From = "from-cursor";
            report.Check("Nor when it happens somewhere other than where they stand", (Profile(away, Sword).First.Tags & Tags.Melee) == 0);
            report.Check("A wand makes a Spell", !Profile(cone, Wand).First.IsAttack && (Profile(cone, Wand).First.Tags & Tags.Spell) != 0);
            report.Check("And so does a staff", !Profile(cone, Staff).First.IsAttack && (Profile(cone, Staff).First.Tags & Tags.Spell) != 0);
            report.Check("A bow makes an Attack that's no sort of Melee", Profile(cone, Bow).First.IsAttack && (Profile(cone, Bow).First.Tags & Tags.Melee) == 0);

            report.Near("Attack Speed hurries an Attack", Profile(cone, Sword, Inc(Stat.AttackSpeed, 100f)).UseTime, Profile(cone, Sword).UseTime / 2f);
            report.Near("And not a Spell", Profile(cone, Wand, Inc(Stat.AttackSpeed, 100f)).UseTime, Profile(cone, Wand).UseTime);
            report.Near("Cast Speed hurries a Spell", Profile(cone, Wand, Inc(Stat.CastSpeed, 100f)).UseTime, Profile(cone, Wand).UseTime / 2f);

            var slow = new WeaponProfile(20f, 30f, WeaponProfile.BareTime * 2f, 5f, Tags.Sword);
            report.Near("And what's in the hands sets the pace", Profile(cone, slow).UseTime, Rulebook.PageUseTime * 2f);

            report.Near("Melee Reach only reaches Melee", Profile(cone, Sword, Flat(Stat.MeleeReach, 2f)).First.Length, Rulebook.ConeReach + 2f);
            report.Near("A Spell's Cone stays as it is", Profile(cone, Wand, Flat(Stat.MeleeReach, 2f)).First.Length, Rulebook.ConeReach);
            report.Near("Area of Effect scales the space, so a side by its root", Profile(cone, Sword, Inc(Stat.AreaOfEffect, 300f)).First.Length, Rulebook.ConeReach * 2f, 0.01f);
        }

        // The goal is that nothing written by hand is needed. So here are the things every ARPG has, written down, and they have to come to what they should.
        private static void Recipes(Report report)
        {
            BookProfile Of(BookPlan plan) => new Loadout(20, Sword, new[] { Flat(Stat.Health, 100f), Flat(Stat.Mana, 100f) }, new[] { plan }).SheetFor(Condition.None).Profile(0);

            report.Check("A plain swing", Of(Shelf.Strike).First.Page is { Kind: PageKind.Area, Shape: AreaShape.Cone, From: Start.You });
            report.Check("A swing all round", Of(Shelf.Whirl).Sign.Form == PageForm.Nova);
            report.Check("An arrow", Of(Shelf.Shot).Sign.Form == PageForm.Bolt);
            report.Check("A fan of arrows", Of(Shelf.Volley).First.Projectiles == 5);
            report.Check("A rain of them", Of(Shelf.RainOfArrows).First is { Projectiles: 5, Page: { To: Bearing.Down, From: Start.Cursor } });
            report.Check("A fireball that bursts", Of(Shelf.Fireball).Pages.Length == 2 && !Of(Shelf.Fireball).First.Page.Hostile && Of(Shelf.Fireball).Pages[1].Page.Kind == PageKind.Area);
            report.Check("A meteor", Of(Shelf.Meteor).First.Page.To == Bearing.Down && Of(Shelf.Meteor).Delay > 0f);
            report.Check("Something that comes back", Of(Shelf.Boomerang).Pages[1].Page.To == Bearing.You);
            report.Check("Chain lightning", Of(Shelf.ChainLightning).Pages.All(page => page.Page.To == Bearing.Nearest));
            report.Check("Something that splits", Of(Shelf.Split).Pages[1] is { Projectiles: 3, Page.To: Bearing.Onward });
            report.Check("A charge", Of(Shelf.Charge).Rides && Of(Shelf.Charge).Sign.Form == PageForm.Leap);
            report.Check("A blink", Of(Shelf.Blink).First.Page is { Moves: true, From: Start.Cursor });
            report.Check("A lance", Of(Shelf.Lance).Sign.Form == PageForm.Beam);
            report.Check("Ground that keeps striking", Of(Shelf.Storm).Sign.Form == PageForm.Zone);
            report.Check("A heal", Of(Shelf.Mend).First.Heals.Length == 1);
            report.Check("A buff", Of(Shelf.StoneSkin).Sign.Form == PageForm.Buff && Of(Shelf.StoneSkin).Cooldown > 0f);
            report.Check("A summon", Of(Shelf.RaiseBones).Sign.Form == PageForm.Summon && Of(Shelf.RaiseBones).First.MinionCount == 2);
        }

        private static Item Book(string name) => new Item { Name = name, Slot = ItemSlot.Book, Level = 1, Seed = 9, Book = Scribe.Starter() };

        private static void Carrying(Report report)
        {
            var hero = new HeroState();
            Item first = Book("First"), second = Book("Second");
            hero.Pickup(first, 3);
            hero.Pickup(second, 5);
            Item sword = new Item { Name = "Sword", Slot = ItemSlot.MainHand, Level = 1, WeaponDamage = 5f };
            hero.Pickup(sword, 7);

            report.Check("A book goes on the bar", hero.Move(ItemPlace.Cell(3), ItemPlace.OnBar(2)) && hero.Bar[2] == first && hero.Inventory[3] == null);
            report.Check("And nothing else does", !hero.CanMove(ItemPlace.Cell(7), ItemPlace.OnBar(1)) && hero.Bar[1] == null);
            report.Check("Two on the bar change places", hero.Move(ItemPlace.Cell(5), ItemPlace.OnBar(1)) && hero.Move(ItemPlace.OnBar(1), ItemPlace.OnBar(2)) && hero.Bar[2] == second && hero.Bar[1] == first);
            report.Check("One from the pack swaps with what's there", hero.Move(ItemPlace.OnBar(1), ItemPlace.Cell(3)) && hero.Pickup(Book("Third"), 4) && hero.Move(ItemPlace.Cell(4), ItemPlace.OnBar(2)) && hero.Inventory[4] == second);
            report.Check("Something that isn't a book can't be swapped onto the bar by the way back", !hero.CanMove(ItemPlace.OnBar(2), ItemPlace.Cell(7)));
            report.Check("Nothing that's on the bar is carried", !hero.Carried().Contains(hero.Bar[2]!));
            report.Check("A book can't be worn", !hero.Equip(4) && !hero.CanMove(ItemPlace.Cell(4), ItemPlace.Worn(WornSlot.Finger2)) && !hero.CanMove(ItemPlace.OnBar(2), ItemPlace.Worn(WornSlot.Finger2)));
            report.Check("Nor can a ring go in its place by being moved there", WornSlots.For(ItemSlot.Book).Count == 0);
            report.Check("A book is set beside nothing, as hovering over one has to ask", HeroState.CreateNew().WornBeside(Book("Hovered")) == null);
            report.Check("And nothing comes off for it", !HeroState.CreateNew().WornInPlaceOf(Book("Hovered")).Any());
            report.Check("It can be taken off the bar for good", hero.Take(ItemPlace.OnBar(2)) != null && hero.Bar[2] == null);

            var writer = new HeroState();
            writer.Bar[0] = ItemGenerator.Book(1);
            Book written = new Book { Pages = { Scribe.NewPage(PageKind.Projectile), Scribe.NewPage(PageKind.Area) } };
            report.Check("A book can be written in", writer.Write(ItemPlace.OnBar(0), written, " Fireball ") && writer.Bar[0]!.Book!.Pages.Count == 2);
            report.Check("And called something", writer.Bar[0]!.Name == "Fireball");
            report.Check("Without what's written staying tied to what wrote it", !ReferenceEquals(writer.Bar[0]!.Book, written));
            report.Check("A name can't go on for ever", writer.Write(ItemPlace.OnBar(0), written, new string('x', 400)) && writer.Bar[0]!.Name.Length == HeroState.LongestBookName);
            report.Check("Leaving it blank leaves what it was", writer.Write(ItemPlace.OnBar(0), written, "   ") && writer.Bar[0]!.Name.Length == HeroState.LongestBookName);
            report.Check("Nothing but a book can be written in", !writer.Write(ItemPlace.Cell(0), written, "") && !writer.Write(ItemPlace.Worn(WornSlot.Body), written, ""));

            Book wrong = new Book();
            writer.Write(ItemPlace.OnBar(0), wrong, "");
            report.Check("And what's written is put right as it goes in", writer.Bar[0]!.Book!.Pages.Count == 1);

            HeroState fresh = HeroState.CreateNew();
            report.Check("Everyone starts with a book on the first place of the bar", fresh.Bar[0] is { IsBook: true } && fresh.Bar.Skip(1).All(item => item == null));
            report.Check("And the kit that comes of it has it there", new Dino_Defenders.Gameplay.HeroKit(fresh, HeroBuild.Compile(fresh).SheetFor(Condition.None)).Slots[0] != null);

            HeroState gaps = HeroState.CreateNew();
            gaps.Bar[4] = gaps.Bar[0];
            gaps.Bar[0] = null;
            var kit = new Dino_Defenders.Gameplay.HeroKit(gaps, HeroBuild.Compile(gaps).SheetFor(Condition.None));
            report.Check("A gap on the bar is a gap", kit.Slots[0] == null && kit.Slots[4] != null);

            report.Check("A book is worth no gold, as anyone can write one", ItemGenerator.Book(1).SellValue == 0);
            var shop = new MerchantTable();
            var seller = new HeroState { Gold = 5 };
            seller.Bar[0] = ItemGenerator.Book(1);
            report.Check("Selling what's on the bar takes it off", shop.Sell(seller, ItemPlace.OnBar(0)) && seller.Bar[0] == null && seller.Gold == 5);

            var library = new HeroState();
            library.Bar[1] = Book("On the bar");
            library.Pickup(Book("In the pack"));
            library.Pickup(sword);
            report.Check("Every book can be found, the bar first", library.BookPlaces().Select(place => library.At(place)!.Name).SequenceEqual(new[] { "On the bar", "In the pack" }));
            report.Check("And a new one goes in the pack", library.NewBook(3) && library.Carried().Count(item => item.IsBook) == 2);

            var full = new HeroState();
            for (int i = 0; i < HeroState.InventorySize; i++) full.Pickup(Book("Book"));
            report.Check("Unless there's no room", !full.NewBook(3));
        }

        private const string BeforeBooks = """
            {
              "Version": 5,
              "Hero": {
                "Level": 12, "Experience": 5, "Gold": 40, "Flasks": 2, "WorldsCleared": 1,
                "Talents": [ "pyre.t0", "pyre.firebolt", "key.slow-burn" ],
                "Bar": [ "strike", "firebolt", null, "whirl", null, null, "no-such-skill" ],
                "Inventory": [ { "Name": "Sturdy Hood", "Slot": "Head", "Rarity": "Magic", "Level": 2, "Armour": 3, "Lines": [ { "Affix": "armour", "Value": 4 }, { "Affix": "skill-level", "Value": 1 } ] } ],
                "Equipped": { "MainHand": { "Name": "Rusted Blade", "Slot": "MainHand", "Rarity": "Common", "Level": 1, "WeaponDamage": 7, "Lines": [] } }
              },
              "WorldSeed": 7, "WorldLevel": 3
            }
            """;

        private static void Saving(Report report)
        {
            SaveGame? save = SaveGame.Read(BeforeBooks);
            report.Check("A save from before books loads", save != null);
            if (save == null) return;

            HeroState hero = save.Hero;
            report.Check("With the hero as they were", hero.Level == 12 && hero.Gold == 40 && hero.WorldsCleared == 1 && save.WorldSeed == 7);
            report.Check("Their tree has gone, every point there to spend again", hero.Talents.Count == 0 && hero.UnspentPoints == hero.TalentPoints, $"{hero.Talents.Count}, {hero.UnspentPoints}");
            report.Check("What they carry stays", hero.Inventory[0]?.Name == "Sturdy Hood" && hero.Equipped[WornSlot.MainHand].WeaponDamage == 7f);
            report.Check("And a line that means nothing now costs only that line", hero.Inventory[0]!.Mods().Any());
            report.Check("They've a book on the first place of the bar", hero.Bar[0] is { IsBook: true } && hero.Bar.Length == HeroState.BarSlots && hero.Bar.Skip(1).All(item => item == null));
            report.Check("And can fight with it", HeroBuild.Compile(hero).Books.Length == 1);

            string path = Path.Combine(Path.GetTempPath(), $"buildlab-books-{Environment.ProcessId}.json");
            File.WriteAllText(path, BeforeBooks);
            SaveGame.PathOverride = path;
            SaveGame? loaded = SaveGame.Load();
            bool kept = File.Exists(path + ".before-books") && File.ReadAllText(path + ".before-books") == BeforeBooks;
            loaded?.Store();
            SaveGame? again = SaveGame.Load();
            File.Delete(path + ".before-books");
            File.Delete(path);
            SaveGame.PathOverride = null;
            report.Check("A copy of the old save is kept, as nothing brings a tree back", kept);
            report.Check("And a book comes through being saved and loaded", again?.Hero.Bar[0]?.Book is { Pages.Count: 1 });

            string edited = JsonSerializer.Serialize(new { Version = SaveGame.CurrentVersion, Hero = new { Level = 5, Bar = new object?[] { new { Name = "Bad", Slot = "Book", Book = new { Pages = new object[] { new { From = "no-such", Outcomes = Array.Empty<object>() } } } }, new { Name = "Sword", Slot = "MainHand" } } } });
            SaveGame? hand = SaveGame.Read(edited);
            report.Check("A book edited by hand is put right", hand?.Hero.Bar[0]?.Book?.Pages[0].From == Sentences.PlainFrom && hand.Hero.Bar[0]!.Book!.Pages[0].Outcomes.Count == 1);
            report.Check("And a sword can't be left on the bar", hand?.Hero.Bar[1] == null);

            var writer = new BitWriter();
            Book meteor = Shelf.Meteor.Source!;
            Protocol.WriteBook(writer, meteor);
            Book back = Protocol.ReadBook(new BitReader(writer.ToArray()));
            report.Check("A book comes through the wire exactly", JsonSerializer.Serialize(back) == JsonSerializer.Serialize(meteor));
            report.Note($"A book of two pages is {writer.ByteLength} bytes");

            var forged = new BitWriter();
            forged.WriteCount(Rulebook.MostPages + 1);
            bool refused = false;
            try { Protocol.ReadBook(new BitReader(forged.ToArray())); }
            catch (NetReadException) { refused = true; }
            report.Check("One longer than a book can be is refused at the door", refused);

            var strange = new BitWriter();
            strange.WriteCount(1);
            strange.WriteByte((byte)PageKind.Projectile);
            for (int i = 0; i < 6; i++) strange.WriteVarInt(99999);
            strange.WriteCount(1);
            strange.WriteVarInt(99999);
            strange.WriteCount(1);
            for (int i = 0; i < 8; i++) strange.WriteVarInt(99999);
            Book read = Protocol.ReadBook(new BitReader(strange.ToArray()));
            report.Check("And sentences nobody has heard of are put right, whatever a stranger sends", read.Pages[0].From == Sentences.PlainFrom && read.Pages[0].Clauses.Count == 0 && read.Pages[0].Outcomes[0].Deed == Sentences.PlainDeed);

            var empty = new Item { Name = "Empty", Slot = ItemSlot.Book };
            var item = new BitWriter();
            Protocol.WriteItem(item, empty);
            report.Check("Something sent as a book with nothing in it arrives with something in it", Protocol.ReadItem(new BitReader(item.ToArray())).Book != null);

            var plain = new Item { Name = "Sword", Slot = ItemSlot.MainHand, Book = Scribe.Starter() };
            var armed = new BitWriter();
            Protocol.WriteItem(armed, plain);
            report.Check("And a sword with pages in it arrives as a sword", Protocol.ReadItem(new BitReader(armed.ToArray())).Book == null);

            var onBar = new BitWriter();
            onBar.WriteCount(0);
            onBar.WriteCount(1);
            onBar.WriteBool(true);
            Protocol.WriteItem(onBar, plain);
            onBar.WriteCount(0);
            bool stopped = false;
            try { Protocol.ReadGear(new BitReader(onBar.ToArray()), new HeroState()); }
            catch (NetReadException) { stopped = true; }
            report.Check("A sword sent as something on the bar is refused", stopped);
        }
    }
}

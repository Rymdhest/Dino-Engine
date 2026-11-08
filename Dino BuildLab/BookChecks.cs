using Dino_Defenders.Books;
using Dino_Defenders.Client;
using Dino_Defenders.Net;
using Dino_Defenders.Progression;
using Dino_Defenders.Server;
using Dino_Defenders.Stats;
using Dino_Defenders.Tooltips;
using Dino_Engine.Networking;
using Dino_Engine.Rendering;
using OpenTK.Windowing.GraphicsLibraryFramework;
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
            Rostered(report);
            Keyed(report);
            Stickered(report);
            Moded(report);
            Carried(report);
            Slotting(report);

            report.Section("Books: saves and the wire");
            Saving(report);
        }

        private static void Words(Report report)
        {
            report.Check("Ids are all different", Sentences.All.Select(s => s.Id).Distinct().Count() == Sentences.All.Count);
            report.Check("Numbers run from one, in order, as that's what's sent", Sentences.All.Select((s, i) => s.Number == i + 1).All(x => x));
            report.Check("Every blank has something to go in it", Enum.GetValues<Blank>().All(blank => Sentences.For(blank).Any()));
            report.Check("Every deed has a way of being said", Enum.GetValues<Deed>().All(deed => Sentences.For(Blank.Deed).Any(s => s.Deed == deed)));
            report.Check("Every shape can be found", Enum.GetValues<AreaShape>().All(shape => Sentences.For(Blank.Shape).Any(s => s.Shape == shape && s.Found > 0f)));
            report.Check("Every ailment can be written", Enum.GetValues<Ailment>().All(ailment => Sentences.For(Blank.Ailment).Any(s => s.Ailment == ailment)));
            report.Check("Every measure can be written", Enum.GetValues<Measure>().All(measure => Sentences.For(Blank.Measure).Any(s => s.Measure == measure)));
            report.Check("What every plain sentence says exists, in its own blank", new[] { (Sentences.PlainShape, Blank.Shape), (Sentences.PlainTiming, Blank.Timing), (Sentences.PlainDeed, Blank.Deed), (Sentences.PlainMeasure, Blank.Measure), (Sentences.PlainAilment, Blank.Ailment), (Sentences.PlainGift, Blank.Gift), (Sentences.PlainKin, Blank.Kin), (Sentences.PlainPool, Blank.Pool), (Sentences.PlainWhom, Blank.Whom) }.All(pair => Sentences.Find(pair.Item1)?.Blank == pair.Item2));

            // A found page deals damage, nearly always. What else turns up is what a person asked to keep: raising and moving.
            var found = Sentences.For(Blank.Deed).Where(s => s.Found > 0f).Select(s => s.Deed).ToHashSet();
            report.Check("What a found page can do is deal, raise or move", found.SetEquals(new[] { Deed.Deal, Deed.Raise, Deed.Move }), string.Join(", ", found));
            float deal = Sentences.Find(Sentences.PlainDeed)!.Found;
            report.Check("And the last two are rare", Sentences.For(Blank.Deed).Where(s => s.Found > 0f && s.Deed != Deed.Deal).All(s => s.Found * 5f < deal));
            report.Check("Waiting for a book pays, and waiting longer pays more", Stickers.Written("rest")!.Good == PageEffect.Potency && Stickers.Written("rest-long")!.GoodBy > Stickers.Written("rest")!.GoodBy);
        }

        private static void Fixing(Report report)
        {
            Book empty = new Book();
            Scribe.Fix(empty);
            report.Check("A book with nothing in it gets a plain page", empty.Pages.Count == 1 && empty.Pages[0].Outcomes.Count == 1);

            var nonsense = new Book { Pages = { new Page { Kind = (PageKind)99, Hand = (Hand)77, Shape = "deal", Timing = "", Stickers = { Stickers.Written("two-at-once")! }, Outcomes = { new Outcome { Deed = "cone", Element = "fire" } } } } };
            Scribe.Fix(nonsense);
            Page fixedUp = nonsense.Pages[0];
            report.Check("Nonsense goes back to plain", fixedUp.Kind == PageKind.Area && fixedUp.Hand == Hand.Attack && fixedUp.Shape == Sentences.PlainShape && fixedUp.Timing == Sentences.PlainTiming);
            report.Check("Nothing of one blank can sit in another", fixedUp.Outcomes[0].Deed == Sentences.PlainDeed && fixedUp.Outcomes[0].Element == null);
            report.Check("A sticker for another kind of page comes out", fixedUp.Stickers.Count == 0);

            var tall = new Book();
            for (int i = 0; i < Rulebook.MostPages + 5; i++) tall.Pages.Add(Scribe.NewPage(PageKind.Projectile));
            Scribe.Fix(tall);
            report.Check($"A book has {Rulebook.MostPages} pages at most", tall.Pages.Count == Rulebook.MostPages);

            var ending = new Book { Pages = { Scribe.NewPage(PageKind.Projectile), Scribe.NewPage(PageKind.Area), Scribe.NewPage(PageKind.Projectile) } };
            Scribe.Fix(ending);
            report.Check("An area can only be the last page", ending.Pages.Count == 2 && ending.Pages[^1].Kind == PageKind.Area);

            Page crowded = Scribe.NewPage(PageKind.Projectile);
            for (int i = 0; i < 9; i++) crowded.Outcomes.Add(new Outcome());
            for (int i = 0; i < Rulebook.MostStickers + 3; i++) crowded.Stickers.Add(Stickers.Written("swift")!);
            var book = new Book { Pages = { crowded } };
            Scribe.Fix(book);
            report.Check("A page does one thing", crowded.Outcomes.Count == 1);
            report.Check($"And has {Rulebook.MostStickers} Stickers at most", crowded.Stickers.Count == Rulebook.MostStickers);

            // Nobody writes a page, so anything a sentence can say stays: a book that's already written and copied out has to come through the wire and a save as it is.
            Page writtenHeal = Scribe.NewPage(PageKind.Area, Hand.Attack);
            writtenHeal.Outcomes[0].Deed = "heal";
            Scribe.Fix(writtenHeal);
            report.Check("Whatever a sentence can say stays said, as it was", writtenHeal.Outcomes[0].Deed == "heal" && writtenHeal.Hand == Hand.Attack);

            Page turned = Scribe.NewPage(PageKind.Projectile);
            turned.Stickers.Add(Stickers.Written("rides")!);
            var carried = new Book { Pages = { Scribe.NewPage(PageKind.Projectile), turned } };
            Scribe.Fix(carried);
            report.Check("Nothing carries anyone but the first page", !Scribe.Read(carried).Pages[1].Rides);

            Page down = Scribe.NewPage(PageKind.Projectile);
            down.Stickers.Add(Stickers.Written("falls")!);
            down.Stickers.Add(Stickers.Written("rides")!);
            var fall = new Book { Pages = { down } };
            Scribe.Fix(fall);
            report.Check("Nor does what falls out of the sky", !Scribe.Read(fall).First.Rides);
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
                    Hand = (Hand)random.Next(3),
                    Shape = Any(Blank.Shape),
                    Timing = Any(Blank.Timing),
                };
                int stickers = random.Next(8);
                for (int c = 0; c < stickers; c++) page.Stickers.Add(Stickers.Roll((Dino_Defenders.Progression.Rarity)random.Next(4), new Dino_Engine.World.WorldRandom((ulong)random.Next())));
                int outcomes = random.Next(6);
                for (int o = 0; o < outcomes; o++)
                {
                    page.Outcomes.Add(new Outcome { Deed = Any(Blank.Deed), Measure = Any(Blank.Measure), Ailment = Any(Blank.Ailment), Gift = Any(Blank.Gift), Kin = Any(Blank.Kin), Pool = Any(Blank.Pool), Whom = Any(Blank.Whom) });
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
            BookProfile plain = Profile(Starter(), Sword);
            report.Check("The plain book is a free cone of what the weapon deals", plain.Cost == 0f && plain.First.Page.Shape == AreaShape.Cone && plain.First.Potency == 1f && plain.First.Flows[0].Type == DamageType.Physical);
            report.Near("And reaches what a sword always has", plain.First.Length, Rulebook.ConeReach);

            Page bolt = Scribe.NewPage(PageKind.Projectile);
            BookProfile flying = Profile(new Book { Pages = { bolt } }, Sword);
            report.Near("A projectile costs something, for flying", flying.Cost, Rulebook.ProjectileCost);
            report.Check("And goes from you towards the cursor", flying.First.Page is { From: Start.You, To: Bearing.Cursor });

            var chain = new Book { Pages = { Scribe.NewPage(PageKind.Projectile), Scribe.NewPage(PageKind.Projectile), Scribe.NewPage(PageKind.Area) } };
            BookPlan chained = Scribe.Read(chain);
            report.Check("Every page after the first goes on from where the last struck, the way it was going", chained.Pages.Skip(1).All(page => page.From == Start.Impact) && chained.Pages[1].To == Bearing.Onward);

            Page wild = Scribe.NewPage(PageKind.Projectile);
            wild.Stickers.Add(Stickers.Written("anywhere")!);
            BookProfile random = Profile(new Book { Pages = { wild } }, Sword);
            Weights anywhere = Stickers.Weighting(PageEffect.ToAnywhere);
            report.Check("What's left to chance pays for itself", anywhere.Potency > 0f && anywhere.Cost < 0f && Stickers.Weighting(PageEffect.FromRandom).Potency > 0f);
            report.Near("And is what it says, Cost", random.Cost, Rulebook.ProjectileCost * (1f + anywhere.Cost / 100f));
            report.Near("And Potency", random.First.Potency, 1f + anywhere.Potency / 100f);
            report.Check("And counts as Wild", (random.First.Tags & Tags.Wild) != 0 && (flying.First.Tags & Tags.Wild) == 0);

            Page twice = Scribe.NewPage(PageKind.Projectile);
            twice.Stickers.Add(Stickers.Written("anywhere")!);
            twice.Stickers.Add(Stickers.Written("slow")!);
            float each = (1f + anywhere.Potency / 100f) * (1f + Stickers.Written("slow")!.GoodBy / 100f);
            report.Near("Each is a multiplier of its own, as more and less always are", Profile(new Book { Pages = { twice } }, Sword).First.Potency, each);

            BookProfile three = Profile(chain, Sword);
            report.Near("A book's Cost is every page's added up", three.Cost, three.Pages.Sum(page => page.Cost));
            report.Check("And that's more than any of them", three.Cost > three.Pages.Max(page => page.Cost));
            report.Near("Unless a book only pays for its first", Profile(new Book { Pages = { Scribe.NewPage(PageKind.Projectile), Scribe.NewPage(PageKind.Projectile) } }, Sword, Rules(Rule.OneCost)).Cost, Rulebook.ProjectileCost);

            Page heal = Scribe.NewPage(PageKind.Area);
            heal.Outcomes[0] = new Outcome { Deed = "heal", Measure = "of-mana" };
            BookProfile healing = Profile(new Book { Pages = { heal } }, Sword, Flat(Stat.Mana, 200f));
            report.Near("A written heal is what it says, of whatever it says", healing.First.Heals[0], 200f * S("of-mana").Share);
            report.Check("And isn't a blow", !healing.First.Page.Hostile && !healing.First.DealsDamage);

            Page lasting = Scribe.NewPage(PageKind.Area);
            lasting.Timing = "lingering";
            BookProfile laid = Profile(new Book { Pages = { lasting } }, Sword);
            report.Near("What lingers does what it says over its whole length, and a premium for being there to be left", laid.First.PulseShare * laid.First.Pulses, Rulebook.LingerPremium);
            report.Check("In pulses", laid.First.Pulses > 1);

            Page rested = Scribe.NewPage(PageKind.Area);
            rested.Stickers.Add(Stickers.Written("rest")!);
            BookProfile resting = Profile(new Book { Pages = { rested } }, Sword);
            report.Near("A book that has to rest does", resting.Cooldown, Rulebook.RestShort);
            report.Near("And is stronger for it", resting.First.Potency, 1f + Stickers.Written("rest")!.GoodBy / 100f);

            BookProfile tagged = Profile(new Book { Pages = { Scribe.NewPage(PageKind.Projectile), Scribe.NewPage(PageKind.Projectile), Scribe.NewPage(PageKind.Area) } }, Sword, More(Stat.Potency, 100f, Tags.Closing), More(Stat.Potency, 50f, Tags.Turned));
            report.Near("Potency can be for where a page is in a book: the first gets neither", tagged.Pages[0].Potency, 1f);
            report.Near("A middle page is only Turned", tagged.Pages[1].Potency, 1.5f);
            report.Near("And the last is both", tagged.Pages[2].Potency, 3f);

            BookProfile single = Profile(Starter(), Sword, More(Stat.Potency, 100f, Tags.Closing), More(Stat.Potency, 100f, Tags.Opening));
            report.Near("A book of one page is its first and its last", single.First.Potency, 4f);
            report.Near("Potency has a ceiling, whatever's stacked on it", Profile(Starter(), Sword, More(Stat.Potency, 100000f)).First.Potency, Rulebook.MostPotency);
        }

        private static Book Of(params Page[] pages) => new Book { Pages = pages.ToList() };

        private static Page Spell(PageKind kind) => Scribe.NewPage(kind, Hand.Spell);

        // Attack or Spell is the page's, and what each deals is a different Damage. As a person put it: an Attack Page deals all your Attack Damage, and a Spell Page all your Spell Damage, of every type there is.
        private static void Weapons(Report report)
        {
            Book bolt = Of(Scribe.NewPage(PageKind.Projectile));
            Book cone = Starter();
            Book spell = Of(Spell(PageKind.Area));

            report.Check("An Attack Page is an Attack, whatever's in the hand", Profile(cone, Sword).First.IsAttack && Profile(cone, Wand).First.IsAttack);
            report.Check("And a Spell Page a Spell", !Profile(spell, Sword).First.IsAttack && (Profile(spell, Wand).First.Tags & Tags.Spell) != 0 && (Profile(spell, Sword).First.Tags & Tags.Spell) != 0);
            report.Check("An Attack by hand is Melee", (Profile(cone, Sword).First.Tags & Tags.Melee) != 0);
            report.Check("And not when it's thrown", (Profile(bolt, Sword).First.Tags & Tags.Melee) == 0 && (Profile(bolt, Sword).First.Tags & Tags.Projectile) != 0);
            Book away = Starter();
            away.Pages[0].Stickers.Add(Stickers.Written("from-cursor")!);
            report.Check("Nor when it happens somewhere other than where they stand", (Profile(away, Sword).First.Tags & Tags.Melee) == 0);
            report.Check("A Spell is never Melee, nor done with a sword", (Profile(spell, Sword).First.Tags & (Tags.Melee | Tags.Sword)) == 0);
            report.Check("A bow makes an Attack that's no sort of Melee", Profile(cone, Bow).First.IsAttack && (Profile(cone, Bow).First.Tags & Tags.Melee) == 0);

            // What each deals.
            var fiery = new WeaponProfile(20f, 30f, 0.6f, 5f, Tags.Wand | Tags.OneHanded, DamageType.Fire);
            report.Near("An Attack deals all of the weapon's damage", Profile(cone, Sword).First.Flows.ToArray().Sum(flow => flow.Max), 30f);
            report.Check("Of the weapon's own type", Profile(cone, fiery).First.Flows is { Length: 1 } flows && flows[0].Type == DamageType.Fire);
            report.Check("A Spell deals none of it", !Profile(spell, Sword).First.DealsDamage || Profile(spell, Sword).First.AverageHit == 0f);
            BookProfile cast = Profile(spell, Wand, Added(DamageType.Cold, 10f, Tags.Spell), Added(DamageType.Physical, 50f, Tags.Attack));
            report.Check("Only what's added to Spells", cast.First.Flows is { Length: 1 } only && only[0].Type == DamageType.Cold, string.Join(", ", cast.First.Flows.ToArray().Select(flow => $"{flow.Type} {flow.Max}")));
            BookProfile struck = Profile(cone, Sword, Added(DamageType.Cold, 10f, Tags.Spell), Added(DamageType.Lightning, 5f, Tags.Attack));
            report.Check("And an Attack only what's added to Attacks, beside the weapon", struck.First.Flows.Length == 2 && struck.First.Flows.ToArray().All(flow => flow.Type is DamageType.Physical or DamageType.Lightning));
            BookProfile everything = Profile(Of(Spell(PageKind.Area)), Wand, Added(DamageType.Fire, 4f, Tags.Spell), Added(DamageType.Cold, 4f, Tags.Spell), Added(DamageType.Poison, 4f));
            report.Check("Of every type there is", everything.First.Flows.Length == 3);
            report.Check("And looks like what it deals most of", Profile(spell, Wand, Added(DamageType.Fire, 1f, Tags.Spell), Added(DamageType.Cold, 9f, Tags.Spell)).First.Look == DamageType.Cold);

            var paired = Item.Paired(new WeaponProfile(10f, 10f, 0.6f, 5f, Tags.Sword | Tags.OneHanded), new WeaponProfile(10f, 10f, 0.6f, 5f, Tags.Wand | Tags.OneHanded, DamageType.Lightning));
            report.Check("Two weapons deal both their types", Profile(cone, paired).First.Flows.Length == 2);

            // How long it takes.
            float swing = Profile(cone, Sword).UseTime;
            report.Near("A book that opens with an Attack takes the weapon's time", swing, Rulebook.PageUseTime);
            report.Near("And Attacks after it take nothing more", Profile(Of(Scribe.NewPage(PageKind.Projectile), Scribe.NewPage(PageKind.Projectile), Scribe.NewPage(PageKind.Area)), Sword).UseTime, swing);
            report.Near("Each Spell Page adds its own time to cast", Profile(Of(Scribe.NewPage(PageKind.Projectile), Spell(PageKind.Projectile), Spell(PageKind.Area)), Sword).UseTime, swing + 2f * Rulebook.CastTime);
            report.Near("A book that opens with a Spell takes only that", Profile(Of(Spell(PageKind.Projectile), Scribe.NewPage(PageKind.Area)), Sword).UseTime, Rulebook.CastTime);
            report.Near("Attack Speed hurries an Attack", Profile(cone, Sword, Inc(Stat.AttackSpeed, 100f)).UseTime, swing / 2f);
            report.Near("And not a Spell", Profile(spell, Wand, Inc(Stat.AttackSpeed, 100f)).UseTime, Profile(spell, Wand).UseTime);
            report.Near("Cast Speed hurries a Spell", Profile(spell, Wand, Inc(Stat.CastSpeed, 100f)).UseTime, Profile(spell, Wand).UseTime / 2f);
            report.Near("And only the Spell in a book that's both", Profile(Of(Scribe.NewPage(PageKind.Projectile), Spell(PageKind.Area)), Sword, Inc(Stat.CastSpeed, 100f)).UseTime, swing + Rulebook.CastTime / 2f);

            Page hasty = Scribe.NewPage(PageKind.Area);
            hasty.Stickers.Add(Stickers.Written("hasty")!);
            float quicker = 1f - Stickers.Written("hasty")!.GoodBy / 100f;
            report.Near("What a sticker says of Use Time goes for the whole book, from whatever page it's on", Profile(Of(Scribe.NewPage(PageKind.Projectile), hasty), Sword).UseTime, swing * quicker);
            report.Near("A Spell's too", Profile(Of(Scribe.NewPage(PageKind.Projectile), Spell(PageKind.Projectile), hasty), Sword).UseTime, (swing + Rulebook.CastTime) * quicker);

            var slow = new WeaponProfile(20f, 30f, WeaponProfile.BareTime * 2f, 5f, Tags.Sword);
            report.Near("What's in the hands sets an Attack's pace", Profile(cone, slow).UseTime, Rulebook.PageUseTime * 2f);
            report.Near("And not a Spell's", Profile(spell, slow).UseTime, Rulebook.CastTime);

            report.Near("Melee Reach only reaches Melee", Profile(cone, Sword, Flat(Stat.MeleeReach, 2f)).First.Length, Rulebook.ConeReach + 2f);
            report.Near("A Spell's Cone stays as it is", Profile(spell, Wand, Flat(Stat.MeleeReach, 2f)).First.Length, Rulebook.ConeReach);
            report.Near("Area of Effect scales the space, so a side by its root", Profile(cone, Sword, Inc(Stat.AreaOfEffect, 300f)).First.Length, Rulebook.ConeReach * 2f, 0.01f);
        }

        // The goal is that nothing written by hand is needed. So here are the things every ARPG has, written down, and they have to come to what they should.
        private static void Recipes(Report report)
        {
            BookProfile Of(BookPlan plan) => new Loadout(20, Sword, new[] { Flat(Stat.Health, 100f), Flat(Stat.Mana, 100f) }, new[] { plan }).SheetFor(Condition.None).Profile(0);

            report.Check("A plain swing", Of(Shelf.Strike).First.Page is { Kind: PageKind.Area, Shape: AreaShape.Cone, From: Start.You, Hand: Hand.Attack });
            report.Check("A swing all round", Of(Shelf.Whirl).Sign.Form == PageForm.Nova);
            report.Check("An arrow", Of(Shelf.Shot).Sign.Form == PageForm.Bolt);
            report.Check("A fan of arrows", Of(Shelf.Volley).First.Projectiles == 5);
            report.Check("A rain of them", Of(Shelf.RainOfArrows).First is { Projectiles: 5, Page.Falls: true });
            report.Check("A fireball that bursts", Of(Shelf.Fireball).Pages.Length == 2 && !Of(Shelf.Fireball).First.Page.Hostile && Of(Shelf.Fireball).Pages[1].Page.Kind == PageKind.Area);
            report.Check("Which is a Spell, and fire", Of(Shelf.Fireball).Pages[1].Page.Hand == Hand.Spell && Of(Shelf.Firebolt).First.Page.Mods.Any(mod => mod.Stat == Stat.Convert && mod.To == DamageType.Fire));
            report.Check("A meteor, which bursts on the ground", Of(Shelf.Meteor).First.Page is { Falls: true, Grounded: true } && Of(Shelf.Meteor).Delay > 0f);
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
            report.Check("Everything set off by something else is an Attack, as everyone has some of that", Shelf.All.Where(book => (book.First.Tags & Tags.Triggered) != 0 && book.Pages.Any(page => page.DealsDamage) && book.Id != "last-word").All(book => book.Pages.All(page => page.Hand == Hand.Attack)));
        }

        private static Book Starter() => new Book { Pages = { Scribe.NewPage(PageKind.Area) } };

        private static Item Book(string name)
        {
            Item book = ItemGenerator.Book(9);
            book.Name = name;
            return book;
        }

        private static Item Leaf(PageKind kind, int places) => new Item { Name = "Leaf", Slot = ItemSlot.Page, Level = 1, Seed = 3, Page = Scribe.NewPage(kind), Held = new Item?[places] };

        private static Item Stuck(string name) => ItemGenerator.StickerItem(Stickers.Written(name)!, 5, 1, Rarity.Normal);

        // Everything a sticker promises, over a great many of them.
        private static void Stickered(Report report)
        {
            var random = new Dino_Engine.World.WorldRandom(12);
            int faults = 0;
            var goods = new HashSet<PageEffect>();
            var bads = new HashSet<PageEffect>();
            var seen = new HashSet<string>();
            for (int i = 0; i < 5000; i++)
            {
                Rarity rarity = (Rarity)(i % 4);
                Sticker sticker = Stickers.Roll(rarity, random.Fork(i));
                goods.Add(sticker.Good);
                bads.Add(sticker.Bad);
                seen.Add($"{sticker.Good} {sticker.GoodBy} {sticker.Bad} {sticker.BadBy}");
                if (Stickers.IsUnique(sticker.Good) != (rarity == Rarity.Unique) || Stickers.IsUnique(sticker.Bad)) faults++;
                bool flag = sticker.Good is PageEffect.Stops or PageEffect.Rides || Stickers.IsUnique(sticker.Good);
                if (sticker.Good == PageEffect.None || sticker.Bad == PageEffect.None || sticker.Good == sticker.Bad) faults++;
                if (!flag && sticker.GoodBy <= 0f || sticker.BadBy <= 0f) faults++;
                if (BookText.Good(sticker).Length == 0 || BookText.Bad(sticker).Length == 0) faults++;
                if (sticker.GoodBy > Rulebook.MostStickerGood || sticker.BadBy > Rulebook.MostStickerBad) faults++;

                // Each side has to mean something on whatever it fits, or one side is a sticker with nothing to it.
                PageKind? either = Stickers.Fits(sticker);
                if (either == PageKind.Area && sticker.Good is PageEffect.Speed or PageEffect.Range or PageEffect.Projectiles or PageEffect.Pierce or PageEffect.Chains or PageEffect.Stops or PageEffect.Rides) faults++;
                if (either == PageKind.Area && sticker.Bad is PageEffect.Speed or PageEffect.Range) faults++;
                if (either == PageKind.Projectile && (sticker.Good == PageEffect.Area || sticker.Bad == PageEffect.Area)) faults++;
            }
            report.Check("Every sticker has one good thing and one bad thing, and they aren't the same thing, and only a Unique one has what only those have", faults == 0, $"{faults}");
            report.Check("Everything that can be good turns up", goods.SetEquals(Stickers.Goods), string.Join(", ", goods));
            report.Check("And everything that can be bad", bads.SetEquals(Stickers.Bads), string.Join(", ", bads));
            report.Check("No two need be alike", seen.Count > 1500, $"{seen.Count} different of 5000");

            float Mean(Rarity rarity, Func<Sticker, float> of, PageEffect effect, bool good)
            {
                var values = new List<float>();
                for (int i = 0; i < 6000; i++)
                {
                    Sticker sticker = Stickers.Roll(rarity, random.Fork(100000 + i));
                    if ((good ? sticker.Good : sticker.Bad) == effect) values.Add(of(sticker));
                }
                return values.Average();
            }
            report.Check("A rarer sticker does more", Mean(Rarity.Rare, s => s.GoodBy, PageEffect.Potency, true) > Mean(Rarity.Normal, s => s.GoodBy, PageEffect.Potency, true) * 2f);
            report.Check("And costs less for it", Mean(Rarity.Rare, s => s.BadBy, PageEffect.Potency, false) < Mean(Rarity.Normal, s => s.BadBy, PageEffect.Potency, false));

            Sticker both = new Sticker { Good = PageEffect.Potency, GoodBy = 40f, Bad = PageEffect.UseTime, BadBy = 50f };
            Page page = Scribe.NewPage(PageKind.Area);
            page.Stickers.Add(both);
            PagePlan plan = Scribe.Read(new Book { Pages = { page } }).First;
            report.Near("A sticker does what its good side says", plan.Potency, 1.4f);
            report.Near("And its bad side", plan.UseScale, 1.5f);

            Page free = Scribe.NewPage(PageKind.Area);
            free.Stickers.Add(new Sticker { Good = PageEffect.Potency, GoodBy = 10f, Bad = PageEffect.Cost, BadBy = 3f });
            report.Near("Cost as a drawback falls on a page that's free too", Scribe.Read(new Book { Pages = { free } }).First.Cost, 3f);

            Page many = Scribe.NewPage(PageKind.Projectile);
            many.Stickers.Add(Stickers.Written("two-at-once")!);
            many.Stickers.Add(Stickers.Written("three-at-once")!);
            many.Stickers.Add(Stickers.Written("pierce-one")!);
            many.Stickers.Add(Stickers.Written("pierce-three")!);
            PagePlan stacked = Scribe.Read(new Book { Pages = { many } }).First;
            report.Check("What adds up does, as anything can be stuck beside anything", stacked.Projectiles == 4 && stacked.Pierce == 4, $"{stacked.Projectiles}, {stacked.Pierce}");

            Page flood = Scribe.NewPage(PageKind.Projectile);
            for (int i = 0; i < Rulebook.MostStickers; i++) flood.Stickers.Add(Stickers.Written("five-at-once")!);
            report.Check("But not past what a page can make", Scribe.Read(new Book { Pages = { flood } }).First.Projectiles == Rulebook.MostProjectiles);

            report.Check("Everything already written has what a sticker can say", Stickers.Names.All(name => BookText.Good(Stickers.Written(name)!).Length > 0));
            report.Check("Every Unique effect has a name to be found by, and words", Stickers.Uniques.All(effect => ItemGenerator.StickerItem(new Sticker { Good = effect }, 1, 1, Rarity.Unique).Name != "Sticker" && BookText.Good(new Sticker { Good = effect }).Length > 0));

            // Where a page starts only means anything on the first, and the rest are only where a sticker says.
            Page aimed = Scribe.NewPage(PageKind.Projectile);
            aimed.Stickers.Add(Stickers.Written("from-nearest")!);
            aimed.Stickers.Add(Stickers.Written("towards-you")!);
            Page second = Scribe.NewPage(PageKind.Projectile);
            second.Stickers.Add(Stickers.Written("from-cursor")!);
            second.Stickers.Add(Stickers.Written("towards-nearest")!);
            BookPlan steered = Scribe.Read(new Book { Pages = { aimed, second } });
            report.Check("A Unique sticker moves where the first page starts", steered.Pages[0].From == Start.Nearest && steered.Pages[0].To == Bearing.You);
            report.Check("But a later page still starts where the last struck, wherever it goes", steered.Pages[1].From == Start.Impact && steered.Pages[1].To == Bearing.Nearest);
            Page solid = Scribe.NewPage(PageKind.Projectile);
            solid.Stickers.Add(Stickers.Written("grounded")!);
            report.Check("And only a sticker makes a wall an Impact", Scribe.Read(new Book { Pages = { solid } }).First.Grounded && !Scribe.Read(new Book { Pages = { Scribe.NewPage(PageKind.Projectile) } }).First.Grounded);

            Sticker uniqueOne = Stickers.Roll(Rarity.Unique, random.Fork(424242));
            var uniqueItem = ItemGenerator.StickerItem(uniqueOne, 7, 10, Rarity.Unique);
            ItemGenerator.Fix(uniqueItem);
            report.Check("A Unique sticker stays one when it's put right", uniqueItem.Rarity == Rarity.Unique && Stickers.IsUnique(uniqueItem.Sticker!.Good));
            var forged = ItemGenerator.StickerItem(new Sticker { Good = PageEffect.Potency, GoodBy = 10f, Bad = PageEffect.Cost, BadBy = 1f }, 7, 10, Rarity.Unique);
            ItemGenerator.Fix(forged);
            report.Check("And one that only says it is isn't", forged.Rarity == Rarity.Rare);
            Sticker cheat = new Sticker { Good = PageEffect.Falls, Bad = PageEffect.Grounded };
            Stickers.Fix(cheat);
            report.Check("Nothing that isn't Unique can have what only those have, on either side", !Stickers.IsUnique(cheat.Good) && !Stickers.IsUnique(cheat.Bad));
            report.Check("Nothing written says more than a sticker can", Stickers.Names.All(name => Stickers.Written(name)!.GoodBy <= Rulebook.MostStickerGood && Stickers.Written(name)!.BadBy <= Rulebook.MostStickerBad));
        }

        // What's pressed for what, which everything a player does goes through.
        private static void Keyed(Report report)
        {
            string folder = Path.Combine(Path.GetTempPath(), $"buildlab-keys-{Environment.ProcessId}");
            SettingsStore.Use(folder, scripted: false);
            Bindings.Load();

            GameAction[] every = Enum.GetValues<GameAction>();
            var shown = Bindings.Page.SelectMany(group => group.Rows).Select(row => row.Action).ToList();
            report.Check("Everything that can be pressed is on the Input page, once", shown.Count == every.Length && shown.Distinct().Count() == every.Length, $"{shown.Count} of {every.Length}");
            report.Check("Every place on the bar has a key", Enumerable.Range(0, HeroState.BarSlots).All(slot => Bindings.Label(Bindings.Slot(slot)).Length > 0));
            report.Check("No two start on the same key", every.Select(Bindings.Of).Distinct().Count() == every.Length);
            report.Check("And they're the ones people expect", Bindings.Label(GameAction.Interact) == "E" && Bindings.Label(GameAction.Slot0) == "LMB" && Bindings.Label(GameAction.Slot3) == "1" && Bindings.Label(GameAction.Dodge) == "Space");

            GameAction? swapped = Bindings.Set(GameAction.Interact, new Bound(Keys.A));
            report.Check("Taking a key someone has swaps the two", swapped == GameAction.MoveLeft && Bindings.Label(GameAction.Interact) == "A" && Bindings.Label(GameAction.MoveLeft) == "E");
            report.Check("Nothing's left with no key", every.All(action => Bindings.Label(action).Length > 0) && every.Select(Bindings.Of).Distinct().Count() == every.Length);
            report.Check("Giving something the key it has does nothing", Bindings.Set(GameAction.Interact, new Bound(Keys.A)) == null);

            // Menu is the one thing on a key nobody can have, so what it takes from can't be given that.
            GameAction? moved = Bindings.Set(GameAction.Menu, new Bound(Keys.M));
            report.Check("Moving Menu onto a key doesn't hand anyone Esc", moved == GameAction.Map && Bindings.Label(GameAction.Menu) == "M" && every.Where(action => action != GameAction.Menu).All(action => Bindings.CanBind(Bindings.Of(action))) && every.Select(Bindings.Of).Distinct().Count() == every.Length, Bindings.Label(GameAction.Map));
            Bindings.Set(GameAction.Map, new Bound(Keys.M));
            Bindings.Set(GameAction.Menu, new Bound(Keys.P));

            Bindings.Set(GameAction.Slot2, Bound.Mouse(MouseButton.Middle));
            report.Check("Anything can be on the mouse", Bindings.OnMouse(GameAction.Slot2) && Bindings.Label(GameAction.Slot2) == "MMB");
            Bindings.Set(GameAction.Slot3, Bound.Mouse(MouseButton.Button4));
            report.Check("Even the buttons with no name", Bindings.Label(GameAction.Slot3) == "Mouse 4");

            Bindings.Set(GameAction.Compare, new Bound(Keys.RightShift));
            report.Check("The two Shifts are one key", Bindings.Label(GameAction.Compare) == "Shift" && Bindings.Label(GameAction.ForceAttack) == "Alt");

            Bindings.Load();
            report.Check("It's all there next time", Bindings.Label(GameAction.Interact) == "A" && Bindings.Label(GameAction.Slot2) == "MMB" && Bindings.Label(GameAction.Slot3) == "Mouse 4");

            report.Check("Escape can't be taken", !Bindings.CanBind(Keys.Escape) && !Bindings.CanBind(Keys.F3) && Bindings.CanBind(Keys.F4));

            Bindings.Reset();
            report.Check("Resetting puts everything back", Bindings.Label(GameAction.Interact) == "E" && Bindings.Label(GameAction.MoveLeft) == "A" && Bindings.Label(GameAction.Slot2) == "Q");

            File.WriteAllText(Path.Combine(folder, "keys.json"), "{ \"Interact\": \"Q\", \"Slot2\": \"Q\", \"Nonsense\": \"E\", \"Dodge\": \"NoSuchKey\" }");
            Bindings.Load();
            report.Check("A file edited by hand can't put two on one key", every.Select(Bindings.Of).Distinct().Count() == every.Length, $"{every.Select(Bindings.Of).Distinct().Count()}");
            report.Check("Or take one away", Bindings.Label(GameAction.Dodge) == "Space");

            File.WriteAllText(Path.Combine(folder, "keys.json"), "{ \"Slot2\": \"E\", \"Flask\": \"E\" }");
            Bindings.Load();
            report.Check("Nor when what someone started on has gone too", every.Select(Bindings.Of).Distinct().Count() == every.Length, $"{every.Select(Bindings.Of).Distinct().Count()}");

            File.WriteAllText(Path.Combine(folder, "keys.json"), "{ \"Interact\": \"Escape\", \"Flask\": \"F3\", \"Hero\": \"Mouse99\", \"Compare\": \"RightAlt\" }");
            Bindings.Load();
            report.Check("Or give anyone what can't be taken", Bindings.Label(GameAction.Interact) == "E" && Bindings.Label(GameAction.Flask) == "R" && Bindings.Label(GameAction.Hero) == "C" && Bindings.Of(GameAction.Compare) == new Bound(Keys.LeftAlt));

            File.WriteAllText(Path.Combine(folder, "keys.json"), "{ this isn't anything");
            Bindings.Load();
            report.Check("And one that can't be read is the defaults", Bindings.Label(GameAction.Interact) == "E");

            string theirs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DinoDefenders", "keys.json");
            DateTime before = File.GetLastWriteTimeUtc(theirs);
            SettingsStore.Use(null, scripted: true);
            Bindings.Load();
            report.Check("A scripted run has the defaults", Bindings.Label(GameAction.Interact) == "E");
            Bindings.Set(GameAction.Interact, new Bound(Keys.Z));
            report.Check("And writes nothing", File.GetLastWriteTimeUtc(theirs) == before);
            Bindings.Reset();
            Directory.Delete(folder, true);

            foreach (GraphicsQuality quality in Enum.GetValues<GraphicsQuality>())
            {
                QualityPreset preset = GraphicsPresets.Of(quality);
                report.Check($"{quality} keeps grass edges, which lean on multisampling", preset.Samples >= 2);
            }
            QualityPreset low = GraphicsPresets.Of(GraphicsQuality.Low), mid = GraphicsPresets.Of(GraphicsQuality.Medium), high = GraphicsPresets.Of(GraphicsQuality.High);
            report.Check("Each level asks for less than the next", low.GrassDensity < mid.GrassDensity && mid.GrassDensity < high.GrassDensity && low.ShadowMap < mid.ShadowMap && mid.ShadowMap < high.ShadowMap && low.FogSteps < mid.FogSteps && mid.FogSteps < high.FogSteps);
            report.Check("And the field still reaches past the camera at every one", low.GrassDistance > 60f);
        }

        // Everyone someone has made, which is more than anything else in the game worth not losing.
        private static void Rostered(Report report)
        {
            string root = Path.Combine(Path.GetTempPath(), $"buildlab-roster-{Environment.ProcessId}");
            Directory.CreateDirectory(root);
            string old = Path.Combine(root, "save.json");
            HeroState first = HeroState.CreateNew();
            first.Level = 12;
            first.Gold = 777;
            SaveGame.PathOverride = old;
            new SaveGame { Hero = first, WorldSeed = 5, PlayerX = 10f, PlayerZ = 10f }.Store();
            SaveGame.PathOverride = null;
            string saved = File.ReadAllText(old);

            var roster = new HeroRoster(root);
            List<RosterEntry> heroes = roster.List();
            report.Check("The save from before there were several heroes is the first of them", heroes.Count == 1 && heroes[0].Hero.Level == 12 && heroes[0].Hero.Gold == 777);
            report.Check("With a name, as it never had one", heroes[0].Hero.Name == HeroRoster.FirstHero);
            report.Check("And the old file is kept, word for word, but moved out of the way", !File.Exists(old) && File.ReadAllText(old + ".before-heroes") == saved);
            report.Check("It's only moved once", new HeroRoster(root).List().Count == 1);

            SaveGame.PathOverride = heroes[0].Path;
            SaveGame? moved = SaveGame.Load();
            SaveGame.PathOverride = null;
            report.Check("With its world, so they go back to where they were", moved is { WorldSeed: 5 } && !moved.NeverPlayed());

            RosterEntry? ash = roster.Create("  Ash  ");
            report.Check("A new hero gets the name they were given, trimmed", ash?.Hero.Name == "Ash");
            report.Check("And what everyone starts with", ash?.Hero.Bar[0] is { IsBook: true });
            report.Check("Nobody can be called nothing", roster.Create("   ") == null && HeroRoster.Clean("") == null);
            report.Check("Or go on for ever", HeroRoster.Clean(new string('x', 400))!.Length == HeroRoster.LongestName);
            report.Check("Two can have the same name, as they're different people", roster.Create("Ash") != null && roster.List().Count == 3);

            SaveGame.PathOverride = ash!.Path;
            report.Check("Someone who hasn't been anywhere has a land still to grow", SaveGame.Load()?.NeverPlayed() == true);
            SaveGame.PathOverride = null;

            File.SetLastWriteTimeUtc(ash.Path, DateTime.UtcNow.AddHours(1));
            report.Check("Whoever was played last comes first", roster.List()[0].Path == ash.Path);

            File.WriteAllText(Path.Combine(root, "heroes", "broken.json"), "{ this isn't a save");
            int readable = roster.List().Count;
            report.Check("A save that can't be read isn't shown", readable == 3);
            report.Check("And isn't touched", File.Exists(Path.Combine(root, "heroes", "broken.json")));
            report.Check("Nor does looking change where saves go", SaveGame.PathOverride == null);

            roster.Delete(ash);
            report.Check("Someone deleted is off the list", roster.List().All(entry => entry.Path != ash.Path));
            report.Check("But not gone, as the game can't undo it", File.Exists(Path.Combine(root, "heroes", "deleted", Path.GetFileName(ash.Path))));

            Directory.Delete(root, true);
        }

        // What makes one book go off and another not.
        private static void Moded(Report report)
        {
            var random = new Dino_Engine.World.WorldRandom(44);
            var modes = new HashSet<BookMode>();
            var whens = new HashSet<BookWhen>();
            int wrong = 0;
            for (int i = 0; i < 4000; i++)
            {
                Item found = ItemGenerator.Folio(20, random.Fork(i), (Rarity)(i % 3));
                if (!found.IsBook)
                {
                    if (found.Mode != BookMode.Active || found.When != null) wrong++;
                    continue;
                }
                modes.Add(found.Mode);
                if (found.When != null) whens.Add(found.When.When);
                if ((found.Mode == BookMode.Trigger) != (found.When != null)) wrong++;
            }
            report.Check("Books are found of every kind", modes.Count == 3, string.Join(", ", modes));
            report.Check("Only a Trigger Book waits for anything, and every one does", wrong == 0, $"{wrong}");
            report.Check("And everything that can be waited for turns up", whens.SetEquals(BookTriggers.All), string.Join(", ", whens));
            report.Check("With words for all of it", BookTriggers.All.All(when => BookText.Waits(new BookTrigger { When = when, Amount = 3f }).Length > 0));

            float Waited(Rarity rarity) => Enumerable.Range(0, 2000).Select(i => BookTriggers.Roll(rarity, random.Fork(50000 + i))).Where(t => t.When == BookWhen.Hit).Average(t => t.Amount);
            report.Check("A rarer book waits for less", Waited(Rarity.Rare) < Waited(Rarity.Normal) * 0.6f);
            report.Check("What everyone starts with is pressed", ItemGenerator.Book(1).Mode == BookMode.Active);

            var aura = new Book { Mode = BookMode.Toggle, Pages = { Scribe.NewPage(PageKind.Area), Scribe.NewPage(PageKind.Projectile), Scribe.NewPage(PageKind.Area) } };
            aura.Pages[0].Timing = "lingering";
            Scribe.Fix(aura);
            report.Check("A Toggle Book reads only Area Pages, however many", aura.Pages.Count == 2 && aura.Pages.All(page => page.Kind == PageKind.Area));
            report.Check("And nothing on it lingers, as pulsing is all it does", aura.Pages[0].Timing == Sentences.PlainTiming);
            BookPlan read = Scribe.Read(aura);
            report.Check("Every Page of one is its own last, with nothing to turn to", read.Pages.All(page => page.Last));

            Page twoAreas = Scribe.NewPage(PageKind.Area);
            var pressed = new Book { Pages = { Scribe.NewPage(PageKind.Area), twoAreas } };
            Scribe.Fix(pressed);
            report.Check("A book that's pressed still stops at its first Area", pressed.Pages.Count == 1);

            BookProfile free = Profile(new Book { Mode = BookMode.Toggle, Pages = { Scribe.NewPage(PageKind.Area) } }, Sword);
            report.Near("A page that costs nothing still can't be left on for nothing", free.Cost, Rulebook.LeastToggleCost);
            report.Check("What it does in a second is a share of what it does by hand", free.DamagePerSecond < Profile(new Book { Pages = { Scribe.NewPage(PageKind.Area) } }, Sword).DamagePerSecond);

            Page quick = Scribe.NewPage(PageKind.Area);
            quick.Stickers.Add(Stickers.Written("hasty")!);
            Page rested = Scribe.NewPage(PageKind.Area);
            rested.Stickers.Add(Stickers.Written("rest")!);
            report.Check("Stickers count on a pulse: what quickens it does", Profile(new Book { Mode = BookMode.Toggle, Pages = { quick } }, Sword).Pulse < free.Pulse);
            report.Near("And what makes it rest holds it back", Profile(new Book { Mode = BookMode.Toggle, Pages = { rested } }, Sword).Pulse, Rulebook.RestShort);

            HeroState hero = HeroState.CreateNew();
            Item toggle = new Item { Name = "Aura", Slot = ItemSlot.Book, Level = 1, Seed = 2, Mode = BookMode.Toggle, Held = new Item?[2] };
            hero.Pickup(toggle, 0);
            hero.Pickup(Leaf(PageKind.Projectile, 1), 1);
            hero.Pickup(Leaf(PageKind.Area, 1), 2);
            hero.Pickup(Leaf(PageKind.Area, 1), 3);
            ItemPlace book = ItemPlace.Cell(0);
            report.Check("A Projectile Page can't go in a Toggle Book", !hero.CanMove(ItemPlace.Cell(1), book.In(0)));
            report.Check("An Area Page can", hero.Move(ItemPlace.Cell(2), book.In(0)));
            report.Check("And another after it", hero.Move(ItemPlace.Cell(3), book.In(1)));
            report.Check("Which reads as both", toggle.Written() is { Pages.Count: 2, Mode: BookMode.Toggle });

            Item waiting = new Item { Name = "Watcher", Slot = ItemSlot.Book, Level = 1, Seed = 2, Mode = BookMode.Trigger, When = new BookTrigger { When = BookWhen.Moved, Amount = 40f }, Held = new Item?[1] };
            var wire = new BitWriter();
            Protocol.WriteItem(wire, waiting);
            Item arrived = Protocol.ReadItem(new BitReader(wire.ToArray()));
            report.Check("What a book is and what it waits for come through the wire", arrived.Mode == BookMode.Trigger && arrived.When is { When: BookWhen.Moved, Amount: 40f });

            var sword = new Item { Name = "Sword", Slot = ItemSlot.MainHand, Mode = BookMode.Toggle, When = new BookTrigger() };
            var armed = new BitWriter();
            Protocol.WriteItem(armed, sword);
            Item plain = Protocol.ReadItem(new BitReader(armed.ToArray()));
            report.Check("A sword can't arrive as one", plain.Mode == BookMode.Active && plain.When == null);

            string edited = JsonSerializer.Serialize(new { Version = SaveGame.CurrentVersion, Hero = new { Level = 5, Bar = new object?[] { new { Name = "Greedy", Slot = "Book", Mode = "Trigger", When = new { When = "Hit", Amount = 0.001f }, Held = new object?[] { null } }, new { Name = "Blind", Slot = "Book", Mode = "Trigger", Held = new object?[] { null } } } } });
            SaveGame? hand = SaveGame.Read(edited);
            report.Check("A save can't make a book wait for next to nothing", hand?.Hero.Bar[0]?.When?.Amount >= 8f, $"{hand?.Hero.Bar[0]?.When?.Amount}");
            report.Check("Or wait for nothing at all", hand?.Hero.Bar[1]?.When != null);
        }

        // Potency goes on from page to page, as a person asked for.
        private static void Carried(Report report)
        {
            Page Weighted(float percent)
            {
                Page page = Scribe.NewPage(PageKind.Projectile);
                if (percent > 0f) page.Stickers.Add(new Sticker { Good = PageEffect.Potency, GoodBy = percent, Bad = PageEffect.UseTime, BadBy = 10f });
                else if (percent < 0f) page.Stickers.Add(new Sticker { Good = PageEffect.UseTime, GoodBy = 10f, Bad = PageEffect.Potency, BadBy = -percent });
                return page;
            }

            BookPlan plan = Scribe.Read(new Book { Pages = { Weighted(-10f), Weighted(0f), Weighted(100f) } });
            report.Near("A first page stands on its own", plan.Pages[0].Potency, 0.9f);
            report.Near("The next starts from where it left off", plan.Pages[1].Potency, 0.9f);
            report.Near("And what doubles a page doubles that", plan.Pages[2].Potency, 1.8f);
            report.Near("It knows where it started from", plan.Pages[2].Carried, 0.9f);

            var heavy = new Book();
            for (int i = 0; i < Rulebook.MostPages; i++) heavy.Pages.Add(Weighted(100f));
            report.Near("It can't climb past the ceiling", Scribe.Read(heavy).Pages[^1].Potency, Rulebook.MostPotency);

            var weak = new Book();
            for (int i = 0; i < Rulebook.MostPages; i++) weak.Pages.Add(Weighted(-60f));
            report.Near("Nor fall through the floor", Scribe.Read(weak).Pages[^1].Potency, Rulebook.LeastPotency);

            BookProfile sword = Profile(new Book { Pages = { Weighted(0f), Weighted(0f), Weighted(0f) } }, Sword, Inc(Stat.Potency, 20f));
            report.Near("What a build adds is added to each page and doesn't compound", sword.Pages[2].Potency, 1.2f);
        }

        // A hierarchy has to hold together however it's pulled about.
        private static void Slotting(Report report)
        {
            ItemPlace bar = ItemPlace.OnBar(0);
            report.Check("A place inside a place knows what it's in", bar.In(1).Holder == bar && bar.In(1).Index == 1 && bar.In(1).In(2).Holder == bar.In(1) && bar.In(1).In(2).Index == 2);
            report.Check("And none of them is mistaken for the pack or the bar", !bar.In(0).IsBar && !ItemPlace.Cell(3).In(0).IsCell && ItemPlace.Cell(3).In(0).IsHeld && !ItemPlace.Cell(3).IsHeld);

            HeroState hero = HeroState.CreateNew();
            hero.Pickup(Leaf(PageKind.Projectile, 2), 0);
            hero.Pickup(Stuck("swift"), 1);
            hero.Pickup(Stuck("broad"), 2);
            hero.Pickup(new Item { Name = "Sword", Slot = ItemSlot.MainHand, Level = 1, WeaponDamage = 5f }, 3);
            hero.Pickup(Leaf(PageKind.Area, 1), 4);

            report.Check("What everyone starts with holds a Page with room for a Sticker, and has a place to spare", hero.Bar[0]!.Places == 2 && hero.At(bar.In(0)) is { IsPage: true, Places: 1 } && hero.At(bar.In(1)) == null);
            report.Check("A Sword goes in no Book", !hero.CanMove(ItemPlace.Cell(3), bar.In(1)));
            report.Check("Nor a Sticker in a place for a Page", !hero.CanMove(ItemPlace.Cell(1), bar.In(1)));
            report.Check("Nor a Page on a Page", !hero.CanMove(ItemPlace.Cell(0), bar.In(0).In(0)));
            report.Check("Nothing can come after an Area Page", !hero.CanMove(ItemPlace.Cell(0), bar.In(1)));
            report.Check("Asking whether it can changes nothing", hero.Inventory[0] != null && hero.At(bar.In(1)) == null);

            report.Check("The Area Page can go last", hero.Move(bar.In(0), bar.In(1)) && hero.At(bar.In(1))!.Page!.Kind == PageKind.Area && hero.At(bar.In(0)) == null);
            report.Check("With a Projectile Page before it", hero.Move(ItemPlace.Cell(0), bar.In(0)) && hero.Inventory[0] == null);
            report.Check("And the Book reads as both, in order", hero.Bar[0]!.Written() is { Pages.Count: 2 } two && two.Pages[0].Kind == PageKind.Projectile && two.Pages[1].Kind == PageKind.Area);
            report.Check("They can't change places, as that puts something after an Area", !hero.CanMove(bar.In(0), bar.In(1)));
            report.Check("An Area in the pack can't take the first place while something's after it", !hero.CanMove(ItemPlace.Cell(4), bar.In(0)));

            report.Check("A Sticker goes on a Page of its kind", hero.Move(ItemPlace.Cell(1), bar.In(0).In(0)) && hero.Bar[0]!.Written()!.Pages[0].Stickers.Count == 1);
            report.Check("And not on the other kind", !hero.CanMove(bar.In(0).In(0), bar.In(1).In(0)) && !hero.CanMove(ItemPlace.Cell(2), bar.In(0).In(1)));
            report.Check("Where there's no such place, there's no such place", !hero.CanMove(ItemPlace.Cell(2), bar.In(1).In(5)) && hero.At(bar.In(9)) == null && hero.At(new ItemPlace(-5)) == null && hero.At(new ItemPlace(int.MaxValue)) == null);
            report.Check("One takes the place of another, which goes where that came from", hero.Pickup(Stuck("slow"), 9) && hero.Move(ItemPlace.Cell(9), bar.In(0).In(0)) && hero.Inventory[9]!.Sticker!.Good == PageEffect.Speed);
            report.Check("It can't swap with what couldn't go back", !hero.CanMove(ItemPlace.Cell(3), bar.In(0)));

            Item? taken = hero.Take(bar.In(0));
            report.Check("A Page comes out with what's on it", taken?.Held![0] != null && hero.At(bar.In(0)) == null);
            report.Check("A gap in a Book is only a gap", hero.Bar[0]!.Written() is { Pages.Count: 1 } && hero.Pickup(taken!, 0));
            hero.Take(bar.In(1));
            report.Check("A Book with nothing in it can't be used", hero.Bar[0]!.Written() == null && HeroBuild.SlotIndex(hero, 0) < 0 && HeroBuild.Compile(hero).Books.Length == 0);
            hero.Bar[2] = Book("Second");
            report.Check("And doesn't throw off what's beside it", HeroBuild.SlotIndex(hero, 2) == 0);

            var writer = HeroState.CreateNew();
            report.Check("A Book can be called something", writer.Title(bar, " Fireball ") && writer.Bar[0]!.Name == "Fireball");
            report.Check("A name can't go on for ever", writer.Title(bar, new string('x', 400)) && writer.Bar[0]!.Name.Length == HeroState.LongestBookName);
            report.Check("Leaving it blank leaves what it was", !writer.Title(bar, "   ") && writer.Bar[0]!.Name.Length == HeroState.LongestBookName);
            report.Check("Nothing but a Book can be called anything", !writer.Title(bar.In(0), "Leaf") && !writer.Title(ItemPlace.Worn(WornSlot.Body), "Shirt"));

            var random = new Dino_Engine.World.WorldRandom(21);
            int wrongRarity = 0, wrongPlaces = 0;
            int stickers = 0, pages = 0, books = 0;
            foreach (Rarity rarity in new[] { Rarity.Normal, Rarity.Magic, Rarity.Rare })
            {
                float mostPages = 0f, mostStickers = 0f;
                for (int i = 0; i < 400; i++)
                {
                    Item found = ItemGenerator.Folio(20, random.Fork((int)rarity * 1000 + i), rarity);
                    if (found.Rarity != rarity) wrongRarity++;
                    if (found.Places > (found.IsBook ? Rulebook.MostPages : Rulebook.MostStickers)) wrongPlaces++;
                    if (found.IsBook) { books++; mostPages = MathF.Max(mostPages, found.Places); }
                    if (found.IsPage) { pages++; mostStickers = MathF.Max(mostStickers, found.Places); }
                    if (found.IsSticker) stickers++;
                }
                if (rarity == Rarity.Normal && (mostPages >= Rulebook.MostPages || mostStickers >= Rulebook.MostStickers)) wrongPlaces++;
                if (rarity == Rarity.Rare && (mostPages != Rulebook.MostPages || mostStickers != Rulebook.MostStickers)) wrongPlaces++;
            }
            report.Check("What's found is the rarity it was rolled at", wrongRarity == 0, $"{wrongRarity}");
            report.Check("What's found Unique for a Book is always a Sticker with what only those have", Enumerable.Range(0, 200).All(i => ItemGenerator.Folio(20, random.Fork(9000 + i), Rarity.Unique) is { IsSticker: true, Rarity: Rarity.Unique, Sticker: Sticker unique } && Stickers.IsUnique(unique.Good)));

            // What a found page says is what it keeps.
            var dealt = new HashSet<Hand>();
            var shapes = new HashSet<string>();
            int raised = 0, moved = 0, odd = 0, total = 0;
            for (int i = 0; i < 3000; i++)
            {
                Page found = Scribe.Roll(random.Fork(20000 + i));
                Page kept = found.Copy();
                Scribe.Fix(kept);
                if (System.Text.Json.JsonSerializer.Serialize(kept) != System.Text.Json.JsonSerializer.Serialize(found)) odd++;
                total++;
                Deed deed = Sentences.Find(found.Outcomes[0].Deed)!.Deed;
                if (deed == Deed.Deal) dealt.Add(found.Hand);
                else if (found.Hand != Hand.Spell) odd++;
                if (deed == Deed.Raise) raised++;
                if (deed == Deed.Move) moved++;
                if (deed == Deed.Move && found.Kind != PageKind.Projectile) odd++;
                if (found.Kind == PageKind.Area) shapes.Add(found.Shape);
            }
            report.Check("A found page is already within the rules", odd == 0, $"{odd}");
            report.Check("Pages are found as Attacks and as Spells", dealt.Count == 2);
            report.Check("And as every shape", shapes.Count == Sentences.For(Blank.Shape).Count());
            report.Check("A few raise or move, and only a few", raised > 0 && moved > 0 && raised + moved < total / 8, $"{raised}, {moved} of {total}");
            report.Check("And only the best has all the places there can be", wrongPlaces == 0, $"{wrongPlaces}");
            report.Check("Books, Pages and Stickers can all be found, Stickers most and Books least", stickers > pages && pages > books && books > 0, $"{stickers}, {pages}, {books}");

            var luck = new LootLuck();
            int folios = 0, all = 0;
            for (int i = 0; i < 400; i++)
            {
                foreach (Item item in Loot.Roll(LootTier.Boss, 20, 20, 0f, luck, random.Fork(9000 + i)))
                {
                    all++;
                    if (item.IsFolio) folios++;
                }
            }
            report.Check("They drop, beside everything else", folios > all / 6 && folios < all / 2, $"{folios} of {all}");
        }

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

            HeroState fresh = HeroState.CreateNew();
            report.Check("Everyone starts with a book on the first place of the bar", fresh.Bar[0] is { IsBook: true } && fresh.Bar.Skip(1).All(item => item == null));
            report.Check("And the kit that comes of it has it there", new Dino_Defenders.Gameplay.HeroKit(fresh, HeroBuild.Compile(fresh).SheetFor(Condition.None)).Slots[0] != null);

            HeroState gaps = HeroState.CreateNew();
            gaps.Bar[4] = gaps.Bar[0];
            gaps.Bar[0] = null;
            var kit = new Dino_Defenders.Gameplay.HeroKit(gaps, HeroBuild.Compile(gaps).SheetFor(Condition.None));
            report.Check("A gap on the bar is a gap", kit.Slots[0] == null && kit.Slots[4] != null);

            Item empty = new Item { Name = "Empty", Slot = ItemSlot.Book, Level = 1, Held = new Item?[2] };
            report.Check("What's in a book is sold with it", ItemGenerator.Book(1).SellValue > empty.SellValue);
            var shop = new MerchantTable();
            var seller = new HeroState { Gold = 5 };
            seller.Bar[0] = ItemGenerator.Book(1);
            int worth = seller.Bar[0]!.SellValue;
            report.Check("Selling what's on the bar takes it off", shop.Sell(seller, ItemPlace.OnBar(0)) && seller.Bar[0] == null && seller.Gold == 5 + worth);

            var library = new HeroState();
            library.Bar[1] = Book("On the bar");
            library.Pickup(Book("In the pack"));
            library.Pickup(sword);
            report.Check("Every book can be found, the bar first", library.BookPlaces().Select(place => library.At(place)!.Name).SequenceEqual(new[] { "On the bar", "In the pack" }));
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

        private const string BeforeStickers = """
            {
              "Version": 6,
              "Hero": {
                "Level": 8, "Gold": 20, "Talents": [],
                "Bar": [ { "Name": "Fireball", "Slot": "Book", "Rarity": "Common", "Level": 1, "Seed": 7, "Lines": [],
                           "Book": { "Pages": [ { "Kind": "Projectile", "Towards": "towards-nearest", "Clauses": [], "Outcomes": [ { "Deed": "pass" } ] },
                                                { "Kind": "Area", "Shape": "circle", "Clauses": [ "tight" ], "Outcomes": [ { "Element": "fire" } ] } ] } },
                         null, null, null, null, null ],
                "Inventory": [ { "Name": "Spare", "Slot": "Book", "Rarity": "Common", "Level": 1, "Seed": 8, "Lines": [],
                                 "Book": { "Pages": [ { "Kind": "Area", "Clauses": [ "no-such-clause" ], "Outcomes": [ {} ] } ] } } ],
                "Equipped": { }
              },
              "WorldSeed": 7, "WorldLevel": 3
            }
            """;

        private const string BeforeFoundPages = """
            {
              "Version": 9,
              "Hero": {
                "Level": 8, "Gold": 20, "Talents": [],
                "Inventory": [ { "Name": "Healer", "Slot": "Page", "Rarity": "Normal", "Level": 1, "Seed": 7, "Page": { "Kind": "Area", "From": "from-cursor", "Outcomes": [ { "Deed": "heal" }, { "Deed": "deal", "Element": "fire" } ] }, "Held": [ null ] },
                               { "Name": "Raiser", "Slot": "Page", "Rarity": "Normal", "Level": 1, "Seed": 8, "Page": { "Kind": "Projectile", "Towards": "towards-you", "Outcomes": [ { "Deed": "raise", "Kin": "hound" } ] }, "Held": [ null ] },
                               { "Name": "Mover", "Slot": "Page", "Rarity": "Normal", "Level": 1, "Seed": 9, "Page": { "Kind": "Area", "Outcomes": [ { "Deed": "move" } ] }, "Held": [ null ] },
                               { "Name": "Striker", "Slot": "Page", "Rarity": "Normal", "Level": 1, "Seed": 10, "Page": { "Kind": "Projectile", "Outcomes": [ { "Deed": "deal", "Element": "physical" } ] }, "Held": [ null ] } ],
                "Equipped": { }
              },
              "WorldSeed": 7
            }
            """;

        private static void Saving(Report report)
        {
            SaveGame? pages = SaveGame.Read(BeforeFoundPages);
            Page? Found(int cell) => pages?.Hero.Inventory[cell]?.Page;
            report.Check("A save from before pages were found loads", pages != null);
            report.Check("A page keeps only its first Outcome, as what a found page could do: a heal becomes dealing, a Spell as it dealt fire", Found(0) is { Hand: Hand.Spell, Outcomes.Count: 1 } healer && healer.Outcomes[0].Deed == Sentences.PlainDeed);
            report.Check("Raising stays raising, and is a Spell", Found(1) is { Hand: Hand.Spell } raiser && raiser.Outcomes[0] is { Deed: "raise", Kin: "hound" });
            report.Check("Moving from an area becomes dealing, as it would leave you where you are", Found(2) is { Hand: Hand.Attack } mover && mover.Outcomes[0].Deed == Sentences.PlainDeed);
            report.Check("And a physical page is an Attack", Found(3) is { Hand: Hand.Attack });

            SaveGame? save = SaveGame.Read(BeforeBooks);
            report.Check("A save from before books loads", save != null);
            if (save == null) return;

            HeroState hero = save.Hero;
            report.Check("With the hero as they were", hero.Level == 12 && hero.Gold == 40 && hero.WorldsCleared == 1 && save.WorldSeed == 7);
            report.Check("Their tree has gone, every point there to spend again", hero.Talents.Count == 0 && hero.UnspentPoints == hero.TalentPoints, $"{hero.Talents.Count}, {hero.UnspentPoints}");
            report.Check("What they carry stays", hero.Inventory[0]?.Name.Contains("Hood") == true && hero.Equipped[WornSlot.MainHand].WeaponDamage == 7f);
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
            report.Check("And a book comes through being saved and loaded", again?.Hero.Bar[0]?.Written() is { Pages.Count: 1 });

            string edited = JsonSerializer.Serialize(new { Version = SaveGame.CurrentVersion, Hero = new { Level = 5, Bar = new object?[] { new { Name = "Bad", Slot = "Book", Book = new { Pages = new object[] { new { Shape = "no-such", Outcomes = Array.Empty<object>() } } } }, new { Name = "Sword", Slot = "MainHand" } } } });
            SaveGame? hand = SaveGame.Read(edited);
            report.Check("A book edited by hand is put right", hand?.Hero.Bar[0]?.Written()?.Pages[0].Shape == Sentences.PlainShape && hand.Hero.Bar[0]!.Written()!.Pages[0].Outcomes.Count == 1);
            report.Check("And a sword can't be left on the bar", hand?.Hero.Bar[1] == null);

            SaveGame? written = SaveGame.Read(BeforeStickers);
            report.Check("A save from before Stickers loads", written != null);
            Item? fireball = written?.Hero.Bar[0];
            report.Check("With its Book made of Pages, as many as it had and no more", fireball is { Name: "Fireball", Places: 2 } && fireball.Held!.All(page => page is { IsPage: true }));
            report.Check("Each with its Clauses turned into Stickers, and room for them", fireball?.Held![1]?.Held is { Length: 1 } held && held[0]?.Sticker?.Good == PageEffect.Potency && held[0]!.Sticker!.Bad == PageEffect.Area);
            report.Check("A Page with none still has a place for one", fireball?.Held![0]?.Places == 1);
            report.Check("Saying what it still can: a fire page is a Spell, and where a page went is gone", fireball?.Written() is { Pages.Count: 2 } read && read.Pages[0].Outcomes[0].Deed == Sentences.PlainDeed && read.Pages[0].Hand == Hand.Attack && read.Pages[1].Shape == "circle" && read.Pages[1].Hand == Hand.Spell);
            report.Check("A Clause that's gone only goes, and costs nothing else", written?.Hero.Inventory[0]?.Held?[0] is { IsPage: true, Places: 1 } gone && gone.Held![0] == null);
            report.Check("And none of the old way of saving it is left", written != null && !written.Hero.Bar.Concat(written.Hero.Carried()).Any(item => item?.Book != null));

            string stickered = Path.Combine(Path.GetTempPath(), $"buildlab-stickers-{Environment.ProcessId}.json");
            File.WriteAllText(stickered, BeforeStickers);
            SaveGame.PathOverride = stickered;
            SaveGame.Load()?.Store();
            bool copied = File.Exists(stickered + ".before-stickers") && File.ReadAllText(stickered + ".before-stickers") == BeforeStickers;
            string stored = File.ReadAllText(stickered);
            SaveGame? stable = SaveGame.Load();
            File.Delete(stickered + ".before-stickers");
            File.Delete(stickered);
            SaveGame.PathOverride = null;
            report.Check("A copy of that kept too", copied);
            report.Check("And what's saved now says nothing of where a page went, or what kind of damage it dealt", !stored.Contains("\"Towards\"") && !stored.Contains("\"Element\""));
            report.Check("And nothing of what's stuck on is lost by saving", stable?.Hero.Bar[0]?.Written()?.Pages[1].Stickers.Count == 1 && !stored.Contains("\"Clauses\""));

            var writer = new BitWriter();
            Item meteor = ItemGenerator.Copied(Shelf.Fireball.Source!, "Fireball", 4);
            Protocol.WriteItem(writer, meteor);
            Item back = Protocol.ReadItem(new BitReader(writer.ToArray()));
            report.Check("A book comes through the wire exactly, with everything in it", JsonSerializer.Serialize(back) == JsonSerializer.Serialize(meteor));
            report.Note($"A book of two pages is {writer.ByteLength} bytes");

            var deep = new Item { Name = "Deep", Slot = ItemSlot.Book, Held = new Item?[1] };
            deep.Held[0] = new Item { Name = "Deeper", Slot = ItemSlot.Book, Held = new Item?[1] };
            deep.Held[0]!.Held![0] = new Item { Name = "Deeper still", Slot = ItemSlot.Book, Held = new Item?[1] };
            deep.Held[0]!.Held![0]!.Held![0] = new Item { Name = "Too deep", Slot = ItemSlot.Book };
            var forged = new BitWriter();
            Protocol.WriteItem(forged, deep);
            bool refused = false;
            try { Protocol.ReadItem(new BitReader(forged.ToArray())); }
            catch (NetReadException) { refused = true; }
            report.Check("Something inside something for ever is refused at the door", refused);

            var strange = new BitWriter();
            strange.WriteByte((byte)PageKind.Area);
            strange.WriteByte((byte)Hand.Spell);
            for (int i = 0; i < 2; i++) strange.WriteVarInt(99999);
            strange.WriteCount(1);
            for (int i = 0; i < 7; i++) strange.WriteVarInt(99999);
            Page read2 = Protocol.ReadPage(new BitReader(strange.ToArray()));
            report.Check("And sentences nobody has heard of are put right, whatever a stranger sends", read2.Shape == Sentences.PlainShape && read2.Outcomes[0].Deed == Sentences.PlainDeed && read2.Hand == Hand.Spell);

            var empty = new Item { Name = "Empty", Slot = ItemSlot.Page };
            var item = new BitWriter();
            Protocol.WriteItem(item, empty);
            report.Check("Something sent as a page with nothing on it arrives with something on it", Protocol.ReadItem(new BitReader(item.ToArray())).Page != null);

            var plain = new Item { Name = "Sword", Slot = ItemSlot.MainHand, Page = Scribe.NewPage(PageKind.Area), Held = new Item?[] { Leaf(PageKind.Area, 0) } };
            var armed = new BitWriter();
            Protocol.WriteItem(armed, plain);
            Item sword = Protocol.ReadItem(new BitReader(armed.ToArray()));
            report.Check("And a sword with pages in it arrives as a sword", sword.Page == null && sword.Held == null);

            var mixed = new Item { Name = "Mixed", Slot = ItemSlot.Book, Held = new Item?[] { Stuck("swift"), Leaf(PageKind.Area, 0) } };
            var odd = new BitWriter();
            Protocol.WriteItem(odd, mixed);
            Item sorted = Protocol.ReadItem(new BitReader(odd.ToArray()));
            report.Check("Nothing arrives in a place it doesn't go", sorted.Held![0] == null && sorted.Held[1] is { IsPage: true });

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

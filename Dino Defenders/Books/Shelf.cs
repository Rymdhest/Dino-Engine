using Dino_Defenders.Stats;
using static Dino_Defenders.Stats.M;

namespace Dino_Defenders.Books
{
    /// <summary>
    /// Books that are already written: what creatures fight with, and what keystones and items set off. They're ordinary books, made of the same phrases as a hero's, read by the same <see cref="Scribe"/>, so anything
    /// an enemy does is something a player could have written. Their first page has to start from whoever's using them or from where they're aiming, as a mark on the ground is a promise, and <c>check</c> holds that.
    /// Numbers are places on the shelf, so a new book goes on the end.
    /// </summary>
    public static class Shelf
    {
        private static readonly List<BookPlan> Everything = new();

        public static IReadOnlyList<BookPlan> All => Everything;

        public static BookPlan Get(int number) => Everything[number - 1];

        public static BookPlan? Find(string id) => Everything.Find(book => book.Id == id);

        private static Outcome Deal() => new Outcome();

        private static Outcome Inflict(string ailment) => new Outcome { Deed = "afflict", Ailment = ailment };

        private static Outcome Give(string gift) => new Outcome { Deed = "give", Gift = gift };

        private static Outcome Do(string deed) => new Outcome { Deed = deed };

        private static Outcome Raise(string kin) => new Outcome { Deed = "raise", Kin = kin };

        private static List<Sentence> Said(string[] sentences) => sentences.Select(name => Sentences.Written(name)!).ToList();

        private static Page Area(string shape, string[] sentences, params Outcome[] outcomes) => new Page { Kind = PageKind.Area, Shape = shape, Sentences = Said(sentences), Outcomes = outcomes.ToList() };

        private static Page Bolt(string[] sentences, params Outcome[] outcomes) => new Page { Kind = PageKind.Projectile, Sentences = Said(sentences), Outcomes = outcomes.ToList() };

        // A Spell Page, which deals Spell Damage and takes its own time to cast.
        // Raising Minions beside whoever reads it, and nothing else.
        private static Page Summoning(Outcome outcome) => new Page { Kind = PageKind.Summon, Hand = Hand.Spell, Outcomes = { outcome } };

        private static Page Cast(this Page page)
        {
            page.Hand = Hand.Spell;
            return page;
        }

        private static Page Lasting(this Page page, string timing)
        {
            page.Timing = timing;
            return page;
        }

        private static readonly string[] Plain = Array.Empty<string>();

        private static string[] Say(params string[] sentences) => sentences;

        // What's set off by something else counts as that, and everything that isn't a hero's own can have numbers a phrase couldn't get to. A page has no element of its own, so what a book deals as something other than
        // what its user has is a conversion of all of their physical damage, on each page.
        private static BookPlan Write(string id, string name, Page[] pages, Action<PagePlan>? tune = null, bool triggered = false, BookMode mode = BookMode.Active, BookTrigger? when = null, DamageType? type = null)
        {
            var book = new Book { Pages = pages.ToList(), Mode = mode, When = when };
            Scribe.Fix(book);
            BookPlan plan = Scribe.Read(book, name);
            plan.Id = id;
            plan.Source = book;
            foreach (PagePlan page in plan.Pages)
            {
                if (triggered) page.Tags |= Tags.Triggered;
                tune?.Invoke(page);
                if (type is DamageType into && page.DealsDamage) page.Mods = page.Mods.Append(Convert(DamageType.Physical, into, 100f)).ToArray();
            }

            Everything.Add(plan);
            plan.Number = (ushort)Everything.Count;
            return plan;
        }

        // What creatures fight with.
        public static readonly BookPlan Strike = Write("strike", "Strike", new[] { Area("cone", Plain, Deal()) });
        public static readonly BookPlan Bite = Write("bite", "Bite", new[] { Area("cone", Say("tight"), Deal()) }, page => { page.Potency = 1f; page.UseTime = 0.5f; page.Mods = new[] { Chance(Tags.Bleed, 25f) }; });
        public static readonly BookPlan Maul = Write("maul", "Maul", new[] { Area("cone", Say("broad", "deliberate"), Deal()) }, page => { page.Potency = 1.6f; page.Mods = new[] { Chance(Tags.Stun, 50f) }; });
        public static readonly BookPlan Whirl = Write("whirl", "Whirl", new[] { Area("circle", Say("rest"), Deal()) }, page => page.Potency = 1.9f);
        public static readonly BookPlan Shot = Write("shot", "Loose", new[] { Bolt(Plain, Deal()) }, page => page.Speed = 56f);

        public static readonly BookPlan Firebolt = Write("firebolt", "Firebolt", new[] { Bolt(Plain, Deal()).Cast() }, page => { page.Speed = 34f; page.Mods = new[] { Chance(Tags.Burn, 25f) }; }, type: DamageType.Fire);
        public static readonly BookPlan Immolate = Write("immolate", "Immolate", new[] { Area("circle", Plain, Deal()).Cast() }, page => { page.Potency = 1.3f; page.Mods = new[] { Chance(Tags.Burn, 35f) }; }, type: DamageType.Fire);
        public static readonly BookPlan FrostShards = Write("frost-shards", "Frost Shards", new[] { Bolt(Say("three-at-once", "pierce-one"), Deal()).Cast() }, page => page.Potency = 0.6f, type: DamageType.Cold);
        public static readonly BookPlan FrostNova = Write("frost-nova", "Frost Nova", new[] { Area("circle", Say("rest"), Deal()).Cast() }, page => { page.Potency = 1.2f; page.Length = 10f; page.Mods = new[] { Inc(Stat.AilmentEffect, 60f, Tags.Chill), Chance(Tags.Freeze, 15f) }; }, type: DamageType.Cold);
        public static readonly BookPlan Spark = Write("spark", "Forked Spark", new[] { Bolt(Say("swift", "chain-three"), Deal()).Cast() }, page => { page.Potency = 1f; page.Mods = new[] { Chance(Tags.Shock, 30f) }; }, type: DamageType.Lightning);
        public static readonly BookPlan VenomSpit = Write("venom-spit", "Venom Spit", new[] { Bolt(Plain, Deal()).Cast() }, page => { page.Speed = 32f; page.Mods = new[] { Chance(Tags.Venom, 70f) }; }, type: DamageType.Poison);
        public static readonly BookPlan Miasma = Write("miasma", "Miasma", new[] { Area("circle", Say("rest", "from-cursor"), Deal()).Cast().Lasting("lingering") }, page => { page.Potency = 1f; page.Mods = new[] { Chance(Tags.Venom, 100f) }; }, type: DamageType.Poison);
        public static readonly BookPlan ArcaneDart = Write("arcane-dart", "Arcane Dart", new[] { Bolt(Plain, Deal()).Cast() }, page => { page.Speed = 46f; page.Mods = new[] { Chance(Tags.Hex, 20f) }; }, type: DamageType.Magic);
        public static readonly BookPlan Unmaking = Write("unmaking", "Unmaking", new[] { Area("circle", Say("rest", "from-cursor"), Deal()).Cast() }, page => { page.Potency = 2.4f; page.Length = 5.5f; page.Mods = new[] { Chance(Tags.Hex, 60f) }; }, type: DamageType.Magic);

        // What other things set off. These are Attacks, as everyone has an Attack Damage and not everyone a Spell Damage.
        public static readonly BookPlan CinderTrail = Write("cinder-trail", "Burning Ground", new[] { Area("circle", Plain, Deal()).Lasting("lingering") }, page => { page.Potency = 0.35f; page.Length = 3.2f; page.Duration = 3f; page.Mods = new[] { Chance(Tags.Burn, 40f) }; }, triggered: true, type: DamageType.Fire);
        public static readonly BookPlan Puddle = Write("puddle", "Standing Water", new[] { Area("circle", Plain, Do("water")).Lasting("lingering") }, page => { page.Length = 5f; page.Duration = Rulebook.WaterLinger; }, triggered: true);
        public static readonly BookPlan VenomCloud = Write("venom-cloud", "Contagion", new[] { Area("circle", Plain, Deal()) }, page => { page.Potency = 0.15f; page.Length = 8f; page.Mods = new[] { Chance(Tags.Venom, 100f) }; }, triggered: true, type: DamageType.Poison);
        public static readonly BookPlan Retaliate = Write("retaliate", "Retaliation", new[] { Area("circle", Plain, Deal()) }, page => page.Potency = 1.2f, triggered: true);
        public static readonly BookPlan Emberfall = Write("emberfall", "Emberfall", new[] { Bolt(Say("falls", "grounded"), Do("pass")), Area("circle", Plain, Deal()) }, page => page.Mods = new[] { Chance(Tags.Burn, 50f) }, triggered: true, type: DamageType.Fire);

        // A few of the things that can be done, to hold the phrases and sentences to: BuildLab and NetLab go through all of these, and --books puts them on a hero's bar.
        public static readonly BookPlan Fireball = Write("fireball", "Fireball", new[] { Bolt(Plain, Do("pass")).Cast(), Area("circle", Say("tight"), Deal()).Cast() }, type: DamageType.Fire);
        public static readonly BookPlan Meteor = Write("meteor", "Meteor", new[] { Bolt(Say("falls", "grounded"), Do("pass")).Cast(), Area("circle", Plain, Deal()).Cast() }, type: DamageType.Fire);
        public static readonly BookPlan Boomerang = Write("boomerang", "Boomerang", new[] { Bolt(Plain, Deal()), Bolt(Say("towards-you"), Deal()) });
        public static readonly BookPlan ChainLightning = Write("chain-lightning", "Chain Lightning", new[] { Bolt(Say("swift", "towards-nearest"), Deal()).Cast(), Bolt(Say("swift", "towards-nearest"), Deal()).Cast(), Bolt(Say("swift", "towards-nearest"), Deal()).Cast() }, type: DamageType.Lightning);
        public static readonly BookPlan Volley = Write("volley", "Volley", new[] { Bolt(Say("five-at-once"), Deal()) });
        public static readonly BookPlan RainOfArrows = Write("rain", "Rain of Arrows", new[] { Bolt(Say("five-at-once", "falls"), Deal()) });
        public static readonly BookPlan Charge = Write("charge", "Charge", new[] { Bolt(Say("rides", "short"), Do("pass")), Area("cone", Plain, Deal()) });
        public static readonly BookPlan Blink = Write("blink", "Blink", new[] { Area("circle", Say("tight", "from-cursor"), Do("move")).Cast() });
        public static readonly BookPlan Lance = Write("lance", "Glacial Lance", new[] { Area("line", Plain, Deal()).Cast() }, type: DamageType.Cold);
        public static readonly BookPlan Storm = Write("storm", "Stormcall", new[] { Area("circle", Say("from-cursor"), Deal()).Cast().Lasting("lingering") }, type: DamageType.Lightning);
        public static readonly BookPlan Mend = Write("mend", "Mend", new[] { Area("circle", Plain, new Outcome { Deed = "heal", Measure = "of-health" }).Cast() });
        public static readonly BookPlan StoneSkin = Write("stone-skin", "Stone Skin", new[] { Area("circle", Say("tight", "rest"), Give("stone-skin")).Cast() });
        public static readonly BookPlan RaiseBones = Write("raise-bones", "Raise Bones", new[] { Summoning(Raise("bones")) });
        public static readonly BookPlan Cleave = Write("cleave", "Cleave", new[] { Area("cone", Say("broad"), Deal()) });
        public static readonly BookPlan Sunder = Write("sunder", "Sunder", new[] { Area("cone", Say("deliberate"), Deal()) }, page => page.Mods = new[] { Flat(Stat.ArmourIgnore, 40f), Chance(Tags.Stun, 60f) });
        public static readonly BookPlan Knives = Write("knives", "Throwing Knives", new[] { Bolt(Say("three-at-once"), Deal()) });
        public static readonly BookPlan Sap = Write("sap", "Sap", new[] { Area("line", Say("tight"), Deal()).Cast() }, page => page.Mods = new[] { Flat(Stat.HealthLeech, 12f), Flat(Stat.ManaLeech, 6f) }, type: DamageType.Magic);
        public static readonly BookPlan Tidepool = Write("tidepool", "Tidepool", new[] { Area("circle", Say("from-cursor"), Do("water")).Cast().Lasting("lingering") }, page => page.Duration = Rulebook.WaterLinger);
        public static readonly BookPlan Split = Write("split", "Splitting Shot", new[] { Bolt(Plain, Deal()), Bolt(Say("three-at-once"), Deal()) });
        public static readonly BookPlan Inferno = Write("inferno", "Inferno", new[] { Area("circle", Plain, Deal()).Cast() }, mode: BookMode.Toggle, type: DamageType.Fire);
        public static readonly BookPlan Retort = Write("retort", "Retort", new[] { Area("circle", Plain, Deal()) }, mode: BookMode.Trigger, when: new BookTrigger { When = BookWhen.Moved, Amount = 20f }, type: DamageType.Lightning);

        // What uniques set off.
        public static readonly BookPlan Aftershock = Write("aftershock", "Aftershock", new[] { Area("circle", Plain, Deal()) }, page => { page.Potency = 0.8f; page.Length = 6f; }, triggered: true);
        public static readonly BookPlan Bristle = Write("bristle", "Bristling Spears", new[] { Area("circle", Plain, Deal()) }, page => { page.Potency = 0.6f; page.Length = 5f; }, triggered: true);
        public static readonly BookPlan Skyfall = Write("skyfall", "Skyfall", new[] { Bolt(Say("towards-nearest", "falls", "grounded"), Do("pass")), Area("circle", Plain, Deal()) }, page =>
        {
            if (page.Index == 0) return;
            page.Potency = 1.2f;
            page.Length = 4f;
            page.Mods = new[] { Chance(Tags.Shock, 50f) };
        }, triggered: true, type: DamageType.Lightning);
        public static readonly BookPlan StaticField = Write("static-field", "Static Field", new[] { Area("circle", Plain, Deal()) }, page => { page.Potency = 0.3f; page.Length = 8f; page.Mods = new[] { Chance(Tags.Shock, 100f) }; }, triggered: true, type: DamageType.Lightning);
        public static readonly BookPlan Kiln = Write("kiln", "Kiln Heat", new[] { Area("circle", Plain, Deal()) }, page => { page.Potency = 0.25f; page.Length = 7f; page.Mods = new[] { Chance(Tags.Burn, 100f) }; }, triggered: true, type: DamageType.Fire);
        public static readonly BookPlan EmberRing = Write("ember-ring", "Ember Ring", new[] { Area("circle", Plain, Deal()) }, page => { page.Potency = 0.7f; page.Length = 6f; page.Mods = new[] { Chance(Tags.Burn, 60f) }; }, triggered: true, type: DamageType.Fire);
        public static readonly BookPlan Trample = Write("trample", "Trample", new[] { Area("circle", Plain, Deal()) }, page => { page.Length = 4.5f; page.Mods = new[] { Chance(Tags.Stun, 40f) }; }, triggered: true);
        public static readonly BookPlan BoneSpurs = Write("bone-spurs", "Bone Spurs", new[] { Bolt(Say("five-at-once", "anywhere"), Deal()) }, page => page.Potency = 0.5f, triggered: true);

        // What a barrier comes to when it breaks: a share of it as Magic Damage, on top of whatever Spell Damage there is.
        public static readonly BookPlan LastWord = Write("last-word", "The Last Word", new[] { Area("circle", Plain, Deal()).Cast() }, page => { page.Potency = 2.5f; page.Length = 8f; page.Mods = new[] { Added(DamageType.Magic, 1f).For(Per.MaxBarrier, 1f) }; }, triggered: true);
        public static readonly BookPlan Steam = Write("steam", "Scalding Steam", new[] { Area("circle", Plain, Deal()).Lasting("lingering") }, page => { page.Potency = 0.4f; page.Length = 4f; }, triggered: true, type: DamageType.Fire);

        // What dinosaurs fight with: their jaws, their tails, their weight and their voices. Each reaches as far as the animal does from where it stands, which is its hips: a raptor's head is five metres
        // out in front of them, and a tyrant's ten.
        public static readonly BookPlan RakingBite = Write("raking-bite", "Raking Bite", new[] { Area("cone", Say("tight"), Deal()) }, page => { page.Potency = 1f; page.Length = 5.6f; page.UseTime = 0.45f; page.Mods = new[] { Chance(Tags.Bleed, 35f) }; });
        public static readonly BookPlan CrushingJaws = Write("crushing-jaws", "Crushing Jaws", new[] { Area("cone", Say("tight"), Deal()) }, page => { page.Potency = 1.5f; page.Length = 10.5f; page.Mods = new[] { Chance(Tags.Bleed, 50f), Flat(Stat.ArmourIgnore, 30f) }; });
        // An enemy does the first thing it has that's ready and reaches, and these come first, so each has a rest after it: a tyrant without them roared its stun at anyone in reach every second and a half.
        public static readonly BookPlan TailSweep = Write("tail-sweep", "Tail Sweep", new[] { Area("circle", Say("rest"), Deal()) }, page => { page.Potency = 1.6f; page.Length = 9.5f; page.Cooldown = 5f; page.Mods = new[] { Chance(Tags.Stun, 35f) }; });
        public static readonly BookPlan Stomp = Write("stomp", "Stomp", new[] { Area("cone", Say("broad", "deliberate"), Deal()) }, page => { page.Potency = 1.8f; page.Length = 8.5f; page.Cooldown = 4f; page.Mods = new[] { Chance(Tags.Stun, 60f) }; });
        // A roar takes long enough to look heavy, which is what tells it from a tail swept round the same circle. A gore is a blow, so it's a cone like a bite: a rectangle is a beam, and goes off like one.
        public static readonly BookPlan Roar = Write("roar", "Terrible Roar", new[] { Area("circle", Say("rest"), Inflict("stun")) }, page => { page.Length = 13f; page.UseTime = 1.2f; page.Cooldown = 9f; });
        public static readonly BookPlan Gore = Write("gore", "Gore", new[] { Area("cone", Say("tight"), Deal()) }, page => { page.Potency = 1.5f; page.Length = 7.5f; page.Mods = new[] { Chance(Tags.Stun, 45f) }; });
        public static readonly BookPlan HungryBite = Write("hungry-bite", "Hungry Bite", new[] { Area("cone", Say("tight"), Deal()) }, page => { page.Potency = 0.9f; page.Length = 3.4f; page.UseTime = 0.4f; page.Mods = new[] { Chance(Tags.Bleed, 20f) }; });

        // What an arena does to whoever's on its floor, which is nobody's to carry (ARENAS.md): the arena sets each off on its own time, from where it is, and it's marked like any blow. The Reich's
        // bolt-throwers on the tribune, the sun its priests' mirrors gather, the Ossuary's vents and the ash heaving where its dead come up, and the chandeliers of the Iron Apex, which come down and burn.
        // What's thrown is an Attack, and can be evaded. What burns is a Spell, which can't, and so whatever it falls on of the tyrant's side is sure to feel it: their Potency is half as much again, as
        // a creature's Spell Damage is a third less than its Attack Damage. None of them is Triggered, which is what something a hero or a creature carries sets off: they're the arena's own, released
        // by it as a creature's books are by the creature, by two actors made for them, who have both kinds of Damage (ArenaSystem.Hazards).
        public static readonly BookPlan BallistaBolt = Write("ballista-bolt", "Ballista Bolt", new[] { Area("line", Plain, Deal()) }, page => { page.Potency = 2.4f; page.Length = 90f; page.Width = 0.9f; page.Mods = new[] { Chance(Tags.Stun, 30f) }; });
        public static readonly BookPlan Sunfire = Write("sunfire", "Sunfire", new[] { Area("circle", Plain, Deal()).Cast() }, page => { page.Potency = 2.3f; page.Length = 3.2f; page.Mods = new[] { Chance(Tags.Burn, 60f) }; }, type: DamageType.Fire);
        public static readonly BookPlan Eruption = Write("eruption", "Eruption", new[] { Area("line", Plain, Deal()).Cast() }, page => { page.Potency = 2.6f; page.Length = 9f; page.Width = 1.5f; page.Mods = new[] { Chance(Tags.Burn, 50f) }; }, type: DamageType.Fire);
        public static readonly BookPlan Smoulder = Write("smoulder", "Smouldering Fissure", new[] { Area("line", Plain, Deal()).Cast().Lasting("lingering") }, page => { page.Potency = 0.45f; page.Length = 9f; page.Width = 1.3f; page.Duration = 4f; }, type: DamageType.Fire);
        public static readonly BookPlan Heave = Write("heave", "Heaving Ash", new[] { Area("circle", Plain, Deal()) }, page => { page.Potency = 1.3f; page.Length = 3.4f; page.Mods = new[] { Chance(Tags.Stun, 50f) }; });
        public static readonly BookPlan Crash = Write("crash", "Falling Chandelier", new[] { Area("circle", Plain, Deal()).Cast() }, page => { page.Potency = 4.9f; page.Length = 3.6f; page.Mods = new[] { Chance(Tags.Stun, 100f) }; }, type: DamageType.Fire);
        public static readonly BookPlan Wreck = Write("wreck", "Burning Wreck", new[] { Area("circle", Plain, Deal()).Cast().Lasting("lingering") }, page => { page.Potency = 0.45f; page.Length = 3.4f; page.Duration = 9f; page.Mods = new[] { Chance(Tags.Burn, 40f) }; }, type: DamageType.Fire);

        // What each kind of beast does that's its own, so that no two are the same fight (LORE.md, the Bestiary). A pounce, a charge and a ram carry whoever does them along: the brain takes it to
        // where the mark is as the blow lands (EnemyBrainSystem.Leaps), and the ground counts for the ride, so it comes down there whether or not anyone's waited for it. Each rests, as it comes first.
        private static Page Ride() => Bolt(Say("rides", "grounded"), Do("pass"));

        public static readonly BookPlan Pounce = Write("pounce", "Pounce", new[] { Ride(), Area("cone", Plain, Deal()) }, page =>
        {
            if (page.Index == 0) { page.Range = 17f; page.Cooldown = 9f; page.UseTime = 0.7f; }
            else { page.Potency = 1.5f; page.Length = 6.5f; page.Mods = new[] { Chance(Tags.Bleed, 60f) }; }
        });
        public static readonly BookPlan HornCharge = Write("horn-charge", "Horn Charge", new[] { Ride(), Area("cone", Plain, Deal()) }, page =>
        {
            if (page.Index == 0) { page.Range = 20f; page.Cooldown = 10f; page.UseTime = 1f; }
            else { page.Potency = 2f; page.Length = 8f; page.Mods = new[] { Chance(Tags.Stun, 70f), Flat(Stat.ArmourIgnore, 30f) }; }
        });
        public static readonly BookPlan Ram = Write("ram", "Skull Ram", new[] { Ride(), Area("cone", Plain, Deal()) }, page =>
        {
            if (page.Index == 0) { page.Range = 15f; page.Cooldown = 7f; page.UseTime = 0.7f; }
            else { page.Potency = 1.6f; page.Length = 5.5f; page.Mods = new[] { Chance(Tags.Stun, 100f) }; }
        });

        // A reaper's arms are as long as it is tall, with a scythe on each: what it sweeps is wide and far, and what it reaches bleeds. It takes long enough to look heavy, as that much ground wants seeing coming. A clubtail's club goes all the way round and stops whoever
        // it reaches, through whatever they're wearing. A wader takes what's at the water's edge from further off than it looks, in a narrow line of its snout.
        public static readonly BookPlan Reap = Write("reap", "Reaping Claws", new[] { Area("cone", Plain, Deal()) }, page => { page.Potency = 1.5f; page.Length = 9.5f; page.Width = 3.1f; page.UseTime = 1f; page.Cooldown = 4f; page.Mods = new[] { Chance(Tags.Bleed, 90f) }; });
        public static readonly BookPlan ScytheClaw = Write("scythe-claw", "Scythe Claw", new[] { Area("cone", Plain, Deal()) }, page => { page.Potency = 1.1f; page.Length = 7.5f; page.Width = 1.5f; page.UseTime = 0.5f; page.Mods = new[] { Chance(Tags.Bleed, 50f) }; });
        public static readonly BookPlan TailClub = Write("tail-club", "Tail Club", new[] { Area("circle", Plain, Deal()) }, page => { page.Potency = 1.9f; page.Length = 8.5f; page.UseTime = 1f; page.Cooldown = 6f; page.Mods = new[] { Chance(Tags.Stun, 100f), Flat(Stat.ArmourIgnore, 40f) }; });
        public static readonly BookPlan Lunge = Write("lunge", "Fisher's Lunge", new[] { Area("cone", Plain, Deal()) }, page => { page.Potency = 1.4f; page.Length = 13.5f; page.Width = 0.8f; page.UseTime = 0.55f; page.Cooldown = 3.5f; page.Mods = new[] { Chance(Tags.Bleed, 60f) }; });

        // What's spat by what the Ossuary made of a spitter. Bones have no venom left, and throw a mouthful of their own teeth, which the light grows back. What's rotting spits the bile that keeps
        // it going, which stays where it lands. A ghost's is its own cold, and the ground keeps that too. The Institute's weavers that are still in harness lay their venom down to hold whoever's
        // in it for the handlers. What's lobbed comes down where it was aimed, and the ground counts, so it lies there whether or not it fell on anyone.
        private static Page Lob() => Bolt(Say("falls", "grounded"), Do("pass")).Cast();
        private static Page Puddled() => Area("circle", Plain, Deal()).Cast().Lasting("lingering");

        public static readonly BookPlan SpatTeeth = Write("spat-teeth", "Spat Teeth", new[] { Bolt(Say("three-at-once"), Deal()) }, page => { page.Potency = 0.6f; page.Speed = 40f; page.Mods = new[] { Chance(Tags.Bleed, 40f) }; });
        public static readonly BookPlan BileSpit = Write("bile-spit", "Bile Spit", new[] { Lob(), Puddled() }, page =>
        {
            if (page.Index == 0) { page.Speed = 40f; page.Cooldown = 5f; }
            else { page.Potency = 0.9f; page.Length = 4.5f; page.Duration = 5f; page.Mods = new[] { Chance(Tags.Venom, 100f) }; }
        }, type: DamageType.Poison);
        public static readonly BookPlan GraveCold = Write("grave-cold", "Grave Cold", new[] { Lob(), Puddled() }, page =>
        {
            if (page.Index == 0) { page.Speed = 40f; page.Cooldown = 6f; }
            else { page.Potency = 0.8f; page.Length = 5f; page.Duration = 5f; page.Mods = new[] { Inc(Stat.AilmentEffect, 60f, Tags.Chill) }; }
        }, type: DamageType.Cold);
        public static readonly BookPlan Snare = Write("snare", "Snare", new[] { Lob(), Puddled() }, page =>
        {
            if (page.Index == 0) { page.Speed = 44f; page.Cooldown = 6f; }
            else { page.Potency = 0.6f; page.Length = 5.5f; page.Duration = 6f; page.Mods = new[] { Chance(Tags.Venom, 100f) }; }
        }, type: DamageType.Poison);

        // A shagback's ghost breathes out the cold it died in, and whoever it reaches stands frozen for what comes next. It takes long enough to look heavy, as a roar does.
        public static readonly BookPlan KillingFrost = Write("killing-frost", "Killing Frost", new[] { Area("circle", Say("rest"), Inflict("freeze")).Cast() }, page => { page.Length = 10f; page.UseTime = 1.2f; page.Cooldown = 10f; });

        // A wrangler's goad, for the beasts and the soldiers round them and not for whoever they're after: they're quicker for a while, so the handler is who to go for first.
        public static readonly BookPlan Goad = Write("goad", "Goad", new[] { Area("circle", Plain, new Outcome { Deed = "give", Gift = "quickening", Whom = "friends" }) }, page => { page.Length = 14f; page.UseTime = 0.8f; page.Cooldown = 12f; });

        // What's left where one of the Ossuary's failures falls: the glass on its back breaks, and the bile that kept it going lies where it lay. It lingers, so it can be walked out of, and
        // nothing it does comes all at once. They're Attacks, as whatever's set off by something else is.
        private static BookPlan Spilled(string id, string name, float radius, float seconds, float potency, float rest = 0f) => Write(id, name, new[] { Area("circle", Plain, Deal()).Lasting("lingering") }, page =>
        {
            page.Potency = potency;
            page.Length = radius;
            page.Duration = seconds;
            page.Cooldown = rest;
            page.Mods = new[] { Chance(Tags.Venom, 100f) };
        }, triggered: true, type: DamageType.Poison);

        public static readonly BookPlan BileSplash = Spilled("bile-splash", "Bile Splash", 2.4f, 3f, 0.5f);
        public static readonly BookPlan BilePool = Spilled("bile-pool", "Spilled Bile", 3.8f, 5f, 0.7f);
        public static readonly BookPlan BileFlood = Spilled("bile-flood", "Burst Bile Glass", 5.5f, 6f, 0.9f);
        public static readonly BookPlan BileSlop = Spilled("bile-slop", "Slopped Bile", 3.2f, 4f, 0.5f, rest: 5f);

        // A weaver spits before it bites, and then it comes on: its spit rests, where a spitter's doesn't, so it closes with whatever it's slowed. A spitter's ghost has only its light to spit.
        public static readonly BookPlan WeaverSpit = Write("weaver-spit", "Weaver's Spit", new[] { Bolt(Plain, Deal()).Cast() }, page => { page.Speed = 36f; page.Cooldown = 6f; page.Mods = new[] { Chance(Tags.Venom, 100f) }; }, type: DamageType.Poison);
        public static readonly BookPlan Wisp = Write("wisp", "Wisp", new[] { Bolt(Plain, Deal()).Cast() }, page => { page.Speed = 38f; page.Mods = new[] { Inc(Stat.AilmentEffect, 30f, Tags.Chill) }; }, type: DamageType.Cold);
    }
}

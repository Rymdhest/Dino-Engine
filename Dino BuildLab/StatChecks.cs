using Dino_Defenders.Books;
using Dino_Defenders.Stats;
using Dino_Defenders.Tooltips;
using Dino_Engine.World;
using System.Reflection;
using static Dino_Defenders.Stats.M;

namespace Dino_BuildLab
{
    /// <summary>The sums themselves, with numbers worked out by hand. If these are wrong, nothing above them can be right.</summary>
    internal static class StatChecks
    {
        private static readonly WeaponProfile Sword = new WeaponProfile(10f, 10f, 0.6f, 5f, Tags.Sword | Tags.OneHanded);

        public static ActorSheet Sheet(params Mod[] mods) => new Loadout(1, Sword, mods, Shelf.All).SheetFor(Condition.None);

        // What's held decides what a book is, and this one rolls exactly one and never crits, so that a page's share of it is exactly what it deals.
        private static readonly WeaponProfile Wand = new WeaponProfile(1f, 1f, 0.6f, 0f, Tags.Wand | Tags.OneHanded);

        private static BookPlan One(PagePlan page) => new BookPlan { Id = "probe", Pages = new[] { page } };

        // What a probe deals is what's in the hand, of exactly the type and amount asked for, as flat damage added to it would have a spread.
        private static readonly Dictionary<BookPlan, WeaponProfile> Held = new();

        private static BookPlan Spell(DamageType type, float damage, params Mod[] mods)
        {
            BookPlan plan = One(new PagePlan { Kind = PageKind.Area, Tags = Tags.Area | Tags.Spell, Deals = true, Mods = mods });
            Held[plan] = new WeaponProfile(damage, damage, 0.6f, 0f, Tags.Wand | Tags.OneHanded, type);
            return plan;
        }

        private static BookProfile Profile(BookPlan skill, params Mod[] mods)
        {
            ActorSheet sheet = new Loadout(1, Held.GetValueOrDefault(skill, Wand), mods, new[] { skill }).SheetFor(Condition.None);
            return sheet.Profile(0);
        }

        private static DamageTable HitWith(BookProfile profile, Condition foe = Condition.None)
        {
            var luck = new WorldRandom(1);
            HitPacket hit = default;
            Hits.Roll(profile.First, foe, luck, ref hit);
            return hit.Damage;
        }

        public static void Run(Report report)
        {
            report.Section("The rule: (base + flat) x (1 + all increases) x each more on its own");
            Arithmetic(report);

            report.Section("Scopes");
            Scopes(report);

            report.Section("Damage remembers what it has been");
            Conversion(report);

            report.Section("Conditions, foes, and what's counted per what");
            Conditional(report);

            report.Section("Stats pointed at other stats");
            Diversions(report);

            report.Section("A hit, from the other side");
            Defence(report);

            report.Section("Pools");
            Pools(report);

            report.Section("Nothing is dead");
            NothingIsDead(report);
        }

        private static void Arithmetic(Report report)
        {
            report.Near("+5 flat health on 100", Sheet(Flat(Stat.Health, 100f), Flat(Stat.Health, 5f)).MaxHealth, 105f);
            report.Near("20% and 30% increased pool into 50%", Sheet(Flat(Stat.Health, 100f), Inc(Stat.Health, 20f), Inc(Stat.Health, 30f)).MaxHealth, 150f);
            report.Near("Reduced comes out of the same pool: 50% increased, 20% reduced", Sheet(Flat(Stat.Health, 100f), Inc(Stat.Health, 50f), Inc(Stat.Health, -20f)).MaxHealth, 130f);
            report.Near("Each more multiplies alone: 20% and 30% more is 56%", Sheet(Flat(Stat.Health, 100f), More(Stat.Health, 20f), More(Stat.Health, 30f)).MaxHealth, 156f);
            report.Near("Less is a more below zero: 50% less", Sheet(Flat(Stat.Health, 100f), More(Stat.Health, -50f)).MaxHealth, 50f);
            report.Near("Flat comes first, so it's scaled by the rest", Sheet(Flat(Stat.Health, 80f), Flat(Stat.Health, 20f), Inc(Stat.Health, 100f), More(Stat.Health, 50f)).MaxHealth, 300f);
            report.Near("100% reduced is nothing, not a debt", Sheet(Flat(Stat.Armour, 100f), Inc(Stat.Armour, -250f)).Armour, 0f);
            report.Near("An override sets it outright", Sheet(Flat(Stat.Armour, 100f), Inc(Stat.Armour, 100f), Set(Stat.Armour, 7f)).Armour, 7f);
            report.Near("Of two overrides, the lowest stands", Sheet(Set(Stat.Armour, 7f), Set(Stat.Armour, 3f)).Armour, 3f);

            var random = new Random(3);
            var mods = new List<Mod> { Flat(Stat.Health, 100f) };
            for (int i = 0; i < 40; i++)
            {
                float value = random.Next(-20, 40);
                mods.Add(random.Next(3) switch { 0 => Flat(Stat.Health, value), 1 => Inc(Stat.Health, value), _ => More(Stat.Health, value * 0.1f) });
            }
            float first = Sheet(mods.ToArray()).MaxHealth;
            bool same = true;
            for (int i = 0; i < 50; i++)
            {
                Mod[] shuffled = mods.OrderBy(_ => random.Next()).ToArray();
                same &= MathF.Abs(Sheet(shuffled).MaxHealth - first) <= first * 1e-4f;
            }
            report.Check("Fifty shuffles of forty modifiers all come to the same", same);
        }

        private static void Scopes(Report report)
        {
            const int Fire = (int)DamageType.Fire;
            BookPlan fire = Spell(DamageType.Fire, 100f);
            report.Near("Fire damage helps a fire spell", HitWith(Profile(fire, Inc(Stat.Damage, 50f, Tags.Fire)))[Fire], 150f);
            report.Near("Spell damage helps it too, in the same pool", HitWith(Profile(fire, Inc(Stat.Damage, 50f, Tags.Fire), Inc(Stat.Damage, 50f, Tags.Spell)))[Fire], 200f);
            report.Near("Elemental damage helps it", HitWith(Profile(fire, Inc(Stat.Damage, 50f, Tags.Elemental)))[Fire], 150f);
            report.Near("Cold damage doesn't", HitWith(Profile(fire, Inc(Stat.Damage, 50f, Tags.Cold)))[Fire], 100f);
            report.Near("Attack damage doesn't", HitWith(Profile(fire, Inc(Stat.Damage, 50f, Tags.Attack)))[Fire], 100f);
            report.Near("A scope of two needs both", HitWith(Profile(fire, Inc(Stat.Damage, 50f, Tags.Fire | Tags.Attack)))[Fire], 100f);
            report.Near("Elemental doesn't reach poison", HitWith(Profile(Spell(DamageType.Poison, 100f), Inc(Stat.Damage, 50f, Tags.Elemental)))[(int)DamageType.Poison], 100f);

            BookPlan strike = One(new PagePlan { Kind = PageKind.Area, From = Start.You, Tags = Tags.Area | Tags.Attack, Deals = true });
            ActorSheet swordsman = new Loadout(1, Sword, new[] { Inc(Stat.Damage, 100f, Tags.Sword) }, new[] { strike }).SheetFor(Condition.None);
            report.Near("A weapon's kind reaches what's done with it", HitWith(swordsman.Profile(0))[(int)DamageType.Physical], 20f);
            ActorSheet other = new Loadout(1, Sword, new[] { Inc(Stat.Damage, 100f, Tags.Axe) }, new[] { strike }).SheetFor(Condition.None);
            report.Near("And not what's done with anything else", HitWith(other.Profile(0))[(int)DamageType.Physical], 10f);
            report.Check("A sword makes a book an Attack, and Melee when it's done by hand", (swordsman.Profile(0).First.Tags & (Tags.Attack | Tags.Melee)) == (Tags.Attack | Tags.Melee) && (swordsman.Profile(0).First.Tags & Tags.Spell) == 0);
            BookPlan cast = One(new PagePlan { Kind = PageKind.Area, From = Start.You, Hand = Hand.Spell, Tags = Tags.Area | Tags.Spell, Deals = true });
            ActorSheet casting = new Loadout(1, Sword, new[] { Inc(Stat.Damage, 100f, Tags.Sword) }, new[] { cast }).SheetFor(Condition.None);
            report.Check("A Spell Page is a Spell in a sword's hand, and no kind of Melee, nor done with the sword", casting.Profile(0).First.Tags.HasFlag(Tags.Spell) && (casting.Profile(0).First.Tags & (Tags.Attack | Tags.Melee | Tags.Sword)) == 0);

            float added = HitWith(Profile(fire, Added(DamageType.Fire, 50f), Inc(Stat.Damage, 100f)))[Fire];
            report.Check("Added damage is damage like any other: 50 around 100, then doubled", added is >= 280f and <= 320f, $"{added}");
            BookPlan light = Spell(DamageType.Fire, 100f);
            light.First.Potency = 0.5f;
            report.Near("And Potency takes all of it along", HitWith(Profile(light, Added(DamageType.Fire, 50f)))[Fire], HitWith(Profile(fire, Added(DamageType.Fire, 50f)))[Fire] * 0.5f);
        }

        private static void Conversion(Report report)
        {
            const int Physical = (int)DamageType.Physical;
            const int Fire = (int)DamageType.Fire;
            const int Cold = (int)DamageType.Cold;

            BookPlan phys = Spell(DamageType.Physical, 100f);
            DamageTable half = HitWith(Profile(phys, Convert(DamageType.Physical, DamageType.Fire, 50f)));
            report.Near("Half converted: 50 stays", half[Physical], 50f);
            report.Near("And 50 moves", half[Fire], 50f);

            DamageTable both = HitWith(Profile(phys, Convert(DamageType.Physical, DamageType.Fire, 100f), Inc(Stat.Damage, 50f, Tags.Physical), Inc(Stat.Damage, 50f, Tags.Fire)));
            report.Near("Converted damage is helped by both what it was and what it became", both[Fire], 200f);

            DamageTable chain = HitWith(Profile(phys, Convert(DamageType.Physical, DamageType.Cold, 100f), Convert(DamageType.Cold, DamageType.Fire, 100f),
                Inc(Stat.Damage, 100f, Tags.Physical), Inc(Stat.Damage, 100f, Tags.Cold), Inc(Stat.Damage, 100f, Tags.Fire)));
            report.Near("A chain of two is helped by all three", chain[Fire], 400f);
            report.Near("And what was physical has gone", chain[Physical], 0f);

            DamageTable loop = HitWith(Profile(Spell(DamageType.Fire, 100f), Convert(DamageType.Fire, DamageType.Cold, 100f), Convert(DamageType.Cold, DamageType.Fire, 100f)));
            report.Near("Fire to cold and cold to fire: nothing is lost", loop.Total, 100f);
            report.Near("And it can't turn back into what it was", loop[Cold], 100f);

            Mod[] everything = DamageKinds.All.SelectMany(from => DamageKinds.All.Where(to => to != from).Select(to => Convert(from, to, 20f))).ToArray();
            report.Near("Every type to every other at once ends, and adds up", HitWith(Profile(phys, everything)).Total, 100f, 0.01f);

            DamageTable over = HitWith(Profile(phys, Convert(DamageType.Physical, DamageType.Fire, 100f), Convert(DamageType.Physical, DamageType.Cold, 100f)));
            report.Near("Past 100%, conversions share: fire gets half", over[Fire], 50f);
            report.Near("Cold gets half", over[Cold], 50f);
            report.Near("And nothing's made from nothing", over.Total, 100f);

            DamageTable extra = HitWith(Profile(phys, GainAs(DamageType.Physical, DamageType.Fire, 30f)));
            report.Near("Gained as extra keeps what was there", extra[Physical], 100f);
            report.Near("And adds to it", extra[Fire], 30f);

            DamageTable gainFirst = HitWith(Profile(phys, Convert(DamageType.Physical, DamageType.Cold, 100f), GainAs(DamageType.Physical, DamageType.Fire, 50f)));
            report.Near("Gained from the whole, before any of it moves", gainFirst[Fire], 50f);
            report.Near("The conversion still takes all of it", gainFirst[Cold], 100f);

            BookPlan built = Spell(DamageType.Physical, 100f, Convert(DamageType.Physical, DamageType.Lightning, 100f));
            report.Near("A skill can do this on its own", HitWith(Profile(built))[(int)DamageType.Lightning], 100f);
        }

        private static void Conditional(Report report)
        {
            const int Fire = (int)DamageType.Fire;
            Mod[] mods = { Flat(Stat.Health, 100f), Inc(Stat.Health, 50f).While(Condition.Moving), Inc(Stat.Health, 50f).Unless(Condition.Moving), More(Stat.Health, 100f).While(Condition.InWater | Condition.Moving) };
            var loadout = new Loadout(1, Sword, mods, Shelf.All);
            report.Near("Unless holds when it isn't so", loadout.SheetFor(Condition.None).MaxHealth, 150f);
            report.Near("While holds when it is", loadout.SheetFor(Condition.Moving).MaxHealth, 150f);
            report.Near("Asking for two needs both", loadout.SheetFor(Condition.Moving | Condition.InWater).MaxHealth, 300f);
            report.Check("The same state is the same sheet, made once", ReferenceEquals(loadout.SheetFor(Condition.Moving), loadout.SheetFor(Condition.Moving)));
            report.Check("What no modifier asks about makes no new sheet", ReferenceEquals(loadout.SheetFor(Condition.Moving), loadout.SheetFor(Condition.Moving | Condition.Burning)));

            var plain = new Loadout(1, Sword, new[] { Flat(Stat.Health, 100f) }, Shelf.All);
            report.Check("An actor with nothing conditional has one sheet whatever happens", ReferenceEquals(plain.SheetFor(Condition.None), plain.SheetFor(Condition.Moving | Condition.Burning | Condition.LowHealth)));

            var granted = new Loadout(1, Sword, new[] { Flat(Stat.Health, 100f), Inc(Stat.Health, 100f).While(Condition.InWater), Grant(Condition.InWater) }, Shelf.All);
            report.Near("A condition can be granted outright", granted.SheetFor(Condition.None).MaxHealth, 200f);

            BookPlan fire = Spell(DamageType.Fire, 100f);
            BookProfile profile = Profile(fire, Inc(Stat.Damage, 100f).Against(Condition.Chilled), More(Stat.Damage, 50f, Tags.Fire).Against(Condition.Burning), Inc(Stat.Damage, 100f, Tags.Cold).Against(Condition.Burning));
            report.Near("Against a foe it doesn't fit, nothing", HitWith(profile)[Fire], 100f);
            report.Near("An increase against one that does joins the pool", HitWith(profile, Condition.Chilled)[Fire], 200f);
            report.Near("A more against one that does multiplies; scopes still count", HitWith(profile, Condition.Burning)[Fire], 150f);
            report.Near("Both at once", HitWith(profile, Condition.Burning | Condition.Chilled)[Fire], 300f);

            report.Near("Per: 1% increased per 100 armour, at 450, counts whole steps", Sheet(Flat(Stat.Armour, 450f), Flat(Stat.Health, 100f), Inc(Stat.Health, 1f).For(Per.Armour, 100f)).MaxHealth, 104f);
            report.Near("And stops at its cap", Sheet(Flat(Stat.Armour, 90000f), Flat(Stat.Health, 100f), Inc(Stat.Health, 1f).For(Per.Armour, 100f, 50f)).MaxHealth, 150f);

            ActorSheet circular = Sheet(Flat(Stat.Armour, 100f), Flat(Stat.Mana, 100f), Flat(Stat.Armour, 1f).For(Per.MaxMana, 1f), Flat(Stat.Mana, 1f).For(Per.Armour, 1f));
            report.Near("Two stats each per the other don't feed on themselves: armour", circular.Armour, 200f);
            report.Near("Mana", circular.MaxMana, 200f);

            var counted = new Loadout(1, Sword, new[] { Flat(Stat.Health, 100f), Inc(Stat.Health, 1f).For(Per.Gold, 100f, 40f) }, Shelf.All);
            report.Near("Counters count", counted.SheetFor(Condition.None, new Counters { Gold = 1234 }).MaxHealth, 112f);
            report.Near("And are read again when they change", counted.SheetFor(Condition.None, new Counters { Gold = 2000 }).MaxHealth, 120f);
            report.Near("With a cap of their own", counted.SheetFor(Condition.None, new Counters { Gold = 900000 }).MaxHealth, 140f);
        }

        private static void Diversions(Report report)
        {
            BookPlan swing = One(new PagePlan { Kind = PageKind.Area, From = Start.You, Tags = Tags.Area | Tags.Attack, UseTime = 0.6f, Deals = true });
            Mod[] mods = { Inc(Stat.AttackSpeed, 50f), Divert(Stat.AttackSpeed, Stat.Damage, 200f, Tags.Attack) };
            BookProfile diverted = new Loadout(1, Sword, mods, new[] { swing }).SheetFor(Condition.None).Profile(0);
            report.Near("A diverted stat no longer moves", diverted.First.UseTime, 0.6f);
            report.Near("Its increases apply elsewhere, at the rate given", HitWith(diverted)[(int)DamageType.Physical], 20f);

            BookProfile echoed = new Loadout(1, Sword, new[] { Inc(Stat.AttackSpeed, 50f), Echo(Stat.AttackSpeed, Stat.Damage, 100f, Tags.Attack) }, new[] { swing }).SheetFor(Condition.None).Profile(0);
            report.Near("An echoed stat keeps working", echoed.UseTime, 0.4f);
            report.Near("And also applies elsewhere", HitWith(echoed)[(int)DamageType.Physical], 15f);

            ActorSheet crossed = Sheet(Flat(Stat.Armour, 100f), Flat(Stat.Evasion, 100f), Inc(Stat.Armour, 50f), Inc(Stat.Evasion, 20f), Divert(Stat.Armour, Stat.Evasion), Divert(Stat.Evasion, Stat.Armour));
            report.Near("Two stats diverted into each other swap: armour", crossed.Armour, 120f);
            report.Near("Evasion", crossed.Evasion, 150f);
        }

        private static float Through(ActorSheet target, DamageType type, float amount, float penetration = 0f, Tags tags = Tags.Attack, float armourIgnore = 0f)
        {
            HitPacket hit = default;
            hit.Damage[(int)type] = amount;
            hit.Penetration[(int)type] = penetration;
            hit.Tags = tags;
            hit.ArmourIgnore = armourIgnore;
            return Hits.Mitigate(target, ref hit);
        }

        private static void Defence(Report report)
        {
            report.Near("Armour equal to five times a hit stops half of it", Through(Sheet(Flat(Stat.Armour, 500f)), DamageType.Physical, 100f), 50f);
            report.Near("A blow ten times the size barely notices the same armour", Through(Sheet(Flat(Stat.Armour, 500f)), DamageType.Physical, 1000f), 1000f * (1f - 500f / 5500f));
            report.Near("And armour never stops more than 90%", Through(Sheet(Flat(Stat.Armour, 10000000f)), DamageType.Physical, 100f), 10f);
            report.Near("Armour does nothing for fire", Through(Sheet(Flat(Stat.Armour, 500f)), DamageType.Fire, 100f), 100f);
            report.Near("Unless it's told to: half of it, at 250 against 100", Through(Sheet(Flat(Stat.Armour, 500f), Flat(Stat.ArmourApplies, 50f, Tags.Fire)), DamageType.Fire, 100f), 100f * (1f - 250f / 750f));
            report.Near("Ignoring half of it is the same as having half", Through(Sheet(Flat(Stat.Armour, 500f)), DamageType.Physical, 100f, 0f, Tags.Attack, 50f), 100f * (1f - 250f / 750f));

            report.Near("Resistance takes its share", Through(Sheet(Resist(Tags.Fire, 40f)), DamageType.Fire, 100f), 60f);
            report.Near("All elemental counts for fire", Through(Sheet(Resist(Tags.Elemental, 40f)), DamageType.Fire, 100f), 60f);
            report.Near("And not for poison", Through(Sheet(Resist(Tags.Elemental, 40f)), DamageType.Poison, 100f), 100f);
            report.Near("Unscoped counts for everything but physical", Through(Sheet(Resist(Tags.None, 40f)), DamageType.Poison, 100f), 60f);
            report.Near("It stops at 75", Through(Sheet(Resist(Tags.Fire, 300f)), DamageType.Fire, 100f), 25f);
            report.Near("The cap can move", Through(Sheet(Resist(Tags.Fire, 300f), Flat(Stat.MaxResistance, 5f, Tags.Fire)), DamageType.Fire, 100f), 20f);
            report.Near("Not past 90", Through(Sheet(Resist(Tags.Fire, 300f), Flat(Stat.MaxResistance, 100f, Tags.Fire)), DamageType.Fire, 100f), 10f);
            report.Near("Below zero it hurts", Through(Sheet(Resist(Tags.Fire, -50f)), DamageType.Fire, 100f), 150f);
            report.Near("Penetration goes through it", Through(Sheet(Resist(Tags.Fire, 60f)), DamageType.Fire, 100f, 20f), 60f);
            report.Near("Not further than nothing", Through(Sheet(Resist(Tags.Fire, 10f)), DamageType.Fire, 100f, 40f), 100f);

            ActorSheet flat = Sheet(Flat(Stat.FlatReduction, 7f));
            report.Near("Flat reduction comes off a hit", Through(flat, DamageType.Physical, 100f), 93f);
            report.Near("A hit smaller than it does nothing", Through(flat, DamageType.Physical, 5f), 0f);
            report.Near("Scoped to a type, it's only that type", Through(Sheet(Flat(Stat.FlatReduction, 7f, Tags.Fire)), DamageType.Cold, 100f), 100f);
            HitPacket mixed = default;
            mixed.Damage[(int)DamageType.Fire] = 50f;
            mixed.Damage[(int)DamageType.Cold] = 50f;
            report.Near("Unscoped, it comes off the hit once, not once per type", Hits.Mitigate(flat, ref mixed), 93f);

            report.Near("Reduced damage taken", Through(Sheet(Inc(Stat.DamageTaken, -20f)), DamageType.Physical, 100f), 80f);
            report.Near("Less damage taken, on top", Through(Sheet(Inc(Stat.DamageTaken, -20f), More(Stat.DamageTaken, -50f)), DamageType.Physical, 100f), 40f);
            report.Near("By type", Through(Sheet(More(Stat.DamageTaken, -50f, Tags.Fire)), DamageType.Fire, 100f), 50f);
            report.Near("By how it arrives", Through(Sheet(More(Stat.DamageTaken, -50f, Tags.Projectile)), DamageType.Fire, 100f, 0f, Tags.Projectile), 50f);
            report.Near("And not otherwise", Through(Sheet(More(Stat.DamageTaken, -50f, Tags.Projectile)), DamageType.Fire, 100f, 0f, Tags.Melee), 100f);

            ActorSheet shifted = Sheet(TakenAs(DamageType.Physical, DamageType.Fire, 50f), Resist(Tags.Fire, 50f), Flat(Stat.Armour, 250f));
            HitPacket taken = default;
            taken.Damage[(int)DamageType.Physical] = 100f;
            Hits.Mitigate(shifted, ref taken);
            report.Near("Taken as: half of it meets resistance as fire", taken.Dealt[(int)DamageType.Fire], 25f);
            report.Near("The rest meets armour as a smaller blow", taken.Dealt[(int)DamageType.Physical], 50f * (1f - 250f / 500f));

            HitPacket order = default;
            order.Damage[(int)DamageType.Physical] = 100f;
            ActorSheet stacked = Sheet(Flat(Stat.Armour, 100f), Flat(Stat.PhysicalReduction, 10f), Flat(Stat.FlatReduction, 10f, Tags.Physical), More(Stat.DamageTaken, -50f));
            float reduced = 100f * (1f - (0.1f + 100f / 600f));
            report.Near("In order: armour and reduction together, then flat, then what's taken", Hits.Mitigate(stacked, ref order), (reduced - 10f) * 0.5f);

            report.Near("Being shocked scales what gets through", MitigatedWith(Sheet(), 0f, 0.3f), 130f);
            report.Near("Being hexed takes resistance away", MitigatedWith(Sheet(Resist(Tags.None, 50f)), 30f, 0f), 80f);
            report.Check("Evasion is a chance, and has a ceiling", Hits.EvadeChance(1e9f, 1f) <= Hits.MostEvasion && Hits.EvadeChance(0f, 100f) == 0f && Hits.EvadeChance(600f, 100f) == 0.5f);

            ActorSheet block = Sheet(Flat(Stat.BlockChance, 30f), Flat(Stat.BlockChance, 20f, Tags.Spell), Flat(Stat.BlockChance, 100f, Tags.Attack));
            report.Near("Block against attacks stops at its cap", block.BlockAttacks, 75f);
            report.Near("Block against spells counts what's unscoped and what's for spells", block.BlockSpells, 50f);
        }

        private static float MitigatedWith(ActorSheet sheet, float resistanceLost, float takenIncrease)
        {
            HitPacket hit = default;
            hit.Damage[(int)DamageType.Fire] = 100f;
            return Hits.Mitigate(sheet, ref hit, Condition.None, resistanceLost, takenIncrease);
        }

        private static void Pools(Report report)
        {
            ActorSheet sheet = Sheet(Flat(Stat.Health, 100f), Flat(Stat.Mana, 100f), Flat(Stat.Barrier, 30f), Flat(Stat.TakenFromMana, 50f));
            Vitals vitals = Vitals.Full(sheet);
            Hits.Pay(sheet, ref vitals, 50f, 0f);
            report.Near("Barrier goes first", vitals.Barrier, 0f);
            report.Near("Then half of what's left from mana", vitals.Mana, 90f);
            report.Near("And health takes the rest", vitals.Health, 90f);

            var drained = new Vitals { Health = 100f, Mana = 5f };
            Hits.Pay(sheet, ref drained, 40f, 0f);
            report.Near("Mana gives only what it has", drained.Mana, 0f);
            report.Near("And health covers the difference", drained.Health, 65f);

            Vitals poisoned = Vitals.Full(sheet);
            HitPacket venom = default;
            venom.Damage[(int)DamageType.Poison] = 20f;
            Hits.Mitigate(sheet, ref venom);
            Hits.Pay(sheet, ref poisoned, venom);
            report.Near("Poison seeps past barrier", poisoned.Barrier, 30f);

            ActorSheet debtor = Sheet(Flat(Stat.Health, 100f), Rules(Rule.DeferredDamage));
            Vitals owing = Vitals.Full(debtor);
            Hits.Pay(debtor, ref owing, 60f, 0f);
            report.Near("A debtor loses nothing at once", owing.Health, 100f);
            report.Near("But owes it", owing.Debt, 60f);
            for (int i = 0; i < 60; i++) Hits.Recover(debtor, ref owing, 1f / 60f, 0f);
            report.Check("And pays it as time goes by", owing.Debt < 60f && owing.Health < 100f && MathF.Abs((100f - owing.Health) - (60f - owing.Debt)) < 0.01f, $"{owing.Health} health, {owing.Debt} owed");

            ActorSheet cup = Sheet(Flat(Stat.Health, 100f), Flat(Stat.Barrier, 50f), Rules(Rule.OverhealBarrier));
            var overflowing = new Vitals { Health = 90f };
            float healed = Hits.Heal(cup, ref overflowing, 30f);
            report.Near("Healing stops at full", healed, 10f);
            report.Near("What's past it can become barrier", overflowing.Barrier, 20f);

            ActorSheet regen = Sheet(Flat(Stat.Health, 100f), Flat(Stat.HealthRegen, 10f), Flat(Stat.HealthRegenPercent, 5f), Inc(Stat.Recovery, 100f));
            report.Near("Regeneration is flat and percent together, and Recovery scales it", regen.HealthRegen, 30f);
            report.Near("A rule can stop it", Sheet(Flat(Stat.Health, 100f), Flat(Stat.HealthRegen, 10f), Rules(Rule.NoRegen)).HealthRegen, 0f);

            ActorSheet barrier = Sheet(Flat(Stat.Barrier, 100f));
            var broken = new Vitals { Health = 1f };
            Hits.Recover(barrier, ref broken, 1f, 0f);
            report.Near("Barrier doesn't recharge right after a hit", broken.Barrier, 0f);
            Hits.Recover(barrier, ref broken, 1f, 5f);
            report.Near("And does once it has been left alone", broken.Barrier, 20f);
        }

        // Every stat has to change something, or it's a line on an item that does nothing, which is the worst thing an item can have.
        private static void NothingIsDead(Report report)
        {
            var weapon = new WeaponProfile(20f, 30f, 0.6f, 5f, Tags.Sword | Tags.OneHanded);
            var wand = new WeaponProfile(20f, 30f, 0.6f, 5f, Tags.Wand | Tags.OneHanded);
            BookPlan[] skills = Shelf.All.ToArray();
            Mod[] floor = { Flat(Stat.Health, 100f), Flat(Stat.Mana, 100f), Flat(Stat.Barrier, 100f), Flat(Stat.Armour, 100f), Flat(Stat.Evasion, 100f), Flat(Stat.MoveSpeed, 10f), Flat(Stat.HealthRegen, 1f), Flat(Stat.ManaRegen, 1f) };

            // Some stats only show against others: a cap needs something pressing on it.
            Mod[][] floors = { floor, floor.Append(Flat(Stat.BlockChance, 200f)).ToArray() };

            var dead = new List<string>();
            for (Stat stat = 0; stat < Stat.Count; stat++)
            {
                bool lives = false;
                foreach (Mod[] start in floors)
                {
                    // What's in the hands decides what a book is, and some stats only speak to one or the other.
                    foreach (WeaponProfile held in new[] { weapon, wand })
                    {
                        string plain = Fingerprint(new Loadout(1, held, start, skills).SheetFor(Condition.None));
                        foreach (Mod probe in Probes(stat))
                        {
                            lives |= Fingerprint(new Loadout(1, held, start.Append(probe), skills).SheetFor(Condition.None)) != plain;
                        }
                    }
                }
                if (!lives) dead.Add(stat.ToString());
            }
            report.Check($"All {(int)Stat.Count} stats change something", dead.Count == 0, string.Join(", ", dead));

            var unnamed = new List<string>();
            for (Stat stat = 0; stat < Stat.Count; stat++)
            {
                if (string.IsNullOrEmpty(StatNames.Of(stat).Name)) unnamed.Add(stat.ToString());
            }
            report.Check("All of them have a name", unnamed.Count == 0, string.Join(", ", unnamed));
        }

        private static IEnumerable<Mod> Probes(Stat stat)
        {
            yield return Flat(stat, 40f);
            yield return Inc(stat, 40f);
            yield return Inc(stat, 40f, Tags.Fire);
            foreach (Ailment ailment in Enum.GetValues<Ailment>())
            {
                yield return Flat(stat, 40f, AilmentRules.Tag(ailment));
                yield return Inc(stat, 40f, AilmentRules.Tag(ailment));
            }
            yield return Flat(stat, 40f) with { From = DamageType.Physical, To = DamageType.Fire };
        }

        // Everything a sheet comes to, as text, along with everything it does with every skill.
        private static string Fingerprint(ActorSheet sheet)
        {
            var text = new System.Text.StringBuilder();
            AppendFields(text, sheet);
            for (int i = 0; i < sheet.Loadout.Books.Length; i++)
            {
                AppendFields(text, sheet.Profile(i));
                foreach (PageProfile page in sheet.Profile(i).Pages) AppendFields(text, page);
            }
            return text.ToString();
        }

        private static void AppendFields(System.Text.StringBuilder text, object target)
        {
            foreach (FieldInfo field in target.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object? value = field.GetValue(target);
                switch (value)
                {
                    case float or int or bool or Rule or Tags:
                        text.Append(value).Append(',');
                        break;
                    case DamageTable table:
                        for (int i = 0; i < DamageKinds.Count; i++) text.Append(table[i]).Append(',');
                        break;
                    case float[] numbers:
                        foreach (float number in numbers) text.Append(number).Append(',');
                        break;
                    case AilmentProfile[] ailments:
                        foreach (AilmentProfile ailment in ailments) text.Append(ailment.Chance).Append(',').Append(ailment.Duration).Append(',').Append(ailment.Effect).Append(',');
                        break;
                }
            }

            if (target is PageProfile profile)
            {
                foreach (DamageFlow flow in profile.Flows) text.Append(flow.Type).Append(flow.Min).Append(',').Append(flow.Max).Append(',').Append(flow.Increased).Append(',').Append(flow.More).Append(',');
            }
        }
    }
}

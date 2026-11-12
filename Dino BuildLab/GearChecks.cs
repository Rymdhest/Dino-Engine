using Dino_Defenders.Net;
using Dino_Defenders.Progression;
using Dino_Defenders.Stats;
using Dino_Engine.Networking;
using Dino_Engine.World;

namespace Dino_BuildLab
{
    /// <summary>The rules of what's worn: ten places on a hero, what fits where, what a family is, and that a look can be found for anything, however old. See GEAR.md.</summary>
    internal static class GearChecks
    {
        public static void Run(Report report)
        {
            report.Section("Places on a hero");
            Places(report);
            Fingers(report);
            Hands(report);
            DualWielding(report);
            FullPack(report);

            report.Section("Weapons");
            Grips(report);
            Pacing(report);
            OldWeapons(report);

            report.Section("Families");
            Families(report);
            Leaning(report);

            report.Section("Looks");
            Seeds(report);
            OldLooks(report);
            Traits(report);
            Uniforms(report);

            report.Section("Saves from before there were ten places");
            OldPlaces(report);
            RealSaves(report);
        }

        private static void Places(Report report)
        {
            report.Check("There are ten", Enum.GetValues<WornSlot>().Length == 10);
            report.Check("Every place holds one kind of thing, and every kind of thing has a place", Enum.GetValues<ItemSlot>().Where(s => s < ItemSlot.Book).All(s => WornSlots.For(s).All(p => WornSlots.Holds(p) == s)) && Enum.GetValues<WornSlot>().All(p => WornSlots.For(WornSlots.Holds(p)).Contains(p)));
            report.Check("The first place for something is the first place it can go", Enum.GetValues<ItemSlot>().Where(s => s < ItemSlot.Book).All(s => WornSlots.First(s) == WornSlots.For(s)[0]));
            report.Check("A ring goes on either hand, and nothing else does", WornSlots.For(ItemSlot.Finger).Count == 2 && Enum.GetValues<ItemSlot>().Where(s => s != ItemSlot.Finger && s < ItemSlot.Book).All(s => WornSlots.For(s).Count == 1));
        }

        private static Item Ring(string name, string affix, float value) => new Item { Name = name, Slot = ItemSlot.Finger, Rarity = Rarity.Magic, Level = 5, Lines = { new ItemLine(affix, value) } };

        private static HeroState Carrying(params Item[] items)
        {
            var hero = new HeroState();
            foreach (Item item in items) hero.Pickup(item);
            return hero;
        }

        private static void Fingers(Report report)
        {
            Item first = Ring("First", "gold-find", 10f), second = Ring("Second", "gold-find", 20f), third = Ring("Third", "gold-find", 40f), fourth = Ring("Fourth", "gold-find", 80f);
            HeroState hero = Carrying(first, second, third, fourth);

            hero.Equip(first);
            hero.Equip(second);
            report.Check("Two rings fill two fingers", hero.Equipped[WornSlot.Finger1] == first && hero.Equipped[WornSlot.Finger2] == second && hero.Carried().Count() == 2);
            report.Check("And both of them count", HeroBuild.Compile(hero).SheetFor(Condition.None).GoldFind == 30f);

            hero.Equip(third);
            report.Check("A third goes on the first finger, and takes that ring off", hero.Equipped[WornSlot.Finger1] == third && hero.Equipped[WornSlot.Finger2] == second && hero.Carried().Contains(first));

            hero.Equip(fourth, WornSlot.Finger2);
            report.Check("Or on the one it's asked to go on", hero.Equipped[WornSlot.Finger1] == third && hero.Equipped[WornSlot.Finger2] == fourth && hero.Carried().Contains(second));

            Item wandering = Ring("Wandering", "gold-find", 1f);
            hero.Pickup(wandering);
            hero.Equip(wandering, WornSlot.Head);
            report.Check("Asking for somewhere it can't go is the same as not asking", !hero.Equipped.ContainsKey(WornSlot.Head) && hero.Equipped.ContainsValue(wandering));

            hero.Unequip(WornSlot.Finger1);
            report.Check("Each comes off by itself", !hero.Equipped.ContainsKey(WornSlot.Finger1) && hero.Equipped.ContainsKey(WornSlot.Finger2));
        }

        private static Item Weapon(WeaponKind kind) => new Item { Name = kind.ToString(), Slot = ItemSlot.MainHand, Kind = kind, Level = 1, WeaponDamage = 5f };

        private static Item Shield() => new Item { Name = "Shield", Slot = ItemSlot.OffHand, Level = 1, Armour = 5f };

        private static void Hands(Report report)
        {
            Item sword = Weapon(WeaponKind.Sword), staff = Weapon(WeaponKind.Staff), shield = Shield();
            report.Check("A sword takes one hand and a staff takes two", !sword.IsTwoHanded && staff.IsTwoHanded);

            HeroState hero = Carrying(sword, shield, staff);
            hero.Equip(sword);
            hero.Equip(shield);
            report.Check("One hand leaves the other free", hero.Equipped[WornSlot.MainHand] == sword && hero.Equipped[WornSlot.OffHand] == shield);

            hero.Equip(staff);
            report.Check("Two hands take the off hand with them", hero.Equipped[WornSlot.MainHand] == staff && !hero.Equipped.ContainsKey(WornSlot.OffHand) && hero.Carried().Contains(shield) && hero.Carried().Contains(sword));

            hero.Equip(shield);
            report.Check("And something in the off hand puts down what needs two", hero.Equipped[WornSlot.OffHand] == shield && !hero.Equipped.ContainsKey(WornSlot.MainHand) && hero.Carried().Contains(staff));

            hero.Equip(sword);
            report.Check("A sword goes back beside it", hero.Equipped[WornSlot.MainHand] == sword && hero.Equipped[WornSlot.OffHand] == shield);

            report.Check("A shield in the off hand counts as a shield to the rules", (HeroBuild.Compile(hero).Granted & Condition.HoldingShield) != 0);
        }

        private static void DualWielding(Report report)
        {
            Item sword = Weapon(WeaponKind.Sword), dagger = Weapon(WeaponKind.Dagger), staff = Weapon(WeaponKind.Staff), shield = Shield();
            dagger.WeaponDamage = 9f;
            report.Check("A weapon that takes one hand goes in either, and one that takes two only in the main", WornSlots.For(sword).SequenceEqual(new[] { WornSlot.MainHand, WornSlot.OffHand }) && WornSlots.For(staff).SequenceEqual(new[] { WornSlot.MainHand }) && WornSlots.For(shield).SequenceEqual(new[] { WornSlot.OffHand }));

            HeroState hero = Carrying(sword, dagger, staff, shield);
            hero.Equip(sword);
            hero.Equip(dagger);
            report.Check("A second weapon goes in the main hand unless it's put in the other", hero.Equipped[WornSlot.MainHand] == dagger && !hero.Equipped.ContainsKey(WornSlot.OffHand) && hero.Carried().Contains(sword));

            report.Check("And can be put there", hero.Move(ItemPlace.Cell(Array.IndexOf(hero.Inventory, sword)), ItemPlace.Worn(WornSlot.OffHand)) && hero.Equipped[WornSlot.OffHand] == sword && hero.Equipped[WornSlot.MainHand] == dagger);

            Loadout both = HeroBuild.Compile(hero);
            WeaponProfile paired = both.Weapon, alone = sword.Weapon(), other = dagger.Weapon();
            report.Check("One in each hand is Dual Wielding, and isn't holding a Shield", (both.Granted & Condition.DualWielding) != 0 && (both.Granted & (Condition.HoldingShield | Condition.Unarmed | Condition.TwoHanded)) == 0);
            report.Check("Two weapons strike as their average, faster, and are both kinds", MathF.Abs(paired.Min - (alone.Min + other.Min) * 0.5f) < 1e-4f && paired.AttackTime < (alone.AttackTime + other.AttackTime) * 0.5f && (paired.Tags & (Tags.Sword | Tags.Dagger)) == (Tags.Sword | Tags.Dagger));
            report.Check("It's the same whichever is in which hand", Item.Paired(alone, other) == Item.Paired(other, alone));
            report.Check("Each brings what its kind does", both.Mods.Any(mod => mod.Stat == Stat.CritMultiplier && mod.Value == 20f) && both.Mods.Any(mod => mod.Stat == Stat.Accuracy && mod.Value == 20f));

            report.Check("They change hands as rings do", hero.Move(ItemPlace.Worn(WornSlot.OffHand), ItemPlace.Worn(WornSlot.MainHand)) && hero.Equipped[WornSlot.MainHand] == sword && hero.Equipped[WornSlot.OffHand] == dagger);

            hero.Move(ItemPlace.Worn(WornSlot.OffHand), ItemPlace.Cell(Array.IndexOf(hero.Inventory, shield)));
            report.Check("A shield on a weapon in the off hand swaps the two", hero.Equipped[WornSlot.OffHand] == shield && hero.Carried().Contains(dagger));
            report.Check("And a shield can't change hands with a sword", !hero.Move(ItemPlace.Worn(WornSlot.OffHand), ItemPlace.Worn(WornSlot.MainHand)) && hero.Equipped[WornSlot.MainHand] == sword && hero.Equipped[WornSlot.OffHand] == shield);

            hero.Move(ItemPlace.Cell(Array.IndexOf(hero.Inventory, dagger)), ItemPlace.Worn(WornSlot.OffHand));
            hero.Equip(staff);
            report.Check("Two hands take a second weapon off as well", hero.Equipped[WornSlot.MainHand] == staff && !hero.Equipped.ContainsKey(WornSlot.OffHand) && hero.Carried().Contains(dagger) && hero.Carried().Contains(sword));
            report.Check("What needs two can't go in the off hand", !hero.Move(ItemPlace.Worn(WornSlot.MainHand), ItemPlace.Worn(WornSlot.OffHand)));

            hero.Move(ItemPlace.Cell(Array.IndexOf(hero.Inventory, dagger)), ItemPlace.Worn(WornSlot.OffHand));
            report.Check("A weapon put in the off hand puts down what needs two", hero.Equipped[WornSlot.OffHand] == dagger && !hero.Equipped.ContainsKey(WornSlot.MainHand) && hero.Carried().Contains(staff));
            Loadout lone = HeroBuild.Compile(hero);
            report.Check("A weapon alone in the off hand is what's attacked with, and isn't Bare Hands", lone.Weapon == dagger.Weapon() && (lone.Granted & (Condition.Unarmed | Condition.DualWielding | Condition.HoldingShield)) == 0);

            HeroState worn = Carrying(Weapon(WeaponKind.Axe));
            worn.Equipped[WornSlot.MainHand] = sword;
            worn.Equipped[WornSlot.OffHand] = dagger;
            report.Check("A weapon is compared against both weapons held", worn.WornInPlaceOf(worn.Inventory[0]!).SequenceEqual(new[] { sword, dagger }));
            worn.Equipped[WornSlot.OffHand] = shield;
            report.Check("But not against a shield, which it wouldn't take off", worn.WornInPlaceOf(worn.Inventory[0]!).SequenceEqual(new[] { sword }));
        }

        // Every weapon there is, at a level everything can be found at.
        private static List<Item> EveryWeapon()
        {
            var random = new WorldRandom(23);
            return ItemGenerator.BaseNames.Select(name => ItemGenerator.Plain(name, 40, random)).Where(item => item.Slot == ItemSlot.MainHand).ToList();
        }

        private static void Grips(Report report)
        {
            List<Item> weapons = EveryWeapon();
            foreach (WeaponKind kind in Enum.GetValues<WeaponKind>())
            {
                Grip grip = WeaponKinds.Of(kind).Grip;
                List<Item> made = weapons.Where(item => item.Kind == kind).ToList();
                bool one = made.Any(item => !item.IsTwoHanded), two = made.Any(item => item.IsTwoHanded);
                string how = grip switch { Grip.Either => "in one hand and in two", Grip.Two => "in two hands only", _ => "in one hand only" };
                report.Check($"Every {kind} comes {how}", made.Count > 0 && grip switch { Grip.Either => one && two, Grip.Two => two && !one, _ => one && !two });
            }
            report.Check("Swords, axes and hammers come both ways, and a spear is a javelin in one hand", new[] { WeaponKind.Sword, WeaponKind.Axe, WeaponKind.Hammer }.All(kind => WeaponKinds.Of(kind).Grip == Grip.Either) && WeaponKinds.Of(WeaponKind.Spear).Grip == Grip.One);

            report.Check("What a look is said with knows how many hands a weapon takes", weapons.All(item => ItemLook.TwoHandedOf(ItemLook.Traits(item)) == item.IsTwoHanded), string.Join(", ", weapons.Where(item => ItemLook.TwoHandedOf(ItemLook.Traits(item)) != item.IsTwoHanded).Select(item => item.Base)));
            bool Great(Item item) => ItemLook.SpecOf(item).Kind is Dino_Engine.Modelling.Procedural.Gear.GearKind.Greatsword or Dino_Engine.Modelling.Procedural.Gear.GearKind.Greataxe or Dino_Engine.Modelling.Procedural.Gear.GearKind.Greathammer;
            report.Check("And grows a sword, an axe or a hammer that takes two hands as a shape of its own", weapons.All(item => Great(item) == (item.IsTwoHanded && item.Kind is WeaponKind.Sword or WeaponKind.Axe or WeaponKind.Hammer)));
            report.Check("A blow says how many hands it took", weapons.All(item => ((item.Weapon().Tags & Tags.TwoHanded) != 0) == item.IsTwoHanded && ((item.Weapon().Tags & Tags.OneHanded) != 0) == !item.IsTwoHanded));
            report.Check("And one with two swings slower than the same kind with one", weapons.Where(item => item.IsTwoHanded && WeaponKinds.Of(item.Kind).Grip == Grip.Either).All(item => item.Weapon().AttackTime > WeaponKinds.Of(item.Kind).AttackTime));

            Item greatsword = weapons.First(item => item.Base == "Greatsword"), longsword = weapons.First(item => item.Base == "Longsword"), smith = weapons.First(item => item.Base == "Smith's Hammer"), javelin = weapons.First(item => item.Base == "Javelin");
            Item guard = Shield();
            HeroState hero = Carrying(greatsword, longsword, smith, javelin, guard);
            hero.Equip(javelin);
            hero.Equip(guard);
            report.Check("A javelin leaves a hand free for a shield", hero.Equipped[WornSlot.MainHand] == javelin && hero.Equipped.ContainsKey(WornSlot.OffHand));
            report.Check("And so does a hammer made for one hand", hero.Equip(smith) && hero.Equipped[WornSlot.MainHand] == smith && hero.Equipped.ContainsKey(WornSlot.OffHand));
            report.Check("But a greatsword takes both", hero.Equip(greatsword) && hero.Equipped[WornSlot.MainHand] == greatsword && !hero.Equipped.ContainsKey(WornSlot.OffHand) && (HeroBuild.Compile(hero).Granted & Condition.TwoHanded) != 0);
            report.Check("And a longsword one, with the other free for a second", !longsword.IsTwoHanded && WornSlots.For(longsword).Count == 2 && WornSlots.For(greatsword).Count == 1);
        }

        // A two-handed make is about a quarter more damage over time than the same kind in one hand at the same level, and a javelin or a hammer in one hand about what a sword is. Between what a
        // kind's one-handed bases hit for, a level's is drawn straight across, and past the last it goes on as the last two did.
        private static void Pacing(Report report)
        {
            List<Item> weapons = EveryWeapon();
            float OneHanded(WeaponKind kind, int level)
            {
                var ladder = weapons.Where(item => item.Kind == kind && !item.IsTwoHanded).Select(item => ItemGenerator.WeaponBase(item.Base)).OrderBy(b => b.MinLevel).ToList();
                if (ladder.Count == 1) return ladder[0].Damage;
                int at = 1;
                while (at < ladder.Count - 1 && ladder[at].MinLevel < level) at++;
                (float low, int from) = ladder[at - 1];
                (float high, int to) = ladder[at];
                return low + (high - low) * (level - from) / (float)(to - from);
            }
            float Rate(WeaponKind kind, bool both, float damage) => damage / WeaponKinds.Of(kind).TimeFor(both);

            foreach (WeaponKind kind in new[] { WeaponKind.Sword, WeaponKind.Axe, WeaponKind.Hammer })
            {
                var ratios = weapons.Where(item => item.Kind == kind && item.IsTwoHanded).Select(item => ItemGenerator.WeaponBase(item.Base)).Select(b => Rate(kind, true, b.Damage) / Rate(kind, false, OneHanded(kind, b.MinLevel))).ToList();
                report.Check($"Two hands on {kind}s hit a quarter harder over time than one, at the same level", ratios.All(r => r is > 1.15f and < 1.4f), string.Join(", ", ratios.Select(r => r.ToString("0.00"))));
            }
            foreach (WeaponKind kind in new[] { WeaponKind.Hammer, WeaponKind.Spear })
            {
                var ratios = weapons.Where(item => item.Kind == kind && !item.IsTwoHanded).Select(item => ItemGenerator.WeaponBase(item.Base)).Select(b => Rate(kind, false, b.Damage) / Rate(WeaponKind.Sword, false, OneHanded(WeaponKind.Sword, b.MinLevel))).ToList();
                report.Check($"A {kind} in one hand hits about as hard over time as a sword", ratios.All(r => r is > 0.9f and < 1.15f), string.Join(", ", ratios.Select(r => r.ToString("0.00"))));
            }
        }

        private const string VersionThirteen = """
            {
              "Version": 13,
              "Hero": {
                "Level": 25, "Experience": 0, "Gold": 10,
                "Inventory": [
                  { "Name": "Sturdy Boar Spear", "Slot": "MainHand", "Rarity": "Magic", "Level": 20, "WeaponDamage": 26, "Kind": "Spear", "Base": "Boar Spear", "Seed": 5, "Lines": [] },
                  { "Name": "Partisan", "Slot": "MainHand", "Rarity": "Normal", "Level": 20, "WeaponDamage": 60, "Kind": "Spear", "Base": "Partisan", "Seed": 6, "Lines": [] },
                  { "Name": "Old Glaive", "Slot": "MainHand", "Rarity": "Normal", "Level": 22, "WeaponDamage": 100, "Kind": "Spear", "Seed": 7, "Lines": [] },
                  { "Name": "The Long Arm", "Slot": "MainHand", "Rarity": "Unique", "Level": 22, "WeaponDamage": 124, "Kind": "Spear", "Base": "Glaive", "Unique": "long-arm", "Lines": [] },
                  { "Name": "Maul", "Slot": "MainHand", "Rarity": "Normal", "Level": 5, "WeaponDamage": 20, "Kind": "Mace", "Base": "Maul", "Seed": 8, "Lines": [] }
                ],
                "Equipped": {}
              },
              "WorldSeed": 1, "WorldLevel": 1
            }
            """;

        // Before 2026-11-12 a hammer was a Mace, and a spear took two hands and hit as hard as anything that does. Both come across as what they are now, and a spear's damage comes down once.
        private static void OldWeapons(Report report)
        {
            string path = Path.Combine(Path.GetTempPath(), $"buildlab-weapons-{Environment.ProcessId}.json");
            File.WriteAllText(path, VersionThirteen);
            SaveGame.PathOverride = path;
            SaveGame? first = SaveGame.Load();
            first?.Store();
            SaveGame? again = SaveGame.Load();
            again?.Store();
            SaveGame? third = SaveGame.Load();
            File.Delete(path);
            SaveGame.PathOverride = null;

            report.Check("A save with hammers and spears from before loads", first != null && third != null);
            if (first == null || third == null) return;
            Item[] carried = first.Hero.Carried().ToArray();
            Item Named(Item[] items, string name) => items.First(item => item.Name.Contains(name) || item.Unique == name);
            report.Check("A Mace is a Hammer, and still takes two hands", Named(carried, "Maul").Kind == WeaponKind.Hammer && Named(carried, "Maul").IsTwoHanded);
            report.Check("An old spear is the javelin it became, by name", Named(carried, "Hunting Spear").Base == "Hunting Spear" && Named(carried, "Winged Spear").Base == "Winged Spear" && Named(carried, "Old Pilum").Base == "Pilum" && Named(carried, "long-arm").Base == "Pilum");
            report.Check("And takes one hand", carried.Where(item => item.Kind == WeaponKind.Spear).All(item => !item.IsTwoHanded));
            bool Near(float a, float b) => a == MathF.Round(b);
            report.Check("Its damage comes down by as much as its base's did, to a whole number as everything's is", Near(Named(carried, "Hunting Spear").WeaponDamage, 20f) && Near(Named(carried, "Winged Spear").WeaponDamage, 50f) && Near(Named(carried, "Old Pilum").WeaponDamage, 100f * 52f / 62f) && Near(Named(carried, "long-arm").WeaponDamage, 124f * 52f / 62f), string.Join(", ", carried.Select(item => $"{item.Name} {item.WeaponDamage:0.##}")));
            Item[] later = third.Hero.Carried().ToArray();
            report.Check("Only ever once, however often it's saved and loaded", later.Length == carried.Length && later.Zip(carried).All(pair => pair.First.WeaponDamage == pair.Second.WeaponDamage && pair.First.Base == pair.Second.Base && pair.First.Name == pair.Second.Name));
        }

        private static void FullPack(Report report)
        {
            Item sword = Weapon(WeaponKind.Sword), staff = Weapon(WeaponKind.Staff), shield = Shield();
            HeroState hero = Carrying(sword, shield, staff);
            hero.Equip(sword);
            hero.Equip(shield);
            while (hero.Carried().Count() < HeroState.InventorySize) hero.Pickup(Ring("Filler", "gold-find", 1f));

            report.Check("Two things can't come off into room for one", !hero.Equip(staff) && hero.Equipped[WornSlot.MainHand] == sword && hero.Equipped[WornSlot.OffHand] == shield && hero.Carried().Contains(staff));

            hero.Inventory[^1] = null;
            report.Check("But can into room for two", hero.Equip(staff) && hero.Equipped[WornSlot.MainHand] == staff && hero.Carried().Count() == HeroState.InventorySize);
        }

        private static void Families(Report report)
        {
            var random = new WorldRandom(11);
            Item Base(string name) => ItemGenerator.Plain(name, 20, random);

            report.Check("Armour is heavy", Base("Plate Harness").Shares().Heavy == 1f);
            report.Check("Evasion is leather", Base("Hunter's Coat").Shares().Leather == 1f);
            report.Check("Barrier is cloth", Base("Silk Vestments").Shares().Cloth == 1f);
            FamilyShares brigandine = Base("Brigandine").Shares();
            report.Check("Something giving two is that much of each", MathF.Abs(brigandine.Heavy - 0.5f) < 0.01f && MathF.Abs(brigandine.Leather - 0.5f) < 0.01f);
            report.Check("A ring is no family", Base("Ring").Shares() == default);
            report.Check("Weapons go by what they are", Base("Longsword").Shares().Heavy == 1f && Base("Stiletto").Shares().Leather == 1f && Base("Runed Staff").Shares().Cloth == 1f);

            foreach (ItemSlot slot in new[] { ItemSlot.Head, ItemSlot.Body, ItemSlot.Legs, ItemSlot.Feet, ItemSlot.Hands, ItemSlot.OffHand })
            {
                var seen = new HashSet<GearFamily>();
                for (int i = 0; i < 400; i++)
                {
                    FamilyShares shares = ItemGenerator.Roll(5, random.Fork(i), slot: slot).Shares();
                    foreach (GearFamily family in Enum.GetValues<GearFamily>())
                    {
                        if (shares.Of(family) > 0.5f) seen.Add(family);
                    }
                }
                report.Check($"Every family can be had for {slot} from early on", seen.Count == 3, string.Join(", ", seen));
            }

            Item robe = Base("Silk Vestments");
            HeroState bare = new HeroState { Level = 20 };
            HeroState dressed = new HeroState { Level = 20 };
            dressed.Equipped[WornSlot.Body] = robe;
            float extra = HeroBuild.Compile(dressed).SheetFor(Condition.None).MaxMana - HeroBuild.Compile(bare).SheetFor(Condition.None).MaxMana;
            report.Check("Cloth holds mana, a whole number of it", extra == MathF.Round(robe.Barrier * Item.ManaPerBarrier), $"{extra}");
            report.Check("And says so", robe.BaseRows().Any(row => row.name == "Mana"));
            report.Check("Heavy and leather don't", !Base("Plate Harness").Mods().Any(m => m.Stat == Stat.Mana) && !Base("Hunter's Coat").Mods().Any(m => m.Stat == Stat.Mana));
        }

        private static void Leaning(Report report)
        {
            report.Check("Everything a family leans towards is an affix", AffixCatalog.Suited.Keys.All(id => AffixCatalog.Find(id) != null), string.Join(", ", AffixCatalog.Suited.Keys.Where(id => AffixCatalog.Find(id) == null)));

            foreach (GearFamily family in Enum.GetValues<GearFamily>())
            {
                report.Check($"{family} can roll what suits it, on every kind of armour", new[] { ItemSlot.Head, ItemSlot.Body, ItemSlot.Legs, ItemSlot.Feet, ItemSlot.Hands }.All(slot =>
                    AffixCatalog.All.Any(a => a.Slots.Contains(slot) && AffixCatalog.Suited.TryGetValue(a.Id, out GearFamily suits) && suits == family)));
            }

            var random = new WorldRandom(12);
            var lines = new float[3, 3];
            foreach (GearFamily family in Enum.GetValues<GearFamily>())
            {
                string name = family switch { GearFamily.Heavy => "Plate Harness", GearFamily.Leather => "Hunter's Coat", _ => "Silk Vestments" };
                for (int i = 0; i < 4000; i++)
                {
                    Item item = ItemGenerator.Forge(30, random.Fork(i * 4 + (int)family), Rarity.Rare, 0.3f, ItemSlot.Body);
                    if (Dominant(item) != family) continue;
                    foreach (ItemLine line in item.Lines)
                    {
                        if (AffixCatalog.Suited.TryGetValue(line.Affix, out GearFamily suits)) lines[(int)family, (int)suits]++;
                    }
                }
            }

            foreach (GearFamily family in Enum.GetValues<GearFamily>())
            {
                int own = (int)family;
                float total = lines[own, 0] + lines[own, 1] + lines[own, 2];
                report.Note($"On {family} bodies, {lines[own, 0] / total:P0} heavy lines, {lines[own, 1] / total:P0} leather, {lines[own, 2] / total:P0} cloth");
                report.Check($"{family} rolls more of what suits it than of anything else", Enumerable.Range(0, 3).All(other => other == own || lines[own, own] > lines[own, other] * 1.3f));
                report.Check($"And can still roll the rest", Enumerable.Range(0, 3).All(other => lines[own, other] > 0f));
            }
        }

        private static GearFamily Dominant(Item item)
        {
            FamilyShares shares = item.Shares();
            return shares.Heavy >= shares.Leather && shares.Heavy >= shares.Cloth ? GearFamily.Heavy : shares.Leather >= shares.Cloth ? GearFamily.Leather : GearFamily.Cloth;
        }

        private static void Seeds(Report report)
        {
            var random = new WorldRandom(13);
            var seeds = new HashSet<ulong>();
            int rolled = 20000;
            for (int i = 0; i < rolled; i++) seeds.Add(ItemGenerator.Roll(25, random.Fork(i)).LookSeed());
            report.Note($"{seeds.Count} looks in {rolled} rolls, some of them uniques");
            report.Check("No two things rolled have the same look, unless they're the same unique", seeds.Count > rolled * 0.97f);

            WorldRandom a = new WorldRandom(14), b = new WorldRandom(14);
            ItemGenerator.NewSeed(a);
            report.Check("Finding a look takes nothing from what's rolled after it", a.NextULong() == b.NextULong());

            report.Check("The same roll comes out with the same look", ItemGenerator.Roll(25, new WorldRandom(15)).LookSeed() == ItemGenerator.Roll(25, new WorldRandom(15)).LookSeed());

            UniqueDefinition unique = UniqueCatalog.Find("ashen-promise")!;
            report.Check("A unique looks the same wherever it turns up", UniqueCatalog.Make(unique, 10, random.Fork(1)).LookSeed() == UniqueCatalog.Make(unique, 40, random.Fork(2)).LookSeed());
            report.Check("But no two uniques look alike", UniqueCatalog.All.Select(u => UniqueCatalog.Make(u, 20, random.Fork(3)).LookSeed()).Distinct().Count() == UniqueCatalog.All.Count);

            Item item = ItemGenerator.Forge(30, random.Fork(4), Rarity.Rare, 0.5f, ItemSlot.Body);
            var writer = new BitWriter();
            Protocol.WriteItem(writer, item);
            Item sent = Protocol.ReadItem(new BitReader(writer.ToArray()));
            report.Check("A look goes over the wire", sent.Seed == item.Seed && sent.Base == item.Base && sent.LookSeed() == item.LookSeed() && item.Base.Length > 0);

            var hero = new HeroState();
            hero.Equipped[WornSlot.Finger2] = Ring("On the second finger", "gold-find", 5f);
            var gear = new BitWriter();
            Protocol.WriteGear(gear, hero);
            var received = new HeroState();
            Protocol.ReadGear(new BitReader(gear.ToArray()), received);
            report.Check("And so does which finger a ring is on", received.Equipped.ContainsKey(WornSlot.Finger2) && !received.Equipped.ContainsKey(WornSlot.Finger1));

            var forged = new BitWriter();
            forged.WriteCount(0);
            forged.WriteCount(1);
            forged.WriteByte((byte)WornSlot.Head);
            Protocol.WriteItem(forged, Ring("Hat", "gold-find", 1f));
            bool refused = false;
            try { Protocol.ReadGear(new BitReader(forged.ToArray()), new HeroState()); }
            catch (NetReadException) { refused = true; }
            report.Check("A ring on someone's head is refused at the door", refused);

            HeroState started = HeroState.CreateNew();
            report.Check("Two new heroes don't start out in the same sword", started.Equipped[WornSlot.MainHand].LookSeed() != HeroState.CreateNew().Equipped[WornSlot.MainHand].LookSeed());
            report.Check("What a hero starts in is what it says it is", started.Equipped.Values.All(i => ItemGenerator.HasBase(i.Base) && i.Seed != 0));
        }

        private static void OldLooks(Report report)
        {
            Item Old(string name, int level, float armour) => new Item { Name = name, Slot = ItemSlot.Body, Level = level, Armour = armour };

            Item coat = Old("Fortified Lamellar Coat of the Ox", 12, 31.5f);
            report.Check("Something older than seeds finds a look", coat.LookSeed() != 0);
            report.Check("And keeps it", coat.LookSeed() == Old("Fortified Lamellar Coat of the Ox", 12, 31.5f).LookSeed());
            report.Check("Two old things have their own", coat.LookSeed() != Old("Fortified Lamellar Coat of the Ox", 12, 31.6f).LookSeed());
            report.Check("And its base can be read from its name", ItemGenerator.BaseOf(coat) == "Lamellar Coat");
            report.Check("Taking the most it can from the name", ItemGenerator.BaseOf(new Item { Name = "Keen Short Bow of Haste", Slot = ItemSlot.MainHand }) == "Short Bow" && ItemGenerator.BaseOf(new Item { Name = "Silk Slippers of the Road", Slot = ItemSlot.Feet }) == "Silk Slippers");
            report.Check("A name that says nothing is a base of nothing", ItemGenerator.BaseOf(new Item { Name = "Travelling Tunic", Slot = ItemSlot.Body }) == "");
            report.Check("A unique's base is in the catalogue", ItemGenerator.BaseOf(new Item { Name = "Ashen Promise", Slot = ItemSlot.MainHand, Unique = "ashen-promise" }) == "Arming Sword");
        }

        // What the land's people and the Reich's wear: each kind dressed within what a crowd of them can cost, nobody but the Reich's own in its colours, and the Reich's plainly in them. What
        // comes in bands has to be cheaper than one met alone, as a warlord or a grave knight is, who costs about what a hero in everything does.
        private const int MostCrowdTriangles = 20000;
        private const int MostAloneTriangles = 34000;

        private static void Uniforms(Report report)
        {
            int dearest = 0, dearestAlone = 0;
            string dearestKey = "", dearestAloneKey = "";
            var wearing = new List<string>();
            var bare = new List<string>();
            foreach (string key in Dino_Defenders.Client.Uniforms.Keys)
            {
                Dino_Engine.Modelling.Model.Mesh mesh = Dino_Defenders.Client.Uniforms.Make(key);
                bool alone = key is "warlord" or "revenant";
                if (alone && mesh.faces.Count > dearestAlone) { dearestAlone = mesh.faces.Count; dearestAloneKey = key; }
                if (!alone && mesh.faces.Count > dearest) { dearest = mesh.faces.Count; dearestKey = key; }
                if (Environment.GetEnvironmentVariable("UNIFORMS") == "1") Console.WriteLine($"    {key}: {mesh.faces.Count}");
                int reichs = 0;
                foreach (Dino_Engine.Modelling.Model.MeshVertex vertex in mesh.meshVertices)
                {
                    OpenTK.Mathematics.Vector3 albedo = vertex.colour.ToVector3() * Dino_Engine.Modelling.Procedural.Gear.GearMaterials.AlbedoOf(vertex.materialTextureIndex);
                    if (Dino_Engine.Modelling.Procedural.Gear.GearSwatches.IsReichs(albedo, Dino_Engine.Modelling.Procedural.Gear.GearMaterials.IsMetal(vertex.materialTextureIndex))) reichs++;
                }
                float share = reichs / (float)Math.Max(mesh.meshVertices.Count, 1);
                bool reich = Dino_Defenders.Client.Uniforms.IsReichs(key);
                if (!reich && share > 0.01f) wearing.Add($"{key} {share:P0}");
                if (reich && share < 0.25f) bare.Add($"{key} {share:P0}");
            }
            report.Check("Every kind of person met in bands is dressed within what a crowd of them can cost", dearest <= MostCrowdTriangles, $"{dearest} triangles at most, {dearestKey}, of {MostCrowdTriangles}");
            report.Check("And one met alone within what a hero in everything costs", dearestAlone <= MostAloneTriangles, $"{dearestAlone}, {dearestAloneKey}, of {MostAloneTriangles}");
            report.Check("Every outfit grows what it asks for: a hood, a straw hat, pauldrons, a helm with or without a crest", Dino_Defenders.Client.Uniforms.Unmet.IsEmpty, string.Join(", ", Dino_Defenders.Client.Uniforms.Unmet));
            report.Check("Nobody but the Reich's own wears its colours", wearing.Count == 0, string.Join(", ", wearing));
            report.Check("And the Reich's own plainly do", bare.Count == 0, string.Join(", ", bare));
        }

        private static void Traits(Report report)
        {
            report.Check("Every base says what it's going for", ItemGenerator.BaseNames.All(ItemLook.HasForm), string.Join(", ", ItemGenerator.BaseNames.Where(b => !ItemLook.HasForm(b))));
            report.Check("There are few enough bases to send in seven bits", ItemGenerator.BaseNames.Count() < 127, $"{ItemGenerator.BaseNames.Count()} of 126");

            var random = new WorldRandom(16);
            int kept = 0, rolled = 3000;
            var kinds = new HashSet<Dino_Engine.Modelling.Procedural.Gear.GearKind>();
            for (int i = 0; i < rolled; i++)
            {
                // Every level of the climb, as what level a look is from is packed with it.
                Item item = ItemGenerator.Roll(1 + i * 37 % ItemGenerator.MostItemLevel, random.Fork(i), 1f);
                Dino_Engine.Modelling.Procedural.Gear.GearSpec spec = ItemLook.SpecOf(item);
                kinds.Add(spec.Kind);

                FamilyShares shares = item.Shares();
                bool same = spec.Seed == item.LookSeed() && spec.Grade == (item.Rarity == Rarity.Unique ? ItemLook.UniqueGrade : (int)item.Rarity) && MathF.Abs(spec.Heavy - shares.Heavy) < 0.05f && MathF.Abs(spec.Leather - shares.Leather) < 0.05f && MathF.Abs(spec.Cloth - shares.Cloth) < 0.05f
                    && MathF.Abs(spec.Level - (item.Level - 1) / (float)(ItemGenerator.MostItemLevel - 1)) <= 0.5f / ItemLook.LevelSteps + 1e-4f;
                if (same) kept++;
            }
            report.Check("What a look needs survives being packed into one number, its level with it", kept == rolled, $"{kept} of {rolled}");
            report.Check("And the first level and the last are the bottom and the top of what a look shows", ItemLook.LevelStep(1) == 0 && ItemLook.LevelStep(ItemGenerator.MostItemLevel) == ItemLook.LevelSteps);

            // Written out, as a unique's look is grown from this number, and a save only keeps its seed: anything else gives every unique in every pack a different look.
            report.Check("A unique keeps the look it's always had", ItemLook.SpecOf(UniqueCatalog.Make(UniqueCatalog.All[0], 20, new WorldRandom(1))).Grade == 4);
            report.Check("And everything worn or held has a kind of look, which what goes in a book isn't", kinds.Count == Enum.GetValues<Dino_Engine.Modelling.Procedural.Gear.GearKind>().Count(kind => kind < Dino_Engine.Modelling.Procedural.Gear.GearKind.Book), $"{kinds.Count}");

            Item burning = new Item { Name = "Test", Slot = ItemSlot.MainHand, Lines = { new ItemLine("added-fire", 10f) } };
            Item frozen = new Item { Name = "Test", Slot = ItemSlot.MainHand, Lines = { new ItemLine("cold-damage", 20f) } };
            Item plain = new Item { Name = "Test", Slot = ItemSlot.MainHand, Lines = { new ItemLine("attack-speed", 10f) } };
            report.Check("What burns looks it, and so on", ItemLook.ThemeOf(burning) == Dino_Engine.Modelling.Procedural.Gear.GearTheme.Fire && ItemLook.ThemeOf(frozen) == Dino_Engine.Modelling.Procedural.Gear.GearTheme.Cold && ItemLook.ThemeOf(plain) == Dino_Engine.Modelling.Procedural.Gear.GearTheme.None);
        }

        // As saved by 0.1.5. Everything renamed since is in here.
        private const string VersionThree = """
            {
              "Version": 3,
              "Hero": {
                "Level": 20, "Experience": 76, "Gold": 368, "Flasks": 2, "WorldsCleared": 1,
                "Talents": [], "Bar": [ "strike", null, null, null, null, null, null ],
                "Inventory": [
                  { "Name": "Gold Ring", "Slot": "Ring", "Rarity": "Magic", "Level": 9, "WeaponDamage": 0, "Armour": 0, "Evasion": 0, "Barrier": 0, "Kind": "Sword", "Lines": [ { "Affix": "gold-find", "Value": 12 } ] },
                  { "Name": "Spare Blade", "Slot": "Weapon", "Rarity": "Common", "Level": 9, "WeaponDamage": 20, "Armour": 0, "Evasion": 0, "Barrier": 0, "Kind": "Axe", "Lines": [] }
                ],
                "Equipped": {
                  "Weapon": { "Name": "Keen Longsword of Haste", "Slot": "Weapon", "Rarity": "Rare", "Level": 12, "WeaponDamage": 33, "Armour": 0, "Evasion": 0, "Barrier": 0, "Kind": "Sword", "Lines": [ { "Affix": "damage", "Value": 40 } ] },
                  "Shield": { "Name": "Round Shield", "Slot": "Shield", "Rarity": "Common", "Level": 4, "WeaponDamage": 0, "Armour": 11, "Evasion": 0, "Barrier": 0, "Kind": "Sword", "Lines": [] },
                  "Ring": { "Name": "The Understudy", "Slot": "Ring", "Rarity": "Legendary", "Level": 6, "WeaponDamage": 0, "Armour": 0, "Evasion": 0, "Barrier": 0, "Kind": "Sword", "Lines": [], "Unique": "understudy" },
                  "Amulet": { "Name": "Amulet", "Slot": "Amulet", "Rarity": "Common", "Level": 1, "WeaponDamage": 0, "Armour": 0, "Evasion": 0, "Barrier": 0, "Kind": "Sword", "Lines": [] },
                  "Body": { "Name": "Tunic", "Slot": "Body", "Rarity": "Common", "Level": 1, "WeaponDamage": 0, "Armour": 6, "Evasion": 0, "Barrier": 0, "Kind": "Sword", "Lines": [] }
                }
              },
              "WorldSeed": 777, "WorldLevel": 3, "PlayerX": 50, "PlayerZ": 60, "Health": 80
            }
            """;

        private static void OldPlaces(Report report)
        {
            SaveGame? save = SaveGame.Read(VersionThree);
            report.Check("A save from 0.1.5 loads", save != null);
            if (save == null) return;

            HeroState hero = save.Hero;
            report.Check("With the hero as they were", hero.Level == 20 && hero.Gold == 368 && save.WorldSeed == 777 && hero.Carried().Count() == 2 && hero.Equipped.Count == 5);
            report.Check("Each thing worn is where it should be", hero.Equipped[WornSlot.MainHand].Name.Contains("Longsword") && hero.Equipped[WornSlot.OffHand].Name == "Round Shield" && hero.Equipped[WornSlot.Finger1].Unique == "understudy" && hero.Equipped[WornSlot.Neck].Name == "Amulet" && hero.Equipped[WornSlot.Body].Name == "Tunic");
            report.Check("And is what it should be", hero.Equipped[WornSlot.MainHand].Slot == ItemSlot.MainHand && hero.Equipped[WornSlot.OffHand].Slot == ItemSlot.OffHand && hero.Equipped[WornSlot.Finger1].Slot == ItemSlot.Finger && hero.Equipped[WornSlot.Neck].Slot == ItemSlot.Neck);
            report.Check("What's carried comes across too", hero.Inventory[0]!.Slot == ItemSlot.Finger && hero.Inventory[1]!.Slot == ItemSlot.MainHand && hero.Inventory[1]!.Kind == WeaponKind.Axe);
            report.Check("Lines that can stay do, as they were", hero.Equipped[WornSlot.MainHand].Lines.Any(line => line.Affix == "damage" && line.Value == 40f) && hero.Inventory[0]!.Lines.Any(line => line.Affix == "gold-find" && line.Value == 12f));
            report.Check("A second finger is free", hero.PlaceFor(hero.Inventory[0]!) == WornSlot.Finger2);
            report.Check("It knows it's up to date", save.Version == SaveGame.CurrentVersion);

            string path = Path.Combine(Path.GetTempPath(), $"buildlab-gear-{Environment.ProcessId}.json");
            File.WriteAllText(path, VersionThree);
            SaveGame.PathOverride = path;
            SaveGame? loaded = SaveGame.Load();
            bool kept = File.Exists(path + ".before-gear") && File.ReadAllText(path + ".before-gear") == VersionThree;
            loaded?.Store();
            string written = File.ReadAllText(path);
            File.Delete(path + ".before-gear");
            SaveGame? again = SaveGame.Load();
            bool noCopyOfNew = !File.Exists(path + ".before-gear");
            File.Delete(path);
            SaveGame.PathOverride = null;

            report.Check("The old save is kept beside it", kept);
            report.Check("It's written out in the new words", written.Contains("\"MainHand\"") && written.Contains("\"Finger1\"") && written.Contains("\"Slot\": \"Neck\"") && !written.Contains("\"Slot\": \"Weapon\"") && !written.Contains("\"Slot\": \"Amulet\""));
            report.Check("And loads again as it was, with nothing to keep this time", again != null && again.Hero.Equipped.Count == 5 && again.Hero.Carried().Count() == 2 && again.Version == SaveGame.CurrentVersion && noCopyOfNew);
            report.Check("With every old item keeping the look it found", again != null && again.Hero.Equipped[WornSlot.MainHand].LookSeed() == hero.Equipped[WornSlot.MainHand].LookSeed());
        }

        // Real ones, from this machine, if there are any: nothing written here is as odd as what a person's save turns out to hold. They're only read, never written.
        private static void RealSaves(Report report)
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DinoDefenders");
            var files = new List<string>();
            if (File.Exists(Path.Combine(folder, "save.json"))) files.Add(Path.Combine(folder, "save.json"));
            if (Directory.Exists(Path.Combine(folder, "backups"))) files.AddRange(Directory.GetFiles(Path.Combine(folder, "backups"), "*.json"));
            // And the heroes being played now, each a save of its own since the main menu (HeroRoster).
            if (Directory.Exists(Path.Combine(folder, "heroes"))) files.AddRange(Directory.GetFiles(Path.Combine(folder, "heroes"), "*.json"));

            foreach (string file in files)
            {
                string name = Path.GetFileName(file);
                string json = File.ReadAllText(file);
                SaveGame? save = SaveGame.Read(json);
                if (save == null)
                {
                    report.Check($"{name} loads", false);
                    continue;
                }

                int items = save.Hero.Carried().Concat(save.Hero.Equipped.Values).Concat(save.Hero.Bar.OfType<Item>()).Sum(Holding);
                int before = CountOf(json, "\"Slot\"");
                report.Check($"{name} loads with all {before} of its items", items >= before, $"{items}");
                report.Check($"{name} has a look for everything", save.Hero.Carried().Concat(save.Hero.Equipped.Values).All(i => i.LookSeed() != 0));
                report.Check($"{name} has everything worn where it fits", save.Hero.Equipped.All(pair => WornSlots.Fits(pair.Value, pair.Key)));
            }
            if (files.Count == 0) report.Note("No real saves on this machine to try");
        }

        // Something and all that's in it, as a book that was one thing in an old save is several now.
        private static int Holding(Item item) => 1 + (item.Held?.Sum(held => held == null ? 0 : Holding(held)) ?? 0);

        private static int CountOf(string text, string what)
        {
            int count = 0;
            for (int at = text.IndexOf(what, StringComparison.Ordinal); at >= 0; at = text.IndexOf(what, at + what.Length, StringComparison.Ordinal)) count++;
            return count;
        }
    }
}

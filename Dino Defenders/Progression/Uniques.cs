using Dino_Defenders.Books;
using Dino_Defenders.Stats;
using Dino_Engine.World;
using static Dino_Defenders.Stats.M;

namespace Dino_Defenders.Progression
{
    /// <summary>One thing of its kind. Its lines are its own, and are the reason to want it: a unique should change what a build is, not add to it.</summary>
    public sealed class UniqueDefinition
    {
        public string Id = "";
        public string Name = "";
        public string Base = "";
        public string Lore = "";
        public int MinLevel = 1;
        public Mod[] Mods = Array.Empty<Mod>();
    }

    /// <summary>
    /// Every unique item. Bosses wear these and fight with them, so a player has usually seen what one does before they ever hold it. Ids are saved
    /// with the item, so they stay; everything else about a unique can change, and the copies in people's bags change with it.
    /// </summary>
    public static class UniqueCatalog
    {
        private static readonly Dictionary<string, UniqueDefinition> ById = new();
        private static readonly List<UniqueDefinition> Everything = new();

        public static IReadOnlyList<UniqueDefinition> All => Everything;

        public static UniqueDefinition? Find(string id) => ById.GetValueOrDefault(id);

        private static void Add(string id, string name, string baseName, int minLevel, string lore, params Mod[] mods)
        {
            var unique = new UniqueDefinition { Id = id, Name = name, Base = baseName, MinLevel = minLevel, Lore = lore, Mods = mods };
            ById.Add(id, unique);
            Everything.Add(unique);
        }

        static UniqueCatalog()
        {
            // Weapons.
            Add("ashen-promise", "Ashen Promise", "Arming Sword", 4, "It was sworn on a burning house.",
                Convert(DamageType.Physical, DamageType.Fire, 100f), Inc(Stat.Damage, 40f, Tags.Fire), Flat(Stat.ExplodeOnKill, 8f), Chance(Tags.Burn, 20f));
            Add("sawtooth-mercy", "Sawtooth Mercy", "Bearded Axe", 8, "The kindest thing it does is finish.",
                Chance(Tags.Bleed, 60f), Inc(Stat.Damage, 80f, Tags.Bleed), More(Stat.Damage, -20f, Tags.Hit), Flat(Stat.HealthOnKill, 8f));
            Add("slow-verdict", "The Slow Verdict", "Warhammer", 10, "The court will rise.",
                Divert(Stat.AttackSpeed, Stat.Damage, 150f, Tags.Attack), Chance(Tags.Stun, 40f), Inc(Stat.Damage, 40f, Tags.Mace));
            Add("widows-needle", "Widow's Needle", "Stiletto", 9, "One was enough, but she brought more.",
                Flat(Stat.CritChance, 4f), Chance(Tags.Venom, 60f), Rules(Rule.AilmentsLeap), Flat(Stat.CritMultiplier, 30f));
            Add("long-goodbye", "The Long Goodbye", "Hunting Bow", 7, "You'll see it coming. That's the idea.",
                Rules(Rule.LongShot), Inc(Stat.ProjectileSpeed, 30f), Flat(Stat.Pierce, 1f));
            Add("homecoming", "Homecoming", "Boar Spear", 5, "Everything thrown comes back. Ask anyone who's left a village.",
                Rules(Rule.ProjectilesReturn), More(Stat.Damage, -10f, Tags.Projectile), Flat(Stat.ProjectileCount, 1f));
            Add("tides-answer", "The Tide's Answer", "Bound Staff", 9, "Ask the sea anything, and this is what you'll hear.",
                Rules(Rule.WaterTrail), Inc(Stat.Damage, 40f, Tags.Cold), Inc(Stat.Damage, 40f, Tags.Lightning), Flat(Stat.Chain, 1f));
            Add("stormglass", "Stormglass Rod", "Bone Wand", 12, "It points at the next one.",
                Flat(Stat.Chain, 2f), Inc(Stat.Damage, 40f, Tags.Lightning), Rules(Rule.ChainsGrow));
            Add("second-opinion", "Second Opinion", "Twig", 3, "It was right the first time as well.",
                Rules(Rule.SpellEcho), More(Stat.CastSpeed, -20f), Inc(Stat.Damage, 15f, Tags.Spell));
            Add("beggars-staff", "Beggar's Staff", "Walking Staff", 2, "Alms for the poor, and for the staff.",
                Inc(Stat.Damage, 1f).For(Per.Gold, 200f, 120f), Inc(Stat.GoldFind, 50f));
            Add("mourning-edge", "Mourning Edge", "Longsword", 14, "It keeps a count, and so should you.",
                Inc(Stat.Damage, 3f).For(Per.RecentKills, 1f, 90f), Inc(Stat.AttackSpeed, 1f).For(Per.RecentKills, 1f, 30f), Trigger(TriggerEvent.Kill, Shelf.Retaliate, 15f));

            // Shields.
            Add("turned-cheek", "The Turned Cheek", "Heater Shield", 11, "Do unto others.",
                Flat(Stat.BlockChance, 30f), Rules(Rule.BlockReflects), Trigger(TriggerEvent.Block, Shelf.Retaliate));
            Add("the-door", "The Door", "Tower Shield", 18, "It came off its hinges with the keep.",
                Flat(Stat.BlockChance, 20f), More(Stat.DamageTaken, -15f, Tags.Projectile), Inc(Stat.MoveSpeed, -10f), Rules(Rule.CannotBeStunned));

            // Heads.
            Add("hollow-crown", "The Hollow Crown", "Circlet", 10, "It weighs nothing at all, and that's what's wrong with it.",
                Set(Stat.Mana, 1f), Rules(Rule.CostsHealth), More(Stat.Damage, 30f, Tags.Spell), Inc(Stat.Health, 15f));
            Add("mockingbird", "Eye of the Mockingbird", "Leather Cap", 6, "It has heard you say that before.",
                Rules(Rule.Steady), Inc(Stat.Potency, 15f, Tags.Wild), Inc(Stat.Damage, 20f, Tags.Attack));
            Add("deep-cowl", "The Deep Cowl", "Hood", 5, "Nothing looks back.",
                Flat(Stat.Stealth, 40f), Grant(Condition.InDarkness), Inc(Stat.Damage, 40f).While(Condition.InDarkness));

            // Bodies.
            Add("drowned-king", "Shell of the Drowned King", "Lamellar Coat", 12, "The sea hasn't finished with it.",
                Grant(Condition.InWater), Inc(Stat.Armour, 60f), Resist(Tags.Fire, 40f), Resist(Tags.Lightning, -40f));
            Add("mantle-of-cinders", "Mantle of Cinders", "Linen Robe", 8, "Whoever wore it first is part of it now.",
                Rules(Rule.FireHeals | Rule.ImmuneToBurn | Rule.BurningTrail), Resist(Tags.Cold, -30f));
            Add("patient-beast", "Hide of the Patient Beast", "Leather Jerkin", 7, "It took a long time to die, and didn't seem to mind.",
                Rules(Rule.DeferredDamage), Inc(Stat.Health, 20f), Flat(Stat.HealthRegenPercent, 1f));
            Add("last-word", "Robe of the Last Word", "Silk Vestments", 13, "There's always something to add.",
                Inc(Stat.Barrier, 80f), Rules(Rule.OverhealBarrier), Inc(Stat.BarrierRecharge, 50f));

            // Feet.
            Add("pilgrims-soles", "Pilgrim's Soles", "Leather Boots", 4, "Worn through at every shrine in the land.",
                Inc(Stat.MoveSpeed, 40f).While(Condition.OnRoad), Inc(Stat.Damage, 2f).For(Per.ShrinesKindled, 1f, 40f));
            Add("stilts", "The Long Step", "Iron Greaves", 11, "From up here, it's all so simple.",
                Inc(Stat.MoveSpeed, 25f), Rules(Rule.HighGround));
            Add("cindertread", "Cindertread", "Sandals", 3, "Mind the rug.",
                Rules(Rule.BurningTrail), Inc(Stat.MoveSpeed, 15f), Resist(Tags.Fire, 30f));
            Add("tidewalkers", "Tidewalkers", "Silk Slippers", 8, "They were never once dry.",
                Inc(Stat.MoveSpeed, 30f).While(Condition.InWater), Rules(Rule.WaterTrail), Inc(Stat.Barrier, 25f));

            // Legs.
            Add("forced-march", "The Forced March", "Iron Cuisses", 10, "Left, right. Left, right. Left.",
                Inc(Stat.Damage, 30f).While(Condition.Moving), Rules(Rule.CannotBeStunned), Inc(Stat.MoveSpeed, 8f));
            Add("waders", "The Waders", "Hide Trousers", 5, "Wet to the knee, and glad of it.",
                Inc(Stat.Evasion, 60f).While(Condition.InWater), Inc(Stat.MoveSpeed, 20f).While(Condition.InWater), Resist(Tags.Lightning, 20f));
            Add("penitents-sackcloth", "Penitent's Sackcloth", "Linen Trousers", 6, "It itches, and that's the point.",
                Inc(Stat.Barrier, 60f), Rules(Rule.NoRegen), Inc(Stat.BarrierRecharge, 40f));

            // Hands.
            Add("thunder-grip", "The Thunder Grip", "Iron Gauntlets", 9, "Nobody has shaken hands with it twice.",
                Convert(DamageType.Physical, DamageType.Lightning, 100f, Tags.Attack), Chance(Tags.Shock, 25f), Inc(Stat.Damage, 25f, Tags.Lightning));
            Add("sleight-of-hand", "Sleight of Hand", "Silk Gloves", 10, "Watch closely. No, closer.",
                Echo(Stat.CastSpeed, Stat.AttackSpeed), Inc(Stat.CastSpeed, 20f));
            Add("cutpurses-fingers", "Cutpurse's Fingers", "Leather Gloves", 4, "What's yours is anyone's.",
                Inc(Stat.GoldFind, 40f), Inc(Stat.ItemRarity, 20f), Inc(Stat.AttackSpeed, 1f).For(Per.RecentKills, 1f, 20f));

            // Rings.
            Add("understudy", "The Understudy", "Ring", 6, "It has learned all your lines.",
                Flat(Stat.MinionCount, 1f), Inc(Stat.Damage, 40f, Tags.Minion), Inc(Stat.Health, 30f, Tags.Minion));
            Add("two-coins", "Two Coins", "Ring", 3, "Tails, you lose a little less.",
                Rules(Rule.LuckyHits), Inc(Stat.Damage, -15f));
            Add("knot-of-regret", "Knot of Regret", "Ring", 12, "You should have been kinder to them while they were alive.",
                Flat(Stat.RaiseFallen, 25f), Inc(Stat.Damage, 25f, Tags.Minion));

            // Amulets.
            Add("first-ember", "The First Ember", "Amulet", 8, "Everything that has ever burned was lit from this.",
                Trigger(TriggerEvent.Kill, Shelf.Emberfall, 20f), Inc(Stat.Damage, 20f, Tags.Fire), Inc(Stat.Potency, 20f, Tags.Falling));
            Add("frostbitten-locket", "Frostbitten Locket", "Amulet", 10, "Someone is still inside.",
                Rules(Rule.FreezeStores), Chance(Tags.Freeze, 10f), Inc(Stat.Damage, 20f, Tags.Cold));
            Add("heart-of-the-swarm", "Heart of the Swarm", "Amulet", 14, "It beats a little out of time with yours.",
                Trigger(TriggerEvent.Struck, Shelf.VenomCloud, 30f), Inc(Stat.Damage, 30f, Tags.Venom), Rules(Rule.Carrier));

            // What's only worth having for how a book is written.
            Add("authors-quill", "The Author's Quill", "Bone Wand", 8, "It has never once been at a loss for words.",
                Flat(Stat.ProjectileCount, 2f, Tags.Turned), More(Stat.Damage, -15f, Tags.Turned), Inc(Stat.Potency, 25f, Tags.Opening));
            Add("dog-eared", "The Dog-Eared Page", "Ring", 7, "Someone keeps coming back to this bit.",
                More(Stat.Potency, 30f, Tags.Turned), Inc(Stat.BookCost, 20f));
            Add("bookmark", "The Bookmark", "Amulet", 9, "You can always pick up where you left off.",
                Rules(Rule.OneCost), Inc(Stat.Potency, -10f));
            Add("inky-fingers", "Inky Fingers", "Silk Gloves", 6, "It gets on everything.",
                Inc(Stat.AreaOfEffect, 40f, Tags.Turned), Inc(Stat.PageDuration, 30f, Tags.Zone), Inc(Stat.Potency, -10f, Tags.Opening));
            Add("upward-glance", "The Upward Glance", "Hood", 8, "It's worth looking up, now and again.",
                More(Stat.Potency, 35f, Tags.Falling), Inc(Stat.ProjectileSpeed, 50f, Tags.Falling), Inc(Stat.Potency, -10f, Tags.Projectile));
        }

        /// <summary>Draws a unique that can drop at a level, for a slot if one is wanted. Null if there isn't one.</summary>
        public static UniqueDefinition? Pick(int level, ItemSlot? slot, WorldRandom random)
        {
            var options = new List<UniqueDefinition>();
            foreach (UniqueDefinition unique in Everything)
            {
                if (unique.MinLevel > level) continue;
                if (slot != null && ItemGenerator.Plain(unique.Base, 1, new WorldRandom(1)).Slot != slot) continue;
                options.Add(unique);
            }
            return options.Count == 0 ? null : random.Pick(options);
        }

        public static Item Make(UniqueDefinition unique, int level, WorldRandom random)
        {
            Item item = ItemGenerator.Plain(unique.Base, level, random);
            item.Name = unique.Name;
            item.Rarity = Rarity.Legendary;
            item.Unique = unique.Id;
            return item;
        }
    }
}

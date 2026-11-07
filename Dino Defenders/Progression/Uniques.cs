using Dino_Defenders.Books;
using Dino_Defenders.Stats;
using Dino_Engine.World;
using static Dino_Defenders.Stats.M;

namespace Dino_Defenders.Progression
{
    /// <summary>
    /// One thing of its kind. What it gives is the reason to want it, and should change what a build is rather than add to it. What it costs is the reason to think twice:
    /// every unique has both, and they're kept apart so that a card can show them apart.
    /// </summary>
    public sealed class UniqueDefinition
    {
        public string Id = "";
        public string Name = "";
        public string Base = "";
        public string Lore = "";
        public int MinLevel = 1;
        public Mod[] Boon = Array.Empty<Mod>();
        public Mod[] Burden = Array.Empty<Mod>();

        /// <summary>Everything it does to whoever wears it: what it gives, then what it costs.</summary>
        public Mod[] Mods = Array.Empty<Mod>();

        /// <summary>Only means anything with a player's hands on it, or would turn a boss who wore it into something else: enemies are never given one.</summary>
        public bool HeroOnly;
    }

    /// <summary>
    /// Every unique item. Bosses wear these and fight with them, so a player has usually seen what one does before they ever hold it. Ids are saved
    /// with the item, so they stay; everything else about a unique can change, and the copies in people's bags change with it. None of them hands out
    /// a keystone's rule: something a tree already gives is no reason to want an item, and BuildLab holds that.
    /// </summary>
    public static class UniqueCatalog
    {
        private static readonly Dictionary<string, UniqueDefinition> ById = new();
        private static readonly List<UniqueDefinition> Everything = new();

        public static IReadOnlyList<UniqueDefinition> All => Everything;

        public static UniqueDefinition? Find(string id) => ById.GetValueOrDefault(id);

        private static Mod[] Gives(params Mod[] mods) => mods;

        private static Mod[] Costs(params Mod[] mods) => mods;

        private static void Add(string id, string name, string baseName, int minLevel, string lore, Mod[] boon, Mod[] burden, bool heroOnly = false)
        {
            var unique = new UniqueDefinition { Id = id, Name = name, Base = baseName, MinLevel = minLevel, Lore = lore, Boon = boon, Burden = burden, Mods = boon.Concat(burden).ToArray(), HeroOnly = heroOnly };
            ById.Add(id, unique);
            Everything.Add(unique);
        }

        static UniqueCatalog()
        {
            // Swords.
            Add("ashen-promise", "Ashen Promise", "Arming Sword", 4, "It was sworn on a burning house.",
                Gives(Convert(DamageType.Physical, DamageType.Fire, 100f), Inc(Stat.Damage, 40f, Tags.Fire), Flat(Stat.ExplodeOnKill, 8f), Chance(Tags.Burn, 20f)),
                Costs(Resist(Tags.Fire, -40f)));
            Add("mourning-edge", "Mourning Edge", "Longsword", 14, "It keeps a count, and so should you.",
                Gives(Inc(Stat.Damage, 3f).For(Per.RecentKills, 1f, 90f), Inc(Stat.AttackSpeed, 1f).For(Per.RecentKills, 1f, 30f), Trigger(TriggerEvent.Kill, Shelf.Retaliate, 15f)),
                Costs(More(Stat.Damage, -35f).Unless(Condition.RecentlyKilled)));
            Add("honest-blade", "The Honest Blade", "Arming Sword", 6, "It has never once surprised anybody.",
                Gives(Set(Stat.CritChance, 100f)),
                Costs(Set(Stat.CritMultiplier, 100f)));
            Add("thin-red-line", "The Thin Red Line", "War Blade", 14, "Everyone's brave with a whole skin.",
                Gives(More(Stat.Damage, 60f).While(Condition.LowHealth), Inc(Stat.AttackSpeed, 25f).While(Condition.LowHealth)),
                Costs(Rules(Rule.NeverHealed)));
            Add("long-grudge", "The Long Grudge", "Longsword", 10, "It remembers every one of them, and so will they.",
                Gives(Gain(TriggerEvent.Struck, BuffCatalog.Grudge), Lose(TriggerEvent.Hit, BuffCatalog.Grudge)),
                Costs(More(Stat.DamageTaken, 25f, Tags.Hit)));
            Add("duellists-courtesy", "Duellist's Courtesy", "Short Sword", 5, "One at a time, please. There's no need to push.",
                Gives(Rules(Rule.Marks), More(Stat.Damage, 100f).Against(Condition.Marked)),
                Costs(More(Stat.Damage, -60f).Against(Condition.Unmarked)));

            // Axes.
            Add("sawtooth-mercy", "Sawtooth Mercy", "Bearded Axe", 8, "The kindest thing it does is finish.",
                Gives(Chance(Tags.Bleed, 60f), Inc(Stat.Damage, 80f, Tags.Bleed), Flat(Stat.Execute, 15f)),
                Costs(More(Stat.Damage, -30f, Tags.Hit)));
            Add("butchers-bill", "The Butcher's Bill", "Broad Axe", 18, "Paid in full, and then some.",
                Gives(Rules(Rule.BleedBurst), Chance(Tags.Bleed, 40f)),
                Costs(More(Stat.Damage, -40f, Tags.Hit)));
            Add("round-the-houses", "Round the Houses", "Hatchet", 3, "It went the long way, and took everyone with it.",
                Gives(Rules(Rule.FullCircle)),
                Costs(More(Stat.AttackSpeed, -35f)));
            Add("first-cut", "First Cut", "Bearded Axe", 8, "The first is the deepest. The rest are a formality.",
                Gives(More(Stat.Damage, 200f).Against(Condition.FullHealth)),
                Costs(More(Stat.Damage, -60f).Against(Condition.Wounded)));
            Add("headsmans-due", "The Headsman's Due", "Reaver Axe", 30, "No appeal has ever reached it in time.",
                Gives(Rules(Rule.ExecuteAny)),
                Costs(More(Stat.Damage, -50f).Against(Condition.FullHealth)));

            // Maces.
            Add("slow-verdict", "The Slow Verdict", "Warhammer", 10, "The court will rise.",
                Gives(EveryGain(5f, BuffCatalog.Verdict), Lose(TriggerEvent.Hit, BuffCatalog.Verdict), Chance(Tags.Stun, 40f), Inc(Stat.Damage, 40f, Tags.Mace)),
                Costs(More(Stat.Damage, -50f)));
            Add("follow-through", "Follow Through", "Great Maul", 20, "Nobody ever told it where to stop.",
                Gives(Rules(Rule.Overkill), Inc(Stat.Damage, 40f, Tags.Mace)),
                Costs(More(Stat.AttackSpeed, -40f)));
            Add("ringing-skull", "The Ringing Skull", "Warhammer", 10, "Everyone in the room hears it, one way or another.",
                Gives(Rules(Rule.StunSpreads), Chance(Tags.Stun, 30f)),
                Costs(Rules(Rule.NeverCrit)));
            Add("great-leveller", "The Great Leveller", "Titan's Hammer", 32, "Kings and beggars weigh the same to it.",
                Gives(Rules(Rule.Leveller)),
                Costs(Rules(Rule.OnlyLevels)));
            Add("aftershock", "Aftershock", "Maul", 2, "The second shake is the one that brings it down.",
                Gives(Trigger(TriggerEvent.Stun, Shelf.Aftershock), Chance(Tags.Stun, 25f)),
                Costs(More(Stat.AttackSpeed, -30f)));

            // Daggers.
            Add("widows-needle", "Widow's Needle", "Stiletto", 9, "One was enough, but she brought more.",
                Gives(Chance(Tags.Venom, 60f), Rules(Rule.VenomSlows), Flat(Stat.CritChance, 4f)),
                Costs(More(Stat.Damage, -30f, Tags.Hit)));
            Add("whisper", "Whisper", "Kris", 18, "Nobody hears it. That's rather the point.",
                Gives(More(Stat.Damage, 400f).Against(Condition.Unaware), Flat(Stat.Stealth, 30f)),
                Costs(More(Stat.Damage, -50f).Against(Condition.Aware)));
            Add("slow-kiss", "The Slow Kiss", "Shiv", 3, "It waits until there's enough to be worth it.",
                Gives(Rules(Rule.VenomBursts), Chance(Tags.Venom, 50f)),
                Costs(Inc(Stat.AilmentDuration, -60f, Tags.Venom)));
            Add("deadmans-hand", "Dead Man's Hand", "Assassin's Blade", 30, "Aces and eights, every time, or nothing at all.",
                Gives(Flat(Stat.CritMultiplier, 200f)),
                Costs(Rules(Rule.AllOrNothing)));
            Add("open-vein", "Open Vein", "Stiletto", 8, "It drinks slowly, and it never stops.",
                Gives(Rules(Rule.LeechesOverTime), Chance(Tags.Bleed, 30f)),
                Costs(Flat(Stat.HealthDrain, 1f)));

            // Spears.
            Add("homecoming", "Homecoming", "Boar Spear", 5, "Everything thrown comes back. Ask anyone who's left a village.",
                Gives(Rules(Rule.ProjectilesReturn), Flat(Stat.ProjectileCount, 1f)),
                Costs(More(Stat.Damage, -25f, Tags.Projectile)));
            Add("long-arm", "The Long Arm", "Glaive", 22, "Close is for people who like being hit.",
                Gives(Flat(Stat.MeleeReach, 3f), More(Stat.Damage, 100f, Tags.Melee).Against(Condition.AtRange)),
                Costs(More(Stat.Damage, -60f, Tags.Melee).Against(Condition.UpClose)));
            Add("hedgerow", "Hedgerow", "Partisan", 10, "Plant it, and wait for the season.",
                Gives(Every(1f, Shelf.Bristle).While(Condition.Stationary)),
                Costs(More(Stat.Damage, -40f).While(Condition.Moving)));
            Add("harpoon", "The Harpoon", "Boar Spear", 2, "Come here. No, closer.",
                Gives(Rules(Rule.Drags)),
                Costs(More(Stat.Damage, -30f, Tags.Projectile)));

            // Bows.
            Add("long-goodbye", "The Long Goodbye", "Hunting Bow", 7, "You'll see it coming. That's the idea.",
                Gives(Rules(Rule.PierceGrows), Flat(Stat.Pierce, 4f), Inc(Stat.ProjectileSpeed, 30f)),
                Costs(More(Stat.AttackSpeed, -30f)));
            Add("point-blank", "Point Blank", "Short Bow", 3, "Why aim, when you can lean?",
                Gives(Flat(Stat.ProjectileCount, 2f), More(Stat.Damage, 100f, Tags.Projectile).Against(Condition.UpClose)),
                Costs(More(Stat.Damage, -60f, Tags.Projectile).Against(Condition.AtRange)));
            Add("rainmaker", "Rainmaker", "Yew Longbow", 15, "What goes up comes down on someone.",
                Gives(Rules(Rule.Lobbed), Flat(Stat.ProjectileCount, 1f)),
                Costs(More(Stat.ProjectileSpeed, -40f)));
            Add("patient-eye", "The Patient Eye", "Horn Recurve", 28, "Breathe in. Breathe out. Breathe in.",
                Gives(EveryGain(1.5f, BuffCatalog.Aim).While(Condition.Stationary), Lose(TriggerEvent.Hit, BuffCatalog.Aim)),
                Costs(More(Stat.Damage, -40f).While(Condition.Moving)));
            Add("fork-in-the-road", "Fork in the Road", "Hunting Bow", 6, "Take both. You've time.",
                Gives(Rules(Rule.Forks)),
                Costs(More(Stat.Damage, -35f, Tags.Projectile)));
            Add("stragglers-friend", "The Straggler's Friend", "Yew Longbow", 12, "Nobody misses the one at the back.",
                Gives(More(Stat.Damage, 120f).Against(Condition.Isolated)),
                Costs(More(Stat.Damage, -50f).Against(Condition.Crowded)));

            // Staves.
            Add("tides-answer", "The Tide's Answer", "Bound Staff", 9, "Ask the sea anything, and this is what you'll hear.",
                Gives(Rules(Rule.WaterTrail), Inc(Stat.Damage, 40f, Tags.Cold), Inc(Stat.Damage, 40f, Tags.Lightning), Flat(Stat.Chain, 1f)),
                Costs(More(Stat.Damage, -50f, Tags.Fire)));
            Add("beggars-staff", "Beggar's Staff", "Walking Staff", 2, "Alms for the poor, and for the staff.",
                Gives(Inc(Stat.Damage, 1f).For(Per.Gold, 200f, 120f), Inc(Stat.GoldFind, 50f)),
                Costs(Rules(Rule.DeathTakesAll)));
            Add("perpetual-hymn", "The Perpetual Hymn", "Runed Staff", 20, "It has never once stopped to breathe.",
                Gives(Rules(Rule.FullVoice)),
                Costs(Rules(Rule.NoActive)));
            Add("all-in", "All In", "Ash Staff", 4, "Everything on the table. Everything.",
                Gives(Rules(Rule.ManaPower)),
                Costs(Rules(Rule.SpendsAll)));
            Add("conductors-baton", "The Conductor's Baton", "Bound Staff", 9, "On my count, all of you.",
                Gives(Rules(Rule.Choir)),
                Costs(More(Stat.Damage, -70f, Tags.Minion)));
            Add("stormcallers-crook", "Stormcaller's Crook", "Walking Staff", 12, "It only answers when the sky is listening.",
                Gives(Every(1.5f, Shelf.Skyfall).While(Condition.InRain), More(Stat.Damage, 50f, Tags.Lightning).While(Condition.InRain)),
                Costs(More(Stat.Damage, -40f).Unless(Condition.InRain)));
            Add("mutual-ruin", "Mutual Ruin", "Ash Staff", 14, "Nobody walks away from this one whole.",
                Gives(Rules(Rule.DeepHex), Chance(Tags.Hex, 30f)),
                Costs(Resist(Tags.None, -25f)));

            // Wands.
            Add("stormglass", "Stormglass Rod", "Bone Wand", 12, "It points at the next one, and then back again.",
                Gives(Rules(Rule.ChainsBack), Flat(Stat.Chain, 3f), Inc(Stat.Damage, 40f, Tags.Lightning)),
                Costs(More(Stat.Damage, -30f, Tags.Projectile)));
            Add("second-opinion", "Second Opinion", "Twig", 3, "It was right the first time as well.",
                Gives(Rules(Rule.SpellsTwice)),
                Costs(More(Stat.Damage, -40f, Tags.Spell)));
            Add("authors-quill", "The Author's Quill", "Bone Wand", 8, "It has never once been at a loss for words.",
                Gives(Flat(Stat.ProjectileCount, 2f, Tags.Turned), Inc(Stat.Potency, 25f, Tags.Opening)),
                Costs(More(Stat.Damage, -25f, Tags.Turned)));
            Add("both-ways", "Both Ways", "Carved Wand", 4, "It never could make up its mind.",
                Gives(Rules(Rule.Mirror)),
                Costs(More(Stat.BookCost, 100f)));
            Add("run-on", "Run-On Sentence", "Sceptre", 20, "And then, and then, and then.",
                Gives(Rules(Rule.RunOn)),
                Costs(More(Stat.CastSpeed, -35f)));
            Add("overdrawn", "Overdrawn", "Bone Wand", 8, "The bank of yourself is very understanding, until it isn't.",
                Gives(Rules(Rule.Overdraw)),
                Costs(Flat(Stat.HealthDrain, 6f).While(Condition.Overdrawn)));
            Add("dowsing-rod", "The Dowsing Rod", "Twig", 1, "It shakes near water, and worse near storms.",
                Gives(Rules(Rule.Conducts)),
                Costs(Resist(Tags.Lightning, -40f)));

            // Shields.
            Add("turned-cheek", "The Turned Cheek", "Heater Shield", 11, "Do unto others.",
                Gives(Flat(Stat.BlockChance, 25f), Rules(Rule.Retort)),
                Costs(More(Stat.Damage, -40f, Tags.Hit)));
            Add("the-door", "The Door", "Tower Shield", 18, "It came off its hinges with the keep.",
                Gives(Flat(Stat.BlockChance, 20f), Rules(Rule.Wall), Rules(Rule.CannotBeStunned)),
                Costs(More(Stat.MoveSpeed, -25f)));
            Add("eater-of-words", "Eater of Words", "Runed Ward", 15, "Every spell is only words, and words are food.",
                Gives(Flat(Stat.BlockChance, 30f, Tags.Spell), Rules(Rule.BlockFeedsMana)),
                Costs(Set(Stat.BlockChance, 0f, Tags.Attack)));
            Add("third-stroke", "The Third Stroke", "Round Shield", 4, "Two, and then three. Never four.",
                Gives(Rules(Rule.ThirdBlock)),
                Costs(Set(Stat.BlockChance, 0f)));
            Add("stillness", "The Stillness", "Wicker Ward", 3, "Nothing gets in while the door is shut.",
                Gives(Rules(Rule.Barricaded)),
                Costs(More(Stat.BarrierRecharge, -50f)));
            Add("parry-and-thrust", "Parry and Thrust", "Buckler", 1, "The answer is always in the question.",
                Gives(Flat(Stat.BlockChance, 20f), Gain(TriggerEvent.Block, BuffCatalog.Riposte), Lose(TriggerEvent.Hit, BuffCatalog.Riposte)),
                Costs(More(Stat.Damage, -30f).Unless(Condition.RecentlyBlocked)));
            Add("hedgehog", "The Hedgehog", "Heater Shield", 10, "Embrace it, if you really must.",
                Gives(Rules(Rule.ThornsAlways), Flat(Stat.Reflect, 30f)),
                Costs(More(Stat.Damage, -50f, Tags.Hit)));
            Add("shield-of-the-host", "Shield of the Host", "Targe", 12, "They volunteered. Mostly.",
                Gives(Rules(Rule.MinionsShoulder)),
                Costs(More(Stat.Health, -30f, Tags.Minion)));
            Add("gilded-aegis", "The Gilded Aegis", "Tower Shield", 17, "Everything has a price, including not being hit.",
                Gives(Rules(Rule.GoldAbsorbs)),
                Costs(Inc(Stat.GoldFind, -50f)));

            // Heads.
            Add("hollow-crown", "The Hollow Crown", "Circlet", 10, "It weighs nothing at all, and that's what's wrong with it.",
                Gives(Rules(Rule.CostIsTime), More(Stat.Damage, 30f, Tags.Spell)),
                Costs(Set(Stat.Mana, 1f)));
            Add("mockingbird", "Eye of the Mockingbird", "Leather Cap", 6, "It has heard you say that before.",
                Gives(Rules(Rule.Steady), Inc(Stat.Potency, 15f, Tags.Wild), Inc(Stat.Damage, 20f, Tags.Attack)),
                Costs(Rules(Rule.NeverCrit)));
            Add("deep-cowl", "The Deep Cowl", "Hood", 5, "Nothing looks back.",
                Gives(Flat(Stat.Stealth, 40f), Grant(Condition.InDarkness), Inc(Stat.Damage, 40f).While(Condition.InDarkness)),
                Costs(More(Stat.AreaOfEffect, -30f)));
            Add("upward-glance", "The Upward Glance", "Hood", 8, "It's worth looking up, now and again.",
                Gives(More(Stat.Potency, 35f, Tags.Falling), Inc(Stat.ProjectileSpeed, 50f, Tags.Falling)),
                Costs(More(Stat.AreaOfEffect, -40f)));
            Add("blindfold", "The Blindfold", "Linen Cowl", 8, "Justice is blind, and hits all the harder for it.",
                Gives(More(Stat.Damage, 50f)),
                Costs(Rules(Rule.Blind)));
            Add("crown-of-thorns", "Crown of Thorns", "Circlet", 6, "It never stops reminding you.",
                Gives(Grant(Condition.Bleeding), More(Stat.Damage, 50f).While(Condition.Bleeding)),
                Costs(Flat(Stat.HealthDrain, 1f), Flat(Stat.HealthDrain, 1.5f).While(Condition.Moving)));
            Add("quiet-man", "The Quiet Man's Mask", "Leather Cap", 3, "He was never there. Ask anyone who was.",
                Gives(Gain(TriggerEvent.Kill, BuffCatalog.Vanish), Flat(Stat.Stealth, 25f)),
                Costs(More(Stat.Health, -25f)));
            Add("many-voices", "Many Voices", "Sage's Coif", 14, "Each of them has something to add.",
                Gives(More(Stat.Potency, 25f).For(Per.Variety, 1f, 100f)),
                Costs(Rules(Rule.NoRepeat)));
            Add("one-track", "One-Track Mind", "Great Helm", 15, "It has heard of other ideas. It didn't care for them.",
                Gives(More(Stat.Damage, 10f).For(Per.Streak, 1f, 100f)),
                Costs(More(Stat.Damage, -30f)));
            Add("beacon", "The Beacon", "Iron Helm", 8, "Come and have a go, all of you.",
                Gives(More(Stat.Damage, 6f).For(Per.EnemiesNear, 1f, 90f), More(Stat.DamageTaken, -3f).For(Per.EnemiesNear, 1f, 30f)),
                Costs(Flat(Stat.Stealth, -100f)));
            Add("short-story", "The Short Story", "Circlet", 4, "It's better short. Everything is.",
                Gives(More(Stat.Potency, 40f, Tags.Opening), More(Stat.Potency, 40f, Tags.Closing)),
                Costs(More(Stat.Potency, -50f, Tags.Turned)));
            Add("minds-eye", "The Mind's Eye", "Hood", 12, "Why walk over, when you can think over?",
                Gives(Rules(Rule.Remote)),
                Costs(More(Stat.AttackSpeed, -30f)));

            // Bodies.
            Add("drowned-king", "Shell of the Drowned King", "Lamellar Coat", 12, "The sea hasn't finished with it.",
                Gives(Grant(Condition.InWater), Inc(Stat.Armour, 60f), Resist(Tags.Fire, 40f)),
                Costs(Resist(Tags.Lightning, -60f)));
            Add("mantle-of-cinders", "Mantle of Cinders", "Linen Robe", 8, "Whoever wore it first is part of it now.",
                Gives(Rules(Rule.FireHeals), Flat(Stat.AilmentAvoid, 100f, Tags.Burn)),
                Costs(Resist(Tags.Cold, -50f)));
            Add("patient-beast", "Hide of the Patient Beast", "Leather Jerkin", 7, "It took a long time to die, and didn't seem to mind.",
                Gives(Rules(Rule.LastGasp), Inc(Stat.Health, 20f)),
                Costs(More(Stat.Recovery, -40f)));
            Add("last-word", "Robe of the Last Word", "Silk Vestments", 13, "There's always something to add.",
                Gives(Inc(Stat.Barrier, 80f), Trigger(TriggerEvent.BarrierBroke, Shelf.LastWord)),
                Costs(More(Stat.Health, -30f)));
            Add("glass-heart", "The Glass Heart", "Silk Vestments", 12, "Beautiful, and you can see right through it.",
                Gives(More(Stat.Barrier, 200f)),
                Costs(Set(Stat.Health, 1f)), heroOnly: true);
            Add("salamander-skin", "Salamander Skin", "Brigandine", 20, "It only knows the one thing, but it knows it well.",
                Gives(Rules(Rule.AllAsFire), Flat(Stat.MaxResistance, 8f, Tags.Fire)),
                Costs(Set(Stat.Armour, 0f), Set(Stat.Evasion, 0f)));
            Add("furnace", "The Furnace", "Plate Harness", 15, "Stoke it, and stand well back.",
                Gives(Grant(Condition.Burning), Every(1f, Shelf.Kiln), More(Stat.Damage, 30f, Tags.Fire).While(Condition.Burning)),
                Costs(Flat(Stat.HealthDrain, 1.5f)), heroOnly: true);
            Add("bone-cage", "The Bone Cage", "Lamellar Coat", 8, "There's always room for one more inside.",
                Gives(Rules(Rule.MinionShield)),
                Costs(More(Stat.Health, -40f, Tags.Minion)));
            Add("hoarders-harness", "The Hoarder's Harness", "Gambeson", 3, "You never know when you'll need it.",
                Gives(Inc(Stat.Armour, 3f).For(Per.Carried, 1f, 180f), Inc(Stat.Health, 1f).For(Per.Carried, 1f, 60f)),
                Costs(Inc(Stat.MoveSpeed, -1f).For(Per.Carried, 2f, 40f)));
            Add("second-skin", "Second Skin", "Hunter's Coat", 12, "Whatever doesn't kill it, it learns from.",
                Gives(Rules(Rule.Adapts)),
                Costs(More(Stat.DamageTaken, 20f)));
            Add("thick-of-it", "In the Thick of It", "Leather Jerkin", 3, "Where else would you rather be?",
                Gives(More(Stat.DamageTaken, -40f).While(Condition.Surrounded), More(Stat.Damage, 40f).While(Condition.Surrounded)),
                Costs(More(Stat.Damage, -40f).While(Condition.Alone)));
            Add("hermits-habit", "The Hermit's Habit", "Linen Robe", 2, "Some people are better company from a distance.",
                Gives(More(Stat.Damage, 60f).While(Condition.Alone), Inc(Stat.MoveSpeed, 20f).While(Condition.Alone)),
                Costs(More(Stat.DamageTaken, 50f).While(Condition.Surrounded)));
            Add("unbowed", "Unbowed", "Plate Harness", 24, "It bends. It doesn't break, and neither do you.",
                Gives(Rules(Rule.HitCap)),
                Costs(More(Stat.Recovery, -50f)));
            Add("drunkards-vest", "The Drunkard's Vest", "Leather Tunic", 1, "Smells like courage.",
                Gives(Gain(TriggerEvent.Flask, BuffCatalog.Courage)),
                Costs(Rules(Rule.FlaskNoHeal)));
            Add("wolfpelt", "Wolfpelt", "Gambeson", 6, "The wolf didn't need it. It had the winter.",
                Gives(Grant(Condition.ColdLand), More(Stat.Damage, 50f, Tags.Cold).While(Condition.InSnow)),
                Costs(More(Stat.DamageTaken, 100f, Tags.Fire)));

            // Feet.
            Add("pilgrims-soles", "Pilgrim's Soles", "Leather Boots", 4, "Worn through at every shrine in the land.",
                Gives(Inc(Stat.MoveSpeed, 40f).While(Condition.OnRoad), Inc(Stat.Damage, 2f).For(Per.ShrinesKindled, 1f, 40f)),
                Costs(More(Stat.MoveSpeed, -25f).Unless(Condition.OnRoad)));
            Add("stilts", "The Long Step", "Iron Greaves", 11, "From up here, it's all so simple.",
                Gives(More(Stat.DamageTaken, -40f, Tags.Melee), Flat(Stat.MeleeReach, 2f)),
                Costs(More(Stat.DamageTaken, 40f, Tags.Projectile)));
            Add("cindertread", "Cindertread", "Sandals", 3, "Mind the rug.",
                Gives(Trigger(TriggerEvent.Dash, Shelf.EmberRing), Inc(Stat.MoveSpeed, 15f)),
                Costs(Resist(Tags.Cold, -30f)));
            Add("tidewalkers", "Tidewalkers", "Silk Slippers", 8, "They were never once dry.",
                Gives(Inc(Stat.MoveSpeed, 30f).While(Condition.InWater), Grant(Condition.WetLand), Inc(Stat.Barrier, 25f)),
                Costs(More(Stat.MoveSpeed, -30f).Unless(Condition.InWater)));
            Add("seven-league", "Seven League Boots", "Leather Boots", 4, "Each step a day's walk, and none of them careful.",
                Gives(More(Stat.DodgeSpeed, 150f)),
                Costs(Rules(Rule.NoSafeDodge), More(Stat.DodgeCooldown, 100f)));
            Add("hares-foot", "The Hare's Foot", "Sandals", 2, "Lucky for someone.",
                Gives(Set(Stat.DodgeCooldown, Rulebook.LeastDodgeCooldown)),
                Costs(Rules(Rule.DodgeCostsMana)));
            Add("stampede", "Stampede", "Mail Sabatons", 4, "Excuse me. Excuse me. Excuse me.",
                Gives(Trigger(TriggerEvent.Dash, Shelf.Trample)),
                Costs(More(Stat.DodgeCooldown, 50f)));
            Add("mudlarks", "Mudlark's Clogs", "Linen Wraps", 1, "Wherever you go, there's a puddle.",
                Gives(EveryMetres(6f, Shelf.Puddle)),
                Costs(More(Stat.Damage, -40f, Tags.Fire)));
            Add("hunters-tread", "The Hunter's Tread", "Leather Boots", 8, "Not a twig snaps.",
                Gives(Flat(Stat.Stealth, 90f).While(Condition.UnderTrees)),
                Costs(Flat(Stat.Stealth, -50f).Unless(Condition.UnderTrees)));
            Add("light-feet", "Light Feet", "Silk Slippers", 8, "Quick feet, quicker hands.",
                Gives(Echo(Stat.MoveSpeed, Stat.AttackSpeed), Echo(Stat.MoveSpeed, Stat.CastSpeed)),
                Costs(More(Stat.Health, -20f)));

            // Legs.
            Add("forced-march", "The Forced March", "Iron Cuisses", 10, "Left, right. Left, right. Left.",
                Gives(Inc(Stat.Damage, 30f).While(Condition.Moving), Rules(Rule.CannotBeStunned), Inc(Stat.MoveSpeed, 8f)),
                Costs(More(Stat.Damage, -40f).While(Condition.Stationary)));
            Add("waders", "The Waders", "Hide Trousers", 5, "Wet to the knee, and glad of it.",
                Gives(Inc(Stat.Evasion, 60f).While(Condition.InWater), Inc(Stat.MoveSpeed, 20f).While(Condition.InWater), Resist(Tags.Lightning, 20f)),
                Costs(More(Stat.Evasion, -40f).Unless(Condition.InWater)));
            Add("penitents-sackcloth", "Penitent's Sackcloth", "Linen Trousers", 6, "It itches, and that's the point.",
                Gives(Inc(Stat.Barrier, 60f), Set(Stat.BarrierDelay, 0f)),
                Costs(Rules(Rule.NoRegen)));
            Add("long-road", "The Long Road", "Ranger's Chaps", 14, "There's always further to go.",
                Gives(EveryGain(1f, BuffCatalog.Stride).While(Condition.Moving)),
                Costs(More(Stat.Damage, -30f).While(Condition.Stationary)));
            Add("festering-bandages", "Festering Bandages", "Silk Leggings", 10, "Leave them on. You've earned them.",
                Gives(More(Stat.Damage, 25f).For(Per.Ailments, 1f, 100f)),
                Costs(Inc(Stat.AilmentTaken, 100f)));
            Add("siege-engine", "The Siege Engine", "Plate Legguards", 16, "Set it up, and let it work.",
                Gives(More(Stat.AttackSpeed, 50f).While(Condition.Stationary), More(Stat.CastSpeed, 50f).While(Condition.Stationary)),
                Costs(More(Stat.MoveSpeed, -30f)));
            Add("dancers-silks", "The Dancer's Silks", "Leather Breeches", 6, "Step lively, and never twice in the same place.",
                Gives(Gain(TriggerEvent.Dodge, BuffCatalog.Dance), Lose(TriggerEvent.Struck, BuffCatalog.Dance)),
                Costs(Set(Stat.Armour, 0f)));
            Add("highwaymans-breeches", "The Highwayman's Breeches", "Hide Trousers", 6, "Your money or your life. Both, ideally.",
                Gives(Inc(Stat.GoldFind, 200f).While(Condition.OnRoad), Inc(Stat.ItemRarity, 60f).While(Condition.OnRoad)),
                Costs(More(Stat.Damage, -25f).Unless(Condition.OnRoad)));
            Add("bone-spurs", "Bone Spurs", "Mail Chausses", 3, "It grows back sharper.",
                Gives(Trigger(TriggerEvent.Struck, Shelf.BoneSpurs)),
                Costs(More(Stat.Damage, -25f)));
            Add("dead-weight", "Dead Weight", "Plate Legguards", 16, "Nothing moves it, least of all you.",
                Gives(Inc(Stat.Damage, 1f).For(Per.Armour, 25f, 150f)),
                Costs(Rules(Rule.NoDodge), More(Stat.MoveSpeed, -20f)));

            // Hands.
            Add("thunder-grip", "The Thunder Grip", "Iron Gauntlets", 9, "Nobody has shaken hands with it twice.",
                Gives(Convert(DamageType.Physical, DamageType.Lightning, 100f, Tags.Attack), Chance(Tags.Shock, 25f), Inc(Stat.Damage, 25f, Tags.Lightning)),
                Costs(Resist(Tags.Lightning, -40f)));
            Add("sleight-of-hand", "Sleight of Hand", "Silk Gloves", 10, "Watch closely. No, closer.",
                Gives(Echo(Stat.CastSpeed, Stat.AttackSpeed), Inc(Stat.CastSpeed, 20f)),
                Costs(More(Stat.Damage, -20f)));
            Add("cutpurses-fingers", "Cutpurse's Fingers", "Leather Gloves", 4, "What's yours is anyone's.",
                Gives(Inc(Stat.GoldFind, 40f), Inc(Stat.ItemRarity, 20f), Rules(Rule.DodgeCrit)),
                Costs(More(Stat.Health, -15f)));
            Add("inky-fingers", "Inky Fingers", "Silk Gloves", 6, "It gets on everything.",
                Gives(Inc(Stat.AreaOfEffect, 40f, Tags.Turned), Inc(Stat.PageDuration, 30f, Tags.Zone)),
                Costs(More(Stat.Potency, -25f, Tags.Opening)));
            Add("catalyst", "The Catalyst", "Cloth Wraps", 3, "It's not what you bring. It's what you start.",
                Gives(More(Stat.ReactionDamage, 150f), Inc(Stat.ReactionArea, 100f)),
                Costs(More(Stat.Damage, -40f, Tags.Hit)));
            Add("open-palm", "The Open Palm", "Mail Mitts", 3, "Why carry what you already have?",
                Gives(Added(DamageType.Physical, 3f).For(Per.Level, 1f).While(Condition.Unarmed)),
                Costs(More(Stat.Damage, -50f).Unless(Condition.Unarmed)));
            Add("midas-touch", "The Midas Touch", "Plate Gauntlets", 16, "Everything it touches, it ruins beautifully.",
                Gives(Inc(Stat.GoldFind, 400f)),
                Costs(Rules(Rule.NoItems)));
            Add("butterfingers", "Butterfingers", "Leather Gloves", 1, "Oops. Oops! Oh, lovely.",
                Gives(Rules(Rule.Fluke)),
                Costs(Rules(Rule.Fumble)));
            Add("kindling-mitts", "Kindling Mitts", "Silk Gloves", 9, "One spark, and the whole field goes.",
                Gives(Rules(Rule.Wildfire)),
                Costs(More(Stat.AilmentEffect, -40f, Tags.Venom)));
            Add("bedside-manner", "Bedside Manner", "Cloth Wraps", 1, "It's for your own good. Mostly theirs.",
                Gives(Rules(Rule.HealsHarm)),
                Costs(More(Stat.Damage, -50f, Tags.Hit)));

            // Rings.
            Add("understudy", "The Understudy", "Ring", 6, "It has learned all your lines.",
                Gives(Flat(Stat.MinionCount, 1f), Inc(Stat.Damage, 40f, Tags.Minion), Inc(Stat.Health, 30f, Tags.Minion)),
                Costs(Inc(Stat.MinionDuration, -40f)));
            Add("two-coins", "Two Coins", "Ring", 3, "Tails, you lose a little less.",
                Gives(Rules(Rule.LuckyHits)),
                Costs(More(Stat.Damage, -15f)));
            Add("knot-of-regret", "Knot of Regret", "Ring", 12, "You should have been kinder to them while they were alive.",
                Gives(Flat(Stat.RaiseFallen, 25f), Inc(Stat.Damage, 25f, Tags.Minion)),
                Costs(Inc(Stat.MinionDuration, -50f)));
            Add("dog-eared", "The Dog-Eared Page", "Ring", 7, "Someone keeps coming back to this bit.",
                Gives(More(Stat.Potency, 30f, Tags.Turned)),
                Costs(Inc(Stat.BookCost, 50f)));
            Add("bare-truth", "The Bare Truth", "Ring", 5, "Nothing to hide, and nothing to hide behind.",
                Gives(More(Stat.Damage, 12f).For(Per.EmptySlots, 1f, 96f), Inc(Stat.MoveSpeed, 4f).For(Per.EmptySlots, 1f, 32f)),
                Costs(More(Stat.Health, -15f)));
            Add("common-cause", "Common Cause", "Ring", 3, "Plain folk, plain steel, plain dealing.",
                Gives(More(Stat.Damage, 10f).For(Per.NormalWorn, 1f, 80f), More(Stat.DamageTaken, -4f).For(Per.NormalWorn, 1f, 32f)),
                Costs(Inc(Stat.ItemRarity, -60f)));
            Add("collectors-seal", "The Collector's Seal", "Ring", 10, "One of each, and then one more.",
                Gives(More(Stat.Damage, 8f).For(Per.UniquesWorn, 1f, 80f)),
                Costs(Inc(Stat.Health, -4f).For(Per.UniquesWorn, 1f, 40f)));
            Add("great-equaliser", "The Great Equaliser", "Ring", 8, "Everyone is equal before it, in the end.",
                Gives(Rules(Rule.EvenResist)),
                Costs(Flat(Stat.MaxResistance, -15f, Tags.Elemental)));
            Add("vow-of-poverty", "Vow of Poverty", "Ring", 2, "Blessed are the poor, for they travel light.",
                Gives(More(Stat.Damage, 80f)),
                Costs(More(Stat.Damage, -1f).For(Per.Gold, 100f, 45f)), heroOnly: true);
            Add("moneylenders-ring", "The Moneylender's Ring", "Ring", 10, "Everything can be bought. Everything.",
                Gives(More(Stat.Damage, 30f)),
                Costs(Rules(Rule.CostsGold)), heroOnly: true);
            Add("the-exchange", "The Exchange", "Ring", 12, "A fair trade, by its own measure.",
                Gives(Rules(Rule.Exchange)),
                Costs(More(Stat.Damage, -15f)));
            Add("borrowed-time", "Borrowed Time", "Ring", 16, "It all has to be paid back, with interest.",
                Gives(Rules(Rule.NoCooldowns)),
                Costs(Rules(Rule.TimeIsCost)), heroOnly: true);
            Add("hair-trigger", "The Hair Trigger", "Ring", 6, "It goes off at the slightest thing.",
                Gives(Rules(Rule.QuickTrigger)),
                Costs(More(Stat.BookCost, 60f)));
            Add("undertakers-ring", "The Undertaker's Ring", "Ring", 9, "Business is always good.",
                Gives(Gain(TriggerEvent.MinionDied, BuffCatalog.Mourning)),
                Costs(More(Stat.Health, -40f, Tags.Minion)));

            // Amulets.
            Add("first-ember", "The First Ember", "Amulet", 8, "Everything that has ever burned was lit from this.",
                Gives(Trigger(TriggerEvent.Kill, Shelf.Emberfall, 20f), Inc(Stat.Damage, 20f, Tags.Fire), Inc(Stat.Potency, 20f, Tags.Falling)),
                Costs(Inc(Stat.BookCost, 40f)));
            Add("frostbitten-locket", "Frostbitten Locket", "Amulet", 10, "Someone is still inside.",
                Gives(Rules(Rule.IceBursts), Chance(Tags.Freeze, 10f), Inc(Stat.Damage, 20f, Tags.Cold)),
                Costs(Resist(Tags.Fire, -40f)));
            Add("heart-of-the-swarm", "Heart of the Swarm", "Amulet", 14, "It beats a little out of time with yours.",
                Gives(Trigger(TriggerEvent.Struck, Shelf.VenomCloud, 30f), Inc(Stat.Damage, 30f, Tags.Venom), Flat(Stat.AilmentAvoid, 100f, Tags.Venom)),
                Costs(More(Stat.Health, -20f)));
            Add("bookmark", "The Bookmark", "Amulet", 9, "You can always pick up where you left off.",
                Gives(Rules(Rule.KillResets)),
                Costs(More(Stat.Potency, -20f)));
            Add("lodestone", "The Lodestone", "Amulet", 12, "Everything comes to those who wait. Everything.",
                Gives(Rules(Rule.Lodestone)),
                Costs(More(Stat.DamageTaken, 25f, Tags.Melee)));
            Add("keyholder", "The Keyholder's Chain", "Amulet", 5, "Every door it opens is worse than the last.",
                Gives(More(Stat.Damage, 4f).For(Per.Peril, 1f, 120f)),
                Costs(More(Stat.DamageTaken, 2f).For(Per.Peril, 1f, 60f)));
            Add("omen-reader", "The Omen Reader", "Amulet", 8, "It saw this coming. It sees everything coming.",
                Gives(More(Stat.Damage, 15f).For(Per.Omens, 1f, 75f), Inc(Stat.ItemRarity, 10f).For(Per.Omens, 1f, 50f)),
                Costs(More(Stat.Health, -15f)));
            Add("eye-of-the-storm", "Eye of the Storm", "Amulet", 10, "It's always calm at the centre. For you.",
                Gives(Every(1f, Shelf.StaticField)),
                Costs(Resist(Tags.Lightning, -40f)));
            Add("shepherds-bell", "The Shepherd's Bell", "Amulet", 6, "When it rings, they go out with a bang.",
                Gives(Rules(Rule.MinionsBurst)),
                Costs(Inc(Stat.MinionDuration, -60f)));
            Add("blood-of-the-flock", "Blood of the Flock", "Amulet", 8, "They give and give, and you take and take.",
                Gives(Rules(Rule.FlockPays)),
                Costs(Rules(Rule.FlockBleeds)));
            Add("slow-to-anger", "Slow to Anger", "Amulet", 10, "It takes a while to get going. Then it doesn't stop.",
                Gives(More(Stat.Damage, 5f).For(Per.FightSeconds, 1f, 100f)),
                Costs(More(Stat.Damage, -40f)), heroOnly: true);
            Add("string-of-teeth", "A String of Teeth", "Amulet", 12, "One from each of them, and they're all still sore about it.",
                Gives(More(Stat.Damage, 3f).For(Per.RaresSlain, 1f, 60f)),
                Costs(More(Stat.Damage, -25f)), heroOnly: true);
            Add("heart-of-oak", "Heart of Oak", "Amulet", 6, "The forest looks after its own.",
                Gives(Flat(Stat.HealthRegenPercent, 3f).While(Condition.UnderTrees), More(Stat.DamageTaken, -25f).While(Condition.UnderTrees)),
                Costs(More(Stat.Damage, -25f).Unless(Condition.UnderTrees)));
            Add("panacea", "Panacea", "Amulet", 14, "It cures everything, and nothing feels good any more.",
                Gives(Rules(Rule.Inoculated)),
                Costs(More(Stat.Recovery, -50f)));
            Add("barometer", "The Barometer", "Amulet", 6, "The worse it gets, the better you feel.",
                Gives(More(Stat.Recovery, 60f).While(Condition.InRain), More(Stat.Damage, 30f, Tags.Cold).While(Condition.InRain), More(Stat.Damage, 30f, Tags.Lightning).While(Condition.InRain)),
                Costs(More(Stat.Recovery, -30f).Unless(Condition.InRain)));
            Add("kettle", "The Kettle", "Amulet", 8, "Watched, it boils over anyway.",
                Gives(Trigger(TriggerEvent.Reaction, Shelf.Steam), Inc(Stat.ReactionDamage, 30f)),
                Costs(More(Stat.Damage, -25f, Tags.Hit)));
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
            item.Rarity = Rarity.Unique;
            item.Unique = unique.Id;
            return item;
        }
    }
}

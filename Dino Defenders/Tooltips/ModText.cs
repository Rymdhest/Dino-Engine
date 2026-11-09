using Dino_Defenders.Books;
using Dino_Defenders.Stats;
using System.Text;
using static Dino_Defenders.Stats.Rulebook;

namespace Dino_Defenders.Tooltips
{
    /// <summary>How stats are shown: what one is called, and whether a flat amount of it is a percentage.</summary>
    public readonly record struct StatName(string Name, bool Percent = false);

    public static class StatNames
    {
        private static readonly StatName[] Table = Build();

        public static StatName Of(Stat stat) => Table[(int)stat];

        private static StatName[] Build()
        {
            var table = new StatName[(int)Stat.Count];
            void Name(Stat stat, string name, bool percent = false) => table[(int)stat] = new StatName(name, percent);

            Name(Stat.Strength, "Strength");
            Name(Stat.Dexterity, "Dexterity");
            Name(Stat.Intellect, "Intellect");
            Name(Stat.Health, "Health");
            Name(Stat.Mana, "Mana");
            Name(Stat.Barrier, "Barrier");
            Name(Stat.HealthRegen, "Health Regeneration a second");
            Name(Stat.HealthRegenPercent, "of Health Regenerated a second", true);
            Name(Stat.ManaRegen, "Mana Regeneration a second");
            Name(Stat.ManaRegenPercent, "of Mana Regenerated a second", true);
            Name(Stat.BarrierRecharge, "Barrier Recharge", true);
            Name(Stat.BarrierDelay, "s Barrier Delay");
            Name(Stat.Recovery, "Recovery");
            Name(Stat.HealthLeech, "of Damage Leeched as Health", true);
            Name(Stat.ManaLeech, "of Damage Leeched as Mana", true);
            Name(Stat.HealthOnHit, "Health on Hit");
            Name(Stat.ManaOnHit, "Mana on Hit");
            Name(Stat.HealthOnKill, "Health on Kill");
            Name(Stat.ManaOnKill, "Mana on Kill");
            Name(Stat.BarrierOnKill, "Barrier on Kill");
            Name(Stat.FlaskEffect, "Flask Effect");
            Name(Stat.HealthDrain, "of Maximum Health lost a second", true);
            Name(Stat.MoveSpeed, "Movement Speed");
            Name(Stat.DodgeCooldown, "Dodge Cooldown");
            Name(Stat.DodgeSpeed, "Dodge Speed");
            Name(Stat.Damage, "Damage");
            Name(Stat.AddedPhysical, "Physical Damage");
            Name(Stat.AddedFire, "Fire Damage");
            Name(Stat.AddedCold, "Cold Damage");
            Name(Stat.AddedLightning, "Lightning Damage");
            Name(Stat.AddedMagic, "Magic Damage");
            Name(Stat.AddedPoison, "Poison Damage");
            Name(Stat.Convert, "Damage Converted");
            Name(Stat.GainAs, "Damage Gained");
            Name(Stat.AttackSpeed, "Attack Speed");
            Name(Stat.CastSpeed, "Cast Speed");
            Name(Stat.UseSpeed, "Use Speed");
            Name(Stat.CritChance, "Critical Chance", true);
            Name(Stat.CritMultiplier, "Critical Multiplier", true);
            Name(Stat.Accuracy, "Accuracy");
            Name(Stat.Penetration, "Resistance Penetration", true);
            Name(Stat.ArmourIgnore, "of Enemy Armour Ignored", true);
            Name(Stat.AreaOfEffect, "Area of Effect");
            Name(Stat.MeleeReach, "m Melee Reach");
            Name(Stat.ProjectileSpeed, "Projectile Speed");
            Name(Stat.ProjectileCount, "Projectiles");
            Name(Stat.Pierce, "Pierce");
            Name(Stat.Chain, "Chains");
            Name(Stat.PageDuration, "Page Duration");
            Name(Stat.CooldownRecovery, "Cooldown Recovery");
            Name(Stat.BookCost, "Book Cost");
            Name(Stat.Potency, "Potency");
            Name(Stat.AilmentChance, "Chance", true);
            Name(Stat.AilmentDuration, "Duration");
            Name(Stat.AilmentEffect, "Effect");
            Name(Stat.Execute, "Execute", true);
            Name(Stat.ReactionDamage, "Reaction Damage");
            Name(Stat.ReactionArea, "Reaction Area");
            Name(Stat.Armour, "Armour");
            Name(Stat.ArmourApplies, "of Armour Applies", true);
            Name(Stat.Evasion, "Evasion");
            Name(Stat.SpellEvasion, "of Evasion also applies to Spells", true);
            Name(Stat.BlockChance, "Chance to Block", true);
            Name(Stat.MaxBlock, "Maximum Chance to Block", true);
            Name(Stat.Resistance, "Resistance", true);
            Name(Stat.MaxResistance, "Maximum Resistance", true);
            Name(Stat.PhysicalReduction, "Physical Damage Reduction", true);
            Name(Stat.FlatReduction, "Damage from Hits");
            Name(Stat.DamageTaken, "Damage Taken");
            Name(Stat.TakenAs, "Damage Taken");
            Name(Stat.TakenFromMana, "of Damage Taken from Mana before Health", true);
            Name(Stat.AilmentAvoid, "Chance to Avoid", true);
            Name(Stat.AilmentTaken, "Duration on You");
            Name(Stat.StunThreshold, "Stun Threshold");
            Name(Stat.Reflect, "of Melee Damage Taken Reflected", true);
            Name(Stat.MinionCount, "Minions");
            Name(Stat.MinionDuration, "Minion Duration");
            Name(Stat.RaiseFallen, "Chance for Enemies you Kill to Rise beside you", true);
            Name(Stat.ItemRarity, "Item Rarity");
            Name(Stat.GoldFind, "Gold Found");
            Name(Stat.ExperienceGain, "Experience Gained");
            Name(Stat.Stealth, "Stealth", true);
            Name(Stat.ExplodeOnKill, "of the Maximum Health of Enemies you Kill Explodes as Fire", true);
            return table;
        }
    }

    /// <summary>A change to how the game works, in words. Numbers in <see cref="Text"/> come from where the rule reads them.</summary>
    /// <param name="Touches">What it's about: it shows on anything with all of these, and among a talent's tags.</param>
    /// <param name="Casts">A book it sets off, shown beside it with that book's own numbers.</param>
    /// <param name="Gives">A buff it hands out, shown the same way.</param>
    /// <param name="Hurts">Whether it's something nobody would want for itself, which is what an item's price is made of.</param>
    public sealed record RuleInfo(Rule Rule, string Text, Tags Touches = Tags.None, BookPlan? Casts = null, BuffDefinition? Gives = null, bool Hurts = false);

    /// <summary>
    /// Turns a modifier into a line of text by the rules in TOOLTIPS.md, so that nobody has to write one out by hand and have it drift from what the numbers do. Each part of a modifier has one
    /// way of being said, always the same: <c>+5</c> is flat, <c>increased</c> and <c>reduced</c> share a sum, <c>more</c> and <c>less</c> are multipliers of their own, and <c>is set to</c> replaces a stat.
    /// </summary>
    public static class ModText
    {
        private static readonly (Tags tag, string word)[] Adjectives =
        {
            (Tags.Melee, "Melee"), (Tags.Projectile, "Projectile"), (Tags.Area, "Area"), (Tags.Zone, "Zone"), (Tags.Chaining, "Chaining"), (Tags.Movement, "Movement"), (Tags.Buff, "Buff"),
            (Tags.Triggered, "Triggered"), (Tags.Reaction, "Reaction"), (Tags.Opening, "Opening Page"), (Tags.Turned, "Turned Page"), (Tags.Closing, "Closing Page"), (Tags.Wild, "Wild"), (Tags.Falling, "Falling"),
            (Tags.Elemental, "Elemental"), (Tags.Physical, "Physical"), (Tags.Fire, "Fire"), (Tags.Cold, "Cold"), (Tags.Lightning, "Lightning"), (Tags.Magic, "Magic"), (Tags.Poison, "Poison"),
            (Tags.Attack, "Attack"), (Tags.Spell, "Spell"), (Tags.Hit, "Hit"),
            (Tags.Burn, "Burn"), (Tags.Bleed, "Bleed"), (Tags.Venom, "Venom"), (Tags.Chill, "Chill"), (Tags.Freeze, "Freeze"), (Tags.Shock, "Shock"), (Tags.Hex, "Hex"), (Tags.Stun, "Stun"),
        };

        private static readonly (Tags tag, string word)[] Weapons =
        {
            (Tags.Sword, "Swords"), (Tags.Axe, "Axes"), (Tags.Mace, "Maces"), (Tags.Dagger, "Daggers"), (Tags.Spear, "Spears"), (Tags.Bow, "Bows"), (Tags.Staff, "Staves"), (Tags.Wand, "Wands"),
            (Tags.Unarmed, "Bare Hands"), (Tags.OneHanded, "One Handed Weapons"), (Tags.TwoHanded, "Two Handed Weapons"),
        };

        private static readonly (Condition condition, string own, string foe)[] Conditions =
        {
            (Condition.FullHealth, "while at Full Health", "at Full Health"),
            (Condition.LowHealth, "while on Low Health", "on Low Health"),
            (Condition.FullMana, "while at Full Mana", "at Full Mana"),
            (Condition.LowMana, "while on Low Mana", "on Low Mana"),
            (Condition.HasBarrier, "while you have Barrier", "with Barrier"),
            (Condition.NoBarrier, "while you have no Barrier", "without Barrier"),
            (Condition.Moving, "while Moving", "that are Moving"),
            (Condition.Stationary, "while Standing Still", "that are Standing Still"),
            (Condition.RecentlyHit, "if you've been Hit Recently", "that were Hit Recently"),
            (Condition.RecentlyKilled, "if you've Killed Recently", "that have Killed Recently"),
            (Condition.RecentlyCrit, "if you've dealt a Critical Hit Recently", "that have dealt a Critical Hit Recently"),
            (Condition.RecentlyBlocked, "if you've Blocked Recently", "that have Blocked Recently"),
            (Condition.RecentlyDodged, "if you've Dodged a Hit Recently", "that have Dodged a Hit Recently"),
            (Condition.RecentlyUsedBook, "if you've used a Book Recently", "that have used a Book Recently"),
            (Condition.Burning, "while Burning", "that are Burning"),
            (Condition.Bleeding, "while Bleeding", "that are Bleeding"),
            (Condition.Envenomed, "while Envenomed", "that are Envenomed"),
            (Condition.Chilled, "while Chilled", "that are Chilled"),
            (Condition.Frozen, "while Frozen", "that are Frozen"),
            (Condition.Shocked, "while Shocked", "that are Shocked"),
            (Condition.Hexed, "while Hexed", "that are Hexed"),
            (Condition.Stunned, "while Stunned", "that are Stunned"),
            (Condition.InWater, "while standing in Water", "standing in Water"),
            (Condition.UnderTrees, "while under the Trees", "under the Trees"),
            (Condition.OnRoad, "while on a Road", "on a Road"),
            (Condition.InDarkness, "while in Darkness", "in Darkness"),
            (Condition.HotLand, "in Hot Lands", "in Hot Lands"),
            (Condition.ColdLand, "in Cold Lands", "in Cold Lands"),
            (Condition.WetLand, "in Wet Lands", "in Wet Lands"),
            (Condition.HighLand, "in the High Country", "in the High Country"),
            (Condition.Rare, "while Rare", "that are Rare"),
            (Condition.Boss, "while a Boss", "that are Bosses"),
            (Condition.HoldingShield, "while holding a Shield", "holding a Shield"),
            (Condition.TwoHanded, "while wielding a Two Handed Weapon", "wielding a Two Handed Weapon"),
            (Condition.Unarmed, "while Unarmed", "that are Unarmed"),
            (Condition.DualWielding, "while Dual Wielding", "that are Dual Wielding"),
            (Condition.Wounded, "while Wounded", "that are Wounded"),
            (Condition.Overdrawn, "while Overdrawn", "that are Overdrawn"),
            (Condition.Surrounded, "while Surrounded", "that are Surrounded"),
            (Condition.Alone, "while Alone", "that are Alone"),
            (Condition.InRain, "while in Rain", "in Rain"),
            (Condition.InSnow, "while in Snow", "in Snow"),
            (Condition.Marked, "while Marked", "you've Marked"),
            (Condition.Unmarked, "while not Marked", "you haven't Marked"),
            (Condition.Unaware, "while Unaware", "that are Unaware"),
            (Condition.Aware, "while Aware", "that are Aware"),
            (Condition.UpClose, "while Up Close", "Up Close"),
            (Condition.AtRange, "while At Range", "At Range"),
            (Condition.Isolated, "while Isolated", "that are Isolated"),
            (Condition.Crowded, "while Crowded", "that are Crowded"),
        };

        private static readonly RuleInfo[] RuleTable =
        {
            new(Rule.CostsHealth, "Books Cost Health instead of Mana", Hurts: true),
            new(Rule.DeferredDamage, $"Damage you take becomes Debt, which drains your Health by {Say.Share(1f / Hits.DebtSeconds)} of itself a second, at least {Say.Share(DebtLeast)} of Maximum Health. Each Kill removes {Say.Share(DebtForgiven)} of it"),
            new(Rule.OverhealBarrier, $"Healing above Maximum Health becomes Barrier, up to {Say.Share(OverhealMost)} of Maximum Health over your Maximum Barrier"),
            new(Rule.BlockReflects, "Hits you Block are sent back as Physical Indirect Damage"),
            new(Rule.Guardian, $"Other Heroes within {Say.Metres(GuardianReach)} take {Say.Share(GuardianShare)} less Damage from Hits. You take that share instead"),
            new(Rule.Puppeteer, "Your Minions also get your Damage, Attack Speed, Cast Speed, Use Speed, Critical Chance and Critical Multiplier modifiers", Tags.Minion),
            new(Rule.EndlessVenom, $"Your Venom can have {AilmentRules.MostEndlessVenom} Stacks instead of {AilmentRules.MostVenom}", Tags.Venom),
            new(Rule.Carrier, $"You can't be Envenomed. Every {Say.Seconds(CarrierInterval)}, Trigger Contagion within {Say.Metres(Shelf.VenomCloud.First.Length)}", Tags.Venom, Casts: Shelf.VenomCloud),
            new(Rule.AilmentsLeap, $"Enemies you Kill pass their Burn, Bleed, Venom, Chill and Shock to Enemies within {Say.Metres(AilmentLeapReach)}"),
            new(Rule.FireFeedsBurn, "Your Fire Hits deal no Damage. All of it becomes a guaranteed Burn", Tags.Fire | Tags.Burn),
            new(Rule.BurningTrail, $"Leave Burning Ground every {Say.Metres(TrailSpacing)} you move", Tags.Fire, Casts: Shelf.CinderTrail),
            new(Rule.Rebirth, $"Once every {Say.Seconds(RebirthCooldown)}, when you would die, return with {Say.Share(RebirthHealth)} of Maximum Health and deal Fire Indirect Damage equal to {Say.Share(RebirthBlast)} of Maximum Health within {Say.Metres(RebirthRadius)}", Tags.Fire),
            new(Rule.SpellEcho, $"Spells repeat after {Say.Seconds(EchoDelay)} at {Say.Share(EchoStrength)} Damage, with no Cost. Triggered Spells don't repeat", Tags.Spell),
            new(Rule.OneCost, "Only the first Page of a Book has a Cost"),
            new(Rule.StaticCharge, $"Moving builds one charge per metre, up to {Say.Whole(MostStatic)}. Your next Hit uses all of them for {Say.Share(StaticPerCharge)} more Damage each", Tags.Hit),
            new(Rule.ChainsGrow, $"Each Chain deals {Say.Share(ChainGrowth - 1f)} more Damage instead of {Say.Share(1f - ChainFade)} less", Tags.Chaining),
            new(Rule.FreezeStores, $"Damage to Frozen Enemies is stored. When the Freeze ends it lands as Cold Indirect Damage with {Say.Share(StoredBonus)} more", Tags.Cold),
            new(Rule.HighGround, $"{Say.Share(HighGroundPerMetre)} more Damage per metre above your target, up to {Say.Share(HighGroundMost)}. {Say.Share(LowGroundPerMetre)} less per metre below, up to {Say.Share(LowGroundMost)}"),
            new(Rule.LongShot, $"Projectiles deal {Say.Share(LongShotPerMetre)} more Damage per metre flown, up to {Say.Share(LongShotMost)}, and {Say.Share(LongShotNearLess)} less within {Say.Metres(LongShotNear)}", Tags.Projectile),
            new(Rule.ProjectilesReturn, "Projectiles fly back to you when spent, and can Hit everything again", Tags.Projectile),
            new(Rule.Steady, "Nothing on your Pages is random: they find the nearest Enemy and go where they're aimed, and are worth what they would be otherwise", Tags.Wild),
            new(Rule.DodgeCrit, "After you Dodge a Hit, your next Hit is a Critical Hit"),
            new(Rule.WaterTrail, "Spells leave Standing Water where aimed. Triggered Spells don't", Tags.Spell, Casts: Shelf.Puddle),
            new(Rule.NoLeech, "You can't Leech", Hurts: true),
            new(Rule.NoRegen, "You can't Regenerate Health", Hurts: true),
            new(Rule.NeverCrit, "You can't deal Critical Hits", Hurts: true),
            new(Rule.CannotEvade, "You can't Evade", Hurts: true),
            new(Rule.CannotBeStunned, "You can't be Stunned"),
            new(Rule.CannotBeFrozen, "You can't be Frozen"),
            new(Rule.CannotBeChilled, "You can't be Chilled"),
            new(Rule.ImmuneToBurn, "You can't be Burned"),
            new(Rule.AlwaysShocked, $"You are always Shocked, taking at least {Say.Share(AlwaysShockedTaken)} more Damage", Tags.Shock, Hurts: true),
            new(Rule.ChillNeverFades, $"Your Chill doesn't fade while the Enemy is within {Say.Metres(ChillReach)}", Tags.Chill),
            new(Rule.FireHeals, $"{Say.Share(FireHealShare)} of Fire Damage from Hits heals you instead", Tags.Fire),
            new(Rule.NoOwnDamage, "Your Books deal no Damage", Hurts: true),
            new(Rule.LuckyHits, "Damage is rolled twice, keeping the better roll"),
            new(Rule.BurstsOnDeath, $"Bursts {Say.Seconds(BurstDelay)} after death for Fire Indirect Damage within {Say.Metres(BurstRadius)}, worth {Say.Share(BurstShare)} of an ordinary Enemy Hit", Tags.Fire),
            new(Rule.RalliesOnDeath, $"On death, gives Rallied to allies within {Say.Metres(RallyReach)}", Gives: BuffCatalog.Rallied),

            // The keystones of the tree as it was remade.
            new(Rule.MeleeSplash, $"Your Melee Hits also deal {Say.Share(SplashShare)} of their Damage to Enemies within {Say.Metres(SplashRadius)} of whoever they Hit, as Indirect Damage", Tags.Melee),
            new(Rule.SpellsArmTheBlade, "Increased, reduced, more and less Spell Damage apply to Attacks instead", Tags.Attack),
            new(Rule.BurnsStack, $"Your Burns add together instead of only the strongest counting, up to {Say.Whole(MostEmbers)} times the newest", Tags.Burn),
            new(Rule.ToggleReserves, $"Toggle Books hold back {Say.N(ReservePerCost)} Maximum Mana for each point of Cost while on, instead of taking Mana Regeneration"),
            new(Rule.BarrierPays, "Book Costs are paid from your Barrier before your Mana, and your Barrier doesn't protect you"),
            new(Rule.Shorthand, "Only the first Page of a Book takes time to use"),
            new(Rule.Prism, "Your Fire, Cold and Lightning Damage is split evenly between all three, and counts as all three", Tags.Elemental),
            new(Rule.StillWaters, "While at Full Mana, everything your Mana would recover goes to your Barrier instead, and your Barrier doesn't refill by itself"),
            new(Rule.Starburst, "Your Projectiles fly out all around you, evenly spaced", Tags.Projectile),
            new(Rule.Unbroken, "Using a Book doesn't slow you down"),
            new(Rule.Equilibrium, "Damage you take is paid in equal shares from your Health, Mana and Barrier, for as long as each has any"),
            new(Rule.NarrowWay, "Only the highest of your Strength, Dexterity and Intellect gives anything, and it gives twice as much"),
            new(Rule.LongCount, $"Cooldown Recovery does nothing for you. Each second of a Book's Cooldown gives it {Say.Share(LongCountPer)} more Potency, up to {Say.Share(LongCountMost)}, and a Book with no Cooldown has {Say.Share(LongCountIdle)} less"),

            // What only uniques do.
            new(Rule.NeverHealed, $"You can't be healed above {Say.Share(Hits.LowShare)} of Maximum Health", Hurts: true),
            new(Rule.Marks, "Your first Hit Marks an Enemy, until it dies"),
            new(Rule.BleedBurst, $"Enemies that die while Bleeding burst for Physical Indirect Damage equal to {Say.Share(BleedBurstShare)} of their Maximum Health, within {Say.Metres(ExplosionRadius)}", Tags.Bleed),
            new(Rule.FullCircle, "Your Melee Pages in a Cone go all the way round you", Tags.Melee),
            new(Rule.ExecuteAny, $"Any Hit can Execute, not only a Critical Hit, and your Hits Execute Enemies below a further {Say.Percent(ExecuteAnyReach)} of Maximum Health"),
            new(Rule.Overkill, $"Damage past what Kills an Enemy is dealt to the nearest Enemy within {Say.Metres(OverkillReach)}, as Physical Indirect Damage"),
            new(Rule.StunSpreads, $"Your Stuns pass on to Enemies within {Say.Metres(StunSpreadReach)}", Tags.Stun),
            new(Rule.Leveller, $"Your Hits deal Physical Damage equal to {Say.Share(LevellerShare)} of the Enemy's Maximum Health, or {Say.Share(LevellerBossShare)} of a Boss's", Tags.Hit),
            new(Rule.OnlyLevels, "Nothing else adds to the Damage of your Hits", Tags.Hit, Hurts: true),
            new(Rule.VenomBursts, $"When an Enemy has {Say.Whole(AilmentRules.MostVenom)} Stacks of your Venom, all of them deal their Damage at once", Tags.Venom),
            new(Rule.VenomSlows, $"Each Stack of your Venom slows the Enemy by {Say.Share(VenomSlowPerStack)}", Tags.Venom),
            new(Rule.AllOrNothing, "Hits that aren't Critical Hits deal no Damage", Tags.Hit, Hurts: true),
            new(Rule.LeechesOverTime, $"{Say.Share(OverTimeLeech)} of your Damage over Time is Leeched as Health", Tags.DamageOverTime),
            new(Rule.Drags, "Your Projectiles drag Enemies towards you", Tags.Projectile),
            new(Rule.Lobbed, "Your Projectiles fly up and come down where you aim, and count as Falling", Tags.Projectile),
            new(Rule.Forks, "Your Projectiles split into two more on their first Hit", Tags.Projectile),
            new(Rule.PierceGrows, $"Your Projectiles deal {Say.Share(PierceGrowth)} more Damage after each Enemy they Pierce", Tags.Projectile),
            new(Rule.FullVoice, $"Each Pulse of a Toggle Book does all it would by hand, instead of {Say.Share(AuraStrength)}"),
            new(Rule.NoActive, "You can't use Active Books", Hurts: true),
            new(Rule.ManaPower, $"Books deal {Say.Percent(1f)} more Damage for each {Say.Whole(ManaPowerStep)} Mana they spend"),
            new(Rule.SpendsAll, "Every Book you use spends all your Mana", Hurts: true),
            new(Rule.Choir, $"When you use a Book, each of your Minions uses it too, at {Say.Share(ChoirStrength)} of its Damage", Tags.Minion),
            new(Rule.DeepHex, $"Your Hex can have {Say.Whole(MostDeepHex)} Stacks instead of {Say.Whole(AilmentRules.MostHex)}", Tags.Hex),
            new(Rule.Mirror, "Books you use also go off the opposite way"),
            new(Rule.RunOn, $"When a Book reaches its end, it starts again from its first Page, once, at {Say.Share(RunOnStrength)} of its Damage"),
            new(Rule.Overdraw, "You can use Books without enough Mana, and be Overdrawn as far as your Maximum Mana"),
            new(Rule.Conducts, "Conduction runs through Shocked Enemies as well as through Water", Tags.Lightning),
            new(Rule.ChainsBack, "Your Chains can go back to Enemies they've already Hit", Tags.Chaining),
            new(Rule.SpellsTwice, "Your Spells Hit everything twice", Tags.Spell),
            new(Rule.DeathTakesAll, "When you fall, you lose all your Gold", Hurts: true),
            new(Rule.Retort, $"Damage you Block is kept, up to {Say.Share(RetortMost)} of Maximum Health, and goes into your next Hit as Physical Damage"),
            new(Rule.Wall, "While Standing Still, you Block every Projectile"),
            new(Rule.BlockFeedsMana, $"Spells you Block restore Mana equal to {Say.Share(BlockManaShare)} of their Damage"),
            new(Rule.ThirdBlock, $"One in every {Say.Whole(ThirdBlockEvery)} Hits against you is Blocked"),
            new(Rule.Barricaded, "While you have Barrier, you can't be given Ailments"),
            new(Rule.ThornsAlways, "You Reflect Damage from every Hit you take, not only Melee ones"),
            new(Rule.MinionsShoulder, $"{Say.Share(ShoulderShare)} of each Hit against you is taken by one of your Minions instead", Tags.Minion),
            new(Rule.GoldAbsorbs, $"{Say.Share(GoldAbsorbShare)} of each Hit against you is paid for in Gold instead, {Say.N(GoldPerHealth)} for each point of Health, while you have any"),
            new(Rule.CostIsTime, $"Books Cost nothing, but their Cooldown is {Say.Seconds(CostToRest)} longer for each point of Cost they had"),
            new(Rule.Blind, "You can't see where Enemies' blows will land", Hurts: true),
            new(Rule.NoRepeat, $"A Book used twice in a row deals {Say.Share(RepeatLess)} less Damage the second time", Hurts: true),
            new(Rule.Remote, "Your Melee Pages land where you aim instead of around you", Tags.Melee),
            new(Rule.LastGasp, $"When you would die, you have {Say.Seconds(GaspSeconds)} to Kill an Enemy. A Kill brings back {Say.Share(GaspHeal)} of Maximum Health, and otherwise you die. Once every {Say.Seconds(GaspCooldown)}", Gives: BuffCatalog.LastGasp),
            new(Rule.AllAsFire, "All Damage you take arrives as Fire", Tags.Fire),
            new(Rule.MinionShield, $"When you would die, your oldest Minion dies instead, and you're left with {Say.Share(MinionShieldHealth)} of Maximum Health", Tags.Minion),
            new(Rule.Adapts, "When you take a Hit, you become Inured to whatever did the most of it, and nothing else"),
            new(Rule.HitCap, $"No Hit can take more than {Say.Share(HitCapShare)} of your Maximum Health"),
            new(Rule.FlaskNoHeal, "Your Flasks heal nothing", Hurts: true),
            new(Rule.NoSafeDodge, "Your Dodge doesn't keep you from being Hit", Hurts: true),
            new(Rule.DodgeCostsMana, $"Each Dodge takes {Say.Share(DodgeManaShare)} of your Maximum Mana, and needs it", Hurts: true),
            new(Rule.NoDodge, "You can't Dodge", Hurts: true),
            new(Rule.NoItems, "Enemies you Kill drop no Items", Hurts: true),
            new(Rule.Fluke, $"Books you use have a {Say.Share(FlukeChance)} Chance to go off a second time"),
            new(Rule.Fumble, $"Books you use have a {Say.Share(FumbleChance)} Chance to do nothing at all", Hurts: true),
            new(Rule.Wildfire, $"Flashfire doesn't use up Venom, and passes the Ailments of whoever it struck to Enemies within {Say.Metres(AilmentLeapReach)}", Tags.Venom),
            new(Rule.HealsHarm, $"Whatever your Pages restore is also dealt as Magic Indirect Damage to Enemies within {Say.Metres(HealsHarmRadius)}"),
            new(Rule.EvenResist, "Your Fire, Cold and Lightning Resistances are each as high as the highest of them"),
            new(Rule.CostsGold, $"Books Cost Gold instead of Mana, {Say.N(GoldPerCost)} for each point", Hurts: true),
            new(Rule.Exchange, "Strength gives what Intellect would, and Intellect what Strength would"),
            new(Rule.NoCooldowns, "Books have no Cooldown"),
            new(Rule.TimeIsCost, $"Books Cost {Say.N(RestToCost)} more for each second of Cooldown they would have had", Hurts: true),
            new(Rule.QuickTrigger, $"Trigger Books go off after {Say.Share(QuickTriggerShare)} of what they wait for"),
            new(Rule.IceBursts, $"Enemies that die while Frozen burst for Cold Indirect Damage equal to {Say.Share(IceBurstShare)} of their Maximum Health, within {Say.Metres(ExplosionRadius)}", Tags.Freeze),
            new(Rule.KillResets, "When you Kill, the Book you last used by hand is off Cooldown"),
            new(Rule.Lodestone, $"Enemies within {Say.Metres(LodestoneReach)} are drawn towards you"),
            new(Rule.MinionsBurst, $"When one of your Minions dies or its time is up, it bursts for Fire Indirect Damage equal to {Say.Share(MinionBurstShare)} of its Maximum Health, within {Say.Metres(ExplosionRadius)}", Tags.Minion),
            new(Rule.FlockPays, "Books Cost nothing, except those that raise Minions"),
            new(Rule.FlockBleeds, "Each Book you use takes your oldest Minion, and can't be used without one, except those that raise Minions", Tags.Minion, Hurts: true),
            new(Rule.Inoculated, "You can't be given Ailments"),
        };

        public static IReadOnlyList<RuleInfo> Rules => RuleTable;

        /// <summary>What's said about a rule. Null only if someone has added one and given it no words, which BuildLab fails.</summary>
        public static RuleInfo? Of(Rule rule)
        {
            foreach (RuleInfo info in RuleTable)
            {
                if (info.Rule == rule) return info;
            }
            return null;
        }

        private static string Number(float value) => Say.N(MathF.Abs(value));

        private static string Adjective(Tags scope)
        {
            var words = new StringBuilder();
            foreach ((Tags tag, string word) in Adjectives)
            {
                if ((scope & tag) == 0) continue;
                // Fire already says it's elemental.
                if (tag == Tags.Elemental && (scope & (Tags.Fire | Tags.Cold | Tags.Lightning)) != 0) continue;
                words.Append(word).Append(' ');
            }
            return words.ToString();
        }

        private static string WeaponWords(Tags scope)
        {
            var words = new List<string>();
            foreach ((Tags tag, string word) in Weapons)
            {
                if ((scope & tag) != 0) words.Add(word);
            }
            return words.Count > 0 ? $" with {string.Join(" and ", words)}" : "";
        }

        private static string Own(Condition condition, string joiner = " ")
        {
            var words = new StringBuilder();
            foreach ((Condition one, string own, _) in Conditions)
            {
                if ((condition & one) != 0) words.Append(joiner).Append(own);
            }
            return words.ToString();
        }

        private static string Foes(Condition condition)
        {
            if (condition == Condition.None) return "";
            var words = new List<string>();
            foreach ((Condition one, _, string foe) in Conditions)
            {
                if ((condition & one) != 0) words.Add(foe);
            }
            return $" against Enemies {string.Join(" and ", words)}";
        }

        private static string Tail(in Mod mod)
        {
            var tail = new StringBuilder();
            tail.Append(WeaponWords(mod.Scope));
            tail.Append(Foes(mod.Foe));
            tail.Append(Own(mod.Need));
            if (mod.Without != Condition.None) tail.Append(" unless").Append(Own(mod.Without).Replace(" while ", " ").Replace(" if ", " "));
            if (mod.Per != Per.None) tail.Append(PerWords(mod));
            return tail.ToString();
        }

        private static string PerWords(in Mod mod)
        {
            bool one = mod.PerStep <= 1f;
            string step = one ? "" : $" {Number(mod.PerStep)}";
            if (mod.Per is Per.MoveSpeedAboveBase or Per.HealthSpent && step.Length == 0) step = $" {Number(1f)}";
            string name = mod.Per switch
            {
                Per.Armour => "Armour",
                Per.Evasion => "Evasion",
                Per.MaxHealth => "Maximum Health",
                Per.MaxMana => "Maximum Mana",
                Per.MaxBarrier => "Maximum Barrier",
                Per.MoveSpeedAboveBase => "% of Movement Speed above base",
                Per.Level => "Level",
                Per.Strength => "Strength",
                Per.Dexterity => "Dexterity",
                Per.Intellect => "Intellect",
                Per.Gold => "Gold carried",
                Per.ShrinesKindled => one ? "Shrine kindled in this land" : "Shrines kindled in this land",
                Per.RecentKills => one ? "Enemy Killed Recently" : "Enemies Killed Recently",
                Per.HealthSpent => "% of Health spent Recently",
                Per.Minions => one ? "Minion" : "Minions",
                Per.AlliesNear => one ? "Ally near you" : "Allies near you",
                Per.EnemiesNear => one ? "Enemy near you" : "Enemies near you",
                Per.Ailments => one ? "Ailment on you" : "Ailments on you",
                Per.EmptySlots => one ? "place where nothing is worn" : "places where nothing is worn",
                Per.NormalWorn => one ? "Normal Item worn" : "Normal Items worn",
                Per.UniquesWorn => one ? "Unique Item worn" : "Unique Items worn",
                Per.Carried => one ? "Item carried" : "Items carried",
                Per.LandLevel => "Land Level",
                Per.Omens => one ? "Omen on this land" : "Omens on this land",
                Per.RaresSlain => one ? "Rare Enemy or Boss slain since you last fell" : "Rare Enemies or Bosses slain since you last fell",
                Per.FightSeconds => one ? "second spent Fighting" : "seconds spent Fighting",
                Per.Streak => one ? "use of the same Book in a row" : "uses of the same Book in a row",
                Per.Variety => one ? "different Book used Recently" : "different Books used Recently",
                _ => "",
            };
            bool percent = mod.Kind != ModKind.Flat || StatNames.Of(mod.Stat).Percent;
            string cap = mod.PerCap > 0f ? $", up to {Number(mod.PerCap)}{(percent ? "%" : "")}" : "";
            return $" per{step} {name}{cap}".Replace(" %", "%");
        }

        public static string Describe(in Mod mod)
        {
            return mod.Kind switch
            {
                ModKind.Flat => DescribeFlat(mod),
                ModKind.Increased => DescribeScaled(mod, "increased", "reduced"),
                ModKind.More => DescribeScaled(mod, "more", "less"),
                ModKind.Override when mod.Stat == Stat.MinionCount => $"Pages raise only {Say.Whole(mod.Value)} of their Minions{Tail(mod)}",
                ModKind.Override => DescribeSet(mod),
                ModKind.Rule => DescribeRule(mod),
                ModKind.Grant => DescribeGrant(mod),
                ModKind.Divert => $"increased, reduced, more and less {StatNames.Of(mod.Stat).Name} apply to {Adjective(mod.Scope)}{StatNames.Of(mod.Into).Name} instead{Rate(mod)}",
                ModKind.Echo => $"increased, reduced, more and less {StatNames.Of(mod.Stat).Name} also apply to {Adjective(mod.Scope)}{StatNames.Of(mod.Into).Name}{Rate(mod)}",
                ModKind.Trigger => DescribeTrigger(mod),
                _ => "",
            };
        }

        private static string Rate(in Mod mod) => mod.Value == 100f ? "" : $", at {Number(mod.Value)}% of their value";

        // A stat replaced outright, said in the same words it's given in: in seconds where it's seconds, and with what it's narrowed to where that has words of its own.
        private static string DescribeSet(in Mod mod)
        {
            StatName name = StatNames.Of(mod.Stat);
            string subject = Subject(mod);
            string value = Number(mod.Value) + (name.Percent ? "%" : "");
            if (name.Name.StartsWith("s ") || mod.Stat == Stat.DodgeCooldown)
            {
                subject = $"{Adjective(mod.Scope)}{name.Name.TrimStart('s', ' ')} ";
                value = Say.Seconds(mod.Value);
            }
            if (mod.Stat == Stat.BlockChance && (mod.Scope & (Tags.Attack | Tags.Spell)) != 0) subject = (mod.Scope & Tags.Spell) != 0 ? "Chance to Block Spells " : "Chance to Block Attacks ";
            return $"{subject}is set to {value}{Tail(mod)}";
        }

        private static string Subject(in Mod mod) => $"{Adjective(mod.Scope)}{StatNames.Of(mod.Stat).Name} ";

        private static string DescribeRule(in Mod mod) => (Of(mod.Rule)?.Text ?? "") + Own(mod.Need);

        private static string DescribeGrant(in Mod mod)
        {
            var words = new List<string>();
            foreach ((Condition one, _, string foe) in Conditions)
            {
                if ((mod.Grants & one) != 0) words.Add(foe.StartsWith("that are ") ? foe["that are ".Length..] : foe);
            }
            return $"You always count as {string.Join(" and ", words)}";
        }

        private static string DescribeTrigger(in Mod mod)
        {
            string when = mod.On switch
            {
                TriggerEvent.Hit => "When you Hit",
                TriggerEvent.Critical => "When you deal a Critical Hit",
                TriggerEvent.Kill => "When you Kill",
                TriggerEvent.Struck => "When you're Hit",
                TriggerEvent.Block => "When you Block",
                TriggerEvent.Dodge => "When you Dodge a Hit",
                TriggerEvent.Flask => "When you use a Flask",
                TriggerEvent.Stun => "When you Stun an Enemy",
                TriggerEvent.Reaction => "When you cause a Reaction",
                TriggerEvent.BarrierBroke => "When your Barrier runs out",
                TriggerEvent.Dash => "When you Dodge",
                TriggerEvent.MinionDied => "When one of your Minions dies",
                TriggerEvent.Used => "When you use a Book",
                TriggerEvent.Time => $"Every {Say.Seconds(mod.PerStep)}",
                _ => $"Every {Say.Metres(mod.PerStep)} you move",
            };
            when += Own(mod.Need);
            if (mod.Without != Condition.None) when += " unless" + Own(mod.Without).Replace(" while ", " ").Replace(" if ", " ");

            if (mod.Gift != 0 && BuffCatalog.Get(mod.Gift) is BuffDefinition buff)
            {
                if (mod.Spends) return $"{when}, lose all {buff.Name}";
                return mod.Value >= 100f ? $"{when}, gain {buff.Name}" : $"{when}, {Number(mod.Value)}% Chance to gain {buff.Name}";
            }

            string chance = mod.Value >= 100f ? ", Trigger" : $", {Number(mod.Value)}% Chance to Trigger";
            return $"{when}{chance} {Shelf.Get(mod.Book).Name}";
        }

        // Damage narrowed to what lingers has a name of its own, and "Damage over Time Damage" isn't it.
        private static string Named(in Mod mod, string name)
        {
            return mod.Stat == Stat.Damage && (mod.Scope & Tags.DamageOverTime) != 0 ? "Damage over Time" : name;
        }

        private static string DescribeFlat(in Mod mod)
        {
            if ((mod.Scope & Tags.Minion) != 0) return $"Minions have {DescribeFlat(mod with { Scope = mod.Scope & ~Tags.Minion })}";

            StatName name = StatNames.Of(mod.Stat);
            if (MathF.Abs(mod.Value) == 1f) name = name with { Name = SingularOf(name.Name) };
            string sign = mod.Value >= 0f ? "+" : "-";
            string number = Number(mod.Value) + (name.Percent ? "%" : "");
            Tags scope = mod.Scope;
            bool toAttacks = (scope & Tags.Attack) != 0;
            bool toSpells = (scope & Tags.Spell) != 0;

            switch (mod.Stat)
            {
                case Stat.AddedPhysical or Stat.AddedFire or Stat.AddedCold or Stat.AddedLightning or Stat.AddedMagic or Stat.AddedPoison:
                    string to = toAttacks ? " to Attacks" : toSpells ? " to Spells" : "";
                    return $"{sign}{number} Added {Adjective(scope & ~(Tags.Attack | Tags.Spell))}{name.Name}{to}{Tail(mod)}";

                case Stat.Convert:
                    return $"{Number(mod.Value)}% of {Adjective(scope)}{mod.From} Damage is Converted to {mod.To}{Tail(mod)}";

                case Stat.GainAs:
                    return $"Gain {Number(mod.Value)}% of {Adjective(scope)}{mod.From} Damage as extra {mod.To}{Tail(mod)}";

                case Stat.TakenAs:
                    return $"{Number(mod.Value)}% of {mod.From} Damage is Taken as {mod.To}{Tail(mod)}";

                case Stat.Resistance:
                    return $"{sign}{number} {ResistanceName(scope)}{Tail(mod)}";

                case Stat.MaxResistance:
                    return $"{sign}{number} Maximum {ResistanceName(scope)}{Tail(mod)}";

                case Stat.Penetration:
                    return $"{Adjective(scope & ~(Tags.Attack | Tags.Spell))}Damage Penetrates {Number(mod.Value)}% Resistance{Tail(mod)}";

                case Stat.ArmourApplies:
                    return $"{Number(mod.Value)}% of Armour also applies to {Adjective(scope)}Damage{Tail(mod)}";

                case Stat.SpellEvasion:
                    return $"{Number(mod.Value)}% of Evasion also applies to Spells{Tail(mod)}";

                case Stat.AilmentChance:
                    Tags ailment = scope & Tags.Ailments;
                    string verb = ailment == Tags.Venom ? "Envenom" : Adjective(ailment).Trim();
                    return $"{sign}{number} Chance to {verb}{DoneWith(scope & ~Tags.Ailments)}{Tail(mod)}";

                case Stat.AilmentAvoid:
                    return $"{number} Chance to Avoid {(scope == Tags.None ? "Ailments" : Adjective(scope).Trim())}{Tail(mod)}";

                case Stat.FlatReduction:
                    return $"-{Number(mod.Value)} {Adjective(scope)}{name.Name}{Tail(mod)}";

                case Stat.BlockChance:
                    return $"{sign}{number} {(toSpells ? "Chance to Block Spells" : toAttacks ? "Chance to Block Attacks" : name.Name)}{Tail(mod)}";

                case Stat.Execute:
                    return $"Critical Hits Execute Enemies below {number} of Maximum Health{Tail(mod)}";

                case Stat.HealthDrain:
                    return $"Lose {number} of Maximum Health a second{Tail(mod)}";

                case Stat.TakenFromMana or Stat.HealthLeech or Stat.ManaLeech or Stat.Reflect or Stat.ArmourIgnore or Stat.RaiseFallen or Stat.ExplodeOnKill or Stat.PhysicalReduction or Stat.HealthRegenPercent or Stat.ManaRegenPercent:
                    return $"{number} {Adjective(scope)}{name.Name}{Tail(mod)}";

                case Stat.BarrierDelay or Stat.MeleeReach:
                    return $"{sign}{number} {name.Name}{Tail(mod)}";
            }

            return $"{sign}{number} {Adjective(scope)}{name.Name}{Tail(mod)}";
        }

        // Whatever a chance is narrowed to, past the ailment and the weapon.
        private static string DoneWith(Tags scope)
        {
            Tags rest = scope & ~(Tags.Weapons | Tags.OneHanded | Tags.TwoHanded);
            if (rest == Tags.None) return "";
            if (rest == Tags.Attack) return " with Attacks";
            if (rest == Tags.Spell) return " with Spells";
            return $" with {Adjective(rest)}Pages";
        }

        private static string SingularOf(string name) => name switch
        {
            "Minions" => "Minion",
            "Projectiles" => "Projectile",
            "Chains" => "Chain",
            _ => name,
        };

        private static string ResistanceName(Tags scope)
        {
            if (scope == Tags.None) return "to all Resistances";
            if (scope == Tags.Elemental) return "Elemental Resistance";
            return $"{Adjective(scope)}Resistance";
        }

        private static string DescribeScaled(in Mod mod, string up, string down)
        {
            string word = mod.Value >= 0f ? up : down;
            string name = Named(mod, StatNames.Of(mod.Stat).Name);
            Tags scope = mod.Scope & ~Tags.DamageOverTime;

            if ((scope & Tags.Minion) != 0)
            {
                return $"Minions have {Number(mod.Value)}% {word} {Adjective(scope & ~Tags.Minion)}{name}{Tail(mod)}";
            }

            if (mod.Stat is Stat.AilmentDuration or Stat.AilmentEffect or Stat.AilmentTaken)
            {
                string ailment = (scope & Tags.Ailments) != 0 ? Adjective(scope & Tags.Ailments).Trim() : "Ailment";
                return mod.Stat == Stat.AilmentEffect
                    ? $"{Number(mod.Value)}% {word} Effect of {ailment}{Tail(mod)}"
                    : $"{Number(mod.Value)}% {word} {ailment} {name}{Tail(mod)}";
            }

            return $"{Number(mod.Value)}% {word} {Adjective(scope)}{name}{Tail(mod)}";
        }
    }
}

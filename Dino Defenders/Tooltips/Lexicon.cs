using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Stats;
using static Dino_Defenders.Stats.Rulebook;
using static Dino_Defenders.Tooltips.Say;

namespace Dino_Defenders.Tooltips
{
    /// <summary>A word that means something exact. Wherever it's written, in any of its forms, it means this and nothing else.</summary>
    public sealed class Term
    {
        public string Name = "";
        public string Says = "";
        public string[] Forms = Array.Empty<string>();

        /// <summary>Plain enough to leave unmarked in a sentence. It's still defined, and in the glossary.</summary>
        public bool Basic;

        /// <summary>Only a term after a percentage, as "more" and "less" are ordinary words anywhere else.</summary>
        public bool AfterPercent;
    }

    /// <summary>Where a term was found in some text.</summary>
    public readonly record struct TermMark(int Start, int Length, Term Term);

    /// <summary>
    /// Every word in the game that means something exact. This is what stops a tooltip having a magic word in it: BuildLab reads all the text the game can show and fails on a capitalised word
    /// that isn't in here, on a word that is but isn't capitalised, and on a stat, rule, condition, tag or ailment with no term. Nobody marks a term up: anything that shows text finds them
    /// with <see cref="Find"/>, and a player rests the mouse on one to read it. Definitions read their numbers from where the rules do, never have one typed in, and stay short and plain.
    /// </summary>
    public static class Lexicon
    {
        private const int LongestForm = 10;  // Words.

        private static readonly List<Term> Everything = new();
        private static readonly Dictionary<string, Term> ByForm = new();
        private static readonly Dictionary<Stat, Term> Stats = new();
        private static readonly Dictionary<Tags, Term> TagTerms = new();
        private static readonly Dictionary<Condition, Term> ConditionTerms = new();
        private static readonly Dictionary<Ailment, Term> AilmentTerms = new();

        public static IReadOnlyList<Term> All => Everything;

        /// <summary>What's said about a stat, a tag, a condition or an ailment. Null only if someone has added one without saying what it means, which BuildLab fails.</summary>
        public static Term? Of(Stat stat) => Stats.GetValueOrDefault(stat);
        public static Term? Of(Tags tag) => TagTerms.GetValueOrDefault(tag);
        public static Term? Of(Condition condition) => ConditionTerms.GetValueOrDefault(condition);
        public static Term? Of(Ailment ailment) => AilmentTerms.GetValueOrDefault(ailment);

        public static Term? Named(string form) => ByForm.GetValueOrDefault(form);

        private static Term Word(string name, string says, params string[] forms)
        {
            var term = new Term { Name = name, Says = says, Forms = forms.Prepend(name).Distinct().ToArray() };
            Everything.Add(term);
            foreach (string form in term.Forms) ByForm.Add(form, term);
            return term;
        }

        private static Term Basic(string name, string says, params string[] forms)
        {
            Term term = Word(name, says, forms);
            term.Basic = true;
            return term;
        }

        private static Term On(Stat stat, Term term)
        {
            Stats.Add(stat, term);
            return term;
        }

        private static Term On(Tags tag, Term term)
        {
            TagTerms.Add(tag, term);
            return term;
        }

        private static Term On(Condition condition, Term term)
        {
            ConditionTerms.Add(condition, term);
            return term;
        }

        private static Term On(Ailment ailment, Term term)
        {
            AilmentTerms.Add(ailment, term);
            TagTerms.Add(AilmentRules.Tag(ailment), term);
            ConditionTerms.Add(AilmentRules.ConditionOf(ailment), term);
            return term;
        }

        static Lexicon()
        {
            HowNumbersStack();
            Basics();
            AttributeTerms();
            KindsOfBook();
            KindsOfDamage();
            Ailments();
            Reactions();
            Pools();
            Offence();
            Defence();
            Reaching();
            Company();
            Weapons();
            Conditions();
            Fortune();
        }

        private static void HowNumbersStack()
        {
            Word("Flat", "Added to the value before any percentages.");
            Term increased = Word("increased", "Adds up with all other increased and reduced on the same Stat, then applies once.", "reduced");
            Term more = Word("more", "Multiplies on its own, separately from everything else.", "less");
            increased.AfterPercent = true;
            more.AfterPercent = true;
            Word("set to", "Replaces the value. If there are several, the lowest counts.");
            Word("Stack", "Each one counts separately.", "Stacks");
            Basic("Maximum", "The highest a value can be.");
            Word("Keystone", "A major talent that changes how you play.", "Keystones");
            Basic("Base", "What every Hero has with nothing on.");
        }

        private static void Basics()
        {
            Basic("Stat", "A number on your Hero. See the character panel.", "Stats");
            Basic("Hero", "A player's character.", "Heroes");
            Basic("Enemy", "Anyone on the other side.", "Enemies");
            Basic("Kill", "Landing the killing blow. Your Minions' Kills count as yours.", "Killed", "Kills");
            Basic("Item", "Something you wear or hold. Its Level decides what it can roll, and wearing it has no requirement.", "Items");
            Basic("Bar", "Your Books along the bottom of the screen.");
            Basic("Gold", "Currency.");
            Basic("Level", $"Each Level gives {HeroState.PointsPerLevel} talent points, and Books Cost more.", "Levels");
            Word("Shrine", "A lit altar. Only those in the current land count.", "Shrines");
            Word("Enemies Killed Recently", $"Each Kill adds {Whole(1f)}. Falls by {N(RecentKillFade)} a second.", "Enemy Killed Recently");
            Word("Health spent Recently", $"Each Cost paid in Health adds to this. Falls by {Percent(HealthSpentFade)} a second.");
            Word("Ally near you", $"Another Hero within {Metres(AlliesReach)}.", "Allies near you");
            Word("Debt", "Damage you still owe, shown dark at the end of your Health.");
            Word("Regenerate", "Recover a little every second.", "Regeneration");
        }

        private static void AttributeTerms()
        {
            On(Stat.Strength, Word("Strength", $"Each point gives {N(StrengthHealth)} Health and {Percent(StrengthPhysical)} increased Physical Damage."));
            On(Stat.Dexterity, Word("Dexterity", $"Each point gives {N(DexterityAccuracy)} Accuracy, {N(DexterityEvasion)} Evasion and {Percent(DexterityAttackSpeed)} increased Attack Speed."));
            On(Stat.Intellect, Word("Intellect", $"Each point gives {N(IntellectMana)} Mana, {N(IntellectManaRegen)} Mana Regeneration and {Percent(IntellectSpellDamage)} increased Spell Damage."));
        }

        private static void KindsOfBook()
        {
            Basic("Book", "What you fight with: anything on your Bar, or anything that Triggers. It holds Pages. Right click one to read it.", "Books");
            Basic("Page", "Goes in a Book, and holds Stickers. When a Projectile has an Impact, the Book turns to the next Page, from there.", "Pages");
            Basic("Sticker", "Goes on a Page. Always does one thing for it and one thing against it.", "Stickers");
            On(Stat.Potency, Word("Potency", $"How strong everything a Page does is, up to {Share(MostPotency)}. A Page starts from where the one before it left off, and its sentences and Stickers go on from there. It counts for healing as much as Damage."));
            On(Tags.Opening, Word("Opening Page", "The first Page of a Book."));
            On(Tags.Turned, Word("Turned Page", "Any Page after the first."));
            On(Tags.Closing, Word("Closing Page", "The last Page of a Book. A single Page is both this and an Opening Page."));
            On(Tags.Wild, Word("Wild", "Something on the Page is random."));
            On(Tags.Falling, Word("Falling", "Comes straight down out of the sky."));
            Basic("Cone", "A wedge spreading out from where it starts.");
            Basic("Circle", "Everything within its Radius.");
            Basic("Line", "A narrow strip, starting from where it starts.");
            Basic("Rectangle", "A wide box. Away from you, it's centred on where it starts.");
            On(Tags.Attack, Word("Attack", "Any Book, unless you hold a Wand or a Staff. Speed comes from Attack Speed. Can be Evaded and Blocked.", "Attacks"));
            On(Tags.Spell, Word("Spell", "Any Book while you hold a Wand or a Staff. Speed comes from Cast Speed. Can be Blocked, but not Evaded.", "Spells"));
            On(Tags.Hit, Word("Hit", "One Page striking one target. Striking several targets is several Hits.", "Hits"));
            On(Tags.DamageOverTime, Word("Damage over Time", "Dealt each second instead of on a Hit. Only Resistance and Damage Taken reduce it."));
            Word("Indirect Damage", "Damage that isn't a Hit. Armour and Resistance reduce it, but it can't be Blocked, Evaded or Dodged. It can't be a Critical Hit, Leech or cause Ailments.");
            On(Tags.Triggered, Word("Trigger", $"Happens automatically, with no Cost. Limited by its Cooldown, or {Seconds(TriggerGap)} if it has none.", "Triggered", "Triggers"));
            On(Tags.Buff, Word("Buff", "A temporary bonus on you. Reapplying it refreshes it.", "Buffs"));
            On(Tags.Movement, Word("Movement", "Takes you somewhere, up to its Range."));
            On(Tags.Minion, Word("Minion", "Fights for you. Raising more of a kind replaces them.", "Minions"));
            Basic("Use Time", $"How long one use takes. The shortest possible is {Seconds(ShortestUse)}.");
            Basic("Impact", "When a use lands. A Dodge or a Stun before then cancels it.");
            Basic("Cost", $"Paid on each use: what every Page Costs, together. Grows by {Share(CostPerLevel)} per Level.", "Book Cost", "Book Costs", "Costs");
            Stats[Stat.BookCost] = ByForm["Book Cost"];
            On(Stat.CooldownRecovery, Word("Cooldown Recovery", "Makes Cooldowns pass faster."));
            Basic("Cooldown", "Time before it can be used again.", "Cooldowns");
            Basic("Damage per Second", "An average Hit on one target, Critical Hits included, per Use Time or Cooldown, whichever is longer. A Zone counts all its Pulses.");
        }

        private static void KindsOfDamage()
        {
            On(Stat.Damage, Basic("Damage", "What your Weapon deals. Numbers on a Page are before the target's defences."));
            On(Tags.Elemental, Word("Elemental", "Fire, Cold and Lightning."));
            On(Tags.Physical, Word("Physical", "Reduced by Armour, not Resistance. Can Bleed, Stun and Envenom."));
            On(Tags.Fire, Word("Fire", "Can Burn."));
            On(Tags.Cold, Word("Cold", "Always Chills. Can Freeze."));
            On(Tags.Lightning, Word("Lightning", "Can Shock."));
            On(Tags.Magic, Word("Magic", "Can Hex."));
            On(Tags.Poison, Word("Poison", "Can Envenom. Hits bypass Barrier."));
            Word("Converted", "Becomes another type, but still counts as the original for modifiers.", "Convert");
            Stats[Stat.Convert] = ByForm["Converted"];
            On(Stat.GainAs, Word("extra", "Added on top, keeping the original."));
            On(Stat.TakenAs, Word("Taken as", "Changes type before your defences apply."));
        }

        private static void Ailments()
        {
            Word("Ailment", "A condition left by a Hit, based on its Damage before the target's defences.", "Ailments");
            On(Stat.AilmentChance, Word("Chance", "Rolled on each Hit, for each target."));
            On(Stat.AilmentDuration, Basic("Duration", "How long it lasts."));
            On(Stat.AilmentEffect, Word("Effect", "How strong an Ailment is."));

            On(Ailment.Burn, Word("Burn", $"Deals {Share(AilmentRules.BurnShare)} of the Hit's Fire Damage over {Seconds(AilmentRules.BurnSeconds)}. Only the strongest applies.", "Burns", "Burning", "Burned"));
            On(Ailment.Bleed, Word("Bleed", $"Deals {Share(AilmentRules.BleedShare)} of the Hit's Physical Damage over {Seconds(AilmentRules.BleedSeconds)}, {Share(AilmentRules.BleedMoving)} more while Moving. Only the strongest applies.", "Bleeds", "Bleeding"));
            On(Ailment.Venom, Word("Venom", $"Each Stack deals {Share(AilmentRules.VenomShare)} of the Hit's Physical and Poison Damage over {Seconds(AilmentRules.VenomSeconds)}. Up to {AilmentRules.MostVenom} Stacks.", "Envenom", "Envenomed", "Envenoms"));
            On(Ailment.Chill, Word("Chill", $"Slows the target by {PercentRange(AilmentRules.LeastChill, AilmentRules.MostChill)} for {Seconds(AilmentRules.ChillSeconds)}, based on Cold Damage dealt. Effect can raise it to {Share(AilmentRules.ChillEffectLimit)}.", "Chills", "Chilled"));
            On(Ailment.Freeze, Word("Freeze", $"The target can't act for {Seconds(AilmentRules.FreezeSeconds)}. Guaranteed if Cold Damage reaches {Share(AilmentRules.FreezeThreshold)} of their Maximum Health.", "Freezes", "Frozen"));
            On(Ailment.Shock, Word("Shock", $"The target takes {PercentRange(AilmentRules.LeastShock, AilmentRules.MostShock)} more Damage for {Seconds(AilmentRules.ShockSeconds)}, based on Lightning Damage dealt.", "Shocks", "Shocked"));
            On(Ailment.Hex, Word("Hex", $"Each Stack lowers all Resistances by {N(AilmentRules.HexPerStack)}, Physical included. Up to {AilmentRules.MostHex} Stacks for {Seconds(AilmentRules.HexSeconds)}.", "Hexes", "Hexed"));
            On(Ailment.Stun, Word("Stun", $"The target can't act for {Seconds(AilmentRules.StunSeconds)}. Guaranteed if a Hit deals {Share(AilmentRules.StunThreshold)} of their Maximum Health, scaled by Stun Threshold.", "Stuns", "Stunned"));

            On(Stat.AilmentAvoid, Word("Chance to Avoid", "If it succeeds, the Ailment doesn't apply.", "Avoid"));
            On(Stat.AilmentTaken, Word("Duration on You", "Scales how long Ailments last on you."));
            Word("Execute", "A Critical Hit finishes off an Enemy left below this share of Maximum Health.");
            Stats[Stat.Execute] = ByForm["Execute"];
        }

        private static void Reactions()
        {
            string around = $"as Indirect Damage within {Metres(ReactionRadius)}";
            On(Tags.Reaction, Word("Reaction", "Happens when two things meet on a target.", "Reactions"));
            Word("Scald", $"Fire on a Chilled or Frozen target removes both, and deals {Share(ReactionShare)} of the Fire Damage {around}.");
            Word("Crackle", $"Cold on a Burning target removes it, and deals {Share(ReactionShare)} of the Cold Damage as Physical {around}.");
            Word("Flashfire", $"Fire on Venom removes it, and deals all its remaining Damage as Fire {around}.");
            Word("Shatter", "Physical Damage on a Frozen target ends the Freeze and doubles the Hit.");
            Word("Conduction", $"Lightning on a target in Water deals {Share(ReactionShare)} of it to everyone in Water within {Metres(ConductReach)}.");
            On(Stat.ReactionDamage, Word("Reaction Damage", "Scales the Damage of all Reactions."));
            On(Stat.ReactionArea, Word("Reaction Area", "Scales the size of all Reactions."));
        }

        private static void Pools()
        {
            On(Stat.Health, Basic("Health", "When this reaches zero, you die.", "Maximum Health"));
            On(Stat.Mana, Basic("Mana", $"Pays for Books. Refills {Percent(ManaRefill)} a second by itself.", "Maximum Mana"));
            On(Stat.Barrier, Word("Barrier", $"Absorbs Damage before Health. Poison Hits bypass it. Refills {Percent(ActorSheet.BaseBarrierRecharge)} a second, {Seconds(ActorSheet.BaseBarrierDelay)} after you were last Hit.", "Maximum Barrier"));
            On(Stat.HealthRegen, Word("Health Regeneration", "Health recovered each second.", "Health Regenerated"));
            Stats[Stat.HealthRegenPercent] = ByForm["Health Regeneration"];
            On(Stat.ManaRegen, Word("Mana Regeneration", $"Extra Mana recovered each second, on top of the {Percent(ManaRefill)} everyone gets.", "Mana Regenerated"));
            Stats[Stat.ManaRegenPercent] = ByForm["Mana Regeneration"];
            On(Stat.BarrierRecharge, Word("Barrier Recharge", "How much faster Barrier refills.", "Barrier Recharges"));
            On(Stat.BarrierDelay, Word("Barrier Delay", "Changes how long after a Hit Barrier starts to refill."));
            On(Stat.Recovery, Word("Recovery", "Scales Regeneration, Barrier Recharge, Leech, and Health and Mana gained on Hit and on Kill."));
            On(Stat.HealthLeech, Word("Leech", "Instantly restores a share of the Damage your Hit deals, after the target's defences.", "Leeched", "Leeching"));
            Stats[Stat.ManaLeech] = ByForm["Leech"];
            On(Stat.HealthOnHit, Word("on Hit", "Gained instantly on each Hit that deals Damage.", "Health on Hit", "Mana on Hit"));
            Stats[Stat.ManaOnHit] = ByForm["on Hit"];
            On(Stat.HealthOnKill, Word("on Kill", "Gained instantly when a Page Kills.", "Health on Kill", "Mana on Kill", "Barrier on Kill"));
            Stats[Stat.ManaOnKill] = ByForm["on Kill"];
            Stats[Stat.BarrierOnKill] = ByForm["on Kill"];
            Word("Waygate", "On the tree, taking one lets you take the one across from it.", "Waygates");
            Word("Flask", $"Instantly heals {Share(HeroRules.FlaskHeal)} of Maximum Health. Holds {HeroState.MaxFlasks}. Refills every {Whole(HeroState.KillsPerFlask)} Kills within {Metres(FlaskReach)}. An Elite counts as {Whole(FlaskFromElite)}, a Boss as {Whole(FlaskFromBoss)}.", "Flasks");
            On(Stat.FlaskEffect, Word("Flask Effect", "Scales how much a Flask heals."));
            On(Stat.MoveSpeed, Basic("Movement Speed", $"Bonus on your walking speed of {Metres(HeroState.BaseMoveSpeed)} a second."));
            Word("Dodge", $"A dash that lasts {Seconds(HeroRules.DashDuration)}. Immune to Hits for {Seconds(HeroRules.DashDuration + DodgeSafeExtra)}. Cancels what you were doing.", "Dodged", "Dodges");
            On(Stat.DodgeCooldown, Basic("Dodge Cooldown", $"Changes the {Seconds(ActorSheet.BaseDodgeCooldown)} between Dodges, down to {Seconds(LeastDodgeCooldown)}."));
            On(Stat.DodgeSpeed, Basic("Dodge Speed", $"Bonus on the {Metres(ActorSheet.BaseDodgeSpeed)} a second of a Dodge."));
        }

        private static void Offence()
        {
            Term added = Word("Added", $"Flat Damage on your Pages, rolling {Share(AddedSpread)} above or below its value.");
            foreach (Stat stat in new[] { Stat.AddedPhysical, Stat.AddedFire, Stat.AddedCold, Stat.AddedLightning, Stat.AddedMagic, Stat.AddedPoison }) Stats[stat] = added;

            On(Stat.AttackSpeed, Basic("Attack Speed", "Shortens the Use Time of Attacks."));
            On(Stat.CastSpeed, Basic("Cast Speed", "Shortens the Use Time of Spells."));
            On(Stat.CritChance, Basic("Critical Chance", "Chance for a Hit to be a Critical Hit."));
            On(Stat.CritMultiplier, Basic("Critical Multiplier", $"Added to the {Percent(ActorSheet.BaseCritMultiplier)} Damage of a Critical Hit."));
            Basic("Critical Hit", "A Hit multiplied by Critical Multiplier.", "Critical Hits");
            On(Stat.Accuracy, Basic("Accuracy", "Makes your Attacks harder to Evade."));
            On(Stat.Penetration, Word("Penetrates", "Ignores this much of the target's Resistance on Hits, but can't take it below zero.", "Resistance Penetration", "Penetration"));
            On(Stat.ArmourIgnore, Word("Enemy Armour Ignored", "Share of the target's Armour your Hits ignore."));
            Basic("Weapon Damage", "The Damage of your Weapon. Attacks deal a share of it.");
            Basic("Attacks per Second", "Base speed of a Weapon, before Attack Speed.");
        }

        private static void Defence()
        {
            On(Stat.Armour, Word("Armour", $"Reduces Physical Hits by Armour / (Armour + {Whole(Hits.ArmourFactor)} x Damage), up to {Share(Hits.MostReduction)}. Big Hits are reduced less."));
            On(Stat.ArmourApplies, Word("Armour also applies", "Armour also reduces that type, at this share.", "of Armour also applies"));
            On(Stat.Evasion, Word("Evasion", $"Chance for an Attack to miss: Evasion / (Evasion + {Whole(Hits.EvasionFactor)} x Accuracy + {Whole(Hits.EvasionMargin)}), up to {Share(Hits.MostEvasion)}. Spells can't be Evaded.", "Evade", "Evaded"));
            On(Stat.BlockChance, Word("Chance to Block", $"Stops a Hit completely. Attacks and Spells are separate chances, up to {Percent(ActorSheet.BaseMaxBlock)}.", "Block", "Blocks", "Blocked"));
            On(Stat.MaxBlock, Word("Maximum Chance to Block", $"Raises the {Percent(ActorSheet.BaseMaxBlock)} limit, up to {Percent(MostBlock)}."));
            On(Stat.Resistance, Word("Resistance", $"Reduces Damage of that type by this much, up to {Percent(ActorSheet.BaseMaxResistance)}. Below zero it increases it, down to {Percent(Hits.LeastResistance)}.", "Resistances"));
            On(Stat.MaxResistance, Word("Maximum Resistance", $"Raises the {Percent(ActorSheet.BaseMaxResistance)} limit, up to {Percent(MostResistance)}."));
            On(Stat.PhysicalReduction, Word("Physical Damage Reduction", "Added to what Armour reduces."));
            On(Stat.FlatReduction, Word("Damage from Hits", "Fixed amount removed from each Hit and from Indirect Damage."));
            On(Stat.DamageTaken, Word("Damage Taken", "Scales all Damage that reaches you."));
            On(Stat.TakenFromMana, Word("Taken from Mana", "This share of Damage comes out of Mana while you have any.", "Damage Taken from Mana before Health"));
            On(Stat.StunThreshold, Word("Stun Threshold", "Raises how much it takes to Stun you outright."));
            On(Stat.Reflect, Word("Melee Damage Taken Reflected", "This share of Melee Damage you take goes back as Physical Indirect Damage.", "Reflect"));
        }

        private static void Reaching()
        {
            On(Tags.Melee, Word("Melee", "Strikes targets next to you. Melee Reach extends it."));
            On(Tags.Projectile, Word("Projectile", "Flies until it strikes something or runs out of Range.", "Projectiles"));
            On(Tags.Area, Word("Area", "Affects a space rather than one target."));
            On(Tags.Zone, Word("Zone", "Stays on the ground for its Duration.", "Zones"));
            Word("Pulse", "A Zone Hits everything inside it this often.", "Pulses");
            On(Tags.Chaining, Word("Chaining", $"Jumps to the nearest target not yet struck, dealing {Share(1f - ChainFade)} less each jump."));
            On(Stat.Chain, Word("Chain", $"One more jump. A Projectile finds its next target within {Metres(ProjectileChainReach)}.", "Chains"));
            On(Stat.Pierce, Word("Pierce", "How many targets a Projectile passes through.", "Pierced", "Pierces"));
            On(Stat.ProjectileCount, Word("Projectile Count", "How many are fired at once. Each can Hit the same target."));
            On(Stat.ProjectileSpeed, Basic("Projectile Speed", "Metres a second."));
            On(Stat.AreaOfEffect, Word("Area of Effect", $"Scales the space covered. Double the space is {Share(MathF.Sqrt(2f) - 1f)} more Radius."));
            On(Stat.MeleeReach, Word("Melee Reach", "Metres on top of the Reach of Melee Pages."));
            On(Stat.PageDuration, Word("Page Duration", "Scales how long Zones and Buffs last."));
            Basic("Reach", "How far a Cone or a Line extends.");
            Basic("Range", "How far away it can be aimed.");
            Basic("Radius", "Distance from its centre.");
            Basic("Arc", "How wide a strike is.");
            Basic("Spread", "Angle between Projectiles.");
            Basic("Delay", "Time from Impact until it lands.");
            Basic("Moving", "Changing position. While using a Book you move at this share of your Movement Speed until its Impact.");
        }

        private static void Company()
        {
            On(Stat.MinionCount, Word("Minion Count", "How many a Page raises."));
            On(Stat.MinionDuration, Word("Minion Duration", "Scales how long Minions last."));
            On(Stat.RaiseFallen, Word("Chance to Rise", $"A Killed Enemy, except a Boss, returns as your Minion after {Seconds(RisenDelay)} for {Seconds(RisenSeconds)}, with {Percent(RisenHealthLess)} less Health.", "Chance for Enemies you Kill to Rise beside you"));
        }

        private static void Weapons()
        {
            void Kind(Tags tag, string name, string plural) => On(tag, Basic(name, "Attacks made with one have this tag.", plural));

            Kind(Tags.Sword, "Sword", "Swords");
            Kind(Tags.Axe, "Axe", "Axes");
            Kind(Tags.Mace, "Mace", "Maces");
            Kind(Tags.Dagger, "Dagger", "Daggers");
            Kind(Tags.Spear, "Spear", "Spears");
            Kind(Tags.Bow, "Bow", "Bows");
            Kind(Tags.Staff, "Staff", "Staves");
            Kind(Tags.Wand, "Wand", "Wands");
            On(Tags.Unarmed, Basic("Bare Hands", "Nothing in your main hand.", "Unarmed"));
            ConditionTerms[Condition.Unarmed] = ByForm["Bare Hands"];
            On(Tags.OneHanded, Basic("One Handed", "Leaves your other hand free.", "One Handed Weapons", "One Handed Weapon"));
            On(Tags.TwoHanded, Basic("Two Handed", "Uses both hands.", "Two Handed Weapons", "Two Handed Weapon"));
            ConditionTerms[Condition.TwoHanded] = ByForm["Two Handed"];
            Basic("Weapon", "What's in your main hand.", "Weapons");
            On(Condition.HoldingShield, Word("Shield", "Anything in your off hand."));
        }

        private static void Conditions()
        {
            string low = $"At or below {Share(Hits.LowShare)}.";
            On(Condition.FullHealth, Word("Full Health", "No Health missing."));
            On(Condition.LowHealth, Word("Low Health", low));
            On(Condition.FullMana, Word("Full Mana", "No Mana missing."));
            On(Condition.LowMana, Word("Low Mana", low));
            ConditionTerms[Condition.HasBarrier] = ByForm["Barrier"];
            ConditionTerms[Condition.NoBarrier] = ByForm["Barrier"];
            On(Condition.Moving, ByForm["Moving"]);
            On(Condition.Stationary, Word("Standing Still", "Not Moving."));
            On(Condition.RecentlyHit, Word("Recently", $"In the last {Seconds(Rulebook.Recently)}."));
            foreach (Condition recent in new[] { Condition.RecentlyKilled, Condition.RecentlyCrit, Condition.RecentlyBlocked, Condition.RecentlyDodged, Condition.RecentlyUsedBook }) ConditionTerms[recent] = ByForm["Recently"];

            On(Condition.InWater, Word("Water", $"Deeper than {Metres(WaterDepth)}, or made by a Page."));
            On(Condition.UnderTrees, Word("Trees", "Under dense tree cover."));
            On(Condition.OnRoad, Word("Road", "Any paved way or trail."));
            On(Condition.InDarkness, Word("Darkness", $"Within {Metres(DarknessReach)} of a graveyard, or anywhere under an Endless Night."));
            On(Condition.HotLand, Word("Hot Lands", "Hot, dry biomes."));
            On(Condition.ColdLand, Word("Cold Lands", "Cold biomes."));
            On(Condition.WetLand, Word("Wet Lands", "Wet biomes."));
            On(Condition.HighLand, Word("High Country", "High altitude biomes."));
            On(Condition.Elite, Word("Elite", "A stronger Enemy with a special trait."));
            On(Condition.Boss, Word("Boss", "The strongest Enemy of a place.", "Bosses"));
        }

        private static void Fortune()
        {
            On(Stat.ItemRarity, Word("Item Rarity", "Better drops. The highest in the party counts."));
            On(Stat.GoldFind, Word("Gold Found", "More Gold from drops. The highest in the party counts."));
            On(Stat.ExperienceGain, Word("Experience Gained", "Scales the experience you gain."));
            On(Stat.Stealth, Word("Stealth", $"Enemies notice you from this much closer, up to {Percent(MostStealth)}."));
            On(Stat.ExplodeOnKill, Word("Explosion", $"Fire Indirect Damage within {Metres(ExplosionRadius)} of what you Killed.", "Explodes"));
        }

        /// <summary>Every term in some text, in the order they come, with where each is. Longer forms win: "Attack Speed" is that, and not an Attack.</summary>
        public static void Find(string text, List<TermMark> marks)
        {
            marks.Clear();
            int i = 0;
            bool afterPercent = false;
            while (i < text.Length)
            {
                char c = text[i];
                if (!char.IsLetter(c))
                {
                    // A signed number is flat, whatever it's on. Ranges use a dash of their own.
                    if ((c == '+' || c == '-') && i + 1 < text.Length && char.IsDigit(text[i + 1]) && (i == 0 || text[i - 1] == ' ')) marks.Add(new TermMark(i, 1, ByForm["Flat"]));
                    if (!char.IsWhiteSpace(c)) afterPercent = c == '%';
                    i++;
                    continue;
                }

                int taken = Match(text, i, afterPercent, out Term? term);
                if (taken > 0)
                {
                    marks.Add(new TermMark(i, taken, term!));
                    i += taken;
                }
                else
                {
                    while (i < text.Length && char.IsLetter(text[i])) i++;
                }
                afterPercent = false;
            }
        }

        // The longest form that starts here and ends where a word does.
        private static int Match(string text, int start, bool afterPercent, out Term? found)
        {
            Span<int> ends = stackalloc int[LongestForm];
            int words = 0;
            int at = start;
            while (words < LongestForm && at < text.Length && char.IsLetter(text[at]))
            {
                while (at < text.Length && char.IsLetter(text[at])) at++;
                ends[words++] = at;
                if (at >= text.Length || text[at] != ' ') break;
                at++;
            }

            for (int word = words - 1; word >= 0; word--)
            {
                if (!ByForm.TryGetValue(text.Substring(start, ends[word] - start), out Term? term)) continue;
                if (term.AfterPercent && !afterPercent) continue;
                found = term;
                return ends[word] - start;
            }
            found = null;
            return 0;
        }
    }
}

namespace Dino_Defenders.Stats
{
    /// <summary>
    /// Every number in the rules that isn't on a stat. What happens in a fight reads these and what a player reads is written from these, so the two can't disagree: a number
    /// that changes here changes in every tooltip that mentions it. Those that belong to something with a home of its own are public there instead: <see cref="AilmentRules"/>,
    /// <see cref="Hits"/>, the bases on <see cref="ActorSheet"/>. A new rule's numbers go here, never in the line of code that uses them, and BuildLab fails if one does.
    /// </summary>
    public static class Rulebook
    {
        // What everyone has. Life and mana are the only things a hero starts with: everything else on the sheet starts at nothing.
        public const float BaseHealth = 100f;
        public const float BaseMana = 100f;
        public const float ManaRefill = 2.2f;           // Percent of maximum mana a second, for everyone. It's a rule and not a stat, or 100 mana spent would never come back.

        // What a point of an attribute is worth, and how many a node on the tree gives.
        public const float StrengthHealth = 1f;
        public const float StrengthPhysical = 0.1f;     // Percent increased.
        public const float DexterityAccuracy = 1f;
        public const float DexterityEvasion = 1f;
        public const float DexterityAttackSpeed = 0.05f;
        public const float IntellectMana = 1f;
        public const float IntellectManaRegen = 0.01f;  // A second.
        public const float IntellectSpellDamage = 0.1f;
        public const int NodeAttribute = 5;
        public const float NodeHealth = 6f;             // What every node gives as well, as levels give none and only strength is otherwise any life at all.

        // How long ago "Recently" is, and what's counted over it.
        public const float Recently = 4f;               // Seconds.
        public const float RecentKillFade = 0.25f;      // Kills a second that the count of recent kills forgets.
        public const float HealthSpentFade = 15f;       // Percent of maximum health a second.

        // Books.
        public const float CostPerLevel = 0.03f;        // Of a page's cost, added at each level past the first.
        public const float AddedSpread = 0.2f;          // Flat damage is a range this far either side of what it says.
        public const float ShortestUse = 0.07f;         // Seconds. No book is used quicker.
        public const float TriggerGap = 0.1f;           // Seconds. The least a trigger waits between goes, if its book has no cooldown.
        public const float FirstPulse = 0.1f;           // Seconds from a zone being laid to its first pulse.
        public const float ShortestZone = 0.5f;         // Seconds. No zone is shorter.
        public const float FlightGrace = 0.25f;         // Seconds a projectile flies past its range.

        // What a page is before anything's written on it.
        public const float PageUseTime = 0.6f;          // Seconds, with bare hands. A weapon goes by how it compares with those.
        public const float PageImpactAt = 0.5f;         // Of its use, for what leaves the hands.
        public const float PageStrikeAt = 0.25f;        // The same, for what's done with them.
        public const float PageMoveFactor = 0.55f;      // Of how fast its user walks, while it's going.
        public const float PageRange = 30f;             // Metres. How far a page can be aimed, and how far what flies goes.
        public const float ProjectileSpeed = 40f;       // Metres a second.
        public const float ProjectileSpread = 0.16f;    // Radians between neighbouring projectiles.
        public const float ProjectileCost = 3f;         // Of the Cost of a page, for making anything fly.
        public const float OutcomeCost = 3f;            // Onto a page's Cost, for each Outcome past the first.
        public const float ConeReach = 6.4f;            // Metres.
        public const float ConeArc = 2.4f;              // Radians.
        public const float CircleRadius = 7f;
        public const float LineLength = 22f;
        public const float LineWidth = 1.5f;            // Metres either side of the middle.
        public const float BoxLength = 11f;
        public const float BoxWidth = 4f;
        public const float PulseInterval = 0.5f;        // Seconds between a lingering page's pulses.
        public const float LingerShort = 4f;            // Seconds.
        public const float LingerLong = 8f;
        public const float LingerPremium = 1.5f;        // What a lingering page does over its whole length, of what it says, as anything can walk out of it.
        public const float ScatterNear = 3f;            // Metres.
        public const float ScatterMid = 6f;
        public const float ScatterFar = 12f;
        public const float RainScatter = 3f;            // Metres apart that several falling at once come down, if nothing says.
        public const float RestShort = 4f;              // Seconds.
        public const float RestLong = 10f;
        public const float SkyHeight = 28f;             // Metres over the ground that what falls starts from.
        public const float FlightHeight = 3.2f;         // Metres over the ground that what's thrown goes at.
        public const float FlightOffset = 1.5f;         // Metres ahead of someone that what's thrown starts from.
        public const float WaterStays = 6f;             // Seconds, for water made at once.
        public const float SeekReach = 30f;             // Metres a page looks for someone in.
        public const float PushSpeed = 18f;             // Metres a second, for what throws or drags.
        public const float HeavyUse = 0.95f;            // Seconds. A use this long or longer looks heavy.
        public const float WideReach = 8f;              // Metres. A shape this big or bigger is worth showing, and goes round the whole body.
        public const float DualWieldSpeed = 10f;        // Percent more attacks a second with a One Handed Weapon in each hand, on top of the two weapons' average.

        // How far a book can go, however it's written. No number on a page can stop one branching, so these do.
        public const int MostPages = 6;
        public const int MostOutcomes = 3;
        public const int MostStickers = 5;              // On a page.
        public const float AuraPulse = 1f;              // Seconds between a Toggle Book's pulses, at bare hands' pace.
        public const float FastestAura = 0.25f;         // Seconds. However it's quickened, it goes no faster than this.
        public const float AuraStrength = 0.4f;         // Of what its pages would do if they were used by hand.
        public const float LeastToggleCost = 1f;        // Mana Regeneration. A page that costs nothing can't be left on for nothing.
        public const int MostProjectiles = 12;          // From a page, whatever's been stuck on it.
        public const float MostStickerGood = 130f;      // What any sticker can say, which is also what a save can claim.
        public const float MostStickerBad = 60f;
        public const int MostMade = 48;                 // Projectiles and areas from one use.
        public const int MostZones = 6;                 // Lingering areas from one caster.
        public const int MostMinions = 12;              // From one caster, the oldest going first.
        public const float LeastPotency = 0.05f;
        public const float MostPotency = 6f;

        // Things that go on from one to the next.
        public const float ChainFade = 0.85f;           // What a chain's next leap is worth, of the last.
        public const float ChainGrowth = 1.25f;         // The same, for those whose chains grow.
        public const float ProjectileChainReach = 18f;  // Metres a projectile looks for its next target.

        // Where what's been done spreads to.
        public const float ExplosionRadius = 6f;        // Metres, around whatever a kill set off.
        public const float AilmentLeapReach = 10f;      // Metres ailments leap from the dead.
        public const float ReactionShare = 0.6f;        // Of the blow, dealt around by a reaction.
        public const float ReactionRadius = 5f;         // Metres.
        public const float ConductReach = 12f;          // Metres lightning runs through water.

        // Keystones and uniques.
        public const float EchoDelay = 0.6f;            // Seconds.
        public const float EchoStrength = 0.5f;
        public const float TrailSpacing = 3.2f;         // Metres walked between patches of burning ground.
        public const float CarrierInterval = 1f;        // Seconds.
        public const float RebirthCooldown = 90f;       // Seconds.
        public const float RebirthHealth = 0.5f;        // Of maximum health, risen with.
        public const float RebirthBlast = 0.5f;         // Of maximum health, dealt around.
        public const float RebirthRadius = 12f;         // Metres.
        public const float StoredBonus = 0.6f;          // What ice adds to the damage held in it.
        public const float ChillReach = 25f;            // Metres a chill holds at, for those whose chill doesn't fade.
        public const float GuardianReach = 20f;         // Metres.
        public const float GuardianShare = 0.3f;        // Of a friend's hit, taken instead.
        public const float FireHealShare = 0.25f;
        public const float DebtForgiven = 0.15f;        // Of what's owed, for each kill.
        public const float DebtLeast = 0.02f;           // Of maximum health a second: what's owed never falls due slower than this.
        public const float OverhealMost = 0.5f;         // Of maximum health: how far barrier can be overfilled.
        public const float MostStatic = 100f;           // Charge, at one a metre.
        public const float StaticPerCharge = 0.01f;     // Onto a blow, for each.
        public const float AlwaysShockedTaken = 0.2f;   // Onto damage taken, for those who are always shocked.
        public const float HighGroundPerMetre = 0.04f;
        public const float HighGroundMost = 0.6f;
        public const float LowGroundPerMetre = 0.02f;
        public const float LowGroundMost = 0.6f;
        public const float LongShotPerMetre = 0.015f;
        public const float LongShotMost = 0.9f;
        public const float LongShotNear = 10f;          // Metres, inside which it's weaker.
        public const float LongShotNearLess = 0.4f;

        // What only uniques do. Nobody has played any of it, so these are only where to start.
        public const float BleedBurstShare = 0.25f;     // Of the bleeder's maximum health, dealt around it as Physical.
        public const float OverkillReach = 12f;         // Metres what's left over looks for someone in.
        public const float ExecuteAnyReach = 12f;       // Percent of maximum health, onto what executes, for those whose every hit can.
        public const float StunSpreadReach = 6f;
        public const float LevellerShare = 0.04f;       // Of a target's maximum health, in each Hit.
        public const float LevellerBossShare = 0.01f;   // The same, for a boss.
        public const float VenomSlowPerStack = 0.05f;   // Off action speed.
        public const float OverTimeLeech = 0.03f;       // Of damage over time dealt, back as health.
        public const float DragSpeed = 14f;             // Metres a second towards whoever threw it.
        public const float ForkAngle = 0.35f;           // Radians either side of the way it was going.
        public const float PierceGrowth = 0.2f;         // More damage after each one passed through.
        public const float ManaPowerStep = 5f;          // Mana spent, for each 1% more.
        public const float ChoirStrength = 0.3f;        // Of what you'd do, for each minion that joins in.
        public const int MostDeepHex = 12;              // Stacks of Hex, for those who go deeper.
        public const float RunOnStrength = 0.5f;
        public const float RetortMost = 1f;             // Of maximum health: what can be kept from blocking.
        public const int ThirdBlockEvery = 3;
        public const float BlockManaShare = 0.3f;       // Of a blocked spell's damage, as mana.
        public const float ShoulderShare = 0.3f;        // Of a Hit, taken by a minion instead.
        public const float GoldAbsorbShare = 0.3f;      // Of each Hit, paid in gold while there's any.
        public const float GoldPerHealth = 0.5f;        // Gold for each point of health it pays for.
        public const float CostToRest = 0.08f;          // Seconds of rest for each point of Cost.
        public const float RestToCost = 10f;            // Cost for each second of rest.
        public const float GaspSeconds = 4f;
        public const float GaspCooldown = 60f;
        public const float GaspHeal = 0.4f;             // Of maximum health, back for a kill while gasping.
        public const float MinionShieldHealth = 0.3f;   // Of maximum health, kept when a minion dies instead.
        public const float HitCapShare = 0.2f;          // Of maximum health: the most a Hit can take.
        public const float DodgeManaShare = 0.12f;      // Of maximum mana, for each dodge.
        public const float FlukeChance = 0.25f;
        public const float FumbleChance = 0.25f;
        public const float FlukeDelay = 0.25f;          // Seconds between a book and its second going.
        public const float HealsHarmRadius = 8f;
        public const float HealsHarmShare = 1f;         // Of what's restored, dealt as Magic.
        public const float GoldPerCost = 3f;            // Gold for each point of a book's Cost.
        public const float QuickTriggerShare = 0.5f;    // Of what a Trigger Book waits for.
        public const float IceBurstShare = 0.3f;        // Of the frozen's maximum health, dealt around as Cold.
        public const float LodestoneReach = 14f;
        public const float LodestonePull = 4f;          // Metres a second.
        public const float MinionBurstShare = 0.6f;     // Of a minion's maximum health, dealt around as Fire.
        public const float RepeatLess = 0.5f;           // Off a book used twice running, for those who can't.
        public const float CloseReach = 6f;             // Metres: Up Close inside it, At Range beyond.
        public const float IsolatedReach = 6f;          // Metres an Enemy has to be from any other to be Isolated.
        public const float CrowdReach = 10f;            // Metres, for who's around a hero.
        public const int SurroundedBy = 5;              // Enemies within that.
        public const float RainFrom = 0.25f;            // How hard it has to be raining to count.
        public const float SnowFrom = 0.25f;

        // What enemies do at the end.
        public const float BurstDelay = 0.9f;           // Seconds from a body falling to its going up. A dodge is half that.
        public const float BurstRadius = 4.5f;
        public const float BurstShare = 0.45f;          // Of an ordinary enemy's blow. They come in numbers.
        public const float RallyReach = 16f;

        // Company.
        public const float AlliesReach = 24f;           // Metres, for what counts allies near you.
        public const float RisenSeconds = 20f;
        public const float MinionSeconds = 40f;         // How long what a page raises stays.
        public const float RisenDelay = 0.8f;           // Seconds from falling to rising.
        public const float RisenHealthLess = 40f;       // Percent less than it had.

        // A dodge, past the dash itself.
        public const float DodgeSafeExtra = 0.1f;       // Seconds it goes on protecting after the dash has ended.

        // What the land counts as.
        public const float WaterDepth = 0.3f;           // Metres.
        public const float DarknessReach = 90f;         // Metres past a graveyard's edge.
        public const float CanopyCover = 0.5f;          // Of the ground, for it to count as under the trees.
        public const float RoadCover = 0.5f;            // The same, for a road.
        public const float HotFrom = 0.7f;              // Biome temperature.
        public const float HotDrierThan = 0.4f;         // Biome moisture.
        public const float ColdFrom = 0.3f;             // Biome temperature.
        public const float WetFrom = 0.75f;             // Biome moisture.
        public const float HighFrom = 0.5f;             // Biome altitude.

        // Limits on what a sheet can come to.
        public const float MostBlock = 90f;
        public const float MostResistance = 90f;
        public const float MostStealth = 90f;
        public const float LeastMoveSpeed = 0.5f;
        public const float LeastDodgeCooldown = 0.2f;

        // Flasks.
        public const float FlaskReach = 80f;            // Metres from a kill that it still counts, which is also how far experience is shared.
        public const float FlaskFromBoss = 30f;         // Kills a boss counts for, towards a flask.
        public const float FlaskFromRare = 4f;

        // Getting about.
        public const float GuideCooldown = 14f;         // Seconds. The guiding wind is the client's own, and nothing is sent.

        // Enemies.
        public const float NightNotice = 1.4f;          // How much further enemies notice a hero under an endless night.
        public const float RareChance = 0.14f;          // Of each who holds a place.
        public const float MagicChance = 0.2f;          // Of each who holds a place and isn't rare.
        public const float WarhostRareChance = 0.35f;
        public const float WarhostLeaderRares = 2.5f;   // On the chance of a pack's leader being rare.
        public const float MostWarhostLeaderRares = 0.8f;
        public const float AbilityCooldown = 7f;        // Seconds before an enemy does what a trait gave it again, or it would do nothing else.

        // Lands.
        public const float DummyRest = 4f;              // Seconds a dummy is left alone before it's as good as new.
        public const float ShrineBlessing = 120f;       // Seconds a shrine's blessing lasts.
        public const float RiseDelay = 1.2f;            // Seconds from falling to rising again, on a land whose orders raise the dead.
        public const float RisenBonesLess = 50f;        // Percent less health than they had, risen as bones.
    }
}

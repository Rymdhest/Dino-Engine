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

        // Skills.
        public const float PowerPerLevel = 0.38f;       // Of a spell's level 1 damage, added at each level past the first.
        public const float CostPerLevel = 0.03f;        // Of a skill's cost, added at each level past the first.
        public const float AddedSpread = 0.2f;          // Flat damage is a range this far either side of what it says.
        public const float ShortestUse = 0.07f;         // Seconds. No skill is used quicker.
        public const float TriggerGap = 0.1f;           // Seconds. The least a trigger waits between goes, if its skill has no cooldown.
        public const float FirstPulse = 0.1f;           // Seconds from a zone being laid to its first pulse.
        public const float ShortestZone = 0.5f;         // Seconds. No zone is shorter.
        public const float FlightGrace = 0.25f;         // Seconds a projectile flies past its range.

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
        public const float NoCooldownCost = 0.25f;      // Onto a skill's cost, for each second of cooldown it no longer has.
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

        // What enemies do at the end.
        public const float BurstDelay = 0.9f;           // Seconds from a body falling to its going up. A dodge is half that.
        public const float BurstRadius = 4.5f;
        public const float BurstShare = 0.45f;          // Of an ordinary enemy's blow. They come in numbers.
        public const float RallyReach = 16f;

        // Company.
        public const float AlliesReach = 24f;           // Metres, for what counts allies near you.
        public const float RisenSeconds = 20f;
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
        public const float FlaskFromElite = 4f;

        // Getting about.
        public const float GuideCooldown = 14f;         // Seconds. The guiding wind is the client's own, and nothing is sent.

        // Enemies.
        public const float NightNotice = 1.4f;          // How much further enemies notice a hero under an endless night.
        public const float EliteChance = 0.14f;         // Of each who holds a place.
        public const float WarhostEliteChance = 0.35f;
        public const float WarhostLeaderElites = 2.5f;  // On the chance of a pack's leader being elite.
        public const float MostWarhostLeaderElites = 0.8f;
    }
}

using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Stats;
using Dino_Engine.World;
using Dino_Engine.World.Content;

namespace Dino_Defenders.Server
{
    /// <summary>What a kind of pack is made of: who leads it, who it's mostly made of, and who stands behind them.</summary>
    public sealed record Recipe(EnemyKind[] Leaders, EnemyKind[] Core, EnemyKind[] Support, float SupportShare);

    /// <summary>
    /// Who a pack turns out to be. The world says where one stands and whose it is; this says who's in it. Most of a pack dies quickly and is there to be
    /// plowed through. What makes one worth stopping for is what's mixed in: something behind them shooting, or something big out in front. Leaders are a
    /// different creature from their followers, as that's what a player reads first.
    /// </summary>
    public sealed class PackOrders
    {
        private const float RareLeaderAtFirst = 0.12f;
        private const float RareLeaderPerLevel = 0.012f;
        private const float MostRareLeaders = 0.5f;
        private const float MagicLeaders = 0.5f;        // Of those who lead and aren't rare.
        private const float MagicFollowersAtFirst = 0.03f;
        private const float MagicFollowersPerLevel = 0.004f;
        private const float MostMagicFollowers = 0.14f;
        private const float SharedTraitAtFirst = 0.05f;
        private const float SharedTraitPerLevel = 0.01f;
        private const float MostSharedTraits = 0.3f;
        private const float LedAtFirst = 0.15f;
        private const float LedPerLevel = 0.04f;
        private const float MostLed = 0.5f;

        private readonly Recipe _recipe;
        private readonly bool _led;
        private readonly bool _firstSteps;
        private readonly Rarity _leaderRarity;
        private readonly int _leaderTraits;
        private readonly int _sharedTrait;
        private readonly float _magicFollowers;
        private readonly bool _noneNormal;

        /// <param name="free">Whether this is a pack of its own, rather than some of the people of a place.</param>
        /// <param name="terms">What the land holds. Whoever has asked for trouble isn't taking their first steps.</param>
        public PackOrders(WorldData world, WorldSite site, SpawnRole folk, int level, bool free, WorldRandom random, LandTerms terms)
        {
            float warhost = terms.ValueOf(Omens.Warhost);
            float bred = 1f + terms.ValueOf(Omens.Bloodlines) / 100f;
            _noneNormal = terms.Has(OrdersCatalog.Bred);
            _recipe = Warbands.RecipeFor(world, site, folk, random.Fork("recipe"));
            _firstSteps = world.Plan.Level <= 1 && site.Remoteness < 0.2f && !terms.Any;
            _leaderTraits = Bestiary.NoTraits;
            _sharedTrait = Bestiary.NoTraits;
            if (!free || _firstSteps) return;

            _magicFollowers = MathF.Min((MagicFollowersAtFirst + MagicFollowersPerLevel * level) * bred, MostMagicFollowers * bred);
            bool ledger = terms.Has(OrdersCatalog.Ledger);
            _led = _recipe.Leaders.Length > 0 && (site.Haunt == HauntKind.Camp || ledger || random.Chance(warhost > 0f ? 1f : MathF.Min(LedAtFirst + LedPerLevel * level, MostLed)));
            if (!_led) return;

            float rares = MathF.Min(RareLeaderAtFirst + RareLeaderPerLevel * level, MostRareLeaders) * bred;
            if (warhost > 0f) rares = MathF.Min(rares * (1f + warhost / 100f), Rulebook.MostWarhostLeaderRares);
            if (ledger || random.Chance(rares))
            {
                _leaderRarity = Rarity.Rare;
                if (random.Chance(MathF.Min(SharedTraitAtFirst + SharedTraitPerLevel * level, MostSharedTraits))) _sharedTrait = Bestiary.Trait(random.Int(Bestiary.Traits.Length));
            }
            else if (random.Chance(MagicLeaders) || _noneNormal) _leaderRarity = Rarity.Magic;
            _leaderTraits = Bestiary.Fill(_sharedTrait, _leaderRarity, random.Fork("leader"));

            // A ledger's leaders were bred for more than the rest.
            if (ledger) _leaderTraits = Bestiary.OneMore(_leaderTraits, random.Fork("ledger"));
        }

        /// <summary>Who stands at this place in the pack. The first place is the leader's, if it has one.</summary>
        public (EnemyKind kind, Rarity rarity, int traits) Draw(int place, WorldRandom random)
        {
            if (place == 0 && _led) return (random.Pick(_recipe.Leaders), _leaderRarity, _leaderTraits);

            // Someone new to all this meets one thing at a time.
            bool behind = !_firstSteps && _recipe.Support.Length > 0 && random.Chance(_recipe.SupportShare);
            EnemyKind kind = random.Pick(behind ? _recipe.Support : _recipe.Core);

            // Nothing normal has a trait, so whoever has taken after their leader is more than that.
            if (_sharedTrait != Bestiary.NoTraits) return (kind, Rarity.Magic, _sharedTrait);
            return (kind, _noneNormal || random.Chance(_magicFollowers) ? Rarity.Magic : Rarity.Normal, Bestiary.NoTraits);
        }
    }

    /// <summary>
    /// The packs of the world, by who they are and where they're found. The roads and the strongholds are the Saurian Reich's, and its war-bands are what hold them, with the outlaws who sold the land to it
    /// working what's left. The dead have the Ossuary's bone-beasts among them, the wet places have its failures, and the deep woods have broods that got out of its pens. Read LORE.md.
    /// </summary>
    public static class Warbands
    {
        private const float TurncoatShare = 0.28f;  // Of what holds the roads, outlaws rather than the Reich's own.
        private const float RotShare = 0.45f;       // Of the dead, where the land is wet enough to take what's dumped there.
        private const float FeralShare = 0.4f;      // Of what lives wild, broods that got out of the pens.
        private const float SwarmShare = 0.2f;
        private const float GhostShare = 0.3f;      // Of the dead that aren't rotting, ghosts the Ossuary called up in light.
        private const float ChurchyardGhosts = 0.45f; // Of a churchyard's dead, where the bones it digs are oldest and most often too far gone to stand.
        private const float NestShare = 0.4f;       // Of the swarms, the Institute's weavers and what scavenges after them.
        private const float FlockShare = 0.3f;      // Of what got out into gentle country, runners in a flock.

        // Raptors out in front, soldiers behind them, something big leading, and venom or arrows from the back.
        private static readonly Recipe Reich = new(
            new[] { Bestiary.Ravager, Bestiary.Hornback, Bestiary.Spikeback, Bestiary.WarRipper, Bestiary.Reaper, Bestiary.Clubtail, Bestiary.Wrangler, Bestiary.Wrangler },
            new[] { Bestiary.WarRaptor, Bestiary.WarRaptor, Bestiary.WarRaptor, Bestiary.Legionary },
            new[] { Bestiary.Spitter, Bestiary.Spitter, Bestiary.Marksman },
            0.26f);

        private static readonly Recipe Outlaws = new(
            new[] { Bestiary.Brute, Bestiary.Brute, Bestiary.Outlaw },
            new[] { Bestiary.Cutthroat, Bestiary.Cutthroat, Bestiary.Outlaw },
            new[] { Bestiary.Archer, Bestiary.Archer, Bestiary.Archer, Bestiary.Firebrand, Bestiary.Stormcaller },
            0.28f);

        // The Reich on the roads in the snow, where its cold-blooded beasts die: shagbacks leading, in their coats, and more of its own soldiers.
        private static readonly Recipe ReichInSnow = new(
            new[] { Bestiary.Shagback, Bestiary.Shagback, Bestiary.Wrangler },
            new[] { Bestiary.Legionary, Bestiary.Legionary, Bestiary.WarRaptor },
            new[] { Bestiary.Marksman, Bestiary.Marksman, Bestiary.Spitter },
            0.3f);

        private static readonly Recipe Dead = new(
            new[] { Bestiary.GraveKnight, Bestiary.BoneRavager, Bestiary.BoneHornback, Bestiary.BoneSpikeback, Bestiary.Mourner },
            new[] { Bestiary.Shambler, Bestiary.BoneRaptor, Bestiary.BoneRaptor, Bestiary.BoneWeaver, Bestiary.Restless },
            new[] { Bestiary.BoneArcher, Bestiary.BoneArcher, Bestiary.Mourner },
            0.26f);

        private static readonly Recipe Rotting = new(new[] { Bestiary.RottingHornback, Bestiary.RottingWader, Bestiary.RottingRavager }, new[] { Bestiary.RottingRaptor }, Array.Empty<EnemyKind>(), 0f);

        // What the Ossuary called up in light where the bones it dug were too far gone to stand: ghosts of the old beasts, with the land's own dead stirred up among them.
        private static readonly Recipe Ghosts = new(
            new[] { Bestiary.GhostRavager, Bestiary.GhostRipper },
            new[] { Bestiary.GhostRaptor, Bestiary.GhostRaptor, Bestiary.GhostWeaver, Bestiary.Restless },
            new[] { Bestiary.Mourner, Bestiary.BoneArcher },
            0.22f);

        private static readonly Recipe Feral = new(new[] { Bestiary.WildRavager, Bestiary.Ripper, Bestiary.Domehead }, new[] { Bestiary.Raptor, Bestiary.Raptor, Bestiary.Strider }, new[] { Bestiary.FeralSpitter }, 0.2f);

        // What got out into the deep woods and found it could live there: the Institute's eight-legged hunters, with what scavenges after them.
        private static readonly Recipe Nest = new(new[] { Bestiary.Weaver }, new[] { Bestiary.Weaver, Bestiary.Skitter, Bestiary.Skitter }, Array.Empty<EnemyKind>(), 0f);

        // Flocks of runners in the open country, with a domehead's herd for company.
        private static readonly Recipe Flock = new(new[] { Bestiary.Domehead }, new[] { Bestiary.Strider }, Array.Empty<EnemyKind>(), 0f);

        // What got out into the wet country: a wader leading, with raptors, which go anywhere, and spitters from the reeds.
        private static readonly Recipe Fen = new(new[] { Bestiary.Wader }, new[] { Bestiary.Raptor }, new[] { Bestiary.FeralSpitter }, 0.2f);
        private static readonly Recipe Swarm = new(new[] { Bestiary.Raptor }, new[] { Bestiary.Skitter }, Array.Empty<EnemyKind>(), 0f);

        // What breeds at a nesting ground (PLACES.md): raptors round their mounds with a ripper or a ravager over them, a ravager's own brood at its scrape.
        private static readonly Recipe RaptorNest = new(new[] { Bestiary.Ripper, Bestiary.WildRavager }, new[] { Bestiary.Raptor, Bestiary.Raptor, Bestiary.Skitter }, new[] { Bestiary.FeralSpitter }, 0.18f);
        private static readonly Recipe RavagerNest = new(new[] { Bestiary.WildRavager }, new[] { Bestiary.Raptor, Bestiary.Skitter, Bestiary.Skitter }, Array.Empty<EnemyKind>(), 0f);

        // What got out of a wrecked ship's cages, and what's come to pick over what drowned.
        private static readonly Recipe Escaped = new(new[] { Bestiary.WildRavager, Bestiary.Ripper }, new[] { Bestiary.Raptor, Bestiary.Raptor, Bestiary.Skitter }, new[] { Bestiary.FeralSpitter }, 0.2f);

        private static readonly Recipe Wolves = new(new[] { Bestiary.DireWolf }, new[] { Bestiary.Wolf }, Array.Empty<EnemyKind>(), 0f);
        private static readonly Recipe WinterWolves = new(new[] { Bestiary.DireWolf, Bestiary.Bear, Bestiary.WildShagback }, new[] { Bestiary.Wolf }, Array.Empty<EnemyKind>(), 0f);
        private static readonly Recipe Boars = new(new[] { Bestiary.Bear }, new[] { Bestiary.Boar }, new[] { Bestiary.Wolf }, 0.2f);

        private static readonly Recipe[] Cults =
        {
            new(new[] { Bestiary.Firebrand }, new[] { Bestiary.EmberAcolyte }, new[] { Bestiary.Firebrand }, 0.24f),
            new(new[] { Bestiary.Rimewitch }, new[] { Bestiary.RimeAcolyte }, new[] { Bestiary.Rimewitch }, 0.24f),
            new(new[] { Bestiary.Stormcaller }, new[] { Bestiary.StormAcolyte }, new[] { Bestiary.Stormcaller }, 0.24f),
            new(new[] { Bestiary.Bogseer }, new[] { Bestiary.MireAcolyte }, new[] { Bestiary.Bogseer }, 0.24f),
        };

        /// <summary>Who an arena's handlers let in when the fight goes against their tyrant: its own brood, which for a tyrant the Ossuary raised or left rotting is the Ossuary's.</summary>
        public static Recipe ArenaRecipe(string bossKind) => bossKind switch
        {
            "bone-tyrant" => Dead,
            "rotting-tyrant" => Rotting,
            "ghost-tyrant" => Ghosts,
            _ => Reich,
        };

        /// <summary>Every recipe there is, for anything that wants to look through them.</summary>
        public static IEnumerable<Recipe> All()
        {
            yield return Reich;
            yield return ReichInSnow;
            yield return Outlaws;
            yield return Dead;
            yield return Rotting;
            yield return Ghosts;
            yield return Feral;
            yield return Nest;
            yield return Flock;
            yield return Fen;
            yield return Swarm;
            yield return RaptorNest;
            yield return RavagerNest;
            yield return Escaped;
            yield return Wolves;
            yield return WinterWolves;
            yield return Boars;
            foreach (Recipe cult in Cults) yield return cult;
        }

        public static Recipe RecipeFor(WorldData world, WorldSite site, SpawnRole folk, WorldRandom random)
        {
            BiomeDefinition biome = world.Plan.Biomes[Math.Clamp(site.Biome, 0, world.Plan.Biomes.Count - 1)];
            if (PlaceRecipe(world, site, folk, biome, random) is Recipe place) return place;
            switch (folk)
            {
                case SpawnRole.Undead:
                    // A churchyard's dead are its own and the Ossuary's raising: bones and ghosts. What rots is dumped in the wet, not buried (CHURCHYARD.md).
                    if (site.Kind == SiteKind.Graveyard) return random.Chance(ChurchyardGhosts) ? Ghosts : Dead;
                    if (biome.Moisture >= 0.6f && random.Chance(RotShare)) return Rotting;
                    return random.Chance(GhostShare) ? Ghosts : Dead;
                case SpawnRole.Beast:
                    // Cold-blooded things don't last the winter, so the snow is still the wolves'.
                    if (biome.Temperature <= 0.35f) return random.Chance(FeralShare * 0.4f) ? Feral : WinterWolves;
                    float roll = random.Float();
                    // Waders keep to the wet country, where they lead what got out.
                    if (roll < FeralShare) return biome.Moisture >= 0.75f ? Fen : random.Chance(FlockShare) && biome.Temperature >= 0.45f ? Flock : Feral;
                    if (roll < FeralShare + SwarmShare) return random.Chance(NestShare) ? Nest : Swarm;
                    return random.Chance(biome.Temperature >= 0.55f ? 0.6f : 0.3f) ? Boars : Wolves;
                case SpawnRole.Cultist:
                    // A ring of old stones is dressed for the cult that holds it (PlaceStory.CultOf), so the two have to agree.
                    if (site.Kind == SiteKind.StandingStones) return Cults[(int)PlaceStory.CultOf(biome, site.Story)];
                    return CultOf(biome, random);
                default:
                    if (random.Chance(TurncoatShare)) return Outlaws;
                    return biome.Temperature <= 0.3f ? ReichInSnow : Reich;
            }
        }

        // Who holds a place with a story of its own, which says whose it is (PLACES.md): the Reich keeps its own works with its own people and never a turncoat, the turncoats keep what they
        // took for it, a nest is whatever nests there, and what got out of a wreck's cages hunts its beach. Anything this doesn't settle goes by the land as anywhere else.
        private static Recipe? PlaceRecipe(WorldData world, WorldSite site, SpawnRole folk, BiomeDefinition biome, WorldRandom random)
        {
            bool cold = biome.Temperature <= 0.3f;
            switch (site.Kind)
            {
                case SiteKind.Keep:
                    // A commander's seat is held by its own (SEATS.md): the Reich's by its greycoats and their beasts and never a turncoat, and the Ossuary's by what it made there, the
                    // bones it raised at its dig, the ghosts it called up at the barrow, and at the bile works its failures, with greycoats at the gate.
                    switch (site.Seat?.Kind)
                    {
                        case SeatKind.Boneyard: return Dead;
                        case SeatKind.Barrow: return Ghosts;
                        case SeatKind.Bileworks when folk == SpawnRole.Undead: return Rotting;
                        default: return folk is SpawnRole.Raider or SpawnRole.Guard ? (cold ? ReichInSnow : Reich) : null;
                    }
                case SiteKind.Prison:
                case SiteKind.Farm:
                case SiteKind.Quarry:
                case SiteKind.LumberCamp:
                    return folk is SpawnRole.Raider or SpawnRole.Guard ? (cold ? ReichInSnow : Reich) : null;
                case SiteKind.Laboratory:
                    // The Institute's failures, kept in its pens until the Ossuary takes them.
                    if (folk == SpawnRole.Undead) return Rotting;
                    return folk is SpawnRole.Raider or SpawnRole.Guard ? (cold ? ReichInSnow : Reich) : null;
                case SiteKind.Excavation:
                    if (folk == SpawnRole.Undead) return random.Chance(ChurchyardGhosts) ? Ghosts : Dead;
                    return folk is SpawnRole.Raider or SpawnRole.Guard ? (cold ? ReichInSnow : Reich) : null;
                case SiteKind.Tradepost:
                    return folk == SpawnRole.Raider ? Outlaws : null;
                case SiteKind.Shipwreck:
                    if (folk == SpawnRole.Beast) return cold ? WinterWolves : Escaped;
                    return folk == SpawnRole.Raider ? Outlaws : null;
                case SiteKind.Nest when folk == SpawnRole.Beast:
                    return PlaceStory.NestOf(world, site) switch
                    {
                        NestKind.Weaver => Nest,
                        NestKind.Wader => Fen,
                        NestKind.Ravager => RavagerNest,
                        _ => RaptorNest,
                    };
                case SiteKind.SnowCastle when folk == SpawnRole.Beast:
                    return WinterWolves;
                default:
                    return null;
            }
        }

        // The land's powers are what the land is. Anywhere gentle could have anyone.
        private static Recipe CultOf(BiomeDefinition biome, WorldRandom random)
        {
            if (biome.Temperature <= 0.3f) return Cults[1];
            if (biome.Moisture >= 0.75f) return Cults[3];
            if (biome.Altitude >= 0.5f) return Cults[2];
            if (biome.Temperature >= 0.7f && biome.Moisture < 0.4f) return Cults[0];
            return random.Pick(Cults);
        }
    }
}

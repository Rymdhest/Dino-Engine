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

        /// <param name="free">Whether this is a pack of its own, rather than some of the people of a place.</param>
        /// <param name="offering">What's been paid on this land. Whoever has paid for trouble isn't taking their first steps.</param>
        public PackOrders(WorldData world, WorldSite site, SpawnRole folk, int level, bool free, WorldRandom random, Offering offering)
        {
            bool warhost = offering.Has(Omens.Warhost);
            _recipe = Warbands.RecipeFor(world, site, folk, random.Fork("recipe"));
            _firstSteps = world.Plan.Level <= 1 && site.Remoteness < 0.2f && !offering.AtStake;
            _leaderTraits = Bestiary.NoTraits;
            _sharedTrait = Bestiary.NoTraits;
            if (!free || _firstSteps) return;

            _magicFollowers = MathF.Min(MagicFollowersAtFirst + MagicFollowersPerLevel * level, MostMagicFollowers);
            _led = _recipe.Leaders.Length > 0 && (site.Haunt == HauntKind.Camp || random.Chance(warhost ? 1f : MathF.Min(LedAtFirst + LedPerLevel * level, MostLed)));
            if (!_led) return;

            float rares = MathF.Min(RareLeaderAtFirst + RareLeaderPerLevel * level, MostRareLeaders);
            if (random.Chance(warhost ? MathF.Min(rares * Rulebook.WarhostLeaderRares, Rulebook.MostWarhostLeaderRares) : rares))
            {
                _leaderRarity = Rarity.Rare;
                if (random.Chance(MathF.Min(SharedTraitAtFirst + SharedTraitPerLevel * level, MostSharedTraits))) _sharedTrait = Bestiary.Trait(random.Int(Bestiary.Traits.Length));
            }
            else if (random.Chance(MagicLeaders)) _leaderRarity = Rarity.Magic;
            _leaderTraits = Bestiary.Fill(_sharedTrait, _leaderRarity, random.Fork("leader"));
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
            return (kind, random.Chance(_magicFollowers) ? Rarity.Magic : Rarity.Normal, Bestiary.NoTraits);
        }
    }

    /// <summary>The packs of the world, by who they are and where they're found.</summary>
    public static class Warbands
    {
        private static readonly Recipe Outlaws = new(
            new[] { Bestiary.Brute, Bestiary.Brute, Bestiary.Outlaw },
            new[] { Bestiary.Cutthroat, Bestiary.Cutthroat, Bestiary.Outlaw },
            new[] { Bestiary.Archer, Bestiary.Archer, Bestiary.Archer, Bestiary.Firebrand, Bestiary.Stormcaller },
            0.28f);

        private static readonly Recipe Dead = new(
            new[] { Bestiary.GraveKnight, Bestiary.GraveKnight, Bestiary.Mourner },
            new[] { Bestiary.Shambler, Bestiary.Shambler, Bestiary.Restless },
            new[] { Bestiary.BoneArcher, Bestiary.BoneArcher, Bestiary.Mourner },
            0.26f);

        private static readonly Recipe Wolves = new(new[] { Bestiary.DireWolf }, new[] { Bestiary.Wolf }, Array.Empty<EnemyKind>(), 0f);
        private static readonly Recipe WinterWolves = new(new[] { Bestiary.DireWolf, Bestiary.Bear }, new[] { Bestiary.Wolf }, Array.Empty<EnemyKind>(), 0f);
        private static readonly Recipe Boars = new(new[] { Bestiary.Bear }, new[] { Bestiary.Boar }, new[] { Bestiary.Wolf }, 0.2f);

        private static readonly Recipe[] Cults =
        {
            new(new[] { Bestiary.Firebrand }, new[] { Bestiary.EmberAcolyte }, new[] { Bestiary.Firebrand }, 0.24f),
            new(new[] { Bestiary.Rimewitch }, new[] { Bestiary.RimeAcolyte }, new[] { Bestiary.Rimewitch }, 0.24f),
            new(new[] { Bestiary.Stormcaller }, new[] { Bestiary.StormAcolyte }, new[] { Bestiary.Stormcaller }, 0.24f),
            new(new[] { Bestiary.Bogseer }, new[] { Bestiary.MireAcolyte }, new[] { Bestiary.Bogseer }, 0.24f),
        };

        /// <summary>Every recipe there is, for anything that wants to look through them.</summary>
        public static IEnumerable<Recipe> All()
        {
            yield return Outlaws;
            yield return Dead;
            yield return Wolves;
            yield return WinterWolves;
            yield return Boars;
            foreach (Recipe cult in Cults) yield return cult;
        }

        public static Recipe RecipeFor(WorldData world, WorldSite site, SpawnRole folk, WorldRandom random)
        {
            BiomeDefinition biome = world.Plan.Biomes[Math.Clamp(site.Biome, 0, world.Plan.Biomes.Count - 1)];
            switch (folk)
            {
                case SpawnRole.Undead:
                    return Dead;
                case SpawnRole.Beast:
                    if (biome.Temperature <= 0.35f) return WinterWolves;
                    return random.Chance(biome.Temperature >= 0.55f ? 0.6f : 0.3f) ? Boars : Wolves;
                case SpawnRole.Cultist:
                    return CultOf(biome, random);
                default:
                    return Outlaws;
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

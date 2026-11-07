using Dino_Defenders.Gameplay;
using Dino_Engine.World;

namespace Dino_Defenders.Progression
{
    /// <summary>
    /// How good a find is, which is what everything that shows one goes by: its colour, its sound, whether it has a beam. It's worked out from the item
    /// itself and never kept, so it can't disagree with what the item is, and an item carried over from an older build is graded like any other.
    /// </summary>
    public enum LootGrade : byte
    {
        Plain,
        Magic,
        Rare,
        Masterwork,     // A rare with a line more than any ordinary rare can have.
        Legendary,
    }

    /// <summary>What a find came out of. Each is a step up from the last, and what's hardest to get at pays best.</summary>
    public enum LootTier
    {
        Fodder,     // Most of a pack, there to be cut down.
        Soldier,    // An ordinary enemy.
        Champion,   // What leads a pack: something large that takes some killing.
        Elite,
        Boss,
        Cache,      // A chest in a camp.
        Chest,      // A chest at a place on the map.
        Hoard,      // A chest in a keep.
    }

    /// <param name="Finds">How many items come out. The whole part always does, and what's over it is the chance of one more.</param>
    /// <param name="Magic">Chance of each grade for each find, before anything has changed the odds. What's left over is plain.</param>
    /// <param name="Least">Nothing that comes out is worse than this.</param>
    /// <param name="Promised">The first find is at least this, whatever the dice say. It's why a boss is always worth it.</param>
    /// <param name="Quality">From 0 to 1, how well each item rolls, as <see cref="ItemGenerator.Forge"/> means it.</param>
    /// <param name="ExtraLevels">How far above the source's own level its items are, which is what decides the tiers they can reach.</param>
    /// <param name="GoldPiles">How many piles the gold comes out in.</param>
    public sealed record LootTable(
        float Finds, float Magic, float Rare, float Masterwork, float Legendary,
        LootGrade Least, LootGrade Promised, float Quality, int ExtraLevels,
        float GoldChance, float GoldScale, int GoldPiles);

    /// <summary>What's been paid for at a waygate, on top of what any source gives. Every share here is a multiplier, so <see cref="None"/> leaves a roll exactly as it was.</summary>
    /// <param name="Quantity">What the number of finds is multiplied by.</param>
    /// <param name="Rarity">What the chance of anything better than plain is multiplied by.</param>
    /// <param name="Legend">What the chance of a masterwork or a legendary is multiplied by, on top of <paramref name="Rarity"/>.</param>
    /// <param name="Quality">Added to a table's quality.</param>
    /// <param name="ExtraLevels">Added to how far above a source's level its items are.</param>
    /// <param name="Least">Nothing is worse than this, whatever the table says.</param>
    /// <param name="Gold">What gold is multiplied by.</param>
    /// <param name="Experience">What experience is multiplied by.</param>
    public readonly record struct LootBoon(float Quantity, float Rarity, float Legend, float Quality, int ExtraLevels, LootGrade Least, float Gold, float Experience)
    {
        public static readonly LootBoon None = new(1f, 1f, 1f, 0f, 0, LootGrade.Plain, 1f, 1f);
    }

    /// <summary>
    /// What everything in the world is worth killing or opening for. Two things carry all of the risk and reward: <see cref="LootTables.TierOf"/> reads how hard
    /// something was to kill from the health it really had, and <see cref="Loot.Daring"/> pays for taking on what's above you and stops paying for what's beneath you.
    /// </summary>
    public static class LootTables
    {
        private const float SoldierThreat = 0.7f;
        private const float ChampionThreat = 1.7f;

        // Worked out against real play, which is nearly all fodder at thirty kills a minute: something good every minute or two, and a rare early in a new hero's life, as that's when a
        // player decides what kind of game this is. `Dino NetLab pace` says how it comes out, and `Dino BuildLab loot` what each of these gives.
        private static readonly LootTable[] Tables =
        {
            //  Finds  Magic  Rare   Master  Legend   Least             Promised          Quality  Levels  Gold  Scale  Piles
            new(0.11f, 0.32f, 0.09f, 0.003f, 0.001f, LootGrade.Plain, LootGrade.Plain, 0f, 0, 0.16f, 1f, 1),
            new(0.18f, 0.38f, 0.12f, 0.006f, 0.002f, LootGrade.Plain, LootGrade.Plain, 0.05f, 1, 0.18f, 1.2f, 1),
            new(0.40f, 0.50f, 0.20f, 0.025f, 0.01f, LootGrade.Plain, LootGrade.Magic, 0.25f, 1, 0.5f, 1.6f, 1),
            new(1.40f, 0.55f, 0.35f, 0.06f, 0.03f, LootGrade.Magic, LootGrade.Magic, 0.45f, 2, 1f, 3f, 2),
            new(5.00f, 0f, 0.45f, 0.30f, 0.25f, LootGrade.Rare, LootGrade.Legendary, 1f, 4, 1f, 10f, 6),
            new(1.50f, 0.60f, 0.20f, 0.02f, 0.01f, LootGrade.Magic, LootGrade.Magic, 0.15f, 1, 1f, 3f, 1),
            new(3.00f, 0.50f, 0.40f, 0.06f, 0.03f, LootGrade.Magic, LootGrade.Rare, 0.4f, 2, 1f, 4f, 2),
            new(5.00f, 0f, 0.50f, 0.20f, 0.10f, LootGrade.Rare, LootGrade.Masterwork, 0.7f, 3, 1f, 6f, 3),
        };

        public static LootTable Of(LootTier tier) => Tables[(int)tier];

        /// <summary>The next chest up, for a land whose chests are worth more than they look.</summary>
        public static LootTier Better(LootTier chest) => chest switch
        {
            LootTier.Cache => LootTier.Chest,
            _ => LootTier.Hoard,
        };

        /// <param name="threat">Its health, as a share of an ordinary enemy's at its level.</param>
        public static LootTier TierOf(EnemyRank rank, float threat) => rank switch
        {
            EnemyRank.Boss => LootTier.Boss,
            EnemyRank.Elite => LootTier.Elite,
            _ => threat >= ChampionThreat ? LootTier.Champion : threat >= SoldierThreat ? LootTier.Soldier : LootTier.Fodder,
        };
    }

    /// <summary>
    /// Keeps a dry spell from going on. Dice alone leave a player walking through a hundred enemies for nothing now and then, and that's what's remembered, so what hasn't
    /// come for a while comes sooner: the odds climb with how long it's been, and go back to where they were when it comes. A world has one of these, shared by everyone in it.
    /// </summary>
    public sealed class LootLuck
    {
        private const float KillsBeforeHelp = 20f;
        private const float FindsBeforeHelp = 4f;
        private const float FindsBeforeLegend = 45f;

        private int _kills;
        private int _sinceGood;
        private int _sinceLegend;

        /// <summary>What a source's chance of dropping anything is multiplied by.</summary>
        public float Finds => Climb(_kills, KillsBeforeHelp);

        /// <summary>What the chance of a rare or better is multiplied by.</summary>
        public float Good => Climb(_sinceGood, FindsBeforeHelp);

        /// <summary>What the chance of a legendary is multiplied by, on top of <see cref="Good"/>.</summary>
        public float Legend => Climb(_sinceLegend, FindsBeforeLegend);

        private static float Climb(int waited, float patience)
        {
            float share = waited / patience;
            return 1f + share * share;
        }

        public void Missed() => _kills++;

        public void Found(LootGrade grade)
        {
            _kills = 0;
            _sinceGood = grade >= LootGrade.Rare ? 0 : _sinceGood + 1;
            _sinceLegend = grade == LootGrade.Legendary ? 0 : _sinceLegend + 1;
        }
    }

    /// <summary>What a find is drawn as, until its own model is ready: a name the server gives and <see cref="Client.CharacterFactory"/> knows. What it really looks like is in <see cref="Gameplay.PickupComponent"/>.</summary>
    public static class LootModels
    {
        public const string Find = "find";
        public const string Gold = "gold";
    }

    public static class Loot
    {
        /// <summary>How many lines make a rare a masterwork. Nothing else can roll this many.</summary>
        public const int MasterworkLines = 6;

        /// <summary>How many places make a rare book or page a masterwork, which is what lines are to anything worn.</summary>
        public const int MasterworkPages = 5;
        public const int MasterworkStickers = 4;

        /// <summary>Of what comes out, how much is something for a book, as what a book can do is half of what a build is.</summary>
        public const float FolioShare = 0.3f;

        /// <summary>From where a hero can pick something up. The server allows a little more than a client walks to, as it sees the hero a moment late.</summary>
        public const float Reach = 3f;
        public const float ServerReach = Reach + 2.5f;

        /// <summary>Seconds gold is in the air, before anyone can have it.</summary>
        public const float GoldFlight = 0.5f;

        private const int MostRedraws = 8;
        private const float DaringStep = 0.07f;
        private const float LeastDaring = 0.4f;
        private const float MostDaring = 2f;
        private const float MostLegendary = 0.5f;   // Of one find. Past this a boss on a paid-for land would drop nothing else, and a legendary is only worth what the rest of the pile makes it.
        private const float MostMasterwork = 0.4f;
        private const float SureQuantity = 0.5f;    // A boss or a chest is a pile already. This much of what's been paid for goes on it, so that the top of the climb is a heap and not a carpet.

        public static LootGrade GradeOf(Item item) => item.Rarity switch
        {
            Rarity.Legendary => LootGrade.Legendary,
            Rarity.Rare => item.Lines.Count >= MasterworkLines || item.IsBook && item.Places >= MasterworkPages || item.IsPage && item.Places >= MasterworkStickers ? LootGrade.Masterwork : LootGrade.Rare,
            Rarity.Magic => LootGrade.Magic,
            _ => LootGrade.Plain,
        };

        /// <summary>What it's worth going after something above you, and how little there is in what's beneath you. Goes by the strongest hero there, so bringing a friend can't get round it.</summary>
        public static float Daring(int level, int heroLevel) => Math.Clamp(1f + DaringStep * (level - heroLevel), LeastDaring, MostDaring);

        /// <summary>What comes out of a source.</summary>
        /// <param name="level">The level of what's being looted.</param>
        /// <param name="heroLevel">The level of the strongest hero there.</param>
        /// <param name="find">The party's item rarity, as a share: 0.5 is half as likely again.</param>
        public static List<Item> Roll(LootTier tier, int level, int heroLevel, float find, LootLuck luck, WorldRandom random) => Roll(tier, level, heroLevel, find, luck, random, LootBoon.None);

        /// <param name="boon">What's been paid for on this land.</param>
        public static List<Item> Roll(LootTier tier, int level, int heroLevel, float find, LootLuck luck, WorldRandom random, in LootBoon boon)
        {
            LootTable table = LootTables.Of(tier);
            float daring = Daring(level, heroLevel);
            float fortune = (1f + find) * daring * boon.Rarity;

            // A small chance is what luck and daring can move. Whatever is sure to drop is sure, and they go into what it is instead.
            float finds = table.Finds < 1f ? table.Finds * luck.Finds * daring * boon.Quantity : table.Finds * (1f + (boon.Quantity - 1f) * SureQuantity);
            int count = (int)finds + (random.Chance(finds - MathF.Floor(finds)) ? 1 : 0);

            var items = new List<Item>(count);
            for (int i = 0; i < count; i++)
            {
                LootGrade grade = Draw(table, fortune, luck, boon.Legend, random);
                if (grade < table.Least) grade = table.Least;
                if (grade < boon.Least) grade = boon.Least;
                if (i == 0 && grade < table.Promised) grade = table.Promised;

                Item item = Fresh(items, level + table.ExtraLevels + boon.ExtraLevels, random.Fork(i), grade, MathF.Min(table.Quality + boon.Quality, 1f));
                luck.Found(GradeOf(item));
                items.Add(item);
            }

            if (count == 0) luck.Missed();
            return items;
        }

        // A unique is one of a kind, and the same one twice out of one chest reads as a mistake. Where there's nothing else it can be, it can be that.
        private static Item Fresh(List<Item> others, int level, WorldRandom random, LootGrade grade, float quality)
        {
            WorldRandom folio = random.Fork("folio");
            if (folio.Chance(FolioShare)) return ItemGenerator.Folio(level, folio, grade);

            Item item = ItemGenerator.Forge(level, random, grade, quality);
            for (int attempt = 0; attempt < MostRedraws && item.Unique != null && others.Any(other => other.Unique == item.Unique); attempt++)
            {
                item = ItemGenerator.Forge(level, random, grade, quality);
            }
            return item;
        }

        private static LootGrade Draw(LootTable table, float fortune, LootLuck luck, float legend, WorldRandom random)
        {
            float good = fortune * luck.Good;
            float roll = random.Float();
            if ((roll -= MathF.Min(table.Legendary * good * luck.Legend * legend, MostLegendary)) < 0f) return LootGrade.Legendary;
            if ((roll -= MathF.Min(table.Masterwork * good * legend, MostMasterwork)) < 0f) return LootGrade.Masterwork;
            if ((roll -= table.Rare * good) < 0f) return LootGrade.Rare;
            return roll - table.Magic * fortune < 0f ? LootGrade.Magic : LootGrade.Plain;
        }

        /// <summary>How much gold a source gives, in all, or nothing if it gives none.</summary>
        /// <param name="share">What it's multiplied by, on a land where that's been paid for.</param>
        public static int RollGold(LootTier tier, int level, float goldFind, WorldRandom random, float share = 1f)
        {
            LootTable table = LootTables.Of(tier);
            return random.Chance(table.GoldChance) ? (int)(random.Range(3f, 9f) * level * table.GoldScale * (1f + goldFind) * share) : 0;
        }
    }
}

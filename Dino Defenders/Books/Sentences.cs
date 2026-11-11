using Dino_Defenders.Progression;
using Dino_Defenders.Stats;
using Dino_Engine.World;

namespace Dino_Defenders.Books
{
    /// <summary>One thing a sentence does to the page it's on. Saved by name, and which way it goes depends on which side of the sentence it's on.</summary>
    public enum PageEffect : byte
    {
        None,
        Potency,
        Speed,
        Area,
        Range,
        Cost,
        UseTime,
        Projectiles,
        Pierce,
        Chains,
        Stops,
        Rides,
        Rest,

        // What only a Unique sentence does: where a page starts or goes, and what counts as reaching anything.
        Grounded,
        FromCursor,
        FromNearest,
        FromRandom,
        ToNearest,
        ToYou,
        ToAnywhere,
        Falls,

        // Anything new goes on the end, as these are sent by number.
        Duration,       // How long a lingering area lasts.
    }

    /// <summary>What's on a sentence: one thing that's good for a page and one that's bad for it, each by how much it rolled. See BOOKS.md.</summary>
    public sealed class Sentence
    {
        public PageEffect Good { get; set; }
        public float GoodBy { get; set; }
        public PageEffect Bad { get; set; }
        public float BadBy { get; set; }

        public Sentence Copy() => (Sentence)MemberwiseClone();
    }

    /// <summary>
    /// How sentences are rolled and what they come to. Nothing about one is decided by hand: a roll picks any good thing and any bad thing that make sense together, and how far each goes, so no two need be alike. A rarer
    /// sentence goes further on its good side and less far on its bad. A Unique one has something on its good side that nothing else can have, which is where a page starts or goes, and a bad side rolled as a rare's would
    /// be. What's <see cref="Written"/> is for books that are already written, which have to come out the same every time.
    /// </summary>
    public static class Sentences
    {
        private const float RaritySteps = 3f;
        private const float BestDrawback = 0.75f;       // Of the way from a drawback's worst to its least, for the best there is. Nothing gets away with nothing.

        /// <param name="Fits">Only means anything on one kind of page, if it isn't null.</param>
        /// <param name="Good">From how little to how much it can do for a page, or nothing if it can't. Something that has no amount has both at nothing, and <paramref name="Flag"/>.</param>
        /// <param name="Bad">The same, for what it can do against one.</param>
        /// <param name="Whole">Only goes in whole numbers.</param>
        /// <param name="Unique">Only a Unique sentence has it, and it's what makes one.</param>
        private readonly record struct Line(PageEffect Effect, PageKind? Fits, (float Least, float Most) Good, (float Least, float Most) Bad, bool Whole = false, bool Flag = false, bool Unique = false);

        private static readonly (float, float) Never = (0f, 0f);

        private static readonly Line[] Lines =
        {
            new(PageEffect.Potency, null, (10f, 70f), (10f, 45f)),
            new(PageEffect.Cost, null, (10f, 45f), (1f, 8f), Whole: false),
            new(PageEffect.UseTime, null, (8f, 35f), (10f, 60f)),
            new(PageEffect.Rest, null, Never, (0.5f, 3f)),
            new(PageEffect.Speed, PageKind.Projectile, (15f, 100f), (15f, 50f)),
            new(PageEffect.Range, PageKind.Projectile, (15f, 100f), (15f, 50f)),
            new(PageEffect.Projectiles, PageKind.Projectile, (1f, 4f), Never, Whole: true),
            new(PageEffect.Pierce, PageKind.Projectile, (1f, 4f), Never, Whole: true),
            new(PageEffect.Chains, PageKind.Projectile, (1f, 3f), Never, Whole: true),
            new(PageEffect.Rides, PageKind.Projectile, Never, Never, Flag: true),
            new(PageEffect.Area, PageKind.Area, (15f, 100f), (15f, 50f)),
            new(PageEffect.Duration, PageKind.Area, (20f, 120f), (15f, 50f)),

            new(PageEffect.Grounded, PageKind.Projectile, Never, Never, Flag: true, Unique: true),
            new(PageEffect.FromCursor, null, Never, Never, Flag: true, Unique: true),
            new(PageEffect.FromNearest, null, Never, Never, Flag: true, Unique: true),
            new(PageEffect.FromRandom, null, Never, Never, Flag: true, Unique: true),
            new(PageEffect.ToNearest, PageKind.Projectile, Never, Never, Flag: true, Unique: true),
            new(PageEffect.ToYou, PageKind.Projectile, Never, Never, Flag: true, Unique: true),
            new(PageEffect.ToAnywhere, PageKind.Projectile, Never, Never, Flag: true, Unique: true),
            new(PageEffect.Falls, PageKind.Projectile, Never, Never, Flag: true, Unique: true),
        };

        // What's left to chance pays for itself, as the phrases that once said so did.
        private static readonly Weights Wild = new Weights(Cost: -50f, Potency: 30f);
        private static readonly Weights Chance = new Weights(Potency: 10f);

        private static readonly Dictionary<string, Sentence> ByName = new()
        {
            ["two-at-once"] = Fixed(PageEffect.Projectiles, 1f, PageEffect.Potency, 30f),
            ["three-at-once"] = Fixed(PageEffect.Projectiles, 2f, PageEffect.Potency, 45f),
            ["five-at-once"] = Fixed(PageEffect.Projectiles, 4f, PageEffect.Potency, 60f),
            ["pierce-one"] = Fixed(PageEffect.Pierce, 1f, PageEffect.Potency, 10f),
            ["pierce-three"] = Fixed(PageEffect.Pierce, 3f, PageEffect.Potency, 25f),
            ["chain-one"] = Fixed(PageEffect.Chains, 1f, PageEffect.Potency, 15f),
            ["chain-three"] = Fixed(PageEffect.Chains, 3f, PageEffect.Potency, 30f),
            ["swift"] = Fixed(PageEffect.Speed, 60f, PageEffect.Potency, 10f),
            ["slow"] = Fixed(PageEffect.Potency, 15f, PageEffect.Speed, 40f),
            ["far"] = Fixed(PageEffect.Range, 50f, PageEffect.Cost, 1f),
            ["short"] = Fixed(PageEffect.Cost, 25f, PageEffect.Range, 50f),
            ["rides"] = Fixed(PageEffect.Rides, 0f, PageEffect.Cost, 4f),
            ["broad"] = Fixed(PageEffect.Area, 60f, PageEffect.Cost, 2f),
            ["tight"] = Fixed(PageEffect.Potency, 20f, PageEffect.Area, 40f),
            ["hasty"] = Fixed(PageEffect.UseTime, 30f, PageEffect.Potency, 20f),
            ["deliberate"] = Fixed(PageEffect.Potency, 40f, PageEffect.UseTime, 60f),
            ["rest"] = Fixed(PageEffect.Potency, 60f, PageEffect.Rest, Rulebook.RestShort),
            ["rest-long"] = Fixed(PageEffect.Potency, 130f, PageEffect.Rest, Rulebook.RestLong),

            // What written books do that only a Unique sentence can, which is what those are named after.
            ["grounded"] = Fixed(PageEffect.Grounded, 0f, PageEffect.None, 0f),
            ["from-cursor"] = Fixed(PageEffect.FromCursor, 0f, PageEffect.None, 0f),
            ["from-nearest"] = Fixed(PageEffect.FromNearest, 0f, PageEffect.None, 0f),
            ["from-random"] = Fixed(PageEffect.FromRandom, 0f, PageEffect.None, 0f),
            ["towards-nearest"] = Fixed(PageEffect.ToNearest, 0f, PageEffect.None, 0f),
            ["towards-you"] = Fixed(PageEffect.ToYou, 0f, PageEffect.None, 0f),
            ["anywhere"] = Fixed(PageEffect.ToAnywhere, 0f, PageEffect.None, 0f),
            ["falls"] = Fixed(PageEffect.Falls, 0f, PageEffect.None, 0f),
        };

        private static Sentence Fixed(PageEffect good, float goodBy, PageEffect bad, float badBy) => new Sentence { Good = good, GoodBy = goodBy, Bad = bad, BadBy = badBy };

        /// <summary>Every sentence that books already written can use, by name. Clauses in saves from before sentences go by these names too.</summary>
        public static IEnumerable<string> Names => ByName.Keys;

        /// <summary>A sentence that always says the same thing, or nothing if there's no such name.</summary>
        public static Sentence? Written(string name) => ByName.GetValueOrDefault(name)?.Copy();

        /// <summary>Every effect a sentence can be rolled with on its good side, Unique ones among them.</summary>
        public static IEnumerable<PageEffect> Goods => Lines.Where(CanHelp).Select(line => line.Effect);

        /// <summary>Every effect only a Unique sentence has.</summary>
        public static IEnumerable<PageEffect> Uniques => Lines.Where(line => line.Unique).Select(line => line.Effect);

        /// <summary>Whether only a Unique sentence can have this.</summary>
        public static bool IsUnique(PageEffect effect) => LineOf(effect).Unique;

        /// <summary>Every effect a sentence can be rolled with on its bad side.</summary>
        public static IEnumerable<PageEffect> Bads => Lines.Where(CanHurt).Select(line => line.Effect);

        private static bool CanHelp(Line line) => line.Good.Most > 0f || line.Flag;

        private static bool CanHurt(Line line) => line.Bad.Most > 0f;

        private static Line LineOf(PageEffect effect) => Array.Find(Lines, line => line.Effect == effect);

        /// <summary>Which kind of page it goes on, if it's only one. It can't be both, as a sentence that only half works isn't one anybody could use.</summary>
        public static PageKind? Fits(Sentence sentence) => LineOf(sentence.Good).Fits ?? LineOf(sentence.Bad).Fits;

        /// <summary>Whether it can go on a page of this kind.</summary>
        public static bool GoesOn(Sentence sentence, PageKind kind) => kind == PageKind.Summon ? Summons(sentence.Good) && Summons(sentence.Bad) : Fits(sentence) is not PageKind only || only == kind;

        // What means anything on a page that only raises Minions beside you: what it costs, how long it takes and how long it rests. Its Potency and where it starts mean nothing.
        private static bool Summons(PageEffect effect) => effect is PageEffect.None or PageEffect.Cost or PageEffect.UseTime or PageEffect.Rest;

        /// <summary>Rolls one.</summary>
        /// <param name="rarity">How good it is, which is how far its good side goes and how little its bad side does. A Unique one has a good side nothing else can have, and a bad side as a rare's.</param>
        /// <param name="only">For a Unique one, which of the things only those have it's to have, for what has to show every one. It's drawn for all the same, so nothing else about it changes.</param>
        public static Sentence Roll(Rarity rarity, WorldRandom random, PageEffect only = PageEffect.None)
        {
            bool unique = rarity == Rarity.Unique;
            if (unique) rarity = Rarity.Rare;

            // A page for projectiles, for areas, or either: whichever it is, everything on it has to mean something there. What only a Unique one has decides that for itself.
            int kind = random.Int(3);
            PageKind? fits = kind == 0 ? null : kind == 1 ? PageKind.Projectile : PageKind.Area;
            Line chosen = default;
            if (unique)
            {
                Line[] uniques = Array.FindAll(Lines, line => line.Unique);
                chosen = uniques[random.Int(uniques.Length)];
                if (Array.Exists(uniques, line => line.Effect == only)) chosen = LineOf(only);
                fits = chosen.Fits;
            }
            bool Belongs(Line line) => line.Fits is null || line.Fits == fits;

            Line[] goods = Array.FindAll(Lines, line => CanHelp(line) && !line.Unique && Belongs(line));
            Line good = unique ? chosen : goods[random.Int(goods.Length)];
            Line[] bads = Array.FindAll(Lines, line => CanHurt(line) && Belongs(line) && line.Effect != good.Effect);
            Line bad = bads[random.Int(bads.Length)];

            float strength = ((int)rarity + random.Float()) / RaritySteps;
            float mercy = ((int)rarity + random.Float()) / RaritySteps * BestDrawback;
            return new Sentence
            {
                Good = good.Effect,
                GoodBy = good.Flag ? 0f : Amount(good.Good.Least, good.Good.Most, strength, good.Effect != PageEffect.Rest),
                Bad = bad.Effect,
                BadBy = Amount(bad.Bad.Least, bad.Bad.Most, 1f - mercy, bad.Effect != PageEffect.Rest),
            };
        }

        private static float Amount(float least, float most, float share, bool whole)
        {
            float value = least + (most - least) * share;
            return whole ? MathF.Round(value) : MathF.Round(value * 2f) * 0.5f;
        }

        /// <summary>What one does to a page's numbers, in the same terms as a phrase. Good and bad each go the way that word means.</summary>
        public static Weights WeightsOf(Sentence sentence) => Add(Add(default, sentence.Good, sentence.GoodBy, 1f), sentence.Bad, sentence.BadBy, -1f);

        private static Weights Add(Weights weights, PageEffect effect, float by, float sign) => effect switch
        {
            PageEffect.ToAnywhere => Wild,
            PageEffect.FromRandom => Chance,
            PageEffect.Potency => weights with { Potency = by * sign },
            PageEffect.Speed => weights with { Speed = by * sign },
            PageEffect.Area => weights with { Area = by * sign },
            PageEffect.Range => weights with { Range = by * sign },
            PageEffect.UseTime => weights with { UseTime = -by * sign },
            PageEffect.Duration => weights with { Duration = by * sign },

            // What it can take off is a share and what it can put on is flat, as a page that's free has to feel it too.
            PageEffect.Cost => sign > 0f ? weights with { Cost = -by } : weights with { Added = by },
            _ => weights,
        };

        /// <summary>What something only a Unique sentence has does to a page's numbers by itself, which is something only for what's left to chance.</summary>
        public static Weights Weighting(PageEffect effect) => effect switch
        {
            PageEffect.ToAnywhere => Wild,
            PageEffect.FromRandom => Chance,
            _ => default,
        };

        /// <summary>Whether what's on it leaves anything to chance, which a build can make something of.</summary>
        public static bool IsWild(Sentence sentence) => sentence.Good is PageEffect.ToAnywhere or PageEffect.FromRandom;

        /// <summary>A value that's arrived from outside brought back to what could have been rolled, for a save that's been edited.</summary>
        /// <param name="unique">Whether it's a Unique sentence, which is the only kind that can have what only those have, and has to.</param>
        public static void Fix(Sentence sentence, bool unique = false)
        {
            if (!Enum.IsDefined(sentence.Good) || IsUnique(sentence.Good) != unique) sentence.Good = unique ? PageEffect.Grounded : PageEffect.None;
            if (!Enum.IsDefined(sentence.Bad) || IsUnique(sentence.Bad)) sentence.Bad = PageEffect.None;
            sentence.GoodBy = Said(sentence.Good, Math.Clamp(float.IsFinite(sentence.GoodBy) ? sentence.GoodBy : 0f, 0f, Rulebook.MostSentenceGood));
            sentence.BadBy = Said(sentence.Bad, Math.Clamp(float.IsFinite(sentence.BadBy) ? sentence.BadBy : 0f, 0f, Rulebook.MostSentenceBad));
        }

        // What a sentence says of anything is a whole number, as anything else is, but for a rest, which is seconds and goes by halves.
        private static float Said(PageEffect effect, float value) => effect == PageEffect.Rest ? MathF.Round(value * 2f) * 0.5f : MathF.Round(value);
    }
}

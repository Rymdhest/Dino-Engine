using Dino_Defenders.Progression;
using Dino_Defenders.Stats;
using Dino_Engine.World;

namespace Dino_Defenders.Books
{
    /// <summary>One thing a sticker does to the page it's on. Saved by name, and which way it goes depends on which side of the sticker it's on.</summary>
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
    }

    /// <summary>What's on a sticker: one thing that's good for a page and one that's bad for it, each by how much it rolled. See BOOKS.md.</summary>
    public sealed class Sticker
    {
        public PageEffect Good { get; set; }
        public float GoodBy { get; set; }
        public PageEffect Bad { get; set; }
        public float BadBy { get; set; }

        public Sticker Copy() => (Sticker)MemberwiseClone();
    }

    /// <summary>
    /// How stickers are rolled and what they come to. Nothing about one is decided by hand: a roll picks any good thing and any bad thing that make sense together, and how far each goes, so no two need be alike. A rarer
    /// sticker goes further on its good side and less far on its bad. What's <see cref="Written"/> is for books that are already written, which have to come out the same every time.
    /// </summary>
    public static class Stickers
    {
        private const float RaritySteps = 3f;
        private const float BestDrawback = 0.75f;       // Of the way from a drawback's worst to its least, for the best there is. Nothing gets away with nothing.

        /// <param name="Fits">Only means anything on one kind of page, if it isn't null.</param>
        /// <param name="Good">From how little to how much it can do for a page, or nothing if it can't. Something that has no amount has both at nothing, and <paramref name="Flag"/>.</param>
        /// <param name="Bad">The same, for what it can do against one.</param>
        /// <param name="Whole">Only goes in whole numbers.</param>
        private readonly record struct Line(PageEffect Effect, PageKind? Fits, (float Least, float Most) Good, (float Least, float Most) Bad, bool Whole = false, bool Flag = false);

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
            new(PageEffect.Stops, PageKind.Projectile, Never, Never, Flag: true),
            new(PageEffect.Rides, PageKind.Projectile, Never, Never, Flag: true),
            new(PageEffect.Area, PageKind.Area, (15f, 100f), (15f, 50f)),
        };

        private static readonly Dictionary<string, Sticker> ByName = new()
        {
            ["two-at-once"] = Fixed(PageEffect.Projectiles, 1f, PageEffect.Potency, 30f),
            ["three-at-once"] = Fixed(PageEffect.Projectiles, 2f, PageEffect.Potency, 45f),
            ["five-at-once"] = Fixed(PageEffect.Projectiles, 4f, PageEffect.Potency, 60f),
            ["pierce-one"] = Fixed(PageEffect.Pierce, 1f, PageEffect.Potency, 10f),
            ["pierce-three"] = Fixed(PageEffect.Pierce, 3f, PageEffect.Potency, 25f),
            ["chain-one"] = Fixed(PageEffect.Chains, 1f, PageEffect.Potency, 15f),
            ["chain-three"] = Fixed(PageEffect.Chains, 3f, PageEffect.Potency, 30f),
            ["stops"] = Fixed(PageEffect.Stops, 0f, PageEffect.None, 0f),
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
        };

        private static Sticker Fixed(PageEffect good, float goodBy, PageEffect bad, float badBy) => new Sticker { Good = good, GoodBy = goodBy, Bad = bad, BadBy = badBy };

        /// <summary>Every sticker that books already written can use, by name. Clauses in saves from before stickers go by these names too.</summary>
        public static IEnumerable<string> Names => ByName.Keys;

        /// <summary>A sticker that always says the same thing, or nothing if there's no such name.</summary>
        public static Sticker? Written(string name) => ByName.GetValueOrDefault(name)?.Copy();

        /// <summary>Every effect a sticker can be rolled with on its good side.</summary>
        public static IEnumerable<PageEffect> Goods => Lines.Where(CanHelp).Select(line => line.Effect);

        /// <summary>Every effect a sticker can be rolled with on its bad side.</summary>
        public static IEnumerable<PageEffect> Bads => Lines.Where(CanHurt).Select(line => line.Effect);

        private static bool CanHelp(Line line) => line.Good.Most > 0f || line.Flag;

        private static bool CanHurt(Line line) => line.Bad.Most > 0f;

        private static Line LineOf(PageEffect effect) => Array.Find(Lines, line => line.Effect == effect);

        /// <summary>Which kind of page it goes on, if it's only one. It can't be both, as a sticker that only half works isn't one anybody could use.</summary>
        public static PageKind? Fits(Sticker sticker) => LineOf(sticker.Good).Fits ?? LineOf(sticker.Bad).Fits;

        /// <summary>Whether it can go on a page of this kind.</summary>
        public static bool GoesOn(Sticker sticker, PageKind kind) => Fits(sticker) is not PageKind only || only == kind;

        /// <summary>Rolls one.</summary>
        /// <param name="rarity">How good it is, which is how far its good side goes and how little its bad side does.</param>
        public static Sticker Roll(Rarity rarity, WorldRandom random)
        {
            // A page for projectiles, for areas, or either: whichever it is, everything on it has to mean something there.
            int kind = random.Int(3);
            PageKind? fits = kind == 0 ? null : kind == 1 ? PageKind.Projectile : PageKind.Area;
            bool Belongs(Line line) => line.Fits is null || line.Fits == fits;

            Line[] goods = Array.FindAll(Lines, line => CanHelp(line) && Belongs(line));
            Line good = goods[random.Int(goods.Length)];
            Line[] bads = Array.FindAll(Lines, line => CanHurt(line) && Belongs(line) && line.Effect != good.Effect);
            Line bad = bads[random.Int(bads.Length)];

            float strength = ((int)rarity + random.Float()) / RaritySteps;
            float mercy = ((int)rarity + random.Float()) / RaritySteps * BestDrawback;
            return new Sticker
            {
                Good = good.Effect,
                GoodBy = good.Flag ? 0f : Amount(good.Good.Least, good.Good.Most, strength, good.Whole),
                Bad = bad.Effect,
                BadBy = Amount(bad.Bad.Least, bad.Bad.Most, 1f - mercy, bad.Whole),
            };
        }

        private static float Amount(float least, float most, float share, bool whole)
        {
            float value = least + (most - least) * share;
            return whole ? MathF.Round(value) : MathF.Round(value * 2f) * 0.5f;
        }

        /// <summary>What one does to a page's numbers, in the same terms as a sentence. Good and bad each go the way that word means.</summary>
        public static Weights WeightsOf(Sticker sticker) => Add(Add(default, sticker.Good, sticker.GoodBy, 1f), sticker.Bad, sticker.BadBy, -1f);

        private static Weights Add(Weights weights, PageEffect effect, float by, float sign) => effect switch
        {
            PageEffect.Potency => weights with { Potency = by * sign },
            PageEffect.Speed => weights with { Speed = by * sign },
            PageEffect.Area => weights with { Area = by * sign },
            PageEffect.Range => weights with { Range = by * sign },
            PageEffect.UseTime => weights with { UseTime = -by * sign },

            // What it can take off is a share and what it can put on is flat, as a page that's free has to feel it too.
            PageEffect.Cost => sign > 0f ? weights with { Cost = -by } : weights with { Added = by },
            _ => weights,
        };

        /// <summary>A value that's arrived from outside brought back to what could have been rolled, for a save that's been edited.</summary>
        public static void Fix(Sticker sticker)
        {
            if (!Enum.IsDefined(sticker.Good)) sticker.Good = PageEffect.None;
            if (!Enum.IsDefined(sticker.Bad)) sticker.Bad = PageEffect.None;
            sticker.GoodBy = Math.Clamp(float.IsFinite(sticker.GoodBy) ? sticker.GoodBy : 0f, 0f, Rulebook.MostStickerGood);
            sticker.BadBy = Math.Clamp(float.IsFinite(sticker.BadBy) ? sticker.BadBy : 0f, 0f, Rulebook.MostStickerBad);
        }
    }
}

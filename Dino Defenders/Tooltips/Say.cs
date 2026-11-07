using System.Globalization;

namespace Dino_Defenders.Tooltips
{
    /// <summary>The only way a number becomes text, so that everything a player reads writes one the same way: metres as "8 m", seconds as "0.6 s", and never a word for what could be a number.</summary>
    public static class Say
    {
        public const string To = "–";

        /// <summary>A number with as many places as it needs, and no more than it's worth.</summary>
        public static string N(float value)
        {
            float size = MathF.Abs(value);
            return value.ToString(size >= 100f ? "0" : size >= 10f ? "0.#" : "0.##", CultureInfo.InvariantCulture);
        }

        /// <summary>A number a build has come to, to as many places as anyone would read: 8.8, 49, 1204.</summary>
        public static string Amount(float value)
        {
            float size = MathF.Abs(value);
            return value.ToString(size >= 10f ? "0" : size >= 1f ? "0.#" : "0.##", CultureInfo.InvariantCulture);
        }

        /// <summary>A percentage a build has come to, written the same way.</summary>
        public static string Rate(float points) => Amount(points) + "%";

        public static string Whole(float value) => value.ToString("0", CultureInfo.InvariantCulture);

        /// <summary>What's already a percentage.</summary>
        public static string Percent(float points) => N(points) + "%";

        /// <summary>A share of one, as a percentage.</summary>
        public static string Share(float share) => N(share * 100f) + "%";

        public static string Metres(float metres) => N(metres) + " m";

        public static string Seconds(float seconds) => N(seconds) + " s";

        public static string Degrees(float radians) => Whole(radians * 180f / MathF.PI) + "°";

        public static string Gold(int amount) => amount.ToString(CultureInfo.InvariantCulture) + " Gold";

        public static string Range(float low, float high) => Amount(low) == Amount(high) ? Amount(low) : Amount(low) + To + Amount(high);

        public static string PercentRange(float low, float high) => N(low * 100f) + To + N(high * 100f) + "%";

        public static string Signed(float value) => (value >= 0f ? "+" : "") + N(value);

        /// <summary>Things in a sentence: "a, b and c".</summary>
        public static string List(IReadOnlyList<string> things)
        {
            if (things.Count == 0) return "";
            if (things.Count == 1) return things[0];
            return string.Join(", ", things.Take(things.Count - 1)) + " and " + things[^1];
        }
    }
}

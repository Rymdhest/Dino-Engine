using Dino_Defenders.Books;
using Dino_Defenders.Stats;

namespace Dino_Defenders.Tooltips
{
    /// <summary>
    /// Which way a line cuts for whoever has it: whether it leaves them better off or worse. More of a stat is better but for the few a build wants less of,
    /// and a rule says for itself (<see cref="RuleInfo.Hurts"/>). It's what puts a buff that hurts with the debuffs, and what says a unique's cost is a cost.
    /// </summary>
    public static class Harm
    {
        /// <summary>The stats a build wants less of. Of everything else, more is better.</summary>
        public static readonly Stat[] Unwanted = { Stat.DamageTaken, Stat.BookCost, Stat.AilmentTaken, Stat.BarrierDelay, Stat.DodgeCooldown, Stat.HealthDrain };

        /// <summary>Whether a line leaves whoever has it worse off, however little.</summary>
        public static bool Hurts(in Mod mod)
        {
            bool unwanted = Array.IndexOf(Unwanted, mod.Stat) >= 0;
            return mod.Kind switch
            {
                ModKind.Rule => ModText.Of(mod.Rule)?.Hurts == true,
                ModKind.Override => !unwanted,
                ModKind.Flat or ModKind.Increased or ModKind.More => mod.Value * (unwanted ? 1f : -1f) > 0f,
                _ => false,
            };
        }

        /// <summary>Whether a line leaves whoever has it better off, however little.</summary>
        public static bool Helps(in Mod mod)
        {
            bool unwanted = Array.IndexOf(Unwanted, mod.Stat) >= 0;
            return mod.Kind switch
            {
                ModKind.Rule => ModText.Of(mod.Rule)?.Hurts != true,
                ModKind.Flat or ModKind.Increased or ModKind.More => mod.Value * (unwanted ? -1f : 1f) > 0f,
                ModKind.Override => unwanted,
                _ => true,
            };
        }

        /// <summary>Whether a buff is a debuff: more of what it does hurts than helps.</summary>
        public static bool IsDebuff(BuffDefinition buff)
        {
            int hurts = 0, helps = 0;
            foreach (Mod mod in buff.Mods)
            {
                if (Hurts(mod)) hurts++;
                else if (Helps(mod)) helps++;
            }
            return hurts > helps;
        }
    }
}

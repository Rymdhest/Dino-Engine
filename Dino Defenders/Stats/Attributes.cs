using static Dino_Defenders.Stats.M;
using static Dino_Defenders.Stats.Rulebook;

namespace Dino_Defenders.Stats
{
    /// <summary>
    /// What gives other stats. These are ordinary modifiers, counted per point, so a sheet adds them up like anything else and anything that scales a stat scales what came from an attribute too.
    /// Whoever has an attribute gets them, hero or enemy, which is what keeps this from being a rule for one side.
    /// </summary>
    public static class Attributes
    {
        public static readonly Mod[] Mods =
        {
            Flat(Stat.Health, StrengthHealth).For(Per.Strength, 1f),
            Inc(Stat.Damage, StrengthPhysical, Tags.Physical).For(Per.Strength, 1f),
            Flat(Stat.Accuracy, DexterityAccuracy).For(Per.Dexterity, 1f),
            Flat(Stat.Evasion, DexterityEvasion).For(Per.Dexterity, 1f),
            Inc(Stat.AttackSpeed, DexterityAttackSpeed).For(Per.Dexterity, 1f),
            Flat(Stat.Mana, IntellectMana).For(Per.Intellect, 1f),
            Flat(Stat.ManaRegen, IntellectManaRegen).For(Per.Intellect, 1f),
            Inc(Stat.Damage, IntellectSpellDamage, Tags.Spell).For(Per.Intellect, 1f),
        };

        public static bool Is(Stat stat) => stat is Stat.Strength or Stat.Dexterity or Stat.Intellect;
    }
}

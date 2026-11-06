using Dino_Defenders.Progression;
using Dino_Defenders.Skills;
using Dino_Defenders.Stats;
using Dino_Engine.Animation;

namespace Dino_Defenders.Client
{
    /// <summary>
    /// What a body does for a skill, worked out from what the skill is. So a new skill looks right without anyone saying anything, and a new way of doing something is one more line here.
    /// </summary>
    public static class SkillMotions
    {
        private const float WideArc = 2.9f;
        private const float HeavyUse = 0.95f;

        /// <param name="serial">Which blow in a run of them this is, as blows go to and fro.</param>
        public static Gesture Of(SkillDefinition skill, Carry carry, bool animal, int serial)
        {
            if (animal) return skill.UseTime >= HeavyUse || skill.Arc >= WideArc ? Gestures.Maul : Gestures.Bite;

            switch (skill.Form)
            {
                case SkillForm.Strike:
                    if (skill.Arc >= MathF.Tau) return Gestures.Whirl;
                    if (skill == SkillCatalog.ShieldBash) return Gestures.Bash;
                    if (skill.UseTime >= HeavyUse) return Gestures.Overhead;
                    if (skill.Arc >= WideArc) return Gestures.Sweep;
                    if (carry == Carry.Spear) return Gestures.Thrust;
                    if (carry == Carry.Dagger && (serial & 3) == 3) return Gestures.Thrust;
                    return Gestures.Forehand;

                case SkillForm.Leap:
                    return Gestures.Lunge;

                case SkillForm.Bolt:
                    if ((skill.Tags & Tags.Attack) == 0) return Gestures.Push;
                    return carry == Carry.Bow ? Gestures.Shoot : Gestures.Throw;

                case SkillForm.Nova:
                    return Gestures.Burst;

                case SkillForm.Burst:
                case SkillForm.Zone:
                    return Gestures.Call;

                case SkillForm.Chain:
                case SkillForm.Beam:
                    return Gestures.Push;

                case SkillForm.Buff:
                    return Gestures.Gather;

                default:
                    return Gestures.Summon;
            }
        }

        /// <summary>How something a hero has in their hands is carried.</summary>
        public static Carry CarryOf(WeaponKind kind) => kind switch
        {
            WeaponKind.Dagger => Carry.Dagger,
            WeaponKind.Spear => Carry.Spear,
            WeaponKind.Bow => Carry.Bow,
            WeaponKind.Staff => Carry.Staff,
            WeaponKind.Wand => Carry.Wand,
            WeaponKind.Mace => Carry.Greatsword,
            _ => Carry.Sword,
        };
    }
}

using Dino_Defenders.Books;
using Dino_Defenders.Progression;
using Dino_Engine.Animation;

namespace Dino_Defenders.Client
{
    /// <summary>
    /// What a body does for a page, worked out from what it looks like. So a book nobody has seen before looks right without anyone saying anything, and a new way of doing something is one more line here.
    /// </summary>
    public static class SkillMotions
    {
        /// <param name="serial">Which blow in a run of them this is, as blows go to and fro.</param>
        public static Gesture Of(PageSign sign, Carry carry, bool animal, int serial)
        {
            if (animal) return sign.Heavy || sign.Wide ? Gestures.Maul : Gestures.Bite;

            switch (sign.Form)
            {
                case PageForm.Strike:
                    if (sign.Spell) return Gestures.Push;
                    if (carry == Carry.Bow) return Gestures.Shoot;
                    if (sign.Heavy) return Gestures.Overhead;
                    if (sign.Wide) return Gestures.Sweep;
                    if (carry == Carry.Spear) return Gestures.Thrust;
                    if (carry == Carry.Dagger && (serial & 3) == 3) return Gestures.Thrust;
                    return Gestures.Forehand;

                case PageForm.Leap:
                    return Gestures.Lunge;

                case PageForm.Bolt:
                    if (sign.Spell) return Gestures.Push;
                    return carry == Carry.Bow ? Gestures.Shoot : Gestures.Throw;

                case PageForm.Nova:
                    return sign.Spell ? Gestures.Burst : Gestures.Whirl;

                case PageForm.Burst:
                case PageForm.Zone:
                    return Gestures.Call;

                case PageForm.Beam:
                    return Gestures.Push;

                case PageForm.Buff:
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

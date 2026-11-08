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
        /// <summary>What a body built like <paramref name="rig"/> does for a page.</summary>
        public static Gesture Of(PageSign sign, Carry carry, Rig rig, int serial)
        {
            if (rig.Saurian) return Saurian(sign, rig.Plan);
            return Of(sign, carry, rig.Plan == BodyPlan.Quadruped, serial);
        }

        /// <summary>What a dinosaur does for a page, which is also what it's heard doing.</summary>
        public static Gesture Saurian(PageSign sign, BodyPlan plan) => plan == BodyPlan.Biped ? Theropod(sign) : Horned(sign);

        // On two legs everything is done with the mouth or the tail, and what's done to oneself or called down is a roar, as is anything round it that takes long enough to look heavy.
        private static Gesture Theropod(PageSign sign) => sign.Form switch
        {
            PageForm.Strike => sign.Heavy ? Gestures.Stomp : Gestures.Chomp,
            PageForm.Leap => Gestures.Pounce,
            PageForm.Bolt => Gestures.Spit,
            PageForm.Nova => sign.Heavy ? Gestures.Roar : Gestures.TailSweep,
            _ => Gestures.Roar,
        };

        // On four it's horns, weight and a bellow.
        private static Gesture Horned(PageSign sign) => sign.Form switch
        {
            PageForm.Strike or PageForm.Leap => sign.Heavy || sign.Wide ? Gestures.Trample : Gestures.Gore,
            PageForm.Nova => Gestures.Trample,
            _ => Gestures.Roar,
        };

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

        /// <summary>Gives a hero's body what they have in their hands: a weapon in the main hand, and in the off hand a second weapon or a shield.</summary>
        public static void Hold(CharacterAnimator animator, IReadOnlyList<WornLook> worn)
        {
            Carry carry = Carry.Nothing;
            Carry? second = null;
            bool shield = false;
            foreach (WornLook look in worn)
            {
                if (look.Place == WornSlot.MainHand) carry = CarryOf(ItemLook.WeaponOf(look.Traits));
                else if (look.Place == WornSlot.OffHand && ItemLook.SlotOf(look.Traits) == ItemSlot.MainHand) second = CarryOf(ItemLook.WeaponOf(look.Traits));
                else if (look.Place == WornSlot.OffHand) shield = true;
            }
            animator.Hold(carry, shield, second);
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

using Dino_Engine.ECS.Components;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>
    /// How far out from the body each place's pieces may come, region by region, so that whatever is worn over something is always outside it. Pieces are made one at a time and never know what else is
    /// worn, so this is the only thing that keeps them apart: every generator keeps inside its own band, and GearLab's <c>fit</c> measures every pair of places that meet and fails on any overlap.
    /// Distances are metres out from <see cref="BodyForm"/>'s surface at the world's scale, and heights are the rig's. See GEAR.md.
    ///
    /// Who goes over whom where they meet, as clothes and armour really are worn:
    /// <list type="bullet">
    /// <item>Below the knee a boot goes over the leg's covering, which stays close (<see cref="LegsUnder"/>), and a knee's plate comes down over the boot's top.</item>
    /// <item>At the waist the body's piece goes over the legs', and what hangs below the hips clears both legs (<see cref="HemClear"/>).</item>
    /// <item>On the forearm a glove's cuff goes over a fitted sleeve, and a belled sleeve over the cuff.</item>
    /// <item>At the neck a helm's or a hood's neck tucks inside the body's collar, and anything wider stays above it, as a helm sits on a gorget.</item>
    /// <item>A shoulder's plates stay below a brim and start outside what the head's pieces may reach across beside the neck, and a drape over the shoulders belongs to the body's piece,
    /// never the head's.</item>
    /// </list>
    /// </summary>
    public static class GearFit
    {
        // Legs: from the waist to the ankle.
        public const float LegsInner = 0.025f;      // A leg's covering sits at least this far off the leg.
        public const float LegsUnder = 0.065f;      // Below the knee, where a boot goes over it, it comes out no further than this.
        public const float LegsShell = 0.16f;       // A thigh's or a knee's plate comes out no further than this.
        public const float LegsTop = 2.42f;         // The highest a leg's covering comes, under the body's piece.
        public const float WaistUnder = 0.07f;      // At the waist, how far out it may come, under the body's piece.
        public const float KneeLow = 0.8f;          // The lowest a knee's plate comes, lapping a boot's top.
        public const float KneeHigh = 1.24f;

        // Feet.
        public const float BootTop = 0.94f;         // The highest a boot's shaft goes, below the knee's plate.
        public const float BootInner = 0.075f;      // A boot's shaft is at least this far off the leg, outside the leg's covering.
        public const float BootOuter = 0.155f;      // And no further than this, a cuff at its top included, unless the top is low.
        public const float BootTopOuter = 0.12f;    // Above KneeLow, where a knee's plate laps down over it, no further than this.

        // Between the legs, which stand too close for everything worn on both to fit, each leg's things are drawn in flat short of the body's middle, each layer a little shorter than the one it
        // goes over, so that the order they're worn in holds there too and neither leg's things meet the other's.
        public const float SideCover = 0.03f;       // A leg's covering.
        public const float SideWraps = 0.024f;      // What's wound round a shin, over the covering and under a boot.
        public const float SideBoot = 0.012f;       // A boot's shaft.
        public const float SideStrap = 0.004f;      // The outside of whatever's strapped round a boot, and of a greave.
        public const float CuffLow = 0.78f;         // A boot whose top is below this may turn a wider cuff down over it.
        public const float CuffOuter = 0.22f;

        // The body: from the hem to the collar.
        public const float BodyBase = 0.08f;        // The body's piece sits at least this far off at the waist, over the legs' covering.
        public const float BodyOuter = 0.26f;       // And comes out no further than this, before a cape.
        public const float HemClear = 0.04f;        // What hangs below the hips clears the outside of both legs by this.
        public const float CollarLow = 3.86f;       // The collar's band, round the neck.
        public const float CollarTop = 4.1f;        // The highest anything of the body's comes, at the neck.
        public const float CollarInner = 0.37f;     // From the neck's middle, the least a collar's inside comes to, so a helm's neck fits inside it.
        public const float CollarOuter = 0.54f;
        public const float ShoulderTop = 4.3f;      // The highest a shoulder's plate rises, below a brim.

        // Arms and hands. A forearm runs from 0.5 to 1 of the way from shoulder to wrist (BodyForm.Arm).
        public const float SleeveFitted = 0.065f;   // A sleeve on the forearm, where a cuff goes over it.
        public const float SleeveBell = 0.25f;      // Unless it's belled, and then it's at least this far out, over the cuff.
        public const float GloveInner = 0.075f;     // A glove's cuff.
        public const float GloveOuter = 0.2f;
        public const float CuffReach = 0.8f;        // How far up the forearm a cuff may come, as a share of it: never as far as the elbow's plate.

        // What's held on the forearm. A shield's grip is in the middle of the fist, this far past the wrist, and its board lies along the back of the forearm this far off its middle, over anything
        // a glove or a sleeve puts there (a gauntlet's cuff comes to 0.29): from the grip out to this far either side, where the forearm and the fist are, nothing of the board is nearer.
        public const float GripPast = 0.1f;
        public const float ShieldStandoff = 0.3f;
        public const float ShieldArmRoom = 0.3f;

        // The head.
        public const float NeckTuck = 0.34f;        // Below the brow's band, a helm's or a hood's neck comes no further from the neck's middle than this.
        public const float HelmLow = 4.12f;         // Anything of the head's wider than that stays above this.
        public const float NeckLow = 3.92f;         // And nothing of the head's comes lower than this.
        public const float BrimLow = 4.38f;         // A brim stays above a shoulder's plate.
        public const float HeadSide = 0.36f;        // Below ShoulderTop, nothing of the head's comes further across than this, inside where a shoulder's plate starts.
        public const float PauldronIn = 0.38f;      // How far across a shoulder's plate starts, from the body's middle.

        /// <summary>
        /// How far a hem's inside must be from the body's middle at a height below the hips, across and front to back, to clear both legs in whatever they have on: the ellipse that holds
        /// a circle round each leg as far out as a thigh's plate comes (<see cref="LegsShell"/>) and <see cref="HemClear"/> more. It touches that circle at the side, and is as deep as it has to
        /// be for its curve there to be no tighter than the circle's, as one only as deep as the circle cut into it at the front and back corners.
        /// </summary>
        public static Vector2 HemAt(float y)
        {
            float leg = BodyForm.Leg(Math.Clamp(y, 0f, BodyForm.Hip)) + LegsShell + HemClear;
            float across = BodyForm.HipX + leg;
            return new Vector2(across, MathF.Sqrt(across * leg));
        }

        /// <summary>The forearm's share from the wrist, as how far along the arm from the shoulder that is.</summary>
        public static float ArmAt(float fromWrist) => 1f - fromWrist * 0.5f;

        /// <summary>
        /// What a shoulder's plate is skinned to, by where it is: over the top of the shoulder it stays with the chest, and out along the arm it goes with the arm, so that a hanging arm takes the
        /// plate down with it and the body's side never comes through. Two weights to a vertex, as everywhere.
        /// </summary>
        public static (Vector2 bones, Vector2 weights) Shoulder(Vector3 at, float side)
        {
            float out_ = MathF.Abs(at.X) - (BodyForm.ShoulderX - 0.06f);
            float onArm = Math.Clamp(out_ / 0.32f, 0f, 1f);
            onArm = onArm * onArm * (3f - 2f * onArm);
            onArm = 0.25f + 0.75f * onArm;
            return (new Vector2((int)BoneType.Chest, (int)BodyForm.UpperArm(side)), new Vector2(1f - onArm, onArm));
        }
    }
}

namespace Dino_Engine.Animation
{
    /// <summary>
    /// How someone moves, as opposed to what they're built like. A few numbers around 1 are all that tell a brute's tread from a cutthroat's scuttle, which is most of how a pack reads from up where the camera is.
    /// A new way of moving is one more of these. Anything left alone stays as it is here.
    /// </summary>
    public sealed class MotionStyle
    {
        /// <summary>How quick the legs go at a given speed. Under 1 is long and heavy strides, over 1 is short and busy ones.</summary>
        public float Cadence = 1f;

        /// <summary>How far apart the feet stand, as a share of how wide the hips are.</summary>
        public float Stance = 1f;

        /// <summary>How far the hips rise and fall.</summary>
        public float Bounce = 1f;

        /// <summary>How much the weight goes from side to side.</summary>
        public float Sway = 1f;

        /// <summary>How far forward the body leans into going fast and into starting off.</summary>
        public float Lean = 1f;

        /// <summary>How much the arms swing.</summary>
        public float ArmSwing = 1f;

        /// <summary>How much hips and shoulders turn against each other.</summary>
        public float Twist = 1f;

        /// <summary>How far down the hips sit, as a share of the leg.</summary>
        public float Crouch;

        /// <summary>How far forward the top of the back curls, in radians.</summary>
        public float Hunch;

        /// <summary>How wide the elbows are carried.</summary>
        public float ElbowsOut = 1f;

        /// <summary>How much of a body's life is in it. Breathing, shifting weight and looking about all go with this.</summary>
        public float Liveliness = 1f;

        /// <summary>How much of a blow a body shows. Something heavy barely notices what throws something light around.</summary>
        public float Give = 1f;

        /// <summary>How far people differ from each other, as a share of each number above. No two of a pack are quite in step.</summary>
        public float Variety = 0.08f;

        // What only a dinosaur has to say about itself.

        /// <summary>How far the tail swings from side to side in time with the legs, as the muscle that pulls each thigh back runs down the tail and pulls it round with it.</summary>
        public float TailSway = 1f;

        /// <summary>How little of that goes down the tail as a wave, from 0 for a whip to 1 for a rod stiffened with tendons, as a raptor's was.</summary>
        public float TailStiff;

        /// <summary>How far the toes point in, in radians. Theropod trackways are narrow, with the toes turned a little towards the line they walk.</summary>
        public float ToeIn;

        /// <summary>How wide the jaw goes, in radians.</summary>
        public float Gape = 0.8f;

        /// <summary>How much the jaw works open and shut while it breathes, when roused. Small hunters pant.</summary>
        public float Pant;

        /// <summary>How far the head is carried below level, in radians. Something that grazes or charges carries it low.</summary>
        public float HeadLow;

        /// <summary>Whether four legs ever go into a gallop, from 0 for something too heavy to, which trots, to 1.</summary>
        public float Gallop = 1f;

        /// <summary>How far something on four legs rolls its weight over to whichever side has more feet down, as an elephant does, from 0 for nothing at all.</summary>
        public float WeightShift;

        public static readonly MotionStyle Hero = new MotionStyle { Lean = 1.15f, Crouch = 0.02f };

        public static readonly MotionStyle Villager = new MotionStyle { Cadence = 0.92f, Lean = 0.6f, ArmSwing = 0.75f, Bounce = 0.8f };

        public static readonly MotionStyle Outlaw = new MotionStyle { Crouch = 0.04f, Stance = 1.1f, Lean = 1.1f };

        public static readonly MotionStyle Cutthroat = new MotionStyle { Cadence = 1.14f, Crouch = 0.1f, Stance = 1.15f, Lean = 1.45f, Hunch = 0.16f, ArmSwing = 0.8f, Give = 1.3f };

        public static readonly MotionStyle Brute = new MotionStyle { Cadence = 0.82f, Stance = 1.3f, Sway = 1.6f, Bounce = 1.25f, Lean = 0.75f, ArmSwing = 0.7f, ElbowsOut = 1.5f, Twist = 1.3f, Hunch = 0.1f, Give = 0.55f };

        public static readonly MotionStyle Armoured = new MotionStyle { Cadence = 0.88f, Stance = 1.15f, Sway = 1.2f, Bounce = 0.8f, Lean = 0.7f, ArmSwing = 0.55f, ElbowsOut = 1.3f, Give = 0.6f };

        // The dead have nothing to carry themselves with. They go on rails, with no spring in them and nothing in the arms.
        public static readonly MotionStyle Dead = new MotionStyle { Cadence = 0.9f, Stance = 0.9f, Bounce = 0.35f, Sway = 1.5f, Lean = 0.4f, ArmSwing = 0.3f, Twist = 0.5f, Hunch = 0.3f, Liveliness = 0.15f, Crouch = 0.03f, Variety = 0.16f, Give = 1.2f };

        // Long robes and a straight back: there's little to see of the legs and hardly any bounce in the head.
        public static readonly MotionStyle Robed = new MotionStyle { Cadence = 1.1f, Bounce = 0.45f, Sway = 0.6f, Lean = 0.45f, ArmSwing = 0.4f, Twist = 0.6f, Stance = 0.8f };

        public static readonly MotionStyle Hound = new MotionStyle { Cadence = 1.2f, Bounce = 1.1f, Give = 1.2f, Variety = 0.1f };

        public static readonly MotionStyle Boar = new MotionStyle { Cadence = 1.35f, Bounce = 0.7f, Sway = 1.3f, Give = 0.8f };

        public static readonly MotionStyle Bear = new MotionStyle { Cadence = 0.78f, Bounce = 1.1f, Sway = 2f, Give = 0.45f };

        // Dinosaurs. A trackway's feet fall near its middle line, so all of them stand narrow, and sway only a little over each foot.

        /// <summary>Quick, light and always looking about, with a stiff tail held out straight behind.</summary>
        public static readonly MotionStyle Raptor = new MotionStyle { Cadence = 1.08f, Stance = 0.42f, Bounce = 0.9f, Sway = 0.7f, Lean = 1.25f, Twist = 0.7f, Liveliness = 1.35f, Give = 1.05f, TailSway = 0.7f, TailStiff = 0.75f, ToeIn = 0.07f, Gape = 0.85f, Pant = 1f, Variety = 0.1f };

        /// <summary>Small, quick and many, busy as birds on the ground.</summary>
        public static readonly MotionStyle Scurrier = new MotionStyle { Cadence = 1.2f, Stance = 0.45f, Bounce = 1.1f, Sway = 0.6f, Lean = 1.3f, Twist = 0.6f, Liveliness = 1.6f, Give = 1.3f, TailSway = 0.9f, TailStiff = 0.35f, ToeIn = 0.05f, Gape = 0.7f, Pant = 1.2f, Variety = 0.14f };

        /// <summary>Lean and long-necked, swaying as it goes.</summary>
        public static readonly MotionStyle Stalker = new MotionStyle { Cadence = 0.98f, Stance = 0.45f, Bounce = 0.85f, Sway = 0.9f, Lean = 1.05f, Twist = 0.85f, Liveliness = 1.1f, Give = 0.9f, TailSway = 1.05f, TailStiff = 0.25f, ToeIn = 0.08f, Gape = 0.9f, Pant = 0.5f };

        /// <summary>Huge and unhurried, every step taken in full, the tail swinging long behind it. What it has hold of it doesn't let go of to look about.</summary>
        public static readonly MotionStyle Tyrant = new MotionStyle { Cadence = 0.84f, Stance = 0.5f, Bounce = 0.7f, Sway = 1.1f, Lean = 0.8f, Twist = 0.8f, Liveliness = 0.7f, Give = 0.38f, TailSway = 1.1f, TailStiff = 0.1f, ToeIn = 0.1f, Gape = 0.95f, Pant = 0.15f, Variety = 0.05f };

        /// <summary>On four legs with its head down and its weight in front, too heavy to gallop.</summary>
        public static readonly MotionStyle Horned = new MotionStyle { Cadence = 0.86f, Bounce = 0.65f, Sway = 1.3f, Lean = 0.7f, Give = 0.45f, TailSway = 0.6f, TailStiff = 0.3f, Gape = 0.45f, HeadLow = 0.22f, Gallop = 0f };

        /// <summary>Bones with nothing to hold them up but what raised them: stiff, jerky and slow to look round.</summary>
        public static readonly MotionStyle Bones = new MotionStyle { Cadence = 0.92f, Stance = 0.5f, Bounce = 0.45f, Sway = 1.3f, Lean = 0.55f, Twist = 0.5f, Liveliness = 0.25f, Give = 1.2f, TailSway = 0.55f, TailStiff = 0.45f, ToeIn = 0.04f, Gape = 1.1f, Variety = 0.16f };

        /// <summary>
        /// A long-necked giant: long slow strides that never hurry, the weight rolling from side to side over the feet that are down, as an elephant's does, the tail swinging long behind, and nothing
        /// a blow could do to it that it would show.
        /// </summary>
        public static readonly MotionStyle Giant = new MotionStyle { Cadence = 0.8f, Stance = 1f, Bounce = 0.4f, Sway = 0.6f, Lean = 0.45f, Twist = 0.5f, Liveliness = 0.55f, Give = 0.15f, TailSway = 0.75f, TailStiff = 0.12f, Gape = 0.45f, Gallop = 0f, WeightShift = 1f, Variety = 0.06f };

        /// <summary>What's rotting on its feet: head hung, weight rolling from side to side, the tail dragging along behind.</summary>
        public static readonly MotionStyle Rotting = new MotionStyle { Cadence = 0.8f, Stance = 0.65f, Bounce = 0.5f, Sway = 2f, Lean = 0.45f, Twist = 0.6f, Liveliness = 0.3f, Give = 1.1f, TailSway = 0.5f, TailStiff = 0f, ToeIn = 0f, Gape = 0.9f, HeadLow = 0.18f, Crouch = 0.04f, Variety = 0.18f, Gallop = 0f };

        /// <summary>This style as one person has it, with <paramref name="seed"/> saying who.</summary>
        public MotionStyle Individual(int seed)
        {
            uint state = (uint)seed * 2654435761u + 0x9E3779B9u;
            float Next(float value)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return value * (1f + Variety * ((state & 0xFFFF) / 32767.5f - 1f));
            }

            return new MotionStyle
            {
                Cadence = Next(Cadence),
                Stance = Next(Stance),
                Bounce = Next(Bounce),
                Sway = Next(Sway),
                Lean = Next(Lean),
                ArmSwing = Next(ArmSwing),
                Twist = Next(Twist),
                Crouch = Crouch,
                Hunch = Hunch,
                ElbowsOut = ElbowsOut,
                Liveliness = Next(Liveliness),
                Give = Give,
                Variety = Variety,
                TailSway = Next(TailSway),
                TailStiff = TailStiff,
                ToeIn = ToeIn,
                Gape = Gape,
                Pant = Pant,
                HeadLow = HeadLow,
                Gallop = Gallop,
                WeightShift = WeightShift,
            };
        }
    }
}

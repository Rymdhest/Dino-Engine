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
            };
        }
    }
}

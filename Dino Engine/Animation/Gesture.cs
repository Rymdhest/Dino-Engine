using OpenTK.Mathematics;

namespace Dino_Engine.Animation
{
    /// <summary>
    /// Where a hand is, and which way what's in it points. Everything is around the body and in terms of the arm, which is what lets one gesture fit anyone: angles are degrees from where the blow is aimed, with
    /// more going to the body's left, and heights and reaches are arm lengths from the middle of the chest. Going round a circle gives a hand its arc without anyone having to draw one.
    /// </summary>
    public readonly record struct HandKey(float Turn, float Height, float Reach, float BladeTurn = 0f, float BladeRise = 0f)
    {
        public static HandKey Lerp(in HandKey a, in HandKey b, float t) => new HandKey(
            a.Turn + (b.Turn - a.Turn) * t, a.Height + (b.Height - a.Height) * t, a.Reach + (b.Reach - a.Reach) * t,
            a.BladeTurn + (b.BladeTurn - a.BladeTurn) * t, a.BladeRise + (b.BladeRise - a.BladeRise) * t);

        /// <summary>The same place on the other side of the body.</summary>
        public HandKey Mirrored => new HandKey(-Turn, Height, Reach, -BladeTurn, BladeRise);
    }

    /// <summary>What the rest of the body does about it. Angles are degrees, and what's a share is of the leg.</summary>
    /// <param name="Turn">Where the shoulders face, from where the blow is aimed.</param>
    /// <param name="Bend">How far forward the back goes.</param>
    /// <param name="Tilt">How far over to the left.</param>
    /// <param name="Sink">How far down the hips go, as a share of a leg.</param>
    /// <param name="Push">How far the hips go towards the aim, as a share of a leg.</param>
    /// <param name="Spin">How far round the whole body has turned.</param>
    /// <param name="Step">How far the front foot goes out, as a share of a leg.</param>
    /// <param name="Head">How far the head goes towards the aim, as a share of a leg. For what fights with its teeth.</param>
    /// <param name="Rise">How far the chest comes up, as a share of a leg. For what rears.</param>
    public readonly record struct BodyKey(float Turn = 0f, float Bend = 0f, float Tilt = 0f, float Sink = 0f, float Push = 0f, float Spin = 0f, float Step = 0f, float Head = 0f, float Rise = 0f)
    {
        public static BodyKey Lerp(in BodyKey a, in BodyKey b, float t) => new BodyKey(
            a.Turn + (b.Turn - a.Turn) * t, a.Bend + (b.Bend - a.Bend) * t, a.Tilt + (b.Tilt - a.Tilt) * t, a.Sink + (b.Sink - a.Sink) * t, a.Push + (b.Push - a.Push) * t,
            a.Spin + (b.Spin - a.Spin) * t, a.Step + (b.Step - a.Step) * t, a.Head + (b.Head - a.Head) * t, a.Rise + (b.Rise - a.Rise) * t);

        public BodyKey Mirrored => new BodyKey(-Turn, Bend, -Tilt, Sink, Push, -Spin, Step, Head, Rise);
    }

    /// <summary>
    /// A thing a body does: a sword cut, a shot, a spell going off. There are no clips anywhere in this. It's three key poses and some rules about time, and everything in between is worked out, so that one
    /// gesture fits every creature it's given to and lands on exactly the moment the game says it lands, however fast or slow that is. Adding one is a few lines in <see cref="Gestures"/>.
    ///
    /// The three are the ones any animator would start from. <b>Cock</b> is the anticipation, held for as long as there is: from next to nothing on a hero to a long, readable draw on an enemy, as a person who
    /// has pressed a button wants the blow now and a person being swung at needs to see it coming. <b>Contact</b> is the moment it lands, which is what the timing is built around. <b>Through</b> is the follow through.
    /// </summary>
    public sealed class Gesture
    {
        public string Name = "";

        /// <summary>What the hand holding the weapon does, or the hand that leads if nothing's held. None of these on a thing with no hands.</summary>
        public HandKey[]? Main;

        /// <summary>What the other hand does, if not its own business.</summary>
        public HandKey[]? Off;

        public BodyKey[] Body = new BodyKey[3];

        /// <summary>The fastest part: seconds from the end of the cock to contact, whatever else is going on. A long wind-up doesn't make a slow blow.</summary>
        public float Snap = 0.09f;

        /// <summary>Seconds from contact to the end of the swing, before it starts to come back.</summary>
        public float Follow = 0.13f;

        /// <summary>How much of the hips goes with it, and so how much the legs get involved.</summary>
        public float Weight = 1f;

        /// <summary>Both feet leave the ground, as in a spin.</summary>
        public bool Airborne;

        /// <summary>What it turns into if it comes straight after this one, so that a run of cuts swings to and fro and doesn't start over each time. It's the body that knows this, from where its hand has got to.</summary>
        public Gesture? Next;
    }

    /// <summary>Where a gesture has got to.</summary>
    public struct GestureSample
    {
        public float Weight;        // How much of the body it has.
        public HandKey Main;
        public HandKey Off;
        public BodyKey Body;
        public bool HasMain;
        public bool HasOff;
        public bool Airborne;

        /// <summary>From cock at 0 to through at 1, with contact at <see cref="Gesture"/>'s middle key. It's the same time the sample was taken at, as the eye saw it.</summary>
        public float Swing;
    }

    /// <summary>
    /// Plays a gesture to a clock it's given. Nothing is kept here: where a gesture is depends only on when it began and when it has to land, so a body seen late picks it up in the right place,
    /// a faster attack is a shorter clock, and whoever's watching sees the same thing as whoever's doing it.
    /// </summary>
    public static class GesturePlayer
    {
        private const float QuickestCock = 0.05f;
        private const float MiddleKey = 0.5f;
        private const float SnapShare = 0.6f;       // Of the time to contact, at most. On something very quick there's no time for anything but the blow.
        private const float Accelerate = 1.8f;      // The way a blow picks up speed into contact.
        private const float Creep = 0.08f;          // How much further back it goes, holding, on a long wind-up.
        private const float FullDraw = 0.24f;       // Seconds it takes to go all the way back.
        private const float LeastDraw = 0.15f;

        /// <param name="elapsed">Seconds since it began.</param>
        /// <param name="impact">Seconds in that it lands.</param>
        /// <param name="length">Seconds it takes.</param>
        public static GestureSample Sample(Gesture gesture, float elapsed, float impact, float length)
        {
            var sample = new GestureSample { HasMain = gesture.Main != null, HasOff = gesture.Off != null, Airborne = gesture.Airborne };

            impact = MathF.Max(impact, 0.02f);
            length = MathF.Max(length, impact + 0.02f);

            float snap = MathF.Min(gesture.Snap, impact * SnapShare);
            float prepare = impact - snap;
            float cockBy = MathF.Min(prepare, MathF.Max(QuickestCock, prepare * 0.55f));

            // With no time to go all the way back, it doesn't. Moving a long way in no time would be faster than the blow, and it's the blow that's meant to be fast.
            float drawn = Math.Clamp(prepare / FullDraw, LeastDraw, 1f);
            float left = length - impact;
            float follow = MathF.Min(gesture.Follow, left * 0.55f);
            float recover = left - follow;

            float swing;
            float weight = 1f;
            float creep = 0f;
            if (elapsed < cockBy)
            {
                swing = 0f;
                weight = Gait.Smooth(0f, cockBy, elapsed);
            }
            else if (elapsed < prepare)
            {
                swing = 0f;
                creep = Gait.Smooth(cockBy, prepare, elapsed) * Creep;
            }
            else if (elapsed < impact)
            {
                float u = (elapsed - prepare) / MathF.Max(snap, 1e-3f);
                swing = MiddleKey * MathF.Pow(u, Accelerate);
            }
            else if (elapsed < impact + follow)
            {
                // Carrying on at the speed it arrived at, and dying away, so that nothing stops dead at the moment of contact.
                float u = (elapsed - impact) / MathF.Max(follow, 1e-3f);
                float carried = MiddleKey * Accelerate / MathF.Max(snap, 1e-3f) * follow / (1f - MiddleKey);
                float power = MathF.Max(carried, 1.2f);
                swing = MiddleKey + (1f - MiddleKey) * (1f - MathF.Pow(1f - u, power));
            }
            else
            {
                swing = 1f;
                weight = 1f - Gait.Smooth(impact + follow, impact + follow + MathF.Max(recover, 1e-3f), elapsed);
            }

            sample.Swing = swing;
            sample.Weight = weight;
            if (gesture.Main != null) sample.Main = AlongHand(gesture.Main, swing, creep, drawn);
            if (gesture.Off != null) sample.Off = AlongHand(gesture.Off, swing, creep, drawn);
            sample.Body = AlongBody(gesture.Body, swing, creep, drawn);
            return sample;
        }

        // A curve through all three, so that a hand goes through contact without a corner in its path.
        private static HandKey AlongHand(HandKey[] keys, float swing, float creep, float drawn)
        {
            HandKey b = keys[1], c = keys[2];
            HandKey a = HandKey.Lerp(b, keys[0], drawn);
            float Curve(float p, float q, float r) => Through(p, q, r, swing);

            var key = new HandKey(Curve(a.Turn, b.Turn, c.Turn), Curve(a.Height, b.Height, c.Height), Curve(a.Reach, b.Reach, c.Reach), Curve(a.BladeTurn, b.BladeTurn, c.BladeTurn), Curve(a.BladeRise, b.BladeRise, c.BladeRise));
            if (creep <= 0f) return key;

            // Drawing back from where it's going to go.
            return new HandKey(key.Turn + (a.Turn - b.Turn) * creep, key.Height + (a.Height - b.Height) * creep, key.Reach, key.BladeTurn + (a.BladeTurn - b.BladeTurn) * creep, key.BladeRise);
        }

        private static BodyKey AlongBody(BodyKey[] keys, float swing, float creep, float drawn)
        {
            BodyKey b = keys[1], c = keys[2];
            BodyKey a = BodyKey.Lerp(b, keys[0], drawn);
            float Curve(float p, float q, float r) => Through(p, q, r, swing);

            var key = new BodyKey(Curve(a.Turn, b.Turn, c.Turn), Curve(a.Bend, b.Bend, c.Bend), Curve(a.Tilt, b.Tilt, c.Tilt), Curve(a.Sink, b.Sink, c.Sink), Curve(a.Push, b.Push, c.Push),
                Curve(a.Spin, b.Spin, c.Spin), Curve(a.Step, b.Step, c.Step), Curve(a.Head, b.Head, c.Head), Curve(a.Rise, b.Rise, c.Rise));
            return creep <= 0f ? key : key with { Turn = key.Turn + (a.Turn - b.Turn) * creep, Bend = key.Bend + (a.Bend - b.Bend) * creep };
        }

        // Through three values at 0, the middle and 1, with no change of speed at the middle: a Hermite curve on each side, with the slope at the middle shared.
        private static float Through(float p, float q, float r, float t)
        {
            float slope = (r - p) * 0.5f;
            if (t < MiddleKey)
            {
                float u = t / MiddleKey;
                float u2 = u * u, u3 = u2 * u;
                return (2f * u3 - 3f * u2 + 1f) * p + (u3 - 2f * u2 + u) * (q - p) * 0.5f + (-2f * u3 + 3f * u2) * q + (u3 - u2) * slope;
            }
            else
            {
                float u = (t - MiddleKey) / (1f - MiddleKey);
                float u2 = u * u, u3 = u2 * u;
                return (2f * u3 - 3f * u2 + 1f) * q + (u3 - 2f * u2 + u) * slope + (-2f * u3 + 3f * u2) * r + (u3 - u2) * (r - q) * 0.5f;
            }
        }
    }

    /// <summary>What's being held, and how: what a body carries changes how it stands, runs and strikes.</summary>
    public sealed class Carry
    {
        /// <summary>How a hand holds this when nothing is going on, on the move and when there's a fight on.</summary>
        public HandKey Idle;
        public HandKey Run;
        public HandKey Guard;

        /// <summary>From the hand to the end of what's held.</summary>
        public float Length;

        /// <summary>How far along it the other hand goes, if it takes two. Under 0 is behind the first hand.</summary>
        public float SecondGrip;
        public bool TwoHanded;

        /// <summary>Which arm has it: 1 is the right, as it nearly always is. A bow is held in the left.</summary>
        public int Arm = 1;

        public static readonly Carry Nothing = new Carry
        {
            Idle = new HandKey(-84f, -0.53f, 0.5f),
            Run = new HandKey(-78f, -0.27f, 0.52f),
            Guard = new HandKey(-42f, -0.14f, 0.56f),
        };

        public static readonly Carry Sword = new Carry
        {
            Idle = new HandKey(-80f, -0.5f, 0.56f, -62f, -22f),
            Run = new HandKey(-92f, -0.36f, 0.6f, -146f, -8f),
            Guard = new HandKey(-48f, -0.2f, 0.66f, -12f, 34f),
            Length = 2.2f,
        };

        public static readonly Carry Dagger = new Carry
        {
            Idle = new HandKey(-78f, -0.5f, 0.54f, -70f, -30f),
            Run = new HandKey(-84f, -0.3f, 0.58f, -110f, -20f),
            Guard = new HandKey(-40f, -0.12f, 0.7f, -6f, 10f),
            Length = 1.1f,
        };

        // Up on the shoulder on the move, as anyone carrying something heavy does, and held across the body with both hands at the ready.
        public static readonly Carry Greatsword = new Carry
        {
            Idle = new HandKey(-62f, -0.42f, 0.62f, -40f, -36f),
            Run = new HandKey(-54f, -0.2f, 0.58f, -176f, 42f),
            Guard = new HandKey(-34f, -0.28f, 0.7f, -14f, 42f),
            Length = 3.2f,
            TwoHanded = true,
            SecondGrip = -0.62f,
        };

        public static readonly Carry Spear = new Carry
        {
            Idle = new HandKey(-72f, -0.36f, 0.56f, -8f, 78f),
            Run = new HandKey(-74f, -0.3f, 0.6f, -10f, 12f),
            Guard = new HandKey(-46f, -0.22f, 0.7f, 0f, 8f),
            Length = 3f,
            TwoHanded = true,
            SecondGrip = -1.5f,
        };

        public static readonly Carry Staff = new Carry
        {
            Idle = new HandKey(-70f, -0.34f, 0.6f, -20f, 84f),
            Run = new HandKey(-72f, -0.3f, 0.58f, -30f, 52f),
            Guard = new HandKey(-44f, -0.2f, 0.72f, -10f, 72f),
            Length = 2.6f,
        };

        public static readonly Carry Wand = new Carry
        {
            Idle = new HandKey(-78f, -0.48f, 0.56f, -60f, -30f),
            Run = new HandKey(-82f, -0.3f, 0.56f, -96f, -16f),
            Guard = new HandKey(-40f, -0.08f, 0.72f, -4f, 20f),
            Length = 1f,
        };

        public static readonly Carry Bow = new Carry
        {
            Idle = new HandKey(72f, -0.46f, 0.6f, 40f, 58f),
            Run = new HandKey(74f, -0.32f, 0.6f, 42f, 68f),
            Guard = new HandKey(40f, -0.18f, 0.74f, 20f, 78f),
            Length = 1.6f,
            Arm = 0,
        };

        /// <summary>How a shield is carried, on the arm that isn't busy. Where a key says a blade points, here it's which way the shield faces.</summary>
        public static readonly HandKey ShieldIdle = new HandKey(62f, -0.4f, 0.6f, 72f, 2f);
        public static readonly HandKey ShieldRun = new HandKey(48f, -0.24f, 0.62f, 56f, 6f);
        public static readonly HandKey ShieldGuard = new HandKey(16f, -0.1f, 0.6f, 12f, 6f);
    }

    /// <summary>
    /// Everything bodies do that isn't walking. All the numbers are in <see cref="HandKey"/>'s terms and for a right hand, with the blow aimed straight ahead. A cut that has a way back has a <see cref="Gesture.Next"/>,
    /// so a run of blows swings to and fro instead of starting from the same place each time.
    /// </summary>
    public static class Gestures
    {
        public static readonly Gesture Forehand = new Gesture
        {
            Name = "forehand",
            Main = new[]
            {
                new HandKey(-118f, 0.04f, 0.66f, -170f, 30f),
                new HandKey(-6f, -0.16f, 0.98f, -4f, -2f),
                new HandKey(74f, -0.36f, 0.74f, 128f, -20f),
            },
            Body = new[]
            {
                new BodyKey(Turn: -40f, Bend: -2f, Tilt: -5f, Sink: 0.05f, Push: -0.08f),
                new BodyKey(Turn: 8f, Bend: 12f, Tilt: 3f, Sink: 0.1f, Push: 0.2f, Step: 0.32f),
                new BodyKey(Turn: 36f, Bend: 15f, Tilt: 7f, Sink: 0.09f, Push: 0.17f, Step: 0.36f),
            },
        };

        public static readonly Gesture Backhand = new Gesture
        {
            Name = "backhand",
            Main = new[]
            {
                new HandKey(80f, -0.06f, 0.68f, 152f, 24f),
                new HandKey(6f, -0.12f, 0.98f, 2f, 2f),
                new HandKey(-86f, -0.28f, 0.82f, -138f, -10f),
            },
            Body = new[]
            {
                new BodyKey(Turn: 32f, Bend: 3f, Tilt: 5f, Sink: 0.05f, Push: -0.04f),
                new BodyKey(Turn: -6f, Bend: 11f, Tilt: -2f, Sink: 0.09f, Push: 0.2f, Step: 0.3f),
                new BodyKey(Turn: -38f, Bend: 12f, Tilt: -7f, Sink: 0.08f, Push: 0.18f, Step: 0.33f),
            },
        };

        /// <summary>Wide and low, with the whole body round behind it.</summary>
        public static readonly Gesture Sweep = new Gesture
        {
            Name = "sweep",
            Main = new[]
            {
                new HandKey(-132f, -0.14f, 0.76f, -178f, 8f),
                new HandKey(-4f, -0.3f, 1.02f, -2f, -8f),
                new HandKey(104f, -0.36f, 0.82f, 158f, -8f),
            },
            Body = new[]
            {
                new BodyKey(Turn: -54f, Bend: 4f, Tilt: -8f, Sink: 0.12f, Push: -0.1f),
                new BodyKey(Turn: 6f, Bend: 16f, Tilt: 2f, Sink: 0.17f, Push: 0.22f, Step: 0.36f),
                new BodyKey(Turn: 52f, Bend: 18f, Tilt: 9f, Sink: 0.15f, Push: 0.2f, Step: 0.4f),
            },
            Snap = 0.11f,
            Follow = 0.17f,
        };

        /// <summary>Up, and all the way down.</summary>
        public static readonly Gesture Overhead = new Gesture
        {
            Name = "overhead",
            Main = new[]
            {
                new HandKey(-24f, 0.64f, 0.24f, 176f, 58f),
                new HandKey(-6f, -0.08f, 0.94f, -2f, -6f),
                new HandKey(-2f, -0.62f, 0.62f, 0f, -52f),
            },
            Body = new[]
            {
                new BodyKey(Turn: -16f, Bend: -13f, Tilt: -3f, Sink: 0.02f, Push: -0.14f),
                new BodyKey(Turn: 2f, Bend: 24f, Tilt: 2f, Sink: 0.16f, Push: 0.3f, Step: 0.42f),
                new BodyKey(Turn: 6f, Bend: 33f, Tilt: 3f, Sink: 0.22f, Push: 0.32f, Step: 0.46f),
            },
            Snap = 0.12f,
            Follow = 0.15f,
        };

        /// <summary>Straight at them.</summary>
        public static readonly Gesture Thrust = new Gesture
        {
            Name = "thrust",
            Main = new[]
            {
                new HandKey(-46f, -0.1f, 0.3f, -6f, 6f),
                new HandKey(-3f, -0.03f, 1.06f, 0f, 0f),
                new HandKey(-2f, -0.08f, 0.96f, 0f, -3f),
            },
            Body = new[]
            {
                new BodyKey(Turn: -44f, Bend: -3f, Sink: 0.07f, Push: -0.16f),
                new BodyKey(Turn: 22f, Bend: 13f, Sink: 0.13f, Push: 0.4f, Step: 0.52f),
                new BodyKey(Turn: 26f, Bend: 15f, Sink: 0.12f, Push: 0.36f, Step: 0.54f),
            },
            Snap = 0.08f,
            Follow = 0.1f,
        };

        /// <summary>A full turn with the blade held out, off the ground. It takes the feet with it.</summary>
        public static readonly Gesture Whirl = new Gesture
        {
            Name = "whirl",
            Main = new[]
            {
                new HandKey(-70f, -0.2f, 0.9f, -74f, 6f),
                new HandKey(-40f, -0.22f, 1.02f, -44f, 2f),
                new HandKey(-30f, -0.26f, 0.96f, -34f, -4f),
            },
            Body = new[]
            {
                new BodyKey(Turn: -18f, Sink: 0.14f, Spin: -55f),
                new BodyKey(Turn: 0f, Bend: 6f, Sink: 0.16f, Spin: 150f),
                new BodyKey(Turn: 10f, Bend: 9f, Sink: 0.12f, Spin: 305f),
            },
            Snap = 0.16f,
            Follow = 0.16f,
            Airborne = true,
        };

        /// <summary>Everything behind what's on the other arm.</summary>
        public static readonly Gesture Bash = new Gesture
        {
            Name = "bash",
            Off = new[]
            {
                new HandKey(40f, -0.08f, 0.42f, 44f, 4f),
                new HandKey(6f, -0.08f, 0.68f, 2f, 6f),
                new HandKey(8f, -0.12f, 0.62f, 6f, 4f),
            },
            Main = new[]
            {
                new HandKey(-96f, -0.34f, 0.5f, -130f, -6f),
                new HandKey(-108f, -0.38f, 0.56f, -146f, -10f),
                new HandKey(-96f, -0.36f, 0.54f, -130f, -8f),
            },
            Body = new[]
            {
                new BodyKey(Turn: 34f, Bend: 2f, Sink: 0.08f, Push: -0.14f),
                new BodyKey(Turn: -18f, Bend: 14f, Sink: 0.12f, Push: 0.46f, Step: 0.5f),
                new BodyKey(Turn: -22f, Bend: 12f, Sink: 0.11f, Push: 0.4f, Step: 0.5f),
            },
        };

        /// <summary>Drawn to the chin and let go. The bow is in the left hand, and what's called the other hand here is the one on the string.</summary>
        public static readonly Gesture Shoot = new Gesture
        {
            Name = "shoot",
            Main = new[]
            {
                new HandKey(24f, 0.06f, 1.02f, 8f, 88f),
                new HandKey(22f, 0.08f, 1.04f, 6f, 88f),
                new HandKey(24f, 0.04f, 1f, 8f, 86f),
            },
            Off = new[]
            {
                new HandKey(-26f, 0.2f, 0.28f),
                new HandKey(-60f, 0.2f, 0.34f),
                new HandKey(-92f, 0.1f, 0.4f),
            },
            Body = new[]
            {
                new BodyKey(Turn: -46f, Bend: 3f, Tilt: 2f, Sink: 0.06f),
                new BodyKey(Turn: -52f, Bend: 3f, Tilt: 2f, Sink: 0.06f),
                new BodyKey(Turn: -40f, Bend: 1f, Sink: 0.05f),
            },
            Snap = 0.04f,
            Follow = 0.09f,
            Weight = 0.4f,
        };

        /// <summary>Over the shoulder and away.</summary>
        public static readonly Gesture Throw = new Gesture
        {
            Name = "throw",
            Main = new[]
            {
                new HandKey(-112f, 0.26f, 0.5f, -170f, 50f),
                new HandKey(-12f, 0.06f, 1f, -6f, 4f),
                new HandKey(24f, -0.28f, 0.82f, 34f, -28f),
            },
            Off = new[]
            {
                new HandKey(32f, 0.02f, 0.84f, 20f, 0f),
                new HandKey(66f, -0.12f, 0.56f, 70f, 0f),
                new HandKey(84f, -0.34f, 0.5f, 84f, 0f),
            },
            Body = new[]
            {
                new BodyKey(Turn: -44f, Bend: -5f, Tilt: -5f, Sink: 0.05f, Push: -0.08f),
                new BodyKey(Turn: 18f, Bend: 13f, Tilt: 3f, Sink: 0.09f, Push: 0.18f, Step: 0.3f),
                new BodyKey(Turn: 30f, Bend: 16f, Tilt: 5f, Sink: 0.08f, Push: 0.16f, Step: 0.32f),
            },
        };

        /// <summary>A spell sent at something: drawn in, and pushed out with a straight arm.</summary>
        public static readonly Gesture Push = new Gesture
        {
            Name = "push",
            Main = new[]
            {
                new HandKey(-58f, 0.02f, 0.3f, -40f, 16f),
                new HandKey(-6f, 0.04f, 1.02f, -4f, 8f),
                new HandKey(-8f, -0.04f, 0.94f, -6f, 2f),
            },
            Off = new[]
            {
                new HandKey(46f, -0.06f, 0.46f),
                new HandKey(78f, -0.2f, 0.56f),
                new HandKey(82f, -0.3f, 0.54f),
            },
            Body = new[]
            {
                new BodyKey(Turn: -32f, Bend: -5f, Sink: 0.04f, Push: -0.1f),
                new BodyKey(Turn: 20f, Bend: 9f, Sink: 0.07f, Push: 0.16f, Step: 0.24f),
                new BodyKey(Turn: 16f, Bend: 7f, Sink: 0.06f, Push: 0.12f, Step: 0.24f),
            },
            Snap = 0.1f,
            Weight = 0.7f,
        };

        /// <summary>A spell called down on a place: an arm up, and brought down to point at it.</summary>
        public static readonly Gesture Call = new Gesture
        {
            Name = "call",
            Main = new[]
            {
                new HandKey(-34f, 0.62f, 0.34f, -30f, 70f),
                new HandKey(-6f, -0.02f, 1f, -4f, -12f),
                new HandKey(-6f, -0.16f, 0.92f, -4f, -26f),
            },
            Off = new[]
            {
                new HandKey(58f, 0.18f, 0.56f),
                new HandKey(70f, -0.2f, 0.56f),
                new HandKey(78f, -0.3f, 0.54f),
            },
            Body = new[]
            {
                new BodyKey(Turn: -16f, Bend: -10f, Tilt: -4f, Sink: 0.02f, Push: -0.06f),
                new BodyKey(Turn: 6f, Bend: 13f, Tilt: 1f, Sink: 0.09f, Push: 0.14f, Step: 0.18f),
                new BodyKey(Turn: 8f, Bend: 15f, Tilt: 2f, Sink: 0.1f, Push: 0.12f, Step: 0.18f),
            },
            Snap = 0.11f,
            Weight = 0.6f,
        };

        /// <summary>Everything drawn in, and let go all round at once.</summary>
        public static readonly Gesture Burst = new Gesture
        {
            Name = "burst",
            Main = new[]
            {
                new HandKey(-22f, -0.04f, 0.26f, -20f, 20f),
                new HandKey(-96f, -0.14f, 0.98f, -100f, 2f),
                new HandKey(-110f, -0.28f, 0.92f, -114f, -8f),
            },
            Off = new[]
            {
                new HandKey(22f, -0.04f, 0.26f),
                new HandKey(96f, -0.14f, 0.98f),
                new HandKey(110f, -0.28f, 0.92f),
            },
            Body = new[]
            {
                new BodyKey(Bend: 18f, Sink: 0.14f),
                new BodyKey(Bend: -9f, Sink: 0.03f),
                new BodyKey(Bend: -4f, Sink: 0.04f),
            },
            Snap = 0.1f,
            Follow = 0.18f,
            Weight = 0.5f,
        };

        /// <summary>Something done to oneself.</summary>
        public static readonly Gesture Gather = new Gesture
        {
            Name = "gather",
            Main = new[]
            {
                new HandKey(-34f, -0.2f, 0.42f, -30f, 40f),
                new HandKey(-22f, 0.08f, 0.4f, -20f, 60f),
                new HandKey(-60f, 0.1f, 0.56f, -56f, 60f),
            },
            Off = new[]
            {
                new HandKey(34f, -0.2f, 0.42f),
                new HandKey(22f, 0.08f, 0.4f),
                new HandKey(60f, 0.1f, 0.56f),
            },
            Body = new[]
            {
                new BodyKey(Bend: 8f, Sink: 0.06f),
                new BodyKey(Bend: -6f, Sink: 0.02f),
                new BodyKey(Bend: -2f, Sink: 0.02f),
            },
            Snap = 0.13f,
            Follow = 0.16f,
            Weight = 0.3f,
        };

        /// <summary>Arms up, for whatever's coming to come.</summary>
        public static readonly Gesture Summon = new Gesture
        {
            Name = "summon",
            Main = new[]
            {
                new HandKey(-54f, -0.1f, 0.5f, -50f, 30f),
                new HandKey(-52f, 0.66f, 0.42f, -50f, 80f),
                new HandKey(-62f, 0.58f, 0.5f, -58f, 72f),
            },
            Off = new[]
            {
                new HandKey(54f, -0.1f, 0.5f),
                new HandKey(52f, 0.66f, 0.42f),
                new HandKey(62f, 0.58f, 0.5f),
            },
            Body = new[]
            {
                new BodyKey(Bend: 7f, Sink: 0.07f),
                new BodyKey(Bend: -11f, Sink: 0.01f),
                new BodyKey(Bend: -8f, Sink: 0.01f),
            },
            Snap = 0.15f,
            Follow = 0.2f,
            Weight = 0.4f,
        };

        /// <summary>Thrown at where it's going: the shoulder leads and the hand comes through.</summary>
        public static readonly Gesture Lunge = new Gesture
        {
            Name = "lunge",
            Main = new[]
            {
                new HandKey(-70f, -0.14f, 0.5f, -36f, 8f),
                new HandKey(-6f, -0.08f, 1.04f, -2f, 0f),
                new HandKey(30f, -0.28f, 0.84f, 66f, -14f),
            },
            Body = new[]
            {
                new BodyKey(Turn: -30f, Bend: 22f, Sink: 0.2f, Push: 0.2f),
                new BodyKey(Turn: 10f, Bend: 24f, Sink: 0.16f, Push: 0.3f, Step: 0.44f),
                new BodyKey(Turn: 26f, Bend: 16f, Sink: 0.1f, Push: 0.2f, Step: 0.44f),
            },
            Snap = 0.08f,
        };

        /// <summary>A quick look over a flask.</summary>
        public static readonly Gesture Drink = new Gesture
        {
            Name = "drink",
            Off = new[]
            {
                new HandKey(40f, -0.2f, 0.5f),
                new HandKey(6f, 0.36f, 0.36f),
                new HandKey(6f, 0.3f, 0.36f),
            },
            Body = new[]
            {
                new BodyKey(),
                new BodyKey(Bend: -9f),
                new BodyKey(Bend: -7f),
            },
            Snap = 0.14f,
            Follow = 0.3f,
            Weight = 0.15f,
        };

        // What fights with its teeth: a body that's all behind its head.
        public static readonly Gesture Bite = new Gesture
        {
            Name = "bite",
            Body = new[]
            {
                new BodyKey(Bend: -10f, Sink: 0.1f, Push: -0.14f, Head: -0.1f),
                new BodyKey(Bend: 14f, Sink: 0.02f, Push: 0.3f, Head: 0.38f),
                new BodyKey(Bend: 6f, Sink: 0.04f, Push: 0.2f, Head: 0.2f),
            },
            Snap = 0.08f,
            Follow = 0.11f,
        };

        /// <summary>Up on the hind legs, and down on whatever's there.</summary>
        public static readonly Gesture Maul = new Gesture
        {
            Name = "maul",
            Body = new[]
            {
                new BodyKey(Bend: -18f, Sink: 0.12f, Push: -0.2f, Head: -0.12f, Rise: 0.62f),
                new BodyKey(Bend: 18f, Sink: 0.05f, Push: 0.32f, Head: 0.32f, Rise: 0.06f),
                new BodyKey(Bend: 8f, Sink: 0.07f, Push: 0.22f, Head: 0.16f, Rise: 0f),
            },
            Snap = 0.14f,
            Follow = 0.16f,
        };

        static Gestures()
        {
            Forehand.Next = Backhand;
            Backhand.Next = Forehand;
        }
    }
}

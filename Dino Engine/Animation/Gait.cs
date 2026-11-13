using OpenTK.Mathematics;

namespace Dino_Engine.Animation
{
    /// <summary>What a foot stands on. It's all that animation asks of a world, so a lab can have a flat floor or a staircase in its place.</summary>
    public interface IGround
    {
        float HeightAt(float x, float z);

        /// <summary>Gets ready to be asked about what's standing around here, and says whether anything is. Most ground has nothing on it.</summary>
        bool LookAround(Vector2 centre, float reach) => false;

        /// <summary>How far something this wide, here, has to go to be clear of what <see cref="LookAround"/> found. False if it's touching nothing.</summary>
        bool TryPushOut(Vector3 at, float radius, out Vector2 push)
        {
            push = default;
            return false;
        }
    }

    /// <summary>A floor with no end, for anything that doesn't care about a world.</summary>
    public sealed class FlatGround : IGround
    {
        public float Level;

        public float HeightAt(float x, float z) => Level;
    }

    /// <summary>One foot. <see cref="Gait"/> is the only thing that moves these.</summary>
    public struct Foot
    {
        /// <summary>Where the sole is in the world, under the ankle. A foot on the ground stays exactly here until it lifts.</summary>
        public Vector3 At;

        /// <summary>Which way it points, in the world.</summary>
        public float Yaw;

        public bool Swinging;

        // The swing it's in.
        public float SwingTime;
        public float SwingLeft;
        public float Height;        // Off the line between where it left and where it's going.

        /// <summary>While it swings, where it's coming down as it will be from the body when it does, in the body's own frame (+X its left, +Z ahead): it hardly changes over a swing, as the foot moves with the body towards it.</summary>
        public Vector3 Lands;
        public float LiftedFrom;
        public float LiftedYaw;

        // How the ground lies where it stands, and how far through standing it is.
        public float Planted;
        public float StandFor;
        public float GroundPitch;
        public float GroundRoll;

        /// <summary>How far toes are above heels, in radians. Under 0 is on tiptoe.</summary>
        public float Pitch;
    }

    /// <summary>What walking needs to be told, each frame, in the world.</summary>
    public struct GaitInput
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Yaw;           // The way the body is turned.
        public float TurnRate;      // Radians a second.
        public float Scale;

        /// <summary>Somewhere to stand other than directly under the hips, off to a side or out in front. It's in the body's own frame, by leg, and for what an attack does with its feet.</summary>
        public float LeadForward;
        public float LeadOut;

        /// <summary>Set while nothing is on the ground at all, as in a dash. Where each foot goes is then up to whoever said so.</summary>
        public bool Suspended;

        /// <summary>How wide to stand, on top of how this person stands anyway.</summary>
        public float Spread;

        /// <summary>Somewhere no foot is to come down, and how far round it: someone standing by a giant's foot. A foot that would come down there lands at the edge instead, and one standing there steps off.</summary>
        public Vector2 KeepOff;
        public float KeepOffRadius;
    }

    /// <summary>
    /// Legs, for any number of them, from nothing but how fast a body is going. A gait is a clock and, for each leg, a duty factor (how much of a cycle a foot is down for) and a step trigger (where in the
    /// cycle it goes): that's how Spore walked creatures nobody had seen before, and how anything with legs is described by those who study it. Speed picks the numbers, by how fast it is for those legs.
    ///
    /// A foot on the ground is fixed to the world and is only ever moved by lifting it, so there's no sliding to clean up: a foot can't slide here at all. If a body runs off and leaves one behind it lifts
    /// early, and when a body stops the feet come back under it a step at a time. Nothing depends on speed changing smoothly, which it never does, as a hero can go from a stand to a run in one tick.
    /// </summary>
    public sealed class Gait
    {
        private const float StartMoving = 0.7f;     // Metres a second.
        private const float StopMoving = 0.3f;
        private const float RunFrom = 0.72f;        // Speed against what legs this long can do, where a walk starts to become a run. A Froude number of about half is where people change over.
        private const float RunBy = 1.02f;
        private const float WalkDuty = 0.64f;
        private const float RunDuty = 0.33f;
        private const float GallopDuty = 0.27f;
        private const float ScuttleWalkDuty = 0.62f;    // On eight legs, four are down at once in a walk, and a little less of the time at a run.
        private const float ScuttleRunDuty = 0.46f;
        private const float WalkLift = 0.1f;        // Of a leg.
        private const float RunLift = 0.22f;
        private const float FarthestAhead = 0.62f;  // Of a leg.
        private const float NarrowestStance = 0.15f;    // Of a leg: however little a leg reaches, a stride is never shorter than this.
        private const float ShortestStand = 0.05f;
        private const float StepTime = 0.24f;       // For a step that isn't part of going anywhere, for legs as long as a person's.
        private const float DriftAllowed = 0.34f;   // Of a leg, before a foot standing still steps back under the body.
        private const float DriftSettling = 0.1f;   // The same, for the moment after stopping.
        private const float SettleFor = 0.7f;
        private const float TurnAllowed = 0.75f;    // Radians between a foot and the body it's on.
        private const float LostAfter = 2.6f;       // Of a leg. Further than that and it's been left in another place altogether.
        private const float Overreach = 1.05f;      // Of what a leg can do with the foot on its toes.
        private const float ToeReach = 0.7f;        // How much of a foot it gets out of rolling over.

        // Steps a second for legs of a given length at a given speed, as measured on people, each scaled out by how long the legs are. Slower than this and it's a shuffle, and the top end levels off, as legs can only go so fast.
        private static readonly (float pace, float cadence)[] Cadences =
        {
            (0f, 0.42f), (0.45f, 0.62f), (0.75f, 0.74f), (1.05f, 0.83f), (1.5f, 0.9f), (2.2f, 0.98f), (3.2f, 1.06f),
        };

        private readonly Rig _rig;
        private readonly MotionStyle _style;
        private readonly Vector2[] _slots;
        private readonly float[] _triggers;
        private readonly bool _quadruped;
        private readonly bool _arachnid;
        private readonly IGround _noGround = new FlatGround();
        private bool _started;
        private float _settling;
        private float _speed;

        // The longest a foot's time on the ground can sweep, in metres at a scale of one, for legs that can't reach far either side of where they stand (MotionStyle.StrideDip), or 0 for no limit.
        private readonly float _widest;

        public readonly Foot[] Feet;

        /// <summary>Where in the cycle things are.</summary>
        public float Phase { get; private set; }

        /// <summary>How far from a walk to a run, 0 to 1.</summary>
        public float Run { get; private set; }

        /// <summary>How much of a cycle a foot is down for, as it stands.</summary>
        public float Duty { get; private set; } = WalkDuty;

        public bool Moving { get; private set; }

        /// <summary>Cycles a second.</summary>
        public float Rate { get; private set; }

        /// <summary>A bit for each foot that has come down this frame, for whatever wants to hear it.</summary>
        public int Landed { get; private set; }

        public Gait(Rig rig, MotionStyle style)
        {
            _rig = rig;
            _style = style;
            _quadruped = rig.Plan == BodyPlan.Quadruped;
            _arachnid = rig.Plan == BodyPlan.Arachnid;
            Feet = new Foot[rig.Legs.Length];
            _slots = new Vector2[rig.Legs.Length];
            _triggers = new float[rig.Legs.Length];
            for (int i = 0; i < rig.Legs.Length; i++)
            {
                // A foot stands under its hip, but for what walks on eight, whose feet stand far out round it where its legs reach, as it was built.
                Vector3 hip = rig.Pivots[(int)(_arachnid ? rig.Legs[i].End : rig.Legs[i].Upper)];
                _slots[i] = new Vector2(hip.X, hip.Z);
            }

            // A leg reaches only so far either side of its hip with the hips let down by StrideDip, and a stance can't sweep further than the leg that reaches least, landing ahead and leaving
            // behind in the shares the style says.
            if (style.StrideDip > 0f)
            {
                float lead = MathF.Max(style.LandAhead, 1f - style.LandAhead);
                float widest = float.MaxValue;
                foreach (Limb limb in rig.Legs)
                {
                    float height = rig.Pivots[(int)limb.Upper].Y - rig.AnkleHeight - style.StrideDip * rig.LegLength;
                    float length = limb.Length * Reach.Comfortable;
                    widest = MathF.Min(widest, MathF.Sqrt(MathF.Max(length * length - height * height, 0f)) / lead);
                }
                _widest = MathF.Max(widest, NarrowestStance * rig.LegLength);
            }
        }

        /// <summary>A body that has just arrived, or been put somewhere: everything starts over, standing where it should.</summary>
        public void Stand(in GaitInput input, IGround ground)
        {
            Moving = false;
            Run = 0f;
            Landed = 0;
            for (int i = 0; i < Feet.Length; i++)
            {
                Vector3 at = KeptOff(Spot(i, input.Position, input.Yaw, input), input);
                at.Y = ground.HeightAt(at.X, at.Z);
                Feet[i] = new Foot { At = at, Yaw = input.Yaw, Planted = 1f, StandFor = 1f };
                ReadGround(ref Feet[i], ground, input.Scale);
            }
            _started = true;
        }

        public void Step(in GaitInput input, float deltaTime, IGround ground)
        {
            if (!_started) Stand(input, ground);
            Landed = 0;

            float scale = input.Scale;
            float leg = _rig.LegLength * scale;
            float speed = input.Velocity.Xz.Length;
            _speed = speed;

            if (input.Suspended)
            {
                _settling = SettleFor;
                return;
            }

            // Whatever has happened, a foot that's been left a long way behind isn't on the end of this leg any more. How far that is goes by how far the leg reaches, as well as how high the
            // hips are: what walks on eight stands its feet further out than its hips are high, and was put back on its feet every frame.
            for (int i = 0; i < Feet.Length; i++)
            {
                float lost = LostAfter * MathF.Max(leg, _rig.Legs[i].Length * scale);
                if ((Feet[i].At.Xz - HipOf(i, input).Xz).LengthSquared > lost * lost)
                {
                    Stand(input, ground);
                    return;
                }
            }

            float pace = speed / MathF.Sqrt(Rig.Gravity * leg);
            float run = Smooth(RunFrom, RunBy, pace);
            Run = Spring.Damp(Run, run, 0.09f, deltaTime);

            bool wasMoving = Moving;
            Moving = Moving ? speed > StopMoving : speed > StartMoving;
            if (wasMoving && !Moving) _settling = SettleFor;
            _settling = MathF.Max(_settling - deltaTime, 0f);

            Duty = _arachnid ? Lerp(ScuttleWalkDuty, ScuttleRunDuty, Run) : _quadruped ? Lerp(WalkDuty, Lerp(RunDuty, GallopDuty, _style.Gallop), Run) : Lerp(WalkDuty, RunDuty, Run);
            ReadTriggers();

            float stepsASecond = CadenceAt(pace) * MathF.Sqrt(Rig.Gravity / leg) * _style.Cadence;

            // Legs that can't reach far go quicker rather than longer, so that a foot's time on the ground never sweeps further than they reach.
            if (_widest > 0f) stepsASecond = MathF.Max(stepsASecond, 2f * speed * Duty / (_widest * scale));
            Rate = Spring.Damp(Rate, stepsASecond * 0.5f, 0.05f, deltaTime);

            if (Moving)
            {
                if (!wasMoving) StartOff(input);
                Phase = (Phase + Rate * deltaTime) % 1f;
            }

            for (int i = 0; i < Feet.Length; i++)
            {
                ref Foot foot = ref Feet[i];
                if (foot.Swinging) Swing(i, ref foot, input, leg, deltaTime, ground);
                else
                {
                    foot.Planted += deltaTime;
                    if (Moving) WalkOn(i, ref foot, input, leg);
                }
            }

            if (!Moving) StandStill(input, leg);
        }

        // Going from a stand, the first step is taken by whichever foot has the most to gain from it.
        private void StartOff(in GaitInput input)
        {
            Vector2 way = input.Velocity.Xz;
            if (way.LengthSquared < 1e-4f) return;
            way.Normalize();

            int first = 0;
            float behind = float.MaxValue;
            for (int i = 0; i < Feet.Length; i++)
            {
                float along = Vector2.Dot(Feet[i].At.Xz - input.Position.Xz, way);
                if (along >= behind) continue;
                behind = along;
                first = i;
            }

            // So that this foot's next moment is the one it lifts at.
            Phase = (_triggers[first] + Duty) % 1f;
        }

        private void WalkOn(int i, ref Foot foot, in GaitInput input, float leg)
        {
            float phase = LegPhase(i);
            foot.StandFor = Duty / MathF.Max(Rate, 0.05f);

            bool due = phase >= Duty && foot.Planted >= ShortestStand;

            // However the clock stands, a foot can't be on the ground further away than the leg is long.
            float reach = (_rig.Legs[i].Length + _rig.AnkleHeight + _rig.FootForward * ToeReach) * input.Scale * Overreach;
            Vector3 hip = HipOf(i, input);
            bool stretched = (hip - foot.At).LengthSquared > reach * reach;
            if (due || stretched) Lift(ref foot, MathF.Max((1f - phase) / MathF.Max(Rate, 0.05f), 0.05f));
        }

        private void StandStill(in GaitInput input, float leg)
        {
            for (int i = 0; i < Feet.Length; i++)
            {
                if (Feet[i].Swinging) return;
            }

            float allowed = (_settling > 0f ? DriftSettling : DriftAllowed) * leg;
            int worst = -1;
            float furthest = allowed;
            for (int i = 0; i < Feet.Length; i++)
            {
                ref Foot foot = ref Feet[i];
                if (foot.Planted < ShortestStand * 2f) continue;

                Vector3 home = KeptOff(Spot(i, input.Position, input.Yaw, input), input);
                float drift = (home.Xz - foot.At.Xz).Length;
                float turned = MathF.Abs(Spring.Shortest(input.Yaw - foot.Yaw));
                if (turned > TurnAllowed) drift = MathF.Max(drift, allowed + turned);

                // A foot standing where no foot is to come down steps off it, however little it's drifted.
                if (input.KeepOffRadius > 0f && (foot.At.Xz - input.KeepOff).LengthSquared < input.KeepOffRadius * input.KeepOffRadius) drift = MathF.Max(drift, allowed + input.KeepOffRadius);
                if (drift <= furthest) continue;
                furthest = drift;
                worst = i;
            }
            if (worst < 0) return;

            ref Foot stepping = ref Feet[worst];
            stepping.StandFor = 1f;
            Lift(ref stepping, StepTime * MathF.Sqrt(leg / 2f));
        }

        private static void Lift(ref Foot foot, float time)
        {
            foot.Swinging = true;
            foot.SwingTime = time;
            foot.SwingLeft = time;
            foot.LiftedFrom = foot.At.Y;
            foot.LiftedYaw = foot.Yaw;
            foot.Planted = 0f;
        }

        private void Swing(int i, ref Foot foot, in GaitInput input, float leg, float deltaTime, IGround ground)
        {
            float before = Math.Clamp(1f - foot.SwingLeft / foot.SwingTime, 0f, 1f);

            // The clock says when it comes down, while there is one. It follows every change in speed, and stays in step with the other feet.
            float left = foot.SwingLeft - deltaTime;
            if (Moving)
            {
                float phase = LegPhase(i);
                if (phase >= Duty - 0.02f) left = (1f - phase) / MathF.Max(Rate, 0.05f);
            }
            foot.SwingLeft = left;

            // A clock that slows down makes what's left longer than the whole of what there was thought to be.
            if (left > foot.SwingTime) foot.SwingTime = left / MathF.Max(1f - before, 0.05f);
            float after = Math.Clamp(1f - left / foot.SwingTime, before, 1f);
            Vector3 landing = Landing(i, input, leg, MathF.Max(left, 0f), ground);

            // Where it lands from where the body will be then, on the ground the body will be on.
            float then = MathF.Max(left, 0f);
            Vector2 body = input.Position.Xz + input.Velocity.Xz * then;
            float below = input.Position.Y + ground.HeightAt(body.X, body.Y) - ground.HeightAt(input.Position.X, input.Position.Z);
            foot.Lands = Vector3.Transform(new Vector3(landing.X - body.X, landing.Y - below, landing.Z - body.Y), Frames.Yaw(-(input.Yaw + input.TurnRate * then)));

            // Each step goes the share of what's left that the curve says, so a foot can change its mind about where it's going at any moment and never jump.
            float eased = Ease(before);
            float share = eased >= 1f ? 1f : Math.Clamp((Ease(after) - eased) / (1f - eased), 0f, 1f);
            foot.At.X += (landing.X - foot.At.X) * share;
            foot.At.Z += (landing.Z - foot.At.Z) * share;

            float fullYaw = Spring.Shortest(input.Yaw - foot.LiftedYaw);
            foot.Yaw = foot.LiftedYaw + fullYaw * Ease(after);

            float clearance = Lerp(WalkLift, RunLift, Run) * leg * MathF.Min(foot.SwingTime / 0.16f, 1f);
            float bent = MathF.Pow(after, 1f - 0.4f * Run);
            float rise = MathF.Sin(MathF.PI * bent);
            float line = Lerp(foot.LiftedFrom, landing.Y, Ease(after));
            foot.Height = clearance * rise;
            foot.At.Y = line + foot.Height;

            // Toes down leaving, toes up arriving.
            foot.Pitch = Lerp(-0.9f - 0.5f * Run, 0.32f - 0.25f * Run, Smooth(0.1f, 0.9f, after));

            if (left > 0f) return;

            foot.Swinging = false;
            foot.At = landing;
            foot.Yaw = input.Yaw;
            foot.Height = 0f;
            foot.Planted = 0f;
            foot.StandFor = Moving ? Duty / MathF.Max(Rate, 0.05f) : 1f;
            ReadGround(ref foot, ground, input.Scale);
            Landed |= 1 << i;
        }

        // Where a foot should come down: under where the hips will be, and a little in front of them by however much of its time on the ground it spends going past them.
        private Vector3 Landing(int i, in GaitInput input, float leg, float left, IGround ground)
        {
            float standing = Moving ? Duty / MathF.Max(Rate, 0.05f) : 0f;
            float landAhead = _style.LandAhead;
            Vector2 ahead = input.Velocity.Xz * (standing * landAhead);
            float most = FarthestAhead * leg;
            if (ahead.LengthSquared > most * most) ahead *= most / ahead.Length;

            Vector2 hips = input.Position.Xz + input.Velocity.Xz * left;
            float yaw = input.Yaw + input.TurnRate * (left + standing * landAhead);
            Vector3 spot = KeptOff(Spot(i, new Vector3(hips.X, input.Position.Y, hips.Y), yaw, input) + new Vector3(ahead.X, 0f, ahead.Y), input);
            spot.Y = ground.HeightAt(spot.X, spot.Z);
            return spot;
        }

        // Out of where no foot is to come down, to its edge, on the side it was already on, or away from the hips if it's right in the middle.
        private static Vector3 KeptOff(Vector3 spot, in GaitInput input)
        {
            if (input.KeepOffRadius <= 0f) return spot;
            Vector2 off = spot.Xz - input.KeepOff;
            float distance = off.Length;
            if (distance >= input.KeepOffRadius) return spot;
            Vector2 away = input.KeepOff - input.Position.Xz;
            Vector2 way = distance > 1e-3f ? off / distance : away.LengthSquared > 1e-6f ? -away.Normalized() : Vector2.UnitX;
            Vector2 edge = input.KeepOff + way * input.KeepOffRadius;
            return new Vector3(edge.X, spot.Y, edge.Y);
        }

        /// <summary>Where a foot stands in the world, for a body somewhere.</summary>
        private Vector3 Spot(int i, Vector3 position, float yaw, in GaitInput input)
        {
            Vector2 slot = _slots[i];

            // At a stand a person has one foot a little ahead. On the move the feet come in under the body, which is where the weight is.
            float narrowing = 1f - 0.55f * Run * Smooth(0.5f, 3f, _speed);
            float width = slot.X * _style.Stance * (1f + input.Spread) * narrowing;
            float forward = slot.Y;
            if (_rig.Plan == BodyPlan.Biped)
            {
                bool leading = _rig.Legs[i].Side < 0f;
                forward += (Moving ? 0f : (leading ? 0.1f : -0.1f) * (1f + input.Spread * 2f)) + (leading ? input.LeadForward : -input.LeadForward * 0.3f);
                width += _rig.Legs[i].Side * (leading ? input.LeadOut : 0f);
            }
            return position + (Frames.Left(yaw) * width + Frames.Forward(yaw) * forward) * input.Scale;
        }

        private Vector3 HipOf(int i, in GaitInput input)
        {
            Vector3 pivot = _rig.Pivots[(int)_rig.Legs[i].Upper];
            return input.Position + (Frames.Left(input.Yaw) * pivot.X + Frames.Forward(input.Yaw) * pivot.Z + Vector3.UnitY * pivot.Y) * input.Scale;
        }

        // How the ground slopes under a foot, across and along it, so that it lies flat on what it's on.
        private void ReadGround(ref Foot foot, IGround ground, float scale)
        {
            Vector3 forward = Frames.Forward(foot.Yaw);
            Vector3 left = Frames.Left(foot.Yaw);
            float reach = (_rig.FootForward + _rig.FootBack) * 0.5f * scale;
            float toes = ground.HeightAt(foot.At.X + forward.X * reach, foot.At.Z + forward.Z * reach);
            float heel = ground.HeightAt(foot.At.X - forward.X * reach, foot.At.Z - forward.Z * reach);
            float l = ground.HeightAt(foot.At.X + left.X * reach, foot.At.Z + left.Z * reach);
            float r = ground.HeightAt(foot.At.X - left.X * reach, foot.At.Z - left.Z * reach);
            foot.GroundPitch = Math.Clamp(MathF.Atan2(toes - heel, reach * 2f), -0.7f, 0.7f);
            foot.GroundRoll = Math.Clamp(MathF.Atan2(l - r, reach * 2f), -0.5f, 0.5f);
        }

        /// <summary>Puts a foot somewhere for as long as nothing is on the ground. It carries on from there when something is.</summary>
        public void Carry(int i, Vector3 at, float yaw, float pitch)
        {
            ref Foot foot = ref Feet[i];
            foot.At = at;
            foot.Yaw = yaw;
            foot.Pitch = pitch;
            foot.Swinging = true;
            foot.SwingTime = 0.12f;
            foot.SwingLeft = 0.12f;
            foot.LiftedFrom = at.Y;
            foot.LiftedYaw = yaw;
            foot.Height = 0f;
        }

        // Where in its own cycle a leg is, with 0 the moment it comes down.
        private float LegPhase(int i)
        {
            float phase = Phase - _triggers[i];
            return phase - MathF.Floor(phase);
        }

        // Two legs are always half a cycle apart. Four go from a walk, one at a time round the body, through a trot in diagonal pairs, to a gallop, pair after pair with one a little ahead.
        private void ReadTriggers()
        {
            if (_arachnid)
            {
                // Eight legs go in two sets of four, each the other's opposite, a front and a third leg on one side with a second and a hind on the other, as a spider walks: so there are always
                // four down in a box round the body. Within a set the hind legs go a little first and the front last, a wave running forward. Legs are hind, fore, second and third, a pair each.
                _triggers[0] = 0.5f;            // Hind left, with the right side's front and third.
                _triggers[1] = 0f;
                _triggers[2] = 0.0f + Metachrony * 3f;
                _triggers[3] = 0.5f + Metachrony * 3f;
                _triggers[4] = 0.5f + Metachrony * 2f;
                _triggers[5] = 0.0f + Metachrony * 2f;
                _triggers[6] = 0.0f + Metachrony;
                _triggers[7] = 0.5f + Metachrony;
                return;
            }
            if (!_quadruped)
            {
                _triggers[0] = 0f;
                _triggers[1] = 0.5f;
                return;
            }

            // Something too heavy to gallop goes on trotting, however fast it's going.
            float trot = Smooth(0f, 0.5f, Run);
            float gallop = Smooth(0.6f, 1f, Run) * _style.Gallop;
            _triggers[0] = 0f;
            _triggers[1] = Lerp(0.5f, 0.1f, gallop);
            _triggers[2] = Lerp(Lerp(0.25f, 0.5f, trot), 0.5f, gallop);
            _triggers[3] = Lerp(Lerp(0.75f, 1f, trot), 0.6f, gallop);
        }

        private const float Metachrony = 0.035f;   // Of a cycle, between one pair of eight legs and the next pair forward within a set of four.

        private static float CadenceAt(float pace)
        {
            for (int i = 1; i < Cadences.Length; i++)
            {
                if (pace > Cadences[i].pace) continue;
                float share = (pace - Cadences[i - 1].pace) / (Cadences[i].pace - Cadences[i - 1].pace);
                return Lerp(Cadences[i - 1].cadence, Cadences[i].cadence, share);
            }
            return Cadences[^1].cadence;
        }

        // A foot leaves slowly, goes through quickly and sets down gently.
        private static float Ease(float t) => t * t * (3f - 2f * t);

        internal static float Smooth(float from, float to, float value)
        {
            float t = Math.Clamp((value - from) / (to - from), 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        internal static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}

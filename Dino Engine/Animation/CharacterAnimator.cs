using Dino_Engine.ECS.Components;
using OpenTK.Mathematics;

namespace Dino_Engine.Animation
{
    /// <summary>What a body is doing that it has to be told, as opposed to what it can see for itself. Everything about time comes from outside, as the game's rules decide when a blow lands, never the animation.</summary>
    public struct ActionInput
    {
        public Gesture? Gesture;
        public float Elapsed;   // Seconds since it began.
        public float Impact;    // Seconds in that it lands.
        public float Length;    // Seconds it takes in all.
        public float Aim;       // Which way it goes, as a yaw in the world.
        public int Serial;      // Different for each one, so that two of the same in a row are seen to be two.
    }

    /// <summary>All of what a body is told, each frame. Position and yaw are the truth of where it is: this reads how fast it's going off those, as nothing else is sent.</summary>
    public struct AnimationInput
    {
        public Vector3 Position;
        public float Yaw;
        public float Scale;
        public ActionInput Action;

        /// <summary>How fast life is going for them: below 1 when chilled, and 0 when frozen solid.</summary>
        public float Rate;

        /// <summary>Between 0, at their ease, and 1, in a fight.</summary>
        public float Alert;

        /// <summary>A little reeling about, for the stunned.</summary>
        public bool Dazed;

        /// <summary>Where to look, if anywhere.</summary>
        public Vector3 LookAt;
        public bool Looking;

        /// <summary>Which way a dodge is going, as long as it lasts. Zero when there isn't one.</summary>
        public Vector2 Dash;

        public static AnimationInput At(Vector3 position, float yaw, float scale = 1f) => new AnimationInput { Position = position, Yaw = yaw, Scale = scale, Rate = 1f };
    }

    /// <summary>
    /// Makes a body move, every frame, with nothing recorded and nothing to play. What it's given is where the body is and what it's been told to do, and everything it does comes from those through the rules of how bodies
    /// work: how long legs are and how fast they can go, where the feet are, what the weight is doing. So it copes with whatever a game does to it, at any speed and on any ground, and needs no new work for a new creature.
    ///
    /// A frame goes from the ground up. The feet are where <see cref="Gait"/> has them. The hips go wherever those allow. The back turns and leans from the hips, the hands go where they're wanted from the chest, and
    /// <see cref="Reach"/> finds the joints between. The rules of the game never wait on any of this: a blow lands when it lands, and the body gets there.
    /// </summary>
    public sealed class CharacterAnimator
    {
        private const float MostStep = 12f;             // Metres in one frame. Further than this isn't walking, and is put down to having been moved.
        private const float MoveHalfLife = 0.045f;
        private const float StepHeightHalfLife = 0.04f;
        private const float BlendHalfLife = 0.035f;     // How quickly a jump between poses is got over: a pose is new almost at once, and never all at once.
        private const float FastestHand = 120f;         // Metres a second. Nothing a hand is meant to do goes faster than this, so anything that does is a jump, and is smoothed away.
        private const float FastestBody = 60f;
        private const float FastestTurn = 40f;          // Radians a second.
        private const float FastestGrip = 70f;
        private const float LookLimit = 1.2f;
        private const float HardestLean = 0.45f;
        private const float BounceHeight = 0.05f;       // Of a leg.
        private const float SwayShare = 0.3f;
        private const float HipTurnFromStride = 0.2f;
        private const float ChestTurnFromStride = 0.3f;
        private const float RunLean = 0.14f;
        private const float BreathRate = 1.45f;
        private const float ChainWithin = 0.4f;         // Seconds. Any longer and the hand has gone back to where it was.
        private const float StruckFor = 0.3f;

        private readonly MotionStyle _style;
        private readonly Gait _gait;
        private readonly int _seed;
        private readonly Vector3[] _joints = new Vector3[Rig.BoneCount];
        private readonly Quaternion[] _rotations = new Quaternion[Rig.BoneCount];
        private readonly Vector3[] _ankles;
        private readonly Quaternion[] _soles;
        private readonly Quaternion[] _toes;
        private readonly TurnContinuity[] _toeTurn;
        private readonly Continuity[] _footMotion;
        private readonly Continuity[] _handMotion;
        private readonly Quaternion[] _gripRests;
        private readonly Vector3[] _gripAxes;
        private readonly TurnContinuity[] _gripTurn;

        private Carry _carry = Carry.Nothing;
        private bool _shield;
        private bool _seen;
        private Vector3 _position;
        private float _yaw;
        private Vector3 _velocity;
        private Vector3 _acceleration;
        private float _turnRate;
        private float _hipTurn;
        private float _hipHeight;
        private float _hipHeightSpeed;
        private float _bounce;
        private float _sway;
        private float _dazed;
        private float _stride;
        private float _clock;
        private float _lookYaw;
        private float _lookPitch;
        private float _alert;
        private int _serial = -1;
        private bool _acting;
        private Gesture? _playing;
        private float _sinceActing = float.MaxValue;
        private bool _airborne;
        private float _aim;

        private Continuity _pelvisMotion;
        private TurnContinuity _pelvisTurn;
        private TurnContinuity _spineTurn;
        private TurnContinuity _chestTurn;
        private TurnContinuity _headTurn;

        // What's been done to them. Each of these rings and dies away on top of everything else.
        private Vector3 _kick;
        private Vector3 _kickSpeed;
        private Vector3 _lurch;
        private Vector3 _lurchSpeed;
        private float _shake;
        private float _held;
        private float _lost;
        private float _sinceStruck = float.MaxValue;

        // Once nobody's in it.
        private Ragdoll? _ragdoll;
        private float _sunk;
        private float _deadFor;
        private readonly Vector3[] _lastJoints = new Vector3[Rig.BoneCount];
        private readonly Vector3[] _worldJoints = new Vector3[Rig.BoneCount];
        private readonly Quaternion[] _worldRotations = new Quaternion[Rig.BoneCount];
        private float _lastFrame = 1f / 60f;

        public readonly Rig Rig;
        public readonly Matrix4[] Skinning = new Matrix4[Rig.BoneCount];

        /// <summary>What the game wants of this body, for it to read each frame. It's left as it was until someone says otherwise.</summary>
        public AnimationInput Orders = new AnimationInput { Rate = 1f, Scale = 1f };

        /// <summary>A bit for each foot that came down this frame. Sounds are read off this, so that what's heard can't drift from what's seen.</summary>
        public int Footfalls => _gait.Landed;

        public Gait Gait => _gait;

        /// <summary>How bright a blow has left them, from 1 down to nothing, and in what.</summary>
        /// <summary>What's drawn round them: a colour, and nothing at all with none in its fourth part.</summary>
        public Vector4 Outline;

        public float Flash { get; private set; }
        public Vector3 FlashColour { get; private set; } = Vector3.One;

        /// <summary>Whether a blow is going through, from the moment it leaves to the end of its swing.</summary>
        public bool Swinging { get; private set; }

        /// <summary>Whether something's just landed on them, and so needn't be shown again by word that it did.</summary>
        public bool JustStruck => _sinceStruck < StruckFor;

        /// <summary>Where a held weapon starts and ends in the world, for whatever draws what it leaves in the air.</summary>
        public Vector3 WeaponBase { get; private set; }
        public Vector3 WeaponTip { get; private set; }

        public Vector3 Position => _position;

        /// <summary>How many times this body has had to start over because of a number that wasn't one. Anything but 0 is a bug.</summary>
        public int Faults { get; private set; }

        /// <summary>What they have in their hands.</summary>
        public Carry Carrying => _carry;

        public CharacterAnimator(Rig rig, MotionStyle style, int seed)
        {
            Rig = rig;
            _seed = seed;
            _style = style.Individual(seed);
            _gait = new Gait(rig, _style);
            _ankles = new Vector3[rig.Legs.Length];
            _soles = new Quaternion[rig.Legs.Length];
            _toes = new Quaternion[rig.Legs.Length];
            _toeTurn = new TurnContinuity[rig.Legs.Length];
            _footMotion = new Continuity[rig.Legs.Length];
            _handMotion = new Continuity[rig.Arms.Length];
            _gripRests = new Quaternion[rig.Arms.Length];
            _gripAxes = new Vector3[rig.Arms.Length];
            _gripTurn = new TurnContinuity[rig.Arms.Length];
            _clock = (seed & 1023) * 0.37f;

            Array.Copy(rig.Pivots, _joints, Rig.BoneCount);
            Array.Fill(_rotations, Quaternion.Identity);
            for (int i = 0; i < Skinning.Length; i++) Skinning[i] = Matrix4.Identity;
            ReadGrips();
        }

        /// <summary>What they have in their hands, and whether there's a shield on the other arm.</summary>
        public void Hold(Carry carry, bool shield)
        {
            _carry = carry;
            _shield = shield;
            ReadGrips();
        }

        // How what's held lies in the hand it's in, as it was built: a weapon points out of the fist, a bow stands across it, and a shield looks out of the back of the hand. What's told to a hand as a blade is this.
        private void ReadGrips()
        {
            for (int i = 0; i < Rig.Arms.Length; i++)
            {
                Limb arm = Rig.Arms[i];
                Vector3 along = (Rig.Pivots[(int)arm.End] - Rig.Pivots[(int)arm.Lower]).Normalized();
                Vector3 axis = i == _carry.Arm ? (_carry == Carry.Bow ? Vector3.UnitY : Vector3.UnitZ) : _carry.TwoHanded ? Vector3.UnitZ : _shield ? Vector3.UnitY : Vector3.UnitZ;
                _gripRests[i] = Quaternion.Invert(Frames.Of(axis, Frames.Across(along, axis, Vector3.UnitX)));
                _gripAxes[i] = axis;
            }
        }

        /// <summary>Anything that was going on is over, and they're standing here.</summary>
        public void Settle(in AnimationInput input, IGround ground)
        {
            _position = input.Position;
            _yaw = input.Yaw;
            _velocity = Vector3.Zero;
            _acceleration = Vector3.Zero;
            _turnRate = 0f;
            _hipTurn = 0f;
            _hipHeight = Rig.HipHeight;
            _hipHeightSpeed = 0f;
            _seen = true;

            _pelvisMotion.Reset();
            _pelvisTurn.Reset();
            _spineTurn.Reset();
            _chestTurn.Reset();
            _headTurn.Reset();
            for (int i = 0; i < _footMotion.Length; i++) _footMotion[i].Reset();
            for (int i = 0; i < _toeTurn.Length; i++) _toeTurn[i].Reset();
            for (int i = 0; i < _handMotion.Length; i++) _handMotion[i].Reset();
            for (int i = 0; i < _gripTurn.Length; i++) _gripTurn[i].Reset();
            _neckTurn.Reset();
            _neckTopTurn.Reset();
            Array.Clear(_tailYaw);
            Array.Clear(_tailYawSpeed);
            Array.Clear(_tailPitch);
            Array.Clear(_tailPitchSpeed);

            _gait.Stand(Walking(input, 0f, 0f, false, 0f), ground);
        }

        private GaitInput Walking(in AnimationInput input, float forward, float spin, bool suspended, float alert) => new GaitInput
        {
            Position = input.Position,
            Velocity = _velocity,
            Yaw = input.Yaw + _hipTurn * 0.5f + spin,
            TurnRate = _turnRate,
            Scale = input.Scale,
            LeadForward = forward,
            Suspended = suspended,
            Spread = alert * 0.3f,
        };

        /// <summary>A blow landing on them, going the way it's going in the world. 0 is a scratch and 1 a good hard hit: past that it's all the same to a body.</summary>
        /// <param name="held">Seconds to stay stopped for, as someone struck is: the blow's own weight, shown.</param>
        /// <param name="colour">What it shows as, going through them.</param>
        public void Struck(Vector3 direction, float strength, float held, Vector3 colour)
        {
            _sinceStruck = 0f;
            FlashColour = colour;
            float give = MathF.Min(strength, 1.6f) * _style.Give;
            Quaternion toBody = Frames.Yaw(-_yaw);
            Vector3 way = Vector3.Transform(new Vector3(direction.X, 0f, direction.Z), toBody);
            if (way.LengthSquared > 1e-6f) way.Normalize();

            _lurchSpeed += way * (give * 7f);
            _kickSpeed += Vector3.Cross(Vector3.UnitY, way) * (give * 15f);
            Flash = MathF.Max(Flash, MathF.Min(0.3f + strength * 0.4f, 0.72f));
            _shake = MathF.Max(_shake, give);
            _held = MathF.Max(_held, held);
        }

        /// <summary>Holds a blow where it lands for a moment, for whoever's giving it. The game goes on regardless, and the body makes up the time.</summary>
        public void Connected(float held) => _held = MathF.Max(_held, held);

        /// <summary>
        /// Whoever this was has gone, and what's left falls down. It starts from exactly the pose they were in and goes on as they were going, so nothing jumps. <paramref name="push"/> is metres a second, in the world:
        /// next to nothing just lets them drop, and a blow that was far too much for them sends them flying.
        /// </summary>
        public void Die(Vector3 push)
        {
            if (_ragdoll != null) return;

            Quaternion toWorld = Frames.Yaw(_yaw);
            float scale = Orders.Scale;
            for (int i = 0; i < Rig.BoneCount; i++)
            {
                _worldJoints[i] = _position + Vector3.Transform(_joints[i], toWorld) * scale;
                _worldRotations[i] = toWorld * _rotations[i];
            }
            if (!_steppedOnce) Array.Copy(_worldJoints, _lastJoints, Rig.BoneCount);
            _ragdoll = new Ragdoll(Rig, scale, _worldJoints, _lastJoints, _worldRotations, _lastFrame, push);
            _deadFor = 0f;
            _sunk = 0f;
        }

        /// <summary>Whoever this is, they're back.</summary>
        public void Revive()
        {
            _ragdoll = null;
            _seen = false;
        }

        public bool Dead => _ragdoll != null;

        /// <summary>Dead and done moving, and costing nothing.</summary>
        public bool Resting => _ragdoll is { Asleep: true };

        /// <summary>Lets go of what's left, down into the ground, over the last moments it's there for.</summary>
        public float Sink
        {
            get => _sunk;
            set => _sunk = value;
        }

        private bool _steppedOnce;
        private bool _posed;

        private const float FrozenShine = 0.16f;
        private static readonly Vector3 FrozenColour = new Vector3(0.35f, 0.72f, 1f);

        private void StepDead(in AnimationInput input, float deltaTime, IGround ground)
        {
            _deadFor += deltaTime;
            Flash = Spring.Damp(Flash, 0f, 0.05f, deltaTime);
            Swinging = false;
            _ragdoll!.Step(deltaTime, ground);
            _ragdoll.Pose(input.Position, input.Yaw, input.Scale, _sunk, _joints, _rotations);
            WriteSkin();
        }

        public void Step(in AnimationInput input, float deltaTime, IGround ground)
        {
            if (_ragdoll != null)
            {
                StepDead(input, deltaTime, ground);
                return;
            }

            if (!_seen) Settle(input, ground);
            if ((input.Position - _position).Xz.LengthSquared > MostStep * MostStep) Settle(input, ground);
            if (deltaTime <= 0f) return;

            // Where everything was, for the moment this body falls, as what's moving when it goes keeps moving.
            Quaternion wasTurned = Frames.Yaw(_yaw);
            float wasScale = input.Scale;
            for (int i = 0; i < Rig.BoneCount; i++) _lastJoints[i] = _position + Vector3.Transform(_joints[i], wasTurned) * wasScale;
            _lastFrame = deltaTime;
            _steppedOnce = true;

            float life = deltaTime * input.Rate;
            ReadMotion(input, deltaTime);
            Flash = Spring.Damp(Flash, 0f, 0.05f, deltaTime);
            _sinceStruck += deltaTime;

            if (input.Rate <= 0f)
            {
                // Frozen, and everything stops where it is. Only what was already happening to the ice goes on, and it has to be plain that it's ice.
                if (Flash < FrozenShine)
                {
                    Flash = FrozenShine;
                    FlashColour = FrozenColour;
                }

                // Someone first seen like this has no way of standing to stop in, so they're given one.
                if (_posed)
                {
                    Solve(input);
                    return;
                }
                life = deltaTime;
            }
            _posed = true;

            _clock += life;
            _alert = Spring.Damp(_alert, input.Alert, 0.14f, deltaTime);
            ReadAction(input, deltaTime, out GestureSample gesture, out bool transition);

            bool dashing = input.Dash.LengthSquared > 1e-4f;
            bool airborne = dashing || (gesture.Airborne && gesture.Weight > 0.2f);
            if (airborne != _airborne) transition = true;
            _airborne = airborne;

            float spin = MathHelper.DegreesToRadians(gesture.Body.Spin) * gesture.Weight;
            ReadFacing(deltaTime, life);
            _gait.Step(Walking(input, gesture.Body.Step * Rig.LegLength * gesture.Weight, spin, airborne, _alert), life, ground);
            if (airborne) CarryFeet(input, spin, dashing);

            ReadFeet(input, life);

            if (Rig.Saurian && Rig.Plan == BodyPlan.Biped) PoseSaurian(input, gesture, spin, life, deltaTime, transition);
            else if (Rig.Plan == BodyPlan.Biped) PoseBiped(input, gesture, spin, dashing, life, deltaTime, transition);
            else PoseQuadruped(input, gesture, life, deltaTime, transition);

            Solve(input);
        }

        private void ReadMotion(in AnimationInput input, float deltaTime)
        {
            Vector3 seen = (input.Position - _position) / deltaTime;
            seen.Y = 0f;

            Vector3 last = _velocity;
            _velocity = Spring.Damp(_velocity, seen, MoveHalfLife, deltaTime);
            _acceleration = Spring.Damp(_acceleration, (_velocity - last) / deltaTime, 0.07f, deltaTime);
            _turnRate = Spring.Damp(_turnRate, Spring.Shortest(input.Yaw - _yaw) / deltaTime, 0.08f, deltaTime);
            _position = input.Position;
            _yaw = input.Yaw;
        }

        private void ReadAction(in AnimationInput input, float deltaTime, out GestureSample gesture, out bool transition)
        {
            gesture = default;
            transition = false;

            ActionInput action = input.Action;
            bool acting = action.Gesture != null && action.Elapsed < action.Length;
            if (acting != _acting || (acting && action.Serial != _serial))
            {
                transition = true;
                _serial = action.Serial;
                _lost = 0f;

                // A blow that follows hard on the last one takes up from where that one left the hand.
                if (acting)
                {
                    bool follows = _sinceActing < ChainWithin && _playing?.Next != null && action.Gesture!.Next != null;
                    _playing = follows ? _playing!.Next : action.Gesture;
                }
            }
            _acting = acting;
            _sinceActing = acting ? 0f : _sinceActing + deltaTime;

            // A blow held where it landed takes that time out of the rest of the swing, so the body is back in step by the time it's over.
            _held = MathF.Max(_held - deltaTime, 0f);
            if (acting)
            {
                _lost = _held > 0f ? _lost + deltaTime : MathF.Max(_lost - deltaTime * 1.5f, 0f);
                gesture = GesturePlayer.Sample(_playing ?? action.Gesture!, MathF.Max(action.Elapsed - _lost, 0f), action.Impact, action.Length);
                _aim = Spring.Shortest(action.Aim - input.Yaw);
            }
            Swinging = acting && gesture.Swing > 0f && gesture.Swing < 1f;
            _shake = Spring.Damp(_shake, 0f, 0.07f, deltaTime);
        }

        // The hips go the way the body is going, so that legs step out the way they're going, while the shoulders stay on what they're looking at.
        private void ReadFacing(float deltaTime, float life)
        {
            float speed = _velocity.Xz.Length;
            float wanted = 0f;
            if (speed > 1f)
            {
                float towards = Spring.Shortest(MathF.Atan2(_velocity.X, _velocity.Z) - _yaw);

                // Going backwards isn't turning round. It's stepping back with the hips still square.
                wanted = MathF.Abs(towards) < 1.95f ? Math.Clamp(towards, -1f, 1f) : Math.Clamp(Spring.Shortest(towards + MathF.PI), -0.6f, 0.6f);
                wanted *= Gait.Smooth(1f, 3f, speed);
            }
            _hipTurn = Spring.Damp(_hipTurn, wanted, 0.07f, life);
        }

        // In a dodge or a spin nothing is on the ground, and legs go where a body's going would throw them.
        private void CarryFeet(in AnimationInput input, float spin, bool dashing)
        {
            float scale = input.Scale;
            float leg = Rig.LegLength * scale;
            float yaw = input.Yaw + spin;
            float ground = input.Position.Y;

            for (int i = 0; i < _gait.Feet.Length; i++)
            {
                Limb limb = Rig.Legs[i];
                Vector3 hip = Rig.Pivots[(int)limb.Upper];

                if (dashing)
                {
                    float way = MathF.Atan2(input.Dash.X, input.Dash.Y);
                    bool leading = limb.Side < 0f;
                    Vector3 forward = Frames.Forward(way);
                    Vector3 spot = input.Position + Frames.Left(way) * (hip.X * scale) + forward * ((leading ? 0.62f : -0.82f) * leg);
                    spot.Y = ground + (leading ? 0.3f : 0.14f) * leg;
                    _gait.Carry(i, spot, way, leading ? 0.1f : -0.8f);
                }
                else
                {
                    Vector3 spot = input.Position + Frames.Left(yaw) * (hip.X * scale * 1.5f) + Frames.Forward(yaw) * (hip.Z * scale);
                    spot.Y = ground + 0.12f * leg;
                    _gait.Carry(i, spot, yaw, -0.2f);
                }
            }
        }

        // Where each ankle is, in the body's own space. A foot on the ground rolls from heel to toe, and it's the part still down that stays where it is.
        private void ReadFeet(in AnimationInput input, float life)
        {
            float scale = input.Scale;
            Quaternion toBody = Frames.Yaw(-input.Yaw);
            float inverse = 1f / scale;

            for (int i = 0; i < _gait.Feet.Length; i++)
            {
                ref Foot foot = ref _gait.Feet[i];
                float pitch = foot.Swinging ? foot.Pitch * (Rig.Saurian ? 0.8f : 1f) : StandingPitch(foot);
                float ankleHeight = Rig.AnkleHeight * scale;

                Vector3 forward = Frames.Forward(foot.Yaw);
                Vector3 ankle;
                if (foot.Swinging) ankle = foot.At + Vector3.UnitY * ankleHeight;
                else if (pitch > 0f)
                {
                    // On the heel.
                    float back = Rig.FootBack * scale;
                    ankle = foot.At - forward * back + forward * (back * MathF.Cos(pitch) - ankleHeight * MathF.Sin(pitch)) + Vector3.UnitY * (back * MathF.Sin(pitch) + ankleHeight * MathF.Cos(pitch));
                }
                else
                {
                    // On the ball of the foot, with the heel coming up.
                    float front = Rig.FootForward * scale;
                    float up = -pitch;
                    ankle = foot.At + forward * front - forward * (front * MathF.Cos(up) - ankleHeight * MathF.Sin(up)) + Vector3.UnitY * (front * MathF.Sin(up) + ankleHeight * MathF.Cos(up));
                }

                // What a foot on the ground does is exact. Only one that's being carried can be thrown about.
                Vector3 smooth = foot.Swinging ? _footMotion[i].Follow(ankle, BlendHalfLife, 200f, life) : ankle;
                if (!foot.Swinging) _footMotion[i].Reset();

                _ankles[i] = Vector3.Transform(smooth - input.Position, toBody) * inverse;
                float groundPitch = foot.Swinging ? 0f : foot.GroundPitch;
                float groundRoll = foot.Swinging ? 0f : foot.GroundRoll;
                float yaw = Spring.Shortest(foot.Yaw - input.Yaw) - Rig.Legs[i].Side * _style.ToeIn;
                _soles[i] = Frames.Yaw(yaw) * Frames.Pitch(-pitch - groundPitch) * Frames.Roll(groundRoll);
                _toes[i] = Rig.Saurian ? Toes(i, foot, yaw, groundPitch, groundRoll, life) : _soles[i];
            }
        }

        // A dinosaur walks on its toes, and they have a bone of their own. Down, they lie on the ground however the foot above them turns, so the foot rolls over them as a bird's does.
        // Off the ground they curl under, and open out again to land.
        private Quaternion Toes(int i, in Foot foot, float yaw, float groundPitch, float groundRoll, float life)
        {
            Quaternion toes;
            if (foot.Swinging)
            {
                float through = Math.Clamp(1f - foot.SwingLeft / MathF.Max(foot.SwingTime, 1e-3f), 0f, 1f);
                float curl = 0.95f * MathF.Sin(MathF.PI * MathF.Min(through * 1.25f, 1f));
                toes = _soles[i] * Frames.Pitch(curl);
            }
            else toes = Frames.Yaw(yaw) * Frames.Pitch(-groundPitch) * Frames.Roll(groundRoll);
            return _toeTurn[i].Follow(toes, BlendHalfLife, FastestTurn, life);
        }

        private float StandingPitch(in Foot foot)
        {
            if (foot.StandFor >= 0.99f && !_gait.Moving) return 0f;

            // On the toes from the moment it lands, and over the ball of the foot at the end, which is all the heel a dinosaur has.
            if (Rig.Saurian)
            {
                float rolled = Math.Clamp(foot.Planted / MathF.Max(foot.StandFor, 0.02f), 0f, 1f);
                return -(0.5f + 0.35f * _gait.Run) * Gait.Smooth(0.5f, 1f, rolled);
            }

            // Coming down on the heel, flat for the middle of it, and off the toes at the end. A runner lands flatter.
            float through = Math.Clamp(foot.Planted / MathF.Max(foot.StandFor, 0.02f), 0f, 1f);
            float heel = (0.34f - 0.26f * _gait.Run) * (1f - Gait.Smooth(0f, 0.22f, through));
            float toes = (0.85f + 0.3f * _gait.Run) * Gait.Smooth(0.52f, 1f, through);
            return heel - toes;
        }

        private void PoseBiped(in AnimationInput input, in GestureSample gesture, float spin, bool dashing, float life, float deltaTime, bool transition)
        {
            float leg = Rig.LegLength;
            float weight = gesture.Weight;
            BodyKey body = gesture.Body;
            float speed = _velocity.Xz.Length / input.Scale;
            float run = _gait.Run;
            bool moving = _gait.Moving;

            // How the feet are set under the body: which is ahead, and which side the weight is over.
            float left = _ankles[0].Z;
            float right = _ankles[1].Z;
            float strideNow = moving ? (left - right) / (leg * 1.2f) : 0f;
            _stride = Spring.Damp(_stride, Math.Clamp(strideNow, -1f, 1f), 0.03f, life);

            float over = 0f;
            int down = 0;
            for (int i = 0; i < 2; i++)
            {
                if (_gait.Feet[i].Swinging) continue;
                over += _ankles[i].X;
                down++;
            }
            float idleShift = MathF.Sin(_clock * 0.37f + _seed) * 0.05f * _style.Liveliness * (1f - run);
            float wantedSway = (down > 0 ? over / down * SwayShare * _style.Sway : 0f) + (moving ? 0f : idleShift);
            _sway = Spring.Damp(_sway, wantedSway, 0.075f, life);

            _dazed = Spring.Damp(_dazed, input.Dazed ? 1f : 0f, 0.09f, life);

            float bounceWanted = down == 0 ? 1f : -0.35f * run;
            _bounce = Spring.Damp(_bounce, bounceWanted * run, 0.05f, life);

            Quaternion yawOnly = Frames.Yaw(0f);
            Vector3 accel = Vector3.Transform(_acceleration, Frames.Yaw(-input.Yaw)) / input.Scale;

            // Standing height, and whatever the feet need taken off it.
            float breath = MathF.Sin(_clock * BreathRate) * 0.012f * _style.Liveliness;
            float stand = Rig.HipHeight - leg * (_style.Crouch + 0.05f * _alert + 0.035f * run + body.Sink * weight) + _bounce * BounceHeight * leg * _style.Bounce + breath * (1f - run);
            float wantedHeight = HeightFeetAllow(stand, _sway, leg);
            Spring.Step(ref _hipHeight, ref _hipHeightSpeed, wantedHeight, StepHeightHalfLife, life);

            // Going fast or starting off, the body gets ahead of its feet, and the back goes with it.
            float forwardAccel = Math.Clamp(accel.Z / Rig.Gravity, -1f, 1f);
            float sideAccel = Math.Clamp(accel.X / Rig.Gravity, -1f, 1f);
            float lean = Math.Clamp(RunLean * Gait.Smooth(1f, 12f, speed) * (0.6f + 0.6f * run) + forwardAccel * 0.42f, -HardestLean, HardestLean) * _style.Lean;
            float bank = Math.Clamp(-sideAccel * 0.42f, -HardestLean, HardestLean) * _style.Lean;

            float forwardShove = -forwardAccel * 0.14f;
            Vector3 aimWay = Frames.Forward(_aim);
            Vector3 hips = Rig.Pivots[(int)BoneType.Pelvis] + new Vector3(_sway, 0f, forwardShove);
            hips.Y = _hipHeight;
            hips += aimWay * (body.Push * leg * weight);

            float strideTurn = -_stride * HipTurnFromStride * _style.Twist;
            float hipYaw = _hipTurn + strideTurn + spin;
            float list = -_sway * 0.34f;
            Quaternion pelvis = Frames.Yaw(hipYaw) * Frames.Pitch(lean * 0.28f) * Frames.Roll(list);

            // Where the shoulders face, and how far that is from the hips. When a blow is going they're on the aim, whatever the legs are up to.
            float chestYaw = _stride * ChestTurnFromStride * _style.Twist;
            float attackYaw = _aim + MathHelper.DegreesToRadians(body.Turn) + spin;
            chestYaw = chestYaw + Spring.Shortest(attackYaw - chestYaw) * weight;
            float twist = Spring.Shortest(chestYaw - hipYaw);

            float bend = lean * 0.72f + _style.Hunch + MathHelper.DegreesToRadians(body.Bend) * weight + 0.09f * _alert;
            float tilt = bank + MathHelper.DegreesToRadians(body.Tilt) * weight - list * 0.5f;

            // Stunned, the head goes down and the body goes round in a slow loop, and it's plain from anywhere that this one isn't going to do anything.
            float reel = _clock * 4.1f + _seed;
            bend += _dazed * (0.32f + MathF.Sin(reel) * 0.14f);
            tilt += _dazed * MathF.Cos(reel) * 0.2f;

            // Blows land on top of everything, and go straight into the shaking of whoever took one.
            AdvanceKicks(life);
            float tremor = _held > 0f ? _shake : 0f;
            Vector3 shakeBy = new Vector3(MathF.Sin(_clock * 97f), 0f, MathF.Cos(_clock * 83f)) * (0.05f * tremor);
            hips += _lurch + shakeBy;

            if (dashing)
            {
                float dashYaw = Spring.Shortest(MathF.Atan2(input.Dash.X, input.Dash.Y) - input.Yaw);
                Vector3 dashWay = Frames.Forward(dashYaw);
                hips.Y = Rig.HipHeight - leg * 0.3f;
                bend = 0.75f;
                pelvis = Frames.Yaw(dashYaw) * Frames.Pitch(0.42f);
                twist = 0f;
                chestYaw = dashYaw;
                hips += dashWay * (0.1f * leg);
            }

            Quaternion upper = Frames.Yaw(twist * 0.45f) * Frames.Pitch(bend * 0.45f) * Frames.Roll(tilt * 0.45f);
            Quaternion chestFrom = Frames.Yaw(twist * 0.55f) * Frames.Pitch(bend * 0.55f) * Frames.Roll(tilt * 0.55f);

            Quaternion kicked = Spring.ToRotation(_kick);
            Vector3 hipsSmooth = _pelvisMotion.Follow(hips, BlendHalfLife, FastestBody, deltaTime, transition);
            Quaternion pelvisSmooth = _pelvisTurn.Follow(pelvis, BlendHalfLife, FastestTurn, deltaTime, transition);
            Quaternion spine = _spineTurn.Follow(pelvisSmooth * upper, BlendHalfLife, FastestTurn, deltaTime, transition);
            Quaternion chest = _chestTurn.Follow(spine * chestFrom, BlendHalfLife, FastestTurn, deltaTime, transition);
            chest = (kicked * chest).Normalized();

            _joints[(int)BoneType.Pelvis] = hipsSmooth;
            _rotations[(int)BoneType.Pelvis] = pelvisSmooth;
            _rotations[(int)BoneType.Spine] = spine;
            _rotations[(int)BoneType.Chest] = chest;
            Chain(BoneType.Spine, BoneType.Pelvis);
            Chain(BoneType.Chest, BoneType.Spine);

            PoseHead(input, chest, chestYaw, life, deltaTime, transition, 0f);
            PoseHands(input, gesture, chestYaw, life, deltaTime, transition, speed, run);
        }

        // As high as the hips can go with every foot on the ground still reachable. This is most of why a walk rises and falls: it comes out of the legs, and doesn't have to be put in.
        private float HeightFeetAllow(float stand, float sway, float leg)
        {
            float highest = stand;
            for (int i = 0; i < _gait.Feet.Length; i++)
            {
                if (_gait.Feet[i].Swinging) continue;

                Limb limb = Rig.Legs[i];
                Vector3 hip = Rig.Pivots[(int)limb.Upper];
                float x = _ankles[i].X - hip.X - sway;
                float z = _ankles[i].Z - hip.Z;
                float length = limb.Length * 0.985f;
                float flat = x * x + z * z;
                float top = _ankles[i].Y + MathF.Sqrt(MathF.Max(length * length - flat, 0f));
                highest = MathF.Min(highest, top);
            }
            return MathF.Max(highest, stand - leg * 0.3f);
        }

        private void AdvanceKicks(float life)
        {
            Spring.Ring(ref _kick, ref _kickSpeed, 3.1f, 0.5f, life);
            Spring.Ring(ref _lurch, ref _lurchSpeed, 2.6f, 0.62f, life);
        }

        private void PoseHead(in AnimationInput input, Quaternion chest, float chestYaw, float life, float deltaTime, bool transition, float nod)
        {
            // Where they want to look: what they've been told, or else where they're going, and a bit of looking about when nothing else is going on.
            float wantedYaw;
            float wantedPitch = 0f;
            if (input.Looking)
            {
                Vector3 toward = Vector3.Transform(input.LookAt - input.Position, Frames.Yaw(-input.Yaw));
                wantedYaw = MathF.Atan2(toward.X, toward.Z) - chestYaw;
                wantedPitch = Math.Clamp(-MathF.Atan2(toward.Y - Rig.HipHeight, MathF.Max(toward.Xz.Length, 1f)), -0.4f, 0.4f);
            }
            else
            {
                wantedYaw = -chestYaw * 0.8f;
                float wander = MathF.Sin(_clock * 0.31f + _seed * 1.7f) + MathF.Sin(_clock * 0.53f + _seed);
                wantedYaw += wander * 0.2f * _style.Liveliness * (1f - _gait.Run);
            }

            _lookYaw = Spring.Damp(_lookYaw, Math.Clamp(Spring.Shortest(wantedYaw), -LookLimit, LookLimit), 0.08f, life);
            _lookPitch = Spring.Damp(_lookPitch, wantedPitch, 0.09f, life);

            // A head stays level: the eyes are what a body is balanced by, so it takes back most of what the chest is doing.
            Quaternion level = Frames.Yaw(chestYaw + _lookYaw) * Frames.Pitch(_lookPitch + nod);
            Quaternion head = Quaternion.Slerp(chest * Frames.Yaw(_lookYaw) * Frames.Pitch(_lookPitch + nod), level, 0.72f);
            head = _headTurn.Follow(head, BlendHalfLife, FastestTurn, deltaTime, transition);

            _rotations[(int)BoneType.Neck] = Quaternion.Slerp(chest, head, 0.5f);
            _rotations[(int)BoneType.NeckTop] = head;
            _rotations[(int)BoneType.Head] = head;
            _rotations[(int)BoneType.Jaw] = head;
            Chain(BoneType.Neck, BoneType.Chest);
            Chain(BoneType.NeckTop, BoneType.Neck);
            Chain(BoneType.Head, BoneType.NeckTop);
            Chain(BoneType.Jaw, BoneType.Head);
        }

        private void PoseHands(in AnimationInput input, in GestureSample gesture, float chestYaw, float life, float deltaTime, bool transition, float speed, float run)
        {
            float arm = Rig.ArmLength;
            Vector3 chest = _joints[(int)BoneType.Chest];
            Quaternion chestRotation = _rotations[(int)BoneType.Chest];
            float weight = gesture.Weight;
            float moving = Gait.Smooth(0.5f, 4f, speed);
            float swing = 20f + 34f * run;

            // The arm with the weapon goes first: a second hand on it goes wherever that is, this frame.
            for (int turnOf = 0; turnOf < Rig.Arms.Length; turnOf++)
            {
                int i = turnOf == 0 ? _carry.Arm : 1 - _carry.Arm;
                Limb limb = Rig.Arms[i];
                bool main = i == _carry.Arm;
                float side = limb.Side;

                HandKey key;
                bool heldUp = false;
                if (main) key = Mix(_carry, moving, run);
                else if (_shield)
                {
                    key = HandKey.Lerp(HandKey.Lerp(Carry.ShieldIdle, Carry.ShieldRun, run), Carry.ShieldGuard, _alert);
                    heldUp = true;
                }
                else
                {
                    key = Mix(Carry.Nothing, moving, run);
                    if (side > 0f) key = key.Mirrored;
                }

                // Opposite arm to the leg in front. What's held swings less, and a shield hardly at all.
                float own = (main && _carry != Carry.Nothing ? 0.3f : 1f) * (heldUp ? 0.2f : 1f);
                float forward = -side * _stride * moving * own * _style.ArmSwing;
                key = key with { Turn = key.Turn - side * forward * swing, Reach = key.Reach + forward * 0.1f * run, Height = key.Height + MathF.Max(forward, 0f) * 0.14f * run };

                float turn = chestYaw + MathHelper.DegreesToRadians(key.Turn);
                float height = key.Height;
                float reach = key.Reach;
                float bladeTurn = chestYaw + MathHelper.DegreesToRadians(key.BladeTurn);
                float bladeRise = MathHelper.DegreesToRadians(key.BladeRise);

                bool told = main ? gesture.HasMain : gesture.HasOff && !(gesture.FreeHand && _shield);
                if (told && weight > 0f)
                {
                    // Both are angles around the body, measured from the front. So going from one to the other stays in front of the body and can't flip: the short way round would be through it.
                    HandKey act = main ? gesture.Main : gesture.Off;
                    float front = _aim + MathHelper.DegreesToRadians(gesture.Body.Spin);
                    float from = Spring.Shortest(chestYaw - front);
                    float baseTurn = from + MathHelper.DegreesToRadians(key.Turn);
                    float baseBlade = from + MathHelper.DegreesToRadians(key.BladeTurn);
                    turn = front + baseTurn + (MathHelper.DegreesToRadians(act.Turn) - baseTurn) * weight;
                    bladeTurn = front + baseBlade + (MathHelper.DegreesToRadians(act.BladeTurn) - baseBlade) * weight;
                    height += (act.Height - height) * weight;
                    reach += (act.Reach - reach) * weight;
                    bladeRise += (MathHelper.DegreesToRadians(act.BladeRise) - bladeRise) * weight;
                }

                Vector3 wanted = chest + (Frames.Forward(turn) * reach + Vector3.UnitY * height) * arm;

                // The second hand on a long weapon goes wherever the weapon is. It's the first hand that decides.
                float rise = MathF.Cos(bladeRise);
                Vector3 axis = Frames.Forward(bladeTurn) * rise + Vector3.UnitY * MathF.Sin(bladeRise);
                if (!main && _carry.TwoHanded)
                {
                    wanted = _mainHand + _mainAxis * _carry.SecondGrip;
                    axis = _mainAxis;
                }

                Vector3 hand = _handMotion[i].Follow(wanted, BlendHalfLife, FastestHand, deltaTime, transition);
                PoseArm(i, limb, chest, chestRotation, chestYaw, hand, axis, main, side, deltaTime, transition);
            }
        }

        private Vector3 _mainHand;
        private Vector3 _mainAxis = Vector3.UnitZ;

        private HandKey Mix(Carry carry, float moving, float run)
        {
            HandKey going = HandKey.Lerp(carry.Idle, carry.Run, Math.Max(run, moving * 0.4f));
            return HandKey.Lerp(going, carry.Guard, _alert * (1f - run * 0.7f));
        }

        private void PoseArm(int index, Limb limb, Vector3 chest, Quaternion chestRotation, float chestYaw, Vector3 hand, Vector3 axis, bool main, float side, float deltaTime, bool transition)
        {
            BoneType clavicle = limb.Parent;
            int clavicleIndex = (int)clavicle;
            int upperIndex = (int)limb.Upper;

            _joints[clavicleIndex] = chest + Vector3.Transform(Rig.Pivots[clavicleIndex] - Rig.Pivots[(int)BoneType.Chest], chestRotation);

            // The shoulder goes a little way to where the hand is going, up and forward, which is how an arm gets its full reach.
            Vector3 restOut = Rig.Pivots[upperIndex] - Rig.Pivots[clavicleIndex];
            Vector3 out0 = Vector3.Transform(restOut, chestRotation);
            Vector3 toHand = hand - _joints[clavicleIndex];
            Quaternion shrug = toHand.LengthSquared > 1e-4f ? Frames.Between(out0.Normalized(), toHand.Normalized()) : Quaternion.Identity;
            Quaternion collar = Quaternion.Slerp(Quaternion.Identity, shrug, 0.16f) * chestRotation;
            _rotations[clavicleIndex] = collar;
            Vector3 shoulder = _joints[clavicleIndex] + Vector3.Transform(restOut, collar);

            // Elbows go back and out, and further out the higher the hand goes.
            Vector3 back = -Frames.Forward(chestYaw);
            Vector3 outward = Frames.Left(chestYaw) * side;
            Vector3 elbowWay = back + outward * (0.55f * _style.ElbowsOut) - Vector3.UnitY * 0.25f;

            // A shield lies along the forearm, so the elbow has to stand out for it to face anything.
            if (!main && _shield) elbowWay = back * 0.4f + outward * 1.3f - Vector3.UnitY * 0.3f;

            Reach.Solve(limb, shoulder, hand, elbowWay, out Vector3 elbow, out Vector3 wrist, out Quaternion upper, out Quaternion lower);
            _joints[upperIndex] = shoulder;
            _joints[(int)limb.Lower] = elbow;
            _joints[(int)limb.End] = wrist;
            _rotations[upperIndex] = upper;
            _rotations[(int)limb.Lower] = lower;

            // A fist closes square to the forearm, so what the forearm is doing and which way the blade should go are enough to say how the hand lies.
            Vector3 forearm = (wrist - elbow).Normalized();
            bool holds = main ? _carry != Carry.Nothing : _shield || _carry.TwoHanded;
            if (!holds)
            {
                _rotations[(int)limb.End] = lower;
                return;
            }

            Quaternion grip = Frames.Of(axis, Frames.Across(forearm, axis, Vector3.Cross(axis, Frames.Left(chestYaw)))) * _gripRests[index];
            grip = _gripTurn[index].Follow(grip, BlendHalfLife, FastestGrip, deltaTime, transition);
            _rotations[(int)limb.End] = grip;

            if (!main) return;
            _mainHand = wrist;

            // It's what ended up being held that counts, and that has been through all of the above.
            _mainAxis = Vector3.Transform(_gripAxes[index], grip);
        }

        private void PoseQuadruped(in AnimationInput input, in GestureSample gesture, float life, float deltaTime, bool transition)
        {
            float leg = Rig.LegLength;
            float weight = gesture.Weight;
            BodyKey body = gesture.Body;
            float run = _gait.Run;
            float speed = _velocity.Xz.Length / input.Scale;

            Vector3 pelvisRest = Rig.Pivots[(int)BoneType.Pelvis];
            Vector3 spineRest = Rig.Pivots[(int)BoneType.Spine];
            Vector3 chestRest = Rig.Pivots[(int)BoneType.Chest];
            float length = chestRest.Z - pelvisRest.Z;

            // Each end of the body stands on its own pair of legs, which is what tips it on a slope.
            float hindLow = MathF.Min(_ankles[0].Y, _ankles[1].Y);
            float foreLow = MathF.Min(_ankles[2].Y, _ankles[3].Y);
            float hindHigh = (_ankles[0].Y + _ankles[1].Y) * 0.5f;
            float foreHigh = (_ankles[2].Y + _ankles[3].Y) * 0.5f;
            float ankle = Rig.AnkleHeight;
            float hind = (_gait.Feet[0].Swinging || _gait.Feet[1].Swinging ? hindLow : hindHigh) - ankle;
            float fore = (_gait.Feet[2].Swinging || _gait.Feet[3].Swinging ? foreLow : foreHigh) - ankle;

            float sink = leg * (_style.Crouch + 0.08f * _alert + 0.05f * run + body.Sink * weight);
            float wantedHeight = pelvisRest.Y + hind - sink;
            Spring.Step(ref _hipHeight, ref _hipHeightSpeed, wantedHeight, StepHeightHalfLife, life);

            float foreHeight = chestRest.Y + fore - sink + body.Rise * leg * weight;
            float slope = MathF.Atan2(foreHeight - wantedHeight - (chestRest.Y - pelvisRest.Y), length);

            float phase = _gait.Phase * MathF.Tau;
            float gallop = Gait.Smooth(0.55f, 1f, run) * _style.Gallop;
            float flex = MathF.Sin(phase) * 0.22f * gallop * _style.Bounce;
            float rock = MathF.Cos(phase) * 0.07f * gallop * _style.Bounce;
            float bob = MathF.Sin(phase * 2f) * 0.03f * leg * run * _style.Bounce * (1f - gallop);

            AdvanceKicks(life);
            Vector3 accel = Vector3.Transform(_acceleration, Frames.Yaw(-input.Yaw)) / input.Scale;
            float pitch = -slope + rock - Math.Clamp(accel.Z / Rig.Gravity, -0.5f, 0.5f) * 0.14f + MathHelper.DegreesToRadians(body.Bend) * weight * 0.5f;
            float roll = Math.Clamp(-accel.X / Rig.Gravity, -0.5f, 0.5f) * 0.3f * _style.Lean;
            float turnBend = Math.Clamp(_turnRate * 0.09f, -0.45f, 0.45f);

            // Stunned, the head hangs and the body goes round in a slow loop, on four legs as on two.
            _dazed = Spring.Damp(_dazed, input.Dazed ? 1f : 0f, 0.09f, life);
            float reel = _clock * 4.1f + _seed;
            pitch += _dazed * (0.1f + MathF.Sin(reel) * 0.05f);
            roll += _dazed * MathF.Cos(reel) * 0.13f;

            Vector3 hips = pelvisRest + new Vector3(0f, 0f, 0f);
            hips.Y = _hipHeight + bob;
            hips += _lurch + Vector3.UnitZ * (body.Push * leg * weight);

            Quaternion pelvis = Frames.Yaw(-turnBend * 0.5f) * Frames.Pitch(pitch - flex * 0.5f) * Frames.Roll(roll);
            Quaternion spine = pelvis * Frames.Yaw(turnBend * 0.5f) * Frames.Pitch(flex * 0.5f);
            Quaternion chest = spine * Frames.Yaw(turnBend * 0.5f) * Frames.Pitch(flex * 0.5f);

            Vector3 hipsSmooth = _pelvisMotion.Follow(hips, BlendHalfLife, FastestBody, deltaTime, transition);
            Quaternion pelvisSmooth = _pelvisTurn.Follow(pelvis, BlendHalfLife, FastestTurn, deltaTime, transition);
            Quaternion spineSmooth = _spineTurn.Follow(spine, BlendHalfLife, FastestTurn, deltaTime, transition);
            Quaternion chestSmooth = _chestTurn.Follow(chest, BlendHalfLife, FastestTurn, deltaTime, transition);
            chestSmooth = (Spring.ToRotation(_kick) * chestSmooth).Normalized();

            _joints[(int)BoneType.Pelvis] = hipsSmooth;
            _rotations[(int)BoneType.Pelvis] = pelvisSmooth;
            _rotations[(int)BoneType.Spine] = spineSmooth;
            _rotations[(int)BoneType.Chest] = chestSmooth;
            Chain(BoneType.Spine, BoneType.Pelvis);
            Chain(BoneType.Chest, BoneType.Spine);

            if (Rig.Saurian)
            {
                // A heavy head on a short neck, carried low, and a tail that answers whatever the front end does.
                PoseSaurianHead(input, gesture, chestSmooth, 0f, life, deltaTime, transition, -MathF.Sin(phase) * 0.05f * run + _dazed * 0.25f);
                PoseSaurianTail(pelvisSmooth, pitch, body.Tail * weight, life);
                return;
            }

            PoseHead(input, chestSmooth, 0f, life, deltaTime, transition, -MathF.Sin(phase) * 0.08f * run);
            Vector3 lunge = Vector3.UnitZ * (body.Head * leg * weight) + Vector3.UnitY * (-(MathF.Abs(body.Head) * 0.3f * weight + _dazed * 0.22f) * leg);
            _joints[(int)BoneType.Neck] += lunge * 0.5f;
            _joints[(int)BoneType.NeckTop] += lunge;
            _joints[(int)BoneType.Head] += lunge;
            _joints[(int)BoneType.Jaw] += lunge;

            PoseTail(pelvisSmooth, speed, life);
        }

        private float _tailSwing;
        private float _tailSwingSpeed;

        // A tail is carried along behind everything else and does nothing of its own.
        private void PoseTail(Quaternion pelvis, float speed, float life)
        {
            float wanted = -_turnRate * 0.15f + MathF.Sin(_clock * 2.3f) * 0.08f * _style.Liveliness;
            Spring.Step(ref _tailSwing, ref _tailSwingSpeed, wanted, 0.12f, life);
            float droop = 0.25f - Gait.Smooth(2f, 10f, speed) * 0.5f;

            _rotations[(int)BoneType.TailBase] = pelvis * Frames.Yaw(_tailSwing) * Frames.Pitch(droop * 0.5f);
            _rotations[(int)BoneType.Tail1] = _rotations[(int)BoneType.TailBase];
            _rotations[(int)BoneType.Tail2] = _rotations[(int)BoneType.TailBase];
            _rotations[(int)BoneType.Tail3] = _rotations[(int)BoneType.TailBase];
            _rotations[(int)BoneType.TailTip] = _rotations[(int)BoneType.TailBase] * Frames.Yaw(_tailSwing * 1.3f) * Frames.Pitch(droop * 0.5f);
            Chain(BoneType.TailBase, BoneType.Pelvis);
            Chain(BoneType.Tail1, BoneType.TailBase);
            Chain(BoneType.Tail2, BoneType.Tail1);
            Chain(BoneType.Tail3, BoneType.Tail2);
            Chain(BoneType.TailTip, BoneType.Tail3);
        }

        // ---- Dinosaurs.

        private const int TailBones = 5;
        private static readonly BoneType[] TailChain = { BoneType.TailBase, BoneType.Tail1, BoneType.Tail2, BoneType.Tail3, BoneType.TailTip };
        private static readonly float[] LashShares = { 0.12f, 0.18f, 0.22f, 0.24f, 0.24f };
        private const float BiteHeight = 2.6f;     // Metres. About a person's chest, which is what anything with teeth is going for.

        private readonly float[] _tailYaw = new float[TailBones];
        private readonly float[] _tailYawSpeed = new float[TailBones];
        private readonly float[] _tailPitch = new float[TailBones];
        private readonly float[] _tailPitchSpeed = new float[TailBones];
        private TurnContinuity _neckTurn;
        private TurnContinuity _neckTopTurn;
        private float _jaw;
        private float _jawSpeed;
        private float _glance;
        private float _glanceLeft;

        // How far down the front end has to come to bite someone standing, as a share of how high its head is: next to nothing for something a person's size, and a long way for a tyrant.
        private float DipFor(float scale)
        {
            float head = Rig.Pivots[(int)BoneType.Head].Y * scale;
            return Math.Clamp((head - BiteHeight) / MathF.Max(head, 0.1f), 0f, 0.8f);
        }

        // A theropod is a beam balanced on its hips: trunk, neck and head out in front, the tail behind, and both held clear of the ground. Its feet go down near the line it walks, the beam rocks over
        // them, and its head stays level and steady while all of that goes on beneath it, as a bird's does.
        private void PoseSaurian(in AnimationInput input, in GestureSample gesture, float spin, float life, float deltaTime, bool transition)
        {
            float leg = Rig.LegLength;
            float size = leg * 0.5f;    // In a person's legs, which is what the numbers that were first set for a person were set by.
            float weight = gesture.Weight;
            BodyKey body = gesture.Body;
            float speed = _velocity.Xz.Length / input.Scale;
            float run = _gait.Run;
            bool moving = _gait.Moving;

            float strideNow = moving ? (_ankles[0].Z - _ankles[1].Z) / (leg * 1.2f) : 0f;
            _stride = Spring.Damp(_stride, Math.Clamp(strideNow, -1f, 1f), 0.03f, life);

            float over = 0f;
            int down = 0;
            for (int i = 0; i < 2; i++)
            {
                if (_gait.Feet[i].Swinging) continue;
                over += _ankles[i].X;
                down++;
            }
            float idleShift = MathF.Sin(_clock * 0.29f + _seed) * 0.03f * size * _style.Liveliness * (1f - run);
            float wantedSway = (down > 0 ? over / down * SwayShare * _style.Sway : 0f) + (moving ? 0f : idleShift);
            _sway = Spring.Damp(_sway, wantedSway, 0.09f, life);
            _dazed = Spring.Damp(_dazed, input.Dazed ? 1f : 0f, 0.09f, life);

            float bounceWanted = down == 0 ? 1f : -0.35f * run;
            _bounce = Spring.Damp(_bounce, bounceWanted * run, 0.05f, life);

            // Breathing slows as bodies grow, as everything else about them does.
            float breathRate = BreathRate / MathF.Sqrt(MathF.Max(size, 0.2f));
            float breath = MathF.Sin(_clock * breathRate) * 0.012f * size * _style.Liveliness;
            float stand = Rig.HipHeight - leg * (_style.Crouch + 0.04f * _alert + 0.03f * run + body.Sink * weight) + _bounce * BounceHeight * leg * _style.Bounce + breath * (1f - run);
            Spring.Step(ref _hipHeight, ref _hipHeightSpeed, HeightFeetAllow(stand, _sway, leg), StepHeightHalfLife, life);

            // The beam tips forward into speed and into starting off, and back as it stops: that's all a body balanced on its hips can do. Going for someone, it comes down as far as it has to
            // to reach them, and rearing it goes back on its tail.
            Vector3 accel = Vector3.Transform(_acceleration, Frames.Yaw(-input.Yaw)) / input.Scale;
            float forwardAccel = Math.Clamp(accel.Z / Rig.Gravity, -1f, 1f);
            float sideAccel = Math.Clamp(accel.X / Rig.Gravity, -1f, 1f);
            float lean = Math.Clamp(RunLean * 0.6f * Gait.Smooth(1f, 12f, speed) * (0.6f + 0.6f * run) + forwardAccel * 0.3f, -HardestLean, HardestLean) * _style.Lean;
            float bank = Math.Clamp(-sideAccel * 0.3f, -HardestLean, HardestLean) * _style.Lean;
            float strike = MathF.Max(body.Head, 0f) * weight;
            float reel = _clock * 3.1f + _seed;
            float pitch = lean + MathHelper.DegreesToRadians(body.Bend) * weight + strike * DipFor(input.Scale) * 0.9f - body.Rise * weight * 1.2f + 0.04f * _alert
                + _dazed * (0.12f + MathF.Sin(reel) * 0.06f);
            float roll = bank - _sway / leg * 0.6f + _dazed * MathF.Cos(reel) * 0.1f;

            Vector3 aimWay = Frames.Forward(_aim);
            Vector3 hips = Rig.Pivots[(int)BoneType.Pelvis] + new Vector3(_sway, 0f, -forwardAccel * 0.1f * leg);
            hips.Y = _hipHeight;
            hips += aimWay * (body.Push * leg * weight);

            float hipYaw = _hipTurn - _stride * HipTurnFromStride * _style.Twist * 0.6f + spin;
            Quaternion pelvis = Frames.Yaw(hipYaw) * Frames.Pitch(pitch * 0.6f) * Frames.Roll(roll);

            // The chest swings against the hips, and turns to whatever it's going for.
            float chestYaw = _stride * ChestTurnFromStride * _style.Twist * 0.4f;
            float attackYaw = _aim + MathHelper.DegreesToRadians(body.Turn) + spin;
            chestYaw += Spring.Shortest(attackYaw - chestYaw) * weight;
            float twist = Spring.Shortest(chestYaw - hipYaw);
            float tilt = MathHelper.DegreesToRadians(body.Tilt) * weight;
            float heave = MathF.Sin(_clock * breathRate) * 0.012f * (1f - run);

            AdvanceKicks(life);
            float tremor = _held > 0f ? _shake : 0f;
            hips += _lurch + new Vector3(MathF.Sin(_clock * 97f), 0f, MathF.Cos(_clock * 83f)) * (0.05f * tremor);

            Quaternion spine = Frames.Yaw(twist * 0.5f) * Frames.Pitch(pitch * 0.2f + heave) * Frames.Roll(tilt * 0.5f);
            Quaternion chestFrom = Frames.Yaw(twist * 0.5f) * Frames.Pitch(pitch * 0.2f - heave) * Frames.Roll(tilt * 0.5f);

            Vector3 hipsSmooth = _pelvisMotion.Follow(hips, BlendHalfLife, FastestBody, deltaTime, transition);
            Quaternion pelvisSmooth = _pelvisTurn.Follow(pelvis, BlendHalfLife, FastestTurn, deltaTime, transition);
            Quaternion spineSmooth = _spineTurn.Follow(pelvisSmooth * spine, BlendHalfLife, FastestTurn, deltaTime, transition);
            Quaternion chest = _chestTurn.Follow(spineSmooth * chestFrom, BlendHalfLife, FastestTurn, deltaTime, transition);
            chest = (Spring.ToRotation(_kick) * chest).Normalized();

            _joints[(int)BoneType.Pelvis] = hipsSmooth;
            _rotations[(int)BoneType.Pelvis] = pelvisSmooth;
            _rotations[(int)BoneType.Spine] = spineSmooth;
            _rotations[(int)BoneType.Chest] = chest;
            Chain(BoneType.Spine, BoneType.Pelvis);
            Chain(BoneType.Chest, BoneType.Spine);

            PoseSaurianHead(input, gesture, chest, chestYaw, life, deltaTime, transition, 0f);
            PoseSaurianArms(chest, strike, run);
            PoseSaurianTail(pelvisSmooth, pitch, body.Tail * weight, life);
        }

        // The head looks where it's going, or at what it means to hurt, in quick turns held still between, as a bird's does, and it's kept level whatever the body under it is doing: the neck takes up
        // the difference. A strike is that neck straightening and throwing the head forward, and the jaw goes with whatever the blow says.
        private void PoseSaurianHead(in AnimationInput input, in GestureSample gesture, Quaternion chest, float chestYaw, float life, float deltaTime, bool transition, float nod)
        {
            float weight = gesture.Weight;
            BodyKey body = gesture.Body;

            float wantedYaw;
            float wantedPitch = _style.HeadLow;
            if (input.Looking)
            {
                Vector3 toward = Vector3.Transform(input.LookAt - input.Position, Frames.Yaw(-input.Yaw)) / MathF.Max(input.Scale, 0.01f);
                wantedYaw = MathF.Atan2(toward.X, toward.Z) - chestYaw;
                wantedPitch += Math.Clamp(-MathF.Atan2(toward.Y - Rig.Pivots[(int)BoneType.Head].Y, MathF.Max(toward.Xz.Length, 1f)), -0.5f, 0.5f) * 0.6f;
            }
            else
            {
                // Somewhere new to look every so often, gone to at once and held.
                _glanceLeft -= life;
                if (_glanceLeft <= 0f)
                {
                    uint hash = ((uint)_seed * 747796405u) ^ ((uint)(_clock * 13.7f) * 2891336453u);
                    hash ^= hash >> 15;
                    hash *= 2246822519u;
                    float where = (hash & 0xFFFF) / 65535f;
                    float wait = (hash >> 16) / 65535f;
                    _glance = (where * 2f - 1f) * 0.55f * _style.Liveliness * (1f - _gait.Run);
                    _glanceLeft = 0.5f + wait * 2.5f / MathF.Max(_style.Liveliness, 0.2f);
                }
                wantedYaw = -chestYaw * 0.8f + _glance;
            }

            _lookYaw = Spring.Damp(_lookYaw, Math.Clamp(Spring.Shortest(wantedYaw), -LookLimit, LookLimit), input.Looking ? 0.07f : 0.045f, life);
            _lookPitch = Spring.Damp(_lookPitch, wantedPitch, 0.09f, life);

            float strike = body.Head * weight;
            float rear = body.Rise * weight;
            Quaternion level = Frames.Yaw(chestYaw + _lookYaw) * Frames.Pitch(_lookPitch + nod - rear * 0.6f + MathF.Max(strike, 0f) * 0.2f);
            Quaternion head = _headTurn.Follow(level, BlendHalfLife, FastestTurn, deltaTime, transition);

            // Most of the turn is at the root of the neck and the rest near the head, and a strike pitches the root forward while the top holds the head up.
            Quaternion neck = Quaternion.Slerp(chest, head, 0.4f) * Frames.Pitch(strike * 0.85f + nod * 0.5f);
            Quaternion neckTop = Quaternion.Slerp(chest, head, 0.75f) * Frames.Pitch(-strike * 0.35f + nod * 0.5f);
            neck = _neckTurn.Follow(neck, BlendHalfLife, FastestTurn, deltaTime, transition);
            neckTop = _neckTopTurn.Follow(neckTop, BlendHalfLife, FastestTurn, deltaTime, transition);

            float pant = _style.Pant * (0.25f + 0.75f * _alert) * (0.5f + 0.5f * MathF.Sin(_clock * 7.5f)) * 0.09f;
            float open = Math.Clamp(body.Jaw * weight + pant + _dazed * 0.3f, 0f, 1.05f);
            Spring.Step(ref _jaw, ref _jawSpeed, open, 0.022f, life);
            float gape = Math.Clamp(_jaw, 0f, 1.1f) * _style.Gape;

            _rotations[(int)BoneType.Neck] = neck;
            _rotations[(int)BoneType.NeckTop] = neckTop;
            _rotations[(int)BoneType.Head] = head;
            _rotations[(int)BoneType.Jaw] = head * Frames.Pitch(gape);
            Chain(BoneType.Neck, BoneType.Chest);
            Chain(BoneType.NeckTop, BoneType.Neck);
            Chain(BoneType.Head, BoneType.NeckTop);
            Chain(BoneType.Jaw, BoneType.Head);
        }

        // Small arms, carried folded with the palms towards each other, as a theropod's could only be. They swing a little with the stride and reach out for whatever the mouth goes for.
        private void PoseSaurianArms(Quaternion chest, float strike, float run)
        {
            for (int i = 0; i < Rig.Arms.Length; i++)
            {
                Limb arm = Rig.Arms[i];
                float swing = arm.Side * _stride * 0.14f * (0.35f + 0.65f * run);
                float reach = strike * 1.3f + _alert * 0.08f;
                Quaternion upper = chest * Frames.Pitch(-reach - swing);
                Quaternion lower = upper * Frames.Pitch(-strike * 0.5f + swing * 0.5f);

                _rotations[(int)arm.Parent] = chest;
                _rotations[(int)arm.Upper] = upper;
                _rotations[(int)arm.Lower] = lower;
                _rotations[(int)arm.End] = lower * Frames.Pitch(-strike * 0.3f);
                Chain(arm.Parent, BoneType.Chest);
                Chain(arm.Upper, arm.Parent);
                Chain(arm.Lower, arm.Upper);
                Chain(arm.End, arm.Lower);
            }
        }

        // The tail swings from side to side in time with the legs, as the muscle that pulls each thigh back runs down it from the hips, and the swing travels down it as a wave: Bishop and others found
        // just that when they made a theropod walk from its muscles. It trails out on the outside of a turn, straightens and lifts with speed, and rises further as the front end comes down. Each joint
        // follows on a spring of its own, slower towards the tip, which is all it takes for a lash to come off the end.
        private void PoseSaurianTail(Quaternion pelvis, float lean, float lash, float life)
        {
            float run = _gait.Run;
            float phase = _gait.Phase * MathF.Tau;
            float stiff = _style.TailStiff;
            float swing = _gait.Moving ? _style.TailSway * (0.07f + 0.05f * run) : 0f;
            float carry = 1f - stiff * 0.8f;
            float lag = 0.75f - 0.5f * stiff;
            float lashBy = MathHelper.DegreesToRadians(lash);

            Quaternion parent = pelvis;
            BoneType parentBone = BoneType.Pelvis;
            for (int i = 0; i < TailBones; i++)
            {
                float down = MathF.Pow(carry, i);
                float wave = MathF.Sin(phase - i * lag) * swing * down;
                float idle = MathF.Sin(_clock * 0.9f - i * 0.8f + _seed) * 0.035f * _style.Liveliness * (1f - run) * (0.4f + 0.6f * down);
                float trail = -_turnRate * (0.025f + 0.02f * i) * (1f - stiff * 0.5f);
                float yaw = Math.Clamp(wave + idle + trail + lashBy * LashShares[i], -1.2f, 1.2f);
                float rise = (0.05f * run + lean * 0.25f - 0.03f * (1f - run)) / TailBones + MathF.Sin(_clock * 0.7f - i) * 0.01f * _style.Liveliness;

                float halfLife = 0.045f + 0.03f * i * (1f - stiff * 0.6f);
                Spring.Step(ref _tailYaw[i], ref _tailYawSpeed[i], yaw, halfLife, life);
                Spring.Step(ref _tailPitch[i], ref _tailPitchSpeed[i], rise, halfLife, life);

                Quaternion here = parent * Frames.Yaw(_tailYaw[i]) * Frames.Pitch(_tailPitch[i]);
                BoneType bone = TailChain[i];
                _rotations[(int)bone] = here;
                Chain(bone, parentBone);
                parent = here;
                parentBone = bone;
            }
        }

        // A bone's joint from its parent's: where it was in the pose it was built in, carried along by how the parent has turned.
        private void Chain(BoneType bone, BoneType parent)
        {
            int child = (int)bone;
            int from = (int)parent;
            _joints[child] = _joints[from] + Vector3.Transform(Rig.Pivots[child] - Rig.Pivots[from], _rotations[from]);
        }

        private void Solve(in AnimationInput input)
        {
            Quaternion pelvis = _rotations[(int)BoneType.Pelvis];
            Vector3 hips = _joints[(int)BoneType.Pelvis];
            Vector3 hipRest = Rig.Pivots[(int)BoneType.Pelvis];

            for (int i = 0; i < Rig.Legs.Length; i++)
            {
                Limb limb = Rig.Legs[i];
                bool front = limb.Parent == BoneType.Chest;
                Vector3 anchor = front ? _joints[(int)BoneType.Chest] : hips;
                Quaternion anchorTurn = front ? _rotations[(int)BoneType.Chest] : pelvis;
                Vector3 anchorRest = front ? Rig.Pivots[(int)BoneType.Chest] : hipRest;

                Vector3 root = anchor + Vector3.Transform(Rig.Pivots[(int)limb.Upper] - anchorRest, anchorTurn);

                // Knees go the way the foot points and the way the hips point, between the two.
                Vector3 facing = Vector3.Transform(limb.Bends, _soles[i]) + Vector3.Transform(limb.Bends, anchorTurn);
                Vector3 knee = facing + Vector3.UnitX * (limb.Side * 0.12f);

                Reach.Solve(limb, root, _ankles[i], knee, out Vector3 middle, out Vector3 end, out Quaternion upper, out Quaternion lower);
                _joints[(int)limb.Upper] = root;
                _joints[(int)limb.Lower] = middle;
                _joints[(int)limb.End] = end;
                _rotations[(int)limb.Upper] = upper;
                _rotations[(int)limb.Lower] = lower;
                _rotations[(int)limb.End] = _soles[i];

                BoneType toe = Rig.ToeOf(limb.End);
                if (toe == BoneType.Count) continue;
                _rotations[(int)toe] = _toes[i];
                Chain(toe, limb.End);
            }

            if (_carry != Carry.Nothing) ReadWeapon(input);
            WriteSkin();
        }

        private void ReadWeapon(in AnimationInput input)
        {
            Quaternion toWorld = Frames.Yaw(input.Yaw);
            WeaponBase = input.Position + Vector3.Transform(_mainHand, toWorld) * input.Scale;
            WeaponTip = input.Position + Vector3.Transform(_mainHand + _mainAxis * _carry.Length, toWorld) * input.Scale;
        }

        private void WriteSkin()
        {
            for (int i = 0; i < Rig.BoneCount; i++)
            {
                Quaternion rotation = _rotations[i];
                Vector3 joint = _joints[i];

                // A number that isn't one takes a whole body off the screen. Nothing should ever make one, and if something does this is the last thing that sees it: better what it stood like last than nothing at all.
                if (!float.IsFinite(rotation.W + rotation.X + rotation.Y + rotation.Z + joint.X + joint.Y + joint.Z))
                {
                    Faults++;
                    _seen = false;
                    return;
                }
                Vector3 offset = joint - Vector3.Transform(Rig.Pivots[i], rotation);
                Matrix4 matrix = Matrix4.CreateFromQuaternion(rotation);
                matrix.Row3 = new Vector4(offset, 1f);
                Skinning[i] = matrix;
            }
        }

        /// <summary>Where a joint is, in the body's own space, for a lab to measure.</summary>
        public Vector3 JointOf(BoneType bone) => _joints[(int)bone];

        /// <summary>How a bone is turned, in the body's own space.</summary>
        public Quaternion RotationOf(BoneType bone) => _rotations[(int)bone];
    }
}

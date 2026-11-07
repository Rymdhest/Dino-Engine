using Dino_Engine.Animation;
using OpenTK.Mathematics;

namespace Dino_Engine.Wildlife
{
    /// <summary>A few rabbits on one patch of grass. When one goes, the others go: they feed with their ears up, and a bolting neighbour is the best warning there is.</summary>
    public sealed class Warren : Party
    {
        private const float Spread = 7f;
        private const float WarnReach = 18f;

        internal readonly RabbitKind Kind;

        internal Warren(WildlifeSim sim, RabbitKind kind, Resident home) : base(sim, home)
        {
            Kind = kind;
            for (int i = 0; i < home.Count; i++)
            {
                Vector2 at = home.At + new Vector2(sim.Random.Range(-Spread, Spread), sim.Random.Range(-Spread, Spread));
                if (!sim.Land.CanStand(at)) at = home.At;
                if (!sim.Land.CanStand(at)) continue;
                Members.Add(new Rabbit(sim, this, at));
            }
        }

        internal void Warn(Rabbit from, Vector2 away)
        {
            Frightened = true;
            foreach (Critter member in Members)
            {
                if (member != from && member is Rabbit other && (other.Position.Xz - from.Position.Xz).LengthSquared < WarnReach * WarnReach) other.Hear(away);
            }
        }
    }

    /// <summary>
    /// One rabbit. Mostly it's eating: head down, ears half back, and every few seconds up to look. It goes by what's been seen of real ones: it <b>freezes</b> first, flat, with its ears laid back, as being still is what's
    /// worked for a long time, and only runs when something comes too close. Then it goes in long bounds with its tail up, turning at landings, as direction can only change with feet on the ground, and it's turning
    /// that's kept rabbits alive. Some way off it stops and sits up to see what happened.
    /// </summary>
    public sealed class Rabbit : Critter
    {
        private enum State { Grazing, Looking, SatUp, Washing, Hopping, Frozen, Bolting, GoingToGround }

        private const float Alert = 1.75f;          // How far out, as a share of how shy it is, it starts paying attention.
        private const float SenseRate = 0.12f;
        private const float HopTime = 0.42f;
        private const float BoltTime = 0.3f;
        private const float HopLength = 0.95f;
        private const float BoltLength = 3.4f;
        private const float GroundTime = 0.35f;     // Seconds it takes to go down a hole.
        private const float TooClose = 0.45f;       // Of how shy it is. There's no time to be clever when something's this near.
        private const float SlopeLook = 0.6f;

        // Where it rests it presses the grass down into a form, as hares do, and that's also what lets it be seen. The grass rises slowly, so a short press now and then holds it down for a fraction of what
        // leaning on it would cost: each contact is paid for by every texel of the field. It's a length of time and not a frame, or it would press harder at a lower frame rate.
        private const float FormEvery = 0.8f;
        private const float FormPress = 0.13f;
        private const float FormRadius = 1.45f;
        private const float GrassToPress = 0.45f;

        private static readonly float[] Tries = { 0f, 0.5f, -0.5f, 1f, -1f, 1.6f, -1.6f, 2.4f, -2.4f };

        private readonly Warren _warren;
        private readonly RabbitKind _kind;

        private State _state = State.Grazing;
        private float _until;
        private float _nextSense;
        private float _nextFlick;
        private float _nextForm;
        private float _formUntil;
        private float _fright = float.PositiveInfinity;
        private Vector2 _away = Vector2.UnitX;
        private float _warnedAt = -1f;

        // How far through a bound, and how it goes.
        private float _through = 1f;
        private float _boundTime = HopTime;
        private float _gait;
        private float _strength;
        private Vector2 _from, _to;
        private float _height;
        private float _toRun;
        private int _hopsLeft;
        private float _grass;

        // What it's doing with itself, eased, so that nothing's ever snapped from one way of standing to another.
        private float _rear, _crouch, _headPitch, _headYaw, _earsBack, _earLeft, _earRight, _earLeftSpeed, _earRightSpeed, _tail, _lean, _slope, _chew, _ground;

        public override WildlifeShape Shape => _kind.Shape;
        public override float Reach => 1.2f * Size;
        public override string Doing => _state.ToString();

        internal Rabbit(WildlifeSim sim, Warren warren, Vector2 at) : base(sim, warren.Kind)
        {
            _warren = warren;
            _kind = warren.Kind;
            Yaw = sim.Random.Range(-MathF.PI, MathF.PI);
            _ground = sim.Land.Ground(at);
            Position = new Vector3(at.X, _ground, at.Y);
            _from = _to = at;
            _grass = sim.Land.GrassTop(at);
            _until = sim.Now + sim.Random.Range(0f, 4f);
            _nextSense = sim.Now + sim.Random.Range(0f, SenseRate);
            _nextFlick = sim.Now + sim.Random.Range(0.5f, 3f);
            _headPitch = 1f;
            _earsBack = 0.3f;
        }

        /// <summary>Another's gone.</summary>
        internal void Hear(Vector2 away)
        {
            if (_state == State.Bolting || _warnedAt >= 0f) return;
            _away = away;
            _warnedAt = Sim.Now + Sim.Random.Range(0.08f, 0.32f);
        }

        public override void Step(float deltaTime)
        {
            float now = Sim.Now;

            if (now >= _nextSense)
            {
                _nextSense = now + SenseRate;
                _fright = Sim.Fright(Position.Xz, _kind.Shy, out Vector2 away);
                if (_fright < Alert) _away = away;
            }

            if (_warnedAt >= 0f && now >= _warnedAt)
            {
                _warnedAt = -1f;
                Bolt();
            }

            switch (_state)
            {
                case State.Bolting:
                    break;
                case State.GoingToGround:
                    Fade = MathF.Max(Fade - deltaTime / GroundTime, 0f);
                    if (Fade <= 0f) Gone = true;
                    break;
                case State.Frozen:
                    if (_fright < 1f) Bolt();
                    else if (_fright > Alert && now >= _until) Calm();
                    break;
                default:
                    if (_fright < 1f) Bolt();
                    else if (_fright < Alert && _through >= 1f) Freeze();
                    else if (now >= _until && _through >= 1f) NextThing();
                    break;
            }

            StepBound(deltaTime);
            Bearing(deltaTime);
            PressForm();
        }

        private void PressForm()
        {
            if (_through < 1f || _grass < GrassToPress || !Sim.InView(Position.Xz, 4f)) return;
            float now = Sim.Now;
            if (now >= _nextForm)
            {
                _nextForm = now + FormEvery;
                _formUntil = now + FormPress;
            }
            if (now < _formUntil) Sim.Touch(Position.Xz, Position.Xz, FormRadius * Size, 1.3f, 1.3f, 0f);
        }

        private void Freeze()
        {
            _state = State.Frozen;
            _until = Sim.Now + Sim.Random.Range(1.2f, 3f);
        }

        private void Calm()
        {
            _state = State.Looking;
            _until = Sim.Now + Sim.Random.Range(1f, 2.6f);
        }

        private void Bolt()
        {
            if (_state == State.GoingToGround) return;
            _warren.Warn(this, _away);
            _state = State.Bolting;
            _toRun = Sim.Random.Range(24f, 46f) * _kind.BoltSpeed;
            _hopsLeft = 0;
            if (_through >= 1f) NextBound(true);
        }

        private void NextThing()
        {
            float roll = Sim.Random.Float();
            if (_state == State.Grazing)
            {
                if (roll < 0.44f) Do(State.Looking, 1f, 3f);
                else if (roll < 0.62f) Do(State.SatUp, 1.4f, 3.6f);
                else if (roll < 0.72f) Do(State.Washing, 1.6f, 3f);
                else
                {
                    _state = State.Hopping;
                    _hopsLeft = Sim.Random.Int(1, 4);
                    NextBound(false);
                }
                return;
            }

            // Whatever else it was doing, it goes back to eating, as that's what it's here for.
            if (roll < 0.25f && _state != State.Hopping)
            {
                _state = State.Hopping;
                _hopsLeft = Sim.Random.Int(1, 3);
                NextBound(false);
                return;
            }
            Do(State.Grazing, 2f, 7f);
        }

        private void Do(State state, float shortest, float longest)
        {
            _state = state;
            _until = Sim.Now + Sim.Random.Range(shortest, longest);
        }

        // Picks where the next bound goes, and starts it. A rabbit that's fleeing turns at landings, and one that isn't wanders.
        private void NextBound(bool fleeing)
        {
            Vector2 at = Position.Xz;
            float length, heading;
            if (fleeing)
            {
                length = BoltLength * _kind.BoltSpeed * Sim.Random.Range(0.85f, 1.15f);
                heading = Turn.YawOf(_away);
                if (_fright > TooClose && Sim.Random.Float() < 0.5f) heading += Sim.Random.Range(0.55f, 1.05f) * (Sim.Random.Float() < 0.5f ? -1f : 1f);
            }
            else
            {
                length = HopLength * Sim.Random.Range(0.75f, 1.25f);
                heading = Yaw + Sim.Random.Range(-0.9f, 0.9f);
            }

            // Long bounds first, and short ones if that's all there's room for among whatever's standing here.
            for (int i = 0; i < Tries.Length * 2; i++)
            {
                float yaw = heading + Tries[i % Tries.Length];
                if (i == Tries.Length)
                {
                    // Short bounds won't outrun anything this close.
                    if (fleeing && _fright < TooClose) break;
                    length *= 0.5f;
                }
                Vector2 to = at + Turn.Heading(yaw) * length;
                if (!Sim.Land.CanStand(to) || !Sim.Land.CanStand((at + to) * 0.5f)) continue;

                _from = at;
                _to = to;
                Yaw = yaw;
                _through = 0f;
                _boundTime = (fleeing ? BoltTime : HopTime) * Sim.Random.Range(0.92f, 1.08f);
                _strength = fleeing ? 1f : 0.62f;
                _grass = Sim.Land.GrassTop(to);

                // Tall grass is cleared, not pushed through, which is why a rabbit in a meadow is seen in leaps.
                _height = (fleeing ? 0.46f : 0.24f) + _grass * (fleeing ? 0.85f : 0.6f);
                return;
            }

            // Nowhere to go from here. There's always a hole under something, for a rabbit that needs one.
            _hopsLeft = 0;
            _toRun = 0f;
            if (_state == State.Bolting) _state = State.GoingToGround;
            else if (_state == State.Hopping) Do(State.Grazing, 2f, 7f);
        }

        private void StepBound(float deltaTime)
        {
            if (_through >= 1f)
            {
                _gait = Spring.Damp(_gait, 0f, 0.05f, deltaTime);
                return;
            }

            _gait = Spring.Damp(_gait, 1f, 0.03f, deltaTime);
            Vector2 was = Position.Xz;
            _through = MathF.Min(_through + deltaTime / _boundTime, 1f);
            Vector2 at = Vector2.Lerp(_from, _to, RabbitGait.Along(_through));
            _ground = Sim.Land.Ground(at);
            Position = new Vector3(at.X, _ground + RabbitGait.Height(_through) * _height, at.Y);

            // It parts what it goes through, a good deal more going flat out, and where it lands is pressed down.
            if (Sim.InView(at, 6f) && _grass > 0.2f)
            {
                float speed = (at - was).Length / MathF.Max(deltaTime, 1e-3f);
                Sim.Touch(at, was, 0.85f * Size, 1.05f, 0.5f, speed / (speed + 5f));
            }

            if (_through < 1f) return;

            if (_state == State.Bolting)
            {
                _toRun -= (_to - _from).Length;
                if (_toRun > 0f)
                {
                    NextBound(true);
                    return;
                }
                // Far enough. It stops and looks back at what it ran from.
                Do(State.SatUp, 2.2f, 4.5f);
                Yaw = Turn.YawOf(-_away);
                return;
            }

            if (_state == State.Hopping)
            {
                _hopsLeft--;
                if (_hopsLeft > 0) NextBound(false);
                else Do(State.Grazing, 2f, 7f);
            }
        }

        private void Bearing(float deltaTime)
        {
            float rear = 0f, crouch = 0f, headPitch = 0f, headYaw = 0f, ears = 0f, tail = 0f, lean = 0f;
            float chew = 0f;
            switch (_state)
            {
                case State.Grazing:
                    headPitch = 1.08f;
                    lean = 0.2f;
                    ears = 0.34f;
                    chew = 1f;
                    break;
                case State.Looking:
                    headPitch = 0.05f;
                    headYaw = MathF.Sin(Sim.Now * 0.9f + Size * 9f) * 0.45f;
                    break;
                case State.SatUp:
                    rear = 1f;
                    headYaw = MathF.Sin(Sim.Now * 0.7f + Size * 5f) * 0.6f;
                    break;
                case State.Washing:
                    rear = 0.72f;
                    headPitch = 0.55f + MathF.Sin(Sim.Now * 7f) * 0.14f;
                    ears = 0.2f;
                    break;
                case State.Hopping:
                    ears = 0.45f;
                    break;
                case State.Frozen:
                case State.GoingToGround:
                    crouch = 1f;
                    ears = 1f;
                    break;
                case State.Bolting:
                    ears = 0.92f;
                    tail = 1f;
                    break;
            }

            _rear = Spring.Damp(_rear, rear, 0.09f, deltaTime);
            _crouch = Spring.Damp(_crouch, crouch, 0.06f, deltaTime);
            _headPitch = Spring.Damp(_headPitch, headPitch, 0.07f, deltaTime);
            _headYaw = Spring.Damp(_headYaw, headYaw, 0.1f, deltaTime);
            _earsBack = Spring.Damp(_earsBack, ears, 0.08f, deltaTime);
            _tail = Spring.Damp(_tail, tail, 0.05f, deltaTime);
            _lean = Spring.Damp(_lean, lean, 0.1f, deltaTime);
            _chew = Spring.Damp(_chew, chew, 0.1f, deltaTime);

            // Ears flick by themselves, and each has a mind of its own.
            if (Sim.Now >= _nextFlick)
            {
                _nextFlick = Sim.Now + Sim.Random.Range(0.7f, 3.4f);
                if (Sim.Random.Float() < 0.5f) _earLeftSpeed += Sim.Random.Range(4f, 9f);
                else _earRightSpeed += Sim.Random.Range(4f, 9f);
            }
            Spring.Step(ref _earLeft, ref _earLeftSpeed, 0f, 0.09f, deltaTime);
            Spring.Step(ref _earRight, ref _earRightSpeed, 0f, 0.09f, deltaTime);

            // It lies along the ground it's on.
            Vector2 front = Turn.Heading(Yaw) * SlopeLook;
            float rise = Sim.Land.Ground(Position.Xz + front) - Sim.Land.Ground(Position.Xz - front);
            _slope = Spring.Damp(_slope, -MathF.Atan2(rise, SlopeLook * 2f), 0.08f, deltaTime);
        }

        public override void Pose(Span<Matrix4> skin)
        {
            var pose = new RabbitPose
            {
                Pitch = _lean,
                Rear = _rear,
                Crouch = _crouch,
                HeadPitch = _headPitch + _chew * MathF.Sin(Sim.Now * 24f + Size) * 0.03f,
                HeadYaw = _headYaw,
                EarsBack = _earsBack,
                EarLeft = _earLeft,
                EarRight = _earRight,
                Tail = _tail,
            };
            RabbitGait.Apply(ref pose, _through, _strength * _gait);

            Vector3 at = Position - Vector3.UnitY * (_crouch * 0.09f * Size);
            pose.Solve(_kind.Bones, Turn.Root(at, Turn.Yaw(Yaw) * Turn.Pitch(_slope), Size), skin);
        }
    }
}

using Dino_Engine.Animation;
using Dino_Engine.Modelling.Procedural.Wildlife;
using OpenTK.Mathematics;

namespace Dino_Engine.Wildlife
{
    /// <summary>The crabs of a stretch of beach, spread out along it, each minding its own business.</summary>
    public sealed class CrabColony : Party
    {
        private const float Spread = 13f;
        private const float LongestGrass = 0.4f;    // Metres it comes up. Any more and it isn't a beach, whatever the land's made of.

        internal readonly CrabKind Kind;

        internal CrabColony(WildlifeSim sim, CrabKind kind, Resident home) : base(sim, home)
        {
            Kind = kind;
            for (int i = 0; i < home.Count; i++)
            {
                for (int attempt = 0; attempt < 6; attempt++)
                {
                    Vector2 at = home.At + new Vector2(sim.Random.Range(-Spread, Spread), sim.Random.Range(-Spread, Spread));
                    if (!Fits(at)) continue;
                    Members.Add(new Crab(sim, this, at));
                    break;
                }
            }
        }

        /// <summary>Open sand it could stand on.</summary>
        internal bool Fits(Vector2 at)
        {
            return (Sim.Land.HabitatAt(at) & Habitat.Beach) != 0 && Sim.Land.CanScuttle(at) && Sim.Land.GrassTop(at) < LongestGrass;
        }
    }

    /// <summary>
    /// One crab. It goes in short rushes with long stops, picking at the sand with one claw and then the other. Anything coming near gets faced, with its claws up and wide, and if it keeps coming the crab goes for
    /// the sea or its hole, flat out and sideways. It's always sideways, as that's how the legs bend, so it turns its side to where it's going and not its face. Ghost of Tsushima's crabs simply keep their distance from Jin,
    /// and that's the feel of it: you never get to stand on one.
    /// </summary>
    public sealed class Crab : Critter
    {
        private enum State { Picking, Walking, Facing, Running, Digging, Wading }

        private const float Alert = 1.7f;
        private const float SenseRate = 0.1f;
        private const float WalkSpeed = 1.15f;
        private const float RunSpeed = 7.2f;
        private const float DigTime = 0.75f;
        private const float DigDepth = 0.34f;
        private const float WadeDepth = 0.26f;
        private const float SeaSearch = 11f;

        private readonly CrabKind _kind;
        private readonly CrabColony _colony;
        private readonly CrabLegs _legs = new CrabLegs();

        private State _state = State.Picking;
        private float _until;
        private float _nextSense;
        private float _fright = float.PositiveInfinity;
        private Vector2 _away = Vector2.UnitX;
        private Vector2 _way;
        private Vector2 _velocity;
        private float _speed;
        private float _raise;
        private float _tilt;
        private float _roll;
        private float _feedLeft, _feedRight;
        private float _sink;
        private bool _placed;

        public override WildlifeShape Shape => _kind.Shape;
        public override float Reach => 0.75f * Size;
        public override string Doing => _state.ToString();

        internal Crab(WildlifeSim sim, CrabColony colony, Vector2 at) : base(sim, colony.Kind)
        {
            _kind = colony.Kind;
            _colony = colony;
            Yaw = sim.Random.Range(-MathF.PI, MathF.PI);
            Position = new Vector3(at.X, sim.Land.Ground(at), at.Y);
            _until = sim.Now + sim.Random.Range(0f, 3f);
            _nextSense = sim.Now + sim.Random.Range(0f, SenseRate);
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

            switch (_state)
            {
                case State.Picking:
                case State.Walking:
                    if (_fright < 1f) Run();
                    else if (_fright < Alert) Face();
                    else if (now >= _until) NextThing();
                    break;
                case State.Facing:
                    if (_fright < 1f) Run();
                    else if (_fright > Alert && now >= _until) Pick();
                    break;
                case State.Running:
                    if (now >= _until) AfterRunning();
                    break;
            }

            Move(deltaTime);
            Bearing(deltaTime);
        }

        private void Pick()
        {
            _state = State.Picking;
            _until = Sim.Now + Sim.Random.Range(1.5f, 5.5f);
            _speed = 0f;
        }

        private void Face()
        {
            _state = State.Facing;
            _until = Sim.Now + Sim.Random.Range(0.6f, 1.6f);
            _speed = 0f;
        }

        private void NextThing()
        {
            if (_state == State.Walking || Sim.Random.Float() < 0.35f)
            {
                Pick();
                return;
            }

            // A short rush along the beach, and not into the sea or up the dunes.
            for (int attempt = 0; attempt < 6; attempt++)
            {
                Vector2 way = Turn.Heading(Sim.Random.Range(-MathF.PI, MathF.PI));
                float rush = Sim.Random.Range(0.8f, 2.4f);
                Vector2 to = Position.Xz + way * rush * WalkSpeed * 1.2f;
                if (!_colony.Fits(to)) continue;
                _state = State.Walking;
                _way = way;
                _speed = WalkSpeed * Sim.Random.Range(0.8f, 1.3f);
                _until = Sim.Now + rush;
                return;
            }
            Pick();
        }

        private void Run()
        {
            _colony.Frightened = true;
            _state = State.Running;
            _speed = RunSpeed * Sim.Random.Range(0.9f, 1.1f);
            _until = Sim.Now + Sim.Random.Range(0.8f, 1.6f);
            _way = SafestWay();
        }

        // The sea if it's near and not towards whatever's coming, or else straight away.
        private Vector2 SafestWay()
        {
            Vector2 at = Position.Xz;
            for (int i = 0; i < 12; i++)
            {
                Vector2 way = Turn.Heading(i * (MathF.Tau / 12f));
                if (Vector2.Dot(way, _away) < -0.2f) continue;
                if (Sim.Land.World.WaterDepthAt(at + way * SeaSearch) > 0.15f) return way;
            }
            return _away;
        }

        private void AfterRunning()
        {
            if (_fright > Alert)
            {
                Face();
                return;
            }

            // Still not far enough: it isn't going to be, so it goes down its hole, whatever's left of the beach.
            _state = State.Digging;
            _until = Sim.Now + DigTime;
            _speed = 0f;
        }

        private void Move(float deltaTime)
        {
            Vector2 at = Position.Xz;
            float ease = _state == State.Running ? 0.04f : 0.08f;
            _velocity = Vector2.Lerp(_velocity, _way * _speed, Spring.Share(ease, deltaTime));

            if (_speed > 0f)
            {
                Vector2 next = at + _velocity * deltaTime;
                bool wet = Sim.Land.World.WaterDepthAt(next) > 0.02f;
                if (wet && _state == State.Running) _state = State.Wading;
                else if (!wet && !Sim.Land.CanScuttle(next))
                {
                    // Something's in the way. It stops, and there's no more to it.
                    if (_state == State.Running) _until = Sim.Now;
                    else Pick();
                    next = at;
                    _velocity = Vector2.Zero;
                }
                at = next;
            }

            float ground = Sim.Land.Ground(at);
            if (_state == State.Wading)
            {
                float depth = Sim.Land.World.WaterDepthAt(at);
                _sink = MathF.Min(_sink + deltaTime * 0.8f, 1f);
                Fade = 1f - Math.Clamp((depth - WadeDepth * 0.5f) / WadeDepth, 0f, 1f);
                if (Fade <= 0f) Gone = true;
            }
            else if (_state == State.Digging)
            {
                float through = 1f - (_until - Sim.Now) / DigTime;
                _sink = through;
                Fade = 1f - Math.Clamp((through - 0.72f) / 0.28f, 0f, 1f);
                if (through >= 1f) Gone = true;
            }

            Position = new Vector3(at.X, ground - _sink * DigDepth * Size, at.Y);
        }

        private void Bearing(float deltaTime)
        {
            // A crab turns its side to where it's going, or its face to what it's afraid of.
            float yaw = Yaw;
            if (_state == State.Facing) yaw = Turn.YawOf(-_away);
            else if (_state is State.Walking or State.Running or State.Wading)
            {
                float flank = MathF.Atan2(-_way.Y, _way.X);
                yaw = MathF.Abs(Spring.Shortest(flank - Yaw)) < MathF.PI * 0.5f ? flank : flank + MathF.PI;
            }
            Yaw = Spring.DampAngle(Yaw, yaw, _state == State.Running ? 0.05f : 0.14f, deltaTime);

            bool facing = _state == State.Facing || _state == State.Digging;
            float raise = facing ? 1f : _state == State.Running ? 0.3f : 0f;
            _raise = Spring.Damp(_raise, raise, 0.06f, deltaTime);
            _tilt = Spring.Damp(_tilt, facing ? 0.26f : 0f, 0.07f, deltaTime);

            // It shrugs itself down into the sand.
            _roll = _state == State.Digging ? MathF.Sin(Sim.Now * 34f) * 0.14f : Spring.Damp(_roll, 0f, 0.04f, deltaTime);

            float feeding = _state == State.Picking ? 1f : 0f;
            float beat = MathF.Sin(Sim.Now * 3.1f + Size * 20f);
            _feedLeft = Spring.Damp(_feedLeft, MathF.Max(beat, 0f) * feeding, 0.05f, deltaTime);
            _feedRight = Spring.Damp(_feedRight, MathF.Max(-beat, 0f) * feeding, 0.05f, deltaTime);

            if (!Sim.InView(Position.Xz, 4f)) return;

            Matrix4 root = Turn.Root(Position, Turn.Yaw(Yaw), Size);
            if (!_placed)
            {
                _placed = true;
                _legs.Place(root, Sim.Ground);
            }
            _legs.Step(root, new Vector3(_velocity.X, 0f, _velocity.Y), Sim.Ground, deltaTime);
        }

        public override void Pose(Span<Matrix4> skin)
        {
            Matrix4 root = Turn.Root(Position, Turn.Yaw(Yaw), Size);
            if (!_placed)
            {
                _placed = true;
                _legs.Place(root, Sim.Ground);
            }
            _legs.Solve(_kind.Bones, new CrabPose { Tilt = _tilt, Roll = _roll, Raise = _raise, FeedLeft = _feedLeft, FeedRight = _feedRight }, root, skin);
        }
    }
}

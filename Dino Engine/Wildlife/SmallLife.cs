using Dino_Engine.Animation;
using Dino_Engine.Modelling.Procedural.Wildlife;
using OpenTK.Mathematics;

namespace Dino_Engine.Wildlife
{
    /// <summary>A few butterflies working one patch of flowers.</summary>
    public sealed class Swarm : Party
    {
        internal const float Roam = 12f;

        internal readonly ButterflyKind Kind;
        internal readonly Vector2 Centre;

        internal Swarm(WildlifeSim sim, ButterflyKind kind, Resident home) : base(sim, home)
        {
            Kind = kind;
            Centre = home.At;
            for (int i = 0; i < home.Count; i++)
            {
                Vector2 at = home.At + new Vector2(sim.Random.Range(-Roam, Roam), sim.Random.Range(-Roam, Roam)) * 0.6f;
                Members.Add(new Butterfly(sim, this, at));
            }
        }

        public override void Step(float deltaTime) => Shelter(deltaTime);
    }

    /// <summary>
    /// One butterfly. Nothing flies like one: its body rises on each downstroke and falls between, which is the bumpy track everyone knows them by, and it can't hold a line, which is thought to be how it survives birds.
    /// So its way wanders, its height bounces with its wings, and every so often it comes down on something and sits, with its wings slowly opening and shutting.
    /// </summary>
    public sealed class Butterfly : Critter
    {
        private const float WingRate = 7f;          // Hz. Real ones are nearer ten, which a frame can't show.
        private const float Bounce = 0.17f;         // Metres each stroke lifts it.
        private const float Speed = 2.6f;
        private const float Scare = 3.5f;

        private readonly Swarm _swarm;
        private readonly ButterflyKind _kind;
        private readonly float _phaseOffset;
        private Vector2 _at;
        private float _hover;
        private float _hoverGoal;
        private float _stroke;
        private float _turn;
        private float _nextTurn;
        private float _restUntil;
        private float _nextRest;
        private float _wings;
        private float _pitch;
        private float _scaredUntil;
        private float _grass;
        private bool _resting;

        public override WildlifeShape Shape => _kind.Shape;
        public override float Reach => _kind.Look.Span * Size;
        public override string Doing => _resting ? "Resting" : "Flying";

        internal Butterfly(WildlifeSim sim, Swarm swarm, Vector2 at) : base(sim, swarm.Kind)
        {
            _swarm = swarm;
            _kind = swarm.Kind;
            _at = at;
            _phaseOffset = sim.Random.Range(0f, MathF.Tau);
            Yaw = sim.Random.Range(-MathF.PI, MathF.PI);
            _hover = _hoverGoal = sim.Random.Range(0.9f, 2f);
            _nextRest = sim.Now + sim.Random.Range(2f, 12f);
            _grass = sim.Land.GrassTop(at);
            Position = new Vector3(at.X, sim.Land.Ground(at) + _grass + _hover, at.Y);
        }

        public override void Step(float deltaTime)
        {
            float now = Sim.Now;
            float grass = Sim.Land.RoughGround(_at) + _grass;
            float fright = Sim.Fright(_at, _kind.Shy, out Vector2 away);
            if (fright < 1f && now >= _scaredUntil)
            {
                _scaredUntil = now + 1.4f;
                _resting = false;
                Yaw = Turn.YawOf(away) + Sim.Random.Range(-0.7f, 0.7f);
                _hoverGoal = Sim.Random.Range(2.4f, 4.2f);
            }

            if (_resting)
            {
                _wings = Spring.Damp(_wings, 1.15f + 0.3f * MathF.Sin(now * 1.3f + _phaseOffset), 0.12f, deltaTime);
                _pitch = Spring.Damp(_pitch, 0f, 0.1f, deltaTime);
                if (now >= _restUntil)
                {
                    _resting = false;
                    _nextRest = now + Sim.Random.Range(4f, 16f);
                }
                Position = new Vector3(_at.X, grass + _hover, _at.Y);
                return;
            }

            if (now >= _nextTurn)
            {
                _nextTurn = now + Sim.Random.Range(0.25f, 0.9f);
                _turn = Sim.Random.Range(-3.4f, 3.4f);
                _hoverGoal = now < _scaredUntil ? _hoverGoal : Sim.Random.Range(0.5f, 1.9f);
                _grass = Spring.Damp(_grass, Sim.Land.GrassTop(_at), 0.25f, 0.5f);
            }

            // Tethered to its flowers, however it wanders.
            Vector2 home = _swarm.Centre - _at;
            if (home.LengthSquared > Swarm.Roam * Swarm.Roam) _turn = Spring.Shortest(Turn.YawOf(home) - Yaw) * 2.6f;

            Yaw += _turn * deltaTime;
            float pace = Speed * (now < _scaredUntil ? 1.7f : 1f) * (0.75f + 0.25f * MathF.Sin(now * 1.9f + _phaseOffset));
            _at += Turn.Heading(Yaw) * pace * deltaTime;

            _stroke += MathF.Tau * WingRate * deltaTime;
            if (_stroke > MathF.Tau) _stroke -= MathF.Tau;
            _wings = 0.28f + 0.96f * (0.5f + 0.5f * MathF.Sin(_stroke));
            _pitch = -0.4f + 0.3f * MathF.Sin(_stroke - 0.7f);

            _hover = Spring.Damp(_hover, _hoverGoal, 0.5f, deltaTime);
            float bounce = MathF.Sin(_stroke + 0.6f) * Bounce;
            Position = new Vector3(_at.X, grass + _hover + bounce, _at.Y);

            if (now >= _nextRest && now >= _scaredUntil && _hover < 1.2f)
            {
                _resting = true;
                _hover = 0.1f;
                _restUntil = now + Sim.Random.Range(2.5f, 8f);
            }
        }

        public override void Pose(Span<Matrix4> skin)
        {
            new ButterflyPose { Wings = _wings, Pitch = _pitch }.Solve(_kind.Bones, Turn.Root(Position, Turn.Yaw(Yaw), Size), skin);
        }
    }

    /// <summary>The dragonflies working a stretch of water's edge.</summary>
    public sealed class Patrol : Party
    {
        internal readonly DragonflyKind Kind;
        internal readonly Vector2 Centre;

        internal Patrol(WildlifeSim sim, DragonflyKind kind, Resident home) : base(sim, home)
        {
            Kind = kind;
            Centre = home.At;
            for (int i = 0; i < home.Count; i++) Members.Add(new Dragonfly(sim, this));
        }

        public override void Step(float deltaTime) => Shelter(deltaTime);
    }

    /// <summary>A dragonfly hangs dead still in the air, and then isn't there: it's somewhere else, as if it had been cut from one frame to the next. There's almost nothing between the two.</summary>
    public sealed class Dragonfly : Critter
    {
        private const float Roam = 11f;
        private const float DartSpeed = 11f;

        private readonly Patrol _patrol;
        private readonly DragonflyKind _kind;
        private Vector3 _from, _to;
        private float _through = 1f;
        private float _dartTime;
        private float _until;
        private float _stroke;
        private float _pitch;
        private float _bank;
        private float _offset;

        public override WildlifeShape Shape => _kind.Shape;
        public override float Reach => _kind.Look.Length * 1.4f * Size;
        public override string Doing => _through < 1f ? "Darting" : "Hovering";

        internal Dragonfly(WildlifeSim sim, Patrol patrol) : base(sim, patrol.Kind)
        {
            _patrol = patrol;
            _kind = patrol.Kind;
            _offset = sim.Random.Range(0f, 10f);
            Position = Somewhere();
            _to = Position;
            _until = sim.Now + sim.Random.Range(0f, 2f);
            Yaw = sim.Random.Range(-MathF.PI, MathF.PI);
        }

        private Vector3 Somewhere()
        {
            Vector2 at = _patrol.Centre + new Vector2(Sim.Random.Range(-Roam, Roam), Sim.Random.Range(-Roam, Roam));
            float below = MathF.Max(Sim.Land.WaterLevel(at), Sim.Land.RoughGround(at) + Sim.Land.GrassTop(at));
            return new Vector3(at.X, below + Sim.Random.Range(0.9f, 1.8f), at.Y);
        }

        public override void Step(float deltaTime)
        {
            float now = Sim.Now;
            if (_through >= 1f)
            {
                float fright = Sim.Fright(Position.Xz, _kind.Shy, out _);
                if (now >= _until || fright < 1f)
                {
                    _from = Position;
                    _to = Somewhere();
                    float length = (_to - _from).Length;
                    _dartTime = MathF.Max(length / DartSpeed, 0.08f);
                    _through = 0f;
                    Yaw = Turn.YawOf((_to - _from).Xz);
                }
                float hover = MathF.Sin(now * 2.3f + _offset) * 0.05f;
                Position = _to + Vector3.UnitY * hover;
            }
            else
            {
                _through = MathF.Min(_through + deltaTime / _dartTime, 1f);
                float t = _through * _through * (3f - 2f * _through);
                Position = Vector3.Lerp(_from, _to, t);
                if (_through >= 1f) _until = now + Sim.Random.Range(0.5f, 3.2f);
            }

            _stroke += MathF.Tau * 12f * deltaTime;
            if (_stroke > MathF.Tau) _stroke -= MathF.Tau;
            bool moving = _through < 1f;
            _pitch = Spring.Damp(_pitch, moving ? 0.26f : -0.07f, 0.05f, deltaTime);
            _bank = Spring.Damp(_bank, moving ? 0.1f : 0f, 0.05f, deltaTime);
        }

        public override void Pose(Span<Matrix4> skin)
        {
            new DragonflyPose { Stroke = _stroke, Pitch = _pitch, Bank = _bank }.Solve(_kind.Bones, Turn.Root(Position, Turn.Yaw(Yaw), Size), skin);
        }
    }

    /// <summary>The frogs on a stretch of bank, facing the water, ready to be in it.</summary>
    public sealed class Chorus : Party
    {
        private const float Spread = 9f;

        internal readonly FrogKind Kind;

        internal Chorus(WildlifeSim sim, FrogKind kind, Resident home) : base(sim, home)
        {
            Kind = kind;
            for (int i = 0; i < home.Count; i++)
            {
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    Vector2 at = home.At + new Vector2(sim.Random.Range(-Spread, Spread), sim.Random.Range(-Spread, Spread));
                    if (!sim.Land.CanScuttle(at) || sim.Land.HabitatAt(at) != Habitat.Bank) continue;
                    if (attempt < 4 && sim.Land.GrassTop(at) > 0.6f) continue;
                    Members.Add(new Frog(sim, this, at));
                    break;
                }
            }
        }
    }

    /// <summary>A frog is a thing that's sitting there, and then a ring on the water. It doesn't move at all until it's frightened, and it never goes away from the water.</summary>
    public sealed class Frog : Critter
    {
        private const float LeapTime = 0.42f;
        private const float LeapLength = 3f;
        private const float LeapHeight = 0.72f;
        private const float WaterSearch = 8f;

        private readonly Chorus _chorus;
        private readonly FrogKind _kind;
        private Vector2 _from, _to;
        private float _through = -1f;
        private bool _toWater;
        private float _leap;
        private float _nextSense;
        private float _nextShuffle;

        public override WildlifeShape Shape => _kind.Shape;
        public override float Reach => 0.6f * Size;
        public override string Doing => _through >= 0f ? "Leaping" : "Sitting";

        internal Frog(WildlifeSim sim, Chorus chorus, Vector2 at) : base(sim, chorus.Kind)
        {
            _chorus = chorus;
            _kind = chorus.Kind;
            Position = new Vector3(at.X, sim.Land.Ground(at), at.Y);
            Yaw = TowardsWater(at, out _);
            _nextShuffle = sim.Now + sim.Random.Range(6f, 30f);
        }

        private float TowardsWater(Vector2 at, out bool found)
        {
            found = false;
            for (int i = 0; i < 12; i++)
            {
                float yaw = i * (MathF.Tau / 12f);
                if (Sim.Land.World.WaterDepthAt(at + Turn.Heading(yaw) * WaterSearch * 0.5f) <= 0.1f) continue;
                found = true;
                return yaw;
            }
            return Sim.Random.Range(-MathF.PI, MathF.PI);
        }

        private void Go(float yaw, float length, bool toWater)
        {
            _from = Position.Xz;
            _to = _from + Turn.Heading(yaw) * length;
            Yaw = yaw;
            _through = 0f;
            _toWater = toWater;
        }

        public override void Step(float deltaTime)
        {
            float now = Sim.Now;
            if (_through < 0f)
            {
                _leap = Spring.Damp(_leap, 0f, 0.05f, deltaTime);
                if (now >= _nextSense)
                {
                    _nextSense = now + 0.12f;
                    if (Sim.Fright(Position.Xz, _kind.Shy, out Vector2 away) < 1f)
                    {
                        _chorus.Frightened = true;
                        float water = TowardsWater(Position.Xz, out bool found);
                        Go(found ? water : Turn.YawOf(away), LeapLength * Sim.Random.Range(0.9f, 1.2f), found);
                    }
                    else if (now >= _nextShuffle)
                    {
                        _nextShuffle = now + Sim.Random.Range(8f, 34f);
                        float yaw = Yaw + Sim.Random.Range(-1.2f, 1.2f);
                        Vector2 to = Position.Xz + Turn.Heading(yaw) * 0.8f;
                        if (Sim.Land.CanScuttle(to)) Go(yaw, 0.8f, false);
                    }
                }
                return;
            }

            _through = MathF.Min(_through + deltaTime / LeapTime, 1f);
            float t = _through;
            _leap = Spring.Damp(_leap, t < 0.72f ? 1f : 0.45f, 0.022f, deltaTime);
            Vector2 at = Vector2.Lerp(_from, _to, t);
            float top = _toWater ? LeapHeight : LeapHeight * 0.34f;
            float start = Sim.Land.Ground(_from);
            float end = _toWater ? MathF.Max(Sim.Land.Ground(_to), Sim.Land.WaterLevel(_to)) : Sim.Land.Ground(_to);
            Position = new Vector3(at.X, start + (end - start) * t + 4f * t * (1f - t) * top, at.Y);
            if (_through < 1f) return;

            _through = -1f;
            if (!_toWater) return;

            // A plop, a ring on the water, and it's gone.
            Vector3 entry = new Vector3(at.X, Sim.Land.WaterLevel(at), at.Y);
            Sim.Splash(entry, 0.75f, 0.09f);
            Sim.MakeNoise(LifeNoiseKind.Plop, entry);
            Gone = true;
        }

        public override void Pose(Span<Matrix4> skin)
        {
            float t = _through < 0f ? 0f : _through;
            float pitch = _through < 0f ? 0f : (t - 0.5f) * 1.5f;
            new FrogPose { Leap = _leap, Pitch = pitch }.Solve(_kind.Bones, Turn.Root(Position, Turn.Yaw(Yaw), Size), skin);
        }
    }

    /// <summary>Whatever's under the water here, which is only ever seen as one fish jumping, now and then. It doesn't exist until it does.</summary>
    public sealed class Shoal : Party
    {
        private const float Reach = 14f;
        private const float LeastGap = 5f, MostGap = 22f;

        private readonly FishKind _kind;
        private readonly Vector2 _centre;
        private float _next;

        public override bool Finished => false;

        internal Shoal(WildlifeSim sim, FishKind kind, Resident home) : base(sim, home)
        {
            _kind = kind;
            _centre = home.At;
            _next = sim.Now + sim.Random.Range(1f, MostGap);
        }

        public override bool StillNear(float margin) => Sim.InView(_centre, margin);

        public override void Step(float deltaTime)
        {
            if (Sim.Now < _next) return;
            _next = Sim.Now + Sim.Random.Range(LeastGap, MostGap);

            Vector2 at = _centre + new Vector2(Sim.Random.Range(-Reach, Reach), Sim.Random.Range(-Reach, Reach));
            if (Sim.Land.World.WaterDepthAt(at) < 0.9f || !Sim.InView(at, -3f)) return;
            Members.Add(new Fish(Sim, _kind, at));
        }
    }

    /// <summary>A fish out of the water, for about a second: up, curving over the top with its tail going, and in again.</summary>
    public sealed class Fish : Critter
    {
        private const float Gravity = 26f;

        private readonly FishKind _kind;
        private readonly Vector3 _from;
        private readonly Vector2 _way;
        private readonly float _flight;
        private readonly float _rise;
        private readonly float _reach;
        private float _time;
        private float _pitch;
        private float _bend;
        private float _lag;

        public override WildlifeShape Shape => _kind.Shape;
        public override float Reach => _kind.Look.Length * Size;
        public override string Doing => "Leaping";

        internal Fish(WildlifeSim sim, FishKind kind, Vector2 at) : base(sim, kind)
        {
            _kind = kind;
            float level = sim.Land.WaterLevel(at);
            _from = new Vector3(at.X, level - 0.1f, at.Y);
            _way = Turn.Heading(sim.Random.Range(-MathF.PI, MathF.PI));
            Yaw = Turn.YawOf(_way);
            _rise = sim.Random.Range(0.9f, 1.7f);
            _flight = 2f * MathF.Sqrt(2f * _rise / Gravity);
            _reach = sim.Random.Range(1.4f, 2.8f);
            Position = _from;
            sim.Splash(new Vector3(at.X, level, at.Y), 0.8f, 0.12f);
            sim.MakeNoise(LifeNoiseKind.Splash, Position, 0.5f);
        }

        public override void Step(float deltaTime)
        {
            _time += deltaTime;
            float t = _time / _flight;
            float y = 4f * t * (1f - t) * _rise;
            Vector2 at = _from.Xz + _way * (_reach * t);
            Position = new Vector3(at.X, _from.Y + y, at.Y);

            // Nose up on the way out and down on the way in, along the arc it's on.
            _pitch = (t - 0.5f) * 2.3f;
            float wag = MathF.Sin(_time * 34f);
            _bend = wag * 0.5f;
            _lag = MathF.Sin(_time * 34f + 1.1f) * 0.24f;

            if (t < 1f) return;
            Vector3 entry = new Vector3(at.X, _from.Y + 0.1f, at.Y);
            Sim.Splash(entry, 1f, 0.17f);
            Sim.MakeNoise(LifeNoiseKind.Splash, entry);
            Gone = true;
        }

        public override void Pose(Span<Matrix4> skin)
        {
            new FishPose { Bend = _bend, Lag = _lag, Pitch = _pitch }.Solve(_kind.Bones, Turn.Root(Position, Turn.Yaw(Yaw), Size), skin);
        }
    }

    /// <summary>Bats, out over one place for as long as it's dark.</summary>
    public sealed class Colony : Party
    {
        internal readonly BatKind Kind;
        internal readonly Vector2 Centre;

        internal Colony(WildlifeSim sim, BatKind kind, Resident home) : base(sim, home)
        {
            Kind = kind;
            Centre = home.At;
            for (int i = 0; i < home.Count; i++) Members.Add(new Bat(sim, this));
        }

        public override void Step(float deltaTime)
        {
            // Day comes, and there's none to be seen.
            if (Sim.View.Night >= 0.3f) return;
            foreach (Critter member in Members) member.Gone = true;
        }
    }

    /// <summary>A bat doesn't glide or hold a line. It's after something that dodges, so it's all quick turns and sudden drops, near the same place over and over, and its wings never stop.</summary>
    public sealed class Bat : Critter
    {
        private const float Speed = 9f;
        private const float Roam = 15f;

        private readonly Colony _colony;
        private readonly BatKind _kind;
        private float _stroke;
        private float _turn;
        private float _nextJink;
        private float _height;
        private float _heightGoal;
        private float _bank;

        public override WildlifeShape Shape => _kind.Shape;
        public override float Reach => (_kind.Look.Arm + _kind.Look.Hand) * 1.5f * Size;
        public override string Doing => "Hunting";

        internal Bat(WildlifeSim sim, Colony colony) : base(sim, colony.Kind)
        {
            _colony = colony;
            _kind = colony.Kind;
            Yaw = sim.Random.Range(-MathF.PI, MathF.PI);
            _height = _heightGoal = sim.Random.Range(4f, 8f);
            Vector2 at = colony.Centre + new Vector2(sim.Random.Range(-Roam, Roam), sim.Random.Range(-Roam, Roam)) * 0.6f;
            Position = new Vector3(at.X, sim.Land.RoughGround(at) + _height, at.Y);
            _stroke = sim.Random.Range(0f, MathF.Tau);
        }

        public override void Step(float deltaTime)
        {
            float now = Sim.Now;
            if (now >= _nextJink)
            {
                _nextJink = now + Sim.Random.Range(0.14f, 0.6f);
                _turn = Sim.Random.Range(-5f, 5f);
                _heightGoal = Sim.Random.Range(3.5f, 9f);
            }

            Vector2 at = Position.Xz;
            Vector2 home = _colony.Centre - at;
            if (home.LengthSquared > Roam * Roam) _turn = Math.Clamp(Spring.Shortest(Turn.YawOf(home) - Yaw) * 4f, -5f, 5f);

            Yaw += _turn * deltaTime;
            at += Turn.Heading(Yaw) * Speed * deltaTime;
            _height = Spring.Damp(_height, _heightGoal, 0.2f, deltaTime);
            Position = new Vector3(at.X, Sim.Land.RoughGround(at) + _height, at.Y);
            _bank = Spring.Damp(_bank, -_turn * 0.2f, 0.05f, deltaTime);
            _stroke += MathF.Tau * 9f * deltaTime;
            if (_stroke > MathF.Tau) _stroke -= MathF.Tau;
        }

        public override void Pose(Span<Matrix4> skin)
        {
            var pose = new BirdPose { Stroke = _stroke, Beat = 0.85f, Bank = _bank, Pitch = 0.08f };
            pose.Solve(_kind.Bones, Turn.Root(Position - Vector3.UnitY * (BirdBody.Height(_kind.Look) * Size), Turn.Yaw(Yaw), Size), skin);
        }
    }
}

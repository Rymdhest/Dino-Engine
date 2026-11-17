using Dino_Engine.Animation;
using Dino_Engine.Modelling.Procedural.Wildlife;
using OpenTK.Mathematics;

namespace Dino_Engine.Wildlife
{
    /// <summary>
    /// Birds that go together. On the ground each feeds by itself. When one goes, the rest go after it in a wave that spreads out from it, as it does through a real flock (sandpipers do this at some tens of metres a
    /// second), and a bigger flock takes fright sooner, for having more eyes. In the air it's Reynolds's boids, with what Ballerini's measurements of starlings say about how far it reaches: each bird heeds its nearest few
    /// and not everyone in range. On top of that it has somewhere to be, as Hildenbrandt's model of real flocks needs, or a flock only wanders off.
    /// </summary>
    public sealed class Flock : Party
    {
        internal enum Plan { Feeding, Hidden, Fleeing, Settling, Passing }

        private const float WaveSpeed = 15f;            // Metres a second, through a flock on the ground.
        private const float BiggerIsWarier = 0.045f;    // How much further off each bird makes a flock go.
        private const float Spread = 4.2f;
        private const int MostFlights = 3;              // After this it's had enough of this place.
        private const float LandFrom = 34f, LandTo = 78f;
        private const float RoostTop = 1.2f;

        internal readonly BirdKind Kind;
        internal Plan Stage = Plan.Feeding;
        internal Vector2 Goal;
        internal float GoalHeight = -1f;
        internal bool Roosted;                          // Where it's going is a tree, and it's gone once it gets there.
        internal bool OnRoofs;                          // Where it's going, or where it is, is along the edges of roofs, a perch to each bird (Bird.Perch).
        private readonly List<SkyFloor.Perch> _perches = new();
        private int _nextPerch;
        private const float RoofShare = 0.6f;           // How often what sits on roofs goes to one when there's one to go to.
        private const float RoofFrom = 12f, RoofTo = 60f;   // Metres off that a roof it flies to is.
        private const float RoofGather = 7f;            // Metres round the first perch it takes the rest from, so a flock sits along one roof or two.
        private const float RoofCalm = 0.55f;           // How much nearer something can come to a bird up on a roof than on the ground.
        private readonly int _count;
        private int _flights;
        private float _nextSense;
        private StandingTree _tree;

        internal Flock(WildlifeSim sim, BirdKind kind, Resident home) : base(sim, home)
        {
            Kind = kind;
            _count = home.Count;
            Goal = home.At;

            if (kind.Roosts && home.Place is Habitat.Edge or Habitat.Wood && sim.Leaves.TryTreeNear(home.At, 22f, out _tree))
            {
                Stage = Plan.Hidden;
                return;
            }

            // In a town, often already up along a roof.
            if (kind.Rooftops && sim.Random.Float() < RoofShare && TryRoofs(home.At, Vector2.Zero, 0f, RoofGather * 3f, home.Count))
            {
                OnRoofs = true;
                for (int i = 0; i < _perches.Count; i++) Members.Add(new Bird(sim, this, _perches[i]));
                if (Members.Count > 0) return;
                OnRoofs = false;
            }

            float spread = Spread * kind.Scatter;
            for (int i = 0; i < home.Count; i++)
            {
                for (int attempt = 0; attempt < 6; attempt++)
                {
                    Vector2 at = home.At + new Vector2(sim.Random.Range(-spread, spread), sim.Random.Range(-spread, spread));
                    if (!Fits(at)) continue;
                    Members.Add(new Bird(sim, this, at));
                    break;
                }
            }
        }

        /// <summary>Whether a bird of this kind would stand here: on the ground it lives on, and not in grass over its head.</summary>
        internal bool Fits(Vector2 at)
        {
            if (!Sim.Land.CanScuttle(at) || (Kind.Lives & Sim.Land.HabitatAt(at)) == 0) return false;
            // Nowhere under a crown or a roof, or by anything taller than a fence, as getting up from there or coming down to it would go through it.
            if (Sim.Land.Over(at) > ClearOver) return false;
            return !Kind.Walks || Sim.Land.GrassTop(at) < 0.6f;
        }

        internal const float ClearOver = 1.6f;      // Metres over the ground that what stands near a bird on the ground may reach.

        private Flock(WildlifeSim sim, BirdKind kind) : base(sim, null)
        {
            Kind = kind;
            Stage = Plan.Passing;
        }

        /// <summary>A few birds crossing the sky on their way somewhere else, from out of view on one side to out of view on the other.</summary>
        internal static Flock Passing(WildlifeSim sim, BirdKind kind)
        {
            var flock = new Flock(sim, kind);
            WildlifeView view = sim.View;
            Vector2 middle = (view.Least + view.Most) * 0.5f;
            Vector2 half = (view.Most - view.Least) * 0.5f;
            Vector2 way = Turn.Heading(sim.Random.Range(-MathF.PI, MathF.PI));
            Vector2 side = new Vector2(-way.Y, way.X);

            // How far it is to the edge of the view this way: a view can be twice as wide as it's deep, or more.
            float toEdge = MathF.Min(half.X / MathF.Max(MathF.Abs(way.X), 1e-3f), half.Y / MathF.Max(MathF.Abs(way.Y), 1e-3f));
            float aside = sim.Random.Range(-0.5f, 0.5f) * MathF.Min(half.X, half.Y);
            Vector2 start = middle - way * (toEdge + 20f) + side * aside;
            flock.Goal = middle + way * (toEdge + 90f) + side * aside;

            int count = kind.Group.Min + sim.Random.Int(0, Math.Max(kind.Group.Max - kind.Group.Min, 0));
            for (int i = 0; i < count; i++)
            {
                Vector2 at = start + new Vector2(sim.Random.Range(-7f, 7f), sim.Random.Range(-7f, 7f));
                float height = sim.Land.Overhead(at) + kind.Height * sim.Random.Range(1f, 1.3f);
                flock.Members.Add(new Bird(sim, flock, new Vector3(at.X, height, at.Y), way));
            }
            return flock;
        }

        public override bool Finished => Stage != Plan.Hidden && Members.Count == 0;

        public override bool StillNear(float margin)
        {
            if (Stage == Plan.Hidden) return Sim.InView(_tree.At, margin);
            return base.StillNear(margin);
        }

        public override void Step(float deltaTime)
        {
            if (Sim.Now < _nextSense) return;
            _nextSense = Sim.Now + 0.14f;

            // Those already down keep watch while the rest are still coming in.
            if (Stage is Plan.Feeding or Plan.Settling) SenseOnGround();
            else if (Stage == Plan.Hidden) SenseInTree();
        }

        private float Wariness => Kind.Shy * (1f + BiggerIsWarier * _count);

        private void SenseOnGround()
        {
            for (int i = 0; i < Members.Count; i++)
            {
                Bird bird = (Bird)Members[i];
                if (bird.Aloft) continue;
                float fright = Sim.Fright(bird.Position.Xz, bird.OnRoof ? Wariness * RoofCalm : Wariness, out Vector2 away);
                if (fright >= 1f) continue;
                Flee(bird, away);
                return;
            }
        }

        private void SenseInTree()
        {
            float fright = Sim.Fright(_tree.At, Wariness * 0.8f, out Vector2 away);
            if (fright >= 1f) return;

            // A crown that reaches up by the camera keeps its birds, as they'd come out of it into the lens.
            Vector3 top = new Vector3(_tree.At.X, _tree.CrownHigh * 0.9f + _tree.CrownLow * 0.1f, _tree.At.Y);
            if ((new Vector3(top.X, MathF.Max(top.Y, Sim.Land.Standing(top.Xz)), top.Z) - Sim.View.Camera).Length < Bird.ClearOfCamera + _tree.Radius) return;

            Frightened = true;
            for (int i = 0; i < _count; i++)
            {
                Vector2 off = Turn.Heading(Sim.Random.Range(-MathF.PI, MathF.PI)) * _tree.Radius * Sim.Random.Range(0.3f, 0.8f);
                // From the outside of the crown, where the tree really is over that spot, as one coming out from deep in it would fly out through its leaves.
                Vector3 from = top + new Vector3(off.X, Sim.Random.Range(-1.4f, RoostTop), off.Y);
                float crown = Sim.Land.Standing(from.Xz);
                if (float.IsFinite(crown)) from.Y = crown + Sim.Random.Range(0.1f, 0.4f);
                var bird = new Bird(Sim, this, from, away);
                bird.Perch(Sim.Now + Sim.Random.Range(0f, 0.3f), away);
                Members.Add(bird);
            }

            // It shakes the leaves off as they go.
            Sim.Leaves.Shake(_tree, 8 + _count * 3);
            Sim.MakeNoise(LifeNoiseKind.Wings, top, _count);
            if (Kind.Calls) Sim.MakeNoise(CallOf(Kind), top);
            Choose(away, top.Xz);
        }

        internal void Flee(Bird first, Vector2 away)
        {
            Frightened = true;
            for (int i = 0; i < Members.Count; i++)
            {
                Bird bird = (Bird)Members[i];
                float distance = (bird.Position.Xz - first.Position.Xz).Length;
                bird.Burst(Sim.Now + distance / WaveSpeed + Sim.Random.Range(0f, 0.09f), away);
            }

            Sim.MakeNoise(LifeNoiseKind.Wings, first.Position, Members.Count);
            if (Kind.Calls) Sim.MakeNoise(CallOf(Kind), first.Position);
            Choose(away, first.Position.Xz);
        }

        private static LifeNoiseKind CallOf(BirdKind kind) => kind == Fauna.Gull ? LifeNoiseKind.Cry : LifeNoiseKind.Caw;

        // Somewhere to go: the same sort of place further on, or a tree for what sits in them, and out of here altogether once it's been bothered too often.
        private void Choose(Vector2 away, Vector2 from)
        {
            _flights++;
            Stage = Plan.Fleeing;
            Roosted = false;
            OnRoofs = false;

            if (_flights <= MostFlights)
            {
                if (Kind.Rooftops && Sim.Random.Float() < RoofShare && TryRoofs(from, away, RoofFrom, RoofTo, Members.Count))
                {
                    OnRoofs = true;
                    Goal = _perches[0].At.Xz;
                    GoalHeight = _perches[0].At.Y;
                    Stage = Plan.Settling;
                    return;
                }

                if (Kind.Roosts && Sim.Leaves.TryTreeAway(from, away, LandFrom, LandTo * 1.3f, out StandingTree tree))
                {
                    _tree = tree;
                    Goal = tree.At;
                    GoalHeight = tree.CrownHigh;
                    Roosted = true;
                    return;
                }

                Habitat wanted = Home?.Place ?? Habitat.Meadow;
                for (int attempt = 0; attempt < 12; attempt++)
                {
                    float turn = Sim.Random.Range(-0.85f, 0.85f);
                    float yaw = Turn.YawOf(away) + turn;
                    Vector2 at = from + Turn.Heading(yaw) * Sim.Random.Range(LandFrom, LandTo);
                    if (!Fits(at) || Sim.Fright(at, Wariness * 1.5f, out _) < 1f) continue;
                    Goal = at;
                    GoalHeight = -1f;
                    Stage = Plan.Settling;
                    return;
                }
            }

            // Nowhere near will do, so it's off out of sight.
            Goal = from + away * ((Sim.View.Most - Sim.View.Least).Length + 70f);
            GoalHeight = -1f;
        }

        // A roof's edge some way off, ahead of where it's going if it's going anywhere, with room along it for the flock, and nothing frightening near: its perches, nearest the first first.
        private readonly List<SkyFloor.Perch> _near = new();
        private readonly List<int> _skipped = new();
        private const float RoofGaps = 0.35f;
        private bool TryRoofs(Vector2 from, Vector2 away, float nearest, float furthest, int count)
        {
            _near.Clear();
            Sim.Land.Sky.PerchesNear(from, furthest, _near);
            _perches.Clear();
            _nextPerch = 0;
            if (_near.Count == 0) return false;

            int first = -1;
            float best = float.NegativeInfinity;
            for (int i = 0; i < _near.Count; i++)
            {
                Vector2 offset = _near[i].At.Xz - from;
                float distance = offset.Length;
                if (distance < nearest) continue;
                float ahead = away == Vector2.Zero || distance < 1e-3f ? 0f : Vector2.Dot(offset / distance, away);
                if (away != Vector2.Zero && ahead < 0.2f) continue;
                if (Sim.Fright(_near[i].At.Xz, Wariness * RoofCalm * 1.5f, out _) < 1f) continue;
                float score = ahead + Sim.Random.Range(0f, 0.8f);
                if (score <= best) continue;
                best = score;
                first = i;
            }
            if (first < 0) return false;

            _skipped.Clear();
            SkyFloor.Perch start = _near[first];
            if (count > 0 && _near.Count < count) return false;
            _perches.Add(start);
            while (_perches.Count < count)
            {
                int next = -1;
                float closest = RoofGather * RoofGather;
                for (int i = 0; i < _near.Count; i++)
                {
                    float gap = (_near[i].At - start.At).LengthSquared;
                    if (gap >= closest || _perches.Contains(_near[i]) || _skipped.Contains(i)) continue;
                    closest = gap;
                    next = i;
                }
                if (next < 0) break;
                // Some left empty, so the row has gaps in it as a real one does, and isn't birds set out a metre apart.
                if (Sim.Random.Float() < RoofGaps)
                {
                    _skipped.Add(next);
                    continue;
                }
                _perches.Add(_near[next]);
            }
            return _perches.Count >= count;
        }

        /// <summary>The next perch along the roof for one of them coming down, or false if they've all been given out.</summary>
        internal bool TryNextPerch(out SkyFloor.Perch perch)
        {
            perch = default;
            if (!OnRoofs || _nextPerch >= _perches.Count) return false;
            perch = _perches[_nextPerch++];
            return true;
        }

        /// <summary>One of them can't get down where they were going without going through something, so they all go somewhere else.</summary>
        internal void Resettle(Bird bird) => Choose(Turn.Heading(bird.Yaw), bird.Position.Xz);

        internal Vector2 Landing(Vector2 at) => Goal + new Vector2(Sim.Random.Range(-Spread, Spread), Sim.Random.Range(-Spread, Spread)) * Kind.Scatter;

        /// <summary>The last of them is down, so they're a flock on the ground again.</summary>
        internal void Landed()
        {
            for (int i = 0; i < Members.Count; i++)
            {
                if (((Bird)Members[i]).Aloft) return;
            }
            Stage = Plan.Feeding;
        }

        internal void Roost()
        {
            if (Members.Count <= 1) Stage = Plan.Hidden;
        }
    }

    /// <summary>
    /// One bird. Its head is the thing about it on the ground: birds look with quick turns and stops, and peck in short jabs. In the air its body banks into turns (Hemelrijk's birds roll to turn, and lose height for
    /// it), it pitches with its climb, and how it uses its wings is the species: a finch shuts them between bursts and dips, a gull barely moves them, a heron rows.
    /// </summary>
    public sealed class Bird : Critter
    {
        private enum State { Feeding, Waiting, Leaping, Flying, Landing }

        private const float FoldTime = 0.09f;
        private const float BurstFlap = 1.35f;      // How much faster it beats its wings getting off the ground.
        private const float Ceiling = 12f;          // Metres over the focus it stays below, whatever the ground ahead is doing, as the camera's not far above that.
        private const float LandBegins = 16f;
        private const float FlareBegins = 3.6f;
        private const int Neighbours = 6;           // What Ballerini found: not everyone near, but the nearest few.
        private const float Separation = 1.7f;
        private const float MostClimb = 0.9f;       // Of its speed, straight up.
        private const float ClearOfGround = 0.5f;
        private const float ClearOfSolid = 1.2f;    // Metres over the top of anything standing that it keeps, at the least.
        private const float LookAhead = 26f;        // Metres ahead it looks for what's in its way, which its turning and climbing can get it out of from there.
        private const float LookStep = 2.5f;        // Metres between looks, under the narrowest thing that stands (a crown, a tower) with its berth.
        private const float GoRound = 3f;
        private const float RoofOut = 9f;
        private const float RoofPatience = 12f;     // Seconds it tries for a roof's edge before giving it up.           // Metres out from a roof's edge it comes in to it from.
        internal const float ClearOfCamera = 14f;   // Metres it keeps from the camera, as the camera's often no higher than the tallest crowns.           // Metres over where it is, or over its ceiling, past which it goes round what's ahead rather than over.

        private readonly Flock _flock;
        private readonly BirdKind _kind;
        private readonly float _pace;
        private readonly float _height;

        private State _state;
        private Vector3 _velocity;
        private float _goesAt = -1f;
        private Vector2 _away;
        private Vector2 _slot;
        private bool _slotted;
        private float _footing;     // How far over the ground it stands: on top of the grass, for what perches.
        private float _hopFooting;
        private float _slotFooting;
        private int _balked;        // How many times running the way down to where it was coming down went through something.
        private bool _onRoof;       // Sitting on the edge of a roof, on its perch.
        private bool _roofSlot;     // Where it's coming down is a perch on a roof's edge.
        private SkyFloor.Perch _perch;
        private bool _hadPerch;
        private float _slottedAt;

        // On the ground.
        private float _nextMove;
        private float _peck, _peckGoal, _peckUntil;
        private int _pecks;
        private float _headYaw, _headYawGoal;
        private float _hop = 1f;
        private Vector2 _hopFrom, _hopTo;
        private float _walkUntil;
        private Vector2 _walkTo;
        private float _stride;

        // In the air.
        private float _stroke;
        private float _beat;
        private float _fold = 1f;
        private float _reach;
        private float _pitch;
        private float _bank;
        private float _legs = 1f;
        private float _tail;
        private float _crouch;
        private float _speed;
        private float _flapClock;
        private bool _glide;

        public bool Aloft => _state != State.Feeding;
        /// <summary>Sitting along the edge of a roof.</summary>
        public bool OnRoof => _onRoof && _state != State.Leaping && _state != State.Flying;
        /// <summary>Metres across from the perch it's sitting on, coming down to or just left, or infinity if it's never had one: a lab's, as up against a roof's edge is no fault.</summary>
        public float FromPerch => _hadPerch ? (Position.Xz - _perch.At.Xz).Length : float.PositiveInfinity;
        public override WildlifeShape Shape => _kind.Shape;
        public override float Reach => (_kind.Look.Arm + _kind.Look.Hand + _kind.Look.Body) * Size;
        public override string Doing => _state.ToString();

        // On the ground, feeding.
        internal Bird(WildlifeSim sim, Flock flock, Vector2 at) : base(sim, flock.Kind)
        {
            _flock = flock;
            _kind = flock.Kind;
            _pace = sim.Random.Range(0.92f, 1.1f);
            _height = sim.Random.Range(0.85f, 1.2f);
            Yaw = sim.Random.Range(-MathF.PI, MathF.PI);
            _footing = FootingAt(at);
            Position = new Vector3(at.X, sim.Land.Ground(at) + _footing, at.Y);
            _nextMove = sim.Now + sim.Random.Range(0f, 1.2f);
            _stroke = sim.Random.Range(0f, MathF.Tau);
        }

        // Already in the air.
        internal Bird(WildlifeSim sim, Flock flock, Vector3 at, Vector2 way) : this(sim, flock, at.Xz)
        {
            Position = at;
            _state = State.Flying;
            _speed = _kind.Cruise * _pace;
            Yaw = Turn.YawOf(way);
            _fold = 0f;
            _legs = 0f;
            _velocity = new Vector3(way.X, 0f, way.Y) * _speed;
        }

        // Up on the edge of a roof, facing out over the drop.
        internal Bird(WildlifeSim sim, Flock flock, SkyFloor.Perch perch) : this(sim, flock, perch.At.Xz)
        {
            _footing = 0f;
            Position = perch.At;
            Yaw = Turn.YawOf(perch.Out) + sim.Random.Range(-0.35f, 0.35f);
            _onRoof = true;
            _perch = perch;
            _hadPerch = true;
        }

        private float FootingAt(Vector2 at) => _kind.Walks ? 0f : Sim.Land.GrassTop(at);

        /// <summary>It was sitting in a tree all along, and it's about to not be.</summary>
        internal void Perch(float goesAt, Vector2 away)
        {
            _state = State.Waiting;
            _goesAt = goesAt;
            _away = away;
            _fold = 1f;
            _legs = 1f;
        }

        /// <summary>Sets it off, at the moment the fright reaches it.</summary>
        internal void Burst(float at, Vector2 away)
        {
            _goesAt = at;
            _away = away;
            if (_state == State.Feeding) _state = State.Waiting;
        }

        public override void Step(float deltaTime)
        {
            switch (_state)
            {
                case State.Feeding:
                    Feed(deltaTime);
                    break;
                case State.Waiting:
                    // It's seen the others go: down on its legs, and up.
                    _crouch = Spring.Damp(_crouch, 1f, 0.03f, deltaTime);
                    _peckGoal = 0f;
                    if (Sim.Now >= _goesAt) Leap();
                    break;
                case State.Leaping:
                case State.Flying:
                    Fly(deltaTime);
                    break;
                case State.Landing:
                    Land(deltaTime);
                    break;
            }

            _peck = Spring.Damp(_peck, _peckGoal, 0.028f, deltaTime);
            _headYaw = Spring.DampAngle(_headYaw, _headYawGoal, 0.02f, deltaTime);
        }

        private void Feed(float deltaTime)
        {
            float now = Sim.Now;
            if (_flock.Stage == Flock.Plan.Feeding) _crouch = Spring.Damp(_crouch, 0f, 0.05f, deltaTime);
            // On a roof it shuffles round to face out over the edge after it comes down, and stays put.
            if (_onRoof) Yaw = Spring.DampAngle(Yaw, Turn.YawOf(_perch.Out), 0.35f, deltaTime);

            // A peck is a stab down and a look up, a few at a time.
            if (_pecks > 0 && now >= _peckUntil)
            {
                if (_peckGoal > 0.5f)
                {
                    _peckGoal = 0.08f;
                    _peckUntil = now + Sim.Random.Range(0.08f, 0.2f);
                    _pecks--;
                }
                else
                {
                    _peckGoal = 1f;
                    _peckUntil = now + 0.09f;
                }
            }

            if (_hop < 1f)
            {
                _hop = MathF.Min(_hop + deltaTime / 0.17f, 1f);
                Vector2 at = Vector2.Lerp(_hopFrom, _hopTo, _hop);
                float footing = _footing + (_hopFooting - _footing) * _hop;
                Position = new Vector3(at.X, Sim.Land.Ground(at) + footing + 4f * _hop * (1f - _hop) * 0.13f * Size, at.Y);
                if (_hop >= 1f) _footing = _hopFooting;
                return;
            }

            if (now < _walkUntil)
            {
                Vector2 way = _walkTo - Position.Xz;
                float left = way.Length;
                if (left < 0.1f)
                {
                    _walkUntil = 0f;
                }
                else
                {
                    float speed = (0.7f + _kind.Look.Legs) * _pace;
                    Vector2 at = Position.Xz + way / left * MathF.Min(speed * deltaTime, left);
                    Yaw = Spring.DampAngle(Yaw, Turn.YawOf(way), 0.08f, deltaTime);
                    Position = new Vector3(at.X, Sim.Land.Ground(at), at.Y);

                    // It walks with its head, thrust forward with each step and held still as the body catches up.
                    _stride += deltaTime * speed / MathF.Max(_kind.Look.Legs * 1.8f * Size, 0.1f);
                    _peckGoal = 0.1f + 0.16f * (0.5f + 0.5f * MathF.Sin(_stride * 2f));
                }
                return;
            }

            if (now < _nextMove || _pecks > 0) return;
            _nextMove = now + Sim.Random.Range(0.25f, 1.5f);

            float roll = Sim.Random.Float();
            if (roll < 0.46f)
            {
                _pecks = Sim.Random.Int(1, 4);
                _peckGoal = 1f;
                _peckUntil = now + 0.09f;
            }
            else if (roll < 0.72f)
            {
                _headYawGoal = Sim.Random.Range(-1.1f, 1.1f);
                _peckGoal = 0f;
            }
            else
            {
                Wander();
            }
        }

        private void Wander()
        {
            if (_onRoof) return;
            float yaw = Yaw + Sim.Random.Range(-1.5f, 1.5f);
            float length = (_kind.Walks ? Sim.Random.Range(0.8f, 2.4f) : Sim.Random.Range(0.3f, 0.75f)) * Size;
            Vector2 to = Position.Xz + Turn.Heading(yaw) * length;
            if (!_flock.Fits(to)) return;

            if (_kind.Walks)
            {
                _walkTo = to;
                _walkUntil = Sim.Now + 4f;
                return;
            }
            Yaw = yaw;
            _hopFrom = Position.Xz;
            _hopTo = to;
            _hopFooting = FootingAt(to);
            _hop = 0f;
        }

        private void Leap()
        {
            _state = State.Leaping;
            // Off a roof it goes out over the edge, whichever way it's going after, as back over the roof is into it.
            Vector2 off = _onRoof && Vector2.Dot(_away, _perch.Out) < 0.3f ? _away + _perch.Out * 1.4f : _away;
            _onRoof = false;
            float yaw = Turn.YawOf(off) + Sim.Random.Range(-0.5f, 0.5f);
            Vector2 way = Turn.Heading(yaw);
            Yaw = yaw;
            _speed = _kind.Cruise * 0.45f * _pace;
            _velocity = new Vector3(way.X * _speed, _kind.Cruise * 0.52f, way.Y * _speed);
            _beat = 1f;
            _slotted = false;
        }

        // A roof's edge is come at from outside it, over the drop, as from over the roof it would come down through it: first to a point out from the edge, then in.
        private Vector2 RoofApproach(Vector2 at) => OutsideEdge(at, LandBegins) ? _slot : _slot + _perch.Out * RoofOut;

        private bool OutsideEdge(Vector2 at, float within)
        {
            Vector2 off = at - _slot;
            float distance = off.Length;
            return distance < within && Vector2.Dot(off, _perch.Out) > 0.6f * distance;
        }

        // The highest it has to clear here and on the way ahead, whatever stands among it (LifeLand.Overhead), which is looked for closely enough that no crown or corner falls between two looks.
        private float GroundAhead(Vector2 at, Vector2 way)
        {
            float ground = Sim.Land.Overhead(at);
            for (float along = LookStep; along <= LookAhead; along += LookStep) ground = MathF.Max(ground, Sim.Land.Overhead(at + way * along));
            return MathF.Max(ground, Sim.Land.WaterLevel(at));
        }

        // The top of whatever stands on the way ahead, and nothing of the ground.
        private float StandingAhead(Vector2 at, Vector2 way)
        {
            float top = float.NegativeInfinity;
            for (float along = LookStep; along <= LookAhead; along += LookStep) top = MathF.Max(top, Sim.Land.Standing(at + way * along));
            return top;
        }

        // Where something too tall to go over stands ahead, the nearest way to what it wants that's clear of it, as a bird swings round a tree rather than climbing over the camera.
        private bool TryWayRound(Vector2 at, float wantedYaw, float limit, out Vector2 round)
        {
            round = default;
            if (StandingAhead(at, Turn.Heading(Yaw)) + ClearOfSolid <= limit) return false;
            for (int i = 1; i <= 4; i++)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float yaw = wantedYaw + side * i * 0.42f;
                    Vector2 way = Turn.Heading(yaw);
                    if (StandingAhead(at, way) + ClearOfSolid > limit) continue;
                    round = way;
                    return true;
                }
            }
            return false;
        }

        // Never into anything standing: if a move would take it closer over something than it keeps, it doesn't go on, and climbs instead. What starts closer than that, getting up beside a
        // fence, is let climb out by itself, as only something low is ever beside where a bird comes down (Flock.Fits).
        private bool KeepOut(Vector3 was)
        {
            float here = Sim.Land.Standing(Position.Xz);
            if (Position.Y >= here + ClearOfSolid) return false;
            float there = Sim.Land.Standing(was.Xz);
            // Within its berth of something it may go where nothing is taller, but never in under the top of anything taller than where it was, as a tower beside a low roof shares its berth.
            float top = Sim.Land.Sky.At(Position.Xz);
            bool under = Position.Y < top && top > Sim.Land.Sky.At(was.Xz) + 0.3f;
            if (!under && was.Y < there + ClearOfSolid - 0.05f && here <= there + 0.05f) return false;
            Position.X = was.X;
            Position.Z = was.Z;
            Position.Y = MathF.Max(Position.Y, was.Y);
            return true;
        }

        private void Fly(float deltaTime)
        {
            Vector2 at = Position.Xz;
            Vector2 way = Turn.Heading(Yaw);

            Vector2 toGoal = (_roofSlot && _slotted && _flock.Stage == Flock.Plan.Settling ? RoofApproach(at) : _flock.Goal) - at;
            float distance = toGoal.Length;
            Vector2 seek = distance > 1e-3f ? toGoal / distance : way;

            // The nearest few, and no one else.
            Span<float> gaps = stackalloc float[Neighbours];
            Span<int> nearest = stackalloc int[Neighbours];
            int heeded = 0;
            List<Critter> others = _flock.Members;
            for (int i = 0; i < others.Count; i++)
            {
                if (others[i] == this || !((Bird)others[i]).Aloft) continue;
                float gap = (others[i].Position.Xz - at).LengthSquared;
                if (gap < 1e-4f) continue;

                int place = heeded;
                while (place > 0 && gaps[place - 1] > gap) place--;
                if (place >= Neighbours) continue;
                for (int j = Math.Min(heeded, Neighbours - 1); j > place; j--)
                {
                    gaps[j] = gaps[j - 1];
                    nearest[j] = nearest[j - 1];
                }
                gaps[place] = gap;
                nearest[place] = i;
                heeded = Math.Min(heeded + 1, Neighbours);
            }

            Vector2 align = Vector2.Zero, gather = Vector2.Zero, avoid = Vector2.Zero;
            for (int i = 0; i < heeded; i++)
            {
                Critter other = others[nearest[i]];
                Vector2 offset = other.Position.Xz - at;
                float length = MathF.Sqrt(gaps[i]);
                align += Turn.Heading(other.Yaw);
                gather += offset / length;
                if (length < Separation * Size) avoid -= offset / length * (Separation * Size / length - 1f);
            }

            Vector2 wanted = seek * 1.3f + way * 0.9f;
            if (heeded > 0) wanted += align / heeded * 0.65f + gather / heeded * 0.4f + avoid * 1.3f;

            // Whatever's still frightening it comes first.
            float fright = Sim.Fright(at, _kind.Shy * 0.8f, out Vector2 away);
            if (fright < 1f) wanted += away * (1f - fright) * 2f;

            float climb;
            float ground = GroundAhead(at, way);
            float cruiseAt = ground + _kind.Height * _height;
            float ceiling = Sim.View.Focus.Y + Ceiling;

            // Out of the camera's way, which a crown as tall as the camera is up can bring it near.
            Vector3 fromCamera = Position - Sim.View.Camera;
            float nearCamera = fromCamera.Length;
            if (nearCamera < ClearOfCamera * 1.6f && fromCamera.Xz.LengthSquared > 1e-4f) wanted += fromCamera.Xz.Normalized() * (ClearOfCamera * 1.6f - nearCamera) / ClearOfCamera * 4f;

            // What's too tall to go over without climbing at the camera, it goes round, if there's a way.
            float limit = MathF.Max(ceiling, MathF.Min(Position.Y + GoRound, Sim.View.Camera.Y - ClearOfCamera));
            if (TryWayRound(at, Turn.YawOf(wanted), limit, out Vector2 round)) wanted = round * MathF.Max(wanted.Length, 1f) * 2f;
            if (_flock.GoalHeight >= 0f && distance < 30f) cruiseAt = MathF.Max(_flock.GoalHeight + 1f, ground + ClearOfSolid + 0.5f);
            float targetHeight = MathF.Min(cruiseAt, MathF.Max(ceiling, ground + 3f));
            climb = Math.Clamp((targetHeight - Position.Y) * 1.3f, -3.5f, _kind.Cruise * MostClimb);

            // A bird in a flat dive for the ground is one heading for the ground, so getting out of the way of it comes first.
            float low = Position.Y - Sim.Land.RoughGround(at);
            if (low < 1.8f && climb < 1f) climb = 1.6f;

            float heading = Turn.YawOf(wanted);
            float off = Spring.Shortest(heading - Yaw);
            float turn = Math.Clamp(off * 3f, -_kind.Turning, _kind.Turning);
            Yaw += turn * deltaTime;

            float cruise = _kind.Cruise * _pace;
            float urgency = _state == State.Leaping ? 1.15f : 1f;
            // Something to get over close ahead slows it, as a bird climbing hard does, so that it gets over in time.
            float toClimb = ground + ClearOfSolid - Position.Y;
            if (toClimb > 0f) urgency *= Math.Clamp(1f - toClimb / 14f, 0.35f, 1f);
            _speed = Spring.Damp(_speed, cruise * urgency, 0.32f, deltaTime);

            way = Turn.Heading(Yaw);
            _velocity = new Vector3(way.X * _speed, Spring.Damp(_velocity.Y, climb, 0.15f, deltaTime), way.Y * _speed);

            float leftTheGround = Position.Y - ground;
            if (_state == State.Leaping && leftTheGround > 2.4f) _state = State.Flying;

            Wings(climb, turn, deltaTime);

            // Bounding dips between bursts, as there's nothing holding it up with its wings shut.
            float dip = _kind.Bounds ? _fold * 2.8f : 0f;
            Vector3 was = Position;
            Position += (_velocity - Vector3.UnitY * dip) * deltaTime;
            if (KeepOut(was))
            {
                _speed *= MathF.Exp(-6f * deltaTime);
                _velocity.Y = MathF.Max(_velocity.Y, _kind.Cruise * MostClimb * 0.6f);
            }

            // However steep the land, it goes over it and never through it. A bird can't outclimb a cliff, so it slows, as a real one would. It's the ground as it's drawn that
            // it keeps over, as on a sheer face that and a straight line between the height map's points can be a metre apart.
            float under = MathF.Max(Sim.Land.RoughGround(Position.Xz), Sim.Land.Ground(Position.Xz)) + ClearOfGround;
            if (Position.Y < under)
            {
                Position.Y = under;
                _speed *= MathF.Exp(-3f * deltaTime);
            }

            _legs = Spring.Damp(_legs, 0f, 0.09f, deltaTime);
            _reach = Spring.Damp(_reach, 0f, 0.1f, deltaTime);
            _bank = Spring.Damp(_bank, -turn * 0.42f, 0.09f, deltaTime);
            _pitch = Spring.Damp(_pitch, -MathF.Atan2(_velocity.Y, MathF.Max(_speed, 1f)) * 0.75f, 0.08f, deltaTime);
            _tail = Spring.Damp(_tail, 0f, 0.1f, deltaTime);
            _crouch = Spring.Damp(_crouch, 0f, 0.05f, deltaTime);

            if (_flock.Stage == Flock.Plan.Settling && _state == State.Flying)
            {
                if (!_slotted)
                {
                    _roofSlot = _flock.TryNextPerch(out SkyFloor.Perch perch);
                    if (_roofSlot)
                    {
                        _perch = perch;
                        _hadPerch = true;
                        _slot = perch.At.Xz;
                        _slotFooting = 0f;
                    }
                    else if (_flock.OnRoofs)
                    {
                        // Every edge along that roof is taken, by those who balked at theirs: they all go somewhere else rather than drop to the ground under it.
                        _flock.Resettle(this);
                        return;
                    }
                    else
                    {
                        _slot = _flock.Landing(at);
                        for (int attempt = 0; attempt < 6 && !_flock.Fits(_slot); attempt++) _slot = _flock.Landing(at);
                        if (!_flock.Fits(_slot)) _slot = _flock.Goal;
                        _slotFooting = FootingAt(_slot);
                    }
                    _slotted = true;
                    _slottedAt = Sim.Now;
                }
                // A roof's edge it can't get in to in good time isn't worth it: the flock goes elsewhere.
                if (_roofSlot && Sim.Now - _slottedAt > RoofPatience)
                {
                    _roofSlot = false;
                    _slotted = false;
                    _flock.Resettle(this);
                }
                if (_roofSlot ? OutsideEdge(at, LandBegins * 0.75f) && Position.Y > _perch.At.Y - 0.5f : (_slot - at).Length < LandBegins) _state = State.Landing;
            }

            // In among the leaves, and gone.
            if (_flock.Roosted && distance < 3.5f)
            {
                Fade = Spring.Damp(Fade, 0f, 0.04f, deltaTime);
                if (Fade < 0.03f)
                {
                    _flock.Roost();
                    Gone = true;
                }
            }
        }

        // A wingbeat is whatever's holding it up. Climbing, or slowly, it's working hard; level, each species has its own idea.
        private void Wings(float climb, float turn, float deltaTime)
        {
            float effort = Math.Clamp(climb / (_kind.Cruise * 0.3f), 0f, 1f);
            float beat;
            float fold = 0f;
            float rate = _kind.Wingbeats;

            if (_state == State.Leaping)
            {
                beat = 1f;
                rate *= BurstFlap;
            }
            else if (_kind.Bounds && effort < 0.5f)
            {
                _flapClock += deltaTime;
                float burst = 0.5f, shut = 0.3f;
                if (_flapClock > burst + shut) _flapClock = 0f;
                bool beating = _flapClock < burst;
                beat = beating ? 0.85f : 0f;
                fold = beating ? 0f : 0.88f;
            }
            else if (_kind.Glides > 0f)
            {
                _flapClock -= deltaTime;
                if (_flapClock <= 0f)
                {
                    _glide = !_glide && effort < 0.35f && Sim.Random.Float() < _kind.Glides + 0.25f;
                    _flapClock = _glide ? Sim.Random.Range(1f, 3.4f) * (0.5f + _kind.Glides) : Sim.Random.Range(0.5f, 1.2f);
                }
                beat = _glide ? 0f : 0.55f + effort * 0.45f;
            }
            else
            {
                beat = 0.62f + effort * 0.38f;
            }

            _beat = Spring.Damp(_beat, beat, 0.06f, deltaTime);
            _fold = Spring.Damp(_fold, fold, FoldTime * 0.5f, deltaTime);
            _stroke += MathF.Tau * rate * deltaTime;
            if (_stroke > MathF.Tau) _stroke -= MathF.Tau;
        }

        private void Land(float deltaTime)
        {
            Vector2 at = Position.Xz;
            Vector2 toSlot = _slot - at;
            float distance = toSlot.Length;
            float ground = _roofSlot ? _perch.At.Y : Sim.Land.Ground(at) + _slotFooting;
            float high = Position.Y - ground;

            Vector2 way = distance > 1e-3f ? toSlot / distance : Turn.Heading(Yaw);
            float off = Spring.Shortest(Turn.YawOf(way) - Yaw);
            Yaw += Math.Clamp(off * 4f, -_kind.Turning * 1.4f, _kind.Turning * 1.4f) * deltaTime;

            float share = Math.Clamp(distance / LandBegins, 0f, 1f);
            _speed = Spring.Damp(_speed, MathF.Max(_kind.Cruise * 0.26f, 1.5f) + _kind.Cruise * 0.64f * share, 0.14f, deltaTime);
            float slope = distance > 0.5f ? high / distance : 3f;
            float sink = MathF.Min(MathF.Max(slope, 0.25f) * _speed, 6f);

            Vector2 heading = Turn.Heading(Yaw);
            Vector3 was = Position;
            Position += new Vector3(heading.X * MathF.Min(_speed, distance / MathF.Max(deltaTime, 1e-3f)), -sink, heading.Y * MathF.Min(_speed, distance / MathF.Max(deltaTime, 1e-3f))) * deltaTime;
            // Its slot is clear of anything standing, but the way down to it may not be: where it would come down into something taller than a fence it doesn't, and flies on to look again.
            // Coming to a roof's edge it's under the roof's own berth all the way in, so only what's really there counts, and nothing at all at the very edge.
            bool blocked;
            if (_roofSlot)
            {
                Position.Y = MathF.Max(Position.Y, _perch.At.Y);
                blocked = (Position.Xz - _slot).Length > 1.5f && Position.Y < Sim.Land.Sky.At(Position.Xz) + 0.2f;
            }
            else
            {
                float standing = Sim.Land.Standing(Position.Xz);
                blocked = Position.Y < standing + ClearOfSolid && standing - Sim.Land.RoughGround(Position.Xz) > Flock.ClearOver;
            }
            if (blocked)
            {
                Position = was;
                _state = State.Flying;
                _slotted = false;
                if (++_balked >= 2)
                {
                    _balked = 0;
                    _flock.Resettle(this);
                }
                return;
            }

            // The flare: wings forward and hard against the air, tail down, feet out.
            bool flaring = distance < FlareBegins * _kind.Look.Body * 3f || high < 2.2f;
            float flare = flaring ? 1f : 0f;
            _reach = Spring.Damp(_reach, flare * 0.62f, 0.07f, deltaTime);
            _legs = Spring.Damp(_legs, flare, 0.08f, deltaTime);
            _tail = Spring.Damp(_tail, flare * 0.6f, 0.07f, deltaTime);
            _pitch = Spring.Damp(_pitch, -flare * 0.92f, 0.07f, deltaTime);
            _bank = Spring.Damp(_bank, 0f, 0.06f, deltaTime);
            _fold = Spring.Damp(_fold, 0f, 0.05f, deltaTime);
            _beat = Spring.Damp(_beat, flaring ? 0.82f : 0.22f, 0.05f, deltaTime);
            _stroke += MathF.Tau * _kind.Wingbeats * deltaTime;
            if (_stroke > MathF.Tau) _stroke -= MathF.Tau;

            bool down = _roofSlot ? (Position.Xz - _slot).Length < 0.25f && Position.Y <= ground + 0.4f : Position.Y <= ground + 0.03f || distance < 0.2f && high < 0.4f;
            if (down)
            {
                Position = _roofSlot ? _perch.At : new Vector3(at.X, ground, at.Y);
                _onRoof = _roofSlot;
                _roofSlot = false;
                _footing = _slotFooting;
                _state = State.Feeding;
                _nextMove = Sim.Now + Sim.Random.Range(0.4f, 1.4f);
                _velocity = Vector3.Zero;
                _speed = 0f;
                _goesAt = -1f;
                _flock.Landed();
            }
        }

        public override void Pose(Span<Matrix4> skin)
        {
            bool aloft = _state is State.Leaping or State.Flying or State.Landing;
            float settle = aloft ? 0f : 1f;

            // Folding is by how long it's been down, and a bird on the ground stands tilted up.
            if (!aloft) _fold = 1f;

            var pose = new BirdPose
            {
                Stroke = _stroke,
                Beat = aloft ? _beat : 0f,
                Fold = _fold,
                Reach = _reach,
                Hold = _kind.Hold,
                Pitch = aloft ? _pitch : -0.32f,
                Bank = aloft ? _bank : 0f,
                Crouch = _crouch,
                Peck = _peck,
                HeadYaw = _headYaw * settle,
                TailPitch = _tail,
                Legs = aloft ? _legs : 1f,
            };

            // On the ground its feet are where it is. In the air where it is is the middle of it.
            float lift = aloft ? BirdBody.Height(_kind.Look) * Size : _crouch * 0.03f * Size;
            pose.Solve(_kind.Bones, Turn.Root(Position - Vector3.UnitY * lift, Turn.Yaw(Yaw), Size), skin);
        }
    }
}

using Dino_Engine.ECS.Components;
using Dino_Engine.Physics;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Wildlife
{
    /// <summary>Something that frightens what's near it. Every body that goes through plants is one, which is how Ghost of Tsushima's fauna know about people too: a faint sphere round every character.</summary>
    public struct Scare
    {
        public Vector2 At;
        public float Moving;    // 0 standing still, 1 at a run.
        public float Radius;    // Metres, for something that frightens everything within reach whatever it's like: a blast. 0 goes by how shy each creature is.
    }

    /// <summary>What a creature wants heard. It's only asked for here, as wildlife knows nothing of sound.</summary>
    public enum LifeNoiseKind
    {
        Wings,
        Caw,
        Cry,
        Plop,
        Splash,
    }

    public readonly record struct LifeNoise(LifeNoiseKind Kind, Vector3 At, float Size);

    /// <summary>A ring to send across water.</summary>
    public readonly record struct LifeSplash(Vector3 At, float Radius, float Strength);

    /// <summary>Something going through grass.</summary>
    public readonly record struct LifeContact(Vector2 At, Vector2 Was, float Radius, float Lean, float Press, float Drag);

    /// <summary>Where draws go. The simulation has no GPU, so a lab can catch them as easily as a renderer can.</summary>
    public interface IWildlifeCanvas
    {
        /// <param name="skin">Every bone of every one, one after the other, each all the way into the world.</param>
        /// <param name="tints">Colour and how solid, one for each.</param>
        void Draw(WildlifeShape shape, ReadOnlySpan<Matrix4> skin, ReadOnlySpan<Vector4> tints);
    }

    /// <summary>What the world is doing this frame, as far as anything living cares.</summary>
    public sealed class WildlifeView
    {
        public Vector3 Camera;
        public Vector3 Focus;
        public Vector2 Least;       // The corners of what can be seen of the ground.
        public Vector2 Most;
        public Frustum? Sight;
        public WindSingleton Wind = new WindSingleton();
        public WeatherSample Weather = new WeatherSample { Gale = 1f };
        public float Night;         // 0 day, 1 full dark.
        public int MostCreatures = 200;
        public int MostLeaves = 1400;
        public readonly List<Scare> Scares = new();
    }

    /// <summary>One group's claim on a place. It's worked out from where it is, so it's always the same one, and it goes on being there when nobody's looking.</summary>
    internal sealed class Resident
    {
        public Creature Kind = null!;
        public Vector2 At;
        public Habitat Place;
        public int Count;
        public uint Seed;
        public Party? Party;
        public float BackAt;
    }

    /// <summary>
    /// Everything alive in a world that the rules of the game know nothing about. It runs on a client alone and each machine has its own, as none of it can touch anything: nothing here is sent, nothing is saved, and it
    /// takes no clicks and stops nobody. It's a plain simulation and not entities, for the reason grass isn't: there's a lot of it, it comes and goes all the time, and it's only ever looked at.
    /// <para>
    /// Who lives where is a pure function of the world's seed and the place, as plants are, so a meadow's rabbits are always in that meadow. They're only made while they could be seen, and let go after, which is what lets
    /// a whole world be alive for the price of what's on screen. All that's kept is who's been frightened off, and for how long.
    /// </para>
    /// </summary>
    public sealed class WildlifeSim
    {
        private const float Cell = 28f;             // Metres. A place has one group at most.
        private const float Margin = 18f;           // Metres outside the view that things settle, so that they're there before they're seen.
        private const float LetGoMargin = 46f;      // Metres outside the view that everything's let go.
        private const float SurveyInterval = 0.4f;
        private const float StartleTime = 1.2f;
        private const float LongestToWalkIn = 0.5f; // Metres the grass comes up, past which what walks goes elsewhere.
        private const int MostForgotten = 6000;

        private readonly Dictionary<long, Resident?> _places = new();
        private readonly List<Party> _parties = new();
        private readonly List<(Vector2 at, float radius, float until)> _startles = new();
        private readonly Dictionary<WildlifeShape, Batch> _batches = new();
        private readonly List<Batch> _drawn = new();
        private readonly uint _seed;
        private float _nextSurvey;
        private float _nextOverhead;
        private int _creatures;
        private bool _settled;
        private Vector3 _lastFocus;

        public readonly LifeLand Land;
        public readonly WorldRandom Random;
        public readonly LeafFall Leaves;
        public readonly Air Air = new Air();

        /// <summary>The ground as it's drawn, for whatever wants it as a function.</summary>
        public readonly Func<Vector2, float> Ground;

        public readonly List<LifeNoise> Noises = new();
        public readonly List<LifeSplash> Splashes = new();
        public readonly List<LifeContact> Contacts = new();

        /// <summary>Seconds it has been running, not counting when it was stopped.</summary>
        public float Now { get; private set; }

        public WildlifeView View { get; private set; } = new WildlifeView();

        public int Creatures => _creatures;
        public int Parties => _parties.Count;

        /// <summary>Each living thing, for a lab to look at.</summary>
        public IEnumerable<Critter> Living => _parties.SelectMany(party => party.Members);

        internal IReadOnlyList<Party> AllParties => _parties;

        public WildlifeSim(WorldData world)
        {
            Land = new LifeLand(world);
            Random = new WorldRandom(world.Plan.Seed).Fork("wildlife");
            _seed = (uint)world.Plan.Seed * 2246822519u + 0x165667B1u;
            Ground = Land.Ground;
            Leaves = new LeafFall(this);
            _nextOverhead = 6f;
        }

        // ---- What frightens them.

        /// <summary>Says how near whatever's most frightening is, as a share of how close this creature will let anything come: under 1 and it's too close. <paramref name="away"/> is the way to go.</summary>
        public float Fright(Vector2 at, float shy, out Vector2 away)
        {
            float worst = float.PositiveInfinity;
            away = Vector2.UnitY;
            List<Scare> scares = View.Scares;
            for (int i = 0; i < scares.Count; i++)
            {
                Scare scare = scares[i];
                Vector2 offset = at - scare.At;
                float distance = offset.Length;
                float reach = scare.Radius > 0f ? scare.Radius : shy * (0.5f + 0.5f * scare.Moving);
                float ratio = distance / MathF.Max(reach, 0.1f);
                if (ratio >= worst) continue;
                worst = ratio;
                if (distance > 1e-3f) away = offset / distance;
            }
            return worst;
        }

        /// <summary>Frightens everything within <paramref name="radius"/>: a loud noise, or a blow.</summary>
        public void Startle(Vector2 at, float radius) => _startles.Add((at, radius, Now + StartleTime));

        internal void MakeNoise(LifeNoiseKind kind, Vector3 at, float size = 1f) => Noises.Add(new LifeNoise(kind, at, size));

        internal void Touch(Vector2 at, Vector2 was, float radius, float lean, float press, float drag) => Contacts.Add(new LifeContact(at, was, radius, lean, press, drag));

        internal void Splash(Vector3 at, float radius, float strength) => Splashes.Add(new LifeSplash(at, radius, strength));

        /// <summary>Whether a point can be seen, or nearly: a shadow can fall into view from a little way out of it.</summary>
        public bool InSight(Vector3 at, float radius)
        {
            return View.Sight == null || View.Sight.IntersectsSphere(at, radius);
        }

        public bool InView(Vector2 at, float margin)
        {
            return at.X > View.Least.X - margin && at.Y > View.Least.Y - margin && at.X < View.Most.X + margin && at.Y < View.Most.Y + margin;
        }

        // ---- Each frame.

        public void Step(WildlifeView view, float deltaTime)
        {
            View = view;
            float dt = MathF.Min(deltaTime, 0.1f);
            Now += dt;

            // A jump to somewhere else is a different place: nothing here is seen again.
            if (_settled && (view.Focus - _lastFocus).LengthSquared > 90f * 90f) Forget();
            _lastFocus = view.Focus;

            for (int i = _startles.Count - 1; i >= 0; i--)
            {
                if (Now >= _startles[i].until)
                {
                    _startles.RemoveAt(i);
                    continue;
                }
                view.Scares.Add(new Scare { At = _startles[i].at, Moving = 1f, Radius = _startles[i].radius });
            }

            Air.Refresh(view, dt);

            if (Now >= _nextSurvey || !_settled)
            {
                _nextSurvey = Now + SurveyInterval;
                Leaves.ReadTrees();
                Survey();
                _settled = true;
            }
            Overhead();

            int creatures = 0;
            for (int i = _parties.Count - 1; i >= 0; i--)
            {
                Party party = _parties[i];
                party.Step(dt);
                for (int m = party.Members.Count - 1; m >= 0; m--)
                {
                    Critter member = party.Members[m];
                    member.Step(dt);
                    if (member.Gone) party.Members.RemoveAt(m);
                }
                creatures += party.Members.Count;

                if (party.Finished || !party.StillNear(LetGoMargin))
                {
                    LetGo(party);
                    _parties.RemoveAt(i);
                }
            }
            _creatures = creatures;
            Leaves.Step(dt);
        }

        private void LetGo(Party party)
        {
            foreach (Critter member in party.Members) member.Gone = true;
            if (party.Home == null) return;
            party.Home.Party = null;
            if (party.Frightened) party.Home.BackAt = Now + party.Home.Kind.BackAfter;
        }

        /// <summary>Puts some of a creature down somewhere, for looking at. They're nobody's: nothing about the place changes.</summary>
        public void Bring(Creature kind, Vector2 at)
        {
            var resident = new Resident { Kind = kind, At = at, Place = Land.HabitatAt(at), Seed = (uint)Random.NextULong(), Count = kind.Group.Max };
            resident.Party = kind.Settle(this, resident);
            _parties.Add(resident.Party);
        }

        /// <summary>Lets everything go at once, and starts again from the view.</summary>
        public void Forget()
        {
            foreach (Party party in _parties)
            {
                if (party.Home != null) party.Home.Party = null;
            }
            _parties.Clear();
            Leaves.Clear();
            _settled = false;
        }

        // Goes over every place that could come into view, and settles whoever lives there.
        private void Survey()
        {
            if (_places.Count > MostForgotten) ForgetFarPlaces();

            int firstX = (int)MathF.Floor((View.Least.X - Margin) / Cell);
            int lastX = (int)MathF.Floor((View.Most.X + Margin) / Cell);
            int firstZ = (int)MathF.Floor((View.Least.Y - Margin) / Cell);
            int lastZ = (int)MathF.Floor((View.Most.Y + Margin) / Cell);

            for (int z = firstZ; z <= lastZ; z++)
            {
                for (int x = firstX; x <= lastX; x++)
                {
                    Resident? resident = ResidentOf(x, z);
                    if (resident == null || resident.Party != null || Now < resident.BackAt) continue;
                    if (!AwakeNow(resident) || _creatures + resident.Count > View.MostCreatures) continue;

                    resident.Party = resident.Kind.Settle(this, resident);
                    _parties.Add(resident.Party);
                    _creatures += resident.Count;
                }
            }
        }

        private void ForgetFarPlaces()
        {
            var far = new List<long>();
            foreach (var (key, resident) in _places)
            {
                if (resident?.Party == null && (resident == null || Now >= resident.BackAt)) far.Add(key);
            }
            foreach (long key in far) _places.Remove(key);
        }

        // Whether it's the time for them, by the hour and the weather. Rain thins a place out by which places they are, so the same ones are left.
        private bool AwakeNow(Resident resident)
        {
            Creature kind = resident.Kind;
            if (kind.Nocturnal ? View.Night < 0.4f : View.Night > 0.6f && !kind.AllHours) return false;

            float rain = View.Weather.Rain;
            if (rain <= 0.05f) return true;
            float stays = 1f + (kind.Rain - 1f) * MathF.Min(rain * 1.6f, 1f);
            return (Hash(resident.Seed, 77, 13) & 0xFFFF) / 65536f < stays;
        }

        private uint Hash(uint seed, int x, int z)
        {
            uint hash = seed ^ (uint)x * 0x9E3779B1u ^ (uint)z * 0x85EBCA77u;
            hash ^= hash >> 15;
            hash *= 0x2C1B3C6Du;
            hash ^= hash >> 12;
            hash *= 0x297A2D39u;
            hash ^= hash >> 15;
            return hash;
        }

        private static float Unit(ref uint state)
        {
            state = state * 1664525u + 1013904223u;
            return (state >> 8) / 16777216f;
        }

        // Who lives in a cell, if anyone. Everything comes from the world's seed and the cell, so it's the same answer for as long as the world lasts, on any machine.
        private Resident? ResidentOf(int x, int z)
        {
            long key = ((long)x << 32) ^ (uint)z;
            if (_places.TryGetValue(key, out Resident? known)) return known;

            uint state = Hash(_seed, x, z);
            Resident? found = null;
            Vector2 at = new Vector2(x + 0.14f + 0.72f * Unit(ref state), z + 0.14f + 0.72f * Unit(ref state)) * Cell;
            Habitat place = Land.HabitatAt(at);
            if (place != Habitat.None)
            {
                Creature[] life = Land.World.BiomeAt(at).Life;
                int start = life.Length > 0 ? (int)(state % (uint)life.Length) : 0;
                for (int i = 0; i < life.Length; i++)
                {
                    Creature kind = life[(start + i) % life.Length];
                    if ((kind.Lives & place) == 0) continue;
                    if (Unit(ref state) >= kind.Density * Suits(kind, at)) continue;

                    found = new Resident
                    {
                        Kind = kind,
                        At = at,
                        Place = place,
                        Seed = state,
                        Count = kind.Group.Min + (int)(Unit(ref state) * (kind.Group.Max - kind.Group.Min + 1)),
                    };
                    break;
                }
            }

            _places[key] = found;
            return found;
        }

        // What makes a place better or worse than others of its kind. Whatever goes about on the ground wants to be able to see out, and to be seen: in grass up to its neck there'd be nothing of it to look at.
        private float Suits(Creature kind, Vector2 at)
        {
            switch (kind)
            {
                case ButterflyKind:
                    return 0.35f + MathF.Min(Land.GrassAt(at).Bloom * 6f, 1f);
                case BirdKind { Walks: true }:
                    return Land.GrassTop(at) < LongestToWalkIn ? 1f : 0f;
                case RabbitKind:
                    return Land.GrassTop(at) < LongestToWalkIn * 2f ? 1f : 0.6f;
                case FrogKind:
                    return Land.GrassTop(at) < LongestToWalkIn ? 1f : 0.3f;
                default:
                    return 1f;
            }
        }

        // Now and then something crosses the sky, on its way somewhere else, whether anyone's frightened it or not. It's what makes a sky lived in.
        private void Overhead()
        {
            if (Now < _nextOverhead) return;
            _nextOverhead = Now + Random.Range(16f, 42f);
            if (View.Weather.Rain > 0.35f || View.Night > 0.5f || _creatures > View.MostCreatures - 12) return;

            Vector2 middle = (View.Least + View.Most) * 0.5f;
            Creature[] life = Land.World.BiomeAt(middle).Life;
            int start = Random.Int(0, Math.Max(life.Length - 1, 0));
            for (int i = 0; i < life.Length; i++)
            {
                if (life[(start + i) % life.Length] is not BirdKind bird || bird.Roosts && bird.Bounds) continue;
                _parties.Add(Flock.Passing(this, bird));
                return;
            }
        }

        // ---- What's drawn.

        internal Batch BatchOf(WildlifeShape shape)
        {
            if (_batches.TryGetValue(shape, out Batch? batch)) return batch;
            batch = new Batch(shape);
            _batches.Add(shape, batch);
            return batch;
        }

        /// <summary>Works out every pose that can be seen and hands them over. What can't be seen isn't worked out at all.</summary>
        public void Draw(IWildlifeCanvas canvas)
        {
            _drawn.Clear();
            foreach (Batch batch in _batches.Values) batch.Count = 0;

            for (int i = 0; i < _parties.Count; i++)
            {
                List<Critter> members = _parties[i].Members;
                for (int m = 0; m < members.Count; m++)
                {
                    Critter member = members[m];
                    if (!InSight(member.Position, member.Reach + ShadowReach)) continue;
                    Batch batch = BatchOf(member.Shape);
                    Span<Matrix4> skin = batch.Next(out Span<Vector4> tint);
                    member.Pose(skin);
                    tint[0] = new Vector4(member.Tint, member.Fade);
                }
            }

            Leaves.Draw();
            foreach (Batch batch in _batches.Values)
            {
                if (batch.Count > 0) canvas.Draw(batch.Shape, batch.Skin.AsSpan(0, batch.Count * batch.Shape.Bones), batch.Tints.AsSpan(0, batch.Count));
            }
        }

        private const float ShadowReach = 8f;   // Metres outside the view a shadow can be cast into it from.
    }

    /// <summary>All of one thing that's to be drawn this frame.</summary>
    internal sealed class Batch
    {
        public readonly WildlifeShape Shape;
        public Matrix4[] Skin;
        public Vector4[] Tints;
        public int Count;

        public Batch(WildlifeShape shape)
        {
            Shape = shape;
            Tints = new Vector4[32];
            Skin = new Matrix4[32 * shape.Bones];
        }

        public Span<Matrix4> Next(out Span<Vector4> tint)
        {
            if (Count == Tints.Length)
            {
                Array.Resize(ref Tints, Count * 2);
                Array.Resize(ref Skin, Count * 2 * Shape.Bones);
            }
            tint = Tints.AsSpan(Count, 1);
            Span<Matrix4> skin = Skin.AsSpan(Count * Shape.Bones, Shape.Bones);
            Count++;
            return skin;
        }
    }

    /// <summary>
    /// The wind as what's small feels it, on a coarse grid over what's in view. A full sample of the wind costs as much as hundreds of leaves do, and what plants and leaves read of it is broad and slow by design,
    /// so a few dozen samples a few times a second is the same wind. Between them it's read by interpolation.
    /// </summary>
    public sealed class Air
    {
        private const float Spacing = 10f;
        private const float RefreshTime = 0.1f;
        private const int MostSide = 48;

        private readonly Vector3[] _cells = new Vector3[MostSide * MostSide];   // Wind x, wind z, and the gust.
        private Vector2 _origin;
        private int _width, _depth;
        private float _wait;
        private float _clock;

        /// <summary>How fast the air here is going, by what a speck can be carried at: a gale's speed levels off, as it does for the patterns plants show, or a leaf's a line across the screen.</summary>
        public Vector2 At(Vector2 where, out float gust)
        {
            if (_width < 2 || _depth < 2)
            {
                gust = 0.5f;
                return Vector2.Zero;
            }

            float x = Math.Clamp((where.X - _origin.X) / Spacing, 0f, _width - 1.001f);
            float z = Math.Clamp((where.Y - _origin.Y) / Spacing, 0f, _depth - 1.001f);
            int cx = (int)x, cz = (int)z;
            float tx = x - cx, tz = z - cz;
            int at = cz * _width + cx;
            Vector3 near = Vector3.Lerp(_cells[at], _cells[at + 1], tx);
            Vector3 far = Vector3.Lerp(_cells[at + _width], _cells[at + _width + 1], tx);
            Vector3 cell = Vector3.Lerp(near, far, tz);
            gust = cell.Z;
            return cell.Xy;
        }

        /// <summary>Quick swirls, which are for specks and no good on anything rooted. Each component only depends on the other's axis, so nothing piles up anywhere: it has no divergence.</summary>
        public Vector2 Swirl(Vector3 where)
        {
            float t = _clock;
            float x = MathF.Sin(where.Z * 0.31f + t * 0.7f + where.Y * 0.2f) + 0.5f * MathF.Sin(where.Z * 0.73f - t * 1.1f);
            float z = MathF.Sin(where.X * 0.29f - t * 0.6f + where.Y * 0.17f) + 0.5f * MathF.Sin(where.X * 0.67f + t * 0.9f);
            return new Vector2(x, z);
        }

        public void Refresh(WildlifeView view, float deltaTime)
        {
            _clock += deltaTime;
            _wait -= deltaTime;
            if (_wait > 0f) return;
            _wait = RefreshTime;

            Vector2 least = view.Least - new Vector2(Spacing * 3f);
            Vector2 most = view.Most + new Vector2(Spacing * 3f);
            _width = Math.Min((int)MathF.Ceiling((most.X - least.X) / Spacing) + 1, MostSide);
            _depth = Math.Min((int)MathF.Ceiling((most.Y - least.Y) / Spacing) + 1, MostSide);
            _origin = (least + most) * 0.5f - new Vector2(_width - 1, _depth - 1) * (Spacing * 0.5f);

            for (int z = 0; z < _depth; z++)
            {
                for (int x = 0; x < _width; x++)
                {
                    WindSample sample = WindField.Sample(view.Wind, _origin + new Vector2(x, z) * Spacing);
                    Vector2 wind = sample.Velocity.Xz;
                    float speed = wind.Length;
                    if (speed > 1e-3f) wind *= WindField.PatternSpeed(speed) / speed;
                    _cells[z * _width + x] = new Vector3(wind.X, wind.Y, sample.Gust);
                }
            }
        }
    }

    /// <summary>A group that lives together, and goes together.</summary>
    public abstract class Party
    {
        protected readonly WildlifeSim Sim;
        internal readonly Resident? Home;

        public readonly List<Critter> Members = new();

        /// <summary>Something's sent them away, so it'll be a while before there are any here again.</summary>
        public bool Frightened;

        /// <summary>It's over, and it can go.</summary>
        public virtual bool Finished => Members.Count == 0;

        internal Party(WildlifeSim sim, Resident? home)
        {
            Sim = sim;
            Home = home;
        }

        public virtual void Step(float deltaTime) { }

        /// <summary>Thins out and goes, for what can't be out in the rain, however long it's been here. It'll be back when it's dry.</summary>
        protected void Shelter(float deltaTime)
        {
            if (Sim.View.Weather.Rain < ShelterFrom) return;
            for (int i = 0; i < Members.Count; i++)
            {
                Critter member = Members[i];
                member.Fade -= deltaTime * ShelterRate;
                if (member.Fade <= 0f) member.Gone = true;
            }
        }

        private const float ShelterFrom = 0.25f;
        private const float ShelterRate = 0.7f;

        /// <summary>Whether anyone in it could still come into view.</summary>
        public virtual bool StillNear(float margin)
        {
            for (int i = 0; i < Members.Count; i++)
            {
                if (Sim.InView(Members[i].Position.Xz, margin)) return true;
            }
            return Members.Count == 0 && Home != null && Sim.InView(Home.At, margin);
        }
    }

    /// <summary>One living thing. <see cref="Position"/> is where its feet are, or the middle of it if it's in the air.</summary>
    public abstract class Critter
    {
        protected readonly WildlifeSim Sim;

        public Vector3 Position;
        public float Yaw;
        public float Size = 1f;
        public Vector3 Tint = Vector3.One;
        public float Fade = 1f;
        public bool Gone;

        public abstract WildlifeShape Shape { get; }

        /// <summary>Metres from <see cref="Position"/> that everything of it stays inside.</summary>
        public abstract float Reach { get; }

        /// <summary>What it's up to, for a lab.</summary>
        public virtual string Doing => "";

        protected Critter(WildlifeSim sim, Creature kind)
        {
            Sim = sim;
            Size = kind.Size.Min + (kind.Size.Max - kind.Size.Min) * sim.Random.Float();
            float shift = (sim.Random.Float() - 0.5f) * 2f * kind.Variation;
            Tint = new Vector3(1f + shift, 1f + shift * 0.9f, 1f + shift * 0.8f);
        }

        public abstract void Step(float deltaTime);
        public abstract void Pose(Span<Matrix4> skin);
    }
}

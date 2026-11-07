using Dino_Engine.Animation;
using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.Modelling.Procedural.Wildlife;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Wildlife
{
    /// <summary>A tree, as far as anything that lives in it or falls from it cares.</summary>
    public struct StandingTree
    {
        public Vector2 At;
        public float CrownLow;      // Heights in the world.
        public float CrownHigh;
        public float Radius;
        public int Kind;            // Its leaf, or -1 for what keeps its leaves to itself.
        public float Rate;          // Leaves a second, with a little wind and no gusts.
    }

    /// <summary>
    /// What comes down from trees. Each leaf comes from a real crown and has that tree's outline and colours, falls as <see cref="FallingCard"/> says a card falls, and rides the same wind the grass bends in, so a gust
    /// going through a wood can be followed by what it shakes loose. Then it lies where it landed for a while, or floats, as Tsushima's do. It's all in one array of structs and drawn in a call or two, so a wood can have
    /// hundreds of them for less than a character costs.
    /// </summary>
    public sealed class LeafFall
    {
        private enum State : byte { Falling, Settling, Lying, Floating }

        /// <summary>How one tree's leaves look and come down.</summary>
        private sealed class Kind
        {
            public WildlifeShape Shape = null!;
            public Palette Colours = null!;
            public FloatRange Length;       // Metres.
            public float Wither;            // How far from the crown's own colour a falling leaf has gone.
            public bool Petal;
        }

        private struct Leaf
        {
            public Vector3 Origin;      // Where the card would be if it hadn't swung, carried along by the air.
            public Vector3 At;
            public Vector3 Colour;
            public Vector3 Normal;
            public Vector2 Carried;
            public float Clock, Rate, Swing;
            public float Azimuth, Precession, Spin, Length;
            public float Age, Lasts;
            public short Kind;
            public byte Card;
            public State State;
            public bool Guided;
        }

        private const float ReadReach = 22f;            // Metres outside the view trees are looked for.
        private const float MoveBeforeReading = 4f;
        private const float MostBoost = 6f;             // What the strongest gust does to how fast a tree lets go.
        private const float QuietShare = 0.11f;         // How much a tree sheds where the land isn't known for it.
        private const float PerTree = 1.3f;             // Leaves a second off a big tree in a wood where they're falling.
        private const float CarryHalfLife = 0.3f;       // Seconds a leaf takes to catch up with the air.
        private const float LieFrom = 16f, LieTo = 34f; // Seconds on the ground.
        private const float FadeTime = 2.2f;
        private const float SettleTime = 0.16f;
        private const float DropMargin = 26f;
        private const float StirReach = 2.6f;           // Metres from a body at a run that a lying leaf is kicked up.
        private const float GustLifts = 0.86f;          // How strong a gust stirs what's lying.
        private const float GuideSpeed = 12f;
        private const float LeastLeaf = 0.3f;           // Metres. Anything smaller is a speck, whatever it is on the tree.

        private static readonly Vector3 Dry = new Vector3(0.3f, 0.17f, 0.04f);
        private static readonly Vector3 GuideColour = new Vector3(0.5f, 0.3f, 0.03f);
        private static readonly FallingCard[] Cards = { FallingCard.Flutter, FallingCard.Tumble, FallingCard.Mixed, FallingCard.Sink };
        private const byte Sinking = 3;

        private static readonly Dictionary<LeafForm, WildlifeShape> Shapes = new();

        private readonly WildlifeSim _sim;
        private readonly TreeSpec?[] _assets;
        private readonly List<Kind> _kinds = new();
        private readonly List<StandingTree> _trees = new();
        private readonly int _guideKind;
        private Leaf[] _leaves = new Leaf[1024];
        private int _count;
        private float _debt;
        private float _treeRate;
        private Vector2 _readAt = new Vector2(float.MaxValue);
        private int _stirTurn;
        private Vector2 _guide;
        private float _guideRate;
        private float _guideDebt;
        private float _splashWait;

        private sealed record TreeSpec(float Height, float Low, float Radius, int Kind);

        public int Count => _count;
        public int Trees => _trees.Count;

        /// <summary>How many are in the air now, for a lab.</summary>
        public int Airborne
        {
            get
            {
                int airborne = 0;
                for (int i = 0; i < _count; i++)
                {
                    if (_leaves[i].State == State.Falling) airborne++;
                }
                return airborne;
            }
        }

        /// <summary>Where each one that's in the air is, for reading a run by.</summary>
        public IEnumerable<Vector3> InTheAir
        {
            get
            {
                for (int i = 0; i < _count; i++)
                {
                    if (_leaves[i].State == State.Falling) yield return _leaves[i].At;
                }
            }
        }

        public LeafFall(WildlifeSim sim)
        {
            _sim = sim;
            WorldObjects objects = sim.Land.World.Objects;
            _assets = new TreeSpec?[objects.AssetIds.Count];
            var known = new Dictionary<object, int>();
            for (int i = 0; i < _assets.Length; i++)
            {
                string name = NatureCatalog.SpeciesOf(objects.AssetIds[i], out _);
                if (!FloraCatalog.TryGet(name, out PlantSpecies species) || species.Kind != WorldAssetKind.Tree || species.Form is not TreeForm form) continue;
                float height = (form.Height.Min + form.Height.Max) * 0.5f;
                _assets[i] = new TreeSpec(height, (form.CrownBase.Min + form.CrownBase.Max) * 0.5f, (form.CrownRadius.Min + form.CrownRadius.Max) * 0.5f, KindOf(form.Foliage, known));
            }

            // The guiding wind carries leaves whether the land has any or not: a wood of pines still points the way.
            _guideKind = _kinds.Count;
            _kinds.Add(new Kind { Shape = ShapeOf(Broadleaves.BeechSpray.Leaf), Colours = new Palette { Shade = GuideColour, Body = GuideColour, Sun = GuideColour }, Length = new FloatRange(0.6f, 0.85f) });
        }

        private static WildlifeShape ShapeOf(LeafForm form)
        {
            lock (Shapes)
            {
                if (Shapes.TryGetValue(form, out WildlifeShape? shape)) return shape;
                shape = new WildlifeShape("leaf." + Shapes.Count, LeafBody.Count, () => LeafBody.Build(form, 1f));
                Shapes.Add(form, shape);
                return shape;
            }
        }

        // Whatever a tree's cards were painted from is what falls from it. What has needles, or fronds, doesn't drop anything worth following.
        private int KindOf(FoliageForm? foliage, Dictionary<object, int> known)
        {
            if (foliage == null) return -1;

            LeafForm? form;
            FloatRange length;
            bool petal = false;
            switch (foliage.Card)
            {
                case LeafSpray spray:
                    // The size it is on the tree: how much of a card a leaf is, and how big the cards are.
                    form = spray.Leaf;
                    float grown = spray.LeafLength * (foliage.CardSize.Min + foliage.CardSize.Max) * 0.5f;
                    length = new FloatRange(MathF.Max(grown * 0.85f, LeastLeaf), MathF.Max(grown * 1.2f, LeastLeaf * 1.3f));
                    break;
                case BlossomSpray blossom:
                    form = blossom.Petal;
                    length = new FloatRange(0.17f, 0.24f);
                    petal = true;
                    break;
                default:
                    return -1;
            }

            if (known.TryGetValue((form, foliage.Colours), out int kind)) return kind;

            // Anything still green when it comes off has started to go.
            Vector3 body = foliage.Colours.Body;
            bool green = body.Y > body.X * 1.05f;
            _kinds.Add(new Kind { Shape = ShapeOf(form), Colours = foliage.Colours, Length = length, Wither = green ? 0.72f : 0.12f, Petal = petal });
            kind = _kinds.Count - 1;
            known.Add((form, foliage.Colours), kind);
            return kind;
        }

        public void Clear()
        {
            _count = 0;
            _trees.Clear();
            _readAt = new Vector2(float.MaxValue);
        }

        // ---- Trees.

        /// <summary>Looks up what's standing in and around the view. It's only done when the view has moved, as a forest stays where it is.</summary>
        public void ReadTrees()
        {
            WildlifeView view = _sim.View;
            Vector2 middle = (view.Least + view.Most) * 0.5f;
            if ((middle - _readAt).LengthSquared < MoveBeforeReading * MoveBeforeReading) return;
            _readAt = middle;

            _trees.Clear();
            _treeRate = 0f;
            WorldData world = _sim.Land.World;
            WorldObjects objects = world.Objects;
            Vector2i from = objects.SectorOf(view.Least - new Vector2(ReadReach));
            Vector2i to = objects.SectorOf(view.Most + new Vector2(ReadReach));
            for (int z = from.Y; z <= to.Y; z++)
            {
                for (int x = from.X; x <= to.X; x++)
                {
                    if (objects.InSector(new Vector2i(x, z)) is not IReadOnlyList<WorldObject> items) continue;
                    for (int i = 0; i < items.Count; i++)
                    {
                        WorldObject item = items[i];
                        if (_assets[item.Asset] is not TreeSpec spec) continue;

                        float height = spec.Height * item.Scale.Y;
                        var tree = new StandingTree
                        {
                            At = item.Position.Xz,
                            CrownLow = item.Position.Y + height * spec.Low,
                            CrownHigh = item.Position.Y + height,
                            Radius = height * spec.Radius,
                            Kind = spec.Kind,
                        };
                        if (spec.Kind >= 0)
                        {
                            AmbientMotes motes = world.BiomeAt(tree.At).Motes;
                            float share = motes is AmbientMotes.FallingLeaves or AmbientMotes.Petals ? 1f : QuietShare;
                            tree.Rate = PerTree * share * MathF.Min(height / 16f, 1.3f);
                            _treeRate += tree.Rate;
                        }
                        _trees.Add(tree);
                    }
                }
            }
        }

        /// <summary>The nearest tree to a point, within reach.</summary>
        public bool TryTreeNear(Vector2 at, float reach, out StandingTree found)
        {
            found = default;
            float best = reach * reach;
            bool any = false;
            for (int i = 0; i < _trees.Count; i++)
            {
                float distance = (_trees[i].At - at).LengthSquared;
                if (distance >= best) continue;
                best = distance;
                found = _trees[i];
                any = true;
            }
            return any;
        }

        /// <summary>A tree to go to, some way off and not back the way it came.</summary>
        public bool TryTreeAway(Vector2 from, Vector2 away, float nearest, float furthest, out StandingTree found)
        {
            found = default;
            bool any = false;
            float best = float.NegativeInfinity;
            for (int i = 0; i < _trees.Count; i++)
            {
                Vector2 offset = _trees[i].At - from;
                float distance = offset.Length;
                if (distance < nearest || distance > furthest) continue;
                float score = Vector2.Dot(offset / distance, away) + _sim.Random.Range(0f, 0.6f);
                if (score < 0.3f || score <= best) continue;
                best = score;
                found = _trees[i];
                any = true;
            }
            return any;
        }

        // ---- Leaves.

        /// <summary>Shakes a tree, as something going through it does.</summary>
        public void Shake(in StandingTree tree, int count)
        {
            if (tree.Kind < 0) return;
            for (int i = 0; i < count; i++) Emit(tree, kick: 3f);
        }

        /// <summary>Sends leaves streaming the way the guiding wind is going, for as long as it's asked to.</summary>
        public void Guide(Vector2 direction, float perSecond)
        {
            _guide = direction;
            _guideRate = perSecond;
        }

        private ref Leaf New()
        {
            if (_count == _leaves.Length)
            {
                if (_count < _sim.View.MostLeaves) Array.Resize(ref _leaves, _count * 2);
                else
                {
                    // Full: the oldest of what's lying goes first, as nobody's watching it.
                    int oldest = 0;
                    float age = -1f;
                    for (int i = 0; i < _count; i += 3)
                    {
                        if (_leaves[i].State != State.Falling && _leaves[i].Age > age)
                        {
                            age = _leaves[i].Age;
                            oldest = i;
                        }
                    }
                    return ref _leaves[oldest];
                }
            }
            return ref _leaves[_count++];
        }

        private void Emit(in StandingTree tree, float kick)
        {
            if (_count >= _sim.View.MostLeaves && kick <= 0f) return;

            WorldRandom random = _sim.Random;
            Kind kind = _kinds[tree.Kind];

            // Off the outside of the crown, which is roughly an egg. It's where leaves are, and from up here anything that comes down inside one is behind it the whole way.
            float up = random.Float();
            float wide = MathF.Sqrt(MathF.Max(1f - (up * 2f - 1f) * (up * 2f - 1f), 0.1f)) * tree.Radius * random.Range(0.78f, 1.08f);
            Vector2 off = Turn.Heading(random.Range(-MathF.PI, MathF.PI)) * wide;
            Vector3 at = new Vector3(tree.At.X + off.X, tree.CrownLow + (tree.CrownHigh - tree.CrownLow) * up, tree.At.Y + off.Y);

            ref Leaf leaf = ref New();
            Dress(ref leaf, tree.Kind, kind, at);
            if (kick > 0f) leaf.Carried = off.LengthSquared > 1e-3f ? off.Normalized() * kick : Vector2.Zero;
        }

        private void Dress(ref Leaf leaf, int kindIndex, Kind kind, Vector3 at)
        {
            WorldRandom random = _sim.Random;

            // Mostly fluttering, some tumbling, and a few of each by turns. A petal hardly swings.
            float roll = random.Float();
            leaf.Card = kind.Petal ? (roll < 0.6f ? Sinking : (byte)0) : roll < 0.56f ? (byte)0 : roll < 0.8f ? (byte)1 : (byte)2;
            FallingCard card = Cards[leaf.Card];

            // Everything's in the units of a card, so a leaf is a speed and a size: fast enough to reach the ground in a few seconds, and swinging by as much as its size will bear. The rate is what keeps it under
            // two swings a second, as a speck that flickers quicker than that reads as a fault in the picture.
            float fall = kind.Petal ? random.Range(0.75f, 1.2f) : random.Range(1.15f, 2f);
            leaf.Swing = kind.Petal ? random.Range(0.09f, 0.15f) : random.Range(0.12f, 0.2f);
            leaf.Rate = fall / (card.MeanFall * leaf.Swing);
            leaf.Clock = random.Range(0f, 8f);
            card.At(leaf.Clock, out float x, out float y, out _);

            leaf.Azimuth = random.Range(-MathF.PI, MathF.PI);
            leaf.Precession = random.Range(-0.5f, 0.5f);
            leaf.Spin = random.Range(-MathF.PI, MathF.PI);
            leaf.Length = random.Range(kind.Length);
            leaf.At = at;
            float cos = MathF.Cos(leaf.Azimuth), sin = MathF.Sin(leaf.Azimuth);
            leaf.Origin = at - new Vector3(cos * x * leaf.Swing, y * leaf.Swing, sin * x * leaf.Swing);
            leaf.Carried = Vector2.Zero;
            leaf.Age = 0f;
            leaf.Lasts = random.Range(LieFrom, LieTo);
            leaf.Kind = (short)kindIndex;
            leaf.State = State.Falling;
            leaf.Guided = false;
            leaf.Normal = Vector3.UnitY;

            Vector3 own = kind.Colours.At(random.Range(0.45f, 1f));
            leaf.Colour = Vector3.Lerp(own, Dry * random.Range(0.75f, 1.2f), kind.Wither * random.Range(0.4f, 1f));
        }

        public void Step(float deltaTime)
        {
            _splashWait -= deltaTime;
            Shed(deltaTime);
            Stream(deltaTime);

            int turn = _stirTurn = (_stirTurn + 1) & 7;
            for (int i = _count - 1; i >= 0; i--)
            {
                ref Leaf leaf = ref _leaves[i];
                leaf.Age += deltaTime;

                bool keep = leaf.State switch
                {
                    State.Falling => Fall(ref leaf, deltaTime),
                    State.Settling => Settle(ref leaf, deltaTime),
                    State.Lying => Lie(ref leaf, (i & 7) == turn),
                    _ => Float(ref leaf, deltaTime),
                };

                if (!keep) _leaves[i] = _leaves[--_count];
            }
        }

        // Each tree lets go now and then, and far more of them when a gust is on it, so a gust can be seen going through a wood. It's thinned from the strongest it could be, which needs no sum over every tree.
        private void Shed(float deltaTime)
        {
            if (_treeRate <= 0f || _trees.Count == 0) return;

            float wind = _sim.View.Wind.LiveSpeed;
            float breeze = 0.35f + MathF.Min(wind / 6f, 1.6f);
            _debt += _treeRate * MostBoost * breeze * deltaTime;

            WorldRandom random = _sim.Random;
            while (_debt >= 1f)
            {
                _debt -= 1f;
                StandingTree tree = _trees[random.Int(_trees.Count)];
                if (tree.Kind < 0 || !_sim.InView(tree.At, 10f)) continue;

                _sim.Air.At(tree.At, out float gust);
                float boost = 1f + (MostBoost - 1f) * Smooth(0.58f, 0.92f, gust);
                float thin = tree.Rate / (PerTree * 1.3f);
                if (random.Float() * MostBoost < boost && random.Float() < thin) Emit(tree, 0f);
            }
        }

        private void Stream(float deltaTime)
        {
            if (_guideRate <= 0f) return;
            _guideDebt += _guideRate * deltaTime;
            _guideRate = 0f;

            WorldRandom random = _sim.Random;
            Vector3 focus = _sim.View.Focus;
            while (_guideDebt >= 1f)
            {
                _guideDebt -= 1f;
                Vector2 at = focus.Xz + new Vector2(random.Range(-30f, 30f), random.Range(-30f, 30f));
                Vector3 where = new Vector3(at.X, _sim.Land.RoughGround(at) + random.Range(1.5f, 9f), at.Y);
                ref Leaf leaf = ref New();
                Dress(ref leaf, _guideKind, _kinds[_guideKind], where);
                leaf.Guided = true;
                leaf.Carried = _guide * GuideSpeed;
                leaf.Colour = GuideColour * random.Range(0.8f, 1.25f);
                leaf.Rate *= 0.34f;
                leaf.Lasts = random.Range(3.5f, 6f);
            }
        }

        private bool Fall(ref Leaf leaf, float deltaTime)
        {
            Vector2 at = leaf.At.Xz;
            float ground = _sim.Land.RoughGround(at);
            float high = leaf.At.Y - ground;

            Vector2 wind = _sim.Air.At(at, out float gust);
            float speed = wind.Length;
            Vector2 air = wind * (0.55f + 0.45f * MathF.Min(high / 12f, 1f)) + _sim.Air.Swirl(leaf.At) * (0.22f + 0.13f * speed);
            if (leaf.Guided) air = _guide * GuideSpeed + air * 0.4f;

            leaf.Carried = Vector2.Lerp(leaf.Carried, air, Spring.Share(CarryHalfLife, deltaTime));
            leaf.Origin.X += leaf.Carried.X * deltaTime;
            leaf.Origin.Z += leaf.Carried.Y * deltaTime;

            // A gust gets under it, and a leaf that's blown at a hill rides up it, as a real one would on the air going over.
            float lift = MathF.Max(gust - 0.62f, 0f) * speed * 0.8f;
            float ahead = _sim.Land.RoughGround(at + leaf.Carried * 0.9f);
            if (ahead > ground && high < 3f) lift += (ahead - ground) * 1.4f;
            leaf.Origin.Y += lift * deltaTime;

            leaf.Clock += leaf.Rate * deltaTime;
            leaf.Azimuth += leaf.Precession * deltaTime;
            Cards[leaf.Card].At(leaf.Clock, out float x, out float y, out _);
            float cos = MathF.Cos(leaf.Azimuth), sin = MathF.Sin(leaf.Azimuth);
            leaf.At = leaf.Origin + new Vector3(cos * x * leaf.Swing, y * leaf.Swing, sin * x * leaf.Swing);

            if (leaf.Age > 45f || !_sim.InView(leaf.At.Xz, DropMargin)) return false;
            if (leaf.Guided && leaf.Age > leaf.Lasts) return false;

            at = leaf.At.Xz;
            float level = _sim.Land.WaterLevel(at);
            ground = _sim.Land.RoughGround(at);
            if (level > ground + 0.05f)
            {
                if (leaf.At.Y > level) return true;
                leaf.At.Y = level;
                leaf.Normal = Vector3.UnitY;
                leaf.State = State.Floating;
                leaf.Age = 0f;
                if (_splashWait <= 0f && _sim.InView(at, 2f))
                {
                    _splashWait = 0.2f;
                    _sim.Splash(leaf.At, 0.5f, 0.012f);
                }
                return true;
            }

            if (leaf.At.Y > ground + 0.04f) return true;
            leaf.At.Y = ground + 0.04f;
            leaf.Normal = Orientation(leaf).Y;
            leaf.State = State.Settling;
            leaf.Age = 0f;
            return true;
        }

        private bool Settle(ref Leaf leaf, float deltaTime)
        {
            float t = MathF.Min(leaf.Age / SettleTime, 1f);
            Vector3 flat = GroundNormal(leaf.At.Xz);
            leaf.Normal = Vector3.Lerp(leaf.Normal, flat, 0.25f + 0.75f * t).Normalized();
            if (t >= 1f) leaf.State = State.Lying;
            return true;
        }

        private Vector3 GroundNormal(Vector2 at)
        {
            float x = _sim.Land.RoughGround(at + Vector2.UnitX) - _sim.Land.RoughGround(at - Vector2.UnitX);
            float z = _sim.Land.RoughGround(at + Vector2.UnitY) - _sim.Land.RoughGround(at - Vector2.UnitY);
            return new Vector3(-x * 0.5f, 1f, -z * 0.5f).Normalized();
        }

        private bool Lie(ref Leaf leaf, bool stirring)
        {
            if (leaf.Age > leaf.Lasts || !_sim.InView(leaf.At.Xz, DropMargin)) return false;
            if (!stirring) return true;

            Vector2 at = leaf.At.Xz;

            // A body going through kicks it up, and only the strongest gusts move what's already down.
            List<Scare> scares = _sim.View.Scares;
            for (int i = 0; i < scares.Count; i++)
            {
                Scare scare = scares[i];
                if (scare.Radius > 0f || scare.Moving < 0.4f) continue;
                Vector2 off = at - scare.At;
                float distance = off.LengthSquared;
                if (distance > StirReach * StirReach || distance < 1e-4f) continue;
                Lift(ref leaf, off / MathF.Sqrt(distance) * _sim.Random.Range(2f, 4.5f), 0.7f);
                return true;
            }

            _sim.Air.At(at, out float gust);
            if (gust > GustLifts && _sim.View.Wind.LiveSpeed > 3f && _sim.Random.Float() < 0.1f)
            {
                Vector2 wind = _sim.Air.At(at, out _);
                Lift(ref leaf, wind * 0.8f, 0.3f);
            }
            return true;
        }

        // Back in the air for a moment. It's let go from where it is, at the top of a small hop.
        private void Lift(ref Leaf leaf, Vector2 carried, float height)
        {
            Cards[leaf.Card].At(leaf.Clock, out float x, out float y, out _);
            leaf.At.Y += height;
            float cos = MathF.Cos(leaf.Azimuth), sin = MathF.Sin(leaf.Azimuth);
            leaf.Origin = leaf.At - new Vector3(cos * x * leaf.Swing, y * leaf.Swing, sin * x * leaf.Swing);
            leaf.Carried = carried;
            leaf.State = State.Falling;
            leaf.Age = 0f;
        }

        private bool Float(ref Leaf leaf, float deltaTime)
        {
            if (leaf.Age > leaf.Lasts || !_sim.InView(leaf.At.Xz, DropMargin)) return false;
            Vector2 wind = _sim.Air.At(leaf.At.Xz, out _);
            leaf.At.X += wind.X * 0.05f * deltaTime;
            leaf.At.Z += wind.Y * 0.05f * deltaTime;
            leaf.Spin += leaf.Precession * 0.2f * deltaTime;
            return true;
        }

        // ---- Drawing.

        private readonly record struct Frame(Vector3 X, Vector3 Y, Vector3 Z);

        // The card turns about its span, in the upright plane it's swinging in, and a leaf lies on the card however it happened to come off.
        private static Frame Orientation(in Leaf leaf)
        {
            Cards[leaf.Card].At(leaf.Clock, out _, out _, out float angle);
            float cos = MathF.Cos(leaf.Azimuth), sin = MathF.Sin(leaf.Azimuth);
            float turnCos = MathF.Cos(angle), turnSin = MathF.Sin(angle);
            Vector3 way = new Vector3(cos, 0f, sin);
            Vector3 chord = way * turnCos + Vector3.UnitY * turnSin;
            Vector3 normal = way * -turnSin + Vector3.UnitY * turnCos;
            Vector3 span = new Vector3(sin, 0f, -cos);

            float spinCos = MathF.Cos(leaf.Spin), spinSin = MathF.Sin(leaf.Spin);
            return new Frame(span * spinCos + chord * spinSin, normal, chord * spinCos - span * spinSin);
        }

        public void Draw()
        {
            for (int i = 0; i < _count; i++)
            {
                ref Leaf leaf = ref _leaves[i];
                if (!_sim.InView(leaf.At.Xz, 8f)) continue;

                Frame frame;
                if (leaf.State == State.Falling)
                {
                    frame = Orientation(leaf);
                }
                else
                {
                    // Flat on whatever it's on, however it was turned.
                    Vector3 y = leaf.Normal;
                    Vector3 along = new Vector3(MathF.Sin(leaf.Spin), 0f, MathF.Cos(leaf.Spin));
                    Vector3 x = Vector3.Cross(y, along).Normalized();
                    frame = new Frame(x, y, Vector3.Cross(x, y));
                }

                Batch batch = _sim.BatchOf(_kinds[leaf.Kind].Shape);
                Span<Matrix4> skin = batch.Next(out Span<Vector4> tint);
                float size = leaf.Length;
                skin[0] = new Matrix4(new Vector4(frame.X * size, 0f), new Vector4(frame.Y * size, 0f), new Vector4(frame.Z * size, 0f), new Vector4(leaf.At, 1f));

                float fade = 1f;
                if (leaf.State is State.Lying or State.Floating) fade = Math.Clamp((leaf.Lasts - leaf.Age) / FadeTime, 0f, 1f);
                else if (leaf.Guided) fade = Math.Clamp((leaf.Lasts - leaf.Age) / 0.8f, 0f, 1f) * Math.Clamp(leaf.Age / 0.4f, 0f, 1f);
                else if (leaf.Age < 0.35f && leaf.State == State.Falling) fade = leaf.Age / 0.35f;
                tint[0] = new Vector4(leaf.Colour, fade);
            }
        }

        private static float Smooth(float from, float to, float value)
        {
            float t = Math.Clamp((value - from) / (to - from), 0f, 1f);
            return t * t * (3f - 2f * t);
        }
    }
}

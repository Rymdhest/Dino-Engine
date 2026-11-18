using OpenTK.Mathematics;

namespace Dino_Engine.World
{
    /// <summary>
    /// The one way through a linear land: a chain of clearings, each joined to the next by a narrower pass, winding across the land from where heroes land at one end to the keep at the other, with
    /// mountains or the sea everywhere else, so that a land of this kind is walked from one end to the other with nothing to turn off for and nothing to come back for. It wanders, or now and then
    /// runs out and back as a lopsided U, and its stretches are held far enough apart that nothing between them can be walked. Headless, and drawn from the plan alone, so it's known before anything is
    /// grown (LANDS.md).
    /// </summary>
    public sealed class LandPath
    {
        /// <summary>A clearing on the way: where places stand, and where a fight has room.</summary>
        public readonly record struct Room(Vector2 Centre, float Radius);

        private const float Edge = 70f;             // Metres from the edge of the world that nothing of the way comes nearer than, as a wall or a coast has to stand there.
        private const float MostLegReach = 112f;    // The largest a clearing can be.
        private const float RoomStep = 185f;        // Metres from one clearing to the next, about.
        private const float SmallestRoom = 48f;

        /// <summary>
        /// Metres of wall or sea between any two stretches of the way that aren't next to each other on it, at the least, so that nobody can walk or wade from one leg to the next: a mountain's side
        /// takes some forty metres to rise out of reach, and a coast keeps a beach either side of a channel.
        /// </summary>
        public const float LeastBetween = 110f;

        public IReadOnlyList<Room> Rooms => _rooms;

        /// <summary>The half-width of each pass, from a clearing to the next.</summary>
        public IReadOnlyList<float> Passes => _passes;

        private readonly List<Room> _rooms;
        private readonly float[] _passes;

        private LandPath(List<Room> rooms, float[] passes)
        {
            _rooms = rooms;
            _passes = passes;
        }

        /// <summary>How far the way runs from one end to the other, through the middle of every clearing.</summary>
        public float Length
        {
            get
            {
                float length = 0f;
                for (int i = 1; i < _rooms.Count; i++) length += (_rooms[i].Centre - _rooms[i - 1].Centre).Length;
                return length;
            }
        }

        public Room First => _rooms[0];
        public Room Last => _rooms[^1];

        /// <summary>Metres outside the way a point is: below zero inside a clearing or a pass, nought at its edge.</summary>
        public float Outside(Vector2 point)
        {
            float best = float.MaxValue;
            for (int i = 0; i < _rooms.Count; i++)
            {
                best = MathF.Min(best, (point - _rooms[i].Centre).Length - _rooms[i].Radius);
                if (i + 1 < _rooms.Count) best = MathF.Min(best, DistanceToSegment(point, _rooms[i].Centre, _rooms[i + 1].Centre) - _passes[i]);
            }
            return best;
        }

        /// <summary>
        /// The way through a land of a size. Most wander: a walk across the land from near one edge, turning a little at every clearing and more where the edge or its own earlier stretches are in
        /// the way, so that it bends, hooks and doubles back as a valley or a spit of land does, and never runs near itself. The rest are a U, two legs side by side, lopsided as anything grown is.
        /// A perfect S of three or more legs was too long and looked drawn, so there's none.
        /// </summary>
        public static LandPath Draw(float size, WorldRandom random)
        {
            List<Vector2>? spine = random.Chance(UShare) ? null : Wander(size, random.Fork("wander"));
            spine ??= Hairpin(size, random.Fork("hairpin"));
            return Grow(spine, size, random.Fork("rooms"));
        }

        private const float UShare = 0.35f;
        private const float Inset = 165f;           // Metres from the edge of the world that a clearing's middle keeps, so that it has room round it before the edge.
        private const float FarFromSelf = 270f;     // Metres a clearing keeps from any of the way's clearings but the last two before it.
        private const float FarFromPass = 215f;     // And from any pass but those that lead to it.
        private const float ShortestWalk = 0.9f;    // Of the land's size, the least a wandering way runs before it may end, or it's tried again.
        private const int WanderTries = 24;

        // A walk from near one edge of the land, a clearing at every step, its turning eased from one step to the next so that it curves rather than zigzags, as long as the land allows.
        private static List<Vector2>? Wander(float size, WorldRandom random)
        {
            float target = size * random.Range(1.2f, 1.65f);
            for (int attempt = 0; attempt < WanderTries; attempt++)
            {
                WorldRandom walk = random.Fork(attempt);
                // Start somewhere along the middle of an edge, heading in.
                int side = walk.Int(4);
                float along = walk.Range(0.25f, 0.75f) * size;
                Vector2 start = side switch
                {
                    0 => new Vector2(along, Inset),
                    1 => new Vector2(size - Inset, along),
                    2 => new Vector2(along, size - Inset),
                    _ => new Vector2(Inset, along),
                };
                float heading = side switch { 0 => MathF.PI * 0.5f, 1 => MathF.PI, 2 => -MathF.PI * 0.5f, _ => 0f } + walk.Range(-0.5f, 0.5f);
                var spine = new List<Vector2> { start };
                float walked = 0f, bend = walk.Range(-0.35f, 0.35f);
                while (walked < target)
                {
                    // A turn eased from the last, and then, if that way is blocked, the nearest way that isn't, either side.
                    bend = bend * 0.65f + walk.Range(-0.32f, 0.32f);
                    float step = RoomStep * walk.Range(0.85f, 1.12f);
                    bool moved = false;
                    for (int k = 0; k < 9 && !moved; k++)
                    {
                        float turn = k == 0 ? 0f : (k + 1) / 2 * 0.3f * (k % 2 == 1 == bend >= 0f ? 1f : -1f);
                        float way = heading + bend + turn;
                        Vector2 next = spine[^1] + new Vector2(MathF.Cos(way), MathF.Sin(way)) * step;
                        if (!Fits(spine, next, size)) continue;
                        spine.Add(next);
                        walked += step;
                        heading = way;
                        moved = true;
                    }
                    if (!moved) break;
                }
                if (walked >= size * ShortestWalk) return spine;
            }
            return null;
        }

        // Whether a clearing can go here: in from the edge, and far enough from the way's earlier stretches that nothing between could be walked.
        private static bool Fits(List<Vector2> spine, Vector2 next, float size)
        {
            if (next.X < Inset || next.Y < Inset || next.X > size - Inset || next.Y > size - Inset) return false;
            for (int i = 0; i < spine.Count - 2; i++)
            {
                if ((spine[i] - next).Length < FarFromSelf) return false;
                if (i + 1 < spine.Count - 1 && DistanceToSegment(next, spine[i], spine[i + 1]) < FarFromPass) return false;
            }
            // Nor may the pass to it run past an earlier clearing.
            for (int i = 0; i < spine.Count - 2; i++)
            {
                if (DistanceToSegment(spine[i], spine[^1], next) < FarFromPass) return false;
            }
            return true;
        }

        // Two legs side by side, joined across one end, each starting and ending somewhere of its own and bowed a little, so that no two are the same shape.
        private static List<Vector2> Hairpin(float size, WorldRandom random)
        {
            bool alongZ = random.Chance(0.65f);
            bool backwards = random.Chance(0.5f), turned = random.Chance(0.5f);
            float apart = (size - 2f * Edge) / 2f;
            Vector2 At(float a, float across) => alongZ ? new Vector2(across, a) : new Vector2(a, across);
            var spine = new List<Vector2>();
            for (int k = 0; k < 2; k++)
            {
                int leg = backwards ? 1 - k : k;
                float across = Edge + (leg + 0.5f) * apart + random.Range(-0.12f, 0.12f) * apart;
                // The open end of each leg stops wherever it does, and the turn is at the same end for both.
                float from = Inset + random.Range(0f, 0.3f) * size;
                float to = size - Inset - random.Range(0f, 0.12f) * size;
                bool forward = (k == 0) != turned;
                float a0 = forward ? from : to, a1 = forward ? to : from;
                int steps = Math.Max(2, (int)MathF.Round(MathF.Abs(a1 - a0) / RoomStep));
                float bow = random.Range(-0.18f, 0.18f) * apart;
                for (int s = 0; s <= steps; s++)
                {
                    float t = (float)s / steps;
                    spine.Add(At(a0 + (a1 - a0) * t, across + bow * MathF.Sin(t * MathF.PI)));
                }
            }
            return spine;
        }

        // Clearings along a spine, as large as a place needs now and then, and passes between them, made smaller wherever a stretch further along or further back would come too near.
        private static LandPath Grow(List<Vector2> spine, float size, WorldRandom random)
        {
            var rooms = new List<Room>();
            for (int i = 0; i < spine.Count; i++)
            {
                Vector2 at = spine[i];
                if (i > 0 && i + 1 < spine.Count) at += new Vector2(random.Range(-18f, 18f), random.Range(-18f, 18f));
                rooms.Add(new Room(at, random.Chance(0.35f) ? random.Range(96f, 112f) : random.Range(62f, 90f)));
            }
            // Where heroes land has room for the landing and a fight off it, and the keep's clearing for a keep.
            rooms[0] = rooms[0] with { Radius = random.Range(86f, 100f) };
            rooms[^1] = rooms[^1] with { Radius = random.Range(104f, MostLegReach) };

            float[] passes = new float[rooms.Count - 1];
            for (int i = 0; i < passes.Length; i++) passes[i] = random.Range(17f, 28f);

            // Nothing on the way comes within LeastBetween of a stretch of it further along or further back: a clearing that would is made smaller, two that would each by half. What's within two
            // clearings of one is the same bend of the way, and walking across that rather than round it takes nobody anywhere new. Nor does a clearing reach the edge of the world.
            float[] drawn = rooms.Select(room => room.Radius).ToArray();
            for (int i = 0; i < rooms.Count; i++)
            {
                Vector2 c = rooms[i].Centre;
                float radius = MathF.Min(drawn[i], MathF.Min(MathF.Min(c.X, size - c.X), MathF.Min(c.Y, size - c.Y)) - Edge);
                for (int j = 0; j < rooms.Count; j++)
                {
                    if (Math.Abs(i - j) <= 2) continue;
                    float over = drawn[i] + drawn[j] + LeastBetween - (c - rooms[j].Centre).Length;
                    if (over > 0f) radius = MathF.Min(radius, drawn[i] - over * 0.5f);
                }
                for (int p = 0; p < passes.Length; p++)
                {
                    if (p >= i - 2 && p <= i + 1) continue;
                    radius = MathF.Min(radius, DistanceToSegment(c, rooms[p].Centre, rooms[p + 1].Centre) - passes[p] - LeastBetween);
                }
                rooms[i] = rooms[i] with { Radius = MathF.Max(radius, SmallestRoom) };
            }
            return new LandPath(rooms, passes);
        }

        /// <summary>A way laid out by hand, clearing by clearing, as the tutorial's is (<see cref="Content.TutorialLand"/>): one pass fewer than there are clearings.</summary>
        public static LandPath Laid(IReadOnlyList<Room> rooms, IReadOnlyList<float> passes)
        {
            if (rooms.Count < 2 || passes.Count != rooms.Count - 1) throw new ArgumentException("A way needs two clearings at least, and a pass between each two.");
            return new LandPath(rooms.ToList(), passes.ToArray());
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.LengthSquared > 1e-6f ? Math.Clamp(Vector2.Dot(point - a, ab) / ab.LengthSquared, 0f, 1f) : 0f;
            return (point - (a + ab * t)).Length;
        }
    }
}

using OpenTK.Mathematics;

namespace Dino_Engine.World
{
    /// <summary>
    /// The one way through a linear land: a chain of clearings, each joined to the next by a narrower pass, winding back and forth across the land from where heroes land at one end to the keep at the
    /// other, with mountains or the sea everywhere else, so that a land of this kind is walked from one end to the other with nothing to turn off for and nothing to come back for. The chain runs in legs,
    /// back and forth, so that it fills a square land rather than running off it, and the legs are held far enough apart that nothing between them can be walked. Headless, and drawn from the plan alone,
    /// so it's known before anything is grown (LANDS.md).
    /// </summary>
    public sealed class LandPath
    {
        /// <summary>A clearing on the way: where places stand, and where a fight has room.</summary>
        public readonly record struct Room(Vector2 Centre, float Radius);

        private const float Edge = 70f;             // Metres from the edge of the world that nothing of the way comes nearer than, as a wall or a coast has to stand there.
        private const float LegApart = 330f;        // Metres from one leg to the next, usually: what the way's clearings and the wall between need.
        private const float LegsClosest = 245f;     // And the least, for a way that winds more often, its clearings smaller.
        private const float MostLegReach = 112f;    // The largest a clearing at the end of a leg can be, which keeps the legs that far in from the edge.
        private const float RoomStep = 185f;        // Metres from one clearing to the next along a leg, about.
        private const float SmallestRoom = 48f;
        private const int MostLegs = 7;

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
        /// The way through a land of a size, back and forth across it in legs, north and south more often than east and west, as walls either side of the way stand beside a hero from this camera
        /// rather than between them and it.
        /// </summary>
        public static LandPath Draw(float size, WorldRandom random)
        {
            bool alongZ = random.Chance(0.65f);
            // As many legs as fit, or now and then one more, closer together, so that one land's way is a U and another's an S.
            int legs = Math.Clamp((int)((size - 2f * Edge) / LegApart), 2, MostLegs);
            if (legs < MostLegs && (size - 2f * Edge) / (legs + 1) >= LegsClosest && random.Chance(0.5f)) legs++;
            float apart = (size - 2f * Edge) / legs;
            bool backwards = random.Chance(0.5f), turned = random.Chance(0.5f);
            float from = Edge + MostLegReach, to = size - Edge - MostLegReach;
            Vector2 At(float along, float across) => alongZ ? new Vector2(across, along) : new Vector2(along, across);

            // The corners of the way: each leg's two ends, the turn between legs running straight across from one to the next.
            var corners = new List<Vector2>();
            for (int k = 0; k < legs; k++)
            {
                int leg = backwards ? legs - 1 - k : k;
                float across = Edge + (leg + 0.5f) * apart;
                bool forward = (k % 2 == 0) != turned;
                corners.Add(At(forward ? from : to, across));
                corners.Add(At(forward ? to : from, across));
            }

            // A clearing at every corner, and between them along each stretch every so often, nudged off the line so the way wanders, and as large as a place needs now and then.
            var rooms = new List<Room>();
            for (int c = 0; c + 1 < corners.Count; c++)
            {
                Vector2 a = corners[c], b = corners[c + 1];
                Vector2 along = (b - a).Normalized(), side = new Vector2(-along.Y, along.X);
                int steps = Math.Max(1, (int)MathF.Round((b - a).Length / (RoomStep * random.Range(0.88f, 1.12f))));
                for (int s = 0; s < steps; s++)
                {
                    Vector2 at = Vector2.Lerp(a, b, (float)s / steps);
                    bool corner = s == 0;
                    if (!corner) at += side * random.Range(-0.12f, 0.12f) * apart + along * random.Range(-15f, 15f);
                    rooms.Add(new Room(at, random.Chance(0.35f) ? random.Range(96f, 112f) : random.Range(62f, 90f)));
                }
            }
            rooms.Add(new Room(corners[^1], random.Range(104f, MostLegReach)));
            // Where heroes land has room for the landing and a fight off it.
            rooms[0] = rooms[0] with { Radius = random.Range(86f, 100f) };

            float[] passes = new float[rooms.Count - 1];
            for (int i = 0; i < passes.Length; i++) passes[i] = random.Range(17f, 28f);

            // Nothing on the way comes within LeastBetween of a stretch of it further along or further back, which is what a leg beside it is: a clearing that would is made smaller, two that would
            // each by half. What's within two clearings of one is the same corner of the way, and walking across that corner rather than round it takes nobody anywhere new.
            float[] drawn = rooms.Select(room => room.Radius).ToArray();
            for (int i = 0; i < rooms.Count; i++)
            {
                float radius = drawn[i];
                for (int j = 0; j < rooms.Count; j++)
                {
                    if (Math.Abs(i - j) <= 2) continue;
                    float over = drawn[i] + drawn[j] + LeastBetween - (rooms[i].Centre - rooms[j].Centre).Length;
                    if (over > 0f) radius = MathF.Min(radius, drawn[i] - over * 0.5f);
                }
                for (int p = 0; p < passes.Length; p++)
                {
                    if (p >= i - 2 && p <= i + 1) continue;
                    radius = MathF.Min(radius, DistanceToSegment(rooms[i].Centre, rooms[p].Centre, rooms[p + 1].Centre) - passes[p] - LeastBetween);
                }
                rooms[i] = rooms[i] with { Radius = MathF.Max(radius, SmallestRoom) };
            }
            return new LandPath(rooms, passes);
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.LengthSquared > 1e-6f ? Math.Clamp(Vector2.Dot(point - a, ab) / ab.LengthSquared, 0f, 1f) : 0f;
            return (point - (a + ab * t)).Length;
        }
    }
}

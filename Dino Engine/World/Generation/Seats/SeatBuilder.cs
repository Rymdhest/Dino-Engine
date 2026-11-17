using Dino_Engine.Modelling.Procedural.Architecture.Places;
using Dino_Engine.Modelling.Procedural.Architecture.Seats;
using Dino_Engine.Modelling.Procedural.Fence;
using Dino_Engine.World.Content;
using Dino_Engine.World.Generation.Places;
using OpenTK.Mathematics;
using System.Linq;

namespace Dino_Engine.World.Generation.Seats
{
    /// <summary>
    /// What every commander's seat is laid out with (SEATS.md). A seat is a stage set for the game's camera, which looks north from above: whatever's tallest stands along its back, to the
    /// north, its sides step down, and its front stays low, so that the yard in its middle, where its tyrant waits, is seen whole. Three of its sides have a way in, so that a fight can
    /// spill out onto the land and come back by another, and the road is brought round to one of them (<see cref="Frame"/>). Its ground is laid level, the yard is kept clear of anything
    /// solid (ARENAS.md), and great fires stand round it. Each kind has a builder of its own, and each of those draws every decision from a fork of its own.
    /// </summary>
    internal abstract class SeatBuilder : PlaceBuilder
    {
        public const float YardShare = 0.5f;            // Of a seat's radius, the open yard in its middle where its tyrant waits.
        private const float SideShare = 0.8f;           // Of its radius, how far its side walls stand from its middle,
        private const float BackShare = 0.86f;          // its back,
        private const float FrontShare = 0.74f;         // and its front.
        private const float HeartAhead = 0.06f;         // Of its radius, how far in front of its middle its tyrant waits, which leaves the back the room for what stands there.
        public const float CutShare = 1.3f;             // Of its radius, where the roads that come to it are cut, to be brought round to a gate.
        private const float RingClear = 12f;            // Metres outside its walls that a road runs round them.
        private const float CornerEase = 7f;            // Metres either side of a corner of that ring the road leaves it by, so that it rounds a tower and doesn't clip it.
        private const float Flat = 3f;                  // Metres outside its walls that its ground is laid level,
        private const float Apron = RingClear + 2f;     // and how far outside each way in, where the road comes up to it,
        private const float ApronHalf = 7f;             // that far either side of the way.

        protected readonly SeatPlan Seat;
        protected readonly float R, Hx, Back, Front, Yard, Level;
        protected readonly Vector2 Heart;               // In the seat's own frame.
        protected readonly Side MainSide;
        protected static readonly Side[] GateSides = { Side.Front, Side.Left, Side.Right };
        private readonly List<HitPart> _hit = new();

        protected SeatBuilder(SiteBuilder b, string name) : base(b, name)
        {
            if (Site.Seat is not { Framed: true }) Frame(World, Site);
            Seat = Site.Seat!;
            R = Site.Radius;
            Hx = Seat.Side;
            Back = Seat.Back;
            Front = Seat.Front;
            Yard = Seat.Yard;
            Level = Seat.Level;
            Plan.Yaw = Seat.Yaw;
            Plan.Half = new Vector2(Hx, MathF.Max(Back, Front));
            Heart = Plan.ToLocal(Seat.Heart);
            MainSide = Seat.GateSide switch { 1 => Side.Left, 2 => Side.Right, _ => Side.Front };
        }

        /// <summary>Lays a keep out as the kind of seat it is.</summary>
        public static void Build(SiteBuilder b)
        {
            if (b.Site.Seat is not { Framed: true }) Frame(b.World, b.Site);
            SeatBuilder seat = b.Site.Seat!.Kind switch
            {
                SeatKind.Boneyard => new BoneyardBuilder(b),
                SeatKind.Barrow => new BarrowBuilder(b),
                SeatKind.Bileworks => new BileworksBuilder(b),
                SeatKind.Hatchery => new HatcheryBuilder(b),
                SeatKind.Foundry => new FoundryBuilder(b),
                SeatKind.Triumph => new TriumphBuilder(b),
                SeatKind.Warcamp => new WarcampBuilder(b),
                _ => new CitadelBuilder(b),
            };
            seat.Lay();
            seat.Finish();
        }

        /// <summary>Lays the seat out.</summary>
        public abstract void Lay();

        // Once everything of the seat's own is down, the whole of its ground is kept: no boulder or wild tree comes up inside it, least of all on a way in.
        private void Finish()
        {
            World.Placement.Add(BoxOf(new Vector2(-Hx - 2f, -Back - 2f), new Vector2(Hx + 2f, Front + 2f)), PlacementLayer.Keepout);
            foreach (Side side in GateSides) World.Placement.Add(Footprint.Segment(W(GateAt(side) + Outward(side) * 9f), W(GateAt(side)), 4f), PlacementLayer.Keepout);
        }

        /// <summary>
        /// How much room there is for a seat somewhere, 0 to 1: whether there's land a person can walk outside each of its three ways in, which count for most, at its corners and
        /// along its walls, and where its road would run round to its front. A seat always faces the camera, so where its ways in are is known before it's sited (SiteStage).
        /// </summary>
        public static float RoomAt(WorldData world, Vector2 centre, float radius)
        {
            float hx = radius * SideShare, back = radius * BackShare, front = radius * FrontShare, heart = radius * HeartAhead;
            bool Land(float x, float y)
            {
                Vector2 at = centre - new Vector2(x, y);
                return world.InBounds(at) && world.Elevation.Sample(at) >= WorldPlan.SeaLevel + 0.6f && world.Reachable.Sample(at) >= 0.5f;
            }
            float got = 0f, of = 0f;
            void Count(float x, float y, float weight)
            {
                of += weight;
                if (Land(x, y)) got += weight;
            }
            foreach (float out_ in new[] { 5f, 10f, 16f })
            {
                Count(0f, front + out_, 3f);
                Count(-hx - out_, heart, 3f);
                Count(hx + out_, heart, 3f);
            }
            foreach (float s in new[] { -1f, 1f })
            {
                Count(s * hx, -back, 1f);
                Count(s * hx, front, 1f);
                Count(s * hx, -back * 0.5f, 1f);
                Count(s * hx, front * 0.6f, 1f);
                Count(s * hx * 0.5f, front, 1f);
                Count(s * hx * 0.5f, -back, 1f);
                Count(s * (hx + RingClear), front + RingClear, 1f);
            }
            Count(0f, -back, 1f);
            // Where whatever it's built round stands, across its back, and the ground between that and its sides.
            foreach (float x in new[] { -0.6f, -0.3f, 0f, 0.3f, 0.6f })
            {
                Count(x * hx, -back + 6f, 2f);
                Count(x * hx, -back + 16f, 2f);
            }
            // And all of the ground inside it, as a seat that takes a land's only hill for its floor takes the hill away.
            const int Across = 12;
            for (int i = 0; i <= Across; i++)
            {
                for (int j = 0; j <= Across; j++) Count(-hx + 2f * hx * i / Across, -back + (back + front) * j / Across, 0.5f);
            }
            return got / of;
        }

        // ---------------------------------------------------------------- The frame, and the road to its gate

        /// <summary>
        /// Works a seat's frame out, and brings its road round to a gate. A seat always faces the camera, its back to the north, so a road can't simply run in where it happens to come
        /// from: every road that came to the seat is cut where it nears it, and carried on round the outside of the walls to the nearest of its three gates, in at it, and no further.
        /// Called before the roads are reserved, as a churchyard's are ended at its wall.
        /// </summary>
        public static void Frame(WorldData world, WorldSite site)
        {
            SeatPlan seat = site.Seat ??= new SeatPlan { Kind = SeatCatalog.KindOf(world.Plan.Seed) };
            float r = site.Radius;
            seat.Yaw = MathF.PI;
            seat.Side = r * SideShare;
            seat.Back = r * BackShare;
            seat.Front = r * FrontShare;
            seat.Yard = r * YardShare;
            seat.Heart = ToWorld(site, new Vector2(0f, r * HeartAhead));
            seat.Level = LevelOf(world, site);
            seat.GateSide = 0;
            seat.Framed = true;
            OpenGround(world, site, seat);

            Vector2 centre = site.Position;
            float cut = r * CutShare;
            bool first = true;
            // Counted before any is added, as each road brought round is a road of its own.
            int roads = world.Roads.Count;
            var gone = new List<WorldRoad>();
            for (int i = 0; i < roads; i++)
            {
                WorldRoad road = world.Roads[i];
                List<Vector3> points = road.Points;
                bool atEnd = false, atStart = false;
                while (points.Count > 2 && (points[^1].Xz - centre).Length < cut)
                {
                    points.RemoveAt(points.Count - 1);
                    atEnd = true;
                }
                while (points.Count > 2 && (points[0].Xz - centre).Length < cut)
                {
                    points.RemoveAt(0);
                    atStart = true;
                }
                // What's left of a road that only came from close by, a watchtower's knoll or a fork, is too short to come down from wherever it began: 18 m of one was left
                // at a grade of four in ten. The way round starts where that road did, and takes its place.
                bool stub = (atEnd || atStart) && road.Length < 40f;
                foreach ((bool cutHere, Vector3 from) in new[] { (atEnd, stub ? points[0] : points[^1]), (atStart, stub ? points[^1] : points[0]) })
                {
                    if (!cutHere || (from.Xz - centre).Length > cut + 40f) continue;
                    int side = BringRound(world, site, seat, road.Class, from);
                    if (stub && side >= 0 && !gone.Contains(road)) gone.Add(road);
                    if (!first || side < 0) continue;
                    // The first road is the seat's own, and its gate the great one.
                    first = false;
                    seat.GateSide = side;
                    site.RoadDirection = (ToWorld(site, GateOf(seat, side)) - centre).Normalized();
                }
            }
            foreach (WorldRoad road in gone) world.Roads.Remove(road);
        }

        // The ground a seat can't be without is ground people walk, all of it: its yard and what's round it, the back of it where what it's built round stands, and the ground
        // outside each of its ways in. What the survey before it took there for the foot of a hill (WorldData.Reachable) is laid level, so its hall isn't left out for standing on
        // it, no way in opens onto a cliff, and no rock is sculpted in its yard. Its corners are left as they are: a land with little rock keeps what it has, and a wall that
        // comes to a cliff stops at it. A seat is sited where there's room (RoomAt), so this is the little that's left over. The sea stays the sea.
        private static void OpenGround(WorldData world, WorldSite site, SeatPlan seat)
        {
            ScalarField open = world.Reachable, land = world.Elevation;
            float reach = Apron + 2f, heart = seat.Side / SideShare * HeartAhead;
            Vector2 min = site.Position - new Vector2(seat.Side + reach, seat.Front + reach), max = site.Position + new Vector2(seat.Side + reach, seat.Back + reach);
            int x0 = Math.Max((int)(min.X / open.CellSize), 0), x1 = Math.Min((int)(max.X / open.CellSize) + 1, open.Width - 1);
            int z0 = Math.Max((int)(min.Y / open.CellSize), 0), z1 = Math.Min((int)(max.Y / open.CellSize) + 1, open.Depth - 1);
            for (int z = z0; z <= z1; z++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 p = open.WorldPosition(x, z), local = site.Position - p;
                    bool yard = (local - new Vector2(0f, heart)).Length <= seat.Yard + 16f;
                    bool back = MathF.Abs(local.X) <= seat.Side * 0.62f && local.Y >= -seat.Back - Flat && local.Y <= heart;
                    bool front = MathF.Abs(local.X) <= ApronHalf && local.Y > 0f && local.Y <= seat.Front + Apron;
                    bool side = MathF.Abs(local.Y - heart) <= ApronHalf && MathF.Abs(local.X) <= seat.Side + Apron;
                    bool ward = MathF.Abs(local.Y - heart) <= 24f && MathF.Abs(local.X) <= seat.Side + Flat;
                    if ((yard || back || front || side || ward) && world.InBounds(p) && land.Sample(p) > WorldPlan.SeaLevel + 0.5f) open[x, z] = 1f;
                }
            }
        }

        // The road from where it was cut round to a gate: the nearest, if there's ground the whole way, or the next. The gate's side, or -1 if there's no way round to any.
        private static int BringRound(WorldData world, WorldSite site, SeatPlan seat, RoadClass roadClass, Vector3 from)
        {
            Vector2 p = ToLocal(site, from.Xz);
            int near = p.X < 0f ? 1 : 2, far = p.X < 0f ? 2 : 1;
            int[] order = p.Y >= MathF.Abs(p.X) * 0.8f ? new[] { 0, near, far } : new[] { near, 0, far };
            // The shorter way round to each first, and then the longer, which may be the only one there's ground for.
            foreach (bool shorter in new[] { true, false })
            {
                foreach (int side in order)
                {
                    List<Vector2>? corners = Route(world, site, seat, p, side, shorter);
                    if (corners == null) continue;
                    WorldRoad? approach = RoadStage.BuildRoad(world, corners, roadClass, from.Y, seat.Level);
                    if (approach == null) continue;
                    world.Roads.Add(approach);
                    return side;
                }
            }
            return -1;
        }

        // The way from a point outside the walls to a gate: out to the ring round them, along it the shorter way, easing round each corner it passes, and square in at the gate.
        private static List<Vector2>? Route(WorldData world, WorldSite site, SeatPlan seat, Vector2 p, int side, bool shorter = true)
        {
            Vector2 gate = GateOf(seat, side), outward = OutwardOf(side);
            float m = RingClear;
            Vector2[] ring =
            {
                new Vector2(seat.Side + m, seat.Front + m), new Vector2(-seat.Side - m, seat.Front + m),
                new Vector2(-seat.Side - m, -seat.Back - m), new Vector2(seat.Side + m, -seat.Back - m),
            };
            var starts = new float[4];
            float total = 0f;
            for (int i = 0; i < 4; i++)
            {
                starts[i] = total;
                total += (ring[(i + 1) % 4] - ring[i]).Length;
            }
            float Along(Vector2 q, out Vector2 on)
            {
                float best = float.MaxValue, at = 0f;
                on = ring[0];
                for (int i = 0; i < 4; i++)
                {
                    Vector2 a = ring[i], span = ring[(i + 1) % 4] - a;
                    float t = Math.Clamp(Vector2.Dot(q - a, span) / span.LengthSquared, 0f, 1f);
                    Vector2 candidate = a + span * t;
                    float away = (candidate - q).LengthSquared;
                    if (away >= best) continue;
                    best = away;
                    on = candidate;
                    at = starts[i] + t * span.Length;
                }
                return at;
            }
            float fromAt = Along(p, out Vector2 onFrom), toAt = Along(gate + outward * m, out Vector2 onTo);
            float forward = ((toAt - fromAt) % total + total) % total;
            bool goForward = forward <= total - forward == shorter;
            float walk = goForward ? forward : total - forward;

            var local = new List<Vector2> { p };
            void Add(Vector2 q)
            {
                if ((q - local[^1]).Length > 3.5f) local.Add(q);
            }
            Add(onFrom);
            // The corners passed on the way, nearest first, each left a little before it and joined again a little after.
            var passed = new List<(float Away, int Corner)>();
            for (int k = 0; k < 4; k++)
            {
                float away = goForward ? ((starts[k] - fromAt) % total + total) % total : ((fromAt - starts[k]) % total + total) % total;
                if (away > 0.01f && away < walk) passed.Add((away, k));
            }
            passed.Sort((a, c) => a.Away.CompareTo(c.Away));
            foreach ((float _, int k) in passed)
            {
                Vector2 corner = ring[k];
                Vector2 before = (ring[(k + 3) % 4] - corner).Normalized() * CornerEase, after = (ring[(k + 1) % 4] - corner).Normalized() * CornerEase;
                Add(corner + (goForward ? before : after));
                Add(corner + (goForward ? after : before));
            }
            Add(onTo);
            Add(gate + outward * 5f);
            local.Add(gate - outward * 2.5f);

            // Every step of it over ground a road can lie on: land, and but for its first leg, which is the way the road was coming anyway, land the survey had people walking.
            var corners = new List<Vector2>(local.Count);
            for (int i = 0; i < local.Count; i++)
            {
                Vector2 here = ToWorld(site, local[i]);
                if (i > 0)
                {
                    Vector2 last = corners[^1];
                    int steps = Math.Max(1, (int)((here - last).Length / 3f));
                    for (int s = 1; s <= steps; s++)
                    {
                        Vector2 at = Vector2.Lerp(last, here, s / (float)steps);
                        if (!world.InBounds(at) || world.Elevation.Sample(at) < WorldPlan.SeaLevel + 0.6f || (i > 1 && world.Reachable.Sample(at) < 0.5f)) return null;
                    }
                }
                corners.Add(here);
            }
            return corners;
        }

        private static Vector2 ToWorld(WorldSite site, Vector2 local) => site.Position - local;         // The frame's yaw is half a turn: its +x is the world's -x, and its front is south.
        private static Vector2 ToLocal(WorldSite site, Vector2 world) => site.Position - world;

        /// <summary>Where each way in is, in the seat's own frame: the middle of its front, and its sides level with where its tyrant waits.</summary>
        public static Vector2 GateOf(SeatPlan seat, int side)
        {
            float heart = seat.Side / SideShare * HeartAhead;
            return side switch
            {
                1 => new Vector2(-seat.Side, heart),
                2 => new Vector2(seat.Side, heart),
                _ => new Vector2(0f, seat.Front),
            };
        }

        private static Vector2 OutwardOf(int side) => side switch
        {
            1 => -Vector2.UnitX,
            2 => Vector2.UnitX,
            _ => Vector2.UnitY,
        };

        // The height a seat's ground is laid level at: what the land there comes to, over all of it.
        private static float LevelOf(WorldData world, WorldSite site)
        {
            float total = world.Elevation.Sample(site.Position);
            int samples = 1;
            foreach (float share in new[] { 0.3f, 0.6f })
            {
                for (int i = 0; i < 8; i++)
                {
                    Vector2 at = site.Position + SiteBuilder.Direction(i * MathF.Tau / 8f) * (site.Radius * share);
                    if (!world.InBounds(at)) continue;
                    total += world.Elevation.Sample(at);
                    samples++;
                }
            }
            return MathF.Max(total / samples, WorldPlan.SeaLevel + 1f);
        }

        // ---------------------------------------------------------------- Ground

        protected Vector2 GateAt(Side side) => side switch
        {
            Side.Left => new Vector2(-Hx, Heart.Y),
            Side.Right => new Vector2(Hx, Heart.Y),
            _ => new Vector2(0f, Front),
        };

        protected static Vector2 Inward(Side side) => -Outward(side);

        /// <summary>A box of the seat's own frame, from one corner to the other, as a footprint in the world.</summary>
        protected Footprint BoxOf(Vector2 from, Vector2 to) => Footprint.Box(W((from + to) * 0.5f), new Vector2(MathF.Abs(to.X - from.X), MathF.Abs(to.Y - from.Y)) * 0.5f, Plan.Yaw);

        /// <summary>Lays the whole seat's ground level, a little past its walls, and the ground outside each of its ways in, easing back into the land beyond.</summary>
        protected void LevelGround(float past = Flat, float margin = 9f)
        {
            World.Pads.Add(new GroundPad(BoxOf(new Vector2(-Hx - past, -Back - past), new Vector2(Hx + past, Front + past)), Level, margin));
            World.Pads.Add(new GroundPad(BoxOf(new Vector2(-ApronHalf, Front), new Vector2(ApronHalf, Front + Apron)), Level, margin));
            foreach (float s in new[] { -1f, 1f }) World.Pads.Add(new GroundPad(BoxOf(new Vector2(s * Hx, Heart.Y - ApronHalf), new Vector2(s * (Hx + Apron), Heart.Y + ApronHalf)), Level, margin));
        }

        /// <summary>
        /// The yard: kept clear of anything put down after, painted, and its tyrant stood in the middle of it. Nobody of the garrison camps in it (PackStage), and
        /// <c>WorldValidator.CheckKeepYard</c> holds it open.
        /// </summary>
        protected void OpenYard(GroundPaint paint, float share = 0.9f, float softness = 5f)
        {
            Keep(Footprint.Circle(W(Heart), Yard));
            Paint(paint, Footprint.Circle(W(Heart), Yard * share), softness);
            B.Spawn(SpawnRole.Boss, W(Heart));
        }

        /// <summary>
        /// A band of mud round the yard, <paramref name="wide"/> across: where a tyrant has been walked out and back, and everyone who keeps it has gone round it, until no turf is
        /// left. Whatever lies low at the yard's edge shows on it, where the land's grass would stand over it.
        /// </summary>
        protected void MudRing(float wide, GroundPaint paint = GroundPaint.Mud)
        {
            const int Steps = 24;
            float radius = Yard + wide * 0.5f - 0.5f;
            for (int i = 0; i < Steps; i++)
            {
                float a0 = i * MathF.Tau / Steps, a1 = (i + 1) * MathF.Tau / Steps;
                Vector2 p0 = Heart + new Vector2(MathF.Sin(a0), MathF.Cos(a0)) * radius, p1 = Heart + new Vector2(MathF.Sin(a1), MathF.Cos(a1)) * radius;
                Paint(paint, Footprint.Segment(W(p0), W(p1), wide * 0.5f), 2.5f);
            }
        }

        // ---------------------------------------------------------------- What a seat can't be without

        /// <summary>Whether there's land under a footprint that something could be built on: looked at where it is and at its corners, and never by its slope, as the seat's ground is laid level.</summary>
        protected bool Stands(in Footprint f)
        {
            bool At(Vector2 p) => World.InBounds(p) && World.Elevation.Sample(p) >= WorldPlan.SeaLevel + 0.4f && World.Reachable.Sample(p) >= 0.5f;
            if (!At(f.Centre)) return false;
            if (f.IsCircle)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (!At(f.Centre + SiteBuilder.Direction(i * MathF.PI * 0.5f) * f.HalfExtents.X)) return false;
                }
                return true;
            }
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    if (!At(f.ToWorld(new Vector2(sx * f.HalfExtents.X, sz * f.HalfExtents.Y)))) return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Raises one of the pieces a seat is built of, a wall, a tower, a hall: where it's told to, facing the way it's told, standing at the seat's level and <paramref name="lift"/> over
        /// it, wherever there's land under all that's solid about it (and no road through it, if <paramref name="offRoads"/>). What's solid about it is its rule's (Hitboxes): each
        /// part is in the way and keeps anything else from being put down on it, but isn't building, as a wall's end stands inside the tower it meets and two buildings may not touch.
        /// What the chart draws of it is said apart (<see cref="Chart"/>).
        /// </summary>
        protected bool Raise(in PlaceSpec spec, Vector2 local, Vector2 facing, float lift = 0f, bool offRoads = false)
        {
            Vector2 at = W(local);
            float yaw = YawOf(facing);
            HitKind kind = Hitboxes.Of(spec, _hit);
            if (kind == HitKind.Solid)
            {
                foreach (HitPart part in _hit)
                {
                    Footprint f = part.At(at, yaw);
                    if (!Stands(f) || (offRoads && World.Placement.Overlaps(f, PlacementLayer.Road, 0.2f))) return false;
                }
                foreach (HitPart part in _hit) World.Placement.Add(part.At(at, yaw), PlacementLayer.Keepout, solid: true, height: part.Height);
            }
            else if (!World.InBounds(at)) return false;
            World.Pending.Add(new PendingObject(spec.Id, at, yaw, Vector3.One, Level + lift));
            return true;
        }

        /// <summary>
        /// What the chart draws of something raised: a building's footprint, in the seat's own frame, which may touch no other and no road, so it's a little inside what it's of.
        /// </summary>
        protected void Chart(Vector2 local, Vector2 half, Vector2 facing, float height) => World.Placement.Add(Footprint.Box(W(local), half, YawOf(facing)), PlacementLayer.Structure, solid: true, height: height);

        /// <summary>The same for something round.</summary>
        protected void Chart(Vector2 local, float radius, float height) => World.Placement.Add(Footprint.Circle(W(local), radius), PlacementLayer.Structure, solid: true, height: height);

        /// <summary>Something that lies in the yard or on a way, with nothing solid about it and nothing kept off it: drawn where it's told, at the seat's level.</summary>
        protected void LayDown(in PlaceSpec spec, Vector2 local, float yaw, float lift = 0f)
        {
            Vector2 at = W(local);
            if (World.InBounds(at)) World.Pending.Add(new PendingObject(spec.Id, at, yaw, Vector3.One, Level + lift));
        }

        private uint _wallSeed;

        /// <summary>
        /// A run of curtain wall between two points of the frame, in stretches of about twelve metres, each with its buttress, its outside away from where the tyrant waits. Every
        /// stretch of a length is the one model. <paramref name="breach"/> is how far along the run, 0 to 1, the stretch is that was broken through and closed in the Reich's iron, if
        /// any was.
        /// </summary>
        protected void Curtain(Vector2 from, Vector2 to, float height, float thick, (int R, int G, int B) stone, int form = Fortress.WallWalk, float breach = -1f)
        {
            Vector2 d = to - from;
            float length = d.Length;
            if (length < 3f) return;
            d /= length;
            if (_wallSeed == 0) _wallSeed = NextSeed() | 1u;
            int count = Math.Max(1, (int)MathF.Round(length / 12f));
            float each = length / count;
            Vector2 inward = new Vector2(-d.Y, d.X);
            if (Vector2.Dot(Heart - (from + to) * 0.5f, inward) < 0f) inward = -inward;
            int breached = breach < 0f ? -1 : Math.Min((int)(breach * count), count - 1);
            for (int i = 0; i < count; i++)
            {
                bool broken = i == breached && form == Fortress.WallWalk;
                var spec = new PlaceSpec(PlacePiece.SeatWall, broken ? _wallSeed + 7u : _wallSeed, PlaceSpec.Cm(each), PlaceSpec.Cm(height), PlaceSpec.Cm(thick), broken ? Fortress.WallBreached : form, stone.R, stone.G, stone.B);
                Vector2 middle = from + d * ((i + 0.5f) * each);
                if (!Raise(spec, middle, inward, offRoads: true)) continue;
                // On the chart each stretch stops short of the run's ends, where a tower stands, and runs on into the next.
                float back = i == 0 ? 1f : 0f, ahead = i == count - 1 ? 1f : 0f;
                Chart(middle + d * ((back - ahead) * 0.5f), new Vector2(MathF.Max(each * 0.5f - (back + ahead) * 0.5f - 0.02f, 0.5f), thick * 0.5f), inward, height);
            }
        }

        /// <summary>A round tower at a point of the frame, whatever's on its head turned to where the tyrant waits.</summary>
        protected bool Tower(Vector2 local, float radius, float height, int form, (int R, int G, int B) stone, uint seed)
        {
            Vector2 toHeart = Heart - local;
            if (!Raise(new PlaceSpec(PlacePiece.SeatTower, seed, PlaceSpec.Cm(radius), PlaceSpec.Cm(height), form, stone.R, stone.G, stone.B), local, toHeart.LengthSquared > 1f ? toHeart.Normalized() : Vector2.UnitY, offRoads: true)) return false;
            Chart(local, radius, height);
            return true;
        }

        /// <summary>
        /// A gatehouse in a side of the frame, the great one if it's the road's: its way through kept clear inside and out. Returns how far either side of its middle it reaches along the
        /// wall, which is where the wall's runs stop.
        /// </summary>
        protected float Gatehouse(Side side, (int R, int G, int B) stone, float height, float lesser = 0.86f)
        {
            bool great = side == MainSide;
            float way = great ? 7.6f : 6.4f, radius = great ? 3.6f : 3.1f;
            Vector2 at = GateAt(side), inward = Inward(side);
            float high = great ? height : height * lesser, offset = Fortress.GateTowerOffset(way, radius);
            if (Raise(new PlaceSpec(PlacePiece.SeatGate, NextSeed(), PlaceSpec.Cm(way), PlaceSpec.Cm(radius), PlaceSpec.Cm(high), great ? Fortress.GateGreat : Fortress.GateLesser, stone.R, stone.G, stone.B), at, inward))
            {
                Vector2 across = new Vector2(-inward.Y, inward.X);
                foreach (float s in new[] { -1f, 1f }) Chart(at + across * (s * offset) - inward * 0.6f, radius, high);
            }
            Keep(Footprint.Box(W(at), new Vector2(way * 0.5f, 10f), YawOf(inward)));
            return offset + radius + 0.2f;
        }

        /// <summary>
        /// An open gate in a side of the frame: two towers with nothing over the way between them but an arch of iron, its leaves standing open. It's what a seat's front has, as a
        /// gatehouse there would stand between the camera and whoever walks in under it. Returns how far either side of its middle it reaches along the wall.
        /// </summary>
        protected float OpenGate(Side side, (int R, int G, int B) stone, float height)
        {
            const float Way = 7.6f, Radius = 3f;
            Vector2 at = GateAt(side), inward = Inward(side);
            float offset = Fortress.GateTowerOffset(Way, Radius);
            if (Raise(new PlaceSpec(PlacePiece.SeatGate, NextSeed(), PlaceSpec.Cm(Way), PlaceSpec.Cm(Radius), PlaceSpec.Cm(height), Fortress.GateOpen, stone.R, stone.G, stone.B), at, inward))
            {
                Vector2 across = new Vector2(-inward.Y, inward.X);
                foreach (float s in new[] { -1f, 1f }) Chart(at + across * (s * offset), Radius, height);
            }
            Keep(Footprint.Box(W(at), new Vector2(Way * 0.5f, 10f), YawOf(inward)));
            return offset + Radius + 0.2f;
        }

        /// <summary>
        /// The Reich's iron fence round the whole of a seat, a gap at each way in between two piers of brick with a lantern on each, and the way in from each laid in
        /// <paramref name="way"/> as far as the yard.
        /// </summary>
        protected void IronFence(float gateHalf, GroundPaint way)
        {
            Vector2 inside = W(Heart);
            B.Wall(FenceUse.Pen, W(new Vector2(-Hx, -Back)), W(new Vector2(Hx, -Back)), inside);
            foreach (float s in new[] { -1f, 1f })
            {
                B.Wall(FenceUse.Pen, W(new Vector2(s * Hx, -Back)), W(new Vector2(s * Hx, Heart.Y - gateHalf - 1.2f)), inside);
                B.Wall(FenceUse.Pen, W(new Vector2(s * Hx, Heart.Y + gateHalf + 1.2f)), W(new Vector2(s * Hx, Front)), inside);
                B.Wall(FenceUse.Pen, W(new Vector2(s * Hx, Front)), W(new Vector2(s * (gateHalf + 1.2f), Front)), inside);
            }
            uint pier = NextSeed();
            foreach (Side side in GateSides)
            {
                Vector2 at = GateAt(side), outward = Outward(side), across = new Vector2(-outward.Y, outward.X);
                bool great = side == MainSide;
                foreach (float s in new[] { -1f, 1f })
                {
                    // The fence ends inside its pier, so the pier minds everything but the fence.
                    Vector2 world = W(at + across * (s * (gateHalf + 1.2f)));
                    float high = great ? 6.4f : 5.2f;
                    Put(new PlaceSpec(PlacePiece.GatePier, great ? pier : pier + 1u, PlaceSpec.Cm(high), 0, 1), world, YawOf(outward), Footprint.Box(world, new Vector2(1f, 1f), YawOf(outward)), high, PlacementLayer.Prop, 0.6f, 0.1f, PlacementLayer.Structure | PlacementLayer.Prop | PlacementLayer.Road);
                }
                Keep(Footprint.Box(W(at), new Vector2(gateHalf - 1f, 9f), YawOf(Inward(side))));
                Trail(at + outward * 3f, Heart + (at - Heart).Normalized() * (Yard + 2f), great ? 6.5f : 5f, way, 1.6f);
            }
        }

        // ---------------------------------------------------------------- What stands about it

        /// <summary>Whether a point of the frame is on the way from one of the gates in to where the tyrant waits, which nothing's put down on.</summary>
        protected bool OnWayIn(Vector2 local, float clear = 5.5f)
        {
            foreach (Side side in GateSides)
            {
                Vector2 a = GateAt(side), span = Heart - a;
                float t = Math.Clamp(Vector2.Dot(local - a, span) / span.LengthSquared, 0f, 1f);
                if ((a + span * t - local).Length < clear) return true;
            }
            return false;
        }

        /// <summary>
        /// The great fires round the yard, just outside it, so that it's lit like a stage: evenly round it, but for the ways in and wherever <paramref name="clear"/> says something else
        /// stands. All of them are the one model.
        /// </summary>
        protected int Fires(int whose, int count, float height = 3.2f, Func<Vector2, bool>? clear = null)
        {
            uint seed = NextSeed();
            int lit = 0;
            for (int i = 0; i < count; i++)
            {
                float a = (i + 0.5f) * MathF.Tau / count;
                Vector2 local = Heart + new Vector2(MathF.Sin(a), MathF.Cos(a)) * (Yard + 1.7f);
                if (OnWayIn(local) || (clear != null && !clear(local))) continue;
                if (PutRound(new PlaceSpec(PlacePiece.GreatBrazier, seed, PlaceSpec.Cm(height), whose), W(local), 0f, 1.3f, height, 0.15f, 0.6f)) lit++;
            }
            return lit;
        }

        /// <summary>One of the Reich's standards: a long banner on a mast of its own, turned to face along <paramref name="facing"/>.</summary>
        protected bool Standard(Vector2 local, Vector2 facing, float height, float width, float drop, uint seed)
        {
            return PutRound(new PlaceSpec(PlacePiece.LongBanner, seed, PlaceSpec.Cm(height), PlaceSpec.Cm(width), PlaceSpec.Cm(drop), 0), W(local), YawOf(facing), 0.6f, 0f, 0.15f, 0.6f);
        }

        /// <summary>The Reich's standards either side of a way in, outside it, facing whoever comes: both or neither.</summary>
        protected void GateStandards(Side side, float apart, float height = 9.5f, float before = 7.5f)
        {
            Vector2 at = GateAt(side), outward = Outward(side), across = new Vector2(-outward.Y, outward.X);
            uint seed = NextSeed();
            Vector2 a = at + outward * before + across * apart, c = at + outward * before - across * apart;
            if (!CanStand(W(a)) || !CanStand(W(c))) return;
            Standard(a, outward, height, 2f, height * 0.58f, seed);
            Standard(c, outward, height, 2f, height * 0.58f, seed);
        }

        /// <summary>
        /// Where the garrison lives, about a point of the frame, facing <paramref name="toward"/>: a row of its tents, and before them its table, a brazier, a rack of its arms, its wood
        /// and what it drinks (Camp.cs, PLACES.md).
        /// </summary>
        protected void Camp(Vector2 local, Vector2 toward, WorldRandom r, int tents = 3)
        {
            Vector2 f = toward.Normalized(), side = new Vector2(-f.Y, f.X);
            Paint(GroundPaint.Yard, Footprint.Circle(W(local + f * 3.5f), 10f), 5f);
            for (int k = 0; k < tents; k++)
            {
                float length = r.Range(5f, 6.4f);
                Vector2 at = local + side * ((k - (tents - 1) * 0.5f) * 5.4f) - f * r.Range(0f, 1.2f);
                PutBox(new PlaceSpec(PlacePiece.Tent, NextSeed(), PlaceSpec.Cm(length), 1), W(at), YawOf(f) + r.Range(-0.12f, 0.12f), new Vector2(2.3f, length * 0.5f + 0.2f), 3.3f, 0.2f, 0.6f);
            }
            Vector2 mess = local + f * 6.6f;
            Tries(6, () => PutBox(new PlaceSpec(PlacePiece.MessTable, NextSeed(), PlaceSpec.Cm(r.Range(2.6f, 3.4f))), W(mess + side * r.Range(-1.5f, 1.5f)), YawOf(f), new Vector2(2.2f, 1.3f), 1.2f, 0.3f, 0.6f));
            Tries(6, () => PutRound(new PlaceSpec(PlacePiece.Brazier, NextSeed(), PlaceSpec.Cm(r.Range(1f, 1.2f))), W(mess + side * r.Range(3.4f, 4.4f) + f * r.Range(-1f, 1f)), 0f, 0.8f, 1.4f, 0.3f, 0.6f));
            Tries(6, () => PutBox(new PlaceSpec(PlacePiece.WeaponRack, NextSeed(), PlaceSpec.Cm(r.Range(2.2f, 2.8f)), 1), W(mess - side * r.Range(4f, 5f) + f * r.Range(-1f, 0.5f)), YawOf(f), new Vector2(1.6f, 0.6f), 2f, 0.3f, 0.6f));
            Tries(6, () => PutBox(new PlaceSpec(PlacePiece.Firewood, NextSeed(), PlaceSpec.Cm(r.Range(2.2f, 3f)), PlaceSpec.Cm(r.Range(1.2f, 1.5f))), W(local - side * ((tents + 1) * 2.7f + r.Range(0f, 1f))), YawOf(f), new Vector2(1.7f, 0.7f), 1.4f, 0.3f, 0.6f));
            Tries(6, () => PutBox(new PlaceSpec(PlacePiece.Barrels, NextSeed(), r.Int(2, 4)), W(local + side * ((tents + 1) * 2.7f + r.Range(0f, 1f))), r.Float() * MathF.Tau, new Vector2(1.5f), 1.4f, 0.3f, 0.6f));
        }

        /// <summary>
        /// Who holds it: a few at the great gate, and the rest round the yard, between it and the walls, never in it. The pack stage brings them up to a garrison
        /// (<c>PackStage.Reinforce</c>).
        /// </summary>
        protected void Garrison(SpawnRole guards, SpawnRole rest, int count, WorldRandom r)
        {
            Vector2 inward = Inward(MainSide);
            Vector2 heart = W(Heart);
            Stand(guards, Around(W(GateAt(MainSide) + inward * 6.5f), 5f, r.Fork("gate"), 24).Where(spot => (spot - heart).Length > Yard + 1.5f), 3, 3.5f, r.Fork("guards"));
            var spots = new List<Vector2>();
            WorldRandom ring = r.Fork("ring");
            for (int i = 0; i < 90; i++)
            {
                float a = ring.Float() * MathF.Tau;
                Vector2 local = Heart + new Vector2(MathF.Sin(a), MathF.Cos(a)) * ring.Range(Yard + 3.5f, Yard + 14f);
                if (MathF.Abs(local.X) > Hx - 4f || local.Y < -Back + 4f || local.Y > Front - 4f) continue;
                spots.Add(W(local));
            }
            Stand(rest, spots, count, 7f, r.Fork("stood"));
        }

        /// <summary>What the commander's taken of the land heaped at a point of the frame, and the chest of it beside it, which is the land's hoard.</summary>
        protected void HoardAt(Vector2 local, WorldRandom r)
        {
            PutBox(new PlaceSpec(PlacePiece.Hoard, NextSeed(), PlaceSpec.Cm(r.Range(4f, 5f))), W(local), YawOf(Vector2.UnitY) + r.Range(-0.3f, 0.3f), new Vector2(2.6f, 2.2f), 1.2f, 0.2f, 0.6f);
            Chest(W(local + new Vector2(r.Chance(0.5f) ? 4.4f : -4.4f, 1.2f)), 9f);
        }

        /// <summary>
        /// What a tyrant leaves of a yard: the ring it was chained by in the middle, the chain run out to where it broke, bones about, blood where it's fed, and its trough at the yard's
        /// edge towards <paramref name="trough"/>. None of it stands as high as a knee.
        /// </summary>
        protected void TyrantsLeavings(WorldRandom r, Vector2 trough)
        {
            LayDown(new PlaceSpec(PlacePiece.YardRing, NextSeed(), PlaceSpec.Cm(r.Range(6f, 9f))), Heart + new Vector2(r.Range(-2f, 2f), r.Range(-4f, -2f)), Plan.Yaw + r.Range(-0.6f, 0.6f));
            Vector2 at = Heart + trough.Normalized() * (Yard - 3.2f);
            LayDown(new PlaceSpec(PlacePiece.FeedTrough, NextSeed(), PlaceSpec.Cm(r.Range(5f, 6.5f))), at, YawOf(new Vector2(-trough.Y, trough.X)));
            for (int k = 0; k < 3; k++)
            {
                float a = r.Float() * MathF.Tau;
                Vector2 kill = Heart + new Vector2(MathF.Sin(a), MathF.Cos(a)) * r.Range(Yard * 0.35f, Yard * 0.8f);
                if (k == 0) LayDown(new PlaceSpec(PlacePiece.Carcass, NextSeed(), 0, PlaceSpec.Cm(r.Range(5f, 7f))), kill, r.Float() * MathF.Tau);
                Strew(LitterKind.Bones, kill, 3.4f, r.Int(10, 16));
                Strew(LitterKind.Blood, kill + new Vector2(r.Range(-2f, 2f), r.Range(-2f, 2f)), 3f, r.Int(5, 9));
            }
            Strew(LitterKind.Blood, at - trough.Normalized() * 2.5f, 3.2f, r.Int(6, 10));
        }
    }
}

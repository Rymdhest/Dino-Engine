using Dino_Engine.Modelling.Procedural.Architecture.Churchyard;
using Dino_Engine.Modelling.Procedural.Fence;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.World.Generation
{
    /// <summary>
    /// Lays out a churchyard (CHURCHYARD.md). A church stands in its middle, turned so that its south side and its porch face the camera, inside a wall with its corners cut off
    /// and two to four gates in it, one where the road comes and one in front of the porch. Gravel paths run from each gate in to the church and round it, and on out to the wall,
    /// cutting the ground into plots, and each plot tells something: rows of the oldest stones nearest the church, newer ones further out with candles still left on them, the
    /// poor's wooden crosses on the north side nobody wanted, a family's railed plot with its obelisk, chest tombs, a mausoleum against the wall, an old tree, and graves the
    /// Ossuary has dug open. Lanterns light the paths, and the dead walk them. Nothing is stored: everything comes from the site's own fork, a fork of its own for each decision.
    /// </summary>
    internal sealed class ChurchyardBuilder
    {
        private enum Side { South, North, East, West }
        private enum Era { Old, Middle, New, North }
        private enum Story { Rows, Family, Tombs, Mausoleum, Dug, Tree }

        private readonly record struct Rect(float U0, float V0, float U1, float V1)
        {
            public float Width => U1 - U0;
            public float Depth => V1 - V0;
            public Vector2 Middle => new Vector2((U0 + U1) * 0.5f, (V0 + V1) * 0.5f);
            public Vector2 Half => new Vector2(Width * 0.5f, Depth * 0.5f);
        }

        private sealed class Cell
        {
            public Rect Area;
            public float Age;
            public Era Era;
            public Story Story = Story.Rows;
            public Side Edge;           // Which side of the yard it's against, if it's against the wall at all.
            public bool Outer;
            public bool South;          // In the band south of the church, which is what the camera sees most of.
        }

        private readonly record struct Gate(Side Side, float At, GateForm Form, float Width, bool Main);

        private const float Margin = 3.1f;          // Metres from the wall's line to where the plots start: a walk all round inside the wall.
        private const float Apron = 3.2f;           // Gravel all round the church.
        private const float PathWidth = 2.6f;       // A character is 2.2 m across.
        private const float GatePath = 3.6f;
        private const float Lane = 2.6f;            // Clear ground between one pair of rows and the next, so the dead can rise where someone could reach them.
        private const float BackToBack = 0.7f;      // Between the two rows of a pair, each grave's foot to the next one's stone.
        private const float GraveSlope = 0.12f;     // Steepest ground a kerb or a slab is laid on: a model only turns about y, so on more it would float at one end.
        private const float Tuck = 1.2f;            // Metres a wall is drawn on into a gate's pier.
        private const int MostCandles = 16;        // A candle's light is small and cheap, and it's what a stone is seen by at night.
        private const int MostLamps = 12;

        private readonly SiteBuilder _b;
        private readonly WorldData _world;
        private readonly WorldSite _site;
        private readonly YardPlan _plan;
        private readonly WorldRandom _random;
        private readonly List<Gate> _gates = new();
        private readonly List<Cell> _cells = new();
        private readonly List<Vector2> _spots = new();         // Where the dead could stand: paths and the lanes between rows.
        private readonly List<Vector2> _candles = new();       // The feet of graves someone still visits.
        private readonly List<Vector2> _trees = new();
        private readonly List<Vector2> _crossings = new();      // Where paths meet among the plots, in the yard's frame.
        private int _shade = 1000, _warmth = 500;
        private ChurchDesign? _church;
        private Vector2 _churchAt;                             // Its middle, in the yard's frame.
        private Rect _zone;                                    // The church and its apron, in the yard's frame.
        private float _uMin, _uMax, _vMin, _vMax;
        private int _lamps;
        private LampForm _lamp;
        private uint _seed;
        private readonly uint _seed0;

        private ChurchyardBuilder(SiteBuilder b)
        {
            _b = b;
            _world = b.World;
            _site = b.Site;
            _random = b.Random.Fork("churchyard");
            _plan = _site.Yard ??= PlanOf(_world, _site, b.Random);
            _seed = (uint)_random.Fork("pieces").NextULong();
            _seed0 = _seed;
        }

        /// <summary>
        /// The shape of a site's churchyard, from its builder's fork, which the settlement stage works out before roads are reserved so that a road ending there stops at its wall.
        /// </summary>
        public static YardPlan PlanOf(WorldData world, WorldSite site, WorldRandom random)
        {
            WorldRandom r = random.Fork("yard frame");
            float twist = r.Range(-0.15f, 0.15f);
            var plan = new YardPlan
            {
                Centre = site.Position,
                // Turned so that the church's south side, its +z, faces south, which the camera looks at.
                Yaw = MathF.PI + twist,
            };
            float hx = site.Radius * r.Range(0.9f, 1f), hz = site.Radius * r.Range(0.72f, 0.84f);

            // Each side comes in from where it was wanted until the ground under the wall and the plots inside it can be built on and walked, as nobody walls in a cliff or a bog,
            // and until it's clear of any road that passes by, as a road along a wall would take the wall. A road that ends here stops at the wall, so it's no reason to come in.
            float u0 = -hx, u1 = hx, v0 = -hz, v1 = hz;
            var passing = new List<(Vector2 A, Vector2 B, float Half)>();
            foreach (WorldRoad road in world.Roads)
            {
                if ((road.Points[0].Xz - site.Position).Length < site.Radius || (road.Points[^1].Xz - site.Position).Length < site.Radius) continue;
                for (int i = 1; i < road.Points.Count; i++)
                {
                    Vector2 a = road.Points[i - 1].Xz, b = road.Points[i].Xz;
                    if ((a - site.Position).Length < site.Radius * 1.8f) passing.Add((a, b, road.HalfWidth + 3f));
                }
            }
            bool Bad(float u, float v)
            {
                Vector2 p = plan.ToWorld(u, v);
                if (!world.InBounds(p) || world.Elevation.Sample(p) < WorldPlan.SeaLevel + 0.6f || world.Elevation.SlopeAt(p) > 0.4f || world.Reachable.Sample(p) < 0.5f) return true;
                foreach ((Vector2 a, Vector2 b, float half) in passing)
                {
                    Vector2 ab = b - a;
                    float t = Math.Clamp(Vector2.Dot(p - a, ab) / MathF.Max(ab.LengthSquared, 1e-4f), 0f, 1f);
                    if ((p - (a + ab * t)).LengthSquared < half * half) return true;
                }
                return false;
            }
            float Share(float fixedAt, float from, float to, bool alongU, float inward)
            {
                int bad = 0, all = 0;
                for (float t = from; t <= to; t += 3f)
                {
                    for (float d = 0f; d <= 9f; d += 3f)
                    {
                        all++;
                        if (alongU ? Bad(t, fixedAt + inward * d) : Bad(fixedAt + inward * d, t)) bad++;
                    }
                }
                return all == 0 ? 0f : bad / (float)all;
            }
            // First it slides a little way off whatever's bad, keeping its size, and only then do its sides come in.
            Vector2 best = Vector2.Zero;
            float bestScore = float.MaxValue;
            for (float dx = -12f; dx <= 12f; dx += 3f)
            {
                for (float dz = -12f; dz <= 12f; dz += 3f)
                {
                    plan.Centre = site.Position + plan.Ax * dx + plan.Az * dz;
                    int bad = 0;
                    for (float u = -hx; u <= hx; u += 3f)
                    {
                        for (float v = -hz; v <= hz; v += 3f) if (Bad(u, v)) bad++;
                    }
                    float score = bad + 0.004f * (dx * dx + dz * dz);
                    if (score < bestScore) { bestScore = score; best = new Vector2(dx, dz); }
                }
            }
            plan.Centre = site.Position + plan.Ax * best.X + plan.Az * best.Y;

            const float Step = 1.5f, Worst = 0.12f;
            for (int pass = 0; pass < 40; pass++)
            {
                bool moved = false;
                if (u1 - u0 > MinLength && Share(u0, v0, v1, false, 1f) > Worst) { u0 += Step; moved = true; }
                if (u1 - u0 > MinLength && Share(u1, v0, v1, false, -1f) > Worst) { u1 -= Step; moved = true; }
                if (v1 - v0 > MinDepth && Share(v0, u0, u1, true, 1f) > Worst) { v0 += Step; moved = true; }
                if (v1 - v0 > MinDepth && Share(v1, u0, u1, true, -1f) > Worst) { v1 -= Step; moved = true; }
                if (!moved) break;
            }
            plan.Centre = plan.ToWorld((u0 + u1) * 0.5f, (v0 + v1) * 0.5f);
            plan.HalfX = (u1 - u0) * 0.5f;
            plan.HalfZ = (v1 - v0) * 0.5f;
            plan.Chamfer = MathF.Min(plan.HalfX, plan.HalfZ) * r.Range(0.16f, 0.36f);
            return plan;
        }

        // The least a churchyard's wall is pulled in to: room for a church and graves either side of it.
        private const float MinLength = 58f;
        private const float MinDepth = 42f;

        /// <summary>A road that goes no further than the churchyard stops just outside its wall, where a gate will be.</summary>
        public static void EndRoadsAtWall(WorldData world, YardPlan plan)
        {
            foreach (WorldRoad road in world.Roads)
            {
                List<Vector3> points = road.Points;
                while (points.Count > 2 && plan.Inside(points[^1].Xz, 2.5f)) points.RemoveAt(points.Count - 1);
                while (points.Count > 2 && plan.Inside(points[0].Xz, 2.5f)) points.RemoveAt(0);
            }
        }

        public static void Build(SiteBuilder b) => new ChurchyardBuilder(b).Lay();

        private uint NextSeed()
        {
            _seed = _seed * 0x9E3779B1u + 0x7F4A7C15u;
            return _seed >> 4;
        }

        private Vector2 W(float u, float v) => _plan.ToWorld(u, v);
        private Vector2 W(Vector2 local) => _plan.ToWorld(local);
        private float Yaw => _plan.Yaw;

        private void Lay()
        {
            StoneOf();
            _lamp = _random.Fork("lamp").Float() switch { < 0.5f => LampForm.Crook, < 0.8f => LampForm.Pillar, _ => LampForm.Hook };
            RaiseChurch();
            ChooseGates();
            PutWalls();
            LayPaths();
            TellStories();
            foreach (Cell cell in _cells) Fill(cell);
            RaiseCross();
            Light();
            Plant();
            Furnish();
            Haunt();
            _b.Paint(GroundPaint.Sward, Footprint.Box(_plan.Centre, new Vector2(_plan.HalfX, _plan.HalfZ), Yaw), 3f);
        }

        // The churchyard's stone is its wall's, so the church, its gates and its cross are of a piece with it.
        private void StoneOf()
        {
            (FenceKind kind, ulong style) = _b.FenceFor(FenceUse.Churchyard);
            if (kind != FenceCatalog.Churchyard) return;
            (float shade, float warmth) = StoneFences.ChurchyardStone(style);
            _shade = (int)MathF.Round(shade * 1000f);
            _warmth = (int)MathF.Round(warmth * 1000f);
        }

        // ---------------------------------------------------------------- The church

        private const float EndBand = 11f;          // The least ground for graves at each end of the church.
        private const float NorthBand = 10.5f;      // And behind it, where the camera sees least.
        private const float SouthBand = 16f;        // And in front of it, where it sees most.

        // A church its yard has room for, so there are graves all round it, set a little north of the middle so the most of them are on the side the camera sees, and as near
        // there as the land allows. WorldValidator fails a churchyard with no church.
        private void RaiseChurch()
        {
            WorldRandom r = _random.Fork("church");
            float roomU = 2f * (_plan.HalfX - Margin) - 2f * (Apron + EndBand);
            float roomV = 2f * (_plan.HalfZ - Margin) - 2f * Apron - SouthBand - NorthBand;
            ChurchDesign? design = null;
            float least = float.MaxValue;
            for (int i = 0; i < 48; i++)
            {
                var tried = ChurchDesign.Of((uint)r.NextULong());
                (Vector2 lo, Vector2 hi) = tried.Bounds;
                float over = MathF.Max(hi.X - lo.X - roomU, hi.Y - lo.Y - roomV);
                if (over < least)
                {
                    least = over;
                    design = tried;
                }
                if (over <= 0f) break;
            }
            (Vector2 min, Vector2 max) = design!.Bounds;
            float spare = 2f * (_plan.HalfZ - Margin) - (max.Y - min.Y) - 2f * Apron;
            // Behind it a share of what's spare, as the camera sees least of that side, and in a tight yard only the walk round the wall.
            float north = spare >= NorthBand + SouthBand ? MathF.Max(NorthBand, spare * r.Range(0.34f, 0.42f)) : MathF.Max(0f, spare * r.Range(0.2f, 0.3f));
            Vector2 wanted = new Vector2(-(min.X + max.X) * 0.5f + r.Range(-1.5f, 1.5f), -_plan.HalfZ + Margin + north + Apron - min.Y);
            for (float distance = 0f; distance <= _site.Radius * 0.3f; distance += 2f)
            {
                int directions = distance > 0f ? 8 : 1;
                for (int i = 0; i < directions; i++)
                {
                    Vector2 at = wanted + SiteBuilder.Direction(i * MathF.Tau / 8f) * distance;
                    // It has to leave room for its apron and graves inside the walls.
                    if (at.X + min.X - Apron < -_plan.HalfX + Margin + 4f || at.X + max.X + Apron > _plan.HalfX - Margin - 4f) continue;
                    if (at.Y + min.Y - Apron < -_plan.HalfZ + Margin + 4f || at.Y + max.Y + Apron > _plan.HalfZ - Margin - 8f) continue;
                    if (!Fits(design, at)) continue;
                    Commit(design, at);
                    return;
                }
            }
            // A yard too tight for graves all round, or with bog or rock in it, still has its church: anywhere inside the walk round its wall.
            for (float distance = 0f; distance <= MathF.Max(_plan.HalfX, _plan.HalfZ); distance += 1.5f)
            {
                int directions = distance > 0f ? Math.Max(12, (int)(distance * 1.2f)) : 1;
                for (int i = 0; i < directions; i++)
                {
                    Vector2 at = wanted + SiteBuilder.Direction(i * MathF.Tau / directions) * distance;
                    if (at.X + min.X - Apron < -_plan.HalfX + Margin || at.X + max.X + Apron > _plan.HalfX - Margin) continue;
                    if (at.Y + min.Y - Apron < -_plan.HalfZ + Margin || at.Y + max.Y + Apron > _plan.HalfZ - Margin) continue;
                    if (!Fits(design, at)) continue;
                    Commit(design, at);
                    return;
                }
            }
            // No church: lay the rest round the middle all the same, and WorldValidator says so.
            _churchAt = wanted;
            _zone = new Rect(wanted.X - 8f, wanted.Y - 6f, wanted.X + 8f, wanted.Y + 6f);
        }

        private IEnumerable<Footprint> PartsOf(ChurchDesign design, Vector2 at, float grow)
        {
            foreach ((Vector2 centre, Vector2 half) in design.Parts()) yield return Footprint.Box(W(at + centre), half + new Vector2(grow), Yaw);
        }

        // Whether the land under a footprint can carry something, looked at all over it rather than round the circle that holds it, which for a long nave reaches far past its sides.
        // `soft` is the share of it that may be over ground nobody walks to, a bog's soft patches, as what's levelled for a church doesn't care; water and slopes never are.
        private bool Ground(in Footprint f, float maxSlope, float soft = 0f)
        {
            if (f.IsCircle) return _b.GroundFits(f, maxSlope);
            int nx = Math.Max(1, (int)MathF.Ceiling(f.HalfExtents.X / 2.5f)), nz = Math.Max(1, (int)MathF.Ceiling(f.HalfExtents.Y / 2.5f));
            int unreached = 0, all = 0;
            for (int i = -nx; i <= nx; i++)
            {
                for (int j = -nz; j <= nz; j++)
                {
                    Vector2 p = f.ToWorld(new Vector2(f.HalfExtents.X * i / nx, f.HalfExtents.Y * j / nz));
                    if (!_world.InBounds(p) || _world.Elevation.Sample(p) < WorldPlan.SeaLevel + 0.4f) return false;
                    if (_world.Elevation.SlopeAt(p) > maxSlope) return false;
                    all++;
                    if (_world.Reachable.Sample(p) < 0.5f) unreached++;
                }
            }
            return unreached <= soft * all;
        }

        private bool Fits(ChurchDesign design, Vector2 at)
        {
            foreach (Footprint part in PartsOf(design, at, ChurchDesign.Reach))
            {
                if (!Ground(part, 0.6f, 0.2f) || _world.Placement.Overlaps(part, PlacementLayer.Built | PlacementLayer.Road, 0.6f)) return false;
            }
            return true;
        }

        private void Commit(ChurchDesign design, Vector2 at)
        {
            // One floor for all of it, the ground's own height averaged over where it stands.
            float total = 0f;
            int samples = 0;
            foreach (Footprint part in PartsOf(design, at, 0f))
            {
                total += _world.Elevation.Sample(part.Centre);
                samples++;
                for (int k = 0; k < 4; k++)
                {
                    total += _world.Elevation.Sample(part.Centre + SiteBuilder.Direction(k * MathF.PI * 0.5f + 0.785f) * part.BoundingRadius * 0.8f);
                    samples++;
                }
            }
            float floor = total / samples;
            foreach (Footprint part in PartsOf(design, at, ChurchDesign.Reach)) _world.Pads.Add(new GroundPad(part.Grown(1f), floor, 5f));
            foreach ((Vector2 centre, Vector2 half) in design.Solids()) _world.Placement.Add(Footprint.Box(W(at + centre), half, Yaw), PlacementLayer.Structure, solid: true);
            var spec = new YardSpec(YardKind.Church, design.Seed, _shade, _warmth);
            _world.Pending.Add(new PendingObject(spec.Id, W(at), Yaw, Vector3.One, floor));
            _church = design;
            _churchAt = at;
            (Vector2 min, Vector2 max) = design.Bounds;
            _zone = new Rect(at.X + min.X - Apron, at.Y + min.Y - Apron, at.X + max.X + Apron, at.Y + max.Y + Apron);
            _plan.Church = W(at);
            _plan.Porch = W(at + design.PorchMouth);
        }

        // ---------------------------------------------------------------- Gates and walls

        private static Vector2 Outward(Side side) => side switch
        {
            Side.South => Vector2.UnitY,
            Side.North => -Vector2.UnitY,
            Side.East => Vector2.UnitX,
            _ => -Vector2.UnitX,
        };

        private Vector2 OnWall(Side side, float at) => side switch
        {
            Side.South => new Vector2(at, _plan.HalfZ),
            Side.North => new Vector2(at, -_plan.HalfZ),
            Side.East => new Vector2(_plan.HalfX, at),
            _ => new Vector2(-_plan.HalfX, at),
        };

        // How far along a side a gate can be from its middle and still clear the cut corners.
        private float Room(Side side, float reach) => (side is Side.South or Side.North ? _plan.HalfX : _plan.HalfZ) - _plan.Chamfer - reach - 1.5f;

        // One where the road comes, one in front of the porch, and now and then one more on another side, each where it leads straight in to the church.
        private void ChooseGates()
        {
            WorldRandom r = _random.Fork("gates");
            var sides = new List<Side>();
            Side main = MainSide(out float? roadAt);
            sides.Add(main);
            // The porch always has a gate in front of it, the way into the church: a second one on the south side if the road comes there too, but well along from it.
            float porch = _church != null ? _churchAt.X + _church.PorchX : 0f;
            if (main != Side.South || (roadAt.HasValue && MathF.Abs(roadAt.Value - porch) > 16f)) sides.Add(Side.South);
            foreach (Side side in new[] { Side.West, Side.East, Side.North })
            {
                if (sides.Contains(side)) continue;
                if (sides.Count < 2 || (sides.Count < 4 && r.Chance(0.42f))) sides.Add(side);
            }

            for (int k = 0; k < sides.Count; k++)
            {
                Side side = sides[k];
                bool isMain = k == 0;
                GateForm form = isMain && r.Chance(0.55f) ? GateForm.Lychgate : GateForm.Piers;
                float width = form == GateForm.Lychgate ? 3.8f : 4.4f;
                float reach = YardForge.GateReach(form, width).X;
                float at = side switch
                {
                    Side.South => _church != null ? _churchAt.X + _church.PorchX : 0f,
                    Side.North => _churchAt.X + r.Range(-0.25f, 0.25f) * _zone.Width * 0.5f,
                    _ => _churchAt.Y,
                };
                if (isMain && roadAt.HasValue) at = roadAt.Value;
                if (!isMain && side == Side.South) at = porch;
                float room = Room(side, reach);
                at = Math.Clamp(at, -room, room);
                // Where a road or the ground is in the way, a little further along.
                bool placed = false;
                foreach (float shift in new[] { 0f, 2.5f, -2.5f, 5f, -5f, 8f, -8f })
                {
                    float tried = Math.Clamp(at + shift, -room, room);
                    if (!PutGate(side, tried, form, width)) continue;
                    _gates.Add(new Gate(side, tried, form, width, isMain));
                    placed = true;
                    break;
                }
                if (!placed && form == GateForm.Lychgate && PutGate(side, at, GateForm.Piers, 4.4f)) _gates.Add(new Gate(side, at, GateForm.Piers, 4.4f, isMain));
            }

            // Two ways in at least: wherever along any side one will stand.
            foreach (Side side in new[] { Side.South, Side.West, Side.East, Side.North })
            {
                if (_gates.Count >= 2) break;
                float room = Room(side, YardForge.GateReach(GateForm.Piers, 4.4f).X);
                for (float at = 0f; MathF.Abs(at) <= room && _gates.Count < 2; at = at <= 0f ? -at + 2f : -at)
                {
                    if (_gates.Any(g => g.Side == side && MathF.Abs(g.At - at) < 14f)) continue;
                    if (!PutGate(side, at, GateForm.Piers, 4.4f)) continue;
                    _gates.Add(new Gate(side, at, GateForm.Piers, 4.4f, false));
                }
            }
        }

        // The side a road that stops at the wall ends at, and where along it; or else the side the site's road comes from.
        private Side MainSide(out float? at)
        {
            at = null;
            float nearest = float.MaxValue;
            Side best = Side.South;
            foreach (WorldRoad road in _world.Roads)
            {
                foreach (Vector3 end in new[] { road.Points[0], road.Points[^1] })
                {
                    if (_plan.Inside(end.Xz) || !_plan.Inside(end.Xz, 7f)) continue;
                    Vector2 local = _plan.ToLocal(end.Xz);
                    Side side = MathF.Abs(local.X) / _plan.HalfX > MathF.Abs(local.Y) / _plan.HalfZ ? (local.X > 0f ? Side.East : Side.West) : (local.Y > 0f ? Side.South : Side.North);
                    float away = (end.Xz - _plan.Centre).Length;
                    if (away >= nearest) continue;
                    nearest = away;
                    best = side;
                    at = side is Side.South or Side.North ? local.X : local.Y;
                }
            }
            if (at.HasValue) return best;
            Vector2 road2 = new Vector2(Vector2.Dot(_site.RoadDirection, _plan.Ax), Vector2.Dot(_site.RoadDirection, _plan.Az));
            if (MathF.Abs(road2.X) > MathF.Abs(road2.Y)) return road2.X > 0f ? Side.East : Side.West;
            return road2.Y > 0f ? Side.South : Side.North;
        }

        private bool PutGate(Side side, float at, GateForm form, float width)
        {
            Vector2 local = OnWall(side, at);
            Vector2 outward = _plan.Dir(Outward(side));
            Vector2 along = new Vector2(outward.Y, -outward.X);
            Vector2 centre = W(local);
            float yaw = SiteBuilder.YawFacing(outward);
            Vector2 reach = YardForge.GateReach(form, width);
            float side_ = YardForge.GateSide(form);
            var solids = new List<Footprint>();
            foreach (float s in new[] { -1f, 1f })
            {
                solids.Add(Footprint.Box(centre + along * (s * (reach.X - side_ * 0.5f)), new Vector2(side_ * 0.5f, reach.Y), yaw));
            }
            Footprint passage = Footprint.Box(centre, new Vector2(width * 0.5f, reach.Y + 3f), yaw);
            foreach (Footprint f in solids)
            {
                if (!Ground(f, 0.5f) || _world.Placement.Overlaps(f, PlacementLayer.Built | PlacementLayer.Road, 0.3f)) return false;
            }
            if (!Ground(passage, 0.5f) || _world.Placement.Overlaps(passage, PlacementLayer.Structure | PlacementLayer.Prop, 0.2f)) return false;
            foreach (Footprint f in solids) _world.Placement.Add(f, PlacementLayer.Structure, solid: true, height: form == GateForm.Lychgate ? 1.3f : PlacementIndex.Tall);
            _world.Placement.Add(passage, PlacementLayer.Keepout);
            var spec = new YardSpec(YardKind.Gate, NextSeed(), (int)form, YardSpec.Cm(width), _shade, _warmth);
            _world.Pending.Add(new PendingObject(spec.Id, centre, yaw, Vector3.One, null));
            _plan.Gates.Add((centre, outward));
            // Gravel up to it from outside, where whoever comes walks.
            _b.Paint(GroundPaint.Plaza, Footprint.Box(centre + outward * 2.5f, new Vector2(width * 0.5f + 0.4f, 2.6f), yaw), 1.2f);
            return true;
        }

        // The wall's eight corners, round the yard, with the side each run from one to the next is on (null across a cut corner).
        private (Vector2 From, Vector2 To, Side? Side)[] Runs()
        {
            float x = _plan.HalfX, z = _plan.HalfZ, c = _plan.Chamfer;
            Vector2[] p =
            {
                new(x, -z + c), new(x, z - c), new(x - c, z), new(-x + c, z), new(-x, z - c), new(-x, -z + c), new(-x + c, -z), new(x - c, -z),
            };
            Side?[] sides = { Side.East, null, Side.South, null, Side.West, null, Side.North, null };
            var runs = new (Vector2, Vector2, Side?)[8];
            for (int i = 0; i < 8; i++) runs[i] = (p[i], p[(i + 1) % 8], sides[i]);
            return runs;
        }

        private void PutWalls()
        {
            foreach ((Vector2 from, Vector2 to, Side? side) in Runs())
            {
                Vector2 along = (to - from).Normalized();
                float length = (to - from).Length;
                // The gates on this run, by how far along it each is.
                var breaks = new List<(float At, float Reach)>();
                foreach (Gate gate in _gates)
                {
                    if (gate.Side != side) continue;
                    float t = Vector2.Dot(OnWall(gate.Side, gate.At) - from, along);
                    breaks.Add((t, YardForge.GateReach(gate.Form, gate.Width).X + 0.5f));
                }
                breaks.Sort((a, b) => a.At.CompareTo(b.At));
                float start = 0f, tuckFrom = 0f;
                foreach ((float at, float reach) in breaks)
                {
                    PutWall(from + along * start, from + along * (at - reach), tuckFrom, Tuck);
                    start = at + reach;
                    tuckFrom = Tuck;
                }
                PutWall(from + along * start, to, tuckFrom, 0f);
            }
        }

        // A run of the wall, stopping short of any road that crosses it and going on beyond, as a road runs through a churchyard by a gap in its wall.
        private void PutWall(Vector2 localFrom, Vector2 localTo, float tuckFrom, float tuckTo)
        {
            Vector2 a = W(localFrom), c = W(localTo);
            float length = (c - a).Length;
            if (length < 1.5f) return;
            int steps = Math.Max(2, (int)MathF.Ceiling(length));
            float? clear = null;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector2 p = Vector2.Lerp(a, c, t);
                bool road = _world.Placement.Overlaps(Footprint.Circle(p, 1.2f), PlacementLayer.Road, 0.4f);
                if (!road && clear == null) clear = t;
                if (clear == null || (!road && i < steps)) continue;
                float end = road ? MathF.Max(clear.Value, (i - 1f) / steps) : t;
                if ((end - clear.Value) * length >= 1.5f)
                {
                    _b.Wall(FenceUse.Churchyard, Vector2.Lerp(a, c, clear.Value), Vector2.Lerp(a, c, end), _plan.Centre, clear.Value <= 0f ? tuckFrom : 0f, end >= 1f ? tuckTo : 0f);
                }
                clear = null;
            }
        }

        // ---------------------------------------------------------------- Paths and plots

        private void LayPaths()
        {
            _uMin = -_plan.HalfX + Margin;
            _uMax = _plan.HalfX - Margin;
            _vMin = -_plan.HalfZ + Margin;
            _vMax = _plan.HalfZ - Margin;
            float uW = _zone.U0, uE = _zone.U1, vN = _zone.V0, vS = _zone.V1;

            // The church's apron, gravel all round it.
            Path(_zone, 1.2f);

            // Its edges carried on out to the walk inside the wall, so every gate's path meets one.
            Path(new Rect(uW - PathWidth * 0.5f, _vMin, uW + PathWidth * 0.5f, _vMax));
            Path(new Rect(uE - PathWidth * 0.5f, _vMin, uE + PathWidth * 0.5f, _vMax));
            Path(new Rect(_uMin, vS - PathWidth * 0.5f, _uMax, vS + PathWidth * 0.5f));
            Path(new Rect(_uMin, vN - PathWidth * 0.5f, _uMax, vN + PathWidth * 0.5f));

            // From each gate straight in.
            foreach (Gate gate in _gates)
            {
                float w = gate.Main || gate.Side == Side.South ? GatePath : PathWidth;
                Rect path = gate.Side switch
                {
                    Side.South => new Rect(gate.At - w * 0.5f, vS, gate.At + w * 0.5f, _plan.HalfZ),
                    Side.North => new Rect(gate.At - w * 0.5f, -_plan.HalfZ, gate.At + w * 0.5f, vN),
                    Side.East => new Rect(uE, gate.At - w * 0.5f, _plan.HalfX, gate.At + w * 0.5f),
                    _ => new Rect(-_plan.HalfX, gate.At - w * 0.5f, uW, gate.At + w * 0.5f),
                };
                Path(path);
            }

            WorldRandom r = _random.Fork("plots");
            // A plot is two or three rows of graves deep and a dozen or more across, so that it reads as a plot and not as gravel.
            float width = r.Range(15f, 19f), depth = r.Range(14.5f, 18.5f);
            float[] GatesOn(Side side) => _gates.Where(g => g.Side == side).Select(g => g.At).ToArray();

            // The band south of the church, which the camera sees most of, the band north of it, and the blocks either end of it.
            Region(new Rect(_uMin, vS, _uMax, _vMax), GatesOn(Side.South).Append(uW).Append(uE).ToArray(), Array.Empty<float>(), width, depth, south: true);
            Region(new Rect(_uMin, _vMin, _uMax, vN), GatesOn(Side.North).Append(uW).Append(uE).ToArray(), Array.Empty<float>(), width, depth, south: false);
            Region(new Rect(_uMin, vN, uW, vS), Array.Empty<float>(), GatesOn(Side.West), width, depth, south: false);
            Region(new Rect(uE, vN, _uMax, vS), Array.Empty<float>(), GatesOn(Side.East), width, depth, south: false);
        }

        // A stretch of path: gravel, kept clear of anything built, and somewhere the dead can stand; laid a few metres at a time, and only where the ground can be walked, as
        // nobody gravels a slope nobody can climb.
        private void Path(Rect r, float softness = 1f)
        {
            if (r.Width < 0.5f || r.Depth < 0.5f) return;
            bool along = r.Width > r.Depth;
            float length = along ? r.Width : r.Depth;
            int pieces = Math.Max(1, (int)MathF.Ceiling(length / 4f));
            for (int k = 0; k < pieces; k++)
            {
                float a = length * k / pieces, c = length * (k + 1) / pieces;
                Rect piece = along ? r with { U0 = r.U0 + a, U1 = r.U0 + c } : r with { V0 = r.V0 + a, V1 = r.V0 + c };
                Vector2 middle = W(piece.Middle);
                if (_world.Elevation.SlopeAt(middle) > 0.45f || _world.Reachable.Sample(middle) < 0.5f) continue;
                Footprint f = Footprint.Box(middle, piece.Half, Yaw);
                _b.Paint(GroundPaint.Plaza, f, softness);
                _world.Placement.Add(f, PlacementLayer.Keepout);
                if (_plan.InsideLocal(piece.Middle, -Margin - 0.6f)) _spots.Add(middle);
            }
        }

        // A region of the yard cut into plots: at the lines it has to have, then evenly between them to about the size wanted. Each cut is a path.
        private void Region(Rect area, float[] forcedU, float[] forcedV, float width, float depth, bool south)
        {
            if (area.Width < 6f || area.Depth < 6f) return;
            List<float> us = Cuts(area.U0, area.U1, forcedU, width);
            List<float> vs = Cuts(area.V0, area.V1, forcedV, depth);
            for (int i = 1; i < us.Count - 1; i++) Path(new Rect(us[i] - PathWidth * 0.5f, area.V0, us[i] + PathWidth * 0.5f, area.V1));
            for (int j = 1; j < vs.Count - 1; j++) Path(new Rect(area.U0, vs[j] - PathWidth * 0.5f, area.U1, vs[j] + PathWidth * 0.5f));
            // Where its paths cross, somewhere a lantern can stand to light the plots round it.
            for (int i = 1; i < us.Count - 1; i++)
            {
                for (int j = 0; j < vs.Count; j++) _crossings.Add(new Vector2(us[i], j == 0 || j == vs.Count - 1 ? (vs[j] + area.Middle.Y) * 0.5f : vs[j]));
            }

            float Inset(List<float> cuts, int k, float edge, float outer) => k == 0 || k == cuts.Count - 1 ? (MathF.Abs(cuts[k] - outer) < 0.01f ? 0.2f : PathWidth * 0.5f + 0.4f) : PathWidth * 0.5f + 0.4f;
            for (int i = 0; i < us.Count - 1; i++)
            {
                for (int j = 0; j < vs.Count - 1; j++)
                {
                    float u0 = us[i] + Inset(us, i, us[i], _uMin), u1 = us[i + 1] - Inset(us, i + 1, us[i + 1], _uMax);
                    float v0 = vs[j] + Inset(vs, j, vs[j], _vMin), v1 = vs[j + 1] - Inset(vs, j + 1, vs[j + 1], _vMax);
                    var rect = new Rect(u0, v0, u1, v1);
                    if (rect.Width < 4f || rect.Depth < Graves.Length + 0.4f) continue;
                    // A plot inside the church's apron isn't one.
                    if (Overlaps(rect, _zone)) continue;
                    var cell = new Cell { Area = rect, South = south };
                    if (MathF.Abs(u0 - _uMin) < 0.3f) { cell.Outer = true; cell.Edge = Side.West; }
                    if (MathF.Abs(u1 - _uMax) < 0.3f) { cell.Outer = true; cell.Edge = Side.East; }
                    if (MathF.Abs(v0 - _vMin) < 0.3f) { cell.Outer = true; cell.Edge = Side.North; }
                    if (MathF.Abs(v1 - _vMax) < 0.3f) { cell.Outer = true; cell.Edge = Side.South; }
                    _cells.Add(cell);
                }
            }
        }

        private static bool Overlaps(Rect a, Rect b) => a.U0 < b.U1 && a.U1 > b.U0 && a.V0 < b.V1 && a.V1 > b.V0;

        private static List<float> Cuts(float a, float b, float[] forced, float target)
        {
            const float Least = 6.5f;
            var cuts = new List<float> { a };
            foreach (float f in forced.Where(f => !float.IsNaN(f) && f > a + Least && f < b - Least).OrderBy(f => f))
            {
                if (f - cuts[^1] >= Least) cuts.Add(f);
            }
            cuts.Add(b);
            var filled = new List<float> { a };
            for (int i = 1; i < cuts.Count; i++)
            {
                float span = cuts[i] - cuts[i - 1];
                int n = Math.Max(1, (int)MathF.Round(span / target));
                for (int k = 1; k < n; k++) filled.Add(cuts[i - 1] + span * k / n);
                filled.Add(cuts[i]);
            }
            return filled;
        }

        // ---------------------------------------------------------------- What each plot is

        // The oldest graves are nearest the church, the newest out by the wall, and the north side, where nobody wanted to lie, is the poor's. Then a few plots are given to something
        // else: the Ossuary's dig, a mausoleum or two against the wall, a family's railed plot, chest tombs and an old tree.
        private void TellStories()
        {
            WorldRandom r = _random.Fork("stories");
            float far = MathF.Max(_plan.HalfX, _plan.HalfZ);
            foreach (Cell cell in _cells)
            {
                Vector2 m = cell.Area.Middle;
                float du = MathF.Max(0f, MathF.Max(_zone.U0 - m.X, m.X - _zone.U1));
                float dv = MathF.Max(0f, MathF.Max(_zone.V0 - m.Y, m.Y - _zone.V1));
                float t = Math.Clamp(MathF.Sqrt(du * du + dv * dv) / (far * 0.55f), 0f, 1f);
                cell.Age = Math.Clamp(MathHelper.Lerp(0.92f, 0.15f, t) + r.Range(-0.14f, 0.14f), 0.05f, 1f);
                bool northSide = m.Y < _zone.V0;
                cell.Era = northSide && r.Chance(0.7f) ? Era.North : cell.Age > 0.7f ? Era.Old : cell.Age > 0.38f ? Era.Middle : Era.New;
            }

            var free = _cells.OrderBy(_ => r.Float()).ToList();
            Cell? Take(Func<Cell, bool> wanted)
            {
                Cell? found = free.FirstOrDefault(wanted);
                if (found != null) free.Remove(found);
                return found;
            }
            float Area(Cell c) => c.Area.Width * c.Area.Depth;

            // Where the camera can see it: the Ossuary has been digging.
            Cell? dug = Take(c => Area(c) >= 70f && c.Area.Width >= 8f && (c.South || r.Chance(0.3f)));
            if (dug != null) dug.Story = Story.Dug;
            int mausoleums = _site.Radius > 43f ? r.Int(1, 3) : r.Int(1, 2);
            for (int i = 0; i < mausoleums; i++)
            {
                Cell? cell = Take(c => c.Outer && c.Edge != Side.South && c.Area.Width >= 8f && c.Area.Depth >= 9f) ?? Take(c => c.Outer && c.Area.Width >= 8f && c.Area.Depth >= 9f);
                if (cell != null) cell.Story = Story.Mausoleum;
            }
            for (int i = r.Int(1, 2); i > 0; i--)
            {
                Cell? cell = Take(c => Area(c) >= 64f && c.Area.Width >= 8.5f && c.Area.Depth >= 7.5f);
                if (cell != null) cell.Story = Story.Family;
            }
            for (int i = r.Int(1, 2); i > 0; i--)
            {
                Cell? cell = Take(c => c.Area.Width >= 7f && c.Area.Depth >= 5f && c.Era != Era.North);
                if (cell != null) cell.Story = Story.Tombs;
            }
            for (int i = r.Int(1, 3); i > 0; i--)
            {
                Cell? cell = Take(c => Area(c) >= 50f);
                if (cell != null) cell.Story = Story.Tree;
            }
        }

        private void Fill(Cell cell)
        {
            WorldRandom r = _random.Fork("cell").Fork(_cells.IndexOf(cell));
            switch (cell.Story)
            {
                case Story.Dug: Dig(cell, r); break;
                case Story.Mausoleum: Mausoleum(cell, r); break;
                case Story.Family: Family(cell, r); break;
                case Story.Tombs: Tombs(cell, r); break;
                case Story.Tree: Tree(cell, r); break;
                default: Rows(cell.Area, cell, r); break;
            }
        }

        // ---------------------------------------------------------------- Graves

        // The stones a churchyard has of each era, each a few kinds a mason made over and over, as a churchyard is carved by a few hands: a grave picks one of its era's.
        private readonly Dictionary<Era, List<(StoneForm Form, GraveDress Dress, int Age, uint Seed)>> _stones = new();

        private (StoneForm Form, GraveDress Dress, int Age, uint Seed) StoneFor(Era era, WorldRandom r, bool level)
        {
            if (!_stones.TryGetValue(era, out var kinds))
            {
                kinds = new List<(StoneForm, GraveDress, int, uint)>();
                WorldRandom k = _random.Fork("stones").Fork((int)era);
                int count = k.Int(6, 8);
                for (int i = 0; i < count; i++)
                {
                    (StoneForm form, GraveDress dress, float age) = era switch
                    {
                        Era.Old => (Pick(k, (StoneForm.Slab, 3f), (StoneForm.Round, 3f), (StoneForm.Shouldered, 1.5f), (StoneForm.Broken, 1.5f), (StoneForm.Pointed, 1f)),
                                    Pick(k, (GraveDress.Grass, 4f), (GraveDress.Mound, 3f), (GraveDress.Sunken, 2f)), k.Range(0.72f, 1f)),
                        Era.Middle => (Pick(k, (StoneForm.Pointed, 3f), (StoneForm.Round, 2f), (StoneForm.Cross, 2f), (StoneForm.Ringed, 1.5f), (StoneForm.Shouldered, 1.5f), (StoneForm.Obelisk, 0.5f)),
                                    Pick(k, (GraveDress.Mound, 3f), (GraveDress.Kerb, 3f), (GraveDress.Ledger, 2f), (GraveDress.Grass, 2f)), k.Range(0.4f, 0.74f)),
                        Era.New => (Pick(k, (StoneForm.Pointed, 2f), (StoneForm.Cross, 2.5f), (StoneForm.Book, 2f), (StoneForm.Round, 1.5f), (StoneForm.Shouldered, 2f)),
                                    Pick(k, (GraveDress.Kerb, 6f), (GraveDress.Ledger, 2.5f), (GraveDress.Mound, 1.5f)), k.Range(0.04f, 0.36f)),
                        _ => (Pick(k, (StoneForm.Wooden, 5f), (StoneForm.Slab, 2f), (StoneForm.Broken, 2f), (StoneForm.Round, 1f)),
                              Pick(k, (GraveDress.Grass, 5f), (GraveDress.Mound, 4f)), k.Range(0.7f, 1f)),
                    };
                    kinds.Add((form, dress, (int)MathF.Round(age * 100f), (uint)k.NextULong()));
                }
                // And one with nothing in front of it, for ground too steep to lay a kerb on.
                kinds.Add((era == Era.North ? StoneForm.Wooden : era == Era.Old ? StoneForm.Slab : StoneForm.Pointed, GraveDress.Grass, era == Era.New ? 20 : 80, (uint)k.NextULong()));
                _stones[era] = kinds;
            }
            for (int tries = 0; tries < 6; tries++)
            {
                var kind = kinds[r.Int(kinds.Count - 1)];
                if (level || kind.Dress is GraveDress.Grass or GraveDress.Mound) return kind;
            }
            return kinds[^1];
        }

        private static T Pick<T>(WorldRandom random, params (T Item, float Weight)[] items)
        {
            float total = items.Sum(i => i.Weight), roll = random.Float() * total;
            foreach ((T item, float weight) in items)
            {
                roll -= weight;
                if (roll <= 0f) return item;
            }
            return items[^1].Item;
        }

        // Rows of graves across a plot, each stone at the north end of its grave facing south, as the camera looks, in pairs with a lane between one pair and the next, so that
        // every grave can be walked up to from one side. A row of a plot shares a favourite stone, as a row was cut by one mason in one year.
        private void Rows(Rect area, Cell cell, WorldRandom r, Func<Vector2, bool>? avoid = null)
        {
            // Where each row's middle is down the plot.
            var lines = new List<float>();
            float used = 0f;
            while (true)
            {
                float next = used + (lines.Count == 0 ? 0f : lines.Count % 2 == 1 ? BackToBack : Lane) + Graves.Length;
                if (next > area.Depth) break;
                lines.Add(used + (lines.Count == 0 ? 0f : lines.Count % 2 == 1 ? BackToBack : Lane) + Graves.Length * 0.5f);
                used = next;
            }
            int rows = lines.Count;
            if (rows == 0) return;
            float gap = r.Range(0.35f, 0.85f);
            float pitchU = Graves.Width + gap;
            int columns = Math.Max(0, (int)MathF.Floor((area.Width + gap) / pitchU));
            if (columns == 0) return;
            float v0 = area.Middle.Y - used * 0.5f;
            float u0 = area.Middle.X - (columns * pitchU - gap) * 0.5f + Graves.Width * 0.5f;
            float skip = cell.Era switch { Era.North => 0.32f, Era.Old => 0.16f, Era.Middle => 0.1f, _ => 0.12f };
            float wobble = cell.Era is Era.Old or Era.North ? 0.12f : 0.035f;
            for (int j = 0; j < rows; j++)
            {
                float v = v0 + lines[j];
                var favourite = StoneFor(cell.Era, r, level: true);
                for (int i = 0; i < columns; i++)
                {
                    if (r.Chance(skip)) continue;
                    float u = u0 + i * pitchU + r.Range(-0.12f, 0.12f);
                    Vector2 local = new Vector2(u, v);
                    if (avoid != null && avoid(local)) continue;
                    Grave(local, cell, r, r.Chance(0.5f) ? favourite : null, wobble);
                }
            }
        }

        private readonly List<HitPart> _parts = new();

        // A piece of the yard's ground kept for it, and what's solid about it where it stands (Hitboxes).
        private void Piece(in YardSpec spec, in Footprint f, float height, Vector2 at, float yaw) => Hitboxes.Put(_world.Placement, Hitboxes.Of(spec, _parts), _parts, f, PlacementLayer.Prop, height, at, yaw);

        private bool Grave(Vector2 local, Cell cell, WorldRandom r, (StoneForm Form, GraveDress Dress, int Age, uint Seed)? kind = null, float wobble = 0.05f)
        {
            if (!_plan.InsideLocal(local + new Vector2(0f, Graves.Length * 0.5f), -Margin) || !_plan.InsideLocal(local - new Vector2(0f, Graves.Length * 0.5f), -Margin)) return false;
            Vector2 at = W(local);
            float yaw = Yaw + r.Range(-wobble, wobble);
            Footprint f = Footprint.Box(at, new Vector2(Graves.Width * 0.5f, Graves.Length * 0.5f), yaw);
            if (!Ground(f, 0.45f) || _world.Placement.Overlaps(f, PlacementLayer.Built | PlacementLayer.Road | PlacementLayer.Tree, 0.12f)) return false;
            // Along a grave, which a model can't lean along, the ground has to be near enough level for a kerb or a slab.
            Vector2 down = _plan.Az * (Graves.Length * 0.5f);
            bool level = MathF.Abs(_world.Elevation.Sample(at + down) - _world.Elevation.Sample(at - down)) < GraveSlope * Graves.Length;
            var chosen = kind.HasValue && (level || kind.Value.Dress is GraveDress.Grass or GraveDress.Mound) ? kind.Value : StoneFor(cell.Era, r, level);
            // The grave is walked over, and so is a stone low or thin enough to step over or brush through: a cross is a post and an obelisk stands in the way (Hitboxes).
            var spec = new YardSpec(YardKind.Stone, chosen.Seed, (int)chosen.Form, (int)chosen.Dress, chosen.Age);
            Piece(spec, f, 0.9f, at, yaw);
            _world.Pending.Add(new PendingObject(spec.Id, at, yaw, Vector3.One, null));
            // Someone still comes to the newer graves, and now and then to an older one.
            // Left at the foot of the stone, so it's lit from in front, where the grave's turf lets them stand; on a kerb or a slab, at the grave's foot.
            if (chosen.Form != StoneForm.Wooden && (chosen.Age < 45 || (chosen.Age < 75 && r.Chance(0.35f))))
            {
                bool atStone = chosen.Dress is GraveDress.Grass or GraveDress.Mound or GraveDress.Sunken;
                float along = atStone ? Graves.Head + 0.8f : Graves.Length * 0.5f + 0.55f;
                _candles.Add(W(local + new Vector2(r.Range(-0.35f, 0.35f), along)));
            }
            return true;
        }

        // ---------------------------------------------------------------- Stories

        // The Ossuary's dig: its pit with the hoist over it and what it's brought up laid out on its cloth (SettlementStage's dig), and the graves round it opened, their stones
        // pulled over, the rest of the plot's stones broken.
        private void Dig(Cell cell, WorldRandom r)
        {
            Rect a = cell.Area;
            Vector2 middle = a.Middle;
            float yaw = Yaw + MathF.PI * 0.5f + r.Range(-0.2f, 0.2f);
            Vector2 digAt = W(middle + new Vector2(-a.Width * 0.18f, 0f));
            if (_b.Place(StructureCatalog.Dig, digAt, yaw, padding: 0.6f))
            {
                _b.Paint(GroundPaint.Yard, StructureCatalog.Dig.FootprintAt(digAt, yaw, 1.6f), 3f);
                Vector2 beside = digAt + _plan.Az * (StructureCatalog.Dig.HalfExtents.X + StructureCatalog.BoneRows.HalfExtents.Y + 1.4f);
                _b.Place(StructureCatalog.BoneRows, beside, yaw, padding: 0.3f);
            }
            int opened = r.Int(2, 3);
            for (int i = 0; i < opened; i++)
            {
                Vector2 local = new Vector2(a.U1 - 2.2f - i * 3.4f, a.V0 + 2.6f + r.Range(0f, MathF.Max(a.Depth - 5.2f, 0f)));
                Vector2 at = W(local);
                Footprint f = Footprint.Box(at, new Vector2(1.5f, 2.2f), Yaw);
                if (!Ground(f, 0.4f) || _world.Placement.Overlaps(f, PlacementLayer.Built | PlacementLayer.Road, 0.2f)) continue;
                var spec = new YardSpec(YardKind.Dug, NextSeed(), r.Chance(0.7f) ? 0 : 1, (int)MathF.Round(cell.Age * 100f));
                Piece(spec, f, 0.9f, at, Yaw);
                _world.Pending.Add(new PendingObject(spec.Id, at, Yaw, Vector3.One, null));
                _b.Paint(GroundPaint.Yard, Footprint.Box(at, new Vector2(2.2f, 2.8f), Yaw), 2f);
            }
            // What's left standing of the plot, broken.
            for (int i = 0; i < 4; i++)
            {
                Vector2 local = new Vector2(r.Range(a.U0 + 1f, a.U1 - 1f), r.Range(a.V0 + 2f, a.V1 - 2f));
                Grave(local, cell, r, (StoneForm.Broken, GraveDress.Grass, (int)MathF.Round(cell.Age * 100f), NextSeed()), 0.3f);
            }
        }

        // A family's house for its dead, against the wall with its door to the yard, and its plot's graves in front of it.
        private void Mausoleum(Cell cell, WorldRandom r)
        {
            Rect a = cell.Area;
            var form = r.Chance(0.5f) ? MausoleumForm.Classical : MausoleumForm.Gothic;
            uint seed = NextSeed();
            Vector3 size = YardForge.MausoleumSize(seed, form);
            Vector2 door = cell.Edge switch { Side.North => Vector2.UnitY, Side.South => -Vector2.UnitY, Side.East => -Vector2.UnitX, _ => Vector2.UnitX };
            float deep = size.Z + 0.3f + 1.4f, wide = size.X + 0.3f;
            bool alongU = MathF.Abs(door.Y) > 0.5f;
            Vector2 back = cell.Edge switch
            {
                Side.North => new Vector2(a.Middle.X, a.V0),
                Side.South => new Vector2(a.Middle.X, a.V1),
                Side.East => new Vector2(a.U1, a.Middle.Y),
                _ => new Vector2(a.U0, a.Middle.Y),
            };
            if ((alongU ? a.Depth : a.Width) < deep * 2f || (alongU ? a.Width : a.Depth) < wide * 2f) { Rows(a, cell, r); return; }
            Vector2 local = back + door * (size.Z + 0.3f);
            Vector2 at = W(local);
            float yaw = SiteBuilder.YawFacing(_plan.Dir(door));
            Footprint f = Footprint.Box(W(local + door * 0.7f), new Vector2(wide, size.Z + 1f), yaw);
            if (!Ground(f, 0.35f) || _world.Placement.Overlaps(f, PlacementLayer.Built | PlacementLayer.Road | PlacementLayer.Tree, 0.3f)) { Rows(a, cell, r); return; }
            float floor = _world.Elevation.Sample(at);
            _world.Placement.Add(Footprint.Box(at, new Vector2(wide, size.Z + 0.3f), yaw), PlacementLayer.Structure, solid: true);
            _world.Placement.Add(f, PlacementLayer.Keepout);
            _world.Pads.Add(new GroundPad(f.Grown(0.5f), floor, 3f));
            bool opened = r.Chance(0.35f);
            var spec = new YardSpec(YardKind.Mausoleum, seed, (int)form, opened ? 1 : 0, _shade, _warmth);
            _world.Pending.Add(new PendingObject(spec.Id, at, yaw, Vector3.One, floor));
            _spots.Add(W(local + door * (size.Z + 3.4f)));
            // Its family's graves in what's left of the plot.
            Rect rest = cell.Edge switch
            {
                Side.North => a with { V0 = a.V0 + (size.Z + 0.3f) * 2f + 2.2f },
                Side.South => a with { V1 = a.V1 - (size.Z + 0.3f) * 2f - 2.2f },
                Side.East => a with { U1 = a.U1 - (size.Z + 0.3f) * 2f - 2.2f },
                _ => a with { U0 = a.U0 + (size.Z + 0.3f) * 2f + 2.2f },
            };
            if (rest.Width > 3f && rest.Depth > Graves.Length) Rows(rest, cell, r);
        }

        // A railed plot with an obelisk or a ringed cross at its head and the family in kerbed graves in front of it, the rest of the plot in rows.
        private void Family(Cell cell, WorldRandom r)
        {
            Rect a = cell.Area;
            float w = MathF.Min(a.Width - 0.8f, r.Range(7.6f, 9.6f)), d = MathF.Min(a.Depth - 0.8f, r.Range(7.2f, 8.6f));
            Vector2 middle = new Vector2(a.Middle.X + r.Range(-1f, 1f) * MathF.Max(0f, (a.Width - w) * 0.5f - 0.4f), a.V0 + d * 0.5f + 0.4f);
            Vector2 at = W(middle);
            // The railing is walked round and seen through, as a post is.
            Vector2 hx = _plan.Ax * (w * 0.5f), hz = _plan.Az * (d * 0.5f);
            Vector2[] corners = { at - hx - hz, at + hx - hz, at + hx + hz, at - hx + hz };
            Footprint whole = Footprint.Box(at, new Vector2(w * 0.5f + 0.3f, d * 0.5f + 0.3f), Yaw);
            if (!Ground(whole, 0.25f) || _world.Placement.Overlaps(whole, PlacementLayer.Built | PlacementLayer.Road | PlacementLayer.Tree, 0.1f)) { Rows(a, cell, r); return; }
            for (int i = 0; i < 4; i++)
            {
                Vector2 p0 = corners[i], p1 = corners[(i + 1) % 4];
                if (i == 2)
                {
                    // Its gap, on the south side.
                    Vector2 mid = (p0 + p1) * 0.5f, dir = (p1 - p0).Normalized();
                    _world.Placement.Add(Footprint.Segment(p0, mid - dir * 0.85f, 0.2f), PlacementLayer.Prop, solid: true, height: 0f);
                    _world.Placement.Add(Footprint.Segment(mid + dir * 0.85f, p1, 0.2f), PlacementLayer.Prop, solid: true, height: 0f);
                    continue;
                }
                _world.Placement.Add(Footprint.Segment(p0, p1, 0.2f), PlacementLayer.Prop, solid: true, height: 0f);
            }
            var railing = new YardSpec(YardKind.Railing, NextSeed(), YardSpec.Cm(w), YardSpec.Cm(d), 1);
            _world.Pending.Add(new PendingObject(railing.Id, at, Yaw, Vector3.One, null));
            int age = (int)MathF.Round(Math.Clamp(cell.Age, 0.25f, 0.8f) * 100f);
            uint headSeed = NextSeed();
            var head = r.Chance(0.6f) ? StoneForm.Obelisk : StoneForm.Ringed;
            Grave(middle + new Vector2(0f, -d * 0.5f + 0.5f + Graves.Length * 0.5f), cell, r, (head, GraveDress.Ledger, age, headSeed), 0.01f);
            int family = (int)MathF.Floor((w - 0.8f) / (Graves.Width + 0.4f));
            float start = middle.X - (family * (Graves.Width + 0.4f) - 0.4f) * 0.5f + Graves.Width * 0.5f;
            uint kin = NextSeed();
            for (int i = 0; i < family; i++)
            {
                if (MathF.Abs(start + i * (Graves.Width + 0.4f) - middle.X) < 1.2f) continue;
                Grave(new Vector2(start + i * (Graves.Width + 0.4f), middle.Y + d * 0.5f - Graves.Length * 0.5f - 0.5f), cell, r, (r.Chance(0.5f) ? StoneForm.Shouldered : StoneForm.Pointed, GraveDress.Kerb, age, kin + (uint)i % 3), 0.01f);
            }
            Rect rest = a with { V0 = a.V0 + d + 1.2f + Lane * 0.5f };
            if (rest.Depth > Graves.Length) Rows(rest, cell, r);
        }

        // A row of chest tombs, those who could pay for one, and the rest of the plot in rows.
        private void Tombs(Cell cell, WorldRandom r)
        {
            Rect a = cell.Area;
            int count = r.Int(2, 4);
            int age = (int)MathF.Round(cell.Age * 100f);
            float v = a.V0 + 2.4f;
            float pitch = 3.6f;
            float u = a.Middle.X - (count - 1) * pitch * 0.5f;
            int placed = 0;
            float deepest = 0f;
            for (int i = 0; i < count; i++)
            {
                uint seed = NextSeed();
                Vector3 size = YardForge.TombSize(seed);
                Vector2 local = new Vector2(u + i * pitch + r.Range(-0.2f, 0.2f), a.V0 + size.Z * 0.5f + 0.5f);
                if (!_plan.InsideLocal(local, -Margin)) continue;
                Vector2 at = W(local);
                float yaw = Yaw + r.Range(-0.04f, 0.04f);
                Footprint f = Footprint.Box(at, new Vector2(size.X * 0.5f + 0.15f, size.Z * 0.5f + 0.15f), yaw);
                if (!Ground(f, 0.25f) || _world.Placement.Overlaps(f, PlacementLayer.Built | PlacementLayer.Road | PlacementLayer.Tree, 0.3f)) continue;
                bool opened = r.Chance(cell.Age > 0.5f ? 0.3f : 0.12f);
                var spec = new YardSpec(YardKind.Tomb, seed, r.Int(0, 2), opened ? 1 : 0, age, _shade, _warmth);
                Piece(spec, f, size.Y, at, yaw);
                _world.Pending.Add(new PendingObject(spec.Id, at, yaw, Vector3.One, null));
                placed++;
                deepest = MathF.Max(deepest, size.Z + 0.8f);
            }
            float used = placed > 0 ? deepest + Lane : 0f;
            Rect rest = a with { V0 = a.V0 + used };
            if (rest.Depth > Graves.Length) Rows(rest, cell, r);
            _spots.Add(W(a.Middle.X, a.V0 + used - Lane * 0.5f));
        }

        // An old tree at one corner of the plot, the graves round it kept off its trunk, its leaves on them.
        private void Tree(Cell cell, WorldRandom r)
        {
            Rect a = cell.Area;
            Vector2 corner = new Vector2(r.Chance(0.5f) ? a.U0 + 2.6f : a.U1 - 2.6f, r.Chance(0.5f) ? a.V0 + 2.6f : a.V1 - 2.6f);
            Vector2? trunk = PlantTree(corner, r, big: true) ? corner : null;
            Rows(a, cell, r, trunk.HasValue ? local => (local - trunk.Value).Length < 3.6f : null);
        }

        // ---------------------------------------------------------------- Trees

        // What grows in a churchyard, by its land: cypresses where it's warm, dead pines and silver snags where it's cold, and elsewhere ancient oaks with a snag or two.
        private bool PlantTree(Vector2 local, WorldRandom r, bool big)
        {
            BiomeDefinition biome = _world.Plan.Biomes[Math.Clamp(_site.Biome, 0, _world.Plan.Biomes.Count - 1)];
            string id = biome.Temperature >= 0.62f ? "cypress" : biome.Temperature <= 0.35f ? (r.Chance(0.5f) ? "pine.dead" : "snag.silver") : big && r.Chance(0.7f) ? "oak.ancient" : "snag.silver";
            if (!NatureCatalog.TryGet(id, out WorldAsset asset)) return false;
            Vector2 at = W(local);
            float scale = asset.Scale.Lerp(r.Range(0.55f, 0.85f)) * (id == "oak.ancient" ? 0.75f : 1f);
            float trunk = MathF.Max(asset.SolidRadius * scale, 0.5f);
            Footprint f = Footprint.Circle(at, trunk + 0.4f);
            if (!_b.GroundFits(f, 0.4f) || _world.Placement.Overlaps(f, PlacementLayer.Built | PlacementLayer.Road | PlacementLayer.Tree, 0.2f)) return false;
            _world.Placement.Add(Footprint.Circle(at, trunk + 0.5f), PlacementLayer.Tree);
            _world.Placement.Add(Footprint.Circle(at, trunk), PlacementLayer.None, solid: true);
            _world.Pending.Add(new PendingObject(NatureCatalog.ModelAt(asset, _world.Plan.Seed, at), at, r.Float() * MathF.Tau, new Vector3(scale), null));
            _trees.Add(at);
            return true;
        }

        private void Plant()
        {
            WorldRandom r = _random.Fork("trees");
            // A few more by the wall, in the walk's corners and along it, where they've grown up unchecked.
            int more = r.Int(2, 4);
            for (int i = 0, tries = 0; i < more && tries < 16; tries++)
            {
                float u = r.Range(-_plan.HalfX + 2f, _plan.HalfX - 2f), v = r.Chance(0.5f) ? -_plan.HalfZ + 1.9f : _plan.HalfZ - 1.9f;
                if (r.Chance(0.4f)) (u, v) = (r.Chance(0.5f) ? -_plan.HalfX + 1.9f : _plan.HalfX - 1.9f, r.Range(-_plan.HalfZ + 2f, _plan.HalfZ - 2f));
                if (!_plan.InsideLocal(new Vector2(u, v), -1.5f)) continue;
                if (PlantTree(new Vector2(u, v), r, big: false)) i++;
            }

            // Leaves under the trees, and pale fungus glowing among the oldest graves.
            BiomeDefinition biome = _world.Plan.Biomes[Math.Clamp(_site.Biome, 0, _world.Plan.Biomes.Count - 1)];
            string litter = biome.Temperature >= 0.62f ? "" : biome.Temperature <= 0.35f ? "litter.oak" : r.Chance(0.5f) ? "litter.amber" : "litter.oak";
            if (litter.Length > 0 && NatureCatalog.TryGet(litter, out WorldAsset leaves))
            {
                foreach (Vector2 tree in _trees)
                {
                    for (int i = 0; i < 9; i++)
                    {
                        Vector2 at = tree + SiteBuilder.Direction(r.Float() * MathF.Tau) * r.Range(1.2f, 7f);
                        if (!_plan.Inside(at, -1f)) continue;
                        _world.Pending.Add(new PendingObject(NatureCatalog.ModelAt(leaves, _world.Plan.Seed, at), at, r.Float() * MathF.Tau, new Vector3(r.Range(0.8f, 1.3f)), null));
                    }
                }
            }
            if (NatureCatalog.TryGet("mushroom.ghost", out WorldAsset caps))
            {
                foreach (Cell cell in _cells.Where(c => c.Era is Era.Old or Era.North))
                {
                    for (int i = r.Int(0, 2); i > 0; i--)
                    {
                        Vector2 at = W(new Vector2(r.Range(cell.Area.U0, cell.Area.U1), r.Range(cell.Area.V0, cell.Area.V1)));
                        if (_world.Placement.Overlaps(Footprint.Circle(at, 0.3f), PlacementLayer.Structure | PlacementLayer.Prop | PlacementLayer.Keepout)) continue;
                        _world.Pending.Add(new PendingObject(NatureCatalog.ModelAt(caps, _world.Plan.Seed, at), at, r.Float() * MathF.Tau, new Vector3(r.Range(0.8f, 1.2f)), null));
                    }
                }
            }
        }

        // ---------------------------------------------------------------- The cross, lights and the rest

        // The churchyard's cross, south of the church beside the path to its porch, where the path from the gate meets the church's apron.
        private void RaiseCross()
        {
            WorldRandom r = _random.Fork("cross");
            float height = r.Range(6.2f, 7.8f);
            float porch = _church != null ? _churchAt.X + _church.PorchX : _churchAt.X;
            Gate? south = _gates.Where(g => g.Side == Side.South).OrderBy(g => MathF.Abs(g.At - porch)).Cast<Gate?>().FirstOrDefault();
            float u = south?.At ?? porch;
            foreach (float side in r.Chance(0.5f) ? new[] { 1f, -1f } : new[] { -1f, 1f })
            {
                foreach (float down in new[] { 4.6f, 7f, 9.5f })
                {
                    Vector2 local = new Vector2(u + side * (GatePath * 0.5f + 2.6f), _zone.V1 + PathWidth * 0.5f + down - 2f);
                    Vector2 at = W(local);
                    Footprint f = Footprint.Circle(at, 1.95f);
                    if (!_b.GroundFits(f, 0.3f) || _world.Placement.Overlaps(f, PlacementLayer.Built | PlacementLayer.Road | PlacementLayer.Tree, 0.3f)) continue;
                    var spec = new YardSpec(YardKind.Cross, NextSeed(), r.Chance(0.5f) ? 1 : 0, YardSpec.Cm(height), _shade, _warmth);
                    Piece(spec, f, 1.3f, at, Yaw);
                    _world.Pending.Add(new PendingObject(spec.Id, at, Yaw, Vector3.One, null));
                    // Candles at its foot, on the side towards the path.
                    Candles(W(local + new Vector2(-side * 2.3f, 0.6f)), 0, r);
                    return;
                }
            }
        }

        // Lanterns down the path from the porch's gate, at the corners of the church's apron and at the inner end of every other gate's path; and candles left on the newest graves.
        private void Light()
        {
            WorldRandom r = _random.Fork("light");
            float lampHeight = r.Range(3.6f, 4.3f);
            foreach (Gate gate in _gates.OrderByDescending(g => g.Side == Side.South))
            {
                if (gate.Side == Side.South)
                {
                    float side = 1f;
                    for (float v = _plan.HalfZ - 3.5f; v > _zone.V1 + 2f; v -= r.Range(8.5f, 10.5f))
                    {
                        Lamp(new Vector2(gate.At + side * (GatePath * 0.5f + 0.75f), v), lampHeight, new Vector2(-side, 0f));
                        side = -side;
                    }
                    continue;
                }
                (Vector2 inner, Vector2 toward) = gate.Side switch
                {
                    Side.North => (new Vector2(gate.At + PathWidth * 0.5f + 0.75f, _zone.V0 - 2.5f), -Vector2.UnitX),
                    Side.East => (new Vector2(_zone.U1 + 2.5f, gate.At + PathWidth * 0.5f + 0.75f), -Vector2.UnitY),
                    _ => (new Vector2(_zone.U0 - 2.5f, gate.At + PathWidth * 0.5f + 0.75f), -Vector2.UnitY),
                };
                Lamp(inner, lampHeight, toward);
            }
            foreach (float su in new[] { -1f, 1f })
            {
                foreach (float sv in new[] { -1f, 1f })
                {
                    Vector2 corner = new Vector2(su < 0f ? _zone.U0 - 1.6f : _zone.U1 + 1.6f, sv < 0f ? _zone.V0 - 1.6f : _zone.V1 + 1.6f);
                    Lamp(corner, lampHeight, new Vector2(-su, -sv).Normalized());
                }
            }

            // And among the plots where their paths cross, the south's first, as that's what the camera sees.
            foreach (Vector2 crossing in _crossings.OrderByDescending(c => c.Y).ThenBy(_ => r.Float()))
            {
                if (_lamps >= MostLamps) break;
                Vector2 offset = new Vector2(PathWidth * 0.5f + 0.7f, PathWidth * 0.5f + 0.7f);
                Lamp(crossing + offset, lampHeight, -Vector2.Normalize(offset));
            }

            // Candles on the graves someone still comes to, spread out rather than all in one corner.
            var chosen = new List<Vector2>();
            foreach (Vector2 foot in _candles.OrderBy(_ => r.Float()))
            {
                if (chosen.Count >= MostCandles) break;
                if (chosen.Any(c => (c - foot).Length < 5.5f)) continue;
                if (Candles(foot, r.Chance(0.65f) ? 1 : 0, r)) chosen.Add(foot);
            }

            // And a bank of them at the porch, where people still light one on the way in.
            if (_church != null) Candles(W(_churchAt + _church.PorchMouth + new Vector2(_church.PorchHalf * 0.4f + 1.7f, 1.4f)), 2, r);
        }

        // A lantern on a path, its lantern hung out towards `toward`, in the yard's frame.
        private bool Lamp(Vector2 local, float height, Vector2 toward)
        {
            if (_lamps >= MostLamps || !_plan.InsideLocal(local, -1.5f)) return false;
            Vector2 at = W(local);
            Footprint f = Footprint.Circle(at, 0.45f);
            if (!_b.GroundFits(f, 0.5f) || _world.Placement.Overlaps(f, PlacementLayer.Structure | PlacementLayer.Prop | PlacementLayer.Fence | PlacementLayer.Road | PlacementLayer.Tree, 0.2f)) return false;
            // A churchyard's lanterns were all made by one smith, in two patterns: two models, however many there are.
            var spec = new YardSpec(YardKind.Lamp, _seed0 + (uint)(_lamps % 2), (int)_lamp, YardSpec.Cm(height), _shade, _warmth);
            Piece(spec, f, 0f, at, SiteBuilder.YawFacing(_plan.Dir(toward)));
            _world.Pending.Add(new PendingObject(spec.Id, at, SiteBuilder.YawFacing(_plan.Dir(toward)), Vector3.One, null));
            _lamps++;
            return true;
        }

        private bool Candles(Vector2 at, int form, WorldRandom r)
        {
            // Candles stand on a grave as well as by one: they're in nobody's way, so only what's built or growing keeps them off.
            Footprint f = Footprint.Circle(at, form == 2 ? 1f : 0.4f);
            if (_world.Placement.Overlaps(f, PlacementLayer.Structure | PlacementLayer.Fence | PlacementLayer.Road | PlacementLayer.Tree, 0.05f)) return false;
            _world.Placement.Add(f, PlacementLayer.Prop, solid: false, height: 0f);
            // And its candles come in a few kinds of bunch, as a model each.
            var spec = new YardSpec(YardKind.Candles, _seed0 + 7u + (uint)r.Int(3), form, form == 2 ? 6 : 2 + (int)(_seed0 % 3));
            _world.Pending.Add(new PendingObject(spec.Id, at, form == 2 ? Yaw : r.Float() * MathF.Tau, Vector3.One, null));
            return true;
        }

        // A bench or two on the church's apron, against its south side, facing the graves.
        private void Furnish()
        {
            if (_church == null) return;
            WorldRandom r = _random.Fork("benches");
            float porch = _churchAt.X + _church.PorchX;
            int benches = r.Int(1, 2);
            foreach (float side in new[] { -1f, 1f })
            {
                if (benches-- <= 0) break;
                Vector2 local = new Vector2(porch + side * r.Range(7f, 11f), _zone.V1 - 1.1f);
                Vector2 at = W(local);
                Footprint f = Footprint.Box(at, new Vector2(1.75f, 0.55f), Yaw);
                if (_world.Placement.Overlaps(f, PlacementLayer.Structure | PlacementLayer.Prop | PlacementLayer.Fence | PlacementLayer.Tree | PlacementLayer.Road, 0.3f)) continue;
                var bench = new YardSpec(YardKind.Bench, NextSeed(), YardSpec.Cm(3.3f));
                Piece(bench, f, 1f, at, Yaw);
                _world.Pending.Add(new PendingObject(bench.Id, at, Yaw, Vector3.One, null));
            }
        }

        // ---------------------------------------------------------------- The dead

        // The dead stand on the paths and in the lanes between the rows, spread over the yard, more on the side the camera sees; and what they took is by the porch.
        private void Haunt()
        {
            WorldRandom r = _random.Fork("dead");
            int count = r.Int(8, 11);
            var chosen = new List<Vector2>();
            // Never by a gate, where whoever's put right in the way in may be moved out of it, and through it.
            _spots.RemoveAll(s => _plan.Gates.Any(g => (g.At - s).Length < 7f));
            foreach (Vector2 spot in _spots.OrderBy(s => r.Float() - (_plan.ToLocal(s).Y > 0f ? 0.25f : 0f)))
            {
                if (chosen.Count >= count) break;
                if (chosen.Any(c => (c - spot).Length < 7f)) continue;
                Footprint standing = Footprint.Circle(spot, 1.2f);
                if (!_b.GroundFits(standing, 0.55f) || _world.Placement.Overlaps(standing, PlacementLayer.Structure | PlacementLayer.Prop | PlacementLayer.Fence)) continue;
                chosen.Add(spot);
                _b.Spawn(SpawnRole.Undead, spot);
            }
            // Too few apart: closer together, but still on the paths.
            foreach (Vector2 spot in _spots.OrderBy(s => r.Float()))
            {
                if (chosen.Count >= count) break;
                if (chosen.Any(c => (c - spot).Length < 3.5f)) continue;
                if (_world.Placement.Overlaps(Footprint.Circle(spot, 1.2f), PlacementLayer.Structure | PlacementLayer.Prop | PlacementLayer.Fence)) continue;
                chosen.Add(spot);
                _b.Spawn(SpawnRole.Undead, spot);
            }

            Vector2 chest = _church != null ? W(_churchAt + _church.PorchMouth + new Vector2(-_church.PorchHalf * 0.4f - 1.8f, 1.6f)) : _plan.Centre;
            foreach (float push in new[] { 0f, 1.5f, 3f, 4.5f })
            {
                Vector2 at = chest + _plan.Az * push;
                if (_world.Placement.Overlaps(Footprint.Circle(at, 1.1f), PlacementLayer.Structure | PlacementLayer.Prop | PlacementLayer.Fence)) continue;
                _b.Spawn(SpawnRole.Chest, at);
                return;
            }
            _b.Spawn(SpawnRole.Chest, chest);
        }
    }
}


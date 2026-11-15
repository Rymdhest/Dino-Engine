using Dino_Engine.Modelling.Procedural.Architecture.Places;
using Dino_Engine.Modelling.Procedural.Fence;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.World.Generation.Places
{
    /// <summary>
    /// Lays out the Reich's prison for beasts (PLACES.md). Inside the Reich's palisade, with its banners at the gate where the road comes, a breaking yard of trodden mud in the middle with the
    /// post a beast is chained to and goaded until it takes the collar; round it, cages of black iron with their doors to the yard, each with a beast in it that was born free to what got out of
    /// the pens, young hornbacks, clubtails and spikebacks, domeheads and striders; the harness racks and the forge the collars are made at; the wranglers' tents and the watch tower. When the place
    /// is freed every cage's door swings open and what was in it walks out through the gate and back into the wild.
    /// </summary>
    internal sealed class PrisonBuilder : PlaceBuilder
    {
        private enum Side { Back, Front, Left, Right }

        private static (string Look, int Variants, float Scale, Vector3 Cage)[] Captives => PlaceStory.Captives;

        private float _hx, _hz;
        private Side _gateSide;
        private Vector2 _gateLocal;
        private Vector2 _away;

        private PrisonBuilder(SiteBuilder b) : base(b, "prison") { }

        public static void Build(SiteBuilder b) => new PrisonBuilder(b).Lay();

        private void Lay()
        {
            // Its back to the north, so the cages along it face the camera, which looks north.
            Plan.Yaw = MathF.PI + Random.Range(-0.12f, 0.12f);
            _hx = Site.Radius * Random.Range(0.8f, 0.9f);
            _hz = Site.Radius * Random.Range(0.68f, 0.78f);
            Plan.Half = new Vector2(_hx, _hz);
            ChooseGate();
            Palisade(Random.Fork("palisade"));
            Yard(Random.Fork("yard"));
            CageRows(Random.Fork("cages"));
            Works(Random.Fork("works"));
            Keeping(Random.Fork("keeping"));
            Hold(Random.Fork("holders"));
            Underfoot(Random.Fork("underfoot"));
        }

        private static Vector2 Outward(Side side) => side switch
        {
            Side.Back => -Vector2.UnitY,
            Side.Front => Vector2.UnitY,
            Side.Left => -Vector2.UnitX,
            _ => Vector2.UnitX,
        };

        // The gate's on the side the road comes from, so whoever comes up the road comes straight in.
        private void ChooseGate()
        {
            Vector2 road = Plan.ToLocal(Site.Position + Site.RoadDirection) ;
            road = road.LengthSquared > 1e-4f ? road.Normalized() : Vector2.UnitY;
            _gateSide = Math.Abs(road.X) / _hx > Math.Abs(road.Y) / _hz ? (road.X > 0f ? Side.Right : Side.Left) : (road.Y > 0f ? Side.Front : Side.Back);
            float along = (_gateSide is Side.Front or Side.Back ? road.X * _hx : road.Y * _hz) * 0.5f;
            _gateLocal = _gateSide switch
            {
                Side.Back => new Vector2(Math.Clamp(along, -_hx + 8f, _hx - 8f), -_hz),
                Side.Front => new Vector2(Math.Clamp(along, -_hx + 8f, _hx - 8f), _hz),
                Side.Left => new Vector2(-_hx, Math.Clamp(along, -_hz + 8f, _hz - 8f)),
                _ => new Vector2(_hx, Math.Clamp(along, -_hz + 8f, _hz - 8f)),
            };
            Vector2 outward = Plan.Dir(Outward(_gateSide));
            Plan.Gates.Add((W(_gateLocal), outward));
            // Where what's freed goes: out through the gate and on into the wild, as far as there's ground.
            _away = W(_gateLocal) + outward * 38f;
            for (int k = 0; k < 12; k++)
            {
                Vector2 tried = W(_gateLocal) + SiteBuilder.Direction(MathF.Atan2(outward.Y, outward.X) + (k % 2 == 0 ? 1f : -1f) * k * 0.2f) * 40f;
                if (World.InBounds(tried) && World.Reachable.Sample(tried) > 0.5f) { _away = tried; break; }
            }
        }

        // The Reich's palisade round it, broken where the gate is, and its banners outside the gate.
        private void Palisade(WorldRandom r)
        {
            Vector2[] corners = { new(-_hx, -_hz), new(_hx, -_hz), new(_hx, _hz), new(-_hx, _hz) };
            Side[] sides = { Side.Back, Side.Right, Side.Front, Side.Left };
            const float GateHalf = 4.6f;
            for (int i = 0; i < 4; i++)
            {
                Vector2 a = corners[i], c = corners[(i + 1) % 4];
                if (sides[i] != _gateSide)
                {
                    B.Wall(FenceUse.Pen, W(a), W(c), Site.Position);
                    continue;
                }
                Vector2 along = (c - a).Normalized();
                float t = Vector2.Dot(_gateLocal - a, along);
                B.Wall(FenceUse.Pen, W(a), W(a + along * (t - GateHalf)), Site.Position);
                B.Wall(FenceUse.Pen, W(a + along * (t + GateHalf)), W(c), Site.Position);
            }
            // Nothing stands in the gate's way, inside or out.
            Vector2 inward = -Outward(_gateSide);
            Keep(Footprint.Box(W(_gateLocal + inward * 5f), new Vector2(GateHalf, 6f), YawOf(inward)));
            Vector2 outward = Plan.Dir(Outward(_gateSide));
            Vector2 across = new Vector2(-outward.Y, outward.X);
            foreach (float s in new[] { -1f, 1f })
            {
                Vector2 at = W(_gateLocal) + outward * 2.6f + across * (s * (GateHalf + 1.6f));
                PutRound(new PlaceSpec(PlacePiece.Banner, NextSeed(), PlaceSpec.Cm(r.Range(6.4f, 7.4f)), 1), at, SiteBuilder.YawFacing(outward), 0.5f, 0f, 0.2f, 0.5f);
            }
            foreach (float s in new[] { -1f, 1f })
            {
                Vector2 at = W(_gateLocal + inward * 2.2f) + across * (s * (GateHalf - 0.6f));
                PutRound(new PlaceSpec(PlacePiece.Lantern, NextSeed(), PlaceSpec.Cm(4.2f), 1), at, SiteBuilder.YawFacing(-outward), 0.4f, 0f, 0.2f, 0.5f);
            }
        }

        private Vector2 _post;

        // The breaking yard: trodden mud round the post in the middle, a beast chained to it now and then, mid-breaking.
        private void Yard(WorldRandom r)
        {
            float radius = r.Range(6.5f, 8.5f);
            _post = new Vector2(r.Range(-2f, 2f), r.Range(-1f, 3f));
            Paint(GroundPaint.Mud, Footprint.Circle(W(_post), radius), 2.5f);
            Paint(GroundPaint.Yard, Footprint.Box(Plan.Centre, new Vector2(_hx - 1f, _hz - 1f), Plan.Yaw), 4f);
            Vector2 at = W(_post);
            if (!PutRound(new PlaceSpec(PlacePiece.BreakingPost, NextSeed(), PlaceSpec.Cm(radius)), at, r.Float() * MathF.Tau, 0.7f, Tall, 0.3f, 0.4f)) return;
            // What's being broken now, chained to it, which goes free with the rest.
            if (!r.Chance(0.55f)) return;
            var (look, variants, scale, _) = Captives[r.Int(3)];
            Vector2 standing = at + SiteBuilder.Direction(r.Float() * MathF.Tau) * 3.4f;
            string model = look + r.Int(variants);
            float facing = SiteBuilder.YawFacing(at - standing);
            B.Spawn(SpawnRole.Captive, standing);
            Plan.Cages.Add(new PlaceCage(standing, facing, "", 0f, standing, standing, facing, model, scale * 0.9f, standing, _away));
        }

        // Cages along the back, facing the yard and the camera, and down the sides that aren't the gate's, each with what's in it.
        private void CageRows(WorldRandom r)
        {
            var rows = new List<Side>();
            foreach (Side side in new[] { Side.Back, Side.Left, Side.Right }) if (side != _gateSide) rows.Add(side);
            int wanted = r.Int(4, 7), placed = 0;
            foreach (Side side in rows)
            {
                bool along = side == Side.Back;
                float span = along ? _hx * 2f - 6f : _hz * 2f - 10f;
                float cursor = -span * 0.5f;
                while (placed < wanted && cursor < span * 0.5f)
                {
                    var (look, variants, scale, size) = Captives[Pick(r, (0, 3f), (1, 2f), (2, 2f), (3, 1.5f), (4, 1.2f))];
                    float width = size.X, depth = size.Y, height = size.Z;
                    if (cursor + width > span * 0.5f) break;
                    float mid = cursor + width * 0.5f;
                    cursor += width + r.Range(1.2f, 2.4f);
                    Vector2 inward = -Outward(side);
                    Vector2 local = side switch
                    {
                        Side.Back => new Vector2(mid, -_hz + 1.6f + depth * 0.5f),
                        Side.Left => new Vector2(-_hx + 1.6f + depth * 0.5f, mid + 1f),
                        _ => new Vector2(_hx - 1.6f - depth * 0.5f, mid + 1f),
                    };
                    if (Cage(local, inward, width, depth, height, look + r.Int(variants), scale * r.Range(0.95f, 1.08f))) placed++;
                }
            }
        }

        private bool Cage(Vector2 local, Vector2 inward, float width, float depth, float height, string look, float scale)
        {
            Vector2 at = W(local);
            float yaw = YawOf(inward);
            Vector2 ax = new Vector2(MathF.Cos(yaw), -MathF.Sin(yaw)), az = new Vector2(MathF.Sin(yaw), MathF.Cos(yaw));
            Footprint whole = Footprint.Box(at, new Vector2(width * 0.5f, depth * 0.5f), yaw);
            if (!Ground(whole, 0.3f) || !Clear(whole, PlacementLayer.Built | PlacementLayer.Road, 0.6f)) return false;
            // Its four sides are solid and tall, and so is its door while it's shut: nothing gets in, and what's in it is seen through its bars.
            float hx = width * 0.5f - 0.25f, hz = depth * 0.5f - 0.25f;
            Vector2 c00 = at - ax * hx - az * hz, c10 = at + ax * hx - az * hz, c11 = at + ax * hx + az * hz, c01 = at - ax * hx + az * hz;
            foreach ((Vector2 a, Vector2 c) in new[] { (c00, c10), (c10, c11), (c11, c01), (c01, c00) }) World.Placement.Add(Footprint.Segment(a, c, 0.25f), PlacementLayer.Prop, solid: true, height: Tall);
            World.Placement.Add(whole, PlacementLayer.Keepout);
            World.Pending.Add(new PendingObject(new PlaceSpec(PlacePiece.Cage, NextSeed(), PlaceSpec.Cm(width), PlaceSpec.Cm(depth), PlaceSpec.Cm(height)).Id, at, yaw, Vector3.One, null));
            float door = Prison.DoorWidth(width);
            Vector2 hinge = at - ax * (door * 0.5f) + az * hz;
            string doorModel = new PlaceSpec(PlacePiece.CageDoor, (uint)(width * 100f) * 7u + (uint)(height * 100f), PlaceSpec.Cm(door), PlaceSpec.Cm(height)).Id;
            Vector2 captive = at - az * (depth * 0.08f);
            Vector2 out_ = at + az * (depth * 0.5f + 3.2f);
            Plan.Cages.Add(new PlaceCage(hinge, yaw, doorModel, -1.75f, at, captive, yaw, look, scale, out_, _away));
            B.Spawn(SpawnRole.Captive, captive);
            Keep(Footprint.Box(out_ - az * 1.2f, new Vector2(door * 0.5f + 0.6f, 2.4f), yaw));
            return true;
        }

        // What the wranglers work with and where they sleep: the harness racks and the collar forge by the yard, the tents along a side, the watch tower at a corner of the gate's side,
        // troughs and a rack of meat for what's in the cages, and lanterns at the yard's corners.
        private void Works(WorldRandom r)
        {
            Vector2 inward = -Outward(_gateSide);
            Vector2 gateIn = _gateLocal + inward * 4f;
            // The tower, at whichever corner of the gate's side has room.
            foreach (float s in new[] { 1f, -1f })
            {
                Vector2 corner = _gateSide is Side.Front or Side.Back ? new Vector2(s * (_hx - 4.6f), _gateLocal.Y + inward.Y * 4.6f) : new Vector2(_gateLocal.X + inward.X * 4.6f, s * (_hz - 4.6f));
                if (Structure(StructureCatalog.GuardTower, W(corner), YawOf(inward), padding: 0.4f)) break;
            }
            Vector2 side = _gateSide is Side.Front or Side.Back ? Vector2.UnitX : Vector2.UnitY;
            foreach ((PlacePiece kind, Vector2 local, Vector2 half, float height) in new[]
            {
                (PlacePiece.HarnessRack, _post + side * 9.5f + inward * 1.5f, new Vector2(2f, 0.8f), 2.4f),
                (PlacePiece.CollarForge, _post - side * 9.5f + inward * 1.5f, new Vector2(2.6f, 2.2f), 1.4f),
            })
            {
                for (int k = 0; k < 6; k++)
                {
                    Vector2 tried = local + new Vector2(r.Range(-2f, 2f), r.Range(-2f, 2f));
                    if (PutBox(new PlaceSpec(kind, NextSeed()), W(tried), YawOf(_post - tried), half, height, 0.5f, 0.4f)) break;
                }
            }
            // The wranglers' tents, beside the gate inside.
            for (int t = 0, tries = 0; t < 2 && tries < 12; tries++)
            {
                float s = t == 0 ? 1f : -1f;
                Vector2 local = gateIn + side * (s * r.Range(8f, 12f)) + inward * r.Range(1f, 4f);
                if (PutBox(new PlaceSpec(PlacePiece.Tent, NextSeed(), PlaceSpec.Cm(r.Range(4.4f, 5.6f)), 1), W(local), YawOf(_post - local), new Vector2(3.9f, 3.6f), 3.4f, 0.5f, 0.4f)) t++;
            }
            for (int k = 0; k < 2; k++) Structure(StructureCatalog.FeedTrough, W(_post + new Vector2(r.Range(-10f, 10f), r.Range(-8f, 2f))), r.Float() * MathF.Tau, 0.5f);
            Structure(StructureCatalog.MeatRack, W(_post + new Vector2(r.Range(-9f, 9f), r.Range(-9f, -4f))), YawOf(-inward), 0.5f);
            PutBox(new PlaceSpec(PlacePiece.Crates, NextSeed(), r.Int(3, 5), 1), W(gateIn + side * r.Range(-6f, 6f) + inward * 6f), r.Float() * MathF.Tau, new Vector2(2.2f, 1.3f), 1.8f, 0.4f);
            for (int k = 0; k < 3; k++)
            {
                Vector2 stake = _post + SiteBuilder.Direction(r.Float() * MathF.Tau) * r.Range(9f, 12f);
                Lay(new PlaceSpec(PlacePiece.ChainStake, NextSeed(), PlaceSpec.Cm(r.Range(2f, 4f))), W(stake), r.Float() * MathF.Tau, 0.5f);
            }
            foreach (Vector2 corner in new[] { new Vector2(-_hx + 2.2f, -_hz + 2.2f), new Vector2(_hx - 2.2f, -_hz + 2.2f), new Vector2(-_hx + 2.2f, _hz - 2.2f), new Vector2(_hx - 2.2f, _hz - 2.2f) })
            {
                PutRound(new PlaceSpec(PlacePiece.Lantern, NextSeed(), PlaceSpec.Cm(4.2f), 1), W(corner), YawOf(-corner), 0.4f, 0f, 0.2f, 0.5f);
            }
        }

        // How the place is kept (PLACES.md): the ways worn from the gate to the breaking yard and along the cages, boards over the mud, hay stacked for what's in the cages with a wagon
        // of it come in, troughs before them, the wranglers' master's tent and their table, woodpile, brazier and spears by the gate, the Reich's cressets along the cages, and the midden.
        private void Keeping(WorldRandom r)
        {
            Vector2 inward = -Outward(_gateSide);
            Vector2 side = _gateSide is Side.Front or Side.Back ? Vector2.UnitX : Vector2.UnitY;
            Trail(_gateLocal, _post, 3f, GroundPaint.Plaza, 1.6f);
            Trail(new Vector2(-_hx + 3f, -_hz + 5.4f), new Vector2(_hx - 3f, -_hz + 5.4f), 2.4f, GroundPaint.Plaza, 1.4f);
            float toPost = (_post - _gateLocal).Length;
            if (toPost > 8f) LayFlat(new PlaceSpec(PlacePiece.Duckboards, NextSeed(), PlaceSpec.Cm(MathF.Min(10f, toPost - 6f)), PlaceSpec.Cm(1.3f)), _gateLocal + (_post - _gateLocal).Normalized() * (3f + MathF.Min(10f, toPost - 6f) * 0.5f), YawOf((_post - _gateLocal).Normalized()), 5f, 0.2f);
            // Hay stacked for the grazers in the cages, and the wagon it came in on.
            for (int k = 0; k < 2; k++) Tries(10, () => PutRound(new PlaceSpec(PlacePiece.Haystack, NextSeed(), PlaceSpec.Cm(r.Range(1.2f, 1.6f))), W(new Vector2(r.Range(-_hx + 4f, _hx - 4f), -_hz + r.Range(8f, 11f))), r.Float() * MathF.Tau, 1.9f, Tall, 0.4f, 0.4f));
            Tries(10, () => PutBox(new PlaceSpec(PlacePiece.Wagon, NextSeed(), 0, 0, PlaceSpec.Cm(r.Range(3.8f, 4.6f))), W(_gateLocal + inward * r.Range(6f, 9f) + side * ((r.Chance(0.5f) ? 1f : -1f) * r.Range(5f, 8f))), YawOf(-inward) + r.Range(-0.3f, 0.3f), new Vector2(1.6f, 4f), 2.6f, 0.4f, 0.4f));
            for (int k = 0; k < 2; k++) Tries(8, () => Structure(StructureCatalog.Trough, W(new Vector2(r.Range(-_hx + 5f, _hx - 5f), -_hz + r.Range(6.5f, 7.5f))), YawOf(Vector2.UnitX), 0.3f, 0.4f));
            // The wranglers' master's tent and their corner by the gate.
            float s = r.Chance(0.5f) ? 1f : -1f;
            Vector2 corner = _gateLocal + inward * r.Range(6f, 9f) + side * (s * r.Range(9f, 13f));
            Tries(10, () => PutBox(new PlaceSpec(PlacePiece.CommandTent, NextSeed(), PlaceSpec.Cm(r.Range(5.2f, 6.4f))), W(corner + new Vector2(r.Range(-1.5f, 1.5f), r.Range(-1.5f, 1.5f))), YawOf((_post - corner).Normalized()), new Vector2(3.3f, 4.6f), 4f, 0.4f, 0.38f));
            Tries(8, () => PutBox(new PlaceSpec(PlacePiece.MessTable, NextSeed(), PlaceSpec.Cm(r.Range(2.4f, 3.2f))), W(corner - side * s * r.Range(5f, 7f) + inward * r.Range(0f, 2f)), YawOf(side), new Vector2(2.2f, 1.3f), 1.2f, 0.3f, 0.4f));
            Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Firewood, NextSeed(), PlaceSpec.Cm(r.Range(2.4f, 3.4f)), PlaceSpec.Cm(r.Range(1.1f, 1.5f))), W(corner + inward * r.Range(4f, 6f)), YawOf(-inward), new Vector2(2.2f, 1.4f), 1.6f, 0.3f, 0.4f));
            Tries(8, () => PutRound(new PlaceSpec(PlacePiece.Brazier, NextSeed(), PlaceSpec.Cm(r.Range(1f, 1.2f))), W(corner + inward * r.Range(2.5f, 3.5f) - side * s * 2.5f), 0f, 0.8f, 1.4f, 0.3f, 0.4f));
            Tries(8, () => PutRound(new PlaceSpec(PlacePiece.Brazier, NextSeed(), PlaceSpec.Cm(r.Range(1f, 1.2f))), W(_post + SiteBuilder.Direction(r.Float() * MathF.Tau) * r.Range(8.5f, 10f)), 0f, 0.8f, 1.4f, 0.3f, 0.4f));
            Tries(8, () => PutBox(new PlaceSpec(PlacePiece.WeaponRack, NextSeed(), PlaceSpec.Cm(r.Range(2.2f, 2.8f)), 1), W(corner + side * s * r.Range(4f, 5f)), YawOf(-side * s), new Vector2(1.6f, 0.6f), 2f, 0.3f, 0.4f));
            // Cressets along the cages, and the midden in a corner away from the gate.
            for (float x = -_hx + 4f; x < _hx - 3f; x += r.Range(7f, 9f)) PutRound(new PlaceSpec(PlacePiece.Torch, NextSeed(), PlaceSpec.Cm(r.Range(2.6f, 3f)), 1), W(new Vector2(x, -_hz + 6.6f)), 0f, 0.35f, 0f, 0.2f, 0.45f);
            Tries(8, () => PutRound(new PlaceSpec(PlacePiece.Midden, NextSeed(), PlaceSpec.Cm(r.Range(1.5f, 2f))), W(new Vector2(-s * (_hx - 3f), -_hz + r.Range(3f, 6f))), r.Float() * MathF.Tau, 2f, 1f, 0.3f, 0.45f));
            for (int k = 0; k < 2; k++) LayFlat(new PlaceSpec(PlacePiece.Puddle, NextSeed(), PlaceSpec.Cm(r.Range(0.8f, 1.3f))), _post + SiteBuilder.Direction(r.Float() * MathF.Tau) * r.Range(2.5f, 5f), r.Float() * MathF.Tau, 1.1f, 0.15f);
        }

        // What's underfoot: straw trodden out of the cages and round the hay, and what the breaking left in the yard.
        private void Underfoot(WorldRandom r)
        {
            for (float x = -_hx + 5f; x < _hx - 4f; x += r.Range(6f, 9f)) Strew(LitterKind.Straw, new Vector2(x, -_hz + r.Range(6f, 8f)), 2.4f, r.Int(16, 26));
            Strew(LitterKind.Straw, _post + new Vector2(r.Range(-3f, 3f), r.Range(-3f, 3f)), 2.6f, r.Int(10, 16));
            Strew(LitterKind.Gear, _post + new Vector2(r.Range(-2f, 2f), r.Range(2f, 4f)), 2f, r.Int(3, 6));
        }

        // Guards at the gate, wranglers about the yard and the cages, and what they've taken in a chest by their tents.
        private void Hold(WorldRandom r)
        {
            Vector2 inward = -Outward(_gateSide);
            Stand(SpawnRole.Guard, Around(W(_gateLocal + inward * 5f), 5f, r.Fork("gate"), 20), 2, 3.5f, r.Fork("guards"));
            Stand(SpawnRole.Raider, Around(W(_post), 13f, r.Fork("yard"), 50), r.Int(4, 6), 4.5f, r.Fork("wranglers"));
            Chest(W(_gateLocal + inward * 9f + (_gateSide is Side.Front or Side.Back ? Vector2.UnitX : Vector2.UnitY) * 9f), 14f);
        }
    }
}

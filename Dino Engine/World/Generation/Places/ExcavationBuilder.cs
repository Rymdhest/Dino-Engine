using Dino_Engine.Modelling.Procedural.Architecture.Places;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.World.Generation.Places
{
    /// <summary>
    /// Lays out the Ossuary's excavation (PLACES.md). The Ossuary found one of the great beasts in the ground and is lifting it out whole: a pit dug into the land in two steps, its sides shored
    /// with boards and a ramp down into it, and on its floor the beast as it lies in the rock, in the death pose, the head end freed and the rest still in the rock, the grid it's measured by
    /// strung over it; a tripod at the pit's edge with a jacketed bone coming up on it; round the pit the spoil heaped, the sieve, the bones jacketed and crated, and laid out on oxblood
    /// to be counted; the table where it's drawn, and the Reich's tent. What it raised stands about the pit, and its eyes are already lit.
    /// </summary>
    internal sealed class ExcavationBuilder : PlaceBuilder
    {
        private const float Step = 1.2f;            // Metres each of the pit's two steps goes down.
        private const float Bench = 1.9f;           // How wide the step round the pit's floor is.
        private Vector2 _floorHalf, _outerHalf;
        private float _top;                         // The ground's height round the pit.
        private Vector2 _rampOut;                   // Where the ramp comes up out of the pit, in the place's frame.
        private Vector2 _rampWay;                   // The way it climbs, in the place's frame.
        private Vector2 _camp;

        private ExcavationBuilder(SiteBuilder b) : base(b, "excavation") { }

        public static void Build(SiteBuilder b) => new ExcavationBuilder(b).Lay();

        private void Lay()
        {
            Plan.Yaw = MathF.PI + Random.Range(-0.2f, 0.2f);
            string species = PlaceStory.ExcavatedOf(Site.Story);
            FossilForm form = Excavation.FormOf(species);
            float length = Excavation.LengthOf(form, Random.Fork("length"));
            // The pit holds the beast with room round it to work: its spine runs along the pit's long way.
            _floorHalf = new Vector2(MathF.Min(length * 0.5f + 2.6f, Site.Radius * 0.62f), MathF.Min(length * 0.3f + 2.4f, Site.Radius * 0.44f));
            _outerHalf = _floorHalf + new Vector2(Bench);
            Plan.Half = new Vector2(Site.Radius, Site.Radius * 0.85f);
            if (!Dig(Random.Fork("pit")))
            {
                SmallDig(form, length, Random.Fork("small"));
                return;
            }
            Shore(Random.Fork("shoring"));
            LayBeast(form, length, Random.Fork("beast"));
            Works(Random.Fork("works"));
            Camping(Random.Fork("camping"));
            Hold(Random.Fork("holders"));
        }

        // The pit: down a step to a bench round it, and down another to its floor, with a ramp from the floor up across the bench to the ground on the side nearest the road. Kept clear of
        // everything else, and of standing water, which a hollow left open would take.
        private bool Dig(WorldRandom r)
        {
            // Where it was found, or as near as the ground lets a pit be dug, and smaller if the land's tight.
            Vector2 floor0 = _floorHalf;
            bool found = false;
            Footprint outer = default;
            foreach (float size in new[] { 1f, 0.86f, 0.72f })
            {
                _floorHalf = floor0 * size;
                _outerHalf = _floorHalf + new Vector2(Bench);
                for (float reach = 0f; reach <= Site.Radius * 0.4f && !found; reach += 3f)
                {
                    int ways = reach <= 0f ? 1 : 8;
                    for (int i = 0; i < ways && !found; i++)
                    {
                        Vector2 centre = Site.Position + SiteBuilder.Direction(i * MathF.Tau / 8f) * reach;
                        outer = Footprint.Box(centre, _outerHalf + new Vector2(1.2f), Plan.Yaw);
                        if (!Ground(outer, 0.34f) || !Clear(outer, PlacementLayer.Built | PlacementLayer.Road, 0.4f)) continue;
                        Plan.Centre = centre;
                        found = true;
                    }
                }
                if (found) break;
            }
            if (!found) return false;
            _top = AverageGround(outer);
            World.Pads.Add(new GroundPad(Footprint.Box(Plan.Centre, _outerHalf, Plan.Yaw), _top - Step, 0.9f));
            World.Pads.Add(new GroundPad(Footprint.Box(Plan.Centre, _floorHalf, Plan.Yaw), _top - Step * 2f, 0.7f));
            // The ramp, on whichever long side faces the road, climbing from the floor to the ground.
            Vector2 road = Plan.ToLocal(Site.Position + Site.RoadDirection);
            float side = road.Y >= 0f ? 1f : -1f;
            _rampWay = new Vector2(0f, side);
            float x = Math.Clamp(road.X * 0.3f, -_floorHalf.X + 3f, _floorHalf.X - 3f);
            float from = side * (_floorHalf.Y - 2.2f), to = side * (_outerHalf.Y + 1.8f);
            float rampLength = MathF.Abs(to - from);
            Vector2 mid = new Vector2(x, (from + to) * 0.5f);
            Vector2 riseWorld = Plan.Dir(_rampWay) * (Step * 2f / rampLength);
            World.Pads.Add(new GroundPad(Footprint.Box(W(mid), new Vector2(1.7f, rampLength * 0.5f), Plan.Yaw), _top - Step, 0.5f, riseWorld));
            _rampOut = new Vector2(x, to);
            World.Placement.Add(Footprint.Box(Plan.Centre, _outerHalf + new Vector2(0.6f), Plan.Yaw), PlacementLayer.Keepout);
            Paint(GroundPaint.Yard, Footprint.Box(Plan.Centre, _outerHalf + new Vector2(5f), Plan.Yaw), 5f);
            // The pit itself is turned earth, wet where it's deepest, that nothing grows in: the bones show pale against it.
            Paint(GroundPaint.Mud, Footprint.Box(Plan.Centre, _outerHalf, Plan.Yaw), 0.8f);
            Plan.Marks.Add(new PlaceMark(PlaceMarkKind.Pit, Plan.Centre, Plan.Yaw, _outerHalf));
            _camp = _rampOut + _rampWay * 9f;
            return true;
        }

        // Boards shoring both steps' sides, all round but where the ramp goes through.
        private void Shore(WorldRandom r)
        {
            foreach ((Vector2 half, float level) in new[] { (_floorHalf, _top - Step), (_outerHalf, _top) })
            {
                foreach (Side side in new[] { Side.Back, Side.Front, Side.Left, Side.Right })
                {
                    bool alongX = side is Side.Back or Side.Front;
                    float span = alongX ? half.X * 2f : half.Y * 2f;
                    Vector2 inward = -Outward(side);
                    Vector2 middle = side switch
                    {
                        Side.Back => new Vector2(0f, -half.Y),
                        Side.Front => new Vector2(0f, half.Y),
                        Side.Left => new Vector2(-half.X, 0f),
                        _ => new Vector2(half.X, 0f),
                    };
                    // Laid in lengths, leaving the ramp's way open.
                    int pieces = Math.Max(1, (int)MathF.Ceiling(span / 6f));
                    for (int k = 0; k < pieces; k++)
                    {
                        float a = -span * 0.5f + span * k / pieces, c = -span * 0.5f + span * (k + 1) / pieces;
                        Vector2 along = alongX ? Vector2.UnitX : Vector2.UnitY;
                        Vector2 centre = middle + along * ((a + c) * 0.5f) + inward * 0.06f;
                        bool ramp = (side == Side.Front && _rampWay.Y > 0f || side == Side.Back && _rampWay.Y < 0f) && MathF.Abs(centre.X - _rampOut.X) < (c - a) * 0.5f + 1.9f;
                        if (ramp) continue;
                        // Boards of a length are the same boards, as a dig's carpenter cut them by the dozen: one model a length, whichever dig they're in.
                        int cm = (int)MathF.Round((c - a + 0.1f) * 4f) * 25;
                        World.Pending.Add(new PendingObject(new PlaceSpec(PlacePiece.Revetment, (uint)cm * 2654435761u, cm, PlaceSpec.Cm(Step)).Id, W(centre), YawOf(inward), Vector3.One, level));
                    }
                }
            }
        }

        // The beast on the pit's floor, its spine along the pit, freed from its head back, and the grid strung over the end that's being worked.
        private void LayBeast(FossilForm form, float length, WorldRandom r)
        {
            float freed = r.Range(0.45f, 0.85f);
            float floor = _top - Step * 2f;
            // Its spine runs along the pit's long way, the model's z to the place's x, its head towards whichever end is further from the ramp.
            float headWay = _rampOut.X > 0f ? -1f : 1f;
            Vector2 local = new Vector2(-headWay * length * 0.06f, r.Range(-0.8f, 0.8f));
            float yaw = YawOf(new Vector2(headWay, 0f));
            Vector2 at = W(local);
            Footprint core = Footprint.Box(at, new Vector2(length * 0.12f, length * 0.32f), yaw);
            World.Placement.Add(core, PlacementLayer.Prop, solid: true, height: 0.9f);
            World.Pending.Add(new PendingObject(new PlaceSpec(PlacePiece.Fossil, NextSeed(), (int)form, PlaceSpec.Cm(length), (int)MathF.Round(freed * 100f)).Id, at, yaw, Vector3.One, floor));
            // The grid over the freed end.
            Vector2 grid = local + new Vector2(headWay * length * 0.22f, 0f);
            float gw = MathF.Min(length * 0.42f, _floorHalf.X), gd = MathF.Min(length * 0.36f, _floorHalf.Y * 1.6f);
            World.Pending.Add(new PendingObject(new PlaceSpec(PlacePiece.DigGrid, NextSeed(), PlaceSpec.Cm(gw), PlaceSpec.Cm(gd)).Id, W(grid), Plan.Yaw, Vector3.One, floor));
        }

        // Round the pit: the tripod at its edge over the beast, the spoil heaped on the far sides, and by the ramp's top the camp: the sieve, the jackets and the crates, what's been taken
        // out laid on oxblood to be counted, the drawing table, the Reich's tent and its fire, and lanterns round the rim, as the Ossuary works by night.
        private void Works(WorldRandom r)
        {
            // The tripod, on the bench's edge over the pit, its legs posts in nobody's way in the air.
            Vector2 tripod = new Vector2(r.Range(-_floorHalf.X * 0.5f, _floorHalf.X * 0.5f), -_rampWay.Y * (_floorHalf.Y + Bench * 0.5f));
            World.Pending.Add(new PendingObject(new PlaceSpec(PlacePiece.Hoist, NextSeed(), PlaceSpec.Cm(r.Range(6.5f, 8f))).Id, W(tripod), Plan.Yaw + r.Range(-0.5f, 0.5f), Vector3.One, _top - Step));
            // Spoil, heaped on the sides away from the camp.
            for (int k = 0, placed = 0; k < 12 && placed < 3; k++)
            {
                Side side = (Side)r.Int(4);
                if (side == (_rampWay.Y > 0f ? Side.Front : Side.Back)) continue;
                Vector2 outward = Outward(side);
                Vector2 along = new Vector2(outward.Y, outward.X);
                Vector2 local = outward * ((side is Side.Left or Side.Right ? _outerHalf.X : _outerHalf.Y) + 4.5f) + along * r.Range(-6f, 6f);
                float length = r.Range(5f, 8f);
                if (PutBox(new PlaceSpec(PlacePiece.Spoil, NextSeed(), PlaceSpec.Cm(length)), W(local), YawOf(along) - MathF.PI * 0.5f, new Vector2(length * 0.55f, length * 0.25f + 1f), 1.4f, 0.4f, 0.45f)) placed++;
            }
            // The camp at the ramp's top.
            Vector2 across = new Vector2(1f, 0f);
            var pieces = new (PlacePiece Kind, Vector2 Offset, Vector2 Half, float Height, int Count)[]
            {
                (PlacePiece.Sieve, across * -6f + _rampWay * -2f, new Vector2(1.4f, 1.1f), 1.3f, 0),
                (PlacePiece.Jackets, across * 6f + _rampWay * -1.5f, new Vector2(1.8f, 1.2f), 1.2f, r.Int(2, 4)),
                (PlacePiece.BoneCrates, across * 9f + _rampWay * 3f, new Vector2(2.2f, 1f), 1.6f, 0),
                (PlacePiece.DrawingTable, across * -8f + _rampWay * 4f, new Vector2(2.4f, 1.6f), 1.4f, 0),
                (PlacePiece.Tent, across * 0f + _rampWay * 7f, new Vector2(3.8f, 3.6f), 3.4f, 0),
                (PlacePiece.Campfire, across * 4f + _rampWay * 2f, new Vector2(1.4f, 1.4f), 0.8f, 0),
            };
            foreach ((PlacePiece kind, Vector2 offset, Vector2 half, float height, int count) in pieces)
            {
                for (int attempt = 0; attempt < 6; attempt++)
                {
                    Vector2 local = _camp + offset + new Vector2(r.Range(-1.5f, 1.5f), r.Range(-1.5f, 1.5f)) * attempt * 0.6f;
                    float yaw = YawOf(_rampWay * -1f) + r.Range(-0.3f, 0.3f);
                    var spec = kind switch
                    {
                        PlacePiece.Jackets => new PlaceSpec(kind, NextSeed(), count),
                        PlacePiece.Tent => new PlaceSpec(kind, NextSeed(), PlaceSpec.Cm(r.Range(4.6f, 5.6f)), 1),
                        _ => new PlaceSpec(kind, NextSeed()),
                    };
                    if (PutBox(spec, W(local), yaw, half, height, 0.4f, 0.4f)) break;
                }
            }
            // What's been taken out, laid on oxblood to be counted, as at every dig of the Ossuary's.
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector2 local = _camp + across * r.Range(-4f, 4f) + _rampWay * r.Range(-4f, -1f);
                if (Structure(StructureCatalog.BoneRows, W(local), YawOf(_rampWay), 0.4f, 0.4f)) break;
            }
            Structure(StructureCatalog.Barrow, W(_camp + across * r.Range(-3f, 3f) + _rampWay * -3.5f), r.Float() * MathF.Tau, 0.3f);
            // Lanterns round the rim.
            foreach (Vector2 corner in new[] { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f) })
            {
                Vector2 local = corner * (_outerHalf + new Vector2(1.6f));
                PutRound(new PlaceSpec(PlacePiece.Lantern, NextSeed(), PlaceSpec.Cm(4f), 1), W(local), YawOf(-corner), 0.4f, 0f, 0.2f, 0.5f);
            }
        }

        // Ground too broken for a pit of any size: the Ossuary has only begun, with a trench of its own (the dig every site of its has) and its camp beside it, and what it's raised so far.
        // Where the ground wouldn't take a pit, a dig only just begun: the beast coming out of a shallow scrape at the ground's own height, less of it freed, the grid over it and the camp
        // round it as at any dig. Only if there's no room even for that does it fall back to a dig of the old kind.
        private void SmallDig(FossilForm form, float length, WorldRandom r)
        {
            _rampWay = Vector2.UnitY;
            _camp = new Vector2(0f, 10f);
            float yaw = YawOf(Vector2.UnitX);
            for (float scale = 1f; scale >= 0.6f; scale -= 0.2f)
            {
                float l = MathF.Max(length * scale, 4f);
                for (float reach = 0f; reach <= Site.Radius * 0.5f; reach += 3f)
                {
                    for (int k = 0; k < (reach <= 0f ? 1 : 10); k++)
                    {
                        Vector2 at = Site.Position + SiteBuilder.Direction(k * MathF.Tau / 10f + r.Range(-0.2f, 0.2f)) * reach;
                        Footprint scrape = Footprint.Box(at, new Vector2(l * 0.5f + 1.5f, l * 0.3f + 1.5f), Plan.Yaw);
                        if (!Ground(scrape, 0.25f) || !Clear(scrape, PlacementLayer.Built | PlacementLayer.Road | PlacementLayer.Tree, 0.6f)) continue;
                        Plan.Centre = at;
                        // The scrape is at the ground's own height, which is where the tripod stands (Works stands it a step under the ground round a pit).
                        _top = AverageGround(scrape) + Step;
                        _floorHalf = new Vector2(l * 0.5f + 1.5f, l * 0.3f + 1.5f);
                        _outerHalf = _floorHalf + new Vector2(2f);
                        _rampOut = new Vector2(0f, _floorHalf.Y + 2f);
                        Keep(scrape);
                        Paint(GroundPaint.Mud, scrape, 1.5f);
                        Paint(GroundPaint.Yard, Footprint.Circle(at, MathF.Max(_floorHalf.X, _floorHalf.Y) + 6f), 4f);
                        World.Placement.Add(Footprint.Box(at, new Vector2(l * 0.32f, l * 0.12f), Plan.Yaw), PlacementLayer.Prop, solid: true, height: 0.9f);
                        World.Pending.Add(new PendingObject(new PlaceSpec(PlacePiece.Fossil, NextSeed(), (int)form, PlaceSpec.Cm(l), (int)MathF.Round(r.Range(0.3f, 0.55f) * 100f)).Id, at, yaw, Vector3.One, null));
                        World.Pending.Add(new PendingObject(new PlaceSpec(PlacePiece.DigGrid, NextSeed(), PlaceSpec.Cm(MathF.Min(l * 0.42f, _floorHalf.X)), PlaceSpec.Cm(MathF.Min(l * 0.36f, _floorHalf.Y * 1.6f))).Id, at + Plan.Ax * (l * 0.2f), Plan.Yaw, Vector3.One, null));
                        Plan.Marks.Add(new PlaceMark(PlaceMarkKind.Pit, at, Plan.Yaw, _floorHalf));
                        Works(r.Fork("works"));
                        Hold(r.Fork("holders"));
                        return;
                    }
                }
            }
            _floorHalf = new Vector2(4f, 3f);
            _outerHalf = _floorHalf + new Vector2(Bench);
            _rampOut = new Vector2(0f, 4f);
            B.PlaceNear(StructureCatalog.Dig, Site.Position, Plan.Yaw + MathF.PI * 0.5f, Site.Radius * 0.6f, out Vector2 dig, padding: 0.6f);
            Plan.Centre = dig;
            _top = World.Elevation.Sample(dig) + Step;
            Paint(GroundPaint.Yard, Footprint.Circle(dig, 9f), 4f);
            Works(r.Fork("works"));
            Hold(r.Fork("holders"));
        }

        // What the Ossuary raised, on the pit's floor and its bench; greycoats at the camp; and the chest by the drawing table.
        // The Ossuary's camp about the dig as it works by night (PLACES.md): its cold lamps round the rim, the tents of the diggers who aren't dead, the wagon of bone crates for the coast,
        // the barrows and spades left by the spoil, more of what's come up laid out to be counted, and the ways worn from the camp down the ramp and round the rim.
        private void Camping(WorldRandom r)
        {
            Vector2 across = new Vector2(-_rampWay.Y, _rampWay.X);
            Trail(_camp, _rampOut, 3f, GroundPaint.Yard, 1.6f);
            // Its cold lamps at the rim's corners and by the ramp.
            foreach (Vector2 corner in new[] { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f) })
            {
                Vector2 at = corner * (_outerHalf + new Vector2(1.2f));
                PutRound(new PlaceSpec(PlacePiece.Torch, NextSeed(), PlaceSpec.Cm(r.Range(2.6f, 3f)), 4), W(at), YawOf(-corner), 0.35f, 0f, 0.2f, 0.5f);
            }
            foreach (float s in new[] { -1f, 1f }) PutRound(new PlaceSpec(PlacePiece.Torch, NextSeed(), PlaceSpec.Cm(r.Range(2.4f, 2.8f)), 4), W(_rampOut + across * (s * 2.6f) + _rampWay * 1f), 0f, 0.35f, 0f, 0.2f, 0.5f);
            // The living diggers' tents, and the wagon to take the crates to the coast.
            for (int k = 0, tries = 0; k < 2 && tries < 14; tries++)
            {
                Vector2 at = _camp + across * ((k == 0 ? 1f : -1f) * r.Range(7f, 10f)) + _rampWay * r.Range(0f, 4f);
                if (PutBox(new PlaceSpec(PlacePiece.Tent, NextSeed(), PlaceSpec.Cm(r.Range(4.2f, 5f)), 1), W(at), YawOf(-across * (k == 0 ? 1f : -1f)), new Vector2(3.8f, 3.4f), 3.4f, 0.4f, 0.4f)) k++;
            }
            Tries(10, () => PutBox(new PlaceSpec(PlacePiece.Wagon, NextSeed(), 0, 1, PlaceSpec.Cm(r.Range(3.8f, 4.6f))), W(_camp + _rampWay * r.Range(5f, 8f) + across * r.Range(-4f, 4f)), YawOf(_rampWay) + r.Range(-0.4f, 0.4f), new Vector2(1.6f, 4f), 2.6f, 0.4f, 0.4f));
            for (int k = 0; k < 2; k++) Tries(8, () => PutBox(new PlaceSpec(PlacePiece.BoneCrates, NextSeed()), W(_camp + across * r.Range(-6f, 6f) + _rampWay * r.Range(2f, 5f)), r.Float() * MathF.Tau, new Vector2(1.6f, 1.2f), 1.4f, 0.3f, 0.4f));
            Tries(8, () => Structure(StructureCatalog.BoneRows, W(_camp + across * r.Range(-5f, 5f) + _rampWay * r.Range(-1f, 2f)), YawOf(across), 0.3f, 0.4f));
            for (int k = 0; k < 2; k++) Tries(6, () => Structure(StructureCatalog.Barrow, W(_rampOut + across * ((r.Chance(0.5f) ? 1f : -1f) * r.Range(3.5f, 6f)) + _rampWay * r.Range(0f, 2f)), r.Float() * MathF.Tau, 0.3f));
            Tries(8, () => PutRound(new PlaceSpec(PlacePiece.Brazier, NextSeed(), PlaceSpec.Cm(r.Range(1f, 1.2f))), W(_camp + across * r.Range(-3f, 3f) + _rampWay * r.Range(3f, 5f)), 0f, 0.8f, 1.4f, 0.3f, 0.4f));
            // What lies about: spoil off the barrows, bones by the crates, the drawing table's papers.
            Strew(LitterKind.Bones, _camp + _rampWay * 2f, 2.6f, r.Int(8, 14));
            Strew(LitterKind.Papers, _camp + across * r.Range(-3f, 3f), 2f, r.Int(5, 8));
            for (int k = 0; k < 3; k++) Strew(LitterKind.Spalls, new Vector2((_outerHalf.X + 2f) * (r.Chance(0.5f) ? 1f : -1f), r.Range(-_outerHalf.Y, _outerHalf.Y)), 2f, r.Int(10, 16), PlaceMaterials.StoneOf(World.Geology));
        }

        private void Hold(WorldRandom r)
        {
            var floor = new List<Vector2>();
            for (int k = 0; k < 40; k++) floor.Add(W(new Vector2(r.Range(-_floorHalf.X + 1.5f, _floorHalf.X - 1.5f), r.Range(-_floorHalf.Y + 1.5f, _floorHalf.Y - 1.5f))));
            Stand(SpawnRole.Undead, floor, r.Int(2, 3), 4f, r.Fork("floor"));
            var rim = new List<Vector2>();
            for (int k = 0; k < 40; k++)
            {
                float a = r.Float() * MathF.Tau;
                rim.Add(W(new Vector2(MathF.Cos(a) * (_outerHalf.X + 3.5f), MathF.Sin(a) * (_outerHalf.Y + 3.5f))));
            }
            Stand(SpawnRole.Undead, rim, r.Int(2, 3), 5f, r.Fork("rim"));
            Stand(SpawnRole.Guard, Around(W(_camp), 6f, r.Fork("camp"), 30), 2, 4f, r.Fork("guards"));
            Chest(W(_camp + new Vector2(-6f, 2f)), 10f);
        }
    }
}

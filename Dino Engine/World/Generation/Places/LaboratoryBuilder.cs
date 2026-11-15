using Dino_Engine.Modelling.Procedural.Architecture.Places;
using Dino_Engine.Modelling.Procedural.Fence;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.World.Generation.Places
{
    /// <summary>
    /// Lays out an outstation of the Amber Institute (PLACES.md). Inside the Reich's palisade, its banners at the gate where the road comes, the blood-doctors' house stands across the back
    /// with its door and its lit windows to the yard and the camera, a paved forecourt before it; along one side a glasshouse where the eggs are warmed, along the other a row of vats of the
    /// growing broth piped into the house, and under a shelter the racks of jars and the benches where amber is cut; in a back corner, behind a fence of its own, the pen where what failed is
    /// kept until the Ossuary takes it, and the failures still in it. Greycoats keep the gate and the yard.
    /// </summary>
    internal sealed class LaboratoryBuilder : PlaceBuilder
    {
        private Vector2 _half;
        private Side _gateSide;
        private Vector2 _gate;
        private Vector2 _door;
        private float _hallHalfDepth;

        private LaboratoryBuilder(SiteBuilder b) : base(b, "laboratory") { }

        public static void Build(SiteBuilder b) => new LaboratoryBuilder(b).Lay();

        private void Lay()
        {
            Plan.Yaw = MathF.PI + Random.Range(-0.1f, 0.1f);
            _half = new Vector2(Site.Radius * Random.Range(0.84f, 0.92f), Site.Radius * Random.Range(0.72f, 0.8f));
            Plan.Half = _half;
            (_gateSide, _gate) = GateOnRoad(_half);
            Enclose(FenceUse.Pen, _half, _gateSide, _gate, 4.8f);
            ReichGate(_gateSide, _gate, 4.8f, Random.Fork("gate"));
            // Its yard kept as the Institute keeps everything: gravelled, and not let go to grass.
            Paint(GroundPaint.Plaza, Footprint.Box(Plan.Centre, _half - new Vector2(1.5f), Plan.Yaw), 3f);
            House(Random.Fork("house"));
            Hatchery(Random.Fork("hatchery"));
            Vats(Random.Fork("vats"));
            Workshop(Random.Fork("workshop"));
            Failures(Random.Fork("failures"));
            Yard(Random.Fork("yard"));
            Works(Random.Fork("works"));
            Hold(Random.Fork("holders"));
            Underfoot(Random.Fork("underfoot"));
        }

        // The blood-doctors' house across the back, its front and its door to the yard, the ground levelled under it and a paved forecourt before its door.
        private void House(WorldRandom r)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                float w = MathF.Min(r.Range(14f, 19f), _half.X * 1.3f), d = r.Range(9.5f, 12f);
                Vector2 local = new Vector2(r.Range(-2f, 2f), -_half.Y + d * 0.5f + 2.8f + attempt * 0.5f);
                // The back's the gate's side only when the road comes from behind: then the house turns to face it.
                if (_gateSide == Side.Back) local.Y = _half.Y - d * 0.5f - 2.8f;
                Vector2 facing = _gateSide == Side.Back ? -Vector2.UnitY : Vector2.UnitY;
                Vector2 at = W(local);
                float yaw = YawOf(facing);
                Footprint f = Footprint.Box(at, new Vector2(w * 0.5f + 0.4f, d * 0.5f + 0.4f), yaw);
                if (!Ground(f, 0.3f) || !Clear(f, PlacementLayer.Built | PlacementLayer.Road, 0.8f)) continue;
                float floor = AverageGround(f);
                World.Placement.Add(f, PlacementLayer.Structure, solid: true, height: Tall);
                World.Pads.Add(new GroundPad(f.Grown(0.6f), floor, 4f));
                World.Pending.Add(new PendingObject(new PlaceSpec(PlacePiece.LabHall, NextSeed(), PlaceSpec.Cm(w), PlaceSpec.Cm(d)).Id, at, yaw, Vector3.One, floor));
                _door = local + facing * (d * 0.5f + 2.6f);
                _hallHalfDepth = d * 0.5f;
                Paint(GroundPaint.Plaza, Footprint.Box(W(local + facing * (d * 0.5f + 3.5f)), new Vector2(w * 0.35f, 3.5f), yaw), 1.5f);
                Keep(Footprint.Box(W(_door), new Vector2(3.4f, 2.4f), yaw));
                return;
            }
            _door = Vector2.Zero;
        }

        // The glasshouse along one side, its long way along it and its door to the yard.
        private Side _hatchSide = Side.Left;

        private void Hatchery(WorldRandom r)
        {
            foreach (Side side in r.Chance(0.5f) ? new[] { Side.Left, Side.Right } : new[] { Side.Right, Side.Left })
            {
                if (side == _gateSide) continue;
                float l = MathF.Min(r.Range(13f, 18f), _half.Y * 1.5f), w = r.Range(6f, 7.5f);
                float x = side == Side.Left ? -_half.X + w * 0.5f + 2f : _half.X - w * 0.5f - 2f;
                Vector2 local = new Vector2(x, r.Range(-2f, 3f));
                Vector2 inward = side == Side.Left ? Vector2.UnitX : -Vector2.UnitX;
                // Its front, and so its door, to the yard: its long way along the side.
                float yaw = YawOf(inward);
                Vector2 at = W(local);
                Footprint f = Footprint.Box(at, new Vector2(l * 0.5f + 0.3f, w * 0.5f + 0.3f), yaw);
                if (!Ground(f, 0.3f) || !Clear(f, PlacementLayer.Built | PlacementLayer.Road, 0.6f)) continue;
                float floor = AverageGround(f);
                World.Placement.Add(f, PlacementLayer.Structure, solid: true, height: Tall);
                World.Pads.Add(new GroundPad(f.Grown(0.4f), floor, 3f));
                World.Pending.Add(new PendingObject(new PlaceSpec(PlacePiece.Hatchery, NextSeed(), PlaceSpec.Cm(l), PlaceSpec.Cm(w)).Id, at, yaw, Vector3.One, floor));
                _hatchSide = side;
                return;
            }
        }

        // A row of vats along the other side, each piped into the house.
        private void Vats(WorldRandom r)
        {
            Side side = _hatchSide == Side.Left ? Side.Right : Side.Left;
            if (side == _gateSide) side = Side.Back;
            int vats = r.Int(2, 4);
            float x = side == Side.Left ? -_half.X + 4.2f : side == Side.Right ? _half.X - 4.2f : 0f;
            for (int k = 0, placed = 0; k < vats * 3 && placed < vats; k++)
            {
                float z = side == Side.Back ? -_half.Y + 4.2f : -_half.Y + _hallHalfDepth * 2f + 6f + placed * 6.4f + r.Range(-0.6f, 0.6f);
                Vector2 local = side == Side.Back ? new Vector2(-_half.X * 0.6f + placed * 6.4f, z) : new Vector2(x + r.Range(-0.6f, 0.6f), z);
                float height = r.Range(5f, 6.8f);
                float radius = height * 0.34f + 0.7f;
                if (!PutRound(new PlaceSpec(PlacePiece.Vat, NextSeed(), PlaceSpec.Cm(height)), W(local), YawOf(-local), radius, Tall, 0.6f, 0.35f)) continue;
                placed++;
                // Its pipe, back towards the house.
                Vector2 toHouse = new Vector2(_door.X, -_half.Y + _hallHalfDepth) - local;
                float length = MathF.Min(toHouse.Length - radius - 1f, 14f);
                if (length < 2f) continue;
                Vector2 from = local + toHouse.Normalized() * (radius + 0.3f);
                Vector2 to = from + toHouse.Normalized() * length;
                Footprint pipe = Footprint.Segment(W(from), W(to), 0.45f);
                if (!Clear(pipe, PlacementLayer.Structure | PlacementLayer.Prop | PlacementLayer.Road, 0.2f)) continue;
                World.Placement.Add(pipe, PlacementLayer.Prop, solid: true, height: 0.9f);
                World.Pending.Add(new PendingObject(new PlaceSpec(PlacePiece.Pipes, NextSeed(), PlaceSpec.Cm(length)).Id, W(from), YawOf(toHouse) - MathF.PI * 0.5f, Vector3.One, null));
            }
        }

        // The racks of jars and the amber benches, under a shelter in the yard's open corner nearest the gate.
        private void Workshop(WorldRandom r)
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                Vector2 local = new Vector2((r.Chance(0.5f) ? -1f : 1f) * r.Range(_half.X * 0.2f, _half.X * 0.55f), r.Range(-_half.Y * 0.1f, _half.Y * 0.5f));
                float w = r.Range(7f, 9f), d = r.Range(4.5f, 5.5f);
                Vector2 facing = (_door - local).LengthSquared > 1f ? (_door - local).Normalized() : Vector2.UnitY;
                float yaw = YawOf(facing);
                Vector2 at = W(local);
                if (!PutBox(new PlaceSpec(PlacePiece.Shelter, NextSeed(), PlaceSpec.Cm(w), PlaceSpec.Cm(d)), at, yaw, new Vector2(w * 0.5f + 0.4f, d * 0.5f + 0.4f), Tall, 0.5f, 0.3f)) continue;
                // Under it, against its back, the jars and the bench.
                Vector2 ax = new Vector2(MathF.Cos(yaw), -MathF.Sin(yaw)), az = new Vector2(MathF.Sin(yaw), MathF.Cos(yaw));
                World.Pending.Add(new PendingObject(new PlaceSpec(PlacePiece.Specimens, NextSeed()).Id, at - az * (d * 0.5f - 0.9f) - ax * (w * 0.22f), yaw, Vector3.One, null));
                World.Pending.Add(new PendingObject(new PlaceSpec(PlacePiece.AmberBench, NextSeed()).Id, at - az * (d * 0.5f - 1.1f) + ax * (w * 0.24f), yaw, Vector3.One, null));
                return;
            }
        }

        // What failed: kept in a pen of its own in a back corner, behind the Reich's fence, the gate of it open to the yard.
        private Vector2 _pen;
        private bool _hasPen;

        private void Failures(WorldRandom r)
        {
            foreach (float s in r.Chance(0.5f) ? new[] { -1f, 1f } : new[] { 1f, -1f })
            {
                Vector2 half = new Vector2(r.Range(6.5f, 8f), r.Range(5.5f, 7f));
                Vector2 local = new Vector2(s * (_half.X - half.X - 1.2f), _gateSide == Side.Back ? _half.Y - half.Y - 1.2f : -_half.Y + half.Y + 1.2f);
                Footprint f = Footprint.Box(W(local), half, Plan.Yaw);
                if (!Ground(f, 0.35f) || !Clear(f, PlacementLayer.Built | PlacementLayer.Road, 0.4f)) continue;
                // Its fence on the two sides that aren't the outer wall, its gate in the one towards the yard.
                Vector2 inX = new Vector2(-s, 0f), inZ = _gateSide == Side.Back ? -Vector2.UnitY : Vector2.UnitY;
                Vector2 cornerIn = local + new Vector2(-s * half.X, inZ.Y * half.Y);
                Vector2 sideEnd = local + new Vector2(-s * half.X, -inZ.Y * half.Y);
                Vector2 frontEnd = local + new Vector2(s * half.X, inZ.Y * half.Y);
                B.Wall(FenceUse.Pen, W(sideEnd), W(cornerIn), W(local));
                Vector2 along = (frontEnd - cornerIn).Normalized();
                float gap = (frontEnd - cornerIn).Length * 0.5f;
                B.Wall(FenceUse.Pen, W(cornerIn), W(cornerIn + along * (gap - 2.6f)), W(local));
                B.Wall(FenceUse.Pen, W(cornerIn + along * (gap + 2.6f)), W(frontEnd), W(local));
                Paint(GroundPaint.Mud, f, 1.5f);
                World.Placement.Add(f.Grown(-0.5f), PlacementLayer.Keepout);
                // Straw, a trough, and what's left of what they're fed.
                Lay(new PlaceSpec(PlacePiece.ChainStake, NextSeed(), PlaceSpec.Cm(2.5f)), W(local + new Vector2(r.Range(-2f, 2f), r.Range(-2f, 2f))), r.Float() * MathF.Tau, 0.5f);
                _pen = local;
                _hasPen = true;
                return;
            }
        }

        // What stands about the yard: crates of what's brought in, stamped with the mark, a wagon, and the Reich's lanterns at its corners.
        private void Yard(WorldRandom r)
        {
            Vector2 inward = -Outward(_gateSide);
            Vector2 across = new Vector2(inward.Y, -inward.X);
            for (int k = 0; k < 2; k++) PutBox(new PlaceSpec(PlacePiece.Crates, NextSeed(), r.Int(3, 5), 1), W(_gate + inward * r.Range(6f, 9f) + across * ((k == 0 ? 1f : -1f) * r.Range(5f, 8f))), r.Float() * MathF.Tau, new Vector2(2.2f, 1.3f), 1.8f, 0.4f);
            Structure(StructureCatalog.Cart, W(_gate + inward * 10f + across * r.Range(-6f, 6f)), YawOf(inward) + r.Range(-0.4f, 0.4f), 0.5f);
            foreach (Vector2 corner in new[] { new Vector2(-_half.X + 2.2f, -_half.Y + 2.2f), new Vector2(_half.X - 2.2f, -_half.Y + 2.2f), new Vector2(-_half.X + 2.2f, _half.Y - 2.2f), new Vector2(_half.X - 2.2f, _half.Y - 2.2f) })
            {
                PutRound(new PlaceSpec(PlacePiece.Lantern, NextSeed(), PlaceSpec.Cm(4.2f), 1), W(corner), YawOf(-corner), 0.4f, 0f, 0.2f, 0.5f);
            }
        }

        // What keeps it working (PLACES.md): the ways worn from the gate to the house's door and out to its works, the boiler that keeps the broth warm smoking by the house, amber
        // come in from the diggings in its split chests, eggs crated for the farms and the wagon to take them, the greycoats' table, brazier and spears by the gate, the Reich's lamps
        // along the way in, and what it throws out heaped by the failures' pen.
        private void Works(WorldRandom r)
        {
            Vector2 inward = -Outward(_gateSide);
            Vector2 across = new Vector2(inward.Y, -inward.X);
            float front = _gateSide == Side.Back ? -1f : 1f;
            Trail(_gate, _door, 3.2f, GroundPaint.Plaza, 1.6f);
            Trail(_door + new Vector2(-6f, 0f), _door + new Vector2(6f, 0f), 2.4f, GroundPaint.Plaza, 1.4f);
            // The boiler beside the house, its chimney up by the house's own.
            foreach (float s in r.Chance(0.5f) ? new[] { -1f, 1f } : new[] { 1f, -1f })
            {
                if (Tries(6, () => PutBox(new PlaceSpec(PlacePiece.Boiler, NextSeed()), W(new Vector2(_door.X + s * r.Range(10f, 13f), _door.Y - front * r.Range(0.5f, 2.5f))), YawOf(new Vector2(0f, front)), new Vector2(2.6f, 1.6f), Tall, 0.4f, 0.36f))) break;
            }
            // Amber in its split chests, and eggs crated for the farms with the wagon that takes them.
            for (int k = 0; k < 2; k++) Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Cargo, NextSeed(), 2, r.Int(2, 3), 1), W(_door + new Vector2(r.Range(-8f, 8f), front * r.Range(4f, 7f))), r.Float() * MathF.Tau, new Vector2(1.8f, 1.4f), 1.4f, 0.3f, 0.4f));
            Tries(10, () => PutBox(new PlaceSpec(PlacePiece.Wagon, NextSeed(), 0, 1, PlaceSpec.Cm(r.Range(3.8f, 4.6f))), W(_gate + inward * r.Range(7f, 10f) + across * ((r.Chance(0.5f) ? 1f : -1f) * r.Range(4f, 7f))), YawOf(-inward) + r.Range(-0.3f, 0.3f), new Vector2(1.6f, 4f), 2.6f, 0.4f, 0.4f));
            // The greycoats' corner by the gate.
            float side = r.Chance(0.5f) ? 1f : -1f;
            Vector2 mess = _gate + inward * r.Range(5f, 7f) + across * (side * r.Range(8f, 11f));
            Tries(8, () => PutBox(new PlaceSpec(PlacePiece.MessTable, NextSeed(), PlaceSpec.Cm(r.Range(2.4f, 3.2f))), W(mess + new Vector2(r.Range(-1f, 1f), r.Range(-1f, 1f))), YawOf(across), new Vector2(2.2f, 1.3f), 1.2f, 0.3f, 0.4f));
            Tries(8, () => PutRound(new PlaceSpec(PlacePiece.Brazier, NextSeed(), PlaceSpec.Cm(r.Range(1f, 1.2f))), W(mess + inward * r.Range(2.5f, 3.5f)), 0f, 0.8f, 1.4f, 0.3f, 0.4f));
            Tries(8, () => PutBox(new PlaceSpec(PlacePiece.WeaponRack, NextSeed(), PlaceSpec.Cm(r.Range(2f, 2.6f)), 1), W(mess - across * side * r.Range(3f, 4f)), YawOf(inward), new Vector2(1.5f, 0.6f), 2f, 0.3f, 0.4f));
            // The Reich's lamps along the way in.
            Vector2 way = (_door - _gate).LengthSquared > 1f ? (_door - _gate).Normalized() : inward;
            Vector2 aside = new Vector2(-way.Y, way.X);
            float length = (_door - _gate).Length;
            for (float t = 5f; t < length - 3f; t += r.Range(6f, 8f))
            {
                foreach (float s in new[] { -1f, 1f }) PutRound(new PlaceSpec(PlacePiece.Lantern, NextSeed(), PlaceSpec.Cm(r.Range(3.6f, 4f)), 1), W(_gate + way * t + aside * (s * 2.6f)), YawOf(-aside * s), 0.4f, 0f, 0.2f, 0.45f);
            }
            // What it throws out, heaped by the failures' pen for the Ossuary's carts.
            if (_hasPen) Tries(8, () => PutRound(new PlaceSpec(PlacePiece.Midden, NextSeed(), PlaceSpec.Cm(r.Range(1.4f, 2f))), W(_pen + new Vector2(r.Range(-9f, 9f), front * r.Range(7f, 9f))), r.Float() * MathF.Tau, 2f, 1f, 0.3f, 0.45f));
            for (int k = 0; k < 2; k++) Tries(6, () => PutBox(new PlaceSpec(PlacePiece.Barrels, NextSeed(), r.Int(2, 4)), W(_door + new Vector2(r.Range(-9f, 9f), front * r.Range(3f, 6f))), r.Float() * MathF.Tau, new Vector2(1.5f), 1.4f, 0.3f, 0.4f));
        }

        // What lies underfoot: papers blown from the house, coal dust by the boiler, straw from the hatchery, and what the failures were fed.
        private void Underfoot(WorldRandom r)
        {
            float front = _gateSide == Side.Back ? -1f : 1f;
            Strew(LitterKind.Papers, _door + new Vector2(r.Range(-3f, 3f), front * r.Range(1.5f, 3f)), 2.4f, r.Int(6, 10));
            Strew(LitterKind.Embers, _door + new Vector2(r.Chance(0.5f) ? 11f : -11f, front * 3f), 1.6f, r.Int(8, 12));
            if (_hasPen) Strew(LitterKind.Bones, _pen, 3f, r.Int(10, 16));
            Strew(LitterKind.Straw, new Vector2(r.Range(-8f, 8f), r.Range(-4f, 4f)), 2f, r.Int(10, 16));
        }

        // Guards at the gate, greycoats in the yard, the failures in their pen, and the Institute's strongbox by the house's door.
        private void Hold(WorldRandom r)
        {
            Vector2 inward = -Outward(_gateSide);
            Stand(SpawnRole.Guard, Around(W(_gate + inward * 5f), 5f, r.Fork("gate"), 20), 2, 3.5f, r.Fork("guards"));
            Stand(SpawnRole.Raider, Around(Plan.Centre, MathF.Min(_half.X, _half.Y) * 0.7f, r.Fork("yard"), 60), r.Int(3, 5), 5f, r.Fork("yard men"));
            if (_hasPen) Stand(SpawnRole.Undead, Around(W(_pen), 4.5f, r.Fork("pen"), 30), r.Int(2, 3), 3f, r.Fork("failures"));
            Chest(W(_door + new Vector2(r.Range(-4f, 4f), 0f)), 10f);
        }
    }
}

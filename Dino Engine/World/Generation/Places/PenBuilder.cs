using Dino_Engine.Modelling.Procedural.Architecture.Places;
using Dino_Engine.Modelling.Procedural.Fence;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.World.Generation.Places
{
    /// <summary>
    /// Lays out a brood's pen (LORE.md: cages, chains and the smell of blood, where the Reich keeps its beasts between hunts). Inside the Reich's fence, its banners at the gate where
    /// the road comes: the kennels its war-raptors are shut in along the north side, facing the camera, troughs before them; the ring of trodden mud in the middle where they're taught,
    /// the straw dummies in the land's clothes they're taught on and the stakes they're chained to; the butcher's block and the rack of meat in a corner, the ground dark round them,
    /// and the midden; the handlers' tents along a side with their master's command tent, their table and their woodpile; a watch tower at the gate; braziers and cressets; and boards
    /// laid over the mud from the gate to the kennels. It's held by its handlers and whatever of its brood is home, and their chest is by the command tent.
    /// </summary>
    internal sealed class PenBuilder : PlaceBuilder
    {
        private const float GateHalf = 3.6f;
        private Side _gateSide;
        private Vector2 _gateLocal;
        private Vector2 _in;
        private Vector2 _ring;
        private float _kennelLine;
        private Vector2 _command;

        private PenBuilder(SiteBuilder b) : base(b, "pen") { }

        public static void Build(SiteBuilder b) => new PenBuilder(b).Lay();

        private void Lay()
        {
            // Its back to the north, so the kennels along it face the camera.
            Plan.Yaw = MathF.PI + Random.Range(-0.12f, 0.12f);
            Plan.Half = new Vector2(Site.Radius * Random.Range(0.74f, 0.82f), Site.Radius * Random.Range(0.62f, 0.7f));
            (_gateSide, _gateLocal) = GateOnRoad(Plan.Half);
            _in = -Outward(_gateSide);
            // Packed hard and pale where the brood's feet and the handlers' have been over it, so the mud of its ring and its butchery is dark against it.
            Paint(GroundPaint.Plaza, Footprint.Box(Plan.Centre, Plan.Half - new Vector2(1.2f), Plan.Yaw), 3f);
            Enclose(FenceUse.Pen, Plan.Half, _gateSide, _gateLocal, GateHalf);
            ReichGate(_gateSide, _gateLocal, GateHalf, Random.Fork("gate"));
            Kennels(Random.Fork("kennels"));
            Ring(Random.Fork("ring"));
            Butchery(Random.Fork("butchery"));
            Quarters(Random.Fork("quarters"));
            Lights(Random.Fork("lights"));
            Hold(Random.Fork("holders"));
            Litter(Random.Fork("litter"));
        }

        // The kennels along the back, facing the yard and the camera, one row or two, a trough before each; along a side if the back won't take them.
        private void Kennels(WorldRandom r)
        {
            _kennelLine = -Plan.Half.Y + 2.6f;
            if (Row(r, Side.Back)) return;
            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                if (side != _gateSide && Row(r, side)) return;
            }
        }

        // A row of kennels along the inside of a side, facing in; false if none would stand there.
        private bool Row(WorldRandom r, Side side)
        {
            Vector2 inward = -Outward(side);
            Vector2 along = new Vector2(-inward.Y, inward.X);
            float halfAlong = MathF.Abs(along.X) > 0.5f ? Plan.Half.X : Plan.Half.Y;
            float halfIn = MathF.Abs(inward.X) > 0.5f ? Plan.Half.X : Plan.Half.Y;
            float span = halfAlong * 2f - 5f;
            int rows = span > 26f && r.Chance(0.6f) ? 2 : 1;
            float cursor = -span * 0.5f;
            bool placed = false;
            for (int k = 0; k < rows + 2 && cursor < span * 0.5f - 5f; k++)
            {
                float w = r.Range(2.5f, 2.9f), d = r.Range(3.2f, 3.8f);
                // Fewer stalls each time a row won't go.
                for (int stalls = r.Int(3, 5); stalls >= 2; stalls--)
                {
                    float length = stalls * w;
                    if (cursor + length > span * 0.5f) continue;
                    Vector2 local = inward * (-halfIn + 2.6f + d * 0.5f) + along * (cursor + length * 0.5f);
                    if (side == _gateSide && (local - _gateLocal).Length < length * 0.5f + GateHalf + 2f) break;
                    if (!PutBox(new PlaceSpec(PlacePiece.Kennel, NextSeed(), stalls, PlaceSpec.Cm(w), PlaceSpec.Cm(d)), W(local), YawOf(inward), new Vector2(length * 0.5f + 0.3f, d * 0.5f + 0.3f), Tall, 0.3f, 0.36f)) continue;
                    placed = true;
                    if (side == Side.Back) _kennelLine = -halfIn + 2.6f + d;
                    // Troughs before them, and boards laid over the mud along their fronts.
                    Tries(6, () => Structure(StructureCatalog.FeedTrough, W(local + inward * (d * 0.5f + 1.6f) + along * r.Range(-length * 0.3f, length * 0.3f)), YawOf(inward), 0.3f, 0.4f));
                    LayFlat(new PlaceSpec(PlacePiece.Duckboards, NextSeed(), PlaceSpec.Cm(length - 0.6f), PlaceSpec.Cm(1.2f)), local + inward * (d * 0.5f + 0.9f), YawOf(along), length * 0.5f, 0.2f);
                    cursor += length;
                    break;
                }
                cursor += r.Range(2.5f, 4f);
            }
            return placed;
        }

        // The brood's cages for moving its beasts, along the side away from its tents, standing open, their doors swung back.
        private void Cages(WorldRandom r, float awaySide)
        {
            Side side = awaySide > 0f ? Side.Right : Side.Left;
            if (side == _gateSide) return;
            Vector2 inward = -Outward(side);
            int made = 0;
            for (int k = 0; k < 6 && made < 2; k++)
            {
                float w = r.Range(3f, 3.6f), d = r.Range(2.8f, 3.2f), h = r.Range(3f, 3.4f);
                Vector2 local = inward * (-Plan.Half.X + 2.4f + d * 0.5f) + new Vector2(0f, r.Range(-Plan.Half.Y * 0.35f, Plan.Half.Y * 0.45f));
                float yaw = YawOf(inward);
                if (!PutBox(new PlaceSpec(PlacePiece.Cage, NextSeed(), PlaceSpec.Cm(w), PlaceSpec.Cm(d), PlaceSpec.Cm(h)), W(local), yaw, new Vector2(w * 0.5f, d * 0.5f), Tall, 0.6f, 0.36f)) continue;
                // Its door, swung back on its hinge.
                float door = Prison.DoorWidth(w);
                Vector2 ax = Plan.Dir(new Vector2(inward.Y, -inward.X)), az = Plan.Dir(inward);
                Vector2 hinge = W(local) - ax * (door * 0.5f) + az * (d * 0.5f - 0.25f);
                World.Pending.Add(new PendingObject(new PlaceSpec(PlacePiece.CageDoor, NextSeed(), PlaceSpec.Cm(door), PlaceSpec.Cm(h)).Id, hinge, yaw - 1.75f, Vector3.One, null));
                Strew(LitterKind.Straw, local + inward * (d * 0.5f + 1.5f), 1.6f, r.Int(10, 16));
                made++;
            }
        }

        // The ring in the middle where the beasts are taught: trodden mud, the dummies they're taught on, and stakes with chains.
        private void Ring(WorldRandom r)
        {
            _ring = new Vector2(r.Range(-3f, 3f), (_kennelLine + Plan.Half.Y) * 0.5f - 1.5f + r.Range(-1.5f, 1.5f));
            float radius = r.Range(6f, 7.5f);
            Paint(GroundPaint.Mud, Footprint.Circle(W(_ring), radius), 2.5f);
            Tries(10, () => PutBox(new PlaceSpec(PlacePiece.BiteDummy, NextSeed(), r.Int(2, 3)), W(_ring + new Vector2(r.Range(-2f, 2f), r.Range(-1.5f, 1.5f))), r.Float() * MathF.Tau, new Vector2(3.6f, 1.4f), 2.2f, 0.3f, 0.4f));
            for (int k = 0; k < 3; k++)
            {
                Vector2 stake = _ring + SiteBuilder.Direction(r.Float() * MathF.Tau) * r.Range(radius * 0.55f, radius * 0.9f);
                Lay(new PlaceSpec(PlacePiece.ChainStake, NextSeed(), PlaceSpec.Cm(r.Range(2f, 3.6f))), W(stake), r.Float() * MathF.Tau, 0.5f);
            }
            for (int k = 0; k < 2; k++) LayFlat(new PlaceSpec(PlacePiece.Puddle, NextSeed(), PlaceSpec.Cm(r.Range(0.8f, 1.4f))), _ring + SiteBuilder.Direction(r.Float() * MathF.Tau) * r.Range(2f, radius * 0.8f), r.Float() * MathF.Tau, 1.2f, 0.15f);
        }

        // The butchery, in the corner away from the gate: the block and its rack, the ground dark round it, a carcass dragged in, and the midden.
        private void Butchery(WorldRandom r)
        {
            float side = _gateLocal.X > 0f ? -1f : 1f;
            if (_gateSide is Side.Left or Side.Right) side = _gateSide == Side.Left ? 1f : -1f;
            Vector2 corner = new Vector2(side * (Plan.Half.X - 5f), _kennelLine + 4f);
            Tries(10, () => PutBox(new PlaceSpec(PlacePiece.ButcherBlock, NextSeed()), W(corner + new Vector2(r.Range(-2f, 2f), r.Range(-1f, 2f))), YawOf(Vector2.UnitY) + r.Range(-0.3f, 0.3f), new Vector2(2.4f, 1.4f), 3f, 0.3f, 0.4f));
            Strew(LitterKind.Blood, corner + new Vector2(0f, 1.5f), 2.4f, r.Int(10, 16));
            Tries(8, () => Structure(StructureCatalog.MeatRack, W(corner + new Vector2(-side * r.Range(4f, 6f), r.Range(-1f, 2f))), YawOf(Vector2.UnitY), 0.3f, 0.4f));
            Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Carcass, NextSeed(), r.Chance(0.4f) ? 0 : 1, PlaceSpec.Cm(r.Range(3.2f, 4.6f))), W(corner + new Vector2(r.Range(-3f, 3f), r.Range(3f, 5f))), r.Float() * MathF.Tau, new Vector2(2.2f, 1.2f), 1f, 0.3f, 0.4f));
            Tries(8, () => PutRound(new PlaceSpec(PlacePiece.Midden, NextSeed(), PlaceSpec.Cm(r.Range(1.6f, 2.2f))), W(new Vector2(side * (Plan.Half.X - 3f), -Plan.Half.Y + r.Range(3f, 6f))), r.Float() * MathF.Tau, 2.2f, 1f, 0.3f, 0.45f));
            Paint(GroundPaint.Mud, Footprint.Circle(W(corner), 5f), 2.5f);
            Strew(LitterKind.Bones, corner + new Vector2(r.Range(-2f, 2f), r.Range(1f, 3f)), 3f, r.Int(14, 22));
            Strew(LitterKind.Straw, corner + new Vector2(-side * 3f, 4f), 2.4f, r.Int(18, 28));
        }

        // The handlers' quarters, along the side of the yard by the gate: their master's command tent, their tents, the table they eat at, their woodpile, their wagon and their gear.
        private void Quarters(WorldRandom r)
        {
            Vector2 across = new Vector2(-_in.Y, _in.X);
            float side = r.Chance(0.5f) ? 1f : -1f;
            Vector2 gateIn = _gateLocal + _in * 5f;
            Cages(r.Fork("cages"), -side);
            _command = gateIn + across * (side * r.Range(10f, 13f)) + _in * r.Range(2f, 4f);
            Tries(12, () =>
            {
                Vector2 tried = _command + new Vector2(r.Range(-2f, 2f), r.Range(-2f, 2f));
                float length = r.Range(5.5f, 7f);
                if (!PutBox(new PlaceSpec(PlacePiece.CommandTent, NextSeed(), PlaceSpec.Cm(length)), W(tried), YawOf(((_ring - tried).LengthSquared > 1f ? (_ring - tried).Normalized() : _in)), new Vector2(3.4f, length * 0.5f + 1.6f), 4f, 0.4f, 0.38f)) return false;
                _command = tried;
                return true;
            });
            for (int k = 0, tries = 0; k < 3 && tries < 18; tries++)
            {
                Vector2 tried = gateIn + across * (-side * r.Range(7f, 14f)) + _in * r.Range(-1f, 8f);
                if (PutBox(new PlaceSpec(PlacePiece.Tent, NextSeed(), PlaceSpec.Cm(r.Range(4.2f, 5.2f)), 1), W(tried), YawOf(across * side), new Vector2(3.8f, 3.4f), 3.4f, 0.4f, 0.4f)) k++;
            }
            Tries(10, () => PutBox(new PlaceSpec(PlacePiece.MessTable, NextSeed(), PlaceSpec.Cm(r.Range(2.4f, 3.4f))), W(_command - across * side * r.Range(4f, 6f) + _in * r.Range(1f, 3f)), YawOf(across), new Vector2(2.2f, 1.3f), 1.2f, 0.3f, 0.4f));
            Tries(10, () => PutBox(new PlaceSpec(PlacePiece.Firewood, NextSeed(), PlaceSpec.Cm(r.Range(2.6f, 4f)), PlaceSpec.Cm(r.Range(1.1f, 1.6f))), W(new Vector2(side * (Plan.Half.X - 2.5f), r.Range(-2f, 4f))), YawOf(new Vector2(-side, 0f)), new Vector2(2.4f, 1.6f), 1.6f, 0.3f, 0.4f));
            Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Wagon, NextSeed(), 0, 1, PlaceSpec.Cm(r.Range(3.8f, 4.6f))), W(gateIn + across * (side * r.Range(4f, 6f)) + _in * r.Range(3f, 6f)), YawOf(-_in) + r.Range(-0.3f, 0.3f), new Vector2(1.6f, 4f), 2.6f, 0.4f, 0.4f));
            Tries(8, () => PutBox(new PlaceSpec(PlacePiece.WeaponRack, NextSeed(), PlaceSpec.Cm(r.Range(2.4f, 3.2f)), 1), W(_command + across * side * r.Range(4f, 5f)), YawOf(-across * side), new Vector2(1.8f, 0.7f), 2f, 0.3f, 0.4f));
            Tries(8, () => PutBox(new PlaceSpec(PlacePiece.HarnessRack, NextSeed()), W(_ring + new Vector2(r.Range(-9f, -7f), r.Range(-1f, 1f))), YawOf(Vector2.UnitX), new Vector2(2f, 0.8f), 2.4f, 0.3f, 0.4f));
            for (int k = 0; k < 2; k++) Tries(6, () => PutBox(new PlaceSpec(PlacePiece.Crates, NextSeed(), r.Int(2, 4), 1), W(_command + new Vector2(r.Range(-5f, 5f), r.Range(-5f, 5f))), r.Float() * MathF.Tau, new Vector2(1.8f, 1.1f), 1.6f, 0.3f, 0.4f));
            // A watch tower at a corner of the gate's side.
            foreach (float s in new[] { 1f, -1f })
            {
                Vector2 corner = _gateSide is Side.Front or Side.Back ? new Vector2(s * (Plan.Half.X - 4.2f), _gateLocal.Y + _in.Y * 4.2f) : new Vector2(_gateLocal.X + _in.X * 4.2f, s * (Plan.Half.Y - 4.2f));
                if (Structure(StructureCatalog.GuardTower, W(corner), YawOf(_in), padding: 0.4f)) break;
            }
            // The way they walk: boards from the gate in, and trodden ground from the gate to the ring, the tents and the butchery.
            Trail(_gateLocal, _ring, 2.4f, GroundPaint.Yard, 1.4f);
            Trail(_gateLocal + _in * 3f, _command, 2f, GroundPaint.Yard, 1.4f);
            LayFlat(new PlaceSpec(PlacePiece.Duckboards, NextSeed(), PlaceSpec.Cm(MathF.Min(9f, (_ring - _gateLocal).Length - 6f)), PlaceSpec.Cm(1.4f)), _gateLocal + _in * 3f + (_ring - _gateLocal).Normalized() * MathF.Min(4.5f, ((_ring - _gateLocal).Length - 6f) * 0.5f), YawOf((_ring - _gateLocal).Normalized()), 4f, 0.2f);
        }

        // Braziers by the ring and the tents, and the Reich's cressets along the kennels.
        private void Lights(WorldRandom r)
        {
            Tries(8, () => PutRound(new PlaceSpec(PlacePiece.Brazier, NextSeed(), PlaceSpec.Cm(r.Range(1f, 1.2f))), W(_ring + SiteBuilder.Direction(r.Float() * MathF.Tau) * r.Range(7.5f, 9f)), 0f, 0.8f, 1.4f, 0.3f, 0.4f));
            Tries(8, () => PutRound(new PlaceSpec(PlacePiece.Brazier, NextSeed(), PlaceSpec.Cm(r.Range(1f, 1.2f))), W(_command + new Vector2(r.Range(-4f, 4f), r.Range(3.5f, 5f))), 0f, 0.8f, 1.4f, 0.3f, 0.4f));
            for (float x = -Plan.Half.X + 5f; x < Plan.Half.X - 4f; x += r.Range(8f, 11f))
            {
                PutRound(new PlaceSpec(PlacePiece.Torch, NextSeed(), PlaceSpec.Cm(r.Range(2.6f, 3f)), 1), W(new Vector2(x, _kennelLine + 3.2f)), 0f, 0.35f, 0f, 0.2f, 0.45f);
            }
        }

        // Handlers about the ring and the kennels, guards at the gate, and the chest by the command tent.
        private void Hold(WorldRandom r)
        {
            Stand(SpawnRole.Raider, Around(W(_ring), 11f, r.Fork("ring"), 60), r.Int(3, 6), 4f, r.Fork("handlers"));
            Stand(SpawnRole.Guard, Around(W(_gateLocal + _in * 4f), 5f, r.Fork("gate"), 30), 2, 3.5f, r.Fork("guards"));
            Chest(W(_command + _in * -1f + new Vector2(-_in.Y, _in.X) * 3.2f), Site.Radius * 0.4f);
        }

        // What's underfoot: straw trodden about the kennels, bones and scraps about the ring, ash by the braziers, chips by the woodpile.
        private void Litter(WorldRandom r)
        {
            for (float x = -Plan.Half.X + 6f; x < Plan.Half.X - 5f; x += r.Range(7f, 10f)) Strew(LitterKind.Straw, new Vector2(x, _kennelLine + 2.2f), 2.6f, r.Int(18, 30));
            Strew(LitterKind.Bones, _ring + new Vector2(r.Range(-3f, 3f), r.Range(-3f, 3f)), 3.2f, r.Int(8, 14));
            Strew(LitterKind.Feathers, _ring + new Vector2(r.Range(-4f, 4f), r.Range(-2f, 2f)), 2.2f, r.Int(8, 14));
            Strew(LitterKind.Gear, _gateLocal + _in * 6f, 2.6f, r.Int(4, 7));
        }
    }
}

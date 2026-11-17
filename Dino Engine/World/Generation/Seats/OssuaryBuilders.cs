using Dino_Engine.Modelling.Procedural.Architecture.Places;
using Dino_Engine.Modelling.Procedural.Architecture.Seats;
using Dino_Engine.Modelling.Procedural.Fence;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.World.Generation.Seats
{
    /// <summary>
    /// Lays out the boneyard (SEATS.md): the Ossuary's great dig, where a land's bone tyrant was raised. The slab it lay in stands across the back, tilted up to the camera, the hollow
    /// it left still in it and the tackle it came up on still over it; the ground before it and the yard are the rock's own chippings; a fence of great bones runs round all of it,
    /// with ribs crossed over each way in; to one side what the dig has taken out is drawn, numbered, jacketed and crated, and to the other the next beast is already half out of the
    /// rock. Its fires are the Ossuary's cold green, and the dead it raised are what holds it.
    /// </summary>
    internal sealed class BoneyardBuilder : SeatBuilder
    {
        private (int R, int G, int B) _stone;
        private float _slabFront;

        public BoneyardBuilder(SiteBuilder b) : base(b, "boneyard") { }

        public override void Lay()
        {
            _stone = LandStone;
            foreach (Side side in GateSides) Plan.Gates.Add((W(GateAt(side)), Dir(Outward(side))));
            LevelGround();
            Paint(GroundPaint.Yard, BoxOf(new Vector2(-Hx + 3f, -Back + 3f), new Vector2(Hx - 3f, Front - 3f)), 5f);
            Slab(Random.Fork("slab"));
            Fence(Random.Fork("fence"));
            // The yard itself is sand, as the rock's chippings are mottled, and what a tyrant's about to do has to be read off its floor.
            OpenYard(GroundPaint.Sand, 0.97f, 3.5f);
            MudRing(6f, GroundPaint.Chippings);
            Fires(SeatDressing.FireOssuary, 8, 3.2f, local => !(MathF.Abs(local.X) < 9f && local.Y < _slabFront + 6f));
            Office(Random.Fork("office"));
            NextBeast(Random.Fork("next"));
            Diggers(Random.Fork("diggers"));
            Hold(Random.Fork("holders"));
            Underfoot(Random.Fork("underfoot"));
        }

        // The slab, across the back, its face to the camera, and the ground before it down to the yard in its own chippings.
        private void Slab(WorldRandom r)
        {
            float width = Math.Clamp(R * 0.54f, 30f, 37f), depth = r.Range(16f, 18f), rise = r.Range(9f, 10.5f);
            Vector2 at = new Vector2(0f, -Back + depth * 0.5f + 2.5f);
            _slabFront = at.Y + depth * 0.5f;
            if (Raise(new PlaceSpec(PlacePiece.BoneMatrix, NextSeed(), PlaceSpec.Cm(width), PlaceSpec.Cm(depth), PlaceSpec.Cm(rise), _stone.R, _stone.G, _stone.B), at, Vector2.UnitY))
            {
                Chart(at, new Vector2(width * 0.5f, depth * 0.5f), Vector2.UnitY, rise);
            }
            Paint(GroundPaint.Chippings, BoxOf(new Vector2(-width * 0.5f - 5f, -Back + 1f), new Vector2(width * 0.5f + 5f, Heart.Y - Yard + 4f)), 3f);
            Keep(BoxOf(new Vector2(-width * 0.5f - 5.5f, _slabFront), new Vector2(width * 0.5f + 5.5f, _slabFront + 3.5f)));
            // What was broken off it, heaped either side of it.
            foreach (float s in new[] { -1f, 1f })
            {
                Vector2 heap = new Vector2(s * (width * 0.5f + r.Range(6f, 8f)), at.Y + r.Range(-3f, 2f));
                PutBox(new PlaceSpec(PlacePiece.Spoil, NextSeed(), PlaceSpec.Cm(r.Range(9f, 12f))), W(heap), YawOf(new Vector2(-s, 0.25f)), new Vector2(5.5f, 3.2f), 2f, 0.2f, 0.6f);
                Strew(LitterKind.Spalls, new Vector2(s * (width * 0.5f + 2.5f), _slabFront + 2f), 3.4f, r.Int(10, 16), _stone);
            }
        }

        // A fence of great bones round all of it, a little lower along the front, with ribs crossed over each of the three ways in.
        private void Fence(WorldRandom r)
        {
            uint seed = NextSeed();
            const float GateHalf = 5.2f;
            void Pale(Vector2 from, Vector2 to, float height)
            {
                Vector2 d = to - from;
                float length = d.Length;
                if (length < 3f) return;
                d /= length;
                int count = Math.Max(1, (int)MathF.Round(length / 10f));
                float each = length / count;
                Vector2 inward = new Vector2(-d.Y, d.X);
                if (Vector2.Dot(Heart - (from + to) * 0.5f, inward) < 0f) inward = -inward;
                for (int i = 0; i < count; i++) Raise(new PlaceSpec(PlacePiece.BonePale, seed + (uint)(i % 3), PlaceSpec.Cm(each), PlaceSpec.Cm(height)), from + d * ((i + 0.5f) * each), inward, offRoads: true);
            }
            Pale(new Vector2(-Hx, -Back), new Vector2(Hx, -Back), 4.4f);
            foreach (float s in new[] { -1f, 1f })
            {
                Pale(new Vector2(s * Hx, -Back), new Vector2(s * Hx, Heart.Y - GateHalf), 4f);
                Pale(new Vector2(s * Hx, Heart.Y + GateHalf), new Vector2(s * Hx, Front), 3.6f);
                Pale(new Vector2(s * Hx, Front), new Vector2(s * GateHalf, Front), 2.8f);
            }
            uint arch = NextSeed();
            foreach (Side side in GateSides)
            {
                bool great = side == MainSide;
                Vector2 at = GateAt(side), inward = Inward(side);
                Raise(new PlaceSpec(PlacePiece.RibArch, great ? arch : arch + 1u, PlaceSpec.Cm(GateHalf * 2f - 0.6f), PlaceSpec.Cm(great ? 10.5f : 8.5f)), at, inward);
                Keep(Footprint.Box(W(at), new Vector2(GateHalf - 1.2f, 9f), YawOf(inward)));
                Trail(at + Outward(side) * 3f, Heart + (at - Heart).Normalized() * (Yard - 1f), great ? 6.5f : 5f, GroundPaint.Chippings, 2f);
            }
        }

        // What the dig has taken out: drawn and numbered under the awning, jacketed, crated, sieved for, and laid out in rows to be counted, by the Ossuary's cold lamps.
        private void Office(WorldRandom r)
        {
            float s = r.Chance(0.5f) ? 1f : -1f;
            _officeSide = s;
            Vector2 at = new Vector2(s * (Hx - 13f), Heart.Y - Yard * 0.55f);
            Paint(GroundPaint.Yard, Footprint.Circle(W(at), 11f), 5f);
            PutBox(new PlaceSpec(PlacePiece.DrawingTable, NextSeed()), W(at), YawOf(new Vector2(-s, 0.3f)), new Vector2(2.4f, 1.9f), 2.6f, 0.2f, 0.6f);
            for (int k = 0; k < 3; k++) Tries(8, () => PutBox(new PlaceSpec(PlacePiece.BoneCrates, NextSeed()), W(at + new Vector2(r.Range(-5f, 5f), r.Range(3.5f, 7f))), r.Float() * MathF.Tau, new Vector2(1.7f, 1.3f), 1.5f, 0.3f, 0.6f));
            for (int k = 0; k < 2; k++) Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Jackets, NextSeed(), r.Int(3, 5)), W(at + new Vector2(r.Range(-6f, 6f), r.Range(-7f, -3.5f))), r.Float() * MathF.Tau, new Vector2(1.8f, 1.4f), 1.2f, 0.3f, 0.6f));
            Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Sieve, NextSeed()), W(at + new Vector2(-s * r.Range(4f, 6f), r.Range(-2f, 2f))), r.Float() * MathF.Tau, new Vector2(1.2f, 1.2f), 1.2f, 0.3f, 0.6f));
            for (int k = 0; k < 2; k++) Tries(8, () => Structure(StructureCatalog.BoneRows, W(at + new Vector2(-s * r.Range(2f, 7f), r.Range(8f, 12f))), YawOf(Vector2.UnitY) + r.Range(-0.3f, 0.3f), 0.3f, 0.6f));
            Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Wagon, NextSeed(), 0, 1, PlaceSpec.Cm(r.Range(4f, 4.8f))), W(at + new Vector2(s * r.Range(1f, 4f), r.Range(10f, 14f))), YawOf(new Vector2(-s, 0.4f)), new Vector2(1.6f, 4.1f), 2.6f, 0.3f, 0.6f));
            for (int k = 0; k < 3; k++) Tries(6, () => PutRound(new PlaceSpec(PlacePiece.Torch, NextSeed(), PlaceSpec.Cm(r.Range(3.2f, 3.8f)), 4), W(at + new Vector2(r.Range(-7f, 7f), r.Range(-6f, 9f))), r.Float() * MathF.Tau, 0.4f, 0f, 0.3f, 0.6f));
            Strew(LitterKind.Papers, at + new Vector2(-s * 2f, 2f), 2.6f, r.Int(6, 10));
            Strew(LitterKind.Bones, at + new Vector2(-s * 5f, 9f), 3.4f, r.Int(10, 16));
        }

        private float _officeSide = 1f;

        // Across from it, the next one: another of the great beasts half out of the rock, under the dig's strings, a hoist over it and its spoil beside it.
        private void NextBeast(WorldRandom r)
        {
            float s = -_officeSide;
            Vector2 at = new Vector2(s * (Hx - 14f), Heart.Y - Yard * 0.5f);
            Paint(GroundPaint.Chippings, Footprint.Circle(W(at), 9f), 3f);
            int form = r.Int(1, 3);     // Anything but another tyrant.
            float length = r.Range(11f, 13f);
            LayDown(new PlaceSpec(PlacePiece.Fossil, NextSeed(), form, PlaceSpec.Cm(length), r.Int(55, 80)), at, YawOf(new Vector2(0.4f * s, 1f)));
            World.Placement.Add(Footprint.Circle(W(at), length * 0.5f), PlacementLayer.Prop, solid: false, height: 0f);
            LayDown(new PlaceSpec(PlacePiece.DigGrid, NextSeed(), PlaceSpec.Cm(length * 0.9f), PlaceSpec.Cm(length * 0.6f)), at, YawOf(new Vector2(0.4f * s, 1f)));
            Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Hoist, NextSeed(), PlaceSpec.Cm(r.Range(5.5f, 6.5f))), W(at + new Vector2(s * r.Range(6.5f, 8f), r.Range(-2f, 2f))), YawOf(new Vector2(-s, 0f)), new Vector2(2.6f, 2.6f), 1.6f, 0.3f, 0.6f));
            Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Spoil, NextSeed(), PlaceSpec.Cm(r.Range(7f, 9f))), W(at + new Vector2(s * r.Range(2f, 5f), -r.Range(8.5f, 10f))), YawOf(new Vector2(0.2f, 1f)), new Vector2(4.5f, 2.6f), 1.8f, 0.3f, 0.6f));
            for (int k = 0; k < 2; k++) Tries(6, () => PutRound(new PlaceSpec(PlacePiece.Torch, NextSeed(), PlaceSpec.Cm(r.Range(3.2f, 3.8f)), 4), W(at + new Vector2(r.Range(-7f, 7f), r.Range(6f, 8f))), r.Float() * MathF.Tau, 0.4f, 0f, 0.3f, 0.6f));
            Strew(LitterKind.Spalls, at + new Vector2(0f, 6f), 3.6f, r.Int(10, 16), _stone);
        }

        // The living who dig for it camp in a corner by the front, and the dead they've raised to dig stand about the yard.
        private void Diggers(WorldRandom r)
        {
            float s = r.Chance(0.5f) ? 1f : -1f;
            Vector2 at = new Vector2(s * (Hx - 12f), Front - 12f);
            Camp(at, Heart - at, r.Fork("camp"), 2);
            Vector2 heap = new Vector2(-s * (Hx - 11f), Front - 11f);
            Paint(GroundPaint.Yard, Footprint.Circle(W(heap), 8f), 4f);
            for (int k = 0; k < 2; k++) Tries(6, () => PutRound(new PlaceSpec(PlacePiece.Carcass, NextSeed(), k, PlaceSpec.Cm(r.Range(4f, 6f))), W(heap + new Vector2(r.Range(-4f, 4f), r.Range(-4f, 4f))), r.Float() * MathF.Tau, 2.4f, 1f, 0.3f, 0.6f, solid: false));
            Strew(LitterKind.Bones, heap, 4f, r.Int(14, 20));
        }

        private void Hold(WorldRandom r)
        {
            Garrison(SpawnRole.Undead, SpawnRole.Undead, r.Int(7, 9), r);
            HoardAt(new Vector2(_officeSide * r.Range(15f, 18f), _slabFront + 6f), r.Fork("hoard"));
        }

        // What lies in the yard: the ring a thing of bone was chained by, and the rock's chips and what fell off it.
        private void Underfoot(WorldRandom r)
        {
            LayDown(new PlaceSpec(PlacePiece.YardRing, NextSeed(), PlaceSpec.Cm(r.Range(6f, 9f))), Heart + new Vector2(r.Range(-2f, 2f), r.Range(-4f, -2f)), Plan.Yaw + r.Range(-0.6f, 0.6f));
            for (int k = 0; k < 4; k++)
            {
                float a = r.Float() * MathF.Tau;
                Vector2 at = Heart + new Vector2(MathF.Sin(a), MathF.Cos(a)) * r.Range(Yard * 0.3f, Yard * 0.85f);
                Strew(k % 2 == 0 ? LitterKind.Bones : LitterKind.Spalls, at, 3.2f, r.Int(8, 14), _stone);
            }
        }
    }

    /// <summary>
    /// Lays out the barrow (SEATS.md): one of the land's great barrows, opened by the Ossuary. The mound stands across the back, cut open to the camera, its portal stones laid bare;
    /// in the cutting's mouth the tyrant's bones lie as they were found, too far gone to stand, under the dig's strings; the land's old stones still ring the ground, some pulled down
    /// where they were in the way, an avenue of them in from the front, and what the land's people left at them kicked over; the turf is the short dark turf of old holy ground, worn
    /// pale in a ring by what walks it; and round the yard burn the cold blue fires the ghost was called up by, which are what holds it there.
    /// </summary>
    internal sealed class BarrowBuilder : SeatBuilder
    {
        private (int R, int G, int B) _stone;
        private float _mouth;       // Where the cutting comes out, in front of the mound.

        public BarrowBuilder(SiteBuilder b) : base(b, "barrow") { }

        public override void Lay()
        {
            _stone = LandStone;
            foreach (Side side in GateSides) Plan.Gates.Add((W(GateAt(side)), Dir(Outward(side))));
            LevelGround();
            Paint(GroundPaint.Sward, BoxOf(new Vector2(-Hx + 1f, -Back + 1f), new Vector2(Hx - 1f, Front - 1f)), 6f);
            Mound(Random.Fork("mound"));
            OpenYard(GroundPaint.Sward, 1f, 4f);
            MudRing(4.5f, GroundPaint.Yard);
            Fires(SeatDressing.FireGhost, 8, 3.4f, local => !(MathF.Abs(local.X) < 8f && local.Y < _mouth + 12f));
            Stones(Random.Fork("stones"));
            Bones(Random.Fork("bones"));
            Dig(Random.Fork("dig"));
            Offerings(Random.Fork("offerings"));
            Hold(Random.Fork("holders"));
        }

        private void Mound(WorldRandom r)
        {
            float width = Math.Clamp(R * 0.72f, 40f, 48f), depth = r.Range(19f, 21f), high = r.Range(7.6f, 8.8f);
            Vector2 at = new Vector2(0f, -Back + depth * 0.5f + 2f);
            _mouth = at.Y + depth * 0.5f;
            if (Raise(new PlaceSpec(PlacePiece.GreatBarrow, NextSeed(), PlaceSpec.Cm(width), PlaceSpec.Cm(depth), PlaceSpec.Cm(high), _stone.R, _stone.G, _stone.B), at, Vector2.UnitY))
            {
                Chart(at, new Vector2(width * 0.4f, depth * 0.33f), Vector2.UnitY, high);
            }
            float cut = OssuaryWorks.CuttingOf(width);
            Keep(BoxOf(new Vector2(-cut - 1f, _mouth - 6f), new Vector2(cut + 1f, _mouth + 2f)));
            Trail(new Vector2(0f, _mouth - 5f), new Vector2(0f, Heart.Y - Yard + 2f), cut * 2f + 2f, GroundPaint.Mud, 2.5f);
        }

        // The land's old stones round the ground: one every dozen metres along its edge, tallest along the back, a pair either side of each way in, some pulled down, and an avenue of
        // smaller ones in from the front.
        private void Stones(WorldRandom r)
        {
            void Stone(Vector2 local, float height, float width, int form, float lean = 0f)
            {
                Vector2 toHeart = (Heart - local).Normalized();
                // Broad, as from the game's camera a stone is seen from above, and a thin one is a stroke.
                height *= 1.15f;
                width = MathF.Min(width * 1.4f, 3.4f);
                PutBox(new PlaceSpec(PlacePiece.Menhir, NextSeed(), PlaceSpec.Cm(height), PlaceSpec.Cm(width), form, (int)lean, _stone.R, _stone.G, _stone.B), W(local), YawOf(toHeart) + r.Range(-0.3f, 0.3f), new Vector2(width * 0.6f, width * 0.5f), height, 0.2f, 0.6f);
            }
            int Form() => Pick(r, (0, 5f), (1, 2f), (2, 1.2f), (3, 1.4f));
            // Along the back and the sides.
            for (float x = -Hx; x <= Hx + 0.1f; x += Hx / 4f)
            {
                if (MathF.Abs(x) < Hx * 0.42f) continue;        // The mound's in the way.
                Stone(new Vector2(x + r.Range(-1f, 1f), -Back + r.Range(-0.5f, 1f)), r.Range(5.5f, 7f), r.Range(1.8f, 2.6f), Form(), r.Range(-6f, 6f));
            }
            foreach (float s in new[] { -1f, 1f })
            {
                for (float z = -Back + 11f; z < Front - 4f; z += 11f)
                {
                    if (MathF.Abs(z - Heart.Y) < 9f) continue;
                    Stone(new Vector2(s * (Hx + r.Range(-1f, 1f)), z + r.Range(-1.5f, 1.5f)), r.Range(4f, 5.6f), r.Range(1.6f, 2.3f), Form(), r.Range(-8f, 8f));
                }
                // The front, lower, so that the yard's seen over them.
                for (float x = 14f; x < Hx - 3f; x += 10f) Stone(new Vector2(s * (x + r.Range(-1f, 1f)), Front + r.Range(-1f, 1f)), r.Range(2.6f, 3.6f), r.Range(1.4f, 2f), Form(), r.Range(-8f, 8f));
            }
            // A taller pair at each way in, carved, and humming.
            foreach (Side side in GateSides)
            {
                Vector2 at = GateAt(side), across = new Vector2(-Outward(side).Y, Outward(side).X);
                float height = side == Side.Front ? 5f : 6.6f;
                foreach (float s in new[] { -1f, 1f }) Stone(at + across * (s * 5.6f), height, 2.3f, 1);
                Keep(Footprint.Box(W(at), new Vector2(4.2f, 9f), YawOf(Inward(side))));
            }
            // The avenue in from the front.
            for (float z = Front - 7f; z > Heart.Y + Yard + 3f; z -= 6f)
            {
                foreach (float s in new[] { -1f, 1f }) Stone(new Vector2(s * 5.4f, z), r.Range(2f, 2.8f), r.Range(1.1f, 1.5f), r.Chance(0.2f) ? 3 : 0, r.Range(-6f, 6f));
            }
            Trail(new Vector2(0f, Front + 3f), new Vector2(0f, Heart.Y + Yard - 1f), 5f);
        }

        // The tyrant's bones, in the mouth of the cutting, as they were found: laid bare and gridded, and never raised. The fires either side of them are small ones of the ghost's blue.
        private void Bones(WorldRandom r)
        {
            Vector2 at = new Vector2(0f, _mouth + 6.5f);
            float length = r.Range(14.5f, 16f);
            float yaw = YawOf(new Vector2(1f, 0.12f * (r.Chance(0.5f) ? 1f : -1f)));
            LayDown(new PlaceSpec(PlacePiece.Fossil, NextSeed(), 0, PlaceSpec.Cm(length), 100), at, yaw);
            LayDown(new PlaceSpec(PlacePiece.DigGrid, NextSeed(), PlaceSpec.Cm(length * 0.62f), PlaceSpec.Cm(length * 0.95f)), at, yaw);
            World.Placement.Add(Footprint.Circle(W(at), length * 0.52f), PlacementLayer.Keepout);
            Paint(GroundPaint.Mud, Footprint.Circle(W(at), length * 0.56f), 3f);
            uint seed = NextSeed();
            foreach (float s in new[] { -1f, 1f })
            {
                Vector2 fire = W(at + new Vector2(s * (length * 0.5f + 2.4f), 2.5f));
                Put(new PlaceSpec(PlacePiece.GreatBrazier, seed, PlaceSpec.Cm(2f), SeatDressing.FireGhost), fire, 0f, Footprint.Circle(fire, 1.3f), 2f, PlacementLayer.Prop, 0.6f, 0.15f, PlacementLayer.Structure | PlacementLayer.Prop | PlacementLayer.Fence);
            }
        }

        // The Ossuary's dig: its table under the awning, its crates and jackets, the hoist it lifted the capstones with, its spoil, its wagon and its own cold lamps.
        private void Dig(WorldRandom r)
        {
            float s = r.Chance(0.5f) ? 1f : -1f;
            _digSide = s;
            Vector2 at = new Vector2(s * (Hx - 14f), Heart.Y - Yard * 0.45f);
            Paint(GroundPaint.Yard, Footprint.Circle(W(at), 10f), 5f);
            PutBox(new PlaceSpec(PlacePiece.DrawingTable, NextSeed()), W(at), YawOf(new Vector2(-s, 0.3f)), new Vector2(2.4f, 1.9f), 2.6f, 0.2f, 0.6f);
            for (int k = 0; k < 2; k++) Tries(8, () => PutBox(new PlaceSpec(PlacePiece.BoneCrates, NextSeed()), W(at + new Vector2(r.Range(-5f, 5f), r.Range(3.5f, 7f))), r.Float() * MathF.Tau, new Vector2(1.7f, 1.3f), 1.5f, 0.3f, 0.6f));
            Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Jackets, NextSeed(), r.Int(3, 5)), W(at + new Vector2(r.Range(-6f, 6f), r.Range(-7f, -3.5f))), r.Float() * MathF.Tau, new Vector2(1.8f, 1.4f), 1.2f, 0.3f, 0.6f));
            Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Hoist, NextSeed(), PlaceSpec.Cm(r.Range(6f, 7f))), W(new Vector2(s * r.Range(9f, 11f), _mouth + 2.5f)), YawOf(new Vector2(-s, 0f)), new Vector2(2.6f, 2.6f), 1.6f, 0.3f, 0.6f));
            Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Wagon, NextSeed(), 0, 1, PlaceSpec.Cm(r.Range(4f, 4.8f))), W(at + new Vector2(s * r.Range(0f, 3f), r.Range(9f, 13f))), YawOf(new Vector2(-s, 0.4f)), new Vector2(1.6f, 4.1f), 2.6f, 0.3f, 0.6f));
            for (int k = 0; k < 3; k++) Tries(6, () => PutRound(new PlaceSpec(PlacePiece.Torch, NextSeed(), PlaceSpec.Cm(r.Range(3.2f, 3.8f)), 4), W(at + new Vector2(r.Range(-7f, 7f), r.Range(-6f, 9f))), r.Float() * MathF.Tau, 0.4f, 0f, 0.3f, 0.6f));
            Strew(LitterKind.Papers, at + new Vector2(-s * 2f, 2f), 2.6f, r.Int(6, 10));
            Vector2 camp = new Vector2(s * (Hx - 12f), Front - 12f);
            Camp(camp, Heart - camp, r.Fork("camp"), 2);
        }

        private float _digSide = 1f;

        // What the land's people left at their dead, across from the dig: cairns, a table of offerings kicked over, ribbons gone grey, and a lantern of theirs knocked down.
        private void Offerings(WorldRandom r)
        {
            float s = -_digSide;
            Vector2 at = new Vector2(s * (Hx - 13f), Heart.Y - Yard * 0.4f);
            Tries(6, () => PutRound(new PlaceSpec(PlacePiece.Offerings, NextSeed(), 1, _stone.R, _stone.G, _stone.B), W(at + new Vector2(r.Range(-2f, 2f), r.Range(-2f, 2f))), r.Float() * MathF.Tau, 1.6f, 1f, 0.3f, 0.6f, solid: false));
            for (int k = 0; k < 2; k++) Tries(6, () => PutRound(new PlaceSpec(PlacePiece.Cairns, NextSeed(), r.Int(3, 5), PlaceSpec.Cm(r.Range(1.6f, 2.4f)), _stone.R, _stone.G, _stone.B), W(at + new Vector2(r.Range(-6f, 6f), r.Range(4f, 9f))), r.Float() * MathF.Tau, 2f, 1f, 0.3f, 0.6f, solid: false));
            for (int k = 0; k < 2; k++) Tries(6, () => PutRound(new PlaceSpec(PlacePiece.ShrineLantern, NextSeed(), PlaceSpec.Cm(r.Range(3.4f, 4f)), k == 0 ? 1 : 2, _stone.R, _stone.G, _stone.B), W(at + new Vector2(r.Range(-7f, 7f), r.Range(-8f, -3f))), r.Float() * MathF.Tau, 1.2f, 2f, 0.3f, 0.6f));
            Strew(LitterKind.Gear, at + new Vector2(r.Range(-3f, 3f), r.Range(10f, 13f)), 3f, r.Int(5, 8));
        }

        private void Hold(WorldRandom r)
        {
            Garrison(SpawnRole.Undead, SpawnRole.Undead, r.Int(7, 9), r);
            HoardAt(new Vector2(_digSide * r.Range(12f, 15f), _mouth + 3f), r.Fork("hoard"));
        }
    }

    /// <summary>
    /// Lays out the bile works (SEATS.md): where the Ossuary keeps a rotting tyrant on its feet. Across the back stand the vats of bile on their firebox, lit their sick yellow-green,
    /// pipes off their heads out to the frame the tyrant is hooked into before them; the Reich's iron fence runs round it between piers of brick; the whole of it is mud, with bile
    /// standing in it where it's leaked; to one side is what the tyrant's fed, carted in and butchered, and to the other the pen of failures that are next for the vats. Its fires are
    /// the bile's own light, greycoats keep its gate, and what holds the rest of it is rotting where it stands.
    /// </summary>
    internal sealed class BileworksBuilder : SeatBuilder
    {
        private float _vatFront;
        private float _penSide = 1f;

        public BileworksBuilder(SiteBuilder b) : base(b, "bileworks") { }

        public override void Lay()
        {
            foreach (Side side in GateSides) Plan.Gates.Add((W(GateAt(side)), Dir(Outward(side))));
            LevelGround();
            Paint(GroundPaint.Yard, BoxOf(new Vector2(-Hx + 3f, -Back + 2f), new Vector2(Hx - 3f, Front - 3f)), 5f);
            Vats(Random.Fork("vats"));
            Enclose(Random.Fork("fence"));
            OpenYard(GroundPaint.Mud, 1f, 3f);
            MudRing(9f);
            Fires(SeatDressing.FireBile, 8, 3.2f, local => !(MathF.Abs(local.X) < 10f && local.Y < _vatFront + 13f));
            Feeding(Random.Fork("feeding"));
            Failures(Random.Fork("failures"));
            Pools(Random.Fork("pools"));
            Hold(Random.Fork("holders"));
            TyrantsLeavings(Random.Fork("yard"), new Vector2(-_penSide, -0.6f));
        }

        // The vats across the back, and before them the frame.
        private void Vats(WorldRandom r)
        {
            float height = r.Range(10.5f, 12f);
            Vector2 at = new Vector2(0f, -Back + 4.85f + 2.2f);
            _vatFront = at.Y + 4.85f;
            const int Count = 4;
            float half = Count * OssuaryWorks.VatSpacing * 0.5f;
            if (Raise(new PlaceSpec(PlacePiece.BileVats, NextSeed(), Count, PlaceSpec.Cm(height)), at, Vector2.UnitY)) Chart(at, new Vector2(half, 4.6f), Vector2.UnitY, height);
            Paint(GroundPaint.Plaza, BoxOf(new Vector2(-half - 3f, -Back + 1f), new Vector2(half + 3f, _vatFront + 4f)), 2f);
            Vector2 cradle = new Vector2(0f, _vatFront + 8f);
            Raise(new PlaceSpec(PlacePiece.BileCradle, NextSeed(), PlaceSpec.Cm(r.Range(15f, 17f)), PlaceSpec.Cm(r.Range(11f, 12.5f))), cradle, Vector2.UnitY);
            Keep(BoxOf(new Vector2(-9f, _vatFront), new Vector2(9f, _vatFront + 12f)));
            // Bile in barrels by the firebox, and the boiler that keeps it all warm.
            foreach (float s in new[] { -1f, 1f })
            {
                Tries(6, () => PutBox(new PlaceSpec(PlacePiece.Barrels, NextSeed(), r.Int(3, 5)), W(new Vector2(s * (half + r.Range(4f, 6f)), _vatFront - r.Range(1f, 4f))), r.Float() * MathF.Tau, new Vector2(1.5f), 1.4f, 0.3f, 0.6f));
            }
            Tries(6, () => PutBox(new PlaceSpec(PlacePiece.Boiler, NextSeed()), W(new Vector2((r.Chance(0.5f) ? 1f : -1f) * (half + 8.5f), -Back + 5f)), YawOf(Vector2.UnitY), new Vector2(2.6f, 1.6f), Tall, 0.3f, 0.6f));
        }

        // The Reich's fence round it, a gap at each way in between two piers of brick with a lantern on each.
        private void Enclose(WorldRandom r)
        {
            const float GateHalf = 4.6f;
            Vector2 inside = W(Heart);
            B.Wall(FenceUse.Pen, W(new Vector2(-Hx, -Back)), W(new Vector2(Hx, -Back)), inside);
            foreach (float s in new[] { -1f, 1f })
            {
                B.Wall(FenceUse.Pen, W(new Vector2(s * Hx, -Back)), W(new Vector2(s * Hx, Heart.Y - GateHalf - 1.2f)), inside);
                B.Wall(FenceUse.Pen, W(new Vector2(s * Hx, Heart.Y + GateHalf + 1.2f)), W(new Vector2(s * Hx, Front)), inside);
                B.Wall(FenceUse.Pen, W(new Vector2(s * Hx, Front)), W(new Vector2(s * (GateHalf + 1.2f), Front)), inside);
            }
            uint pier = NextSeed();
            foreach (Side side in GateSides)
            {
                Vector2 at = GateAt(side), outward = Outward(side), across = new Vector2(-outward.Y, outward.X);
                bool great = side == MainSide;
                foreach (float s in new[] { -1f, 1f })
                {
                    // The fence ends inside its pier, so the pier minds everything but the fence.
                    Vector2 local = W(at + across * (s * (GateHalf + 1.2f)));
                    float high = great ? 6.4f : 5.2f;
                    Put(new PlaceSpec(PlacePiece.GatePier, great ? pier : pier + 1u, PlaceSpec.Cm(high), 0, 1), local, YawOf(outward), Footprint.Box(local, new Vector2(1f, 1f), YawOf(outward)), high, PlacementLayer.Prop, 0.6f, 0.1f, PlacementLayer.Structure | PlacementLayer.Prop | PlacementLayer.Road);
                }
                Keep(Footprint.Box(W(at), new Vector2(GateHalf - 1f, 9f), YawOf(Inward(side))));
                Trail(at + outward * 3f, Heart + (at - Heart).Normalized() * (Yard + 2f), great ? 6.5f : 5f, GroundPaint.Plaza, 1.6f);
            }
            GateStandards(MainSide, 8.6f, 9.5f, 4.2f);
        }

        // What the tyrant's fed: carcasses carted in, the block they're cut up on, the heap of what's left, and the wagon.
        private void Feeding(WorldRandom r)
        {
            float s = r.Chance(0.5f) ? 1f : -1f;
            _penSide = -s;
            Vector2 at = new Vector2(s * (Hx - 13f), Heart.Y - Yard * 0.5f);
            Tries(8, () => PutBox(new PlaceSpec(PlacePiece.ButcherBlock, NextSeed()), W(at + new Vector2(r.Range(-2f, 2f), r.Range(-2f, 2f))), YawOf(new Vector2(-s, 0.3f)), new Vector2(2.2f, 1.5f), 2.4f, 0.3f, 0.6f));
            for (int k = 0; k < 3; k++) Tries(8, () => PutRound(new PlaceSpec(PlacePiece.Carcass, NextSeed(), k == 0 ? 0 : 1, PlaceSpec.Cm(r.Range(4f, 6.5f))), W(at + new Vector2(r.Range(-6f, 6f), r.Range(5f, 12f))), r.Float() * MathF.Tau, 2.4f, 1f, 0.3f, 0.6f, solid: false));
            Tries(8, () => PutRound(new PlaceSpec(PlacePiece.Midden, NextSeed(), PlaceSpec.Cm(r.Range(1.8f, 2.3f))), W(at + new Vector2(s * r.Range(2f, 5f), -r.Range(5f, 8f))), r.Float() * MathF.Tau, 2.3f, 1f, 0.3f, 0.6f));
            Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Wagon, NextSeed(), 0, 1, PlaceSpec.Cm(r.Range(4f, 4.8f))), W(at + new Vector2(s * r.Range(0f, 3f), r.Range(13f, 17f))), YawOf(new Vector2(-s, 0.4f)), new Vector2(1.6f, 4.1f), 2.6f, 0.3f, 0.6f));
            Strew(LitterKind.Blood, at + new Vector2(-s * 2f, 1f), 3.4f, r.Int(6, 10));
            Strew(LitterKind.Bones, at + new Vector2(0f, 7f), 4f, r.Int(12, 18));
            Vector2 camp = new Vector2(s * (Hx - 12f), Front - 12f);
            Camp(camp, Heart - camp, r.Fork("camp"), 2);
        }

        // The failures, penned across from the butchery behind a fence of their own until there's room in the vats: the jars of what didn't hatch at all racked beside it.
        private void Failures(WorldRandom r)
        {
            float s = _penSide;
            Vector2 half = new Vector2(r.Range(7f, 8.5f), r.Range(6f, 7.5f));
            Vector2 at = new Vector2(s * (Hx - half.X - 3f), Heart.Y - Yard * 0.5f - 2f);
            Footprint f = BoxOf(at - half, at + half);
            if (Clear(f, PlacementLayer.Built | PlacementLayer.Road, 0.4f))
            {
                Vector2 a = at + new Vector2(-s * half.X, -half.Y), c = at + new Vector2(-s * half.X, half.Y), e = at + new Vector2(s * half.X, half.Y), g = at + new Vector2(s * half.X, -half.Y);
                B.Wall(FenceUse.Pen, W(g), W(a), W(at));
                B.Wall(FenceUse.Pen, W(a), W(c), W(at));
                B.Wall(FenceUse.Pen, W(c), W(c + new Vector2(s * (half.X - 2.6f), 0f)), W(at));
                B.Wall(FenceUse.Pen, W(c + new Vector2(s * (half.X + 2.6f), 0f)), W(e), W(at));
                World.Placement.Add(BoxOf(at - half + new Vector2(0.6f), at + half - new Vector2(0.6f)), PlacementLayer.Keepout);
                Lay(new PlaceSpec(PlacePiece.ChainStake, NextSeed(), PlaceSpec.Cm(2.5f)), W(at + new Vector2(r.Range(-2f, 2f), r.Range(-2f, 2f))), r.Float() * MathF.Tau, 0.5f);
                Stand(SpawnRole.Undead, Around(W(at), 4.5f, r.Fork("pen"), 30), r.Int(2, 3), 3f, r.Fork("failures"));
                Strew(LitterKind.Straw, at, 4f, r.Int(10, 16));
            }
            Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Shelter, NextSeed(), PlaceSpec.Cm(8f), PlaceSpec.Cm(4.8f)), W(at + new Vector2(-s * 2f, half.Y + 6.5f)), YawOf(Vector2.UnitY), new Vector2(4.4f, 2.8f), Tall, 0.25f, 0.6f));
            Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Crates, NextSeed(), r.Int(3, 5), 1), W(at + new Vector2(-s * r.Range(6f, 9f), half.Y + r.Range(9f, 12f))), r.Float() * MathF.Tau, new Vector2(2.2f, 1.3f), 1.8f, 0.3f, 0.6f));
        }

        // Bile standing in the mud where the pipes have leaked: about the frame, and out across the yard, a few of the pools lit.
        private void Pools(WorldRandom r)
        {
            for (int k = 0; k < 9; k++)
            {
                float a = r.Float() * MathF.Tau;
                Vector2 at = k < 3 ? new Vector2(r.Range(-9f, 9f), _vatFront + r.Range(3f, 12f)) : Heart + new Vector2(MathF.Sin(a), MathF.Cos(a)) * r.Range(4f, Yard + 6f);
                LayDown(new PlaceSpec(PlacePiece.BilePool, NextSeed(), PlaceSpec.Cm(r.Range(1.4f, 3.2f)), k % 3 == 0 ? 1 : 0), at, r.Float() * MathF.Tau);
            }
        }

        private void Hold(WorldRandom r)
        {
            Garrison(SpawnRole.Guard, SpawnRole.Undead, r.Int(6, 8), r);
            HoardAt(new Vector2(-_penSide * r.Range(15f, 18f), _vatFront + 5f), r.Fork("hoard"));
        }
    }
}

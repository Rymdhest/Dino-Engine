using Dino_Engine.Modelling.Procedural.Architecture.Places;
using Dino_Engine.Modelling.Procedural.Architecture.Seats;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.World.Generation.Seats
{
    /// <summary>
    /// Lays out the citadel (SEATS.md): the land's old castle, taken. Its hall and keep stand across the back on an upper ward two metres over the yard, up a broad flight, the land's
    /// arms gone from the keep and the Reich's banner down it; curtain walls of the land's own stone run round both wards between round towers, beacons burning on the two at the back,
    /// and one stretch of them is still closed with the Reich's iron where its beasts came through. In the lower ward the tyrant's stall stands under the terrace, open towards the yard
    /// it's let loose in; across from it the tithe is weighed and written down; the garrison camps in the corners by the front wall, which is low, so that the whole of it is seen from
    /// where the camera stands; and at the yard's edge the land's lord lies in stone where the ropes left him, the Reich's standard beside what's left of his plinth.
    /// </summary>
    internal sealed class CitadelBuilder : SeatBuilder
    {
        private const int StepCount = 5;
        private const float StepRise = 0.4f, StepTread = 1f, FlightWidth = 13f, Cheek = 1.5f;
        private const float WallThick = 2.6f;

        private (int R, int G, int B) _stone;
        private float _rise;            // How far the upper ward stands over the lower.
        private float _terrace;         // Where the upper ward ends, towards the yard.
        private float _hallFoot;        // Where the steps up to the hall's door come down to.
        private float _hallHalf;        // How far either side of the middle the hall reaches, its lean-tos with it.
        private float _stallSide;       // Which side of the lower ward the tyrant's stall is on: -1 its left.
        private Vector2 _stallMouth;

        public CitadelBuilder(SiteBuilder b) : base(b, "citadel") { }

        public override void Lay()
        {
            _stone = LandStone;
            _rise = StepCount * StepRise;
            _terrace = Heart.Y - Yard;
            _stallSide = MainSide == Side.Left ? 1f : MainSide == Side.Right ? -1f : Random.Fork("side").Chance(0.5f) ? 1f : -1f;
            foreach (Side side in GateSides) Plan.Gates.Add((W(GateAt(side)), Dir(Outward(side))));

            LevelGround();
            Ground();
            Terrace();
            Hall(Random.Fork("hall"));
            Walls(Random.Fork("walls"));
            OpenYard(GroundPaint.Sand, 0.97f, 3.5f);
            Light(Random.Fork("light"));
            Stall(Random.Fork("stall"));
            Lord(Random.Fork("lord"));
            Tithe(Random.Fork("tithe"));
            Works(Random.Fork("works"));
            Camps(Random.Fork("camps"));
            Hold(Random.Fork("holders"));
            Underfoot(Random.Fork("underfoot"));
        }

        // The lower ward's ground: earth trodden bare, the land's own grass left along the foot of its walls, gravelled ways in from each gate and before the flight, and the
        // yard itself in sand, raked for the tyrant as an arena's is.
        private void Ground()
        {
            Paint(GroundPaint.Yard, BoxOf(new Vector2(-Hx + 4.5f, _terrace + 1f), new Vector2(Hx - 4.5f, Front - 4f)), 4f);
            MudRing(8f);
            foreach (Side side in GateSides)
            {
                Vector2 gate = GateAt(side);
                Trail(gate + Outward(side) * 3f, Heart + (gate - Heart).Normalized() * (Yard + 7f), side == MainSide ? 7.5f : 5.5f, GroundPaint.Plaza, 1.6f);
            }
        }

        // What the Reich keeps a castle running on: its war-raptors' kennels along the back wall of the upper ward and the dummies they're set on, the land's arms it took stacked
        // across from them, the butchery by the tyrant's stall, the old well, and outside the great gate its gibbet and its stakes.
        private void Works(WorldRandom r)
        {
            int stalls = r.Int(4, 5);
            float along = _hallHalf + 3.5f + stalls * 1.4f;
            if (along + stalls * 1.4f < Hx - 6.5f)
            {
                Vector2 kennel = new Vector2(_stallSide * along, -Back + WallThick * 0.5f + 2.9f);
                if (PutBox(new PlaceSpec(PlacePiece.Kennel, NextSeed(), stalls, PlaceSpec.Cm(2.8f), PlaceSpec.Cm(3.6f)), W(kennel), YawOf(Vector2.UnitY), new Vector2(stalls * 1.4f + 0.2f, 2f), 3.1f, 0.15f, 0.6f))
                {
                    Tries(6, () => PutBox(new PlaceSpec(PlacePiece.BiteDummy, NextSeed(), r.Int(2, 3)), W(kennel + new Vector2(r.Range(-3f, 3f), r.Range(6f, 8f))), r.Float() * MathF.Tau, new Vector2(1.6f, 1f), 1.2f, 0.3f, 0.6f));
                    Strew(LitterKind.Straw, kennel + new Vector2(0f, 3.5f), 4f, r.Int(10, 14));
                }
                Vector2 arms = new Vector2(-_stallSide * along, -Back + WallThick * 0.5f + 2.4f);
                for (int k = 0; k < 2; k++) Tries(6, () => PutBox(new PlaceSpec(PlacePiece.WeaponRack, NextSeed(), PlaceSpec.Cm(r.Range(2.4f, 3f)), 0), W(arms + new Vector2((k - 0.5f) * 4.4f + r.Range(-0.4f, 0.4f), 0f)), YawOf(Vector2.UnitY), new Vector2(1.7f, 0.6f), 2f, 0.2f, 0.6f));
                for (int k = 0; k < 2; k++) Tries(6, () => PutBox(new PlaceSpec(PlacePiece.Crates, NextSeed(), r.Int(3, 5), 1), W(arms + new Vector2(r.Range(-5f, 5f), r.Range(3f, 5.5f))), r.Float() * MathF.Tau, new Vector2(2.2f, 1.3f), 1.8f, 0.3f, 0.6f));
                Strew(LitterKind.Gear, arms + new Vector2(0f, 3f), 3.2f, r.Int(5, 8));
            }
            // The butchery, by the stall, and the heap of what's left.
            Vector2 block = _stallMouth + new Vector2(_stallSide * 5.5f, r.Range(9f, 12f));
            Tries(8, () => PutBox(new PlaceSpec(PlacePiece.ButcherBlock, NextSeed()), W(block + new Vector2(r.Range(-1.5f, 1.5f), r.Range(-1.5f, 1.5f))), YawOf(new Vector2(-_stallSide, 0.4f)), new Vector2(2.2f, 1.5f), 2.4f, 0.3f, 0.6f));
            Tries(8, () => PutRound(new PlaceSpec(PlacePiece.Midden, NextSeed(), PlaceSpec.Cm(r.Range(1.5f, 2f))), W(block + new Vector2(_stallSide * r.Range(1f, 3f), r.Range(4f, 6f))), r.Float() * MathF.Tau, 2.1f, 1f, 0.3f, 0.6f));
            Strew(LitterKind.Blood, block + new Vector2(-_stallSide * 1.5f, 0f), 2.6f, r.Int(5, 8));
            // The castle's well, in the ward across from the stall.
            Tries(8, () => Structure(StructureCatalog.Well, W(new Vector2(-_stallSide * (Hx - r.Range(17f, 21f)), _terrace + r.Range(17f, 21f))), r.Float() * MathF.Tau, 0.5f, 0.6f));
            // Outside the great gate: the gibbet, and stakes either side of the road.
            Vector2 gate = GateAt(MainSide), outward = Outward(MainSide), across = new Vector2(-outward.Y, outward.X);
            Tries(6, () => PutRound(new PlaceSpec(PlacePiece.Gibbet, NextSeed(), PlaceSpec.Cm(r.Range(5.6f, 6.4f))), W(gate + outward * r.Range(13f, 16f) + across * (r.Chance(0.5f) ? 9.5f : -9.5f)), YawOf(outward), 0.7f, 0f, 0.3f, 0.6f));
            uint stakes = NextSeed();
            foreach (float s in new[] { -1f, 1f }) PutBox(new PlaceSpec(PlacePiece.Barricade, stakes, PlaceSpec.Cm(4.6f), 1), W(gate + outward * 11.5f + across * (s * 7.6f)), YawOf(outward), new Vector2(2.5f, 0.7f), 1.6f, 0.2f, 0.6f);
        }

        // The upper ward: ground raised behind a wall, the flight up to it, and the ground under the flight laid to its pitch (SQUARE.md). The ground it holds up stops short of the
        // wall's face, as a step in a grid of heights a metre apart is laid as a slope, and the wall is paved back over that.
        private void Terrace()
        {
            float half = FlightWidth * 0.5f, run = StepCount * StepTread;
            World.Terraces.Add(new GroundPad(BoxOf(new Vector2(-Hx, -Back), new Vector2(Hx, _terrace - TownMason.Inset)), Level + _rise, TownMason.Sharp));
            World.Terraces.Add(new GroundPad(BoxOf(new Vector2(-half - Cheek, _terrace - TownMason.Inset - 1f), new Vector2(half + Cheek, _terrace)), Level + _rise, TownMason.Sharp));
            World.Terraces.Add(new GroundPad(BoxOf(new Vector2(-half, _terrace), new Vector2(half, _terrace + run)), Level + _rise * 0.5f - 0.01f, TownMason.Sharp, Dir(-Vector2.UnitY) * (StepRise / StepTread)));
            Paint(GroundPaint.Paving, BoxOf(new Vector2(-Hx + 1.4f, -Back + 1.4f), new Vector2(Hx - 1.4f, _terrace - 0.5f)), 1.2f);

            Raise(new PlaceSpec(PlacePiece.SeatSteps, NextSeed(), PlaceSpec.Cm(FlightWidth), StepCount, PlaceSpec.Cm(StepRise), PlaceSpec.Cm(StepTread), PlaceSpec.Cm(Cheek), _stone.R, _stone.G, _stone.B), new Vector2(0f, _terrace + run), Vector2.UnitY);
            Keep(BoxOf(new Vector2(-half - Cheek - 0.5f, _terrace - 3f), new Vector2(half + Cheek + 0.5f, _terrace + run + 3.5f)));

            // The wall either side of the flight, out to where the towers stand at its ends.
            float from = half + Cheek, to = Hx - 4.2f;
            int count = Math.Max(1, (int)MathF.Round((to - from) / 10.5f));
            float each = (to - from) / count;
            uint seed = NextSeed();
            foreach (float s in new[] { -1f, 1f })
            {
                for (int i = 0; i < count; i++)
                {
                    Raise(new PlaceSpec(PlacePiece.SeatRevet, seed, PlaceSpec.Cm(each), PlaceSpec.Cm(_rise), _stone.R, _stone.G, _stone.B), new Vector2(s * (from + (i + 0.5f) * each), _terrace), Vector2.UnitY);
                }
            }
        }

        // The hall and keep, across the back of the upper ward, their door towards the flight.
        private void Hall(WorldRandom r)
        {
            float width = Math.Clamp(R * 0.5f, 28f, 34f);
            const float Depth = 12f, Eaves = 13f;
            _hallHalf = width * 0.5f + Fortress.WingWidth;
            float back = -Back + WallThick * 0.5f + 0.8f;
            Vector2 at = new Vector2(0f, back + Depth * 0.5f);
            if (Raise(new PlaceSpec(PlacePiece.GreatHall, NextSeed(), PlaceSpec.Cm(width), PlaceSpec.Cm(Depth), PlaceSpec.Cm(Eaves), PlaceSpec.Cm(r.Range(30f, 33f)), _stone.R, _stone.G, _stone.B), at, Vector2.UnitY, _rise))
            {
                Chart(at, new Vector2(width * 0.5f + 0.4f, Depth * 0.5f + 0.4f), Vector2.UnitY, Eaves);
            }
            _hallFoot = back + Depth + Fortress.PorchDepth + 3.2f;
            Keep(BoxOf(new Vector2(-Fortress.PorchHalf - 1f, back + Depth), new Vector2(Fortress.PorchHalf + 1f, _hallFoot + 1.5f)));
        }

        // The towers and the curtain between them: tall round the upper ward, a little lower round the yard, a gatehouse in each side of it, and along the front a low wall with an
        // open gate, so that nothing stands between the camera and the yard.
        private void Walls(WorldRandom r)
        {
            uint towers = NextSeed();
            float mid = _terrace - 0.9f;
            foreach (float s in new[] { -1f, 1f })
            {
                Tower(new Vector2(s * Hx, -Back), 5.2f, 15f + _rise, Fortress.TowerBeacon, _stone, towers);
                Tower(new Vector2(s * Hx, mid), 4.4f, 12.5f + _rise, Fortress.TowerPennant, _stone, towers + 1u);
                Tower(new Vector2(s * Hx, Front), 4f, 10.5f, Fortress.TowerHoarding, _stone, towers + 2u);
            }
            float high = 10f + _rise, low = 9.5f;
            Curtain(new Vector2(-Hx + 5f, -Back), new Vector2(Hx - 5f, -Back), high, WallThick, _stone);
            // Where the Reich's beasts came through: the far stretch of the side away from the stall, towards the front.
            float breached = -_stallSide;
            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                float s = side == Side.Left ? -1f : 1f;
                Curtain(new Vector2(s * Hx, -Back + 5f), new Vector2(s * Hx, mid - 4.2f), high, WallThick, _stone);
                float span = Gatehouse(side, _stone, 14f);
                Curtain(new Vector2(s * Hx, mid + 4.2f), new Vector2(s * Hx, Heart.Y - span), low, WallThick, _stone);
                Curtain(new Vector2(s * Hx, Heart.Y + span), new Vector2(s * Hx, Front - 3.8f), low, WallThick, _stone, Fortress.WallWalk, s == breached ? 0.99f : -1f);
            }
            float way = OpenGate(Side.Front, _stone, 8.6f);
            foreach (float s in new[] { -1f, 1f }) Curtain(new Vector2(s * (Hx - 3.8f), Front), new Vector2(s * way, Front), 4.4f, 1.7f, _stone, Fortress.WallLow);
        }

        // The tyrant's stall, in the corner of the lower ward under the terrace, away from the great gate, its mouth to the yard and the camera, and the ground between it and the
        // yard trodden to mud.
        private void Stall(WorldRandom r)
        {
            const float Width = 16f, Depth = 15f;
            Vector2 at = new Vector2(_stallSide * (Hx - WallThick * 0.5f - 1f - Width * 0.5f), _terrace + 1.5f + Depth * 0.5f);
            _stallMouth = at + new Vector2(0f, Depth * 0.5f + 1f);
            if (!Raise(new PlaceSpec(PlacePiece.TyrantStall, NextSeed(), PlaceSpec.Cm(Width), PlaceSpec.Cm(Depth), PlaceSpec.Cm(r.Range(8f, 9f))), at, Vector2.UnitY)) return;
            Keep(BoxOf(at + new Vector2(-Width * 0.5f - 1.5f, Depth * 0.5f), at + new Vector2(Width * 0.5f + 1.5f, Depth * 0.5f + 9f)));
            Paint(GroundPaint.Mud, BoxOf(at - new Vector2(Width * 0.5f - 0.5f, Depth * 0.5f - 0.5f), at + new Vector2(Width * 0.5f - 0.5f, Depth * 0.5f + 4f)), 2.5f);
            Trail(_stallMouth, Heart + (_stallMouth - Heart).Normalized() * (Yard * 0.5f), 7f, GroundPaint.Mud, 3f);
        }

        // The land's lord, pulled down, at the edge of the yard across from the stall and towards the camera, and the Reich's standard set up by his plinth.
        private void Lord(WorldRandom r)
        {
            float a = r.Range(0.55f, 0.85f) * -_stallSide;
            Vector2 at = Heart + new Vector2(MathF.Sin(a), MathF.Cos(a)) * (Yard + 4.8f);
            if (!PutRound(new PlaceSpec(PlacePiece.FallenLord, NextSeed(), PlaceSpec.Cm(r.Range(6f, 7.5f)), _stone.R, _stone.G, _stone.B), W(at), YawOf(Vector2.UnitY) + r.Range(-0.35f, 0.35f), 2.4f, 2.3f, 0.2f, 0.6f)) return;
            Standard(at + new Vector2(-_stallSide * 3.4f, -1.2f), Vector2.UnitY, 8.4f, 1.8f, 4.8f, NextSeed());
            Strew(LitterKind.Rubble, at + new Vector2(0f, 3f), 3f, r.Int(8, 12), _stone);
            Strew(LitterKind.Gear, at + new Vector2(r.Range(-4f, 4f), r.Range(4f, 7f)), 3f, r.Int(5, 8));
        }

        // The tithe: what the land's made to give, weighed and written down under a shelter against the terrace, across the ward from the stall, and the wagon that takes it.
        private void Tithe(WorldRandom r)
        {
            float s = -_stallSide;
            Vector2 shelter = new Vector2(s * (Hx - WallThick * 0.5f - 7.2f), _terrace + 1.4f + 3f);
            PutBox(new PlaceSpec(PlacePiece.Shelter, NextSeed(), PlaceSpec.Cm(11f), PlaceSpec.Cm(5.4f)), W(shelter), YawOf(Vector2.UnitY), new Vector2(5.9f, 3.1f), Tall, 0.25f, 0.6f);
            Vector2 yard = shelter + new Vector2(0f, 8f);
            for (int k = 0; k < 4; k++) Tries(6, () => Structure(StructureCatalog.Sacks, W(shelter + new Vector2(r.Range(-4.2f, 4.2f), r.Range(-1f, 1.4f))), r.Float() * MathF.Tau, 0.2f, 0.6f));
            Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Ledger, NextSeed()), W(yard + new Vector2(-s * r.Range(3f, 5f), r.Range(-2f, 0f))), YawOf(Vector2.UnitY), new Vector2(1.6f, 1.4f), 2.6f, 0.3f, 0.6f));
            Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Scales, NextSeed()), W(yard + new Vector2(s * r.Range(0f, 2f), r.Range(-1.5f, 0.5f))), YawOf(Vector2.UnitY), new Vector2(1.7f, 0.7f), 2.6f, 0.3f, 0.6f));
            for (int k = 0; k < 2; k++) Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Bales, NextSeed(), r.Int(3, 5)), W(yard + new Vector2(r.Range(-5f, 5f), r.Range(1.5f, 4f))), r.Float() * MathF.Tau, new Vector2(1.6f, 1.4f), 1.6f, 0.3f, 0.6f));
            Tries(10, () => PutBox(new PlaceSpec(PlacePiece.Wagon, NextSeed(), 0, 1, PlaceSpec.Cm(r.Range(4f, 4.8f))), W(yard + new Vector2(s * r.Range(-1f, 3f), r.Range(5f, 8f))), YawOf(new Vector2(-s, 0.3f)), new Vector2(1.6f, 4.1f), 2.6f, 0.3f, 0.6f));
            Strew(LitterKind.Papers, yard + new Vector2(-s * 4f, 1f), 2.4f, r.Int(6, 9));
            Strew(LitterKind.Straw, yard + new Vector2(0f, 3f), 3.4f, r.Int(10, 14));
        }

        // The garrison, in the two corners by the front wall, its tents turned to the yard.
        private void Camps(WorldRandom r)
        {
            foreach (float s in new[] { -1f, 1f })
            {
                Vector2 at = new Vector2(s * (Hx - 12f), Front - 12.5f);
                Camp(at, Heart - at, r.Fork(s > 0f ? "right" : "left"));
                Trail(at + (Heart - at).Normalized() * 5f, Heart + (at - Heart).Normalized() * Yard, 3f);
            }
        }

        // The great fires round the yard and at the foot of the flight, the Reich's standards outside its great gate and before the hall, and its lanterns along the way in.
        private void Light(WorldRandom r)
        {
            float half = FlightWidth * 0.5f + Cheek;
            Fires(SeatDressing.FireReich, 8, 3.2f, local => !(MathF.Abs(local.X) < half + 3f && local.Y < _terrace + StepCount * StepTread + 4f));
            uint seed = NextSeed();
            foreach (float s in new[] { -1f, 1f })
            {
                // At the yard's edge, inside what's kept clear for the tyrant, as the flight's own piers are.
                Vector2 at = W(new Vector2(s * (half + 2.7f), _terrace + StepCount * StepTread + 1.2f));
                Put(new PlaceSpec(PlacePiece.GreatBrazier, seed, PlaceSpec.Cm(3.6f), SeatDressing.FireReich), at, 0f, Footprint.Circle(at, 1.3f), 3.6f, PlacementLayer.Prop, 0.6f, 0.15f, PlacementLayer.Structure | PlacementLayer.Prop | PlacementLayer.Fence);
            }
            GateStandards(MainSide, MainSide == Side.Front ? 7.5f : 6.8f);
            uint banner = NextSeed();
            foreach (float s in new[] { -1f, 1f }) Standard(new Vector2(s * (Fortress.PorchHalf + 9.2f), _hallFoot - 0.5f), Vector2.UnitY, 9f, 2f, 5.2f, banner);
            // Lanterns either side of the way in from the great gate, as far as the yard.
            Vector2 gate = GateAt(MainSide), inward = Inward(MainSide), across = new Vector2(-inward.Y, inward.X);
            float reach = (Heart - gate).Length - Yard - 2f;
            for (float t = 6f; t < reach; t += 7f)
            {
                foreach (float s in new[] { -1f, 1f }) PutRound(new PlaceSpec(PlacePiece.Lantern, NextSeed(), PlaceSpec.Cm(4.2f), 1), W(gate + inward * t + across * (s * 5.2f)), YawOf(-across * s), 0.4f, 0f, 0.15f, 0.6f);
            }
        }

        // Guards at the great gate and at the head of the flight, the rest round the yard, and the hoard by the hall's door.
        private void Hold(WorldRandom r)
        {
            Garrison(SpawnRole.Guard, SpawnRole.Raider, r.Int(6, 9), r);
            Stand(SpawnRole.Guard, new[] { W(new Vector2(-4.5f, _terrace - 3.5f)), W(new Vector2(4.5f, _terrace - 3.5f)) }, 2, 3f, r.Fork("flight"));
            HoardAt(new Vector2((r.Chance(0.5f) ? 1f : -1f) * (Fortress.PorchHalf + 4.6f), _hallFoot - 0.8f), r.Fork("hoard"));
        }

        // What lies about: what the tyrant's left of its yard, straw trodden out of its stall, and rubble still lying inside the breach.
        private void Underfoot(WorldRandom r)
        {
            TyrantsLeavings(r.Fork("yard"), _stallMouth - Heart);
            Strew(LitterKind.Straw, _stallMouth + new Vector2(r.Range(-3f, 3f), 2f), 4f, r.Int(12, 18));
            Strew(LitterKind.Bones, _stallMouth + new Vector2(r.Range(-5f, 5f), 4f), 3f, r.Int(8, 12));
            Strew(LitterKind.Rubble, new Vector2(-_stallSide * (Hx - 4.5f), Front - 9f), 3.2f, r.Int(8, 12), _stone);
        }
    }
}

using Dino_Engine.Modelling.Procedural.Architecture.Places;
using Dino_Engine.Modelling.Procedural.Architecture.Seats;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.World.Generation.Seats
{
    /// <summary>
    /// Lays out the hatchery (SEATS.md): where the Amber Institute grew the land's tyrant. Its brood house stands at the back, a dome of iron and amber glass burst open towards
    /// the yard, where the tyrant came out of its tank and has been kept since; along the back wall either side of it stand the tanks the next brood is growing in; in one corner is
    /// the blood-doctors' house, with what they cut and keep set out before it, and glasshouses of eggs down the sides; by the way in the shell it hatched from is kept behind a
    /// chain, across from the post it was broken to the collar at; the young that came of the same clutch are kennelled in a corner. The yard is the sand eggs are warmed in.
    /// </summary>
    internal sealed class HatcheryBuilder : SeatBuilder
    {
        private float _radius, _domeFront, _side = 1f;

        public HatcheryBuilder(SiteBuilder b) : base(b, "hatchery") { }

        public override void Lay()
        {
            foreach (Side side in GateSides) Plan.Gates.Add((W(GateAt(side)), Dir(Outward(side))));
            LevelGround();
            Paint(GroundPaint.Yard, BoxOf(new Vector2(-Hx + 3f, -Back + 2f), new Vector2(Hx - 3f, Front - 3f)), 5f);
            _side = Random.Chance(0.5f) ? 1f : -1f;
            Dome(Random.Fork("dome"));
            Tanks(Random.Fork("tanks"));
            IronFence(4.6f, GroundPaint.Plaza);
            GateStandards(MainSide, 8.6f, 9.5f, 4.2f);
            OpenYard(GroundPaint.Sand, 0.97f, 3.5f);
            MudRing(5f, GroundPaint.Yard);
            Fires(SeatDressing.FireReich, 8, 3.2f, local => !(MathF.Abs(local.X) < _radius * 0.8f && local.Y < _domeFront + 6f));
            Institute(Random.Fork("institute"));
            Glasshouses(Random.Fork("glass"));
            Shell(Random.Fork("shell"));
            Young(Random.Fork("young"));
            Hold(Random.Fork("holders"));
            TyrantsLeavings(Random.Fork("yard"), new Vector2(_side, -0.6f));
            Underfoot(Random.Fork("underfoot"));
        }

        // The brood house, at the back, its breach to the yard, and the ground before the breach kept clear, as that's the way the tyrant came.
        private void Dome(WorldRandom r)
        {
            _radius = Math.Clamp(R * 0.17f, 10f, 11.5f);
            Vector2 at = new Vector2(0f, -Back + _radius + 2.6f);
            _domeFront = at.Y + _radius;
            if (Raise(new PlaceSpec(PlacePiece.BroodDome, NextSeed(), PlaceSpec.Cm(_radius)), at, Vector2.UnitY)) Chart(at, _radius - 0.4f, 12f);
            Paint(GroundPaint.Plaza, Footprint.Circle(W(at), _radius + 5f), 2.5f);
            Keep(BoxOf(new Vector2(-6f, _domeFront), new Vector2(6f, _domeFront + 7f)));
        }

        // The tanks, in rows along the back either side of it, their fronts to the camera, and the boiler that keeps them warm.
        private void Tanks(WorldRandom r)
        {
            const int Count = 4;
            float half = Count * ReichWorks.TankSpacing * 0.5f + 0.4f, y = -Back + 4.6f;
            void Row(float x)
            {
                float height = r.Range(4.6f, 5.6f);
                Vector2 at = new Vector2(x, y);
                if (Raise(new PlaceSpec(PlacePiece.BroodTanks, NextSeed(), Count, PlaceSpec.Cm(height)), at, Vector2.UnitY)) Chart(at, new Vector2(half - 0.3f, 1.3f), Vector2.UnitY, height);
                Paint(GroundPaint.Plaza, BoxOf(at - new Vector2(half + 1.5f, 2.5f), at + new Vector2(half + 1.5f, 4.5f)), 2f);
            }
            float inner = _radius + 2.6f + half, outer = Hx - half - 3.2f;
            Row(-inner);
            Row(inner);
            if (outer - half > inner + half + 1.5f) Row(-_side * outer);
            Tries(6, () => PutBox(new PlaceSpec(PlacePiece.Boiler, NextSeed()), W(new Vector2(-_side * (inner + r.Range(-2f, 3f)), y + r.Range(8.5f, 10.5f))), YawOf(Vector2.UnitY), new Vector2(2.6f, 1.6f), Tall, 0.3f, 0.6f));
        }

        // The blood-doctors' house in a back corner, and before it what they keep: jars of what failed, the benches amber's cut at, and the amber, crated.
        private void Institute(WorldRandom r)
        {
            float w = r.Range(14f, 16f), d = r.Range(9f, 10f);
            Vector2 at = new Vector2(_side * (Hx - w * 0.5f - 4f), -Back + d * 0.5f + 3.2f);
            PutBox(new PlaceSpec(PlacePiece.LabHall, NextSeed(), PlaceSpec.Cm(w), PlaceSpec.Cm(d)), W(at), YawOf(Vector2.UnitY), new Vector2(w * 0.5f + 0.4f, d * 0.5f + 0.4f), Tall, 0.2f, 0.6f, PlacementLayer.Structure);
            Vector2 court = at + new Vector2(0f, d * 0.5f + 5.5f);
            Paint(GroundPaint.Plaza, Footprint.Circle(W(court), 8.5f), 3f);
            for (int k = 0; k < 3; k++) Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Specimens, NextSeed()), W(court + new Vector2(r.Range(-7f, 7f), r.Range(-2.5f, 0f))), YawOf(Vector2.UnitY), new Vector2(1.9f, 0.6f), 2.8f, 0.3f, 0.6f));
            for (int k = 0; k < 2; k++) Tries(8, () => PutBox(new PlaceSpec(PlacePiece.AmberBench, NextSeed()), W(court + new Vector2(r.Range(-6f, 6f), r.Range(1.5f, 4f))), YawOf(Vector2.UnitY) + r.Range(-0.3f, 0.3f), new Vector2(1.5f, 0.8f), 1.4f, 0.3f, 0.6f));
            for (int k = 0; k < 3; k++) Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Cargo, NextSeed(), 2, r.Int(2, 3), 1), W(court + new Vector2(r.Range(-8f, 8f), r.Range(3f, 7f))), r.Float() * MathF.Tau, new Vector2(1.8f, 1.4f), 1.4f, 0.3f, 0.6f));
            Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Crates, NextSeed(), r.Int(3, 5), 1), W(court + new Vector2(-_side * r.Range(6f, 9f), r.Range(0f, 5f))), r.Float() * MathF.Tau, new Vector2(2.2f, 1.3f), 1.8f, 0.3f, 0.6f));
            Strew(LitterKind.Papers, court + new Vector2(r.Range(-3f, 3f), 1f), 3f, r.Int(6, 10));
        }

        // Glasshouses where the eggs are warmed: one before the far tanks, one down the side by the house.
        private void Glasshouses(WorldRandom r)
        {
            float length = r.Range(15f, 17f), width = r.Range(5.6f, 6.4f);
            var half = new Vector2(length * 0.5f + 0.4f, width * 0.5f + 0.4f);
            Vector2 far = new Vector2(-_side * (Hx - length * 0.5f - 4.5f), -Back + 14.5f);
            Tries(3, () => PutBox(new PlaceSpec(PlacePiece.Hatchery, NextSeed(), PlaceSpec.Cm(length), PlaceSpec.Cm(width)), W(far + new Vector2(0f, r.Range(0f, 2f))), YawOf(Vector2.UnitY), half, Tall, 0.3f, 0.6f, PlacementLayer.Structure));
            Vector2 near = new Vector2(_side * (Hx - width * 0.5f - 4f), Heart.Y - 10.5f - length * 0.5f);
            Tries(3, () => PutBox(new PlaceSpec(PlacePiece.Hatchery, NextSeed(), PlaceSpec.Cm(length), PlaceSpec.Cm(width)), W(near - new Vector2(0f, r.Range(0f, 2f))), YawOf(new Vector2(-_side, 0f)), half, Tall, 0.3f, 0.6f, PlacementLayer.Structure));
            foreach (Vector2 by in new[] { far + new Vector2(0f, 5.5f), near + new Vector2(-_side * 5.5f, 0f) })
            {
                for (int k = 0; k < 2; k++) Lay(new PlaceSpec(PlacePiece.Shells, NextSeed()), W(by + new Vector2(r.Range(-5f, 5f), r.Range(-2f, 2f))), r.Float() * MathF.Tau, 1.4f);
            }
        }

        // By the way in from the front: the shell it came out of, kept, and across the way the post it was broken to the collar at, with the harness it's put in.
        private void Shell(WorldRandom r)
        {
            float size = r.Range(3f, 3.4f);
            Vector2 at = new Vector2(-_side * r.Range(14.5f, 16f), Heart.Y + Yard + 5.4f);
            Tries(6, () => PutRound(new PlaceSpec(PlacePiece.GreatShell, NextSeed(), PlaceSpec.Cm(size)), W(at + new Vector2(r.Range(-1.5f, 1.5f), r.Range(-0.4f, 0.4f))), YawOf(Vector2.UnitY), size * 0.5f + 2.1f, 1.6f, 0.15f, 0.6f));
            float ring = r.Range(3.4f, 3.9f);
            Vector2 post = new Vector2(_side * (Hx - ring - 6.5f), Heart.Y + r.Range(12.5f, 14.5f));
            if (Clear(Footprint.Circle(W(post), ring + 0.5f)) && PutRound(new PlaceSpec(PlacePiece.BreakingPost, NextSeed(), PlaceSpec.Cm(ring)), W(post), r.Float() * MathF.Tau, 0.7f, Tall, 0.3f, 0.6f))
            {
                Paint(GroundPaint.Mud, Footprint.Circle(W(post), ring + 0.8f), 1.5f);
                Tries(6, () => PutBox(new PlaceSpec(PlacePiece.HarnessRack, NextSeed()), W(post + new Vector2(_side * r.Range(5.5f, 7f), r.Range(-1f, 2f))), YawOf(new Vector2(-_side, 0.2f)), new Vector2(2f, 0.8f), 2.4f, 0.3f, 0.6f));
            }
        }

        // The rest of the clutch, kennelled in a front corner: what they're fed and what they're set on.
        private void Young(WorldRandom r)
        {
            int stalls = r.Int(4, 5);
            Vector2 kennel = new Vector2(-_side * (Hx - stalls * 1.4f - 5f), Front - 17f);
            Paint(GroundPaint.Mud, Footprint.Circle(W(kennel + new Vector2(0f, 4f)), 10f), 4f);
            if (PutBox(new PlaceSpec(PlacePiece.Kennel, NextSeed(), stalls, PlaceSpec.Cm(2.8f), PlaceSpec.Cm(3.6f)), W(kennel), YawOf(Vector2.UnitY), new Vector2(stalls * 1.4f + 0.2f, 2f), 3.1f, 0.15f, 0.6f))
            {
                Tries(6, () => PutBox(new PlaceSpec(PlacePiece.BiteDummy, NextSeed(), r.Int(2, 3)), W(kennel + new Vector2(r.Range(-3f, 3f), r.Range(6f, 8f))), r.Float() * MathF.Tau, new Vector2(1.6f, 1f), 1.2f, 0.3f, 0.6f));
                Strew(LitterKind.Straw, kennel + new Vector2(0f, 3.5f), 4f, r.Int(10, 16));
            }
            Tries(6, () => PutBox(new PlaceSpec(PlacePiece.ButcherBlock, NextSeed()), W(kennel + new Vector2(_side * r.Range(8f, 10f), r.Range(1f, 5f))), YawOf(Vector2.UnitY) + r.Range(-0.3f, 0.3f), new Vector2(2.4f, 1.4f), 3f, 0.3f, 0.6f));
            Tries(6, () => PutRound(new PlaceSpec(PlacePiece.Midden, NextSeed(), PlaceSpec.Cm(r.Range(1.6f, 2.2f))), W(kennel + new Vector2(-_side * r.Range(1f, 4f), r.Range(-7f, -5f))), r.Float() * MathF.Tau, 2.3f, 1f, 0.3f, 0.6f));
            Vector2 camp = new Vector2(_side * (Hx - 12f), Front - 12f);
            Camp(camp, Heart - camp, r.Fork("camp"), 3);
        }

        private void Hold(WorldRandom r)
        {
            Garrison(SpawnRole.Guard, SpawnRole.Raider, r.Int(7, 9), r);
            HoardAt(new Vector2(_side * (_radius + r.Range(6f, 8f)), _domeFront - r.Range(2f, 4f)), r.Fork("hoard"));
        }

        // Shell underfoot in the sand.
        private void Underfoot(WorldRandom r)
        {
            for (int k = 0; k < 4; k++)
            {
                float a = r.Float() * MathF.Tau;
                Strew(LitterKind.Feathers, Heart + new Vector2(MathF.Sin(a), MathF.Cos(a)) * r.Range(Yard * 0.3f, Yard * 0.9f), 3.2f, r.Int(8, 14));
            }
        }
    }

    /// <summary>
    /// Lays out the foundry (SEATS.md): where the Reich makes what its tyrant wears, out of what the land had. Its blast furnace stands at the back, its throat burning and its iron
    /// running out down a bed of sand towards the yard; to one side of it the fitting dummy of a tyrant stands in its cradle with the new regalia on it, the harness racks and the
    /// collar forges by it; to the other is what it's fed, the land's bells and ploughs and gates heaped to be melted, coal, and the slag tipped still bright; the land's smiths
    /// work chained in a corner; loaded wagons wait by the gate. Its yard is the sand it casts in.
    /// </summary>
    internal sealed class FoundryBuilder : SeatBuilder
    {
        private float _bedFront, _side = 1f;

        public FoundryBuilder(SiteBuilder b) : base(b, "foundry") { }

        public override void Lay()
        {
            foreach (Side side in GateSides) Plan.Gates.Add((W(GateAt(side)), Dir(Outward(side))));
            LevelGround();
            // Mud is laid over sand wherever both are, so the yard's own ground is left out of it: trodden earth all over, and mud where the work is.
            Paint(GroundPaint.Yard, BoxOf(new Vector2(-Hx + 3f, -Back + 2f), new Vector2(Hx - 3f, Front - 3f)), 5f);
            _side = Random.Chance(0.5f) ? 1f : -1f;
            Furnace(Random.Fork("furnace"));
            Fitting(Random.Fork("fitting"));
            Stock(Random.Fork("stock"));
            IronFence(4.6f, GroundPaint.Plaza);
            GateStandards(MainSide, 8.6f, 9.5f, 4.2f);
            OpenYard(GroundPaint.Sand, 0.97f, 3.5f);
            MudRing(9f);
            Fires(SeatDressing.FireReich, 8, 3.2f, local => !(MathF.Abs(local.X) < 9.5f && local.Y < _bedFront + 5f));
            Smiths(Random.Fork("smiths"));
            Carts(Random.Fork("carts"));
            Hold(Random.Fork("holders"));
            TyrantsLeavings(Random.Fork("yard"), new Vector2(-_side, -0.6f));
            Underfoot(Random.Fork("underfoot"));
        }

        // The furnace at the back, its bed towards the yard.
        private void Furnace(WorldRandom r)
        {
            float height = r.Range(22f, 25f);
            Vector2 at = new Vector2(0f, -Back + ReichWorks.FurnaceFoot + 2.8f);
            _bedFront = at.Y + ReichWorks.FurnaceFoot + ReichWorks.FurnaceBed;
            if (Raise(new PlaceSpec(PlacePiece.BlastFurnace, NextSeed(), PlaceSpec.Cm(height)), at, Vector2.UnitY))
            {
                Chart(at, new Vector2(ReichWorks.FurnaceFoot - 0.2f), Vector2.UnitY, height);
                Chart(at + ReichWorks.StoveAt, new Vector2(ReichWorks.StoveHalf - 0.2f), Vector2.UnitY, height * 0.5f);
                Chart(at + ReichWorks.StackAt, new Vector2(ReichWorks.StackHalf - 0.2f), Vector2.UnitY, height * 0.85f);
                Chart(at + ReichWorks.HoistAt, new Vector2(ReichWorks.HoistHalf - 0.2f), Vector2.UnitY, height);
            }
            Keep(BoxOf(new Vector2(-ReichWorks.BedHalf - 1.2f, at.Y + ReichWorks.FurnaceFoot), new Vector2(ReichWorks.BedHalf + 1.2f, _bedFront + 2.5f)));
        }

        // The dummy the regalia's fitted on, to one side of the bed, facing the camera, and round it what's made for the broods: harness, collars and arms.
        private void Fitting(WorldRandom r)
        {
            // As long as the tyrant it's for, side on to the camera, its head towards the furnace.
            float length = r.Range(21f, 23f);
            Vector2 at = new Vector2(_side * (ReichWorks.BedHalf + 4.5f + length * 0.5f), -Back + 8.5f);
            if (Raise(new PlaceSpec(PlacePiece.Armature, NextSeed(), r.Int(0, 3), PlaceSpec.Cm(length), _side > 0f ? 1 : 0), at, Vector2.UnitY)) Chart(at, new Vector2(length * 0.24f - 0.2f, length * 0.1f - 0.2f), Vector2.UnitY, 8f);
            Keep(Footprint.Box(W(at), new Vector2(length * 0.5f + 1f, 5f), Plan.Yaw));
            Paint(GroundPaint.Plaza, Footprint.Box(W(at), new Vector2(length * 0.5f + 2f, 6.5f), Plan.Yaw), 3f);
            Vector2 bench = at + new Vector2(0f, 9.5f);
            Tries(6, () => PutBox(new PlaceSpec(PlacePiece.CollarForge, NextSeed()), W(bench + new Vector2(r.Range(-3f, 3f), r.Range(-1f, 1f))), YawOf(new Vector2(-_side * 0.2f, 1f)), new Vector2(2.6f, 2.2f), 1.4f, 0.3f, 0.6f));
            for (int k = 0; k < 2; k++) Tries(6, () => PutBox(new PlaceSpec(PlacePiece.HarnessRack, NextSeed()), W(bench + new Vector2(_side * (5.5f + k * 4.4f + r.Range(0f, 1f)), r.Range(-2f, 1f))), YawOf(new Vector2(0.2f, 1f)), new Vector2(2f, 0.8f), 2.4f, 0.3f, 0.6f));
            for (int k = 0; k < 2; k++) Tries(6, () => PutBox(new PlaceSpec(PlacePiece.WeaponRack, NextSeed(), PlaceSpec.Cm(r.Range(2.2f, 2.8f)), 1), W(bench + new Vector2(-_side * (5f + k * 3.6f + r.Range(0f, 1f)), r.Range(-2.5f, 0f))), YawOf(Vector2.UnitY), new Vector2(1.6f, 0.6f), 2f, 0.3f, 0.6f));
            Strew(LitterKind.Embers, bench + new Vector2(0f, 2.5f), 3.2f, r.Int(8, 14));
        }

        // What it's fed, on the other side: the land's metal, with the wagon it came on, coal, and the slag.
        private void Stock(WorldRandom r)
        {
            float s = -_side;
            Paint(GroundPaint.Mud, BoxOf(new Vector2(s * (ReichWorks.BedHalf + 2f), -Back + 2f), new Vector2(s * (Hx - 3f), -Back + 24f)), 4f);
            float scrap = r.Range(7f, 8f);
            Tries(4, () => PutRound(new PlaceSpec(PlacePiece.ScrapHeap, NextSeed(), 0, PlaceSpec.Cm(scrap)), W(new Vector2(s * (ReichWorks.BedHalf + r.Range(10f, 12f)), -Back + r.Range(8.5f, 10.5f))), r.Float() * MathF.Tau, scrap * 0.5f, 1.2f, 0.3f, 0.6f));
            float coal = r.Range(6f, 7f);
            Tries(4, () => PutRound(new PlaceSpec(PlacePiece.ScrapHeap, NextSeed(), 1, PlaceSpec.Cm(coal)), W(new Vector2(s * (ReichWorks.BedHalf + r.Range(21f, 23f)), -Back + r.Range(7f, 8.5f))), r.Float() * MathF.Tau, coal * 0.5f + 1.2f, 1.2f, 0.3f, 0.6f));
            for (int k = 0; k < 2; k++)
            {
                float slag = r.Range(5.4f, 6.6f);
                Vector2 at = k == 0 ? new Vector2(s * (Hx - r.Range(8f, 10f)), -Back + r.Range(18f, 21f)) : new Vector2(s * (ReichWorks.BedHalf + r.Range(7.5f, 9.5f)), _bedFront + r.Range(-1f, 1.5f));
                Tries(4, () => PutRound(new PlaceSpec(PlacePiece.ScrapHeap, NextSeed(), 2, PlaceSpec.Cm(slag)), W(at + new Vector2(r.Range(-1f, 1f), r.Range(-1f, 1f))), r.Float() * MathF.Tau, slag * 0.5f, 1.2f, 0.3f, 0.6f));
            }
            Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Wagon, NextSeed(), 1, 0, PlaceSpec.Cm(r.Range(4f, 4.8f))), W(new Vector2(s * (ReichWorks.BedHalf + r.Range(13f, 18f)), -Back + r.Range(16f, 19f))), YawOf(new Vector2(-s, 0.5f)), new Vector2(1.6f, 4.1f), 2.6f, 0.3f, 0.6f));
            Strew(LitterKind.Gear, new Vector2(s * (ReichWorks.BedHalf + 12f), -Back + 14.5f), 3.4f, r.Int(8, 12));
        }

        // The land's smiths, chained to their work in a front corner: a shelter, their fires and their wood, and the stake.
        private void Smiths(WorldRandom r)
        {
            Vector2 at = new Vector2(-_side * (Hx - 12f), Front - 14f);
            Paint(GroundPaint.Mud, Footprint.Circle(W(at), 10f), 4f);
            Tries(6, () => PutBox(new PlaceSpec(PlacePiece.Shelter, NextSeed(), PlaceSpec.Cm(8f), PlaceSpec.Cm(4.8f)), W(at + new Vector2(r.Range(-1f, 1f), -3f)), YawOf(Vector2.UnitY), new Vector2(4.4f, 2.8f), Tall, 0.25f, 0.6f));
            Tries(6, () => PutBox(new PlaceSpec(PlacePiece.CollarForge, NextSeed()), W(at + new Vector2(r.Range(-3f, 3f), r.Range(2.5f, 4.5f))), YawOf(Vector2.UnitY) + r.Range(-0.4f, 0.4f), new Vector2(2.6f, 2.2f), 1.4f, 0.3f, 0.6f));
            Lay(new PlaceSpec(PlacePiece.ChainStake, NextSeed(), PlaceSpec.Cm(r.Range(2.6f, 3.4f))), W(at + new Vector2(r.Range(-2f, 2f), 1f)), r.Float() * MathF.Tau, 0.5f);
            Tries(6, () => PutBox(new PlaceSpec(PlacePiece.Firewood, NextSeed(), PlaceSpec.Cm(r.Range(2.4f, 3.2f)), PlaceSpec.Cm(r.Range(1.2f, 1.5f))), W(at + new Vector2(_side * r.Range(6f, 8f), r.Range(-2f, 2f))), YawOf(Vector2.UnitY), new Vector2(1.7f, 0.7f), 1.4f, 0.3f, 0.6f));
            Tries(6, () => PutBox(new PlaceSpec(PlacePiece.Barrels, NextSeed(), r.Int(2, 4)), W(at + new Vector2(-_side * r.Range(5f, 7f), r.Range(2f, 5f))), r.Float() * MathF.Tau, new Vector2(1.5f), 1.4f, 0.3f, 0.6f));
            Strew(LitterKind.Embers, at + new Vector2(0f, 3f), 3.4f, r.Int(8, 14));
            Vector2 camp = new Vector2(_side * (Hx - 12f), Front - 12f);
            Camp(camp, Heart - camp, r.Fork("camp"), 3);
        }

        // What goes out: the Reich's wagons, loaded, inside the great gate, and a gibbet for whoever spoils a casting.
        private void Carts(WorldRandom r)
        {
            Vector2 gate = GateAt(MainSide), inward = Inward(MainSide), across = new Vector2(-inward.Y, inward.X);
            foreach (float s in new[] { -1f, 1f })
            {
                Tries(6, () => PutBox(new PlaceSpec(PlacePiece.Wagon, NextSeed(), 0, 1, PlaceSpec.Cm(r.Range(4.2f, 5f))), W(gate + inward * r.Range(9f, 13f) + across * (s * r.Range(9f, 11f))), YawOf(-inward) + r.Range(-0.25f, 0.25f), new Vector2(1.6f, 4.1f), 2.6f, 0.3f, 0.6f));
            }
            Tries(6, () => PutRound(new PlaceSpec(PlacePiece.Gibbet, NextSeed(), PlaceSpec.Cm(r.Range(5.6f, 6.4f))), W(gate - inward * r.Range(13f, 16f) + across * (r.Chance(0.5f) ? 9.5f : -9.5f)), YawOf(-inward), 0.7f, 0f, 0.3f, 0.6f));
        }

        private void Hold(WorldRandom r)
        {
            Garrison(SpawnRole.Guard, SpawnRole.Raider, r.Int(7, 9), r);
            HoardAt(new Vector2(_side * (ReichWorks.BedHalf + 4.6f), _bedFront - r.Range(2.5f, 4f)), r.Fork("hoard"));
        }

        private void Underfoot(WorldRandom r)
        {
            for (int k = 0; k < 4; k++)
            {
                float a = r.Float() * MathF.Tau;
                Strew(LitterKind.Embers, Heart + new Vector2(MathF.Sin(a), MathF.Cos(a)) * r.Range(Yard * 0.4f, Yard + 8f), 3.2f, r.Int(8, 14));
            }
        }
    }

    /// <summary>
    /// Lays out the triumph (SEATS.md): where the Reich raised its tyrant in iron over what the land held holy. The colossus stands at the back on its plinth, roaring over the
    /// yard at whoever comes; a low wall of the Reich's granite runs round the ground, a pylon at each corner and more down the way in, each hung with the shields of the land's
    /// dead; to one side is the stand its commander watches from, the chair empty; in a back corner lies what stood here before, the land's lord in stone and its stones and
    /// lanterns, dragged aside; and in a front corner the land's masons cut the next plinth in chains.
    /// </summary>
    internal sealed class TriumphBuilder : SeatBuilder
    {
        private Vector2 _plinth;
        private float _plinthFront, _side = 1f;
        private (int R, int G, int B) _stone;

        public TriumphBuilder(SiteBuilder b) : base(b, "triumph") { }

        public override void Lay()
        {
            _stone = LandStone;
            foreach (Side side in GateSides) Plan.Gates.Add((W(GateAt(side)), Dir(Outward(side))));
            LevelGround();
            Paint(GroundPaint.Yard, BoxOf(new Vector2(-Hx + 3f, -Back + 2f), new Vector2(Hx - 3f, Front - 3f)), 5f);
            _side = Random.Chance(0.5f) ? 1f : -1f;
            Statue(Random.Fork("colossus"));
            Walls(Random.Fork("walls"));
            OpenYard(GroundPaint.Sand, 0.97f, 3.5f);
            MudRing(6f, GroundPaint.Plaza);
            Fires(SeatDressing.FireReich, 8, 3.2f, local => !(MathF.Abs(local.X) < _plinth.X + 4f && local.Y < _plinthFront + 5f));
            Avenue(Random.Fork("avenue"));
            Reviewing(Random.Fork("stand"));
            Toppled(Random.Fork("toppled"));
            Masons(Random.Fork("masons"));
            Hold(Random.Fork("holders"));
            TyrantsLeavings(Random.Fork("yard"), new Vector2(-_side, -0.6f));
        }

        // The colossus, at the back, facing the camera, the Reich's standards either side of it.
        private void Statue(WorldRandom r)
        {
            // Half as long again as the tyrant lives, side on to the camera, and facing the great gate if that's at a side.
            float length = Math.Clamp(R * 0.55f, 32f, 36f), high = r.Range(5f, 6f);
            int facing = MainSide == Side.Left ? 1 : MainSide == Side.Right ? 0 : r.Int(0, 1);
            _plinth = ReichField.PlinthOf(length);
            Vector2 at = new Vector2(0f, -Back + _plinth.Y + 3.4f);
            _plinthFront = at.Y + _plinth.Y;
            if (Raise(new PlaceSpec(PlacePiece.Colossus, NextSeed(), r.Int(0, 3), PlaceSpec.Cm(length), PlaceSpec.Cm(high), facing), at, Vector2.UnitY)) Chart(at, _plinth - new Vector2(0.3f), Vector2.UnitY, Tall);
            Paint(GroundPaint.Plaza, BoxOf(at - _plinth - new Vector2(5f, 2f), at + _plinth + new Vector2(5f, 6f)), 2.5f);
            Keep(BoxOf(new Vector2(-_plinth.X - 1.5f, _plinthFront), new Vector2(_plinth.X + 1.5f, _plinthFront + 3f)));
            uint seed = NextSeed();
            foreach (float s in new[] { -1f, 1f }) Standard(new Vector2(s * (_plinth.X + 4f), _plinthFront + 1.5f), Vector2.UnitY, 12f, 2.4f, 7f, seed);
        }

        // A low wall of the Reich's granite round it, piers with fire on them at each way in, and a tall pylon at each corner.
        private void Walls(WorldRandom r)
        {
            const float GateHalf = 5f, Height = 3f, Thick = 1.2f;
            (int R, int G, int B) granite = SeatKit.ReichStone;
            Curtain(new Vector2(-Hx, -Back), new Vector2(Hx, -Back), Height, Thick, granite, Fortress.WallLow);
            foreach (float s in new[] { -1f, 1f })
            {
                Curtain(new Vector2(s * Hx, -Back), new Vector2(s * Hx, Heart.Y - GateHalf - 2f), Height, Thick, granite, Fortress.WallLow);
                Curtain(new Vector2(s * Hx, Heart.Y + GateHalf + 2f), new Vector2(s * Hx, Front), Height, Thick, granite, Fortress.WallLow);
                Curtain(new Vector2(s * Hx, Front), new Vector2(s * (GateHalf + 2f), Front), Height, Thick, granite, Fortress.WallLow);
            }
            uint pier = NextSeed(), corner = NextSeed();
            foreach (Side side in GateSides)
            {
                Vector2 at = GateAt(side), outward = Outward(side), across = new Vector2(-outward.Y, outward.X);
                bool great = side == MainSide;
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector2 world = W(at + across * (s * (GateHalf + 1f)));
                    float high = great ? 6.4f : 5.2f;
                    Put(new PlaceSpec(PlacePiece.GatePier, great ? pier : pier + 1u, PlaceSpec.Cm(high), 1, 0), world, YawOf(outward), Footprint.Box(world, new Vector2(0.95f, 0.95f), YawOf(outward)), high, PlacementLayer.Prop, 0.6f, 0.05f, PlacementLayer.Structure | PlacementLayer.Prop | PlacementLayer.Road);
                }
                Keep(Footprint.Box(W(at), new Vector2(GateHalf - 1f, 9f), YawOf(Inward(side))));
                Trail(at + outward * 3f, Heart + (at - Heart).Normalized() * (Yard + 2f), great ? 7f : 5f, GroundPaint.Plaza, 1.6f);
            }
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sy in new[] { -Back, Front })
                {
                    Vector2 at = new Vector2(sx * Hx, sy);
                    Vector2 facing = (Heart - at).Normalized();
                    if (Raise(new PlaceSpec(PlacePiece.TrophyPylon, corner, PlaceSpec.Cm(9.5f), 0), at, MathF.Abs(facing.X) > MathF.Abs(facing.Y) ? new Vector2(MathF.Sign(facing.X), 0f) : Vector2.UnitY, offRoads: true)) Chart(at, new Vector2(0.8f), Vector2.UnitY, 9.5f);
                }
            }
            GateStandards(MainSide, 9.2f, 9.5f, 5f);
        }

        // Pylons round the yard between its fires, the two at the front burning, a pair either side of the way in from the front, and two by the colossus.
        private void Avenue(WorldRandom r)
        {
            uint lit = NextSeed(), dark = NextSeed();
            for (int k = 0; k < 4; k++)
            {
                float a = (k + 0.5f) * MathF.PI * 0.5f;
                bool front = MathF.Cos(a) > 0f;
                Vector2 at = Heart + new Vector2(MathF.Sin(a), MathF.Cos(a)) * (Yard + 6.5f);
                PutBox(new PlaceSpec(PlacePiece.TrophyPylon, front ? lit : dark, PlaceSpec.Cm(7.5f), front ? 0 : 2), W(at), YawOf(Vector2.UnitY), new Vector2(1.1f, 1.1f), 7.5f, 0.2f, 0.6f);
            }
            foreach (float s in new[] { -1f, 1f })
            {
                PutBox(new PlaceSpec(PlacePiece.TrophyPylon, dark + 1u, PlaceSpec.Cm(6.5f), 2), W(new Vector2(s * 6.8f, Heart.Y + Yard + 5.5f)), YawOf(Vector2.UnitY), new Vector2(1.1f, 1.1f), 6.5f, 0.2f, 0.6f);
                PutBox(new PlaceSpec(PlacePiece.TrophyPylon, lit + 1u, PlaceSpec.Cm(8f), 1), W(new Vector2(s * (_plinth.X + 9.5f), _plinthFront - 2f)), YawOf(Vector2.UnitY), new Vector2(1.1f, 1.1f), 8f, 0.2f, 0.6f);
            }
        }

        // The stand its commander watches from, at one side, facing the yard.
        private void Reviewing(WorldRandom r)
        {
            float width = r.Range(11f, 13f);
            Vector2 at = new Vector2(_side * (Hx - ReichField.DaisDepth * 0.5f - 2.8f), Heart.Y - width * 0.5f - 9f), facing = new Vector2(-_side, 0f);
            if (Raise(new PlaceSpec(PlacePiece.Dais, NextSeed(), PlaceSpec.Cm(width)), at, facing)) Chart(at, new Vector2(width * 0.5f - 0.3f, ReichField.DaisDepth * 0.5f - 0.3f), facing, 5f);
            Keep(Footprint.Box(W(at + facing * (ReichField.DaisDepth * 0.5f + 2f)), new Vector2(width * 0.5f, 2f), YawOf(facing)));
            Paint(GroundPaint.Plaza, Footprint.Box(W(at + facing * 4f), new Vector2(width * 0.5f + 2f, 7f), YawOf(facing)), 2.5f);
        }

        // What stood here before, in a back corner: the land's lord in stone, pulled down, and round him its stones, its lanterns, its bell and what was left at them.
        private void Toppled(WorldRandom r)
        {
            Vector2 at = new Vector2(-_side * (Hx - 15f), -Back + 14f);
            Paint(GroundPaint.Yard, Footprint.Circle(W(at), 12f), 5f);
            Tries(4, () => PutRound(new PlaceSpec(PlacePiece.FallenLord, NextSeed(), PlaceSpec.Cm(r.Range(6f, 7.5f)), _stone.R, _stone.G, _stone.B), W(at + new Vector2(r.Range(-2f, 2f), r.Range(-3f, 0f))), YawOf(Vector2.UnitY) + r.Range(-0.35f, 0.35f), 2.4f, 2.3f, 0.2f, 0.6f));
            for (int k = 0; k < 3; k++)
            {
                float height = r.Range(3.2f, 4.4f), width = r.Range(1.6f, 2.2f);
                Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Menhir, NextSeed(), PlaceSpec.Cm(height), PlaceSpec.Cm(width), 3, 0, _stone.R, _stone.G, _stone.B), W(at + new Vector2(r.Range(-9f, 9f), r.Range(-8f, 8f))), r.Float() * MathF.Tau, new Vector2(width * 0.6f, height * 0.5f), 1f, 0.2f, 0.6f));
            }
            for (int k = 0; k < 2; k++) Tries(8, () => PutRound(new PlaceSpec(PlacePiece.ShrineLantern, NextSeed(), PlaceSpec.Cm(r.Range(3.4f, 4f)), k == 0 ? 1 : 2, _stone.R, _stone.G, _stone.B), W(at + new Vector2(r.Range(-8f, 8f), r.Range(-7f, 7f))), r.Float() * MathF.Tau, 1.2f, 2f, 0.3f, 0.6f));
            Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Bell, NextSeed(), 1), W(at + new Vector2(r.Range(-7f, 7f), r.Range(2f, 7f))), r.Float() * MathF.Tau, new Vector2(1.5f, 1f), 3.4f, 0.3f, 0.6f));
            Tries(8, () => PutRound(new PlaceSpec(PlacePiece.Offerings, NextSeed(), 1, _stone.R, _stone.G, _stone.B), W(at + new Vector2(r.Range(-6f, 6f), r.Range(3f, 8f))), r.Float() * MathF.Tau, 1.6f, 1f, 0.3f, 0.6f, solid: false));
            Strew(LitterKind.Rubble, at + new Vector2(0f, 3f), 4f, r.Int(12, 18), _stone);
            Strew(LitterKind.Rubble, at + new Vector2(_side * 6f, -3f), 3.4f, r.Int(8, 14), _stone);
        }

        // The land's masons, in a front corner, cutting the next plinth in chains: a crane, blocks stacked and half worked, and the stake.
        private void Masons(WorldRandom r)
        {
            (int R, int G, int B) g = SeatKit.ReichStone;
            Vector2 at = new Vector2(-_side * (Hx - 13f), Front - 14f);
            Paint(GroundPaint.Chippings, Footprint.Circle(W(at), 10f), 4f);
            Tries(6, () => PutBox(new PlaceSpec(PlacePiece.Crane, NextSeed(), PlaceSpec.Cm(r.Range(7f, 9f)), g.R, g.G, g.B), W(at + new Vector2(r.Range(-2f, 2f), r.Range(-3f, -1f))), YawOf(Vector2.UnitY), new Vector2(3.6f, 3f), 2.5f, 0.3f, 0.6f));
            for (int k = 0; k < 2; k++) Tries(8, () => PutBox(new PlaceSpec(PlacePiece.BlockStack, NextSeed(), r.Int(5, 9), g.R, g.G, g.B), W(at + new Vector2(r.Range(-8f, 8f), r.Range(2f, 7f))), YawOf(Vector2.UnitX) + r.Range(-0.2f, 0.2f), new Vector2(2.6f, 1.2f), 1.8f, 0.3f, 0.6f));
            for (int k = 0; k < 2; k++) Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Banker, NextSeed(), g.R, g.G, g.B), W(at + new Vector2(r.Range(-7f, 7f), r.Range(-6f, 6f))), r.Float() * MathF.Tau, new Vector2(1.8f, 1.4f), 1.1f, 0.3f, 0.6f));
            Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Sledge, NextSeed(), g.R, g.G, g.B), W(at + new Vector2(_side * r.Range(6f, 9f), r.Range(-2f, 4f))), YawOf(new Vector2(_side, 0.4f)), new Vector2(1.2f, 3f), 1.8f, 0.3f, 0.6f));
            Lay(new PlaceSpec(PlacePiece.ChainStake, NextSeed(), PlaceSpec.Cm(r.Range(2.6f, 3.4f))), W(at + new Vector2(r.Range(-2f, 2f), 2f)), r.Float() * MathF.Tau, 0.5f);
            Strew(LitterKind.Spalls, at + new Vector2(0f, 1f), 4f, r.Int(12, 18), g);
            Vector2 camp = new Vector2(_side * (Hx - 12f), Front - 12f);
            Camp(camp, Heart - camp, r.Fork("camp"), 3);
        }

        private void Hold(WorldRandom r)
        {
            Garrison(SpawnRole.Guard, SpawnRole.Raider, r.Int(8, 10), r);
            HoardAt(new Vector2(_side * (_plinth.X + 10.8f), _plinthFront + r.Range(2.6f, 3.4f)), r.Fork("hoard"));
        }
    }

    /// <summary>
    /// Lays out the war camp (SEATS.md): a brood in the field, which has taken no house of the land's and built none. Its wall is its wagons, chained end to end round the whole
    /// of it, plated on the side they turn to the land, a bolt thrower on every third; its commander's pavilion stands at the back, the maps of the land out under its fly; the
    /// cage its tyrant came in is drawn up by the yard with its door let down; its tents stand in rows, its raptors are kennelled in a corner, its stores are stacked off the
    /// wagons that brought them, and all of it is mud.
    /// </summary>
    internal sealed class WarcampBuilder : SeatBuilder
    {
        private float _tentFront, _side = 1f;

        public WarcampBuilder(SiteBuilder b) : base(b, "warcamp") { }

        public override void Lay()
        {
            foreach (Side side in GateSides) Plan.Gates.Add((W(GateAt(side)), Dir(Outward(side))));
            LevelGround();
            Paint(GroundPaint.Yard, BoxOf(new Vector2(-Hx + 3f, -Back + 2f), new Vector2(Hx - 3f, Front - 3f)), 5f);
            _side = Random.Chance(0.5f) ? 1f : -1f;
            Tent(Random.Fork("pavilion"));
            Wagons(Random.Fork("wagons"));
            OpenYard(GroundPaint.Mud, 1f, 3f);
            MudRing(7f);
            Fires(SeatDressing.FireReich, 8, 3.2f, local => !(MathF.Abs(local.X) < 9f && local.Y < _tentFront + 4f));
            Cage(Random.Fork("cage"));
            Lines(Random.Fork("lines"));
            Beasts(Random.Fork("beasts"));
            Stores(Random.Fork("stores"));
            Hold(Random.Fork("holders"));
            TyrantsLeavings(Random.Fork("yard"), new Vector2(-_side, -0.6f));
            Underfoot(Random.Fork("underfoot"));
        }

        // The pavilion at the back, its door to the yard.
        private void Tent(WorldRandom r)
        {
            float width = Math.Clamp(R * 0.4f, 23f, 26f), depth = r.Range(12f, 13.2f), hz = depth * 0.5f;
            Vector2 at = new Vector2(0f, -Back + hz + 3.8f);
            _tentFront = at.Y + hz + ReichField.PavilionFly + 4.2f;
            (float middle, float wing, float off) = ReichField.PavilionBays(width);
            if (Raise(new PlaceSpec(PlacePiece.Pavilion, NextSeed(), PlaceSpec.Cm(width), PlaceSpec.Cm(depth)), at, Vector2.UnitY))
            {
                Chart(at, new Vector2(middle - 0.2f, hz - 0.2f), Vector2.UnitY, 7f);
                foreach (float s in new[] { -1f, 1f }) Chart(at + new Vector2(s * off, -hz * 0.15f), new Vector2(wing - 0.2f, hz * 0.8f - 0.2f), Vector2.UnitY, 5f);
            }
            Keep(BoxOf(new Vector2(-6.5f, at.Y + hz), new Vector2(6.5f, _tentFront + 1f)));
            Trail(new Vector2(0f, _tentFront), Heart + new Vector2(0f, -Yard + 1f), 5f, GroundPaint.Mud, 2f);
        }

        // Its wall of wagons, round all of it, a gap at each way in with the stakes that close it by night dragged aside and a cresset either side.
        private void Wagons(WorldRandom r)
        {
            const float GateHalf = 5.5f, Length = 7.5f, Inset = 2.6f;
            uint seed = NextSeed();
            int n = 0;
            void Run(Vector2 from, Vector2 to)
            {
                Vector2 d = to - from;
                float length = d.Length;
                if (length < Length + 0.6f) return;
                d /= length;
                int count = Math.Max(1, (int)(length / (Length + 1.2f)));
                float each = length / count;
                Vector2 inward = new Vector2(-d.Y, d.X);
                if (Vector2.Dot(Heart - (from + to) * 0.5f, inward) < 0f) inward = -inward;
                for (int i = 0; i < count; i++, n++)
                {
                    Vector2 middle = from + d * ((i + 0.5f) * each);
                    var spec = new PlaceSpec(PlacePiece.WarWagon, seed + (uint)(n % 2), PlaceSpec.Cm(Length), n % 3 == 1 ? 1 : 0);
                    if (Raise(spec, middle, inward, offRoads: true)) Chart(middle, new Vector2(Length * 0.5f - 0.2f, ReichField.WagonHalf), inward, 3.6f);
                }
            }
            Run(new Vector2(-Hx + Inset, -Back), new Vector2(Hx - Inset, -Back));
            foreach (float s in new[] { -1f, 1f })
            {
                Run(new Vector2(s * Hx, -Back + Inset), new Vector2(s * Hx, Heart.Y - GateHalf));
                Run(new Vector2(s * Hx, Heart.Y + GateHalf), new Vector2(s * Hx, Front - Inset));
                Run(new Vector2(s * (Hx - Inset), Front), new Vector2(s * GateHalf, Front));
            }
            uint stakes = NextSeed(), torch = NextSeed();
            foreach (Side side in GateSides)
            {
                Vector2 at = GateAt(side), outward = Outward(side), across = new Vector2(-outward.Y, outward.X);
                bool great = side == MainSide;
                Keep(Footprint.Box(W(at), new Vector2(GateHalf - 1.6f, 9f), YawOf(Inward(side))));
                Trail(at + outward * 3f, Heart + (at - Heart).Normalized() * (Yard + 2f), great ? 6.5f : 5f, GroundPaint.Mud, 1.8f);
                foreach (float s in new[] { -1f, 1f })
                {
                    PutRound(new PlaceSpec(PlacePiece.Torch, torch, PlaceSpec.Cm(3.8f), 1), W(at + across * (s * (GateHalf - 0.9f)) - outward * 0.5f), 0f, 0.4f, 0f, 0.1f, 0.6f);
                    PutBox(new PlaceSpec(PlacePiece.Barricade, stakes, PlaceSpec.Cm(4.6f), 1), W(at + outward * r.Range(5f, 7f) + across * (s * r.Range(8f, 9.5f))), YawOf(outward) + s * r.Range(0.2f, 0.6f), new Vector2(2.5f, 0.7f), 1.6f, 0.2f, 0.6f);
                }
            }
            GateStandards(MainSide, 9.4f, 9.5f, 3.4f);
        }

        // The cage its tyrant came in, drawn up by the yard, its door let down towards where the tyrant waits.
        private void Cage(WorldRandom r)
        {
            float length = r.Range(11.5f, 13f);
            Vector2 way = new Vector2(_side * 0.74f, -0.67f);
            Vector2 at = Heart + way * (Yard + length * 0.5f + 6.5f);
            Raise(new PlaceSpec(PlacePiece.CageWagon, NextSeed(), PlaceSpec.Cm(length)), at, -way);
            Keep(Footprint.Box(W(at - way * (length * 0.5f + 2.5f)), new Vector2(ReichField.CageHalf + 0.6f, 3.2f), YawOf(-way)));
            Strew(LitterKind.Straw, at - way * (length * 0.5f + 3f), 3.4f, r.Int(10, 16));
            Strew(LitterKind.Blood, at - way * (length * 0.5f + 5f), 2.6f, r.Int(4, 7));
        }

        // Its tents in rows on one side, and the brood's own command tent by them.
        private void Lines(WorldRandom r)
        {
            float s = -_side;
            Vector2 rows = new Vector2(s * (Hx - 15f), -Back + 11f);
            Camp(rows, Vector2.UnitY, r.Fork("rows"), 4);
            float length = r.Range(6f, 7f);
            Vector2 command = new Vector2(s * (Hx - 9f), Heart.Y - 16f);
            Tries(4, () => PutBox(new PlaceSpec(PlacePiece.CommandTent, NextSeed(), PlaceSpec.Cm(length)), W(command + new Vector2(0f, r.Range(-2f, 0f))), YawOf(new Vector2(-s, 0f)), new Vector2(3.4f, length * 0.5f + 1.6f), 4f, 0.3f, 0.6f));
            Vector2 second = new Vector2(_side * (Hx - 13f), -Back + 9f);
            Camp(second, Vector2.UnitY, r.Fork("second"), 3);
        }

        // Its raptors, kennelled in a front corner, with what they're fed and what they're set on.
        private void Beasts(WorldRandom r)
        {
            int stalls = r.Int(4, 6);
            Vector2 kennel = new Vector2(_side * (Hx - stalls * 1.4f - 5.5f), Front - 17f);
            if (PutBox(new PlaceSpec(PlacePiece.Kennel, NextSeed(), stalls, PlaceSpec.Cm(2.8f), PlaceSpec.Cm(3.6f)), W(kennel), YawOf(Vector2.UnitY), new Vector2(stalls * 1.4f + 0.2f, 2f), 3.1f, 0.15f, 0.6f))
            {
                Tries(6, () => PutBox(new PlaceSpec(PlacePiece.BiteDummy, NextSeed(), r.Int(2, 3)), W(kennel + new Vector2(r.Range(-3f, 3f), r.Range(6f, 8f))), r.Float() * MathF.Tau, new Vector2(1.6f, 1f), 1.2f, 0.3f, 0.6f));
                Strew(LitterKind.Straw, kennel + new Vector2(0f, 3.5f), 4f, r.Int(10, 16));
                Strew(LitterKind.Bones, kennel + new Vector2(_side * -5f, 5f), 3f, r.Int(8, 14));
            }
            Tries(6, () => PutBox(new PlaceSpec(PlacePiece.ButcherBlock, NextSeed()), W(kennel + new Vector2(-_side * r.Range(8.5f, 10.5f), r.Range(1f, 5f))), YawOf(Vector2.UnitY) + r.Range(-0.3f, 0.3f), new Vector2(2.4f, 1.4f), 3f, 0.3f, 0.6f));
            Tries(6, () => PutRound(new PlaceSpec(PlacePiece.Midden, NextSeed(), PlaceSpec.Cm(r.Range(1.6f, 2.2f))), W(kennel + new Vector2(_side * r.Range(1f, 4f), r.Range(-7f, -5f))), r.Float() * MathF.Tau, 2.3f, 1f, 0.3f, 0.6f));
        }

        // Its stores, in the other front corner: the wagons that brought them, what's been stacked off them, its hay, its field forge and its gibbet.
        private void Stores(WorldRandom r)
        {
            float s = -_side;
            Vector2 at = new Vector2(s * (Hx - 12f), Front - 14f);
            for (int k = 0; k < 3; k++)
            {
                Vector2 wagon = at + new Vector2(-s * (k * 4.6f - 2f), -3f);
                Tries(4, () => PutBox(new PlaceSpec(PlacePiece.Wagon, NextSeed(), 0, 1, PlaceSpec.Cm(r.Range(4.2f, 5f))), W(wagon + new Vector2(r.Range(-0.5f, 0.5f), r.Range(-1f, 1f))), YawOf(Vector2.UnitY) + r.Range(-0.2f, 0.2f), new Vector2(1.6f, 4.1f), 2.6f, 0.3f, 0.6f));
            }
            for (int k = 0; k < 3; k++) Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Crates, NextSeed(), r.Int(3, 5), 1), W(at + new Vector2(r.Range(-8f, 8f), r.Range(4f, 8f))), r.Float() * MathF.Tau, new Vector2(2.2f, 1.3f), 1.8f, 0.3f, 0.6f));
            for (int k = 0; k < 2; k++) Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Barrels, NextSeed(), r.Int(3, 5)), W(at + new Vector2(r.Range(-8f, 8f), r.Range(3f, 8f))), r.Float() * MathF.Tau, new Vector2(1.5f), 1.4f, 0.3f, 0.6f));
            Tries(6, () => PutRound(new PlaceSpec(PlacePiece.Haystack, NextSeed(), PlaceSpec.Cm(r.Range(1.4f, 1.8f))), W(at + new Vector2(s * r.Range(3f, 6f), r.Range(-9f, -7f))), r.Float() * MathF.Tau, 2.1f, Tall, 0.3f, 0.6f));
            Tries(6, () => PutBox(new PlaceSpec(PlacePiece.CollarForge, NextSeed()), W(at + new Vector2(-s * r.Range(9f, 11f), r.Range(3f, 6f))), YawOf(Vector2.UnitY) + r.Range(-0.4f, 0.4f), new Vector2(2.6f, 2.2f), 1.4f, 0.3f, 0.6f));
            Vector2 gate = GateAt(MainSide), outward = Outward(MainSide), across = new Vector2(-outward.Y, outward.X);
            Tries(6, () => PutRound(new PlaceSpec(PlacePiece.Gibbet, NextSeed(), PlaceSpec.Cm(r.Range(5.6f, 6.4f))), W(gate + outward * r.Range(13f, 16f) + across * (r.Chance(0.5f) ? 11f : -11f)), YawOf(outward), 0.7f, 0f, 0.3f, 0.6f));
        }

        private void Hold(WorldRandom r)
        {
            Garrison(SpawnRole.Guard, SpawnRole.Raider, r.Int(9, 11), r);
            HoardAt(new Vector2(-_side * r.Range(15f, 17f), _tentFront - r.Range(6f, 8f)), r.Fork("hoard"));
        }

        // What an army drops.
        private void Underfoot(WorldRandom r)
        {
            for (int k = 0; k < 3; k++)
            {
                float a = r.Float() * MathF.Tau;
                Strew(LitterKind.Gear, Heart + new Vector2(MathF.Sin(a), MathF.Cos(a)) * r.Range(Yard + 3f, Yard + 10f), 3f, r.Int(6, 10));
            }
        }
    }
}

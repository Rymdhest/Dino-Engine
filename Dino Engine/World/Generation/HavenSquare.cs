using Dino_Engine.Modelling.Procedural.Architecture.Plaza;
using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.World.Generation.SiteBuilder;

namespace Dino_Engine.World.Generation
{
    /// <summary>
    /// The heart of the Haven, built as a town's square is, in layers, so that it reads from the game's camera, high in the south looking north. At its head the great waygate stands on
    /// a terrace of dressed stone two metres up, reached by a broad flight between cheek walls whose piers carry lanterns, with the map table at its foot; lawns and blossom either side
    /// of the gate. Down from it an avenue runs between raised beds walled in field stone into a round square paved in the gate's stone, with a sakura in a bed in its middle whose
    /// wall is a seat, bands of kerbs ringing it, and beds of field stone round its southern half, broken by an arched gateway where the lane comes in from the south. A loggia of stone
    /// columns under a sail shades tables laid for a meal on its east side, by the way to the Gaming House; the market keeps its west side; and bunting crosses it all from four masts.
    /// Quiet in the middle and rich at the edges, as anywhere people gather is, with every way through it at least a body wide. Every piece is grown from its id (SQUARE.md,
    /// <see cref="PlazaForge"/>), and the ground under it is raised exactly to it (<see cref="WorldData.Terraces"/>), the walls hiding where it's raised. Positions are metres east and
    /// north of the square's middle.
    /// </summary>
    internal sealed class HavenSquare
    {
        // ---------------------------------------------------------------- The plan

        public const float SquareRadius = 15.6f;            // Of the paving, to the band round its edge.
        public const float TableAhead = 13.2f;              // The map table's middle, north of the square's.
        private const float Footing = 8.4f;                 // Where the Haven's people stand, north of the square's middle.

        // The terrace the gate stands on: its walls' faces, and how high it is.
        public const float TerraceFront = 19f;
        public const float TerraceBack = 44f;
        public const float TerraceSide = 19.5f;
        public const int Steps = 5;
        public const float Rise = 0.4f;                     // A step's rise and tread at a person's 4.6 m: a real 15 cm and 38 cm.
        public const float Tread = 1f;
        public static float TerraceUp => Steps * Rise;
        private const float FlightHalf = 6f;
        private const float Cheek = 1.6f;
        private const float PierOut = 1.4f;
        private const float Parapet = 0.85f;                // How far the terrace's walls stand above it.
        private const float TerraceThick = 1.3f;
        private const float CornerPier = 1.8f;

        // Raised beds of field stone: how high their walls stand, and their soil.
        private const float BedTop = 1.6f;
        private const float BedSoil = 1.2f;
        private const float BedThick = 1.2f;
        private const float Corner = 1.4f;                  // Of a bed's rounded corners: no more than the inset below, or its ground shows past the wall there.

        // How far inside a wall's face the raised ground under it stops: a grid of heights a metre apart lays a step as a slope up to 1.42 m wide, which has to fall
        // inside the wall and the soil banked behind it, never in front.
        private const float Inset = 1.47f;
        private const float Sharp = 0.02f;                  // A raised pad's margin: none to speak of, as its walls hide its edge.

        // The beds either side of the avenue, against the terrace.
        private const float AvenueHalf = 7.95f;             // Just clear of the flight's piers, so a bed and a pier stand together.
        private const float BedOuter = 17.6f;
        private const float BedFront = 12.6f;

        // The round bed in the middle, and its seat of a wall.
        private const float HeartRadius = 4.6f;
        private const float HeartTop = 1.3f;
        private const float HeartSoil = 0.95f;
        private const float HeartThick = 0.95f;
        private const float Collar = 6.6f;
        private const int Spokes = 8;

        // The beds round the southern half: an arc of ground between two walls, each end closed by a half circle.
        private const float RingInner = 17.2f;
        private const float RingOuter = 22.6f;
        private const float RingFrom = 124f;                // Bearings, clockwise from north, of the east bed's ends; the west's are mirrored.
        private const float RingTo = 158f;

        // The arch over the lane in from the south.
        private const float ArchSpan = 4.8f;
        private const float ArchSpring = 4.2f;
        private const float ArchDepth = 1.4f;
        private const float ArchPier = 1.25f;
        public static float ArchAt => -(RingInner + RingOuter) * 0.5f;

        // The loggia on the east side, its length running north and south.
        private static readonly Vector2 LoggiaAt = new Vector2(24.2f, -2.8f);
        private const float LoggiaLength = 11f;
        private const float LoggiaDepth = 6.4f;
        private const int LoggiaBays = 4;
        private const float LoggiaHeight = 4.9f;

        private static readonly Vector2 WellAt = new Vector2(7f, -8f);
        private const float WellRadius = 1.75f;

        // Masts for the bunting.
        private const float MastHeight = 8.6f;
        private static readonly Vector2[] Masts = { new(-10.6f, 10f), new(10.6f, 10f), new(-12.4f, -10.4f), new(12.4f, -10.4f) };

        private static readonly Vector2 North = new Vector2(0f, 1f);
        private static readonly Vector2 East = new Vector2(1f, 0f);

        private readonly SiteBuilder _b;
        private readonly Vector2 _c;
        private readonly WorldRandom _random;
        private uint _seed;

        /// <summary>Where people stand in the square, which everything in it is laid from.</summary>
        public readonly float Level;

        public HavenSquare(SiteBuilder b)
        {
            _b = b;
            _c = b.Centre;
            _random = b.Random.Fork("square");
            _seed = 0x5A0A2E00u;
            Level = AverageGround(b.World, _c, 20f);
        }

        private WorldData World => _b.World;

        /// <summary>The square's ground, laid before the roads, as the roads into it are planned to meet it.</summary>
        public void LayGround()
        {
            // The Haven's middle is the heart's bed, which nobody stands in: its people stand on the paving between it and the map table.
            _b.Site.Footing = _c + North * Footing;
            World.Pads.Add(new GroundPad(Footprint.Box(_c + new Vector2(0f, -2f), new Vector2(30f, 22f), 0f), Level, 6f));
        }

        /// <summary>Everything else, once the gate and the map table stand and the streets are laid.</summary>
        public void Build()
        {
            Well();
            Terrace();
            Flight();
            AvenueBeds();
            Heart();
            SouthBeds();
            Arch();
            Loggia();
            Bunting();
            Paving();
            Plant();

            // The open paving of the square and its avenue is kept as laid: nothing's put down on it later, though what's planted in its middle grows.
            Garden(Footprint.Circle(_c, SquareRadius - 0.3f));
            Garden(Footprint.Box(_c + new Vector2(0f, (TableAhead - 5f + TerraceFront) * 0.5f), new Vector2(AvenueHalf - 0.2f, (TerraceFront - TableAhead + 5f) * 0.5f), 0f));
        }

        /// <summary>People about the square, on its paving between the heart's bed and the beds round it.</summary>
        public void People(int count)
        {
            for (int i = 0; i < count; i++)
            {
                _b.Spawn(SpawnRole.Villager, _c + Toward(_random.Range(0f, 360f)) * _random.Range(7.6f, 13.4f));
            }
        }

        // ---------------------------------------------------------------- The terrace

        private void Terrace()
        {
            float up = TerraceUp;
            float top = up + PlazaStone.Ground + Parapet;
            float cornerX = TerraceSide - 0.5f, cornerHalf = CornerPier * 0.5f;
            float innerX = cornerX - cornerHalf;

            // The front, either side of the flight, then the sides and the back, each running between corner piers.
            Run(new Vector2(FlightHalf + Cheek, TerraceFront), new Vector2(innerX, TerraceFront), -North, WallForm.Dressed, top, up, TerraceThick);
            Run(new Vector2(-FlightHalf - Cheek, TerraceFront), new Vector2(-innerX, TerraceFront), -North, WallForm.Dressed, top, up, TerraceThick);
            float southInner = TerraceFront + 1f + cornerHalf - 0.5f, northInner = TerraceBack - 1f - cornerHalf + 0.5f;
            foreach (float s in new[] { -1f, 1f })
            {
                Run(new Vector2(s * TerraceSide, southInner), new Vector2(s * TerraceSide, northInner), East * s, WallForm.Dressed, top, up, TerraceThick);
            }
            Run(new Vector2(-innerX, TerraceBack), new Vector2(innerX, TerraceBack), North, WallForm.Dressed, top, up, TerraceThick);

            // Corner piers, the front two carrying urns of flowers.
            foreach (float s in new[] { -1f, 1f })
            {
                Pier(new Vector2(s * cornerX, TerraceFront + 0.5f), CornerPier, top + 0.45f, crown: 2, flowers: Undergrowth.Lavender);
                Pier(new Vector2(s * cornerX, TerraceBack - 0.5f), CornerPier, top + 0.45f, crown: 0, flowers: null);
            }

            // The ground, two metres up and level, its edges inside the walls.
            Raise(Footprint.Box(_c + new Vector2(0f, (TerraceFront + TerraceBack) * 0.5f), new Vector2(TerraceSide - Inset, (TerraceBack - TerraceFront) * 0.5f - Inset), 0f), up);
        }

        // The flight up to it, with the map table at its foot: steps between cheeks, piers with lanterns out in front, and the ground under it laid to its pitch, at the inner
        // corners of its steps, so the drawn ground never comes up through a tread.
        private void Flight()
        {
            float up = TerraceUp;
            float top = up + PlazaStone.Ground + Parapet;
            const float Landing = 0.8f;
            Piece(new PlazaSpec(PlazaKind.Steps, NextSeed(), Cm(FlightHalf * 2f), Steps, Cm(Rise), Cm(Tread), Cm(Cheek), Cm(top), Cm(Landing), Cm(PierOut)),
                new Vector2(0f, TerraceFront), YawFacing(-North), 0f);

            float length = Steps * Tread + Landing;
            foreach (float s in new[] { -1f, 1f })
            {
                float x = s * (FlightHalf + Cheek * 0.5f);
                Solid(Footprint.Box(_c + new Vector2(x, TerraceFront - PierOut + (length + PierOut) * 0.5f), new Vector2(Cheek * 0.5f + 0.05f, (length + PierOut) * 0.5f), 0f), top);
            }
            float pitch = Rise / Tread;
            float middle = TerraceFront + Steps * Tread * 0.5f;
            World.Terraces.Add(new GroundPad(Footprint.Box(_c + new Vector2(0f, middle), new Vector2(FlightHalf, Steps * Tread * 0.5f), 0f),
                Level + pitch * (middle - TerraceFront) - 0.01f, Sharp, new Vector2(0f, pitch)));
            Keep(Footprint.Box(_c + new Vector2(0f, middle), new Vector2(FlightHalf, Steps * Tread * 0.5f), 0f));
        }

        // ---------------------------------------------------------------- Beds

        // A raised bed either side of the avenue, against the terrace: field stone on three sides with rounded corners, and the terrace's wall behind.
        private void AvenueBeds()
        {
            foreach (float s in new[] { -1f, 1f })
            {
                float inner = AvenueHalf, outer = BedOuter;
                float back = TerraceFront + 0.4f;
                Run(new Vector2(s * inner, BedFront + Corner), new Vector2(s * inner, back), -East * s, WallForm.Field, BedTop, BedSoil, BedThick);
                Run(new Vector2(s * (inner + Corner), BedFront), new Vector2(s * (outer - Corner), BedFront), -North, WallForm.Field, BedTop, BedSoil, BedThick);
                Run(new Vector2(s * outer, BedFront + Corner), new Vector2(s * outer, back), East * s, WallForm.Field, BedTop, BedSoil, BedThick);
                // Corners: the avenue's side turns from facing it to facing south, and the outer side from facing south to facing out.
                Arc(new Vector2(s * (inner + Corner), BedFront + Corner), Corner, s > 0f ? 180f : 90f, 90f, 1f, WallForm.Field, BedTop, BedSoil, BedThick);
                Arc(new Vector2(s * (outer - Corner), BedFront + Corner), Corner, s > 0f ? 90f : 180f, 90f, 1f, WallForm.Field, BedTop, BedSoil, BedThick);

                Footprint ground = Footprint.Box(_c + new Vector2(s * (inner + outer) * 0.5f, (BedFront + TerraceFront + 1f) * 0.5f),
                    new Vector2((outer - inner) * 0.5f - Inset, (TerraceFront + 1f - BedFront) * 0.5f - Inset), 0f);
                Raise(ground, BedSoil);
                Garden(Footprint.Box(_c + new Vector2(s * (inner + outer) * 0.5f, (BedFront + TerraceFront) * 0.5f), new Vector2((outer - inner) * 0.5f - 0.5f, (TerraceFront - BedFront) * 0.5f - 0.5f), 0f));

                // Pots on its wall where it meets the avenue: by the terrace, and on the rounded corner, halfway round it.
                PotOn(new Vector2(s * (inner + BedThick * 0.5f), back - 1.3f), BedTop, 0, 1.0f, Undergrowth.Azalea);
                PotOn(new Vector2(s * (inner + Corner), BedFront + Corner) + Toward(s > 0f ? 225f : 135f) * (Corner - BedThick * 0.5f), BedTop, 1, 1.15f, Undergrowth.Lavender);
            }
        }

        // The round bed in the middle of the square, its wall a seat all round, with a sakura in it, and a ring of kerbs round that.
        private void Heart()
        {
            Arc(Vector2.Zero, HeartRadius, 0f, 360f, 1f, WallForm.Seat, HeartTop, HeartSoil, HeartThick);
            Raise(Footprint.Circle(_c, HeartRadius - Inset), HeartSoil);
            Garden(Footprint.Circle(_c, HeartRadius - HeartThick));
            Piece(new PlazaSpec(PlazaKind.Ring, NextSeed(), 0, Cm(Collar), 0, 360, 60), Vector2.Zero, 0f, 0f);
        }

        // Beds round the southern half of the square, either side of the lane in, each an arc of ground walled in and out, its ends half circles.
        private void SouthBeds()
        {
            float middle = (RingInner + RingOuter) * 0.5f, halfWidth = (RingOuter - RingInner) * 0.5f;
            foreach (float s in new[] { -1f, 1f })
            {
                // Bearings run clockwise, so the west bed's are the east's taken from 360.
                float from = s > 0f ? RingFrom : 360f - RingTo, to = s > 0f ? RingTo : 360f - RingFrom;
                float sweep = to - from;
                Arc(Vector2.Zero, RingInner, from, sweep, -1f, WallForm.Field, BedTop, BedSoil, BedThick);
                Arc(Vector2.Zero, RingOuter, from, sweep, 1f, WallForm.Field, BedTop, BedSoil, BedThick);
                Arc(Toward(from) * middle, halfWidth, from - 180f, 180f, 1f, WallForm.Field, BedTop, BedSoil, BedThick);
                Arc(Toward(to) * middle, halfWidth, to, 180f, 1f, WallForm.Field, BedTop, BedSoil, BedThick);

                // Its ground, as a chain of circles along its middle.
                int links = (int)MathF.Ceiling(MathHelper.DegreesToRadians(sweep) * middle / 0.7f);
                for (int i = 0; i <= links; i++)
                {
                    Vector2 at = Toward(from + sweep * i / links) * middle;
                    Raise(Footprint.Circle(_c + at, halfWidth - Inset), BedSoil);
                    Garden(Footprint.Circle(_c + at, halfWidth - 0.5f));
                }

                // Pots on its wall: one by the arch, and one at its other end.
                float byArch = s > 0f ? to - 3f : from + 3f, farEnd = s > 0f ? from + 4f : to - 4f;
                PotOn(Toward(byArch) * (RingInner + BedThick * 0.5f), BedTop, 1, 1.15f, Undergrowth.Azalea);
                PotOn(Toward(farEnd) * (RingInner + BedThick * 0.5f), BedTop, 0, 1.0f, Undergrowth.Lavender);
            }
        }

        // A well off the middle of the square, south-east of the heart, where it's always been.
        private void Well()
        {
            Piece(new PlazaSpec(PlazaKind.Well, NextSeed(), Cm(WellRadius)), WellAt, YawFacing(-North), 0f);
            Solid(Footprint.Circle(_c + WellAt, PlazaPieces.WellReach(WellRadius)), 1.4f);
        }

        // ---------------------------------------------------------------- The arch, the loggia and the bunting

        private void Arch()
        {
            float z = ArchAt;
            Piece(new PlazaSpec(PlazaKind.Arch, NextSeed(), Cm(ArchSpan), Cm(ArchSpring), Cm(ArchDepth), Cm(ArchPier)), new Vector2(0f, z), YawFacing(-North), 0f);
            foreach (float s in new[] { -1f, 1f })
            {
                Solid(Footprint.Box(_c + new Vector2(s * (ArchSpan * 0.5f + ArchPier * 0.5f), z), new Vector2(ArchPier * 0.5f + 0.1f, ArchDepth * 0.5f + 0.1f), 0f), PlacementIndex.Tall);
                // Pots either side of it, on the square's side.
                PotAt(new Vector2(s * (ArchSpan * 0.5f + 0.6f), z + ArchDepth * 0.5f + 1.05f), 1, 1.2f, Undergrowth.Boxwood);
            }
            Keep(Footprint.Box(_c + new Vector2(0f, z), new Vector2(ArchSpan * 0.5f, ArchDepth * 0.5f + 1f), 0f));
        }

        // The loggia: columns under oak and a sail, its front to the square, tables laid for a meal under it, pots at its ends and a flagstone floor.
        private void Loggia()
        {
            float yaw = YawFacing(-East);
            Piece(new PlazaSpec(PlazaKind.Pergola, NextSeed(), Cm(LoggiaLength), Cm(LoggiaDepth), LoggiaBays, Cm(LoggiaHeight), 58), LoggiaAt, yaw, 0f);
            for (int k = 0; k <= LoggiaBays; k++)
            {
                float along = -LoggiaLength * 0.5f + LoggiaLength * k / LoggiaBays;
                foreach (float across in new[] { -LoggiaDepth * 0.5f, LoggiaDepth * 0.5f })
                {
                    Solid(Footprint.Circle(_c + LoggiaAt + new Vector2(-across, along), 0.48f), PlacementIndex.Tall);
                }
            }
            foreach (float along in new[] { -1.9f, 3.5f })
            {
                Vector2 at = LoggiaAt + new Vector2(0f, along);
                Piece(new PlazaSpec(PlazaKind.Table, NextSeed(), Cm(4f)), at, 0f, 0f);
                Solid(Footprint.Box(_c + at, new Vector2(2.1f, 1.62f), 0f), 2.1f);
            }
            // Pots at the ends of its front.
            foreach (float s in new[] { -1f, 1f })
            {
                PotAt(LoggiaAt + new Vector2(-LoggiaDepth * 0.5f - 0.9f, s * (LoggiaLength * 0.5f + 0.2f)), 0, 1.1f, Undergrowth.Lavender);
            }
            Keep(Footprint.Box(_c + LoggiaAt, new Vector2(LoggiaDepth * 0.5f, LoggiaLength * 0.5f), 0f));
        }

        // Four masts round the square, each with a lantern out over it, and bunting strung between them and on from them to the loggia.
        private void Bunting()
        {
            float tie = MastHeight - PlazaDressing.MastTie;
            foreach (Vector2 mast in Masts)
            {
                // Each carries a lantern out over the square, which is all that lights its middle at night.
                Piece(new PlazaSpec(PlazaKind.Mast, NextSeed(), Cm(MastHeight), 1), mast, YawFacing(-mast.Normalized()), 0f);
                Solid(Footprint.Circle(_c + mast, 0.5f), 0f);
            }
            String(Masts[0], tie, Masts[1], tie, 1.7f);
            String(Masts[2], tie, Masts[3], tie, 2.1f);
            String(Masts[0], tie, Masts[2], tie, 1.8f);
            String(Masts[1], tie, Masts[3], tie, 1.8f);
            // From the north-east mast down to the loggia's corner beam.
            String(Masts[1], tie, LoggiaAt + new Vector2(-LoggiaDepth * 0.5f, LoggiaLength * 0.5f + 0.5f), LoggiaHeight + 0.3f, 1.1f);
        }

        // A line of bunting from one point to another at the heights given, sagging `sag` in the middle.
        private void String(Vector2 from, float fromHeight, Vector2 to, float toHeight, float sag)
        {
            Vector2 run = to - from;
            float span = run.Length;
            // Its model runs along +x, so it's turned to point that way along the line.
            float yaw = MathF.Atan2(-run.Y, run.X);
            Piece(new PlazaSpec(PlazaKind.Bunting, NextSeed(), Cm(span), Cm(toHeight - fromHeight), Cm(sag)), from, yaw, fromHeight);
        }

        // ---------------------------------------------------------------- Paving and planting

        private void Paving()
        {
            // The square, its avenue and the court round the gate in the gate's stone; the loggia's floor too.
            _b.Paint(GroundPaint.Paving, Footprint.Circle(_c, SquareRadius + 0.2f), 1f);
            _b.Paint(GroundPaint.Paving, Footprint.Box(_c + new Vector2(0f, (TableAhead - 5f + TerraceFront) * 0.5f), new Vector2(AvenueHalf - 0.2f, (TerraceFront - TableAhead + 5f) * 0.5f), 0f), 1f);
            _b.Paint(GroundPaint.Paving, Footprint.Box(_c + new Vector2(0f, (TerraceFront + 36f) * 0.5f), new Vector2(10.6f, (36f - TerraceFront) * 0.5f), 0f), 1f);
            _b.Paint(GroundPaint.Paving, Footprint.Box(_c + LoggiaAt, new Vector2(LoggiaDepth * 0.5f + 0.6f, LoggiaLength * 0.5f + 0.6f), 0f), 1f);
            // Cobbles round the beds and out to the streets.
            _b.Paint(GroundPaint.Plaza, Footprint.Circle(_c, RingOuter + 2f), 4f);
            // Lawns on the terrace either side of the gate's court, and behind it.
            foreach (float s in new[] { -1f, 1f })
            {
                Footprint lawn = Footprint.Box(_c + new Vector2(s * 15f, (TerraceFront + TerraceBack) * 0.5f), new Vector2(4.2f, (TerraceBack - TerraceFront) * 0.5f - 0.8f), 0f);
                _b.Paint(GroundPaint.Garden, lawn, 0.8f);
                Garden(lawn);
            }
            Footprint behind = Footprint.Box(_c + new Vector2(0f, (36.6f + TerraceBack) * 0.5f), new Vector2(10.8f, (TerraceBack - 36.6f) * 0.5f - 0.8f), 0f);
            _b.Paint(GroundPaint.Garden, behind, 0.8f);
            Garden(behind);
            Keep(Footprint.Box(_c + new Vector2(0f, (TerraceFront + 36f) * 0.5f), new Vector2(10.4f, (36f - TerraceFront) * 0.5f), 0f));

            // Bands of kerbs: round the square's edge, but where the avenue leaves it, and round the heart's bed.
            float gap = MathHelper.RadiansToDegrees(MathF.Asin(MathF.Min((AvenueHalf - 0.3f) / SquareRadius, 1f)));
            Piece(new PlazaSpec(PlazaKind.Ring, NextSeed(), 0, Cm(SquareRadius), (int)MathF.Round(gap), (int)MathF.Round(360f - 2f * gap), 55), Vector2.Zero, 0f, 0f);
            // Spokes of the same stone from the heart's ring out to the square's edge, between the ways in: every waygate's road comes here first, and the square is its compass.
            for (int k = 0; k < Spokes; k++)
            {
                Vector2 direction = Toward(180f / Spokes + k * 360f / Spokes);
                float from = Collar + 0.35f, to = SquareRadius - 0.32f;
                Piece(new PlazaSpec(PlazaKind.Line, NextSeed(), 0, Cm(to - from), 45), direction * from, MathF.Atan2(-direction.Y, direction.X), 0f);
            }

            // A gutter down each side of the avenue, from the foot of the flight's piers to the square.
            foreach (float s in new[] { -1f, 1f })
            {
                float from = MathF.Sqrt(MathF.Max(SquareRadius * SquareRadius - (AvenueHalf - 0.75f) * (AvenueHalf - 0.75f), 0f));
                float to = TerraceFront - PierOut - 0.2f;
                // A line's model runs along +x, which a quarter turn points north.
                Piece(new PlazaSpec(PlazaKind.Line, NextSeed(), 1, Cm(to - from), 70), new Vector2(s * (AvenueHalf - 0.75f), from), -MathF.PI * 0.5f, 0f);
            }

            // Bed and garden ground is planted, so the cobbles' paint doesn't reach into it.
            foreach (float s in new[] { -1f, 1f })
            {
                _b.Paint(GroundPaint.Garden, Footprint.Box(_c + new Vector2(s * (AvenueHalf + BedOuter) * 0.5f, (BedFront + TerraceFront) * 0.5f), new Vector2((BedOuter - AvenueHalf) * 0.5f - 0.6f, (TerraceFront - BedFront) * 0.5f - 0.6f), 0f), 0.6f);
                float from = s > 0f ? RingFrom : 360f - RingTo, sweep = RingTo - RingFrom;
                float middle = (RingInner + RingOuter) * 0.5f;
                int links = (int)MathF.Ceiling(MathHelper.DegreesToRadians(sweep) * middle / 1.4f);
                for (int i = 0; i <= links; i++) _b.Paint(GroundPaint.Garden, Footprint.Circle(_c + Toward(from + sweep * i / links) * middle, (RingOuter - RingInner) * 0.5f - 0.6f), 0.6f);
            }
            _b.Paint(GroundPaint.Garden, Footprint.Circle(_c, HeartRadius - HeartThick - 0.1f), 0.5f);
        }

        // Everything planted in the beds and on the terrace: blossom over the gate, maples in the avenue's beds, a sakura in the heart, a cherry and an olive in the southern
        // beds, and shrubs and flowers under them all. A tree needs room for its crown, so they stand apart, and what's between them is low. They're grown where they stand
        // rather than planted (Grow), as a site's planting keeps a tree's whole crown clear of anything built, and a tree in a bed always reaches over its wall.
        private void Plant()
        {
            float up = TerraceUp;
            // The terrace: great sakuras either side of the gate and one behind it, framed by its arch, and shrubs along the parapet.
            foreach (float s in new[] { -1f, 1f })
            {
                Grow(Broadleaves.Sakura, new Vector2(s * 15.2f, 31.5f), 0.72f, up);
                for (int i = 0; i < 3; i++)
                {
                    Grow(i % 2 == 0 ? Undergrowth.Azalea : Undergrowth.WhiteAzalea, new Vector2(s * (12.2f + i * 2.4f), TerraceFront + 4f), 0.95f, up);
                }
                Grow(Undergrowth.Boxwood, new Vector2(s * 11.6f, 38.5f), 0.95f, up);
                Grow(Undergrowth.Lavender, new Vector2(s * 16.6f, 41f), 1f, up);
            }
            Grow(Broadleaves.Sakura, new Vector2(0f, 39.6f), 0.85f, up);

            // The avenue's beds: a maple in each, azaleas at their ends and lavender along their fronts.
            foreach (float s in new[] { -1f, 1f })
            {
                Grow(Broadleaves.JapaneseMaple, new Vector2(s * 13.2f, 16.9f), 0.48f, BedSoil);
                Grow(Undergrowth.WhiteAzalea, new Vector2(s * 10.4f, 17.8f), 0.95f, BedSoil);
                Grow(Undergrowth.Azalea, new Vector2(s * 15.9f, 17.9f), 0.95f, BedSoil);
                // Along its front, inside the wall: box at the corners and lavender between.
                Grow(Undergrowth.Boxwood, new Vector2(s * 10.0f, 14.6f), 0.85f, BedSoil);
                Grow(Undergrowth.Lavender, new Vector2(s * 11.7f, 14.5f), 1f, BedSoil);
                Grow(Undergrowth.Lavender, new Vector2(s * 13.3f, 14.4f), 1f, BedSoil);
                Grow(Undergrowth.Lavender, new Vector2(s * 14.9f, 14.5f), 1f, BedSoil);
                Grow(Undergrowth.Boxwood, new Vector2(s * 16.4f, 14.6f), 0.85f, BedSoil);
                Grow(Undergrowth.HostaPlant, new Vector2(s * 9.6f, 16.2f), 1f, BedSoil);
                Grow(Undergrowth.HostaPlant, new Vector2(s * 16.6f, 16.3f), 1f, BedSoil);
            }

            // The heart: a sakura, and a ring of azaleas round its trunk.
            Grow(Broadleaves.Sakura, Vector2.Zero, 0.62f, HeartSoil);
            for (int i = 0; i < 4; i++) Grow(i % 2 == 0 ? Undergrowth.Azalea : Undergrowth.WhiteAzalea, Toward(45f + i * 90f) * 2.3f, 0.85f, HeartSoil);

            // The southern beds.
            float middle = (RingInner + RingOuter) * 0.5f;
            foreach (float s in new[] { -1f, 1f })
            {
                float Bearing(float b) => s > 0f ? b : 360f - b;
                Grow(s > 0f ? Broadleaves.WhiteCherry : Broadleaves.Olive, Toward(Bearing(141f)) * middle, s > 0f ? 0.55f : 0.8f, BedSoil);
                Grow(Undergrowth.Boxwood, Toward(Bearing(127f)) * (middle + 0.6f), 0.9f, BedSoil);
                Grow(Undergrowth.Azalea, Toward(Bearing(132f)) * (middle - 1.1f), 0.95f, BedSoil);
                Grow(Undergrowth.Lavender, Toward(Bearing(150f)) * (middle - 1.0f), 1f, BedSoil);
                Grow(Undergrowth.WhiteAzalea, Toward(Bearing(154f)) * (middle + 1.0f), 0.9f, BedSoil);
                Grow(Undergrowth.Lavender, Toward(Bearing(136f)) * (middle + 1.15f), 1f, BedSoil);
                Grow(Undergrowth.HostaPlant, Toward(Bearing(146f)) * (middle + 1.2f), 1f, BedSoil);
                Grow(Undergrowth.Boxwood, Toward(Bearing(156f)) * (middle - 1.0f), 0.8f, BedSoil);
                Grow(Undergrowth.Lavender, Toward(Bearing(126f)) * (middle - 0.9f), 1f, BedSoil);
            }
        }

        // ---------------------------------------------------------------- Pieces

        private uint NextSeed()
        {
            _seed = _seed * 0x9E3779B1u + 0x7F4A7C15u;
            return _seed >> 4;
        }

        private static int Cm(float metres) => PlazaSpec.Cm(metres);

        private static Vector2 Toward(float degrees) => new Vector2(MathF.Sin(MathHelper.DegreesToRadians(degrees)), MathF.Cos(MathHelper.DegreesToRadians(degrees)));

        // A piece's model, `at` from the square's middle, turned by `yaw`, standing `lift` above where people stand in the square.
        private void Piece(in PlazaSpec spec, Vector2 at, float yaw, float lift)
        {
            World.Pending.Add(new PendingObject(spec.Id, _c + at, yaw, Vector3.One, Level + lift));
        }

        private void Solid(in Footprint footprint, float height) => World.Placement.Add(footprint, PlacementLayer.Prop, solid: true, height: height);
        private void Garden(in Footprint footprint) => World.Placement.Add(footprint, PlacementLayer.Garden);
        private void Keep(in Footprint footprint) => World.Placement.Add(footprint, PlacementLayer.Keepout);
        private void Raise(in Footprint footprint, float lift) => World.Terraces.Add(new GroundPad(footprint, Level + lift, Sharp));

        // A wall along a straight line from one point to another, its face towards `face`.
        private void Run(Vector2 a, Vector2 b, Vector2 face, WallForm form, float top, float soil, float thick)
        {
            float yaw = YawFacing(face);
            Vector2 along = new Vector2(MathF.Cos(yaw), -MathF.Sin(yaw));
            if (Vector2.Dot(b - a, along) < 0f) (a, b) = (b, a);
            float length = (b - a).Length;
            Piece(new PlazaSpec(PlazaKind.Run, NextSeed(), (int)form, Cm(length), Cm(top), soil < 0f ? -1 : Cm(soil), Cm(thick)), a, yaw, 0f);
            Vector2 back = -face.Normalized() * (thick * 0.5f);
            Solid(Footprint.Segment(_c + a + back, _c + b + back, thick * 0.5f + 0.05f), top - PlazaStone.Ground);
        }

        // A wall round an arc about `centre`, from a bearing through `sweep` degrees clockwise, its face out (1) or in (-1).
        private void Arc(Vector2 centre, float radius, float from, float sweep, float facing, WallForm form, float top, float soil, float thick)
        {
            Piece(new PlazaSpec(PlazaKind.Arc, NextSeed(), (int)form, Cm(radius), (int)MathF.Round(from), (int)MathF.Round(sweep), Cm(top), soil < 0f ? -1 : Cm(soil), Cm(thick), (int)facing), centre, 0f, 0f);
            float middle = radius - facing * thick * 0.5f;
            int pieces = Math.Max(1, (int)MathF.Ceiling(MathHelper.DegreesToRadians(MathF.Abs(sweep)) * middle / 1.2f));
            for (int i = 0; i < pieces; i++)
            {
                Vector2 p0 = centre + Toward(from + sweep * i / pieces) * middle, p1 = centre + Toward(from + sweep * (i + 1) / pieces) * middle;
                Solid(Footprint.Segment(_c + p0, _c + p1, thick * 0.5f + 0.05f), top - PlazaStone.Ground);
            }
        }

        private void Pier(Vector2 at, float width, float top, int crown, WorldAsset? flowers)
        {
            Piece(new PlazaSpec(PlazaKind.Pier, NextSeed(), Cm(width), Cm(top), crown), at, 0f, 0f);
            Solid(Footprint.Box(_c + at, new Vector2(width * 0.5f + 0.1f), 0f), PlacementIndex.Tall);
            if (crown == 2 && flowers != null) Flower(flowers, at, top + PlazaPieces.UrnSoil * width * 0.36f, 0.9f);
        }

        // A pot standing on the ground, with something growing in it.
        private void PotAt(Vector2 at, int form, float height, WorldAsset plant)
        {
            Piece(new PlazaSpec(PlazaKind.Pot, NextSeed(), form, Cm(height)), at, _random.Float() * MathF.Tau, 0f);
            Solid(Footprint.Circle(_c + at, height * 0.5f + 0.1f), height);
            Flower(plant, at, PlazaDressing.PotSoil(form, height), 0.8f);
        }

        // A pot on top of a wall whose cope is at `wallTop`.
        private void PotOn(Vector2 at, float wallTop, int form, float height, WorldAsset plant)
        {
            float lift = wallTop - PlazaStone.Ground;
            Piece(new PlazaSpec(PlazaKind.Pot, NextSeed(), form, Cm(height)), at, _random.Float() * MathF.Tau, lift);
            Flower(plant, at, lift + PlazaDressing.PotSoil(form, height), 0.75f);
        }

        // Something growing that stands on a piece rather than on the ground, so it's placed at a height rather than planted, `scale` times its kind's size.
        private void Flower(WorldAsset asset, Vector2 at, float lift, float scale)
        {
            Vector2 world = _c + at;
            World.Pending.Add(new PendingObject(NatureCatalog.ModelAt(asset, World.Plan.Seed, world), world, _random.Float() * MathF.Tau, new Vector3(scale), Level + lift - 0.04f));
        }

        // A plant grown where it stands, on ground `lift` above the square, `scale` times its kind's size: a town keeps its trees smaller than the woods do. Its trunk is
        // solid and its room kept, so the woods keep clear of it, as anything planted is.
        private void Grow(WorldAsset asset, Vector2 at, float scale, float lift)
        {
            Vector2 world = _c + at;
            bool tree = asset.Kind == WorldAssetKind.Tree;
            // Its trunk's room only: its crown reaches over the bed's wall, as it would in any town.
            World.Placement.Add(Footprint.Circle(world, tree ? asset.SolidRadius * scale + 0.5f : MathF.Min(asset.Footprint * scale, 0.8f)), tree ? PlacementLayer.Tree : PlacementLayer.Shrub);
            if (asset.SolidRadius > 0f) World.Placement.Add(Footprint.Circle(world, asset.SolidRadius * scale), PlacementLayer.None, solid: true);
            World.Pending.Add(new PendingObject(NatureCatalog.ModelAt(asset, World.Plan.Seed, world), world, _random.Float() * MathF.Tau, new Vector3(scale), Level + lift - (tree ? 0.15f : 0.05f)));
        }

        private static float AverageGround(WorldData world, Vector2 centre, float radius)
        {
            float total = 0f;
            int count = 0;
            for (float x = -radius; x <= radius; x += 4f)
            {
                for (float z = -radius; z <= radius; z += 4f)
                {
                    if (x * x + z * z > radius * radius) continue;
                    total += world.Elevation.Sample(centre + new Vector2(x, z));
                    count++;
                }
            }
            return count > 0 ? total / count : world.Elevation.Sample(centre);
        }
    }
}

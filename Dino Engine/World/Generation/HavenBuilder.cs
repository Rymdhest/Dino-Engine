using Dino_Engine.Modelling.Procedural.Fence;
using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.World.Generation.SiteBuilder;

namespace Dino_Engine.World.Generation
{
    /// <summary>
    /// Builds the Haven, the one village the Reich has never found, where a hero spends more time than anywhere else: a little town laid out for the game's camera, which looks north
    /// from the south and high up. The great waygate stands at the head of everything on a terrace of its own among blossom, with the map table before it at the top of the square,
    /// and two of the halls that can be walked into flank it, facing down over the square: the Archive to the west and the Gaming House to the east. Streets run from the square to
    /// each of their doors. The merchant and the beta vendor keep the west side of the square and the strongbox its east; the Forge stands to the south, its doors onto the training
    /// ground, where the lane from the square ends; a quiet garden is off the square to the south-east; homes stand round all of it; and two plots are kept empty for halls to come.
    /// Every hall faces south, towards the camera, as a hall's front is the wall that comes down when someone's inside it (HALLS.md). Everything here goes through
    /// <see cref="SiteBuilder"/> like anything else built, so walls, paths and plants keep to the same rules, and what can be used is placed with <see cref="SiteBuilder.PlaceNear"/>,
    /// as a mark with nothing under it is a prompt in an empty field. The ground, not this, says where everything ends up exactly: a thing that doesn't fit moves or isn't built.
    /// </summary>
    internal static class HavenBuilder
    {
        private const float SquareRadius = 16f;
        private const float GateAhead = 31f;
        private const float StallAside = 26f;
        private const float StashAside = 17.4f;
        private const float StashNorth = -0.6f;
        private const float BetaBelow = 11f;            // Metres south of the merchant's stall that the beta vendor's stands.
        private const float GroundSouth = 66f;          // Of the training ground's middle, from the square's.
        private const float GroundWest = 4f;
        private static readonly Vector2 GroundHalf = new Vector2(36f, 12f);
        private const float SingleAlong = 27f;          // Metres east of the training ground's middle, and the handful and the crowd west of it, each far enough from the next that nothing aimed at one reaches another.
        private const float SmallAlong = 7f;
        private const float BigAlong = -20f;
        private const float PenReach = 4f;
        private const float FencePiece = 5f;            // Metres of the yard's fence placed at a time, so that whatever stands in the way of one piece leaves the rest standing.
        private const float YardGate = 3.2f;            // Half the width of the way into it.
        private const float Apron = 3f;                 // Metres of gravel outside the fence: grass grows in tufts that reach as far again as they stand, and the yard's edge would be overgrown.
        private const float DummyGap = 3.4f;            // Between the dummies of a group: close enough that what hits one can reach the next.
        private const int SmallGroup = 5;
        private const int BigGroup = 15;
        private const float ReachForEssentials = 30f;
        private const float DoorPath = 0.4f;            // Metres short of a hall's doorstep that the street to it ends.

        // Where each hall stands, from the square's middle, east and north. They all face south, at the camera.
        private static readonly Vector2 ArchiveAt = new Vector2(-46f, 28f);
        private static readonly Vector2 GamingHouseAt = new Vector2(41f, 25f);
        private static readonly Vector2 ForgeAt = new Vector2(-39f, -32f);
        private const float HallReach = 12f;            // Metres a hall may move from where it's meant to be if the ground there won't have it.

        /// <summary>The ground kept for halls to come, from the square's middle: each the size of the Forge, facing south, with its own way to it.</summary>
        public static readonly Vector2[] Plots = { new Vector2(36f, -32f), new Vector2(78f, 23.5f) };
        private static readonly Vector2 PlotHalf = new Vector2(16f, 12f);

        public static readonly Vector2 North = new Vector2(0f, 1f);
        public static readonly Vector2 East = new Vector2(1f, 0f);
        private static readonly float FacingCamera = YawFacing(-North);

        public static void Build(SiteBuilder b)
        {
            Vector2 c = b.Centre;

            // The square is built as a town's is, in layers of walls, steps and beds round the gate (HavenSquare, SQUARE.md): its ground first, as the streets into it are
            // planned to meet it.
            var square = new HavenSquare(b);
            square.LayGround();
            LayPaths(b, square.Level);

            PlaceGate(b, c);
            PlaceTable(b, c);
            WorldHall? archive = PlaceHall(b, HallKind.Archive, c + ArchiveAt);
            WorldHall? gaming = PlaceHall(b, HallKind.GamingHouse, c + GamingHouseAt);
            WorldHall? forge = PlaceHall(b, HallKind.Forge, c + ForgeAt);
            // Every street before anything else, so that what's put down after keeps off them.
            LayStreets(b, c, archive, gaming, forge, square.Level);
            PlaceTrade(b, c);
            square.Build();
            PlaceTrainingGround(b, c + North * -GroundSouth + East * -GroundWest);
            DressHalls(b, archive, gaming, forge);
            KeepPlots(b, c);
            PlaceGarden(b, c);
            PlaceHomes(b, c);
            PlaceShore(b, c);
            PlantGardens(b, c);

            for (int i = 0; i < 4; i++)
            {
                b.Scatter(StructureCatalog.Barrels, c, b.Site.Radius * 0.85f, attempts: 10, padding: 0.8f);
                b.Scatter(StructureCatalog.Crates, c, b.Site.Radius * 0.85f, attempts: 10, padding: 0.8f);
            }
            b.Scatter(StructureCatalog.Cart, c + North * -30f + East * 14f, 12f, attempts: 12);
            square.People(5);
            b.SpawnAround(SpawnRole.Villager, c, b.Site.Radius * 0.7f, 4);
        }

        // The ways that everything else keeps off: from the square's arch on south past the Forge to the training ground's gate, and out to the shore. The square and its avenue
        // up to the gate are laid out by HavenSquare, which keeps its own ways clear.
        private static void LayPaths(SiteBuilder b, float level)
        {
            Vector2 c = b.Centre;
            WorldData world = b.World;
            float fromArch = -HavenSquare.ArchAt + 2.6f;
            Lay(world, new List<Vector2> { c + North * -fromArch, c + North * -30f + East * 1.5f, c + North * -(GroundSouth - GroundHalf.Y - 7f) + East * -GroundWest * 0.5f, c + North * -(GroundSouth - GroundHalf.Y - 2.5f) + East * -GroundWest },
                RoadClass.Highway, startHeight: level);
        }

        private static WorldRoad? Lay(WorldData world, List<Vector2> corners, RoadClass kind, float? endHeight = null, float? startHeight = null)
        {
            WorldRoad? path = RoadStage.BuildRoad(world, corners, kind, startHeight, endHeight);
            if (path == null) return null;
            world.Roads.Add(path);
            SettlementStage.ReserveRoad(world, path);
            return path;
        }

        // A hall where it's meant to be, or as near as the ground lets it, facing the camera.
        private static WorldHall? PlaceHall(SiteBuilder b, HallKind kind, Vector2 at)
        {
            for (float distance = 0f; distance <= HallReach; distance += 2f)
            {
                int directions = distance > 0f ? 8 : 1;
                for (int i = 0; i < directions; i++)
                {
                    Vector2 placed = at + Direction(i * (MathF.Tau / 8f)) * distance;
                    if (b.PlaceHall(kind, placed, FacingCamera, out WorldHall hall)) return hall;
                }
            }
            // WorldLab's check fails a Haven with a hall missing, so a world this happens in doesn't go unnoticed.
            return null;
        }

        // A street from the square to each hall's door, ending at its doorstep at the height of its floor, and a forecourt there.
        private static void LayStreets(SiteBuilder b, Vector2 c, WorldHall? archive, WorldHall? gaming, WorldHall? forge, float level)
        {
            WorldData world = b.World;
            // The halls' streets leave the square at its edge, at the square's own height, between its beds.
            Vector2 west = new Vector2(-HavenSquare.SquareRadius + 0.8f, 7.2f), east = new Vector2(HavenSquare.SquareRadius - 0.8f, 7.2f);
            if (archive != null)
            {
                Vector2 door = DoorStreetEnd(archive);
                Lay(world, new List<Vector2> { c + west, c + new Vector2(-26f, 8.6f), door + new Vector2(10f, -1.2f), door }, RoadClass.Highway, archive.Floor, level);
                Forecourt(b, archive, 13f, 6f);
            }
            if (gaming != null)
            {
                Vector2 door = DoorStreetEnd(gaming) - North * 2.6f;   // Short of its porch's posts.
                Lay(world, new List<Vector2> { c + east, c + new Vector2(26f, 8.6f), door + new Vector2(-9f, -0.8f), door }, RoadClass.Highway, gaming.Floor, level);
                Forecourt(b, gaming, 12f, 7f);
            }
            // The training ground's street, along the north of its fence: the Forge at its west end and a plot kept at its east, both facing onto it.
            Vector2 lane = c + North * -(GroundSouth - GroundHalf.Y - 5.6f) + East * -2.4f;
            if (forge != null)
            {
                Vector2 door = DoorStreetEnd(forge);
                Lay(world, new List<Vector2> { lane, lane + new Vector2(-12f, 1.5f), door + new Vector2(8f, -0.5f), door }, RoadClass.Track, forge.Floor);
                Forecourt(b, forge, 14f, 5f);
            }
            Vector2 plotDoor = c + Plots[0] - North * (PlotHalf.Y + 4.95f);
            Lay(world, new List<Vector2> { lane, lane + new Vector2(14f, -0.3f), plotDoor + new Vector2(-8f, -0.2f), plotDoor }, RoadClass.Track);

            // And the east street goes on past the Gaming House to the other plot.
            Vector2 farDoor = c + Plots[1] - North * (PlotHalf.Y + 4.95f);
            Vector2 from = gaming != null ? DoorStreetEnd(gaming) - North * 2.6f : c + new Vector2(30f, 7f);
            Lay(world, new List<Vector2> { from, from + new Vector2(13f, 0.1f), farDoor + new Vector2(-10f, -0.2f), farDoor }, RoadClass.Track);

            // A path down to the water from the edge of the town nearest it, once everything it might have run through is placed.
            if (b.FindWater(260f, out Vector2 toWater, out float distance) && distance > SquareRadius + 30f)
            {
                Vector2 start = ShoreStart(c, toWater, forge);
                Vector2 end = c + toWater * MathF.Max(distance - 6f, SquareRadius + 10f);
                Vector2 bend = new Vector2(-toWater.Y, toWater.X) * (end - start).Length * 0.1f;
                Lay(world, new List<Vector2> { start, (start + end) * 0.5f + bend, end }, RoadClass.Footpath);
            }
        }

        // Where the path to the shore leaves the town: the end of whichever street is nearest the water.
        private static Vector2 ShoreStart(Vector2 c, Vector2 toWater, WorldHall? forge)
        {
            var ends = new List<Vector2>
            {
                c + new Vector2(-GroundWest - GroundHalf.X - 6f, -GroundSouth + GroundHalf.Y + 5.6f),   // the west end of the training ground's street
                c + new Vector2(-GroundWest + GroundHalf.X + 6f, -GroundSouth + GroundHalf.Y + 5.6f),   // its east end
                c + new Vector2(-GroundWest - GroundHalf.X - 6f, -GroundSouth),                        // the west end of the yard
                c + new Vector2(-GroundWest + GroundHalf.X + 6f, -GroundSouth),                        // its east end
                c + North * (SquareRadius + 2f),
            };
            if (forge != null) ends.Add(DoorStreetEnd(forge) - forge.Axis * 14f);
            return ends.OrderBy(end => -Vector2.Dot(end - c, toWater)).First();
        }

        // Where the street to a hall's main door stops: on the ground just outside its doorstep.
        private static Vector2 DoorStreetEnd(WorldHall hall)
        {
            HallOpening door = hall.Design.Doors.First();
            return DoorOutside(hall, door, 4.5f + DoorPath + 0.05f);
        }

        // Paved ground before a hall's door, as wide as its front wants.
        private static void Forecourt(SiteBuilder b, WorldHall hall, float halfWidth, float depth)
        {
            HallOpening door = hall.Design.Doors.First();
            Vector2 middle = DoorOutside(hall, door, depth * 0.5f);
            b.Paint(GroundPaint.Plaza, Footprint.Box(middle, new Vector2(halfWidth, depth * 0.5f), hall.Yaw), 3f);
        }

        // What stands about each hall outside: lanterns at its forecourt, a bench, barrels by the forge and the gaming house, flowers along their fronts.
        private static void DressHalls(SiteBuilder b, WorldHall? archive, WorldHall? gaming, WorldHall? forge)
        {
            if (archive != null)
            {
                HallOpening door = archive.Design.Doors.First();
                foreach (float s in new[] { -1f, 1f })
                {
                    b.Place(StructureCatalog.StoneLantern, archive.ToWorld(archive.Design.Doorstep(door, 6.6f) + new Vector2(s * 9.5f, 0f)), 0f, padding: 0.2f);
                    b.Place(StructureCatalog.Bench, archive.ToWorld(archive.Design.Doorstep(door, 4.4f) + new Vector2(s * 15f, 0f)), archive.Yaw + MathF.PI, padding: 0.2f);
                }
            }
            if (gaming != null)
            {
                HallOpening door = gaming.Design.Doors.First();
                b.Place(StructureCatalog.Barrels, gaming.ToWorld(new Vector2(gaming.Design.Half.X + 2.4f, 6f)), 0f, padding: 0.3f);
                b.Place(StructureCatalog.Barrels, gaming.ToWorld(new Vector2(gaming.Design.Half.X + 2.2f, 2.2f)), 0f, padding: 0.3f);
                b.Place(StructureCatalog.Bench, gaming.ToWorld(gaming.Design.Doorstep(door, 5.2f) + new Vector2(10f, 0f)), gaming.Yaw + MathF.PI, padding: 0.2f);
                b.Place(StructureCatalog.Lantern, gaming.ToWorld(gaming.Design.Doorstep(door, 7.2f) + new Vector2(-9f, 0f)), 0f, padding: 0.2f);
            }
            if (forge != null)
            {
                HallOpening door = forge.Design.Doors.First();
                b.Place(StructureCatalog.Crates, forge.ToWorld(forge.Design.Doorstep(door, 3.4f) + new Vector2(10.5f, 0f)), 0.3f, padding: 0.3f);
                b.Place(StructureCatalog.Barrels, forge.ToWorld(forge.Design.Doorstep(door, 3f) + new Vector2(-10f, 0f)), 0f, padding: 0.3f);
                b.Place(StructureCatalog.Woodpile, forge.ToWorld(new Vector2(-forge.Design.Half.X - 2.6f, -4f)), forge.Yaw + MathF.PI * 0.5f, padding: 0.3f);
                b.Place(StructureCatalog.Lantern, forge.ToWorld(forge.Design.Doorstep(door, 6.4f) + new Vector2(6.5f, 0f)), 0f, padding: 0.2f);
            }
        }

        // The plots kept for halls to come: the ground's kept clear, and timber and stone are stacked on each, waiting.
        private static void KeepPlots(SiteBuilder b, Vector2 c)
        {
            foreach (Vector2 plot in Plots)
            {
                Footprint ground = Footprint.Box(c + plot, PlotHalf, FacingCamera);
                if (!b.Fits(ground, 0.3f, 1.5f)) continue;
                b.World.Placement.Add(ground, PlacementLayer.Keepout);
                b.World.Clearings.Add(new Clearing(c + plot, PlotHalf.Length, 10f));
                // What's stacked there, out of the middle, where a hall will stand.
                b.World.Pending.Add(new PendingObject(StructureCatalog.LogStack.Id, c + plot + new Vector2(-PlotHalf.X + 5f, -PlotHalf.Y + 3.5f), 0.1f, Vector3.One, null));
                b.World.Pending.Add(new PendingObject(StructureCatalog.Rubble.Id, c + plot + new Vector2(PlotHalf.X - 4f, -PlotHalf.Y + 3.5f), 1.3f, Vector3.One * 0.8f, null));
            }
        }

        // The great waygate, at the head of everything on the square's terrace, facing down over the square, with a lantern either side. The flight up to it carries its own.
        private static void PlaceGate(SiteBuilder b, Vector2 c)
        {
            Vector2 at = c + North * GateAhead;
            if (b.PlaceNear(StructureCatalog.GreatWaygate, at, YawFacing(-North), ReachForEssentials, out Vector2 gate, padding: 0.4f))
            {
                b.Spawn(SpawnRole.Waygate, gate - North * (StructureCatalog.GreatWaygate.HalfExtents.Y + 3f));
            }
            foreach (float side in new[] { -1f, 1f })
            {
                b.Place(StructureCatalog.StoneLantern, gate + East * (side * (StructureCatalog.GreatWaygate.HalfExtents.X + 1.9f)) - North * 1.6f, 0f, padding: 0.2f);
            }
        }

        // The map table at the top of the square, between the square and the gate, where the way to every land is chosen.
        private static void PlaceTable(SiteBuilder b, Vector2 c)
        {
            if (!b.PlaceNear(StructureCatalog.MapTable, c + North * HavenSquare.TableAhead, YawFacing(-North), ReachForEssentials, out Vector2 table, padding: 0.6f)) return;
            b.Spawn(SpawnRole.MapTable, table - North * (StructureCatalog.MapTable.HalfExtents.Y + 1.8f));
        }

        // The merchant on the west side of the square and the strongbox on the east, each facing in, and the beta vendor along from the merchant.
        private static void PlaceTrade(SiteBuilder b, Vector2 c)
        {
            Vector2 stallAt = c - East * StallAside + North * -4f;
            if (b.PlaceNear(StructureCatalog.MarketStall, stallAt, YawFacing(East), ReachForEssentials, out Vector2 stall, padding: 0.6f))
            {
                b.Spawn(SpawnRole.Merchant, stall + East * 3.6f);
                b.Place(StructureCatalog.Crates, stall - East * 1.5f + North * 4.2f, 0f, padding: 0.3f);
                b.Place(StructureCatalog.Barrels, stall - East * 1f - North * 4f, 0f, padding: 0.3f);
            }

            // The beta vendor along from the merchant, who gives away anything there is, for testing the game.
            if (b.PlaceNear(StructureCatalog.MarketStall, stallAt - North * BetaBelow, YawFacing(East), ReachForEssentials, out Vector2 beta, padding: 0.6f))
            {
                b.Spawn(SpawnRole.BetaVendor, beta + East * 3.6f);
            }

            // Beside the loggia, facing in across the square.
            Vector2 boxAt = c + East * StashAside + North * StashNorth;
            if (b.PlaceNear(StructureCatalog.Strongbox, boxAt, YawFacing(-East), ReachForEssentials, out Vector2 box, padding: 0.6f))
            {
                b.Spawn(SpawnRole.Stash, box - East * (StructureCatalog.Strongbox.HalfExtents.Y + 1.6f));
                b.Place(StructureCatalog.Lantern, box + North * 3f, 0f, padding: 0.2f);
            }
        }

        // A quiet garden off the square to the south-east, to sit in: lanterns, a bench under a tree and flowers. There's no shrine, as nothing here needs a blessing to face.
        private static void PlaceGarden(SiteBuilder b, Vector2 c)
        {
            Vector2 at = c + East * 27f + North * -14f;
            Vector2 facing = (c - at).Normalized();
            Vector2 side = new Vector2(-facing.Y, facing.X);
            b.Paint(GroundPaint.Plaza, Footprint.Circle(at, 3.4f), 2f);
            b.Place(StructureCatalog.StoneLantern, at + side * 3.8f, 0f, padding: 0.2f);
            b.Place(StructureCatalog.StoneLantern, at - side * 3.8f, 0f, padding: 0.2f);
            b.Place(StructureCatalog.Bench, at - facing * 1.5f, YawFacing(facing), padding: 0.2f);

            Plant(b, Broadleaves.Sakura, at - facing * 8f, 0.85f);
            foreach (float flank in new[] { -1f, 1f })
            {
                Plant(b, Undergrowth.Azalea, at + side * (flank * 6.4f) - facing * 1f, 0.55f);
                Plant(b, Undergrowth.Lavender, at + side * (flank * 5.8f) + facing * 2.4f, 0.6f);
            }
        }

        // A fenced yard south of the Forge with three groups to try a build on: one alone, a handful, and a crowd.
        private static void PlaceTrainingGround(SiteBuilder b, Vector2 centre)
        {
            Footprint yard = Footprint.Box(centre, GroundHalf, 0f);
            // Packed gravel, as a yard that's drilled in every day is, so that what's in it stands out, and a little way past its fence.
            b.Paint(GroundPaint.Plaza, Footprint.Box(centre, GroundHalf + new Vector2(Apron), 0f), Apron + 2f);
            b.World.Placement.Add(yard, PlacementLayer.Keepout);
            FenceYard(b, centre);

            // Three pens along the yard, one alone, a handful and a crowd, far enough apart that whatever's aimed at one meets only that one, with low fences between them.
            b.Spawn(SpawnRole.Dummy, centre + East * SingleAlong);

            Vector2 small = centre + East * SmallAlong - North * 1f;
            for (int i = 0; i < SmallGroup; i++)
            {
                float angle = i * MathF.Tau / SmallGroup + 0.3f;
                b.Spawn(SpawnRole.Dummy, small + Direction(angle) * DummyGap);
            }

            foreach (float between in new[] { (SingleAlong + SmallAlong + DummyGap) * 0.5f, (SmallAlong - DummyGap + BigAlong + DummyGap * 2.7f) * 0.5f })
            {
                b.Wall(FenceUse.Yard, centre + East * between - North * (GroundHalf.Y - 0.5f), centre + East * between + North * PenReach);
            }

            Vector2 big = centre + East * BigAlong - North * 1f;
            for (int i = 0; i < BigGroup; i++)
            {
                int row = i / SmallGroup;
                int column = i % SmallGroup;
                Vector2 offset = new Vector2((column - 2f) * DummyGap * 1.2f + (row % 2) * DummyGap * 0.6f, (row - 1f) * DummyGap * 1.25f);
                b.Spawn(SpawnRole.Dummy, big + offset);
            }

            // Benches at either end to watch from, as the street runs along its north side.
            b.Place(StructureCatalog.Bench, centre - East * (GroundHalf.X + 2.6f) + North * 3f, YawFacing(East), padding: 0.2f);
            b.Place(StructureCatalog.Bench, centre + East * (GroundHalf.X + 2.6f) - North * 3f, YawFacing(-East), padding: 0.2f);
            b.Place(StructureCatalog.DryingRack, centre - North * (GroundHalf.Y + 2f) + East * 4f, 0f, padding: 0.3f);
            b.Place(StructureCatalog.Barrels, centre + East * (GroundHalf.X + 2.4f) - North * 7f, 0f, padding: 0.2f);
            b.Place(StructureCatalog.Lantern, centre + East * (GroundHalf.X + 1.6f) - North * (GroundHalf.Y + 1.6f), 0f, padding: 0.2f);
            b.Place(StructureCatalog.Lantern, centre - East * (GroundHalf.X + 1.6f) - North * (GroundHalf.Y + 1.6f), 0f, padding: 0.2f);
        }

        // A fence round the yard, open in the middle of its north side, where the lane from the square comes in.
        private static void FenceYard(SiteBuilder b, Vector2 centre)
        {
            Vector2 half = GroundHalf + new Vector2(0.8f);
            Vector2[] corners = { centre + new Vector2(-half.X, -half.Y), centre + new Vector2(half.X, -half.Y), centre + new Vector2(half.X, half.Y), centre + new Vector2(-half.X, half.Y) };
            for (int side = 0; side < 4; side++)
            {
                Vector2 from = corners[side];
                Vector2 to = corners[(side + 1) % 4];
                float length = (to - from).Length;
                int pieces = Math.Max(1, (int)MathF.Ceiling(length / FencePiece));
                for (int i = 0; i < pieces; i++)
                {
                    Vector2 start = Vector2.Lerp(from, to, i / (float)pieces);
                    Vector2 end = Vector2.Lerp(from, to, (i + 1f) / pieces);
                    // The north side is the third, from the north-east corner to the north-west.
                    if (side == 2 && MathF.Max(start.X, end.X) > centre.X - YardGate && MathF.Min(start.X, end.X) < centre.X + YardGate)
                    {
                        if (start.X > centre.X + YardGate) b.Wall(FenceUse.Yard, start, new Vector2(centre.X + YardGate, start.Y), centre);
                        if (end.X < centre.X - YardGate) b.Wall(FenceUse.Yard, new Vector2(centre.X - YardGate, end.Y), end, centre);
                        continue;
                    }
                    b.Wall(FenceUse.Yard, start, end, centre);
                }
            }
        }

        // Homes round the town, each facing the way into it, on whatever ground is left: the people who got out live here.
        private static void PlaceHomes(SiteBuilder b, Vector2 c)
        {
            (StructureDefinition kind, Vector2 at, Vector2 facing)[] homes =
            {
                (StructureCatalog.House, new Vector2(-62f, -9f), East),
                (StructureCatalog.Cottage, new Vector2(-46f, -10f), East),
                (StructureCatalog.Cottage, new Vector2(47f, -5f), -East),
                (StructureCatalog.House, new Vector2(63f, -13f), -East),
                (StructureCatalog.Cottage, new Vector2(-81f, 15f), East + -North * 0.4f),
                (StructureCatalog.Longhouse, new Vector2(-64f, 55f), -North),
                (StructureCatalog.Cottage, new Vector2(58f, 52f), -North + -East * 0.3f),
                (StructureCatalog.Cottage, new Vector2(12f, -35f), -East),
                (StructureCatalog.House, new Vector2(-12f, -21f), East),
                (StructureCatalog.Shed, new Vector2(-60f, -24f), East),
                (StructureCatalog.Cottage, new Vector2(70f, -42f), -East + North),
            };
            foreach ((StructureDefinition kind, Vector2 at, Vector2 facing) in homes)
            {
                Vector2 front = facing.Normalized();
                for (int attempt = 0; attempt < 9; attempt++)
                {
                    Vector2 placed = c + at + (attempt == 0 ? Vector2.Zero : Direction(attempt * (MathF.Tau / 8f)) * (2.5f + attempt * 0.8f));
                    if (!b.Place(kind, placed, YawFacing(front), padding: 1.8f, maxSlope: 0.34f)) continue;
                    b.Paint(GroundPaint.Yard, Footprint.Circle(placed + front * (kind.HalfExtents.Y + 1f), kind.HalfExtents.X * 0.8f), 4f);
                    Vector2 side = new Vector2(-front.Y, front.X);
                    if (b.Random.Chance(0.6f)) b.Place(StructureCatalog.Woodpile, placed + side * (kind.HalfExtents.X + 2f), YawFacing(-front), padding: 0.3f);
                    if (b.Random.Chance(0.5f)) b.Place(StructureCatalog.Lantern, placed + front * (kind.HalfExtents.Y + 2.2f) + side * (kind.HalfExtents.X * 0.7f), 0f, padding: 0.2f);
                    break;
                }
            }
        }

        // Blossom, planted about the halls and beyond the terrace's back wall: everything in the square and on its terrace is planted by HavenSquare. These are the Haven's own and
        // nobody else's, so every one is chosen here rather than drawn from the woods. A tree needs room for its crown, so they stand well apart and clear of the streets.
        private static void PlantGardens(SiteBuilder b, Vector2 c)
        {
            Vector2 gate = c + North * GateAhead;
            foreach (float side in new[] { -1f, 1f })
            {
                Plant(b, Broadleaves.WhiteCherry, gate + East * (side * 15.5f) + North * 16.5f, 0.75f);
            }

            // Trees about the halls, clear of their walls and their streets.
            Plant(b, Broadleaves.JapaneseMaple, c + ArchiveAt + new Vector2(-27f, -2f), 0.7f);
            Plant(b, Broadleaves.Sakura, c + ArchiveAt + new Vector2(-6f, 22f), 0.8f);
            Plant(b, Broadleaves.WisteriaTree, c + GamingHouseAt + new Vector2(20f, -6f), 0.65f);
            Plant(b, Broadleaves.WhiteCherry, c + GamingHouseAt + new Vector2(6f, 19f), 0.7f);
            Plant(b, Broadleaves.JapaneseMaple, c + ForgeAt + new Vector2(-20f, 4f), 0.6f);
            foreach (float s in new[] { -1f, 1f })
            {
                Plant(b, Undergrowth.Lavender, c + ArchiveAt + new Vector2(s * 19f, -18.5f), 0.6f);
                Plant(b, Undergrowth.Azalea, c + GamingHouseAt + new Vector2(s * 12.5f, -15.5f), 0.55f);
            }
        }

        private static void Plant(SiteBuilder b, WorldAsset asset, Vector2 at, float growth)
        {
            b.World.Plantings.Add(new Planting(asset, at, asset.Scale.Lerp(growth), b.Random.Float() * MathF.Tau));
        }

        // A jetty where the path meets the water, with a boat or two drawn up beside it, as the Haven is an island and everyone came by water once.
        private static void PlaceShore(SiteBuilder b, Vector2 c)
        {
            if (!b.FindWater(260f, out Vector2 toWater, out float distance) || distance < SquareRadius + 20f) return;
            Vector2 shore = c + toWater * (distance - 3f);
            Vector2 side = new Vector2(-toWater.Y, toWater.X);
            b.World.Pending.Add(new PendingObject(StructureCatalog.Jetty.Id, shore + toWater * 8f, YawFacing(toWater), Vector3.One, 0.9f));
            for (int i = -1; i <= 1; i += 2)
            {
                b.Place(StructureCatalog.Boat, shore + side * (i * b.Random.Range(8f, 12f)) - toWater * 4f, YawFacing(side) + b.Random.Range(-0.3f, 0.3f), padding: 0.4f, maxSlope: 0.5f);
            }
            b.Place(StructureCatalog.DryingRack, shore - toWater * 9f + side * 5f, YawFacing(side), padding: 0.3f);
        }
    }
}

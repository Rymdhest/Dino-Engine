using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.World.Generation.SiteBuilder;

namespace Dino_Engine.World.Generation
{
    /// <summary>
    /// Builds the Haven, the one village the Reich has never found, where a hero spends more time than anywhere else. It's laid out for the game's camera, which looks north from the south and high up:
    /// the great waygate stands at the head of everything on a terrace of its own among blossom, the map table in front of it at the top of the square, and the square opens south onto the village, with
    /// the merchant on one side and the strongbox on the other, a garden off it, and the training ground beyond. Its trees were planted by somebody, as nowhere else's are. Everything here goes through <see cref="SiteBuilder"/> like anything else built, so walls, paths and plants keep to the same rules, and what can be used is placed with
    /// <see cref="SiteBuilder.PlaceNear"/>, as a mark with nothing under it is a prompt in an empty field.
    /// </summary>
    internal static class HavenBuilder
    {
        private const float SquareRadius = 16f;
        private const float TableAhead = 14f;          // Metres north of the square's middle.
        private const float GateAhead = 29f;
        private const float TerraceRadius = 11f;
        private const float StallAside = 18f;
        private const float StashAside = 18f;
        private const float BetaBelow = 9f;             // Metres south of the square's middle that the beta vendor's stall stands, along from the merchant's.
        private const float GroundSouth = 47f;          // Of the training ground's middle, from the square's.
        private const float HomesBeyond = 15f;          // Metres of open ground between the square's edge and the nearest wall.
        private const float GroundWest = 6f;
        private static readonly Vector2 GroundHalf = new Vector2(36f, 12f);
        private const float SingleAlong = 27f;          // Metres east of the training ground's middle, and the handful and the crowd west of it, each far enough from the next that nothing aimed at one reaches another.
        private const float SmallAlong = 7f;
        private const float BigAlong = -20f;
        private const float PenReach = 4f;
        private const float FencePiece = 5f;            // Metres of the yard's fence placed at a time, so that whatever stands in the way of one piece leaves the rest standing.
        private const float YardGate = 3.2f;            // Half the width of the way into it.
        private const float Apron = 3f;                 // Metres of gravel outside the fence: grass grows in tufts that reach as far again as they stand, and the yard's edge would be overgrown.              // Metres north of the yard's middle that the low fences between them run to, leaving a way along the top.
        private const float DummyGap = 3.4f;            // Between the dummies of a group: close enough that what hits one can reach the next.
        private const int SmallGroup = 5;
        private const int BigGroup = 15;
        private const float ReachForEssentials = 30f;
        private const float GateClear = 1.1f;           // Radians either side of north that no home stands in, so that the gate stands free among its trees.
        private const float HedgeRing = 2.2f;           // Metres beyond the square's edge that its shrubs are set.
        private const int HedgeShrubs = 28;

        public static readonly Vector2 North = new Vector2(0f, 1f);
        public static readonly Vector2 East = new Vector2(1f, 0f);

        public static void Build(SiteBuilder b)
        {
            Vector2 c = b.Centre;

            LayPaths(b);

            b.Paint(GroundPaint.Plaza, Footprint.Circle(c, SquareRadius), 7f);
            b.Paint(GroundPaint.Plaza, Footprint.Segment(c, c + North * GateAhead, 4.2f), 3f);
            b.Paint(GroundPaint.Plaza, Footprint.Circle(c + North * GateAhead, TerraceRadius), 5f);

            PlaceGate(b, c);
            PlaceTable(b, c);
            PlaceTrade(b, c);
            PlaceGarden(b, c);
            PlaceTrainingGround(b, c + North * -GroundSouth + East * -GroundWest);
            PlaceHomes(b, c);
            PlaceShore(b, c);
            PlantGardens(b, c);

            b.Place(StructureCatalog.Well, c + East * 6f + North * -7f, 0f, padding: 0.6f);
            for (int i = 0; i < 4; i++)
            {
                b.Scatter(StructureCatalog.Barrels, c, b.Site.Radius * 0.85f, attempts: 10, padding: 0.8f);
                b.Scatter(StructureCatalog.Crates, c, b.Site.Radius * 0.85f, attempts: 10, padding: 0.8f);
            }
            b.Scatter(StructureCatalog.Cart, c + North * -20f, 16f, attempts: 12);
            b.SpawnAround(SpawnRole.Villager, c + North * -4f, SquareRadius + 6f, 5);
            b.SpawnAround(SpawnRole.Villager, c, b.Site.Radius * 0.7f, 4);
        }

        // Paths first, so that everything built keeps off them: down from the gate through the square to the training ground's gate, as a path through the yard would wear its gravel away, and out to the shore.
        private static void LayPaths(SiteBuilder b)
        {
            Vector2 c = b.Centre;
            WorldData world = b.World;
            var paths = new List<(List<Vector2> corners, RoadClass kind)>
            {
                (new List<Vector2> { c + North * (GateAhead - TerraceRadius * 0.6f), c + North * 4f }, RoadClass.Track),
                (new List<Vector2> { c + North * -4f, c + North * -(GroundSouth - GroundHalf.Y - 6f) + East * -GroundWest * 0.5f, c + North * -(GroundSouth - GroundHalf.Y - 2.5f) + East * -GroundWest }, RoadClass.Track),
            };
            if (b.FindWater(220f, out Vector2 toWater, out float distance) && distance > SquareRadius + 20f)
            {
                Vector2 bend = new Vector2(-toWater.Y, toWater.X) * distance * 0.12f;
                paths.Add((new List<Vector2> { c + toWater * (SquareRadius - 2f), c + toWater * (distance * 0.55f) + bend, c + toWater * MathF.Max(distance - 6f, SquareRadius + 10f) }, RoadClass.Footpath));
            }

            foreach ((List<Vector2> corners, RoadClass kind) in paths)
            {
                WorldRoad? path = RoadStage.BuildRoad(world, corners, kind);
                if (path == null) continue;
                world.Roads.Add(path);
                SettlementStage.ReserveRoad(world, path);
            }
        }

        // The great waygate, at the head of everything on a terrace of its own, facing down over the square. Lanterns go up the steps to it in pairs.
        private static void PlaceGate(SiteBuilder b, Vector2 c)
        {
            Vector2 at = c + North * GateAhead;
            if (b.PlaceNear(StructureCatalog.GreatWaygate, at, YawFacing(-North), ReachForEssentials, out Vector2 gate, padding: 0.4f))
            {
                b.Spawn(SpawnRole.Waygate, gate - North * (StructureCatalog.GreatWaygate.HalfExtents.Y + 3f));
            }
            for (int pair = 0; pair < 3; pair++)
            {
                float along = TableAhead + 4f + pair * 4.5f;
                foreach (float side in new[] { -1f, 1f })
                {
                    b.Place(StructureCatalog.StoneLantern, c + North * along + East * (side * 5.2f), 0f, padding: 0.2f);
                }
            }
            foreach (float side in new[] { -1f, 1f })
            {
                b.Place(StructureCatalog.StoneLantern, gate + East * (side * (StructureCatalog.GreatWaygate.HalfExtents.X + 2.4f)) - North * 1.6f, 0f, padding: 0.2f);
            }
        }

        // The map table at the top of the square, between the square and the gate, where the way to every land is chosen.
        private static void PlaceTable(SiteBuilder b, Vector2 c)
        {
            if (!b.PlaceNear(StructureCatalog.MapTable, c + North * TableAhead, YawFacing(-North), ReachForEssentials, out Vector2 table, padding: 0.6f)) return;
            b.Spawn(SpawnRole.MapTable, table - North * (StructureCatalog.MapTable.HalfExtents.Y + 1.8f));
        }

        // The merchant on one side of the square and the strongbox on the other, each facing in, and the beta vendor below the merchant.
        private static void PlaceTrade(SiteBuilder b, Vector2 c)
        {
            Vector2 stallAt = c - East * StallAside + North * 2f;
            if (b.PlaceNear(StructureCatalog.MarketStall, stallAt, YawFacing(East), ReachForEssentials, out Vector2 stall, padding: 0.6f))
            {
                b.Spawn(SpawnRole.Merchant, stall + East * 3.6f);
                b.Place(StructureCatalog.Crates, stall - East * 1.5f + North * 4.2f, 0f, padding: 0.3f);
                b.Place(StructureCatalog.Barrels, stall - East * 1f - North * 4f, 0f, padding: 0.3f);
            }

            // The beta vendor along from the merchant, who gives away anything there is, for testing the game.
            if (b.PlaceNear(StructureCatalog.MarketStall, c - East * StallAside - North * BetaBelow, YawFacing(East), ReachForEssentials, out Vector2 beta, padding: 0.6f))
            {
                b.Spawn(SpawnRole.BetaVendor, beta + East * 3.6f);
            }

            Vector2 boxAt = c + East * StashAside + North * 2f;
            if (b.PlaceNear(StructureCatalog.Strongbox, boxAt, YawFacing(-East), ReachForEssentials, out Vector2 box, padding: 0.6f))
            {
                b.Spawn(SpawnRole.Stash, box - East * (StructureCatalog.Strongbox.HalfExtents.Y + 1.6f));
                b.Place(StructureCatalog.Lantern, box + North * 3f, 0f, padding: 0.2f);
            }
        }

        // A quiet garden off the square, to sit in: lanterns, a bench under a tree and flowers. There's no shrine, as nothing here needs a blessing to face.
        private static void PlaceGarden(SiteBuilder b, Vector2 c)
        {
            Vector2 at = c + East * 16f + North * -14f;
            Vector2 facing = (c - at).Normalized();
            Vector2 side = new Vector2(-facing.Y, facing.X);
            b.Paint(GroundPaint.Plaza, Footprint.Circle(at, 3.2f), 2f);
            b.Place(StructureCatalog.StoneLantern, at + side * 3.6f, 0f, padding: 0.2f);
            b.Place(StructureCatalog.StoneLantern, at - side * 3.6f, 0f, padding: 0.2f);
            b.Place(StructureCatalog.Bench, at - facing * 1.5f, YawFacing(facing), padding: 0.2f);

            Plant(b, Broadleaves.Sakura, at - facing * 8f, 0.8f);
            foreach (float flank in new[] { -1f, 1f })
            {
                Plant(b, Undergrowth.Azalea, at + side * (flank * 6.2f) - facing * 1f, 0.55f);
                Plant(b, Undergrowth.Lavender, at + side * (flank * 5.6f) + facing * 2.4f, 0.6f);
            }
        }

        // A fenced yard south of the square with three groups to try a build on: one alone, a handful, and a crowd.
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
                b.Fence(centre + East * between - North * (GroundHalf.Y - 0.5f), centre + East * between + North * PenReach);
            }

            Vector2 big = centre + East * BigAlong - North * 1f;
            for (int i = 0; i < BigGroup; i++)
            {
                int row = i / SmallGroup;
                int column = i % SmallGroup;
                Vector2 offset = new Vector2((column - 2f) * DummyGap * 1.2f + (row % 2) * DummyGap * 0.6f, (row - 1f) * DummyGap * 1.25f);
                b.Spawn(SpawnRole.Dummy, big + offset);
            }

            Vector2 benches = centre + North * (GroundHalf.Y + 2.4f);
            b.Place(StructureCatalog.Bench, benches + East * 10f, YawFacing(-North), padding: 0.2f);
            b.Place(StructureCatalog.Bench, benches - East * 10f, YawFacing(-North), padding: 0.2f);
            b.Place(StructureCatalog.DryingRack, centre - North * (GroundHalf.Y + 2f) + East * 4f, 0f, padding: 0.3f);
            b.Place(StructureCatalog.Barrels, centre + East * (GroundHalf.X + 2.4f) - North * 5f, 0f, padding: 0.2f);
            b.Place(StructureCatalog.Lantern, centre + East * (GroundHalf.X + 1.6f) + North * (GroundHalf.Y + 1.6f), 0f, padding: 0.2f);
            b.Place(StructureCatalog.Lantern, centre - East * (GroundHalf.X + 1.6f) + North * (GroundHalf.Y + 1.6f), 0f, padding: 0.2f);
        }

        // A fence round the yard, open in the middle of its north side, where the path from the square comes in.
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
                        if (start.X > centre.X + YardGate) b.Fence(start, new Vector2(centre.X + YardGate, start.Y));
                        if (end.X < centre.X - YardGate) b.Fence(new Vector2(centre.X - YardGate, end.Y), end);
                        continue;
                    }
                    b.Fence(start, end);
                }
            }
        }

        // Homes around the square, facing it, leaving the north to the gate and its trees and the south for the way to the training ground.
        private static void PlaceHomes(SiteBuilder b, Vector2 c)
        {
            StructureDefinition[] kinds = { StructureCatalog.Inn, StructureCatalog.Hall, StructureCatalog.House, StructureCatalog.House, StructureCatalog.Cottage, StructureCatalog.Cottage, StructureCatalog.Longhouse, StructureCatalog.Cottage, StructureCatalog.House, StructureCatalog.Shed };
            float[] bearings = { GateClear, -GateClear, GateClear + 0.38f, -GateClear - 0.38f, GateClear + 0.76f, -GateClear - 0.76f, GateClear + 1.12f, -GateClear - 1.12f, GateClear + 1.46f, -GateClear - 1.46f };
            for (int i = 0; i < kinds.Length; i++)
            {
                StructureDefinition kind = kinds[i];
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    float distance = SquareRadius + HomesBeyond + kind.HalfExtents.Y + attempt * 2.5f;
                    Vector2 outward = new Vector2(MathF.Sin(bearings[i]), MathF.Cos(bearings[i]));
                    Vector2 at = c + outward * distance;
                    if (!b.Place(kind, at, YawFacing(-outward), padding: 1.8f, maxSlope: 0.34f)) continue;

                    b.Paint(GroundPaint.Yard, Footprint.Circle(at - outward * (kind.HalfExtents.Y + 1f), kind.HalfExtents.X * 0.8f), 4f);
                    if (b.Random.Chance(0.6f)) b.Place(StructureCatalog.Woodpile, at + new Vector2(-outward.Y, outward.X) * (kind.HalfExtents.X + 2f), YawFacing(outward), padding: 0.3f);
                    if (b.Random.Chance(0.4f)) b.Place(StructureCatalog.Lantern, at - outward * (kind.HalfExtents.Y + 2.2f) + new Vector2(-outward.Y, outward.X) * (kind.HalfExtents.X * 0.7f), 0f, padding: 0.2f);
                    break;
                }
            }
        }

        // Blossom, planted: great trees either side of the gate and behind it, so that its arch frames them, more at the top corners of the square, a ring of shrubs round the square, and trees
        // off its lower corners. These are the Haven's own and nobody else's, so every one is chosen here rather than drawn from the woods. A tree needs room for its crown, so they stand well
        // apart and clear of the walk, and the gardens between them are shrubs.
        private static void PlantGardens(SiteBuilder b, Vector2 c)
        {
            Vector2 gate = c + North * GateAhead;
            Plant(b, Broadleaves.Sakura, gate + North * 14f, 1f);
            foreach (float side in new[] { -1f, 1f })
            {
                Plant(b, Broadleaves.Sakura, gate + East * (side * 17f) + North * 1.5f, 0.9f);
                Plant(b, Broadleaves.WhiteCherry, gate + East * (side * 16f) + North * 16f, 0.75f);
                Plant(b, Broadleaves.JapaneseMaple, c + East * (side * 18f) + North * (TableAhead + 2f), 0.6f);
                Plant(b, side < 0f ? Broadleaves.WisteriaTree : Broadleaves.WhiteCherry, c + East * (side * 21f) + North * -14f, 0.65f);
                Plant(b, side < 0f ? Broadleaves.Sakura : Broadleaves.JapaneseMaple, c + East * (side * 27f) + North * -26f, 0.7f);

                // Azaleas up either side of the walk, below the lanterns.
                for (int step = 0; step < 3; step++) Plant(b, step % 2 == 0 ? Undergrowth.Azalea : Undergrowth.WhiteAzalea, c + North * (TableAhead + 5f + step * 4.5f) + East * (side * 8f), 0.6f);
            }

            // A low ring of shrubs round the square, broken where the walk, the paths, the stall and the strongbox meet it.
            for (int i = 0; i < HedgeShrubs; i++)
            {
                float bearing = i * MathF.Tau / HedgeShrubs;
                if (Near(bearing, 0f, 0.4f) || Near(bearing, MathF.PI, 0.45f) || Near(bearing, MathF.PI * 0.5f, 0.4f) || Near(bearing, -MathF.PI * 0.5f, 0.4f)) continue;
                WorldAsset shrub = (i % 3) switch { 0 => Undergrowth.Boxwood, 1 => Undergrowth.Azalea, _ => Undergrowth.WhiteAzalea };
                Plant(b, shrub, c + Toward(bearing) * (SquareRadius + HedgeRing), 0.5f);
            }
        }

        private static Vector2 Toward(float bearing) => new Vector2(MathF.Sin(bearing), MathF.Cos(bearing));

        private static bool Near(float bearing, float to, float within) => MathF.Abs(MathF.IEEERemainder(bearing - to, MathF.Tau)) < within;

        private static void Plant(SiteBuilder b, WorldAsset asset, Vector2 at, float growth)
        {
            b.World.Plantings.Add(new Planting(asset, at, asset.Scale.Lerp(growth), b.Random.Float() * MathF.Tau));
        }

        // A jetty where the path meets the water, with a boat or two drawn up beside it, as the Haven is an island and everyone came by water once.
        private static void PlaceShore(SiteBuilder b, Vector2 c)
        {
            if (!b.FindWater(220f, out Vector2 toWater, out float distance) || distance < SquareRadius + 20f) return;
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

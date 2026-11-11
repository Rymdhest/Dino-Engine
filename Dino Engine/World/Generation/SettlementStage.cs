using Dino_Engine.Modelling.Procedural.Fence;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.World.Generation.SiteBuilder;

namespace Dino_Engine.World.Generation
{
    /// <summary>
    /// Builds what stands at each site. Buildings line the streets with their doors to the road, as houses do in the villages
    /// Emilien et al. measured; fields and fences wrap around them; camps and strongholds close themselves off with walls that
    /// open where their road comes in. Everything is marked for the game with <see cref="SiteSpawn"/>s. Between sites, roadside
    /// markers keep something ahead of the traveller, after the "30 second rule" of Ghost of Tsushima's world design.
    /// </summary>
    public sealed class SettlementStage : IWorldStage
    {
        private const float WaysideSpacing = 130f;
        private const float AltarForecourt = 4.7f;      // A road that ends at an altar stops this far short of it, which clears the road's own width and leaves room to stand.
        internal const float KeepRoadEnd = 0.66f;        // Of a keep's radius, where a road that ends there stops: inside its gatehouse, short of the arena in the middle of it and the camp round that.
        private const float EssentialReach = 1.5f;      // Share of a site's radius something it can't do without may stray to find ground: a neighbour's field can cover all of a small site.

        public string Description => "Raising the villages";
        public float Cost => 1f;

        public void Run(WorldData world)
        {
            world.Placement = new PlacementIndex(world.Size);
            // The Haven builds as its halls do, in oak, lime and thatch.
            world.Style = world.Plan.Archetype.Peaceful ? BuildingStyle.Timber : StyleOf(world);
            foreach (WorldSite site in world.Sites)
            {
                if (site.Kind == SiteKind.Graveyard) EndRoadsShortOf(world, site.Position, StructureCatalog.Church.HalfExtents.Y + ChurchForecourt);
                if (site.Kind is SiteKind.Shrine or SiteKind.StandingStones) EndRoadsShortOf(world, site.Position, StructureCatalog.Altar.HalfExtents.X + AltarForecourt);
                if (site.Kind == SiteKind.Waygate) EndRoadsShortOf(world, site.Position, StructureCatalog.Waygate.HalfExtents.Y + AltarForecourt);
                if (site.Kind == SiteKind.Keep) EndRoadsShortOf(world, site.Position, site.Radius * KeepRoadEnd);
            }
            foreach (WorldRoad road in world.Roads) ReserveRoad(world, road);

            // What a land can't be done without goes down first, so that nothing a neighbour lays out, a field or the track to it, takes its ground. Each site draws on a
            // fork of its own, so the order changes nothing it draws.
            WorldRandom random = world.Random.Fork("settlements");
            foreach (WorldSite site in world.Sites.OrderBy(site => BuiltFirst(site.Kind) ? 0 : 1))
            {
                world.Clearings.Add(new Clearing(site.Position, site.Radius * ClearingShare(site.Kind), 34f));
                var builder = new SiteBuilder(world, site, random.Fork(site.Id));
                switch (site.Kind)
                {
                    case SiteKind.Landing: BuildLanding(builder); break;
                    case SiteKind.Village: BuildVillage(builder, houses: builder.Random.Int(14, 24), fields: builder.Random.Int(4, 7)); break;
                    case SiteKind.Hamlet:
                        BuildVillage(builder, houses: builder.Random.Int(5, 9), fields: builder.Random.Int(2, 4));
                        Overrun(builder, SpawnRole.Beast, builder.Random.Int(2, 3));
                        break;
                    case SiteKind.Farmstead:
                        BuildFarmstead(builder);
                        Overrun(builder, SpawnRole.Beast, builder.Random.Int(3, 4));
                        break;
                    case SiteKind.FishingHamlet:
                        BuildFishingHamlet(builder);
                        Overrun(builder, SpawnRole.Raider, builder.Random.Int(3, 4));
                        break;
                    case SiteKind.LumberCamp:
                        BuildLumberCamp(builder);
                        Overrun(builder, SpawnRole.Raider, builder.Random.Int(2, 3));
                        break;
                    case SiteKind.Shrine: BuildShrine(builder); break;
                    case SiteKind.StandingStones: BuildStandingStones(builder); break;
                    case SiteKind.Watchtower: BuildWatchtower(builder); break;
                    case SiteKind.Ruins: BuildRuins(builder); break;
                    case SiteKind.BanditCamp: BuildBanditCamp(builder); break;
                    case SiteKind.Graveyard: BuildGraveyard(builder); break;
                    case SiteKind.Keep: BuildKeep(builder); break;
                    case SiteKind.Waygate: BuildWaygate(builder); break;
                    case SiteKind.Haven: HavenBuilder.Build(builder); break;
                    case SiteKind.Arena: ArenaBuilder.Build(builder); break;
                }
            }

            PlaceWayside(world, random.Fork("wayside"));
            PlaceLandmarks(world, random.Fork("landmarks"));
        }

        // The way out of a land, and the altars a hero kindles: a site is nothing without them, and WorldValidator fails one that lost its own.
        private static bool BuiltFirst(SiteKind kind) => kind is SiteKind.Landing or SiteKind.Waygate or SiteKind.Shrine or SiteKind.StandingStones;

        // Each language has its people, and they build the way they build, wherever the land doesn't decide for them (WorldData.StyleOf).
        private static BuildingStyle StyleOf(WorldData world)
        {
            return world.Plan.Language switch
            {
                1 => BuildingStyle.Eastern,
                2 => BuildingStyle.Stone,
                _ => BuildingStyle.Timber,
            };
        }

        private static float ClearingShare(SiteKind kind) => kind switch
        {
            SiteKind.Village or SiteKind.Hamlet or SiteKind.Keep => 1.25f,
            SiteKind.Haven => 1.05f,    // Its own trees are planted, and the woods come close round them.
            SiteKind.Arena => 1.3f,     // Bare ground all round, as an arena is a thing to be seen from far off.
            SiteKind.Ruins or SiteKind.Graveyard => 0.8f,
            _ => 1.05f,
        };

        internal static void ReserveRoad(WorldData world, WorldRoad road)
        {
            float halfWidth = road.HalfWidth + 0.8f;
            for (int i = 1; i < road.Points.Count; i++)
            {
                world.Placement.Add(Footprint.Segment(road.Points[i - 1].Xz, road.Points[i].Xz, halfWidth), PlacementLayer.Road);
            }
        }

        private static void BuildLanding(SiteBuilder b)
        {
            Vector2 toRoad = b.Site.RoadDirection;
            b.Paint(GroundPaint.Plaza, Footprint.Circle(b.Centre, b.Site.Radius * 0.45f), 9f);

            // The gate stands back from the road with its face towards it, so the first thing seen is the way onwards.
            b.PlaceNear(StructureCatalog.Waygate, b.Centre - toRoad * 7f, YawFacing(toRoad), b.Site.Radius * EssentialReach, out Vector2 gate, padding: 0.5f);
            b.Spawn(SpawnRole.Waygate, gate + toRoad * 2.5f);

            Vector2 side = new Vector2(-toRoad.Y, toRoad.X);
            b.Place(StructureCatalog.StoneLantern, gate + side * 6.5f + toRoad * 1.5f, 0f, padding: 0.3f);
            b.Place(StructureCatalog.StoneLantern, gate - side * 6.5f + toRoad * 1.5f, 0f, padding: 0.3f);

            Vector2 camp = b.Centre + side * 11f;
            b.Place(StructureCatalog.Tent, camp, YawFacing(b.Centre - camp), padding: 0.5f);
            b.Place(StructureCatalog.Campfire, b.Centre + side * 6f + toRoad * 5f, 0f, padding: 0.5f);
            b.Scatter(StructureCatalog.Crates, camp, 7f);
            b.Scatter(StructureCatalog.Barrels, camp, 7f);
            b.Spawn(SpawnRole.Merchant, b.Centre + side * 6.5f - toRoad * 1f);
        }

        private static void BuildVillage(SiteBuilder b, int houses, int fields)
        {
            WorldSite site = b.Site;
            bool isVillage = site.Kind == SiteKind.Village;
            bool fortified = isVillage && site.Remoteness > 0.3f && b.Random.Chance(0.55f);

            AddLanes(b, count: isVillage ? b.Random.Int(2, 4) : b.Random.Int(0, 2));

            // Walls before houses, so that houses keep to the inside.
            if (fortified)
            {
                b.Ring(FenceUse.Stockade, site.Radius * 1.02f, b.Random.Int(12, 16), site.RoadDirection, 12f);
            }

            b.Paint(GroundPaint.Plaza, Footprint.Circle(b.Centre, isVillage ? 13f : 7f), 9f);
            if (isVillage) PlaceRefuge(b);
            PlaceSquare(b, isVillage);

            List<List<Vector3>> streets = b.StreetsWithin(site.Radius);
            int built = 0;
            int attempts = 0;
            // Passes outward through the streets until the village has its houses or the streets are full.
            while (built < houses && attempts < 3)
            {
                attempts++;
                foreach (List<Vector3> street in streets)
                {
                    built += LineStreet(b, street, houses - built, setback: attempts * 1.5f);
                    if (built >= houses) break;
                }
            }

            PlaceFields(b, fields);

            for (int i = 0; i < 5; i++)
            {
                b.Scatter(StructureCatalog.Barrels, b.Centre, site.Radius * 0.8f);
                b.Scatter(StructureCatalog.Crates, b.Centre, site.Radius * 0.8f);
            }
            b.Scatter(StructureCatalog.Cart, b.Centre, site.Radius * 0.7f, attempts: 10);
            b.Scatter(StructureCatalog.Haystack, b.Centre, site.Radius, attempts: 10);

            PlaceStreetLanterns(b, streets);
        }

        // What a village in a land keeps for whoever fights for it: a waygate at the edge of its square, a shrine in it, and a strongbox where what's put away can be got at. These go
        // down first, before anything else in the square, as a village is nothing to a hero without them, and WorldValidator fails one that lost any.
        private static void PlaceRefuge(SiteBuilder b)
        {
            WorldSite site = b.Site;
            float turn = b.Random.Float() * MathF.Tau;

            // Off to one side of the road through, so that it doesn't stand in anyone's way.
            Vector2 gateWay = AwayFromRoads(b, turn);
            Vector2 gateAt = b.Centre + gateWay * 17f;
            if (b.PlaceNear(StructureCatalog.Waygate, gateAt, YawFacing(-gateWay), site.Radius * EssentialReach, out Vector2 gate, padding: 0.6f))
            {
                Vector2 front = (b.Centre - gate).LengthSquared > 1f ? (b.Centre - gate).Normalized() : -gateWay;
                b.Place(StructureCatalog.StoneLantern, gate + new Vector2(-front.Y, front.X) * 6.5f + front * 1.5f, 0f, padding: 0.3f);
                b.Place(StructureCatalog.StoneLantern, gate - new Vector2(-front.Y, front.X) * 6.5f + front * 1.5f, 0f, padding: 0.3f);
                b.Spawn(SpawnRole.Waygate, gate + front * 2.5f);
            }

            Vector2 shrineWay = Direction(MathF.Atan2(gateWay.Y, gateWay.X) + MathF.PI * 0.75f);
            if (b.PlaceNear(StructureCatalog.Altar, b.Centre + shrineWay * 10f, YawFacing(-shrineWay), site.Radius * EssentialReach, out Vector2 altar, padding: 0.5f))
            {
                Vector2 front = (b.Centre - altar).LengthSquared > 1f ? (b.Centre - altar).Normalized() : -shrineWay;
                b.Spawn(SpawnRole.Altar, altar + front * 3f);
            }

            Vector2 stashWay = Direction(MathF.Atan2(gateWay.Y, gateWay.X) - MathF.PI * 0.7f);
            if (b.PlaceNear(StructureCatalog.Strongbox, b.Centre + stashWay * 12f, YawFacing(-stashWay), site.Radius * EssentialReach, out Vector2 box, padding: 0.5f))
            {
                Vector2 front = (b.Centre - box).LengthSquared > 1f ? (b.Centre - box).Normalized() : -stashWay;
                b.Spawn(SpawnRole.Stash, box + front * (StructureCatalog.Strongbox.HalfExtents.Y + 1.6f));
            }

            // Someone always trades, whatever room the square has left for stalls.
            Vector2 stallWay = Direction(MathF.Atan2(gateWay.Y, gateWay.X) + MathF.PI * 1.35f);
            if (b.PlaceNear(StructureCatalog.MarketStall, b.Centre + stallWay * 11f, YawFacing(-stallWay), site.Radius * EssentialReach, out Vector2 stall, padding: 0.6f))
            {
                Vector2 front = (b.Centre - stall).LengthSquared > 1f ? (b.Centre - stall).Normalized() : -stallWay;
                b.Spawn(SpawnRole.Merchant, stall + front * 3.6f);
            }
        }

        // The way out of the middle of a place that's furthest from every road through it.
        private static Vector2 AwayFromRoads(SiteBuilder b, float start)
        {
            Vector2 best = Direction(start);
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < 12; i++)
            {
                Vector2 candidate = Direction(start + i * (MathF.Tau / 12f));
                float nearest = 2f;
                foreach (WorldRoad road in b.World.Roads)
                {
                    foreach (Vector3 point in road.Points)
                    {
                        Vector2 offset = point.Xz - b.Centre;
                        float length = offset.Length;
                        if (length < 4f || length > b.Site.Radius) continue;
                        nearest = MathF.Min(nearest, 1f - Vector2.Dot(offset / length, candidate));
                    }
                }
                if (nearest <= bestScore) continue;
                bestScore = nearest;
                best = candidate;
            }
            return best;
        }

        // A place whose people are gone, and something else holds it now: whoever lived there is who holds it, and more besides, and what they've taken is in a chest.
        private static void Overrun(SiteBuilder b, SpawnRole by, int more)
        {
            List<SiteSpawn> spawns = b.Site.Spawns;
            for (int i = 0; i < spawns.Count; i++)
            {
                if (spawns[i].Role is SpawnRole.Villager or SpawnRole.Merchant or SpawnRole.Elder) spawns[i] = new SiteSpawn(by, spawns[i].Position);
            }
            b.SpawnAround(by, b.Centre, b.Site.Radius * 0.55f, more);
            SpawnChest(b, b.Centre, b.Site.Radius * 0.45f);
        }

        // Every place a land asks to be freed has a chest, so there has to be one, wherever there's ground for it.
        private static void SpawnChest(SiteBuilder b, Vector2 around, float radius)
        {
            // Out as far as the edge of the place if it has to, as a held place with no chest is a promise broken, and only then where it was asked for, whatever stands there.
            int before = b.Site.Spawns.Count;
            float furthest = MathF.Max(radius * 3f, b.Site.Radius);
            for (float reach = radius; reach <= furthest && b.Site.Spawns.Count == before; reach += radius * 0.5f) b.SpawnAround(SpawnRole.Chest, around, reach, 1);
            if (b.Site.Spawns.Count == before) b.Spawn(SpawnRole.Chest, around);
        }

        // A gate of the land's old roads, on its own beside a road, with a little paved ground before it and a lantern either side.
        private static void BuildWaygate(SiteBuilder b)
        {
            Vector2 toRoad = b.Site.RoadDirection;
            Vector2 side = new Vector2(-toRoad.Y, toRoad.X);
            b.Paint(GroundPaint.Plaza, Footprint.Circle(b.Centre, b.Site.Radius * 0.6f), 6f);

            b.PlaceNear(StructureCatalog.Waygate, b.Centre - toRoad * 2f, YawFacing(toRoad), b.Site.Radius * EssentialReach, out Vector2 gate, padding: 0.5f);
            b.Spawn(SpawnRole.Waygate, gate + toRoad * 2.5f);
            b.Place(StructureCatalog.StoneLantern, gate + side * 6.5f + toRoad * 1.5f, 0f, padding: 0.3f);
            b.Place(StructureCatalog.StoneLantern, gate - side * 6.5f + toRoad * 1.5f, 0f, padding: 0.3f);
            b.Scatter(StructureCatalog.Cairn, b.Centre + toRoad * 4f, b.Site.Radius * 0.8f, attempts: 6, padding: 0.6f);
        }

        // Short lanes off the square, in the directions the roads don't already take.
        private static void AddLanes(SiteBuilder b, int count)
        {
            WorldSite site = b.Site;
            WorldData world = b.World;
            for (int i = 0; i < count; i++)
            {
                float bestScore = float.NegativeInfinity;
                Vector2 bestDirection = Vector2.UnitX;
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    Vector2 candidate = Direction(b.Random.Float() * MathF.Tau);
                    float nearest = float.PositiveInfinity;
                    foreach (WorldRoad road in world.Roads)
                    {
                        foreach (Vector3 point in road.Points)
                        {
                            Vector2 offset = point.Xz - site.Position;
                            float length = offset.Length;
                            if (length < 12f || length > site.Radius) continue;
                            nearest = MathF.Min(nearest, 1f - Vector2.Dot(offset / length, candidate));
                        }
                    }
                    float score = MathF.Min(nearest, 2f) - world.Elevation.SlopeAt(site.Position + candidate * site.Radius * 0.6f) * 2f;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestDirection = candidate;
                    }
                }

                float laneLength = site.Radius * b.Random.Range(0.65f, 1f);
                Vector2 bend = new Vector2(-bestDirection.Y, bestDirection.X) * laneLength * b.Random.Range(-0.2f, 0.2f);
                var corners = new List<Vector2>
                {
                    site.Position + bestDirection * 4f,
                    site.Position + bestDirection * laneLength * 0.5f + bend,
                    site.Position + bestDirection * laneLength,
                };
                if (world.Elevation.Sample(corners[^1]) < WorldPlan.SeaLevel + 1f) continue;

                WorldRoad? lane = RoadStage.BuildRoad(world, corners, RoadClass.Footpath);
                if (lane == null || LaneIsBlocked(world, lane)) continue;
                world.Roads.Add(lane);
                ReserveRoad(world, lane);
            }
        }

        private static bool LaneIsBlocked(WorldData world, WorldRoad lane)
        {
            for (int i = 1; i < lane.Points.Count; i++)
            {
                Footprint stretch = Footprint.Segment(lane.Points[i - 1].Xz, lane.Points[i].Xz, lane.HalfWidth + 0.8f);
                if (world.Placement.Overlaps(stretch, PlacementLayer.Built)) return true;
            }
            return false;
        }

        private static void PlaceSquare(SiteBuilder b, bool isVillage)
        {
            // The square's heart is usually where roads meet, so its well stands to one side, with a trough beside it for the beasts.
            for (int attempt = 0; attempt < 14; attempt++)
            {
                Vector2 spot = b.Centre + Direction(b.Random.Float() * MathF.Tau) * b.Random.Range(6f, 13f);
                if (!b.Place(StructureCatalog.Well, spot, 0f)) continue;
                WorldRandom trough = b.Random.Fork("trough");
                Vector2 away = (spot - b.Centre).Normalized();
                Vector2 along = new Vector2(-away.Y, away.X) * (trough.Chance(0.5f) ? 1f : -1f);
                b.Place(StructureCatalog.Trough, spot + along * 4.6f, YawFacing(away), padding: 0.3f);
                break;
            }

            if (isVillage)
            {
                int stalls = b.Random.Int(2, 4);
                for (int i = 0; i < stalls; i++)
                {
                    float angle = b.Random.Float() * MathF.Tau;
                    Vector2 spot = b.Centre + Direction(angle) * b.Random.Range(9f, 15f);
                    if (b.Place(StructureCatalog.MarketStall, spot, YawFacing(b.Centre - spot), padding: 0.6f))
                    {
                        b.Spawn(SpawnRole.Merchant, spot + (b.Centre - spot).Normalized() * 3.6f);
                    }
                }
            }
            b.SpawnAround(SpawnRole.Villager, b.Centre, isVillage ? 14f : 8f, isVillage ? 3 : 1);
        }

        // Walks a street placing houses on both sides. Returns how many it placed.
        private static int LineStreet(SiteBuilder b, List<Vector3> street, int wanted, float setback)
        {
            WorldSite site = b.Site;
            int placed = 0;
            float[] nextFree = { 0f, 0f };  // Metres along the street where each side is free again.
            float walked = 0f;

            for (int i = 1; i < street.Count - 1 && placed < wanted; i++)
            {
                Vector2 point = street[i].Xz;
                Vector2 tangent = (street[i + 1].Xz - street[i - 1].Xz).Normalized();
                walked += (street[i].Xz - street[i - 1].Xz).Length;
                float fromCentre = (point - site.Position).Length / site.Radius;
                if (fromCentre < 0.12f) continue;

                for (int side = 0; side < 2 && placed < wanted; side++)
                {
                    if (walked < nextFree[side]) continue;

                    Vector2 outward = side == 0 ? new Vector2(-tangent.Y, tangent.X) : new Vector2(tangent.Y, -tangent.X);
                    float halfWidth = b.RoadHalfWidthAt(point);
                    StructureDefinition? type = null;
                    Vector2 position = default;
                    foreach (StructureDefinition candidate in HousesFor(b, fromCentre))
                    {
                        float offset = halfWidth + 2.6f + setback + candidate.HalfExtents.Y + b.Random.Range(0f, 1.4f);
                        Vector2 at = point + outward * offset;
                        if (!b.Place(candidate, at, YawFacing(-outward), padding: 1.4f, maxSlope: 0.34f)) continue;
                        type = candidate;
                        position = at;
                        break;
                    }
                    if (type == null) continue;
                    if (type == StructureCatalog.Hall) b.Site.HasHall = true;
                    else if (type == StructureCatalog.Inn) b.Site.HasInn = true;

                    placed++;
                    nextFree[side] = walked + type.Width + b.Random.Range(2.2f, 6.5f);
                    FurnishPlot(b, type, position, outward, tangent);
                }
            }
            return placed;
        }

        // What's tried in a gap along a street, in turn until one fits: near a village's middle its hall and its inn while it lacks them, as they're big and may only
        // fit somewhere further on, and then whatever house the gap would have had. A village marks its hall and inn built only once they stand (LineStreet), as
        // marking them when first tried once left almost every village without either, and without the elder who stands at the hall's door.
        private static IEnumerable<StructureDefinition> HousesFor(SiteBuilder b, float fromCentre)
        {
            if (b.Site.Kind == SiteKind.Village && fromCentre < 0.65f)
            {
                if (!b.Site.HasHall) yield return StructureCatalog.Hall;
                if (!b.Site.HasInn) yield return StructureCatalog.Inn;
            }
            yield return PickHouse(b, fromCentre);
        }

        private static StructureDefinition PickHouse(SiteBuilder b, float fromCentre)
        {
            if (fromCentre > 0.75f && b.Random.Chance(0.35f)) return StructureCatalog.Barn;
            if (fromCentre < 0.55f && b.Random.Chance(0.4f)) return StructureCatalog.House;
            return b.Random.Chance(0.3f) ? StructureCatalog.Longhouse : StructureCatalog.Cottage;
        }

        // What collects around a house: a yard, a shed, firewood and whoever lives there.
        private static void FurnishPlot(SiteBuilder b, StructureDefinition type, Vector2 position, Vector2 outward, Vector2 tangent)
        {
            b.Paint(GroundPaint.Yard, Footprint.Circle(position - outward * (type.HalfExtents.Y + 1f), type.HalfExtents.X * 0.8f), 4f);

            Vector2 door = position - outward * (type.HalfExtents.Y + 2.4f);
            if (type == StructureCatalog.Hall) b.Spawn(SpawnRole.Elder, door);
            else if (type == StructureCatalog.Inn) b.Spawn(SpawnRole.Merchant, door);
            else if (b.Random.Chance(0.55f)) b.Spawn(SpawnRole.Villager, door + tangent * b.Random.Range(-3f, 3f));

            Vector2 back = position + outward * (type.HalfExtents.Y + 3.2f);
            if (b.Random.Chance(0.45f)) b.Place(StructureCatalog.Woodpile, position + tangent * (type.HalfExtents.X + 2f), YawFacing(outward), padding: 0.3f);
            if (b.Random.Chance(0.3f)) b.Place(StructureCatalog.Barrels, position - tangent * (type.HalfExtents.X + 1.8f), 0f, padding: 0.3f);

            if (b.Random.Chance(0.4f))
            {
                Vector2 shed = back + tangent * b.Random.Range(-4f, 4f);
                if (b.Place(StructureCatalog.Shed, shed + outward * 3f, YawFacing(-outward), padding: 0.8f)) back += outward * 7f;
            }

            if (b.Random.Chance(0.35f))
            {
                // A back garden, fenced off from the wilds.
                Vector2 garden = position + outward * (type.HalfExtents.Y + 6.5f);
                Vector2 half = new Vector2(type.HalfExtents.X, 4.2f);
                Footprint plot = Footprint.Box(garden, half, YawFacing(-outward));
                if (b.Fits(plot, 0.2f, 0.6f))
                {
                    b.World.Placement.Add(plot, PlacementLayer.Keepout);
                    b.FenceAround(plot, -outward, FenceUse.Garden);
                }
            }

            YardClutter(b, type, position, outward, tangent);
        }

        // What a household keeps about its house, on a stream of its own so that the rest of the village is laid out as it always was: an oven and jars where it's dry
        // enough to bake out of doors, hides drying where it's cold, and anywhere bees, hens, washing, a trough, a barrow and sacks of grain, more of them at a barn.
        private static void YardClutter(SiteBuilder b, StructureDefinition type, Vector2 position, Vector2 outward, Vector2 tangent)
        {
            if (type == StructureCatalog.Hall) return;
            WorldRandom random = b.Random.Fork("yard");
            bool barn = type == StructureCatalog.Barn;
            Vector2 side = tangent * (random.Chance(0.5f) ? 1f : -1f);
            Vector2 back = position + outward * (type.HalfExtents.Y + 2.8f);
            float width = type.HalfExtents.X;
            switch (b.Style)
            {
                case BuildingStyle.Adobe:
                case BuildingStyle.Tiled:
                    if (random.Chance(0.35f)) b.Place(StructureCatalog.Oven, position + side * (width + 3f) + outward * 1.5f, YawFacing(-outward), padding: 0.4f);
                    if (random.Chance(0.4f)) b.Place(StructureCatalog.Jars, position - side * (width + 2.1f) - outward * (type.HalfExtents.Y * 0.4f), YawFacing(side), padding: 0.3f);
                    break;
                case BuildingStyle.Log:
                    if (random.Chance(0.3f)) b.Place(StructureCatalog.DryingRack, back - side * (width * 0.5f) + outward * 1.5f, YawFacing(outward), padding: 0.3f);
                    break;
            }
            if (random.Chance(0.16f)) b.Place(StructureCatalog.Hives, back + tangent * random.Range(-3f, 3f) + outward * 2.2f, YawFacing(-outward), padding: 0.6f);
            if (random.Chance(barn ? 0.3f : 0.18f)) b.Place(StructureCatalog.Coop, back - side * (width * 0.6f) + outward * 0.6f, YawFacing(-outward), padding: 0.5f);
            if (random.Chance(0.2f)) b.Place(StructureCatalog.Laundry, back + side * (width * 0.35f) + outward * 1.4f, YawFacing(outward), padding: 0.4f);
            if (random.Chance(barn ? 0.45f : 0.12f)) b.Place(StructureCatalog.Trough, position + side * (width + 2.4f), YawFacing(side), padding: 0.3f);
            if (random.Chance(barn ? 0.4f : 0.14f)) b.Place(StructureCatalog.Barrow, position + side * (width + 1.6f) - outward * 1.2f, YawFacing(tangent) + random.Range(-0.5f, 0.5f), padding: 0.2f);
            if (random.Chance(barn ? 0.55f : 0.14f)) b.Place(StructureCatalog.Sacks, position - side * (width + 1.6f) + outward * 0.8f, random.Float() * MathF.Tau, padding: 0.2f);
        }

        private static void PlaceFields(SiteBuilder b, int wanted)
        {
            WorldSite site = b.Site;
            int placed = 0;
            for (int attempt = 0; attempt < wanted * 10 && placed < wanted; attempt++)
            {
                Vector2 direction = Direction(b.Random.Float() * MathF.Tau);
                float distance = site.Radius * b.Random.Range(0.75f, 1.55f);
                Vector2 position = site.Position + direction * distance;
                Vector2 half = new Vector2(b.Random.Range(11f, 20f), b.Random.Range(8f, 15f));
                // A field is its own place's, and never runs onto a neighbour's ground, where it once closed off a camp's only way in.
                if (b.World.Sites.Any(other => other != site && (other.Position - position).Length < other.Radius + half.Length + FieldClearance)) continue;
                // Fields line up with the road they lie along, not with the compass, and open onto it.
                Vector2 towardsRoad = TowardsNearestRoad(b, position);
                if (!b.Field(position, half, YawFacing(towardsRoad), towardsRoad, out Vector2 gate)) continue;
                placed++;
                LayFieldTrack(b, gate, position);
            }
        }

        private const float FieldClearance = 6f;        // Metres a field keeps off any other place's ground.
        private const float LongestFieldTrack = 80f;    // Metres a field's track runs to a road. A field further out than that is reached across the open ground.
        private const float FieldTrackStart = 2.5f;     // Metres out from the gate that the track starts, clear of the gateposts.

        // A field is worked from somewhere, so a footpath runs from its gate to the nearest road, if there's one near enough and nothing built stands in the way.
        private static void LayFieldTrack(SiteBuilder b, Vector2 gate, Vector2 field)
        {
            Vector2 outward = (gate - field).Normalized();
            Vector2 start = gate + outward * FieldTrackStart;
            float best = float.PositiveInfinity;
            Vector3 end = default;
            foreach (WorldRoad road in b.World.Roads)
            {
                foreach (Vector3 point in road.Points)
                {
                    float distance = (point.Xz - start).LengthSquared;
                    if (distance >= best) continue;
                    best = distance;
                    end = point;
                }
            }
            float length = MathF.Sqrt(best);
            if (length > LongestFieldTrack || length < 3f) return;

            // Straight out of the gate first, and then round to the road, so that it doesn't run along the fence.
            var corners = new List<Vector2> { start, start + outward * MathF.Min(4f, length * 0.4f), end.Xz };
            WorldRoad? track = RoadStage.BuildRoad(b.World, corners, RoadClass.Footpath, endHeight: end.Y);
            if (track == null || LaneIsBlocked(b.World, track)) return;
            b.World.Roads.Add(track);
            ReserveRoad(b.World, track);
        }

        private static Vector2 TowardsNearestRoad(SiteBuilder b, Vector2 position)
        {
            float best = float.PositiveInfinity;
            Vector2 direction = (b.Centre - position).Normalized();
            foreach (WorldRoad road in b.World.Roads)
            {
                for (int i = 0; i < road.Points.Count; i += 2)
                {
                    Vector2 offset = road.Points[i].Xz - position;
                    float distance = offset.LengthSquared;
                    if (distance < best && distance > 1f)
                    {
                        best = distance;
                        direction = offset.Normalized();
                    }
                }
            }
            return direction;
        }

        private static void PlaceStreetLanterns(SiteBuilder b, List<List<Vector3>> streets)
        {
            // A lantern on a wooden post, a white pillar where they limewash, and paper lanterns on cane where those are hung.
            StructureDefinition lamp = b.Style switch
            {
                BuildingStyle.Adobe or BuildingStyle.Tiled => StructureCatalog.PillarLamp,
                BuildingStyle.Eastern or BuildingStyle.Stilt => StructureCatalog.PaperLantern,
                _ => StructureCatalog.Lantern,
            };
            foreach (List<Vector3> street in streets)
            {
                for (int i = 5; i < street.Count; i += 9)
                {
                    Vector2 tangent = (street[i].Xz - street[i - 1].Xz).Normalized();
                    Vector2 side = new Vector2(-tangent.Y, tangent.X) * (i % 2 == 0 ? 1f : -1f);
                    Vector2 spot = street[i].Xz + side * (b.RoadHalfWidthAt(street[i].Xz) + 1.6f);
                    // Its lantern hangs out over the street.
                    b.Place(lamp, spot, YawFacing(-side), padding: 0.2f);
                }
            }
        }

        private static void BuildFarmstead(SiteBuilder b)
        {
            Vector2 toRoad = b.Site.RoadDirection;
            Vector2 side = new Vector2(-toRoad.Y, toRoad.X);

            Vector2 housePosition = b.Centre + toRoad * 4f;
            b.Place(StructureCatalog.House, housePosition, YawFacing(toRoad), padding: 1f);
            b.Paint(GroundPaint.Yard, Footprint.Circle(b.Centre, 13f), 5f);
            b.Spawn(SpawnRole.Villager, housePosition + toRoad * 8.4f);

            Vector2 barn = b.Centre - toRoad * 14f + side * b.Random.Range(-5f, 5f);
            b.Place(StructureCatalog.Barn, barn, YawFacing(b.Centre - barn), padding: 1f);

            b.Scatter(StructureCatalog.Haystack, b.Centre + side * 14f, 8f);
            b.Scatter(StructureCatalog.Haystack, b.Centre + side * 14f, 8f);
            b.Scatter(StructureCatalog.Cart, b.Centre - side * 12f, 6f);
            b.Scatter(StructureCatalog.Woodpile, b.Centre, 12f);
            b.Scatter(StructureCatalog.Barrels, b.Centre, 12f);

            int fields = b.Random.Int(2, 3);
            for (int attempt = 0, placed = 0; attempt < 18 && placed < fields; attempt++)
            {
                Vector2 direction = Direction(b.Random.Float() * MathF.Tau);
                Vector2 position = b.Centre + direction * b.Random.Range(28f, 46f);
                Vector2 half = new Vector2(b.Random.Range(12f, 20f), b.Random.Range(9f, 14f));
                Vector2 towardsRoad = TowardsNearestRoad(b, position);
                if (b.Field(position, half, YawFacing(towardsRoad), towardsRoad, out Vector2 gate))
                {
                    placed++;
                    LayFieldTrack(b, gate, position);
                    if (b.Random.Chance(0.5f)) b.World.Pending.Add(new PendingObject(StructureCatalog.Scarecrow.Id, position, b.Random.Float() * MathF.Tau, Vector3.One, null));
                }
            }

            // A farm keeps more about its yard than a village house does.
            WorldRandom yard = b.Random.Fork("farmyard");
            if (yard.Chance(0.55f)) b.Scatter(StructureCatalog.Coop, b.Centre + side * 9f, 6f, padding: 0.6f);
            if (yard.Chance(0.5f)) b.Scatter(StructureCatalog.Hives, b.Centre - side * 16f, 6f, padding: 0.8f);
            if (yard.Chance(0.6f)) b.Scatter(StructureCatalog.Trough, b.Centre - toRoad * 6f, 7f, padding: 0.4f);
            if (yard.Chance(0.45f)) b.Scatter(StructureCatalog.Sacks, barn + toRoad * 9f, 5f, padding: 0.3f);
            if (yard.Chance(0.35f)) b.Scatter(StructureCatalog.Barrow, b.Centre, 11f, padding: 0.3f);
            if (yard.Chance(0.4f)) b.Scatter(StructureCatalog.Laundry, housePosition - toRoad * 10f, 5f, padding: 0.4f);
            if (b.Style is BuildingStyle.Adobe or BuildingStyle.Tiled && yard.Chance(0.6f)) b.Scatter(StructureCatalog.Oven, b.Centre + side * 6f, 6f, padding: 0.5f);
            if (b.Style is BuildingStyle.Adobe or BuildingStyle.Tiled && yard.Chance(0.5f)) b.Scatter(StructureCatalog.Jars, housePosition + side * 10f, 4f, padding: 0.3f);
        }

        private static void BuildFishingHamlet(SiteBuilder b)
        {
            Vector2 toWater = b.FindWater(90f, out Vector2 water, out float distance) ? water : -b.Site.RoadDirection;
            Vector2 side = new Vector2(-toWater.Y, toWater.X);
            b.Paint(GroundPaint.Yard, Footprint.Circle(b.Centre, b.Site.Radius * 0.38f), 14f);

            int huts = b.Random.Int(3, 6);
            for (int i = 0; i < huts; i++)
            {
                float along = (i - (huts - 1) * 0.5f) * 13f + b.Random.Range(-1.5f, 1.5f);
                Vector2 spot = b.Centre + side * along - toWater * b.Random.Range(3f, 9f);
                if (b.Place(StructureCatalog.Hut, spot, YawFacing(toWater), padding: 0.8f) && b.Random.Chance(0.6f))
                {
                    b.Spawn(SpawnRole.Villager, spot + toWater * 6f);
                }
            }

            for (int i = 0; i < 3; i++) b.Scatter(StructureCatalog.DryingRack, b.Centre + toWater * 12f, 14f);
            b.Scatter(StructureCatalog.Barrels, b.Centre, 14f);
            b.Scatter(StructureCatalog.Crates, b.Centre, 14f);
            WorldRandom yard = b.Random.Fork("yard");
            if (yard.Chance(0.5f)) b.Scatter(StructureCatalog.Laundry, b.Centre - toWater * 10f, 8f, padding: 0.4f);
            if (yard.Chance(0.4f)) b.Scatter(StructureCatalog.Sacks, b.Centre, 12f, padding: 0.3f);

            if (distance < 90f)
            {
                // Boats are drawn up where the sand meets the water; the jetty starts at the same line and runs out.
                Vector2 shore = b.Centre + toWater * (distance - 3f);
                b.World.Pending.Add(new PendingObject(StructureCatalog.Jetty.Id, shore + toWater * 8f, YawFacing(toWater), Vector3.One, 0.9f));
                for (int i = -1; i <= 1; i += 2)
                {
                    Vector2 boat = shore + side * (i * b.Random.Range(8f, 14f)) - toWater * 4f;
                    b.Place(StructureCatalog.Boat, boat, YawFacing(side) + b.Random.Range(-0.3f, 0.3f), padding: 0.4f, maxSlope: 0.5f);
                }
            }
        }

        private static void BuildLumberCamp(SiteBuilder b)
        {
            b.Paint(GroundPaint.Yard, Footprint.Circle(b.Centre, b.Site.Radius * 0.45f), 12f);
            Vector2 toRoad = b.Site.RoadDirection;
            b.Place(StructureCatalog.Hut, b.Centre - toRoad * 8f, YawFacing(toRoad), padding: 0.8f);
            b.Place(StructureCatalog.Campfire, b.Centre + toRoad * 3f, 0f);
            b.Scatter(StructureCatalog.Cart, b.Centre, b.Site.Radius * 0.7f, attempts: 10);
            for (int i = 0; i < 5; i++) b.Scatter(StructureCatalog.LogStack, b.Centre, b.Site.Radius * 0.85f);
            b.Scatter(StructureCatalog.Woodpile, b.Centre, b.Site.Radius * 0.7f);
            WorldRandom yard = b.Random.Fork("yard");
            if (yard.Chance(0.55f)) b.Scatter(StructureCatalog.Barrow, b.Centre, b.Site.Radius * 0.6f, padding: 0.3f);
            if (yard.Chance(0.35f)) b.Scatter(StructureCatalog.Sacks, b.Centre - toRoad * 4f, 6f, padding: 0.3f);
            b.SpawnAround(SpawnRole.Villager, b.Centre, b.Site.Radius * 0.6f, 2);
        }

        private static void BuildShrine(SiteBuilder b)
        {
            Vector2 toRoad = b.Site.RoadDirection;
            Vector2 side = new Vector2(-toRoad.Y, toRoad.X);
            b.Paint(GroundPaint.Plaza, Footprint.Circle(b.Centre, b.Site.Radius * 0.34f), 7f);

            b.PlaceNear(StructureCatalog.Altar, b.Centre, YawFacing(toRoad), b.Site.Radius * EssentialReach, out Vector2 altar, padding: 0.5f);
            b.Spawn(SpawnRole.Altar, altar + toRoad * 3f);
            b.Place(StructureCatalog.GateArch, b.Centre + toRoad * (b.Site.Radius * 0.72f), YawFacing(toRoad), padding: 0.2f);
            for (int i = -1; i <= 1; i += 2)
            {
                b.Place(StructureCatalog.StoneLantern, altar + side * (i * 4.6f) + toRoad * 2.2f, 0f, padding: 0.2f);
            }

            // The Reich put it out, and keeps it out.
            b.SpawnAround(SpawnRole.Raider, b.Centre + toRoad * 5f, b.Site.Radius * 0.7f, b.Random.Int(3, 4));
            SpawnChest(b, altar - toRoad * 3.2f, 3f);
        }

        private static void BuildStandingStones(SiteBuilder b)
        {
            int stones = b.Random.Int(5, 9);
            float radius = b.Site.Radius * 0.72f;
            float start = b.Random.Float() * MathF.Tau;
            for (int i = 0; i < stones; i++)
            {
                float angle = start + i * (MathF.Tau / stones);
                b.Place(StructureCatalog.Monolith, b.Centre + Direction(angle) * radius, b.Random.Float() * MathF.Tau, b.Random.Range(0.85f, 1.3f), padding: 0.2f);
            }
            b.PlaceNear(StructureCatalog.Altar, b.Centre, b.Random.Float() * MathF.Tau, b.Site.Radius * EssentialReach, out Vector2 altar, padding: 0.3f);
            b.Spawn(SpawnRole.Altar, altar + b.Site.RoadDirection * 3f);

            // The old stones belong to whatever power the land has, and its cult keeps them.
            b.SpawnAround(SpawnRole.Cultist, b.Centre, radius, b.Random.Int(3, 4));
            SpawnChest(b, b.Centre - b.Site.RoadDirection * 3f, 3f);
        }

        private static void BuildWatchtower(SiteBuilder b)
        {
            Vector2 toRoad = b.Site.RoadDirection;
            b.Place(StructureCatalog.WatchTower, b.Centre, YawFacing(toRoad), padding: 0.5f);
            b.Place(StructureCatalog.Campfire, b.Centre + toRoad * 8f, 0f, padding: 0.3f);
            b.Scatter(StructureCatalog.Barrels, b.Centre, b.Site.Radius);
            b.SpawnAround(SpawnRole.Guard, b.Centre + toRoad * 7f, 5f, b.Random.Int(2, 3));
            SpawnChest(b, b.Centre - toRoad * 7f, 3f);
        }

        private static void BuildRuins(SiteBuilder b)
        {
            WorldSite site = b.Site;
            float angle = b.Random.Float() * MathF.PI;
            int rooms = b.Random.Int(2, 4);
            for (int room = 0; room < rooms; room++)
            {
                Vector2 middle = b.Centre + Direction(b.Random.Float() * MathF.Tau) * b.Random.Range(0f, site.Radius * 0.5f);
                Vector2 half = new Vector2(b.Random.Range(6f, 12f), b.Random.Range(5f, 9f));
                Footprint outline = Footprint.Box(middle, half, angle);
                // Walls of a square room, with the gaps of what has fallen.
                int doorway = b.Random.Int(4);
                for (int edge = 0; edge < 4; edge++)
                {
                    if (edge == doorway) continue;
                    bool alongX = edge % 2 == 0;
                    float length = alongX ? half.X : half.Y;
                    float across = (edge < 2 ? -1f : 1f) * (alongX ? half.Y : half.X);
                    for (float t = -length + 4.5f; t <= length - 4.5f + 0.01f; t += 9f)
                    {
                        if (b.Random.Chance(0.3f)) continue;
                        Vector2 local = alongX ? new Vector2(t, across) : new Vector2(across, t);
                        b.Place(StructureCatalog.RuinWall, outline.ToWorld(local), alongX ? angle : angle + MathF.PI * 0.5f, padding: 0f);
                    }
                }
            }

            int columns = b.Random.Int(4, 8);
            for (int i = 0; i < columns; i++) b.Scatter(StructureCatalog.RuinColumn, b.Centre, site.Radius * 0.85f);
            for (int i = 0; i < 6; i++) b.Scatter(StructureCatalog.Rubble, b.Centre, site.Radius * 0.9f);

            b.SpawnAround(b.Random.Chance(0.5f) ? SpawnRole.Undead : SpawnRole.Raider, b.Centre, site.Radius * 0.75f, b.Random.Int(3, 5));
            SpawnChest(b, b.Centre, site.Radius * 0.4f);
        }

        private static void BuildBanditCamp(SiteBuilder b)
        {
            WorldSite site = b.Site;
            Vector2 toRoad = site.RoadDirection;
            b.Paint(GroundPaint.Yard, Footprint.Circle(b.Centre, site.Radius * 0.62f), 12f);
            b.Ring(FenceUse.Pen, site.Radius, b.Random.Int(9, 13), toRoad, 8f, gateOnEdge: true);

            b.Place(StructureCatalog.Campfire, b.Centre, 0f);
            Vector2 side = new Vector2(-toRoad.Y, toRoad.X);
            b.Place(StructureCatalog.GuardTower, b.Centre + toRoad * (site.Radius * 0.62f) + side * 9f, YawFacing(toRoad), padding: 0.5f);

            int tents = b.Random.Int(3, 5);
            for (int i = 0; i < tents; i++)
            {
                float angle = i * (MathF.Tau / tents) + b.Random.Range(-0.3f, 0.3f);
                Vector2 spot = b.Centre + Direction(angle) * site.Radius * 0.58f;
                b.Place(i == 0 ? StructureCatalog.LargeTent : StructureCatalog.Tent, spot, YawFacing(b.Centre - spot), padding: 0.6f);
            }
            for (int i = 0; i < 4; i++)
            {
                b.Scatter(StructureCatalog.Crates, b.Centre, site.Radius * 0.75f);
                b.Scatter(StructureCatalog.Barrels, b.Centre, site.Radius * 0.75f);
            }

            b.SpawnAround(SpawnRole.Raider, b.Centre, site.Radius * 0.6f, b.Random.Int(3, 6));
            b.SpawnAround(SpawnRole.Guard, b.Centre + toRoad * site.Radius * 0.8f, 5f, 2);
            SpawnChest(b, b.Centre - toRoad * site.Radius * 0.42f, 3f);
        }

        private const float ChurchForecourt = 2f;
        private const float AisleHalfWidth = 3.6f;
        private const float DeadTreeScale = 0.6f;
        private const float GraveColumns = 5.6f;    // Metres apart, wide enough to walk between: a character is broad, and
        private const float GraveRows = 7f;         // the dead can only rise where someone could reach them.
        private const float WalkAround = 3.2f;      // Clear ground kept along the church and inside the wall.

        private static void BuildGraveyard(SiteBuilder b)
        {
            WorldSite site = b.Site;
            Vector2 toRoad = site.RoadDirection;
            b.Paint(GroundPaint.Yard, Footprint.Circle(b.Centre, site.Radius * 0.95f), 6f);

            // The church goes first: everything else makes way for it, and whatever the land allows, it is what the place is for.
            RaiseChurch(b, toRoad, out Vector2 church, out Vector2 front);
            b.Ring(FenceUse.Churchyard, site.Radius, 8, toRoad, 6f, gateOnEdge: true);

            // The path to the door, between tall candles, and a vigil kept either side of it.
            Vector2 across = new Vector2(-front.Y, front.X);
            Vector2 door = church + front * (StructureCatalog.Church.HalfExtents.Y + 0.6f);
            float aisle = MathF.Max(site.Radius - Vector2.Dot(door - b.Centre, front), 4f);
            Vector2 aisleEnd = door + front * aisle;
            b.Paint(GroundPaint.Plaza, Footprint.Segment(door, aisleEnd, AisleHalfWidth - 1f), 2f);
            foreach (float side in new[] { -1f, 1f })
            {
                b.Place(StructureCatalog.VigilCandles, door + front * 1.6f + across * side * 4.4f, b.Random.Float() * MathF.Tau, padding: 0f);
                for (float along = 6f; along < aisle - 1f; along += 6f)
                {
                    b.Place(StructureCatalog.CandleStand, door + front * along + across * side * AisleHalfWidth, 0f, padding: 0.2f);
                }
            }

            float angle = YawFacing(front);
            WorldRandom vigils = b.Random.Fork("candles");
            Footprint rows = Footprint.Box(b.Centre, Vector2.One, angle);
            for (float z = -site.Radius * 0.84f; z <= site.Radius * 0.84f; z += GraveRows)
            {
                for (float x = -site.Radius * 0.84f; x <= site.Radius * 0.84f; x += GraveColumns)
                {
                    if (b.Random.Chance(0.18f)) continue;
                    Vector2 local = new Vector2(x + b.Random.Range(-0.3f, 0.3f), z + b.Random.Range(-0.3f, 0.3f));
                    Vector2 spot = rows.ToWorld(local);
                    if (DistanceToSegment(spot, door, aisleEnd) < AisleHalfWidth + 1.2f) continue;
                    if (b.World.Placement.Overlaps(StructureCatalog.Grave.FootprintAt(spot, angle), PlacementLayer.Structure | PlacementLayer.Fence, WalkAround)) continue;

                    float yaw = angle + b.Random.Range(-0.12f, 0.12f);
                    if (!b.Place(StructureCatalog.Grave, spot, yaw, padding: 0.3f)) continue;

                    // Someone still comes: candles at the foot of the grave, on the side towards the path.
                    if (vigils.Chance(0.6f))
                    {
                        Vector2 foot = spot + new Vector2(MathF.Sin(yaw), MathF.Cos(yaw)) * 2.1f;
                        b.Place(StructureCatalog.Candles, foot, vigils.Float() * MathF.Tau, vigils.Range(0.85f, 1.2f), padding: 0f);
                    }
                }
            }

            for (int i = 0; i < 2; i++) b.Scatter(StructureCatalog.Tomb, b.Centre, site.Radius * 0.85f, attempts: 10, padding: 0.8f);
            for (int i = 0; i < 5; i++)
            {
                Vector2 spot = b.Centre + Direction(b.Random.Float() * MathF.Tau) * site.Radius * b.Random.Range(0.55f, 0.95f);
                b.Place(StructureCatalog.DeadTree, spot, b.Random.Float() * MathF.Tau, DeadTreeScale * b.Random.Range(0.8f, 1.25f), padding: 0.6f);
            }

            b.SpawnAround(SpawnRole.Undead, b.Centre, site.Radius * 0.75f, b.Random.Int(4, 7));
            b.Spawn(SpawnRole.Chest, door + front * 3.4f);
        }

        // As near the middle as the land and any passing road allow, with the door to the road. Beside a road that runs on through,
        // it stands along the road instead, which takes less of the ground either side.
        private static void RaiseChurch(SiteBuilder b, Vector2 toRoad, out Vector2 position, out Vector2 front)
        {
            Vector2 side = new Vector2(-toRoad.Y, toRoad.X);
            Vector2[] fronts = { toRoad, side, -side, -toRoad };
            var arms = new Footprint[2];
            float first = MathF.Atan2(-toRoad.Y, -toRoad.X);

            for (float distance = 0f; distance <= b.Site.Radius * 0.7f; distance += 2f)
            {
                int directions = distance > 0f ? 8 : 1;
                foreach (Vector2 facing in fronts)
                {
                    for (int i = 0; i < directions; i++)
                    {
                        Vector2 spot = b.Centre + Direction(first + i * (MathF.Tau / 8f)) * distance;
                        arms[0] = StructureCatalog.ChurchArm(spot, facing, -1f);
                        arms[1] = StructureCatalog.ChurchArm(spot, facing, 1f);
                        if (!b.PlaceWithWings(StructureCatalog.Church, spot, YawFacing(facing), arms, padding: 0.6f, maxSlope: 0.6f)) continue;
                        position = spot;
                        front = facing;
                        return;
                    }
                }
            }

            // WorldValidator fails a churchyard with no church, so a world this happens in doesn't go unnoticed.
            position = b.Centre;
            front = toRoad;
        }

        // A road that goes no further than here stops at the church door, not at the middle of the ground it would stand on.
        private static void EndRoadsShortOf(WorldData world, Vector2 centre, float reach)
        {
            foreach (WorldRoad road in world.Roads)
            {
                List<Vector3> points = road.Points;
                while (points.Count > 2 && (points[^1].Xz - centre).Length < reach) points.RemoveAt(points.Count - 1);
                while (points.Count > 2 && (points[0].Xz - centre).Length < reach) points.RemoveAt(0);
            }
        }

        private static float DistanceToSegment(Vector2 point, Vector2 from, Vector2 to)
        {
            Vector2 span = to - from;
            float along = Math.Clamp(Vector2.Dot(point - from, span) / MathF.Max(span.LengthSquared, 1e-4f), 0f, 1f);
            return (point - (from + span * along)).Length;
        }

        private const int TowerRetreats = 4;
        private const float TowerRetreatStep = 1.5f;
        private const float TowerClearance = 0.5f;      // Metres of ground between a tower's foot and where its walls are tried from, which is more than the padding a wall keeps.
        private const float WallIntoTower = 2.6f;       // Metres a wall is drawn on into a tower, and the end it stops at hidden in it.
        private const float WallIntoGatehouse = 1.8f;

        private static void BuildKeep(SiteBuilder b)
        {
            WorldSite site = b.Site;
            Vector2 toRoad = site.RoadDirection;
            Vector2 side = new Vector2(-toRoad.Y, toRoad.X);
            b.Paint(GroundPaint.Plaza, Footprint.Circle(b.Centre, site.Radius * 0.9f), 8f);

            float half = site.Radius * 0.86f;
            Vector2[] corners =
            {
                b.Centre + toRoad * half + side * half,
                b.Centre - toRoad * half + side * half,
                b.Centre - toRoad * half - side * half,
                b.Centre + toRoad * half - side * half,
            };
            // A tower too close to a passing road gives ground toward the keep, and the walls meet it where it ends up.
            for (int i = 0; i < corners.Length; i++)
            {
                Vector2 inward = (b.Centre - corners[i]).Normalized();
                for (int step = 0; step <= TowerRetreats; step++)
                {
                    Vector2 spot = corners[i] + inward * (step * TowerRetreatStep);
                    if (!b.Place(StructureCatalog.StoneTower, spot, 0f, padding: 0f, maxSlope: 0.6f)) continue;
                    corners[i] = spot;
                    break;
                }
            }

            // Walls start where a tower's foot ends, as they can't stand in it, and are drawn on into the towers and the gatehouse so that they meet them.
            float clear = StructureCatalog.StoneTower.HalfExtents.X + TowerClearance;
            for (int i = 0; i < 4; i++)
            {
                Vector2 from = corners[i];
                Vector2 to = corners[(i + 1) % 4];
                Vector2 along = (to - from).Normalized();
                Vector2 start = from + along * clear;
                Vector2 end = to - along * clear;
                if (i == 3)
                {
                    // The wall facing the road carries the gatehouse.
                    Vector2 middle = (start + end) * 0.5f;
                    b.Wall(FenceUse.Curtain, start, middle - along * 7f, b.Centre, WallIntoTower, WallIntoGatehouse);
                    b.Wall(FenceUse.Curtain, middle + along * 7f, end, b.Centre, WallIntoGatehouse, WallIntoTower);
                    b.Place(StructureCatalog.Gatehouse, middle, YawFacing(toRoad), padding: -1f, maxSlope: 0.6f);
                }
                else
                {
                    b.Wall(FenceUse.Curtain, start, end, b.Centre, WallIntoTower, WallIntoTower);
                }
            }

            // The keep's heart is the arena its tyrant is kept in (ARENAS.md), its tyrant and its hoard on the floor, and the keep's people camp round it inside the walls.
            ArenaPlan arena = ArenaBuilder.Keep(b, b.Centre);
            float camp = MathF.Max((arena.Rim + 3.5f + half - 3f) * 0.5f, arena.Rim + 3.5f);
            float campWidth = MathF.Max(half - 3f - (arena.Rim + 3.5f), 1f) * 0.5f;
            b.Scatter(StructureCatalog.Tent, b.Centre + side * camp, MathF.Min(campWidth, 8f));
            b.Scatter(StructureCatalog.Tent, b.Centre - side * camp, MathF.Min(campWidth, 8f));
            for (int i = 0; i < 6; i++)
            {
                float angle = b.Random.Float() * MathF.Tau;
                b.Scatter(StructureCatalog.Crates, b.Centre + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * camp, MathF.Min(campWidth, 5f));
            }

            b.SpawnAround(SpawnRole.Guard, b.Centre + toRoad * (half - 5f), 6f, 3);
            int raiders = b.Random.Int(6, 9);
            for (int i = 0; i < raiders; i++)
            {
                float angle = (i + b.Random.Range(-0.3f, 0.3f)) * MathF.Tau / raiders;
                b.Spawn(SpawnRole.Raider, b.Centre + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (camp + b.Random.Range(-campWidth, campWidth) * 0.6f));
            }
        }

        // Things along the road that promise something further on, spaced to be met every half minute or so at a run.
        private static void PlaceWayside(WorldData world, WorldRandom random)
        {
            var builder = new SiteBuilder(world, world.Sites[0], random);

            foreach (WorldRoad road in world.Roads)
            {
                if (road.Class == RoadClass.Footpath && road.Points.Count < 25) continue;

                float nextMark = random.Range(30f, WaysideSpacing);
                float walked = 0f;
                for (int i = 1; i < road.Points.Count - 1; i++)
                {
                    walked += (road.Points[i] - road.Points[i - 1]).Xz.Length;
                    if (walked < nextMark) continue;

                    Vector2 point = road.Points[i].Xz;
                    if (NearSite(world, point, 28f)) continue;

                    Vector2 tangent = (road.Points[i + 1].Xz - road.Points[i - 1].Xz).Normalized();
                    Vector2 side = new Vector2(-tangent.Y, tangent.X) * (random.Chance(0.5f) ? 1f : -1f);
                    // A marker that doesn't fit here is tried again a few metres on.
                    if (!PlaceMarker(builder, road, point, side, tangent, random)) continue;
                    nextMark = walked + random.Range(WaysideSpacing * 0.7f, WaysideSpacing * 1.35f);
                }
            }
        }

        private const float LandmarkCell = 30f;         // Metres: the land is tried for a landmark this often.
        private const float SpawnClearance = 3.2f;      // Metres anything added after a site is laid out keeps from where anyone of it stands.
        private const float MillReach = 34f;            // Metres beyond a village's edge it looks for high ground for a windmill.

        // What's added to a land once its places are laid out, on a stream of its own so that none of them moves: what stands out of the land itself, the windmill on the
        // high ground by a village, the Ossuary's digs, the Reich's pens dressed as the pens they are, and its banners at the gate of every keep (LORE.md). Each keeps
        // clear of where anyone of a place stands, so that it can never push someone off their spot or shut a chest in.
        private static void PlaceLandmarks(WorldData world, WorldRandom random)
        {
            if (world.Plan.Archetype.Peaceful) return;

            // Termite mounds and old bones, as the biome has them, on a jittered grid so that they're spread out and not in rows.
            WorldRandom land = random.Fork("land");
            var builder = new SiteBuilder(world, world.Sites[0], land);
            for (float z = LandmarkCell * 0.5f; z < world.Size; z += LandmarkCell)
            {
                for (float x = LandmarkCell * 0.5f; x < world.Size; x += LandmarkCell)
                {
                    WorldRandom cell = land.Fork((int)(z / LandmarkCell) * 4096 + (int)(x / LandmarkCell));
                    Vector2 spot = new Vector2(x, z) + new Vector2(cell.Range(-0.4f, 0.4f), cell.Range(-0.4f, 0.4f)) * LandmarkCell;
                    if (!world.InBounds(spot)) continue;
                    BiomeDefinition biome = world.BiomeAt(spot);
                    if (biome.Landmark == null || !cell.Chance(biome.LandmarksPerKm2 * LandmarkCell * LandmarkCell / 1_000_000f)) continue;
                    if (NearSite(world, spot, 12f)) continue;
                    float yaw = cell.Float() * MathF.Tau, scale = cell.Range(0.8f, 1.2f);
                    if (biome.Landmark == StructureCatalog.Fossil) PlaceBared(builder, biome.Landmark, spot, yaw, scale, 1f, 0.4f);
                    else builder.Place(biome.Landmark, spot, yaw, scale, padding: 1f, maxSlope: 0.4f);
                }
            }

            foreach (WorldSite site in world.Sites)
            {
                WorldRandom own = random.Fork(site.Id);
                var b = new SiteBuilder(world, site, own);
                switch (site.Kind)
                {
                    case SiteKind.Village when b.Style is not (BuildingStyle.Adobe or BuildingStyle.Stilt or BuildingStyle.Eastern) && own.Chance(0.6f):
                        RaiseWindmill(b);
                        break;
                    case SiteKind.Graveyard when own.Chance(0.8f):
                    case SiteKind.Ruins when own.Chance(0.45f):
                    case SiteKind.StandingStones when own.Chance(0.4f):
                        OpenDig(b);
                        break;
                    case SiteKind.BanditCamp:
                        DressPen(b);
                        break;
                    case SiteKind.Keep:
                        FlyBanners(b, b.Centre + site.RoadDirection * (site.Radius * 0.86f + 3.5f), site.RoadDirection, 9f);
                        break;
                }
            }
        }

        // What lies low in the ground shows only where the ground is worn bare round it, as grass taller than it grows through it otherwise: an old beast's bones, a dig.
        private static bool PlaceBared(SiteBuilder b, StructureDefinition definition, Vector2 at, float yaw, float scale, float padding, float maxSlope)
        {
            if (!b.Place(definition, at, yaw, scale, padding, maxSlope)) return false;
            b.Paint(GroundPaint.Yard, definition.FootprintAt(at, yaw, 1.5f), 3f);
            return true;
        }

        // Whether something with this footprint at this spot keeps clear of everyone a site has standing.
        private static bool ClearOfSpawns(WorldSite site, Vector2 spot, float reach)
        {
            foreach (SiteSpawn spawn in site.Spawns)
            {
                if ((spawn.Position - spot).Length < reach + SpawnClearance) return false;
            }
            return true;
        }

        private static bool PlaceClear(SiteBuilder b, StructureDefinition definition, Vector2 spot, float yaw, float padding = 0.6f)
        {
            if (!ClearOfSpawns(b.Site, spot, definition.HalfExtents.Length)) return false;
            return b.Place(definition, spot, yaw, padding: padding);
        }

        // A post mill on the highest ground just outside a village, kept clear of trees all round as its sails turn over the ground in front of it. A land's mills all face
        // much the same way, into its wind, and that way is south, towards the game's camera, as sails seen edge on from up here are only lines.
        private static void RaiseWindmill(SiteBuilder b)
        {
            WorldSite site = b.Site;
            Vector2 best = default;
            float highest = float.NegativeInfinity;
            for (int i = 0; i < 16; i++)
            {
                float distance = site.Radius + b.Random.Range(14f, MillReach);
                Vector2 spot = site.Position + Direction(i * MathF.Tau / 16f + b.Random.Range(-0.1f, 0.1f)) * distance;
                if (!b.World.InBounds(spot) || b.World.Reachable.Sample(spot) < 0.5f) continue;
                if (b.World.Sites.Any(other => other != site && (other.Position - spot).Length < other.Radius + 12f)) continue;
                float height = b.World.Elevation.Sample(spot);
                if (height <= highest || !b.Fits(StructureCatalog.Windmill.FootprintAt(spot, 0f), 0.3f, 2.5f)) continue;
                highest = height;
                best = spot;
            }
            if (float.IsNegativeInfinity(highest)) return;
            float yaw = MathF.PI + b.Random.Range(-0.35f, 0.35f);
            Vector2 facing = new Vector2(MathF.Sin(yaw), MathF.Cos(yaw));
            if (b.Place(StructureCatalog.Windmill, best, yaw, padding: 2.5f, maxSlope: 0.3f))
            {
                b.World.Placement.Add(Footprint.Circle(best + facing * 2f, 10.5f), PlacementLayer.Keepout);
            }
        }

        // A dig at the edge of a place where the Ossuary works, with what it has taken out laid in rows beside it.
        private static void OpenDig(SiteBuilder b)
        {
            WorldSite site = b.Site;
            for (int attempt = 0; attempt < 14; attempt++)
            {
                Vector2 spot = site.Position + Direction(b.Random.Float() * MathF.Tau) * site.Radius * b.Random.Range(0.45f, 0.8f);
                float yaw = b.Random.Float() * MathF.Tau;
                if (!PlaceClear(b, StructureCatalog.Dig, spot, yaw, padding: 1f)) continue;
                b.Paint(GroundPaint.Yard, StructureCatalog.Dig.FootprintAt(spot, yaw, 1.5f), 3f);
                Vector2 across = Direction(-yaw);
                Vector2 beside = spot + across * (StructureCatalog.Dig.HalfExtents.Y + StructureCatalog.BoneRows.HalfExtents.Y + 1.6f);
                if (!PlaceClear(b, StructureCatalog.BoneRows, beside, yaw, padding: 0.4f)) PlaceClear(b, StructureCatalog.BoneRows, spot - across * (StructureCatalog.Dig.HalfExtents.Y + StructureCatalog.BoneRows.HalfExtents.Y + 1.6f), yaw, padding: 0.4f);
                return;
            }
        }

        // A brood's pen (LORE.md): cages round the inside of its fence facing the fire, troughs and a rack of meat between them, and its banners at the gate.
        private static void DressPen(SiteBuilder b)
        {
            WorldSite site = b.Site;
            Vector2 toRoad = site.RoadDirection;
            float start = MathF.Atan2(toRoad.Y, toRoad.X);
            int cages = b.Random.Int(2, 3);
            for (int i = 0; i < cages; i++)
            {
                // Round the back of the pen, away from the gate.
                float angle = start + MathF.PI + (i - (cages - 1) * 0.5f) * 0.95f;
                for (int attempt = 0; attempt < 4; attempt++)
                {
                    Vector2 spot = site.Position + Direction(angle + b.Random.Range(-0.15f, 0.15f)) * site.Radius * b.Random.Range(0.55f, 0.72f);
                    if (PlaceClear(b, StructureCatalog.Cage, spot, YawFacing(site.Position - spot))) break;
                }
            }
            for (int i = 0; i < 2; i++)
            {
                Vector2 spot = site.Position + Direction(start + MathF.PI * (i == 0 ? 0.5f : 1.5f) + b.Random.Range(-0.3f, 0.3f)) * site.Radius * b.Random.Range(0.4f, 0.6f);
                PlaceClear(b, StructureCatalog.FeedTrough, spot, YawFacing(site.Position - spot), padding: 0.4f);
            }
            Vector2 rack = site.Position + Direction(start + MathF.PI * 0.75f + b.Random.Range(-0.3f, 0.3f)) * site.Radius * 0.5f;
            PlaceClear(b, StructureCatalog.MeatRack, rack, YawFacing(site.Position - rack), padding: 0.4f);
            FlyBanners(b, site.Position + toRoad * (site.Radius + 2.5f), toRoad, 5f);
        }

        // A pair of the Reich's banners either side of a way in, outside it, facing whoever comes up the road: both or neither, as one alone reads as one lost.
        private static void FlyBanners(SiteBuilder b, Vector2 gate, Vector2 outward, float apart)
        {
            Vector2 across = new Vector2(-outward.Y, outward.X);
            float yaw = YawFacing(outward);
            // Where the road or the lie of the land doesn't allow them by the gate, a little further out or further apart.
            foreach ((float further, float wider) in new[] { (0f, 1f), (2.5f, 1f), (0f, 1.3f), (2.5f, 1.3f), (5f, 0.8f), (5f, 1.5f) })
            {
                Vector2 left = gate + outward * further - across * (apart * wider), right = gate + outward * further + across * (apart * wider);
                bool fits = true;
                foreach (Vector2 spot in new[] { left, right })
                {
                    fits &= ClearOfSpawns(b.Site, spot, StructureCatalog.Banner.HalfExtents.X) && b.Fits(StructureCatalog.Banner.FootprintAt(spot, yaw), 0.42f, 0.3f);
                }
                if (!fits) continue;
                b.Place(StructureCatalog.Banner, left, yaw, padding: 0.3f);
                b.Place(StructureCatalog.Banner, right, yaw, padding: 0.3f);
                return;
            }
        }

        private static bool NearSite(WorldData world, Vector2 point, float margin)
        {
            foreach (WorldSite site in world.Sites)
            {
                float reach = site.Radius + margin;
                if ((site.Position - point).LengthSquared < reach * reach) return true;
            }
            return false;
        }

        private static bool PlaceMarker(SiteBuilder b, WorldRoad road, Vector2 point, Vector2 side, Vector2 tangent, WorldRandom random)
        {
            Vector2 verge = point + side * (road.HalfWidth + 2f);
            // The land's own little shrines and the old milestones go anywhere a road does; a hunter's stand where it runs through woods, and the bones of a great
            // beast where dry ground wears away beside it.
            BiomeDefinition biome = b.World.BiomeAt(point);
            bool wooded = biome.ForestCover >= 0.35f;
            bool dry = biome.Moisture < 0.35f && biome.Temperature > 0.55f;
            float[] weights = { 3f, 2.5f, 2f, 1.5f, 1f, 2f, 1.5f, wooded ? 1.4f : 0f, dry ? 1.2f : 0f };
            switch (random.PickWeighted(weights))
            {
                case 5:
                    return b.Place(StructureCatalog.WaysideShrine, verge, SiteBuilder.YawFacing(-side), padding: 0.2f);
                case 6:
                    return b.Place(StructureCatalog.Milestone, verge, SiteBuilder.YawFacing(-side), padding: 0.2f);
                case 7:
                    return b.Place(StructureCatalog.HuntingStand, verge + side * 5f, SiteBuilder.YawFacing(-side) + random.Range(-0.4f, 0.4f), padding: 0.5f);
                case 8:
                    // Its long way along the road, as a slope beside one is cut.
                    return PlaceBared(b, StructureCatalog.Fossil, verge + side * 5.5f, SiteBuilder.YawFacing(side), 1f, 0.8f, 0.38f);
                case 0:
                    return b.Place(StructureCatalog.StoneLantern, verge, SiteBuilder.YawFacing(-side), padding: 0.2f);
                case 1:
                    return b.Place(StructureCatalog.Cairn, verge, random.Float() * MathF.Tau, padding: 0.2f);
                case 2:
                    return b.Place(StructureCatalog.Signpost, verge, SiteBuilder.YawFacing(tangent), padding: 0.2f);
                case 3:
                    if (!b.Place(StructureCatalog.Cart, verge + side * 1.6f, SiteBuilder.YawFacing(tangent) + random.Range(-0.4f, 0.4f), padding: 0.3f)) return false;
                    b.Place(StructureCatalog.Barrels, verge + side * 3f + tangent * 4.4f, 0f, padding: 0.1f);
                    return true;
                default:
                    return b.Place(StructureCatalog.Bench, verge, SiteBuilder.YawFacing(-side), padding: 0.2f);
            }
        }
    }
}

using Dino_Engine.Modelling.Procedural.Fence;
using Dino_Engine.Modelling.Procedural.Flora;
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
        internal const float KeepRoadEnd = 0.66f;        // Of a keep's radius, where a road that ends there stops: inside its gatehouse, short of the yard in the middle of it.
        private const float EssentialReach = 1.5f;      // Share of a site's radius something it can't do without may stray to find ground: a neighbour's field can cover all of a small site.

        public string Description => "Raising the villages";
        public float Cost => 1f;

        public void Run(WorldData world)
        {
            world.Placement = new PlacementIndex(world.Size);
            // The Haven builds as its halls do, in oak, lime and thatch.
            world.Style = world.Plan.Archetype.Peaceful ? BuildingStyle.Timber : StyleOf(world);
            WorldRandom random = world.Random.Fork("settlements");
            foreach (WorldSite site in world.Sites)
            {
                // A churchyard's shape is worked out from the same fork its builder will have, so that a road ending there stops at its wall (CHURCHYARD.md).
                if (site.Kind == SiteKind.Graveyard) ChurchyardBuilder.EndRoadsAtWall(world, site.Yard = ChurchyardBuilder.PlanOf(world, site, random.Fork(site.Id)));
                // A shrine's road stops at its gateway, a ring of stones' at the end of its avenue, a watchtower's at the foot of its yard and a pen's at its gate (PLACES.md), as
                // each is laid out by a builder of its own that puts its way in where the road comes.
                if (site.Kind == SiteKind.Shrine) EndRoadsShortOf(world, site.Position, site.Radius * 0.85f);
                if (site.Kind is SiteKind.StandingStones or SiteKind.Watchtower) EndRoadsShortOf(world, site.Position, site.Radius * 0.88f);
                if (site.Kind == SiteKind.BanditCamp) EndRoadsShortOf(world, site.Position, site.Radius * 0.86f);
                if (site.Kind == SiteKind.Waygate) EndRoadsShortOf(world, site.Position, StructureCatalog.Waygate.HalfExtents.Y + AltarForecourt);
                if (site.Kind == SiteKind.Keep) EndRoadsShortOf(world, site.Position, site.Radius * KeepRoadEnd);
                // A walled place with a story of its own (PLACES.md) is come into by its gate, which its builder puts where the road comes: the road stops there, and at a quarry's mouth.
                // A dig's road stops at its camp, short of the pit, a road to ruins at their gateway, short of the hall, one to a snow castle short of its gate, and one to a
                // nesting ground at the edge of its clearing, as nobody goes further who means to come back.
                if (WalledStory(site.Kind) || site.Kind == SiteKind.Quarry) EndRoadsShortOf(world, site.Position, site.Radius * 0.92f);
                if (site.Kind is SiteKind.Excavation or SiteKind.Ruins or SiteKind.SnowCastle or SiteKind.Nest) EndRoadsShortOf(world, site.Position, site.Radius * 0.85f);
            }
            foreach (WorldRoad road in world.Roads) ReserveRoad(world, road);

            // What a land can't be done without goes down first, so that nothing a neighbour lays out, a field or the track to it, takes its ground. Each site draws on a
            // fork of its own, so the order changes nothing it draws.
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
                    case SiteKind.FishingHamlet: Places.FishingBuilder.Build(builder); break;
                    case SiteKind.LumberCamp: Places.LoggingBuilder.Build(builder); break;
                    case SiteKind.Shrine: Places.ShrineBuilder.Build(builder); break;
                    case SiteKind.StandingStones: Places.StonesBuilder.Build(builder); break;
                    case SiteKind.Watchtower: Places.TowerBuilder.Build(builder); break;
                    case SiteKind.Ruins: Places.RuinsBuilder.Build(builder); break;
                    case SiteKind.BanditCamp: Places.PenBuilder.Build(builder); break;
                    case SiteKind.Graveyard: ChurchyardBuilder.Build(builder); break;
                    case SiteKind.Keep: BuildKeep(builder); break;
                    case SiteKind.Waygate: BuildWaygate(builder); break;
                    case SiteKind.Haven: HavenBuilder.Build(builder); break;
                    case SiteKind.Arena: ArenaBuilder.Build(builder); break;
                    // Places with a story of their own, each laid out by a builder of its own (PLACES.md).
                    case SiteKind.Shipwreck: Places.WreckBuilder.Build(builder); break;
                    case SiteKind.Prison: Places.PrisonBuilder.Build(builder); break;
                    case SiteKind.Laboratory: Places.LaboratoryBuilder.Build(builder); break;
                    case SiteKind.Excavation: Places.ExcavationBuilder.Build(builder); break;
                    case SiteKind.Nest: Places.NestBuilder.Build(builder); break;
                    case SiteKind.Quarry: Places.QuarryBuilder.Build(builder); break;
                    case SiteKind.Farm: Places.FarmBuilder.Build(builder); break;
                    case SiteKind.Tradepost: Places.TradepostBuilder.Build(builder); break;
                    case SiteKind.SnowCastle: Places.SnowCastleBuilder.Build(builder); break;
                }
            }

            PlaceWayside(world, random.Fork("wayside"));
            PlaceLandmarks(world, random.Fork("landmarks"));
        }

        /// <summary>The places with a story of their own that have a wall or a fence round them with a gate in it, which a road stops at.</summary>
        internal static bool WalledStory(SiteKind kind) => kind is SiteKind.Prison or SiteKind.Laboratory or SiteKind.Farm or SiteKind.Tradepost;

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
            SiteKind.Ruins => 0.8f,
            SiteKind.Graveyard => 1.2f,     // Its wall's corners reach past its radius, and it plants its own trees inside it.
            SiteKind.Nest => 0.72f,         // Deep in the woods, which close in round it.
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
            // Nobody trades at the landing: a land's people are found in its villages, and the Haven has its own merchants.
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
            bool nordic = b.Style == BuildingStyle.Nordic;
            // A midsummer pole is dressed in fresh leaves, which nothing under snow has.
            if (nordic && !Snowy(b)) PlaceMaypole(b, isVillage);
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
            // The north fences the lanes out past its fields and lines them with birches; a village behind a stockade keeps only the birches, as its gates are its own.
            if (nordic) LineLanes(b, fences: !fortified);
            if (nordic && !Snowy(b)) HangHayAbout(b);

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

        private const float MaypoleReach = 4.4f;        // Metres a midsummer pole's arms and rings reach from its foot, and a little more.

        // The north raises a pole on its green for midsummer, dressed in leaves and flowers, and leaves it standing the year round. Its arms and rings reach well past
        // its foot, so the ground under them is kept from anything built.
        private static void PlaceMaypole(SiteBuilder b, bool isVillage)
        {
            WorldRandom random = b.Random.Fork("maypole");
            for (int attempt = 0; attempt < 48; attempt++)
            {
                // Near the middle first, and further out the more tries it takes. Its arms are well over anyone's head, so only what's built has to keep from under them.
                float reach = MathHelper.Lerp(isVillage ? 12f : 8f, isVillage ? 26f : 18f, attempt / 47f);
                Vector2 spot = b.Centre + Direction(random.Float() * MathF.Tau) * random.Range(isVillage ? 5f : 3f, reach);
                if (b.World.Placement.Overlaps(Footprint.Circle(spot, MaypoleReach), PlacementLayer.Built)) continue;
                // Never in front of the gate, the shrine, the stash or a merchant from where the camera looks, which is north, as it's tall and its rings hang wide.
                if (b.Site.Spawns.Any(spawn => spawn.Role is SpawnRole.Waygate or SpawnRole.Altar or SpawnRole.Stash or SpawnRole.Merchant
                    && ((spawn.Position - spot).Length < 9f || (MathF.Abs(spawn.Position.X - spot.X) < 8f && spawn.Position.Y > spot.Y && spawn.Position.Y - spot.Y < 24f)))) continue;
                if (!b.Place(StructureCatalog.Maypole, spot, random.Float() * MathF.Tau, padding: 1f)) continue;
                b.World.Placement.Add(Footprint.Circle(spot, MaypoleReach), PlacementLayer.Keepout);
                return;
            }
        }

        private static void PlaceSquare(SiteBuilder b, bool isVillage)
        {
            // The square's heart is usually where roads meet, so its well stands to one side, with a trough beside it for the beasts. The north draws its water with a
            // sweep, its curb towards the square and its stone away from it.
            bool sweep = b.Style == BuildingStyle.Nordic;
            for (int attempt = 0; attempt < (sweep ? 26 : 14); attempt++)
            {
                Vector2 spot = b.Centre + Direction(b.Random.Float() * MathF.Tau) * b.Random.Range(6f, sweep ? 17f : 13f);
                // A sweep is long, so it's tried pointing at the square and across it, and a square too crowded for it has a well of stone after all.
                Vector2 toCentre = (b.Centre - spot).LengthSquared > 1e-4f ? (b.Centre - spot).Normalized() : Vector2.UnitY;
                bool placed = sweep && (b.Place(StructureCatalog.WellSweep, spot, YawFacing(toCentre)) || b.Place(StructureCatalog.WellSweep, spot, YawFacing(new Vector2(-toCentre.Y, toCentre.X))));
                if (!placed && (!sweep || attempt >= 18)) placed = b.Place(StructureCatalog.Well, spot, 0f);
                if (!placed) continue;
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

            // The north fences more of its gardens, with the same poles on the slant as its fields.
            bool nordic = b.Style == BuildingStyle.Nordic;
            if (b.Random.Chance(nordic ? 0.6f : 0.35f))
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
            if (nordic && type != StructureCatalog.Barn) PlantGuardian(b, type, position, outward, tangent);
        }

        // A farm in the north keeps a tree by its house that nobody may fell, as old as the farm (a vårdträd): a birch, or now and then a linden, clear of the roofs.
        // It's asked for here and planted with the rest of what sites plant, where nothing built stands in its way by then (VegetationStage.PlantOnPurpose).
        private static void PlantGuardian(SiteBuilder b, StructureDefinition type, Vector2 position, Vector2 outward, Vector2 tangent)
        {
            WorldRandom random = b.Random.Fork("guardian");
            PlantSpecies tree = !Snowy(b) && random.Chance(0.2f) ? Woodlands.Linden : NordicBirch(b);
            float scale = tree.Scale.Lerp(random.Range(0.1f, 0.5f));
            float side = random.Chance(0.5f) ? 1f : -1f;
            Vector2 half = type.HalfExtents;
            foreach (int spot in new[] { 0, 1, 2 })
            {
                // Each spot is tried with the reach of the very tree that would grow there, so that no bough reaches into a roof.
                Vector2 Spot(float clear) => spot switch
                {
                    0 => position + tangent * side * (half.X + clear + 0.4f) + outward * (half.Y * 0.35f),
                    1 => position - tangent * side * (half.X + clear + 0.4f) + outward * (half.Y * 0.35f),
                    _ => position + outward * (half.Y + clear + 0.6f) + tangent * side * (half.X * 0.45f),
                };
                Vector2 at = Spot(tree.Footprint * scale);
                at = Spot(CrownRoom(b, tree, at, scale));
                if (TryPlant(b, tree, at, scale, GuardianRoom, random)) return;
            }
        }

        private const float GuardianRoom = 0.85f;      // Of its kind's room, what a tree by a house keeps from anything low, as a fence or a garden is.
        private const float WaysideRoom = 0.4f;        // And a birch along a lane from the lane, whose crown leans out over it.

        // Asks for a tree at a place if nothing tall stands within its crown's reach and nothing low within `room` of its kind's room, and keeps its crown's ground from
        // anything built after it as a site keeps a bed (PlacementLayer.Garden), which nothing wild grows on and only what a site plants does.
        private static bool TryPlant(SiteBuilder b, PlantSpecies tree, Vector2 at, float scale, float room, WorldRandom random)
        {
            float crown = CrownRoom(b, tree, at, scale);
            float low = tree.Footprint * scale * room;
            PlacementIndex placement = b.World.Placement;
            if (placement.Overlaps(Footprint.Circle(at, crown), PlacementLayer.Structure)) return false;
            if (placement.Overlaps(Footprint.Circle(at, low), PlacementLayer.Built | PlacementLayer.Road | PlacementLayer.Tree)) return false;
            if (!b.GroundFits(Footprint.Circle(at, 1f), 0.5f)) return false;
            placement.Add(Footprint.Circle(at, crown), PlacementLayer.Garden);
            b.World.Plantings.Add(new Planting(tree, at, scale, random.Float() * MathF.Tau, room));
            return true;
        }

        // How far from its trunk a tree planted at a place, at a scale, reaches: its crown and a little more, or what its kind keeps if that's more. A plant is chosen by
        // where it stands (NatureCatalog.ModelAt), so the crown is that very tree's.
        private static float CrownRoom(SiteBuilder b, PlantSpecies tree, Vector2 at, float scale)
        {
            float room = tree.Footprint * scale * GuardianRoom;
            NatureCatalog.SpeciesOf(NatureCatalog.ModelAt(tree, b.World.Plan.Seed, at), out ulong seed);
            if (tree.TryCrown(seed, out _, out _, out float crown)) room = MathF.Max(room, crown * scale + 0.8f);
            return room;
        }

        // Which birch a northern village plants: a summer one, or one white with frost where the land lies under snow.
        private static PlantSpecies NordicBirch(SiteBuilder b) => Snowy(b) ? Broadleaves.WinterBirch : Broadleaves.Birch;

        private static bool Snowy(SiteBuilder b) => b.Site.Biome >= 0 && b.Site.Biome < b.World.Plan.Biomes.Count && b.World.Plan.Biomes[b.Site.Biome].Ground == SurfaceMaterial.Snow;

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
                case BuildingStyle.Nordic:
                    // The winter's wood stacked round behind the house, by a barn the hay on its rack along the back of it, and now and then a farm's own well with
                    // its sweep, in the yard behind the house.
                    if (random.Chance(barn ? 0.4f : 0.32f)) b.Place(StructureCatalog.Woodstack, back - side * (width * 0.55f) + outward * 1.8f, random.Float() * MathF.Tau, padding: 0.4f);
                    if (barn && random.Chance(0.5f)) b.Place(StructureCatalog.HayRack, back + outward * 3.6f, YawFacing(outward), padding: 0.8f);
                    if (!barn && random.Chance(0.22f)) b.Place(StructureCatalog.WellSweep, back + side * (width * 0.4f) + outward * 4.6f, YawFacing(side), padding: 0.6f);
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
            // The north mows some of its fields for hay rather than sowing them, and hangs the hay on racks across them to dry, unless the land's under snow.
            WorldRandom meadows = b.Random.Fork("meadows");
            bool mows = b.Style == BuildingStyle.Nordic && !Snowy(b);
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
                bool meadow = mows && meadows.Chance(0.4f);
                if (!b.Field(position, half, YawFacing(towardsRoad), towardsRoad, out Vector2 gate, meadow)) continue;
                placed++;
                LayFieldTrack(b, gate, position);
            }
        }

        // Hay hung on racks in the open meadows round a northern village, beyond its houses and along the lie of the nearest road, as the summer's mowing dries.
        private static void HangHayAbout(SiteBuilder b)
        {
            WorldRandom random = b.Random.Fork("hay");
            int wanted = random.Int(2, 4), placed = 0;
            for (int attempt = 0; attempt < 30 && placed < wanted; attempt++)
            {
                Vector2 spot = b.Centre + Direction(random.Float() * MathF.Tau) * b.Site.Radius * random.Range(0.7f, 1.35f);
                Vector2 towardsRoad = TowardsNearestRoad(b, spot);
                // Room to walk round it either side, as it's a long wall of hay in the open.
                if (b.Place(StructureCatalog.HayRack, spot, YawFacing(towardsRoad), padding: 2.6f, maxSlope: 0.25f)) placed++;
            }
        }

        private const float LaneFenceOut = 2.5f;        // Metres out from a lane's edge that its fence stands.
        private const float LaneFenceRun = 11f;         // Metres a run of it goes before it turns with the lane.
        private const float LaneTreeEvery = 11.5f;      // Metres between the birches along a lane.

        // The lanes out of a northern village are fenced along one side with poles on the slant, where the beasts are driven out past the fields to the grazing, and planted
        // with birches along the other, from the last houses out past the fields. Each stretch draws which side is which, and every run of fence and every tree keeps out of
        // the way of anything already there, and of anyone standing anywhere.
        private static void LineLanes(SiteBuilder b, bool fences)
        {
            WorldSite site = b.Site;
            WorldRandom random = b.Random.Fork("lanes");
            float inner = site.Radius * 0.82f, outer = site.Radius * 1.6f;
            var stretch = new List<Vector3>();
            foreach (WorldRoad road in b.World.Roads.ToList())
            {
                if (road.Class == RoadClass.Footpath) continue;
                stretch.Clear();
                for (int i = 0; i <= road.Points.Count; i++)
                {
                    if (i < road.Points.Count)
                    {
                        float distance = (road.Points[i].Xz - site.Position).Length;
                        if (distance >= inner && distance <= outer)
                        {
                            stretch.Add(road.Points[i]);
                            continue;
                        }
                    }
                    if (stretch.Count >= 3) LineStretch(b, road, stretch, random, fences);
                    stretch.Clear();
                }
            }
        }

        private static void LineStretch(SiteBuilder b, WorldRoad road, List<Vector3> points, WorldRandom random, bool fences)
        {
            float fenceSide = random.Chance(0.5f) ? 1f : -1f;
            bool birches = random.Chance(0.85f);
            Vector2 Left(int i)
            {
                Vector2 tangent = points[Math.Min(i + 1, points.Count - 1)].Xz - points[Math.Max(i - 1, 0)].Xz;
                return tangent.LengthSquared > 1e-6f ? new Vector2(-tangent.Y, tangent.X).Normalized() : Vector2.Zero;
            }

            // The fence first, in runs from point to point a few lengths of it apart, each starting where the last ended, so they're one fence.
            if (fences)
            {
                Vector2? from = null;
                for (int i = 0; i < points.Count; i++)
                {
                    Vector2 left = Left(i);
                    if (left == Vector2.Zero) continue;
                    Vector2 at = points[i].Xz + left * fenceSide * (road.HalfWidth + LaneFenceOut);
                    if (from == null) { from = at; continue; }
                    if ((at - from.Value).Length < LaneFenceRun && i < points.Count - 1) continue;
                    if (LaneFenceFits(b, from.Value, at)) b.Wall(FenceUse.Field, from.Value, at);
                    from = at;
                }
            }

            // Then the birches, along the other side.
            if (!birches) return;
            PlantSpecies birch = NordicBirch(b);
            float since = random.Range(3f, 9f);
            for (int i = 1; i < points.Count; i++)
            {
                since += (points[i].Xz - points[i - 1].Xz).Length;
                if (since < LaneTreeEvery) continue;
                Vector2 left = Left(i);
                if (left == Vector2.Zero) continue;
                float scale = birch.Scale.Lerp(random.Range(0.1f, 0.8f));
                Vector2 at = points[i].Xz - left * fenceSide * (road.HalfWidth + 1f + birch.Footprint * scale * WaysideRoom + random.Range(0.2f, 1.2f));
                if (TryPlant(b, birch, at, scale, WaysideRoom, random)) since = random.Range(-1.5f, 1.5f);
            }
        }

        // Whether a run of a lane's fence stands clear of anything built, any road, any other fence, and anyone who stands near.
        private static bool LaneFenceFits(SiteBuilder b, Vector2 from, Vector2 to)
        {
            Vector2 along = to - from;
            float length = along.Length;
            if (length < 3f) return false;
            along /= length;
            float half = FenceCatalog.Roundpole.HalfThickness;
            PlacementIndex placement = b.World.Placement;
            Footprint run = Footprint.Segment(from + along * 0.5f, to - along * 0.5f, half);
            if (placement.Overlaps(run, PlacementLayer.Structure | PlacementLayer.Prop | PlacementLayer.Garden, 1.2f)) return false;
            if (placement.Overlaps(run, PlacementLayer.Field | PlacementLayer.Keepout, 1.8f)) return false;
            if (placement.Overlaps(run, PlacementLayer.Road, 0.5f)) return false;
            // Another fence, short of its own ends, which the runs either side of it share.
            if (placement.Overlaps(Footprint.Segment(from + along * 1.2f, to - along * 1.2f, half), PlacementLayer.Fence, 0.3f)) return false;
            Vector2 middle = (from + to) * 0.5f;
            foreach (WorldSite site in b.World.Sites)
            {
                if ((site.Position - middle).Length > site.Radius + length + 40f) continue;
                foreach (SiteSpawn spawn in site.Spawns)
                {
                    float t = Math.Clamp(Vector2.Dot(spawn.Position - from, along), 0f, length);
                    if ((from + along * t - spawn.Position).Length < 2.6f) return false;
                }
            }
            return true;
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
            WorldRandom meadows = b.Random.Fork("meadows");
            bool mows = b.Style == BuildingStyle.Nordic && !Snowy(b);
            for (int attempt = 0, placed = 0; attempt < 18 && placed < fields; attempt++)
            {
                Vector2 direction = Direction(b.Random.Float() * MathF.Tau);
                Vector2 position = b.Centre + direction * b.Random.Range(28f, 46f);
                Vector2 half = new Vector2(b.Random.Range(12f, 20f), b.Random.Range(9f, 14f));
                Vector2 towardsRoad = TowardsNearestRoad(b, position);
                if (b.Field(position, half, YawFacing(towardsRoad), towardsRoad, out Vector2 gate, mows && meadows.Chance(0.4f)))
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
            if (b.Style == BuildingStyle.Nordic)
            {
                // A northern farm's winter wood stacked round, its hay on a rack by the barn, and the tree by its house that nobody may fell.
                if (yard.Chance(0.65f)) b.Scatter(StructureCatalog.Woodstack, housePosition - toRoad * 9f - side * 7f, 5f, padding: 0.4f);
                if (yard.Chance(0.6f)) b.Scatter(StructureCatalog.HayRack, barn - toRoad * 11f, 6f, padding: 0.8f);
                PlantGuardian(b, StructureCatalog.House, housePosition, -toRoad, side);
            }
        }

        // A road that goes no further than here stops short of what stands in its middle, not at the middle of the ground it would stand on.
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
                if (i != 1)
                {
                    // Every wall but the back one carries a gatehouse, open, so that a fight in the yard can spill out onto the land and back in by another.
                    Vector2 middle = (start + end) * 0.5f;
                    Vector2 outward = (middle - b.Centre).Normalized();
                    b.Wall(FenceUse.Curtain, start, middle - along * 7f, b.Centre, WallIntoTower, WallIntoGatehouse);
                    b.Wall(FenceUse.Curtain, middle + along * 7f, end, b.Centre, WallIntoGatehouse, WallIntoTower);
                    b.Place(StructureCatalog.Gatehouse, middle, YawFacing(outward), padding: -1f, maxSlope: 0.6f);
                    // Nothing the camp puts down stands in a gate's way.
                    b.World.Placement.Add(Footprint.Box(middle - outward * 7f, new Vector2(7f, 7f), YawFacing(outward)), PlacementLayer.Keepout);
                }
                else
                {
                    b.Wall(FenceUse.Curtain, start, end, b.Centre, WallIntoTower, WallIntoTower);
                }
            }

            // The keep stands against the back wall, its door on the yard, with its hoard before it.
            float keepDepth = StructureCatalog.Keep.HalfExtents.Y;
            Vector2 keep = b.Centre - toRoad * (half - keepDepth - 3.5f);
            b.Place(StructureCatalog.Keep, keep, YawFacing(toRoad), padding: 0.5f, maxSlope: 0.6f);
            b.Spawn(SpawnRole.Chest, keep + toRoad * (keepDepth + 3f) + side * 7f);

            // The yard is the tyrant's: open ground with nothing standing in it, so that whoever fights it has room to lead it round, and nobody of the keep's garrison is in it.
            float yard = KeepYard(site);
            Vector2 heart = b.Centre + toRoad * (half * 0.08f);
            b.World.Placement.Add(Footprint.Circle(heart, yard), PlacementLayer.Keepout);
            b.Paint(GroundPaint.Yard, Footprint.Circle(heart, yard * 0.85f), 6f);
            b.Spawn(SpawnRole.Boss, heart);

            // The keep's people camp along its walls, out of the yard.
            float camp = (yard + half) * 0.5f;
            b.Scatter(StructureCatalog.Tent, b.Centre + side * camp - toRoad * (half * 0.45f), 6f);
            b.Scatter(StructureCatalog.Tent, b.Centre - side * camp - toRoad * (half * 0.45f), 6f);
            b.Scatter(StructureCatalog.Tent, b.Centre + side * camp + toRoad * (half * 0.5f), 5f);
            b.Scatter(StructureCatalog.Tent, b.Centre - side * camp + toRoad * (half * 0.5f), 5f);
            for (int i = 0; i < 6; i++)
            {
                float angle = b.Random.Float() * MathF.Tau;
                b.Scatter(StructureCatalog.Crates, b.Centre + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (half - 6f), 4f);
            }

            b.SpawnAround(SpawnRole.Guard, b.Centre + toRoad * (half - 5f), 6f, 3);
            int raiders = b.Random.Int(6, 9);
            for (int i = 0; i < raiders; i++)
            {
                float angle = (i + b.Random.Range(-0.3f, 0.3f)) * MathF.Tau / raiders;
                b.Spawn(SpawnRole.Raider, b.Centre + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (camp + b.Random.Range(-3f, 3f)));
            }
        }

        private const float KeepYardShare = 0.5f;           // Of a keep's radius, the open yard in its middle where its tyrant waits.

        /// <summary>The radius of the open yard in a keep's middle, where its tyrant waits and nothing stands, which nobody of its garrison camps in.</summary>
        public static float KeepYard(WorldSite site) => site.Radius * KeepYardShare;

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
                    // A ring of stones' barrow and a pen's cages and banners are their builders' own now (PLACES.md).
                    case SiteKind.Ruins when own.Chance(0.45f):
                        OpenDig(b);
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

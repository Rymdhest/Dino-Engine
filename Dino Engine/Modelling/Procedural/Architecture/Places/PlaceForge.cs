using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture.Seats;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Places
{
    /// <summary>
    /// Builds a piece of a place with a story of its own from its <see cref="PlaceSpec"/>, which is all there is of it: the same id makes the same piece on any machine (PLACES.md). Each kind's
    /// numbers are listed with it in <see cref="PlacePiece"/>. Headless, and touches nothing shared, so it runs on worker threads while a world loads.
    /// </summary>
    public static class PlaceForge
    {
        public static Mesh Make(in PlaceSpec spec, List<AssetLight> lights)
        {
            var b = new MeshBuilder();
            var random = new WorldRandom(spec.Seed).Fork(spec.Kind.ToString());
            switch (spec.Kind)
            {
                case PlacePiece.Hull: Ships.Hull(b, spec, random); break;
                case PlacePiece.Mast: Ships.Mast(b, spec, random); break;
                case PlacePiece.Wreckage: Ships.Wreckage(b, spec, random); break;
                case PlacePiece.Anchor: Ships.Anchor(b, spec, random); break;
                case PlacePiece.ShipBoat: Ships.ShipBoat(b, spec, random); break;
                case PlacePiece.BurstCage: Ships.BurstCage(b, spec, random); break;
                case PlacePiece.Cargo: Ships.Cargo(b, spec, random, lights); break;
                case PlacePiece.Driftwood: Ships.Driftwood(b, spec, random); break;
                case PlacePiece.Salvage: Ships.Salvage(b, spec, random); break;

                case PlacePiece.Cage: Prison.Cage(b, spec, random); break;
                case PlacePiece.CageDoor: Prison.CageDoor(b, spec, random); break;
                case PlacePiece.BreakingPost: Prison.BreakingPost(b, spec, random); break;
                case PlacePiece.HarnessRack: Prison.HarnessRack(b, spec, random); break;
                case PlacePiece.CollarForge: Prison.CollarForge(b, spec, random, lights); break;
                case PlacePiece.ChainStake: Prison.ChainStake(b, spec, random); break;

                case PlacePiece.LabHall: Laboratory.Hall(b, spec, random, lights); break;
                case PlacePiece.Hatchery: Laboratory.Hatchery(b, spec, random, lights); break;
                case PlacePiece.Vat: Laboratory.Vat(b, spec, random, lights); break;
                case PlacePiece.Specimens: Laboratory.Specimens(b, spec, random, lights); break;
                case PlacePiece.AmberBench: Laboratory.AmberBench(b, spec, random, lights); break;
                case PlacePiece.Pipes: Laboratory.Pipes(b, spec, random); break;

                case PlacePiece.Fossil: Excavation.Fossil(b, spec, random, lights); break;
                case PlacePiece.DigGrid: Excavation.DigGrid(b, spec, random); break;
                case PlacePiece.Revetment: Excavation.Revetment(b, spec, random); break;
                case PlacePiece.Sieve: Excavation.Sieve(b, spec, random); break;
                case PlacePiece.Jackets: Excavation.Jackets(b, spec, random); break;
                case PlacePiece.BoneCrates: Excavation.BoneCrates(b, spec, random); break;
                case PlacePiece.Spoil: Excavation.Spoil(b, spec, random); break;
                case PlacePiece.DrawingTable: Excavation.DrawingTable(b, spec, random, lights); break;
                case PlacePiece.Hoist: Excavation.Hoist(b, spec, random); break;

                case PlacePiece.NestMound: Nests.Mound(b, spec, random); break;
                case PlacePiece.Carcass: Nests.Carcass(b, spec, random); break;
                case PlacePiece.Shells: Nests.Shells(b, spec, random); break;
                case PlacePiece.Web: Nests.Web(b, spec, random); break;
                case PlacePiece.EggSac: Nests.EggSac(b, spec, random); break;
                case PlacePiece.Cocoon: Nests.Cocoon(b, spec, random); break;
                case PlacePiece.Wrap: Nests.Wrap(b, spec, random); break;
                case PlacePiece.SilkFloor: Nests.SilkFloor(b, spec, random); break;

                case PlacePiece.Stump: Logging.Stump(b, spec, random); break;
                case PlacePiece.Log: Logging.Log(b, spec, random); break;
                case PlacePiece.LogDeck: Logging.LogDeck(b, spec, random); break;
                case PlacePiece.Sawpit: Logging.Sawpit(b, spec, random); break;
                case PlacePiece.PlankStack: Logging.PlankStack(b, spec, random); break;
                case PlacePiece.ToolRack: Logging.ToolRack(b, spec, random); break;
                case PlacePiece.CharcoalClamp: Logging.CharcoalClamp(b, spec, random); break;
                case PlacePiece.Skidder: Logging.Skidder(b, spec, random); break;

                case PlacePiece.Face: Quarry.Face(b, spec, random); break;
                case PlacePiece.Block: Quarry.Block(b, spec, random); break;
                case PlacePiece.BlockStack: Quarry.BlockStack(b, spec, random); break;
                case PlacePiece.Crane: Quarry.Crane(b, spec, random); break;
                case PlacePiece.Sledge: Quarry.Sledge(b, spec, random); break;
                case PlacePiece.Banker: Quarry.Banker(b, spec, random); break;
                case PlacePiece.Rubble: Quarry.Rubble(b, spec, random); break;
                case PlacePiece.SplitLine: Quarry.SplitLine(b, spec, random); break;

                case PlacePiece.Jetty: Fishing.Jetty(b, spec, random, lights); break;
                case PlacePiece.Boat: Fishing.Boat(b, spec, random); break;
                case PlacePiece.NetRack: Fishing.NetRack(b, spec, random); break;
                case PlacePiece.FishRack: Fishing.FishRack(b, spec, random); break;
                case PlacePiece.Smokehouse: Fishing.Smokehouse(b, spec, random); break;
                case PlacePiece.BoatStocks: Fishing.BoatStocks(b, spec, random); break;
                case PlacePiece.Pots: Fishing.Pots(b, spec, random); break;
                case PlacePiece.Capstan: Fishing.Capstan(b, spec, random); break;

                case PlacePiece.HatchingShed: Farm.HatchingShed(b, spec, random, lights); break;
                case PlacePiece.FeedRack: Farm.FeedRack(b, spec, random); break;
                case PlacePiece.WaterTank: Farm.WaterTank(b, spec, random); break;
                case PlacePiece.FarmGate: Farm.Gate(b, spec, random); break;

                case PlacePiece.Scales: Trading.Scales(b, spec, random); break;
                case PlacePiece.Bales: Trading.Bales(b, spec, random); break;
                case PlacePiece.Ledger: Trading.Ledger(b, spec, random); break;
                case PlacePiece.HitchingRail: Trading.HitchingRail(b, spec, random); break;
                case PlacePiece.NoticeBoard: Trading.NoticeBoard(b, spec, random); break;

                case PlacePiece.RuinWall: Ruins.Wall(b, spec, random); break;
                case PlacePiece.RuinColumn: Ruins.Column(b, spec, random); break;
                case PlacePiece.FallenColumn: Ruins.FallenColumn(b, spec, random); break;
                case PlacePiece.RuinArch: Ruins.Arch(b, spec, random); break;
                case PlacePiece.RuinSteps: Ruins.Steps(b, spec, random); break;
                case PlacePiece.RuneStone: Ruins.RuneStone(b, spec, random); break;
                case PlacePiece.Statue: Ruins.Statue(b, spec, random); break;

                case PlacePiece.SnowWall: SnowWorks.Wall(b, spec, random); break;
                case PlacePiece.SnowTower: SnowWorks.Tower(b, spec, random); break;
                case PlacePiece.SnowGate: SnowWorks.Gate(b, spec, random); break;
                case PlacePiece.Snowman: SnowWorks.Snowman(b, spec, random); break;
                case PlacePiece.SnowLantern: SnowWorks.SnowLantern(b, spec, random, lights); break;
                case PlacePiece.Sledges: SnowWorks.Sledges(b, spec, random); break;
                case PlacePiece.Pennant: SnowWorks.Pennant(b, spec, random); break;
                case PlacePiece.IceAltar: SnowWorks.IceAltar(b, spec, random, lights); break;

                case PlacePiece.ShrineHall: Shrine.Hall(b, spec, random); break;
                case PlacePiece.ShrineGate: Shrine.Gate(b, spec, random); break;
                case PlacePiece.ShrineLantern: Shrine.Lantern(b, spec, random); break;
                case PlacePiece.Offerings: Shrine.Offerings(b, spec, random); break;
                case PlacePiece.Basin: Shrine.Basin(b, spec, random); break;
                case PlacePiece.Bell: Shrine.Bell(b, spec, random); break;
                case PlacePiece.Ribbons: Shrine.Ribbons(b, spec, random); break;
                case PlacePiece.Cairns: Shrine.Cairns(b, spec, random); break;
                case PlacePiece.PrayerLine: Shrine.PrayerLine(b, spec, random); break;

                case PlacePiece.Menhir: Stones.Menhir(b, spec, random, lights); break;
                case PlacePiece.Trilithon: Stones.Trilithon(b, spec, random); break;
                case PlacePiece.Barrow: Stones.Barrow(b, spec, random, lights); break;
                case PlacePiece.Totem: Stones.Totem(b, spec, random, lights); break;
                case PlacePiece.CultHeart: Stones.CultHeart(b, spec, random, lights); break;

                case PlacePiece.BeaconTower: Beacon.Tower(b, spec, random); break;
                case PlacePiece.BeaconPyre: Beacon.Pyre(b, spec, random); break;
                case PlacePiece.SightStone: Beacon.SightStone(b, spec, random); break;
                case PlacePiece.Horn: Beacon.Horn(b, spec, random); break;

                case PlacePiece.Kennel: Pen.Kennel(b, spec, random); break;
                case PlacePiece.ButcherBlock: Pen.ButcherBlock(b, spec, random); break;
                case PlacePiece.BiteDummy: Pen.BiteDummy(b, spec, random); break;
                case PlacePiece.Midden: Pen.Midden(b, spec, random); break;

                case PlacePiece.Boiler: Works.Boiler(b, spec, random, lights); break;
                case PlacePiece.Haystack: Works.Haystack(b, spec, random); break;
                case PlacePiece.MendingNet: Works.MendingNet(b, spec, random); break;
                case PlacePiece.GuttingTable: Works.GuttingTable(b, spec, random); break;
                case PlacePiece.Slash: Works.Slash(b, spec, random); break;

                case PlacePiece.Litter: Underfoot.Litter(b, spec, random); break;
                case PlacePiece.Puddle: Underfoot.Puddle(b, spec, random); break;
                case PlacePiece.Duckboards: Underfoot.Duckboards(b, spec, random); break;

                case PlacePiece.WeaponRack: Camp.WeaponRack(b, spec, random); break;
                case PlacePiece.Torch: Camp.Torch(b, spec, random, lights); break;
                case PlacePiece.Brazier: Camp.Brazier(b, spec, random, lights); break;
                case PlacePiece.Firewood: Camp.Firewood(b, spec, random); break;
                case PlacePiece.Wagon: Camp.Wagon(b, spec, random); break;
                case PlacePiece.Gibbet: Camp.Gibbet(b, spec, random); break;
                case PlacePiece.Barricade: Camp.Barricade(b, spec, random); break;
                case PlacePiece.CommandTent: Camp.CommandTent(b, spec, random, lights); break;
                case PlacePiece.Signpost: Camp.Signpost(b, spec, random); break;
                case PlacePiece.MessTable: Camp.MessTable(b, spec, random, lights); break;

                case PlacePiece.Crates: Common.Crates(b, spec, random); break;
                case PlacePiece.Barrels: Common.Barrels(b, spec, random); break;
                case PlacePiece.Tent: Common.Tent(b, spec, random); break;
                case PlacePiece.Campfire: Common.Campfire(b, spec, random, lights); break;
                case PlacePiece.Lantern: Common.Lantern(b, spec, random, lights); break;
                case PlacePiece.Banner: Common.Banner(b, spec, random); break;
                case PlacePiece.Shelter: Common.Shelter(b, spec, random); break;

                // A commander's seat (SEATS.md).
                case PlacePiece.SeatWall: Fortress.Wall(b, spec, random); break;
                case PlacePiece.SeatTower: Fortress.Tower(b, spec, random, lights); break;
                case PlacePiece.SeatGate: Fortress.Gate(b, spec, random, lights); break;
                case PlacePiece.SeatSteps: Fortress.Steps(b, spec, random); break;
                case PlacePiece.SeatRevet: Fortress.Revet(b, spec, random); break;
                case PlacePiece.GreatHall: Fortress.Hall(b, spec, random, lights); break;
                case PlacePiece.TyrantStall: Fortress.Stall(b, spec, random, lights); break;
                case PlacePiece.FallenLord: SeatDressing.FallenLord(b, spec, random); break;
                case PlacePiece.GreatBrazier: SeatDressing.GreatBrazier(b, spec, random, lights); break;
                case PlacePiece.LongBanner: SeatDressing.LongBanner(b, spec, random); break;
                case PlacePiece.FeedTrough: SeatDressing.FeedTrough(b, spec, random); break;
                case PlacePiece.YardRing: SeatDressing.YardRing(b, spec, random); break;
                case PlacePiece.Hoard: SeatDressing.Hoard(b, spec, random); break;
                case PlacePiece.GatePier: SeatDressing.GatePier(b, spec, random, lights); break;
                case PlacePiece.BoneMatrix: OssuaryWorks.Matrix(b, spec, random, lights); break;
                case PlacePiece.BonePale: OssuaryWorks.BonePale(b, spec, random); break;
                case PlacePiece.RibArch: OssuaryWorks.RibArch(b, spec, random, lights); break;
                case PlacePiece.GreatBarrow: OssuaryWorks.GreatBarrow(b, spec, random, lights); break;
                case PlacePiece.BileVats: OssuaryWorks.BileVats(b, spec, random, lights); break;
                case PlacePiece.BileCradle: OssuaryWorks.Cradle(b, spec, random, lights); break;
                case PlacePiece.BilePool: OssuaryWorks.BilePool(b, spec, random, lights); break;
                case PlacePiece.BroodDome: ReichWorks.BroodDome(b, spec, random, lights); break;
                case PlacePiece.BroodTanks: ReichWorks.BroodTanks(b, spec, random, lights); break;
                case PlacePiece.GreatShell: ReichWorks.GreatShell(b, spec, random, lights); break;
                case PlacePiece.BlastFurnace: ReichWorks.BlastFurnace(b, spec, random, lights); break;
                case PlacePiece.Armature: ReichWorks.Armature(b, spec, random); break;
                case PlacePiece.ScrapHeap: ReichWorks.ScrapHeap(b, spec, random, lights); break;
                case PlacePiece.Colossus: ReichField.Colossus(b, spec, random, lights); break;
                case PlacePiece.TrophyPylon: ReichField.TrophyPylon(b, spec, random, lights); break;
                case PlacePiece.Dais: ReichField.Dais(b, spec, random, lights); break;
                case PlacePiece.Pavilion: ReichField.Pavilion(b, spec, random, lights); break;
                case PlacePiece.WarWagon: ReichField.WarWagon(b, spec, random); break;
                case PlacePiece.CageWagon: ReichField.CageWagon(b, spec, random); break;
            }
            return b.Build();
        }

        /// <summary>Where smoke leaves a piece, in its own frame: a smokehouse's roof, a charcoal clamp's vents, the Institute's chimneys.</summary>
        public static IReadOnlyList<Vector3> ChimneysOf(in PlaceSpec spec) => spec.Kind switch
        {
            PlacePiece.LabHall => Laboratory.ChimneyTops(Math.Clamp(spec.Metres(0), 10f, 26f), Math.Clamp(spec.Metres(1), 8f, 16f)),
            PlacePiece.CharcoalClamp => Logging.VentsOf(Logging.ClampRadius(spec)),
            PlacePiece.Smokehouse => Fishing.SmokeOf(),
            PlacePiece.Boiler => Works.BoilerSmoke(),
            PlacePiece.BileVats => OssuaryWorks.VatChimneys(Math.Clamp(spec.Int(0), 1, 5), Math.Clamp(spec.Metres(1), 5f, 13f)),
            PlacePiece.BlastFurnace => ReichWorks.FurnaceChimneys(Math.Clamp(spec.Metres(0), 14f, 28f)),
            PlacePiece.GreatHall => Fortress.HallChimneys(Math.Clamp(spec.Metres(0), 18f, 34f), Math.Clamp(spec.Metres(1), 10f, 18f), Math.Clamp(spec.Metres(2), 8f, 16f)),
            _ => Array.Empty<Vector3>(),
        };

        /// <summary>How long a piece takes to grow, roughly, so the slowest are begun first.</summary>
        public static int Slowness(PlacePiece kind) => kind switch
        {
            PlacePiece.Hull or PlacePiece.LabHall or PlacePiece.Face or PlacePiece.Fossil or PlacePiece.BeaconTower or PlacePiece.ShrineHall => 3,
            PlacePiece.GreatHall or PlacePiece.Colossus or PlacePiece.Armature or PlacePiece.BroodDome or PlacePiece.BlastFurnace or PlacePiece.Pavilion => 3,
            PlacePiece.Mast or PlacePiece.Hatchery or PlacePiece.Cage or PlacePiece.RuinWall or PlacePiece.SnowTower or PlacePiece.Jetty or PlacePiece.Barrow or PlacePiece.Kennel
                or PlacePiece.CommandTent or PlacePiece.Menhir or PlacePiece.Trilithon => 2,
            PlacePiece.SeatWall or PlacePiece.SeatTower or PlacePiece.SeatGate or PlacePiece.TyrantStall or PlacePiece.SeatSteps or PlacePiece.SeatRevet => 2,
            PlacePiece.BoneMatrix or PlacePiece.GreatBarrow or PlacePiece.BileVats or PlacePiece.BileCradle => 2,
            PlacePiece.BroodTanks or PlacePiece.CageWagon or PlacePiece.WarWagon or PlacePiece.Dais or PlacePiece.TrophyPylon => 2,
            _ => 1,
        };
    }
}

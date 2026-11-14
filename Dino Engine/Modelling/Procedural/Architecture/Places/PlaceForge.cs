using Dino_Engine.Modelling.Model;
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

                case PlacePiece.Crates: Common.Crates(b, spec, random); break;
                case PlacePiece.Barrels: Common.Barrels(b, spec, random); break;
                case PlacePiece.Tent: Common.Tent(b, spec, random); break;
                case PlacePiece.Campfire: Common.Campfire(b, spec, random, lights); break;
                case PlacePiece.Lantern: Common.Lantern(b, spec, random, lights); break;
                case PlacePiece.Banner: Common.Banner(b, spec, random); break;
                case PlacePiece.Shelter: Common.Shelter(b, spec, random); break;
            }
            return b.Build();
        }

        /// <summary>Where smoke leaves a piece, in its own frame: a smokehouse's roof, a charcoal clamp's vents, the Institute's chimneys.</summary>
        public static IReadOnlyList<Vector3> ChimneysOf(in PlaceSpec spec) => spec.Kind switch
        {
            PlacePiece.LabHall => Laboratory.ChimneyTops(Math.Clamp(spec.Metres(0), 10f, 26f), Math.Clamp(spec.Metres(1), 8f, 16f)),
            PlacePiece.CharcoalClamp => Logging.VentsOf(Logging.ClampRadius(spec)),
            PlacePiece.Smokehouse => Fishing.SmokeOf(),
            _ => Array.Empty<Vector3>(),
        };

        /// <summary>How long a piece takes to grow, roughly, so the slowest are begun first.</summary>
        public static int Slowness(PlacePiece kind) => kind switch
        {
            PlacePiece.Hull or PlacePiece.LabHall or PlacePiece.Face or PlacePiece.Fossil => 3,
            PlacePiece.Mast or PlacePiece.Hatchery or PlacePiece.Cage or PlacePiece.RuinWall or PlacePiece.SnowTower or PlacePiece.Jetty => 2,
            _ => 1,
        };
    }
}

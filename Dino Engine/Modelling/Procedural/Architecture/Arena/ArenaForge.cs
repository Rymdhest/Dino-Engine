using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture.Plaza;
using Dino_Engine.World.Content;

namespace Dino_Engine.Modelling.Procedural.Architecture.Arena
{
    /// <summary>
    /// Builds a piece of an arena from its <see cref="ArenaSpec"/>, which is all there is of it: the same id makes the same piece on any machine. Each shape's numbers are listed with it in
    /// <see cref="ArenaShape"/>. Headless, and touches nothing shared, so the world's pieces are built on worker threads with its plants (WorldAssetLibrary.Grow). Read ARENAS.md.
    /// </summary>
    public static class ArenaForge
    {
        /// <summary>How far under its hook a chandelier's wheel hangs, which is what it comes down onto.</summary>
        public const float ChandelierDrop = CathedralPieces.ChandelierDrop;

        /// <summary>Where a bolt-thrower's bolt points from in its own frame, facing +z and turning about its post at the origin.</summary>
        public static OpenTK.Mathematics.Vector3 BallistaMuzzle => ColosseumPieces.Muzzle;

        public static Mesh Make(in ArenaSpec spec, List<AssetLight> lights)
        {
            var b = new MeshBuilder();
            Build(b, spec, lights);
            return b.Build();
        }

        private static void Build(MeshBuilder b, in ArenaSpec spec, List<AssetLight> lights)
        {
            switch (spec.Shape)
            {
                case ArenaShape.Arc:
                    ArenaWalls.Build(b, WallLine.Arc(spec.Metres(2), spec.Degrees(3), spec.Degrees(4), spec.Int(8)), (ArenaWallForm)spec.Int(0), (ArenaStoneKind)spec.Int(1), spec.Metres(5), spec.Metres(6),
                        SoilOf(spec, 7), (ArenaTrim)spec.Int(9), spec.Seed, lights);
                    break;
                case ArenaShape.Run:
                    ArenaWalls.Build(b, WallLine.Straight(spec.Metres(2)), (ArenaWallForm)spec.Int(0), (ArenaStoneKind)spec.Int(1), spec.Metres(3), spec.Metres(4), SoilOf(spec, 5), (ArenaTrim)spec.Int(6), spec.Seed, lights);
                    break;
                case ArenaShape.Gatehouse:
                    ArenaGates.Gatehouse(b, (ArenaStoneKind)spec.Int(0), spec.Metres(1), spec.Metres(2), spec.Metres(3), spec.Metres(4), spec.Metres(5), spec.Seed, lights);
                    break;
                case ArenaShape.Portcullis:
                    ArenaGates.Grille(b, (ArenaGrille)spec.Int(0), spec.Metres(1), spec.Metres(2), spec.Seed);
                    break;
                case ArenaShape.Pylon:
                    ArenaDressing.Pylon(b, (ArenaStoneKind)spec.Int(0), spec.Metres(1), spec.Metres(2), spec.Int(3), spec.Seed, lights);
                    break;
                case ArenaShape.Brazier:
                    ArenaDressing.Brazier(b, spec.Int(0), spec.Int(1) / 100f, spec.Seed, lights);
                    break;
                case ArenaShape.Banner:
                    ArenaDressing.Banner(b, spec.Metres(0), spec.Metres(1), spec.Metres(2), spec.Int(3), spec.Seed);
                    break;
                case ArenaShape.Bay:
                    ArenaDressing.Bay(b, (ArenaStoneKind)spec.Int(0), spec.Metres(1), spec.Metres(2), spec.Metres(3), spec.Seed, lights);
                    break;
                case ArenaShape.Stair:
                    ArenaDressing.Stair(b, (ArenaStoneKind)spec.Int(0), spec.Metres(1), spec.Int(2), spec.Metres(3), spec.Metres(4), spec.Seed);
                    break;
                case ArenaShape.Lamp:
                    ArenaDressing.Lamp(b, spec.Int(0), spec.Seed, lights);
                    break;
                case ArenaShape.Remains:
                    ArenaDressing.Remains(b, spec.Int(0), spec.Int(1) / 100f, spec.Seed);
                    break;
                case ArenaShape.Arcade:
                    ColosseumPieces.Arcade(b, WallLine.Arc(spec.Metres(1), spec.Degrees(2), spec.Degrees(3), spec.Int(4)), (ArenaStoneKind)spec.Int(0), Math.Max(1, spec.Int(5)), spec.Metres(6), spec.Metres(7), spec.Metres(8), spec.Seed);
                    break;
                case ArenaShape.Tribune:
                    ColosseumPieces.Tribune(b, (ArenaStoneKind)spec.Int(0), spec.Metres(1), spec.Metres(2), spec.Metres(3), spec.Seed, lights);
                    break;
                case ArenaShape.Hatch:
                    ColosseumPieces.HatchFrame(b, spec.Metres(0), spec.Seed);
                    break;
                case ArenaShape.Leaf:
                    ColosseumPieces.HatchLeaf(b, spec.Metres(0), spec.Seed);
                    break;
                case ArenaShape.Ballista:
                    ColosseumPieces.Ballista(b, spec.Seed);
                    break;
                case ArenaShape.Bars:
                    CagePieces.Bars(b, spec.Metres(0), spec.Degrees(1), spec.Degrees(2), spec.Metres(3), spec.Metres(4), spec.Metres(5), spec.Seed);
                    break;
                case ArenaShape.PenGate:
                    CagePieces.PenGate(b, (ArenaStoneKind)spec.Int(0), spec.Metres(1), spec.Metres(2), spec.Metres(3), spec.Metres(4), spec.Seed);
                    break;
                case ArenaShape.Nest:
                    CagePieces.Nest(b, spec.Metres(0), spec.Seed);
                    break;
                case ArenaShape.Gallery:
                    CagePieces.Gallery(b, spec.Metres(0), spec.Degrees(1), spec.Degrees(2), spec.Metres(3), spec.Metres(4), spec.Seed, lights);
                    break;
                case ArenaShape.Trough:
                    CagePieces.Trough(b, (ArenaStoneKind)spec.Int(0), spec.Metres(1), spec.Seed);
                    break;
                case ArenaShape.Gibbet:
                    CagePieces.Gibbet(b, spec.Metres(0), spec.Seed);
                    break;
                case ArenaShape.Colonnade:
                    TemplePieces.Colonnade(b, (ArenaStoneKind)spec.Int(0), spec.Metres(1), spec.Metres(2), spec.Metres(3), spec.Seed);
                    break;
                case ArenaShape.SunDisc:
                    TemplePieces.SunDisc(b, (ArenaStoneKind)spec.Int(0), spec.Metres(1), spec.Seed, lights);
                    break;
                case ArenaShape.Portal:
                    TemplePieces.Portal(b, (ArenaStoneKind)spec.Int(0), spec.Metres(1), spec.Metres(2), spec.Metres(3), spec.Seed);
                    break;
                case ArenaShape.DoorLeaf:
                    TemplePieces.DoorLeaf(b, spec.Metres(0), spec.Metres(1), spec.Seed);
                    break;
                case ArenaShape.Pedestal:
                    TemplePieces.MirrorPedestal(b, (ArenaStoneKind)spec.Int(0), spec.Seed);
                    break;
                case ArenaShape.Mirror:
                    TemplePieces.Mirror(b, spec.Metres(0), spec.Seed);
                    break;
                case ArenaShape.Inlay:
                    TemplePieces.Inlay(b, (ArenaStoneKind)spec.Int(0), spec.Metres(1), spec.Seed);
                    break;
                case ArenaShape.Statue:
                    OnslaughtPieces.Statue(b, spec.Int(0), spec.Int(1), spec.Int(2) / 100f, spec.Degrees(3), spec.Metres(4), spec.Seed);
                    break;
                case ArenaShape.Wagon:
                    OnslaughtPieces.Wagon(b, spec.Seed);
                    break;
                case ArenaShape.Standard:
                    OnslaughtPieces.Standard(b, spec.Degrees(0), spec.Seed);
                    break;
                case ArenaShape.Fossil:
                    OnslaughtPieces.FossilBed(b, spec.Int(0) / 100f, spec.Seed);
                    break;
                case ArenaShape.Scaffold:
                    OnslaughtPieces.Scaffold(b, spec.Metres(0), spec.Metres(1), spec.Seed);
                    break;
                case ArenaShape.Crane:
                    OnslaughtPieces.Crane(b, spec.Metres(0), spec.Seed);
                    break;
                case ArenaShape.Vent:
                    OnslaughtPieces.Vent(b, spec.Metres(0), spec.Seed, lights);
                    break;
                case ArenaShape.GothicPier:
                    CathedralPieces.Pier(b, (ArenaStoneKind)spec.Int(0), spec.Metres(1), spec.Seed);
                    break;
                case ArenaShape.IronArch:
                    CathedralPieces.IronArch(b, spec.Metres(0), spec.Metres(1), spec.Seed);
                    break;
                case ArenaShape.Chain:
                    CathedralPieces.Chain(b, spec.Metres(0), spec.Seed);
                    break;
                case ArenaShape.Chandelier:
                    CathedralPieces.Chandelier(b, spec.Metres(0), spec.Seed, lights);
                    break;
                case ArenaShape.Screen:
                    CathedralPieces.Screen(b, spec.Metres(0), spec.Metres(1), spec.Seed);
                    break;
                case ArenaShape.ScreenGate:
                    CathedralPieces.ScreenGate(b, (ArenaStoneKind)spec.Int(0), spec.Metres(1), spec.Metres(2), spec.Metres(3), spec.Seed);
                    break;
                case ArenaShape.Apse:
                    CathedralPieces.Apse(b, (ArenaStoneKind)spec.Int(0), spec.Metres(1), spec.Degrees(2), spec.Degrees(3), spec.Metres(4), Math.Max(1, spec.Int(5)), spec.Seed);
                    break;
                case ArenaShape.Throne:
                    CathedralPieces.Throne(b, (ArenaStoneKind)spec.Int(0), spec.Metres(1), spec.Seed, lights);
                    break;
                case ArenaShape.IronSkull:
                    CathedralPieces.IronSkull(b, spec.Metres(0), spec.Seed, lights);
                    break;
                case ArenaShape.Pews:
                    CathedralPieces.Pews(b, spec.Metres(0), Math.Max(1, spec.Int(1)), spec.Seed);
                    break;
                case ArenaShape.Clutch:
                case ArenaShape.Shells:
                    CagePieces.Clutch(b, spec.Metres(0), spec.Seed, broken: spec.Shape == ArenaShape.Shells);
                    break;
            }
        }

        // A wall's ground, where "-1" means it holds nothing up.
        private static float SoilOf(in ArenaSpec spec, int index) => spec.Int(index) < 0 ? -1f : spec.Metres(index);
    }
}

using Dino_Engine.Modelling.Model;
using Dino_Engine.World.Content;

namespace Dino_Engine.Modelling.Procedural.Architecture.Plaza
{
    /// <summary>
    /// Builds a piece of a town square from its <see cref="PlazaSpec"/>, which is all there is of it: the same id makes the same piece on any machine. Each kind's numbers are listed
    /// with it in <see cref="PlazaKind"/>. Headless, and touches nothing shared. Read SQUARE.md.
    /// </summary>
    public static class PlazaForge
    {
        public static Mesh Make(in PlazaSpec spec, List<AssetLight> lights)
        {
            var b = new MeshBuilder();
            switch (spec.Kind)
            {
                case PlazaKind.Arc:
                    PlazaWalls.Build(b, WallLine.Arc(spec.Metres(1), spec.Degrees(2), spec.Degrees(3), spec.Int(7)), (WallForm)spec.Int(0), spec.Metres(4), SoilOf(spec, 5), spec.Metres(6), spec.Seed);
                    break;
                case PlazaKind.Run:
                    PlazaWalls.Build(b, WallLine.Straight(spec.Metres(1)), (WallForm)spec.Int(0), spec.Metres(2), SoilOf(spec, 3), spec.Metres(4), spec.Seed);
                    break;
                case PlazaKind.Steps:
                    PlazaPieces.Steps(b, spec.Metres(0), spec.Int(1), spec.Metres(2), spec.Metres(3), spec.Metres(4), spec.Metres(5), spec.Metres(6), spec.Metres(7), spec.Seed, lights);
                    break;
                case PlazaKind.Pier:
                    PlazaPieces.PierPiece(b, spec.Metres(0), spec.Metres(1), spec.Int(2), spec.Seed, lights);
                    break;
                case PlazaKind.Pergola:
                    PlazaDressing.Pergola(b, spec.Metres(0), spec.Metres(1), Math.Max(1, spec.Int(2)), spec.Metres(3), spec.Int(4) / 100f, spec.Seed, lights);
                    break;
                case PlazaKind.Arch:
                    PlazaPieces.Arch(b, spec.Metres(0), spec.Metres(1), spec.Metres(2), spec.Metres(3), spec.Seed, lights);
                    break;
                case PlazaKind.Table:
                    PlazaDressing.Table(b, spec.Metres(0), spec.Seed);
                    break;
                case PlazaKind.Pot:
                    PlazaDressing.Pot(b, spec.Int(0), spec.Metres(1), spec.Seed);
                    break;
                case PlazaKind.Mast:
                    PlazaDressing.Mast(b, spec.Metres(0), spec.Int(1) == 1, spec.Seed, lights);
                    break;
                case PlazaKind.Bunting:
                    PlazaDressing.Bunting(b, spec.Metres(0), spec.Metres(1), spec.Metres(2), spec.Seed);
                    break;
                case PlazaKind.Line:
                    PlazaWalls.Band(b, WallLine.Straight(spec.Metres(1)), spec.Int(0) == 1, spec.Metres(2), spec.Seed);
                    break;
                case PlazaKind.Well:
                    PlazaPieces.Well(b, spec.Metres(0), spec.Seed);
                    break;
                case PlazaKind.Ring:
                    PlazaWalls.Band(b, WallLine.Arc(spec.Metres(1), spec.Degrees(2), spec.Degrees(3), 1f), spec.Int(0) == 1, spec.Metres(4), spec.Seed);
                    break;
            }
            return b.Build();
        }

        // A wall's soil, where "-1" means it holds nothing up.
        private static float SoilOf(in PlazaSpec spec, int index) => spec.Int(index) < 0 ? -1f : spec.Metres(index);
    }
}

using Dino_Engine.Modelling.Model;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Town
{
    /// <summary>
    /// Builds a piece of the Haven's town from its <see cref="TownSpec"/>, which is all there is of it: the same id makes the same piece on any machine. Each kind's numbers are
    /// listed with it in <see cref="TownKind"/>. Headless, and touches nothing shared, so the game builds them on worker threads while the Haven loads. Read HAVEN.md.
    /// </summary>
    public static class TownForge
    {
        /// <param name="chimneys">Where smoke leaves it, in its own frame, if it has fires.</param>
        public static Mesh Make(in TownSpec spec, List<AssetLight> lights, List<Vector3> chimneys)
        {
            var b = new MeshBuilder { Outward = true };
            switch (spec.Kind)
            {
                case TownKind.House:
                    TownHouse.Build(b, spec, lights, chimneys);
                    break;
                case TownKind.Stepping:
                    TownPieces.Stepping(b, spec.Metres(0), spec.Metres(1), spec.Seed);
                    break;
                case TownKind.Stall:
                    TownPieces.Stall(b, spec.Metres(0), (StallTrade)spec.Int(1), spec.Int(2), spec.Seed, lights);
                    break;
                case TownKind.Bed:
                    TownPieces.Bed(b, spec.Metres(0), spec.Metres(1), (BedCrop)spec.Int(2), spec.Seed);
                    break;
                case TownKind.Sign:
                    TownPieces.Sign(b, (SignEmblem)spec.Int(0), spec.Metres(1), spec.Seed);
                    break;
                case TownKind.Banner:
                    TownPieces.Banner(b, spec.Metres(0), spec.Int(1), spec.Seed);
                    break;
                case TownKind.Trellis:
                    TownPieces.Trellis(b, spec.Metres(0), spec.Metres(1), spec.Int(2), spec.Seed);
                    break;
                case TownKind.Yardwork:
                    TownPieces.Yardwork(b, (YardTrade)spec.Int(0), spec.Seed);
                    break;
                case TownKind.Bench:
                    TownPieces.Bench(b, spec.Metres(0), spec.Seed);
                    break;
                case TownKind.Lamp:
                    TownPieces.Lamp(b, spec.Metres(0), spec.Seed, lights);
                    break;
                case TownKind.Trough:
                    TownPieces.Trough(b, spec.Metres(0), spec.Seed);
                    break;
                case TownKind.Memorial:
                    TownPieces.Memorial(b, spec.Metres(0), Math.Max(1, spec.Int(1)), spec.Seed, lights);
                    break;
                case TownKind.Edging:
                    TownPieces.Edging(b, spec.Metres(0), spec.Seed);
                    break;
                case TownKind.Canopy:
                    TownPieces.Canopy(b, spec.Metres(0), spec.Metres(1), spec.Metres(2), spec.Int(3), spec.Seed);
                    break;
            }
            return b.Build();
        }

        /// <summary>Where along its front a house's door is, in the house's own frame, for whoever lays a way to it or checks that there is one.</summary>
        public static float DoorAt(in TownSpec spec) => TownHouse.DoorAt(spec);

        /// <summary>How long a piece takes to build, roughly, so that the slowest are begun first.</summary>
        public static int Slowness(TownKind kind) => kind == TownKind.House ? 2 : kind is TownKind.Stall or TownKind.Memorial or TownKind.Trellis ? 1 : 0;
    }
}

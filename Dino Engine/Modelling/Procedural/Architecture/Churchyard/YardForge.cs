using Dino_Engine.Modelling.Model;
using Dino_Engine.World.Content;

namespace Dino_Engine.Modelling.Procedural.Architecture.Churchyard
{
    /// <summary>
    /// Builds a piece of a churchyard from its <see cref="YardSpec"/>, which is all there is of it: the same id makes the same piece on any machine. Each kind's numbers are listed with
    /// it in <see cref="YardKind"/>. Headless, and touches nothing shared, so it runs on worker threads while a world loads. Read CHURCHYARD.md.
    /// </summary>
    public static class YardForge
    {
        public static Mesh Make(in YardSpec spec, List<AssetLight> lights)
        {
            return spec.Kind switch
            {
                YardKind.Church => ChurchForge.Make(spec, lights),
                YardKind.Stone => Graves.Stone(spec),
                YardKind.Tomb => Graves.Tomb(spec),
                YardKind.Dug => Graves.Dug(spec),
                YardKind.Candles => Graves.Candles(spec, lights),
                YardKind.Bench => Graves.Bench(spec),
                YardKind.Mausoleum => YardPieces.Mausoleum(spec, lights),
                YardKind.Railing => YardPieces.Railing(spec),
                YardKind.Lamp => YardPieces.Lamp(spec, lights),
                YardKind.Cross => YardPieces.Cross(spec),
                YardKind.Gate => YardPieces.Gate(spec, lights),
                _ => new MeshBuilder().Build(),
            };
        }

        /// <summary>How long a grave is, stone and all, and how wide.</summary>
        public static float GraveLength => Graves.Length;
        public static float GraveWidth => Graves.Width;

        /// <summary>A chest tomb's width, height and length.</summary>
        public static OpenTK.Mathematics.Vector3 TombSize(uint seed) => Graves.TombSize(seed);

        /// <summary>A mausoleum's half width, height and half depth.</summary>
        public static OpenTK.Mathematics.Vector3 MausoleumSize(uint seed, MausoleumForm form) => YardPieces.MausoleumSize(seed, form);

        /// <summary>How far a gate's solid parts reach either side of the way through and how deep they are, and how wide each side is.</summary>
        public static OpenTK.Mathematics.Vector2 GateReach(GateForm form, float width) => YardPieces.GateReach(form, width);
        public static float GateSide(GateForm form) => YardPieces.GateSide(form);
    }
}

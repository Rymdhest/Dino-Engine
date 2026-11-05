using System.Diagnostics;

namespace Dino_Engine.World.Generation
{
    /// <summary>One step of world generation. Stages run in order and read what earlier stages left in the <see cref="WorldData"/>.</summary>
    public interface IWorldStage
    {
        /// <summary>Shown while the world loads.</summary>
        string Description { get; }
        /// <summary>Rough share of the generation time, for the progress bar.</summary>
        float Cost { get; }
        void Run(WorldData world);
    }

    /// <summary>
    /// Builds worlds by running a list of stages over a <see cref="WorldData"/>. Nothing in a stage depends on the GPU or on
    /// global state: a request always gives the same world, wherever and on whichever thread it runs.
    /// </summary>
    public sealed class WorldGenerator
    {
        private readonly List<IWorldStage> _stages;

        public WorldGenerator() : this(DefaultStages())
        {
        }

        public WorldGenerator(IEnumerable<IWorldStage> stages)
        {
            _stages = new List<IWorldStage>(stages);
        }

        public static List<IWorldStage> DefaultStages()
        {
            return new List<IWorldStage>
            {
                new LandformStage(),
                new ErosionStage(),
                new ClimateStage(),
                new BiomeStage(),
                new SiteStage(),
                new RoadStage(),
                new SettlementStage(),
                new CampStage(),
                new TerrainBakeStage(),
                new WaterStage(),
                new VegetationStage(),
                new FinalizeStage(),
                new PackStage(),
            };
        }

        /// <param name="progress">Told how far along generation is, 0..1, and what it is doing. Called on the generating thread.</param>
        public WorldData Generate(WorldRequest request, Action<float, string>? progress = null)
        {
            var world = new WorldData(WorldPlan.Create(request));

            float totalCost = 0f;
            foreach (IWorldStage stage in _stages) totalCost += stage.Cost;

            float done = 0f;
            var clock = new Stopwatch();
            foreach (IWorldStage stage in _stages)
            {
                progress?.Invoke(done / totalCost, stage.Description);
                clock.Restart();
                stage.Run(world);
                world.Timings.Add((stage.GetType().Name, clock.Elapsed.TotalMilliseconds));
                done += stage.Cost;
            }
            progress?.Invoke(1f, "Ready");
            return world;
        }
    }
}

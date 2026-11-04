using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Procedural.Terrain;
using Dino_Engine.Physics;
using Dino_Engine.Rendering;
using Dino_Engine.Util;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.World
{
    /// <summary>
    /// Sets up the ECS for a generated world: clears out the old one, registers the singletons the engine's systems run on,
    /// and creates the light and water. What lives in the world (a player, its cameras) is up to the game.
    /// </summary>
    public static class WorldScene
    {
        public const float ChunkSize = 8f;
        public const int ChunkRadius = 8;
        public const float LongestGrass = 2.6f;

        public static void Enter(ECSWorld ecs, WorldData world, WorldAssetLibrary assets)
        {
            ecs.DirtyEntitiesBuffers.ClearAll();
            ecs.DirtyEntities.Clear();
            ecs.ClearAllEntitiesExcept();

            WorldMood mood = world.Plan.Mood;
            var generator = new TerrainGenerator(world);
            Engine.RenderEngine._terrainRenderer.SetSurfacePalette(generator.Palette);

            ecs.RegisterSingleton<TerrainQuadTreeSingleton>(ecs.CreateEntity(new TerrainQuadTreeSingleton(ChunkSize, ChunkRadius)));
            ecs.RegisterSingleton<TerrainGeneratorSingleton>(ecs.CreateEntity(new TerrainGeneratorSingleton(generator)));
            ecs.RegisterSingleton<CollisionEventBufferSingleton>(ecs.CreateEntity(new CollisionEventBufferSingleton()));
            ecs.RegisterSingleton<RenderSpatialGridSingleton>(ecs.CreateEntity(new RenderSpatialGridSingleton(15f)));
            ecs.RegisterSingleton<WorldStreamSingleton>(ecs.CreateEntity(new WorldStreamSingleton(world, assets)));
            ecs.RegisterSingleton<WorldDataSingleton>(ecs.CreateEntity(new WorldDataSingleton(world)));
            ecs.RegisterSingleton<WindSingleton>(ecs.CreateEntity("Wind", new WindSingleton { Speed = mood.WindSpeed, Direction = mood.WindDirection }));
            ecs.RegisterSingleton<GrassSingleton>(ecs.CreateEntity("Grass", new GrassSingleton { Height = LongestGrass }));
            ecs.RegisterSingleton<VolumetricFogSingleton>(ecs.CreateEntity("Volumetric Fog", CreateFog(mood)));
            ecs.RegisterSingleton<WaterSingleton>(ecs.CreateEntity("Water", CreateWater(world)));
            ecs.RegisterSingleton<NightfallSingleton>(ecs.CreateEntity("Nightfall", new NightfallSingleton()));
            ecs.ApplyDeferredCommands();

            ecs.sun = ecs.CreateEntity("Sun",
                new DirectionalLightTag(),
                new DirectionNormalizedComponent(mood.SunDirection),
                new ColorComponent(new Colour(mood.SunColour.X, mood.SunColour.Y, mood.SunColour.Z, mood.SunIntensity)),
                new DirectionalCascadingShadowComponent(new Vector2i(1024, 1024) * 4, 1, 150),
                new CelestialBodyComponent());

            ecs.CreateEntity("Sky",
                new ColorComponent(new Colour(mood.SkyColour.X, mood.SkyColour.Y, mood.SkyColour.Z, mood.SkyIntensity)),
                new SkyTag());

            foreach (WorldPond pond in world.Ponds)
            {
                ecs.CreateEntity("Water Body",
                    new PositionComponent(new Vector3(pond.Centre.X, pond.Level, pond.Centre.Y)),
                    WaterBodyComponent.Pond(pond.HalfExtents, pond.Area > 150f ? WaterOptics.Lake : WaterOptics.Pond));
            }
        }

        private static VolumetricFogSingleton CreateFog(WorldMood mood)
        {
            var fog = new VolumetricFogSingleton();
            fog.Density = mood.FogDensity;
            fog.Height = mood.FogHeight;
            fog.Albedo = mood.FogColour;
            return fog;
        }

        private static WaterSingleton CreateWater(WorldData world)
        {
            var water = new WaterSingleton { SeaLevel = WorldPlan.SeaLevel };
            if (world.Plan.Archetype.StillWater)
            {
                // Inland water: no swell to run up the shore, and the clearer look of a lake.
                water.ShoreWaveHeight = 0.04f;
                water.WindWaveHeight = 0.08f;
                water.Foam = 0.2f;
                water.Whitecaps = 0f;
                water.OceanOptics = WaterOptics.Lake;
            }
            return water;
        }
    }
}

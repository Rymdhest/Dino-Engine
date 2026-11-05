using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.World.Generation.SiteBuilder;

namespace Dino_Engine.World.Generation
{
    /// <summary>
    /// Pitches the small places that aren't on any map: an outlaw's fire and tents, a patch of forgotten graves, a ring of stones someone keeps lit, a den
    /// among the rubble. They come before the ground is baked and the trees planted, so that the earth is worn around them and the woods leave room. Who stays
    /// there is settled later by <see cref="PackStage"/>, once it's known where a character can stand.
    /// </summary>
    public sealed class CampStage : IWorldStage
    {
        private const float CampsPerSquareKilometre = 3.4f;
        private const float LeastRemoteness = 0.07f;
        private const float CampSpacing = 170f;
        private const float SiteMargin = 46f;
        private const float MinInland = 12f;
        private const float SteepestCamp = 0.34f;

        public string Description => "Pitching camps";
        public float Cost => 0.2f;

        public void Run(WorldData world)
        {
            WorldRandom random = world.Random.Fork("camps");
            var territory = new Territory(world);
            int wanted = (int)(world.ReachableArea / 1_000_000f * CampsPerSquareKilometre * world.Plan.Archetype.Dangers);

            for (int attempt = 0; attempt < wanted * 80 && world.Haunts.Count < wanted; attempt++)
            {
                var spot = new Vector2(random.Range(0f, world.Size), random.Range(0f, world.Size));
                float radius = random.Range(13f, 18f);
                if (!Suits(world, spot, radius)) continue;

                var camp = new WorldSite
                {
                    Id = -1,
                    Definition = SiteCatalog.Haunt,
                    Haunt = HauntKind.Camp,
                    Name = "Camp",
                    Position = spot,
                    Radius = radius,
                    Remoteness = world.Remoteness.Sample(spot),
                    Biome = world.DominantBiome(spot),
                };

                WorldRandom campRandom = random.Fork(world.Haunts.Count);
                SpawnRole folk = territory.FolkAt(spot, campRandom.Fork("folk"));
                var builder = new SiteBuilder(world, camp, campRandom);
                if (!builder.GroundFits(Footprint.Circle(spot, radius * 0.8f), SteepestCamp)) continue;

                camp.Folk = folk;
                world.Clearings.Add(new Clearing(spot, radius * 1.15f, 20f));
                switch (folk)
                {
                    case SpawnRole.Undead: BuildGraves(builder); break;
                    case SpawnRole.Beast: BuildDen(builder); break;
                    case SpawnRole.Cultist: BuildCircle(builder); break;
                    default: BuildFire(builder); break;
                }
                world.Haunts.Add(camp);
            }
        }

        private static bool Suits(WorldData world, Vector2 spot, float radius)
        {
            if (world.Reachable.Sample(spot) < 0.5f || world.Inland.Sample(spot) < MinInland) return false;
            if (world.Remoteness.Sample(spot) < LeastRemoteness) return false;

            foreach (WorldSite site in world.Sites)
            {
                if ((site.Position - spot).Length < site.Radius + radius + SiteMargin) return false;
            }
            foreach (WorldSite camp in world.Haunts)
            {
                if ((camp.Position - spot).Length < CampSpacing) return false;
            }
            return !world.Placement.Overlaps(Footprint.Circle(spot, radius + 3f), PlacementLayer.Road | PlacementLayer.Structure | PlacementLayer.Fence);
        }

        private static void BuildFire(SiteBuilder b)
        {
            float radius = b.Site.Radius;
            b.Paint(GroundPaint.Yard, Footprint.Circle(b.Centre, radius * 0.72f), 8f);
            b.Place(StructureCatalog.Campfire, b.Centre, 0f);

            int tents = b.Random.Int(2, 3);
            float first = b.Random.Float() * MathF.Tau;
            for (int i = 0; i < tents; i++)
            {
                float angle = first + i * (MathF.Tau / tents) + b.Random.Range(-0.35f, 0.35f);
                Vector2 spot = b.Centre + Direction(angle) * radius * 0.62f;
                b.Place(StructureCatalog.Tent, spot, YawFacing(b.Centre - spot), padding: 0.6f);
            }

            b.Scatter(StructureCatalog.LogStack, b.Centre, radius * 0.8f);
            for (int i = 0; i < 2; i++)
            {
                b.Scatter(StructureCatalog.Crates, b.Centre, radius * 0.8f);
                b.Scatter(StructureCatalog.Barrels, b.Centre, radius * 0.8f);
            }
        }

        private static void BuildGraves(SiteBuilder b)
        {
            float radius = b.Site.Radius;
            int graves = b.Random.Int(5, 8);
            float first = b.Random.Float() * MathF.Tau;

            // Wide apart, as what's left of a burying ground is: a character is broad, and has to get between them.
            for (int i = 0; i < graves; i++)
            {
                float angle = first + i * (MathF.Tau / graves) + b.Random.Range(-0.2f, 0.2f);
                Vector2 spot = b.Centre + Direction(angle) * radius * b.Random.Range(0.55f, 0.85f);
                b.Place(StructureCatalog.Grave, spot, b.Random.Float() * MathF.Tau, padding: 0.3f);
            }

            b.Scatter(StructureCatalog.Tomb, b.Centre, radius * 0.5f, attempts: 10, padding: 0.8f);
            for (int i = 0; i < 3; i++)
            {
                Vector2 spot = b.Centre + Direction(b.Random.Float() * MathF.Tau) * radius * b.Random.Range(0.7f, 1f);
                b.Place(StructureCatalog.DeadTree, spot, b.Random.Float() * MathF.Tau, 0.6f * b.Random.Range(0.8f, 1.25f), padding: 0.6f);
            }
            for (int i = 0; i < 3; i++) b.Scatter(StructureCatalog.Candles, b.Centre, radius * 0.7f, padding: 0f);
        }

        private static void BuildDen(SiteBuilder b)
        {
            float radius = b.Site.Radius;
            for (int i = 0; i < 6; i++) b.Scatter(StructureCatalog.Rubble, b.Centre, radius * 0.9f);
            for (int i = 0; i < 3; i++) b.Scatter(StructureCatalog.Cairn, b.Centre, radius * 0.85f, padding: 0.8f);
            for (int i = 0; i < 2; i++)
            {
                Vector2 spot = b.Centre + Direction(b.Random.Float() * MathF.Tau) * radius * b.Random.Range(0.6f, 1f);
                b.Place(StructureCatalog.DeadTree, spot, b.Random.Float() * MathF.Tau, 0.7f * b.Random.Range(0.8f, 1.2f), padding: 0.6f);
            }
        }

        private static void BuildCircle(SiteBuilder b)
        {
            float radius = b.Site.Radius;
            b.Paint(GroundPaint.Yard, Footprint.Circle(b.Centre, radius * 0.55f), 8f);
            b.Place(StructureCatalog.Campfire, b.Centre, 0f);

            int stones = b.Random.Int(5, 7);
            float first = b.Random.Float() * MathF.Tau;
            for (int i = 0; i < stones; i++)
            {
                Vector2 spot = b.Centre + Direction(first + i * (MathF.Tau / stones)) * radius * 0.78f;
                b.Place(StructureCatalog.Monolith, spot, YawFacing(b.Centre - spot), padding: 0.6f);
            }
            for (int i = 0; i < 4; i++) b.Scatter(StructureCatalog.Candles, b.Centre, radius * 0.5f, padding: 0f);
        }
    }
}

using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.World.WorldMath;

namespace Dino_Engine.World.Generation
{
    /// <summary>
    /// Who has made a part of the world theirs. Outlaws work the roads and spread out from their camps, the dead gather around old graves, beasts keep to the
    /// deep woods and the far side of nowhere, and those who serve the land's powers go where it's harshest and where the old stones stand. Each folk also
    /// has its own slow noise, which is what makes it a territory: a stretch of country belongs to someone, rather than every pack being drawn afresh.
    /// </summary>
    internal sealed class Territory
    {
        private const float TerritorySize = 340f;

        private static readonly SpawnRole[] Folk = { SpawnRole.Raider, SpawnRole.Undead, SpawnRole.Beast, SpawnRole.Cultist };

        private readonly WorldData _world;
        private readonly WorldNoise[] _claims;
        private readonly float[] _scores = new float[Folk.Length];

        public Territory(WorldData world)
        {
            _world = world;
            WorldRandom random = world.Random.Fork("territory");
            _claims = new WorldNoise[Folk.Length];
            for (int i = 0; i < Folk.Length; i++) _claims[i] = new WorldNoise(random.Fork(i));
        }

        /// <summary>Whose ground this is. <paramref name="random"/> only settles what's close.</summary>
        public SpawnRole FolkAt(Vector2 at, WorldRandom random)
        {
            float outlaws = 0.45f;
            float dead = 0.2f;
            float beasts = 0.5f;
            float cult = 0.2f;

            bool roadside = _world.Placement.Overlaps(Footprint.Circle(at, 32f), PlacementLayer.Road);
            bool nearRoad = roadside || _world.Placement.Overlaps(Footprint.Circle(at, 90f), PlacementLayer.Road);
            if (roadside) outlaws += 0.7f;
            else if (nearRoad) outlaws += 0.3f;
            else beasts += 0.45f;

            foreach (WorldSite site in _world.Sites)
            {
                float away = (site.Position - at).Length - site.Radius;
                switch (site.Kind)
                {
                    case SiteKind.BanditCamp:
                    case SiteKind.Keep:
                        outlaws += 1.3f * Reach(away, 240f);
                        break;
                    case SiteKind.Graveyard:
                        dead += 1.9f * Reach(away, 240f);
                        break;
                    case SiteKind.Ruins:
                        dead += 0.9f * Reach(away, 190f);
                        break;
                    case SiteKind.StandingStones:
                    case SiteKind.Shrine:
                        cult += 0.8f * Reach(away, 170f);
                        break;
                }
            }

            BiomeDefinition biome = _world.BiomeAt(at);
            if (IsHarsh(biome)) cult += 0.6f;

            // Before the trees are planted, there's no telling how thick they'll be.
            if (_world.Canopy != null) beasts += 1.1f * _world.Canopy.Sample(at);

            // Nobody's first steps should be among the things that need a build to deal with.
            float settled = Smoothstep(0.04f, 0.2f, _world.Remoteness.Sample(at));
            dead *= Lerp(0.3f, 1f, settled);
            cult *= settled;

            _scores[0] = outlaws;
            _scores[1] = dead;
            _scores[2] = beasts;
            _scores[3] = cult;

            int best = 0;
            float highest = float.NegativeInfinity;
            for (int i = 0; i < Folk.Length; i++)
            {
                float score = _scores[i] + _claims[i].Fbm(at.X, at.Y, TerritorySize, 2) * 0.85f + random.Float() * 0.15f;
                if (score <= highest) continue;
                highest = score;
                best = i;
            }
            return Folk[best];
        }

        private static float Reach(float away, float reach) => Saturate(1f - away / reach);

        private static bool IsHarsh(BiomeDefinition biome) => biome.Temperature <= 0.3f || biome.Moisture >= 0.75f || biome.Altitude >= 0.5f || (biome.Temperature >= 0.7f && biome.Moisture < 0.4f);
    }
}

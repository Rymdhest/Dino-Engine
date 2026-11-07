using Dino_Engine.Modelling.Procedural.Rock;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.World.WorldMath;

namespace Dino_Engine.World.Generation
{
    /// <summary>
    /// Sets real stone on the mountains, for what a height map can't do. It has one height at every point, so it has no overhangs, and at a metre to a node it has no edge finer than that.
    /// Crags and spires break the line of a ridge and stand out of a face, and what's fallen lies below it. All of it is on rock nobody goes to, so none of it needs to be solid.
    /// Crags stand along the same joints as the mountain, so the two read as one thing.
    /// </summary>
    public sealed class RockStage : IWorldStage
    {
        private const float CragSpacing = 13f;
        private const float FallenSpacing = 3.6f;
        private const float LeastRock = 0.7f;   // How bare the ground has to be for a crag to stand on it.
        private const float EdgeRock = 0.3f;    // And how bare all round it, or part of it would stand where someone could walk through it.

        public string Description => "Breaking the stone";
        public float Cost => 1f;

        public void Run(WorldData world)
        {
            WorldRandom random = world.Random.Fork("stones");
            var patches = new WorldNoise(random.Fork("patches"));
            PlaceCrags(world, random.Fork("crags"), patches);
            PlaceFallen(world, random.Fork("fallen"));
        }

        private static void PlaceCrags(WorldData world, WorldRandom random, WorldNoise patches)
        {
            RockKind kind = world.Geology.Kind;
            int steps = (int)(world.Size / CragSpacing);
            for (int z = 0; z < steps; z++)
            {
                for (int x = 0; x < steps; x++)
                {
                    Vector2 point = new Vector2(x + random.Float(), z + random.Float()) * CragSpacing;
                    float pick = random.Float();
                    float size = random.Float();
                    float turn = random.Float();
                    if (world.Rock.Sample(point) < LeastRock) continue;

                    // They stand in groups, with bare slopes between them, and most of all along the crests.
                    float crest = Smoothstep(0.1f, 0.7f, world.Crests.Sample(point));
                    float group = Smoothstep(0.42f, 0.68f, patches.Fbm01(point.X, point.Y, 70f, 3));
                    if (pick >= 0.08f + 0.5f * group + 0.3f * crest * group) continue;

                    RockForm form = (pick * 7.3f % 1f) < 0.25f + 0.3f * crest ? RockForm.Spire : (pick * 13.7f % 1f) < 0.6f ? RockForm.Crag : RockForm.Outcrop;
                    WorldAsset asset = NatureCatalog.RockOf(kind, form);
                    float scale = asset.Scale.Lerp(size * size);

                    float reach = asset.Footprint * scale;
                    if (!StandsOnRock(world, point, reach, out float lowest, out float highest)) continue;

                    // Set by the low side of the ground, or a slope would leave its foot in the air.
                    float height = Lerp(lowest, highest, 0.2f);
                    world.Objects.Add(NatureCatalog.ModelAt(asset, world.Plan.Seed, point), new Vector3(point.X, height, point.Y), AlongJoints(world.Geology, turn), new Vector3(scale));
                }
            }
        }

        private static bool StandsOnRock(WorldData world, Vector2 point, float reach, out float lowest, out float highest)
        {
            lowest = float.MaxValue;
            highest = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                float angle = i * (MathF.Tau / 8f);
                Vector2 at = point + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * reach;
                if (!world.InBounds(at) || world.Rock.Sample(at) < EdgeRock) return false;
                float ground = world.Height.Sample(at);
                lowest = MathF.Min(lowest, ground);
                highest = MathF.Max(highest, ground);
            }
            return true;
        }

        // A crag's joints are square to its own axes, so it's turned to lie along the world's. Beds that lean have to lean the same way.
        private static Quaternion AlongJoints(Geology rock, float turn)
        {
            float quarter = MathF.Floor(turn * 4f) * MathF.PI * 0.5f;
            float leaning = rock.Dip.LengthSquared > 0.04f ? -MathF.Atan2(rock.Dip.Y, rock.Dip.X) : -rock.Strike + quarter;
            return Quaternion.FromAxisAngle(Vector3.UnitY, leaning + (turn - 0.5f) * 0.2f);
        }

        // Whatever's come down off the rock, lying where it stopped: in the gullies and along the foot of the faces. Few large ones and many small.
        private static void PlaceFallen(WorldData world, WorldRandom random)
        {
            RockKind kind = world.Geology.Kind;
            int steps = (int)(world.Size / FallenSpacing);
            for (int z = 0; z < steps; z++)
            {
                for (int x = 0; x < steps; x++)
                {
                    Vector2 point = new Vector2(x + random.Float(), z + random.Float()) * FallenSpacing;
                    float pick = random.Float();
                    float size = random.Float();
                    float turn = random.Float();
                    float roll = random.Float();

                    float scree = world.Scree.Sample(point);
                    if (scree < 0.3f || pick >= scree * 0.34f) continue;
                    // Where someone can walk a stone has to be solid, and that's for the plants' stage to weigh up.
                    if (world.Rock.Sample(point) < 0.55f) continue;

                    RockForm form = roll < 0.45f ? RockForm.Boulder : roll < 0.85f ? RockForm.Block : RockForm.Slab;
                    WorldAsset asset = NatureCatalog.RockOf(kind, form);
                    float scale = asset.Scale.Lerp(size * size * size);
                    Quaternion rotation = Quaternion.FromAxisAngle(Vector3.UnitY, turn * MathF.Tau)
                        * Quaternion.FromAxisAngle(Vector3.UnitX, (roll - 0.5f) * 0.9f)
                        * Quaternion.FromAxisAngle(Vector3.UnitZ, (pick / MathF.Max(scree * 0.34f, 0.01f) - 0.5f) * 0.9f);
                    float height = world.Height.Sample(point) - 0.12f * scale;
                    world.Objects.Add(NatureCatalog.ModelAt(asset, world.Plan.Seed, point), new Vector3(point.X, height, point.Y), rotation, new Vector3(scale));
                }
            }
        }
    }
}

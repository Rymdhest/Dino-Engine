using Dino_Engine.Modelling.Procedural.Rock;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.World.WorldMath;

namespace Dino_Engine.World.Generation
{
    /// <summary>
    /// Sets real stone on the mountains, for what a height map can't do. It has one height at every point, so it has no overhangs, and at a metre to a node it has no edge finer than that.
    /// What stands where is the mountain's form (<see cref="MountainForm"/>): bluffs of stacked beds along the cliffs of a ledged mountain, spires and fins on jagged crests, pinnacles
    /// among towers, tors on domes, hoodoos in groups along a canyon's rim and an arch now and then, columns of basalt along its flows, and blades in rows where beds stand on end.
    /// What's fallen lies below. All of it is on rock nobody goes to, so none of it needs to be solid, and none of it may stand between the camera and anyone on the land beyond it.
    /// It all lines up with the mountain's own joints, so the two read as one thing.
    /// </summary>
    public sealed class RockStage : IWorldStage
    {
        private const float FormationSpacing = 9f;
        private const float FallenSpacing = 3.6f;
        private const float LeastRock = 0.7f;   // How bare the ground has to be for a formation to stand on it.
        private const float EdgeRock = 0.3f;    // And how bare all round it, or part of it would stand where someone could walk through it.
        private const float Sunk = 0.3f;        // Metres a formation's foot goes into the ground, so that no edge of it hangs in the air.

        // The game's camera, as WorldValidator measures it: what a formation's top must stay under, seen from anyone it could stand in front of.
        private static readonly float CameraRise = MathF.Tan(MathF.PI / 2.7f);
        private const float CameraReach = 14f;
        private const float HeroWaist = 1f;
        private const float SightMargin = 0.5f;

        public string Description => "Breaking the stone";
        public float Cost => 1f;

        public void Run(WorldData world)
        {
            WorldRandom random = world.Random.Fork("stones");
            var patches = new WorldNoise(random.Fork("patches"));
            var placed = new SpacingGrid(world.Size);
            PlaceFormations(world, random.Fork("formations"), patches, placed);
            PlaceFallen(world, random.Fork("fallen"));
        }

        // A kind of stone and how likely it is to stand at a point, by the form of the mountain there and the lie of the ground: a cliff band, a crest, a ledge or a floor.
        private static (RockForm form, float chance) Choose(MountainForm form, float slope, float crest, float pick)
        {
            bool steep = slope > 0.9f && slope < 4.5f;
            switch (form)
            {
                case MountainForm.Ledged:
                    if (steep) return (RockForm.Bluff, 0.34f);
                    if (crest > 0.3f) return (RockForm.Crag, 0.12f);
                    return slope < 0.7f ? (RockForm.Outcrop, 0.16f) : (RockForm.Crag, 0.06f);
                case MountainForm.Jagged:
                    if (crest > 0.6f && pick < 0.3f) return (RockForm.Fin, 0.4f);
                    if (crest > 0.35f) return (pick < 0.55f ? RockForm.Spire : RockForm.Crag, 0.42f);
                    return (RockForm.Crag, steep ? 0.16f : 0.06f);
                case MountainForm.Towers:
                    return (pick < 0.8f ? RockForm.Spire : RockForm.Crag, 0.3f);
                case MountainForm.Domes:
                    if (slope < 1.4f) return (RockForm.Tor, crest > 0.2f ? 0.4f : 0.14f);
                    return (RockForm.Outcrop, 0.06f);
                case MountainForm.Canyons:
                    if (steep) return (pick < 0.85f ? RockForm.Bluff : RockForm.Arch, 0.24f);
                    if (crest > 0.2f && pick < 0.06f) return (RockForm.Arch, 0.5f);
                    return (pick < 0.85f ? RockForm.Hoodoo : RockForm.Fin, 0.24f);
                case MountainForm.Columns:
                    return (pick < 0.85f ? RockForm.Columns : RockForm.Bluff, steep ? 0.4f : 0.13f);
                default:
                    return (pick < 0.85f ? RockForm.Fin : RockForm.Crag, 0.36f);
            }
        }

        private static void PlaceFormations(WorldData world, WorldRandom random, WorldNoise patches, SpacingGrid placed)
        {
            Geology rock = world.Geology;
            RockKind kind = rock.Kind;
            int steps = (int)(world.Size / FormationSpacing);
            for (int z = 0; z < steps; z++)
            {
                for (int x = 0; x < steps; x++)
                {
                    Vector2 point = new Vector2(x + random.Float(), z + random.Float()) * FormationSpacing;
                    float pick = random.Float();
                    float size = random.Float();
                    float turn = random.Float();
                    float shape = random.Float();
                    if (!world.InBounds(point) || world.Rock.Sample(point) < LeastRock) continue;

                    FormBlend blend = MountainForms.At(world, point.X, point.Y);
                    MountainForm mountain = Draw(blend, shape);
                    float slope = world.Height.SlopeAt(point);
                    float crest = world.Crests.Sample(point);
                    // They stand in groups, with bare stretches between them.
                    float group = Smoothstep(0.36f, 0.64f, patches.Fbm01(point.X, point.Y, 70f, 3));
                    (RockForm form, float chance) = Choose(mountain, slope, crest, pick * 7.3f % 1f);
                    if (pick >= chance * (0.35f + 0.65f * group) * blend[mountain] * 1.6f) continue;

                    if (TryStand(world, kind, form, point, size * size, turn, placed) && form == RockForm.Hoodoo)
                    {
                        // Hoodoos stand in companies along a rim, smaller round the first.
                        int company = 2 + (int)(turn * 5f);
                        for (int i = 0; i < company; i++)
                        {
                            float angle = random.Float() * MathF.Tau;
                            float out_ = random.Range(3f, 8f);
                            Vector2 at = point + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * out_;
                            if (!world.InBounds(at) || world.Rock.Sample(at) < LeastRock) continue;
                            TryStand(world, kind, RockForm.Hoodoo, at, random.Float() * 0.55f, random.Float(), placed);
                        }
                    }
                }
            }
        }

        // One of the forms at a point, drawn by how much of it each has, so that across a border between two the stones mingle.
        private static MountainForm Draw(in FormBlend blend, float pick)
        {
            for (int i = 0; i < MountainForms.Count; i++)
            {
                pick -= blend[(MountainForm)i];
                if (pick <= 0f) return (MountainForm)i;
            }
            return blend.Strongest;
        }

        // Stands a formation at a point if it fits there: all of it on bare rock, clear of the others, and under the line from the camera to anyone on the land beyond it. If it's
        // too tall for that, it's tried smaller, and left out if even that won't do.
        private static bool TryStand(WorldData world, RockKind kind, RockForm form, Vector2 point, float size, float turn, SpacingGrid placed)
        {
            WorldAsset asset = NatureCatalog.RockOf(kind, form);
            for (int attempt = 0; attempt < 3; attempt++)
            {
                float scale = asset.Scale.Lerp(size) * (attempt == 0 ? 1f : attempt == 1 ? 0.75f : 0.55f);
                if (scale < asset.Scale.Min * 0.5f) return false;
                float reach = asset.Footprint * scale;
                if (!StandsOnRock(world, point, reach, out float lowest, out float highest)) continue;
                if (placed.Crowded(point, reach * 0.8f)) return false;

                Quaternion rotation = Facing(world, form, point, turn, out Vector2 downhill);
                float height;
                if (form == RockForm.Bluff)
                {
                    // A stretch of cliff has its back in the slope and its face looking down it, its foot where the slope runs out in front.
                    if (downhill == Vector2.Zero) return false;
                    Vector2 front = point + downhill * (asset.Footprint * 0.45f * scale);
                    height = world.Height.Sample(front) - Sunk;
                }
                else
                {
                    // Set by the low side of the ground, or a slope would leave its foot in the air.
                    height = Lerp(lowest, highest, form is RockForm.Tor or RockForm.Columns ? 0.1f : 0.18f) - Sunk;
                }

                float top = height + RockForge.TopOf(form) * scale;
                if (!HidesNoOne(world, point, reach, top)) continue;

                world.Objects.Add(NatureCatalog.ModelAt(asset, world.Plan.Seed, point), new Vector3(point.X, height, point.Y), rotation, new Vector3(scale));
                placed.Add(point, reach * 0.8f);
                return true;
            }
            return false;
        }

        // Which way a formation faces. A bluff looks down its slope, a fin or an arch stands along the strike as joints do, and anything else squares up to the joints.
        private static Quaternion Facing(WorldData world, RockForm form, Vector2 point, float turn, out Vector2 downhill)
        {
            Geology rock = world.Geology;
            downhill = Vector2.Zero;
            switch (form)
            {
                case RockForm.Bluff:
                {
                    const float Reach = 3f;
                    float dx = world.Height.Sample(point + new Vector2(Reach, 0f)) - world.Height.Sample(point - new Vector2(Reach, 0f));
                    float dz = world.Height.Sample(point + new Vector2(0f, Reach)) - world.Height.Sample(point - new Vector2(0f, Reach));
                    Vector2 down = new Vector2(-dx, -dz);
                    if (down.LengthSquared < 1e-6f) return Quaternion.Identity;
                    downhill = down.Normalized();
                    return Quaternion.FromAxisAngle(Vector3.UnitY, MathF.Atan2(downhill.X, downhill.Y) + (turn - 0.5f) * 0.2f);
                }
                case RockForm.Fin:
                case RockForm.Arch:
                    return Quaternion.FromAxisAngle(Vector3.UnitY, MathF.PI * 0.5f - rock.Strike + (turn - 0.5f) * 0.25f + (turn > 0.5f ? MathF.PI : 0f));
                default:
                    return AlongJoints(rock, turn);
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

        /// <summary>
        /// Whether something standing at a point, reaching that far round it and as tall as <paramref name="top"/>, stays under the line from the camera to the middle of anyone who
        /// could stand on the land north of it. The camera only looks along +z, so nobody south of it can be hidden by it, and nobody further north than the camera's reach. What can
        /// be walked isn't known yet (FinalizeStage), so this goes by everything that could be: the ground's own rules for it, before it's flooded out from the landing.
        /// </summary>
        public static bool HidesNoOne(WorldData world, Vector2 point, float reach, float top)
        {
            // Walkable ground is judged at the nodes of a grid of its own and holds for the square round each (ByteField.IsSet), so it's every node near enough that's tried,
            // and every corner of its square.
            const float Cell = WorldData.WalkCell;
            float north = point.Y + reach;
            int x0 = (int)MathF.Floor((point.X - reach) / Cell) - 1, x1 = (int)MathF.Ceiling((point.X + reach) / Cell) + 1;
            int z0 = (int)MathF.Floor((point.Y - reach) / Cell) - 1, z1 = (int)MathF.Ceiling((north + CameraReach) / Cell) + 1;
            for (int nz = z0; nz <= z1; nz++)
            {
                for (int nx = x0; nx <= x1; nx++)
                {
                    Vector2 node = new Vector2(nx, nz) * Cell;
                    if (!world.InBounds(node) || !CouldStand(world, node)) continue;
                    for (float dz = -0.5f; dz <= 0.5f; dz += 0.5f)
                    {
                        for (float dx = -0.5f; dx <= 0.5f; dx += 0.5f)
                        {
                            Vector2 at = node + new Vector2(dx, dz) * Cell;
                            float line = world.Height.Sample(at) + HeroWaist + CameraRise * MathF.Max(at.Y - north, 0f);
                            if (top > line - SightMargin) return false;
                        }
                    }
                }
            }
            return true;
        }

        // FinalizeStage.BuildWalkable's rules for ground someone could stand on, short of being reachable, which isn't known yet.
        private static bool CouldStand(WorldData world, Vector2 at)
        {
            return world.WaterDepthAt(at) <= WorldData.WadingDepth && world.Height.SlopeAt(at) <= WorldData.WalkableSlope && world.Boundary.Sample(at) < 0.6f && world.Rock.Sample(at) < 0.5f;
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
                    if (!HidesNoOne(world, point, asset.Footprint * scale, height + RockForge.TopOf(form) * scale)) continue;
                    world.Objects.Add(NatureCatalog.ModelAt(asset, world.Plan.Seed, point), new Vector3(point.X, height, point.Y), rotation, new Vector3(scale));
                }
            }
        }

        /// <summary>Where formations already stand, on a coarse grid, so that two don't grow through each other.</summary>
        private sealed class SpacingGrid
        {
            private const float Cell = 8f;
            private readonly int _width;
            private readonly List<(Vector2 at, float reach)>?[] _cells;

            public SpacingGrid(float size)
            {
                _width = Math.Max((int)MathF.Ceiling(size / Cell), 1);
                _cells = new List<(Vector2, float)>?[_width * _width];
            }

            public bool Crowded(Vector2 at, float reach)
            {
                int span = (int)MathF.Ceiling((reach + 16f) / Cell);
                int cx = (int)(at.X / Cell), cz = (int)(at.Y / Cell);
                for (int z = Math.Max(cz - span, 0); z <= Math.Min(cz + span, _width - 1); z++)
                {
                    for (int x = Math.Max(cx - span, 0); x <= Math.Min(cx + span, _width - 1); x++)
                    {
                        if (_cells[z * _width + x] is not List<(Vector2 at, float reach)> list) continue;
                        foreach ((Vector2 other, float otherReach) in list)
                        {
                            if ((other - at).Length < reach + otherReach) return true;
                        }
                    }
                }
                return false;
            }

            public void Add(Vector2 at, float reach)
            {
                int x = Math.Clamp((int)(at.X / Cell), 0, _width - 1);
                int z = Math.Clamp((int)(at.Y / Cell), 0, _width - 1);
                (_cells[z * _width + x] ??= new List<(Vector2, float)>()).Add((at, reach));
            }
        }
    }
}

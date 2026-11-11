using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.World.WorldMath;

namespace Dino_Engine.World.Generation
{
    /// <summary>
    /// Bakes the final ground at one metre: the shaped land, smoothly upsampled with fine detail on top, then cut and filled for
    /// the roads and levelled under buildings. After this every height, road and ground paint query is a single map lookup, which
    /// is what lets the game ask them thousands of times a frame.
    /// </summary>
    public sealed class TerrainBakeStage : IWorldStage
    {
        public string Description => "Cutting the roads";
        public float Cost => 3f;

        public void Run(WorldData world)
        {
            float size = world.Size;
            ScalarField height = ScalarField.ForWorld(size, WorldData.DetailCell);
            var detail = new WorldNoise(world.Random.Fork("detail"));
            ScalarField elevation = world.Elevation;
            world.Rock = ByteField.ForWorld(size, WorldData.DetailCell);
            world.Scree = ByteField.ForWorld(size, WorldData.DetailCell);
            world.Proud = ByteField.ForWorld(size, WorldData.DetailCell);
            var sculpt = new RockSculpt(world, world.Random.Fork("sculpt"));

            height.Generate((x, z) =>
            {
                float h = elevation.SampleCubic(x, z);
                // Beaches and shallows stay smooth, or the shoreline would fray into little pools.
                float land = Smoothstep(0.3f, 3.5f, h);
                float rolls = detail.Fbm(x, z, 34f, 3) * 0.6f;
                float bumps = detail.Fbm(x + 53f, z - 91f, 7.5f, 3) * 0.13f;
                return sculpt.Apply((int)(x / WorldData.DetailCell), (int)(z / WorldData.DetailCell), x, z, h + (rolls + bumps) * land);
            });

            sculpt.Break(height);

            world.Height = height;
            world.PavedRoad = ByteField.ForWorld(size, WorldData.DetailCell);
            world.TrailRoad = ByteField.ForWorld(size, WorldData.DetailCell);
            world.Yard = ByteField.ForWorld(size, WorldData.DetailCell);
            world.Plaza = ByteField.ForWorld(size, WorldData.DetailCell);
            world.Farmland = ByteField.ForWorld(size, WorldData.DetailCell);
            world.Paving = ByteField.ForWorld(size, WorldData.DetailCell);
            world.Garden = ByteField.ForWorld(size, WorldData.DetailCell);
            world.Sand = ByteField.ForWorld(size, WorldData.DetailCell);
            world.Mud = ByteField.ForWorld(size, WorldData.DetailCell);
            world.Ash = ByteField.ForWorld(size, WorldData.DetailCell);

            ApplyPads(world, world.Pads, height);
            StampRoads(world, height, detail);
            ApplyPads(world, world.Terraces, height);
            foreach (PaintPatch patch in world.Paint) ApplyPaint(world, patch);
            sculpt.KeepHeroInSight(height);
            LevelHalls(world, height);
            sculpt.MeasureRelief(height);
        }

        // Every hall's ground exactly at its floor, wherever its walls stand, last of all: people stand on this ground and its floor is laid just over it (HALLS.md), and a
        // street's shoulder, stamped after the pads, once lifted a corner of the Archive 9 cm through its floor. Its walls go well below it, so the edge never shows.
        private static void LevelHalls(WorldData world, ScalarField height)
        {
            foreach (WorldHall hall in world.Halls)
            {
                Footprint shape = hall.Footprint;
                ForEachNode(height, shape.Centre, shape.BoundingRadius, (x, z, position) =>
                {
                    if (DistanceOutside(shape, position) > 0f) return;
                    height.Data[height.Index(x, z)] = hall.Floor;
                });
            }
        }

        private static void ApplyPads(WorldData world, List<GroundPad> pads, ScalarField height)
        {
            foreach (GroundPad pad in pads)
            {
                Footprint shape = pad.Footprint;
                float reach = shape.BoundingRadius + pad.Margin;
                ForEachNode(height, shape.Centre, reach, (x, z, position) =>
                {
                    float outside = DistanceOutside(shape, position);
                    float weight = 1f - Smoothstep(0f, pad.Margin, outside);
                    if (weight <= 0f) return;
                    int i = height.Index(x, z);
                    height.Data[i] = Lerp(height.Data[i], pad.HeightAt(position), weight);
                });
            }
        }

        // Metres from a point to the edge of a footprint, 0 inside it.
        private static float DistanceOutside(in Footprint shape, Vector2 point)
        {
            if (shape.IsCircle) return MathF.Max((point - shape.Centre).Length - shape.HalfExtents.X, 0f);
            Vector2 local = shape.ToLocal(point);
            float dx = MathF.Max(MathF.Abs(local.X) - shape.HalfExtents.X, 0f);
            float dz = MathF.Max(MathF.Abs(local.Y) - shape.HalfExtents.Y, 0f);
            return MathF.Sqrt(dx * dx + dz * dz);
        }

        private static void StampRoads(WorldData world, ScalarField height, WorldNoise noise)
        {
            int nodes = height.Data.Length;
            float[] carve = new float[nodes];
            float[] totalHeight = new float[nodes];
            float[] totalWeight = new float[nodes];

            foreach (WorldRoad road in world.Roads)
            {
                bool highway = road.Class == RoadClass.Highway;
                float reach = road.HalfWidth + road.Shoulder;

                for (int i = 1; i < road.Points.Count; i++)
                {
                    Vector3 from = road.Points[i - 1];
                    Vector3 to = road.Points[i];
                    Vector2 middle = (from.Xz + to.Xz) * 0.5f;
                    float segment = (to.Xz - from.Xz).Length * 0.5f;

                    ForEachNode(height, middle, reach + segment, (x, z, position) =>
                    {
                        float distance = DistanceToSegment(position, from.Xz, to.Xz, out float t);
                        if (distance > reach) return;

                        float weight = 1f - Smoothstep(road.HalfWidth, reach, distance);
                        int node = height.Index(x, z);
                        carve[node] = MathF.Max(carve[node], weight);

                        // Where roads meet or run side by side the ground takes a blend of them, weighted towards the
                        // nearest, so that one road handing over to the next leaves no step.
                        float share = weight * weight * weight + 1e-4f;
                        totalHeight[node] += (from.Y + (to.Y - from.Y) * t) * share;
                        totalWeight[node] += share;

                        // Worn edges wander, where a straight mask would look painted on.
                        float edge = road.HalfWidth + noise.Fbm(position.X, position.Y, 6f, 2) * 0.55f;
                        float paint = 1f - Smoothstep(edge - 0.5f, edge + 0.7f, distance);
                        if (paint <= 0f) return;
                        if (highway && NearVillage(world, position)) world.PavedRoad.Raise(x, z, paint);
                        else world.TrailRoad.Raise(x, z, paint);
                    });
                }
            }

            float[] data = height.Data;
            Parallel.For(0, nodes, i =>
            {
                if (carve[i] > 0f) data[i] = Lerp(data[i], totalHeight[i] / totalWeight[i], carve[i]);
            });
        }

        // Cobbles only where people have reason to pay for them.
        internal static bool NearVillage(WorldData world, Vector2 position)
        {
            foreach (WorldSite site in world.Sites)
            {
                // The Haven's streets are laid as a village's are, in setts between kerbs, as they're the way to its halls.
                if (site.Kind is not (SiteKind.Village or SiteKind.Keep or SiteKind.Haven)) continue;
                float reach = site.Radius * 1.2f;
                if ((site.Position - position).LengthSquared < reach * reach) return true;
            }
            return false;
        }

        private static void ApplyPaint(WorldData world, in PaintPatch patch)
        {
            ByteField target = patch.Paint switch
            {
                GroundPaint.Yard => world.Yard,
                GroundPaint.Plaza => world.Plaza,
                GroundPaint.Paving => world.Paving,
                GroundPaint.Garden => world.Garden,
                GroundPaint.Sand => world.Sand,
                GroundPaint.Mud => world.Mud,
                GroundPaint.Ash => world.Ash,
                _ => world.Farmland,
            };
            if (patch.Paint == GroundPaint.Paving) world.Paved = true;
            if (patch.Paint == GroundPaint.Sand) world.Sanded = true;
            if (patch.Paint == GroundPaint.Mud) world.Mudded = true;
            if (patch.Paint == GroundPaint.Ash) world.Ashen = true;
            Footprint shape = patch.Shape;
            float softness = patch.Softness;
            ForEachNode(world.Height, shape.Centre, shape.BoundingRadius + softness, (x, z, position) =>
            {
                float outside = DistanceOutside(shape, position);
                target.Raise(x, z, 1f - Smoothstep(0f, softness, outside));
            });
        }

        private static void ForEachNode(ScalarField field, Vector2 centre, float reach, Action<int, int, Vector2> visit)
        {
            int minX = Math.Max((int)MathF.Floor((centre.X - reach) / field.CellSize), 0);
            int maxX = Math.Min((int)MathF.Ceiling((centre.X + reach) / field.CellSize), field.Width - 1);
            int minZ = Math.Max((int)MathF.Floor((centre.Y - reach) / field.CellSize), 0);
            int maxZ = Math.Min((int)MathF.Ceiling((centre.Y + reach) / field.CellSize), field.Depth - 1);
            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    visit(x, z, field.WorldPosition(x, z));
                }
            }
        }
    }
}

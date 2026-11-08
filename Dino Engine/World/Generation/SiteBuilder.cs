using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.World.WorldMath;

namespace Dino_Engine.World.Generation
{
    /// <summary>
    /// The primitives every site is built from. All of them go through the world's <see cref="PlacementIndex"/>, so whatever a
    /// layout asks for, nothing ends up inside anything else: a request that doesn't fit simply fails, and the layout moves on.
    /// </summary>
    internal sealed class SiteBuilder
    {
        private const float HighestBuildSlope = 0.42f;
        private const float MinGroundAboveWater = 0.4f;
        private const PlacementLayer Blockers = PlacementLayer.Built | PlacementLayer.Road;
        private const float NearStep = 1.5f;

        public readonly WorldData World;
        public readonly WorldSite Site;
        public readonly WorldRandom Random;
        public readonly BuildingStyle Style;

        public SiteBuilder(WorldData world, WorldSite site, WorldRandom random)
        {
            World = world;
            Site = site;
            Random = random;
            Style = world.Style;
        }

        public Vector2 Centre => Site.Position;

        /// <summary>The yaw that points an object's front (+z) along <paramref name="direction"/>.</summary>
        public static float YawFacing(Vector2 direction) => MathF.Atan2(direction.X, direction.Y);

        public static Vector2 Direction(float angle) => new Vector2(MathF.Cos(angle), MathF.Sin(angle));

        /// <summary>Whether the land under <paramref name="footprint"/> can carry something, ignoring what is already there.</summary>
        public bool GroundFits(in Footprint footprint, float maxSlope)
        {
            float reach = footprint.BoundingRadius;
            for (int i = 0; i <= 8; i++)
            {
                Vector2 point = i == 8 ? footprint.Centre : footprint.Centre + Direction(i * (MathF.Tau / 8f)) * reach;
                if (!World.InBounds(point)) return false;
                if (World.Elevation.Sample(point) < WorldPlan.SeaLevel + MinGroundAboveWater) return false;
                if (World.Elevation.SlopeAt(point) > maxSlope) return false;
                if (World.Reachable.Sample(point) < 0.5f) return false;
            }
            return true;
        }

        public bool Fits(in Footprint footprint, float maxSlope = HighestBuildSlope, float padding = 1.2f, PlacementLayer blockers = Blockers)
        {
            return GroundFits(footprint, maxSlope) && !World.Placement.Overlaps(footprint, blockers, padding);
        }

        /// <summary>Puts a structure down if it fits. Its ground is levelled if it wants that.</summary>
        public bool Place(StructureDefinition definition, Vector2 position, float yaw, float scale = 1f, float padding = 1.2f, float maxSlope = HighestBuildSlope)
        {
            Footprint footprint = Scaled(definition, position, yaw, scale);
            PlacementLayer blockers = definition.SpansRoad ? PlacementLayer.Built : Blockers;
            if (!Fits(footprint, maxSlope, padding, blockers)) return false;
            Commit(definition, position, yaw, scale, footprint);
            return true;
        }

        /// <summary>
        /// Puts down something a site is nothing without: where it was meant to go if it fits there, or else as near to that as it will. A spawn with nothing standing on it is a
        /// prompt in an empty field, so anything that can be used goes through here, and <paramref name="placed"/> is where to put its spawn.
        /// </summary>
        public bool PlaceNear(StructureDefinition definition, Vector2 position, float yaw, float reach, out Vector2 placed, float padding = 1.2f)
        {
            for (float distance = 0f; distance <= reach; distance += NearStep)
            {
                int directions = distance > 0f ? 8 : 1;
                for (int i = 0; i < directions; i++)
                {
                    placed = position + Direction(i * (MathF.Tau / 8f)) * distance;
                    if (Place(definition, placed, yaw, padding: padding)) return true;
                }
            }

            // WorldValidator fails a site that loses one of these, so a world this happens in doesn't go unnoticed.
            placed = position;
            return false;
        }

        /// <summary>Puts down a building and the wings that stick out of it, all of them or none. Each wing has its own footprint.</summary>
        public bool PlaceWithWings(StructureDefinition definition, Vector2 position, float yaw, Footprint[] wings, float padding, float maxSlope)
        {
            Footprint footprint = Scaled(definition, position, yaw, 1f);
            if (!Fits(footprint, maxSlope, padding)) return false;
            foreach (Footprint wing in wings)
            {
                if (!Fits(wing, maxSlope, padding)) return false;
            }

            Commit(definition, position, yaw, 1f, footprint);
            float height = World.Pads[^1].Height;
            foreach (Footprint wing in wings)
            {
                World.Placement.Add(wing, PlacementLayer.Structure, definition.Solid);
                World.Pads.Add(new GroundPad(wing, height, 5f));
            }
            return true;
        }

        private static Footprint Scaled(StructureDefinition definition, Vector2 position, float yaw, float scale)
        {
            return definition.IsRound
                ? Footprint.Circle(position, definition.HalfExtents.X * scale)
                : Footprint.Box(position, definition.HalfExtents * scale, yaw);
        }

        private void Commit(StructureDefinition definition, Vector2 position, float yaw, float scale, in Footprint footprint)
        {
            PlacementLayer layer = definition.SpansRoad ? PlacementLayer.Keepout : definition.OnPad ? PlacementLayer.Structure : PlacementLayer.Prop;
            World.Placement.Add(footprint, layer, definition.Solid, definition.Height);

            float? padHeight = null;
            if (definition.OnPad)
            {
                float height = AverageGround(footprint);
                padHeight = height;
                World.Pads.Add(new GroundPad(footprint, height, 5f));
            }

            string asset = StructureCatalog.AssetId(definition, Style, Random.Int(Math.Max(definition.Variants, 1)));
            World.Pending.Add(new PendingObject(asset, position, yaw, new Vector3(scale), padHeight));
        }

        private float AverageGround(in Footprint footprint)
        {
            float total = World.Elevation.Sample(footprint.Centre);
            float reach = footprint.BoundingRadius * 0.7f;
            for (int i = 0; i < 6; i++)
            {
                total += World.Elevation.Sample(footprint.Centre + Direction(i * (MathF.Tau / 6f)) * reach);
            }
            return total / 7f;
        }

        /// <summary>Tries the spots around <paramref name="around"/> until the structure fits; for loose clutter that can go anywhere nearby.</summary>
        public bool Scatter(StructureDefinition definition, Vector2 around, float radius, int attempts = 6, float padding = 0.5f)
        {
            for (int i = 0; i < attempts; i++)
            {
                Vector2 position = around + Direction(Random.Float() * MathF.Tau) * (radius * MathF.Sqrt(Random.Float()));
                if (Place(definition, position, Random.Float() * MathF.Tau, 1f, padding)) return true;
            }
            return false;
        }

        /// <summary>A run of wall or fence, kept out of whatever already stands there. False if it doesn't fit.</summary>
        public bool Wall(StructureDefinition definition, Vector2 from, Vector2 to, float segmentLength, float scale, float thickness)
        {
            Vector2 span = to - from;
            float length = span.Length;
            if (length < 0.5f) return false;
            Vector2 middle = (from + to) * 0.5f;

            for (int i = 0; i <= 4; i++)
            {
                Vector2 point = Vector2.Lerp(from, to, i * 0.25f);
                if (!World.InBounds(point) || World.Elevation.Sample(point) < WorldPlan.SeaLevel + MinGroundAboveWater) return false;
            }

            Footprint footprint = Footprint.Segment(from, to, thickness);
            if (World.Placement.Overlaps(footprint, PlacementLayer.Structure | PlacementLayer.Prop | PlacementLayer.Road, 0.4f)) return false;

            World.Placement.Add(footprint, PlacementLayer.Fence, solid: true, height: definition.Height);
            World.Walls.Add(new WallRun(definition.Id, from, to, segmentLength, scale));
            return true;
        }

        /// <summary>
        /// A closed ring of wall around the site, open where a road passes through and where <paramref name="gate"/> asks. A gate
        /// that lands on a corner can come out too narrow to walk through; <paramref name="gateOnEdge"/> turns the ring so it can't.
        /// </summary>
        public List<Vector2> Ring(StructureDefinition definition, float radius, int corners, float segmentLength, float scale, float thickness, Vector2? gate = null, float gateWidth = 9f, bool gateOnEdge = false)
        {
            var points = new List<Vector2>(corners);
            float start = Random.Float() * MathF.Tau;
            if (gateOnEdge && gate.HasValue) start = MathF.Atan2(gate.Value.Y, gate.Value.X) + MathF.PI / corners;
            for (int i = 0; i < corners; i++)
            {
                float angle = start + i * (MathF.Tau / corners);
                points.Add(Centre + Direction(angle) * radius * Random.Range(0.92f, 1.08f));
            }

            for (int i = 0; i < corners; i++)
            {
                Vector2 from = points[i];
                Vector2 to = points[(i + 1) % corners];
                if (gate.HasValue && BreakForGate(ref from, ref to, gate.Value, definition, segmentLength, scale, thickness, gateWidth)) continue;
                Wall(definition, from, to, segmentLength, scale, thickness);
            }
            return points;
        }

        // Splits the wall that the gate direction runs through into two pieces either side of the opening.
        private bool BreakForGate(ref Vector2 from, ref Vector2 to, Vector2 gate, StructureDefinition definition, float segmentLength, float scale, float thickness, float gateWidth)
        {
            Vector2 a = from - Centre;
            Vector2 b = to - Centre;
            float crossA = a.X * gate.Y - a.Y * gate.X;
            float crossB = b.X * gate.Y - b.Y * gate.X;
            bool straddles = crossA * crossB <= 0f && Vector2.Dot(a + b, gate) > 0f;
            if (!straddles) return false;

            float t = crossA / (crossA - crossB);
            Vector2 middle = Vector2.Lerp(from, to, t);
            Vector2 along = (to - from).Normalized();
            Wall(definition, from, middle - along * gateWidth * 0.5f, segmentLength, scale, thickness);
            Wall(definition, middle + along * gateWidth * 0.5f, to, segmentLength, scale, thickness);
            return true;
        }

        public void Paint(GroundPaint paint, in Footprint shape, float softness)
        {
            World.Paint.Add(new PaintPatch(paint, shape, softness));
        }

        public void Spawn(SpawnRole role, Vector2 position)
        {
            Site.Spawns.Add(new SiteSpawn(role, position));
        }

        /// <summary>Marks <paramref name="count"/> spawns on open ground around <paramref name="around"/>.</summary>
        public void SpawnAround(SpawnRole role, Vector2 around, float radius, int count)
        {
            for (int i = 0; i < count; i++)
            {
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    Vector2 position = around + Direction(Random.Float() * MathF.Tau) * (radius * MathF.Sqrt(Random.Float()));
                    Footprint standing = Footprint.Circle(position, 1.2f);
                    if (!GroundFits(standing, 0.55f)) continue;
                    if (World.Placement.Overlaps(standing, PlacementLayer.Structure | PlacementLayer.Prop | PlacementLayer.Fence)) continue;
                    Spawn(role, position);
                    break;
                }
            }
        }

        /// <summary>
        /// A fenced field beside <paramref name="position"/>: painted earth, rows of crops and a gap in the fence on the side
        /// facing <paramref name="gateSide"/>.
        /// </summary>
        public bool Field(Vector2 position, Vector2 halfExtents, float yaw, Vector2 gateSide)
        {
            Footprint footprint = Footprint.Box(position, halfExtents, yaw);
            if (!GroundFits(footprint, 0.17f)) return false;
            if (World.Placement.Overlaps(footprint, Blockers, 2f)) return false;

            World.Placement.Add(footprint, PlacementLayer.Field);
            Paint(GroundPaint.Farmland, footprint, 1.6f);
            World.Clearings.Add(new Clearing(position, halfExtents.Length, 18f));
            World.Pads.Add(new GroundPad(footprint, AverageGround(footprint), 7f));

            StructureDefinition crop = Random.Chance(0.6f) ? StructureCatalog.GrainCrop : StructureCatalog.VegetableCrop;
            float rowSpacing = crop == StructureCatalog.GrainCrop ? 2.5f : 3f;
            float plantSpacing = crop == StructureCatalog.GrainCrop ? 2.3f : 2.6f;
            for (float z = -halfExtents.Y + 2f; z <= halfExtents.Y - 2f; z += rowSpacing)
            {
                for (float x = -halfExtents.X + 2f; x <= halfExtents.X - 2f; x += plantSpacing)
                {
                    Vector2 plant = footprint.ToWorld(new Vector2(x + Random.Range(-0.4f, 0.4f), z + Random.Range(-0.25f, 0.25f)));
                    World.Pending.Add(new PendingObject(crop.Id, plant, Random.Float() * MathF.Tau, new Vector3(Random.Range(0.85f, 1.15f)), null));
                }
            }

            FenceAround(footprint, gateSide);
            return true;
        }

        /// <summary>Fences a box, leaving a gate in the side nearest <paramref name="gateSide"/>.</summary>
        public void FenceAround(in Footprint box, Vector2 gateSide)
        {
            Vector2 half = box.HalfExtents + new Vector2(0.8f);
            Vector2[] corners =
            {
                box.ToWorld(new Vector2(-half.X, -half.Y)),
                box.ToWorld(new Vector2(half.X, -half.Y)),
                box.ToWorld(new Vector2(half.X, half.Y)),
                box.ToWorld(new Vector2(-half.X, half.Y)),
            };

            int gateEdge = 0;
            float bestFacing = float.NegativeInfinity;
            for (int i = 0; i < 4; i++)
            {
                Vector2 middle = (corners[i] + corners[(i + 1) % 4]) * 0.5f;
                float facing = Vector2.Dot((middle - box.Centre).Normalized(), gateSide);
                if (facing > bestFacing)
                {
                    bestFacing = facing;
                    gateEdge = i;
                }
            }

            for (int i = 0; i < 4; i++)
            {
                Vector2 from = corners[i];
                Vector2 to = corners[(i + 1) % 4];
                if (i == gateEdge)
                {
                    Vector2 along = (to - from).Normalized();
                    Vector2 middle = (from + to) * 0.5f;
                    Fence(from, middle - along * 2.4f);
                    Fence(middle + along * 2.4f, to);
                }
                else
                {
                    Fence(from, to);
                }
            }
        }

        public void Fence(Vector2 from, Vector2 to)
        {
            Wall(StructureCatalog.Fence, from, to, 4.6f, 5f, 0.25f);
        }

        /// <summary>The road points within <paramref name="radius"/> of the site, split into the separate streets running through it.</summary>
        public List<List<Vector3>> StreetsWithin(float radius)
        {
            var streets = new List<List<Vector3>>();
            foreach (WorldRoad road in World.Roads)
            {
                List<Vector3>? street = null;
                foreach (Vector3 point in road.Points)
                {
                    if ((point.Xz - Centre).LengthSquared <= radius * radius)
                    {
                        if (street == null)
                        {
                            street = new List<Vector3>();
                            streets.Add(street);
                        }
                        street.Add(point);
                    }
                    else
                    {
                        street = null;
                    }
                }
            }
            streets.RemoveAll(s => s.Count < 3);
            return streets;
        }

        /// <summary>Half the width of the road nearest a street point, to set buildings back from its edge.</summary>
        public float RoadHalfWidthAt(Vector2 position)
        {
            float best = float.PositiveInfinity;
            float width = 2f;
            foreach (WorldRoad road in World.Roads)
            {
                foreach (Vector3 point in road.Points)
                {
                    float distance = (point.Xz - position).LengthSquared;
                    if (distance < best)
                    {
                        best = distance;
                        width = road.HalfWidth;
                    }
                }
            }
            return width;
        }

        /// <summary>The direction of the water nearest the site, and how far it is. False if there is none in reach.</summary>
        public bool FindWater(float reach, out Vector2 direction, out float distance)
        {
            direction = Vector2.UnitX;
            distance = float.PositiveInfinity;
            for (int i = 0; i < 24; i++)
            {
                Vector2 candidate = Direction(i * (MathF.Tau / 24f));
                for (float d = 6f; d <= reach; d += 4f)
                {
                    if (World.Elevation.Sample(Centre + candidate * d) > WorldPlan.SeaLevel) continue;
                    if (d < distance)
                    {
                        distance = d;
                        direction = candidate;
                    }
                    break;
                }
            }
            return !float.IsPositiveInfinity(distance);
        }
    }
}

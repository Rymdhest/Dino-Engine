using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.World.WorldMath;

namespace Dino_Engine.World
{
    /// <summary>
    /// Where a point of the ground lies on the roads: how far across the nearest one it is and how far along, which way that road runs and how wide it is,
    /// and how much of a place where roads meet or end it is, where neither's frame can be trusted. Whatever the terrain lays in a road's own direction reads
    /// this (TEXTURES.md, Roads), so a track's ruts follow every bend and a street's setts lie square across it.
    /// </summary>
    public readonly struct RoadFrame
    {
        /// <summary>What <see cref="Across"/> reads where no road is near.</summary>
        public const float Far = 1e4f;

        /// <summary>Metres from the centre line, positive on the side <see cref="Side"/> points to.</summary>
        public readonly float Across;
        /// <summary>Metres from the road's start along its centre line.</summary>
        public readonly float Along;
        /// <summary>The way the road runs here, unit, the way <see cref="Along"/> grows.</summary>
        public readonly Vector2 Way;
        /// <summary>Metres from the centre line to the road's edge, 0 where no road is near.</summary>
        public readonly float HalfWidth;
        /// <summary>1 where another road meets this one or it ends, fading to 0 a few metres away: there across and along jump, and nothing may be laid by them.</summary>
        public readonly float Junction;

        public RoadFrame(float across, float along, Vector2 way, float halfWidth, float junction)
        {
            Across = across;
            Along = along;
            Way = way;
            HalfWidth = halfWidth;
            Junction = junction;
        }

        public bool Found => HalfWidth > 0f;

        /// <summary>Across's own direction: the way turned a right angle, as Terrain.frag turns it.</summary>
        public Vector2 Side => new Vector2(-Way.Y, Way.X);

        public static readonly RoadFrame None = new RoadFrame(Far, 0f, Vector2.Zero, 0f, 1f);
    }

    /// <summary>Finds the <see cref="RoadFrame"/> of any point, from a grid of which road segments pass near each cell. Headless, and safe to ask from many threads at once.</summary>
    public sealed class RoadFrames
    {
        private const float Cell = 8f;
        /// <summary>
        /// Metres past a road's edge that its frame is known: further than anything of it is painted (TerrainBakeStage frays a road's edge out to about 1.3 m past
        /// it), and a texel more, so that every texel the road's own materials blend across reads the same road.
        /// </summary>
        public const float Margin = 3f;
        /// <summary>How much nearer one road has to be than the next for its frame to hold: where two are nearly as near, the nearest swaps between texels.</summary>
        private const float Clear = 2.5f;

        private readonly WorldData _world;
        private readonly int _width;
        private readonly int[][] _cells;    // Per cell: road << 16 | segment, of every segment whose reach touches it.
        private readonly float[][] _along;   // Per road: metres along it at each point.

        public RoadFrames(WorldData world)
        {
            _world = world;
            _width = Math.Max((int)MathF.Ceiling(world.Size / Cell) + 1, 1);
            var lists = new List<int>?[_width * _width];
            _along = new float[world.Roads.Count][];

            for (int r = 0; r < world.Roads.Count; r++)
            {
                WorldRoad road = world.Roads[r];
                float[] along = new float[road.Points.Count];
                for (int i = 1; i < road.Points.Count; i++) along[i] = along[i - 1] + (road.Points[i].Xz - road.Points[i - 1].Xz).Length;
                _along[r] = along;

                float reach = road.HalfWidth + Margin;
                for (int i = 1; i < road.Points.Count; i++)
                {
                    Vector2 a = road.Points[i - 1].Xz, b = road.Points[i].Xz;
                    int minX = Math.Max((int)MathF.Floor((MathF.Min(a.X, b.X) - reach) / Cell), 0);
                    int maxX = Math.Min((int)MathF.Floor((MathF.Max(a.X, b.X) + reach) / Cell), _width - 1);
                    int minZ = Math.Max((int)MathF.Floor((MathF.Min(a.Y, b.Y) - reach) / Cell), 0);
                    int maxZ = Math.Min((int)MathF.Floor((MathF.Max(a.Y, b.Y) + reach) / Cell), _width - 1);
                    for (int z = minZ; z <= maxZ; z++)
                    {
                        for (int x = minX; x <= maxX; x++)
                        {
                            (lists[z * _width + x] ??= new List<int>()).Add(r << 16 | (i - 1));
                        }
                    }
                }
            }

            _cells = new int[lists.Length][];
            for (int i = 0; i < lists.Length; i++) _cells[i] = lists[i]?.ToArray() ?? Array.Empty<int>();
        }

        public RoadFrame At(Vector2 position)
        {
            int cx = (int)MathF.Floor(position.X / Cell), cz = (int)MathF.Floor(position.Y / Cell);
            if (cx < 0 || cz < 0 || cx >= _width || cz >= _width) return RoadFrame.None;
            int[] segments = _cells[cz * _width + cx];
            if (segments.Length == 0) return RoadFrame.None;

            // The nearest segment, measured from its edge, so that a wide road isn't lost to a narrow one whose middle is a little nearer.
            int bestRoad = -1, bestSegment = -1;
            float bestDistance = float.MaxValue, bestEdge = float.MaxValue, bestT = 0f;
            foreach (int packed in segments)
            {
                int r = packed >> 16, s = packed & 0xFFFF;
                WorldRoad road = _world.Roads[r];
                float distance = DistanceToSegment(position, road.Points[s].Xz, road.Points[s + 1].Xz, out float t);
                float edge = distance - road.HalfWidth;
                if (edge < bestEdge)
                {
                    bestEdge = edge;
                    bestDistance = distance;
                    bestRoad = r;
                    bestSegment = s;
                    bestT = t;
                }
            }
            WorldRoad nearest = _world.Roads[bestRoad];
            if (bestEdge > Margin) return RoadFrame.None;

            Vector2 from = nearest.Points[bestSegment].Xz, to = nearest.Points[bestSegment + 1].Xz;
            Vector2 way = to - from;
            float length = way.Length;
            way = length > 1e-6f ? way / length : Vector2.UnitX;
            float along = _along[bestRoad][bestSegment] + bestT * length;
            Vector2 side = new Vector2(-way.Y, way.X);
            float across = Vector2.Dot(position - (from + (to - from) * bestT), side);

            // Anything else near enough to take over: another road, or this one coming back round a hairpin, where along has run on far further than across could.
            float junction = 0f;
            float otherEdge = float.MaxValue;
            foreach (int packed in segments)
            {
                int r = packed >> 16, s = packed & 0xFFFF;
                WorldRoad road = _world.Roads[r];
                float distance = DistanceToSegment(position, road.Points[s].Xz, road.Points[s + 1].Xz, out float t);
                if (r == bestRoad)
                {
                    float there = _along[r][s] + t * (road.Points[s + 1].Xz - road.Points[s].Xz).Length;
                    if (MathF.Abs(there - along) < 2f * (distance + bestDistance) + 6f) continue;
                }
                otherEdge = MathF.Min(otherEdge, distance - road.HalfWidth);
                // On or beside another road's own ground the two overlap.
                junction = MathF.Max(junction, 1f - Smoothstep(1f, 4f, distance - road.HalfWidth));
            }
            junction = MathF.Max(junction, 1f - Smoothstep(0.75f, Clear, otherEdge - bestEdge));

            // Past either end the frame turns round the end point, and it ends in a village, a site or another road's middle anyway.
            float toStart = (position - nearest.Points[0].Xz).Length;
            float toEnd = (position - nearest.Points[^1].Xz).Length;
            junction = MathF.Max(junction, 1f - Smoothstep(nearest.HalfWidth + 0.5f, nearest.HalfWidth + 3.5f, MathF.Min(toStart, toEnd)));

            return new RoadFrame(across, along, way, nearest.HalfWidth, junction);
        }
    }

    /// <summary>
    /// The cross-section of a dirt road at a point, in metres at the world's twice real scale: where its two ruts run, the crown between them, and the line feet
    /// wear down a footpath. Terrain.frag works out the very same (roadShape), as the grass on a crown is laid on the CPU and the ruts are drawn on the GPU,
    /// and the two have to agree: change both together.
    /// </summary>
    public static class RoadProfile
    {
        /// <summary>Half widths from which carts use a road and leave ruts: a track or a highway, not a footpath.</summary>
        public const float RuttedFrom = 1.8f;
        /// <summary>Half a rut's width.</summary>
        public const float RutHalfWidth = 0.3f;

        /// <summary>Where the pair of ruts is centred, as the carts that made them wandered to one side and back.</summary>
        public static float Wander(float along) => 0.22f * MathF.Sin(along * (MathF.Tau / 29f) + 1.1f) + 0.1f * MathF.Sin(along * (MathF.Tau / 11.3f) + 2.7f);

        /// <summary>From the pair's middle to either rut's middle: half a cart's track.</summary>
        public static float Gauge(float along) => 1.3f + 0.06f * MathF.Sin(along * (MathF.Tau / 17f) + 0.4f);

        /// <summary>How much of the crown between the ruts a point is, 0..1: where grass grows on a road carts seldom use.</summary>
        public static float Crown(in RoadFrame frame)
        {
            if (!frame.Found || frame.HalfWidth < RuttedFrom) return 0f;
            float x = MathF.Abs(frame.Across - Wander(frame.Along));
            float inner = Gauge(frame.Along) - RutHalfWidth * 1.6f;
            return (1f - Smoothstep(inner - 0.35f, inner, x)) * (1f - frame.Junction);
        }

        /// <summary>How much grass the crown of a dirt road keeps, 0..1 of what grows beside it: much on a track, little on a highway, none where it's paved.</summary>
        public static float CrownGrass(in RoadFrame frame, float trail)
        {
            float use = frame.HalfWidth > 2.6f ? 0.15f : 0.75f;
            return Crown(frame) * Saturate(trail) * use;
        }
    }
}

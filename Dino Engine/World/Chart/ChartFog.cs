using OpenTK.Mathematics;

namespace Dino_Engine.World.Chart
{
    /// <summary>
    /// Where a hero has been in a land, as a grid of how well each bit of it has been seen, 0 to 1. Nothing about it is sent or saved: it's this player's own, kept while they're
    /// in the land and for a while after, in case they come back.
    /// </summary>
    public sealed class ChartDiscovery
    {
        /// <summary>Metres a cell is across.</summary>
        public const float Cell = 4f;
        /// <summary>Metres round the hero that are seen outright, about what the camera shows, and how much further they fade out over.</summary>
        public const float Sight = 40f;
        public const float SightFade = 18f;

        private readonly float[] _seen;
        private readonly int _side;
        private Vector2 _last = new Vector2(float.NaN);
        private Vector2 _dirtyMin = new Vector2(float.MaxValue), _dirtyMax = new Vector2(float.MinValue);

        public ChartDiscovery(float worldSize)
        {
            _side = (int)MathF.Ceiling(worldSize / Cell) + 1;
            _seen = new float[_side * _side];
        }

        /// <summary>Goes up by one whenever anything more has been seen.</summary>
        public int Version { get; private set; }

        /// <summary>Sees round a point. Cheap to call every frame: it does nothing until the hero has moved a little.</summary>
        public void Reveal(Vector2 at)
        {
            if (!float.IsNaN(_last.X) && (at - _last).LengthSquared < 4f) return;
            _last = at;
            float reach = Sight + SightFade;
            int x0 = Math.Max((int)((at.X - reach) / Cell), 0), x1 = Math.Min((int)((at.X + reach) / Cell) + 1, _side - 1);
            int z0 = Math.Max((int)((at.Y - reach) / Cell), 0), z1 = Math.Min((int)((at.Y + reach) / Cell) + 1, _side - 1);
            bool changed = false;
            for (int z = z0; z <= z1; z++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float distance = (new Vector2(x * Cell, z * Cell) - at).Length;
                    float seen = 1f - Math.Clamp((distance - Sight) / SightFade, 0f, 1f);
                    ref float cell = ref _seen[z * _side + x];
                    if (seen <= cell + 0.01f) continue;
                    cell = seen;
                    changed = true;
                }
            }
            if (!changed) return;
            Version++;
            _dirtyMin = Vector2.ComponentMin(_dirtyMin, at - new Vector2(reach + Cell));
            _dirtyMax = Vector2.ComponentMax(_dirtyMax, at + new Vector2(reach + Cell));
        }

        /// <summary>Everything seen at once, as a hero knows their own home.</summary>
        public void RevealAll()
        {
            Array.Fill(_seen, 1f);
            Version++;
            _dirtyMin = Vector2.Zero;
            _dirtyMax = new Vector2(_side * Cell);
        }

        /// <summary>How well a point has been seen, smoothly between cells.</summary>
        public float Sample(Vector2 at)
        {
            float fx = Math.Clamp(at.X / Cell, 0f, _side - 1.001f), fz = Math.Clamp(at.Y / Cell, 0f, _side - 1.001f);
            int x = (int)fx, z = (int)fz;
            float tx = fx - x, tz = fz - z;
            float a = _seen[z * _side + x], b = _seen[z * _side + x + 1], c = _seen[(z + 1) * _side + x], d = _seen[(z + 1) * _side + x + 1];
            return a + (b - a) * tx + (c - a) * tz + (a - b - c + d) * tx * tz;
        }

        /// <summary>The part of the world that's changed since this was last asked, if any.</summary>
        public bool TakeChanged(out Vector2 min, out Vector2 max)
        {
            min = _dirtyMin;
            max = _dirtyMax;
            _dirtyMin = new Vector2(float.MaxValue);
            _dirtyMax = new Vector2(float.MinValue);
            return min.X <= max.X;
        }

        /// <summary>Takes everything as changed, for drawing a chart's fog for the first time.</summary>
        public void Touch()
        {
            _dirtyMin = Vector2.Zero;
            _dirtyMax = new Vector2(_side * Cell);
        }
    }

    /// <summary>
    /// What lies over a chart where nobody has been: its uncoloured sketch, which thins away wherever the hero has seen, its edge ragged and a little darker, as a wash dries.
    /// The result is drawn over the coloured chart, straight alpha.
    /// </summary>
    public static class ChartFog
    {
        /// <summary>Writes the fog over a box of the chart's pixels into <paramref name="overlay"/>, a copy of the chart's sketch.</summary>
        public static void Compose(Chart chart, ChartDiscovery discovery, byte[] overlay, int x0, int y0, int x1, int y1)
        {
            int size = chart.Resolution;
            x0 = Math.Max(x0, 0);
            y0 = Math.Max(y0, 0);
            x1 = Math.Min(x1, size - 1);
            y1 = Math.Min(y1, size - 1);
            if (x0 > x1 || y0 > y1) return;
            float wobble = 7f;
            float scale = size / 2048f;
            Parallel.For(y0, y1 + 1, y =>
            {
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 world = chart.Frame.ToWorld(new Vector2(x + 0.5f, y + 0.5f));
                    // A ragged edge, as paint stops where it will.
                    world += new Vector2(ChartNoise.Fbm(x, y, 60f * scale, 3, 71u) - 0.5f, ChartNoise.Fbm(x, y, 60f * scale, 3, 73u) - 0.5f) * wobble * 2f;
                    float seen = discovery.Sample(world);
                    float fog = 1f - ChartInk.Smooth(0.3f, 0.62f, seen);
                    float rim = ChartInk.Smooth(0.18f, 0.42f, seen) * (1f - ChartInk.Smooth(0.42f, 0.6f, seen));
                    int i = (y * size + x) * 4;
                    float darken = 1f - 0.36f * rim;
                    overlay[i] = (byte)(chart.Sketch[i] * darken);
                    overlay[i + 1] = (byte)(chart.Sketch[i + 1] * darken);
                    overlay[i + 2] = (byte)(chart.Sketch[i + 2] * darken);
                    overlay[i + 3] = ChartCanvas.ToByte(MathF.Max(fog, rim * 0.5f) * (chart.Sketch[i + 3] / 255f));
                }
            });
        }
    }
}

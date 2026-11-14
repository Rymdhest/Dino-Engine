using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.World.Chart.ChartInk;

namespace Dino_Engine.World.Chart
{
    /// <summary>
    /// Draws a land as an old map, from what's really in it: read CHART.md. The ground is washed in its biomes' colours on an old sheet, shaded by its relief; the sea and every
    /// pond are ringed by ripples drawn along the shore; roads are inked, fields hatched and fences dotted; and every wood is drawn as its own trees, where they really stand and in
    /// their species' shapes and colours, every mountain in the form its rock breaks into, and every building on its own footprint, all in front of what's behind them. Headless and
    /// deterministic: the same land always draws the same chart, which the game shows as its map and the WorldLab writes to a file.
    /// </summary>
    public static class WorldChart
    {
        public static Chart Draw(WorldData world, int resolution) => new Drawing(world, resolution).Run();

        /// <summary>The square a land's chart shows: everywhere anyone can go and every place on it, with room round them, and never past the world's edge.</summary>
        public static ChartFrame FrameOf(WorldData world, int resolution)
        {
            Vector2 min = new Vector2(float.MaxValue), max = new Vector2(float.MinValue);
            void Take(Vector2 p)
            {
                min = Vector2.ComponentMin(min, p);
                max = Vector2.ComponentMax(max, p);
            }
            foreach (WorldSite site in world.Sites) Take(site.Position);
            if (world.Reachable != null)
            {
                ScalarField reach = world.Reachable;
                for (int z = 0; z < reach.Depth; z++)
                {
                    for (int x = 0; x < reach.Width; x++)
                    {
                        if (reach[x, z] > 0.5f) Take(reach.WorldPosition(x, z));
                    }
                }
            }
            float size = world.Size;
            if (min.X > max.X) return new ChartFrame(Vector2.Zero, size, resolution);

            float extent = MathF.Max(max.X - min.X, max.Y - min.Y);
            float span = MathF.Min(extent + 2f * MathF.Max(extent * 0.13f, 48f), size);
            Vector2 centre = (min + max) * 0.5f;
            Vector2 origin = new Vector2(Math.Clamp(centre.X - span * 0.5f, 0f, size - span), Math.Clamp(centre.Y - span * 0.5f, 0f, size - span));
            return new ChartFrame(origin, span, resolution);
        }

        private sealed class Drawing
        {
            private readonly WorldData _world;
            private readonly ChartFrame _frame;
            private readonly int _r;
            private readonly float _s;          // How big a pen stroke is, against a chart 2048 across.
            private readonly float _ink;
            private readonly ChartCanvas _canvas;
            private readonly float[] _rise;     // Metres above the water there.
            private readonly bool[] _water;
            private readonly float[] _shore;    // Pixels from the water's edge, out into the water and below nothing inland.
            private readonly bool[] _clear;     // What no glyph may stand over: roads and buildings.

            public Drawing(WorldData world, int resolution)
            {
                _world = world;
                _r = resolution;
                _frame = FrameOf(world, resolution);
                _s = MathF.Max(resolution / 2048f, 0.45f);
                _ink = MathF.Max(1.15f * _s, 0.75f);
                _canvas = new ChartCanvas(resolution);
                _rise = new float[resolution * resolution];
                _water = new bool[resolution * resolution];
                _shore = new float[resolution * resolution];
                _clear = new bool[resolution * resolution];
            }

            private Vector2 World(int x, int y) => _frame.ToWorld(new Vector2(x + 0.5f, y + 0.5f));
            private Vector2 Pixel(Vector2 world) => _frame.ToPixel(world);
            private float PixelsPerMetre => _frame.PixelsPerMetre;

            public Chart Run()
            {
                MeasureWater();
                PaintGround();
                DrawWaves();
                DrawFences();
                DrawRoads();
                var glyphs = new List<ChartGlyph>();
                AddBuildings(glyphs);
                AddTrees(glyphs);
                AddMountains(glyphs);
                DrawGlyphs(glyphs);
                DrawBorder();
                DrawCompass();
                Age();
                byte[] colour = _canvas.ToBytes();
                return new Chart(_frame, colour, Sketch(colour), RegionLabels());
            }

            // ---- The ground.

            private void MeasureWater()
            {
                ScalarField height = _world.Height;
                Parallel.For(0, _r, y =>
                {
                    for (int x = 0; x < _r; x++)
                    {
                        Vector2 p = World(x, y);
                        float rise = height.Sample(p) - _world.WaterLevelAt(p);
                        _rise[y * _r + x] = rise;
                        _water[y * _r + x] = rise < 0f;
                    }
                });

                float[] toLand = ChartDistance.Transform(_water, _r, feature: false);
                float[] toWater = ChartDistance.Transform(_water, _r, feature: true);
                Parallel.For(0, _r, y =>
                {
                    for (int x = 0; x < _r; x++)
                    {
                        int i = y * _r + x;
                        float far = _water[i] ? toLand[i] - 0.5f : 0.5f - toWater[i];
                        // Near the edge the height says where it really is between pixels, so the line doesn't step.
                        if (MathF.Abs(far) < 3f && x > 0 && y > 0 && x < _r - 1 && y < _r - 1)
                        {
                            float gx = (_rise[i + 1] - _rise[i - 1]) * 0.5f, gy = (_rise[i + _r] - _rise[i - _r]) * 0.5f;
                            float near = -_rise[i] / MathF.Max(MathF.Sqrt(gx * gx + gy * gy), 0.02f);
                            if (MathF.Abs(near) < 2.5f) far = near;
                        }
                        _shore[i] = far;
                    }
                });
            }

            private void PaintGround()
            {
                IReadOnlyList<BiomeDefinition> biomes = _world.Plan.Biomes;
                int count = biomes.Count;
                var pigments = new Vector3[count];
                var snowy = new float[count];
                for (int b = 0; b < count; b++)
                {
                    pigments[b] = Pigment(biomes[b].MapColour);
                    snowy[b] = biomes[b].Ground is SurfaceMaterial.Snow or SurfaceMaterial.Ice ? 1f : 0f;
                }
                Vector3 rockWash = RockColours(_world.Geology?.Kind ?? RockKind.Granite).lit;
                int[] fields = FieldsOf();
                float mpp = 1f / PixelsPerMetre;
                float ripple = MathF.Max(_r * 0.0062f, 4f);

                Parallel.For(0, _r, y =>
                {
                    Span<float> weights = stackalloc float[Math.Max(count, 1)];
                    for (int x = 0; x < _r; x++)
                    {
                        int i = y * _r + x;
                        Vector2 p = World(x, y);
                        Vector3 paper = PaperAt(x, y);
                        Vector3 colour;
                        float shore = _shore[i];
                        if (_water[i])
                        {
                            float depth = -_rise[i];
                            colour = Vector3.Lerp(SeaShallow, SeaDeep, Smooth(0f, 110f * _s, shore) * 0.75f + Smooth(1f, 12f, depth) * 0.25f) * (paper / Paper);
                            colour = Vector3.Lerp(colour, SeaShallow * 1.07f, (1f - Smooth(0f, 16f * _s, shore)) * 0.55f);
                            for (int k = 1; k <= 4; k++)
                            {
                                float at = ripple * k + (ChartNoise.Fbm(x, y, 70f * _s, 2, 900u + (uint)k) - 0.5f) * 4f * _s;
                                float cover = Pen.Coverage(MathF.Abs(shore - at) - 0.6f * _s, 1.2f * _s);
                                colour = Vector3.Lerp(colour, SeaInk, cover * (0.78f - 0.15f * k));
                            }
                        }
                        else
                        {
                            float total = 0f;
                            float top = 0f, second = 0f;
                            for (int b = 0; b < count; b++)
                            {
                                float w = _world.BiomeWeights[b].Sample(p);
                                w = w * w * w;
                                weights[b] = w;
                                total += w;
                            }
                            Vector3 pigment = Vector3.Zero;
                            float snow = 0f;
                            for (int b = 0; b < count; b++)
                            {
                                float w = weights[b] / MathF.Max(total, 1e-6f);
                                pigment += pigments[b] * w;
                                snow += snowy[b] * w;
                                if (w > top)
                                {
                                    second = top;
                                    top = w;
                                }
                                else if (w > second) second = w;
                            }
                            float density = 0.84f + 0.4f * (ChartNoise.Fbm(x, y, 110f * _s, 3, 11u) - 0.5f);
                            colour = paper * Vector3.Lerp(Vector3.One, pigment, density);
                            colour = Vector3.Lerp(colour, Snow * (paper / Paper), snow * 0.7f);
                            // A darker edge where one wash meets the next, as wet paint does.
                            colour *= 1f - 0.08f * (1f - Smooth(0f, 0.28f, top - second));

                            colour *= Relief(p, mpp);

                            float rock = _world.Rock.Sample(p);
                            colour = Vector3.Lerp(colour, rockWash * (paper / Paper), rock * 0.45f);
                            float laid = MathF.Max(_world.Plaza.Sample(p), MathF.Max(_world.Yard.Sample(p), _world.Paved ? _world.Paving.Sample(p) : 0f));
                            colour = Vector3.Lerp(colour, paper * new Vector3(1f, 0.95f, 0.86f), laid * 0.3f);
                            if (_world.Sanded) colour = Vector3.Lerp(colour, paper * new Vector3(1f, 0.92f, 0.74f), _world.Sand.Sample(p) * 0.8f);
                            if (_world.Mudded) colour = Vector3.Lerp(colour, paper * new Vector3(0.72f, 0.6f, 0.48f), _world.Mud.Sample(p) * 0.8f);
                            if (_world.Ashen) colour = Vector3.Lerp(colour, paper * new Vector3(0.56f, 0.54f, 0.52f), _world.Ash.Sample(p) * 0.8f);
                            if (_world.Chipped) colour = Vector3.Lerp(colour, paper * new Vector3(0.84f, 0.82f, 0.78f), _world.Chippings.Sample(p) * 0.7f);
                            if (fields[i] > 0) colour = Field(colour, paper, x, y, fields[i]);
                            if (shore > -10f * _s && _world.ShoreHeightAt(p) < 0.7f) colour = Vector3.Lerp(colour, paper * new Vector3(1f, 0.95f, 0.8f), 0.6f);
                            // Paint gathers along the coast.
                            colour *= 1f - 0.2f * (1f - Smooth(0.5f, 6f * _s, -shore));
                        }
                        colour = Vector3.Lerp(colour, Ink, Pen.Coverage(MathF.Abs(shore) - 0.75f * _s, 1.5f * _s) * 0.85f);
                        _canvas.Pixels[i] = colour;
                    }
                });
            }

            // A biome's colour as a watercolour wash: lighter than the world, so that what's drawn on it reads.
            private static Vector3 Pigment(Vector3 mapColour)
            {
                Vector3 lifted = new Vector3(MathF.Pow(mapColour.X, 0.75f), MathF.Pow(mapColour.Y, 0.75f), MathF.Pow(mapColour.Z, 0.75f));
                lifted = Vector3.Lerp(new Vector3(Luma(lifted)), lifted, 0.7f);
                return new Vector3(0.4f) + lifted * 0.6f;
            }

            private Vector3 PaperAt(int x, int y)
            {
                float mottle = ChartNoise.Fbm(x, y, 150f * _s, 3, 1u);
                float grain = ChartNoise.Value(x, y, 1.6f, 2u);
                return Paper * (0.955f + 0.07f * mottle + 0.025f * (grain - 0.5f));
            }

            // Hill shading, lit from the top left of the chart, which is where +x and +z are: soft, as a wash is.
            private float Relief(Vector2 p, float mpp)
            {
                float step = MathF.Max(2f * mpp, 1.5f);
                ScalarField h = _world.Height;
                float dx = (h.Sample(p.X + step, p.Y) - h.Sample(p.X - step, p.Y)) / (2f * step) * 1.6f;
                float dz = (h.Sample(p.X, p.Y + step) - h.Sample(p.X, p.Y - step)) / (2f * step) * 1.6f;
                Vector3 normal = Vector3.Normalize(new Vector3(-dx, 1f, -dz));
                Vector3 light = Vector3.Normalize(new Vector3(0.55f, 0.85f, 0.65f));
                float lit = Vector3.Dot(normal, light) / light.Y;
                return Math.Clamp(1f + (lit - 1f) * 0.7f, 0.72f, 1.12f);
            }

            // Every field a patch of its own, hatched its own way.
            private int[] FieldsOf()
            {
                var id = new int[_r * _r];
                int count = 0;
                ByteField farmland = _world.Farmland;
                var mask = new bool[_r * _r];
                Parallel.For(0, _r, y =>
                {
                    for (int x = 0; x < _r; x++) mask[y * _r + x] = !_water[y * _r + x] && farmland.Sample(World(x, y)) > 0.5f;
                });
                var queue = new Queue<int>();
                for (int start = 0; start < mask.Length; start++)
                {
                    if (!mask[start] || id[start] != 0) continue;
                    count++;
                    id[start] = count;
                    queue.Enqueue(start);
                    while (queue.Count > 0)
                    {
                        int i = queue.Dequeue();
                        int x = i % _r, y = i / _r;
                        void Visit(int nx, int ny)
                        {
                            if (nx < 0 || ny < 0 || nx >= _r || ny >= _r) return;
                            int j = ny * _r + nx;
                            if (!mask[j] || id[j] != 0) return;
                            id[j] = count;
                            queue.Enqueue(j);
                        }
                        Visit(x + 1, y);
                        Visit(x - 1, y);
                        Visit(x, y + 1);
                        Visit(x, y - 1);
                    }
                }
                return id;
            }

            private Vector3 Field(Vector3 colour, Vector3 paper, int x, int y, int field)
            {
                uint hash = ChartNoise.Hash((uint)field * 2654435761u);
                float angle = (hash & 1023u) / 1023f * MathF.PI;
                bool green = (hash >> 12 & 3u) == 0u;
                Vector3 tint = green ? new Vector3(0.84f, 0.92f, 0.66f) : new Vector3(1f, 0.92f, 0.66f);
                colour = Vector3.Lerp(colour, paper * tint, 0.65f);
                float spacing = MathF.Max(3.6f * _s, 2.2f);
                float u = (x * MathF.Cos(angle) + y * MathF.Sin(angle)) / spacing;
                float d = MathF.Abs(u - MathF.Floor(u) - 0.5f) * spacing;
                return Vector3.Lerp(colour, InkSoft, Pen.Coverage(d - 0.35f * _s, 0.7f * _s) * 0.32f);
            }

            // Little waves out on open water, as an engraver puts them there.
            private void DrawWaves()
            {
                float spacing = 70f * _s;
                int cells = (int)(_r / spacing) + 1;
                var pen = _canvas.All;
                for (int cy = 0; cy < cells; cy++)
                {
                    for (int cx = 0; cx < cells; cx++)
                    {
                        if (ChartNoise.Unit(cx, cy, 500u) > 0.42f) continue;
                        Vector2 at = new Vector2((cx + 0.2f + 0.6f * ChartNoise.Unit(cx, cy, 501u)) * spacing, (cy + 0.2f + 0.6f * ChartNoise.Unit(cx, cy, 502u)) * spacing);
                        int x = (int)at.X, y = (int)at.Y;
                        if (x < 0 || y < 0 || x >= _r || y >= _r || !_water[y * _r + x] || _shore[y * _r + x] < 40f * _s) continue;
                        float size = 7f * _s;
                        for (int hump = 0; hump < 2; hump++)
                        {
                            Vector2 start = at + new Vector2((hump - 1f) * size * 1.6f, 0f);
                            Vector2 last = start;
                            for (int k = 1; k <= 6; k++)
                            {
                                float t = k / 6f;
                                Vector2 next = start + new Vector2(t * size * 1.6f, -MathF.Sin(t * MathF.PI) * size * 0.55f);
                                pen.Stroke(last, next, 0.9f * _s, SeaInk, 0.55f);
                                last = next;
                            }
                        }
                    }
                }
            }

            private void DrawFences()
            {
                var pen = _canvas.All;
                float dot = MathF.Max(2.2f * _s, 1.5f);
                foreach (FenceRun run in _world.Fences)
                {
                    Vector2 a = Pixel(run.From), b = Pixel(run.To);
                    float length = (b - a).Length;
                    int steps = Math.Max((int)(length / dot), 1);
                    for (int k = 0; k < steps; k += 2)
                    {
                        pen.Stroke(Vector2.Lerp(a, b, k / (float)steps), Vector2.Lerp(a, b, MathF.Min((k + 1f) / steps, 1f)), 0.85f * _s, InkSoft, 0.6f);
                    }
                }
            }

            private void DrawRoads()
            {
                var pen = _canvas.All;
                float ppm = PixelsPerMetre;
                Vector3 trackInk = new Vector3(0.42f, 0.31f, 0.2f);
                // Footpaths and tracks first, so that the highways cross over them.
                foreach (RoadClass kind in new[] { RoadClass.Footpath, RoadClass.Track, RoadClass.Highway })
                {
                    foreach (WorldRoad road in _world.Roads)
                    {
                        if (road.Class != kind || road.Points.Count < 2) continue;
                        float width = kind switch
                        {
                            RoadClass.Highway => Math.Clamp(road.HalfWidth * 2f * ppm * 0.75f, 4.5f * _s, 9f * _s),
                            RoadClass.Track => MathF.Max(1.9f * _s, 1.2f),
                            _ => MathF.Max(1.3f * _s, 1f),
                        };
                        Vector2[] line = road.Points.Select(point => Pixel(point.Xz)).ToArray();
                        MarkClear(line, kind == RoadClass.Highway ? width * 0.5f + 2f * _s : width * 0.5f + 2.5f * _s);
                        if (kind == RoadClass.Highway)
                        {
                            for (int i = 1; i < line.Length; i++) pen.Stroke(line[i - 1], line[i], width, Ink, 0.9f);
                            for (int i = 1; i < line.Length; i++) pen.Stroke(line[i - 1], line[i], MathF.Max(width - 2.4f * _ink, 1f), Paper * new Vector3(1f, 0.97f, 0.9f));
                        }
                        else if (kind == RoadClass.Track)
                        {
                            for (int i = 1; i < line.Length; i++) pen.Stroke(line[i - 1], line[i], width, trackInk, 0.9f);
                        }
                        else
                        {
                            // A footpath is dashed.
                            float dash = 4.5f * _s, gap = 3.5f * _s, along = 0f;
                            for (int i = 1; i < line.Length; i++)
                            {
                                Vector2 a = line[i - 1], b = line[i];
                                float length = (b - a).Length;
                                float done = 0f;
                                while (done < length)
                                {
                                    float phase = along % (dash + gap);
                                    float step = phase < dash ? MathF.Min(dash - phase, length - done) : MathF.Min(dash + gap - phase, length - done);
                                    if (phase < dash) pen.Stroke(Vector2.Lerp(a, b, done / length), Vector2.Lerp(a, b, (done + step) / length), width, trackInk, 0.85f);
                                    done += MathF.Max(step, 0.01f);
                                    along += MathF.Max(step, 0.01f);
                                }
                            }
                        }
                    }
                }
            }

            private void MarkClear(Vector2[] line, float reach)
            {
                for (int i = 1; i < line.Length; i++)
                {
                    Vector2 a = line[i - 1], b = line[i];
                    int x0 = Math.Max((int)(MathF.Min(a.X, b.X) - reach), 0), x1 = Math.Min((int)(MathF.Max(a.X, b.X) + reach) + 1, _r - 1);
                    int y0 = Math.Max((int)(MathF.Min(a.Y, b.Y) - reach), 0), y1 = Math.Min((int)(MathF.Max(a.Y, b.Y) + reach) + 1, _r - 1);
                    for (int y = y0; y <= y1; y++)
                    {
                        for (int x = x0; x <= x1; x++)
                        {
                            if (Sdf.Segment(new Vector2(x + 0.5f, y + 0.5f), a, b) <= reach) _clear[y * _r + x] = true;
                        }
                    }
                }
            }

            private void MarkClear(Vector2 min, Vector2 max)
            {
                for (int y = Math.Max((int)min.Y, 0); y <= Math.Min((int)max.Y, _r - 1); y++)
                {
                    for (int x = Math.Max((int)min.X, 0); x <= Math.Min((int)max.X, _r - 1); x++) _clear[y * _r + x] = true;
                }
            }

            private bool Clear(Vector2 pixel)
            {
                int x = (int)pixel.X, y = (int)pixel.Y;
                if (x < 0 || y < 0 || x >= _r || y >= _r) return true;
                return !_clear[y * _r + x];
            }

            // ---- What stands on it.

            private void AddBuildings(List<ChartGlyph> glyphs)
            {
                float ppm = PixelsPerMetre;
                var found = new List<(Footprint shape, float height)>();
                _world.Placement?.ForEach((footprint, layer, solid, height) =>
                {
                    if ((layer & PlacementLayer.Structure) == 0 || !solid || height < 1.5f || footprint.BoundingRadius < 1f) return;
                    if (_world.HallAt(footprint.Centre, 1f) != null) return;
                    found.Add((footprint, height));
                });
                foreach (WorldHall hall in _world.Halls) found.Add((hall.Footprint, hall.Design.WallTop));

                foreach ((Footprint shape, float height) in found)
                {
                    Vector2 centre = Pixel(shape.Centre);
                    if (centre.X < -20f || centre.Y < -20f || centre.X > _r + 20f || centre.Y > _r + 20f) continue;
                    (Vector3 wall, Vector3 roof, bool flat) = LookOfBuilding(shape.Centre);
                    float lift = Math.Clamp(height, 2f, 12f) * ppm * 0.42f;
                    ChartGlyph glyph;
                    if (shape.IsCircle)
                    {
                        float radius = MathF.Max(shape.HalfExtents.X * ppm, 1.5f);
                        glyph = new BuildingGlyph(centre, radius, lift, wall, roof, _ink * 0.85f);
                    }
                    else
                    {
                        var corners = new Vector2[4];
                        Vector2 h = shape.HalfExtents;
                        corners[0] = Pixel(shape.ToWorld(new Vector2(-h.X, -h.Y)));
                        corners[1] = Pixel(shape.ToWorld(new Vector2(h.X, -h.Y)));
                        corners[2] = Pixel(shape.ToWorld(new Vector2(h.X, h.Y)));
                        corners[3] = Pixel(shape.ToWorld(new Vector2(-h.X, h.Y)));
                        // A wall's thin edge is a line on a chart: give it a pen's width at least.
                        glyph = new BuildingGlyph(corners, lift, wall, roof, flat || MathF.Min(h.X, h.Y) < 1.2f, _ink * 0.85f);
                    }
                    MarkClear(glyph.Min, glyph.Max);
                    glyphs.Add(glyph);
                }
            }

            private (Vector3 wall, Vector3 roof, bool flat) LookOfBuilding(Vector2 at)
            {
                WorldSite? nearest = null;
                float best = float.MaxValue;
                foreach (WorldSite site in _world.Sites)
                {
                    float d = (site.Position - at).LengthSquared;
                    if (d < best)
                    {
                        best = d;
                        nearest = site;
                    }
                }
                // The Reich's keeps are its own dark stone.
                if (nearest is { Kind: SiteKind.Keep } && best < nearest.Radius * nearest.Radius * 2.5f) return (new Vector3(0.52f, 0.5f, 0.48f), new Vector3(0.3f, 0.29f, 0.3f), false);
                BuildingStyle style = nearest != null ? _world.StyleOf(nearest) : _world.Style;
                return style switch
                {
                    BuildingStyle.Eastern => (new Vector3(0.82f, 0.76f, 0.64f), new Vector3(0.36f, 0.37f, 0.4f), false),
                    BuildingStyle.Stone => (new Vector3(0.72f, 0.7f, 0.65f), new Vector3(0.42f, 0.44f, 0.48f), false),
                    BuildingStyle.Log => (new Vector3(0.62f, 0.44f, 0.28f), new Vector3(0.46f, 0.4f, 0.3f), false),
                    BuildingStyle.Adobe => (new Vector3(0.84f, 0.66f, 0.46f), new Vector3(0.8f, 0.62f, 0.44f), true),
                    BuildingStyle.Stilt => (new Vector3(0.62f, 0.47f, 0.3f), new Vector3(0.74f, 0.62f, 0.38f), false),
                    BuildingStyle.Tiled => (new Vector3(0.94f, 0.91f, 0.85f), new Vector3(0.74f, 0.38f, 0.24f), false),
                    BuildingStyle.Nordic => (new Vector3(0.66f, 0.22f, 0.15f), new Vector3(0.44f, 0.32f, 0.27f), false),
                    _ => (new Vector3(0.9f, 0.86f, 0.74f), new Vector3(0.68f, 0.54f, 0.32f), false),
                };
            }

            private void AddTrees(List<ChartGlyph> glyphs)
            {
                WorldObjects? objects = _world.Objects;
                if (objects == null) return;
                var looks = new TreeLook?[objects.AssetIds.Count];
                for (int i = 0; i < looks.Length; i++) looks[i] = ChartTrees.LookOf(objects.AssetIds[i]);

                // A tree a cell, the biggest that stands in it, so a wood is as thick as its trees and a lone tree is still there.
                float width = 27f * _s;
                float cell = width * 0.48f;
                int cells = (int)(_r / cell) + 1;
                var chosen = new (WorldObject tree, float size)[cells * cells];
                foreach (WorldObject tree in objects.All())
                {
                    TreeLook? look = looks[tree.Asset];
                    if (look == null) continue;
                    Vector2 at = Pixel(tree.Position.Xz);
                    if (at.X < 0f || at.Y < 0f || at.X >= _r || at.Y >= _r) continue;
                    int index = (int)(at.Y / cell) * cells + (int)(at.X / cell);
                    float size = look.MetresTall * tree.Scale.Y;
                    if (size > chosen[index].size) chosen[index] = (tree, size);
                }

                foreach ((WorldObject tree, float size) in chosen)
                {
                    if (size <= 0f) continue;
                    TreeLook look = looks[tree.Asset]!;
                    Vector2 foot = Pixel(tree.Position.Xz);
                    float w = width * look.Width * Math.Clamp(MathF.Sqrt(size / 18f), 0.78f, 1.2f);
                    float h = w * look.Aspect;
                    if (!Clear(foot) || !Clear(foot - new Vector2(0f, h * 0.55f)) || !Clear(foot - new Vector2(0f, h * 0.92f))
                        || !Clear(foot - new Vector2(w * 0.38f, h * 0.55f)) || !Clear(foot + new Vector2(w * 0.38f, -h * 0.55f))) continue;
                    uint seed = ChartNoise.Hash((uint)(int)(tree.Position.X * 8f) * 0x9E3779B1u ^ (uint)(int)(tree.Position.Z * 8f));
                    glyphs.Add(new TreeGlyph(foot, w, look, seed, _ink));
                }
            }

            private static (Vector3 lit, Vector3 shade) RockColours(RockKind kind) => kind switch
            {
                RockKind.Limestone => (new Vector3(0.82f, 0.78f, 0.68f), new Vector3(0.56f, 0.52f, 0.46f)),
                RockKind.Sandstone => (new Vector3(0.82f, 0.6f, 0.44f), new Vector3(0.58f, 0.38f, 0.28f)),
                RockKind.Schist => (new Vector3(0.64f, 0.66f, 0.65f), new Vector3(0.4f, 0.42f, 0.44f)),
                RockKind.Basalt => (new Vector3(0.48f, 0.46f, 0.45f), new Vector3(0.28f, 0.27f, 0.28f)),
                _ => (new Vector3(0.7f, 0.66f, 0.6f), new Vector3(0.46f, 0.43f, 0.41f)),
            };

            private void AddMountains(List<ChartGlyph> glyphs)
            {
                ScalarField? massif = _world.Massif;
                ByteField rock = _world.Rock;
                float width = 120f * _s;
                float cell = width * 0.3f;
                int cells = (int)(_r / cell) + 1;
                var candidates = new List<(Vector2 pixel, Vector2 world, float height, float mountain, int index)>();
                for (int cy = 0; cy < cells; cy++)
                {
                    for (int cx = 0; cx < cells; cx++)
                    {
                        float best = float.MinValue;
                        Vector2 bestPixel = default, bestWorld = default;
                        float bestMountain = 0f;
                        for (int j = 0; j < 4; j++)
                        {
                            for (int i = 0; i < 4; i++)
                            {
                                Vector2 pixel = new Vector2((cx + (i + 0.5f) / 4f) * cell, (cy + (j + 0.5f) / 4f) * cell);
                                if (pixel.X >= _r || pixel.Y >= _r) continue;
                                int index = (int)pixel.Y * _r + (int)pixel.X;
                                if (_water[index] || _rise[index] < 1f) continue;
                                Vector2 p = _frame.ToWorld(pixel);
                                if (!_world.InBounds(p)) continue;
                                float mountain = MathF.Max(rock.Sample(p), massif != null ? massif.Sample(p) : 0f);
                                if (mountain < 0.4f) continue;
                                float h = _world.Height.Sample(p);
                                if (h > best)
                                {
                                    best = h;
                                    bestPixel = pixel;
                                    bestWorld = p;
                                    bestMountain = mountain;
                                }
                            }
                        }
                        if (best > float.MinValue) candidates.Add((bestPixel, bestWorld, best, bestMountain, cy * cells + cx));
                    }
                }
                if (candidates.Count == 0) return;

                float[] heights = candidates.Select(c => c.height).OrderBy(h => h).ToArray();
                float low = heights[(int)(heights.Length * 0.1f)], high = heights[(int)((heights.Length - 1) * 0.95f)];
                (Vector3 lit, Vector3 shade) stone = RockColours(_world.Geology?.Kind ?? RockKind.Granite);
                ScalarField reachable = _world.Reachable;

                // Summits first, and nothing else within a mountain's reach of one, so a range is peaks and not a heap.
                var summits = new List<(Vector2 pixel, Vector2 world, float height, float mountain, int index)>();
                float apart = width * 0.56f;
                foreach (var candidate in candidates.OrderByDescending(c => c.height))
                {
                    // Flat tops stand further apart, or a plateau reads as a wall of bricks.
                    float room = MountainForms.At(_world, candidate.world.X, candidate.world.Y).Strongest is MountainForm.Canyons or MountainForm.Columns or MountainForm.Ledged ? apart * 1.35f : apart;
                    bool crowded = false;
                    foreach (var summit in summits)
                    {
                        Vector2 gap = summit.pixel - candidate.pixel;
                        // Closer side by side than up and down the sheet, as one stands in front of the next.
                        if (gap.X * gap.X + gap.Y * gap.Y * 2.2f < room * room)
                        {
                            crowded = true;
                            break;
                        }
                    }
                    if (!crowded) summits.Add(candidate);
                }

                foreach ((Vector2 pixel, Vector2 world, float height, float mountain, int index) in summits)
                {
                    float rank = Math.Clamp((height - low) / MathF.Max(high - low, 1f), 0f, 1f);
                    BiomeDefinition biome = _world.BiomeAt(world);
                    MountainForm form = MountainForms.At(_world, world.X, world.Y).Strongest;
                    Vector3 tint = Pigment(biome.MapColour);
                    Vector3 lit = Vector3.Lerp(stone.lit, stone.lit * tint * 1.15f, 0.3f);
                    Vector3 shade = Vector3.Lerp(stone.shade, stone.shade * tint * 1.1f, 0.3f);
                    bool snowGround = biome.Ground is SurfaceMaterial.Snow or SurfaceMaterial.Ice;
                    bool snow = snowGround || (biome.Temperature < 0.42f && rank > 0.55f);
                    float snowLine = snowGround ? 0.38f : 0.7f;
                    uint seed = ChartNoise.Hash((uint)index * 0x9E3779B1u ^ 0xC0FFEEu);
                    float size = width * (0.7f + 0.55f * rank) * (0.65f + 0.35f * Smooth(0.4f, 0.8f, mountain)) * (0.9f + 0.2f * ChartNoise.Unit((int)seed, 1, 3u));

                    // Stood a little above its summit, and made smaller until it hides nowhere anyone goes.
                    foreach (float fit in new[] { 1f, 0.8f, 0.64f, 0.5f })
                    {
                        float w = size * fit;
                        Vector2 foot = pixel + new Vector2(0f, w * MountainGlyph.AspectOf(form) * 0.62f);
                        var glyph = new MountainGlyph(foot, w, form, lit, shade, snow, snowLine, seed, _ink, _s);
                        bool hides = false;
                        foreach (Vector2 sample in glyph.Samples(6, 4))
                        {
                            if (!Clear(sample))
                            {
                                hides = true;
                                break;
                            }
                            Vector2 under = _frame.ToWorld(sample);
                            if (reachable != null && reachable.Sample(under) > 0.5f && rock.Sample(under) < 0.3f && (massif == null || massif.Sample(under) < 0.4f))
                            {
                                hides = true;
                                break;
                            }
                        }
                        if (hides) continue;
                        glyphs.Add(glyph);
                        break;
                    }
                }
            }

            // Everything standing, from the top of the sheet down, in bands drawn side by side: each band draws everything that reaches it, in the same order.
            private void DrawGlyphs(List<ChartGlyph> glyphs)
            {
                glyphs.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : a.Min.X.CompareTo(b.Min.X));
                int bands = Math.Max(Environment.ProcessorCount * 2, 4);
                int height = (_r + bands - 1) / bands;
                Parallel.For(0, bands, band =>
                {
                    int top = band * height, bottom = Math.Min(top + height, _r);
                    var pen = new Pen(_canvas, top, bottom);
                    foreach (ChartGlyph glyph in glyphs)
                    {
                        if (glyph.Max.Y < top || glyph.Min.Y >= bottom) continue;
                        glyph.Draw(pen);
                    }
                });
            }

            // ---- The sheet.

            private void DrawBorder()
            {
                float band = MathF.Round(_r * 0.024f);
                float segment = _r * 0.045f;
                Vector3 frame = new Vector3(0.8f, 0.72f, 0.56f);
                Parallel.For(0, _r, y =>
                {
                    for (int x = 0; x < _r; x++)
                    {
                        float fx = MathF.Min(x + 0.5f, _r - x - 0.5f), fy = MathF.Min(y + 0.5f, _r - y - 0.5f);
                        float edge = MathF.Min(fx, fy);
                        if (edge > band + 3f * _s) continue;
                        int i = y * _r + x;
                        Vector3 c = _canvas.Pixels[i];
                        if (edge <= band) c = frame * (PaperAt(x, y) / Paper);
                        // The neat line inside, a thin one outside, and between them the old checkered scale.
                        c = Vector3.Lerp(c, Ink, Pen.Coverage(MathF.Abs(edge - band) - 1f * _s, 2f * _s));
                        c = Vector3.Lerp(c, Ink, Pen.Coverage(MathF.Abs(edge - band * 0.2f) - 0.6f * _s, 1.2f * _s) * 0.9f);
                        c = Vector3.Lerp(c, Ink, Pen.Coverage(MathF.Abs(edge - band * 0.78f) - 0.4f * _s, 0.8f * _s) * 0.8f);
                        bool corner = fx <= band && fy <= band;
                        if (edge > band * 0.38f && edge < band * 0.66f)
                        {
                            float along = fx < fy ? y : x;
                            bool dark = corner || ((int)(along / segment) & 1) == 0;
                            if (dark) c = Vector3.Lerp(c, Ink, 0.82f);
                        }
                        _canvas.Pixels[i] = c;
                    }
                });
            }

            // A compass rose in whichever corner has least of the land in it.
            private void DrawCompass()
            {
                float inset = _r * 0.024f + _r * 0.085f;
                float radius = _r * 0.06f;
                Vector2[] corners = { new(_r - inset, _r - inset), new(inset, _r - inset), new(_r - inset, inset), new(inset, inset) };
                Vector2 centre = corners[0];
                int least = int.MaxValue;
                foreach (Vector2 corner in corners)
                {
                    int land = 0;
                    for (int j = -5; j <= 5; j++)
                    {
                        for (int i = -5; i <= 5; i++)
                        {
                            Vector2 p = corner + new Vector2(i, j) * radius * 0.25f;
                            int x = (int)p.X, y = (int)p.Y;
                            if (x < 0 || y < 0 || x >= _r || y >= _r) continue;
                            if (!_water[y * _r + x]) land++;
                            Vector2 w = _frame.ToWorld(p);
                            if (_world.Reachable != null && _world.Reachable.Sample(w) > 0.5f) land += 4;
                        }
                    }
                    if (land < least)
                    {
                        least = land;
                        centre = corner;
                    }
                }

                var pen = _canvas.All;
                float ink = MathF.Max(1.2f * _s, 0.8f);
                Vector3 light = Paper * new Vector3(1.02f, 1f, 0.95f);
                pen.Disc(centre, radius * 1.02f, Paper * new Vector3(1.01f, 0.98f, 0.92f), 0.88f);
                pen.Ring(centre, radius * 1.02f, ink * 0.8f, Ink, 0.7f);
                pen.Ring(centre, radius * 0.86f, ink, Ink, 0.9f);
                pen.Ring(centre, radius * 0.78f, ink * 0.7f, Ink, 0.8f);
                for (int k = 0; k < 32; k++)
                {
                    float a = MathF.Tau * k / 32f;
                    Vector2 dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
                    pen.Stroke(centre + dir * radius * 0.78f, centre + dir * radius * (k % 4 == 0 ? 0.92f : 0.86f), ink * 0.7f, Ink, 0.8f);
                }
                // Short points between, then the four long ones over them.
                for (int pass = 0; pass < 2; pass++)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        float a = MathF.PI * 0.5f * k + (pass == 0 ? MathF.PI * 0.25f : 0f);
                        float length = radius * (pass == 0 ? 0.62f : 1.05f);
                        float half = radius * (pass == 0 ? 0.1f : 0.14f);
                        Vector2 dir = new Vector2(MathF.Cos(a), MathF.Sin(a)), side = new Vector2(-dir.Y, dir.X);
                        Vector2 tip = centre + dir * length;
                        pen.Polygon(new[] { centre, tip, centre + side * half }, Ink * 1.3f, 1f, ink * 0.8f, Ink);
                        pen.Polygon(new[] { centre, centre - side * half, tip }, light, 1f, ink * 0.8f, Ink);
                    }
                }
                pen.Disc(centre, radius * 0.06f, Ink);

                // N, over the north point, in three strokes.
                Vector2 n = centre - new Vector2(0f, radius * 1.24f);
                float hn = radius * 0.14f, wn = radius * 0.1f;
                pen.Stroke(n + new Vector2(-wn, hn), n + new Vector2(-wn, -hn), ink * 1.3f, Ink);
                pen.Stroke(n + new Vector2(-wn, -hn), n + new Vector2(wn, hn), ink * 1.3f, Ink);
                pen.Stroke(n + new Vector2(wn, hn), n + new Vector2(wn, -hn), ink * 1.3f, Ink);
            }

            // The sheet's age over everything drawn on it: stains, fibres, and edges browned by handling.
            private void Age()
            {
                Parallel.For(0, _r, y =>
                {
                    for (int x = 0; x < _r; x++)
                    {
                        int i = y * _r + x;
                        Vector3 c = _canvas.Pixels[i];
                        float stain = Smooth(0.6f, 0.8f, ChartNoise.Fbm(x, y, 300f * _s, 4, 31u));
                        c *= Vector3.Lerp(Vector3.One, new Vector3(0.92f, 0.85f, 0.74f), stain * 0.45f);
                        float fibre = ChartNoise.Value(x * 0.35f, y, 2.5f * _s, 37u);
                        c *= 0.988f + 0.024f * fibre;
                        float edge = MathF.Min(MathF.Min(x, _r - 1 - x), MathF.Min(y, _r - 1 - y)) / (float)_r;
                        float handled = 1f - Smooth(0f, 0.1f, edge);
                        c = Vector3.Lerp(c, c * new Vector3(0.82f, 0.72f, 0.58f), handled * 0.6f);
                        _canvas.Pixels[i] = c;
                    }
                });
            }

            // The drawing before it was coloured: its ink and the tone of its washes in sepia on the sheet.
            // Its alpha says how much of the fog may stand there: all of it over the land anyone can walk to, and none over the sea or the mountains round it, which can't be
            // walked and are always on the map, so that the coloured part of the land is exactly where the hero has been.
            private byte[] Sketch(byte[] colour)
            {
                byte[] sketch = new byte[colour.Length];
                float paperLuma = Luma(Paper);
                var walked = new bool[_r * _r];
                ScalarField? reach = _world.Reachable;
                Parallel.For(0, _r, y =>
                {
                    for (int x = 0; x < _r; x++) walked[y * _r + x] = !_water[y * _r + x] && (reach == null || reach.Sample(World(x, y)) > 0.5f);
                });
                float[] fromWalked = ChartDistance.Transform(walked, _r, feature: true);
                Parallel.For(0, _r, y =>
                {
                    for (int x = 0; x < _r; x++)
                    {
                        int i = y * _r + x;
                        Vector3 c = _canvas.Pixels[i];
                        float tone = Math.Clamp(Luma(c) / paperLuma, 0f, 1.12f);
                        Vector3 sepia = Paper * new Vector3(1f, 0.97f, 0.9f) * MathF.Pow(tone, 1.5f);
                        Vector3 s = Vector3.Lerp(sepia, Paper * (PaperAt(x, y) / Paper), 0.34f);
                        s = Vector3.Lerp(s, c, 0.05f);
                        sketch[i * 4] = ChartCanvas.ToByte(s.X);
                        sketch[i * 4 + 1] = ChartCanvas.ToByte(s.Y);
                        sketch[i * 4 + 2] = ChartCanvas.ToByte(s.Z);
                        sketch[i * 4 + 3] = _water[i] ? (byte)0 : ChartCanvas.ToByte(1f - Smooth(10f * _s, 40f * _s, fromWalked[i]));
                    }
                });
                return sketch;
            }

            // Each biome's name where it has most room, if it has enough.
            private List<ChartLabel> RegionLabels()
            {
                const int Grid = 160;
                IReadOnlyList<BiomeDefinition> biomes = _world.Plan.Biomes;
                var labels = new List<ChartLabel>();
                if (biomes.Count < 2 || _world.Plan.Archetype.Arena) return labels;
                var dominant = new int[Grid * Grid];
                float inner = 0.06f;
                for (int y = 0; y < Grid; y++)
                {
                    for (int x = 0; x < Grid; x++)
                    {
                        Vector2 share = new Vector2((x + 0.5f) / Grid, (y + 0.5f) / Grid);
                        Vector2 p = _frame.ToWorld(share * _r);
                        int px = (int)(share.X * _r), py = (int)(share.Y * _r);
                        bool inside = share.X > inner && share.Y > inner && share.X < 1f - inner && share.Y < 1f - inner;
                        bool usable = inside && !_water[py * _r + px] && _world.Rock.Sample(p) < 0.4f;
                        dominant[y * Grid + x] = usable ? _world.DominantBiome(p) : -1;
                    }
                }
                for (int b = 0; b < biomes.Count; b++)
                {
                    var outside = new bool[Grid * Grid];
                    for (int i = 0; i < outside.Length; i++) outside[i] = dominant[i] != b;
                    float[] room = ChartDistance.Transform(outside, Grid, feature: true);
                    int best = -1;
                    float most = 0f;
                    for (int i = 0; i < room.Length; i++)
                    {
                        if (room[i] > most)
                        {
                            most = room[i];
                            best = i;
                        }
                    }
                    if (best < 0 || most / Grid < 0.05f) continue;
                    labels.Add(new ChartLabel(biomes[b].Name, new Vector2((best % Grid + 0.5f) / Grid, (best / Grid + 0.5f) / Grid), most / Grid));
                }
                return labels;
            }
        }
    }

    /// <summary>Felzenszwalb and Huttenlocher's exact Euclidean distance transform, in pixels.</summary>
    internal static class ChartDistance
    {
        /// <summary>How far each pixel is from the nearest one whose <paramref name="mask"/> equals <paramref name="feature"/>.</summary>
        public static float[] Transform(bool[] mask, int size, bool feature)
        {
            const float Far = 1e20f;
            var grid = new float[size * size];
            for (int i = 0; i < grid.Length; i++) grid[i] = mask[i] == feature ? 0f : Far;

            Parallel.For(0, size, x =>
            {
                var f = new float[size];
                var d = new float[size];
                var v = new int[size];
                var z = new float[size + 1];
                for (int y = 0; y < size; y++) f[y] = grid[y * size + x];
                Line(f, d, v, z, size);
                for (int y = 0; y < size; y++) grid[y * size + x] = d[y];
            });
            Parallel.For(0, size, y =>
            {
                var f = new float[size];
                var d = new float[size];
                var v = new int[size];
                var z = new float[size + 1];
                Array.Copy(grid, y * size, f, 0, size);
                Line(f, d, v, z, size);
                for (int x = 0; x < size; x++) grid[y * size + x] = MathF.Sqrt(d[x]);
            });
            return grid;
        }

        private static void Line(float[] f, float[] d, int[] v, float[] z, int n)
        {
            int k = 0;
            v[0] = 0;
            z[0] = float.NegativeInfinity;
            z[1] = float.PositiveInfinity;
            for (int q = 1; q < n; q++)
            {
                float s = ((f[q] + q * q) - (f[v[k]] + v[k] * v[k])) / (2f * q - 2f * v[k]);
                while (s <= z[k])
                {
                    k--;
                    s = ((f[q] + q * q) - (f[v[k]] + v[k] * v[k])) / (2f * q - 2f * v[k]);
                }
                k++;
                v[k] = q;
                z[k] = s;
                z[k + 1] = float.PositiveInfinity;
            }
            k = 0;
            for (int q = 0; q < n; q++)
            {
                while (z[k + 1] < q) k++;
                float dq = q - v[k];
                d[q] = dq * dq + f[v[k]];
            }
        }
    }
}

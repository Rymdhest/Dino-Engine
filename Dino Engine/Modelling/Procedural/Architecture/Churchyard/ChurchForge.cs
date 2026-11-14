using Dino_Engine.Modelling.Model;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Churchyard
{
    /// <summary>
    /// Builds a church from its <see cref="ChurchDesign"/>. Seen from 33 m up at 67° a church is mostly its roofs, its tower and the south side the porch is on, so that's where the work
    /// goes: steep slate roofs weathered in soft patches, coped gables with crosses, buttresses stepping back between the bays, windows of stained glass in stone surrounds with tracery in
    /// their heads and hoods over them, lit from inside by candles, as it's always night in a churchyard; a tower in three stages with its belfry, a clock, a parapet, pinnacles and a
    /// spire; a porch with its arch going dark. Headless, and touches nothing shared. Read CHURCHYARD.md.
    /// </summary>
    internal static class ChurchForge
    {
        /// <summary>Metres every wall reaches below the floor, so that no slope round a pad ever shows daylight under one.</summary>
        private const float Foot = -2.4f;

        /// <summary>
        /// The glass a church can have: its field, the figures in it, its border and its highlights, all deep and saturated, as glass that glows pale bleaches to white. Blue leads in
        /// every one, as the Reich's cathedral is oxblood and amber and nothing of the land's should be taken for it.
        /// </summary>
        public static readonly Vector3[][] Palettes =
        {
            new[] { new Vector3(0.03f, 0.08f, 0.6f), new Vector3(0.85f, 0.42f, 0.04f), new Vector3(0.04f, 0.2f, 0.62f), new Vector3(0.8f, 0.55f, 0.12f) },
            new[] { new Vector3(0.04f, 0.07f, 0.62f), new Vector3(0.55f, 0.03f, 0.12f), new Vector3(0.03f, 0.32f, 0.1f), new Vector3(0.82f, 0.48f, 0.08f) },
            new[] { new Vector3(0.05f, 0.14f, 0.6f), new Vector3(0.06f, 0.36f, 0.1f), new Vector3(0.85f, 0.42f, 0.05f), new Vector3(0.75f, 0.6f, 0.2f) },
            new[] { new Vector3(0.18f, 0.05f, 0.58f), new Vector3(0.03f, 0.12f, 0.66f), new Vector3(0.85f, 0.45f, 0.06f), new Vector3(0.45f, 0.05f, 0.3f) },
        };

        // Deep and saturated, with the light behind them doing the work, as glass that glows pale goes white once the picture's tone-mapped.
        private const float GlassGlow = 1.1f;

        public static Mesh Make(in YardSpec spec, List<AssetLight> lights)
        {
            ChurchDesign design = ChurchDesign.Of(spec.Seed);
            var b = new MeshBuilder();
            new Job(b, design, YardStone.Colour(spec, 0, 1), new WorldRandom(spec.Seed).Fork("forge"), lights).Build();
            return b.Build();
        }

        // ---------------------------------------------------------------- Frames and arches

        /// <summary>A wall's face: an origin on it at floor level, the way out of it, and the way along it that's to the right seen from outside.</summary>
        private readonly struct Frame
        {
            public readonly Vector3 O, U, N;

            public Frame(Vector3 origin, Vector3 outward)
            {
                O = origin;
                N = outward;
                U = Vector3.Cross(Vector3.UnitY, outward);
            }

            public Vector3 At(float u, float y, float d) => O + U * u + Vector3.UnitY * y + N * d;
        }

        /// <summary>
        /// A pointed arch across 2·<see cref="Half"/>, springing at <see cref="Spring"/> and rising <see cref="Rise"/> to its point: two arcs, each centred on the springing line across
        /// the middle, as a Gothic arch is struck.
        /// </summary>
        private readonly struct Pointed
        {
            public readonly float Half, Spring, Rise, C, R, ApexAngle;

            public Pointed(float half, float spring, float rise)
            {
                Half = half;
                Spring = spring;
                Rise = rise;
                C = (rise * rise - half * half) / (2f * half);
                R = C + half;
                ApexAngle = MathF.Atan2(rise, C);
            }

            public float Apex => Spring + Rise;

            /// <summary>Half the opening's width at a height.</summary>
            public float HalfAt(float y)
            {
                if (y <= Spring) return Half;
                float h = y - Spring;
                if (h >= Rise) return 0f;
                return MathF.Max(MathF.Sqrt(MathF.Max(R * R - h * h, 0f)) - C, 0f);
            }

            /// <summary>A point on the right half (<paramref name="side"/> 1) or the left (-1), from its springing (0) to its point (1), <paramref name="out_"/> outside the opening.</summary>
            public Vector2 On(float side, float s, float out_)
            {
                float a = s * ApexAngle;
                float r = R + out_;
                if (s >= 1f) return new Vector2(0f, Spring + MathF.Sin(ApexAngle) * r);
                return new Vector2(side * (-C + MathF.Cos(a) * r), Spring + MathF.Sin(a) * r);
            }
        }

        // ---------------------------------------------------------------- The job

        private sealed class Job
        {
            private readonly MeshBuilder _b;
            private readonly ChurchDesign _d;
            private readonly Vector3 _stone;
            private readonly WorldRandom _random;
            private readonly List<AssetLight> _lights;
            private readonly Vector3[] _glass;
            private readonly VertexMaterial _roofing;
            private readonly Vector3 _roofColour;
            private int _forks;
            private int _glassLights;

            public Job(MeshBuilder b, ChurchDesign d, Vector3 stone, WorldRandom random, List<AssetLight> lights)
            {
                _b = b;
                _d = d;
                _stone = stone;
                _random = random;
                _lights = lights;
                _glass = Palettes[d.Palette % Palettes.Length];
                _roofing = d.StoneSlates ? new VertexMaterial(Textures.MaterialCatalog.Slate.Index, new Colour(stone * 0.86f)) : YardStone.Slate(random.Fork("slate"));
                _roofColour = new Vector3(_roofing.colour.Red, _roofing.colour.Green, _roofing.colour.Blue);
            }

            private WorldRandom Next() => _random.Fork(++_forks);

            private VertexMaterial Coursed(float shade = 1f) => YardStone.Coursed(_stone, shade);
            private Func<Vector3, VertexMaterial> Dressed(float y, float moss = 0.3f) => YardStone.Facing(_stone, Next(), y, moss);

            public void Build()
            {
                Nave();
                if (_d.Aisles) AisleSides();
                Chancel();
                if (_d.HasTower) Tower();
                else Bellcote();
                Porch();
            }

            // ---------------------------------------------------------------- Walls

            // A block of walls, laid in courses, standing from the foot to `top`, its plinth round it and a band at its eaves.
            private void Walls(float x0, float x1, float z0, float z1, float top, bool plinth = true, bool eaves = true)
            {
                Vector3 centre = new Vector3((x0 + x1) * 0.5f, (Foot + top) * 0.5f, (z0 + z1) * 0.5f);
                _b.Box(centre, new Vector3((x1 - x0) * 0.5f, (top - Foot) * 0.5f, (z1 - z0) * 0.5f), Coursed());
                if (plinth) Plinth(x0, x1, z0, z1);
                if (eaves) Band(x0, x1, z0, z1, top - 0.32f, top, 0.16f);
            }

            // A plinth round the foot of a block of walls: a course standing out, its top weathered back into the wall.
            private void Plinth(float x0, float x1, float z0, float z1)
            {
                const float Out = 0.24f, Top = 0.85f;
                Vector3 min = new Vector3(x0 - Out, Foot + 0.2f, z0 - Out), max = new Vector3(x1 + Out, Top, z1 + Out);
                _b.Box((min + max) * 0.5f, (max - min) * 0.5f, Coursed(0.82f));
                // The weathering: a slope from the plinth's edge back to the wall.
                Vector3 a = new Vector3(x0 - Out, Top, z1 + Out), bb = new Vector3(x1 + Out, Top, z1 + Out), c = new Vector3(x1, Top + 0.22f, z1), d = new Vector3(x0, Top + 0.22f, z1);
                VertexMaterial m = YardStone.Dressed(_stone, Next(), 0.6f);
                YardStone.Face(_b, a, bb, c, d, new Vector3(0f, 1f, 1f), m);
                YardStone.Face(_b, new Vector3(x1 + Out, Top, z0 - Out), new Vector3(x0 - Out, Top, z0 - Out), new Vector3(x0, Top + 0.22f, z0), new Vector3(x1, Top + 0.22f, z0), new Vector3(0f, 1f, -1f), m);
                YardStone.Face(_b, new Vector3(x1 + Out, Top, z1 + Out), new Vector3(x1 + Out, Top, z0 - Out), new Vector3(x1, Top + 0.22f, z0), new Vector3(x1, Top + 0.22f, z1), new Vector3(1f, 1f, 0f), m);
                YardStone.Face(_b, new Vector3(x0 - Out, Top, z0 - Out), new Vector3(x0 - Out, Top, z1 + Out), new Vector3(x0, Top + 0.22f, z1), new Vector3(x0, Top + 0.22f, z0), new Vector3(-1f, 1f, 0f), m);
            }

            // A band of stone standing `out` proud of a block of walls, from y0 to y1: a string course, or the course under the eaves.
            private void Band(float x0, float x1, float z0, float z1, float y0, float y1, float out_)
            {
                Vector3 min = new Vector3(x0 - out_, y0, z0 - out_), max = new Vector3(x1 + out_, y1, z1 + out_);
                _b.Box((min + max) * 0.5f, (max - min) * 0.5f, Coursed(0.9f));
            }

            // A buttress against a wall at `u` along it: two or three stages, each standing back from the one under it, its top weathered back into the wall, and a pinnacle over it if
            // the walls have parapets.
            private void Buttress(Frame f, float u, float top, float depth = 1.05f, float width = 0.95f, bool pinnacle = false)
            {
                int stages = top > 7f ? 3 : 2;
                float y = Foot;
                for (int s = 0; s < stages; s++)
                {
                    float share = (s + 1f) / stages;
                    float to = top * (0.45f + 0.47f * share) - (s == stages - 1 ? 0f : 0f);
                    float d = depth * (1f - 0.24f * s);
                    float w = width * (1f - 0.06f * s);
                    float slope = MathF.Min(d * 0.9f, 1.1f);
                    var c = new[]
                    {
                        f.At(-w * 0.5f, y, d), f.At(w * 0.5f, y, d), f.At(w * 0.5f, to, d), f.At(-w * 0.5f, to, d),
                        f.At(-w * 0.5f, y, -0.05f), f.At(w * 0.5f, y, -0.05f), f.At(w * 0.5f, to + slope, -0.05f), f.At(-w * 0.5f, to + slope, -0.05f),
                    };
                    for (int k = 0; k < 8; k++) c[k] += f.U * u;
                    YardStone.Solid(_b, c, YardStone.Chamfer, Dressed(to, 0.5f));
                    y = to - 0.4f;
                }
                if (pinnacle)
                {
                    Vector3 foot = f.At(u, top + 0.4f, 0.45f);
                    Pinnacle(foot, 0.32f, 2.6f);
                }
            }

            // A pinnacle: a square shaft with a little gable on each face and a tall pyramid over it, a finial on its point.
            private void Pinnacle(Vector3 foot, float half, float height)
            {
                float shaft = height * 0.42f;
                YardStone.Block(_b, foot + new Vector3(0f, shaft * 0.5f, 0f), new Vector3(half, shaft * 0.5f, half), 0f, YardStone.Chamfer, Dressed(foot.Y + shaft));
                YardStone.Block(_b, foot + new Vector3(0f, shaft + 0.06f, 0f), new Vector3(half + 0.08f, 0.06f, half + 0.08f), 0f, 0.02f, Dressed(foot.Y + shaft, 0.4f));
                Spike(foot + new Vector3(0f, shaft + 0.12f, 0f), (half + 0.02f) * MathF.Sqrt(2f), height - shaft - 0.12f, 4, MathF.PI * 0.25f, YardStone.Dressed(_stone, Next(), foot.Y + height));
                YardStone.Block(_b, foot + new Vector3(0f, height + 0.08f, 0f), new Vector3(0.09f, 0.09f, 0.09f), MathF.PI * 0.25f, 0.02f, Dressed(foot.Y + height));
            }

            // A pyramid with `sides` flat faces, its corners `radius` from its axis, turned by `turn`, rising `height` from `foot`.
            private void Spike(Vector3 foot, float radius, float height, int sides, float turn, VertexMaterial m)
            {
                Vector3 apex = foot + Vector3.UnitY * height;
                for (int i = 0; i < sides; i++)
                {
                    float a0 = turn + i * MathF.Tau / sides, a1 = turn + (i + 1) * MathF.Tau / sides;
                    Vector3 p0 = foot + new Vector3(MathF.Cos(a0), 0f, MathF.Sin(a0)) * radius, p1 = foot + new Vector3(MathF.Cos(a1), 0f, MathF.Sin(a1)) * radius;
                    Vector3 mid = (p0 + p1) * 0.5f - foot;
                    YardStone.Face(_b, p0, p1, apex, new Vector3(mid.X, height * 0.3f, mid.Z), m);
                }
            }

            // ---------------------------------------------------------------- Roofs

            // Weathering that drifts over a roof in soft patches, lichen and a green bloom towards its eaves, worked out at each corner so no two neighbours are quite alike.
            private Colour RoofTint(Vector3 p, float eave, float ridge)
            {
                float n = Noise(p.X * 0.34f + p.Z * 0.11f, p.Y * 0.4f + p.Z * 0.29f);
                float low = 1f - Math.Clamp((p.Y - eave) / MathF.Max(ridge - eave, 0.1f), 0f, 1f);
                Vector3 c = _roofColour * (0.9f + 0.18f * n);
                c = Vector3.Lerp(c, c * new Vector3(0.86f, 1.02f, 0.78f), low * low * 0.7f);
                return new Colour(c);
            }

            private float Noise(float x, float y)
            {
                float s = MathF.Sin(x * 1.7f + _d.Seed % 97) * MathF.Sin(y * 2.3f + _d.Seed % 53);
                float t = MathF.Sin(x * 0.61f - y * 0.83f + _d.Seed % 31);
                return 0.5f + 0.32f * s + 0.18f * t;
            }

            // One slope of a roof, from its eave line (a to b) up to its ridge line (d to c), in cells whose corners carry the weathering, as a slab with its edge showing.
            private void Slope(Vector3 a, Vector3 bb, Vector3 c, Vector3 d, Vector3 outward, float thick = 0.24f)
            {
                Vector3 u = (bb - a).Normalized();
                Vector3 side = d - a;
                Vector3 v = (side - u * Vector3.Dot(side, u)).Normalized();
                if (Vector3.Dot(Vector3.Cross(u, v), outward) < 0f)
                {
                    (a, bb) = (bb, a);
                    (c, d) = (d, c);
                    u = -u;
                    side = d - a;
                    v = (side - u * Vector3.Dot(side, u)).Normalized();
                }
                float length = (bb - a).Length, rise = Vector3.Dot(d - a, v);
                float eave = MathF.Min(a.Y, bb.Y), ridge = MathF.Max(c.Y, d.Y);
                var bounds = new Vector4(0f, length, 0f, rise);
                int along = Math.Max(1, (int)MathF.Ceiling(length / 3.2f)), up = Math.Max(1, (int)MathF.Ceiling(rise / 3.2f));
                for (int j = 0; j < up; j++)
                {
                    for (int i = 0; i < along; i++)
                    {
                        float u0 = length * i / along, u1 = length * (i + 1) / along;
                        float v0 = rise * j / up, v1 = rise * (j + 1) / up;
                        // A roof that isn't a rectangle (an apse's facets) narrows towards its ridge: each cell's ends follow its sides.
                        Vector3 P(float uu, float vv)
                        {
                            float t = vv / MathF.Max(rise, 1e-3f);
                            Vector3 left = Vector3.Lerp(a, d, t), right = Vector3.Lerp(bb, c, t);
                            return Vector3.Lerp(left, right, uu / MathF.Max(length, 1e-3f));
                        }
                        _b.PlaneQuad(P(u0, v0), P(u1, v0), P(u1, v1), P(u0, v1), _roofing, a, u, v, bounds, p => RoofTint(p, eave, ridge));
                    }
                }
                // Its edge along the eaves and its underside.
                Vector3 down = -Vector3.Cross(u, v).Normalized() * thick;
                VertexMaterial edge = YardStone.Tint(_roofing, new Vector3(0.7f));
                YardStone.Face(_b, a, bb, bb + down, a + down, -v, edge);
                YardStone.Face(_b, a + down, bb + down, c + down, d + down, -Vector3.Cross(u, v), edge);
            }

            // A pitched roof whose ridge runs from `from` to `to` (on the ground, at the ridge line's ends), over walls `half` either side of it, its eaves at `eave`: two slopes, a
            // ridge of rolled lead or crested tiles, and its gables, coped in stone with a kneeler at each foot and a cross where `cross` asks.
            private void Roof(Vector2 from, Vector2 to, float half, float eave, float rise, bool gableFrom, bool gableTo, bool crossFrom, bool crossTo, float verge = 0.35f, float overhang = ChurchDesign.Eave)
            {
                Vector2 along2 = (to - from).Normalized();
                Vector3 along = new Vector3(along2.X, 0f, along2.Y);
                Vector3 across = Vector3.Cross(Vector3.UnitY, along);
                Vector3 f = new Vector3(from.X, 0f, from.Y) - along * verge, t = new Vector3(to.X, 0f, to.Y) + along * verge;
                float reach = half + overhang;
                float ridge = eave + reach * (rise / MathF.Max(half + ChurchDesign.Eave, 0.1f));
                float low = eave - 0.25f;
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector3 side = across * s;
                    Slope(f + side * reach + Vector3.UnitY * low, t + side * reach + Vector3.UnitY * low, t + Vector3.UnitY * ridge, f + Vector3.UnitY * ridge, side + Vector3.UnitY);
                }
                VertexMaterial lead = _d.StoneSlates ? YardStone.Tint(_roofing, new Vector3(0.82f)) : YardStone.Lead;
                _b.Tube(f + Vector3.UnitY * (ridge + 0.05f), t + Vector3.UnitY * (ridge + 0.05f), 0.2f, 0.2f, 6, lead);

                // The gables, in the walls' stone, and their copes.
                foreach ((Vector3 end, Vector3 outward, bool gable, bool cross) in new[] { (new Vector3(from.X, 0f, from.Y), -along, gableFrom, crossFrom), (new Vector3(to.X, 0f, to.Y), along, gableTo, crossTo) })
                {
                    if (!gable) continue;
                    // The gable's rake stays under the roof all the way up, and its cope stands over the roof, as a coped gable does.
                    Vector3 l = end + across * half + Vector3.UnitY * eave, r = end - across * half + Vector3.UnitY * eave;
                    YardStone.Face(_b, l, r, end + Vector3.UnitY * (ridge - 0.32f), outward, Coursed());
                    Cope(end + outward * 0.1f, across, half + 0.25f, eave - 0.1f, ridge + 0.05f, outward, cross);
                }
            }

            // The coping up both sides of a gable to its apex, a kneeler at each foot, and at the apex a cross or a finial.
            private void Cope(Vector3 end, Vector3 across, float half, float foot, float apex, Vector3 outward, bool cross)
            {
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector3 low = end + across * (s * half) + Vector3.UnitY * foot, high = end + Vector3.UnitY * apex;
                    int stones = 3;
                    for (int k = 0; k < stones; k++)
                    {
                        Vector3 p0 = Vector3.Lerp(low, high, k / (float)stones), p1 = Vector3.Lerp(low, high, (k + 1f) / stones);
                        Vector3 rake = (p1 - p0).Normalized();
                        Vector3 normal = Vector3.Cross(rake, outward).Normalized();
                        if (normal.Y < 0f) normal = -normal;
                        Vector3 sideways = outward * 0.32f;
                        var c = new[]
                        {
                            p0 + sideways, p1 + sideways, p1 + sideways + normal * 0.34f, p0 + sideways + normal * 0.34f,
                            p0 - sideways * 1.6f, p1 - sideways * 1.6f, p1 - sideways * 1.6f + normal * 0.34f, p0 - sideways * 1.6f + normal * 0.34f,
                        };
                        YardStone.Solid(_b, c, 0.025f, Dressed(p0.Y, 0.45f));
                    }
                    // The kneeler: a block at the foot of the cope that it stops against.
                    YardStone.Block(_b, low + Vector3.UnitY * 0.25f - across * (s * 0.2f), new Vector3(0.42f, 0.32f, 0.42f), MathF.Atan2(across.X, across.Z), YardStone.Chamfer, Dressed(foot + 0.3f, 0.5f));
                }
                Vector3 top = end + Vector3.UnitY * (apex + 0.3f);
                if (cross) Cross(top, outward, 1.7f);
                else YardStone.Block(_b, top + Vector3.UnitY * 0.25f, new Vector3(0.3f, 0.3f, 0.3f), MathF.PI * 0.25f, 0.03f, Dressed(apex));
            }

            // A stone cross standing on a gable's apex, its arms across the gable.
            private void Cross(Vector3 foot, Vector3 facing, float height)
            {
                float yaw = MathF.Atan2(facing.X, facing.Z);
                Func<Vector3, VertexMaterial> m = Dressed(foot.Y + height, 0.25f);
                YardStone.Block(_b, foot + new Vector3(0f, 0.18f, 0f), new Vector3(0.36f, 0.18f, 0.3f), yaw, 0.03f, m);
                YardStone.Block(_b, foot + new Vector3(0f, 0.36f + height * 0.5f, 0f), new Vector3(0.13f, height * 0.5f, 0.12f), yaw, 0.02f, m);
                YardStone.Block(_b, foot + new Vector3(0f, 0.36f + height * 0.66f, 0f), new Vector3(height * 0.3f, 0.12f, 0.12f), yaw, 0.02f, m);
            }

            // ---------------------------------------------------------------- Windows

            // A window in a wall: its sill, its jambs in long and short stones, the arch over it in voussoirs, a hood mould with its stops, and the glass lit from inside in a pattern
            // of quarries, lead between them, with mullions and tracery for as many lights as it has. `width` is the whole opening's; `lights` 1 makes a lancet.
            private void Window(Frame f, float u, float sill, float width, float height, int lights, bool light = false)
            {
                float half = width * 0.5f;
                float rise = lights == 1 ? half * 1.9f : half * 1.35f;
                float spring = sill + height - rise;
                var arch = new Pointed(half, spring, rise);
                const float Proud = 0.2f, Jamb = 0.34f, Ring = 0.36f;

                Glass(f, u, arch, sill, lights);

                // The sill, sloped to shed the rain.
                var sillStone = new[]
                {
                    f.At(u - half - Jamb, sill - 0.32f, Proud + 0.14f), f.At(u + half + Jamb, sill - 0.32f, Proud + 0.14f), f.At(u + half + Jamb, sill - 0.08f, Proud + 0.14f), f.At(u - half - Jamb, sill - 0.08f, Proud + 0.14f),
                    f.At(u - half - Jamb, sill - 0.32f, -0.04f), f.At(u + half + Jamb, sill - 0.32f, -0.04f), f.At(u + half + Jamb, sill + 0.06f, -0.04f), f.At(u - half - Jamb, sill + 0.06f, -0.04f),
                };
                YardStone.Solid(_b, sillStone, 0.02f, Dressed(sill));

                // Jambs: long and short stones up each side to the springing.
                foreach (float s in new[] { -1f, 1f })
                {
                    float y = sill;
                    int k = 0;
                    while (y < spring - 0.05f)
                    {
                        float h = MathF.Min(k % 2 == 0 ? 0.62f : 0.42f, spring - y);
                        float w = k % 2 == 0 ? Jamb : Jamb * 0.72f;
                        float inner = u + s * half, outer = u + s * (half + w);
                        Vector3 p0 = f.At(MathF.Min(inner, outer), y + 0.01f, 0f), p1 = f.At(MathF.Max(inner, outer), y + 0.01f, 0f);
                        var c = new[]
                        {
                            p0 + f.N * Proud, p1 + f.N * Proud, p1 + f.N * Proud + Vector3.UnitY * (h - 0.02f), p0 + f.N * Proud + Vector3.UnitY * (h - 0.02f),
                            p0 - f.N * 0.04f, p1 - f.N * 0.04f, p1 - f.N * 0.04f + Vector3.UnitY * (h - 0.02f), p0 - f.N * 0.04f + Vector3.UnitY * (h - 0.02f),
                        };
                        YardStone.Solid(_b, c, 0.025f, Dressed(y, 0.15f));
                        y += h;
                        k++;
                    }
                }

                // The arch in voussoirs, each side meeting the other at the point.
                int stones = lights == 1 ? 4 : 5;
                foreach (float s in new[] { -1f, 1f })
                {
                    for (int i = 0; i < stones; i++)
                    {
                        float s0 = i / (float)stones, s1 = (i + 1f) / stones;
                        Vector2 i0 = arch.On(s, s0, 0f), i1 = arch.On(s, s1, 0f), o0 = arch.On(s, s0, Ring), o1 = arch.On(s, s1, Ring);
                        if (s1 >= 1f) i1 = new Vector2(0f, arch.Apex);
                        var c = new[]
                        {
                            f.At(u + i0.X, i0.Y, Proud), f.At(u + i1.X, i1.Y, Proud), f.At(u + o1.X, o1.Y, Proud), f.At(u + o0.X, o0.Y, Proud),
                            f.At(u + i0.X, i0.Y, -0.04f), f.At(u + i1.X, i1.Y, -0.04f), f.At(u + o1.X, o1.Y, -0.04f), f.At(u + o0.X, o0.Y, -0.04f),
                        };
                        YardStone.Solid(_b, c, 0.022f, Dressed(i0.Y, 0.2f));
                    }
                }

                // The hood mould over the arch, standing out further, turned down at its ends onto stops.
                VertexMaterial hood = YardStone.Dressed(_stone, Next(), spring + rise, 0.03f);
                const int Steps = 7;
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector3 last = f.At(u + s * (half + Ring + 0.12f), spring - 0.02f, Proud + 0.12f);
                    for (int i = 1; i <= Steps; i++)
                    {
                        Vector2 p = arch.On(s, i / (float)Steps, Ring + 0.14f);
                        Vector3 here = f.At(u + p.X, p.Y, Proud + 0.12f);
                        _b.Tube(last, here, 0.085f, 0.085f, 5, hood, capped: false);
                        last = here;
                    }
                    Vector3 stop = f.At(u + s * (half + Ring + 0.12f), spring - 0.34f, Proud + 0.12f);
                    _b.Tube(stop, f.At(u + s * (half + Ring + 0.12f), spring + 0.02f, Proud + 0.12f), 0.085f, 0.085f, 5, hood, capped: false);
                    YardStone.Block(_b, stop - Vector3.UnitY * 0.1f, new Vector3(0.14f, 0.14f, 0.14f), MathF.Atan2(f.N.X, f.N.Z), 0.02f, Dressed(spring));
                }

                if (light && _glassLights < 6)
                {
                    _glassLights++;
                    Vector3 colour = Vector3.Lerp(_glass[0], _glass[1], 0.4f);
                    colour = Vector3.Lerp(colour, new Vector3(1f, 0.7f, 0.42f), 0.45f);
                    _lights.Add(AssetLight.StainedGlass(f.At(u, sill + (spring - sill) * 0.6f, 0.7f), f.N, colour) with { Intensity = 190f, Radius = 12f });
                }
            }

            // The glass: quarries in rows, lead showing between them, clipped to the opening; a border round it in one colour, a figure up its middle in another, the field in a
            // third and a few highlights. Mullions divide it into lights, each under its own arch, with a circle of tracery over them.
            private void Glass(Frame f, float u, Pointed arch, float sill, int lights)
            {
                const float Pane = 0.46f, Row = 0.58f, Lead = 0.035f, At = 0.05f;
                var random = Next();
                // The lead behind the panes, which is what shows between them.
                VertexMaterial lead = new VertexMaterial(Textures.MaterialCatalog.Iron.Index, new Colour(0.05f, 0.05f, 0.055f));
                for (float y = sill; y < arch.Apex - 0.02f; y += Row)
                {
                    float y1 = MathF.Min(y + Row, arch.Apex);
                    float w0 = arch.HalfAt(y), w1 = arch.HalfAt(y1);
                    if (w1 < 0.02f) w1 = 0f;
                    YardStone.Face(_b, f.At(u - w0, y, At - 0.02f), f.At(u + w0, y, At - 0.02f), f.At(u + w1, y1, At - 0.02f), f.At(u - w1, y1, At - 0.02f), f.N, lead);
                }

                float half = arch.Half;
                int row = 0;
                for (float y = sill + Lead; y < arch.Apex - 0.1f; y += Row, row++)
                {
                    float y1 = MathF.Min(y + Row - Lead, arch.Apex);
                    float clear = MathF.Min(arch.HalfAt(y), arch.HalfAt(y1)) - Lead;
                    if (clear < 0.06f)
                    {
                        // The point: one pane to the apex.
                        float w = arch.HalfAt(y) - Lead;
                        if (w > 0.05f) YardStone.Face(_b, f.At(u - w, y, At), f.At(u + w, y, At), f.At(u, arch.Apex - Lead, At), f.N, Pane3(1, row, 0, random));
                        break;
                    }
                    int col = 0;
                    for (float x = -half + Lead; x < half - 0.05f; x += Pane, col++)
                    {
                        float x0 = MathF.Max(x, -clear), x1 = MathF.Min(x + Pane - Lead, clear);
                        if (x1 - x0 < 0.07f) continue;
                        int kind;
                        if (half > 0.8f)
                        {
                            // A wide window: a border round it, a figure up its middle, and the field with a few highlights.
                            bool border = x0 <= -half + Pane * 0.9f || x1 >= half - Pane * 0.9f;
                            kind = border ? 2 : MathF.Abs((x0 + x1) * 0.5f) < Pane * 0.8f && row % 3 != 0 ? 1 : (row + col) % 5 == 0 ? 3 : 0;
                        }
                        else
                        {
                            // A lancet: the field, its figure in the middle third and a light in its head, as a narrow window is composed top to bottom.
                            float t = (y - sill) / MathF.Max(arch.Apex - sill, 0.1f);
                            kind = t > 0.8f ? 3 : t > 0.34f && t < 0.64f ? 1 : t < 0.1f ? 2 : 0;
                        }
                        YardStone.Face(_b, f.At(u + x0, y, At), f.At(u + x1, y, At), f.At(u + x1, y1, At), f.At(u + x0, y1, At), f.N, Pane3(kind, row, col, random));
                    }
                }

                // Mullions, each up to where its lights' own heads meet it, and the tracery over them.
                if (lights > 1)
                {
                    float lightHalf = half / lights;
                    var heads = new Pointed(lightHalf, arch.Spring - lightHalf * 0.4f, lightHalf * 1.4f);
                    VertexMaterial m = YardStone.Dressed(_stone, Next(), arch.Spring, 0.03f);
                    for (int k = 1; k < lights; k++)
                    {
                        float mu = u - half + k * 2f * lightHalf;
                        var c = new[]
                        {
                            f.At(mu - 0.1f, sill, 0.17f), f.At(mu + 0.1f, sill, 0.17f), f.At(mu + 0.1f, heads.Apex, 0.17f), f.At(mu - 0.1f, heads.Apex, 0.17f),
                            f.At(mu - 0.1f, sill, -0.02f), f.At(mu + 0.1f, sill, -0.02f), f.At(mu + 0.1f, heads.Apex, -0.02f), f.At(mu - 0.1f, heads.Apex, -0.02f),
                        };
                        YardStone.Solid(_b, c, 0.02f, _ => m);
                    }
                    // Each light's head, as a rib of stone.
                    for (int k = 0; k < lights; k++)
                    {
                        float lu = u - half + (2f * k + 1f) * lightHalf;
                        Rib(f, lu, heads, 0.13f, 0.08f, m);
                    }
                    // A circle (or three) in the head, over the lights.
                    float headTop = heads.Apex;
                    float room = arch.Apex - headTop;
                    if (lights == 2)
                    {
                        float r = MathF.Min(room * 0.42f, half * 0.42f);
                        Ring(f.At(u, headTop + room * 0.42f, 0.12f), f.U, r, m);
                    }
                    else
                    {
                        float r = MathF.Min(room * 0.3f, half * 0.24f);
                        Ring(f.At(u, headTop + room * 0.56f, 0.12f), f.U, r, m);
                        Ring(f.At(u - half * 0.42f, headTop + room * 0.18f, 0.12f), f.U, r * 0.8f, m);
                        Ring(f.At(u + half * 0.42f, headTop + room * 0.18f, 0.12f), f.U, r * 0.8f, m);
                    }
                }
            }

            private VertexMaterial Pane3(int kind, int row, int col, WorldRandom random)
            {
                Vector3 c = _glass[kind] * random.Range(0.82f, 1.12f);
                float glow = GlassGlow * (kind == 3 ? 1.15f : kind == 2 ? 0.9f : 1f) * random.Range(0.85f, 1.1f);
                return YardStone.Glow(Math.Clamp(c.X, 0f, 1f), Math.Clamp(c.Y, 0f, 1f), Math.Clamp(c.Z, 0f, 1f), glow);
            }

            // A rib of stone round a pointed arch.
            private void Rib(Frame f, float u, Pointed arch, float out_, float radius, VertexMaterial m)
            {
                const int Steps = 6;
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector3 last = f.At(u + s * arch.Half, arch.Spring, out_);
                    for (int i = 1; i <= Steps; i++)
                    {
                        Vector2 p = arch.On(s, i / (float)Steps, 0f);
                        Vector3 here = f.At(u + p.X, p.Y, out_);
                        _b.Tube(last, here, radius, radius, 5, m, capped: false);
                        last = here;
                    }
                }
            }

            // A ring of stone in a wall's plane: tracery's circle, or a clock's rim.
            private void Ring(Vector3 centre, Vector3 across, float radius, VertexMaterial m, float thick = 0.08f)
            {
                const int Sides = 12;
                for (int s = 0; s < Sides; s++)
                {
                    float a0 = s * MathF.Tau / Sides, a1 = (s + 1) * MathF.Tau / Sides;
                    _b.Tube(centre + across * MathF.Cos(a0) * radius + Vector3.UnitY * MathF.Sin(a0) * radius, centre + across * MathF.Cos(a1) * radius + Vector3.UnitY * MathF.Sin(a1) * radius, thick, thick, 5, m, capped: false);
                }
            }

            // ---------------------------------------------------------------- Doors

            // A pointed doorway: an oak door banded in old iron, and round it orders of moulding stepping out, each a ring of voussoirs on jambs, under a hood.
            private void Doorway(Frame f, float u, float width, float height, int orders)
            {
                float half = width * 0.5f;
                var arch = new Pointed(half, height - half * 1.15f, half * 1.15f);
                var random = Next();
                VertexMaterial oak = YardStone.Oak(random), iron = YardStone.Rust(random);
                // The door, in two leaves of boards, filling the arch.
                for (float y = 0f; y < arch.Apex - 0.02f; y += 0.5f)
                {
                    float y1 = MathF.Min(y + 0.5f, arch.Apex);
                    float w0 = arch.HalfAt(y), w1 = arch.HalfAt(y1);
                    YardStone.Face(_b, f.At(u - w0, y, 0.06f), f.At(u + w0, y, 0.06f), f.At(u + w1, y1, 0.06f), f.At(u - w1, y1, 0.06f), f.N, oak);
                }
                for (float y = 0.9f; y < arch.Spring; y += 1.6f)
                {
                    var strap = new[]
                    {
                        f.At(u - half + 0.1f, y, 0.11f), f.At(u + half - 0.1f, y, 0.11f), f.At(u + half - 0.1f, y + 0.16f, 0.11f), f.At(u - half + 0.1f, y + 0.16f, 0.11f),
                        f.At(u - half + 0.1f, y, 0.06f), f.At(u + half - 0.1f, y, 0.06f), f.At(u + half - 0.1f, y + 0.16f, 0.06f), f.At(u - half + 0.1f, y + 0.16f, 0.06f),
                    };
                    YardStone.Solid(_b, strap, 0.01f, iron);
                }
                YardStone.Solid(_b, new[]
                {
                    f.At(u - 0.03f, 0f, 0.1f), f.At(u + 0.03f, 0f, 0.1f), f.At(u + 0.03f, arch.Apex - 0.05f, 0.1f), f.At(u - 0.03f, arch.Apex - 0.05f, 0.1f),
                    f.At(u - 0.03f, 0f, 0.05f), f.At(u + 0.03f, 0f, 0.05f), f.At(u + 0.03f, arch.Apex - 0.05f, 0.05f), f.At(u - 0.03f, arch.Apex - 0.05f, 0.05f),
                }, 0.01f, YardStone.Tint(oak, new Vector3(0.6f)));

                // The orders, each wider and standing further out.
                for (int o = 0; o < orders; o++)
                {
                    float grow = o * 0.3f, depth = 0.16f + o * 0.16f;
                    var ring = new Pointed(half + grow, arch.Spring, arch.Rise + grow * 0.9f);
                    float y = -0.2f;
                    foreach (float s in new[] { -1f, 1f })
                    {
                        float inner = u + s * (half + grow), outer = u + s * (half + grow + 0.3f);
                        var c = new[]
                        {
                            f.At(MathF.Min(inner, outer), y, depth), f.At(MathF.Max(inner, outer), y, depth), f.At(MathF.Max(inner, outer), arch.Spring, depth), f.At(MathF.Min(inner, outer), arch.Spring, depth),
                            f.At(MathF.Min(inner, outer), y, -0.04f), f.At(MathF.Max(inner, outer), y, -0.04f), f.At(MathF.Max(inner, outer), arch.Spring, -0.04f), f.At(MathF.Min(inner, outer), arch.Spring, -0.04f),
                        };
                        YardStone.Solid(_b, c, 0.03f, Dressed(1f, 0.15f));
                        const int Stones = 5;
                        for (int i = 0; i < Stones; i++)
                        {
                            float s0 = i / (float)Stones, s1 = (i + 1f) / Stones;
                            Vector2 i0 = ring.On(s, s0, 0f), i1 = ring.On(s, s1, 0f), o0 = ring.On(s, s0, 0.3f), o1 = ring.On(s, s1, 0.3f);
                            if (s1 >= 1f) i1 = new Vector2(0f, ring.Apex);
                            var v = new[]
                            {
                                f.At(u + i0.X, i0.Y, depth), f.At(u + i1.X, i1.Y, depth), f.At(u + o1.X, o1.Y, depth), f.At(u + o0.X, o0.Y, depth),
                                f.At(u + i0.X, i0.Y, -0.04f), f.At(u + i1.X, i1.Y, -0.04f), f.At(u + o1.X, o1.Y, -0.04f), f.At(u + o0.X, o0.Y, -0.04f),
                            };
                            YardStone.Solid(_b, v, 0.025f, Dressed(i0.Y, 0.2f));
                        }
                    }
                }
            }

            // ---------------------------------------------------------------- The nave

            private void Nave()
            {
                ChurchDesign d = _d;
                float x0 = d.NaveFrom, x1 = d.NaveTo;
                Walls(x0, x1, -d.NaveHalf, d.NaveHalf, d.NaveWall, plinth: !d.Aisles, eaves: !d.Parapets);
                if (d.Parapets) Parapet(x0, x1, d.NaveHalf, d.NaveWall);
                float rise = (d.NaveHalf + ChurchDesign.Eave) * d.Pitch;
                // Behind a parapet its eaves are tucked inside the wall's top.
                Roof(new Vector2(x0, 0f), new Vector2(x1, 0f), d.NaveHalf, d.NaveWall, rise, gableFrom: true, gableTo: true, crossFrom: false, crossTo: true, overhang: d.Parapets ? -0.45f : ChurchDesign.Eave);

                foreach (float side in new[] { -1f, 1f })
                {
                    var wall = new Frame(new Vector3(0f, 0f, side * d.NaveHalf), new Vector3(0f, 0f, side));
                    float U(float x) => Vector3.Dot(new Vector3(x, 0f, 0f), wall.U);
                    if (d.Aisles)
                    {
                        // The clerestory, over the aisles' roofs: a small window in each bay.
                        float sill = d.AisleWall + (d.AisleWidth + ChurchDesign.Eave) * 0.5f + 0.7f;
                        for (int k = 0; k < d.Bays; k++)
                        {
                            float x = x0 + d.Bay * (k + 0.5f);
                            Window(wall, U(x), sill, 1.5f, d.NaveWall - sill - 0.7f, 2);
                        }
                        continue;
                    }
                    for (int k = 0; k <= d.Bays; k++)
                    {
                        float x = x0 + d.Bay * k;
                        if (k == 0 && d.HasTower) continue;
                        if (side > 0f && MathF.Abs(x - d.PorchX) < d.PorchHalf + 0.6f) continue;
                        Buttress(wall, U(x), d.NaveWall - 0.6f, pinnacle: d.Parapets);
                        // A lantern on every other buttress of the south side, which is what lights the church's face at night.
                        if (side > 0f && k % 2 == 1) Lantern(wall.At(U(x), 4.4f, 1.12f), wall.N, 90f, 11f);
                    }
                    for (int k = 0; k < d.Bays; k++)
                    {
                        float x = x0 + d.Bay * (k + 0.5f);
                        if (side > 0f && k == d.PorchBay) continue;
                        BayWindow(wall, U(x), d.NaveWall, light: side > 0f && k % 2 == (d.PorchBay + 1) % 2);
                    }
                }
            }

            // A bay's window, of the church's kind, sized to the wall it's in.
            private void BayWindow(Frame wall, float u, float wallTop, bool light)
            {
                float sill = MathF.Max(2.4f, wallTop * 0.3f);
                float height = wallTop - sill - 1.1f;
                switch (_d.Windows)
                {
                    case ChurchWindows.Lancets:
                        Window(wall, u - 0.75f, sill, 0.95f, height, 1, light);
                        Window(wall, u + 0.75f, sill, 0.95f, height, 1);
                        break;
                    case ChurchWindows.TwoLight:
                        Window(wall, u, sill, 2.3f, height, 2, light);
                        break;
                    default:
                        Window(wall, u, sill, 2.9f, height, 3, light);
                        break;
                }
            }

            // A parapet along both long walls of the nave, on a corbelled band, its top coped.
            private void Parapet(float x0, float x1, float half, float wallTop)
            {
                Band(x0, x1, -half, half, wallTop - 0.4f, wallTop, 0.2f);
                foreach (float side in new[] { -1f, 1f })
                {
                    float z = side * half;
                    Vector3 min = new Vector3(x0, wallTop, MathF.Min(z, z - side * 0.55f)), max = new Vector3(x1, wallTop + 1.0f, MathF.Max(z, z - side * 0.55f));
                    _b.Box((min + max) * 0.5f, (max - min) * 0.5f, Coursed(0.95f));
                    int stones = Math.Max(2, (int)MathF.Round((x1 - x0) / 1.4f));
                    for (int k = 0; k < stones; k++)
                    {
                        float a = x0 + (x1 - x0) * k / stones, bb = x0 + (x1 - x0) * (k + 1f) / stones;
                        YardStone.Box(_b, new Vector3(a + 0.01f, wallTop + 1.0f, MathF.Min(z + side * 0.1f, z - side * 0.65f)), new Vector3(bb - 0.01f, wallTop + 1.22f, MathF.Max(z + side * 0.1f, z - side * 0.65f)), 0.03f, Dressed(wallTop + 1f, 0.45f));
                    }
                }
            }

            private void AisleSides()
            {
                ChurchDesign d = _d;
                float x0 = d.NaveFrom, x1 = d.NaveTo;
                float roofRise = (d.AisleWidth + ChurchDesign.Eave) * 0.5f;
                foreach (float side in new[] { -1f, 1f })
                {
                    float zIn = side * d.NaveHalf, zOut = side * d.BodyHalf;
                    Walls(x0, x1, MathF.Min(zIn, zOut), MathF.Max(zIn, zOut), d.AisleWall);
                    // Its lean-to roof, from its eaves up to the nave's wall.
                    Vector3 e0 = new Vector3(x0 - 0.3f, d.AisleWall - 0.25f, zOut + side * ChurchDesign.Eave), e1 = new Vector3(x1 + 0.3f, d.AisleWall - 0.25f, zOut + side * ChurchDesign.Eave);
                    Vector3 r1 = new Vector3(x1 + 0.3f, d.AisleWall + roofRise, zIn), r0 = new Vector3(x0 - 0.3f, d.AisleWall + roofRise, zIn);
                    Slope(e0, e1, r1, r0, new Vector3(0f, 1f, side));
                    _b.Tube(r0 + new Vector3(0f, 0.05f, -side * 0.05f), r1 + new Vector3(0f, 0.05f, -side * 0.05f), 0.14f, 0.14f, 6, YardStone.Lead);
                    // The triangles of wall under its ends.
                    foreach ((float x, float outward) in new[] { (x0, -1f), (x1, 1f) })
                    {
                        YardStone.Face(_b, new Vector3(x, d.AisleWall, zOut), new Vector3(x, d.AisleWall, zIn), new Vector3(x, d.AisleWall + roofRise, zIn), new Vector3(outward, 0f, 0f), Coursed());
                    }

                    var wall = new Frame(new Vector3(0f, 0f, zOut), new Vector3(0f, 0f, side));
                    float U(float x) => Vector3.Dot(new Vector3(x, 0f, 0f), wall.U);
                    for (int k = 0; k <= d.Bays; k++)
                    {
                        float x = x0 + d.Bay * k;
                        if (k == 0 && d.HasTower && d.TowerHalf > d.NaveHalf + 0.5f) continue;
                        if (side > 0f && MathF.Abs(x - d.PorchX) < d.PorchHalf + 0.6f) continue;
                        Buttress(wall, U(x), d.AisleWall - 0.3f, depth: 0.9f);
                        if (side > 0f && k % 2 == 1) Lantern(wall.At(U(x), 3.8f, 0.97f), wall.N, 90f, 11f);
                    }
                    for (int k = 0; k < d.Bays; k++)
                    {
                        float x = x0 + d.Bay * (k + 0.5f);
                        if (side > 0f && k == d.PorchBay) continue;
                        float sill = 1.9f;
                        bool light = side > 0f && k % 2 == (d.PorchBay + 1) % 2;
                        if (d.Windows == ChurchWindows.Lancets)
                        {
                            Window(wall, U(x) - 0.7f, sill, 0.9f, d.AisleWall - sill - 0.75f, 1, light);
                            Window(wall, U(x) + 0.7f, sill, 0.9f, d.AisleWall - sill - 0.75f, 1);
                        }
                        else Window(wall, U(x), sill, 2.2f, d.AisleWall - sill - 0.75f, 2, light);
                    }
                }
            }

            // ---------------------------------------------------------------- The chancel

            private void Chancel()
            {
                ChurchDesign d = _d;
                float x0 = d.ChancelFrom - 0.3f, x1 = d.ChancelTo;
                Walls(x0, x1, -d.ChancelHalf, d.ChancelHalf, d.ChancelWall);
                float rise = (d.ChancelHalf + ChurchDesign.Eave) * d.Pitch;
                Roof(new Vector2(x0, 0f), new Vector2(x1, 0f), d.ChancelHalf, d.ChancelWall, rise, gableFrom: false, gableTo: !d.Apse, crossFrom: false, crossTo: !d.Apse, verge: d.Apse ? 0f : 0.35f);

                foreach (float side in new[] { -1f, 1f })
                {
                    var wall = new Frame(new Vector3(0f, 0f, side * d.ChancelHalf), new Vector3(0f, 0f, side));
                    float U(float x) => Vector3.Dot(new Vector3(x, 0f, 0f), wall.U);
                    for (int k = 1; k <= d.ChancelBays; k++)
                    {
                        float x = d.ChancelFrom + d.ChancelBay * k;
                        if (k == d.ChancelBays && !d.Apse) continue;
                        Buttress(wall, U(x), d.ChancelWall - 0.6f, depth: 0.95f);
                    }
                    for (int k = 0; k < d.ChancelBays; k++)
                    {
                        float x = d.ChancelFrom + d.ChancelBay * (k + 0.5f);
                        // The priest's door, in the first bay of the south side, under a small window.
                        if (side > 0f && k == 0)
                        {
                            Doorway(wall, U(x), 1.8f, 4.2f, 1);
                            Lantern(wall.At(U(x) + 1.7f, 3.6f, 0.15f), wall.N);
                            continue;
                        }
                        Window(wall, U(x), 2.6f, 1.05f, d.ChancelWall - 3.6f, 1, light: side > 0f);
                    }
                }

                if (!d.Apse)
                {
                    // A flat east end, with a great window, and diagonal buttresses at its corners.
                    var east = new Frame(new Vector3(x1, 0f, 0f), Vector3.UnitX);
                    int lights = _d.ChancelHalf > 4.4f ? 3 : 2;
                    Window(east, 0f, 2.8f, MathF.Min(_d.ChancelHalf * 1.15f, 4.6f), d.ChancelWall + rise * 0.55f - 2.8f, lights, light: true);
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var corner = new Frame(new Vector3(x1, 0f, side * d.ChancelHalf), new Vector3(1f, 0f, side).Normalized());
                        Buttress(corner, 0f, d.ChancelWall - 0.4f, depth: 1.1f);
                    }
                    return;
                }

                // A half-octagon apse: three faces, each with a tall lancet, buttresses at its corners, and its roof in facets up to the chancel's ridge.
                float r = d.ChancelHalf;
                float ridge = d.ChancelWall + rise;
                Vector3[] corners =
                {
                    new Vector3(x1, 0f, r), new Vector3(x1 + d.ApseDepth * 0.72f, 0f, r * 0.45f), new Vector3(x1 + d.ApseDepth * 0.72f, 0f, -r * 0.45f), new Vector3(x1, 0f, -r),
                };
                for (int i = 0; i < 3; i++)
                {
                    Vector3 a = corners[i], c = corners[i + 1];
                    Vector3 along = (c - a).Normalized();
                    Vector3 outward = Vector3.Cross(along, Vector3.UnitY).Normalized();
                    if (Vector3.Dot(outward, (a + c) * 0.5f - new Vector3(x1, 0f, 0f)) < 0f) outward = -outward;
                    YardStone.Face(_b, a + Vector3.UnitY * Foot, c + Vector3.UnitY * Foot, c + Vector3.UnitY * d.ChancelWall, a + Vector3.UnitY * d.ChancelWall, outward, Coursed());
                    var face = new Frame((a + c) * 0.5f, outward);
                    Window(face, 0f, 2.6f, MathF.Min((c - a).Length * 0.4f, 1.3f), d.ChancelWall - 3.4f, 1, light: i == 1);
                    // Its eaves course and plinth, along the face.
                    Vector3 eave0 = a + outward * 0.16f, eave1 = c + outward * 0.16f;
                    YardStone.Face(_b, eave0 + Vector3.UnitY * (d.ChancelWall - 0.32f), eave1 + Vector3.UnitY * (d.ChancelWall - 0.32f), eave1 + Vector3.UnitY * d.ChancelWall, eave0 + Vector3.UnitY * d.ChancelWall, outward, Coursed(0.9f));
                    Vector3 p0 = a + outward * 0.24f, p1 = c + outward * 0.24f;
                    YardStone.Face(_b, p0 + Vector3.UnitY * Foot, p1 + Vector3.UnitY * Foot, p1 + Vector3.UnitY * 0.85f, p0 + Vector3.UnitY * 0.85f, outward, Coursed(0.82f));
                    // The roof's facet over this face, from its eaves to the ridge's end.
                    Vector3 ea = a + outward * ChurchDesign.Eave + Vector3.UnitY * (d.ChancelWall - 0.25f), ec = c + outward * ChurchDesign.Eave + Vector3.UnitY * (d.ChancelWall - 0.25f);
                    Vector3 apex = new Vector3(x1, ridge, 0f);
                    Slope(ea, ec, apex + new Vector3(0.001f, 0f, 0f), apex, outward + Vector3.UnitY);
                }
                // Where the chancel's slopes end and the apse's first and last facets begin, a triangle of roof closes the corner between them.
                foreach ((Vector3 corner, Vector3 next, float side) in new[] { (corners[0], corners[1], 1f), (corners[3], corners[2], -1f) })
                {
                    Vector3 along = (next - corner).Normalized();
                    Vector3 outward = Vector3.Cross(along, Vector3.UnitY).Normalized();
                    if (Vector3.Dot(outward, (corner + next) * 0.5f - new Vector3(x1, 0f, 0f)) < 0f) outward = -outward;
                    Vector3 chancelEave = new Vector3(x1, d.ChancelWall - 0.25f, side * (r + ChurchDesign.Eave));
                    Vector3 facetEave = corner + outward * ChurchDesign.Eave + Vector3.UnitY * (d.ChancelWall - 0.25f);
                    YardStone.Face(_b, chancelEave, facetEave, new Vector3(x1, ridge, 0f), new Vector3(0.3f, 1f, side), _roofing);
                }
                for (int i = 1; i < 3; i++)
                {
                    Vector3 outward = new Vector3(corners[i].X - x1, 0f, corners[i].Z).Normalized();
                    Buttress(new Frame(corners[i], outward), 0f, d.ChancelWall - 0.4f, depth: 1.0f);
                }
                YardStone.Block(_b, new Vector3(x1, ridge + 0.5f, 0f), new Vector3(0.3f, 0.5f, 0.3f), MathF.PI * 0.25f, 0.03f, Dressed(ridge));
                Cross(new Vector3(x1, ridge + 1f, 0f), Vector3.UnitX, 1.5f);
            }

            // ---------------------------------------------------------------- The tower

            private void Tower()
            {
                ChurchDesign d = _d;
                float h = d.TowerHalf, x = d.TowerX;
                float parapet = d.Tower == ChurchTower.Pinnacles ? 1.5f : 1.1f;
                float shaft = d.TowerTop - parapet;
                Walls(x - h, x + h, -h, h, shaft, eaves: false);
                float first = shaft * 0.38f, second = shaft * 0.7f;
                Band(x - h, x + h, -h, h, first - 0.2f, first + 0.12f, 0.16f);
                Band(x - h, x + h, -h, h, second - 0.2f, second + 0.12f, 0.16f);
                Band(x - h, x + h, -h, h, shaft - 0.5f, shaft, 0.22f);

                // Diagonal buttresses at its corners, stepping back up to the belfry.
                foreach (float sx in new[] { -1f, 1f })
                {
                    foreach (float sz in new[] { -1f, 1f })
                    {
                        var corner = new Frame(new Vector3(x + sx * h, 0f, sz * h), new Vector3(sx, 0f, sz).Normalized());
                        if (sx > 0f && h < d.BodyHalf) continue;
                        Buttress(corner, 0f, second + 0.5f, depth: 1.25f, width: 1.05f);
                    }
                }

                // The west door, and a window over it.
                var west = new Frame(new Vector3(x - h, 0f, 0f), -Vector3.UnitX);
                Doorway(west, 0f, 2.6f, 5.4f, 3);
                Lantern(west.At(2.4f, 4.6f, 0.15f), west.N);
                Window(west, 0f, first + 0.7f, 2.0f, second - first - 1.6f, 2, light: true);

                // A narrow lancet on the other faces of the middle stage, and the clock on its south face.
                foreach (Vector3 n in new[] { Vector3.UnitZ, -Vector3.UnitZ })
                {
                    var face = new Frame(new Vector3(x, 0f, 0f) + n * h, n);
                    if (n.Z > 0f && d.Clock) Clock(face, (first + second) * 0.5f + 0.4f, MathF.Min(h * 0.55f, 2f));
                    else Window(face, 0f, first + 1.2f, 0.8f, second - first - 2.2f, 1);
                    Window(face, 0f, 3.2f, 0.7f, first - 4.2f, 1);
                }

                // The belfry: on every face, two openings with louvres in them.
                foreach (Vector3 n in new[] { Vector3.UnitZ, -Vector3.UnitZ, Vector3.UnitX, -Vector3.UnitX })
                {
                    var face = new Frame(new Vector3(x, 0f, 0f) + n * h, n);
                    Belfry(face, -h * 0.4f, second + 0.7f, shaft - 1.1f);
                    Belfry(face, h * 0.4f, second + 0.7f, shaft - 1.1f);
                }

                // Its top.
                Vector3 top = new Vector3(x, shaft, 0f);
                _b.Box(top + new Vector3(0f, 0.05f, 0f), new Vector3(h - 0.5f, 0.05f, h - 0.5f), YardStone.Lead);
                switch (d.Tower)
                {
                    case ChurchTower.Pinnacles:
                        Battlements(top, h + 0.2f, parapet);
                        foreach (float sx in new[] { -1f, 1f })
                        {
                            foreach (float sz in new[] { -1f, 1f })
                            {
                                Pinnacle(top + new Vector3(sx * (h - 0.2f), parapet + 0.1f, sz * (h - 0.2f)), 0.45f, 3.6f);
                                Gargoyle(top + new Vector3(sx * (h + 0.2f), -0.6f, sz * (h + 0.2f)), new Vector3(sx, 0f, sz).Normalized());
                            }
                        }
                        break;
                    case ChurchTower.Spire:
                        PlainParapet(top, h + 0.2f, parapet);
                        foreach (float sx in new[] { -1f, 1f })
                        {
                            foreach (float sz in new[] { -1f, 1f })
                            {
                                Pinnacle(top + new Vector3(sx * (h - 0.15f), parapet + 0.1f, sz * (h - 0.15f)), 0.36f, 2.6f);
                            }
                        }
                        Spire(top, h - 0.7f, d.SpireHeight + parapet, false);
                        break;
                    case ChurchTower.Broach:
                        Band(x - h, x + h, -h, h, shaft, shaft + 0.3f, 0.12f);
                        Spire(top + new Vector3(0f, 0.3f, 0f), h, d.SpireHeight, true);
                        break;
                }
            }

            // A belfry opening: a lancet's surround with louvres of oak across the dark inside it.
            private void Belfry(Frame f, float u, float sill, float top)
            {
                float width = MathF.Min(_d.TowerHalf * 0.62f, 1.35f);
                float half = width * 0.5f;
                float rise = half * 1.7f;
                var arch = new Pointed(half, top - rise, rise);
                var random = Next();
                for (float y = sill; y < arch.Apex - 0.02f; y += 0.5f)
                {
                    float y1 = MathF.Min(y + 0.5f, arch.Apex);
                    YardStone.Face(_b, f.At(u - arch.HalfAt(y), y, 0.03f), f.At(u + arch.HalfAt(y), y, 0.03f), f.At(u + arch.HalfAt(y1), y1, 0.03f), f.At(u - arch.HalfAt(y1), y1, 0.03f), f.N, YardStone.Dark);
                }
                VertexMaterial slat = YardStone.Silvered(random);
                for (float y = sill + 0.25f; y < arch.Spring + rise * 0.4f; y += 0.42f)
                {
                    float w = arch.HalfAt(y + 0.2f) - 0.02f;
                    if (w < 0.1f) break;
                    var c = new[]
                    {
                        f.At(u - w, y, 0.18f), f.At(u + w, y, 0.18f), f.At(u + w, y + 0.26f, 0.06f), f.At(u - w, y + 0.26f, 0.06f),
                        f.At(u - w, y - 0.04f, 0.14f), f.At(u + w, y - 0.04f, 0.14f), f.At(u + w, y + 0.22f, 0.02f), f.At(u - w, y + 0.22f, 0.02f),
                    };
                    YardStone.Solid(_b, c, 0.01f, slat);
                }
                Rib(f, u, arch, 0.14f, 0.13f, YardStone.Dressed(_stone, Next(), top, 0.03f));
                YardStone.Solid(_b, new[]
                {
                    f.At(u - half - 0.2f, sill - 0.25f, 0.3f), f.At(u + half + 0.2f, sill - 0.25f, 0.3f), f.At(u + half + 0.2f, sill, 0.3f), f.At(u - half - 0.2f, sill, 0.3f),
                    f.At(u - half - 0.2f, sill - 0.25f, -0.04f), f.At(u + half + 0.2f, sill - 0.25f, -0.04f), f.At(u + half + 0.2f, sill + 0.1f, -0.04f), f.At(u - half - 0.2f, sill + 0.1f, -0.04f),
                }, 0.02f, Dressed(sill));
                foreach (float s in new[] { -1f, 1f })
                {
                    YardStone.Solid(_b, new[]
                    {
                        f.At(u + s * half, sill, 0.16f), f.At(u + s * (half + 0.22f), sill, 0.16f), f.At(u + s * (half + 0.22f), arch.Spring, 0.16f), f.At(u + s * half, arch.Spring, 0.16f),
                        f.At(u + s * half, sill, -0.04f), f.At(u + s * (half + 0.22f), sill, -0.04f), f.At(u + s * (half + 0.22f), arch.Spring, -0.04f), f.At(u + s * half, arch.Spring, -0.04f),
                    }, 0.02f, Dressed(sill + 1f, 0.1f));
                }
            }

            // A clock face on the tower: a round stone dial with a ring of hours, and its two hands in old iron, stopped.
            private void Clock(Frame f, float y, float radius)
            {
                var random = Next();
                Vector3 centre = f.At(0f, y, 0.12f);
                Vector3 across = f.U;
                VertexMaterial dial = YardStone.Tint(YardStone.Dressed(_stone, random, y, 0.02f), new Vector3(1.12f));
                const int Sides = 20;
                for (int s = 0; s < Sides; s++)
                {
                    float a0 = s * MathF.Tau / Sides, a1 = (s + 1) * MathF.Tau / Sides;
                    YardStone.Face(_b, centre, centre + (across * MathF.Cos(a0) + Vector3.UnitY * MathF.Sin(a0)) * radius, centre + (across * MathF.Cos(a1) + Vector3.UnitY * MathF.Sin(a1)) * radius, f.N, dial);
                }
                Ring(centre + f.N * 0.03f, across, radius + 0.05f, YardStone.Dressed(_stone, random, y, 0.02f), 0.12f);
                VertexMaterial iron = YardStone.Rust(random, 0.7f);
                for (int hour = 0; hour < 12; hour++)
                {
                    float a = hour * MathF.Tau / 12f;
                    Vector3 dir = across * MathF.Cos(a) + Vector3.UnitY * MathF.Sin(a);
                    _b.Beam(centre + f.N * 0.04f + dir * radius * 0.78f, centre + f.N * 0.04f + dir * radius * 0.92f, 0.05f, iron);
                }
                // Stopped, as nobody has wound it since the ships came.
                float hourHand = random.Range(0f, MathF.Tau), minuteHand = random.Range(0f, MathF.Tau);
                _b.Beam(centre + f.N * 0.08f, centre + f.N * 0.08f + (across * MathF.Cos(hourHand) + Vector3.UnitY * MathF.Sin(hourHand)) * radius * 0.5f, 0.06f, iron);
                _b.Beam(centre + f.N * 0.1f, centre + f.N * 0.1f + (across * MathF.Cos(minuteHand) + Vector3.UnitY * MathF.Sin(minuteHand)) * radius * 0.74f, 0.045f, iron);
            }

            // A battlemented parapet round a tower's top: merlons with the gaps between them, all coped.
            private void Battlements(Vector3 top, float half, float height)
            {
                foreach (Vector3 n in new[] { Vector3.UnitZ, -Vector3.UnitZ, Vector3.UnitX, -Vector3.UnitX })
                {
                    Vector3 along = Vector3.Cross(Vector3.UnitY, n);
                    _b.Box(top + n * (half - 0.3f) + Vector3.UnitY * (height * 0.3f), Abs(along * half + n * 0.3f) + Vector3.UnitY * (height * 0.3f), Coursed(0.95f));
                    int merlons = Math.Max(2, (int)MathF.Round(half * 2f / 1.5f));
                    float step = half * 2f / merlons;
                    for (int k = 0; k < merlons; k++)
                    {
                        float a = -half + step * k + step * 0.12f, bb = -half + step * (k + 0.55f);
                        Vector3 p = top + n * (half - 0.3f) + along * ((a + bb) * 0.5f) + Vector3.UnitY * (height * 0.6f + height * 0.2f);
                        YardStone.Block(_b, p, new Vector3((bb - a) * 0.5f, height * 0.2f + 0.02f, 0.32f), MathF.Atan2(n.X, n.Z), YardStone.Chamfer, Dressed(top.Y + height, 0.4f));
                    }
                }
            }

            // A plain parapet round a tower's top, with a moulded cope.
            private void PlainParapet(Vector3 top, float half, float height)
            {
                foreach (Vector3 n in new[] { Vector3.UnitZ, -Vector3.UnitZ, Vector3.UnitX, -Vector3.UnitX })
                {
                    Vector3 along = Vector3.Cross(Vector3.UnitY, n);
                    _b.Box(top + n * (half - 0.3f) + Vector3.UnitY * (height * 0.5f), Abs(along * half + n * 0.3f) + Vector3.UnitY * (height * 0.5f), Coursed(0.95f));
                    YardStone.Block(_b, top + n * (half - 0.3f) + Vector3.UnitY * (height + 0.1f), Abs(along * (half + 0.05f)) + new Vector3(0f, 0.12f, 0f) + Abs(n * 0.38f), 0f, 0.03f, Dressed(top.Y + height, 0.4f));
                }
            }

            private static Vector3 Abs(Vector3 v) => new Vector3(MathF.Abs(v.X), MathF.Abs(v.Y), MathF.Abs(v.Z));

            // A gargoyle out from under a tower's parapet at a corner: a crouching beast of stone with its mouth open over the drop, as rain spouts were carved.
            private void Gargoyle(Vector3 at, Vector3 outward)
            {
                float yaw = MathF.Atan2(outward.X, outward.Z);
                Func<Vector3, VertexMaterial> m = Dressed(at.Y, 0.3f);
                YardStone.Block(_b, at + outward * 0.5f, new Vector3(0.3f, 0.28f, 0.7f), yaw, 0.05f, m);
                YardStone.Block(_b, at + outward * 1.25f + Vector3.UnitY * 0.06f, new Vector3(0.26f, 0.24f, 0.26f), yaw, 0.06f, m);
                YardStone.Block(_b, at + outward * 1.5f - Vector3.UnitY * 0.08f, new Vector3(0.16f, 0.08f, 0.16f), yaw, 0.03f, YardStone.Dark);
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector3 side = Vector3.Cross(Vector3.UnitY, outward) * s;
                    YardStone.Block(_b, at + outward * 1.3f + side * 0.2f + Vector3.UnitY * 0.34f, new Vector3(0.06f, 0.14f, 0.06f), yaw, 0.02f, m);
                }
            }

            // An octagonal spire rising from `foot`: of stone in courses with bands round it, or of slate with lucarnes in its faces; with broaches at its foot if it stands straight
            // off the tower; and an iron cross on a ball at its point.
            private void Spire(Vector3 foot, float apothem, float height, bool broaches)
            {
                const int Sides = 8;
                float radius = apothem / MathF.Cos(MathF.PI / Sides);
                float turn = MathF.PI / Sides;
                var random = Next();
                VertexMaterial skin = _d.StoneSpire ? YardStone.Coursed(_stone, 0.92f) : _roofing;
                Vector3 apex = foot + Vector3.UnitY * height;
                for (int i = 0; i < Sides; i++)
                {
                    float a0 = turn + i * MathF.Tau / Sides, a1 = turn + (i + 1) * MathF.Tau / Sides;
                    Vector3 p0 = foot + new Vector3(MathF.Cos(a0), 0f, MathF.Sin(a0)) * radius, p1 = foot + new Vector3(MathF.Cos(a1), 0f, MathF.Sin(a1)) * radius;
                    Vector3 mid = (p0 + p1) * 0.5f - foot;
                    YardStone.Face(_b, p0, p1, apex, new Vector3(mid.X, apothem / height * apothem, mid.Z), skin);
                }
                if (_d.StoneSpire)
                {
                    foreach (float t in new[] { 0.3f, 0.62f })
                    {
                        float y = foot.Y + height * t;
                        float r = radius * (1f - t) + 0.08f;
                        for (int i = 0; i < Sides; i++)
                        {
                            float a0 = turn + i * MathF.Tau / Sides, a1 = turn + (i + 1) * MathF.Tau / Sides;
                            _b.Tube(new Vector3(foot.X + MathF.Cos(a0) * r, y, foot.Z + MathF.Sin(a0) * r), new Vector3(foot.X + MathF.Cos(a1) * r, y, foot.Z + MathF.Sin(a1) * r), 0.1f, 0.1f, 4, YardStone.Dressed(_stone, random, y, 0.03f), capped: false);
                        }
                    }
                }
                else
                {
                    // Lucarnes: little gabled openings on the four faces that look out over the tower's faces.
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i * MathF.PI * 0.5f;
                        Vector3 n = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a));
                        float t = 0.22f;
                        Vector3 at = foot + n * (apothem * (1f - t)) + Vector3.UnitY * (height * t);
                        Vector3 along = Vector3.Cross(Vector3.UnitY, n);
                        float yaw = MathF.Atan2(n.X, n.Z);
                        YardStone.Block(_b, at + n * 0.1f + Vector3.UnitY * 0.5f, new Vector3(0.5f, 0.6f, 0.45f), yaw, 0.02f, _ => _roofing);
                        YardStone.Face(_b, at + n * 0.56f + along * -0.32f, at + n * 0.56f + along * 0.32f, at + n * 0.56f + along * 0.32f + Vector3.UnitY * 0.8f, at + n * 0.56f + along * -0.32f + Vector3.UnitY * 0.8f, n, YardStone.Dark);
                        YardStone.Face(_b, at + n * 0.56f + along * -0.55f + Vector3.UnitY * 1.1f, at + n * 0.56f + along * 0.55f + Vector3.UnitY * 1.1f, at + n * 0.1f + Vector3.UnitY * 1.75f, n, _roofing);
                    }
                }
                if (broaches)
                {
                    // The square's corners carried up onto the octagon's diagonal faces.
                    float h = _d.TowerHalf;
                    for (int i = 0; i < 4; i++)
                    {
                        float a = MathF.PI * 0.25f + i * MathF.PI * 0.5f;
                        Vector3 corner = foot + new Vector3(MathF.Cos(a), 0f, MathF.Sin(a)) * (h * MathF.Sqrt(2f));
                        Vector3 v0 = foot + new Vector3(MathF.Cos(a - MathF.PI / Sides), 0f, MathF.Sin(a - MathF.PI / Sides)) * radius;
                        Vector3 v1 = foot + new Vector3(MathF.Cos(a + MathF.PI / Sides), 0f, MathF.Sin(a + MathF.PI / Sides)) * radius;
                        float up = height * 0.16f;
                        Vector3 tip = foot + new Vector3(MathF.Cos(a), 0f, MathF.Sin(a)) * (apothem * (1f - up / height)) + Vector3.UnitY * up;
                        Vector3 outward = new Vector3(MathF.Cos(a), 0.6f, MathF.Sin(a));
                        YardStone.Face(_b, corner, v0, tip, outward + Vector3.Cross(Vector3.UnitY, new Vector3(MathF.Cos(a), 0f, MathF.Sin(a))) * -0.5f, skin);
                        YardStone.Face(_b, corner, tip, v1, outward + Vector3.Cross(Vector3.UnitY, new Vector3(MathF.Cos(a), 0f, MathF.Sin(a))) * 0.5f, skin);
                    }
                }
                // The finial: a ball, and a cross of iron on it.
                VertexMaterial iron = YardStone.Rust(random, 0.8f);
                _b.Tube(apex - Vector3.UnitY * 0.2f, apex + Vector3.UnitY * 0.25f, 0.28f, 0.16f, 8, YardStone.Dressed(_stone, random, apex.Y, 0.02f));
                _b.Beam(apex + Vector3.UnitY * 0.2f, apex + Vector3.UnitY * 2.4f, 0.07f, iron);
                _b.Beam(apex + new Vector3(-0.7f, 1.7f, 0f), apex + new Vector3(0.7f, 1.7f, 0f), 0.06f, iron);
            }

            // ---------------------------------------------------------------- A bellcote

            private void Bellcote()
            {
                ChurchDesign d = _d;
                var west = new Frame(new Vector3(d.NaveFrom, 0f, 0f), -Vector3.UnitX);
                // The west front: a door, a great window over it, and buttresses at its corners.
                Doorway(west, 0f, 2.6f, 5.2f, 2);
                Window(west, 0f, 6.4f, MathF.Min(d.NaveHalf * 0.9f, 3.6f), d.NaveWall + (d.NaveHalf + ChurchDesign.Eave) * d.Pitch * 0.5f - 6.4f, 3, light: true);
                foreach (float side in new[] { -1f, 1f })
                {
                    Buttress(new Frame(new Vector3(d.NaveFrom, 0f, side * d.NaveHalf), new Vector3(-1f, 0f, side).Normalized()), 0f, d.NaveWall - 0.4f, depth: 1.1f);
                }

                // The cote over the gable's apex: a slab of stone with two arches in it, a bell hung in each, under a little gable with a cross.
                float apex = d.Ridge - 0.3f;
                float top = d.TowerTop;
                float x = d.NaveFrom + 0.4f;
                var random = Next();
                YardStone.Box(_b, new Vector3(x - 0.55f, apex - 1.4f, -1.7f), new Vector3(x + 0.55f, top, -0.9f), YardStone.Chamfer, Dressed(top));
                YardStone.Box(_b, new Vector3(x - 0.55f, apex - 1.4f, 0.9f), new Vector3(x + 0.55f, top, 1.7f), YardStone.Chamfer, Dressed(top));
                YardStone.Box(_b, new Vector3(x - 0.55f, apex - 1.4f, -0.12f), new Vector3(x + 0.55f, top, 0.12f), YardStone.Chamfer, Dressed(top));
                YardStone.Box(_b, new Vector3(x - 0.55f, apex - 1.4f, -1.7f), new Vector3(x + 0.55f, apex - 0.1f, 1.7f), YardStone.Chamfer, Dressed(apex));
                YardStone.Box(_b, new Vector3(x - 0.6f, top - 0.05f, -1.8f), new Vector3(x + 0.6f, top + 0.3f, 1.8f), 0.03f, Dressed(top, 0.4f));
                VertexMaterial bronze = new VertexMaterial(Textures.MaterialCatalog.Iron.Index, new Colour(0.16f, 0.22f, 0.18f));
                foreach (float z in new[] { -0.5f, 0.5f })
                {
                    _b.Tube(new Vector3(x, top - 0.6f, z), new Vector3(x, top - 1.55f, z), 0.14f, 0.36f, 10, bronze);
                    _b.Beam(new Vector3(x, top - 0.15f, z), new Vector3(x, top - 0.6f, z), 0.04f, YardStone.Rust(random));
                }
                YardStone.Face(_b, new Vector3(x - 0.62f, top + 0.3f, -1.85f), new Vector3(x - 0.62f, top + 0.3f, 1.85f), new Vector3(x, top + 1.1f, 1.85f), new Vector3(x, top + 1.1f, -1.85f), new Vector3(-1f, 1f, 0f), _roofing);
                YardStone.Face(_b, new Vector3(x + 0.62f, top + 0.3f, 1.85f), new Vector3(x + 0.62f, top + 0.3f, -1.85f), new Vector3(x, top + 1.1f, -1.85f), new Vector3(x, top + 1.1f, 1.85f), new Vector3(1f, 1f, 0f), _roofing);
                Cross(new Vector3(x, top + 1.1f, 0f), -Vector3.UnitX, 1.2f);
            }

            // ---------------------------------------------------------------- The porch

            private void Porch()
            {
                ChurchDesign d = _d;
                float x = d.PorchX, half = d.PorchHalf;
                float z0 = d.BodyHalf - 0.3f, z1 = d.BodyHalf + d.PorchDepth;
                Walls(x - half, x + half, z0, z1, d.PorchWall);
                float rise = (half + ChurchDesign.Eave) * 1.05f;
                Roof(new Vector2(x, z0 - 0.2f), new Vector2(x, z1), half, d.PorchWall, rise, gableFrom: false, gableTo: true, crossFrom: false, crossTo: true);

                // Its mouth: an open arch going dark, its orders round it, and a lantern hung beside it.
                var front = new Frame(new Vector3(x, 0f, z1), Vector3.UnitZ);
                float mouth = half * 0.62f;
                var arch = new Pointed(mouth, d.PorchWall * 0.62f, mouth * 1.15f);
                for (float y = 0f; y < arch.Apex - 0.02f; y += 0.5f)
                {
                    float y1 = MathF.Min(y + 0.5f, arch.Apex);
                    YardStone.Face(_b, front.At(-arch.HalfAt(y), y, 0.03f), front.At(arch.HalfAt(y), y, 0.03f), front.At(arch.HalfAt(y1), y1, 0.03f), front.At(-arch.HalfAt(y1), y1, 0.03f), front.N, YardStone.Dark);
                }
                for (int o = 0; o < 2; o++)
                {
                    float grow = o * 0.3f;
                    Rib(front, 0f, new Pointed(mouth + grow + 0.12f, arch.Spring, arch.Rise + grow), 0.12f + o * 0.12f, 0.13f, YardStone.Dressed(_stone, Next(), arch.Spring, 0.03f));
                    foreach (float s in new[] { -1f, 1f })
                    {
                        float u = s * (mouth + grow + 0.12f);
                        YardStone.Block(_b, front.At(u, arch.Spring * 0.5f - 0.1f, 0.12f + o * 0.12f), new Vector3(0.15f, arch.Spring * 0.5f + 0.1f, 0.15f), 0f, 0.03f, Dressed(1f, 0.1f));
                    }
                }
                foreach (float s in new[] { -1f, 1f })
                {
                    Buttress(new Frame(new Vector3(x + s * half, 0f, z1), new Vector3(s, 0f, 1f).Normalized()), 0f, d.PorchWall - 0.5f, depth: 0.8f, width: 0.75f);
                }
                Lantern(front.At(mouth + 0.55f, arch.Spring + 0.4f, 0.2f), front.N);
            }

            // A lantern hung from an iron bracket off a wall, its light in it: the warmth at a church's door.
            private void Lantern(Vector3 at, Vector3 outward, float intensity = 130f, float radius = 14f)
            {
                var random = Next();
                VertexMaterial iron = YardStone.Rust(random, 0.75f);
                Vector3 hang = at + outward * 0.55f;
                _b.Beam(at, hang + Vector3.UnitY * 0.05f, 0.04f, iron);
                _b.Beam(at - Vector3.UnitY * 0.5f, hang - Vector3.UnitY * 0.05f + outward * -0.25f, 0.03f, iron);
                Vector3 c = hang - Vector3.UnitY * 0.55f;
                _b.Box(c, new Vector3(0.14f, 0.2f, 0.14f), YardStone.Glow(1f, 0.6f, 0.26f, 2.2f));
                _b.Tube(c - Vector3.UnitY * 0.24f, c - Vector3.UnitY * 0.2f, 0.2f, 0.2f, 6, iron);
                _b.Tube(c + Vector3.UnitY * 0.2f, c + Vector3.UnitY * 0.42f, 0.24f, 0.04f, 6, iron);
                foreach (float sx in new[] { -1f, 1f })
                {
                    foreach (float sz in new[] { -1f, 1f })
                    {
                        _b.Beam(c + new Vector3(sx * 0.16f, -0.21f, sz * 0.16f), c + new Vector3(sx * 0.16f, 0.21f, sz * 0.16f), 0.018f, iron);
                    }
                }
                _b.Beam(hang, c + Vector3.UnitY * 0.42f, 0.015f, iron);
                _lights.Add(YardStone.PathLantern(c) with { Intensity = intensity, Radius = radius });
            }
        }
    }
}

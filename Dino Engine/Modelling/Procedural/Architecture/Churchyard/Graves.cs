using Dino_Engine.Modelling.Model;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Churchyard
{
    /// <summary>
    /// The dead and what marks them: a gravestone and the grave in front of it, chest tombs, graves the Ossuary has opened, candles left by whoever still comes, and benches. A stone's
    /// look is its form, its quarry and its age: the older it is the further it leans and sinks, the darker and greener it goes, and the more lichen is on what faces the sky. Everything
    /// is at the world's scale, twice life, so a headstone a person would come up to the thigh of is a metre and a half. Model +z is the way a stone faces, and its grave lies on that
    /// side of it. Headless. Read CHURCHYARD.md.
    /// </summary>
    internal static class Graves
    {
        /// <summary>How long a grave is, stone and all, and how wide: what the layout leaves for each.</summary>
        public const float Length = 3.7f;
        public const float Width = 1.8f;

        /// <summary>Where a stone stands along z, at the head of its grave.</summary>
        public const float Head = -Length * 0.5f + 0.35f;

        // ---------------------------------------------------------------- Gravestones

        public static Mesh Stone(in YardSpec spec)
        {
            var b = new MeshBuilder();
            var form = (StoneForm)Math.Clamp(spec.Int(0), 0, (int)StoneForm.Book);
            var dress = (GraveDress)Math.Clamp(spec.Int(1), 0, (int)GraveDress.Sunken);
            float age = Math.Clamp(spec.Share(2), 0f, 1f);
            var random = new WorldRandom(spec.Seed).Fork("stone");
            YardStone.Quarry quarry = form == StoneForm.Wooden ? YardStone.Quarry.Limestone : YardStone.QuarryFor(age, random.Fork("quarry"));
            Vector3 colour = YardStone.ColourOf(quarry, random.Fork("colour"));
            Func<Vector3, VertexMaterial> stone = YardStone.Weathered(colour, quarry, age, random.Fork("weather"));

            Dress(b, dress, age, random.Fork("dress"), stone);

            // Older stones lean, forwards or back, a little to one side, and sink.
            WorldRandom lean = random.Fork("lean");
            float pitch = (lean.Chance(0.5f) ? 1f : -1f) * age * age * lean.Range(0.03f, 0.24f);
            float roll = lean.Range(-1f, 1f) * age * 0.09f;
            float sink = age * lean.Range(0.05f, 0.3f);
            var at = new Vector3(0f, -sink, Head);
            var put = new Placed(b, at, pitch, roll);

            switch (form)
            {
                case StoneForm.Round: Tablet(put, random.Fork("form"), stone, top: 0); break;
                case StoneForm.Pointed: Tablet(put, random.Fork("form"), stone, top: 1); break;
                case StoneForm.Shouldered: Shouldered(put, random.Fork("form"), stone); break;
                case StoneForm.Cross: Cross(put, random.Fork("form"), stone, ringed: false); break;
                case StoneForm.Ringed: Cross(put, random.Fork("form"), stone, ringed: true); break;
                case StoneForm.Obelisk: Obelisk(put, random.Fork("form"), stone); break;
                case StoneForm.Slab: Slab(put, random.Fork("form"), stone); break;
                case StoneForm.Broken: Broken(b, put, random.Fork("form"), stone); break;
                case StoneForm.Wooden: Wooden(put, random.Fork("form")); break;
                case StoneForm.Book: Book(put, random.Fork("form"), stone); break;
            }
            return b.Build();
        }

        /// <summary>A stone's own frame: built upright about its foot, then leaned, rolled and set where it stands.</summary>
        private readonly struct Placed
        {
            public readonly MeshBuilder B;
            private readonly Vector3 _at;
            private readonly Matrix3 _turn;

            public Placed(MeshBuilder b, Vector3 at, float pitch, float roll)
            {
                B = b;
                _at = at;
                _turn = Matrix3.CreateRotationX(pitch) * Matrix3.CreateRotationZ(roll);
            }

            public Vector3 P(float x, float y, float z) => _at + new Vector3(x, y, z) * _turn;
            public Vector3 P(Vector3 v) => P(v.X, v.Y, v.Z);
            public Vector3 Dir(Vector3 v) => v * _turn;

            /// <summary>A block in the stone's frame, from its least corner to its most.</summary>
            public void Box(Vector3 min, Vector3 max, float chamfer, Func<Vector3, VertexMaterial> facing)
            {
                YardStone.Solid(B, new[]
                {
                    P(min.X, min.Y, max.Z), P(max.X, min.Y, max.Z), P(max.X, max.Y, max.Z), P(min.X, max.Y, max.Z),
                    P(min.X, min.Y, min.Z), P(max.X, min.Y, min.Z), P(max.X, max.Y, min.Z), P(min.X, max.Y, min.Z),
                }, chamfer, facing);
            }

            /// <summary>A flat outline in x and y, convex and counter-clockwise seen from +z, stood up from z = back to z = front.</summary>
            public void Extrude(IReadOnlyList<Vector2> outline, float back, float front, Func<Vector3, VertexMaterial> facing)
            {
                Vector2 middle = Vector2.Zero;
                foreach (Vector2 p in outline) middle += p;
                middle /= outline.Count;
                Vector3 forward = Dir(Vector3.UnitZ);
                VertexMaterial face = facing(forward), backFace = facing(-forward);
                for (int i = 0; i < outline.Count; i++)
                {
                    Vector2 a = outline[i], c = outline[(i + 1) % outline.Count];
                    B.Triangle(P(middle.X, middle.Y, front), P(a.X, a.Y, front), P(c.X, c.Y, front), face);
                    B.Triangle(P(middle.X, middle.Y, back), P(c.X, c.Y, back), P(a.X, a.Y, back), backFace);
                    Vector2 edge = c - a;
                    Vector3 outward = Dir(new Vector3(edge.Y, -edge.X, 0f).Normalized());
                    YardStone.Face(B, P(a.X, a.Y, front), P(c.X, c.Y, front), P(c.X, c.Y, back), P(a.X, a.Y, back), outward, facing(outward));
                }
            }
        }

        // A tablet: a slab with a rounded (0) or pointed (1) head, a moulded border proud of its face, and on a pointed one a cross cut in relief.
        private static void Tablet(Placed s, WorldRandom random, Func<Vector3, VertexMaterial> stone, int top)
        {
            float half = random.Range(0.5f, 0.72f), height = random.Range(1.35f, 2.05f), thick = random.Range(0.1f, 0.15f);
            List<Vector2> outline = Outline(half, height, top, -0.45f);
            s.Extrude(outline, -thick, thick, stone);
            // A border standing a little proud of the face, following its outline in from the edge.
            List<Vector2> inner = Outline(half - 0.09f, height - 0.09f, top, 0.25f);
            Func<Vector3, VertexMaterial> panel = n => YardStone.Tint(stone(n), new Vector3(0.9f));
            s.Extrude(inner, thick - 0.01f, thick + 0.018f, panel);
            if (top == 1)
            {
                float y = height - half * 1.2f;
                s.Box(new Vector3(-0.045f, y - 0.42f, thick), new Vector3(0.045f, y + 0.12f, thick + 0.05f), 0.01f, stone);
                s.Box(new Vector3(-0.2f, y - 0.06f, thick), new Vector3(0.2f, y + 0.03f, thick + 0.05f), 0.01f, stone);
            }
            Foot(s, half, thick, stone);
        }

        // The outline of a tablet from y = bottom up to its head: a rounded head as a half-ellipse, a pointed one as two arcs meeting.
        private static List<Vector2> Outline(float half, float height, int top, float bottom)
        {
            var points = new List<Vector2> { new Vector2(-half, bottom), new Vector2(half, bottom) };
            float spring = height - half * (top == 0 ? 0.8f : 1.3f);
            const int Steps = 7;
            for (int i = 0; i <= Steps; i++)
            {
                float t = i / (float)Steps;
                if (top == 0)
                {
                    float a = t * MathF.PI;
                    points.Add(new Vector2(MathF.Cos(a) * half, spring + MathF.Sin(a) * half * 0.8f));
                }
                else
                {
                    // Right arc from the springing to the point, then the left back down.
                    float rise = height - spring;
                    float c = (rise * rise - half * half) / (2f * half), r = c + half;
                    float apex = MathF.Atan2(rise, c);
                    if (t <= 0.5f)
                    {
                        float a = t * 2f * apex;
                        points.Add(new Vector2(-c + MathF.Cos(a) * r, spring + MathF.Sin(a) * r));
                    }
                    else
                    {
                        float a = (1f - t) * 2f * apex;
                        points.Add(new Vector2(c - MathF.Cos(a) * r, spring + MathF.Sin(a) * r));
                    }
                }
            }
            // Drop the duplicate at the point.
            for (int i = points.Count - 1; i > 0; i--) if ((points[i] - points[i - 1]).LengthSquared < 1e-6f) points.RemoveAt(i);
            return points;
        }

        // The stone a tablet is set in, just showing above the turf.
        private static void Foot(Placed s, float half, float thick, Func<Vector3, VertexMaterial> stone)
        {
            s.Box(new Vector3(-half - 0.1f, -0.5f, -thick - 0.1f), new Vector3(half + 0.1f, YardStone.Ground + 0.12f, thick + 0.12f), 0.03f, stone);
        }

        // A tablet with shoulders: a tall middle with a rounded head, a lower shoulder on each side, as a bed's head is.
        private static void Shouldered(Placed s, WorldRandom random, Func<Vector3, VertexMaterial> stone)
        {
            float half = random.Range(0.62f, 0.8f), height = random.Range(1.45f, 1.95f), thick = random.Range(0.11f, 0.15f);
            float middle = half * 0.55f;
            s.Extrude(Outline(middle, height, 0, -0.45f), -thick, thick, stone);
            float shoulder = height * random.Range(0.62f, 0.74f);
            foreach (float side in new[] { -1f, 1f })
            {
                float x0 = side * (middle - 0.02f), x1 = side * half;
                var outline = new List<Vector2>
                {
                    new Vector2(MathF.Min(x0, x1), -0.45f), new Vector2(MathF.Max(x0, x1), -0.45f),
                    new Vector2(MathF.Max(x0, x1), shoulder - (side > 0f ? 0.12f : 0f)), new Vector2(MathF.Min(x0, x1), shoulder - (side < 0f ? 0.12f : 0f)),
                };
                s.Extrude(outline, -thick * 0.9f, thick * 0.9f, stone);
            }
            s.Extrude(Outline(middle - 0.08f, height - 0.1f, 0, shoulder * 0.35f), thick - 0.01f, thick + 0.018f, n => YardStone.Tint(stone(n), new Vector3(0.9f)));
            Foot(s, half, thick, stone);
        }

        // A cross on a base of two steps, or a ringed cross on a tapering shaft.
        private static void Cross(Placed s, WorldRandom random, Func<Vector3, VertexMaterial> stone, bool ringed)
        {
            float height = random.Range(1.9f, 2.7f) * (ringed ? 1.12f : 1f);
            float arm = height * random.Range(0.26f, 0.32f), shaft = random.Range(0.09f, 0.12f);
            s.Box(new Vector3(-0.55f, -0.5f, -0.4f), new Vector3(0.55f, YardStone.Ground + 0.2f, 0.4f), 0.04f, stone);
            s.Box(new Vector3(-0.38f, YardStone.Ground + 0.2f, -0.28f), new Vector3(0.38f, YardStone.Ground + 0.5f, 0.28f), 0.035f, stone);
            float from = YardStone.Ground + 0.5f;
            float cross = height * 0.72f;
            if (ringed)
            {
                // A shaft that tapers as it rises.
                YardStone.Solid(s.B, new[]
                {
                    s.P(-0.2f, from, 0.12f), s.P(0.2f, from, 0.12f), s.P(shaft, cross, 0.1f), s.P(-shaft, cross, 0.1f),
                    s.P(-0.2f, from, -0.12f), s.P(0.2f, from, -0.12f), s.P(shaft, cross, -0.1f), s.P(-shaft, cross, -0.1f),
                }, 0.02f, stone);
                s.Box(new Vector3(-shaft, cross - 0.05f, -0.1f), new Vector3(shaft, height, 0.1f), 0.02f, stone);
            }
            else s.Box(new Vector3(-shaft, from, -shaft), new Vector3(shaft, height, shaft), 0.02f, stone);
            s.Box(new Vector3(-arm, cross - shaft, -shaft), new Vector3(arm, cross + shaft, shaft), 0.02f, stone);
            if (!ringed) return;
            float r = arm * 0.62f;
            const int Segments = 12;
            VertexMaterial m = stone(Vector3.UnitZ);
            for (int i = 0; i < Segments; i++)
            {
                float a0 = i * MathF.Tau / Segments, a1 = (i + 1) * MathF.Tau / Segments;
                s.B.Tube(s.P(MathF.Cos(a0) * r, cross + MathF.Sin(a0) * r, 0f), s.P(MathF.Cos(a1) * r, cross + MathF.Sin(a1) * r, 0f), 0.065f, 0.065f, 5, m, capped: false);
            }
        }

        // An obelisk: two steps, a die, a shaft tapering to a pyramid at its point.
        private static void Obelisk(Placed s, WorldRandom random, Func<Vector3, VertexMaterial> stone)
        {
            float height = random.Range(3f, 4.2f);
            float foot = random.Range(0.3f, 0.38f);
            s.Box(new Vector3(-0.75f, -0.5f, -0.75f), new Vector3(0.75f, YardStone.Ground + 0.25f, 0.75f), 0.04f, stone);
            s.Box(new Vector3(-0.58f, YardStone.Ground + 0.25f, -0.58f), new Vector3(0.58f, YardStone.Ground + 0.5f, 0.58f), 0.035f, stone);
            s.Box(new Vector3(-0.46f, YardStone.Ground + 0.5f, -0.46f), new Vector3(0.46f, YardStone.Ground + 1.25f, 0.46f), 0.03f, stone);
            s.Box(new Vector3(-0.52f, YardStone.Ground + 1.25f, -0.52f), new Vector3(0.52f, YardStone.Ground + 1.36f, 0.52f), 0.025f, stone);
            float from = YardStone.Ground + 1.36f, to = height - foot * 0.9f;
            float tip = foot * 0.62f;
            YardStone.Solid(s.B, new[]
            {
                s.P(-foot, from, foot), s.P(foot, from, foot), s.P(tip, to, tip), s.P(-tip, to, tip),
                s.P(-foot, from, -foot), s.P(foot, from, -foot), s.P(tip, to, -tip), s.P(-tip, to, -tip),
            }, 0.02f, stone);
            Vector3 apex = s.P(0f, height, 0f);
            Vector3[] corners = { s.P(-tip, to, tip), s.P(tip, to, tip), s.P(tip, to, -tip), s.P(-tip, to, -tip) };
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = corners[i], c = corners[(i + 1) % 4];
                Vector3 outward = (a + c) * 0.5f - s.P(0f, to, 0f) + s.Dir(Vector3.UnitY) * 0.2f;
                YardStone.Face(s.B, a, c, apex, outward, stone(outward.Normalized()));
            }
        }

        // An old slab, thick and square-headed, its top edge moulded, its face worn smooth.
        private static void Slab(Placed s, WorldRandom random, Func<Vector3, VertexMaterial> stone)
        {
            float half = random.Range(0.55f, 0.75f), height = random.Range(1.1f, 1.5f), thick = random.Range(0.16f, 0.22f);
            s.Box(new Vector3(-half, -0.45f, -thick), new Vector3(half, height, thick), 0.05f, stone);
            s.Box(new Vector3(-half - 0.06f, height - 0.08f, -thick - 0.05f), new Vector3(half + 0.06f, height + 0.08f, thick + 0.05f), 0.04f, stone);
            if (random.Chance(0.5f)) s.Box(new Vector3(-half + 0.15f, height * 0.42f, thick), new Vector3(half - 0.15f, height * 0.84f, thick + 0.02f), 0.01f, n => YardStone.Tint(stone(n), new Vector3(0.88f)));
        }

        // What's left of a tablet snapped off by frost, a tree or the Ossuary's diggers: its stump, jagged at the top, and its head lying in the grass in front of it.
        private static void Broken(MeshBuilder b, Placed s, WorldRandom random, Func<Vector3, VertexMaterial> stone)
        {
            float half = random.Range(0.52f, 0.7f), thick = random.Range(0.11f, 0.15f);
            float stump = random.Range(0.5f, 0.9f);
            var outline = new List<Vector2> { new Vector2(-half, -0.45f), new Vector2(half, -0.45f) };
            const int Teeth = 5;
            for (int i = 0; i <= Teeth; i++)
            {
                float x = half - 2f * half * i / Teeth;
                outline.Add(new Vector2(x, stump + random.Range(-0.18f, 0.18f)));
            }
            // A convex hull of the jagged top would fill its notches, so the top is laid as a fan from the middle of the stump, which is inside it whatever the teeth do.
            s.Extrude(new[] { outline[0], outline[1], outline[2], outline[^1] }, -thick, thick, stone);
            for (int i = 2; i < outline.Count - 1; i++)
            {
                Vector2 mid = new Vector2(0f, stump * 0.4f);
                s.Extrude(new[] { mid, outline[i + 1], outline[i] }.Reverse().ToArray(), -thick + 0.002f, thick - 0.002f, stone);
            }
            Foot(s, half, thick, stone);
            // The head, lying on its back in front of it, half sunk in the turf.
            float height = random.Range(0.7f, 1f);
            float yaw = random.Range(-0.5f, 0.5f);
            Vector3 lying = new Vector3(random.Range(-0.3f, 0.3f), YardStone.Ground + 0.04f, Head + 0.9f + height * 0.5f);
            Matrix3 turn = Matrix3.CreateRotationY(yaw) * Matrix3.CreateRotationX(-MathF.PI * 0.5f + random.Range(-0.08f, 0.08f));
            var head = Outline(half, height, random.Chance(0.5f) ? 0 : 1, 0f);
            Vector2 middle = Vector2.Zero;
            foreach (Vector2 p in head) middle += p;
            middle /= head.Count;
            Vector3 L(Vector2 p, float z) => lying + new Vector3(p.X, p.Y - height * 0.5f, z) * turn;
            VertexMaterial face = stone(Vector3.UnitY);
            for (int i = 0; i < head.Count; i++)
            {
                Vector2 a = head[i], c = head[(i + 1) % head.Count];
                YardStone.Face(b, L(middle, thick), L(a, thick), L(c, thick), Vector3.UnitY, face);
                Vector3 outward = new Vector3(c.Y - a.Y, -(c.X - a.X), 0f) * turn;
                YardStone.Face(b, L(a, thick), L(c, thick), L(c, -thick), L(a, -thick), outward, stone(outward.Normalized()));
            }
        }

        // A cross of oak, gone grey, with a little gable over its head as the poor were given, leaning further than any stone.
        private static void Wooden(Placed s, WorldRandom random)
        {
            VertexMaterial oak = YardStone.Silvered(random);
            float height = random.Range(1.8f, 2.3f), arm = random.Range(0.45f, 0.6f);
            float cross = height - random.Range(0.4f, 0.55f);
            s.Box(new Vector3(-0.07f, -0.6f, -0.06f), new Vector3(0.07f, height, 0.06f), 0.015f, _ => oak);
            s.Box(new Vector3(-arm, cross - 0.06f, -0.055f), new Vector3(arm, cross + 0.06f, 0.055f), 0.015f, _ => oak);
            if (random.Chance(0.6f))
            {
                VertexMaterial board = YardStone.Tint(oak, new Vector3(0.82f));
                foreach (float side in new[] { -1f, 1f })
                {
                    YardStone.Solid(s.B, new[]
                    {
                        s.P(-0.04f, height - 0.02f, 0.02f), s.P(side * 0.32f, height - 0.22f, 0.02f), s.P(side * 0.32f, height - 0.17f, 0.02f), s.P(-0.04f * -side, height + 0.05f, 0.02f),
                        s.P(-0.04f, height - 0.02f, -0.2f), s.P(side * 0.32f, height - 0.22f, -0.2f), s.P(side * 0.32f, height - 0.17f, -0.2f), s.P(-0.04f * -side, height + 0.05f, -0.2f),
                    }, 0.01f, _ => board);
                }
            }
        }

        // An open book of stone on a plinth, the newest kind, low enough that the grass nearly hides it.
        private static void Book(Placed s, WorldRandom random, Func<Vector3, VertexMaterial> stone)
        {
            s.Box(new Vector3(-0.62f, -0.4f, -0.45f), new Vector3(0.62f, YardStone.Ground + 0.42f, 0.45f), 0.04f, stone);
            foreach (float side in new[] { -1f, 1f })
            {
                float x0 = side * 0.03f, x1 = side * 0.58f;
                YardStone.Solid(s.B, new[]
                {
                    s.P(MathF.Min(x0, x1), YardStone.Ground + 0.42f, 0.38f), s.P(MathF.Max(x0, x1), YardStone.Ground + 0.42f, 0.38f), s.P(MathF.Max(x0, x1), YardStone.Ground + (side > 0f ? 0.62f : 0.82f), -0.34f), s.P(MathF.Min(x0, x1), YardStone.Ground + (side > 0f ? 0.82f : 0.62f), -0.34f),
                    s.P(MathF.Min(x0, x1), YardStone.Ground + 0.32f, 0.38f), s.P(MathF.Max(x0, x1), YardStone.Ground + 0.32f, 0.38f), s.P(MathF.Max(x0, x1), YardStone.Ground + (side > 0f ? 0.52f : 0.72f), -0.34f), s.P(MathF.Min(x0, x1), YardStone.Ground + (side > 0f ? 0.72f : 0.52f), -0.34f),
                }, 0.02f, stone);
            }
        }

        // What lies in front of a stone.
        private static void Dress(MeshBuilder b, GraveDress dress, float age, WorldRandom random, Func<Vector3, VertexMaterial> stone)
        {
            const float Half = Width * 0.5f - 0.05f;
            float z0 = Head + 0.25f, z1 = Length * 0.5f - 0.05f;
            float g = YardStone.Ground;
            switch (dress)
            {
                case GraveDress.Mound:
                {
                    VertexMaterial turf = YardStone.Turf(random.Range(0.85f, 1.1f));
                    float high = random.Range(0.24f, 0.36f) * (1f - age * 0.6f);
                    // v runs towards -z, so that the surface faces up (MeshBuilder.Surface winds counter-clockwise as u and then v increase).
                    b.Surface(6, 5, (u, v) =>
                    {
                        float x = (u * 2f - 1f) * Half * 0.86f;
                        float z = z1 - (z1 - z0) * v;
                        float across = 1f - (u * 2f - 1f) * (u * 2f - 1f);
                        float along = 1f - MathF.Pow(v * 2f - 1f, 4f);
                        return new Vector3(x, g - 0.04f + high * across * along, z);
                    }, turf);
                    break;
                }
                case GraveDress.Kerb:
                case GraveDress.Sunken:
                {
                    bool sunk = dress == GraveDress.Sunken;
                    float top = g + random.Range(0.2f, 0.3f);
                    // Two long kerbs, a short one at the foot, and the fill between.
                    foreach (float side in new[] { -1f, 1f })
                    {
                        float tip = sunk ? side * -random.Range(0.08f, 0.2f) : 0f;
                        Vector3 centre = new Vector3(side * (Half - 0.09f), top * 0.5f - 0.1f, (z0 + z1) * 0.5f);
                        Matrix3 turn = Matrix3.CreateRotationZ(tip);
                        Vector3 P(float x, float y, float z) => centre + new Vector3(x, y, z) * turn;
                        float h = top * 0.5f + 0.1f;
                        YardStone.Solid(b, new[]
                        {
                            P(-0.09f, -h, (z1 - z0) * 0.5f), P(0.09f, -h, (z1 - z0) * 0.5f), P(0.09f, h, (z1 - z0) * 0.5f), P(-0.09f, h, (z1 - z0) * 0.5f),
                            P(-0.09f, -h, -(z1 - z0) * 0.5f), P(0.09f, -h, -(z1 - z0) * 0.5f), P(0.09f, h, -(z1 - z0) * 0.5f), P(-0.09f, h, -(z1 - z0) * 0.5f),
                        }, 0.025f, stone);
                    }
                    YardStone.Box(b, new Vector3(-Half, -0.2f, z1 - 0.18f), new Vector3(Half, top - (sunk ? 0.06f : 0f), z1), 0.025f, stone);
                    float fill = sunk ? g + 0.02f : top - 0.06f;
                    // Chippings while someone keeps it, turf once nobody does, and bare earth where it's fallen in: never so dark it reads as an open grave from above.
                    VertexMaterial floor = sunk ? YardStone.Soil(1.25f) : age > 0.55f ? YardStone.Turf(1.05f) : YardStone.Chippings(random.Range(0.9f, 1.05f));
                    b.Quad(new Vector3(-Half + 0.18f, fill, z1 - 0.18f), new Vector3(Half - 0.18f, fill, z1 - 0.18f), new Vector3(Half - 0.18f, fill, z0), new Vector3(-Half + 0.18f, fill, z0), floor);
                    if (!sunk && age < 0.45f && random.Chance(0.55f)) Flowers(b, new Vector3(random.Range(-0.3f, 0.3f), fill, z1 - 0.5f), random);
                    break;
                }
                case GraveDress.Ledger:
                {
                    YardStone.Box(b, new Vector3(-Half, -0.3f, z0), new Vector3(Half, g + 0.22f, z1), 0.035f, stone);
                    var ledger = random.Fork("ledger");
                    float crack = age > 0.6f && ledger.Chance(0.5f) ? ledger.Range(-0.4f, 0.4f) : float.NaN;
                    if (float.IsNaN(crack)) YardStone.Box(b, new Vector3(-Half - 0.06f, g + 0.22f, z0 - 0.06f), new Vector3(Half + 0.06f, g + 0.4f, z1 + 0.06f), 0.04f, stone);
                    else
                    {
                        // Cracked across, its two halves settled a little apart.
                        float zc = (z0 + z1) * 0.5f + crack;
                        YardStone.Box(b, new Vector3(-Half - 0.06f, g + 0.2f, z0 - 0.06f), new Vector3(Half + 0.06f, g + 0.38f, zc - 0.02f), 0.04f, stone);
                        YardStone.Solid(b, new[]
                        {
                            new Vector3(-Half - 0.06f, g + 0.22f, z1 + 0.06f), new Vector3(Half + 0.06f, g + 0.17f, z1 + 0.06f), new Vector3(Half + 0.06f, g + 0.35f, z1 + 0.06f), new Vector3(-Half - 0.06f, g + 0.4f, z1 + 0.06f),
                            new Vector3(-Half - 0.06f, g + 0.22f, zc + 0.03f), new Vector3(Half + 0.06f, g + 0.2f, zc + 0.03f), new Vector3(Half + 0.06f, g + 0.38f, zc + 0.03f), new Vector3(-Half - 0.06f, g + 0.4f, zc + 0.03f),
                        }, 0.035f, stone);
                    }
                    break;
                }
            }
        }

        // Flowers left at a grave, in a stone vase: a few deep heads on their stems, as flowers are albedos under a strong sun and pale ones go white.
        private static void Flowers(MeshBuilder b, Vector3 foot, WorldRandom random)
        {
            VertexMaterial vase = YardStone.Dressed(new Vector3(0.5f, 0.5f, 0.48f), random, 1f, 0.05f);
            b.Tube(foot, foot + new Vector3(0f, 0.32f, 0f), 0.13f, 0.17f, 8, vase);
            Vector3[] colours = { new Vector3(0.5f, 0.48f, 0.44f), new Vector3(0.3f, 0.16f, 0.48f), new Vector3(0.55f, 0.36f, 0.06f), new Vector3(0.45f, 0.12f, 0.28f) };
            Vector3 colour = colours[random.Int(colours.Length)];
            VertexMaterial stem = new VertexMaterial(Textures.MaterialCatalog.Flat.Index, new Colour(0.08f, 0.16f, 0.05f));
            VertexMaterial petal = new VertexMaterial(Textures.MaterialCatalog.Flat.Index, new Colour(colour));
            for (int i = 0; i < 5; i++)
            {
                float a = i * MathF.Tau / 5f + random.Range(-0.3f, 0.3f);
                Vector3 head = foot + new Vector3(MathF.Cos(a) * 0.18f, 0.62f + random.Range(-0.08f, 0.1f), MathF.Sin(a) * 0.18f);
                b.Beam(foot + new Vector3(0f, 0.28f, 0f), head, 0.012f, stem);
                b.Box(head, new Vector3(0.07f, 0.05f, 0.07f), petal);
            }
        }

        // ---------------------------------------------------------------- Tombs

        /// <summary>A chest tomb's length and width, and how high it stands, for its footprint.</summary>
        public static Vector3 TombSize(uint seed)
        {
            var random = new WorldRandom(seed).Fork("tomb size");
            return new Vector3(random.Range(1.7f, 2.1f), random.Range(1.5f, 1.9f), random.Range(3.4f, 4.1f));
        }

        public static Mesh Tomb(in YardSpec spec)
        {
            var b = new MeshBuilder();
            var form = (TombForm)Math.Clamp(spec.Int(0), 0, (int)TombForm.Coped);
            bool opened = spec.Int(1) == 1;
            float age = Math.Clamp(spec.Share(2), 0f, 1f);
            var random = new WorldRandom(spec.Seed).Fork("tomb");
            Vector3 size = TombSize(spec.Seed);
            float hx = size.X * 0.5f, hz = size.Z * 0.5f, top = size.Y;
            Vector3 colour = Vector3.Lerp(YardStone.Colour(spec, 3, 4), YardStone.ColourOf(YardStone.QuarryFor(age, random.Fork("quarry")), random.Fork("colour")), 0.35f);
            Func<Vector3, VertexMaterial> stone = YardStone.Weathered(colour, YardStone.Quarry.Sandstone, age, random.Fork("weather"));
            float g = YardStone.Ground;
            float lidTop = top, lidFrom = top - 0.24f;

            // The plinth it stands on.
            YardStone.Box(b, new Vector3(-hx - 0.12f, -0.4f, -hz - 0.12f), new Vector3(hx + 0.12f, g + 0.22f, hz + 0.12f), 0.04f, stone);
            switch (form)
            {
                case TombForm.Table:
                {
                    // A slab on short columns.
                    for (int i = 0; i < 3; i++)
                    {
                        foreach (float side in new[] { -1f, 1f })
                        {
                            Vector3 foot = new Vector3(side * (hx - 0.24f), g + 0.22f, -hz + 0.35f + (size.Z - 0.7f) * i / 2f);
                            VertexMaterial m = stone(Vector3.UnitX);
                            b.Tube(foot, foot + new Vector3(0f, lidFrom - g - 0.22f, 0f), 0.14f, 0.12f, 8, m, capped: false);
                            YardStone.Box(b, foot + new Vector3(-0.2f, 0f, -0.2f), foot + new Vector3(0.2f, 0.12f, 0.2f), 0.02f, stone);
                            YardStone.Box(b, foot + new Vector3(-0.19f, lidFrom - g - 0.34f, -0.19f), foot + new Vector3(0.19f, lidFrom - g - 0.22f, 0.19f), 0.02f, stone);
                        }
                    }
                    break;
                }
                default:
                {
                    YardStone.Box(b, new Vector3(-hx, g + 0.22f, -hz), new Vector3(hx, lidFrom, hz), 0.04f, stone);
                    // Corner piers, and sunk panels between them on every side.
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        foreach (float sz in new[] { -1f, 1f })
                        {
                            YardStone.Box(b, new Vector3(sx * hx - 0.13f, g + 0.22f, sz * hz - 0.13f), new Vector3(sx * hx + 0.13f, lidFrom, sz * hz + 0.13f), 0.03f, stone);
                        }
                    }
                    if (form == TombForm.Panelled)
                    {
                        Func<Vector3, VertexMaterial> sunk = n => YardStone.Tint(stone(n), new Vector3(0.78f));
                        int panels = size.Z > 3.7f ? 3 : 2;
                        for (int i = 0; i < panels; i++)
                        {
                            float z0 = -hz + 0.3f + (size.Z - 0.6f) * i / panels + 0.08f, z1 = -hz + 0.3f + (size.Z - 0.6f) * (i + 1f) / panels - 0.08f;
                            foreach (float sx in new[] { -1f, 1f })
                            {
                                YardStone.Box(b, new Vector3(sx * hx - 0.015f, g + 0.42f, z0), new Vector3(sx * hx + 0.015f, lidFrom - 0.2f, z1), 0.01f, sunk);
                            }
                        }
                        foreach (float sz in new[] { -1f, 1f })
                        {
                            YardStone.Box(b, new Vector3(-hx + 0.3f, g + 0.42f, sz * hz - 0.015f), new Vector3(hx - 0.3f, lidFrom - 0.2f, sz * hz + 0.015f), 0.01f, sunk);
                        }
                    }
                    break;
                }
            }

            // The lid, moulded, or ridged as a little roof; slid off its chest if the Ossuary has been at it.
            Vector3 shift = opened ? new Vector3(random.Range(0.55f, 0.85f) * (random.Chance(0.5f) ? 1f : -1f), -0.06f, random.Range(-0.4f, 0.4f)) : Vector3.Zero;
            float turn = opened ? random.Range(-0.3f, 0.3f) : 0f;
            float tilt = opened ? -MathF.Sign(shift.X) * random.Range(0.1f, 0.2f) : 0f;
            Matrix3 lidTurn = Matrix3.CreateRotationZ(tilt) * Matrix3.CreateRotationY(turn);
            Vector3 lidAt = new Vector3(0f, lidFrom, 0f) + shift;
            Vector3 L(float x, float y, float z) => lidAt + new Vector3(x, y, z) * lidTurn;
            float lx = hx + 0.12f, lz = hz + 0.12f;
            if (form == TombForm.Coped)
            {
                YardStone.Solid(b, Corners(L, -lx, lx, 0f, 0.14f, -lz, lz), 0.03f, stone);
                foreach (float side in new[] { -1f, 1f })
                {
                    YardStone.Solid(b, new[]
                    {
                        L(side * lx, 0.14f, lz), L(side * 0.02f, 0.5f, lz), L(side * 0.02f, 0.62f, lz), L(side * lx, 0.26f, lz),
                        L(side * lx, 0.14f, -lz), L(side * 0.02f, 0.5f, -lz), L(side * 0.02f, 0.62f, -lz), L(side * lx, 0.26f, -lz),
                    }, 0.03f, stone);
                }
            }
            else
            {
                YardStone.Solid(b, Corners(L, -lx, lx, 0f, 0.16f, -lz, lz), 0.035f, stone);
                YardStone.Solid(b, Corners(L, -lx + 0.1f, lx - 0.1f, 0.16f, 0.26f, -lz + 0.1f, lz - 0.1f), 0.03f, stone);
            }
            if (opened)
            {
                // The dark inside, where the lid used to be.
                b.Quad(new Vector3(-hx + 0.14f, lidFrom - 0.02f, hz - 0.14f), new Vector3(hx - 0.14f, lidFrom - 0.02f, hz - 0.14f), new Vector3(hx - 0.14f, lidFrom - 0.02f, -hz + 0.14f), new Vector3(-hx + 0.14f, lidFrom - 0.02f, -hz + 0.14f), YardStone.Dark);
            }
            return b.Build();
        }

        private static Vector3[] Corners(Func<float, float, float, Vector3> p, float x0, float x1, float y0, float y1, float z0, float z1) => new[]
        {
            p(x0, y0, z1), p(x1, y0, z1), p(x1, y1, z1), p(x0, y1, z1),
            p(x0, y0, z0), p(x1, y0, z0), p(x1, y1, z0), p(x0, y1, z0),
        };

        // ---------------------------------------------------------------- What the Ossuary opened

        /// <summary>
        /// A grave the Ossuary's diggers have opened: its pit going dark between walls of raw earth, the spoil heaped along +x, its coffin's broken lid thrown up beside it, the stone
        /// that stood at its head pulled over, and a spade left in the heap. Fresh (0) or dug and left to the weather (1).
        /// </summary>
        public static Mesh Dug(in YardSpec spec)
        {
            var b = new MeshBuilder();
            bool old = spec.Int(0) == 1;
            float age = Math.Clamp(spec.Share(1), 0f, 1f);
            var random = new WorldRandom(spec.Seed).Fork("dug");
            float g = YardStone.Ground;
            const float Hx = 0.85f, Hz = 1.55f;
            VertexMaterial earth = YardStone.Soil(old ? 0.85f : 1.1f), raw = YardStone.Soil(0.7f);
            // The pit: its walls sloping in from a lip of earth to the dark at its bottom.
            float lip = g + 0.28f;
            Vector3[] rim = { new(-Hx - 0.25f, lip, Hz + 0.25f), new(Hx + 0.25f, lip, Hz + 0.25f), new(Hx + 0.25f, lip, -Hz - 0.25f), new(-Hx - 0.25f, lip, -Hz - 0.25f) };
            Vector3[] floor = { new(-Hx * 0.8f, g + 0.01f, Hz * 0.86f), new(Hx * 0.8f, g + 0.01f, Hz * 0.86f), new(Hx * 0.8f, g + 0.01f, -Hz * 0.86f), new(-Hx * 0.8f, g + 0.01f, -Hz * 0.86f) };
            for (int i = 0; i < 4; i++)
            {
                Vector3 inward = -(rim[i] + rim[(i + 1) % 4]) * 0.5f;
                inward.Y = 1.2f;
                YardStone.Face(b, rim[i], rim[(i + 1) % 4], floor[(i + 1) % 4], floor[i], inward, raw);
            }
            b.Quad(floor[3], floor[2], floor[1], floor[0], YardStone.Dark);
            b.Quad(floor[0], floor[1], floor[2], floor[3], YardStone.Dark);
            // The lip all round, falling away to the turf.
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = rim[i], c = rim[(i + 1) % 4];
                Vector3 outward = (a + c) * 0.5f;
                outward.Y = 0f;
                outward = outward.Normalized();
                YardStone.Face(b, a, c, c + outward * 0.5f + new Vector3(0f, g - 0.02f - lip, 0f), a + outward * 0.5f + new Vector3(0f, g - 0.02f - lip, 0f), outward + Vector3.UnitY, earth);
            }
            // The spoil heap along its side.
            float heap = old ? 0.55f : 0.85f;
            uint lumps = spec.Seed % 13;
            b.Surface(7, 5, (u, v) =>
            {
                float x = Hx + 0.45f + u * 1.6f;
                float z = (1f - v * 2f) * (Hz + 0.3f);
                float across = MathF.Sin(u * MathF.PI);
                float along = 1f - MathF.Pow(v * 2f - 1f, 4f);
                float lump = 1f + 0.18f * MathF.Sin(u * 7.3f + v * 5.1f + lumps);
                return new Vector3(x, g - 0.06f + heap * across * along * lump, z);
            }, old ? YardStone.Turf(0.8f) : earth);
            // The coffin's lid, split, thrown up against the heap.
            VertexMaterial board = YardStone.Oak(random, 0.7f);
            Matrix3 lean = Matrix3.CreateRotationZ(-0.9f) * Matrix3.CreateRotationY(random.Range(-0.2f, 0.2f));
            Vector3 lidAt = new Vector3(Hx + 0.7f, g + 0.55f, random.Range(-0.4f, 0.2f));
            Vector3 P(float x, float y, float z) => lidAt + new Vector3(x, y, z) * lean;
            YardStone.Solid(b, new[]
            {
                P(-0.38f, -0.04f, 1.05f), P(0.38f, -0.04f, 1.05f), P(0.38f, 0.04f, 1.05f), P(-0.38f, 0.04f, 1.05f),
                P(-0.3f, -0.04f, -0.95f), P(0.3f, -0.04f, -0.95f), P(0.3f, 0.04f, -0.95f), P(-0.3f, 0.04f, -0.95f),
            }, 0.01f, board);
            // The stone, pulled over onto the turf at the pit's head.
            var tablet = random.Fork("stone");
            var quarry = YardStone.QuarryFor(age, tablet);
            Func<Vector3, VertexMaterial> stone = YardStone.Weathered(YardStone.ColourOf(quarry, tablet), quarry, age, tablet);
            float th = tablet.Range(1.2f, 1.7f), tw = tablet.Range(0.5f, 0.65f);
            Matrix3 fallen = Matrix3.CreateRotationY(tablet.Range(-0.6f, 0.6f));
            Vector3 stoneAt = new Vector3(-Hx - 0.2f, g + 0.1f, -Hz - 0.4f - th * 0.5f);
            Vector3 S(float x, float y, float z) => stoneAt + new Vector3(x, y, z) * fallen;
            YardStone.Solid(b, new[]
            {
                S(-tw, 0f, th * 0.5f), S(tw, 0f, th * 0.5f), S(tw, 0.24f, th * 0.5f), S(-tw, 0.24f, th * 0.5f),
                S(-tw, 0f, -th * 0.5f), S(tw, 0f, -th * 0.5f), S(tw, 0.24f, -th * 0.5f), S(-tw, 0.24f, -th * 0.5f),
            }, 0.03f, stone);
            // The spade, standing in the heap.
            VertexMaterial haft = YardStone.Oak(random, 1.15f), blade = YardStone.Rust(random);
            Vector3 spade = new Vector3(Hx + 1.15f, g + heap * 0.8f, Hz * 0.4f);
            Vector3 handle = spade + new Vector3(0.25f, 1.9f, -0.1f);
            b.Tube(spade, handle, 0.04f, 0.04f, 5, haft);
            b.Beam(handle + new Vector3(-0.15f, 0f, 0f), handle + new Vector3(0.15f, 0f, 0f), 0.03f, haft);
            YardStone.Solid(b, new[]
            {
                spade + new Vector3(-0.16f, -0.5f, 0.02f), spade + new Vector3(0.16f, -0.5f, 0.02f), spade + new Vector3(0.16f, 0.05f, 0.02f), spade + new Vector3(-0.16f, 0.05f, 0.02f),
                spade + new Vector3(-0.16f, -0.5f, -0.02f), spade + new Vector3(0.16f, -0.5f, -0.02f), spade + new Vector3(0.16f, 0.05f, -0.02f), spade + new Vector3(-0.16f, 0.05f, -0.02f),
            }, 0.005f, blade);
            // Bones turned up with the earth, if the dig went deep enough.
            if (!old || random.Chance(0.5f))
            {
                VertexMaterial bone = YardStone.Bone(random);
                for (int i = 0; i < 4; i++)
                {
                    Vector3 at = new Vector3(Hx + 0.6f + random.Range(0f, 1.2f), g + heap * 0.55f, random.Range(-Hz, Hz));
                    float a = random.Float() * MathF.Tau;
                    b.Tube(at, at + new Vector3(MathF.Cos(a), 0.1f, MathF.Sin(a)) * random.Range(0.35f, 0.6f), 0.045f, 0.04f, 5, bone);
                }
                b.Tube(new Vector3(Hx + 0.9f, g + heap * 0.62f, -0.6f), new Vector3(Hx + 0.9f, g + heap * 0.62f + 0.22f, -0.6f), 0.13f, 0.11f, 7, bone);
            }
            return b.Build();
        }

        // ---------------------------------------------------------------- Candles

        public static Mesh Candles(in YardSpec spec, List<AssetLight> lights)
        {
            var b = new MeshBuilder();
            int form = spec.Int(0);
            int count = Math.Clamp(spec.Int(1), 1, 7);
            var random = new WorldRandom(spec.Seed).Fork("candles");
            float g = YardStone.Ground;
            int flames = 0;
            void Candle(Vector3 foot, float height, float radius)
            {
                b.Tube(foot, foot + Vector3.UnitY * height, radius, radius * 0.9f, 7, YardStone.Wax);
                // Drips down its side and a pool at its foot.
                b.Tube(foot - Vector3.UnitY * 0.01f, foot + Vector3.UnitY * 0.03f, radius * 1.7f, radius * 1.5f, 7, YardStone.Wax);
                if (flames++ < 5) lights.Add(AssetLight.Burning(foot + Vector3.UnitY * height, radius * 0.8f));
            }
            switch (form)
            {
                case 1:
                {
                    // Grave lights: candles in glass, each under a little cap of old metal, which burn for days.
                    VertexMaterial cap = YardStone.Rust(random, 0.9f);
                    for (int i = 0; i < count; i++)
                    {
                        float a = i * MathF.Tau / count + random.Range(-0.4f, 0.4f);
                        Vector3 foot = new Vector3(MathF.Cos(a) * 0.3f * (count > 1 ? 1f : 0f), g, MathF.Sin(a) * 0.3f * (count > 1 ? 1f : 0f));
                        float h = random.Range(0.32f, 0.46f);
                        b.Tube(foot, foot + Vector3.UnitY * h, 0.11f, 0.12f, 8, YardStone.Glow(1f, 0.56f, 0.2f, 1.2f + random.Range(0f, 0.4f)));
                        b.Tube(foot + Vector3.UnitY * h, foot + Vector3.UnitY * (h + 0.1f), 0.13f, 0.06f, 8, cap);
                    }
                    lights.Add(YardStone.GraveCandles(new Vector3(0f, 0.9f, 0f)));
                    break;
                }
                case 2:
                {
                    // A bank of candles on an iron stand of three tiers, at a door.
                    VertexMaterial iron = YardStone.Rust(random, 0.8f);
                    for (int tier = 0; tier < 3; tier++)
                    {
                        float y = g + 0.6f + tier * 0.28f;
                        float z = 0.3f - tier * 0.3f;
                        b.Box(new Vector3(0f, y, z), new Vector3(0.95f, 0.03f, 0.16f), iron);
                        for (int i = 0; i < 6; i++) Candle(new Vector3(-0.8f + i * 0.32f + random.Range(-0.04f, 0.04f), y + 0.03f, z + random.Range(-0.05f, 0.05f)), random.Range(0.18f, 0.42f), 0.055f);
                    }
                    foreach (float x in new[] { -0.9f, 0.9f })
                    {
                        b.Beam(new Vector3(x, g - 0.2f, 0.35f), new Vector3(x, g + 0.62f, 0.35f), 0.03f, iron);
                        b.Beam(new Vector3(x, g - 0.2f, -0.35f), new Vector3(x, g + 1.2f, -0.35f), 0.03f, iron);
                    }
                    lights.Add(AssetLight.VigilCandles(new Vector3(0f, 1.6f, 0f)) with { Radius = 11f });
                    break;
                }
                default:
                {
                    // A few left on the turf, on a flat stone, burnt down to different heights.
                    b.Tube(new Vector3(0f, g - 0.1f, 0f), new Vector3(0f, g + 0.05f, 0f), 0.42f, 0.4f, 9, YardStone.Dressed(new Vector3(0.45f, 0.44f, 0.42f), random, 0f));
                    for (int i = 0; i < count; i++)
                    {
                        float a = i * MathF.Tau / count + random.Range(-0.3f, 0.3f);
                        float r = i == 0 ? 0f : random.Range(0.16f, 0.3f);
                        Candle(new Vector3(MathF.Cos(a) * r, g + 0.05f, MathF.Sin(a) * r), random.Range(0.18f, 0.7f), random.Range(0.06f, 0.09f));
                    }
                    lights.Add(YardStone.GraveCandles(new Vector3(0f, 1.1f, 0f)));
                    break;
                }
            }
            return b.Build();
        }

        // ---------------------------------------------------------------- Benches

        public static Mesh Bench(in YardSpec spec)
        {
            var b = new MeshBuilder();
            var random = new WorldRandom(spec.Seed).Fork("bench");
            float half = MathF.Max(spec.Metres(0), 1.6f) * 0.5f;
            float g = YardStone.Ground;
            Func<Vector3, VertexMaterial> stone = YardStone.Weathered(new Vector3(0.48f, 0.47f, 0.44f), YardStone.Quarry.Limestone, 0.7f, random);
            foreach (float s in new[] { -1f, 1f })
            {
                YardStone.Box(b, new Vector3(s * (half - 0.2f) - 0.15f, -0.3f, -0.4f), new Vector3(s * (half - 0.2f) + 0.15f, g + 0.85f, 0.4f), 0.03f, stone);
            }
            VertexMaterial oak = YardStone.Silvered(random);
            for (int i = 0; i < 3; i++)
            {
                float z = -0.3f + i * 0.3f;
                YardStone.Box(b, new Vector3(-half, g + 0.85f, z - 0.13f), new Vector3(half, g + 0.95f, z + 0.13f), 0.015f, _ => oak);
            }
            return b.Build();
        }
    }
}

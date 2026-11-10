using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.Modelling.Procedural;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Waygates
{
    /// <summary>What a way is drawn from: its face and its runes' light, in the gate's own frame. A vertex's colour says what it is (x: 0 the way, 1 a rune; y: when a rune lights, 0 to 1; z: which rune).</summary>
    public sealed class WayMesh
    {
        public readonly List<Vector3> Positions = new();
        public readonly List<Vector3> Colours = new();
        public readonly List<Vector3> Normals = new();
        public readonly List<Vector2> UVs = new();
        public readonly List<int> Indices = new();

        public int Add(Vector3 position, Vector3 colour, Vector3 normal, Vector2 uv)
        {
            Positions.Add(position);
            Colours.Add(colour);
            Normals.Add(normal);
            UVs.Add(uv);
            return Positions.Count - 1;
        }
    }

    /// <summary>
    /// Builds a waygate from its <see cref="WaygateDesign"/>: the frame the world stands in it (<see cref="Frame"/>), and what the game stands in that once the way is open (<see cref="Way"/>),
    /// so the two can't disagree. The frame is dressed stone laid as stone is laid: a plinth of slabs, jambs of coursed blocks on a base under an impost, and an arch of wedge-shaped stones
    /// on radial joints round a keystone, each a block of its own with its arrises taken off (<see cref="Masonry"/>), touching its neighbours face to face and overlapping nothing anyone can
    /// see. The Haven's is the same road raised higher: three steps, piers with columns, an arch of two rings, coursed spandrels under a cornice, and a crystal held up over all of it.
    /// Runes are cut where the camera can read them, on the faces of the arch and along the treads, each a dark inlay in the frame with its light standing just proud of it in the way.
    /// Headless.
    /// </summary>
    public static class WaygateForge
    {
        private const float Chamfer = 0.05f;
        private const float BigChamfer = 0.065f;

        /// <summary>The frame, into <paramref name="b"/>, with any light it gives of its own in <paramref name="lights"/>. A land's gives none: it's dark until someone comes near.</summary>
        public static void Frame(MeshBuilder b, WaygateDesign d, List<AssetLight> lights)
        {
            var genes = new Genes(d.Kind == WaygateKind.Great ? 0x6A7E5EEDUL : 0x1A7E5EEDUL);
            bool great = d.Kind == WaygateKind.Great;

            Plinth(b, d, genes.Part("plinth"));
            Jambs(b, d, genes.Part("jambs"));
            for (int r = 0; r < d.Rings.Length; r++) Ring(b, d, r, genes.Part("ring").Part(r));
            if (great)
            {
                Spandrels(b, d, genes.Part("spandrels"));
                Crown(b, d, genes.Part("crown"), lights);
                Pylons(b, d, genes.Part("pylons"), lights);
            }
            Inlays(b, d);
        }

        // ---------------------------------------------------------------- Stone

        // A stone's own shade: the Haven's dressed pale and warm, a land's older, greyer and greener, and darker towards the ground on both, where the weather has had longest.
        private static VertexMaterial Stone(WaygateDesign d, Genes genes, string name, float y = 2f)
        {
            float shade = genes.Around(name, 1f, 0.045f) * WaygateDesign.WeatherShade(y);
            float warm = genes.Range(name + " warm", -0.025f, 0.025f);
            Vector3 colour = d.Kind == WaygateKind.Great ? WaygateDesign.GreatStone : WaygateDesign.LandStone;
            colour *= shade;
            return new VertexMaterial(MaterialCatalog.Rock.Index, new Colour(colour.X * (1f + warm), colour.Y, colour.Z * (1f - warm)));
        }

        // The plinth: tiers of slabs, each slab the whole depth of its tier, so that no joint runs under the way itself.
        private static void Plinth(MeshBuilder b, WaygateDesign d, Genes genes)
        {
            float bottom = -d.Footing;
            for (int t = 0; t < d.Tiers.Length; t++)
            {
                (float halfWidth, float halfDepth, float top, int stones) = d.Tiers[t];
                float width = halfWidth * 2f / stones;
                for (int i = 0; i < stones; i++)
                {
                    float x0 = -halfWidth + i * width;
                    Masonry.Box(b, new Vector3(x0, bottom, -halfDepth), new Vector3(x0 + width, top, halfDepth), Chamfer, Stone(d, genes, $"{t}.{i}", top));
                }
                bottom = top;
            }
        }

        // Each jamb: a base, its courses, alternately long and short on the outside, and an impost the arch springs from. Its inside face is the opening's edge, all the way up.
        private static void Jambs(MeshBuilder b, WaygateDesign d, Genes genes)
        {
            bool great = d.Kind == WaygateKind.Great;
            float t = d.JambHalfThick;
            float baseProud = great ? 0.41f : 0.12f;
            float impostProud = great ? 0.43f : 0.14f;

            // The courses are stretched to fill exactly what's between the base and the impost.
            float from = d.Sill + d.JambBase;
            float to = d.Spring - d.ImpostHeight;
            float written = d.Courses.Sum();

            for (int s = -1; s <= 1; s += 2)
            {
                Genes side = genes.Part(s);
                Box(b, s, d.HalfWidth, d.JambOuter + baseProud, d.Sill, from, t + baseProud, BigChamfer, Stone(d, side, "base", d.Sill));

                float y = from;
                for (int c = 0; c < d.Courses.Length; c++)
                {
                    float top = c == d.Courses.Length - 1 ? to : y + d.Courses[c] * (to - from) / written;
                    float outer = d.JambOuter - (c % 2 == 1 ? 0.12f : 0f);
                    Box(b, s, d.HalfWidth, outer, y, top, t, Chamfer, Stone(d, side, $"course {c}", y));
                    y = top;
                }
                Box(b, s, d.HalfWidth, d.JambOuter + impostProud, to, d.Spring, t + impostProud, BigChamfer, Stone(d, side, "impost", to));

                // The Haven's piers have a column on each face, half sunk in the stone, from the base to the impost.
                if (great)
                {
                    float x = s * (d.HalfWidth + d.JambOuter) * 0.5f;
                    foreach (float face in new[] { t, -t })
                    {
                        b.Tube(new Vector3(x, from - 0.05f, face), new Vector3(x, to + 0.05f, face), 0.36f, 0.34f, 14, Stone(d, side, $"column {face}", 3f), capped: false);
                    }
                }
            }
        }

        // A block square to the axes on one side, from the opening's edge out: x runs from `inner` to `outer`, mirrored for the left.
        private static void Box(MeshBuilder b, int side, float inner, float outer, float y0, float y1, float halfThick, float chamfer, VertexMaterial material)
        {
            float x0 = side > 0 ? inner : -outer;
            float x1 = side > 0 ? outer : -inner;
            Masonry.Box(b, new Vector3(x0, y0, -halfThick), new Vector3(x1, y1, halfThick), chamfer, material);
        }

        // ---------------------------------------------------------------- The arch

        /// <summary>The joints of one side of a ring, as angles up from the springing line on the right-hand arc: stone i lies between i and i + 1, and the keystone from the last up to the middle.</summary>
        public static float[] Joints(WaygateDesign d, ArchRing ring)
        {
            float top = d.ArcTopAt(ring.Inner) - ring.KeyHalf;
            var joints = new float[ring.Stones + 1];
            for (int i = 0; i <= ring.Stones; i++) joints[i] = top * i / ring.Stones;
            return joints;
        }

        // The corners of a ring's stone in the xy plane, counter-clockwise seen from the front: on the right between two joint angles, or its mirror on the left. A ring that stands on
        // another dips into it a few centimetres, as the faces of each are flat between their own joints and the two would otherwise leave slivers of daylight where they don't line up.
        private static Vector2[] StoneShape(WaygateDesign d, int r, float a0, float a1, int side)
        {
            ArchRing ring = d.Rings[r];
            float inner = ring.Inner - (r > 0 ? 0.035f : 0f);
            Vector2 p0 = d.OnArc(inner, a0), p1 = d.OnArc(ring.Outer, a0), p2 = d.OnArc(ring.Outer, a1), p3 = d.OnArc(inner, a1);
            if (side > 0) return new[] { p0, p1, p2, p3 };
            return new[] { Mirror(p3), Mirror(p2), Mirror(p1), Mirror(p0) };
        }

        // The keystone: from the last joint on each side up and out, as far as the ring and its lift.
        private static Vector2[] KeyShape(WaygateDesign d, int r)
        {
            ArchRing ring = d.Rings[r];
            float inner = ring.Inner - (r > 0 ? 0.035f : 0f);
            float a = d.ArcTopAt(ring.Inner) - ring.KeyHalf;
            Vector2 low = d.OnArc(inner, a);
            Vector2 high = d.OnArc(ring.Outer + ring.KeyLift, a);
            return new[] { Mirror(low), low, high, Mirror(high) };
        }

        private static Vector2 Mirror(Vector2 p) => new Vector2(-p.X, p.Y);

        private static void Ring(MeshBuilder b, WaygateDesign d, int r, Genes genes)
        {
            ArchRing ring = d.Rings[r];
            float[] joints = Joints(d, ring);
            for (int s = -1; s <= 1; s += 2)
            {
                for (int i = 0; i < ring.Stones; i++)
                {
                    Vector2[] shape = StoneShape(d, r, joints[i], joints[i + 1], s);
                    Masonry.Prism(b, shape[0], shape[1], shape[2], shape[3], -ring.HalfThick, ring.HalfThick, Chamfer, Stone(d, genes.Part(s), $"{i}", shape[0].Y));
                }
            }
            Vector2[] key = KeyShape(d, r);
            float proud = ring.HalfThick + ring.KeyProud;
            Masonry.Prism(b, key[0], key[1], key[2], key[3], -proud, proud, BigChamfer, Stone(d, genes, "key"));
        }

        // ---------------------------------------------------------------- The Haven's

        private const float CorniceBottom = 12.5f;
        private const float CorniceTop = 13.12f;
        private const float AtticTop = 13.7f;
        private const float PedestalTop = 14.2f;
        private const float SpandrelHalfThick = 0.5f;
        private const float CrystalLow = 14.4f;
        private const float CrystalMiddle = 15.2f;
        private const float CrystalHigh = 16.5f;

        // The walls between the arch and the cornice, in courses that meet the outer ring wherever it's cut by them, each course split with its joints over the middle of the one below.
        private static void Spandrels(MeshBuilder b, WaygateDesign d, Genes genes)
        {
            ArchRing outer = d.Rings[^1];
            float radius = outer.Outer - 0.06f;
            float apex = d.Spring + MathF.Sqrt(radius * radius - d.Offset * d.Offset);
            float wall = d.JambOuter;
            float AtHeight(float y) => -d.Offset + MathF.Sqrt(MathF.Max(radius * radius - (y - d.Spring) * (y - d.Spring), 0f));

            // As many courses as fit to the apex, and the rest to the cornice.
            int below = Math.Max(1, (int)MathF.Round((apex - d.Spring) / 0.78f));
            var heights = new List<float>();
            for (int i = 0; i <= below; i++) heights.Add(d.Spring + (apex - d.Spring) * i / below);
            int above = Math.Max(1, (int)MathF.Round((CorniceBottom - apex) / 0.78f));
            for (int i = 1; i <= above; i++) heights.Add(apex + (CorniceBottom - apex) * i / above);

            for (int c = 0; c + 1 < heights.Count; c++)
            {
                float y0 = heights[c], y1 = heights[c + 1];
                Genes course = genes.Part(c);
                if (y1 <= apex + 1e-4f)
                {
                    float inner0 = AtHeight(y0), inner1 = AtHeight(y1);
                    float joint = MathF.Max(inner0, inner1) + (wall - MathF.Max(inner0, inner1)) * (c % 2 == 0 ? 0.42f : 0.62f);
                    bool split = wall - MathF.Max(inner0, inner1) > 1.5f;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        if (split)
                        {
                            Course(b, s, new Vector2(inner0, y0), new Vector2(joint, y0), new Vector2(joint, y1), new Vector2(inner1, y1), Stone(d, course.Part(s), "in", y0));
                            Course(b, s, new Vector2(joint, y0), new Vector2(wall, y0), new Vector2(wall, y1), new Vector2(joint, y1), Stone(d, course.Part(s), "out", y0));
                        }
                        else
                        {
                            Course(b, s, new Vector2(inner0, y0), new Vector2(wall, y0), new Vector2(wall, y1), new Vector2(inner1, y1), Stone(d, course.Part(s), "in", y0));
                        }
                    }
                }
                else
                {
                    // Over the apex, a course runs right across, in three stones.
                    float joint = c % 2 == 0 ? 1.9f : 2.6f;
                    float[] xs = { -wall, -joint, joint, wall };
                    for (int k = 0; k < 3; k++)
                    {
                        Masonry.Prism(b, new Vector2(xs[k], y0), new Vector2(xs[k + 1], y0), new Vector2(xs[k + 1], y1), new Vector2(xs[k], y1),
                            -SpandrelHalfThick, SpandrelHalfThick, Chamfer, Stone(d, course.Part(k), "across", y0));
                    }
                }
            }
        }

        // One stone of a spandrel's course on the right, or its mirror on the left.
        private static void Course(MeshBuilder b, int side, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, VertexMaterial material)
        {
            if (side > 0) Masonry.Prism(b, p0, p1, p2, p3, -SpandrelHalfThick, SpandrelHalfThick, Chamfer, material);
            else Masonry.Prism(b, Mirror(p1), Mirror(p0), Mirror(p3), Mirror(p2), -SpandrelHalfThick, SpandrelHalfThick, Chamfer, material);
        }

        // The cornice, the attic over it, a pedestal, and the crystal held up on four claws, which is what's seen first from anywhere in the village.
        private static void Crown(MeshBuilder b, WaygateDesign d, Genes genes, List<AssetLight> lights)
        {
            float[] xs = { -6.75f, -2.2f, 2.2f, 6.75f };
            for (int k = 0; k < 3; k++)
            {
                Masonry.Box(b, new Vector3(xs[k], CorniceBottom, -1.12f), new Vector3(xs[k + 1], CorniceTop, 1.12f), BigChamfer, Stone(d, genes.Part(k), "cornice", 9f));
            }
            Masonry.Box(b, new Vector3(-3f, CorniceTop, -0.8f), new Vector3(3f, AtticTop, 0.8f), BigChamfer, Stone(d, genes, "attic", 9f));
            Masonry.Box(b, new Vector3(-1f, AtticTop, -0.65f), new Vector3(1f, PedestalTop, 0.65f), BigChamfer, Stone(d, genes, "pedestal", 9f));

            const float Girth = 0.58f;
            VertexMaterial claw = Stone(d, genes, "claw", 9f);
            for (int i = 0; i < 4; i++)
            {
                float angle = MathF.PI * 0.25f + i * MathF.PI * 0.5f;
                Vector3 way = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                b.Beam(new Vector3(0f, PedestalTop - 0.05f, 0f) + way * 0.55f, new Vector3(0f, CrystalMiddle - 0.15f, 0f) + way * (Girth * 0.82f), 0.07f, claw);
            }

            // A crystal of six faces each way, pale, as the colour of where the way goes is the way's.
            VertexMaterial glow = new VertexMaterial(MaterialCatalog.FlatGlow.Index, new Colour(0.55f, 0.88f, 1f, 2.2f));
            Vector3 top = new Vector3(0f, CrystalHigh, 0f), foot = new Vector3(0f, CrystalLow, 0f);
            for (int i = 0; i < 6; i++)
            {
                float a0 = i * MathF.Tau / 6f, a1 = (i + 1) * MathF.Tau / 6f;
                Vector3 e0 = new Vector3(MathF.Cos(a0) * Girth, CrystalMiddle, MathF.Sin(a0) * Girth);
                Vector3 e1 = new Vector3(MathF.Cos(a1) * Girth, CrystalMiddle, MathF.Sin(a1) * Girth);
                b.Triangle(e1, e0, top, glow);
                b.Triangle(e0, e1, foot, glow);
            }
            lights.Add(AssetLight.Glow(new Vector3(0f, CrystalMiddle, 0f), new Vector3(0.45f, 0.86f, 1f), 120f, 26f));
        }

        // Wings either side, stepped, each with a lamp of the same cold light on top.
        private static void Pylons(MeshBuilder b, WaygateDesign d, Genes genes, List<AssetLight> lights)
        {
            float tier = d.Tiers[^1].Top;
            VertexMaterial glow = new VertexMaterial(MaterialCatalog.FlatGlow.Index, new Colour(0.5f, 0.9f, 1f, 2.2f));
            for (int s = -1; s <= 1; s += 2)
            {
                Genes side = genes.Part(s);
                Box(b, s, 6.45f, 7.75f, tier, 2.9f, 1.25f, BigChamfer, Stone(d, side, "low", tier));
                Box(b, s, 6.5f, 7.3f, 2.9f, 4.3f, 1f, Chamfer, Stone(d, side, "high", 2.9f));
                Box(b, s, 6.4f, 7.4f, 4.3f, 4.52f, 1.1f, Chamfer, Stone(d, side, "cap", 4.3f));
                float x = s * 6.9f;
                b.Tube(new Vector3(x, 4.52f, 0f), new Vector3(x, 4.8f, 0f), 0.26f, 0.38f, 12, Stone(d, side, "bowl", 4.5f));
                b.Add(IcoSphereGenerator.CreateIcosphere(1, glow), new Vector3(x, 4.96f, 0f), new Vector3(0.26f));
                lights.Add(AssetLight.Glow(new Vector3(x, 5.2f, 0f), new Vector3(0.45f, 0.86f, 1f), 46f, 13f));
            }
        }

        // ---------------------------------------------------------------- Runes

        /// <summary>Where every rune of a gate is cut, in the order they light: along the treads first, from their ends in, then up the arch from both springings to the keystone.</summary>
        internal static List<RuneSlot> Runes(WaygateDesign d)
        {
            var runes = new List<RuneSlot>();
            bool great = d.Kind == WaygateKind.Great;
            int glyph = great ? 7000 : 3000;

            // Along the treads, centred on the slabs (every other one along the long outer treads, so that they read as a line of letters and not a pattern), front and back.
            void Row(int tier, float z, float within, float from, float to, float size, bool back, int every = 1)
            {
                (float halfWidth, _, float top, int stones) = d.Tiers[tier];
                float width = halfWidth * 2f / stones;
                for (int i = 0; i < stones; i++)
                {
                    float x = -halfWidth + (i + 0.5f) * width;
                    if (MathF.Abs(x) > within || (i - stones / 2) % every != 0) continue;
                    Vector3 right = back ? -Vector3.UnitX : Vector3.UnitX;
                    Vector3 up = back ? Vector3.UnitZ : -Vector3.UnitZ;
                    float along = from + (to - from) * (1f - MathF.Abs(x) / MathF.Max(within, 1e-3f));
                    runes.Add(new RuneSlot(new Vector3(x, top, back ? -z : z), right, up, Vector3.UnitY, size, glyph++, along));
                }
            }

            if (great)
            {
                foreach (bool back in new[] { false, true })
                {
                    if (!back) Row(0, (d.Tiers[0].HalfDepth + d.Tiers[1].HalfDepth) * 0.5f, 7.9f, 0f, 0.08f, 0.42f, back, every: 2);
                    Row(1, (d.Tiers[1].HalfDepth + d.Tiers[2].HalfDepth) * 0.5f, 7.6f, 0.1f, 0.18f, 0.42f, back, every: 2);
                    Row(2, (d.JambHalfThick + 0.41f + d.Tiers[2].HalfDepth) * 0.5f, d.HalfWidth - 0.4f, 0.2f, 0.28f, 0.4f, back);
                }
            }
            else
            {
                foreach (bool back in new[] { false, true }) Row(1, (d.JambHalfThick + 0.12f + d.Tiers[1].HalfDepth) * 0.5f, d.HalfWidth - 0.3f, 0f, 0.14f, 0.38f, back);
            }

            // One on each face of every stone of the inner ring, from the springings up to the keystone.
            ArchRing ring = d.Rings[0];
            float[] joints = Joints(d, ring);
            float firstRing = great ? 0.34f : 0.2f;
            foreach (bool back in new[] { false, true })
            {
                float z = back ? -ring.HalfThick : ring.HalfThick;
                Vector3 normal = back ? -Vector3.UnitZ : Vector3.UnitZ;
                for (int s = -1; s <= 1; s += 2)
                {
                    for (int i = 0; i < ring.Stones; i++)
                    {
                        Vector2[] shape = StoneShape(d, 0, joints[i], joints[i + 1], s);
                        Vector2 middle = (shape[0] + shape[1] + shape[2] + shape[3]) * 0.25f;
                        float angle = (joints[i] + joints[i + 1]) * 0.5f;
                        Vector2 outward = new Vector2(MathF.Cos(angle) * s, MathF.Sin(angle));
                        Vector2 across = new Vector2(outward.Y, -outward.X);
                        float size = MathF.Min(ring.Outer - ring.Inner, (shape[3] - shape[0]).Length) * 0.62f;
                        Vector3 up = new Vector3(outward.X, outward.Y, 0f);
                        Vector3 right = back ? new Vector3(-across.X, -across.Y, 0f) : new Vector3(across.X, across.Y, 0f);
                        // Seen from the front a glyph's right is to the viewer's right and its up out from the arch: Right x Up has to point out of the face.
                        if (Vector3.Dot(Vector3.Cross(right, up), normal) < 0f) right = -right;
                        float along = firstRing + (0.9f - firstRing) * (joints[i] + joints[i + 1]) * 0.5f / joints[^1];
                        runes.Add(new RuneSlot(new Vector3(middle.X, middle.Y, z), right, up, normal, size, glyph++, along));
                    }
                }
                Vector2[] key = KeyShape(d, 0);
                Vector2 keyMiddle = new Vector2(0f, (key[0].Y + d.Apex) * 0.25f + (key[2].Y + key[3].Y) * 0.25f);
                float keyZ = (ring.HalfThick + ring.KeyProud) * (back ? -1f : 1f);
                Vector3 keyRight = back ? -Vector3.UnitX : Vector3.UnitX;
                runes.Add(new RuneSlot(new Vector3(0f, keyMiddle.Y, keyZ), keyRight, Vector3.UnitY, normal, (key[1].X - key[0].X) * 0.55f, glyph++, 0.93f));
            }

            // And the Haven's cornice carries a line of them across its face.
            if (great)
            {
                foreach (bool back in new[] { false, true })
                {
                    for (int i = 0; i < 9; i++)
                    {
                        float x = -5.6f + i * 1.4f;
                        Vector3 right = back ? -Vector3.UnitX : Vector3.UnitX;
                        Vector3 normal = back ? -Vector3.UnitZ : Vector3.UnitZ;
                        runes.Add(new RuneSlot(new Vector3(x, (CorniceBottom + CorniceTop) * 0.5f, back ? -1.12f : 1.12f), right, Vector3.UnitY, normal, 0.46f, glyph++, 0.95f + 0.05f * MathF.Abs(x) / 5.6f));
                    }
                }
            }
            return runes;
        }

        // The runes cut into the frame: dark stone set in a land's gate, and a cold glow in the Haven's, which is never quite dark. Their light, when the way is open, stands just proud of them.
        private static void Inlays(MeshBuilder b, WaygateDesign d)
        {
            VertexMaterial inlay = d.Kind == WaygateKind.Great
                ? new VertexMaterial(MaterialCatalog.FlatGlow.Index, new Colour(0.3f, 0.74f, 1f, 0.45f))
                : new VertexMaterial(MaterialCatalog.Flat.Index, new Colour(0.15f, 0.16f, 0.19f));
            foreach (RuneSlot rune in d.Runes)
            {
                foreach ((Vector3 a, Vector3 bb, Vector3 c, Vector3 e, _) in StrokeQuads(rune, WaygateDesign.InlayLift))
                {
                    b.Quad(a, bb, c, e, inlay);
                }
            }
        }

        /// <summary>
        /// A rune as quads in its face, <paramref name="lift"/> proud of it, each wound to face out of it, with how far across its stroke each corner is (-1 to 1). Strokes run as strips mitred at their
        /// bends, so nothing of one stroke lies over another part of it.
        /// </summary>
        internal static IEnumerable<(Vector3 A, Vector3 B, Vector3 C, Vector3 D, float Width)> StrokeQuads(RuneSlot rune, float lift)
        {
            Vector3 Place(Vector2 g) => rune.Centre + rune.Normal * lift + (rune.Right * g.X + rune.Up * g.Y) * rune.Size;
            foreach (Glyphs.Stroke stroke in Glyphs.Of(rune.Glyph))
            {
                Vector2[] p = stroke.Points;
                float half = stroke.Width * 0.5f;
                var left = new Vector2[p.Length];
                var right = new Vector2[p.Length];
                for (int i = 0; i < p.Length; i++)
                {
                    Vector2 inWay = i > 0 ? (p[i] - p[i - 1]).Normalized() : (p[1] - p[0]).Normalized();
                    Vector2 outWay = i < p.Length - 1 ? (p[i + 1] - p[i]).Normalized() : inWay;
                    Vector2 tangent = (inWay + outWay).LengthSquared > 1e-6f ? (inWay + outWay).Normalized() : inWay;
                    Vector2 normal = new Vector2(-tangent.Y, tangent.X);
                    float miter = 1f / MathF.Max(Vector2.Dot(normal, new Vector2(-inWay.Y, inWay.X)), 0.5f);
                    left[i] = p[i] + normal * (half * miter);
                    right[i] = p[i] - normal * (half * miter);
                }
                for (int i = 0; i + 1 < p.Length; i++)
                {
                    yield return (Place(right[i]), Place(right[i + 1]), Place(left[i + 1]), Place(left[i]), stroke.Width);
                }
            }
        }

        // ---------------------------------------------------------------- The way

        /// <summary>What stands in the gate once its way is open: the way's face, the opening grown by <see cref="WaygateDesign.Tuck"/> into the stone, and the light of every rune.</summary>
        public static WayMesh Way(WaygateDesign d)
        {
            var mesh = new WayMesh();
            List<Vector2> edge = d.Outline(WaygateDesign.Tuck, 32);
            Vector2 heart = d.Heart;
            int middle = mesh.Add(new Vector3(heart.X, heart.Y, 0f), Vector3.Zero, Vector3.UnitZ, heart);
            int first = mesh.Positions.Count;
            foreach (Vector2 p in edge) mesh.Add(new Vector3(p.X, p.Y, 0f), Vector3.Zero, Vector3.UnitZ, p);
            for (int i = 0; i < edge.Count; i++)
            {
                mesh.Indices.Add(middle);
                mesh.Indices.Add(first + i);
                mesh.Indices.Add(first + (i + 1) % edge.Count);
            }

            foreach (RuneSlot rune in d.Runes)
            {
                float which = (rune.Glyph % 997) / 997f;
                foreach ((Vector3 a, Vector3 b, Vector3 c, Vector3 e, _) in StrokeQuads(rune, WaygateDesign.GlowLift))
                {
                    Vector3 colour = new Vector3(1f, rune.Along, which);
                    int i0 = mesh.Add(a, colour, rune.Normal, new Vector2(-1f, 0f));
                    int i1 = mesh.Add(b, colour, rune.Normal, new Vector2(-1f, 1f));
                    int i2 = mesh.Add(c, colour, rune.Normal, new Vector2(1f, 1f));
                    int i3 = mesh.Add(e, colour, rune.Normal, new Vector2(1f, 0f));
                    mesh.Indices.AddRange(new[] { i0, i1, i2, i0, i2, i3 });
                }
            }
            return mesh;
        }
    }
}

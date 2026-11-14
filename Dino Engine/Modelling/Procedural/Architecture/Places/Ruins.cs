using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture.Waygates;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using OpenTK.Mathematics;
using static Dino_Engine.Modelling.Procedural.Architecture.Places.PlaceMaterials;

namespace Dino_Engine.Modelling.Procedural.Architecture.Places
{
    /// <summary>
    /// Ruins (PLACES.md), raised by the same hands as the waygates and fallen long before anyone came: walls of their ashlar in courses, broken off ragged and their stones lying at their foot;
    /// columns standing to where they broke, and others lying in their drums where they fell; an arch still standing on its piers, or half of one; a flight of steps up to nothing; blocks carved
    /// with the waygates' own runes, which nobody can read; and what's left of the figures that stood there. Their stone is a land's waygate's (<see cref="WaygateDesign.LandStone"/>), older and
    /// greener, so that the two are plainly one people's work. Headless.
    /// </summary>
    internal static class Ruins
    {
        // The waygates' stone, as a land's gate is faced with it, older again: its shade and warmth as the id says, and darker towards the ground.
        private static VertexMaterial Stone(in PlaceSpec spec, int first, WorldRandom random, float y = 2f)
        {
            float shade = Math.Clamp(spec.Mille(first), 0.7f, 1.2f) * WaygateDesign.WeatherShade(y) * random.Range(0.95f, 1.04f);
            float warm = Math.Clamp(spec.Mille(first + 1), -0.06f, 0.06f);
            Vector3 colour = WaygateDesign.LandStone * new Vector3(0.9f, 0.94f, 0.88f) * shade;
            return new VertexMaterial(MaterialCatalog.Rock.Index, new Colour(colour.X * (1f + warm), colour.Y, colour.Z * (1f - warm)));
        }

        /// <summary>
        /// A stretch of wall along x, its ashlar in courses with the joints broken between them, standing to its full height at its ends and broken off ragged between, a doorway or a window
        /// left in it, and some of its stones fallen at its foot. Numbers: length, height, gap, shade, warmth.
        /// </summary>
        public static void Wall(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float length = Math.Clamp(spec.Metres(0), 2f, 12f), height = Math.Clamp(spec.Metres(1), 0.5f, 6.5f);
            int gap = Math.Clamp(spec.Int(2), 0, 2);
            const float Thick = 0.85f, Course = 0.5f;
            float half = length * 0.5f;
            // Where it stands to along its length: highest at its ends, broken down between in a ragged line.
            int points = Math.Max(2, (int)(length / 1.4f)) + 1;
            float[] top = new float[points];
            for (int i = 0; i < points; i++)
            {
                float t = i / (float)(points - 1);
                float ends = MathF.Pow(MathF.Abs(t - 0.5f) * 2f, 2f);
                top[i] = height * MathHelper.Lerp(random.Range(0.3f, 0.75f), random.Range(0.8f, 1f), ends);
            }
            float TopAt(float x)
            {
                float f = (x + half) / length * (points - 1);
                int i = Math.Clamp((int)f, 0, points - 2);
                return MathHelper.Lerp(top[i], top[i + 1], f - i);
            }
            bool Open(float x0, float x1, float y0, float y1) => gap switch
            {
                1 => x1 > -0.8f && x0 < 0.8f && y0 < 2.3f,
                2 => x1 > -0.55f && x0 < 0.55f && y1 > 1.1f && y0 < 2f,
                _ => false,
            };
            int courses = (int)MathF.Ceiling(height / Course) + 1;
            for (int c = 0; c < courses; c++)
            {
                float y0 = -0.35f + c * Course, y1 = y0 + Course - 0.015f;
                float x = -half + (c % 2 == 1 ? -random.Range(0.3f, 0.5f) : 0f);
                while (x < half - 0.05f)
                {
                    float w = random.Range(0.75f, 1.35f);
                    float x0 = MathF.Max(x, -half), x1 = MathF.Min(x + w, half);
                    x += w;
                    if (x1 - x0 < 0.25f) continue;
                    float standing = TopAt((x0 + x1) * 0.5f);
                    if (y1 > standing && (y0 > standing || random.Chance(0.55f))) continue;
                    if (Open(x0, x1, y0, y1)) continue;
                    float inset = random.Range(0f, 0.04f);
                    Masonry.Box(b, new Vector3(x0 + 0.01f, y0, -Thick * 0.5f + inset), new Vector3(x1 - 0.01f, y1, Thick * 0.5f - random.Range(0f, 0.04f)), 0.05f, Stone(spec, 3, random, y0));
                }
            }
            // A lintel over a doorway, where the wall still stands that high.
            if (gap == 1 && TopAt(0f) > 2.9f) Masonry.Box(b, new Vector3(-1.15f, 2.3f, -Thick * 0.5f), new Vector3(1.15f, 2.85f, Thick * 0.5f), 0.05f, Stone(spec, 3, random, 2.5f));
            // Stones fallen at its foot, either side.
            int fallen = random.Int(2, 5);
            for (int k = 0; k < fallen; k++)
            {
                Vector3 size = new Vector3(random.Range(0.35f, 0.65f), random.Range(0.2f, 0.26f), random.Range(0.3f, 0.42f));
                float side = random.Chance(0.5f) ? 1f : -1f;
                Vector3 at = new Vector3(random.Range(-half, half), size.Y * 0.8f, side * (Thick * 0.5f + random.Range(0.5f, 1.8f)));
                Kit.Block(b, at, size, random.Float() * MathF.Tau, 0.04f, Stone(spec, 3, random, 0.3f));
            }
        }

        /// <summary>A column on its plinth and base, its drums each a little out of true, standing to where it broke, or whole with its capital if it's tall. Numbers: height, fluted, shade, warmth.</summary>
        public static void Column(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float height = Math.Clamp(spec.Metres(0), 0.6f, 7f);
            bool fluted = spec.Int(1) == 1;
            const float Radius = 0.4f, Drum = 0.85f;
            int around = fluted ? 20 : 14;
            Masonry.Box(b, new Vector3(-0.6f, -0.3f, -0.6f), new Vector3(0.6f, 0.3f, 0.6f), 0.05f, Stone(spec, 2, random, 0.2f));
            Kit.Lathe(b, new Vector3(0f, 0.3f, 0f), Vector3.UnitY, 0.28f, Kit.Profile((0f, Radius * 1.3f), (0.5f, Radius * 1.22f), (1f, Radius * 1.05f)), around, 2, Stone(spec, 2, random, 0.5f));
            float y = 0.58f;
            Vector2 drift = Vector2.Zero;
            while (y < height - 0.1f)
            {
                float h = MathF.Min(Drum * random.Range(0.85f, 1.1f), height - y);
                drift += new Vector2(random.Range(-0.025f, 0.025f), random.Range(-0.025f, 0.025f));
                float r0 = Radius * (1f - 0.08f * y / 7f), r1 = Radius * (1f - 0.08f * (y + h) / 7f);
                Kit.Lathe(b, new Vector3(drift.X, y, drift.Y), Vector3.UnitY, h - 0.01f, Kit.Profile((0f, r0), (1f, r1)), around, 1, Stone(spec, 2, random, y));
                if (fluted) for (int k = 0; k < around; k += 2)
                    {
                        float a = k * MathF.Tau / around;
                        Vector3 dir = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a));
                        Kit.Face(b, new Vector3(drift.X, y + 0.04f, drift.Y) + dir * (r0 + 0.004f) + Vector3.Cross(Vector3.UnitY, dir) * 0.035f, new Vector3(drift.X, y + 0.04f, drift.Y) + dir * (r0 + 0.004f) - Vector3.Cross(Vector3.UnitY, dir) * 0.035f, new Vector3(drift.X, y + h - 0.04f, drift.Y) + dir * (r1 + 0.004f) - Vector3.Cross(Vector3.UnitY, dir) * 0.035f, new Vector3(drift.X, y + h - 0.04f, drift.Y) + dir * (r1 + 0.004f) + Vector3.Cross(Vector3.UnitY, dir) * 0.035f, dir, Tint(Stone(spec, 2, random, y + 1f), new Vector3(0.82f)));
                    }
                y += h;
            }
            if (height >= 5.5f)
            {
                // Whole, with its capital and the block that carried what it held up.
                Kit.Lathe(b, new Vector3(drift.X, y, drift.Y), Vector3.UnitY, 0.3f, Kit.Profile((0f, Radius * 0.95f), (0.6f, Radius * 1.25f), (1f, Radius * 1.4f)), around, 2, Stone(spec, 2, random, y));
                Masonry.Box(b, new Vector3(drift.X - 0.65f, y + 0.3f, drift.Y - 0.65f), new Vector3(drift.X + 0.65f, y + 0.6f, drift.Y + 0.65f), 0.04f, Stone(spec, 2, random, y));
            }
            else
            {
                // Broken: a jag of its last drum standing on one side.
                float a = random.Float() * MathF.Tau;
                Vector3 jag = new Vector3(drift.X + MathF.Cos(a) * Radius * 0.35f, y + 0.12f, drift.Y + MathF.Sin(a) * Radius * 0.35f);
                Kit.Block(b, jag, new Vector3(Radius * 0.45f, 0.14f, Radius * 0.3f), a, 0.04f, Stone(spec, 2, random, y));
            }
        }

        /// <summary>A column lying where it fell along x, its drums rolled a little apart and turned, its capital at its end and its base left standing. Numbers: length, shade, warmth.</summary>
        public static void FallenColumn(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float length = Math.Clamp(spec.Metres(0), 2f, 8f);
            const float Radius = 0.4f;
            float x = -length * 0.5f;
            while (x < length * 0.5f - 0.3f)
            {
                float h = MathF.Min(random.Range(0.75f, 0.95f), length * 0.5f - x);
                float z = random.Range(-0.25f, 0.25f), turn = random.Range(-0.25f, 0.25f);
                Vector3 axis = new Vector3(MathF.Cos(turn), 0f, MathF.Sin(turn));
                Kit.Lathe(b, new Vector3(x, Radius - 0.08f, z), axis, h - 0.02f, Kit.Profile((0f, Radius), (1f, Radius * 0.99f)), 14, 1, Stone(spec, 1, random, 0.5f));
                x += h + random.Range(0.05f, 0.35f);
            }
            Kit.Block(b, new Vector3(length * 0.5f + 0.5f, 0.35f, random.Range(-0.4f, 0.4f)), new Vector3(0.6f, 0.3f, 0.6f), random.Range(-0.6f, 0.6f), 0.05f, Stone(spec, 1, random, 0.4f));
        }

        /// <summary>
        /// An arch on two piers across x, its voussoirs round a half circle and its keystone at its crown, the opening along z; or half of one, its stones gone from one side down to a ragged end.
        /// Numbers: span, shade, warmth.
        /// </summary>
        public static void Arch(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float span = Math.Clamp(spec.Metres(0), 2f, 7f);
            float r = span * 0.5f, ring = 0.7f, depth = 0.9f;
            float spring = random.Range(2.2f, 3f);
            bool broken = random.Chance(0.35f);
            // The piers, in courses.
            foreach (float s in new[] { -1f, 1f })
            {
                float x0 = s < 0f ? -r - ring : r, x1 = s < 0f ? -r : r + ring;
                for (float y = -0.3f; y < spring - 0.01f; y += 0.55f)
                {
                    float y1 = MathF.Min(y + 0.54f, spring);
                    Masonry.Box(b, new Vector3(x0, y, -depth * 0.5f), new Vector3(x1, y1, depth * 0.5f), 0.05f, Stone(spec, 1, random, y));
                }
            }
            int stones = Math.Max(7, (int)(MathF.PI * r / 0.55f)) | 1;
            int keep = broken ? random.Int(stones / 3, stones - 2) : stones;
            bool fromLeft = random.Chance(0.5f);
            for (int k = 0; k < stones; k++)
            {
                int index = fromLeft ? k : stones - 1 - k;
                if (k >= keep) break;
                float a0 = MathF.PI * index / stones, a1 = MathF.PI * (index + 1) / stones;
                Vector2 In(float a) => new Vector2(MathF.Cos(a) * r, spring + MathF.Sin(a) * r);
                Vector2 Out(float a) => new Vector2(MathF.Cos(a) * (r + ring), spring + MathF.Sin(a) * (r + ring));
                bool key = index == stones / 2;
                float lift = key ? 0.1f : 0f;
                Masonry.Prism(b, In(a1), In(a0), Out(a0) + new Vector2(0f, lift), Out(a1) + new Vector2(0f, lift), -depth * 0.5f - (key ? 0.05f : 0f), depth * 0.5f + (key ? 0.05f : 0f), 0.04f, Stone(spec, 1, random, spring + 1f));
            }
            // What fell from it, at its foot.
            if (broken) for (int k = 0; k < 3; k++) Kit.Block(b, new Vector3(random.Range(-r, r), 0.25f, random.Range(0.6f, 2f) * (random.Chance(0.5f) ? 1f : -1f)), new Vector3(0.35f, 0.25f, 0.4f), random.Float() * MathF.Tau, 0.05f, Stone(spec, 1, random, 0.3f));
        }

        /// <summary>A flight of steps along z up to a landing that ends broken off, its treads of a few stones each, worn and some shifted. Numbers: width, rise, shade, warmth.</summary>
        public static void Steps(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float width = Math.Clamp(spec.Metres(0), 2f, 9f), rise = Math.Clamp(spec.Metres(1), 0.6f, 3f);
            int steps = Math.Clamp((int)(rise / 0.22f), 3, 12);
            float step = rise / steps;
            const float Tread = 0.42f;
            for (int k = 0; k < steps; k++)
            {
                float z0 = steps * Tread * 0.5f - k * Tread, y1 = (k + 1) * step;
                int stones = Math.Max(2, (int)(width / 1.4f));
                for (int s = 0; s < stones; s++)
                {
                    if (k > steps / 2 && random.Chance(0.12f)) continue;
                    float x0 = -width * 0.5f + s * width / stones, x1 = x0 + width / stones - 0.015f;
                    float shift = random.Range(-0.04f, 0.04f);
                    Masonry.Box(b, new Vector3(x0, -0.3f, z0 - Tread - 0.4f + shift), new Vector3(x1, y1 - random.Range(0f, 0.03f), z0 + shift), 0.035f, Stone(spec, 2, random, y1));
                }
            }
            // The landing at its head, broken off.
            float back = steps * Tread * 0.5f - steps * Tread;
            for (float x = -width * 0.5f; x < width * 0.5f - 0.2f; x += 1.2f)
            {
                float reach = random.Range(0.6f, 2.2f);
                Masonry.Box(b, new Vector3(x, -0.3f, back - reach), new Vector3(MathF.Min(x + 1.18f, width * 0.5f), rise, back + 0.01f), 0.04f, Stone(spec, 2, random, rise));
            }
        }

        /// <summary>A great block fallen on its side, half sunk, carved along its face with the waygates' runes in their dark inlay, which nobody living can read.</summary>
        public static void RuneStone(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            Vector3 half = new Vector3(random.Range(0.9f, 1.3f), random.Range(0.4f, 0.55f), random.Range(0.55f, 0.75f));
            float tilt = random.Range(-0.12f, 0.12f);
            Vector3 centre = new Vector3(0f, half.Y - 0.25f, 0f);
            Kit.Block(b, centre, half, 0f, 0.06f, Stone(spec, 0, random, 0.6f));
            // The runes: strokes between the points of a small grid, a few to a glyph, in a row along its top.
            float top = centre.Y + half.Y + WaygateDesign.InlayLift;
            int glyphs = (int)(half.X * 2f / 0.42f);
            for (int g = 0; g < glyphs; g++)
            {
                float gx = -half.X + 0.25f + g * 0.42f;
                int strokes = random.Int(2, 4);
                for (int s = 0; s < strokes; s++)
                {
                    Vector2 a = new Vector2(gx + random.Int(0, 2) * 0.12f, -0.18f + random.Int(0, 3) * 0.12f);
                    Vector2 c = new Vector2(gx + random.Int(0, 2) * 0.12f, -0.18f + random.Int(0, 3) * 0.12f);
                    if ((c - a).LengthSquared < 0.01f) continue;
                    Vector2 side = new Vector2(-(c - a).Y, (c - a).X).Normalized() * 0.018f;
                    Kit.Face(b, new Vector3(a.X - side.X, top + tilt * a.X * 0f, a.Y - side.Y), new Vector3(c.X - side.X, top, c.Y - side.Y), new Vector3(c.X + side.X, top, c.Y + side.Y), new Vector3(a.X + side.X, top, a.Y + side.Y), Vector3.UnitY, Flat(new Vector3(0.05f, 0.05f, 0.05f)));
                }
            }
        }

        /// <summary>What's left of a figure: its great head lying on its side, brow and nose and lips worn soft; or its feet on their plinth, broken off at the ankle. Numbers: form, shade, warmth.</summary>
        public static void Statue(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            if (spec.Int(0) == 0)
            {
                // The head, on its side along x, its face towards +z.
                float size = random.Range(1.6f, 2.2f);
                VertexMaterial stone = Stone(spec, 1, random, 0.8f);
                Kit.Lathe(b, new Vector3(-size * 0.55f, size * 0.42f, 0f), Vector3.UnitX, size * 1.15f, Kit.Profile((0f, size * 0.12f), (0.15f, size * 0.36f), (0.5f, size * 0.46f), (0.82f, size * 0.42f), (1f, size * 0.18f)), 14, 6, stone);
                // Brow, nose and lips along its face, lying sideways: what's up for a standing head runs along x here.
                Kit.Block(b, new Vector3(size * 0.08f, size * 0.42f, size * 0.42f), new Vector3(size * 0.06f, size * 0.32f, size * 0.07f), 0f, 0.05f, stone);
                Kit.Block(b, new Vector3(-size * 0.12f, size * 0.42f, size * 0.47f), new Vector3(size * 0.14f, size * 0.07f, size * 0.07f), 0f, 0.04f, stone);
                Kit.Block(b, new Vector3(-size * 0.32f, size * 0.42f, size * 0.42f), new Vector3(size * 0.04f, size * 0.14f, size * 0.05f), 0f, 0.03f, stone);
                Kit.Heap(b, new Vector3(0f, Ground - 0.1f, -size * 0.2f), new Vector2(size * 0.9f, size * 0.55f), 0.15f, 0f, Soil(0.85f), random, 2, 10);
            }
            else
            {
                // The plinth, and the feet on it, broken off at the ankle.
                Masonry.Box(b, new Vector3(-1.2f, -0.3f, -0.8f), new Vector3(1.2f, 0.7f, 0.8f), 0.05f, Stone(spec, 1, random, 0.4f));
                foreach (float s in new[] { -0.42f, 0.42f })
                {
                    float turn = s * 0.15f;
                    VertexMaterial stone = Stone(spec, 1, random, 1f);
                    Kit.Block(b, new Vector3(s, 0.85f, 0.1f), new Vector3(0.24f, 0.16f, 0.55f), turn, 0.08f, stone);
                    Kit.Lathe(b, new Vector3(s, 0.85f, -0.15f), Vector3.UnitY, random.Range(0.4f, 0.9f), Kit.Profile((0f, 0.22f), (1f, 0.2f)), 10, 2, stone);
                }
            }
        }
    }
}

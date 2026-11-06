using Dino_Engine.Modelling.Model;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>
    /// Swords and daggers: the same four parts at different sizes. Built standing up, with the middle of the grip at the origin, the blade up +y, its width across x and its thickness along z.
    /// Nothing here is a shape chosen from a list. Each part is curves through drawn points, and which parts there are follows from <see cref="GearStyle"/>.
    /// </summary>
    public static class BladeGenerator
    {
        private readonly record struct Metrics(float ShortBlade, float LongBlade, float Narrow, float Broad, float Grip);

        private static readonly Metrics SwordMetrics = new(1.25f, 2.65f, 0.085f, 0.235f, 0.52f);
        private static readonly Metrics DaggerMetrics = new(0.62f, 1.2f, 0.05f, 0.16f, 0.4f);

        public static void Build(GearMesh mesh, GearStyle style)
        {
            Metrics metrics = style.Spec.Kind == GearKind.Dagger ? DaggerMetrics : SwordMetrics;
            Genes genes = style.Genes;
            GearForm form = style.Spec.Form;

            float length = genes.Around("length", metrics.ShortBlade + (metrics.LongBlade - metrics.ShortBlade) * form.Size, (metrics.LongBlade - metrics.ShortBlade) * 0.09f);
            float width = genes.Around("width", metrics.Narrow + (metrics.Broad - metrics.Narrow) * (1f - form.Slender), (metrics.Broad - metrics.Narrow) * 0.13f) * (0.8f + 0.4f * style.Heft);
            float grip = metrics.Grip * genes.Range("grip.length", 0.85f, 1.3f) * (0.82f + 0.36f * form.Size);

            float gripTop = grip * 0.5f;
            float guardHeight = Guard(mesh, style, genes.Part("guard"), width, gripTop, grip);
            Blade(mesh, style, genes.Part("blade"), length, width, gripTop + guardHeight * 0.6f);
            Grip(mesh, style, genes.Part("grip"), grip);
            Pommel(mesh, style, genes.Part("pommel"), width, -grip * 0.5f);
        }

        /// <summary>A blade from <paramref name="baseY"/> up, on whatever it's on. It's also what a spear's point is.</summary>
        internal static void Blade(GearMesh mesh, GearStyle style, Genes genes, float length, float width, float baseY)
        {
            GearForm form = style.Spec.Form;
            GearPalette palette = style.Palette;

            // Its outline: a width through a few drawn points, then a point of its own.
            float tipLength = genes.Range("tip.length", 0.07f, 0.34f);
            float tipStart = 1f - tipLength;
            float tipPower = genes.Range("tip.power", 0.7f, 2.6f);
            float belly = genes.Range("belly.at", 0.08f, 0.7f);
            Profile outline = new Profile(
                (0f, genes.Range("width.base", 0.62f, 1f)),
                (belly * tipStart, 1f),
                ((belly + (1f - belly) * 0.55f) * tipStart, genes.Range("width.mid", 0.55f, 1.08f)),
                (tipStart, genes.Range("width.shoulder", 0.42f, 1.04f)));

            // Which way it bends: a curved thing is often single-edged, with all of the width on one side of its back.
            float curve = form.Curve;
            float bend = curve * genes.Range("bend", 0.05f, 0.24f) * length * genes.Sign("bend.way");
            float sway = genes.Around("sway", 0f, 0.012f + 0.02f * curve) * length;
            float bias = Math.Clamp(genes.Around("edge.bias", curve * 0.8f, 0.22f), 0f, 1f);
            bool wavy = genes.Chance("wave", curve * curve * 0.8f);
            float waveHeight = wavy ? width * genes.Range("wave.height", 0.3f, 0.7f) : 0f;
            float waves = genes.Range("wave.count", 2.5f, 6.5f);

            // What's been done to the edge, on purpose or otherwise.
            int teeth = genes.Chance("teeth", style.Spikes * 0.45f) ? genes.Count("teeth.count", 3, 11) : 0;
            float teethFrom = genes.Range("teeth.from", 0f, 0.35f);
            float teethTo = teethFrom + genes.Range("teeth.reach", 0.18f, 0.5f);
            float teethDepth = genes.Range("teeth.depth", 0.1f, 0.3f);
            int notches = (int)(style.Wear * genes.Range("notches", 0f, 7f));
            var bites = new (float at, float depth, float span)[notches];
            for (int i = 0; i < notches; i++) bites[i] = (genes.Unit($"notch{i}.at"), genes.Range($"notch{i}.depth", 0.08f, 0.32f), genes.Range($"notch{i}.span", 0.012f, 0.03f));

            float WidthAt(float t)
            {
                float w = t < tipStart ? outline.At(t) : outline.At(tipStart) * (1f - MathF.Pow((t - tipStart) / tipLength, tipPower));
                if (teeth > 0 && t > teethFrom && t < teethTo)
                {
                    float phase = (t - teethFrom) / (teethTo - teethFrom) * teeth;
                    w -= teethDepth * (phase - MathF.Floor(phase));
                }
                foreach ((float at, float depth, float span) in bites)
                {
                    float d = (t - at) / span;
                    w -= depth * MathF.Exp(-d * d);
                }
                return MathF.Max(w, 0.03f) * width;
            }

            // Across it: a flat, bevels to the edge, and maybe a groove that runs out partway up.
            float thickness = width * genes.Range("thickness", 0.13f, 0.27f) * (0.8f + 0.5f * style.Heft);
            float flat = genes.Range("flat", 0.02f, 0.72f);
            bool fuller = genes.Chance("fuller", 0.62f);
            float fullerWidth = genes.Range("fuller.width", 0.3f, 0.78f);
            float fullerDepth = fuller ? genes.Range("fuller.depth", 0.25f, 0.7f) : 0f;
            float fullerEnd = genes.Range("fuller.end", 0.35f, 0.92f);

            int steps = Math.Clamp((int)(length * 16f) + teeth * 3 + notches * 3, 18, 72);
            var rings = new Ring[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                float w = WidthAt(t);

                // The back stays where it is, and the belly goes out from it.
                float centre = bias * (w - width * 0.8f) * 0.9f + bend * t * t + sway * MathF.Sin(t * MathF.PI) + waveHeight * MathF.Sin(t * waves * MathF.Tau) * (1f - t * t);
                float groove = fullerDepth * Math.Clamp((fullerEnd - t) / 0.08f, 0f, 1f);
                rings[i] = new Ring
                {
                    Centre = new Vector3(centre, baseY + t * length, 0f),
                    Side = Vector3.UnitX,
                    Front = Vector3.UnitZ,
                    HalfWidth = w,
                    HalfDepth = MathF.Min(thickness * MathF.Max(1f - t * 0.7f, 0.1f), w * 0.7f),
                    Shape = Section.BladeOutline(flat, fullerWidth, groove),
                };
            }

            float rust = style.Wear * style.Wear * genes.Range("rust", 0.3f, 1f);
            VertexMaterial spotted = new VertexMaterial(GearMaterials.Metal, new Colour(0.2f, 0.11f, 0.06f));
            mesh.Loft(Section.Blade(), rings, (ring, point) =>
            {
                VertexMaterial material = Section.IsBladeEdge(point) ? palette.Edge : Section.IsBladeGroove(point) && fullerDepth > 0f ? palette.Dark : palette.Metal;
                return rust > 0.05f && Noise(ring, point, genes) < rust ? Mixed(material, spotted, 0.7f) : material;
            }, capStart: true, capEnd: false);

            if (style.Shines("runes") && fuller) Runes(mesh, style, genes.Part("runes"), rings, fullerEnd, thickness);
        }

        // Marks set into the groove, lit from inside, on both faces.
        private static void Runes(GearMesh mesh, GearStyle style, Genes genes, Ring[] rings, float end, float thickness)
        {
            int count = genes.Count("count", 3, 9);
            float from = genes.Range("from", 0.04f, 0.14f);
            float to = end * genes.Range("to", 0.6f, 0.95f);
            float size = thickness * genes.Range("size", 0.7f, 1.25f);
            int sides = genes.Count("sides", 3, 6);
            for (int i = 0; i < count; i++)
            {
                Vector3 at = rings[(int)((from + (to - from) * (i + 0.5f) / count) * (rings.Length - 1))].Centre;
                float scale = size * (i % 2 == 0 ? 1f : genes.Range("alternate", 0.6f, 1f));
                foreach (float face in new[] { -1f, 1f })
                {
                    float z = thickness * face * 0.72f;
                    mesh.At(Matrix4.CreateRotationX(MathF.PI / 2f) * Matrix4.CreateTranslation(at.X, at.Y, z), () =>
                    {
                        mesh.Lathe(Section.Polygon(sides, genes.Range("turn", 0f, 1f)), thickness * 0.12f, 1, _ => scale, style.Palette.Glow);
                    });
                }
            }
        }

        // A guard sweeps out from the middle, and the sides are almost always each other's mirror. What comes back is how much height it takes up.
        private static float Guard(GearMesh mesh, GearStyle style, Genes genes, float width, float y, float grip)
        {
            GearPalette palette = style.Palette;
            float cover = style.Spec.Form.Cover;

            float span = width * genes.Around("span", 1.3f + 2.2f * cover, 0.4f) * (0.8f + 0.4f * style.Flare);
            float thick = genes.Range("thick", 0.028f, 0.05f) * (0.7f + 0.9f * style.Heft) * MathF.Sqrt(Math.Max(width / 0.15f, 0.4f));
            float deep = thick * genes.Range("deep", 0.8f, 1.7f);
            float droop = span * genes.Around("droop", 0.05f, 0.24f);
            float curl = span * genes.Around("curl", 0f, 0.12f);
            float swell = genes.Range("swell", 0.55f, 1.6f) * (0.7f + 0.6f * style.Flare);
            bool pointed = genes.Chance("pointed", style.Spikes);
            Section bar = style.Angular > 0.5f ? Section.Slab(genes.Range("bevel", 0.1f, 0.45f)) : Section.Super(10, genes.Range("round", 1.8f, 3f));

            Vector3 Point(float s, float side)
            {
                float rise = droop * s * s + genes.Around("kink", 0f, 0.012f) * MathF.Sin(s * MathF.PI);
                return new Vector3(s * span * side, y + rise, curl * s * s * side);
            }

            void Arm(float side)
            {
                var keys = new[] { Point(0f, side), Point(0.33f, side), Point(0.66f, side), Point(1f, side) };
                List<Vector3> spine = Spine.Through(keys, 10);
                mesh.Sweep(bar, spine, t =>
                {
                    float end = pointed ? MathF.Pow(1f - t, 0.75f) : 1f + (swell - 1f) * t * t;
                    return new Vector2(deep * end, thick * end);
                }, palette.Fitting, Vector3.UnitY, capStart: false, capEnd: !pointed);

                if (pointed || !genes.Chance("finial", 0.5f + style.Ornate * 0.4f)) return;
                float r = thick * swell * genes.Range("finial.size", 1.2f, 2.1f);
                Vector3 tip = Point(1f, side);
                mesh.At(Matrix4.CreateTranslation(tip - Vector3.UnitY * r), () => Knob(mesh, style, genes.Part("finial"), r * 2f, r, palette.Fitting));
            }

            Arm(1f);
            if (!genes.Chance("one.sided", 0.06f)) Arm(-1f);

            // The block the two arms come out of, wrapped round where the blade goes in.
            float blockHeight = thick * genes.Range("block.height", 1.6f, 4f);
            float blockWidth = MathF.Max(width * genes.Range("block.width", 0.9f, 1.3f), thick * 1.6f);
            mesh.At(Matrix4.CreateTranslation(0f, y - blockHeight * 0.5f, 0f), () =>
            {
                mesh.Loft(style.Angular > 0.5f ? Section.Slab(genes.Range("block.bevel", 0.15f, 0.5f)) : Section.Super(12, genes.Range("block.round", 2.4f, 4.5f)), new[]
                {
                    Ring.Flat(0f, blockWidth * genes.Range("block.foot", 0.65f, 1f), deep * 1.1f, true),
                    Ring.Flat(blockHeight, blockWidth, deep * 1.25f, true),
                }, palette.Fitting);
            });

            // Tongues running up over the blade.
            if (genes.Chance("langets", 0.2f + style.Spikes * 0.5f))
            {
                float reach = width * genes.Range("langet.reach", 0.8f, 2.4f);
                float half = width * genes.Range("langet.width", 0.3f, 0.6f);
                foreach (float face in new[] { -1f, 1f })
                {
                    mesh.At(Matrix4.CreateTranslation(0f, y + blockHeight * 0.4f, face * width * 0.17f), () =>
                    {
                        mesh.Loft(Section.Slab(0.3f), new[] { Ring.Flat(0f, half, width * 0.05f, true), Ring.Flat(reach * 0.6f, half * 0.75f, width * 0.05f), Ring.Flat(reach, half * 0.08f, width * 0.03f, true) }, palette.Fitting);
                    });
                }
            }

            // A bow round the knuckles, down to the pommel.
            if (genes.Chance("knuckle.bow", MathF.Max(cover - 0.55f, 0f) * 1.8f))
            {
                float side = genes.Sign("knuckle.side");
                float out_ = genes.Range("knuckle.out", 0.1f, 0.2f) + width * 0.3f;
                var keys = new[]
                {
                    new Vector3(0f, y, 0f), new Vector3(out_ * 0.8f * side, y - grip * 0.1f, 0f), new Vector3(out_ * side, y - grip * 0.55f, 0f), new Vector3(out_ * 0.6f * side, y - grip * 0.92f, 0f), new Vector3(0f, y - grip * 1.04f, 0f),
                };
                mesh.Sweep(bar, Spine.Through(keys, 12), _ => new Vector2(deep, thick) * 0.62f, palette.Fitting, Vector3.UnitZ);
            }

            return blockHeight;
        }

        // Something to hold: a swelling shape wrapped in whatever it's wrapped in.
        private static void Grip(GearMesh mesh, GearStyle style, Genes genes, float length)
        {
            GearPalette palette = style.Palette;
            float radius = genes.Range("radius", 0.05f, 0.076f) * (0.82f + 0.4f * style.Heft);
            Profile shape = new Profile((0f, genes.Range("foot", 0.8f, 1f)), (genes.Range("swell.at", 0.3f, 0.7f), genes.Range("swell", 0.98f, 1.22f)), (1f, genes.Range("head", 0.75f, 1f)));

            int wraps = genes.Count("wraps", 3, 11);
            float ridge = genes.Range("ridge", 0.02f, 0.13f);
            int kind = genes.Pick("wrapped", 4f, 2f, 1f + style.Ornate * 2f);
            VertexMaterial first = kind == 1 ? palette.Wood : palette.Leather;
            VertexMaterial second = kind == 2 ? palette.Fitting : kind == 1 ? palette.Dark : palette.Binding;

            int steps = wraps * 4;
            var rings = new Ring[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                float r = radius * shape.At(t) * (1f + ridge * MathF.Abs(MathF.Sin(t * wraps * MathF.PI)));
                rings[i] = Ring.Flat(-length * 0.5f + t * length, r, r * 0.86f);
            }
            mesh.Loft(Section.Round(10), rings, (ring, _) => ring % 4 == 0 || ring % 4 == 3 ? second : first, capStart: false, capEnd: false);
        }

        private static void Pommel(GearMesh mesh, GearStyle style, Genes genes, float width, float y)
        {
            float size = genes.Range("size", 0.09f, 0.16f) * (0.72f + 0.6f * style.Heft) * MathF.Sqrt(Math.Max(width / 0.15f, 0.5f));
            float height = size * genes.Range("height", 1.1f, 2.6f);
            mesh.At(Matrix4.CreateTranslation(0f, y - height, 0f), () =>
            {
                Knob(mesh, style, genes, height, size, style.Palette.Fitting);

                if (genes.Chance("spike", style.Spikes * 0.7f))
                {
                    float reach = size * genes.Range("spike.reach", 0.7f, 2f);
                    mesh.At(Matrix4.CreateTranslation(0f, -reach, 0f), () => mesh.Lathe(Section.Polygon(genes.Count("spike.sides", 3, 6)), reach * 1.1f, 2, t => size * 0.42f * t, style.Palette.Fitting, capEnd: false));
                }
            });

            if (!style.Shines("stone")) return;
            float stone = size * genes.Range("stone.size", 0.32f, 0.5f);
            foreach (float face in new[] { -1f, 1f })
            {
                mesh.At(Matrix4.CreateRotationX(face * MathF.PI / 2f) * Matrix4.CreateTranslation(0f, y - height * 0.5f, face * size * 0.86f), () =>
                {
                    mesh.Gem(genes.Count("stone.sides", 5, 8), stone, stone * 0.5f, stone * 0.2f, 0.55f, style.Palette.Stone);
                });
            }
        }

        /// <summary>A turned lump of metal: a pommel, a finial, the end of anything. Its outline is drawn, so it's as likely to be a wheel as a pear or an onion.</summary>
        public static void Knob(GearMesh mesh, GearStyle style, Genes genes, float height, float radius, VertexMaterial material)
        {
            float waist = genes.Range("knob.waist", 0.18f, 0.72f);
            Profile outline = new Profile(
                (0f, genes.Range("knob.foot", 0.1f, 0.5f)),
                (genes.Range("knob.belly.at", 0.25f, 0.72f), 1f),
                (0.9f, genes.Range("knob.shoulder", 0.3f, 0.8f)),
                (1f, waist));

            bool cut = style.Angular > 0.45f && genes.Chance("knob.cut", 0.65f);
            Section section = cut ? Section.Polygon(genes.Count("knob.sides", 4, 10)) : Section.Round(12);
            int steps = cut ? genes.Count("knob.steps", 3, 5) : 8;
            mesh.Lathe(section, height, steps, t => radius * outline.At(t), material, squash: genes.Range("knob.squash", 0.55f, 1f));
        }

        private static VertexMaterial Mixed(in VertexMaterial material, in VertexMaterial other, float share)
        {
            return new VertexMaterial(material.materialTextureIndex, Colour.mix(material.colour, other.colour, share));
        }

        // Fixed for a place on a thing, so a patch of rust is the same patch wherever this is drawn.
        private static float Noise(int ring, int point, Genes genes)
        {
            return genes.Part(ring / 3).Unit($"patch{point / 3}");
        }
    }
}

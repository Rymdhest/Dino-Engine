using Dino_Engine.Modelling.Model;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>
    /// What's in the other hand. A shield is a width drawn down its height and a curve across it, so a buckler, a heater and a tower are places in one space, and its face is painted by
    /// the same few rules whatever shape it is. A caster's ward isn't a shield at all: see <see cref="Ward"/>. Built facing +z, with the hand at the origin.
    /// </summary>
    public static class ShieldGenerator
    {
        public static void Build(GearMesh mesh, GearStyle style)
        {
            if (style.Spec.Cloth > 0.5f) Ward(mesh, style);
            else Shield(mesh, style);
        }

        private static void Shield(GearMesh mesh, GearStyle style)
        {
            Genes genes = style.Genes;
            GearForm form = style.Spec.Form;
            GearPalette palette = style.Palette;

            // How tall and how wide, and what the outline does on the way down.
            float wide = genes.Around("wide", 0.62f + form.Size * 0.42f + (1f - form.Slender) * 0.14f, 0.05f);
            float tall = wide * 2f * genes.Around("tall", 0.95f + form.Size * 0.75f, 0.09f);

            // The outline is a widest point and two falls from it. A fall of 1/2 is a circle's edge, nearer nothing is a flat end, and above 1 it runs in to a point.
            float belly = genes.Around("belly", 0.6f, 0.1f);
            float fallBelow = MathF.Pow(2f, genes.Around("fall.below", -0.4f + form.Size * 0.9f, 0.6f));
            float fallAbove = MathF.Pow(2f, genes.Around("fall.above", -1.2f - form.Size * 1.6f, 0.7f));
            float WidthAt(float t)
            {
                float s = t < belly ? MathF.Sin(t / belly * MathF.PI * 0.5f) : MathF.Cos((t - belly) / (1f - belly) * MathF.PI * 0.5f);
                return wide * MathF.Pow(MathF.Max(s, 0f), t < belly ? fallBelow : fallAbove);
            }

            float bow = form.Curve * genes.Range("bow", 0.1f, 0.42f) * wide;
            float thick = genes.Range("thick", 0.03f, 0.055f) * (0.8f + 0.6f * style.Heft);

            // What the face is made of, and how it's divided.
            float metal = style.Spec.Heavy;
            bool hide = style.Spec.Leather > style.Spec.Heavy;
            VertexMaterial ground = hide ? palette.Leather : metal > 0.5f && genes.Chance("all.metal", 0.6f) ? palette.Metal : palette.Wood;
            VertexMaterial paint = hide ? palette.Binding : palette.Cloth;
            VertexMaterial emblem = hide ? palette.Fitting : palette.Trim;
            int pattern = genes.Pick("pattern", 5f, 2f, 2f, 2f, 1f + style.Ornate * 3f);
            int bands = genes.Count("bands", 2, 5);
            float pivot = genes.Range("pattern.at", 0.36f, 0.64f);

            const int across = 16;
            int steps = 26;
            var shape = new Vector2[steps + 1][];
            var rings = new Ring[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                // Closer together at the ends, where an outline does most of its turning.
                float t = StepAt(i, steps);
                float w = MathF.Max(WidthAt(t), 0.02f);
                var section = new Vector2[across * 2];
                for (int j = 0; j < across; j++)
                {
                    // Across the front one way and across the back the other, bowed together.
                    float x = 1f - 2f * j / (across - 1f);
                    float z = (1f - x * x) * bow / MathF.Max(thick, 1e-3f);
                    section[j] = new Vector2(x, z + 1f);
                    section[across * 2 - 1 - j] = new Vector2(x, z - 1f);
                }
                shape[i] = section;
                rings[i] = new Ring { Centre = new Vector3(0f, -tall * 0.5f + t * tall, 0f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = w, HalfDepth = thick, Shape = section };
            }

            VertexMaterial Face(int ring, int point)
            {
                float t = StepAt(ring, steps);
                float x = point < across ? 1f - 2f * point / (across - 1f) : 1f - 2f * (across * 2 - 1 - point) / (across - 1f);
                return pattern switch
                {
                    1 => (t > pivot) == (x > 0f) ? paint : ground,                                          // Quartered.
                    2 => (int)(t * bands) % 2 == 0 ? paint : ground,                                        // Bars.
                    3 => (int)((x * 0.5f + 0.5f) * (bands * 2 + 1)) % 2 == 0 ? paint : ground,              // Pales.
                    4 => MathF.Abs(x) < 0.22f || MathF.Abs(t - pivot) < 0.07f ? emblem : paint,             // A cross.
                    _ => ground,
                };
            }

            mesh.Loft(new Section(shape[0], new bool[across * 2]) { ClosesAcross = true }, rings, Face);

            // Round the edge, as a shield's rim is what takes the blows and what draws it from up where the camera is.
            float rim = genes.Range("rim", 0.03f, 0.075f) * (0.8f + 0.5f * style.Heft);
            VertexMaterial edge = hide ? palette.Binding : palette.Fitting;
            var loop = new List<Vector3>(steps * 2 + 1);
            for (int i = 0; i <= steps; i++) loop.Add(rings[i].Centre + Vector3.UnitX * MathF.Max(WidthAt(StepAt(i, steps)), 0.02f));
            for (int i = steps - 1; i >= 0; i--) loop.Add(rings[i].Centre - Vector3.UnitX * MathF.Max(WidthAt(StepAt(i, steps)), 0.02f));
            loop.Add(loop[0]);
            mesh.Sweep(Section.Round(6), loop, _ => new Vector2(rim), edge, Vector3.UnitZ, capStart: false, capEnd: false);

            // A boss in the middle, and studs round it.
            float boss = wide * genes.Range("boss", 0.16f, 0.32f);
            float bossUp = boss * genes.Range("boss.height", 0.5f, 1.1f);
            float front = bow + thick;
            bool spiked = genes.Chance("boss.spike", style.Spikes);
            mesh.At(Matrix4.CreateRotationX(MathF.PI / 2f) * Matrix4.CreateTranslation(0f, 0f, front), () =>
            {
                Section bossShape = style.Angular > 0.5f ? Section.Polygon(genes.Count("boss.sides", 5, 8)) : Section.Round(12);
                mesh.Lathe(bossShape, bossUp, 5, t => boss * (spiked ? 1f - t * 0.94f : MathF.Sqrt(MathF.Max(1f - t * t, 0.03f))), palette.Fitting, capStart: false);
            });

            int studs = genes.Count("studs", 0, (int)(4 + style.Ornate * 14f));
            float studSize = genes.Range("stud", 0.022f, 0.045f);
            float ringAt = genes.Range("stud.ring", 0.55f, 0.85f);
            for (int i = 0; i < studs; i++)
            {
                float angle = i * (MathF.Tau / MathF.Max(studs, 1));
                float y = MathF.Sin(angle) * tall * 0.5f * ringAt;
                float t = y / tall + 0.5f;
                float x = MathF.Cos(angle) * WidthAt(t) * ringAt;
                float z = (1f - MathF.Pow(x / wide, 2f)) * bow + thick;
                mesh.At(Matrix4.CreateRotationX(MathF.PI / 2f) * Matrix4.CreateTranslation(x, y, z), () => mesh.Lathe(Section.Round(6), studSize, 2, s => studSize * MathF.Sqrt(MathF.Max(1f - s * s, 0.03f)), palette.Fitting, capStart: false));
            }

            if (style.Shines("stone"))
            {
                float stone = boss * genes.Range("stone", 0.28f, 0.48f);
                mesh.At(Matrix4.CreateRotationX(MathF.PI / 2f) * Matrix4.CreateTranslation(0f, 0f, front + bossUp * 0.8f), () => mesh.Gem(genes.Count("stone.sides", 5, 9), stone, stone * 0.6f, stone * 0.3f, 0.5f, palette.Stone));
            }

            Grip(mesh, style, genes.Part("grip"), thick);
        }

        private static float StepAt(int step, int steps) => 0.5f - 0.5f * MathF.Cos((float)step / steps * MathF.PI);

        // Where the arm goes, on the back.
        private static void Grip(GearMesh mesh, GearStyle style, Genes genes, float thick)
        {
            float reach = genes.Range("reach", 0.24f, 0.34f);
            var keys = new[] { new Vector3(-reach, 0f, -thick), new Vector3(-reach * 0.5f, 0f, -thick - 0.08f), new Vector3(reach * 0.5f, 0f, -thick - 0.08f), new Vector3(reach, 0f, -thick) };
            mesh.Sweep(Section.Slab(0.3f), Spine.Through(keys, 8), _ => new Vector2(0.05f, 0.022f), style.Palette.Binding, Vector3.UnitZ);
        }

        // A ward is held up and doesn't stop anything by being in the way: a cage, a stone, things turning around it. What it's made of leans on what a caster would carry.
        private static void Ward(GearMesh mesh, GearStyle style)
        {
            Genes genes = style.Genes;
            GearForm form = style.Spec.Form;
            GearPalette palette = style.Palette;

            float size = genes.Around("size", 0.32f + form.Size * 0.46f, 0.04f);
            float stem = genes.Range("stem", 0.22f, 0.48f);
            float y = stem + size * genes.Range("above", 0.75f, 1.1f);
            Vector3 heart = new Vector3(0f, y, 0f);

            // What it's held by.
            mesh.At(Matrix4.CreateTranslation(0f, -0.26f, 0f), () => mesh.Lathe(Section.Round(8), stem + 0.26f, 6, t => 0.05f * (1.15f - 0.5f * t), palette.Binding));
            mesh.At(Matrix4.CreateTranslation(0f, -0.34f, 0f), () => BladeGenerator.Knob(mesh, style, genes.Part("foot"), 0.12f, 0.09f, palette.Fitting));

            // Bands round it, each tipped its own way, so that it looks like something held in place by more than metal.
            int bands = genes.Count("bands", 1, 2 + (int)(form.Fancy * 3f));
            VertexMaterial band = genes.Chance("wooden", 0.25f + (1f - form.Fancy) * 0.4f) ? palette.Wood : palette.Fitting;
            for (int i = 0; i < bands; i++)
            {
                float radius = size * genes.Range($"band{i}.size", 0.82f, 1.2f);
                float flat = genes.Range($"band{i}.width", 0.02f, 0.06f);
                Matrix4 turn = Matrix4.CreateRotationX(genes.Range($"band{i}.x", -1.4f, 1.4f)) * Matrix4.CreateRotationZ(genes.Range($"band{i}.z", -1.4f, 1.4f)) * Matrix4.CreateTranslation(heart);
                mesh.At(turn, () => mesh.Hoop(style.Angular > 0.5f ? Section.Slab(0.3f) : Section.Round(6), radius, flat, flat * 0.6f, 24, band));
            }

            // Prongs up from the stem, as far as the Middle of it.
            int prongs = genes.Count("prongs", 0, 5);
            float spread = size * genes.Range("spread", 0.7f, 1.15f);
            for (int i = 0; i < prongs; i++)
            {
                float angle = i * (MathF.Tau / MathF.Max(prongs, 1));
                Vector3 outwards = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                var keys = new[] { new Vector3(0f, stem, 0f) + outwards * 0.05f, new Vector3(0f, y - size * 0.45f, 0f) + outwards * spread, new Vector3(0f, y + size * 0.5f, 0f) + outwards * spread * 0.45f };
                mesh.Sweep(Section.Round(6), Spine.Through(keys, 9), t => new Vector2(0.03f * (1f - t * 0.8f)), band, Vector3.UnitZ);
            }

            float stone = size * genes.Range("stone", 0.42f, 0.66f);
            bool lit = style.Shines("lit") || style.Spec.Theme != GearTheme.None;
            mesh.At(Matrix4.CreateTranslation(heart), () => mesh.Gem(genes.Count("facets", 5, 10), stone, stone * genes.Range("crown", 0.7f, 1.3f), stone * genes.Range("pavilion", 0.7f, 1.4f), genes.Range("table", 0.2f, 0.6f), lit ? palette.Glow : palette.Stone));
        }
    }
}

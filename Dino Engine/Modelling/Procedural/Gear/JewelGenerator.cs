using Dino_Engine.Modelling.Model;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>Rings and amulets. Life size, as they are worn, and drawn bigger than that on the ground. Both are the same few things: a band, a setting, and something set in it.</summary>
    public static class JewelGenerator
    {
        private const float FingerRadius = 0.065f;

        /// <summary>A ring lying flat around y, with its setting out along +x.</summary>
        public static void Ring(GearMesh mesh, GearStyle style)
        {
            Genes genes = style.Genes;
            GearPalette palette = style.Palette;

            float radius = FingerRadius * genes.Range("radius", 1f, 1.14f);
            float wide = FingerRadius * genes.Range("wide", 0.18f, 0.5f) * (0.8f + 0.6f * style.Heft);
            float thick = wide * genes.Range("thick", 0.3f, 0.7f);
            int bands = genes.Chance("twin", 0.22f) ? 2 : 1;
            float twist = genes.Chance("twisted", 0.3f) ? genes.Range("twist", 0.15f, 0.5f) : 0f;
            int waves = genes.Count("waves", 2, 7);
            Section section = style.Angular > 0.5f ? Section.Slab(genes.Range("bevel", 0.2f, 0.5f)) : Section.Super(8, genes.Range("round", 1.8f, 3.2f));

            for (int b = 0; b < bands; b++)
            {
                float offset = bands == 1 ? 0f : (b == 0 ? 1f : -1f) * wide * 0.7f;
                int steps = 40;
                var rings = new Ring[steps + 1];
                for (int i = 0; i <= steps; i++)
                {
                    float t = (float)i / steps;
                    float angle = t * MathF.Tau;
                    float swell = 1f + 0.7f * MathF.Pow(MathF.Max(MathF.Cos(angle), 0f), 3f);
                    float y = offset + wide * twist * MathF.Sin(angle * waves);
                    Vector3 outwards = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                    rings[i] = new Ring { Centre = outwards * radius + Vector3.UnitY * y, Side = outwards, Front = Vector3.UnitY, HalfWidth = thick * swell, HalfDepth = wide * swell * (bands == 2 ? 0.6f : 1f) };
                }
                mesh.Loft(section, rings, palette.Fitting, capStart: false, capEnd: false);
            }

            // The better a ring the likelier it holds something, and a plain one is just a band.
            if (style.Spec.Grade == 0 && !genes.Chance("plain.stone", 0.2f)) return;
            mesh.At(Matrix4.CreateRotationZ(-MathF.PI / 2f) * Matrix4.CreateTranslation(radius + thick, 0f, 0f), () => Setting(mesh, style, genes.Part("setting"), wide * genes.Range("stone", 1.05f, 1.9f)));
        }

        // A stone in a seat of its own, standing up y from where it's put.
        private static void Setting(GearMesh mesh, GearStyle style, Genes genes, float stone)
        {
            GearPalette palette = style.Palette;
            float height = stone * genes.Range("height", 0.3f, 0.75f);
            int claws = genes.Count("claws", 3, 8);
            int sides = genes.Count("sides", 4, 10);
            mesh.Lathe(genes.Chance("seat.round", 0.5f) ? Section.Round(10) : Section.Polygon(sides), height, 2, t => stone * (0.62f + 0.48f * t), palette.Fitting, capEnd: true);

            for (int i = 0; i < claws; i++)
            {
                float angle = i * (MathF.Tau / claws);
                Vector3 outwards = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                var keys = new[] { outwards * stone * 1.02f + Vector3.UnitY * height * 0.6f, outwards * stone * 1.12f + Vector3.UnitY * height * 1.5f, outwards * stone * 0.86f + Vector3.UnitY * height * 2.1f };
                mesh.Sweep(Section.Round(4), Spine.Through(keys, 4), t => new Vector2(stone * 0.13f * (1f - t * 0.6f)), palette.Fitting, Vector3.UnitZ);
            }

            VertexMaterial gem = style.Spec.Grade >= 3 || style.Spec.Theme != GearTheme.None && style.Shines("lit") ? palette.Glow : palette.Stone;
            mesh.At(Matrix4.CreateTranslation(0f, height * 1.25f, 0f), () => mesh.Gem(sides, stone, stone * genes.Range("crown", 0.4f, 0.85f), stone * genes.Range("pavilion", 0.2f, 0.5f), genes.Range("table", 0.35f, 0.7f), gem));
        }

        /// <summary>An amulet hanging from its chain, with its front towards +z, and the chain's loop rising to around a neck at <paramref name="neck"/> above it.</summary>
        public static void Amulet(GearMesh mesh, GearStyle style)
        {
            Genes genes = style.Genes;
            GearForm form = style.Spec.Form;
            GearPalette palette = style.Palette;

            float drop = genes.Range("drop", 0.34f, 0.62f);
            float neckWide = genes.Range("neck.wide", 0.26f, 0.34f);
            float chain = genes.Range("chain", 0.012f, 0.03f) * (0.8f + 0.7f * style.Heft);
            VertexMaterial cord = genes.Chance("cord", 0.3f + (1f - form.Fancy) * 0.3f) ? palette.Binding : palette.Fitting;
            int links = genes.Count("links", 12, 22);

            var keys = new List<Vector3>();
            for (int i = 0; i <= 16; i++)
            {
                float angle = i / 16f * MathF.Tau;
                float x = MathF.Sin(angle) * neckWide;
                float front = 0.5f + 0.5f * MathF.Cos(angle);
                float y = drop * (1f - MathF.Pow(front, 2.4f));
                keys.Add(new Vector3(x, y, MathF.Cos(angle) * neckWide * 0.72f - neckWide * 0.72f));
            }
            mesh.Sweep(Section.Round(5), Spine.Through(keys, links * 2), t => new Vector2(chain * (1f + 0.35f * MathF.Sin(t * links * MathF.Tau))), cord, Vector3.UnitY, false, false);

            float size = genes.Around("size", 0.08f + form.Size * 0.1f, 0.018f) * (0.9f + 0.4f * style.Heft);
            float outline = genes.Range("outline", 1.1f, 4f);
            int sides = genes.Count("sides", 3, 9);
            float tall = genes.Range("tall", 1f, 1.6f);
            float depth = size * genes.Range("depth", 0.16f, 0.32f);
            bool cut = style.Angular > 0.45f;

            // Its face, a plate cut to a drawn outline, with a stone in the middle of it.
            mesh.At(Matrix4.CreateRotationX(MathF.PI / 2f) * Matrix4.CreateTranslation(0f, -size * tall * 0.7f, 0f), () =>
            {
                mesh.Lathe(cut ? Section.Polygon(sides, genes.Range("turn", 0f, 1f)) : Section.Super(14, outline), depth, 2, t => size * (1f - 0.18f * t * t), palette.Fitting, squash: tall);
            });

            float loop = size * genes.Range("loop", 0.22f, 0.36f);
            mesh.At(Matrix4.CreateRotationZ(MathF.PI / 2f) * Matrix4.CreateTranslation(0f, -loop * 0.2f, 0f), () => mesh.Hoop(Section.Round(5), loop, chain * 0.8f, chain * 0.8f, 10, palette.Fitting));

            if (style.Spec.Grade == 0 && !genes.Chance("plain.stone", 0.3f)) return;
            mesh.At(Matrix4.CreateRotationX(MathF.PI / 2f) * Matrix4.CreateTranslation(0f, -size * tall * 0.7f, depth), () => Setting(mesh, style, genes.Part("setting"), size * genes.Range("stone", 0.42f, 0.66f)));
        }
    }
}

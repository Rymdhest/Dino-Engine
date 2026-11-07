using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>A book, shut, lying flat with its spine along -x. Boards, a block of pages between them, and whatever's been done to keep it shut.</summary>
    public static class BookGenerator
    {
        public static void Build(GearMesh mesh, GearStyle style)
        {
            Genes genes = style.Genes;
            GearPalette palette = style.Palette;

            float wide = genes.Range("wide", 0.5f, 0.66f);
            float tall = wide * genes.Range("tall", 1.2f, 1.5f);
            float thick = genes.Range("thick", 0.2f, 0.42f);
            float board = genes.Range("board", 0.028f, 0.05f);
            float inset = genes.Range("inset", 0.025f, 0.05f);
            Section flat = Section.Slab(genes.Range("bevel", 0.08f, 0.22f));

            // Boards, top and bottom, with the same leather running round the spine.
            Slab(mesh, flat, 0f, board, wide, tall, 0f, palette.Leather);
            Slab(mesh, flat, thick - board, board, wide, tall, 0f, palette.Leather);
            Slab(mesh, flat, board, thick - board * 2f, wide - inset * 2f, tall - inset, inset * 0.7f, palette.Bone);
            Slab(mesh, flat, 0f, thick, board * 1.5f, tall, -wide + board, palette.Binding);

            // Corners of something harder, for a book that's been looked after.
            if (genes.Chance("corners", 0.65f))
            {
                float corner = wide * genes.Range("corner", 0.12f, 0.2f);
                foreach (float side in new[] { -1f, 1f })
                {
                    Slab(mesh, flat, thick - board * 0.4f, board * 0.7f, corner, corner, wide - corner * 0.8f, palette.Fitting, side * (tall - corner * 0.8f));
                }
            }

            // Something on its face, which is what tells one from another.
            float face = wide * genes.Range("face", 0.2f, 0.34f);
            int sides = genes.Count("sides", 4, 8);
            mesh.At(Matrix4.CreateTranslation(0f, thick, 0f), () =>
            {
                mesh.Lathe(style.Angular > 0.5f ? Section.Polygon(sides, genes.Range("turn", 0f, 1f)) : Section.Round(12), board * 0.8f, 1, t => face * (1f - 0.2f * t), palette.Fitting);
                mesh.At(Matrix4.CreateTranslation(0f, board * 0.8f, 0f), () => mesh.Gem(sides, face * 0.5f, face * 0.25f, face * 0.1f, 0.6f, palette.Glow));
            });

            // A strap across the front, and a clasp where it meets the boards.
            if (!genes.Chance("strap", 0.6f)) return;
            float strap = tall * genes.Range("strap.wide", 0.08f, 0.14f);
            Slab(mesh, flat, thick, board * 0.4f, wide * 0.5f, strap, wide * 0.5f, palette.Binding);
            Slab(mesh, flat, board, thick - board, board * 0.5f, strap, wide + board * 0.4f, palette.Binding);
        }

        // A flat box, from one height to another, set off to one side of the middle.
        private static void Slab(GearMesh mesh, Section section, float bottom, float height, float halfWidth, float halfDepth, float x, Model.VertexMaterial material, float z = 0f)
        {
            mesh.At(Matrix4.CreateTranslation(x, 0f, z), () =>
            {
                mesh.Loft(section, new[] { Ring.Flat(bottom, halfWidth, halfDepth, hard: true), Ring.Flat(bottom + height, halfWidth, halfDepth, hard: true) }, material);
            });
        }
    }
}

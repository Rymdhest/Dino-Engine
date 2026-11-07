using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>A book, lying flat: shut with its spine along -x, or being read with its spine down the middle. Boards, a block of pages between them, and whatever's been done to keep it shut.</summary>
    public static class BookGenerator
    {
        private const int PageSteps = 10;

        // What a book is made to, whichever way it's lying.
        private readonly record struct Cut(float Wide, float Tall, float Thick, float Board, float Inset, Section Flat, Model.VertexMaterial Cover);

        private static Cut Measure(GearStyle style)
        {
            Genes genes = style.Genes;
            GearPalette palette = style.Palette;

            // Its proportions change and its size doesn't much, as they're all something to be carried.
            float size = genes.Range("size", 0.46f, 0.6f);
            float shape = MathF.Sqrt(genes.Range("tall", 1.05f, 1.75f));
            Section flat = Section.Slab(genes.Range("bevel", 0.08f, 0.22f));

            // Bound in leather more often than not, and in cloth by those who couldn't run to it.
            Model.VertexMaterial cover = genes.Pick("cover", 3f, 2f, 1f) switch { 0 => palette.Leather, 1 => palette.Cloth, _ => palette.Trim };
            return new Cut(size / shape, size * shape, genes.Range("thick", 0.14f, 0.5f), genes.Range("board", 0.028f, 0.05f), genes.Range("inset", 0.025f, 0.05f), flat, cover);
        }

        public static void Build(GearMesh mesh, GearStyle style)
        {
            Genes genes = style.Genes;
            GearPalette palette = style.Palette;
            (float wide, float tall, float thick, float board, float inset, Section flat, Model.VertexMaterial cover) = Measure(style);

            // Boards, top and bottom, with the same thing running round the spine.
            Slab(mesh, flat, 0f, board, wide, tall, 0f, cover);
            Slab(mesh, flat, thick - board, board, wide, tall, 0f, cover);
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

            // A ribbon left in it, coming out wherever it was left.
            if (genes.Chance("ribbon", 0.55f))
            {
                float hangs = tall * genes.Range("ribbon.hangs", 0.12f, 0.34f);
                Slab(mesh, flat, thick * 0.5f, board * 0.3f, wide * genes.Range("ribbon.wide", 0.04f, 0.08f), hangs, wide * genes.Range("ribbon.along", -0.5f, 0.6f), palette.Binding, tall + hangs * 0.6f);
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

        /// <summary>The same book, laid open somewhere in the middle, with its spine along z and writing on both pages.</summary>
        public static void Open(GearMesh mesh, GearStyle style)
        {
            Genes genes = style.Genes;
            GearPalette palette = style.Palette;
            (float wide, float tall, float thick, float board, float inset, Section flat, Model.VertexMaterial cover) = Measure(style);

            float page = thick - board * 2f;
            float through = genes.Range("open.through", 0.3f, 0.7f);
            int lines = genes.Count("open.lines", 4, 7);

            Slab(mesh, flat, 0f, board * 0.8f, board * 2f, tall, 0f, palette.Binding);
            foreach (float side in new[] { -1f, 1f })
            {
                Slab(mesh, flat, 0f, board, wide, tall, side * wide, cover);

                // However far in it's open, a leaf falls away from the gutter and then rises over what's under it.
                float height = MathF.Max(page * (side < 0f ? through : 1f - through), board);
                float Top(float along) => board + height * (0.55f + 0.45f * MathF.Sin(MathF.PI * MathF.Pow(along, 0.55f)));
                float across = (wide - inset) * 2f;

                var leaves = new Ring[PageSteps + 1];
                for (int i = 0; i <= PageSteps; i++)
                {
                    float along = (float)i / PageSteps;
                    leaves[i] = Across(side, along * across, (Top(along) + board) * 0.5f, 0f, tall - inset, (Top(along) - board) * 0.5f);
                }
                mesh.Loft(flat, leaves, palette.Bone);

                // Writing, which is all that says this is a page from further off than anyone looks at one.
                for (int line = 0; line < lines; line++)
                {
                    float z = (tall - inset) * (0.72f - 1.44f * line / (lines - 1f));
                    float ends = genes.Range($"open.line{line}.{side}", 0.72f, 0.9f);
                    var ink = new Ring[PageSteps / 2 + 1];
                    for (int i = 0; i < ink.Length; i++)
                    {
                        float along = 0.14f + (ends - 0.14f) * i / (ink.Length - 1f);
                        ink[i] = Across(side, along * across, Top(along) + board * 0.05f, z, tall * 0.022f, board * 0.08f);
                    }
                    mesh.Loft(flat, ink, line == 0 ? palette.Glow : palette.Binding);
                }
            }

            // The ribbon stays with the page it marks, down the gutter and out of the bottom.
            if (genes.Chance("ribbon", 0.55f))
            {
                float hangs = tall * genes.Range("ribbon.hangs", 0.12f, 0.34f);
                Slab(mesh, flat, board * 1.1f, board * 0.3f, wide * genes.Range("ribbon.wide", 0.04f, 0.08f), tall + hangs * 0.5f, 0f, palette.Binding, hangs * 0.5f);
            }
        }

        // A slice through something that runs out from the gutter, to either side of it.
        private static Ring Across(float side, float x, float y, float z, float halfWidth, float halfDepth) => new Ring
        {
            Centre = new Vector3(side * x, y, z), Side = Vector3.UnitZ * -side, Front = Vector3.UnitY, HalfWidth = halfWidth, HalfDepth = halfDepth,
        };

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

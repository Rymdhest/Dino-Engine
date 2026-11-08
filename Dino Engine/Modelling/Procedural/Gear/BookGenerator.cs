using Dino_Engine.Util;
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

        /// <summary>A loose leaf, lying flat and curling a little, with writing on it. Its proportions and its curl are what tell one from another, and how far down it's written.</summary>
        public static void Leaf(GearMesh mesh, GearStyle style)
        {
            Genes genes = style.Genes;
            GearPalette palette = style.Palette;

            float wide = genes.Range("leaf.wide", 0.34f, 0.72f);
            float tall = wide * genes.Range("leaf.tall", 1f, 2f);
            float curl = genes.Range("leaf.curl", 0.02f, 0.3f);
            float lean = genes.Range("leaf.lean", -0.3f, 0.3f);
            float taper = genes.Range("leaf.taper", -0.45f, 0.45f);
            float thin = 0.012f;
            Section flat = Section.Slab(0.2f);
            float Rise(float along) => curl * along * along * (genes.Chance("leaf.flip", 0.5f) ? 1f : 0.4f);

            var sheet = new Ring[PageSteps + 1];
            for (int i = 0; i <= PageSteps; i++)
            {
                float along = (float)i / PageSteps;
                sheet[i] = Across(1f, along * wide * 2f - wide, thin + Rise(along), lean * (along - 0.5f), tall * (1f + taper * (along - 0.5f)), thin);
            }
            mesh.Loft(flat, sheet, palette.Bone);

            int lines = genes.Count("leaf.lines", 2, 9);
            for (int line = 0; line < lines; line++)
            {
                float z = tall * (0.7f - 1.4f * line / (lines - 1f));
                float ends = genes.Range($"leaf.line{line}", 0.6f, 0.88f);
                var ink = new Ring[PageSteps / 2 + 1];
                for (int i = 0; i < ink.Length; i++)
                {
                    float along = 0.12f + (ends - 0.12f) * i / (ink.Length - 1f);
                    ink[i] = Across(1f, along * wide * 2f - wide, thin * 2.2f + Rise(along), z + lean * (along - 0.5f), tall * 0.028f, thin * 0.3f);
                }
                mesh.Loft(flat, ink, line == 0 ? palette.Glow : palette.Binding);
            }

            // A seal, for one somebody meant to keep, wherever it was pressed.
            if (genes.Chance("leaf.seal", 0.6f))
            {
                float along = genes.Range("leaf.seal.along", 0.15f, 0.85f);
                float radius = wide * genes.Range("leaf.seal.size", 0.1f, 0.26f);
                Vector3 at = new Vector3(along * wide * 2f - wide, thin * 2f + Rise(along), tall * genes.Range("leaf.seal.down", -0.8f, 0.8f));
                mesh.At(Matrix4.CreateTranslation(at), () => mesh.Lathe(Section.Polygon(genes.Count("leaf.seal.sides", 5, 9)), thin * 2f, 1, t => radius, palette.Fitting));
            }
        }

        /// <summary>A sticker: a flat seal of some shape with a rim, and a stone in the middle of it for its colour.</summary>
        public static void Seal(GearMesh mesh, GearStyle style)
        {
            Genes genes = style.Genes;
            GearPalette palette = style.Palette;

            float radius = genes.Range("seal.radius", 0.26f, 0.42f);
            int sides = genes.Count("seal.sides", 3, 9);
            float turn = genes.Range("seal.turn", 0f, 1f);
            float height = genes.Range("seal.height", 0.03f, 0.07f);
            float long_ = genes.Range("seal.long", 1f, 1.5f);
            Section section = sides >= 9 ? Section.Round(14) : Section.Polygon(sides, turn);

            mesh.At(Matrix4.CreateScale(1f, 1f, long_), () =>
            {
                mesh.Lathe(section, height, 1, t => radius, palette.Fitting);
                mesh.At(Matrix4.CreateTranslation(0f, height, 0f), () =>
                {
                    mesh.Lathe(section, height * 0.5f, 1, t => radius * 0.78f, genes.Pick("seal.face", 2f, 2f, 1f) switch { 0 => palette.Leather, 1 => palette.Cloth, _ => palette.Trim });
                    mesh.At(Matrix4.CreateTranslation(0f, height * 0.5f, 0f), () => mesh.Gem(Math.Max(sides, 4), radius * genes.Range("seal.stone", 0.3f, 0.5f), radius * 0.2f, radius * 0.05f, 0.6f, palette.Glow));
                });
            });
        }

        // The Reich's colours, which its orders are sealed in, and nobody else's are. Read LORE.md.
        private static readonly Vector3 Oxblood = new Vector3(0.28f, 0.03f, 0.03f);
        private static readonly Vector3 Brass = new Vector3(0.62f, 0.44f, 0.16f);

        /// <summary>
        /// Sealed orders: a roll of parchment lying on its side, tied round the middle and sealed in oxblood wax pressed with brass, as the Reich seals everything it sends. Better orders are rolled tighter
        /// and tied more than once, and the best come in a case. Every number comes from its own gene, so no two are the same roll.
        /// </summary>
        public static void Orders(GearMesh mesh, GearStyle style)
        {
            Genes genes = style.Genes;
            GearPalette palette = style.Palette;
            int grade = style.Spec.Grade;

            float length = genes.Range("orders.length", 0.62f, 0.92f);
            float radius = genes.Range("orders.radius", 0.065f, 0.1f);
            float lip = radius * genes.Range("orders.lip", 1.12f, 1.3f);
            float lipWidth = length * genes.Range("orders.lip.width", 0.05f, 0.09f);
            var wax = new Model.VertexMaterial(GearMaterials.Gem, new Colour(Oxblood * genes.Range("orders.wax", 0.85f, 1.15f)));
            var brass = new Model.VertexMaterial(GearMaterials.Metal, new Colour(Brass * genes.Range("orders.brass", 0.85f, 1.1f)));
            var parchment = genes.Chance("orders.paper", 0.7f) ? palette.Bone : new Model.VertexMaterial(GearMaterials.Bone, new Colour(GearPalette.Hsv(genes.Range("orders.paper.hue", 28f, 42f) / 360f, genes.Range("orders.paper.sat", 0.2f, 0.36f), genes.Range("orders.paper.value", 0.48f, 0.66f))));
            bool cased = grade >= 3 || genes.Chance("orders.cased", grade * 0.12f);

            // Built along +y, then laid on its side along +x with its middle at the origin.
            mesh.At(Matrix4.CreateTranslation(0f, -length * 0.5f, 0f) * Matrix4.CreateRotationZ(MathF.PI / 2f) * Matrix4.CreateTranslation(0f, lip, 0f), () =>
            {
                if (cased)
                {
                    // A leather case with brass caps, for orders somebody meant to arrive.
                    mesh.Lathe(Section.Round(12), length, 1, t => lip * 1.05f, palette.Leather);
                    foreach (float end in new[] { 0f, length - lipWidth * 1.4f })
                    {
                        mesh.At(Matrix4.CreateTranslation(0f, end, 0f), () => mesh.Lathe(Section.Round(12), lipWidth * 1.4f, 1, t => lip * 1.18f, brass));
                    }
                }
                else
                {
                    mesh.Lathe(Section.Round(10), length, 1, t => radius, parchment);
                    foreach (float end in new[] { 0f, length - lipWidth })
                    {
                        mesh.At(Matrix4.CreateTranslation(0f, end, 0f), () => mesh.Lathe(Section.Round(10), lipWidth, 1, t => lip, parchment));
                    }
                }

                // Tied, once round the middle and once more for each grade past the first, in oxblood.
                int ties = Math.Clamp(1 + grade / 2, 1, 3);
                for (int tie = 0; tie < ties; tie++)
                {
                    float along = length * (ties == 1 ? genes.Range("orders.tie", 0.42f, 0.58f) : 0.3f + 0.4f * tie / (ties - 1f));
                    float outer = (cased ? lip * 1.08f : radius) * 1.06f;
                    mesh.At(Matrix4.CreateTranslation(0f, along, 0f), () => mesh.Lathe(Section.Round(10), length * 0.035f, 1, t => outer, new Model.VertexMaterial(GearMaterials.Cloth, new Colour(Oxblood))));
                }

                // The seal, pressed on where it's tied, facing up.
                float sealAt = length * genes.Range("orders.seal.at", 0.4f, 0.6f);
                float sealRadius = radius * genes.Range("orders.seal.size", 0.75f, 1.05f) * (cased ? 1.25f : 1f);
                float surface = cased ? lip * 1.05f : radius;
                mesh.At(Matrix4.CreateRotationZ(-MathF.PI / 2f) * Matrix4.CreateTranslation(surface * 0.82f, sealAt, 0f), () =>
                {
                    mesh.Lathe(Section.Polygon(genes.Count("orders.seal.sides", 7, 11)), sealRadius * 0.55f, 1, t => sealRadius * (1f - 0.18f * t), wax);
                    mesh.At(Matrix4.CreateTranslation(0f, sealRadius * 0.55f, 0f), () => mesh.Lathe(Section.Round(10), sealRadius * 0.12f, 1, t => sealRadius * 0.48f, brass));
                });
            });
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

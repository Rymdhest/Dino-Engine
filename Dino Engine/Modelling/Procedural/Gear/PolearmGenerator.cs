using Dino_Engine.Modelling.Model;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>
    /// Everything that's a head on a shaft: axes, maces, spears, staffs and wands. Built standing up with the hand at the origin and the head up +y. What makes one of these what it is
    /// is its head, and every head is drawn curves: an axe's blade is two edges and a neck, not one of a list of axes.
    /// </summary>
    public static class PolearmGenerator
    {
        /// <summary>A shaft from one height to another. The two ends stay on the axis, so that whatever's fixed to them is where it was put.</summary>
        internal static void Shaft(GearMesh mesh, GearStyle style, Genes genes, float bottom, float top, float radius, float gnarl = 0f)
        {
            GearPalette palette = style.Palette;
            float length = top - bottom;
            float taper = genes.Range("taper", 0.72f, 1f);
            float bend = gnarl * genes.Around("bend", 0f, 0.05f) * length;
            float lumps = genes.Range("lumps", 3f, 9f);
            float lumpSize = gnarl * genes.Range("lump.size", 0.1f, 0.26f);
            float phase = genes.Range("phase", 0f, MathF.Tau);

            int bands = genes.Count("bands", 0, 2 + (int)(style.Ornate * 3f));
            int wraps = genes.Count("wraps", 3, 9);
            float wrapHalf = genes.Range("grip.half", 0.28f, 0.5f);
            bool metal = style.Spec.Heavy > 0.5f && genes.Chance("shaft.metal", 0.35f);
            VertexMaterial body = metal ? palette.Dark : palette.Wood;

            int steps = Math.Clamp((int)(length * 7f) + wraps * 2, 12, 56);
            var rings = new Ring[steps + 1];
            var colours = new VertexMaterial[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                float y = bottom + t * length;
                float ends = MathF.Sin(t * MathF.PI);
                float r = radius * (1f + (taper - 1f) * t) * (1f + lumpSize * MathF.Sin(t * lumps * MathF.Tau + phase) * ends);
                rings[i] = new Ring { Centre = new Vector3(bend * ends * ends, y, gnarl * bend * 0.6f * MathF.Sin(t * MathF.PI * 2f)), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = r, HalfDepth = r };

                colours[i] = body;
                if (MathF.Abs(y) < wrapHalf && i % 2 == 0) colours[i] = palette.Binding;
                else if (MathF.Abs(y) < wrapHalf) colours[i] = palette.Leather;
            }
            mesh.Loft(Section.Round(8), rings, (ring, _) => colours[ring], capStart: true, capEnd: true);

            for (int i = 0; i < bands; i++)
            {
                float at = bottom + length * genes.Range($"band{i}", 0.12f, 0.92f);
                float half = radius * genes.Range($"band{i}.half", 0.5f, 1.2f);
                mesh.At(Matrix4.CreateTranslation(0f, at - half, 0f), () => mesh.Lathe(Section.Round(8), half * 2f, 1, _ => radius * 1.22f, palette.Fitting));
            }
        }

        /// <summary>Something fixed to the end of a shaft to finish it.</summary>
        private static void Butt(GearMesh mesh, GearStyle style, Genes genes, float y, float radius)
        {
            float height = radius * genes.Range("butt.height", 1.4f, 4f);
            if (genes.Chance("butt.spike", style.Spikes))
            {
                float reach = height * genes.Range("butt.reach", 1.2f, 2.6f);
                mesh.At(Matrix4.CreateTranslation(0f, y - reach, 0f), () => mesh.Lathe(Section.Polygon(genes.Count("butt.sides", 3, 6)), reach, 2, t => radius * 1.2f * t, style.Palette.Fitting, capEnd: false));
                return;
            }
            mesh.At(Matrix4.CreateTranslation(0f, y - height, 0f), () => BladeGenerator.Knob(mesh, style, genes.Part("butt"), height, radius * genes.Range("butt.size", 1.3f, 2.1f), style.Palette.Fitting));
        }

        private static float Scale(float low, float high, float t) => low + (high - low) * t;

        // An axe's blade is drawn by its two edges from top to bottom: where the cutting edge is at each height, and where the neck comes back to meet the haft.
        public static void Axe(GearMesh mesh, GearStyle style)
        {
            Genes genes = style.Genes;
            GearForm form = style.Spec.Form;
            GearPalette palette = style.Palette;

            float length = genes.Around("length", Scale(1.5f, 2.6f, form.Size), 0.12f);
            float radius = genes.Range("radius", 0.05f, 0.07f) * (0.85f + 0.4f * style.Heft);
            float top = length * 0.68f;
            float bottom = -length * 0.32f;

            Shaft(mesh, style, genes.Part("shaft"), bottom, top, radius);
            Butt(mesh, style, genes, bottom, radius);

            float bite = genes.Around("bite", Scale(0.34f, 0.72f, 1f - form.Slender), 0.07f) * (0.8f + 0.4f * form.Size);
            float upper = bite * genes.Range("reach.up", 0.3f, 0.75f);
            float lower = bite * genes.Range("reach.down", 0.3f, 0.7f) * (1f + form.Curve * 1.1f);      // The beard.
            float crescent = genes.Range("crescent", 0.08f, 0.42f) * bite;
            float neck = genes.Range("neck", 0.2f, 0.7f);
            float thick = radius * genes.Range("thick", 0.8f, 1.5f) * (0.8f + 0.6f * style.Heft);
            float waist = genes.Range("waist", 0.85f, 1.8f);
            float y = top - upper - genes.Range("drop", 0.02f, 0.14f);

            void Blade(float side, Genes part)
            {
                int steps = 14;
                var rings = new Ring[steps + 1];
                for (int i = 0; i <= steps; i++)
                {
                    float t = (float)i / steps;
                    float height = -lower + (upper + lower) * t;
                    float along = t * 2f - 1f;

                    // The edge bows outwards and the neck comes in, with its tips swept out from the haft.
                    float edge = bite - crescent * along * along;
                    float inner = radius * 0.6f + bite * (1f - neck) * MathF.Pow(MathF.Abs(along), waist);
                    inner = MathF.Min(inner, edge - bite * 0.05f);
                    float tip = 1f - along * along;
                    rings[i] = new Ring
                    {
                        Centre = new Vector3(side * (edge + inner) * 0.5f, y + height, 0f),
                        Side = Vector3.UnitX * side,
                        Front = Vector3.UnitZ,
                        HalfWidth = (edge - inner) * 0.5f,
                        HalfDepth = thick * (0.25f + 0.75f * tip),
                        Shape = Section.BladeOutline(part.Range("flat", 0.1f, 0.6f), 0.5f, 0f),
                    };
                }

                mesh.Loft(Section.Blade(), rings, (_, point) => point == 0 ? palette.Edge : point == 6 ? palette.Metal : palette.Metal);
            }

            Blade(1f, genes.Part("blade"));
            bool both = genes.Chance("double", style.Heft * 0.45f + style.Ornate * 0.15f);
            if (both) Blade(-1f, genes.Part("blade"));
            else Poll(mesh, style, genes.Part("poll"), y, radius);

            // What holds it on.
            float eye = radius * genes.Range("eye", 1.5f, 2.1f);
            float eyeHeight = (upper + lower) * genes.Range("eye.height", 0.38f, 0.7f);
            mesh.At(Matrix4.CreateTranslation(0f, y - eyeHeight * 0.5f, 0f), () => mesh.Lathe(style.Angular > 0.5f ? Section.Polygon(8) : Section.Round(10), eyeHeight, 2, _ => eye, palette.Metal));

            if (genes.Chance("top.spike", style.Spikes * 0.6f)) Spike(mesh, style, genes.Part("spike"), top, radius);
        }

        // The back of a one-sided head.
        private static void Poll(GearMesh mesh, GearStyle style, Genes genes, float y, float radius)
        {
            float reach = radius * genes.Range("reach", 2f, 7f);
            bool pick = genes.Chance("pick", style.Spikes);
            float size = radius * genes.Range("size", 1f, 1.9f);
            var spine = new List<Vector3>();
            float droop = genes.Around("droop", 0.1f, 0.14f) * reach;
            for (int i = 0; i <= 6; i++)
            {
                float t = i / 6f;
                spine.Add(new Vector3(-reach * t, y - droop * t * t, 0f));
            }
            mesh.Sweep(style.Angular > 0.4f ? Section.Slab(0.3f) : Section.Round(8), spine, t => new Vector2(size, size * 0.7f) * (pick ? 1f - t * 0.96f : 1f + t * 0.3f), style.Palette.Metal, Vector3.UnitY, capStart: false, capEnd: !pick);
        }

        private static void Spike(GearMesh mesh, GearStyle style, Genes genes, float y, float radius)
        {
            float reach = radius * genes.Range("reach", 2.5f, 7f);
            mesh.At(Matrix4.CreateTranslation(0f, y, 0f), () => mesh.Lathe(Section.Polygon(genes.Count("sides", 3, 6)), reach, 3, t => radius * 1.1f * (1f - t), style.Palette.Metal, capEnd: false));
        }

        // Hitting things is simple, so a head can be almost anything heavy. Which is decided by weights and not a list of maces: a head can be a block with flanges and a spike on it.
        public static void Mace(GearMesh mesh, GearStyle style)
        {
            Genes genes = style.Genes;
            GearForm form = style.Spec.Form;
            GearPalette palette = style.Palette;

            float length = genes.Around("length", Scale(1.9f, 3.2f, form.Size), 0.14f);
            float radius = genes.Range("radius", 0.055f, 0.08f) * (0.85f + 0.45f * style.Heft);
            float top = length * 0.7f;
            float bottom = -length * 0.3f;
            Shaft(mesh, style, genes.Part("shaft"), bottom, top, radius);
            Butt(mesh, style, genes, bottom, radius);

            float size = genes.Around("head", Scale(0.2f, 0.56f, (1f - form.Slender) * 0.6f + form.Size * 0.4f), 0.04f);
            float block = genes.Unit("block") * (1f - form.Curve * 0.5f);
            if (block > 0.42f)
            {
                // A block across the shaft, its two faces drawn apart.
                float face = size * genes.Range("face", 0.75f, 1.05f);
                float span = size * genes.Range("span", 1.3f, 2.1f);
                float swell = genes.Range("swell", 0.8f, 1.5f);
                Section section = style.Angular > 0.35f ? Section.Slab(genes.Range("bevel", 0.1f, 0.5f)) : Section.Super(12, genes.Range("round", 2.4f, 5f));
                var rings = new Ring[9];
                for (int i = 0; i <= 8; i++)
                {
                    float t = i / 8f;
                    float s = t * 2f - 1f;
                    float r = face * (1f + (swell - 1f) * s * s);
                    rings[i] = new Ring { Centre = new Vector3(s * span, top - face * 0.5f, 0f), Side = Vector3.UnitY, Front = Vector3.UnitZ, HalfWidth = r, HalfDepth = r * genes.Range("depth", 0.7f, 1.05f), Hard = i == 0 || i == 8 };
                }
                mesh.Loft(section, rings, palette.Metal);

                if (genes.Chance("spike.side", style.Spikes)) mesh.At(Matrix4.CreateRotationZ(MathF.PI / 2f) * Matrix4.CreateTranslation(-span, top - face * 0.5f, 0f), () => mesh.Lathe(Section.Polygon(genes.Count("sides", 3, 6)), span * genes.Range("spike.reach", 0.6f, 1.3f), 3, t => face * 0.6f * (1f - t), palette.Metal, capEnd: false));
            }
            else
            {
                float height = size * genes.Range("height", 1.2f, 2.1f);
                float y = top - height * 0.45f;
                mesh.At(Matrix4.CreateTranslation(0f, y, 0f), () => BladeGenerator.Knob(mesh, style, genes.Part("core"), height, size * 0.72f, palette.Metal));

                // Flanges stand out from the middle, and studs sit all over it.
                int flanges = genes.Chance("flanged", 0.6f) ? genes.Count("flanges", 4, 9) : 0;
                float reach = size * genes.Range("flange.reach", 0.5f, 1.05f);
                for (int i = 0; i < flanges; i++)
                {
                    float angle = i * (MathF.Tau / flanges);
                    mesh.At(Matrix4.CreateRotationY(angle) * Matrix4.CreateTranslation(0f, y + height * 0.5f, 0f), () =>
                    {
                        float half = height * 0.46f;
                        mesh.Loft(Section.Slab(0.4f), new[]
                        {
                            new Ring { Centre = new Vector3(size * 0.45f, -half, 0f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = reach * 0.2f, HalfDepth = size * 0.08f, Hard = true },
                            new Ring { Centre = new Vector3(size * 0.5f + reach * 0.45f, 0f, 0f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = reach * 0.55f, HalfDepth = size * 0.1f },
                            new Ring { Centre = new Vector3(size * 0.45f, half, 0f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = reach * 0.2f, HalfDepth = size * 0.08f, Hard = true },
                        }, palette.Metal);
                    });
                }

                if (flanges == 0)
                {
                    int studs = genes.Count("studs", 6, 20);
                    float stud = size * genes.Range("stud", 0.2f, 0.42f);
                    for (int i = 0; i < studs; i++)
                    {
                        // Spread evenly, round and up.
                        float up = 1f - 2f * (i + 0.5f) / studs;
                        float round = i * 2.399963f;
                        float flat = MathF.Sqrt(1f - up * up);
                        Vector3 outwards = new Vector3(MathF.Cos(round) * flat, up, MathF.Sin(round) * flat);
                        Vector3 at = new Vector3(0f, y + height * 0.5f, 0f) + outwards * size * 0.7f;
                        Vector3 axis = Vector3.Cross(Vector3.UnitY, outwards);
                        float turn = MathF.Acos(Math.Clamp(up, -1f, 1f));
                        Matrix4 place = (axis.LengthSquared > 1e-5f ? Matrix4.CreateFromAxisAngle(axis.Normalized(), turn) : Matrix4.Identity) * Matrix4.CreateTranslation(at);
                        mesh.At(place, () => mesh.Lathe(Section.Polygon(5), stud * 1.5f, 2, t => stud * 0.6f * (1f - t), palette.Fitting, capEnd: false));
                    }
                }
            }

            if (style.Shines("stone")) Stone(mesh, style, genes.Part("stone"), top + size * 0.15f, radius * 1.6f);
        }

        private static void Stone(GearMesh mesh, GearStyle style, Genes genes, float y, float radius)
        {
            mesh.At(Matrix4.CreateTranslation(0f, y + radius * 0.3f, 0f), () => mesh.Gem(genes.Count("sides", 5, 9), radius, radius * genes.Range("crown", 0.5f, 1.2f), radius * genes.Range("pavilion", 0.3f, 0.9f), genes.Range("table", 0.3f, 0.7f), style.Palette.Stone));
        }

        // A spear's point is a blade, made by what makes swords, on a long shaft with whatever a smith put below it to stop an animal coming up it.
        public static void Spear(GearMesh mesh, GearStyle style)
        {
            Genes genes = style.Genes;
            GearForm form = style.Spec.Form;
            GearPalette palette = style.Palette;

            float length = genes.Around("length", Scale(4f, 5.4f, form.Size), 0.2f);
            float radius = genes.Range("radius", 0.045f, 0.062f) * (0.85f + 0.35f * style.Heft);
            float top = length * 0.62f;
            float bottom = -length * 0.38f;
            Shaft(mesh, style, genes.Part("shaft"), bottom, top, radius);
            Butt(mesh, style, genes, bottom, radius);

            float socket = genes.Range("socket", 0.25f, 0.5f);
            float head = genes.Around("head", Scale(0.7f, 1.8f, form.Size * 0.5f + form.Curve * 0.5f), 0.1f);
            float width = genes.Around("width", Scale(0.09f, 0.22f, 1f - form.Slender), 0.02f);
            mesh.At(Matrix4.CreateTranslation(0f, top - socket * 0.4f, 0f), () => mesh.Lathe(Section.Round(8), socket, 3, t => radius * (1.5f - 0.4f * t), palette.Metal));
            BladeGenerator.Blade(mesh, style, genes.Part("head"), head, width, top + socket * 0.5f);

            // Lugs below the head. How far they stand out is what a smith was asked for.
            float lugs = form.Cover * genes.Range("lug.reach", 0.5f, 1.3f);
            if (genes.Chance("lugs", form.Cover))
            {
                float reach = width * (0.8f + lugs * 2.2f);
                float rise = reach * genes.Around("lug.rise", 0.2f, 0.4f);
                float thick = radius * genes.Range("lug.thick", 0.3f, 0.7f);
                float y = top + socket * 0.15f;
                mesh.BothSides(() =>
                {
                    var keys = new[] { new Vector3(radius * 0.8f, y, 0f), new Vector3(reach * 0.5f, y + rise * 0.25f, 0f), new Vector3(reach, y + rise, 0f) };
                    mesh.Sweep(Section.Slab(0.4f), Spine.Through(keys, 7), t => new Vector2(thick * 1.3f, thick) * (genes.Chance("lug.taper", 0.7f) ? 1f - t * 0.9f : 1f), palette.Metal, Vector3.UnitZ);
                });
            }

            if (genes.Chance("tassel", style.Ornate * 0.5f)) Tassel(mesh, style, genes.Part("tassel"), top - socket, radius);
        }

        private static void Tassel(GearMesh mesh, GearStyle style, Genes genes, float y, float radius)
        {
            int strands = genes.Count("strands", 4, 9);
            float reach = genes.Range("reach", 0.35f, 0.8f);
            for (int i = 0; i < strands; i++)
            {
                float angle = i * (MathF.Tau / strands);
                Vector3 outwards = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                var keys = new[] { outwards * radius + Vector3.UnitY * y, outwards * radius * 2.4f + Vector3.UnitY * (y - reach * 0.5f), outwards * radius * 2f + Vector3.UnitY * (y - reach * genes.Range($"len{i}", 0.75f, 1.1f)) };
                mesh.Sweep(Section.Round(5), Spine.Through(keys, 5), t => new Vector2(radius * 0.32f * (1f - t * 0.75f)), style.Palette.Trim, Vector3.UnitZ);
            }
        }

        // Tall and light, and it's what's at the top that matters: something held up, in whatever's holding it.
        public static void Staff(GearMesh mesh, GearStyle style)
        {
            Genes genes = style.Genes;
            GearForm form = style.Spec.Form;

            float length = genes.Around("length", Scale(3.8f, 5.2f, form.Size), 0.2f);
            float radius = genes.Range("radius", 0.05f, 0.075f) * (0.8f + 0.5f * (1f - form.Slender));
            float top = length * 0.64f;
            float bottom = -length * 0.36f;
            Shaft(mesh, style, genes.Part("shaft"), bottom, top, radius, gnarl: genes.Range("gnarl", 0f, 1f) * (1f - form.Fancy * 0.6f));
            Butt(mesh, style, genes, bottom, radius);
            Crown(mesh, style, genes.Part("crown"), top, radius, Scale(0.3f, 0.75f, form.Cover * 0.5f + form.Size * 0.5f));

            if (genes.Chance("charms", style.Ornate * 0.5f)) Tassel(mesh, style, genes.Part("charm"), top - radius * 3f, radius);
        }

        // Short, tapering and fine, and otherwise the same thing as a staff.
        public static void Wand(GearMesh mesh, GearStyle style)
        {
            Genes genes = style.Genes;
            GearForm form = style.Spec.Form;
            GearPalette palette = style.Palette;

            float length = genes.Around("length", Scale(1.1f, 2f, form.Size), 0.1f);
            float radius = genes.Range("radius", 0.035f, 0.055f) * (0.8f + 0.5f * (1f - form.Slender));
            float bottom = -length * 0.28f;
            float top = length * 0.72f;
            float curl = form.Curve * genes.Around("curl", 0.12f, 0.1f);

            int steps = 22;
            bool bone = genes.Chance("bone", 0.4f + form.Fancy * 0.2f);
            VertexMaterial body = bone ? palette.Bone : palette.Wood;
            var rings = new Ring[steps + 1];
            int knuckles = genes.Count("knuckles", 2, 7);
            float knuckle = genes.Range("knuckle", 0f, 0.3f);
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                float r = radius * (1.2f - 0.72f * t) * (1f + knuckle * MathF.Pow(MathF.Abs(MathF.Sin(t * knuckles * MathF.PI)), 3f));
                rings[i] = new Ring { Centre = new Vector3(curl * length * t * t * MathF.Sin(t * 2f), bottom + t * length, 0f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = r, HalfDepth = r };
            }
            mesh.Loft(Section.Round(7), rings, (ring, _) => MathF.Abs(bottom + (float)ring / steps * length) < 0.26f && ring % 2 == 0 ? palette.Binding : body);

            Vector3 tip = rings[steps].Centre;
            mesh.At(Matrix4.CreateTranslation(tip.X, 0f, 0f), () => Crown(mesh, style, genes.Part("crown"), top, radius * 0.7f, Scale(0.12f, 0.34f, form.Cover)));
            mesh.At(Matrix4.CreateTranslation(0f, bottom - radius * 2.4f, 0f), () => BladeGenerator.Knob(mesh, style, genes.Part("pommel"), radius * 2.6f, radius * 1.9f, palette.Fitting));
        }

        // Whatever a caster's weapon holds up. There's a cage of prongs, or a hook it hangs from, or a ring around it, by degrees, and a stone or a light at the heart of it.
        private static void Crown(GearMesh mesh, GearStyle style, Genes genes, float y, float radius, float size)
        {
            GearPalette palette = style.Palette;
            float stone = size * genes.Range("stone", 0.32f, 0.55f);
            float above = size * genes.Range("above", 0.55f, 0.95f);
            Vector3 heart = new Vector3(0f, y + above, 0f);

            int prongs = genes.Count("prongs", 2, 6);
            float spread = size * genes.Range("spread", 0.5f, 0.85f);
            bool wooden = genes.Chance("wooden", 0.35f);
            VertexMaterial claw = wooden ? palette.Wood : palette.Fitting;
            float tilt = genes.Range("twist", -0.7f, 0.7f);
            float rise = size * genes.Range("rise", 0.9f, 1.6f);
            float thick = radius * genes.Range("thick", 0.45f, 0.9f);
            for (int i = 0; i < prongs; i++)
            {
                float angle = i * (MathF.Tau / prongs);
                Vector3 Out(float a) => new Vector3(MathF.Cos(a), 0f, MathF.Sin(a));
                var keys = new[]
                {
                    new Vector3(0f, y, 0f) + Out(angle) * radius * 0.6f,
                    new Vector3(0f, y + rise * 0.33f, 0f) + Out(angle + tilt * 0.4f) * spread,
                    new Vector3(0f, y + rise * 0.7f, 0f) + Out(angle + tilt) * spread * 1.05f,
                    new Vector3(0f, y + rise, 0f) + Out(angle + tilt * 1.5f) * spread * genes.Range("close", 0.2f, 0.7f),
                };
                mesh.Sweep(Section.Round(6), Spine.Through(keys, 10), t => new Vector2(thick * (1f - t * 0.82f)), claw, Vector3.UnitZ, capEnd: true);
            }

            if (genes.Chance("hoop", style.Ornate * 0.6f))
            {
                float hoop = spread * genes.Range("hoop.size", 1f, 1.45f);
                mesh.At(Matrix4.CreateRotationX(genes.Range("hoop.tilt", -0.5f, 0.5f)) * Matrix4.CreateTranslation(heart), () => mesh.Hoop(Section.Round(6), hoop, thick * 0.7f, thick * 0.7f, 18, palette.Fitting));
            }

            bool lit = style.Shines("lit") || style.Spec.Theme != GearTheme.None;
            mesh.At(Matrix4.CreateTranslation(heart), () => mesh.Gem(genes.Count("facets", 5, 9), stone, stone * genes.Range("crown", 0.7f, 1.4f), stone * genes.Range("pavilion", 0.7f, 1.3f), genes.Range("table", 0.25f, 0.6f), lit ? palette.Glow : palette.Stone));
        }
    }
}

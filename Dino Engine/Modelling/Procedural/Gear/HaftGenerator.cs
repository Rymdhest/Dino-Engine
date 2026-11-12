using Dino_Engine.Modelling.Model;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>
    /// Everything that's a head on a haft and fights by its weight or its point: axes and greataxes, hammers and mauls, and javelins. Built standing up with the head up +y. Something held in one hand
    /// is held at the origin near the haft's foot; something that takes both is held by its upper hand at the origin, with the haft running down past where the lower one goes. A head is drawn, never
    /// picked: an axe's blade is two edges and a bit, a hammer's head is a block with a face and a back, and a javelin's is a blade or a long iron shank. See GEAR.md.
    /// </summary>
    public static class HaftGenerator
    {
        private const float SecondHand = 0.62f;     // How far below the upper hand the lower one holds anything that takes two (Carry.Greatsword).

        private static float Scale(float low, float high, float t) => low + (high - low) * t;

        /// <summary>
        /// A haft from one height to another, of wood with its grain along it, swelling to a knob at its foot, wrapped where the hands go, with metal langets running down from the head and riveted
        /// through, and bands where it needs holding together. The ends stay on the axis, so whatever's fixed to them is where it was put.
        /// </summary>
        internal static void Haft(GearMesh mesh, GearLook look, Genes genes, float bottom, float top, float radius, bool twoHands, float langets = 0f)
        {
            GearColours colours = look.Colours;
            float length = top - bottom;
            float taper = genes.Range("taper", 0.8f, 0.96f);
            float knob = genes.Chance("knob", 0.55f) ? genes.Range("knob.size", 0.12f, 0.3f) : 0f;
            float bow = genes.Around("bow", 0f, 0.012f) * length;
            bool metal = look.Heavy > 0.5f && genes.Chance("metal", 0.12f + look.Craft * 0.25f);
            VertexMaterial body = metal ? colours.MetalDark : colours.Wood;

            int steps = Math.Clamp((int)(length * 6f), 10, 40);
            var rings = new Ring[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                float y = bottom + t * length;
                float foot = knob * MathF.Exp(-t * 14f);
                float r = radius * (1f + (taper - 1f) * t) * (1f + foot);
                float ends = MathF.Sin(t * MathF.PI);
                rings[i] = new Ring { Centre = new Vector3(bow * ends * ends, y, 0f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = r, HalfDepth = r * genes.Range("oval", 0.82f, 1f) };
            }
            mesh.Loft(look.Angular > 0.75f && !metal ? Section.Polygon(8) : Section.Round(10), rings, body);

            // Where the hands go, wrapped: round the one hand, or down from the upper hand past the lower.
            float wrapTop = genes.Range("wrap.above", 0.14f, 0.26f);
            float wrapBottom = twoHands ? -SecondHand - genes.Range("wrap.below", 0.12f, 0.2f) : MathF.Max(bottom + radius * 2f, -genes.Range("wrap.below.one", 0.18f, 0.3f));
            if (genes.Chance("wrapped", 0.55f + look.Craft * 0.35f))
            {
                float RadiusAt(float y) => radius * (1f + (taper - 1f) * (y - bottom) / length) * 1.04f;
                mesh.Wrap(wrapBottom, wrapTop, t => RadiusAt(wrapBottom + (wrapTop - wrapBottom) * t), genes.Range("wrap.pitch", 0.04f, 0.07f), genes.Range("wrap.ridge", 0.08f, 0.14f), colours.Strap);
                if (twoHands && genes.Chance("wrap.split", 0.4f))
                {
                    // A bare stretch between the hands, with a band at either end of each wrap.
                    float gap = -SecondHand * 0.5f;
                    mesh.At(Matrix4.CreateTranslation(0f, gap - 0.04f, 0f), () => mesh.Lathe(Section.Round(10), 0.08f, 1, _ => RadiusAt(gap) * 1.12f, colours.Fitting));
                }
            }

            // Bands, which hold a split haft together or are only there to be seen.
            int bands = genes.Count("bands", 0, 1 + (int)(look.Ornament * 3f));
            for (int i = 0; i < bands; i++)
            {
                float at = bottom + length * genes.Range($"band{i}", 0.05f, 0.9f);
                if (at > wrapBottom - 0.06f && at < wrapTop + 0.06f) continue;
                float half = radius * genes.Range($"band{i}.half", 0.5f, 1.1f);
                float r = radius * (1f + (taper - 1f) * (at - bottom) / length) * 1.18f;
                mesh.At(Matrix4.CreateTranslation(0f, at - half, 0f), () => mesh.Lathe(look.Angular > 0.5f ? Section.Polygon(8) : Section.Round(10), half * 2f, 2, t => r * (1f + 0.08f * MathF.Sin(t * MathF.PI)), colours.Fitting));
            }

            // Langets: iron straps down the haft from the head, riveted through, so a blow can't cut the head off.
            if (langets > 0f)
            {
                float reach = langets * genes.Range("langet.reach", 0.8f, 1.2f);
                float wide = radius * genes.Range("langet.wide", 0.55f, 0.85f);
                int rivets = genes.Count("langet.rivets", 2, 4);
                for (int side = 0; side < (genes.Chance("langet.four", 0.35f) ? 4 : 2); side++)
                {
                    float angle = side * (MathF.Tau / (side >= 2 ? 4f : 2f)) + (side >= 2 ? MathF.PI / 2f : 0f);
                    var path = new List<Vector3>();
                    var normals = new List<Vector3>();
                    for (int k = 0; k <= 4; k++)
                    {
                        float y = top - reach * k / 4f;
                        float r = radius * (1f + (taper - 1f) * (y - bottom) / length);
                        Vector3 out_ = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                        path.Add(new Vector3(0f, y, 0f) + out_ * r);
                        normals.Add(out_);
                    }
                    mesh.Strip(path, normals, wide, radius * 0.16f, colours.Metal, width: t => 1f - t * 0.45f);
                    for (int k = 0; k < rivets; k++)
                    {
                        float s = (k + 0.6f) / (rivets + 0.4f);
                        Vector3 at = Vector3.Lerp(path[0], path[^1], s) + normals[0] * radius * 0.16f;
                        mesh.Rivet(at, normals[0], radius * 0.2f, colours.Fitting);
                    }
                }
            }
        }

        /// <summary>What finishes a haft's foot: a cap with the motif's end on it, or a spike.</summary>
        internal static void Butt(GearMesh mesh, GearLook look, Genes genes, float y, float radius)
        {
            GearColours colours = look.Colours;
            if (genes.Chance("spike", look.Spikes * 0.8f))
            {
                float reach = radius * genes.Range("spike.reach", 2.2f, 4.5f);
                mesh.At(Matrix4.CreateTranslation(0f, y - reach, 0f), () => mesh.Lathe(Section.Polygon(genes.Count("spike.sides", 4, 6)), reach, 3, t => radius * 1.15f * t, colours.Metal, capEnd: false));
                return;
            }
            if (!genes.Chance("cap", 0.35f + look.Craft * 0.5f)) return;
            float height = radius * genes.Range("cap.height", 1.6f, 3.2f);
            mesh.At(Matrix4.CreateTranslation(0f, y - height * 0.85f, 0f), () => GearKit.Terminal(mesh, look, genes.Part("cap"), height, radius * genes.Range("cap.size", 1.25f, 1.8f), colours.Fitting));
        }

        // ---------------------------------------------------------------------------------------------------------------- Axes

        /// <summary>
        /// An axe, with one hand or two. Its bit is drawn by its two edges from top to bottom: where the cutting edge is at each height, and where the neck comes back to the eye. A beard drops below,
        /// a crescent sweeps, and a greataxe's bit can be a bardiche's long blade lashed along the haft. Behind the eye is a poll, a hammer face, a spike, or a second bit.
        /// </summary>
        public static void Axe(GearMesh mesh, GearLook look)
        {
            Genes genes = look.Genes;
            GearForm form = look.Spec.Form;
            GearColours colours = look.Colours;
            bool two = look.Spec.Kind == GearKind.Greataxe;

            float length = genes.Around("length", two ? Scale(2.8f, 3.6f, form.Size) : Scale(1.45f, 2.1f, form.Size), 0.1f);
            float radius = genes.Range("radius", 0.05f, 0.066f) * (0.85f + 0.35f * look.Heft) * (two ? 1.22f : 1f);
            float bottom = two ? -SecondHand - genes.Range("below", 0.2f, 0.34f) : -genes.Range("below.one", 0.2f, 0.32f);
            float top = bottom + length;

            float bite = genes.Around("bite", Scale(0.36f, 0.66f, 1f - form.Slender), 0.06f) * (0.85f + 0.3f * form.Size) * (two ? 1.55f : 1f) * (look.Legendary ? 1.12f : 1f);
            float upper = bite * genes.Range("reach.up", 0.25f, 0.65f);
            float beard = bite * genes.Range("beard", 0.25f, 0.6f) * (1f + form.Curve * 1.4f);
            bool bardiche = two && form.Curve > 0.7f && genes.Chance("bardiche", 0.7f);
            if (bardiche) beard = bite * genes.Range("bardiche.drop", 1.6f, 2.4f);
            float crescent = genes.Range("crescent", 0.06f, 0.36f) * bite * (0.6f + look.Curl);
            float neck = genes.Range("neck", 0.18f, 0.6f);
            float thick = radius * genes.Range("thick", 0.5f, 0.85f) * (0.8f + 0.45f * look.Heft);
            float waist = genes.Range("waist", 0.9f, 1.8f);
            float y = top - upper - genes.Range("drop", 0.04f, 0.16f);
            float eye = radius * genes.Range("eye", 1.45f, 1.9f);
            float eyeHeight = MathF.Max((upper + beard) * genes.Range("eye.height", 0.32f, 0.55f), eye * 2.2f);
            if (bardiche) eyeHeight = (upper + beard) * 0.9f;

            Haft(mesh, look, genes.Part("haft"), bottom, top, radius, two, genes.Chance("langets", 0.3f + look.Craft * 0.4f) ? eyeHeight * genes.Range("langets", 0.9f, 1.6f) : 0f);
            Butt(mesh, look, genes.Part("butt"), bottom, radius);

            void Bit(float side, Genes part, float scale)
            {
                int steps = 16;
                float b = bite * scale;
                var rings = new Ring[steps + 1];
                for (int i = 0; i <= steps; i++)
                {
                    float t = (float)i / steps;
                    float height = -beard + (upper + beard) * t;
                    float along = t * 2f - 1f;

                    // The edge bows out and the neck draws in, with its horns swept out from the haft, and the beard trailing down along it for a bardiche.
                    float edge = b - crescent * along * along;
                    if (bardiche) edge = b * (0.75f + 0.25f * MathF.Sin(t * MathF.PI)) - crescent * 0.3f * along * along;
                    float inner = eye * 0.9f + b * (1f - neck) * MathF.Pow(MathF.Abs(along), waist) * (bardiche ? 0.2f : 1f);
                    inner = MathF.Min(inner, edge - b * 0.08f);
                    float tip = 1f - along * along;
                    rings[i] = new Ring
                    {
                        Centre = new Vector3(side * (edge + inner) * 0.5f, y + height, 0f),
                        Side = Vector3.UnitX * side,
                        Front = Vector3.UnitZ,
                        HalfWidth = (edge - inner) * 0.5f,
                        HalfDepth = thick * (0.3f + 0.7f * MathF.Sqrt(tip)),
                        Shape = Section.BladeSection(part.Range("flat", 0.3f, 0.7f), 0.5f, 0f, part.Range("band", 0.16f, 0.3f), 0.85f, 0.04f),
                    };
                }
                mesh.Loft(Section.BladeHard(), rings, (_, point, after) => Section.IsFrontBand(point, after) ? colours.Edge : colours.Metal);
            }

            Bit(1f, genes.Part("bit"), 1f);
            bool both = !bardiche && genes.Chance("double", look.Heft * 0.35f + look.Ornament * 0.15f + (two ? 0.2f : 0f));
            if (both) Bit(-1f, genes.Part("bit"), genes.Range("second", 0.85f, 1f));
            else Poll(mesh, look, genes.Part("poll"), y, eye, eyeHeight);

            // The eye, socketed round the haft, swelling where it grips it.
            mesh.At(Matrix4.CreateTranslation(0f, y - eyeHeight * 0.5f + (bardiche ? (upper - beard) * 0.5f : 0f), 0f), () =>
                mesh.Lathe(look.Angular > 0.45f ? Section.Polygon(8) : Section.Round(12), eyeHeight, 3, t => eye * (1f + 0.12f * MathF.Sin(t * MathF.PI)), colours.Metal));

            if (genes.Chance("top.spike", look.Spikes * 0.5f)) Spike(mesh, look, genes.Part("spike"), top, radius);
            if (look.Shines("cheek.stone") || look.Legendary) Cheek(mesh, look, genes.Part("cheek"), new Vector3(bite * 0.42f, y + (upper - beard) * 0.3f, 0f), thick, bite * 0.13f);
        }

        // Behind a one-sided head: a flat hammer face, a spike, or a squared poll.
        private static void Poll(GearMesh mesh, GearLook look, Genes genes, float y, float eye, float height)
        {
            GearColours colours = look.Colours;
            int kind = genes.Pick("kind", 2f, look.Spikes * 3f + (look.Motif == Motif.Fang ? 1.5f : 0f), 1.5f + look.Heft);
            float reach = eye * genes.Range("reach", 0.8f, 2.6f);
            float size = MathF.Min(height * 0.42f, eye * genes.Range("size", 0.75f, 1.1f));
            switch (kind)
            {
                case 1:
                    float spike = eye * genes.Range("spike", 3f, 6f);
                    float droop = genes.Around("droop", 0.12f, 0.12f) * spike;
                    var spine = new List<Vector3>();
                    for (int i = 0; i <= 6; i++) spine.Add(new Vector3(-eye * 0.8f - spike * i / 6f, y - droop * MathF.Pow(i / 6f, 2f), 0f));
                    mesh.Sweep(look.Angular > 0.4f ? Section.Slab(0.3f) : Section.Round(8), spine, t => new Vector2(size, size * 0.75f) * (1f - t * 0.94f), colours.Metal, Vector3.UnitY, capStart: false, capEnd: false);
                    break;
                case 2:
                    mesh.At(Matrix4.CreateRotationZ(MathF.PI / 2f) * Matrix4.CreateTranslation(-eye * 0.7f, y - size, 0f), () => mesh.Block(size, reach, size * 0.85f, 0.3f, colours.Metal));
                    break;
                default:
                    mesh.At(Matrix4.CreateRotationZ(MathF.PI / 2f) * Matrix4.CreateTranslation(-eye * 0.7f, y, 0f), () => mesh.Lathe(look.Angular > 0.5f ? Section.Polygon(8) : Section.Round(10), reach, 3, t => size * (0.9f + 0.18f * t), colours.Metal));
                    break;
            }
        }

        private static void Spike(GearMesh mesh, GearLook look, Genes genes, float y, float radius)
        {
            float reach = radius * genes.Range("reach", 2.5f, 6f);
            mesh.At(Matrix4.CreateTranslation(0f, y, 0f), () => mesh.Lathe(Section.Polygon(genes.Count("sides", 4, 6)), reach, 3, t => radius * 1.1f * (1f - t), look.Colours.Metal, capEnd: false));
        }

        // A stone set into the bit's cheek, on both faces, or inlaid scrolls on a legendary one.
        private static void Cheek(GearMesh mesh, GearLook look, Genes genes, Vector3 at, float thick, float size)
        {
            foreach (float face in new[] { -1f, 1f })
            {
                if (look.Legendary && genes.Chance("scroll", 0.6f))
                {
                    GearKit.Scroll(mesh, at + new Vector3(-size, -size * 0.6f, face * thick * 0.95f), Vector3.UnitX, Vector3.UnitY * face, size * 1.4f, 1.4f, size * 0.12f, look.Colours.Precious);
                }
                else
                {
                    mesh.At(Matrix4.CreateRotationX(face * MathF.PI / 2f) * Matrix4.CreateTranslation(at.X, at.Y, face * thick * 0.7f), () => GearKit.Setting(mesh, genes, size, look.Colours.Precious, look.Theme != GearTheme.None ? look.Colours.Glow : look.Colours.Gem));
                }
            }
        }

        // ---------------------------------------------------------------------------------------------------------------- Hammers

        /// <summary>
        /// A hammer, with one hand or two. Its head is a block across the haft, square, round or cut in facets, with a face to strike with that's flat, crowned or toothed, and a back that's a second
        /// face, a beak or a pick; on top it may carry a spike, and below it a disc to keep the hand off. A maul's head is a great block banded at its ends.
        /// </summary>
        public static void Hammer(GearMesh mesh, GearLook look)
        {
            Genes genes = look.Genes;
            GearForm form = look.Spec.Form;
            GearColours colours = look.Colours;
            bool two = look.Spec.Kind == GearKind.Greathammer;

            float length = genes.Around("length", two ? Scale(2.6f, 3.5f, form.Size) : Scale(1.35f, 1.95f, form.Size), 0.1f);
            float radius = genes.Range("radius", 0.05f, 0.068f) * (0.85f + 0.35f * look.Heft) * (two ? 1.25f : 1f);
            float bottom = two ? -SecondHand - genes.Range("below", 0.2f, 0.34f) : -genes.Range("below.one", 0.2f, 0.3f);
            float top = bottom + length;

            float size = genes.Around("head", (two ? Scale(0.26f, 0.5f, (1f - form.Slender) * 0.6f + form.Size * 0.4f) : Scale(0.15f, 0.26f, 1f - form.Slender)), 0.025f) * (look.Legendary ? 1.12f : 1f);
            bool maul = two && genes.Chance("maul", 0.25f + (1f - form.Slender) * 0.6f);
            float face = size * genes.Range("face", 0.75f, 1.05f);
            float span = size * (maul ? genes.Range("span.maul", 1.6f, 2.3f) : genes.Range("span", 1.1f, 1.8f));
            float y = top - face * genes.Range("sit", 0.55f, 0.75f);

            Haft(mesh, look, genes.Part("haft"), bottom, top - face * 0.2f, radius, two, genes.Chance("langets", 0.45f + look.Craft * 0.4f) ? face * genes.Range("langets", 1.6f, 3f) : 0f);
            Butt(mesh, look, genes.Part("butt"), bottom, radius);

            // The block, lying across the haft along x, its two ends what strikes and what's behind.
            bool round = look.Motif != Motif.Flute && look.Angular <= 0.3f;
            Section block = look.Motif == Motif.Flute ? Section.Fluted(16, 8, 0.12f) : look.Angular > 0.5f ? Section.Slab(genes.Range("bevel", 0.12f, 0.4f)) : !round ? Section.Polygon(8) : Section.Round(14);
            float swell = genes.Range("swell", 0.85f, 1.25f);
            int steps = 8;
            var rings = new Ring[steps + 1];
            float back = maul || genes.Chance("two.faced", 0.35f) ? span : span * genes.Range("back", 0.35f, 0.7f);
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                float x = -back + (span + back) * t;
                float s = t * 2f - 1f;
                float r = face * (1f + (swell - 1f) * (1f - s * s)) * (maul ? 1f : 0.82f + 0.18f * MathF.Abs(s));
                rings[i] = new Ring { Centre = new Vector3(x, y, 0f), Side = Vector3.UnitY, Front = Vector3.UnitZ, HalfWidth = r, HalfDepth = r * genes.Range("depth", 0.8f, 1.02f), Hard = i == 0 || i == steps };
            }
            mesh.Loft(block, rings, colours.Metal);

            // A maul's ends are banded, and a hammer's face is crowned or toothed.
            if (maul || genes.Chance("bands", 0.3f + look.Ornament * 0.4f))
            {
                foreach (float end in new[] { span, -back })
                {
                    float at = end - MathF.Sign(end) * face * 0.25f;
                    mesh.At(Matrix4.CreateRotationZ(-MathF.PI / 2f) * Matrix4.CreateTranslation(at - face * 0.08f, y, 0f), () => mesh.Lathe(round ? Section.Round(14) : Section.Polygon(8), face * 0.16f, 1, _ => face * 1.08f, colours.Fitting));
                }
            }
            Face(mesh, look, genes.Part("face"), new Vector3(span, y, 0f), face);
            if (!maul && back < span * 0.75f) Beak(mesh, look, genes.Part("beak"), new Vector3(-back, y, 0f), face);
            else if (!maul) Face(mesh, look, genes.Part("back.face"), new Vector3(-back, y, 0f), face, -1f);

            if (genes.Chance("top.spike", look.Spikes * 0.7f + (maul ? 0f : 0.15f))) Spike(mesh, look, genes.Part("spike"), y + face * 0.85f, radius * 1.2f);
            if (!maul && genes.Chance("rondel", 0.2f + look.Craft * 0.3f))
            {
                float disc = radius * genes.Range("rondel", 2.4f, 3.4f);
                float at = genes.Range("rondel.at", 0.22f, 0.32f);
                mesh.At(Matrix4.CreateTranslation(0f, at, 0f), () => mesh.Lathe(look.Angular > 0.5f ? Section.Polygon(10) : Section.Round(16), radius * 0.4f, 2, t => disc * (1f - 0.06f * MathF.Abs(t * 2f - 1f)), colours.Fitting, squash: 0.95f));
            }
            if (look.Shines("block.stone") || look.Legendary)
            {
                float stone = face * genes.Range("stone", 0.25f, 0.38f);
                foreach (float side in new[] { -1f, 1f })
                {
                    mesh.At(Matrix4.CreateRotationX(side * MathF.PI / 2f) * Matrix4.CreateTranslation(span * 0.1f, y, side * face * 0.9f), () => GearKit.Setting(mesh, genes.Part("stone"), stone, colours.Precious, look.Theme != GearTheme.None ? colours.Glow : colours.Gem));
                }
            }
        }

        // What strikes: a flat, a dome, or a grid of teeth, on the block's end facing +x (or -x).
        private static void Face(GearMesh mesh, GearLook look, Genes genes, Vector3 at, float size, float way = 1f)
        {
            GearColours colours = look.Colours;
            int kind = genes.Pick("kind", 2f, 1.5f, look.Spikes * 2.5f + look.Heft);
            switch (kind)
            {
                case 1:
                    mesh.At(Matrix4.CreateRotationZ(-way * MathF.PI / 2f) * Matrix4.CreateTranslation(at), () => mesh.Lathe(Section.Round(12), size * genes.Range("crown", 0.15f, 0.3f), 3, t => size * 0.85f * MathF.Sqrt(MathF.Max(1f - t * t, 0.04f)), colours.Edge, capStart: false));
                    break;
                case 2:
                    int teeth = genes.Count("teeth", 2, 4);
                    float tooth = size * 1.5f / teeth;
                    for (int i = 0; i < teeth; i++)
                    {
                        for (int j = 0; j < teeth; j++)
                        {
                            Vector3 place = at + new Vector3(0f, (i + 0.5f - teeth * 0.5f) * tooth, (j + 0.5f - teeth * 0.5f) * tooth);
                            mesh.At(Matrix4.CreateRotationZ(-way * MathF.PI / 2f) * Matrix4.CreateTranslation(place), () => mesh.Lathe(Section.Polygon(4, MathF.PI / 4f), tooth * 0.55f, 1, t => tooth * 0.55f * (1f - t), colours.Edge, capStart: false, capEnd: false));
                        }
                    }
                    break;
                default:
                    mesh.At(Matrix4.CreateRotationZ(-way * MathF.PI / 2f) * Matrix4.CreateTranslation(at), () => mesh.Lathe(look.Angular > 0.5f ? Section.Slab(0.3f) : Section.Round(12), size * 0.06f, 1, _ => size * 0.92f, colours.Edge, capStart: false));
                    break;
            }
        }

        // A beak behind the head, curved down to a point, for punching through plate.
        private static void Beak(GearMesh mesh, GearLook look, Genes genes, Vector3 at, float size)
        {
            float reach = size * genes.Range("reach", 2.2f, 4.2f);
            float droop = reach * genes.Range("droop", 0.1f, 0.4f);
            var spine = new List<Vector3>();
            for (int i = 0; i <= 8; i++)
            {
                float t = i / 8f;
                spine.Add(at + new Vector3(-reach * t, -droop * t * t, 0f));
            }
            mesh.Sweep(look.Angular > 0.4f ? Section.Polygon(4, MathF.PI / 4f) : Section.Super(8, 2.5f), spine, t => new Vector2(size * 0.62f, size * 0.42f) * (1f - t * 0.95f), look.Colours.Metal, Vector3.UnitY, capStart: false, capEnd: false);
        }

        // ---------------------------------------------------------------------------------------------------------------- Javelins

        /// <summary>
        /// A javelin, held at its balance in one hand and thrown point first: a head on a socket, a leaf of a blade, a winged blade with lugs below it, or a pilum's long iron shank with a small hard
        /// point to it; a long ash shaft with a thong at its balance for throwing, and a small iron butt.
        /// </summary>
        public static void Spear(GearMesh mesh, GearLook look)
        {
            Genes genes = look.Genes;
            GearForm form = look.Spec.Form;
            GearColours colours = look.Colours;

            float length = genes.Around("length", Scale(3.4f, 4.4f, form.Size), 0.15f);
            float radius = genes.Range("radius", 0.04f, 0.054f) * (0.85f + 0.3f * look.Heft);
            float balance = genes.Range("balance", 0.4f, 0.46f);
            float bottom = -length * balance;
            float top = bottom + length;

            Haft(mesh, look, genes.Part("haft"), bottom, top, radius, false);
            Thong(mesh, look, genes.Part("thong"), radius);
            Butt(mesh, look, genes.Part("butt"), bottom, radius * 0.9f);

            int kind = genes.Pick("head", 3f + form.Cover, form.Cover * 4f, (form.Slender - 0.4f) * 6f);
            float socket = genes.Range("socket", 0.22f, 0.42f);
            mesh.At(Matrix4.CreateTranslation(0f, top - socket * 0.5f, 0f), () => mesh.Lathe(look.Angular > 0.5f ? Section.Polygon(8) : Section.Round(10), socket, 4, t => radius * (1.35f - 0.45f * t), colours.Metal));
            float headBase = top + socket * 0.5f;
            if (kind == 2)
            {
                // A pilum: a long thin shank, square, and a small pyramid of a point.
                float shank = genes.Range("shank", 0.7f, 1.3f) * (0.8f + 0.4f * form.Size);
                mesh.At(Matrix4.CreateTranslation(0f, headBase, 0f), () => mesh.Lathe(Section.Polygon(4, MathF.PI / 4f), shank, 2, t => radius * (0.42f - 0.12f * t), colours.MetalDark));
                float point = radius * genes.Range("point", 2.6f, 4f);
                mesh.At(Matrix4.CreateTranslation(0f, headBase + shank, 0f), () => mesh.Lathe(Section.Polygon(genes.Chance("point.three", 0.4f) ? 3 : 4), point, 3, t => radius * 0.9f * (1f - t) * (1f + 0.4f * MathF.Sin(t * MathF.PI)), colours.Edge, capEnd: false));
            }
            else
            {
                var blade = new BladeShape
                {
                    Length = genes.Around("blade.length", Scale(0.45f, 0.95f, form.Size * 0.5f + form.Slender * 0.5f), 0.06f),
                    Width = genes.Around("blade.width", Scale(0.12f, 0.06f, form.Slender), 0.015f) * (look.Legendary ? 1.15f : 1f),
                    BaseWidth = genes.Range("base", 0.45f, 0.75f),
                    Belly = genes.Range("belly", 0.3f, 0.55f),
                    BellyWidth = 1f,
                    MidWidth = genes.Range("mid", 0.75f, 0.95f),
                    Shoulder = genes.Range("shoulder", 0.55f, 0.85f),
                    TipStart = genes.Range("tip", 0.6f, 0.78f),
                    TipRound = genes.Range("tip.round", 0.1f, 0.5f),
                    TipNeedle = genes.Range("needle", 0.9f, 1.4f),
                    Flat = genes.Range("flat", 0.02f, 0.25f),
                    EdgeBand = genes.Range("band", 0.15f, 0.3f),
                };
                blade.Thick = blade.Width * genes.Range("thick", 0.24f, 0.36f);
                blade.Runes = look.Shines("runes");
                blade.Fuller = blade.Runes ? 0.3f : 0f;
                blade.FullerWidth = 0.6f;
                blade.FullerFrom = 0.08f;
                blade.FullerTo = 0.55f;
                blade.RuneMaterial = look.Theme != GearTheme.None || look.Legendary ? colours.Glow : colours.Precious;
                SwordGenerator.Blade(mesh, look, genes.Part("blade"), blade, headBase);

                if (kind == 1)
                {
                    // Wings below the head, which a beast can't run up the shaft past.
                    float reach = blade.Width * genes.Range("wing.reach", 1.3f, 2.4f);
                    float rise = reach * genes.Around("wing.rise", 0.25f, 0.3f);
                    float thick = radius * genes.Range("wing.thick", 0.32f, 0.55f);
                    float at = top + socket * 0.1f;
                    mesh.BothSides(() =>
                    {
                        var keys = new[] { new Vector3(radius * 0.9f, at, 0f), new Vector3(reach * 0.5f, at + rise * 0.3f, 0f), new Vector3(reach, at + rise, 0f) };
                        mesh.Sweep(Section.Slab(0.4f), Spine.Through(keys, 6), t => new Vector2(thick * 1.4f, thick) * (1f - t * 0.6f), colours.Metal, Vector3.UnitZ);
                        GearKit.Tip(mesh, look, genes.Part("wing.tip"), new Vector3(reach, at + rise, 0f), new Vector3(1f, 0.4f, 0f), Vector3.UnitZ, thick * 1.1f, colours.Metal);
                    });
                }
            }

            if (look.Ornament > 0.6f && genes.Chance("tassel", look.Ornament * 0.6f) || look.Legendary)
            {
                GearKit.Tassel(mesh, genes.Part("tassel"), new Vector3(0f, top - socket, 0f), genes.Range("tassel.length", 0.25f, 0.45f), radius * 0.9f, colours.Trim, colours.Fitting);
            }
        }

        // The thong a javelin is thrown with: a loop of leather bound round its balance, hanging free.
        private static void Thong(GearMesh mesh, GearLook look, Genes genes, float radius)
        {
            GearColours colours = look.Colours;
            float bind = genes.Range("bind", 0.1f, 0.16f);
            mesh.At(Matrix4.CreateTranslation(0f, -bind * 0.5f, 0f), () => mesh.Lathe(Section.Round(10), bind, 4, t => radius * (1.18f + 0.06f * MathF.Sin(t * MathF.Tau * 3f)), colours.Strap, capStart: true, capEnd: true));
            float loop = genes.Range("loop", 0.14f, 0.24f);
            float side = genes.Sign("side");
            var keys = new[] { new Vector3(radius * side, 0f, 0f), new Vector3((radius + loop * 0.4f) * side, -loop * 0.4f, loop * 0.25f), new Vector3((radius + loop * 0.2f) * side, -loop, loop * 0.1f), new Vector3(radius * side, -bind * 0.3f, -radius * 0.5f) };
            mesh.Sweep(Section.Slab(0.3f), Spine.Through(keys, 10), _ => new Vector2(radius * 0.32f, radius * 0.1f), colours.Strap, Vector3.UnitZ);
        }
    }
}

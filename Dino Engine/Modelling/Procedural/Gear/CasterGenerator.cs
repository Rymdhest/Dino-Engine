using Dino_Engine.Modelling.Model;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>
    /// What a caster carries: a staff, held in one hand as a walking staff is though it takes both, and a wand, short and fine, or a sceptre of metal. Built standing up with the hand at the origin and
    /// the head up +y. What matters on these is what's held up at the top, and every head is one of a few ways of holding something up, drawn: a cage of claws round a stone, a crook curling over with
    /// something hung from it, a branch that forks to cradle it, a ring, a turned head, or a crown of horn. See GEAR.md.
    /// </summary>
    public static class CasterGenerator
    {
        private static float Scale(float low, float high, float t) => low + (high - low) * t;

        public static void Staff(GearMesh mesh, GearLook look)
        {
            Genes genes = look.Genes;
            GearForm form = look.Spec.Form;
            GearColours colours = look.Colours;

            float length = genes.Around("length", Scale(3.9f, 5.1f, form.Size), 0.18f);
            float radius = genes.Range("radius", 0.052f, 0.072f) * (0.85f + 0.4f * (1f - form.Slender));
            float bottom = -length * genes.Range("hold", 0.34f, 0.4f);
            float top = bottom + length;
            float gnarl = genes.Range("gnarl", 0f, 1f) * (1f - look.Craft * 0.8f);
            int head = genes.Pick("head", 3f, 1.5f + look.Curl * 2f, 1f + gnarl * 3f, 1f + look.Ornament, 1f + look.Heavy * 2f + look.Angular, look.Motif == Motif.Fang ? 3f : 0.3f);

            // A forked head grows out of the shaft, so the shaft stops below the fork; a crook curls over from the top.
            Shaft(mesh, look, genes.Part("shaft"), bottom, top, radius, gnarl);
            Foot(mesh, look, genes.Part("foot"), bottom, radius);
            float size = Scale(0.28f, 0.62f, form.Cover * 0.5f + form.Size * 0.5f) * (look.Legendary ? 1.15f : 1f);
            switch (head)
            {
                case 1: Crook(mesh, look, genes.Part("crook"), top, radius, size); break;
                case 2: Fork(mesh, look, genes.Part("fork"), top, radius, size); break;
                case 3: Hoop(mesh, look, genes.Part("hoop"), top, radius, size); break;
                case 4: Turned(mesh, look, genes.Part("turned"), top, radius, size); break;
                case 5: Antlers(mesh, look, genes.Part("antlers"), top, radius, size); break;
                default: Cage(mesh, look, genes.Part("cage"), top, radius, size); break;
            }
            if (genes.Chance("charms", look.Ornament * 0.6f + (look.Motif == Motif.Fang ? 0.3f : 0f)) || look.Legendary) Charms(mesh, look, genes.Part("charms"), top - size * 0.25f, radius);
        }

        public static void Wand(GearMesh mesh, GearLook look)
        {
            Genes genes = look.Genes;
            GearForm form = look.Spec.Form;
            GearColours colours = look.Colours;

            float length = genes.Around("length", Scale(1.15f, 1.9f, form.Size), 0.08f);
            float radius = genes.Range("radius", 0.034f, 0.05f) * (0.85f + 0.45f * (1f - form.Slender));
            float bottom = -length * 0.24f;
            float top = bottom + length;
            float curl = form.Curve * genes.Around("curl", 0.1f, 0.08f);

            // A sceptre is metal, flanged at its head; anything else is carved from wood or bone.
            bool sceptre = genes.Chance("sceptre", form.Fancy * form.Cover * 0.9f + look.Heavy * 0.2f);
            bool bone = !sceptre && genes.Chance("bone", 0.3f + (look.Motif == Motif.Fang ? 0.3f : 0f));
            VertexMaterial body = sceptre ? colours.Fitting : bone ? colours.Bone : colours.Wood;
            int knuckles = genes.Count("knuckles", 2, 6);
            float knuckle = bone ? genes.Range("knuckle.bone", 0.18f, 0.35f) : genes.Range("knuckle", 0f, 0.22f);
            bool spiral = !sceptre && !bone && genes.Chance("spiral", 0.25f + look.Craft * 0.3f);

            int steps = 26;
            var rings = new Ring[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                float r = radius * (1.15f - 0.55f * t) * (1f + knuckle * MathF.Pow(MathF.Abs(MathF.Sin(t * knuckles * MathF.PI)), 4f));
                Vector2[]? shape = null;
                if (spiral)
                {
                    shape = new Vector2[12];
                    for (int k = 0; k < 12; k++)
                    {
                        float angle = k * MathF.Tau / 12f;
                        float groove = 1f - 0.12f * MathF.Pow(0.5f + 0.5f * MathF.Cos(angle * 3f + t * 18f), 4f);
                        shape[k] = new Vector2(MathF.Cos(angle) * groove, MathF.Sin(angle) * groove);
                    }
                }
                rings[i] = new Ring { Centre = new Vector3(curl * length * t * t * MathF.Sin(t * 2f), bottom + t * length, 0f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = r, HalfDepth = r, Shape = shape };
            }
            mesh.Loft(sceptre && look.Angular > 0.5f ? Section.Polygon(8) : Section.Round(12), rings, body);

            // A grip of cord or leather where the hand goes.
            if (!sceptre && genes.Chance("wrapped", 0.6f))
            {
                float from = bottom + length * 0.02f, to = genes.Range("wrap.to", 0.14f, 0.24f);
                mesh.Wrap(from, to, t => radius * (1.15f - 0.55f * (from + (to - from) * t - bottom) / length) * 1.08f, genes.Range("pitch", 0.025f, 0.04f), 0.12f, colours.Strap);
            }

            Vector3 tip = rings[steps].Centre;
            float headSize = Scale(0.12f, 0.3f, form.Cover) * (look.Legendary ? 1.2f : 1f);
            mesh.At(Matrix4.CreateTranslation(tip.X, 0f, 0f), () =>
            {
                if (sceptre) Flanged(mesh, look, genes.Part("flanged"), top, radius * 0.8f, headSize);
                else Cage(mesh, look, genes.Part("cage"), top, radius * 0.6f, headSize);
            });
            mesh.At(Matrix4.CreateTranslation(0f, bottom - radius * 2.2f, 0f), () => GearKit.Terminal(mesh, look, genes.Part("pommel"), radius * 2.4f, radius * 1.7f, colours.Fitting));
        }

        // A staff's shaft: turned straight, gnarled as it grew, twisted, or carved in rings, with a wrap where the hand goes.
        private static void Shaft(GearMesh mesh, GearLook look, Genes genes, float bottom, float top, float radius, float gnarl)
        {
            GearColours colours = look.Colours;
            float length = top - bottom;
            float taper = genes.Range("taper", 0.72f, 0.95f);
            float bend = gnarl * genes.Around("bend", 0f, 0.05f) * length;
            float lumps = genes.Range("lumps", 3f, 9f);
            float lump = gnarl * genes.Range("lump.size", 0.1f, 0.24f);
            float phase = genes.Range("phase", 0f, MathF.Tau);
            int carving = genes.Pick("carving", 3f, 1f + look.Craft * 2f, 1f + look.Craft * 1.5f);
            int steps = Math.Clamp((int)(length * 9f), 20, 60);
            var rings = new Ring[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                float y = bottom + t * length;
                float ends = MathF.Sin(t * MathF.PI);
                float r = radius * (1f + (taper - 1f) * t) * (1f + lump * MathF.Sin(t * lumps * MathF.Tau + phase) * ends);
                Vector2[]? shape = null;
                if (carving == 1)
                {
                    // Twisted, as a vine round it.
                    shape = new Vector2[12];
                    for (int k = 0; k < 12; k++)
                    {
                        float angle = k * MathF.Tau / 12f;
                        float groove = 1f - 0.1f * MathF.Pow(0.5f + 0.5f * MathF.Cos(angle * 2f + t * length * 9f), 3f);
                        shape[k] = new Vector2(MathF.Cos(angle) * groove, MathF.Sin(angle) * groove);
                    }
                }
                else if (carving == 2) r *= 1f + 0.08f * MathF.Pow(MathF.Abs(MathF.Sin(t * length * 3.2f * MathF.PI)), 12f);
                rings[i] = new Ring { Centre = new Vector3(bend * ends * ends, y, gnarl * bend * 0.6f * MathF.Sin(t * MathF.PI * 2f)), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = r, HalfDepth = r, Shape = shape };
            }
            mesh.Loft(Section.Round(12), rings, colours.Wood);

            if (genes.Chance("wrapped", 0.55f + look.Cloth * 0.3f))
            {
                float from = -genes.Range("wrap.below", 0.22f, 0.32f), to = genes.Range("wrap.above", 0.22f, 0.32f);
                mesh.Wrap(from, to, t => radius * (1f + (taper - 1f) * (from + (to - from) * t - bottom) / length) * 1.08f, genes.Range("pitch", 0.04f, 0.065f), 0.12f, look.Cloth > 0.5f && genes.Chance("cloth", 0.6f) ? colours.Trim : colours.Strap);
            }
            int bands = genes.Count("bands", 0, 1 + (int)(look.Ornament * 3f));
            for (int i = 0; i < bands; i++)
            {
                float at = top - length * genes.Range($"band{i}", 0.05f, 0.25f);
                float r = radius * (1f + (taper - 1f) * (at - bottom) / length) * 1.2f;
                mesh.At(Matrix4.CreateTranslation(0f, at - radius * 0.5f, 0f), () => mesh.Lathe(Section.Round(10), radius, 2, t => r * (1f + 0.1f * MathF.Sin(t * MathF.PI)), colours.Fitting));
            }
        }

        private static void Foot(GearMesh mesh, GearLook look, Genes genes, float y, float radius)
        {
            float height = radius * genes.Range("height", 2f, 3.6f);
            mesh.At(Matrix4.CreateTranslation(0f, y - height * 0.8f, 0f), () => mesh.Lathe(look.Angular > 0.5f ? Section.Polygon(8) : Section.Round(10), height, 3, t => radius * (0.85f + 0.35f * t), look.Colours.Fitting));
        }

        private static VertexMaterial StoneOf(GearLook look, string name) => look.Theme != GearTheme.None || look.Shines(name) ? look.Colours.Glow : look.Colours.Gem;

        // Claws round a stone, which a staff or a wand holds up: metal, or wood grown into shape, closing over it or opening round it, twisted as the motif likes.
        private static void Cage(GearMesh mesh, GearLook look, Genes genes, float y, float radius, float size)
        {
            GearColours colours = look.Colours;
            float stone = size * genes.Range("stone", 0.3f, 0.48f);
            float above = size * genes.Range("above", 0.55f, 0.85f);
            int claws = genes.Count("claws", 3, 6);
            float spread = size * genes.Range("spread", 0.42f, 0.7f);
            bool wooden = genes.Chance("wooden", 0.3f + (1f - look.Craft) * 0.3f);
            VertexMaterial claw = wooden ? colours.Wood : colours.Fitting;
            float twist = genes.Range("twist", -0.8f, 0.8f) * (look.Motif == Motif.Scroll || look.Motif == Motif.Rope ? 1.4f : 0.6f);
            float rise = size * genes.Range("rise", 1f, 1.5f);
            float close = genes.Range("close", 0.15f, 0.6f);
            float thick = radius * genes.Range("thick", 0.4f, 0.75f);
            Vector3 Out(float a) => new Vector3(MathF.Cos(a), 0f, MathF.Sin(a));
            mesh.At(Matrix4.CreateTranslation(0f, y - radius, 0f), () => mesh.Lathe(Section.Round(10), radius * 1.6f, 3, t => radius * (1.05f + 0.35f * t), claw));
            for (int i = 0; i < claws; i++)
            {
                float angle = i * (MathF.Tau / claws);
                var keys = new[]
                {
                    new Vector3(0f, y, 0f) + Out(angle) * radius * 0.7f,
                    new Vector3(0f, y + rise * 0.3f, 0f) + Out(angle + twist * 0.4f) * spread,
                    new Vector3(0f, y + rise * 0.7f, 0f) + Out(angle + twist) * spread * 1.02f,
                    new Vector3(0f, y + rise, 0f) + Out(angle + twist * 1.5f) * spread * close,
                };
                mesh.Sweep(look.Motif == Motif.Facet ? Section.Polygon(4) : Section.Round(6), Spine.Through(keys, 10), t => new Vector2(thick * (1f - t * 0.8f)), claw, Vector3.UnitZ, capEnd: true);
                if (look.Motif == Motif.Scroll && genes.Chance($"curl{i}", 0.6f))
                {
                    Vector3 end = keys[^1];
                    GearKit.Scroll(mesh, end, Out(angle + twist * 1.5f) * -1f, Vector3.UnitY, thick * 2.2f, 1.2f, thick * 0.4f, claw);
                }
            }
            Vector3 heart = new Vector3(0f, y + above, 0f);
            mesh.At(Matrix4.CreateTranslation(heart), () => mesh.Gem(genes.Count("facets", 5, 9), stone, stone * genes.Range("crown", 0.8f, 1.4f), stone * genes.Range("pavilion", 0.8f, 1.3f), genes.Range("table", 0.2f, 0.5f), StoneOf(look, "cage.lit")));
            if (look.Legendary) Halo(mesh, look, genes.Part("halo"), heart, spread * 1.25f, thick * 0.35f);
        }

        // The shaft curls over at the top into a crook, and something hangs from its end: a stone in a cage, or a small lantern of it.
        private static void Crook(GearMesh mesh, GearLook look, Genes genes, float y, float radius, float size)
        {
            GearColours colours = look.Colours;
            float reach = size * genes.Range("reach", 0.7f, 1.1f);
            float turns = genes.Range("turns", 0.55f, 0.85f);
            var spine = new List<Vector3>();
            for (int i = 0; i <= 14; i++)
            {
                float t = i / 14f;
                float angle = t * turns * MathF.Tau;
                float r = reach * (1f - 0.25f * t);
                spine.Add(new Vector3(-r + MathF.Cos(angle) * r, y + MathF.Sin(angle) * r, 0f));
            }
            mesh.Sweep(Section.Round(10), spine, t => new Vector2(radius * (1f - 0.45f * t)), colours.Wood, Vector3.UnitZ, capStart: false, capEnd: true);
            Vector3 end = spine[^1];
            float drop = size * genes.Range("drop", 0.3f, 0.55f);
            mesh.Sweep(Section.Round(4), new[] { end, end - Vector3.UnitY * drop }, _ => new Vector2(radius * 0.18f), colours.Fitting, Vector3.UnitZ);
            float stone = size * genes.Range("stone", 0.2f, 0.32f);
            Vector3 hung = end - Vector3.UnitY * (drop + stone);
            int bars = genes.Count("bars", 3, 5);
            for (int i = 0; i < bars; i++)
            {
                float angle = i * MathF.Tau / bars;
                Vector3 out_ = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                var keys = new[] { hung + Vector3.UnitY * stone * 1.2f, hung + out_ * stone * 1.15f, hung - Vector3.UnitY * stone * 1.2f };
                mesh.Sweep(Section.Round(4), Spine.Through(keys, 6), _ => new Vector2(radius * 0.14f), colours.Fitting, Vector3.UnitZ);
            }
            mesh.At(Matrix4.CreateTranslation(hung), () => mesh.Gem(genes.Count("facets", 6, 9), stone, stone, stone, 0.35f, StoneOf(look, "crook.lit")));
        }

        // The shaft parts into branches, which grow up and round a stone and hold it among them.
        private static void Fork(GearMesh mesh, GearLook look, Genes genes, float y, float radius, float size)
        {
            GearColours colours = look.Colours;
            int branches = genes.Count("branches", 2, 4);
            float rise = size * genes.Range("rise", 1.1f, 1.6f);
            float spread = size * genes.Range("spread", 0.5f, 0.85f);
            float stone = size * genes.Range("stone", 0.28f, 0.42f);
            float turn = genes.Range("turn", 0f, MathF.Tau);
            for (int i = 0; i < branches; i++)
            {
                float angle = turn + i * MathF.Tau / branches;
                Vector3 out_ = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                var keys = new[] { new Vector3(0f, y - radius, 0f), new Vector3(0f, y + rise * 0.35f, 0f) + out_ * spread * 0.8f, new Vector3(0f, y + rise * 0.75f, 0f) + out_ * spread, new Vector3(0f, y + rise, 0f) + out_ * spread * genes.Range($"close{i}", 0.2f, 0.55f) };
                mesh.Sweep(Section.Round(8), Spine.Through(keys, 12), t => new Vector2(radius * (0.85f - 0.65f * t)), colours.Wood, Vector3.UnitZ, capStart: false, capEnd: true);
            }
            mesh.At(Matrix4.CreateTranslation(0f, y + rise * 0.62f, 0f), () => mesh.Gem(genes.Count("facets", 5, 8), stone, stone * 1.2f, stone * 1.1f, 0.3f, StoneOf(look, "fork.lit")));
            if (look.Ornament > 0.4f)
            {
                float band = rise * 0.05f;
                mesh.At(Matrix4.CreateTranslation(0f, y - radius * 2f, 0f), () => mesh.Lathe(Section.Round(10), band * 2f, 2, _ => radius * 1.25f, colours.Fitting));
            }
        }

        // A ring at the top with the stone held in its middle, and smaller rings hung loose on it to chime.
        private static void Hoop(GearMesh mesh, GearLook look, Genes genes, float y, float radius, float size)
        {
            GearColours colours = look.Colours;
            float hoop = size * genes.Range("hoop", 0.6f, 0.9f);
            float wire = radius * genes.Range("wire", 0.35f, 0.55f);
            Vector3 heart = new Vector3(0f, y + hoop, 0f);
            mesh.At(Matrix4.CreateRotationX(MathF.PI / 2f) * Matrix4.CreateTranslation(heart), () => mesh.Hoop(look.Angular > 0.5f ? Section.Slab(0.3f) : Section.Round(8), hoop, wire, wire, 24, colours.Fitting));
            mesh.Sweep(Section.Round(6), new[] { new Vector3(0f, y - radius * 1.5f, 0f), heart - Vector3.UnitY * (hoop - wire * 0.5f) }, _ => new Vector2(wire * 1.2f), colours.Fitting, Vector3.UnitZ);
            float stone = hoop * genes.Range("stone", 0.32f, 0.5f);
            mesh.Sweep(Section.Round(4), new[] { heart + Vector3.UnitY * hoop, heart + Vector3.UnitY * stone }, _ => new Vector2(wire * 0.5f), colours.Fitting, Vector3.UnitZ);
            mesh.Sweep(Section.Round(4), new[] { heart - Vector3.UnitY * hoop, heart - Vector3.UnitY * stone }, _ => new Vector2(wire * 0.5f), colours.Fitting, Vector3.UnitZ);
            mesh.At(Matrix4.CreateTranslation(heart), () => mesh.Gem(genes.Count("facets", 6, 10), stone, stone, stone, 0.3f, StoneOf(look, "hoop.lit")));
            int chimes = genes.Count("chimes", 2, 5);
            for (int i = 0; i < chimes; i++)
            {
                float angle = MathF.PI * 0.15f + MathF.PI * 0.7f * i / MathF.Max(chimes - 1, 1);
                Vector3 at = heart + new Vector3(MathF.Cos(angle) * hoop, -MathF.Sin(angle) * hoop, 0f);
                float ring = hoop * genes.Range($"chime{i}", 0.14f, 0.22f);
                mesh.At(Matrix4.CreateTranslation(at - Vector3.UnitY * ring), () => mesh.Hoop(Section.Round(5), ring, wire * 0.35f, wire * 0.35f, 12, colours.Fitting));
            }
        }

        // A turned head on a plain shaft, banded and set with a stone, as a mace's head is made, for the one who'd rather it could hit.
        private static void Turned(GearMesh mesh, GearLook look, Genes genes, float y, float radius, float size)
        {
            float height = size * genes.Range("height", 0.9f, 1.4f);
            mesh.At(Matrix4.CreateTranslation(0f, y - radius, 0f), () => GearKit.Terminal(mesh, look, genes.Part("head"), height, size * 0.5f, look.Colours.Fitting));
            float stone = size * genes.Range("stone", 0.15f, 0.22f);
            float at = y - radius + height * 0.5f;
            int stones = genes.Count("stones", 1, 4);
            for (int i = 0; i < stones; i++)
            {
                float angle = i * MathF.Tau / stones + MathF.PI / 2f;
                Vector3 out_ = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                mesh.At(GearMesh.FacingOut(new Vector3(0f, at, 0f) + out_ * size * 0.42f, out_), () => GearKit.Setting(mesh, genes.Part($"stone{i}"), stone, look.Colours.Precious, StoneOf(look, "turned.lit")));
            }
        }

        // Tines of horn or bone rising round the top, as a beast's crown is, cradling a stone.
        private static void Antlers(GearMesh mesh, GearLook look, Genes genes, float y, float radius, float size)
        {
            GearColours colours = look.Colours;
            int tines = genes.Count("tines", 3, 6);
            VertexMaterial horn = genes.Chance("horn", 0.5f) ? colours.Horn : colours.Bone;
            for (int i = 0; i < tines; i++)
            {
                float angle = i * MathF.Tau / tines + genes.Range($"t{i}", -0.2f, 0.2f);
                Vector3 out_ = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                Vector3 side = Vector3.Cross(Vector3.UnitY, out_);
                float length = size * genes.Range($"l{i}", 0.9f, 1.4f);
                mesh.At(GearMesh.Basis(new Vector3(0f, y - radius, 0f) + out_ * radius * 0.6f, side, Vector3.Normalize(Vector3.UnitY * 1.6f + out_), Vector3.Cross(side, Vector3.Normalize(Vector3.UnitY * 1.6f + out_))), () => GearKit.Fang(mesh, length, radius * 0.55f, -1f, horn));
            }
            float stone = size * genes.Range("stone", 0.22f, 0.32f);
            mesh.At(Matrix4.CreateTranslation(0f, y + size * 0.35f, 0f), () => mesh.Gem(genes.Count("facets", 5, 8), stone, stone, stone * 1.2f, 0.35f, StoneOf(look, "antler.lit")));
            mesh.At(Matrix4.CreateTranslation(0f, y - radius * 1.5f, 0f), () => GearKit.Twist(mesh, radius * 1.15f, radius * 0.22f, 10, colours.Strap));
        }

        // A sceptre's head: flanges of metal standing out round a core, a stone on top.
        private static void Flanged(GearMesh mesh, GearLook look, Genes genes, float y, float radius, float size)
        {
            GearColours colours = look.Colours;
            int flanges = genes.Count("flanges", 4, 8);
            float height = size * genes.Range("height", 1f, 1.5f);
            float reach = size * genes.Range("reach", 0.35f, 0.55f);
            mesh.At(Matrix4.CreateTranslation(0f, y - height * 0.2f, 0f), () => mesh.Lathe(Section.Round(10), height, 4, t => radius * (1.3f + 0.6f * MathF.Sin(t * MathF.PI)), colours.Fitting));
            for (int i = 0; i < flanges; i++)
            {
                float angle = i * MathF.Tau / flanges;
                Vector3 out_ = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                Vector3 side = Vector3.Cross(Vector3.UnitY, out_);
                var rings = new[]
                {
                    new Ring { Centre = new Vector3(0f, y - height * 0.15f, 0f) + out_ * radius, Side = out_, Front = side, HalfWidth = reach * 0.15f, HalfDepth = radius * 0.22f, Hard = true },
                    new Ring { Centre = new Vector3(0f, y + height * 0.4f, 0f) + out_ * (radius + reach * 0.5f), Side = out_, Front = side, HalfWidth = reach * 0.55f, HalfDepth = radius * 0.25f },
                    new Ring { Centre = new Vector3(0f, y + height * 0.8f, 0f) + out_ * radius, Side = out_, Front = side, HalfWidth = reach * 0.15f, HalfDepth = radius * 0.18f, Hard = true },
                };
                mesh.Loft(Section.Slab(0.35f), rings, colours.Fitting);
            }
            float stone = size * genes.Range("stone", 0.25f, 0.4f);
            mesh.At(Matrix4.CreateTranslation(0f, y + height * 0.85f, 0f), () => GearKit.Setting(mesh, genes.Part("stone"), stone, colours.Precious, StoneOf(look, "sceptre.lit")));
        }

        // Cords hanging from below the head with beads, and a tooth or a quill on each for the trophy-takers.
        private static void Charms(GearMesh mesh, GearLook look, Genes genes, float y, float radius)
        {
            GearColours colours = look.Colours;
            int cords = genes.Count("cords", 1, 3);
            for (int i = 0; i < cords; i++)
            {
                float angle = genes.Range($"a{i}", 0f, MathF.Tau);
                Vector3 out_ = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                float drop = genes.Range($"d{i}", 0.25f, 0.55f);
                Vector3 from = new Vector3(0f, y, 0f) + out_ * radius;
                Vector3 to = from + out_ * radius * 1.5f - Vector3.UnitY * drop;
                mesh.Sweep(Section.Round(4), Spine.Through(new[] { from, Vector3.Lerp(from, to, 0.5f) + out_ * radius * 0.6f, to }, 6), _ => new Vector2(radius * 0.12f), colours.Strap, Vector3.UnitZ);
                if (look.Motif == Motif.Fang || genes.Chance($"tooth{i}", 0.3f)) GearKit.Trophy(mesh, genes.Part($"trophy{i}"), to, radius * genes.Range($"ts{i}", 2.2f, 3.4f), colours.Bone, colours.Strap);
                else mesh.At(Matrix4.CreateTranslation(to - Vector3.UnitY * radius * 0.6f), () => mesh.Lathe(Section.Round(8), radius * 1.2f, 4, t => radius * 0.6f * MathF.Sin(MathF.Max(t, 0.05f) * MathF.PI), genes.Chance($"bead{i}", 0.5f) ? colours.Gem : colours.Fitting));
            }
        }

        // A thin ring of light round a legendary head, tilted as it hangs.
        internal static void Halo(GearMesh mesh, GearLook look, Genes genes, Vector3 heart, float radius, float wire)
        {
            mesh.At(Matrix4.CreateRotationX(genes.Range("tilt", 1.1f, 1.45f)) * Matrix4.CreateRotationZ(genes.Range("lean", -0.3f, 0.3f)) * Matrix4.CreateTranslation(heart), () => mesh.Hoop(Section.Round(6), radius, wire, wire, 32, look.Colours.Glow));
        }
    }
}

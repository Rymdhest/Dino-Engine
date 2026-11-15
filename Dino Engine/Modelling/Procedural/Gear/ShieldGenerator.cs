using Dino_Engine.Modelling.Model;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>
    /// What's in the other hand. A shield is a width drawn down its height and a bow across it, so a buckler, a round shield, a heater and a tower are places in one space, and how it's made
    /// decides its face: boards painted in two tinctures, leather over wood studded in rings, or a face of steel. Heraldry is laid on as fields of its own with exact edges, the way paint over
    /// boards reads, never as colour smeared across a mesh, and keeps to the old rule of a light tincture against a dark. It never makes a cross, never puts white and red together and never wears
    /// the Reich's colours (LORE.md); its charges are a Defender's: a beast's fang, claws, a lozenge, a ring. A caster's ward isn't a shield at all (<see cref="Ward"/>). A shield is built facing +z
    /// with the middle of the fist that holds it at the origin and the forearm along +y, its board <see cref="GearFit.ShieldStandoff"/> in front of them; a ward stands up y out of the fist, as a
    /// weapon does. See GEAR.md.
    /// </summary>
    public static class ShieldGenerator
    {
        private enum Face { Boards, Leather, Steel }

        // What a shield can be painted with: each light or dark, so that a pair is always one of each and reads from far off. No black, which is the Reich's, and the red is a bright scarlet,
        // as a deep red is its oxblood.
        private static readonly (string Name, Vector3 Albedo, bool Light)[] Tinctures =
        {
            ("white", new Vector3(0.52f, 0.5f, 0.44f), true),
            ("gold", new Vector3(0.58f, 0.44f, 0.13f), true),
            ("ochre", new Vector3(0.47f, 0.32f, 0.12f), true),
            ("red", new Vector3(0.58f, 0.12f, 0.06f), false),
            ("blue", new Vector3(0.11f, 0.19f, 0.4f), false),
            ("green", new Vector3(0.1f, 0.24f, 0.11f), false),
            ("russet", new Vector3(0.4f, 0.2f, 0.1f), false),
            ("purple", new Vector3(0.24f, 0.07f, 0.34f), false),
        };

        public static void Build(GearMesh mesh, GearLook look)
        {
            if (IsWard(look.Spec)) Ward(mesh, look);
            else Shield(mesh, look);
        }

        /// <summary>Whether what's in the other hand is a caster's ward, which is held up out of the fist like a weapon, rather than a shield on the forearm.</summary>
        public static bool IsWard(GearSpec spec)
        {
            GearMake make = spec.Form.Make;
            float total = spec.Heavy + spec.Leather + spec.Cloth;
            return make is GearMake.Silk or GearMake.Wraps or GearMake.Cloth || make == GearMake.Any && total > 0f && spec.Cloth / total > 0.5f;
        }

        private static void Shield(GearMesh mesh, GearLook look)
        {
            var kit = new ArmourKit(look);
            Genes genes = look.Genes;
            GearForm form = look.Spec.Form;
            GearColours c = look.Colours;
            Face face = kit.Make switch
            {
                GearMake.Plate => Face.Steel,
                GearMake.Hide or GearMake.Hardened or GearMake.Studded => Face.Leather,
                _ => Face.Boards,
            };

            // How tall and how wide, and what the outline does on the way down: a widest point and a fall either side of it. A fall of 1/2 is a circle's edge, nearer nothing a flat end, and above 1
            // it runs in to a point.
            float wide = genes.Around("wide", 0.62f + form.Size * 0.42f + (1f - form.Slender) * 0.14f, 0.09f);
            float tall = wide * 2f * genes.Around("tall", 0.95f + form.Size * 0.75f, 0.19f);
            float belly = genes.Around("belly", 0.6f, 0.13f);
            float fallBelow = MathF.Pow(2f, genes.Around("fall.below", -0.4f + form.Size * 0.9f, 0.85f));
            float fallAbove = MathF.Pow(2f, genes.Around("fall.above", -1.2f - form.Size * 1.6f, 1f));
            float waistAt = genes.Range("waist.at", 0.3f, 0.75f);
            float waistBy = genes.Chance("waisted", 0.1f) ? genes.Range("waist.by", 0.05f, 0.12f) : 0f;
            // Below the widest point a pointed shield's flanks stay convex all the way to its point, as a heater's and a kite's do, and a round one's follow a circle's edge.
            bool pointed = fallBelow > 1f;
            float point = Math.Clamp(1.4f + (fallBelow - 1f) * 0.6f, 1.4f, 2.6f);
            float Width(float t)
            {
                float s = t < belly
                    ? pointed ? 1f - MathF.Pow(1f - t / belly, point) : MathF.Pow(MathF.Max(MathF.Sin(t / belly * MathF.PI * 0.5f), 0f), fallBelow)
                    : MathF.Pow(MathF.Max(MathF.Cos((t - belly) / (1f - belly) * MathF.PI * 0.5f), 0f), fallAbove);
                float d = (t - waistAt) / 0.2f;
                return MathF.Max(wide * s * (1f - waistBy * MathF.Exp(-d * d)), 0.02f);
            }
            float Low = -tall * 0.5f;
            float WidthAt(float y) => Width(Math.Clamp((y - Low) / tall, 0f, 1f));
            bool round = MathF.Abs(tall - wide * 2f) < wide * 0.35f && fallBelow < 1.2f;

            float bow = form.Curve * genes.Range("bow", 0.08f, 0.36f) * wide;
            float thick = genes.Range("thick", 0.025f, 0.045f) * (0.8f + 0.6f * look.Heft);
            float Bowed(float x) => (1f - MathF.Min(x * x / (wide * wide), 1f)) * bow;
            Vector3 On(float x, float y, float lift) => new Vector3(x, y, Bowed(x) + thick + lift);
            Vector3 Normal(float x) => new Vector3(2f * x * bow / (wide * wide), 0f, 1f).Normalized();

            // Where the face is wide enough to paint on, as a pointed shield comes down to nothing.
            float paintLow = Low, paintHigh = -Low;
            for (int i = 0; i <= 200 && Width(i / 200f) < 0.06f; i++) paintLow = Low + tall * i / 200f;
            for (int i = 200; i >= 0 && Width(i / 200f) < 0.06f; i--) paintHigh = Low + tall * i / 200f;

            // The tinctures: one light and one dark, never white with red, and only the finest and the legendary ones purple.
            int light, dark;
            {
                var lights = Enumerable.Range(0, Tinctures.Length).Where(i => Tinctures[i].Light).ToArray();
                var darks = Enumerable.Range(0, Tinctures.Length).Where(i => !Tinctures[i].Light && (Tinctures[i].Name != "purple" || look.Legendary || look.Craft > 0.8f)).ToArray();
                light = lights[genes.Pick("light", lights.Select(_ => 1f).ToArray())];
                int lightOne = light;
                dark = darks[genes.Pick("dark", darks.Select(i => Tinctures[lightOne].Name == "white" && Tinctures[i].Name == "red" ? 0f : 1f).ToArray())];
            }
            bool lightField = genes.Chance("light.field", 0.45f);
            Vector3 woodColour = GearColours.AlbedoOf(c.Wood);
            VertexMaterial Paint(int tincture) => GearColours.Of(GearMaterials.Timber, Vector3.Lerp(Tinctures[tincture].Albedo, woodColour, look.Wear * 0.35f));

            // Boards are painted more often than not, and leather now and then, the paint taking the leather's own grain.
            bool painted = face == Face.Boards && genes.Chance("painted", 0.55f + look.Ornament * 0.35f) || face == Face.Leather && genes.Chance("painted.leather", 0.4f);
            VertexMaterial PaintedLeather(int tincture) => GearColours.Of(GearMaterials.Leather, Vector3.Lerp(Tinctures[tincture].Albedo, GearColours.AlbedoOf(c.Leather), 0.3f + look.Wear * 0.3f));
            VertexMaterial fieldMaterial = face switch
            {
                Face.Steel => c.Metal,
                Face.Leather => painted ? PaintedLeather(lightField ? light : dark) : c.Leather,
                _ => painted ? Paint(lightField ? light : dark) : c.Wood,
            };
            VertexMaterial divisionMaterial = face switch
            {
                Face.Steel => c.MetalDark,
                Face.Leather => painted ? PaintedLeather(lightField ? dark : light) : c.LeatherDark,
                _ => painted ? Paint(lightField ? dark : light) : c.WoodDark,
            };
            VertexMaterial back = face == Face.Steel ? c.MetalDark : c.Wood;

            // The body: a slab bowed across, its front the field and its back raw, closer-ringed at the ends where an outline does most of its turning.
            const int across = 16;
            int steps = 24;
            var rings = new Ring[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float t = 0.5f - 0.5f * MathF.Cos((float)i / steps * MathF.PI);
                float w = Width(t);
                var section = new Vector2[across * 2];
                for (int j = 0; j < across; j++)
                {
                    float x = 1f - 2f * j / (across - 1f);
                    float z = Bowed(x * w) / thick;
                    section[j] = new Vector2(x, z + 1f);
                    section[across * 2 - 1 - j] = new Vector2(x, z - 1f);
                }
                rings[i] = new Ring { Centre = new Vector3(0f, Low + t * tall, 0f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = w, HalfDepth = thick, Shape = section };
            }
            mesh.Counted("body", () => mesh.Loft(new Section(rings[0].Shape!, new bool[across * 2]) { ClosesAcross = true }, rings, (_, point) => point < across ? fieldMaterial : back));

            mesh.Counted("field", () =>
            {
                if (face == Face.Boards) Boards(mesh, genes.Part("boards"), c, wide, WidthAt, On, Normal, paintLow, paintHigh, painted);
                Heraldry(mesh, genes.Part("heraldry"), look, round, WidthAt, On, Normal, paintLow, paintHigh, divisionMaterial);
            });

            // The charge, at the honour point: the middle of a round shield, a little above it on a tall one.
            VertexMaterial chargeMaterial = look.Legendary ? c.Precious : face switch
            {
                Face.Steel => genes.Chance("charge.fitting", 0.6f) ? c.Fitting : c.MetalDark,
                Face.Leather => genes.Chance("charge.bone", 0.5f) ? c.Bone : c.Fitting,
                _ => painted ? Paint(lightField ? dark : light) : c.Fitting,
            };
            bool bossed = round || face == Face.Leather || genes.Chance("boss", 0.2f);
            float honour = bossed ? 0f : genes.Range("honour", 0.04f, 0.14f) * tall;
            if (!bossed || genes.Chance("charge.round", 0.35f))
            {
                mesh.Counted("charge", () => Charge(mesh, genes.Part("charge"), look, wide * (bossed ? 0.62f : 0.5f), honour, bossed, On, Normal, chargeMaterial));
            }

            // The rim, which takes the blows and is what draws the shield from up where the camera is.
            float rim = genes.Range("rim", 0.026f, 0.05f) * (0.8f + 0.5f * look.Heft);
            VertexMaterial edge = look.Legendary ? c.Precious : face switch
            {
                Face.Steel => c.Edge,
                Face.Leather => c.LeatherDark,
                _ => genes.Chance("rim.iron", 0.35f + look.Heavy * 0.4f) ? c.Metal : GearColours.Of(GearMaterials.Leather, GearSwatches.Find(GearSwatches.Leathers, "rawhide").Albedo),
            };
            mesh.Counted("rim", () =>
            {
                // Once round, through the foot and the top of the outline once each, as a pair of points there a few centimetres apart turned the rim back on itself.
                var loop = new List<Vector3>(steps * 2 + 1);
                loop.Add(new Vector3(0f, rings[0].Centre.Y, Bowed(0f)));
                for (int i = 1; i < steps; i++) loop.Add(new Vector3(rings[i].HalfWidth, rings[i].Centre.Y, Bowed(rings[i].HalfWidth)));
                loop.Add(new Vector3(0f, rings[steps].Centre.Y, Bowed(0f)));
                for (int i = steps - 1; i >= 1; i--) loop.Add(new Vector3(-rings[i].HalfWidth, rings[i].Centre.Y, Bowed(rings[i].HalfWidth)));
                loop.Add(loop[0]);
                mesh.Sweep(Section.Super(6, 2.4f), loop, _ => new Vector2(MathF.Max(rim, thick * 1.15f), MathF.Max(rim * 0.8f, thick * 1.1f)), edge, Vector3.UnitZ, capStart: false, capEnd: false);
                // Nails through the rim on boards, and rivets on steel.
                if (face != Face.Leather && genes.Chance("rim.nails", 0.6f))
                {
                    int nails = genes.Count("nails", 10, 22);
                    for (int i = 0; i < nails; i++)
                    {
                        float a = (i + 0.5f) / nails * MathF.Tau;
                        float y = MathF.Sin(a) * tall * 0.5f * 0.94f;
                        float x = MathF.Cos(a) * WidthAt(y) * 0.92f;
                        mesh.Rivet(On(x, y, 0f), Normal(x), rim * 0.45f, face == Face.Steel ? c.Fitting : c.Metal);
                    }
                }
            });

            if (bossed) mesh.Counted("boss", () => Boss(mesh, genes.Part("boss"), look, kit, wide, On, Normal, face));
            if (face == Face.Leather && (kit.Make == GearMake.Studded || genes.Chance("studs", 0.4f))) mesh.Counted("studs", () => StudRings(mesh, genes.Part("studs"), look, c, WidthAt, On, Normal, tall));
            if (tall > wide * 2.6f) mesh.Counted("bands", () => Bands(mesh, genes.Part("bands"), c, WidthAt, On, Normal, tall, Low));
            if (look.Legendary) mesh.Counted("flourish", () => Flourish(mesh, genes.Part("flourish"), look, c, wide, WidthAt, On, Normal, tall, Low, bossed));
            // Where the hand goes: far enough behind the board that nothing on the forearm or the fist meets it, where they lie across it. Everything moves so that's the origin.
            // The rim comes nearer still where it crosses the top of a short shield, over the forearm, when it's thicker than the board.
            float rimDepth = MathF.Max(rim * 0.8f, thick * 1.1f);
            float hand = MathF.Min(Bowed(GearFit.ShieldArmRoom) - thick, Bowed(0f) - rimDepth) - GearFit.ShieldStandoff;
            mesh.Counted("grip", () => Grip(mesh, genes.Part("grip"), c, x => Bowed(x) - thick, hand, wide, bossed));
            mesh.Transform(Matrix4.CreateTranslation(0f, 0f, -hand));
        }

        // Boards edge to edge down the face, their seams showing through whatever's painted over them.
        private static void Boards(GearMesh mesh, Genes genes, GearColours c, float wide, Func<float, float> widthAt, Func<float, float, float, Vector3> on, Func<float, Vector3> normal, float low, float high, bool painted)
        {
            int boards = genes.Count("count", 3, 6);
            VertexMaterial seam = painted ? GearColours.Of(GearMaterials.Timber, GearColours.AlbedoOf(c.WoodDark) * 0.7f) : c.WoodDark;
            for (int b = 1; b < boards; b++)
            {
                float x = wide * (2f * b / boards - 1f);
                var path = new List<Vector3>();
                var normals = new List<Vector3>();
                for (int i = 0; i <= 10; i++)
                {
                    float y = low + (high - low) * i / 10f;
                    if (MathF.Abs(x) > widthAt(y) * 0.94f) continue;
                    path.Add(on(x, y, 0.004f));
                    normals.Add(normal(x));
                }
                if (path.Count >= 2) mesh.Strip(path, normals, 0.004f, 0.0015f, seam);
            }
        }

        // The field divided in two tinctures, each division a field of its own laid over the face with exact edges: a half up the middle, a chief, quarters, bars, pales, or a border round it all.
        private static void Heraldry(GearMesh mesh, Genes genes, GearLook look, bool round, Func<float, float> widthAt, Func<float, float, float, Vector3> on, Func<float, Vector3> normal, float low, float high, VertexMaterial material)
        {
            int division = genes.Pick("division", 3f, 2f, 2f, 1.5f, round ? 0.5f : 1.5f, round ? 0.3f : 1f, 1.5f + look.Ornament);
            const float Lift = 0.003f;
            void Field(float y0, float y1, Func<float, (float a, float b)> span, int columns, int rows)
            {
                mesh.Plate((u, v) =>
                {
                    float y = y0 + (y1 - y0) * v;
                    (float a, float b) = span(y);
                    float x = a + (b - a) * u;
                    return (on(x, y, Lift), normal(x));
                }, columns, rows, 0.002f, (_, _) => material, PlateEdge.None, underside: material);
            }
            float W(float y) => widthAt(y) * 0.97f;
            float pivot = low + (high - low) * genes.Range("pivot", 0.45f, 0.6f);
            switch (division)
            {
                case 1:
                    Field(low, high, y => (0.006f, W(y)), 4, 12);
                    break;
                case 2:
                    Field(pivot, high, y => (-W(y), W(y)), 8, 6);
                    break;
                case 3:
                    Field(pivot, high, y => (0.006f, W(y)), 4, 6);
                    Field(low, pivot, y => (-W(y), -0.006f), 4, 6);
                    break;
                case 4:
                {
                    int bars = genes.Count("bars", 2, 3);
                    float band = (high - low) / (bars * 2 + 1);
                    for (int i = 0; i < bars; i++)
                    {
                        float y0 = low + band * (i * 2 + 1);
                        Field(y0, y0 + band, y => (-W(y), W(y)), 8, 2);
                    }
                    break;
                }
                case 5:
                {
                    // Pales: dark stripes up the field, a share of its width wherever it's measured, so they follow the outline in.
                    int pales = genes.Count("pales", 2, 3);
                    float share = 1f / (pales * 2 + 1);
                    for (int i = 0; i < pales; i++)
                    {
                        float f0 = -1f + 2f * share * (i * 2 + 1), f1 = f0 + 2f * share;
                        Field(low, high, y => (W(y) * f0, W(y) * f1), 2, 12);
                    }
                    break;
                }
                case 6:
                {
                    // A border, round the inside of the rim.
                    float band = genes.Range("border", 0.08f, 0.14f);
                    mesh.Plate((u, v) =>
                    {
                        float a = u * MathF.Tau;
                        float y = MathF.Sin(a) * (high - low) * 0.5f + (high + low) * 0.5f;
                        float outer = W(y);
                        float x = MathF.Cos(a) * MathF.Max(outer - band * (1f - v), 0.01f);
                        float yy = y - MathF.Sin(a) * band * (1f - v) * 0.6f;
                        return (on(x, yy, Lift), normal(x));
                    }, 40, 1, 0.002f, (_, _) => material, PlateEdge.None, wrap: true, underside: material);
                    break;
                }
            }
        }

        // What's borne on the field: a beast's fang, three claws, a lozenge, a ring or three roundels, all a Defender's. Laid on proud of whatever's under it.
        private static void Charge(GearMesh mesh, Genes genes, GearLook look, float size, float honour, bool bossed, Func<float, float, float, Vector3> on, Func<float, Vector3> normal, VertexMaterial material)
        {
            const float Lift = 0.007f;
            int kind = genes.Pick("kind", 2.5f + (look.Motif == Motif.Fang ? 2f : 0f), 1.5f, 1.5f, bossed ? 0f : 1.2f, bossed ? 0f : 1f);
            float s = size * genes.Range("size", 0.55f, 0.8f);
            switch (kind)
            {
                case 0:
                {
                    // A fang, point down, hooked.
                    float hook = genes.Sign("hook") * genes.Range("hook", 0.15f, 0.35f);
                    float top = honour + s * 0.55f, length = s * 1.2f;
                    mesh.Plate((u, v) =>
                    {
                        float y = top - length * v;
                        float half = s * 0.24f * MathF.Pow(1f - v, 0.75f) + 0.006f;
                        float x = hook * s * v * v + (u * 2f - 1f) * half;
                        return (on(x, y, Lift), normal(x));
                    }, 3, 8, 0.004f, (_, _) => material, PlateEdge.None, underside: material);
                    break;
                }
                case 1:
                {
                    // Three claws raked across, as a beast leaves them.
                    for (int k = -1; k <= 1; k++)
                    {
                        var path = new List<Vector3>();
                        var normals = new List<Vector3>();
                        for (int i = 0; i <= 6; i++)
                        {
                            float t = i / 6f;
                            float x = k * s * 0.32f + (t - 0.5f) * s * 0.5f;
                            float y = honour + s * 0.6f - t * s * 1.2f + MathF.Sin(t * MathF.PI) * s * 0.08f;
                            path.Add(on(x, y, Lift));
                            normals.Add(normal(x));
                        }
                        mesh.Strip(path, normals, s * 0.07f, 0.004f, material, width: t => 0.35f + 0.65f * MathF.Sin(MathF.Max(t, 0.05f) * MathF.PI));
                    }
                    break;
                }
                case 2:
                {
                    // A lozenge.
                    float h = s * 0.7f, w = s * 0.42f;
                    mesh.Plate((u, v) =>
                    {
                        float y = honour - h * 0.96f + h * 1.92f * v;
                        float half = w * (1f - MathF.Abs(y - honour) / h) + 0.004f;
                        float x = (u * 2f - 1f) * half;
                        return (on(x, y, Lift), normal(x));
                    }, 4, 8, 0.004f, (_, _) => material, PlateEdge.None, underside: material);
                    break;
                }
                case 3:
                {
                    // A ring.
                    float r1 = s * 0.5f, r0 = r1 * genes.Range("ring", 0.62f, 0.78f);
                    mesh.Plate((u, v) =>
                    {
                        float a = u * MathF.Tau;
                        float r = r0 + (r1 - r0) * v;
                        float x = MathF.Cos(a) * r;
                        return (on(x, honour + MathF.Sin(a) * r, Lift), normal(x));
                    }, 24, 1, 0.004f, (_, _) => material, PlateEdge.None, wrap: true, underside: material);
                    break;
                }
                default:
                {
                    // Three roundels, two over one.
                    float r = s * 0.2f;
                    foreach ((float x, float y) in new[] { (-s * 0.3f, honour + s * 0.25f), (s * 0.3f, honour + s * 0.25f), (0f, honour - s * 0.28f) })
                    {
                        mesh.At(GearMesh.FacingOut(on(x, y, Lift - 0.004f), normal(x)), () => mesh.Lathe(Section.Round(14), 0.006f, 1, _ => r, material, capStart: false));
                    }
                    break;
                }
            }
        }

        // A boss in the middle, over the hand: a dome, a cone, a dome on a collar, or on a studded targe a spike; on a flange nailed through.
        private static void Boss(GearMesh mesh, Genes genes, GearLook look, ArmourKit kit, float wide, Func<float, float, float, Vector3> on, Func<float, Vector3> normal, Face face)
        {
            GearColours c = look.Colours;
            float size = wide * genes.Range("size", 0.18f, 0.3f);
            float height = size * genes.Range("height", 0.55f, 1.05f);
            int kind = genes.Pick("kind", 3f, 1.5f, 1.5f, look.Spikes * 2f + (kit.Make == GearMake.Studded ? 1.5f : 0f));
            VertexMaterial metal = look.Legendary ? c.Precious : face == Face.Steel ? c.Edge : c.Metal;
            Section section = look.Angular > 0.6f ? Section.Polygon(genes.Count("sides", 6, 8)) : Section.Round(14);
            mesh.At(GearMesh.FacingOut(on(0f, 0f, 0f), normal(0f)), () =>
            {
                // The flange, and the dome on it.
                mesh.Lathe(Section.Round(16), 0.012f, 1, _ => size * 1.35f, metal, capStart: false);
                mesh.At(Matrix4.CreateTranslation(0f, 0.01f, 0f), () =>
                {
                    switch (kind)
                    {
                        case 1:
                            mesh.Lathe(section, height, 3, t => size * (1f - t * 0.9f), metal, capStart: false);
                            break;
                        case 2:
                            mesh.Lathe(section, height * 0.35f, 1, _ => size * 0.92f, metal, capStart: false, capEnd: false);
                            mesh.At(Matrix4.CreateTranslation(0f, height * 0.35f, 0f), () => mesh.Lathe(section, height * 0.65f, 4, t => size * 0.92f * MathF.Sqrt(MathF.Max(1f - t * t, 0.03f)), metal, capStart: false));
                            break;
                        case 3:
                            mesh.Lathe(section, height * 0.5f, 3, t => size * MathF.Sqrt(MathF.Max(1f - t * t * 0.7f, 0.05f)), metal, capStart: false, capEnd: false);
                            mesh.At(Matrix4.CreateTranslation(0f, height * 0.45f, 0f), () => mesh.Lathe(Section.Round(8), height * 1.1f, 3, t => size * 0.42f * (1f - t * 0.95f), metal, capStart: false));
                            break;
                        default:
                            mesh.Lathe(section, height, 4, t => size * MathF.Sqrt(MathF.Max(1f - t * t, 0.03f)), metal, capStart: false);
                            break;
                    }
                });
                int nails = genes.Count("nails", 4, 8);
                for (int i = 0; i < nails; i++)
                {
                    float a = (i + 0.5f) / nails * MathF.Tau;
                    mesh.Rivet(new Vector3(MathF.Cos(a) * size * 1.17f, 0.012f, MathF.Sin(a) * size * 1.17f), Vector3.UnitY, size * 0.1f, c.Fitting);
                }
                if (look.Legendary || look.Shines("boss.stone"))
                {
                    float stone = size * genes.Range("stone", 0.28f, 0.42f) * (look.Legendary ? 1.25f : 1f);
                    float topAt = kind == 3 ? height * 0.42f : kind == 1 ? height * 0.55f : height * 0.86f;
                    mesh.At(Matrix4.CreateTranslation(0f, topAt, 0f), () => GearKit.Setting(mesh, genes.Part("stone"), stone, c.Precious, look.Theme != GearTheme.None || look.Legendary ? c.Glow : c.Gem));
                }
            });
        }

        // Studs in rings round a targe's boss, the finer the closer.
        private static void StudRings(GearMesh mesh, Genes genes, GearLook look, GearColours c, Func<float, float> widthAt, Func<float, float, float, Vector3> on, Func<float, Vector3> normal, float tall)
        {
            int rings = genes.Count("rings", 1, 3);
            float stud = genes.Range("stud", 0.018f, 0.03f);
            VertexMaterial metal = look.Legendary ? c.Precious : c.Fitting;
            for (int r = 0; r < rings; r++)
            {
                float at = 0.45f + 0.45f * (r + 1f) / (rings + 0.6f);
                int count = (int)(10 + r * 6 + look.Ornament * 6);
                for (int i = 0; i < count; i++)
                {
                    float a = (i + (r % 2) * 0.5f) / count * MathF.Tau;
                    float y = MathF.Sin(a) * tall * 0.5f * at;
                    float x = MathF.Cos(a) * widthAt(y) * at;
                    mesh.Rivet(on(x, y, 0f), normal(x), stud, metal);
                }
            }
        }

        // Iron bands across a tall shield, nailed, as a tower's boards need more than glue.
        private static void Bands(GearMesh mesh, Genes genes, GearColours c, Func<float, float> widthAt, Func<float, float, float, Vector3> on, Func<float, Vector3> normal, float tall, float low)
        {
            int bands = genes.Count("count", 2, 3);
            for (int b = 0; b < bands; b++)
            {
                float y = low + tall * (0.2f + 0.6f * b / MathF.Max(bands - 1, 1));
                float w = widthAt(y) * 0.96f;
                var path = new List<Vector3>();
                var normals = new List<Vector3>();
                for (int i = 0; i <= 8; i++)
                {
                    float x = -w + 2f * w * i / 8f;
                    path.Add(on(x, y, 0.006f));
                    normals.Add(normal(x));
                }
                mesh.Strip(path, normals, genes.Range("width", 0.03f, 0.05f), 0.006f, c.Metal);
                GearKit.Rivets(mesh, path.Select(p => p + Vector3.UnitZ * 0.006f).ToList(), normals, 0.16f, 0.014f, c.Fitting);
            }
        }

        // What makes a legendary shield one of a kind: scrolls of gilt at its corners, its power's runes round the middle, and a beast's teeth hung from its foot.
        private static void Flourish(GearMesh mesh, Genes genes, GearLook look, GearColours c, float wide, Func<float, float> widthAt, Func<float, float, float, Vector3> on, Func<float, Vector3> normal, float tall, float low, bool bossed)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                foreach (float at in new[] { 0.78f, 0.28f })
                {
                    float y = low + tall * at;
                    float x = side * widthAt(y) * 0.62f;
                    GearKit.Scroll(mesh, on(x, y, 0.008f), new Vector3(side, 0f, 0f), Vector3.UnitY * (at > 0.5f ? 1f : -1f), wide * 0.14f, 1.4f, 0.01f, c.Precious);
                }
            }
            if (look.Theme != GearTheme.None || genes.Chance("runes", 0.6f))
            {
                int runes = genes.Count("runes", 8, 12);
                float r = wide * (bossed ? 0.5f : 0.4f);
                for (int i = 0; i < runes; i++)
                {
                    float a = i / (float)runes * MathF.Tau;
                    float x = MathF.Cos(a) * r, y = MathF.Sin(a) * r * 1.1f;
                    Vector3 dir = new Vector3(-MathF.Sin(a), MathF.Cos(a), 0f);
                    mesh.Strip(new[] { on(x, y, 0.008f) - dir * 0.03f, on(x, y, 0.008f) + dir * 0.03f }, new[] { normal(x), normal(x) }, 0.008f, 0.003f, c.Glow);
                }
            }
            if (genes.Chance("teeth", 0.5f))
            {
                int teeth = genes.Count("teeth.count", 2, 4);
                for (int i = 0; i < teeth; i++)
                {
                    float x = (i - (teeth - 1) * 0.5f) * wide * 0.22f;
                    GearKit.Trophy(mesh, genes.Part($"tooth{i}"), on(x, low + 0.06f, -0.01f), genes.Range($"tooth{i}.size", 0.12f, 0.18f), c.Bone, c.Strap);
                }
            }
        }

        // Where the arm goes, on the back: a bar across the fist on posts from the board, and on a shield without a boss a strap round the forearm above it. The board's back is at back(x),
        // and the hand at hand, behind it.
        private static void Grip(GearMesh mesh, Genes genes, GearColours c, Func<float, float> back, float hand, float wide, bool bossed)
        {
            float reach = MathF.Min(genes.Range("reach", 0.2f, 0.26f), wide * 0.7f);
            VertexMaterial grip = bossed ? c.Wood : c.Strap;
            var keys = new[]
            {
                new Vector3(-reach, 0f, back(reach) + 0.01f), new Vector3(-reach, 0f, hand + 0.04f), new Vector3(-reach * 0.6f, 0f, hand),
                new Vector3(reach * 0.6f, 0f, hand), new Vector3(reach, 0f, hand + 0.04f), new Vector3(reach, 0f, back(reach) + 0.01f),
            };
            mesh.Sweep(Section.Slab(0.3f), Spine.Through(keys, 12), _ => new Vector2(0.022f, 0.05f), grip, Vector3.UnitY);
            if (!bossed)
            {
                // A strap for the forearm, round it from the board and back, loose enough for whatever's worn on it.
                float y = genes.Range("strap.at", 0.24f, 0.34f);
                float round = GearFit.ShieldStandoff - 0.01f;
                var strap = new List<Vector3>();
                for (int i = 0; i <= 12; i++)
                {
                    float angle = MathF.PI * 0.5f + MathF.Tau * (0.06f + 0.88f * i / 12f);
                    strap.Add(new Vector3(MathF.Cos(angle) * round, y, hand + MathF.Sin(angle) * round));
                }
                strap[0] = new Vector3(strap[0].X, y, back(strap[0].X) + 0.01f);
                strap[12] = new Vector3(strap[12].X, y, back(strap[12].X) + 0.01f);
                mesh.Sweep(Section.Slab(0.3f), strap, _ => new Vector2(0.012f, 0.04f), c.Strap, Vector3.UnitY);
            }
        }

        // A ward is held up and doesn't stop anything by being in the way. A cheap one is withies bound into a hoop round a charm, a fine one a ring of metal round a stone with its runes lit, and
        // between them a cage of bands round a stone, set at even turns so that it reads as made.
        private static void Ward(GearMesh mesh, GearLook look)
        {
            Genes genes = look.Genes;
            GearForm form = look.Spec.Form;
            GearColours c = look.Colours;
            int kind = form.Make switch
            {
                GearMake.Wraps => 0,
                GearMake.Silk => 1,
                _ => genes.Pick("kind", 2f * (1f - look.Craft), 1f + look.Craft * 2f, 1.5f),
            };
            float size = genes.Around("size", 0.3f + form.Size * 0.3f, 0.04f);
            // Its head clears the fist and whatever's worn on the forearm beside it, with what hangs from its foot and a legendary's halo, which come a third of its size below it.
            float stem = GearFit.ShieldStandoff + size * 0.4f + genes.Range("stem", 0.02f, 0.1f);
            Vector3 heart = new Vector3(0f, stem + size, 0f);
            VertexMaterial metal = look.Legendary ? c.Precious : look.Craft > 0.55f ? c.Fitting : c.Metal;

            // What it's held by: a handle, a wrapped grip and a knob at its foot.
            mesh.Counted("handle", () =>
            {
                mesh.At(Matrix4.CreateTranslation(0f, -0.24f, 0f), () => mesh.Lathe(Section.Round(8), stem + 0.24f, 4, t => 0.045f * (1.1f - 0.4f * t), kind == 0 ? c.Wood : metal));
                mesh.At(Matrix4.CreateTranslation(0f, -0.16f, 0f), () => mesh.Wrap(0f, 0.22f, _ => 0.047f, 0.03f, 0.18f, kind == 0 ? c.Strap : c.Leather, sides: 8));
                mesh.At(Matrix4.CreateTranslation(0f, -0.3f, 0f), () => GearKit.Terminal(mesh, look, genes.Part("foot"), 0.08f, 0.07f, metal));
            });

            float stone = size * genes.Range("stone", 0.34f, 0.5f);
            bool lit = look.Legendary || look.Theme != GearTheme.None || look.Shines("lit");
            switch (kind)
            {
                case 0:
                    mesh.Counted("hoop", () =>
                    {
                        // Withies twisted into a hoop, cords laced across it to the charm, and what's hung from its foot.
                        int withies = genes.Count("withies", 2, 3);
                        for (int k = 0; k < withies; k++)
                        {
                            int twist = k;
                            mesh.At(Matrix4.CreateRotationX(MathF.PI / 2f) * Matrix4.CreateRotationY(k * 0.7f) * Matrix4.CreateTranslation(heart), () => GearKit.Twist(mesh, size * (1f + 0.04f * twist), 0.022f, 6 + twist, twist == 0 ? c.Wood : c.WoodDark));
                        }
                        // The charm hangs on two cords from the hoop's shoulders, in a V.
                        foreach (float a in new[] { MathF.PI * 0.28f, MathF.PI * 0.72f })
                        {
                            Vector3 rim = heart + new Vector3(MathF.Cos(a), MathF.Sin(a), 0f) * size;
                            mesh.Sweep(Section.Round(4), new[] { rim, heart + (rim - heart) * 0.15f }, _ => new Vector2(0.007f), c.Strap, Vector3.UnitZ);
                        }
                        if (genes.Chance("tooth", 0.55f + (look.Motif == Motif.Fang ? 0.3f : 0f)))
                        {
                            mesh.At(Matrix4.CreateTranslation(heart + new Vector3(0f, size * 0.25f, 0f)), () => GearKit.Fang(mesh, size * 0.55f, size * 0.12f, genes.Sign("hook"), c.Bone));
                        }
                        else
                        {
                            mesh.At(Matrix4.CreateTranslation(heart), () => mesh.Gem(genes.Count("facets", 5, 7), stone * 0.6f, stone * 0.5f, stone * 0.5f, 0.45f, lit ? c.Glow : c.Gem));
                        }
                        int hung = genes.Count("hung", 1, 3);
                        for (int i = 0; i < hung; i++)
                        {
                            float x = (i - (hung - 1) * 0.5f) * size * 0.3f;
                            GearKit.Trophy(mesh, genes.Part($"hung{i}"), heart + new Vector3(x, -size * 0.98f, 0f), size * 0.35f, c.Bone, c.Strap);
                        }
                    });
                    break;

                case 1:
                    mesh.Counted("ring", () =>
                    {
                        // An open ring of metal with its runes on its face, lit, a smaller ring inside it held on a bar across, and the stone held in the middle of that. Never spokes all round,
                        // as a wheel of them reads as a sun wheel, which is the Reich's sort of mark (LORE.md).
                        float r = size;
                        float inner = r * genes.Range("inner", 0.42f, 0.58f);
                        mesh.At(Matrix4.CreateRotationX(MathF.PI / 2f) * Matrix4.CreateTranslation(heart), () =>
                        {
                            mesh.Hoop(look.Angular > 0.5f ? Section.Slab(0.35f) : Section.Super(8, 2.4f), r, 0.04f, 0.05f, 32, metal);
                            mesh.Hoop(Section.Round(6), inner, 0.016f, 0.016f, 24, metal);
                        });
                        foreach (float a in new[] { 0f, MathF.PI })
                        {
                            Vector3 out_ = new Vector3(MathF.Cos(a), MathF.Sin(a), 0f);
                            mesh.Sweep(Section.Round(5), new[] { heart + out_ * inner, heart + out_ * (r - 0.03f) }, _ => new Vector2(0.011f), metal, Vector3.UnitZ);
                            mesh.Sweep(Section.Round(5), new[] { heart + out_ * stone * 0.5f, heart + out_ * inner }, _ => new Vector2(0.008f), metal, Vector3.UnitZ);
                        }
                        int runes = genes.Count("runes", 9, 14);
                        for (int i = 0; i < runes; i++)
                        {
                            float a = (i + 0.5f) / runes * MathF.Tau;
                            Vector3 at = heart + new Vector3(MathF.Cos(a), MathF.Sin(a), 0f) * r + Vector3.UnitZ * 0.052f;
                            Vector3 dir = new Vector3(-MathF.Sin(a), MathF.Cos(a), 0f);
                            mesh.Strip(new[] { at - dir * r * 0.06f, at + dir * r * 0.06f }, new[] { Vector3.UnitZ, Vector3.UnitZ }, 0.01f, 0.004f, lit ? c.Glow : c.Precious);
                        }
                        mesh.At(Matrix4.CreateRotationX(MathF.PI / 2f) * Matrix4.CreateTranslation(heart - Vector3.UnitZ * stone * 0.25f), () => GearKit.Setting(mesh, genes.Part("stone"), stone * 0.5f, c.Precious, lit ? c.Glow : c.Gem));
                        foreach (float side in new[] { -1f, 1f })
                        {
                            var prong = new[] { new Vector3(side * 0.03f, stem, 0f), new Vector3(side * r * 0.55f, stem + r * 0.2f, 0f), new Vector3(side * r * 0.93f, heart.Y - r * 0.3f, 0f) };
                            mesh.Sweep(Section.Round(6), Spine.Through(prong, 6), t => new Vector2(0.028f * (1f - t * 0.4f)), metal, Vector3.UnitZ);
                        }
                    });
                    break;

                default:
                    mesh.Counted("cage", () =>
                    {
                        // Bands round a stone, set at even turns so that it reads as an instrument and not a tangle.
                        int bands = genes.Count("bands", 2, 3);
                        float tilt = genes.Range("tilt", 0.25f, 0.6f);
                        for (int i = 0; i < bands; i++)
                        {
                            Matrix4 turn = Matrix4.CreateRotationZ(tilt) * Matrix4.CreateRotationY(i * MathF.PI / bands) * Matrix4.CreateTranslation(heart);
                            mesh.At(turn, () => mesh.Hoop(look.Angular > 0.5f ? Section.Slab(0.3f) : Section.Round(6), size, 0.022f, 0.014f, 28, metal));
                        }
                        mesh.At(Matrix4.CreateTranslation(heart), () => mesh.Gem(genes.Count("facets", 6, 9), stone, stone * genes.Range("crown", 0.8f, 1.2f), stone * genes.Range("pavilion", 0.8f, 1.2f), genes.Range("table", 0.3f, 0.55f), lit ? c.Glow : c.Gem));
                        // A stalk from the handle up into the cage, to just under the stone.
                        var collar = new[] { new Vector3(0f, stem - 0.01f, 0f), new Vector3(0f, heart.Y - stone * 1.3f, 0f) };
                        mesh.Sweep(Section.Round(8), collar, t => new Vector2(0.035f * (1f - t * 0.3f)), metal, Vector3.UnitZ);
                    });
                    break;
            }

            if (look.Legendary)
            {
                // A halo behind it, and scrolls of gilt off its sides.
                mesh.At(Matrix4.CreateRotationX(MathF.PI / 2f) * Matrix4.CreateTranslation(heart - Vector3.UnitZ * 0.06f), () => mesh.Hoop(Section.Round(6), size * 1.3f, 0.012f, 0.012f, 36, c.Glow));
                foreach (float side in new[] { -1f, 1f })
                {
                    GearKit.Scroll(mesh, heart + new Vector3(side * size * 1.05f, -size * 0.2f, 0f), new Vector3(side, 0f, 0f), Vector3.UnitY, size * 0.22f, 1.5f, 0.011f, c.Precious);
                }
            }
        }
    }
}

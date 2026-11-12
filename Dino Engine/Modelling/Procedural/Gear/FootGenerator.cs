using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Model;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>
    /// What's worn on the feet: a shoe round the foot on a sole, and a shaft up the shin over the leg's covering (<see cref="GearFit.BootInner"/>) no higher than a knee's plate comes down
    /// (<see cref="GearFit.BootTop"/>). Plate is a greave round the shin and a sabaton's lames over the foot; leather is a boot, laced or strapped, with its cuff turned down if it's low; a sandal
    /// is a sole and straps, with wraps up the ankle, and leaves the foot bare. Each foot is built once a side, sitting on the ground. See GEAR.md.
    /// </summary>
    public static class FootGenerator
    {
        private const float Heel = -0.2f;

        public static void Build(GearMesh mesh, GearLook look)
        {
            var kit = new ArmourKit(look);
            Genes genes = look.Genes;
            GearForm form = look.Spec.Form;

            float top = kit.Make switch
            {
                GearMake.Plate => genes.Range("top", 0.8f, GearFit.BootTop),
                GearMake.Mail => genes.Range("top", 0.45f, 0.72f),
                GearMake.Wraps => genes.Range("top", 0.35f, 0.7f),
                GearMake.Silk => genes.Range("top", 0.2f, 0.3f),
                GearMake.Cloth or GearMake.Padded => genes.Range("top", 0.3f, 0.6f),
                _ => 0.32f + (GearFit.BootTop - 0.32f) * Math.Clamp(form.Cover * genes.Range("reach", 0.8f, 1.2f), 0f, 1f),
            };
            top = MathF.Min(top, GearFit.BootTop);
            bool sandal = kit.Make == GearMake.Wraps;
            float toe = kit.Blend("toe", new Vector2(0.42f, 0.56f), new Vector2(0.38f, 0.5f), new Vector2(0.4f, 0.56f));
            // Narrow enough that a pair side by side don't meet between the feet.
            float wide = kit.Blend("wide", new Vector2(0.18f, 0.21f), new Vector2(0.16f, 0.2f), new Vector2(0.14f, 0.18f));
            float point = kit.Blend("point", new Vector2(0.1f, 0.5f), new Vector2(0.1f, 0.4f), new Vector2(0.2f, 0.85f));
            float curl = kit.Make == GearMake.Silk ? form.Curve * genes.Range("curl", 0.15f, 0.4f) : 0f;

            foreach (float side in new[] { 1f, -1f })
            {
                float x = side * BodyForm.HipX;
                if (sandal) mesh.Counted("sandal", () => Sandal(mesh, kit, genes.Part("sandal"), side, x, toe, wide, top));
                else
                {
                    mesh.Counted("shoe", () => Shoe(mesh, kit, genes.Part("shoe"), side, x, toe, wide, point, curl));
                    if (top > 0.34f) mesh.Counted("shaft", () => Shaft(mesh, kit, genes.Part("shaft"), side, x, top));
                }
            }
            if (!sandal) mesh.Covers |= BodyRegions.Feet;
        }

        // The foot: from the heel forwards, sitting on its sole. Plate steps in lames over the top of it; leather has a toe cap and a welt where it meets the sole.
        private static void Shoe(GearMesh mesh, ArmourKit kit, Genes genes, float side, float x, float toe, float wide, float point, float curl)
        {
            GearLook look = kit.Look;
            float height = kit.Blend("height", new Vector2(0.27f, 0.32f), new Vector2(0.24f, 0.29f), new Vector2(0.2f, 0.26f));
            int lames = kit.Make == GearMake.Plate ? genes.Count("lames", 4, 7) : 0;
            float sole = genes.Range("sole", 0.035f, 0.055f);
            int steps = 12 + lames;
            var rings = new List<Ring>();
            var bands = new List<int>();
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                float z = Heel + t * (toe - Heel);
                float tip = MathF.Pow(t, 2.4f);
                float hw = wide * (1f - point * tip) * (0.86f + 0.3f * MathF.Sin(MathF.Min(t * 1.5f, 1f) * MathF.PI)) * (1f - 0.65f * MathF.Pow(t, 9f));
                float hh = height * (1f - 0.6f * MathF.Pow(t, 1.2f)) * 0.5f + 0.012f;
                float lift = curl * MathF.Pow(t, 4f);
                float step = lames > 0 && t > 0.32f && i % 2 == 0 ? 0.012f : 0f;
                rings.Add(new Ring { Centre = new Vector3(x, sole + hh + lift, z), Side = Vector3.UnitX, Front = Vector3.UnitY, HalfWidth = hw + step, HalfDepth = hh + step, Hard = lames > 0 && t > 0.32f });
                // A toe cap on some, and always on a legendary pair, where it's gilt.
                bands.Add(lames > 0 && t > 0.32f ? (i % 2 == 0 ? 1 : 0) : t > 0.78f && (look.Legendary || !kit.Metal && !kit.Woven && genes.Chance("toecap", 0.5f)) ? 2 : 0);
            }
            mesh.Bone = (int)BodyForm.Foot(side);
            int first = mesh.VertexCount;
            mesh.Bands(Section.Super(14, 2.6f), rings, (ring, _) => bands[ring] switch { 1 => kit.Edge, 2 => kit.Gilt ?? kit.ShellDark, _ => kit.Shell }, capStart: true, capEnd: true);

            // The sole, a little proud of the shoe all round, and a heel block on what's made for riding or walking.
            VertexMaterial soleMaterial = kit.C.LeatherDark;
            mesh.At(Matrix4.CreateTranslation(x, 0f, (Heel + toe) * 0.5f - 0.02f), () => mesh.Lathe(Section.Super(12, 2.4f), sole, 1, _ => wide * 1.05f, soleMaterial, squash: (toe - Heel) / (2f * wide) * 0.94f));
            if (bands.Contains(2)) mesh.Mark("toe cap");
            if (!kit.Woven && genes.Chance("heel", 0.55f))
            {
                mesh.Mark("heel");
                mesh.At(Matrix4.CreateTranslation(x, 0f, Heel + 0.06f), () => mesh.Block(wide * 0.65f, sole * 1.4f, 0.07f, 0.3f, soleMaterial));
            }
            KeepToSide(mesh, first, side, GearFit.SideStrap);
            if (kit.Make == GearMake.Silk && look.Ornament > 0.4f)
            {
                // A tassel or a bead on the curled toe.
                Vector3 tipAt = rings[^1].Centre + Vector3.UnitZ * 0.02f + Vector3.UnitY * 0.03f;
                mesh.At(Matrix4.CreateTranslation(tipAt), () => mesh.Lathe(Section.Round(8), 0.05f, 3, t => 0.028f * MathF.Sin(MathF.Max(t, 0.05f) * MathF.PI), kit.Trim));
            }
        }

        // Up the shin, outside the leg's covering: a greave of plate, front and back, rolled at the top; or a boot's shaft, laced up the front or strapped, its top turned down into a cuff if
        // it's low enough not to meet a knee's plate.
        private static void Shaft(GearMesh mesh, ArmourKit kit, Genes genes, float side, float x, float top)
        {
            GearLook look = kit.Look;
            float bottom = 0.2f;
            float gap = GearFit.BootInner + genes.Range("gap", 0f, 0.02f);
            mesh.Bone = (int)BodyForm.LowerLeg(side);
            if (kit.Make == GearMake.Plate)
            {
                mesh.Mark("greave");
                float thick = 0.016f;
                // Where its outside, roll and all, is no nearer the middle than GearFit.SideStrap.
                float flat = GearFit.SideStrap + thick + thick * 0.9f * 0.45f;
                foreach (float half in new[] { 0f, 1f })
                {
                    mesh.Plate((u, v) =>
                    {
                        float angle = half * MathF.PI + u * MathF.PI;
                        float y = bottom + (top - bottom) * v;
                        float calf = half > 0.5f ? 0.025f * MathF.Sin(MathF.Min(v * 1.4f, 1f) * MathF.PI) : 0f;
                        float r = MathF.Min(BodyForm.Leg(y) + gap + calf, BodyForm.Leg(y) + GearFit.BootOuter - thick - 0.012f);
                        if (y > GearFit.KneeLow) r = MathF.Min(r, BodyForm.Leg(y) + GearFit.BootTopOuter - thick - thick * 0.5f - 0.01f);
                        var at = new Vector3(x + MathF.Cos(angle) * r, y, MathF.Sin(angle) * r);
                        var out_ = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                        if (at.X * side < flat)
                        {
                            at.X = flat * side;
                            out_ = new Vector3(-side, 0f, 0f);
                        }
                        return (at, out_);
                    }, 8, 6, thick, (_, _) => kit.Shell, PlateEdge.Top | PlateEdge.Bottom, thick * 0.9f, underside: kit.ShellDark, rollMaterial: kit.Gilt);
                }
                // Hinged on the outside, strapped behind.
                foreach (float angle in new[] { side > 0f ? 0f : MathF.PI, -MathF.PI / 2f })
                {
                    for (int k = 0; k < 2; k++)
                    {
                        float y = bottom + (top - bottom) * (0.3f + 0.4f * k);
                        float r = BodyForm.Leg(y) + gap + thick;
                        Vector3 out_ = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                        mesh.Rivet(new Vector3(x, y, 0f) + out_ * r, out_, angle < 0f ? 0.012f : 0.016f, kit.Fitting);
                    }
                }
                return;
            }

            float flare = genes.Range("flare", 0f, 0.03f);
            bool cuffed = top <= GearFit.CuffLow && genes.Chance("cuff", 0.55f);
            // A boot's shaft is often a different hide from its foot.
            VertexMaterial shaftHide = kit.Hide && genes.Chance("two.tone", 0.4f) ? kit.C.LeatherContrast : kit.Shell;
            float cuffHeight = cuffed ? genes.Range("cuff.height", 0.08f, 0.15f) : 0f;
            bool furred = kit.Hide && top <= GearFit.CuffLow && genes.Chance("fur", 0.25f);
            var rings = new List<Ring>();
            var bands = new List<int>();
            // Up to an edge at the top, which is the band from just below it to the top (a band runs from its ring to the next).
            float edge = genes.Range("edge", 0.025f, 0.045f);
            var heights = new List<float>();
            const int Steps = 8;
            for (int i = 0; i <= Steps; i++) heights.Add(bottom + (top - edge - bottom) * i / Steps);
            heights.Add(top);
            for (int i = 0; i < heights.Count; i++)
            {
                float y = heights[i];
                float t = (y - bottom) / (top - bottom);
                float r = BodyForm.Leg(y) + gap + flare * t * t;
                r = MathF.Min(r, BodyForm.Leg(y) + GearFit.BootOuter);
                if (y > GearFit.KneeLow) r = MathF.Min(r, BodyForm.Leg(y) + GearFit.BootTopOuter - 0.012f);
                rings.Add(Ring.Flat(y, r, r * 1.04f));
                bands.Add(i == heights.Count - 2 ? 1 : i == heights.Count - 1 ? 2 : 0);
            }
            for (int i = 0; i < rings.Count; i++)
            {
                Ring r = rings[i];
                r.Centre = new Vector3(x, r.Centre.Y, 0f);
                rings[i] = r;
            }
            int shaft = mesh.VertexCount;
            mesh.Bands(Section.Round(14), rings, (ring, _) => bands[ring] == 1 ? kit.Edge : shaftHide, capStart: false, capEnd: false);
            if (cuffed || furred)
            {
                // The top turned down over itself, a loft of its own so that it faces out: up the outside of the fold, and over its lip back in to the shaft's top. Only a low boot has one
                // (GearFit.CuffLow), so it never meets a knee's plate.
                float inside = rings[^1].HalfWidth;
                float r = MathF.Max(MathF.Min(BodyForm.Leg(top) + gap + 0.04f, BodyForm.Leg(top) + GearFit.CuffOuter), inside + 0.012f);
                Ring[] fold =
                {
                    new Ring { Centre = new Vector3(x, top - MathF.Max(cuffHeight, 0.06f), 0f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = r * 0.98f, HalfDepth = r * 1.02f },
                    new Ring { Centre = new Vector3(x, top + 0.012f, 0f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = r, HalfDepth = r * 1.04f, Hard = true },
                    new Ring { Centre = new Vector3(x, top, 0f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = inside, HalfDepth = inside * 1.04f },
                };
                mesh.Loft(Section.Round(14), fold, furred ? kit.C.Fur : kit.Edge, capStart: false, capEnd: false);
            }
            KeepToSide(mesh, shaft, side, GearFit.SideBoot);

            // Laces up the front, or straps round with buckles.
            if (GearMesh.Same(shaftHide, kit.C.LeatherContrast) && !GearMesh.Same(shaftHide, kit.Shell)) mesh.Mark("two tone");
            if (cuffed || furred) mesh.Mark(furred ? "fur" : "cuff");
            if (kit.Hide && genes.Chance("laces", 0.45f))
            {
                mesh.Mark("laces");
                int crosses = genes.Count("laces.count", 3, 6);
                float laceTop = top - 0.06f - cuffHeight;
                for (int i = 0; i < crosses; i++)
                {
                    float y0 = 0.28f + (laceTop - 0.28f) * i / crosses, y1 = 0.28f + (laceTop - 0.28f) * (i + 1) / crosses;
                    foreach (float way in new[] { -1f, 1f })
                    {
                        Vector3 a = new Vector3(x + way * 0.05f, y0, BodyForm.Leg(y0) + gap + 0.01f), b = new Vector3(x - way * 0.05f, y1, BodyForm.Leg(y1) + gap + 0.01f);
                        mesh.Sweep(Section.Round(4), new[] { a, b }, _ => new Vector2(0.008f), kit.Strap, Vector3.UnitZ);
                    }
                }
            }
            else if (!kit.Woven && kit.Likely("straps", 0.4f, 0.75f, 0f))
            {
                int straps = genes.Count("straps.count", 1, 3);
                mesh.Mark($"straps {straps}");
                for (int i = 0; i < straps; i++)
                {
                    float y = 0.3f + (top - 0.36f - cuffHeight) * (i + 0.5f) / straps;
                    float r = BodyForm.Leg(y) + gap + 0.008f;
                    kit.Belt(mesh, genes.Part($"strap{i}"), a => OnSide(new Vector3(x + MathF.Cos(a) * r, y, MathF.Sin(a) * r * 1.04f), side, GearFit.SideStrap + 0.0075f), a => new Vector3(MathF.Cos(a), 0f, MathF.Sin(a)), 0.02f, 0.007f, side > 0f ? 0.5f : MathF.PI - 0.5f);
                }
            }
        }

        // A point kept on its foot's side of the body's middle, and a surface drawn in flat there: the legs stand close, and what's on both feet would otherwise meet (GearFit.SideBoot).
        private static Vector3 OnSide(Vector3 p, float side, float clear) => p.X * side < clear ? new Vector3(clear * side, p.Y, p.Z) : p;

        private static void KeepToSide(GearMesh mesh, int from, float side, float clear) => mesh.Reshape(p => OnSide(p, side, clear), from);

        // A sole on the ground, a strap over the toes, straps crossing the foot, and a strip wound up round the ankle and shin. The foot itself is bare.
        private static void Sandal(GearMesh mesh, ArmourKit kit, Genes genes, float side, float x, float toe, float wide, float top)
        {
            VertexMaterial sole = kit.C.LeatherDark;
            VertexMaterial strap = kit.Hide || genes.Chance("leather", 0.6f) ? kit.Strap : kit.C.Lining;
            float thick = genes.Range("sole", 0.03f, 0.045f);
            mesh.Bone = (int)BodyForm.Foot(side);
            mesh.At(Matrix4.CreateTranslation(x, 0f, 0.1f), () => mesh.Lathe(Section.Super(12, 2.3f), thick, 1, _ => 0.11f, sole, squash: 0.2f / 0.11f));
            float footTop = 0.21f;
            foreach (float z in new[] { 0.22f, 0.08f })
            {
                var path = new[] { new Vector3(x - 0.1f, thick, z), new Vector3(x - 0.09f, footTop * 0.7f, z), new Vector3(x, footTop + 0.01f, z), new Vector3(x + 0.09f, footTop * 0.7f, z), new Vector3(x + 0.1f, thick, z) };
                mesh.Sweep(Section.Slab(0.3f), Spine.Through(path, 8), _ => new Vector2(0.024f, 0.007f), strap, Vector3.UnitZ);
            }
            mesh.Bone = (int)BodyForm.LowerLeg(side);
            // Over whatever's on the leg, as a boot's shaft is (GearFit.BootInner), and drawn in flat between the legs as one is.
            int wraps = mesh.VertexCount;
            mesh.At(Matrix4.CreateTranslation(x, 0f, 0f), () => mesh.Wrap(0.22f, top, t => BodyForm.Leg(0.22f + (top - 0.22f) * t) + GearFit.BootInner, genes.Range("pitch", 0.07f, 0.1f), 0.1f, strap, sides: 12));
            KeepToSide(mesh, wraps, side, GearFit.SideBoot);
            if (kit.Look.Legendary)
            {
                // A twisted anklet of gold or silver over the wraps.
                int anklet = mesh.VertexCount;
                mesh.At(Matrix4.CreateTranslation(x, 0.3f, 0f), () => GearKit.Twist(mesh, BodyForm.Leg(0.3f) + GearFit.BootInner + 0.018f, 0.012f, 10, kit.C.Precious));
                KeepToSide(mesh, anklet, side, GearFit.SideStrap);
            }
        }
    }
}

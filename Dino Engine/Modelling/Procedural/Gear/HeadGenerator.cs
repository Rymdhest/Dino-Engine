using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Model;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>
    /// What's worn on the head, built around where it is in <see cref="BodyForm"/> and all on the head's bone. A dome is the start of nearly everything, and what's added to it decides what it is. From
    /// above, which is where it's seen from, a head is mostly a top and a silhouette, so that's what most of this goes on.
    /// </summary>
    public static class HeadGenerator
    {
        private const float Top = BodyForm.HeadTop;
        private const float Centre = BodyForm.HeadCentre;

        public static void Build(GearMesh mesh, GearStyle style)
        {
            var cut = new ArmourCut(style);
            mesh.Bone = (int)BoneType.Head;
            GearForm form = style.Spec.Form;

            // A thin band and nothing else, and hair still shows.
            if (cut.Cloth > 0.5f && form.Cover < 0.12f)
            {
                Circlet(mesh, cut, style.Genes.Part("circlet"));
                return;
            }

            mesh.Covers |= BodyRegions.Hair;
            if (cut.Likely("hat", 0f, 0.05f, form.Size > 0.5f ? 0.8f : 0.1f)) Hat(mesh, cut, style.Genes.Part("hat"));
            else if (cut.Likely("hood", 0.04f, 0.45f, 0.85f)) Hood(mesh, cut, style.Genes.Part("hood"));
            else Helm(mesh, cut, style.Genes.Part("helm"));
        }

        private static float HalfAt(float y, float halfWidth)
        {
            float s = Math.Clamp((y - Centre) / BodyForm.HeadHalfHeight, -1f, 1f);
            return halfWidth * MathF.Sqrt(MathF.Max(1f - s * s, 0.0005f));
        }

        // The top of a head, offset out, from a height to the crown. It can run up to a point.
        private static void Dome(GearMesh mesh, ArmourCut cut, Genes genes, float from, float gap, float peak, VertexMaterial material, Section section, Func<int, VertexMaterial>? colour = null)
        {
            int steps = 10;
            var rings = new Ring[steps + 1];
            float top = Top + gap + peak;
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                float y = from + (top - from) * MathF.Sin(t * MathF.PI * 0.5f);

                // Within the head's own height it follows it. Above that it runs up to a peak.
                float s = (y - from) / (top - from);
                float along = MathF.Sqrt(MathF.Max(1f - MathF.Pow(s, 2.2f - 1f * MathF.Min(peak / 0.3f, 1f)), 0f));
                float half = BodyForm.HeadHalfWidth + gap;
                float halfDepth = BodyForm.HeadHalfDepth + gap;
                float widest = y <= Centre ? 1f : 1f;
                rings[i] = new Ring { Centre = new Vector3(0f, y, -0.015f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = half * along * widest, HalfDepth = halfDepth * along * widest };
            }
            mesh.Loft(section, rings, (ring, _) => colour?.Invoke(ring) ?? material, capStart: false, capEnd: true);
        }

        private static void Helm(GearMesh mesh, ArmourCut cut, Genes genes)
        {
            GearStyle style = cut.Style;
            GearForm form = style.Spec.Form;
            float gap = cut.Blend("gap", new Vector2(0.06f, 0.12f), new Vector2(0.03f, 0.07f), new Vector2(0.03f, 0.06f));
            float peak = genes.Range("peak", 0f, 0.3f) * (0.4f + style.Spikes * 1.2f);
            float brow = Centre + genes.Range("brow", -0.02f, 0.12f);

            // How far it comes down: an open cap, or a whole face behind steel.
            float cover = form.Cover;
            Section section = style.Angular > 0.5f ? Section.Polygon(genes.Count("facets", 6, 12)) : Section.Round(14);
            int ribs = genes.Count("ribs", 0, 2 + (int)(style.Ornate * 4f));

            // A plate cap with a mail or padded skirt below, or for a great helm a straight drum all the way down.
            if (cover > 0.75f)
            {
                float rim = BodyForm.Neck - 0.05f;
                Ring[] drum =
                {
                    new Ring { Centre = new Vector3(0f, rim, 0f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = BodyForm.HeadHalfWidth + gap + 0.03f, HalfDepth = BodyForm.HeadHalfDepth + gap + 0.03f, Hard = true },
                    new Ring { Centre = new Vector3(0f, Centre - 0.14f, 0.02f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = BodyForm.HeadHalfWidth + gap, HalfDepth = BodyForm.HeadHalfDepth + gap },
                    new Ring { Centre = new Vector3(0f, Centre + 0.02f, 0.02f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = BodyForm.HeadHalfWidth + gap, HalfDepth = BodyForm.HeadHalfDepth + gap, Hard = true },
                    new Ring { Centre = new Vector3(0f, Centre + 0.09f, 0.02f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = BodyForm.HeadHalfWidth + gap, HalfDepth = BodyForm.HeadHalfDepth + gap, Hard = true },
                };
                VertexMaterial slit = style.Palette.Dark;
                mesh.Loft(section, drum, (ring, _) => ring == 2 ? slit : cut.Main, capStart: false, capEnd: false);
                brow = Centre + 0.09f;

                // Breaths, on the cheek.
                int holes = genes.Count("holes", 0, 5);
                for (int i = 0; i < holes; i++)
                {
                    float y = Centre - 0.2f - i * 0.05f;
                    cut.Stud(mesh, new Vector3(genes.Sign("holes.side") * 0.2f, y, BodyForm.HeadHalfDepth + gap - 0.02f), Vector3.UnitZ, 0.018f, style.Palette.Dark);
                }
            }
            else if (cover > 0.4f)
            {
                // Mail or padding hanging round the neck, leaving the face open.
                float rim = BodyForm.Neck + genes.Range("aventail", -0.14f, 0.04f);
                Ring[] skirt =
                {
                    new Ring { Centre = new Vector3(0f, rim, -0.06f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = BodyForm.HeadHalfWidth + gap + 0.09f, HalfDepth = BodyForm.HeadHalfDepth + gap + 0.03f, Hard = true },
                    new Ring { Centre = new Vector3(0f, brow, -0.1f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = BodyForm.HeadHalfWidth + gap - 0.005f, HalfDepth = BodyForm.HeadHalfDepth + gap - 0.06f, Hard = true },
                };
                mesh.Loft(Section.Round(14), skirt, cut.Heavy > 0.3f ? cut.Under : cut.Second, capStart: false, capEnd: false);
            }

            Dome(mesh, cut, genes, brow, gap, peak, cut.Main, section, ring => ribs > 0 && ring % Math.Max(10 / Math.Max(ribs, 1), 2) == 0 ? cut.Second : cut.Main);

            // A band round the brow.
            float browRadius = BodyForm.HeadHalfWidth + gap;
            ArmourCut.Band(mesh, new Vector3(0f, brow, -0.015f), browRadius, BodyForm.HeadHalfDepth + gap, genes.Range("band", 0.02f, 0.05f), 0.012f, cut.Second);

            if (cover <= 0.75f && cut.Likely("nasal", 0.6f, 0.08f, 0f)) Nasal(mesh, cut, genes.Part("nasal"), brow, gap);
            if (cover <= 0.75f && cut.Likely("cheeks", 0.45f, 0.25f, 0f)) Cheeks(mesh, cut, genes.Part("cheeks"), brow, gap);
            Crest(mesh, cut, genes.Part("crest"), brow, gap, peak);
        }

        private static void Nasal(GearMesh mesh, ArmourCut cut, Genes genes, float brow, float gap)
        {
            float length = genes.Range("length", 0.18f, 0.36f);
            float wide = genes.Range("wide", 0.03f, 0.06f);
            float z = BodyForm.HeadHalfDepth + gap;
            var path = new[] { new Vector3(0f, brow + 0.04f, z), new Vector3(0f, brow - length * 0.5f, z + 0.02f), new Vector3(0f, brow - length, z + 0.012f) };
            mesh.Sweep(Section.Slab(0.3f), Spine.Through(path, 5), t => new Vector2(wide * (1f - t * 0.4f), 0.012f), cut.Second, Vector3.UnitZ);
        }

        private static void Cheeks(GearMesh mesh, ArmourCut cut, Genes genes, float brow, float gap)
        {
            float length = genes.Range("length", 0.26f, 0.5f);
            float wide = genes.Range("wide", 0.08f, 0.14f);
            foreach (float side in new[] { 1f, -1f })
            {
                float x = side * (BodyForm.HeadHalfWidth + gap - 0.02f);
                var path = new[] { new Vector3(x, brow, 0.1f), new Vector3(x * 0.98f, brow - length * 0.6f, 0.16f), new Vector3(x * 0.7f, brow - length, 0.2f) };
                mesh.Sweep(Section.Slab(0.3f), Spine.Through(path, 5), t => new Vector2(wide * (1f - t * 0.55f), 0.014f), cut.Main, Vector3.UnitX);
            }
        }

        // What makes a helm a shape from above: a fin, a plume, a pair of horns or wings.
        private static void Crest(GearMesh mesh, ArmourCut cut, Genes genes, float brow, float gap, float peak)
        {
            GearStyle style = cut.Style;
            float top = Top + gap + peak;
            int kind = genes.Pick("kind", 2f, cut.Heavy * 1.5f + style.Ornate, cut.Heavy * 1.2f + style.Ornate * 0.5f, style.Spikes * 3f, style.Spikes * 1.5f + style.Flare);
            switch (kind)
            {
                case 1:
                    // A fin, front to back.
                    float tall = genes.Range("fin.tall", 0.1f, 0.3f);
                    float length = BodyForm.HeadHalfDepth + gap;
                    var fin = new Ring[9];
                    for (int i = 0; i <= 8; i++)
                    {
                        float t = i / 8f;
                        float z = (t * 2f - 1f) * length * 0.95f;
                        float s = t * 2f - 1f;
                        fin[i] = new Ring { Centre = new Vector3(0f, top - (top - brow) * s * s * 0.65f + tall * 0.5f * (1f - s * s), z), Side = Vector3.UnitY, Front = Vector3.UnitX, HalfWidth = tall * (0.3f + 0.7f * (1f - s * s)), HalfDepth = 0.018f };
                    }
                    mesh.Loft(Section.Slab(0.3f), fin, cut.Fitting);
                    break;

                case 2:
                    // A plume.
                    float reach = genes.Range("plume.reach", 0.6f, 1.2f);
                    float back = genes.Range("plume.back", 0.5f, 1.1f);
                    var keys = new[] { new Vector3(0f, top - 0.03f, 0f), new Vector3(0f, top + reach * 0.36f, -back * 0.25f), new Vector3(0f, top + reach * 0.42f, -back * 0.7f), new Vector3(0f, top + reach * 0.02f, -back) };
                    float wide = genes.Range("plume.wide", 0.08f, 0.16f);
                    mesh.Sweep(Section.Super(10, 2.2f), Spine.Through(keys, 12), t => new Vector2(wide * (0.3f + 1.1f * MathF.Sin(t * MathF.PI)) * (1f - t * 0.4f), wide * 0.6f * (0.3f + MathF.Sin(t * MathF.PI))), style.Palette.Cloth, Vector3.UnitX);
                    break;

                case 3:
                    // Horns.
                    float span = genes.Range("horn.span", 0.4f, 0.9f);
                    float rise = genes.Range("horn.rise", 0.2f, 0.9f);
                    float thick = genes.Range("horn.thick", 0.05f, 0.1f);
                    foreach (float side in new[] { 1f, -1f })
                    {
                        float x = side * (BodyForm.HeadHalfWidth + gap - 0.04f);
                        var horn = new[] { new Vector3(x, Centre + 0.14f, 0f), new Vector3(x + side * span * 0.55f, Centre + 0.2f, -0.05f), new Vector3(x + side * span, Centre + 0.2f + rise * 0.6f, -0.02f), new Vector3(x + side * span * 0.86f, Centre + 0.2f + rise, 0.06f) };
                        mesh.Sweep(Section.Round(7), Spine.Through(horn, 10), t => new Vector2(thick * (1f - t * 0.94f)), style.Palette.Bone, Vector3.UnitZ);
                    }
                    break;

                case 4:
                    // Something rising from either side.
                    int blades = genes.Count("wing.blades", 2, 4);
                    float length2 = genes.Range("wing.length", 0.4f, 0.75f);
                    foreach (float side in new[] { 1f, -1f })
                    {
                        for (int i = 0; i < blades; i++)
                        {
                            float tilt = 0.5f + (float)i / blades * 0.7f;
                            float x = side * (BodyForm.HeadHalfWidth + gap - 0.03f);
                            var wing = new[] { new Vector3(x, Centre + 0.2f, -0.06f * i), new Vector3(x + side * MathF.Cos(tilt) * length2 * 0.5f, Centre + 0.2f + MathF.Sin(tilt) * length2 * 0.5f, -0.06f * i), new Vector3(x + side * MathF.Cos(tilt) * length2, Centre + 0.2f + MathF.Sin(tilt) * length2, -0.06f * i - 0.04f) };
                            mesh.Sweep(Section.Slab(0.3f), Spine.Through(wing, 5), t => new Vector2(0.03f, 0.05f * (1f - t * 0.9f)), cut.Fitting, Vector3.UnitZ);
                        }
                    }
                    break;
            }
        }

        // Cloth over the head, back from the face, and down onto the shoulders.
        private static void Hood(GearMesh mesh, ArmourCut cut, Genes genes)
        {
            GearStyle style = cut.Style;
            float gap = genes.Range("gap", 0.06f, 0.13f);
            float peak = genes.Range("peak", 0f, 0.28f) * style.Spec.Form.Curve;
            float pull = genes.Range("pull", 0.06f, 0.16f);
            float drape = genes.Range("drape", 0.05f, 0.34f);

            int steps = 12;
            var rings = new Ring[steps + 1];
            float bottom = BodyForm.Neck - 0.26f - drape;
            float top = Top + gap + peak;
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                float y = bottom + (top - bottom) * t;

                // Wide at the shoulders, in at the neck, out round the head and closing at the top.
                float head = y > Centre - 0.2f ? MathF.Sqrt(MathF.Max(1f - MathF.Pow((y - (Centre - 0.2f)) / (top - (Centre - 0.2f)), 2.4f), 0.0008f)) : 1f;
                float widen = Math.Clamp((BodyForm.Neck - y) / 0.5f, 0f, 1f);
                float half = (BodyForm.HeadHalfWidth + gap) * head + widen * widen * 0.42f;
                float deep = (BodyForm.HeadHalfDepth + gap - pull * 0.4f) * head + widen * widen * 0.2f;
                rings[i] = new Ring { Centre = new Vector3(0f, y, -pull), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = half, HalfDepth = deep };
            }

            mesh.Bone = (int)BoneType.Head;
            int first = mesh.VertexCount;
            mesh.Loft(Section.Round(16), rings, (ring, _) => ring == 0 ? cut.Second : cut.Main, capStart: false, capEnd: true);

            // What hangs on the shoulders stays with the body.
            mesh.SkinBy(at => at.Y < BodyForm.Neck + 0.1f ? (new Vector2((int)BoneType.Chest, 0f), new Vector2(1f, 0f)) : (new Vector2((int)BoneType.Head, 0f), new Vector2(1f, 0f)), first);

            // Round the face.
            float rim = BodyForm.HeadHalfWidth + gap * 0.5f;
            mesh.At(Matrix4.CreateRotationX(MathF.PI / 2f - 0.35f) * Matrix4.CreateTranslation(0f, Centre + 0.02f, 0.14f), () => mesh.Hoop(Section.Round(6), rim, 0.028f, 0.028f, 18, cut.Second));
        }

        // A wizard's, tall, soft and bent over.
        private static void Hat(GearMesh mesh, ArmourCut cut, Genes genes)
        {
            GearStyle style = cut.Style;
            float gap = genes.Range("gap", 0.05f, 0.09f);
            float tall = genes.Range("tall", 0.7f, 1.8f) * (0.65f + style.Spec.Form.Size * 0.7f);
            float lean = style.Spec.Form.Curve * genes.Range("lean", 0.15f, 0.9f) * genes.Sign("lean.way");
            float brim = genes.Range("brim", 0.1f, 0.5f);
            float y = Centre + genes.Range("sit", 0.1f, 0.22f);
            float base_ = BodyForm.HeadHalfWidth + gap;

            mesh.Hoop2(new Vector3(0f, y, 0f), base_ + brim, base_ * 0.9f, genes.Range("brim.droop", -0.02f, 0.08f), cut.Second);

            int steps = 14;
            var spine = new List<Vector3>(steps + 1);
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                spine.Add(new Vector3(lean * t * t * t * tall, y + tall * t * (1f - 0.18f * t * t * MathF.Abs(lean)), -lean * 0.3f * t * t * tall));
            }

            mesh.Sweep(Section.Round(12), spine, t => new Vector2(base_ * MathF.Pow(1f - t, 0.8f) + 0.005f), cut.Main, Vector3.UnitZ);

            float band = genes.Range("band", 0.03f, 0.07f);
            ArmourCut.Band(mesh, new Vector3(0f, y + band + 0.015f, 0f), base_ * 0.96f, base_ * 0.96f, band, 0.012f, cut.Second);

            if (style.Shines("stone"))
            {
                float size = genes.Range("stone.size", 0.05f, 0.09f);
                mesh.At(Matrix4.CreateRotationX(MathF.PI / 2f) * Matrix4.CreateTranslation(0f, y + band + 0.015f, base_ * 0.96f), () => mesh.Gem(genes.Count("stone.sides", 5, 8), size, size * 0.6f, size * 0.3f, 0.5f, style.Palette.Glow));
            }
        }

        private static void Circlet(GearMesh mesh, ArmourCut cut, Genes genes)
        {
            GearStyle style = cut.Style;
            float y = Centre + genes.Range("at", 0.1f, 0.2f);
            float gap = 0.02f;
            float wide = genes.Range("wide", 0.02f, 0.06f);
            float rw = HalfAt(y, BodyForm.HeadHalfWidth) + gap, rd = HalfAt(y, BodyForm.HeadHalfDepth) + gap;
            ArmourCut.Band(mesh, new Vector3(0f, y, 0f), rw, rd, wide, 0.01f, cut.Fitting);

            int points = genes.Count("points", 0, 7);
            float reach = genes.Range("point.reach", 0.05f, 0.18f);
            for (int i = 0; i < points; i++)
            {
                float angle = MathF.PI / 2f + (i - (points - 1) * 0.5f) * genes.Range("point.gap", 0.3f, 0.6f);
                Vector3 at = new Vector3(MathF.Cos(angle) * rw, y + wide, MathF.Sin(angle) * rd);
                mesh.At(Matrix4.CreateTranslation(at), () => mesh.Lathe(Section.Polygon(4), reach * (i == points / 2 ? 1.5f : 1f), 2, t => 0.024f * (1f - t), cut.Fitting, capEnd: false));
            }

            float size = genes.Range("stone.size", 0.04f, 0.08f);
            mesh.At(Matrix4.CreateRotationX(MathF.PI / 2f) * Matrix4.CreateTranslation(0f, y, rd + 0.01f), () => mesh.Gem(genes.Count("stone.sides", 5, 8), size, size * 0.6f, size * 0.3f, 0.5f, style.Palette.Stone));
        }
    }
}

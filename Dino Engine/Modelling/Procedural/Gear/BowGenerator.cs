using Dino_Engine.Modelling.Model;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>
    /// A bow, held at the origin with its limbs up and down y, its back towards +z and its string on the near side. A limb is one curve drawn out from the grip: how far it arcs, and how far
    /// it turns back at the tip, and everything between a straight stave and a horn recurve is the same two numbers.
    /// </summary>
    public static class BowGenerator
    {
        public static void Build(GearMesh mesh, GearStyle style)
        {
            Genes genes = style.Genes;
            GearForm form = style.Spec.Form;
            GearPalette palette = style.Palette;

            float reach = genes.Around("reach", 1.5f + form.Size * 1.3f, 0.09f);
            float arc = reach * genes.Around("arc", 0.34f - form.Size * 0.12f, 0.075f);
            float recurve = reach * (form.Curve * genes.Range("recurve", 0.2f, 0.42f) + genes.Range("recurve.own", 0f, 0.16f));
            float width = genes.Range("width", 0.055f, 0.09f) * (0.8f + 0.5f * style.Heft) * (0.75f + 0.5f * (1f - form.Slender));
            float depth = width * genes.Range("depth", 0.5f, 0.85f);
            float tipShare = genes.Range("tip", 0.22f, 0.55f);
            float lower = genes.Range("lower", 0.9f, 1f);
            bool horn = genes.Chance("horn", form.Fancy * 0.6f);
            VertexMaterial limb = horn ? palette.Bone : palette.Wood;

            Vector3 Tip(float side, float share)
            {
                float length = reach * (side > 0f ? 1f : lower);
                float z = arc * MathF.Sin(share * MathF.PI * 0.5f) - recurve * MathF.Pow(share, 5f);
                return new Vector3(0f, side * length * share, z);
            }

            void Limb(float side)
            {
                var keys = new Vector3[8];
                for (int i = 0; i < keys.Length; i++) keys[i] = Tip(side, i / 7f);
                mesh.Sweep(style.Angular > 0.5f ? Section.Slab(0.35f) : Section.Super(8, 2.6f), Spine.Through(keys, 22), t =>
                {
                    float taper = 1f - (1f - tipShare) * t * t;
                    return new Vector2(width * taper, depth * taper);
                }, limb, Vector3.UnitX, capStart: false, capEnd: true);

                Vector3 end = Tip(side, 1f);
                float nock = width * genes.Range("nock", 0.6f, 1.2f);
                mesh.At(Matrix4.CreateTranslation(end + Vector3.UnitY * (side > 0f ? 0f : -nock * 1.4f)), () => BladeGenerator.Knob(mesh, style, genes.Part("nock"), nock * 1.4f, nock * 0.7f, palette.Fitting));
            }

            Limb(1f);
            Limb(-1f);

            // The riser, wrapped where the hand goes.
            float grip = genes.Range("grip", 0.28f, 0.42f);
            int wraps = genes.Count("wraps", 5, 13);
            var rings = new Ring[wraps * 2 + 1];
            for (int i = 0; i <= wraps * 2; i++)
            {
                float t = (float)i / (wraps * 2);
                float s = t * 2f - 1f;
                float bulge = 1f + 0.5f * (1f - s * s);
                rings[i] = new Ring { Centre = new Vector3(0f, s * grip, arc * 0.04f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = width * bulge, HalfDepth = depth * bulge * 1.15f };
            }
            mesh.Loft(Section.Round(8), rings, (ring, _) => ring % 2 == 0 ? palette.Binding : palette.Leather);

            // A string has no thickness to speak of from up where the camera is, so it's a little thicker than life.
            mesh.Sweep(Section.Round(4), new[] { Tip(1f, 1f), Tip(-1f, 1f) }, _ => new Vector2(0.014f), palette.Bone, Vector3.UnitZ);

            if (genes.Chance("charm", style.Ornate * 0.5f) || style.Spec.Theme != GearTheme.None)
            {
                float size = width * genes.Range("charm.size", 0.8f, 1.5f);
                float at = genes.Range("charm.at", 0.3f, 0.75f);
                foreach (float side in new[] { 1f, -1f })
                {
                    mesh.At(Matrix4.CreateTranslation(Tip(side, at) + Vector3.UnitZ * depth), () => mesh.Gem(genes.Count("charm.sides", 4, 7), size, size * 0.6f, size * 0.4f, 0.5f, style.Shines("lit") ? palette.Glow : palette.Stone));
                }
            }
        }
    }
}

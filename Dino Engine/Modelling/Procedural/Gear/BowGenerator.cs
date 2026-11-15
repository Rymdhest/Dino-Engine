using Dino_Engine.Modelling.Model;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>
    /// A bow, held at the origin with its limbs up and down y, its back towards +z and its string on the near side. A limb is one curve drawn out from the grip: how far it arcs forwards, and how far it
    /// turns back at its tip, so everything between a straight stave and a horn recurve is the same few numbers. A self bow is one wood, D-shaped across; a composite has a belly of horn and a back of
    /// sinew laid on a wooden core, and stiff ears of bone where it recurves, the string held off by bridges. See GEAR.md.
    /// </summary>
    public static class BowGenerator
    {
        public static void Build(GearMesh mesh, GearLook look)
        {
            Genes genes = look.Genes;
            GearForm form = look.Spec.Form;
            GearColours colours = look.Colours;

            // A composite bow is short and curls back; a self bow or a longbow is long, and only bends.
            bool composite = genes.Chance("composite", form.Curve * 0.85f + form.Fancy * 0.2f);
            float reach = genes.Around("reach", composite ? 1.25f + form.Size * 0.6f : 1.6f + form.Size * 1.25f, 0.08f);
            float arc = reach * genes.Around("arc", composite ? 0.12f : 0.28f - form.Size * 0.08f, 0.04f);
            float recurve = reach * (composite ? genes.Range("recurve", 0.22f, 0.42f) : genes.Range("recurve.self", 0f, 0.08f));
            float ear = composite ? genes.Range("ear", 0.16f, 0.3f) : 0f;
            float width = genes.Range("width", 0.055f, 0.085f) * (0.8f + 0.5f * look.Heft) * (0.8f + 0.4f * (1f - form.Slender));
            float depth = width * genes.Range("depth", composite ? 0.42f : 0.6f, composite ? 0.6f : 0.85f);
            float lower = genes.Range("lower", 0.92f, 1f);

            Vector3 Point(float side, float share)
            {
                float length = reach * (side > 0f ? 1f : lower);
                // Out to where the ear starts the limb arcs forward; past it, it turns back hard, as a siyah does.
                float bend = MathF.Min(share / (1f - ear * 0.6f), 1f);
                float z = arc * MathF.Sin(bend * MathF.PI * 0.5f) - recurve * MathF.Pow(share, composite ? 3.5f : 5f);
                return new Vector3(0f, side * length * share, z);
            }

            VertexMaterial core = colours.Wood;
            VertexMaterial belly = composite ? colours.Horn : colours.Wood;
            VertexMaterial back = composite ? colours.Bone : genes.Chance("backed", 0.25f) ? colours.Strap : colours.Wood;
            void Limb(float side)
            {
                var keys = new Vector3[9];
                for (int i = 0; i < keys.Length; i++) keys[i] = Point(side, 0.08f + 0.92f * i / 8f);
                List<Vector3> spine = Spine.Through(keys, 26);
                var rings = new Ring[spine.Count];
                Vector3 front = Vector3.UnitZ;
                for (int i = 0; i < spine.Count; i++)
                {
                    Vector3 tangent = (spine[Math.Min(i + 1, spine.Count - 1)] - spine[Math.Max(i - 1, 0)]).Normalized();
                    front = (front - tangent * Vector3.Dot(front, tangent)).Normalized();
                    float t = (float)i / (spine.Count - 1);
                    float taper = 1f - 0.62f * t * t;
                    bool stiff = composite && t > 1f - ear;
                    rings[i] = new Ring { Centre = spine[i], Side = Vector3.Cross(front, tangent), Front = front, HalfWidth = width * taper * (stiff ? 0.75f : 1f), HalfDepth = depth * taper * (stiff ? 1.2f : 1f) };
                }
                // Across it: D-shaped, round on the belly and flat on the back, so that a composite's two faces are two materials meeting at a crease.
                mesh.Loft(Section.LimbD(), rings, (ring, point, after) =>
                {
                    float t = (float)ring / (rings.Length - 1);
                    if (composite && t > 1f - ear) return colours.Bone;
                    return Section.IsLimbBack(point, after) ? back : belly;
                });

                // A nock of horn at the tip, and on a composite a bridge where the string lies against the ear.
                Vector3 tip = spine[^1];
                float nock = width * genes.Range("nock", 0.7f, 1.1f);
                Vector3 way = (spine[^1] - spine[^2]).Normalized();
                mesh.At(GearMesh.Basis(tip - way * nock * 0.4f, Vector3.UnitX, way, Vector3.Cross(Vector3.UnitX, way)), () => GearKit.Terminal(mesh, look, genes.Part("nock"), nock * 1.6f, nock * 0.75f, composite ? colours.Horn : colours.Bone));
                if (composite)
                {
                    int at = (int)((1f - ear) * (spine.Count - 1));
                    Vector3 bridge = spine[at] - rings[at].Front * depth;
                    mesh.At(Matrix4.CreateTranslation(bridge), () => mesh.Block(width * 0.6f, width * 0.5f, width * 0.35f, 0.3f, colours.Bone));
                }
                // Bands of sinew or silk lashed round the limb, where it's been bound.
                int bands = genes.Count("bands", 0, 1 + (int)(look.Ornament * 3f));
                for (int i = 0; i < bands; i++)
                {
                    int k = Math.Min((int)(genes.Range($"band{i}", 0.12f, 0.75f) * (spine.Count - 1)), spine.Count - 2);
                    Ring r = rings[k];
                    Vector3 along = (spine[k + 1] - spine[k]).Normalized();
                    float half = width * genes.Range($"band{i}.half", 0.25f, 0.45f);
                    Ring a = r, b = r;
                    a.Centre = r.Centre - along * half;
                    b.Centre = r.Centre + along * half;
                    a.HalfWidth *= 1.12f;
                    a.HalfDepth *= 1.18f;
                    b.HalfWidth *= 1.12f;
                    b.HalfDepth *= 1.18f;
                    mesh.Loft(Section.Round(10), new[] { a, b }, genes.Chance($"band{i}.silk", look.Craft) ? colours.Trim : colours.Strap);
                }
            }
            Limb(1f);
            Limb(-1f);

            // The riser: the stiff middle the hand closes round, wrapped, with a shelf on the left for the arrow and a plate of bone on a finer bow.
            float grip = genes.Range("grip", 0.26f, 0.38f);
            var riser = new Ring[11];
            for (int i = 0; i <= 10; i++)
            {
                float t = i / 10f;
                float s = t * 2f - 1f;
                float bulge = 1f + 0.55f * (1f - s * s);
                Vector3 at = Vector3.Lerp(Point(-1f, 0.08f), Point(1f, 0.08f), t);
                riser[i] = new Ring { Centre = at + Vector3.UnitZ * depth * 0.15f, Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = width * bulge, HalfDepth = depth * bulge * 1.15f };
            }
            mesh.Loft(Section.Round(10), riser, core);
            mesh.Wrap(-grip * 0.5f, grip * 0.5f, t => width * (1f + 0.5f * (1f - MathF.Pow(t * 2f - 1f, 2f))) * 1.06f, genes.Range("grip.pitch", 0.03f, 0.05f), 0.12f, genes.Chance("grip.cloth", look.Cloth * 0.6f) ? colours.Trim : colours.Strap, squash: 1.1f);
            mesh.At(Matrix4.CreateTranslation(width * 1.15f, grip * 0.5f, depth * 0.15f), () => mesh.Block(width * 0.3f, width * 0.5f, depth * 0.55f, 0.4f, colours.LeatherDark));
            if (look.Craft > 0.5f && genes.Chance("inlay", look.Ornament))
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 at = Point(side, 0.22f);
                    mesh.At(GearMesh.Basis(at + Vector3.UnitZ * depth * 0.9f, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ), () => mesh.Block(width * 0.55f, grip * 0.35f, depth * 0.08f, 0.5f, look.Legendary ? colours.Precious : colours.Bone));
                }
            }

            // A string has no thickness to speak of from up where the camera is, so it's a little thicker than life, with its serving at the middle where an arrow is nocked.
            Vector3 top = Point(1f, 1f), bottom = Point(-1f, 1f);
            mesh.Sweep(Section.Round(4), new[] { top, bottom }, _ => new Vector2(0.012f), colours.Lining, Vector3.UnitZ);
            Vector3 middle = Vector3.Lerp(top, bottom, 0.5f);
            mesh.Sweep(Section.Round(5), new[] { middle + Vector3.UnitY * 0.12f, middle - Vector3.UnitY * 0.12f }, _ => new Vector2(0.018f), colours.Strap, Vector3.UnitZ);

            if (look.Shines("limb.stones") || look.Theme != GearTheme.None && genes.Chance("themed", 0.5f))
            {
                float size = width * genes.Range("stone", 0.55f, 0.8f);
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 at = Point(side, genes.Range("stone.at", 0.25f, 0.4f));
                    mesh.At(GearMesh.Basis(at + Vector3.UnitZ * depth * 0.95f, Vector3.UnitX, Vector3.UnitZ, -Vector3.UnitY), () => GearKit.Setting(mesh, genes.Part("stone"), size, colours.Precious, look.Theme != GearTheme.None ? colours.Glow : colours.Gem));
                }
            }
            if (look.Legendary || look.Motif == Motif.Fang && genes.Chance("quills", 0.6f))
            {
                // Quills taken from a raptor, bound to the tips.
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 tip = Point(side, 0.94f);
                    for (int i = 0; i < 3; i++)
                    {
                        float a = (i - 1) * 0.35f;
                        Vector3 way = new Vector3(MathF.Sin(a) * 0.6f, -side * 0.5f, -0.6f).Normalized();
                        var keys = new[] { tip, tip + way * 0.18f, tip + way * 0.36f + Vector3.UnitZ * 0.04f };
                        mesh.Sweep(Section.Slab(0.2f), Spine.Through(keys, 5), t => new Vector2(0.03f * MathF.Sin(MathF.Max(t, 0.1f) * MathF.PI), 0.004f), look.Legendary ? colours.Trim : colours.Bone, Vector3.UnitX);
                    }
                }
            }
        }
    }
}

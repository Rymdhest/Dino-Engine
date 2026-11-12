using Dino_Engine.Modelling.Model;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>
    /// What one piece of armour is made of, by what each part of it does: its shell, what shows under it, its edging, what holds it on, its fittings and its trim. How it's made comes from its name
    /// when that says (<see cref="GearForm.Make"/>), and otherwise from how much of each family it is: heavy is plate, scale, lamellar, mail or quilting; leather is soft hide, hardened leather or
    /// studded; cloth is plain or fine. So a belt is always leather and a buckle always metal, whatever else is drawn. See GEAR.md.
    /// </summary>
    internal sealed class ArmourKit
    {
        public readonly GearLook Look;
        public readonly Genes Genes;
        public readonly GearColours C;
        public readonly GearMake Make;

        public readonly VertexMaterial Shell;       // What most of it is.
        public readonly VertexMaterial ShellDark;   // The same in a recess or a shadow of its own.
        public readonly VertexMaterial Under;       // What shows below it and between its parts.
        public readonly VertexMaterial Edge;        // Its edging: a rolled rim, a welt, a hem.
        public readonly VertexMaterial Strap;       // What holds it on, always leather.
        public readonly VertexMaterial Fitting;     // Buckles, rivets and studs, always metal.
        public readonly VertexMaterial Trim;        // What picks it out.

        public bool Metal => Make is GearMake.Plate or GearMake.Scale or GearMake.Lamellar or GearMake.Mail;
        public bool Hide => Make is GearMake.Hide or GearMake.Hardened or GearMake.Studded;
        public bool Woven => Make is GearMake.Cloth or GearMake.Silk or GearMake.Padded or GearMake.Wraps;

        public ArmourKit(GearLook look)
        {
            Look = look;
            Genes = look.Genes;
            C = look.Colours;
            Make = look.Spec.Form.Make != GearMake.Any ? look.Spec.Form.Make : Choose(look);

            Strap = C.Strap;
            Fitting = C.Fitting;
            switch (Make)
            {
                case GearMake.Plate:
                    Shell = C.Metal;
                    ShellDark = C.MetalDark;
                    Under = Genes.Chance("under.mail", 0.55f + look.Craft * 0.2f) ? C.Mail : C.Padding;
                    Edge = Genes.Chance("edge.fitting", 0.25f + look.Ornament * 0.5f) ? C.Fitting : C.Metal;
                    Trim = Genes.Chance("trim.cloth", 0.5f) ? C.Cloth : C.Fitting;
                    break;
                case GearMake.Scale:
                    Shell = C.Scale;
                    ShellDark = C.MetalDark;
                    Under = C.Padding;
                    Edge = C.LeatherDark;
                    Trim = C.Cloth;
                    break;
                case GearMake.Lamellar:
                    Shell = C.Metal;
                    ShellDark = C.MetalDark;
                    Under = C.Padding;
                    Edge = C.LeatherDark;
                    Trim = Genes.Chance("lacing.dyed", 0.6f + look.Craft * 0.3f) ? C.Trim : C.Strap;
                    break;
                case GearMake.Mail:
                    Shell = C.Mail;
                    ShellDark = C.MetalDark;
                    Under = C.Padding;
                    Edge = Genes.Chance("edge.leather", 0.5f) ? C.LeatherDark : C.Fitting;
                    Trim = C.Cloth;
                    break;
                case GearMake.Brigandine:
                    bool cloth = Genes.Chance("cover.cloth", 0.55f);
                    Shell = cloth ? C.Cloth : C.Leather;
                    ShellDark = cloth ? C.ClothDark : C.LeatherDark;
                    Under = C.Mail;
                    Edge = C.LeatherDark;
                    Trim = C.Trim;
                    break;
                case GearMake.Padded:
                    Shell = C.Padding;
                    ShellDark = C.ClothDark;
                    Under = C.Lining;
                    Edge = Genes.Chance("edge.leather", 0.5f) ? C.LeatherDark : C.Trim;
                    Trim = C.Trim;
                    break;
                case GearMake.Hide:
                case GearMake.Hardened:
                case GearMake.Studded:
                    Shell = C.Leather;
                    ShellDark = C.LeatherDark;
                    // What shows under leather is plain: a lining, or the cloth in its shadow, and only now and then the cloth itself.
                    int under = Genes.Pick("under", 5f, 3f, 2f);
                    Under = under == 0 ? C.Lining : under == 1 ? C.ClothDark : C.Cloth;
                    Edge = C.LeatherDark;
                    Trim = Genes.Chance("trim.fur", 0.3f) ? C.Fur : C.Trim;
                    break;
                default:
                    Shell = C.Cloth;
                    ShellDark = C.ClothDark;
                    Under = Genes.Chance("under.lining", 0.5f) ? C.Lining : C.ClothDark;
                    Edge = C.Trim;
                    Trim = Make == GearMake.Silk || look.Craft > 0.6f ? C.Precious : C.Trim;
                    break;
            }
        }

        // How it's made when its name doesn't say, by how much of each family it is: heavy that's finely made is plate, heavy that's poor is quilted, and so on.
        private static GearMake Choose(GearLook look)
        {
            Genes genes = look.Genes.Part("make");
            float craft = look.Craft;
            float heavy = look.Heavy, leather = look.Leather, cloth = look.Cloth;
            if (heavy > 0.25f && leather > 0.25f) return GearMake.Brigandine;
            if (heavy > 0.25f && cloth > 0.25f) return GearMake.Padded;
            if (heavy >= leather && heavy >= cloth)
            {
                return (GearMake)new[] { GearMake.Plate, GearMake.Scale, GearMake.Lamellar, GearMake.Mail, GearMake.Padded }.GetValue(
                    genes.Pick("heavy", 1f + craft * 3f, 1.2f, 1f + craft, 1.4f, 1.6f * (1f - craft)))!;
            }
            if (leather >= cloth) return (GearMake)new[] { GearMake.Hide, GearMake.Hardened, GearMake.Studded }.GetValue(genes.Pick("leather", 2f, 1f + craft * 1.5f, 1.2f))!;
            return craft > 0.55f && genes.Chance("silk", craft) ? GearMake.Silk : GearMake.Cloth;
        }

        /// <summary>Whether it has something, going by how likely each family is to.</summary>
        public bool Likely(string name, float heavy, float leather, float cloth) => Look.Likely(name, heavy, leather, cloth);

        /// <summary>A number that depends on the family.</summary>
        public float Blend(string name, Vector2 heavy, Vector2 leather, Vector2 cloth) => Look.Blend(name, heavy, leather, cloth);

        /// <summary>A strap round something that's round, as a belt or a leg's strap is: a band standing out a little, with a buckle on its front, and its tongue end tucked through a keeper.</summary>
        /// <remarks>
        /// It goes round in as many steps as what it's round has (24 for a body's garment, 16 for a leg or an arm), so that the two polygons run together and it never cuts in between
        /// their corners.
        /// </remarks>
        public void Belt(GearMesh mesh, Genes genes, Func<float, Vector3> around, Func<float, Vector3> outward, float halfWidth, float thick, float buckleAt, bool buckled = true, int steps = 24)
        {
            int Steps = steps;
            var path = new List<Vector3>(Steps + 1);
            var normals = new List<Vector3>(Steps + 1);
            for (int i = 0; i <= Steps; i++)
            {
                float angle = i * (MathF.Tau / Steps);
                path.Add(around(angle));
                normals.Add(outward(angle));
            }
            mesh.Strip(path, normals, halfWidth, thick, Strap, capStart: false, capEnd: false, bevel: 0.3f);
            if (!buckled) return;
            Vector3 at = around(buckleAt) + outward(buckleAt) * thick;
            Vector3 out_ = outward(buckleAt);
            Vector3 along = (around(buckleAt + 0.05f) - around(buckleAt - 0.05f)).Normalized();
            Vector3 up = Vector3.Cross(out_, along).Normalized();
            float width = halfWidth * 2.3f;
            mesh.At(GearMesh.Basis(at, up, along, out_), () => mesh.Buckle(width, width * genes.Range("buckle.long", 0.75f, 1.1f), halfWidth * 0.14f, Fitting));
            // The tongue end, past the buckle, lying over the belt and tucked under a keeper.
            var tail = new List<Vector3>();
            var tailOut = new List<Vector3>();
            for (int i = 0; i <= 4; i++)
            {
                float angle = buckleAt + 0.04f + i * 0.06f;
                tail.Add(around(angle) + outward(angle) * thick * 1.1f);
                tailOut.Add(outward(angle));
            }
            mesh.Strip(tail, tailOut, halfWidth * 0.92f, thick * 0.9f, Strap, width: t => t > 0.85f ? 1f - (t - 0.85f) * 4f : 1f);
            float keeper = buckleAt + 0.2f;
            mesh.Strip(new[] { around(keeper - 0.012f) + outward(keeper) * thick * 2f, around(keeper + 0.012f) + outward(keeper) * thick * 2f }, new[] { outward(keeper), outward(keeper) }, halfWidth * 1.15f, thick * 0.8f, Strap);
        }
    }
}

using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>
    /// What one piece of armour is made of and how it's made, from how much of each family it is. This is the structure in the randomness: heavy is thick and metal with big shapes, leather sits
    /// close and is held on by straps, cloth hangs and carries colour. Each is a share and not a choice, so something half heavy and half leather is plates riveted to leather, and nothing
    /// anywhere asks which family a thing is. Whatever a piece has, it has because <see cref="Likely"/> said so, by shares.
    /// </summary>
    internal sealed class ArmourCut
    {
        public readonly GearStyle Style;
        public readonly Genes Genes;
        public readonly float Heavy;
        public readonly float Leather;
        public readonly float Cloth;

        public readonly VertexMaterial Main;        // What most of it is.
        public readonly VertexMaterial Second;      // What sets that off.
        public readonly VertexMaterial Under;       // What shows between and beneath.
        public readonly VertexMaterial Strap;       // What holds it on.
        public readonly VertexMaterial Plate;       // Metal laid over it.
        public readonly VertexMaterial Fitting;     // Buckles, rivets, trim.

        public ArmourCut(GearStyle style)
        {
            Style = style;
            Genes = style.Genes;
            GearSpec spec = style.Spec;
            GearPalette palette = style.Palette;

            // Something with no family at all, from an item older than any of this, is plain leather.
            float total = spec.Heavy + spec.Leather + spec.Cloth;
            Heavy = total > 0f ? spec.Heavy / total : 0f;
            Leather = total > 0f ? spec.Leather / total : 1f;
            Cloth = total > 0f ? spec.Cloth / total : 0f;

            Plate = palette.Metal;
            Fitting = palette.Fitting;
            Strap = palette.Binding;

            if (Heavy >= Leather && Heavy >= Cloth && Heavy > 0.6f)
            {
                Main = palette.Metal;
                Second = palette.Fitting;
                Under = palette.Mail;
            }
            else if (Cloth > Leather)
            {
                Main = palette.Cloth;
                Second = palette.Trim;
                Under = new VertexMaterial(GearMaterials.Cloth, Shade(palette.Cloth.colour, 0.6f));
            }
            else
            {
                Main = palette.Leather;
                Second = palette.Binding;
                Under = new VertexMaterial(GearMaterials.Cloth, Shade(palette.Cloth.colour, 0.55f));
            }
        }

        public static Colour Shade(Colour colour, float by) => new Colour(colour.Red * by, colour.Green * by, colour.Blue * by);

        /// <summary>
        /// How something's surface is divided, from 0 to 1 along it and 0 to 1 around it, with the front at a quarter. All of it is drawn, so bands land where they land. It costs no geometry, and
        /// it's most of what sets one close-fitting piece apart from another: armour has to follow a body, so only so much of it can show in its outline.
        /// </summary>
        public Func<float, float, VertexMaterial> Pattern(Genes genes)
        {
            VertexMaterial main = Main;
            VertexMaterial second = Second;
            VertexMaterial third = genes.Chance("third.under", 0.5f) ? Under : Strap;
            int kind = genes.Pick("kind", 2.5f, 2.5f, 2f, 2f, 1.5f + Style.Ornate, 1.5f);
            int bands = genes.Count("bands", 2, 6);
            float width = genes.Range("width", 0.16f, 0.5f);
            float at = genes.Range("at", 0.2f, 0.8f);
            int stripes = genes.Count("stripes", 2, 7);
            float fade = genes.Range("fade", 0f, 0.5f);
            bool alternate = genes.Chance("alternate", 0.4f);

            return kind switch
            {
                1 => (along, around) => (int)(along * bands) % 2 == 0 ? main : alternate ? third : second,                                      // Bands.
                2 => (along, around) => (int)(around * stripes * 2f) % 2 == 0 ? main : second,                                                  // Stripes.
                3 => (along, around) => along > at ? second : main,                                                                             // A yoke.
                4 => (along, around) => around is > 0.25f and < 0.75f ? main : second,                                                          // Parti-coloured.
                5 => (along, around) => (int)(along * bands) % 2 == 0 == ((int)(around * stripes * 2f) % 2 == 0) ? main : third,                // Chequered.
                _ => (along, around) => MathF.Abs(around - 0.25f) < width * 0.25f && along > fade ? second : main,                              // A panel down the front.
            };
        }

        /// <summary>Whether it has something, going by how likely each family is to.</summary>
        public bool Likely(string name, float heavy, float leather, float cloth) => Genes.Chance(name, Heavy * heavy + Leather * leather + Cloth * cloth);

        /// <summary>A number that depends on the family, and is drawn from within what that family's range comes to.</summary>
        public float Blend(string name, Vector2 heavy, Vector2 leather, Vector2 cloth)
        {
            float low = Heavy * heavy.X + Leather * leather.X + Cloth * cloth.X;
            float high = Heavy * heavy.Y + Leather * leather.Y + Cloth * cloth.Y;
            return Genes.Range(name, low, high);
        }

        /// <summary>Where a rivet or a stud sits, out from a surface along a normal.</summary>
        public static Matrix4 FacingOut(Vector3 at, Vector3 normal)
        {
            Vector3 axis = Vector3.Cross(Vector3.UnitY, normal);
            if (axis.LengthSquared < 1e-6f) return Matrix4.CreateTranslation(at);
            return Matrix4.CreateFromAxisAngle(axis.Normalized(), MathF.Acos(Math.Clamp(normal.Y, -1f, 1f))) * Matrix4.CreateTranslation(at);
        }

        /// <summary>A dome that sits on a surface, for what's fixed to anything.</summary>
        public void Stud(GearMesh mesh, Vector3 at, Vector3 normal, float size, VertexMaterial material)
        {
            mesh.At(FacingOut(at, normal), () => mesh.Lathe(Section.Round(6), size * 0.7f, 2, t => size * MathF.Sqrt(MathF.Max(1f - t * t, 0.04f)), material, capStart: false));
        }

        /// <summary>A band around something, for belts and straps.</summary>
        public static void Band(GearMesh mesh, Vector3 centre, float halfWidth, float halfDepth, float height, float thick, VertexMaterial material)
        {
            Ring[] rings =
            {
                new Ring { Centre = centre - Vector3.UnitY * height, Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = halfWidth, HalfDepth = halfDepth, Hard = true },
                new Ring { Centre = centre - Vector3.UnitY * height * 0.9f, Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = halfWidth + thick, HalfDepth = halfDepth + thick },
                new Ring { Centre = centre + Vector3.UnitY * height * 0.9f, Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = halfWidth + thick, HalfDepth = halfDepth + thick },
                new Ring { Centre = centre + Vector3.UnitY * height, Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = halfWidth, HalfDepth = halfDepth, Hard = true },
            };
            mesh.Loft(Section.Round(18), rings, material, capStart: false, capEnd: false);
        }

        /// <summary>A buckle, on the front of whatever's at <paramref name="at"/>.</summary>
        public void Buckle(GearMesh mesh, Genes genes, Vector3 at, float size)
        {
            mesh.At(Matrix4.CreateRotationX(MathF.PI / 2f) * Matrix4.CreateTranslation(at), () =>
            {
                mesh.Lathe(genes.Chance("buckle.round", 0.5f) ? Section.Round(10) : Section.Slab(0.3f), size * 0.5f, 1, _ => size, Fitting, squash: genes.Range("buckle.squash", 0.7f, 1.1f));
            });
        }
    }
}

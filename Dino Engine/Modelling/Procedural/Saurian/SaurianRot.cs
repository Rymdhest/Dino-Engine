using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.Modelling.Procedural.Gear;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Saurian
{
    /// <summary>Where a rotting body has opened up.</summary>
    public readonly record struct Wound(Vector3 Centre, Vector3 Radii, Vector3 Forward, Vector3 Up, float Along, bool OnTrunk);

    /// <summary>
    /// What the Reich's failures look like: animals that hatched dying, or were raised before the blood took, kept on their feet by the Ossuary's iron and bile. Their hide has gone grey and green and
    /// split open where it's rotted through, with ribs showing in the worst of it, and there's a sick light in their eyes. An iron collar, a glass of glowing bile on the back and brass tubes into the
    /// neck are what keep them going, and the first thing a player should see of one.
    /// </summary>
    public static class SaurianRot
    {
        private static readonly Vector3 Glow = new Vector3(0.55f, 0.85f, 0.12f);
        private static readonly Vector3 Raw = new Vector3(0.22f, 0.04f, 0.04f);
        private static readonly Vector3 Deep = new Vector3(0.07f, 0.012f, 0.015f);
        private static readonly Vector3 Iron = new Vector3(0.09f, 0.09f, 0.1f);
        private static readonly Vector3 Brass = new Vector3(0.42f, 0.3f, 0.11f);
        private static readonly Vector3 Bone = new Vector3(0.4f, 0.35f, 0.26f);

        /// <summary>A living colour as it looks once it's started to go: drained, greyed and gone green, with bruising in patches.</summary>
        public static Vector3 Sicken(Vector3 colour, Vector3 p, uint seed, float size)
        {
            float grey = (colour.X + colour.Y + colour.Z) / 3f;
            Vector3 drained = Vector3.Lerp(colour, new Vector3(grey), 0.65f);
            Vector3 rotten = drained * new Vector3(0.85f, 1.02f, 0.8f) + new Vector3(0.012f, 0.02f, 0.008f);
            float bruise = FloraNoise.Value(p / (size * 0.04f), seed + 71u);
            return Vector3.Lerp(rotten, new Vector3(0.09f, 0.05f, 0.1f), MathF.Max(bruise - 0.62f, 0f) * 1.6f);
        }

        /// <summary>Where it's rotted through: three to five wounds, mostly on the flanks where there's most of it to rot, carved into the flesh.</summary>
        public static void Wound(SaurianField field, SaurianBody body)
        {
            Genes genes = body.Genes.Part("rot");
            SaurianAxis axis = body.Axis;
            int count = genes.Count("wounds", 4, 6);
            for (int i = 0; i < count; i++)
            {
                Genes g = genes.Part(i);
                int where = g.Pick("where", 0.55f, 0.2f, 0.25f);
                float along = where switch
                {
                    0 => body.HipAt + (body.ChestAt - body.HipAt) * g.Range("trunk", 0.2f, 0.85f),
                    1 => body.NeckAt + (body.HeadAt - body.NeckAt) * g.Range("neck", 0.15f, 0.7f),
                    _ => body.HipAt * g.Range("tail", 0.55f, 0.92f),
                };
                Vector3 way = axis.Way(along);
                Vector3 up = (Vector3.UnitY - way * Vector3.Dot(Vector3.UnitY, way)).Normalized();
                Vector3 side = Vector3.Cross(up, way) * g.Sign("side");
                float deep = axis.DeepAt(along), wide = axis.WideAt(along);
                float height = g.Range("height", -0.35f, 0.55f);
                Vector3 centre = axis.At(along) + up * (height * deep) + side * (wide * MathF.Sqrt(MathF.Max(1f - height * height, 0.2f)) * 0.95f);
                float size = g.Range("size", 0.8f, 1.25f);
                var radii = new Vector3(deep * 0.52f, deep * 0.46f, deep * 0.82f) * size;
                var wound = new Wound(centre, radii, way, up, along, where == 0);
                body.Wounds.Add(wound);
                field.Ellipsoid(SaurianPart.Body, centre, radii, deep * 0.08f, way, up, carve: true);
            }

            // One side of the face has gone, down to the teeth.
            SaurianHead head = body.Head;
            float cheek = head.Length * genes.Range("cheek", 0.42f, 0.62f);
            float sideOf = genes.Sign("cheek side");
            Vector3 hole = new Vector3(sideOf * head.WidthAt(cheek) * 0.95f, head.Lip(cheek) + head.DepthAt(cheek) * 0.35f, cheek);
            Vector3 hollow = new Vector3(head.WidthAt(cheek) * 0.45f, head.DepthAt(cheek) * 0.42f, head.Length * 0.14f);
            field.Ellipsoid(SaurianPart.Head, hole, hollow, head.Length * 0.02f, inHead: true, carve: true);
            body.Wounds.Add(new Wound(head.ToBody(hole), hollow, head.Forward, head.Up, body.HeadAt + cheek, false));
        }

        /// <summary>How far into a wound a point is: 1 in the middle of one, 0 at its edge and beyond.</summary>
        private static float Into(SaurianBody body, Vector3 p, out float rim)
        {
            float deepest = 0f;
            rim = 0f;
            foreach (Wound wound in body.Wounds)
            {
                Vector3 side = Vector3.Cross(wound.Up, wound.Forward);
                Vector3 d = p - wound.Centre;
                Vector3 local = new Vector3(Vector3.Dot(d, side) / wound.Radii.X, Vector3.Dot(d, wound.Up) / wound.Radii.Y, Vector3.Dot(d, wound.Forward) / wound.Radii.Z);
                float reach = local.Length;
                deepest = MathF.Max(deepest, 1f - Math.Clamp(reach, 0f, 1f));
                rim = MathF.Max(rim, 1f - Math.Clamp((reach - 1f) / 0.5f, 0f, 1f));
            }
            return deepest;
        }

        public sealed class Dress : SaurianDress
        {
            private readonly SaurianPaint _paint;

            public Dress(SaurianBody body, SaurianPaint paint) : base(body, paint) => _paint = paint;

            public override Vector3 Colour(Vector3 p, Vector3 normal, SaurianPart part, out int material, out float glow)
            {
                Vector3 colour = Sicken(base.Colour(p, normal, part, out material, out glow), p, _paint.Coat.Seed, Body.Length);
                float into = Into(Body, p, out float rim);

                // Raw at the edge of a wound, dark as old blood in the depths of it, and bruised for a way round it.
                if (into > 0f) return Vector3.Lerp(Raw, Deep, MathF.Min(into * 2.5f, 1f));
                return Vector3.Lerp(colour, colour * new Vector3(0.6f, 0.45f, 0.55f), rim * 0.8f);
            }
        }

        public static void Eyes(SaurianMesh mesh, SaurianBody body) => SaurianParts.Eyes(mesh, body, Glow, 2.4f, GearMaterials.Glow);

        /// <summary>Ribs showing where the flank has gone, and what keeps it going: an iron collar, a glass of glowing bile on its back, brass tubes from that into its neck, and bolts in its skull.</summary>
        public static void Bare(SaurianMesh mesh, SaurianBody body, SaurianDress dress)
        {
            SaurianAxis axis = body.Axis;
            foreach (Wound wound in body.Wounds)
            {
                if (!wound.OnTrunk) continue;
                Vector3 side = Vector3.Cross(wound.Up, wound.Forward);
                Vector3 inward = (axis.At(wound.Along) - wound.Centre).Normalized();
                float thick = wound.Radii.Y * 0.09f;
                for (int r = -1; r <= 1; r++)
                {
                    Vector3 middle = wound.Centre + wound.Forward * (r * wound.Radii.Z * 0.42f) + inward * (wound.Radii.X * 0.55f);
                    Vector3 top = middle + wound.Up * wound.Radii.Y * 0.95f + inward * (wound.Radii.X * 0.15f);
                    Vector3 bottom = middle - wound.Up * wound.Radii.Y * 0.95f + inward * (wound.Radii.X * 0.15f) - wound.Forward * wound.Radii.Z * 0.12f;
                    mesh.Tube(new[] { top, middle, bottom }, new[] { thick, thick * 1.1f, thick * 0.9f }, 5, Bone, GearMaterials.Bone, dress.Along(wound.Along));
                }
            }

            Apparatus(mesh, body, dress);
        }

        private static void Apparatus(SaurianMesh mesh, SaurianBody body, SaurianDress dress)
        {
            SaurianAxis axis = body.Axis;

            // A collar round the root of the neck, bolted.
            float collar = body.NeckAt + (body.NeckTopAt - body.NeckAt) * 0.3f;
            Band(mesh, axis, collar, 1.04f, axis.DeepAt(collar) * 0.11f, Iron, GearMaterials.Metal, dress.Along(collar), bolts: 7, Brass);

            // A glass of bile on its back, over the shoulders, in an iron cage, glowing.
            float at = body.SpineAt + (body.ChestAt - body.SpineAt) * 0.45f;
            Vector3 way = axis.Way(at);
            Vector3 up = (Vector3.UnitY - way * Vector3.Dot(Vector3.UnitY, way)).Normalized();
            float deep = axis.DeepAt(at);
            Vector3 seat = axis.At(at) + up * (deep * 0.95f);
            float tall = deep * 0.75f, round = deep * 0.24f;
            Hang hang = dress.Along(at);
            mesh.Tube(new[] { seat + up * tall * 0.08f, seat + up * tall * 0.9f }, new[] { round * 0.92f, round * 0.92f }, 10, Glow, GearMaterials.Glow, hang, glow: 1.8f);
            mesh.Tube(new[] { seat - up * tall * 0.05f, seat + up * tall * 0.1f }, new[] { round * 1.12f, round * 1.12f }, 10, Iron, GearMaterials.Metal, hang);
            mesh.Tube(new[] { seat + up * tall * 0.88f, seat + up * tall * 1.02f }, new[] { round * 1.12f, round * 0.7f }, 10, Iron, GearMaterials.Metal, hang);
            Vector3 across = Vector3.Cross(up, way);
            for (int k = 0; k < 4; k++)
            {
                float turn = k * MathF.Tau / 4f + 0.4f;
                Vector3 out_ = across * MathF.Cos(turn) + way * MathF.Sin(turn);
                mesh.Tube(new[] { seat + out_ * round * 1.05f, seat + out_ * round * 1.05f + up * tall }, new[] { round * 0.07f, round * 0.07f }, 4, Iron, GearMaterials.Metal, hang);
            }

            // Two brass tubes from the top of it down into the neck, behind the collar.
            Vector3 tap = seat + up * tall;
            for (int s = -1; s <= 1; s += 2)
            {
                var path = new List<Vector3>();
                var radii = new List<float>();
                const int Steps = 7;
                for (int k = 0; k <= Steps; k++)
                {
                    float t = k / (float)Steps;
                    float along = at + (collar - at) * t;
                    Vector3 surface = axis.At(along) + Vector3.UnitY * (axis.DeepAt(along) * 1.03f) + Vector3.Cross(Vector3.UnitY, axis.Way(along)) * (s * axis.WideAt(along) * 0.35f);
                    Vector3 lift = Vector3.UnitY * (MathF.Sin(t * MathF.PI) * deep * 0.25f);
                    path.Add(Vector3.Lerp(tap + across * (s * round * 0.4f), surface, MathF.Pow(t, 0.6f)) + lift);
                    radii.Add(round * 0.13f);
                }
                mesh.Tube(path, radii, 5, Brass, GearMaterials.Metal, dress.Along(at + (collar - at) * 0.5f));
            }

            // Bolts in the skull, where the Ossuary went in.
            SaurianHead head = body.Head;
            for (int s = -1; s <= 1; s += 2)
            {
                float z = head.Length * 0.2f;
                Vector3 root = head.ToBody(new Vector3(s * head.WidthAt(z) * 0.5f, head.Lip(z) + head.DepthAt(z) * 1.2f, z));
                Vector3 out_ = head.ToBody(new Vector3(s * head.WidthAt(z) * 0.5f, head.Lip(z) + head.DepthAt(z) * 1.2f, z) + new Vector3(s * 0.35f, 1f, 0f)) - root;
                mesh.Tube(new[] { root, root + out_.Normalized() * head.Length * 0.1f }, new[] { head.Length * 0.03f, head.Length * 0.03f }, 6, Iron, GearMaterials.Metal, BoneType.Head);
                mesh.Ball(root + out_.Normalized() * head.Length * 0.11f, new Vector3(head.Length * 0.045f), 1, Brass, GearMaterials.Metal, BoneType.Head);
            }
        }

        /// <summary>A band right round the body at a place along it, a little outside its skin, with bolts on it if asked.</summary>
        public static void Band(SaurianMesh mesh, SaurianAxis axis, float along, float outside, float thick, Vector3 colour, int material, Hang hang, int bolts = 0, Vector3 boltColour = default)
        {
            Vector3 centre = axis.At(along);
            Vector3 way = axis.Way(along);
            Vector3 up = (Vector3.UnitY - way * Vector3.Dot(Vector3.UnitY, way)).Normalized();
            Vector3 across = Vector3.Cross(up, way);
            float deep = axis.DeepAt(along) * outside, wide = axis.WideAt(along) * outside;
            const int Steps = 16;
            var path = new List<Vector3>();
            var radii = new List<float>();
            for (int k = 0; k <= Steps; k++)
            {
                float a = k * MathF.Tau / Steps;
                path.Add(centre + up * (MathF.Cos(a) * deep) + across * (MathF.Sin(a) * wide));
                radii.Add(thick);
            }
            mesh.Tube(path, radii, 5, colour, material, hang, way, 0.45f);
            for (int b = 0; b < bolts; b++)
            {
                float a = (b + 0.5f) * MathF.Tau / bolts;
                Vector3 at = centre + up * (MathF.Cos(a) * (deep + thick * 0.4f)) + across * (MathF.Sin(a) * (wide + thick * 0.4f));
                mesh.Ball(at, new Vector3(thick * 0.55f), 1, boltColour, material, hang);
            }
        }
    }
}

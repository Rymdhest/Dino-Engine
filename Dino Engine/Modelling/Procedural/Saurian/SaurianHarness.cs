using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Procedural.Gear;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Saurian
{
    /// <summary>
    /// What the Saurian Reich puts on what it's bred: an iron collar with its bolts, leather straps round the chest and belly joined down the back, and on the big ones an iron mask over the snout,
    /// spikes, and a plate on the back carrying a pole with the Reich's pennant on it, oxblood with a tyrant's skull in brass. Black iron, oxblood and brass are the Reich's colours, and nothing else wears
    /// them. Read LORE.md.
    /// </summary>
    public static class SaurianHarness
    {
        public static readonly Vector3 Iron = new Vector3(0.075f, 0.075f, 0.085f);
        public static readonly Vector3 Oxblood = new Vector3(0.3f, 0.028f, 0.03f);
        public static readonly Vector3 Brass = new Vector3(0.44f, 0.32f, 0.12f);
        public static readonly Vector3 Strap = new Vector3(0.15f, 0.022f, 0.022f);   // Oxblood leather, so a war-beast is plainly the Reich's from across a field.

        public static void Fit(SaurianMesh mesh, SaurianBody body)
        {
            var dress = new SaurianDress(body, new SaurianPaint(body));
            SaurianAxis axis = body.Axis;
            bool big = body.Length >= 10f;

            float collar = body.NeckAt + (body.NeckTopAt - body.NeckAt) * 0.28f;
            SaurianRot.Band(mesh, axis, collar, 1.03f, axis.DeepAt(collar) * 0.1f, Iron, GearMaterials.Metal, dress.Along(collar), bolts: 8, Brass);

            if (!body.OnFour)
            {
                float chest = body.ChestAt - (body.ChestAt - body.SpineAt) * 0.25f;
                float girth = body.HipAt + (body.SpineAt - body.HipAt) * 0.9f;
                SaurianRot.Band(mesh, axis, chest, 1.02f, axis.DeepAt(chest) * 0.06f, Strap, GearMaterials.Leather, dress.Along(chest));
                SaurianRot.Band(mesh, axis, girth, 1.02f, axis.DeepAt(girth) * 0.06f, Strap, GearMaterials.Leather, dress.Along(girth));
                Spine(mesh, body, dress, girth, collar);
            }
            else
            {
                float girth = body.HipAt + (body.ChestAt - body.HipAt) * 0.55f;
                SaurianRot.Band(mesh, axis, girth, 1.02f, axis.DeepAt(girth) * 0.05f, Strap, GearMaterials.Leather, dress.Along(girth));
                Spine(mesh, body, dress, girth, collar);
            }

            if (!big) return;
            Mask(mesh, body);
            Standard(mesh, body, dress);
        }

        // A strap down the middle of the back from the girth to the collar, with a buckle where it meets each.
        private static void Spine(SaurianMesh mesh, SaurianBody body, SaurianDress dress, float from, float to)
        {
            SaurianAxis axis = body.Axis;
            const int Steps = 10;
            var path = new List<Vector3>();
            var radii = new List<float>();
            for (int k = 0; k <= Steps; k++)
            {
                float along = from + (to - from) * k / Steps;
                path.Add(axis.At(along) + Vector3.UnitY * (axis.DeepAt(along) * 1.03f));
                radii.Add(axis.DeepAt(along) * 0.07f);
            }
            mesh.Tube(path, radii, 5, Strap, GearMaterials.Leather, dress.Along((from + to) * 0.5f), Vector3.UnitY, 0.3f);
            foreach (float at in new[] { from, to })
            {
                Vector3 buckle = axis.At(at) + Vector3.UnitY * (axis.DeepAt(at) * 1.07f);
                mesh.Ball(buckle, new Vector3(axis.DeepAt(at) * 0.12f, axis.DeepAt(at) * 0.05f, axis.DeepAt(at) * 0.1f), 1, Brass, GearMaterials.Metal, dress.Along(at));
            }
        }

        // An iron mask over the bridge of the snout, riveted, with spikes over the brows. A horned grazer has its horns capped in iron instead.
        private static void Mask(SaurianMesh mesh, SaurianBody body)
        {
            SaurianHead head = body.Head;
            if (body.OnFour)
            {
                float eyeZ = head.EyeAt * head.Length;
                float top = head.Lip(eyeZ) + head.DepthAt(eyeZ) * 1.24f;
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 root = new Vector3(side * head.WidthAt(eyeZ) * 0.6f, top - head.BrowHorns * 0.08f, eyeZ - head.EyeSize * 0.6f);
                    Vector3 way = new Vector3(side * 0.18f, 0.55f, 0.82f).Normalized();
                    Vector3 cap = head.ToBody(root + way * head.BrowHorns * 0.72f);
                    Vector3 tip = head.ToBody(root + way * head.BrowHorns * 1.08f);
                    mesh.Tube(new[] { cap, tip }, new[] { head.BrowHorns * 0.075f, 0f }, 6, Iron, GearMaterials.Metal, BoneType.Head);
                }
                return;
            }

            var path = new List<Vector3>();
            var radii = new List<float>();
            for (int k = 0; k <= 6; k++)
            {
                float z = head.Length * (0.32f + 0.4f * k / 6f);
                float top = head.Lip(z) + head.DepthAt(z) * 1.24f;
                path.Add(head.ToBody(new Vector3(0f, top + head.Length * 0.006f, z)));
                radii.Add(head.WidthAt(z) * 0.82f);
            }
            mesh.Tube(path, radii, 8, Iron, GearMaterials.Metal, BoneType.Head, head.Up, 0.22f);

            for (int k = 0; k < 5; k++)
            {
                float z = head.Length * (0.36f + 0.32f * k / 4f);
                float top = head.Lip(z) + head.DepthAt(z) * 1.24f;
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 rivet = head.ToBody(new Vector3(side * head.WidthAt(z) * 0.7f, top + head.Length * 0.012f, z));
                    mesh.Ball(rivet, new Vector3(head.Length * 0.014f), 1, Brass, GearMaterials.Metal, BoneType.Head);
                }
            }

            float browZ = head.EyeAt * head.Length;
            float brow = head.Lip(browZ) + head.DepthAt(browZ) * 1.24f;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 root = new Vector3(side * head.WidthAt(browZ) * 0.6f, brow, browZ - head.EyeSize * 0.4f);
                Vector3 rootBody = head.ToBody(root);
                Vector3 up = head.ToBody(root + new Vector3(side * 0.35f, 1f, -0.25f)) - rootBody;
                mesh.Spike(rootBody, up, head.Length * 0.13f, head.Length * 0.035f, -head.Forward, 0.25f, 6, Iron, GearMaterials.Metal, BoneType.Head);
            }
        }

        // A plate on the back between the shoulders and the hips, carrying a pole, and the Reich's pennant flying from it.
        private static void Standard(SaurianMesh mesh, SaurianBody body, SaurianDress dress)
        {
            SaurianAxis axis = body.Axis;
            float at = body.HipAt + (body.ChestAt - body.HipAt) * 0.55f;
            Vector3 way = axis.Way(at);
            float deep = axis.DeepAt(at);
            Hang hang = dress.Along(at);

            var plate = new List<Vector3>();
            var plateRadii = new List<float>();
            for (int k = 0; k <= 6; k++)
            {
                float along = at + deep * (k / 6f - 0.5f) * 1.6f;
                plate.Add(axis.At(along) + Vector3.UnitY * (axis.DeepAt(along) * 1.0f));
                plateRadii.Add(axis.WideAt(along) * 0.62f);
            }
            mesh.Tube(plate, plateRadii, 8, Iron, GearMaterials.Metal, hang, Vector3.UnitY, 0.18f);

            Vector3 foot = axis.At(at) + Vector3.UnitY * (deep * 1.08f);
            float tall = deep * 2.6f;
            Vector3 pole = (Vector3.UnitY - way * 0.12f).Normalized();
            Vector3 top = foot + pole * tall;
            mesh.Tube(new[] { foot, top }, new[] { deep * 0.05f, deep * 0.04f }, 6, Iron, GearMaterials.Metal, hang);
            mesh.Spike(top, pole, deep * 0.3f, deep * 0.07f, pole, 0f, 5, Brass, GearMaterials.Metal, hang);

            // The pennant streams back from the top of the pole, oxblood, with the Reich's mark on both sides of it in brass.
            float length = deep * 1.9f, width = deep * 0.75f;
            Vector3 back = -way;
            Vector3 down = -pole;
            Vector3 side = Vector3.Cross(back, down).Normalized();
            Vector3 hoist = top - pole * deep * 0.08f;
            var cloth = new List<Vector3>
            {
                hoist,
                hoist + back * length * 0.55f + down * width * 0.08f,
                hoist + back * length + down * width * 0.32f,
                hoist + back * length * 0.62f + down * width * 0.55f,
                hoist + back * length + down * width * 0.82f,
                hoist + back * length * 0.5f + down * width,
                hoist + down * width,
            };
            Vector3 middle = hoist + back * length * 0.42f + down * width * 0.5f;
            mesh.Sheet(cloth, middle, side, deep * 0.02f, Oxblood, Oxblood * 0.8f, GearMaterials.Cloth, _ => hang);

            Vector3 mark = hoist + back * length * 0.3f + down * width * 0.5f;
            for (int face = -1; face <= 1; face += 2) Mark(mesh, mark + side * (face * deep * 0.016f), back, -down, side * face, width * 0.62f, hang);
        }

        /// <summary>
        /// The Reich's mark: the skull of the first tyrant the Amber Institute ever raised, in profile, in brass. It looks like nothing but what it is, which matters: a curved claw on a red field read from
        /// across a battlefield as a crescent, and a flag that looks like a real country's has no place on an enemy.
        /// </summary>
        public static void Mark(SaurianMesh mesh, Vector3 centre, Vector3 right, Vector3 up, Vector3 facing, float size, Hang hang)
        {
            Vector3 At(float x, float y) => centre + right * (x * size) + up * (y * size);

            // The skull, facing the way the pennant flies from, as every banner of the Reich's carries it (ReichMark).
            var outline = new List<Vector3>(ReichMark.Skull.Length);
            foreach ((float x, float y) in ReichMark.Skull) outline.Add(At(x, y));
            mesh.Sheet(outline, At(ReichMark.Middle.X, ReichMark.Middle.Y), facing, size * 0.02f, Brass, Brass * 0.85f, GearMaterials.Metal, _ => hang);

            // Its eye and the opening in front of it, in the cloth's own colour, a hair proud of the brass.
            Vector3 proud = facing.Normalized() * size * 0.016f;
            void Hole(float x, float y, float rx, float ry)
            {
                var hole = new List<Vector3>();
                for (int k = 0; k < 10; k++)
                {
                    float a = k * MathF.Tau / 10f;
                    hole.Add(At(x + MathF.Cos(a) * rx, y + MathF.Sin(a) * ry) + proud);
                }
                mesh.Sheet(hole, At(x, y) + proud, facing, size * 0.004f, Oxblood, Oxblood, GearMaterials.Cloth, _ => hang);
            }
            foreach ((float x, float y, float rx, float ry) in ReichMark.Holes) Hole(x, y, rx, ry);
        }
    }
}

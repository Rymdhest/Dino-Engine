using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.Modelling.Procedural.Saurian;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Character
{
    public static partial class SkeletonForge
    {
        private const float BeltY = 2.2f;       // Low round the hips, under the flare of the pelvis, where a belt slides to with nothing to hold it up.

        // ---- The cold light, in each socket and behind the breastbone, where it shows through the ribs from the front and above: what the Ossuary put there, and all that moves it (LORE.md).

        private static void Glow(SaurianMesh mesh, SkeletonBody body)
        {
            Hang head = SkeletonBody.On(BoneType.Head);
            for (int s = -1; s <= 1; s += 2) mesh.Ball(EyeAt(body, s), new Vector3(0.028f * body.SkullSize), 1, SkeletonBody.Light, GearMaterials.Glow, head, 3.2f);
            mesh.Ball(new Vector3(0.05f, 3.3f, 0.04f), new Vector3(0.065f, 0.075f, 0.058f), 1, SkeletonBody.Light, GearMaterials.Glow, SkeletonBody.Back(3.3f), 2.2f);
        }

        // ---- What it was buried in and still has on.

        private static void Grave(SaurianMesh mesh, SkeletonBody body)
        {
            switch (body.Headwear)
            {
                case SkeletonHeadwear.Hood: Hood(mesh, body); break;
                case SkeletonHeadwear.Cap: Cap(mesh, body); break;
            }
            if (body.Rags) Rags(mesh, body);
            if (body.Belt) Belt(mesh, body);
            if (body.Pauldron) Pauldron(mesh, body);
            if (body.Arrow) Arrow(mesh, body);
        }

        // A hood of cloth rotted through at its hem, over the top and back of the skull and open at the face, so the sockets' light shows from under it.
        private static void Hood(SaurianMesh mesh, SkeletonBody body)
        {
            float k = body.SkullSize;
            Vector3 S = SkeletonBody.SkullAt;
            Vector3 middle = S + new Vector3(0f, 0.06f, -0.05f) * k;
            var hood = new SaurianField();
            const SaurianPart Part = SaurianPart.Head;
            hood.Ellipsoid(Part, middle, new Vector3(0.31f, 0.33f, 0.37f) * k, 0.02f);
            // Down the sides and the back to below the jaw, as cloth falls, and drawn up into a soft point behind, as a hood is cut.
            hood.Cone(Part, S + new Vector3(0f, -0.02f, -0.07f) * k, S + new Vector3(0f, -0.44f, -0.1f) * k, 0.31f * k, 0.34f * k, 0.06f, 1.08f);
            hood.Ellipsoid(Part, middle + new Vector3(0f, 0.24f, -0.2f) * k, new Vector3(0.11f, 0.12f, 0.15f) * k, 0.12f, new Vector3(0f, 0.5f, -1f));
            hood.Ellipsoid(Part, middle, new Vector3(0.278f, 0.298f, 0.338f) * k, 0.01f, carve: true);
            hood.Cone(Part, S + new Vector3(0f, -0.02f, -0.07f) * k, S + new Vector3(0f, -0.5f, -0.1f) * k, 0.278f * k, 0.308f * k, 0.01f, 1.08f, carve: true);
            hood.Ellipsoid(Part, S + new Vector3(0f, -0.14f, 0.3f) * k, new Vector3(0.235f, 0.33f, 0.28f) * k, 0.02f, carve: true);
            hood.Ellipsoid(Part, S + new Vector3(0f, -0.72f, 0f) * k, new Vector3(0.7f, 0.28f, 0.7f) * k, 0.015f, carve: true);

            // Bites out of its hem, where it's rotted away.
            var rot = body.Genes.Part("hood").Stream("rot");
            int bites = rot.Int(3, 7);
            for (int i = 0; i < bites; i++)
            {
                float a = rot.Range(-2.6f, 2.6f) + MathF.PI;
                Vector3 at = S + new Vector3(MathF.Sin(a) * 0.33f, -0.44f + rot.Range(0f, 0.05f), -0.1f + MathF.Cos(a) * 0.33f) * k;
                hood.Ellipsoid(Part, at, new Vector3(rot.Range(0.04f, 0.07f)) * k, 0.01f, carve: true);
            }

            Vector3 cloth = body.Cloth;
            Hang head = SkeletonBody.On(BoneType.Head);
            var dress = new Dresser(GearMaterials.Cloth, (p, n) => cloth * (0.82f + 0.18f * MathF.Max(n.Y, 0f)), _ => head);
            SaurianSkin.Grow(mesh, hood, 0.008f * k, 650, float.PositiveInfinity, dress, settle: true);
        }

        // A plain iron cap gone to rust, close over the top of the skull, with a band round its rim.
        private static void Cap(SaurianMesh mesh, SkeletonBody body)
        {
            float k = body.SkullSize;
            Vector3 S = SkeletonBody.SkullAt;
            Hang head = SkeletonBody.On(BoneType.Head);
            Vector3 Up(float y) => S + new Vector3(0f, y, -0.04f) * k;

            // Turned about the up, its radii front to back, and narrower across, as a head is (a tube along y is squashed across x).
            float[] heights = { 0.14f, 0.17f, 0.23f, 0.29f, 0.335f, 0.362f };
            float[] radii = { 0.33f, 0.325f, 0.305f, 0.25f, 0.155f, 0.03f };
            var path = new Vector3[heights.Length];
            var widths = new float[heights.Length];
            for (int i = 0; i < heights.Length; i++)
            {
                path[i] = Up(heights[i]);
                widths[i] = radii[i] * k;
            }
            mesh.Tube(path, widths, 14, body.Rust, GearMaterials.Metal, head, null, 0.86f);
            mesh.Tube(new[] { Up(0.125f), Up(0.145f), Up(0.165f) }, new[] { 0.338f * k, 0.345f * k, 0.338f * k }, 14, body.Rust * 0.8f, GearMaterials.Metal, head, null, 0.86f);
        }

        // Strips of a shroud, front and back, hanging from the hips to the thighs and torn at their ends, on the legs the further down they go, as a skirt is (BodyForm.Skirt).
        private static void Rags(SaurianMesh mesh, SkeletonBody body)
        {
            var tear = body.Genes.Part("rags").Stream("tear");
            Vector3 cloth = body.Cloth;
            for (int back = 0; back < 2; back++)
            {
                float half = back == 0 ? 0.16f : 0.2f;
                float z = back == 0 ? 0.24f : -0.31f;
                float lean = back == 0 ? 0.06f : -0.06f;
                float top = BeltY + 0.04f;
                float hem = tear.Range(1.5f, 1.68f);

                // Across the top, then back along a torn hem.
                var outline = new List<Vector3> { new Vector3(-half, top, z), new Vector3(half, top, z) };
                const int Tatters = 7;
                for (int i = 0; i <= Tatters; i++)
                {
                    float x = half * (1f - 2f * i / Tatters) * (i == 0 || i == Tatters ? 1.05f : 1f);
                    float y = i == 0 || i == Tatters ? top - tear.Range(0.3f, 0.45f) : hem + (i % 2 == 0 ? tear.Range(0.12f, 0.24f) : tear.Range(-0.06f, 0.04f));
                    outline.Add(new Vector3(x, y, z));
                }
                for (int i = 0; i < outline.Count; i++)
                {
                    Vector3 p = outline[i];
                    outline[i] = new Vector3(p.X, p.Y, p.Z + (top - p.Y) * lean);
                }
                Vector3 middle = new Vector3(0f, (top + hem) * 0.5f + 0.1f, z + (top - hem) * 0.5f * lean);
                Vector3 facing = new Vector3(0f, lean, back == 0 ? 1f : -1f).Normalized();
                Vector3 colour = cloth * tear.Range(0.85f, 1.05f);
                mesh.Sheet(outline, middle, facing, 0.018f, colour, colour * 0.75f, GearMaterials.Cloth, p => SkeletonBody.From(BodyForm.Skirt(p)));
            }
        }

        // A leather belt slid down onto the hips, its buckle in front.
        private static void Belt(SaurianMesh mesh, SkeletonBody body)
        {
            Hang hang = SkeletonBody.On(BoneType.Pelvis);
            const int Steps = 20;
            var path = new Vector3[Steps + 1];
            var radii = new float[Steps + 1];
            for (int i = 0; i <= Steps; i++)
            {
                float a = i * MathF.Tau / Steps;
                float z = MathF.Cos(a);
                path[i] = new Vector3(MathF.Sin(a) * 0.47f, BeltY - 0.02f * z, z * (z > 0f ? 0.27f : 0.31f));
                radii[i] = 0.013f;
            }
            // A band: thin out from the hips and tall up them.
            mesh.Tube(path, radii, 6, body.Leather, GearMaterials.Leather, hang, Vector3.UnitY, 2.4f);
            mesh.Ball(new Vector3(0f, BeltY - 0.02f, 0.285f), new Vector3(0.045f, 0.04f, 0.016f), 1, body.Rust, GearMaterials.Metal, hang);
        }

        // A rusted plate or two from a coat it was buried in, still on the shoulder of the arm a shield was on.
        private static void Pauldron(SaurianMesh mesh, SkeletonBody body)
        {
            Hang hang = SkeletonBody.On(BoneType.UpperArmL);
            float y = SkeletonBody.ShoulderY;
            mesh.Ball(new Vector3(0.5f, y + 0.07f, -0.005f), new Vector3(0.15f, 0.095f, 0.15f), 1, body.Rust, GearMaterials.Metal, hang);
            mesh.Ball(new Vector3(0.62f, y + 0.02f, 0f), new Vector3(0.1f, 0.08f, 0.13f), 1, body.Rust * 0.85f, GearMaterials.Metal, hang);
        }

        // What killed it, maybe: an arrow broken off in its chest, its head among the ribs and its shaft out through the front.
        private static void Arrow(SaurianMesh mesh, SkeletonBody body)
        {
            var aim = body.Genes.Part("arrow");
            float side = aim.Sign("side");
            Vector3 head = new Vector3(side * aim.Range("x", 0.06f, 0.18f), aim.Range("y", 3.15f, 3.45f), aim.Range("z", -0.05f, 0.06f));
            Vector3 way = new Vector3(side * aim.Range("across", 0.15f, 0.45f), aim.Range("rise", 0.1f, 0.4f), 1f).Normalized();
            Hang hang = SkeletonBody.Back(head.Y);
            float length = aim.Range("length", 0.45f, 0.6f);
            mesh.Tube(new[] { head, head + way * (length * 0.6f), head + way * length }, new[] { 0.018f, 0.018f, 0.011f }, 5, new Vector3(0.3f, 0.22f, 0.14f), GearMaterials.Timber, hang);
            mesh.Spike(head + way * 0.02f, -way, 0.08f, 0.03f, Vector3.UnitY, 0f, 4, body.Rust, GearMaterials.Metal, hang);
        }
    }
}

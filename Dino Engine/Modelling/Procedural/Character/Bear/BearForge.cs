using Dino_Engine.Animation;
using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.Modelling.Procedural.Saurian;
using Dino_Engine.Rendering.Shells;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Character
{
    /// <summary>
    /// Grows cave bears, as the boars, the wolves and the dinosaurs are grown: a body drawn from a seed (<see cref="BearBody"/>), its flesh a field (<see cref="BearSculpt"/>), a skin grown from that
    /// by surface nets and quadric decimation (<see cref="SaurianSkin"/>), and what's too small or too thin for a field put on after: the eyes, the ears, the claws and the stub of a tail. It stands on
    /// the same bones as people and every other animal, and its coat is a bear's long fur (<see cref="ShellStyles.BearFur"/>). Nothing is stored and nothing touches a GPU: it's safe on any thread,
    /// and a lab can grow one with no window.
    /// </summary>
    public static class BearForge
    {
        private const int Budget = 7000;                // Triangles in the skin: it's the biggest of them, and what leads a pack.
        private const float Smallest = 0.045f;          // Nothing smaller than this is made: it would be under a pixel.

        public static Rig RigOf(BearSpec spec) => new BearBody(spec).BuildRig();

        public static Mesh Make(BearSpec spec) => Make(spec, out _);

        public static Mesh Make(BearSpec spec, out BearBody body)
        {
            body = new BearBody(spec);
            var mesh = new SaurianMesh();
            SaurianField flesh = BearSculpt.Flesh(body);
            var dress = new BearDress(body);

            SaurianSkin.Grow(mesh, flesh, body.Withers / 105f, Budget, body.Length / 34f, dress, settle: true);

            Eyes(mesh, body, flesh);
            Ears(mesh, body, flesh);
            Claws(mesh, body);
            Tail(mesh, body);

            Mesh built = mesh.Build();
            built.ShellStyle = ShellStyles.BearFur;
            return built;
        }

        private static void Furred(SaurianMesh mesh, int from, float amount)
        {
            for (int i = from; i < mesh.Shells.Count; i++) mesh.Shells[i] = amount;
        }

        // Small and dark, set forward in the broad face under the brow.
        private static void Eyes(SaurianMesh mesh, BearBody body, SaurianField flesh)
        {
            float l = body.HeadLength;
            float size = l * 0.03f;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 inside = body.FromHead(new Vector3(side * l * 0.06f, l * 0.1f, l * 0.52f));
                Vector3 way = (Vector3.UnitX * side * 0.75f + body.HeadUp * 0.3f + body.HeadForward * 0.6f).Normalized();
                Vector3 skin = inside + way * flesh.Exit(inside, way, l * 0.25f);
                mesh.Ball(skin - way * size * 0.6f, new Vector3(size), 2, body.Hide.Eye, GearMaterials.Gem, BoneType.Head);
            }
        }

        // Small and round, set wide on top of the head, furred like the rest of it.
        private static void Ears(SaurianMesh mesh, BearBody body, SaurianField flesh)
        {
            float l = body.HeadLength;
            float radius = body.Ears * 0.5f;
            if (radius * 2f < Smallest) return;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 inside = body.FromHead(new Vector3(side * l * 0.18f, l * 0.16f, l * 0.16f));
                Vector3 way = (Vector3.UnitY * 0.88f + Vector3.UnitX * side * 0.47f).Normalized();
                Vector3 root = inside + way * (flesh.Exit(inside, way, l * 0.25f) - radius * 0.35f);
                Vector3 facing = (Vector3.UnitZ + Vector3.UnitX * side * 0.4f).Normalized();
                facing = (facing - way * Vector3.Dot(facing, way)).Normalized();
                Vector3 across = Vector3.Cross(facing, way).Normalized();
                Vector3 centre = root + way * radius * 0.85f;

                // A round flap: an arc over the top, closed below where it goes into the head.
                const int Arc = 9;
                var outline = new Vector3[Arc + 1];
                for (int i = 0; i < Arc; i++)
                {
                    float angle = MathF.PI * (-0.1f + 1.2f * i / (Arc - 1f));
                    outline[i] = centre + across * (MathF.Cos(angle) * radius) + way * (MathF.Sin(angle) * radius) + facing * (MathF.Sin(angle) * radius * 0.15f);
                }
                outline[Arc] = root - way * radius * 0.2f;
                int first = mesh.Positions.Count;
                mesh.Sheet(outline, centre, facing, radius * 0.12f, body.Hide.Ears, body.Hide.Ears * 0.9f, GearMaterials.Hide, _ => BoneType.Head);
                Furred(mesh, first, 1f);
            }
        }

        // Five long, curved claws on each paw, longest on the forepaws, which a bear digs and strikes with: pale horn, from the front of each toe, curving down.
        private static void Claws(SaurianMesh mesh, BearBody body)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                bool left = side > 0;
                Vector3 fore = side > 0 ? body.ForePaw : BearBody.Right(body.ForePaw);
                Vector3 hind = side > 0 ? body.HindPaw : BearBody.Right(body.HindPaw);
                Paw(mesh, body, new Vector3(fore.X, body.W(0.035f), fore.Z + body.W(0.14f)), body.ForeClaws, body.W(0.024f), left ? BoneType.HandL : BoneType.HandR);
                Paw(mesh, body, new Vector3(hind.X, body.W(0.032f), hind.Z + body.W(0.09f)), body.HindClaws, body.W(0.022f), left ? BoneType.FootL : BoneType.FootR);
            }
        }

        private static void Paw(SaurianMesh mesh, BearBody body, Vector3 middle, float length, float toe, BoneType bone)
        {
            if (length < Smallest) return;
            for (int i = -2; i <= 2; i++)
            {
                float own = length * (i == 0 ? 1f : MathF.Abs(i) == 1 ? 0.95f : 0.82f);
                Vector3 root = middle + new Vector3(i * toe * 1.05f, toe * 0.1f, toe * 0.7f - MathF.Abs(i) * toe * 0.45f);
                Vector3 way = new Vector3(i * 0.08f, -0.35f, 1f).Normalized();
                mesh.Spike(root, way, own, own * 0.2f, -Vector3.UnitY, 0.4f, 5, body.Hide.Claw, GearMaterials.Leather, bone, 0.7f, Vector3.UnitX);
            }
        }

        // The stub of a tail, furred, so all that shows of it is a tuft at the top of the rump.
        private static void Tail(SaurianMesh mesh, BearBody body)
        {
            int first = mesh.Positions.Count;
            mesh.Tube(new[] { body.TailRoot, body.TailMiddle }, new[] { body.W(0.04f), body.W(0.032f) }, 6, body.Hide.Back, GearMaterials.Hide, BoneType.TailBase);
            mesh.Tube(new[] { body.TailMiddle - (body.TailMiddle - body.TailRoot) * 0.2f, body.TailEnd }, new[] { body.W(0.032f), body.W(0.012f) }, 6, body.Hide.Back, GearMaterials.Hide, BoneType.TailTip);
            Furred(mesh, first, 1f);
        }
    }
}

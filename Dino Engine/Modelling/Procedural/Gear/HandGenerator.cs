using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Model;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>
    /// What's worn on the hands: round the fist, and a cuff up the forearm over a fitted sleeve (<see cref="GearFit.GloveInner"/>) but never as far as an elbow's plate
    /// (<see cref="GearFit.CuffReach"/>). Plate is an hourglass gauntlet with its fingers in lames; leather a glove with a flared cuff or a bracer laced on; cloth is wound round. A hand lies out
    /// along x in the bind pose, closed round whatever it holds. Each is built once a side. See GEAR.md.
    /// </summary>
    public static class HandGenerator
    {
        public static void Build(GearMesh mesh, GearLook look)
        {
            var kit = new ArmourKit(look);
            Genes genes = look.Genes;
            GearForm form = look.Spec.Form;
            float reach = MathF.Min(kit.Blend("reach", new Vector2(0.35f, 0.65f), new Vector2(0.3f, 0.65f), new Vector2(0.2f, 0.55f)) * (0.6f + form.Cover * 0.6f), GearFit.CuffReach);
            if (kit.Make == GearMake.Hardened) reach = MathF.Max(reach, genes.Range("bracer", 0.6f, GearFit.CuffReach));
            float bulk = kit.Blend("bulk", new Vector2(0.17f, 0.24f), new Vector2(0.14f, 0.19f), new Vector2(0.13f, 0.17f));
            float length = genes.Range("long", 0.32f, 0.42f);
            float flare = kit.Blend("flare", new Vector2(0.02f, 0.06f), new Vector2(0.01f, 0.05f), new Vector2(0f, 0.03f));

            foreach (float side in new[] { 1f, -1f })
            {
                mesh.Counted("fist", () => Fist(mesh, kit, genes.Part("fist"), side, bulk, length));
                if (kit.Make == GearMake.Wraps) mesh.Counted("wraps", () => Wound(mesh, kit, genes.Part("wraps"), side, reach, bulk, length));
                else mesh.Counted("cuff", () => Cuff(mesh, kit, genes.Part("cuff"), side, reach, flare));
            }
            mesh.Covers |= BodyRegions.Hands;
        }

        // The hand, closed, out along x from the wrist: a block that swells across the knuckles, its fingers in lames on a gauntlet, with a thumb along its front.
        private static void Fist(GearMesh mesh, ArmourKit kit, Genes genes, float side, float bulk, float length)
        {
            GearLook look = kit.Look;
            float from = BodyForm.WristX - 0.05f;
            int lames = kit.Make == GearMake.Plate ? genes.Count("lames", 3, 5) : 0;
            int steps = 8 + lames * 2;
            var rings = new List<Ring>();
            var bands = new List<int>();
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                float swell = MathF.Sin(MathF.Min(t * 1.35f + 0.25f, 1f) * MathF.PI * 0.5f);
                float tip = 1f - MathF.Pow(t, 5f) * 0.68f;
                bool finger = lames > 0 && t > 0.55f;
                float step = finger && i % 2 == 1 ? 0.012f : 0f;
                rings.Add(new Ring { Centre = new Vector3(side * (from + t * length), BodyForm.Shoulder, 0f), Side = Vector3.UnitZ, Front = Vector3.UnitY, HalfWidth = bulk * swell * tip + step, HalfDepth = bulk * 0.7f * swell * tip + step, Hard = finger });
                bands.Add(finger ? (i % 2) : t > 0.5f && t < 0.62f && !kit.Woven ? 2 : 0);
            }
            mesh.Bone = (int)BodyForm.Hand(side);
            mesh.Bands(Section.Super(12, 2.6f), rings, (ring, _) => bands[ring] switch { 1 => kit.ShellDark, 2 => kit.Make == GearMake.Plate ? kit.Edge : kit.ShellDark, _ => kit.Make == GearMake.Hardened ? kit.C.Leather : kit.Shell }, capStart: true, capEnd: true);

            // The thumb, along the front of the fist.
            Vector3 root = new Vector3(side * (from + length * 0.25f), BodyForm.Shoulder - bulk * 0.1f, bulk * 0.55f);
            var keys = new[] { root, root + new Vector3(side * length * 0.25f, -bulk * 0.05f, bulk * 0.25f), root + new Vector3(side * length * 0.5f, -bulk * 0.1f, bulk * 0.2f) };
            mesh.Sweep(Section.Round(8), Spine.Through(keys, 5), t => new Vector2(bulk * 0.38f * (1f - t * 0.3f)), kit.Make == GearMake.Hardened ? kit.C.Leather : kit.Shell, Vector3.UnitY, capStart: false, capEnd: true);

            // A plate over the back of a gauntlet's hand, a ridge of knuckles on a fist made to strike, and studs on a studded glove.
            if (kit.Make == GearMake.Plate)
            {
                mesh.Plate((u, v) =>
                {
                    float x = side * (from + length * (0.02f + 0.55f * u));
                    float angle = MathF.PI * (0.18f + 0.64f * v);
                    float t = 0.02f + 0.55f * u;
                    float swell = MathF.Sin(MathF.Min(t * 1.35f + 0.25f, 1f) * MathF.PI * 0.5f);
                    float r = bulk * swell + 0.012f;
                    return (new Vector3(x, BodyForm.Shoulder + MathF.Sin(angle) * r * 0.7f, MathF.Cos(angle) * r), new Vector3(0f, MathF.Sin(angle), MathF.Cos(angle)));
                }, 5, 5, 0.016f, (_, _) => kit.Shell, PlateEdge.End, 0.014f, underside: kit.ShellDark);
            }
            if (kit.Make == GearMake.Studded || look.Spikes > 0.6f && kit.Metal && genes.Chance("knuckles", look.Spikes))
            {
                for (int i = 0; i < 4; i++)
                {
                    float z = bulk * (i / 3f * 1.2f - 0.6f);
                    mesh.Rivet(new Vector3(side * (from + length * 0.6f), BodyForm.Shoulder + bulk * 0.7f, z), Vector3.UnitY, kit.Make == GearMake.Studded ? 0.024f : 0.03f, kit.Fitting);
                }
            }
        }

        // Up the forearm from the wrist, over a fitted sleeve: a gauntlet's cuff flaring out, a glove's, or a bracer laced on the inside of the arm.
        private static void Cuff(GearMesh mesh, ArmourKit kit, Genes genes, float side, float reach, float flare)
        {
            GearLook look = kit.Look;
            int steps = 8;
            var rings = new List<Ring>();
            var bands = new List<int>();
            for (int i = 0; i <= steps; i++)
            {
                float s = (float)i / steps;
                float t = GearFit.ArmAt(reach * s);
                float r = MathF.Min(BodyForm.Arm(t) + GearFit.GloveInner + flare * s * s * s, BodyForm.Arm(t) + GearFit.GloveOuter);
                rings.Add(new Ring { Centre = new Vector3(side * (BodyForm.ShoulderX + t * BodyForm.ArmLength), BodyForm.Shoulder, 0f), Side = Vector3.UnitZ, Front = Vector3.UnitY, HalfWidth = r, HalfDepth = r, Hard = i == steps });
                bands.Add(i >= steps - 1 ? 1 : 0);
            }
            mesh.Bone = (int)BodyForm.LowerArm(side);
            VertexMaterial body = kit.Make == GearMake.Silk ? kit.Shell : kit.Shell;
            VertexMaterial rim = kit.Make == GearMake.Plate ? kit.Edge : kit.Woven ? kit.Trim : kit.Edge;
            mesh.Bands(Section.Round(14), rings, (ring, _) => bands[ring] == 1 ? rim : body, capStart: false, capEnd: false);

            if (kit.Make == GearMake.Hardened)
            {
                // Laced on the inside of the arm, which is underneath in the bind pose.
                int crosses = genes.Count("laces", 3, 5);
                for (int i = 0; i < crosses; i++)
                {
                    float s0 = 0.15f + 0.7f * i / crosses, s1 = 0.15f + 0.7f * (i + 1) / crosses;
                    float t0 = GearFit.ArmAt(reach * s0), t1 = GearFit.ArmAt(reach * s1);
                    float r = BodyForm.Arm(t0) + GearFit.GloveInner + 0.012f;
                    foreach (float way in new[] { -1f, 1f })
                    {
                        Vector3 a = new Vector3(side * (BodyForm.ShoulderX + t0 * BodyForm.ArmLength), BodyForm.Shoulder - r, way * 0.04f);
                        Vector3 b = new Vector3(side * (BodyForm.ShoulderX + t1 * BodyForm.ArmLength), BodyForm.Shoulder - r, -way * 0.04f);
                        mesh.Sweep(Section.Round(4), new[] { a, b }, _ => new Vector2(0.007f), kit.Strap, -Vector3.UnitY);
                    }
                }
            }
            else if (!kit.Woven && kit.Likely("strap", 0.25f, 0.6f, 0f))
            {
                float t = GearFit.ArmAt(reach * genes.Range("strap.at", 0.3f, 0.6f));
                float r = BodyForm.Arm(t) + GearFit.GloveInner + 0.01f;
                float x = side * (BodyForm.ShoulderX + t * BodyForm.ArmLength);
                kit.Belt(mesh, genes.Part("strap"), a => new Vector3(x, BodyForm.Shoulder + MathF.Cos(a) * r, MathF.Sin(a) * r), a => new Vector3(0f, MathF.Cos(a), MathF.Sin(a)), 0.022f, 0.007f, 0.3f);
            }
            if (kit.Make == GearMake.Silk && look.Ornament > 0.3f)
            {
                float t = GearFit.ArmAt(reach * 0.9f);
                float r = BodyForm.Arm(t) + GearFit.GloveInner + flare * 0.7f + 0.006f;
                float x = side * (BodyForm.ShoulderX + t * BodyForm.ArmLength);
                mesh.At(Matrix4.CreateRotationZ(MathF.PI / 2f) * Matrix4.CreateTranslation(x, BodyForm.Shoulder, 0f), () => GearKit.Twist(mesh, r, 0.009f, 12, kit.C.Precious));
            }
        }

        // Cloth wound round and round from the wrist up the forearm; the hand itself is the fist, in the same cloth.
        private static void Wound(GearMesh mesh, ArmourKit kit, Genes genes, float side, float reach, float bulk, float length)
        {
            float from = BodyForm.ShoulderX + GearFit.ArmAt(reach) * BodyForm.ArmLength;
            float to = BodyForm.WristX + 0.02f;
            mesh.Bone = (int)BodyForm.LowerArm(side);
            // Built up y and laid out along the arm: +y goes to +x on the left and to -x on the right.
            mesh.At(Matrix4.CreateRotationZ(side * -MathF.PI / 2f) * Matrix4.CreateTranslation(0f, BodyForm.Shoulder, 0f), () =>
            {
                mesh.Wrap(from, to, t => BodyForm.Arm(Math.Clamp((from + (to - from) * t - BodyForm.ShoulderX) / BodyForm.ArmLength, 0f, 1f)) + GearFit.GloveInner, genes.Range("pitch", 0.05f, 0.08f), 0.1f, kit.Shell, sides: 12);
            });
        }
    }
}

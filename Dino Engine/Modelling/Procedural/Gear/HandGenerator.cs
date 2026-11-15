using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Model;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>
    /// What's worn on the hands: round the fist, and a cuff up the forearm over a fitted sleeve (<see cref="GearFit.GloveInner"/>) but never as far as an elbow's plate
    /// (<see cref="GearFit.CuffReach"/>). Plate is an hourglass gauntlet with faceted fingers; leather a glove with a flared cuff or a bracer laced on; cloth is wound round. A hand lies out
    /// along x in the bind pose, closed round whatever it holds, its fingers curled each on its own. Each is built once a side. See GEAR.md.
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

        // The hand, closed round what it holds, out along x from the wrist with its back up +y and its thumb towards +z: the back and the palm out to the knuckles, swelling across them, four
        // fingers each curled down from there and back under the palm, the middle one longest, and the thumb across their front, so that it reads as a hand from any side and in a picture.
        // A gauntlet's fingers are plates, faceted, and a glove's leather.
        private static void Fist(GearMesh mesh, ArmourKit kit, Genes genes, float side, float bulk, float length)
        {
            GearLook look = kit.Look;
            float from = BodyForm.WristX - 0.05f;
            const float Knuckles = 0.58f;
            float wide = bulk, deep = bulk * 0.7f;
            bool plate = kit.Make == GearMake.Plate;
            VertexMaterial skin = kit.Make == GearMake.Hardened ? kit.C.Leather : kit.Shell;
            mesh.Bone = (int)BodyForm.Hand(side);

            const int Steps = 6;
            var rings = new List<Ring>();
            for (int i = 0; i <= Steps; i++)
            {
                float t = (float)i / Steps * Knuckles;
                float swell = MathF.Sin(MathF.Min(t / Knuckles * 1.15f + 0.3f, 1f) * MathF.PI * 0.5f);
                rings.Add(new Ring { Centre = new Vector3(side * (from + t * length), BodyForm.Shoulder, 0f), Side = Vector3.UnitZ, Front = Vector3.UnitY, HalfWidth = wide * swell, HalfDepth = deep * swell, Hard = i == Steps && plate });
            }
            mesh.Bands(Section.Super(12, 2.6f), rings, (ring, _) => ring == Steps && plate ? kit.Edge : skin, capStart: true, capEnd: true);

            // The fingers, from the index at the thumb's side to the little one.
            float[] longer = { 0.94f, 1f, 0.95f, 0.8f };
            Section finger = plate ? Section.Polygon(6) : Section.Round(6);
            for (int k = 0; k < 4; k++)
            {
                float z = wide * (0.72f - 0.48f * k);
                float r = wide * 0.24f * (k == 3 ? 0.88f : 1f);
                float reach = length * 0.36f * longer[k];
                float a = from + length * (Knuckles - 0.05f);
                var keys = new[]
                {
                    new Vector3(side * a, BodyForm.Shoulder + deep * 0.42f, z),
                    new Vector3(side * (a + reach * 0.55f), BodyForm.Shoulder + deep * 0.36f, z),
                    new Vector3(side * (a + reach * 0.85f), BodyForm.Shoulder - deep * 0.12f, z),
                    new Vector3(side * (a + reach * 0.58f), BodyForm.Shoulder - deep * 0.64f, z),
                    new Vector3(side * (a + reach * 0.12f), BodyForm.Shoulder - deep * 0.76f, z),
                };
                mesh.Sweep(finger, Spine.Through(keys, 8), t => new Vector2(r * (1f - 0.16f * t)), skin, Vector3.UnitZ, capStart: false, capEnd: true);
            }

            // The thumb, from low on the palm's side across the front of the curled fingers.
            var thumb = new[]
            {
                new Vector3(side * (from + length * 0.16f), BodyForm.Shoulder - deep * 0.18f, wide * 0.78f),
                new Vector3(side * (from + length * 0.46f), BodyForm.Shoulder - deep * 0.4f, wide * 0.98f),
                new Vector3(side * (from + length * 0.72f), BodyForm.Shoulder - deep * 0.5f, wide * 0.6f),
                new Vector3(side * (from + length * 0.84f), BodyForm.Shoulder - deep * 0.44f, wide * 0.2f),
            };
            mesh.Sweep(finger, Spine.Through(thumb, 8), t => new Vector2(wide * 0.28f * (1f - 0.22f * t)), skin, Vector3.UnitY, capStart: false, capEnd: true);

            // A plate over the back of a gauntlet's hand, or of hardened leather on a glove now and then, on the palm's own shape and a little proud of it.
            bool backed = plate || kit.Hide && genes.Chance("back.panel", 0.4f);
            if (backed)
            {
                mesh.Mark("back");
                mesh.Plate((u, v) =>
                {
                    float t = 0.02f + (Knuckles - 0.03f) * u;
                    float x = side * (from + length * t);
                    float angle = MathF.PI * (0.18f + 0.64f * v);
                    float swell = MathF.Sin(MathF.Min(t / Knuckles * 1.15f + 0.3f, 1f) * MathF.PI * 0.5f);
                    float c = MathF.Cos(angle), s = MathF.Sin(angle);
                    float across = MathF.Sign(c) * MathF.Pow(MathF.Abs(c), 2f / 2.6f), up = MathF.Pow(MathF.Abs(s), 2f / 2.6f);
                    return (new Vector3(x, BodyForm.Shoulder + up * (deep * swell + 0.012f), across * (wide * swell + 0.012f)), new Vector3(0f, s, c));
                }, 5, 5, plate ? 0.016f : 0.009f, (_, _) => plate ? kit.Shell : kit.C.LeatherContrast, PlateEdge.End, plate ? 0.014f : 0.008f, underside: kit.ShellDark, rollMaterial: kit.Gilt);
            }
            if (look.Legendary)
            {
                // A stone set on the back of the hand.
                Vector3 at = new Vector3(side * (from + length * 0.3f), BodyForm.Shoulder + bulk * 0.72f + 0.016f, 0f);
                mesh.At(GearMesh.FacingOut(at, Vector3.UnitY), () => GearKit.Setting(mesh, genes.Part("stone"), bulk * 0.22f, kit.C.Precious, look.Theme != GearTheme.None ? kit.C.Glow : kit.C.Gem));
            }
            if (kit.Make == GearMake.Studded || look.Spikes > 0.6f && kit.Metal && genes.Chance("knuckles", look.Spikes))
            {
                mesh.Mark("knuckles");
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
            // A glove's cuff is often of another hide or cloth from its hand.
            VertexMaterial body = !kit.Metal && genes.Chance("cuff.contrast", 0.45f) ? (kit.Woven ? kit.C.Trim : kit.C.LeatherContrast) : kit.Shell;
            if (!GearMesh.Same(body, kit.Shell)) mesh.Mark("contrast cuff");
            VertexMaterial rim = kit.Make == GearMake.Plate ? kit.Edge : kit.Woven ? kit.Trim : kit.Edge;
            mesh.Bands(Section.Round(14), rings, (ring, _) => bands[ring] == 1 ? rim : body, capStart: false, capEnd: false);

            if (kit.Make == GearMake.Hardened)
            {
                mesh.Mark("bracer laces");
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
            else if (!kit.Woven && kit.Hide && genes.Chance("laced", 0.3f))
            {
                // A soft glove's cuff laced shut up the inside of the wrist.
                mesh.Mark("laced");
                int crosses = genes.Count("laces", 2, 4);
                for (int i = 0; i < crosses; i++)
                {
                    float s0 = 0.2f + 0.6f * i / crosses, s1 = 0.2f + 0.6f * (i + 1) / crosses;
                    float t0 = GearFit.ArmAt(reach * s0), t1 = GearFit.ArmAt(reach * s1);
                    float r = BodyForm.Arm(t0) + GearFit.GloveInner + 0.012f;
                    foreach (float way in new[] { -1f, 1f })
                    {
                        Vector3 a = new Vector3(side * (BodyForm.ShoulderX + t0 * BodyForm.ArmLength), BodyForm.Shoulder - r, way * 0.035f);
                        Vector3 b = new Vector3(side * (BodyForm.ShoulderX + t1 * BodyForm.ArmLength), BodyForm.Shoulder - r, -way * 0.035f);
                        mesh.Sweep(Section.Round(4), new[] { a, b }, _ => new Vector2(0.006f), kit.Strap, -Vector3.UnitY);
                    }
                }
            }
            else if (!kit.Woven && kit.Likely("strap", 0.25f, 0.6f, 0f))
            {
                mesh.Mark("strap");
                float t = GearFit.ArmAt(reach * genes.Range("strap.at", 0.3f, 0.6f));
                float r = BodyForm.Arm(t) + GearFit.GloveInner + 0.01f;
                float x = side * (BodyForm.ShoulderX + t * BodyForm.ArmLength);
                kit.Belt(mesh, genes.Part("strap"), a => new Vector3(x, BodyForm.Shoulder + MathF.Cos(a) * r, MathF.Sin(a) * r), a => new Vector3(0f, MathF.Cos(a), MathF.Sin(a)), 0.022f, 0.007f, 0.3f);
            }
            if (kit.Hide && genes.Chance("cuff.top", 0.3f))
            {
                // Fur round a glove's top, or studs round it on a studded one.
                float t = GearFit.ArmAt(reach * 0.97f);
                float r = MathF.Min(BodyForm.Arm(t) + GearFit.GloveInner + flare * 0.9f + 0.012f, BodyForm.Arm(t) + GearFit.GloveOuter - 0.02f);
                float x = side * (BodyForm.ShoulderX + t * BodyForm.ArmLength);
                if (kit.Make == GearMake.Studded)
                {
                    mesh.Mark("studded cuff");
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i / 8f * MathF.Tau;
                        var n = new Vector3(0f, MathF.Cos(a), MathF.Sin(a));
                        mesh.Rivet(new Vector3(x - side * 0.04f, BodyForm.Shoulder, 0f) + n * (r - 0.01f), n, 0.02f, kit.Fitting);
                    }
                }
                else
                {
                    mesh.Mark("fur cuff");
                    // From the cuff's top down towards the wrist, on either hand, so it stays inside how far up the cuff may come.
                    mesh.At(Matrix4.CreateRotationZ(-side * MathF.PI / 2f) * Matrix4.CreateTranslation(x, BodyForm.Shoulder, 0f), () => mesh.Lathe(Section.Round(14), 0.06f, 2, s => r * (0.96f + 0.08f * MathF.Sin(s * MathF.PI)), kit.C.Fur, capStart: false, capEnd: false));
                }
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

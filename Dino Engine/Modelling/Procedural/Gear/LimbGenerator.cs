using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Model;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>What goes on legs, feet and hands. Each is a pair, built once for each side, around the limb in <see cref="BodyForm"/>. A leg bends at the knee and an arm at the elbow, so what's either side of those goes with its own half.</summary>
    public static class LimbGenerator
    {
        private static (Vector2, Vector2) Leg(Vector3 at, float side)
        {
            float blend = Math.Clamp((BodyForm.Knee + 0.14f - at.Y) / 0.28f, 0f, 1f);
            return (new Vector2((int)BodyForm.UpperLeg(side), (int)BodyForm.LowerLeg(side)), new Vector2(1f - blend, blend));
        }

        public static void Legs(GearMesh mesh, GearStyle style)
        {
            var cut = new ArmourCut(style);
            Genes genes = style.Genes;

            float gap = cut.Blend("gap", new Vector2(0.06f, 0.11f), new Vector2(0.025f, 0.05f), new Vector2(0.04f, 0.1f));
            float loose = cut.Blend("loose", new Vector2(0f, 0.03f), new Vector2(0f, 0.05f), new Vector2(0.02f, 0.11f));
            float ankle = cut.Blend("ankle", new Vector2(0.28f, 0.4f), new Vector2(0.24f, 0.46f), new Vector2(0.18f, 0.38f));
            float seat = genes.Range("seat", 0.02f, 0.08f);
            int lames = cut.Likely("lames", 0.75f, 0.08f, 0f) ? genes.Count("lames", 2, 5) : 0;

            // How it fits on the way down, drawn: gathered at the knee and full below it, tight to the shin, wide at the thigh and anything between.
            float fit = cut.Blend("fit", new Vector2(0.05f, 0.22f), new Vector2(0.04f, 0.2f), new Vector2(0.06f, 0.17f));
            Profile outline = new Profile((0f, genes.Unit("fit.hip")), (0.3f, genes.Unit("fit.thigh")), (0.55f, genes.Range("fit.knee", 0f, 0.7f)), (0.8f, genes.Unit("fit.shin")), (1f, genes.Range("fit.ankle", 0f, 1.3f)));
            VertexMaterial front = cut.Main;
            VertexMaterial back = cut.Heavy > 0.5f ? cut.Under : cut.Main;
            VertexMaterial hemCloth = cut.Second;
            float seam = cut.Likely("seam", 0.1f, 0.7f, 0.5f) ? genes.Range("seam.width", 0.1f, 0.3f) : 0f;
            Func<float, float, VertexMaterial> pattern = cut.Pattern(genes.Part("pattern"));

            foreach (float side in new[] { 1f, -1f })
            {
                int steps = 14;
                var rings = new Ring[steps + 1];
                float top = BodyForm.Hip + 0.12f;
                for (int i = 0; i <= steps; i++)
                {
                    float t = (float)i / steps;
                    float y = top + (ankle - top) * t;
                    float drape = loose * MathF.Sin(MathF.Min(t * 1.25f, 1f) * MathF.PI * 0.5f) * (1f - MathF.Pow(t, 6f) * 0.7f);
                    float step = lames > 0 && y > BodyForm.Knee + 0.1f && y < BodyForm.Hip - 0.1f && (int)(t * lames * 3f) % 2 == 0 ? 0.02f : 0f;
                    float r = BodyForm.Leg(MathF.Min(y, BodyForm.Hip)) + gap + drape + step + fit * outline.At(t);
                    rings[i] = new Ring { Centre = new Vector3(side * BodyForm.HipX * (1f + 0.08f * t), y, 0f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = r, HalfDepth = r * (y > BodyForm.Knee ? 1.05f : 1f) };
                }

                int first = mesh.VertexCount;
                mesh.Loft(Section.Round(14), rings, (ring, point) =>
                {
                    if (ring == steps) return hemCloth;
                    bool outer = MathF.Abs(MathF.Cos(point * (MathF.Tau / 14f)) - side) < seam;
                    if (outer) return cut.Second;
                    float z = MathF.Sin(point * (MathF.Tau / 14f));
                    return z > -0.1f ? pattern((float)ring / steps, point / 14f) : back;
                }, capStart: false, capEnd: false);
                float s = side;
                mesh.SkinBy(at => Leg(at, s), first);

                Knee(mesh, cut, genes.Part("knee"), side, gap);
                if (cut.Likely("strap", 0.45f, 0.65f, 0.15f))
                {
                    float y = genes.Range("strap.at", 1.2f, 1.7f);
                    float r = BodyForm.Leg(y) + gap + 0.012f;
                    first = mesh.VertexCount;
                    ArmourCut.Band(mesh, new Vector3(side * BodyForm.HipX, y, 0f), r, r * 1.05f, genes.Range("strap.height", 0.03f, 0.06f), 0.012f, cut.Strap);
                    mesh.SkinBy(at => Leg(at, s), first);
                }
            }

            // The seat, between the two.
            float hipRadius = BodyForm.Torso(BodyForm.Hip) + gap * 0.7f + seat;
            Ring[] seatRings =
            {
                new Ring { Centre = new Vector3(0f, BodyForm.Hip - 0.3f, 0f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = hipRadius * 0.82f, HalfDepth = hipRadius * BodyForm.Depth * 0.92f },
                new Ring { Centre = new Vector3(0f, BodyForm.Hip, 0f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = hipRadius, HalfDepth = hipRadius * BodyForm.Depth },
                new Ring { Centre = new Vector3(0f, BodyForm.Hip + 0.3f, 0f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = hipRadius * 0.95f, HalfDepth = hipRadius * BodyForm.Depth * 0.95f },
            };
            mesh.Bone = (int)BoneType.Chest;
            mesh.Loft(Section.Round(16), seatRings, cut.Heavy > 0.5f ? cut.Under : cut.Main, capStart: true, capEnd: false);
            Panels(mesh, cut, genes.Part("panels"), hipRadius);
            mesh.Covers |= BodyRegions.UpperLegs | BodyRegions.LowerLegs;
        }

        // What hangs from the hips over the top of them: a loincloth, a pair of flaps, plates on the thighs. A leg has little of its own to set it apart, so this is most of what does.
        private static void Panels(GearMesh mesh, ArmourCut cut, Genes genes, float hipRadius)
        {
            int panels = cut.Likely("any", 0.8f, 0.8f, 0.6f) ? genes.Count("count", 1, 5) : 0;
            float length = cut.Blend("length", new Vector2(0.35f, 0.8f), new Vector2(0.3f, 0.95f), new Vector2(0.5f, 1.35f));
            float wide = genes.Range("wide", 0.12f, 0.3f);
            float spread = genes.Range("spread", 0.5f, 1.2f);
            float flare = genes.Range("flare", 0.8f, 1.5f);
            float tip = genes.Range("tip", 0.1f, 1.1f);
            float top = BodyForm.Hip + 0.14f;
            VertexMaterial material = cut.Heavy > 0.5f ? cut.Plate : genes.Chance("second", 0.5f) ? cut.Second : cut.Main;
            VertexMaterial edge = cut.Heavy > 0.5f ? cut.Fitting : cut.Second;
            for (int i = 0; i < panels; i++)
            {
                float angle = MathF.PI / 2f + (panels == 1 ? 0f : ((float)i / (panels - 1) - 0.5f) * spread * 2f);
                Vector3 outwards = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle) * BodyForm.Depth);
                Vector3 facing = outwards.Normalized();
                Vector3 side = Vector3.Cross(Vector3.UnitY, facing);
                var rings = new Ring[5];
                for (int r = 0; r <= 4; r++)
                {
                    float t = r / 4f;
                    float width = wide * (1f + (flare - 1f) * t) * (t > 0.75f ? 1f + (tip - 1f) * (t - 0.75f) * 4f : 1f);
                    rings[r] = new Ring { Centre = outwards * (hipRadius + 0.02f + 0.1f * t) + Vector3.UnitY * (top - length * t), Side = side, Front = facing, HalfWidth = MathF.Max(width, 0.02f), HalfDepth = 0.016f, Hard = r == 0 || r == 4 };
                }
                int first = mesh.VertexCount;
                mesh.Loft(Section.Slab(0.4f), rings, (ring, _) => ring >= 3 ? edge : material);
                mesh.SkinBy(BodyForm.Skirt, first);
            }
        }

        private static void Knee(GearMesh mesh, ArmourCut cut, Genes genes, float side, float gap)
        {
            if (!cut.Likely("any", 0.92f, 0.5f, 0.05f)) return;

            float size = cut.Blend("size", new Vector2(0.22f, 0.5f), new Vector2(0.18f, 0.32f), new Vector2(0.2f, 0.24f));
            float height = size * genes.Range("height", 0.6f, 1.5f);
            VertexMaterial material = cut.Heavy > 0.4f ? cut.Plate : cut.Second;
            float z = BodyForm.Leg(BodyForm.Knee) + gap;
            mesh.Bone = (int)BodyForm.LowerLeg(side);
            mesh.At(Matrix4.CreateRotationX(MathF.PI / 2f) * Matrix4.CreateTranslation(side * BodyForm.HipX, BodyForm.Knee, z * 0.8f), () =>
            {
                mesh.Lathe(cut.Style.Angular > 0.5f ? Section.Polygon(genes.Count("sides", 5, 9)) : Section.Round(10), height * 0.6f, 4, t => size * MathF.Sqrt(MathF.Max(1f - t * t, 0.03f)), material, capStart: false, squash: 1.2f);
            });

            if (!genes.Chance("spike", cut.Style.Spikes * cut.Heavy)) return;
            mesh.At(Matrix4.CreateRotationX(MathF.PI / 2f) * Matrix4.CreateTranslation(side * BodyForm.HipX, BodyForm.Knee, z + height * 0.45f), () => mesh.Lathe(Section.Polygon(4), size * 0.8f, 2, t => size * 0.26f * (1f - t), cut.Fitting, capEnd: false));
        }

        public static void Feet(GearMesh mesh, GearStyle style)
        {
            var cut = new ArmourCut(style);
            Genes genes = style.Genes;
            GearForm form = style.Spec.Form;

            float reach = cut.Blend("reach", new Vector2(0.5f, 1.05f), new Vector2(0.34f, 0.9f), new Vector2(0.16f, 0.5f)) * (0.55f + form.Cover * 0.75f);
            float gap = cut.Blend("gap", new Vector2(0.07f, 0.12f), new Vector2(0.04f, 0.075f), new Vector2(0.02f, 0.05f));
            float toe = cut.Blend("toe", new Vector2(0.5f, 0.85f), new Vector2(0.44f, 0.64f), new Vector2(0.44f, 0.72f));
            float point = cut.Blend("point", new Vector2(0.15f, 0.7f), new Vector2(0.1f, 0.5f), new Vector2(0.2f, 0.9f));
            float curl = cut.Cloth * form.Curve * genes.Range("curl", 0.1f, 0.42f);
            float height = cut.Blend("height", new Vector2(0.26f, 0.34f), new Vector2(0.22f, 0.3f), new Vector2(0.14f, 0.22f));
            float wide = cut.Blend("wide", new Vector2(0.21f, 0.27f), new Vector2(0.17f, 0.22f), new Vector2(0.15f, 0.2f));
            int lames = cut.Likely("lames", 0.85f, 0.04f, 0f) ? genes.Count("lames", 3, 7) : 0;
            bool heel = cut.Likely("heel", 0.2f, 0.5f, 0.1f);
            float cuffFlare = cut.Blend("cuff", new Vector2(0.02f, 0.2f), new Vector2(0.03f, 0.3f), new Vector2(0f, 0.1f));
            Profile shaft = new Profile((0f, genes.Range("shaft.ankle", 0f, 0.6f)), (0.45f, genes.Unit("shaft.calf")), (0.8f, genes.Unit("shaft.top")), (1f, genes.Unit("shaft.lip")));
            float shaftFit = cut.Blend("shaft.fit", new Vector2(0.03f, 0.12f), new Vector2(0.02f, 0.12f), new Vector2(0.01f, 0.08f));
            float toeUp = genes.Range("toe.up", 0f, 0.14f) * (1f - cut.Heavy * 0.6f);
            Func<float, float, VertexMaterial> pattern = cut.Pattern(genes.Part("pattern"));

            foreach (float side in new[] { 1f, -1f })
            {
                // From the heel forwards, sitting on the ground.
                float x = side * BodyForm.HipX;
                int steps = 11 + lames;
                var rings = new Ring[steps + 1];
                for (int i = 0; i <= steps; i++)
                {
                    float t = (float)i / steps;
                    float z = -0.24f + t * (0.24f + toe);
                    float tip = MathF.Pow(t, 2.4f);
                    float hw = wide * (1f - point * tip) * (0.84f + 0.32f * MathF.Sin(MathF.Min(t * 1.5f, 1f) * MathF.PI)) * (1f - 0.7f * MathF.Pow(t, 10f));
                    float hh = height * (1f - 0.62f * MathF.Pow(t, 1.3f)) * 0.5f + 0.012f;
                    float lift = (curl + toeUp) * MathF.Pow(t, 4f);
                    float step = lames > 0 && t > 0.3f && i % 2 == 0 ? 0.012f : 0f;
                    rings[i] = new Ring { Centre = new Vector3(x, hh + lift, z), Side = Vector3.UnitX, Front = Vector3.UnitY, HalfWidth = hw + step, HalfDepth = hh + step, Hard = lames > 0 && t > 0.3f };
                }

                mesh.Bone = (int)BodyForm.Foot(side);
                mesh.Loft(Section.Super(12, 2.6f), rings, (ring, _) => ring % 2 == 0 && lames > 0 ? cut.Second : cut.Main);

                // The sole, which is what's seen of a foot from behind in a run.
                VertexMaterial sole = cut.Strap;
                float soleTop = 0.05f;
                mesh.At(Matrix4.CreateTranslation(x, 0f, -0.02f), () => mesh.Lathe(Section.Super(10, 2.4f), soleTop, 1, _ => wide * 1.02f, sole, squash: (toe + 0.24f) / (2f * wide) * 0.92f));

                // Up the shin.
                if (reach > 0.26f)
                {
                    int rows = 8;
                    var shin = new Ring[rows + 1];
                    for (int i = 0; i <= rows; i++)
                    {
                        float t = (float)i / rows;
                        float y = 0.16f + (reach - 0.16f) * t;
                        float r = BodyForm.Leg(y) + gap + cuffFlare * t * t * t + shaftFit * shaft.At(t);
                        shin[i] = new Ring { Centre = new Vector3(x * (1f + 0.03f * (1f - t)), y, 0f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = r, HalfDepth = r * 1.04f };
                    }
                    mesh.Bone = (int)BodyForm.LowerLeg(side);
                    mesh.Loft(Section.Round(12), shin, (ring, point) => ring >= rows - 1 ? cut.Second : pattern((float)ring / rows, point / 12f), capStart: false, capEnd: false);

                    if (cut.Likely("straps", 0.25f, 0.8f, 0.15f))
                    {
                        int straps = genes.Count("strap.count", 1, 3);
                        for (int i = 0; i < straps; i++)
                        {
                            float y = 0.26f + (reach - 0.3f) * (i + 0.5f) / straps;
                            float r = BodyForm.Leg(y) + gap + 0.012f;
                            ArmourCut.Band(mesh, new Vector3(x, y, 0f), r, r * 1.04f, 0.028f, 0.008f, cut.Strap);
                            cut.Buckle(mesh, genes, new Vector3(x + side * r * 0.6f, y, r * 0.84f), 0.04f);
                        }
                    }
                }

                if (heel)
                {
                    mesh.Bone = (int)BodyForm.Foot(side);
                    mesh.At(Matrix4.CreateTranslation(x, -0.0f, -0.16f), () => mesh.Lathe(Section.Slab(0.3f), 0.05f, 1, _ => wide * 0.68f, cut.Fitting, squash: 0.65f));
                }
            }

            mesh.Covers |= BodyRegions.Feet;
            if (reach > 0.8f) mesh.Covers |= BodyRegions.LowerLegs;
        }

        public static void Hands(GearMesh mesh, GearStyle style)
        {
            var cut = new ArmourCut(style);
            Genes genes = style.Genes;
            GearForm form = style.Spec.Form;

            float reach = cut.Blend("reach", new Vector2(0.28f, 0.48f), new Vector2(0.2f, 0.42f), new Vector2(0.14f, 0.42f)) * (0.55f + form.Cover * 0.75f);
            float gap = cut.Blend("gap", new Vector2(0.07f, 0.13f), new Vector2(0.03f, 0.06f), new Vector2(0.02f, 0.05f));
            float flare = cut.Blend("flare", new Vector2(0.04f, 0.22f), new Vector2(0.02f, 0.1f), new Vector2(0f, 0.08f));
            float bulk = cut.Blend("bulk", new Vector2(0.17f, 0.3f), new Vector2(0.13f, 0.21f), new Vector2(0.12f, 0.19f));
            float long_ = genes.Range("long", 0.3f, 0.46f);
            Profile forearm = new Profile((0f, genes.Unit("arm.wrist")), (0.4f, genes.Unit("arm.mid")), (0.8f, genes.Unit("arm.top")), (1f, genes.Unit("arm.lip")));
            float armFit = cut.Blend("arm.fit", new Vector2(0.03f, 0.13f), new Vector2(0.02f, 0.11f), new Vector2(0.02f, 0.13f));
            Func<float, float, VertexMaterial> pattern = cut.Pattern(genes.Part("pattern"));
            int fingers = cut.Likely("segments", 0.8f, 0.3f, 0f) ? genes.Count("segments", 2, 5) : 0;
            bool spikes = genes.Chance("spikes", style.Spikes * cut.Heavy);
            int wraps = cut.Likely("wraps", 0f, 0.25f, 0.75f) ? genes.Count("wraps", 3, 8) : 0;

            foreach (float side in new[] { 1f, -1f })
            {
                // The hand, out along x.
                float from = BodyForm.WristX - 0.04f;
                int steps = 7 + fingers;
                var rings = new Ring[steps + 1];
                for (int i = 0; i <= steps; i++)
                {
                    float t = (float)i / steps;
                    float swell = MathF.Sin(MathF.Min(t * 1.4f + 0.2f, 1f) * MathF.PI * 0.5f);
                    float tip = 1f - MathF.Pow(t, 5f) * 0.72f;
                    float step = fingers > 0 && i % 2 == 1 ? 0.012f : 0f;
                    rings[i] = new Ring { Centre = new Vector3(side * (from + t * long_), BodyForm.Shoulder, 0f), Side = Vector3.UnitZ, Front = Vector3.UnitY, HalfWidth = bulk * swell * tip + step, HalfDepth = bulk * 0.68f * swell * tip + step, Hard = fingers > 0 };
                }
                mesh.Bone = (int)BodyForm.Hand(side);
                mesh.Loft(Section.Super(10, 2.6f), rings, (ring, _) => fingers > 0 && ring % 2 == 1 ? cut.Second : cut.Main);

                if (spikes)
                {
                    int count = genes.Count("spike.count", 2, 4);
                    for (int i = 0; i < count; i++)
                    {
                        float z = bulk * ((i + 0.5f) / count * 1.5f - 0.75f);
                        mesh.At(ArmourCut.FacingOut(new Vector3(side * (from + 0.24f), BodyForm.Shoulder + bulk * 0.66f, z), Vector3.UnitY), () => mesh.Lathe(Section.Polygon(4), 0.1f, 2, t => 0.036f * (1f - t), cut.Fitting, capEnd: false));
                    }
                }

                // Back up the forearm.
                int rows = 8;
                var arm = new Ring[rows + 1];
                for (int i = 0; i <= rows; i++)
                {
                    float t = (float)i / rows;
                    float along = 1f - reach * t;
                    float r = BodyForm.Arm(along) + gap + flare * t * t * t + armFit * forearm.At(t);
                    arm[i] = new Ring { Centre = new Vector3(side * (BodyForm.ShoulderX + along * BodyForm.ArmLength), BodyForm.Shoulder, 0f), Side = Vector3.UnitZ, Front = Vector3.UnitY, HalfWidth = r, HalfDepth = r };
                }
                mesh.Bone = (int)BodyForm.LowerArm(side);
                mesh.Loft(Section.Round(10), arm, (ring, point) => ring >= rows - 1 ? cut.Second : wraps > 0 && ring % 2 == 0 ? cut.Under : pattern((float)ring / rows, point / 10f), capStart: false, capEnd: false);

                if (cut.Likely("strap", 0.3f, 0.75f, 0.3f))
                {
                    int straps = genes.Count("strap.count", 1, 2);
                    for (int i = 0; i < straps; i++)
                    {
                        float along = 1f - reach * (i + 0.6f) / (straps + 0.4f);
                        float r = BodyForm.Arm(along) + gap + 0.01f;
                        Vector3 at = new Vector3(side * (BodyForm.ShoulderX + along * BodyForm.ArmLength), BodyForm.Shoulder, 0f);
                        var band = new[]
                        {
                            new Ring { Centre = at - Vector3.UnitX * 0.03f, Side = Vector3.UnitZ, Front = Vector3.UnitY, HalfWidth = r, HalfDepth = r },
                            new Ring { Centre = at + Vector3.UnitX * 0.03f, Side = Vector3.UnitZ, Front = Vector3.UnitY, HalfWidth = r, HalfDepth = r },
                        };
                        mesh.Loft(Section.Round(10), band, cut.Strap, capStart: false, capEnd: false);
                    }
                }
            }

            mesh.Covers |= BodyRegions.Hands;
            if (reach > 0.4f) mesh.Covers |= BodyRegions.LowerArms;
        }
    }
}

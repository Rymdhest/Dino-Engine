using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Procedural.Gear;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Saurian
{
    /// <summary>
    /// A body's bones, dug out of the ground and raised as they lay: the same animal its flesh would be, measured from the same body, so a raptor's skeleton is that raptor's. The skull is its field with
    /// the openings a theropod skull has cut through it, the backbone is counted as Sue's is, ten in the neck, thirteen in the back, five over the hips and forty down the tail, and the ribs follow the
    /// shape the flesh had. A cold light burns in the eyes and where the heart was, as that's what the Ossuary put there.
    /// </summary>
    public static class SaurianBones
    {
        private const int Neck = 10, Back = 13, Hips = 5, Tail = 40;
        private const float Smallest = 0.045f;     // Metres. About a pixel and a half from where the camera is.
        private static readonly Vector3 Light = new Vector3(0.3f, 0.95f, 0.85f);

        public static void Build(SaurianMesh mesh, SaurianBody body)
        {
            Genes genes = body.Genes.Part("bones");

            // Some come out of the ground bleached, and some as dark as the rock they lay in.
            Vector3 bone = Vector3.Lerp(new Vector3(0.42f, 0.37f, 0.28f), new Vector3(0.2f, 0.15f, 0.1f), MathF.Pow(genes.Unit("fossil"), 1.6f));
            Vector3 dark = bone * 0.55f;
            var dress = new SaurianDress(body, new SaurianPaint(body));
            Bare(body, bone);

            Skull(mesh, body, bone);
            Spine(mesh, body, dress, bone, dark);
            Ribs(mesh, body, dress, bone);
            Girdles(mesh, body, bone);
            Limbs(mesh, body, bone, dark);
            SaurianParts.Teeth(mesh, ExposedTeeth(body), 1f);
            SaurianParts.Horns(mesh, body);
            SaurianParts.Frill(mesh, body);

            // The light that keeps it going, in the eyes and in the chest, which shows through the ribs from above.
            SaurianParts.Eyes(mesh, body, Light, 3.2f, GearMaterials.Glow);
            float heart = body.Axis.DeepAt(body.SpineAt + (body.ChestAt - body.SpineAt) * 0.5f) * 0.28f;
            Vector3 chest = body.Axis.At(body.SpineAt + (body.ChestAt - body.SpineAt) * 0.5f);
            mesh.Ball(chest, new Vector3(heart), 2, Light, GearMaterials.Glow, dress.Along(body.SpineAt + (body.ChestAt - body.SpineAt) * 0.5f), 2.4f);
        }

        // A skull shows every tooth: there's nothing to hide them behind.
        private static SaurianBody ExposedTeeth(SaurianBody body)
        {
            body.Head.Lips = 0f;
            return body;
        }

        // ---- The skull and the jaw.

        private static void Skull(SaurianMesh mesh, SaurianBody body, Vector3 colour)
        {
            SaurianHead head = body.Head;
            float cell = head.Length / 70f;
            var plain = new BoneDress(body, colour, BoneType.Head);

            var skull = new SaurianField { Head = head };
            SaurianSculpt.HeadVolume(skull, head);
            Openings(skull, head, jaw: false);
            SaurianSkin.Grow(mesh, skull, cell, 1100, head.Length / 8f, plain);

            var jaw = new SaurianField { Head = head, Jaw = true, Gap = head.Length * 0.004f };
            SaurianSculpt.HeadVolume(jaw, head);
            Openings(jaw, head, jaw: true);
            SaurianSkin.Grow(mesh, jaw, cell * 0.8f, 420, head.Length / 8f, new BoneDress(body, colour, BoneType.Jaw));
        }

        // What a theropod skull has instead of a face: the eye's orbit, the great opening in front of it, the window behind it where the jaw muscles ran, and the nostril. They go right through,
        // so a skull can be seen through. The jaw is two thin walls with a gap between, as it was in life.
        private static void Openings(SaurianField field, SaurianHead head, bool jaw)
        {
            float length = head.Length;
            if (jaw)
            {
                float z = length * 0.55f;
                field.Ellipsoid(SaurianPart.Head, new Vector3(0f, head.Lip(z) - head.DepthAt(z) * 0.35f, z), new Vector3(head.WidthAt(z) * 0.72f, head.DepthAt(z) * 0.55f, length * 0.42f), length * 0.02f, inHead: true, carve: true);
                float back = length * 0.22f;
                field.Ellipsoid(SaurianPart.Head, new Vector3(0f, head.Lip(back) - head.DepthAt(back) * 0.35f, back), new Vector3(length, head.DepthAt(back) * 0.22f, length * 0.08f), length * 0.015f, inHead: true, carve: true);
                return;
            }

            float eyeZ = head.EyeAt * length;
            float eyeY = SaurianSculpt.EyeHeight(head);
            field.Ellipsoid(SaurianPart.Head, new Vector3(0f, eyeY, eyeZ), new Vector3(length, head.EyeSize * 1.25f, head.EyeSize * 1.1f), length * 0.015f, inHead: true, carve: true);

            float front = length * 0.55f;
            float middle = head.Lip(front) + head.DepthAt(front) * 0.5f;
            field.Ellipsoid(SaurianPart.Head, new Vector3(0f, middle, front), new Vector3(length, head.DepthAt(front) * 0.38f, length * 0.13f), length * 0.015f, inHead: true, carve: true);

            float temple = length * 0.1f;
            field.Ellipsoid(SaurianPart.Head, new Vector3(0f, head.Lip(temple) + head.DepthAt(temple) * 0.95f, temple), new Vector3(length, head.DepthAt(temple) * 0.3f, length * 0.065f), length * 0.012f, inHead: true, carve: true);

            float nose = length * 0.86f;
            field.Ellipsoid(SaurianPart.Head, new Vector3(0f, head.Lip(nose) + head.DepthAt(nose) * 0.85f, nose), new Vector3(length, head.DepthAt(nose) * 0.25f, length * 0.06f), length * 0.01f, inHead: true, carve: true);

            // It's bone all through, so the skull's inside is hollowed out behind the openings.
            float hollow = length * 0.4f;
            field.Ellipsoid(SaurianPart.Head, new Vector3(0f, head.Lip(hollow) + head.DepthAt(hollow) * 0.45f, hollow), new Vector3(head.WidthAt(hollow) * 0.72f, head.DepthAt(hollow) * 0.62f, length * 0.32f), length * 0.03f, inHead: true, carve: true);
        }

        /// <summary>A skull or jaw is one bone, all one colour, a little darker in the hollows where earth has got in.</summary>
        private sealed class BoneDress : SaurianDress
        {
            private readonly Vector3 _colour;
            private readonly BoneType _bone;

            public BoneDress(SaurianBody body, Vector3 colour, BoneType bone) : base(body, new SaurianPaint(body))
            {
                _colour = colour;
                _bone = bone;
            }

            public override int Mapping(Vector3 p, SaurianPart part) => Side;

            public override Hang Hang(Vector3 p, SaurianPart part, ReadOnlySpan<float> parts) => _bone;

            public override Vector3 Colour(Vector3 p, Vector3 normal, SaurianPart part, out int material, out float glow)
            {
                material = GearMaterials.Bone;
                glow = 1f;
                return _colour * (0.75f + 0.25f * MathF.Max(normal.Y, 0f));
            }
        }

        // ---- The backbone.

        private static void Spine(SaurianMesh mesh, SaurianBody body, SaurianDress dress, Vector3 bone, Vector3 dark)
        {
            SaurianAxis axis = body.Axis;
            float neckEnd = body.HeadAt - (body.HeadAt - body.NeckAt) * 0.02f;
            float neckStart = body.NeckAt - (body.NeckAt - body.ChestAt) * 0.4f;
            float hipFront = body.HipAt + body.ChestDepth * 0.6f;
            float hipBack = body.HipAt - body.ChestDepth * 0.5f;

            // Tail tip to skull, each run of vertebrae evenly spaced over its own stretch.
            // Down the tail, as many as can be told apart from up where the camera is: forty little bones a pixel each are only noise.
            int tail = Math.Clamp((int)((hipBack - axis.Length * 0.004f) / (Smallest * 3f)), 12, Tail);
            Run(mesh, axis, dress, axis.Length * 0.004f, hipBack, tail, bone, dark, body, Region.Tail);
            Run(mesh, axis, dress, hipBack, hipFront, Hips, bone, dark, body, Region.Hips);
            Run(mesh, axis, dress, hipFront, neckStart, Back, bone, dark, body, Region.Back);
            Run(mesh, axis, dress, neckStart, neckEnd, Neck, bone, dark, body, Region.Neck);
        }

        private enum Region { Tail, Hips, Back, Neck }

        private static void Run(SaurianMesh mesh, SaurianAxis axis, SaurianDress dress, float from, float to, int count, Vector3 bone, Vector3 dark, SaurianBody body, Region region)
        {
            float spacing = (to - from) / count;
            for (int i = 0; i < count; i++)
            {
                float at = from + spacing * (i + 0.5f);
                float t = count > 1 ? i / (count - 1f) : 0f;
                Vector3 way = axis.Way(at);
                Vector3 up = (Vector3.UnitY - way * Vector3.Dot(Vector3.UnitY, way)).Normalized();
                Vector3 side = Vector3.Cross(up, way);
                float deep = axis.DeepAt(at);

                // The backbone runs along the top of the body, not its middle.
                float high = region switch { Region.Back => 0.42f, Region.Hips => 0.4f, _ => 0.22f };
                Vector3 centre = axis.At(at) + up * (deep * high);
                Hang hang = dress.Along(at);

                // A spool for the body of each vertebra, wider at its ends.
                float radius = MathF.Max(deep * (region == Region.Neck ? 0.2f : 0.17f), body.Length * 0.0016f);
                float half = spacing * 0.42f;
                mesh.Tube(new[] { centre - way * half, centre - way * half * 0.6f, centre + way * half * 0.6f, centre + way * half }, new[] { radius * 1.05f, radius * 0.82f, radius * 0.82f, radius * 1.05f }, 5, bone, GearMaterials.Bone, hang);

                // A spine up from it, tallest over the back and the hips, shrinking down the tail and gone at the end of it.
                float spine = region switch
                {
                    Region.Back => deep * (0.45f + 0.15f * t),
                    Region.Hips => deep * 0.55f,
                    Region.Neck => deep * (0.28f - 0.12f * t),
                    _ => deep * 0.5f * MathF.Max(0f, 1f - t * 1.8f),
                };
                if (region == Region.Tail) spine = deep * 0.5f * MathF.Max(0f, (t - 0.45f) / 0.55f);
                if (spine > radius * 0.6f)
                {
                    Vector3 rise = (up - way * 0.25f).Normalized();
                    mesh.Tube(new[] { centre, centre + rise * spine * 0.6f, centre + rise * spine }, new[] { spacing * 0.32f, spacing * 0.3f, spacing * 0.2f }, 4, bone, GearMaterials.Bone, hang, side, 0.28f);
                }

                // Out to the sides from the back, and under the tail.
                if (region is Region.Back or Region.Hips || region == Region.Tail && t > 0.3f)
                {
                    float reach = deep * (region == Region.Tail ? 0.4f * (t - 0.3f) / 0.7f : 0.38f);
                    if (reach > radius * 0.8f)
                    {
                        for (int s = -1; s <= 1; s += 2) mesh.Tube(new[] { centre, centre + side * (s * reach) + up * (deep * 0.04f) }, new[] { radius * 0.4f, radius * 0.22f }, 5, dark, GearMaterials.Bone, hang);
                    }
                }
                if (region == Region.Tail && t > 0.35f)
                {
                    float chevron = deep * 0.7f * (t - 0.35f) / 0.65f;
                    if (chevron > radius * 0.8f) mesh.Tube(new[] { centre - up * radius, centre - up * (radius + chevron * 0.6f) - way * chevron * 0.25f, centre - up * (radius + chevron) - way * chevron * 0.45f }, new[] { spacing * 0.22f, spacing * 0.2f, spacing * 0.08f }, 5, bone, GearMaterials.Bone, hang, side, 0.35f);
                }

                // The neck's own little ribs, laid back along it.
                if (region == Region.Neck)
                {
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Vector3 start = centre + side * (s * radius * 1.3f) - up * radius * 0.6f;
                        mesh.Tube(new[] { start, start - way * spacing * 1.6f - up * radius * 0.4f }, new[] { radius * 0.28f, radius * 0.08f }, 4, dark, GearMaterials.Bone, hang);
                    }
                }
            }
        }

        // ---- Ribs, following the shape of the body they held.

        private static void Ribs(SaurianMesh mesh, SaurianBody body, SaurianDress dress, Vector3 bone)
        {
            SaurianAxis axis = body.Axis;
            float from = body.HipAt + body.ChestDepth * 0.7f;
            float to = body.NeckAt - (body.NeckAt - body.ChestAt) * 0.4f;
            for (int i = 0; i < Back; i++)
            {
                float t = i / (Back - 1f);
                float at = from + (to - from) * t;
                Vector3 way = axis.Way(at);
                Vector3 up = (Vector3.UnitY - way * Vector3.Dot(Vector3.UnitY, way)).Normalized();
                Vector3 side = Vector3.Cross(up, way);
                Vector3 centre = axis.At(at);

                // Longest in the middle of the chest, as they were.
                float deep = axis.DeepAt(at) * 0.9f + body.BellyHang * MathF.Sin(t * MathF.PI) * 0.5f;
                float wide = axis.WideAt(at) * 0.88f;
                float reach = 0.5f + 0.5f * MathF.Sin(MathF.Min(t * 1.4f + 0.15f, 1f) * MathF.PI);
                float radius = MathF.Max(axis.DeepAt(at) * 0.035f, body.Length * 0.0012f);
                Hang hang = dress.Along(at);
                for (int s = -1; s <= 1; s += 2)
                {
                    var path = new List<Vector3>();
                    var radii = new List<float>();
                    const int Steps = 6;
                    // From the side of the backbone, out to the widest of the chest and down towards the belly, slanting back as ribs do.
                    float top = axis.DeepAt(at) * 0.45f, bottom = -deep;
                    float middle = (top + bottom) * 0.5f, half = (top - bottom) * 0.5f;
                    for (int k = 0; k <= Steps; k++)
                    {
                        float a = (0.06f + 0.9f * reach * k / Steps) * MathF.PI;
                        Vector3 p = centre + up * (middle + MathF.Cos(a) * half) + side * (s * MathF.Sin(a) * wide) - way * (deep * 0.28f * k / Steps);
                        path.Add(p);
                        radii.Add(radius * (1f - 0.45f * k / Steps));
                    }
                    mesh.Tube(path, radii, 4, bone, GearMaterials.Bone, hang);
                }
            }

            // Belly ribs, crossing under it in a V.
            float bellyFrom = body.HipAt + body.ChestDepth * 0.4f;
            float bellyTo = body.ChestAt - body.ChestDepth * 0.2f;
            const int Belly = 14;
            for (int i = 0; i < Belly; i++)
            {
                float at = bellyFrom + (bellyTo - bellyFrom) * i / (Belly - 1f);
                Vector3 centre = body.Axis.At(at);
                float deep = body.Axis.DeepAt(at) + body.BellyHang * 0.6f;
                float wide = body.Axis.WideAt(at) * 0.55f;
                Vector3 low = centre - Vector3.UnitY * (deep * 0.92f);
                float radius = MathF.Max(body.Axis.DeepAt(at) * 0.02f, body.Length * 0.001f);
                Hang hang = dress.Along(at);
                for (int s = -1; s <= 1; s += 2) mesh.Tube(new[] { low + new Vector3(s * wide, deep * 0.25f, -deep * 0.05f), low + new Vector3(s * wide * 0.5f, deep * 0.05f, 0f), low + new Vector3(0f, 0f, deep * 0.06f) }, new[] { radius, radius, radius * 0.8f }, 4, bone, GearMaterials.Bone, hang);
            }
        }

        // ---- Hips and shoulders.

        private static void Girdles(SaurianMesh mesh, SaurianBody body, Vector3 bone)
        {
            SaurianAxis axis = body.Axis;
            Vector3 sacrum = axis.At(body.HipAt) + Vector3.UnitY * (axis.DeepAt(body.HipAt) * 0.4f);
            float deep = axis.DeepAt(body.HipAt);
            float femur = (body.Legs[0].Knee - body.Legs[0].Hip).Length;

            foreach (SaurianLeg leg in body.Legs)
            {
                if (leg.Front) continue;
                float x = leg.Hip.X * 0.65f;
                Vector3 joint = new Vector3(x, leg.Hip.Y, leg.Hip.Z);

                // The ilium, a long blade over the hip joint holding the backbone. The pubis, a long rod down and forward to a boot, and the ischium, a shorter one down and back.
                var blade = new List<Vector3>();
                for (int k = 0; k <= 12; k++)
                {
                    float a = k / 12f * MathF.Tau;
                    blade.Add(new Vector3(x, (joint.Y + sacrum.Y) * 0.5f + MathF.Sin(a) * (sacrum.Y - joint.Y) * 0.62f, joint.Z + MathF.Cos(a) * deep * 1.2f));
                }
                mesh.Sheet(blade, new Vector3(x, (joint.Y + sacrum.Y) * 0.5f, joint.Z), Vector3.UnitX, deep * 0.06f, bone, bone * 0.8f, GearMaterials.Bone, _ => BoneType.Pelvis);

                Vector3 boot = new Vector3(x * 0.25f, joint.Y - femur * 0.82f, joint.Z + femur * 0.42f);
                mesh.Tube(new[] { joint, (joint + boot) * 0.5f, boot }, new[] { deep * 0.09f, deep * 0.07f, deep * 0.08f }, 6, bone, GearMaterials.Bone, BoneType.Pelvis);
                mesh.Ball(boot, new Vector3(deep * 0.1f, deep * 0.12f, deep * 0.24f), 1, bone, GearMaterials.Bone, BoneType.Pelvis);
                Vector3 ischium = new Vector3(x * 0.4f, joint.Y - femur * 0.5f, joint.Z - femur * 0.38f);
                mesh.Tube(new[] { joint, ischium }, new[] { deep * 0.08f, deep * 0.04f }, 6, bone, GearMaterials.Bone, BoneType.Pelvis);
            }

            // Shoulder blades along the ribs, and the plates below them where the arms joined.
            if (!body.OnFour)
            {
                foreach (SaurianArm arm in body.Arms)
                {
                    Vector3 top = arm.Shoulder + new Vector3(-arm.Side * body.ChestWidth * 0.15f, body.ChestDepth * 0.75f, -body.ChestDepth * 0.65f);
                    mesh.Tube(new[] { arm.Shoulder, (arm.Shoulder + top) * 0.5f, top }, new[] { arm.Radius * 0.55f, arm.Radius * 0.4f, arm.Radius * 0.5f }, 6, bone, GearMaterials.Bone, BoneType.Chest, Vector3.UnitX, 0.35f);
                }
            }
            else
            {
                foreach (SaurianLeg leg in body.Legs)
                {
                    if (!leg.Front) continue;
                    Vector3 top = leg.Hip + new Vector3(-leg.Side * body.ChestWidth * 0.2f, body.ChestDepth * 0.9f, -body.ChestDepth * 0.5f);
                    mesh.Tube(new[] { leg.Hip, (leg.Hip + top) * 0.5f, top }, new[] { leg.HipRadius * 0.45f, leg.HipRadius * 0.35f, leg.HipRadius * 0.45f }, 6, bone, GearMaterials.Bone, BoneType.Chest, Vector3.UnitX, 0.35f);
                }
            }
        }

        // ---- Legs and arms: a long bone for each, with its knobbed ends, and the toes and fingers in their pieces.

        private static void Limbs(SaurianMesh mesh, SaurianBody body, Vector3 bone, Vector3 dark)
        {
            foreach (SaurianLeg leg in body.Legs)
            {
                bool left = leg.Side > 0f;
                BoneType upper = leg.Front ? (left ? BoneType.UpperArmL : BoneType.UpperArmR) : (left ? BoneType.UpperLegL : BoneType.UpperLegR);
                BoneType lower = leg.Front ? (left ? BoneType.LowerArmL : BoneType.LowerArmR) : (left ? BoneType.LowerLegL : BoneType.LowerLegR);
                BoneType foot = leg.Front ? (left ? BoneType.HandL : BoneType.HandR) : (left ? BoneType.FootL : BoneType.FootR);
                float thick = (leg.Knee - leg.Hip).Length * 0.07f;

                LongBone(mesh, leg.Hip, leg.Knee, thick, bone, upper);
                Vector3 behind = new Vector3(leg.Side * thick * 0.9f, 0f, -thick * 0.6f);
                LongBone(mesh, leg.Knee, leg.Ankle, thick * 0.85f, bone, lower);
                mesh.Tube(new[] { leg.Knee + behind, leg.Ankle + behind * 0.5f }, new[] { thick * 0.35f, thick * 0.25f }, 5, dark, GearMaterials.Bone, lower);

                // Three long bones from ankle to toes, bound together at the top and apart at the bottom.
                for (int m = -1; m <= 1; m++)
                {
                    Vector3 spread = new Vector3(m * thick * 0.55f, 0f, 0f);
                    mesh.Tube(new[] { leg.Ankle + spread * 0.4f, leg.Ball + spread }, new[] { thick * 0.38f, thick * 0.32f }, 5, bone, GearMaterials.Bone, foot);
                }
            }

            // Toes and claws are the same bones whether there's flesh on them or not.
            SaurianParts.Feet(mesh, body);
            SaurianParts.Hands(mesh, body);

            foreach (SaurianArm arm in body.Arms)
            {
                bool left = arm.Side > 0f;
                float thick = arm.Radius * 0.45f;
                LongBone(mesh, arm.Shoulder, arm.Elbow, thick, bone, left ? BoneType.UpperArmL : BoneType.UpperArmR);
                LongBone(mesh, arm.Elbow, arm.Wrist, thick * 0.75f, bone, left ? BoneType.LowerArmL : BoneType.LowerArmR);
            }
        }

        // A body whose skin is bone: what its toes, fingers, horns and frill are made of then.
        private static void Bare(SaurianBody body, Vector3 bone)
        {
            body.Coat.Back = bone / 0.6f * 0.75f;
            body.Coat.Pattern = bone * 0.2f;
            body.Coat.Flank = bone / 0.7f * 0.8f;
            body.Coat.Claw = bone * 0.7f;
            body.Coat.Horn = bone * 0.9f;
            body.Coat.Display = bone * 0.85f;
        }

        // A long bone: a shaft that narrows in the middle, with a knob at each end.
        private static void LongBone(SaurianMesh mesh, Vector3 from, Vector3 to, float thick, Vector3 colour, BoneType hang)
        {
            Vector3 d = to - from;
            mesh.Tube(new[] { from, from + d * 0.08f, from + d * 0.3f, from + d * 0.7f, from + d * 0.92f, to }, new[] { thick * 1.15f, thick * 1.2f, thick * 0.78f, thick * 0.78f, thick * 1.15f, thick * 1.05f }, 7, colour, GearMaterials.Bone, hang);
        }
    }
}

using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Procedural.Gear;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Saurian
{
    /// <summary>
    /// The small hard parts that go on a skin: teeth, eyes, toes and claws, fingers, horns, a frill, crests and quills. Each hangs from one bone, as each is a solid thing. Anything that would be smaller
    /// than a pixel from the game's camera isn't made at all: a body is paid for in triangles thousands of times over.
    /// </summary>
    public static class SaurianParts
    {
        private const float Smallest = 0.045f;     // Metres. About a pixel and a half from where the camera is.
        private static readonly Vector3 Tooth = new Vector3(0.6f, 0.55f, 0.44f);

        // ---- The head.

        /// <summary>Teeth along both jaws, biggest a little way back from the front as Sue's are, and set inside the lips or outside them as the species has it.</summary>
        public static void Teeth(SaurianMesh mesh, SaurianBody body, float scale)
        {
            SaurianHead head = body.Head;
            int count = head.Teeth;
            if (count <= 0 || head.Tooth * scale < Smallest) return;
            int every = head.Tooth * scale < Smallest * 1.8f ? 2 : 1;
            float gap = head.Length * 0.004f;

            for (int jaw = 0; jaw < 2; jaw++)
            {
                bool upper = jaw == 0;
                for (int side = -1; side <= 1; side += 2)
                {
                    for (int i = 0; i < count; i += every)
                    {
                        float u = count > 1 ? i / (count - 1f) : 0f;

                        // A sauropod crops with a few pegs at the front of its mouth, and has none further back.
                        float z = head.Sauropod ? head.Length * (upper ? 0.97f - 0.16f * u : 0.95f - 0.15f * u)
                            : head.Length * (upper ? 0.96f - 0.6f * u : 0.92f - 0.56f * u);
                        float size = head.Tooth * (0.5f + 0.5f * Smooth(0f, 0.22f, u) - 0.38f * Smooth(0.45f, 1f, u)) * (upper ? 1f : 0.86f);
                        if (size * scale < Smallest * 0.6f) continue;
                        float radius = size * 0.24f;
                        float inset = radius * (head.Lips * 2.4f - 0.6f);
                        float lip = head.Lip(z);
                        Vector3 root = new Vector3(side * (head.WidthAt(z) - inset), upper ? lip + radius : lip - gap - radius, z);
                        Vector3 way = upper ? -Vector3.UnitY : Vector3.UnitY;
                        Vector3 rootBody = head.ToBody(root);
                        Vector3 wayBody = head.ToBody(root + way) - rootBody;
                        Vector3 back = -head.Forward;
                        mesh.Spike(rootBody, wayBody, size + radius, radius, back, 0.18f, 4, Tooth, GearMaterials.Bone, upper ? BoneType.Head : BoneType.Jaw, 0.75f, head.Forward);
                    }
                }
            }
        }

        /// <summary>Eyes, set in their sockets. The living look out of them; what's been raised has a light in them instead.</summary>
        public static void Eyes(SaurianMesh mesh, SaurianBody body, Vector3 colour, float glow, int material)
        {
            SaurianHead head = body.Head;
            float z = head.EyeAt * head.Length;
            float y = SaurianSculpt.EyeHeight(head);
            float size = head.EyeSize;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 centre = head.ToBody(new Vector3(side * (head.WidthAt(z) * 0.98f - size * 0.28f), y, z));
                mesh.Ball(centre, new Vector3(size * 0.62f, size * 0.68f, size * 0.74f), 2, colour, material, BoneType.Head, glow);
            }
        }

        /// <summary>Horns over the eyes, as a Carnotaurus's or a horned grazer's, and one on the nose.</summary>
        public static void Horns(SaurianMesh mesh, SaurianBody body)
        {
            SaurianHead head = body.Head;
            Vector3 horn = body.Coat.Horn;
            float eyeZ = head.EyeAt * head.Length;
            float top = head.Lip(eyeZ) + head.DepthAt(eyeZ) * 1.24f;
            bool grazer = body.OnFour;

            if (head.BrowHorns > 0f)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 root = new Vector3(side * head.WidthAt(eyeZ) * 0.6f, top - head.BrowHorns * 0.08f, eyeZ - head.EyeSize * (grazer ? 0.6f : 0.1f));

                    // A grazer's go forward over its face; a hunter's stand up and out.
                    Vector3 way = grazer ? new Vector3(side * 0.18f, 0.55f, 0.82f) : new Vector3(side * 0.5f, 0.8f, -0.1f);
                    Vector3 bend = grazer ? new Vector3(0f, -0.4f, 1f) : new Vector3(side * 0.6f, 0.2f, 0.4f);
                    Vector3 rootBody = head.ToBody(root);
                    float length = head.BrowHorns;
                    mesh.Spike(rootBody, head.ToBody(root + way) - rootBody, length, length * (grazer ? 0.15f : 0.24f), head.ToBody(root + bend) - rootBody, grazer ? 0.22f : 0.35f, 7, horn, GearMaterials.Bone, BoneType.Head);
                }
            }

            if (head.NoseHorn > 0f)
            {
                float z = head.Length * 0.8f;
                Vector3 root = new Vector3(0f, head.Lip(z) + head.DepthAt(z) * 1.18f, z);
                Vector3 rootBody = head.ToBody(root);
                mesh.Spike(rootBody, head.ToBody(root + new Vector3(0f, 0.85f, 0.35f)) - rootBody, head.NoseHorn, head.NoseHorn * 0.24f, head.ToBody(root + Vector3.UnitZ) - rootBody, 0.2f, 7, horn, GearMaterials.Bone, BoneType.Head);
            }
        }

        /// <summary>A pair of thin crests along the top of the snout, which are shown off.</summary>
        public static void Crests(SaurianMesh mesh, SaurianBody body)
        {
            SaurianHead head = body.Head;
            if (head.Crest <= 0f) return;
            for (int side = -1; side <= 1; side += 2)
            {
                var outline = new List<Vector3>();
                const int Points = 14;
                for (int i = 0; i <= Points; i++)
                {
                    float t = i / (float)Points;
                    float z = head.Length * (0.22f + 0.66f * t);
                    float rise = MathF.Sin(MathF.Pow(t, 0.8f) * MathF.PI) * head.Crest * (1f + 0.25f * MathF.Sin(t * 17f) * 0.2f);
                    float top = head.Lip(z) + head.DepthAt(z) * 1.24f;
                    outline.Add(head.ToBody(new Vector3(side * head.WidthAt(z) * 0.32f, top + rise - head.Length * 0.015f, z)));
                }
                for (int i = Points - 1; i > 0; i--)
                {
                    float t = i / (float)Points;
                    float z = head.Length * (0.22f + 0.66f * t);
                    float top = head.Lip(z) + head.DepthAt(z) * 1.24f;
                    outline.Add(head.ToBody(new Vector3(side * head.WidthAt(z) * 0.32f, top - head.Length * 0.03f, z)));
                }
                Vector3 centre = Vector3.Zero;
                foreach (Vector3 p in outline) centre += p;
                centre /= outline.Count;
                Vector3 normal = Vector3.Cross(head.Up, head.Forward);
                mesh.Sheet(outline, centre, normal, head.Length * 0.012f, body.Coat.Display, Vector3.Lerp(body.Coat.Display, body.Coat.Pattern, 0.5f), GearMaterials.Leather, _ => BoneType.Head);
            }
        }

        /// <summary>A grazer's frill: a fan of bone from the back of the skull, solid on some and edged with spikes on others.</summary>
        public static void Frill(SaurianMesh mesh, SaurianBody body)
        {
            SaurianHead head = body.Head;
            if (head.Frill <= 0f) return;

            // It leans back from the skull, and goes from one jaw hinge, over the top, to the other.
            Vector3 root = new Vector3(0f, head.Lip(head.Length * 0.1f) + head.DepthAt(head.Length * 0.1f) * 0.9f, head.Length * 0.08f);
            Vector3 back = new Vector3(0f, 0.62f, -0.78f).Normalized();
            Vector3 side = Vector3.UnitX;
            float reach = head.Frill;
            float wide = head.BackWidth * 0.75f + reach * 0.35f;
            const int Points = 24;
            var outline = new List<Vector3>();
            var rim = new List<(Vector3 at, Vector3 out_)>();
            for (int i = 0; i <= Points; i++)
            {
                float a = MathF.PI * (i / (float)Points);
                float across = -MathF.Cos(a);
                float up = MathF.Sin(a);
                float scallop = 1f - 0.05f * MathF.Abs(MathF.Sin(a * 9f));
                Vector3 local = root + side * (across * wide * scallop) + back * (up * reach * scallop) + new Vector3(0f, -head.DepthAt(head.Length * 0.1f) * 0.9f * (1f - up), 0f);
                outline.Add(head.ToBody(local));
                rim.Add((local, (side * across + back * up).Normalized()));
            }
            Vector3 centre = head.ToBody(root + back * reach * 0.35f);
            Vector3 normal = (head.ToBody(Vector3.Cross(side, back)) - head.ToBody(Vector3.Zero)).Normalized();
            outline.Add(head.ToBody(root - back * reach * 0.05f));
            mesh.Sheet(outline, centre, normal, head.Length * 0.03f, body.Coat.Display, body.Coat.Horn, GearMaterials.Leather, _ => BoneType.Head);

            // Spikes off its edge, or the small bony knobs that edge every frill.
            int points = head.FrillSpikes > head.Length * 0.03f ? head.FrillPoints : 13;
            for (int i = 1; i < points; i++)
            {
                float t = i / (float)points;
                (Vector3 at, Vector3 out_) = rim[(int)MathF.Round(t * Points)];
                bool spike = head.FrillSpikes > head.Length * 0.03f;
                float length = spike ? head.FrillSpikes * (0.6f + 0.4f * MathF.Sin(t * MathF.PI)) : head.Length * 0.04f;
                if (length < Smallest) continue;
                Vector3 rootBody = head.ToBody(at);
                mesh.Spike(rootBody, head.ToBody(at + out_) - rootBody, length, length * (spike ? 0.18f : 0.4f), head.ToBody(at + back) - rootBody, spike ? 0.25f : 0f, 5, body.Coat.Horn, GearMaterials.Bone, BoneType.Head);
            }
        }

        // ---- Feet and hands.

        /// <summary>
        /// Toes on each hind foot, lying flat on the ground from the ball of the foot, with a claw on each: three forward and a small one behind, held off the ground. A raptor walks on two of the three,
        /// and holds the second up with its killing claw on it. A grazer has short blunt toes ending in hooves.
        /// </summary>
        public static void Feet(SaurianMesh mesh, SaurianBody body)
        {
            Vector3 skin = body.Coat.Back * 0.6f + body.Coat.Pattern * 0.25f;
            Vector3 claw = body.Coat.Claw;
            float sickle = body.Species.Sickle;

            foreach (SaurianLeg leg in body.Legs)
            {
                bool left = leg.Side > 0f;
                if (body.Sauropod)
                {
                    PillarFoot(mesh, leg, claw, leg.Front ? (left ? BoneType.HandL : BoneType.HandR) : (left ? BoneType.ToeL : BoneType.ToeR));
                    continue;
                }
                if (leg.Front)
                {
                    ForeFoot(mesh, leg, skin, claw, left ? BoneType.HandL : BoneType.HandR);
                    continue;
                }

                BoneType toeBone = left ? BoneType.ToeL : BoneType.ToeR;
                BoneType footBone = left ? BoneType.FootL : BoneType.FootR;
                float radius = leg.BallRadius * 0.42f;
                float inward = -leg.Side;

                if (body.OnFour)
                {
                    for (int d = -1; d <= 1; d++)
                    {
                        float angle = d * 0.42f;
                        Vector3 way = new Vector3(MathF.Sin(angle) * leg.Side, 0f, MathF.Cos(angle));
                        float length = leg.ToeLength * (d == 0 ? 1f : 0.82f);
                        Toe(mesh, leg.Ball + way * leg.BallRadius * 0.6f, way, length, radius * 1.4f, 2, skin, claw, toeBone, hoof: true);
                    }
                    continue;
                }

                // Digit III straight on, II turned in and IV out, each a little shorter.
                (float angle, float length, bool raised)[] digits = { (0f, 1f, false), (0.4f * inward, 0.78f, sickle > 0f), (-0.4f * inward, 0.84f, false) };
                foreach ((float angle, float length, bool raised) in digits)
                {
                    Vector3 way = new Vector3(MathF.Sin(angle), 0f, MathF.Cos(angle));
                    Vector3 start = leg.Ball + way * leg.BallRadius * 0.5f + new Vector3(0f, radius - leg.Ball.Y, 0f);
                    if (raised)
                    {
                        // The second toe, held up, with the killing claw on the end of it.
                        Vector3 up = (way * 0.55f + Vector3.UnitY * 0.83f).Normalized();
                        float toe = leg.ToeLength * 0.5f;
                        Vector3 end = start + up * toe;
                        mesh.Tube(new[] { start, start + up * toe * 0.5f, end }, new[] { radius * 1.05f, radius * 0.95f, radius * 0.8f }, 6, skin, GearMaterials.Leather, toeBone);
                        float sickleLength = leg.ToeLength * 0.62f * sickle;
                        if (sickleLength >= Smallest) mesh.Spike(end, way * 0.6f + Vector3.UnitY * 0.5f, sickleLength, radius * 0.95f, -Vector3.UnitY + way * 0.3f, 0.75f, 6, claw, GearMaterials.Bone, toeBone, 0.55f, Vector3.Cross(way, Vector3.UnitY));
                        continue;
                    }
                    Toe(mesh, start, way, leg.ToeLength * length, radius, 3, skin, claw, toeBone, hoof: false);
                }

                // The first toe, small and turned back, on the inside of the foot, off the ground.
                float hallux = leg.ToeLength * 0.32f;
                if (hallux >= Smallest)
                {
                    Vector3 way = new Vector3(inward * 0.5f, -0.25f, -0.83f).Normalized();
                    Vector3 start = Vector3.Lerp(leg.Ankle, leg.Ball, 0.72f) + new Vector3(inward * leg.BallRadius * 0.7f, 0f, -leg.BallRadius * 0.4f);
                    Toe(mesh, start, way, hallux, radius * 0.8f, 2, skin, claw, footBone, hoof: false);
                }
            }
        }

        private static void Toe(SaurianMesh mesh, Vector3 start, Vector3 way, float length, float radius, int joints, Vector3 skin, Vector3 claw, Hang hang, bool hoof)
        {
            // Knuckles stand out, and each bone is a little thinner than the last.
            var path = new List<Vector3>();
            var radii = new List<float>();
            int rings = joints * 2 + 1;
            float flesh = length * (hoof ? 0.7f : 0.78f);
            for (int i = 0; i <= rings; i++)
            {
                float t = i / (float)rings;
                path.Add(start + way * (flesh * t));
                float knuckle = i % 2 == 0 ? 1.12f : 0.94f;
                radii.Add(radius * (1f - 0.35f * t) * knuckle);
            }
            mesh.Tube(path, radii, 6, skin, GearMaterials.Leather, hang, Vector3.UnitY, 0.78f);

            float clawLength = length - flesh + radius * 0.5f;
            if (clawLength < Smallest * 0.7f) return;
            Vector3 tip = path[^1] - way * radius * 0.2f;
            if (hoof) mesh.Spike(tip, way + Vector3.UnitY * -0.25f, clawLength, radius * 0.95f, -Vector3.UnitY, 0.2f, 6, claw, GearMaterials.Bone, hang, 0.55f, Vector3.UnitY);
            else mesh.Spike(tip, way, clawLength, radius * 0.7f, -Vector3.UnitY, 0.45f, 5, claw, GearMaterials.Bone, hang, 0.6f, Vector3.Cross(way, Vector3.UnitY));
        }

        // A sauropod's claws. A hind foot ends in short toes buried in the front of its pad, and the inner three show claws, turned in and down and the first the biggest, as the bones of the foot say.
        // A brachiosaur's hand has only its thumb claw, on the inside of the column, and the rest of its fingers are inside the pad.
        private static void PillarFoot(SaurianMesh mesh, SaurianLeg leg, Vector3 claw, BoneType bone)
        {
            float inward = -leg.Side;
            float pad = leg.BallRadius;
            if (leg.Front)
            {
                Vector3 thumbWay = new Vector3(inward * 0.45f, -0.2f, 0.87f).Normalized();
                Vector3 thumbAt = new Vector3(leg.Ball.X + inward * pad * 0.62f, pad * 0.36f, leg.Ball.Z + pad * 0.55f);
                float thumb = pad * 0.5f;
                if (thumb >= Smallest) mesh.Spike(thumbAt, thumbWay, thumb, pad * 0.14f, -Vector3.UnitY, 0.5f, 6, claw, GearMaterials.Bone, bone, 0.6f, Vector3.UnitY);
                return;
            }

            for (int digit = 0; digit < 3; digit++)
            {
                float angle = (0.45f - digit * 0.36f) * inward;
                Vector3 way = new Vector3(MathF.Sin(angle), 0f, MathF.Cos(angle));
                Vector3 at = new Vector3(leg.Ball.X, pad * 0.3f, leg.Ball.Z) + way * pad * 0.72f;
                float length = leg.ToeLength * (digit == 0 ? 0.95f : digit == 1 ? 0.78f : 0.6f);
                if (length < Smallest) continue;
                Vector3 points = (way * 0.85f + new Vector3(inward * 0.3f, -0.3f, 0f)).Normalized();
                mesh.Spike(at, points, length, pad * (digit == 0 ? 0.17f : 0.14f), -Vector3.UnitY, 0.45f, 6, claw, GearMaterials.Bone, bone, 0.55f, Vector3.UnitY);
            }
        }

        // What a horned grazer stands on in front: short fingers round a pad, the inner three with hooves.
        private static void ForeFoot(SaurianMesh mesh, SaurianLeg leg, Vector3 skin, Vector3 claw, BoneType hand)
        {
            float radius = leg.BallRadius * 0.38f;
            for (int d = 0; d < 5; d++)
            {
                float angle = (d - 2f) * 0.38f * leg.Side;
                Vector3 way = new Vector3(MathF.Sin(angle), 0f, MathF.Cos(angle));
                Vector3 start = leg.Ball + way * leg.BallRadius * 0.55f + new Vector3(0f, radius - leg.Ball.Y, 0f);
                Toe(mesh, start, way, leg.ToeLength * (d < 3 ? 1f : 0.7f), radius, 1, skin, claw, hand, hoof: d < 3);
            }
        }

        /// <summary>Fingers on a theropod's hands, curled forward with a claw each. A tyrant has two, and a raptor three long ones with claws to hold on with.</summary>
        public static void Hands(SaurianMesh mesh, SaurianBody body)
        {
            Vector3 skin = body.Coat.Flank * 0.7f;
            foreach (SaurianArm arm in body.Arms)
            {
                BoneType hand = arm.Side > 0f ? BoneType.HandL : BoneType.HandR;
                Vector3 way = (arm.Hand - arm.Wrist).Normalized();
                Vector3 across = new Vector3(arm.Side, 0f, 0f);
                Vector3 palm = Vector3.Cross(way, across).Normalized();
                float length = (arm.Hand - arm.Wrist).Length * 0.55f;
                float radius = arm.Radius * 0.24f;
                for (int f = 0; f < arm.Fingers; f++)
                {
                    float spread = arm.Fingers == 1 ? 0f : (f / (arm.Fingers - 1f) - 0.5f);
                    Vector3 start = arm.Wrist + way * ((arm.Hand - arm.Wrist).Length * 0.45f) + palm * (spread * arm.Radius * 0.7f);
                    Vector3 fingerWay = (way + palm * spread * 0.25f - across * 0.05f).Normalized();
                    float own = length * (1f - MathF.Abs(spread) * 0.3f);
                    Toe(mesh, start, fingerWay, own, radius, 2, skin, body.Coat.Claw, hand, hoof: false);
                    if (arm.Claw * 0.5f >= Smallest) mesh.Spike(start + fingerWay * own * 0.95f, fingerWay, arm.Claw * 0.55f, radius * 0.8f, -Vector3.UnitY + fingerWay * 0.2f, 0.55f, 5, body.Coat.Claw, GearMaterials.Bone, hand, 0.55f, across);
                }
            }
        }

        // ---- What grows out of the hide.

        /// <summary>A mane of stiff quills down the neck and back and along the tail, and short feathers along the back of each forearm, as dromaeosaurs had.</summary>
        public static void Quills(SaurianMesh mesh, SaurianBody body, SaurianDress dress)
        {
            if (body.Quills <= 0f) return;
            SaurianAxis axis = body.Axis;
            Vector3 colour = Vector3.Lerp(body.Coat.Back, body.Coat.Display, 0.55f);
            Vector3 tip = body.Coat.Pattern;
            float from = body.NeckTopAt, to = body.HipAt * 0.55f;
            float spacing = body.Quills * 0.32f;
            for (float at = from; at > to; at -= spacing)
            {
                float t = (from - at) / (from - to);
                float length = body.Quills * (0.55f + 0.45f * MathF.Sin(MathF.Min(t * 2.2f, 1f) * MathF.PI * 0.5f)) * (1f - 0.6f * Smooth(0.6f, 1f, t));
                if (length < Smallest) continue;
                Vector3 centre = axis.At(at);
                Vector3 way = axis.Way(at);
                Vector3 root = centre + Vector3.UnitY * (axis.DeepAt(at) * 0.92f);
                Vector3 lean = (Vector3.UnitY * 0.6f - way * 0.8f).Normalized();
                mesh.Spike(root, lean, length, length * 0.13f, -way, 0.2f, 4, Vector3.Lerp(colour, tip, t * 0.3f), GearMaterials.Leather, dress.Along(at), 0.35f, way);
            }

            foreach (SaurianArm arm in body.Arms)
            {
                BoneType lower = arm.Side > 0f ? BoneType.LowerArmL : BoneType.LowerArmR;
                Vector3 forearm = arm.Wrist - arm.Elbow;
                for (int i = 0; i < 5; i++)
                {
                    float t = (i + 0.5f) / 5f;
                    float length = body.Quills * 0.55f * (0.7f + 0.3f * t);
                    if (length < Smallest) continue;
                    Vector3 root = arm.Elbow + forearm * t + new Vector3(0f, -arm.Radius * 0.3f, -arm.Radius * 0.4f);
                    Vector3 way = (new Vector3(arm.Side * 0.2f, -0.55f, -0.8f)).Normalized();
                    mesh.Spike(root, way, length, length * 0.12f, -forearm, 0.15f, 4, colour, GearMaterials.Leather, lower, 0.3f, forearm);
                }
            }
        }

        /// <summary>A row of low spines down the middle of the back and along the tail.</summary>
        public static void Scutes(SaurianMesh mesh, SaurianBody body, SaurianDress dress)
        {
            if (body.Scutes <= 0f) return;
            SaurianAxis axis = body.Axis;
            float spacing = body.Scutes * 2.6f;
            for (float at = body.NeckAt + (body.HeadAt - body.NeckAt) * 0.5f; at > body.HipAt * 0.3f; at -= spacing)
            {
                float size = body.Scutes * (0.7f + 0.3f * FloraWave(at * 3.1f));
                if (size < Smallest) continue;
                Vector3 root = axis.At(at) + Vector3.UnitY * (axis.DeepAt(at) * 0.93f);
                mesh.Spike(root, Vector3.UnitY - axis.Way(at) * 0.35f, size, size * 0.55f, -axis.Way(at), 0.15f, 4, body.Coat.Pattern, GearMaterials.Leather, dress.Along(at), 0.6f, axis.Way(at));
            }
        }

        private static float FloraWave(float at) => MathF.Sin(at) * 0.5f + 0.5f;

        private static float Smooth(float from, float to, float value)
        {
            float t = Math.Clamp((value - from) / (to - from), 0f, 1f);
            return t * t * (3f - 2f * t);
        }
    }
}

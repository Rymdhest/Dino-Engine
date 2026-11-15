using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.Modelling.Procedural.Saurian;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Character
{
    public static partial class SkeletonForge
    {
        // Where the jaw hinges, under the back of each cheekbone, from the skull's middle.
        private static readonly Vector3 Hinge = new Vector3(0f, -0.15f, -0.07f);

        // The curve the jaw's body follows under its teeth: how wide, how far forward from its middle, and where its middle is front to back, from the skull's middle.
        private static readonly Vector3 LowerArc = new Vector3(0.12f, 0.135f, 0.085f);

        // The eye sockets and the opening of the nose, from the skull's middle: what's in them is dark, and the light sits in the sockets.
        private static readonly Vector3 Orbit = new Vector3(0.098f, -0.045f, 0.235f);
        private static readonly Vector3 OrbitRadii = new Vector3(0.072f, 0.068f, 0.13f);
        private static readonly Vector3 Nose = new Vector3(0f, -0.15f, 0.27f);
        private static readonly Vector3 NoseRadii = new Vector3(0.038f, 0.058f, 0.09f);

        /// <summary>Where each eye's light burns, deep in its socket.</summary>
        internal static Vector3 EyeAt(SkeletonBody body, float side) => SkeletonBody.SkullAt + new Vector3(side * Orbit.X, Orbit.Y - 0.005f, 0.15f) * body.SkullSize;

        // ---- The skull: a cranium and a face, with the sockets, the nose and the hollows under the cheekbones cut out of it, and the jaw a field of its own hung under it.

        private static void Skull(SaurianMesh mesh, SkeletonBody body)
        {
            float k = body.SkullSize;
            float face = body.FaceWidth;
            Vector3 S = SkeletonBody.SkullAt;
            Vector3 At(float x, float y, float z) => S + new Vector3(x, y, z) * k;
            Vector3 Size(float x, float y, float z) => new Vector3(x, y, z) * k;

            var skull = new SaurianField();
            const SaurianPart Head = SaurianPart.Head;
            skull.Ellipsoid(Head, At(0f, 0.07f, -0.04f), Size(0.245f, 0.265f, 0.3f), 0.05f * k);
            skull.Ellipsoid(Head, At(0f, 0.04f, 0.09f), Size(0.225f, 0.21f, 0.19f), 0.08f * k);
            skull.Ellipsoid(Head, At(0f, -0.16f, 0.15f), Size(0.155f * face, 0.12f, 0.13f), 0.08f * k);
            // The ridge the upper teeth stand in, round the front of the face.
            skull.Ellipsoid(Head, At(0f, -0.235f, 0.1f), Size(0.13f * face, 0.035f, 0.165f), 0.05f * k);

            // A heavy brow over the sockets, or hardly any.
            float brow = (0.026f + 0.016f * body.Brow) * k;
            for (int s = -1; s <= 1; s += 2)
            {
                skull.Cone(Head, At(0f, 0.0f, 0.265f), At(s * 0.16f, 0.005f, 0.2f), brow, brow * 0.8f, 0.04f * k);

                // The cheekbones, and the arch of bone back from each to over the jaw's hinge, which the jaw's muscle ran under.
                skull.Ellipsoid(Head, At(s * 0.155f * face, -0.09f, 0.15f), Size(0.065f, 0.055f, 0.07f), 0.04f * k);
                skull.Cone(Head, At(s * 0.17f * face, -0.1f, 0.1f), At(s * 0.215f, -0.1f, -0.07f), 0.032f * k, 0.028f * k, 0.03f * k);

                // Behind each ear, the knob the neck's muscles held.
                skull.Ellipsoid(Head, At(s * 0.18f, -0.2f, -0.1f), Size(0.04f, 0.06f, 0.04f), 0.04f * k);
            }

            for (int s = -1; s <= 1; s += 2)
            {
                skull.Ellipsoid(Head, S + new Vector3(s * Orbit.X, Orbit.Y, Orbit.Z) * k, OrbitRadii * k, 0.02f * k, carve: true);
                skull.Ellipsoid(Head, At(s * 0.255f, -0.02f, 0.02f), Size(0.04f, 0.09f, 0.11f), 0.03f * k, carve: true);
            }
            skull.Ellipsoid(Head, S + Nose * k, NoseRadii * k, 0.015f * k, carve: true);
            skull.Ellipsoid(Head, At(0f, -0.105f, 0.275f), Size(0.02f, 0.03f, 0.08f), 0.01f * k, carve: true);

            Vector3 bone = body.Tint("skull");
            Hang head = SkeletonBody.On(BoneType.Head);
            var dress = new Dresser(GearMaterials.Bone, (p, n) => bone * Hollow(p, k), _ => head);
            SaurianSkin.Grow(mesh, skull, 0.009f * k, 1100, float.PositiveInfinity, dress, settle: true);

            Jaw(mesh, body, bone);
            Teeth(mesh, body, bone);
        }

        // Dark in the sockets and up the nose, where no light gets and earth has: the deeper behind the face, the darker, as that's what makes a skull read from across a field.
        private static float Hollow(Vector3 p, float k)
        {
            Vector3 local = (p - SkeletonBody.SkullAt) / k;
            float socket = MathF.Max(Behind(local, new Vector3(Orbit.X, Orbit.Y, 0.235f), OrbitRadii, 0.05f), Behind(local, new Vector3(-Orbit.X, Orbit.Y, 0.235f), OrbitRadii, 0.05f));
            float nose = Behind(local, new Vector3(Nose.X, Nose.Y, 0.265f), NoseRadii, 0.05f);
            return 1f - 0.88f * MathF.Max(socket, nose);
        }

        // How far into an opening a point on the skull is: inside its outline seen from the front, and from nothing at its mouth to all of it <paramref name="deep"/> behind.
        private static float Behind(Vector3 local, Vector3 mouth, Vector3 radii, float deep)
        {
            float across = MathF.Sqrt(MathF.Pow((local.X - mouth.X) / radii.X, 2f) + MathF.Pow((local.Y - mouth.Y) / radii.Y, 2f));
            float within = 1f - Math.Clamp((across - 0.85f) / 0.3f, 0f, 1f);
            float depth = Math.Clamp((mouth.Z - local.Z) / deep, 0f, 1f);
            return within * depth * depth * (3f - 2f * depth);
        }

        // The jaw as it is shut, then let fall about its hinge.
        private static Vector3 Opened(SkeletonBody body, Vector3 local)
        {
            Vector3 from = local - Hinge;
            float c = MathF.Cos(body.Gape), s = MathF.Sin(body.Gape);
            // Turning about x, the chin going down.
            return Hinge + new Vector3(from.X, from.Y * c - from.Z * s, from.Y * s + from.Z * c);
        }

        private static void Jaw(SaurianMesh mesh, SkeletonBody body, Vector3 bone)
        {
            float k = body.SkullSize;
            float face = body.FaceWidth;
            Vector3 S = SkeletonBody.SkullAt;
            Vector3 At(float x, float y, float z) => S + Opened(body, new Vector3(x, y, z)) * k;
            Vector3 Way(float x, float y, float z) => Opened(body, new Vector3(x, y, z) + Hinge) - Hinge;

            var jaw = new SaurianField();
            const SaurianPart Head = SaurianPart.Head;
            jaw.Ellipsoid(Head, At(0f, -0.355f, 0.2f), new Vector3(0.06f, 0.045f, 0.04f) * k, 0.04f * k, Way(0f, 0f, 1f), Way(0f, 1f, 0f));
            for (int s = -1; s <= 1; s += 2)
            {
                // Its body, round under the teeth from the chin and back to its corner: tall and thin, as a jaw is, and a U rather than a V, as the teeth in it are.
                float w = s * face;
                Vector3 Round(float a, float y) => new Vector3(w * LowerArc.X * MathF.Sin(a), y, LowerArc.Z + LowerArc.Y * MathF.Cos(a));
                Vector3[] line = { Round(0f, -0.345f), Round(0.5f, -0.343f), Round(1.0f, -0.34f), Round(1.45f, -0.337f), new Vector3(w * 0.165f, -0.335f, -0.025f) };
                for (int i = 0; i + 1 < line.Length; i++)
                {
                    Vector3 a = line[i], b = line[i + 1];
                    jaw.Cone(Head, At(a.X, a.Y, a.Z), At(b.X, b.Y, b.Z), (0.037f - 0.001f * i) * k, (0.036f - 0.001f * i) * k, 0.025f * k, 1.5f, Way(0f, 1f, 0f));
                }
                jaw.Ellipsoid(Head, At(w * 0.165f, -0.335f, -0.025f), new Vector3(0.026f, 0.045f, 0.04f) * k, 0.03f * k, Way(0f, 0f, 1f), Way(0f, 1f, 0f));

                // Up from its corner to the hinge, a flat plate, and the point in front of it the muscle pulled on.
                jaw.Cone(Head, At(w * 0.165f, -0.33f, -0.025f), At(s * 0.19f, -0.165f, -0.07f), 0.04f * k, 0.03f * k, 0.03f * k, 1.45f, Way(0f, 0f, 1f));
                jaw.Cone(Head, At(w * 0.162f, -0.29f, 0.02f), At(w * 0.168f, -0.235f, 0.035f), 0.024f * k, 0.013f * k, 0.02f * k, 1.7f, Way(0f, 0f, 1f));
            }

            Hang head = SkeletonBody.On(BoneType.Head);
            var dress = new Dresser(GearMaterials.Bone, (p, n) => bone, _ => head);
            SaurianSkin.Grow(mesh, jaw, 0.0065f * k, 380, float.PositiveInfinity, dress, settle: true);
        }

        // Each tooth on its own, as a row of them is what tells a skull's grin from across a field; a few gone, as more are the older it is.
        private static void Teeth(SaurianMesh mesh, SkeletonBody body, Vector3 bone)
        {
            float k = body.SkullSize;
            Vector3 S = SkeletonBody.SkullAt;
            Vector3 enamel = bone * new Vector3(1.08f, 1.05f, 0.94f);
            Hang head = SkeletonBody.On(BoneType.Head);
            var gone = body.Genes.Stream("teeth");
            const int Row = 10;

            for (int jaw = 0; jaw < 2; jaw++)
            {
                bool lower = jaw == 1;
                float rx = (lower ? 0.112f : 0.122f) * body.FaceWidth, rz = lower ? 0.128f : 0.14f;
                Vector3 centre = lower ? new Vector3(0f, -0.305f, LowerArc.Z) : new Vector3(0f, -0.255f, 0.105f);
                for (int i = 0; i < Row; i++)
                {
                    float t = (i + 0.5f) / Row * 2f - 1f;
                    bool front = MathF.Abs(t) < 0.45f;
                    if (gone.Chance(front ? 0.08f : 0.14f)) continue;
                    float angle = t * 1.25f;
                    Vector3 at = centre + new Vector3(rx * MathF.Sin(angle), 0f, rz * MathF.Cos(angle));
                    // Thinner front to back than along the row.
                    Vector3 outward = new Vector3(MathF.Sin(angle) / rx, 0f, MathF.Cos(angle) / rz).Normalized();
                    float wide = (front ? 0.016f : 0.02f) * k;
                    float root = lower ? -0.012f : 0.018f;
                    float tip = lower ? 0.036f : -0.032f;
                    Vector3 a = at + new Vector3(0f, root, 0f), b = at + new Vector3(0f, tip, 0f);
                    if (lower)
                    {
                        a = Opened(body, a);
                        b = Opened(body, b);
                    }
                    mesh.Tube(new[] { S + a * k, S + b * k }, new[] { wide, wide * 0.8f }, 4, enamel, GearMaterials.Bone, head, outward, 0.75f);
                }
            }
        }
    }
}

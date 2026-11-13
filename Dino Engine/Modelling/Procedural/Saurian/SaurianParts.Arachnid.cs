using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Procedural.Gear;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Saurian
{
    public static partial class SaurianParts
    {
        /// <summary>
        /// More eyes, on what the Institute grew from more than one animal: a smaller pair over and behind the first, another forward on top of the snout, and a fourth low on the sides behind,
        /// all looking out, as a hunting spider's do. It's the first thing anyone sees of its face.
        /// </summary>
        public static void MoreEyes(SaurianMesh mesh, SaurianBody body, Vector3 colour, float glow, int material)
        {
            SaurianHead head = body.Head;
            float eyeZ = head.EyeAt * head.Length;
            float size = head.EyeSize;
            (float z, float up, float across, float scale)[] pairs =
            {
                (eyeZ - size * 1.1f, size * 1.25f, 0.62f, 0.62f),
                (eyeZ + head.Length * 0.16f, size * 0.95f, 0.5f, 0.5f),
                (eyeZ - size * 1.9f, -size * 0.2f, 0.92f, 0.46f),
            };
            for (int pair = 0; pair < Math.Min(body.Species.EyePairs - 1, pairs.Length); pair++)
            {
                (float z, float up, float across, float scale) = pairs[pair];
                float y = SaurianSculpt.EyeHeight(head) + up;
                for (int side = -1; side <= 1; side += 2)
                {
                    float s = size * scale;
                    if (s < Smallest * 0.5f) continue;
                    Vector3 centre = head.ToBody(new Vector3(side * head.WidthAt(z) * across, y, z));
                    mesh.Ball(centre, new Vector3(s * 0.66f, s * 0.66f, s * 0.7f), 2, colour, material, BoneType.Head, glow);
                }
            }
        }

        /// <summary>A pair of fangs from the front of the jaw, curved down and back, as long as its kind has them: what it puts its venom in with.</summary>
        public static void Fangs(SaurianMesh mesh, SaurianBody body)
        {
            SaurianHead head = body.Head;
            float length = body.Species.Fangs.Max > 0f ? body.Genes.Part("fangs").Range("length", body.Species.Fangs) * head.Length : 0f;
            if (length < Smallest) return;
            Vector3 claw = body.Coat.Claw * 0.8f;
            for (int side = -1; side <= 1; side += 2)
            {
                float z = head.Length * 0.88f;
                Vector3 root = new Vector3(side * head.WidthAt(z) * 0.42f, head.Lip(z) - head.Length * 0.02f, z);
                Vector3 rootBody = head.ToBody(root);
                Vector3 down = head.ToBody(root + new Vector3(side * 0.15f, -1f, 0.35f)) - rootBody;
                Vector3 back = head.ToBody(root - Vector3.UnitZ) - rootBody;
                mesh.Spike(rootBody, down, length, length * 0.16f, back, 0.45f, 6, claw, GearMaterials.Bone, BoneType.Jaw, 0.7f, head.Forward);
            }
        }

        // A hooked claw at the end of a leg on eight, down into the ground as it stands.
        private static void LegClaw(SaurianMesh mesh, SaurianBody body, SaurianLeg leg, Vector3 claw)
        {
            float length = leg.ToeLength;
            if (length < Smallest) return;
            Vector3 out_ = new Vector3(leg.Ball.X - leg.Ankle.X, 0f, leg.Ball.Z - leg.Ankle.Z);
            out_ = out_.LengthSquared > 1e-6f ? out_.Normalized() : new Vector3(leg.Side, 0f, 0f);
            BoneType foot = SaurianBody.LegEnds[Math.Max(body.IndexOf(leg), 0)];
            mesh.Spike(leg.Ball, out_ * 0.4f - Vector3.UnitY, length, leg.BallRadius * 0.9f, out_, 0.4f, 5, claw, GearMaterials.Bone, foot, 0.7f, Vector3.Cross(out_, Vector3.UnitY));
        }
    }
}

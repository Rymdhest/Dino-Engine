using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Procedural.Gear;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Saurian
{
    public static partial class SaurianParts
    {
        /// <summary>
        /// Armour, as an ankylosaur's was: rows of oval plates of bone across its back from the neck to the middle of the tail, each with a low keel, a row of short spikes out along each flank,
        /// and a pair of horns at the back corners of its skull. From up where the camera is it's a shell, which is what it is.
        /// </summary>
        public static void Armour(SaurianMesh mesh, SaurianBody body, SaurianDress dress)
        {
            float plate = body.Armour;
            if (plate < Smallest) return;
            SaurianAxis axis = body.Axis;
            Vector3 horn = body.Coat.Horn;
            Vector3 shell = Vector3.Lerp(body.Coat.Back, body.Coat.Horn, 0.45f);
            float from = body.NeckAt + (body.NeckTopAt - body.NeckAt) * 0.5f;
            float to = body.HipAt * 0.45f;
            float spacing = plate * 1.7f;
            int row = 0;
            for (float at = from; at > to; at -= spacing, row++)
            {
                Vector3 centre = axis.At(at), way = axis.Way(at);
                Vector3 up = (Vector3.UnitY - way * Vector3.Dot(Vector3.UnitY, way)).Normalized();
                Vector3 across = Vector3.Cross(up, way);
                float deep = axis.DeepAt(at), wide = axis.WideAt(at);
                Hang hang = dress.Along(at);
                float size = plate * (0.75f + 0.25f * MathF.Min(deep / MathF.Max(body.ChestDepth, 1e-3f), 1f));
                int across_ = Math.Clamp((int)(wide * 2.2f / (size * 1.6f)), 2, 6);
                float stagger = row % 2 == 0 ? 0f : 0.5f;
                for (int k = 0; k < across_; k++)
                {
                    float angle = ((k + stagger) / (across_ - 1 + stagger * 2f) - 0.5f) * 2.1f;
                    Vector3 out_ = (up * MathF.Cos(angle) + across * MathF.Sin(angle)).Normalized();
                    Vector3 root = centre + up * (MathF.Cos(angle) * deep * 1.02f) + across * (MathF.Sin(angle) * wide * 1.02f);
                    // A plate is a short, broad, flattened spike lying back along the body, its keel along the middle of it.
                    mesh.Spike(root - way * size * 0.2f, out_ * 0.45f - way * 0.9f, size, size * 0.5f, out_, 0.15f, 5, k % 2 == 0 ? shell : shell * 0.85f, GearMaterials.Bone, hang, 0.32f, out_);
                }

                // Short spikes out along the flanks, under the plates.
                if (row % 2 == 0 && at < body.ChestAt && at > body.HipAt * 0.6f)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 root = centre + across * (side * wide * 0.98f) - up * (deep * 0.15f);
                        Vector3 out_ = (across * side - way * 0.35f + up * 0.1f).Normalized();
                        mesh.Spike(root, out_, size * 1.2f, size * 0.32f, -way, 0.2f, 5, horn, GearMaterials.Bone, hang, 0.55f, up);
                    }
                }
            }

            // A pair of horns at the back corners of the skull, pointing back and out.
            SaurianHead head = body.Head;
            for (int side = -1; side <= 1; side += 2)
            {
                float z = head.Length * 0.08f;
                Vector3 root = new Vector3(side * head.WidthAt(z) * 0.9f, head.Lip(z) + head.DepthAt(z) * 1.1f, z);
                Vector3 rootBody = head.ToBody(root);
                Vector3 out_ = head.ToBody(root + new Vector3(side * 0.8f, 0.2f, -0.55f)) - rootBody;
                mesh.Spike(rootBody, out_, head.Length * 0.22f, head.Length * 0.07f, -head.Forward, 0.2f, 6, horn, GearMaterials.Bone, BoneType.Head);
            }
        }

        /// <summary>A club at the end of the tail, as an ankylosaur's: two great lobes of bone either side of a knob, which is what it swings.</summary>
        public static void Club(SaurianMesh mesh, SaurianBody body)
        {
            float club = body.Club;
            if (club < Smallest) return;
            SaurianAxis axis = body.Axis;
            float at = body.TipAt * 0.55f;
            Vector3 centre = axis.At(at), way = axis.Way(at);
            Vector3 up = (Vector3.UnitY - way * Vector3.Dot(Vector3.UnitY, way)).Normalized();
            Vector3 across = Vector3.Cross(up, way);
            Vector3 bone = Vector3.Lerp(body.Coat.Horn, body.Coat.Back, 0.35f);
            for (int side = -1; side <= 1; side += 2) mesh.Ball(centre + across * (side * club * 0.42f), new Vector3(club * 0.55f, club * 0.42f, club * 0.62f), 2, bone, GearMaterials.Bone, BoneType.TailTip);
            mesh.Ball(centre - way * club * 0.35f, new Vector3(club * 0.36f, club * 0.34f, club * 0.5f), 2, bone * 0.9f, GearMaterials.Bone, BoneType.TailTip);
        }

        /// <summary>A dome of bone over the back of the skull, as a pachycephalosaur's, ringed with knobs, and a few knobs along its snout: what it butts with.</summary>
        public static void Dome(SaurianMesh mesh, SaurianBody body)
        {
            SaurianHead head = body.Head;
            if (head.Dome < Smallest) return;
            Vector3 knob = body.Coat.Horn;
            float z = head.Length * 0.22f;
            float top = head.Lip(z) + head.DepthAt(z) * 1.24f;
            const int Knobs = 9;
            for (int k = 0; k < Knobs; k++)
            {
                float a = MathF.PI * (0.15f + 0.7f * k / (Knobs - 1f));
                Vector3 at = new Vector3(MathF.Cos(a) * head.WidthAt(z) * 0.62f, top + head.Dome * 0.05f - MathF.Sin(a) * head.Dome * 0.1f, z - MathF.Sin(a) * head.Length * 0.2f);
                Vector3 rootBody = head.ToBody(at);
                Vector3 out_ = head.ToBody(at + new Vector3(MathF.Cos(a), 0.3f, -MathF.Sin(a) * 0.6f)) - rootBody;
                mesh.Spike(rootBody, out_, head.Dome * 0.32f, head.Dome * 0.14f, out_, 0f, 5, knob, GearMaterials.Bone, BoneType.Head);
            }
            for (int k = 0; k < 3; k++)
            {
                float snout = head.Length * (0.55f + 0.12f * k);
                Vector3 at = new Vector3(0f, head.Lip(snout) + head.DepthAt(snout) * 1.22f, snout);
                Vector3 rootBody = head.ToBody(at);
                mesh.Spike(rootBody, head.Up, head.Dome * 0.2f, head.Dome * 0.1f, head.Up, 0f, 5, knob, GearMaterials.Bone, BoneType.Head);
            }
        }
    }
}

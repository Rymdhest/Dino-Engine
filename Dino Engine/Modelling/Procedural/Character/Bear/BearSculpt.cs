using Dino_Engine.Modelling.Procedural.Saurian;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Character
{
    /// <summary>
    /// A bear's flesh as a field of signed distances (<see cref="SaurianField"/>, the dinosaurs' own): rounded cones down a long, broad trunk, the hump of muscle over its shoulders, a belly that hangs
    /// a little, a short neck as thick as the head, legs like columns, each blended into the body only near where it joins, on broad paws with the sole flat on the ground, and a domed head with
    /// broad cheeks, a steep brow and a short thick muzzle. What's built here is the shape, and the skin is grown from it.
    /// </summary>
    public static class BearSculpt
    {
        public static SaurianField Flesh(BearBody body)
        {
            var field = new SaurianField();
            Trunk(field, body);
            for (int side = -1; side <= 1; side += 2)
            {
                Foreleg(field, body, side);
                HindLeg(field, body, side);
            }
            Head(field, body);
            return field;
        }

        private static Vector3 Mirror(Vector3 left, int side) => side > 0 ? left : BearBody.Right(left);

        private static void Trunk(SaurianField field, BearBody body)
        {
            BoarStation[] trunk = body.Trunk;
            for (int i = 0; i + 1 < trunk.Length; i++)
            {
                BoarStation a = trunk[i], b = trunk[i + 1];
                float squash = (a.Deep + b.Deep) / MathF.Max(a.Half + b.Half, 1e-4f);
                field.Cone(SaurianPart.Body, new Vector3(0f, a.Middle, a.Z), new Vector3(0f, b.Middle, b.Z), a.Deep, b.Deep, MathF.Min(a.Deep, b.Deep) * 0.35f, squash);
            }

            // The neck is short and as thick as the head is wide: its throat runs from the chest up under the jaw, and its top on from the hump down to behind the ears.
            BoarStation last = trunk[^1];
            float l = body.HeadLength;
            Vector3 throat = body.FromHead(new Vector3(0f, -l * 0.12f, l * 0.18f));
            field.Cone(SaurianPart.Body, new Vector3(0f, last.Middle, last.Z), throat, last.Deep * 0.95f, l * 0.3f, l * 0.12f, 1.15f);
            Vector3 poll = body.FromHead(new Vector3(0f, l * 0.12f, l * 0.1f));
            field.Cone(SaurianPart.Body, new Vector3(0f, body.W(0.9f), body.ForeZ), poll, body.W(0.15f), l * 0.25f, body.W(0.08f), 1.3f);

            // The hump: the muscle over the shoulders a bear digs with, the highest thing about it.
            field.Ellipsoid(SaurianPart.Body, new Vector3(0f, body.W(0.88f), body.ForeZ - body.W(0.03f)), new Vector3(body.Breadth * 0.78f, body.Hump, body.W(0.3f)), body.W(0.1f));

            // The front of the chest between the forelegs, and a belly that hangs a little between the legs.
            field.Ellipsoid(SaurianPart.Body, new Vector3(0f, body.Brisket + body.W(0.13f), body.ForeZ + body.W(0.08f)), new Vector3(body.Breadth * 0.7f, body.W(0.15f), body.W(0.2f)), body.W(0.08f));
            float middle = (body.HindZ + body.ForeZ) * 0.5f;
            field.Ellipsoid(SaurianPart.Body, new Vector3(0f, body.Brisket + body.W(0.15f), middle + body.W(0.05f)), new Vector3(body.Breadth * 0.9f, body.W(0.17f), (body.ForeZ - body.HindZ) * 0.4f), body.W(0.12f));
        }

        // A foreleg: a great shoulder blade and upper arm, mostly inside the chest, a thick forearm down to the wrist, which is just above the paw, and a broad forepaw.
        private static void Foreleg(SaurianField field, BearBody body, int side)
        {
            SaurianPart part = side > 0 ? SaurianPart.ForeL : SaurianPart.ForeR;
            Vector3 shoulder = Mirror(body.Shoulder, side), elbow = Mirror(body.Elbow, side), carpus = Mirror(body.Carpus, side), paw = Mirror(body.ForePaw, side);

            field.JoinAt(part, shoulder + new Vector3(0f, body.W(0.06f), 0f), body.W(0.1f), body.W(0.5f));
            Vector3 blade = new Vector3(0f, 0.86f, -0.5f).Normalized();
            field.Ellipsoid(part, shoulder + new Vector3(side * body.W(0.015f), body.W(0.08f), -body.W(0.04f)), new Vector3(body.W(0.1f), body.W(0.13f), body.W(0.22f)), body.W(0.05f), blade, Vector3.UnitZ);
            field.Cone(part, shoulder, elbow, body.W(0.12f), body.W(0.1f), body.W(0.05f));

            Vector3 forearm = carpus - elbow;
            field.Cone(part, elbow, carpus, body.W(0.095f), body.W(0.066f), body.W(0.04f));
            field.Ellipsoid(part, elbow + forearm * 0.3f, new Vector3(body.W(0.085f), body.W(0.09f), forearm.Length * 0.36f), body.W(0.04f), forearm, Vector3.UnitZ);
            field.Ellipsoid(part, carpus, new Vector3(body.W(0.068f), body.W(0.06f), body.W(0.07f)), body.W(0.02f));

            // The forepaw, broad and round, the palm under the wrist and the toes in front.
            Vector3 palm = new Vector3(paw.X, body.W(0.042f), paw.Z + body.W(0.04f));
            field.Cone(part, carpus, palm + new Vector3(0f, body.W(0.02f), 0f), body.W(0.066f), body.W(0.06f), body.W(0.02f));
            field.Ellipsoid(part, palm, new Vector3(body.W(0.075f), body.W(0.045f), body.W(0.095f)), body.W(0.015f));
            Toes(field, body, part, new Vector3(paw.X, body.W(0.035f), paw.Z + body.W(0.12f)), body.W(0.024f));
            Sole(field, body, part, paw);
        }

        // A hind leg: the ham, the knee forward at the bottom of the flank, a thick shank down to the ankle, which is just above the heel, and the long sole of a foot that's flat on the ground.
        private static void HindLeg(SaurianField field, BearBody body, int side)
        {
            SaurianPart part = side > 0 ? SaurianPart.LegL : SaurianPart.LegR;
            Vector3 hip = Mirror(body.Hip, side), knee = Mirror(body.Knee, side), ankle = Mirror(body.Ankle, side), paw = Mirror(body.HindPaw, side);

            field.JoinAt(part, hip + new Vector3(0f, body.W(0.05f), -body.W(0.02f)), body.W(0.12f), body.W(0.55f));
            field.Ellipsoid(part, hip + new Vector3(0f, -body.W(0.08f), -body.W(0.02f)), new Vector3(body.W(0.11f), body.W(0.22f), body.W(0.17f)), body.W(0.06f), new Vector3(0f, -0.15f, 1f));
            field.Ellipsoid(part, new Vector3(hip.X * 0.5f, hip.Y - body.W(0.04f), hip.Z - body.W(0.12f)), new Vector3(body.W(0.09f), body.W(0.17f), body.W(0.1f)), body.W(0.05f));
            field.Ellipsoid(part, knee, new Vector3(body.W(0.09f)), body.W(0.04f));
            field.Cone(part, knee + new Vector3(0f, body.W(0.02f), 0f), ankle, body.W(0.095f), body.W(0.06f), body.W(0.04f));
            field.Ellipsoid(part, ankle, new Vector3(body.W(0.06f), body.W(0.055f), body.W(0.06f)), body.W(0.02f));

            // The sole, from the heel under the ankle to the toes in front of the ball of the foot.
            Vector3 sole = new Vector3(paw.X, body.W(0.038f), paw.Z - body.W(0.06f));
            field.Cone(part, ankle, sole + new Vector3(0f, body.W(0.02f), 0f), body.W(0.06f), body.W(0.055f), body.W(0.02f));
            field.Ellipsoid(part, sole, new Vector3(body.W(0.068f), body.W(0.04f), body.W(0.15f)), body.W(0.015f));
            field.Ellipsoid(part, new Vector3(paw.X, body.W(0.045f), ankle.Z - body.W(0.03f)), new Vector3(body.W(0.05f)), body.W(0.02f));
            Toes(field, body, part, new Vector3(paw.X, body.W(0.032f), paw.Z + body.W(0.07f)), body.W(0.022f));
            Sole(field, body, part, paw);
        }

        // Five toes along the front of a paw, the middle three furthest forward.
        private static void Toes(SaurianField field, BearBody body, SaurianPart part, Vector3 middle, float size)
        {
            for (int toe = -2; toe <= 2; toe++)
            {
                Vector3 at = middle + new Vector3(toe * size * 1.05f, 0f, -MathF.Abs(toe) * size * 0.45f);
                field.Ellipsoid(part, at, new Vector3(size, size * 0.85f, size * 1.1f), size * 0.4f);
            }
        }

        // Whatever of a paw would go under the ground is cut away, so it stands on a flat sole.
        private static void Sole(SaurianField field, BearBody body, SaurianPart part, Vector3 paw)
        {
            field.Ellipsoid(part, new Vector3(paw.X, -body.W(0.3f), paw.Z), new Vector3(body.W(0.4f), body.W(0.3f), body.W(0.4f)), body.W(0.004f), carve: true);
        }

        // A domed skull over broad cheeks, a steep brow where it meets a short, thick muzzle, a big nose at its end and a heavy lower jaw.
        private static void Head(SaurianField field, BearBody body)
        {
            float l = body.HeadLength;
            Vector3 forward = body.HeadForward, up = body.HeadUp;
            Vector3 At(float x, float y, float z) => body.FromHead(new Vector3(x * l, y * l, z * l));

            field.JoinAt(SaurianPart.Head, body.Atlas, l * 0.15f, l * 0.9f);
            field.Ellipsoid(SaurianPart.Head, At(0f, 0.12f, 0.24f), new Vector3(0.3f, 0.27f, 0.28f) * l, l * 0.07f, forward, up);
            field.Ellipsoid(SaurianPart.Head, At(0f, -0.04f, 0.3f), new Vector3(0.34f, 0.2f, 0.24f) * l, l * 0.08f, forward, up);

            field.Cone(SaurianPart.Head, At(0f, -0.06f, 0.5f), At(0f, -0.09f, 0.94f), l * 0.17f, l * 0.11f, l * 0.06f, 1f, up);
            field.Cone(SaurianPart.Head, At(0f, 0.18f, 0.42f), At(0f, 0f, 0.9f), l * 0.12f, l * 0.07f, l * 0.05f, 1.3f, up);
            field.Ellipsoid(SaurianPart.Head, At(0f, -0.07f, 0.975f), new Vector3(0.1f, 0.08f, 0.055f) * l, l * 0.015f, forward, up);

            field.Cone(SaurianPart.Head, At(0f, -0.2f, 0.36f), At(0f, -0.17f, 0.86f), l * 0.13f, l * 0.08f, l * 0.05f, 1f, up);
            for (int side = -1; side <= 1; side += 2)
            {
                field.Ellipsoid(SaurianPart.Head, At(side * 0.15f, 0.13f, 0.46f), new Vector3(0.07f, 0.05f, 0.07f) * l, l * 0.04f, forward, up);
            }
        }
    }
}

using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Saurian
{
    public static partial class SaurianSculpt
    {
        // What walks on eight: a body like a low shield with its legs round its edge, a narrow waist, and an abdomen swollen into an egg behind it, with the spinnerets at its tip.
        private static void ArachnidBody(SaurianField field, SaurianBody body)
        {
            SaurianAxis axis = body.Axis;
            float middle = body.HipAt + (body.ShoulderAt - body.HipAt) * 0.5f;
            float trunk = body.ShoulderAt - body.HipAt;
            field.Ellipsoid(SaurianPart.Body, axis.At(middle) + new Vector3(0f, axis.DeepAt(middle) * 0.1f, 0f), new Vector3(axis.WideAt(middle) * 0.95f, axis.DeepAt(middle) * 0.78f, trunk * 0.56f), axis.DeepAt(middle) * 0.45f, axis.Way(middle));

            // The abdomen, fullest a little behind its middle.
            float full = body.HipAt * 0.45f;
            float deep = axis.DeepAt(full), wide = axis.WideAt(full);
            field.Ellipsoid(SaurianPart.Body, axis.At(full), new Vector3(wide * 0.98f, deep * 0.96f, body.HipAt * 0.44f), deep * 0.35f, axis.Way(full));

            // Two short spinnerets at its tip, pointing back and down.
            Vector3 tip = axis.At(body.TipAt * 0.5f);
            Vector3 back = -axis.Way(body.TipAt * 0.5f);
            float r = axis.DeepAt(body.TipAt * 0.5f) * 0.35f;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 root = tip + new Vector3(side * r * 0.8f, -r * 0.4f, 0f);
                field.Cone(SaurianPart.Body, root, root + (back + new Vector3(side * 0.25f, -0.55f, 0f)).Normalized() * r * 2.4f, r * 0.7f, r * 0.3f, r * 0.4f);
            }
        }

        // A leg in its pieces, each a little thinner than the last, with a knob at every joint: a thigh that stands up and out from the edge of the body to the knee, a shin down and out from
        // there, and the foot down to its claw. It joins the body softly only where it leaves it, so that eight legs side by side never grow together.
        private static void JointedLeg(SaurianField field, SaurianLeg leg, SaurianPart part)
        {
            float hip = leg.HipRadius, knee = leg.KneeRadius, ankle = leg.AnkleRadius, ball = leg.BallRadius;
            Vector3 thigh = leg.Knee - leg.Hip;
            field.JoinAt(part, leg.Hip, hip * 1.1f, hip * 3.2f);
            field.Cone(part, leg.Hip, leg.Knee, hip, knee * 1.08f, knee * 0.35f);
            field.Ellipsoid(part, leg.Hip + thigh * 0.38f, new Vector3(hip * 1.06f, hip * 1.06f, thigh.Length * 0.3f), hip * 0.35f, thigh);
            field.Ellipsoid(part, leg.Knee, new Vector3(knee * 1.3f), knee * 0.35f);
            field.Cone(part, leg.Knee, leg.Ankle, knee * 1.04f, ankle, ankle * 0.35f);
            field.Ellipsoid(part, leg.Ankle, new Vector3(ankle * 1.25f), ankle * 0.35f);
            field.Cone(part, leg.Ankle, leg.Ball, ankle * 0.95f, ball, ball * 0.35f);
        }
    }
}

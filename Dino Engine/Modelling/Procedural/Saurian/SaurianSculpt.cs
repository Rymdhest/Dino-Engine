using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Saurian
{
    /// <summary>
    /// Builds a body's flesh as a field: rounded cones down the line of the body, a belly that hangs below it, muscle on the thighs and calves where it bulges, and a head shaped from ellipsoids along the
    /// skull. What's built here is the shape, and the skin is grown from it.
    /// </summary>
    public static class SaurianSculpt
    {
        /// <summary>Everything but the jaw.</summary>
        public static SaurianField Flesh(SaurianBody body)
        {
            var field = new SaurianField { Head = body.Head };
            Line(field, body);
            Trunk(field, body);
            for (int i = 0; i < body.Legs.Length; i++) Leg(field, body.Legs[i], LegPart(body.Legs[i]));
            foreach (SaurianArm arm in body.Arms) Arm(field, arm);
            HeadVolume(field, body.Head);
            HeadDetail(field, body.Head);
            field.JoinAt(SaurianPart.Head, body.Head.Origin, body.Head.Length * 0.16f, body.Head.Length * 1.3f);
            return field;
        }

        /// <summary>The jaw on its own: the bottom of the same head, deepest at the back where what closes it pulls.</summary>
        public static SaurianField Jaw(SaurianBody body)
        {
            SaurianHead head = body.Head;
            var field = new SaurianField { Head = head, Jaw = true, Gap = head.Length * 0.004f };
            HeadVolume(field, head);
            float z = head.Length * 0.16f;
            field.Ellipsoid(SaurianPart.Head, new Vector3(0f, head.Lip(z) - head.DepthAt(z) * 0.5f, z), new Vector3(head.WidthAt(z) * 0.86f, head.DepthAt(z) * 0.5f, head.Length * 0.2f), head.Length * 0.06f, inHead: true);
            return field;
        }

        public static SaurianPart LegPart(SaurianLeg leg) => leg.Front ? (leg.Side > 0f ? SaurianPart.ForeL : SaurianPart.ForeR) : (leg.Side > 0f ? SaurianPart.LegL : SaurianPart.LegR);

        // Down the line of the body, as rounded cones squashed to be deeper than they're wide, as far as the back of the skull and a little into it.
        private static void Line(SaurianField field, SaurianBody body)
        {
            SaurianAxis axis = body.Axis;
            float end = body.HeadAt + body.Head.Length * 0.08f;
            int last = 0;
            while (last + 1 < axis.Points.Length && axis.Along[last + 1] <= end) last++;

            for (int i = 0; i < last; i += 2)
            {
                int j = Math.Min(i + 2, last);
                if (j == i) break;
                float deepA = axis.Deep[i], deepB = axis.Deep[j];
                float squash = (deepA + deepB) / MathF.Max(axis.Wide[i] + axis.Wide[j], 1e-4f);
                Vector3 way = (axis.Points[j] - axis.Points[i]).Normalized();
                Vector3 up = Vector3.UnitY - way * Vector3.Dot(Vector3.UnitY, way);
                field.Cone(SaurianPart.Body, axis.Points[i], axis.Points[j], deepA, deepB, MathF.Min(deepA, deepB) * 0.4f, squash, up);
            }
        }

        // What hangs below the line and stands out on it: a belly, a deep chest, a broad rump, a throat that's shown off.
        private static void Trunk(SaurianField field, SaurianBody body)
        {
            SaurianAxis axis = body.Axis;
            float hip = body.HipAt, chest = body.ChestAt;
            float trunk = chest - hip;

            // The belly hangs in front of the thighs, between the legs and the arms, where the belly ribs were, so the thigh and knee stand clear of it.
            float mid = hip + trunk * 0.6f;
            Vector3 way = axis.Way(mid);
            float deep = axis.DeepAt(mid), wide = axis.WideAt(mid);
            field.Ellipsoid(SaurianPart.Body, axis.At(mid) - new Vector3(0f, deep * 0.3f + body.BellyHang * 0.6f, 0f), new Vector3(wide * 0.66f, deep * 0.66f + body.BellyHang * 0.45f, trunk * 0.34f), deep * 0.55f, way);

            float front = hip + trunk * 0.85f;
            field.Ellipsoid(SaurianPart.Body, axis.At(front) - new Vector3(0f, axis.DeepAt(front) * 0.35f, 0f), new Vector3(axis.WideAt(front) * 0.72f, axis.DeepAt(front) * 0.6f, trunk * 0.22f), axis.DeepAt(front) * 0.5f, axis.Way(front));

            float rumpDeep = axis.DeepAt(hip);
            field.Ellipsoid(SaurianPart.Body, axis.At(hip) + new Vector3(0f, rumpDeep * 0.15f, 0f), new Vector3(axis.WideAt(hip) * 0.95f, rumpDeep * 0.82f, rumpDeep * 1.5f), rumpDeep * 0.5f, axis.Way(hip));

            if (body.Head.Throat > 0f)
            {
                float at = body.NeckTopAt + (body.HeadAt - body.NeckTopAt) * 0.55f;
                float t = body.Head.Throat;
                field.Ellipsoid(SaurianPart.Body, axis.At(at) - new Vector3(0f, axis.DeepAt(at) * 0.85f, 0f), new Vector3(t * 0.42f, t * 0.55f, t * 0.85f), axis.DeepAt(at) * 0.5f, axis.Way(at));
            }
        }

        // A leg: a thigh that's mostly muscle and joins the body softly, a shin with a calf behind it, a metatarsus that's little more than tendon, and the pad at the ball of the foot.
        private static void Leg(SaurianField field, SaurianLeg leg, SaurianPart part)
        {
            Vector3 femur = leg.Knee - leg.Hip;
            Vector3 tibia = leg.Ankle - leg.Knee;
            Vector3 meta = leg.Ball - leg.Ankle;
            float thigh = leg.HipRadius;
            float knee = leg.KneeRadius;

            // The thigh is the biggest muscle there is on one of these, and stands out of the body as a drumstick does.
            field.JoinAt(part, leg.Hip, thigh * 0.95f, thigh * 3.2f);
            field.Cone(part, leg.Hip + new Vector3(0f, thigh * 0.1f, 0f), leg.Knee, thigh * 0.95f, knee * 1.05f, knee * 0.4f);
            field.Ellipsoid(part, leg.Hip + femur * 0.46f, new Vector3(thigh * 0.98f, thigh * 1.12f, femur.Length * 0.58f), thigh * 0.45f, femur);
            field.Ellipsoid(part, leg.Knee, new Vector3(knee * 0.95f, knee * 0.9f, knee * 1.05f), knee * 0.35f);

            Vector3 behind = Vector3.Cross(Vector3.UnitX, tibia).Normalized();
            if (Vector3.Dot(behind, Vector3.UnitZ) > 0f) behind = -behind;
            field.Cone(part, leg.Knee, leg.Ankle, knee, leg.AnkleRadius, knee * 0.35f);
            field.Ellipsoid(part, leg.Knee + tibia * 0.3f + behind * knee * 0.35f, new Vector3(knee * 0.82f, knee * 0.9f, tibia.Length * 0.32f), knee * 0.4f, tibia);

            field.Ellipsoid(part, leg.Ankle, new Vector3(leg.AnkleRadius * 1.25f), leg.AnkleRadius * 0.4f);
            field.Cone(part, leg.Ankle, leg.Ball, leg.AnkleRadius * 1.05f, leg.BallRadius, leg.AnkleRadius * 0.35f, 1.2f, Vector3.Cross(Vector3.UnitX, meta));
            field.Ellipsoid(part, leg.Ball + new Vector3(0f, -leg.BallRadius * 0.15f, leg.BallRadius * 0.15f), new Vector3(leg.BallRadius * 1.5f, leg.BallRadius * 0.85f, leg.BallRadius * 1.35f), leg.BallRadius * 0.4f);
        }

        // An arm carried folded, with a narrow hand whose palm faces the other.
        private static void Arm(SaurianField field, SaurianArm arm)
        {
            SaurianPart part = arm.Side > 0f ? SaurianPart.ArmL : SaurianPart.ArmR;
            float r = arm.Radius;
            field.JoinAt(part, arm.Shoulder, r * 1.3f, r * 4.5f);
            field.Cone(part, arm.Shoulder, arm.Elbow, r, r * 0.72f, r * 0.35f);
            field.Cone(part, arm.Elbow, arm.Wrist, r * 0.72f, r * 0.5f, r * 0.3f);
            Vector3 hand = arm.Hand - arm.Wrist;
            field.Ellipsoid(part, arm.Wrist + hand * 0.32f, new Vector3(r * 0.32f, r * 0.62f, hand.Length * 0.42f), r * 0.25f, hand);
        }

        /// <summary>A head is a few ellipsoids along the skull, deep and wide at the back and narrowing to the snout, sitting more above the line of the mouth than below it.</summary>
        public static void HeadVolume(SaurianField field, SaurianHead head)
        {
            float length = head.Length;
            (float at, float reach)[] stations = { (0.06f, 0.13f), (0.21f, 0.26f), (0.46f, 0.25f), (0.72f, 0.2f), (0.92f, 0.1f) };
            foreach ((float at, float reach) in stations)
            {
                float z = at * length;
                float depth = head.DepthAt(z);
                field.Ellipsoid(SaurianPart.Head, new Vector3(0f, head.Lip(z) + depth * 0.24f, z), new Vector3(head.WidthAt(z), depth, reach * length), length * 0.12f, inHead: true);
            }

            // A Roman nose stands up along the snout, and a dished one goes in.
            float snout = length * 0.62f;
            float top = head.Lip(snout) + head.DepthAt(snout) * 1.24f;
            if (head.Profile > 0f) field.Ellipsoid(SaurianPart.Head, new Vector3(0f, top - head.DepthAt(snout) * 0.35f, snout), new Vector3(head.WidthAt(snout) * 0.45f, head.DepthAt(snout) * 0.42f + head.Profile * length, length * 0.26f), length * 0.08f, inHead: true);
            else if (head.Profile < 0f) field.Ellipsoid(SaurianPart.Head, new Vector3(0f, top + -head.Profile * length * 0.6f, length * 0.68f), new Vector3(head.WidthAt(length * 0.68f) * 0.7f, -head.Profile * length * 1.6f, length * 0.16f), length * 0.05f, inHead: true, carve: true);
        }

        // Bosses over the eyes, muscle bulging behind them, the eyes set in, and nostrils near the tip.
        private static void HeadDetail(SaurianField field, SaurianHead head)
        {
            float length = head.Length;
            float eyeZ = head.EyeAt * length;
            float eyeY = EyeHeight(head);
            float width = head.WidthAt(eyeZ);

            for (int side = -1; side <= 1; side += 2)
            {
                if (head.Brow > 0f)
                {
                    float top = head.Lip(eyeZ) + head.DepthAt(eyeZ) * 1.24f;
                    field.Ellipsoid(SaurianPart.Head, new Vector3(side * width * 0.74f, top - head.Brow * 0.4f, eyeZ - head.EyeSize * 0.2f), new Vector3(head.Brow * 1.1f, head.Brow * 0.75f, head.Brow * 1.7f), head.Brow * 0.9f, inHead: true);
                }
                if (head.Jowls > 0f)
                {
                    float z = length * 0.12f;
                    field.Ellipsoid(SaurianPart.Head, new Vector3(side * head.WidthAt(z) * 0.82f, head.Lip(z) + head.DepthAt(z) * 0.45f, z), new Vector3(head.Jowls, head.Jowls * 1.6f, head.Jowls * 1.9f), head.Jowls, inHead: true);
                }

                field.Ellipsoid(SaurianPart.Head, new Vector3(side * width * 0.98f, eyeY, eyeZ), new Vector3(head.EyeSize * 0.75f, head.EyeSize * 0.85f, head.EyeSize * 0.95f), head.EyeSize * 0.35f, inHead: true, carve: true);

                float noseZ = length * 0.88f;
                field.Ellipsoid(SaurianPart.Head, new Vector3(side * head.WidthAt(noseZ) * 0.72f, head.Lip(noseZ) + head.DepthAt(noseZ) * 1.05f, noseZ), new Vector3(length * 0.022f, length * 0.016f, length * 0.035f), length * 0.012f, inHead: true, carve: true);
            }
        }

        /// <summary>How high on the side of the head the eyes are, in its own frame.</summary>
        public static float EyeHeight(SaurianHead head)
        {
            float eyeZ = head.EyeAt * head.Length;
            return head.Lip(eyeZ) + head.DepthAt(eyeZ) * 1.24f * 0.64f;
        }
    }
}

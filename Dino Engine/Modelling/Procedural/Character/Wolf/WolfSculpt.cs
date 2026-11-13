using Dino_Engine.Modelling.Procedural.Saurian;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Character
{
    /// <summary>
    /// A wolf's flesh as a field of signed distances (<see cref="SaurianField"/>, the dinosaurs' own): rounded cones down a trunk that's deep at the chest and drawn up sharply at the waist, the
    /// keel of the chest between the forelegs, a long neck thickened by its ruff, lean legs that are muscle at the top and tendon below, each blended into the body only near where it joins, standing
    /// on compact paws, and a head with a broad skull, wide cheeks, a long narrow muzzle and a nose pad at its end. What's built here is the shape, and the skin is grown from it.
    /// </summary>
    public static class WolfSculpt
    {
        public static SaurianField Flesh(WolfBody body)
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

        private static Vector3 Mirror(Vector3 left, int side) => side > 0 ? left : WolfBody.Right(left);

        private static void Trunk(SaurianField field, WolfBody body)
        {
            BoarStation[] trunk = body.Trunk;
            for (int i = 0; i + 1 < trunk.Length; i++)
            {
                BoarStation a = trunk[i], b = trunk[i + 1];
                float squash = (a.Deep + b.Deep) / MathF.Max(a.Half + b.Half, 1e-4f);
                field.Cone(SaurianPart.Body, new Vector3(0f, a.Middle, a.Z), new Vector3(0f, b.Middle, b.Z), a.Deep, b.Deep, MathF.Min(a.Deep, b.Deep) * 0.35f, squash);
            }

            // The neck rises up and forward to the head, thick with the ruff a wolf carries round it: its top runs on from the withers to behind the ears, and its throat from the chest up under
            // the jaw.
            BoarStation last = trunk[^1];
            float l = body.HeadLength;
            float ruff = 1f + 0.15f * body.Build + (body.Dire ? 0.12f : 0f);
            Vector3 throat = body.FromHead(new Vector3(0f, -l * 0.1f, l * 0.2f));
            field.Cone(SaurianPart.Body, new Vector3(0f, last.Middle, last.Z), throat, last.Deep * 0.95f, l * 0.22f * ruff, l * 0.1f, 1.25f);
            Vector3 poll = body.FromHead(new Vector3(0f, l * 0.1f, l * 0.08f));
            field.Cone(SaurianPart.Body, new Vector3(0f, body.W(0.88f), body.ForeZ), poll, body.W(0.12f) * ruff, l * 0.2f * ruff, body.W(0.08f), 1.35f);

            // The ruff itself, deepest at the throat and over the shoulders, where a wolf's coat is longest.
            Vector3 neckMiddle = Vector3.Lerp(new Vector3(0f, last.Middle, last.Z), throat, 0.45f);
            field.Ellipsoid(SaurianPart.Body, neckMiddle + new Vector3(0f, body.W(0.01f), 0f), new Vector3(body.Breadth * 0.95f * ruff, body.W(0.15f) * ruff, body.W(0.17f)), body.W(0.08f), throat - new Vector3(0f, last.Middle, last.Z));

            // Withers, the keel of the chest between the forelegs, and the ribs, which end well before the hips.
            field.Ellipsoid(SaurianPart.Body, new Vector3(0f, body.W(0.86f), body.ForeZ - body.W(0.04f)), new Vector3(body.Breadth * 0.8f, body.W(0.13f), body.W(0.26f)), body.W(0.08f));
            field.Ellipsoid(SaurianPart.Body, new Vector3(0f, body.Brisket + body.W(0.12f), body.ForeZ + body.W(0.06f)), new Vector3(body.Breadth * 0.55f, body.W(0.12f), body.W(0.15f)), body.W(0.07f));
            field.Ellipsoid(SaurianPart.Body, new Vector3(0f, body.Brisket + body.W(0.15f), body.ForeZ - (body.ForeZ - body.HindZ) * 0.2f), new Vector3(body.Breadth * 0.86f, body.W(0.15f), (body.ForeZ - body.HindZ) * 0.3f), body.W(0.1f));
        }

        // A foreleg: the shoulder blade and the muscle over the upper arm, mostly inside the chest, a lean forearm, the knee (the wrist), a short pastern and a paw.
        private static void Foreleg(SaurianField field, WolfBody body, int side)
        {
            SaurianPart part = side > 0 ? SaurianPart.ForeL : SaurianPart.ForeR;
            Vector3 shoulder = Mirror(body.Shoulder, side), elbow = Mirror(body.Elbow, side), carpus = Mirror(body.Carpus, side), paw = Mirror(body.ForePaw, side);

            field.JoinAt(part, shoulder + new Vector3(0f, body.W(0.05f), 0f), body.W(0.08f), body.W(0.45f));
            Vector3 blade = new Vector3(0f, 0.86f, -0.5f).Normalized();
            field.Ellipsoid(part, shoulder + new Vector3(side * body.W(0.01f), body.W(0.07f), -body.W(0.035f)), new Vector3(body.W(0.065f), body.W(0.1f), body.W(0.19f)), body.W(0.045f), blade, Vector3.UnitZ);
            field.Cone(part, shoulder, elbow, body.W(0.08f), body.W(0.062f), body.W(0.035f));

            Vector3 forearm = carpus - elbow;
            field.Cone(part, elbow, carpus, body.W(0.058f), body.W(0.03f), body.W(0.025f));
            field.Ellipsoid(part, elbow + forearm * 0.25f, new Vector3(body.W(0.05f), body.W(0.055f), forearm.Length * 0.32f), body.W(0.03f), forearm, Vector3.UnitZ);
            field.Ellipsoid(part, carpus, new Vector3(body.W(0.034f), body.W(0.038f), body.W(0.036f)), body.W(0.015f));

            Lower(field, body, part, carpus, paw);
        }

        // A hind leg: the thigh, lean and long, the buttock behind it, the gaskin down from the stifle to the hock with its point behind, a long metatarsus, and a paw.
        private static void HindLeg(SaurianField field, WolfBody body, int side)
        {
            SaurianPart part = side > 0 ? SaurianPart.LegL : SaurianPart.LegR;
            Vector3 hip = Mirror(body.Hip, side), stifle = Mirror(body.Stifle, side), hock = Mirror(body.Hock, side), paw = Mirror(body.HindPaw, side);

            field.JoinAt(part, hip + new Vector3(0f, body.W(0.04f), -body.W(0.02f)), body.W(0.1f), body.W(0.5f));
            field.Ellipsoid(part, hip + new Vector3(0f, -body.W(0.1f), body.W(0.0f)), new Vector3(body.W(0.062f), body.W(0.19f), body.W(0.13f)), body.W(0.05f), new Vector3(0f, -0.1f, 1f));
            field.Ellipsoid(part, new Vector3(hip.X * 0.5f, hip.Y - body.W(0.06f), hip.Z - body.W(0.1f)), new Vector3(body.W(0.06f), body.W(0.14f), body.W(0.08f)), body.W(0.045f));

            field.Cone(part, stifle + new Vector3(0f, body.W(0.02f), 0f), hock, body.W(0.065f), body.W(0.031f), body.W(0.028f));
            field.Ellipsoid(part, hock, new Vector3(body.W(0.033f), body.W(0.04f), body.W(0.04f)), body.W(0.015f));
            field.Ellipsoid(part, hock + new Vector3(0f, body.W(0.025f), -body.W(0.028f)), new Vector3(body.W(0.02f), body.W(0.028f), body.W(0.022f)), body.W(0.014f));

            Lower(field, body, part, hock, paw);
        }

        // Below the knee or the hock: a lean pastern down to a compact paw, its toes gathered in front and its pads under it, standing on a flat sole.
        private static void Lower(SaurianField field, WolfBody body, SaurianPart part, Vector3 joint, Vector3 paw)
        {
            field.Cone(part, joint, paw + new Vector3(0f, body.W(0.005f), 0f), body.W(0.028f), body.W(0.027f), body.W(0.012f));
            Vector3 middle = new Vector3(paw.X, body.W(0.032f), paw.Z + body.W(0.03f));
            field.Ellipsoid(part, middle, new Vector3(body.W(0.038f), body.W(0.032f), body.W(0.055f)), body.W(0.01f));
            field.Ellipsoid(part, new Vector3(paw.X, body.W(0.03f), paw.Z + body.W(0.07f)), new Vector3(body.W(0.034f), body.W(0.026f), body.W(0.028f)), body.W(0.012f));
            field.Ellipsoid(part, new Vector3(paw.X, -body.W(0.2f), paw.Z), new Vector3(body.W(0.25f), body.W(0.2f), body.W(0.25f)), body.W(0.004f), carve: true);
        }

        // A broad skull and wide cheeks behind a long, narrow muzzle that tapers to the nose, a moderate stop where the brow meets the bridge of the nose, and a lower jaw under it.
        private static void Head(SaurianField field, WolfBody body)
        {
            float l = body.HeadLength;
            Vector3 forward = body.HeadForward, up = body.HeadUp;
            Vector3 At(float x, float y, float z) => body.FromHead(new Vector3(x * l, y * l, z * l));

            field.JoinAt(SaurianPart.Head, body.Atlas, l * 0.14f, l * 0.9f);
            field.Ellipsoid(SaurianPart.Head, At(0f, 0.1f, 0.22f), new Vector3(0.25f, 0.2f, 0.26f) * l, l * 0.06f, forward, up);
            field.Ellipsoid(SaurianPart.Head, At(0f, -0.02f, 0.3f), new Vector3(0.27f, 0.17f, 0.2f) * l, l * 0.07f, forward, up);

            // The muzzle and the bridge of the nose down its top, which meets the brow at a gentle stop.
            field.Cone(SaurianPart.Head, At(0f, -0.04f, 0.42f), At(0f, -0.06f, 0.94f), l * 0.135f, l * 0.06f, l * 0.05f, 1.1f, up);
            field.Cone(SaurianPart.Head, At(0f, 0.17f, 0.3f), At(0f, 0.0f, 0.9f), l * 0.1f, l * 0.042f, l * 0.05f, 1.5f, up);
            field.Ellipsoid(SaurianPart.Head, At(0f, -0.03f, 0.965f), new Vector3(0.055f, 0.045f, 0.04f) * l, l * 0.012f, forward, up);

            field.Cone(SaurianPart.Head, At(0f, -0.17f, 0.32f), At(0f, -0.12f, 0.88f), l * 0.095f, l * 0.042f, l * 0.045f, 1f, up);
            for (int side = -1; side <= 1; side += 2)
            {
                field.Ellipsoid(SaurianPart.Head, At(side * 0.12f, 0.15f, 0.41f), new Vector3(0.06f, 0.04f, 0.07f) * l, l * 0.035f, forward, up);
            }
        }
    }
}

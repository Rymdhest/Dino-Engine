using Dino_Engine.Modelling.Procedural.Saurian;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Character
{
    /// <summary>
    /// A boar's flesh as a field of signed distances (<see cref="SaurianField"/>, the dinosaurs' own): rounded cones down the trunk, deeper than they're wide, the withers and the chest built up
    /// over them, a crest of mane along the top, legs that are muscle at the top and bone and tendon below, each blended into the body only near where it joins, and a long wedge of a head with
    /// heavy jowls, a straight face and a flat disc at the end of the snout. What's built here is the shape, and the skin is grown from it.
    /// </summary>
    public static class BoarSculpt
    {
        public static SaurianField Flesh(BoarBody body)
        {
            var field = new SaurianField();
            Trunk(field, body);
            Mane(field, body);
            for (int side = -1; side <= 1; side += 2)
            {
                Foreleg(field, body, side);
                HindLeg(field, body, side);
            }
            Head(field, body);
            return field;
        }

        private static Vector3 Mirror(Vector3 left, int side) => side > 0 ? left : BoarBody.Right(left);

        // Down the trunk from the rump to the throat, then the neck to the head, and what stands out on it: the withers, the prow of the chest between the forelegs, and a belly.
        private static void Trunk(SaurianField field, BoarBody body)
        {
            BoarStation[] trunk = body.Trunk;
            for (int i = 0; i + 1 < trunk.Length; i++)
            {
                BoarStation a = trunk[i], b = trunk[i + 1];
                float squash = (a.Deep + b.Deep) / MathF.Max(a.Half + b.Half, 1e-4f);
                field.Cone(SaurianPart.Body, new Vector3(0f, a.Middle, a.Z), new Vector3(0f, b.Middle, b.Z), a.Deep, b.Deep, MathF.Min(a.Deep, b.Deep) * 0.35f, squash);
            }

            // The neck is short and thick, more of the shoulders than of the head: its crest runs on from the withers down to behind the ears, and its throat from the chest up under the jaw.
            BoarStation last = trunk[^1];
            float l = body.HeadLength;
            Vector3 throat = body.FromHead(new Vector3(0f, -l * 0.08f, l * 0.16f));
            field.Cone(SaurianPart.Body, new Vector3(0f, last.Middle, last.Z), throat, last.Deep * 0.95f, l * 0.2f, l * 0.1f, 1.2f);
            Vector3 poll = body.FromHead(new Vector3(0f, l * 0.12f, l * 0.08f));
            field.Cone(SaurianPart.Body, new Vector3(0f, body.W(0.86f), body.ForeZ - body.W(0.02f)), poll, body.W(0.13f), l * 0.17f, body.W(0.08f), 1.3f);

            // A wild boar's withers stand well above its hips, built up of the muscle that holds its head, which the old males thicken into a shield.
            field.Ellipsoid(SaurianPart.Body, new Vector3(0f, body.W(0.84f), body.ForeZ - body.W(0.03f)), new Vector3(body.Breadth * (0.8f + 0.1f * body.Age), body.W(0.15f), body.W(0.3f)), body.W(0.1f));

            // The prow of the chest between the forelegs, and a belly between the legs, drawn up into the flank in front of the hind legs.
            field.Ellipsoid(SaurianPart.Body, new Vector3(0f, body.Brisket + body.W(0.12f), body.ForeZ + body.W(0.08f)), new Vector3(body.Breadth * 0.66f, body.W(0.14f), body.W(0.18f)), body.W(0.08f));
            float middle = (body.HindZ + body.ForeZ) * 0.5f;
            field.Ellipsoid(SaurianPart.Body, new Vector3(0f, body.Brisket + body.W(0.16f), middle + body.W(0.1f)), new Vector3(body.Breadth * 0.8f, body.W(0.15f), (body.ForeZ - body.HindZ) * 0.36f), body.W(0.12f));
        }

        // A crest of longer bristles along the top of the neck and the back, from behind the ears to the middle of the back, highest over the withers: what a boar raises when it's angry. It's
        // laid on the top as it really is, found by looking up from inside, as the withers and the neck stand well above the line the trunk was built along.
        private static void Mane(SaurianField field, BoarBody body)
        {
            if (body.Mane <= 0f) return;
            const int Points = 7;
            float from = body.Atlas.Z - body.W(0.02f);
            float to = body.HindZ + (body.ForeZ - body.HindZ) * 0.38f;
            var crest = new Vector3[Points];
            var radii = new float[Points];
            for (int i = 0; i < Points; i++)
            {
                float t = i / (Points - 1f);
                float z = MathHelper.Lerp(from, to, t);
                float inside = z > body.Trunk[^1].Z ? body.Atlas.Y : body.Station(z).Middle;
                float top = inside + field.Exit(new Vector3(0f, inside, z), Vector3.UnitY, body.W(0.2f));

                // Highest over the withers, and down to nothing at either end.
                float rise = MathF.Sin(MathF.PI * MathF.Pow(t, 0.75f)) * (0.55f + 0.45f * MathF.Exp(-MathF.Pow((t - 0.4f) / 0.25f, 2f)));
                float r = body.Mane / 0.7f * MathF.Max(rise, 0.15f);
                radii[i] = r;
                crest[i] = new Vector3(0f, top - r * 0.3f, z);
            }
            for (int i = 0; i + 1 < Points; i++) field.Cone(SaurianPart.Body, crest[i], crest[i + 1], radii[i], radii[i + 1], body.W(0.035f), 2.6f);
        }

        // A foreleg: the shoulder blade and the muscle over the upper arm, mostly inside the chest, the forearm, the knee (the wrist), a short cannon, the fetlock and the pastern down to a small
        // hoof with a flat sole.
        private static void Foreleg(SaurianField field, BoarBody body, int side)
        {
            SaurianPart part = side > 0 ? SaurianPart.ForeL : SaurianPart.ForeR;
            Vector3 shoulder = Mirror(body.Shoulder, side), elbow = Mirror(body.Elbow, side), carpus = Mirror(body.Carpus, side);
            Vector3 fetlock = Mirror(body.ForeFetlock, side), coronet = Mirror(body.ForeCoronet, side);

            field.JoinAt(part, shoulder + new Vector3(0f, body.W(0.05f), 0f), body.W(0.09f), body.W(0.5f));
            Vector3 blade = new Vector3(0f, 0.86f, -0.5f).Normalized();
            field.Ellipsoid(part, shoulder + new Vector3(side * body.W(0.012f), body.W(0.07f), -body.W(0.035f)), new Vector3(body.W(0.085f), body.W(0.11f), body.W(0.2f)), body.W(0.05f), blade, Vector3.UnitZ);
            field.Cone(part, shoulder, elbow, body.W(0.09f), body.W(0.072f), body.W(0.04f));

            // The forearm is all the muscle there is below the elbow, and goes down to the knee as a tendon.
            Vector3 forearm = carpus - elbow;
            field.Cone(part, elbow, carpus, body.W(0.075f), body.W(0.038f), body.W(0.03f));
            field.Ellipsoid(part, elbow + forearm * 0.28f, new Vector3(body.W(0.066f), body.W(0.068f), forearm.Length * 0.35f), body.W(0.035f), forearm, Vector3.UnitZ);
            field.Ellipsoid(part, carpus, new Vector3(body.W(0.041f), body.W(0.047f), body.W(0.045f)), body.W(0.018f));

            Lower(field, body, part, carpus, fetlock, coronet);
        }

        // A hind leg: the ham, which is most of the back of the hindquarters, and the buttock behind it, the gaskin down from the stifle to the hock with its point behind, a cannon, the fetlock
        // and the pastern down to the hoof.
        private static void HindLeg(SaurianField field, BoarBody body, int side)
        {
            SaurianPart part = side > 0 ? SaurianPart.LegL : SaurianPart.LegR;
            Vector3 hip = Mirror(body.Hip, side), stifle = Mirror(body.Stifle, side), hock = Mirror(body.Hock, side);
            Vector3 fetlock = Mirror(body.HindFetlock, side), coronet = Mirror(body.HindCoronet, side);
            float outward = side;

            field.JoinAt(part, hip + new Vector3(0f, body.W(0.04f), -body.W(0.02f)), body.W(0.11f), body.W(0.55f));
            field.Ellipsoid(part, hip + new Vector3(-outward * body.W(0.012f), -body.W(0.08f), -body.W(0.03f)), new Vector3(body.W(0.066f), body.W(0.18f), body.W(0.145f)), body.W(0.06f), new Vector3(0f, -0.2f, 1f));
            field.Ellipsoid(part, new Vector3(hip.X * 0.4f, hip.Y - body.W(0.04f), hip.Z - body.W(0.13f)), new Vector3(body.W(0.066f), body.W(0.15f), body.W(0.09f)), body.W(0.05f));

            field.Cone(part, stifle + new Vector3(0f, body.W(0.03f), 0f), hock, body.W(0.078f), body.W(0.037f), body.W(0.03f));
            field.Ellipsoid(part, hock, new Vector3(body.W(0.04f), body.W(0.046f), body.W(0.046f)), body.W(0.018f));
            field.Ellipsoid(part, hock + new Vector3(0f, body.W(0.028f), -body.W(0.032f)), new Vector3(body.W(0.025f), body.W(0.032f), body.W(0.026f)), body.W(0.016f));

            Lower(field, body, part, hock, fetlock, coronet);
        }

        // Below the knee or the hock, the same on all four: a cannon that's bone and tendon, the knuckle of the fetlock, a short pastern sloping forward, and a small hoof, its wall sloping to
        // a point in front and a heel behind, standing on a flat sole.
        private static void Lower(SaurianField field, BoarBody body, SaurianPart part, Vector3 joint, Vector3 fetlock, Vector3 coronet)
        {
            field.Cone(part, joint, fetlock, body.W(0.034f), body.W(0.03f), body.W(0.015f));
            field.Ellipsoid(part, fetlock, new Vector3(body.W(0.035f), body.W(0.035f), body.W(0.037f)), body.W(0.014f));
            field.Cone(part, fetlock, coronet, body.W(0.03f), body.W(0.031f), body.W(0.012f));
            Vector3 toe = new Vector3(coronet.X, body.W(0.012f), coronet.Z + body.W(0.05f));
            field.Cone(part, coronet + new Vector3(0f, -body.W(0.006f), body.W(0.004f)), toe, body.W(0.032f), body.W(0.012f), body.W(0.006f), 1.15f, new Vector3(0f, 1f, -0.6f));
            field.Ellipsoid(part, new Vector3(coronet.X, body.W(0.024f), coronet.Z - body.W(0.008f)), new Vector3(body.W(0.029f), body.W(0.024f), body.W(0.026f)), body.W(0.008f));
            field.Ellipsoid(part, new Vector3(coronet.X, -body.W(0.2f), coronet.Z), new Vector3(body.W(0.25f), body.W(0.2f), body.W(0.25f)), body.W(0.004f), carve: true);
        }

        // A long wedge: the skull and the brow at the back, heavy jowls below it where the jaw muscles are, a straight face down the top of a snout that tapers to a flat disc, a lower jaw under
        // it, and the lip swelling where the tusks come out.
        private static void Head(SaurianField field, BoarBody body)
        {
            float l = body.HeadLength;
            Vector3 forward = body.HeadForward, up = body.HeadUp;
            Vector3 At(float x, float y, float z) => body.FromHead(new Vector3(x * l, y * l, z * l));

            field.JoinAt(SaurianPart.Head, body.Atlas, l * 0.14f, l * 0.9f);
            field.Ellipsoid(SaurianPart.Head, At(0f, 0.08f, 0.2f), new Vector3(0.215f, 0.18f, 0.24f) * l, l * 0.06f, forward, up);
            field.Ellipsoid(SaurianPart.Head, At(0f, -0.1f, 0.25f), new Vector3(0.255f, 0.17f, 0.23f) * l, l * 0.07f, forward, up);

            // The snout, and the bridge of the nose down its top, which makes the face one straight line from the brow to the disc.
            field.Cone(SaurianPart.Head, At(0f, -0.03f, 0.34f), At(0f, -0.02f, 0.93f), l * 0.14f, l * 0.072f, l * 0.06f, 1f, up);
            field.Cone(SaurianPart.Head, At(0f, 0.12f, 0.25f), At(0f, 0.035f, 0.9f), l * 0.11f, l * 0.045f, l * 0.05f, 1.6f, up);
            field.Ellipsoid(SaurianPart.Head, At(0f, -0.02f, 0.955f), new Vector3(0.082f, 0.074f, 0.04f) * l, l * 0.014f, forward, up);

            field.Cone(SaurianPart.Head, At(0f, -0.2f, 0.3f), At(0f, -0.1f, 0.8f), l * 0.1f, l * 0.05f, l * 0.05f, 1f, up);
            for (int side = -1; side <= 1; side += 2)
            {
                field.Ellipsoid(SaurianPart.Head, At(side * 0.115f, 0.12f, 0.38f), new Vector3(0.065f, 0.045f, 0.075f) * l, l * 0.04f, forward, up);
                field.Ellipsoid(SaurianPart.Head, At(side * 0.08f, -0.08f, 0.68f), new Vector3(0.055f, 0.055f, 0.09f) * l, l * 0.04f, forward, up);
            }
        }
    }
}

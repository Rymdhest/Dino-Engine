using Dino_Engine.Modelling.Model;
using Dino_Engine.Wildlife;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Wildlife
{
    /// <summary>What a bird looks like, in metres at the game's scale. Everything from a finch to a heron, and a bat, is these numbers.</summary>
    public sealed class BirdLook
    {
        public float Body = 0.34f;          // Chest to rump.
        public float Girth = 0.11f;
        public float Neck = 0.07f;
        public float Head = 0.075f;
        public float Bill = 0.055f;
        public float BillDepth = 0.03f;
        public float Tail = 0.2f;
        public float TailWidth = 0.09f;
        public float Fork;                  // How far the middle of the tail falls short of its corners, as a share of its length.
        public float Arm = 0.2f;            // Shoulder to wrist.
        public float Hand = 0.26f;          // Wrist to tip.
        public float Chord = 0.2f;          // How deep the wing is.
        public float Point = 0.4f;          // 0 a rounded wing, 1 a blade.
        public int Fingers;                 // Separate feathers at the tip, for what soars.
        public float Legs = 0.1f;
        public bool Ears;                   // A bat's.

        public Vector3 Back = new Vector3(0.27f, 0.17f, 0.09f);
        public Vector3 Belly = new Vector3(0.5f, 0.44f, 0.34f);
        public Vector3 Crown = new Vector3(0.24f, 0.13f, 0.07f);
        public Vector3 Wing = new Vector3(0.25f, 0.16f, 0.085f);
        public Vector3 Tips = new Vector3(0.09f, 0.07f, 0.05f);
        public Vector3 TailColour = new Vector3(0.16f, 0.11f, 0.07f);
        public Vector3 Beak = new Vector3(0.06f, 0.05f, 0.04f);
        public Vector3 Foot = new Vector3(0.14f, 0.09f, 0.07f);
        public Vector3? Bars;               // Two pale lines across the wing.
    }

    /// <summary>
    /// A bird, on ten bones: body, neck, head, tail, and an arm and a hand for each wing, so a wing can bend at the wrist, which is most of what makes a wingbeat look like one. A wing is one sheet of a few dozen
    /// triangles with its colour in the corners. From up here a bird in the air is its wings and its shadow, and the rest is hardly there.
    /// </summary>
    public static class BirdBody
    {
        public const int Trunk = 0, Neck = 1, Head = 2, Tail = 3, ArmL = 4, HandL = 5, ArmR = 6, HandR = 7, LegL = 8, LegR = 9, Count = 10;

        /// <summary>How far the middle of a body stands off the ground.</summary>
        public static float Height(BirdLook look) => look.Legs + look.Girth * 1.05f;

        private static Vector3 Shoulder(BirdLook look, float side) => new Vector3(look.Girth * 0.72f * side, Height(look) + look.Girth * 0.38f, look.Body * 0.16f);
        private static Vector3 Wrist(BirdLook look, float side) => Shoulder(look, side) + new Vector3(look.Arm * side, 0f, 0f);
        private static Vector3 NeckRoot(BirdLook look) => new Vector3(0f, Height(look) + look.Girth * 0.55f, look.Body * 0.46f);
        private static Vector3 HeadRoot(BirdLook look) => NeckRoot(look) + new Vector3(0f, look.Neck * 0.8f, look.Neck * 0.6f);
        private static Vector3 Hip(BirdLook look, float side) => new Vector3(look.Girth * 0.32f * side, Height(look) - look.Girth * 0.55f, -look.Body * 0.06f);

        public static Skeleton NewSkeleton(BirdLook look) => new Skeleton(new[]
        {
            new Vector3(0f, Height(look), 0f),
            NeckRoot(look),
            HeadRoot(look),
            new Vector3(0f, Height(look) + look.Girth * 0.1f, -look.Body * 0.46f),
            Shoulder(look, 1f),
            Wrist(look, 1f),
            Shoulder(look, -1f),
            Wrist(look, -1f),
            Hip(look, 1f),
            Hip(look, -1f),
        }, new[] { -1, Trunk, Neck, Trunk, Trunk, ArmL, Trunk, ArmR, Trunk, Trunk });

        public static Mesh Build(BirdLook look)
        {
            var body = new BodyMesh();
            float g = look.Girth;
            float l = look.Body;
            float y = Height(look);

            Vector3 Plumage(float along, float up) => Vector3.Lerp(look.Belly, look.Back, Smooth(-0.45f, 0.25f, up));
            Span<Ring> trunk = stackalloc Ring[]
            {
                new Ring(new Vector3(0f, y + g * 0.1f, -l * 0.56f), g * 0.22f, g * 0.14f, Trunk),
                new Ring(new Vector3(0f, y + g * 0.02f, -l * 0.38f), g * 0.6f, g * 0.52f, Trunk),
                new Ring(new Vector3(0f, y, -l * 0.12f), g * 0.94f, g * 0.9f, Trunk),
                new Ring(new Vector3(0f, y, l * 0.14f), g, g, Trunk),
                new Ring(new Vector3(0f, y + g * 0.14f, l * 0.34f), g * 0.8f, g * 0.84f, Trunk, Neck, 0.25f),
                new Ring(NeckRoot(look), g * 0.52f, g * 0.54f, Neck, Trunk, 0.3f),
            };
            body.Loft(trunk, 7, Plumage);

            // A short neck is a shape and a long one is a limb, and the head goes on the end of either.
            Vector3 headRoot = HeadRoot(look);
            float neckWide = MathF.Max(g * 0.34f, look.Head * 0.62f);
            Span<Ring> neck = stackalloc Ring[]
            {
                new Ring(NeckRoot(look), g * 0.52f, g * 0.52f, Neck),
                new Ring(Vector3.Lerp(NeckRoot(look), headRoot, 0.55f), neckWide, neckWide, Neck, Head, 0.4f),
                new Ring(headRoot, neckWide * 0.9f, neckWide * 0.9f, Head),
            };
            body.Loft(neck, 6, Plumage);

            Vector3 skull = headRoot + new Vector3(0f, look.Head * 0.35f, look.Head * 0.45f);
            body.Egg(skull, new Vector3(look.Head * 0.84f, look.Head * 0.86f, look.Head), look.Crown, Head, 6, 3);

            Vector3 billRoot = skull + new Vector3(0f, -look.Head * 0.12f, look.Head * 0.78f);
            body.Limb(billRoot, billRoot + new Vector3(0f, -look.Bill * 0.14f, look.Bill), look.BillDepth, look.BillDepth * 0.14f, look.Beak, Head, 4);
            for (int side = -1; side <= 1; side += 2)
            {
                body.Egg(skull + new Vector3(look.Head * 0.74f * side, look.Head * 0.14f, look.Head * 0.3f), new Vector3(look.Head * 0.16f), new Vector3(0.012f, 0.01f, 0.01f), Head, 4, 1);
                if (look.Ears) Ear(body, skull, look, side);
                Wing(body, look, side);
                Leg(body, look, side);
            }

            TailFan(body, look);
            return body.Build();
        }

        private static void Ear(BodyMesh body, Vector3 skull, BirdLook look, float side)
        {
            Vector3 foot = skull + new Vector3(look.Head * 0.5f * side, look.Head * 0.6f, -look.Head * 0.1f);
            int a = body.Corner(foot + new Vector3(-look.Head * 0.3f * side, 0f, 0f), look.Crown, Head);
            int b = body.Corner(foot + new Vector3(look.Head * 0.3f * side, 0f, 0f), look.Crown, Head);
            int c = body.Corner(foot + new Vector3(look.Head * 0.3f * side, look.Head * 1.1f, -look.Head * 0.1f), look.Crown, Head);
            if (side > 0) body.Triangle(a, b, c);
            else body.Triangle(b, a, c);
        }

        private static void Leg(BodyMesh body, BirdLook look, float side)
        {
            int bone = side > 0 ? LegL : LegR;
            Vector3 hip = Hip(look, side);
            Vector3 ankle = new Vector3(hip.X, look.Head * 0.06f + 0.015f, hip.Z + look.Legs * 0.1f);
            float thick = MathF.Max(look.Girth * 0.1f, 0.012f);
            body.Limb(hip, ankle, thick * 1.2f, thick * 0.8f, look.Foot, bone, 4);
            body.Egg(ankle + new Vector3(0f, -0.005f, thick * 2.5f), new Vector3(thick * 1.6f, thick * 0.6f, thick * 4f), look.Foot, bone, 4, 1);
        }

        // Stations out along a wing, each a place on the leading edge and how far back the feathers run from it. The wrist is a station of its own, shared between arm and hand, so the skin there stretches and the
        // two don't come apart.
        private static void Wing(BodyMesh body, BirdLook look, float side)
        {
            int arm = side > 0 ? ArmL : ArmR;
            int hand = side > 0 ? HandL : HandR;
            Vector3 shoulder = Shoulder(look, side);
            float span = look.Arm + look.Hand;
            float wrist = look.Arm / span;

            var stations = new List<float> { 0f, wrist * 0.5f, wrist };
            int outer = look.Fingers > 0 ? look.Fingers * 2 + 1 : 3;
            for (int i = 1; i <= outer; i++) stations.Add(wrist + (1f - wrist) * i / outer);

            const int Rows = 3;
            int[] previous = new int[Rows + 1];
            for (int s = 0; s < stations.Count; s++)
            {
                float t = stations[s];
                bool onHand = t > wrist + 0.001f;
                bool atWrist = MathF.Abs(t - wrist) < 0.001f;
                int bone = onHand || atWrist ? hand : arm;

                // Fingers are the gaps: a soaring bird's wing ends in a hand of separate feathers.
                float depth = Chord(look, t);
                if (look.Fingers > 0 && onHand && t > wrist + (1f - wrist) * 0.35f && s % 2 == 1) depth *= 0.45f;

                float lead = LeadingEdge(look, t);
                float droop = -0.05f * look.Chord * MathF.Sin(MathF.PI * t);
                int[] row = new int[Rows + 1];
                for (int r = 0; r <= Rows; r++)
                {
                    float across = (float)r / Rows;
                    Vector3 place = shoulder + new Vector3(span * t * side, droop - across * across * look.Chord * 0.1f + across * (1f - across) * look.Chord * 0.14f, lead - depth * across);
                    Vector3 colour = FeatherColour(look, t, across);
                    row[r] = atWrist ? body.Corner(place, colour, arm, hand, 0.5f) : body.Corner(place, colour, bone);
                }

                if (s > 0)
                {
                    for (int r = 0; r < Rows; r++)
                    {
                        if (side > 0) body.Quad(previous[r], previous[r + 1], row[r + 1], row[r]);
                        else body.Quad(previous[r], row[r], row[r + 1], previous[r + 1]);
                    }
                }
                previous = row;
            }
        }

        private static float Chord(BirdLook look, float t)
        {
            float tip = MathF.Pow(1f - Math.Clamp(t, 0f, 1f), 0.4f + look.Point * 0.9f);
            return look.Chord * (0.72f + 0.28f * MathF.Sin(MathF.PI * MathF.Min(t * 1.5f, 1f))) * MathF.Max(tip, 0.12f + (1f - look.Point) * 0.22f);
        }

        // The wrist stands forward of the shoulder, and a pointed wing sweeps back from it.
        private static float LeadingEdge(BirdLook look, float t)
        {
            float wrist = look.Arm / (look.Arm + look.Hand);
            float forward = look.Chord * 0.2f * MathF.Sin(MathF.PI * 0.5f * MathF.Min(t / wrist, 1f));
            float back = t > wrist ? (t - wrist) / (1f - wrist) : 0f;
            return forward - look.Chord * (0.1f + look.Point * 0.7f) * back * back;
        }

        private static Vector3 FeatherColour(BirdLook look, float t, float across)
        {
            Vector3 colour = Vector3.Lerp(look.Wing, look.Tips, Smooth(0.5f, 0.92f, t) * 0.85f + Smooth(0.5f, 1f, across) * 0.3f);
            if (look.Bars is Vector3 bars && t < 0.6f)
            {
                float bar = MathF.Abs(across - 0.42f) < 0.2f ? 1f : 0f;
                colour = Vector3.Lerp(colour, bars, bar);
            }
            return colour;
        }

        private static void TailFan(BodyMesh body, BirdLook look)
        {
            float y = Height(look) + look.Girth * 0.06f;
            float root = -look.Body * 0.46f;
            float end = root - look.Tail;
            float half = look.TailWidth;
            float notch = root - look.Tail * (1f - look.Fork);

            int a = body.Corner(new Vector3(look.Girth * 0.3f, y, root), look.TailColour, Tail);
            int b = body.Corner(new Vector3(-look.Girth * 0.3f, y, root), look.TailColour, Tail);
            int c = body.Corner(new Vector3(-half, y - look.Tail * 0.04f, end), look.TailColour * 0.8f, Tail);
            int d = body.Corner(new Vector3(0f, y, notch), look.TailColour * 0.8f, Tail);
            int e = body.Corner(new Vector3(half, y - look.Tail * 0.04f, end), look.TailColour * 0.8f, Tail);
            body.Triangle(a, b, d);
            body.Triangle(b, c, d);
            body.Triangle(a, d, e);
        }

        private static float Smooth(float from, float to, float value)
        {
            float t = Math.Clamp((value - from) / (to - from), 0f, 1f);
            return t * t * (3f - 2f * t);
        }
    }
}

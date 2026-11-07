using Dino_Engine.Modelling.Model;
using Dino_Engine.Wildlife;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Wildlife
{
    /// <summary>What a rabbit or a hare looks like. Colours are albedos under a sun of about 13, so what's meant to read as pale is still well short of white.</summary>
    public sealed class RabbitLook
    {
        public Vector3 Back = new Vector3(0.17f, 0.115f, 0.07f);
        public Vector3 Belly = new Vector3(0.36f, 0.3f, 0.22f);
        public Vector3 Scut = new Vector3(0.7f, 0.68f, 0.62f);
        public Vector3 EarTip = new Vector3(0.09f, 0.07f, 0.05f);
        public float Ears = 0.4f;           // Metres long. A hare's are longer, and black at the tip.
    }

    /// <summary>
    /// A rabbit, on twelve bones. Nearly everything of it is in the rump and the ears: seen from over it, that's a pear with two long leaves on the end. People are 2.7 times real size here, and this is a
    /// little more than that, or it's lost in the grass from where the camera is.
    /// </summary>
    public static class RabbitBody
    {
        public const int Pelvis = 0, Chest = 1, Head = 2, EarL = 3, EarR = 4, Scut = 5, ForeL = 6, ForeR = 7, ThighL = 8, ThighR = 9, FootL = 10, FootR = 11, Count = 12;

        private static readonly Vector3 HipPivot = new Vector3(0f, 0.3f, -0.28f);

        public static Skeleton NewSkeleton() => new Skeleton(new[]
        {
            HipPivot,
            new Vector3(0f, 0.34f, 0.02f),
            new Vector3(0f, 0.44f, 0.31f),
            new Vector3(0.05f, 0.55f, 0.36f),
            new Vector3(-0.05f, 0.55f, 0.36f),
            new Vector3(0f, 0.37f, -0.5f),
            new Vector3(0.11f, 0.27f, 0.19f),
            new Vector3(-0.11f, 0.27f, 0.19f),
            new Vector3(0.18f, 0.3f, -0.24f),
            new Vector3(-0.18f, 0.3f, -0.24f),
            new Vector3(0.18f, 0.07f, -0.38f),
            new Vector3(-0.18f, 0.07f, -0.38f),
        }, new[] { -1, Pelvis, Chest, Head, Head, Pelvis, Chest, Chest, Pelvis, Pelvis, ThighL, ThighR });

        public static Mesh Build(RabbitLook look)
        {
            var body = new BodyMesh();
            Vector3 Coat(float along, float up) => Vector3.Lerp(look.Belly, look.Back, Smooth(-0.35f, 0.3f, up));

            // From the rump to the neck: wide at the haunches, drawn in behind the shoulders.
            Span<Ring> trunk = stackalloc Ring[]
            {
                new Ring(new Vector3(0f, 0.335f, -0.51f), 0.1f, 0.1f, Pelvis),
                new Ring(new Vector3(0f, 0.335f, -0.45f), 0.17f, 0.18f, Pelvis),
                new Ring(new Vector3(0f, 0.34f, -0.34f), 0.235f, 0.25f, Pelvis),
                new Ring(new Vector3(0f, 0.35f, -0.2f), 0.245f, 0.265f, Pelvis),
                new Ring(new Vector3(0f, 0.35f, -0.06f), 0.215f, 0.235f, Pelvis, Chest, 0.5f),
                new Ring(new Vector3(0f, 0.345f, 0.07f), 0.18f, 0.205f, Chest),
                new Ring(new Vector3(0f, 0.35f, 0.18f), 0.15f, 0.18f, Chest),
                new Ring(new Vector3(0f, 0.375f, 0.27f), 0.115f, 0.135f, Chest, Head, 0.25f),
                new Ring(new Vector3(0f, 0.415f, 0.33f), 0.085f, 0.095f, Chest, Head, 0.75f),
            };
            body.Loft(trunk, 8, Coat);

            Span<Ring> head = stackalloc Ring[]
            {
                new Ring(new Vector3(0f, 0.44f, 0.31f), 0.07f, 0.08f, Head),
                new Ring(new Vector3(0f, 0.475f, 0.37f), 0.1f, 0.1f, Head),
                new Ring(new Vector3(0f, 0.475f, 0.45f), 0.095f, 0.09f, Head),
                new Ring(new Vector3(0f, 0.45f, 0.53f), 0.062f, 0.06f, Head),
                new Ring(new Vector3(0f, 0.43f, 0.585f), 0.032f, 0.032f, Head),
            };
            body.Loft(head, 7, Coat);

            for (int side = -1; side <= 1; side += 2)
            {
                float x = side;
                body.Egg(new Vector3(0.088f * x, 0.495f, 0.43f), new Vector3(0.018f, 0.024f, 0.028f), new Vector3(0.012f, 0.01f, 0.01f), Head, 5, 2);
                Ear(body, look, side > 0 ? EarL : EarR, x);

                int fore = side > 0 ? ForeL : ForeR;
                int thigh = side > 0 ? ThighL : ThighR;
                int foot = side > 0 ? FootL : FootR;
                body.Limb(new Vector3(0.11f * x, 0.28f, 0.19f), new Vector3(0.1f * x, 0.04f, 0.23f), 0.055f, 0.036f, look.Back * 1.15f, fore);
                body.Egg(new Vector3(0.1f * x, 0.03f, 0.26f), new Vector3(0.042f, 0.03f, 0.07f), look.Belly, fore, 5, 2);

                body.Egg(new Vector3(0.2f * x, 0.26f, -0.27f), new Vector3(0.085f, 0.17f, 0.2f), look.Back * 1.08f, thigh, 6, 3, -0.4f);
                body.Limb(new Vector3(0.19f * x, 0.2f, -0.32f), new Vector3(0.185f * x, 0.07f, -0.38f), 0.07f, 0.045f, look.Back * 1.05f, thigh);
                body.Egg(new Vector3(0.185f * x, 0.045f, -0.25f), new Vector3(0.05f, 0.042f, 0.17f), look.Belly * 0.85f, foot, 5, 2);
            }

            body.Egg(new Vector3(0f, 0.39f, -0.54f), new Vector3(0.08f, 0.08f, 0.075f), look.Scut, Scut, 6, 3);
            return body.Build();
        }

        // A long leaf of a thing, cupped forward, on a joint at its foot.
        private static void Ear(BodyMesh body, RabbitLook look, int bone, float side)
        {
            const int Rows = 4;
            float length = look.Ears;
            Vector3 foot = new Vector3(0.05f * side, 0.55f, 0.36f);
            int previousIn = -1, previousOut = -1, previousMid = -1;

            for (int row = 0; row <= Rows; row++)
            {
                float t = (float)row / Rows;
                float wide = 0.062f * MathF.Pow(MathF.Sin(MathF.PI * MathF.Min(0.14f + t * 0.86f, 1f)), 0.7f);
                Vector3 mid = foot + new Vector3(0.05f * side * t, length * t, -0.09f * t * t);
                Vector3 colour = Vector3.Lerp(look.Back * 1.3f, look.EarTip, Smooth(0.6f, 0.95f, t));
                int inner = body.Corner(mid + new Vector3(-wide * side, 0f, 0.012f), colour, bone);
                int centre = body.Corner(mid + new Vector3(0f, 0f, -0.018f), colour, bone);
                int outer = body.Corner(mid + new Vector3(wide * side, 0f, 0.012f), colour, bone);
                if (row > 0)
                {
                    if (side > 0)
                    {
                        body.Quad(previousIn, previousMid, centre, inner);
                        body.Quad(previousMid, previousOut, outer, centre);
                    }
                    else
                    {
                        body.Quad(previousIn, inner, centre, previousMid);
                        body.Quad(previousMid, centre, outer, previousOut);
                    }
                }
                previousIn = inner;
                previousMid = centre;
                previousOut = outer;
            }
        }

        private static float Smooth(float from, float to, float value)
        {
            float t = Math.Clamp((value - from) / (to - from), 0f, 1f);
            return t * t * (3f - 2f * t);
        }
    }
}

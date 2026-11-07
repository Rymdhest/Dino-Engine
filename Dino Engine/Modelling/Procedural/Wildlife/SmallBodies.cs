using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.Wildlife;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Wildlife
{
    /// <summary>A butterfly's colours: the ground of its wings, the border, and the spot in the hind wing that's for showing to birds.</summary>
    public sealed class ButterflyLook
    {
        public float Span = 0.38f;          // Metres across, a good deal over life size, or it's two pixels from up here.
        public Vector3 Ground = new Vector3(0.5f, 0.2f, 0.02f);
        public Vector3 Border = new Vector3(0.03f, 0.025f, 0.02f);
        public Vector3 Spot = new Vector3(0.55f, 0.5f, 0.4f);
        public Vector3 Body = new Vector3(0.05f, 0.04f, 0.03f);
    }

    /// <summary>A butterfly: a body and two wings of a few triangles each, which is everything it is. Its wings shut over its back, so they hinge along its length.</summary>
    public static class ButterflyBody
    {
        public const int Trunk = 0, WingL = 1, WingR = 2, Count = 3;

        public static Skeleton NewSkeleton() => new Skeleton(new[] { Vector3.Zero, Vector3.Zero, Vector3.Zero }, new[] { -1, Trunk, Trunk });

        public static Mesh Build(ButterflyLook look)
        {
            var body = new BodyMesh();
            float s = look.Span * 0.5f;
            body.Egg(new Vector3(0f, 0f, -s * 0.05f), new Vector3(s * 0.07f, s * 0.07f, s * 0.42f), look.Body, Trunk, 4, 2);

            for (int side = -1; side <= 1; side += 2)
            {
                int bone = side > 0 ? WingL : WingR;
                float x = side;

                // Round from the root: the fore wing's long edge first, then the hind wing's lobe.
                (Vector2 at, Vector3 colour)[] rim =
                {
                    (new Vector2(0.05f, 0.34f), look.Border),
                    (new Vector2(0.55f, 0.6f), look.Border),
                    (new Vector2(1f, 0.58f), look.Border),
                    (new Vector2(0.95f, 0.14f), look.Border),
                    (new Vector2(0.62f, -0.06f), look.Ground),
                    (new Vector2(0.78f, -0.42f), look.Border),
                    (new Vector2(0.5f, -0.74f), look.Border),
                    (new Vector2(0.14f, -0.6f), look.Border),
                };
                int hub = body.Corner(new Vector3(0.03f * s * x, 0f, 0f), look.Ground, bone);
                int inner = body.Corner(new Vector3(0.45f * s * x, 0f, 0.2f * s), look.Ground, bone);
                int spot = body.Corner(new Vector3(0.4f * s * x, 0f, -0.36f * s), look.Spot, bone);

                int[] corners = new int[rim.Length];
                for (int i = 0; i < rim.Length; i++) corners[i] = body.Corner(new Vector3(rim[i].at.X * s * x, 0f, rim[i].at.Y * s), rim[i].colour, bone);

                void Face(int a, int b, int c)
                {
                    if (side > 0) body.Triangle(a, c, b);
                    else body.Triangle(a, b, c);
                }

                Face(hub, corners[0], inner);
                Face(inner, corners[0], corners[1]);
                Face(inner, corners[1], corners[2]);
                Face(inner, corners[2], corners[3]);
                Face(inner, corners[3], corners[4]);
                Face(hub, inner, corners[4]);
                Face(hub, corners[4], spot);
                Face(spot, corners[4], corners[5]);
                Face(spot, corners[5], corners[6]);
                Face(spot, corners[6], corners[7]);
                Face(hub, spot, corners[7]);
            }
            return body.Build();
        }
    }

    /// <summary>A dragonfly: a long thin body with four narrow wings held out flat, which shimmer more than they beat.</summary>
    public sealed class DragonflyLook
    {
        public float Length = 0.5f;
        public Vector3 Body = new Vector3(0.04f, 0.2f, 0.34f);
        public Vector3 Head = new Vector3(0.05f, 0.1f, 0.08f);
        public Vector3 Wing = new Vector3(0.5f, 0.56f, 0.56f);
    }

    public static class DragonflyBody
    {
        public const int Trunk = 0, FrontL = 1, FrontR = 2, BackL = 3, BackR = 4, Count = 5;

        public static Skeleton NewSkeleton(DragonflyLook look)
        {
            float l = look.Length;
            return new Skeleton(new[] { Vector3.Zero, new Vector3(0f, 0f, l * 0.2f), new Vector3(0f, 0f, l * 0.2f), new Vector3(0f, 0f, l * 0.1f), new Vector3(0f, 0f, l * 0.1f) }, new[] { -1, Trunk, Trunk, Trunk, Trunk });
        }

        public static Mesh Build(DragonflyLook look)
        {
            var body = new BodyMesh();
            float l = look.Length;
            body.Limb(new Vector3(0f, 0f, l * 0.08f), new Vector3(0f, 0f, -l * 0.62f), l * 0.035f, l * 0.018f, look.Body, Trunk, 4);
            body.Egg(new Vector3(0f, 0f, l * 0.16f), new Vector3(l * 0.06f, l * 0.06f, l * 0.11f), look.Body * 0.7f, Trunk, 4, 2);
            body.Egg(new Vector3(0f, 0f, l * 0.31f), new Vector3(l * 0.06f, l * 0.05f, l * 0.05f), look.Head, Trunk, 4, 1);

            for (int side = -1; side <= 1; side += 2)
            {
                Wing(body, look, side > 0 ? FrontL : FrontR, side, l * 0.2f, l * 0.62f);
                Wing(body, look, side > 0 ? BackL : BackR, side, l * 0.1f, l * 0.58f);
            }
            return body.Build();
        }

        private static void Wing(BodyMesh body, DragonflyLook look, int bone, float side, float z, float length)
        {
            int a = body.Corner(new Vector3(0.02f * side, 0f, z), look.Wing, bone);
            int b = body.Corner(new Vector3(length * 0.45f * side, 0f, z + length * 0.07f), look.Wing, bone);
            int c = body.Corner(new Vector3(length * side, 0f, z + length * 0.02f), look.Wing, bone);
            int d = body.Corner(new Vector3(length * 0.55f * side, 0f, z - length * 0.11f), look.Wing, bone);
            if (side > 0)
            {
                body.Triangle(a, d, b);
                body.Triangle(b, d, c);
            }
            else
            {
                body.Triangle(a, b, d);
                body.Triangle(b, c, d);
            }
        }
    }

    /// <summary>What a frog looks like.</summary>
    public sealed class FrogLook
    {
        public Vector3 Back = new Vector3(0.07f, 0.17f, 0.05f);
        public Vector3 Belly = new Vector3(0.45f, 0.42f, 0.26f);
        public Vector3 Eye = new Vector3(0.28f, 0.2f, 0.03f);
    }

    /// <summary>A frog, folded up on its hind legs, which unfold to twice its length. Bigger than life, like the rest of what's small.</summary>
    public static class FrogBody
    {
        public const int Trunk = 0, ThighL = 1, ThighR = 2, ShinL = 3, ShinR = 4, ArmL = 5, ArmR = 6, Count = 7;

        private static Vector3 HipAt(float x) => new Vector3(0.09f * x, 0.1f, -0.12f);
        private static Vector3 KneeAt(float x) => new Vector3(0.19f * x, 0.08f, 0.02f);

        public static Skeleton NewSkeleton() => new Skeleton(new[]
        {
            new Vector3(0f, 0.09f, -0.1f),
            HipAt(1f), HipAt(-1f), KneeAt(1f), KneeAt(-1f),
            new Vector3(0.09f, 0.11f, 0.08f), new Vector3(-0.09f, 0.11f, 0.08f),
        }, new[] { -1, Trunk, Trunk, ThighL, ThighR, Trunk, Trunk });

        public static Mesh Build(FrogLook look)
        {
            var body = new BodyMesh();
            Vector3 Skin(float along, float up) => Vector3.Lerp(look.Belly, look.Back, Math.Clamp((up + 0.25f) * 2.4f, 0f, 1f));

            Span<Ring> trunk = stackalloc Ring[]
            {
                new Ring(new Vector3(0f, 0.11f, -0.17f), 0.05f, 0.04f, Trunk),
                new Ring(new Vector3(0f, 0.125f, -0.09f), 0.12f, 0.085f, Trunk),
                new Ring(new Vector3(0f, 0.14f, 0.03f), 0.135f, 0.09f, Trunk),
                new Ring(new Vector3(0f, 0.15f, 0.13f), 0.115f, 0.07f, Trunk),
                new Ring(new Vector3(0f, 0.145f, 0.19f), 0.06f, 0.035f, Trunk),
            };
            body.Loft(trunk, 7, Skin);

            for (int side = -1; side <= 1; side += 2)
            {
                float x = side;
                body.Egg(new Vector3(0.082f * x, 0.205f, 0.1f), new Vector3(0.036f), look.Eye, Trunk, 5, 2);
                int thigh = side > 0 ? ThighL : ThighR;
                int shin = side > 0 ? ShinL : ShinR;
                int arm = side > 0 ? ArmL : ArmR;
                body.Limb(HipAt(x), KneeAt(x), 0.05f, 0.032f, look.Back, thigh);
                body.Limb(KneeAt(x), new Vector3(0.14f * x, 0.03f, -0.18f), 0.03f, 0.018f, look.Back * 1.1f, shin);
                body.Egg(new Vector3(0.15f * x, 0.02f, -0.1f), new Vector3(0.03f, 0.012f, 0.12f), look.Back * 1.1f, shin, 4, 1);
                body.Limb(new Vector3(0.09f * x, 0.11f, 0.08f), new Vector3(0.12f * x, 0.02f, 0.15f), 0.024f, 0.014f, look.Back * 1.2f, arm, 4);
            }
            return body.Build();
        }
    }

    /// <summary>A fish, for the moment it's out of the water. It bends down its length at two joints.</summary>
    public sealed class FishLook
    {
        public float Length = 1.05f;
        public Vector3 Back = new Vector3(0.07f, 0.09f, 0.1f);
        public Vector3 Belly = new Vector3(0.55f, 0.56f, 0.56f);
    }

    public static class FishBody
    {
        public const int Front = 0, Middle = 1, Tail = 2, Count = 3;

        public static Skeleton NewSkeleton(FishLook look) => new Skeleton(new[] { Vector3.Zero, new Vector3(0f, 0f, -look.Length * 0.1f), new Vector3(0f, 0f, -look.Length * 0.34f) }, new[] { -1, Front, Middle });

        public static Mesh Build(FishLook look)
        {
            var body = new BodyMesh();
            float l = look.Length;
            Vector3 Scales(float along, float up) => Vector3.Lerp(look.Belly, look.Back, Math.Clamp((up + 0.2f) * 1.6f, 0f, 1f));
            Span<Ring> trunk = stackalloc Ring[]
            {
                new Ring(new Vector3(0f, 0f, l * 0.44f), l * 0.03f, l * 0.04f, Front),
                new Ring(new Vector3(0f, 0f, l * 0.3f), l * 0.075f, l * 0.11f, Front),
                new Ring(new Vector3(0f, 0.01f, l * 0.08f), l * 0.09f, l * 0.14f, Front, Middle, 0.5f),
                new Ring(new Vector3(0f, 0.01f, -l * 0.14f), l * 0.07f, l * 0.11f, Middle),
                new Ring(new Vector3(0f, 0f, -l * 0.34f), l * 0.035f, l * 0.055f, Middle, Tail, 0.5f),
                new Ring(new Vector3(0f, 0f, -l * 0.42f), l * 0.016f, l * 0.03f, Tail),
            };
            body.Loft(trunk, 6, Scales);

            int a = body.Corner(new Vector3(0f, 0f, -l * 0.41f), look.Back, Tail);
            int b = body.Corner(new Vector3(0f, l * 0.11f, -l * 0.56f), look.Back * 0.8f, Tail);
            int c = body.Corner(new Vector3(0f, 0f, -l * 0.5f), look.Back, Tail);
            int d = body.Corner(new Vector3(0f, -l * 0.11f, -l * 0.56f), look.Back * 0.8f, Tail);
            body.Triangle(a, b, c);
            body.Triangle(a, c, d);
            return body.Build();
        }
    }

    /// <summary>
    /// A leaf in the air. It's the same outline its tree's cards are painted from, and it's geometry and not a card: from up here a leaf is a few pixels, an edge is what's seen of it, and the antialiasing that gives it for
    /// nothing is more than a texture could. Its colour comes with each one, so the mesh is white, and its creased midrib catches the light differently on each side as it turns.
    /// </summary>
    public static class LeafBody
    {
        public const int Count = 1;

        private const int FewestSteps = 7;      // Down each side of a blade, and more for every lobe, or lobes alias into a zigzag.
        private const int FewestSpokes = 18;    // Round a palmate leaf, likewise.

        public static Skeleton NewSkeleton() => new Skeleton(new[] { Vector3.Zero }, new[] { -1 });

        /// <param name="length">Metres, stalk to tip.</param>
        /// <param name="crease">Radians each half stands up from flat.</param>
        public static Mesh Build(LeafForm form, float length, float crease = 0.28f, uint seed = 11u)
        {
            var body = new BodyMesh();
            uint noise = FloraNoise.Hash(seed);
            Vector3 blade = Vector3.One;
            Vector3 rib = new Vector3(0.72f, 0.72f, 0.66f);
            float centre = 0.5f;
            float lift = MathF.Tan(crease);

            // A blade runs along +z through the origin, which is where it turns about.
            Vector3 On(float across, float along) => new Vector3(across * length, MathF.Abs(across) * length * lift, (along - centre) * length);

            if (form.Blade == LeafBlade.Palmate || form.Blade == LeafBlade.Fan)
            {
                bool palmate = form.Blade == LeafBlade.Palmate;
                float root = palmate ? 0.3f : 0.02f;
                float full = palmate ? 0.72f : 1f;
                float spread = palmate ? MathF.PI : form.LobeSpread;
                int hub = body.Corner(On(0f, root), rib, 0);
                int previous = -1;
                int spokes = Math.Max(FewestSpokes, Math.Max(form.Lobes, 3) * 8);
                for (int i = 0; i <= spokes; i++)
                {
                    float angle = -spread + 2f * spread * i / spokes;
                    float reach = (palmate ? form.PalmateReach(angle, noise) : form.FanReach(angle * 0.995f, noise)) * full;
                    int corner = body.Corner(On(MathF.Sin(angle) * reach, root + MathF.Cos(angle) * reach), blade, 0);
                    if (previous >= 0) body.Triangle(hub, corner, previous);
                    previous = corner;
                }
                return body.Build();
            }

            int lastLeft = -1, lastRight = -1, lastRib = -1;
            int steps = Math.Max(FewestSteps, form.Lobes * 5);
            for (int i = 0; i <= steps; i++)
            {
                float t = 0.02f + 0.97f * i / steps;
                float left = form.HalfWidth(t, noise, -1f) * form.Width;
                float right = form.HalfWidth(t, noise, 1f) * form.Width;
                int middle = body.Corner(On(0f, t), rib, 0);
                int a = body.Corner(On(-left, t), blade, 0);
                int b = body.Corner(On(right, t), blade, 0);
                if (i > 0)
                {
                    body.Quad(lastRib, middle, a, lastLeft);
                    body.Quad(lastRib, lastRight, b, middle);
                }
                lastRib = middle;
                lastLeft = a;
                lastRight = b;
            }
            return body.Build();
        }
    }
}

using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Flora
{
    /// <summary>
    /// A kind of card: a picture of a piece of foliage that's put on a plant as one bent quad, which is the only way to pay for a crown. A card is painted from real parts (leaves on
    /// twigs, needles on shoots) and it's pale, as a plant's colours are in its vertices, so one card serves a green tree, a red one and a gold one. Four different pictures of it are
    /// painted, one to a corner of a layer, and a plant picks between them, so that no two neighbouring cards are the same.
    /// </summary>
    public abstract class CardForm
    {
        public string Id = "";

        /// <summary>Which layer of cards it's on. Set when the library is built.</summary>
        public int Layer;

        public abstract void Paint(CardCanvas canvas, Genes genes);

        protected static Vector2 Turn(Vector2 direction, float angle)
        {
            float cos = MathF.Cos(angle), sin = MathF.Sin(angle);
            return new Vector2(direction.X * cos - direction.Y * sin, direction.X * sin + direction.Y * cos);
        }

        // Keeps a point on the card, away from its edge, as what touches the edge of a card shows up as a straight line in the crown.
        protected static Vector2 Inside(Vector2 point, float margin = 0.02f) => Vector2.Clamp(point, new Vector2(margin), new Vector2(1f - margin));

        protected static bool OnCard(Vector2 point, float margin = 0.015f) => point.X > margin && point.X < 1f - margin && point.Y > margin && point.Y < 1f - margin;
    }

    /// <summary>A twig with leaves on it: most broadleaved things.</summary>
    public sealed class LeafSpray : CardForm
    {
        public LeafForm Leaf = new LeafForm();
        public float LeafLength = 0.16f;            // As a share of the card.
        public float LeafWidth = 1f;                // Squeezes the leaf's own width.
        public IntRange Shoots = new IntRange(6, 9);
        public IntRange LeavesPerShoot = new IntRange(5, 8);
        public float ShootAngle = 0.75f;            // Radians off the main twig.
        public float ShootLength = 0.42f;
        public float LeafAngle = 0.6f;              // Radians off its shoot.
        public float Droop = 0.25f;                 // How far leaves hang towards the foot of the card, as a leaf turns away from its own weight.
        public bool Opposite;                       // Leaves in pairs.
        public bool Hanging;                        // Long shoots trailing down the card, as a willow.
        public float Fold = 0.35f;
        public float Translucency = 0.72f;
        public float Roughness = 0.74f;
        public Vector3 Twig = new Vector3(0.14f, 0.1f, 0.07f);

        public override void Paint(CardCanvas canvas, Genes genes)
        {
            WorldRandom random = genes.Stream("spray");
            var leaves = new LeafImage[3];
            for (int i = 0; i < leaves.Length; i++) leaves[i] = Leaf.Paint(160, (uint)random.NextULong());

            Vector2 foot = new Vector2(0.5f + random.Range(-0.04f, 0.04f), 0.015f);
            Vector2 direction = new Vector2(random.Range(-0.1f, 0.1f), 1f).Normalized();
            int shoots = random.Int(Shoots);

            // The main twig bends as it goes, and leaves come off it as well as off its shoots.
            const int Steps = 12;
            Vector2 at = foot;
            float bend = random.Range(-0.5f, 0.5f);
            var path = new Vector2[Steps + 1];
            path[0] = at;
            for (int i = 1; i <= Steps; i++)
            {
                direction = Turn(direction, bend / Steps + random.Range(-0.07f, 0.07f));
                at += direction * (0.92f / Steps);
                path[i] = Inside(at);
                float t = (float)i / Steps;
                Vector2 from = path[i - 1], to = path[i];
                canvas.Later(0.42f, () => canvas.Stroke(from, to, MathHelper.Lerp(0.0075f, 0.003f, t - 1f / Steps), MathHelper.Lerp(0.0075f, 0.003f, t), CardInk.Wood(Twig, 0.4f)));
            }

            for (int s = 0; s < shoots; s++)
            {
                float t = 0.12f + 0.85f * (s + random.Range(0f, 0.6f)) / shoots;
                int index = Math.Clamp((int)(t * Steps), 0, Steps - 1);
                Vector2 start = path[index];
                Vector2 heading = (path[index + 1] - path[index]).Normalized();
                float side = s % 2 == 0 ? 1f : -1f;
                float reach = ShootLength * MathF.Sin(MathF.PI * MathF.Min(t + 0.14f, 0.98f)) * random.Range(0.8f, 1.15f);
                Vector2 shoot = Turn(heading, side * ShootAngle * random.Range(0.75f, 1.2f));
                if (Hanging) shoot = Vector2.Lerp(shoot, new Vector2(shoot.X * 0.3f, -1f), 0.55f).Normalized();
                Shoot(canvas, random, leaves, start, shoot, reach, side, 0.2f + random.Float() * 0.75f);
            }
            Shoot(canvas, random, leaves, path[Steps - 2], (path[Steps] - path[Steps - 2]).Normalized(), 0.14f, 1f, 0.95f);
        }

        private void Shoot(CardCanvas canvas, WorldRandom random, LeafImage[] leaves, Vector2 start, Vector2 heading, float reach, float side, float depth)
        {
            int count = random.Int(LeavesPerShoot);
            Vector2 at = start;
            float curl = random.Range(-0.35f, 0.35f) - side * 0.15f;
            for (int i = 0; i < count; i++)
            {
                float t = (i + 1f) / count;
                Vector2 next = Inside(at + heading * (reach / count));
                float radius = MathHelper.Lerp(0.0042f, 0.0016f, t);
                Vector2 from = at, to = next;
                canvas.Later(depth - 0.02f, () => canvas.Stroke(from, to, radius, radius * 0.8f, CardInk.Wood(Twig, depth)));
                at = next;
                heading = Turn(heading, curl / count);

                int pairs = Opposite ? 2 : 1;
                for (int pair = 0; pair < pairs; pair++)
                {
                    float which = Opposite ? (pair == 0 ? 1f : -1f) : (i % 2 == 0 ? 1f : -1f);
                    AddLeaf(canvas, random, leaves, at, heading, which, depth + random.Range(-0.12f, 0.12f), 1f - 0.3f * t);
                }
            }
            AddLeaf(canvas, random, leaves, at, heading, 0f, depth + 0.1f, 0.85f);
        }

        private void AddLeaf(CardCanvas canvas, WorldRandom random, LeafImage[] leaves, Vector2 at, Vector2 heading, float side, float depth, float size)
        {
            float length = LeafLength * size * random.Range(0.8f, 1.15f);
            float angle = side * LeafAngle * random.Range(0.7f, 1.2f);
            Vector2 direction = Turn(heading, angle);
            direction = (direction + new Vector2(0f, -Droop * random.Range(0.4f, 1.2f))).Normalized();

            // Seen a little on edge, a leaf is shorter and narrower than it is.
            float along = random.Range(0.72f, 1f);
            float across = random.Range(0.55f, 1f);
            Vector2 stalk = at + direction * (length * 0.08f);
            Vector2 tip = stalk + direction * (length * along);
            if (!OnCard(tip) || !OnCard(stalk)) return;

            LeafImage image = leaves[random.Int(leaves.Length)];
            float reach = Leaf.Reach * length * across * LeafWidth;
            float shade = random.Range(0.8f, 1.05f);
            Vector3 colour = new Vector3(shade + random.Range(-0.03f, 0.03f), shade, shade + random.Range(-0.04f, 0.02f));
            Vector3 normal = new Vector3(random.Range(-0.55f, 0.55f), random.Range(-0.55f, 0.55f), 1f).Normalized();
            float clamped = Math.Clamp(depth, 0f, 1f);
            CardInk ink = CardInk.Leaf(colour * (0.7f + 0.3f * clamped), normal, clamped);
            ink.Translucency = Translucency;
            ink.Roughness = Roughness;
            float bend = random.Range(-0.12f, 0.12f);
            canvas.Later(depth, () =>
            {
                canvas.Stroke(at, stalk, 0.0016f, 0.0013f, CardInk.Wood(Twig, clamped));
                canvas.Leaf(image, stalk, tip, reach, ink, Fold, 0.25f, bend);
            });
        }
    }

    /// <summary>A shoot of a conifer with its needles in tufts.</summary>
    public sealed class NeedleSpray : CardForm
    {
        public IntRange Shoots = new IntRange(5, 8);
        public float ShootAngle = 0.6f;
        public float ShootLength = 0.46f;
        public float Needle = 0.11f;            // As a share of the card.
        public float NeedleRadius = 0.0024f;
        public float Spread = 0.8f;             // Radians either side of the shoot.
        public int NeedlesPerShoot = 90;
        public float Tufted = 0.6f;             // How much they gather at the end of a shoot, as a pine's do.
        public Vector3 Twig = new Vector3(0.16f, 0.1f, 0.06f);
        public float Translucency = 0.35f;
        public float Roughness = 0.62f;

        public override void Paint(CardCanvas canvas, Genes genes)
        {
            WorldRandom random = genes.Stream("needles");
            Vector2 start = new Vector2(0.5f + random.Range(-0.03f, 0.03f), 0.015f);
            Vector2 heading = new Vector2(random.Range(-0.1f, 0.1f), 1f).Normalized();
            Vector2 tip = Inside(start + heading * 0.84f);
            Shoot(canvas, random, start, heading, 0.84f, 0.5f, 0.0075f);

            int shoots = random.Int(Shoots);
            for (int s = 0; s < shoots; s++)
            {
                float t = 0.12f + 0.78f * (s + random.Float() * 0.6f) / shoots;
                Vector2 from = Vector2.Lerp(start, tip, t);
                float side = s % 2 == 0 ? 1f : -1f;
                float reach = ShootLength * MathF.Sin(MathF.PI * MathF.Min(t + 0.2f, 0.95f)) * random.Range(0.75f, 1.1f);
                Vector2 direction = Turn(heading, side * ShootAngle * random.Range(0.8f, 1.25f));
                Shoot(canvas, random, from, direction, reach, random.Range(0.2f, 0.9f), 0.0045f);
            }
        }

        private void Shoot(CardCanvas canvas, WorldRandom random, Vector2 from, Vector2 heading, float reach, float depth, float radius)
        {
            Vector2 to = Inside(from + heading * reach);
            canvas.Later(depth - 0.05f, () => canvas.Stroke(from, to, radius, radius * 0.45f, CardInk.Wood(Twig, depth)));

            int needles = (int)(NeedlesPerShoot * MathF.Max(reach / 0.5f, 0.4f));
            for (int i = 0; i < needles; i++)
            {
                float t = MathF.Pow(random.Float(), 1f - Tufted * 0.65f);
                Vector2 foot = Vector2.Lerp(from, to, t);
                float spread = Spread * random.Range(-1f, 1f) * (1f - 0.4f * t * t);
                Vector2 direction = Turn(heading, spread);
                float length = Needle * random.Range(0.75f, 1.15f) * (1f - 0.25f * (1f - t));
                Vector2 end = foot + direction * length;
                if (!OnCard(end)) continue;

                float near = Math.Clamp(depth + random.Range(-0.25f, 0.25f), 0f, 1f);
                float shade = random.Range(0.74f, 1.04f) * (0.68f + 0.32f * near);
                Vector3 normal = new Vector3(random.Range(-0.5f, 0.5f), random.Range(-0.5f, 0.5f), 1f).Normalized();
                CardInk ink = CardInk.Leaf(new Vector3(shade * 0.96f, shade, shade * 0.9f), normal, near);
                ink.Translucency = Translucency;
                ink.Roughness = Roughness;
                canvas.Later(near, () => canvas.Stroke(foot, end, NeedleRadius, NeedleRadius * 0.35f, ink));
            }
        }
    }

    /// <summary>Flat fans of tiny scales, branching and branching: cypress, cedar, and with its short flat needles, redwood.</summary>
    public sealed class ScaleSpray : CardForm
    {
        public int Depth = 3;
        public float Length = 0.86f;
        public float Angle = 0.62f;
        public float Ratio = 0.5f;          // Each branchlet against the one it came from.
        public int Branchlets = 7;
        public float Thickness = 0.011f;
        public int Barbs;                   // Short needles along each side of the finest stems, as a redwood's.
        public float BarbLength = 0.03f;
        public float Translucency = 0.4f;
        public float Roughness = 0.7f;

        public override void Paint(CardCanvas canvas, Genes genes)
        {
            WorldRandom random = genes.Stream("scales");
            Vector2 foot = new Vector2(0.5f + random.Range(-0.03f, 0.03f), 0.015f);
            Branch(canvas, random, foot, new Vector2(random.Range(-0.1f, 0.1f), 1f).Normalized(), Length, Thickness, Depth, 0.5f);
        }

        private void Branch(CardCanvas canvas, WorldRandom random, Vector2 from, Vector2 heading, float length, float radius, int depth, float near)
        {
            const int Steps = 6;
            Vector2 at = from;
            float curl = random.Range(-0.3f, 0.3f);
            for (int i = 0; i < Steps; i++)
            {
                float t = (i + 1f) / Steps;
                Vector2 next = at + heading * (length / Steps);
                if (!OnCard(next)) return;
                Vector2 a = at, b = next;
                float ra = radius * (1f - 0.6f * (t - 1f / Steps)), rb = radius * (1f - 0.6f * t);
                float shade = random.Range(0.82f, 1.02f) * (0.7f + 0.3f * near);
                Vector3 normal = new Vector3(random.Range(-0.35f, 0.35f), random.Range(-0.35f, 0.35f), 1f).Normalized();
                CardInk ink = CardInk.Leaf(new Vector3(shade), normal, near);
                ink.Translucency = Translucency;
                ink.Roughness = Roughness;
                canvas.Later(near, () => canvas.Stroke(a, b, ra, rb, ink));

                if (depth == 0 && Barbs > 0)
                {
                    for (int barb = 0; barb < Barbs; barb++)
                    {
                        float along = (barb + 0.5f) / Barbs;
                        Vector2 root = Vector2.Lerp(a, b, along);
                        foreach (float side in new[] { -1f, 1f })
                        {
                            Vector2 end = root + Turn(heading, side * 1.05f) * BarbLength * random.Range(0.8f, 1.15f);
                            if (!OnCard(end)) continue;
                            canvas.Later(near + 0.01f, () => canvas.Stroke(root, end, radius * 0.5f, radius * 0.2f, ink));
                        }
                    }
                }
                at = next;
                heading = Turn(heading, curl / Steps);
            }

            if (depth <= 0) return;
            for (int i = 0; i < Branchlets; i++)
            {
                float t = 0.12f + 0.8f * (i + random.Float() * 0.5f) / Branchlets;
                Vector2 start = from + heading * (length * t);
                float side = i % 2 == 0 ? 1f : -1f;
                float fade = 1f - 0.55f * t;
                Branch(canvas, random, start, Turn(heading, side * Angle * random.Range(0.85f, 1.15f)), length * Ratio * fade * random.Range(0.85f, 1.1f), radius * 0.68f, depth - 1, Math.Clamp(near + random.Range(-0.2f, 0.25f), 0f, 1f));
            }
        }
    }

    /// <summary>A bare twig crowded with flowers, and maybe a few new leaves.</summary>
    public sealed class BlossomSpray : CardForm
    {
        public LeafForm Petal = new LeafForm { Blade = LeafBlade.Petal, Width = 0.42f, Skew = 1.5f, Round = 0.7f, Notch = 0.2f, Veins = 0, VeinStrength = 0f, Mottle = 0.08f, Tone = new Vector3(1f, 0.97f, 0.98f) };
        public int PetalCount = 5;
        public IntRange Shoots = new IntRange(6, 9);
        public float ShootLength = 0.44f;
        public int FlowersPerShoot = 16;
        public float FlowerSize = 0.06f;
        public Vector3 Heart = new Vector3(0.7f, 0.26f, 0.32f);
        public LeafForm? YoungLeaf;
        public float YoungLeaves = 0.15f;
        public Vector3 Twig = new Vector3(0.08f, 0.05f, 0.05f);

        public override void Paint(CardCanvas canvas, Genes genes)
        {
            WorldRandom random = genes.Stream("blossom");
            var petals = new LeafImage[2];
            for (int i = 0; i < petals.Length; i++) petals[i] = Petal.Paint(64, (uint)random.NextULong());
            LeafImage? young = YoungLeaf?.Paint(96, (uint)random.NextULong());

            Vector2 start = new Vector2(0.5f + random.Range(-0.04f, 0.04f), 0.015f);
            Vector2 heading = new Vector2(random.Range(-0.12f, 0.12f), 1f).Normalized();
            Vector2 tip = Inside(start + heading * 0.9f);
            canvas.Later(0.3f, () => canvas.Stroke(start, tip, 0.0085f, 0.003f, CardInk.Wood(Twig, 0.3f)));
            Cluster(canvas, random, petals, young, Vector2.Lerp(start, tip, 0.45f), tip, 0.75f);

            int shoots = random.Int(Shoots);
            for (int s = 0; s < shoots; s++)
            {
                float t = 0.1f + 0.8f * (s + random.Float() * 0.6f) / shoots;
                Vector2 from = Vector2.Lerp(start, tip, t);
                float side = s % 2 == 0 ? 1f : -1f;
                float reach = ShootLength * MathF.Sin(MathF.PI * MathF.Min(t + 0.16f, 0.96f)) * random.Range(0.8f, 1.15f);
                Vector2 to = Inside(from + Turn(heading, side * random.Range(0.55f, 0.95f)) * reach);
                float depth = random.Range(0.25f, 0.85f);
                canvas.Later(depth - 0.05f, () => canvas.Stroke(from, to, 0.0048f, 0.0018f, CardInk.Wood(Twig, depth)));
                Cluster(canvas, random, petals, young, from, to, depth);
            }
        }

        private void Cluster(CardCanvas canvas, WorldRandom random, LeafImage[] petals, LeafImage? young, Vector2 from, Vector2 to, float depth)
        {
            for (int i = 0; i < FlowersPerShoot; i++)
            {
                float t = MathF.Pow(random.Float(), 0.8f);
                Vector2 at = Vector2.Lerp(from, to, 0.15f + 0.85f * t) + new Vector2(random.Range(-1f, 1f), random.Range(-1f, 1f)) * FlowerSize * 0.75f;
                if (!OnCard(at, 0.05f)) continue;

                float near = Math.Clamp(depth + random.Range(-0.2f, 0.2f), 0f, 1f);
                if (young != null && random.Float() < YoungLeaves)
                {
                    Vector2 direction = Turn(new Vector2(0f, 1f), random.Range(0f, MathF.Tau));
                    float length = FlowerSize * 1.7f;
                    Vector3 green = new Vector3(0.42f, 0.56f, 0.2f) * random.Range(0.8f, 1.05f);
                    CardInk leafInk = CardInk.Leaf(green, Vector3.UnitZ, near);
                    float width = YoungLeaf!.Reach * length;
                    Vector2 stop = at + direction * length;
                    if (OnCard(stop)) canvas.Later(near - 0.02f, () => canvas.Leaf(young, at, stop, width, leafInk));
                    continue;
                }
                Flower(canvas, random, petals, at, near);
            }
        }

        private void Flower(CardCanvas canvas, WorldRandom random, LeafImage[] petals, Vector2 at, float near)
        {
            float size = FlowerSize * random.Range(0.75f, 1.15f);
            float turn = random.Range(0f, MathF.Tau);
            float squash = random.Range(0.6f, 1f);      // Flowers face all ways, and most aren't looking at you.
            float squashAngle = random.Range(0f, MathF.PI);
            Vector3 normal = new Vector3(random.Range(-0.5f, 0.5f), random.Range(-0.5f, 0.5f), 1f).Normalized();
            float shade = random.Range(0.86f, 1.04f) * (0.74f + 0.26f * near);
            CardInk ink = CardInk.Leaf(new Vector3(shade), normal, near);
            ink.Translucency = 0.85f;
            ink.Roughness = 0.72f;

            Vector2 Skew(Vector2 v)
            {
                Vector2 axis = new Vector2(MathF.Cos(squashAngle), MathF.Sin(squashAngle));
                float along = Vector2.Dot(v, axis);
                return v - axis * (along * (1f - squash));
            }

            for (int p = 0; p < PetalCount; p++)
            {
                float angle = turn + p * (MathF.Tau / PetalCount);
                Vector2 direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                Vector2 tip = at + Skew(direction * size);
                float width = Petal.Reach * size * 0.95f;
                LeafImage petal = petals[p % petals.Length];
                canvas.Later(near, () => canvas.Leaf(petal, at, tip, width, ink, 0.3f, 0.5f));
            }
            CardInk heart = CardInk.Leaf(Heart, normal, near);
            canvas.Later(near + 0.005f, () => canvas.Stroke(at, at + new Vector2(0.0005f, 0f), size * 0.17f, size * 0.17f, heart));
        }
    }

    /// <summary>Bare twigs, forking: what's left, or what winter does.</summary>
    public sealed class BareTwigs : CardForm
    {
        public int Depth = 4;
        public float Thickness = 0.008f;
        public float Angle = 0.55f;
        public Vector3 Colour = new Vector3(0.16f, 0.13f, 0.11f);

        public override void Paint(CardCanvas canvas, Genes genes)
        {
            WorldRandom random = genes.Stream("twigs");
            Fork(canvas, random, new Vector2(0.5f, 0.015f), new Vector2(random.Range(-0.15f, 0.15f), 1f).Normalized(), 0.36f, Thickness, Depth);
        }

        private void Fork(CardCanvas canvas, WorldRandom random, Vector2 from, Vector2 heading, float length, float radius, int depth)
        {
            Vector2 at = from;
            const int Steps = 4;
            for (int i = 0; i < Steps; i++)
            {
                Vector2 next = at + heading * (length / Steps);
                if (!OnCard(next)) return;
                Vector2 a = at, b = next;
                float ra = radius * (1f - 0.1f * i), rb = radius * (1f - 0.1f * (i + 1));
                float near = random.Range(0.3f, 0.9f);
                canvas.Later(near, () => canvas.Stroke(a, b, ra, rb, CardInk.Wood(Colour * (0.75f + 0.4f * near), near)));
                at = next;
                heading = Turn(heading, random.Range(-0.28f, 0.28f));
            }
            if (depth <= 0) return;
            int forks = random.Float() < 0.3f ? 3 : 2;
            for (int i = 0; i < forks; i++)
            {
                float spread = (i - (forks - 1) * 0.5f) * Angle * 2f / MathF.Max(forks - 1, 1) * random.Range(0.7f, 1.15f);
                Fork(canvas, random, at, Turn(heading, spread), length * random.Range(0.6f, 0.82f), radius * 0.68f, depth - 1);
            }
        }
    }

    /// <summary>
    /// A whole compound leaf, stem up the middle and leaflets down both sides: ferns, palms, cycads. A bigger plant uses it as the blade of a bent strip, so it's painted to fill the
    /// card whatever its proportions, and its width is left to the strip.
    /// </summary>
    public sealed class PinnateFrond : CardForm
    {
        public LeafForm Pinna = new LeafForm { Width = 0.22f, Skew = 0.8f, Round = 0.6f, Teeth = 0, Veins = 0, VeinStrength = 0f, Mottle = 0.1f };
        public int Pairs = 26;
        public float Longest = 0.46f;           // How far a leaflet reaches, as a share of the card.
        public float Skew = 0.72f;              // Where along the frond the longest are.
        public float Slope = 1.2f;              // Radians off the rachis at the foot, closing to about half that at the tip.
        public float Reduce = 0.75f;            // How fast leaflets shrink towards the tip.
        public int Pinnules;                    // Leaflets on the leaflets: a fern's are twice divided.
        public float Gap = 0.42f;               // How much of the space along the rachis is air between leaflets.
        public Vector3 Rachis = new Vector3(0.3f, 0.3f, 0.12f);
        public float Translucency = 0.7f;
        public float Roughness = 0.72f;

        public override void Paint(CardCanvas canvas, Genes genes)
        {
            WorldRandom random = genes.Stream("frond");
            var pinnae = new LeafImage[2];
            for (int i = 0; i < pinnae.Length; i++) pinnae[i] = Pinna.Paint(96, (uint)random.NextULong());

            Vector2 foot = new Vector2(0.5f, 0.012f);
            Vector2 tip = new Vector2(0.5f + random.Range(-0.012f, 0.012f), 0.985f);
            canvas.Later(0.7f, () => canvas.Stroke(foot, tip, 0.0085f, 0.002f, CardInk.Wood(Rachis, 0.7f)));
            float spacing = 0.93f / Pairs;

            for (int i = 0; i < Pairs; i++)
            {
                float t = 0.05f + 0.93f * (i + 0.4f) / Pairs;
                float profile = MathF.Pow(MathF.Sin(MathF.PI * MathF.Pow(t, Skew)), Reduce);
                float length = Longest * MathF.Max(profile, 0.05f);
                float angle = Slope * (1f - 0.5f * t);
                Vector2 at = Vector2.Lerp(foot, tip, t);
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector2 heading = Turn(Vector2.UnitY, -side * angle * random.Range(0.94f, 1.06f));
                    float near = Math.Clamp(0.5f + random.Range(-0.2f, 0.2f), 0f, 1f);
                    float shade = random.Range(0.84f, 1.03f) * (0.76f + 0.24f * near);
                    Vector3 normal = new Vector3(random.Range(-0.2f, 0.2f) + side * 0.22f, random.Range(-0.2f, 0.2f), 1f).Normalized();
                    CardInk ink = CardInk.Leaf(new Vector3(shade), normal, near);
                    ink.Translucency = Translucency;
                    ink.Roughness = Roughness;

                    if (Pinnules > 0) Divided(canvas, random, pinnae, at, heading, length, near, ink, spacing);
                    else
                    {
                        Vector2 end = at + heading * length;
                        if (!OnCard(end, 0.004f)) end = Inside(end, 0.006f);
                        LeafImage image = pinnae[random.Int(pinnae.Length)];
                        float width = spacing * 0.5f * (1f - Gap) / MathF.Max(MathF.Sin(angle), 0.4f);
                        canvas.Later(near, () => canvas.Leaf(image, at, end, width, ink, 0.25f, 0.15f, side * 0.06f));
                    }
                }
            }
        }

        private void Divided(CardCanvas canvas, WorldRandom random, LeafImage[] pinnae, Vector2 at, Vector2 heading, float length, float near, CardInk ink, float spacing)
        {
            Vector2 end = Inside(at + heading * length, 0.006f);
            canvas.Later(near - 0.01f, () => canvas.Stroke(at, end, 0.0028f, 0.0009f, CardInk.Wood(Rachis, near)));
            for (int i = 0; i < Pinnules; i++)
            {
                float t = (i + 0.5f) / Pinnules;
                float size = MathF.Min(length * 0.28f, spacing * 0.5f) * MathF.Pow(1f - t, 0.55f) + length * 0.015f;
                Vector2 root = Vector2.Lerp(at, end, t);
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector2 stop = root + Turn(heading, side * 0.95f) * size;
                    if (!OnCard(stop, 0.004f)) continue;
                    LeafImage image = pinnae[random.Int(pinnae.Length)];
                    float width = length / Pinnules * 0.5f * (1f - Gap * 0.5f);
                    canvas.Later(near, () => canvas.Leaf(image, root, stop, width, ink, 0.2f, 0.1f));
                }
            }
        }
    }
}

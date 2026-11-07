using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Flora
{
    /// <summary>A flower looking straight up, or a cluster of small ones, filling the card. It's what the camera sees of a flower: nobody in this game looks at one from the side.</summary>
    public sealed class FlowerHead : CardForm
    {
        public LeafForm Petal = new LeafForm { Blade = LeafBlade.Petal, Width = 0.34f, Skew = 1.3f, Round = 0.75f, Veins = 0, VeinStrength = 0f, Mottle = 0.08f, Tone = new Vector3(1f, 0.98f, 0.96f) };
        public int Petals = 9;
        public int Whorls = 2;                  // Rings of petals, the inner shorter and turned between the outer.
        public float Length = 0.46f;
        public float Heart = 0.11f;             // The size of its middle.
        public Vector3 HeartColour = new Vector3(0.75f, 0.55f, 0.12f);
        public int Clustered = 1;               // Above 1, small flowers in a head: hydrangea, yarrow, azalea.
        public float Fan = 0f;                  // Above 0 the petals curl up and out: spider lilies.

        public override void Paint(CardCanvas canvas, Genes genes)
        {
            WorldRandom random = genes.Stream("flower");
            var petals = new LeafImage[2];
            for (int i = 0; i < petals.Length; i++) petals[i] = Petal.Paint(96, (uint)random.NextULong());

            if (Clustered <= 1)
            {
                Bloom(canvas, random, petals, new Vector2(0.5f), Length, 0.6f);
                return;
            }
            for (int i = 0; i < Clustered; i++)
            {
                // Sunflower seeds: Vogel's model is how anything packs into a disc.
                float t = (i + 0.5f) / Clustered;
                float angle = i * 2.39996f;
                Vector2 at = new Vector2(0.5f) + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (MathF.Sqrt(t) * 0.36f);
                Bloom(canvas, random, petals, at, Length / MathF.Sqrt(Clustered) * 1.5f * random.Range(0.85f, 1.12f), 1f - t * 0.5f);
            }
        }

        private void Bloom(CardCanvas canvas, WorldRandom random, LeafImage[] petals, Vector2 at, float length, float near)
        {
            for (int whorl = 0; whorl < Whorls; whorl++)
            {
                float own = length * (1f - whorl * 0.26f);
                float turn = random.Range(0f, MathF.Tau);
                float depth = near + whorl * 0.05f;
                float shade = 0.82f + whorl * 0.14f;
                for (int p = 0; p < Petals; p++)
                {
                    float angle = turn + p * (MathF.Tau / Petals) + random.Range(-0.08f, 0.08f);
                    Vector2 direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    Vector2 tip = at + direction * (own * random.Range(0.9f, 1.06f));
                    if (!OnCard(tip, 0.004f)) tip = Inside(tip, 0.006f);
                    Vector3 normal = new Vector3(direction.X * 0.4f, direction.Y * 0.4f, 1f).Normalized();
                    CardInk ink = CardInk.Leaf(new Vector3(shade * random.Range(0.94f, 1.02f)), normal, depth);
                    ink.Translucency = 0.85f;
                    ink.Roughness = 0.72f;
                    LeafImage petal = petals[p % petals.Length];
                    float width = Petal.Reach * own;
                    float bend = Fan * (p % 2 == 0 ? 1f : -1f);
                    canvas.Later(depth + p * 0.0005f, () => canvas.Leaf(petal, at, tip, width, ink, 0.3f, 0.45f, bend));
                }
            }

            int seeds = 18;
            for (int i = 0; i < seeds; i++)
            {
                float a = i * 2.39996f;
                float r = MathF.Sqrt((i + 0.5f) / seeds) * length * Heart * 2.2f;
                Vector2 dot = at + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
                CardInk ink = CardInk.Leaf(HeartColour * random.Range(0.8f, 1.1f), Vector3.UnitZ, near + 0.2f);
                float size = length * Heart * 0.5f;
                canvas.Later(near + 0.2f, () => canvas.Stroke(dot, dot + new Vector2(0.0006f, 0f), size, size, ink));
            }
        }
    }

    /// <summary>Flowers up a stem, or hanging from one: lupine, lavender, foxglove, wisteria. The foot of the card is where it's held.</summary>
    public sealed class FlowerSpike : CardForm
    {
        public LeafForm Floret = new LeafForm { Blade = LeafBlade.Petal, Width = 0.5f, Skew = 1.2f, Round = 0.7f, Veins = 0, VeinStrength = 0f, Mottle = 0.1f, Tone = new Vector3(1f, 0.97f, 1f) };
        public int Florets = 46;
        public float Size = 0.1f;
        public float Width = 0.26f;
        public int Strands = 1;
        public Vector3 Stalk = new Vector3(0.3f, 0.32f, 0.14f);

        public override void Paint(CardCanvas canvas, Genes genes)
        {
            WorldRandom random = genes.Stream("spike");
            LeafImage floret = Floret.Paint(64, (uint)random.NextULong());
            for (int strand = 0; strand < Strands; strand++)
            {
                float x = Strands > 1 ? 0.5f + ((strand + 0.5f) / Strands - 0.5f) * 0.7f : 0.5f;
                float reach = Strands > 1 ? random.Range(0.7f, 0.98f) : 0.96f;
                Vector2 foot = new Vector2(x, 0.012f);
                Vector2 tip = new Vector2(x + random.Range(-0.03f, 0.03f), reach);
                canvas.Later(0.3f, () => canvas.Stroke(foot, tip, 0.006f, 0.002f, CardInk.Wood(Stalk, 0.3f)));

                for (int i = 0; i < Florets / Strands; i++)
                {
                    float t = MathF.Pow(random.Float(), 0.8f);
                    float fade = 1f - t * 0.72f;
                    float spread = Width * fade / MathF.Max(Strands * 0.55f, 1f);
                    Vector2 at = Vector2.Lerp(foot, tip, 0.05f + 0.95f * t) + new Vector2(random.Range(-1f, 1f) * spread, 0f);
                    Vector2 direction = Turn(new Vector2(0f, -1f), random.Range(-1.4f, 1.4f));
                    float size = Size * fade * random.Range(0.8f, 1.15f);
                    Vector2 end = at + direction * size;
                    if (!OnCard(end, 0.006f) || !OnCard(at, 0.006f)) continue;

                    float near = random.Range(0.2f, 0.95f);
                    float shade = random.Range(0.8f, 1.04f) * (0.7f + 0.3f * near);
                    Vector3 normal = new Vector3(random.Range(-0.5f, 0.5f), random.Range(-0.5f, 0.5f), 1f).Normalized();
                    CardInk ink = CardInk.Leaf(new Vector3(shade), normal, near);
                    ink.Translucency = 0.8f;
                    float width = Floret.Reach * size;
                    canvas.Later(near, () => canvas.Leaf(floret, at, end, width, ink, 0.25f, 0.4f));
                }
            }
        }
    }

    /// <summary>Leaves that have come down, lying any way up: the carpet under a maple. It's a picture of a drift, laid on the ground.</summary>
    public sealed class LeafScatter : CardForm
    {
        public LeafForm Leaf = new LeafForm();
        public int Leaves = 90;
        public float LeafLength = 0.1f;

        public override void Paint(CardCanvas canvas, Genes genes)
        {
            WorldRandom random = genes.Stream("litter");
            var leaves = new LeafImage[3];
            for (int i = 0; i < leaves.Length; i++) leaves[i] = Leaf.Paint(128, (uint)random.NextULong());

            for (int i = 0; i < Leaves; i++)
            {
                // Thick in the middle and thinning to nothing, so that a drift has no edge.
                float angle = random.Float() * MathF.Tau;
                float distance = MathF.Pow(random.Float(), 0.65f) * 0.44f;
                Vector2 at = new Vector2(0.5f) + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
                Vector2 direction = Turn(Vector2.UnitY, random.Range(0f, MathF.Tau));
                float length = LeafLength * random.Range(0.75f, 1.2f);
                Vector2 tip = at + direction * length;
                if (!OnCard(tip) || !OnCard(at)) continue;

                float near = random.Float();
                float shade = random.Range(0.62f, 1.04f) * (0.62f + 0.38f * near);
                Vector3 colour = new Vector3(shade + random.Range(-0.08f, 0.1f), shade, shade + random.Range(-0.14f, 0.02f));
                Vector3 normal = new Vector3(random.Range(-0.3f, 0.3f), random.Range(-0.3f, 0.3f), 1f).Normalized();
                CardInk ink = CardInk.Leaf(colour, normal, near);
                ink.Translucency = 0.1f;
                ink.Roughness = 0.82f;
                LeafImage leaf = leaves[random.Int(leaves.Length)];
                float reach = Leaf.Reach * length * random.Range(0.75f, 1f);
                float bend = random.Range(-0.15f, 0.15f);
                canvas.Later(near, () => canvas.Leaf(leaf, at, tip, reach, ink, 0.4f, 0.3f, bend));
            }
        }
    }

    /// <summary>A cushion of something fine: moss, lichen, a mat of creeping thyme. Thousands of small dabs, thick in the middle.</summary>
    public sealed class Cushion : CardForm
    {
        public int Dabs = 2600;
        public float Size = 0.011f;
        public int Lobes = 5;           // How broken up its outline is.

        public override void Paint(CardCanvas canvas, Genes genes)
        {
            WorldRandom random = genes.Stream("cushion");
            uint seed = (uint)random.NextULong();
            for (int i = 0; i < Dabs; i++)
            {
                float angle = random.Float() * MathF.Tau;
                float distance = MathF.Sqrt(random.Float()) * 0.46f;
                Vector2 at = new Vector2(0.5f) + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
                float edge = 0.3f + 0.2f * FloraNoise.Wave(angle * Lobes / MathF.Tau * 3f, seed);
                if (distance > edge + 0.14f * FloraNoise.Value(new Vector3(at.X * 9f, at.Y * 9f, 0.5f), seed)) continue;

                float near = random.Float();
                float tone = FloraNoise.Fbm(new Vector3(at.X * 14f, at.Y * 14f, 1.5f), seed + 7u, 3);
                float shade = (0.5f + 0.8f * tone) * (0.7f + 0.3f * near);
                Vector3 normal = new Vector3((at.X - 0.5f) * 1.6f + random.Range(-0.3f, 0.3f), (at.Y - 0.5f) * 1.6f + random.Range(-0.3f, 0.3f), 1f).Normalized();
                CardInk ink = CardInk.Leaf(new Vector3(shade), normal, near);
                ink.Translucency = 0.2f;
                ink.Roughness = 0.85f;
                float size = Size * random.Range(0.7f, 1.4f);
                canvas.Later(near, () => canvas.Stroke(at, at + new Vector2(0.0006f, 0f), size, size * 0.7f, ink));
            }
        }
    }

    /// <summary>A soft head of strands, from a stem up the middle: pampas, reed, foxtail.</summary>
    public sealed class Plume : CardForm
    {
        public int Strands = 240;
        public float Length = 0.34f;
        public float Spread = 0.5f;
        public float Thickness = 0.0034f;

        public override void Paint(CardCanvas canvas, Genes genes)
        {
            WorldRandom random = genes.Stream("plume");
            for (int i = 0; i < Strands; i++)
            {
                float t = MathF.Pow(random.Float(), 0.75f);
                Vector2 foot = new Vector2(0.5f + random.Range(-0.02f, 0.02f), 0.02f + 0.72f * t);
                float side = random.Range(-1f, 1f);
                float fade = MathF.Sin(MathF.PI * MathF.Min(t * 0.88f + 0.1f, 1f));
                Vector2 heading = Turn(Vector2.UnitY, -side * Spread * (1f - 0.5f * t));
                float length = Length * fade * random.Range(0.6f, 1.15f);
                Vector2 at = foot;
                float curl = random.Range(-0.2f, 0.2f) - side * 0.15f;
                float near = random.Float();
                float shade = random.Range(0.72f, 1.05f) * (0.7f + 0.3f * near);
                Vector3 normal = new Vector3(side * 0.5f, random.Range(-0.3f, 0.3f), 1f).Normalized();
                CardInk ink = CardInk.Leaf(new Vector3(shade), normal, near);
                ink.Translucency = 0.9f;
                ink.Roughness = 0.8f;

                const int Steps = 4;
                for (int s = 0; s < Steps; s++)
                {
                    Vector2 next = Inside(at + heading * (length / Steps), 0.01f);
                    Vector2 a = at, b = next;
                    float ra = Thickness * (1f - s * 0.2f), rb = Thickness * (1f - (s + 1) * 0.2f);
                    canvas.Later(near, () => canvas.Stroke(a, b, ra, rb, ink));
                    at = next;
                    heading = Turn(heading, curl);
                }
            }
        }
    }

    /// <summary>One big leaf, filling the card: a banana, a hosta, a lotus.</summary>
    public sealed class BroadLeaf : CardForm
    {
        public LeafForm Leaf = new LeafForm { Width = 0.42f, Skew = 0.9f, Round = 0.7f, Veins = 16, VeinAngle = 0.5f, VeinStrength = 0.4f, Mottle = 0.1f };
        public float Tatters;       // Banana leaves split along their veins in the wind.

        public override void Paint(CardCanvas canvas, Genes genes)
        {
            WorldRandom random = genes.Stream("leaf");
            LeafImage leaf = Leaf.Paint(CardLibrary.CellSize, (uint)random.NextULong());
            CardInk ink = CardInk.Leaf(new Vector3(1f), Vector3.UnitZ, 1f);
            ink.Translucency = 0.78f;
            ink.Roughness = 0.62f;
            canvas.Later(0.5f, () => canvas.Leaf(leaf, new Vector2(0.5f, 0.004f), new Vector2(0.5f, 0.996f), 0.5f, ink, 0.25f, 0.2f));
            canvas.Finish();

            if (Tatters <= 0f) return;
            // Tears run in from the edge, along the veins, and take the alpha out.
            for (int i = 0; i < (int)(Tatters * 26f); i++)
            {
                float y = random.Range(0.08f, 0.95f);
                float side = random.Float() < 0.5f ? -1f : 1f;
                float x = 0.5f + side * random.Range(0.1f, 0.2f);
                float reach = random.Range(0.25f, 0.5f);
                Tear(canvas, new Vector2(x, y), new Vector2(0.5f + side * 0.55f, y + reach * 0.5f));
            }
        }

        private static void Tear(CardCanvas canvas, Vector2 from, Vector2 to)
        {
            int size = canvas.Size;
            int steps = (int)((to - from).Length * size) + 1;
            for (int s = 0; s <= steps; s++)
            {
                Vector2 at = Vector2.Lerp(from, to, s / (float)steps);
                int cx = (int)(at.X * size), cy = (int)(at.Y * size);
                for (int y = cy - 1; y <= cy + 1; y++)
                {
                    for (int x = cx - 1; x <= cx + 1; x++)
                    {
                        if (x < 0 || y < 0 || x >= size || y >= size) continue;
                        int index = (y * size + x) * CardCanvas.Channels;
                        for (int c = 0; c < CardCanvas.Channels; c++) canvas.Data[index + c] = 0f;
                    }
                }
            }
        }
    }

    /// <summary>Fine strands hanging: lichen on a dead tree, or moss on one that's only sad.</summary>
    public sealed class HangingStrands : CardForm
    {
        public int Strands = 70;
        public float Thickness = 0.0034f;

        public override void Paint(CardCanvas canvas, Genes genes)
        {
            WorldRandom random = genes.Stream("strands");
            for (int i = 0; i < Strands; i++)
            {
                float x = 0.5f + random.Range(-0.36f, 0.36f) * random.Float();
                float length = random.Range(0.35f, 0.96f) * (1f - MathF.Abs(x - 0.5f));
                Vector2 at = new Vector2(x, 0.01f);
                float near = random.Float();
                float shade = random.Range(0.72f, 1.04f) * (0.72f + 0.28f * near);
                Vector3 normal = new Vector3(random.Range(-0.4f, 0.4f), random.Range(-0.2f, 0.2f), 1f).Normalized();
                CardInk ink = CardInk.Leaf(new Vector3(shade), normal, near);
                ink.Translucency = 0.5f;
                ink.Roughness = 0.85f;
                const int Steps = 7;
                float wander = random.Range(-0.04f, 0.04f);
                for (int s = 0; s < Steps; s++)
                {
                    Vector2 next = Inside(at + new Vector2(wander + random.Range(-0.012f, 0.012f), length / Steps), 0.006f);
                    Vector2 a = at, b = next;
                    float ra = Thickness * (1f - s * 0.1f), rb = Thickness * (1f - (s + 1) * 0.1f);
                    canvas.Later(near, () => canvas.Stroke(a, b, ra, rb, ink));
                    at = next;
                }
            }
        }
    }
}

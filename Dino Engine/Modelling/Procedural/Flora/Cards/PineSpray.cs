using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Flora
{
    /// <summary>
    /// The end of a pine's bough, seen from above, which is what a pine's crown is made of: pads of needles at the ends of its limbs with air between them. A twig grows a year at a
    /// time and forks at every year's node into shoots that fan out flat, and those fork again. A shoot keeps its needles for only its last few years, so the wood behind them is bare and
    /// the needles are all out at the ends, and every shoot finishes in a brush of this year's needles round its bud. Needles come in pairs from one sheath, as a two-needled pine's do, stand
    /// out wider as they age, and go round the shoot, so what's seen from above is a bottle brush: those pointing up are in front of the shoot and short, as they're seen end on, and those
    /// pointing down are behind it and in its shade. Sizes are a real pine's, a needle a few centimetres on a card a couple of metres long, so up close a pad reads as needles and from
    /// the game's camera as the fringed masses a pine's crown is.
    /// </summary>
    public sealed class PineSpray : CardForm
    {
        public IntRange Years = new IntRange(7, 8);    // Years along the main twig, foot to tip.
        public float Growth = 0.1f;                 // A year's growth along the main twig, as a share of the card.
        public float LateralGrowth = 0.85f;         // A side shoot's year against what it grew from.
        public IntRange Laterals = new IntRange(2, 3);  // Side shoots in the whorl at each of the main twig's nodes.
        public IntRange SubLaterals = new IntRange(0, 2);   // And at each node of those.
        public float LateralAngle = 0.95f;          // Radians off the shoot they leave, for the two out to its sides; the rest reach on ahead.
        public int Orders = 3;                      // The twig, its shoots, and theirs.
        public int NeedleYears = 4;                 // How many years a shoot keeps its needles. Behind that it's bare.
        public float Needle = 0.06f;                // As a share of the card: as long as most of a year's growth, as a pine's needles are.
        public float NeedleRadius = 0.0012f;        // A little over a texel wide, so the fringe of a brush survives the cutoff.
        public float NeedleAngle = 0.62f;           // Radians off the shoot for this year's, swept forward into a tuft.
        public float Opening = 0.45f;               // How much wider than that the oldest stand.
        public float Density = 420f;                // Pairs to a card's length of shoot.
        public float ShadedOut = 0.45f;             // The chance of one of the oldest side shoots, deep in the crown, being dead and bare.
        public Vector3 Twig = new Vector3(0.24f, 0.15f, 0.09f);
        public Vector3 Bud = new Vector3(0.34f, 0.19f, 0.1f);
        public float Translucency = 0.42f;
        public float Roughness = 0.6f;              // Waxy: a pine's needles have a bloom on them.

        public override void Paint(CardCanvas canvas, Genes genes)
        {
            WorldRandom random = genes.Stream("pine");
            int years = random.Int(Years);
            Vector2 foot = new Vector2(0.5f + random.Range(-0.03f, 0.03f), 0.02f);
            Vector2 heading = new Vector2(random.Range(-0.07f, 0.07f), 1f).Normalized();
            float growth = MathF.Min(Growth, 0.86f / years);
            Shoot(canvas, random, foot, heading, years, 0, 0.5f, growth, 0.0085f, 0f, years);
        }

        // A shoot `age` years old from `from`: a year's wood at a time, side shoots at every node, needles on its last few years, and a bud in a brush at the end. `side` is which way it
        // left what it grew from, so it can curl gently back the way that went, as a pine's side shoots do.
        private void Shoot(CardCanvas canvas, WorldRandom random, Vector2 from, Vector2 heading, int age, int order, float depth, float growth, float radius, float side, int twigYears)
        {
            Vector2 at = from;
            float curl = random.Range(-0.1f, 0.1f) - side * 0.07f;
            float tipRadius = radius * 0.38f;
            for (int year = 0; year < age; year++)
            {
                int fromTip = age - year;   // 1 for this year's wood.
                float length = growth * random.Range(0.82f, 1.16f);
                Vector2 next = at + heading * length;
                if (!OnCard(next, 0.03f))
                {
                    // What would run off the card stops here, in a brush, as a shoot stopped by its neighbours does.
                    if (year > 0) Tuft(canvas, random, at, heading, depth);
                    return;
                }

                float rFrom = MathHelper.Lerp(tipRadius, radius, (float)fromTip / age);
                float rTo = MathHelper.Lerp(tipRadius, radius, (fromTip - 1f) / age);
                Vector2 a = at, b = next;
                float woodShade = 0.8f + 0.25f * random.Float();
                canvas.Later(depth - 0.07f, () => canvas.Stroke(a, b, rFrom, rTo, CardInk.Wood(Twig * woodShade, Math.Clamp(depth, 0f, 1f))));
                if (fromTip <= NeedleYears) Needles(canvas, random, a, b, fromTip, depth);

                at = next;
                heading = Turn(heading, curl * random.Range(0.6f, 1.4f));

                // A node: this year's buds round the tip became next year's shoots. The last year's are still buds.
                if (year >= age - 1 || order >= Orders - 1) continue;
                int count = order == 0 ? random.Int(Laterals) : random.Int(SubLaterals);
                int lateralAge = age - year - 1;
                float start = random.Float() < 0.5f ? 1f : -1f;
                for (int l = 0; l < count; l++)
                {
                    // Deep in the crown the oldest side shoots have been shaded out, and what's left of them is a bare spike.
                    bool old = order == 0 && year < twigYears - NeedleYears - 1;
                    if (old && random.Float() < ShadedOut)
                    {
                        Vector2 stub = Turn(heading, (l % 2 == 0 ? start : -start) * LateralAngle * random.Range(0.8f, 1.2f)) * growth * random.Range(0.3f, 0.8f);
                        Vector2 end = at + stub;
                        if (OnCard(end, 0.03f))
                        {
                            Vector2 c = at;
                            canvas.Later(depth - 0.08f, () => canvas.Stroke(c, end, radius * 0.4f, radius * 0.15f, CardInk.Wood(Twig * 0.85f, Math.Clamp(depth - 0.1f, 0f, 1f))));
                        }
                        continue;
                    }

                    // A whorl: out to either side, as a bough holds its shoots to the light, one or two between them reaching on ahead, and now and then one standing up out of
                    // the pad or hanging under it, seen shorter.
                    float sign = l % 2 == 0 ? start : -start;
                    float angle = LateralAngle * (l < 2 ? random.Range(0.62f, 1.05f) : random.Range(0.12f, 0.55f));
                    float lift = random.Range(-0.7f, 0.9f);
                    float shortened = MathF.Cos(lift);
                    Shoot(canvas, random, at, Turn(heading, sign * angle), lateralAge, order + 1, depth + MathF.Sin(lift) * 0.16f + random.Range(-0.03f, 0.03f), growth * LateralGrowth * shortened, radius * 0.6f, sign, twigYears);
                }
            }
            Tuft(canvas, random, at, heading, depth);
        }

        // Pairs of needles all the way round a year's wood. Seen from above, one standing up from the shoot is in front of it and short, as it's end on; one hanging below is
        // behind it and in its shade; those out to the sides are seen at their full length. Older needles stand wider, and some have fallen.
        private void Needles(CardCanvas canvas, WorldRandom random, Vector2 from, Vector2 to, int fromTip, float depth)
        {
            Vector2 line = to - from;
            float length = line.Length;
            if (length < 1e-4f) return;
            Vector2 axis = line / length;
            Vector2 across = new Vector2(-axis.Y, axis.X);
            float age = (fromTip - 1f) / MathF.Max(NeedleYears - 1, 1);
            float kept = 1f - 0.28f * age;
            int pairs = (int)(Density * length * random.Range(0.88f, 1.12f));
            for (int i = 0; i < pairs; i++)
            {
                if (random.Float() > kept) continue;
                Vector2 root = Vector2.Lerp(from, to, random.Float());
                float around = random.Float() * MathF.Tau;
                float open = NeedleAngle * (1f + Opening * age) * random.Range(0.8f, 1.2f);
                float spread = random.Range(0.14f, 0.32f);
                float needle = Needle * random.Range(0.84f, 1.14f) * (fromTip == 1 ? 0.9f : 1f);
                for (int n = 0; n < 2; n++)
                {
                    Draw(canvas, random, root, axis, across, around + (n == 0 ? -spread : spread), open, needle, depth, age);
                }
            }
        }

        // This year's needles round the bud, gathered forward into the brush a pine's shoot ends in, and the bud itself among them.
        private void Tuft(CardCanvas canvas, WorldRandom random, Vector2 at, Vector2 heading, float depth)
        {
            Vector2 across = new Vector2(-heading.Y, heading.X);
            int pairs = (int)(Density * Needle * 2.2f);
            for (int i = 0; i < pairs; i++)
            {
                Vector2 root = at - heading * (Needle * random.Range(0f, 0.5f));
                float around = random.Float() * MathF.Tau;
                float open = NeedleAngle * random.Range(0.35f, 0.85f);
                float spread = random.Range(0.12f, 0.28f);
                float needle = Needle * random.Range(0.62f, 0.92f);
                for (int n = 0; n < 2; n++) Draw(canvas, random, root, heading, across, around + (n == 0 ? -spread : spread), open, needle, depth + 0.02f, 0f);
            }

            Vector2 tip = at + heading * (Needle * 0.32f);
            if (!OnCard(tip)) return;
            CardInk bud = CardInk.Wood(Bud * random.Range(0.85f, 1.1f), Math.Clamp(depth + 0.04f, 0f, 1f));
            bud.Roughness = 0.5f;
            float budRadius = NeedleRadius * 2.6f;
            canvas.Later(depth + 0.035f, () => canvas.Stroke(at, tip, budRadius, budRadius * 0.55f, bud));
        }

        // One needle, from its sheath at `root`, `open` off the shoot and `around` it from the side of the card.
        private void Draw(CardCanvas canvas, WorldRandom random, Vector2 root, Vector2 axis, Vector2 across, float around, float open, float length, float depth, float age)
        {
            float sine = MathF.Sin(open), cosine = MathF.Cos(open);
            float side = MathF.Cos(around), up = MathF.Sin(around);
            Vector2 flat = axis * cosine + across * (sine * side);
            float rise = sine * up;     // Towards whoever is looking down on the card.
            Vector2 tip = root + flat * length;
            if (!OnCard(tip) || flat.LengthSquared < 1e-4f) return;

            float near = depth + rise * 0.07f;
            float clamped = Math.Clamp(near, 0f, 1f);
            // Lit from above: what stands up out of the brush is in the light, what hangs under it is in the shade of the shoot and the needles over it. Older needles have lost
            // some of their green, and a few carry more of the waxy bloom that makes a pine blue-grey.
            // What carries a pad from far off, where needles are long gone into the mips, is its tufts: this year's needles bright at the tip of every shoot, over the older ones
            // inside, which are darker in themselves and in the shade of all that's grown past them.
            float light = 0.66f + 0.34f * (0.5f + 0.5f * up);
            float shade = random.Range(0.84f, 1.03f) * light * (1f - 0.24f * age);
            Vector3 tone = new Vector3(0.95f, 1f, 0.92f);
            tone = Vector3.Lerp(tone, new Vector3(1f, 0.98f, 0.82f), age * 0.45f);
            if (random.Float() < 0.25f) tone *= new Vector3(0.95f, 0.99f, 1.04f);

            // The needle's own surface, turned to the eye: the view, less the part of it along the needle.
            Vector3 direction = new Vector3(flat.X, flat.Y, rise).Normalized();
            Vector3 normal = Vector3.UnitZ - direction * direction.Z;
            normal = normal.LengthSquared > 1e-4f ? normal.Normalized() : Vector3.UnitZ;

            CardInk ink = CardInk.Leaf(tone * shade, normal, clamped);
            ink.Translucency = Translucency * (1.1f - 0.3f * age);
            ink.Roughness = Roughness;
            ink.Ambient = Math.Clamp(0.42f + 0.36f * light + 0.22f * clamped - 0.3f * age, 0f, 1f);
            float radius = NeedleRadius * random.Range(0.9f, 1.1f);
            canvas.Later(near, () => canvas.Stroke(root, tip, radius, radius * 0.45f, ink));
        }
    }
}

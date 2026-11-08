using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Fence
{
    /// <summary>One model of a fence being built: what it is, the style's genes that every piece of it shares, its own, and how long a span of that style is.</summary>
    internal readonly record struct FenceJob(FenceBuilder B, FenceSpec Spec, Genes Style, Genes Own, float Bay)
    {
        public bool IsSpan => Spec.Piece == FencePiece.Span;
        public bool IsEnd => Spec.Piece == FencePiece.End;
        public float Sink => Spec.Kind.Sink;
    }

    /// <summary>
    /// Builds fences. A style is a seed, and its genes say everything that pieces of it have to agree on, as how high the rails are and how thick the wall: so any post meets any
    /// span of its style. What's a piece's own, its stones and its knots, comes from genes of its own, and a span's differ with the slope it's built for, so a run over uneven
    /// ground never repeats. Every number comes from <see cref="Genes"/> by a name of its own. Safe on any thread: it knows nothing of the game and touches no GPU.
    /// </summary>
    public static class FenceForge
    {
        /// <summary>Metres from post to post in a style, which <see cref="FenceLayout"/> lays its runs out by.</summary>
        public static float BayOf(FenceKind kind, ulong style) => new Genes(style).Part(kind.Id).Range("bay", kind.Bay);

        public static FloraMesh Make(FenceSpec spec)
        {
            Genes style = new Genes(spec.Style).Part(spec.Kind.Id);
            Genes own = style.Part(spec.Piece.ToString()).Part(spec.Variant).Part(spec.Slope + 4096);
            var b = new FenceBuilder { Slope = spec.Gradient };
            var job = new FenceJob(b, spec, style, own, BayOf(spec.Kind, spec.Style));
            switch (spec.Kind.Form)
            {
                case FenceForm.Rails: TimberFences.Rails(job); break;
                case FenceForm.Paling: TimberFences.Paling(job); break;
                case FenceForm.Wattle: TimberFences.Wattle(job); break;
                case FenceForm.Bamboo: BambooFence.Build(job); break;
                case FenceForm.Stockade: LogFences.Stockade(job); break;
                case FenceForm.Pen: LogFences.Pen(job); break;
                case FenceForm.DryStone: StoneFences.DryStone(job); break;
                case FenceForm.Churchyard: StoneFences.Churchyard(job); break;
                case FenceForm.Roofed: StoneFences.Roofed(job); break;
                case FenceForm.Curtain: StoneFences.Curtain(job); break;
            }
            return b.Finish();
        }
    }

    /// <summary>Colours and shapes more than one kind of fence shares.</summary>
    internal static class FenceLook
    {
        public static readonly Vector3 Cord = new Vector3(0.44f, 0.37f, 0.26f);
        public static readonly Vector3 Iron = new Vector3(0.15f, 0.15f, 0.16f);

        /// <summary>Wood left out in the weather, somewhere between the colour it was cut and silver.</summary>
        public static Vector3 Weathered(Genes style) => Vector3.Lerp(new Vector3(0.4f, 0.38f, 0.35f), new Vector3(0.42f, 0.32f, 0.23f), style.Unit("warmth")) * style.Range("wood shade", 0.84f, 1.08f);

        /// <summary>Fresh end grain goes grey too, but stays paler than the sides.</summary>
        public static Vector3 EndGrain(Vector3 wood) => Vector3.ComponentMin(wood * 1.28f + new Vector3(0.04f), new Vector3(0.9f));

        /// <summary>A hewn post's section: a square with its corners taken off by the axe, none quite the same.</summary>
        public static Vector2[] Hewn(float half, WorldRandom random, float least, float most)
        {
            Vector2[] corners = { new(half, -half), new(half, half), new(-half, half), new(-half, -half) };
            var points = new Vector2[8];
            for (int i = 0; i < 4; i++)
            {
                Vector2 corner = corners[i];
                Vector2 previous = corners[(i + 3) % 4], next = corners[(i + 1) % 4];
                float off = half * random.Range(least, most);
                points[i * 2] = corner + (previous - corner).Normalized() * off + new Vector2(random.Range(-1f, 1f), random.Range(-1f, 1f)) * half * 0.05f;
                points[i * 2 + 1] = corner + (next - corner).Normalized() * off + new Vector2(random.Range(-1f, 1f), random.Range(-1f, 1f)) * half * 0.05f;
            }
            return points;
        }

        /// <summary>A split rail's section: as a log comes apart along its grain, no two alike, wider one way than the other.</summary>
        public static Vector2[] Split(float halfWidth, float halfHeight, WorldRandom random)
        {
            var points = new Vector2[5];
            float spin = random.Range(0f, MathF.Tau);
            for (int i = 0; i < points.Length; i++)
            {
                float angle = spin + i * (MathF.Tau / points.Length) + random.Range(-0.22f, 0.22f);
                float reach = random.Range(0.8f, 1.12f);
                points[i] = new Vector2(MathF.Cos(angle) * halfWidth * reach, MathF.Sin(angle) * halfHeight * reach);
            }
            return points;
        }

        /// <summary>
        /// Noise along a wall that comes round again at the end of a span, so that one span's weathering meets the next one's where they join. From 0 to 1, about
        /// <paramref name="size"/> metres to a feature.
        /// </summary>
        public static float AlongWall(float x, float y, float z, float bay, float size, uint seed)
        {
            float angle = MathF.Tau * x / bay;
            float radius = bay / MathF.Tau / size;
            return FloraNoise.Fbm(new Vector3(MathF.Cos(angle) * radius + z * 0.3f, MathF.Sin(angle) * radius, y / size), seed, 3);
        }

        /// <summary>How a wall of dressed stone weathers: blotches of lichen, a little unevenness of colour, and stains running down from under its top.</summary>
        public static Vector3 Weathering(Vector3 at, Vector3 colour, float bay, float top, uint seed)
        {
            float tone = AlongWall(at.X, at.Y, at.Z, bay, 1.1f, seed);
            float lichen = AlongWall(at.X, at.Y, at.Z, bay, 0.7f, seed + 31u);
            float streaks = AlongWall(at.X, 0f, at.Z, bay, 0.18f, seed + 57u);
            Vector3 c = colour * (0.86f + 0.26f * tone);
            c *= Vector3.Lerp(Vector3.One, new Vector3(0.84f, 0.92f, 0.74f), FenceBuilder.Smoothstep(0.56f, 0.74f, lichen));
            float under = FenceBuilder.Smoothstep(top - 1.3f, top - 0.05f, at.Y);
            c *= 1f - 0.22f * under * FenceBuilder.Smoothstep(0.5f, 0.75f, streaks);
            return c;
        }
    }
}

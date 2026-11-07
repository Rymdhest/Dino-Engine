using Dino_Engine.Modelling.Procedural.Gear;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Flora
{
    /// <summary>What a leaf's outline is built on, in the terms a botanist would use.</summary>
    public enum LeafBlade
    {
        Simple,     // One blade along a midrib: elliptic, ovate, lanceolate, and with lobes or teeth, oak and elm.
        Palmate,    // Fingers from one point: maple, sweetgum.
        Fan,        // A wedge from the stalk: ginkgo.
        Petal,      // A blade that's wide near the tip, and notched or rounded there.
    }

    /// <summary>
    /// A leaf, as numbers. Its outline is a half width along a midrib for a blade and a radius about the stalk for a palmate leaf, in the manner of Runions's margin terms (entire,
    /// toothed, lobed) and Infinigen's leaf generators, and its veins follow the same geometry, so the two can't disagree. Everything is pale: a leaf's colour is the plant's, in its vertices.
    /// </summary>
    public sealed class LeafForm
    {
        public LeafBlade Blade = LeafBlade.Simple;
        public float Width = 0.3f;          // Half width as a share of its length.
        public float Skew = 0.85f;          // Below 1 the widest part is near the stalk, above 1 near the tip.
        public float Round = 0.8f;          // Below 1 blunt and full, above 1 slender and pointed.
        public float DripTip;               // 0 to 1, how far the tip draws out to a point.
        public int Lobes;                   // Each side, or fingers on a palmate leaf.
        public float LobeDepth = 0.55f;     // How far in the gaps between lobes cut, 0 to 1.
        public float LobeSharp = 1.4f;      // Below 1 the gaps are wide, above 1 they're narrow notches.
        public float LobeSpread = 2.3f;     // Radians either side, for palmate and fan leaves.
        public int Teeth;                   // Along each side.
        public float ToothDepth = 0.06f;
        public float Notch;                 // A gap in the top of a fan or a petal.
        public int Veins = 7;               // Each side.
        public float VeinAngle = 0.8f;      // How far forward they sweep.
        public float VeinStrength = 0.5f;
        public float Mottle = 0.14f;        // How uneven its colour is.
        public Vector3 Tone = new Vector3(0.92f, 0.96f, 0.86f);
        public Vector3 VeinTone = new Vector3(1f, 1f, 0.82f);

        /// <summary>How far the leaf can reach to either side of its midrib, as a share of its length: what the image has to be wide enough for.</summary>
        public float Reach => Blade switch
        {
            LeafBlade.Palmate => 1f,
            LeafBlade.Fan => 0.95f,
            _ => Width,
        };

        // 0 at the stalk to 1 at the tip, the half width there as a share of the widest.
        internal float HalfWidth(float t, uint seed, float side)
        {
            if (t <= 0f || t >= 1f) return 0f;
            float width = MathF.Pow(MathF.Sin(MathF.PI * MathF.Pow(t, Skew)), Round);
            if (DripTip > 0f) width *= 1f - DripTip * Smooth(0.6f, 1f, t) * 0.7f;

            if (Lobes > 0)
            {
                // Lobes get smaller towards the tip, as each has had less time to grow out.
                float wave = 0.5f - 0.5f * MathF.Cos(MathF.Tau * Lobes * MathF.Pow(t, 0.85f));
                width *= 1f - LobeDepth * MathF.Pow(1f - wave, LobeSharp) * (0.55f + 0.45f * MathF.Sin(MathF.PI * t));
            }
            if (Teeth > 0)
            {
                float saw = t * Teeth + side * 0.3f;
                width *= 1f - ToothDepth * (saw - MathF.Floor(saw));
            }
            return width * (1f + 0.08f * side * FloraNoise.Wave(t * 3f, seed));
        }

        private static float Smooth(float from, float to, float value)
        {
            float t = Math.Clamp((value - from) / (to - from), 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        // How far a palmate leaf reaches at an angle from its stalk.
        internal float PalmateReach(float angle, uint seed)
        {
            int fingers = Math.Max(Lobes, 3);
            float best = 0f;
            float gap = 2f * LobeSpread / (fingers - 1);
            for (int i = 0; i < fingers; i++)
            {
                float centre = -LobeSpread + i * gap;
                float length = 1f - 0.42f * MathF.Pow(MathF.Abs(centre) / LobeSpread, 1.6f);
                float x = MathF.Abs(angle - centre) / (gap * (0.62f + 0.18f * LobeDepth));
                if (x >= 1f) continue;
                best = MathF.Max(best, length * MathF.Pow(1f - MathF.Pow(x, 1.5f), 0.9f));
            }
            float web = 0.42f * (1f - LobeDepth * 0.7f) * Smooth(LobeSpread + 0.25f, LobeSpread - 0.3f, MathF.Abs(angle));
            float reach = MathF.Max(best, web);
            if (Teeth > 0)
            {
                float saw = (angle + 4f) * Teeth;
                reach *= 1f - ToothDepth * (saw - MathF.Floor(saw));
            }
            return reach * (1f + 0.04f * FloraNoise.Wave(angle * 3f, seed));
        }

        internal float FanReach(float angle, uint seed)
        {
            if (MathF.Abs(angle) >= LobeSpread) return 0f;
            float edge = 1f - 0.16f * MathF.Pow(MathF.Abs(angle) / LobeSpread, 3f);
            edge *= 1f - Notch * MathF.Exp(-angle * angle / 0.012f);
            edge *= 1f + 0.05f * FloraNoise.Wave(angle * 7f, seed);
            return edge;
        }

        /// <summary>Paints one leaf, or a petal. Each seed is a different leaf of the same kind.</summary>
        public LeafImage Paint(int size, uint seed)
        {
            var image = new LeafImage(size);
            float reach = Reach;
            uint noise = FloraNoise.Hash(seed);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Across goes from -reach to reach, and along from the stalk at 0 to the tip at 1.
                    float across = ((x + 0.5f) / size * 2f - 1f) * reach;
                    float along = (y + 0.5f) / size;
                    float edge = Edge(across, along, noise, out float vein, out float spot);
                    if (edge <= 0f) continue;

                    Vector3 sample = new Vector3(across * 7f, along * 7f, 0.5f);
                    float mottle = FloraNoise.Fbm(sample, noise, 3) * 2f - 1f;
                    float fine = FloraNoise.Value(sample * 5f, noise + 17u) * 2f - 1f;
                    Vector3 colour = Tone * (1f + Mottle * mottle + Mottle * 0.4f * fine);
                    colour = Vector3.Lerp(colour, VeinTone, vein * VeinStrength);
                    // It darkens a little towards its edge, and goes brown at the odd bruise.
                    colour *= 0.82f + 0.18f * Smooth(0f, 0.22f, spot);

                    float bruise = Smooth(0.7f, 0.8f, FloraNoise.Fbm(sample * 1.7f + new Vector3(4f), noise + 31u, 2));
                    colour = Vector3.Lerp(colour, colour * new Vector3(0.95f, 0.8f, 0.55f), bruise * 0.6f);

                    image.Set(x, y, colour, edge, 0.5f + vein * 0.35f + mottle * 0.1f);
                }
            }
            return image;
        }

        // How solid the leaf is at a point, softened over about a texel's worth, with how much vein is there and how far in from the margin it is.
        private float Edge(float across, float along, uint noise, out float vein, out float spot)
        {
            vein = 0f;
            spot = 0f;
            float distance;

            if (Blade == LeafBlade.Palmate || Blade == LeafBlade.Fan)
            {
                // Measured from where the stalk meets the blade, which is low down on a maple and at the very foot of a ginkgo.
                float originY = Blade == LeafBlade.Palmate ? 0.3f : 0f;
                float dy = along - originY, dx = across;
                float radius = MathF.Sqrt(dx * dx + dy * dy);
                float angle = MathF.Atan2(dx, dy);
                float full = Blade == LeafBlade.Palmate ? 0.72f : 1f;
                float limit = (Blade == LeafBlade.Palmate ? PalmateReach(angle, noise) : FanReach(angle, noise)) * full;
                distance = limit - radius;
                spot = distance / MathF.Max(limit, 1e-3f);

                if (Blade == LeafBlade.Palmate)
                {
                    int fingers = Math.Max(Lobes, 3);
                    float gap = 2f * LobeSpread / (fingers - 1);
                    float wrapped = (angle + LobeSpread) / gap;
                    float line = MathF.Abs(wrapped - MathF.Round(wrapped)) * gap * radius;
                    vein = 1f - Smooth(0.004f, 0.016f, line);
                }
                else
                {
                    float line = MathF.Abs(MathF.Sin(angle * 26f));
                    vein = (1f - Smooth(0f, 0.2f, line)) * 0.6f;
                }
            }
            else
            {
                float side = across < 0f ? -1f : 1f;
                float petal = Blade == LeafBlade.Petal && Notch > 0f ? 1f - Notch * MathF.Exp(-across * across / 0.006f) : 1f;
                float top = along / petal;
                float half = HalfWidth(top, noise, side) * Width;
                distance = half - MathF.Abs(across);
                spot = distance / MathF.Max(Width, 1e-3f);

                float midrib = 1f - Smooth(0.003f, 0.012f + 0.01f * (1f - along), MathF.Abs(across));
                vein = midrib;
                if (Veins > 0)
                {
                    float sweep = along - MathF.Abs(across) * VeinAngle / MathF.Max(Width, 0.05f) * 0.5f;
                    float wave = sweep * Veins + (across < 0f ? 0.5f : 0f);
                    float line = MathF.Abs(wave - MathF.Round(wave)) / Veins;
                    vein = MathF.Max(midrib, (1f - Smooth(0.003f, 0.012f, line)) * 0.7f);
                }
            }

            return Smooth(-0.004f, 0.006f, distance);
        }
    }

    /// <summary>A leaf as pixels, with the stalk at the bottom middle.</summary>
    public sealed class LeafImage
    {
        public readonly int Size;
        private readonly float[] _channels;     // Red, green, blue, alpha, height.
        private const int Stride = 5;

        public LeafImage(int size)
        {
            Size = size;
            _channels = new float[size * size * Stride];
        }

        public void Set(int x, int y, Vector3 colour, float alpha, float height)
        {
            int at = (y * Size + x) * Stride;
            _channels[at] = colour.X;
            _channels[at + 1] = colour.Y;
            _channels[at + 2] = colour.Z;
            _channels[at + 3] = alpha;
            _channels[at + 4] = height;
        }

        /// <summary>Reads the leaf between its texels, with <paramref name="across"/> -1 to 1 and <paramref name="along"/> 0 to 1.</summary>
        public void Sample(float across, float along, out Vector3 colour, out float alpha, out float height)
        {
            float fx = (across * 0.5f + 0.5f) * Size - 0.5f;
            float fy = along * Size - 0.5f;
            int x = (int)MathF.Floor(fx), y = (int)MathF.Floor(fy);
            float tx = fx - x, ty = fy - y;

            colour = Vector3.Zero;
            alpha = 0f;
            height = 0f;
            float weight = 0f;
            for (int j = 0; j <= 1; j++)
            {
                int py = y + j;
                if (py < 0 || py >= Size) continue;
                float wy = j == 0 ? 1f - ty : ty;
                for (int i = 0; i <= 1; i++)
                {
                    int px = x + i;
                    if (px < 0 || px >= Size) continue;
                    float w = wy * (i == 0 ? 1f - tx : tx);
                    int at = (py * Size + px) * Stride;
                    float a = _channels[at + 3];
                    alpha += a * w;
                    // Colour counts only where there's leaf, or its edge would go to black.
                    if (a <= 0f) continue;
                    float solid = w * a;
                    colour += new Vector3(_channels[at], _channels[at + 1], _channels[at + 2]) * solid;
                    height += _channels[at + 4] * solid;
                    weight += solid;
                }
            }
            if (weight > 0f)
            {
                colour /= weight;
                height /= weight;
            }
        }
    }
}

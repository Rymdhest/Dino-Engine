using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Flora
{
    /// <summary>
    /// A water lily's pad, seen from above, filling the card: round, cleft from its rim to its middle where the stalk comes up, its veins running out from there to the rim, waxy and a
    /// little uneven in colour, its rim darker where the red of its underside shows, and the odd blemish. The cleft is at the foot of the card, which is where a plant hangs it from.
    /// </summary>
    public sealed class LilyPad : CardForm
    {
        public int Veins = 19;
        public float Cleft = 0.3f;                  // Radians either side of the cleft at the rim.
        public float Mottle = 0.12f;
        public Vector3 Tone = new Vector3(0.94f, 0.97f, 0.9f);
        public Vector3 RimTone = new Vector3(0.82f, 0.62f, 0.56f);
        public Vector3 VeinTone = new Vector3(1.04f, 1.04f, 0.92f);

        public override void Paint(CardCanvas canvas, Genes genes)
        {
            WorldRandom random = genes.Stream("pad");
            const int Size = 320;
            var image = new LeafImage(Size);
            uint noise = (uint)random.NextULong();
            float twist = random.Range(-0.12f, 0.12f);
            float rimWave = random.Range(0.015f, 0.035f);
            int veins = Veins + random.Int(-2, 2);
            float veinTurn = random.Float() * MathF.Tau;
            float texel = 2f / Size;

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    // Across goes -1 to 1, and along the same once the middle is put at half way, so the pad is a circle in the canvas.
                    float px = (x + 0.5f) / Size * 2f - 1f;
                    float py = (y + 0.5f) / Size * 2f - 1f;
                    float r = MathF.Sqrt(px * px + py * py);
                    // Angle from the cleft, which points at the foot of the card.
                    float angle = MathF.Atan2(px, -py) - twist;
                    float rim = 0.97f * (1f + rimWave * FloraNoise.Wave(angle * 1.6f + 7f, noise));
                    float alpha = Math.Clamp((rim - r) / texel + 0.5f, 0f, 1f);
                    // The cleft is a slit at the middle that opens to the rim, its sides nearly touching where the stalk is.
                    float cleft = Cleft * Smooth(0.04f, 1f, r);
                    alpha *= Math.Clamp((MathF.Abs(angle) - cleft) / (texel / MathF.Max(r, 0.05f)) + 0.5f, 0f, 1f);
                    if (alpha <= 0.002f) continue;

                    // Veins run out from where the stalk is, thinning and fading towards the rim.
                    float spoke = (angle + veinTurn) / MathF.Tau * veins;
                    float off = MathF.Abs(spoke - MathF.Round(spoke)) * MathF.Tau / veins * r;
                    float vein = (1f - Smooth(0.003f, 0.012f, off)) * (1f - Smooth(0.6f, 0.95f, r)) * Smooth(0.03f, 0.12f, r);

                    Vector3 sample = new Vector3(px * 4.5f, py * 4.5f, 0.5f);
                    float mottle = FloraNoise.Fbm(sample, noise, 3) * 2f - 1f;
                    float fine = FloraNoise.Value(sample * 6f, noise + 11u) * 2f - 1f;
                    Vector3 colour = Tone * (1f + Mottle * mottle + Mottle * 0.35f * fine);
                    colour = Vector3.Lerp(colour, VeinTone, vein * 0.55f);
                    colour = Vector3.Lerp(colour, colour * RimTone, Smooth(0.82f, 0.97f, r / rim));
                    // A few spots gone brown, where something's grazed it or it's beginning to die back.
                    float blemish = Smooth(0.72f, 0.8f, FloraNoise.Fbm(sample * 1.9f + new Vector3(3f), noise + 23u, 2));
                    colour = Vector3.Lerp(colour, colour * new Vector3(0.9f, 0.72f, 0.42f), blemish * 0.7f);

                    // A pad is a shallow dish: it lies a little higher at its rim than its middle, which is what the canvas lights across.
                    image.Set(x, y, colour, alpha, 0.45f + 0.15f * r * r + vein * 0.1f);
                }
            }

            CardInk ink = CardInk.Leaf(Vector3.One, Vector3.UnitZ, 0.6f);
            ink.Translucency = 0.2f;    // Thick, and floating on water: little of the sun comes through.
            ink.Roughness = 0.42f;      // Waxy.
            canvas.Later(0.6f, () => canvas.Leaf(image, new Vector2(0.5f, 0.03f), new Vector2(0.5f, 0.97f), 0.47f, ink, 0.05f, 0.04f));
        }

        private static float Smooth(float from, float to, float value)
        {
            float t = Math.Clamp((value - from) / (to - from), 0f, 1f);
            return t * t * (3f - 2f * t);
        }
    }

    /// <summary>
    /// A sea fan, flat on, filling the card from a stalk at its foot: its main branches spread out across a fan and fork as they go, and all of them are knitted together by fine
    /// cross-links into a net, which is what a gorgonian is. Far finer than a pixel from up here, so what reads is its outline and how open its net is.
    /// </summary>
    public sealed class SeaFan : CardForm
    {
        public IntRange Branches = new IntRange(9, 12);
        public float Spread = 1.25f;                // Radians either side of upright the fan reaches.
        public float Reach = 0.86f;                 // How far up the card it reaches, from its foot.
        public float Link = 0.026f;                 // Canvas between cross-links along a branch.

        public override void Paint(CardCanvas canvas, Genes genes)
        {
            WorldRandom random = genes.Stream("fan");
            Vector2 foot = new Vector2(0.5f + random.Range(-0.02f, 0.02f), 0.02f);
            Vector2 hub = foot + new Vector2(random.Range(-0.02f, 0.02f), 0.08f);
            CardInk ink = CardInk.Wood(Vector3.One, 0.5f);
            ink.Translucency = 0.35f;
            ink.Roughness = 0.82f;
            canvas.Later(0.4f, () => canvas.Stroke(foot, hub, 0.012f, 0.009f, ink));

            // Each main branch wanders out from the hub across its share of the fan, and the fan's edge is an ellipse that's wider than it is tall from there.
            int count = random.Int(Branches);
            var paths = new List<Vector2[]>();
            for (int b = 0; b < count; b++)
            {
                float angle = -Spread + 2f * Spread * (b + 0.5f + random.Range(-0.2f, 0.2f)) / count;
                paths.Add(Path(random, hub, angle, 0f));
            }
            // Smaller branches fork off into the gaps between the main ones, so the net is even.
            int mains = paths.Count;
            for (int b = 0; b + 1 < mains; b++)
            {
                Vector2[] from = paths[b];
                int at = random.Int(3, 6);
                float angle = MathF.Atan2(from[at].X - hub.X, from[at].Y - hub.Y);
                float next = MathF.Atan2(paths[b + 1][at].X - hub.X, paths[b + 1][at].Y - hub.Y);
                paths.Insert(paths.Count, Path(random, from[at], (angle + next) * 0.5f, 0.35f));
            }

            foreach (Vector2[] path in paths)
            {
                for (int i = 1; i < path.Length; i++)
                {
                    Vector2 a = path[i - 1], c = path[i];
                    float t = (float)i / path.Length;
                    canvas.Later(0.45f, () => canvas.Stroke(a, c, MathHelper.Lerp(0.0065f, 0.0028f, t), MathHelper.Lerp(0.006f, 0.0025f, t), ink));
                }
            }

            // The net: every branch is linked to whichever branch is nearest it, over and over along its length.
            for (int p = 0; p < paths.Count; p++)
            {
                Vector2[] path = paths[p];
                for (int i = 1; i < path.Length; i++)
                {
                    Vector2 point = path[i];
                    Vector2 best = point;
                    float bestDistance = float.MaxValue;
                    for (int q = 0; q < paths.Count; q++)
                    {
                        if (q == p) continue;
                        foreach (Vector2 other in paths[q])
                        {
                            float d = (other - point).LengthSquared;
                            if (d < bestDistance && d > Link * Link * 0.2f)
                            {
                                bestDistance = d;
                                best = other;
                            }
                        }
                    }
                    if (bestDistance > 0.09f * 0.09f) continue;
                    int links = Math.Max(1, (int)(MathF.Sqrt(bestDistance) / Link));
                    for (int l = 0; l < links; l++)
                    {
                        Vector2 a = Vector2.Lerp(point, best, (float)l / links), c = Vector2.Lerp(point, best, (l + 1f) / links);
                        Vector2 jitter = new Vector2(random.Range(-0.004f, 0.004f), random.Range(-0.004f, 0.004f));
                        canvas.Later(0.5f, () => canvas.Stroke(a, c + jitter, 0.0019f, 0.0016f, ink));
                    }
                }
            }
        }

        // A branch from `from` heading out at `angle` from upright, wandering a little and ending at the fan's edge.
        private Vector2[] Path(WorldRandom random, Vector2 from, float angle, float shorter)
        {
            const int Steps = 9;
            var points = new Vector2[Steps + 1];
            points[0] = from;
            Vector2 at = from;
            float heading = angle;
            float step = Reach / Steps * (1f - shorter);
            for (int i = 1; i <= Steps; i++)
            {
                heading += random.Range(-0.12f, 0.12f);
                Vector2 next = at + new Vector2(MathF.Sin(heading) * 0.62f, MathF.Cos(heading)) * step;
                // The fan's edge: an ellipse about the hub, wider than it is tall.
                Vector2 off = next - new Vector2(0.5f, 0.1f);
                float edge = (off.X * off.X) / (0.46f * 0.46f) + (off.Y * off.Y) / (Reach * Reach);
                if (edge > 1f) next = new Vector2(0.5f, 0.1f) + off / MathF.Sqrt(edge);
                at = Inside(next, 0.012f);
                points[i] = at;
            }
            return points;
        }
    }
}

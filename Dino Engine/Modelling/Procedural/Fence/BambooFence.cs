using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Fence
{
    /// <summary>
    /// A bamboo lattice, as the East fences a garden: thick culms for posts, three or four bars across, and canes standing on alternate sides of them, tied at every crossing
    /// with black cord. The knots are what reads from above, a line of dark points along each bar, and canes are spaced half a pitch in from each post so the lattice runs on.
    /// </summary>
    internal static class BambooFence
    {
        public static void Build(in FenceJob job)
        {
            Genes style = job.Style;
            FenceBuilder b = job.B;
            float postRadius = style.Range("post", 0.1f, 0.13f);
            float height = style.Range("height", 2.9f, 3.3f);
            int bars = 3 + style.Pick("bars", 0.5f, 0.5f);
            float barRadius = style.Range("bar", 0.05f, 0.065f);
            float caneRadius = style.Range("cane", 0.035f, 0.045f);
            float pitchWanted = style.Range("cane pitch", 0.3f, 0.42f);
            float barLow = style.Range("low bar", 0.45f, 0.62f);
            float barHigh = height - style.Range("high bar below", 0.35f, 0.5f);
            float caneTop = height - style.Range("cane below", 0.08f, 0.28f);
            float nodeSpacing = style.Range("node spacing", 0.45f, 0.65f);
            // Deep enough to stand out from the gravel and the dry grass it's put round, which are near the colour of pale cane.
            Vector3 culm = Vector3.Lerp(new Vector3(0.3f, 0.34f, 0.17f), new Vector3(0.44f, 0.36f, 0.2f), style.Unit("age")) * style.Range("shade", 0.9f, 1.05f);
            Vector3 smoked = new Vector3(0.24f, 0.19f, 0.13f) * style.Range("post shade", 0.85f, 1.1f);
            var cane = new Surface(FenceMaterials.Cane, culm, 1.2f);
            var cord = new Surface(FenceMaterials.Cord, new Vector3(0.055f, 0.048f, 0.042f), 0.4f);
            Vector3 cutTop = new Vector3(0.5f, 0.44f, 0.3f);

            if (!job.IsSpan)
            {
                WorldRandom random = job.Own.Stream("post");
                float radius = postRadius * (job.IsEnd ? 1.2f : 1f) * random.Range(0.93f, 1.07f);
                float tall = height + (job.IsEnd ? 0.14f : 0f) + random.Range(-0.04f, 0.04f);
                float[] nodes = Nodes(job.Sink + tall, nodeSpacing * 1.1f, random, job.Sink + tall - 0.05f);
                var post = new Surface(FenceMaterials.Cane, smoked * random.Range(0.88f, 1.1f), 1.2f);
                b.Log(new Vector3(0f, -job.Sink, 0f), new Vector3(0f, tall, 0f), radius, radius * 0.96f, 9, post, (uint)random.Int(int.MaxValue), headEnd: LogEnd.Cut,
                    rough: 0.02f, rings: 1, endColour: cutTop, nodes: nodes, nodeColour: post.Colour * 0.72f);
                if (job.IsEnd)
                {
                    float y = tall - 0.3f;
                    b.Log(new Vector3(0f, y - 0.06f, 0f), new Vector3(0f, y + 0.06f, 0f), radius + 0.02f, radius + 0.02f, 9, cord, 3u, LogEnd.Cut, LogEnd.Cut, rough: 0f, rings: 1);
                }
                return;
            }

            WorldRandom bamboo = job.Own.Stream("bamboo");
            var heights = new float[bars];
            for (int k = 0; k < bars; k++)
            {
                heights[k] = MathHelper.Lerp(barLow, barHigh, k / (float)(bars - 1));
                float[] nodes = Nodes(job.Bay, nodeSpacing * 1.2f, bamboo, -1f);
                Surface bar = cane.Tinted(bamboo.Range(0.88f, 1.08f));
                b.Log(new Vector3(0f, heights[k], 0f), new Vector3(job.Bay, heights[k], 0f), barRadius, barRadius, 6, bar, (uint)bamboo.Int(int.MaxValue), LogEnd.Open, LogEnd.Open,
                    rough: 0.02f, rings: 1, nodes: nodes, nodeColour: bar.Colour * 0.74f);
            }

            int canes = Math.Max(2, (int)MathF.Round(job.Bay / pitchWanted));
            float pitch = job.Bay / canes;
            for (int i = 0; i < canes; i++)
            {
                float x = (i + 0.5f) * pitch;
                float side = i % 2 == 0 ? -1f : 1f;
                float z = side * (barRadius + caneRadius);
                float top = caneTop + bamboo.Range(-0.04f, 0.04f);
                int mark = b.Mark;
                b.Log(new Vector3(x, -0.3f, z), new Vector3(x, top, z), caneRadius, caneRadius * 0.94f, 5, cane.Tinted(bamboo.Range(0.86f, 1.1f)), (uint)bamboo.Int(int.MaxValue),
                    headEnd: LogEnd.Cut, rough: 0.02f, rings: 1, endColour: cutTop);

                // A wrap of cord where the cane crosses each bar, on its outer face, crossed as the knot is.
                foreach (float y in heights)
                {
                    var turn = Matrix3.CreateRotationZ(MathF.PI * 0.25f * (bamboo.Chance(0.5f) ? 1f : -1f));
                    b.Block(new Vector3(x, y, z + side * caneRadius * 0.55f), new Vector3(caneRadius + 0.016f, 0.026f, caneRadius * 0.72f), turn, 0f, cord, Faces.NegativeY);
                }
                b.Upright(mark, x);
            }
        }

        // Where the nodes of a culm fall along it, from a start of its own, with one kept at the top if asked.
        private static float[] Nodes(float length, float spacing, WorldRandom random, float keep)
        {
            var nodes = new List<float>();
            for (float at = random.Range(0.1f, spacing); at < length - 0.05f; at += spacing * random.Range(0.85f, 1.15f)) nodes.Add(at);
            if (keep > 0f)
            {
                nodes.RemoveAll(n => n > keep - spacing * 0.4f);
                nodes.Add(keep);
            }
            return nodes.ToArray();
        }
    }
}

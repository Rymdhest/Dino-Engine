using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Fence
{
    /// <summary>
    /// Walls of logs stood on end: a village's stockade, and the pens the Reich keeps its broods in. Every log is sharpened to a point of axe facets, stands a little out
    /// of true and a little taller or shorter than the next, as felled timber does. Rails behind hold them, and between bays stands a heavier log.
    /// </summary>
    internal static class LogFences
    {
        private static readonly float[] Ends = { 0f, 1f };

        private readonly record struct Logs(float Radius, float Gap, float Height, float Point, float Ragged, float PostRadius, Surface Bark, Vector3 Cut);

        public static void Stockade(in FenceJob job)
        {
            Genes style = job.Style;
            FenceBuilder b = job.B;
            float radius = style.Range("log", 0.19f, 0.25f);
            Vector3 bark = Vector3.Lerp(new Vector3(0.34f, 0.27f, 0.21f), new Vector3(0.31f, 0.29f, 0.26f), style.Unit("grey")) * style.Range("shade", 0.88f, 1.06f);
            Vector3 cut = Vector3.Lerp(new Vector3(0.62f, 0.5f, 0.36f), new Vector3(0.46f, 0.43f, 0.38f), style.Unit("weathered points"));
            var logs = new Logs(radius, style.Range("gap", 0f, 0.03f), style.Range("height", 5.6f, 6.4f), style.Range("point", 0.6f, 0.95f), style.Range("ragged", 0.15f, 0.4f),
                radius * 1.3f, new Surface(FenceMaterials.Bark, bark, 2f), cut);
            float railRadius = style.Range("rail", 0.12f, 0.15f);
            float[] rails = { logs.Height * style.Range("low rail", 0.2f, 0.26f), logs.Height * style.Range("high rail", 0.62f, 0.7f) };
            float[] ropes = { style.Range("low rope", 0.9f, 1.3f), logs.Height - style.Range("high rope below", 1.1f, 1.5f) };
            var cord = new Surface(FenceMaterials.Cord, FenceLook.Cord * style.Range("rope shade", 0.85f, 1.1f), 0.6f);

            if (!job.IsSpan)
            {
                WorldRandom random = job.Own.Stream("post");
                float r = logs.PostRadius * (job.IsEnd ? 1.15f : 1f) * random.Range(0.94f, 1.06f);
                float tall = logs.Height + (job.IsEnd ? 0.9f : random.Range(0.3f, 0.6f));
                b.Log(new Vector3(0f, -job.Sink, 0f), new Vector3(random.Range(-0.03f, 0.03f), tall, random.Range(-0.02f, 0.02f)), r, r * 0.9f, 9, logs.Bark.Tinted(random.Range(0.88f, 1.06f)),
                    (uint)random.Int(int.MaxValue), headEnd: LogEnd.Point, rough: 0.07f, rings: 3, point: logs.Point * 1.1f, endColour: logs.Cut);
                foreach (float y in job.IsEnd ? ropes : new[] { ropes[0] })
                {
                    b.Log(new Vector3(0f, y - 0.05f, 0f), new Vector3(0f, y + 0.05f, 0f), r + 0.04f, r + 0.04f, 9, cord, 5u, LogEnd.Cut, LogEnd.Cut, rough: 0.04f, rings: 1);
                }
                return;
            }

            StandLogs(job, logs, job.Own.Stream("logs"));
            BackRails(job, logs, rails, railRadius, logs.Bark.Tinted(0.9f));
            foreach (float y in ropes)
            {
                float z = -(logs.Radius * 1.04f + 0.035f);
                b.Log(new Vector3(0f, y, z), new Vector3(job.Bay, y - 0.02f, z), 0.035f, 0.035f, 5, cord, 13u, LogEnd.Open, LogEnd.Open, rough: 0.05f, rings: 2, bow: new Vector3(0f, -0.04f, 0f));
            }
        }

        /// <summary>
        /// A pen of the Reich's: bigger logs, tarred black, their points hardened in fire, bound across the outside by straps of black iron, with hewn posts capped and spiked in
        /// iron. A gate's posts carry a band of oxblood. Black iron and oxblood are the Reich's, and nothing else in the world wears them.
        /// </summary>
        public static void Pen(in FenceJob job)
        {
            Genes style = job.Style;
            FenceBuilder b = job.B;
            float radius = style.Range("log", 0.23f, 0.29f);
            Vector3 tar = new Vector3(0.2f, 0.17f, 0.14f) * style.Range("tar", 0.85f, 1.1f);
            var logs = new Logs(radius, style.Range("gap", 0f, 0.02f), style.Range("height", 6.6f, 7.4f), style.Range("point", 0.7f, 1f), style.Range("ragged", 0.1f, 0.25f),
                radius * 1.45f, new Surface(FenceMaterials.Bark, tar, 2f), new Vector3(0.11f, 0.085f, 0.07f));
            float railRadius = style.Range("rail", 0.14f, 0.17f);
            float[] rails = { logs.Height * style.Range("low rail", 0.2f, 0.26f), logs.Height * style.Range("high rail", 0.6f, 0.68f) };
            float[] straps = { style.Range("low strap", 1.1f, 1.5f), logs.Height - style.Range("high strap below", 1.4f, 1.9f) };
            const float StrapHalfHeight = 0.13f, StrapHalfDepth = 0.025f;
            var iron = new Surface(FenceMaterials.Iron, FenceLook.Iron * style.Range("iron shade", 0.85f, 1.1f), 1.5f);

            if (!job.IsSpan)
            {
                WorldRandom random = job.Own.Stream("post");
                float half = logs.PostRadius * (job.IsEnd ? 1.15f : 1f);
                float tall = logs.Height + (job.IsEnd ? 1.4f : random.Range(0.4f, 0.7f));
                Vector2[] section = FenceLook.Hewn(half, random, 0.12f, 0.3f);
                // A flat face in the sun reads far paler than a round log of the same colour, so the tar on a hewn post is laid on darker.
                var timber = new Surface(FenceMaterials.Weathered, tar * 0.45f * random.Range(0.9f, 1.1f), 1.6f);
                b.Prism(new Vector3(0f, -job.Sink, 0f), new Vector3(0f, tall, 0f), Vector3.UnitZ, section, timber, Ends, capFoot: false, endColour: tar);

                // An iron cap, and a spike out of it, and the straps carried round the post.
                float capHalf = half * 1.08f;
                b.Block(new Vector3(0f, tall + 0.06f, 0f), new Vector3(capHalf, 0.1f, capHalf), Matrix3.Identity, 0.02f, iron, Faces.NegativeY);
                Vector3 apex = new Vector3(0f, tall + 0.16f + half * 2.2f, 0f);
                Vector3[] rim = { new(-half * 0.7f, tall + 0.16f, -half * 0.7f), new(half * 0.7f, tall + 0.16f, -half * 0.7f), new(half * 0.7f, tall + 0.16f, half * 0.7f), new(-half * 0.7f, tall + 0.16f, half * 0.7f) };
                for (int i = 0; i < 4; i++) b.Triangle(rim[i], rim[(i + 1) % 4], apex, new Vector3(0f, tall + 0.2f, 0f), iron);
                foreach (float y in straps)
                {
                    b.Block(new Vector3(0f, y, 0f), new Vector3(half * 1.08f + StrapHalfDepth, StrapHalfHeight, half * 1.08f + StrapHalfDepth), Matrix3.Identity, 0.01f, iron);
                }
                if (job.IsEnd)
                {
                    var paint = new Surface(FenceMaterials.Weathered, new Vector3(0.15f, 0.022f, 0.02f), 1.6f);
                    float y = logs.Height * 0.52f;
                    b.Block(new Vector3(0f, y, 0f), new Vector3(half * 1.03f, 0.34f, half * 1.03f), Matrix3.Identity, 0.01f, paint);
                }
                return;
            }

            StandLogs(job, logs, job.Own.Stream("logs"));
            BackRails(job, logs, rails, railRadius, logs.Bark.Tinted(1.1f));
            foreach (float y in straps)
            {
                float z = -(logs.Radius * 1.08f + StrapHalfDepth);
                b.Block(new Vector3(job.Bay * 0.5f, y, z), new Vector3(job.Bay * 0.5f, StrapHalfHeight, StrapHalfDepth), Matrix3.Identity, 0.008f, iron);
            }
        }

        // The rails the logs are pinned to, on the inside, sawn off at the heavier logs either end: at a corner they'd otherwise run out past it.
        private static void BackRails(in FenceJob job, in Logs logs, float[] heights, float radius, Surface bark)
        {
            float z = logs.Radius + radius;
            float end = logs.PostRadius * 0.95f;
            Vector3 cut = FenceLook.EndGrain(bark.Colour);
            foreach (float y in heights)
            {
                job.B.Log(new Vector3(end, y, z), new Vector3(job.Bay - end, y, z), radius, radius, 6, bark, 11u, LogEnd.Cut, LogEnd.Cut, rough: 0.05f, rings: 2, endColour: cut);
            }
        }

        // The logs of a span, between the heavier logs at its ends, each its own height, girth and lean.
        private static void StandLogs(in FenceJob job, in Logs logs, WorldRandom random)
        {
            FenceBuilder b = job.B;
            float start = logs.PostRadius + logs.Gap + logs.Radius;
            float usable = job.Bay - start * 2f;
            int count = Math.Max(1, (int)MathF.Round(usable / (logs.Radius * 2f + logs.Gap)) + 1);
            float pitch = count > 1 ? usable / (count - 1) : 0f;
            for (int i = 0; i < count; i++)
            {
                float x = count > 1 ? start + i * pitch : job.Bay * 0.5f;
                float r = logs.Radius * random.Range(0.88f, 1.08f);
                float h = logs.Height + random.Range(-logs.Ragged, logs.Ragged);
                Vector3 lean = new Vector3(random.Range(-0.02f, 0.02f), 0f, random.Range(-0.006f, 0.006f)) * h;
                float z = random.Range(-0.02f, 0.02f);
                int mark = b.Mark;
                b.Log(new Vector3(x, -job.Sink, z), new Vector3(x, h, z) + lean, r, r * 0.9f, 8, logs.Bark.Tinted(random.Range(0.86f, 1.1f)), (uint)random.Int(int.MaxValue),
                    headEnd: LogEnd.Point, rough: 0.07f, rings: 3, point: logs.Point * random.Range(0.85f, 1.15f), endColour: logs.Cut * random.Range(0.88f, 1.1f));
                b.Upright(mark, x);
            }
        }
    }
}

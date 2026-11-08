using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.Modelling.Procedural.Fence;
using OpenTK.Mathematics;

namespace Dino_Engine.World
{
    /// <summary>A run of fence or wall from one point to the next. Its inside, which its spans' +z faces, is to the left of the way it runs.</summary>
    public readonly record struct FenceRun(FenceKind Kind, ulong Style, Vector2 From, Vector2 To);

    /// <summary>
    /// Stands fences on the finished ground. Every post is upright and on the ground where it stands, and every span reaches from one post to the next exactly: it's the model
    /// built for the nearest slope (<see cref="FenceKind.Step"/>), stretched to the length and tilted by whatever's left over, so its uprights lean by a degree or so at most
    /// and its rails meet the posts at the height they leave them. Ends of runs that meet are one post: where they carry straight on, an ordinary one, and where they turn, at a
    /// corner, or stop, at a gate or a loose end, the heavier end post that holds them. Headless and deterministic: the game and the lab lay fences out by this alone.
    /// </summary>
    public static class FenceLayout
    {
        private const float Join = 0.35f;           // Metres apart that two runs' ends are taken to meet.
        private const float Straight = 0.9f;        // Two runs meeting closer to a straight line than this (a cosine, about 25 degrees) go on through an ordinary post.
        private const float Square = 0.3f;          // And closer to a right angle than this, a corner's post squares up with the first of them.
        private const float LeastStretch = 0.7f;    // A run can be given more posts than its bays want, to follow the ground, down to this share of a bay apart.
        private const float MostGradient = 1f;      // Steeper than this, a span leans by what's beyond it.

        private sealed class Node
        {
            public Vector2 At;
            public readonly List<(int Run, bool Start)> Ends = new();
        }

        /// <param name="place">Takes a model id, where its origin stands, how it's turned and how it's stretched, as <see cref="WorldObjects.Add"/> does.</param>
        public static void Lay(IReadOnlyList<FenceRun> runs, Func<Vector2, float> ground, Action<string, Vector3, Quaternion, Vector3> place)
        {
            var from = new Vector2[runs.Count];
            var to = new Vector2[runs.Count];
            for (int i = 0; i < runs.Count; i++)
            {
                from[i] = runs[i].From;
                to[i] = runs[i].To;
            }

            List<Node> nodes = Meet(runs);
            foreach (Node node in nodes)
            {
                foreach ((int run, bool start) in node.Ends)
                {
                    if (start) from[run] = node.At;
                    else to[run] = node.At;
                }
            }

            foreach (Node node in nodes) PlacePost(node, runs, from, to, ground, place);

            for (int i = 0; i < runs.Count; i++)
            {
                FenceRun run = runs[i];
                Vector2 a = from[i], b = to[i];
                float length = (b - a).Length;
                if (length < 0.3f) continue;

                float bay = FenceForge.BayOf(run.Kind, run.Style);
                int bays = Math.Max(1, (int)MathF.Round(length / bay));
                int most = Math.Max(bays, (int)(length / (bay * LeastStretch)));
                float tolerance = MathF.Max(0.25f, run.Kind.Sink * 0.6f);
                while (bays < most && Deviation(a, b, bays, ground) > tolerance) bays++;

                Vector2 direction = (b - a) / length;
                float yaw = YawOf(direction);
                Vector2 previous = a;
                float previousHeight = ground(a);
                for (int k = 1; k <= bays; k++)
                {
                    Vector2 next = k == bays ? b : Vector2.Lerp(a, b, k / (float)bays);
                    float nextHeight = ground(next);
                    PlaceSpan(run, previous, previousHeight, next, nextHeight, bay, yaw, place);
                    if (k < bays && run.Kind.LinePosts)
                    {
                        var post = new FenceSpec(run.Kind, run.Style, FencePiece.Post, VariantAt(next, run.Kind.PostVariants, 17u), 0);
                        place(post.Id, new Vector3(next.X, nextHeight, next.Y), Quaternion.FromAxisAngle(Vector3.UnitY, yaw), Vector3.One);
                    }
                    previous = next;
                    previousHeight = nextHeight;
                }
            }
        }

        // Which ends of which runs are one point: those of the same style within Join of each other.
        private static List<Node> Meet(IReadOnlyList<FenceRun> runs)
        {
            var nodes = new List<Node>();
            var cells = new Dictionary<(int, int), List<Node>>();
            for (int i = 0; i < runs.Count; i++)
            {
                foreach (bool start in new[] { true, false })
                {
                    Vector2 at = start ? runs[i].From : runs[i].To;
                    (int, int) cell = ((int)MathF.Floor(at.X / Join), (int)MathF.Floor(at.Y / Join));
                    Node? found = null;
                    for (int dz = -1; dz <= 1 && found == null; dz++)
                    {
                        for (int dx = -1; dx <= 1 && found == null; dx++)
                        {
                            if (!cells.TryGetValue((cell.Item1 + dx, cell.Item2 + dz), out List<Node>? near)) continue;
                            foreach (Node node in near)
                            {
                                FenceRun other = runs[node.Ends[0].Run];
                                if (other.Kind != runs[i].Kind || other.Style != runs[i].Style || (node.At - at).Length > Join) continue;
                                found = node;
                                break;
                            }
                        }
                    }
                    if (found == null)
                    {
                        found = new Node { At = at };
                        nodes.Add(found);
                        if (!cells.TryGetValue(cell, out List<Node>? list)) cells[cell] = list = new List<Node>();
                        list.Add(found);
                    }
                    found.Ends.Add((i, start));
                }
            }

            // Where they meet is where they meet on average, so no run's end moves far.
            foreach (Node node in nodes)
            {
                Vector2 sum = Vector2.Zero;
                foreach ((int run, bool start) in node.Ends) sum += start ? runs[run].From : runs[run].To;
                node.At = sum / node.Ends.Count;
            }
            return nodes;
        }

        private static void PlacePost(Node node, IReadOnlyList<FenceRun> runs, Vector2[] from, Vector2[] to, Func<Vector2, float> ground, Action<string, Vector3, Quaternion, Vector3> place)
        {
            FenceRun first = runs[node.Ends[0].Run];
            Vector2 Into((int Run, bool Start) end)
            {
                Vector2 span = to[end.Run] - from[end.Run];
                Vector2 direction = span.LengthSquared > 1e-8f ? span.Normalized() : Vector2.UnitX;
                return end.Start ? direction : -direction;
            }

            Vector2 d0 = Into(node.Ends[0]);
            FencePiece piece = FencePiece.End;
            Vector2 facing = d0;
            if (node.Ends.Count == 2)
            {
                Vector2 d1 = Into(node.Ends[1]);
                float turn = Vector2.Dot(d0, d1);
                Vector2 through = (d1 - d0).LengthSquared > 1e-8f ? (d1 - d0).Normalized() : d1;
                if (turn < -Straight)
                {
                    if (!first.Kind.LinePosts) return;
                    piece = FencePiece.Post;
                    facing = through;
                }
                else if (MathF.Abs(turn) >= Square)
                {
                    facing = through;
                }
            }

            var spec = new FenceSpec(first.Kind, first.Style, piece, VariantAt(node.At, first.Kind.PostVariants, piece == FencePiece.Post ? 17u : 29u), 0);
            place(spec.Id, new Vector3(node.At.X, ground(node.At), node.At.Y), Quaternion.FromAxisAngle(Vector3.UnitY, YawOf(facing)), Vector3.One);
        }

        // A span from one post to the next. Its model is built level for a length of one bay and leans up the slope it's for; stretched along its length to fit, and turned
        // about its foot by what's left over, its far end lands exactly where the next post stands.
        private static void PlaceSpan(in FenceRun run, Vector2 a, float heightA, Vector2 b, float heightB, float bay, float yaw, Action<string, Vector3, Quaternion, Vector3> place)
        {
            float length = (b - a).Length;
            float rise = heightB - heightA;
            int most = (int)MathF.Round(MostGradient / run.Kind.Step);

            // Stretching it along x changes the slope its model has by as much, so the step that fits is the one that will once it's stretched.
            int steps = Math.Clamp((int)MathF.Round(rise / bay / run.Kind.Step), -most, most);
            float modelRise = steps * run.Kind.Step * bay;
            float reach = length * length + rise * rise - modelRise * modelRise;
            if (reach < length * length * 0.25f)
            {
                steps = 0;
                modelRise = 0f;
                reach = length * length + rise * rise;
            }
            float stretch = MathF.Sqrt(reach) / bay;
            float tilt = MathF.Atan2(rise, length) - MathF.Atan2(modelRise, stretch * bay);

            var spec = new FenceSpec(run.Kind, run.Style, FencePiece.Span, VariantAt((a + b) * 0.5f, run.Kind.SpanVariants, 41u), steps);
            Quaternion rotation = Quaternion.FromAxisAngle(Vector3.UnitY, yaw) * Quaternion.FromAxisAngle(Vector3.UnitZ, tilt);
            place(spec.Id, new Vector3(a.X, heightA, a.Y), rotation, new Vector3(stretch, 1f, 1f));
        }

        // How far the ground strays from straight lines between the posts, at its worst.
        private static float Deviation(Vector2 a, Vector2 b, int bays, Func<Vector2, float> ground)
        {
            float worst = 0f;
            float previous = ground(a);
            for (int k = 1; k <= bays; k++)
            {
                Vector2 start = Vector2.Lerp(a, b, (k - 1f) / bays), end = Vector2.Lerp(a, b, k / (float)bays);
                float next = ground(end);
                for (int s = 1; s <= 3; s++)
                {
                    float t = s / 4f;
                    worst = MathF.Max(worst, MathF.Abs(ground(Vector2.Lerp(start, end, t)) - MathHelper.Lerp(previous, next, t)));
                }
                previous = next;
            }
            return worst;
        }

        /// <summary>The yaw that turns a model's +x along <paramref name="direction"/>.</summary>
        public static float YawOf(Vector2 direction) => MathF.Atan2(-direction.Y, direction.X);

        // Which of a few models stands somewhere, by where, so that nothing is drawn from a stream that anything else depends on.
        private static int VariantAt(Vector2 at, int variants, uint salt)
        {
            if (variants <= 1) return 0;
            uint here = FloraNoise.Hash((uint)(int)MathF.Floor(at.X * 4f) * 0x9E3779B1u ^ FloraNoise.Hash((uint)(int)MathF.Floor(at.Y * 4f) ^ salt));
            return (int)(here % (uint)variants);
        }
    }
}

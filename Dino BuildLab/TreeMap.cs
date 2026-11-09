using Dino_Defenders.Talents;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_BuildLab
{
    /// <summary>
    /// The whole tree as a picture, with no window: every node in its region's colour, every link, the roads and the waygates. It's for judging the
    /// shape of the tree as it's grown, which no check can, and for finding where things crowd each other.
    ///   tree [--out file.png] [--size pixels] [--x units --y units --span units]   The whole tree, or a square of it.
    /// </summary>
    internal static class TreeMap
    {
        private static bool Verbose;

        public static int Run(Options options)
        {
            Verbose = options.Text("verbose") != null;
            if (options.Text("node") is string probe && TalentTree.Find(probe) is TalentNode probed)
            {
                Console.WriteLine($"{probed.Id} at {probed.Position} r {probed.Position.Length:0}: " + string.Join(", ", probed.Links.Select(l => $"{TalentTree.Nodes[l].Id} ({(TalentTree.Nodes[l].Position - probed.Position).Length:0})")));
                return 0;
            }
            string path = options.Text("out") ?? "tree.png";
            int size = options.Int("size", 3600);
            TalentNode[] nodes = TalentTree.Nodes;
            float reach = nodes.Max(n => n.Position.Length) + 120f;
            float span = options.Int("span", (int)(reach * 2f));
            var centre = new Vector2(options.Int("x", 0), options.Int("y", 0));
            float scale = size / span;
            var canvas = new Canvas(size, size, new Vector3(0.03f, 0.035f, 0.06f));
            Vector2 ToPixel(Vector2 at) => (at - centre) * scale + new Vector2(size * 0.5f);


            foreach (TalentNode node in nodes)
            {
                foreach (int link in node.Links)
                {
                    if (link < node.Index) continue;
                    TalentNode other = nodes[link];
                    bool road = node.Highway && other.Highway;
                    Vector3 colour = road ? new Vector3(0.85f, 0.74f, 0.45f) : new Vector3(0.38f, 0.4f, 0.48f);
                    float width = (road ? 3.2f : 1.6f) * MathF.Max(scale * 2f, 0.6f);
                    if (node.OrbitRadius > 0f && MathF.Abs(node.OrbitRadius - other.OrbitRadius) < 0.5f && (node.Orbit - other.Orbit).Length < 0.5f)
                    {
                        canvas.Arc(ToPixel(node.Orbit), node.OrbitRadius * scale, ToPixel(node.Position), ToPixel(other.Position), width, colour);
                    }
                    else canvas.Line(ToPixel(node.Position), ToPixel(other.Position), width, colour);
                }
                if (node.Twin > node.Index) canvas.Line(ToPixel(node.Position), ToPixel(nodes[node.Twin].Position), MathF.Max(scale * 2f, 0.8f), new Vector3(0.25f, 0.45f, 0.75f));
            }

            foreach (TalentNode node in nodes)
            {
                Vector3 hue = node.Region >= 0 ? TalentContent.Regions[node.Region].Colour : new Vector3(0.95f, 0.9f, 0.7f);
                float radius = node.Kind switch
                {
                    TalentKind.Keystone => 34f,
                    TalentKind.Root => 30f,
                    TalentKind.Gate => 26f,
                    TalentKind.Notable => 21f,
                    _ => node.Travel ? 9f : 12f,
                } * scale;
                radius = MathF.Max(radius, 1.2f);
                Vector3 fill = node.Travel && !node.Highway ? hue * 0.45f : node.Highway ? new Vector3(0.9f, 0.8f, 0.5f) : hue;
                canvas.Disc(ToPixel(node.Position), radius, fill);
                if (node.Kind is TalentKind.Keystone or TalentKind.Gate) canvas.Ring(ToPixel(node.Position), radius + 3f * scale + 1f, MathF.Max(3f * scale, 1f), node.Kind == TalentKind.Gate ? new Vector3(0.5f, 0.75f, 1f) : new Vector3(1f, 0.85f, 0.4f));
                if (node.Kind == TalentKind.Notable) canvas.Ring(ToPixel(node.Position), radius, MathF.Max(2f * scale, 0.8f), new Vector3(0.95f, 0.9f, 0.75f));
            }

            foreach ((Vector2 a, Vector2 b) in Crossings()) canvas.Ring(ToPixel((a + b) * 0.5f), 14f, 3f, new Vector3(1f, 0.1f, 0.1f));
            foreach ((TalentNode a, TalentNode b) in Crowded(45f)) canvas.Ring(ToPixel((a.Position + b.Position) * 0.5f), 10f, 2f, new Vector3(1f, 0.2f, 1f));

            canvas.Save(path);
            Console.WriteLine($"{nodes.Length} nodes, {nodes.Sum(n => n.Links.Count) / 2} links, {span:0} units across at {scale:0.###} pixels a unit, written to {Path.GetFullPath(path)}");
            Console.WriteLine($"  {nodes.Count(n => n.Kind == TalentKind.Small && !n.Travel)} small, {nodes.Count(n => n.Travel && !n.Highway)} travel, {nodes.Count(n => n.Highway)} on roads, {nodes.Count(n => n.Kind == TalentKind.Notable)} notable, {nodes.Count(n => n.Kind == TalentKind.Keystone)} keystones, {nodes.Count(n => n.Kind == TalentKind.Gate)} gates");
            Console.WriteLine($"  {Crossings().Count()} links cross (red), {Crowded(45f).Count()} pairs of nodes closer than 45 (pink)");
            for (int i = 0; i < TalentContent.Regions.Length; i++) Console.WriteLine($"  {TalentContent.Regions[i].Name}: {nodes.Count(n => n.Region == i)} nodes, {nodes.Count(n => n.Region == i && n.Kind == TalentKind.Notable)} notable");
            return 0;
        }

        /// <summary>Every pair of links that cross each other, as their middles. Links on a circle are taken as their chords, which is near enough.</summary>
        public static IEnumerable<(Vector2, Vector2)> Crossings()
        {
            var links = new List<(TalentNode a, TalentNode b)>();
            foreach (TalentNode node in TalentTree.Nodes)
            {
                foreach (int link in node.Links)
                {
                    if (link > node.Index) links.Add((node, TalentTree.Nodes[link]));
                }
            }
            for (int i = 0; i < links.Count; i++)
            {
                for (int j = i + 1; j < links.Count; j++)
                {
                    (TalentNode a, TalentNode b) = links[i];
                    (TalentNode c, TalentNode d) = links[j];
                    if (a == c || a == d || b == c || b == d) continue;
                    if (!TalentTree.Intersect(a.Position, b.Position, c.Position, d.Position)) continue;
                    if (Verbose) Console.WriteLine($"    cross: {a.Id}-{b.Id} x {c.Id}-{d.Id}");
                    yield return ((a.Position + b.Position) * 0.5f, (c.Position + d.Position) * 0.5f);
                }
            }
        }

        public static IEnumerable<(TalentNode, TalentNode)> Crowded(float least)
        {
            TalentNode[] nodes = TalentTree.Nodes;
            for (int i = 0; i < nodes.Length; i++)
            {
                for (int j = i + 1; j < nodes.Length; j++)
                {
                    if ((nodes[i].Position - nodes[j].Position).LengthSquared >= least * least) continue;
                    if (Verbose) Console.WriteLine($"    close: {nodes[i].Id} {nodes[j].Id} {(nodes[i].Position - nodes[j].Position).Length:0}");
                    yield return (nodes[i], nodes[j]);
                }
            }
        }

        /// <summary>Just enough of a picture to draw a tree on: discs, rings, lines and arcs, all smoothed at the edge.</summary>
        private sealed class Canvas
        {
            private readonly int _width;
            private readonly int _height;
            private readonly Vector3[] _pixels;

            public Canvas(int width, int height, Vector3 background)
            {
                _width = width;
                _height = height;
                _pixels = new Vector3[width * height];
                Array.Fill(_pixels, background);
            }

            private void Blend(int x, int y, Vector3 colour, float coverage)
            {
                if (x < 0 || y < 0 || x >= _width || y >= _height || coverage <= 0f) return;
                int i = y * _width + x;
                _pixels[i] = Vector3.Lerp(_pixels[i], colour, MathF.Min(coverage, 1f));
            }

            private void Shape(Vector2 min, Vector2 max, Func<Vector2, float> distance, Vector3 colour)
            {
                for (int y = Math.Max((int)min.Y - 1, 0); y <= Math.Min((int)max.Y + 1, _height - 1); y++)
                {
                    for (int x = Math.Max((int)min.X - 1, 0); x <= Math.Min((int)max.X + 1, _width - 1); x++)
                    {
                        float d = distance(new Vector2(x + 0.5f, y + 0.5f));
                        Blend(x, y, colour, 0.5f - d);
                    }
                }
            }

            public void Disc(Vector2 at, float radius, Vector3 colour) =>
                Shape(at - new Vector2(radius), at + new Vector2(radius), p => (p - at).Length - radius, colour);

            public void Ring(Vector2 at, float radius, float width, Vector3 colour) =>
                Shape(at - new Vector2(radius + width), at + new Vector2(radius + width), p => MathF.Abs((p - at).Length - radius) - width * 0.5f, colour);

            public void Line(Vector2 a, Vector2 b, float width, Vector3 colour) =>
                Shape(Vector2.ComponentMin(a, b) - new Vector2(width), Vector2.ComponentMax(a, b) + new Vector2(width), p => TalentTree.DistanceToSegment(p, a, b) - width * 0.5f, colour);

            public void Arc(Vector2 centre, float radius, Vector2 a, Vector2 b, float width, Vector3 colour)
            {
                float from = MathF.Atan2(a.Y - centre.Y, a.X - centre.X);
                float to = MathF.Atan2(b.Y - centre.Y, b.X - centre.X);
                float sweep = MathF.IEEERemainder(to - from, MathF.Tau);
                int steps = Math.Max(2, (int)(MathF.Abs(sweep) * radius / 4f));
                Vector2 last = a;
                for (int i = 1; i <= steps; i++)
                {
                    float angle = from + sweep * i / steps;
                    Vector2 next = centre + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                    Line(last, next, width, colour);
                    last = next;
                }
            }

            public void Save(string path)
            {
                var bytes = new byte[_width * _height * 4];
                for (int i = 0; i < _pixels.Length; i++)
                {
                    bytes[i * 4] = (byte)(Math.Clamp(_pixels[i].X, 0f, 1f) * 255f);
                    bytes[i * 4 + 1] = (byte)(Math.Clamp(_pixels[i].Y, 0f, 1f) * 255f);
                    bytes[i * 4 + 2] = (byte)(Math.Clamp(_pixels[i].Z, 0f, 1f) * 255f);
                    bytes[i * 4 + 3] = 255;
                }
                PngWriter.Write(path, bytes, _width, _height);
            }
        }
    }
}
